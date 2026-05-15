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

// ReSharper disable CompareOfFloatsByEqualityOperator

namespace SnowBank.Data.Json
{
	using System.Text;
	using SnowBank.Buffers.Text;
	using SnowBank.Runtime.Converters;
	using SnowBank.Text;

	[PublicAPI]
	public static class CrystalJsonFormatter
	{

		public static void WriteJavaScriptPropertyName(ref ValueStringWriter writer, string name)
		{
			Contract.NotNull(name);
			WriteJavaScriptPropertyName(ref writer, name.AsSpan());
		}

		public static void WriteJavaScriptPropertyName(ref ValueStringWriter writer, ReadOnlySpan<char> name)
		{
			if (name.Length == 0)
			{ // "''"
				writer.Write("''");
			}
			else if (JavaScriptEncoding.IsCleanJavaScriptPropertyName(name))
			{ // "foo"
				writer.Write(name);
			}
			else
			{ // "'foo\'bar'"
				//TODO: optimize!
				writer.Write(JavaScriptEncoding.EncodeSlow(new StringBuilder(), name, includeQuotes: true).ToString());
			}
		}

		public static void WriteJavaScriptString(ref ValueStringWriter writer, string? text)
		{
			if (text == null)
			{ // "null"
				writer.Write(JsonTokens.Null);
			}
			else
			{
				WriteJavaScriptString(ref writer, text.AsSpan());
			}
		}

		public static void WriteJavaScriptString(ref ValueStringWriter writer, ReadOnlySpan<char> text)
		{
			if (text.Length == 0)
			{ // "''"
				writer.Write("''");
			}
			else if (JavaScriptEncoding.IsCleanJavaScript(text))
			{ // "'foo bar'"
				writer.Write('\'', text, '\'');
			}
			else
			{ // "'foo\'bar'"
				//TODO: optimize!
				writer.Write(JavaScriptEncoding.EncodeSlow(new StringBuilder(), text, includeQuotes: true).ToString());
			}
		}

		public static string EncodeJavaScriptString(string? text)
		{
			if (text == null)
			{ // => null
				return JsonTokens.Null;
			}
			if (text.Length == 0)
			{ // => ""
				return "\"\"";
			}
			return JavaScriptEncoding.IsCleanJavaScript(text.AsSpan())
				? string.Concat("'", text, "'")
				: JavaScriptEncoding.EncodeSlow(new StringBuilder(), text.AsSpan(), includeQuotes: true).ToString();
		}

		internal static void WriteFixedIntegerWithDecimalPartUnsafe(ref ValueStringWriter output, long integer, long decimals, int digits)
		{
			Span<char> buf = stackalloc char[StringConverters.Base10MaxCapacityInt64 + 1 + digits];

			// The number X is split into (INTEGER, DECIMALS, DIGITS) such that X = INTEGER + (DECIMALS / 10^DIGITS)
			//                   <-- 'DIGITS' -->
			// [  INTEGER  ] '.' [000...DECIMALS]

			// Examples:
			// - (integer: 123, dec: 456, digits: 3) => "123.456"
			// - (integer: 123, dec: 456, digits: 4) => "123.0456"
			// - (integer: 123, dec: 456, digits: 5) => "123.00456"
			// - (integer: 123, dec:   1, digits: 3) => "123.001" // we pad with '0' between '.' and the first non-0 digit in the decimal part
			// - (integer: 123, dec:  10, digits: 3) => "123.01"  // we truncate the trailing '0' in the decimal part
			// - (integer: 123, dec:   0, digits: 3) => "123"     // we completely skip the decimal part if it is 0

			int len = buf.Length;
			int p = buf.Length - 1;

			// decimal part (if required)
			long value = decimals;

			if (value != 0 && digits != 0)
			{
				bool allZero = true; // set to false as soon as we find a non-zero digit
				for (int i = 0; i < digits; i++)
				{
					int d = (int) (value % 10);
					buf[p--] = (char) ('0' + d);
					value /= 10;
					if (d == 0 && allZero)
					{ // truncate trailing 0
						len--;
					}
					else
					{ // non-zero digit
						allZero = false;
					}
				}
				buf[p--] = '.';
			}

			// integer part
			value = integer;
			bool neg = value < 0;
			value = Math.Abs(value);
			do
			{
				buf[p--] = (char) (48 + (value % 10));
				value /= 10;
			}
			while (value > 0);

			if (neg) buf[p] = '-';
			else ++p;

			output.Write(buf.Slice(p, len - p));
		}

