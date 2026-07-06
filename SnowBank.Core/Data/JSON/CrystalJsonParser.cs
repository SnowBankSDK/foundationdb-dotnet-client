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

//#define DEBUG_JSON_PARSER
//#define DEBUG_JSON_BINDER

namespace SnowBank.Data.Json
{
	using System.Buffers;
	using System.Globalization;
	using System.Reflection;
	using SnowBank.Buffers;
	using SnowBank.Runtime;
	using SnowBank.Text;

	internal enum JsonLiteralKind
	{
		/// <summary>Name of a field</summary>
		Field,
		/// <summary>Value of a field</summary>
		Value,
		/// <summary>Integer number written in base 10 (ex: "123", "-123"), excluding any scientific notation</summary>
		Integer,
		/// <summary>Decimal number (ex: "1.234"), including numbers written using the scientific notation (ex: "1234E-3")</summary>
		Decimal
	}

	internal enum JsonTokenType
	{
		Invalid = 0,
		Object, // '{'
		Array,  // '['
		String, // '"'
		Number, // '-+0123456789'
		Null,   // 'n'
		True,   // 't'
		False,  // 'f'
		Special, // 'NI' (NaN or Infinity)
	}

	public static class CrystalJsonParser
	{
		internal const char EndOfStream = '\xFFFF'; // == -1

		internal static readonly SearchValues<char> WhiteCharsMap = SearchValues.Create("\t\r\n ");
		//REVIEW: is '\xA0' (&#160;) considered a whitespace in the JSON spec and/or popular parser implementations ?

		/// <summary>List of characters that are allowed to follow after a number</summary>
		internal static readonly SearchValues<char> ValidNumberTrailingCharacters = SearchValues.Create(",}]: \t\r\n");

		internal static readonly JsonTokenType[] TokenMap =
		[
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, JsonTokenType.String, 0, 0, 0, 0, 0, 0, 0, 0, JsonTokenType.Number, 0, JsonTokenType.Number, 0, 0,
			JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, JsonTokenType.Special, 0, 0, 0, 0, JsonTokenType.Special, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, JsonTokenType.Array, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, JsonTokenType.False, 0, 0, 0, 0, 0, 0, 0, JsonTokenType.Null, 0,
			0, 0, 0, 0, JsonTokenType.True, 0, 0, 0, 0, 0, 0, JsonTokenType.Object, 0, 0, 0, 0,
		];

#if RECOMPUTE_TOKEN_MAP

		public static JsonTokenType[] ComputeTokenTypeMap()
		{
			var map = new JsonTokenType[TOKEN_TYPE_LENGTH];
			map['{'] = JsonTokenType.Object; // { ... }
			map['['] = JsonTokenType.Array; // [ ... ]
			map['"'] = JsonTokenType.String; // "..."
			map['n'] = JsonTokenType.Null; // null
			map['t'] = JsonTokenType.True; // true
			map['f'] = JsonTokenType.False; // false
			map['N'] = JsonTokenType.Special; // NaN
			map['I'] = JsonTokenType.Special; // Infinity
			for (int i = 0; i <= 9; i++)
			{
				map['0' + i] = JsonTokenType.Number;
			}
			map['+'] = JsonTokenType.Number; // +###
			map['-'] = JsonTokenType.Number; // -###

			var sb = new StringBuilder();
			sb.AppendLine("[").Append("\t");
			for (int i = 0; i < map.Length; i++)
			{
				if (map[i] == JsonTokenType.Invalid)
				{
					sb.Append('0');
				}
				else
				{
					sb.Append(nameof(JsonTokenType) + ".").Append(map[i].ToString("G"));
				}

				if (i % 16 == 15) sb.AppendLine(",\t"); else sb.Append(", ");
			}
			sb.AppendLine("];");
			Console.WriteLine(sb);

			return map;
		}

#endif

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber? ParseJsonNumber(string? literal)
		{
			return ParseJsonNumber(literal.AsSpan(), literal);
		}

		public static JsonNumber? ParseJsonNumber(ReadOnlySpan<char> literal, string? original = null)
		{
#if DEBUG_JSON_PARSER
			Debug.WriteLine("CrystalJsonParser.ParseJsonNumber('{0}')", (object)literal);
#endif
			if (literal.Length == 0) return null;

			const int MAX_NUMBER_CHARS = 64;
			if (literal.Length > MAX_NUMBER_CHARS) throw new ArgumentException("Buffer is too large for a numeric value");

			bool negative = false;
			bool hasDot = false;
			bool hasExponent = false;
			bool hasExponentSign = false;
			bool incomplete = true;
			bool computed = true;
			bool overflow = false;
			ulong num = 0;
			int p = 0;
			foreach (char c in literal)
			{
				++p;
				if (c is <= '9' and >= '0')
				{ // digit
					incomplete = false;
					ulong digit = (ulong) (c - '0');
					if (num > (ulong.MaxValue - digit) / 10) overflow = true; // no longer fits in a UInt64: fall back to the literal parser below
					num = (num * 10) + digit;
					continue;
				}

				if (c == '.')
				{
					if (hasDot) throw InvalidNumberFormat(literal, "duplicate decimal point");
					incomplete = true;
					hasDot = true;
					computed = false;
					continue;
				}

				if (c is 'e' or 'E')
				{
					if (hasExponent) throw InvalidNumberFormat(literal, "duplicate exponent");
					incomplete = true;
					hasExponent = true;
					computed = false;
					continue;
				}

				if (c is '-' or '+')
				{
					if (p == 1)
					{
						negative = c == '-';
					}
					else
					{
						if (!hasExponent)
						{
							throw InvalidNumberFormat(literal, "unexpected sign at this location");
						}
						if (hasExponentSign)
						{
							throw InvalidNumberFormat(literal, "duplicate sign is exponent");
						}
						hasExponentSign = true;
					}
					incomplete = true;
					continue;
				}

				if (c == 'I')
				{ // +Infinity / -Infinity ?
					if (literal is "+Infinity")
					{
						return JsonNumber.PositiveInfinity;
					}
					if (literal is "-Infinity")
					{
						return JsonNumber.NegativeInfinity;
					}
				}
				if (c == 'N')
				{
					if (literal is "NaN") return JsonNumber.NaN;
				}
				// character is invalid after a number
				throw InvalidNumberFormat(literal, $"unexpected character '{c}' found)");
			}

			if (incomplete)
			{ // all numbers should end up with a digit (with or without exponent)
				throw InvalidNumberFormat(literal, "truncated");
			}

			// if we did not see either a '.' or an 'E', and the number of digits is <= 16, we know that this is a valid integer that has already been computed
			if (computed && !overflow)
			{
				if (literal.Length < 4)
				{
					if (num == 0) return JsonNumber.Zero; // we consider "-0" to be equal to "0"
					if (num == 1) return negative ? JsonNumber.MinusOne : JsonNumber.One;
					if (!negative)
					{
						// use the cache for small positive integers
						if (num <= JsonNumber.CACHED_SIGNED_MAX)
						{
							return JsonNumber.GetCachedSmallNumber((int) num);
						}
					}
					else
					{
						// use the cache for small negative integers
						if (num <= -JsonNumber.CACHED_SIGNED_MIN)
						{
							return JsonNumber.GetCachedSmallNumber(-((int) num));
						}
					}
				}

				return !negative
					? JsonNumber.ParseUnsigned(num, literal, original)
					: JsonNumber.ParseSigned(-((long) num), literal, original); // with 16 digits max, there is no risk of overflow when going negative
			}

			// the number is a decimal number, or has en exponent, we need to parse the string
			var value = ParseNumberFromLiteral(literal, original, negative, hasDot, hasExponent);
			return value ?? throw InvalidNumberFormat(literal, "malformed");
		}

