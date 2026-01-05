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

namespace FoundationDB.Client
{
	using System.Collections;

	/// <summary>Metadata about the result of a range read operation</summary>
	public sealed class FdbRangeResult
	{

		// this is used for by the "visitor" to keep track of some metadata required to do multipage reads
		// => the keys and values have already been consumed (and probably returned to the pool) before this instance is created!

		public FdbRangeResult(int count, bool hasMore, int iteration, FdbRangeOptions options, Slice first, Slice last, int totalBytes)
		{
			this.Count = count;
			this.HasMore = hasMore;
			this.Iteration = iteration;
			this.Options = options;
			this.First = first;
			this.Last = last;
			this.TotalBytes = totalBytes;
		}

		/// <summary>Number of results returned by this range read</summary>
		public int Count { get; }

		/// <summary>Set to <see langword="true"/> if there are more results in the database than could fit in a single chunk</summary>
		public bool HasMore { get; }

		/// <summary>Iteration number of this chunk, starting from 1 for the first page</summary>
		/// <remarks>This value must be passed to subsequent calls to <see cref="IFdbReadOnlyTransaction.GetRangeAsync(KeySelector,KeySelector,FdbRangeOptions?,int)"/> to continue reading; otherwise, the <see cref="FdbRangeOptions.Streaming">streaming</see> will not behave properly.</remarks>
		public int Iteration { get; }

		/// <summary>Options used to perform this operation</summary>
		/// <remarks>They can be passed again to <see cref="IFdbReadOnlyTransaction.GetRangeAsync(KeySelector,KeySelector,FdbRangeOptions?,int)"/>, together with <c><see cref="Iteration"/> + 1</c> to continue reading from the database.</remarks>
		public FdbRangeOptions Options { get; }

		/// <summary>Returns the first item in the chunk</summary>
		/// <remarks>Note that if the range is reversed, then the first item will be GREATER than the last !</remarks>
		public Slice Last { get; private set; }

		/// <summary>Returns the last item in the chunk</summary>
		/// <remarks>Note that if the range is reversed, then the last item will be LESS than the first!</remarks>
		public Slice First { get; private set; }

		/// <summary>Sum of the size (in bytes) of all the keys and values read from the database</summary>
		public int TotalBytes { get; private set; }

	}

	/// <summary>Page of results from a <see cref="IFdbReadOnlyTransaction.GetRangeAsync(KeySelector,KeySelector,FdbRangeOptions?,int)"/> operation</summary>
	/// <remarks>If a pool was specified in the <see cref="FdbRangeOptions"/>, then this instance <b>MUST</b> be disposed; otherwise, rented buffers will not be returned to the pool</remarks>
	[DebuggerDisplay("Count={Count}, HasMore={HasMore}, Reversed={Reversed}, Iteration={Iteration}")]
	[PublicAPI]
	public sealed class FdbRangeChunk : IReadOnlyList<KeyValuePair<Slice, Slice>>, IDisposable
	{

		/// <summary>Keys and Values that were read from the database</summary>
		/// <remarks>The entries are sorted by keys, either with an ascending or descending order, depending on the value of <see cref="Reversed"/></remarks>
		public KeyValuePair<Slice, Slice>[] Items { get; private set; }

		/// <summary>Set to <see langword="true"/> if there are more results in the database than could fit in a single chunk</summary>
		public bool HasMore { get; }

		/// <summary>Iteration number of this chunk, starting from 1 for the first page</summary>
		/// <remarks>This value must be passed to subsequent calls to <see cref="IFdbReadOnlyTransaction.GetRangeAsync(KeySelector,KeySelector,FdbRangeOptions?,int)"/> to continue reading; otherwise, the <see cref="FdbRangeOptions.Streaming">streaming</see> will not behave properly.</remarks>
		public int Iteration { get; }

		/// <summary>Options used to perform this operation</summary>
		/// <remarks>They can be passed again to <see cref="IFdbReadOnlyTransaction.GetRangeAsync(KeySelector,KeySelector,FdbRangeOptions?,int)"/>, together with <c><see cref="Iteration"/> + 1</c> to continue reading from the database.</remarks>
		public FdbRangeOptions Options { get; }

