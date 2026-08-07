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

namespace FoundationDB.FdbLite
{
	using System.Runtime.InteropServices;

	/// <summary>Builds the writable copy-on-write generation of a tree: inserts and updates, page splits, shadow-set page reuse.</summary>
	/// <remarks>
	/// <para>Single-writer machinery (one instance per writable generation, used under the engine's commit lock).</para>
	/// <para>Small mutations edit an OWNED page image in place (splice, overwrite, relocate, remove - each books any dead bytes it leaves into the page's wasted-bytes counter); everything else REBUILDS the touched page, and a rebuilt page is compact by construction. Either way the image is sealed and generation-stamped on the way to the pager. A page first allocated by THIS generation is rebuilt in place (shadow set); any other page is copied to a fresh location and its old blocks are queued for delayed free.</para>
	/// <para>Splits are K-way (greedy, largest prefix that fits per page): with maximum-size cells a two-way split can have NO legal cut point (the unimplemented "three-way split" hole of the legacy prototype), so the general form is the correctness fix, and K stays 2 for every realistic cell size.</para>
	/// </remarks>
	public sealed partial class FdbLiteTreeWriter
	{

		/// <param name="pageBufferPool">Page images recycled ACROSS generations (and, via <see cref="FdbLitePageBufferPool.Shared"/>, across engines). Optional: without it every generation allocates its own, which measured as the engine's largest remaining <c>byte[]</c> source once the gather lists were pooled (a write workload allocates one page image per page it touches, so the cost is the whole dirty set per commit).</param>
		public FdbLiteTreeWriter(IFdbLitePager pager, FdbLiteBlockAllocator allocator, ulong generation, uint root, FdbLitePageBufferPool? pageBufferPool = null)
		{
			Contract.NotNull(pager);
			Contract.NotNull(allocator);
			this.Pager = pager;
			this.Allocator = allocator;
			this.Generation = generation;
			this.Root = root;
			this.PageBufferPool = pageBufferPool;
		}

		/// <summary>Free list of page-sized image buffers, or <c>null</c> when this writer allocates its own.</summary>
		private FdbLitePageBufferPool? PageBufferPool { get; }

		/// <summary>A page-image buffer for a newly dirtied page. UNINITIALIZED either way: every dirty image is written whole (<see cref="WritePage"/> copies a full page over it) before anything reads it, so neither a recycled buffer nor a fresh one needs clearing.</summary>
		private byte[] RentPageBuffer()
			=> this.PageBufferPool?.Rent() ?? GC.AllocateUninitializedArray<byte>(this.Pager.Geometry.PageSize);

		/// <summary>Hands a page image back for a later generation (or another engine) to reuse.</summary>
		/// <remarks>The pool is UNCAPPED on purpose (see <see cref="FdbLitePageBufferPool"/>): a buffer only enters it when it LEAVES a dirty set, so retained memory is bounded by the peak concurrent demand, and a cap below the dirty-set size just moves the allocations past the cap.</remarks>
		private void ReturnPageBuffer(byte[] buffer) => this.PageBufferPool?.Return(buffer);

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

		/// <summary>Ids of the pages this generation has dirtied so far, SORTED (the commit manifest must be deterministic for the twin-run suites). Must run before <see cref="FlushDirtyPages"/> clears the set.</summary>
		internal void CollectDirtyPageIds(List<uint> ids)
		{
			foreach (var id in this.Dirty.Keys)
			{
				ids.Add(id);
			}
			ids.Sort();
		}

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
			/// <remarks>Always off: a buffered image is unsealed mid-generation (its checksum lands at flush), so first-touch verification through this view would reject the writer's own work; clean blocks verify through the REAL pager's map.</remarks>
			public bool TrackFirstTouch { get => false; set { } }

			/// <inheritdoc />
			public bool MarkTouched(uint firstBlock) => false;

			/// <inheritdoc />
			public void PunchHole(uint firstBlock, uint count) => this.Inner.PunchHole(firstBlock, count);

			/// <inheritdoc />
			public void Prefetch(uint firstBlock, uint count) => this.Inner.Prefetch(firstBlock, count);

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
			/// <remarks>A buffered image ref points at the buffer CURRENTLY holding the page; a held ref across a structural change of that page dangles exactly like a held <see cref="ReadBlocks"/> span would, and the same cursor-invalidation rules forbid it.</remarks>
			public FdbLitePageRef ReadBlocksRef(uint firstBlock, int count)
			{
				if (count == this.Inner.Geometry.BlocksPerPage && this.Writer.Dirty.TryGetValue(firstBlock, out var image))
				{
					return new(image, 0, image.Length);
				}
				return this.Inner.ReadBlocksRef(firstBlock, count);
			}

			/// <inheritdoc />
			/// <remarks>Pass-through ON PURPOSE, asymmetric with <see cref="ReadBlocks"/>: tree pages never come through here (they are buffered in <see cref="FdbLiteTreeWriter.Dirty"/> by <see cref="WritePage"/> and flushed at commit), only extent data, which is written once and read back through the inner pager. Do not "fix" this by adding a dirty-buffer check on the write side.</remarks>
			public void WriteBlocks(uint firstBlock, ReadOnlySpan<byte> data) => this.Inner.WriteBlocks(firstBlock, data);

			/// <inheritdoc />
			public void ResetFirstTouch() => this.Inner.ResetFirstTouch();

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

		/// <summary>Optional sink for a full trace of what the writer did, one tagged record per event.</summary>
		/// <remarks>
		/// <para>Null by default and therefore free. When set, every structural event emits a TAB-separated
		/// record whose first field is a TAG, so the file can be located in with a plain grep rather than read:
		/// <c>OP</c> (one per public mutation), <c>LEAF+</c> / <c>NODE+</c> (a page came into existence, with
		/// the method that created it), <c>SPLIT</c>, <c>SPLICE</c>, <c>OVERWRITE</c>, <c>REMOVE</c>,
		/// <c>COW</c>, <c>FREE</c>.</para>
		/// <para>The point of the caller tag is that counters can say a thing happened N times but never WHERE
		/// from, which is exactly the wall this session hit: 484 two-way splits cannot account for 929 leaves,
		/// and no counter can say what made the other 444.</para>
		/// </remarks>
		public static Action<string>? OpLog { get; set; }

		/// <summary>True when tracing is on, so callers can skip building records nobody will read.</summary>
		public static bool IsLogging => OpLog is not null;

		/// <summary>Optional sink for leaf-split SIZING decisions, for diagnosing fill factor.</summary>
		/// <remarks>
		/// <para>Null by default and therefore free. It exists because the split fan-out is computed from an
		/// ESTIMATE of what the run will occupy, and an estimate that runs high splits a page into more parts
		/// than it needs, leaving every part correspondingly emptier. Nothing else in the engine can show the
		/// difference between "this page really needed three parts" and "the sizing thought it did".</para>
		/// <para>Arguments: cell count, the estimated run footprint, the capacity it was tested against, the
		/// resulting part count, the SOURCE page's prefix length, the run's own longest common prefix, and the
		/// source page's actual live bytes. The last three are the ones that decide whether prefix re-expansion
		/// inflated the estimate: a run LCP shorter than the source prefix lengthens every stored key at once.</para>
		/// </remarks>
		public static Action<int, long, int, int, int, int, long>? SplitDiagnostics { get; set; }

		/// <summary>Sibling pages CREATED by those splits, which is not the same number.</summary>
		/// <remarks>A split is K-way, so one event can produce more than one sibling. The ratio of this to <see cref="PageSplits"/> is the average fan-out, and it is the difference between "pages are half full because that is what a 2-way split does" and "pages are a third full because the split made three of them".</remarks>
		public int SplitSiblingsCreated { get; private set; }

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

		/// <summary>Leaf splits performed by the insert path (the append fast path is counted by <see cref="PagesAppended"/>, not here; internal splits by <see cref="PageSplits"/> only)</summary>
		/// <remarks>The denominator of the spill-on-split opportunity fraction: these counters exist to MEASURE whether a spill-into-sibling arm could pay, before any such arm is built. They count; they never change what the writer does.</remarks>
		public int LeafSplits { get; private set; }

		/// <summary>Of <see cref="LeafSplits"/>, those where an adjacent same-parent sibling was already in this generation's dirty set</summary>
		/// <remarks>A spill into a page that will be written at flush regardless adds no page write and no cold copy-on-write; a spill anywhere else pays for a page the mutation never touched. The dirty-sibling gate is therefore the precondition of a foreground spill, and this is how often it is even open.</remarks>
		public int LeafSplitsWithDirtySibling { get; private set; }

		/// <summary>Of <see cref="LeafSplitsWithDirtySibling"/>, those whose overflow that dirty sibling could actually have taken, so the split would not have happened at all</summary>
		/// <remarks>An UPPER BOUND on purpose: the minimal boundary run is moved, both pages are sized compacted, and the recipient may fill to 100% (a real spill would keep a margin). If even this bound measures negligible on a workload, no spill arm can pay there.</remarks>
		public int LeafSplitsAbsorbableByDirtySibling { get; private set; }

		/// <summary>Start a fresh page when a key appends past the last key of the RIGHTMOST leaf, instead of splitting that leaf in half.</summary>
		/// <remarks>
		/// <para>A balanced split leaves both halves near half full; on an append-shaped load nothing ever inserts into the left half again, so the whole file settles at ~50% occupancy. Starting a fresh page instead packs the finished pages to capacity, roughly halving pages, file size and pages written for that load.</para>
		/// <para>The trade, taken knowingly: a right-edge page packed to 100% splits on the first later insert into it, converting one cheap split into two for append-then-update data. Exposed as a knob so a benchmark can measure both ways.</para>
		/// </remarks>
		public bool AvoidSequentialAppendSplits { get; set; } = true;

		/// <summary>Extents written by this generation, with their block counts (freed immediately on replace/delete instead of waiting out the horizon; the counts are what lets an abandoned generation free them without re-reading a descriptor)</summary>
		private Dictionary<uint, uint> ShadowExtents { get; } = [ ];

		/// <summary>Net key-count change of this generation (inserts of new keys minus removals), for the snapshot header's exact count</summary>
		public long KeyCountDelta { get; private set; }

		private const int MaxDepth = 20;

		/// <summary>Leaf the last descent landed on, and the key range that descent proved that leaf covers (0 = no position).</summary>
		/// <remarks>
		/// <para>The bounds are the separators on either side of the descent path, so <c>[Lower, Upper)</c> is exactly the range routed to this leaf; a key inside it needs no descent at all. <c>null</c> is unbounded, and an unbounded <see cref="CursorUpper"/> also identifies the RIGHTMOST leaf, which is what <see cref="AvoidSequentialAppendSplits"/> keys off.</para>
		/// <para>Valid only while the leaf's identity and range hold: any structural change (a split, a delete's rebuild) drops the position rather than trying to repair it.</para>
		/// </remarks>
		private uint CursorLeaf { get; set; }

		/// <summary>Backing buffers for the two bounds, sized to the separators actually seen and overwritten by every descent.</summary>
		/// <remarks>
		/// <para>The bounds used to be a fresh <c>ToArray()</c> per bounded level per descent, which measured as the second-largest <c>byte[]</c> source in the engine. They are copies rather than spans because the mutation that follows the descent can rewrite the very page they point into.</para>
		/// <para>Fields rather than auto-properties so <see cref="GrowScratch"/> can grow them by ref. Sized on demand rather than <c>MaxKeyLength</c>: a writer lives one commit, and two eager 10 KB arrays were 20 of the 22.5 KB/op allocation of a one-commit-per-op range delete.</para>
		/// </remarks>
		private byte[]? CursorLowerBuffer;

		private byte[]? CursorUpperBuffer;

		/// <summary>Buffered image of the dirty page <see cref="CursorBufferId"/> (0 = nothing cached): the splice path's per-key Dictionary lookup, paid once per page instead.</summary>
		/// <remarks>Sound because a Dirty entry's buffer is assigned ONCE per id per generation (<c>WritePage</c> reuses it for every later mutation); the only way the pair goes stale is the id being FREED and reallocated, and both free paths clear it by id.</remarks>
		private byte[]? CursorBuffer;

		private uint CursorBufferId;

		/// <summary>The dirty buffer of <paramref name="leafId"/>, through the one-entry cache; null when this generation does not own the page.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private byte[]? DirtyBufferOf(uint leafId)
		{
			if (this.CursorBufferId == leafId)
			{
				return this.CursorBuffer;
			}
			if (this.Dirty.TryGetValue(leafId, out var buffered))
			{
				this.CursorBufferId = leafId;
				this.CursorBuffer = buffered;
				return buffered;
			}
			return null;
		}

		/// <summary>Undoes every side effect this UNCOMMITTED generation left in the shared engine state: buffered pages return to the pool, the generation's own allocations (shadow pages and extents) go straight back to free space, and the frees it recorded against the durable tree are erased (that tree still references them).</summary>
		/// <remarks><see cref="FdbLiteEngine.Abandon"/> is the only caller. Sound even after a failed flush: flushed bytes without an advanced header are dead, and freeing the shadow set immediately is correct because no retained root references it. Extent bytes already written to the pager are likewise dead.</remarks>
		internal void RollbackAllocations()
		{
			foreach (var buffered in this.Dirty.Values)
			{
				ReturnPageBuffer(buffered);
			}
			this.Dirty.Clear();
			this.CursorBufferId = 0;
			this.CursorBuffer = null;
			this.CursorLeaf = 0;
			this.AppendLeaf = 0;

			uint blocksPerPage = (uint) this.Pager.Geometry.BlocksPerPage;
			foreach (var pageId in this.Shadow)
			{
				this.Allocator.FreeSpace.FreeImmediately(pageId, blocksPerPage);
			}
			this.Shadow.Clear();
			foreach (var (start, blockCount) in this.ShadowExtents)
			{
				this.Allocator.FreeSpace.FreeImmediately(start, blockCount);
			}
			this.ShadowExtents.Clear();

			// LAST, after the shadow frees above (which are this generation's own space, not the durable tree's):
			// erase the delayed frees this generation recorded, or a later commit would hand out pages the
			// durable tree still references
			this.Allocator.FreeSpace.RemovePendingFrom(this.Generation);
		}

