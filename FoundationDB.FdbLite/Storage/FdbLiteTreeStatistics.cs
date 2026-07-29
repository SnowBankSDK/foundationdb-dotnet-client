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
		long FreeGapBytes)
	{

		private struct Accumulator
		{
			public int InternalPages;
			public int LeafPages;
			public int MaxWastedBytesPerPage;
			public long CellCount;
			public long WastedBytes;
			public long FreeGapBytes;
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
			return new(acc.InternalPages, acc.LeafPages, acc.CellCount, acc.WastedBytes, acc.MaxWastedBytesPerPage, acc.FreeGapBytes);
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

}
