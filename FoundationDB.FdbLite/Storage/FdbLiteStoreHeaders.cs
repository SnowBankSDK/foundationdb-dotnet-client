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
	using System.IO.Hashing;

	/// <summary>The static file header at block 0. Written at creation; the ONLY field ever rewritten afterwards is the session flag at <see cref="SessionFlagOffset"/>, and a rewrite carries byte-identical content everywhere else, so any torn rewrite yields the old or the new flag with the rest of the header unchanged.</summary>
	public static class FdbLiteFileHeader
	{

		/// <summary>Byte offset of the session ("in use") flag: 0 = clean shutdown (also every pre-flag file), anything else = a writer session is (or died) in progress and recovery must validate the newest commit manifest. The fail-safe polarity: unreadable or ambiguous reads as in-use.</summary>
		public const int SessionFlagOffset = 40;

		/// <summary>True when the file records a live (or crashed) writer session.</summary>
		public static bool ReadInUse(ReadOnlySpan<byte> block) => block[SessionFlagOffset] != 0;

		/// <summary>Stamps the session flag into a block-0 image (the caller writes the whole block back; every other byte must be carried verbatim).</summary>
		public static void WriteInUse(Span<byte> block, bool inUse) => block[SessionFlagOffset] = inUse ? (byte) 1 : (byte) 0;

		/// <summary>ASCII <c>SBKV01\r\n</c>: the newline canary catches ASCII-mode mangling (no working-name bake-in, per the format ruling)</summary>
		public static ReadOnlySpan<byte> Magic => "SBKV01\r\n"u8;

		/// <summary>Format 2 widened the universal page header to 128 bytes (the aggregate block plus its reserve); format 3 claimed reserve bytes 72..80 for the subtree extent-blocks aggregate (an older file would read 0 there and let a range clear silently leak its extents, which is why there is no migration path); format 4 moved the free list into the commit slot when it fits (an older reader would see <c>FreeListRoot = 0</c> and silently leak the whole free set, so the floor rises too). The format is Experimental: older files fail loudly at open.</summary>
		public const ushort FormatVersion = 4;

		public const ushort MinReaderVersion = 4;

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

		#region Commit manifest (single-fsync commits)...

		// The manifest lives AFTER the fixed header ([Size..]) in the same slot block: the block ids a
		// single-fsync generation wrote, split into tree pages (BlocksPerPage each) and free-list chain
		// blocks (one block each), so recovery can verify every one by its own seal. It carries its OWN
		// checksum, BOUND TO THE GENERATION: Write() only clears the fixed header region, so without the
		// binding a stale manifest from an earlier tenant of the slot could pair with a fresh header.

		private const int ManifestOffset = Size;
		private const int ManifestHeaderSize = 16; // u64 checksum, u32 page count, u32 chain count

		/// <summary>Ids a manifest can carry in one slot block (pages plus chain blocks combined).</summary>
		public static int ManifestCapacity(int blockSize) => (blockSize - ManifestOffset - ManifestHeaderSize) / 4;

		/// <summary>Writes the manifest section (an empty one for a two-phase commit, so no stale tenant survives).</summary>
		public static void WriteManifest(Span<byte> block, uint blockId, ulong generation, ReadOnlySpan<uint> pageIds, ReadOnlySpan<uint> chainIds)
		{
			Contract.Debug.Requires(pageIds.Length + chainIds.Length <= ManifestCapacity(block.Length));
			var section = block[ManifestOffset..];
			BinaryPrimitives.WriteUInt32LittleEndian(section[8..], (uint) pageIds.Length);
			BinaryPrimitives.WriteUInt32LittleEndian(section[12..], (uint) chainIds.Length);
			var ids = section[ManifestHeaderSize..];
			for (int i = 0; i < pageIds.Length; i++)
			{
				BinaryPrimitives.WriteUInt32LittleEndian(ids[(i * 4)..], pageIds[i]);
			}
			ids = ids[(pageIds.Length * 4)..];
			for (int i = 0; i < chainIds.Length; i++)
			{
				BinaryPrimitives.WriteUInt32LittleEndian(ids[(i * 4)..], chainIds[i]);
			}
			BinaryPrimitives.WriteUInt64LittleEndian(section, ComputeManifestChecksum(section, blockId, generation, pageIds.Length + chainIds.Length));
		}

		/// <summary>Reads and verifies the manifest section for the given generation; false when it is torn, stale, or foreign - in which case the slot CANNOT be trusted by a crashed-session recovery.</summary>
		public static bool TryReadManifest(ReadOnlySpan<byte> block, uint blockId, ulong generation, List<uint> pageIds, List<uint> chainIds)
		{
			var section = block[ManifestOffset..];
			int pages = (int) BinaryPrimitives.ReadUInt32LittleEndian(section[8..]);
			int chain = (int) BinaryPrimitives.ReadUInt32LittleEndian(section[12..]);
			if (pages < 0 || chain < 0 || pages + chain > ManifestCapacity(block.Length))
			{
				return false;
			}
			if (BinaryPrimitives.ReadUInt64LittleEndian(section) != ComputeManifestChecksum(section, blockId, generation, pages + chain))
			{
				return false;
			}
			var ids = section[ManifestHeaderSize..];
			for (int i = 0; i < pages; i++)
			{
				pageIds.Add(BinaryPrimitives.ReadUInt32LittleEndian(ids[(i * 4)..]));
			}
			ids = ids[(pages * 4)..];
			for (int i = 0; i < chain; i++)
			{
				chainIds.Add(BinaryPrimitives.ReadUInt32LittleEndian(ids[(i * 4)..]));
			}
			return true;
		}

		private static ulong ComputeManifestChecksum(ReadOnlySpan<byte> section, uint blockId, ulong generation, int totalIds)
		{
			var hash = new XxHash3(unchecked((long) blockId));
			Span<byte> seed = stackalloc byte[16];
			BinaryPrimitives.WriteUInt64LittleEndian(seed, generation);
			hash.Append(seed[..8]);
			hash.Append(section.Slice(8, 8 + totalIds * 4));
			return hash.GetCurrentHashAsUInt64();
		}

		#endregion

		#region Inline free list (freelist-in-slot, format 4)...

		// The free list ALSO lives in the slot when it fits: a third section after the manifest ids, in
		// the chain-block entry format, with its own generation-bound checksum (same scheme and same
		// stale-tenant reason as the manifest's). FreeListRoot == 0 means THIS section is the list (a
		// count of 0 is the empty map); != 0 means the list is chained and this section is sealed empty.
		// The section is part of the slot's validity: a slot whose inline section fails its checksum is
		// invalid AS A WHOLE, and a crashed-session recovery falls back to the other slot, so a torn slot
		// write costs one generation and can never load a wrong free list.

		private const int FreeListHeaderSize = 16; // u64 checksum, u32 range count, u32 reserved

		private const int FreeListEntrySize = 16; // start u32, count u32, freed-at generation u64 (0 = immediately reusable)

		/// <summary>Ranges the inline free-list section can carry next to a manifest of <paramref name="manifestIds"/> ids.</summary>
		public static int InlineFreeListCapacity(int blockSize, int manifestIds)
			=> (blockSize - ManifestOffset - ManifestHeaderSize - (manifestIds * 4) - FreeListHeaderSize) / FreeListEntrySize;

		/// <summary>Writes the inline free-list section (sealed EMPTY when <paramref name="map"/> is null: the list is chained, or the store is fresh).</summary>
		/// <remarks>Must run after <see cref="WriteManifest"/> with the id count that call wrote: the section sits directly after the manifest ids of the same slot image.</remarks>
		public static void WriteInlineFreeList(Span<byte> block, uint blockId, ulong generation, int manifestIds, FdbLiteFreeSpaceMap? map)
		{
			Contract.Debug.Requires(map is null || map.TotalRangeCount <= InlineFreeListCapacity(block.Length, manifestIds));
			var section = block[(ManifestOffset + ManifestHeaderSize + (manifestIds * 4))..];
			int written = 0;
			if (map is not null)
			{
				var cursor = section[FreeListHeaderSize..];
				foreach (var (gen, start, count) in map.Enumerate())
				{
					BinaryPrimitives.WriteUInt32LittleEndian(cursor, start);
					BinaryPrimitives.WriteUInt32LittleEndian(cursor[4..], count);
					BinaryPrimitives.WriteUInt64LittleEndian(cursor[8..], gen);
					cursor = cursor[FreeListEntrySize..];
					written++;
				}
			}
			BinaryPrimitives.WriteUInt32LittleEndian(section[8..], (uint) written);
			BinaryPrimitives.WriteUInt32LittleEndian(section[12..], 0);
			BinaryPrimitives.WriteUInt64LittleEndian(section, ComputeFreeListChecksum(section, blockId, generation, written));
		}

		/// <summary>Reads and verifies the inline free-list section; false when it (or the manifest header that locates it) is torn, stale, or foreign - the slot is then invalid as a whole.</summary>
		public static bool TryReadInlineFreeList(ReadOnlySpan<byte> block, uint blockId, ulong generation, [NotNullWhen(true)] out FdbLiteFreeSpaceMap? map)
		{
			map = null;

			// the manifest counts locate the section, so they verify first - on EVERY open, not only
			// crashed ones: garbage counts would point the read anywhere in the block
			var manifest = block[ManifestOffset..];
			int pages = (int) BinaryPrimitives.ReadUInt32LittleEndian(manifest[8..]);
			int chain = (int) BinaryPrimitives.ReadUInt32LittleEndian(manifest[12..]);
			if (pages < 0 || chain < 0 || pages + chain > ManifestCapacity(block.Length))
			{
				return false;
			}
			if (BinaryPrimitives.ReadUInt64LittleEndian(manifest) != ComputeManifestChecksum(manifest, blockId, generation, pages + chain))
			{
				return false;
			}

			int manifestIds = pages + chain;
			var section = block[(ManifestOffset + ManifestHeaderSize + (manifestIds * 4))..];
			int ranges = (int) BinaryPrimitives.ReadUInt32LittleEndian(section[8..]);
			if (ranges < 0 || ranges > InlineFreeListCapacity(block.Length, manifestIds))
			{
				return false;
			}
			if (BinaryPrimitives.ReadUInt64LittleEndian(section) != ComputeFreeListChecksum(section, blockId, generation, ranges))
			{
				return false;
			}

			var result = new FdbLiteFreeSpaceMap();
			var cursor = section[FreeListHeaderSize..];
			for (int i = 0; i < ranges; i++)
			{
				uint start = BinaryPrimitives.ReadUInt32LittleEndian(cursor);
				uint count = BinaryPrimitives.ReadUInt32LittleEndian(cursor[4..]);
				ulong gen = BinaryPrimitives.ReadUInt64LittleEndian(cursor[8..]);
				if (gen == 0)
				{
					result.FreeImmediately(start, count);
				}
				else
				{
					result.Free(start, count, gen);
				}
				cursor = cursor[FreeListEntrySize..];
			}
			map = result;
			return true;
		}

		private static ulong ComputeFreeListChecksum(ReadOnlySpan<byte> section, uint blockId, ulong generation, int ranges)
		{
			// ~blockId keeps this domain distinct from the manifest checksum of the same slot and generation
			var hash = new XxHash3(unchecked((long) ~blockId));
			Span<byte> seed = stackalloc byte[8];
			BinaryPrimitives.WriteUInt64LittleEndian(seed, generation);
			hash.Append(seed);
			hash.Append(section.Slice(8, 8 + ranges * FreeListEntrySize));
			return hash.GetCurrentHashAsUInt64();
		}

		#endregion

	}

}
