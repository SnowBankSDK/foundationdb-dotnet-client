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
	/// <para><b>Internal cell</b>: child page id u32, keyLen u16, separator key bytes. A page with N cells has N separators and N+1 children; child i (i &gt;= 1) covers keys &gt;= separator i-1, child 0 covers everything below separator 0.</para>
	/// <para>A page is compact after a REBUILD; the in-place mutation paths (<see cref="TryOverwriteLeafValue"/>, <see cref="TryRelocateLeafValue"/>, <see cref="TryRemoveLeafCell"/>) deliberately leave dead bytes behind and book them in <see cref="FdbLitePageHeader.GetWastedBytes"/>, which only a rebuild reclaims. The free GAP is therefore not all of a page's free space.</para>
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

		/// <summary>Length of the longest prefix <paramref name="a"/> and <paramref name="b"/> share.</summary>
		public static int CommonPrefixLength(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
		{
			int n = Math.Min(a.Length, b.Length);
			int i = 0;
			while (i < n && a[i] == b[i]) { ++i; }
			return i;
		}

		/// <summary>Installs a page's shared key prefix, before any cell is written to it.</summary>
		/// <remarks>Must precede the run: the slot directory sits after the prefix, so the prefix's length decides where every later offset lands. The stored region is padded to an even length, which is what keeps the u16 slot directory aligned whatever the prefix happens to be.</remarks>
		public static void WriteLeafPrefix(Span<byte> image, ReadOnlySpan<byte> prefix)
		{
			FdbLitePageHeader.SetPrefixLength(image, (ushort) prefix.Length);
			prefix.CopyTo(image[FdbLitePageHeader.Size..]);
			if ((prefix.Length & 1) != 0)
			{ // the pad byte is part of the region, so leave it defined rather than whatever the page held
				image[FdbLitePageHeader.Size + prefix.Length] = 0;
			}
		}

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
		/// <remarks>Based on the RESERVED slot count, so headroom keeps the key heap still across inserts. <c>Max</c> is a fail-safe: a page whose capacity was never stamped reports zero and falls back to the cell count, which is exactly the pre-headroom layout.</remarks>
		public static int LeafKeyBase(ReadOnlySpan<byte> page)
			=> SlotsOffset(page, isInternal: false) + (Math.Max(FdbLitePageHeader.GetSlotCapacity(page), FdbLitePageHeader.GetCellCount(page)) * 2);

		/// <summary>Slots reserved per growth step when a splice exhausts the directory's headroom.</summary>
		/// <remarks>
		/// <para>32 slots is 64 bytes on a page of 16 KiB or more, so the density cost is under 0.2 percent against
		/// amortising the key-area slide over 32 inserts.</para>
		/// <para>A knob rather than a constant so a benchmark can measure both ways, as
		/// <c>FdbLiteTreeWriter.AvoidSequentialAppendSplits</c> already is. <b>A value of 1 reproduces the
		/// pre-headroom behaviour exactly</b> (the key area slides by one slot on every insert), which is what
		/// makes an A/B of this change possible from a single binary. Zero is NOT a legal "off": the directory
		/// would grow into the key heap without ever moving it.</para>
		/// </remarks>
		public static int SlotGrowth { get; set; } = 32;

		/// <summary>Debug-only invariant: the occupied key heap must never reach into the value heap.</summary>
		/// <remarks>Asserted at every site that moves the directory or the key heap, so a base disagreement is caught where it is CREATED rather than several operations later inside a binary search reading past the page.</remarks>
		[Conditional("DEBUG")]
		private static void AssertHeapsIntact(ReadOnlySpan<byte> page, string site)
		{
			int cells = FdbLitePageHeader.GetCellCount(page);
			int cap = FdbLitePageHeader.GetSlotCapacity(page);
			int area = FdbLitePageHeader.GetKeyAreaLength(page);
			int keyEnd = LeafKeyBase(page) + area;
			string state = $"(cells={cells}, capacity={cap}, keyArea={area}, keyBase={LeafKeyBase(page)}, frontier={LeafValueFrontier(page)})";
			Contract.Debug.Assert(cap == 0 || cap >= cells, $"{site}: capacity below cell count {state}");
			Contract.Debug.Assert(keyEnd <= LeafValueFrontier(page), $"{site}: key heap crossed the value frontier {state}");
		}

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

		/// <summary>Orders leaf cell <paramref name="cellIndex"/>'s WHOLE key against <paramref name="other"/>, without assembling it.</summary>
		/// <remarks>The comparison walks the page prefix and then the stored suffix, so it stays copy-free on a prefix-stripped page. Comparing <see cref="GetLeafKey"/> directly against a bound is a BUG once a prefix is stripped: that returns the suffix, so a shorter string is compared against a whole key and the ordering is silently wrong rather than failing.</remarks>
		public static int CompareLeafKey(ReadOnlySpan<byte> page, int cellIndex, ReadOnlySpan<byte> other)
		{
			int prefixLen = FdbLitePageHeader.GetPrefixLength(page);
			var suffix = GetLeafKey(page, cellIndex);
			if (prefixLen == 0)
			{
				return suffix.SequenceCompareTo(other);
			}

			var prefix = page.Slice(FdbLitePageHeader.Size, prefixLen);
			if (other.Length < prefixLen)
			{ // the bound ends inside the prefix: it decides, unless it is a proper prefix, in which case the key is longer and so greater
				int head = prefix[..other.Length].SequenceCompareTo(other);
				return head != 0 ? head : 1;
			}

			int cmp = prefix.SequenceCompareTo(other[..prefixLen]);
			return cmp != 0 ? cmp : suffix.SequenceCompareTo(other[prefixLen..]);
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

		/// <summary>Value extent AND flags of leaf cell <paramref name="cellIndex"/>, resolved in ONE pass over its key-heap entry.</summary>
		/// <remarks>
		/// The pair exists because reading a value needs both, and asking for them separately walks the whole
		/// entry chain twice per cell: slot lookup, key-heap base (itself a cell-count read), then the
		/// value-offset field. On a scan that is per ROW, and it showed up in a profile as time inside
		/// <see cref="ValueOffsetField"/> and <c>ReadUInt16LittleEndian</c>. Same reasoning as
		/// <c>CellRef.OfLeafPage</c>, which already resolves a whole cell in one pass for the same reason.
		/// </remarks>
		public static (int Offset, int Length, byte Flags) LeafValueAndFlags(ReadOnlySpan<byte> page, int cellIndex)
		{
			int f = ValueOffsetField(page, LeafEntry(page, cellIndex));
			return (BinaryPrimitives.ReadUInt16LittleEndian(page[f..]), BinaryPrimitives.ReadUInt16LittleEndian(page[(f + 2)..]), page[f + 4]);
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

		/// <summary>Frontier of the down-growing value heap, which starts at the end of the page.</summary>
		private static int LeafValueFrontier(ReadOnlySpan<byte> page)
		{
			int area = FdbLitePageHeader.GetCellAreaOffset(page);
			return area != 0 ? area : page.Length;
		}

		/// <summary>Stores the value-heap frontier, encoding an empty heap (frontier still at the page end) as the 0 sentinel <see cref="LeafValueFrontier"/> decodes.</summary>
		/// <remarks>The mapping must be explicit on 64 KiB pages, where the page length itself does not fit the u16 field: a leaf holding only zero-length values (the secondary-index shape) keeps its frontier at the page end through every insert.</remarks>
		private static void SetLeafValueFrontier(Span<byte> page, int frontier)
		{
			Contract.Debug.Requires(frontier > FdbLitePageHeader.Size && frontier <= page.Length);
			FdbLitePageHeader.SetCellAreaOffset(page, frontier == page.Length ? (ushort) 0 : checked((ushort) frontier));
		}

		/// <summary>Bytes between the two heaps that no cell occupies: the room a splice or a relocation can use without reclaiming anything.</summary>
		/// <remarks>This is the free GAP only. It deliberately excludes <see cref="FdbLitePageHeader.GetWastedBytes"/>, which is real room but needs a repack to reach.</remarks>
		public static int LeafFreeGap(ReadOnlySpan<byte> page)
			=> LeafValueFrontier(page) - (LeafKeyBase(page) + FdbLitePageHeader.GetKeyAreaLength(page));

		/// <summary>Fill-oriented live bytes of a leaf, derived from its v1 header fields: everything but the free gap and the booked waste.</summary>
		/// <remarks>A leaf does not STORE this number, because its own fields already carry it: header + prefix region + slots + occupied key heap + occupied value heap, minus the dead bytes the in-place mutations booked. This is the per-leaf term of the subtree occupancy aggregates, so the derivation must stay in one place.</remarks>
		public static long LeafLiveBytes(ReadOnlySpan<byte> page)
			=> LeafKeyBase(page) - UnusedSlotBytes(page) + FdbLitePageHeader.GetKeyAreaLength(page)
			+ (page.Length - LeafValueFrontier(page))
			- FdbLitePageHeader.GetWastedBytes(page);

		/// <summary>Bytes of directory the page has RESERVED but not yet filled.</summary>
		/// <remarks>Occupancy must not count these, or a page looks fuller simply for having headroom and the underflow and consolidation arms start deciding on the headroom rather than on the data. Subtracting them makes <see cref="LeafLiveBytes"/> identical for the same cells with and without reserved slots.</remarks>
		private static int UnusedSlotBytes(ReadOnlySpan<byte> page)
			=> (Math.Max(FdbLitePageHeader.GetSlotCapacity(page), FdbLitePageHeader.GetCellCount(page)) - FdbLitePageHeader.GetCellCount(page)) * 2;

		/// <summary>Logical length of a stored value: the full extent length for an extent cell (read from its descriptor), the payload length itself otherwise.</summary>
		public static long LeafLogicalValueLength(ReadOnlySpan<byte> storedValue, byte flags)
			=> (flags & FlagValueIsExtent) != 0 ? BinaryPrimitives.ReadUInt32LittleEndian(storedValue[6..]) : storedValue.Length;

		/// <summary>Logical value length of an existing leaf cell, extent-aware.</summary>
		private static long LeafLogicalValueLengthOf(ReadOnlySpan<byte> page, int cellIndex)
			=> LeafLogicalValueLength(GetLeafStoredValue(page, cellIndex), GetLeafFlags(page, cellIndex));

		/// <summary>Applies a leaf mutation's deltas to the aggregate block (entry count, whole-key bytes, logical value bytes).</summary>
		private static void AdjustLeafAggregates(Span<byte> page, int entryDelta, long keyBytesDelta, long valueBytesDelta, long extentBlocksDelta)
		{
			FdbLitePageHeader.SetEntryCount(page, (ulong) ((long) FdbLitePageHeader.GetEntryCount(page) + entryDelta));
			FdbLitePageHeader.SetLogicalKeyBytes(page, (ulong) ((long) FdbLitePageHeader.GetLogicalKeyBytes(page) + keyBytesDelta));
			FdbLitePageHeader.SetLogicalValueBytes(page, (ulong) ((long) FdbLitePageHeader.GetLogicalValueBytes(page) + valueBytesDelta));
			if (extentBlocksDelta != 0)
			{
				FdbLitePageHeader.SetExtentBlocks(page, (ulong) ((long) FdbLitePageHeader.GetExtentBlocks(page) + extentBlocksDelta));
			}
		}

		/// <summary>Allocated extent blocks a stored value contributes: the descriptor's block count when the extent flag is set, else 0.</summary>
		internal static long ExtentBlocksOf(ReadOnlySpan<byte> storedValue, byte flags)
			=> (flags & FlagValueIsExtent) != 0 ? BinaryPrimitives.ReadUInt16LittleEndian(storedValue[4..]) : 0;

		/// <summary>Allocated extent blocks of leaf cell <paramref name="cellIndex"/> (0 for an inline value).</summary>
		internal static long ExtentBlocksOfCell(ReadOnlySpan<byte> page, int cellIndex)
			=> (GetLeafFlags(page, cellIndex) & FlagValueIsExtent) != 0 ? GetLeafExtentDescriptor(page, cellIndex).BlockCount : 0;

		/// <summary>True when the free gap between the two heaps can take one more cell of these sizes, slot included.</summary>
		/// <remarks>Counts the free GAP only. A page can also hold reclaimable room in <see cref="FdbLitePageHeader.GetWastedBytes"/>, which this does NOT include, so a false here means "cannot take it without reclaiming", not "cannot take it at all". The slot array is about to grow by one, and it grows into the key heap's end, so the entry has to clear that too.</remarks>
		public static bool LeafHasRoomFor(ReadOnlySpan<byte> page, int keySuffixLength, int storedValueLength)
			=> LeafFreeGap(page) >= LeafCellOverhead + keySuffixLength + storedValueLength;

		/// <summary>Moves a cell's value to the free gap so it can grow, and books the slot it vacated as wasted.</summary>
		/// <returns><c>false</c> when the flags differ, the value would not grow, or the gap cannot take it - all of which are the caller's signal to rebuild.</returns>
		/// <remarks>
		/// <para>The other half of in-place mutation, and the reason a growing replace need not be O(cells)
		/// either. Only the value moves: the key stays put, the slot array is untouched, and no other cell is
		/// disturbed. The old bytes are zeroed and their room is recorded rather than reclaimed, because
		/// reclaiming means shuffling every cell, which is precisely what this avoids.</para>
		/// <para>The page therefore grows a gap on purpose. That is the trade the wasted-byte counter exists to
		/// make safe: a later probe can see the room is there and repack once, instead of splitting a page that
		/// was never really full.</para>
		/// </remarks>
		public static bool TryRelocateLeafValue(Span<byte> page, int cellIndex, ReadOnlySpan<byte> storedValue, byte flags)
		{
			if (GetLeafFlags(page, cellIndex) != flags)
			{
				return false;
			}
			var (offset, length) = LeafValueExtent(page, cellIndex);
			if (storedValue.Length <= length || LeafFreeGap(page) < storedValue.Length)
			{ // not a growth, or no room to grow into without reclaiming first
				return false;
			}
			AdjustLeafAggregates(page, 0, 0, LeafLogicalValueLength(storedValue, flags) - LeafLogicalValueLengthOf(page, cellIndex), ExtentBlocksOf(storedValue, flags) - ExtentBlocksOfCell(page, cellIndex));

			int landing = LeafValueFrontier(page) - storedValue.Length;
			storedValue.CopyTo(page.Slice(landing, storedValue.Length));
			SetLeafValueFrontier(page, landing);

			int f = ValueOffsetField(page, LeafEntry(page, cellIndex));
			BinaryPrimitives.WriteUInt16LittleEndian(page[f..], (ushort) landing);
			BinaryPrimitives.WriteUInt16LittleEndian(page[(f + 2)..], (ushort) storedValue.Length);

			// the vacated slot is dead now: clear it so none of the old value can be reached, and book it
			page.Slice(offset, length).Clear();
			FdbLitePageHeader.SetWastedBytes(page, checked((ushort) (FdbLitePageHeader.GetWastedBytes(page) + length)));
			return true;
		}

		/// <summary>Overwrites a cell's stored value where it lies, when the replacement fits in the room it already has.</summary>
		/// <returns><c>false</c> when the replacement is LONGER or the flags differ, which is the caller's signal to rebuild instead.</returns>
		/// <remarks>
		/// <para>Nothing moves: no cell is relocated, no offset changes, no slot is touched and the key is not
		/// even read. That is what makes it safe to do to a live page image, and it is the whole difference
		/// between a replace costing O(1) and costing O(cells).</para>
		/// <para>A SHORTER replacement keeps the slot it already had. The surplus is zeroed (so no bytes of the
		/// old value survive where a later reader could reach them) and added to
		/// <see cref="FdbLitePageHeader.GetWastedBytes"/>, which is what lets a later probe know the page can be
		/// repacked instead of split. Reclaiming it immediately would cost the O(cells) shuffle this exists to
		/// avoid, so it is deferred until something actually needs the room.</para>
		/// </remarks>
		public static bool TryOverwriteLeafValue(Span<byte> page, int cellIndex, ReadOnlySpan<byte> storedValue, byte flags)
		{
			if (GetLeafFlags(page, cellIndex) != flags)
			{
				return false;
			}
			var (offset, length) = LeafValueExtent(page, cellIndex);
			if (storedValue.Length > length)
			{
				return false;
			}
			AdjustLeafAggregates(page, 0, 0, LeafLogicalValueLength(storedValue, flags) - LeafLogicalValueLengthOf(page, cellIndex), ExtentBlocksOf(storedValue, flags) - ExtentBlocksOfCell(page, cellIndex));

			storedValue.CopyTo(page.Slice(offset, storedValue.Length));
			int slack = length - storedValue.Length;
			if (slack > 0)
			{
				page.Slice(offset + storedValue.Length, slack).Clear();
				int f = ValueOffsetField(page, LeafEntry(page, cellIndex));
				BinaryPrimitives.WriteUInt16LittleEndian(page[(f + 2)..], (ushort) storedValue.Length);
				FdbLitePageHeader.SetWastedBytes(page, checked((ushort) (FdbLitePageHeader.GetWastedBytes(page) + slack)));
			}
			return true;
		}

		/// <summary>Splices one cell into a leaf at <paramref name="index"/>, keeping key order, without rewriting any other cell.</summary>
		/// <returns><c>false</c> when the free gap cannot take it, which is the caller's signal to rebuild the page (compacting it) or split it.</returns>
		/// <remarks>
		/// <para>The entry is appended to the key heap and the payload to the value heap, and only the slot suffix after <paramref name="index"/> moves. Cost is two copies plus a slot-sized memmove, against a rebuild's re-serialization of every cell.</para>
		/// <para>Neither heap is kept in key order: the slot directory carries the ordering, which is what lets an insert append instead of shifting a heap. Callers must not use this for a REPLACE, since it always adds a slot.</para>
		/// <para>A leaf is no longer gap-free by construction: <see cref="TryOverwriteLeafValue"/> and
		/// <see cref="TryRelocateLeafValue"/> deliberately leave slack behind rather than pay O(cells) to close
		/// it, and record it in <see cref="FdbLitePageHeader.GetWastedBytes"/>. Anything reasoning about how
		/// much room a page really has must consult that counter as well as the free gap.</para>
		/// </remarks>
		public static bool TryInsertLeafCell(Span<byte> page, int index, ReadOnlySpan<byte> key, ReadOnlySpan<byte> storedValue, byte flags)
		{
			int cellCount = FdbLitePageHeader.GetCellCount(page);
			Contract.Debug.Requires(index >= 0 && index <= cellCount);
			int wholeKeyLength = key.Length; // captured before the prefix strip below: the aggregates count WHOLE keys

			// every key on the page must start with the page prefix, so a key that does not cannot be spliced in:
			// the page's shared prefix would have to shrink, which means re-expanding every stored suffix. Refusing
			// hands that to the rebuild path, which recomputes the prefix over the old keys plus this one.
			int prefixLen = FdbLitePageHeader.GetPrefixLength(page);
			if (prefixLen > 0)
			{
				if (key.Length < prefixLen || !key[..prefixLen].SequenceEqual(GetPagePrefix(page, isInternal: false)))
				{
					return false;
				}
				key = key[prefixLen..];
			}
			var keySuffix = key;

			// The directory grows INTO the key heap. Rather than sliding the key region on every insert, the
			// directory reserves slots ahead of the count: while headroom remains, NOTHING moves. Only when it is
			// exhausted does the key area slide, by a whole growth step at once, so the O(key-area) cost is paid
			// once per SlotGrowth inserts instead of once per insert. That per-insert slide was the dominant cost
			// of a sequential load, and it is why FdbLite got slower as values got SMALLER (more cells per page,
			// so more key area to move) where the legacy prototype, whose append moves nothing, got faster.
			AssertHeapsIntact(page, "TryInsertLeafCell entry");
			int capacity = Math.Max(FdbLitePageHeader.GetSlotCapacity(page), cellCount);
			int growBy = cellCount < capacity ? 0 : SlotGrowth;

			// the reserved-but-unused slots are room this cell cannot have, so they belong in the room test
			if (LeafFreeGap(page) < LeafCellOverhead + keySuffix.Length + storedValue.Length + (growBy * 2))
			{
				return false;
			}

			int keyBase = LeafKeyBase(page);
			int keyUsed = FdbLitePageHeader.GetKeyAreaLength(page);

			if (growBy > 0)
			{
				if (keyUsed > 0)
				{
					page.Slice(keyBase, keyUsed).CopyTo(page[(keyBase + (growBy * 2))..]);
				}
				capacity += growBy;
				FdbLitePageHeader.SetSlotCapacity(page, checked((ushort) capacity));
				keyBase += growBy * 2; // the heap moved, and LeafKeyBase now agrees
			}

			int valueAt = LeafValueFrontier(page) - storedValue.Length;

			// open the sorted position by sliding the slots above it up one entry (overlapping, so copy order matters)
			int from = SlotsOffset(page, isInternal: false) + (index * 2);
			int tailBytes = (cellCount - index) * 2;
			if (tailBytes > 0)
			{
				page.Slice(from, tailBytes).CopyTo(page[(from + 2)..]);
			}

			FdbLitePageHeader.SetCellCount(page, (ushort) (cellCount + 1));
			SetSlot(page, isInternal: false, index, (ushort) keyUsed);
			// keyBase already accounts for the reserved directory (and for a growth step, when one just happened),
			// so the append point is simply the end of the occupied key heap. The old "+ 2" here was the
			// per-insert slide, which no longer takes place.
			// keyBase already accounts for the reserved directory (and for a growth step, when one just happened),
			// so the append point is the end of the occupied key heap. The old "+ 2" here was the per-insert slide.
			WriteLeafEntry(page, keyBase + keyUsed, valueAt, keySuffix, storedValue, flags);
			FdbLitePageHeader.SetKeyAreaLength(page, checked((ushort) (keyUsed + 2 + keySuffix.Length + 5)));
			SetLeafValueFrontier(page, valueAt);
			AdjustLeafAggregates(page, +1, wholeKeyLength, LeafLogicalValueLength(storedValue, flags), ExtentBlocksOf(storedValue, flags));
			AssertHeapsIntact(page, "TryInsertLeafCell exit");
			return true;
		}

		/// <summary>Removes one cell from a leaf without rewriting the others, booking the room it held as wasted.</summary>
		/// <returns><c>false</c> when this is the page's LAST cell or the value is an extent: both need the writer, not the page.</returns>
		/// <remarks>
		/// <para>The exact mirror of <see cref="TryInsertLeafCell"/>. The directory loses a slot and therefore
		/// shrinks INTO the key heap, so the key region slides DOWN by one slot's width - and because slots are
		/// key-heap-relative, the region moves and not one slot changes. With absolute offsets this would be an
		/// O(cells) fixup and the whole thing would be pointless.</para>
		/// <para>The removed key entry stays where it is as a gap, and its bytes plus the value's are booked
		/// into <see cref="FdbLitePageHeader.GetWastedBytes"/>. Both are zeroed first: a dead cell's bytes must
		/// not remain reachable, and a delete is exactly the operation where someone expects them gone.</para>
		/// <para>Cost is two memmoves bounded by the page, against a rebuild's re-serialisation of every cell.</para>
		/// </remarks>
		public static bool TryRemoveLeafCell(Span<byte> page, int cellIndex)
		{
			int cellCount = FdbLitePageHeader.GetCellCount(page);
			Contract.Debug.Requires(cellIndex >= 0 && cellIndex < cellCount);
			if (cellCount <= 1 || (GetLeafFlags(page, cellIndex) & FlagValueIsExtent) != 0)
			{ // emptying a page means unhooking it from its parent, and an extent's blocks must be freed: both
			  // are the writer's business, so hand them back
				return false;
			}

			int entry = LeafEntry(page, cellIndex);
			int keyLen = BinaryPrimitives.ReadUInt16LittleEndian(page[entry..]);
			int entryBytes = 2 + keyLen + 5; // key length, the suffix, then value offset, value length, flags
			var (valueOffset, valueLength) = LeafValueExtent(page, cellIndex);

			// wipe both before anything moves, while their offsets still mean what they say
			page.Slice(valueOffset, valueLength).Clear();
			page.Slice(entry, entryBytes).Clear();

			// close the hole in the sorted directory (overlapping, so the direction matters)
			int slotsBase = SlotsOffset(page, isInternal: false);
			int from = slotsBase + ((cellIndex + 1) * 2);
			int tailBytes = (cellCount - cellIndex - 1) * 2;
			if (tailBytes > 0)
			{
				page.Slice(from, tailBytes).CopyTo(page[(from - 2)..]);
			}

			// The key heap does NOT move. The directory keeps its reserved capacity when a cell goes, so the slot
			// the removal freed becomes headroom for the next insert instead of a reason to slide the whole key
			// area down and then back up on the next splice. Capacity is pinned here for pages that carry none:
			// without it, dropping the count would move LeafKeyBase and orphan every key on the page.
			FdbLitePageHeader.SetSlotCapacity(page, checked((ushort) Math.Max(FdbLitePageHeader.GetSlotCapacity(page), cellCount)));
			FdbLitePageHeader.SetCellCount(page, (ushort) (cellCount - 1));
			FdbLitePageHeader.SetWastedBytes(page, checked((ushort) (FdbLitePageHeader.GetWastedBytes(page) + entryBytes + valueLength)));
			// extents are refused above, so the stored value length IS the logical one
			AdjustLeafAggregates(page, -1, -(FdbLitePageHeader.GetPrefixLength(page) + keyLen), -valueLength, 0);
			AssertHeapsIntact(page, "TryRemoveLeafCell exit");
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
			private long LogicalValueBytes;

			private long ExtentBlocks;

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
				this.LogicalValueBytes = 0;
			}

			public void Add(ReadOnlySpan<byte> keySuffix, ReadOnlySpan<byte> storedValue, byte flags)
				=> Add(default, keySuffix, storedValue, flags);

			/// <summary>Adds a cell whose stored key is <paramref name="keyHead"/> followed by <paramref name="keyTail"/>.</summary>
			/// <remarks>The two-part form exists so a rebuild never has to materialize a whole key. A cell gathered from a page that stripped a prefix holds only its suffix, and the page being built may strip a shorter prefix; the difference is then a slice of the OLD prefix followed by that suffix, which is two spans and no copy.</remarks>
			public void Add(ReadOnlySpan<byte> keyHead, ReadOnlySpan<byte> keyTail, ReadOnlySpan<byte> storedValue, byte flags)
			{
				int keyLen = keyHead.Length + keyTail.Length;
				this.ValueAt -= storedValue.Length;

				int at = this.KeyBase + this.KeyUsed;
				// the caller's size plan is the only thing standing between the two heaps: a wrong plan writes
				// keys over values SILENTLY (that was the shape of the prefix-boundary defect), so catch it at
				// the first offending cell instead of at the next audit
				Contract.Debug.Assert(at + 2 + keyLen + 5 <= this.ValueAt, "key heap and value heap crossed: the caller's size plan was wrong");
				BinaryPrimitives.WriteUInt16LittleEndian(this.Image[at..], (ushort) keyLen);
				keyHead.CopyTo(this.Image[(at + 2)..]);
				keyTail.CopyTo(this.Image[(at + 2 + keyHead.Length)..]);
				int f = at + 2 + keyLen;
				BinaryPrimitives.WriteUInt16LittleEndian(this.Image[f..], (ushort) this.ValueAt);
				BinaryPrimitives.WriteUInt16LittleEndian(this.Image[(f + 2)..], (ushort) storedValue.Length);
				this.Image[f + 4] = flags;
				storedValue.CopyTo(this.Image[this.ValueAt..]);

				BinaryPrimitives.WriteUInt16LittleEndian(this.Image[(this.SlotsAt + (this.Index * 2))..], (ushort) this.KeyUsed);
				this.KeyUsed += 2 + keyLen + 5;
				this.LogicalValueBytes += LeafLogicalValueLength(storedValue, flags);
				this.ExtentBlocks += ExtentBlocksOf(storedValue, flags);
				++this.Index;
			}

			public readonly void Complete()
			{
				FdbLitePageHeader.SetCellCount(this.Image, (ushort) this.Index);
				FdbLitePageHeader.SetKeyAreaLength(this.Image, (ushort) this.KeyUsed);
				SetLeafValueFrontier(this.Image, this.ValueAt);

				// the leaf's own aggregate block: entry count, whole-key bytes (each stored suffix re-expanded by
				// the page prefix), logical value bytes accumulated extent-aware by Add, and itself as one leaf
				int prefixLength = FdbLitePageHeader.GetPrefixLength(this.Image);
				long storedKeyBytes = this.KeyUsed - (7L * this.Index); // each entry carries 7 bytes outside its key
				FdbLitePageHeader.SetEntryCount(this.Image, (ulong) this.Index);
				FdbLitePageHeader.SetLogicalKeyBytes(this.Image, (ulong) (storedKeyBytes + ((long) this.Index * prefixLength)));
				FdbLitePageHeader.SetLogicalValueBytes(this.Image, (ulong) this.LogicalValueBytes);
				FdbLitePageHeader.SetSubtreeLiveBytes(this.Image, 0);
				FdbLitePageHeader.SetLeafCount(this.Image, 1);
				FdbLitePageHeader.SetExtentBlocks(this.Image, (ulong) this.ExtentBlocks);
			}

		}

		/// <summary>Binary search of a leaf: index of the first cell whose key is &gt;= <paramref name="key"/> (= cell count when all keys are smaller); <paramref name="exact"/> reports an exact hit.</summary>
		/// <remarks>The slot directory's position and the key heap's base are computed ONCE here rather than per probe. Both depend on page data (the prefix length and the cell count), so resolving them inside the loop would put several header reads on every step of the search this layout exists to make cheap.</remarks>
		public static int FindLeafSlot(ReadOnlySpan<byte> page, ReadOnlySpan<byte> key, out bool exact)
		{
			int count = FdbLitePageHeader.GetCellCount(page);
			int slotsAt = SlotsOffset(page, isInternal: false);
			// MUST go through LeafKeyBase: the directory reserves slots ahead of the cell count, so the base is
			// NOT slotsAt + count * 2. Inlining that expression here (which this did, for the one-read-per-search
			// reason in the remarks) silently resolved every slot against the wrong base the moment headroom
			// existed, and the search then read past the key heap. Still one call, still once per search.
			int keyBase = LeafKeyBase(page);
			exact = false;

			// The probe is compared against the page prefix ONCE, not once per probe, which is what keeps the
			// search at fixed cost per step. Every key on the page starts with this prefix, so a probe that
			// diverges inside it sorts entirely before or entirely after the page and needs no search at all.
			int prefixLen = FdbLitePageHeader.GetPrefixLength(page);
			if (prefixLen > 0)
			{
				var prefix = page.Slice(FdbLitePageHeader.Size, prefixLen);
				if (key.Length < prefixLen)
				{
					int c = key.SequenceCompareTo(prefix[..key.Length]);
					// a proper prefix of the page prefix sorts before every extension of it, so before every key here
					return c <= 0 ? 0 : count;
				}
				int cmp = key[..prefixLen].SequenceCompareTo(prefix);
				if (cmp != 0)
				{
					return cmp < 0 ? 0 : count;
				}
				key = key[prefixLen..];
			}

			int lo = 0, hi = count;
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
