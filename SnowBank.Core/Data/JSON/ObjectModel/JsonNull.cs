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
	using SnowBank.Buffers;

	/// <summary>JSON null</summary>
	[DebuggerDisplay("JSON Null({m_kind})")]
	[PublicAPI]
	[System.Text.Json.Serialization.JsonConverter(typeof(CrystalJsonCustomJsonConverter))]
	[DebuggerNonUserCode]
	public sealed class JsonNull : JsonValue, IEquatable<JsonNull>
	{
		internal enum NullKind
		{
			Null = 0,
			Missing,
			Error
		}

		/// <summary>Explicit null value</summary>
		/// <remarks>This singleton is used to represent values that are present in the JSON document, usually represents by the <c>null</c> token.</remarks>
		public static readonly JsonValue Null = new JsonNull(NullKind.Null);

		/// <summary>Missing value</summary>
		/// <remarks>This singleton is returned when </remarks>
		public static readonly JsonValue Missing = new JsonNull(NullKind.Missing);

		/// <summary>Result of an invalid operation</summary>
		/// <remarks>
		/// <para>This singleton is used to represent a null or missing value, which is due to some sort of error condition</para>
		/// <para>For example, attempting to index an object (ex: obj[123]) or access the field of an array (ex: arr["foo"]) are 'soft errors'</para>
		/// </remarks>
		public static readonly JsonValue Error = new JsonNull(NullKind.Error);

		/// <summary>Type of null</summary>
		private readonly NullKind m_kind;

		private JsonNull(NullKind kind)
		{
			m_kind = kind;
		}

		/// <inheritdoc />
		public override string ToJsonText(CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null) => JsonTokens.Null;

		/// <inheritdoc />
		public override string ToString() => string.Empty;

		/// <inheritdoc />
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public override string? ToStringOrDefault(string? defaultValue = null) => defaultValue;

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static object? ValueTypeDefault(Type type)
		{
			// we can only return singletons for immutable "struct" values
			if (type == typeof(int)) return BoxedZeroInt32;
			if (type == typeof(long)) return BoxedZeroInt64;
			if (type == typeof(bool)) return BoxedFalse;
			if (type == typeof(double)) return BoxedZeroDouble;
			if (type == typeof(float)) return BoxedZeroSingle;
			if (type == typeof(Guid)) return BoxedGuidEmpty;
			if (type == typeof(Uuid128)) return BoxedUuid128Empty;
			if (type == typeof(TimeSpan)) return BoxedTimeSpanZero;

#pragma warning disable IL2067
			// for all other cases, we have to return a new value
			return Activator.CreateInstance(type, nonPublic: false);
#pragma warning restore IL2067
		}

		#region JsonValue Members

		/// <inheritdoc />
		public override JsonType Type => JsonType.Null;

		/// <inheritdoc />
		public override object? ToObject() => null;

		/// <inheritdoc />
		public override TValue? Bind<TValue>(TValue? defaultValue = default, ICrystalJsonTypeResolver? resolver = null)
			where TValue : default
		{
			if (default(TValue) is null && (typeof(TValue) == typeof(JsonValue) || typeof(TValue) == typeof(JsonNull)))
			{
				return defaultValue ?? (TValue?) (object) this;
			}
			return defaultValue;
		}

		/// <inheritdoc />
		public override object? Bind(Type? type, ICrystalJsonTypeResolver? resolver = null)
		{
			// If we bind to JsonValue (or JsonNull), we must keep the same singleton (JsonNull.Null, JsonNull.Missing, ...)
			if (type == typeof(JsonValue) || type == typeof(JsonNull))
			{
				return this;
			}

			// In all other cases, we must return the default for this type
			// - If this is a ValueType, we have to return a boxed default(T), or we could cause a nullref when casting to (T) !
			if (type?.IsValueType ?? false)
			{
				return ValueTypeDefault(type);
			}
			return null;
		}

		// cached singletons for the most common default values for ValueTypes
		private static readonly object BoxedFalse = false;
		private static readonly object BoxedZeroInt32 = 0;
		private static readonly object BoxedZeroInt64 = 0L;
		private static readonly object BoxedZeroSingle = 0f;
		private static readonly object BoxedZeroDouble = 0d;
		private static readonly object BoxedTimeSpanZero = TimeSpan.Zero;
		private static readonly object BoxedGuidEmpty = Guid.Empty;
		private static readonly object BoxedUuid128Empty = Uuid128.Empty;

		/// <inheritdoc />
		public override bool IsNull => true;

		/// <inheritdoc />
		public override bool IsDefault => true;

		/// <inheritdoc />
		public override bool IsReadOnly => true; //note: null is immutable

		/// <summary>Tests if this is the <see cref="JsonNull.Missing"/> singleton</summary>
		public bool IsMissing
		{
			[Pure]
			get => m_kind == NullKind.Missing;
		}

		/// <summary>Tests if this is the <see cref="JsonNull.Error"/> singleton</summary>
		public bool IsError
		{
			[Pure]
			get => m_kind == NullKind.Error;
		}

		/// <inheritdoc />
		[AllowNull] // setter only
		public override JsonValue this[int index]
		{
			get => ReferenceEquals(this, JsonNull.Error) ? this : JsonNull.Missing;
			set => throw FailCannotMutateImmutableValue(this);
		}

		/// <inheritdoc />
		[AllowNull] // setter only
		public override JsonValue this[Index key]
		{
			get => ReferenceEquals(this, JsonNull.Error) ? this : JsonNull.Missing;
			set => throw FailCannotMutateImmutableValue(this);
		}

		/// <inheritdoc />
		[AllowNull] // setter only
		public override JsonValue this[string key]
		{
			get => ReferenceEquals(this, JsonNull.Error) ? this : JsonNull.Missing;
			set => throw FailCannotMutateImmutableValue(this);
		}

		/// <inheritdoc />
		[AllowNull] // setter only
		public override JsonValue this[ReadOnlySpan<char> key]
		{
			get => ReferenceEquals(this, JsonNull.Error) ? this : JsonNull.Missing;
			set => throw FailCannotMutateImmutableValue(this);
		}

		/// <inheritdoc />
		[AllowNull] // setter only
		public override JsonValue this[ReadOnlyMemory<char> key]
		{
			get => ReferenceEquals(this, JsonNull.Error) ? this : JsonNull.Missing;
			set => throw FailCannotMutateImmutableValue(this);
		}

		/// <inheritdoc />
		public override JsonValue GetValueOrDefault(string key, JsonValue? defaultValue = null) => defaultValue ?? JsonNull.Missing;

		/// <inheritdoc />
		public override JsonValue GetValueOrDefault(ReadOnlyMemory<char> key, JsonValue? defaultValue = null) => defaultValue ?? JsonNull.Missing;

		/// <inheritdoc />
		public override JsonValue GetValueOrDefault(ReadOnlySpan<char> key, JsonValue? defaultValue = null) => defaultValue ?? JsonNull.Missing;

#if NET9_0_OR_GREATER

		/// <inheritdoc />
		public override JsonValue GetValueOrDefault(ReadOnlyMemory<char> key, JsonValue? defaultValue, out string? actualKey)
		{
			actualKey = null;
			return defaultValue ?? JsonNull.Missing;
		}

		/// <inheritdoc />
		public override JsonValue GetValueOrDefault(ReadOnlySpan<char> key, JsonValue? defaultValue, out string? actualKey)
		{
			actualKey = null;
			return defaultValue ?? JsonNull.Missing;
		}

#endif

		/// <inheritdoc />
		public override JsonValue GetValueOrDefault(int index, JsonValue? defaultValue = null) => defaultValue ?? JsonNull.Missing;

		/// <inheritdoc />
		public override JsonValue GetValueOrDefault(Index index, JsonValue? defaultValue = null) => defaultValue ?? JsonNull.Missing;

		/// <inheritdoc />
		[DoesNotReturn, EditorBrowsable(EditorBrowsableState.Never)]
		public override JsonValue GetValue(string key) => JsonValueExtensions.FailFieldIsNullOrMissing(this, key);

		/// <inheritdoc />
		[DoesNotReturn, EditorBrowsable(EditorBrowsableState.Never)]
		public override JsonValue GetValue(ReadOnlySpan<char> key) => JsonValueExtensions.FailFieldIsNullOrMissing(this, key);

		/// <inheritdoc />
		[DoesNotReturn, EditorBrowsable(EditorBrowsableState.Never)]
		public override JsonValue GetValue(ReadOnlyMemory<char> key) => JsonValueExtensions.FailFieldIsNullOrMissing(this, key);

#if NET9_0_OR_GREATER

		/// <inheritdoc />
		[DoesNotReturn, EditorBrowsable(EditorBrowsableState.Never)]
		public override JsonValue GetValue(ReadOnlySpan<char> key, out string actualKey) => throw CrystalJson.Errors.Parsing_FieldIsNullOrMissing(this, key.ToString(), null);

		/// <inheritdoc />
		[DoesNotReturn, EditorBrowsable(EditorBrowsableState.Never)]
		public override JsonValue GetValue(ReadOnlyMemory<char> key, out string actualKey) => throw CrystalJson.Errors.Parsing_FieldIsNullOrMissing(this, key.GetStringOrCopy(), null);

#endif

		/// <inheritdoc />
		[DoesNotReturn, EditorBrowsable(EditorBrowsableState.Never)]
		public override JsonValue GetValue(int index) => JsonValueExtensions.FailIndexIsNullOrMissing(index, JsonNull.Error);

		/// <inheritdoc />
		[DoesNotReturn, EditorBrowsable(EditorBrowsableState.Never)]
		public override JsonValue GetValue(Index index) => JsonValueExtensions.FailIndexIsNullOrMissing(index, JsonNull.Error);

		/// <inheritdoc />
		public override bool Contains(JsonValue? value) => false;

		/// <inheritdoc />
		internal override bool IsSmallValue() => true;

		/// <inheritdoc />
		internal override bool IsInlinable() => true;

		#endregion

		#region IJsonSerializable...

		/// <inheritdoc />
		public override void JsonSerialize(CrystalJsonWriter writer)
		{
			writer.WriteNull(); // "null"
		}

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
		{
			return "null".TryCopyTo(destination, out charsWritten);
		}

#if NET8_0_OR_GREATER

		/// <inheritdoc />
		public override bool TryFormat(Span<byte> destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
		{
			return "null"u8.TryCopyTo(destination, out bytesWritten);
		}

#endif

		#endregion

		#region Equality Operators...

		/// <inheritdoc />
		public override bool Equals(object? obj)
		{
			// default(object) and DBNull are both considered to be equal to a JsonNull instance
			if (obj is null or DBNull) return true;

			return base.Equals(obj);
		}

		/// <inheritdoc />
		public override bool Equals(JsonValue? value)
		{
			if (value is null)
			{ // explicit null reference is considered equal to any kind of nulls
				return true;
			}

			if (value is not JsonNull jn)
			{ // non-null value
				return false;
			}

			// if both are JsonNull, they must have the same kind
			return m_kind == jn.m_kind;
		}

		/// <inheritdoc />
		public bool Equals(JsonNull? value)
		{
			return value is null || m_kind == value.m_kind;
		}

		/// <inheritdoc />
		public override bool StrictEquals(JsonValue? other) => other is JsonNull n && n.m_kind == m_kind;

		/// <inheritdoc cref="StrictEquals(SnowBank.Data.Json.JsonValue?)"/>
		public bool StrictEquals(JsonNull? other) => other is not null && other.m_kind == m_kind;

		/// <inheritdoc />
		public override bool ValueEquals<TValue>(TValue? value, IEqualityComparer<TValue>? comparer = null)
			where TValue : default
		{
			if (default(TValue) is null)
			{
				if (value is null) return true;
				if (value is JsonNull jn) return m_kind == jn.m_kind;
			}
			return false;
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return 0;
		}

		/// <inheritdoc />
		public override int CompareTo(JsonValue? value)
		{
			if (value is null)
			{ // nullref is equal to all kind of nulls
				return 0;
			}

			if (value is not JsonNull jn)
			{ // null is always smaller than any other values
				return -1;
			}

			// between nulls, the order is Null < Missing < Error, as ordered in the NullKind enum
			return m_kind.CompareTo(jn.m_kind);
		}

		/// <summary>Returns always <see langword="false"/></summary>
		public static explicit operator bool(JsonNull obj)
		{
			return false;
		}

		#endregion

		#region IJsonConvertible...

		/// <inheritdoc />
		public override bool ToBoolean(bool defaultValue = false) => defaultValue;

		/// <inheritdoc />
		public override bool? ToBooleanOrDefault(bool? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override byte ToByte(byte defaultValue = 0) => defaultValue;

		/// <inheritdoc />
		public override byte? ToByteOrDefault(byte? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override sbyte ToSByte(sbyte defaultValue = 0) => defaultValue;

		/// <inheritdoc />
		public override sbyte? ToSByteOrDefault(sbyte? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override char ToChar(char defaultValue = '\0') => defaultValue;

		/// <inheritdoc />
		public override char? ToCharOrDefault(char? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override short ToInt16(short defaultValue = 0) => defaultValue;

		/// <inheritdoc />
		public override short? ToInt16OrDefault(short? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override ushort ToUInt16(ushort defaultValue = 0) => defaultValue;

		/// <inheritdoc />
		public override ushort? ToUInt16OrDefault(ushort? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override int ToInt32(int defaultValue = 0) => defaultValue;

		/// <inheritdoc />
		public override int? ToInt32OrDefault(int? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override uint ToUInt32(uint defaultValue = 0U) => defaultValue;

		/// <inheritdoc />
		public override uint? ToUInt32OrDefault(uint? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override long ToInt64(long defaultValue = 0L) => defaultValue;

		/// <inheritdoc />
		public override long? ToInt64OrDefault(long? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override ulong ToUInt64(ulong defaultValue = 0UL) => defaultValue;

		/// <inheritdoc />
		public override ulong? ToUInt64OrDefault(ulong? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override float ToSingle(float defaultValue = 0f) => defaultValue;

		/// <inheritdoc />
		public override float? ToSingleOrDefault(float? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override double ToDouble(double defaultValue = 0d) => defaultValue;

		/// <inheritdoc />
		public override double? ToDoubleOrDefault(double? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override Half ToHalf(Half defaultValue = default) => defaultValue;

		/// <inheritdoc />
		public override Half? ToHalfOrDefault(Half? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override decimal ToDecimal(decimal defaultValue = 0m) => defaultValue;

		/// <inheritdoc />
		public override decimal? ToDecimalOrDefault(decimal? defaultValue = null) => defaultValue;

#if NET8_0_OR_GREATER

		/// <inheritdoc />
		public override Int128 ToInt128(Int128 defaultValue = default) => defaultValue;

		/// <inheritdoc />
		public override Int128? ToInt128OrDefault(Int128? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override UInt128 ToUInt128(UInt128 defaultValue = default) => defaultValue;

		/// <inheritdoc />
		public override UInt128? ToUInt128OrDefault(UInt128? defaultValue = null) => defaultValue;

#endif

		/// <inheritdoc />
		public override Guid ToGuid(Guid defaultValue = default) => defaultValue;

		/// <inheritdoc />
		public override Guid? ToGuidOrDefault(Guid? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override Uuid128 ToUuid128(Uuid128 defaultValue = default) => defaultValue;

		/// <inheritdoc />
		public override Uuid128? ToUuid128OrDefault(Uuid128? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override Uuid96 ToUuid96(Uuid96 defaultValue = default) => defaultValue;

		/// <inheritdoc />
		public override Uuid96? ToUuid96OrDefault(Uuid96? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override Uuid80 ToUuid80(Uuid80 defaultValue = default) => defaultValue;

		/// <inheritdoc />
		public override Uuid80? ToUuid80OrDefault(Uuid80? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override Uuid64 ToUuid64(Uuid64 defaultValue = default) => defaultValue;

		/// <inheritdoc />
		public override Uuid64? ToUuid64OrDefault(Uuid64? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override Uuid48 ToUuid48(Uuid48 defaultValue = default) => defaultValue;

		/// <inheritdoc />
		public override Uuid48? ToUuid48OrDefault(Uuid48? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override DateTime ToDateTime(DateTime defaultValue = default) => defaultValue;

		/// <inheritdoc />
		public override DateTime? ToDateTimeOrDefault(DateTime? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override DateTimeOffset ToDateTimeOffset(DateTimeOffset defaultValue = default) => defaultValue;

		/// <inheritdoc />
		public override DateTimeOffset? ToDateTimeOffsetOrDefault(DateTimeOffset? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override DateOnly ToDateOnly(DateOnly defaultValue = default) => defaultValue;

		/// <inheritdoc />
		public override DateOnly? ToDateOnlyOrDefault(DateOnly? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override TimeOnly ToTimeOnly(TimeOnly defaultValue = default) => defaultValue;

		/// <inheritdoc />
		public override TimeOnly? ToTimeOnlyOrDefault(TimeOnly? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override TimeSpan ToTimeSpan(TimeSpan defaultValue = default) => defaultValue;

		/// <inheritdoc />
		public override TimeSpan? ToTimeSpanOrDefault(TimeSpan? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override TEnum ToEnum<TEnum>(TEnum defaultValue = default) => defaultValue;

		/// <inheritdoc />
		public override TEnum? ToEnumOrDefault<TEnum>(TEnum? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override NodaTime.Instant ToInstant(NodaTime.Instant defaultValue = default) => defaultValue;

		/// <inheritdoc />
		public override NodaTime.Instant? ToInstantOrDefault(NodaTime.Instant? defaultValue = null) => defaultValue;

		/// <inheritdoc />
		public override NodaTime.Duration ToDuration(NodaTime.Duration defaultValue = default) => defaultValue;

		/// <inheritdoc />
		public override NodaTime.Duration? ToDurationOrDefault(NodaTime.Duration? defaultValue = null) => defaultValue;

		#endregion

		#region ISliceSerializable...

		/// <inheritdoc />
		public override void WriteTo(ref SliceWriter writer)
		{
			// 'null' => 6E 75 6C 6C
			writer.WriteInt32(0x6C6C756E);
		}

		#endregion

	}

}