		internal static JsonNumber? ParseNumberFromLiteral(ReadOnlySpan<char> literal, string? original, bool negative, bool hasDot, bool hasExponent)
		{
			var styles = NumberStyles.AllowLeadingSign;
			if (hasExponent) styles |= NumberStyles.AllowExponent;

			if (!hasDot)
			{
				if (!negative)
				{ // unsigned
#if NET5_0_OR_GREATER
					if (ulong.TryParse(literal, styles, NumberFormatInfo.InvariantInfo, out var u64))
#else
					// span-based TryParse is not on netstandard2.0
					if (ulong.TryParse(literal.ToString(), styles, NumberFormatInfo.InvariantInfo, out var u64))
#endif
					{
						return JsonNumber.ParseUnsigned(u64, literal, original);
					}
				}
				else
				{ // signed
#if NET5_0_OR_GREATER
					if (long.TryParse(literal, styles, NumberFormatInfo.InvariantInfo, out var s64))
#else
					// span-based TryParse is not on netstandard2.0
					if (long.TryParse(literal.ToString(), styles, NumberFormatInfo.InvariantInfo, out var s64))
#endif
					{
						return JsonNumber.ParseSigned(s64, literal, original);
					}
				}
			}
			else
			{ // decimal, or a very large integer (1.23E10)
				styles |= NumberStyles.AllowDecimalPoint;
				// maybe it fits in a double...?
#if NET5_0_OR_GREATER
				if (double.TryParse(literal, styles, NumberFormatInfo.InvariantInfo, out var dbl))
#else
				// span-based TryParse is not on netstandard2.0
				if (double.TryParse(literal.ToString(), styles, NumberFormatInfo.InvariantInfo, out var dbl))
#endif
				{
					return JsonNumber.Parse(dbl, literal, original);
				}
			}

			// use decimal has the last resort fallback...
#if NET5_0_OR_GREATER
			if (decimal.TryParse(literal, styles, NumberFormatInfo.InvariantInfo, out var dec))
#else
			// span-based TryParse is not on netstandard2.0
			if (decimal.TryParse(literal.ToString(), styles, NumberFormatInfo.InvariantInfo, out var dec))
#endif
			{
				return JsonNumber.Parse(dec, literal, original);
			}

			// no luck ...
			return null;
		}

		[Pure]
		private static FormatException InvalidNumberFormat(ReadOnlySpan<char> literal, string reason) => new($"Invalid number '{literal}.' ({reason})");

		/// <summary>Tests if the string COULD be a date in the ISO 8601 format</summary>
		/// <param name="value">String literal to parse</param>
		/// <param name="kind">Receives <see cref="DateTimeKind.Utc"/> if ends with <c>'Z'</c>, <see cref="DateTimeKind.Local"/> if could end with a time offset, or <see cref="DateTimeKind.Unspecified"/> if no offset indication was found</param>
		/// <remarks>This is a fast heuristic that <i>may</i> have false positives</remarks>
		[Pure]
		public static bool CouldBeIso8601DateTime(ReadOnlySpan<char> value, out DateTimeKind kind)
		{
			// look for markers like '-', 'T' and ':' at the correct place
			// must end with either 'Z' (UTC) or '+##:##' / '-##:##'
			kind = DateTimeKind.Unspecified;

#if NET7_0_OR_GREATER
			if (value.Length < 10 || !char.IsAsciiDigit(value[0]))
#else
			// char.IsAsciiDigit is not on netstandard2.0
			if (value.Length < 10 || !(value[0] is >= '0' and <= '9'))
#endif
			{
				return false;
			}

			if (value.Length == 10)
			{ // could be a <date> ("YYYY-MM-DD")
				return value[4] == '-' && value[7] == '-';
			}

			if (value.Length < 19)
			{ // too small to be a <data-time> ("YYYY-MM-DDThh:mm:ss")
				return false;
			}

			if (value[10] != 'T' || value[13] != ':' || value[16] != ':')
			{ // not a valid time part
				return false;
			}

			if (value[^1] == 'Z')
			{ // ends with 'Z', could be UTC
				kind = DateTimeKind.Utc;
				return true;
			}

			if (value[^3] == ':' && (value[^6] is '+' or '-'))
			{ // could end with a time offset
				kind = DateTimeKind.Local;
				return true;
			}

#if NET7_0_OR_GREATER
			if (char.IsAsciiDigit(value[^1]))
#else
			// char.IsAsciiDigit is not on netstandard2.0
			if (value[^1] is >= '0' and <= '9')
#endif
			{
				kind = DateTimeKind.Unspecified;
				return true;
			}

			return false;
		}

		[Pure]
		public static bool TryParseIso8601DateTime(ReadOnlySpan<char> value, out DateTime result)
		{
#if DEBUG_JSON_PARSER
			Debug.WriteLine("CrystalJsonConverter.TryParseMicrosoftDateTime(" + value +")");
#endif
			result = DateTime.MinValue;

			if (value.Length == 0 || !CouldBeIso8601DateTime(value, out _))
			{
				return false;
			}

			// cf http://msdn.microsoft.com/en-us/library/bb882584.aspx
#if NET5_0_OR_GREATER
			return DateTime.TryParse(value, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.RoundtripKind, out result);
#else
			// span-based TryParse is not on netstandard2.0
			return DateTime.TryParse(value.ToString(), DateTimeFormatInfo.InvariantInfo, DateTimeStyles.RoundtripKind, out result);
#endif
		}