		internal static string GetNaNToken(CrystalJsonSettings.FloatFormat format) =>
			format switch
			{
				CrystalJsonSettings.FloatFormat.Default => JsonTokens.SymbolNaN,
				CrystalJsonSettings.FloatFormat.Symbol => JsonTokens.SymbolNaN,
				CrystalJsonSettings.FloatFormat.String => JsonTokens.StringNaN,
				CrystalJsonSettings.FloatFormat.Null => JsonTokens.Null,
				CrystalJsonSettings.FloatFormat.JavaScript => JsonTokens.JavaScriptNaN,
				_ => throw new ArgumentException(null, nameof(format))
			};

		internal static string GetPositiveInfinityToken(CrystalJsonSettings.FloatFormat format) =>
			format switch
			{
				CrystalJsonSettings.FloatFormat.Default => JsonTokens.SymbolInfinityPos,
				CrystalJsonSettings.FloatFormat.Symbol => JsonTokens.SymbolInfinityPos,
				CrystalJsonSettings.FloatFormat.String => JsonTokens.StringInfinityPos,
				CrystalJsonSettings.FloatFormat.Null => JsonTokens.Null,
				CrystalJsonSettings.FloatFormat.JavaScript => JsonTokens.JavaScriptInfinityPos,
				_ => throw new ArgumentException(null, nameof(format))
			};

		internal static string GetNegativeInfinityToken(CrystalJsonSettings.FloatFormat format) =>
			format switch
			{
				CrystalJsonSettings.FloatFormat.Default => JsonTokens.SymbolInfinityNeg,
				CrystalJsonSettings.FloatFormat.Symbol => JsonTokens.SymbolInfinityNeg,
				CrystalJsonSettings.FloatFormat.String => JsonTokens.StringInfinityNeg,
				CrystalJsonSettings.FloatFormat.Null => JsonTokens.Null,
				CrystalJsonSettings.FloatFormat.JavaScript => JsonTokens.JavaScriptInfinityNeg,
				_ => throw new ArgumentException(null, nameof(format))
			};

		private static readonly int[] DaysToMonth365 = [ 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334, 365 ];
		private static readonly int[] DaysToMonth366 = [ 0, 31, 60, 91, 121, 152, 182, 213, 244, 274, 305, 335, 366 ];

		internal const int ISO8601_MAX_FORMATTED_SIZE = 35; // With quotes and TimeZone

		public static string ToIso8601String(DateTime date)
		{
			if (date == DateTime.MinValue) return string.Empty;

			Span<char> buf = stackalloc char[ISO8601_MAX_FORMATTED_SIZE];
			return FormatIso8601DateTime(buf, date, date.Kind, null, quotes: '\0').ToString();
		}

		public static string ToIso8601String(DateTimeOffset date)
		{
			if (date == DateTimeOffset.MinValue) return string.Empty;

			Span<char> buf = stackalloc char[ISO8601_MAX_FORMATTED_SIZE];
			return FormatIso8601DateTime(buf, date.DateTime, DateTimeKind.Local, date.Offset, quotes: '\0').ToString();
		}

		public static string ToIso8601String(DateOnly date)
		{
			if (date == DateOnly.MinValue) return string.Empty;

			Span<char> buf = stackalloc char[ISO8601_MAX_FORMATTED_SIZE];
			return FormatIso8601DateTime(buf, date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified, null, quotes: '\0', omitTimeIfZero: true).ToString();
		}

