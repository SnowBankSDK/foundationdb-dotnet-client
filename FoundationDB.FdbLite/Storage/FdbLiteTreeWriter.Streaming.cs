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

// PROTOTYPE (benchmark-first, see the cell-source streaming design note): a leaf rebuild that reads
// its cells straight off the source page instead of gathering a materialized CellRef[]. net10+ only:
// the source is a ref struct used as a generic type argument (`allows ref struct`, ref-struct
// interfaces are C# 13), which the net8 leg cannot express. Off by default; enabled per writer by the
// differential suite and the KvBench A/B. The algorithm is deliberately a line-for-line clone of the
// leaf half of WriteCells/LeafPartEnd/AppendCells with the array indexing swapped for source indexing:
// the two paths must stay byte-identical, and every divergence is a bug in THIS file.

#if NET10_0_OR_GREATER

namespace FoundationDB.Storage.FdbLite
{

	public sealed partial class FdbLiteTreeWriter
	{

		/// <summary>Route leaf rebuilds through the streaming writer instead of the materialized <see cref="CellRef"/> gather.</summary>
		/// <remarks>Benchmark knob (same contract as <see cref="AvoidSequentialAppendSplits"/>): both paths must produce byte-identical stores, which the differential suite proves.</remarks>
		public bool UseStreamingRebuild { get; set; }

		/// <summary>Leaf rebuilds taken by the streaming path. Proof of execution: a behavioural test cannot tell a working toggle from an ignored one without it.</summary>
		public long StreamedLeafRebuilds { get; private set; }

		/// <summary>A sorted run of leaf cells, indexable by position. The consumer never learns where a cell lives: on a page, in a scratch buffer, or synthesized on the fly.</summary>
		/// <remarks>Indexed (not forward-only) because the sizing walk needs first/last keys and re-reads the boundary cell; every rebuild source is a page or a buffer, both O(1) by index.</remarks>
		internal interface ICellSource
		{
			int Count { get; }

			CellRef this[int index] { get; }

			/// <summary>Swap the page the cells resolve from. The writer calls this when it snapshots the source: part 0 of a split rewrites the original page in place, so a source still reading it would resolve cells from clobbered memory.</summary>
			void Rebase(ReadOnlySpan<byte> snapshot);
		}

		/// <summary>The leaf-rebuild source: every cell of one page, with one injected cell at a known index (an insert makes the run one longer, a replace substitutes in place).</summary>
		internal ref struct LeafInsertSource : ICellSource
		{
			private ReadOnlySpan<byte> Page;
			private readonly CellRef NewCell;
			private readonly int InsertAt;
			private readonly bool Replace;

			public LeafInsertSource(ReadOnlySpan<byte> page, in CellRef newCell, int insertAt, bool replace, int resultCount)
			{
				this.Page = page;
				this.NewCell = newCell;
				this.InsertAt = insertAt;
				this.Replace = replace;
				this.Count = resultCount;
			}

			public int Count { get; }

			public CellRef this[int index]
				=> index == this.InsertAt ? this.NewCell
					// on an insert the source cells after the injection point sit one position earlier in the page
					: CellRef.OfLeafPage(this.Page, index > this.InsertAt && !this.Replace ? index - 1 : index);

			public void Rebase(ReadOnlySpan<byte> snapshot) => this.Page = snapshot;
		}

