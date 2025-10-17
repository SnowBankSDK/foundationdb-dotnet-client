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
	using System;
	using System.Collections.Frozen;
	using System.Globalization;
	using System.Net;
	using SnowBank.Buffers;
	using SnowBank.Buffers.Text;
	using SnowBank.Collections.Caching;
	using SnowBank.Runtime;
	using SnowBank.Runtime.Converters;
	using SnowBank.Text;

	/// <summary>JSON string literal</summary>
	[DebuggerDisplay("JSON String({" + nameof(m_value) + "})")]
	[PublicAPI]
	[System.Text.Json.Serialization.JsonConverter(typeof(CrystalJsonCustomJsonConverter))]
	[DebuggerNonUserCode]
	public sealed class JsonString : JsonValue,
		IEquatable<JsonString>,
		IComparable<JsonString>,
		IEquatable<string>,
		IComparable<string>,
#if NET9_0_OR_GREATER
		IEquatable<ReadOnlySpan<char>>,
		IEquatable<ReadOnlyMemory<char>>,
		IComparable<ReadOnlySpan<char>>,
		IComparable<ReadOnlyMemory<char>>,
#endif
		IEquatable<Guid>,
		IEquatable<Uuid128>,
		IEquatable<Uuid96>,
		IEquatable<Uuid80>,
		IEquatable<Uuid64>,
		IEquatable<DateTime>,
		IEquatable<DateTimeOffset>,
		IEquatable<DateOnly>,
		IEquatable<TimeOnly>,
		IEquatable<NodaTime.Instant>,
		IEquatable<NodaTime.LocalDateTime>,
		IEquatable<NodaTime.LocalDate>,
		IEquatable<Uri>,
		IEquatable<IPAddress>,
		IEquatable<Version>
	{
		
		/// <summary>Empty string singleton</summary>
		internal static readonly JsonString EmptyString = new(string.Empty);

		/// <summary>Returns the empty string</summary>
		public static readonly JsonValue Empty = EmptyString;

		private readonly string m_value;

		internal JsonString(string value)
		{
			Contract.Debug.Requires(value is not null);
			m_value = value;
		}

		#region Factory...

		#region Create(...)...

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <param name="value">String literal</param>
		/// <returns>Equivalent <see cref="JsonString"/>, or the <see cref="JsonString.Empty"/> singleton if <paramref name="value"/> is empty</returns>
		/// <exception cref="ArgumentNullException"> if <paramref name="value"/> is <c>null</c></exception>
		/// <remarks>If <paramref name="value"/> could be null, call <see cref="Return(string)"/> instead, which returns <see cref="JsonNull.Null"/> instead of an exception</remarks>
		/// <seealso cref="Return(string)"/>
		public static JsonString Create(string value)
		{
			Contract.NotNull(value);
			return value.Length == 0 ? EmptyString : new JsonString(value);
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <param name="value">Character literal</param>
		/// <returns>Equivalent <see cref="JsonString"/></returns>
		/// <seealso cref="Return(char)"/>
		public static JsonString Create(char value)
		{
			return new JsonString(new string(value, 1));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <param name="value">String literal</param>
		/// <returns>Equivalent <see cref="JsonString"/>, or the <see cref="JsonString.Empty"/> singleton if <paramref name="value"/> is empty</returns>
		/// <exception cref="ArgumentNullException"> if <paramref name="value"/> is <c>null</c></exception>
		/// <seealso cref="Return(System.ReadOnlySpan{char})"/>
		public static JsonString Create(ReadOnlySpan<char> value)
		{
			return value.Length == 0 ? EmptyString : new JsonString(value.ToString());
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <param name="value">String literal</param>
		/// <returns>Equivalent <see cref="JsonString"/>, or the <see cref="JsonString.Empty"/> singleton if <paramref name="value"/> is empty</returns>
		/// <exception cref="ArgumentNullException"> if <paramref name="value"/> is <c>null</c></exception>
		/// <seealso cref="Return(System.ReadOnlyMemory{char})"/>
		public static JsonString Create(ReadOnlyMemory<char> value)
		{
			return value.Length == 0 ? EmptyString : new JsonString(value.Span.ToString());
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <param name="value">Byte slice literal</param>
		/// <returns>Equivalent <see cref="JsonString"/> with <paramref name="value"/> encoded as Base64, or the <see cref="JsonString.Empty"/> singleton if <paramref name="value"/> is empty</returns>
		/// <seealso cref="Return(Slice)"/>
		public static JsonString Create(Slice value)
		{
			return value.Count == 0 ? EmptyString : new JsonString(value.ToBase64()!);
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <param name="value">Byte slice literal</param>
		/// <returns>Equivalent <see cref="JsonString"/> with <paramref name="value"/> encoded as Base64, or the <see cref="JsonString.Empty"/> singleton if <paramref name="value"/> is empty</returns>
		/// <seealso cref="Return(ReadOnlySpan{byte})"/>
		public static JsonString Create(ReadOnlySpan<byte> value)
		{
			return value.Length == 0 ? EmptyString : new JsonString(Base64Encoding.ToBase64String(value));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <remarks><para>The <see cref="Guid.Empty"/> guid is equivalent to the <see cref="Empty"/> string, which is a different behavior compared to <see cref="Return(Guid)"/>.</para></remarks>
		/// <seealso cref="Return(Guid)"/>
		public static JsonString Create(Guid value)
		{
			return value != Guid.Empty ? new JsonString(value.ToString()) : EmptyString;
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <remarks><para>The <see cref="Uuid128.Empty"/> guid is equivalent to the <see cref="Empty"/> string, which is a different behavior compared to <see cref="Return(Uuid128)"/>.</para></remarks>
		/// <seealso cref="Return(Uuid128)"/>
		public static JsonString Create(Uuid128 value)
		{
			Guid x = value.ToGuid();
			return x != Guid.Empty ? new JsonString(x.ToString()) : EmptyString;
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <seealso cref="Return(DateTime)"/>
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonString Create(DateTime value)
		{
			return value == DateTime.MinValue ? EmptyString : new JsonString(value.ToString("O"));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <seealso cref="Return(DateTimeOffset)"/>
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonString Create(DateTimeOffset value)
		{
			return value == DateTime.MinValue ? EmptyString : new JsonString(value.ToString("O"));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <seealso cref="Return(DateOnly)"/>
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonString Create(DateOnly value)
		{
			return value == DateOnly.MinValue ? EmptyString : new JsonString(value.ToString("O"));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <seealso cref="Return(TimeOnly)"/>
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonString Create(TimeOnly value)
		{
			return value == TimeOnly.MinValue ? EmptyString : new JsonString(value.ToString("O"));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <seealso cref="Return(NodaTime.Instant)"/>
		public static JsonString Create(NodaTime.Instant value)
		{
			return value.ToUnixTimeTicks() == 0 ? EmptyString : new JsonString(CrystalJsonNodaPatterns.Instants.Format(value));
		}

		//TODO: more to have parity with all non-nullable Return(...) overloads

		#endregion

		/// <summary>Returns a <see cref="JsonValue"/> that is equivalent to the specified string literal</summary>
		/// <param name="value">String literal, or <c>null</c></param>
		/// <returns>Equivalent <see cref="JsonString"/>, the <see cref="JsonString.Empty"/> singleton if <paramref name="value"/> is empty, or the <see cref="JsonNull.Null"/> singleton if <paramref name="value"/> is <c>null</c></returns>
		/// <seealso cref="Create(string)"/>
		public static JsonValue Return(string? value)
		{
			return value is null ? JsonNull.Null : value.Length == 0 ? JsonString.Empty : new JsonString(value);
		}

		/// <summary>Returns a <see cref="JsonValue"/> that is equivalent to the span of characters</summary>
		/// <param name="value">String literal</param>
		/// <returns>Equivalent <see cref="JsonString"/>, or the <see cref="JsonString.Empty"/> singleton if <paramref name="value"/> is empty</returns>
		/// <seealso cref="Create(System.ReadOnlySpan{char})"/>
		public static JsonValue Return(ReadOnlySpan<char> value)
		{
			return value.Length == 0 ? JsonString.Empty : new JsonString(value.ToString());
		}

		/// <summary>Returns a <see cref="JsonValue"/> that is equivalent to the span of characters</summary>
		/// <param name="value">String literal</param>
		/// <returns>Equivalent <see cref="JsonString"/>, or the <see cref="JsonString.Empty"/> singleton if <paramref name="value"/> is empty</returns>
		/// <seealso cref="Create(System.ReadOnlyMemory{char})"/>
		public static JsonValue Return(ReadOnlyMemory<char> value)
		{
			return value.Length == 0 ? JsonString.Empty : new JsonString(value.GetStringOrCopy());
		}

		/// <summary>Returns a <see cref="JsonValue"/> that is equivalent to the content of the specified <see cref="System.Text.StringBuilder"/></summary>
		/// <param name="value">String literal</param>
		/// <returns>Equivalent <see cref="JsonString"/>, or the <see cref="JsonString.Empty"/> singleton if <paramref name="value"/> is empty</returns>
		public static JsonValue Return(System.Text.StringBuilder? value)
		{
			return value is null ? JsonNull.Null : value.Length == 0 ? JsonString.Empty : new JsonString(value.ToString());
		}

		/// <summary>Returns a <see cref="JsonValue"/> that is equivalent to the specified character</summary>
		/// <param name="value">Character literal</param>
		/// <returns>Equivalent <see cref="JsonString"/></returns>
		/// <remarks>Please note that if <paramref name="value"/> is equal to <c>0</c>, this method will return the <c>"\x00"</c> string</remarks>
		/// <seealso cref="Create(char)"/>
		public static JsonValue Return(char value)
		{
			return new JsonString(new string(value, 1));
		}

		/// <summary>Returns a <see cref="JsonValue"/> that is equivalent to the specified character</summary>
		/// <param name="value">Character literal, or <c>null</c></param>
		/// <returns>Equivalent <see cref="JsonString"/>, or the <see cref="JsonNull.Null"/> singleton if <paramref name="value"/> is <c>null</c></returns>
		/// <remarks>Please note that if <paramref name="value"/> is equal to <c>0</c>, this method will return the <c>"\x00"</c> string</remarks>
		public static JsonValue Return(char? value)
		{
			return value is null ? JsonNull.Null : Return(value.Value);
		}

		/// <summary>Returns a <see cref="JsonValue"/> that is equivalent to the span of characters</summary>
		/// <param name="value">Buffer that contains the string literal</param>
		/// <param name="offset">Offset to the start of the string literal in <paramref name="value"/></param>
		/// <param name="count">Size (in characters) of the string literal</param>
		/// <returns>Equivalent <see cref="JsonString"/>, or the <see cref="JsonString.Empty"/> singleton if <paramref name="count"/> is <c>0</c></returns>
		public static JsonValue Return(char[]? value, int offset, int count)
		{
			return count == 0 ? (value is null ? JsonNull.Null : JsonString.Empty) : new JsonString(new string(value!, offset, count));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		public static JsonValue Return(byte[]? value)
		{
			return value is null ? JsonNull.Null : value.Length == 0 ? JsonString.Empty : new JsonString(Convert.ToBase64String(value));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <seealso cref="Create(Slice)"/>
		public static JsonValue Return(Slice value)
		{
			return value.Count == 0
				// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
				? (value.Array is null ? JsonNull.Null : JsonString.Empty)
				: new JsonString(value.ToBase64()!);
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		public static JsonValue Return(ReadOnlySpan<byte> value)
		{
			return value.Length == 0
				? JsonString.Empty
				: new JsonString(Base64Encoding.ToBase64String(value));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		public static JsonValue Return(ReadOnlyMemory<byte> value)
		{
			return value.Length == 0
				? JsonString.Empty
				: new JsonString(Base64Encoding.ToBase64String(value.Span));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		public static JsonValue Return(ArraySegment<byte> value)
		{
			return value.Count == 0
				? (value.Array is null ? JsonNull.Null : JsonString.Empty)
				: new JsonString(Convert.ToBase64String(value.Array!, value.Offset, value.Count));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <remarks><para>The <see cref="Guid.Empty"/> guid is equivalent to a <see cref="JsonNull.Null"/> entry</para></remarks>
		/// <seealso cref="Create(Guid)"/>
		public static JsonValue Return(Guid value)
		{
			return value != Guid.Empty
				? new JsonString(value.ToString())
				: JsonNull.Null;
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <remarks><para>The <see cref="Guid.Empty"/> guid is equivalent to a <see cref="JsonNull.Null"/> entry</para></remarks>
		public static JsonValue Return(Guid? value)
		{
			Guid x;
			return (x = value.GetValueOrDefault()) != Guid.Empty
				? new JsonString(x.ToString())
				: JsonNull.Null;
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <remarks><para>The <see cref="Uuid128.Empty"/> uuid is equivalent to a <see cref="JsonNull.Null"/> entry</para></remarks>
		/// <seealso cref="Create(Uuid128)"/>
		public static JsonValue Return(Uuid128 value)
		{
			Guid x = value.ToGuid();
			return x != Guid.Empty
				? new JsonString(x.ToString())
				: JsonNull.Null;
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <remarks><para>The <see cref="Uuid128.Empty"/> uuid is equivalent to a <see cref="JsonNull.Null"/> entry</para></remarks>
		public static JsonValue Return(Uuid128? value)
		{
			Uuid128 x;
			return (x = value.GetValueOrDefault()) != Uuid128.Empty
				? new JsonString(x.ToString())
				: JsonNull.Null;
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <remarks><para>The <see cref="Uuid96.Empty"/> uuid is equivalent to a <see cref="JsonNull.Null"/> entry</para></remarks>
		public static JsonValue Return(Uuid96 value)
		{
			return value != Uuid96.Empty
				? new JsonString(value.ToString())
				: JsonNull.Null;
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <remarks><para>The <see cref="Uuid96.Empty"/> uuid is equivalent to a <see cref="JsonNull.Null"/> entry</para></remarks>
		public static JsonValue Return(Uuid96? value)
		{
			Uuid96 x;
			return (x = value.GetValueOrDefault()) != Uuid96.Empty
				? new JsonString(x.ToString())
				: JsonNull.Null;
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <remarks><para>The <see cref="Uuid80.Empty"/> uuid is equivalent to a <see cref="JsonNull.Null"/> entry</para></remarks>
		public static JsonValue Return(Uuid80 value)
		{
			return value != Uuid80.Empty
				? new JsonString(value.ToString())
				: JsonNull.Null;
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <remarks><para>The <see cref="Uuid80.Empty"/> uuid is equivalent to a <see cref="JsonNull.Null"/> entry</para></remarks>
		public static JsonValue Return(Uuid80? value)
		{
			Uuid80 x;
			return (x = value.GetValueOrDefault()) != Uuid80.Empty
				? new JsonString(x.ToString())
				: JsonNull.Null;
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <remarks><para>The <see cref="Uuid64.Empty"/> uuid is equivalent to a <see cref="JsonNull.Null"/> entry</para></remarks>
		public static JsonValue Return(Uuid64 value)
		{
			return value != Uuid64.Empty
				? new JsonString(value.ToString())
				: JsonNull.Null;
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		/// <remarks><para>The <see cref="Uuid64.Empty"/> uuid is equivalent to a <see cref="JsonNull.Null"/> entry</para></remarks>
		public static JsonValue Return(Uuid64? value)
		{
			Uuid64 x;
			return (x = value.GetValueOrDefault()) != Uuid64.Empty
				? new JsonString(x.ToString())
				: JsonNull.Null;
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		public static JsonValue Return(System.Net.IPAddress? value)
		{
			return value is not null
				? new JsonString(value.ToString())
				: JsonNull.Null;
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		public static JsonValue Return(Uri? value)
		{
			return value is not null
				? new JsonString(value.OriginalString)
				: JsonNull.Null;
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		public static JsonValue Return(Version? value)
		{
			return value is not null
				? new JsonString(value.ToString())
				: JsonNull.Null;
		}

		internal static class TypeNameCache
		{

			private static readonly QuasiImmutableCache<Type, JsonString> Names = CreateDefaultCachedTypes();

			[ContractAnnotation("type:notnull => notnull")]
			public static JsonString? FromType(Type? type)
			{
				if (type is null) return null;
				return Names.TryGetValue(type, out var value)
					? value
					: CreateTypeNameSingleton(type);
			}

			private static QuasiImmutableCache<Type, JsonString> CreateDefaultCachedTypes()
			{
				var map = new Dictionary<Type, JsonString>(TypeEqualityComparer.Default)
				{
					// basic types
					[typeof(object)] = new("object"),
					[typeof(string)] = new("string"),
					[typeof(bool)] = new("bool"),
					[typeof(char)] = new("char"),
					[typeof(sbyte)] = new("sbyte"),
					[typeof(short)] = new("short"),
					[typeof(int)] = new("int"),
					[typeof(long)] = new("long"),
					[typeof(byte)] = new("byte"),
					[typeof(ushort)] = new("ushort"),
					[typeof(uint)] = new("uint"),
					[typeof(ulong)] = new("ulong"),
					[typeof(float)] = new("float"),
					[typeof(double)] = new("double"),
					[typeof(decimal)] = new("decimal"),
					[typeof(TimeSpan)] = new("TimeSpan"),
					[typeof(DateTime)] = new("DateTime"),
					[typeof(DateTimeOffset)] = new("DateTimeOffset"),
					[typeof(DateOnly)] = new("DateOnly"),
					[typeof(TimeOnly)] = new("TimeOnly"),
					[typeof(Guid)] = new("Guid"),
					// nullable types
					[typeof(bool?)] = new("bool?"),
					[typeof(char?)] = new("char?"),
					[typeof(sbyte?)] = new("sbyte?"),
					[typeof(short?)] = new("short?"),
					[typeof(int?)] = new("int?"),
					[typeof(long?)] = new("long?"),
					[typeof(byte?)] = new("byte?"),
					[typeof(ushort?)] = new("ushort?"),
					[typeof(uint?)] = new("uint?"),
					[typeof(ulong?)] = new("ulong?"),
					[typeof(float?)] = new("float?"),
					[typeof(double?)] = new("double?"),
					[typeof(decimal?)] = new("decimal?"),
					[typeof(TimeSpan?)] = new("TimeSpan?"),
					[typeof(DateTime?)] = new("DateTime?"),
					[typeof(DateTimeOffset?)] = new("DateTimeOffset?"),
					[typeof(DateOnly?)] = new("DateOnly?"),
					[typeof(TimeOnly?)] = new("TimeOnly?"),
					[typeof(Guid?)] = new("Guid?"),
					// system types
					[typeof(Uri)] = new(typeof(Uri).FullName!),
					[typeof(Version)] = new(typeof(Version).FullName!),
					[typeof(Exception)] = new(typeof(Exception).FullName!),
					//
				};
				return new(map.ToFrozenDictionary());
			}

			[Pure]
			private static string GetSerializableTypeName(Type type)
			{
				// note: "simple" types (string, Guid, ...) have already been handled by the cache

				// we will omit the assembly name for mscorlib and System.Core because they will always be loaded!
				string assName = type.Assembly.FullName!;
				if (assName.StartsWith("mscorlib, ", StringComparison.Ordinal)
				 || assName.StartsWith("System.Core, ", StringComparison.Ordinal)
				 || assName.StartsWith("System.Private.CoreLib, ", StringComparison.Ordinal))
				{
					return type.FullName ?? type.Name;
				}

				// for everything else, we must add the full qualified name, including the assembly and culture :(
				return type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
			}

			[Pure]
			private static JsonString CreateTypeNameSingleton(Type type)
			{
				return Names.GetOrAdd(type, new JsonString(GetSerializableTypeName(type)));
			}

		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		[Pure]
		public static JsonValue Return(Type? type)
		{
			return TypeNameCache.FromType(type) ?? JsonNull.Null;
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonValue Return(DateTime value)
		{
			return value == DateTime.MinValue ? JsonString.Empty : new JsonString(value.ToString("O"));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonValue Return(DateTime? value)
		{
			return value is null ? JsonNull.Null : value.Value == DateTime.MinValue ? JsonString.Empty : new JsonString(value.Value.ToString("O"));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonValue Return(DateTimeOffset value)
		{
			return value == DateTime.MinValue ? JsonString.Empty : new JsonString(value.ToString("O"));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonValue Return(DateTimeOffset? value)
		{
			return value is null ? JsonNull.Null : value.Value == DateTime.MinValue ? JsonString.Empty : new JsonString(value.Value.ToString("O"));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonValue Return(DateOnly value)
		{
			return value == DateOnly.MinValue ? JsonString.Empty : new JsonString(value.ToString("O"));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonValue Return(DateOnly? value)
		{
			return value is null ? JsonNull.Null : value.Value == DateOnly.MinValue ? JsonString.Empty : new JsonString(value.Value.ToString("O"));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonValue Return(TimeOnly value)
		{
			return value == TimeOnly.MinValue ? JsonString.Empty : new JsonString(value.ToString("O"));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonValue Return(TimeOnly? value)
		{
			return value is null ? JsonNull.Null : value.Value == TimeOnly.MinValue ? JsonString.Empty : new JsonString(value.Value.ToString("O"));
		}

		#region Noda Types...

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		public static JsonValue Return(NodaTime.Instant value)
		{
			if (value.ToUnixTimeTicks() == 0) return JsonString.Empty;
			return new JsonString(CrystalJsonNodaPatterns.Instants.Format(value));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		public static JsonValue Return(NodaTime.Instant? value)
		{
			if (!value.HasValue) return JsonNull.Null;
			if (value.Value.ToUnixTimeTicks() == 0) return JsonString.Empty;
			return new JsonString(CrystalJsonNodaPatterns.Instants.Format(value.Value));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		public static JsonValue Return(NodaTime.ZonedDateTime value)
		{
			return new JsonString(CrystalJsonNodaPatterns.ZonedDateTimes.Format(value));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		public static JsonValue Return(NodaTime.ZonedDateTime? value)
		{
			if (!value.HasValue) return JsonNull.Null;
			return new JsonString(CrystalJsonNodaPatterns.ZonedDateTimes.Format(value.Value));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		public static JsonValue Return(NodaTime.DateTimeZone? value)
		{
			return value is not null
				? new JsonString(value.Id)
				: JsonNull.Null;
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		public static JsonValue Return(NodaTime.LocalDateTime value)
		{
			return new JsonString(CrystalJsonNodaPatterns.LocalDateTimes.Format(value));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		public static JsonValue Return(NodaTime.LocalDateTime? value)
		{
			if (!value.HasValue) return JsonNull.Null;
			return new JsonString(CrystalJsonNodaPatterns.LocalDateTimes.Format(value.Value));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		public static JsonValue Return(NodaTime.LocalDate value)
		{
			return new JsonString(CrystalJsonNodaPatterns.LocalDates.Format(value));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		public static JsonValue Return(NodaTime.LocalDate? value)
		{
			if (!value.HasValue) return JsonNull.Null;
			return new JsonString(CrystalJsonNodaPatterns.LocalDates.Format(value.Value));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		public static JsonValue Return(NodaTime.LocalTime value)
		{
			return new JsonString(CrystalJsonNodaPatterns.LocalTimes.Format(value));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		public static JsonValue Return(NodaTime.LocalTime? value)
		{
			if (!value.HasValue) return JsonNull.Null;
			return new JsonString(CrystalJsonNodaPatterns.LocalTimes.Format(value.Value));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		public static JsonValue Return(NodaTime.OffsetDateTime value)
		{
			return new JsonString(CrystalJsonNodaPatterns.OffsetDateTimes.Format(value));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		public static JsonValue Return(NodaTime.OffsetDateTime? value)
		{
			if (!value.HasValue) return JsonNull.Null;
			return new JsonString(CrystalJsonNodaPatterns.OffsetDateTimes.Format(value.Value));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		public static JsonValue Return(NodaTime.Offset value)
		{
			return new JsonString(CrystalJsonNodaPatterns.Offsets.Format(value));
		}

		/// <summary>Returns the equivalent <see cref="JsonString"/></summary>
		public static JsonValue Return(NodaTime.Offset? value)
		{
			if (!value.HasValue) return JsonNull.Null;
			return new JsonString(CrystalJsonNodaPatterns.Offsets.Format(value.Value));
		}

		#endregion

		#endregion

		#region Public Properties...

		/// <summary>Gets the string literal</summary>
		public string Value => m_value;

		/// <summary>Gets the length of the string</summary>
		public int Length => m_value.Length;

		/// <summary>Returns <see langword="true"/> if the string is empty</summary>
		public bool IsNullOrEmpty => string.IsNullOrEmpty(m_value);

		/// <summary>Tests if the string starts with the specified literal</summary>
		[Pure]
		public bool StartsWith(string? value) => value != null && m_value.StartsWith(value, StringComparison.Ordinal);

		/// <summary>Tests if the string starts with the specified literal</summary>
		[Pure]
		public bool StartsWith(ReadOnlySpan<char> value) => m_value.AsSpan().StartsWith(value, StringComparison.Ordinal);

		/// <summary>Tests if the string starts with the specified literal</summary>
		[Pure]
		public bool StartsWith(ReadOnlyMemory<char> value) => m_value.AsSpan().StartsWith(value.Span, StringComparison.Ordinal);

		/// <summary>Tests if the string starts with the specified literal</summary>
		[Pure]
		public bool StartsWith(JsonString value) => m_value.StartsWith(value.m_value, StringComparison.Ordinal);

		/// <summary>Tests if the string starts with the specified literal</summary>
		[Pure]
		public bool StartsWith(JsonValue value) => value is JsonString str && m_value.StartsWith(str.m_value, StringComparison.Ordinal);

		/// <summary>Tests if the string ends with the specified literal</summary>
		[Pure]
		public bool EndsWith(string? value) => value != null && m_value.EndsWith(value, StringComparison.Ordinal);

		/// <summary>Tests if the string ends with the specified literal</summary>
		[Pure]
		public bool EndsWith(ReadOnlySpan<char> value) => m_value.AsSpan().EndsWith(value, StringComparison.Ordinal);

		/// <summary>Tests if the string ends with the specified literal</summary>
		[Pure]
		public bool EndsWith(ReadOnlyMemory<char> value) => m_value.AsSpan().EndsWith(value.Span, StringComparison.Ordinal);

		/// <summary>Tests if the string ends with the specified literal</summary>
		[Pure]
		public bool EndsWith(JsonString value) => m_value.EndsWith(value.m_value, StringComparison.Ordinal);

		/// <summary>Tests if the string ends with the specified literal</summary>
		[Pure]
		public bool EndsWith(JsonValue value) => value is JsonString str && m_value.EndsWith(str.m_value, StringComparison.Ordinal);

		/// <summary>Tests if the string contains the specified literal</summary>
		[Pure]
		public bool Contains(string? value) => value != null && m_value.IndexOf(value, StringComparison.Ordinal) >= 0;

		/// <summary>Tests if the string contains the specified literal</summary>
		[Pure]
		public bool Contains(ReadOnlySpan<char> value) => m_value.AsSpan().IndexOf(value, StringComparison.Ordinal) >= 0;

		/// <summary>Tests if the string contains the specified literal</summary>
		[Pure]
		public bool Contains(ReadOnlyMemory<char> value) => m_value.AsSpan().IndexOf(value.Span, StringComparison.Ordinal) >= 0;

		/// <summary>Tests if the string contains the specified literal</summary>
		[Pure]
		public bool Contains(JsonString value) => m_value.IndexOf(value.m_value, StringComparison.Ordinal) >= 0;

		/// <summary>Tests if the string contains the specified literal</summary>
		[Pure]
		public override bool Contains(JsonValue? value) => value is JsonString str && m_value.IndexOf(str.m_value, StringComparison.Ordinal) >= 0;

		/// <summary>Returns the index of the first occurrence of the specified literal</summary>
		/// <remarks>Returns <c>-1</c> if the string does not contain this literal</remarks>
		public int IndexOf(string? value) => value != null ? m_value.IndexOf(value, StringComparison.Ordinal) : -1;

		/// <summary>Returns the index of the first occurrence of the specified literal</summary>
		/// <remarks>Returns <c>-1</c> if the string does not contain this literal</remarks>
		public int IndexOf(ReadOnlySpan<char> value) => m_value.AsSpan().IndexOf(value, StringComparison.Ordinal);

		/// <summary>Returns the index of the first occurrence of the specified literal</summary>
		/// <remarks>Returns <c>-1</c> if the string does not contain this literal</remarks>
		public int IndexOf(ReadOnlyMemory<char> value) => m_value.AsSpan().IndexOf(value.Span, StringComparison.Ordinal);

		/// <summary>Returns the index of the first occurrence of the specified literal</summary>
		/// <remarks>Returns <c>-1</c> if the string does not contain this literal</remarks>
		public int IndexOf(JsonString value) => m_value.IndexOf(value.m_value, StringComparison.Ordinal);

		/// <summary>Returns the index of the first occurrence of the specified literal</summary>
		/// <remarks>Returns <c>-1</c> if the string does not contain this literal</remarks>
		public int IndexOf(JsonValue value) => value is JsonString str ? m_value.IndexOf(str.m_value, StringComparison.Ordinal) : -1;

		#endregion

		#region JsonValue Members...

		/// <inheritdoc />
		public override JsonType Type => JsonType.String;

		/// <inheritdoc />
		public override bool IsNull => false;

		/// <inheritdoc />
		public override bool IsDefault => false; //note: string empty is NOT "default"

		/// <inheritdoc />
		public override bool IsReadOnly => true; //note: strings are immutable

		/// <summary>Decodes this string as a Base64 encoded buffer</summary>
		public byte[] ToBuffer() //REVIEW: => GetBytes()? DecodeBase64() ?
		{
			return m_value.Length != 0 ? Convert.FromBase64String(m_value) : [ ];
		}

		/// <inheritdoc />
		public override object ToObject()
		{
			return m_value;
		}

		/// <inheritdoc />
		public override T? Bind<T>(T? defaultValue = default, ICrystalJsonTypeResolver? resolver = null) where T : default
		{
			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build

			if (default(T) is not null)
			{
				if (typeof(T) == typeof(bool)) return (T) (object) ToBoolean();
				if (typeof(T) == typeof(byte)) return (T) (object) ToByte();
				if (typeof(T) == typeof(sbyte)) return (T) (object) ToSByte();
				if (typeof(T) == typeof(char)) return (T) (object) ToChar();
				if (typeof(T) == typeof(short)) return (T) (object) ToInt16();
				if (typeof(T) == typeof(ushort)) return (T) (object) ToUInt16();
				if (typeof(T) == typeof(int)) return (T) (object) ToInt32();
				if (typeof(T) == typeof(uint)) return (T) (object) ToUInt32();
				if (typeof(T) == typeof(ulong)) return (T) (object) ToUInt64();
				if (typeof(T) == typeof(long)) return (T) (object) ToInt64();
				if (typeof(T) == typeof(float)) return (T) (object) ToSingle();
				if (typeof(T) == typeof(double)) return (T) (object) ToDouble();
				if (typeof(T) == typeof(decimal)) return (T) (object) ToDecimal();
				if (typeof(T) == typeof(TimeSpan)) return (T) (object) ToTimeSpan();
				if (typeof(T) == typeof(DateTime)) return (T) (object) ToDateTime();
				if (typeof(T) == typeof(DateTimeOffset)) return (T) (object) ToDateTimeOffset();
				if (typeof(T) == typeof(DateOnly)) return (T) (object) ToDateOnly();
				if (typeof(T) == typeof(TimeOnly)) return (T) (object) ToTimeOnly();
				if (typeof(T) == typeof(Guid)) return (T) (object) ToGuid();
				if (typeof(T) == typeof(Uuid128)) return (T) (object) ToUuid128();
				if (typeof(T) == typeof(Uuid96)) return (T) (object) ToUuid96();
				if (typeof(T) == typeof(Uuid80)) return (T) (object) ToUuid80();
				if (typeof(T) == typeof(Uuid64)) return (T) (object) ToUuid64();
				if (typeof(T) == typeof(NodaTime.Instant)) return (T) (object) ToInstant();
				if (typeof(T) == typeof(NodaTime.Duration)) return (T) (object) ToDuration();
				if (typeof(T) == typeof(NodaTime.LocalDateTime)) return (T) (object) ToLocalDateTime();
				if (typeof(T) == typeof(NodaTime.LocalDate)) return (T) (object) ToLocalDate();
				if (typeof(T) == typeof(NodaTime.Offset)) return (T) (object) ToOffset();
				if (typeof(T) == typeof(Half)) return (T) (object) ToHalf();
#if NET8_0_OR_GREATER
				if (typeof(T) == typeof(Int128)) return (T) (object) ToInt128();
				if (typeof(T) == typeof(UInt128)) return (T) (object) ToUInt128();
#endif

				return (T?) Bind(typeof(T), resolver);
			}
			else
			{
				if (typeof(T) == typeof(string)) return (T) (object) m_value;

				if (typeof(T) == typeof(bool?)) return (T?) (object?) ToBooleanOrDefault((bool?) (object?) defaultValue);
				if (typeof(T) == typeof(byte?)) return (T?) (object?) ToByteOrDefault((byte?) (object?) defaultValue);
				if (typeof(T) == typeof(sbyte?)) return (T?) (object?) ToSByteOrDefault((sbyte?) (object?) defaultValue);
				if (typeof(T) == typeof(char?)) return (T?) (object?) ToCharOrDefault((char?) (object?) defaultValue);
				if (typeof(T) == typeof(short?)) return (T?) (object?) ToInt16OrDefault((short?) (object?) defaultValue);
				if (typeof(T) == typeof(ushort?)) return (T?) (object?) ToUInt16OrDefault((ushort?) (object?) defaultValue);
				if (typeof(T) == typeof(int?)) return (T?) (object?) ToInt32OrDefault((int?) (object?) defaultValue);
				if (typeof(T) == typeof(uint?)) return (T?) (object?) ToUInt32OrDefault((uint?) (object?) defaultValue);
				if (typeof(T) == typeof(ulong?)) return (T?) (object?) ToUInt64OrDefault((ulong?) (object?) defaultValue);
				if (typeof(T) == typeof(long?)) return (T?) (object?) ToInt64OrDefault((long?) (object?) defaultValue);
				if (typeof(T) == typeof(float?)) return (T?) (object?) ToSingleOrDefault((float?) (object?) defaultValue);
				if (typeof(T) == typeof(double?)) return (T?) (object?) ToDoubleOrDefault((double?) (object?) defaultValue);
				if (typeof(T) == typeof(decimal?)) return (T?) (object?) ToDecimalOrDefault((decimal?) (object?) defaultValue);
				if (typeof(T) == typeof(TimeSpan?)) return (T?) (object?) ToTimeSpanOrDefault((TimeSpan?) (object?) defaultValue);
				if (typeof(T) == typeof(DateTime?)) return (T?) (object?) ToDateTimeOrDefault((DateTime?) (object?) defaultValue);
				if (typeof(T) == typeof(DateTimeOffset?)) return (T?) (object?) ToDateTimeOffsetOrDefault((DateTimeOffset?) (object?) defaultValue);
				if (typeof(T) == typeof(DateOnly?)) return (T?) (object?) ToDateOnlyOrDefault((DateOnly?) (object?) defaultValue);
				if (typeof(T) == typeof(TimeOnly?)) return (T?) (object?) ToTimeOnlyOrDefault((TimeOnly?) (object?) defaultValue);
				if (typeof(T) == typeof(Guid?)) return (T?) (object?) ToGuidOrDefault((Guid?) (object?) defaultValue);
				if (typeof(T) == typeof(Uuid128?)) return (T?) (object?) ToUuid128OrDefault((Uuid128?) (object?) defaultValue);
				if (typeof(T) == typeof(Uuid96?)) return (T?) (object?) ToUuid96OrDefault((Uuid96?) (object?) defaultValue);
				if (typeof(T) == typeof(Uuid80?)) return (T?) (object?) ToUuid80OrDefault((Uuid80?) (object?) defaultValue);
				if (typeof(T) == typeof(Uuid64?)) return (T?) (object?) ToUuid64OrDefault((Uuid64?) (object?) defaultValue);
				if (typeof(T) == typeof(NodaTime.Instant?)) return (T?) (object?) ToInstantOrDefault((NodaTime.Instant?) (object?) defaultValue);
				if (typeof(T) == typeof(NodaTime.Duration?)) return (T?) (object?) ToDurationOrDefault((NodaTime.Duration?) (object?) defaultValue);
				if (typeof(T) == typeof(NodaTime.LocalDateTime?)) return (T?) (object?) ToLocalDateTime();
				if (typeof(T) == typeof(NodaTime.LocalDate?)) return (T?) (object?) ToLocalDate();
				if (typeof(T) == typeof(NodaTime.Offset?)) return (T?) (object?) ToOffset();
				if (typeof(T) == typeof(Half?)) return (T?) (object?) ToHalfOrDefault((Half?) (object?) defaultValue);
#if NET8_0_OR_GREATER
				if (typeof(T) == typeof(Int128?)) return (T?) (object?) ToInt128OrDefault((Int128?) (object?) defaultValue);
				if (typeof(T) == typeof(UInt128?)) return (T?) (object?) ToUInt128OrDefault((UInt128?) (object?) defaultValue);
#endif

				return (T?) (Bind(typeof(T), resolver) ?? defaultValue);
			}

			#endregion
		}

		/// <inheritdoc />
		public override object? Bind(Type? type, ICrystalJsonTypeResolver? resolver = null)
		{
			if (type is null || typeof(string) == type || typeof(object) == type)
			{
				return m_value;
			}

			if (type.IsAssignableTo(typeof(JsonValue)))
			{
				if (type == typeof(JsonValue) || type == typeof(JsonString))
				{
					return this;
				}
				throw JsonBindingException.CannotBindJsonStringToThisType(this, type);
			}

			resolver ??= CrystalJson.DefaultResolver;

			try
			{
				if (type.IsPrimitive)
				{
					switch(System.Type.GetTypeCode(type))
					{
						case TypeCode.Int32: return ToInt32();
						case TypeCode.Int64: return ToInt64();
						case TypeCode.Single: return ToSingle();
						case TypeCode.Double: return ToDouble();
						case TypeCode.Boolean: return ToBoolean();
						case TypeCode.Char: return ToChar();
						case TypeCode.UInt32: return ToUInt32();
						case TypeCode.UInt64: return ToUInt64();
						case TypeCode.Int16: return ToInt16();
						case TypeCode.UInt16: return ToUInt16();
						case TypeCode.Byte: return ToByte();
						case TypeCode.SByte: return ToSByte();
						//note: decimal and DateTime are not 'IsPrimitive' !
					}
				}
				else if (typeof(DateTime) == type)
				{
					return ToDateTime();
				}
				else if (typeof(DateTimeOffset) == type)
				{
					return ToDateTimeOffset();
				}
				else if (type.IsEnum)
				{
					return Enum.Parse(type, m_value, true);
				}
				else if (typeof(decimal) == type)
				{
					return ToDecimal();
				}
				else if (typeof(Guid) == type)
				{
					return ToGuid();
				}
				else if (typeof(Uuid128) == type)
				{
					return ToUuid128();
				}
				else if (typeof(Uuid64) == type)
				{
					return ToUInt64();
				}
				else if (typeof(Uuid96) == type)
				{
					return ToUuid96();
				}
				else if (typeof(Uuid80) == type)
				{
					return ToUuid80();
				}
				else if (typeof(TimeSpan) == type)
				{
					return ToTimeSpan();
				}
				else if (typeof(DateOnly) == type)
				{
					return ToDateOnly();
				}
				else if (typeof(TimeOnly) == type)
				{
					return ToTimeOnly();
				}
				else if (typeof(char[]) == type)
				{
					return m_value.ToCharArray();
				}
				else if (typeof(byte[]) == type)
				{ // by convention should be a Base64 encoded string
					return ToBuffer();
				}
				else if (typeof(ArraySegment<byte>) == type)
				{
					return new ArraySegment<byte>(ToBuffer());
				}
				else if (typeof(Slice) == type)
				{
					return ToBuffer().AsSlice();
				}
				else if (typeof(System.Text.StringBuilder) == type)
				{
					return new System.Text.StringBuilder(m_value);
				}
				else if (typeof(System.Net.IPAddress) == type)
				{
					return ToIpAddress();
				}
				else if (typeof(Version) == type)
				{
					return ToVersion();
				}
				else if (typeof(Uri) == type)
				{
					return ToUri();
				}

				#region NodaTime types...

				if (typeof(NodaTime.Instant) == type) return ToInstant();
				if (typeof(NodaTime.Duration) == type) return ToDuration();
				if (typeof(NodaTime.LocalDate) == type) return ToLocalDate();
				if (typeof(NodaTime.LocalDateTime) == type) return ToLocalDateTime();
				if (typeof(NodaTime.ZonedDateTime) == type) return ToZonedDateTime();
				if (typeof(NodaTime.OffsetDateTime) == type) return ToOffsetDateTime();
				if (typeof(NodaTime.Offset) == type) return ToOffset();
				if (typeof(NodaTime.LocalTime) == type) return CrystalJsonNodaPatterns.LocalTimes.Parse(m_value).Value;
				if (typeof(NodaTime.DateTimeZone).IsAssignableFrom(type)) return ToDateTimeZone();

				#endregion

				//TODO: XmlNode ?

				var nullableType = Nullable.GetUnderlyingType(type);
				if (nullableType is not null)
				{
					//note: missing/null or "" returns default(T?), which is already null
					if (string.IsNullOrEmpty(m_value)) return null;
					return Bind(nullableType, resolver);
				}
			}
			catch (Exception e) when (!e.IsFatalError())
			{
				if (e is FormatException) throw new FormatException($"Failed to convert JSON string into {type.GetFriendlyName()}: {e.Message}");
				throw JsonBindingException.CannotBindJsonStringToThisType(this, type, e);
			}

			// maybe we have a custom binder?
			if (resolver.TryResolveTypeDefinition(type, out var def) && def.CustomBinder is not null)
			{
				return def.CustomBinder(this, type, resolver);
			}

			throw JsonBindingException.CannotBindJsonStringToThisType(this, type);
		}

		/// <inheritdoc />
		internal override bool IsSmallValue() => m_value.Length <= 36; // guid!

		/// <inheritdoc />
		internal override bool IsInlinable() => m_value.Length <= 80;

		/// <inheritdoc />
		internal override string GetCompactRepresentation(int depth)
		{
			// depth 0, complete string up to 128 chars, then truncated with '[...]' in the middle if larger
			// depth 1: complete string up to  64 chars, then truncated with '[...]' in the middle if larger
			// depth 2: complete string up to  36 chars, then truncated with '[...]' in the middle if larger
			// depth 3: complete string up to  16 chars, then truncated with '[...]' in the middle if larger
			// depth 4+: '...'

			var value = m_value;
			if (value.Length == 0) return "''";
			if (depth > 3) return "'\u2026'";
			int allowance = depth switch
			{
				0 => 128,
				1 => 64,
				2 => 48,
				3 => 36, // size for a guid/uuid128
				_ => 16
			};
			return value.Length <= allowance
				? "'" + value + "'"
				: $"'{value.AsSpan(0, (allowance / 2) - 1)}[\u2026]{value.AsSpan(value.Length - (allowance / 2) + 1)}'";
		}

		#endregion

		#region IJsonSerializable...

		/// <inheritdoc />
		public override void JsonSerialize(CrystalJsonWriter writer)
		{
			writer.WriteValue(m_value);
		}

		/// <inheritdoc />
		public override bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
		{
			//BUGBUG: TODO: need to validate the format!
			if (format is "J" or "j")
			{
				return TryFormatJavaScript(m_value, destination, out charsWritten);
			}
			return JsonEncoding.TryEncodeTo(destination, m_value, out charsWritten);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static bool TryFormatJavaScript(string value, Span<char> destination, out int charsWritten)
			{
				// we don't have a JavaScriptEncoding.TryEncodeTo(...) so we have to use a temp buffer :/
				var sw = new ValueStringWriter(destination.Length + 8);
				JavaScriptEncoding.EncodeTo(ref sw, value, includeQuotes: true);
				sw.TryCopyTo(destination, out charsWritten);
				sw.Dispose();
				return charsWritten != 0;
			}
		}

#if NET8_0_OR_GREATER

		/// <inheritdoc />
		public override bool TryFormat(Span<byte> destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
		{
			//BUGBUG: TODO: need to validate the format!

			if (format is "J" or "j")
			{
				//BUGBUG: TODO: need to handle "J"/"j" for JavaScript strings!
				throw new NotImplementedException();
			}

			return JsonEncoding.TryEncodeTo(destination, m_value, out bytesWritten);
		}

#endif

		#endregion

		#region IEquatable<...>...

		/// <inheritdoc />
		public override bool Equals(object? obj)
		{
			if (ReferenceEquals(obj, this)) return true;
			return obj switch
			{
				JsonValue val      => Equals(val),
				string s           => Equals(s),
				Guid g             => Equals(g),
				Uuid128 u128       => Equals(u128),
				Uuid96 u96         => Equals(u96),
				Uuid80 u80         => Equals(u80),
				Uuid64 u64         => Equals(u64),
				null               => false,
				DateTime dt        => Equals(dt),
				DateTimeOffset dto => Equals(dto),
				NodaTime.Instant t => Equals(t),
				Uri uri            => Equals(uri),
				Version ver        => Equals(ver),
				IPAddress ip       => Equals(ip),
				_                  => false
			};
			//TODO: compare with int, long, ...?
		}

		/// <inheritdoc />
		public override bool Equals(JsonValue? other)
		{
			if (ReferenceEquals(other, this)) return true;
			if (other is null) return false;
			return other switch
			{
				JsonString str    => this.Equals(str),
				JsonNumber num    => num.Equals(this),
				JsonDateTime date => date.Equals(this),
				_                 => false
			};
		}

		/// <inheritdoc />
		public bool Equals(JsonString? other)
		{
			return other is not null && m_value ==  other.m_value;
		}

		/// <inheritdoc />
		public override bool StrictEquals(JsonValue? other) => other is JsonString s && m_value == s.Value;

		public bool StrictEquals(JsonString? other) => other is not null && other.Value == this.Value;

		/// <inheritdoc />
		public override bool ValueEquals<TValue>(TValue? value, IEqualityComparer<TValue>? comparer = null)
			where TValue : default
		{
			if (default(TValue) is null)
			{
				if (value is null) return false;

				if (typeof(TValue) == typeof(DateTime?)) return Equals((DateTime) (object) value);
				if (typeof(TValue) == typeof(DateTimeOffset?)) return Equals((DateTimeOffset) (object) value);
				if (typeof(TValue) == typeof(DateOnly?)) return Equals((DateOnly) (object) value);
				if (typeof(TValue) == typeof(TimeOnly?)) return Equals((TimeOnly) (object) value);
				if (typeof(TValue) == typeof(Guid?)) return Equals((Guid) (object) value);
				if (typeof(TValue) == typeof(Uuid128?)) return Equals((Uuid128) (object) value);
				if (typeof(TValue) == typeof(Uuid96?)) return Equals((Uuid96) (object) value);
				if (typeof(TValue) == typeof(Uuid80?)) return Equals((Uuid80) (object) value);
				if (typeof(TValue) == typeof(Uuid64?)) return Equals((Uuid64) (object) value);
				if (typeof(TValue) == typeof(NodaTime.Instant?)) return Equals((NodaTime.Instant) (object) value);
				if (typeof(TValue) == typeof(NodaTime.LocalDate?)) return Equals((NodaTime.LocalDate) (object) value);

				if (typeof(TValue) == typeof(string)) return Equals(Unsafe.As<string>(value));
				if (typeof(TValue) == typeof(Uri)) return Equals(Unsafe.As<Uri>(value));
				if (typeof(TValue) == typeof(Version)) return Equals(Unsafe.As<Version>(value));
				if (typeof(TValue) == typeof(IPAddress)) return Equals(Unsafe.As<IPAddress>(value));

				if (value is JsonValue j) return Equals(j);
			}
			else
			{
				if (typeof(TValue) == typeof(DateTime)) return Equals((DateTime) (object) value!);
				if (typeof(TValue) == typeof(DateTimeOffset)) return Equals((DateTimeOffset) (object) value!);
				if (typeof(TValue) == typeof(DateOnly)) return Equals((DateOnly) (object) value!);
				if (typeof(TValue) == typeof(TimeOnly)) return Equals((TimeOnly) (object) value!);
				if (typeof(TValue) == typeof(Guid)) return Equals((Guid) (object) value!);
				if (typeof(TValue) == typeof(Uuid128)) return Equals((Uuid128) (object) value!);
				if (typeof(TValue) == typeof(Uuid96)) return Equals((Uuid96) (object) value!);
				if (typeof(TValue) == typeof(Uuid80)) return Equals((Uuid80) (object) value!);
				if (typeof(TValue) == typeof(Uuid64)) return Equals((Uuid64) (object) value!);
				if (typeof(TValue) == typeof(NodaTime.Instant)) return Equals((NodaTime.Instant) (object) value!);
				if (typeof(TValue) == typeof(NodaTime.LocalDate)) return Equals((NodaTime.LocalDate) (object) value!);
			}

			return false;
		}

		/// <inheritdoc />
		public bool Equals(string? other)
		{
			return string.Equals(m_value, other);
		}

#if NET9_0_OR_GREATER
		/// <inheritdoc />
#endif
		public bool Equals(ReadOnlySpan<char> other) => m_value.AsSpan().SequenceEqual(other);

#if NET9_0_OR_GREATER
		/// <inheritdoc />
#endif
		public bool Equals(ReadOnlyMemory<char> other) => m_value.AsSpan().SequenceEqual(other.Span);

		/// <inheritdoc />
		public bool Equals(Guid other) => TryConvertToGuid(out var value) && value == other;

		/// <inheritdoc />
		public bool Equals(Uuid128 other) => TryConvertToUuid128(out var value) && value == other;

		/// <inheritdoc />
		public bool Equals(Uuid96 other) => TryConvertToUuid96(out var value) && value == other;

		/// <inheritdoc />
		public bool Equals(Uuid80 other) => TryConvertToUuid80(out var value) && value == other;

		/// <inheritdoc />
		public bool Equals(Uuid64 other) => TryConvertToUuid64(out var value) && value == other;

		/// <inheritdoc />
		public bool Equals(DateTime other) => TryConvertToDateTime(out var value) && value == other;

		/// <inheritdoc />
		public bool Equals(DateTimeOffset other) => TryConvertToDateTimeOffset(out var value) && value == other;

		/// <inheritdoc />
		public bool Equals(DateOnly other) => TryConvertToDateOnly(out var value) && value == other;

		/// <inheritdoc />
		public bool Equals(TimeOnly other) => TryConvertToTimeOnly(out var value) && value == other;

		/// <inheritdoc />
		public bool Equals(NodaTime.Instant other) => TryConvertToInstant(out var value) && value == other;

		/// <inheritdoc />
		public bool Equals(NodaTime.LocalDateTime other) => TryConvertToLocalDateTime(out var value) && value == other;

		/// <inheritdoc />
		public bool Equals(NodaTime.LocalDate other) => TryConvertToLocalDate(out var value) && value == other;

		/// <inheritdoc />
		public bool Equals(IPAddress? other) => other != null && TryConvertToIpAddress(out var value) && other.Equals(value);

		/// <inheritdoc />
		public bool Equals(Uri? other) => other != null && TryConvertToUri(out var value) && other.Equals(value);

		/// <inheritdoc />
		public bool Equals(Version? other) => other != null && TryConvertToVersion(out var value) && other.Equals(value);

		/// <inheritdoc />
		public override int GetHashCode() => m_value.GetHashCode();

		/// <inheritdoc />
		public override int CompareTo(JsonValue? other)
		{
			if (ReferenceEquals(other, this)) return 0;
			if (other is null) return +1;

			return other switch
			{
				JsonString str => CompareTo(str),
				JsonNumber num => -num.CompareTo(this), // use the JsonNumber.CompareTo(JsonString) implementation
				JsonNull => +1,
				_ => base.CompareTo(other)
			};
		}

		/// <inheritdoc />
		public int CompareTo(JsonString? other)
		{
			return other is not null ? string.CompareOrdinal(m_value, other.Value) : +1;
		}

		/// <inheritdoc />
		public int CompareTo(string? other)
		{
			return other is not null ? string.CompareOrdinal(m_value, other) : +1;
		}

#if NET9_0_OR_GREATER
		/// <inheritdoc />
#endif
		public int CompareTo(ReadOnlySpan<char> other)
		{
			return m_value.AsSpan().SequenceCompareTo(other);
		}

#if NET9_0_OR_GREATER
		/// <inheritdoc />
#endif
		public int CompareTo(ReadOnlyMemory<char> other)
		{
			return m_value.AsSpan().SequenceCompareTo(other.Span);
		}

		#endregion

		#region IJsonConvertible...

		#region String

		/// <inheritdoc />
		public override string ToJsonText(CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return settings?.TargetLanguage != CrystalJsonSettings.Target.JavaScript
				? JsonEncoding.Encode(m_value)
				: CrystalJsonFormatter.EncodeJavaScriptString(m_value);
		}

		/// <inheritdoc />
		public override string ToString() => m_value;

		/// <inheritdoc />
		public override string ToStringOrDefault(string? defaultValue = null) => m_value;

		public bool TryConvertToString(out string value)
		{
			value = m_value;
			return true;
		}

		#endregion

		#region Boolean

		/// <inheritdoc />
		public override bool ToBoolean(bool defaultValue = false)
		{
			return !string.IsNullOrEmpty(m_value) ? bool.Parse(m_value) : defaultValue;
		}

		/// <inheritdoc />
		public override bool? ToBooleanOrDefault(bool? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : bool.Parse(m_value);
		}

		#endregion

		#region Byte

		/// <inheritdoc />
		public override byte ToByte(byte defaultValue = 0) => string.IsNullOrEmpty(m_value) ? defaultValue : byte.Parse(m_value, NumberFormatInfo.InvariantInfo);

		/// <inheritdoc />
		public override byte? ToByteOrDefault(byte? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : byte.Parse(m_value, NumberFormatInfo.InvariantInfo);

		#endregion

		#region SByte

		/// <inheritdoc />
		public override sbyte ToSByte(sbyte defaultValue = 0)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : sbyte.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		/// <inheritdoc />
		public override sbyte? ToSByteOrDefault(sbyte? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : sbyte.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		#endregion

		#region Char

		/// <inheritdoc />
		public override char ToChar(char defaultValue = '\0')
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : m_value[0];
		}

		/// <inheritdoc />
		public override char? ToCharOrDefault(char? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : m_value[0];
		}

		#endregion

		#region Int16

		/// <inheritdoc />
		public override short ToInt16(short defaultValue = 0)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : short.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		/// <inheritdoc />
		public override short? ToInt16OrDefault(short? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToInt16();
		}

		public bool TryConvertToInt16(out short value)
		{
			return short.TryParse(m_value, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region UInt16

		/// <inheritdoc />
		public override ushort ToUInt16(ushort defaultValue = 0)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ushort.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		/// <inheritdoc />
		public override ushort? ToUInt16OrDefault(ushort? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToUInt16();
		}

		public bool TryConvertToUInt16(out ushort value)
		{
			return ushort.TryParse(m_value, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region Int32

		/// <inheritdoc />
		public override int ToInt32(int defaultValue = 0)
		{
			var value = m_value;
			if (string.IsNullOrEmpty(value))
			{
				return defaultValue;
			}

			if (value.Length == 1)
			{ // the most frequent case is a number between 0 and 9
				char c = value[0];
				if (c >= '0' & c <= '9') return c - '0';
			}
			//TODO: PERF: faster parsing ?
			return int.Parse(value, NumberFormatInfo.InvariantInfo);
		}

		/// <inheritdoc />
		public override int? ToInt32OrDefault(int? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : ToInt32();

		public bool TryConvertToInt32(out int value)
		{
			var lit = m_value;
			if (lit.Length == 0) { value = 0; return false; }
			if (lit.Length == 1)
			{ // the most frequent case is a number between 0 and 9
				char c = lit[0];
				if (c >= '0' & c <= '9') { value = c - '0'; return true; }
			}
			//TODO: PERF: faster parsing ?
			return int.TryParse(lit, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region UInt32

		/// <inheritdoc />
		public override uint ToUInt32(uint defaultValue = 0)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : uint.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		/// <inheritdoc />
		public override uint? ToUInt32OrDefault(uint? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToUInt32();
		}

		public bool TryConvertToUInt32(out uint value)
		{
			return uint.TryParse(m_value, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region Int64

		/// <inheritdoc />
		public override long ToInt64(long defaultValue = 0)
		{
			var value = m_value;
			if (string.IsNullOrEmpty(value))
			{
				return defaultValue;
			}

			if (value.Length == 1)
			{ // the most frequent case is a number between 0 and 9
				char c = value[0];
				if (c >= '0' & c <= '9') return c - '0';
			}

			//TODO: PERF: faster parsing ?
			return long.Parse(value, NumberFormatInfo.InvariantInfo);
		}

		/// <inheritdoc />
		public override long? ToInt64OrDefault(long? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToInt64();
		}

		public bool TryConvertToInt64(out long value)
		{
			var lit = m_value;
			if (lit.Length == 0) { value = 0L; return false; }
			if (lit.Length == 1)
			{ // le cas le plus fréquent est un nombre de 0 à 9
				char c = lit[0];
				if (c >= '0' & c <= '9') { value = c - '0'; return true; }
			}
			// note: NumberStyles obtenus via Reflector
			return long.TryParse(lit, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region UInt64

		/// <inheritdoc />
		public override ulong ToUInt64(ulong defaultValue = 0)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ulong.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		/// <inheritdoc />
		public override ulong? ToUInt64OrDefault(ulong? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToUInt64();
		}

		public bool TryConvertToUInt64(out ulong value)
		{
			return ulong.TryParse(m_value, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region Int128

#if NET8_0_OR_GREATER

		/// <inheritdoc />
		public override Int128 ToInt128(Int128 defaultValue = default)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : Int128.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		/// <inheritdoc />
		public override Int128? ToInt128OrDefault(Int128? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToInt128();
		}

		public bool TryConvertToInt128(out Int128 value)
		{
			return Int128.TryParse(m_value, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out value);
		}

#endif

		#endregion

		#region UInt128

#if NET8_0_OR_GREATER


		/// <inheritdoc />
		public override UInt128 ToUInt128(UInt128 defaultValue = default)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : UInt128.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		/// <inheritdoc />
		public override UInt128? ToUInt128OrDefault(UInt128? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToUInt128();
		}

		public bool TryConvertToUInt128(out UInt128 value)
		{
			return UInt128.TryParse(m_value, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out value);
		}

#endif

		#endregion

		#region Single

		/// <inheritdoc />
		public override float ToSingle(float defaultValue = 0)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : float.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		/// <inheritdoc />
		public override float? ToSingleOrDefault(float? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToSingle();
		}

		public bool TryConvertToSingle(out float value)
		{
			return float.TryParse(m_value, NumberStyles.Float | NumberStyles.AllowThousands, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region Double

		/// <inheritdoc />
		public override double ToDouble(double defaultValue = 0)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : double.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		/// <inheritdoc />
		public override double? ToDoubleOrDefault(double? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToDouble();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryConvertToDouble(out double value)
		{
			return TryConvertToDouble(m_value, out value);
		}

		internal static bool TryConvertToDouble(string literal, out double value)
		{
			return double.TryParse(literal, NumberStyles.Float | NumberStyles.AllowThousands, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region Half

		/// <inheritdoc />
		public override Half ToHalf(Half defaultValue = default)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : Half.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		/// <inheritdoc />
		public override Half? ToHalfOrDefault(Half? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : Half.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryConvertToHalf(out Half value)
		{
			return TryConvertToHalf(m_value, out value);
		}

		internal static bool TryConvertToHalf(string literal, out Half value)
		{
			return Half.TryParse(literal, NumberStyles.Float | NumberStyles.AllowThousands, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region Decimal

		/// <inheritdoc />
		public override decimal ToDecimal(decimal defaultValue = 0)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : decimal.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		/// <inheritdoc />
		public override decimal? ToDecimalOrDefault(decimal? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToDecimal();
		}

		public bool TryConvertToDecimal(out decimal value)
		{
			return decimal.TryParse(m_value, NumberStyles.Number, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region Guid

		/// <inheritdoc />
		public override Guid ToGuid(Guid defaultValue = default)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : Guid.Parse(m_value);
		}

		/// <inheritdoc />
		public override Guid? ToGuidOrDefault(Guid? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : Guid.Parse(m_value);
		}

		public bool TryConvertToGuid(out Guid result) => TryConvertToGuid(m_value, out result);

		internal static bool TryConvertToGuid(ReadOnlySpan<char> literal, out Guid result)
		{
			switch (literal.Length)
			{
				case 0:
				{
					result = Guid.Empty;
					return true;
				}
				case 36: /* 00000000-0000-0000-0000-000000000000 */
				case 32: /* 00000000000000000000000000000000 */
				case 38: /* {00000000-0000-0000-0000-000000000000} */
				{
					return Guid.TryParse(literal, out result);
				}
				default:
				{
					result = Guid.Empty;
					return false;
				}
			}
		}

		/// <inheritdoc />
		public override Uuid128 ToUuid128(Uuid128 defaultValue = default) => string.IsNullOrEmpty(m_value) ? defaultValue : Uuid128.Parse(m_value);

		/// <inheritdoc />
		public override Uuid128? ToUuid128OrDefault(Uuid128? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : Uuid128.Parse(m_value);

		public bool TryConvertToUuid128(out Uuid128 result) => TryConvertToUuid128(m_value, out result);

		internal static bool TryConvertToUuid128(ReadOnlySpan<char> literal, out Uuid128 result)
		{
			if (literal.Length == 0)
			{
				result = Uuid128.Empty;
				return true;
			}

			return Uuid128.TryParse(literal, out result);
		}

		/// <inheritdoc />
		public override Uuid96 ToUuid96(Uuid96 defaultValue = default) => string.IsNullOrEmpty(m_value) ? defaultValue : Uuid96.Parse(m_value);

		/// <inheritdoc />
		public override Uuid96? ToUuid96OrDefault(Uuid96? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : Uuid96.Parse(m_value);

		public bool TryConvertToUuid96(out Uuid96 result) => TryConvertToUuid96(m_value, out result);

		internal static bool TryConvertToUuid96(ReadOnlySpan<char> literal, out Uuid96 result)
		{
			if (literal.Length == 0)
			{
				result = Uuid96.Empty;
				return true;
			}

			return Uuid96.TryParse(literal, out result);
		}

		/// <inheritdoc />
		public override Uuid80 ToUuid80(Uuid80 defaultValue = default) => string.IsNullOrEmpty(m_value) ? defaultValue : Uuid80.Parse(m_value);

		/// <inheritdoc />
		public override Uuid80? ToUuid80OrDefault(Uuid80? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : Uuid80.Parse(m_value);

		public bool TryConvertToUuid80(out Uuid80 result) => TryConvertToUuid80(m_value, out result);

		internal static bool TryConvertToUuid80(ReadOnlySpan<char> literal, out Uuid80 result)
		{
			if (literal.Length == 0)
			{
				result = Uuid80.Empty;
				return true;
			}

			return Uuid80.TryParse(literal, out result);
		}

		/// <inheritdoc />
		public override Uuid64 ToUuid64(Uuid64 defaultValue = default) => string.IsNullOrEmpty(m_value) ? defaultValue : Uuid64.Parse(m_value);

		/// <inheritdoc />
		public override Uuid64? ToUuid64OrDefault(Uuid64? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : Uuid64.Parse(m_value);

		public bool TryConvertToUuid64(out Uuid64 result) => TryConvertToUuid64(m_value, out result);

		internal static bool TryConvertToUuid64(ReadOnlySpan<char> literal, out Uuid64 result)
		{
			if (literal.Length == 0)
			{
				result = Uuid64.Empty;
				return true;
			}

			return Uuid64.TryParse(literal, out result);
		}

		#endregion

		#region DateTime

		/// <inheritdoc />
		public override DateTime ToDateTime(DateTime defaultValue = default)
		{
			if (string.IsNullOrEmpty(m_value))
			{
				return defaultValue;
			}

			if (CrystalJsonParser.TryParseIso8601DateTime(m_value, out var d))
			{
				return d;
			}

			if (CrystalJsonParser.TryParseMicrosoftDateTime(m_value, out d, out var tz))
			{
				if (tz.HasValue)
				{
					var utcOffset = TimeZoneInfo.Local.GetUtcOffset(d).Subtract(tz.Value);
					return d.ToLocalTime().Add(utcOffset);
				}
				return d;
			}

			return StringConverters.ParseDateTime(m_value);
		}

		/// <inheritdoc />
		public override DateTime? ToDateTimeOrDefault(DateTime? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToDateTime();
		}

		public bool TryConvertToDateTime(out DateTime result)
		{
			return TryConvertToDateTime(m_value, out result);
		}

		internal static bool TryConvertToDateTime(ReadOnlySpan<char> literal, out DateTime result)
		{
			if (literal.Length == 0)
			{
				result = default;
				return true;
			}

			if (CrystalJsonParser.TryParseIso8601DateTime(literal, out result))
			{
				return true;
			}

			if (CrystalJsonParser.TryParseMicrosoftDateTime(literal, out result, out var tz))
			{
				if (tz.HasValue)
				{
					var utcOffset = TimeZoneInfo.Local.GetUtcOffset(result).Subtract(tz.Value);
					result = result.ToLocalTime().Add(utcOffset);
				}
				return true;
			}

			return StringConverters.TryParseDateTime(literal, CultureInfo.InvariantCulture, out result, false);
		}


		/// <inheritdoc />
		public override DateOnly ToDateOnly(DateOnly defaultValue = default)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : DateOnly.FromDateTime(ToDateTime());
		}

		/// <inheritdoc />
		public override DateOnly? ToDateOnlyOrDefault(DateOnly? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToDateOnly();
		}

		public bool TryConvertToDateOnly(out DateOnly result)
		{
			return TryConvertToDateOnly(m_value, out result);
		}

		internal static bool TryConvertToDateOnly(ReadOnlySpan<char> literal, out DateOnly result)
		{
			if (!TryConvertToDateTime(literal, out var dt))
			{
				result = default;
				return false;
			}

			result = DateOnly.FromDateTime(dt);
			return true;
		}

		/// <inheritdoc />
		public override TimeOnly ToTimeOnly(TimeOnly defaultValue = default)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : TimeOnly.FromTimeSpan(ToTimeSpan());
		}

		/// <inheritdoc />
		public override TimeOnly? ToTimeOnlyOrDefault(TimeOnly? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToTimeOnly();
		}

		public bool TryConvertToTimeOnly(out TimeOnly result) => TryConvertToTimeOnly(m_value, out result);

		internal static bool TryConvertToTimeOnly(ReadOnlySpan<char> literal, out TimeOnly result)
		{
			if (!TryConvertToTimeSpan(literal, out var ts))
			{
				result = default;
				return false;
			}

			result = TimeOnly.FromTimeSpan(ts);
			return true;
		}

		#endregion

		#region DateTimeOffset

		/// <inheritdoc />
		public override DateTimeOffset ToDateTimeOffset(DateTimeOffset  defaultValue = default)
		{
			if (m_value.Length == 0)
			{
				return defaultValue;
			}

			if (CrystalJsonParser.TryParseIso8601DateTimeOffset(m_value, out var dto))
			{
				return dto;
			}

			if (CrystalJsonParser.TryParseMicrosoftDateTime(m_value, out var d, out var tz))
			{
				if (tz.HasValue)
				{
					return new(d, tz.Value);
				}
				return new(d);
			}

			return new(StringConverters.ParseDateTime(m_value));
		}

		/// <inheritdoc />
		public override DateTimeOffset? ToDateTimeOffsetOrDefault(DateTimeOffset? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToDateTimeOffset();
		}

		public bool TryConvertToDateTimeOffset(out DateTimeOffset dto)
		{
			return TryConvertToDateTimeOffset(m_value, out dto);
		}

		internal static bool TryConvertToDateTimeOffset(ReadOnlySpan<char> literal, out DateTimeOffset dto)
		{
			if (literal.Length == 0)
			{
				dto = default;
				return false;
			}

			if (CrystalJsonParser.TryParseIso8601DateTimeOffset(literal, out dto))
			{
				return true;
			}

			if (CrystalJsonParser.TryParseMicrosoftDateTime(literal, out var dt, out var tz))
			{
				dto = tz.HasValue ? new DateTimeOffset(dt, tz.Value) : new DateTimeOffset(dt);
				return true;
			}

			if (!StringConverters.TryParseDateTime(literal, CultureInfo.InvariantCulture, out dt, false))
			{
				dto = default;
				return false;
			}
			
			// correct for MinValue/MaxValue; otherwise, the DateTimeOffset ctor will throw if the server is GMT+XX
			dto = dt == DateTime.MinValue ? DateTimeOffset.MinValue
				: dt == DateTime.MaxValue ? DateTimeOffset.MaxValue
				: new DateTimeOffset(dt);
			return true;
		}

		#endregion

		#region TimeSpan

		/// <inheritdoc />
		public override TimeSpan ToTimeSpan(TimeSpan defaultValue = default)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : TimeSpan.Parse(m_value, CultureInfo.InvariantCulture);
		}

		/// <inheritdoc />
		public override TimeSpan? ToTimeSpanOrDefault(TimeSpan? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : ToTimeSpan();

		public bool TryConvertToTimeSpan(out TimeSpan result) => TryConvertToTimeSpan(m_value, out result);

		internal static bool TryConvertToTimeSpan(ReadOnlySpan<char> literal, out TimeSpan result)
		{
			if (literal.Length == 0)
			{
				result = TimeSpan.Zero;
				return true;
			}

			return TimeSpan.TryParse(literal, CultureInfo.InvariantCulture, out result);
		}

		#endregion

		#region NodaTime

		/// <inheritdoc />
		public override NodaTime.Instant ToInstant(NodaTime.Instant defaultValue = default)
		{
			if (string.IsNullOrEmpty(m_value))
			{
				return defaultValue;
			}

			var parseResult = CrystalJsonNodaPatterns.Instants.Parse(m_value);
			if (parseResult.TryGetValue(default(NodaTime.Instant), out var instant))
			{
				return instant;
			}

			// this does not look like an "Instant", try going the DateTimeOffset route...
			var dateTimeOffset = ToDateTimeOffset();
			return NodaTime.Instant.FromDateTimeOffset(dateTimeOffset);
		}

		/// <inheritdoc />
		public override NodaTime.Instant? ToInstantOrDefault(NodaTime.Instant? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : ToInstant();

		public bool TryConvertToInstant(out NodaTime.Instant result) => TryConvertToInstant(m_value, out result);

		internal static bool TryConvertToInstant(string? literal, out NodaTime.Instant result)
		{
			//note: NodaTime does not currently support parsing from span, so we have to take a string as input for the moment!
			// see https://github.com/nodatime/nodatime/issues/1131

			if (string.IsNullOrEmpty(literal))
			{
				result = default;
				return true;
			}

			var parseResult = CrystalJsonNodaPatterns.Instants.Parse(literal);
			if (parseResult.TryGetValue(default(NodaTime.Instant), out result))
			{
				return true;
			}

			// this does not look like an "Instant", try going the DateTimeOffset route...
			if (TryConvertToDateTimeOffset(literal, out var dateTimeOffset))
			{
				result = NodaTime.Instant.FromDateTimeOffset(dateTimeOffset);
				return true;
			}

			result = default;
			return false;
		}

		/// <inheritdoc />
		public override NodaTime.Duration ToDuration(NodaTime.Duration defaultValue = default)
		{
			string value = m_value;
			return string.IsNullOrEmpty(value)
				? defaultValue
				: NodaTime.Duration.FromTicks((long) (double.Parse(value) * NodaTime.NodaConstants.TicksPerSecond));
		}

		/// <inheritdoc />
		public override NodaTime.Duration? ToDurationOrDefault(NodaTime.Duration? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : ToDuration();

		public NodaTime.LocalDateTime ToLocalDateTime(NodaTime.LocalDateTime defaultValue = default)
		{
			string value = m_value;
			if (string.IsNullOrEmpty(value))
			{
				return defaultValue;
			}

			if (value[^1] == 'Z')
			{ // this is a UTC date, probably an Instant
				//HACK: not pretty, but we assume the original intention was to store a local time, so we to a local time as well
				// => this will NOT work as intended if the server that serialized the JSON value is in a different timezone, but then the app should have used Instant, or a ZonedDateTime instead!
				return NodaTime.LocalDateTime.FromDateTime(ToInstant().ToDateTimeUtc().ToLocalTime());
			}

			return CrystalJsonNodaPatterns.LocalDateTimes.Parse(value).Value;
		}

		public NodaTime.LocalDateTime? ToLocalDateTimeOrDefault(NodaTime.LocalDateTime? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : ToLocalDateTime();

		public bool TryConvertToLocalDateTime(out NodaTime.LocalDateTime result) => TryConvertToLocalDateTime(m_value, out result);

		internal static bool TryConvertToLocalDateTime(string? literal, out NodaTime.LocalDateTime result)
		{
			if (string.IsNullOrEmpty(literal))
			{
				result = default;
				return true;
			}

			if (literal[^1] == 'Z')
			{ // this is a UTC date, probably an Instant
				//HACK: not pretty, but we assume the original intention was to store a local time, so we convert to a local time as well
				// => this will NOT work as intended if the server that serialized the JSON value is in a different timezone, but then the app should have used Instant, or a ZonedDateTime instead!
				if (TryConvertToInstant(literal, out var value))
				{
					result = NodaTime.LocalDateTime.FromDateTime(value.ToDateTimeUtc().ToLocalTime());
					return true;
				}

				result = default;
				return false;
			}

			var parseResult = CrystalJsonNodaPatterns.LocalDateTimes.Parse(literal);
			if (parseResult.TryGetValue(default, out result))
			{
				return true;
			}
			result = default;
			return false;
		}

		public NodaTime.ZonedDateTime ToZonedDateTime(NodaTime.ZonedDateTime defaultValue = default)
		{
			string value = m_value;
			return string.IsNullOrEmpty(value) ? defaultValue : CrystalJsonNodaPatterns.ZonedDateTimes.Parse(value).Value;
		}

		public NodaTime.ZonedDateTime? ToZonedDateTimeOrDefault(NodaTime.ZonedDateTime? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : ToZonedDateTime();

		public NodaTime.OffsetDateTime ToOffsetDateTime(NodaTime.OffsetDateTime defaultValue = default)
		{
			string value = m_value;
			return string.IsNullOrEmpty(value) ? defaultValue : CrystalJsonNodaPatterns.OffsetDateTimes.Parse(value).Value;
		}

		public NodaTime.OffsetDateTime? ToOffsetDateTimeOrDefault(NodaTime.OffsetDateTime? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : ToOffsetDateTime();

		public NodaTime.DateTimeZone? ToDateTimeZone(NodaTime.DateTimeZone? defaultValue = null)
		{
			string value = m_value;
			return string.IsNullOrEmpty(value) ? defaultValue : (NodaTime.DateTimeZoneProviders.Tzdb.GetZoneOrNull(value));
		}

		public NodaTime.Offset ToOffset(NodaTime.Offset defaultValue = default)
		{
			string value = m_value;
			return string.IsNullOrEmpty(value) ? defaultValue : CrystalJsonNodaPatterns.Offsets.Parse(value).Value;
		}

		public NodaTime.Offset? ToOffsetOrDefault(NodaTime.Offset? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : ToOffset();

		public NodaTime.LocalDate ToLocalDate(NodaTime.LocalDate defaultValue = default)
		{
			string value = m_value;
			return string.IsNullOrEmpty(value) ? defaultValue : CrystalJsonNodaPatterns.LocalDates.Parse(value).Value;
		}

		public NodaTime.LocalDate? ToLocalDateOrDefault(NodaTime.LocalDate? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : ToLocalDate();

		public bool TryConvertToLocalDate(out NodaTime.LocalDate result) => TryConvertToLocalDate(m_value, out result);

		internal static bool TryConvertToLocalDate(string? literal, out NodaTime.LocalDate result)
		{
			if (string.IsNullOrEmpty(literal))
			{
				result = default;
				return true;
			}

			var parseResult = CrystalJsonNodaPatterns.LocalDates.Parse(literal);
			if (parseResult.TryGetValue(default, out result))
			{
				return true;
			}

			result = default;
			return false;
		}

		#endregion

		#region Enums

		/// <inheritdoc />
		public override TEnum ToEnum<TEnum>(TEnum defaultValue = default) => string.IsNullOrEmpty(m_value) ? defaultValue : Enum.Parse<TEnum>(m_value, ignoreCase: true);

		/// <inheritdoc />
		public override TEnum? ToEnumOrDefault<TEnum>(TEnum? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : Enum.Parse<TEnum>(m_value, ignoreCase: true);

		#endregion

		#region Uri

		public Uri? ToUri()
		{
			//note: new Uri("") is not valid, so we return null if this is the case
			return string.IsNullOrEmpty(m_value) ? null : new Uri(m_value);
		}

		public bool TryConvertToUri([MaybeNullWhen(false)] out Uri result) => TryConvertToUri(m_value, out result);

		internal static bool TryConvertToUri(string? literal, [MaybeNullWhen(false)] out Uri result)
		{
			return Uri.TryCreate(literal, UriKind.RelativeOrAbsolute, out result);
		}

		#endregion

		#region IPAddress

		public IPAddress? ToIpAddress()
		{
			return string.IsNullOrEmpty(m_value) ? null : System.Net.IPAddress.Parse(m_value);
		}

		public bool TryConvertToIpAddress([MaybeNullWhen(false)] out IPAddress result) => TryConvertToIpAddress(m_value, out result);

		internal static bool TryConvertToIpAddress(ReadOnlySpan<char> literal, [MaybeNullWhen(false)] out IPAddress result)
		{
			return IPAddress.TryParse(literal, out result);
		}

		#endregion

		#region Version

		public Version? ToVersion()
		{
			return string.IsNullOrEmpty(m_value) ? null : Version.Parse(m_value);
		}

		public bool TryConvertToVersion([MaybeNullWhen(false)] out Version result) => TryConvertToVersion(m_value, out result);

		internal static bool TryConvertToVersion(ReadOnlySpan<char> literal, [MaybeNullWhen(false)] out Version result)
		{
			return Version.TryParse(literal, out result);
		}

		#endregion

		#endregion

		#region ISliceSerializable...

		/// <inheritdoc />
		public override void WriteTo(ref SliceWriter writer)
		{
			var value = m_value;
			if (string.IsNullOrEmpty(value))
			{ // "" => 22 22
				writer.WriteInt16(0x2222);
				return;
			}

			if (JsonEncoding.NeedsEscaping(value))
			{
				writer.WriteStringUtf8(JsonEncoding.EncodeSlow(value));
				return;
			}

			writer.WriteByte('"');
			writer.WriteStringUtf8(m_value);
			writer.WriteByte('"');
		}

		#endregion

		public static class WellKnown
		{
			/// <summary>PascalCase</summary>
			public static class PascalCase
			{
				/// <summary><c>"Id"</c></summary>
				public static readonly JsonString Id = new("Id");
				/// <summary><c>"Name"</c></summary>
				public static readonly JsonString Name = new("Name");
				/// <summary><c>"Error"</c></summary>
				public static readonly JsonString Error = new("Error");
			}

			/// <summary>camelCase</summary>
			public static class CamelCase
			{
				/// <summary><c>"id"</c></summary>
				public static readonly JsonString Id = new("id");
				/// <summary><c>"name"</c></summary>
				public static readonly JsonString Name = new("name");
				/// <summary><c>"error"</c></summary>
				public static readonly JsonString Error = new("error");
			}

		}

	}

}
