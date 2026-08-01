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

namespace SnowBank.Data.Json
{
	using System.Buffers;
	using System.Collections.Immutable;
	using System.ComponentModel;
	using System.Runtime.InteropServices;
#if NETSTANDARD2_0
	using CollectionsMarshal = SnowBank.Compat.CollectionsMarshalCompat; // shim: CollectionsMarshal does not exist on netstandard2.0
#endif
	using SnowBank.Runtime;
	using SnowBank.Runtime.Converters;
	using SnowBank.Buffers;

	/// <summary>Helper methods for working with JSON converters</summary>
	[PublicAPI]
	public static class JsonSerializerExtensions
	{

		#region IJsonSerializer<T>...

		/// <summary>Serializes a value (of any type) into a string literal, using a customer serializer</summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="value">Instance to serialize (can be <b>null</b>)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns><c>`123`</c>, <c>`true`</c>, <c>`"ABC"`</c>, <c>`{ "foo":..., "bar": ... }`</c>, <c>`[ ... ]`</c>, ...</returns>
		/// <exception cref="JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		public static string ToJson<T>(this IJsonSerializer<T> serializer, T? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return CrystalJson.Serialize<T>(value, serializer, settings, resolver);
		}

		/// <summary>Serializes a value of type <typeparamref name="T"/> into a <see cref="Slice"/>, using a customer serializer</summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="value">Instance to serialize (can be <b>null</b>)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns><c>`123`</c>, <c>`true`</c>, <c>`"ABC"`</c>, <c>`{ "foo":..., "bar": ... }`</c>, <c>`[ ... ]`</c>, ...</returns>
		/// <exception cref="JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		public static Slice ToSlice<T>(this IJsonSerializer<T> serializer, T? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return CrystalJson.ToSlice<T>(value, serializer, settings, resolver);
		}

		/// <summary>Serializes a value of type <typeparamref name="T"/> into a <see cref="SliceOwner"/>, using a customer serializer, and the specified <see cref="ArrayPool{T}">pool</see></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="value">Instance to serialize (can be <b>null</b>)</param>
		/// <param name="pool">Pool used to allocate the content of the slice.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns><c>`123`</c>, <c>`true`</c>, <c>`"ABC"`</c>, <c>`{ "foo":..., "bar": ... }`</c>, <c>`[ ... ]`</c>, ...</returns>
		/// <exception cref="JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		/// <remarks>
		/// <para>The <see cref="SliceOwner"/> returned <b>MUST</b> be disposed; otherwise, the rented buffer will not be returned to the <paramref name="pool"/>.</para>
		/// </remarks>
		public static SliceOwner ToSlice<T>(this IJsonSerializer<T> serializer, T? value, ArrayPool<byte> pool, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return CrystalJson.ToSlice<T>(value, serializer, pool, settings, resolver);
		}

		#endregion

		#region IJsonDeserializer<T>...

		/// <summary>Deserializes a JSON text literal into an instance of type <typeparamref name="T" /></summary>
		/// <param name="serializer">Deserializer instance to use</param>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver</param>
		/// <returns>Deserialized value</returns>
		/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="JsonBindingException">If the parsed JSON document cannot be bound to an instance of <typeparamref name="T"/>.</exception>
		public static T Deserialize<T>(this IJsonDeserializer<T> serializer, string jsonText, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			where T : notnull
		{
			return serializer.Unpack(CrystalJson.Parse(jsonText, settings), resolver);
		}

		/// <summary>Deserializes a JSON text literal into an instance of type <typeparamref name="T" /></summary>
		/// <param name="serializer">Deserializer instance to use</param>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver</param>
		/// <returns>Deserialized value</returns>
		/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="JsonBindingException">If the parsed JSON document cannot be bound to an instance of <typeparamref name="T"/>.</exception>
		public static T Deserialize<T>(this IJsonDeserializer<T> serializer, ReadOnlySpan<char> jsonText, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			where T : notnull
		{
			return serializer.Unpack(CrystalJson.Parse(jsonText, settings), resolver);
		}

		/// <summary>Deserializes a JSON text literal into an instance of type <typeparamref name="T" /></summary>
		/// <param name="serializer">Deserializer instance to use</param>
		/// <param name="jsonBytes">JSON text document to parse, encoded as UTF-8</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver</param>
		/// <returns>Deserialized value</returns>
		/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="JsonBindingException">If the parsed JSON document cannot be bound to an instance of <typeparamref name="T"/>.</exception>
		public static T Deserialize<T>(this IJsonDeserializer<T> serializer, Slice jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			where T : notnull
		{
			return serializer.Unpack(CrystalJson.Parse(jsonBytes, settings), resolver);
		}

		/// <summary>Deserializes a JSON text literal into an instance of type <typeparamref name="T" /></summary>
		/// <param name="serializer">Deserializer instance to use</param>
		/// <param name="jsonBytes">JSON text document to parse, encoded as UTF-8</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver</param>
		/// <returns>Deserialized value</returns>
		/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="JsonBindingException">If the parsed JSON document cannot be bound to an instance of <typeparamref name="T"/>.</exception>
		public static T Deserialize<T>(this IJsonDeserializer<T> serializer, ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			where T : notnull
		{
			return serializer.Unpack(CrystalJson.Parse(jsonBytes, settings), resolver);
		}

		/// <summary>Deserializes a JSON value into an array of <typeparamref name="T"/></summary>
		/// <param name="serializer">Deserializer instance to use</param>
		/// <param name="destination">Destination buffer, which must be large enough to fit the deserialized elements</param>
		/// <param name="written">Number of items written into the <paramref name="destination"/></param>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="resolver">Optional custom resolver</param>
		/// <returns><c>true</c> if the deserialization was successful; otherwise, <c>false</c></returns>
		public static bool TryUnpackArray<T>(this IJsonDeserializer<T> serializer, Span<T> destination, out int written, JsonValue value, ICrystalJsonTypeResolver? resolver = null)
		{
			if (value.IsNullOrMissing())
			{
				written = 0;
				return true;
			}

			var arr = value.AsArray();
			var input = arr.GetSpan();
			if (destination.Length < input.Length)
			{
				written = 0;
				return false;
			}

			try
			{
				for (int i = 0; i < input.Length; i++)
				{
					destination[i] = serializer.Unpack(input[i], resolver);
				}
			}
			catch (JsonBindingException)
			{
				destination[..input.Length].Clear();
				written = 0;
				return false;
			}

			written = input.Length;
			return true;
		}

		/// <summary>Deserializes a JSON value stored in an optional field, into an array of <typeparamref name="T"/></summary>
		/// <param name="serializer">Deserializer instance to use</param>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="defaultValue">Default value to return, if <paramref name="value"/> is null or missing.</param>
		/// <param name="resolver">Optional custom resolver</param>
		/// <param name="fieldName">Name of the field that holds this value in its parent objet (used when throwing exceptions)</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to an instance of <typeparamref name="T"/>.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static T[]? UnpackArray<T>(this IJsonDeserializer<T> serializer, JsonValue? value, T[]? defaultValue = null, ICrystalJsonTypeResolver? resolver = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => UnpackArray(serializer, arr, resolver),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a JSON value stored in an optional field, into an array of <typeparamref name="T"/></summary>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="defaultValue">Default value to return, if <paramref name="value"/> is null or missing.</param>
		/// <param name="resolver">Optional custom resolver</param>
		/// <param name="fieldName">Name of the field that holds this value in its parent objet (used when throwing exceptions)</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to an instance of <typeparamref name="T"/>.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static T?[]? UnpackArray<T>(JsonValue? value, T[]? defaultValue = null, ICrystalJsonTypeResolver? resolver = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToArray<T>(resolver: resolver),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a JSON value stored in a <b>required</b> field, into an array of <typeparamref name="T"/></summary>
		/// <param name="serializer">Deserializer instance to use</param>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="resolver">Optional custom resolver</param>
		/// <param name="parent">Parent JSON object that holds this field (used when throwing exceptions).</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to an instance of <typeparamref name="T"/>.</exception>
		[Pure]
		public static T[] UnpackRequiredArray<T>(this IJsonDeserializer<T> serializer, JsonValue? value, ICrystalJsonTypeResolver? resolver = null, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => UnpackArray(serializer, arr, resolver),
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a JSON value stored in a <b>required</b> field, into an array of <typeparamref name="T"/></summary>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="resolver">Optional custom resolver</param>
		/// <param name="parent">Parent JSON object that holds this field (used when throwing exceptions).</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to an instance of <typeparamref name="T"/>.</exception>
		[Pure]
		public static T[] UnpackRequiredArray<T>(JsonValue? value, ICrystalJsonTypeResolver? resolver = null, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => (T[]) arr.ToArray<T>(resolver: resolver)!,
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a JSON value stored in an optional field, into an array of <typeparamref name="T"/></summary>
		/// <param name="serializer">Deserializer instance to use</param>
		/// <param name="array">JSON array to unpack</param>
		/// <param name="resolver">Optional custom resolver</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to an instance of <typeparamref name="T"/>.</exception>
		[Pure]
		public static T[] UnpackArray<T>(this IJsonDeserializer<T> serializer, JsonArray array, ICrystalJsonTypeResolver? resolver = null)
		{
			var input = array.GetSpan();
			var result = new T[input.Length];

			for (int i = 0; i < input.Length; i++)
			{
				result[i] = serializer.Unpack(input[i], resolver);
			}

			return result;
		}

		/// <summary>Deserializes a JSON value stored in a <b>required</b> field, into an array of <see cref="string"/>.</summary>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="parent">Parent JSON object that holds this field (used when throwing exceptions).</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to a string.</exception>
		[Pure]
		public static string[] UnpackRequiredStringArray(JsonValue? value, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => (string[]) arr.ToStringArray()!,
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a JSON value stored in an optional field, into an array of <see cref="string"/>.</summary>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="defaultValue">Default value to return, if <paramref name="value"/> is null or missing.</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to a string.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static string?[]? UnpackStringArray(JsonValue? value, string[]? defaultValue = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToStringArray(),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a JSON value stored in a <b>required</b> field, into an array of <see cref="int"/>.</summary>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="parent">Parent JSON object that holds this field (used when throwing exceptions).</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to a string.</exception>
		[Pure]
		public static int[] UnpackRequiredInt32Array(JsonValue? value, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToInt32Array(),
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a JSON value stored in an optional field, into an array of <see cref="int"/>.</summary>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="defaultValue">Default value to return, if <paramref name="value"/> is null or missing.</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to a string.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static int[]? UnpackInt32Array(JsonValue? value, int[]? defaultValue = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToInt32Array(),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a JSON value stored in a <b>required</b> field, into an array of <see cref="long"/>.</summary>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="parent">Parent JSON object that holds this field (used when throwing exceptions).</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to a string.</exception>
		[Pure]
		public static long[] UnpackRequiredInt64Array(JsonValue? value, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToInt64Array(),
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a JSON value stored in an optional field, into an array of <see cref="long"/>.</summary>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="defaultValue">Default value to return, if <paramref name="value"/> is null or missing.</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to a string.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static long[]? UnpackInt64Array(JsonValue? value, long[]? defaultValue = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToInt64Array(),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a JSON value stored in a <b>required</b> field, into an array of <see cref="double"/>.</summary>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="parent">Parent JSON object that holds this field (used when throwing exceptions).</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to a string.</exception>
		[Pure]
		public static double[] UnpackRequiredDoubleArray(JsonValue? value, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToDoubleArray(),
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a JSON value stored in an optional field, into an array of <see cref="double"/>.</summary>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="defaultValue">Default value to return, if <paramref name="value"/> is null or missing.</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to a string.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static double[]? UnpackDoubleArray(JsonValue? value, double[]? defaultValue = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToDoubleArray(),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/>, stored into an optional field of a parent object</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static List<T>? UnpackList<T>(this IJsonDeserializer<T> serializer, JsonValue? value, List<T>? defaultValue = null, ICrystalJsonTypeResolver? resolver = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => UnpackList(serializer, arr, resolver),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/>, stored into an optional field of a parent object</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static List<T?>? UnpackList<T>(JsonValue? value, List<T?>? defaultValue = null, ICrystalJsonTypeResolver? resolver = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToList<T>(resolver: resolver),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/>, stored into a <b>required</b> field of a parent object</summary>
		[Pure]
		public static List<T> UnpackRequiredList<T>(this IJsonDeserializer<T> serializer, JsonValue? value, ICrystalJsonTypeResolver? resolver = null, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => UnpackList(serializer, arr, resolver),
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/> of <see cref="string"/>, stored into a <b>required</b> field of a parent object</summary>
		[Pure]
		public static List<T> UnpackRequiredList<T>(JsonValue? value, ICrystalJsonTypeResolver? resolver = null, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => (List<T>) arr.ToList<T>(resolver: resolver)!,
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/> of <see cref="string"/>, stored into an optional field of a parent object</summary>
		[Pure]
		public static List<string> UnpackRequiredStringList(JsonValue? value, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => (List<string>) arr.ToStringList()!,
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/> of <see cref="string"/>, stored into a <b>required</b> field of a parent object</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static List<string?>? UnpackStringList(JsonValue? value, List<string?>? defaultValue = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToStringList(),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/> of <see cref="int"/>, stored into a <b>required</b> field of a parent object</summary>
		[Pure]
		public static List<int> UnpackRequiredInt32List(JsonValue? value, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToInt32List(),
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/> of <see cref="int"/>, stored into an optional field of a parent object</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static List<int>? UnpackInt32List(JsonValue? value, List<int>? defaultValue = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToInt32List(),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/> of <see cref="long"/>, stored into a <b>required</b> field of a parent object</summary>
		[Pure]
		public static List<long> UnpackRequiredInt64List(JsonValue? value, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToInt64List(),
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/> of <see cref="long"/>, stored into an optional field of a parent object</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static List<long>? UnpackInt64List(JsonValue? value, List<long>? defaultValue = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToInt64List(),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/> of <see cref="double"/>, stored into a <b>required</b> field of a parent object</summary>
		[Pure]
		public static List<double> UnpackRequiredDoubleList(JsonValue? value, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToDoubleList(),
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/> of <see cref="double"/>, stored into an optional field of a parent object</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static List<double>? UnpackDoubleList(JsonValue? value, List<double>? defaultValue = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToDoubleList(),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/></summary>
		[Pure]
		public static List<T> UnpackList<T>(this IJsonDeserializer<T> serializer, JsonArray array, ICrystalJsonTypeResolver? resolver = null)
		{
			var input = array.GetSpan();

#if NET8_0_OR_GREATER
			var result = new List<T>();
			// return a span with the correct size
			CollectionsMarshal.SetCount(result, input.Length);
			var span = CollectionsMarshal.AsSpan(result);

			for (int i = 0; i < input.Length; i++)
			{
				span[i] = serializer.Unpack(input[i], resolver);
			}
#else
			var result = new List<T>(array.Count);
			foreach (var item in array)
			{
				result.Add(serializer.Unpack(item, resolver));
			}
#endif

			return result;
		}

		/// <summary>Deserializes a <see cref="JsonArray"/> into a custom collection (<see cref="System.Collections.ObjectModel.Collection{T}"/>, a <c>KeyedCollection&lt;TKey, TItem&gt;</c> subclass, ...), stored into an optional field of a parent object</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static TCollection? UnpackCollection<TCollection, T>(this IJsonDeserializer<T> serializer, JsonValue? value, TCollection? defaultValue = null, ICrystalJsonTypeResolver? resolver = null, string? fieldName = null)
			where TCollection : class, ICollection<T>, new()
			=> value switch
			{
				JsonArray arr => UnpackCollection<TCollection, T>(serializer, arr, resolver),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a custom collection (<see cref="System.Collections.ObjectModel.Collection{T}"/>, a <c>KeyedCollection&lt;TKey, TItem&gt;</c> subclass, ...), stored into a <b>required</b> field of a parent object</summary>
		[Pure]
		public static TCollection UnpackRequiredCollection<TCollection, T>(this IJsonDeserializer<T> serializer, JsonValue? value, ICrystalJsonTypeResolver? resolver = null, JsonValue? parent = null, string? fieldName = null)
			where TCollection : class, ICollection<T>, new()
			=> value switch
			{
				JsonArray arr => UnpackCollection<TCollection, T>(serializer, arr, resolver),
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a custom collection, constructed and populated via <see cref="ICollection{T}.Add"/> like a collection initializer</summary>
		[Pure]
		public static TCollection UnpackCollection<TCollection, T>(this IJsonDeserializer<T> serializer, JsonArray array, ICrystalJsonTypeResolver? resolver = null)
			where TCollection : class, ICollection<T>, new()
		{
			var result = new TCollection();
			foreach (var item in array)
			{
				result.Add(serializer.Unpack(item, resolver));
			}
			return result;
		}

		/// <summary>Deserializes a <see cref="JsonArray"/> into an <see cref="ImmutableArray{T}"/>, stored into an optional field of a parent object</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static ImmutableArray<T>? UnpackImmutableArray<T>(this IJsonDeserializer<T> serializer, JsonValue? value, ImmutableArray<T>? defaultValue = null, ICrystalJsonTypeResolver? resolver = null, string? fieldName = null)
			=> value switch
			{
				null or JsonNull => defaultValue,
				JsonArray arr => UnpackImmutableArray(serializer, arr, resolver),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into an <see cref="ImmutableArray{T}"/>, stored into an optional field of a parent object</summary>
		[Pure]
		public static ImmutableArray<T> UnpackImmutableArray<T>(this IJsonDeserializer<T> serializer, JsonArray array, ICrystalJsonTypeResolver? resolver = null)
		{
			// will wrap the array, without any copy
			return ImmutableCollectionsMarshal.AsImmutableArray<T>(UnpackArray(serializer, array, resolver));
		}

		/// <summary>Deserializes a <see cref="JsonArray"/> into an <see cref="ImmutableList{T}"/>, stored into an optional field of a parent object</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static ImmutableList<T>? UnpackImmutableList<T>(this IJsonDeserializer<T> serializer, JsonValue? value, ImmutableList<T>? defaultValue = null, ICrystalJsonTypeResolver? resolver = null, string? fieldName = null)
			=> value switch
			{
				null or JsonNull => defaultValue,
				JsonArray arr => UnpackImmutableList(serializer, arr, resolver),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into an <see cref="ImmutableList{T}"/>, stored into an optional field of a parent object</summary>
		[Pure]
		public static ImmutableList<T> UnpackImmutableList<T>(this IJsonDeserializer<T> serializer, JsonArray array, ICrystalJsonTypeResolver? resolver = null)
		{
			// not sure if there is a way to fill the immutable list "in place"?
			return ImmutableList.Create<T>(UnpackArray(serializer, array, resolver));
		}

		/// <summary>Deserializes a <see cref="JsonArray"/> into an <see cref="Dictionary{TKey,TValue}"/> with <see cref="string"/> keys, stored into an optional field of a parent object</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static Dictionary<string, TValue>? UnpackDictionary<TValue>(this IJsonDeserializer<TValue> serializer, JsonValue? value, Dictionary<string, TValue>? defaultValue = null, IEqualityComparer<string>? keyComparer = null, ICrystalJsonTypeResolver? resolver = null, string? fieldName = null)
			=> value switch
			{
				null or JsonNull => defaultValue,
				JsonObject obj => UnpackDictionary(serializer, obj, keyComparer, resolver),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonObject(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonObject(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into an <see cref="Dictionary{TKey,TValue}"/> with <see cref="string"/> keys, stored into an optional field of a parent object</summary>
		[Pure]
		public static Dictionary<string, TValue> UnpackDictionary<TValue>(this IJsonDeserializer<TValue> serializer, JsonObject obj, IEqualityComparer<string>? keyComparer = null, ICrystalJsonTypeResolver? resolver = null)
		{
			var res = new Dictionary<string, TValue>(obj.Count, keyComparer);

			foreach (var kv in obj)
			{
				res.Add(kv.Key, serializer.Unpack(kv.Value, resolver));
			}

			return res;
		}

		/// <summary>Deserializes a <see cref="JsonArray"/> into an <see cref="Dictionary{TKey,TValue}"/> with <see cref="int"/> keys, stored into an optional field of a parent object</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static Dictionary<int, TValue>? UnpackDictionaryInt32<TValue>(this IJsonDeserializer<TValue> serializer, JsonValue? value, Dictionary<int, TValue>? defaultValue = null, ICrystalJsonTypeResolver? resolver = null, string? fieldName = null)
			=> value switch
			{
				null or JsonNull => defaultValue,
				JsonObject obj   => UnpackDictionaryInt32(serializer, obj, resolver),
				_                => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonObject(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonObject(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into an <see cref="Dictionary{TKey,TValue}"/> with <see cref="int"/> keys, stored into an optional field of a parent object</summary>
		[Pure]
		public static Dictionary<int, TValue> UnpackDictionaryInt32<TValue>(this IJsonDeserializer<TValue> serializer, JsonObject obj, ICrystalJsonTypeResolver? resolver = null)
		{
			var res = new Dictionary<int, TValue>(obj.Count);

			foreach (var kv in obj)
			{
				if (!int.TryParse(kv.Key, out int k))
				{
					throw new InvalidOperationException("Cannot parse non-integer key");
				}
				res.Add(k, serializer.Unpack(kv.Value, resolver));
			}

			return res;
		}

		#endregion

		#region IJsonPacker<T>...

		#region Collections...

		/// <summary>Converts a span of items into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="items">Span of items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		public static JsonArray PackSpan<TValue>(this IJsonPacker<TValue> serializer, ReadOnlySpan<TValue> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			var result = new JsonArray();
			var span = result.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				span[i] = items[i] is not null ? serializer.Pack(items[i], settings, resolver) : JsonNull.Null;
			}

			if (settings.IsReadOnly())
			{
				result = result.FreezeUnsafe();
			}

			return result;
		}

		/// <summary>Converts an array of items into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="items">Items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Packed JSON Array, or <c>null</c> if <paramref name="items"/> is <c>null</c></returns>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArray<TValue>(this IJsonPacker<TValue> serializer, TValue[]? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null)
			{
				return null;
			}

			return PackSpan<TValue>(serializer, new ReadOnlySpan<TValue>(items), settings, resolver);
		}

		/// <summary>Converts a list of items into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="items">Items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Packed JSON Array, or <c>null</c> if <paramref name="items"/> is <c>null</c></returns>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackList<TValue>(this IJsonPacker<TValue> serializer, List<TValue>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null)
			{
				return null;
			}

			return PackSpan<TValue>(serializer, CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		/// <summary>Converts a sequence of items into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="items">Items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Packed JSON Array, or <c>null</c> if <paramref name="items"/> is <c>null</c></returns>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackEnumerable<TValue>(this IJsonPacker<TValue> serializer, IEnumerable<TValue>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null)
			{
				return null;
			}

			if (items.TryGetSpan(out var span))
			{
				return PackSpan<TValue>(serializer, span, settings, resolver);
			}

			_ = items.TryGetNonEnumeratedCount(out var count);
			var result = new JsonArray(count);
			foreach (var item in items)
			{
				result.Add(item is not null ? serializer.Pack(item, settings, resolver) : JsonNull.Null);
			}

			if (settings.IsReadOnly())
			{
				result = result.FreezeUnsafe();
			}

			return result;
		}

		#endregion

		#region Dictionaries...

		#region String keys...

		/// <summary>Converts a dictionary into the equivalent <see cref="JsonObject"/></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="items">Dictionary to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Packed JSON Object, or <c>null</c> if <paramref name="items"/> is <c>null</c></returns>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? PackObject<TValue>(this IJsonPacker<TValue> serializer, Dictionary<string, TValue>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null)
			{
				return null;
			}

			if (items.Count == 0)
			{
				return settings.IsReadOnly() ? JsonObject.ReadOnly.Empty : new();
			}

			var result = new Dictionary<string, JsonValue>(items.Count, settings.GetKeyComparer());
			foreach (var kv in items)
			{
				result[kv.Key] = kv.Value is not null ? serializer.Pack(kv.Value, settings, resolver) : JsonNull.Null;
			}

			return new(result, settings.IsReadOnly());
		}

		/// <summary>Converts a dictionary into the equivalent <see cref="JsonObject"/></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="items">Dictionary to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Packed JSON Object, or <c>null</c> if <paramref name="items"/> is <c>null</c></returns>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? PackObject<TValue>(this IJsonPacker<TValue> serializer, IDictionary<string, TValue>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null)
			{
				return null;
			}

			if (items.Count == 0)
			{
				return settings.IsReadOnly() ? JsonObject.ReadOnly.Empty : new();
			}

			var result = new Dictionary<string, JsonValue>(items.Count, settings.GetKeyComparer());
			if (items is Dictionary<string, TValue> dict)
			{
				// we can skip the IEnumerator<...> allocation for this type
				foreach (var kv in dict)
				{
					result[kv.Key] = kv.Value is not null ? serializer.Pack(kv.Value, settings, resolver) : JsonNull.Null;
				}
			}
			else
			{
				foreach (var kv in items)
				{
					result[kv.Key] = kv.Value is not null ? serializer.Pack(kv.Value, settings, resolver) : JsonNull.Null;
				}
			}

			return new(result, settings.IsReadOnly());
		}

		/// <summary>Converts a dictionary into the equivalent <see cref="JsonObject"/></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="items">Dictionary to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Packed JSON Object, or <c>null</c> if <paramref name="items"/> is <c>null</c></returns>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? PackObject<TValue>(this IJsonPacker<TValue> serializer, IDictionary<string, TValue[]?>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null)
			{
				return null;
			}

			if (items.Count == 0)
			{
				return settings.IsReadOnly() ? JsonObject.ReadOnly.Empty : new();
			}

			var result = new Dictionary<string, JsonValue>(items.Count, settings.GetKeyComparer());
			if (items is Dictionary<string, TValue[]?> dict)
			{
				// we can skip the IEnumerator<...> allocation for this type
				foreach (var kv in dict)
				{
					result[kv.Key] = kv.Value is not null ? serializer.PackArray(kv.Value, settings, resolver) : JsonNull.Null;
				}
			}
			else
			{
				foreach (var kv in items)
				{
					result[kv.Key] = kv.Value is not null ? serializer.PackArray(kv.Value, settings, resolver) : JsonNull.Null;
				}
			}

			return new(result, settings.IsReadOnly());
		}

		/// <summary>Converts a span of key/value pairs into the equivalent <see cref="JsonObject"/></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="items">Span to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Packed JSON Object, or <c>null</c> if <paramref name="items"/> is <c>null</c></returns>
		public static JsonObject PackObject<TValue>(this IJsonPacker<TValue> serializer, ReadOnlySpan<KeyValuePair<string, TValue>> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items.Length == 0)
			{
				return settings.IsReadOnly() ? JsonObject.ReadOnly.Empty : new();
			}

			var result = new Dictionary<string, JsonValue>(items.Length, settings.GetKeyComparer());

			foreach (var kv in items)
			{
				result[kv.Key] = kv.Value is not null ? serializer.Pack(kv.Value, settings, resolver) : JsonNull.Null;
			}

			return new(result, settings.IsReadOnly());
		}

		/// <summary>Converts a sequence of key/value pairs into the equivalent <see cref="JsonObject"/></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="items">Sequence to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Packed JSON Object, or <c>null</c> if <paramref name="items"/> is <c>null</c></returns>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? PackObject<TValue>(this IJsonPacker<TValue> serializer, IEnumerable<KeyValuePair<string, TValue>>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items is null)
			{
				return null;
			}

			if (items.TryGetSpan(out var span))
			{
				return PackObject(serializer, span, settings, resolver);
			}

			Dictionary<string, JsonValue> result;
			if (items.TryGetNonEnumeratedCount(out var count))
			{
				if (count == 0)
				{
					return settings.IsReadOnly() ? JsonObject.ReadOnly.Empty : new();
				}
				result = new(count, settings.GetKeyComparer());
			}
			else
			{
				result = new(settings.GetKeyComparer());
			}

			foreach (var kv in items)
			{
				result[kv.Key] = kv.Value is not null ? serializer.Pack(kv.Value, settings, resolver) : JsonNull.Null;
			}

			return new(result, settings.IsReadOnly());
		}

		#endregion

		#region Integer keys...

		/// <summary>Converts a dictionary into the equivalent <see cref="JsonObject"/></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="items">Dictionary to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Packed JSON Object, or <c>null</c> if <paramref name="items"/> is <c>null</c></returns>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? PackObject<TValue>(this IJsonPacker<TValue> serializer, Dictionary<int, TValue>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null)
			{
				return null;
			}

			if (items.Count == 0)
			{
				return settings.IsReadOnly() ? JsonObject.ReadOnly.Empty : new();
			}

			var result = new Dictionary<string, JsonValue>(items.Count, settings.GetKeyComparer());
			foreach (var kv in items)
			{
				result[StringConverters.ToString(kv.Key)] = kv.Value is not null ? serializer.Pack(kv.Value, settings, resolver) : JsonNull.Null;
			}

			return new(result, settings.IsReadOnly());
		}

		/// <summary>Converts a dictionary into the equivalent <see cref="JsonObject"/></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="items">Dictionary to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Packed JSON Object, or <c>null</c> if <paramref name="items"/> is <c>null</c></returns>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? PackObject<TValue>(this IJsonPacker<TValue> serializer, IDictionary<int, TValue>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null)
			{
				return null;
			}

			if (items.Count == 0)
			{
				return settings.IsReadOnly() ? JsonObject.ReadOnly.Empty : new();
			}

			var result = new Dictionary<string, JsonValue>(items.Count, settings.GetKeyComparer());
			if (items is Dictionary<int, TValue> dict)
			{
				// we can skip the IEnumerator<...> allocation for this type
				foreach (var kv in dict)
				{
					result[StringConverters.ToString(kv.Key)] = kv.Value is not null ? serializer.Pack(kv.Value, settings, resolver) : JsonNull.Null;
				}
			}
			else
			{
				foreach (var kv in items)
				{
					result[StringConverters.ToString(kv.Key)] = kv.Value is not null ? serializer.Pack(kv.Value, settings, resolver) : JsonNull.Null;
				}
			}

			return new(result, settings.IsReadOnly());
		}

		/// <summary>Converts a dictionary into the equivalent <see cref="JsonObject"/></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="items">Dictionary to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Packed JSON Object, or <c>null</c> if <paramref name="items"/> is <c>null</c></returns>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? PackObject<TValue>(this IJsonPacker<TValue> serializer, IDictionary<int, TValue[]?>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null)
			{
				return null;
			}

			if (items.Count == 0)
			{
				return settings.IsReadOnly() ? JsonObject.ReadOnly.Empty : new();
			}

			var result = new Dictionary<string, JsonValue>(items.Count, settings.GetKeyComparer());
			if (items is Dictionary<int, TValue[]?> dict)
			{
				// we can skip the IEnumerator<...> allocation for this type
				foreach (var kv in dict)
				{
					result[StringConverters.ToString(kv.Key)] = kv.Value is not null ? serializer.PackArray(kv.Value, settings, resolver) : JsonNull.Null;
				}
			}
			else
			{
				foreach (var kv in items)
				{
					result[StringConverters.ToString(kv.Key)] = kv.Value is not null ? serializer.PackArray(kv.Value, settings, resolver) : JsonNull.Null;
				}
			}

			return new(result, settings.IsReadOnly());
		}

		/// <summary>Converts a span of key/value pairs into the equivalent <see cref="JsonObject"/></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="items">Span to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Packed JSON Object, or <c>null</c> if <paramref name="items"/> is <c>null</c></returns>
		public static JsonObject PackObject<TValue>(this IJsonPacker<TValue> serializer, ReadOnlySpan<KeyValuePair<int, TValue>> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items.Length == 0)
			{
				return settings.IsReadOnly() ? JsonObject.ReadOnly.Empty : new();
			}

			var result = new Dictionary<string, JsonValue>(items.Length, settings.GetKeyComparer());

			foreach (var kv in items)
			{
				result[StringConverters.ToString(kv.Key)] = kv.Value is not null ? serializer.Pack(kv.Value, settings, resolver) : JsonNull.Null;
			}

			return new(result, settings.IsReadOnly());
		}

		/// <summary>Converts a sequence of key/value pairs into the equivalent <see cref="JsonObject"/></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="items">Sequence to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Packed JSON Object, or <c>null</c> if <paramref name="items"/> is <c>null</c></returns>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? PackObject<TValue>(this IJsonPacker<TValue> serializer, IEnumerable<KeyValuePair<int, TValue>>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items is null)
			{
				return null;
			}

			if (items.TryGetSpan(out var span))
			{
				return PackObject(serializer, span, settings, resolver);
			}

			Dictionary<string, JsonValue> result;
			if (items.TryGetNonEnumeratedCount(out var count))
			{
				if (count == 0)
				{
					return settings.IsReadOnly() ? JsonObject.ReadOnly.Empty : new();
				}
				result = new(count, settings.GetKeyComparer());
			}
			else
			{
				result = new(settings.GetKeyComparer());
			}

			foreach (var kv in items)
			{
				result[StringConverters.ToString(kv.Key)]= kv.Value is not null ? serializer.Pack(kv.Value, settings, resolver) : JsonNull.Null;
			}

			return new(result, settings.IsReadOnly());
		}

		#endregion

		#endregion

		#endregion

		#region CodeGen Helpers...

		// these methods are called by generated source code

		/// <summary>Converts a span of <see cref="bool"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Span of items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		public static JsonArray PackSpan(ReadOnlySpan<bool> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items.Length == 0)
			{
				return (settings.IsReadOnly()) ? JsonArray.ReadOnly.Empty : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i] ? JsonBoolean.True : JsonBoolean.False;
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a span of <see cref="int"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Span of items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		public static JsonArray PackSpan(ReadOnlySpan<int> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items.Length == 0)
			{
				return (settings.IsReadOnly()) ? JsonArray.ReadOnly.Empty : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonNumber.Return(items[i]);
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a span of <see cref="long"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Span of items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		public static JsonArray PackSpan(ReadOnlySpan<long> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items.Length == 0)
			{
				return (settings.IsReadOnly()) ? JsonArray.ReadOnly.Empty : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonNumber.Return(items[i]);
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a span of <see cref="float"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Span of items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		public static JsonArray PackSpan(ReadOnlySpan<float> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items.Length == 0)
			{
				return (settings.IsReadOnly()) ? JsonArray.ReadOnly.Empty : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonNumber.Return(items[i]);
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a span of <see cref="double"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Span of items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		public static JsonArray PackSpan(ReadOnlySpan<double> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items.Length == 0)
			{
				return (settings.IsReadOnly()) ? JsonArray.ReadOnly.Empty : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonNumber.Return(items[i]);
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a span of <see cref="string"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Span of items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		public static JsonArray PackSpan(ReadOnlySpan<string> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items.Length == 0)
			{
				return settings.IsReadOnly() ? JsonArray.ReadOnly.Empty : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonString.Return(items[i]);
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a span of <see cref="Guid"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Span of items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		public static JsonArray PackSpan(ReadOnlySpan<Guid> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items.Length == 0)
			{
				return (settings.IsReadOnly()) ? JsonArray.ReadOnly.Empty : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonString.Return(items[i]);
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a span of <see cref="Uuid128"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Span of items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		public static JsonArray PackSpan(ReadOnlySpan<Uuid128> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items.Length == 0)
			{
				return (settings.IsReadOnly()) ? JsonArray.ReadOnly.Empty : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonString.Return(items[i]);
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a span of <see cref="Uuid64"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Span of items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		public static JsonArray PackSpan(ReadOnlySpan<Uuid64> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items.Length == 0)
			{
				return (settings.IsReadOnly()) ? JsonArray.ReadOnly.Empty : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonString.Return(items[i]);
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a sequence of <see cref="bool"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Sequence to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackEnumerable(IEnumerable<bool>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (items.TryGetSpan(out var span))
			{
				return PackSpan(span, settings, resolver);
			}

			JsonArray arr = items.TryGetNonEnumeratedCount(out var count) ? new(count) : new();

			foreach (var item in items)
			{
				arr.Add(item ? JsonBoolean.True : JsonBoolean.False);
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a sequence of <see cref="int"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Sequence to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackEnumerable(IEnumerable<int>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (items.TryGetSpan(out var span))
			{
				return PackSpan(span, settings, resolver);
			}

			JsonArray arr = items.TryGetNonEnumeratedCount(out var count) ? new(count) : new();

			foreach (var item in items)
			{
				arr.Add(JsonNumber.Return(item));
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a sequence of <see cref="long"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Sequence to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackEnumerable(IEnumerable<long>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (items.TryGetSpan(out var span))
			{
				return PackSpan(span, settings, resolver);
			}

			JsonArray arr = items.TryGetNonEnumeratedCount(out var count) ? new(count) : new();

			foreach (var item in items)
			{
				arr.Add(JsonNumber.Return(item));
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a sequence of <see cref="float"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Sequence to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackEnumerable(IEnumerable<float>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (items.TryGetSpan(out var span))
			{
				return PackSpan(span, settings, resolver);
			}

			JsonArray arr = items.TryGetNonEnumeratedCount(out var count) ? new(count) : new();

			foreach (var item in items)
			{
				arr.Add(JsonNumber.Return(item));
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a sequence of <see cref="double"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Sequence to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackEnumerable(IEnumerable<double>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (items.TryGetSpan(out var span))
			{
				return PackSpan(span, settings, resolver);
			}

			JsonArray arr = items.TryGetNonEnumeratedCount(out var count) ? new(count) : new();

			foreach (var item in items)
			{
				arr.Add(JsonNumber.Return(item));
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a sequence of <see cref="string"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Sequence to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackEnumerable(IEnumerable<string>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (items.TryGetSpan(out var span))
			{
				return PackSpan(span, settings, resolver);
			}

			JsonArray arr = items.TryGetNonEnumeratedCount(out var count) ? new(count) : new();

			foreach (var item in items)
			{
				arr.Add(JsonString.Return(item));
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a sequence of <see cref="Guid"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Sequence to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackEnumerable(IEnumerable<Guid>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (items.TryGetSpan(out var span))
			{
				return PackSpan(span, settings, resolver);
			}

			JsonArray arr = items.TryGetNonEnumeratedCount(out var count) ? new(count) : new();

			foreach (var item in items)
			{
				arr.Add(JsonString.Return(item));
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a sequence of <see cref="Uuid128"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Sequence to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackEnumerable(IEnumerable<Uuid128>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (items.TryGetSpan(out var span))
			{
				return PackSpan(span, settings, resolver);
			}

			JsonArray arr = items.TryGetNonEnumeratedCount(out var count) ? new(count) : new();

			foreach (var item in items)
			{
				arr.Add(JsonString.Return(item));
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts an array of <see cref="bool"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Array to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArray(bool[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(new ReadOnlySpan<bool>(items), settings, resolver);
		}

		/// <summary>Converts an array of <see cref="int"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Array to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArray(int[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(new ReadOnlySpan<int>(items), settings, resolver);
		}

		/// <summary>Converts an array of <see cref="long"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Array to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArray(long[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(new ReadOnlySpan<long>(items), settings, resolver);
		}

		/// <summary>Converts an array of <see cref="float"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Array to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArray(float[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(new ReadOnlySpan<float>(items), settings, resolver);
		}

		/// <summary>Converts an array of <see cref="double"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Array to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArray(double[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(new ReadOnlySpan<double>(items), settings, resolver);
		}

		/// <summary>Converts an array of <see cref="string"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Array to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArray(string[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(new ReadOnlySpan<string>(items), settings, resolver);
		}

		/// <summary>Converts an array of <see cref="Guid"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Array to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArray(Guid[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(new ReadOnlySpan<Guid>(items), settings, resolver);
		}

		/// <summary>Converts an array of <see cref="Uuid128"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Array to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArray(Uuid128[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(new ReadOnlySpan<Uuid128>(items), settings, resolver);
		}

		/// <summary>Converts an array of <see cref="Uuid64"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Array to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArray(Uuid64[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(new ReadOnlySpan<Uuid64>(items), settings, resolver);
		}

		/// <summary>Converts a list of <see cref="string"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">List to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackList(List<string>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		/// <summary>Converts a list of <see cref="bool"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">List to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackList(List<bool>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		/// <summary>Converts a list of <see cref="int"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">List to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackList(List<int>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		/// <summary>Converts a list of <see cref="long"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">List to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackList(List<long>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		/// <summary>Converts a list of <see cref="float"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">List to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackList(List<float>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		/// <summary>Converts a list of <see cref="double"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">List to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackList(List<double>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		/// <summary>Converts a list of <see cref="Guid"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">List to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackList(List<Guid>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		/// <summary>Converts a list of <see cref="Uuid64"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">List to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackList(List<Uuid64>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		/// <summary>Converts a span of <typeparamref name="TValue"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Span of items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		public static JsonArray PackSpan<TValue>(ReadOnlySpan<TValue?> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			return JsonArray.FromValues(items, settings, resolver);
		}

		/// <summary>Converts a span of items that implements <see cref="IJsonPackable"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Span of items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		public static JsonArray PackSpanPackable<TPackable>(ReadOnlySpan<TPackable?> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
			where TPackable : IJsonPackable
		{
			settings ??= CrystalJsonSettings.Json;
			resolver ??= CrystalJson.DefaultResolver;
			if (items.Length == 0) return settings.ReadOnly ? JsonArray.ReadOnly.Empty : [ ];

			var buffer = new JsonValue[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buffer[i] = items[i]?.JsonPack(settings, resolver) ?? JsonNull.Null;
			}
			return new(buffer, items.Length, settings.ReadOnly);
		}

		/// <summary>Converts an array of <typeparamref name="TValue"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Array to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArray<TValue>(TValue[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			return items is not null ? JsonArray.FromValues<TValue>(new ReadOnlySpan<TValue>(items), settings, resolver) : null;
		}

		/// <summary>Converts an array of items that implements <see cref="IJsonPackable"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Array to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArrayPackable<TPackable>(TPackable[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
			where TPackable : IJsonPackable
		{
			return items is not null ? PackSpanPackable<TPackable>(new(items), settings, resolver) : null;
		}

		/// <summary>Converts a list of <typeparamref name="TValue"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">List to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackList<TValue>(List<TValue>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			return items is not null ? JsonArray.FromValues<TValue>(CollectionsMarshal.AsSpan(items), settings, resolver) : null;
		}

		/// <summary>Converts a list of items that implements <see cref="IJsonPackable"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">List to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackListPackable<TPackable>(List<TPackable>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
			where TPackable : IJsonPackable
		{
			return items is not null ? PackSpanPackable<TPackable>(CollectionsMarshal.AsSpan(items), settings, resolver) : null;
		}

		/// <summary>Converts a sequence of <typeparamref name="TValue"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Sequence to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackEnumerable<TValue>(IEnumerable<TValue?>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonArray.FromValues(items, settings, resolver);
		}

		/// <summary>Converts a sequence of items that implements <see cref="IJsonPackable"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Sequence to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackEnumerablePackable<TPackable>(IEnumerable<TPackable?>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
			where TPackable : IJsonPackable
		{
			if (items == null) return null;
			if (items.TryGetSpan(out var span))
			{
				return PackSpanPackable(span, settings, resolver);
			}

			return PackEnumerableSlow(items, settings, resolver);

			static JsonArray PackEnumerableSlow(IEnumerable<TPackable?> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
			{
				settings ??= CrystalJsonSettings.Json;
				resolver ??= CrystalJson.DefaultResolver;
				var arr = new JsonArray(items.TryGetNonEnumeratedCount(out var count) ? count : 0);
				foreach (var item in items)
				{
					arr.Add(item?.JsonPack(settings, resolver) ?? JsonNull.Null);
				}
				return settings.ReadOnly ? arr.FreezeUnsafe() : arr;
			}
		}

		/// <summary>Converts a dictionary with string keys into the equivalent <see cref="JsonObject"/></summary>
		/// <param name="items">Dictionary to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? PackDictionary<TValue>(Dictionary<string, TValue>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonObject.FromValues<TValue>(items, settings, resolver);
		}

		/// <summary>Converts a dictionary with string keys into the equivalent <see cref="JsonObject"/></summary>
		/// <param name="items">Dictionary to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? PackDictionary<TValue>(IDictionary<string, TValue>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonObject.FromValues<TValue>(items, settings, resolver);
		}

		/// <summary>Converts a dictionary with string keys into the equivalent <see cref="JsonObject"/></summary>
		/// <param name="items">Dictionary to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? PackDictionary<TValue>(IEnumerable<KeyValuePair<string, TValue>>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonObject.FromValues<TValue>(items, settings, resolver);
		}

		/// <summary>Deserializes a JSON value into an instance of <typeparamref name="T"/>, that is known to implement <see cref="IJsonDeserializable{T}"/></summary>
		/// <typeparam name="T">Type that implements <see cref="IJsonDeserializable{T}"/></typeparam>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonBindingException"> if <paramref name="jsonBytes"/> could not be bound to the type <typeparamref name="T"/>.</exception>
		public static T DeserializeJsonDeserializable<T>(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			where T : IJsonDeserializable<T>
		{
			var value = CrystalJson.Parse(jsonBytes, settings).Required();
#if NET7_0_OR_GREATER
			return T.JsonDeserialize(value, resolver);
#else
			// static interface dispatch needs runtime support this target lacks: go through the reflection-based dispatcher
			return JsonDeserializableDispatcher<T>.Invoke(value, resolver);
#endif
		}

		/// <summary>Deserializes a JSON value into an instance of <typeparamref name="T"/>, that is known to implement <see cref="IJsonDeserializable{T}"/></summary>
		/// <typeparam name="T">Type that implements <see cref="IJsonDeserializable{T}"/></typeparam>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <param name="defaultValue">Fallback value returned when <paramref name="jsonBytes"/> is empty or <c>"null"</c>.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Deserialized instance, or <paramref name="defaultValue"/> if <paramref name="jsonBytes"/> is empty or <c>"null"</c></returns>
		/// <exception cref="JsonBindingException"> if <paramref name="jsonBytes"/> could not be bound to the type <typeparamref name="T"/>.</exception>
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static T? DeserializeJsonDeserializable<T>(ReadOnlySpan<byte> jsonBytes, T? defaultValue, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			where T : IJsonDeserializable<T>
		{
			var value = CrystalJson.Parse(jsonBytes, settings);
			if (value is JsonNull)
			{
				return defaultValue;
			}
#if NET7_0_OR_GREATER
			return T.JsonDeserialize(value, resolver);
#else
			// static interface dispatch needs runtime support this target lacks: go through the reflection-based dispatcher
			return JsonDeserializableDispatcher<T>.Invoke(value, resolver);
#endif
		}

		/// <summary>Deserializes a JSON value into an instance of <typeparamref name="T"/>, that is known to implement <see cref="IJsonDeserializable{T}"/></summary>
		/// <typeparam name="T">Type that implements <see cref="IJsonDeserializable{T}"/></typeparam>
		/// <param name="value">JSON value to deserialize</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Deserialized instance, or <c>null</c> if <paramref name="value"/> is null or missing</returns>
		/// <exception cref="JsonBindingException"> if <paramref name="value"/> could not be bound to the type <typeparamref name="T"/>.</exception>
		public static T UnpackJsonDeserializable<T>(JsonValue value, ICrystalJsonTypeResolver? resolver)
			where T : IJsonDeserializable<T>
		{
#if NET7_0_OR_GREATER
			return T.JsonDeserialize(value.Required(), resolver);
#else
			// static interface dispatch needs runtime support this target lacks: go through the reflection-based dispatcher
			return JsonDeserializableDispatcher<T>.Invoke(value.Required(), resolver);
#endif
		}

		/// <summary>Deserializes a JSON value into an instance of <typeparamref name="T"/>, that is known to implement <see cref="IJsonDeserializable{T}"/></summary>
		/// <typeparam name="T">Type that implements <see cref="IJsonDeserializable{T}"/></typeparam>
		/// <param name="value">JSON value to deserialize</param>
		/// <param name="defaultValue">Fallback value returned when <paramref name="value"/> is null or missing.</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Deserialized instance, or <paramref name="defaultValue"/> if <paramref name="value"/> is null or missing</returns>
		/// <exception cref="JsonBindingException"> if <paramref name="value"/> could not be bound to the type <typeparamref name="T"/>.</exception>
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static T? UnpackJsonDeserializable<T>(JsonValue? value, T? defaultValue, ICrystalJsonTypeResolver? resolver)
			where T : IJsonDeserializable<T>
		{
			if (value is null or JsonNull)
			{
				return defaultValue;
			}
#if NET7_0_OR_GREATER
			return T.JsonDeserialize(value, resolver);
#else
			// static interface dispatch needs runtime support this target lacks: go through the reflection-based dispatcher
			return JsonDeserializableDispatcher<T>.Invoke(value, resolver);
#endif
		}

		/// <summary>Deserializes a JSON value into an instance of <typeparamref name="T"/></summary>
		/// <typeparam name="T">Type of the target instance</typeparam>
		/// <param name="value">JSON value to deserialize</param>
		/// <param name="defaultValue">Fallback value returned when <paramref name="value"/> is null or missing.</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Deserialized instance, or <paramref name="defaultValue"/> if <paramref name="value"/> is null or missing</returns>
		/// <exception cref="JsonBindingException"> if <paramref name="value"/> could not be bound to the type <typeparamref name="T"/>.</exception>
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static T? Unpack<T>(JsonValue? value, T? defaultValue, ICrystalJsonTypeResolver? resolver)
		{
			return value.As<T>(defaultValue, resolver);
		}

		/// <summary>Deserializes a JSON value into a <see cref="Nullable{T}"/> value.</summary>
		/// <typeparam name="T">Type of the target value</typeparam>
		/// <param name="value">JSON value to deserialize</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Deserialized instance, or <c>null</c> if <paramref name="value"/> is null or missing</returns>
		/// <exception cref="JsonBindingException"> if <paramref name="value"/> could not be bound to the type <typeparamref name="T"/>.</exception>
		public static T? UnpackNullableJsonDeserializable<T>(JsonValue? value, ICrystalJsonTypeResolver? resolver)
			where T : struct, IJsonDeserializable<T>
		{
			if (value is null or JsonNull)
			{
				return null;
			}
#if NET7_0_OR_GREATER
			return T.JsonDeserialize(value, resolver);
#else
			// static interface dispatch needs runtime support this target lacks: go through the reflection-based dispatcher
			return JsonDeserializableDispatcher<T>.Invoke(value, resolver);
#endif
		}

		/// <summary>Deserializes a JSON value into a <see cref="Nullable{T}"/> value.</summary>
		/// <typeparam name="T">Type of the target value</typeparam>
		/// <param name="value">JSON value to deserialize</param>
		/// <param name="defaultValue">Fallback value returned when <paramref name="value"/> is null or missing.</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Deserialized instance, or <paramref name="defaultValue"/> if <paramref name="value"/> is null or missing</returns>
		/// <exception cref="JsonBindingException"> if <paramref name="value"/> could not be bound to the type <typeparamref name="T"/>.</exception>
		public static T? UnpackNullableJsonDeserializable<T>(JsonValue? value, T? defaultValue, ICrystalJsonTypeResolver? resolver)
			where T : struct, IJsonDeserializable<T>
		{
			if (value is null or JsonNull)
			{
				return defaultValue;
			}
#if NET7_0_OR_GREATER
			return T.JsonDeserialize(value, resolver);
#else
			// static interface dispatch needs runtime support this target lacks: go through the reflection-based dispatcher
			return JsonDeserializableDispatcher<T>.Invoke(value, resolver);
#endif
		}

		/// <summary>Deserializes a required JSON value into an instance of <typeparamref name="T"/>, that is known to implement <see cref="IJsonDeserializable{T}"/></summary>
		/// <typeparam name="T">Type of the target instance</typeparam>
		/// <param name="value">JSON value to deserialize</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <param name="parent">Parent JSON object that holds this field (used when throwing exceptions).</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonBindingException"> if <paramref name="value"/> is null or missing, or it could not be bound to the type <typeparamref name="T"/>.</exception>
		public static T UnpackRequiredJsonDeserializable<T>(JsonValue? value, ICrystalJsonTypeResolver? resolver, JsonValue? parent = null, string? fieldName = null)
			where T : IJsonDeserializable<T>
		{
			if (value is null or JsonNull)
			{
				throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing());
			}
#if NET7_0_OR_GREATER
			return T.JsonDeserialize(value, resolver);
#else
			// static interface dispatch needs runtime support this target lacks: go through the reflection-based dispatcher
			return JsonDeserializableDispatcher<T>.Invoke(value, resolver);
#endif
		}

		/// <summary>Deserializes a required JSON value into an instance of <typeparamref name="T"/></summary>
		/// <typeparam name="T">Type that implements <see cref="IJsonDeserializable{T}"/></typeparam>
		/// <param name="converter">Deserializer instance to use</param>
		/// <param name="value">JSON value to deserialize</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <param name="parent">Parent JSON object that holds this field (used when throwing exceptions).</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonBindingException"> if <paramref name="value"/> is null or missing, or it could not be bound to the type <typeparamref name="T"/>.</exception>
		public static T UnpackRequired<T>(this IJsonDeserializer<T> converter, JsonValue? value, ICrystalJsonTypeResolver? resolver, JsonValue? parent = null, string? fieldName = null)
			where T : notnull
		{
			if (value is null or JsonNull)
			{
				throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing());
			}
			return converter.Unpack(value, resolver);
		}

		/// <summary>Verifies that a member declared <c>[DataMember(IsRequired = true)]</c> is PRESENT in the document</summary>
		/// <param name="obj">Object being bound</param>
		/// <param name="fieldName">Name of the field on the wire</param>
		/// <exception cref="JsonBindingException"> if <paramref name="fieldName"/> is absent from <paramref name="obj"/>.</exception>
		/// <remarks>
		/// <para>An explicit <c>null</c> SATISFIES this contract, which is what <c>DataContractJsonSerializer</c> does; only absence is refused. The C# <c>required</c> keyword is stricter and refuses null as well.</para>
		/// <para>Presence is independent of how the member's value is decoded, so generated converters emit this as a single guard per member rather than threading the condition through every decoding shape.</para>
		/// </remarks>
		public static void VerifyRequiredPresence(JsonObject obj, string fieldName)
		{
			if (!obj.ContainsKey(fieldName))
			{
				throw CrystalJson.Errors.Parsing_FieldIsNullOrMissing(obj, fieldName, null);
			}
		}

		/// <summary>Deserializes a required JSON value into an instance of <typeparamref name="T"/></summary>
		/// <typeparam name="T">Type that implements <see cref="IJsonDeserializable{T}"/></typeparam>
		/// <param name="value">JSON value to deserialize</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <param name="parent">Parent JSON object that holds this field (used when throwing exceptions).</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonBindingException"> if <paramref name="value"/> is null or missing, or it could not be bound to the type <typeparamref name="T"/>.</exception>
		public static T UnpackRequired<T>(JsonValue? value, ICrystalJsonTypeResolver? resolver, JsonValue? parent = null, string? fieldName = null)
			where T : notnull
		{
			if (value is null or JsonNull)
			{
				throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing());
			}
			return value.Required<T>(resolver);
		}

		/// <summary>Deserializes a required JSON value into an instance of <typeparamref name="T"/></summary>
		/// <typeparam name="T">Type that implements <see cref="IJsonDeserializable{T}"/></typeparam>
		/// <param name="converter">Deserializer instance to use</param>
		/// <param name="value">JSON value to deserialize</param>
		/// <param name="defaultValue">Default value to return, if <paramref name="value"/> is null or missing.</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonBindingException"> if <paramref name="value"/> is null or missing, or it could not be bound to the type <typeparamref name="T"/>.</exception>
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static T? Unpack<T>(this IJsonDeserializer<T> converter, JsonValue? value, T? defaultValue, ICrystalJsonTypeResolver? resolver)
			where T : notnull
			{
			if (value is null or JsonNull)
			{
				return defaultValue;
			}
			return converter.Unpack(value, resolver);
		}

		/// <summary>Deserializes a required JSON value into an instance of <typeparamref name="T"/></summary>
		/// <typeparam name="T">Type that implements <see cref="IJsonDeserializable{T}"/></typeparam>
		/// <param name="value">JSON value to deserialize</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <param name="parent">Parent JSON object that holds this field (used when throwing exceptions).</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonBindingException"> if <paramref name="value"/> is null or missing, or it could not be bound to the type <typeparamref name="T"/>.</exception>
		public static T Unpack<T>(JsonValue? value, ICrystalJsonTypeResolver? resolver, JsonValue? parent = null, string? fieldName = null)
			where T : notnull
		{
			if (value is null or JsonNull)
			{
				throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing());
			}
			return value.Required<T>(resolver);
		}

		/// <summary>Deserializes an optional JSON value into a <see cref="Nullable{T}"/>, using a custom deserializer</summary>
		/// <returns>Deserialized instance, or <see langword="null"/> if <paramref name="value"/> is null or missing</returns>
		public static T? UnpackNullable<T>(this IJsonDeserializer<T> converter, JsonValue? value, ICrystalJsonTypeResolver? resolver)
			where T : struct
		{
			if (value is null or JsonNull)
			{
				return null;
			}
			return converter.Unpack(value, resolver);
		}

		/// <summary>Deserializes an optional JSON value into a <see cref="Nullable{T}"/>, using a custom deserializer declared for the nullable form itself (<c>IJsonDeserializer&lt;T?&gt;</c>)</summary>
		/// <returns>Whatever the converter answers for a present value (which may be <see langword="null"/>, e.g. for a present-but-unreadable value), or <see langword="null"/> if <paramref name="value"/> is null or missing (the converter never runs)</returns>
		public static T? UnpackNullableForm<T>(this IJsonDeserializer<T?> converter, JsonValue? value, ICrystalJsonTypeResolver? resolver)
			where T : struct
		{
			if (value is null or JsonNull)
			{
				return null;
			}
			return converter.Unpack(value, resolver);
		}

		/// <summary>Deserializes a required JSON value into a <see cref="Nullable{T}"/>, using a custom deserializer declared for the nullable form itself (<c>IJsonDeserializer&lt;T?&gt;</c>)</summary>
		/// <returns>Whatever the converter answers for a present value (which may be <see langword="null"/>, e.g. for a present-but-unreadable value)</returns>
		/// <exception cref="JsonBindingException"> if <paramref name="value"/> is null or missing.</exception>
		public static T? UnpackRequiredNullableForm<T>(this IJsonDeserializer<T?> converter, JsonValue? value, ICrystalJsonTypeResolver? resolver, JsonValue? parent = null, string? fieldName = null)
			where T : struct
		{
			if (value is null or JsonNull)
			{
				throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing());
			}
			return converter.Unpack(value, resolver);
		}

		/// <summary>Fails because an asymmetric converter was asked to pack, but only implements the deserializing facet</summary>
		/// <remarks>Declared as returning a value so that call sites (including generated code) can use it in expression position without unreachable-code warnings; it always throws.</remarks>
		public static JsonValue FailConverterMissingPackerFacet(Type converterType, Type valueType)
		{
			throw new JsonSerializationException($"Converter '{converterType.GetFriendlyName()}' only implements deserializing (IJsonDeserializer<{valueType.GetFriendlyName()}>); packing this value requires it to also implement IJsonPacker<{valueType.GetFriendlyName()}>.Pack({valueType.GetFriendlyName()} instance, CrystalJsonSettings?, ICrystalJsonTypeResolver?).");
		}

		/// <summary>Fails because an asymmetric converter was asked to deserialize, but only implements the packing facet</summary>
		/// <remarks>Declared as returning a value so that call sites (including generated code) can use it in expression position without unreachable-code warnings; it always throws.</remarks>
		public static T FailConverterMissingDeserializerFacet<T>(Type converterType)
		{
			throw new JsonBindingException($"Converter '{converterType.GetFriendlyName()}' only implements packing (IJsonPacker<{typeof(T).GetFriendlyName()}>); deserializing this value requires it to also implement IJsonDeserializer<{typeof(T).GetFriendlyName()}>.Unpack(JsonValue, ICrystalJsonTypeResolver?).");
		}

		/// <summary>Deserializes an optional JSON value through an asymmetric converter that lacks the deserializing facet: an absent or null value binds to the default (the converter never runs), anything else fails loudly</summary>
		public static T? FailConverterMissingDeserializerFacet<T>(JsonValue? value, Type converterType, T? defaultValue)
		{
			if (value is null or JsonNull)
			{
				return defaultValue;
			}
			return FailConverterMissingDeserializerFacet<T>(converterType);
		}

		/// <summary>Packs an enum value into its string form, honoring any custom wire tokens declared on the enum's fields</summary>
		/// <remarks>Used by generated converters for members carrying <c>[JsonProperty(EnumFormat = JsonEnumFormat.String)]</c>.</remarks>
		[Pure]
		public static JsonValue PackEnumString<TEnum>(TEnum value)
			where TEnum : struct, Enum
		{
			return CrystalJsonEnumCache.GetLiteral(typeof(TEnum), value);
		}

		/// <summary>Packs an enum value into its numeric form</summary>
		/// <remarks>Used by generated converters for members carrying <c>[JsonProperty(EnumFormat = JsonEnumFormat.Number)]</c>.</remarks>
		[Pure]
		public static JsonValue PackEnumNumber<TEnum>(TEnum value)
			where TEnum : struct, Enum
		{
			return CrystalJsonEnumCache.GetNumber(typeof(TEnum), value);
		}

		#endregion

		extension<TJsonPackable>(TJsonPackable? self)
			where TJsonPackable : IJsonPackable
		{

			/// <summary>Packs a <see cref="IJsonPackable"/> value into the corresponding mutable <see cref="JsonValue"/></summary>
			/// <param name="settings">Custom serialization settings</param>
			/// <param name="resolver">Optional type resolver used to bind the value into a managed CLR type (<see cref="CrystalJson.DefaultResolver"/> is omitted)</param>
			/// <remarks>Note: if the JSON has to be sent over HTTP, or stored on disk, prefer <see cref="JsonValueExtensions.ToJsonSlice(JsonValue?,CrystalJsonSettings?)"/> or <see cref="JsonValueExtensions.ToJsonBytes(JsonValue)"/> that will return the same result but already utf-8 encoded</remarks>
			public JsonValue ToJsonValue(CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				return self?.JsonPack(settings ?? CrystalJsonSettings.Json, resolver ?? CrystalJson.DefaultResolver) ?? JsonNull.Null;
			}

			/// <summary>Packs a <see cref="IJsonPackable"/> value into the corresponding read-only <see cref="JsonValue"/></summary>
			/// <param name="settings">Custom serialization settings</param>
			/// <param name="resolver">Optional type resolver used to bind the value into a managed CLR type (<see cref="CrystalJson.DefaultResolver"/> is omitted)</param>
			/// <remarks>Note: if the JSON has to be sent over HTTP, or stored on disk, prefer <see cref="JsonValueExtensions.ToJsonSlice(JsonValue?,CrystalJsonSettings?)"/> or <see cref="JsonValueExtensions.ToJsonBytes(JsonValue)"/> that will return the same result but already utf-8 encoded</remarks>
			public JsonValue ToJsonValueReadOnly(CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				return self?.JsonPack(settings.AsReadOnly(), resolver ?? CrystalJson.DefaultResolver) ?? JsonNull.Null;
			}

			[Pure]
			public static JsonValue JsonPack(TJsonPackable? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				return value is not null ? value.JsonPack(settings ?? CrystalJsonSettings.Json, resolver ?? CrystalJson.DefaultResolver) : JsonNull.Null;
			}

			[Pure]
			public static JsonValue JsonPack(TJsonPackable? value, JsonValue? defaultValue, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				return value?.JsonPack(settings ?? CrystalJsonSettings.Json, resolver ?? CrystalJson.DefaultResolver) ?? defaultValue ?? JsonNull.Null;
			}

			[Pure]
			public static JsonValue JsonPackReadOnly(TJsonPackable? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				return value is not null ? value.JsonPack(settings.AsReadOnly(), resolver ?? CrystalJson.DefaultResolver) : JsonNull.Null;
			}

			[Pure]
			public static JsonValue JsonPackReadOnly(TJsonPackable? value, JsonValue? defaultValue, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				return value?.JsonPack(settings.AsReadOnly(), resolver ?? CrystalJson.DefaultResolver) ?? defaultValue?.ToReadOnly() ?? JsonNull.Null;
			}

			[Pure]
			public static JsonArray? JsonPackArray(IEnumerable<TJsonPackable?>? values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				if (values is null) return null;
				if (values.TryGetSpan(out var span))
				{
					return JsonPackArray<TJsonPackable>(span, settings, resolver);
				}

				return JsonPackArrayEnumerable(values, settings, resolver);

				static JsonArray JsonPackArrayEnumerable(IEnumerable<TJsonPackable?> values, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
				{
					settings ??= CrystalJsonSettings.Json;

					JsonArray array;

					if (values.TryGetNonEnumeratedCount(out var count))
					{
						if (count == 0) return settings.IsReadOnly() ? JsonArray.ReadOnly.Empty : [ ];
						array = new(count);
					}
					else
					{
						array = [ ];
					}

					resolver ??= CrystalJson.DefaultResolver;
					foreach (var value in values)
					{
						array.Add(value?.JsonPack(settings, resolver) ?? JsonNull.Null);
					}

					if (settings.IsReadOnly())
					{
						array.FreezeUnsafe();
					}

					return array;
				}
			}

			[Pure]
			public static JsonArray JsonPackArray(ReadOnlySpan<TJsonPackable?> values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				settings ??= CrystalJsonSettings.Json;
				if (values.Length == 0) return settings.IsReadOnly() ? JsonArray.ReadOnly.Empty : [ ];

				var tmp = new JsonValue[values.Length];
				resolver ??= CrystalJson.DefaultResolver;
				for (int i = 0; i < values.Length; i ++)
				{
					tmp[i] = values[i]?.JsonPack(settings, resolver) ?? JsonNull.Null;
				}

				return new(tmp, tmp.Length, settings.IsReadOnly());
			}

			[Pure]
			public static JsonArray? JsonPackArrayReadOnly(IEnumerable<TJsonPackable?>? values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
				=> JsonPackArray<TJsonPackable>(values, settings.AsReadOnly(), resolver);

			[Pure]
			public static JsonArray JsonPackArrayReadOnly(ReadOnlySpan<TJsonPackable?> values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
				=> JsonPackArray<TJsonPackable>(values, settings.AsReadOnly(), resolver);

		}

		extension<TJsonDeserializable>(TJsonDeserializable)
			where TJsonDeserializable: IJsonDeserializable<TJsonDeserializable>
		{

			#region static JsonDeserialize...

			[Pure]
			public static TJsonDeserializable JsonDeserialize(Slice jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
				=> JsonDeserialize<TJsonDeserializable>(jsonBytes.Span, settings, resolver);

			[Pure]
			public static TJsonDeserializable JsonDeserialize(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				var value = CrystalJson.Parse(jsonBytes, settings).Required();
#if NET7_0_OR_GREATER
				return TJsonDeserializable.JsonDeserialize(value, resolver);
#else
				// static interface dispatch needs runtime support this target lacks: go through the reflection-based dispatcher
				return JsonDeserializableDispatcher<TJsonDeserializable>.Invoke(value, resolver);
#endif
			}

			[Pure]
			[return: NotNullIfNotNull(nameof(defaultValue))]
			public static TJsonDeserializable JsonDeserialize(Slice jsonBytes, TJsonDeserializable? defaultValue, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
				=> JsonDeserialize<TJsonDeserializable>(jsonBytes.Span, settings, resolver);

			[Pure]
			[return: NotNullIfNotNull(nameof(defaultValue))]
			public static TJsonDeserializable? JsonDeserialize(ReadOnlySpan<byte> jsonBytes, TJsonDeserializable? defaultValue, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				var value = CrystalJson.Parse(jsonBytes, settings);
#if NET7_0_OR_GREATER
				return value is not JsonNull ? TJsonDeserializable.JsonDeserialize(value, resolver) : defaultValue;
#else
				// static interface dispatch needs runtime support this target lacks: go through the reflection-based dispatcher
				return value is not JsonNull ? JsonDeserializableDispatcher<TJsonDeserializable>.Invoke(value, resolver) : defaultValue;
#endif
			}

			[Pure]
			public static TJsonDeserializable?[] JsonDeserializeArrayItemOptional(Slice jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
				=> JsonDeserializeArrayItemOptional<TJsonDeserializable>(jsonBytes.Span, settings, resolver);

			[Pure]
			public static TJsonDeserializable?[] JsonDeserializeArrayItemOptional(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				var values = CrystalJson.Parse(jsonBytes, settings).AsArray();
				return JsonUnpackArrayItemOptional<TJsonDeserializable>(values, resolver);
			}

			/// <summary>Deserializes a required array of non-null elements of type <typeparamref name="TJsonDeserializable"/></summary>
			/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <param name="resolver">Resolver used to deserialize the items of the array (optional)</param>
			/// <returns>Array of <typeparamref name="TJsonDeserializable"/> elements</returns>
			/// <remarks>This method will throw if array is <c>null</c>, or if it contains <c>null</c> elements.</remarks>
			/// <seealso cref="JsonDeserializeArrayOrDefaultItemNotNull(ReadOnlySpan{byte},CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/>
			/// <seealso cref="JsonDeserializeArrayItemOptional(ReadOnlySpan{byte},CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/>
			/// <seealso cref="JsonDeserializeArrayOrDefaultItemOptional(ReadOnlySpan{byte},CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/>
			[Pure]
			public static TJsonDeserializable[] JsonDeserializeArrayItemNotNull(Slice jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
				=> JsonDeserializeArrayItemNotNull<TJsonDeserializable>(jsonBytes.Span, settings, resolver);

			/// <summary>Deserializes a required array of non-null elements of type <typeparamref name="TJsonDeserializable"/></summary>
			/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <param name="resolver">Resolver used to deserialize the items of the array (optional)</param>
			/// <returns>Array of <typeparamref name="TJsonDeserializable"/> elements</returns>
			/// <remarks>This method will throw if array is <c>null</c>, or if it contains <c>null</c> elements.</remarks>
			/// <seealso cref="JsonDeserializeArrayOrDefaultItemNotNull(ReadOnlySpan{byte},CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/>
			/// <seealso cref="JsonDeserializeArrayItemOptional(ReadOnlySpan{byte},CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/>
			/// <seealso cref="JsonDeserializeArrayOrDefaultItemOptional(ReadOnlySpan{byte},CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/>
			[Pure]
			public static TJsonDeserializable[] JsonDeserializeArrayItemNotNull(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				var values = CrystalJson.Parse(jsonBytes, settings).AsArray();
				return JsonUnpackArrayItemNotNull<TJsonDeserializable>(values, resolver);
			}

			[Pure]
			public static TJsonDeserializable?[]? JsonDeserializeArrayOrDefaultItemOptional(Slice jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
				=> JsonDeserializeArrayOrDefaultItemOptional<TJsonDeserializable>(jsonBytes.Span, settings, resolver);

			[Pure]
			public static TJsonDeserializable?[]? JsonDeserializeArrayOrDefaultItemOptional(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				var values = CrystalJson.Parse(jsonBytes, settings).AsArrayOrDefault();
				return values is not null
					? JsonUnpackArrayItemOptional<TJsonDeserializable>(values, resolver)
					: null;
			}

			[Pure]
			public static TJsonDeserializable[]? JsonDeserializeArrayOrDefaultItemNotNull(Slice jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
				=> JsonDeserializeArrayOrDefaultItemNotNull<TJsonDeserializable>(jsonBytes.Span, settings, resolver);

			[Pure]
			public static TJsonDeserializable[]? JsonDeserializeArrayOrDefaultItemNotNull(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				var values = CrystalJson.Parse(jsonBytes, settings).AsArrayOrDefault();
				return values is not null
					? JsonUnpackArrayItemNotNull<TJsonDeserializable>(values, resolver)
					: null;
			}

			/// <summary>Unpacks an optional instance of type <typeparamref name="TJsonDeserializable"/> from a <see cref="JsonValue"/></summary>
			/// <param name="value">JSON value that will be bound to the new instance</param>
			/// <param name="resolver">Resolver used to deserialize the items of the array (optional)</param>
			/// <returns>Deserialized instance, or <c>null</c> if <paramref name="value"/> is null or missing.</returns>
			/// <exception cref="JsonBindingException">If <paramref name="value"/> is not a valid representation for <typeparamref name="TJsonDeserializable"/></exception>
			[Pure]
			public static TJsonDeserializable? JsonUnpackOrDefault(JsonValue? value, ICrystalJsonTypeResolver? resolver = null)
			{
#if NET7_0_OR_GREATER
				return value is not (null or JsonNull)
					? TJsonDeserializable.JsonDeserialize(value, resolver ?? CrystalJson.DefaultResolver)
					: default;
#else
				// static interface dispatch needs runtime support this target lacks: go through the reflection-based dispatcher
				return value is not (null or JsonNull)
					? JsonDeserializableDispatcher<TJsonDeserializable>.Invoke(value, resolver ?? CrystalJson.DefaultResolver)
					: default;
#endif
			}

			/// <summary>Unpacks a required instance of type <typeparamref name="TJsonDeserializable"/> from a <see cref="JsonValue"/></summary>
			/// <param name="value">JSON value that will be bound to the new instance</param>
			/// <param name="resolver">Resolver used to deserialize the items of the array (optional)</param>
			/// <returns>Deserialized instance.</returns>
			/// <exception cref="JsonBindingException">If <paramref name="value"/> is null-or-missing, or if it is not a valid representation for <typeparamref name="TJsonDeserializable"/></exception>
			[Pure]
			public static TJsonDeserializable JsonUnpack(JsonValue value, ICrystalJsonTypeResolver? resolver = null)
			{
#if NET7_0_OR_GREATER
				return value is not (null or JsonNull)
					? TJsonDeserializable.JsonDeserialize(value, resolver ?? CrystalJson.DefaultResolver)
					: throw CrystalJson.Errors.Parsing_ValueIsNullOrMissing();
#else
				// static interface dispatch needs runtime support this target lacks: go through the reflection-based dispatcher
				return value is not (null or JsonNull)
					? JsonDeserializableDispatcher<TJsonDeserializable>.Invoke(value, resolver ?? CrystalJson.DefaultResolver)
					: throw CrystalJson.Errors.Parsing_ValueIsNullOrMissing();
#endif
			}

			/// <summary>Deserializes a required array of non-null elements of type <typeparamref name="TJsonDeserializable"/></summary>
			/// <param name="values">JSON Array to deserialize</param>
			/// <param name="resolver">Resolver used to deserialize the items of the array (optional)</param>
			/// <returns>Array of <typeparamref name="TJsonDeserializable"/> elements</returns>
			/// <remarks>This method will throw if array is <c>null</c>, or if it contains <c>null</c> elements.</remarks>
			/// <seealso cref="JsonUnpackArrayOrDefaultItemNotNull"/>
			/// <seealso cref="JsonUnpackArrayItemOptional"/>
			/// <seealso cref="JsonUnpackArrayOrDefaultItemOptional"/>
			[Pure]
			public static TJsonDeserializable[] JsonUnpackArrayItemNotNull(JsonArray values, ICrystalJsonTypeResolver? resolver = null)
			{
				Contract.NotNull(values);

				var items = values.GetSpan();
				if (items.Length == 0) return [ ];

				var tmp = new TJsonDeserializable[items.Length];
				resolver ??= CrystalJson.DefaultResolver;
				for (int i = 0; i < items.Length; i++)
				{
					var value = items[i];
					if (value is JsonNull)
					{
						throw JsonBindingException.CannotBindArrayWithNullItemToCollectionOfType(values, i, typeof(TJsonDeserializable[]));
					}
#if NET7_0_OR_GREATER
					tmp[i] = TJsonDeserializable.JsonDeserialize(value, resolver);
#else
					// static interface dispatch needs runtime support this target lacks: go through the reflection-based dispatcher
					tmp[i] = JsonDeserializableDispatcher<TJsonDeserializable>.Invoke(value, resolver);
#endif
				}
				return tmp;
			}

			/// <summary>Deserializes an optional array of non-null elements of type <typeparamref name="TJsonDeserializable"/></summary>
			/// <param name="values">JSON Array to deserialize</param>
			/// <param name="resolver">Resolver used to deserialize the items of the array (optional)</param>
			/// <returns>Array of <typeparamref name="TJsonDeserializable"/> elements, or null if it was null or missing.</returns>
			/// <remarks>This method will throw if the array contains null elements.</remarks>
			/// <seealso cref="JsonUnpackArrayItemNotNull"/>
			/// <seealso cref="JsonUnpackArrayItemOptional"/>
			/// <seealso cref="JsonUnpackArrayOrDefaultItemOptional"/>
			[Pure]
			public static TJsonDeserializable[]? JsonUnpackArrayOrDefaultItemNotNull(JsonArray? values, ICrystalJsonTypeResolver? resolver = null)
			{
				return values is not null ? JsonUnpackArrayItemNotNull<TJsonDeserializable>(values, resolver) : null;
			}

			/// <summary>Deserializes a required array of nullable elements of type <typeparamref name="TJsonDeserializable"/></summary>
			/// <param name="values">JSON Array to deserialize</param>
			/// <param name="resolver">Resolver used to deserialize the items of the array (optional)</param>
			/// <returns>Array of <typeparamref name="TJsonDeserializable"/> elements</returns>
			/// <remarks>This method will throw if the parent does not have this field, if the field is null or not an array.</remarks>
			/// <seealso cref="JsonUnpackArrayItemNotNull"/>
			/// <seealso cref="JsonUnpackArrayOrDefaultItemNotNull"/>
			/// <seealso cref="JsonUnpackArrayOrDefaultItemOptional"/>
			[Pure]
			public static TJsonDeserializable?[] JsonUnpackArrayItemOptional(JsonArray values, ICrystalJsonTypeResolver? resolver = null)
			{
				Contract.NotNull(values);

				var items = values.GetSpan();
				if (items.Length == 0) return [ ];

				var tmp = new TJsonDeserializable?[items.Length];
				resolver ??= CrystalJson.DefaultResolver;
				for (int i = 0; i < items.Length; i++)
				{
					var value = items[i];
#if NET7_0_OR_GREATER
					tmp[i] = value is not JsonNull ? TJsonDeserializable.JsonDeserialize(value, resolver) : default;
#else
					// static interface dispatch needs runtime support this target lacks: go through the reflection-based dispatcher
					tmp[i] = value is not JsonNull ? JsonDeserializableDispatcher<TJsonDeserializable>.Invoke(value, resolver) : default;
#endif
				}
				return tmp;
			}

			/// <summary>Deserializes an optional array of nullable elements of type <typeparamref name="TJsonDeserializable"/></summary>
			/// <param name="values">JSON Array to deserialize</param>
			/// <param name="resolver">Resolver used to deserialize the items of the array (optional)</param>
			/// <returns>Array of <typeparamref name="TJsonDeserializable"/> elements, or <c>null</c> if it was null or missing.</returns>
			/// <seealso cref="JsonUnpackArrayItemNotNull"/>
			/// <seealso cref="JsonUnpackArrayOrDefaultItemNotNull"/>
			/// <seealso cref="JsonUnpackArrayItemOptional"/>
			[Pure]
			public static TJsonDeserializable?[]? JsonUnpackArrayOrDefaultItemOptional(JsonArray? values, ICrystalJsonTypeResolver? resolver = null)
			{
				return values is not null ? JsonUnpackArrayItemOptional<TJsonDeserializable>(values, resolver) : null;
			}

			[Pure]
			public static List<TJsonDeserializable?> JsonUnpackListItemOptional(JsonArray values, ICrystalJsonTypeResolver? resolver = null)
			{
				Contract.NotNull(values);

				var items = values.GetSpan();
				if (items.Length == 0) return [ ];

				var res = new List<TJsonDeserializable?>();
				CollectionsMarshal.SetCount(res, items.Length);
				var tmp = CollectionsMarshal.AsSpan(res);
				resolver ??= CrystalJson.DefaultResolver;
				for (int i = 0; i < items.Length; i++)
				{
					var value = items[i];
#if NET7_0_OR_GREATER
					tmp[i] = value is not JsonNull ? TJsonDeserializable.JsonDeserialize(value, resolver) : default;
#else
					// static interface dispatch needs runtime support this target lacks: go through the reflection-based dispatcher
					tmp[i] = value is not JsonNull ? JsonDeserializableDispatcher<TJsonDeserializable>.Invoke(value, resolver) : default;
#endif
				}
				return res;
			}

			[Pure]
			public static List<TJsonDeserializable> JsonUnpackListItemNotNull(JsonArray values, ICrystalJsonTypeResolver? resolver = null)
			{
				Contract.NotNull(values);

				var items = values.GetSpan();
				if (items.Length == 0) return [ ];

				var res = new List<TJsonDeserializable>();
				CollectionsMarshal.SetCount(res, items.Length);
				var tmp = CollectionsMarshal.AsSpan(res);
				resolver ??= CrystalJson.DefaultResolver;
				for (int i = 0; i < items.Length; i++)
				{
					var value = items[i];
					if (value is JsonNull)
					{
						throw JsonBindingException.CannotBindArrayWithNullItemToCollectionOfType(values, i, typeof(List<TJsonDeserializable>));
					}
#if NET7_0_OR_GREATER
					tmp[i] = TJsonDeserializable.JsonDeserialize(value, resolver);
#else
					// static interface dispatch needs runtime support this target lacks: go through the reflection-based dispatcher
					tmp[i] = JsonDeserializableDispatcher<TJsonDeserializable>.Invoke(value, resolver);
#endif
				}
				return res;
			}

			[Pure]
			public static List<TJsonDeserializable>? JsonUnpackListOrDefaultItemNotNull(JsonArray? values, ICrystalJsonTypeResolver? resolver = null)
			{
				return values is not null ? JsonUnpackListItemNotNull<TJsonDeserializable>(values, resolver) : null;
			}

			[Pure]
			public static ImmutableArray<TJsonDeserializable?> JsonUnpackImmutableArrayItemOptional(JsonArray values, ICrystalJsonTypeResolver? resolver = null)
			{
				return ImmutableCollectionsMarshal.AsImmutableArray(JsonUnpackArrayItemOptional<TJsonDeserializable>(values, resolver));
			}

			[Pure]
			public static ImmutableArray<TJsonDeserializable?> JsonUnpackImmutableArrayOrEmptyItemOptional(JsonArray values, ICrystalJsonTypeResolver? resolver = null)
			{
				return ImmutableCollectionsMarshal.AsImmutableArray(JsonUnpackArrayOrDefaultItemOptional<TJsonDeserializable>(values, resolver) ?? [ ]);
			}

			[Pure]
			[EditorBrowsable(EditorBrowsableState.Advanced)]
			public static ImmutableArray<TJsonDeserializable> JsonUnpackImmutableArrayItemNotNull(JsonArray values, ICrystalJsonTypeResolver? resolver = null)
			{
				return ImmutableCollectionsMarshal.AsImmutableArray(JsonUnpackArrayItemNotNull<TJsonDeserializable>(values, resolver));
			}

			[Pure]
			public static ImmutableArray<TJsonDeserializable> JsonUnpackImmutableArrayOrEmptyItemNotNull(JsonArray values, ICrystalJsonTypeResolver? resolver = null)
			{
				return ImmutableCollectionsMarshal.AsImmutableArray(JsonUnpackArrayOrDefaultItemNotNull<TJsonDeserializable>(values, resolver) ?? [ ]);
			}

			#endregion

		}

		extension<TJsonSerializable>(TJsonSerializable? self)
			where TJsonSerializable : IJsonSerializable
		{

			/// <summary>Serializes a <see cref="IJsonSerializable"/> value into a text literal</summary>
			/// <remarks>Note: if the JSON has to be sent over HTTP, or stored on disk, prefer <see cref="JsonValueExtensions.ToJsonSlice(JsonValue?,CrystalJsonSettings?)"/> or <see cref="JsonValueExtensions.ToJsonBytes(JsonValue)"/> that will return the same result but already utf-8 encoded</remarks>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			[EditorBrowsable(EditorBrowsableState.Never)]
			[Obsolete("Use either ToJsonText() or ToJsonValue() instead!", error: true)] //TODO: phase out this overload ASAP!
			public string ToJson(CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
				=> ToJsonText<TJsonSerializable>(self, settings, resolver);

			/// <summary>Serializes a <see cref="IJsonSerializable"/> value into a text literal</summary>
			/// <remarks>Note: if the JSON has to be sent over HTTP, or stored on disk, prefer <see cref="JsonValueExtensions.ToJsonSlice(JsonValue?,CrystalJsonSettings?)"/> or <see cref="JsonValueExtensions.ToJsonBytes(JsonValue)"/> that will return the same result but already utf-8 encoded</remarks>
			[Pure]
			public string ToJsonText(CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				if (self is null)
				{ // special case for null instances
					return JsonTokens.Null;
				}

				var writer = CrystalJson.WriterPool.Allocate();
				try
				{
					writer.Initialize(0, settings, resolver);

					self.JsonSerialize(writer);

					return writer.GetString();
				}
				finally
				{
					writer.Dispose();
					CrystalJson.WriterPool.Free(writer);
				}
			}

			/// <summary>Serializes a <see cref="IJsonSerializable"/> into the most compact text literal possible</summary>
			/// <remarks>Note: if the JSON has to be sent over HTTP, or stored on disk, prefer <see cref="JsonValueExtensions.ToJsonSlice(JsonValue?,CrystalJsonSettings?)"/> or <see cref="JsonValueExtensions.ToJsonBytes(JsonValue)"/> that will return the same result but already utf-8 encoded</remarks>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public string ToJsonTextCompact() => ToJsonText<TJsonSerializable>(self, CrystalJsonSettings.JsonCompact, null);

			/// <summary>Serializes a <see cref="JsonValue"/> into a human-friendly indented text representation (for logging, console output, etc...)</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public string ToJsonTextIndented() => ToJsonText<TJsonSerializable>(self, CrystalJsonSettings.JsonIndented);

			/// <summary>Serializes a <see cref="IJsonSerializable"/> value into a <see cref="Slice"/>, using custom settings</summary>
			/// <param name="settings">Custom serialization settings</param>
			/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
			/// <returns><see cref="Slice"/> that contains the utf-8 encoded text representation of the JSON value</returns>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public Slice ToJsonSlice(CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
				=> JsonSerialize(self, settings, resolver);

			/// <summary>Serializes a <see cref="IJsonSerializable"/> value into a <see cref="Slice"/>, using custom settings</summary>
			/// <param name="pool">Pool used to allocate the content of the slice (use <see cref="ArrayPool{T}.Shared"/> if <see langword="null"/>)</param>
			/// <param name="settings">Custom serialization settings</param>
			/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
			/// <returns><see cref="Slice"/> that contains the utf-8 encoded text representation of the JSON value</returns>
			[Pure, MustDisposeResource, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public SliceOwner ToJsonSlice(ArrayPool<byte>? pool, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
				=> JsonSerialize(self, pool, settings, resolver);

			/// <summary>Serializes this instance as JSON</summary>
			/// <param name="writer">Writer that will output the content of this instance</param>
			/// <param name="value">Instance to serialize (can be <b>null</b>)</param>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void JsonSerialize(CrystalJsonWriter writer, in TJsonSerializable? value)
			{
				if (value is not null)
				{
					value.JsonSerialize(writer);
				}
				else
				{
					writer.WriteNull();
				}
			}

			/// <summary>Serializes a value of type <typeparamref name="TJsonSerializable"/> into a <see cref="Slice"/></summary>
			/// <param name="value">Instance to serialize (can be <b>null</b>)</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
			/// <returns><c>`123`</c>, <c>`true`</c>, <c>`"ABC"`</c>, <c>`{ "foo":..., "bar": ... }`</c>, <c>`[ ... ]`</c>, ...</returns>
			/// <exception cref="JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
			[Pure]
			public static Slice JsonSerialize(in TJsonSerializable? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				var writer = CrystalJson.WriterPool.Allocate();
				try
				{
					writer.Initialize(0, settings, resolver);

					if (value is not null)
					{
						value.JsonSerialize(writer);
					}
					else
					{
						writer.WriteNull();
					}

					return writer.GetUtf8Slice();
				}
				finally
				{
					writer.Dispose();
					CrystalJson.WriterPool.Free(writer);
				}
			}

			/// <summary>Serializes a value of type <typeparamref name="TJsonSerializable"/> into a <see cref="SliceOwner"/> using the specified <see cref="ArrayPool{T}">pool</see></summary>
			/// <param name="value">Instance to serialize (can be <b>null</b>)</param>
			/// <param name="pool">Pool used to allocate the content of the slice (use <see cref="ArrayPool{T}.Shared"/> if <see langword="null"/>)</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
			/// <returns><c>`123`</c>, <c>`true`</c>, <c>`"ABC"`</c>, <c>`{ "foo":..., "bar": ... }`</c>, <c>`[ ... ]`</c>, ...</returns>
			/// <exception cref="JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
			/// <remarks>
			/// <para>The <see cref="SliceOwner"/> returned <b>MUST</b> be disposed; otherwise, the rented buffer will not be returned to the <paramref name="pool"/>.</para>
			/// </remarks>
			[Pure, MustDisposeResource]
			public static SliceOwner JsonSerialize(in TJsonSerializable? value, ArrayPool<byte>? pool, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				var writer = CrystalJson.WriterPool.Allocate();
				try
				{
					writer.Initialize(0, settings, resolver);

					if (value is not null)
					{
						value.JsonSerialize(writer);
					}
					else
					{
						writer.WriteNull();
					}

					return writer.GetUtf8Slice(pool);
				}
				finally
				{
					writer.Dispose();
					CrystalJson.WriterPool.Free(writer);
				}
			}

			/// <summary>Serializes a sequence of values of type <typeparamref name="TJsonSerializable"/> as a JSON Array</summary>
			/// <param name="values">Sequence of instances to serialize (can be null)</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
			/// <returns><c>`[ 123, 456, 789, ... ]`</c>, <c>`[ true, true, false, ... ]`</c>, <c>`[ "ABC", "DEF", "GHI", ... ]`</c>, <c>`[ { "foo":..., "bar": ... }, { "foo":..., "bar": ... }, ... ]`</c>, <c>`[ [ ... ], [ ... ], ... ]`</c>, ...</returns>
			/// <exception cref="JsonSerializationException">If any value fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
			public static Slice JsonSerializeArray(IEnumerable<TJsonSerializable?> values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				if (values.TryGetSpan(out var span))
				{
					return JsonSerializeArray(span, settings, resolver);
				}

				return JsonSerializeEnumerableArray(values, settings, resolver);

				static Slice JsonSerializeEnumerableArray(IEnumerable<TJsonSerializable?> values, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
				{
					var writer = CrystalJson.WriterPool.Allocate();
					try
					{
						writer.Initialize(0, settings, resolver);

						var state = writer.BeginArray();

						foreach(var value in values)
						{
							writer.WriteFieldSeparator();
							if (value is not null)
							{
								value.JsonSerialize(writer);
							}
							else
							{
								writer.WriteNull();
							}
						}

						writer.EndArray(state);

						return writer.GetUtf8Slice();
					}
					finally
					{
						writer.Dispose();
						CrystalJson.WriterPool.Free(writer);
					}
				}
			}

			/// <summary>Serializes a span of values of type <typeparamref name="TJsonSerializable"/> as a JSON Array</summary>
			/// <param name="values">Immutable array of instances to serialize</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
			/// <returns><c>`[ 123, 456, 789, ... ]`</c>, <c>`[ true, true, false, ... ]`</c>, <c>`[ "ABC", "DEF", "GHI", ... ]`</c>, <c>`[ { "foo":..., "bar": ... }, { "foo":..., "bar": ... }, ... ]`</c>, <c>`[ [ ... ], [ ... ], ... ]`</c>, ...</returns>
			/// <exception cref="JsonSerializationException">If any value fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
			public static Slice JsonSerializeArray(ImmutableArray<TJsonSerializable?> values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
				=> JsonSerializeArray(values.AsSpan(), settings, resolver);

			/// <summary>Serializes a span of values of type <typeparamref name="TJsonSerializable"/> as a JSON Array</summary>
			/// <param name="values">Span of instances to serialize</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
			/// <returns><c>`[ 123, 456, 789, ... ]`</c>, <c>`[ true, true, false, ... ]`</c>, <c>`[ "ABC", "DEF", "GHI", ... ]`</c>, <c>`[ { "foo":..., "bar": ... }, { "foo":..., "bar": ... }, ... ]`</c>, <c>`[ [ ... ], [ ... ], ... ]`</c>, ...</returns>
			/// <exception cref="JsonSerializationException">If any value fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
			public static Slice JsonSerializeArray(ReadOnlySpan<TJsonSerializable?> values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				var writer = CrystalJson.WriterPool.Allocate();
				try
				{
					writer.Initialize(0, settings, resolver);

					if (values.Length == 0)
					{
						writer.WriteEmptyArray();
					}
					else
					{
						var state = writer.BeginArray();

						for (int i = 0; i < values.Length; i++)
						{
							writer.WriteFieldSeparator();
							ref readonly TJsonSerializable? value = ref values[i];
							if (value is not null)
							{
								value.JsonSerialize(writer);
							}
							else
							{
								writer.WriteNull();
							}
						}

						writer.EndArray(state);
					}

					return writer.GetUtf8Slice();
				}
				finally
				{
					writer.Dispose();
					CrystalJson.WriterPool.Free(writer);
				}
			}

			/// <summary>Serializes a sequence of values of type <typeparamref name="TJsonSerializable"/> as a JSON Array into a <see cref="SliceOwner"/> using the specified <see cref="ArrayPool{T}">pool</see></summary>
			/// <param name="values">Span of the instances to serialize (can be <c>null</c>)</param>
			/// <param name="pool">Pool used to allocate the content of the slice (use <see cref="ArrayPool{T}.Shared"/> if <see langword="null"/>)</param>
			/// <param name="settings">Serialization settings (use default JSON settings if <c>null</c>)</param>
			/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
			/// <returns><c>`[ 123, 456, 789, ... ]`</c>, <c>`[ true, true, false, ... ]`</c>, <c>`[ "ABC", "DEF", "GHI", ... ]`</c>, <c>`[ { "foo":..., "bar": ... }, { "foo":..., "bar": ... }, ... ]`</c>, <c>`[ [ ... ], [ ... ], ... ]`</c>, ...</returns>
			/// <exception cref="JsonSerializationException">If any value fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
			/// <remarks>
			/// <para>The <see cref="SliceOwner"/> returned <b>MUST</b> be disposed; otherwise, the rented buffer will not be returned to the <paramref name="pool"/>.</para>
			/// </remarks>
			public static SliceOwner JsonSerializeArray(IEnumerable<TJsonSerializable?> values, ArrayPool<byte>? pool, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				if (values.TryGetSpan(out var span))
				{
					return JsonSerializeArray(span, pool, settings, resolver);
				}

				return JsonSerializeEnumerableArray(values, pool, settings, resolver);

				static SliceOwner JsonSerializeEnumerableArray(IEnumerable<TJsonSerializable?> values, ArrayPool<byte>? pool, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
				{
					var writer = CrystalJson.WriterPool.Allocate();
					try
					{
						writer.Initialize(0, settings, resolver);

						var state = writer.BeginArray();

						foreach(var value in values)
						{
							writer.WriteFieldSeparator();
							if (value is not null)
							{
								value.JsonSerialize(writer);
							}
							else
							{
								writer.WriteNull();
							}
						}

						writer.EndArray(state);

						return writer.GetUtf8Slice(pool);
					}
					finally
					{
						writer.Dispose();
						CrystalJson.WriterPool.Free(writer);
					}
				}
			}

			/// <summary>Serializes a span of values of type <typeparamref name="TJsonSerializable"/> as a JSON Array into a <see cref="SliceOwner"/> using the specified <see cref="ArrayPool{T}">pool</see></summary>
			/// <param name="values">Immutable array the instances to serialize</param>
			/// <param name="pool">Pool used to allocate the content of the slice (use <see cref="ArrayPool{T}.Shared"/> if <see langword="null"/>)</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
			/// <returns><c>`[ 123, 456, 789, ... ]`</c>, <c>`[ true, true, false, ... ]`</c>, <c>`[ "ABC", "DEF", "GHI", ... ]`</c>, <c>`[ { "foo":..., "bar": ... }, { "foo":..., "bar": ... }, ... ]`</c>, <c>`[ [ ... ], [ ... ], ... ]`</c>, ...</returns>
			/// <exception cref="JsonSerializationException">If any value fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
			/// <remarks>
			/// <para>The <see cref="SliceOwner"/> returned <b>MUST</b> be disposed; otherwise, the rented buffer will not be returned to the <paramref name="pool"/>.</para>
			/// </remarks>
			public static SliceOwner JsonSerializeArray(ImmutableArray<TJsonSerializable?> values, ArrayPool<byte>? pool, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
				=> JsonSerializeArray(values.AsSpan(), pool, settings, resolver);

			/// <summary>Serializes a span of values of type <typeparamref name="TJsonSerializable"/> as a JSON Array into a <see cref="SliceOwner"/> using the specified <see cref="ArrayPool{T}">pool</see></summary>
			/// <param name="values">Span of instances to serialize</param>
			/// <param name="pool">Pool used to allocate the content of the slice (use <see cref="ArrayPool{T}.Shared"/> if <see langword="null"/>)</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
			/// <returns><c>`[ 123, 456, 789, ... ]`</c>, <c>`[ true, true, false, ... ]`</c>, <c>`[ "ABC", "DEF", "GHI", ... ]`</c>, <c>`[ { "foo":..., "bar": ... }, { "foo":..., "bar": ... }, ... ]`</c>, <c>`[ [ ... ], [ ... ], ... ]`</c>, ...</returns>
			/// <exception cref="JsonSerializationException">If any value fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
			/// <remarks>
			/// <para>The <see cref="SliceOwner"/> returned <b>MUST</b> be disposed; otherwise, the rented buffer will not be returned to the <paramref name="pool"/>.</para>
			/// </remarks>
			public static SliceOwner JsonSerializeArray(ReadOnlySpan<TJsonSerializable?> values, ArrayPool<byte>? pool, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				var writer = CrystalJson.WriterPool.Allocate();
				try
				{
					writer.Initialize(0, settings, resolver);

					if (values.Length == 0)
					{
						writer.WriteEmptyArray();
					}
					else
					{
						var state = writer.BeginArray();

						for (int i = 0; i < values.Length; i++)
						{
							writer.WriteFieldSeparator();
							ref readonly TJsonSerializable? value = ref values[i];
							if (value is not null)
							{
								value.JsonSerialize(writer);
							}
							else
							{
								writer.WriteNull();
							}
						}

						writer.EndArray(state);
					}

					return writer.GetUtf8Slice(pool);
				}
				finally
				{
					writer.Dispose();
					CrystalJson.WriterPool.Free(writer);
				}
			}

		}

	}

}