		[Pure]
		public static bool TryParseIso8601DateTimeOffset(ReadOnlySpan<char> value, out DateTimeOffset result)
		{
#if DEBUG_JSON_PARSER
			Debug.WriteLine("CrystalJsonConverter.TryParseMicrosoftDateTime(" + value +")");
#endif
			result = DateTimeOffset.MinValue;

			if (!TryParseDateTimeOffsetComponents(value, out DateOnly date, out TimeOnly time, out long nanos, out TimeSpan offset, out DateTimeKind kind))
			{
				return false;
			}

			if (nanos != 0)
			{ // add the nanoseconds to the time
				Contract.Debug.Requires((ulong) nanos < 1_000_000_000);
#if NET5_0_OR_GREATER
				time = time.Add(TimeSpan.FromTicks(nanos / TimeSpan.NanosecondsPerTick)); // note: 100 nanos per BCL tick
#else
				// TimeSpan.NanosecondsPerTick is not on netstandard2.0
				time = time.Add(TimeSpan.FromTicks(nanos / 100)); // note: 100 nanos per BCL tick
#endif
			}

			switch (kind)
			{
				case DateTimeKind.Unspecified:
				{ // use the local server offset
					Contract.Debug.Assert(offset == TimeSpan.Zero);
#if NET6_0_OR_GREATER
					var dt = new DateTime(date, time, DateTimeKind.Unspecified);
#else
					// the DateTime(DateOnly, TimeOnly, DateTimeKind) ctor is not on netstandard2.0
					var dt = date.ToDateTime(time, DateTimeKind.Unspecified);
#endif
					result = dt == DateTime.MinValue ? DateTimeOffset.MinValue
						: dt == DateTime.MaxValue ? DateTimeOffset.MaxValue
						: new(dt);
					break;
				}
				case DateTimeKind.Local:
				{ // there is an offset specified
					// the ctor for DTO insists on subtracting the offset to the time, so we have to compensate!
#if NET8_0_OR_GREATER
					result = new(date, time, offset);
#else
					// the DateTimeOffset(DateOnly, TimeOnly, TimeSpan) ctor is not on netstandard2.0
					result = new DateTimeOffset(date.ToDateTime(time), offset);
#endif
					break;
				}
				default:
				{ // the time is UTC
					Contract.Debug.Assert(kind == DateTimeKind.Utc);
#if NET8_0_OR_GREATER
					result = new(date, time, TimeSpan.Zero);
#else
					// the DateTimeOffset(DateOnly, TimeOnly, TimeSpan) ctor is not on netstandard2.0
					result = new DateTimeOffset(date.ToDateTime(time), TimeSpan.Zero);
#endif
					break;
				}
			}

			return true;
		}

		public static bool TryParseDateTimeOffsetComponents(ReadOnlySpan<char> value, out DateOnly date, out TimeOnly time, out long nanos, out TimeSpan offset, out DateTimeKind kind)
		{
#if NET7_0_OR_GREATER
			if (value.Length < 10
			 || !char.IsAsciiDigit(value[0])
			 || !TryParseDateOnlyComponent(value, out date, out var remainder))
#else
			// char.IsAsciiDigit is not on netstandard2.0
			if (value.Length < 10
			 || !(value[0] is >= '0' and <= '9')
			 || !TryParseDateOnlyComponent(value, out date, out var remainder))
#endif
			{
				goto invalid;
			}

			value = remainder;
			if (value.Length == 0)
			{ // date only
				time = default;
				nanos = 0;
				offset = TimeSpan.Zero;
				kind = DateTimeKind.Unspecified;
				return true;
			}

			if (value[0] == 'T')
			{ // there is a time component
				if (!TryParseTimeOnlyComponent(value[1..], out time, out nanos, out remainder))
				{
					goto invalid;
				}
				value = remainder;
			}
			else
			{ // no time component
				time = default;
				nanos = 0;
			}

			if (value.Length == 0)
			{ // there is no time offset specified
				offset = TimeSpan.Zero;
				kind = DateTimeKind.Unspecified;
				return true;
			}

			if (value[0] == 'Z')
			{
				if (value.Length != 1) goto invalid;
				offset = TimeSpan.Zero;
				kind = DateTimeKind.Utc;
				return true;
			}
			if (value[0] is ('+' or '-'))
			{
				if (!TryParseTimeOffsetComponent(value, out offset, out remainder) || remainder.Length != 0)
				{
					goto invalid;
				}
				kind = DateTimeKind.Local;
				return true;
			}

			// this is not valid!

		invalid:
			date = default;
			time = default;
			nanos = 0;
			offset = TimeSpan.Zero;
			kind = default;
			return false;
		}

		private static bool TryParseDateOnlyComponent(ReadOnlySpan<char> value, out DateOnly date, out ReadOnlySpan<char> remainder)
		{
			// YYYY-MM-DD

#if NET5_0_OR_GREATER
			if (value.Length >= 10
			 && value[4] == '-' && value[7] == '-'
			 && int.TryParse(value[..4], NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out var year)
			 && year is (>= 1 and <= 9999)
			 && int.TryParse(value[5..7], NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out var month)
			 && month is (>= 1 and <= 12)
			 && int.TryParse(value[8..10], NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out var day)
			 && day is >= 1 && day <= DateTime.DaysInMonth(year, month)
			)
#else
			// span-based TryParse is not on netstandard2.0
			if (value.Length >= 10
			 && value[4] == '-' && value[7] == '-'
			 && int.TryParse(value[..4].ToString(), NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out var year)
			 && year is (>= 1 and <= 9999)
			 && int.TryParse(value[5..7].ToString(), NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out var month)
			 && month is (>= 1 and <= 12)
			 && int.TryParse(value[8..10].ToString(), NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out var day)
			 && day is >= 1 && day <= DateTime.DaysInMonth(year, month)
			)
#endif
			{
				date = new DateOnly(year, month, day);
				remainder = value[10..];
				return true;
			}

			date = default;
			remainder = default;
			return false;
		}

		private static bool TryParseTimeOnlyComponent(ReadOnlySpan<char> value, out TimeOnly time, out long nanos, out ReadOnlySpan<char> remainder)
		{
			// hh:mm:ss[.ffffff]

#if NET5_0_OR_GREATER
			if (value.Length >= 8
				&& value[2] == ':' && value[5] == ':'
				&& int.TryParse(value[..2], NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out var hour)
				&& hour is (>= 0 and <= 23)
				&& int.TryParse(value[3..5], NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out var minute)
				&& minute is (>= 0 and <= 59)
				&& int.TryParse(value[6..8], NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out var second)
				&& second is >= 0 && second <= 60 /* leap second! */
			)
#else
			// span-based TryParse is not on netstandard2.0
			if (value.Length >= 8
				&& value[2] == ':' && value[5] == ':'
				&& int.TryParse(value[..2].ToString(), NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out var hour)
				&& hour is (>= 0 and <= 23)
				&& int.TryParse(value[3..5].ToString(), NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out var minute)
				&& minute is (>= 0 and <= 59)
				&& int.TryParse(value[6..8].ToString(), NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out var second)
				&& second is >= 0 && second <= 60 /* leap second! */
			)
#endif
			{
				value = value[8..];
				nanos = 0;
				if (value.Length > 0)
				{ // there may be a millisecond part
					if (value[0] == '.')
					{ // ".f" minimum, up to any number of digits?
						value = value[1..];
						// count the number of digits
#if NET5_0_OR_GREATER
						int digits = value.IndexOfAnyExceptInRange('0', '9');
#else
						// IndexOfAnyExceptInRange is not on netstandard2.0
						int digits = -1;
						for (int i = 0; i < value.Length; i++)
						{
							if (value[i] is < '0' or > '9')
							{
								digits = i;
								break;
							}
						}
#endif
						if (digits == -1) digits = value.Length;
#if NET5_0_OR_GREATER
						if (digits is (0 or > 15) || !ulong.TryParse(value[..digits], NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out var fractional))
#else
						// span-based TryParse is not on netstandard2.0
						if (digits is (0 or > 15) || !ulong.TryParse(value[..digits].ToString(), NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out var fractional))
#endif
						{
							goto invalid;
						}
						value = value[digits..];

						if (fractional != 0)
						{
							// adjust the fractional part until we have nanoseconds
							while (digits < 9)
							{
								fractional *= 10;
								++digits;
							}
							while (digits > 9)
							{
								fractional /= 10; //TODO: how should we round? up or down?
								--digits;
							}
						}
						nanos = (long) fractional;
					}
				}

				time = new TimeOnly(hour, minute, second);
				remainder = value;
				return true;
			}
		invalid:
			time = default;
			nanos = 0;
			remainder = default;
			return false;
		}

