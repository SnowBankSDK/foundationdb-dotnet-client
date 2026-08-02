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
	using System.IO.Hashing;

	/// <summary>Type of a formatted page (value-extent blocks are raw payload and carry no header)</summary>
	public enum FdbLitePageType : byte
	{
		None = 0,
		Leaf = 1,
		Internal = 2,
		FreeList = 3,
	}

	/// <summary>Span accessors for the 128-byte universal header at the start of every formatted page.</summary>
	/// <remarks>
	/// <para>Layout (little-endian). The v1 block, bytes 0..32: checksum u64 (XxHash3-64 over the page with this field zeroed, seeded by the page's first block id), generation u64 (commit generation the page was written at), type u8, encoding u8 (payload-transform door, Plain=0 in v1), cell count u16, value-area offset u16 (the down-growing heap), prefix length u16, key-area length u16 (bytes occupied by the up-growing heap), wasted-bytes u16 (dead room booked by in-place mutations), then 4 bytes zero.</para>
	/// <para>The aggregate block, bytes 32..69: entry count u64, logical key bytes u64, logical value bytes u64, subtree live bytes u64, leaf count u32, volatility episodes u8. A LEAF stores its own exact totals (entry count = its live cells, logical key bytes = whole key lengths with the page prefix re-expanded, logical value bytes = full value lengths where an extent cell contributes its total extent length rather than its descriptor, leaf count = 1, subtree live bytes = 0 since its own fill derives from the v1 fields); an INTERNAL page stores its subtree's sums, maintained for free by the dirty-chain invariant (anything that changes a leaf dirties its whole ancestor chain, so a stamp pass at flush time reaches every stale sum without one extra page write). Aggregates live in pages and pages are copy-on-write versioned, so every retained generation carries its own consistent totals. An <i>entry</i> is a cell visible in the generation whose root the descent started from; the v1 tree stores no tombstones, and if a future layer introduces them they count in fill-oriented live bytes (they occupy page room) and NOT in the entry/logical aggregates.</para>
	/// <para>Bytes 69..128 are reserved and MUST be zero: asserted at <see cref="Format"/>, checked at <see cref="Verify"/>, so a stale writer scribbling there is caught by an explicit check rather than discovered by a future feature reading garbage.</para>
	/// <para>The block-id seed makes a page written to the wrong location fail verification; the generation stamp lets a lock-free inspector detect a page reused under its feet.</para>
	/// </remarks>
	public static class FdbLitePageHeader
	{

		/// <summary>Size of the universal page header, in bytes</summary>
		/// <remarks>
		/// <para>Two cache lines: the search-hot v1 fields and the aggregate block sit in the first. It does NOT align the slot directory: the variable-length page prefix sits between this header and the slots, so what keeps that u16 array 2-byte aligned is the prefix being padded to an even length, and nothing else. Removing that pad as redundant would make the alignment of the array depend on each page's prefix.</para>
		/// <para>The 59-byte reserve exists for named future occupants (feature flags, encryption-at-rest nonce/tag material: an AES-GCM nonce plus tag alone is 28 bytes) and MUST stay zero until one claims its bytes. Pages are zeroed on format, so the reserve costs nothing today, and that is what makes its bytes safe to give a meaning later.</para>
		/// </remarks>
		public const int Size = 128;

		/// <summary>The only payload encoding defined in v1</summary>
		public const byte EncodingPlain = 0;

		private const int ChecksumOffset = 0;
		private const int GenerationOffset = 8;
		private const int TypeOffset = 16;
		private const int EncodingOffset = 17;
		private const int CellCountOffset = 18;
		private const int CellAreaOffset = 20;
		private const int PrefixLengthOffset = 22;
		private const int KeyAreaLengthOffset = 24;

		private const int WastedBytesOffset = 26;

		private const int SlotCapacityOffset = 28;

		private const int EntryCountOffset = 32;
		private const int LogicalKeyBytesOffset = 40;
		private const int LogicalValueBytesOffset = 48;
		private const int SubtreeLiveBytesOffset = 56;
		private const int LeafCountOffset = 64;
		private const int VolatilityEpisodesOffset = 68;

		private const int ReservedOffset = 69;
		private const int ReservedLength = Size - ReservedOffset;

		static FdbLitePageHeader()
		{
			// a rearrangement that pushes a field past the header-size constant must fail on first touch of the
			// type, not compile-and-truncate into the prefix region that follows the header
			Contract.Requires(VolatilityEpisodesOffset + 1 <= Size && ReservedOffset + ReservedLength == Size, "aggregate fields overflow the page-header size constant");
		}

		public static ulong GetChecksum(ReadOnlySpan<byte> page) => BinaryPrimitives.ReadUInt64LittleEndian(page[ChecksumOffset..]);

		public static void SetChecksum(Span<byte> page, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(page[ChecksumOffset..], value);

		public static ulong GetGeneration(ReadOnlySpan<byte> page) => BinaryPrimitives.ReadUInt64LittleEndian(page[GenerationOffset..]);

		public static void SetGeneration(Span<byte> page, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(page[GenerationOffset..], value);

		public static FdbLitePageType GetPageType(ReadOnlySpan<byte> page) => (FdbLitePageType) page[TypeOffset];

		public static void SetPageType(Span<byte> page, FdbLitePageType value) => page[TypeOffset] = (byte) value;

		public static byte GetEncoding(ReadOnlySpan<byte> page) => page[EncodingOffset];

		public static void SetEncoding(Span<byte> page, byte value) => page[EncodingOffset] = value;

		public static ushort GetCellCount(ReadOnlySpan<byte> page) => BinaryPrimitives.ReadUInt16LittleEndian(page[CellCountOffset..]);

		public static void SetCellCount(Span<byte> page, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(page[CellCountOffset..], value);

		public static ushort GetCellAreaOffset(ReadOnlySpan<byte> page) => BinaryPrimitives.ReadUInt16LittleEndian(page[CellAreaOffset..]);

		public static void SetCellAreaOffset(Span<byte> page, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(page[CellAreaOffset..], value);

		/// <summary>Bytes of the key heap that are occupied - including dead entries booked in <see cref="GetWastedBytes"/> - measured from its base rather than from the page.</summary>
		/// <remarks>
		/// <para>A LENGTH, not an offset, and deliberately so: the key heap's base moves whenever the slot directory grows, and a length survives that move untouched while an absolute end would have to be rewritten.</para>
		/// <para>The leaf holds two heaps growing towards each other and so needs two frontiers: this one and <see cref="GetCellAreaOffset"/>, the down-growing value heap. Neither is derivable from the slot directory, because the heaps are packed in insertion order rather than key order; keeping them in key order would cost a memmove per insert, which is the trade the layout deliberately refuses.</para>
		/// </remarks>
		/// <summary>Slots the leaf's directory has RESERVED, which may exceed its cell count. Zero means "exactly the cell count", which is what a page built by a rebuild carries.</summary>
		/// <remarks>Lives in the padding between the wasted-bytes and aggregate fields, NOT in the reserved block at the end of the header, which <see cref="Verify"/> requires to stay zero.</remarks>
		public static ushort GetSlotCapacity(ReadOnlySpan<byte> page) => BinaryPrimitives.ReadUInt16LittleEndian(page[SlotCapacityOffset..]);

		public static void SetSlotCapacity(Span<byte> page, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(page[SlotCapacityOffset..], value);

		public static ushort GetKeyAreaLength(ReadOnlySpan<byte> page) => BinaryPrimitives.ReadUInt16LittleEndian(page[KeyAreaLengthOffset..]);

		public static void SetKeyAreaLength(Span<byte> page, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(page[KeyAreaLengthOffset..], value);

		/// <summary>Bytes inside this page that no cell points at any more, and that a repack would reclaim.</summary>
		/// <remarks>
		/// <para>Zero for any page written by the rebuild path, which is compact by construction. It becomes
		/// non-zero only where a mutation deliberately leaves a gap rather than paying O(cells) to close it:
		/// a value replaced by a SHORTER one keeps its slot and the slack is recorded here, a relocated value
		/// leaves its old slot behind, and an in-place delete leaves its entry and value as booked holes.</para>
		/// <para>The counter is booked by those paths but consumed by nothing yet: the rebuild path reclaims
		/// waste as a side effect without reading it. It occupies two of the six header bytes FL-36 reserved
		/// as zero - backward-compatible for readers (an older page reads as having no waste, which is exactly
		/// true of it), but a format commitment all the same.</para>
		/// </remarks>
		public static ushort GetWastedBytes(ReadOnlySpan<byte> page) => BinaryPrimitives.ReadUInt16LittleEndian(page[WastedBytesOffset..]);

		public static void SetWastedBytes(Span<byte> page, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(page[WastedBytesOffset..], value);

		/// <summary>Length of the key prefix common to every key on this page, stored once between the header and the slot directory.</summary>
		/// <remarks>Zero means no prefix is stripped, which is the layout's degenerate case and behaves exactly as an unstripped page. The prefix bytes themselves follow the header (and the leftmost-child field on an internal page), so the slot directory starts that much further in.</remarks>
		public static ushort GetPrefixLength(ReadOnlySpan<byte> page) => BinaryPrimitives.ReadUInt16LittleEndian(page[PrefixLengthOffset..]);

		public static void SetPrefixLength(Span<byte> page, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(page[PrefixLengthOffset..], value);

		/// <summary>Entries in this page's subtree: a leaf's own live cell count, an internal page's subtree sum.</summary>
		public static ulong GetEntryCount(ReadOnlySpan<byte> page) => BinaryPrimitives.ReadUInt64LittleEndian(page[EntryCountOffset..]);

		public static void SetEntryCount(Span<byte> page, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(page[EntryCountOffset..], value);

		/// <summary>Sum of WHOLE key lengths (the page prefix re-expanded) in this page's subtree: a leaf's own total, an internal page's subtree sum.</summary>
		public static ulong GetLogicalKeyBytes(ReadOnlySpan<byte> page) => BinaryPrimitives.ReadUInt64LittleEndian(page[LogicalKeyBytesOffset..]);

		public static void SetLogicalKeyBytes(Span<byte> page, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(page[LogicalKeyBytesOffset..], value);

		/// <summary>Sum of FULL value lengths in this page's subtree (an extent cell contributes its total extent length, not its descriptor): a leaf's own total, an internal page's subtree sum.</summary>
		/// <remarks>u64 on purpose: this counts extent contents, so a subtree's total is bounded by nothing smaller than the file's data plus extents, and a u32 saturates at 4 GiB, which is a realistic single subtree.</remarks>
		public static ulong GetLogicalValueBytes(ReadOnlySpan<byte> page) => BinaryPrimitives.ReadUInt64LittleEndian(page[LogicalValueBytesOffset..]);

		public static void SetLogicalValueBytes(Span<byte> page, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(page[LogicalValueBytesOffset..], value);

		/// <summary>Sum of the fill-oriented live bytes of the LEAVES in this page's subtree (internal pages only; a leaf stores 0, since its own live bytes derive from its v1 fields).</summary>
		public static ulong GetSubtreeLiveBytes(ReadOnlySpan<byte> page) => BinaryPrimitives.ReadUInt64LittleEndian(page[SubtreeLiveBytesOffset..]);

		public static void SetSubtreeLiveBytes(Span<byte> page, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(page[SubtreeLiveBytesOffset..], value);

		/// <summary>Leaves in this page's subtree: 1 for a leaf, the subtree sum for an internal page.</summary>
		public static uint GetLeafCount(ReadOnlySpan<byte> page) => BinaryPrimitives.ReadUInt32LittleEndian(page[LeafCountOffset..]);

		public static void SetLeafCount(Span<byte> page, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(page[LeafCountOffset..], value);

		/// <summary>Saturating count of POST-FILL mutation episodes of this page: bumped at most once per generation that mutates it, reset when its cells are repacked into a merged output.</summary>
		/// <remarks>
		/// <para>The signal is HOW OFTEN, never WHEN: a write-once page from yesterday equals one from last month. Deletes, in-place value mutations and interior inserts (a key strictly below the receiving leaf's own maximum: the leaf's right edge is its append edge, which holds for any number of append-shaped subspaces at once) are episodes; append-edge growth is a page FILLING rather than mutating and must reach its packed state at count zero; whole-page death counts nothing.</para>
		/// <para>Reset-on-repack is part of the definition, not an optimization: counted since birth, a one-time bulk load brands its leaves volatile forever (its own interior inserts are episodes) and the write-once shape the count exists to identify can never be packed full.</para>
		/// <para>Consumers quantize at read (0 = never changed since filled, 1 = changed rarely, 2+ = volatile); storing the byte rather than the class keeps re-bucketing free of a format change.</para>
		/// </remarks>
		public static byte GetVolatilityEpisodes(ReadOnlySpan<byte> page) => page[VolatilityEpisodesOffset];

		public static void SetVolatilityEpisodes(Span<byte> page, byte value) => page[VolatilityEpisodesOffset] = value;

		/// <summary>Computes the page checksum: XxHash3-64 over everything AFTER the checksum field, seeded by the page's first block id.</summary>
		/// <remarks>
		/// <para>The checksum field is EXCLUDED from its own input rather than zeroed inside it, which makes the hashed bytes one contiguous run and the whole thing a single static call. The previous form fed 8 zero bytes and then the rest through a streaming <c>XxHash3</c>, and <c>XxHash3</c> is a CLASS: that allocated one instance per page sealed and per page verified, which measured at 466 MB on one benchmark sweep. A constant zero prefix contributes no integrity, so dropping it costs nothing but the constant.</para>
		/// <para>The seed is what ties a page to its location, so a page written to the wrong block still fails verification.</para>
		/// <para>FORMAT: this changes the checksum VALUES. A store written by an older build fails <see cref="Verify"/> against this one.</para>
		/// </remarks>
		public static ulong ComputeChecksum(ReadOnlySpan<byte> page, uint firstBlockId)
		{
			Contract.Debug.Requires(page.Length >= Size);
			return XxHash3.HashToUInt64(page[GenerationOffset..], unchecked((long) firstBlockId));
		}

		/// <summary>Writes the checksum of a fully-built page into its header (the last step before the page goes to the pager).</summary>
		public static void Seal(Span<byte> page, uint firstBlockId) => SetChecksum(page, ComputeChecksum(page, firstBlockId));

		/// <summary>Verifies a page's checksum against its location, and that its reserved bytes are still zero.</summary>
		public static bool Verify(ReadOnlySpan<byte> page, uint firstBlockId)
			=> GetChecksum(page) == ComputeChecksum(page, firstBlockId)
			&& page.Slice(ReservedOffset, ReservedLength).IndexOfAnyExcept((byte) 0) < 0;

		/// <summary>Initializes a fresh page: zeroes the span, stamps type, encoding, generation, and an empty cell area at the page end.</summary>
		public static void Format(Span<byte> page, FdbLitePageType type, ulong generation)
		{
			Contract.Debug.Requires(page.Length is > Size and <= 65536);
			page.Clear();
			SetPageType(page, type);
			SetEncoding(page, EncodingPlain);
			SetGeneration(page, generation);
			// the cell heap is empty: it starts at the end of the page (cells will grow down from there).
			// The cell-area offset stores the offset of the LOWEST allocated cell byte, and an empty page
			// stores 0 meaning "no cell allocated yet" - a full 64 KiB page's end offset (65536) does not
			// fit a u16, so "empty" cannot be encoded as the page length itself.
			SetCellAreaOffset(page, 0);
			Contract.Debug.Assert(page.Slice(ReservedOffset, ReservedLength).IndexOfAnyExcept((byte) 0) < 0, "a freshly formatted page must leave its reserved bytes zero");
		}

	}

}
