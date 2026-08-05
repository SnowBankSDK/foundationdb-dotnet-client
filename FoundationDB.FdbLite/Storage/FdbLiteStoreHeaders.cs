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

	/// <summary>The static file header at block 0 (written once at creation, never rewritten: it cannot be torn).</summary>
	public static class FdbLiteFileHeader
	{

		/// <summary>ASCII <c>SBKV01\r\n</c>: the newline canary catches ASCII-mode mangling (no working-name bake-in, per the format ruling)</summary>
		public static ReadOnlySpan<byte> Magic => "SBKV01\r\n"u8;

		/// <summary>Format 2 widened the universal page header to 128 bytes (the aggregate block plus its reserve); format 3 claimed reserve bytes 72..80 for the subtree extent-blocks aggregate (an older file would read 0 there and let a range clear silently leak its extents, which is why there is no migration path). The format is Experimental: older files fail loudly at open.</summary>
		public const ushort FormatVersion = 3;

		public const ushort MinReaderVersion = 2;

		/// <summary>Bytes actually used at the head of block 0 (the rest is reserved, zero)</summary>
		public const int Size = 48;

		/// <summary>Writes a fresh file header (block 0 image); the caller supplies the whole zeroed block.</summary>
		public static void Write(Span<byte> block, FdbLiteGeometry geometry, Guid fileId, long creationUnixMillis)
		{
			block.Clear();
			Magic.CopyTo(block);
			BinaryPrimitives.WriteUInt16LittleEndian(block[8..], FormatVersion);
			BinaryPrimitives.WriteUInt16LittleEndian(block[10..], MinReaderVersion);
			block[12] = geometry.BlockSizeLog2;
			block[13] = geometry.PageSizeInBlocksLog2;
			fileId.TryWriteBytes(block[16..32]);
			BinaryPrimitives.WriteInt64LittleEndian(block[32..], creationUnixMillis);
		}

		/// <summary>Parses and validates a file header; throws on anything unreadable.</summary>
		public static (FdbLiteGeometry Geometry, Guid FileId) Read(ReadOnlySpan<byte> block)
		{
			if (!block[..8].SequenceEqual(Magic))
			{
				throw new InvalidDataException("Not a store file (bad magic)");
			}
			ushort version = BinaryPrimitives.ReadUInt16LittleEndian(block[8..]);
			ushort minReader = BinaryPrimitives.ReadUInt16LittleEndian(block[10..]);
			if (minReader > FormatVersion)
			{
				throw new InvalidDataException($"Store file requires a newer reader (format {minReader})");
			}
			if (version < FormatVersion)
			{ // the format is Experimental: an older file has no migration path and must fail here, loudly,
			  // rather than have format-2 accessors read garbage out of its narrower page headers
				throw new InvalidDataException($"Store file was written at format {version}; this reader supports format {FormatVersion} and the Experimental format has no migration path");
			}
			var geometry = new FdbLiteGeometry(block[12], block[13]);
			var fileId = new Guid(block[16..32]);
			return (geometry, fileId);
		}

	}

	/// <summary>One decoded snapshot header (blocks 1 and 2 alternate; a torn write fails its checksum, so the newest VALID header wins and no half-committed state marks exist).</summary>
	public readonly record struct FdbLiteSnapshotHeader(
		ulong Generation,
		ulong DatabaseVersion,
		uint RootPageId,
		uint AllocatedBlockCount,
		uint FreeListRoot,
		uint AllocationFrontier,
		ulong KeyCount,
		ulong Horizon)
	{

		/// <summary>Bytes covered by the checksum at the head of the header block (the rest of the block is reserved, zero)</summary>
		public const int Size = 128;

		/// <summary>Serializes into the first 128 bytes of a header block and seals the checksum.</summary>
		public void Write(Span<byte> block, uint blockId)
		{
			Contract.Debug.Requires(blockId is 1 or 2);
			block[..Size].Clear();
			BinaryPrimitives.WriteUInt64LittleEndian(block[8..], this.Generation);
			BinaryPrimitives.WriteUInt64LittleEndian(block[16..], this.DatabaseVersion);
			BinaryPrimitives.WriteUInt32LittleEndian(block[24..], this.RootPageId);
			BinaryPrimitives.WriteUInt32LittleEndian(block[28..], this.AllocatedBlockCount);
			BinaryPrimitives.WriteUInt32LittleEndian(block[32..], this.FreeListRoot);
			BinaryPrimitives.WriteUInt32LittleEndian(block[36..], this.AllocationFrontier);
			BinaryPrimitives.WriteUInt64LittleEndian(block[40..], this.KeyCount);
			BinaryPrimitives.WriteUInt64LittleEndian(block[48..], this.Horizon);
			BinaryPrimitives.WriteUInt64LittleEndian(block, ComputeChecksum(block, blockId));
		}

		/// <summary>Decodes and verifies one header slot; false when the slot is torn, foreign, or blank.</summary>
		public static bool TryRead(ReadOnlySpan<byte> block, uint blockId, out FdbLiteSnapshotHeader header)
		{
			header = default;
			if (block.Length < Size)
			{
				return false;
			}
			if (BinaryPrimitives.ReadUInt64LittleEndian(block) != ComputeChecksum(block, blockId))
			{
				return false;
			}
			header = new(
				Generation: BinaryPrimitives.ReadUInt64LittleEndian(block[8..]),
				DatabaseVersion: BinaryPrimitives.ReadUInt64LittleEndian(block[16..]),
				RootPageId: BinaryPrimitives.ReadUInt32LittleEndian(block[24..]),
				AllocatedBlockCount: BinaryPrimitives.ReadUInt32LittleEndian(block[28..]),
				FreeListRoot: BinaryPrimitives.ReadUInt32LittleEndian(block[32..]),
				AllocationFrontier: BinaryPrimitives.ReadUInt32LittleEndian(block[36..]),
				KeyCount: BinaryPrimitives.ReadUInt64LittleEndian(block[40..]),
				Horizon: BinaryPrimitives.ReadUInt64LittleEndian(block[48..]));
			return true;
		}

		private static ulong ComputeChecksum(ReadOnlySpan<byte> block, uint blockId)
		{
			var hash = new XxHash3(unchecked((long) blockId));
			Span<byte> zeroed = stackalloc byte[8];
			hash.Append(zeroed);
			hash.Append(block[8..Size]);
			return hash.GetCurrentHashAsUInt64();
		}

	}

}