		/// <summary>Returns <paramref name="buffer"/> grown to hold at least <paramref name="needed"/> bytes (existing content need not survive: every caller overwrites from offset 0).</summary>
		private static byte[] GrowScratch([NotNull] ref byte[]? buffer, int needed)
		{
			if (buffer is null || buffer.Length < needed)
			{
				buffer = new byte[Math.Max((int) BitOperations.RoundUpToPowerOf2((uint) needed), 64)];
			}
			return buffer;
		}

		/// <summary>Length of the bound held in the matching buffer, or -1 when that side is UNBOUNDED.</summary>
		private int CursorLowerLength { get; set; } = -1;

		private int CursorUpperLength { get; set; } = -1;

		private ReadOnlySpan<byte> CursorLower => this.CursorLowerLength < 0 ? default : this.CursorLowerBuffer.AsSpan(0, this.CursorLowerLength);

		private ReadOnlySpan<byte> CursorUpper => this.CursorUpperLength < 0 ? default : this.CursorUpperBuffer.AsSpan(0, this.CursorUpperLength);

		/// <summary>The APPEND-EDGE slot: the rightmost leaf, kept beside the roaming slot above so interior updates cannot evict the append cursor.</summary>
		/// <remarks>
		/// <para>The ledger shape (append a record, update a few recent ones) alternated between the edge leaf and window leaves, and with ONE slot each op stole the cursor from the next: the round-6 ledger leg measured 388k descents for 400k ops. The rightmost leaf's upper bound is unbounded by definition, so this slot carries no upper buffer.</para>
		/// <para>Recorded by every descent that lands rightmost; follows its leaf's rebuild by id; cleared wherever the roaming slot is cleared.</para>
		/// </remarks>
		private uint AppendLeaf;

		private byte[]? AppendLowerBuffer;

		private int AppendLowerLength = -1;

		/// <summary>The covered leaf that takes <paramref name="key"/> without a descent: the append edge first (append-heavy loads), then the roaming slot; 0 = neither covers it.</summary>
		private uint CoveredLeaf(ReadOnlySpan<byte> key)
		{
			if (this.AppendLeaf != 0
			 && (this.AppendLowerLength < 0 || key.SequenceCompareTo(this.AppendLowerBuffer.AsSpan(0, this.AppendLowerLength)) >= 0))
			{
				return this.AppendLeaf;
			}
			if (this.CursorLeaf != 0
			 && (this.CursorLowerLength < 0 || key.SequenceCompareTo(this.CursorLower) >= 0)
			 && (this.CursorUpperLength < 0 || key.SequenceCompareTo(this.CursorUpper) < 0))
			{
				return this.CursorLeaf;
			}
			return 0;
		}

		/// <summary>Outcome of rebuilding one page: the page (possibly relocated), plus right siblings when it split</summary>
		private readonly record struct RebuildResult(uint FirstId, List<(Slice Separator, uint PageId)>? Siblings)
		{
			public bool Split => this.Siblings != null;
		}

		/// <summary>Reference to one gathered cell, held as its key part and its value part because a leaf cell is no longer contiguous: it lives in two regions of the page. Either part may be a slice of the source page image or of a scratch buffer (a span-of-spans is not expressible, so gathered cell lists carry these instead).</summary>
		/// <remarks>An internal cell has no value part: its whole cell is the key part, which keeps one gather-and-emit path serving both page kinds.</remarks>
		internal readonly struct CellRef
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
			// an out-of-line value is ONE contiguous block run, and a run cannot straddle a mapping region:
			// that is the value ceiling, surfaced here with its name instead of as an internal allocator
			// contract failure several frames deep
			Contract.Requires(value.Length <= (long) this.Pager.RegionSizeInBlocks << this.Pager.Geometry.BlockSizeLog2, "value exceeds the store's region size (the maximum length of one contiguous extent)");

			if (OpLog is { } opLog) { opLog($"OP\tinsert\tklen={key.Length}\tvlen={value.Length}\tkey={Convert.ToHexString(key[..Math.Min(12, key.Length)])}\troot={this.Root}"); }

			// THE HOT PATH, and it must not touch a buffer. An inline value is stored verbatim, so the bytes the
			// page wants are the caller's own bytes: splicing straight from these spans lets the page copy key and
			// value ONCE, into their final home, which is what the legacy prototype does. Going through the
			// scratch below instead cost a rent/return pair plus a full extra copy of every key and value on
			// EVERY insert - and the splice takes ~99.9% of a sorted load, so that was almost the whole cost.
			if (this.Root != 0
			 && value.Length <= this.Pager.Geometry.MaxInlineValueLength
			 && CoveredLeaf(key) is var covered && covered != 0
			 && TrySpliceInto(covered, key, value, flags: 0))
			{
				return;
			}

			// Everything else: a first key, an out-of-line value (whose stored bytes are a SYNTHESIZED descriptor
			// rather than the caller's), or a page that could not take the cell where it lies. Those reach the
			// rebuild path, which gathers this cell alongside cells that live in page memory, so it needs the cell
			// in a buffer that outlives the call.
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
			// the descriptor's block count is a u16: with a mapping region above 65,535 blocks the region-size
			// ceiling checked at Insert stops binding first, and an unchecked cast would truncate the count
			// SILENTLY on disk (the store corrupts here and faults only at the read, far from the cause)
			Contract.Requires(blockCount <= ushort.MaxValue, "value exceeds the maximum extent length (65,535 blocks)");
			uint start = this.Allocator.AllocateExtent((uint) blockCount);
			this.ShadowExtents.Add(start, (uint) blockCount);

			var padded = ArrayPool<byte>.Shared.Rent(blockCount * blockSize);
			try
			{
				var span = padded.AsSpan(0, blockCount * blockSize);
				value.CopyTo(span);
				span[value.Length..].Clear();
				this.Pager.WriteBlocks(start, span);
				// same contract as the dirty-page flush: bytes this process computed need no first-read verification
				this.Pager.MarkTouched(start);
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(padded);
			}

			ulong checksum = System.IO.Hashing.XxHash3.HashToUInt64(value, unchecked((long) start));
			FdbLiteTreePage.BuildExtentDescriptor(scratch.AsSpan(key.Length), start, checked((ushort) blockCount), (uint) value.Length, checksum);
			return CellRef.OfLeafBuffer(scratch, key.Length, FdbLiteTreePage.ExtentDescriptorSize, FdbLiteTreePage.FlagValueIsExtent);
		}

		private void InsertCell(ReadOnlySpan<byte> key, CellRef newCell)
		{
			if (this.Root == 0)
			{ // first key: a fresh single-cell leaf becomes the root, and covers the whole keyspace
				this.Root = WriteFreshSingleCellPage(in newCell);
				this.KeyCountDelta++;
				this.CursorLeaf = this.Root;
				this.CursorLowerLength = -1;
				this.CursorUpperLength = -1;
				this.AppendLeaf = this.Root;
				this.AppendLowerLength = -1;
				return;
			}

			if (CoveredLeaf(key) is var covered && covered != 0 && TrySpliceInto(covered, key, newCell))
			{ // the last descent already proved this leaf takes this key, and its free area had room: no descent,
			  // and no ancestor to patch, since the page neither moved nor split
				return;
			}

			// descend to the leaf, remembering the path
			Span<uint> pathPages = stackalloc uint[MaxDepth];
			Span<int> pathChildren = stackalloc int[MaxDepth];
			uint pageId = DescendToLeaf(key, pathPages, pathChildren, out int depth);

			int splitsBefore = this.PageSplits;
			var outcome = RebuildLeafWithInsert(pageId, key, newCell, rightmost: this.CursorUpperLength < 0);
			if (this.PageSplits != splitsBefore)
			{ // the ONE site where a leaf splits (delete rebuilds only shrink); the ascent has not run yet, so the
			  // parent still lists the pre-split siblings, which is exactly what the opportunity probe needs
				this.LeafSplits++;
				if (depth > 0)
				{
					ProbeLeafSplitSpillOpportunity(pathPages[depth - 1], pathChildren[depth - 1], in outcome);
				}
			}
			AscendPatch(pathPages, pathChildren, depth - 1, pageId, outcome);

			// the descent set the cursor on the leaf it landed on: a rebuild may have relocated that leaf (same
			// keys, same range), a split invalidates the range outright
			uint reseated = outcome.Split ? 0 : outcome.FirstId;
			if (this.AppendLeaf == pageId) { this.AppendLeaf = reseated; }
			this.CursorLeaf = reseated;
		}

		/// <summary>Walks from the root to the leaf covering <paramref name="key"/>, recording internal pages and child indexes, and positioning the writer's cursor on the leaf it reaches.</summary>
		internal uint DescendToLeaf(ReadOnlySpan<byte> key, Span<uint> pathPages, Span<int> pathChildren, out int depth)
		{
			depth = 0;
			this.LeafDescents++;
			uint pageId = this.Root;
			int lowerLength = -1, upperLength = -1;
			while (true)
			{
				var page = ReadPage(pageId);
				if (FdbLitePageHeader.GetPageType(page) == FdbLitePageType.Leaf)
				{
					this.CursorLeaf = pageId;
					this.CursorLowerLength = lowerLength;
					this.CursorUpperLength = upperLength;
					if (upperLength < 0)
					{ // the rightmost leaf: record the append-edge slot too, so interior descents cannot evict it
						this.AppendLeaf = pageId;
						this.AppendLowerLength = lowerLength;
						if (lowerLength > 0)
						{
							this.CursorLowerBuffer.AsSpan(0, lowerLength).CopyTo(GrowScratch(ref this.AppendLowerBuffer, lowerLength));
						}
					}
					return pageId;
				}
				Contract.Debug.Assert(depth < MaxDepth);
				int childIndex = FdbLiteTreePage.FindChildIndex(page, key);

				// the separators on either side of the chosen child bound its whole subtree, and each level can only
				// narrow that range: what reaches the leaf is exactly the range routed to it (copied, since the
				// mutation about to happen can rewrite the page these spans point into)
				if (childIndex > 0)
				{
					var sep = FdbLiteTreePage.GetSeparator(page, childIndex - 1);
					sep.CopyTo(GrowScratch(ref this.CursorLowerBuffer, sep.Length));
					lowerLength = sep.Length;
				}
				if (childIndex < FdbLitePageHeader.GetCellCount(page))
				{
					var sep = FdbLiteTreePage.GetSeparator(page, childIndex);
					sep.CopyTo(GrowScratch(ref this.CursorUpperBuffer, sep.Length));
					upperLength = sep.Length;
				}

				pathPages[depth] = pageId;
				pathChildren[depth] = childIndex;
				depth++;
				pageId = FdbLiteTreePage.GetChild(page, childIndex);
			}
		}

		/// <summary>Ascends from <paramref name="fromLevel"/>, patching each parent's child pointer and inserting separators for split siblings; grows the root as needed.</summary>
		private void AscendPatch(ReadOnlySpan<uint> pathPages, ReadOnlySpan<int> pathChildren, int fromLevel, uint originalChildId, RebuildResult outcome)
			=> AscendPatch(pathPages, pathChildren, fromLevel, originalChildId, outcome.FirstId, AsSiblingSpan(outcome));

		/// <summary>The siblings of a rebuild outcome as a span, without copying: an empty span IS "did not split", which is the only distinction the ascent makes.</summary>
		private static ReadOnlySpan<(Slice Separator, uint PageId)> AsSiblingSpan(RebuildResult outcome)
			=> outcome.Siblings is { } siblings ? CollectionsMarshal.AsSpan(siblings) : default;