		private static bool TryParseTimeOffsetComponent(ReadOnlySpan<char> value, out TimeSpan offset, out ReadOnlySpan<char> remainder)
		{
			// +hh:mm or -hh:mm
#if NET5_0_OR_GREATER
			if (value.Length >= 6
			 && value[0] is ('+' or '-') && value[3] == ':'
			 && int.TryParse(value[1..3], NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out var hour)
			 && hour is (>= 0 and <= 12)
			 && int.TryParse(value[4..6], NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out var minute)
			 && minute is (>= 0 and < 60)
			)
#else
			// span-based TryParse is not on netstandard2.0
			if (value.Length >= 6
			 && value[0] is ('+' or '-') && value[3] == ':'
			 && int.TryParse(value[1..3].ToString(), NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out var hour)
			 && hour is (>= 0 and <= 12)
			 && int.TryParse(value[4..6].ToString(), NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out var minute)
			 && minute is (>= 0 and < 60)
			)
#endif
			{
				var minutes = (hour * 60) + minute;
				offset = TimeSpan.FromMinutes(value[0] == '+' ? minutes : -minutes);
				remainder = value[6..];
				return true;
			}

			offset = TimeSpan.Zero;
			remainder = default;
			return false;
		}

		[Pure]
		public static bool CouldBeJsonMicrosoftDateTime(ReadOnlySpan<char> value)
		{
			return value.Length >= 9 && value.StartsWith("/Date(") && value.EndsWith(")/");
		}

		[Pure, ContractAnnotation("value:null => false")]
		public static bool TryParseMicrosoftDateTime(ReadOnlySpan<char> value, out DateTime result, out TimeSpan? tz)
		{
#if DEBUG_JSON_PARSER
			Debug.WriteLine("CrystalJsonConverter.TryParseMicrosoftDateTime(" + value +")");
#endif
			result = DateTime.MinValue;
			tz = null;

			if (value.Length == 0 || !CouldBeJsonMicrosoftDateTime(value)) return false;

			//Note: the string has already been decoded so the JSON literal "\/Date(...)\/" is seen as "/Date(...)/"
			//Supported format: "/Date(ticks)/" ou "/Date(ticks+HHMM)/"

			bool isLocal = false;
			int endOffset = value.Length - 2; // 2 = ")/".Length

			// check for an optional TimeZone
			char c = value[endOffset - 5];
			if (c is '+' or '-')
			{ // seems likely
				isLocal = true;
				endOffset -= 5;
			}
#if NET5_0_OR_GREATER
			if (!long.TryParse(value[6..endOffset], out var ticks)) // 6 = "/Date(".Length
#else
			// span-based TryParse is not on netstandard2.0
			if (!long.TryParse(value[6..endOffset].ToString(), out var ticks)) // 6 = "/Date(".Length
#endif
			{
				return false;
			}

			const int MILLISECONDS_PER_DAY = 86400 * 1000;
			if (ticks < -62135596800000 + MILLISECONDS_PER_DAY)
			{ // MinValue
				result = DateTime.MinValue;
			}
			else if (ticks > 253402300799999 - MILLISECONDS_PER_DAY)
			{ // MaxValue
				result = DateTime.MaxValue;
			}
			else
			{
				DateTime date = CrystalJson.JavaScriptTicksToDate(ticks);
				if (isLocal)
				{
#if NET5_0_OR_GREATER
					if (!int.TryParse(value[(endOffset + 1)..^2], out var offset))
#else
					// span-based TryParse is not on netstandard2.0
					if (!int.TryParse(value[(endOffset + 1)..^2].ToString(), out var offset))
#endif
					{
						return false;
					}

					// Decode the offset from "BCD" to a number of minutes
					int h = offset / 100;
					int m = offset % 100;
					if (h > 12 || m > 59) return false;
					offset = h * 60 + m;
					tz = (c == '-') ? TimeSpan.FromMinutes(-offset) : TimeSpan.FromMinutes(offset);
				}
				result = date;
			}
			return true;
		}

		#region Deserialization...

		/// <summary>Deserializes a custom class or struct</summary>
		[RequiresUnreferencedCode(AotMessages.TypeMightBeRemoved)]
		public static object? DeserializeCustomClassOrStruct(JsonObject data, Type type, ICrystalJsonTypeResolver resolver)
		{
			if (type == typeof(object) || type.IsInterface || type.IsClass)
			{
				//note: even if there is a "$type" property, we don't know in which polymorphic chain it belongs, so we would be unable to map it to a valid derived type
				// => the best we can to here is to create an expando object
				if (type == typeof(object))
				{
					return data.ToExpando();
				}
			}

			// look up the type's definition
			if (!resolver.TryResolveTypeDefinition(type, out var typeDef))
			{
				throw JsonBindingException.CannotDeserializeCustomTypeNoTypeDefinition(data, type);
			}

			if (typeDef.IsPolymorphic)
			{
				var discriminator = data[typeDef.TypeDiscriminatorProperty?.Value ?? "$type"];

				// we have to decide if we are the correct "concrete" type that matches the data, or if we have to defer to another type
				if (typeDef.TypeDiscriminatorValue is null || !typeDef.TypeDiscriminatorValue.Equals(discriminator))
				{ // this is not "us"!

					if (typeDef.DerivedTypeMap is not null)
					{ // we found the correct type
						if (typeDef.DerivedTypeMap.TryGetValue(discriminator, out var derivedType))
						{
							Contract.Debug.Assert(derivedType != type); // infinite loop?
							return DeserializeCustomClassOrStruct(data, derivedType, resolver);
						}
					}
					else if (typeDef.BaseType is not null)
					{ // try our luck with the top type
						if (!discriminator.IsNullOrMissing())
						{ // we can't try our luck by going through the top of the chain!
							return DeserializeCustomClassOrStruct(data, typeDef.BaseType, resolver);
						}
					}

					if (typeDef.CustomBinder == null && typeDef.Generator == null)
					{ // we don't know its $type, and we have no way to generate an instance => error !
						throw JsonBindingException.CannotDeserializeCustomTypeWithUnknownTypeDiscriminator(data, type, discriminator);
					}
				}

				// we are the correct candidate!
			}

