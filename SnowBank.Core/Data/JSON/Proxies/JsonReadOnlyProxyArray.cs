#region Copyright (c) 2023-2026 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

#if NET7_0_OR_GREATER // needs runtime support for static abstract members in interfaces

namespace SnowBank.Data.Json
{
	using System.Collections;

	/// <summary>Wraps a <see cref="JsonArray"/> into a typed read-only proxy that emulates an array of elements of type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated element type</typeparam>
	[PublicAPI]
	public readonly struct JsonReadOnlyProxyArray<TValue> : IReadOnlyList<TValue?>, IJsonSerializable, IJsonPackable, IEquatable<ObservableJsonValue>, IEquatable<JsonValue>, IEquatable<JsonReadOnlyProxyArray<TValue>>
	{

		private readonly ObservableJsonValue m_value;
		private readonly IJsonConverter<TValue> m_converter;

		public JsonReadOnlyProxyArray(ObservableJsonValue value, IJsonConverter<TValue>? converter = null)
		{
			Contract.Debug.Requires(value != null);
			m_value = value;
			m_converter = converter ?? RuntimeJsonConverter<TValue>.Default;
		}

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_value.ToJsonValue().JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_value.ToJsonValue();

		public TValue[] ToArray() => m_converter.UnpackArray(m_value.ToJsonValue().AsArrayOrEmpty());

		public List<TValue> ToList() => m_converter.UnpackList(m_value.ToJsonValue().AsArrayOrEmpty());

		public JsonValue ToJsonValue() => m_value.ToJsonValue();

		/// <inheritdoc />
		public IEnumerator<TValue?> GetEnumerator()
		{
			// we cannot know how the result will be used, so mark this a full "Value" read.

			var value = m_value.ToJsonValue();
			if (value is not JsonArray array)
			{
				if (value is JsonNull)
				{
					yield break;
				}
				throw new InvalidOperationException("Cannot iterate a non-array");
			}

			foreach(var item in array)
			{
				yield return m_converter.Unpack(item, null);
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

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
		public bool IsNullOrEmpty() => m_value.GetJsonUnsafe() is JsonArray ? m_value.Count != 0 : m_value.IsNullOrMissing();

		/// <summary>Tests if the wrapped value is a valid JSON Array.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is a non-null Array; otherwise, <c>false</c></returns>
		/// <remarks>This can be used to protect against malformed JSON document that would have a different type (object, string literal, ...).</remarks>
		public bool IsArray() => m_value.IsOfType(JsonType.Array);

		/// <summary>Tests if the wrapped value is a valid JSON Array, or is null-or-missing.</summary>
		/// <returns><c>true</c> if the wrapped JSON value either null-or-missing, or an Array; otherwise, <c>false</c></returns>
		/// <remarks>This can be used to protect against malformed JSON document that would have a different type (object, string literal, ...).</remarks>
		public bool IsArrayOrMissing() => m_value.IsOfTypeOrNull(JsonType.Array);

		/// <inheritdoc />
		public TValue? this[int index] => m_value.TryGetValue(index, m_converter, out var value) ? value : default(TValue);

		public TValue? this[Index index] => m_value.TryGetValue(index, m_converter, out var value) ? value : default(TValue);

		public TValue? Get(int index) => m_value.TryGetValue(index, m_converter, out var value) ? value : default(TValue);

		public TValue? Get(Index index) => m_value.TryGetValue(index, m_converter, out var value) ? value : default(TValue);

		public bool TryGet(int index, out TValue? value) => m_value.TryGetValue(index, m_converter, out value);

		public bool TryGet(Index index, out TValue? value) => m_value.TryGetValue(index, m_converter, out value);

		/// <inheritdoc />
		public override string ToString() => m_value.ToString();

		/// <inheritdoc />
		public override bool Equals(object? obj) => obj switch
		{
			ObservableJsonValue value => m_value.Equals(value),
			JsonValue value => m_value.Equals(value),
			null => m_value.IsNullOrMissing(),
			_ => false,
		};

		/// <inheritdoc />
		public override int GetHashCode() => m_value.GetHashCode();

		public bool Equals(ObservableJsonValue? other) => m_value.Equals(other);

		public bool Equals(JsonValue? other) => m_value.Equals(other);

		public bool Equals(JsonReadOnlyProxyArray<TValue> other) => m_value.Equals(other.m_value);

	}

	/// <summary>Wraps a <see cref="JsonArray"/> into a typed read-only proxy that emulates an array of elements of type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated element type</typeparam>
	/// <typeparam name="TProxy">Corresponding <see cref="IJsonReadOnlyProxy{TValue}"/> for type <typeparamref name="TValue"/>, usually source-generated</typeparam>
	[PublicAPI]
	public readonly struct JsonReadOnlyProxyArray<TValue, TProxy> : IReadOnlyList<TProxy>, IJsonSerializable, IJsonPackable
		where TProxy : IJsonReadOnlyProxy<TValue, TProxy>
	{

		private readonly ObservableJsonValue m_value;

		public JsonReadOnlyProxyArray(ObservableJsonValue value)
		{
			m_value = value;
		}

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_value.ToJsonValue().JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_value.ToJsonValue();

		public JsonValue ToJsonValue() => m_value.ToJsonValue();

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException OperationRequiresArrayOrNull() => new("This operation requires a valid JSON Array");

		public IEnumerator<TProxy> GetEnumerator()
		{
			// the result would change if there are additional items in the array
			if (!m_value.TryGetCount(out var count))
			{
				if (m_value.GetJsonUnsafe().IsNullOrMissing())
				{
					yield break;
				}
				throw OperationRequiresArrayOrNull();
			}

			for(int i = 0; i < count; i++)
			{
				yield return TProxy.Create(m_value[i]);
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <inheritdoc />
		public int Count => m_value.Count;

		/// <summary>Tests if the array is present.</summary>
		/// <returns><c>false</c> if the wrapped JSON value is null, missing or empty; otherwise, <c>true</c>.</returns>
		/// <remarks>This can return <c>true</c> if the wrapped value is another type, like an object, string literal, etc...</remarks>
		public bool Exists() => m_value.Exists();

		/// <summary>Tests if the array is null or missing.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is null or missing; otherwise, <c>false</c>.</returns>
		/// <remarks>This can return <c>false</c> if the wrapped value is another type, like an object, string literal, etc...</remarks>
		public bool IsNullOrMissing() => m_value.IsNullOrMissing();

		/// <summary>Tests if the array is null, missing, or empty.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is null, missing or an empty array; otherwise, <c>false</c>.</returns>
		/// <remarks>This can return <c>false</c> if the wrapped value is another type, like an object, string literal, etc...</remarks>
		public bool IsNullOrEmpty() => m_value.GetJsonUnsafe() is JsonArray ? m_value.Count != 0 : m_value.IsNullOrMissing();

		/// <summary>Tests if the wrapped value is a valid JSON Array.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is a non-null Array; otherwise, <c>false</c></returns>
		/// <remarks>This can be used to protect against malformed JSON document that would have a different type (object, string literal, ...).</remarks>
		public bool IsArray() => m_value.IsOfType(JsonType.Array);

		/// <summary>Tests if the wrapped value is a valid JSON Array, or is null-or-missing.</summary>
		/// <returns><c>true</c> if the wrapped JSON value either null-or-missing, or an Array; otherwise, <c>false</c></returns>
		/// <remarks>This can be used to protect against malformed JSON document that would have a different type (object, string literal, ...).</remarks>
		public bool IsArrayOrMissing() => m_value.IsOfTypeOrNull(JsonType.Array);

		/// <inheritdoc />
		public TProxy this[int index] => TProxy.Create(m_value.Get(index));

		public TProxy this[Index index] => TProxy.Create(m_value.Get(index));

		public TProxy Get(int index) => TProxy.Create(m_value.Get(index));

		public TProxy Get(Index index) => TProxy.Create(m_value.Get(index));

		public bool TryGet(int index, [MaybeNullWhen(false)] out TProxy value)
		{
			if (!m_value.TryGetValue(index, out var item))
			{
				value = default;
				return false;
			}

			value = TProxy.Create(item);
			return true;
		}

		public bool TryGet(Index index, [MaybeNullWhen(false)] out TProxy value)
		{
			if (!m_value.TryGetValue(index, out var item))
			{
				value = default;
				return false;
			}

			value = TProxy.Create(item);
			return true;
		}

		/// <inheritdoc />
		public override string ToString() => m_value.ToString();

	}

}

#endif
