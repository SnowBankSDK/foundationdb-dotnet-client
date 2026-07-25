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

	/// <summary>Heap-backed pager: same layout as the file pager, no file.</summary>
	/// <remarks>Serves engine tests and a future non-persistent store mode. <see cref="Flush"/> is a no-op (there is nothing durable to order), which is exactly the "memory-only pager" role the design record assigns to the in-memory story.</remarks>
	public sealed class FdbLiteHeapPager : IFdbLitePager
	{

		/// <summary>Default region size for heap stores: small (1 MiB), so region-boundary handling is exercised constantly by ordinary tests</summary>
		public const int DefaultRegionSizeInBytes = 1 << 20;

		private List<byte[]> Regions { get; } = [ ];

		public FdbLiteHeapPager(FdbLiteGeometry geometry, int regionSizeInBytes = DefaultRegionSizeInBytes)
		{
			Contract.Requires(regionSizeInBytes >= geometry.PageSize && BitOperations.IsPow2(regionSizeInBytes));
			this.Geometry = geometry;
			this.RegionSizeInBlocks = (uint) (regionSizeInBytes >> geometry.BlockSizeLog2);
			this.RegionSizeInBytes = regionSizeInBytes;
		}

		/// <inheritdoc />
		public FdbLiteGeometry Geometry { get; }

		/// <inheritdoc />
		public uint BlockCount { get; private set; }

		/// <inheritdoc />
		public uint RegionSizeInBlocks { get; }

		private int RegionSizeInBytes { get; }

		private bool Disposed { get; set; }

		/// <inheritdoc />
		public ReadOnlySpan<byte> ReadBlocks(uint firstBlock, int count) => GetSpan(firstBlock, count);

		/// <inheritdoc />
		public void WriteBlocks(uint firstBlock, ReadOnlySpan<byte> data)
		{
			Contract.Requires((data.Length & (this.Geometry.BlockSize - 1)) == 0);
			data.CopyTo(GetSpan(firstBlock, data.Length >> this.Geometry.BlockSizeLog2));
		}

		private Span<byte> GetSpan(uint firstBlock, int count)
		{
			ObjectDisposedException.ThrowIf(this.Disposed, this);
			Contract.Requires(count > 0 && firstBlock + (uint) count <= this.BlockCount, "block run out of bounds");
			uint region = firstBlock / this.RegionSizeInBlocks;
			uint last = (firstBlock + (uint) count - 1) / this.RegionSizeInBlocks;
			Contract.Requires(region == last, "block run straddles a region boundary");
			int offset = (int) (firstBlock % this.RegionSizeInBlocks) << this.Geometry.BlockSizeLog2;
			return this.Regions[(int) region].AsSpan(offset, count << this.Geometry.BlockSizeLog2);
		}

		/// <inheritdoc />
		public void Flush()
		{
			ObjectDisposedException.ThrowIf(this.Disposed, this);
		}

		/// <inheritdoc />
		public void Grow(uint minimumBlockCount)
		{
			ObjectDisposedException.ThrowIf(this.Disposed, this);
			if (minimumBlockCount <= this.BlockCount)
			{
				return;
			}
			uint regionsNeeded = (minimumBlockCount + this.RegionSizeInBlocks - 1) / this.RegionSizeInBlocks;
			while ((uint) this.Regions.Count < regionsNeeded)
			{
				this.Regions.Add(new byte[this.RegionSizeInBytes]);
			}
			this.BlockCount = regionsNeeded * this.RegionSizeInBlocks;
		}

		/// <inheritdoc />
		public void Truncate(uint newBlockCount)
		{
			ObjectDisposedException.ThrowIf(this.Disposed, this);
			Contract.Requires(newBlockCount <= this.BlockCount);
			// whole trailing regions are released; the store shrinks in region granularity (same as the file
			// pager, whose mapped regions are the unmapping unit)
			uint regionsKept = (newBlockCount + this.RegionSizeInBlocks - 1) / this.RegionSizeInBlocks;
			while ((uint) this.Regions.Count > regionsKept)
			{
				this.Regions.RemoveAt(this.Regions.Count - 1);
			}
			this.BlockCount = regionsKept * this.RegionSizeInBlocks;
		}

		/// <inheritdoc />
		public void Dispose()
		{
			this.Disposed = true;
			this.Regions.Clear();
		}

	}

}
