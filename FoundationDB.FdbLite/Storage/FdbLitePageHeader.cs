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

	/// <summary>Span accessors for the 32-byte universal header at the start of every formatted page.</summary>
	/// <remarks>
	/// <para>Layout (little-endian): checksum u64 (XxHash3-64 over the page with this field zeroed, seeded by the page's first block id), generation u64 (commit generation the page was written at), type u8, encoding u8 (payload-transform door, Plain=0 in v1), cell count u16, value-area offset u16 (the down-growing heap), prefix length u16, key-area length u16 (bytes used by the up-growing heap), then 6 bytes reserved and required to be zero.</para>
	/// <para>The block-id seed makes a page written to the wrong location fail verification; the generation stamp lets a lock-free inspector detect a page reused under its feet.</para>
	/// </remarks>
	public static class FdbLitePageHeader
	{

		/// <summary>Size of the universal page header, in bytes</summary>
		/// <remarks>
		/// <para>A multiple of 8 by convention only. It does NOT align the slot directory: the variable-length page prefix sits between this header and the slots, so what keeps that u16 array 2-byte aligned is the prefix being padded to an even length, and nothing else. Removing that pad as redundant would make the alignment of the array depend on each page's prefix.</para>
		/// <para>Bytes 26..32 are reserved and MUST be zero. Pages are zeroed on format, so this costs nothing today, and it is what makes them safe to give a meaning later.</para>
		/// </remarks>
		public const int Size = 32;

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

		/// <summary>Bytes of the key heap that are in use, measured from its base rather than from the page.</summary>
		/// <remarks>
		/// <para>A LENGTH, not an offset, and deliberately so: the key heap's base moves whenever the slot directory grows, and a length survives that move untouched while an absolute end would have to be rewritten.</para>
		/// <para>The leaf holds two heaps growing towards each other and so needs two frontiers: this one and <see cref="GetCellAreaOffset"/>, the down-growing value heap. Neither is derivable from the slot directory, because the heaps are packed in insertion order rather than key order; keeping them in key order would cost a memmove per insert, which is the trade the layout deliberately refuses.</para>
		/// </remarks>
		public static ushort GetKeyAreaLength(ReadOnlySpan<byte> page) => BinaryPrimitives.ReadUInt16LittleEndian(page[KeyAreaLengthOffset..]);

		public static void SetKeyAreaLength(Span<byte> page, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(page[KeyAreaLengthOffset..], value);

		/// <summary>Length of the key prefix common to every key on this page, stored once between the header and the slot directory.</summary>
		/// <remarks>Zero means no prefix is stripped, which is the layout's degenerate case and behaves exactly as an unstripped page. The prefix bytes themselves follow the header (and the leftmost-child field on an internal page), so the slot directory starts that much further in.</remarks>
		public static ushort GetPrefixLength(ReadOnlySpan<byte> page) => BinaryPrimitives.ReadUInt16LittleEndian(page[PrefixLengthOffset..]);

		public static void SetPrefixLength(Span<byte> page, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(page[PrefixLengthOffset..], value);

		/// <summary>Computes the page checksum: XxHash3-64 over the page with the checksum field zeroed, seeded by the page's first block id.</summary>
		public static ulong ComputeChecksum(ReadOnlySpan<byte> page, uint firstBlockId)
		{
			Contract.Debug.Requires(page.Length >= Size);
			var hash = new XxHash3(unchecked((long) firstBlockId));
			Span<byte> zeroed = stackalloc byte[8];
			hash.Append(zeroed);
			hash.Append(page[GenerationOffset..]);
			return hash.GetCurrentHashAsUInt64();
		}

		/// <summary>Writes the checksum of a fully-built page into its header (the last step before the page goes to the pager).</summary>
		public static void Seal(Span<byte> page, uint firstBlockId) => SetChecksum(page, ComputeChecksum(page, firstBlockId));

		/// <summary>Verifies a page's checksum against its location.</summary>
		public static bool Verify(ReadOnlySpan<byte> page, uint firstBlockId) => GetChecksum(page) == ComputeChecksum(page, firstBlockId);

		/// <summary>Initializes a fresh page: zeroes the span, stamps type, encoding, generation, and an empty cell area at the page end.</summary>
		public static void Format(Span<byte> page, FdbLitePageType type, ulong generation)
		{
			Contract.Debug.Requires(page.Length is > Size and <= 65536);
			page.Clear();
			SetPageType(page, type);
			SetEncoding(page, EncodingPlain);
			SetGeneration(page, generation);
			// the cell heap is empty: it starts at the end of the page (cells will grow down from there);
			// a full 64 KiB page's end offset (65536) does not fit a u16, so the stored value is (offset - 1)
			// of the last byte... instead the cell-area offset stores the offset of the LOWEST allocated cell
			// byte, and an empty page stores 0 meaning "no cell allocated yet".
			SetCellAreaOffset(page, 0);
		}

	}

}
