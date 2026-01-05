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

//#define FULL_DEBUG // enable to diagnose allocations of this type

namespace SnowBank.Buffers
{
	using System.Buffers;

	/// <summary>Allocator that stores all data into slabs allocated from a pool (or the heap for large allocations)</summary>
	/// <remarks>
	/// <para>This instance <b>MUST</b> be disposed in order to return all slabs to the pool. Failing to do so will hinder the performance of the pool!</para>
	/// <para>This buffer will allocate new slabs of memory as needed, and keep them alive until it is disposed or cleared.</para>
	/// <para>Slice allocated from this writer <b>MUST NOT</b> be used after this instance has been disposed or cleared!</para>
	/// <para>If you require all allocated data to survive this instance, use <see cref="ArraySliceAllocator"/> instead.</para>
	/// </remarks>
	[PublicAPI]
	public sealed class PooledSliceAllocator : ISliceAllocator
	{

		/// <summary>The current slab</summary>
		private byte[]? m_current;

		/// <summary>Index, in the current slab, of the next free byte</summary>
		private int m_index;

		/// <summary>Maximum size of slabs that we will allocate</summary>
		private readonly int m_maxSlabSize;

		/// <summary>Optional pool used to allocate slabs. Will allocate from the heap if null (or too large)</summary>
		private readonly ArrayPool<byte> m_pool;

		/// <summary>Total number of bytes that were written in the slabs, excluding the current one</summary>
		private long m_slabWritten;

		/// <summary>List of previous allocated slabs (excluding the current one and large allocations not from the pool)</summary>
		private List<byte[]>? m_slabs;

		/// <summary>Default slab size (if not provided in the constructor)</summary>
		private const int DefaultSlabSize = 4 * 1024;

		/// <summary>
		/// Creates an instance of a <see cref="PooledSliceAllocator"/>, in which data can be written to, with an initial capacity specified.
		/// </summary>
		public PooledSliceAllocator() : this(DefaultSlabSize, null)
		{ }

		/// <summary>
		/// Creates an instance of a <see cref="PooledSliceAllocator"/>, in which data can be written to, with an initial capacity specified.
		/// </summary>
		public PooledSliceAllocator(int slabSize) : this(slabSize, null)
		{ }

		/// <summary>
		/// Creates an instance of a <see cref="PooledSliceAllocator"/>, in which data can be written to, with an initial capacity specified.
		/// </summary>
		public PooledSliceAllocator(ArrayPool<byte>? pool) : this(DefaultSlabSize, pool)
		{ }

		/// <summary>
		/// Creates an instance of a <see cref="PooledSliceAllocator"/>, in which data can be written to, with an initial capacity specified.
		/// </summary>
		/// <param name="slabSize">The initial capacity with which to initialize the underlying buffer.</param>
		/// <param name="pool">If provided, used to rent the slabs. If null, slabs will be allocated from the heap.</param>
		/// <exception cref="ArgumentException">
		/// Thrown when <paramref name="slabSize"/> is not positive (i.e. less than or equal to 0).
		/// </exception>
		public PooledSliceAllocator(int slabSize, ArrayPool<byte>? pool)
		{
			Contract.GreaterThan(slabSize, 0, "Slab size must be greater than zero");

			m_pool = pool ?? ArrayPool<byte>.Shared;
			m_current = m_pool.Rent(slabSize);
			m_maxSlabSize = slabSize;
		}

#if FULL_DEBUG
		private readonly string m_allocationStackTrace = Environment.StackTrace;

		~PooledSliceAllocator()
		{
			if (System.Diagnostics.Debugger.IsAttached)
			{
				// if you break here, then an instance of this type was not disposed properly!
				// this type MUST be disposed in order from allocated slabs to be returned to the pool!
				// => the stacktrace of the allocation of this instance is stored in the m_allocationStackTrace field!
				System.Diagnostics.Debugger.Break();
			}
		}
#endif

		/// <summary>
		/// Returns the amount of data written to the underlying buffer so far.
		/// </summary>
		public long TotalAllocated => m_slabWritten + m_index;

		/// <summary>
		/// Returns the amount of space available that can still be written into without forcing the underlying buffer to grow.
		/// </summary>
		public int FreeCapacity => (m_current?.Length - m_index) ?? 0;

