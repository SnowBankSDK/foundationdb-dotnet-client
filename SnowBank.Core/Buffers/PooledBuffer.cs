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
	using System.Collections.Immutable;
	using System.Runtime.InteropServices;

	/// <summary>Lightweight wrapper over a pooled buffer, that can be resized whenever necessary.</summary>
	/// <summary>Lightweight wrapper over a pooled buffer, that can be resized whenever necessary.</summary>
	/// <typeparam name="T">Type of the items in the buffer</typeparam>
	/// <remarks>
	/// <para>This type is intended for reusability where the caller will typically call <see cref="Add"/>, <see cref="AddRange"/> or <see cref="GetSpan"/> once per operation.</para>
	/// <para>This instance should be disposed when the caller is done; otherwise, the currently allocated buffer will not be returned to the pool</para>
	/// </remarks>
	[DebuggerDisplay("Written={Count}, Capacity={Capacity}")]
	public struct PooledBuffer<T> : IDisposable, IBufferWriter<T>
	{

		/// <summary>Constructs a <see cref="PooledBuffer{T}"/> with an initial capacity</summary>
		/// <param name="pool">Pool used to rent buffers</param>
		/// <param name="initialCapacity">Initial capacity (or <c>0</c> for no initial buffer)</param>
		public PooledBuffer(ArrayPool<T> pool, int initialCapacity)
		{
			Contract.NotNull(pool);
			Contract.Positive(initialCapacity);

			this.Pool = pool;
			this.Buffer = initialCapacity > 0 ? pool.Rent(initialCapacity) : [ ];
		}

		/// <summary>Returns a new <see cref="PooledBuffer{T}"/></summary>
		/// <param name="pool">Pool used to rent buffers (use the shared pool if <c>null</c>)</param>
		/// <param name="initialCapacity">Minimum initial capacity (or <c>0</c> for no initial buffer). Actual buffer size may be larger.</param>
		public static PooledBuffer<T> Create(ArrayPool<T>? pool = null, int initialCapacity = 0)
		{
			return new(pool ?? ArrayPool<T>.Shared, initialCapacity);
		}

		/// <summary>Pool used to rent the buffers</summary>
		public readonly ArrayPool<T> Pool;

		/// <summary>Current buffer</summary>
		public T[] Buffer;

		/// <summary>Number of items written to the buffer so far</summary>
		public int Count;

		/// <summary>Current buffer capacity</summary>
		public readonly int Capacity => this.Buffer.Length;

		private void ReturnBufferToPool()
		{
			var tmp = Interlocked.Exchange(ref this.Buffer, [ ]);
			if (tmp.Length > 0)
			{
				// only clear the chunk we have used
				tmp.AsSpan(0, this.Count).Clear();
				// return to the pool
				this.Pool.Return(tmp);
			}
		}

		/// <summary>Resize the current buffer, keeping any written items</summary>
		private T[] ResizeBuffer(int capacity)
		{
			// rent the new (larger) buffer
			var newBuffer = this.Pool.Rent(capacity);

			// copy over any items we already have
			if (this.Count > 0)
			{
				this.Buffer.AsSpan(0, this.Count).CopyTo(newBuffer);
			}

			// swap the buffers
			this.ReturnBufferToPool();
			this.Buffer = newBuffer;

			return newBuffer;
		}

		/// <summary>Returns a reference to an item that has been written to this buffer</summary>
		public ref T this[int index]
		{
			get
			{
				if ((uint)index >= this.Count) throw new IndexOutOfRangeException();
				return ref this.Buffer[index];
			}
		}

		/// <summary>Adds an item to the buffer</summary>
		public void Add(in T value)
		{
			if (this.Count < this.Buffer.Length)
			{
				this.Buffer[this.Count] = value;
				++this.Count;
			}
			else
			{
				AddAfterResize(in value);
			}
		}

		/// <summary>Adds a span of items to the buffer</summary>
		/// <param name="values"></param>
		public void AddRange(ReadOnlySpan<T> values)
		{
			int requiredCapacity = checked(this.Count + values.Length);

			var buffer = this.Buffer.Length < requiredCapacity ? ResizeBuffer(requiredCapacity) : this.Buffer;
			var span = buffer.AsSpan(this.Count, values.Length);

			values.CopyTo(span);
			this.Count += values.Length;
		}

		private void AddAfterResize(in T value)
		{
			int capacity = this.Buffer.Length < 4 ? 4 : checked(this.Buffer.Length * 2);
			var buffer = ResizeBuffer(capacity);
			buffer[this.Count] = value;
			++this.Count;
		}

		/// <summary>Notifies the buffer that the caller intends to write a batch of items, and that it should pre-allocate the space now.</summary>
		/// <param name="size">Number of items that are expected to be written soon</param>
		/// <remarks>
		/// <para>If the remaining free capacity is not large enough, the buffer will resize its internal buffer.</para>
		/// <para>The caller may decide to write fewer items, in which case some memory could be wasted. It could also decide to write more, in which case additional buffer resize may happen.</para>
		/// </remarks>
		public void EnsureRemainingCapacity(int size)
		{
			var buffer = Buffer;
			int minimumCapacity = checked(this.Count + size);
			if (minimumCapacity > buffer.Length)
			{ // we may need to resize
				ResizeBuffer(minimumCapacity);
			}
		}

		/// <summary>Gets a span for writing items to this buffer</summary>
		/// <param name="size">Required capacity, or <c>0</c> to return all the remaining space</param>
		/// <returns>Span with the specified length</returns>
		/// <remarks>
		/// <para>The caller <b>MUST</b> call <see cref="Advance"/> to specify how many items where actually written to the buffer!</para>
		/// <para>If <paramref name="size"/> if non-zero, the return span will have this exact size, even if there are more space available. This is different from the typical <see cref="IBufferWriter{T}.GetSpan"/> behavior of returning a span that can be larger.</para>
		/// <para>The previous buffer will be recycled if it is not large enough.</para>
		/// </remarks>
		public Span<T> GetSpan(int size = 0)
		{
			var buffer = this.Buffer;
			if (size == 0)
			{ // "all you have"
				if (this.Count >= buffer.Length)
				{ // we need to resize
					buffer = ResizeBuffer(checked(buffer.Length * 2));
				}
				return buffer.AsSpan(this.Count);
			}
			else
			{ // "I want exactly N items"
				int minimumCapacity = checked(this.Count + size);
				if (minimumCapacity > buffer.Length)
				{ // we may need to resize
					buffer = ResizeBuffer(minimumCapacity);
				}
				return buffer.AsSpan(this.Count, size);
			}
		}

		/// <summary>Gets a span for writing items to this buffer</summary>
		/// <param name="size">Required capacity, or <c>0</c> to return all the remaining space</param>
		/// <returns>Memory with the specified length</returns>
		/// <remarks>
		/// <para>The caller <b>MUST</b> call <see cref="Advance"/> to specify how many items where actually written to the buffer!</para>
		/// <para>The previous buffer will be recycled if it is not large enough.</para>
		/// </remarks>
		public Memory<T> GetMemory(int size)
		{
			var buffer = this.Buffer;
			if (size == 0)
			{ // "all you have"
				if (this.Count >= buffer.Length)
				{ // we need to resize
					buffer = ResizeBuffer(checked(buffer.Length * 2));
				}
				return buffer.AsMemory(this.Count);
			}
			else
			{ // "I want exactly N items"
				int minimumCapacity = checked(this.Count + size);
				if (minimumCapacity > buffer.Length)
				{ // we may need to resize
					buffer = ResizeBuffer(minimumCapacity);
				}
				return buffer.AsMemory(this.Count, size);
			}
		}

		/// <summary>Notifies the buffer that <paramref name="count"/> amount of data was written to the output <see cref="Span{T}"/>/<see cref="Memory{T}"/></summary>
		/// <exception cref="InvalidOperationException"></exception>
		public void Advance(int count)
		{
			int cursor = checked(this.Count + count);
			if (cursor > this.Buffer.Length)
			{
				throw new InvalidOperationException("Cannot advance past the end of the buffer.");
			}
			this.Count = cursor;
		}

		/// <summary>Clears the buffer</summary>
		public void Clear()
		{
			if (this.Buffer.Length > 4096)
			{ // return large buffers to the pool
				ReturnBufferToPool();
			}
			else
			{ // keep small buffers
				var written = this.Count;
				if (written > 0)
				{
					this.Buffer.AsSpan(0, written).Clear();
				}
			}
			this.Count = 0;
		}

		/// <summary>Returns a span of all items previously written to this buffer.</summary>
		public readonly Span<T> AsSpan() => this.Buffer.AsSpan(0, this.Count);

		/// <summary>Returns a memory region of all items previously written to this buffer.</summary>
		public readonly Memory<T> AsMemory() => this.Buffer.AsMemory(0, this.Count);

		/// <summary>Returns an array of items previously written to this buffer.</summary>
		/// <param name="clear">If <c>true</c>, this section of the buffer will be cleared, so that it can be reused immediately without leaking references.</param>
		public T[] ToArray(bool clear = false)
		{
			var items = this.AsSpan();

			if (items.Length == 0) return [ ];

			var res = items.ToArray();

			if (clear) this.Clear();

			return res;
		}

		/// <summary>Returns a list of items previously written to this buffer.</summary>
		/// <param name="clear">If <c>true</c>, this section of the buffer will be cleared, so that it can be reused immediately without leaking references.</param>
		public List<T> ToList(bool clear = false)
		{
			var items = this.AsSpan();

			if (items.Length == 0) return [ ];

#if NET8_0_OR_GREATER
			List<T> res = new();
			CollectionsMarshal.SetCount(res, items.Length);
			items.CopyTo(CollectionsMarshal.AsSpan(res));
#else
			List<T> res = [ ..items ];
#endif

			if (clear) this.Clear();

			return res;
		}

		/// <summary>Returns a list of items previously written to this buffer.</summary>
		/// <param name="clear">If <c>true</c>, this section of the buffer will be cleared, so that it can be reused immediately without leaking references.</param>
		public ImmutableArray<T> ToImmutableArray(bool clear = false)
		{
			var items = this.AsSpan();

			if (items.Length == 0) return [ ];

			var res = ImmutableArray.Create(items);

			if (clear) this.Clear();

			return res;
		}

		/// <summary>Returns a list of items previously written to this buffer.</summary>
		/// <param name="comparer">Comparer for the elements in the set</param>
		/// <param name="clear">If <c>true</c>, this section of the buffer will be cleared, so that it can be reused immediately without leaking references.</param>
		public HashSet<T> ToHashSet(IEqualityComparer<T> comparer, bool clear = false)
		{
			var items = this.AsSpan();

			if (items.Length == 0) return [ ];

			var res = new HashSet<T>(items.Length, comparer);
			foreach (var item in items)
			{
				res.Add(item);
			}

			if (clear) this.Clear();

			return res;
		}

		/// <summary>Copies items previously written to this buffer to the specified destination</summary>
		/// <param name="destination">Destination buffer, that must be large enough.</param>
		/// <param name="clear">If <c>true</c>, this section of the buffer will be cleared, so that it can be reused immediately without leaking references.</param>
		/// <returns><c>true</c> if the buffer was large enough; otherwise, <c>false</c>.</returns>
		public bool TryCopyTo(Span<T> destination, bool clear = false)
		{
			if (!this.AsSpan().TryCopyTo(destination))
			{
				return false;
			}

			if (clear) this.Clear();
			return true;
		}

		/// <summary>Copies items previously written to this buffer to the specified destination</summary>
		/// <param name="destination">Destination buffer, that must be large enough.</param>
		/// <param name="clear">If <c>true</c>, this section of the buffer will be cleared, so that it can be reused immediately without leaking references.</param>
		/// <returns><c>true</c> if the buffer was large enough; otherwise, <c>false</c>.</returns>
		/// <exception cref="InvalidOperationException">when <paramref name="destination"/> is not large enough</exception>
		public void CopyTo(Span<T> destination, bool clear = false)
		{
			this.AsSpan().CopyTo(destination);

			if (clear) this.Clear();
		}

		/// <inheritdoc />
		public void Dispose()
		{
			this.ReturnBufferToPool();
			this.Count = 0;
		}

	}

}
