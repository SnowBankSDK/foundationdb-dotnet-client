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

	/// <summary>One leaf page rendered by a graft: the page, and where in the run its first cell (which IS its separator) lives.</summary>
	/// <remarks>The separator is BORROWED from the run rather than copied out of it, which is the whole difference with the split path's <c>(byte[] Separator, uint PageId)</c> pairs: a graft emits hundreds of pages, so one array per page plus a growing list is exactly the cost the bulk path exists to avoid.</remarks>
	internal readonly record struct FdbLiteGraftedPage(int RunIndex, uint PageId);

	public sealed partial class FdbLiteTreeWriter
	{

		/// <summary>Renders an ordered run of cells into finished leaf pages, each packed to <paramref name="fillCeiling"/> live bytes.</summary>
		/// <param name="cells">Cells in strictly ascending key order, each carrying its own buffer (a grafted cell is built, not gathered from a page).</param>
		/// <param name="fillCeiling">Live bytes a page is packed to before the next one is started; clamped to the page size.</param>
		/// <param name="reusePageId">Page whose id the FIRST output may take over, or 0 for all-new pages.</param>
		/// <param name="sourcePage">Page the buffer-less cells were gathered from, or empty when every cell carries its own buffer.</param>
		/// <param name="output">Receives one entry per emitted page, in key order. Must hold at least <c>cells.Length</c> entries.</param>
		/// <returns>Number of pages emitted, i.e. the number of <paramref name="output"/> entries written.</returns>
		/// <remarks>
		/// <para>The whole point of the bulk path: page boundaries are chosen with the ENTIRE run in hand, so every
		/// page but the last comes out full. Feeding the same keys through <see cref="Insert"/> cannot do this, because
		/// a split decides where to cut before the writer knows which keys still arrive.</para>
		/// <para>Sizing deliberately goes through <see cref="LeafPartEnd"/>, the same boundary rule <see cref="WriteCells"/>
		/// uses for its split parts, rather than a second rule of its own: this renderer hands each range to
		/// <see cref="WriteCells"/> as ONE page, so a sizing that disagreed by a byte would make that call split again -
		/// and the extra sibling would be dropped on the floor here. Agreement is what makes the postcondition below hold.</para>
		/// </remarks>
		internal int RenderRun(ReadOnlySpan<CellRef> cells, int fillCeiling, uint reusePageId, ReadOnlySpan<byte> sourcePage, FdbLiteGraftedPage[] output)
		{
			Contract.Requires(cells.Length > 0);
			Contract.Requires(fillCeiling > 0);
			Contract.Requires(output.Length >= cells.Length, "a page holds at least one cell, so cells.Length bounds the page count");

			int pageSize = this.Pager.Geometry.PageSize;
			long ceiling = Math.Min(fillCeiling, pageSize);

			// A run whose cells all carry their own buffer has no page prefix to put back, and that is the
			// `default` case: sourcePrefixLength stays 0, MaterializeKey never touches the scratch, and none is
			// rented. A GRAFT is not that case - its run is bracketed by cells gathered from the boundary leaf,
			// which hold only their suffix - so the sizing has to be told which page and which prefix.
			int sourcePrefixLength = sourcePage.Length > 0 && FdbLitePageHeader.GetCellCount(sourcePage) > 0
				? FdbLitePageHeader.GetPrefixLength(sourcePage)
				: 0;
			var scratch = sourcePrefixLength > 0 ? ArrayPool<byte>.Shared.Rent(FdbLiteTreePage.MaxKeyLength) : null;
			try
			{
				int pages = 0;
				int start = 0;
				while (start < cells.Length)
				{
					int end = LeafPartEnd(cells, start, sourcePage, sourcePrefixLength, ceiling, pageSize, scratch.AsSpan(), out _);

					uint reuse = pages == 0 ? reusePageId : 0;
					var part = WriteCells(reuse, isInternal: false, leftmostChild: 0, sourcePage, cells[start..end]);
					Contract.Ensures(!part.Split, "the boundary was chosen to fit one page, so WriteCells must not split it (a dropped sibling would silently orphan cells)");

					output[pages++] = new(start, part.FirstId);
					start = end;
				}
				return pages;
			}
			finally
			{
				if (scratch != null) { ArrayPool<byte>.Shared.Return(scratch); }
			}
		}

		/// <summary>Partitions a leaf's cells at <paramref name="key"/>: everything strictly below, and everything at or above.</summary>
		/// <remarks>
		/// <para>The split is at the INSERTION POINT, not at the page's midpoint, which is what makes a graft's two boundary
		/// pages the only ones it disturbs. Either side may come back empty, and that is the normal case rather than a
		/// degenerate one: an empty left side is a run landing before every key in the page, an empty right side is a
		/// run landing after all of them, and those are the two edge situations the graft handles with the same code.</para>
		/// <para>The returned <see cref="CellRef"/>s carry no buffer of their own: <see cref="CellRef.OfLeafPage"/> leaves
		/// <c>Buffer</c> null and records bare offsets into <paramref name="leafId"/>'s page, unlike every other gatherer in
		/// this class, which resolves its cells synchronously while the page span is still in hand. Whatever resolves these
		/// cells later must be handed the SAME page bytes this call read. For a dirty page <see cref="ReadPage"/> returns the
		/// live mutable array, so the caller must not let <paramref name="leafId"/> be rewritten while it still holds these
		/// cells, or they silently point at different bytes than the ones they were read from.</para>
		/// </remarks>
		internal (CellRef[] Below, CellRef[] AtOrAbove) SplitCellsAt(uint leafId, ReadOnlySpan<byte> key)
		{
			var page = ReadPage(leafId);
			int count = FdbLitePageHeader.GetCellCount(page);
			int at = FdbLiteTreePage.FindLeafSlot(page, key, out _);

			var below = new CellRef[at];
			var above = new CellRef[count - at];
			for (int i = 0; i < at; i++)
			{
				below[i] = CellRef.OfLeafPage(page, i);
			}
			for (int i = at; i < count; i++)
			{
				above[i - at] = CellRef.OfLeafPage(page, i);
			}
			return (below, above);
		}

		/// <summary>Grafts an ordered run into a range that holds no existing keys, rewriting only the boundary page.</summary>
		/// <param name="leafId">Leaf the run's range falls inside.</param>
		/// <param name="begin">First key of the run, used to find the insertion point in <paramref name="leafId"/>.</param>
		/// <param name="run">The run's cells, in strictly ascending key order.</param>
		/// <param name="fillCeiling">Live bytes each output page is packed to.</param>
		/// <param name="output">Receives one entry per emitted page, in key order. Must hold at least <c>run.Length</c> plus the boundary leaf's cell count entries.</param>
		/// <returns>Number of pages emitted.</returns>
		/// <remarks>
		/// <para>The boundary page's cells join the run rather than being preserved beside it, which is what lets the two
		/// ends come out packed instead of half empty: the head of the run tops up what was below the insertion point
		/// and the tail is completed by what was above it. Everything between is written whole.</para>
		/// <para>The ascent is done HERE rather than left to the caller, because the two things it reads - the merged
		/// cell list and the boundary page - exist only inside this call: every emitted page's separator is read
		/// straight out of them, and handing them back out would mean handing out a pooled array and a page image
		/// whose lifetime the caller cannot see.</para>
		/// </remarks>
		internal int GraftIntoGap(uint leafId, ReadOnlySpan<byte> begin, ReadOnlySpan<CellRef> run, int fillCeiling, FdbLiteGraftedPage[] output)
		{
			Contract.Requires(run.Length > 0);

			Span<uint> pathPages = stackalloc uint[MaxDepth];
			Span<int> pathChildren = stackalloc int[MaxDepth];
			uint descended = DescendToLeaf(begin, pathPages, pathChildren, out int depth);
			Contract.Requires(descended == leafId, "the run's first key must fall in the leaf the caller named");

			// CELLS GATHERED FROM A PAGE DO NOT CARRY THAT PAGE. CellRef.OfLeafPage leaves Buffer null and
			// stores bare offsets, so resolving one needs the SAME page bytes handed back. Two consequences,
			// both load-bearing here:
			//  - the source page must be passed down to WriteCells, or below/above resolve against nothing;
			//  - the render must NOT reuse leafId, because for a dirty page ReadPage hands back the live
			//    mutable array and the first emitted page would overwrite the very bytes the remaining cells
			//    still point at, mid-render.
			var page = ReadPage(leafId);
			var (below, above) = SplitCellsAt(leafId, begin);

			if (above.Length > 0)
			{ // an unsorted merge would render a silently mis-ordered tree, which this code must never produce
				var runLastKey = WholeKeyOf(run[^1], page);
				var aboveFirstKey = WholeKeyOf(above[0], page);
				Contract.Requires(runLastKey.AsSpan().SequenceCompareTo(aboveFirstKey) < 0, "the run's last key must sort strictly below the first key of the gap's upper side, or the caller handed GraftIntoGap a run that overruns the gap");
			}

			var all = ArrayPool<CellRef>.Shared.Rent(below.Length + run.Length + above.Length);
			try
			{
				below.CopyTo(all.AsSpan(0));
				run.CopyTo(all.AsSpan(below.Length));
				above.CopyTo(all.AsSpan(below.Length + run.Length));

				int total = below.Length + run.Length + above.Length;
				int pages = RenderRun(all.AsSpan(0, total), fillCeiling, reusePageId: 0, sourcePage: page, output);

				AscendPatchGrafted(pathPages, pathChildren, depth - 1, leafId, page, all.AsSpan(0, total), output.AsSpan(0, pages));

				// the run owns its range, so every one of its cells is a key the tree did not hold; the boundary
				// page's own cells were carried over, not re-added, so they are already counted
				this.KeyCountDelta += run.Length;
				// the boundary leaf is gone and every emitted page is new, so nothing the cursor remembers survives
				this.CursorLeaf = 0;
				return pages;
			}
			finally
			{ // cleared: a CellRef holds a byte[] reference, and a pooled array must not pin the run's buffers
				ArrayPool<CellRef>.Shared.Return(all, clearArray: true);
			}
		}

		/// <summary>Ascends from <paramref name="fromLevel"/>, splicing a graft's emitted pages in place of the boundary leaf; grows the root as needed.</summary>
		/// <param name="originalChildId">The boundary leaf the graft replaced, retired here.</param>
		/// <param name="sourcePage">The boundary leaf's page, which the buffer-less cells of <paramref name="cells"/> resolve against.</param>
		/// <param name="cells">The merged cell list the pages were rendered from.</param>
		/// <param name="pages">The emitted pages, in key order.</param>
		/// <remarks>
		/// The twin of <see cref="AscendPatch"/>, and it exists for one reason: a split hands its parent a
		/// <c>(byte[] Separator, uint PageId)</c> per sibling, which a graft emitting hundreds of pages would pay
		/// for hundreds of times. Here each page's separator is READ from <c>cells[page.RunIndex]</c> - the first
		/// cell of that page IS its separator - so nothing is copied out of the run. Only the first level differs;
		/// above it a rebuilt internal page splits like any other, so the ordinary ascent takes over.
		/// </remarks>
		internal void AscendPatchGrafted(ReadOnlySpan<uint> pathPages, ReadOnlySpan<int> pathChildren, int fromLevel, uint originalChildId, ReadOnlySpan<byte> sourcePage, ReadOnlySpan<CellRef> cells, ReadOnlySpan<FdbLiteGraftedPage> pages)
		{
			Contract.Requires(pages.Length > 0);

			if (fromLevel < 0)
			{ // the boundary leaf WAS the whole tree: the emitted pages become a new root level (which may itself split)
				var grown = pages.Length == 1
					? new RebuildResult(pages[0].PageId, null)
					: new RebuildResult(pages[0].PageId, GraftedSiblings(sourcePage, cells, pages));
				while (grown.Split)
				{
					grown = BuildRootLevel(grown);
				}
				this.Root = grown.FirstId;
			}
			else
			{
				var outcome = RebuildInternalGrafted(pathPages[fromLevel], pathChildren[fromLevel], sourcePage, cells, pages);
				AscendPatch(pathPages, pathChildren, fromLevel - 1, pathPages[fromLevel], outcome);
			}

			// Nothing else retires the boundary leaf: on the rebuild path WritePage frees the old page as it copies
			// it, and a graft deliberately never hands it one (see GraftIntoGap). LAST, because FreePage recycles
			// the page's buffer and the separators above were still being read out of it.
			FreePage(originalChildId);
		}

		/// <summary>Materialises a graft's separators as owned arrays, for the one path that cannot read them in place: growing a new root level.</summary>
		private static List<(byte[] Separator, uint PageId)> GraftedSiblings(ReadOnlySpan<byte> sourcePage, ReadOnlySpan<CellRef> cells, ReadOnlySpan<FdbLiteGraftedPage> pages)
		{
			var siblings = new List<(byte[], uint)>(pages.Length - 1);
			for (int i = 1; i < pages.Length; i++)
			{
				siblings.Add((WholeKeyOf(cells[pages[i].RunIndex], sourcePage), pages[i].PageId));
			}
			return siblings;
		}

		/// <summary>Rebuilds the graft's parent: the descended child pointer becomes the first emitted page, and each further page inserts one separator cell after it.</summary>
		/// <remarks>The body mirrors <see cref="RebuildInternal"/> cell for cell; the ONE difference is where a sibling's separator comes from - <c>cells[page.RunIndex]</c> read in place, instead of a <c>byte[]</c> the splitter copied out.</remarks>
		private RebuildResult RebuildInternalGrafted(uint pageId, int childIndex, ReadOnlySpan<byte> sourcePage, ReadOnlySpan<CellRef> runCells, ReadOnlySpan<FdbLiteGraftedPage> pages)
		{
			var page = ReadPage(pageId);
			int cellCount = FdbLitePageHeader.GetCellCount(page);
			int inserted = pages.Length - 1;

			byte[]? patchScratch = null;
			var siblingScratch = inserted == 0 ? [ ] : new byte[inserted][];
			var keyScratch = ArrayPool<byte>.Shared.Rent(FdbLiteTreePage.MaxKeyLength);
			int cellTotal = cellCount + inserted;
			var cells = ArrayPool<CellRef>.Shared.Rent(cellTotal);
			try
			{
				// a cell gathered from the boundary leaf holds only its suffix, and a separator handed to a PARENT
				// must be the whole key or the tree mis-sorts instead of failing loudly
				var sourcePrefix = sourcePage.Length > 0 && FdbLitePageHeader.GetCellCount(sourcePage) > 0
					? FdbLiteTreePage.GetPagePrefix(sourcePage, isInternal: false)
					: default;

				uint leftmost = FdbLiteTreePage.GetLeftmostChild(page);

				var patchedCell = default(CellRef);
				if (childIndex == 0)
				{
					leftmost = pages[0].PageId;
				}
				else
				{
					var original = FdbLiteTreePage.GetInternalCell(page, childIndex - 1);
					patchScratch = ArrayPool<byte>.Shared.Rent(original.Length);
					original.CopyTo(patchScratch);
					FdbLiteTreePage.PatchInternalCellChild(patchScratch, pages[0].PageId);
					patchedCell = CellRef.OfInternalBuffer(patchScratch, original.Length);
				}

				int w = 0;
				for (int i = 0; i <= cellCount; i++)
				{
					if (i == childIndex)
					{ // the grafted pages slot in right after the descended child
						for (int s = 0; s < inserted; s++)
						{
							var grafted = pages[s + 1];
							var separator = MaterializeKey(runCells[grafted.RunIndex], sourcePage, sourcePrefix, keyScratch.AsSpan(0, FdbLiteTreePage.MaxKeyLength));
							siblingScratch[s] = ArrayPool<byte>.Shared.Rent(6 + separator.Length);
							int len = FdbLiteTreePage.BuildInternalCell(siblingScratch[s], grafted.PageId, separator).Length;
							cells[w++] = CellRef.OfInternalBuffer(siblingScratch[s], len);
						}
					}
					if (i < cellCount)
					{
						cells[w++] = (i == childIndex - 1) ? patchedCell : CellRef.OfInternalPage(FdbLiteTreePage.GetInternalCellExtent(page, i));
					}
				}
				Contract.Debug.Assert(w == cellTotal);

				return WriteCells(pageId, isInternal: true, leftmost, page, cells.AsSpan(0, cellTotal));
			}
			finally
			{
				ArrayPool<CellRef>.Shared.Return(cells, clearArray: true);
				ArrayPool<byte>.Shared.Return(keyScratch);
				if (patchScratch != null) { ArrayPool<byte>.Shared.Return(patchScratch); }
				foreach (var s in siblingScratch)
				{
					if (s != null) { ArrayPool<byte>.Shared.Return(s); }
				}
			}
		}

	}

}
