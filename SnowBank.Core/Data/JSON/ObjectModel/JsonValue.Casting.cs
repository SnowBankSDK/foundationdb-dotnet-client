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
	using System.Collections.Immutable;
	using System.Net;
	using System.Text;

	public abstract partial class JsonValue
	{

		#region String...

		[Pure]
		public static implicit operator JsonValue(string? value) => JsonString.Return(value);

		[Pure]
		public static explicit operator string?(JsonValue? value)
		{
			//note: JsonNull.Null.ToString() returns String.Empty which is not what we want
			// => we need to call ToStringOrDefault() that will return "null" as expected
			return (value ?? JsonNull.Null).ToStringOrDefault();
		}

		[Pure]
		public static implicit operator JsonValue(StringBuilder? value) => JsonString.Return(value);

		[Pure]
		public static explicit operator JsonValue(char[]? value)
		{
			return value is null ? JsonNull.Null : JsonString.Return(value, 0, value.Length);
		}

		public static explicit operator JsonValue(ReadOnlySpan<char> value) => JsonString.Return(value);

		#endregion

		#region Byte[]

		[Pure]
		public static implicit operator JsonValue(byte[]? value)
		{
			return value is null ? JsonNull.Null : JsonString.Return(value);
		}

		[ Pure]
		public static explicit operator byte[]?(JsonValue? value)
		{
			if (value is null || value.IsNull) return null;

			if (value.Type == JsonType.String)
			{
				return Convert.FromBase64String(((JsonString)value).Value);
			}

			throw Errors.JsonConversionNotSupported(value, typeof(byte[]));
		}

		#endregion

		#region Slice

		[Pure]
		public static implicit operator JsonValue(Slice value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator Slice(JsonValue? value)
		{
			if (value.IsNullOrMissing()) return default(Slice);

			if (value.Type == JsonType.String)
			{
				return Slice.FromBase64(((JsonString)value).Value);
			}

			throw Errors.JsonConversionNotSupported(value, typeof(Slice));
		}

		#endregion

		#region ArraySegment<byte>

		[Pure]
		public static implicit operator JsonValue(ArraySegment<byte> value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator ArraySegment<byte>(JsonValue? value)
		{
			if (value.IsNullOrMissing()) return default(ArraySegment<byte>);

			if (value.Type == JsonType.String)
			{
				return new ArraySegment<byte>(Convert.FromBase64String(((JsonString)value).Value));
			}

			throw Errors.JsonConversionNotSupported(value, typeof(ArraySegment<byte>));
		}

		#endregion

		#region Boolean

		[Pure]
		public static implicit operator JsonValue(bool value)
		{
			return JsonBoolean.Return(value);
		}

		[Pure]
		public static explicit operator bool(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToBoolean();
		}

		[Pure]
		public static implicit operator JsonValue(bool? value)
		{
			return JsonBoolean.Return(value);
		}

		[Pure]
		public static explicit operator bool?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToBooleanOrDefault();
		}

		#endregion

		#region Char

		[Pure]
		public static implicit operator JsonValue(char value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator char(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToChar();
		}

		[Pure]
		public static implicit operator JsonValue(char? value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator char?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToCharOrDefault();
		}

		#endregion

		#region SByte

		[Pure]
		public static implicit operator JsonValue(sbyte value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator sbyte(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToSByte();
		}

		[Pure]
		public static implicit operator JsonValue(sbyte? value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator sbyte?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToSByteOrDefault();
		}

		#endregion

		#region Byte

		[Pure]
		public static implicit operator JsonValue(byte value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator byte(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToByte();
		}

		[Pure]
		public static implicit operator JsonValue(byte? value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator byte?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToByteOrDefault();
		}

		#endregion

		#region Int16

		[Pure]
		public static implicit operator JsonValue(short value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator short(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToInt16();
		}

		[Pure]
		public static implicit operator JsonValue(short? value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator short?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToInt16OrDefault();
		}

		#endregion

		#region UInt16

		[Pure]
		public static implicit operator JsonValue(ushort value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator ushort(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUInt16();
		}

		[Pure]
		public static implicit operator JsonValue(ushort? value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator ushort?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUInt16OrDefault();
		}

		#endregion

		#region Int32

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(int value) => JsonNumber.Return(value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator int(JsonValue? value) => (value ?? JsonNull.Null).ToInt32();

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(int? value) => JsonNumber.Return(value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator int?(JsonValue? value) => (value ?? JsonNull.Null).ToInt32OrDefault();

		#endregion

		#region UInt32

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(uint value) => JsonNumber.Return(value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator uint(JsonValue? value) => (value ?? JsonNull.Null).ToUInt32();

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(uint? value) => JsonNumber.Return(value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator uint?(JsonValue? value) => (value ?? JsonNull.Null).ToUInt32OrDefault();

		#endregion

		#region Int64

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(long value) => JsonNumber.Return(value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator long(JsonValue? value) => (value ?? JsonNull.Null).ToInt64();

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(long? value) => JsonNumber.Return(value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator long?(JsonValue? value) => (value ?? JsonNull.Null).ToInt64OrDefault();

		#endregion

		#region UInt64

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(ulong value) => JsonNumber.Return(value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator ulong(JsonValue? value) => (value ?? JsonNull.Null).ToUInt64();

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(ulong? value) => JsonNumber.Return(value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator ulong?(JsonValue? value) => (value ?? JsonNull.Null).ToUInt64OrDefault();

		#endregion

		#region Single

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(float value) => JsonNumber.Return(value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator float(JsonValue? value) => (value ?? JsonNull.Null).ToSingle();

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(float? value) => JsonNumber.Return(value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator float?(JsonValue? value) => (value ?? JsonNull.Null).ToSingleOrDefault();

		#endregion

		#region Double

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(double value) => JsonNumber.Return(value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator double(JsonValue? value) => (value ?? JsonNull.Null).ToDouble();

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(double? value) => JsonNumber.Return(value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator double?(JsonValue? value) => (value ?? JsonNull.Null).ToDoubleOrDefault();

		#endregion

		#region Half

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(Half value) => JsonNumber.Return(value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Half(JsonValue? value) => (value ?? JsonNull.Null).ToHalf();

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(Half? value) => JsonNumber.Return(value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Half?(JsonValue? value) => (value ?? JsonNull.Null).ToHalfOrDefault();

		#endregion

		#region Decimal

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(decimal value) => JsonNumber.Return(value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator decimal(JsonValue? value) => (value ?? JsonNull.Null).ToDecimal();

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(decimal? value) => JsonNumber.Return(value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator decimal?(JsonValue? value) => (value ?? JsonNull.Null).ToDecimalOrDefault();

		#endregion

		#region Int128

#if NET8_0_OR_GREATER

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(Int128 value) => JsonNumber.Return(value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Int128(JsonValue? value) => (value ?? JsonNull.Null).ToInt128();

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(Int128? value) => JsonNumber.Return(value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Int128?(JsonValue? value) => (value ?? JsonNull.Null).ToInt128OrDefault();

#endif

#endregion

		#region Guid...

		[Pure]
		public static implicit operator JsonValue(Guid value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator Guid(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToGuid();
		}

		[Pure]
		public static implicit operator JsonValue(Guid? value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator Guid?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToGuidOrDefault();
		}

		[Pure]
		public static implicit operator JsonValue(Uuid128 value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator Uuid128(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUuid128();
		}

		[Pure]
		public static implicit operator JsonValue(Uuid128? value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator Uuid128? (JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUuid128OrDefault();
		}

		[Pure]
		public static implicit operator JsonValue(Uuid96 value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator Uuid96(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUuid96();
		}

		[Pure]
		public static implicit operator JsonValue(Uuid96? value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator Uuid96? (JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUuid96OrDefault();
		}

		[Pure]
		public static implicit operator JsonValue(Uuid80 value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator Uuid80(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUuid80();
		}

		[Pure]
		public static implicit operator JsonValue(Uuid80? value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator Uuid80? (JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUuid80OrDefault();
		}

		[Pure]
		public static implicit operator JsonValue(Uuid64 value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator Uuid64(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUuid64();
		}

		[Pure]
		public static implicit operator JsonValue(Uuid64? value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator Uuid64? (JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUuid64OrDefault();
		}

		#endregion

		#region Uri...

		[Pure]
		public static implicit operator JsonValue(Uri? value) => JsonString.Return(value);

		[Pure]
		public static explicit operator Uri?(JsonValue? value) => value.IsNullOrMissing() ? null : new Uri(value.ToString());

		#endregion

		#region IPAddress...

		[Pure]
		public static implicit operator JsonValue(IPAddress? value) => JsonString.Return(value);

		[Pure]
		public static explicit operator IPAddress?(JsonValue? value)
		{
			if (value.IsNullOrMissing()) return null;
			var str = value.ToString();
			return str.Length == 0 ? IPAddress.Any : IPAddress.Parse(str);
		}

		#endregion

		#region TimeSpan

		[Pure]
		public static implicit operator JsonValue(TimeSpan value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator TimeSpan(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToTimeSpan();
		}

		[Pure]
		public static implicit operator JsonValue(TimeSpan? value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator TimeSpan?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToTimeSpanOrDefault();
		}

		#endregion

		#region DateTime

		[Pure]
		public static implicit operator JsonValue(DateTime value)
		{
			return JsonDateTime.Return(value);
		}

		[Pure]
		public static explicit operator DateTime(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToDateTime();
		}

		[Pure]
		public static implicit operator JsonValue(DateTime? value)
		{
			return JsonDateTime.Return(value);
		}

		[Pure]
		public static explicit operator DateTime?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToDateTimeOrDefault();
		}

		#endregion

		#region DateTimeOffset

		[Pure]
		public static implicit operator JsonValue(DateTimeOffset value)
		{
			return JsonDateTime.Return(value);
		}

		[Pure]
		public static explicit operator DateTimeOffset(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToDateTimeOffset();
		}

		[Pure]
		public static implicit operator JsonValue(DateTimeOffset? value)
		{
			return JsonDateTime.Return(value);
		}

		[Pure]
		public static explicit operator DateTimeOffset?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToDateTimeOffsetOrDefault();
		}

		#endregion

		#region DateOnly

		[Pure]
		public static implicit operator JsonValue(DateOnly value)
		{
			return JsonDateTime.Return(value);
		}

		[Pure]
		public static explicit operator DateOnly(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToDateOnly();
		}

		[Pure]
		public static implicit operator JsonValue(DateOnly? value)
		{
			return JsonDateTime.Return(value);
		}

		[Pure]
		public static explicit operator DateOnly?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToDateOnlyOrDefault();
		}

		#endregion

		#region TimeOnly

		[Pure]
		public static implicit operator JsonValue(TimeOnly value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator TimeOnly(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToTimeOnly();
		}

		[Pure]
		public static implicit operator JsonValue(TimeOnly? value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator TimeOnly?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToTimeOnlyOrDefault();
		}

		#endregion

		#region NodaTime.Duration

		[Pure]
		public static implicit operator JsonValue(NodaTime.Duration value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator NodaTime.Duration(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToDuration();
		}

		[Pure]
		public static implicit operator JsonValue(NodaTime.Duration? value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator NodaTime.Duration?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToDurationOrDefault();
		}

		#endregion

		#region NodaTime.Instant

		[Pure]
		public static implicit operator JsonValue(NodaTime.Instant value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator NodaTime.Instant(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToInstant();
		}

		[Pure]
		public static implicit operator JsonValue(NodaTime.Instant? value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator NodaTime.Instant?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToInstantOrDefault();
		}

		#endregion

		#region NodaTime.OffsetDateTime

		[Pure]
		public static implicit operator JsonValue(NodaTime.OffsetDateTime value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator NodaTime.OffsetDateTime(JsonValue? value)
		{
			return (value ?? JsonNull.Null).As<NodaTime.OffsetDateTime>();
		}

		[Pure]
		public static implicit operator JsonValue(NodaTime.OffsetDateTime? value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator NodaTime.OffsetDateTime?(JsonValue? value)
		{
			return value.As<NodaTime.OffsetDateTime?>();
		}

		#endregion

		#region NodaTime.ZonedDateTime

		[Pure]
		public static implicit operator JsonValue(NodaTime.ZonedDateTime value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator NodaTime.ZonedDateTime(JsonValue? value)
		{
			return value.As<NodaTime.ZonedDateTime>();
		}

		[Pure]
		public static implicit operator JsonValue(NodaTime.ZonedDateTime? value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator NodaTime.ZonedDateTime?(JsonValue? value)
		{
			return value.As<NodaTime.ZonedDateTime>();
		}

		#endregion

		#region NodaTime.LocalDateTime

		[Pure]
		public static implicit operator JsonValue(NodaTime.LocalDateTime value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator NodaTime.LocalDateTime(JsonValue? value)
		{
			return value.As<NodaTime.LocalDateTime>();
		}

		[Pure]
		public static implicit operator JsonValue(NodaTime.LocalDateTime? value)
		{
			return JsonString.Return(value);
		}

		public static explicit operator NodaTime.LocalDateTime?(JsonValue? value)
		{
			return value.As<NodaTime.LocalDateTime?>();
		}

		#endregion

		#region NodaTime.LocalDate

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(NodaTime.LocalDate value)
		{
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator NodaTime.LocalDate(JsonValue? value)
		{
			return value.As<NodaTime.LocalDate>();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(NodaTime.LocalDate? value)
		{
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator NodaTime.LocalDate?(JsonValue? value)
		{
			return value.As<NodaTime.LocalDate?>();
		}

		#endregion

		#region Common Array Types

		#region string[]...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(ReadOnlySpan<string?> values)
		{
			return JsonArray.FromValues(values);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(string?[]? values)
		{
			return JsonArray.FromValues(values) ?? JsonNull.Null;
		}
		
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(List<string>? values)
		{
			return JsonArray.FromValues(values) ?? JsonNull.Null;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(ImmutableArray<string> values)
		{
			return JsonArray.FromValues(values.AsSpan());
		}

		#endregion

		#region int[]...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(ReadOnlySpan<int> values)
		{
			return JsonArray.FromValues(values);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(int[]? values)
		{
			return JsonArray.FromValues(values) ?? JsonNull.Null;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(List<int>? values)
		{
			return JsonArray.FromValues(values) ?? JsonNull.Null;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(ImmutableArray<int> values)
		{
			return JsonArray.FromValues(values.AsSpan());
		}

		#endregion

		#region long[]...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(ReadOnlySpan<long> values)
		{
			return JsonArray.FromValues(values);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(long[]? values)
		{
			return JsonArray.FromValues(values) ?? JsonNull.Null;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(List<long>? values)
		{
			return JsonArray.FromValues(values) ?? JsonNull.Null;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(ImmutableArray<long> values)
		{
			return JsonArray.FromValues(values.AsSpan());
		}

		#endregion

		#region long[]...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(ReadOnlySpan<float> values)
		{
			return JsonArray.FromValues(values);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(float[]? values)
		{
			return JsonArray.FromValues(values) ?? JsonNull.Null;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(List<float>? values)
		{
			return JsonArray.FromValues(values) ?? JsonNull.Null;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(ImmutableArray<float> values)
		{
			return JsonArray.FromValues(values.AsSpan());
		}

		#endregion

		#region long[]...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(ReadOnlySpan<double> values)
		{
			return JsonArray.FromValues(values);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(double[]? values)
		{
			return JsonArray.FromValues(values) ?? JsonNull.Null;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(List<double>? values)
		{
			return JsonArray.FromValues(values) ?? JsonNull.Null;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(ImmutableArray<double> values)
		{
			return JsonArray.FromValues(values.AsSpan());
		}

		#endregion

		#region long[]...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(ReadOnlySpan<Guid> values)
		{
			return JsonArray.FromValues(values);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(Guid[]? values)
		{
			return JsonArray.FromValues(values) ?? JsonNull.Null;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(List<Guid>? values)
		{
			return JsonArray.FromValues(values) ?? JsonNull.Null;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(ImmutableArray<Guid> values)
		{
			return JsonArray.FromValues(values.AsSpan());
		}

		#endregion

		#region long[]...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(ReadOnlySpan<Uuid128> values)
		{
			return JsonArray.FromValues(values);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(Uuid128[]? values)
		{
			return JsonArray.FromValues(values) ?? JsonNull.Null;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(List<Uuid128>? values)
		{
			return JsonArray.FromValues(values) ?? JsonNull.Null;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(ImmutableArray<Uuid128> values)
		{
			return JsonArray.FromValues(values.AsSpan());
		}

		#endregion

		#region long[]...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(ReadOnlySpan<NodaTime.Instant> values)
		{
			return JsonArray.FromValues(values);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(NodaTime.Instant[]? values)
		{
			return JsonArray.FromValues(values) ?? JsonNull.Null;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(List<NodaTime.Instant>? values)
		{
			return JsonArray.FromValues(values) ?? JsonNull.Null;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(ImmutableArray<NodaTime.Instant> values)
		{
			return JsonArray.FromValues(values.AsSpan());
		}

		#endregion

		#endregion

	}

}