		/// <summary>Streaming twin of the leaf half of <see cref="WriteCells"/>: writes a rebuilt cell run as one page, or as a K-way split when it does not fit, reading each cell from the source on demand.</summary>
		private RebuildResult WriteCellsStreamed<TSource>(uint oldPageId, ReadOnlySpan<byte> sourcePage, TSource cells, [CallerMemberName] string? caller = null)
			where TSource : ICellSource, allows ref struct
		{
			int pageSize = this.Pager.Geometry.PageSize;
			int usable = pageSize; // leaf sizing: LeafRunBytes is the FULL page footprint, so capacity is the full page
			int count = cells.Count;

			int sourcePrefixLength = sourcePage.Length > 0 && FdbLitePageHeader.GetCellCount(sourcePage) > 0
				? FdbLitePageHeader.GetPrefixLength(sourcePage)
				: 0;

			// measure scan: the run's shared prefix is what its first and last keys share, then one pass for the sums
			long totalBytes;
			{
				int runLcp = 0;
				if (count > 1)
				{
					var estimateScratch = ArrayPool<byte>.Shared.Rent(2 * FdbLiteTreePage.MaxKeyLength);
					try
					{
						var sp = sourcePrefixLength > 0 ? FdbLiteTreePage.GetPagePrefix(sourcePage, isInternal: false) : default;
						var lowest = MaterializeKey(cells[0], sourcePage, sp, estimateScratch.AsSpan(0, FdbLiteTreePage.MaxKeyLength));
						var highest = MaterializeKey(cells[count - 1], sourcePage, sp, estimateScratch.AsSpan(FdbLiteTreePage.MaxKeyLength, FdbLiteTreePage.MaxKeyLength));
						runLcp = FdbLiteTreePage.CommonPrefixLength(lowest, highest);
					}
					finally
					{
						ArrayPool<byte>.Shared.Return(estimateScratch);
					}
				}

				long sumWhole = 0, sumValue = 0;
				for (int i = 0; i < count; i++)
				{
					var cell = cells[i];
					sumWhole += LeafWholeKeyLength(in cell, sourcePrefixLength);
					sumValue += cell.ValueLength;
				}
				totalBytes = LeafRunBytes(count, sumWhole, sumValue, runLcp);
			}

			byte carriedEpisodes = sourcePage.Length > 0 ? FdbLitePageHeader.GetVolatilityEpisodes(sourcePage) : (byte) 0;

			var scratch = ArrayPool<byte>.Shared.Rent(pageSize);
			var partScratch = ArrayPool<byte>.Shared.Rent(FdbLiteTreePage.MaxKeyLength);
			byte[]? sourceCopy = null;
			try
			{
				if (totalBytes > usable && !sourcePage.IsEmpty)
				{ // splitting: part 0 may rewrite the source page in place (shadowed), which would clobber the
				  // memory later parts still resolve their cells from - snapshot the source first, and REBASE the
				  // source so its own reads move to the snapshot too (with the array this was implicit: the
				  // descriptors were gathered before any write; a streaming source re-reads, so it must follow)
					sourceCopy = ArrayPool<byte>.Shared.Rent(sourcePage.Length);
					sourcePage.CopyTo(sourceCopy);
					sourcePage = sourceCopy.AsSpan(0, sourcePage.Length);
					cells.Rebase(sourcePage);
				}

				var image = scratch.AsSpan(0, pageSize);
				List<(Slice Separator, uint PageId)>? siblings = null;
				uint firstId = 0;

				int partCount = (int) ((totalBytes + usable - 1) / usable);

				long remainingBytes = totalBytes;
				int remainingParts = partCount;

				int start = 0;
				byte[]? partSeparator = null;
				while (true)
				{
					long targetBytes = remainingParts > 1 ? (remainingBytes + remainingParts - 1) / remainingParts : long.MaxValue;

					int end = LeafPartEndStreamed(cells, start, sourcePage, sourcePrefixLength, targetBytes, pageSize, partScratch.AsSpan(0, FdbLiteTreePage.MaxKeyLength), out long bytes);

					int nextStart;
					byte[]? nextSeparator = null;
					if (end < count)
					{ // the separator is promoted to the PARENT, which shares no prefix with this leaf: whole key
						nextSeparator = WholeKeyOf(cells[end], sourcePage);
						nextStart = end;
					}
					else
					{
						nextStart = end;
					}

					// write this part: the first one lands on the original page (copy-on-write applies), the rest are fresh
					FdbLitePageHeader.Format(image, FdbLitePageType.Leaf, this.Generation);
					if (carriedEpisodes != 0) { FdbLitePageHeader.SetVolatilityEpisodes(image, carriedEpisodes); }
					AppendCellsStreamed(image, sourcePage, cells, start, end);
					uint reusing = partSeparator == null ? oldPageId : 0;
					uint id = WritePage(reusing, image);
					if (OpLog is { } log)
					{
						string tag = reusing == 0 ? "LEAF+" : "LEAF=";
						log($"{tag}\t{id}\tfrom={caller}\tpart={(partSeparator == null ? 0 : (siblings?.Count ?? 0) + 1)}\tcells={end - start}\tsrc={oldPageId}\tparts={partCount}");
					}
					if (partSeparator == null)
					{
						firstId = id;
					}
					else
					{
						(siblings ??= [ ]).Add((partSeparator.AsSlice(), id));
					}

					if (nextStart >= count && nextSeparator == null)
					{
						break;
					}
					remainingBytes -= bytes;
					if (remainingParts > 1) { remainingParts--; }

					start = nextStart;
					partSeparator = nextSeparator;
				}

				if (siblings != null)
				{
					this.PageSplits++;
					this.SplitSiblingsCreated += siblings.Count;
				}
				return new(firstId, siblings);
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(scratch);
				ArrayPool<byte>.Shared.Return(partScratch);
				if (sourceCopy != null) { ArrayPool<byte>.Shared.Return(sourceCopy); }
			}
		}

