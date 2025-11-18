#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace SnowBank.Data.Json
{
	using System.Collections;
	using SnowBank.Runtime;

	/// <summary>Wraps a <see cref="JsonArray"/> into a typed mutable proxy that emulates an array of elements of type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated element type</typeparam>
	[PublicAPI]
	[CollectionBuilder(typeof(JsonWritableProxyArrayBuilder), nameof(JsonWritableProxyArrayBuilder.Create))]
	public readonly struct JsonWritableProxyArray<TValue> : IList<TValue>, IJsonProxyNode, IJsonSerializable, IJsonPackable
	{

		private readonly MutableJsonValue m_value;

		private readonly IJsonConverter<TValue> m_converter;

		private readonly IEqualityComparer<TValue> m_comparer;

		public JsonWritableProxyArray(MutableJsonValue value, IJsonConverter<TValue>? converter = null, IJsonProxyNode? parent = null, JsonPathSegment segment = default, IEqualityComparer<TValue>? comparer = null)
		{
			m_value = value;
			m_converter = converter ?? RuntimeJsonConverter<TValue>.Default;
			m_comparer = comparer ?? EqualityComparer<TValue>.Default;
		}

		#region IJsonProxyNode...

		/// <inheritdoc />
		JsonType IJsonProxyNode.Type => JsonType.Array;

		/// <inheritdoc />
		IJsonProxyNode? IJsonProxyNode.Parent => m_value.Parent;

		/// <inheritdoc />
		JsonPathSegment IJsonProxyNode.Segment => m_value.Segment;

		/// <inheritdoc />
		int IJsonProxyNode.Depth => m_value.Depth;

		/// <inheritdoc />
		void IJsonProxyNode.WritePath(ref JsonPathBuilder builder) => m_value.WritePath(ref builder);

		/// <inheritdoc />
		public JsonPath GetPath() => m_value.GetPath();

		/// <inheritdoc />
		public JsonPath GetPath(JsonPathSegment child) => m_value.GetPath(child);

		#endregion

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException OperationRequiresArray() => new("This operation requires a valid JSON Array");

		public TValue[] ToArray() => m_value.Json switch
		{
			JsonArray arr => m_converter.UnpackArray(arr),
			JsonNull => [ ],
			_ => throw OperationRequiresArray(),
		};

		public List<TValue> ToList() => m_value.Json switch
		{
			JsonArray arr => m_converter.UnpackList(arr),
			JsonNull => [ ],
			_ => throw OperationRequiresArray(),
		};

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_value.Json.JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_value.Json;

		public JsonValue ToJsonValue() => m_value.Json;

		/// <inheritdoc />
		public IEnumerator<TValue> GetEnumerator()
		{
			if (m_value.Json is not JsonArray arr)
			{
				if (m_value.Json is JsonNull)
				{
					yield break;
				}
				throw OperationRequiresArray();
			}

			foreach (var item in arr)
			{
				yield return Unpack(item);
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <inheritdoc />
		public void Add(TValue item)
		{
			// this method could be called on an empty instance by the compiler when the call-site is a collection expression: (foo).SomeArray = [ 1, 2, 3 ];
			// => in theory we have defined a CollectionBuilder for this type, but the behavior may change in future versions?
			Contract.Debug.Requires(m_value != null && m_converter != null, "Invalid collection expression initializer");

			m_value.Add(m_converter.Pack(item));
		}

		/// <inheritdoc />
		public void Clear() => m_value.Clear();

		/// <inheritdoc />
		public void CopyTo(TValue[] array, int arrayIndex)
		{
			Contract.NotNull(array);
			Contract.Positive(arrayIndex);
			Contract.DoesNotOverflow(array, arrayIndex, m_value.Count);

			if (m_value.Json is not JsonArray arr)
			{
				if (m_value.Json is JsonNull)
				{
					return;
				}
				throw OperationRequiresArray();
			}

			foreach (var item in arr)
			{
				array[arrayIndex++] = Unpack(item);
			}
		}

		/// <inheritdoc />
		bool ICollection<TValue>.Remove(TValue item) => throw new NotSupportedException("This operation is too costly.");

		/// <inheritdoc />
		int IList<TValue>.IndexOf(TValue item) => throw new NotSupportedException("This operation is too costly.");

		/// <inheritdoc />
		public int Count { get; }

		/// <summary>Tests if the array is present.</summary>
		/// <returns><c>false</c> if the wrapped JSON value is null or missing; otherwise, <c>true</c>.</returns>
		public bool Exists() => m_value.Exists();

		/// <summary>Tests if the array is null or missing.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is null or missing; otherwise, <c>false</c>.</returns>
		/// <remarks>This can return <c>false</c> if the wrapped value is another type, like an object, string literal, etc...</remarks>
		public bool IsNullOrMissing() => m_value.IsNullOrMissing();

		/// <summary>Tests if the array is null, missing, or empty.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is null, missing or an empty array; otherwise, <c>false</c>.</returns>
		/// <remarks>This can return <c>false</c> if the wrapped value is another type, like an object, string literal, etc...</remarks>
		public bool IsNullOrEmpty() => m_value.Json switch { JsonArray arr => arr.Count != 0, JsonNull => true, _ => false };

		/// <summary>Tests if the wrapped value is a valid JSON Array.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is a non-null Array; otherwise, <c>false</c></returns>
		/// <remarks>This can be used to protect against malformed JSON document that would have a different type (object, string literal, ...).</remarks>
		public bool IsArray() => m_value.Json is JsonArray;

		/// <summary>Tests if the wrapped value is a valid JSON Array, or is null-or-missing.</summary>
		/// <returns><c>true</c> if the wrapped JSON value either null-or-missing, or an Array; otherwise, <c>false</c></returns>
		/// <remarks>This can be used to protect against malformed JSON document that would have a different type (object, string literal, ...).</remarks>
		public bool IsArrayOrMissing() => m_value.Json is (JsonArray or JsonNull);

		/// <inheritdoc />
		bool ICollection<TValue>.IsReadOnly => false;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private TValue Unpack(JsonValue value) => m_converter.Unpack(value, null);

		/// <inheritdoc />
		public TValue this[int index]
		{
			get => Unpack(m_value.Json[index]);
			set => m_value.Set(index, m_converter.Pack(value));
		}

		public TValue this[Index index]
		{
			get => Unpack(m_value.Json[index]);
			set => m_value.Set(index, m_converter.Pack(value));
		}

		/// <inheritdoc />
		public void Insert(int index, TValue item) => m_value.Insert(index, m_converter.Pack(item));

		/// <inheritdoc />
		public void RemoveAt(int index) => m_value.RemoveAt(index);

		/// <inheritdoc />
		public bool Contains(TValue item)
		{
			if (m_value.Json is not JsonArray arr)
			{
				if (m_value.Json is JsonNull)
				{
					return false;
				}
				throw OperationRequiresArray();
			}

			foreach (var child in arr.GetSpan())
			{
				if (child.ValueEquals(item, m_comparer))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Find the index of the first occurrence of the specified value in this array.</summary>
		/// <param name="item">The item to locate in the array.</param>
		/// <returns>The zero-based index of the first occurrence of <paramref name="item" /> within the entire array, if found; otherwise, -1</returns>
		/// <exception cref="InvalidOperationException">If the wrapped JSON value is neither null nor an array.</exception>
		public int IndexOf(TValue item) => IndexOf(item, 0);

		/// <summary>Find the index of the first occurrence of the specified value in this array.</summary>
		/// <param name="item">The item to locate in the array.</param>
		/// <param name="index">The zero-based starting index of the search. 0 (zero) is valid in an empty array.</param>
		/// <returns>The zero-based index of the first occurrence of <paramref name="item" /> within the range of elements in the array that extends from <paramref name="index" /> to the last element, if found; otherwise, -1</returns>
		/// <exception cref="InvalidOperationException">If the wrapped JSON value is neither null nor an array.</exception>
		public int IndexOf(TValue item, int index)
		{
			Contract.Positive(index);
			const int NOT_FOUND = -1;

			if (m_value.Json is not JsonArray arr)
			{
				if (m_value.Json is JsonNull)
				{
					return NOT_FOUND;
				}
				throw OperationRequiresArray();
			}

			var span = arr.GetSpan();
			for (int i = index; i < span.Length; i++)
			{
				if (span[i].ValueEquals(item, m_comparer))
				{
					return i;
				}
			}

			return NOT_FOUND;
		}

		/// <summary>Searches for an item that matches the conditions defined by the specified predicate, and returns the first occurrence within the entire array.</summary>
		/// <param name="match">The <see cref="T:System.Predicate`1" /> delegate that defines the conditions of the element to search for.</param>
		/// <returns>The first element that matches the conditions defined by the specified predicate, if found; otherwise, the default value for type <typeparamref name="TValue" />.</returns>
		/// <exception cref="InvalidOperationException">If the wrapped JSON value is neither null nor an array.</exception>
		public TValue? Find(Predicate<TValue> match)
		{
			if (m_value.Json is not JsonArray arr)
			{
				if (m_value.Json is JsonNull)
				{
					return default;
				}
				throw OperationRequiresArray();
			}

			foreach (var item in arr.GetSpan())
			{
				var value = Unpack(item);
				if (match(value))
				{
					return value;
				}
			}

			return default;
		}

		/// <summary>Retrieves all the values that match the conditions defined by the specified predicate.</summary>
		/// <param name="match">The <see cref="T:System.Predicate`1" /> delegate that defines the conditions of the element to search for.</param>
		/// <returns>A <see cref="T:System.Collections.Generic.List`1" /> containing all the values that match the conditions defined by the specified predicate, if found; otherwise, an empty <see cref="T:System.Collections.Generic.List`1" />.</returns>
		/// <exception cref="InvalidOperationException">If the wrapped JSON value is neither null nor an array.</exception>
		public List<TValue> FindAll(Predicate<TValue> match)
		{
			if (m_value.Json is not JsonArray arr)
			{
				if (m_value.Json is JsonNull)
				{
					return [ ];
				}
				throw OperationRequiresArray();
			}

			var matches = new List<TValue>();
			foreach (var item in arr.GetSpan())
			{
				var value = Unpack(item);
				if (match(value))
				{
					matches.Add(value);
				}
			}
			return matches;
		}

		/// <summary>Removes the first occurence of a specific value from the array.</summary>
		/// <param name="item">The value to remove from the array.</param>
		/// <returns><see langword="true" /> if <paramref name="item" /> is found and removed; otherwise, <see langword="false" />.</returns>
		/// <exception cref="InvalidOperationException">If the wrapped JSON value is neither null nor an array.</exception>
		public bool Remove(TValue item)
		{
			if (m_value.Json is not JsonArray arr)
			{
				if (m_value.Json is JsonNull)
				{
					return false;
				}
				throw OperationRequiresArray();
			}

			var span = arr.GetSpan();
			for (int i = 0; i < span.Length; i++)
			{
				if (span[i].ValueEquals(item, m_comparer))
				{
					m_value.RemoveAt(i);
					return true;
				}
			}

			return false;
		}

		/// <summary>Applies an accumulator function over the items on this array. The specified seed value is used as the initial accumulator value.</summary>
		/// <typeparam name="TAccumulate">The type of the accumulator value.</typeparam>
		/// <param name="seed">The initial accumulator value.</param>
		/// <param name="handler">An accumulator function to be invoked on each element.</param>
		/// <returns>The final accumulator value.</returns>
		/// <exception cref="InvalidOperationException">If the wrapped JSON value is neither null nor an array.</exception>
		public TAccumulate Aggregate<TAccumulate>(TAccumulate seed, Func<TAccumulate, TValue, TAccumulate> handler)
		{
			if (m_value.Json is not JsonArray arr)
			{
				if (m_value.Json is JsonNull)
				{
					return seed;
				}
				throw OperationRequiresArray();
			}

			var accumulator = seed;
			foreach (var item in arr.GetSpan())
			{
				accumulator = handler(accumulator, Unpack(item));
			}

			return accumulator;
		}

		/// <summary>Applies an accumulator function over the items on this array. The specified seed value is used as the initial accumulator value.</summary>
		/// <typeparam name="TAccumulate">The type of the accumulator value.</typeparam>
		/// <typeparam name="TResult">The type of the resulting value.</typeparam>
		/// <param name="seed">The initial accumulator value.</param>
		/// <param name="handler">An accumulator function to be invoked on each element.</param>
		/// <param name="resultSelector">A function to transform the final accumulator value into the result value.</param>
		/// <returns>The transformed final accumulator value.</returns>
		/// <exception cref="InvalidOperationException">If the wrapped JSON value is neither null nor an array.</exception>
		public TResult Aggregate<TAccumulate, TResult>(TAccumulate seed, Func<TAccumulate, TValue, TAccumulate> handler, Func<TAccumulate, TResult> resultSelector)
		{
			if (m_value.Json is not JsonArray arr)
			{
				if (m_value.Json is JsonNull)
				{
					return resultSelector(seed);
				}
				throw OperationRequiresArray();
			}

			var accumulator = seed;
			foreach (var item in arr.GetSpan())
			{
				accumulator = handler(accumulator, Unpack(item));
			}

			return resultSelector(accumulator);
		}

		/// <summary>Visit all the elements of this array.</summary>
		/// <param name="handler">A function to be invoked on each element.</param>
		/// <exception cref="InvalidOperationException">If the wrapped JSON value is neither null nor an array.</exception>
		public void Visit(Action<TValue> handler)
		{
			if (m_value.Json is not JsonArray arr)
			{
				if (m_value.Json is JsonNull)
				{
					return;
				}
				throw OperationRequiresArray();
			}

			foreach (var item in arr.GetSpan())
			{
				handler(Unpack(item));
			}
		}

		/// <summary>Visit all the elements of this array.</summary>
		/// <param name="handler">A function to be invoked on each element.</param>
		/// <exception cref="InvalidOperationException">If the wrapped JSON value is neither null nor an array.</exception>
		public void Visit(Action<TValue, int> handler)
		{
			if (m_value.Json is not JsonArray arr)
			{
				if (m_value.Json is JsonNull)
				{
					return;
				}
				throw OperationRequiresArray();
			}

			var span = arr.GetSpan();
			for(int i = 0; i < span.Length; i++)
			{
				handler(Unpack(span[i]), i);
			}
		}

		/// <inheritdoc />
		public override string ToString() => $"({typeof(TValue).GetFriendlyName()}[]) {m_value}";

	}

	/// <summary>Wraps a <see cref="JsonArray"/> into a typed mutable proxy that emulates an array of elements of type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated element type</typeparam>
	/// <typeparam name="TProxy">Corresponding <see cref="IJsonWritableProxy{TValue}"/> for type <typeparamref name="TValue"/>, usually source-generated</typeparam>
	[PublicAPI]
	public sealed class JsonWritableProxyArray<TValue, TProxy> : IList<TProxy>, IJsonProxyNode, IJsonSerializable, IJsonPackable
		where TProxy : IJsonWritableProxy<TValue, TProxy>
	{

		private readonly MutableJsonValue m_value;

		public JsonWritableProxyArray(MutableJsonValue value)
		{
			m_value = value;
		}

		#region IJsonProxyNode...

		/// <inheritdoc />
		JsonType IJsonProxyNode.Type => JsonType.Array;

		/// <inheritdoc />
		IJsonProxyNode? IJsonProxyNode.Parent => m_value.Parent;

		/// <inheritdoc />
		JsonPathSegment IJsonProxyNode.Segment => m_value.Segment;

		/// <inheritdoc />
		int IJsonProxyNode.Depth => m_value.Depth;

		/// <inheritdoc />
		void IJsonProxyNode.WritePath(ref JsonPathBuilder builder) => m_value.WritePath(ref builder);

		/// <inheritdoc />
		public JsonPath GetPath() => m_value.GetPath();

		/// <inheritdoc />
		public JsonPath GetPath(JsonPathSegment child) => m_value.GetPath(child);

		#endregion

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static JsonValue Pack(TValue item) => TProxy.Converter.Pack(item, CrystalJsonSettings.JsonReadOnly);

		/// <summary>Replaces all the elements of this array</summary>
		/// <param name="items">New elements</param>
		public void Set(ReadOnlySpan<TValue> items)
		{
			m_value.Set(TProxy.Converter.PackSpan(items, CrystalJsonSettings.JsonReadOnly));
		}

		/// <summary>Replaces all the elements of this array</summary>
		/// <param name="items">New elements</param>
		public void Set(TValue[] items)
		{
			m_value.Set(TProxy.Converter.PackArray(items, CrystalJsonSettings.JsonReadOnly));
		}

		/// <summary>Replaces all the elements of this array</summary>
		/// <param name="items">New elements</param>
		public void Set(List<TValue> items)
		{
			m_value.Set(TProxy.Converter.PackList(items, CrystalJsonSettings.JsonReadOnly));
		}

		/// <summary>Replaces all the elements of this array</summary>
		/// <param name="items">New elements</param>
		public void Set(IEnumerable<TValue> items)
		{
			m_value.Set(TProxy.Converter.PackEnumerable(items, CrystalJsonSettings.JsonReadOnly));
		}

		/// <summary>Returns the deserialized elements as a <typeparamref name="TValue"/> array.</summary>
		/// <remarks>
		/// <para>If the contents are only meant to be enumerated once (via <c>foreach</c> or LINQ), you may want to call <see cref="ToEnumerable"/> instead</para>
		/// </remarks>
		public TValue[] ToArray() => m_value.Json switch
		{
			JsonArray arr => TProxy.Converter.UnpackArray(arr),
			JsonNull => [ ],
			_ => throw OperationRequiresArrayOrNull(),
		};

		/// <summary>Returns the deserialized elements as a <see cref="List{TValue}"/></summary>
		public List<TValue> ToList() => m_value.Json switch
		{
			JsonArray arr => TProxy.Converter.UnpackList(arr),
			JsonNull => [ ],
			_ => throw OperationRequiresArrayOrNull(),
		};

		/// <summary>Returns a sequence that enumerates the deserialized <typeparamref name="TValue"/> elements in this array</summary>
		public Enumerable ToEnumerable() => m_value.Json switch
		{
			JsonArray array => new(array),
			JsonNull        => new(JsonArray.ReadOnly.Empty),
			_               => throw OperationRequiresArrayOrNull()
		};

		/// <summary>Optimized implementation used to enumerate the deserialized elements of a proxy array</summary>
		public readonly struct Enumerable : IEnumerable<TValue>
		{
			private readonly JsonArray Items;

			public Enumerable(JsonArray items)
			{
				this.Items = items;
			}

			IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this.Items);

			IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => new Enumerator(this.Items);

			public Enumerator GetEnumerator() => new(this.Items);

		}

		/// <summary>Optimized implementation used to enumerate the deserialized elements of a proxy array</summary>
		public struct Enumerator : IEnumerator<TValue>
		{
			private JsonArray Items;
			private int Index;
			private TValue? Item;

			public Enumerator(JsonArray items)
			{
				this.Items = items;
				this.Index = -1;
				this.Item = default!;
			}

			public bool MoveNext()
			{
				int index = this.Index + 1;
				if (index >= this.Items.Count)
				{
					this.Item = default!;
					return false;
				}

				this.Item = TProxy.Converter.Unpack(this.Items[index], null);
				this.Index = index;
				return true;
			}

			object? IEnumerator.Current => this.Item;

			public readonly TValue Current => this.Item!;

			public void Reset()
			{
				this.Index = -1;
				this.Item = default!;
			}

			public void Dispose()
			{
				this.Items = JsonArray.ReadOnly.Empty;
				this.Index = 0;
				this.Item = default!;
			}

		}

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_value.Json.JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_value.Json;

		public JsonValue ToJsonValue() => m_value.Json;

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException OperationRequiresArrayOrNull() => new("This operation requires a valid JSON Array");

		/// <inheritdoc />
		public IEnumerator<TProxy> GetEnumerator()
		{
			if (m_value.Json is not JsonArray array)
			{
				if (m_value.Json is JsonNull)
				{
					yield break;
				}
				throw OperationRequiresArrayOrNull();
			}

			for(int i = 0; i < array.Count; i++)
			{
				yield return TProxy.Create(m_value[i]);
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <inheritdoc />
		public void Add(TProxy value) => m_value.Add(value.ToJsonValue());

		public void Add(TValue value) => m_value.Add(Pack(value));

		/// <inheritdoc />
		public void Clear() => m_value.Clear();

		/// <inheritdoc />
		bool ICollection<TProxy>.Contains(TProxy item) => throw new NotSupportedException("This operation is too costly.");

		/// <inheritdoc />
		public void CopyTo(TProxy[] array, int arrayIndex)
		{
			Contract.NotNull(array);
			Contract.Positive(arrayIndex);

			if (m_value.Json is not JsonArray arr)
			{
				if (m_value.Json is JsonNull)
				{
					return;
				}
				throw OperationRequiresArrayOrNull();
			}
			Contract.DoesNotOverflow(array, arrayIndex, arr.Count);

			for(int i = 0; i < arr.Count; i++)
			{
				array[arrayIndex++] = TProxy.Create(m_value[i]);
			}
		}

		/// <inheritdoc />
		bool ICollection<TProxy>.Remove(TProxy item) => throw new NotSupportedException("This operation is too costly.");

		/// <inheritdoc />
		int IList<TProxy>.IndexOf(TProxy item) => throw new NotSupportedException("This operation is too costly.");

		/// <inheritdoc />
		public int Count => m_value.Count;

		/// <summary>Tests if the array is present.</summary>
		/// <returns><c>false</c> if the wrapped JSON value is null or missing; otherwise, <c>true</c>.</returns>
		public bool Exists() => m_value.Exists();

		/// <summary>Tests if the array is null or missing.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is null or missing; otherwise, <c>false</c>.</returns>
		/// <remarks>This can return <c>false</c> if the wrapped value is another type, like an object, string literal, etc...</remarks>
		public bool IsNullOrMissing() => m_value.IsNullOrMissing();

		/// <summary>Tests if the array is null, missing, or empty.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is null, missing or an empty array; otherwise, <c>false</c>.</returns>
		/// <remarks>This can return <c>false</c> if the wrapped value is another type, like an object, string literal, etc...</remarks>
		public bool IsNullOrEmpty() => m_value.Json switch { JsonArray arr => arr.Count != 0, JsonNull => true, _ => false };

		/// <summary>Tests if the wrapped value is a valid JSON Array.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is a non-null Array; otherwise, <c>false</c></returns>
		/// <remarks>This can be used to protect against malformed JSON document that would have a different type (object, string literal, ...).</remarks>
		public bool IsArray() => m_value.Json is JsonArray;

		/// <summary>Tests if the wrapped value is a valid JSON Array, or is null-or-missing.</summary>
		/// <returns><c>true</c> if the wrapped JSON value either null-or-missing, or an Array; otherwise, <c>false</c></returns>
		/// <remarks>This can be used to protect against malformed JSON document that would have a different type (object, string literal, ...).</remarks>
		public bool IsArrayOrMissing() => m_value.Json is (JsonArray or JsonNull);

		/// <inheritdoc />
		bool ICollection<TProxy>.IsReadOnly => false;

		/// <summary>Inserts an item to the array at the specified index.</summary>
		/// <param name="index">The zero-based index at which <paramref name="item" /> should be inserted.</param>
		/// <param name="item">The instance to insert into the array.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException"> <paramref name="index" /> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1" />.</exception>
		public void Insert(int index, TProxy item) => m_value.Insert(index, item.ToJsonValue());

		/// <summary>Inserts an item to the array at the specified index.</summary>
		/// <param name="index">The zero-based index at which <paramref name="item" /> should be inserted.</param>
		/// <param name="item">The instance to insert into the array.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException"> <paramref name="index" /> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1" />.</exception>
		public void Insert(int index, TValue item) => m_value.Insert(index, Pack(item));

		/// <summary>Inserts an item to the array at the specified index.</summary>
		/// <param name="index">The zero-based index at which <paramref name="item" /> should be inserted.</param>
		/// <param name="item">The instance to insert into the array.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException"> <paramref name="index" /> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1" />.</exception>
		public void Insert(Index index, TProxy item) => m_value.Insert(index, item.ToJsonValue());

		/// <summary>Inserts an item to the array at the specified index.</summary>
		/// <param name="index">The zero-based index at which <paramref name="item" /> should be inserted.</param>
		/// <param name="item">The instance to insert into the array.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException"> <paramref name="index" /> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1" />.</exception>
		public void Insert(Index index, TValue item) => m_value.Insert(index, Pack(item));

		/// <inheritdoc />
		public void RemoveAt(int index) => m_value.RemoveAt(index);

		public void RemoveAt(Index index) => m_value.RemoveAt(index);

		/// <inheritdoc />
		public TProxy this[int index]
		{
			get => TProxy.Create(m_value[index]);
			set => m_value.Set(index, value.ToJsonValue());
		}

		/// <inheritdoc />
		public override string ToString() => $"({typeof(TValue).GetFriendlyName()}[]) {m_value}";

	}

	public static class JsonWritableProxyArrayBuilder
	{

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonWritableProxyArray<TValue> Create<TValue>(ReadOnlySpan<TValue> values)
		{
			return new(MutableJsonValue.Untracked(values.Length == 0 ? JsonArray.ReadOnly.Empty : JsonArray.FromValues(values)));
		}

	}

}
