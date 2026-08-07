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

	/// <summary>Serializes the free-space map as a fresh chain of free-list blocks on each commit (the ruled v1 representation; a paged FIFO with amortized cost is the planned post-v1 upgrade behind a format-version bump).</summary>
	/// <remarks>Block layout: the universal page header (type FreeList, cell count = entries in this block), next block id u32 (0 = end of chain), then 16-byte entries (start u32, count u32, freed-at generation u64; generation 0 = immediately reusable).</remarks>
	public static class FdbLiteFreeListChain
	{

		private const int NextPointerOffset = FdbLitePageHeader.Size;

		private const int EntriesOffset = FdbLitePageHeader.Size + 4;

		private const int EntrySize = 16;

		/// <summary>Number of entries one free-list block holds</summary>
		public static int CapacityPerBlock(FdbLiteGeometry geometry) => (geometry.BlockSize - EntriesOffset) / EntrySize;

		/// <summary>Writes the current free-space state as a fresh chain and returns its root block id (0 when there is nothing to record).</summary>
		/// <remarks>The chain's own blocks are allocated first (through the allocator, so they leave the free set), then the REMAINING state is what gets serialized; the previous generation's chain must have been freed by the caller before this runs.</remarks>
		public static uint Persist(FdbLiteFreeSpaceMap map, FdbLiteBlockAllocator allocator, IFdbLitePager pager, ulong generation)
		{
			Contract.NotNull(map);
			Contract.NotNull(allocator);
			Contract.NotNull(pager);

			int capacity = CapacityPerBlock(pager.Geometry);

			// allocating a chain block can itself split or consume free ranges, so allocate until the chain
			// covers the CURRENT range count (each iteration changes the count by at most one: converges)
			var chain = new List<uint>();
			while (chain.Count < (map.TotalRangeCount + capacity - 1) / capacity)
			{
				chain.Add(allocator.AllocateExtent(1));
			}

			if (chain.Count == 0)
			{
				return 0;
			}

			int blockSize = pager.Geometry.BlockSize;
			var buffer = ArrayPool<byte>.Shared.Rent(blockSize);
			try
			{
				var page = buffer.AsSpan(0, blockSize);
				using var entries = map.Enumerate().GetEnumerator();
				for (int i = 0; i < chain.Count; i++)
				{
					FdbLitePageHeader.Format(page, FdbLitePageType.FreeList, generation);
					BinaryPrimitives.WriteUInt32LittleEndian(page[NextPointerOffset..], i + 1 < chain.Count ? chain[i + 1] : 0);

					int written = 0;
					var cursor = page[EntriesOffset..];
					while (written < capacity && entries.MoveNext())
					{
						var (gen, start, count) = entries.Current;
						BinaryPrimitives.WriteUInt32LittleEndian(cursor, start);
						BinaryPrimitives.WriteUInt32LittleEndian(cursor[4..], count);
						BinaryPrimitives.WriteUInt64LittleEndian(cursor[8..], gen);
						cursor = cursor[EntrySize..];
						written++;
					}
					FdbLitePageHeader.SetCellCount(page, (ushort) written);

					FdbLitePageHeader.Seal(page, chain[i]);
					pager.WriteBlocks(chain[i], page);
				}
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer);
			}

			return chain[0];
		}

		/// <summary>Loads a free-space map from a chain root (0 = empty map).</summary>
		public static FdbLiteFreeSpaceMap Load(IFdbLitePager pager, uint root, ulong expectedGeneration)
		{
			Contract.NotNull(pager);

			var map = new FdbLiteFreeSpaceMap();
			uint block = root;
			while (block != 0)
			{
				var page = pager.ReadBlocks(block, 1);
				if (!FdbLitePageHeader.Verify(page, block) || FdbLitePageHeader.GetPageType(page) != FdbLitePageType.FreeList || FdbLitePageHeader.GetGeneration(page) != expectedGeneration)
				{
					throw new InvalidDataException($"Corrupted free-list block {block}");
				}

				int count = FdbLitePageHeader.GetCellCount(page);
				var cursor = page[EntriesOffset..];
				for (int i = 0; i < count; i++)
				{
					uint start = BinaryPrimitives.ReadUInt32LittleEndian(cursor);
					uint blocks = BinaryPrimitives.ReadUInt32LittleEndian(cursor[4..]);
					ulong gen = BinaryPrimitives.ReadUInt64LittleEndian(cursor[8..]);
					if (gen == 0)
					{
						map.FreeImmediately(start, blocks);
					}
					else
					{
						map.Free(start, blocks, gen);
					}
					cursor = cursor[EntrySize..];
				}

				block = BinaryPrimitives.ReadUInt32LittleEndian(page[NextPointerOffset..]);
			}
			return map;
		}

		/// <summary>Collects the block ids of an existing chain (so a commit can free the previous generation's chain).</summary>
		public static List<uint> CollectChainBlocks(IFdbLitePager pager, uint root)
		{
			var blocks = new List<uint>();
			uint block = root;
			while (block != 0)
			{
				blocks.Add(block);
				var page = pager.ReadBlocks(block, 1);
				if (!FdbLitePageHeader.Verify(page, block) || FdbLitePageHeader.GetPageType(page) != FdbLitePageType.FreeList)
				{
					throw new InvalidDataException($"Corrupted free-list block {block}");
				}
				block = BinaryPrimitives.ReadUInt32LittleEndian(page[NextPointerOffset..]);
			}
			return blocks;
		}

	}

}
