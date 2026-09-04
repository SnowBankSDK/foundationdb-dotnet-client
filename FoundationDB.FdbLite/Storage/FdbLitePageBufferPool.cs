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
	using System.Collections.Concurrent;

	/// <summary>Process-wide pool of page-image buffers, shared by every engine with the same page size.</summary>
	/// <remarks>
	/// <para>Per-engine pooling made every short-lived engine (a benchmark run, a tool, a test) pay the whole peak-dirty-set warm-up in fresh allocations: measured at ~1.8 GB of <c>byte[]</c> over one profiling session whose engines each started with an empty pool. Sharing per page size keeps the warm-up a once-per-process cost.</para>
	/// <para>A rented buffer is UNINITIALIZED: every page image is written whole (<see cref="FdbLiteTreeWriter"/>'s WritePage copies a full page over it) before anything reads it, so the zeroing of <c>new byte[]</c> was pure waste. Any new consumer must keep that contract.</para>
	/// <para>Deliberately uncapped, like the per-engine pool before it: a cap below the dirty-set size just moves the allocations past the cap (a 256-buffer cap measured as 5.3 GB still allocated on a standard-scale sweep). Retained memory is bounded by the peak CONCURRENT demand across engines, which is what the process genuinely needed at its high-water mark.</para>
	/// </remarks>
	public sealed class FdbLitePageBufferPool
	{

		private static readonly ConcurrentDictionary<int, FdbLitePageBufferPool> ByPageSize = new();

		/// <summary>The process-wide pool for <paramref name="pageSize"/>-byte page images.</summary>
		public static FdbLitePageBufferPool Shared(int pageSize)
		{
			Contract.Positive(pageSize);
			return ByPageSize.GetOrAdd(pageSize, static size => new(size));
		}

		private FdbLitePageBufferPool(int pageSize)
		{
			this.PageSize = pageSize;
		}

		/// <summary>Length of every buffer this pool hands out.</summary>
		public int PageSize { get; }

		private ConcurrentStack<byte[]> Buffers { get; } = new();

		/// <summary>Buffers this pool allocated because no recycled one was available (diagnostics: the pool's warm-up cost).</summary>
		public long AllocatedTotal => Volatile.Read(ref this.Allocated);

		private long Allocated;

		/// <summary>A page-image buffer, recycled when one is available. UNINITIALIZED on a miss: the caller must write it whole before anything reads it.</summary>
		public byte[] Rent()
		{
			if (this.Buffers.TryPop(out var buffer))
			{
				return buffer;
			}
			Interlocked.Increment(ref this.Allocated);
			return GC.AllocateUninitializedArray<byte>(this.PageSize);
		}

		/// <summary>Recycles a buffer. The caller must not touch it afterwards.</summary>
		public void Return(byte[] buffer)
		{
			Contract.Debug.Requires(buffer.Length == this.PageSize);
			this.Buffers.Push(buffer);
		}

	}

}
