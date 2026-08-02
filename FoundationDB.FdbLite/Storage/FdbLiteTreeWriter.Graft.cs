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
	/// <remarks>The renderer records an INDEX rather than a separator, so a run of hundreds of pages costs no array per page while it is being rendered; the separators are materialised once, at the end, only for the pages that need one (see <c>GraftedSiblings</c>).</remarks>
	internal readonly record struct FdbLiteGraftedPage(int RunIndex, uint PageId);

	public sealed partial class FdbLiteTreeWriter
	{

		/// <summary>Renders an ordered run of cells into finished leaf pages, each packed to <paramref name="fillCeiling"/> live bytes.</summary>
		/// <param name="cells">Cells in strictly ascending key order, each carrying its own buffer (a grafted cell is built, not gathered from a page).</param>
		/// <param name="fillCeiling">Live bytes a page is packed to before the next one is started; clamped to the page size.</param>
		/// <param name="reusePageId">Page whose id the FIRST output may take over, or 0 for all-new pages.</param>
		/// <param name="sourcePage">Page the buffer-less cells were gathered from, or empty when every cell carries its own buffer.</param>
		/// <param name="volatility">Declared future mutability of the rendered data, stamped on every emitted page as its episode count.</param>
		/// <param name="output">Receives one entry per emitted page, in key order. Must hold at least <c>cells.Length</c> entries.</param>
		/// <returns>Number of pages emitted, i.e. the number of <paramref name="output"/> entries written.</returns>
		/// <remarks>
		/// <para>The whole point of the bulk path: page boundaries are chosen with the ENTIRE run in hand, so every
		/// page but the last comes out full. Feeding the same keys through <see cref="Insert"/> cannot do this, because
		/// a split decides where to cut before the writer knows which keys still arrive.</para>
		/// <para>Every emitted page takes the DECLARED volatility class as its episode count, and does NOT inherit
		/// <paramref name="sourcePage"/>'s. These pages are fresh, not that page's life continuing, and the reset-on-repack
		/// rule the counter is defined by exists precisely so a one-time bulk load cannot brand its leaves volatile
		/// forever. <see cref="FdbLiteVolatilityClass"/>'s values are on the same scale as the counter, so the class
		/// value IS the count.</para>
		/// <para>Sizing deliberately goes through <see cref="LeafPartEnd"/>, the same boundary rule <see cref="WriteCells"/>
		/// uses for its split parts, rather than a second rule of its own: this renderer hands each range to
		/// <see cref="WriteCells"/> as ONE page, so a sizing that disagreed by a byte would make that call split again -
		/// and the extra sibling would be dropped on the floor here. Agreement is what makes the postcondition below hold.</para>
		/// </remarks>
		internal int RenderRun(ReadOnlySpan<CellRef> cells, int fillCeiling, uint reusePageId, ReadOnlySpan<byte> sourcePage, FdbLiteVolatilityClass volatility, FdbLiteGraftedPage[] output)
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
					var part = WriteCells(reuse, isInternal: false, leftmostChild: 0, sourcePage, cells[start..end], declaredEpisodes: (int) volatility);
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
		/// <param name="volatility">Declared future mutability of the run, stamped on every emitted page as its episode count (see <see cref="RenderRun"/>).</param>
		/// <param name="output">Receives one entry per emitted page, in key order. Must hold at least <c>run.Length</c> plus the boundary leaf's cell count entries.</param>
		/// <returns>Number of pages emitted.</returns>
		/// <remarks>
		/// <para>The boundary page's cells join the run rather than being preserved beside it, which is what lets the two
		/// ends come out packed instead of half empty: the head of the run tops up what was below the insertion point
		/// and the tail is completed by what was above it. Everything between is written whole.</para>
		/// <para>The ascent is done HERE rather than left to the caller, because the two things its separators are
		/// materialised from - the merged cell list and the boundary page - exist only inside this call: handing
		/// them back out would mean handing out a pooled array and a page image whose lifetime the caller cannot see.</para>
		/// </remarks>
		internal int GraftIntoGap(uint leafId, ReadOnlySpan<byte> begin, ReadOnlySpan<CellRef> run, int fillCeiling, FdbLiteVolatilityClass volatility, FdbLiteGraftedPage[] output)
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
				int pages = RenderRun(all.AsSpan(0, total), fillCeiling, reusePageId: 0, sourcePage: page, volatility, output);

				// RENTED, not a List: a graft emits hundreds of pages, and the ascent only needs the separators
				// for the length of one call. The separator BYTES are rented too, as ONE buffer the Slices point
				// into, so a run of hundreds costs one array instead of one per page; `separators` owns that buffer
				// and must outlive every read of them, which means the whole ascent.
				var siblings = ArrayPool<(Slice Separator, uint PageId)>.Shared.Rent(pages - 1);
				var separators = default(SliceOwner);
				try
				{
					separators = GraftedSiblings(page, all.AsSpan(0, total), output.AsSpan(0, pages), siblings);
					AscendPatch(pathPages, pathChildren, depth - 1, leafId, output[0].PageId, siblings.AsSpan(0, pages - 1));
				}
				finally
				{ // cleared: a Slice holds a byte[] reference, and a pooled array must not pin the separator buffer
					separators.Dispose();
					ArrayPool<(Slice Separator, uint PageId)>.Shared.Return(siblings, clearArray: true);
				}

				// Nothing else retires the boundary leaf: on the rebuild path WritePage frees the old page as it
				// copies it, and a graft deliberately never hands it one (the render reuses no page id). LAST,
				// because FreePage recycles the page's buffer and every read of those bytes - the render, and the
				// separators materialised above - had to happen first.
				FreePage(leafId);

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

		/// <summary>Materialises a graft's separators into <paramref name="siblings"/>: the first cell of each page after the first IS that page's separator.</summary>
		/// <remarks>
		/// <para>Materialised rather than read in place, because a separator handed to a parent outlives the page image it came from.
		/// They all land in ONE pooled buffer, each <see cref="Slice"/> pointing at its own region, so a run of hundreds of pages costs
		/// one rental instead of one array per page. The returned owner holds that buffer: the caller must keep it alive until the
		/// ascent has copied every separator into its parent page, and dispose it in a <c>finally</c>.</para>
		/// <para>The prefix rule is <see cref="WholeKeyOf(in CellRef, ReadOnlySpan{byte})"/>'s, and is what puts the source page's
		/// stripped prefix back: a cell gathered from the boundary leaf holds only its suffix, and a partial separator would
		/// mis-sort the tree instead of failing loudly. A cell that carries its own buffer was built whole and takes no prefix.</para>
		/// </remarks>
		private static SliceOwner GraftedSiblings(ReadOnlySpan<byte> sourcePage, ReadOnlySpan<CellRef> cells, ReadOnlySpan<FdbLiteGraftedPage> pages, Span<(Slice Separator, uint PageId)> siblings)
		{
			var prefix = FdbLiteTreePage.GetPagePrefix(sourcePage, isInternal: false);

			int total = 0;
			for (int i = 1; i < pages.Length; i++)
			{
				ref readonly var cell = ref cells[pages[i].RunIndex];
				total += cell.ResolveKey(sourcePage).Length + (cell.Buffer is null ? prefix.Length : 0);
			}

			var buffer = ArrayPool<byte>.Shared.Rent(total);
			int pos = 0;
			for (int i = 1; i < pages.Length; i++)
			{
				ref readonly var cell = ref cells[pages[i].RunIndex];
				int start = pos;
				if (cell.Buffer is null && prefix.Length > 0)
				{
					prefix.CopyTo(buffer.AsSpan(pos));
					pos += prefix.Length;
				}
				var stored = cell.ResolveKey(sourcePage);
				stored.CopyTo(buffer.AsSpan(pos));
				pos += stored.Length;
				siblings[i - 1] = (buffer.AsSlice(start, pos - start), pages[i].PageId);
			}
			Contract.Debug.Assert(pos == total);

			return SliceOwner.Create(buffer.AsSlice(0, pos), ArrayPool<byte>.Shared);
		}

	}

}
