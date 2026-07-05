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

	/// <summary>Wraps a <see cref="JsonObject"/> into a typed read-only proxy that emulates a dictionary of elements of type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated element type</typeparam>
	[PublicAPI]
	public readonly struct JsonReadOnlyProxyDictionary<TValue> : IReadOnlyDictionary<string, TValue>, IJsonSerializable, IJsonPackable
	{

		private readonly ObservableJsonValue m_value;
		private readonly IJsonConverter<TValue> m_converter;

		public JsonReadOnlyProxyDictionary(ObservableJsonValue value, IJsonConverter<TValue>? converter = null)
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

		/// <inheritdoc />
		public int Count => m_value.TryGetCount(out int count) ? count : m_value.IsNullOrMissing() ? 0 : throw OperationRequiresObjectOrNull();

		/// <inheritdoc />
		public TValue this[string key] => m_value.TryGetValue(key, m_converter, out var value) ? value : m_converter.Unpack(JsonNull.Missing, null);

		public TValue this[ReadOnlyMemory<char> key] => m_value.TryGetValue(key, m_converter, out var value) ? value : m_converter.Unpack(JsonNull.Missing, null);

		/// <inheritdoc />
		public IEnumerable<string> Keys
			=> m_value.IsObjectUnsafe(out var obj) ? obj.Keys
			 : m_value.GetJsonUnsafe().IsNullOrMissing() ? Array.Empty<string>()
			 : throw OperationRequiresObjectOrNull();

		/// <inheritdoc />
		public IEnumerable<TValue> Values => throw new NotImplementedException();

		/// <inheritdoc />
		public bool ContainsKey(string key) => m_value.ContainsKey(key);

		/// <inheritdoc />
		public bool TryGetValue(string key, [MaybeNullWhen(false)] out TValue value)
		{
			return m_value.TryGetValue(key, m_converter, out value);
		}

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_value.ToJsonValue().JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_value.ToJsonValue();

		public Dictionary<string, TValue> ToDictionary() => m_converter.UnpackDictionary(m_value.ToJsonValue())!;

		public JsonValue ToJsonValue() => m_value.ToJsonValue();

		/// <inheritdoc />
		public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator()
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
				yield return new(kv.Key, m_converter.Unpack(kv.Value, null));
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <inheritdoc />
		public override string ToString() => $"Dictionary<string, {typeof(TValue).GetFriendlyName()}>";

	}

	/// <summary>Wraps a <see cref="JsonObject"/> into a typed read-only proxy that emulates a dictionary of elements of type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated element type</typeparam>
	/// <typeparam name="TProxy">Corresponding <see cref="IJsonReadOnlyProxy{TValue}"/> for type <typeparamref name="TValue"/>, usually source-generated</typeparam>
	[PublicAPI]
	public readonly struct JsonReadOnlyProxyDictionary<TValue, TProxy> : IReadOnlyDictionary<string, TProxy>, IJsonSerializable, IJsonPackable
		where TProxy : IJsonReadOnlyProxy<TValue, TProxy>
	{

		private readonly ObservableJsonValue m_value;

		public JsonReadOnlyProxyDictionary(ObservableJsonValue value)
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

		/// <inheritdoc />
		public TProxy this[string key] => TProxy.Create(m_value[key]);

		public TProxy this[ReadOnlyMemory<char> key] => TProxy.Create(m_value[key]);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException OperationRequiresObjectOrNull() => new("This operation requires a valid JSON Object");

		/// <inheritdoc />
		public IEnumerable<string> Keys
			=> m_value.IsObjectUnsafe(out var obj) ? obj.Keys
			 : m_value.GetJsonUnsafe().IsNullOrMissing() ? Array.Empty<string>()
			 : throw OperationRequiresObjectOrNull();

		/// <inheritdoc />
		public IEnumerable<TProxy> Values => throw new NotImplementedException();

		/// <inheritdoc />
		public bool ContainsKey(string key) => m_value.ContainsKey(key);

		/// <inheritdoc />
		public bool TryGetValue(string key, [MaybeNullWhen(false)] out TProxy value)
		{
			if (m_value.TryGetValue(key, out var json))
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
		public IEnumerator<KeyValuePair<string, TProxy>> GetEnumerator()
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
				yield return new(k, TProxy.Create(m_value.Get(k)));
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <inheritdoc />
		public override string ToString() => $"Dictionary<string, {typeof(TValue).GetFriendlyName()}>";

	}

}

#endif
