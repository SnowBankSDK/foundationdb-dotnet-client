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

	public partial class JsonValue
	{

		/// <summary>Operations for <b>read-only</b> JSON values</summary>
		[SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
		[PublicAPI]
		public static class ReadOnly
		{

			#region Parse...

			/// <summary>Parses a JSON text literal, and returns the corresponding JSON value</summary>
			/// <param name="jsonText">JSON text document to parse</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <returns>Corresponding JSON value. If <paramref name="jsonText"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
			/// <remarks>
			/// <para>The value will be immutable and cannot be modified. If you require a mutable value, please use <see cref="Parse(string?,SnowBank.Data.Json.CrystalJsonSettings?)"/> instead.</para>
			/// <para>If the result is always expected to be an Array or an Object, please call either <see cref="ParseArray(string?,SnowBank.Data.Json.CrystalJsonSettings?)"/> or <see cref="ParseObject(string?,SnowBank.Data.Json.CrystalJsonSettings?)"/>.</para>
			/// </remarks>
			/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
			[Pure]
			public static JsonValue Parse(
#if NET8_0_OR_GREATER
				[System.Diagnostics.CodeAnalysis.StringSyntax(StringSyntaxAttribute.Json)]
#endif
				string? jsonText,
				CrystalJsonSettings? settings = null
			)
			{
				return CrystalJson.Parse(jsonText, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly);
			}

			/// <summary>Parses a JSON text literal, and returns the corresponding JSON value</summary>
			/// <param name="jsonText">JSON text document to parse</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <returns>Corresponding JSON value. If <paramref name="jsonText"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
			/// <remarks>
			/// <para>The value will be immutable and cannot be modified. If you require a mutable value, please use <see cref="Parse(string?,SnowBank.Data.Json.CrystalJsonSettings?)"/> instead.</para>
			/// <para>If the result is always expected to be an Array or an Object, please call either <see cref="JsonValue.ReadOnly.ParseArray(string?,SnowBank.Data.Json.CrystalJsonSettings?)"/> or <see cref="JsonValue.ReadOnly.ParseObject(string?,SnowBank.Data.Json.CrystalJsonSettings?)"/>.</para>
			/// </remarks>
			/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
			[Pure]
			public static JsonValue Parse(
#if NET8_0_OR_GREATER
				[System.Diagnostics.CodeAnalysis.StringSyntax(StringSyntaxAttribute.Json)]
#endif
				ReadOnlySpan<char> jsonText,
				CrystalJsonSettings? settings = null
			)
			{
				return CrystalJson.Parse(jsonText, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly);
			}

			/// <summary>Parses a JSON text literal that is expected to contain an Array, as a read-only JSON array</summary>
			/// <param name="jsonText">JSON text document to parse</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <returns>Corresponding immutable JSON Array. If <paramref name="jsonText"/> is empty or not an Array, an exception will be thrown instead.</returns>
			/// <remarks>
			/// <para>The JSON Array is immutable and cannot be modified. If you require a mutable array, please use <see cref="ParseArray(string?,SnowBank.Data.Json.CrystalJsonSettings?)"/> instead.</para>
			/// <para>If the JSON document can sometimes be empty of the '<c>null</c>' token, you should call <see cref="Parse(string?,SnowBank.Data.Json.CrystalJsonSettings?)"/> and then use <see cref="JsonValueExtensions.AsArrayOrDefault"/> on the result.</para>
			/// </remarks>
			/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
			/// <exception cref="InvalidOperationException">If the JSON document was empty, or the '<c>null</c>' token, or not an Array.</exception>
			[Pure]
			public static JsonArray ParseArray(
#if NET8_0_OR_GREATER
				[System.Diagnostics.CodeAnalysis.StringSyntax(StringSyntaxAttribute.Json)]
#endif
				string? jsonText,
				CrystalJsonSettings? settings = null
			)
			{
				return CrystalJson.Parse(jsonText, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly).AsArray();
			}

			/// <summary>Parses a JSON text literal that is expected to contain an Array, as a read-only JSON array</summary>
			/// <param name="jsonText">JSON text document to parse</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <returns>Corresponding immutable JSON Array. If <paramref name="jsonText"/> is empty or not an Array, an exception will be thrown instead.</returns>
			/// <remarks>
			/// <para>The JSON Array is immutable and cannot be modified. If you require a mutable array, please use <see cref="ParseArray(string?,SnowBank.Data.Json.CrystalJsonSettings?)"/> instead.</para>
			/// <para>If the JSON document can sometimes be empty of the '<c>null</c>' token, you should call <see cref="Parse(string?,SnowBank.Data.Json.CrystalJsonSettings?)"/> and then use <see cref="JsonValueExtensions.AsArrayOrDefault"/> on the result.</para>
			/// </remarks>
			/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
			/// <exception cref="InvalidOperationException">If the JSON document was empty, or the '<c>null</c>' token, or not an Array.</exception>
			[Pure]
			public static JsonArray ParseArray(
#if NET8_0_OR_GREATER
				[System.Diagnostics.CodeAnalysis.StringSyntax(StringSyntaxAttribute.Json)]
#endif
				ReadOnlySpan<char> jsonText,
				CrystalJsonSettings? settings = null
			)
			{
				return CrystalJson.Parse(jsonText, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly).AsArray();
			}

			/// <summary>Parse a string literal containing a JSON Object</summary>
			/// <param name="jsonText">Input text to parse</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <returns>Corresponding JSON Object. If <paramref name="jsonText"/> is empty or not an object, an exception will be thrown instead.</returns>
			/// <remarks>The JSON object that is returned is immutable, and is safe for use as a singleton, a cached document, or for multithreaded operations. If you require a mutable version, please call <see cref="ParseObject(string?,SnowBank.Data.Json.CrystalJsonSettings?)"/></remarks>
			/// <exception cref="FormatException">If there is a syntax error while parsing the JSON document</exception>
			/// <exception cref="InvalidOperationException">If the text is empty or equal to <c>"null"</c>.</exception>
			[Pure]
			public static JsonObject ParseObject(
#if NET8_0_OR_GREATER
				[System.Diagnostics.CodeAnalysis.StringSyntax(StringSyntaxAttribute.Json)]
#endif
				string? jsonText,
				CrystalJsonSettings? settings = null
			)
			{
				return CrystalJson.Parse(jsonText, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly).AsObject();
			}

			/// <summary>Parse a string literal containing a JSON Object</summary>
			/// <param name="jsonText">Input text to parse</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <returns>Corresponding JSON Object. If <paramref name="jsonText"/> is empty or not an object, an exception will be thrown instead.</returns>
			/// <remarks>The JSON object that is returned is immutable, and is safe for use as a singleton, a cached document, or for multithreaded operations. If you require a mutable version, please call <see cref="ParseObject(string?,SnowBank.Data.Json.CrystalJsonSettings?)"/></remarks>
			/// <exception cref="FormatException">If there is a syntax error while parsing the JSON document</exception>
			/// <exception cref="InvalidOperationException">If the text is empty or equal to <c>"null"</c>.</exception>
			[Pure]
			public static JsonObject ParseObject(
#if NET8_0_OR_GREATER
				[System.Diagnostics.CodeAnalysis.StringSyntax(StringSyntaxAttribute.Json)]
#endif
				ReadOnlySpan<char> jsonText,
				CrystalJsonSettings? settings = null
			)
			{
				return CrystalJson.Parse(jsonText, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly).AsObject();
			}

			/// <summary>Parses a buffer containing a document</summary>
			/// <param name="jsonBytes">UTF-8 encoded bytes</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <returns>Corresponding read-only JSON value. If <paramref name="jsonBytes"/> is null or empty, will return <see cref="JsonNull.Missing"/></returns>
			[Pure]
			public static JsonValue Parse(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null)
			{
				return CrystalJson.Parse(jsonBytes, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly);
			}

			/// <summary>Parses a buffer containing a document that is expected to be an Array</summary>
			/// <param name="jsonBytes">UTF-8 encoded bytes</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <returns>Corresponding JSON Array. If <paramref name="jsonBytes"/> is empty or not an Array, an exception will be thrown</returns>
			/// <remarks>
			/// <para>The resulting Array is immutable and cannot be modified. If you require a mutable array, please use <see cref="ParseArray(string?,SnowBank.Data.Json.CrystalJsonSettings?)"/> instead.</para>
			/// <para>If the JSON document can sometimes be empty of the 'null' token, you should call <see cref="Parse(string?,SnowBank.Data.Json.CrystalJsonSettings?)"/> and then use <see cref="JsonValueExtensions.AsArrayOrDefault"/> on the result.</para>
			/// </remarks>
			/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
			/// <exception cref="InvalidOperationException">If the JSON document was empty, or the 'null' token, or not an Array.</exception>
			[Pure]
			public static JsonArray ParseArray(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null)
			{
				return CrystalJson.Parse(jsonBytes, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly).AsArray();
			}

			/// <summary>Parses a buffer containing a document that is expected to be an Object</summary>
			/// <param name="jsonBytes">UTF-8 encoded bytes</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <returns>Corresponding immutable JSON Object. If <paramref name="jsonBytes"/> is empty or not an Array, an exception will be thrown</returns>
			/// <remarks>
			/// <para>The resulting object is immutable and cannot be modified. If you require a mutable array, please use <see cref="ParseObject(System.ReadOnlySpan{byte},SnowBank.Data.Json.CrystalJsonSettings?)"/> instead.</para>
			/// </remarks>
			/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
			/// <exception cref="InvalidOperationException">If the JSON document was empty, or the 'null' token, or not an Object.</exception>
			[Pure]
			public static JsonObject ParseObject(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null)
			{
				return CrystalJson.Parse(jsonBytes, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly).AsObject();
			}

			/// <summary>Parses a buffer containing a document</summary>
			[Pure]
			public static JsonValue Parse(Slice jsonBytes, CrystalJsonSettings? settings = null)
			{
				return CrystalJson.Parse(jsonBytes, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly);
			}

			/// <summary>Parses a buffer containing a UTF-8 encoded JSON Array</summary>
			/// <param name="jsonBytes">UTF-8 encoded bytes</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <returns>Corresponding readonly JSON Array. If <paramref name="jsonBytes"/> is null or empty, will return <see cref="JsonNull.Missing"/></returns>
			/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
			[Pure]
			public static JsonArray ParseArray(Slice jsonBytes, CrystalJsonSettings? settings = null)
			{
				return CrystalJson.Parse(jsonBytes, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly).AsArray();
			}

			/// <summary>Parses a buffer containing a UTF-8 encoded JSON Object</summary>
			/// <param name="jsonBytes">UTF-8 encoded bytes</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <returns>Corresponding readonly JSON Object. If <paramref name="jsonBytes"/> is null or empty, will return <see cref="JsonNull.Missing"/></returns>
			/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
			[Pure]
			public static JsonObject ParseObject(Slice jsonBytes, CrystalJsonSettings? settings = null)
			{
				return CrystalJson.Parse(jsonBytes, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly).AsObject();
			}

			#endregion

			#region FromValue...

			/// <summary>Converts a boxed instance into a read-only JSON value</summary>
			/// <param name="value">Instance to convert (primitive, class, struct, array, ...)</param>
			/// <returns>Corresponding JSON value(JsonNumber, JsonObject, JsonArray, ...), or <see cref="JsonNull.Null"/> if <paramref name="value"/> is <c>null</c></returns>
			/// <remarks>Consider using <see cref="FromValue{T}(T)"/> instead, if the type is known at compile time, in order to reduce runtime overhead.</remarks>
			public static JsonValue FromValue(object? value)
			{
				if (value is null) return JsonNull.Null;
				if (value is JsonValue jv) return jv;
				var type = value.GetType();
				//TODO: PERF: Pooling?
				return CrystalJsonDomWriter.DefaultReadOnly.ParseObject(value, type, type);
			}

			/// <summary>Converts a boxed instance into a read-only JSON value</summary>
			/// <param name="value">Instance to convert (primitive, class, struct, array, ...)</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
			/// <returns>Corresponding JSON value(JsonNumber, JsonObject, JsonArray, ...), or <see cref="JsonNull.Null"/> if <paramref name="value"/> is <c>null</c></returns>
			/// <remarks>Consider using <see cref="FromValue{T}(T)"/> instead, if the type is known at compile time, in order to reduce runtime overhead.</remarks>
			public static JsonValue FromValue(object? value, CrystalJsonSettings settings, ICrystalJsonTypeResolver? resolver = null)
			{
				if (value is null) return JsonNull.Null;
				if (value is JsonValue jv) return jv;
				var type = value.GetType();
				return CrystalJsonDomWriter.CreateReadOnly(settings, resolver).ParseObject(value, type, type);
			}

			/// <summary>Converts a boxed instance into a read-only JSON value</summary>
			/// <param name="value">Instance to convert (primitive, class, struct, array, ...)</param>
			/// <param name="declaredType">Type of the field in the parent container that points to this value (can be a base class or interface)</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
			/// <returns>Corresponding JSON value(JsonNumber, JsonObject, JsonArray, ...), or <see cref="JsonNull.Null"/> if <paramref name="value"/> is <c>null</c></returns>
			/// <remarks>Consider using <see cref="FromValue{T}(T)"/> instead, if the type is known at compile time, in order to reduce runtime overhead.</remarks>
			[Pure]
			public static JsonValue FromValue(object? value, Type declaredType, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				return value is not null
					? CrystalJsonDomWriter.CreateReadOnly(settings, resolver).ParseObject(value, declaredType, value.GetType())
					: JsonNull.Null;
			}

			/// <summary>Converts a typed value into a read-only JSON value</summary>
			/// <typeparam name="T">Type of the value (can be base class or interface)</typeparam>
			/// <param name="value">Instance to convert</param>
			/// <returns>Corresponding JSON value(JsonNumber, JsonObject, JsonArray, ...), or <see cref="JsonNull.Null"/> if <paramref name="value"/> is <c>null</c></returns>
			[Pure]
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static JsonValue FromValue<T>(T? value)
			{
				#region <JIT_HACK>
#if !DEBUG
				if (typeof (T) == typeof (bool)) return JsonBoolean.Return((bool) (object) value!);
				if (typeof (T) == typeof (char)) return JsonString.Return((char) (object) value!);
				if (typeof (T) == typeof (byte)) return JsonNumber.Return((byte) (object) value!);
				if (typeof (T) == typeof (sbyte)) return JsonNumber.Return((sbyte) (object) value!);
				if (typeof (T) == typeof (short)) return JsonNumber.Return((short) (object) value!);
				if (typeof (T) == typeof (ushort)) return JsonNumber.Return((ushort) (object) value!);
				if (typeof (T) == typeof (int)) return JsonNumber.Return((int) (object) value!);
				if (typeof (T) == typeof (uint)) return JsonNumber.Return((uint) (object) value!);
				if (typeof (T) == typeof (long)) return JsonNumber.Return((long) (object) value!);
				if (typeof (T) == typeof (ulong)) return JsonNumber.Return((ulong) (object) value!);
				if (typeof (T) == typeof (float)) return JsonNumber.Return((float) (object) value!);
				if (typeof (T) == typeof (double)) return JsonNumber.Return((double) (object) value!);
				if (typeof (T) == typeof (decimal)) return JsonNumber.Return((decimal) (object) value!);
				if (typeof (T) == typeof (Guid)) return JsonString.Return((Guid) (object) value!);
				if (typeof (T) == typeof (Uuid128)) return JsonString.Return((Uuid128) (object) value!);
				if (typeof (T) == typeof (Uuid96)) return JsonString.Return((Uuid96) (object) value!);
				if (typeof (T) == typeof (Uuid80)) return JsonString.Return((Uuid80) (object) value!);
				if (typeof (T) == typeof (Uuid64)) return JsonString.Return((Uuid64) (object) value!);
				if (typeof (T) == typeof (TimeSpan)) return JsonNumber.Return((TimeSpan) (object) value!);
				if (typeof (T) == typeof (DateTime)) return JsonDateTime.Return((DateTime) (object) value!);
				if (typeof (T) == typeof (DateTimeOffset)) return JsonDateTime.Return((DateTimeOffset) (object) value!);
				if (typeof (T) == typeof (NodaTime.Instant)) return JsonString.Return((NodaTime.Instant) (object) value!);
				if (typeof (T) == typeof (NodaTime.Duration)) return JsonNumber.Return((NodaTime.Duration) (object) value!);
				if (typeof (T) == typeof (NodaTime.LocalDateTime)) return JsonString.Return((NodaTime.LocalDateTime) (object) value!);
				if (typeof (T) == typeof (NodaTime.LocalDate)) return JsonString.Return((NodaTime.LocalDate) (object) value!);
				if (typeof (T) == typeof (NodaTime.LocalTime)) return JsonString.Return((NodaTime.LocalTime) (object) value!);
				if (typeof (T) == typeof (NodaTime.ZonedDateTime)) return JsonString.Return((NodaTime.ZonedDateTime) (object) value!);
				// nullable types
				if (typeof (T) == typeof (bool?)) return JsonBoolean.Return((bool?) (object?) value);
				if (typeof (T) == typeof (char?)) return JsonString.Return((char?) (object?) value);
				if (typeof (T) == typeof (byte?)) return JsonNumber.Return((byte?) (object?) value);
				if (typeof (T) == typeof (sbyte?)) return JsonNumber.Return((sbyte?) (object?) value);
				if (typeof (T) == typeof (short?)) return JsonNumber.Return((short?) (object?) value);
				if (typeof (T) == typeof (ushort?)) return JsonNumber.Return((ushort?) (object?) value);
				if (typeof (T) == typeof (int?)) return JsonNumber.Return((int?) (object?) value);
				if (typeof (T) == typeof (uint?)) return JsonNumber.Return((uint?) (object?) value);
				if (typeof (T) == typeof (long?)) return JsonNumber.Return((long?) (object?) value);
				if (typeof (T) == typeof (ulong?)) return JsonNumber.Return((ulong?) (object?) value);
				if (typeof (T) == typeof (float?)) return JsonNumber.Return((float?) (object?) value);
				if (typeof (T) == typeof (double?)) return JsonNumber.Return((double?) (object?) value);
				if (typeof (T) == typeof (decimal?)) return JsonNumber.Return((decimal?) (object?) value);
				if (typeof (T) == typeof (Guid?)) return JsonString.Return((Guid?) (object?) value);
				if (typeof (T) == typeof (Uuid128?)) return JsonString.Return((Uuid128?) (object?) value);
				if (typeof (T) == typeof (Uuid96?)) return JsonString.Return((Uuid96?) (object?) value);
				if (typeof (T) == typeof (Uuid80?)) return JsonString.Return((Uuid80?) (object?) value);
				if (typeof (T) == typeof (Uuid64?)) return JsonString.Return((Uuid64?) (object?) value);
				if (typeof (T) == typeof (TimeSpan?)) return JsonNumber.Return((TimeSpan?) (object?) value);
				if (typeof (T) == typeof (DateTime?)) return JsonDateTime.Return((DateTime?) (object?) value);
				if (typeof (T) == typeof (DateTimeOffset?)) return JsonDateTime.Return((DateTimeOffset?) (object?) value);
				if (typeof (T) == typeof (NodaTime.Instant?)) return JsonString.Return((NodaTime.Instant?) (object?) value);
				if (typeof (T) == typeof (NodaTime.Duration?)) return JsonNumber.Return((NodaTime.Duration?) (object?) value);
				if (typeof (T) == typeof (NodaTime.LocalDateTime?)) return JsonString.Return((NodaTime.LocalDateTime?) (object?) value);
				if (typeof (T) == typeof (NodaTime.LocalDate?)) return JsonString.Return((NodaTime.LocalDate?) (object?) value);
				if (typeof (T) == typeof (NodaTime.LocalTime?)) return JsonString.Return((NodaTime.LocalTime?) (object?) value);
				if (typeof (T) == typeof (NodaTime.ZonedDateTime?)) return JsonString.Return((NodaTime.ZonedDateTime?) (object?) value);
#endif
				#endregion </JIT_HACK>

				return CrystalJsonDomWriter.DefaultReadOnly.ParseObject(value, typeof(T));
			}

			/// <summary>Converts a typed value into a read-only JSON value</summary>
			/// <typeparam name="T">Type of the value (can be base class or interface)</typeparam>
			/// <param name="value">Instance to convert</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
			/// <returns>Corresponding JSON value(JsonNumber, JsonObject, JsonArray, ...), or <see cref="JsonNull.Null"/> if <paramref name="value"/> is <c>null</c></returns>
			[Pure]
			public static JsonValue FromValue<T>(T? value, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver = null)
			{
				if (value is null) return JsonNull.Null;
				settings ??= CrystalJsonSettings.JsonReadOnly;

				if (resolver is not null && !ReferenceEquals(resolver, CrystalJson.DefaultResolver))
				{
					if (resolver.TryGetConverterFor<T>(out var converter))
					{
						return converter.Pack(value, settings, resolver);
					}
				}
				return CrystalJsonDomWriter.CreateReadOnly(settings, resolver).ParseObject<T>(value);
			}

			#endregion

		}

	}

}
