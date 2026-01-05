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
	using System.ComponentModel;
	using System.Runtime.InteropServices;

#if NET8_0_OR_GREATER
	/// <summary>Buffer that will accumulate data in a contiguous span, starting from a stack allocated buffer, and switching to pooled buffers if required</summary>
	/// <typeparam name="T">Type of elements stored in the buffer</typeparam>
	/// <remarks>
	/// <para>The final list of items will be available as a single contiguous <see cref="Span{T}"/></para>
	/// <para>Consider using <see cref="SegmentedValueBuffer{T}"/> if you do not need to consume the items as a single span, <see cref="SegmentedValueBuffer{T}"/> may be faster</para>
	/// </remarks>
	/// <seealso cref="SegmentedValueBuffer{T}"/>
#else
	/// <summary>Buffer that will accumulate data in a contiguous span, starting from a stack allocated buffer, and switching to pooled buffers if required</summary>
	/// <typeparam name="T">Type of elements stored in the buffer</typeparam>
	/// <remarks>
	/// <para>The final list of items will be available as a single contiguous <see cref="Span{T}"/></para>
	/// </remarks>
#endif
	[DebuggerDisplay("Count={Count}, Capacity{Buffer.Length}")]
	[DebuggerTypeProxy(typeof(ValueBufferDebugView<>))]
	[PublicAPI]
	public ref struct ValueBuffer<T>
#if NET9_0_OR_GREATER
		: IDisposable