		/// <summary>Returns the first item in the chunk</summary>
		/// <remarks>Note that if the range is reversed, then the first item will be GREATER than the last !</remarks>
		public Slice Last { get; private set; }

		/// <summary>Returns the last item in the chunk</summary>
		/// <remarks>Note that if the range is reversed, then the last item will be LESS than the first!</remarks>
		public Slice First { get; private set; }

		/// <summary>Sum of the size (in bytes) of all the keys and values read from the database</summary>
		public int TotalBytes { get; private set; }

		/// <summary>Internal buffer used to store the content of <see cref="Items"/>, which may be rented from a pool</summary>
		private SliceOwner Buffer;

		public FdbRangeChunk(KeyValuePair<Slice, Slice>[] items, bool hasMore, int iteration, FdbRangeOptions options, Slice first, Slice last, int totalBytes, SliceOwner buffer)
		{
			Contract.NotNull(items);
			this.Items = items;
			this.HasMore = hasMore;
			this.Iteration = iteration;
			this.Options = options;
			this.First = first;
			this.Last = last;
			this.TotalBytes = totalBytes;
			this.Buffer = buffer;
		}

		/// <summary>Returns the number of results in this chunk</summary>
		public int Count => this.Items.Length;

		/// <summary>Returns true if the chunk does not contain any item.</summary>
		public bool IsEmpty => this.Items.Length == 0;

		/// <summary>Set to <see langword="true"/> if the original range read was reversed (meaning the items are in reverse lexicographic order</summary>
		public bool Reversed => this.Options.IsReversed;

		/// <summary>Specifies if the chunk contains only keys, only values, or both (default)</summary>
		public FdbFetchMode FetchMode => this.Options.Fetch ?? FdbFetchMode.KeysAndValues;

		/// <summary>Releases any buffer used by this chunk, if it was pooled</summary>
		/// <remarks>
		/// <para><b>CAUTION</b>: Any key or value slice obtained from this instance MUST NOT be used after this has been disposed!</para>
		/// <para>This does nothing if the no pool was specified in the <see cref="FdbRangeOptions"/></para>
		/// <para>If any pooled content must survive this instance, it MUST be copied to a new <see cref="Slice"/>, or transferred to another <see cref="SliceOwner"/> instance!</para>
		/// </remarks>
		public void Dispose()
		{
			this.TotalBytes = 0;
			this.Items.AsSpan().Clear();
			this.First = default;
			this.Last = default;
			this.Buffer.Dispose();
		}

		#region Items...

		/// <summary>Return a reference to the result at the specified index</summary>
		public KeyValuePair<Slice, Slice> this[int index]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Items[index];
		}