		internal static ReadOnlySpan<char> FormatIso8601DateTime(Span<char> output, DateTime date, DateTimeKind kind, TimeSpan? utcOffset, char quotes = '\0', bool omitTimeIfZero = false)
		{
			// we will need between 28 and 33 (+2 with quotes) characters for the buffer
			if (output.Length < ISO8601_MAX_FORMATTED_SIZE) ThrowHelper.ThrowArgumentException(nameof(output), "Output buffer size is too small");

			GetDateParts(date.Ticks, out var year, out var month, out var day, out var hour, out var min, out var sec, out var millis);

			bool hasTimePart = !omitTimeIfZero || (hour | min | sec | millis) != 0;

			ref char cursor = ref output[0];

			if (quotes != '\0')
			{
				cursor = quotes;
				cursor = ref Unsafe.Add(ref cursor, 1);
			}

			cursor = ref FormatDatePart(ref cursor, year, month, day);

			if (hasTimePart)
			{
				cursor = 'T';
				cursor = ref Unsafe.Add(ref cursor, 1);

				cursor = ref FormatTimePart(ref cursor, hour, min, sec, millis);
			}

			if (kind == DateTimeKind.Utc)
			{ // "Z"
				cursor = 'Z';
				cursor = ref Unsafe.Add(ref cursor, 1);
			} 
			else if (utcOffset.HasValue)
			{
				cursor = ref FormatTimeZoneOffset(ref cursor, utcOffset.Value, kind == DateTimeKind.Local);
			}
			else if (kind == DateTimeKind.Local)
			{
				cursor = ref FormatTimeZoneOffset(ref cursor, TimeZoneInfo.Local.GetUtcOffset(date), true);
			}

			if (quotes != '\0')
			{
				cursor = quotes;
				cursor = ref Unsafe.Add(ref cursor, 1);
			}

			int offset = (int) (Unsafe.ByteOffset(ref output[0], ref cursor).ToInt64() / Unsafe.SizeOf<char>());
			if ((uint) offset > output.Length) throw ThrowHelper.InvalidOperationException("Internal formatting error");

			return output[..offset];
		}

		internal static int ComputeIso8601DateTimeSize(bool hasTimeComponent, bool hasMilliseconds, DateTimeKind kind, TimeSpan? utcOffset, char quotes)
		{
			// Compute the exact required size
			// - 'YYYY-DD-MM' => at least 10
			// - '___THH:MM:SS___' => +9
			// - '"...."' if quotes != 0 => +2
			// - '___.0000000____" if there are milliseconds => +8
			// - '___Z" if UTC => +1
			// - '___' if no offset and kind unspecified => +0
			// - '___+XX:XX" if offset => +6

			return (quotes == '\0' ? 0 : 2) + (hasTimeComponent ? 19 : 10) + (hasMilliseconds ? 8 : 0) + ((kind == DateTimeKind.Utc ? 1 : (kind == DateTimeKind.Local || utcOffset != null) ? 6 : 0));
		}

		internal static bool TryFormatIso8601DateTime(Span<char> output, out int charsWritten, DateTime date, DateTimeKind kind, TimeSpan? utcOffset, char quotes = '\0', bool omitTimeIfZero = false)
		{
			GetDateParts(date.Ticks, out var year, out var month, out var day, out var hour, out var min, out var sec, out var millis);

			bool hasTimePart = !omitTimeIfZero || (hour | min | sec | millis) != 0;

			int size = ComputeIso8601DateTimeSize(hasTimePart, millis != 0, kind, utcOffset, quotes);

			// we will need between 28 and 33 (+2 with quotes) characters for the buffer
			if (output.Length < size)
			{
				charsWritten = 0;
				return false;
			}

			ref char cursor = ref output[0];

			if (quotes != '\0')
			{
				cursor = quotes;
				cursor = ref Unsafe.Add(ref cursor, 1);
			}

			cursor = ref FormatDatePart(ref cursor, year, month, day);

			if (hasTimePart)
			{
				cursor = 'T';
				cursor = ref Unsafe.Add(ref cursor, 1);
				cursor = ref FormatTimePart(ref cursor, hour, min, sec, millis);
			}

			switch (kind)
			{
				case DateTimeKind.Utc:
				{ // "Z"
					cursor = 'Z';
					cursor = ref Unsafe.Add(ref cursor, 1);
					break;
				}
				case DateTimeKind.Local:
				{
					cursor = ref FormatTimeZoneOffset(ref cursor, utcOffset ?? TimeZoneInfo.Local.GetUtcOffset(date), true);
					break;
				}
				default:
				{
					if (utcOffset is not null)
					{
						cursor = ref FormatTimeZoneOffset(ref cursor, utcOffset.Value, true);
					}
					break;
				}
			}

			if (quotes != '\0')
			{
				cursor = quotes;
				cursor = ref Unsafe.Add(ref cursor, 1);
			}

			{
				int offset = (int) (Unsafe.ByteOffset(ref output[0], ref cursor).ToInt64() / Unsafe.SizeOf<char>());
				if (offset > size) throw ThrowHelper.InvalidOperationException("Internal formatting error");

				charsWritten = offset;
				return true;
			}
		}