		/// <summary>Ascends from <paramref name="fromLevel"/> over a caller-owned sibling span, for a producer that has no <see cref="RebuildResult"/> to hand over.</summary>
		/// <param name="raiseFollowingSeparatorTo">Lower bound the separator that FOLLOWS the descended child must reach, at every level where it sits below it, or empty for the ordinary ascent. Only valid when the interval it moves left was CLEARED and proven to hold no survivor. See the remarks.</param>
		/// <remarks>
		/// <para>A GRAFT emits its pages straight into a rented array rather than a <see cref="List{T}"/>, which is the only reason this form exists; the split path reaches it through the overload above and behaves identically (an empty span and a null <see cref="RebuildResult.Siblings"/> are the same thing here).</para>
		/// <para><paramref name="raiseFollowingSeparatorTo"/> repairs what a preceding <see cref="RemoveRange"/> leaves behind: dropping a leaf's LEADING cells raises the page's first key but not the separator that routes to it, which stays a valid (merely loose) lower bound for every other operation. It stops being one as soon as pages are SPLICED into the space that clear vacated: the graft's last emitted separator can then sort above that loose one, and the parent's separators come out of order. Raising it to the imported range's exclusive upper bound restores the order, and routes nothing wrongly: the interval it moves left is exactly what the clear emptied.</para>
		/// <para>PRECONDITION, and it is the whole reason the repair is safe: <paramref name="raiseFollowingSeparatorTo"/> may only be passed for a range the caller CLEARED and proved held no survivor. Raising a separator makes every key still living below the new bound unreachable - the cursor routes past them, with no exception and nothing the audit can see. A driver grafting into a gap between survivors WITHOUT clearing it (the design's situation D) must therefore pass nothing here, however similar its call looks.</para>
		/// </remarks>
		private void AscendPatch(ReadOnlySpan<uint> pathPages, ReadOnlySpan<int> pathChildren, int fromLevel, uint originalChildId, uint firstId, ReadOnlySpan<(Slice Separator, uint PageId)> siblings, ReadOnlySpan<byte> raiseFollowingSeparatorTo = default)
		{
			for (int level = fromLevel; level >= 0; level--)
			{
				if (siblings.Length == 0 && firstId == originalChildId && raiseFollowingSeparatorTo.Length == 0 && this.Dirty.ContainsKey(pathPages[level]))
				{ // the child was rebuilt in place and its parent is in the dirty set: every ancestor already
				  // points at it and is itself dirty, so the aggregate stamp pass will re-sum the whole chain.
				  // The dirty-parent condition is load-bearing: after a mid-generation flush a shadow page
				  // rebuilds in place under CLEAN ancestors, and returning here would leave their stored
				  // aggregates stale forever - the chain must be re-dirtied all the way up instead.
					return;
				}
				originalChildId = pathPages[level];
				// checked per level, not once: whether the loose separator sits at this level or at an ancestor
				// depends on where the descended child is the LAST one, and both can need raising
				var raise = raiseFollowingSeparatorTo.Length > 0 && FollowingSeparatorSitsBelow(pathPages[level], pathChildren[level], raiseFollowingSeparatorTo)
					? raiseFollowingSeparatorTo
					: default;
				if (raise.Length > 0 && siblings.Length > 0)
				{ // Kept ALWAYS ON: the raised separator lands immediately after the last one being inserted, so a
				  // bound at or below it writes the parent's separators out of order - silently, and only the caller
				  // knows whether its bound really is the top of a cleared range. This is the one comparison that
				  // notices, it costs one per graft, and being wrong corrupts the tree.
					Contract.Requires(siblings[^1].Separator.Span.SequenceCompareTo(raise) < 0, "the raise bound must sort strictly above the last separator the ascent inserts, or the parent comes out unsorted and keys below the bound become unreachable");
				}
				var rebuilt = RebuildInternal(pathPages[level], pathChildren[level], firstId, siblings, raise);
				firstId = rebuilt.FirstId;
				siblings = AsSiblingSpan(rebuilt);
			}

			// the root may have been relocated, and may have split (possibly more than once)
			while (siblings.Length > 0)
			{
				var grown = BuildRootLevel(firstId, siblings);
				firstId = grown.FirstId;
				siblings = AsSiblingSpan(grown);
			}
			this.Root = firstId;
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

			if (CoveredLeaf(key) is var covered && covered != 0 && TryRemoveInPlace(covered, key, out bool removedInPlace))
			{ // the last descent already proved this leaf covers this key, and the page is ours to edit: no
			  // descent, no rebuild, and no ancestor to patch since the page neither moved nor changed range
				return removedInPlace;
			}

			Span<uint> pathPages = stackalloc uint[MaxDepth];
			Span<int> pathChildren = stackalloc int[MaxDepth];
			uint leafId = DescendToLeaf(key, pathPages, pathChildren, out int depth);

			// The descent proves this leaf is where the key lives, which is the same proof the cursor carries.
			// So when this generation ALSO already owns the page, closing the directory over the cell is the
			// whole operation: no rebuild, no gather list, no ancestor patch. Only the cursor-covered path used
			// to reach here, so a batch that came back to an owned page through a fresh descent - any delete
			// order that is not one run per leaf - paid a full O(cells) rebuild the page could have absorbed.
			if (TryRemoveInPlace(leafId, key, out removedInPlace))
			{
				return removedInPlace;
			}

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
		/// <remarks>Interior subtrees whose separator bounds sit wholly inside the range are DROPPED, not walked:
		/// their internal pages are read to enumerate children, their leaves are read only to release extents,
		/// and nothing in them is rebuilt or rewritten. Only the (at most two) boundary leaves pay a rebuild.
		/// Each step re-descends from the root because a drop patches ancestors and stales any recorded path;
		/// the step count is bounded by the boundary structure, not by the number of doomed leaves.</remarks>
		public int RemoveRange(ReadOnlySpan<byte> begin, ReadOnlySpan<byte> end)
		{
			if (end.SequenceCompareTo(begin) <= 0)
			{
				return 0;
			}
			long total = 0;
			while (this.Root != 0)
			{
				long removed = RemoveRangeStep(begin, end);
				if (removed == 0)
				{
					break;
				}
				total += removed;
			}
			return checked((int) total);
		}

		/// <summary>One unit of range-clearing work: the begin-boundary leaf's in-range cells, or one doomed
		/// sibling run harvested off the begin path, or the single trailing straddler leaf. Returns 0 when the
		/// range holds nothing more.</summary>
		private long RemoveRangeStep(ReadOnlySpan<byte> begin, ReadOnlySpan<byte> end)
		{
			Span<uint> pathPages = stackalloc uint[MaxDepth];
			Span<int> pathChildren = stackalloc int[MaxDepth];

			uint leafId = DescendToLeaf(begin, pathPages, pathChildren, out int depth);
			var page = ReadPage(leafId);
			int cellCount = FdbLitePageHeader.GetCellCount(page);
			int first = FdbLiteTreePage.FindLeafSlot(page, begin, out _);
			int last = first;
			// compares against the WHOLE key: the stored key is only a suffix once the page strips a prefix
			while (last < cellCount && FdbLiteTreePage.CompareLeafKey(page, last, end) < 0)
			{
				last++;
			}
			if (last > first)
			{
				DropLeafSlots(leafId, page, first, last, pathPages, pathChildren, depth);
				CollapseRoot();
				return last - first;
			}

			// the begin leaf holds nothing in range: harvest ONE run of fully-doomed siblings off the path.
			// A right sibling's lower bound is at/above the separator that routed the descent, so it is >= begin
			// by construction; it is doomed when its own upper separator is <= end. Deepest level first, so
			// leaf-level runs (the common case) go without touching the grandparents.
			for (int level = depth - 1; level >= 0; level--)
			{
				var parent = ReadPage(pathPages[level]);
				int separators = FdbLitePageHeader.GetCellCount(parent);
				int from = pathChildren[level] + 1;
				int to = from;
				while (to < separators && FdbLiteTreePage.GetSeparator(parent, to).SequenceCompareTo(end) <= 0)
				{ // child `to` is bounded above by separator `to`: wholly inside the range
					to++;
				}
				if (to > from)
				{
					return DropChildRun(pathPages, pathChildren, level, parent, from, to);
				}
			}

			// no boundary work and no doomed run: the only candidate left is the next leaf to the right, which
			// can STRADDLE end (all its keys below end, its separator above). Its lower bound is the separator
			// that precedes it, which is a key we can descend by.
			for (int level = depth - 1; level >= 0; level--)
			{
				var parent = ReadPage(pathPages[level]);
				int separators = FdbLitePageHeader.GetCellCount(parent);
				int next = pathChildren[level];
				if (next >= separators)
				{
					continue;
				}
				var separator = FdbLiteTreePage.GetSeparator(parent, next);
				if (separator.SequenceCompareTo(end) >= 0)
				{
					return 0; // everything to the right starts at/after end
				}
				var scratch = ArrayPool<byte>.Shared.Rent(separator.Length);
				try
				{ // the descent reads pages and the separator span points into one: descend by a copy
					separator.CopyTo(scratch);
					uint straddler = DescendToLeaf(scratch.AsSpan(0, separator.Length), pathPages, pathChildren, out depth);
					var image = ReadPage(straddler);
					int cells = FdbLitePageHeader.GetCellCount(image);
					int drop = 0;
					while (drop < cells && FdbLiteTreePage.CompareLeafKey(image, drop, end) < 0)
					{
						drop++;
					}
					if (drop == 0)
					{
						return 0;
					}
					DropLeafSlots(straddler, image, 0, drop, pathPages, pathChildren, depth);
					CollapseRoot();
					return drop;
				}
				finally
				{
					ArrayPool<byte>.Shared.Return(scratch);
				}
			}
			return 0;
		}

		/// <summary>Drops children [<paramref name="from"/>, <paramref name="to"/>) of the internal page at path
		/// <paramref name="level"/>: frees every subtree wholesale, rebuilds the parent once, patches the
		/// ancestors once. Returns the number of keys removed.</summary>
		private long DropChildRun(ReadOnlySpan<uint> pathPages, ReadOnlySpan<int> pathChildren, int level, ReadOnlySpan<byte> parent, int from, int to)
		{
			uint parentId = pathPages[level];
			var doomed = ArrayPool<uint>.Shared.Rent(to - from);
			try
			{
				for (int i = from; i < to; i++)
				{ // ids copied out first: the subtree drops below read other pages while we still hold `parent`
					doomed[i - from] = FdbLiteTreePage.GetChild(parent, i);
				}
				long removed = 0;
				var freed = default(FreedRunBatcher);
				// the doomed children are about to be read (leaves for their extents, subtree roots to recurse):
				// announce the whole run first so the faults overlap at the drive instead of paying QD1 each
				PrefetchPages(doomed.AsSpan(0, to - from));
				for (int i = 0; i < to - from; i++)
				{
					removed += DropSubtree(doomed[i], ref freed);
				}
				freed.Flush(this);
				this.KeyCountDelta -= removed;
				this.CursorLeaf = 0; // the drop and the parent rebuild below invalidate any covered-leaf claim
				this.AppendLeaf = 0;

				var outcome = RebuildInternalRemoveChildRun(parentId, parent, from, to);
				AscendPatch(pathPages, pathChildren, level - 1, parentId, outcome);
				CollapseRoot();
				return removed;
			}
			finally
			{
				ArrayPool<uint>.Shared.Return(doomed);
			}
		}

		/// <summary>Frees a whole subtree without rebuilding anything in it: internal pages are read to
		/// enumerate children, leaves are read only to release their extents, and no page is rewritten.
		/// Returns the number of keys the subtree held.</summary>
		/// <remarks>Two accelerations on the leaf tier. A CLEAN leaf-parent whose subtree carries ZERO extent
		/// blocks (the format-3 aggregate) frees its leaves by id without reading one - nothing inside them
		/// needs releasing beyond the pages themselves, and a parent knows its children are leaves when its
		/// leaf count equals its child count. When leaves must be read (extents to release), they are
		/// prefetched as a batch first, so the faults overlap at the drive instead of paying QD1 latency each.
		/// Only CLEAN pages get either treatment: a dirty internal page's aggregate block is stale until the
		/// flush-time stamp pass, so a dirty subtree root falls back to the plain recursion.</remarks>
		private long DropSubtree(uint pageId, ref FreedRunBatcher freed)
		{
			bool dirty = this.Dirty.ContainsKey(pageId);
			var page = ReadPage(pageId);
			long removed;
			if (FdbLitePageHeader.GetPageType(page) == FdbLitePageType.Leaf)
			{
				int cells = FdbLitePageHeader.GetCellCount(page);
				for (int i = 0; i < cells; i++)
				{
					FreeExtentOfCell(page, i);
				}
				removed = cells;
			}
			else
			{
				int children = FdbLiteTreePage.GetChildCount(page);
				bool leafParent = FdbLitePageHeader.GetLeafCount(page) == (uint) children;
				var ids = ArrayPool<uint>.Shared.Rent(children);
				try
				{
					for (int i = 0; i < children; i++)
					{
						ids[i] = FdbLiteTreePage.GetChild(page, i);
					}

					if (!dirty && leafParent && FdbLitePageHeader.GetExtentBlocks(page) == 0)
					{ // nothing inside the leaves needs releasing: free them by id, unread
						removed = (long) FdbLitePageHeader.GetEntryCount(page);
						for (int i = 0; i < children; i++)
						{
							FreeSubtreePage(ids[i], ref freed);
						}
					}
					else
					{
						if (!dirty && leafParent)
						{ // the leaves must be read (extents to release): overlap the faults
							PrefetchPages(ids.AsSpan(0, children));
						}
						removed = 0;
						for (int i = 0; i < children; i++)
						{
							removed += DropSubtree(ids[i], ref freed);
						}
					}
				}
				finally
				{
					ArrayPool<uint>.Shared.Return(ids);
				}
			}

			FreeSubtreePage(pageId, ref freed);
			return removed;
		}

		/// <summary>Frees one subtree page by id (never reads it), through the same ownership routing as <see cref="FreePage"/>.</summary>
		private void FreeSubtreePage(uint pageId, ref FreedRunBatcher freed)
		{
			this.UnderflowCandidates.Remove(pageId);
			if (this.Dirty.Remove(pageId, out var released))
			{
				if (this.CursorBufferId == pageId) { this.CursorBufferId = 0; this.CursorBuffer = null; }
				ReturnPageBuffer(released);
			}
			if (this.Shadow.Remove(pageId))
			{
				this.Allocator.FreeSpace.FreeImmediately(pageId, (uint) this.Pager.Geometry.BlocksPerPage);
			}
			else
			{
				freed.Add(pageId, (uint) this.Pager.Geometry.BlocksPerPage, this);
			}
		}

		/// <summary>Issues prefetch advice for a set of pages, coalescing contiguous ids into single runs.</summary>
		private void PrefetchPages(ReadOnlySpan<uint> pageIds)
		{
			uint blocksPerPage = (uint) this.Pager.Geometry.BlocksPerPage;
			uint start = 0, count = 0;
			foreach (uint id in pageIds)
			{
				if (count > 0 && id == start + count)
				{
					count += blocksPerPage;
					continue;
				}
				if (count > 0)
				{
					this.Pager.Prefetch(start, count);
				}
				start = id;
				count = blocksPerPage;
			}
			if (count > 0)
			{
				this.Pager.Prefetch(start, count);
			}
		}

		/// <summary>Coalesces adjacent delayed frees into single ranges: a subtree drop releases pages in id
		/// order, and per-page entries would flood the pending queue and the per-commit free-list chain - and
		/// starve the hole punch, which only fires on ranges worth punching.</summary>
		private struct FreedRunBatcher
		{
			private uint Start;
			private uint Count;

			public void Add(uint firstBlock, uint blocks, FdbLiteTreeWriter writer)
			{
				if (this.Count > 0 && firstBlock == this.Start + this.Count)
				{
					this.Count += blocks;
					return;
				}
				if (this.Count > 0 && firstBlock + blocks == this.Start)
				{
					this.Start = firstBlock;
					this.Count += blocks;
					return;
				}
				Flush(writer);
				this.Start = firstBlock;
				this.Count = blocks;
			}

			public void Flush(FdbLiteTreeWriter writer)
			{
				if (this.Count > 0)
				{
					writer.Allocator.Free(this.Start, this.Count, writer.Generation);
					this.Count = 0;
				}
			}
		}

		/// <summary>Rebuilds an internal page without the children in [<paramref name="from"/>, <paramref name="to"/>)
		/// (never splits: it only shrinks). The run must not include child 0, which anchors the descent.</summary>
		private RebuildResult RebuildInternalRemoveChildRun(uint pageId, ReadOnlySpan<byte> page, int from, int to)
		{
			Contract.Debug.Requires(from >= 1 && to > from);
			int cellCount = FdbLitePageHeader.GetCellCount(page);
			Contract.Debug.Requires(to <= cellCount + 1);

			// child k rides cell k-1: dropping children [from, to) drops cells [from-1, to-1)
			return RemoveInternalRangeStreamed(pageId, page, cellCount, from - 1, to - 1, FdbLiteTreePage.GetLeftmostChild(page), caller: nameof(RebuildInternalRemoveChildRun));
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
				this.AppendLeaf = 0;
				FreePage(leafId);
				if (depth == 0)
				{
					this.Root = 0;
					return;
				}
				RemoveChildFromAncestors(pathPages, pathChildren, depth);
				return;
			}

			// a delete the page SURVIVES is an episode (whole-page death, the branch above, never is); the
			// dedup reads the pre-rebuild stamp, as everywhere the mutation rebuilds its page
			bool firstMutationThisGeneration = FdbLitePageHeader.GetGeneration(page) != this.Generation;
			var outcome = DropLeafRangeStreamed(leafId, page, cellCount, first, last);
			BumpVolatilityEpisodeAfterRebuild(in outcome, episode: true, firstMutationThisGeneration);
			if (!outcome.Split && this.Dirty.TryGetValue(outcome.FirstId, out var shrunk))
			{ // delete-driven underflow is exactly what the pre-commit consolidation arm feeds on
				NoteUnderflowCandidate(outcome.FirstId, shrunk);
			}
			AscendPatch(pathPages, pathChildren, depth - 1, leafId, outcome);

			// The page survived: it holds the same key RANGE, its parent's separators did not move, and the
			// descent that got us here already set the cursor's bounds. Only its id changed. Keeping the cursor
			// is what lets the next delete in this batch take the in-place path instead of descending again -
			// discarding it here meant the fast path could never engage twice in a row.
			uint reseated = outcome.Split ? 0 : outcome.FirstId;
			if (this.AppendLeaf == leafId) { this.AppendLeaf = reseated; }
			this.CursorLeaf = reseated;
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
			return childIndex == 0
				// the leftmost child dies: cell 0's child is the new leftmost, and its separator disappears
				? RemoveInternalRangeStreamed(pageId, page, cellCount, 0, 1, FdbLiteTreePage.GetChild(page, 1), caller: nameof(RebuildInternalRemoveChild))
				// cell childIndex-1 carried the dead child
				: RemoveInternalRangeStreamed(pageId, page, cellCount, childIndex - 1, childIndex, FdbLiteTreePage.GetLeftmostChild(page), caller: nameof(RebuildInternalRemoveChild));
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
			if (this.Dirty.Remove(pageId, out var released))
			{ // a released page must not be written back by a later flush, and its image buffer can be recycled
				if (this.CursorBufferId == pageId) { this.CursorBufferId = 0; this.CursorBuffer = null; }
				ReturnPageBuffer(released);
			}

			// a dead page's id can be reallocated within this very generation (its blocks go back through
			// FreeImmediately), and a stale candidacy would then nominate the fresh page - typically a GROWING
			// leaf, which is exactly what the consolidation arm promises never to touch
			this.UnderflowCandidates.Remove(pageId);

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

		/// <summary>Saturating increment of a leaf image's volatility episode count (the caller decided the dedup).</summary>
		private static void BumpVolatilityEpisodeRaw(Span<byte> image)
		{
			byte episodes = FdbLitePageHeader.GetVolatilityEpisodes(image);
			if (episodes != byte.MaxValue)
			{
				FdbLitePageHeader.SetVolatilityEpisodes(image, (byte) (episodes + 1));
			}
		}

		/// <summary>Records one post-fill mutation episode on an owned leaf image, at most once per generation.</summary>
		/// <remarks>The generation stamp IS the dedup: a stamp equal to this writer's generation means the page was already created, rebuilt, or episode-counted in this generation, and one generation is one episode however many mutations it lands. Advancing the stamp here also makes it truthful mid-generation on a verbatim-copied image (the seal re-stamp remains the backstop for images no episode touches).</remarks>
		private void BumpVolatilityEpisode(Span<byte> image)
		{
			if (FdbLitePageHeader.GetGeneration(image) == this.Generation)
			{
				return;
			}
			BumpVolatilityEpisodeRaw(image);
			FdbLitePageHeader.SetGeneration(image, this.Generation);
		}

		/// <summary>Records one mutation episode on every part of a rebuild outcome, when the caller's pre-rebuild capture says the episode counts and the page had not been touched this generation yet.</summary>
		/// <remarks>Rebuild outputs are freshly formatted and so already carry this generation's stamp: the dedup had to be decided against the SOURCE page's stamp, before the rebuild, which is why this cannot reuse the stamp-checking bump. Every part inherits the bump - a split of one volatile page is that page's history continuing in all of its parts.</remarks>
		private void BumpVolatilityEpisodeAfterRebuild(in RebuildResult outcome, bool episode, bool firstMutationThisGeneration)
		{
			if (!episode || !firstMutationThisGeneration)
			{
				return;
			}
			if (this.Dirty.TryGetValue(outcome.FirstId, out var image))
			{
				BumpVolatilityEpisodeRaw(image);
			}
			if (outcome.Siblings is { } siblings)
			{
				foreach (var (_, siblingId) in siblings)
				{
					if (this.Dirty.TryGetValue(siblingId, out var sibling))
					{
						BumpVolatilityEpisodeRaw(sibling);
					}
				}
			}
		}

		/// <summary>Splices a new key into a page image this generation already owns, when its free area can take the cell.</summary>
		/// <returns><c>false</c> when the page is not owned yet, the key is a REPLACE, or the cell does not fit: all three are the caller's signal to take the rebuild path, which compacts and splits.</returns>
		private bool TrySpliceInto(uint leafId, ReadOnlySpan<byte> key, CellRef newCell)
			=> TrySpliceInto(leafId, key, newCell.ResolveValue(default), newCell.Flags);

		/// <inheritdoc cref="TrySpliceInto(uint,System.ReadOnlySpan{byte},CellRef)"/>
		/// <remarks>
		/// Span-native on purpose, and it is the form the hot path calls. For an inline value the bytes the page
		/// wants ARE the caller's own bytes, so handing the spans down lets the page copy key and value exactly
		/// ONCE, straight into their final home - which is what the legacy prototype does and why it pays no
		/// scratch. Routing this through a <see cref="CellRef"/> instead cost a rented buffer plus a full extra
		/// copy of every key and every value, on every insert, for a cell that never outlives the call.
		/// </remarks>
		private bool TrySpliceInto(uint leafId, ReadOnlySpan<byte> key, ReadOnlySpan<byte> storedValue, byte flags)
		{
			if (DirtyBufferOf(leafId) is not { } buffered)
			{
				return false;
			}

			// splicing into the free area beats re-gathering and re-serializing every cell in the page (the rebuild
			// path is O(cells) per insert)
			var image = buffered.AsSpan();
			// append fast path: a sequential load lands EVERY key past the page's last cell, and the binary
			// search pays ~10 suffix decode-and-compares per key to discover that; one compare against the
			// last cell answers it (greater = append, anything else = the ordinary search)
			int at;
			bool exists;
			int cellCount = FdbLitePageHeader.GetCellCount(image);
			if (cellCount > 0 && FdbLiteTreePage.CompareLeafKey(image, cellCount - 1, key) < 0)
			{
				at = cellCount;
				exists = false;
			}
			else
			{
				at = FdbLiteTreePage.FindLeafSlot(image, key, out exists);
			}
			if (exists)
			{
				// A REPLACE. When the replacement occupies exactly the same room this is a memcpy over bytes
				// this generation already owns: nothing moves, and the cursor stays valid, so the per-key
				// descent disappears along with the rebuild.
				// An extent on EITHER side is deliberately excluded. Those blocks have to be freed and
				// reallocated, which is not an overwrite - it is precisely where a leak or a double free would
				// live - so it keeps the rebuild path.
				if (flags == 0
				 && (FdbLiteTreePage.GetLeafFlags(image, at) & FdbLiteTreePage.FlagValueIsExtent) == 0)
				{
					// fits where it lies, or grows into the free gap and leaves its old slot behind as waste
					if (FdbLiteTreePage.TryOverwriteLeafValue(image, at, storedValue, flags)
					 || FdbLiteTreePage.TryRelocateLeafValue(image, at, storedValue, flags))
					{
						this.CellsOverwritten++;
						BumpVolatilityEpisode(image); // an in-place value mutation is an episode
						NoteUnderflowCandidate(leafId, image); // a shrink can leave the page under the threshold
						// deliberately NOT KeyCountDelta: a replace introduces no key
						return true;
					}
				}
				return false;
			}

			// interior iff strictly below this leaf's own maximum: the leaf's right edge is its append edge,
			// which holds for any number of append-shaped subspaces at once (a global maximum would not)
			bool interior = at < FdbLitePageHeader.GetCellCount(image);
			if (!FdbLiteTreePage.TryInsertLeafCell(image, at, key, storedValue, flags))
			{
				return false;
			}

			this.CellsSpliced++;
			this.KeyCountDelta++;
			if (interior)
			{
				BumpVolatilityEpisode(image);
			}
			// the INPUT side of the trace. The LEAF+/LEAF= records say which pages were created and by which
			// method, which is enough to explain a split once you suspect one, but not enough to reconstruct
			// how a page got to the state where it split. This says where each key landed and how full the
			// page was afterwards, so one leaf's whole life can be replayed from the log.
			if (OpLog is { } log)
			{
				log($"SPLICE\t{leafId}\tat={at}\tcells={FdbLitePageHeader.GetCellCount(image)}\tlive={FdbLiteTreePage.LeafLiveBytes(image)}\tinterior={(interior ? 1 : 0)}\tepisodes={FdbLitePageHeader.GetVolatilityEpisodes(image)}");
			}
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
			if (DirtyBufferOf(leafId) is not { } buffered)
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
			BumpVolatilityEpisode(image); // a delete is an episode
			NoteUnderflowCandidate(leafId, image);
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

			// Snapshot before allocating: WritePage allocates, and an allocation may grow the pager, which is
			// exactly the source-page aliasing WriteCells already guards against. Mutating the SNAPSHOT before
			// anything is allocated or freed also means a refused mutation (a flags byte the helpers reject -
			// impossible today, one new flag bit away tomorrow) backs out with NO side effects and the rebuild
			// path, correct for any flags, takes over. Mutating after WritePage had no such exit: returning
			// true dropped the write silently in Release, and returning false would have freed the page twice.
			int pageSize = this.Pager.Geometry.PageSize;
			var snapshot = ArrayPool<byte>.Shared.Rent(pageSize);
			try
			{
				page.CopyTo(snapshot);
				var copy = snapshot.AsSpan(0, pageSize);
				if (!FdbLiteTreePage.TryOverwriteLeafValue(copy, at, storedValue, newCell.Flags)
				 && !FdbLiteTreePage.TryRelocateLeafValue(copy, at, storedValue, newCell.Flags))
				{
					return false;
				}
				BumpVolatilityEpisode(copy); // an in-place value mutation is an episode (this is its first-touch form)
				newId = WritePage(leafId, copy);
				NoteUnderflowCandidate(newId, copy); // after WritePage: a first touch relocates, and the note must name the page that exists
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(snapshot);
			}
			this.CellsOverwritten++;
			// deliberately NOT KeyCountDelta: a replace introduces no key
			return true;
		}

		/// <summary>Rebuilds a leaf with one key inserted or replaced (a replaced extent value is released).</summary>
		/// <param name="leafId">The ID of the leaf to rebuild</param>
		/// <param name="key">The key to insert or replace</param>
		/// <param name="newCell">The cell to insert or replace</param>
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

			// The episode decision is taken HERE, against the page as it stands before any rebuild below: a
			// rebuild output carries this generation's stamp, so the once-per-generation dedup has to read the
			// SOURCE's. A replace or an interior insert (strictly below this leaf's own maximum) is an episode;
			// landing at or past the maximum is the leaf's append edge filling, which never counts.
			bool firstMutationThisGeneration;
			bool episode;
			{
				var current = ReadPage(leafId);
				firstMutationThisGeneration = FdbLitePageHeader.GetGeneration(current) != this.Generation;
				int at = FdbLiteTreePage.FindLeafSlot(current, key, out bool exact);
				episode = exact || at < FdbLitePageHeader.GetCellCount(current);
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
					var stripped = new RebuildResult(strippedId, null);
					BumpVolatilityEpisodeAfterRebuild(in stripped, episode, firstMutationThisGeneration);
					return stripped;
				}
				leafId = strippedId;
			}

