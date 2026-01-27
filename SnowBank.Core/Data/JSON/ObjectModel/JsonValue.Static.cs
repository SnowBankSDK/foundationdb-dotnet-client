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
	using System.ComponentModel;
	using System.Globalization;
	using SnowBank.Runtime;
	using SnowBank.Text;

	public abstract partial class JsonValue
	{

		public static object BindValueType<T>(T value, Type type, ICrystalJsonTypeResolver resolver)
			where T : struct, IConvertible
		{
			if (type == typeof(T)) return value;
			if (type.IsPrimitive) return value.ToType(type, null);
			if (type.IsEnum) return Convert.ChangeType(value, type);
			// nullable ??
			var nullableType = Nullable.GetUnderlyingType(type);
			if (nullableType is not null)
			{ // Nullable<T> => retry with the underlying value type
				// note: we will not reach here if the value is null, so we are guaranteed that it is "proper" value
				return BindValueType(value, nullableType, resolver);
			}
			throw Errors.CannotBindJsonValue(nameof(type), typeof(T), type);
		}

		internal static object? BindNative<TJson, TNative>(TJson? jsonValue, TNative nativeValue, Type? type, ICrystalJsonTypeResolver? resolver = null)
			where TJson : JsonValue
		{
			// Note: Since TNative is a ValueType, we will have many different JITed versions of this method in memory, one for each value type, which may cost a lost of first-time initialization cost?
			// in early .NET Framework versions, the JIT seemed to be in O(N^2) with the number of generic types for the same call site, not sure if this is still an issue with modern .NET Core ?

			if (jsonValue is null)
			{
				return null;
			}

			if (type is null || type == typeof(object))
			{
				return jsonValue.ToObject();
			}

			// short circuit...
			if (type == typeof(TNative))
			{
				return nativeValue;
			}

			if (type.IsPrimitive)
			{
				// Note: some base types like decimal, DateTime or TimeSpan are not considered "primitive" types and are handled elsewhere
				switch (System.Type.GetTypeCode(type))
				{
					case TypeCode.Boolean: return jsonValue.ToBoolean();
					case TypeCode.Char: return jsonValue.ToChar();
					case TypeCode.SByte: return jsonValue.ToSByte();
					case TypeCode.Byte: return jsonValue.ToByte();
					case TypeCode.Int16: return jsonValue.ToInt16();
					case TypeCode.UInt16: return jsonValue.ToUInt16();
					case TypeCode.Int32: return jsonValue.ToInt32();
					case TypeCode.UInt32: return jsonValue.ToUInt32();
					case TypeCode.Int64: return jsonValue.ToInt64();
					case TypeCode.UInt64: return jsonValue.ToUInt64();
					case TypeCode.Single: return jsonValue.ToSingle();
					case TypeCode.Double: return jsonValue.ToDouble();
					case TypeCode.Object:
					{
						if (type == typeof(IntPtr)) return new IntPtr(jsonValue.ToInt64());
						break;
					}
				}
			}

			if (type == typeof(string)) return jsonValue.ToString();
			if (type == typeof(DateTime)) return jsonValue.ToDateTime();
			if (type == typeof(TimeSpan)) return jsonValue.ToTimeSpan();
			if (type == typeof(Guid)) return jsonValue.ToGuid();
			if (type == typeof(decimal)) return jsonValue.ToDecimal();
			if (type == typeof(Half)) return jsonValue.ToHalf();
			if (type == typeof(DateOnly)) return jsonValue.ToDateOnly();
			if (type == typeof(TimeOnly)) return jsonValue.ToTimeOnly();
			if (type == typeof(JsonValue)) return jsonValue;
			if (type == typeof(NodaTime.Instant)) return jsonValue.ToInstant();
			if (type == typeof(NodaTime.Duration)) return jsonValue.ToDuration();

			if (type.IsEnum)
			{ // Enumeration
				// first convert into int, since decimal=>enum is not supported...
				// note: not all enums use Int32 as a backing type, so we have to first convert into their UnderlyingType (recursively)
				return Enum.ToObject(type, jsonValue.Bind(type.GetEnumUnderlyingType(), resolver)!);
			}

			var nullableType = Nullable.GetUnderlyingType(type);
			if (nullableType is not null)
			{ // if this is a JsonNumber then we already know we are not null, so we handle Nullable<T> just like a regular non-nullable T

				// bind to the non-nullable type (ex: Int32 for Nullable<Int32>)
				return jsonValue.Bind(nullableType, resolver);
			}

			// last resort for types that implement IConvertible
			if (nativeValue is IConvertible)
			{
				return Convert.ChangeType(nativeValue, type, NumberFormatInfo.InvariantInfo);
			}

			// there is no known conversion path for this type
			throw JsonValue.Errors.CannotBindJsonValue(nameof(type), typeof(TNative), type);
		}

		private static class Errors
		{

			[Pure]
			internal static Exception CannotBindJsonValue([InvokerParameterName] string paramName, Type sourceType, Type targetType)
			{
				return ThrowHelper.ArgumentException(paramName, $"Cannot convert JSON value from type {sourceType.GetFriendlyName()} to type {targetType.GetFriendlyName()}");
			}

			[Pure]
			internal static Exception JsonConversionNotSupported(JsonValue source, Type targetType)
			{
				return new JsonBindingException($"Cannot convert a {source.GetType().GetFriendlyName()} into a value of type {targetType.GetFriendlyName()}", source);
			}
		}

		#region ParseXYZ(...)

		// Text/Binary => JsonValue

		/// <summary>Parses a JSON text literal, and returns the corresponding JSON value</summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonText"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please use <see cref="JsonValue.ReadOnly.Parse(string?,CrystalJsonSettings?)"/> instead.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call either <see cref="JsonArray.Parse(string?,CrystalJsonSettings?)"/> or <see cref="JsonObject.Parse(string?,CrystalJsonSettings?)"/>.</para>
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
			return CrystalJson.Parse(jsonText, settings);
		}

		[Obsolete("Use JsonValue.ReadOnly.Parse(...) instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static JsonValue ParseReadOnly(
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
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please use <see cref="JsonValue.ReadOnly.Parse(string?,CrystalJsonSettings?)"/> instead.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call either <see cref="JsonArray.Parse(string?,CrystalJsonSettings?)"/> or <see cref="JsonObject.Parse(string?,CrystalJsonSettings?)"/>.</para>
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
			return CrystalJson.Parse(jsonText, settings);
		}

		[Obsolete("Use JsonValue.ReadOnly.Parse(...) instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static JsonValue ParseReadOnly(
#if NET8_0_OR_GREATER
			[System.Diagnostics.CodeAnalysis.StringSyntax(StringSyntaxAttribute.Json)]
#endif
			ReadOnlySpan<char> jsonText,
			CrystalJsonSettings? settings = null
		)
		{
			return CrystalJson.Parse(jsonText, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly);
		}

		/// <summary>Parses a JSON text literal, and returns the corresponding JSON value if it is valid.</summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="result">Corresponding JSON value. If <paramref name="jsonText"/> is empty, will return <see cref="JsonNull.Missing"/></param>
		/// <returns><c>true</c> if <paramref name="jsonText"/> contained valid JSON; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		[Pure]
		public static bool TryParse([NotNullWhen(true)] string? jsonText, CrystalJsonSettings? settings, [MaybeNullWhen(false)] out JsonValue result)
		{
			try
			{
				result = CrystalJson.Parse(jsonText, settings);
				return true;
			}
			catch
			{
				result = null;
				return false;
			}
		}

		/// <summary>Parses a JSON text literal, and returns the corresponding JSON value if it is valid.</summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="result">Corresponding JSON value. If <paramref name="jsonText"/> is empty, will return <see cref="JsonNull.Missing"/></param>
		/// <param name="error">If the parsing fails, receives the exception that represents the issue</param>
		/// <returns><c>true</c> if <paramref name="jsonText"/> contained valid JSON; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		[Pure]
		public static bool TryParse([NotNullWhen(true)] string? jsonText, CrystalJsonSettings? settings, [MaybeNullWhen(false)] out JsonValue result, out Exception? error)
		{
			try
			{
				result = CrystalJson.Parse(jsonText, settings);
				error = null;
				return true;
			}
			catch(Exception e)
			{
				result = null;
				error = e;
				return false;
			}
		}

		/// <summary>Parses a JSON text literal, and returns the corresponding JSON value if it is valid.</summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="result">Corresponding JSON value. If <paramref name="jsonText"/> is empty, will return <see cref="JsonNull.Missing"/></param>
		/// <returns><c>true</c> if <paramref name="jsonText"/> contained valid JSON; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		[Pure]
		public static bool TryParse(ReadOnlySpan<char> jsonText, CrystalJsonSettings? settings, [MaybeNullWhen(false)] out JsonValue result)
		{
			try
			{
				result = CrystalJson.Parse(jsonText, settings);
				return true;
			}
			catch
			{
				result = null;
				return false;
			}
		}

		/// <summary>Parses a JSON text literal, and returns the corresponding JSON value if it is valid.</summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="result">Corresponding JSON value. If <paramref name="jsonText"/> is empty, will return <see cref="JsonNull.Missing"/></param>
		/// <param name="error">If the parsing fails, receives the exception that represents the issue</param>
		/// <returns><c>true</c> if <paramref name="jsonText"/> contained valid JSON; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		public static bool TryParse(ReadOnlySpan<char> jsonText, CrystalJsonSettings? settings, [MaybeNullWhen(false)] out JsonValue result, out Exception? error)
		{
			try
			{
				result = CrystalJson.Parse(jsonText, settings);
				error = null;
				return true;
			}
			catch(Exception e)
			{
				result = null;
				error = e;
				return false;
			}
		}

		/// <summary>Parses a JSON text literal, and returns the corresponding JSON value if it is valid.</summary>
		/// <param name="jsonBytes">JSON document to parse, encoded as UTF-8 bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="result">Corresponding JSON value. If <paramref name="jsonBytes"/> is empty, will return <see cref="JsonNull.Missing"/></param>
		/// <returns><c>true</c> if <paramref name="jsonBytes"/> contained valid JSON; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		public static bool TryParse(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings, [MaybeNullWhen(false)] out JsonValue result)
		{
			try
			{
				result = CrystalJson.Parse(jsonBytes, settings);
				return true;
			}
			catch
			{
				result = null;
				return false;
			}
		}

		/// <summary>Parses a JSON text literal, and returns the corresponding JSON value if it is valid.</summary>
		/// <param name="jsonBytes">JSON document to parse, encoded as UTF-8 bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="result">Corresponding JSON value. If <paramref name="jsonBytes"/> is empty, will return <see cref="JsonNull.Missing"/></param>
		/// <returns><c>true</c> if <paramref name="jsonBytes"/> contained valid JSON; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		public static bool TryParse(Slice jsonBytes, CrystalJsonSettings? settings, [MaybeNullWhen(false)] out JsonValue result)
			=> TryParse(jsonBytes.Span, settings, out result);

		/// <summary>Parses a JSON text literal, and returns the corresponding JSON Object if it is valid.</summary>
		/// <param name="jsonBytes">JSON document to parse, encoded as UTF-8 bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if <c>null</c>)</param>
		/// <param name="result">Corresponding JSON Object.</param>
		/// <returns><c>true</c> if <paramref name="jsonBytes"/> contained valid JSON Object; otherwise, <c>false</c></returns>
		public static bool TryParseObject(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings, [MaybeNullWhen(false)] out JsonObject result)
		{
			if (!TryParse(jsonBytes, settings, out var value) || value is not JsonObject obj)
			{
				result = null;
				return false;
			}
			result = obj;
			return true;
		}

		/// <summary>Parses a JSON text literal, and returns the corresponding JSON Object if it is valid.</summary>
		/// <param name="jsonBytes">JSON document to parse, encoded as UTF-8 bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if <c>null</c>)</param>
		/// <param name="result">Corresponding JSON Object.</param>
		/// <returns><c>true</c> if <paramref name="jsonBytes"/> contained valid JSON Object; otherwise, <c>false</c></returns>
		public static bool TryParseObject(Slice jsonBytes, CrystalJsonSettings? settings, [MaybeNullWhen(false)] out JsonObject result)
			=> TryParseObject(jsonBytes.Span, settings, out result);

		/// <summary>Parses a JSON text literal, and returns the corresponding JSON Array if it is valid.</summary>
		/// <param name="jsonBytes">JSON document to parse, encoded as UTF-8 bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if <c>null</c>)</param>
		/// <param name="result">Corresponding JSON Array.</param>
		/// <returns><c>true</c> if <paramref name="jsonBytes"/> contained valid a JSON Array; otherwise, <c>false</c></returns>
		public static bool TryParseArray(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings, [MaybeNullWhen(false)] out JsonArray result)
		{
			if (!TryParse(jsonBytes, settings, out var value) || value is not JsonArray arr)
			{
				result = null;
				return false;
			}
			result = arr;
			return true;
		}

		/// <summary>Parses a JSON text literal, and returns the corresponding JSON Array if it is valid.</summary>
		/// <param name="jsonBytes">JSON document to parse, encoded as UTF-8 bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if <c>null</c>)</param>
		/// <param name="result">Corresponding JSON Array.</param>
		/// <returns><c>true</c> if <paramref name="jsonBytes"/> contained valid a JSON Array; otherwise, <c>false</c></returns>
		public static bool TryParseArray(Slice jsonBytes, CrystalJsonSettings? settings, [MaybeNullWhen(false)] out JsonArray result)
			=> TryParseArray(jsonBytes.Span, settings, out result);

		/// <summary>Parses a JSON text literal, and returns the corresponding JSON value if it is valid.</summary>
		/// <param name="jsonBytes">JSON document to parse, encoded as UTF-8 bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="result">Corresponding JSON value. If <paramref name="jsonBytes"/> is empty, will return <see cref="JsonNull.Missing"/></param>
		/// <param name="error">If the parsing fails, receives the exception that represents the issue</param>
		/// <returns><c>true</c> if <paramref name="jsonBytes"/> contained valid JSON; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		public static bool TryParse(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings, [MaybeNullWhen(false)] out JsonValue result, out Exception? error)
		{
			try
			{
				result = CrystalJson.Parse(jsonBytes, settings);
				error = null;
				return true;
			}
			catch(Exception e)
			{
				result = null;
				error = e;
				return false;
			}
		}

		/// <summary>Parses a JSON text literal, and returns the corresponding JSON value if it is valid.</summary>
		/// <param name="jsonBytes">JSON document to parse, encoded as UTF-8 bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="result">Corresponding JSON value. If <paramref name="jsonBytes"/> is empty, will return <see cref="JsonNull.Missing"/></param>
		/// <param name="error">If the parsing fails, receives the exception that represents the issue</param>
		/// <returns><c>true</c> if <paramref name="jsonBytes"/> contained valid JSON; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		public static bool TryParse(Slice jsonBytes, CrystalJsonSettings? settings, [MaybeNullWhen(false)] out JsonValue result, out Exception? error)
			=> TryParse(jsonBytes.Span, settings, out result, out error);

		/// <summary>Parses a JSON text literal that is expected to contain an Array</summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON Array. If <paramref name="jsonText"/> is empty or not an Array, an exception will be thrown instead.</returns>
		/// <remarks>
		/// <para>The JSON Array is mutable and can be freely modified. If you require an immutable array, please use <see cref="JsonValue.ReadOnly.ParseArray(string?,CrystalJsonSettings?)"/> instead.</para>
		/// <para>If the JSON document can sometimes be empty of the 'null' token, you should call <see cref="JsonValue.Parse(string?,CrystalJsonSettings?)"/> and then use <see cref="JsonValueExtensions.AsArrayOrDefault"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document was empty or the 'null' token.</exception>
		[Pure]
		public static JsonArray ParseArray(
#if NET8_0_OR_GREATER
			[System.Diagnostics.CodeAnalysis.StringSyntax(StringSyntaxAttribute.Json)]
#endif
			string? jsonText,
			CrystalJsonSettings? settings = null
		)
		{
			return CrystalJson.Parse(jsonText, settings).AsArray();
		}

		/// <summary>Parses a JSON text literal that is expected to contain an Array</summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON Array. If <paramref name="jsonText"/> is empty or not an Array, an exception will be thrown instead.</returns>
		/// <remarks>
		/// <para>The JSON Array is mutable and can be freely modified. If you require an immutable array, please use <see cref="JsonValue.ReadOnly.ParseArray(string?,CrystalJsonSettings?)"/> instead.</para>
		/// <para>If the JSON document can sometimes be empty of the 'null' token, you should call <see cref="JsonValue.Parse(string?,CrystalJsonSettings?)"/> and then use <see cref="JsonValueExtensions.AsArrayOrDefault"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document was empty or the 'null' token.</exception>
		[Pure]
		public static JsonArray ParseArray(
#if NET8_0_OR_GREATER
			[System.Diagnostics.CodeAnalysis.StringSyntax(StringSyntaxAttribute.Json)]
#endif
			ReadOnlySpan<char> jsonText,
			CrystalJsonSettings? settings = null
		)
		{
			return CrystalJson.Parse(jsonText, settings).AsArray();
		}

		[Obsolete("Use JsonValue.ReadOnly.ParseArray(...) instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static JsonArray ParseArrayReadOnly(
#if NET8_0_OR_GREATER
			[System.Diagnostics.CodeAnalysis.StringSyntax(StringSyntaxAttribute.Json)]
#endif
			string? jsonText,
			CrystalJsonSettings? settings = null
		)
		{
			return CrystalJson.Parse(jsonText, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly).AsArray();
		}

		/// <summary>Parses a JSON text literal that is expected to contain an Object</summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON Object. If <paramref name="jsonText"/> is empty or not an object, an exception will be thrown instead.</returns>
		/// <remarks>If the JSON document can sometimes be empty of the 'null' token, you should call <see cref="JsonValue.Parse(string?,CrystalJsonSettings?)"/> and then use <see cref="JsonValueExtensions.AsObjectOrDefault"/> on the result.</remarks>
		/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document was empty or the 'null' token.</exception>
		[Pure]
		public static JsonObject ParseObject(
#if NET8_0_OR_GREATER
			[System.Diagnostics.CodeAnalysis.StringSyntax(StringSyntaxAttribute.Json)]
#endif
			string? jsonText,
			CrystalJsonSettings? settings = null
		)
		{
			return CrystalJson.Parse(jsonText, settings).AsObject();
		}

		/// <summary>Parses a JSON text literal that is expected to contain an Object</summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON Object. If <paramref name="jsonText"/> is empty or not an object, an exception will be thrown instead.</returns>
		/// <remarks>If the JSON document can sometimes be empty of the 'null' token, you should call <see cref="JsonValue.Parse(string?,CrystalJsonSettings?)"/> and then use <see cref="JsonValueExtensions.AsObjectOrDefault"/> on the result.</remarks>
		/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document was empty or the 'null' token.</exception>
		[Pure]
		public static JsonObject ParseObject(
#if NET8_0_OR_GREATER
			[System.Diagnostics.CodeAnalysis.StringSyntax(StringSyntaxAttribute.Json)]
#endif
			ReadOnlySpan<char> jsonText,
			CrystalJsonSettings? settings = null
		)
		{
			return CrystalJson.Parse(jsonText, settings).AsObject();
		}

		[Obsolete("Use JsonValue.ReadOnly.ParseObject(...) instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static JsonObject ParseObjectReadOnly(
#if NET8_0_OR_GREATER
			[System.Diagnostics.CodeAnalysis.StringSyntax(StringSyntaxAttribute.Json)]
#endif
			string? jsonText,
			CrystalJsonSettings? settings = null
		)
		{
			return CrystalJson.Parse(jsonText, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly).AsObject();
		}

		/// <summary>Parses a buffer containing a document</summary>
		/// <param name="jsonBytes">UTF-8 encoded bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonBytes"/> is null or empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please use <see cref="JsonValue.ReadOnly.Parse(string?,CrystalJsonSettings?)"/> instead.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call either <see cref="JsonArray.Parse(string?,CrystalJsonSettings?)"/> or <see cref="JsonObject.Parse(System.ReadOnlySpan{byte},CrystalJsonSettings?)"/>.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
		[Pure]
		public static JsonValue Parse(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings);
		}

		[Obsolete("Use JsonValue.ReadOnly.Parse(...) instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static JsonValue ParseReadOnly(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly);
		}

		/// <summary>Parses a buffer containing a document that is expected to be an Array</summary>
		/// <param name="jsonBytes">UTF-8 encoded bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON Array. If <paramref name="jsonBytes"/> is empty or not an Array, an exception will be thrown</returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please use <see cref="JsonValue.ReadOnly.ParseArray(string?,CrystalJsonSettings?)"/> instead.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document was empty, or the 'null' token, or not an Array.</exception>
		[Pure]
		public static JsonArray ParseArray(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings).AsArray();
		}

		[Obsolete("Use JsonValue.ReadOnly.ParseArray(...) instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static JsonArray ParseArrayReadOnly(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly).AsArray();
		}

		/// <summary>Parses a buffer containing a document that is expected to be an Object</summary>
		/// <param name="jsonBytes">UTF-8 encoded bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON Object. If <paramref name="jsonBytes"/> is empty or not an Array, an exception will be thrown</returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please use <see cref="JsonValue.ReadOnly.ParseObject(System.ReadOnlySpan{byte},CrystalJsonSettings?)"/> instead.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document was empty, or the 'null' token, or not an Object.</exception>
		[Pure]
		public static JsonObject ParseObject(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings).AsObject();
		}

		[Obsolete("Use JsonValue.ReadOnly.ParseObject(...) instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static JsonObject ParseObjectReadOnly(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly).AsObject();
		}

		/// <summary>Parses a buffer containing a document</summary>
		/// <param name="jsonBytes">UTF-8 encoded bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonBytes"/> is null or empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please use <see cref="JsonValue.ReadOnly.Parse(System.Slice,CrystalJsonSettings?)"/> instead.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call either <see cref="JsonArray.Parse(System.Slice,CrystalJsonSettings?)"/> or <see cref="JsonObject.Parse(string?,CrystalJsonSettings?)"/>.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
		[Pure]
		public static JsonValue Parse(Slice jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings);
		}

		[Obsolete("Use JsonValue.ReadOnly.Parse(...) instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static JsonValue ParseReadOnly(Slice jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly);
		}

		/// <summary>Parses a buffer containing a document that is expected to be an Array</summary>
		/// <param name="jsonBytes">UTF-8 encoded bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON Array.</returns>
		/// <remarks>
		/// <para>The value may be mutable and can be modified. If you require an immutable thread-safe array, please use <see cref="JsonValue.ReadOnly.ParseArray(System.Slice,CrystalJsonSettings?)"/> instead.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
		[Pure]
		public static JsonArray ParseArray(Slice jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings).AsArray();
		}

		[Obsolete("Use JsonValue.ReadOnly.ParseArray(...) instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static JsonArray ParseArrayReadOnly(Slice jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly).AsArray();
		}

		/// <summary>Parses a buffer containing a UTF-8 JSON document, and returns the corresponding expected JSON Object</summary>
		/// <param name="jsonBytes">UTF-8 encoded bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON Object. If <paramref name="jsonBytes"/> is empty or not an Array, an exception will be thrown</returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please use <see cref="JsonValue.ReadOnly.ParseObject(System.ReadOnlySpan{byte},CrystalJsonSettings?)"/> instead.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document was empty, or the 'null' token, or not an Object.</exception>
		[Pure]
		public static JsonObject ParseObject(Slice jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings).AsObject();
		}

		[Obsolete("Use JsonValue.ReadOnly.ParseObject(...) instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static JsonObject ParseObjectReadOnly(Slice jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly).AsObject();
		}

		#endregion

		#region FromValue(...)

		// CLR => JsonValue

		/// <summary>Converts a boxed instance into a mutable JSON value</summary>
		/// <param name="value">Instance to convert (primitive, class, struct, array, ...)</param>
		/// <returns>Corresponding JSON value(JsonNumber, JsonObject, JsonArray, ...), or <see cref="JsonNull.Null"/> if <paramref name="value"/> is <c>null</c></returns>
		/// <remarks>Consider using <see cref="FromValue{T}(T)"/> instead, if the type is known at compile time, in order to reduce runtime overhead.</remarks>
		public static JsonValue FromValue(object? value)
		{
			if (value is null) return JsonNull.Null;
			if (value is JsonValue jv) return jv;
			var type = value.GetType();
			//TODO: PERF: Pooling?
			return CrystalJsonDomWriter.Default.ParseObject(value, type, type);
		}

		[Obsolete("Please call JsonValue.ReadOnly.FromValue(...) instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static JsonValue FromValueReadOnly(object? value) => JsonValue.ReadOnly.FromValue(value);

		/// <summary>Converts a boxed instance into a mutable JSON value</summary>
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
			return CrystalJsonDomWriter.Create(settings, resolver).ParseObject(value, type, type);
		}

		[Obsolete("Please call JsonValue.ReadOnly.FromValue(...) instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static JsonValue FromValueReadOnly(object? value, CrystalJsonSettings settings, ICrystalJsonTypeResolver? resolver = null) => JsonValue.ReadOnly.FromValue(value, settings, resolver);

		/// <summary>Converts a boxed instance into a mutable JSON value</summary>
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
				? CrystalJsonDomWriter.Create(settings, resolver).ParseObject(value, declaredType, value.GetType())
				: JsonNull.Null;
		}

		[Obsolete("Please call JsonValue.ReadOnly.FromValue(...) instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static JsonValue FromValueReadOnly(object? value, Type declaredType, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null) => JsonValue.ReadOnly.FromValue(value, declaredType, settings, resolver);

		/// <summary>Converts a typed value into a mutable JSON value</summary>
		/// <typeparam name="T">Type of the value (can be base class or interface)</typeparam>
		/// <param name="value">Instance to convert</param>
		/// <returns>Corresponding JSON value(JsonNumber, JsonObject, JsonArray, ...), or <see cref="JsonNull.Null"/> if <paramref name="value"/> is <c>null</c></returns>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue FromValue<T>(T? value)
		{
			#region <JIT_HACK>

			// In Release configuration, the JIT will optimize away the "if (typeof(T) == typeof(VALUETYPE)) { ... }" pattern, and should inline this method a call to JsonXYZ.Return(...) for well-known Value Types
			// In Debug configuration, this is not the case, and would be too slow
#if !DEBUG
			if (default(T) is not null)
			{
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
				if (typeof (T) == typeof (DateOnly)) return JsonDateTime.Return((DateOnly) (object) value!);
				if (typeof (T) == typeof (TimeOnly)) return JsonNumber.Return((TimeOnly) (object) value!);
				if (typeof (T) == typeof (NodaTime.Instant)) return JsonString.Return((NodaTime.Instant) (object) value!);
				if (typeof (T) == typeof (NodaTime.Duration)) return JsonNumber.Return((NodaTime.Duration) (object) value!);
				if (typeof (T) == typeof (NodaTime.LocalDateTime)) return JsonString.Return((NodaTime.LocalDateTime) (object) value!);
				if (typeof (T) == typeof (NodaTime.LocalDate)) return JsonString.Return((NodaTime.LocalDate) (object) value!);
				if (typeof (T) == typeof (NodaTime.LocalTime)) return JsonString.Return((NodaTime.LocalTime) (object) value!);
				if (typeof (T) == typeof (NodaTime.ZonedDateTime)) return JsonString.Return((NodaTime.ZonedDateTime) (object) value!);
			}
			else
			{
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
				if (typeof (T) == typeof (DateOnly?)) return JsonDateTime.Return((DateOnly?) (object?) value);
				if (typeof (T) == typeof (TimeOnly?)) return JsonNumber.Return((TimeOnly?) (object?) value);
				if (typeof (T) == typeof (NodaTime.Instant?)) return JsonString.Return((NodaTime.Instant?) (object?) value);
				if (typeof (T) == typeof (NodaTime.Duration?)) return JsonNumber.Return((NodaTime.Duration?) (object?) value);
				if (typeof (T) == typeof (NodaTime.LocalDateTime?)) return JsonString.Return((NodaTime.LocalDateTime?) (object?) value);
				if (typeof (T) == typeof (NodaTime.LocalDate?)) return JsonString.Return((NodaTime.LocalDate?) (object?) value);
				if (typeof (T) == typeof (NodaTime.LocalTime?)) return JsonString.Return((NodaTime.LocalTime?) (object?) value);
				if (typeof (T) == typeof (NodaTime.ZonedDateTime?)) return JsonString.Return((NodaTime.ZonedDateTime?) (object?) value);
			}
#endif
			#endregion </JIT_HACK>

			return CrystalJsonDomWriter.Default.ParseObject(value, typeof(T));
		}

		[Obsolete("Please call JsonValue.ReadOnly.FromValue(...) instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static JsonValue FromValueReadOnly<T>(T? value) => JsonValue.ReadOnly.FromValue<T>(value);

		/// <summary>Converts a typed value into a mutable JSON value</summary>
		/// <typeparam name="T">Type of the value (can be base class or interface)</typeparam>
		/// <param name="writer">Destination writer</param>
		/// <param name="context">Serialization context</param>
		/// <param name="value">Instance to convert</param>
		/// <returns>Corresponding JSON value(JsonNumber, JsonObject, JsonArray, ...), or <see cref="JsonNull.Null"/> if <paramref name="value"/> is <c>null</c></returns>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static JsonValue FromValue<T>(CrystalJsonDomWriter writer, ref CrystalJsonDomWriter.VisitingContext context, T? value)
		{
			#region <JIT_HACK>

			// En mode RELEASE, le JIT reconnaît les patterns "if (typeof(T) == typeof(VALUETYPE)) { ... }" dans une méthode générique Foo<T> quand T est un ValueType,
			// et les remplace par des "if (true) { ...}" ce qui permet d'éliminer le reste du code (très efficace si le if contient un return!)
			// Egalement, le JIT optimise le "(VALUE_TYPE)(object)value" si T == VALUE_TYPE pour éviter le boxing inutile (le cast intermédiaire en object est pour faire taire le compilateur)
			// => pour le vérifier, il faut inspecter l'asm généré par le JIT au runtime (en mode release, en dehors du debugger, etc...) ce qui n'est pas facile...
			// => vérifié avec .NET 4.6.1 + RyuJIT x64, la méthode FromValue<int> est directement inlinée en l'appel à JsonNumber.Return(...) !

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

			//TODO: PERF: Pooling?
			return writer.ParseObjectInternal(ref context, value, typeof (T), null);
		}

		/// <summary>Converts a typed value into a mutable JSON value</summary>
		/// <typeparam name="T">Type of the value (can be base class or interface)</typeparam>
		/// <param name="value">Instance to convert</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>Corresponding JSON value(JsonNumber, JsonObject, JsonArray, ...), or <see cref="JsonNull.Null"/> if <paramref name="value"/> is <c>null</c></returns>
		[Pure]
		public static JsonValue FromValue<T>(T? value, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver = null)
		{
			if (value is null) return JsonNull.Null;
			settings ??= CrystalJsonSettings.Json;
			if (resolver != null && !ReferenceEquals(resolver, CrystalJson.DefaultResolver))
			{
				if (resolver.TryGetConverterFor<T>(out var converter))
				{
					return converter.Pack(value, settings);
				}
			}
			return CrystalJsonDomWriter.Create(settings, resolver).ParseObject<T>(value);
		}

		[Obsolete("Please call JsonValue.ReadOnly.FromValue(...) instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static JsonValue FromValueReadOnly<T>(T? value, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver = null) => JsonValue.ReadOnly.FromValue(value, settings, resolver);

		#region Specialized Converters...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue FromValue<T1>(ValueTuple<T1> tuple) => JsonArray.Return(tuple);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue FromValue<T1, T2>(in ValueTuple<T1, T2> tuple) => JsonArray.Return(in tuple);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue FromValue<T1, T2, T3>(in ValueTuple<T1, T2, T3> tuple) => JsonArray.Return(in tuple);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue FromValue<T1, T2, T3, T4>(in ValueTuple<T1, T2, T3, T4> tuple) => JsonArray.Return(in tuple);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue FromValue<T1, T2, T3, T4, T5>(in ValueTuple<T1, T2, T3, T4, T5> tuple) => JsonArray.Return(in tuple);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue FromValue<T1, T2, T3, T4, T5, T6>(in ValueTuple<T1, T2, T3, T4, T5, T6> tuple) => JsonArray.Return(in tuple);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue FromValue<T1, T2, T3, T4, T5, T6, T7>(in ValueTuple<T1, T2, T3, T4, T5, T6, T7> tuple) => JsonArray.Return(in tuple);

		#endregion

		#endregion

		#region TryFormat Helpers...

		#region Normal...

		public static bool TryFormatNormal(JsonValue value, Span<char> destination, out int charsWritten) => value switch
		{
			JsonNull        => "null".TryCopyTo(destination, out charsWritten),
			JsonBoolean b   => (b.Value ? "true" : "false").TryCopyTo(destination, out charsWritten),
			JsonString str  => JsonEncoding.TryEncodeTo(str.Value, destination, out charsWritten),
			JsonNumber num  => num.TryFormat(destination, out charsWritten, "N"),
			JsonDateTime dt => dt.TryFormat(destination, out charsWritten, "N"),
			JsonArray arr   => TryFormatNormal(arr.GetSpan(), destination, out charsWritten),
			JsonObject obj  => TryFormatNormal(obj.GetItems(), destination, out charsWritten),
			_               => throw new NotSupportedException("Unexpected JsonValue type!")
		};

		internal static bool TryFormatNormal(ReadOnlySpan<JsonValue> items, Span<char> destination, out int charsWritten)
		{
			var buffer = destination;
			if (buffer.Length < 3) goto too_small;

			if (items.Length == 0)
			{
				buffer[0] = '[';
				buffer[1] = ' ';
				buffer[2] = ']';
				charsWritten = 3;
				return true;
			}

			buffer[0] = '[';
			buffer[1] = ' ';
			int pos = 2;
			foreach (var item in items)
			{
				if (pos > 2)
				{
					if (pos + 2 > buffer.Length) goto too_small;
					buffer[pos] = ',';
					buffer[pos + 1] = ' ';
					pos += 2;
				}

				if (!item.TryFormat(buffer[pos..], out int len, "N"))
				{
					goto too_small;
				}

				pos += len;
			}

			if (pos + 2 > buffer.Length) goto too_small;
			buffer[pos] = ' ';
			buffer[pos + 1] = ']';
			pos += 2;

			charsWritten = pos;
			return true;

		too_small:
			charsWritten = 0;
			return false;
		}

		internal static bool TryFormatNormal(Dictionary<string, JsonValue> items, Span<char> destination, out int charsWritten)
		{
			if (destination.Length < 3) goto too_small;

			if (items.Count == 0)
			{
				destination[0] = '{';
				destination[1] = ' ';
				destination[2] = '}';
				charsWritten = 3;
				return true;
			}

			destination[0] = '{';
			destination[1] = ' ';
			int pos = 2;
			foreach (var kv in items)
			{
				if (pos > 2)
				{
					if (!", ".TryCopyTo(destination[pos..])) goto too_small;
					pos += 2;
				}

				// encode the property name
				if (!JsonEncoding.TryEncodeTo(kv.Key, destination[pos..], out int len, withQuotes: true))
				{
					goto too_small;
				}
				pos += len;

				if (!": ".TryCopyTo(destination[pos..])) goto too_small;
				pos += 2;

				// encode the value
				if (!TryFormatNormal(kv.Value, destination[pos..], out len))
				{
					goto too_small;
				}
				pos += len;
			}

			if (!" }".TryCopyTo(destination[pos..])) goto too_small;
			pos += 2;

			charsWritten = pos;
			return true;

		too_small:
			charsWritten = 0;
			return false;
		}

		#endregion

		#region Compact...

		public static bool TryFormatCompact(JsonValue value, Span<char> destination, out int charsWritten) => value switch
		{
			JsonNull        => "null".TryCopyTo(destination, out charsWritten),
			JsonBoolean b   => (b.Value ? "true" : "false").TryCopyTo(destination, out charsWritten),
			JsonString str  => JsonEncoding.TryEncodeTo(str.Value, destination, out charsWritten),
			JsonNumber num  => num.TryFormat(destination, out charsWritten, "C"),
			JsonDateTime dt => dt.TryFormat(destination, out charsWritten, "C"),
			JsonArray arr   => TryFormatCompact(arr.GetSpan(), destination, out charsWritten),
			JsonObject obj  => TryFormatCompact(obj.GetItems(), destination, out charsWritten),
			_               => throw new NotSupportedException("Unexpected JsonValue type!")
		};

		internal static bool TryFormatCompact(ReadOnlySpan<JsonValue> items, Span<char> destination, out int charsWritten)
		{
			if (destination.Length < 2) goto too_small;

			if (items.Length == 0)
			{
				destination[0] = '[';
				destination[1] = ']';
				charsWritten = 2;
				return true;
			}

			destination[0] = '[';
			int pos = 1;
			foreach (var item in items)
			{
				if (pos > 1)
				{
					if (pos >= destination.Length) goto too_small;
					destination[pos++] = ',';
				}

				if (!TryFormatCompact(item, destination[pos..], out int len))
				{
					goto too_small;
				}
				pos += len;
			}

			if (pos >= destination.Length) goto too_small;
			destination[pos++] = ']';

			charsWritten = pos;
			return true;

		too_small:
			charsWritten = 0;
			return false;
		}

		internal static bool TryFormatCompact(Dictionary<string, JsonValue> items, Span<char> destination, out int charsWritten)
		{
			if (destination.Length < 2) goto too_small;

			if (items.Count == 0)
			{
				destination[0] = '{';
				destination[1] = '}';
				charsWritten = 2;
				return true;
			}

			destination[0] = '{';
			int pos = 1;
			foreach (var kv in items)
			{
				if (pos > 1)
				{
					if (pos >= destination.Length) goto too_small;
					destination[pos++] = ',';
				}

				// encode the property name
				if (!JsonEncoding.TryEncodeTo(kv.Key, destination[pos..], out int len, withQuotes: true))
				{
					goto too_small;
				}
				pos += len;

				if (pos >= destination.Length) goto too_small;
				destination[pos++] = ':';

				// encode the value
				if (!TryFormatCompact(kv.Value, destination[pos..], out len))
				{
					goto too_small;
				}
				pos += len;
			}

			if (pos >= destination.Length) goto too_small;
			destination[pos++] = '}';

			charsWritten = pos;
			return true;

		too_small:
			charsWritten = 0;
			return false;
		}

		#endregion

		#endregion

	}

}
