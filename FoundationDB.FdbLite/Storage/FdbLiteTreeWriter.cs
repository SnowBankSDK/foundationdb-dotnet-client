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

	/// <summary>Builds the writable copy-on-write generation of a tree: inserts and updates, page splits, shadow-set page reuse.</summary>
	/// <remarks>
	/// <para>Single-writer machinery (one instance per writable generation, used under the engine's commit lock).</para>
	/// <para>Small mutations edit an OWNED page image in place (splice, overwrite, relocate, remove - each books any dead bytes it leaves into the page's wasted-bytes counter); everything else REBUILDS the touched page, and a rebuilt page is compact by construction. Either way the image is sealed and generation-stamped on the way to the pager. A page first allocated by THIS generation is rebuilt in place (shadow set); any other page is copied to a fresh location and its old blocks are queued for delayed free.</para>
	/// <para>Splits are K-way (greedy, largest prefix that fits per page): with maximum-size cells a two-way split can have NO legal cut point (the unimplemented "three-way split" hole of the legacy prototype), so the general form is the correctness fix, and K stays 2 for every realistic cell size.</para>
	/// </remarks>
	public sealed class FdbLiteTreeWriter
	{

		public FdbLiteTreeWriter(IFdbLitePager pager, FdbLiteBlockAllocator allocator, ulong generation, uint root)
		{
			Contract.NotNull(pager);
			Contract.NotNull(allocator);
			this.Pager = pager;
			this.Allocator = allocator;
			this.Generation = generation;
			this.Root = root;
		}

		private IFdbLitePager Pager { get; }

		private FdbLiteBlockAllocator Allocator { get; }

		/// <summary>Generation being built</summary>
		public ulong Generation { get; }

		/// <summary>Current root of the writable tree (0 = empty)</summary>
		public uint Root { get; private set; }

		/// <summary>Pages allocated by this generation (rebuilt in place instead of copied)</summary>
		private HashSet<uint> Shadow { get; } = [ ];

		/// <summary>Page images this generation has modified but not yet handed to the pager, by page id.</summary>
		/// <remarks>
		/// <para>A page touched N times in one generation reaches the pager ONCE, at <see cref="FlushDirtyPages"/>: without this, every mutation writes a whole page image, so a bulk load writes a full page per inserted key.</para>
		/// <para>Held for the whole generation with no eviction: the dirty set is bounded by the transaction's own size limit, so an application that raises that limit has asked for the page memory. Nothing here is written until the flush, so an abandoned generation touches the file not at all.</para>
		/// </remarks>
		private Dictionary<uint, byte[]> Dirty { get; } = [ ];

		/// <summary>The store as THIS generation sees it: its own buffered page images first, the underlying pager for everything else.</summary>
		/// <remarks>Anything reading the tree while this generation is being built must go through here, not through the pager: the pager does not receive a modified page until <see cref="FlushDirtyPages"/>, so a direct pager read would see the previous generation's bytes (or, for a freshly allocated page, no page at all).</remarks>
		public IFdbLitePager PagerView => this.View ??= new DirtyOverlayPager(this);

		private DirtyOverlayPager? View { get; set; }

		/// <summary>Reads served from the writer's buffered images when it has one, and from the underlying pager otherwise.</summary>
		private sealed class DirtyOverlayPager : IFdbLitePager
		{

			public DirtyOverlayPager(FdbLiteTreeWriter writer)
			{
				this.Writer = writer;
			}

			private FdbLiteTreeWriter Writer { get; }

			private IFdbLitePager Inner => this.Writer.Pager;

			/// <inheritdoc />
			public FdbLiteGeometry Geometry => this.Inner.Geometry;

			/// <inheritdoc />
			public uint BlockCount => this.Inner.BlockCount;

			/// <inheritdoc />
			public uint RegionSizeInBlocks => this.Inner.RegionSizeInBlocks;

			/// <inheritdoc />
			public ReadOnlySpan<byte> ReadBlocks(uint firstBlock, int count)
			{
				// only whole-page reads can hit a buffered image: value extents are block-granular, written
				// through on the spot, and never share a start block with a tree page
				if (count == this.Inner.Geometry.BlocksPerPage && this.Writer.Dirty.TryGetValue(firstBlock, out var image))
				{
					return image;
				}
				return this.Inner.ReadBlocks(firstBlock, count);
			}

			/// <inheritdoc />
			/// <remarks>Pass-through ON PURPOSE, asymmetric with <see cref="ReadBlocks"/>: tree pages never come through here (they are buffered in <see cref="FdbLiteTreeWriter.Dirty"/> by <see cref="WritePage"/> and flushed at commit), only extent data, which is written once and read back through the inner pager. Do not "fix" this by adding a dirty-buffer check on the write side.</remarks>
			public void WriteBlocks(uint firstBlock, ReadOnlySpan<byte> data) => this.Inner.WriteBlocks(firstBlock, data);

			/// <inheritdoc />
			public void Flush() => this.Inner.Flush();

			/// <inheritdoc />
			public void Grow(uint minimumBlockCount) => this.Inner.Grow(minimumBlockCount);

			/// <inheritdoc />
			public void Truncate(uint newBlockCount) => this.Inner.Truncate(newBlockCount);

			/// <inheritdoc />
			public void Dispose() { } // a view over the engine's pager, which owns it

		}

		/// <summary>Number of page images currently held in memory for this generation</summary>
		public int DirtyPageCount => this.Dirty.Count;

		/// <summary>Bytes of page images currently held in memory for this generation</summary>
		public long DirtyBytes => (long) this.Dirty.Count * this.Pager.Geometry.PageSize;

		/// <summary>Page images handed to the pager so far (one per dirty page per flush, NOT one per mutation)</summary>
		public int PagesWritten { get; private set; }

		/// <summary>Pages copied out of a previous generation (a first touch: the copy-on-write count)</summary>
		public int PageCopies { get; private set; }

		/// <summary>Page splits performed by this generation</summary>
		public int PageSplits { get; private set; }

		/// <summary>Cells spliced into an already-owned page, instead of rebuilding it (the cheap insert path)</summary>
		public int CellsSpliced { get; private set; }

		/// <summary>Values replaced without a rebuild (the cheap REPLACE path): overwritten where they lay, or relocated into the free gap when they grew</summary>
		public int CellsOverwritten { get; private set; }

		/// <summary>Cells removed by closing the directory over them, instead of rebuilding the page (the cheap DELETE path)</summary>
		public int CellsRemovedInPlace { get; private set; }

		/// <summary>Replaces that could NOT be overwritten in place and rebuilt their page instead</summary>
		/// <remarks>
		/// The ratio of this to <see cref="CellsOverwritten"/> is the health of the replace path, and it exists
		/// so a gate can watch it. A replace-heavy workload that rebuilds every time costs O(cells) per
		/// mutation where the prototype this engine succeeds pays a memcpy; that gap was measured at 4x to 86x
		/// before the in-place path existed. It went unnoticed for weeks because it is invisible to every
		/// correctness assertion - the results stay right, they just take far longer to produce.
		/// </remarks>
		public int ReplacesRebuilt { get; private set; }

		/// <summary>Root-to-leaf descents performed by this generation (a sorted run costs one per LEAF, not one per key)</summary>
		public int LeafDescents { get; private set; }

		/// <summary>Fresh pages started at the right edge instead of splitting a full leaf in half (see <see cref="AvoidSequentialAppendSplits"/>)</summary>
		public int PagesAppended { get; private set; }

		/// <summary>Full leaves rebuilt to strip the prefix their keys share, which happens at most ONCE per page per fill</summary>
		/// <remarks>Exposed so a test can assert the rebuild does not degenerate into one per insert: it should track the number of pages that filled, never the number of keys.</remarks>
		public int PagesStripped { get; private set; }

		/// <summary>Start a fresh page when a key appends past the last key of the RIGHTMOST leaf, instead of splitting that leaf in half.</summary>
		/// <remarks>
		/// <para>A balanced split leaves both halves near half full; on an append-shaped load nothing ever inserts into the left half again, so the whole file settles at ~50% occupancy. Starting a fresh page instead packs the finished pages to capacity, roughly halving pages, file size and pages written for that load.</para>
		/// <para>The trade, taken knowingly: a right-edge page packed to 100% splits on the first later insert into it, converting one cheap split into two for append-then-update data. Exposed as a knob so a benchmark can measure both ways.</para>
		/// </remarks>
		public bool AvoidSequentialAppendSplits { get; set; } = true;

		/// <summary>Extents written by this generation (freed immediately on replace/delete, instead of waiting out the horizon)</summary>
		private HashSet<uint> ShadowExtents { get; } = [ ];

		/// <summary>Net key-count change of this generation (inserts of new keys minus removals), for the snapshot header's exact count</summary>
		public long KeyCountDelta { get; private set; }

		private const int MaxDepth = 20;

		/// <summary>Leaf the last descent landed on, and the key range that descent proved that leaf covers (0 = no position).</summary>
		/// <remarks>
		/// <para>The bounds are the separators on either side of the descent path, so <c>[Lower, Upper)</c> is exactly the range routed to this leaf; a key inside it needs no descent at all. <c>null</c> is unbounded, and an unbounded <see cref="CursorUpper"/> also identifies the RIGHTMOST leaf, which is what <see cref="AvoidSequentialAppendSplits"/> keys off.</para>
		/// <para>Valid only while the leaf's identity and range hold: any structural change (a split, a delete's rebuild) drops the position rather than trying to repair it.</para>
		/// </remarks>
		private uint CursorLeaf { get; set; }

		private byte[]? CursorLower { get; set; }

		private byte[]? CursorUpper { get; set; }

		/// <summary>True when <paramref name="key"/> falls in the range the cached descent proved the cursor leaf covers.</summary>
		private bool CursorCovers(ReadOnlySpan<byte> key)
			=> this.CursorLeaf != 0
			&& (this.CursorLower is null || key.SequenceCompareTo(this.CursorLower) >= 0)
			&& (this.CursorUpper is null || key.SequenceCompareTo(this.CursorUpper) < 0);

		/// <summary>Outcome of rebuilding one page: the page (possibly relocated), plus right siblings when it split</summary>
		private readonly record struct RebuildResult(uint FirstId, List<(byte[] Separator, uint PageId)>? Siblings)
		{
			public bool Split => this.Siblings != null;
		}

		/// <summary>Reference to one gathered cell, held as its key part and its value part because a leaf cell is no longer contiguous: it lives in two regions of the page. Either part may be a slice of the source page image or of a scratch buffer (a span-of-spans is not expressible, so gathered cell lists carry these instead).</summary>
		/// <remarks>An internal cell has no value part: its whole cell is the key part, which keeps one gather-and-emit path serving both page kinds.</remarks>
		private readonly struct CellRef
		{
			public readonly byte[]? Buffer;
			public readonly int KeyOffset;
			public readonly int KeyLength;
			public readonly int ValueOffset;
			public readonly int ValueLength;
			public readonly byte Flags;

			public CellRef(byte[]? buffer, int keyOffset, int keyLength, int valueOffset, int valueLength, byte flags)
			{
				this.Buffer = buffer;
				this.KeyOffset = keyOffset;
				this.KeyLength = keyLength;
				this.ValueOffset = valueOffset;
				this.ValueLength = valueLength;
				this.Flags = flags;
			}

			/// <summary>An internal cell: contiguous, and carried entirely in the key part</summary>
			public static CellRef OfInternalPage((int Offset, int Length) extent) => new(null, extent.Offset, extent.Length, 0, 0, 0);

			public static CellRef OfInternalBuffer(byte[] buffer, int length) => new(buffer, 0, length, 0, 0, 0);

			/// <summary>A leaf cell already living in a page, whose two parts are gathered from their own regions</summary>
			public static CellRef OfLeafPage(ReadOnlySpan<byte> page, int cellIndex)
			{
				// one pass, so the key-heap base is resolved once for the whole cell rather than once per part
				var c = FdbLiteTreePage.ReadLeafCell(page, cellIndex);
				return new(null, c.KeyOffset, c.KeyLength, c.ValueOffset, c.ValueLength, c.Flags);
			}

			/// <summary>A leaf cell built into scratch: key at the front, stored value straight after it</summary>
			public static CellRef OfLeafBuffer(byte[] buffer, int keyLength, int valueLength, byte flags) => new(buffer, 0, keyLength, keyLength, valueLength, flags);

			/// <summary>Key/value bytes only, without the fixed per-cell overhead</summary>
			public int PayloadLength => this.KeyLength + this.ValueLength;

			public ReadOnlySpan<byte> ResolveKey(ReadOnlySpan<byte> sourcePage)
				=> this.Buffer is null ? sourcePage.Slice(this.KeyOffset, this.KeyLength) : this.Buffer.AsSpan(this.KeyOffset, this.KeyLength);

			public ReadOnlySpan<byte> ResolveValue(ReadOnlySpan<byte> sourcePage)
				=> this.ValueLength == 0 ? default
				: this.Buffer is null ? sourcePage.Slice(this.ValueOffset, this.ValueLength) : this.Buffer.AsSpan(this.ValueOffset, this.ValueLength);
		}

		/// <summary>Inserts or replaces one key; values above the inline threshold go to a contiguous extent.</summary>
		public void Insert(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			Contract.Requires(key.Length <= FdbLiteTreePage.MaxKeyLength);

			var cellScratch = ArrayPool<byte>.Shared.Rent(key.Length + Math.Max(Math.Min(value.Length, this.Pager.Geometry.MaxInlineValueLength), FdbLiteTreePage.ExtentDescriptorSize));
			try
			{
				InsertCell(key, BuildValueCell(cellScratch, key, value));
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(cellScratch);
			}
		}

		/// <summary>Gathers the two parts of a leaf cell into <paramref name="scratch"/>: the key, then the bytes the value heap will hold, which are the value itself below the inline threshold and an extent descriptor above it.</summary>
		private CellRef BuildValueCell(byte[] scratch, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			key.CopyTo(scratch);
			if (value.Length <= this.Pager.Geometry.MaxInlineValueLength)
			{
				value.CopyTo(scratch.AsSpan(key.Length));
				return CellRef.OfLeafBuffer(scratch, key.Length, value.Length, 0);
			}

			// write the value as a headerless contiguous extent, zero-padding the last block
			int blockSize = this.Pager.Geometry.BlockSize;
			int blockCount = (value.Length + blockSize - 1) / blockSize;
			uint start = this.Allocator.AllocateExtent((uint) blockCount);
			this.ShadowExtents.Add(start);

			var padded = ArrayPool<byte>.Shared.Rent(blockCount * blockSize);
			try
			{
				var span = padded.AsSpan(0, blockCount * blockSize);
				value.CopyTo(span);
				span[value.Length..].Clear();
				this.Pager.WriteBlocks(start, span);
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(padded);
			}

			ulong checksum = System.IO.Hashing.XxHash3.HashToUInt64(value, unchecked((long) start));
			FdbLiteTreePage.BuildExtentDescriptor(scratch.AsSpan(key.Length), start, (ushort) blockCount, (uint) value.Length, checksum);
			return CellRef.OfLeafBuffer(scratch, key.Length, FdbLiteTreePage.ExtentDescriptorSize, FdbLiteTreePage.FlagValueIsExtent);
		}

		private void InsertCell(ReadOnlySpan<byte> key, CellRef newCell)
		{
			if (this.Root == 0)
			{ // first key: a fresh single-cell leaf becomes the root, and covers the whole keyspace
				var result = WriteCells(0, isInternal: false, leftmostChild: 0, default, [ newCell ]);
				Contract.Debug.Assert(!result.Split);
				this.Root = result.FirstId;
				this.KeyCountDelta++;
				this.CursorLeaf = result.FirstId;
				this.CursorLower = null;
				this.CursorUpper = null;
				return;
			}

			if (CursorCovers(key) && TrySpliceInto(this.CursorLeaf, key, newCell))
			{ // the last descent already proved this leaf takes this key, and its free area had room: no descent,
			  // and no ancestor to patch, since the page neither moved nor split
				return;
			}

			// descend to the leaf, remembering the path
			Span<uint> pathPages = stackalloc uint[MaxDepth];
			Span<int> pathChildren = stackalloc int[MaxDepth];
			uint pageId = DescendToLeaf(key, pathPages, pathChildren, out int depth);

			var outcome = RebuildLeafWithInsert(pageId, key, newCell, rightmost: this.CursorUpper is null);
			AscendPatch(pathPages, pathChildren, depth - 1, pageId, outcome);

			// the descent set the cursor on the leaf it landed on: a rebuild may have relocated that leaf (same
			// keys, same range), a split invalidates the range outright
			this.CursorLeaf = outcome.Split ? 0 : outcome.FirstId;
		}

		/// <summary>Walks from the root to the leaf covering <paramref name="key"/>, recording internal pages and child indexes, and positioning the writer's cursor on the leaf it reaches.</summary>
		private uint DescendToLeaf(ReadOnlySpan<byte> key, Span<uint> pathPages, Span<int> pathChildren, out int depth)
		{
			depth = 0;
			this.LeafDescents++;
			uint pageId = this.Root;
			byte[]? lower = null, upper = null;
			while (true)
			{
				var page = ReadPage(pageId);
				if (FdbLitePageHeader.GetPageType(page) == FdbLitePageType.Leaf)
				{
					this.CursorLeaf = pageId;
					this.CursorLower = lower;
					this.CursorUpper = upper;
					return pageId;
				}
				Contract.Debug.Assert(depth < MaxDepth);
				int childIndex = FdbLiteTreePage.FindChildIndex(page, key);

				// the separators on either side of the chosen child bound its whole subtree, and each level can only
				// narrow that range: what reaches the leaf is exactly the range routed to it (copied, since the
				// mutation about to happen can rewrite the page these spans point into)
				if (childIndex > 0)
				{
					lower = FdbLiteTreePage.GetSeparator(page, childIndex - 1).ToArray();
				}
				if (childIndex < FdbLitePageHeader.GetCellCount(page))
				{
					upper = FdbLiteTreePage.GetSeparator(page, childIndex).ToArray();
				}

				pathPages[depth] = pageId;
				pathChildren[depth] = childIndex;
				depth++;
				pageId = FdbLiteTreePage.GetChild(page, childIndex);
			}
		}

		/// <summary>Ascends from <paramref name="fromLevel"/>, patching each parent's child pointer and inserting separators for split siblings; grows the root as needed.</summary>
		private void AscendPatch(ReadOnlySpan<uint> pathPages, ReadOnlySpan<int> pathChildren, int fromLevel, uint originalChildId, RebuildResult outcome)
		{
			for (int level = fromLevel; level >= 0; level--)
			{
				if (!outcome.Split && outcome.FirstId == originalChildId)
				{ // the child was rebuilt in place: the parent (and every ancestor) already points at it
					return;
				}
				originalChildId = pathPages[level];
				outcome = RebuildInternal(pathPages[level], pathChildren[level], outcome);
			}

			// the root may have been relocated, and may have split (possibly more than once)
			while (outcome.Split)
			{
				outcome = BuildRootLevel(outcome);
			}
			this.Root = outcome.FirstId;
		}

		#region Deletion...

		/// <summary>Removes one key; returns false when it was not present.</summary>
		/// <remarks>No sibling merging on underflow: a shrunken leaf persists until a later rebuild touches it, only EMPTIED pages are unlinked and released, and range deletes drop whole leaves. (ponytail: merge-on-underflow is the space-amplification upgrade if delete-heavy workloads ever measure it)</remarks>
		public bool Remove(ReadOnlySpan<byte> key)
		{
			if (this.Root == 0)
			{
				return false;
			}

			if (CursorCovers(key) && TryRemoveInPlace(this.CursorLeaf, key, out bool removedInPlace))
			{ // the last descent already proved this leaf covers this key, and the page is ours to edit: no
			  // descent, no rebuild, and no ancestor to patch since the page neither moved nor changed range
				return removedInPlace;
			}

			Span<uint> pathPages = stackalloc uint[MaxDepth];
			Span<int> pathChildren = stackalloc int[MaxDepth];
			uint leafId = DescendToLeaf(key, pathPages, pathChildren, out int depth);

			var page = ReadPage(leafId);
			int slot = FdbLiteTreePage.FindLeafSlot(page, key, out bool exact);
			if (!exact)
			{
				return false;
			}

			// NOTE: a copy-verbatim-then-remove path was tried here and MEASURED WORSE - random deletes went
			// 23,930 -> 46,225 ns because every one lands on a different page, so it paid a full page copy per
			// delete and never got to amortise it. The rebuild re-serialises only the LIVE cells and produces a
			// compact page, which is the better trade when the page will not be touched again. Do not
			// reintroduce it without a measurement; the symmetry with the replace path is misleading.
			DropLeafSlots(leafId, page, slot, slot + 1, pathPages, pathChildren, depth);
			CollapseRoot();
			return true;
		}

		/// <summary>Removes every key in <c>[begin, end)</c>; returns the number removed.</summary>
		public int RemoveRange(ReadOnlySpan<byte> begin, ReadOnlySpan<byte> end)
		{
			Span<uint> pathPages = stackalloc uint[MaxDepth];
			Span<int> pathChildren = stackalloc int[MaxDepth];

			int total = 0;
			while (this.Root != 0)
			{
				// the first key at/above 'begin' pins the leaf to clear next (over the overlay: earlier passes of
				// this loop have already modified pages that are still only in the writer's buffers)
				var cursor = new FdbLiteTreeCursor(this.PagerView, this.Root);
				if (!cursor.SeekCeiling(begin))
				{
					break;
				}
				if (cursor.CurrentKey.SequenceCompareTo(end) >= 0)
				{
					break;
				}
				// the rebuild below invalidates cursor memory: the descent key must be a copy
				var target = cursor.CurrentKey.ToArray();

				uint leafId = DescendToLeaf(target, pathPages, pathChildren, out int depth);
				var page = ReadPage(leafId);
				int cellCount = FdbLitePageHeader.GetCellCount(page);
				int first = FdbLiteTreePage.FindLeafSlot(page, begin, out _);
				int last = first;
				// compares against the WHOLE key: the stored key is only a suffix once the page strips a prefix
				while (last < cellCount && FdbLiteTreePage.CompareLeafKey(page, last, end) < 0)
				{
					last++;
				}
				Contract.Debug.Assert(last > first, "the ceiling key lives in this leaf, so at least one cell is in range");

				total += last - first;
				DropLeafSlots(leafId, page, first, last, pathPages, pathChildren, depth);
				CollapseRoot();
			}
			return total;
		}

		/// <summary>Drops leaf cells [<paramref name="first"/>, <paramref name="last"/>): releases their extents, rebuilds the leaf, or unlinks it entirely when it empties.</summary>
		private void DropLeafSlots(uint leafId, ReadOnlySpan<byte> page, int first, int last, ReadOnlySpan<uint> pathPages, ReadOnlySpan<int> pathChildren, int depth)
		{
			int cellCount = FdbLitePageHeader.GetCellCount(page);
			for (int i = first; i < last; i++)
			{
				FreeExtentOfCell(page, i);
			}
			this.KeyCountDelta -= last - first;

			if (last - first == cellCount)
			{ // the leaf empties: unlink it from its ancestors, and NO cursor survives that
				this.CursorLeaf = 0;
				FreePage(leafId);
				if (depth == 0)
				{
					this.Root = 0;
					return;
				}
				RemoveChildFromAncestors(pathPages, pathChildren, depth);
				return;
			}

			var cells = new CellRef[cellCount - (last - first)];
			int w = 0;
			for (int i = 0; i < cellCount; i++)
			{
				if (i >= first && i < last) { continue; }
				cells[w++] = CellRef.OfLeafPage(page, i);
			}
			var outcome = WriteCells(leafId, isInternal: false, leftmostChild: 0, page, cells);
			AscendPatch(pathPages, pathChildren, depth - 1, leafId, outcome);

			// The page survived: it holds the same key RANGE, its parent's separators did not move, and the
			// descent that got us here already set the cursor's bounds. Only its id changed. Keeping the cursor
			// is what lets the next delete in this batch take the in-place path instead of descending again -
			// discarding it here meant the fast path could never engage twice in a row.
			this.CursorLeaf = outcome.Split ? 0 : outcome.FirstId;
		}

		/// <summary>Removes the child at the deepest path level from its parent, cascading upward while parents empty out.</summary>
		private void RemoveChildFromAncestors(ReadOnlySpan<uint> pathPages, ReadOnlySpan<int> pathChildren, int depth)
		{
			int level = depth - 1;
			while (level >= 0)
			{
				uint pageId = pathPages[level];
				int childIndex = pathChildren[level];
				var page = ReadPage(pageId);
				int cellCount = FdbLitePageHeader.GetCellCount(page);

				if (cellCount == 0)
				{ // a leftmost-only page loses its only child: the page itself dies, cascade up
					Contract.Debug.Assert(childIndex == 0);
					FreePage(pageId);
					level--;
					continue;
				}

				var outcome = RebuildInternalRemoveChild(pageId, page, childIndex);
				AscendPatch(pathPages, pathChildren, level - 1, pageId, outcome);
				return;
			}

			// every level died: the tree is empty
			this.Root = 0;
		}

		/// <summary>Rebuilds an internal page without one child (never splits: it only shrinks).</summary>
		private RebuildResult RebuildInternalRemoveChild(uint pageId, ReadOnlySpan<byte> page, int childIndex)
		{
			int cellCount = FdbLitePageHeader.GetCellCount(page);
			uint leftmost = FdbLiteTreePage.GetLeftmostChild(page);

			var cells = new CellRef[cellCount - 1];
			int w = 0;
			if (childIndex == 0)
			{ // the leftmost child dies: cell 0's child is the new leftmost, and its separator disappears
				leftmost = FdbLiteTreePage.GetChild(page, 1);
				for (int i = 1; i < cellCount; i++)
				{
					cells[w++] = CellRef.OfInternalPage(FdbLiteTreePage.GetInternalCellExtent(page, i));
				}
			}
			else
			{ // cell childIndex-1 carried the dead child
				for (int i = 0; i < cellCount; i++)
				{
					if (i == childIndex - 1) { continue; }
					cells[w++] = CellRef.OfInternalPage(FdbLiteTreePage.GetInternalCellExtent(page, i));
				}
			}
			Contract.Debug.Assert(w == cells.Length);

			var outcome = WriteCells(pageId, isInternal: true, leftmost, page, cells);
			Contract.Debug.Assert(!outcome.Split);
			return outcome;
		}

		/// <summary>Shrinks a degenerate root chain (internal pages with a single child) and clears the empty tree.</summary>
		private void CollapseRoot()
		{
			while (this.Root != 0)
			{
				var page = ReadPage(this.Root);
				if (FdbLitePageHeader.GetPageType(page) != FdbLitePageType.Internal || FdbLitePageHeader.GetCellCount(page) > 0)
				{
					break;
				}
				uint child = FdbLiteTreePage.GetLeftmostChild(page);
				FreePage(this.Root);
				this.Root = child;
			}
		}

		/// <summary>Releases a whole tree page (immediately when this generation wrote it, else after the horizon).</summary>
		private void FreePage(uint pageId)
		{
			uint blocks = (uint) this.Pager.Geometry.BlocksPerPage;
			this.Dirty.Remove(pageId); // a released page must not be written back by a later flush

			if (this.Shadow.Remove(pageId))
			{
				this.Allocator.FreeSpace.FreeImmediately(pageId, blocks);
			}
			else
			{
				this.Allocator.Free(pageId, blocks, this.Generation);
			}
		}

		#endregion

		#region Page rebuilding...

		/// <summary>Splices a new key into a page image this generation already owns, when its free area can take the cell.</summary>
		/// <returns><c>false</c> when the page is not owned yet, the key is a REPLACE, or the cell does not fit: all three are the caller's signal to take the rebuild path, which compacts and splits.</returns>
		private bool TrySpliceInto(uint leafId, ReadOnlySpan<byte> key, CellRef newCell)
		{
			if (!this.Dirty.TryGetValue(leafId, out var buffered))
			{
				return false;
			}

			// splicing into the free area beats re-gathering and re-serializing every cell in the page (the rebuild
			// path is O(cells) per insert)
			var image = buffered.AsSpan();
			int at = FdbLiteTreePage.FindLeafSlot(image, key, out bool exists);
			if (exists)
			{
				// A REPLACE. When the replacement occupies exactly the same room this is a memcpy over bytes
				// this generation already owns: nothing moves, and the cursor stays valid, so the per-key
				// descent disappears along with the rebuild.
				// An extent on EITHER side is deliberately excluded. Those blocks have to be freed and
				// reallocated, which is not an overwrite - it is precisely where a leak or a double free would
				// live - so it keeps the rebuild path.
				if (newCell.Flags == 0
				 && (FdbLiteTreePage.GetLeafFlags(image, at) & FdbLiteTreePage.FlagValueIsExtent) == 0)
				{
					var storedValue = newCell.ResolveValue(default);
					// fits where it lies, or grows into the free gap and leaves its old slot behind as waste
					if (FdbLiteTreePage.TryOverwriteLeafValue(image, at, storedValue, newCell.Flags)
					 || FdbLiteTreePage.TryRelocateLeafValue(image, at, storedValue, newCell.Flags))
					{
						this.CellsOverwritten++;
						// deliberately NOT KeyCountDelta: a replace introduces no key
						return true;
					}
				}
				return false;
			}

			if (!FdbLiteTreePage.TryInsertLeafCell(image, at, newCell.ResolveKey(default), newCell.ResolveValue(default), newCell.Flags))
			{
				return false;
			}

			this.CellsSpliced++;
			this.KeyCountDelta++;
			return true;
		}

		/// <summary>Removes a key from a page this generation already owns, by closing the directory over it.</summary>
		/// <returns><c>true</c> when the operation was HANDLED here (whether or not a key was actually removed); <c>false</c> means the caller must descend and take the rebuild path.</returns>
		/// <remarks>
		/// The delete counterpart of <see cref="TrySpliceInto"/>. Note the two different falses: an absent key
		/// is handled (the cursor proved this leaf is where it would have been, so it is definitively not in
		/// the tree) and reported through <paramref name="removed"/>, whereas a page this generation does not
		/// own, an extent value, or a page down to its last cell are all handed back to the descent.
		/// <para>The cursor SURVIVES this. Removing a key from inside a page changes neither the page's
		/// identity nor the range its parent routes by, which is why no ancestor has to be patched.</para>
		/// </remarks>
		private bool TryRemoveInPlace(uint leafId, ReadOnlySpan<byte> key, out bool removed)
		{
			removed = false;
			if (!this.Dirty.TryGetValue(leafId, out var buffered))
			{
				return false;
			}

			var image = buffered.AsSpan();
			int at = FdbLiteTreePage.FindLeafSlot(image, key, out bool exists);
			if (!exists)
			{ // the cursor proved this leaf covers the key, so absent here means absent everywhere
				return true;
			}

			if (!FdbLiteTreePage.TryRemoveLeafCell(image, at))
			{
				return false;
			}

			this.CellsRemovedInPlace++;
			this.KeyCountDelta--;
			removed = true;
			return true;
		}

		/// <summary>Copies a page VERBATIM into this generation and overwrites one value in the copy.</summary>
		/// <remarks>
		/// <para>The companion to <see cref="TrySpliceInto"/>, for the case it cannot serve: the FIRST mutation
		/// of a page in a generation. That page is still shared with the committed generation, so it must be
		/// copied - but copying it is a block memcpy, whereas the rebuild path re-gathers and re-serialises
		/// every cell to achieve the same thing. For a workload that replaces ONE value per transaction, every
		/// replace is a first touch, so the rebuild is the entire cost and this is the whole fix.</para>
		/// <para>The prototype this engine succeeds has always done it this way: duplicate the page image, then
		/// mutate in place. This closes that gap.</para>
		/// </remarks>
		private bool TryCopyAndOverwrite(uint leafId, ReadOnlySpan<byte> key, in CellRef newCell, out uint newId)
		{
			newId = 0;
			if (newCell.Flags != 0 || this.Dirty.ContainsKey(leafId))
			{ // an extent is not an overwrite; an already-owned page is TrySpliceInto's job and must not be
			  // copied a second time
				return false;
			}

			var page = ReadPage(leafId);
			if (FdbLitePageHeader.GetPageType(page) != FdbLitePageType.Leaf)
			{
				return false;
			}

			int at = FdbLiteTreePage.FindLeafSlot(page, key, out bool exists);
			if (!exists || (FdbLiteTreePage.GetLeafFlags(page, at) & FdbLiteTreePage.FlagValueIsExtent) != 0)
			{
				return false;
			}

			var storedValue = newCell.ResolveValue(default);
			if (storedValue.Length > FdbLiteTreePage.LeafValueExtent(page, at).Length
			 && storedValue.Length > FdbLiteTreePage.LeafFreeGap(page))
			{ // it neither fits the room it already has nor the free gap it could grow into, so the page has to
			  // be reclaimed or split: that is the rebuild path's job
				return false;
			}

			// snapshot before allocating: WritePage allocates, and an allocation may grow the pager, which is
			// exactly the source-page aliasing WriteCells already guards against. One page memcpy is still far
			// cheaper than re-serialising every cell.
			int pageSize = this.Pager.Geometry.PageSize;
			var snapshot = ArrayPool<byte>.Shared.Rent(pageSize);
			try
			{
				page.CopyTo(snapshot);
				newId = WritePage(leafId, snapshot.AsSpan(0, pageSize));
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(snapshot);
			}

			var copy = this.Dirty[newId].AsSpan();
			bool overwritten = FdbLiteTreePage.TryOverwriteLeafValue(copy, at, storedValue, newCell.Flags)
			                || FdbLiteTreePage.TryRelocateLeafValue(copy, at, storedValue, newCell.Flags);
			Contract.Debug.Assert(overwritten, "the mutation was proved possible on the source image before the copy");
			this.CellsOverwritten++;
			// deliberately NOT KeyCountDelta: a replace introduces no key
			return true;
		}

		/// <summary>Rebuilds a leaf with one key inserted or replaced (a replaced extent value is released).</summary>
		/// <param name="rightmost">True when no separator bounds this leaf on the right, i.e. it holds the highest keys in the tree</param>
		private RebuildResult RebuildLeafWithInsert(uint leafId, ReadOnlySpan<byte> key, CellRef newCell, bool rightmost)
		{
			if (TrySpliceInto(leafId, key, newCell))
			{
				return new(leafId, null);
			}

			if (TryCopyAndOverwrite(leafId, key, newCell, out uint copiedId))
			{ // first touch of this page in this generation, and the mutation is an overwrite: the page still
			  // has to be copied, but it does NOT have to be re-serialised
				return new(copiedId, null);
			}

			// The splice failed, which is the FIRST moment this page's key set is known, and therefore the first
			// moment its shared prefix can be computed without re-expanding suffixes. Splicing cannot do it, so a
			// page filled entirely by splices carries no prefix at all - which is every sequentially built page.
			// Strip it now: shorter keys may make room, in which case the key fits and no page is spilled or split.
			// The rebuild may RELOCATE the page (copy-on-write), so the caller is told where it went. Once it has
			// been rebuilt, everything below MUST work from the new page: rebuilding the original a second time
			// would queue its blocks for free twice.
			uint strippedId = TryStripAndRetry(leafId, key, newCell, out bool spliced);
			if (strippedId != 0)
			{
				if (spliced)
				{
					return new(strippedId, null);
				}
				leafId = strippedId;
			}

			var page = ReadPage(leafId);
			int cellCount = FdbLitePageHeader.GetCellCount(page);
			int insertAt = FdbLiteTreePage.FindLeafSlot(page, key, out bool replace);

			if (this.AvoidSequentialAppendSplits
			 && rightmost && !replace && insertAt == cellCount
			 && !FdbLiteTreePage.LeafHasRoomFor(page, newCell.KeyLength, newCell.ValueLength))
			{ // a key appending past the last one in the rightmost leaf: nothing will ever insert into this page
			  // again, so splitting it in half strands half of it forever. Leave it packed and start a fresh page,
			  // which the ascent hangs off the parent as a right sibling separated by this very key.
				this.KeyCountDelta++;
				this.PagesAppended++;
				var fresh = WriteCells(0, isInternal: false, leftmostChild: 0, default, [ newCell ]);
				Contract.Debug.Assert(!fresh.Split);
				return new(leafId, [ (key.ToArray(), fresh.FirstId) ]);
			}

			if (replace)
			{
				// counted HERE and not at the splice attempt: this is the single point a replace actually
				// pays for a rebuild, and it is reached once, whereas TrySpliceInto can be attempted twice for
				// the same key (once off the cursor, once after the descent)
				this.ReplacesRebuilt++;
				FreeExtentOfCell(page, insertAt);
			}
			else
			{
				this.KeyCountDelta++;
			}

			int resultCount = cellCount + (replace ? 0 : 1);
			var cells = new CellRef[resultCount];
			int w = 0;
			for (int i = 0; i < cellCount; i++)
			{
				if (i == insertAt)
				{
					cells[w++] = newCell;
					if (replace) { continue; }
				}
				cells[w++] = CellRef.OfLeafPage(page, i);
			}
			if (insertAt == cellCount)
			{ // appending past the last key
				cells[w++] = newCell;
			}
			Contract.Debug.Assert(w == resultCount);

			return WriteCells(leafId, isInternal: false, leftmostChild: 0, page, cells);
		}

		/// <summary>Rebuilds a full leaf so it strips the prefix its keys share, then retries the splice into the room that frees.</summary>
		/// <returns>The page's id after the rebuild, which may differ because rebuilding relocates a page this generation does not already own; <c>0</c> when there is nothing to gain, so the caller proceeds to spill or split.</returns>
		/// <remarks>
		/// <para>This CANNOT loop into a rebuild per insert, and the reason is worth stating because it is the obvious hazard. Rebuilding sets the page's prefix to exactly the longest one its keys share, so a second call computes the same value, finds no gain, and returns false. Once stripped, a page is stripped; every later splice either fits or spills. A key that does NOT share the prefix is refused by the splice itself and spills, which puts a page boundary exactly at a prefix divergence, where locality wants one anyway.</para>
		/// <para>Cost is one rebuild per page, at the moment it fills: amortised O(1) per key, not O(cells) per insert.</para>
		/// </remarks>
		private uint TryStripAndRetry(uint leafId, ReadOnlySpan<byte> key, in CellRef newCell, out bool spliced)
		{
			spliced = false;
			var page = ReadPage(leafId);
			int cellCount = FdbLitePageHeader.GetCellCount(page);
			if (cellCount < 2)
			{
				return 0;
			}

			int current = FdbLitePageHeader.GetPrefixLength(page);
			var prefix = FdbLiteTreePage.GetPagePrefix(page, isInternal: false);

			// the page is in key order, so what all of its keys share is what its first and last share; both are
			// stored as suffixes of the CURRENT prefix, so the shared run is that prefix plus their common suffix
			int shared = current + FdbLiteTreePage.CommonPrefixLength(FdbLiteTreePage.GetLeafKey(page, 0), FdbLiteTreePage.GetLeafKey(page, cellCount - 1));
			if (shared <= current)
			{ // already stripped as far as it goes: this is the guard that makes a second attempt a no-op
				return 0;
			}

			// the incoming key must share the new prefix too, or the rebuild would only have to be undone
			if (key.Length < shared || !key[..shared].SequenceEqual(WholeKeyOf(page, 0).AsSpan(0, shared)))
			{
				return 0;
			}

			var cells = new CellRef[cellCount];
			for (int i = 0; i < cellCount; i++)
			{
				cells[i] = CellRef.OfLeafPage(page, i);
			}

			var rebuilt = WriteCells(leafId, isInternal: false, leftmostChild: 0, page, cells);
			if (rebuilt.Split)
			{ // the run did not fit one page after all; the caller's split path handles it properly
				return 0;
			}

			this.PagesStripped++;
			spliced = TrySpliceInto(rebuilt.FirstId, key, newCell);
			return rebuilt.FirstId;
		}

		/// <summary>Whole key of leaf cell <paramref name="cellIndex"/> on <paramref name="page"/>, prefix included.</summary>
		private static byte[] WholeKeyOf(ReadOnlySpan<byte> page, int cellIndex)
		{
			var prefix = FdbLiteTreePage.GetPagePrefix(page, isInternal: false);
			var suffix = FdbLiteTreePage.GetLeafKey(page, cellIndex);
			var whole = new byte[prefix.Length + suffix.Length];
			prefix.CopyTo(whole);
			suffix.CopyTo(whole.AsSpan(prefix.Length));
			return whole;
		}

		/// <summary>Releases the extent of a leaf cell, if it has one (immediately when this generation wrote it, else after the horizon).</summary>
		private void FreeExtentOfCell(ReadOnlySpan<byte> page, int cellIndex)
		{
			if ((FdbLiteTreePage.GetLeafFlags(page, cellIndex) & FdbLiteTreePage.FlagValueIsExtent) == 0)
			{
				return;
			}
			var (start, blockCount, _, _) = FdbLiteTreePage.GetLeafExtentDescriptor(page, cellIndex);
			if (this.ShadowExtents.Remove(start))
			{
				this.Allocator.FreeSpace.FreeImmediately(start, blockCount);
			}
			else
			{
				this.Allocator.Free(start, blockCount, this.Generation);
			}
		}

		/// <summary>Rebuilds an internal page after a child rebuild: the descended child pointer becomes <paramref name="child"/>.FirstId, and each split sibling inserts one separator cell after it.</summary>
		private RebuildResult RebuildInternal(uint pageId, int childIndex, RebuildResult child)
		{
			var page = ReadPage(pageId);
			int cellCount = FdbLitePageHeader.GetCellCount(page);
			int inserted = child.Siblings?.Count ?? 0;

			// scratch for the patched cell and each inserted separator cell
			byte[]? patchScratch = null;
			var siblingScratch = new byte[inserted][];
			try
			{
				uint leftmost = FdbLiteTreePage.GetLeftmostChild(page);

				var patchedCell = default(CellRef);
				if (childIndex == 0)
				{
					leftmost = child.FirstId;
				}
				else
				{
					var original = FdbLiteTreePage.GetInternalCell(page, childIndex - 1);
					patchScratch = ArrayPool<byte>.Shared.Rent(original.Length);
					original.CopyTo(patchScratch);
					FdbLiteTreePage.PatchInternalCellChild(patchScratch, child.FirstId);
					patchedCell = CellRef.OfInternalBuffer(patchScratch, original.Length);
				}

				var cells = new CellRef[cellCount + inserted];
				int w = 0;
				for (int i = 0; i <= cellCount; i++)
				{
					if (i == childIndex)
					{ // the sibling separators slot in right after the descended child
						for (int s = 0; s < inserted; s++)
						{
							var (separator, siblingId) = child.Siblings![s];
							siblingScratch[s] = ArrayPool<byte>.Shared.Rent(6 + separator.Length);
							int len = FdbLiteTreePage.BuildInternalCell(siblingScratch[s], siblingId, separator).Length;
							cells[w++] = CellRef.OfInternalBuffer(siblingScratch[s], len);
						}
					}
					if (i < cellCount)
					{
						cells[w++] = (i == childIndex - 1) ? patchedCell : CellRef.OfInternalPage(FdbLiteTreePage.GetInternalCellExtent(page, i));
					}
				}
				Contract.Debug.Assert(w == cells.Length);

				return WriteCells(pageId, isInternal: true, leftmost, page, cells);
			}
			finally
			{
				if (patchScratch != null) { ArrayPool<byte>.Shared.Return(patchScratch); }
				foreach (var s in siblingScratch)
				{
					if (s != null) { ArrayPool<byte>.Shared.Return(s); }
				}
			}
		}

		/// <summary>Builds one new root level over a split result (loops in the caller if the new level itself splits).</summary>
		private RebuildResult BuildRootLevel(RebuildResult split)
		{
			var siblings = split.Siblings!;
			var scratches = new byte[siblings.Count][];
			try
			{
				var cells = new CellRef[siblings.Count];
				for (int i = 0; i < siblings.Count; i++)
				{
					var (separator, id) = siblings[i];
					scratches[i] = ArrayPool<byte>.Shared.Rent(6 + separator.Length);
					int len = FdbLiteTreePage.BuildInternalCell(scratches[i], id, separator).Length;
					cells[i] = CellRef.OfInternalBuffer(scratches[i], len);
				}
				return WriteCells(0, isInternal: true, leftmostChild: split.FirstId, default, cells);
			}
			finally
			{
				foreach (var s in scratches)
				{
					if (s != null) { ArrayPool<byte>.Shared.Return(s); }
				}
			}
		}

		/// <summary>Writes a rebuilt cell list as one page, or as a K-way split when it does not fit (greedy: each page takes the largest prefix that fits).</summary>
		private RebuildResult WriteCells(uint oldPageId, bool isInternal, uint leftmostChild, ReadOnlySpan<byte> sourcePage, CellRef[] cells)
		{
			var type = isInternal ? FdbLitePageType.Internal : FdbLitePageType.Leaf;
			int pageSize = this.Pager.Geometry.PageSize;

			// an internal page stores whole separators and strips nothing, so its directory starts right after the header
			int usable = pageSize - FdbLiteTreePage.SlotsOffset(isInternal, prefixRegionSize: 0);

			// a leaf's cells carry the prefix of the page they came FROM, and will be stored against the prefix of
			// the page they land ON; the two differ, so the sizing has to name which one it means
			int sourcePrefixLength = !isInternal && sourcePage.Length > 0 && FdbLitePageHeader.GetCellCount(sourcePage) > 0
				? FdbLitePageHeader.GetPrefixLength(sourcePage)
				: 0;

			long totalBytes = 0;
			if (isInternal)
			{
				foreach (var cell in cells)
				{
					totalBytes += CellFootprint(cell, isInternal);
				}
			}
			else
			{
				// the whole run's shared prefix is what the first and last key share (they are in key order), which
				// is the right basis for the BALANCE estimate; each part re-derives its own below
				int runLcp = 0;
				if (cells.Length > 1)
				{
					var estimateScratch = ArrayPool<byte>.Shared.Rent(2 * FdbLiteTreePage.MaxKeyLength);
					try
					{
						var sp = sourcePrefixLength > 0 ? FdbLiteTreePage.GetPagePrefix(sourcePage, isInternal: false) : default;
						var lowest = MaterializeKey(cells[0], sourcePage, sp, estimateScratch.AsSpan(0, FdbLiteTreePage.MaxKeyLength));
						var highest = MaterializeKey(cells[^1], sourcePage, sp, estimateScratch.AsSpan(FdbLiteTreePage.MaxKeyLength, FdbLiteTreePage.MaxKeyLength));
						runLcp = FdbLiteTreePage.CommonPrefixLength(lowest, highest);
					}
					finally
					{
						ArrayPool<byte>.Shared.Return(estimateScratch);
					}
				}

				long sumWhole = 0, sumValue = 0;
				foreach (var cell in cells)
				{
					sumWhole += LeafWholeKeyLength(cell, sourcePrefixLength);
					sumValue += cell.ValueLength;
				}
				// Demand and capacity on the SAME basis: LeafRunBytes is the FULL page footprint (header and
				// prefix region included), so the capacity it is tested against is the full page size.
				// Subtracting the prefix region from the capacity while the demand still contained it opened
				// a false-positive band exactly one prefix-region wide, where a run that FITS one page was
				// planned - and then genuinely executed, via the balance target - as a two-way split.
				// TryStripAndRetry then discarded the split it never expected, orphaning half the leaf.
				totalBytes = LeafRunBytes(cells.Length, sumWhole, sumValue, runLcp);
				usable = pageSize;
			}

			var scratch = ArrayPool<byte>.Shared.Rent(pageSize);
			var partScratch = ArrayPool<byte>.Shared.Rent(FdbLiteTreePage.MaxKeyLength);
			byte[]? sourceCopy = null;
			try
			{
				if (totalBytes > usable && !sourcePage.IsEmpty)
				{ // splitting: part 0 may rewrite the source page in place (shadowed), which would clobber the
				  // memory later parts still resolve their cells from - snapshot the source first
					sourceCopy = ArrayPool<byte>.Shared.Rent(sourcePage.Length);
					sourcePage.CopyTo(sourceCopy);
					sourcePage = sourceCopy.AsSpan(0, sourcePage.Length);
				}

				var image = scratch.AsSpan(0, pageSize);
				List<(byte[] Separator, uint PageId)>? siblings = null;
				uint firstId = 0;

				// balanced K-way split: cutting at the LARGEST prefix that fits would leave near-empty right
				// siblings that collapse occupancy to ~20% under random inserts (a full left page re-splits on
				// its very next insert); aiming each part at total/K keeps post-split occupancy near half, and
				// the hard per-page limit still absorbs the adversarial giant-cell cases
				int partCount = (int) ((totalBytes + usable - 1) / usable);
				long targetBytes = partCount > 1 ? (totalBytes + partCount - 1) / partCount : long.MaxValue;

				int start = 0;
				uint partLeftmost = leftmostChild;
				byte[]? partSeparator = null;
				while (true)
				{
					// extend the part up to the balance target, never past the page capacity
					long bytes = 0;
					int end = start;
					if (isInternal)
					{
						while (end < cells.Length)
						{
							long next = bytes + CellFootprint(cells[end], isInternal);
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
					}
					else
					{
						// A leaf part is measured against the prefix IT will strip, which shrinks as the part grows and
						// makes every cell already in it a byte or more longer. So the cost of the whole part is
						// recomputed on each candidate rather than accumulated per cell: an incremental sum cannot
						// express "the cell I just added made all the previous ones bigger".
						var sourcePrefix = sourcePrefixLength > 0 ? FdbLiteTreePage.GetPagePrefix(sourcePage, isInternal: false) : default;
						var firstKey = MaterializeKey(cells[start], sourcePage, sourcePrefix, partScratch.AsSpan(0, FdbLiteTreePage.MaxKeyLength));
						int lcp = firstKey.Length;
						long sumWhole = 0, sumValue = 0;

						while (end < cells.Length)
						{
							int candidateLcp = end == start ? lcp : LeafLcpWith(firstKey, cells[end], sourcePage, sourcePrefix, lcp);
							long nextWhole = sumWhole + LeafWholeKeyLength(cells[end], sourcePrefixLength);
							long nextValue = sumValue + cells[end].ValueLength;
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
					}

					// on an internal boundary the boundary cell is PROMOTED: its child seeds the next part, its key separates
					int nextStart;
					byte[]? nextSeparator = null;
					uint nextLeftmost = 0;
					if (end < cells.Length)
					{
						if (isInternal)
						{
							var boundary = cells[end].ResolveKey(sourcePage);
							int keyLen = BinaryPrimitives.ReadUInt16LittleEndian(boundary[4..]);
							nextSeparator = boundary.Slice(6, keyLen).ToArray();
							nextLeftmost = BinaryPrimitives.ReadUInt32LittleEndian(boundary);
							nextStart = end + 1;
						}
						else
						{
							// The separator is promoted to the PARENT, which shares no prefix with this leaf, so it has
							// to be the WHOLE key. A cell gathered from a prefix-stripped page holds only its suffix,
							// and handing that up produces a parent whose separators are shorter than the keys they
							// route, which mis-sorts the tree rather than failing loudly.
							nextSeparator = WholeKeyOf(cells[end], sourcePage);
							nextStart = end;
						}
					}
					else
					{
						nextStart = end;
					}

					// write this part: the first one lands on the original page (copy-on-write applies), the rest are fresh
					FdbLitePageHeader.Format(image, type, this.Generation);
					if (isInternal) { FdbLiteTreePage.SetLeftmostChild(image, partLeftmost); }
					AppendCells(image, isInternal, sourcePage, cells, start, end);
					uint id = WritePage(partSeparator == null ? oldPageId : 0, image);
					if (partSeparator == null)
					{
						firstId = id;
					}
					else
					{
						(siblings ??= [ ]).Add((partSeparator, id));
					}

					if (nextStart >= cells.Length && nextSeparator == null)
					{
						break;
					}
					start = nextStart;
					partSeparator = nextSeparator;
					partLeftmost = nextLeftmost;

					if (start >= cells.Length && isInternal)
					{ // the last cell got promoted: the final part is a leftmost-only internal page (degenerate but legal)
						FdbLitePageHeader.Format(image, type, this.Generation);
						FdbLiteTreePage.SetLeftmostChild(image, partLeftmost);
						AppendCells(image, isInternal, sourcePage, cells, 0, 0);
						uint tailId = WritePage(0, image);
						(siblings ??= [ ]).Add((partSeparator!, tailId));
						break;
					}
				}

				if (siblings != null)
				{
					this.PageSplits++;
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

		/// <summary>The whole key of a gathered LEAF cell as an owned array, for a separator that leaves this page.</summary>
		private static byte[] WholeKeyOf(in CellRef cell, ReadOnlySpan<byte> sourcePage)
		{
			var stored = cell.ResolveKey(sourcePage);
			if (cell.Buffer is not null)
			{ // built here, so already whole
				return stored.ToArray();
			}

			var prefix = FdbLiteTreePage.GetPagePrefix(sourcePage, isInternal: false);
			if (prefix.Length == 0)
			{
				return stored.ToArray();
			}

			var whole = new byte[prefix.Length + stored.Length];
			prefix.CopyTo(whole);
			stored.CopyTo(whole.AsSpan(prefix.Length));
			return whole;
		}

		/// <summary>Assembles a gathered cell's WHOLE key, which a page-backed cell does not hold contiguously once its page strips a prefix.</summary>
		/// <remarks>Used only to compute the prefix of a page being built, which needs two whole keys and not the run, so this stays off the per-cell path.</remarks>
		private static ReadOnlySpan<byte> MaterializeKey(in CellRef cell, ReadOnlySpan<byte> sourcePage, ReadOnlySpan<byte> sourcePrefix, Span<byte> scratch)
		{
			var stored = cell.ResolveKey(sourcePage);
			if (cell.Buffer is not null || sourcePrefix.Length == 0)
			{ // already whole
				return stored;
			}
			sourcePrefix.CopyTo(scratch);
			stored.CopyTo(scratch[sourcePrefix.Length..]);
			return scratch[..(sourcePrefix.Length + stored.Length)];
		}

		/// <summary>Bytes one cell costs in a page, its slot included: an internal cell is contiguous, a leaf cell pays the fixed overhead of its two-region entry.</summary>
		/// <remarks>For a LEAF this is only the prefix-independent part; what the key itself costs depends on the prefix of the page it lands on, which is what <see cref="LeafRunBytes"/> accounts for.</remarks>
		private static int CellFootprint(in CellRef cell, bool isInternal)
			=> isInternal ? cell.KeyLength + 2 : cell.PayloadLength + FdbLiteTreePage.LeafCellOverhead;

		/// <summary>Length of a leaf cell's WHOLE key, putting back the prefix of the page it was gathered from.</summary>
		private static int LeafWholeKeyLength(in CellRef cell, int sourcePrefixLength)
			=> cell.Buffer is not null ? cell.KeyLength : sourcePrefixLength + cell.KeyLength;

		/// <summary>Bytes a run of <paramref name="count"/> leaf cells needs in a page, INCLUDING the prefix region and with every key stored relative to <paramref name="lcp"/>.</summary>
		/// <remarks>
		/// <para>Sizing a leaf run by the key lengths as stored in the SOURCE page is wrong whenever the destination strips a different prefix, and a carry inside a structured key forces exactly that: the shared run shortens, so every cell's stored key grows by the difference and a run planned to fit overruns the page. Everything else then behaves perfectly - each cell is written faithfully, executing a plan that was already wrong - so the damage shows up only as the two heaps crossing.</para>
		/// <para>A single-cell page strips nothing (there is no second key to share with), so its key is stored whole.</para>
		/// </remarks>
		private static long LeafRunBytes(int count, long sumWholeKeyLengths, long sumValueLengths, int lcp)
		{
			int effective = count > 1 ? lcp : 0;
			return FdbLiteTreePage.SlotsOffset(isInternal: false, prefixRegionSize: (effective + 1) & ~1)
				+ sumWholeKeyLengths - ((long) count * effective)
				+ ((long) count * FdbLiteTreePage.LeafCellOverhead)
				+ sumValueLengths;
		}

		/// <summary>Longest prefix <paramref name="first"/> shares with a cell's whole key, without materializing that key.</summary>
		/// <remarks>Capped at <paramref name="cap"/> because the run's shared prefix only ever SHRINKS as the run grows, so the comparison never has to look past what is still shared.</remarks>
		private static int LeafLcpWith(ReadOnlySpan<byte> first, in CellRef cell, ReadOnlySpan<byte> sourcePage, ReadOnlySpan<byte> sourcePrefix, int cap)
		{
			var stored = cell.ResolveKey(sourcePage);
			int n = 0;
			if (cell.Buffer is null && sourcePrefix.Length > 0)
			{ // the key is the source page's prefix followed by the stored suffix: walk them in turn
				int head = Math.Min(cap, sourcePrefix.Length);
				while (n < head && first[n] == sourcePrefix[n]) { ++n; }
				if (n < head) { return n; }
				for (int k = 0; n < cap && k < stored.Length && first[n] == stored[k]; ++k) { ++n; }
				return n;
			}
			while (n < cap && n < stored.Length && first[n] == stored[n]) { ++n; }
			return n;
		}

		/// <summary>Appends cells [<paramref name="start"/>, <paramref name="end"/>) (already in key order) to a freshly formatted page image.</summary>
		private static void AppendCells(Span<byte> image, bool isInternal, ReadOnlySpan<byte> sourcePage, CellRef[] cells, int start, int end)
		{
			int count = end - start;

			if (!isInternal)
			{
				// A cell gathered from a page that stripped a prefix carries only its SUFFIX, while a freshly built
				// cell carries a whole key. So a key here is conceptually (sourcePrefix if page-backed) + stored, and
				// the prefix this page will strip is computed over those whole keys.
				var sourcePrefix = sourcePage.Length > 0 && FdbLitePageHeader.GetCellCount(sourcePage) > 0
					? FdbLiteTreePage.GetPagePrefix(sourcePage, isInternal: false)
					: default;

				// only the FIRST and LAST keys are assembled, never the whole run: the run is in key order, so the
				// prefix shared by all of its keys is the one shared by those two, and the middle cannot diverge and
				// then converge again
				var keyScratch = ArrayPool<byte>.Shared.Rent(2 * FdbLiteTreePage.MaxKeyLength);
				int prefixLen;
				try
				{
					var firstKey = MaterializeKey(cells[start], sourcePage, sourcePrefix, keyScratch.AsSpan(0, FdbLiteTreePage.MaxKeyLength));
					var lastKey = MaterializeKey(cells[end - 1], sourcePage, sourcePrefix, keyScratch.AsSpan(FdbLiteTreePage.MaxKeyLength, FdbLiteTreePage.MaxKeyLength));
					prefixLen = count > 1 ? FdbLiteTreePage.CommonPrefixLength(firstKey, lastKey) : 0;

					// must precede the run: the prefix sits in front of the slot directory, so it decides where every
					// offset in this page lands
					FdbLiteTreePage.WriteLeafPrefix(image, firstKey[..prefixLen]);
				}
				finally
				{
					ArrayPool<byte>.Shared.Return(keyScratch);
				}

				var run = new FdbLiteTreePage.LeafRunWriter(image, count);
				for (int i = start; i < end; i++)
				{
					var stored = cells[i].ResolveKey(sourcePage);
					var value = cells[i].ResolveValue(sourcePage);
					if (cells[i].Buffer is not null)
					{ // a whole key: strip this page's prefix outright
						run.Add(stored[prefixLen..], value, cells[i].Flags);
					}
					else if (prefixLen >= sourcePrefix.Length)
					{ // the new prefix reaches into the stored suffix, so the remainder is one slice of it
						run.Add(stored[(prefixLen - sourcePrefix.Length)..], value, cells[i].Flags);
					}
					else
					{ // the new prefix is SHORTER, so what is stored gains back the tail of the old one: two spans, no copy
						run.Add(sourcePrefix[prefixLen..], stored, value, cells[i].Flags);
					}
				}
				run.Complete();
				return;
			}

			// internal cells stay contiguous, packed down from the end of the page
			int tail = image.Length;
			for (int i = start; i < end; i++)
			{
				var cell = cells[i].ResolveKey(sourcePage);
				tail -= cell.Length;
				cell.CopyTo(image[tail..]);
				FdbLiteTreePage.SetSlot(image, isInternal, i - start, (ushort) tail);
			}
			FdbLitePageHeader.SetCellCount(image, (ushort) count);
			FdbLitePageHeader.SetCellAreaOffset(image, count > 0 ? (ushort) tail : (ushort) 0);
		}

		#endregion

		#region Page I/O...

		private ReadOnlySpan<byte> ReadPage(uint pageId)
		{
			if (this.Dirty.TryGetValue(pageId, out var buffered))
			{ // this generation's own in-memory image, newer than anything the pager holds
				return buffered;
			}

			var page = this.Pager.ReadBlocks(pageId, this.Pager.Geometry.BlocksPerPage);
			if (!this.Shadow.Contains(pageId) && !FdbLitePageHeader.Verify(page, pageId))
			{ // verification is per FIRST TOUCH of a page by this generation, not per read: a shadowed page was
			  // written by this same writer, so re-hashing it would check our own bytes against our own checksum
				throw new InvalidDataException($"Corrupted tree page {pageId}");
			}
			return page;
		}

		/// <summary>Records a page image for this generation: in place when this generation owns the page, else copy-on-write to a fresh page (queueing the old one for delayed free).</summary>
		/// <remarks>The image is buffered, NOT written: it reaches the pager at <see cref="FlushDirtyPages"/>, sealed once, however many times this generation modified it.</remarks>
		private uint WritePage(uint oldPageId, ReadOnlySpan<byte> image)
		{
			uint id;
			if (oldPageId != 0 && this.Shadow.Contains(oldPageId))
			{
				id = oldPageId;
			}
			else
			{
				id = this.Allocator.AllocatePage();
				this.Shadow.Add(id);
				if (oldPageId != 0)
				{
					this.PageCopies++;
					this.Allocator.Free(oldPageId, (uint) this.Pager.Geometry.BlocksPerPage, this.Generation);
				}
			}

			if (!this.Dirty.TryGetValue(id, out var slot))
			{ // one buffer per dirty page, allocated once and reused for every later mutation of that page
				slot = new byte[this.Pager.Geometry.PageSize];
				this.Dirty.Add(id, slot);
			}
			image.CopyTo(slot);
			return id;
		}

		/// <summary>Seals and writes every page image this generation is holding, then releases them.</summary>
		/// <remarks>Called by the engine before the commit protocol's first flush barrier, and wherever the writer must let a raw pager read observe its work. Ordering of the two commit barriers is unaffected: this only decides WHEN the data blocks are handed over, never that they are handed over after the header.</remarks>
		public void FlushDirtyPages()
		{
			if (this.Dirty.Count == 0)
			{
				return;
			}

			// ascending page order turns the dirty set into as few forward runs as the allocation pattern allows
			var ids = new uint[this.Dirty.Count];
			this.Dirty.Keys.CopyTo(ids, 0);
			Array.Sort(ids);

			foreach (var id in ids)
			{
				var image = this.Dirty[id];
				FdbLitePageHeader.Seal(image, id);
				this.Pager.WriteBlocks(id, image);
				this.PagesWritten++;
			}
			this.Dirty.Clear();
		}

		#endregion

	}

}
