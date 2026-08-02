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
		internal int RenderRun(ReadOnlySpan<CellRef> cells, int fillCeiling, uint reusePageId, FdbLiteGraftedPage[] output)
		{
			Contract.Requires(cells.Length > 0);
			Contract.Requires(fillCeiling > 0);
			Contract.Requires(output.Length >= cells.Length, "a page holds at least one cell, so cells.Length bounds the page count");

			int pageSize = this.Pager.Geometry.PageSize;
			long ceiling = Math.Min(fillCeiling, pageSize);

			var scratch = ArrayPool<byte>.Shared.Rent(FdbLiteTreePage.MaxKeyLength);
			try
			{
				int pages = 0;
				int start = 0;
				while (start < cells.Length)
				{
					// no source page: a grafted cell carries its own buffer, so there is no page prefix to put back
					int end = LeafPartEnd(cells, start, sourcePage: default, sourcePrefixLength: 0, ceiling, pageSize, scratch.AsSpan(0, FdbLiteTreePage.MaxKeyLength), out _);

					uint reuse = pages == 0 ? reusePageId : 0;
					var part = WriteCells(reuse, isInternal: false, leftmostChild: 0, default, cells[start..end]);
					Contract.Ensures(!part.Split, "the boundary was chosen to fit one page, so WriteCells must not split it (a dropped sibling would silently orphan cells)");

					output[pages++] = new(start, part.FirstId);
					start = end;
				}
				return pages;
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(scratch);
			}
		}

	}

}
