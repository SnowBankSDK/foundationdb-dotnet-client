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
	using System.Buffers;
	using SnowBank.Data.Binary;

	/// <summary>Buffer writer that writes all data into a slabs allocated from a pool (or the heap)</summary>
	/// <remarks>This buffer will allocate new slabs of memory as needed, and keep them alive until it is disposed or cleared.</remarks>
	/// <remarks>Slice allocated from this writer CAN still be used after this instance has been disposed or cleared.</remarks>
	/// <remarks>If you require all data to be consecutive in memory, use <see cref="ArraySliceWriter"/> instead.</remarks>
	/// <remarks>If all data allocated from this writer is guaranteed to not be used outside its lifetime, consider using <see cref="PooledSliceAllocator"/> for performance reasons.</remarks>
	[PublicAPI]
	public sealed class SlabSliceWriter : IBufferWriter<byte>, ISliceBufferWriter, ISpanEncodable
	{

		/// <summary>The current slab</summary>
		private byte[]? m_current;

		/// <summary>Index, in the current slab, of the next free byte</summary>
		private int m_index;

		/// <summary>Maximum size of slabs that we will allocate</summary>
		private readonly int m_maxSlabSize;

		/// <summary>Total number of bytes that were written in the slabs, excluding the current one</summary>
		private long m_slabWritten;

		/// <summary>List of previous allocated slabs (excluding the current one)</summary>
		/// <remarks>If <c>Pool</c> is non-null, then the <b>Buffer</b> should be return to it</remarks>
		private List<byte[]>? m_slabs;

		/// <summary>Default initial slab size (if not provided in the constructor)</summary>
		private const int DefaultSlabSize = 4 * 1024;

		/// <summary>Default spill size before allocating to the heap (when using a pool)</summary>
		private const int DefaultSpillSize = 1024 * 1024;

		/// <summary>Constructs a <see cref="SlabSliceWriter"/> using the default slab and spill sizes</summary>
		public SlabSliceWriter() : this(DefaultSlabSize, DefaultSpillSize)
		{ }


		/// <summary>Constructs a <see cref="SlabSliceWriter"/> using a custom initial slab and spill sizes</summary>
		public SlabSliceWriter(int initialSize) : this(initialSize, Math.Max(initialSize, DefaultSpillSize))
		{ }

		/// <summary>Constructs a <see cref="SlabSliceWriter"/>, in which data can be written to, with an initial capacity specified.</summary>
		/// <param name="initialSize">The initial capacity with which to initialize the underlying buffer.</param>
		/// <param name="spillSize">The maximum required size that will be served from the pool. Larger buffer size will be allocated from the heap</param>
		/// <exception cref="ArgumentException">
		/// Thrown when either <paramref name="initialSize"/> or <paramref name="spillSize"/> is not positive (i.e. less than or equal to 0).
		/// </exception>
		public SlabSliceWriter(int initialSize, int spillSize)
		{
			Contract.Positive(initialSize);
			Contract.Positive(spillSize);
			Contract.LessOrEqual(initialSize, spillSize, "Initial size cannot be greater than the spill size.");

			m_current = initialSize == 0 ? [ ] : new byte[initialSize];
			m_maxSlabSize = spillSize;
		}

		/// <summary>Returns the amount of data written to the underlying buffer so far.</summary>
		public long WrittenCount => m_slabWritten + m_index;

		/// <summary>Returns the amount of space available that can still be written into without forcing the underlying buffer to grow.</summary>
		public int FreeCapacity => (m_current?.Length - m_index) ?? 0;

		/// <summary>Maximum size of slabs allocated from the pool. Slabs with a size greater than this value will be allocated on the heap instead.</summary>
		public int MaxSlabSize => m_maxSlabSize;

		/// <summary>Clears the data written to the underlying buffers.</summary>
		/// <remarks>You must clear the <see cref="SlabSliceWriter"/> before trying to re-use it.</remarks>
		public void Clear()
		{
			if (m_current is null)
			{
				throw ThrowObjectDisposedException();
			}

			if (m_index > 0)
			{ // the current slab was used previously, replace it with a new one
				m_current = m_current.Length > 0 ? new byte[m_current.Length] : [ ];
				m_index = 0;
			}

			m_slabs?.Clear();
			m_slabWritten = 0;
		}

		/// <summary>Notifies the <see cref="ISliceBufferWriter" /> that <paramref name="count" /> data items were written to the output <see cref="Slice" />.</summary>
		/// <param name="count">The number of data items written to the <see cref="Slice" />.</param>
		public void Advance(int count)
		{
			Contract.Positive(count);
			var buffer = m_current ?? throw ThrowObjectDisposedException();

			if (m_index > buffer.Length - count)
			{
				throw ThrowInvalidOperationException_AdvancedTooFar();
			}

			m_index += count;
		}

		/// <summary>Returns a <see cref="ArraySegment{T}" /> to write to that is at least the requested size (specified by <paramref name="sizeHint" />).</summary>
		/// <param name="sizeHint">The minimum length of the returned <see cref="Slice" />. If 0, a non-empty buffer is returned.</param>
		/// <exception cref="T:System.OutOfMemoryException">The requested buffer size is not available.</exception>
		/// <returns>A <see cref="ArraySegment{T}" /> of at least the size <paramref name="sizeHint" />. If <paramref name="sizeHint" /> is 0, returns a non-empty buffer.</returns>
		public ArraySegment<byte> GetSlice(int sizeHint = 0)
		{
			Contract.Positive(sizeHint);
			var (buffer, owned) = CheckAndResizeBuffer(sizeHint, canSpill: false);

			if (!owned)
			{
				return new(buffer, 0, buffer.Length);
			}

			var index = m_index;
			Contract.Debug.Assert(buffer.Length > index);
			return new(buffer, index, buffer.Length - index);
		}

		/// <summary>Returns a <see cref="ArraySegment{T}" /> to write to that is exactly the requested size (specified by <paramref name="size" />) and advance the cursor.</summary>
		/// <param name="size">The exact length of the returned <see cref="Slice" />. If 0, a non-empty buffer is returned.</param>
		/// <exception cref="T:System.OutOfMemoryException">The requested buffer size is not available.</exception>
		/// <returns>A <see cref="ArraySegment{T}" /> of at exactly the <paramref name="size" /> requested.</returns>
		public ArraySegment<byte> Allocate(int size)
		{
			Contract.GreaterOrEqual(size, 0);

			var (buffer, owned) = CheckAndResizeBuffer(size, canSpill: true);
			if (!owned)
			{
				return new ArraySegment<byte>(buffer, 0, size);
			}

			var index = m_index;
			Contract.Debug.Assert(buffer.Length > index && buffer.Length >= index + size);
			m_index += size;
			return new(buffer, index, size);
		}

		/// <summary>
		/// Returns a <see cref="Memory{T}"/> to write to that is at least the requested length (specified by <paramref name="sizeHint"/>).
		/// If no <paramref name="sizeHint"/> is provided (or it's equal to <c>0</c>), some non-empty buffer is returned.
		/// </summary>
		/// <exception cref="ArgumentException">
		/// Thrown when <paramref name="sizeHint"/> is negative.
		/// </exception>
		/// <remarks>
		/// This will never return an empty <see cref="Memory{T}"/>.
		/// </remarks>
		/// <remarks>
		/// There is no guarantee that successive calls will return the same buffer or the same-sized buffer.
		/// </remarks>
		/// <remarks>
		/// You must request a new buffer after calling Advance to continue writing more data and cannot write to a previously acquired buffer.
		/// </remarks>
		public Memory<byte> GetMemory(int sizeHint = 0)
		{
			return GetSlice(sizeHint).AsMemory();
		}

		/// <summary>
		/// Returns a <see cref="Span{T}"/> to write to that is at least the requested length (specified by <paramref name="sizeHint"/>).
		/// If no <paramref name="sizeHint"/> is provided (or it's equal to <c>0</c>), some non-empty buffer is returned.
		/// </summary>
		/// <exception cref="ArgumentException">
		/// Thrown when <paramref name="sizeHint"/> is negative.
		/// </exception>
		/// <remarks>
		/// This will never return an empty <see cref="Span{T}"/>.
		/// </remarks>
		/// <remarks>
		/// There is no guarantee that successive calls will return the same buffer or the same-sized buffer.
		/// </remarks>
		/// <remarks>
		/// You must request a new buffer after calling Advance to continue writing more data and cannot write to a previously acquired buffer.
		/// </remarks>
		public Span<byte> GetSpan(int sizeHint = 0)
		{
			return GetSlice(sizeHint).AsSpan();
		}

		private (byte[] Buffer, bool Owned) CheckAndResizeBuffer(int sizeHint, bool canSpill)
		{
			Contract.Debug.Assert(sizeHint >= 0);
			var buffer = m_current ?? throw ThrowObjectDisposedException();

			if (sizeHint == 0)
			{
				sizeHint = 1;
			}

			if (sizeHint > this.FreeCapacity)
			{
				return GrowBufferSlow(buffer, sizeHint, canSpill);
			}

			Contract.Debug.Ensures(this.FreeCapacity > 0 && this.FreeCapacity >= sizeHint);
			return (buffer, true);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private (byte[] Buffer, bool Owned) GrowBufferSlow(byte[] current, int sizeHint, bool canSpill)
		{
			if (canSpill && sizeHint > m_maxSlabSize)
			{ // too large for the pool, or no pool => allocate from the heap
				current = new byte[sizeHint];
				(m_slabs ??= new ()).Add(current);
				m_slabWritten += sizeHint;
				return (current, false);
			}

			// try allocating larger slabs, until we reach the max spill size.
			// by default, we will double the size until we either reach the max slab size, or enough to satisfy the request
			long newSize = Math.Min(current.Length, 2048);
			do { newSize *= 2; } while (newSize < sizeHint);

			if (canSpill)
			{ // never exceed the spill size for our slabs
				newSize = Math.Min(newSize, m_maxSlabSize);
			}

			Contract.Debug.Assert(newSize >= sizeHint && newSize <= m_maxSlabSize);

			// allocate using the heap
			var newBuffer = new byte[(int) newSize];
			(m_slabs ??= new()).Add(current);

			m_current = newBuffer;
			m_slabWritten += m_index;
			m_index = 0;
			return (newBuffer, true);
		}

		#region ISpanEncodable...

		/// <inheritdoc />
		bool ISpanEncodable.TryGetSpan(out ReadOnlySpan<byte> span)
		{
			if (m_slabs is not null)
			{ // split into multiple slabs!
				span = default;
				return false;
			}

			span = m_current;
			return true;
		}

		/// <inheritdoc />
		bool ISpanEncodable.TryGetSizeHint(out int sizeHint)
		{
			long size = m_index;
			var slabs = m_slabs;
			if (slabs is not null)
			{
				foreach (var slab in slabs)
				{
					size += slab.Length;
				}
			}

			if (size > int.MaxValue)
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = unchecked((int) size);
			return true;
		}

		/// <inheritdoc />
		bool ISpanEncodable.TryEncode(Span<byte> destination, out int bytesWritten)
		{
			var current = m_current;
			if (current is null) throw ThrowObjectDisposedException();

			var index = m_index;
			var slabs = m_slabs;

			var buffer = destination;

			if (slabs is not null)
			{
				foreach (var slab in slabs)
				{
					if (!slab.AsSpan().TryCopyTo(buffer))
					{
						goto too_small;
					}
					buffer = buffer[slab.Length..];
				}
			}

			if (index > 0)
			{
				if (!current.AsSpan(0, index).TryCopyTo(buffer))
				{
					goto too_small;
				}
				buffer = buffer[index..];
			}

			bytesWritten = destination.Length - buffer.Length;
			return true;

		too_small:
			bytesWritten = 0;
			return false;

		}

		#endregion

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException ThrowInvalidOperationException_AdvancedTooFar()
		{
			return new InvalidOperationException("Buffer writer advanced too far");
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static ObjectDisposedException ThrowObjectDisposedException()
		{
			return new ObjectDisposedException("Buffer writer has already been disposed, or the buffer has already been acquired by someone else.");
		}

	}

}
