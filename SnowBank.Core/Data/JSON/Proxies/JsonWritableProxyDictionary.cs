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

#if NET7_0_OR_GREATER // needs runtime support for static abstract members in interfaces

namespace SnowBank.Data.Json
{
	using System.Collections;
	using SnowBank.Runtime;

	/// <summary>Wraps a <see cref="JsonObject"/> into a typed mutable proxy that emulates a dictionary of elements of type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated element type</typeparam>
	[PublicAPI]
	[DebuggerDisplay("{ToString(),nq}, Count={Count}")]
	public readonly struct JsonWritableProxyDictionary<TValue> : IDictionary<string, TValue>, IJsonProxyNode, IJsonSerializable, IJsonPackable
	{

		private readonly MutableJsonValue m_value;

		private readonly IJsonConverter<TValue> m_converter;
	
		public JsonWritableProxyDictionary(MutableJsonValue value, IJsonConverter<TValue>? converter = null, IJsonProxyNode? parent = null, JsonPathSegment segment = default)
		{
			m_value = value;
			m_converter = converter ?? CrystalJsonTypeResolver.GetConverterFor<TValue>();
		}

		/// <inheritdoc />
		JsonType IJsonProxyNode.Type => JsonType.Object;

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

		/// <inheritdoc />
		void ICollection<KeyValuePair<string, TValue>>.Add(KeyValuePair<string, TValue> item) => Add(item.Key, item.Value);

		/// <inheritdoc />
		public void Clear() => m_value.Clear();

		/// <inheritdoc />
		bool ICollection<KeyValuePair<string, TValue>>.Contains(KeyValuePair<string, TValue> item) => throw new NotSupportedException("This operation is too costly.");


		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException OperationRequiresObjectOrNull() => new("This operation requires a valid JSON Object");

		/// <inheritdoc />
		public void CopyTo(KeyValuePair<string, TValue>[] array, int arrayIndex)
		{
			Contract.NotNull(array);
			Contract.Positive(arrayIndex);
			Contract.DoesNotOverflow(array, arrayIndex, m_value.Count);

			if (m_value.Json is not JsonObject obj)
			{
				if (m_value.Json.IsNullOrMissing())
				{
					return;
				}
				throw OperationRequiresObjectOrNull();
			}
			foreach (var kv in obj)
			{
				array[arrayIndex++] = new(kv.Key, m_converter.Unpack(kv.Value, null));
			}
		}

		/// <inheritdoc />
		public bool Remove(KeyValuePair<string, TValue> item) => throw new NotSupportedException("This operation is too costly.");

		public int Count => m_value.Count;

		/// <summary>Tests if the object is present.</summary>
		/// <returns><c>false</c> if the wrapped JSON value is null or empty; otherwise, <c>true</c>.</returns>
		public bool Exists() => m_value.Exists();

		/// <summary>Tests if the object is null or missing.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is null or missing; otherwise, <c>false</c>.</returns>
		/// <remarks>This can return <c>false</c> if the wrapped value is another type, like an array, string literal, etc...</remarks>
		public bool IsNullOrMissing() => m_value.IsNullOrMissing();

		/// <summary>Tests if the object is null, missing, or empty.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is null, missing or an empty object; otherwise, <c>false</c>.</returns>
		/// <remarks>This can return <c>false</c> if the wrapped value is an empty object, or another type, like an array, string literal, etc...</remarks>
		public bool IsNullOrEmpty() => m_value.Json switch { JsonArray arr =>  arr.Count != 0, JsonNull => true, _ => false };

		/// <summary>Tests if the wrapped value is a valid JSON Object.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is a non-null Object; otherwise, <c>false</c></returns>
		/// <remarks>This can be used to protect against malformed JSON document that would have a different type (array, string literal, ...).</remarks>
		public bool IsObject() => m_value.Json is JsonObject;

		/// <summary>Tests if the wrapped value is a valid JSON Object, or is null-or-missing.</summary>
		/// <returns><c>true</c> if the wrapped JSON value either null-or-missing, or an Object; otherwise, <c>false</c></returns>
		/// <remarks>This can be used to protect against malformed JSON document that would have a different type (array, string literal, ...).</remarks>
		public bool IsObjectOrMissing() => m_value.Json is (JsonObject or JsonNull);
		/// <inheritdoc />
		bool ICollection<KeyValuePair<string, TValue>>.IsReadOnly => false;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private JsonValue Pack(TValue value) => m_converter.Pack(value, CrystalJsonSettings.JsonReadOnly);

		/// <inheritdoc />
		public void Add(string key, TValue value) => m_value.Add(key, Pack(value));

		public void Add(ReadOnlyMemory<char> key, TValue value) => m_value.Add(key, Pack(value));

		public void Set(string key, TValue value) => m_value.Set(key, Pack(value));

		public void Set(ReadOnlyMemory<char> key, TValue value) => m_value.Set(key, Pack(value));

		/// <inheritdoc />
		public bool ContainsKey(string key) => m_value.ContainsKey(key);

		public bool ContainsKey(ReadOnlyMemory<char> key) => m_value.ContainsKey(key);

		public bool ContainsKey(ReadOnlySpan<char> key) => m_value.ContainsKey(key);

		/// <inheritdoc />
		public bool Remove(string key) => m_value.Remove(key);

		public bool Remove(ReadOnlyMemory<char> key) => m_value.Remove(key);

		/// <inheritdoc />
		public bool TryGetValue(string key, [MaybeNullWhen(false)] out TValue value)
		{
			if (!m_value.Json.TryGetValue(key, out var json))
			{
				value = default;
				return false;
			}

			value = m_converter.Unpack(json, null);
			return true;
		}

		public bool TryGetValue(ReadOnlyMemory<char> key, [MaybeNullWhen(false)] out TValue value)
		{
			if (!m_value.Json.TryGetValue(key, out var json))
			{
				value = default;
				return false;
			}

			value = m_converter.Unpack(json, null);
			return true;
		}

		public bool TryGetValue(ReadOnlySpan<char> key, [MaybeNullWhen(false)] out TValue value)
		{
			if (!m_value.Json.TryGetValue(key, out var json))
			{
				value = default;
				return false;
			}

			value = m_converter.Unpack(json, null);
			return true;
		}

		/// <inheritdoc />
		public TValue this[string key]
		{
			get => m_converter.Unpack(m_value.Json[key], null);
			set => m_value.Set(key, m_converter.Pack(value, CrystalJsonSettings.JsonReadOnly));
		}

		public TValue this[ReadOnlyMemory<char> key]
		{
			get => m_converter.Unpack(m_value.Json[key], null);
			set => m_value.Set(key, m_converter.Pack(value, CrystalJsonSettings.JsonReadOnly));
		}

		public TValue this[ReadOnlySpan<char> key]
		{
			get => m_converter.Unpack(m_value.Json[key], null);
		}

		/// <inheritdoc />
		public ICollection<string> Keys => m_value.Json switch
		{
			JsonObject obj => obj.Keys,
			JsonNull => [ ],
			_ => throw OperationRequiresObjectOrNull(),
		};

		/// <inheritdoc />
		public ICollection<TValue> Values => throw new NotImplementedException();

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_value.Json.JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_value.Json;

		public Dictionary<string, TValue> ToDictionary(IEqualityComparer<string>? keyComparer = null) => m_value.Json switch
		{
			JsonObject obj => m_converter.UnpackDictionary(obj, keyComparer),
			JsonNull => [ ],
			_ => throw OperationRequiresObjectOrNull(),
		};

		public JsonValue ToJsonValue() => m_value.Json;

		/// <inheritdoc />
		public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator()
		{
			if (m_value.Json is not JsonObject obj)
			{
				if (m_value.Json is JsonNull)
				{
					yield break;
				}

				throw OperationRequiresObjectOrNull();
			}

			foreach (var kv in obj)
			{
				yield return new(kv.Key, m_converter.Unpack(kv.Value, null));
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <inheritdoc />
		public override string ToString() => $"(Dictionary<string, {typeof(TValue).GetFriendlyName()}>) {m_value}";
	}

	/// <summary>Wraps a <see cref="JsonObject"/> into a typed mutable proxy that emulates a dictionary of elements of type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated element type</typeparam>
	/// <typeparam name="TProxy">Corresponding <see cref="IJsonWritableProxy{TValue}"/> for type <typeparamref name="TValue"/>, usually source-generated</typeparam>
	[PublicAPI]
	public readonly struct JsonWritableProxyDictionary<TValue, TProxy> : IDictionary<string, TProxy>, IJsonProxyNode, IJsonSerializable, IJsonPackable
		where TProxy : IJsonWritableProxy<TValue, TProxy>
	{

		private readonly MutableJsonValue m_value;

		public JsonWritableProxyDictionary(MutableJsonValue value, IJsonProxyNode? parent = null, JsonPathSegment segment = default)
		{
			m_value = value;
		}

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

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException OperationRequiresObjectOrNull() => new("This operation requires a valid JSON Object");

		/// <inheritdoc />
		void ICollection<KeyValuePair<string, TProxy>>.Add(KeyValuePair<string, TProxy> item) => Add(item.Key, item.Value);

		/// <inheritdoc />
		public void Clear() => m_value.Clear();

		/// <inheritdoc />
		bool ICollection<KeyValuePair<string, TProxy>>.Contains(KeyValuePair<string, TProxy> item) => throw new NotSupportedException("This operation is too costly.");

		/// <inheritdoc />
		public void CopyTo(KeyValuePair<string, TProxy>[] array, int arrayIndex)
		{
			Contract.NotNull(array);
			Contract.Positive(arrayIndex);

			if (m_value.Json is not JsonObject obj)
			{
				if (m_value.Json is JsonNull) return;
				throw OperationRequiresObjectOrNull();
			}

			Contract.DoesNotOverflow(array, arrayIndex, obj.Count);

			foreach (var k in obj.Keys)
			{
				array[arrayIndex++] = new(k, TProxy.Create(m_value[k]));
			}
		}

		/// <inheritdoc />
		bool ICollection<KeyValuePair<string, TProxy>>.Remove(KeyValuePair<string, TProxy> item) => throw new NotSupportedException("This operation is too costly.");

		public int Count => m_value.Count;

		/// <inheritdoc />
		bool ICollection<KeyValuePair<string, TProxy>>.IsReadOnly => false;

		/// <summary>Tests if the object is present.</summary>
		/// <returns><c>false</c> if the wrapped JSON value is null or empty; otherwise, <c>true</c>.</returns>
		public bool Exists() => m_value.Exists();

		/// <summary>Tests if the object is null or missing.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is null or missing; otherwise, <c>false</c>.</returns>
		/// <remarks>This can return <c>false</c> if the wrapped value is another type, like an array, string literal, etc...</remarks>
		public bool IsNullOrMissing() => m_value.IsNullOrMissing();

		/// <summary>Tests if the object is null, missing, or empty.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is null, missing or an empty object; otherwise, <c>false</c>.</returns>
		/// <remarks>This can return <c>false</c> if the wrapped value is an empty object, or another type, like an array, string literal, etc...</remarks>
		public bool IsNullOrEmpty() => m_value.Json switch { JsonArray arr =>  arr.Count != 0, JsonNull => true, _ => false };

		/// <summary>Tests if the wrapped value is a valid JSON Object.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is a non-null Object; otherwise, <c>false</c></returns>
		/// <remarks>This can be used to protect against malformed JSON document that would have a different type (array, string literal, ...).</remarks>
		public bool IsObject() => m_value.Json is JsonObject;

		/// <summary>Tests if the wrapped value is a valid JSON Object, or is null-or-missing.</summary>
		/// <returns><c>true</c> if the wrapped JSON value either null-or-missing, or an Object; otherwise, <c>false</c></returns>
		/// <remarks>This can be used to protect against malformed JSON document that would have a different type (array, string literal, ...).</remarks>
		public bool IsObjectOrMissing() => m_value.Json is (JsonObject or JsonNull);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static JsonValue Pack(TValue value) => TProxy.Converter.Pack(value, CrystalJsonSettings.JsonReadOnly);

		/// <inheritdoc />
		public void Add(string key, TProxy value) => m_value.Add(key, value.ToJsonValue());

		public void Add(ReadOnlyMemory<char> key, TProxy value) => m_value.Add(key, value.ToJsonValue());

		public void Add(string key, TValue value) => m_value.Add(key, Pack(value));

		public void Add(ReadOnlyMemory<char> key, TValue value) => m_value.Add(key, Pack(value));

		public void Set(string key, TProxy value) => m_value.Set(key, value.ToJsonValue());

		public void Set(ReadOnlyMemory<char> key, TProxy value) => m_value.Set(key, value.ToJsonValue());

		public void Set(string key, TValue value) => m_value.Set(key, Pack(value));

		public void Set(ReadOnlyMemory<char> key, TValue value) => m_value.Set(key, Pack(value));

		/// <inheritdoc />
		public bool ContainsKey(string key) => m_value.ContainsKey(key);

		public bool ContainsKey(ReadOnlyMemory<char> key) => m_value.ContainsKey(key);

		/// <inheritdoc />
		public bool Remove(string key) => m_value.Remove(key);

		public bool Remove(ReadOnlyMemory<char> key) => m_value.Remove(key);

		/// <inheritdoc />
		public bool TryGetValue(string key, [MaybeNullWhen(false)] out TProxy value)
		{
			if (m_value.TryGetValue(key, out var json))
			{
				value = TProxy.Create(json); //BUGBUG
				return true;
			}

			value = default;
			return false;
		}

		public bool TryGetValue(ReadOnlyMemory<char> key, [MaybeNullWhen(false)] out TProxy value)
		{
			if (m_value.TryGetValue(key, out var json))
			{
				value = TProxy.Create(json); //BUGBUG
				return true;
			}

			value = default;
			return false;
		}

		/// <inheritdoc />
		public TProxy this[string key]
		{
			get => TProxy.Create(m_value[key]);
			set => m_value.Set(key, value.ToJsonValue());
		}

		public TProxy this[ReadOnlyMemory<char> key]
		{
			get => TProxy.Create(m_value[key]);
			set => m_value.Set(key, value.ToJsonValue());
		}

		/// <inheritdoc />
		public ICollection<string> Keys => m_value.Json switch
		{
			JsonObject obj => obj.Keys,
			JsonNull => [ ],
			_ => throw OperationRequiresObjectOrNull(),
		};

		/// <inheritdoc />
		public ICollection<TProxy> Values => throw new NotImplementedException();

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_value.Json.JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_value.Json;

		public Dictionary<string, TValue> ToDictionary() => m_value.Json switch
		{
			JsonObject obj => TProxy.Converter.UnpackDictionary(obj),
			JsonNull => [ ],
			_ => throw OperationRequiresObjectOrNull(),
		};

		public JsonValue ToJsonValue() => m_value.Json;

		/// <inheritdoc />
		public IEnumerator<KeyValuePair<string, TProxy>> GetEnumerator()
		{
			if (m_value.Json is not JsonObject obj)
			{
				if (m_value.Json is JsonNull)
				{
					yield break;
				}
				throw OperationRequiresObjectOrNull();
			}
			foreach (var k in obj.Keys)
			{
				yield return new(k, TProxy.Create(m_value[k]));
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <inheritdoc />
		public override string ToString() => $"(Dictionary<string, {typeof(TValue).GetFriendlyName()}>) {m_value}";

		/// <inheritdoc />
		public JsonType Type { get; }

	}

}

#endif
