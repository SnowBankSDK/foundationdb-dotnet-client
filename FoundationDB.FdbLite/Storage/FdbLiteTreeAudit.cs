#region Copyright (c) 2023-2026 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace FoundationDB.Storage.FdbLite
{

	/// <summary>Structural audit of a committed tree generation: checks each page against what its ANCESTORS say it must contain.</summary>
	/// <remarks>
	/// <para>This exists because a page-internal check cannot arbitrate a corrupt page. Whether keys ascend, whether the header agrees with the cells, whether a rebuild preserved what it was handed - all of those are computed FROM the page under suspicion, so a defect that still satisfies them is invisible no matter how carefully the check is written. A key that lost its last bytes is the standard example: it is a proper prefix of every key after it, so it sorts exactly where a search expects it and every ordering check passes.</para>
	/// <para>A B+tree carries its own oracle. The separators on either side of a child were written by the PARENT, when that child was created, and they state what the child's key range must be. Nothing inside the child contributes to them, so comparing the two can settle what neither can settle alone. That is the check here, and it is the class of check that finds corruption a page cannot self-report.</para>
	/// <para>Cost is one pass over every page, so this belongs in a verify mode, a test, or a debugging session - not on the read path.</para>
	/// </remarks>
	public static class FdbLiteTreeAudit
	{

		/// <summary>Audits the generation rooted at <paramref name="root"/> and returns what is wrong with it, empty when it is sound.</summary>
		/// <param name="pager">Pager holding the generation (the writer's <see cref="FdbLiteTreeWriter.PagerView"/> to audit a generation being built)</param>
		/// <param name="root">Root page of the generation to walk</param>
		/// <param name="maxProblems">Stops walking once this many problems are found, since one corrupt page usually reports many</param>
		public static List<string> Check(IFdbLitePager pager, uint root, int maxProblems = 16)
		{
			Contract.NotNull(pager);
			Contract.Positive(maxProblems);

			var problems = new List<string>();
			if (root != 0)
			{
				Walk(pager, root, null, null, 0, problems, maxProblems);
			}
			return problems;
		}

		/// <summary>Walks one page, given the bounds its ancestors impose on it: every key must sit in <c>[lower, upper)</c>.</summary>
		private static void Walk(IFdbLitePager pager, uint pageId, byte[]? lower, byte[]? upper, int depth, List<string> problems, int maxProblems)
		{
			if (problems.Count >= maxProblems || depth > 32)
			{
				return;
			}

			var page = pager.ReadBlocks(pageId, pager.Geometry.BlocksPerPage);
			int count = FdbLitePageHeader.GetCellCount(page);

			if (FdbLitePageHeader.GetPageType(page) == FdbLitePageType.Leaf)
			{
				// the two heaps grow towards each other, so they crossing is the page holding more than it can:
				// checked FIRST because every key it reports past that point is meaningless
				int keyEnd = FdbLiteTreePage.LeafKeyBase(page) + FdbLitePageHeader.GetKeyAreaLength(page);
				int valueFloor = FdbLitePageHeader.GetCellAreaOffset(page);
				if (count > 0 && valueFloor > 0 && keyEnd > valueFloor)
				{
					problems.Add($"leaf {pageId} OVERFLOWS: {count} cells, key heap ends at {keyEnd} but the value heap starts at {valueFloor} ({keyEnd - valueFloor} bytes too many) with prefixLen={FdbLitePageHeader.GetPrefixLength(page)} in a {pager.Geometry.PageSize} byte page");
				}

				// the stored aggregate block against a recount from the cells: the aggregates are maintained
				// incrementally by every mutation path, so a drift here names the path that miscounted
				long keyBytes = 0, valueBytes = 0;
				for (int i = 0; i < count; i++)
				{
					keyBytes += FdbLitePageHeader.GetPrefixLength(page) + FdbLiteTreePage.LeafKeyExtent(page, i).Length;
					valueBytes += FdbLiteTreePage.LeafLogicalValueLength(FdbLiteTreePage.GetLeafStoredValue(page, i), FdbLiteTreePage.GetLeafFlags(page, i));
				}
				if (FdbLitePageHeader.GetEntryCount(page) != (ulong) count
				 || FdbLitePageHeader.GetLogicalKeyBytes(page) != (ulong) keyBytes
				 || FdbLitePageHeader.GetLogicalValueBytes(page) != (ulong) valueBytes
				 || FdbLitePageHeader.GetLeafCount(page) != 1
				 || FdbLitePageHeader.GetSubtreeLiveBytes(page) != 0)
				{
					problems.Add($"leaf {pageId} AGGREGATES DRIFTED: stored entries={FdbLitePageHeader.GetEntryCount(page)} keyBytes={FdbLitePageHeader.GetLogicalKeyBytes(page)} valueBytes={FdbLitePageHeader.GetLogicalValueBytes(page)} leafCount={FdbLitePageHeader.GetLeafCount(page)} subtreeLive={FdbLitePageHeader.GetSubtreeLiveBytes(page)}, recounted entries={count} keyBytes={keyBytes} valueBytes={valueBytes}");
				}

				byte[]? previous = null;
				for (int i = 0; i < count && problems.Count < maxProblems; i++)
				{
					var key = WholeLeafKey(page, i);

					// THE CROSS-LEVEL CHECK. The parent derived these bounds when it created this page, so a key
					// outside them is a disagreement between two independently written things - which is exactly
					// what a page-internal check can never produce.
					if (lower is not null && key.AsSpan().SequenceCompareTo(lower) < 0)
					{
						problems.Add($"leaf {pageId} cell {i}/{count} (prefixLen={FdbLitePageHeader.GetPrefixLength(page)}) is BELOW the separator its parent routes by: {Describe(key)} < {Describe(lower)}");
					}
					if (upper is not null && key.AsSpan().SequenceCompareTo(upper) >= 0)
					{
						problems.Add($"leaf {pageId} cell {i}/{count} is at or ABOVE its parent's next separator: {Describe(key)} >= {Describe(upper)}");
					}
					if (previous is not null && previous.AsSpan().SequenceCompareTo(key) >= 0)
					{
						problems.Add($"leaf {pageId} cells {i - 1},{i} are out of order: {Describe(previous)} then {Describe(key)}");
					}
					previous = key;

					if ((FdbLiteTreePage.GetLeafFlags(page, i) & FdbLiteTreePage.FlagValueIsExtent) != 0)
					{ // the read path never verifies extent payloads (tree pages are checksummed on first touch,
					  // extents are raw blocks), so the audit is where payload bit-rot gets caught
						var (start, blockCount, totalLength, checksum) = FdbLiteTreePage.GetLeafExtentDescriptor(page, i);
						var payload = pager.ReadBlocks(start, blockCount)[..(int) totalLength];
						if (System.IO.Hashing.XxHash3.HashToUInt64(payload, unchecked((long) start)) != checksum)
						{
							problems.Add($"leaf {pageId} cell {i}: extent at block {start} ({totalLength} bytes) fails its checksum");
						}
					}
				}
				return;
			}

			// the stored subtree sums against the children's own headers: exactness across generations rests on
			// the dirty-chain invariant, so a drift here means a path changed a child without dirtying its chain
			{
				ulong entries = 0, keyBytes = 0, valueBytes = 0, liveBytes = 0;
				uint leaves = 0;
				int childCount = FdbLiteTreePage.GetChildCount(page);
				for (int i = 0; i < childCount; i++)
				{
					var agg = FdbLiteTreeAggregates.ReadFrom(pager.ReadBlocks(FdbLiteTreePage.GetChild(page, i), pager.Geometry.BlocksPerPage));
					entries += agg.EntryCount;
					keyBytes += agg.LogicalKeyBytes;
					valueBytes += agg.LogicalValueBytes;
					liveBytes += agg.LeafLiveBytes;
					leaves += agg.LeafCount;
				}
				if (FdbLitePageHeader.GetEntryCount(page) != entries
				 || FdbLitePageHeader.GetLogicalKeyBytes(page) != keyBytes
				 || FdbLitePageHeader.GetLogicalValueBytes(page) != valueBytes
				 || FdbLitePageHeader.GetSubtreeLiveBytes(page) != liveBytes
				 || FdbLitePageHeader.GetLeafCount(page) != leaves)
				{
					problems.Add($"internal {pageId} AGGREGATES DRIFTED: stored entries={FdbLitePageHeader.GetEntryCount(page)} keyBytes={FdbLitePageHeader.GetLogicalKeyBytes(page)} valueBytes={FdbLitePageHeader.GetLogicalValueBytes(page)} subtreeLive={FdbLitePageHeader.GetSubtreeLiveBytes(page)} leafCount={FdbLitePageHeader.GetLeafCount(page)}, children sum to entries={entries} keyBytes={keyBytes} valueBytes={valueBytes} subtreeLive={liveBytes} leafCount={leaves}");
				}
			}

			// separators must ascend, and each child inherits the pair around it as its own bounds
			byte[]? previousSeparator = null;
			for (int i = 0; i < count && problems.Count < maxProblems; i++)
			{
				var separator = FdbLiteTreePage.GetSeparator(page, i).ToArray();
				if (previousSeparator is not null && previousSeparator.AsSpan().SequenceCompareTo(separator) >= 0)
				{
					problems.Add($"internal {pageId} separators {i - 1},{i} are out of order: {Describe(previousSeparator)} then {Describe(separator)}");
				}
				if (lower is not null && separator.AsSpan().SequenceCompareTo(lower) < 0)
				{
					problems.Add($"internal {pageId} separator {i} is below its own parent's bound: {Describe(separator)} < {Describe(lower)}");
				}
				previousSeparator = separator;
			}

			int children = FdbLiteTreePage.GetChildCount(page);
			for (int i = 0; i < children && problems.Count < maxProblems; i++)
			{
				// child i is bounded below by the separator before it and above by the one after it: the first child
				// inherits this page's lower bound, the last inherits its upper
				var childLower = i == 0 ? lower : FdbLiteTreePage.GetSeparator(page, i - 1).ToArray();
				var childUpper = i < count ? FdbLiteTreePage.GetSeparator(page, i).ToArray() : upper;
				Walk(pager, FdbLiteTreePage.GetChild(page, i), childLower, childUpper, depth + 1, problems, maxProblems);
			}
		}

		/// <summary>Whole key of a leaf cell, the page prefix put back in front of the stored suffix.</summary>
		private static byte[] WholeLeafKey(ReadOnlySpan<byte> page, int cellIndex)
		{
			var prefix = FdbLiteTreePage.GetPagePrefix(page, isInternal: false);
			var suffix = FdbLiteTreePage.GetLeafKey(page, cellIndex);
			var whole = new byte[prefix.Length + suffix.Length];
			prefix.CopyTo(whole);
			suffix.CopyTo(whole.AsSpan(prefix.Length));
			return whole;
		}

		/// <summary>Hex, with the length, because a key of the WRONG LENGTH is the interesting case and hex alone hides it.</summary>
		private static string Describe(byte[] key) => $"{Convert.ToHexString(key)}({key.Length}B)";

	}

}
