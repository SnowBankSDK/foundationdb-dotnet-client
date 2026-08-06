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

		/// <summary>Route rebuilds through the streaming writer instead of the materialized <see cref="CellRef"/> gather. ON by default (owner ruling 2026-08-06, after the bracketed A/B: 6-16% faster warm writes at byte-identical output); off reproduces the materialized path, which the differential suite compares against.</summary>
		public bool UseStreamingRebuild { get; set; } = true;

		/// <summary>Leaf rebuilds taken by the streaming path. Proof of execution: a behavioural test cannot tell a working toggle from an ignored one without it.</summary>
		public long StreamedLeafRebuilds { get; private set; }

		/// <summary>Streamed rebuilds that completed in the single-pass fast path (no split, one fused measure-and-emit scan). Byte-identical output makes a dead fast path invisible to every behavioural test; this counter is what shows it ran.</summary>
		public long StreamedSinglePassRebuilds { get; private set; }

		/// <summary>Prefix-strip rebuilds taken by the streaming path (same execution-proof rationale as <see cref="StreamedSinglePassRebuilds"/>).</summary>
		public long StreamedStrips { get; private set; }

		/// <summary>Internal-page rebuilds taken by the streaming path.</summary>
		public long StreamedInternalRebuilds { get; private set; }

		/// <summary>Streamed internal rebuilds that completed in the fused single pass (internal pages have no prefix, so only a genuine split falls back).</summary>
		public long StreamedInternalSinglePass { get; private set; }

		/// <summary>Replace-run rebuilds (a merge outcome replacing a child run) taken by the streaming path.</summary>
		public long StreamedReplaceRuns { get; private set; }

		/// <summary>Drop-leading-children rebuilds (the right side of a cross-parent merge) taken by the streaming path.</summary>
		public long StreamedDropLeading { get; private set; }

		/// <summary>Join-ancestor rebuilds (both sides of a cross-parent merge plus the moved separator) taken by the streaming path.</summary>
		public long StreamedJoins { get; private set; }

		/// <summary>Root-level builds taken by the streaming path.</summary>
		public long StreamedRootBuilds { get; private set; }

		/// <summary>Leaf K-to-1 consolidation merges taken by the streaming path (cells re-based on demand from the input pages instead of gathered into whole-key buffers).</summary>
		public long StreamedMerges { get; private set; }

		/// <summary>A sorted run of leaf cells, indexable by position. The consumer never learns where a cell lives: on a page, in a scratch buffer, or synthesized on the fly.</summary>
		/// <remarks>Indexed (not forward-only) because the sizing walk needs first/last keys and re-reads the boundary cell; every rebuild source is a page or a buffer, both O(1) by index.</remarks>
		internal interface ICellSource
		{
			int Count { get; }

			CellRef this[int index] { get; }

			/// <summary>Swap the page the cells resolve from. The writer calls this when it snapshots the source: part 0 of a split rewrites the original page in place, so a source still reading it would resolve cells from clobbered memory.</summary>
			void Rebase(ReadOnlySpan<byte> snapshot);
		}

		/// <summary>The strip-rebuild source: every cell of one page, unchanged.</summary>
		internal ref struct LeafPageSource : ICellSource
		{
			private ReadOnlySpan<byte> Page;

			public LeafPageSource(ReadOnlySpan<byte> page, int count)
			{
				this.Page = page;
				this.Count = count;
			}

			public int Count { get; }

			public CellRef this[int index] => CellRef.OfLeafPage(this.Page, index);

			public void Rebase(ReadOnlySpan<byte> snapshot) => this.Page = snapshot;
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

		/// <summary>The generalized internal-rebuild source, covering every internal gather site: the page's cells with a DROPPED child range [<c>dropFrom</c>, <c>dropTo</c>), the cell before it patched, an injected run (split-sibling separators, then optionally one extra owned cell - the join's moved separator) in the gap, and optionally the cell at <c>dropTo</c> raised. Separator cells are BUILT ON DEMAND into one shared scratch, so a returned <see cref="CellRef"/> is valid only until the next access - every consumer in this file resolves and copies before touching another index.</summary>
		/// <remarks>The four sites are points of this one shape: RebuildInternal is an empty drop range at the child index (raise allowed), RebuildInternalReplaceRun drops the merged-away run, RebuildInternalJoin drops the old separator and injects the moved one as the extra cell, and RebuildInternalDropLeadingChildren drops the leading range with nothing injected.</remarks>
		internal ref struct InternalRunSource : ICellSource
		{
			private ReadOnlySpan<byte> Page;
			private readonly ReadOnlySpan<(Slice Separator, uint PageId)> Siblings;
			private readonly byte[] SeparatorScratch;
			private readonly CellRef PatchedCell;
			private readonly CellRef RaisedCell;
			private readonly CellRef ExtraCell;
			private readonly int DropFrom;
			private readonly int DropTo;
			private readonly bool HasRaised;
			private readonly bool HasExtra;

			public InternalRunSource(ReadOnlySpan<byte> page, int cellCount, int dropFrom, int dropTo, in CellRef patchedCell, in CellRef raisedCell, bool hasRaised, in CellRef extraCell, bool hasExtra, ReadOnlySpan<(Slice Separator, uint PageId)> siblings, byte[] separatorScratch)
			{
				Contract.Debug.Requires(dropFrom >= 0 && dropTo >= dropFrom && dropTo <= cellCount);
				this.Page = page;
				this.Siblings = siblings;
				this.SeparatorScratch = separatorScratch;
				this.PatchedCell = patchedCell;
				this.RaisedCell = raisedCell;
				this.ExtraCell = extraCell;
				this.DropFrom = dropFrom;
				this.DropTo = dropTo;
				this.HasRaised = hasRaised;
				this.HasExtra = hasExtra;
				this.Count = cellCount - (dropTo - dropFrom) + siblings.Length + (hasExtra ? 1 : 0);
			}

			public int Count { get; }

			public CellRef this[int index]
			{
				get
				{
					// output order (same as every materialized gather): page cells below the drop range (the
					// last one patched), the injected siblings, the extra cell, then the page's tail (its
					// first cell raised when asked)
					if (index < this.DropFrom)
					{
						return index == this.DropFrom - 1
							? this.PatchedCell
							: CellRef.OfInternalPage(FdbLiteTreePage.GetInternalCellExtent(this.Page, index));
					}
					int s = index - this.DropFrom;
					if (s < this.Siblings.Length)
					{
						var (separator, siblingId) = this.Siblings[s];
						int len = FdbLiteTreePage.BuildInternalCell(this.SeparatorScratch, siblingId, separator.Span).Length;
						return CellRef.OfInternalBuffer(this.SeparatorScratch, len);
					}
					s -= this.Siblings.Length;
					if (this.HasExtra)
					{
						if (s == 0)
						{
							return this.ExtraCell;
						}
						s--;
					}
					int i = this.DropTo + s;
					return (i == this.DropTo && this.HasRaised)
						? this.RaisedCell
						: CellRef.OfInternalPage(FdbLiteTreePage.GetInternalCellExtent(this.Page, i));
				}
			}

			public void Rebase(ReadOnlySpan<byte> snapshot) => this.Page = snapshot;
		}

		/// <summary>Streamed variant of <see cref="RebuildInternal(uint,int,uint,ReadOnlySpan{ValueTuple{Slice,uint}},ReadOnlySpan{byte})"/>: same patched/raised construction, but the separators build on demand into ONE scratch and no cell list is materialized.</summary>
		private RebuildResult RebuildInternalStreamed(uint pageId, ReadOnlySpan<byte> page, int cellCount, int childIndex, uint childFirstId, ReadOnlySpan<(Slice Separator, uint PageId)> childSiblings, ReadOnlySpan<byte> raiseFollowingSeparator)
		{
			this.StreamedInternalRebuilds++;

			byte[]? patchScratch = null;
			byte[]? raiseScratch = null;
			byte[]? separatorScratch = null;
			try
			{
				uint leftmost = FdbLiteTreePage.GetLeftmostChild(page);

				var patchedCell = default(CellRef);
				if (childIndex == 0)
				{
					leftmost = childFirstId;
				}
				else
				{
					var original = FdbLiteTreePage.GetInternalCell(page, childIndex - 1);
					patchScratch = ArrayPool<byte>.Shared.Rent(original.Length);
					original.CopyTo(patchScratch);
					FdbLiteTreePage.PatchInternalCellChild(patchScratch, childFirstId);
					patchedCell = CellRef.OfInternalBuffer(patchScratch, original.Length);
				}

				var raisedCell = default(CellRef);
				bool hasRaised = false;
				if (raiseFollowingSeparator.Length > 0 && childIndex < cellCount)
				{ // same child, higher key: the cell is rebuilt rather than copied
					raiseScratch = ArrayPool<byte>.Shared.Rent(6 + raiseFollowingSeparator.Length);
					int len = FdbLiteTreePage.BuildInternalCell(raiseScratch, FdbLiteTreePage.GetChild(page, childIndex + 1), raiseFollowingSeparator).Length;
					raisedCell = CellRef.OfInternalBuffer(raiseScratch, len);
					hasRaised = true;
				}

				if (childSiblings.Length > 0)
				{ // ONE scratch serves every injected separator, built on demand (the materialized path rents one per sibling)
					separatorScratch = ArrayPool<byte>.Shared.Rent(6 + FdbLiteTreePage.MaxKeyLength);
				}

				var source = new InternalRunSource(page, cellCount, dropFrom: childIndex, dropTo: childIndex, in patchedCell, in raisedCell, hasRaised, extraCell: default, hasExtra: false, childSiblings, separatorScratch ?? [ ]);
				return WriteInternalCellsStreamed(pageId, leftmost, page, source, caller: nameof(RebuildInternal));
			}
			finally
			{
				if (patchScratch != null) { ArrayPool<byte>.Shared.Return(patchScratch); }
				if (raiseScratch != null) { ArrayPool<byte>.Shared.Return(raiseScratch); }
				if (separatorScratch != null) { ArrayPool<byte>.Shared.Return(separatorScratch); }
			}
		}

		/// <summary>Streamed variant of <see cref="RebuildInternalReplaceRun"/>: the merged-away run's separators drop, the merge outcome's parts inject, no cell list.</summary>
		private RebuildResult RebuildInternalReplaceRunStreamed(uint pageId, ReadOnlySpan<byte> page, int cellCount, int firstChildIndex, int lastChildIndex, in RebuildResult merged)
		{
			this.StreamedReplaceRuns++;

			byte[]? patchScratch = null;
			byte[]? separatorScratch = null;
			try
			{
				uint leftmost = FdbLiteTreePage.GetLeftmostChild(page);
				var patchedCell = default(CellRef);
				if (firstChildIndex == 0)
				{
					leftmost = merged.FirstId;
				}
				else
				{ // cell firstChildIndex-1 keeps its separator and carries the merged first part
					var original = FdbLiteTreePage.GetInternalCell(page, firstChildIndex - 1);
					patchScratch = ArrayPool<byte>.Shared.Rent(original.Length);
					original.CopyTo(patchScratch);
					FdbLiteTreePage.PatchInternalCellChild(patchScratch, merged.FirstId);
					patchedCell = CellRef.OfInternalBuffer(patchScratch, original.Length);
				}

				var siblings = AsSiblingSpan(merged);
				if (siblings.Length > 0)
				{
					separatorScratch = ArrayPool<byte>.Shared.Rent(6 + FdbLiteTreePage.MaxKeyLength);
				}

				var source = new InternalRunSource(page, cellCount, dropFrom: firstChildIndex, dropTo: lastChildIndex, in patchedCell, raisedCell: default, hasRaised: false, extraCell: default, hasExtra: false, siblings, separatorScratch ?? [ ]);
				return WriteInternalCellsStreamed(pageId, leftmost, page, source, caller: nameof(RebuildInternalReplaceRun));
			}
			finally
			{
				if (patchScratch != null) { ArrayPool<byte>.Shared.Return(patchScratch); }
				if (separatorScratch != null) { ArrayPool<byte>.Shared.Return(separatorScratch); }
			}
		}

		/// <summary>Streamed variant of <see cref="RebuildInternalDropLeadingChildren"/>: the page's tail as-is, nothing injected.</summary>
		private RebuildResult RebuildInternalDropLeadingChildrenStreamed(uint pageId, ReadOnlySpan<byte> page, int cellCount, int dropCount)
		{
			this.StreamedDropLeading++;

			uint leftmost = FdbLiteTreePage.GetChild(page, dropCount);
			var source = new InternalRunSource(page, cellCount, dropFrom: 0, dropTo: dropCount, patchedCell: default, raisedCell: default, hasRaised: false, extraCell: default, hasExtra: false, siblings: default, separatorScratch: [ ]);
			var outcome = WriteInternalCellsStreamed(pageId, leftmost, page, source, caller: nameof(RebuildInternalDropLeadingChildren));
			Contract.Debug.Assert(!outcome.Split);
			return outcome;
		}

		/// <summary>Streamed variant of <see cref="RebuildInternalJoin"/>: the old separator between the two paths drops, the left side's parts inject, and the moved separator is the extra owned cell.</summary>
		private RebuildResult RebuildInternalJoinStreamed(uint pageId, ReadOnlySpan<byte> page, int cellCount, int leftChildIndex, in RebuildResult left, in RebuildResult right, byte[] joinSeparator)
		{
			this.StreamedJoins++;

			byte[]? patchScratch = null;
			byte[]? joinScratch = null;
			byte[]? separatorScratch = null;
			try
			{
				uint leftmost = FdbLiteTreePage.GetLeftmostChild(page);
				var patchedCell = default(CellRef);
				if (leftChildIndex == 0)
				{
					leftmost = left.FirstId;
				}
				else
				{
					var original = FdbLiteTreePage.GetInternalCell(page, leftChildIndex - 1);
					patchScratch = ArrayPool<byte>.Shared.Rent(original.Length);
					original.CopyTo(patchScratch);
					FdbLiteTreePage.PatchInternalCellChild(patchScratch, left.FirstId);
					patchedCell = CellRef.OfInternalBuffer(patchScratch, original.Length);
				}

				// cell leftChildIndex (the separator between the two paths) is rebuilt outright: new key, new child
				joinScratch = ArrayPool<byte>.Shared.Rent(6 + joinSeparator.Length);
				int joinLen = FdbLiteTreePage.BuildInternalCell(joinScratch, right.FirstId, joinSeparator).Length;
				var joinCell = CellRef.OfInternalBuffer(joinScratch, joinLen);

				var siblings = AsSiblingSpan(left);
				if (siblings.Length > 0)
				{
					separatorScratch = ArrayPool<byte>.Shared.Rent(6 + FdbLiteTreePage.MaxKeyLength);
				}

				var source = new InternalRunSource(page, cellCount, dropFrom: leftChildIndex, dropTo: leftChildIndex + 1, in patchedCell, raisedCell: default, hasRaised: false, in joinCell, hasExtra: true, siblings, separatorScratch ?? [ ]);
				return WriteInternalCellsStreamed(pageId, leftmost, page, source, caller: nameof(RebuildInternalJoin));
			}
			finally
			{
				if (patchScratch != null) { ArrayPool<byte>.Shared.Return(patchScratch); }
				if (joinScratch != null) { ArrayPool<byte>.Shared.Return(joinScratch); }
				if (separatorScratch != null) { ArrayPool<byte>.Shared.Return(separatorScratch); }
			}
		}

		/// <summary>Streaming twin of the INTERNAL half of <see cref="WriteCells"/>: no prefix machinery, so the single-page fast path is a straight fused measure-and-pack, and only a genuine split walks twice.</summary>
		private RebuildResult WriteInternalCellsStreamed<TSource>(uint oldPageId, uint leftmostChild, ReadOnlySpan<byte> sourcePage, TSource cells, [CallerMemberName] string? caller = null)
			where TSource : ICellSource, allows ref struct
		{
			int pageSize = this.Pager.Geometry.PageSize;
			int usable = pageSize - FdbLiteTreePage.SlotsOffset(isInternal: true, prefixRegionSize: 0);
			int count = cells.Count;

			var scratch = ArrayPool<byte>.Shared.Rent(pageSize);
			byte[]? sourceCopy = null;
			try
			{
				var image = scratch.AsSpan(0, pageSize);

				// FAST PATH: fused measure-and-pack; crossing the usable capacity proves a split and abandons
				// the image (WritePage only sees completed images, Format clears on reuse)
				{
					FdbLitePageHeader.Format(image, FdbLitePageType.Internal, this.Generation);
					FdbLiteTreePage.SetLeftmostChild(image, leftmostChild);
					long bytes = 0;
					int tail = image.Length;
					bool fits = true;
					for (int i = 0; i < count; i++)
					{
						var cell = cells[i];
						bytes += cell.KeyLength + 2;
						if (bytes > usable)
						{
							fits = false;
							break;
						}
						var body = cell.ResolveKey(sourcePage);
						tail -= body.Length;
						body.CopyTo(image[tail..]);
						FdbLiteTreePage.SetSlot(image, isInternal: true, i, (ushort) tail);
					}
					if (fits)
					{
						FdbLitePageHeader.SetCellCount(image, (ushort) count);
						FdbLitePageHeader.SetCellAreaOffset(image, count > 0 ? (ushort) tail : (ushort) 0);
						uint id = WritePage(oldPageId, image);
						if (OpLog is { } log)
						{
							string tag = oldPageId == 0 ? "NODE+" : "NODE=";
							log($"{tag}\t{id}\tfrom={caller}\tpart=0\tcells={count}\tsrc={oldPageId}\tparts=1");
						}
						this.StreamedInternalSinglePass++;
						return new(id, null);
					}
				}

				// SLOW PATH: a genuine split. Totals for the balance targets, then the same sizing walk,
				// boundary PROMOTION (the boundary cell's child seeds the next part, its key separates), and
				// tail-packed emit as the materialized internal path.
				long totalBytes = 0;
				for (int i = 0; i < count; i++)
				{
					totalBytes += cells[i].KeyLength + 2;
				}
				Contract.Debug.Assert(totalBytes > usable, "the fast path aborted on a run its own accounting says fits one page");

				if (!sourcePage.IsEmpty)
				{ // part 0 may rewrite the source page in place while later parts still resolve from it
					sourceCopy = ArrayPool<byte>.Shared.Rent(sourcePage.Length);
					sourcePage.CopyTo(sourceCopy);
					sourcePage = sourceCopy.AsSpan(0, sourcePage.Length);
					cells.Rebase(sourcePage);
				}

				List<(Slice Separator, uint PageId)>? siblings = null;
				uint firstId = 0;

				int partCount = (int) ((totalBytes + usable - 1) / usable);
				long remainingBytes = totalBytes;
				int remainingParts = partCount;

				int start = 0;
				uint partLeftmost = leftmostChild;
				byte[]? partSeparator = null;
				while (true)
				{
					long targetBytes = remainingParts > 1 ? (remainingBytes + remainingParts - 1) / remainingParts : long.MaxValue;

					// extend the part up to the balance target, never past the page capacity
					long bytes = 0;
					int end = start;
					while (end < count)
					{
						long next = bytes + cells[end].KeyLength + 2;
						if (next > usable)
						{
							break;
						}
						if (end > start && next > targetBytes)
						{ // the boundary cell rides into the next part
							break;
						}
						bytes = next;
						end++;
					}

					// on an internal boundary the boundary cell is PROMOTED: its child seeds the next part, its key separates
					int nextStart;
					byte[]? nextSeparator = null;
					uint nextLeftmost = 0;
					if (end < count)
					{
						var boundary = cells[end].ResolveKey(sourcePage);
						int keyLen = BinaryPrimitives.ReadUInt16LittleEndian(boundary[4..]);
						nextSeparator = boundary.Slice(6, keyLen).ToArray();
						nextLeftmost = BinaryPrimitives.ReadUInt32LittleEndian(boundary);
						nextStart = end + 1;
					}
					else
					{
						nextStart = end;
					}

					FdbLitePageHeader.Format(image, FdbLitePageType.Internal, this.Generation);
					FdbLiteTreePage.SetLeftmostChild(image, partLeftmost);
					int tail = image.Length;
					for (int i = start; i < end; i++)
					{
						var body = cells[i].ResolveKey(sourcePage);
						tail -= body.Length;
						body.CopyTo(image[tail..]);
						FdbLiteTreePage.SetSlot(image, isInternal: true, i - start, (ushort) tail);
					}
					FdbLitePageHeader.SetCellCount(image, (ushort) (end - start));
					FdbLitePageHeader.SetCellAreaOffset(image, end > start ? (ushort) tail : (ushort) 0);
					uint reusing = partSeparator == null ? oldPageId : 0;
					uint id = WritePage(reusing, image);
					if (OpLog is { } log)
					{
						string tag = reusing == 0 ? "NODE+" : "NODE=";
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
					partLeftmost = nextLeftmost;

					if (start >= count)
					{ // the last cell got promoted: the final part is a leftmost-only internal page (degenerate but legal)
						FdbLitePageHeader.Format(image, FdbLitePageType.Internal, this.Generation);
						FdbLiteTreePage.SetLeftmostChild(image, partLeftmost);
						FdbLitePageHeader.SetCellCount(image, 0);
						FdbLitePageHeader.SetCellAreaOffset(image, 0);
						uint tailId = WritePage(0, image);
						(siblings ??= [ ]).Add((partSeparator!.AsSlice(), tailId));
						break;
					}
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
				if (sourceCopy != null) { ArrayPool<byte>.Shared.Return(sourceCopy); }
			}
		}

		/// <summary>Streamed variant of <see cref="BuildRootLevel"/>: the sibling separators build on demand, no cell list and no per-sibling scratches.</summary>
		private RebuildResult BuildRootLevelStreamed(uint firstId, ReadOnlySpan<(Slice Separator, uint PageId)> siblings)
		{
			this.StreamedRootBuilds++;

			var separatorScratch = ArrayPool<byte>.Shared.Rent(6 + FdbLiteTreePage.MaxKeyLength);
			try
			{
				var source = new InternalRunSource(default, 0, 0, 0, patchedCell: default, raisedCell: default, hasRaised: false, extraCell: default, hasExtra: false, siblings, separatorScratch);
				return WriteInternalCellsStreamed(0, firstId, default, source, caller: nameof(BuildRootLevel));
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(separatorScratch);
			}
		}

		/// <summary>The K-to-1 merge source: every cell of every input page in order, re-based to its whole key ON DEMAND into one shared scratch. Input 0 reads from a snapshot taken at construction: part 0 of the emission rewrites that page in place, and inputs 1+ are only freed after the emission, never written during it.</summary>
		internal ref struct LeafMergeSource : ICellSource
		{
			private readonly FdbLiteTreeWriter Owner;
			private readonly uint[] InputIds;
			private readonly int[] CumulativeCells;
			private readonly byte[] CellScratch;
			private readonly ReadOnlySpan<byte> InputZero;

			public LeafMergeSource(FdbLiteTreeWriter owner, uint[] inputIds, int[] cumulativeCells, int count, ReadOnlySpan<byte> inputZeroSnapshot, byte[] cellScratch)
			{
				this.Owner = owner;
				this.InputIds = inputIds;
				this.CumulativeCells = cumulativeCells;
				this.CellScratch = cellScratch;
				this.InputZero = inputZeroSnapshot;
				this.Count = count;
			}

			public int Count { get; }

			public CellRef this[int index]
			{
				get
				{
					// linear page lookup: K is the merge's small input count, and the copy below dwarfs it.
					// Pages re-read per access on purpose: emitting a part allocates, an allocation may grow
					// the pager, and a grow can invalidate previously returned spans.
					int p = 0;
					while (index >= this.CumulativeCells[p]) { p++; }
					int local = p == 0 ? index : index - this.CumulativeCells[p - 1];
					var page = p == 0 ? this.InputZero : this.Owner.ReadPage(this.InputIds[p]);
					var prefix = FdbLiteTreePage.GetPagePrefix(page, isInternal: false);
					var c = FdbLiteTreePage.ReadLeafCell(page, local);
					var scratch = this.CellScratch;
					prefix.CopyTo(scratch);
					page.Slice(c.KeyOffset, c.KeyLength).CopyTo(scratch.AsSpan(prefix.Length));
					int keyLength = prefix.Length + c.KeyLength;
					page.Slice(c.ValueOffset, c.ValueLength).CopyTo(scratch.AsSpan(keyLength));
					return CellRef.OfLeafBuffer(scratch, keyLength, c.ValueLength, c.Flags);
				}
			}

			/// <summary>No-op: there is no shared source page to move (cells are owned copies, and the input-0 hazard is covered by the construction-time snapshot).</summary>
			public void Rebase(ReadOnlySpan<byte> snapshot)
			{
			}
		}

		/// <summary>Streamed variant of the K-to-1 leaf merge shared by <see cref="MergeConsolidationRun"/> and <see cref="ExecuteCrossParentRun"/>: no whole-key gather buffers, no cell list.</summary>
		private RebuildResult MergeConsolidationCellsStreamed(uint[] inputIds, int fillCeiling, string caller)
		{
			this.StreamedMerges++;

			int pageSize = this.Pager.Geometry.PageSize;
			var cumulative = ArrayPool<int>.Shared.Rent(inputIds.Length);
			var inputZero = ArrayPool<byte>.Shared.Rent(pageSize);
			// one whole key plus the stored value bytes (an inline value or an extent descriptor)
			var cellScratch = ArrayPool<byte>.Shared.Rent(FdbLiteTreePage.MaxKeyLength + Math.Max(this.Pager.Geometry.MaxInlineValueLength, FdbLiteTreePage.ExtentDescriptorSize));
			try
			{
				int total = 0;
				for (int p = 0; p < inputIds.Length; p++)
				{
					total += FdbLitePageHeader.GetCellCount(ReadPage(inputIds[p]));
					cumulative[p] = total;
				}
				ReadPage(inputIds[0]).CopyTo(inputZero);

				var source = new LeafMergeSource(this, inputIds, cumulative, total, inputZero.AsSpan(0, pageSize), cellScratch);
				return WriteCellsStreamed(inputIds[0], default, source, fillCeiling, caller);
			}
			finally
			{
				ArrayPool<int>.Shared.Return(cumulative);
				ArrayPool<byte>.Shared.Return(inputZero);
				ArrayPool<byte>.Shared.Return(cellScratch);
			}
		}

		/// <summary>Streaming twin of the leaf half of <see cref="WriteCells"/>: writes a rebuilt cell run as one page, or as a K-way split when it does not fit, reading each cell from the source on demand.</summary>
		/// <param name="maxLeafFillBytes">Fill ceiling per emitted page (0 = the page size), same contract as <see cref="WriteCells"/>: a consolidation merge aims each part at its volatility-adaptive target instead of packing to capacity.</param>
		private RebuildResult WriteCellsStreamed<TSource>(uint oldPageId, ReadOnlySpan<byte> sourcePage, TSource cells, int maxLeafFillBytes = 0, [CallerMemberName] string? caller = null)
			where TSource : ICellSource, allows ref struct
		{
			int pageSize = this.Pager.Geometry.PageSize;
			int usable = pageSize; // leaf sizing: LeafRunBytes is the FULL page footprint, so capacity is the full page
			int fillCeiling = maxLeafFillBytes > 0 ? Math.Min(maxLeafFillBytes, usable) : usable;
			int count = cells.Count;

			int sourcePrefixLength = sourcePage.Length > 0 && FdbLitePageHeader.GetCellCount(sourcePage) > 0
				? FdbLitePageHeader.GetPrefixLength(sourcePage)
				: 0;

			byte carriedEpisodes = sourcePage.Length > 0 ? FdbLitePageHeader.GetVolatilityEpisodes(sourcePage) : (byte) 0;

			var scratch = ArrayPool<byte>.Shared.Rent(pageSize);
			// both boundary keys at once: the fast path needs the first key's BYTES (they become the page prefix)
			// while the last key is probed; the slow path reuses the first half as its per-part scratch
			var partScratch = ArrayPool<byte>.Shared.Rent(2 * FdbLiteTreePage.MaxKeyLength);
			byte[]? sourceCopy = null;
			try
			{
				Contract.Debug.Assert(count > 0);
				var image = scratch.AsSpan(0, pageSize);

				uint fastId = TryWriteCellsSinglePage(oldPageId, sourcePage, cells, sourcePrefixLength, carriedEpisodes, image, partScratch, fillCeiling, out int runLcp, caller);
				if (fastId != 0)
				{
					this.StreamedSinglePassRebuilds++;
					return new(fastId, null);
				}

				// SLOW PATH: a genuine split. Only now do the balance targets need the run's exact total, so the
				// sums walk runs here, on the minority of rebuilds that split.
				long totalBytes;
				{
					long sumWhole = 0, sumValue = 0;
					for (int i = 0; i < count; i++)
					{
						var cell = cells[i];
						sumWhole += LeafWholeKeyLength(in cell, sourcePrefixLength);
						sumValue += cell.ValueLength;
					}
					totalBytes = LeafRunBytes(count, sumWhole, sumValue, runLcp);
				}
				Contract.Debug.Assert(totalBytes > fillCeiling, "the fast path aborted on a run its own accounting says fits one page");

				if (!sourcePage.IsEmpty)
				{ // splitting: part 0 may rewrite the source page in place (shadowed), which would clobber the
				  // memory later parts still resolve their cells from - snapshot the source first, and REBASE the
				  // source so its own reads move to the snapshot too (with the array this was implicit: the
				  // descriptors were gathered before any write; a streaming source re-reads, so it must follow)
					sourceCopy = ArrayPool<byte>.Shared.Rent(sourcePage.Length);
					sourcePage.CopyTo(sourceCopy);
					sourcePage = sourceCopy.AsSpan(0, sourcePage.Length);
					cells.Rebase(sourcePage);
				}
				List<(Slice Separator, uint PageId)>? siblings = null;
				uint firstId = 0;

				int partCount = (int) ((totalBytes + fillCeiling - 1) / fillCeiling);

				long remainingBytes = totalBytes;
				int remainingParts = partCount;

				int start = 0;
				byte[]? partSeparator = null;
				while (true)
				{
					long targetBytes = remainingParts > 1 ? (remainingBytes + remainingParts - 1) / remainingParts
						: maxLeafFillBytes > 0 ? fillCeiling
						: long.MaxValue;

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

		/// <summary>The FAST PATH: speculative single-page emit, fusing measure and emit into ONE scan. Returns the written page's id, or 0 when the run does not fit one page (in which case NOTHING was written: the caller's image is scratch, and <see cref="FdbLitePageHeader.Format"/> clears it on reuse).</summary>
		/// <remarks>A single-page rebuild's destination prefix is the run's own LCP, known from two O(1) boundary probes, so nothing depends on a cut point. The same <see cref="LeafRunBytes"/> accounting the split decision uses runs incrementally alongside the emit; crossing the page size proves this is really a split. "Fits by formula" and "fits by emit" are the same statement (the two-scan byte-identity invariant), so this takes exactly the rebuilds the materialized path would emit as one page - the differential suite proves it.</remarks>
		/// <param name="keyScratch">At least 2 x <see cref="FdbLiteTreePage.MaxKeyLength"/> bytes, for the two boundary keys.</param>
		/// <param name="runLcp">The run's whole-key LCP from the boundary probe, for the split slow path to reuse.</param>
		/// <param name="fillCeiling">Bytes this page may take (a consolidation merge aims below capacity; everything else passes the page size).</param>
		private uint TryWriteCellsSinglePage<TSource>(uint oldPageId, ReadOnlySpan<byte> sourcePage, TSource cells, int sourcePrefixLength, byte carriedEpisodes, Span<byte> image, Span<byte> keyScratch, int fillCeiling, out int runLcp, string? caller)
			where TSource : ICellSource, allows ref struct
		{
			int count = cells.Count;
			var sourcePrefix = sourcePrefixLength > 0 ? FdbLiteTreePage.GetPagePrefix(sourcePage, isInternal: false) : default;

			// the whole run's shared prefix is what its first and last keys share (they are in key order);
			// the FIRST key is held across the last-key access, so it must be materialized STABLE
			var firstKey = MaterializeKeyStable(cells[0], sourcePage, sourcePrefix, keyScratch[..FdbLiteTreePage.MaxKeyLength]);
			runLcp = 0;
			if (count > 1)
			{
				var lastKey = MaterializeKey(cells[count - 1], sourcePage, sourcePrefix, keyScratch.Slice(FdbLiteTreePage.MaxKeyLength, FdbLiteTreePage.MaxKeyLength));
				runLcp = FdbLiteTreePage.CommonPrefixLength(firstKey, lastKey);
			}

			int effective = count > 1 ? runLcp : 0;
			// the slot directory is committed for the WHOLE run the moment the LeafRunWriter is built, so the
			// running total must carry all count*2 slot bytes up front; charging each cell's slot as it is
			// added under-counts the real frontier by 2 x (remaining cells) and lets a deep abort write into
			// directory space (the sum still ends at LeafRunBytes exactly, so the fit verdict is unchanged)
			long total = FdbLiteTreePage.SlotsOffset(isInternal: false, prefixRegionSize: (effective + 1) & ~1) + (long) count * 2;
			FdbLitePageHeader.Format(image, FdbLitePageType.Leaf, this.Generation);
			if (carriedEpisodes != 0) { FdbLitePageHeader.SetVolatilityEpisodes(image, carriedEpisodes); }
			FdbLiteTreePage.WriteLeafPrefix(image, firstKey[..effective]);
			var run = new FdbLiteTreePage.LeafRunWriter(image, count);
			for (int i = 0; i < count; i++)
			{
				var cell = cells[i];
				total += LeafWholeKeyLength(in cell, sourcePrefixLength) - effective + (FdbLiteTreePage.LeafCellOverhead - 2) + cell.ValueLength;
				// the ceiling binds from the SECOND cell on, mirroring LeafPartEnd's first-cell exemption: a
				// single cell above the merge ceiling but within the page still emits as one page there
				if (total > image.Length || (i > 0 && total > fillCeiling))
				{
					return 0;
				}
				var stored = cell.ResolveKey(sourcePage);
				var value = cell.ResolveValue(sourcePage);
				if (cell.Buffer is not null)
				{ // a whole key: strip this page's prefix outright
					run.Add(stored[effective..], value, cell.Flags);
				}
				else if (effective >= sourcePrefix.Length)
				{ // the new prefix reaches into the stored suffix, so the remainder is one slice of it
					run.Add(stored[(effective - sourcePrefix.Length)..], value, cell.Flags);
				}
				else
				{ // the new prefix is SHORTER, so what is stored gains back the tail of the old one
					run.Add(sourcePrefix[effective..], stored, value, cell.Flags);
				}
			}
			run.Complete();
			uint id = WritePage(oldPageId, image);
			if (OpLog is { } log)
			{
				string tag = oldPageId == 0 ? "LEAF+" : "LEAF=";
				log($"{tag}\t{id}\tfrom={caller}\tpart=0\tcells={count}\tsrc={oldPageId}\tparts=1");
			}
			return id;
		}

		/// <summary>Streamed fresh single-cell page (the sequential-append shortcut): the run is one owned cell, and the page-size floor guarantees it fits.</summary>
		private uint WriteFreshSingleCellPage(in CellRef cell, [CallerMemberName] string? caller = null)
		{
			int pageSize = this.Pager.Geometry.PageSize;
			var scratch = ArrayPool<byte>.Shared.Rent(pageSize);
			var keyScratch = ArrayPool<byte>.Shared.Rent(2 * FdbLiteTreePage.MaxKeyLength);
			try
			{
				var source = new LeafInsertSource(default, in cell, insertAt: 0, replace: false, resultCount: 1);
				uint id = TryWriteCellsSinglePage(0, default, source, sourcePrefixLength: 0, carriedEpisodes: 0, scratch.AsSpan(0, pageSize), keyScratch, fillCeiling: pageSize, out _, caller);
				Contract.Debug.Assert(id != 0, "a single cell always fits a page (the page-size floor guarantees it)");
				return id;
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(scratch);
				ArrayPool<byte>.Shared.Return(keyScratch);
			}
		}

		/// <summary>Streamed variant of the strip rebuild: the fused single-page emit over the page's own cells. Returns 0 without side effects when the rebuilt run would not fit (the materialized guard's "fail safely" contract).</summary>
		private uint TryStripStreamed(uint leafId, ReadOnlySpan<byte> page)
		{
			int pageSize = this.Pager.Geometry.PageSize;
			var scratch = ArrayPool<byte>.Shared.Rent(pageSize);
			var keyScratch = ArrayPool<byte>.Shared.Rent(2 * FdbLiteTreePage.MaxKeyLength);
			try
			{
				var source = new LeafPageSource(page, FdbLitePageHeader.GetCellCount(page));
				uint id = TryWriteCellsSinglePage(
					leafId, page, source,
					FdbLitePageHeader.GetPrefixLength(page),
					FdbLitePageHeader.GetVolatilityEpisodes(page),
					scratch.AsSpan(0, pageSize), keyScratch, fillCeiling: pageSize, out _,
					caller: nameof(TryStripAndRetry));
				if (id != 0) { this.StreamedStrips++; }
				return id;
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(scratch);
				ArrayPool<byte>.Shared.Return(keyScratch);
			}
		}

		/// <summary>Like <see cref="MaterializeKey"/>, but the result is guaranteed to SURVIVE further source accesses: a buffer-backed cell from a streaming source may live in that source's shared scratch (rebuilt on every access), so its key is copied out into <paramref name="scratch"/>. Required for any key held across another <c>cells[i]</c> - the boundary keys of the probe and sizing walks.</summary>
		private static ReadOnlySpan<byte> MaterializeKeyStable(in CellRef cell, ReadOnlySpan<byte> sourcePage, ReadOnlySpan<byte> sourcePrefix, Span<byte> scratch)
		{
			if (cell.Buffer is null)
			{ // page-backed: resolves against the page (or the snapshot), which no probe mutates
				return MaterializeKey(in cell, sourcePage, sourcePrefix, scratch);
			}
			var stored = cell.Buffer.AsSpan(cell.KeyOffset, cell.KeyLength);
			stored.CopyTo(scratch);
			return scratch[..stored.Length];
		}

		/// <summary>Streaming twin of <see cref="LeafPartEnd"/>: end (exclusive) of the part starting at <paramref name="start"/>, reading each candidate cell from the source.</summary>
		private static int LeafPartEndStreamed<TSource>(TSource cells, int start, ReadOnlySpan<byte> sourcePage, int sourcePrefixLength, long targetBytes, int pageSize, Span<byte> scratch, out long bytes)
			where TSource : ICellSource, allows ref struct
		{
			var sourcePrefix = sourcePrefixLength > 0 ? FdbLiteTreePage.GetPagePrefix(sourcePage, isInternal: false) : default;
			// held across every candidate access below, so it must be materialized STABLE
			var firstKey = MaterializeKeyStable(cells[start], sourcePage, sourcePrefix, scratch);
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
				// the first key survives the last-key access AND feeds WriteLeafPrefix after it: STABLE
				var firstKey = MaterializeKeyStable(cells[start], sourcePage, sourcePrefix, keyScratch.AsSpan(0, FdbLiteTreePage.MaxKeyLength));
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
