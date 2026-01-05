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
	using SnowBank.Data.Binary;
	using SnowBank.Data.Tuples.Binary;

	/// <summary>Buffer writer that writes all data into a single consecutive buffer allocated from the heap</summary>
	/// <remarks>
	/// <para>This buffer will grow and copy the underlying array as needed. For performance reason, prefer using <see cref="SlabSliceWriter"/> if you don't require the final output to be consecutive in memory</para>
	/// <para>If all data allocated from this writer is guaranteed to not be used outside its lifetime, consider using <see cref="PooledSliceWriter"/> for performance reasons.</para>
	/// </remarks>
	[PublicAPI]
	public sealed class ArraySliceWriter : ISliceBufferWriter, ISpanEncodable
	{
		private byte[]? m_buffer;
		private int m_index;

		private const int DefaultInitialBufferSize = 256;

		/// <summary>
		/// Creates an instance of an <see cref="ArraySliceWriter"/>, in which data can be written to,
		/// with the default initial capacity.
		/// </summary>
		public ArraySliceWriter() : this(0)
		{ }

		/// <summary>
		/// Creates an instance of an <see cref="ArraySliceWriter"/>, in which data can be written to,
		/// with an initial capacity specified.
		/// </summary>
		/// <param name="initialCapacity">The minimum capacity with which to initialize the underlying buffer.</param>
		/// <exception cref="ArgumentException">
		/// Thrown when <paramref name="initialCapacity"/> is not positive (i.e. less than or equal to 0).
		/// </exception>
		public ArraySliceWriter(int initialCapacity)
		{
			Contract.Positive(initialCapacity);

			if (initialCapacity == 0) initialCapacity = DefaultInitialBufferSize;

			m_buffer = initialCapacity <= 0 ? [ ] : new byte[initialCapacity];
			m_index = 0;
		}

		/// <summary>
		/// Returns the data written to the underlying buffer so far, as a <see cref="Slice"/>.
		/// </summary>
		public Slice WrittenSlice => m_buffer.AsSlice(0, m_index);

		/// <summary>
		/// Returns the data written to the underlying buffer so far, as a <see cref="ReadOnlyMemory{T}"/>.
		/// </summary>
		public ReadOnlySpan<byte> WrittenSpan => m_buffer.AsSpan(0, m_index);

		/// <summary>
		/// Returns the amount of data written to the underlying buffer so far.
		/// </summary>
		public int WrittenCount => m_index;

		/// <summary>
		/// Returns the total amount of space within the underlying buffer.
		/// </summary>
		public int Capacity => m_buffer?.Length ?? 0;

		/// <summary>
		/// Returns the amount of space available that can still be written into without forcing the underlying buffer to grow.
		/// </summary>
		public int FreeCapacity => m_buffer?.Length - m_index ?? 0;

		/// <summary>
		/// Clears the data written to the underlying buffer.
		/// </summary>
		/// <remarks>
		/// You must clear the <see cref="ArraySliceWriter"/> before trying to re-use it.
		/// </remarks>
		public void Clear()
		{
			var buffer = m_buffer ?? ThrowObjectDisposedException();
			Contract.Debug.Requires(buffer.Length >= m_index);
			buffer.AsSpan(0, m_index).Clear();
			m_index = 0;
		}

		/// <summary>Notifies the <see cref="ISliceBufferWriter" /> that <paramref name="count" /> data items were written to the output <see cref="Slice" />.</summary>
		/// <param name="count">The number of data items written to the <see cref="Slice" />.</param>
		public void Advance(int count)
		{
			Contract.Positive(count);
			var buffer = m_buffer ?? ThrowObjectDisposedException();

			if (m_index > buffer.Length - count)
			{
				ThrowInvalidOperationException_AdvancedTooFar();
			}

			m_index += count;
		}

		/// <summary>Returns an <see cref="ArraySegment{T}" /> to write to that is at least the requested size (specified by <paramref name="sizeHint" />).</summary>
		/// <param name="sizeHint">The minimum length of the returned <see cref="Slice" />. If 0, a non-empty buffer is returned.</param>
		/// <exception cref="T:System.OutOfMemoryException">The requested buffer size is not available.</exception>
		/// <returns>A <see cref="ArraySegment{T}" /> of at least the size <paramref name="sizeHint" />. If <paramref name="sizeHint" /> is 0, returns a non-empty buffer.</returns>
		/// <remarks>If the requested size exceeds the <see cref="FreeCapacity"/>, the internal buffer will be resized</remarks>
		public ArraySegment<byte> GetSlice(int sizeHint = 0)
		{
			Contract.Positive(sizeHint);
			var buffer = CheckAndResizeBuffer(sizeHint);
			var index = m_index;
			Contract.Debug.Assert(buffer.Length > index);
			return new(buffer, index, buffer.Length - index);
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
			Contract.Positive(sizeHint);
			var buffer = CheckAndResizeBuffer(sizeHint);
			Contract.Debug.Assert(buffer.Length > m_index);
			return buffer.AsMemory(m_index);
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
			Contract.Positive(sizeHint);
			var buffer = CheckAndResizeBuffer(sizeHint);
			Contract.Debug.Assert(buffer.Length > m_index);
			return buffer.AsSpan(m_index);
		}

		private byte[] CheckAndResizeBuffer(int sizeHint)
		{
			Contract.Debug.Requires(sizeHint >= 0);
			var buffer = m_buffer ?? ThrowObjectDisposedException();

			if (sizeHint == 0)
			{
				sizeHint = 1;
			}

			if (sizeHint > this.FreeCapacity)
			{
				// attempt to double the size, or use the size hint if it is larger than that
				int growBy = Math.Max(sizeHint, buffer.Length);
				if (buffer.Length == 0)
				{
					growBy = Math.Max(growBy, DefaultInitialBufferSize);
				}
				int newSize = checked(buffer.Length + growBy);

				// allocate from the heap
				var newBuffer = new byte[newSize];
				buffer.AsSpan().CopyTo(newBuffer);
				m_buffer = newBuffer;
				buffer = newBuffer;
			}

			Contract.Debug.Ensures(this.FreeCapacity > 0 && this.FreeCapacity >= sizeHint);
			return buffer;
		}

		/// <summary>Copies the content of the buffer into the specified destination</summary>
		public void CopyTo(Span<byte> output)
		{
			if (!TryCopyTo(output))
			{
				throw new ArgumentException("Output buffer is too small.", nameof(output));
			}
		}

		/// <summary>Copies the content of the buffer into the specified destination</summary>
		public void CopyTo(Span<byte> output, out int written)
		{
			if (!TryCopyTo(output, out written))
			{
				throw new ArgumentException("Output buffer is too small.", nameof(output));
			}
		}

		/// <summary>Copies the content of the buffer into the specified destination, if it is large enough</summary>
		public bool TryCopyTo(Span<byte> output)
		{
			var buffer = m_buffer ?? ThrowObjectDisposedException();
			return buffer.AsSpan(m_index).TryCopyTo(output);
		}

		/// <summary>Copies the content of the buffer into the specified destination, if it is large enough</summary>
		public bool TryCopyTo(Span<byte> output, out int written)
		{
			var buffer = m_buffer ?? ThrowObjectDisposedException();
			written = buffer.Length;
			return buffer.AsSpan(m_index).TryCopyTo(output);
		}

		#region ISpanEncodable...

		/// <inheritdoc />
		bool ISpanEncodable.TryGetSpan(out ReadOnlySpan<byte> span)
		{
			var buffer = m_buffer;
			if (buffer is null)
			{
				span = default;
				return false;
			}

			var pos = m_index;
			span = pos == 0 ? default : new(buffer, 0, pos);
			return true;
		}

		/// <inheritdoc />
		bool ISpanEncodable.TryGetSizeHint(out int sizeHint)
		{
			if (m_buffer is null)
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = m_index;
			return true;
		}

		/// <inheritdoc />
		bool ISpanEncodable.TryEncode(Span<byte> destination, out int bytesWritten)
		{
			var buffer = m_buffer ?? ThrowObjectDisposedException();

			var chunk = buffer.AsSpan(0, m_index);
			if (!chunk.TryCopyTo(destination))
			{
				bytesWritten = 0;
				return false;
			}

			bytesWritten = chunk.Length;
			return true;
		}

		#endregion

		[DoesNotReturn, StackTraceHidden]
		private static void ThrowInvalidOperationException_AdvancedTooFar()
		{
			throw new InvalidOperationException("Buffer writer advanced too far");
		}

		[DoesNotReturn, StackTraceHidden]
		private static byte[] ThrowObjectDisposedException()
		{
			throw new ObjectDisposedException("Buffer writer has already been disposed, or the buffer has already been acquired by someone else.");
		}

		/// <summary>Releases the memory allocated by this instance</summary>
		public void Dispose()
		{
			m_buffer = null;
			m_index = 0;
		}

	}

}
