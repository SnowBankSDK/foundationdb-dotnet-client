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

namespace FoundationDB.Storage.FdbLite.Tests
{
	using System.Buffers.Binary;
	using FoundationDB.Storage.FdbLite;

	/// <summary>One committed leaf as the space diagnostics see it: raw-layout aggregates plus its numeric key range.</summary>
	/// <remarks><see cref="LiveBytes"/> is the page footprint its cells would need freshly compacted (prefix included), which is what candidacy and merge sizing compare against the page size.</remarks>
	internal sealed record VacuumLeafInfo(uint PageId, int CellCount, long SumWholeKeyBytes, long SumValueBytes, long LiveBytes, long FirstKey, long LastKey);

	/// <summary>Every leaf of one committed generation, in key order grouped by direct parent, plus the id set for dirty detection by delta.</summary>
	internal sealed record VacuumTreeSnapshot(List<List<VacuumLeafInfo>> Groups, HashSet<uint> LeafIds);

	/// <summary>Raw-layout leaf analysis shared by the space-reclamation diagnostics (independent of the engine's own accessors, same approach as the page-accounting oracle).</summary>
	/// <remarks>The numeric key range restricts these helpers to workloads writing 8-byte big-endian keys, which every diagnostic in this family does; <see cref="ParseLeaf"/> asserts it rather than trusting it.</remarks>
	internal static class FdbLiteLeafAnalysis
	{

		/// <summary>Replicates LeafRunBytes: full page footprint of a run stored against <paramref name="lcp"/>.</summary>
		public static long RunBytes(int count, long sumWhole, long sumValue, int lcp)
		{
			int effective = count > 1 ? lcp : 0;
			return 128 + ((effective + 1) & ~1) + (sumWhole - ((long) count * effective)) + ((long) count * 9) + sumValue;
		}

		/// <summary>Longest common prefix of two keys, over their 8-byte big-endian forms.</summary>
		public static int Lcp(long a, long b)
		{
			Span<byte> ka = stackalloc byte[8];
			Span<byte> kb = stackalloc byte[8];
			BinaryPrimitives.WriteInt64BigEndian(ka, a);
			BinaryPrimitives.WriteInt64BigEndian(kb, b);
			int i = 0;
			while (i < 8 && ka[i] == kb[i]) { ++i; }
			return i;
		}

		public static VacuumLeafInfo ParseLeaf(ReadOnlySpan<byte> page, uint pageId)
		{
			int count = FdbLitePageHeader.GetCellCount(page);
			int prefixLen = FdbLitePageHeader.GetPrefixLength(page);
			var prefix = page.Slice(128, prefixLen);
			int slotsAt = 128 + ((prefixLen + 1) & ~1);
			// the directory reserves slots ahead of the cell count, so the key heap starts after the RESERVED span
			int keyBase = slotsAt + (Math.Max(FdbLitePageHeader.GetSlotCapacity(page), count) * 2);

			long sumWhole = 0, sumValue = 0;
			long first = 0, last = 0;
			Span<byte> whole = stackalloc byte[8];
			for (int i = 0; i < count; i++)
			{
				int entry = keyBase + BinaryPrimitives.ReadUInt16LittleEndian(page[(slotsAt + (i * 2))..]);
				int keyLen = BinaryPrimitives.ReadUInt16LittleEndian(page[entry..]);
				int f = entry + 2 + keyLen;
				sumWhole += prefixLen + keyLen;
				sumValue += BinaryPrimitives.ReadUInt16LittleEndian(page[(f + 2)..]);
				if (i == 0 || i == count - 1)
				{
					Assert.That(prefixLen + keyLen, Is.EqualTo(8), "these workloads write 8-byte keys only; the numeric analysis depends on it");
					prefix.CopyTo(whole);
					page.Slice(entry + 2, keyLen).CopyTo(whole[prefixLen..]);
					long k = BinaryPrimitives.ReadInt64BigEndian(whole);
					if (i == 0) { first = k; }
					last = k;
				}
			}
			if (count == 1) { last = first; }
			long live = RunBytes(count, sumWhole, sumValue, Lcp(first, last));
			return new(pageId, count, sumWhole, sumValue, live, first, last);
		}

		/// <summary>Walks the committed tree: leaves in key order grouped by direct parent, plus the id set for dirty detection.</summary>
		/// <remarks>Dirty leaves of a generation are the ids ABSENT from the previous generation's snapshot: any cross-generation touch relocates the page (copy-on-write), so the id delta is exact and needs no generation bookkeeping (the header stamp, re-stamped at seal, now also identifies the publishing generation, but the delta predates that and stands on its own).</remarks>
		public static VacuumTreeSnapshot Snapshot(IFdbLitePager pager, uint root)
		{
			var groups = new List<List<VacuumLeafInfo>>();
			var ids = new HashSet<uint>();
			if (root != 0)
			{
				Walk(pager, root, groups, ids);
			}
			return new(groups, ids);

			static void Walk(IFdbLitePager pager, uint pageId, List<List<VacuumLeafInfo>> groups, HashSet<uint> ids)
			{
				var page = pager.ReadBlocks(pageId, pager.Geometry.BlocksPerPage).ToArray();
				if (FdbLitePageHeader.GetPageType(page) == FdbLitePageType.Leaf)
				{ // a root that is itself a leaf: a degenerate single-leaf group
					groups.Add([ ParseLeaf(page, pageId) ]);
					ids.Add(pageId);
					return;
				}

				int count = FdbLitePageHeader.GetCellCount(page);
				var children = new uint[count + 1];
				children[0] = BinaryPrimitives.ReadUInt32LittleEndian(page.AsSpan(128));
				for (int i = 0; i < count; i++)
				{ // internal slots start at 132 (leftmost child u32 after the header; internal pages strip no prefix)
					int off = BinaryPrimitives.ReadUInt16LittleEndian(page.AsSpan(132 + (i * 2)));
					children[i + 1] = BinaryPrimitives.ReadUInt32LittleEndian(page.AsSpan(off));
				}

				var firstChild = pager.ReadBlocks(children[0], pager.Geometry.BlocksPerPage);
				if (FdbLitePageHeader.GetPageType(firstChild) == FdbLitePageType.Leaf)
				{ // all siblings of a leaf are leaves: this internal page parents one group
					var group = new List<VacuumLeafInfo>(children.Length);
					foreach (var child in children)
					{
						group.Add(ParseLeaf(pager.ReadBlocks(child, pager.Geometry.BlocksPerPage), child));
						ids.Add(child);
					}
					groups.Add(group);
					return;
				}
				foreach (var child in children)
				{
					Walk(pager, child, groups, ids);
				}
			}
		}

	}

}
