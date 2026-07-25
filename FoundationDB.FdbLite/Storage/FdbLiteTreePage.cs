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
	/// <para>After the universal header: internal pages carry the leftmost child id (u32, does not fit the header's u16 type-specific field), then both kinds have the slot directory (u16 cell offsets in key order, binary-searchable) growing up, and the cell heap growing down from the page end.</para>
	/// <para><b>Leaf cell</b>: flags u8 (bit 0 = value is an extent reference, bit 1 reserved for a future key-overflow variant), keyLen u16, key bytes, valueLen u32, inline value bytes.</para>
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

		/// <summary>Offset of the slot directory</summary>
		public static int SlotsOffset(bool isInternal) => FdbLitePageHeader.Size + (isInternal ? 4 : 0);

		/// <summary>Fixed per-cell overhead outside the key/value bytes (slot included)</summary>
		public static int LeafCellOverhead => 1 + 2 + 4 + 2;

		public static int InternalCellOverhead => 4 + 2 + 2;

		#region Slots...

		public static ushort GetSlot(ReadOnlySpan<byte> page, bool isInternal, int index)
			=> BinaryPrimitives.ReadUInt16LittleEndian(page[(SlotsOffset(isInternal) + (index * 2))..]);

		public static void SetSlot(Span<byte> page, bool isInternal, int index, ushort cellOffset)
			=> BinaryPrimitives.WriteUInt16LittleEndian(page[(SlotsOffset(isInternal) + (index * 2))..], cellOffset);

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

		public static ReadOnlySpan<byte> GetLeafKey(ReadOnlySpan<byte> page, int cellIndex)
		{
			int off = GetSlot(page, isInternal: false, cellIndex);
			int keyLen = BinaryPrimitives.ReadUInt16LittleEndian(page[(off + 1)..]);
			return page.Slice(off + 3, keyLen);
		}

		public static byte GetLeafFlags(ReadOnlySpan<byte> page, int cellIndex) => page[GetSlot(page, isInternal: false, cellIndex)];

		/// <summary>Inline value bytes of leaf cell <paramref name="cellIndex"/> (the caller checked the extent flag)</summary>
		public static ReadOnlySpan<byte> GetLeafInlineValue(ReadOnlySpan<byte> page, int cellIndex)
		{
			int off = GetSlot(page, isInternal: false, cellIndex);
			Contract.Debug.Requires((page[off] & FlagValueIsExtent) == 0);
			int keyLen = BinaryPrimitives.ReadUInt16LittleEndian(page[(off + 1)..]);
			int valueLen = (int) BinaryPrimitives.ReadUInt32LittleEndian(page[(off + 3 + keyLen)..]);
			return page.Slice(off + 7 + keyLen, valueLen);
		}

		/// <summary>Size of the extent descriptor stored in an extent-valued leaf cell</summary>
		public const int ExtentDescriptorSize = 18;

		/// <summary>Extent descriptor of leaf cell <paramref name="cellIndex"/> (the caller checked the extent flag)</summary>
		public static (uint StartBlock, ushort BlockCount, uint TotalLength, ulong Checksum) GetLeafExtentDescriptor(ReadOnlySpan<byte> page, int cellIndex)
		{
			int off = GetSlot(page, isInternal: false, cellIndex);
			Contract.Debug.Requires((page[off] & FlagValueIsExtent) != 0);
			int keyLen = BinaryPrimitives.ReadUInt16LittleEndian(page[(off + 1)..]);
			var d = page[(off + 3 + keyLen)..];
			return (
				BinaryPrimitives.ReadUInt32LittleEndian(d),
				BinaryPrimitives.ReadUInt16LittleEndian(d[4..]),
				BinaryPrimitives.ReadUInt32LittleEndian(d[6..]),
				BinaryPrimitives.ReadUInt64LittleEndian(d[10..])
			);
		}

		/// <summary>Raw bytes of leaf cell <paramref name="cellIndex"/></summary>
		public static ReadOnlySpan<byte> GetLeafCell(ReadOnlySpan<byte> page, int cellIndex)
		{
			var (off, len) = GetLeafCellExtent(page, cellIndex);
			return page.Slice(off, len);
		}

		/// <summary>Offset and length of leaf cell <paramref name="cellIndex"/> within the page</summary>
		public static (int Offset, int Length) GetLeafCellExtent(ReadOnlySpan<byte> page, int cellIndex)
		{
			int off = GetSlot(page, isInternal: false, cellIndex);
			int keyLen = BinaryPrimitives.ReadUInt16LittleEndian(page[(off + 1)..]);
			if ((page[off] & FlagValueIsExtent) != 0)
			{
				return (off, 3 + keyLen + ExtentDescriptorSize);
			}
			int valueLen = (int) BinaryPrimitives.ReadUInt32LittleEndian(page[(off + 3 + keyLen)..]);
			return (off, 7 + keyLen + valueLen);
		}

		/// <summary>Builds the bytes of a leaf cell with an inline value into <paramref name="scratch"/> and returns the written slice.</summary>
		public static ReadOnlySpan<byte> BuildLeafCell(Span<byte> scratch, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			scratch[0] = 0;
			BinaryPrimitives.WriteUInt16LittleEndian(scratch[1..], (ushort) key.Length);
			key.CopyTo(scratch[3..]);
			BinaryPrimitives.WriteUInt32LittleEndian(scratch[(3 + key.Length)..], (uint) value.Length);
			value.CopyTo(scratch[(7 + key.Length)..]);
			return scratch[..(7 + key.Length + value.Length)];
		}

		/// <summary>Builds the bytes of a leaf cell whose value lives in a contiguous extent.</summary>
		public static ReadOnlySpan<byte> BuildLeafExtentCell(Span<byte> scratch, ReadOnlySpan<byte> key, uint startBlock, ushort blockCount, uint totalLength, ulong checksum)
		{
			scratch[0] = FlagValueIsExtent;
			BinaryPrimitives.WriteUInt16LittleEndian(scratch[1..], (ushort) key.Length);
			key.CopyTo(scratch[3..]);
			var d = scratch[(3 + key.Length)..];
			BinaryPrimitives.WriteUInt32LittleEndian(d, startBlock);
			BinaryPrimitives.WriteUInt16LittleEndian(d[4..], blockCount);
			BinaryPrimitives.WriteUInt32LittleEndian(d[6..], totalLength);
			BinaryPrimitives.WriteUInt64LittleEndian(d[10..], checksum);
			return scratch[..(3 + key.Length + ExtentDescriptorSize)];
		}

		/// <summary>Splices one already-built cell into a leaf page at <paramref name="index"/>, keeping key order, without touching any other cell.</summary>
		/// <returns><c>false</c> when the contiguous free area cannot take the cell, which is the caller's signal to rebuild the page (compacting it) or split it.</returns>
		/// <remarks>
		/// <para>The cell goes into the free area between the slot array and the packed cells; only the slot suffix after <paramref name="index"/> moves. Cost is one cell copy plus a slot-sized memmove, against a rebuild's re-serialization of every cell in the page.</para>
		/// <para>Callers must not use this for a REPLACE: it always adds a slot. Since replaces and deletes go through the rebuild path, which compacts, a page mutated only through here never develops gaps, and the free area is always the whole of its free space.</para>
		/// </remarks>
		/// <summary>True when the free area of a leaf can take one more cell of <paramref name="cellLength"/> bytes, slot included.</summary>
		/// <remarks>Exact rather than conservative: pages are compact by construction (replaces and deletes rebuild), so the gap between the slot array and the cell heap IS the whole of the page's free space.</remarks>
		public static bool LeafHasRoomFor(ReadOnlySpan<byte> page, int cellLength)
		{
			// the slot array is about to grow by one, so the new cell must clear its END
			int slotsEnd = SlotsOffset(isInternal: false) + ((FdbLitePageHeader.GetCellCount(page) + 1) * 2);
			int cellArea = FdbLitePageHeader.GetCellAreaOffset(page);
			if (cellArea == 0)
			{ // no cell allocated yet: the heap starts at the end of the page
				cellArea = page.Length;
			}
			return cellArea - cellLength >= slotsEnd;
		}

		public static bool TryInsertLeafCell(Span<byte> page, int index, ReadOnlySpan<byte> cell)
		{
			int cellCount = FdbLitePageHeader.GetCellCount(page);
			Contract.Debug.Requires(index >= 0 && index <= cellCount);

			if (!LeafHasRoomFor(page, cell.Length))
			{
				return false;
			}

			int cellArea = FdbLitePageHeader.GetCellAreaOffset(page);
			if (cellArea == 0)
			{
				cellArea = page.Length;
			}
			int target = cellArea - cell.Length;
			cell.CopyTo(page[target..]);

			// open the sorted position by sliding the slots above it up one entry (overlapping, so memmove order matters)
			int from = SlotsOffset(isInternal: false) + (index * 2);
			int tailBytes = (cellCount - index) * 2;
			if (tailBytes > 0)
			{
				page.Slice(from, tailBytes).CopyTo(page[(from + 2)..]);
			}

			SetSlot(page, isInternal: false, index, (ushort) target);
			FdbLitePageHeader.SetCellCount(page, (ushort) (cellCount + 1));
			FdbLitePageHeader.SetCellAreaOffset(page, (ushort) target);
			return true;
		}

		/// <summary>Binary search of a leaf: index of the first cell whose key is &gt;= <paramref name="key"/> (= cell count when all keys are smaller); <paramref name="exact"/> reports an exact hit.</summary>
		public static int FindLeafSlot(ReadOnlySpan<byte> page, ReadOnlySpan<byte> key, out bool exact)
		{
			int lo = 0, hi = FdbLitePageHeader.GetCellCount(page);
			exact = false;
			while (lo < hi)
			{
				int mid = (lo + hi) >> 1;
				int cmp = GetLeafKey(page, mid).SequenceCompareTo(key);
				if (cmp < 0) { lo = mid + 1; }
				else
				{
					if (cmp == 0) { exact = true; }
					hi = mid;
				}
			}
			return lo;
		}

		#endregion

	}

}
