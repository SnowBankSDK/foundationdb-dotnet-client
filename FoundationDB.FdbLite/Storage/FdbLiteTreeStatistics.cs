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

	/// <summary>Aggregate statistics of one committed tree generation, gathered by walking every page.</summary>
	/// <remarks>
	/// <para><see cref="WastedBytes"/> surfaces the per-page wasted-bytes counter (see
	/// <see cref="FdbLitePageHeader.GetWastedBytes"/>) as a per-GENERATION total. The counter itself is two
	/// bytes in each page's header: booked by the in-place mutations (a shrink's slack, a relocated value's
	/// vacated slot, a removed cell's entry and value), reset only when THAT page is rebuilt. No file-level
	/// number is stored anywhere - it exists only by summing the pages a generation can reach, which is what
	/// this walk does. Two generations of one store can therefore report different totals, since each reaches
	/// its own pages.</para>
	/// <para>Cost is one pass over every page of the generation: an inspection call for probes, tests and
	/// space diagnostics, not a hot-path one. Measure under a read pin (or through
	/// <see cref="FdbLiteEngine.MeasureTreeStatistics"/>, which pins for you).</para>
	/// </remarks>
	public readonly record struct FdbLiteTreeStatistics(
		int InternalPages,
		int LeafPages,
		long CellCount,
		long WastedBytes,
		int MaxWastedBytesPerPage,
		long FreeGapBytes,
		long LeafLiveBytes)
	{

		private struct Accumulator
		{
			public int InternalPages;
			public int LeafPages;
			public int MaxWastedBytesPerPage;
			public long CellCount;
			public long WastedBytes;
			public long FreeGapBytes;
			public long LeafLiveBytes;
		}

		/// <summary>Walks the generation rooted at <paramref name="root"/> and aggregates its statistics (all zeroes for an empty tree).</summary>
		public static FdbLiteTreeStatistics Measure(IFdbLitePager pager, uint root)
		{
			Contract.NotNull(pager);
			var acc = default(Accumulator);
			if (root != 0)
			{
				Walk(pager, root, 0, ref acc);
			}
			return new(acc.InternalPages, acc.LeafPages, acc.CellCount, acc.WastedBytes, acc.MaxWastedBytesPerPage, acc.FreeGapBytes, acc.LeafLiveBytes);
		}

		/// <summary>Walks the generation rooted at <paramref name="root"/> and reports every LEAF's live bytes.</summary>
		/// <param name="visit">Called once per leaf with its fill-oriented live bytes.</param>
		/// <remarks>
		/// The per-leaf form of <see cref="Measure"/>, for the questions an aggregate cannot answer. A mean fill
		/// of 68% is produced equally by every leaf sitting at 68% and by half of them sitting at 50% while the
		/// other half sit at 85%, and those two imply different defects: the second is splits leaving half-pages
		/// behind, the first is not. Same cost and same walk as <see cref="Measure"/>; a diagnostic, not a hot
		/// path, and it wants a read pin exactly as that one does.
		/// </remarks>
		public static void VisitLeaves(IFdbLitePager pager, uint root, Action<long> visit)
		{
			Contract.NotNull(pager);
			Contract.NotNull(visit);
			if (root == 0)
			{
				return;
			}
			VisitLeaf(pager, root, 0, visit);
		}

		private static void VisitLeaf(IFdbLitePager pager, uint pageId, int depth, Action<long> visit)
		{
			Contract.Requires(depth <= 32, "tree deeper than any legal geometry allows");
			var page = pager.ReadBlocks(pageId, pager.Geometry.BlocksPerPage);
			if (FdbLitePageHeader.GetPageType(page) == FdbLitePageType.Leaf)
			{
				visit(FdbLiteTreePage.LeafLiveBytes(page));
				return;
			}
			int children = FdbLiteTreePage.GetChildCount(page);
			for (int i = 0; i < children; i++)
			{
				VisitLeaf(pager, FdbLiteTreePage.GetChild(page, i), depth + 1, visit);
			}
		}

		private static void Walk(IFdbLitePager pager, uint pageId, int depth, ref Accumulator acc)
		{
			Contract.Requires(depth <= 32, "tree deeper than any legal geometry allows");
			var page = pager.ReadBlocks(pageId, pager.Geometry.BlocksPerPage);
			int count = FdbLitePageHeader.GetCellCount(page);

			if (FdbLitePageHeader.GetPageType(page) == FdbLitePageType.Leaf)
			{
				acc.LeafPages++;
				acc.CellCount += count;
				int pageWaste = FdbLitePageHeader.GetWastedBytes(page);
				acc.WastedBytes += pageWaste;
				if (pageWaste > acc.MaxWastedBytesPerPage) { acc.MaxWastedBytesPerPage = pageWaste; }
				acc.FreeGapBytes += FdbLiteTreePage.LeafFreeGap(page);
				acc.LeafLiveBytes += FdbLiteTreePage.LeafLiveBytes(page);
				return;
			}

			acc.InternalPages++;
			int children = FdbLiteTreePage.GetChildCount(page);
			for (int i = 0; i < children; i++)
			{
				Walk(pager, FdbLiteTreePage.GetChild(page, i), depth + 1, ref acc);
			}
		}

	}

	/// <summary>Tree-wide totals of one committed generation, read from its ROOT page's aggregate block in O(1).</summary>
	/// <remarks>
	/// <para>Maintained exactly by the dirty-chain invariant (see <see cref="FdbLitePageHeader"/>): no walk, no sampling, no estimation machinery. <see cref="LeafLiveBytes"/> counts the LEAVES' fill-oriented live bytes only (internal pages are ~1 in fanout and are not occupancy the vacuum can reclaim).</para>
	/// <para><c>idealLeaves = ceil(LeafLiveBytes / (fillTarget * pageSize))</c> against <see cref="LeafCount"/> is a subtree's reclaim opportunity, which is what makes a threshold-guided vacuum descent cost O(hot paths). The logical byte totals are the exact FDB-cluster-style storage numbers (logical k/v bytes against file size).</para>
	/// </remarks>
	public readonly record struct FdbLiteTreeAggregates(
		ulong EntryCount,
		ulong LogicalKeyBytes,
		ulong LogicalValueBytes,
		ulong LeafLiveBytes,
		uint LeafCount,
		ulong ExtentBlocks = 0)
	{

		/// <summary>Reads the aggregates of the generation rooted at <paramref name="root"/> from its root page's header (0 = empty tree, all zeroes).</summary>
		public static FdbLiteTreeAggregates Read(IFdbLitePager pager, uint root)
		{
			Contract.NotNull(pager);
			if (root == 0)
			{
				return default;
			}
			var page = pager.ReadBlocks(root, pager.Geometry.BlocksPerPage);
			return ReadFrom(page);
		}

		/// <summary>Decodes the aggregate block of one page (a leaf derives its own live bytes from its v1 fields).</summary>
		internal static FdbLiteTreeAggregates ReadFrom(ReadOnlySpan<byte> page)
		{
			bool leaf = FdbLitePageHeader.GetPageType(page) == FdbLitePageType.Leaf;
			return new(
				EntryCount: FdbLitePageHeader.GetEntryCount(page),
				LogicalKeyBytes: FdbLitePageHeader.GetLogicalKeyBytes(page),
				LogicalValueBytes: FdbLitePageHeader.GetLogicalValueBytes(page),
				LeafLiveBytes: leaf ? (ulong) FdbLiteTreePage.LeafLiveBytes(page) : FdbLitePageHeader.GetSubtreeLiveBytes(page),
				LeafCount: leaf ? 1u : FdbLitePageHeader.GetLeafCount(page),
				ExtentBlocks: FdbLitePageHeader.GetExtentBlocks(page));
		}

	}

}