		/// <summary>Streaming twin of <see cref="LeafPartEnd"/>: end (exclusive) of the part starting at <paramref name="start"/>, reading each candidate cell from the source.</summary>
		private static int LeafPartEndStreamed<TSource>(TSource cells, int start, ReadOnlySpan<byte> sourcePage, int sourcePrefixLength, long targetBytes, int pageSize, Span<byte> scratch, out long bytes)
			where TSource : ICellSource, allows ref struct
		{
			var sourcePrefix = sourcePrefixLength > 0 ? FdbLiteTreePage.GetPagePrefix(sourcePage, isInternal: false) : default;
			var firstKey = MaterializeKey(cells[start], sourcePage, sourcePrefix, scratch);
			int lcp = firstKey.Length;
			long sumWhole = 0, sumValue = 0;

			bytes = 0;
			int end = start;
			int count = cells.Count;
			while (end < count)
			{
				var cell = cells[end];
				int candidateLcp = end == start ? lcp : LeafLcpWith(firstKey, in cell, sourcePage, sourcePrefix, lcp);
				long nextWhole = sumWhole + LeafWholeKeyLength(in cell, sourcePrefixLength);
				long nextValue = sumValue + cell.ValueLength;
				long next = LeafRunBytes(end - start + 1, nextWhole, nextValue, candidateLcp);

				if (next > pageSize)
				{
					Contract.Debug.Assert(end > start, "a single cell always fits a page (the page-size floor guarantees it)");
					break;
				}
				if (end > start && next > targetBytes)
				{ // the boundary cell rides into the next part
					break;
				}
				lcp = candidateLcp;
				sumWhole = nextWhole;
				sumValue = nextValue;
				bytes = next;
				end++;
			}
			return end;
		}

		/// <summary>Streaming twin of the leaf half of <see cref="AppendCells"/>: appends cells [<paramref name="start"/>, <paramref name="end"/>) to a freshly formatted page image.</summary>
		private static void AppendCellsStreamed<TSource>(Span<byte> image, ReadOnlySpan<byte> sourcePage, TSource cells, int start, int end)
			where TSource : ICellSource, allows ref struct
		{
			int count = end - start;

			var sourcePrefix = sourcePage.Length > 0 && FdbLitePageHeader.GetCellCount(sourcePage) > 0
				? FdbLiteTreePage.GetPagePrefix(sourcePage, isInternal: false)
				: default;

			// only the FIRST and LAST keys are assembled: the run is in key order, so the prefix shared by all
			// of its keys is the one shared by those two
			var keyScratch = ArrayPool<byte>.Shared.Rent(2 * FdbLiteTreePage.MaxKeyLength);
			int prefixLen;
			try
			{
				var firstKey = MaterializeKey(cells[start], sourcePage, sourcePrefix, keyScratch.AsSpan(0, FdbLiteTreePage.MaxKeyLength));
				var lastKey = MaterializeKey(cells[end - 1], sourcePage, sourcePrefix, keyScratch.AsSpan(FdbLiteTreePage.MaxKeyLength, FdbLiteTreePage.MaxKeyLength));
				prefixLen = count > 1 ? FdbLiteTreePage.CommonPrefixLength(firstKey, lastKey) : 0;

				// must precede the run: the prefix sits in front of the slot directory
				FdbLiteTreePage.WriteLeafPrefix(image, firstKey[..prefixLen]);
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(keyScratch);
			}

			var run = new FdbLiteTreePage.LeafRunWriter(image, count);
			for (int i = start; i < end; i++)
			{
				var cell = cells[i];
				var stored = cell.ResolveKey(sourcePage);
				var value = cell.ResolveValue(sourcePage);
				if (cell.Buffer is not null)
				{ // a whole key: strip this page's prefix outright
					run.Add(stored[prefixLen..], value, cell.Flags);
				}
				else if (prefixLen >= sourcePrefix.Length)
				{ // the new prefix reaches into the stored suffix, so the remainder is one slice of it
					run.Add(stored[(prefixLen - sourcePrefix.Length)..], value, cell.Flags);
				}
				else
				{ // the new prefix is SHORTER, so what is stored gains back the tail of the old one: two spans, no copy
					run.Add(sourcePrefix[prefixLen..], stored, value, cell.Flags);
				}
			}
			run.Complete();
		}

	}

}

#endif