			var page = ReadPage(leafId);
			int cellCount = FdbLitePageHeader.GetCellCount(page);
			int insertAt = FdbLiteTreePage.FindLeafSlot(page, key, out bool replace);

			// the moment a page stops taking keys in place. Paired with the SPLICE records it says how full the
			// page was when it gave up and WHERE in it the key was going, which is what distinguishes a page
			// that filled evenly from one a sweeping run walked off the end of.
			if (OpLog is { } rlog)
			{
				rlog($"REBUILD\t{leafId}\tat={insertAt}\tcells={cellCount}\tlive={FdbLiteTreePage.LeafLiveBytes(page)}\trightmost={(rightmost ? 1 : 0)}\treplace={(replace ? 1 : 0)}\tepisodes={FdbLitePageHeader.GetVolatilityEpisodes(page)}");
			}

			// `rightmost` is load-bearing and is NOT safely generalisable to "append-shaped", which was tried
			// and measured on 2026-08-02. The zero-volatility-episode count looks like the same statement
			// (only an `interior` insert bumps it, see TrySpliceInto) but it is a statement about the PAST,
			// whereas this branch is a bet about the FUTURE. At the tree's right edge the bet is guaranteed:
			// no key can ever sort into this page again. Anywhere else it is a guess, and under scattered
			// arrival it is usually wrong, because later keys DO land in the page that was packed full and
			// split it anyway. Measured cost of the generalisation over 500k scattered keys: leaves 10,408 ->
			// 10,458, page copies 21,898 -> 22,381, fill 76% -> 75%.
			//
			// It also bought nothing on sorted arrival, which is what it was written for. Sorting a commit's
			// keys does not make its inserts append-shaped: 100,000 sorted keys spread over 10,408 leaves land
			// BETWEEN keys already in each leaf, so they are interior inserts and neither this condition nor
			// `insertAt == cellCount` holds. Append-shape needs fresh space, not ordered input, which is why
			// only a load into an empty range reaches it.
			if (this.AvoidSequentialAppendSplits
			 && rightmost && !replace && insertAt == cellCount
			 && !FdbLiteTreePage.LeafHasRoomFor(page, newCell.KeyLength, newCell.ValueLength))
			{ // a key appending past the last one in the rightmost leaf: nothing will ever insert into this page
			  // again, so splitting it in half strands half of it forever. Leave it packed and start a fresh page,
			  // which the ascent hangs off the parent as a right sibling separated by this very key.
				this.KeyCountDelta++;
				this.PagesAppended++;
				return new(leafId, [ (Slice.FromBytes(key), WriteFreshSingleCellPage(in newCell)) ]);
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
			this.StreamedLeafRebuilds++;
			var outcome = WriteCellsStreamed(leafId, page, new LeafInsertSource(page, in newCell, insertAt, replace, resultCount));
			BumpVolatilityEpisodeAfterRebuild(in outcome, episode, firstMutationThisGeneration);
			if (replace && !outcome.Split && this.Dirty.TryGetValue(outcome.FirstId, out var replaced))
			{ // a shrinking replace is a shrink site like any other (an insert or a split is growth, and growth
			  // must never seed a consolidation candidate: repacking a growing region invites split/merge cycles)
				NoteUnderflowCandidate(outcome.FirstId, replaced);
			}
			return outcome;
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

			// the fused single-page emit's incremental accounting IS the strict-shrink guard: an abort (the
			// rebuilt run would split, which cannot happen while the sizing is exact) fails safely before any
			// side effect, and the caller's ordinary rebuild-and-split path handles it
			uint rebuiltId = TryStripStreamed(leafId, page);
			if (rebuiltId == 0)
			{
				return 0;
			}
			this.PagesStripped++;
			spliced = TrySpliceInto(rebuiltId, key, newCell);
			return rebuiltId;
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
			=> RebuildInternal(pageId, childIndex, child.FirstId, AsSiblingSpan(child));

		/// <summary>True when <paramref name="pageId"/> holds a separator right after child <paramref name="childIndex"/> and it sorts strictly below <paramref name="bound"/>.</summary>
		private bool FollowingSeparatorSitsBelow(uint pageId, int childIndex, ReadOnlySpan<byte> bound)
		{
			var page = ReadPage(pageId);
			return childIndex < FdbLitePageHeader.GetCellCount(page)
				&& FdbLiteTreePage.GetSeparator(page, childIndex).SequenceCompareTo(bound) < 0;
		}

		/// <inheritdoc cref="RebuildInternal(uint,int,RebuildResult)"/>
		/// <param name="raiseFollowingSeparator">Replaces the separator of cell <paramref name="childIndex"/> (the one that follows the descended child), keeping its child; empty leaves it verbatim. See <see cref="AscendPatch(ReadOnlySpan{uint},ReadOnlySpan{int},int,uint,uint,ReadOnlySpan{ValueTuple{Slice,uint}},ReadOnlySpan{byte})"/>.</param>
		private RebuildResult RebuildInternal(uint pageId, int childIndex, uint childFirstId, ReadOnlySpan<(Slice Separator, uint PageId)> childSiblings, ReadOnlySpan<byte> raiseFollowingSeparator = default)
		{
			var page = ReadPage(pageId);
			int cellCount = FdbLitePageHeader.GetCellCount(page);
			return RebuildInternalStreamed(pageId, page, cellCount, childIndex, childFirstId, childSiblings, raiseFollowingSeparator);
		}

		/// <summary>Builds one new root level over a split result (loops in the caller if the new level itself splits).</summary>
		private RebuildResult BuildRootLevel(uint firstId, ReadOnlySpan<(Slice Separator, uint PageId)> siblings)
			=> BuildRootLevelStreamed(firstId, siblings);

		/// <summary>Writes a rebuilt cell list as one page, or as a K-way split when it does not fit (greedy: each page takes the largest prefix that fits).</summary>
		/// <param name="oldPageId">The ID of the page being replaced</param>
		/// <param name="isInternal">True if the page is an internal node, false if it is a leaf</param>
		/// <param name="leftmostChild">The ID of the leftmost child node</param>
		/// <param name="sourcePage">The page from which the cells are being rebuilt</param>
		/// <param name="cells">The list of cells to write</param>
		/// <param name="maxLeafFillBytes">Fill ceiling per emitted LEAF page (0 = the page size): a consolidation merge aims each part at its volatility-adaptive target instead of packing to capacity, which is the hysteresis that keeps a merged run from re-splitting under the workload that produced it</param>
		/// <param name="declaredEpisodes">Volatility episode count to stamp on the emitted LEAF pages, or -1 to carry <paramref name="sourcePage"/>'s own. An IMPORT knows the answer up front (<see cref="FdbLiteVolatilityClass"/>, whose values ARE episode counts) and says so; inheriting the boundary leaf's history instead would brand a fresh bulk-loaded page with the volatility of the page it happened to land next to.</param>
		/// <param name="caller">Filled in by the compiler; carried only so the trace can name what created a page.</param>
		private RebuildResult WriteCells(uint oldPageId, bool isInternal, uint leftmostChild, ReadOnlySpan<byte> sourcePage, ReadOnlySpan<CellRef> cells, int maxLeafFillBytes = 0, int declaredEpisodes = -1, [System.Runtime.CompilerServices.CallerMemberName] string? caller = null)
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

			// A single source page's rebuild is that page's life continuing (all split parts inherit its
			// episode count); a page built from anywhere else - fresh, or a MERGE gathered from several
			// pages into buffers - restarts at zero. Reset-on-repack is part of the counter's definition:
			// counted since birth, a one-time bulk load brands its leaves volatile forever and the
			// write-once shape the count exists to identify could never be packed full again.
			// A DECLARED class overrides the whole rule: a graft always passes a source page (its buffer-less cells
			// resolve against it), so without this every fresh grafted page would inherit the boundary leaf's
			// history and the caller's declared intent would be silently discarded.
			byte carriedEpisodes =
				declaredEpisodes >= 0 ? (byte) declaredEpisodes
				: !isInternal && sourcePage.Length > 0 ? FdbLitePageHeader.GetVolatilityEpisodes(sourcePage)
				: (byte) 0;

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
				List<(Slice Separator, uint PageId)>? siblings = null;
				uint firstId = 0;

				// balanced K-way split: cutting at the LARGEST prefix that fits would leave near-empty right
				// siblings that collapse occupancy to ~20% under random inserts (a full left page re-splits on
				// its very next insert); aiming each part at total/K keeps post-split occupancy near half, and
				// the hard per-page limit still absorbs the adversarial giant-cell cases
				long fillCeiling = !isInternal && maxLeafFillBytes > 0 ? Math.Min(maxLeafFillBytes, usable) : usable;
				int partCount = (int) ((totalBytes + fillCeiling - 1) / fillCeiling);

				if (!isInternal && partCount > 1 && SplitDiagnostics is { } diag)
				{
					// re-derive the run's own LCP for the report; the sizing above already used it
					int reportLcp = 0;
					if (cells.Length > 1)
					{
						var probe = ArrayPool<byte>.Shared.Rent(2 * FdbLiteTreePage.MaxKeyLength);
						try
						{
							var sp = sourcePrefixLength > 0 ? FdbLiteTreePage.GetPagePrefix(sourcePage, isInternal: false) : default;
							var lo = MaterializeKey(cells[0], sourcePage, sp, probe.AsSpan(0, FdbLiteTreePage.MaxKeyLength));
							var hi = MaterializeKey(cells[^1], sourcePage, sp, probe.AsSpan(FdbLiteTreePage.MaxKeyLength, FdbLiteTreePage.MaxKeyLength));
							reportLcp = FdbLiteTreePage.CommonPrefixLength(lo, hi);
						}
						finally { ArrayPool<byte>.Shared.Return(probe); }
					}
					long sourceLive = sourcePage.IsEmpty ? 0 : FdbLiteTreePage.LeafLiveBytes(sourcePage);
					diag(cells.Length, totalBytes, (int) fillCeiling, partCount, sourcePrefixLength, reportLcp, sourceLive);
				}
				// The balance target is recomputed from what is STILL LEFT, not fixed once at total/partCount.
				//
				// Fixing it strands a tail, and that tail was the engine's largest space defect. Every part pays
				// the per-page fixed overhead (header, prefix region, its own slot directory) in full, but a
				// target of total/K silently assumes that overhead is shared across the K parts. So K parts sized
				// at total/K hold slightly LESS than the run, and the leftover becomes an extra, nearly empty
				// page. Measured before this change: a two-way split of a 1,717-cell leaf emitted parts of 854,
				// 854 and NINE cells, on every split, which is what drove leaf fill on random inserts down to
				// 31% (against 99% sequential) and the store to several times its logical size.
				//
				// Dividing what remains by the parts that remain makes the last part's target the whole
				// remainder, so it absorbs the tail. The hard per-page cap below is untouched: if the remainder
				// genuinely does not fit, the loop still starts another part, so this can only reduce part count.
				long remainingBytes = totalBytes;
				int remainingParts = partCount;

				int start = 0;
				uint partLeftmost = leftmostChild;
				byte[]? partSeparator = null;
				while (true)
				{
					long targetBytes = remainingParts > 1 ? (remainingBytes + remainingParts - 1) / remainingParts
						: maxLeafFillBytes > 0 ? fillCeiling
						: long.MaxValue;

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
						end = LeafPartEnd(cells, start, sourcePage, sourcePrefixLength, targetBytes, pageSize, partScratch.AsSpan(0, FdbLiteTreePage.MaxKeyLength), out bytes);
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
					if (carriedEpisodes != 0) { FdbLitePageHeader.SetVolatilityEpisodes(image, carriedEpisodes); }
					if (isInternal) { FdbLiteTreePage.SetLeftmostChild(image, partLeftmost); }
					AppendCells(image, isInternal, sourcePage, cells, start, end);
					uint reusing = partSeparator == null ? oldPageId : 0;
					uint id = WritePage(reusing, image);
					if (OpLog is { } log)
					{
						// "+" means a page that did NOT exist before this call: either a part beyond the first
						// (which always allocates) or a build with no source page at all. This is the ONLY place
						// a tree page is created, so a count of these records by caller is exhaustive.
						string tag = reusing == 0 ? (isInternal ? "NODE+" : "LEAF+") : (isInternal ? "NODE=" : "LEAF=");
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

					if (nextStart >= cells.Length && nextSeparator == null)
					{
						break;
					}
					// book what this part consumed, so the next target is computed from what is genuinely left.
					// remainingParts floors at 1: once the plan is used up, every further part (which can only
					// happen if the remainder did not fit) takes as much as the page allows.
					remainingBytes -= bytes;
					if (remainingParts > 1) { remainingParts--; }

					start = nextStart;
					partSeparator = nextSeparator;
					partLeftmost = nextLeftmost;

					if (start >= cells.Length && isInternal)
					{ // the last cell got promoted: the final part is a leftmost-only internal page (degenerate but legal)
						FdbLitePageHeader.Format(image, type, this.Generation);
						FdbLiteTreePage.SetLeftmostChild(image, partLeftmost);
						AppendCells(image, isInternal, sourcePage, cells, 0, 0);
						uint tailId = WritePage(0, image);
						// wrapped, not copied: a split emits one or two separators, so the per-separator array it
						// already allocated is not worth pooling away (the GRAFT path, which emits hundreds, packs
						// them into one rented buffer instead - see GraftedSiblings)
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

		/// <summary>End (exclusive) of the leaf part starting at <paramref name="start"/>: the longest run that stays within <paramref name="targetBytes"/> and never exceeds <paramref name="pageSize"/>, with the bytes it occupies.</summary>
		/// <param name="scratch">Working buffer of at least <see cref="FdbLiteTreePage.MaxKeyLength"/> bytes, for materializing the part's first key.</param>
		/// <param name="bytes">Footprint of the returned part, on the same basis as <see cref="LeafRunBytes"/>.</param>
		/// <remarks>
		/// <para>A leaf part is measured against the prefix IT will strip, which shrinks as the part grows and makes
		/// every cell already in it a byte or more longer. So the cost of the whole part is recomputed on each
		/// candidate rather than accumulated per cell: an incremental sum cannot express "the cell I just added made
		/// all the previous ones bigger".</para>
		/// <para>Shared between <see cref="WriteCells"/> and <see cref="RenderRun"/> deliberately: the renderer picks
		/// page boundaries and then hands each range to <see cref="WriteCells"/> as a single page, so a second sizing
		/// rule disagreeing by one byte would make that call split again, stranding a near-empty page per run.</para>
		/// <para>Always takes at least one cell: a single cell is guaranteed to fit a page by the page-size floor.</para>
		/// </remarks>
		private static int LeafPartEnd(ReadOnlySpan<CellRef> cells, int start, ReadOnlySpan<byte> sourcePage, int sourcePrefixLength, long targetBytes, int pageSize, Span<byte> scratch, out long bytes)
		{
			var sourcePrefix = sourcePrefixLength > 0 ? FdbLiteTreePage.GetPagePrefix(sourcePage, isInternal: false) : default;
			var firstKey = MaterializeKey(cells[start], sourcePage, sourcePrefix, scratch);
			int lcp = firstKey.Length;
			long sumWhole = 0, sumValue = 0;

			bytes = 0;
			int end = start;
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
			return end;
		}

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
		private static void AppendCells(Span<byte> image, bool isInternal, ReadOnlySpan<byte> sourcePage, ReadOnlySpan<CellRef> cells, int start, int end)
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

		#region Pre-commit consolidation...

		// The pre-commit consolidation arm: dirty pages have not been written yet, so merging N of them into
		// fewer REMOVES page writes from the flush and frees their pages immediately. The trigger is
		// delete/shrink-driven underflow ONLY: a page that is under-full because its region is GROWING (a fresh
		// append page, a freshly split half) must never be repacked, or the next insert re-splits it - which is
		// why candidacy comes from the mutation sites' notes and never from occupancy alone.

		/// <summary>Leaves this generation noted as consolidation candidates at a delete or shrink site (advisory: each is re-validated at consume time).</summary>
		private HashSet<uint> UnderflowCandidates { get; } = [ ];

		/// <summary>Test probe over <see cref="UnderflowCandidates"/>: the invariant under test is that a freed page never stays noted (its id can be reallocated within the generation).</summary>
		internal IReadOnlySet<uint> UnderflowCandidateSet => this.UnderflowCandidates;

		/// <summary>Candidate runs merged by this writer's pre-commit consolidation</summary>
		public int ConsolidationRunsMerged { get; private set; }

		/// <summary>Net pages freed by consolidation (inputs minus merged outputs)</summary>
		public int ConsolidationPagesFreed { get; private set; }

		/// <summary>Cold (clean, committed) sparse neighbors pulled into a merge at a run edge</summary>
		public int ConsolidationColdPagesPulled { get; private set; }

		/// <summary>Viable runs left unmerged when the consolidation loop stopped on its budget or caps</summary>
		public int ConsolidationRunsSkipped { get; private set; }

		/// <summary>Underflow: live bytes strictly below U = 0.60 of the page (the ruled threshold; it moves far more space than the fill target, and 0.60 sits a comfortable margin from the measured U=0.70 oscillation cliff).</summary>
		private static bool IsUnderflowLeaf(ReadOnlySpan<byte> page)
			=> FdbLiteTreePage.LeafLiveBytes(page) * 10 < (long) page.Length * 6;

		/// <summary>Fill ceiling of a merged run, from the run's volatility class: pack-full is the default posture, headroom the lavish exception for the small volatile population (class 0 packs to 1.00, class 1 to 0.90, class 2+ to 0.85 - the measured target that re-split nowhere).</summary>
		private static int MergedFillCeiling(byte maxEpisodes, int pageSize)
			=> maxEpisodes == 0 ? pageSize
			 : maxEpisodes == 1 ? (pageSize * 9) / 10
			 : (pageSize * 85) / 100;

		/// <summary>Notes a leaf as a consolidation candidate when the mutation that just shrank it left it under the underflow threshold.</summary>
		private void NoteUnderflowCandidate(uint leafId, ReadOnlySpan<byte> image)
		{
			if (IsUnderflowLeaf(image))
			{
				this.UnderflowCandidates.Add(leafId);
			}
		}

		/// <summary>One mergeable run: adjacent under-same-parent leaves (noted candidates, optionally extended by one cold sparse neighbor per edge) whose combined prefix-adjusted live bytes fit fewer pages at the volatility-adaptive fill target.</summary>
		private sealed record ConsolidationRun(uint[] PathPages, int[] PathChildren, int Depth, uint ParentId, int FirstChildIndex, int LastChildIndex, uint[] InputIds, int OutputParts, int FillCeiling, int ColdPulls)
		{
			public int PagesFreed => this.InputIds.Length - this.OutputParts;
		}

		/// <summary>Merges under-full dirty leaf runs, best run first, until the caps (or the caller's budget) stop the loop.</summary>
		/// <param name="maxRuns">Maximum candidate runs to merge</param>
		/// <param name="maxInputPages">Hard cap on total input pages consumed (the adaptive budget's safety net)</param>
		/// <param name="outOfBudget">Polled before each merge; <c>true</c> stops the loop THERE - the candidate order itself stays deterministic, so two runs of the same commit differ at most in a suffix of the merge list</param>
		/// <returns>Runs merged and net pages freed</returns>
		public (int RunsMerged, int PagesFreed) ConsolidateUnderflow(int maxRuns, int maxInputPages = int.MaxValue, Func<bool>? outOfBudget = null)
		{
			Contract.Positive(maxRuns);
			int runsMerged = 0, pagesFreed = 0, inputPages = 0;
			if (this.UnderflowCandidates.Count == 0 || this.Root == 0)
			{ // the common no-delete commit pays exactly this check
				return (0, 0);
			}

			while (true)
			{
				// re-collect after every merge: a merge rewrites the parent (and can split it), so ranked
				// runs collected before it would point at stale pages; the walk is O(dirty internal pages)
				var runs = CollectConsolidationRuns();
				if (runs.Count == 0)
				{
					break;
				}
				if (runsMerged >= maxRuns || inputPages >= maxInputPages || outOfBudget?.Invoke() == true)
				{
					this.ConsolidationRunsSkipped += runs.Count;
					break;
				}

				var best = runs[0];
				pagesFreed += MergeConsolidationRun(best);
				runsMerged++;
				inputPages += best.InputIds.Length;
				this.ConsolidationColdPagesPulled += best.ColdPulls;
			}

			this.ConsolidationRunsMerged += runsMerged;
			this.ConsolidationPagesFreed += pagesFreed;
			return (runsMerged, pagesFreed);
		}

		/// <summary>Walks the dirty internal pages and returns every viable run, best first (most pages freed, then fewest cold pulls, then tree order - fully deterministic).</summary>
		private List<ConsolidationRun> CollectConsolidationRuns()
		{
			var runs = new List<ConsolidationRun>();
			Span<uint> pathPages = stackalloc uint[MaxDepth];
			Span<int> pathChildren = stackalloc int[MaxDepth];
			Collect(this.Root, pathPages, pathChildren, 0, runs);
			// List.Sort is unstable, so the tree-order tiebreak is explicit: FirstChildIndex then ParentId
			runs.Sort(static (a, b) =>
			{
				int c = b.PagesFreed.CompareTo(a.PagesFreed);
				if (c != 0) { return c; }
				c = a.ColdPulls.CompareTo(b.ColdPulls);
				if (c != 0) { return c; }
				c = a.ParentId.CompareTo(b.ParentId);
				return c != 0 ? c : a.FirstChildIndex.CompareTo(b.FirstChildIndex);
			});
			return runs;

			void Collect(uint pageId, Span<uint> pathPages, Span<int> pathChildren, int depth, List<ConsolidationRun> runs)
			{
				if (!this.Dirty.TryGetValue(pageId, out var buffered))
				{ // candidates are dirty, and their ancestor chain is dirty: a clean subtree cannot hold one
					return;
				}
				var image = buffered.AsSpan();
				if (FdbLitePageHeader.GetPageType(image) != FdbLitePageType.Internal)
				{
					return;
				}

				int childCount = FdbLiteTreePage.GetChildCount(image);
				uint firstChildId = FdbLiteTreePage.GetChild(image, 0);
				bool childrenAreLeaves = FdbLitePageHeader.GetPageType(
					this.Dirty.TryGetValue(firstChildId, out var childImage) ? childImage : this.Pager.ReadBlocks(firstChildId, this.Pager.Geometry.BlocksPerPage)) == FdbLitePageType.Leaf;

				if (!childrenAreLeaves)
				{
					Contract.Debug.Assert(depth + 1 < MaxDepth);
					pathPages[depth] = pageId;
					for (int i = 0; i < childCount; i++)
					{
						pathChildren[depth] = i;
						Collect(FdbLiteTreePage.GetChild(image, i), pathPages, pathChildren, depth + 1, runs);
					}
					return;
				}

				// maximal stretches of candidate children (noted at a delete/shrink site AND still under U)
				int stretchStart = -1;
				for (int i = 0; i <= childCount; i++)
				{
					bool candidate = false;
					if (i < childCount)
					{
						uint childId = FdbLiteTreePage.GetChild(image, i);
						candidate = this.UnderflowCandidates.Contains(childId)
							&& this.Dirty.TryGetValue(childId, out var leafImage)
							&& FdbLitePageHeader.GetPageType(leafImage) == FdbLitePageType.Leaf
							&& IsUnderflowLeaf(leafImage);
					}
					if (candidate)
					{
						if (stretchStart < 0) { stretchStart = i; }
						continue;
					}
					if (stretchStart >= 0)
					{
						var run = EvaluateRun(image, pathPages, pathChildren, depth, pageId, stretchStart, i - 1);
						if (run is not null) { runs.Add(run); }
						stretchStart = -1;
					}
				}
			}
		}

		/// <summary>True when the child at <paramref name="childIndex"/> is a COLD sparse leaf: clean (a page dirtied this generation is not cold, which is also what keeps a freshly split half out) and itself under the underflow threshold. Reading it is the writer's ordinary verified first touch, the cost the ruling priced at ~one cold read per extra page freed.</summary>
		private bool IsColdSparseChild(ReadOnlySpan<byte> parent, int childIndex, int childCount)
		{
			if (childIndex < 0 || childIndex >= childCount)
			{
				return false;
			}
			uint id = FdbLiteTreePage.GetChild(parent, childIndex);
			if (this.Dirty.ContainsKey(id))
			{
				return false;
			}
			var page = ReadPage(id);
			return FdbLitePageHeader.GetPageType(page) == FdbLitePageType.Leaf && IsUnderflowLeaf(page);
		}

		/// <summary>Sizes one candidate stretch and its cold-neighbor extensions, and returns the best viable variant (null when no variant frees a page).</summary>
		private ConsolidationRun? EvaluateRun(ReadOnlySpan<byte> parent, ReadOnlySpan<uint> pathPages, ReadOnlySpan<int> pathChildren, int depth, uint parentId, int firstChild, int lastChild)
		{
			int childCount = FdbLiteTreePage.GetChildCount(parent);

			bool leftCold = IsColdSparseChild(parent, firstChild - 1, childCount);
			bool rightCold = IsColdSparseChild(parent, lastChild + 1, childCount);

			ConsolidationRun? best = null;
			foreach (var (extendLeft, extendRight) in new[] { (false, false), (true, false), (false, true), (true, true) })
			{
				if ((extendLeft && !leftCold) || (extendRight && !rightCold))
				{
					continue;
				}
				int a = firstChild - (extendLeft ? 1 : 0);
				int b = lastChild + (extendRight ? 1 : 0);
				int inputCount = b - a + 1;
				if (inputCount < 2)
				{ // a lone candidate merges with nothing
					continue;
				}

				// prefix-adjusted sizing over the whole variant, and its volatility class from the worst member
				long sumWhole = 0, sumValue = 0;
				int cellTotal = 0;
				byte maxEpisodes = 0;
				for (int c = a; c <= b; c++)
				{
					var page = ReadPage(FdbLiteTreePage.GetChild(parent, c));
					int prefixLen = FdbLitePageHeader.GetPrefixLength(page);
					int cells = FdbLitePageHeader.GetCellCount(page);
					for (int i = 0; i < cells; i++)
					{
						var cell = FdbLiteTreePage.ReadLeafCell(page, i);
						sumWhole += prefixLen + cell.KeyLength;
						sumValue += cell.ValueLength;
					}
					cellTotal += cells;
					byte episodes = FdbLitePageHeader.GetVolatilityEpisodes(page);
					if (episodes > maxEpisodes) { maxEpisodes = episodes; }
				}
				if (cellTotal == 0)
				{
					continue;
				}

				var firstPage = ReadPage(FdbLiteTreePage.GetChild(parent, a));
				var lastPage = ReadPage(FdbLiteTreePage.GetChild(parent, b));
				int lcp = FdbLiteTreePage.CommonPrefixLength(WholeKeyOf(firstPage, 0), WholeKeyOf(lastPage, FdbLitePageHeader.GetCellCount(lastPage) - 1));

				int fillCeiling = MergedFillCeiling(maxEpisodes, this.Pager.Geometry.PageSize);
				long runBytes = LeafRunBytes(cellTotal, sumWhole, sumValue, lcp);
				int outputParts = (int) ((runBytes + fillCeiling - 1) / fillCeiling);
				if (outputParts >= inputCount)
				{ // no page freed: leave it to the background vacuum's longer cross-parent runs
					continue;
				}

				var inputIds = new uint[inputCount];
				for (int c = a; c <= b; c++)
				{
					inputIds[c - a] = FdbLiteTreePage.GetChild(parent, c);
				}
				var candidate = new ConsolidationRun(
					pathPages[..depth].ToArray(), pathChildren[..depth].ToArray(), depth,
					parentId, a, b, inputIds, outputParts, fillCeiling,
					ColdPulls: (extendLeft ? 1 : 0) + (extendRight ? 1 : 0));

				if (best is null
				 || candidate.PagesFreed > best.PagesFreed
				 || (candidate.PagesFreed == best.PagesFreed && candidate.ColdPulls < best.ColdPulls))
				{
					best = candidate;
				}
			}
			return best;
		}

		/// <summary>Executes one merge: gathers the run's live cells (whole keys re-expanded), emits them at the run's fill ceiling, frees the emptied inputs, and rewrites the parent over the new child list.</summary>
		/// <returns>Pages actually freed (the emission's real part count can differ from the sizing estimate by a page)</returns>
		private int MergeConsolidationRun(ConsolidationRun run)
		{
			// extent cells travel as their descriptors: the extents themselves do not move and must NOT be
			// freed - only the emptied leaf pages are
			var merged = MergeConsolidationCellsStreamed(run.InputIds, run.FillCeiling, caller: nameof(MergeConsolidationRun));
			for (int p = 1; p < run.InputIds.Length; p++)
			{ // the emission already handled input 0 (rebuilt in place when dirty, queued for delayed free when cold)
				FreePage(run.InputIds[p]);
			}

			var parentOutcome = RebuildInternalReplaceRun(run.ParentId, run.FirstChildIndex, run.LastChildIndex, merged);
			AscendPatch(run.PathPages, run.PathChildren, run.Depth - 1, run.ParentId, parentOutcome);

			// leaf identities and ranges under this parent changed; the cursor must not survive that
			this.CursorLeaf = 0;
			this.AppendLeaf = 0;

			return run.InputIds.Length - 1 - (merged.Siblings?.Count ?? 0);
		}

		/// <summary>Rebuilds an internal page with children [<paramref name="firstChildIndex"/>, <paramref name="lastChildIndex"/>] replaced by a merge outcome's parts.</summary>
		private RebuildResult RebuildInternalReplaceRun(uint pageId, int firstChildIndex, int lastChildIndex, RebuildResult merged)
		{
			var page = ReadPage(pageId);
			int cellCount = FdbLitePageHeader.GetCellCount(page);
			return RebuildInternalReplaceRunStreamed(pageId, page, cellCount, firstChildIndex, lastChildIndex, in merged);
		}

		#endregion

		#region Background vacuum...

		// A vacuum step is an ordinary writer generation with NO logical changes: descend by the occupancy
		// aggregates to the worst leaf-parent, gather the live cells of a run of adjacent sparse leaves, and
		// re-emit them at the volatility-adaptive fill target. Same primitives as everything else; candidacy
		// is OCCUPANCY here (not the delete-site notes), which is what lets whatever the pre-commit arm
		// skipped on budget fall to this arm by construction. Cross-parent consolidation is in scope HERE and
		// only here: a run may extend across ONE leaf-parent boundary into the forward neighbor's leading
		// sparse leaves (ponytail: one boundary per step, and the neighbor keeps its last leaf so the join
		// separator always exists one level down; longer chains take one extra step each).

		/// <summary>What one vacuum pass over the worst region did.</summary>
		public readonly record struct VacuumOutcome(int InputPages, int OutputPages, bool CrossedParentBoundary)
		{
			public int PagesFreed => this.InputPages - this.OutputPages;
		}

		/// <summary>Descends to the worst-occupancy leaf-parent and merges its best run of adjacent sparse leaves (possibly extending over one boundary); all-zero when nothing viable exists, in which case NOTHING was allocated or written and the generation can be abandoned.</summary>
		public VacuumOutcome VacuumWorstRegion(int maxInputPages)
		{
			Contract.Requires(maxInputPages >= 2);
			if (this.Root == 0)
			{
				return default;
			}

			// descend by the stored aggregates: expand only the guiltiest subtree, so the plan costs O(hot
			// path) header peeks (raw reads on purpose: a peek is not a touch, and the pages a merge actually
			// consumes are read verified below)
			Span<uint> pathPages = stackalloc uint[MaxDepth];
			Span<int> pathChildren = stackalloc int[MaxDepth];
			int depth = 0;
			uint pageId = this.Root;
			int pageSize = this.Pager.Geometry.PageSize;
			long idealPageBytes = (pageSize * 9L) / 10;
			while (true)
			{
				var page = this.Pager.ReadBlocks(pageId, this.Pager.Geometry.BlocksPerPage);
				if (FdbLitePageHeader.GetPageType(page) != FdbLitePageType.Internal)
				{ // a root that is itself a leaf has no siblings to merge
					return default;
				}
				var firstChild = this.Pager.ReadBlocks(FdbLiteTreePage.GetChild(page, 0), this.Pager.Geometry.BlocksPerPage);
				if (FdbLitePageHeader.GetPageType(firstChild) == FdbLitePageType.Leaf)
				{
					break; // pageId is the target leaf-parent
				}

				int bestIndex = 0;
				long bestOpportunity = long.MinValue;
				int childCount = FdbLiteTreePage.GetChildCount(page);
				for (int i = 0; i < childCount; i++)
				{
					var child = this.Pager.ReadBlocks(FdbLiteTreePage.GetChild(page, i), this.Pager.Geometry.BlocksPerPage);
					long live = (long) FdbLitePageHeader.GetSubtreeLiveBytes(child);
					long leaves = FdbLitePageHeader.GetLeafCount(child);
					long opportunity = leaves - ((live + idealPageBytes - 1) / idealPageBytes);
					if (opportunity > bestOpportunity)
					{ // strictly greater: ties resolve to the leftmost child, deterministically
						bestOpportunity = opportunity;
						bestIndex = i;
					}
				}
				Contract.Debug.Assert(depth < MaxDepth);
				pathPages[depth] = pageId;
				pathChildren[depth] = bestIndex;
				depth++;
				pageId = FdbLiteTreePage.GetChild(page, bestIndex);
			}

			return VacuumLeafParent(pageId, pathPages, pathChildren, depth, maxInputPages);
		}

		/// <summary>Finds and merges the best sparse run under one leaf-parent, extending across its forward boundary when that frees more.</summary>
		private VacuumOutcome VacuumLeafParent(uint parentId, Span<uint> pathPages, Span<int> pathChildren, int depth, int maxInputPages)
		{
			var parent = ReadPage(parentId);
			int childCount = FdbLiteTreePage.GetChildCount(parent);
			int blocksPerPage = this.Pager.Geometry.BlocksPerPage;

			// maximal stretches of sparse children (header peeks, raw on purpose: a leaf-parent can have
			// hundreds of children and the merge verifies the pages it actually consumes), then the best
			// same-parent run among them; the sizing is EvaluateRun's, shared with the pre-commit arm
			ConsolidationRun? best = null;
			int stretchStart = -1;
			int edgeStretchStart = -1;
			for (int i = 0; i <= childCount; i++)
			{
				bool sparse = i < childCount && IsUnderflowLeaf(this.Pager.ReadBlocks(FdbLiteTreePage.GetChild(parent, i), blocksPerPage));
				if (sparse)
				{
					if (stretchStart < 0) { stretchStart = i; }
					continue;
				}
				if (stretchStart >= 0)
				{
					if (i == childCount) { edgeStretchStart = stretchStart; }
					int last = Math.Min(i - 1, stretchStart + maxInputPages - 1);
					var run = EvaluateRun(parent, pathPages[..depth], pathChildren[..depth], depth, parentId, stretchStart, last);
					if (run is not null && (best is null || run.PagesFreed > best.PagesFreed))
					{
						best = run;
					}
					stretchStart = -1;
				}
			}

			// the cross-parent option: the trailing sparse stretch (if any) extended into the forward
			// neighbor's leading sparse leaves
			if (edgeStretchStart >= 0 && depth > 0)
			{
				var cross = EvaluateCrossParentRun(parent, pathPages, pathChildren, depth, parentId, edgeStretchStart, maxInputPages);
				if (cross is not null && (best is null || cross.Value.Freed > best.PagesFreed))
				{
					return ExecuteCrossParentRun(cross.Value);
				}
			}

			if (best is null)
			{
				return default;
			}
			int freed = MergeConsolidationRun(best);
			return new(best.InputIds.Length, best.InputIds.Length - freed, CrossedParentBoundary: false);
		}

		/// <summary>Everything a cross-parent merge needs, captured against the ORIGINAL tree before any surgery.</summary>
		private readonly record struct CrossParentRun(
			uint[] Path1Pages, int[] Path1Children, int Depth1, uint Parent1Id, int FirstChildIndex,
			uint[] Path2Pages, int[] Path2Children, int Depth2, uint Parent2Id, int ConsumedFromParent2,
			int JoinLevel, byte[] JoinSeparator, uint[] InputIds, int FillCeiling, int Freed);

		/// <summary>Sizes the trailing sparse stretch of one leaf-parent together with the forward neighbor's leading sparse leaves; null when no boundary, no sparse head, or nothing freed.</summary>
		private CrossParentRun? EvaluateCrossParentRun(ReadOnlySpan<byte> parent, Span<uint> pathPages, Span<int> pathChildren, int depth, uint parentId, int firstChild, int maxInputPages)
		{
			// the join: the deepest ancestor where the descent did not take the last child - its next child
			// leads to the forward neighbor, and its separator between the two is the one the merge moves
			int joinLevel = -1;
			for (int level = depth - 1; level >= 0; level--)
			{
				var ancestor = ReadPage(pathPages[level]);
				if (pathChildren[level] < FdbLitePageHeader.GetCellCount(ancestor))
				{
					joinLevel = level;
					break;
				}
			}
			if (joinLevel < 0)
			{ // this leaf-parent holds the tree's highest keys: no forward neighbor
				return null;
			}

			// path to the neighbor: shared up to the join, its NEXT child there, then leftmost all the way down
			var path2Pages = new uint[MaxDepth];
			var path2Children = new int[MaxDepth];
			pathPages[..depth].CopyTo(path2Pages);
			pathChildren[..depth].CopyTo(path2Children);
			path2Children[joinLevel] = pathChildren[joinLevel] + 1;
			int depth2 = joinLevel + 1;
			uint neighborId = FdbLiteTreePage.GetChild(ReadPage(pathPages[joinLevel]), path2Children[joinLevel]);
			while (true)
			{
				var page = ReadPage(neighborId);
				var firstChildPage = ReadPage(FdbLiteTreePage.GetChild(page, 0));
				if (FdbLitePageHeader.GetPageType(firstChildPage) == FdbLitePageType.Leaf)
				{
					break;
				}
				Contract.Debug.Assert(depth2 < MaxDepth);
				path2Pages[depth2] = neighborId;
				path2Children[depth2] = 0;
				depth2++;
				neighborId = FdbLiteTreePage.GetChild(page, 0);
			}

			var neighbor = ReadPage(neighborId);
			int neighborChildren = FdbLiteTreePage.GetChildCount(neighbor);
			int p1ChildCount = FdbLiteTreePage.GetChildCount(parent);
			int p1RunLength = p1ChildCount - firstChild;

			// the neighbor's leading sparse leaves, keeping its LAST leaf whatever happens: the join separator
			// must exist one level down (its cell b), and a fully consumed neighbor would push the surgery
			// into an unbounded ancestor cascade
			int consumable = 0;
			while (consumable < neighborChildren - 1
				&& p1RunLength + consumable < maxInputPages
				&& IsUnderflowLeaf(ReadPage(FdbLiteTreePage.GetChild(neighbor, consumable))))
			{
				consumable++;
			}
			if (consumable == 0)
			{
				return null;
			}

			// combined prefix-adjusted sizing at the run's volatility ceiling
			var inputIds = new uint[p1RunLength + consumable];
			for (int i = 0; i < p1RunLength; i++)
			{
				inputIds[i] = FdbLiteTreePage.GetChild(parent, firstChild + i);
			}
			for (int i = 0; i < consumable; i++)
			{
				inputIds[p1RunLength + i] = FdbLiteTreePage.GetChild(neighbor, i);
			}
			long sumWhole = 0, sumValue = 0;
			int cellTotal = 0;
			byte maxEpisodes = 0;
			foreach (var id in inputIds)
			{
				var page = ReadPage(id);
				int prefixLen = FdbLitePageHeader.GetPrefixLength(page);
				int cells = FdbLitePageHeader.GetCellCount(page);
				for (int i = 0; i < cells; i++)
				{
					var cell = FdbLiteTreePage.ReadLeafCell(page, i);
					sumWhole += prefixLen + cell.KeyLength;
					sumValue += cell.ValueLength;
				}
				cellTotal += cells;
				byte episodes = FdbLitePageHeader.GetVolatilityEpisodes(page);
				if (episodes > maxEpisodes) { maxEpisodes = episodes; }
			}
			if (cellTotal == 0)
			{
				return null;
			}
			var firstPage = ReadPage(inputIds[0]);
			var lastPage = ReadPage(inputIds[^1]);
			int lcp = FdbLiteTreePage.CommonPrefixLength(WholeKeyOf(firstPage, 0), WholeKeyOf(lastPage, FdbLitePageHeader.GetCellCount(lastPage) - 1));
			int fillCeiling = MergedFillCeiling(maxEpisodes, this.Pager.Geometry.PageSize);
			int outputParts = (int) ((LeafRunBytes(cellTotal, sumWhole, sumValue, lcp) + fillCeiling - 1) / fillCeiling);
			if (outputParts >= inputIds.Length)
			{
				return null;
			}

			// the new lower bound of the neighbor's surviving subtree: its separator before the first
			// surviving leaf, captured as an owned copy before any surgery rewrites the page it lives in
			var joinSeparator = FdbLiteTreePage.GetSeparator(neighbor, consumable - 1).ToArray();

			return new(
				pathPages[..depth].ToArray(), pathChildren[..depth].ToArray(), depth, parentId, firstChild,
				path2Pages[..depth2].ToArray(), path2Children[..depth2].ToArray(), depth2, neighborId, consumable,
				joinLevel, joinSeparator, inputIds, fillCeiling, inputIds.Length - outputParts);
		}

		/// <summary>Executes a cross-parent merge: one emission, both leaf-parents rebuilt, and ONE combined bottom-up ascent that rebuilds every ancestor at most once and moves the join separator where the cells went.</summary>
		private VacuumOutcome ExecuteCrossParentRun(in CrossParentRun run)
		{
			var merged = MergeConsolidationCellsStreamed(run.InputIds, run.FillCeiling, caller: nameof(ExecuteCrossParentRun));
			for (int p = 1; p < run.InputIds.Length; p++)
			{
				FreePage(run.InputIds[p]);
			}

			// both leaf-parents rebuilt against their ORIGINAL images, then one combined ascent
			int p1LastChild = FdbLiteTreePage.GetChildCount(ReadPage(run.Parent1Id)) - 1;
			var outcome1 = RebuildInternalReplaceRun(run.Parent1Id, run.FirstChildIndex, p1LastChild, merged);
			var outcome2 = RebuildInternalDropLeadingChildren(run.Parent2Id, run.ConsumedFromParent2);

			AscendPatchPair(
				run.Path1Pages, run.Path1Children, run.Depth1, outcome1,
				run.Path2Pages, run.Path2Children, run.Depth2, outcome2,
				run.JoinLevel, run.JoinSeparator);

			this.CursorLeaf = 0;
			this.AppendLeaf = 0;
			int outputs = 1 + (merged.Siblings?.Count ?? 0);
			return new(run.InputIds.Length, outputs, CrossedParentBoundary: true);
		}

		/// <summary>Rebuilds an internal page without its first <paramref name="dropCount"/> children (their separator cells drop with them; never splits, it only shrinks).</summary>
		private RebuildResult RebuildInternalDropLeadingChildren(uint pageId, int dropCount)
		{
			var page = ReadPage(pageId);
			int cellCount = FdbLitePageHeader.GetCellCount(page);
			Contract.Debug.Requires(dropCount >= 1 && dropCount <= cellCount, "the page must keep at least one child");
			return RebuildInternalDropLeadingChildrenStreamed(pageId, page, cellCount, dropCount);
		}

		/// <summary>Rebuilds the JOIN ancestor: the left-path child carries the merge's parts, the right-path child is the neighbor's rebuilt remainder, and the separator between them moves to <paramref name="joinSeparator"/> - the bound of the cells that stayed behind.</summary>
		private RebuildResult RebuildInternalJoin(uint pageId, int leftChildIndex, in RebuildResult left, in RebuildResult right, byte[] joinSeparator)
		{
			Contract.Debug.Requires(right.Siblings is null, "the right side only shrinks");
			var page = ReadPage(pageId);
			int cellCount = FdbLitePageHeader.GetCellCount(page);
			return RebuildInternalJoinStreamed(pageId, page, cellCount, leftChildIndex, in left, in right, joinSeparator);
		}

		/// <summary>The two-path ascent of a cross-parent merge: each side climbs to (not including) the join level, the join ancestor is rebuilt ONCE with both sides and the moved separator, and one ordinary ascent continues above it.</summary>
		/// <remarks>Every page along both paths is rebuilt at most once, strictly bottom-up, each read against its pre-surgery image - which is what makes the recorded paths safe to use without re-descending between the surgeries.</remarks>
		private void AscendPatchPair(
			uint[] path1Pages, int[] path1Children, int depth1, RebuildResult outcome1,
			uint[] path2Pages, int[] path2Children, int depth2, RebuildResult outcome2,
			int joinLevel, byte[] joinSeparator)
		{
			// no in-place early-out on either side: the join rebuild must observe both sides' final ids, so
			// every level below it rebuilds even when a child kept its id
			for (int level = depth1 - 1; level > joinLevel; level--)
			{
				outcome1 = RebuildInternal(path1Pages[level], path1Children[level], outcome1);
			}
			for (int level = depth2 - 1; level > joinLevel; level--)
			{
				outcome2 = RebuildInternal(path2Pages[level], path2Children[level], outcome2);
			}
			Contract.Debug.Assert(!outcome2.Split, "the right side of a cross-parent merge only shrinks");

			var joined = RebuildInternalJoin(path1Pages[joinLevel], path1Children[joinLevel], in outcome1, in outcome2, joinSeparator);
			AscendPatch(path1Pages.AsSpan(0, joinLevel), path1Children.AsSpan(0, joinLevel), joinLevel - 1, path1Pages[joinLevel], joined);
		}

		#endregion

		#region Spill-on-split opportunity probe...

		/// <summary>Measures, at a leaf split that just happened, whether an adjacent same-parent sibling already in the dirty set could have absorbed the overflow instead (see <see cref="LeafSplitsAbsorbableByDirtySibling"/>).</summary>
		/// <remarks>Counting only: the split stands. Runs before the ascent patches the parent, which is what keeps the pre-split siblings addressable through it. Cost is bounded by the split that just paid for whole page rebuilds: this parses the freshly buffered part pages and at most two sibling images, and only at split moments.</remarks>
		private void ProbeLeafSplitSpillOpportunity(uint parentId, int childIndex, in RebuildResult outcome)
		{
			var parent = ReadPage(parentId);
			int parentCells = FdbLitePageHeader.GetCellCount(parent);
			uint leftId = childIndex > 0 ? FdbLiteTreePage.GetChild(parent, childIndex - 1) : 0;
			uint rightId = childIndex < parentCells ? FdbLiteTreePage.GetChild(parent, childIndex + 1) : 0;

			bool leftDirty = leftId != 0 && this.Dirty.ContainsKey(leftId);
			bool rightDirty = rightId != 0 && this.Dirty.ContainsKey(rightId);
			if (!leftDirty && !rightDirty)
			{
				return;
			}
			this.LeafSplitsWithDirtySibling++;

			// the split run, reassembled from the parts WriteCells just buffered (all of them are in the dirty set)
			var siblings = outcome.Siblings!;
			var parts = new byte[1 + siblings.Count][];
			parts[0] = this.Dirty[outcome.FirstId];
			for (int i = 0; i < siblings.Count; i++)
			{
				parts[i + 1] = this.Dirty[siblings[i].PageId];
			}

			if ((leftDirty && CouldSiblingAbsorbSpill(this.Dirty[leftId], parts, fromLowEdge: true))
			 || (rightDirty && CouldSiblingAbsorbSpill(this.Dirty[rightId], parts, fromLowEdge: false)))
			{
				this.LeafSplitsAbsorbableByDirtySibling++;
			}
		}

		/// <summary>True when moving the minimal run of boundary cells into <paramref name="sibling"/> would have let the rest of the split run fit ONE page, with every run sized against the prefix it would actually strip.</summary>
		/// <param name="sibling">The dirty image of the adjacent recipient leaf</param>
		/// <param name="parts">The split's freshly written part pages, in key order</param>
		/// <param name="fromLowEdge">True to move the run's lowest cells into the left sibling, false to move its highest into the right one</param>
		private bool CouldSiblingAbsorbSpill(byte[] sibling, byte[][] parts, bool fromLowEdge)
		{
			int pageSize = this.Pager.Geometry.PageSize;

			int totalCount = 0;
			long totalWhole = 0, totalValue = 0;
			foreach (var part in parts)
			{
				AccumulateLeafCells(part, ref totalCount, ref totalWhole, ref totalValue);
			}

			var lastPart = parts[^1];
			var runFirst = WholeKeyOf(parts[0], 0);
			var runLast = WholeKeyOf(lastPart, FdbLitePageHeader.GetCellCount(lastPart) - 1);

			// a spill moves boundary cells, so walk the move up from the edge until the remainder fits; the cap
			// bounds the walk far beyond any minimal-plus-margin policy an actual spill arm would use
			int movedCount = 0;
			long movedWhole = 0, movedValue = 0;
			int maxMove = Math.Min(totalCount - 1, 256);
			for (int k = 1; k <= maxMove; k++)
			{
				var (movedPage, movedLocal) = LocateCell(parts, fromLowEdge ? k - 1 : totalCount - k);
				movedCount++;
				movedWhole += FdbLitePageHeader.GetPrefixLength(movedPage) + FdbLiteTreePage.LeafKeyExtent(movedPage, movedLocal).Length;
				movedValue += FdbLiteTreePage.LeafValueExtent(movedPage, movedLocal).Length;

				// the remainder's prefix is what its new boundary key shares with its far end
				var (bPage, bLocal) = LocateCell(parts, fromLowEdge ? k : totalCount - k - 1);
				var boundaryKey = WholeKeyOf(bPage, bLocal);
				int remainderLcp = FdbLiteTreePage.CommonPrefixLength(boundaryKey, fromLowEdge ? runLast : runFirst);
				if (LeafRunBytes(totalCount - k, totalWhole - movedWhole, totalValue - movedValue, remainderLcp) > pageSize)
				{
					continue;
				}

				// minimal move found (a larger one only loads the recipient more): does the recipient take it,
				// sized against ITS post-move prefix, which the arriving foreign keys may shorten?
				int sibCount = 0;
				long sibWhole = 0, sibValue = 0;
				AccumulateLeafCells(sibling, ref sibCount, ref sibWhole, ref sibValue);
				var movedEdge = WholeKeyOf(movedPage, movedLocal);
				var sibFar = fromLowEdge ? WholeKeyOf(sibling, 0) : WholeKeyOf(sibling, FdbLitePageHeader.GetCellCount(sibling) - 1);
				int recipientLcp = FdbLiteTreePage.CommonPrefixLength(movedEdge, sibFar);
				return LeafRunBytes(sibCount + movedCount, sibWhole + movedWhole, sibValue + movedValue, recipientLcp) <= pageSize;
			}
			return false;
		}

		/// <summary>Adds a leaf page's cell count, whole-key bytes (prefix put back) and stored-value bytes to the running totals.</summary>
		private static void AccumulateLeafCells(ReadOnlySpan<byte> page, ref int count, ref long sumWhole, ref long sumValue)
		{
			int cells = FdbLitePageHeader.GetCellCount(page);
			int prefixLen = FdbLitePageHeader.GetPrefixLength(page);
			for (int i = 0; i < cells; i++)
			{
				sumWhole += prefixLen + FdbLiteTreePage.LeafKeyExtent(page, i).Length;
				sumValue += FdbLiteTreePage.LeafValueExtent(page, i).Length;
			}
			count += cells;
		}

		/// <summary>Resolves a run-wide cell index to its part page and the index within it.</summary>
		private static (byte[] Page, int Local) LocateCell(byte[][] parts, int globalIndex)
		{
			Contract.Debug.Requires(globalIndex >= 0);
			int i = 0;
			while (true)
			{ // the caller keeps the index inside the run, so the walk always lands; overrunning the array is an invariant violation and faults
				int count = FdbLitePageHeader.GetCellCount(parts[i]);
				if (globalIndex < count)
				{
					return (parts[i], globalIndex);
				}
				globalIndex -= count;
				i++;
			}
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
			if (!this.Shadow.Contains(pageId) && this.Pager.MarkTouched(pageId) && !FdbLitePageHeader.Verify(page, pageId))
			{ // verification is per FIRST TOUCH of a block since the pager opened (the shared MarkTouched gate the
			  // readers use): a shadowed page was written by this same writer, and a previously touched page was
			  // either verified then or sealed by this process, so re-hashing would check our own bytes against
			  // our own checksum - which is also why this stopped hashing EVERY clean read, as it used to
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
				// Placement bias: leaf pages allocate from the high end of the free space, internal pages from the
				// low end, so that over the churn of copy-on-write the internal tier clusters near the start of the
				// file (see FdbLiteFreeSpaceMap.TryAllocate). A clustered, small internal tier can be warmed into
				// cache on open, keeping tree descent hot.
				bool leaf = FdbLitePageHeader.GetPageType(image) == FdbLitePageType.Leaf;
				id = this.Allocator.AllocatePage(fromHighEnd: leaf);
				this.Shadow.Add(id);
				if (oldPageId != 0)
				{
					this.PageCopies++;
					this.Allocator.Free(oldPageId, (uint) this.Pager.Geometry.BlocksPerPage, this.Generation);
				}
			}

			if (!this.Dirty.TryGetValue(id, out var slot))
			{ // one buffer per dirty page, taken once and reused for every later mutation of that page
				slot = RentPageBuffer();
				this.Dirty.Add(id, slot);
			}
			image.CopyTo(slot);
			return id;
		}

		/// <summary>Sums of one subtree's aggregate block, as carried up the stamp pass.</summary>
		private readonly record struct SubtreeAggregates(ulong Entries, ulong KeyBytes, ulong ValueBytes, ulong LeafLiveBytes, uint Leaves, ulong ExtentBlocks)
		{
			public static SubtreeAggregates operator +(SubtreeAggregates a, SubtreeAggregates b)
				=> new(a.Entries + b.Entries, a.KeyBytes + b.KeyBytes, a.ValueBytes + b.ValueBytes, a.LeafLiveBytes + b.LeafLiveBytes, a.Leaves + b.Leaves, a.ExtentBlocks + b.ExtentBlocks);
		}

		/// <summary>Stamps the subtree aggregates into every dirty INTERNAL page, bottom-up, before the images are sealed.</summary>
		/// <remarks>
		/// <para>This is free of extra page writes by the dirty-chain invariant: every page whose numbers changed has its whole ancestor chain in this generation's dirty set, so the pass recurses over dirty pages only and reads ONE header per clean child (a clean leaf's live bytes derive from its v1 fields, a clean internal page carries its stored sums, and both are exact because the same pass stamped them when THEY were last dirty).</para>
		/// <para>Dirty leaves are already exact: the run writer and the in-place mutation paths maintain their aggregate block as they go.</para>
		/// </remarks>
		private void StampAggregates()
		{
			Contract.Debug.Assert(this.Root != 0 && this.Dirty.ContainsKey(this.Root), "a non-empty dirty set without a dirty root breaks the dirty-chain invariant");
			int visited = 0;
			StampSubtree(this.Root, ref visited);
			Contract.Debug.Assert(visited == this.Dirty.Count, "a dirty page is not reachable through dirty ancestors: the dirty-chain invariant is broken and its aggregates would go stale");
		}

		private SubtreeAggregates StampSubtree(uint pageId, ref int visited)
		{
			if (this.Dirty.TryGetValue(pageId, out var buffered))
			{
				visited++;
				var image = buffered.AsSpan();
				if (FdbLitePageHeader.GetPageType(image) == FdbLitePageType.Leaf)
				{
					return new(FdbLitePageHeader.GetEntryCount(image), FdbLitePageHeader.GetLogicalKeyBytes(image), FdbLitePageHeader.GetLogicalValueBytes(image), (ulong) FdbLiteTreePage.LeafLiveBytes(image), 1, FdbLitePageHeader.GetExtentBlocks(image));
				}

				var sum = default(SubtreeAggregates);
				int children = FdbLiteTreePage.GetChildCount(image);
				for (int i = 0; i < children; i++)
				{
					sum += StampSubtree(FdbLiteTreePage.GetChild(image, i), ref visited);
				}
				FdbLitePageHeader.SetEntryCount(image, sum.Entries);
				FdbLitePageHeader.SetLogicalKeyBytes(image, sum.KeyBytes);
				FdbLitePageHeader.SetLogicalValueBytes(image, sum.ValueBytes);
				FdbLitePageHeader.SetSubtreeLiveBytes(image, sum.LeafLiveBytes);
				FdbLitePageHeader.SetLeafCount(image, sum.Leaves);
				FdbLitePageHeader.SetExtentBlocks(image, sum.ExtentBlocks);
				return sum;
			}

			// a clean child: one header read of its stored aggregates, no verify (this is the hot part of the
			// pass, and the page's own first-touch verification already guards the paths that mutate it)
			var page = this.Pager.ReadBlocks(pageId, this.Pager.Geometry.BlocksPerPage);
			if (FdbLitePageHeader.GetPageType(page) == FdbLitePageType.Leaf)
			{
				return new(FdbLitePageHeader.GetEntryCount(page), FdbLitePageHeader.GetLogicalKeyBytes(page), FdbLitePageHeader.GetLogicalValueBytes(page), (ulong) FdbLiteTreePage.LeafLiveBytes(page), 1, FdbLitePageHeader.GetExtentBlocks(page));
			}
			return new(FdbLitePageHeader.GetEntryCount(page), FdbLitePageHeader.GetLogicalKeyBytes(page), FdbLitePageHeader.GetLogicalValueBytes(page), FdbLitePageHeader.GetSubtreeLiveBytes(page), FdbLitePageHeader.GetLeafCount(page), FdbLitePageHeader.GetExtentBlocks(page));
		}

		/// <summary>Seals and writes every page image this generation is holding, then releases them.</summary>
		/// <remarks>Called by the engine before the commit protocol's first flush barrier, and wherever the writer must let a raw pager read observe its work. Ordering of the two commit barriers is unaffected: this only decides WHEN the data blocks are handed over, never that they are handed over after the header.</remarks>
		public void FlushDirtyPages()
		{
			if (this.Dirty.Count == 0)
			{
				return;
			}

			// subtree aggregates go into the dirty internal pages now, while every page whose numbers changed
			// is still in hand: after this, the images are final and can be sealed
			StampAggregates();

			// ascending page order turns the dirty set into as few forward runs as the allocation pattern allows
			var ids = new uint[this.Dirty.Count];
			this.Dirty.Keys.CopyTo(ids, 0);
			Array.Sort(ids);

			foreach (var id in ids)
			{
				var image = this.Dirty[id];
				// the stamp says which generation PUBLISHED the page, and a verbatim-copied image still carries
				// its source's; every dirty image passes through here exactly once, so this is where it is true
				FdbLitePageHeader.SetGeneration(image, this.Generation);
				FdbLitePageHeader.Seal(image, id);
				this.Pager.WriteBlocks(id, image);
				// this process computed these bytes, so their first READ needs no checksum verification:
				// without this, a build-then-scan in one open re-hashes its whole store (measured +2.2 us/op
				// on value scans - the 2026-08-06 range-scan regression)
				this.Pager.MarkTouched(id);
				this.PagesWritten++;
				// the pager copies the bytes out synchronously, so the image buffer is free the moment it returns
				ReturnPageBuffer(image);
			}
			this.Dirty.Clear();
			this.CursorBufferId = 0;
			this.CursorBuffer = null;
		}

		#endregion

	}

}
