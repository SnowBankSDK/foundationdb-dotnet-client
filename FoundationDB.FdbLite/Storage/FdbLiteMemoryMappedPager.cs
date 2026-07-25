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
	using System.IO.MemoryMappedFiles;
	using Microsoft.Win32.SafeHandles;

	/// <summary>The file pager: memory-mapped read-only region views for reads, positional file I/O for writes (the ruled LMDB-style architecture).</summary>
	/// <remarks>
	/// <para>Reads hand out spans straight over the mapped regions; the OS page cache keeps them coherent with the positional writes on every target platform. Nothing is ever written through a mapping, which is what keeps the data-before-header flush ordering enforceable with two plain <see cref="Flush"/> calls (on Apple platforms .NET issues <c>F_FULLFSYNC</c>, so the barrier is real there too).</para>
	/// <para>The file grows in region multiples (a read-only view cannot extend past the end of file); region views map lazily on first touch and stay mapped - except trailing views, which are unmapped during <see cref="Truncate"/> (a mapped file cannot shrink on Windows), which is safe because truncated blocks are beyond the reclamation horizon by contract.</para>
	/// </remarks>
	public sealed unsafe class FdbLiteMemoryMappedPager : IFdbLitePager
	{

		/// <summary>Default region size for file stores (16 MiB: small enough that a fresh store stays reasonable, large enough to keep the view count trivial)</summary>
		public const int DefaultRegionSizeInBytes = 16 << 20;

		private sealed class Region : IDisposable
		{
			public required MemoryMappedFile File { get; init; }
			public required MemoryMappedViewAccessor View { get; init; }
			public required byte* Pointer { get; init; }

			public void Dispose()
			{
				this.View.SafeMemoryMappedViewHandle.ReleasePointer();
				this.View.Dispose();
				this.File.Dispose();
			}
		}

		private FdbLiteMemoryMappedPager(SafeFileHandle handle, FdbLiteGeometry geometry, int regionSizeInBytes)
		{
			this.Handle = handle;
			this.Geometry = geometry;
			this.RegionSizeInBytes = regionSizeInBytes;
			this.RegionSizeInBlocks = (uint) (regionSizeInBytes >> geometry.BlockSizeLog2);
			this.BlockCount = (uint) (RandomAccess.GetLength(handle) >> geometry.BlockSizeLog2);
		}

		/// <summary>Opens (or creates a zero-length file for) a store file. The file is held with shared-READ access: a second writer cannot open it, read-only inspectors can.</summary>
		public static FdbLiteMemoryMappedPager Open(string path, FdbLiteGeometry geometry, int regionSizeInBytes = DefaultRegionSizeInBytes)
		{
			Contract.NotNullOrEmpty(path);
			Contract.Requires(regionSizeInBytes >= geometry.PageSize && BitOperations.IsPow2(regionSizeInBytes));
			var handle = File.OpenHandle(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, FileOptions.None);
			return new FdbLiteMemoryMappedPager(handle, geometry, regionSizeInBytes);
		}

		/// <summary>Reads the geometry out of an existing store file's header (needed before a pager can be constructed).</summary>
		public static FdbLiteGeometry ReadGeometry(string path)
		{
			using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			Span<byte> head = stackalloc byte[FdbLiteFileHeader.Size];
			int read = RandomAccess.Read(handle, head, 0);
			if (read < FdbLiteFileHeader.Size)
			{
				throw new InvalidDataException("Store file too short");
			}
			return FdbLiteFileHeader.Read(head).Geometry;
		}

		private SafeFileHandle Handle { get; }

		private List<Region?> Regions { get; } = [ ];

		private int RegionSizeInBytes { get; }

		private bool Disposed { get; set; }

		/// <inheritdoc />
		public FdbLiteGeometry Geometry { get; }

		/// <inheritdoc />
		public uint BlockCount { get; private set; }

		/// <inheritdoc />
		public uint RegionSizeInBlocks { get; }

		/// <inheritdoc />
		public ReadOnlySpan<byte> ReadBlocks(uint firstBlock, int count)
		{
			ObjectDisposedException.ThrowIf(this.Disposed, this);
			Contract.Requires(count > 0 && firstBlock + (uint) count <= this.BlockCount, "block run out of bounds");
			uint region = firstBlock / this.RegionSizeInBlocks;
			Contract.Requires((firstBlock + (uint) count - 1) / this.RegionSizeInBlocks == region, "block run straddles a region boundary");

			var mapped = GetOrMapRegion((int) region);
			int offset = (int) (firstBlock % this.RegionSizeInBlocks) << this.Geometry.BlockSizeLog2;
			return new ReadOnlySpan<byte>(mapped.Pointer + offset, count << this.Geometry.BlockSizeLog2);
		}

		/// <inheritdoc />
		public void WriteBlocks(uint firstBlock, ReadOnlySpan<byte> data)
		{
			ObjectDisposedException.ThrowIf(this.Disposed, this);
			Contract.Requires((data.Length & (this.Geometry.BlockSize - 1)) == 0);
			int count = data.Length >> this.Geometry.BlockSizeLog2;
			Contract.Requires(count > 0 && firstBlock + (uint) count <= this.BlockCount, "block run out of bounds");
			Contract.Requires((firstBlock + (uint) count - 1) / this.RegionSizeInBlocks == firstBlock / this.RegionSizeInBlocks, "block run straddles a region boundary");

			RandomAccess.Write(this.Handle, data, (long) firstBlock << this.Geometry.BlockSizeLog2);
		}

		/// <inheritdoc />
		public void Flush()
		{
			ObjectDisposedException.ThrowIf(this.Disposed, this);
			RandomAccess.FlushToDisk(this.Handle);
		}

		/// <inheritdoc />
		public void Grow(uint minimumBlockCount)
		{
			ObjectDisposedException.ThrowIf(this.Disposed, this);
			if (minimumBlockCount <= this.BlockCount)
			{
				return;
			}
			// region-granular growth: a read-only view cannot extend past the end of file, so the file is
			// always long enough for any region that can be mapped (sparse/lazy allocation keeps this cheap).
			// The extension is done by WRITING past the end of file, never SetLength: Windows refuses ANY
			// end-of-file change while a user-mapped section exists on the file, but write-extension is allowed.
			uint regionsNeeded = (minimumBlockCount + this.RegionSizeInBlocks - 1) / this.RegionSizeInBlocks;
			long newLength = (long) regionsNeeded * this.RegionSizeInBytes;
			Span<byte> zero = [ 0 ];
			RandomAccess.Write(this.Handle, zero, newLength - 1);
			this.BlockCount = regionsNeeded * this.RegionSizeInBlocks;
		}

		/// <inheritdoc />
		public void Truncate(uint newBlockCount)
		{
			ObjectDisposedException.ThrowIf(this.Disposed, this);
			Contract.Requires(newBlockCount <= this.BlockCount);
			// Windows refuses end-of-file changes while ANY user-mapped section exists, so truncation
			// unmaps EVERY view (invalidating all outstanding spans) and remaps lazily afterwards: the
			// engine only truncates in pin-free quiet moments, where no span can be live
			foreach (var region in this.Regions)
			{
				region?.Dispose();
			}
			this.Regions.Clear();
			uint regionsKept = (newBlockCount + this.RegionSizeInBlocks - 1) / this.RegionSizeInBlocks;
			RandomAccess.SetLength(this.Handle, (long) regionsKept * this.RegionSizeInBytes);
			this.BlockCount = regionsKept * this.RegionSizeInBlocks;
		}

		private Region GetOrMapRegion(int index)
		{
			while (this.Regions.Count <= index)
			{
				this.Regions.Add(null);
			}
			var region = this.Regions[index];
			if (region == null)
			{
				Contract.Requires((long) (index + 1) * this.RegionSizeInBytes <= RandomAccess.GetLength(this.Handle), "region beyond the end of file");
				var file = MemoryMappedFile.CreateFromFile(this.Handle, mapName: null, capacity: 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: true);
				var view = file.CreateViewAccessor((long) index * this.RegionSizeInBytes, this.RegionSizeInBytes, MemoryMappedFileAccess.Read);
				byte* pointer = null;
				view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
				// the accessor's pointer is to the start of the OS allocation granule: apply its own offset
				pointer += view.PointerOffset;
				region = new Region { File = file, View = view, Pointer = pointer };
				this.Regions[index] = region;
			}
			return region;
		}

		/// <inheritdoc />
		public void Dispose()
		{
			if (!this.Disposed)
			{
				this.Disposed = true;
				foreach (var region in this.Regions)
				{
					region?.Dispose();
				}
				this.Regions.Clear();
				this.Handle.Dispose();
			}
		}

	}

}
