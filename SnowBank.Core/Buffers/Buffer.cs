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

	/// <summary>Small buffer that keeps a list of chunks that are larger and larger</summary>
	/// <typeparam name="T">Type of elements stored in the buffer</typeparam>
	[DebuggerDisplay("Count={Count}, Chunks={Chunks.Length}, Current={Index}/{Current.Length}")]
	[PublicAPI]
	public sealed class Buffer<T> : IReadOnlyList<T>, IBufferWriter<T>, IDisposable
	{
		// We want to avoid growing the same array again and again !
		// Instead, we grow list of chunks, that grow in size (until a max), and concatenate all the chunks together at the end, once we know the final size

		/// <summary>Default initial capacity, if not specified</summary>
		const int DefaultCapacity = 16;

		//REVIEW: should we use a power of 2 or of 10 for initial capacity?
		// Since humans prefer the decimal system, it is more likely that query limit count be set to something like 10, 50, 100 or 1000
		// but most "human friendly" limits are close to the next power of 2, like 10 ~= 16, 50 ~= 64, 100 ~= 128, 500 ~= 512, 1000 ~= 1024, so we don't waste that much space...

		/// <summary>Maximum size of a chunk</summary>
		const int MaxChunkSize = 4096;

		/// <summary>Number of items in the buffer</summary>
		public int Count;

		/// <summary>Index in the current chunk</summary>
		private int Index;

		/// <summary>Current (and last) chunk</summary>
		private T[] Current;

		/// <summary>List of previous chunks (not including the current one)</summary>
		private ArraySegment<T>[]? Chunks;

		private ArrayPool<T>? Pool;

		/// <summary>Flag that sp</summary>
		public bool IsSingleSegment
		{
			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			get => this.Chunks is null;
		}

		public Buffer(int capacity = 0, ArrayPool<T>? pool = null)
		{
			Contract.Positive(capacity);
			capacity = capacity > 0 ? capacity : Buffer<T>.DefaultCapacity;

			this.Count = 0;
			this.Index = 0;
			this.Current = pool?.Rent(capacity) ?? new T[capacity];
			this.Pool = pool;
		}

		/// <inheritdoc />
		public void Dispose()
		{
			if (this.Pool != null)
			{
				if (this.Current.Length > 0)
				{
					if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
					{
						this.Current.AsSpan(0, this.Index).Clear();
					}
					this.Pool.Return(this.Current);
					this.Current = [ ];
				}

				if (this.Chunks != null)
				{
					foreach (var chunk in this.Chunks)
					{
						chunk.AsSpan().Clear();
						this.Pool.Return(chunk.Array!);
					}
					this.Chunks = null;
				}
			}

		}

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void Add(T item)
		{
			if (this.Index >= this.Current.Length)
			{
				Grow();
			}

			checked { ++this.Count; }
			this.Current[this.Index++] = item;
		}

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void AddRange(T[] items)
		{
			Contract.NotNull(items);
			AddRange(new ReadOnlySpan<T>(items));
		}

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
#if NET9_0_OR_GREATER
		public void AddRange(params ReadOnlySpan<T> items)
#else
		public void AddRange(ReadOnlySpan<T> items)
#endif
		{
			switch (items.Length)
			{
				case 0:
				{
					break;
				}
				case 1:
				{
					Add(items[0]);
					break;
				}
				default:
				{
					if (this.Index < this.Current.Length)
					{ // fill what we can in the current chunk
						var span = GetSpan();
						if (items.Length <= span.Length)
						{ // it fits
							items.CopyTo(span);
							Advance(items.Length);
							return;
						}

						// we have more items to copy
						items[..span.Length].CopyTo(span);
						Advance(span.Length);
						items = items[span.Length..];
					}

					if (items.Length > 0)
					{ // copy the remainder into a new chunk
						var span = GetSpan(items.Length);
						items.CopyTo(span);
						Advance(items.Length);
					}
					break;
				}
			}
		}

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void AddRange(IEnumerable<T> items)
		{
			if (items.TryGetSpan(out var span))
			{
				AddRange(span);
			}
			else if (items.TryGetNonEnumeratedCount(out int count))
			{
				AddRangeKnownSize(this, items, count);
			}
			else
			{
				AddRangeUnknownSize(this, items);
			}

			static void AddRangeKnownSize(Buffer<T> self, IEnumerable<T> items, int itemCount)
			{
				// first, fill the current segment

				using var it = items.GetEnumerator();

				// attempt to fill what remains in the current buffer
				if (self.Index < self.Current.Length)
				{
					var span = self.GetSpan();
					for (int i = 0; i < span.Length; i++)
					{
						if (!it.MoveNext())
						{
							self.Advance(i);
							return;
						}
						span[i] = it.Current;
					}
					self.Advance(span.Length);
					itemCount -= span.Length;
				}

				// any extra item will be allocated in a dedicated buffer
				if (itemCount > 0)
				{
					var span = self.GetSpan(itemCount);
					int index = 0;
					while (it.MoveNext())
					{
						span[index++] = it.Current;
					}
					self.Advance(index);
				}
			}

			static void AddRangeUnknownSize(Buffer<T> self, IEnumerable<T> items)
			{
				var current = self.Current;
				var index = self.Index;
				var capacity = current.Length;
				var count = self.Count;
				var chunks = self.Chunks;

				foreach (var item in items)
				{
					if (index >= capacity)
					{
						chunks = [ ..(chunks ?? [ ]), current ];
						var chunkSize = Math.Min(capacity * 2, Buffer<T>.MaxChunkSize);
						current = self.Pool?.Rent(chunkSize) ?? new T[chunkSize];
						index = 0;
						capacity = current.Length;
					}

					current[index++] = item;
					++count;
				}

				self.Count = count;
				self.Index = index;
				self.Current = current;
				self.Chunks = chunks;
			}
		}

		private void Grow()
		{
			// Growth rate:
			// - newly created chunk is always half the total size
			// - except the first chunk who is set to the initial capacity

			var current = this.Current;
			var newCapacity = Math.Min(Math.Max(this.Count, current.Length), Buffer<T>.MaxChunkSize);
			var tmp = this.Pool?.Rent(newCapacity) ?? new T[newCapacity];

			// append current chunk to existing chunk list
			this.Chunks = [ ..(Chunks ?? [ ]), current ];
			this.Current = tmp;
			this.Index = 0;
			Contract.Debug.Ensures(this.Current.Length > this.Index);
		}

		private void CopyChunksTo(Span<T> destination)
		{
			Contract.Debug.Requires(this.Chunks != null && this.Count > 0);

			var chunks = this.Chunks;
			Span<T> buffer = destination;

			// copy any previous chunk
			foreach (var chunk in chunks)
			{
				chunk.AsSpan().CopyTo(buffer);
				buffer = buffer[chunk.Count..];
			}

			// copy current chunk
			this.Current.AsSpan(0, this.Index).CopyTo(buffer);
		}

		/// <summary>Returns the content of the buffer as a span, if it fits in a single chunk</summary>
		/// <param name="segment">Receives a view of the content, or default if the buffer spans multiple chunks</param>
		/// <returns><see langword="true"/> if the buffer is empty, or its content is stored in a single continuous chunk, available in <paramref name="segment"/>; otherwise, <see langword="false"/></returns>
		public bool TryGetSpan(out Span<T> segment)
		{
			if (this.Chunks is null)
			{
				Contract.Debug.Assert(this.Count == this.Index);
				segment = this.Current.AsSpan(0, this.Index);
				return true;
			}

			segment = default;
			return false;
		}

		/// <summary>Returns the content of the buffer as an array</summary>
		/// <returns>Array of size <see cref="Count"/> containing all the items in this buffer</returns>
		/// <exception cref="InvalidOperationException">If a <see cref="Pool"/> is attached to this buffer</exception>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public ReadOnlyMemory<T> ToMemory()
		{
			if (this.Pool != null) throw new InvalidOperationException($"This method cannot be used in combination with a pool. Please use {nameof(ToArrayAndClear)} instead.");

			int count = this.Count;

			if (count == 0) return default;

			if (this.Chunks is null)
			{ // a single buffer page was used
				Contract.Debug.Assert(count == this.Index);
				return this.Current.AsMemory(0, count);
			}
			
			// concatenate all the buffer pages into one big array
			var tmp = new T[count];
			CopyChunksTo(tmp);
			return tmp;
		}

		/// <summary>Returns the content of the buffer as an array</summary>
		/// <returns>Array of size <see cref="Count"/> containing all the items in this buffer</returns>
		/// <exception cref="InvalidOperationException">If a <see cref="Pool"/> is attached to this buffer</exception>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public T[] ToArray()
		{
			if (this.Pool != null) throw new InvalidOperationException($"This method cannot be used in combination with a pool. Please use {nameof(ToArrayAndClear)} instead.");

			int count = this.Count;

			if (count == 0) return [ ];

			if (this.Chunks is null)
			{ // a single buffer page was used
				Contract.Debug.Assert(count == this.Index);
				return this.Current.Length == count
					? this.Current
					: this.Current.AsSpan(0, count).ToArray();
			}
			
			// concatenate all the buffer pages into one big array
			var tmp = new T[count];
			CopyChunksTo(tmp);
			return tmp;
		}

		/// <summary>Returns the content of the buffer as an array, and returns all allocations to the pool</summary>
		/// <returns>Array of size <see cref="Count"/> containing all the items in this buffer</returns>
		/// <remarks>The buffer instance can be reused after this call.</remarks>
		[MustUseReturnValue, CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public T[] ToArrayAndClear()
		{
			int count = this.Count;

			T[] tmp;

			if (count == 0)
			{
				tmp = [ ];
			}
			else if (this.Chunks is null)
			{ // a single buffer page was used
				Contract.Debug.Assert(count == this.Index);
				tmp = this.Current.AsSpan(0, count).ToArray();
			}
			else
			{ // concatenate all the buffer pages into one big array
				tmp = new T[count];
				CopyChunksTo(tmp);
			}

			Dispose();

			return tmp;
		}

		/// <summary>Returns the content of the buffer as an <see cref="ImmutableArray{T}">immutable array</see></summary>
		/// <returns>Array of size <see cref="Count"/> containing all the items in this buffer</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public ImmutableArray<T> ToImmutableArray()
		{
			// fits in a single segment
			if (TryGetSpan(out var span))
			{
				return [ ..span ];
			}
			
			// concatenate all the buffer pages into one big array
			var tmp = new T[this.Count];
			CopyChunksTo(tmp);
			return [ ..tmp ];
		}

		/// <summary>Returns the content of the buffer as an <see cref="ImmutableArray{T}">immutable array</see>, and returns all allocations to the pool</summary>
		/// <returns>Array of size <see cref="Count"/> containing all the items in this buffer</returns>
		/// <remarks>The buffer instance can be reused after this call.</remarks>
		[MustUseReturnValue, CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public ImmutableArray<T> ToImmutableArrayAndClear()
		{
			var results = ToImmutableArray();
			Dispose();
			return results;
		}

		/// <summary>Returns the content of the buffer as a list</summary>
		/// <returns>List of size <see cref="Count"/> containing all the items in this buffer</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<T> ToList()
		{
			if (TryGetSpan(out var span))
			{
				var res = new List<T>(span.Length);
#if NET8_0_OR_GREATER
				res.AddRange(span);
#else
				foreach(var item in span)
				{
					res.Add(item);
				}
#endif
				return res;
			}

			return MergeToList();

			[MethodImpl(MethodImplOptions.NoInlining)]
			List<T> MergeToList()
			{
				int count = this.Count;
				var chunks = this.Chunks!;
				Contract.Debug.Requires(count > 0 && chunks != null);

				var list = new List<T>(count);

				// add previous chunks
				foreach (var chunk in chunks)
				{
#if NET9_0_OR_GREATER
					list.AddRange(chunk.AsSpan());
#else
					foreach (var item in chunk.AsSpan())
					{
						list.Add(item);
					}
#endif
				}

				// add current chunk
				var span = this.Current.AsSpan(0, this.Index);
#if NET8_0_OR_GREATER
				list.AddRange(span);
#else
				foreach (var item in span)
				{
					list.Add(item);
				}
#endif
				Contract.Debug.Assert(list.Count == count);

				return list;
			}
		}

		/// <summary>Returns the content of the buffer as a list, and release all allocations to the pool</summary>
		/// <returns>List of size <see cref="Count"/> containing all the items in this buffer</returns>
		/// <remarks>The buffer instance can be reused after this call.</remarks>
		[MustUseReturnValue, CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public List<T> ToListAndClear()
		{
			var results = ToList();
			Dispose();
			return results;
		}

		/// <summary>Returns the content of the buffer as a set</summary>
		/// <returns>Set of size <see cref="Count"/> containing all the unique items in this buffer</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public HashSet<T> ToHashSet(IEqualityComparer<T>? comparer = null)
		{
			var hashset = new HashSet<T>(comparer);

			if (this.Count > 0)
			{
				// add the previous chunks
				var chunks = this.Chunks;
				if (chunks != null)
				{
					foreach (var chunk in chunks)
					{
						foreach (var item in chunk.AsSpan())
						{
							hashset.Add(item);
						}
					}
				}

				// add the current chunk
				foreach (var item in this.Current.AsSpan(0, this.Index))
				{
					hashset.Add(item);
				}
			}

			return hashset;
		}

		/// <summary>Returns the content of the buffer as a set, and release all allocations to the pool</summary>
		/// <returns>Set of size <see cref="Count"/> containing all the unique items in this buffer</returns>
		/// <remarks>The buffer instance can be reused after this call.</remarks>
		[MustUseReturnValue, CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public HashSet<T> ToHashSetAndClear(IEqualityComparer<T>? comparer = null)
		{
			var results = ToHashSet(comparer);
			Dispose();
			return results;
		}

		/// <summary>Returns the content of the buffer as a dictionary</summary>
		/// <returns>List of size <see cref="Count"/> containing all the items in this buffer</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public Dictionary<TKey, T> ToDictionary<TKey>(Func<T, TKey> keySelector, IEqualityComparer<TKey>? comparer = null)
			where TKey : notnull
		{
			var hashset = new Dictionary<TKey, T>(this.Count, comparer);

			if (this.Count > 0)
			{
				// add the previous chunk
				var chunks = this.Chunks;
				if (chunks != null)
				{
					foreach(var chunk in chunks)
					{
						foreach (var item in chunk.AsSpan())
						{
							hashset.Add(keySelector(item), item);
						}
					}
				}

				// add the current chunk
				foreach (var item in Current.AsSpan(0, this.Index))
				{
					hashset.Add(keySelector(item), item);
				}
			}

			return hashset;
		}

		/// <summary>Returns the content of the buffer as a dictionary</summary>
		/// <returns>List of size <see cref="Count"/> containing all the items in this buffer</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(Func<T, TKey> keySelector, Func<T, TValue> valueSelector, IEqualityComparer<TKey>? comparer = null)
			where TKey : notnull
		{
			var hashset = new Dictionary<TKey, TValue>(this.Count, comparer);

			if (this.Count > 0)
			{
				// add the previous chunks
				var chunks = this.Chunks;
				if (chunks != null)
				{
					foreach (var chunk in chunks)
					{
						foreach (var item in chunk.AsSpan())
						{
							hashset.Add(keySelector(item), valueSelector(item));
						}
					}
				}

				// add the current chunk
				foreach (var item in Current.AsSpan(0, this.Index))
				{
					hashset.Add(keySelector(item), valueSelector(item));
				}
			}

			return hashset;
		}

		/// <summary>Copies the content of the buffer into a destination span</summary>
		[CollectionAccess(CollectionAccessType.Read)]
		public int CopyTo(Span<T> destination)
		{
			int count = this.Count;
			if (count < destination.Length)
			{
				throw new ArgumentException("Destination buffer is too small", nameof(destination));
			}

			if (TryGetSpan(out var span))
			{
				Contract.Debug.Assert(span.Length == count);
				span.CopyTo(destination);
				return span.Length;
			}

			CopyChunksTo(destination);
			return count;
		}

		/// <summary>Copies the content of the buffer into a destination span, if it is large enough</summary>
		[CollectionAccess(CollectionAccessType.Read)]
		public bool TryCopyTo(Span<T> destination, out int written)
		{
			int count = this.Count;
			if (count < destination.Length)
			{
				written = 0;
				return false;
			}

			if (TryGetSpan(out var span))
			{ // fits in a single segment
				Contract.Debug.Assert(span.Length == count);
				span.CopyTo(destination);
				written = span.Length;
				return true;
			}

			// copy all the segments
			CopyChunksTo(destination);
			written = count;
			return true;
		}

		#region IReadOnlyList<T>...

		int IReadOnlyCollection<T>.Count => this.Count;

		public T this[int index]
		{
			[Pure, CollectionAccess(CollectionAccessType.Read)]
			get
			{
				if (TryGetSpan(out var span))
				{
					return span[index];
				}

				return GetAt(index);

				T GetAt(int offset)
				{
					Contract.Debug.Requires(this.Chunks != null && this.Count > 0);
					var chunks = this.Chunks!;

					foreach (var chunk in chunks)
					{
						if (offset < chunk.Count)
						{
							return chunk.Array![chunk.Offset + offset];
						}
						offset -= chunk.Count;
					}

					return this.Current.AsSpan(0, this.Index)[offset];
				}
			}
		}

		public ref T GetReference(int index)
		{
			if (TryGetSpan(out var span))
			{
				return ref span[index];
			}

			return ref GetReferenceMultiChunk(index);

			ref T GetReferenceMultiChunk(int offset)
			{
				Contract.Debug.Requires(this.Chunks != null && this.Count > 0);
				var chunks = this.Chunks!;

				foreach (var chunk in chunks)
				{
					if (offset < chunk.Count)
					{
						return ref chunk.Array![chunk.Offset + offset];
					}
					offset -= chunk.Count;
				}

				var span = this.Current.AsSpan(0, this.Index);
				return ref span[offset];
			}
		}

		public IEnumerator<T> GetEnumerator()
		{
			var chunks = this.Chunks;
			var index = this.Index;
			var current = this.Current;

			if (chunks != null)
			{
				foreach (var chunk in chunks)
				{
					for (int i = 0; i < chunk.Count; i++)
					{
						yield return chunk.Array![chunk.Offset + i];
					}
				}
			}

			for (int i = 0; i < index; i++)
			{
				yield return current[i];
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();

		#endregion

		#region IBufferWriter<T>...

		/// <inheritdoc />
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void Advance(int count)
		{
			Contract.Positive(count);
			var newIndex = checked(this.Index + count);
			if (newIndex > this.Current.Length)
			{
				throw new ArgumentException("Cannot advance past the previously allocated buffer");
			}
			this.Count = checked(this.Count + count);
			this.Index = newIndex;
			Contract.Debug.Ensures((uint) this.Index <= this.Current.Length);
		}

		/// <inheritdoc />
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public Memory<T> GetMemory(int sizeHint = 0)
		{
			Contract.Positive(sizeHint);

			var current = this.Current;

			// do we have enough space in the current segment?
			int remaining = current.Length - this.Index;

			if (remaining > 0 && (sizeHint == 0 || remaining >= sizeHint))
			{ // we have enough remaining data to accomodate the requested size
				return current.AsMemory(this.Index);
			}

			return GetMemorySlow(this, sizeHint);

			static Memory<T> GetMemorySlow(Buffer<T> self, int sizeHint)
			{
				var current = self.Current;

				// uhoh, not enough space in the current buffer :/
				if (self.Index > 0)
				{
					// we need to push what was written previously
					self.Chunks = [ ..(self.Chunks ?? [ ]), new(current, 0, self.Index) ];
					self.Index = 0;
				}

				// we don't want to make allocations too small
				int capacity = Math.Max(sizeHint, Math.Min(self.Current.Length * 2, Buffer<T>.MaxChunkSize));
				Contract.Debug.Assert(capacity >= sizeHint);

				current = self.Pool?.Rent(capacity) ?? new T[capacity];
				self.Current = current;

				Contract.Debug.Ensures(self.Index == 0 && current.Length >= sizeHint);
				return current.AsMemory();
			}
		}

		/// <inheritdoc />
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public Span<T> GetSpan(int sizeHint = 0)
		{
			return GetMemory(sizeHint).Span;
		}

		/// <summary>Enumerates all the segments in this buffer</summary>
		public IEnumerable<ReadOnlyMemory<T>> EnumerateSegments()
		{
			var chunks = this.Chunks;
			var last = this.Current.AsMemory(0, this.Index);
			if (chunks != null)
			{
				foreach (var chunk in chunks)
				{
					yield return chunk;
				}
			}

			if (last.Length > 0)
			{
				yield return last;
			}
		}

		/// <summary>Executes an action on each segment of the buffer</summary>
		public void ForEachSegments<TState>(TState state, Action<TState, ReadOnlyMemory<T>> action)
		{
			var chunks = this.Chunks;
			var last = this.Current.AsMemory(0, this.Index);

			if (chunks != null)
			{
				foreach (var chunk in chunks)
				{
					action(state, chunk);
				}
			}

			if (last.Length > 0)
			{
				action(state, last);
			}
		}

		/// <summary>Executes an action on each element in the buffer</summary>
		public void ForEach<TState>(TState state, Action<TState, T> action)
		{
			var chunks = this.Chunks;
			var last = this.Current.AsMemory(0, this.Index);

			if (chunks != null)
			{
				foreach (var chunk in chunks)
				{
					foreach (var item in chunk.AsSpan())
					{
						action(state, item);
					}
				}
			}

			if (last.Length > 0)
			{
				foreach (var item in last.Span)
				{
					action(state, item);
				}
			}
		}

		/// <summary>Aggregates a value over all the segments of the buffer</summary>
		public TAggregate AggregateSegments<TAggregate>(TAggregate initialValue, Func<TAggregate, ReadOnlyMemory<T>, TAggregate> action)
		{
			var chunks = this.Chunks;
			var last = this.Current.AsMemory(0, this.Index);
			var value = initialValue;

			if (chunks != null)
			{
				foreach (var chunk in chunks)
				{
					value = action(value, chunk);
				}
			}

			if (last.Length > 0)
			{
				value = action(value, last);
			}

			return value;
		}

		/// <summary>Aggregates a value over all the elements in the buffer</summary>
		public TAggregate Aggregate<TAggregate>(TAggregate initialValue, Func<TAggregate, T, TAggregate> action)
		{
			var chunks = this.Chunks;
			var last = this.Current.AsMemory(0, this.Index);
			var value = initialValue;

			if (chunks != null)
			{
				foreach (var chunk in chunks)
				{
					foreach (var item in chunk.AsSpan())
					{
						value = action(value, item);
					}
				}
			}

			if (last.Length > 0)
			{
				foreach (var item in last.Span)
				{
					value = action(value, item);
				}
			}

			return value;
		}

#if NET9_0_OR_GREATER

		public delegate void SegmentAggregator<TAggregate>(ref TAggregate agg, ReadOnlyMemory<T> segment) where TAggregate: allows ref struct;

		public delegate void Aggregator<TAggregate>(ref TAggregate agg, T item) where TAggregate: allows ref struct;

		/// <summary>Aggregates a value over all the segments of the buffer</summary>
		public void ForEachSegments<TAggregate>(ref TAggregate aggregate, SegmentAggregator<TAggregate> action)
			where TAggregate: allows ref struct
		{
			var chunks = this.Chunks;
			var last = this.Current.AsMemory(0, this.Index);

			if (chunks != null)
			{
				foreach (var chunk in chunks)
				{
					action(ref aggregate, chunk);
				}
			}

			if (last.Length > 0)
			{
				action(ref aggregate, last);
			}
		}

		/// <summary>Aggregates a value over all the elements in the buffer</summary>
		public void ForEach<TAggregate>(ref TAggregate aggregate, Aggregator<TAggregate> action)
			where TAggregate: allows ref struct
		{
			var chunks = this.Chunks;
			var last = this.Current.AsMemory(0, this.Index);

			if (chunks != null)
			{
				foreach (var chunk in chunks)
				{
					foreach (var item in chunk.AsSpan())
					{
						action(ref aggregate, item);
					}
				}
			}

			if (last.Length > 0)
			{
				foreach (var item in last.Span)
				{
					action(ref aggregate, item);
				}
			}
		}

#endif

		/// <summary>Projects each element of a buffer into a new form.</summary>
		public IEnumerable<TOther> SelectMany<TOther>(Func<ReadOnlyMemory<T>, IEnumerable<TOther>> selector)
		{
			var chunks = this.Chunks;
			var last = this.Current.AsMemory(0, this.Index);

			if (chunks != null)
			{
				foreach (var chunk in chunks)
				{
					var items = selector(chunk);
					foreach(var item in items)
					{
						yield return item;
					}
				}
			}

			if (last.Length > 0)
			{
				var items = selector(last);
				foreach(var item in items)
				{
					yield return item;
				}
			}
		}

		/// <summary>Projects each element of a buffer into a new form.</summary>
		public IEnumerable<TOther> Select<TOther>(Func<T, TOther> selector)
		{
			var chunks = this.Chunks;
			var last = this.Current.AsMemory(0, this.Index);

			if (chunks != null)
			{
				foreach (var chunk in chunks)
				{
					for(int i = 0; i < chunk.Count; i++)
					{
						yield return selector(chunk.Array![chunk.Offset + i]);
					}
				}
			}

			if (last.Length > 0)
			{
				for (int i = 0; i < last.Length; i++)
				{
					yield return selector(last.Span[i]);
				}
			}
		}

		/// <summary>Projects each element of a buffer into a new form by incorporating the element's index.</summary>
		public IEnumerable<TOther> Select<TOther>(Func<T, int, TOther> selector)
		{
			var chunks = this.Chunks;
			var last = this.Current.AsMemory(0, this.Index);

			int p = 0;
			if (chunks != null)
			{
				foreach (var chunk in chunks)
				{
					for(int i = 0; i < chunk.Count; i++)
					{
						yield return selector(chunk.Array![chunk.Offset + i], p++);
					}
				}
			}

			if (last.Length > 0)
			{
				for (int i = 0; i < last.Length; i++)
				{
					yield return selector(last.Span[i], p++);
				}
			}
		}

		/// <summary>Filters a buffer of values based on a predicate.</summary>
		public IEnumerable<T> Where(Func<T, bool> predicate)
		{
			var chunks = this.Chunks;
			var last = this.Current.AsMemory(0, this.Index);
			if (chunks != null)
			{
				foreach (var chunk in chunks)
				{
					for(int i = 0; i < chunk.Count; i++)
					{
						var item = chunk.Array![chunk.Offset + i];
						if (predicate(item))
						{
							yield return item;
						}
					}
				}
			}

			if (last.Length > 0)
			{
				for (int i = 0; i < last.Length; i++)
				{
					var item = last.Span[i];
					if (predicate(item))
					{
						yield return item;
					}
				}
			}
		}

		/// <summary>Filters a buffer of values based on a predicate. Each element's index is used in the logic of the predicate function.</summary>
		public IEnumerable<T> Where(Func<T, int, bool> predicate)
		{
			var chunks = this.Chunks;
			var last = this.Current.AsMemory(0, this.Index);
			int p = 0;
			if (chunks != null)
			{
				foreach (var chunk in chunks)
				{
					for(int i = 0; i < chunk.Count; i++)
					{
						var item = chunk.Array![chunk.Offset + i];
						if (predicate(item, p++))
						{
							yield return item;
						}
					}
				}
			}

			if (last.Length > 0)
			{
				for (int i = 0; i < last.Length; i++)
				{
					var item = last.Span[i];
					if (predicate(item, p++))
					{
						yield return item;
					}
				}
			}
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException BufferHasNoElements() => new("The buffer has not elements");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException BufferHasMoreThanOneElement() => new("The buffer has more than one element");

		/// <summary>Returns the only element of a buffer, and throws an exception if there is not exactly one element in the buffer.</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public T Single()
		{
			if (this.Count != 1)
			{
				throw this.Count == 0 ? BufferHasNoElements() : BufferHasMoreThanOneElement();
			}
			return this.Chunks is null
				? this.Current[0]
				: this.Chunks[0].AsSpan()[0];
		}

		/// <summary>Returns the only element of a buffer, or a default value if the buffer is empty; this method throws an exception if there is more than one element in the buffer.</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public T? SingleOrDefault()
		{
			if (this.Count == 0)
			{
				return default;
			}
			if (this.Count > 1)
			{
				throw BufferHasNoElements();
			}
			return this.Chunks is null
				? this.Current[0]
				: this.Chunks[0].AsSpan()[0];
		}

		/// <summary>Returns the first element of a buffer.</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public T First()
		{
			if (this.Count == 0)
			{
				throw BufferHasNoElements();
			}

			return this.Chunks is null
				? this.Current[0]
				: this.Chunks[0].AsSpan()[0];
		}

		/// <summary>Returns the first element of a buffer, or a default value if the buffer contains no elements.</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public T? FirstOrDefault()
		{
			if (this.Count == 0)
			{
				return default;
			}

			return this.Chunks is null
				? this.Current[0]
				: this.Chunks[0].AsSpan()[0];
		}

		/// <summary>Returns the last element of a buffer.</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public T Last()
		{
			if (this.Count == 0)
			{
				throw BufferHasNoElements();
			}

			return this.Chunks is null
				? this.Current[this.Index - 1]
				: this.Chunks[^1].AsSpan()[^1];
		}

		/// <summary>Returns the last element of a buffer, or a default value if the buffer contains no elements.</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public T? LastOrDefault()
		{
			if (this.Count == 0)
			{
				return default;
			}

			return this.Chunks is null
				? this.Current[this.Index - 1]
				: this.Chunks[^1].AsSpan()[^1];
		}

		#endregion

	}

	/// <summary>Helper methods for working with buffers</summary>
	public static class BufferExtensions
	{

		/// <summary>Attempts to extract the contents of this sequence as a <see cref="ReadOnlySpan{T}"/></summary>
		/// <typeparamref name="T">Type of the elements of the sequence</typeparamref>
		/// <param name="items">Source sequence.</param>
		/// <param name="span">Receives a view of the content of the sequence if it is supported; otherwise, the default value.</param>
		/// <returns><see langword="true"/> it was possible to extract the content of the buffer and expose it as a read-only span; otherwise, <see langword="false"/>.</returns>
		/// <remarks>
		/// <para>The following types are currently recognized: <b>T[]</b>, <see cref="List{T}"/>, <see cref="ImmutableArray{T}"/></para>
		/// <para>If <paramref name="items"/> is <c>null</c>, the empty span will be returned.</para>
		/// <para>This method does NOT guarantee that the content of the returned span will be stable in time (for example <see cref="List{T}"/> if the internal buffer is expanded),
		/// so the caller should consume the result immediately, or have a way to guarantee that the source will not be modified.</para>
		/// </remarks>
		public static bool TryGetSpan<T>([NoEnumeration] this IEnumerable<T>? items, out ReadOnlySpan<T> span)
		{
			//note: this has a high probability to collide with a future version of this in the BCL...
			// => hopefully it will have the same name and behavior, so we will simply #if it out of existence if/when this happens...

			switch (items)
			{
				case T[] arr:
				{
					span = new ReadOnlySpan<T>(arr);
					return true;
				}
				case List<T> list:
				{
					span = CollectionsMarshal.AsSpan(list);
					return true;
				}
				case ImmutableArray<T> arr:
				{
					span = arr.AsSpan();
					return true;
				}
				case null:
				{
					span = default;
					return true;
				}
				default:
				{
					span = default;
					return false;
				}
			}
		}

	}

}
