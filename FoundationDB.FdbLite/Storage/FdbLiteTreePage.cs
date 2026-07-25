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

	/// <summary>Slotted-page layout shared by leaf and internal tree pages.</summary>
	/// <remarks>
	/// <para>After the universal header: internal pages carry the leftmost child id (u32, which does not fit a u16 header field), then the page prefix, then the slot directory (u16, in key order, binary-searchable) growing up.</para>
	/// <para><b>A leaf holds THREE regions</b>: <c>[header][prefix][slots -&gt;][key heap -&gt;][ free ][&lt;- value heap]</c>. A binary search reads the slot directory and the key heap, both contiguous and dense, and never touches the value heap until there is a hit. An interleaved key/value heap instead scatters ~11 probes of a 32 KiB leaf across most of the 4 KiB OS pages behind it.</para>
	/// <para><b>Leaf key-heap entry</b>: keyLen u16, key suffix bytes, value offset u16, value length u16, flags u8 (bit 0 = the value is an extent reference). <b>Value heap</b>: payload bytes only, or the extent descriptor. Per cell that is 9 bytes of overhead including the slot, against the 9 of the interleaved layout it replaces, so the locality is bought for nothing.</para>
	/// <para><b>Slots are key-heap-relative.</b> The directory and the key heap grow towards the same end, so the heap's base moves whenever a slot is added; relative slots make that a memmove that invalidates nothing. Key-only pages (an index, whose values are empty) are all key heap, which is optimal for space and the worst case for that shift.</para>
	/// <para>Internal pages keep one contiguous cell heap growing down from the page end, since a separator has no value to separate out.</para>
	/// <para><b>Internal cell</b>: child page id u32, keyLen u16, separator key bytes.</para>
	/// <para><b>Internal cell</b>: child page id u32, keyLen u16, separator key bytes. A page with N cells has N separators and N+1 children; child i (i &gt;= 1) covers keys &gt;= separator i-1, child 0 covers everything below separator 0.</para>
	/// <para>Every page image is written compact (mutations rebuild the page), so there is no dead-cell bookkeeping anywhere.</para>
	/// </remarks>
	internal static class FdbLiteTreePage
	{

		/// <summary>Largest legal key (the fdb limit); the 16 KiB page floor guarantees one max-size cell always fits inline</summary>
		public const int MaxKeyLength = 10_000;

		/// <summary>Leaf-cell flag: the value is an extent reference instead of inline bytes</summary>
		public const byte FlagValueIsExtent = 0x01;

		private const int LeftmostChildOffset = FdbLitePageHeader.Size;

		/// <summary>Bytes the page prefix occupies, which is its length rounded up to an even number.</summary>
		/// <remarks>The pad is what keeps the u16 slot directory 2-byte aligned. The prefix is variable-length and sits in front of the slots, so without it the alignment of the array a binary search probes would depend on each page's data. The header being a multiple of 8 does not achieve this on its own.</remarks>
		public static int PrefixRegionSize(ReadOnlySpan<byte> page) => (FdbLitePageHeader.GetPrefixLength(page) + 1) & ~1;

		/// <summary>The key prefix every key on this page shares, stripped from the stored suffixes (empty when the page stores whole keys).</summary>
		public static ReadOnlySpan<byte> GetPagePrefix(ReadOnlySpan<byte> page, bool isInternal)
			=> page.Slice(FdbLitePageHeader.Size + (isInternal ? 4 : 0), FdbLitePageHeader.GetPrefixLength(page));

		/// <summary>Offset of the slot directory, past the header and the page prefix</summary>
		public static int SlotsOffset(ReadOnlySpan<byte> page, bool isInternal) => SlotsOffset(isInternal, PrefixRegionSize(page));

		/// <summary>Offset of the slot directory for a page whose prefix region is <paramref name="prefixRegionSize"/> bytes, for sizing a page that does not exist yet.</summary>
		public static int SlotsOffset(bool isInternal, int prefixRegionSize) => FdbLitePageHeader.Size + (isInternal ? 4 : 0) + prefixRegionSize;

		/// <summary>Fixed per-cell overhead outside the key/value bytes: slot, key length, value offset, value length, flags</summary>
		public static int LeafCellOverhead => 2 + 2 + 2 + 2 + 1;

		public static int InternalCellOverhead => 4 + 2 + 2;

		#region Slots...

		public static ushort GetSlot(ReadOnlySpan<byte> page, bool isInternal, int index)
			=> BinaryPrimitives.ReadUInt16LittleEndian(page[(SlotsOffset(page, isInternal) + (index * 2))..]);

		public static void SetSlot(Span<byte> page, bool isInternal, int index, ushort cellOffset)
			=> BinaryPrimitives.WriteUInt16LittleEndian(page[(SlotsOffset(page, isInternal) + (index * 2))..], cellOffset);

		#endregion

		#region Internal pages...

		public static uint GetLeftmostChild(ReadOnlySpan<byte> page) => BinaryPrimitives.ReadUInt32LittleEndian(page[LeftmostChildOffset..]);

		public static void SetLeftmostChild(Span<byte> page, uint child) => BinaryPrimitives.WriteUInt32LittleEndian(page[LeftmostChildOffset..], child);

		/// <summary>Number of children of an internal page (cell count + 1)</summary>
		public static int GetChildCount(ReadOnlySpan<byte> page) => FdbLitePageHeader.GetCellCount(page) + 1;

		/// <summary>Child page id by child index (0 = leftmost, i &gt;= 1 = cell i-1's child)</summary>
		public static uint GetChild(ReadOnlySpan<byte> page, int childIndex)
		{
			if (childIndex == 0)
			{
				return GetLeftmostChild(page);
			}
			int off = GetSlot(page, isInternal: true, childIndex - 1);
			return BinaryPrimitives.ReadUInt32LittleEndian(page[off..]);
		}

		/// <summary>Separator key of internal cell <paramref name="cellIndex"/></summary>
		public static ReadOnlySpan<byte> GetSeparator(ReadOnlySpan<byte> page, int cellIndex)
		{
			int off = GetSlot(page, isInternal: true, cellIndex);
			int keyLen = BinaryPrimitives.ReadUInt16LittleEndian(page[(off + 4)..]);
			return page.Slice(off + 6, keyLen);
		}

		/// <summary>Raw bytes of internal cell <paramref name="cellIndex"/></summary>
		public static ReadOnlySpan<byte> GetInternalCell(ReadOnlySpan<byte> page, int cellIndex)
		{
			int off = GetSlot(page, isInternal: true, cellIndex);
			int keyLen = BinaryPrimitives.ReadUInt16LittleEndian(page[(off + 4)..]);
			return page.Slice(off, 6 + keyLen);
		}

		/// <summary>Offset and length of internal cell <paramref name="cellIndex"/> within the page</summary>
		public static (int Offset, int Length) GetInternalCellExtent(ReadOnlySpan<byte> page, int cellIndex)
		{
			int off = GetSlot(page, isInternal: true, cellIndex);
			int keyLen = BinaryPrimitives.ReadUInt16LittleEndian(page[(off + 4)..]);
			return (off, 6 + keyLen);
		}

		/// <summary>Index of the child covering <paramref name="key"/>: one past the last separator at or below the key.</summary>
		public static int FindChildIndex(ReadOnlySpan<byte> page, ReadOnlySpan<byte> key)
		{
			int lo = 0, hi = FdbLitePageHeader.GetCellCount(page);
			while (lo < hi)
			{
				int mid = (lo + hi) >> 1;
				if (GetSeparator(page, mid).SequenceCompareTo(key) <= 0) { lo = mid + 1; } else { hi = mid; }
			}
			return lo;
		}

		/// <summary>Builds the bytes of an internal cell into <paramref name="scratch"/> and returns the written slice.</summary>
		public static ReadOnlySpan<byte> BuildInternalCell(Span<byte> scratch, uint child, ReadOnlySpan<byte> separator)
		{
			BinaryPrimitives.WriteUInt32LittleEndian(scratch, child);
			BinaryPrimitives.WriteUInt16LittleEndian(scratch[4..], (ushort) separator.Length);
			separator.CopyTo(scratch[6..]);
			return scratch[..(6 + separator.Length)];
		}

		/// <summary>Rewrites the child id of an internal cell held in a scratch buffer.</summary>
		public static void PatchInternalCellChild(Span<byte> cell, uint child) => BinaryPrimitives.WriteUInt32LittleEndian(cell, child);

		#endregion

		#region Leaf pages...

		// A leaf cell lives in two regions. The slot points at the key-heap entry, which is
		//     [keyLen u16][key suffix bytes][valueOffset u16][valueLen u16][flags u8]
		// and the value heap holds nothing but payload bytes at valueOffset. A binary search reads the slot array and
		// the key heap and never touches the value heap, which is the whole point of the layout.

		/// <summary>Where the key heap starts: immediately after the slot directory, whose size depends on the cell count.</summary>
		/// <remarks><b>Slots are key-heap-RELATIVE, so the key region can move freely.</b> That is the invariant, not an implementation detail of insertion: the directory and the key heap grow towards the same end of the page, so the base shifts whenever a slot is added, and relative slots mean such a move costs one memmove and leaves every slot valid. It is also what would make a reserve, a compaction or a defragmentation a memmove rather than a rewrite. Do NOT "simplify" these to absolute page offsets.</remarks>
		public static int LeafKeyBase(ReadOnlySpan<byte> page) => SlotsOffset(page, isInternal: false) + (FdbLitePageHeader.GetCellCount(page) * 2);

		/// <summary>Absolute offset of cell <paramref name="cellIndex"/>'s key-heap entry</summary>
		private static int LeafEntry(ReadOnlySpan<byte> page, int cellIndex) => LeafKeyBase(page) + GetSlot(page, isInternal: false, cellIndex);

		/// <summary>Offset of the value-offset field inside a key-heap entry starting at <paramref name="entry"/></summary>
		private static int ValueOffsetField(ReadOnlySpan<byte> page, int entry) => entry + 2 + BinaryPrimitives.ReadUInt16LittleEndian(page[entry..]);

		/// <summary>Key of leaf cell <paramref name="cellIndex"/>, as stored: the suffix after the page prefix, which is the whole key while no prefix is stripped.</summary>
		public static ReadOnlySpan<byte> GetLeafKey(ReadOnlySpan<byte> page, int cellIndex)
		{
			int entry = LeafEntry(page, cellIndex);
			return page.Slice(entry + 2, BinaryPrimitives.ReadUInt16LittleEndian(page[entry..]));
		}

		/// <summary>Where this cell's stored key sits in the key heap, and how long it is</summary>
		public static (int Offset, int Length) LeafKeyExtent(ReadOnlySpan<byte> page, int cellIndex)
		{
			int entry = LeafEntry(page, cellIndex);
			return (entry + 2, BinaryPrimitives.ReadUInt16LittleEndian(page[entry..]));
		}

		public static byte GetLeafFlags(ReadOnlySpan<byte> page, int cellIndex) => page[ValueOffsetField(page, LeafEntry(page, cellIndex)) + 4];

		/// <summary>Where this cell's value bytes sit in the value heap, and how many there are</summary>
		public static (int Offset, int Length) LeafValueExtent(ReadOnlySpan<byte> page, int cellIndex)
		{
			int f = ValueOffsetField(page, LeafEntry(page, cellIndex));
			return (BinaryPrimitives.ReadUInt16LittleEndian(page[f..]), BinaryPrimitives.ReadUInt16LittleEndian(page[(f + 2)..]));
		}

		/// <summary>Inline value bytes of leaf cell <paramref name="cellIndex"/> (the caller checked the extent flag)</summary>
		public static ReadOnlySpan<byte> GetLeafInlineValue(ReadOnlySpan<byte> page, int cellIndex)
		{
			Contract.Debug.Requires((GetLeafFlags(page, cellIndex) & FlagValueIsExtent) == 0);
			var (off, len) = LeafValueExtent(page, cellIndex);
			return page.Slice(off, len);
		}

		/// <summary>Size of the extent descriptor, which occupies the value heap in place of inline bytes</summary>
		public const int ExtentDescriptorSize = 18;

		/// <summary>Extent descriptor of leaf cell <paramref name="cellIndex"/> (the caller checked the extent flag)</summary>
		public static (uint StartBlock, ushort BlockCount, uint TotalLength, ulong Checksum) GetLeafExtentDescriptor(ReadOnlySpan<byte> page, int cellIndex)
		{
			Contract.Debug.Requires((GetLeafFlags(page, cellIndex) & FlagValueIsExtent) != 0);
			var (off, _) = LeafValueExtent(page, cellIndex);
			var d = page[off..];
			return (
				BinaryPrimitives.ReadUInt32LittleEndian(d),
				BinaryPrimitives.ReadUInt16LittleEndian(d[4..]),
				BinaryPrimitives.ReadUInt32LittleEndian(d[6..]),
				BinaryPrimitives.ReadUInt64LittleEndian(d[10..])
			);
		}

		/// <summary>Value bytes of leaf cell <paramref name="cellIndex"/> exactly as stored, inline payload or extent descriptor alike, for gathering into a rebuilt page.</summary>
		public static ReadOnlySpan<byte> GetLeafStoredValue(ReadOnlySpan<byte> page, int cellIndex)
		{
			var (off, len) = LeafValueExtent(page, cellIndex);
			return page.Slice(off, len);
		}

		/// <summary>Builds the extent descriptor that stands in for a value too large to inline.</summary>
		public static ReadOnlySpan<byte> BuildExtentDescriptor(Span<byte> scratch, uint startBlock, ushort blockCount, uint totalLength, ulong checksum)
		{
			BinaryPrimitives.WriteUInt32LittleEndian(scratch, startBlock);
			BinaryPrimitives.WriteUInt16LittleEndian(scratch[4..], blockCount);
			BinaryPrimitives.WriteUInt32LittleEndian(scratch[6..], totalLength);
			BinaryPrimitives.WriteUInt64LittleEndian(scratch[10..], checksum);
			return scratch[..ExtentDescriptorSize];
		}

		/// <summary>Writes one key-heap entry and its value-heap bytes into a page, and returns the entry's offset.</summary>
		/// <remarks>Callers own the frontiers: <paramref name="keyAt"/> is where the entry goes and <paramref name="valueAt"/> is where the payload goes, so this can serve both the append-into-free-space insert and the sequential rebuild.</remarks>
		private static void WriteLeafEntry(Span<byte> page, int keyAt, int valueAt, ReadOnlySpan<byte> keySuffix, ReadOnlySpan<byte> storedValue, byte flags)
		{
			BinaryPrimitives.WriteUInt16LittleEndian(page[keyAt..], (ushort) keySuffix.Length);
			keySuffix.CopyTo(page[(keyAt + 2)..]);
			int f = keyAt + 2 + keySuffix.Length;
			BinaryPrimitives.WriteUInt16LittleEndian(page[f..], (ushort) valueAt);
			BinaryPrimitives.WriteUInt16LittleEndian(page[(f + 2)..], (ushort) storedValue.Length);
			page[f + 4] = flags;
			storedValue.CopyTo(page[valueAt..]);
		}

		/// <summary>Splices one already-built cell into a leaf page at <paramref name="index"/>, keeping key order, without touching any other cell.</summary>
		/// <returns><c>false</c> when the contiguous free area cannot take the cell, which is the caller's signal to rebuild the page (compacting it) or split it.</returns>
		/// <remarks>
		/// <para>The cell goes into the free area between the slot array and the packed cells; only the slot suffix after <paramref name="index"/> moves. Cost is one cell copy plus a slot-sized memmove, against a rebuild's re-serialization of every cell in the page.</para>
		/// <para>Callers must not use this for a REPLACE: it always adds a slot. Since replaces and deletes go through the rebuild path, which compacts, a page mutated only through here never develops gaps, and the free area is always the whole of its free space.</para>
		/// </remarks>
		/// <summary>True when the free area of a leaf can take one more cell of <paramref name="cellLength"/> bytes, slot included.</summary>
		/// <remarks>Exact rather than conservative: pages are compact by construction (replaces and deletes rebuild), so the gap between the slot array and the cell heap IS the whole of the page's free space.</remarks>
		/// <summary>Frontier of the down-growing value heap, which starts at the end of the page.</summary>
		private static int LeafValueFrontier(ReadOnlySpan<byte> page)
		{
			int area = FdbLitePageHeader.GetCellAreaOffset(page);
			return area != 0 ? area : page.Length;
		}

		/// <summary>True when the free gap between the two heaps can take one more cell of these sizes, slot included.</summary>
		/// <remarks>Exact rather than conservative: pages are compact by construction (replaces and deletes rebuild), so the gap between the key heap and the value heap IS the whole of the page's free space. The slot array is about to grow by one, and it grows into the key heap's end, so the entry has to clear that too.</remarks>
		public static bool LeafHasRoomFor(ReadOnlySpan<byte> page, int keySuffixLength, int storedValueLength)
		{
			int used = LeafKeyBase(page) + FdbLitePageHeader.GetKeyAreaLength(page);
			return LeafValueFrontier(page) - used >= LeafCellOverhead + keySuffixLength + storedValueLength;
		}

		/// <summary>Splices one cell into a leaf at <paramref name="index"/>, keeping key order, without rewriting any other cell.</summary>
		/// <returns><c>false</c> when the free gap cannot take it, which is the caller's signal to rebuild the page (compacting it) or split it.</returns>
		/// <remarks>
		/// <para>The entry is appended to the key heap and the payload to the value heap, and only the slot suffix after <paramref name="index"/> moves. Cost is two copies plus a slot-sized memmove, against a rebuild's re-serialization of every cell.</para>
		/// <para>Neither heap is kept in key order: the slot directory carries the ordering, which is what lets an insert append instead of shifting a heap. Callers must not use this for a REPLACE, since it always adds a slot; replaces and deletes go through the rebuild path, which compacts, so a page mutated only through here never develops gaps.</para>
		/// </remarks>
		public static bool TryInsertLeafCell(Span<byte> page, int index, ReadOnlySpan<byte> keySuffix, ReadOnlySpan<byte> storedValue, byte flags)
		{
			int cellCount = FdbLitePageHeader.GetCellCount(page);
			Contract.Debug.Requires(index >= 0 && index <= cellCount);

			if (!LeafHasRoomFor(page, keySuffix.Length, storedValue.Length))
			{
				return false;
			}

			int keyBase = LeafKeyBase(page);
			int keyUsed = FdbLitePageHeader.GetKeyAreaLength(page);
			int valueAt = LeafValueFrontier(page) - storedValue.Length;

			// The directory is about to gain a slot, and it grows INTO the key heap. Slide the key region up by one
			// slot's width to make room. Because slots are key-heap-relative the region moves and NOT ONE SLOT
			// CHANGES; with absolute offsets this would be an O(cells) fixup and the splice would buy nothing.
			if (keyUsed > 0)
			{
				page.Slice(keyBase, keyUsed).CopyTo(page[(keyBase + 2)..]);
			}

			// open the sorted position by sliding the slots above it up one entry (overlapping, so copy order matters)
			int from = SlotsOffset(page, isInternal: false) + (index * 2);
			int tailBytes = (cellCount - index) * 2;
			if (tailBytes > 0)
			{
				page.Slice(from, tailBytes).CopyTo(page[(from + 2)..]);
			}

			FdbLitePageHeader.SetCellCount(page, (ushort) (cellCount + 1));
			SetSlot(page, isInternal: false, index, (ushort) keyUsed);
			WriteLeafEntry(page, keyBase + 2 + keyUsed, valueAt, keySuffix, storedValue, flags);
			FdbLitePageHeader.SetKeyAreaLength(page, (ushort) (keyUsed + 2 + keySuffix.Length + 5));
			FdbLitePageHeader.SetCellAreaOffset(page, (ushort) valueAt);
			return true;
		}

		/// <summary>Lays out a run of cells, already in key order, into a freshly formatted leaf image.</summary>
		/// <remarks>The cell count is known before the run starts, so the slot directory's size is known too and the key heap can begin immediately after it; both heaps then fill sequentially towards each other. Add every cell, then <see cref="Complete"/> to stamp the header.</remarks>
		public ref struct LeafRunWriter
		{

			private readonly Span<byte> Image;
			private readonly int SlotsAt;
			private readonly int KeyBase;
			private int KeyUsed;
			private int ValueAt;
			private int Index;

			public LeafRunWriter(Span<byte> image, int count)
			{
				this.Image = image;
				this.SlotsAt = SlotsOffset(image, isInternal: false);
				// the cell count is known before the run starts, so the directory's final size is too and the key heap
				// can begin right after it; nothing has to move as the run fills
				this.KeyBase = this.SlotsAt + (count * 2);
				this.KeyUsed = 0;
				this.ValueAt = image.Length;
				this.Index = 0;
			}

			public void Add(ReadOnlySpan<byte> keySuffix, ReadOnlySpan<byte> storedValue, byte flags)
			{
				this.ValueAt -= storedValue.Length;
				WriteLeafEntry(this.Image, this.KeyBase + this.KeyUsed, this.ValueAt, keySuffix, storedValue, flags);
				BinaryPrimitives.WriteUInt16LittleEndian(this.Image[(this.SlotsAt + (this.Index * 2))..], (ushort) this.KeyUsed);
				this.KeyUsed += 2 + keySuffix.Length + 5;
				++this.Index;
			}

			public readonly void Complete()
			{
				FdbLitePageHeader.SetCellCount(this.Image, (ushort) this.Index);
				FdbLitePageHeader.SetKeyAreaLength(this.Image, (ushort) this.KeyUsed);
				FdbLitePageHeader.SetCellAreaOffset(this.Image, this.Index > 0 ? (ushort) this.ValueAt : (ushort) 0);
			}

		}

		/// <summary>Binary search of a leaf: index of the first cell whose key is &gt;= <paramref name="key"/> (= cell count when all keys are smaller); <paramref name="exact"/> reports an exact hit.</summary>
		/// <remarks>The slot directory's position and the key heap's base are computed ONCE here rather than per probe. Both depend on page data (the prefix length and the cell count), so resolving them inside the loop would put several header reads on every step of the search this layout exists to make cheap.</remarks>
		public static int FindLeafSlot(ReadOnlySpan<byte> page, ReadOnlySpan<byte> key, out bool exact)
		{
			int count = FdbLitePageHeader.GetCellCount(page);
			int slotsAt = SlotsOffset(page, isInternal: false);
			int keyBase = slotsAt + (count * 2);

			int lo = 0, hi = count;
			exact = false;
			while (lo < hi)
			{
				int mid = (lo + hi) >> 1;
				int entry = keyBase + BinaryPrimitives.ReadUInt16LittleEndian(page[(slotsAt + (mid * 2))..]);
				int cmp = page.Slice(entry + 2, BinaryPrimitives.ReadUInt16LittleEndian(page[entry..])).SequenceCompareTo(key);
				if (cmp < 0) { lo = mid + 1; }
				else
				{
					if (cmp == 0) { exact = true; }
					hi = mid;
				}
			}
			return lo;
		}

		/// <summary>Reads every part of a leaf cell in one pass, resolving the key-heap base once instead of once per part.</summary>
		/// <remarks>Gathering a page for a rebuild asks for the key, the value and the flags of every cell; going through the individual accessors would recompute the base three times per cell.</remarks>
		public static (int KeyOffset, int KeyLength, int ValueOffset, int ValueLength, byte Flags) ReadLeafCell(ReadOnlySpan<byte> page, int cellIndex)
		{
			int entry = LeafEntry(page, cellIndex);
			int keyLen = BinaryPrimitives.ReadUInt16LittleEndian(page[entry..]);
			int f = entry + 2 + keyLen;
			return (
				entry + 2, keyLen,
				BinaryPrimitives.ReadUInt16LittleEndian(page[f..]),
				BinaryPrimitives.ReadUInt16LittleEndian(page[(f + 2)..]),
				page[f + 4]
			);
		}

		#endregion

	}

}
