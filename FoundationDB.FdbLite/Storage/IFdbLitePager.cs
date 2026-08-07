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

	/// <summary>Block-addressed storage under the engine.</summary>
	/// <remarks>
	/// <para>Blocks (<see cref="FdbLiteGeometry.BlockSize"/>) are the allocation unit. The file is presented as fixed-size mapping regions; a read never crosses a region boundary, which is why the allocator never places a page or extent across one (<see cref="RegionSizeInBlocks"/>).</para>
	/// <para><see cref="ReadBlocks"/> hands out read-only spans over the pager's own memory (mapped regions, or heap arrays for the in-memory pager); the span stays valid until the blocks are reused or truncated - the engine's pin/horizon machinery provides that guarantee, and the read-only mapping makes stray writes fault at the culprit.</para>
	/// <para>Writes are positional and only durable after <see cref="Flush"/>; the two-fsync commit protocol is: write data blocks, Flush, write the snapshot header block, Flush.</para>
	/// </remarks>
	public interface IFdbLitePager : IDisposable
	{

		/// <summary>Size parameters of this store</summary>
		FdbLiteGeometry Geometry { get; }

		/// <summary>Number of addressable blocks (the current file length)</summary>
		uint BlockCount { get; }

		/// <summary>Number of blocks per mapping region (a power of two): reads, pages, and extents never straddle a region boundary</summary>
		uint RegionSizeInBlocks { get; }

		/// <summary>Returns a read-only view over a contiguous run of blocks (which must not straddle a region boundary).</summary>
		ReadOnlySpan<byte> ReadBlocks(uint firstBlock, int count);

		/// <summary>Returns a HOLDABLE reference to a contiguous run of blocks, with the same validity contract as <see cref="ReadBlocks"/>.</summary>
		/// <remarks>The scan path's per-leaf cache: a cursor (a plain struct, so it cannot hold a span) resolves the pager once per LEAF and reads rows off <see cref="FdbLitePageRef.Span"/>, instead of paying this interface two to three times per row.</remarks>
		FdbLitePageRef ReadBlocksRef(uint firstBlock, int count);

		/// <summary>Writes a contiguous run of blocks (which must not straddle a region boundary); durable only after <see cref="Flush"/>.</summary>
		void WriteBlocks(uint firstBlock, ReadOnlySpan<byte> data);

		/// <summary>Forces everything written so far to durable storage.</summary>
		void Flush();

		/// <summary>Grows the store to at least the requested number of blocks.</summary>
		void Grow(uint minimumBlockCount);

		/// <summary>Shrinks the store to the requested number of blocks (the caller guarantees nothing above is referenced by any retained generation or pin).</summary>
		void Truncate(uint newBlockCount);

		/// <summary>Advisory: tells the storage that this block run holds nothing and its physical space may be released (a filesystem hole punch / device deallocate). Best effort - a pager or platform without the capability does nothing, and the bytes simply stay. The caller guarantees the run is unreferenced by every retained generation and pin.</summary>
		void PunchHole(uint firstBlock, uint count);

		/// <summary>Advisory: the caller is about to read this block run; start fetching it (read-ahead / <c>madvise(WILLNEED)</c>). Best effort - a pager without the capability does nothing. Turns a chain of one-at-a-time demand faults into overlapped device reads, which is the difference between QD1 latency per page and the drive's actual bandwidth.</summary>
		void Prefetch(uint firstBlock, uint count);

		/// <summary>Enables the first-touch tracking behind <see cref="MarkTouched"/> (on by default). Turn off to measure the raw read path: readers then skip checksum verification entirely.</summary>
		bool TrackFirstTouch { get; set; }

		/// <summary>Forgets every recorded touch, so the next read of ANY block pays first-touch checksum verification again. The measurement counterpart of <see cref="TrackFirstTouch"/>: off = pretend everything is verified, reset = pretend nothing is.</summary>
		void ResetFirstTouch();

		/// <summary>True exactly once per block since this pager opened; always false when <see cref="TrackFirstTouch"/> is off.</summary>
		/// <remarks>The gate of read-path verification: the caller that receives <c>true</c> performs the one-time checksum check of the page (or extent) starting at that block. Content the process writes afterwards is its own sealed bytes, so a block never needs re-verification within one open; rot that develops AFTER a block's first touch is the offline audit's job.</remarks>
		bool MarkTouched(uint firstBlock);

	}

	/// <summary>A reference to a run of blocks in a pager's own memory that a plain struct (a cursor) can hold across row accesses.</summary>
	/// <remarks>
	/// <para>Backed by a heap array (heap-pager regions, the writer overlay's buffered images) or by a raw pointer into a mapped region. It carries EXACTLY the lifetime of the span <see cref="IFdbLitePager.ReadBlocks"/> hands out: valid until the blocks are reused or truncated, which the engine's pin/horizon machinery guarantees for a pinned generation. Every state that would dangle this ref already dangles a plain <see cref="IFdbLitePager.ReadBlocks"/> span the same way.</para>
	/// <para><see cref="Span"/> is one branch and a span construction, no call: the reason this exists instead of a <see cref="ReadOnlyMemory{T}"/> (whose unmanaged backing would pay a virtual <c>GetSpan</c> per access, the very cost the cache removes).</para>
	/// </remarks>
	public readonly unsafe struct FdbLitePageRef
	{

		private readonly byte[]? Array;

		private readonly int Offset;

		private readonly byte* Pointer;

		private readonly int Length;

		public FdbLitePageRef(byte[] array, int offset, int length)
		{
			this.Array = array;
			this.Offset = offset;
			this.Length = length;
		}

		public FdbLitePageRef(byte* pointer, int length)
		{
			this.Pointer = pointer;
			this.Length = length;
		}

		public ReadOnlySpan<byte> Span
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Array is { } array ? array.AsSpan(this.Offset, this.Length) : new ReadOnlySpan<byte>(this.Pointer, this.Length);
		}

	}

	/// <summary>First-touch bitmap over a pager's block space (1 bit per block), safe for lock-free readers.</summary>
	/// <remarks>Growth swaps the array under a lock while marks stay lock-free, so a mark racing a growth can be lost - the only consequence is that the block verifies once more, which is benign by design.</remarks>
	internal sealed class FdbLiteTouchMap
	{

		private long[] Bits = [ ];

		private readonly object GrowLock = new();

		/// <summary>Forgets every touch, so the next read of ANY block pays first-touch verification again. Measurement aid (the deliberate cold-integrity scan); single-writer context assumed.</summary>
		public void Reset()
		{
			lock (this.GrowLock)
			{
				Array.Clear(Volatile.Read(ref this.Bits));
			}
		}

		/// <summary>True exactly once per block (best effort under concurrent growth): the caller that gets <c>true</c> owns the one-time verification.</summary>
		public bool MarkTouched(uint block)
		{
			int index = (int) (block >> 6);
			var bits = Volatile.Read(ref this.Bits);
			if (index >= bits.Length)
			{
				bits = GrowTo(index + 1);
			}
			long mask = 1L << (int) (block & 63);
			return (Interlocked.Or(ref bits[index], mask) & mask) == 0;
		}

		private long[] GrowTo(int minimumLength)
		{
			lock (this.GrowLock)
			{
				var current = Volatile.Read(ref this.Bits);
				if (minimumLength <= current.Length)
				{
					return current;
				}
				var grown = new long[Math.Max(minimumLength, current.Length * 2)];
				current.CopyTo(grown, 0);
				Volatile.Write(ref this.Bits, grown);
				return grown;
			}
		}

	}

}