		internal static ReadOnlySpan<char> FormatIso8601DateOnly(Span<char> output, DateOnly date, char quotes = '\0')
		{
			// we will need between 28 and 33 (+2 with quotes) characters for the buffer
			if (output.Length < ISO8601_MAX_FORMATTED_SIZE) ThrowHelper.ThrowArgumentException(nameof(output), "Output buffer size is too small");

#if NET8_0_OR_GREATER
			date.Deconstruct(out var year, out var month, out var day);
#else
			int year = date.Year;
			int month = date.Month;
			int day = date.Day;
#endif

			ref char cursor = ref output[0];
			if (quotes != '\0')
			{
				cursor = quotes;
				cursor = ref Unsafe.Add(ref cursor, 1);
			}

			cursor = ref FormatDatePart(ref cursor, year, month, day);

			if (quotes != '\0')
			{
				cursor = quotes;
				cursor = ref Unsafe.Add(ref cursor, 1);
			}

			return output[..(int) (Unsafe.ByteOffset(ref output[0], ref cursor).ToInt64() / Unsafe.SizeOf<char>())];
		}

		private static ref char FormatDatePart(ref char ptr, int year, int month, int day)
		{
			Paranoid.Requires(year >= 0 && month is >= 1 and <= 12 && day is >= 1 and <= 31);

			// Year
			Unsafe.Add(ref ptr, 3) = (char) ('0' + (year % 10)); year /= 10;
			Unsafe.Add(ref ptr, 2) = (char) ('0' + (year % 10)); year /= 10;
			Unsafe.Add(ref ptr, 1) = (char) ('0' + (year % 10));
			Unsafe.Add(ref ptr, 0) = (char) ('0' + (year / 10));

			// Month
			Unsafe.Add(ref ptr, 4) = '-';
			Unsafe.Add(ref ptr, 5) = (char) ('0' + (month / 10));
			Unsafe.Add(ref ptr, 6) = (char) ('0' + (month % 10));

			// Day
			Unsafe.Add(ref ptr, 7) = '-';
			Unsafe.Add(ref ptr, 8) = (char) ('0' + (day / 10));
			Unsafe.Add(ref ptr, 9) = (char) ('0' + (day % 10));

			return ref Unsafe.Add(ref ptr, 10);
		}

		private static ref char FormatTimePart(ref char ptr, int hour, int min, int sec, int ticks)
		{
			Paranoid.Requires(hour is >= 0 and < 24 && min is >= 0 and < 60 && sec is >= 0 and < 60 && ticks >= 0 && ticks < TimeSpan.TicksPerSecond);

			Unsafe.Add(ref ptr, 0) = (char)(48 + (hour / 10));
			Unsafe.Add(ref ptr, 1) = (char)(48 + (hour % 10));

			// Minutes
			Unsafe.Add(ref ptr, 2) = ':';
			Unsafe.Add(ref ptr, 3) = (char)(48 + (min / 10));
			Unsafe.Add(ref ptr, 4) = (char)(48 + (min % 10));

			// Seconds
			Unsafe.Add(ref ptr, 5) = ':';
			Unsafe.Add(ref ptr, 6) = (char)(48 + (sec / 10));
			Unsafe.Add(ref ptr, 7) = (char)(48 + (sec % 10));

			ptr = ref Unsafe.Add(ref ptr, 8);

			if (ticks > 0)
			{ // writes the milliseconds (7 digits)

				Unsafe.Add(ref ptr, 0) = '.';
				Unsafe.Add(ref ptr, 7) = (char) (48 + (ticks % 10)); ticks /= 10;
				Unsafe.Add(ref ptr, 6) = (char) (48 + (ticks % 10)); ticks /= 10;
				Unsafe.Add(ref ptr, 5) = (char) (48 + (ticks % 10)); ticks /= 10;
				Unsafe.Add(ref ptr, 4) = (char) (48 + (ticks % 10)); ticks /= 10;
				Unsafe.Add(ref ptr, 3) = (char) (48 + (ticks % 10)); ticks /= 10;
				Unsafe.Add(ref ptr, 2) = (char) (48 + (ticks % 10));
				Unsafe.Add(ref ptr, 1) = (char) (48 + (ticks / 10));

				ptr = ref Unsafe.Add(ref ptr, 8);
			}

			return ref ptr;

		}