		/// <summary>Return a reference to the result at the specified index</summary>
		public KeyValuePair<Slice, Slice> this[Index index]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Items[index];
		}

		/// <summary>Return a slice of the results in the specified range</summary>
		public ReadOnlySpan<KeyValuePair<Slice, Slice>> this[Range range]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Items.AsSpan(range);
		}

		/// <summary>Return a reference to the result at the specified index</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ref readonly KeyValuePair<Slice, Slice> ItemRef(int index) => ref this.Items[index];

		public KeyValuePair<Slice, Slice>[] ToArray()
		{
			var tmp = new KeyValuePair<Slice, Slice>[this.Count];
			this.Items.CopyTo(tmp, 0);
			return tmp;
		}

		public void CopyTo(KeyValuePair<Slice, Slice>[] array, int offset)
		{
			this.Items.CopyTo(array, offset);
		}

		public ChunkEnumerator GetEnumerator()
		{
			return new ChunkEnumerator(this.Items);
		}

		IEnumerator<KeyValuePair<Slice, Slice>> IEnumerable<KeyValuePair<Slice, Slice>>.GetEnumerator() => GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public struct ChunkEnumerator : IEnumerator<KeyValuePair<Slice, Slice>>
		{

			private readonly KeyValuePair<Slice, Slice>[] Items;
			private int Index;

			public ChunkEnumerator(KeyValuePair<Slice, Slice>[] items)
			{
				this.Items = items;
				this.Index = -1;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool MoveNext()
			{
				int p = this.Index + 1;
				if (p >= this.Items.Length) return MoveNextRare();
				this.Index = p;
				return true;
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private bool MoveNextRare()
			{
				this.Index = this.Items.Length;
				return false;
			}

			public KeyValuePair<Slice, Slice> Current
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => this.Items[this.Index];
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Dispose()
			{ }

			object IEnumerator.Current => this.Current;

			void IEnumerator.Reset()
			{
				this.Index = -1;
			}

		}

		#endregion

		#region Keys...

		public KeysCollection Keys => new(this.Items);

		public readonly struct KeysCollection : IReadOnlyList<Slice>
		{
			private readonly KeyValuePair<Slice, Slice>[] Items;

			internal KeysCollection(KeyValuePair<Slice, Slice>[] items)
			{
				this.Items = items;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public ChunkKeysEnumerator GetEnumerator()
			{
				return new ChunkKeysEnumerator(this.Items);
			}

			IEnumerator<Slice> IEnumerable<Slice>.GetEnumerator() => GetEnumerator();

			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

			public int Count => this.Items.Length;

			public Slice this[int index] => this.Items[index].Key;
		}

		public struct ChunkKeysEnumerator : IEnumerator<Slice>
		{

			private readonly KeyValuePair<Slice, Slice>[] Items;
			private int Index;

			public ChunkKeysEnumerator(KeyValuePair<Slice, Slice>[] items)
			{
				this.Items = items;
				this.Index = -1;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool MoveNext()
			{
				int p = this.Index + 1;
				if (p >= this.Items.Length) return MoveNextRare();
				this.Index = p;
				return true;
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private bool MoveNextRare()
			{
				this.Index = this.Items.Length;
				return false;
			}

			public Slice Current
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => this.Items[this.Index].Key;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Dispose()
			{ }

			object IEnumerator.Current => this.Current;

			void IEnumerator.Reset()
			{
				this.Index = -1;
			}

		}

		#endregion

		#region Values...

		public ValuesCollection Values => new(this.Items);

		/// <summary>Append all the values into a buffer, in sequential order</summary>
		/// <returns>Slice with all values copied in sequential order, or <see cref="Slice.Nil"/> if the chunk is empty</returns>
		/// <remarks>
		/// This is useful when using a range queries to read all the chunks of a single entity.
		/// Since this method will allocate and copy all the results, if the expected size is expected to be large, prefer iterating over the <see cref="Values"/> instead!
		/// </remarks>
		public Slice ConcatValues()
		{
			var items = this.Items;
			switch (items.Length)
			{
				case 0: return Slice.Nil;
				case 1: return items[0].Value;
				case 2: return items[0].Value + items[1].Value;
				default:
				{
					// compute the total sum
					var sw = new SliceWriter();
					AppendValues(ref sw);
					return sw.ToSlice();
				}
			}
		}

		/// <summary>Append all the values into a buffer, in sequential order</summary>
		/// <param name="writer">Buffer where to write all the values</param>
		/// <returns>Total number of bytes written to the buffer</returns>
		public void AppendValues(ref SliceWriter writer)
		{
			var items = this.Items;
			switch (items.Length)
			{
				case 0:
				{
					break;
				}
				case 1:
				{
					var value = items[0].Value;
					writer.WriteBytes(value);
					break;
				}
				default:
				{
					// TotalBytes is the sum of Keys+Values, but we only want the values,
					// so unfortunately, we have to re-compute the sum
					int total = 0;
					for (int i = 0; i < items.Length; i++)
					{
						total += items[i].Value.Count;
					}

					// sanity check
					if ((uint) total >= (uint) this.TotalBytes)
					{
						throw new OutOfMemoryException("Total size of merged values exceeds maximum allowed value.");
					}

					// pre-allocate the size required
					var span = writer.AllocateSpan(total);
					for (int i = 0; i < items.Length; i++)
					{
						items[i].Value.CopyTo(span);
						span = span[items[i].Value.Count..];
					}
					Contract.Debug.Ensures(span.Length == 0);

					break;
				}
			}
		}

		public readonly struct ValuesCollection : IReadOnlyList<Slice>
		{
			private readonly KeyValuePair<Slice, Slice>[] Items;

			internal ValuesCollection(KeyValuePair<Slice, Slice>[] items)
			{
				this.Items = items;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public ChunkKeysEnumerator GetEnumerator()
			{
				return new ChunkKeysEnumerator(this.Items);
			}

			IEnumerator<Slice> IEnumerable<Slice>.GetEnumerator() => GetEnumerator();

			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

			public int Count => this.Items.Length;

			public Slice this[int index] => this.Items[index].Value;
		}

		public struct ChunkValuesEnumerator : IEnumerator<Slice>
		{

			private readonly KeyValuePair<Slice, Slice>[] Items;
			private int Index;

			public ChunkValuesEnumerator(KeyValuePair<Slice, Slice>[] items)
			{
				this.Items = items;
				this.Index = -1;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool MoveNext()
			{
				int p = this.Index + 1;
				if (p >= this.Items.Length) return MoveNextRare();
				this.Index = p;
				return true;
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private bool MoveNextRare()
			{
				this.Index = this.Items.Length;
				return false;
			}

			public Slice Current
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => this.Items[this.Index].Value;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Dispose()
			{ }

			object IEnumerator.Current => this.Current;

			void IEnumerator.Reset()
			{
				this.Index = -1;
			}

		}

		#endregion

		#region Decoding Helpers...

		//REVIEW: is this really used? It could be moved to a set of extensions methods instead?

		/// <summary>Decode the content of this chunk into an array of typed key/value pairs</summary>
		/// <typeparam name="TKey">Type of the keys</typeparam>
		/// <typeparam name="TValue">Type of the values</typeparam>
		/// <param name="keyHandler">Lambda that can decode the keys of this chunk</param>
		/// <param name="valueHandler">Lambda that can decode the values of this chunk</param>
		/// <returns>Array of decoded key/value pairs, or an empty array if the chunk doesn't have any results</returns>
		public KeyValuePair<TKey, TValue>[] Decode<TKey, TValue>(Func<Slice, TKey> keyHandler, Func<Slice, TValue> valueHandler)
		{
			Contract.NotNull(keyHandler);
			Contract.NotNull(valueHandler);

			var results = this.Items;
			var items = new KeyValuePair<TKey, TValue>[results.Length];

			for (int i = 0; i < results.Length; i++)
			{
				items[i] = new KeyValuePair<TKey, TValue>(
					keyHandler(results[i].Key),
					valueHandler(results[i].Value)
				);
			}

			return items;
		}

		/// <summary>Decode the content of this chunk into an array of typed keys</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="handler">Instance used to decode the keys of this chunk</param>
		/// <returns>Array of decoded keys, or an empty array if the chunk doesn't have any results</returns>
		public T[] DecodeKeys<T>(Func<Slice, T> handler)
		{
			Contract.NotNull(handler);

			var results = this.Items;
			var keys = new T[results.Length];
			for (int i = 0; i < keys.Length; i++)
			{
				keys[i] = handler(results[i].Key);
			}
			return keys;
		}

		/// <summary>Decode the content of this chunk into an array of typed values</summary>
		/// <typeparam name="T">Type of the values</typeparam>
		/// <param name="handler">Lambda that can decode the values of this chunk</param>
		/// <returns>Array of decoded values, or an empty array if the chunk doesn't have any results</returns>
		public T[] DecodeValues<T>(Func<Slice, T> handler)
		{
			Contract.NotNull(handler);

			var results = this.Items;
			var values = new T[results.Length];
			for (int i = 0; i < values.Length; i++)
			{
				values[i] = handler(results[i].Value);
			}
			return values;
		}

		/// <summary>Decode the content of this chunk into an array of typed values</summary>
		/// <typeparam name="T">Type of the values</typeparam>
		/// <param name="valueEncoder">Instance used to decode the values of this chunk</param>
		/// <returns>Array of decoded values, or an empty array if the chunk doesn't have any results</returns>
		public T[] DecodeValues<T>(IValueEncoder<T> valueEncoder)
		{
			Contract.NotNull(valueEncoder);

			var results = this.Items;
			var values = new T[results.Length];
			for (int i = 0; i < values.Length; i++)
			{
				values[i] = valueEncoder.DecodeValue(results[i].Value)!;
			}
			return values;
		}

		#endregion

	}

	[DebuggerDisplay("Count={Count}, HasMore={HasMore}, Reversed={Reversed}, Iteration={Iteration}")]
	[PublicAPI]
	public sealed class FdbRangeChunk<TResult> : IReadOnlyList<TResult>
	{

		/// <summary>Contains the items that where </summary>
		public ReadOnlyMemory<TResult> Items { get; }

		/// <summary>Set to true if there are more results in the database than could fit in a single chunk</summary>
		public bool HasMore { get; }

		/// <summary>Iteration number of this chunk (used when paging through a long range)</summary>
		public int Iteration { get; }

		/// <summary>Options used to perform this operation</summary>
		public FdbRangeOptions Options { get; }

		/// <summary>Returns the first item in the chunk</summary>
		/// <remarks>Note that if the range is reversed, then the first item will be GREATER than the last !</remarks>
		public Slice Last { get; }

		/// <summary>Returns the last item in the chunk</summary>
		/// <remarks>Note that if the range is reversed, then the last item will be LESS than the first!</remarks>
		public Slice First { get; }

		/// <summary>Sum of the size (in bytes) of all the keys and values read from the database</summary>
		/// <returns>This is the size of the data read from the network, before they were decoded into instances of <typeparamref name="TResult"/></returns>
		public int TotalBytes { get; }

		public FdbRangeChunk(ReadOnlyMemory<TResult> items, bool hasMore, int iteration, FdbRangeOptions options, Slice first, Slice last, int totalBytes)
		{
			this.Items = items;
			this.HasMore = hasMore;
			this.Iteration = iteration;
			this.Options = options;
			this.First = first;
			this.Last = last;
			this.TotalBytes = totalBytes;
		}

		/// <summary>Returns the number of results in this chunk</summary>
		public int Count => this.Items.Length;

		/// <summary>Returns true if the chunk does not contain any item.</summary>
		public bool IsEmpty => this.Items.Length == 0;

		/// <summary>Set to true if the original range read was reversed (meaning the items are in reverse lexicographic order</summary>
		public bool Reversed => this.Options.IsReversed;

		/// <summary>Specifies if the chunk contains only keys, only values, or both (default)</summary>
		public FdbFetchMode FetchMode => this.Options.Fetch ?? FdbFetchMode.KeysAndValues;

		#region Items...

		/// <summary>Return a reference to the result at the specified index</summary>
		public TResult this[int index]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Items.Span[index];
		}

		/// <summary>Return a reference to the result at the specified index</summary>
		public TResult this[Index index]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Items.Span[index];
		}

		/// <summary>Return a slice of the results in the specified range</summary>
		public ReadOnlySpan<TResult> this[Range range]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Items.Span[range];
		}

		/// <summary>Return a reference to the result at the specified index</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ref readonly TResult ItemRef(int index) => ref this.Items.Span[index];

		public TResult[] ToArray()
		{
			return this.Items.ToArray();
		}

		public void CopyTo(TResult[] array, int offset = 0)
		{
			this.Items.Span.CopyTo(array.AsSpan(offset));
		}

		public void CopyTo(Span<TResult> destination)
		{
			this.Items.Span.CopyTo(destination);
		}

		public ChunkEnumerator GetEnumerator()
		{
			return new ChunkEnumerator(this.Items);
		}

		IEnumerator<TResult> IEnumerable<TResult>.GetEnumerator() => GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public struct ChunkEnumerator : IEnumerator<TResult>
		{

			private readonly ReadOnlyMemory<TResult> Items;
			private int Index;

			public ChunkEnumerator(ReadOnlyMemory<TResult> items)
			{
				this.Items = items;
				this.Index = -1;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool MoveNext()
			{
				int p = this.Index + 1;
				if (p >= this.Items.Length) return MoveNextRare();
				this.Index = p;
				return true;
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private bool MoveNextRare()
			{
				this.Index = this.Items.Length;
				return false;
			}

			public TResult Current
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => this.Items.Span[this.Index];
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Dispose()
			{ }

			object? IEnumerator.Current => this.Current;

			void IEnumerator.Reset()
			{
				this.Index = -1;
			}

		}

		#endregion

	}

}
