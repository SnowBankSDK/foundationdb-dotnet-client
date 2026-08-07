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
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using Microsoft.Win32.SafeHandles;

	/// <summary>The file pager: memory-mapped read-only region views for reads, positional file I/O for writes (the ruled LMDB-style architecture).</summary>
	/// <remarks>
	/// <para>Reads hand out spans straight over the mapped regions; the OS page cache keeps them coherent with the positional writes on every target platform. Nothing is ever written through a mapping, which is what keeps the data-before-header flush ordering enforceable with two plain <see cref="Flush"/> calls. On Linux and Windows a flush includes the drive's own cache, so commits are power-loss durable; on macOS (a development platform, not a production target) <see cref="Flush"/> deliberately stops at <c>fsync</c> - see the remarks there.</para>
	/// <para>The file grows in region multiples (a read-only view cannot extend past the end of file); region views map lazily on first touch and stay mapped - except trailing views, which are unmapped during <see cref="Truncate"/> (a mapped file cannot shrink on Windows), which is safe because truncated blocks are beyond the reclamation horizon by contract.</para>
	/// </remarks>
	public sealed unsafe partial class FdbLiteMemoryMappedPager : IFdbLitePager
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
			// Open() requires a power-of-two region and the block size is one too, so the block count per region
			// is one as well: the region index and the in-region offset are a shift and a mask, never a division.
			// This matters because ReadBlocks is per-ROW on a scan, where two integer divisions were a measurable
			// share of the per-row cost.
			this.RegionSizeInBlocksLog2 = BitOperations.TrailingZeroCount(this.RegionSizeInBlocks);
			this.BlockCount = (uint) (RandomAccess.GetLength(handle) >> geometry.BlockSizeLog2);
		}

		/// <summary>Opens (or creates a zero-length file for) a store file. The file is held with shared-READ access: a second writer cannot open it, read-only inspectors can.</summary>
		/// <param name="path">The path to the store file.</param>
		/// <param name="geometry">The geometry of the store.</param>
		/// <param name="regionSizeInBytes">The size of each region in bytes.</param>
		/// <param name="initialSizeInBytes">Reserve this much file up front instead of growing into it one region at a time. Rounded UP to a whole number of regions; ignored when the file is already at least this long.</param>
		/// <remarks>
		/// <para>Pre-allocating costs nothing at rest - the file is sparse until written - but it moves every
		/// extension out of the write path. On a bulk import that is the difference between paying a file
		/// extension every <see cref="DefaultRegionSizeInBytes"/> of data and paying none at all.</para>
		/// <para>It is a HINT, not a cap: the store still grows past it on demand.</para>
		/// </remarks>
		public static FdbLiteMemoryMappedPager Open(string path, FdbLiteGeometry geometry, int regionSizeInBytes = DefaultRegionSizeInBytes, long initialSizeInBytes = 0)
		{
			Contract.NotNullOrEmpty(path);
			Contract.Requires(regionSizeInBytes >= geometry.PageSize && BitOperations.IsPow2(regionSizeInBytes));
			Contract.Requires(initialSizeInBytes >= 0);
			var handle = File.OpenHandle(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, FileOptions.None);
			var pager = new FdbLiteMemoryMappedPager(handle, geometry, regionSizeInBytes);
			if (initialSizeInBytes > 0)
			{
				pager.Grow(checked((uint) (initialSizeInBytes >> geometry.BlockSizeLog2)));
			}
			return pager;
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

		/// <summary>Mapped regions, as an IMMUTABLE snapshot: a published array is never mutated again, which is what lets <see cref="ReadBlocks"/> stay lock-free on any thread; only the COLD first touch of a region takes <see cref="RegionsLock"/>. A volatile FIELD rather than a property, because volatile is the publish barrier.</summary>
		private volatile Region?[] Regions = [ ];

		private readonly object RegionsLock = new();

		private int RegionSizeInBytes { get; }

		private bool Disposed { get; set; }

		/// <inheritdoc />
		public FdbLiteGeometry Geometry { get; }

		/// <inheritdoc />
		public uint BlockCount { get; private set; }

		/// <inheritdoc />
		public uint RegionSizeInBlocks { get; }

		/// <inheritdoc />
		public bool TrackFirstTouch { get; set; } = true;

		private FdbLiteTouchMap Touched { get; } = new();

		/// <inheritdoc />
		public bool MarkTouched(uint firstBlock) => this.TrackFirstTouch && this.Touched.MarkTouched(firstBlock);

		/// <inheritdoc />
		public void ResetFirstTouch() => this.Touched.Reset();

		/// <inheritdoc />
		/// <remarks>
		/// <para>Real on Linux (the production target): <c>fallocate(FALLOC_FL_PUNCH_HOLE | FALLOC_FL_KEEP_SIZE)</c> makes the file sparse over the run and lets the filesystem deallocate the underlying device blocks (TRIM), while the logical length and every mapping stay untouched - a punched hole reads as zeros. Failures are swallowed: the operation is advisory and a filesystem without hole support just keeps the bytes.</para>
		/// <para>No-op on macOS: the only API is <c>fcntl(F_PUNCHHOLE)</c>, and fcntl is VARIADIC - on Apple arm64 a P/Invoke passes the argument struct where a variadic callee will not look for it, and a garbage-range punch is data loss. macOS is a development platform; do not wire it without a native shim. No-op on Windows (dev platform; would need SET_SPARSE + SET_ZERO_DATA).</para>
		/// </remarks>
		public void PunchHole(uint firstBlock, uint count)
		{
			ObjectDisposedException.ThrowIf(this.Disposed, this);
			if (!OperatingSystem.IsLinux())
			{
				return;
			}
			long offset = (long) firstBlock << this.Geometry.BlockSizeLog2;
			long length = (long) count << this.Geometry.BlockSizeLog2;
			_ = fallocate(this.Handle, FALLOC_FL_PUNCH_HOLE | FALLOC_FL_KEEP_SIZE, offset, length);
		}

		/// <inheritdoc />
		/// <remarks><c>madvise(MADV_WILLNEED)</c> on macOS and Linux (it is NOT variadic, unlike fcntl, so the P/Invoke is sound on arm64); no-op elsewhere. Mapping a region is cheap and faults nothing by itself, so prefetching a cold region only maps it and hands the kernel the read-ahead hint.</remarks>
		public void Prefetch(uint firstBlock, uint count)
		{
			ObjectDisposedException.ThrowIf(this.Disposed, this);
			if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
			{
				return;
			}
			while (count > 0)
			{
				uint region = firstBlock >> this.RegionSizeInBlocksLog2;
				uint offsetInRegion = firstBlock & (this.RegionSizeInBlocks - 1);
				uint run = Math.Min(count, this.RegionSizeInBlocks - offsetInRegion);
				var mapped = GetOrMapRegion((int) region);
				_ = madvise(mapped.Pointer + ((long) offsetInRegion << this.Geometry.BlockSizeLog2), (nuint) ((long) run << this.Geometry.BlockSizeLog2), MADV_WILLNEED);
				firstBlock += run;
				count -= run;
			}
		}

		private const int MADV_WILLNEED = 3;

		[LibraryImport("libc", SetLastError = true)]
		private static partial int madvise(byte* address, nuint length, int advice);

		private const int FALLOC_FL_KEEP_SIZE = 0x01;

		private const int FALLOC_FL_PUNCH_HOLE = 0x02;

		[LibraryImport("libc", SetLastError = true)]
		private static partial int fallocate(SafeFileHandle fd, int mode, long offset, long length);

		/// <summary>Log2 of <see cref="RegionSizeInBlocks"/>, so the hot path shifts instead of dividing.</summary>
		private int RegionSizeInBlocksLog2 { get; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReadOnlySpan<byte> ReadBlocks(uint firstBlock, int count)
		{
			ObjectDisposedException.ThrowIf(this.Disposed, this);
			Contract.Requires(count > 0 && firstBlock + (uint) count <= this.BlockCount, "block run out of bounds");
			uint region = firstBlock >> this.RegionSizeInBlocksLog2;
			Contract.Requires((firstBlock + (uint) count - 1) >> this.RegionSizeInBlocksLog2 == region, "block run straddles a region boundary");

			var mapped = GetOrMapRegion((int) region);
			int offset = (int) (firstBlock & (this.RegionSizeInBlocks - 1)) << this.Geometry.BlockSizeLog2;
			return new ReadOnlySpan<byte>(mapped.Pointer + offset, count << this.Geometry.BlockSizeLog2);
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbLitePageRef ReadBlocksRef(uint firstBlock, int count)
		{
			ObjectDisposedException.ThrowIf(this.Disposed, this);
			Contract.Requires(count > 0 && firstBlock + (uint) count <= this.BlockCount, "block run out of bounds");
			uint region = firstBlock >> this.RegionSizeInBlocksLog2;
			Contract.Requires((firstBlock + (uint) count - 1) >> this.RegionSizeInBlocksLog2 == region, "block run straddles a region boundary");

			var mapped = GetOrMapRegion((int) region);
			int offset = (int) (firstBlock & (this.RegionSizeInBlocks - 1)) << this.Geometry.BlockSizeLog2;
			return new(mapped.Pointer + offset, count << this.Geometry.BlockSizeLog2);
		}

		/// <inheritdoc />
		public void WriteBlocks(uint firstBlock, ReadOnlySpan<byte> data)
		{
			ObjectDisposedException.ThrowIf(this.Disposed, this);
			Contract.Requires((data.Length & (this.Geometry.BlockSize - 1)) == 0);
			int count = data.Length >> this.Geometry.BlockSizeLog2;
			Contract.Requires(count > 0 && firstBlock + (uint) count <= this.BlockCount, "block run out of bounds");
			Contract.Requires((firstBlock + (uint) count - 1) >> this.RegionSizeInBlocksLog2 == firstBlock >> this.RegionSizeInBlocksLog2, "block run straddles a region boundary");

			RandomAccess.Write(this.Handle, data, (long) firstBlock << this.Geometry.BlockSizeLog2);
		}

		/// <inheritdoc />
		/// <remarks>macOS splits the barrier in two: <c>fsync</c> hands data to the drive (durable against an OS
		/// crash), <c>F_FULLFSYNC</c> also flushes the drive's own cache (durable against power loss) - and .NET's
		/// <see cref="RandomAccess.FlushToDisk"/> issues the latter, measured at 4 ms against fsync's 21 µs on an
		/// M4 SSD (193x). macOS is a development platform here, and LMDB, SQLite and RocksDB all stop at
		/// <c>fsync</c> on it, so this pager does too: the same crash-safety posture as the sibling engines, and
		/// comparable benchmark numbers. Linux and Windows fold both barriers into one call, so the production
		/// targets keep full power-loss durability.</remarks>
		public void Flush()
		{
			ObjectDisposedException.ThrowIf(this.Disposed, this);
			if (OperatingSystem.IsMacOS())
			{
				if (fsync(this.Handle) != 0)
				{
					throw new IOException($"fsync of the store file failed (errno {Marshal.GetLastPInvokeError()})");
				}
				return;
			}
			RandomAccess.FlushToDisk(this.Handle);
		}

		[LibraryImport("libc", SetLastError = true)]
		private static partial int fsync(SafeFileHandle fd);

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
			this.Regions = [ ];
			uint regionsKept = (newBlockCount + this.RegionSizeInBlocks - 1) / this.RegionSizeInBlocks;
			RandomAccess.SetLength(this.Handle, (long) regionsKept * this.RegionSizeInBytes);
			this.BlockCount = regionsKept * this.RegionSizeInBlocks;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private Region GetOrMapRegion(int index)
		{
			var regions = this.Regions;
			if ((uint) index < (uint) regions.Length && regions[index] is { } mapped)
			{
				return mapped;
			}
			// the lock's try/finally makes a method un-inlinable, so the cold mapping path lives in its own method
			return MapRegionSlow(index);
		}

		private Region MapRegionSlow(int index)
		{
			lock (this.RegionsLock)
			{ // cold first touch: re-check under the lock (another reader may have mapped it first)
				var regions = this.Regions;
				if ((uint) index < (uint) regions.Length && regions[index] is { } raced)
				{
					return raced;
				}
				Contract.Requires((long) (index + 1) * this.RegionSizeInBytes <= RandomAccess.GetLength(this.Handle), "region beyond the end of file");
				var file = MemoryMappedFile.CreateFromFile(this.Handle, mapName: null, capacity: 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: true);
				var view = file.CreateViewAccessor((long) index * this.RegionSizeInBytes, this.RegionSizeInBytes, MemoryMappedFileAccess.Read);
				byte* pointer = null;
				view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
				// the accessor's pointer is to the start of the OS allocation granule: apply its own offset
				pointer += view.PointerOffset;
				var region = new Region { File = file, View = view, Pointer = pointer };
				var grown = new Region?[Math.Max(regions.Length, index + 1)];
				regions.AsSpan().CopyTo(grown);
				grown[index] = region;
				this.Regions = grown;
				return region;
			}
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
				this.Regions = [ ];
				this.Handle.Dispose();
			}
		}

	}

}