#endif
	{

		// This should only be used when needing to create a list of array of a few elements with as few memory allocations as possible,
		// either by passing a pre-allocated span (on the stack usually), or by using pooled buffers when a resize is required.
		
		// One typical use is to start from a small buffer allocated on the stack, that will be used until the buffer needs to be resized,
		// in which case another buffer will be used from a shared pool.

		/// <summary>Current buffer</summary>
		private Span<T> Buffer;

		/// <summary>Number of items in the buffer</summary>
		public int Count;

		/// <summary>Optional array coming from a pool</summary>
		private T[]? Array;

		/// <summary>Constructs a <see cref="ValueBuffer{T}"/> using an already allocated scratch buffer</summary>
		/// <param name="initialBuffer">Buffer used initially.</param>
		public ValueBuffer(Span<T> initialBuffer)
		{
			this.Count = 0;
			this.Array = null;
			this.Buffer = initialBuffer;
		}

		/// <summary>Constructs a <see cref="ValueBuffer{T}"/> with an initial capacity</summary>
		/// <param name="capacity">Capacity of the initial buffer (allocated on a pool)</param>
		public ValueBuffer(int capacity)
		{
			Contract.Positive(capacity);
			this.Count = 0;
			this.Array = capacity > 0 ? ArrayPool<T>.Shared.Rent(capacity) : [ ];
			this.Buffer = this.Array;
		}

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

		/// <summary><c>YOU MUST PROVIDE AN INITIAL CAPACITY OR SCRATCH SPACE!</c></summary>
		[Obsolete("You must specify an initial capacity or scratch buffer", error: true)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public ValueBuffer() { }

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

		/// <summary>Returns a span with all the items already written to this buffer</summary>
		public Span<T> Span => this.Count > 0 ? this.Buffer[..this.Count] : default;

		/// <summary>Returns the current capacity of the buffer</summary>
		public int Capacity => this.Buffer.Length;

		/// <summary>Add an item to the end of the buffer</summary>
		/// <param name="item">Item to add</param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void Add(T item)
		{
			int pos = this.Count;
			var buff = this.Buffer;
			if ((uint) pos < (uint) buff.Length)
			{
				buff[pos] = item;
				this.Count = pos + 1;
			}
			else
			{
				AddWithResize(item);
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void AddWithResize(T item)
		{
			int pos = this.Count;
			Grow(1);
			this.Buffer[pos] = item;
			this.Count = pos + 1;
		}

		/// <summary>Add a span of items at the end of the buffer</summary>
		/// <param name="items">Items to add</param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
#if NET9_0_OR_GREATER
		public void AddRange(params ReadOnlySpan<T> items)
#else
		public void AddRange(scoped ReadOnlySpan<T> items)
#endif
		{
			int pos = this.Count;
			var buf = this.Buffer;
			if (items.Length == 1 && (uint) pos < buf.Length)
			{
				buf[pos] = items[0];
				this.Count = pos + 1;
			}
			else if ((uint) (items.Length + this.Count) <= (uint) buf.Length)
			{
				items.CopyTo(buf[pos..]);
				this.Count = pos + items.Length;
			}
			else
			{
				AddWithResize(items);
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void AddWithResize(scoped ReadOnlySpan<T> items)
		{
			Grow(items.Length);
			int pos = this.Count;
			items.CopyTo(this.Buffer[pos..]);
			this.Count = pos + items.Length;
		}

		/// <summary>Add a sequence of items at the end of the buffer</summary>
		/// <param name="items">Items to add</param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void AddRange(IEnumerable<T> items)
		{
			if (items.TryGetSpan(out var span))
			{
				AddRange(span);
			}
			else if (items.TryGetNonEnumeratedCount(out int count))
			{
				AddRangeKnownSize(items, count);
			}
			else
			{
				AddRangeUnknownSize(items);
			}
		}

		void AddRangeKnownSize(IEnumerable<T> items, int itemCount)
		{
			// first, fill the current segment

			int pos = this.Count;
			var buf = this.Buffer;

			// expand if needed
			if ((ulong) (uint) pos + (ulong) (uint) itemCount > (ulong) (uint) buf.Length)
			{
				Grow(checked((pos + itemCount) - buf.Length));
				buf = this.Buffer;
			}

			// copy the items one by one
			foreach (var item in items)
			{
				buf[pos++] = item;
			}

			this.Count = pos;
		}

		void AddRangeUnknownSize(IEnumerable<T> items)
		{
			int pos = this.Count;
			var buf = this.Buffer;

			foreach (var item in items)
			{
				if ((uint) pos >= (uint) buf.Length)
				{
					Grow(1);
					buf = Buffer;
				}
				buf[pos] = item;
				this.Count = pos + 1;
			}
			this.Count = pos;
		}

		private void Grow(int required)
		{
			Contract.GreaterThan(required, 0);
			// Growth rate:
			// - first chunk size is 4 if empty
			// - double the buffer size
			// - except the first chunk who is set to the initial capacity

			int length = this.Buffer.Length;
			long capacity = (long) length + required;
			if (capacity > 1_000_000)
			{
				throw new InvalidOperationException("Buffer cannot expand because it would exceed the maximum size limit.");
			}
			capacity = Math.Max(length != 0 ? length * 2 : 4, length + required);

			// allocate a new buffer (note: may be bigger than requested)
			var tmp = ArrayPool<T>.Shared.Rent((int) capacity);
			this.Buffer.CopyTo(new Span<T>(tmp));

			var array = this.Array;
			this.Array = tmp;
			this.Buffer = new Span<T>(tmp);

			// return any previous buffer to the pool
			if (array != null)
			{
				array.AsSpan(0, this.Count).Clear();
				ArrayPool<T>.Shared.Return(array);
			}
		}

		private void Clear(bool release)
		{
			this.Span.Clear();
			this.Count = 0;

			// return the array to the pool
			if (release)
			{
				this.Buffer = default;

				var array = this.Array;
				if (array != null)
				{
					this.Array = null;
					ArrayPool<T>.Shared.Return(array);
				}
			}
		}

		/// <summary>Clears the content of the buffer</summary>
		/// <remarks>The buffer count is reset to zero, but the current backing store remains the same</remarks>
		public void Clear() => Clear(release: false);

		/// <summary>Returns the content of the buffer as an array</summary>
		/// <returns>Array of size <see cref="Count"/> containing all the items in this buffer</returns>
		/// <seealso cref="ToArrayAndClear"/>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public T[] ToArray() => this.Span.ToArray();

		/// <summary>Returns the content of the buffer as an array, and clears the buffer for reuse</summary>
		/// <returns>Array of size <see cref="Count"/> containing all the items in this buffer</returns>
		/// <remarks>The buffer is cleared and any allocated buffer returned to the pool.</remarks>
		[Pure, CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public T[] ToArrayAndClear()
		{
			var items = this.Span.ToArray();
			Clear(release: true);
			return items;
		}

		/// <summary>Returns the content of the buffer as an <see cref="ImmutableArray{T}">immutable array</see></summary>
		/// <returns>Array of size <see cref="Count"/> containing all the items in this buffer</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public ImmutableArray<T> ToImmutableArray() => [ ..this.Span ];

		/// <summary>Returns the content of the buffer as a list</summary>
		/// <returns>List of size <see cref="Count"/> containing all the items in this buffer</returns>
		/// <seealso cref="ToListAndClear"/>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<T> ToList()
		{
			int length = this.Count;
			var res = new List<T>(length);
#if NET8_0_OR_GREATER
			CollectionsMarshal.SetCount(res, length);
			this.Span.CopyTo(CollectionsMarshal.AsSpan(res));
#else
			foreach (var item in this.Span)
			{
				res.Add(item);
			}
#endif
			return res;
		}

		/// <summary>Returns the content of the buffer as a list, and clears the buffer for reuse</summary>
		/// <returns>List of size <see cref="Count"/> containing all the items in this buffer</returns>
		/// <remarks>The buffer is cleared and any allocated buffer returned to the pool.</remarks>
		[Pure, CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public List<T> ToListAndClear()
		{
			var items = ToList();
			Clear(release: true);
			return items;
		}

		/// <summary>Returns the content of the buffer as a set</summary>
		/// <returns>Set containing all the items in this buffer</returns>
		/// <seealso cref="ToHashSetAndClear"/>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public HashSet<T> ToHashSet(IEqualityComparer<T>? comparer = null)
		{
			var res = new HashSet<T>(this.Count, comparer);

			foreach (var item in this.Span)
			{
				res.Add(item);
			}

			return res;
		}

		/// <summary>Returns the content of the buffer as a set, and clears the buffer for reuse</summary>
		/// <returns>Set containing all the items in this buffer</returns>
		/// <remarks>The buffer is cleared and any allocated buffer returned to the pool.</remarks>
		[Pure, CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public HashSet<T> ToHashSetAndClear(IEqualityComparer<T>? comparer = null)
		{
			var items = ToHashSet(comparer);
			Clear(release: true);
			return items;
		}

		/// <summary>Returns the content of the buffer as a dictionary</summary>
		/// <param name="keySelector">Selector used to extract the keys for each item in the buffer.</param>
		/// <param name="comparer">Key comparer used by the dictionary.</param>
		/// <returns>Dictionary containing all the items in this buffer, using <paramref name="keySelector"/> to generate the keys.</returns>
		/// <exception cref="ArgumentException"> if two values in the buffer used the same key</exception>
		/// <seealso cref="ToDictionaryAndClear{TKey}"/>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public Dictionary<TKey, T> ToDictionary<TKey>(Func<T, TKey> keySelector, IEqualityComparer<TKey>? comparer = null)
			where TKey : notnull
		{
			var res = new Dictionary<TKey, T>(this.Count, comparer);

			foreach (var item in this.Span)
			{
				res.Add(keySelector(item), item);
			}

			return res;
		}

		/// <summary>Returns the content of the buffer as a dictionary, and clears the buffer for reuse</summary>
		/// <param name="keySelector">Selector used to extract the keys for each item in the buffer.</param>
		/// <param name="comparer">Key comparer used by the dictionary.</param>
		/// <returns>Dictionary containing all the items in this buffer, using <paramref name="keySelector"/> to generate the keys.</returns>
		/// <remarks>The buffer is cleared and any allocated buffer returned to the pool.</remarks>
		/// <exception cref="ArgumentException"> if two values in the buffer used the same key</exception>
		[Pure, CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public Dictionary<TKey, T> ToDictionaryAndClear<TKey>(Func<T, TKey> keySelector, IEqualityComparer<TKey>? comparer = null)
			where TKey : notnull
		{
			var items = ToDictionary(keySelector, comparer);
			Clear(release: true);
			return items;
		}

		/// <summary>Returns the content of the buffer as a dictionary</summary>
		/// <param name="keySelector">Selector used to extract the keys for each item in the buffer.</param>
		/// <param name="valueSelector">Selector used to extract the values for each item in the buffer.</param>
		/// <param name="comparer">Key comparer used by the dictionary.</param>
		/// <returns>Dictionary containing all the mapped items in this buffer, using <paramref name="keySelector"/> for the keys, and <paramref name="valueSelector"/> for the values.</returns>
		/// <exception cref="ArgumentException"> if two values in the buffer used the same key</exception>
		/// <seealso cref="ToDictionaryAndClear{TKey,TValue}"/>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(Func<T, TKey> keySelector, Func<T, TValue> valueSelector, IEqualityComparer<TKey>? comparer = null)
			where TKey : notnull
		{
			var res = new Dictionary<TKey, TValue>(this.Count, comparer);

			// add the current chunk
			foreach (var item in this.Span)
			{
				res.Add(keySelector(item), valueSelector(item));
			}

			return res;
		}

		/// <summary>Returns the content of the buffer as a dictionary, and clears the buffer for reuse</summary>
		/// <param name="keySelector">Selector used to extract the keys for each item in the buffer.</param>
		/// <param name="valueSelector">Selector used to extract the values for each item in the buffer.</param>
		/// <param name="comparer">Key comparer used by the dictionary.</param>
		/// <returns>Dictionary containing all the mapped items in this buffer, using <paramref name="keySelector"/> for the keys, and <paramref name="valueSelector"/> for the values.</returns>
		/// <remarks>The buffer is cleared and any allocated buffer returned to the pool.</remarks>
		/// <exception cref="ArgumentException"> if two values in the buffer used the same key</exception>
		[Pure, CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public Dictionary<TKey, TValue> ToDictionaryAndClear<TKey, TValue>(Func<T, TKey> keySelector, Func<T, TValue> valueSelector, IEqualityComparer<TKey>? comparer = null)
			where TKey : notnull
		{
			var items = ToDictionary(keySelector, valueSelector, comparer);
			Clear(release: true);
			return items;
		}

		/// <summary>Copies the content of the buffer into a destination span</summary>
		[CollectionAccess(CollectionAccessType.Read)]
		public int CopyTo(Span<T> destination)
		{
			if (this.Count < destination.Length)
			{
				throw new ArgumentException("Destination buffer is too small", nameof(destination));
			}
			this.Span.CopyTo(destination);
			return this.Count;
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

			this.Span.TryCopyTo(destination);
			written = count;
			return true;
		}

		#region IReadOnlyList<T>...

		/// <summary>Returns a reference to the item at the specified index</summary>
		/// <param name="index">Index of the item (must be valid)</param>
		/// <returns>Reference to this item</returns>
		/// <remarks>This allows modifying the content of items "in place".</remarks>
		/// <exception cref="IndexOutOfRangeException">If <paramref name="index"/> is outside the bounds of the buffer. The number of items currently in the buffer is available via the <see cref="Count"/> property.</exception>
		[Pure, CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public ref T this[int index]
		{
			[Pure, CollectionAccess(CollectionAccessType.Read)]
			get => ref this.Buffer[..this.Count][index];
			//note: the span will perform the bound-checking for us
		}

		/// <summary>Returns an enumerator that will list all the items added to the buffer so far.</summary>
		public Span<T>.Enumerator GetEnumerator()
		{
			return this.Span.GetEnumerator();
		}

		#endregion

		#region IBufferWriter<T>...

		/// <summary>Notifies the <see cref="ValueBuffer{T}" /> that <paramref name="count" /> data items were written to the output <see cref="T:System.Span`1" />.</summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void Advance(int count)
		{
			Contract.Positive(count);
			var newIndex = checked(this.Count + count);
			if ((uint) newIndex > (uint) this.Buffer.Length)
			{
				throw new ArgumentException("Cannot advance past the previously allocated buffer");
			}
			this.Count = newIndex;
			Contract.Debug.Ensures((uint) this.Count <= this.Buffer.Length);
		}

		/// <summary>Returns a <see cref="T:System.Span`1" /> to write to that is at least the requested size (specified by <paramref name="sizeHint" />).</summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public Span<T> GetSpan(int sizeHint = 0)
		{
			Contract.Positive(sizeHint);

			// do we have enough space in the current segment?
			int remaining = this.Buffer.Length - this.Count;

			if (remaining <= 0 || (sizeHint != 0 && remaining < sizeHint))
			{
				Grow(sizeHint);
			}

			// we have enough remaining data to accomodate the requested size
			return this.Buffer[this.Count..];
		}

		/// <summary>Executes an action on each element in the buffer</summary>
		public void ForEach(Action<T> action)
		{
			foreach (var item in this.Span)
			{
				action(item);
			}
		}

		/// <summary>Executes an action on each element in the buffer</summary>
		public void ForEach<TState>(TState state, Action<TState, T> action)
		{
			foreach (var item in this.Span)
			{
				action(state, item);
			}
		}

		/// <summary>Aggregates a value over all the elements in the buffer</summary>
		public TAggregate Aggregate<TAggregate>(TAggregate initialValue, Func<TAggregate, T, TAggregate> action)
		{
			var value = initialValue;

			foreach (var item in this.Span)
			{
				value = action(value, item);
			}

			return value;
		}

#if NET9_0_OR_GREATER

		/// <summary>Delegate that can update an aggregate by reference</summary>
		/// <typeparam name="TAggregate">Type of the aggregate</typeparam>
		/// <param name="agg">Reference to the current aggregate, which can be updated to include the new value</param>
		/// <param name="item">New value that should be added to aggregate</param>
		public delegate void Aggregator<TAggregate>(ref TAggregate agg, T item) where TAggregate: allows ref struct;

		/// <summary>Aggregates a value over all the elements in the buffer</summary>
		public void ForEach<TAggregate>(ref TAggregate aggregate, Aggregator<TAggregate> action)
			where TAggregate: allows ref struct
		{
			foreach (var item in this.Span)
			{
				action(ref aggregate, item);
			}
		}

#endif

		/// <summary>Projects each element of a buffer into a new form.</summary>
		public int ProjectTo<TOther>(Span<TOther> destination, Func<T, TOther> selector)
		{
			int length = this.Count;
			if (destination.Length < length) throw ThrowHelper.ArgumentException(nameof(destination), "Destination buffer is too small");

			var buf = this.Buffer;
			for (int i = 0; i < length; i++)
			{
				destination[i] = selector(buf[i]);
			}

			return length;
		}

		/// <summary>Projects each element of a buffer into a new form by incorporating the element's index.</summary>
		public int ProjectTo<TOther>(Span<TOther> destination, Func<T, int, TOther> selector)
		{
			int length = this.Count;
			if (destination.Length < length) throw ThrowHelper.ArgumentException(nameof(destination), "Destination buffer is too small");

			var buf = this.Buffer;
			for (int i = 0; i < length; i++)
			{
				destination[i] = selector(buf[i], i);
			}

			return length;
		}

		/// <summary>Filters a buffer of values based on a predicate.</summary>
		public int FilterTo(Span<T> destination, Func<T, bool> predicate)
		{
			Contract.NotNull(predicate);

			int n = 0;
			foreach(var item in this.Span)
			{
				if (predicate(item))
				{
					destination[n++] = item;
				}
			}

			return n;
		}

		/// <summary>Filters a buffer of values based on a predicate. Each element's index is used in the logic of the predicate function.</summary>
		public int FilterTo(Span<T> destination, Func<T, int, bool> predicate)
		{
			Contract.NotNull(predicate);

			int i = 0, n = 0;

			foreach(var item in this.Span)
			{
				if (predicate(item, i))
				{
					destination[n++] = item;
				}
				++i;
			}

			return n;
		}

		/// <summary>Projects any elements that match a predicate of a buffer into a new form.</summary>
		public int FilterAndProjectTo<TOther>(Span<TOther> destination, Func<T, bool> predicate, Func<T, TOther> selector)
		{
			int length = this.Count;
			if (destination.Length < length) throw ThrowHelper.ArgumentException(nameof(destination), "Destination buffer is too small");

			int p = 0;
			foreach(var item in this.Span)
			{
				if (predicate(item))
				{
					destination[p++] = selector(item);
				}
			}

			return p;
		}

		/// <summary>Projects any elements that match a predicate of a buffer into a new form.</summary>
		public int FilterAndProjectTo<TOther>(Span<TOther> destination, Func<T, int, bool> predicate, Func<T, int, TOther> selector)
		{
			int length = this.Count;
			if (destination.Length < length) throw ThrowHelper.ArgumentException(nameof(destination), "Destination buffer is too small");

			int p = 0, i = 0;
			foreach(var item in this.Span)
			{
				if (predicate(item, i))
				{
					destination[p++] = selector(item, i);
				}
				++i;
			}

			return p;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException BufferHasNoElements() => new("The buffer has not elements");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException BufferHasMoreThanOneElement() => new("The buffer has more than one element");

		/// <summary>Returns the only element of a buffer, and throws an exception if there is not exactly one element in the buffer.</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public T Single() => this.Count == 1 ? this.Buffer[0] : throw (this.Count == 0 ? BufferHasNoElements() : BufferHasMoreThanOneElement());

		/// <summary>Returns the only element of a buffer, or a default value if the buffer is empty; this method throws an exception if there is more than one element in the buffer.</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public T? SingleOrDefault() => this.Count switch
		{
			0 => default,
			1 => this.Buffer[0],
			_ => throw BufferHasNoElements(),
		};

		/// <summary>Returns the first element of a buffer.</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public T First() => this.Count != 0 ? this.Buffer[0] : throw BufferHasNoElements();

		/// <summary>Returns the first element of a buffer, or a default value if the buffer contains no elements.</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public T? FirstOrDefault() => this.Count != 0 ? this.Buffer[0] : default;

		/// <summary>Returns the last element of a buffer.</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public T Last() => this.Count != 0 ? this.Buffer[this.Count - 1] : throw BufferHasNoElements();

		/// <summary>Returns the last element of a buffer, or a default value if the buffer contains no elements.</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public T? LastOrDefault() => this.Count == 0 ? default : this.Buffer[this.Count - 1];

		#endregion

		/// <inheritdoc />
		public override string ToString()
		{
			if (typeof(T) == typeof(char))
			{ // => string
				return this.Span.ToString();
			}

			if (typeof(T) == typeof(byte))
			{ // => base64

				// we need to trick the compiler into casting Span<T> into Span<byte>!
				var bytes = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(this.Buffer)), this.Count);

				return Convert.ToBase64String(bytes);
			}

			return $"{nameof(ValueBuffer<>)}<{typeof(T).Name}>[{this.Count}]";
		}

		/// <inheritdoc />
		public void Dispose()
		{
			Clear(release: true);
		}

	}

	/// <summary>Helper methods for working with <see cref="ValueBuffer{T}"/></summary>
	[PublicAPI]
	public static class ValueBufferExtensions
	{

		/// <summary>Adds a string to a <see cref="ValueBuffer{T}"/></summary>
		public static void Add(this ValueBuffer<char> buffer, string text)
		{
			buffer.AddRange(text.AsSpan());
		}

	}

	[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
	internal class ValueBufferDebugView<T>
	{
		public ValueBufferDebugView(ValueBuffer<T> buffer)
		{
			this.Items = buffer.ToArray();
		}

		[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
		public T[] Items { get; set; }

	}

}
