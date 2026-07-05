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
	using System.Globalization;
	using SnowBank.Runtime;

	/// <summary>Wraps a <see cref="JsonObject"/> into a typed read-only proxy that emulates a dictionary of elements of type <typeparamref name="TValue"/> with integer keys</summary>
	/// <typeparam name="TValue">Emulated element type</typeparam>
	/// <remarks>The keys in the JSON Object must be base-10 encoded integers (ex: "0", "123", "-456").</remarks>
	[PublicAPI]
	public readonly struct JsonReadOnlyProxyInt32Dictionary<TValue> : IReadOnlyDictionary<int, TValue>, IJsonSerializable, IJsonPackable
	{

		private readonly ObservableJsonValue m_value;
		private readonly IJsonConverter<TValue> m_converter;

		public JsonReadOnlyProxyInt32Dictionary(ObservableJsonValue value, IJsonConverter<TValue>? converter = null)
		{
			m_value = value;
			m_converter = converter ?? RuntimeJsonConverter<TValue>.Default;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException OperationRequiresObjectOrNull() => new("This operation requires a valid JSON Object");

		/// <summary>Tests if the object is present.</summary>
		/// <returns><c>false</c> if the wrapped JSON value is null, missing or empty; otherwise, <c>true</c>.</returns>
		/// <remarks>This can return <c>true</c> if the wrapped value is of another type, like an array, string literal, etc...</remarks>
		public bool Exists() => m_value.Exists();

		/// <summary>Tests if the object is null or missing.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is null or missing; otherwise, <c>false</c>.</returns>
		/// <remarks>This can return <c>false</c> if the wrapped value is another type, like an array, string literal, etc...</remarks>
		public bool IsNullOrMissing() => m_value.IsNullOrMissing();

		/// <summary>Tests if the object is null, missing, or empty.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is null, missing or an empty object; otherwise, <c>false</c>.</returns>
		/// <remarks>This can return <c>false</c> if the wrapped value is an empty object, or another type, like an array, string literal, etc...</remarks>
		public bool IsNullOrEmpty() => m_value.GetJsonUnsafe() is JsonArray ? m_value.Count != 0 : m_value.IsNullOrMissing();

		/// <summary>Tests if the wrapped value is a valid JSON Object.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is a non-null Object; otherwise, <c>false</c></returns>
		/// <remarks>This can be used to protect against malformed JSON document that would have a different type (array, string literal, ...).</remarks>
		public bool IsObject() => m_value.IsOfType(JsonType.Object);

		/// <summary>Tests if the wrapped value is a valid JSON Object, or is null-or-missing.</summary>
		/// <returns><c>true</c> if the wrapped JSON value either null-or-missing, or an Object; otherwise, <c>false</c></returns>
		/// <remarks>This can be used to protect against malformed JSON document that would have a different type (array, string literal, ...).</remarks>
		public bool IsObjectOrMissing() => m_value.IsOfTypeOrNull(JsonType.Object);

		private static int ParseKey(string key) => !string.IsNullOrWhiteSpace(key) ? int.Parse(key, NumberStyles.Integer, NumberFormatInfo.InvariantInfo) : throw new InvalidOperationException("Cannot parse empty key");

		private static string FormatKey(int key) => (uint) key < 300U ? key.ToString(default(IFormatProvider)) : key.ToString(NumberFormatInfo.InvariantInfo);

		/// <inheritdoc />
		public int Count => m_value.TryGetCount(out int count) ? count : m_value.IsNullOrMissing() ? 0 : throw OperationRequiresObjectOrNull();

		/// <inheritdoc />
		public TValue this[int key]
		{
			get
			{
				if (m_value.GetJsonUnsafe() is not JsonObject)
				{
					m_value.RecordSelfAccess(ObservableJsonAccess.Type);
				}
				else if (m_value.TryGetValue(FormatKey(key), m_converter, out var value))
				{
					return value;
				}
				return m_converter.Unpack(JsonNull.Missing, null);
			}
		}

		/// <inheritdoc />
		public IEnumerable<int> Keys
			=> m_value.IsObjectUnsafe(out var obj) ? obj.Keys.Select(ParseKey)
			 : m_value.GetJsonUnsafe().IsNullOrMissing() ? [ ]
			 : throw OperationRequiresObjectOrNull();

		/// <inheritdoc />
		public IEnumerable<TValue> Values => throw new NotImplementedException();

		/// <inheritdoc />
		public bool ContainsKey(int key)
		{
			// for small positive integers, ToString() already returns a cached singleton, but for others it will allocate!

			// perform the null-check manually so that at least we don't allocate if this is not an object
			if (m_value.GetJsonUnsafe() is not JsonObject)
			{
				m_value.RecordSelfAccess(ObservableJsonAccess.Type);
				return false;
			}

			return m_value.ContainsKey(FormatKey(key));
		}

		/// <inheritdoc />
		public bool TryGetValue(int key, [MaybeNullWhen(false)] out TValue value)
		{
			// for small positive integers, ToString() already returns a cached singleton, but for others it will allocate!

			// perform the null-check manually so that at least we don't allocate if this is not an object
			if (m_value.GetJsonUnsafe() is not JsonObject)
			{
				m_value.RecordSelfAccess(ObservableJsonAccess.Type);
				value = default;
				return false;
			}

			return m_value.TryGetValue(FormatKey(key), m_converter, out value);
		}

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_value.ToJsonValue().JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_value.ToJsonValue();

		public Dictionary<int, TValue> ToDictionary() => m_converter.UnpackDictionaryInt32(m_value.ToJsonValue())!;

		public JsonValue ToJsonValue() => m_value.ToJsonValue();

		/// <inheritdoc />
		public IEnumerator<KeyValuePair<int, TValue>> GetEnumerator()
		{
			if (!m_value.IsObjectUnsafe(out var obj))
			{
				if (m_value.GetJsonUnsafe().IsNullOrMissing())
				{
					yield break;
				}
				throw OperationRequiresObjectOrNull();
			}
			foreach (var kv in obj)
			{
				yield return new(ParseKey(kv.Key), m_converter.Unpack(kv.Value, null));
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <inheritdoc />
		public override string ToString() => $"Dictionary<int, {typeof(TValue).GetFriendlyName()}>";

	}

	/// <summary>Wraps a <see cref="JsonObject"/> into a typed read-only proxy that emulates a dictionary of elements of type <typeparamref name="TValue"/> with integer keys</summary>
	/// <typeparam name="TValue">Emulated element type</typeparam>
	/// <typeparam name="TProxy">Corresponding <see cref="IJsonReadOnlyProxy{TValue}"/> for type <typeparamref name="TValue"/>, usually source-generated</typeparam>
	/// <remarks>The keys in the JSON Object must be base-10 encoded integers (ex: "0", "123", "-456").</remarks>
	[PublicAPI]
	public readonly struct JsonReadOnlyProxyInt32Dictionary<TValue, TProxy> : IReadOnlyDictionary<int, TProxy>, IJsonSerializable, IJsonPackable
		where TProxy : IJsonReadOnlyProxy<TValue, TProxy>
	{

		private readonly ObservableJsonValue m_value;

		public JsonReadOnlyProxyInt32Dictionary(ObservableJsonValue value)
		{
			m_value = value;
		}

		/// <inheritdoc />
		public int Count => m_value.TryGetCount(out int count) ? count : m_value.IsNullOrMissing() ? 0 : throw OperationRequiresObjectOrNull();

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
		public bool IsNullOrEmpty() => m_value.GetJsonUnsafe() is JsonArray ? m_value.Count != 0 : m_value.IsNullOrMissing();

		/// <summary>Tests if the wrapped value is a valid JSON Object.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is a non-null Object; otherwise, <c>false</c></returns>
		/// <remarks>This can be used to protect against malformed JSON document that would have a different type (array, string literal, ...).</remarks>
		public bool IsObject() => m_value.IsOfType(JsonType.Object);

		/// <summary>Tests if the wrapped value is a valid JSON Object, or is null-or-missing.</summary>
		/// <returns><c>true</c> if the wrapped JSON value either null-or-missing, or an Object; otherwise, <c>false</c></returns>
		/// <remarks>This can be used to protect against malformed JSON document that would have a different type (array, string literal, ...).</remarks>
		public bool IsObjectOrMissing() => m_value.IsOfTypeOrNull(JsonType.Object);

		private static int ParseKey(string key) => !string.IsNullOrWhiteSpace(key) ? int.Parse(key, NumberStyles.Integer, NumberFormatInfo.InvariantInfo) : throw new InvalidOperationException("Cannot parse empty key");

		private static string FormatKey(int key) => (uint) key < 300U ? key.ToString(default(IFormatProvider)) : key.ToString(NumberFormatInfo.InvariantInfo);

		/// <inheritdoc />
		public TProxy this[int key]
		{
			get
			{
				if (m_value.GetJsonUnsafe() is not JsonObject)
				{
					m_value.RecordSelfAccess(ObservableJsonAccess.Type);
					return TProxy.Create(JsonNull.Missing);
				}
				return TProxy.Create(m_value[FormatKey(key)]);
			}
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException OperationRequiresObjectOrNull() => new("This operation requires a valid JSON Object");

		/// <inheritdoc />
		public IEnumerable<int> Keys
			=> m_value.IsObjectUnsafe(out var obj) ? obj.Keys.Select(ParseKey)
			 : m_value.GetJsonUnsafe().IsNullOrMissing() ? [ ]
			 : throw OperationRequiresObjectOrNull();

		/// <inheritdoc />
		public IEnumerable<TProxy> Values => throw new NotImplementedException();

		/// <inheritdoc />
		public bool ContainsKey(int key)
		{
			if (m_value.GetJsonUnsafe() is not JsonObject)
			{
				m_value.RecordSelfAccess(ObservableJsonAccess.Type);
				return false;
			}

			return m_value.ContainsKey(FormatKey(key));
		}

		/// <inheritdoc />
		public bool TryGetValue(int key, [MaybeNullWhen(false)] out TProxy value)
		{
			if (m_value.GetJsonUnsafe() is not JsonObject)
			{
				m_value.RecordSelfAccess(ObservableJsonAccess.Type);
			}
			else if (m_value.TryGetValue(FormatKey(key), out var json))
			{
				value = TProxy.Create(json);
				return true;
			}

			value = default;
			return false;
		}

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_value.ToJsonValue().JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_value.ToJsonValue();

		public Dictionary<string, TValue> ToDictionary() => TProxy.Converter.UnpackDictionary(m_value.ToJsonValue().AsObject());

		public JsonValue ToJsonValue() => m_value.ToJsonValue();

		/// <inheritdoc />
		public IEnumerator<KeyValuePair<int, TProxy>> GetEnumerator()
		{
			if (!m_value.IsObjectUnsafe(out var obj))
			{
				if (m_value.GetJsonUnsafe().IsNullOrMissing())
				{
					yield break;
				}
				throw OperationRequiresObjectOrNull();
			}
			foreach (var k in obj.Keys)
			{
				yield return new(ParseKey(k), TProxy.Create(m_value.Get(k)));
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <inheritdoc />
		public override string ToString() => $"Dictionary<int, {typeof(TValue).GetFriendlyName()}>";

	}

	/// <summary>Wraps a <see cref="JsonObject"/> into a typed read-only proxy that emulates a dictionary of elements of type <typeparamref name="TValue"/> with integer keys</summary>
	/// <typeparam name="TValue">Emulated element type</typeparam>
	/// <typeparam name="TProxy">Corresponding <see cref="IJsonReadOnlyProxy{TValue}"/> for type <typeparamref name="TValue"/>, usually source-generated</typeparam>
	/// <remarks>The keys in the JSON Object must be base-10 encoded integers (ex: "0", "123", "-456").</remarks>
	[PublicAPI]
	public readonly struct JsonReadOnlyProxyInt32DictionaryOfArray<TValue, TProxy> : IReadOnlyDictionary<int, JsonReadOnlyProxyArray<TValue, TProxy>>, IJsonSerializable, IJsonPackable
		where TProxy : IJsonReadOnlyProxy<TValue, TProxy>
	{

		private readonly ObservableJsonValue m_value;

		public JsonReadOnlyProxyInt32DictionaryOfArray(ObservableJsonValue value)
		{
			m_value = value;
		}

		/// <inheritdoc />
		public int Count => m_value.TryGetCount(out int count) ? count : m_value.IsNullOrMissing() ? 0 : throw OperationRequiresObjectOrNull();

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
		public bool IsNullOrEmpty() => m_value.GetJsonUnsafe() is JsonArray ? m_value.Count != 0 : m_value.IsNullOrMissing();

		/// <summary>Tests if the wrapped value is a valid JSON Object.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is a non-null Object; otherwise, <c>false</c></returns>
		/// <remarks>This can be used to protect against malformed JSON document that would have a different type (array, string literal, ...).</remarks>
		public bool IsObject() => m_value.IsOfType(JsonType.Object);

		/// <summary>Tests if the wrapped value is a valid JSON Object, or is null-or-missing.</summary>
		/// <returns><c>true</c> if the wrapped JSON value either null-or-missing, or an Object; otherwise, <c>false</c></returns>
		/// <remarks>This can be used to protect against malformed JSON document that would have a different type (array, string literal, ...).</remarks>
		public bool IsObjectOrMissing() => m_value.IsOfTypeOrNull(JsonType.Object);

		private static int ParseKey(string key) => !string.IsNullOrWhiteSpace(key) ? int.Parse(key, NumberStyles.Integer, NumberFormatInfo.InvariantInfo) : throw new InvalidOperationException("Cannot parse empty key");

		private static string FormatKey(int key) => (uint) key < 300U ? key.ToString(default(IFormatProvider)) : key.ToString(NumberFormatInfo.InvariantInfo);

		/// <inheritdoc />
		public JsonReadOnlyProxyArray<TValue, TProxy> this[int key] => new(m_value[FormatKey(key)]);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException OperationRequiresObjectOrNull() => new("This operation requires a valid JSON Object");

		/// <inheritdoc />
		public IEnumerable<int> Keys
			=> m_value.IsObjectUnsafe(out var obj) ? obj.Keys.Select(ParseKey)
			 : m_value.GetJsonUnsafe().IsNullOrMissing() ? [ ]
			 : throw OperationRequiresObjectOrNull();

		/// <inheritdoc />
		public IEnumerable<JsonReadOnlyProxyArray<TValue, TProxy>> Values => throw new NotImplementedException();

		/// <inheritdoc />
		public bool ContainsKey(int key)
		{
			if (m_value.GetJsonUnsafe() is not JsonObject)
			{
				m_value.RecordSelfAccess(ObservableJsonAccess.Type);
				return false;
			}

			return m_value.ContainsKey(FormatKey(key));
		}

		/// <inheritdoc />
		public bool TryGetValue(int key, out JsonReadOnlyProxyArray<TValue, TProxy> value)
		{
			if (m_value.GetJsonUnsafe() is not JsonObject)
			{
				m_value.RecordSelfAccess(ObservableJsonAccess.Type);
			}
			else if (m_value.TryGetValue(FormatKey(key), out var json))
			{
				value = new(json);
				return true;
			}

			value = default;
			return false;
		}

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_value.ToJsonValue().JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_value.ToJsonValue();

		public Dictionary<string, TValue> ToDictionary() => TProxy.Converter.UnpackDictionary(m_value.ToJsonValue().AsObject());

		public JsonValue ToJsonValue() => m_value.ToJsonValue();

		/// <inheritdoc />
		public IEnumerator<KeyValuePair<int, JsonReadOnlyProxyArray<TValue, TProxy>>> GetEnumerator()
		{
			if (!m_value.IsObjectUnsafe(out var obj))
			{
				if (m_value.GetJsonUnsafe().IsNullOrMissing())
				{
					yield break;
				}
				throw OperationRequiresObjectOrNull();
			}
			foreach (var k in obj.Keys)
			{
				yield return new(ParseKey(k), new(m_value.Get(k)));
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <inheritdoc />
		public override string ToString() => $"Dictionary<int, {typeof(TValue).GetFriendlyName()}[]>";

	}

}

#endif