			if (typeDef.CustomBinder != null)
			{ // the custom binder will handle deserializing the object
				return typeDef.CustomBinder(data, type, resolver);
			}

			if (typeDef.Generator == null)
			{ // we don't have an instance generator for this type, we won't be able to do anything!
				throw JsonBindingException.CannotDeserializeCustomTypeNoBinderOrGenerator(data, type);
			}

			// create a new (empty) instance of this type
			object instance;
			try
			{
				instance = typeDef.Generator();
			}
			catch (Exception e) when (!e.IsFatalError())
			{ // This could happen if there is no parameterless ctor, or trimming was too aggressive...
				throw JsonBindingException.FailedToConstructTypeInstanceErrorOccurred(data, type, e);
			}

			if (instance == null)
			{ // This could happen if the type was an interface or an abstract class...
				throw JsonBindingException.FailedToConstructTypeInstanceReturnedNull(data, type);
			}

			// populate the instance members one by one
			
			foreach (var member in typeDef.Members)
			{
				// skip readonly members
				if (member.IsReadOnly) continue;

				// do we have a value for this field?
				if (!data.TryGetValue(member.Name, out var child) 
				  || child.IsNull
				)
				{ // not found, or explicit null
					//TODO: should we assign a default value if one is specified via [JsonProperty] ?
					continue;
				}

				// We must have a valid Binder for this type, and the ability to write the value to the instance's member
				if (member.Binder == null)
				{
					throw JsonBindingException.CannotDeserializeCustomTypeNoReaderForMember(child, member, type);
				}
				if (member.Setter == null)
				{
					throw JsonBindingException.CannotDeserializeCustomTypeNoBinderForMember(child, member, type);
				}

				// convert the value into an instance that is assignable to this member's type.
				object? value;
				try
				{
					value = member.Binder(child, member.Type, resolver);
				}
				catch(Exception e)
				{
					if (e is TargetInvocationException invokeEx)
					{ // make the callstack a bit nicer by un-wrapping the inner exception
						e = invokeEx.InnerException ?? e;
					}

					var path = JsonPath.Create(member.OriginalName);
					if (e is JsonBindingException bindingEx)
					{
						// we have to repeat the original reason and the original path!
						var reason = bindingEx.Reason ?? bindingEx.Message;
						if (bindingEx.Path != null)
						{
							path = JsonPath.Combine(path, bindingEx.Path.Value);
						}
						var targetType = bindingEx.TargetType ?? member.Type;
						throw new JsonBindingException($"Cannot bind JSON {child.Type} to member '({typeDef.Type.GetFriendlyName()}).{path}' of type '{member.Type.GetFriendlyName()}': {reason}", reason, path, bindingEx.Value, targetType, bindingEx.InnerException);
					}

					throw new JsonBindingException($"Cannot bind JSON {child.Type} to member '({typeDef.Type.GetFriendlyName()}).{path}' of type '{member.Type.GetFriendlyName()}': [{e.GetType().GetFriendlyName()}] {e.Message}", path, child, member.Type, e);
				}

				// update the instance member with the decoded value
				try
				{
					member.Setter(instance, value);
				}
				catch(Exception e)
				{
					var path = JsonPath.Create(member.Name);
					throw new JsonBindingException($"Cannot assign member '{instance.GetType().GetFriendlyName()}.{member.Name}' of type '{member.Type.GetFriendlyName()}' with value of type '{(value?.GetType().GetFriendlyName() ?? "<null>")}': [{e.GetType().GetFriendlyName()}] {e.Message}", path, child, member.Type, e);
				}
			}

			return instance;
		}

		#endregion

	}

