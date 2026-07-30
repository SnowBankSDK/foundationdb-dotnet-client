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
	using SnowBank.Buffers;
	using SnowBank.Text;

	/// <summary>JSON DateTime</summary>
	[DebuggerDisplay("JSON DateTime({m_value}, {m_value}+{m_offset})")]
	[PublicAPI]
#if !NETSTANDARD2_0
	// System.Text.Json interop is disabled on the netstandard2.0 build
	[System.Text.Json.Serialization.JsonConverter(typeof(CrystalJsonCustomJsonConverter))]
#endif
	[DebuggerNonUserCode]
	public sealed class JsonDateTime : JsonValue, IEquatable<JsonDateTime>, IEquatable<DateTime>, IEquatable<DateTimeOffset>, IEquatable<NodaTime.LocalDateTime>, IEquatable<NodaTime.LocalDate>
	{
		private const long UNIX_EPOCH_TICKS = 621355968000000000L;
		private const short NO_TIMEZONE = short.MinValue;

		#region Static Helpers...

		/// <summary>Singleton equivalent to <see cref="System.DateTime.MinValue"/></summary>
		public static readonly JsonDateTime MinValue = new(DateTime.MinValue, NO_TIMEZONE);
		
		/// <summary>Singleton equivalent to <see cref="System.DateTime.MinValue"/></summary>
		public static readonly JsonDateTime MaxValue = new(DateTime.MaxValue, NO_TIMEZONE);
		
		internal static readonly DateTime MaxValueDate = new(3155378112000000000);
		
		/// <summary>Singleton equivalent to <see cref="DateOnly.MinValue"/></summary>
		public static readonly JsonDateTime DateOnlyMaxValue = new(MaxValueDate, NO_TIMEZONE);

		/// <summary>Default precision for JSON dates (due to truncation during serialization)</summary>
		/// <remarks>Dates that differ by less than this value will be considered equal, and produce the same JSON text literal</remarks>
		public static readonly long PrecisionTicks = TimeSpan.TicksPerMillisecond;

		/// <summary>Tests if two dates are considered equal within the minimum supported precision</summary>
		public static bool AreEqual(DateTime left, DateTime right)
		{
			return Math.Abs(left.Ticks - right.Ticks) <= PrecisionTicks;
		}

		/// <summary>Tests if two dates are considered equal within the minimum supported precision</summary>
		public static bool AreEqual(DateTimeOffset left, DateTimeOffset right)
		{
			return Math.Abs(left.Ticks - right.Ticks) <= PrecisionTicks;
		}

		#endregion

		#region Private Members...

		// Problem: DateTimeOffset "lose" the information about the original DateTimeKind
		// => when the server is in a timezone with offset 0, like Greenwich (~= UTC),
		//    then we will not be able to tell if the original DateTime was Kind.Utc or Kind.Local

		/// <summary>Copy of the "m_dateTime" field of a DateTimeOffset, or the original DateTime value</summary>
		private readonly DateTime m_value;

		/// <summary>Copy of the "m_offsetMinutes" of a DateTimeOffset, or int.MinValue for DateTime</summary>
		private readonly short m_offset;

		// DateTimeOffset is stored as a UTC DateTime and the time zone offset
		// DateTime from local sources (UTC or Local) will have m_offset == NO_TIMEZONE
		// DateTime from remote sources (parsed from strings) will have m_offset = NO_TIMEZONE if there is no timezone details in the source
		// => if we have to bind a remote date with a different TZ into a DateTime instance, it will be converted to a LocalTime relative to the current server

		#endregion

		#region Constructors...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal JsonDateTime(DateTime value, short offset)
		{
			m_value = value;
			m_offset = offset;
		}

		/// <summary>Wraps a <see cref="System.DateTime"/> instance</summary>
		public JsonDateTime(DateTime value)
		{
			m_value = value;
			m_offset = NO_TIMEZONE;
		}

		/// <summary>Wraps a <see cref="DateTimeOffset"/> instance</summary>
		public JsonDateTime(DateTimeOffset value)
		{
			m_value = value.DateTime;
			m_offset = (short) value.Offset.TotalMinutes;
		}

		/// <summary>Wraps a <see cref="DateOnly"/> instance</summary>
		public JsonDateTime(DateOnly value)
		{
			m_value = value.ToDateTime(default, DateTimeKind.Unspecified);
			m_offset = NO_TIMEZONE;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonDateTime(long ticks, DateTimeKind kind) : this(new DateTime(ticks, kind)) { }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonDateTime(int year, int month, int day) : this(new DateTime(year, month, day)) { }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonDateTime(int year, int month, int day, int hour, int minute, int second, DateTimeKind kind) : this(new DateTime(year, month, day, hour, minute, second, kind)) { }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonDateTime(int year, int month, int day, int hour, int minute, int second, int millisecond, DateTimeKind kind) : this(new DateTime(year, month, day, hour, minute, second, millisecond, kind)) { }

		/// <summary>Returns the equivalent <see cref="JsonDateTime"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonDateTime Return(DateTime value)
		{
			if (value == DateTime.MinValue) return JsonDateTime.MinValue;
			if (value == DateTime.MaxValue) return JsonDateTime.MaxValue;
			return new JsonDateTime(value);
		}

		/// <summary>Returns the equivalent <see cref="JsonDateTime"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(DateTime? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		/// <summary>Returns the equivalent <see cref="JsonDateTime"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonDateTime Return(DateTimeOffset value)
		{
			if (value == DateTimeOffset.MinValue) return JsonDateTime.MinValue;
			if (value == DateTimeOffset.MaxValue) return JsonDateTime.MaxValue;
			return new JsonDateTime(value);
		}

		/// <summary>Returns the equivalent <see cref="JsonDateTime"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(DateTimeOffset? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		/// <summary>Returns the equivalent <see cref="JsonDateTime"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonDateTime Return(DateOnly value)
		{
			if (value == DateOnly.MinValue) return JsonDateTime.MinValue;
			if (value == DateOnly.MaxValue) return JsonDateTime.DateOnlyMaxValue;
			return new JsonDateTime(value);
		}

		/// <summary>Returns the equivalent <see cref="JsonDateTime"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(DateOnly? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.Instant value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.Instant? value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.ZonedDateTime value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.ZonedDateTime? value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.LocalDateTime value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.LocalDateTime? value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.LocalDate value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.LocalDate? value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.LocalTime value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.LocalTime? value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.OffsetDateTime value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.OffsetDateTime? value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		#endregion

		#region Public Members...

		public long Ticks
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_value.Ticks;
		}

		public long UtcTicks
		{
			get
			{
				if (m_offset == NO_TIMEZONE && m_value.Kind != DateTimeKind.Utc)
				{ // local DateTime => converted to UTC
					return m_value.ToUniversalTime().Ticks;
				}
				//DTO: already UTC
				return m_value.Ticks;
			}
		}

		public DateTime Date
		{
			get
			{
				if (m_offset != NO_TIMEZONE)
				{ // DateTimeOffset, or local DateTime => convert to UTC
					return DateTime.SpecifyKind(m_value, DateTimeKind.Utc);
				}
				else
				{ // return as-is
					return m_value;
				}
			}
		}

		public DateTimeOffset DateWithOffset
		{
			get
			{
				// the extremes are sentinels ("no value" / "the end of time"): applying an offset to them would
				// throw east of Greenwich for MinValue, west of Greenwich for MaxValue (the UTC equivalent falls
				// outside year 1..9999), so they map to the canonical extremes instead
				if (m_value == DateTime.MinValue)
				{
					return DateTimeOffset.MinValue;
				}
				if (m_value == DateTime.MaxValue)
				{
					return DateTimeOffset.MaxValue;
				}
				if (m_offset == NO_TIMEZONE)
				{ // convert to DTO using local timezone
					return new DateTimeOffset(m_value);
				}
				return new DateTimeOffset(m_value, TimeSpan.FromMinutes(m_offset));
			}
		}

		public DateOnly DateWithoutTime
		{
			get
			{
				if (m_offset != NO_TIMEZONE)
				{ // DateTimeOffset, or local DateTime local => convert to UTC
					return DateOnly.FromDateTime(DateTime.SpecifyKind(m_value, DateTimeKind.Utc));
				}
				else
				{ // return as-is
					return DateOnly.FromDateTime(m_value);
				}
			}
		}

		public DateTime LocalDateTime => this.DateWithOffset.LocalDateTime;

		public DateTime UtcDateTime => this.DateWithOffset.UtcDateTime;

		public TimeSpan UtcOffset => m_offset != NO_TIMEZONE ? TimeSpan.FromMinutes(m_offset) : m_value.Kind == DateTimeKind.Utc ? TimeSpan.Zero : TimeZoneInfo.Local.GetUtcOffset(m_value);

		public bool HasOffset => m_offset != NO_TIMEZONE;

		/// <summary>Number of milliseconds since Unix Epoch  (1970-01-01 00:00:00.000 UTC)</summary>
		public long UnixTime => (this.UtcDateTime.Ticks - UNIX_EPOCH_TICKS) / TimeSpan.TicksPerMillisecond;
		//note: this is a long to hopefully not be vulnerable to the Y2038 bug (Unix Time Epoch Bug)

		/// <summary>Number of days since Unix Epoch (1970-01-01 00:00:00.000 UTC)</summary>
		public double UnixTimeDays => (this.UtcTicks - UNIX_EPOCH_TICKS) / (double) TimeSpan.TicksPerDay;
		//note: this should hopefully be safe from the Y2038 bug

		public bool IsLocalTime => m_offset == NO_TIMEZONE ? m_value.Kind == DateTimeKind.Local : m_offset != 0 /*TODO: compare with the local TZ ? */;

		public bool IsUtc => m_offset == NO_TIMEZONE ? m_value.Kind == DateTimeKind.Utc : m_offset == 0;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public DateTime ToUniversalTime() => this.UtcDateTime;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public DateTime ToLocalTime() => this.LocalDateTime;

		#endregion

		#region JsonValue Members...

		/// <inheritdoc />
		public override JsonType Type => JsonType.DateTime;

		/// <inheritdoc />
		public override bool IsDefault => m_value == DateTime.MinValue;

		/// <inheritdoc />
		public override bool IsReadOnly => true; //note: dates are immutable

		/// <inheritdoc />
		public override object ToObject() => m_value;

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public override TValue? Bind<TValue>(TValue? defaultValue = default, ICrystalJsonTypeResolver? resolver = null)
			where TValue : default
		{
			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
			if (default(TValue) is not null)
			{
				if (typeof(TValue) == typeof(DateTime)) return (TValue) (object) ToDateTime((DateTime) (object) defaultValue!);
				if (typeof(TValue) == typeof(DateTimeOffset)) return (TValue) (object) ToDateTimeOffset((DateTimeOffset) (object) defaultValue!);
				if (typeof(TValue) == typeof(NodaTime.Instant)) return (TValue) (object) ToInstant((NodaTime.Instant) (object) defaultValue!);
				if (typeof(TValue) == typeof(DateOnly)) return (TValue) (object) ToDateOnly((DateOnly) (object) defaultValue!);
			}
			else
			{
				if (typeof(TValue) == typeof(DateTime?)) return (TValue?) (object?) ToDateTimeOrDefault((DateTime?) (object?) defaultValue);
				if (typeof(TValue) == typeof(DateTimeOffset?)) return (TValue?) (object?) ToDateTimeOffsetOrDefault((DateTimeOffset?) (object?) defaultValue);
				if (typeof(TValue) == typeof(NodaTime.Instant?)) return (TValue?) (object?) ToInstantOrDefault((NodaTime.Instant?) (object?) defaultValue);
				if (typeof(TValue) == typeof(DateOnly?)) return (TValue?) (object?) ToDateOnlyOrDefault((DateOnly?) (object?) defaultValue);

				if (typeof(TValue) == typeof(string)) return (TValue?) (object?) ToStringOrDefault((string?) (object?) defaultValue);
			}

			#endregion

			return (TValue?) BindNative(this, this.Ticks, typeof(TValue), resolver) ?? defaultValue;
		}

		/// <inheritdoc />
		public override object? Bind(Type? type, ICrystalJsonTypeResolver? resolver = null)
		{
			if (type == typeof(DateTimeOffset)) return this.DateWithOffset;
			if (type == typeof(DateTime)) return this.Date;
			if (type == typeof(NodaTime.Instant)) return NodaTime.Instant.FromDateTimeOffset(this.DateWithOffset);
			if (type == typeof(DateOnly)) return this.DateWithoutTime;
			if (type == typeof(string)) return ToStringOrDefault();
			if (type == typeof(DateTimeOffset?)) return this.DateWithOffset;
			if (type == typeof(DateTime?)) return this.Date;
			if (type == typeof(NodaTime.Instant?)) return NodaTime.Instant.FromDateTimeOffset(this.DateWithOffset);
			if (type == typeof(DateOnly?)) return this.DateWithoutTime;

			return BindNative(this, this.Ticks, type, resolver);
		}

		/// <inheritdoc />
		internal override bool IsSmallValue() => true;

		/// <inheritdoc />
		internal override bool IsInlinable() => true;

		#endregion

		#region IJsonSerializable

		/// <inheritdoc />
		public override string ToJsonText(CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_offset == NO_TIMEZONE)
			{ // DateTime
				if (m_value == DateTime.MinValue) return "\"\"";
				return "\"" + CrystalJsonFormatter.ToIso8601String(m_value) + "\"";
			}
			else
			{ // DateTimeOffset
				if (m_value == DateTime.MinValue) return "''";
				return "'" + CrystalJsonFormatter.ToIso8601String(this.DateWithOffset) + "'";
			}
		}

		/// <inheritdoc />
		public override void JsonSerialize(CrystalJsonWriter writer)
		{
			if (m_offset == NO_TIMEZONE)
			{ // DateTime
				writer.WriteValue(m_value);
			}
			else
			{ // DateTimeOffset
				writer.WriteValue(this.DateWithOffset);
			}
		}

		/// <inheritdoc />
		public override bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
		{
			return format switch
			{
				"" or "D" or "d"                       => this.TryFormatIso8601(destination, out charsWritten, quoted: false, omitTimeIfZero: m_value.Kind == DateTimeKind.Unspecified),
				"P" or "p" or "C" or "c" or "N" or "n" => this.TryFormatIso8601(destination, out charsWritten, quoted: true, omitTimeIfZero: m_value.Kind == DateTimeKind.Unspecified),
				"J" or "j"                             => this.TryFormatJavaScript(destination, out charsWritten),
				"Q" or "q"                             => this.GetCompactRepresentation(0).TryCopyTo(destination, out charsWritten),
				_                                      => throw ErrorNotSupportedFormatLiteral(format)
			};
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private bool TryFormatJavaScript(Span<char> destination, out int charsWritten)
		{
			if (destination.Length < 11)
			{ // "new Date(0)"
				goto too_small;
			}

			Span<char> buffer = stackalloc char[32]; // 10 + 20, rounded up
			JsonTokens.DateBeginJavaScript.CopyTo(buffer);
			int pos = JsonTokens.DateBeginJavaScript.Length;

			long ticks = m_offset == NO_TIMEZONE
				? CrystalJson.DateToJavaScriptTicks(m_value)
				: CrystalJson.DateToJavaScriptTicks(this.DateWithOffset);
			if (!ticks.TryFormat(buffer[pos..], out int len, null))
			{
				goto too_small;
			}
			pos += len;
			buffer[pos++] = ')';
			if (pos > destination.Length) goto too_small;

			buffer[..pos].CopyTo(destination);
			charsWritten = pos;
			return true;

		too_small:
			charsWritten = 0;
			return false;
		}

		internal bool TryFormatIso8601(Span<char> destination, out int charsWritten, bool quoted, bool omitTimeIfZero)
		{
			if (m_offset == NO_TIMEZONE)
			{ // DateTime

				if (m_value == DateTime.MinValue)
				{
					if (!quoted)
					{
						charsWritten = 0;
						return true;
					}
					if (destination.Length < 2)
					{
						goto too_small;
					}
					destination[0] = '"';
					destination[1] = '"';
					charsWritten = 2;
					return true;
				}
				
				if (m_value == DateTime.MaxValue)
				{ // DateTime.MaxValue

					ReadOnlySpan<char> literal = JsonTokens.Iso8601DateTimeMaxValue;
					if (!quoted) literal = literal[1..^1];

					if (destination.Length < literal.Length)
					{
						goto too_small;
					}
					literal.CopyTo(destination);
					charsWritten = literal.Length;
					return true;
				}

				if (m_value == JsonDateTime.MaxValueDate)
				{ // DateOnly.MaxValue
					ReadOnlySpan<char> literal = JsonTokens.Iso8601DateOnlyMaxValue;
					if (!quoted) literal = literal[1..^1];
					if (destination.Length < literal.Length)
					{
						goto too_small;
					}
					literal.CopyTo(destination);
					charsWritten = literal.Length;
					return true;
				}

				if (!quoted)
				{
					if (!CrystalJsonFormatter.TryFormatIso8601DateTime(destination, out charsWritten, m_value, m_value.Kind, null, omitTimeIfZero: omitTimeIfZero))
					{
						goto too_small;
					}

					return true;
				}

				if (destination.Length < 2 || !CrystalJsonFormatter.TryFormatIso8601DateTime(destination[1..], out int n, m_value, m_value.Kind, null, omitTimeIfZero: omitTimeIfZero))
				{
					goto too_small;
				}

				destination[0] = '"';
				destination[n + 1] = '"';
				charsWritten = n + 2;
				return true;
			}
			else
			{ // DateTimeOffset
				var dto = new DateTimeOffset(m_value, TimeSpan.FromMinutes(m_offset));

				if (!quoted)
				{
					if (dto == DateTimeOffset.MinValue)
					{
						charsWritten = 0;
					}
					else if (!CrystalJsonFormatter.TryFormatIso8601DateTime(destination, out charsWritten, m_value, m_value.Kind, dto.Offset, omitTimeIfZero: omitTimeIfZero))
					{
						goto too_small;
					}
					return true;
				}

				if (dto == DateTimeOffset.MinValue)
				{
					return JsonString.EmptyString.TryFormat(destination, out charsWritten);
				}

				if (destination.Length < 2)
				{
					goto too_small;
				}

				destination[0] = '"';
				if (!CrystalJsonFormatter.TryFormatIso8601DateTime(destination[1..], out int n, m_value, m_value.Kind, dto.Offset, omitTimeIfZero: omitTimeIfZero))
				//if (!dto.TryFormat(destination[1..], out int n, "O", CultureInfo.InvariantCulture))
				{
					goto too_small;
				}

				destination[n + 1] = '"';
				charsWritten = n + 2;
				return true;
			}

		too_small:
			charsWritten = 0;
			return false;
		}

#if NET8_0_OR_GREATER

		/// <inheritdoc />
		public override bool TryFormat(Span<byte> destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
		{
			// we first convert to chars, then to utf-8

			Span<char> chars = stackalloc char[40]; // '\"xxxx-xx-xxTxx:xx:xx.xxxxxxx+xx:xx\"' = max 35
			if (!TryFormat(chars, out int charsWritten, format, provider) 
			 || !JsonEncoding.Utf8NoBom.TryGetBytes(chars[..charsWritten], destination, out bytesWritten))
			{
				bytesWritten = 0;
				return false;
			}
			return true;
		}

#endif


		#endregion

		#region IEquatable<...>

		/// <inheritdoc />
		public override bool Equals(object? obj) => obj switch
		{
			DateTime dt => AreEqual(this.Date, dt),
			DateTimeOffset dto => AreEqual(this.DateWithOffset, dto),
			DateOnly d => AreEqual(this.Date, d.ToDateTime(TimeOnly.MinValue)),
			string s => this.HasOffset
				? JsonString.TryConvertToDateTimeOffset(s, out var dto) && this.DateWithOffset == dto
				: JsonString.TryConvertToDateTime(s, out var dt) && this.Date == dt,
			NodaTime.Instant i => ToInstant().Equals(i),
			_ => base.Equals(obj),
		};

		/// <inheritdoc />
		public override bool Equals(JsonValue? obj) => obj switch
		{
			null => false,
			JsonDateTime date => Equals(date),
			JsonNumber num => num.Equals(this),
			JsonString str => this.HasOffset ? str.TryConvertToDateTimeOffset(out var dto) && dto == this.DateWithOffset : str.TryConvertToDateTime(out var dt) && dt == this.Date,
			_ => false
		};

		/// <inheritdoc />
		public override bool StrictEquals(JsonValue? other) => other switch
		{
			JsonDateTime dt => Equals(dt),
			JsonString s => Equals(s),
			_ => false
		};

		public bool StrictEquals(JsonDateTime? other) => other is not null && Equals(other);
	
		public bool StrictEquals(JsonString? other) => other is not null && Equals(other);

		/// <inheritdoc />
		public bool Equals(JsonDateTime? obj)
		{
			return obj is not null && m_value == obj.m_value && m_offset == obj.m_offset;
		}

		/// <inheritdoc />
		public bool Equals(DateTime value)
		{
			return AreEqual(this.Date, value);
		}

		/// <inheritdoc />
		public bool Equals(DateTimeOffset value)
		{
			return AreEqual(this.DateWithOffset, value);
		}

		/// <inheritdoc />
		public bool Equals(NodaTime.LocalDateTime value)
		{
			return ToLocalDateTime() == value;
		}

		/// <inheritdoc />
		public bool Equals(NodaTime.LocalDate value)
		{
			return ToLocalDate() == value;
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return m_value.GetHashCode();
		}

		
		/// <inheritdoc />
		public override int CompareTo(JsonValue? other)
		{
			if (ReferenceEquals(other, this)) return 0;
			if (other is null) return +1;

			return other switch
			{
				JsonDateTime dt => this.DateWithOffset.CompareTo(dt.DateWithOffset),
				JsonString str => str.TryConvertToDateTimeOffset(out var dto) ? this.DateWithOffset.CompareTo(dto) : base.CompareTo(other),
				JsonNumber num => num.ToDateTimeOffset().CompareTo(this.DateWithOffset),
				JsonNull => +1,
				_ => base.CompareTo(other)
			};
		}

		#endregion

		public override string ToString(string? format, IFormatProvider? provider = null)
		{
			return (format ?? "") switch
			{
				"" or "D" or "d"
					=> string.CreateInvariant($"{this}"),
				"N" or "n" or "C" or "c" or "P" or "p"
					=> string.CreateInvariant($"{this:N}"),
				"Q" or "q"
					=> this.GetCompactRepresentation(0),
				"B" or "b"
					=> JsonEncoding.Encode(string.CreateInvariant($"{this:N}")),
				_
					=> throw ErrorNotSupportedFormatLiteral(format),
			};
		}

		public bool TryConvertToString(out string value)
		{
			value = string.CreateInvariant($"{this:N}");
			return true;
		}

		/// <inheritdoc />
		public override bool ToBoolean(bool _ = false)
		{
			return m_value != DateTime.MinValue;
		}

		/// <summary>Returns the number of milliseconds elapsed since Unix Epoch (1970-01-01 00:00:00.000 UTC)</summary>
		/// <remarks>Note: will throw after 2038 instead of returning a negative number. Please use <see cref="UnixTime"/> instead, wich returns a <see langword="long"/>.</remarks>
		[Obsolete("This method is subject to the Y2038 bug, and will throw for dates that are after 2^31 milliseconds since Unix Epoch.")]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
		public override int ToInt32(int _ = 0)
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
		{
			//BUGBUG: will throw in 2038 (cf Y2038 bug) !
			return checked((int)this.UnixTime);
		}

		/// <summary>Returns the number of milliseconds elapsed since Unix Epoch (1970-01-01 00:00:00.000 UTC)</summary>
		/// <remarks>Note: will throw for years after 2100, for reasons similar to the Y2038 bug</remarks>
		public override uint ToUInt32(uint _ = 0)
		{
			//BUGBUG: will not throw in 2038 since it is unsigned, but will still throw in ~2100 for similar reasons!
			return checked((uint) this.UnixTime);
		}

		/// <summary>Returns the number of ticks</summary>
		/// <remarks>Similar to <see cref="System.DateTime.Ticks"/></remarks>
		public override long ToInt64(long _ = 0)
		{
			return this.UtcTicks;
		}

		/// <summary>Returns the number of ticks</summary>
		/// <remarks>Similar to <see cref="System.DateTime.Ticks"/></remarks>
		public override ulong ToUInt64(ulong _ = 0)
		{
			return (ulong) this.UtcTicks;
		}

		/// <summary>Returns the number of days elapsed since January 1st 1970 UTC</summary>
		public override float ToSingle(float _ = 0)
		{
			return (float) this.UnixTimeDays;
		}

		/// <summary>Returns the number of days elapsed since January 1st 1970 UTC</summary>
		public override double ToDouble(double _ = 0)
		{
			return this.UnixTimeDays;
		}

		/// <summary>Returns the number of days elapsed since January 1st 1970 UTC</summary>
		public override decimal ToDecimal(decimal _ = 0)
		{
			return (decimal) this.UnixTimeDays;
		}

		/// <inheritdoc />
		public override DateTime ToDateTime(DateTime _ = default)
		{
			return this.Date;
		}

		/// <inheritdoc />
		public override DateTimeOffset ToDateTimeOffset(DateTimeOffset _ = default)
		{
			return this.DateWithOffset;
		}

		/// <inheritdoc />
		public override DateOnly ToDateOnly(DateOnly _ = default)
		{
			return DateOnly.FromDateTime(this.Date);
		}

		public NodaTime.LocalDateTime ToLocalDateTime()
		{
			return NodaTime.LocalDateTime.FromDateTime(this.Date);
		}

		public NodaTime.LocalDate ToLocalDate()
		{
			return NodaTime.LocalDate.FromDateTime(this.Date);
		}

		/// <summary>Returns the elapsed time since January 1st 1970 UTC</summary>
		public override TimeSpan ToTimeSpan(TimeSpan _ = default)
		{
			return new TimeSpan(this.UtcTicks - UNIX_EPOCH_TICKS);
		}

		/// <summary>Returns the elapsed time since January 1st 1970 UTC</summary>
		public override NodaTime.Duration ToDuration(NodaTime.Duration _ = default)
		{
			return NodaTime.Duration.FromTicks(this.UtcTicks - UNIX_EPOCH_TICKS);
		}

		/// <inheritdoc />
		public override NodaTime.Instant ToInstant(NodaTime.Instant _ = default)
		{
			if (m_offset == NO_TIMEZONE)
			{
				return NodaTime.Instant.FromDateTimeUtc(this.Date.ToUniversalTime());
			}

			return NodaTime.Instant.FromDateTimeOffset(this.DateWithOffset);
		}

		/// <inheritdoc />
		public override void WriteTo(ref SliceWriter writer)
		{
			//TODO: optimize!
			writer.WriteStringUtf8(ToJsonText());
		}
		
	}

}