		private static ref char FormatTimeZoneOffset(ref char ptr, TimeSpan utcOffset, bool forceLocal)
		{
			// special case: we still output 'Z' for DateTimeOffset with GMT offset, since we cannot distinguish with values set to UTC
			// => we may mix up times set to GMT offset with UTC times, but if the server is set to GMT without any DST, this should not change the actual instant
			if (utcOffset == TimeSpan.Zero && !forceLocal)
			{ // "Z"
				Unsafe.Add(ref ptr, 0) = 'Z';
				return ref Unsafe.Add(ref ptr, 1);
			}
			
			// "+HH:MM"

			int minutes = (int) (utcOffset.Ticks / TimeSpan.TicksPerMinute);
			Unsafe.Add(ref ptr, 0) = minutes >= 0 ? '+' : '-';

			minutes = Math.Abs(minutes);
			int hour = minutes / 60;
			minutes %= 60;

			Unsafe.Add(ref ptr, 1) = (char)(48 + (hour / 10));
			Unsafe.Add(ref ptr, 2) = (char)(48 + (hour % 10));
			Unsafe.Add(ref ptr, 3) = ':';
			Unsafe.Add(ref ptr, 4) = (char)(48 + (minutes / 10));
			Unsafe.Add(ref ptr, 5) = (char)(48 + (minutes % 10));
			return ref Unsafe.Add(ref ptr, 6);
		}

		public static void GetDateParts(long ticks, out int year, out int month, out int day, out int hour, out int minute, out int second, out int remainder)
		{
			// n = number of days since 1/1/0001
			int n = (int)(ticks / (TimeSpan.TicksPerSecond * 86400));
			// y400 = number of whole 400-year periods since 1/1/0001
			int y400 = n / 146097;
			// n = day number within 400-year period
			n -= y400 * 146097;
			// y100 = number of whole 100-year periods within 400-year period
			int y100 = n / 36524;
			// Last 100-year period has an extra day, so decrement result if 4
			if (y100 == 4) y100 = 3;

			// n = day number within 100-year period
			n -= y100 * 36524;
			// y4 = number of whole 4-year periods within 100-year period
			int y4 = n / 1461;
			// n = day number within 4-year period
			n -= y4 * 1461;
			// y1 = number of whole years within 4-year period
			int y1 = n / 365;
			// Last year has an extra day, so decrement result if 4
			if (y1 == 4) y1 = 3;

			year = y400 * 400 + y100 * 100 + y4 * 4 + y1 + 1;

			// n = day number within year
			n -= y1 * 365;

			// Leap year calculation looks different from IsLeapYear since y1, y4,
			// and y100 are relative to year 1, not year 0
			var days = ((y1 == 3) && ((y4 != 24) || (y100 == 3))) ? DaysToMonth366 : DaysToMonth365;
			// All months have less than 32 days, so n >> 5 is a good conservative
			// estimate for the month
			int m = (n >> 5) + 1;
			// m = 1-based month number
			while (n >= days[m]) m++;
			month = m;
			// Return 1-based day-of-month
			day = (n - days[m - 1]) + 1;

			hour = (int)((ticks / 36000000000L) % 24L);
			minute = (int)((ticks / 600000000L) % 60L);
			second = (int)((ticks / 10000000L) % 60L);
			remainder = (int)(ticks % TimeSpan.TicksPerSecond);
		}

	}

}