	public static class CrystalJsonParser<TReader>
		where TReader : struct, IJsonReader
	{

		#region Parsing...

		/// <summary>Parses the next JSON in the reader</summary>
		/// <returns>Parsed token, or <see langword="null"/> if we reached the end of the JSON document</returns>
		public static JsonValue? ParseJsonValue(ref CrystalJsonTokenizer<TReader> reader)
			=> ParseJsonValue(ref reader, 0);

		/// <summary>Maximum nesting depth (objects and arrays) allowed while parsing a JSON document.</summary>
		/// <remarks>Protects against a <see cref="StackOverflowException"/> (which cannot be caught and would crash the process) when parsing hostile deeply-nested input.</remarks>
		internal const int MaximumDepth = 64;

		private static JsonValue? ParseJsonValue(ref CrystalJsonTokenizer<TReader> reader, int depth)
		{
#if DEBUG_JSON_PARSER
			System.Diagnostics.Debug.WriteLine("CrystalJsonConverter.ParseJsonValue(...)");
#endif
			char first = reader.ReadNextToken();

			var map = CrystalJsonParser.TokenMap;
			if (first < map.Length)
			{
				switch (map[first])
				{
					case JsonTokenType.Object:
					{
						return ParseJsonObject(ref reader, depth);
					}
					case JsonTokenType.Array:
					{
						return ParseJsonArray(ref reader, depth);
					}
					case JsonTokenType.Null:
					{ // null
						// ReSharper disable once StringLiteralTypo
						reader.ReadExpectedKeyword("ull");
						return JsonNull.Null;
					}
					case JsonTokenType.True:
					{ // true
						// ReSharper disable once StringLiteralTypo
						reader.ReadExpectedKeyword("rue");
						return JsonBoolean.True;
					}
					case JsonTokenType.False:
					{ // false
						// ReSharper disable once StringLiteralTypo
						reader.ReadExpectedKeyword("alse");
						return JsonBoolean.False;
					}
					case JsonTokenType.String:
					{ // string
						return ParseJsonStringOrDateTime(ref reader);
					}
					case JsonTokenType.Number:
					{ // number
						return ParseJsonNumber(ref reader, first);
					}
					case JsonTokenType.Special:
					{ // NaN ou Infinity ?
						return ParseSpecialKeyword(ref reader, first);
					}
				}
			}

			if (first == CrystalJsonParser.EndOfStream)
			{ // an empty document is equivalent to "null"
				return null;
			}

			// map the syntax error into a proper error message
			switch (first)
			{
				case '}':
				{ // attempting to close an object that was not opened
					throw reader.FailInvalidSyntax("Unexpected '}' encountered without corresponding '{'");
				}
				case ']':
				{ // attempting to close an array that was not opened
					throw reader.FailInvalidSyntax("Unexpected ']' encountered without corresponding '['");
				}
				case ',':
				{ // comma outside an object or array
					throw reader.FailInvalidSyntax("Unexpected separator encountered outside of an array or an object");
				}
				default:
				{ // ??
					throw reader.FailInvalidSyntax($"Unexpected character '{first}'");
				}
			}
		}

		private static JsonValue ParseJsonStringOrDateTime(ref CrystalJsonTokenizer<TReader> reader)
		{
			string value = ParseJsonStringInternal(ref reader, reader.GetStringTable(JsonLiteralKind.Value));
#if DEBUG_JSON_PARSER
			System.Diagnostics.Debug.WriteLine("CrystalJsonConverter.ParseJsonStringOrDateTime(" + value + ")");
#endif
			return JsonString.Return(value);
		}

		private static string ParseJsonName(ref CrystalJsonTokenizer<TReader> reader)
		{
#if DEBUG_JSON_PARSER
			System.Diagnostics.Debug.WriteLine("CrystalJsonConverter.ParseJsonName(...)");
#endif
			return ParseJsonStringInternal(ref reader, reader.GetStringTable(JsonLiteralKind.Field));
		}

		private static unsafe string ParseJsonStringInternal(ref CrystalJsonTokenizer<TReader> reader, StringTable? table)
		{
			// note: we have already parsed the opening double-quote (")

			const int SIZE = 128;

			Span<char> buf = stackalloc char[SIZE];
			using var sb = new ValueBuffer<char>(buf);

			while (true)
			{
				// read a full Unicode code point: this may be greater than 0xFFFF (outside the BMP), or -1 at the end of the stream.
				// note: reading a raw code point (instead of a char) is what allows a genuine U+FFFF to be told apart from EOF.
				int c = reader.ReadOneCodePoint();

				// From most frequent to less frequent:
				// > letters
				// > the last double-quote that ends the string
				// > spaces
				// > '\' used for escaping
				// > EOF (this would only happen if the whole document is a string, usually it is an object or array)

				if (c == '"') break; // must be evaluated BEFORE parsing '\"'
				if (c == '\\')
				{ // decode the escaped character (always within the BMP)
					c = ParseEscapedCharacter(ref reader);
				}
				else if (c < 0)
				{
					throw reader.FailUnexpectedEndOfStream("String is incomplete");
				}

				if (c <= 0xFFFF)
				{
					sb.Add((char) c);
				}
				else
				{ // encode the astral code point as a UTF-16 surrogate pair
					int v = c - 0x10000;
					sb.Add((char) (0xD800 | (v >> 10)));
					sb.Add((char) (0xDC00 | (v & 0x3FF)));
				}
			}

			if (sb.Count == 0)
			{
				return string.Empty;
			}

			//TODO: table for single letter names ?
			if (table != null)
			{ // interning
				return table.Add(sb.Span);
			}
			else
			{
				return sb.Span.ToString();
			}

		}

		private static char ParseEscapedCharacter(ref CrystalJsonTokenizer<TReader> reader)
		{
			char c = reader.ReadOne();
			return c switch
			{
				'\"' or '\\' or '/' => c,
				'b' => '\b',
				'f' => '\f',
				'n' => '\n',
				'r' => '\r',
				't' => '\t',
				'u' => ParseEscapedUnicodeCharacter(ref reader),
				CrystalJsonParser.EndOfStream => throw reader.FailUnexpectedEndOfStream("Invalid string escaping"),
				_ => throw reader.FailInvalidSyntax($@"Invalid escaped character \{c} found in string")
			};
		}

		private static char ParseEscapedUnicodeCharacter(ref CrystalJsonTokenizer<TReader> reader)
		{
			// Format: "\uXXXX" where XXXX = hex digits
			int x = 0;
			for (int i = 0; i < 4; i++)
			{
				char c = reader.ReadOne();

				x <<= 4;
				if ((uint) (c - '0') <= ('9' - '0'))
				{ // c is >= '0' and <= '9'
					x |= (c - 48);
				}
				else if ((uint) (c - 'A') <= ('F' - 'A'))
				{ // c is >= 'A' and <= 'F'
					x |= (c - 55);
				}
				else if ((uint) (c - 'a') <= ('f' - 'a'))
				{ // c is >= 'a' and <= 'f'
					x |= (c - 87);
				}
				else if (c == CrystalJsonParser.EndOfStream)
				{
					throw reader.FailUnexpectedEndOfStream("Invalid Unicode character escaping");
				}
				else
				{
					throw reader.FailInvalidSyntax("Invalid Unicode character escaping");
				}
			}

			return (char) x;
		}

		private static unsafe JsonNumber ParseJsonNumber(ref CrystalJsonTokenizer<TReader> reader, char first)
		{
#if DEBUG_JSON_PARSER
			System.Diagnostics.Debug.WriteLine("CrystalJsonConverter.ParseJsonNumber(...)");
#endif
			const int MAX_NUMBER_CHARS = 64;

			Span<char> buffer = stackalloc char[MAX_NUMBER_CHARS];
			buffer[0] = first;
			int p = 1;
			bool negative = first == '-';
			bool hasDot = false;
			bool hasExponent = false;
			bool hasExponentSign = false;
			bool incomplete = first is < '0' or > '9';
			bool computed = negative || !incomplete;
			bool overflow = false;
			ulong num = incomplete ? 0 : (ulong)(first - '0');
			while (p < MAX_NUMBER_CHARS)
			{
				char c = reader.ReadOne();

				if ((uint) (c - '0') < 10) // "0" .. "9"
				{ // digit
					incomplete = false;
					ulong digit = (ulong)(c - '0');
					if (num > (ulong.MaxValue - digit) / 10) overflow = true; // no longer fits in a UInt64: fall back to the literal parser below
					num = (num * 10) + digit;
				}
				else if (CrystalJsonParser.ValidNumberTrailingCharacters.Contains(c))
				{ // this is a valid end-of-stream character
				  // rewind this character
					reader.Push(c);
					break;
				}
				else if (c == '.')
				{
					if (hasDot)
					{
						throw reader.FailInvalidSyntax($"Invalid number '{buffer[..p].ToString()}.' (duplicate decimal point)");
					}
					incomplete = true;
					hasDot = true;
					computed = false;
				}
				else if (c is 'e' or 'E')
				{ // exponent (scientific form)
					if (hasExponent)
					{
						throw reader.FailInvalidSyntax($"Invalid number '{buffer[..p].ToString()}{c}' (duplicate exponent)");
					}
					incomplete = true; // must be followed by a sign or digit! ("123E" is not valid)
					hasExponent = true;
					computed = false;
				}
				else if (c is '-' or '+')
				{ // sign of the exponent
					if (!hasExponent)
					{
						throw reader.FailInvalidSyntax($"Invalid number '{buffer[..p].ToString()}{c}' (unexpected sign at this location)");
					}
					if (hasExponentSign)
					{
						throw reader.FailInvalidSyntax($"Invalid number '{buffer[..p].ToString()}{c}' (duplicate sign is exponent)");
					}
					incomplete = true; // must be followed by a digit! ("123E-" is not valid)
					hasExponentSign = true;
				}
				else if (c == 'I' && p == 1 && (first is '+' or '-'))
				{ // '+Infinity' / '-Infinity' ?
					ParseSpecialKeyword(ref reader, c);
					//HACKHACK: if this succeeds, then the keyword was "Infinity" as expected
					return negative ? JsonNumber.NegativeInfinity : JsonNumber.PositiveInfinity;
				}
				else if (c == CrystalJsonParser.EndOfStream)
				{ // end of stream => end of number
					break;
				}
				else
				{ // invalid character after a (valid) number => fail
					throw reader.FailInvalidSyntax($"Invalid number '{buffer[..p].ToString()}' (unexpected character '{c}' found)");
				}

				buffer[p++] = c;
			}

			if (incomplete)
			{ // this should always end with a digit!
				throw reader.FailInvalidSyntax("Invalid JSON number (truncated)");
			}

			// if we did not see neither '.' nor exponent, and the number of digits is <= 4, we have a valid integer that will fit in the cache!
			if (computed && p <= 4)
			{
				if (num == 0)
				{
					return JsonNumber.Zero;
				}
				if (num == 1)
				{
					return negative ? JsonNumber.MinusOne : JsonNumber.One;
				}
				if (!negative)
				{
					// le literal est-il en cache?
					if (num <= JsonNumber.CACHED_SIGNED_MAX)
					{
						return JsonNumber.GetCachedSmallNumber((int) num);
					}
				}
				else
				{
					// le literal est-il en cache?
					if (num <= -JsonNumber.CACHED_SIGNED_MIN)
					{
						return JsonNumber.GetCachedSmallNumber(-((int) num));
					}
				}
			}

			if (computed && !overflow)
			{
				if (negative)
				{ // with max 16 digits, there is no risk of overflow due to the negative sign
					return JsonNumber.ParseSigned(-((long) num), null, default);
				}
				else
				{
					return JsonNumber.ParseUnsigned(num, null, default);
				}
			}

			// this is either a floating pointer number, or a large integer that does not fit in the cache
			var literal = buffer[..p];

			// we may get the literal from a string table
			var table = reader.GetStringTable(computed ? JsonLiteralKind.Integer : JsonLiteralKind.Decimal);
			string? original = table?.Add(literal);

			// complete the parsing
			var value = CrystalJsonParser.ParseNumberFromLiteral(literal, original, negative, hasDot, hasExponent);

			return value ?? throw reader.FailInvalidSyntax($"Invalid JSON number '{literal.ToString()}' (malformed)");
		}

		private static JsonValue ParseSpecialKeyword(ref CrystalJsonTokenizer<TReader> reader, char first)
		{
#if DEBUG_JSON_PARSER
			System.Diagnostics.Debug.WriteLine("CrystalJsonConverter.ParseJsonNumber(...)");
#endif

			// read a literal like "NaN", "Infinity", etc...

			char c = first;

			var sb = StringBuilderCache.Acquire();
			sb.Append(c);

			switch (first)
			{
				case 'N':
				{ // NaN
					sb.Append(c = reader.ReadOne());
					if (c != 'a') break;
					sb.Append(c = reader.ReadOne());
					if (c != 'N') break;
					return JsonNumber.NaN;
				}
				case 'I':
				{ // Infinity
					sb.Append(c = reader.ReadOne());
					if (c != 'n') break;
					sb.Append(c = reader.ReadOne());
					if (c != 'f') break;
					sb.Append(c = reader.ReadOne());
					if (c != 'i') break;
					sb.Append(c = reader.ReadOne());
					if (c != 'n') break;
					sb.Append(c = reader.ReadOne());
					if (c != 'i') break;
					sb.Append(c = reader.ReadOne());
					if (c != 't') break;
					sb.Append(c = reader.ReadOne());
					if (c != 'y') break;
					return JsonNumber.PositiveInfinity;
				}
			}

			if (sb[^1] == CrystalJsonParser.EndOfStream)
			{
				sb.Length--;
			}

			throw reader.FailInvalidSyntax($"Invalid literal '{StringBuilderCache.GetStringAndRelease(sb)}'");
		}

		private static JsonObject ParseJsonObject(ref CrystalJsonTokenizer<TReader> reader, int depth)
		{
			if (depth >= MaximumDepth) throw reader.FailInvalidSyntax($"The JSON document exceeds the maximum allowed nesting depth ({MaximumDepth})");
#if DEBUG_JSON_PARSER
			System.Diagnostics.Debug.WriteLine("CrystalJsonConverter.ParseJsonObject(...) [BEGIN]");
#endif

			const int EXPECT_PROPERTY = 0; // Expect a string that contains a name of a new property, or '}' to close the object
			const int EXPECT_VALUE = 1; // Expect a ':' followed by the value of the current property
			const int EXPECT_NEXT = 2; // Expect a ',' to start next property, or '}' to close the object
			int state = EXPECT_PROPERTY;

			char c = '\0';
			string? name = null;
			var createReadOnly = reader.Settings.ReadOnly;

#if NET8_0_OR_GREATER
			var scratch = new SegmentedValueBuffer<KeyValuePair<string, JsonValue>>.Scratch();
			using var props = new SegmentedValueBuffer<KeyValuePair<string, JsonValue>>(scratch);
#else
			// SegmentedValueBuffer requires inline-array runtime support that netstandard2.0/netfx lacks: use a plain List instead
			var props = new List<KeyValuePair<string, JsonValue>>();
#endif

			while (true)
			{
				char prev = c;
				switch (c = reader.ReadNextToken())
				{
					case '"':
					{ // start of property name
						if (state != EXPECT_PROPERTY)
						{
							if (state == EXPECT_VALUE)
							{
								throw reader.FailInvalidSyntax($"Missing colon after field #{props.Count + 1} value");
							}
							else
							{
								throw reader.FailInvalidSyntax($"Missing comma after field #{props.Count}");
							}
						}

						name = ParseJsonName(ref reader);
						// next should be ':'
						state = EXPECT_VALUE;
						break;
					}
					case '}':
					{ // end of object
						if (state != EXPECT_NEXT)
						{
							if (state == EXPECT_PROPERTY && prev == ',' && reader.Settings.DenyTrailingCommas)
							{
								throw reader.FailInvalidSyntax("Missing field before end of object");
							}
							else if (state == EXPECT_VALUE)
							{
								throw reader.FailInvalidSyntax($"Missing value for field #{props.Count} at the end of object definition");
							}
						}
#if DEBUG_JSON_PARSER
						System.Diagnostics.Debug.WriteLine("CrystalJsonConverter.ParseJsonObject(...) [END] read " + map.Count + " fields");
#endif

						if (props.Count == 0)
						{ // empty object
							return createReadOnly ? JsonObject.ReadOnly.Empty : JsonObject.Create();
						}

						// convert into the dictionary
						var map = new Dictionary<string, JsonValue>(props.Count, reader.FieldComparer);
						foreach (var kv in props)
						{
							map[kv.Key] = kv.Value;
#if DEBUG
							if (createReadOnly && !kv.Value.IsReadOnly)
							{
								Contract.Fail("Parsed child was mutable even though the settings are set to Immutable!");
							}
#endif
						}
						var obj = new JsonObject(map, createReadOnly);
						if (obj.Count != props.Count && !reader.Settings.OverwriteDuplicateFields)
						{
							var x = new HashSet<string>(reader.FieldComparer);
							foreach (var kv in props)
							{
								if (!x.Add(kv.Key))
								{
									throw reader.FailInvalidSyntax($"Duplicate field '{kv.Key}' in JSON Object.");
								}
							}
						}
						return obj;
					}
					case ':':
					{ // start of property value
						if (state != EXPECT_VALUE)
						{
							if (state == EXPECT_PROPERTY)
							{
								throw reader.FailInvalidSyntax($"Missing field name after field #{props.Count + 1}");
							}
							else if (name != null)
							{
								throw reader.FailInvalidSyntax($"Duplicate colon after field #{props.Count + 1} '{name}'");
							}
							else
							{
								throw reader.FailInvalidSyntax($"Unexpected semicolon after field #{props.Count + 1}");
							}
						}
						// must be immediately followed by a value

						props.Add(new (name!, ParseJsonValue(ref reader, depth + 1)!));
						// next should be ',' or '}'
						state = EXPECT_NEXT;
						name = null;
						break;
					}
					case ',':
					{ // next field
						if (state != EXPECT_NEXT)
						{
							if (name != null)
							{
								throw reader.FailInvalidSyntax($"Unexpected comma after name of field #{props.Count + 1} ");
							}
							else
							{
								throw reader.FailInvalidSyntax($"Unexpected comma after field #{props.Count + 1}");
							}
						}

						// next should be '"' or '}' if trailing commas are allowed
						state = EXPECT_PROPERTY;
						break;
					}
					case '/':
					{ // comment
						ParseComment(ref reader);
						break;
					}
					default:
					{ // object
						if (c == CrystalJsonParser.EndOfStream)
						{
							throw reader.FailUnexpectedEndOfStream("Incomplete object definition");
						}
						if (state == EXPECT_NEXT)
						{
							throw reader.FailInvalidSyntax($"Missing comma after field #{props.Count + 1}");
						}
						if (state == EXPECT_VALUE)
						{
							throw reader.FailInvalidSyntax($"Missing semicolon after field '{name!}' value");
						}
						if (c == ']')
						{
							throw reader.FailInvalidSyntax("Unexpected ']' encountered inside an object. Did you forget to close the object?");
						}

						if (char.IsLetterOrDigit(c))
						{
							throw reader.FailInvalidSyntax($"Missing required '\"' before property name starting with '{c}' encountered inside an object.");
						}
						throw reader.FailInvalidSyntax($"Invalid character '{c}' after field #{props.Count + 1}");
					}
				}
			}
		}

		private static void ParseComment(ref CrystalJsonTokenizer<TReader> reader)
		{
#if DEBUG_JSON_PARSER
			System.Diagnostics.Debug.WriteLine("CrystalJsonConverter.ParseJsonComment(...) [BEGIN]");
#endif

			// we already consumed the first '/', we expect the next character to be either '/' (single line) or '*' (multi line)

			char c = reader.ReadOne();
			switch(c)
			{
				case '/':
				{ // read until next CRLF
					SkipSingleLineComment(ref reader);
					break;
				}
				case '*':
				{ // read until next */
					SkipMultiLineComment(ref reader);
					break;
				}
				default:
				{
					if (c == CrystalJsonParser.EndOfStream)
					{
						throw reader.FailUnexpectedEndOfStream("Incomplete comment");
					}
					else
					{
						throw reader.FailInvalidSyntax($"Invalid character '{c}' after comment start");
					}
				}
			}
		}

		/// <summary>Skip a single-line comment</summary>
		/// <remarks>The reader will be placed after the next '\n'</remarks>
		private static void SkipSingleLineComment(ref CrystalJsonTokenizer<TReader> reader)
		{
			// the reader is just after "//" and we will read until next LF or end of stream
			char c;
			do
			{
				c = reader.ReadOne();
			}
			while (c is not ('\n' or CrystalJsonParser.EndOfStream));
		}

		/// <summary>Skip a multi-line comment</summary>
		/// <remarks>The reader will be place after the final '*/'</remarks>
		private static void SkipMultiLineComment(ref CrystalJsonTokenizer<TReader> reader)
		{
			// the reader is just after "/*" and we will read until the next '*' followed by '/'
			while(true)
			{
				char c = reader.ReadOne();
				if (c == '*')
				{
					switch((c = reader.ReadOne()))
					{

						case '/': return;
						case '*': reader.Push(c); continue;
						case CrystalJsonParser.EndOfStream: throw reader.FailUnexpectedEndOfStream("Truncated multi-line comment");
					}
				}
				else if (c == CrystalJsonParser.EndOfStream)
				{
					throw reader.FailUnexpectedEndOfStream("Truncated multi-line comment");
				}
			}
		}

		private static JsonArray ParseJsonArray(ref CrystalJsonTokenizer<TReader> reader, int depth)
		{
			if (depth >= MaximumDepth) throw reader.FailInvalidSyntax($"The JSON document exceeds the maximum allowed nesting depth ({MaximumDepth})");
#if DEBUG_JSON_PARSER
			System.Diagnostics.Debug.WriteLine("CrystalJsonConverter.ParseJsonArray(...) [BEGIN]");
#endif
			// on a déjà le ']'

			bool commaRequired = false;
			bool valueRequired = false;
			bool readOnly = reader.Settings.ReadOnly;

#if NET8_0_OR_GREATER
			var scratch = new SegmentedValueBuffer<JsonValue>.Scratch();
			using var buffer = new SegmentedValueBuffer<JsonValue>(scratch);
#else
			// SegmentedValueBuffer requires inline-array runtime support that netstandard2.0/netfx lacks: use a plain List instead
			var buffer = new List<JsonValue>();
#endif

			while (true)
			{
				char c = reader.ReadNextToken();

				if (c == CrystalJsonParser.EndOfStream)
				{
					throw reader.FailUnexpectedEndOfStream("Array is incomplete");
				}

				if (c == ']')
				{
					if (valueRequired && reader.Settings.DenyTrailingCommas) throw reader.FailInvalidSyntax("Missing value before end of array");
#if DEBUG_JSON_PARSER
					System.Diagnostics.Debug.WriteLine("CrystalJsonConverter.ParseJsonArray(...) [END] read " + list.Count + " values");
#endif
					if (buffer.Count == 0)
					{ // empty object
						return readOnly ? JsonArray.ReadOnly.Empty : new JsonArray();
					}

					var tmp = buffer.ToArray();
					return new(tmp, tmp.Length, readOnly);
				}

				if (c == ',')
				{
					if (!commaRequired) throw reader.FailInvalidSyntax("Unexpected comma in array");
					commaRequired = false;
					valueRequired = true;
				}
				else
				{
					if (commaRequired) throw reader.FailInvalidSyntax("Missing comma between two items of an array");
					reader.Push(c);

					var val = ParseJsonValue(ref reader, depth + 1) ?? throw reader.FailUnexpectedEndOfStream("Array is incomplete");
					buffer.Add(val);
					commaRequired = true;
					valueRequired = false;
				}
			}
		}

		#endregion

	}

}