		/// <summary>
		/// Maximum size of slabs allocated from the pool. Slabs with a size greater than this value will be allocated on the heap instead.
		/// </summary>
		public int MaxSlabSize => m_maxSlabSize;

		/// <summary>
		/// Clears the data written to the underlying buffers.
		/// </summary>
		/// <remarks>
		/// You must clear the <see cref="ArraySliceWriter"/> before trying to re-use it.
		/// </remarks>
		public void Clear()
		{
			var buffer = m_current ?? throw ThrowObjectDisposedException();
			Contract.Debug.Requires(buffer.Length >= m_index);

			// keep the current buffer
			buffer.AsSpan(0, m_index).Clear();

			if (m_slabs != null)
			{
				// return all pooled slabs
				var pool = m_pool;
				foreach (var slab in m_slabs)
				{
					pool.Return(slab);
				}

				m_slabs.Clear();
			}
			m_index = 0;
			m_slabWritten = 0;
		}

		/// <summary>Returns a <see cref="ArraySegment{T}" /> to write to that is exactly the requested size (specified by <paramref name="size" />) and advance the cursor.</summary>
		/// <param name="size">The exact length of the returned <see cref="Slice" />. If 0, a non-empty buffer is returned.</param>
		/// <exception cref="T:System.OutOfMemoryException">The requested buffer size is not available.</exception>
		/// <returns>A <see cref="ArraySegment{T}" /> of at exactly the <paramref name="size" /> requested.</returns>
		public ArraySegment<byte> Allocate(int size)
		{
			Contract.GreaterOrEqual(size, 0);

			var (buffer, owned) = CheckAndResizeBuffer(size);
			if (!owned)
			{
				return new(buffer, 0, size);
			}

			var index = m_index;
			Contract.Debug.Assert(buffer.Length > index && buffer.Length >= index + size);
			m_index += size;
			return new(buffer, index, size);
		}

		private (byte[] Buffer, bool Owned) CheckAndResizeBuffer(int sizeHint)
		{
			Contract.Debug.Assert(sizeHint >= 0);
			var buffer = m_current ?? throw ThrowObjectDisposedException();

			if (sizeHint == 0)
			{
				sizeHint = 1;
			}

			if (sizeHint > this.FreeCapacity)
			{
				return GrowBufferSlow(buffer, sizeHint);
			}

			Contract.Debug.Ensures(this.FreeCapacity > 0 && this.FreeCapacity >= sizeHint);
			return (buffer, true);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private (byte[] Buffer, bool Owned) GrowBufferSlow(byte[] current, int sizeHint)
		{
			if (sizeHint > m_maxSlabSize)
			{ // too large for the pool, or no pool => allocate from the heap
				current = new byte[sizeHint];
				// do not keep track of this, only the size
				m_slabWritten += sizeHint;
				return (current, false);
			}

			// try allocating larger slabs, until we reach the max spill size.
			// by default, we will double the size until we either reach the max slab size, or enough to satisfy the request
			long newSize = Math.Min(current.Length, 2048);
			do { newSize *= 2; } while (newSize < sizeHint);

			// never exceed the spill size for our slabs
			newSize = Math.Min(newSize, m_maxSlabSize);

			Contract.Debug.Assert(newSize >= sizeHint && newSize <= m_maxSlabSize);

			// allocate using the pool
			var newBuffer = m_pool.Rent((int) newSize);
			// keep track of the previous buffer, we need to return it to the pool later
			(m_slabs ??= new()).Add(current);
			m_slabWritten += m_index;

			m_current = newBuffer;
			m_index = 0;
			return (newBuffer, true);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static ObjectDisposedException ThrowObjectDisposedException()
		{
			return new ObjectDisposedException("Buffer writer has already been disposed, or the buffer has already been acquired by someone else.");
		}

		/// <summary>Clears the writer, and returns all the slabs to the pool</summary>
		public void Dispose()
		{
			var pool = m_pool;

			// return all pooled slabs
			if (m_slabs != null)
			{
				foreach (var slab in m_slabs)
				{
					pool.Return(slab);
				}

				m_slabs.Clear();
				m_slabWritten = 0;
			}

			// return the current slab
			if (m_current is not null)
			{
				pool.Return(m_current);
				m_current = null;
				m_index = 0;
			}

#if FULL_DEBUG
			GC.SuppressFinalize(this);
#endif
		}

	}

}
