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
	using System.ComponentModel;
	using SnowBank.Buffers.Binary;

	/// <summary>Buffer that can be used to efficiently store multiple slices into as few chunks as possible</summary>
	/// <remarks>
	/// This class is useful to centralize a lot of temporary slices whose lifetime is linked to a specific operation. Dropping the reference to the buffer will automatically reclaim all the slices that were stored with it.
	/// This class is not thread safe.
	/// </remarks>
	[DebuggerDisplay("Pos={m_pos}, Remaining={m_remaining}, PageSize={m_pageSize}, Size={Size}, Allocated={Allocated}")]
	[PublicAPI]
	public sealed class SliceBuffer : IBufferWriter<byte>
	{
		private const int DefaultPageSize = 256;
		private const int MaxPageSize = 64 * 1024; // 64KB (small enough to not go into the LOH)

		/// <summary>Default initial size of pages (doubled every time until it reached the max page size)</summary>
		private int m_pageSize;
		/// <summary>Current buffer</summary>
		private byte[]? m_current;
		/// <summary>Position of the next free slot in the current buffer</summary>
		private int m_pos;
		/// <summary>Number of bytes remaining in the current buffer</summary>
		private int m_remaining;
		/// <summary>If non-null, list of previously used buffers (excluding the current buffer)</summary>
		private List<(byte[] Buffer, int Used)>? m_chunks;
		/// <summary>Running total of the length of all previously used buffers, excluding the size of the current buffer</summary>
		private int m_allocated;
		/// <summary>Running total of the number of bytes stored in the previously used buffers, excluding the size of the current buffer</summary>
		private int m_used;
		/// <summary>Pool for buffers</summary>
		private readonly ArrayPool<byte>? m_pool;

		/// <summary>Create a new slice buffer with the default page size</summary>
		public SliceBuffer()
			: this(0)
		{ }

		/// <summary>Create a new slice buffer with the specified page size</summary>
		/// <param name="pageSize">Initial page size</param>
		/// <param name="pool"></param>
		public SliceBuffer(int pageSize, ArrayPool<byte>? pool = null)
		{
			if (pageSize < 0) throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size cannot be less than zero");
			m_pageSize = pageSize == 0 ? DefaultPageSize : BitHelpers.AlignPowerOfTwo(pageSize, powerOfTwo: 16);
			m_pool = pool;
		}

		/// <summary>Gets the number of bytes used by all the slice allocated in this buffer</summary>
		public int Size => m_used + m_pos;

		/// <summary>Gets the total memory size allocated to store all the slices in this buffer</summary>
		public int Allocated => m_allocated + m_pos + m_remaining;

		/// <summary>Number of memory pages used by this buffer</summary>
		public int PageCount => m_chunks?.Count + 1 ?? 1;

		/// <summary>Return the list of all the pages used by this buffer</summary>
		/// <returns>Array of pages used by the buffer</returns>
		public Slice[] GetPages()
		{
			var pages = new Slice[this.PageCount];
			int p = 0;
			if (m_chunks is not null)
			{
				foreach (var chunk in m_chunks)
				{
					pages[p++] = chunk.Buffer.AsSlice(0, chunk.Used);
				}
			}
			pages[^1] = m_current.AsSlice(0, m_pos);
			return pages;
		}

		/// <summary>Allocate an empty space in the buffer</summary>
		/// <param name="count">Number of bytes to allocate</param>
		/// <param name="aligned">If true, align the start of the slice with the default padding size.</param>
		/// <returns>Slice pointing to a space in the buffer</returns>
		/// <remarks>There is NO guarantees that the allocated slice will be pre-filled with zeroes.</remarks>
		private Slice AllocateUnsafe(int count, bool aligned = false)
		{
			if (count < 0) throw new ArgumentException("Cannot allocate less than zero bytes.", nameof(count));

			const int ALIGNMENT = 4;

			if (count == 0)
			{
				return default;
			}

			int p = m_pos;
			int r = m_remaining;
			int extra = aligned ? (ALIGNMENT - (p & (ALIGNMENT - 1))) : 0;
			if (count + extra > r)
			{ // does not fit
				return AllocateUnsafeFallback(count);
			}

			Contract.Debug.Assert(m_current != null && m_pos >= 0);
			m_pos = p + (count + extra);
			m_remaining = r - (count + extra);
			Contract.Debug.Ensures(m_remaining >= 0);
			//note: we rely on the fact that the buffer was pre-filled with zeroes
			return m_current.AsSlice(p + extra, count);
		}

		private Slice AllocateUnsafeFallback(int count)
		{
			// keys that are too large are best kept in their own chunks
			if (count > (m_pageSize >> 1))
			{
				var array = m_pool?.Rent(count) ?? new byte[count];
				Keep(array, count);
				return array.AsSlice(0, count);
			}

			int pageSize = m_pageSize;

			// double the page size on each new allocation
			if (m_current != null)
			{
				if (m_pos > 0) Keep(m_current, m_pos);
				pageSize <<= 1;
				if (pageSize > MaxPageSize) pageSize = MaxPageSize;
				m_pageSize = pageSize;
			}

			var buffer = m_pool?.Rent(pageSize) ?? new byte[pageSize];
			m_current = buffer;
			m_pos = count;
			m_remaining = pageSize - count;

			return buffer.AsSlice(0, count);
		}

		/// <summary>
		/// Returns a <see cref="Span{T}">Span&lt;byte&gt;</see> to write to that is at least the requested length (specified by <paramref name="sizeHint"/>).
		/// If no <paramref name="sizeHint"/> is provided (or it's equal to <code>0</code>), some non-empty buffer is returned.
		/// </summary>
		/// <param name="sizeHint">Minimum size required, or <c>0</c> to return all the remaining allocated space</param>
		/// <returns>Span that can hold at least <paramref name="sizeHint"/> bytes (if <paramref name="sizeHint"/> is greater than <c>0</c>), or all the space remaining in the buffer (if <paramref name="sizeHint"/> is <c>0</c>)</returns>
		/// <exception cref="ArgumentException"> <paramref name="sizeHint"/> is negative.</exception>
		public Span<byte> GetSpan(int sizeHint = 0)
		{
			if (sizeHint < 0) throw new ArgumentException("Cannot allocate less than zero bytes.", nameof(sizeHint));

			int p = m_pos;
			if (sizeHint > m_remaining)
			{ // does not fit
				return GetSpanFallback(sizeHint);
			}

			//note: we rely on the fact that the buffer was pre-filled with zeroes
			return m_current.AsSpan(p);
		}
		
		private Span<byte> GetSpanFallback(int count)
		{
			if (count > MaxPageSize)
			{
				throw new InvalidOperationException("Cannot return a span that is larger than the maximum allowed page size");
			}

			// keys that are too large are best kept in their own chunks

			int pageSize = m_pageSize;
			if (count > pageSize * 2)
			{
				// allocate a larger page just once
			}
			else if (count > pageSize)
			{ // double the page size
				pageSize *= 2;
				pageSize = Math.Min(pageSize, MaxPageSize); // already checked for overflow at the start
				count = pageSize;
			}
			else
			{
				count = pageSize;
			}

			if (m_current != null)
			{
				if (m_pos > 0)
				{
					Keep(m_current, m_pos);
				}
				else
				{ // we did not use the current buffer, return it to the pool
					m_pool?.Return(m_current);
				}
			}
			m_pageSize = pageSize;

			// switch to the new buffer, but keeps at the start of the buffer
			var buffer = m_pool?.Rent(count) ?? new byte[count];
			m_current = buffer;
			m_pos = 0;
			m_remaining = buffer.Length;

			return buffer.AsSpan();
		}

		/// <summary>
		/// Returns a <see cref="Memory{T}">Memory&lt;byte&gt;</see> to write to that is at least the requested length (specified by <paramref name="sizeHint"/>).
		/// If no <paramref name="sizeHint"/> is provided (or it's equal to <code>0</code>), some non-empty buffer is returned.
		/// </summary>
		/// <param name="sizeHint">Minimum size required, or <c>0</c> to return all the remaining allocated space</param>
		/// <returns>Memory that can hold at least <paramref name="sizeHint"/> bytes (if <paramref name="sizeHint"/> is greater than <c>0</c>), or all the space remaining in the buffer (if <paramref name="sizeHint"/> is <c>0</c>)</returns>
		/// <exception cref="ArgumentException"> <paramref name="sizeHint"/> is negative.</exception>
		public Memory<byte> GetMemory(int sizeHint = 0)
		{
			if (sizeHint < 0) throw new ArgumentException("Cannot allocate less than zero bytes.", nameof(sizeHint));

			int p = m_pos;
			if (sizeHint > m_remaining)
			{ // does not fit
				return GetMemoryFallback(sizeHint);
			}

			//note: we rely on the fact that the buffer was pre-filled with zeroes
			return m_current.AsMemory(p);
		}
		
		private Memory<byte> GetMemoryFallback(int count)
		{
			if (count > MaxPageSize)
			{
				throw new InvalidOperationException("Cannot return a span that is larger than the maximum allowed page size");
			}

			// keys that are too large are best kept in their own chunks

			int pageSize = m_pageSize;
			if (count > pageSize * 2)
			{
				// allocate a larger page just once
			}
			else if (count > pageSize)
			{ // double the page size
				pageSize *= 2;
				pageSize = Math.Min(pageSize, MaxPageSize); // already checked for overflow at the start
				count = pageSize;
			}
			else
			{
				count = pageSize;
			}

			if (m_current != null)
			{
				if (m_pos > 0)
				{
					Keep(m_current, m_pos);
				}
				else
				{ // we did not use the current buffer, return it to the pool
					m_pool?.Return(m_current);
				}
			}
			m_pageSize = pageSize;

			// switch to the new buffer, but keeps at the start of the buffer
			var buffer = m_pool?.Rent(count) ?? new byte[count];
			m_current = buffer;
			m_pos = 0;
			m_remaining = buffer.Length;

			return buffer.AsMemory();
		}

		/// <summary>Notifies <see cref="SliceBuffer"/> that <paramref name="count"/> amount of data was written to the output <see cref="Span{T}"/> returned by a previous call to <see cref="GetSpan"/></summary>
		/// <param name="count">Number of bytes written</param>
		/// <returns>Slice with the data that was written</returns>
		/// <exception cref="InvalidOperationException"></exception>
		public Slice Advance(int count)
		{
			int p = m_pos;
			int r = m_remaining;

			if (count > r)
			{
				throw new InvalidOperationException("Cannot advance more than the remaining allocated capacity.");
			}
			m_pos = p + count;
			m_remaining = r - count;
			return m_current.AsSlice(p, count);
		}

		/// <inheritdoc />
		void IBufferWriter<byte>.Advance(int count) => Advance(count);

		/// <summary>Allocate an empty space in the buffer</summary>
		/// <param name="count">Number of bytes to allocate</param>
		/// <param name="aligned">If true, align the start of the slice with the default padding size.</param>
		/// <returns>Slice pointing to a space in the buffer</returns>
		/// <remarks>There is NO guarantees that the allocated slice will be pre-filled with zeroes.</remarks>
		public Span<byte> AllocateSpan(int count, bool aligned = false)
		{
			if (count < 0) throw new ArgumentException("Cannot allocate less than zero bytes.", nameof(count));

			const int ALIGNMENT = 4;

			if (count == 0)
			{
				return default;
			}

			int p = m_pos;
			int r = m_remaining;
			int extra = aligned ? (ALIGNMENT - (p & (ALIGNMENT - 1))) : 0;
			if (count + extra > r)
			{ // does not fit
				return AllocateSpanFallback(count);
			}

			Contract.Debug.Assert(m_current != null && m_pos >= 0);
			m_pos = p + (count + extra);
			m_remaining = r - (count + extra);
			Contract.Debug.Ensures(m_remaining >= 0);
			//note: we rely on the fact that the buffer was pre-filled with zeroes
			return m_current.AsSpan(p + extra, count);
		}

		private Span<byte> AllocateSpanFallback(int count)
		{
			// keys that are too large are best kept in their own chunks
			if (count > (m_pageSize >> 1))
			{
				var array = m_pool?.Rent(count) ?? new byte[count];
				Keep(array, count);
				return array.AsSpan(0, count);
			}

			int pageSize = m_pageSize;

			// double the page size on each new allocation
			if (m_current != null)
			{
				if (m_pos > 0) Keep(m_current, m_pos);
				pageSize <<= 1;
				if (pageSize > MaxPageSize) pageSize = MaxPageSize;
				m_pageSize = pageSize;
			}

			var buffer = m_pool?.Rent(pageSize) ?? new byte[pageSize];
			m_current = buffer;
			m_pos = count;
			m_remaining = pageSize - count;

			return buffer.AsSpan(0, count);
		}

		/// <summary>Copy a slice into the buffer, with optional alignment, and return a new identical slice.</summary>
		/// <param name="data">Data to copy to the buffer</param>
		/// <param name="aligned">If true, align the index of first byte of the slice with a multiple of 8 bytes</param>
		/// <returns>Slice that is the equivalent of <paramref name="data"/>, backed by the buffer.</returns>
		public Slice Intern(ReadOnlySpan<byte> data, bool aligned = false)
		{
			if (data.Length == 0)
			{
				// transform into the corresponding Slice.Empty singleton
				return Slice.Empty;
			}

			// allocate the slice
			var slice = AllocateUnsafe(data.Length, aligned);
			// DON'T TRY THIS AT HOME!
			data.CopyTo(slice.Array.AsSpan(slice.Offset, slice.Count));
			return slice;
		}

		/// <summary>Copy a slice into the buffer, with optional alignment, and return a new identical slice.</summary>
		/// <param name="data">Data to copy to the buffer</param>
		/// <param name="aligned">If true, align the index of first byte of the slice with a multiple of 8 bytes</param>
		/// <returns>Slice that is the equivalent of <paramref name="data"/>, backed by the buffer.</returns>
		public Slice Intern(Slice data, bool aligned = false)
		{
			if (data.Count == 0)
			{
				// transform into the corresponding Slice.Nil / Slice.Empty singleton
				return data.Copy();
			}

			data.EnsureSliceIsValid();

			// allocate the slice
			var slice = AllocateUnsafe(data.Count, aligned);
			data.CopyTo(slice.Array.AsSpan(slice.Offset, slice.Count));
			return slice;
		}

		/// <summary>Copy a slice into the buffer, immediately followed by a suffix, and return a new slice that is the concatenation of the two.</summary>
		/// <param name="data">Data to copy to the buffer</param>
		/// <param name="suffix">Suffix to copy immediately after <paramref name="data"/>.</param>
		/// <param name="aligned">If true, align the index of first byte of the slice with a multiple of 8 bytes</param>
		/// <returns>Slice that is the equivalent of <paramref name="data"/> plus <paramref name="suffix"/>, backed by the buffer.</returns>
		/// <remarks>When <paramref name="data"/> is empty, <paramref name="suffix"/> is returned without being copied to the buffer itself.</remarks>
		public Slice Intern(Slice data, Slice suffix, bool aligned = false)
		{
			if (data.Count == 0)
			{
				// note: we don't memoize the suffix, because in most case, it comes from a constant, and it would be a waste to copy it other and other again...
				return suffix.Count > 0 ? suffix : data.Array == null! ? default : Slice.Empty;
			}

			data.EnsureSliceIsValid();
			suffix.EnsureSliceIsValid();

			var slice = AllocateUnsafe(data.Count + suffix.Count, aligned);
			var span = slice.Array.AsSpan(slice.Offset, slice.Count);
			data.CopyTo(span);
			suffix.CopyTo(span[data.Count..]);
			return slice;
		}

		/// <summary>Copy a list of slices into the buffer, with optional alignment, and return a new array with the identical slices.</summary>
		/// <param name="data">List of data to copy to the buffer</param>
		/// <param name="aligned">If true, align the index of first byte of the slice with a multiple of 8 bytes</param>
		/// <returns>Array of slices that are the equivalent of each slice in <paramref name="data"/>, backed by the buffer.</returns>
		public Slice[] Intern(ReadOnlySpan<Slice> data, bool aligned = false)
		{
			if (data.Length == 0)
			{
				return [ ];
			}

			var res = new Slice[data.Length];
			for (int i = 0; i < res.Length; i++)
			{
				var slice = AllocateUnsafe(data[i].Count, aligned);
				data[i].Span.CopyTo(slice.Array.AsSpan(slice.Offset, slice.Count));
				res[i] = slice;
			}

			return res;
		}

		/// <summary>Copy a slice into the buffer, immediately followed by a suffix, and return a new slice that is the concatenation of the two.</summary>
		/// <param name="data">Data to copy to the buffer</param>
		/// <param name="suffix">Suffix to copy immediately after <paramref name="data"/>.</param>
		/// <param name="aligned">If true, align the index of first byte of the slice with a multiple of 8 bytes</param>
		/// <returns>Slice that is the equivalent of <paramref name="data"/> plus <paramref name="suffix"/>, backed by the buffer.</returns>
		/// <remarks>When <paramref name="data"/> is empty, <paramref name="suffix"/> is returned without being copied to the buffer itself.</remarks>
		public Slice Intern(ReadOnlySpan<byte> data, Slice suffix, bool aligned = false)
		{
			if (data.Length == 0)
			{
				// note: we don't memoize the suffix, because in most case, it comes from a constant, and it would be a waste to copy it other and other again...
				return suffix.Count > 0 ? suffix : Slice.Empty;
			}

			suffix.EnsureSliceIsValid();

			var slice = AllocateUnsafe(data.Length + suffix.Count, aligned);
			var span = slice.Array.AsSpan(slice.Offset, slice.Count);
			data.CopyTo(span);
			suffix.CopyTo(span[data.Length..]);
			return slice;
		}

		/// <summary>Adds a buffer to the list of allocated slices</summary>
		private void Keep(byte[] buffer, int used)
		{
			(m_chunks ??= [ ]).Add((buffer, used));
			m_allocated += buffer.Length;
			m_used += used;
		}

		/// <summary>Reset the buffer to its initial state and allow reuse of previously allocated memory</summary>
		/// <remarks>
		/// If there is a pool attached, all pages are returned to the pool and could be reused later.
		/// IMPORTANT: all slices allocated from this buffer CANNOT be used after a call to Reset!!!
		/// </remarks>
		public void ReleaseMemoryUnsafe(bool clear = false)
		{
			int p = m_pos;

			m_allocated = 0;
			m_pos = 0;
			m_used = 0;
			m_remaining = 0;
			var pool = m_pool;
			var chunks = m_chunks;
			if (pool != null)
			{ // release everything back to the pool
				var current = m_current;
				if (current != null)
				{
					if (clear)
					{
						current.AsSpan(0, p).Clear();
					}
					pool.Return(current, clearArray: false);
					m_current = null;
				}
				if (chunks != null)
				{
					foreach (var chunk in chunks)
					{
						if (clear)
						{
							chunk.Buffer.AsSpan(0, chunk.Used).Clear();
						}
						pool.Return(chunk.Buffer, clearArray: false);
					}
					chunks.Clear();
				}
			}
			else
			{
				// keep the current chunk but drop the previous chunks
				chunks?.Clear();
			}
		}

		/// <summary>This method is not supported anymore</summary>
		[Obsolete("This method is not supported anymore", error: true)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public Slice.Pinned Pin()
		{
			throw new NotSupportedException();
		}

	}

}
