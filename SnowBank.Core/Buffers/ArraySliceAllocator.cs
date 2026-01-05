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

namespace SnowBank.Buffers
{
	/// <summary>Allocator that stores all data into slabs allocated from the heap.</summary>
	/// <remarks>
	/// <para>This buffer will allocate new slabs of memory as needed.</para>
	/// <para>Slices allocated from this writer <b>CAN</b> be used after this instance has been disposed or cleared.</para>
	/// <para>If you can guarantee that no slice allocated will survive this instance, you can also use <see cref="PooledSliceAllocator"/> which can rent memory from a pool.</para>
	/// </remarks>
	[PublicAPI]
	public sealed class ArraySliceAllocator : ISliceAllocator
	{

		/// <summary>The current slab</summary>
		private byte[]? m_current;

		/// <summary>Index, in the current slab, of the next free byte</summary>
		private int m_index;

		/// <summary>Maximum size of slabs that we will allocate</summary>
		private readonly int m_maxSlabSize;

		/// <summary>Total number of bytes that were written in the slabs, excluding the current one</summary>
		private long m_slabWritten;

		/// <summary>Default initial slab size (if not provided in the constructor)</summary>
		private const int DefaultSlabSize = 4 * 1024;

		/// <summary>Default spill size before allocating to the heap (when using a pool)</summary>
		private const int DefaultSpillSize = 1024 * 1024;

		/// <summary>Constructs a new slice allocator using the default slab and spill sizes</summary>
		public ArraySliceAllocator() : this(DefaultSlabSize, DefaultSpillSize)
		{ }

		/// <summary>Constructs a new slice allocator using custom slab and spill sizes</summary>
		/// <param name="initialSize"></param>
		public ArraySliceAllocator(int initialSize) : this(initialSize, Math.Max(initialSize, DefaultSpillSize))
		{ }

		/// <summary>
		/// Creates an instance of a <see cref="ArraySliceAllocator"/>, in which data can be written to, with an initial capacity specified.
		/// </summary>
		/// <param name="initialSize">The initial capacity with which to initialize the underlying buffer.</param>
		/// <param name="spillSize">The maximum required size that will be served from the pool. Larger buffer size will be allocated from the heap</param>
		/// <exception cref="ArgumentException">
		/// Thrown when either <paramref name="initialSize"/> or <paramref name="spillSize"/> is not positive (i.e. less than or equal to 0).
		/// </exception>
		public ArraySliceAllocator(int initialSize, int spillSize)
		{
			Contract.Positive(initialSize);
			Contract.Positive(spillSize);
			Contract.LessOrEqual(initialSize, spillSize, "Initial size cannot be greater than the spill size.");

			m_current = initialSize == 0 ? [ ] : new byte[initialSize];
			m_maxSlabSize = spillSize;
		}

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

			// keep the current buffer unless it has been written to
			if (m_index > 0)
			{
				m_current = new byte[buffer.Length];
				m_index = 0;
			}
			m_slabWritten = 0;
		}

		/// <summary>Returns a <see cref="ArraySegment{T}" /> to write to that is exactly the requested size (specified by <paramref name="size" />) and advance the cursor.</summary>
		/// <param name="size">The exact length of the returned <see cref="Slice" />. If 0, a non-empty buffer is returned.</param>
		/// <exception cref="T:System.OutOfMemoryException">The requested buffer size is not available.</exception>
		/// <returns>A <see cref="ArraySegment{T}" /> of at exactly the <paramref name="size" /> requested.</returns>
		public ArraySegment<byte> Allocate(int size)
		{
			Contract.GreaterOrEqual(size, 0);

			var buffer = m_current ?? throw ThrowObjectDisposedException();

			if (size > this.MaxSlabSize)
			{ // too large, use a dedicated buffer
				return new(new byte[size], 0, size);
			}

			if (buffer.Length - m_index < size)
			{ // need to resize!
				buffer = GrowBufferSlow(buffer, size);
			}

			var index = m_index;
			Contract.Debug.Assert(buffer.Length > index && buffer.Length >= index + size);
			m_index = index + size;
			return new(buffer, index, size);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private byte[] GrowBufferSlow(byte[] current, int sizeHint)
		{
			// try allocating larger slabs, until we reach the max spill size.
			// by default, we will double the size until we either reach the max slab size, or enough to satisfy the request
			long newSize = Math.Min(current.Length, 2048);
			do { newSize *= 2; } while (newSize < sizeHint);

			// never exceed the spill size for our slabs
			newSize = Math.Min(newSize, m_maxSlabSize);

			Contract.Debug.Assert(newSize >= sizeHint && newSize <= m_maxSlabSize);

			// allocate using the pool
			var newBuffer = new byte[(int) newSize];

			m_slabWritten += m_index;

			m_current = newBuffer;
			m_index = 0;
			return newBuffer;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static ObjectDisposedException ThrowObjectDisposedException()
		{
			return new("Buffer writer has already been disposed, or the buffer has already been acquired by someone else.");
		}

		/// <inheritdoc />
		public void Dispose()
		{
			m_current = null;
			m_index = 0;
			m_slabWritten = 0;
		}

	}

}
