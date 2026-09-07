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
	using System.Runtime.InteropServices;
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

		/// <summary>Maximum size of an ISO 8601 date, with quotes, nine fraction digits and a time zone offset</summary>
		internal const int ISO8601_MAX_FORMATTED_SIZE = 40;

		/// <summary>The decimal digits of 0 to 99, as pairs of ASCII characters</summary>
		private static ReadOnlySpan<byte> TwoDigits => "00010203040506070809101112131415161718192021222324252627282930313233343536373839404142434445464748495051525354555657585960616263646566676869707172737475767778798081828384858687888990919293949596979899"u8;

		/// <summary>No suffix after the time</summary>
		private const int SuffixNone = 0;

		/// <summary>The <c>Z</c> suffix</summary>
		private const int SuffixUtc = 1;

		/// <summary>A <c>+HH:MM</c> suffix</summary>
		private const int SuffixOffset = 2;

		/// <summary>Parts of an ISO 8601 date, kept as the state of a <see cref="string.Create{TState}"/> call</summary>
		private readonly struct Iso8601Parts
		{
			public readonly int Days;
			public readonly uint SecondOfDay;
			public readonly uint Nanos;
			public readonly bool HasTime;
			public readonly int Suffix;
			public readonly int OffsetMinutes;
			public readonly char Quotes;

			public Iso8601Parts(int days, uint secondOfDay, uint nanos, bool hasTime, int suffix, int offsetMinutes, char quotes)
			{
				this.Days = days;
				this.SecondOfDay = secondOfDay;
				this.Nanos = nanos;
				this.HasTime = hasTime;
				this.Suffix = suffix;
				this.OffsetMinutes = offsetMinutes;
				this.Quotes = quotes;
			}
		}

		public static string ToIso8601String(DateTime date)
		{
			if (date == DateTime.MinValue) return string.Empty;
			return ToIso8601String(date, date.Kind, null, omitTimeIfZero: false);
		}

		public static string ToIso8601String(DateTimeOffset date)
		{
			if (date == DateTimeOffset.MinValue) return string.Empty;
			return ToIso8601String(date.DateTime, DateTimeKind.Local, date.Offset, omitTimeIfZero: false);
		}

		public static string ToIso8601String(DateOnly date)
		{
			if (date == DateOnly.MinValue) return string.Empty;
#if NET8_0_OR_GREATER
			return string.Create(10, date, static (span, d) =>
			{
				d.Deconstruct(out var year, out var month, out var day);
				FormatDatePart(ref span[0], (uint) year, (uint) month, (uint) day);
			});
#else
			Span<char> buf = stackalloc char[ISO8601_MAX_FORMATTED_SIZE];
			return FormatIso8601DateOnly(buf, date, quotes: '\0').ToString();
#endif
		}

		/// <summary>Formats a date using the ISO 8601 format, into a new string of the exact size</summary>
		/// <param name="date">Date to format (only the ticks are used, the kind is taken from <paramref name="kind"/>)</param>
		/// <param name="kind">Kind that decides the suffix: <c>Z</c> for UTC, an offset for local, nothing for unspecified</param>
		/// <param name="utcOffset">Explicit offset (for a <see cref="DateTimeOffset"/>), or <see langword="null"/> to use the offset of the local time zone</param>
		/// <param name="omitTimeIfZero">If <see langword="true"/>, a date at midnight is written as <c>YYYY-MM-DD</c></param>
		/// <param name="quotes">Character used to quote the result, or the null character for none</param>
		internal static string ToIso8601String(DateTime date, DateTimeKind kind, TimeSpan? utcOffset, bool omitTimeIfZero, char quotes = '\0')
		{
			SplitTicks(date.Ticks, out int days, out uint secondOfDay, out uint nanos);
			bool hasTime = !omitTimeIfZero || (secondOfDay | nanos) != 0;
			int suffix = ResolveSuffix(date, kind, utcOffset, forceLocal: kind == DateTimeKind.Local, out int offsetMinutes);
			return CreateString(new Iso8601Parts(days, secondOfDay, nanos, hasTime, suffix, offsetMinutes, quotes));
		}

		/// <summary>Formats an instant using the ISO 8601 format, into a new string of the exact size</summary>
		/// <param name="instant">Instant to format, on or after 0001-01-01T00:00:00Z</param>
		/// <param name="quotes">Character used to quote the result, or the null character for none</param>
		/// <remarks>An instant with tick precision has the same text as the equivalent UTC <see cref="System.DateTime"/>. Nanoseconds below the tick add two more fraction digits, so that the instant parses back without loss.</remarks>
		internal static string ToIso8601String(NodaTime.Instant instant, char quotes = '\0')
		{
			SplitInstant(instant, out int days, out uint secondOfDay, out uint nanos);
			return CreateString(new Iso8601Parts(days, secondOfDay, nanos, hasTime: true, SuffixUtc, 0, quotes));
		}

		/// <summary>Writes the parts into a new string of the exact size</summary>
		private static string CreateString(in Iso8601Parts parts)
		{
			int size = ComputeSize(parts.Nanos, parts.HasTime, parts.Suffix, parts.Quotes);
#if NET8_0_OR_GREATER
			return string.Create(size, parts, static (span, p) => WriteIso8601(ref span[0], p.Days, p.SecondOfDay, p.Nanos, p.HasTime, p.Suffix, p.OffsetMinutes, p.Quotes));
#else
			Span<char> buf = stackalloc char[ISO8601_MAX_FORMATTED_SIZE];
			WriteIso8601(ref buf[0], parts.Days, parts.SecondOfDay, parts.Nanos, parts.HasTime, parts.Suffix, parts.OffsetMinutes, parts.Quotes);
			return buf[..size].ToString();
#endif
		}

		/// <summary>Formats a date using the ISO 8601 format: <c>YYYY-MM-DDTHH:mm:ss[.fffffff][Z|+HH:MM]</c></summary>
		/// <param name="output">Buffer of at least <see cref="ISO8601_MAX_FORMATTED_SIZE"/> characters</param>
		/// <param name="date">Date to format (only the ticks are used, the kind is taken from <paramref name="kind"/>)</param>
		/// <param name="kind">Kind that decides the suffix: <c>Z</c> for UTC, an offset for local, nothing for unspecified</param>
		/// <param name="utcOffset">Explicit offset (for a <see cref="DateTimeOffset"/>), or <see langword="null"/> to use the offset of the local time zone</param>
		/// <param name="quotes">Character used to quote the result, or the null character for none</param>
		/// <param name="omitTimeIfZero">If <see langword="true"/>, a date at midnight is written as <c>YYYY-MM-DD</c></param>
		/// <returns>Slice of <paramref name="output"/> that contains the formatted date</returns>
		internal static ReadOnlySpan<char> FormatIso8601DateTime(Span<char> output, DateTime date, DateTimeKind kind, TimeSpan? utcOffset, char quotes = '\0', bool omitTimeIfZero = false)
		{
			if (output.Length < ISO8601_MAX_FORMATTED_SIZE) ThrowHelper.ThrowArgumentException(nameof(output), "Output buffer size is too small");

			SplitTicks(date.Ticks, out int days, out uint secondOfDay, out uint nanos);
			bool hasTime = !omitTimeIfZero || (secondOfDay | nanos) != 0;
			int suffix = ResolveSuffix(date, kind, utcOffset, forceLocal: kind == DateTimeKind.Local, out int offsetMinutes);
			int size = ComputeSize(nanos, hasTime, suffix, quotes);
			WriteIso8601(ref output[0], days, secondOfDay, nanos, hasTime, suffix, offsetMinutes, quotes);
			return output[..size];
		}

		/// <summary>Formats an instant using the ISO 8601 format: <c>YYYY-MM-DDTHH:mm:ss[.fffffff[ff]]Z</c></summary>
		/// <param name="output">Buffer of at least <see cref="ISO8601_MAX_FORMATTED_SIZE"/> characters</param>
		/// <param name="instant">Instant to format, on or after 0001-01-01T00:00:00Z</param>
		/// <param name="quotes">Character used to quote the result, or the null character for none</param>
		/// <returns>Slice of <paramref name="output"/> that contains the formatted instant</returns>
		/// <remarks>An instant with tick precision has the same text as the equivalent UTC <see cref="System.DateTime"/>. Nanoseconds below the tick add two more fraction digits, so that the instant parses back without loss.</remarks>
		internal static ReadOnlySpan<char> FormatIso8601Instant(Span<char> output, NodaTime.Instant instant, char quotes = '\0')
		{
			if (output.Length < ISO8601_MAX_FORMATTED_SIZE) ThrowHelper.ThrowArgumentException(nameof(output), "Output buffer size is too small");

			SplitInstant(instant, out int days, out uint secondOfDay, out uint nanos);
			int size = ComputeSize(nanos, hasTime: true, SuffixUtc, quotes);
			WriteIso8601(ref output[0], days, secondOfDay, nanos, hasTime: true, SuffixUtc, 0, quotes);
			return output[..size];
		}

		internal static bool TryFormatIso8601DateTime(Span<char> output, out int charsWritten, DateTime date, DateTimeKind kind, TimeSpan? utcOffset, char quotes = '\0', bool omitTimeIfZero = false)
		{
			SplitTicks(date.Ticks, out int days, out uint secondOfDay, out uint nanos);
			bool hasTime = !omitTimeIfZero || (secondOfDay | nanos) != 0;
			// an explicit offset of zero is written as "+00:00", even for an unspecified kind
			int suffix = ResolveSuffix(date, kind, utcOffset, forceLocal: true, out int offsetMinutes);
			int size = ComputeSize(nanos, hasTime, suffix, quotes);
			if (output.Length < size)
			{
				charsWritten = 0;
				return false;
			}
			WriteIso8601(ref output[0], days, secondOfDay, nanos, hasTime, suffix, offsetMinutes, quotes);
			charsWritten = size;
			return true;
		}

		internal static ReadOnlySpan<char> FormatIso8601DateOnly(Span<char> output, DateOnly date, char quotes = '\0')
		{
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

			cursor = ref FormatDatePart(ref cursor, (uint) year, (uint) month, (uint) day);

			if (quotes != '\0')
			{
				cursor = quotes;
				cursor = ref Unsafe.Add(ref cursor, 1);
			}

			return output[..(int) (Unsafe.ByteOffset(ref output[0], ref cursor).ToInt64() / Unsafe.SizeOf<char>())];
		}

		/// <summary>Splits the ticks of a date into the day number, the second within the day, and the nanoseconds within the second</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void SplitTicks(long ticks, out int days, out uint secondOfDay, out uint nanos)
		{
			// unsigned arithmetic lets the JIT use the cheaper multiply-high sequence for each constant division
			ulong t = (ulong) ticks;
			ulong d = t / TimeSpan.TicksPerDay;
			ulong ticksOfDay = t - (d * TimeSpan.TicksPerDay);
			uint sod = (uint) (ticksOfDay / TimeSpan.TicksPerSecond);
			days = (int) d;
			secondOfDay = sod;
			nanos = (uint) (ticksOfDay - (sod * (ulong) TimeSpan.TicksPerSecond)) * 100;
		}

		/// <summary>Splits an instant on or after 0001-01-01T00:00:00Z into the day number, the second within the day, and the nanoseconds within the second</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void SplitInstant(NodaTime.Instant instant, out int days, out uint secondOfDay, out uint nanos)
		{
			// the duration since year 1 is never negative here, so Days and NanosecondOfDay are both non-negative
			var sinceYearOne = instant - NodaTime.NodaConstants.BclEpoch;
			Contract.Debug.Requires(sinceYearOne >= NodaTime.Duration.Zero);
			days = sinceYearOne.Days;
			ulong nanosOfDay = (ulong) sinceYearOne.NanosecondOfDay;
			uint sod = (uint) (nanosOfDay / 1_000_000_000);
			secondOfDay = sod;
			nanos = (uint) (nanosOfDay - (sod * 1_000_000_000UL));
		}

		/// <summary>Decides the suffix of a date: <see cref="SuffixUtc"/>, <see cref="SuffixOffset"/> (with the offset in minutes), or <see cref="SuffixNone"/></summary>
		/// <param name="date">Date, used to compute the offset of the local time zone when needed</param>
		/// <param name="kind">Kind that decides the suffix: <c>Z</c> for UTC, an offset for local, nothing for unspecified</param>
		/// <param name="utcOffset">Explicit offset, or <see langword="null"/> to use the offset of the local time zone</param>
		/// <param name="forceLocal">If <see langword="true"/>, an explicit offset of zero is written as <c>+00:00</c> instead of <c>Z</c></param>
		/// <param name="offsetMinutes">Receives the offset in minutes, for <see cref="SuffixOffset"/></param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int ResolveSuffix(DateTime date, DateTimeKind kind, TimeSpan? utcOffset, bool forceLocal, out int offsetMinutes)
		{
			offsetMinutes = 0;
			if (kind == DateTimeKind.Utc)
			{
				return SuffixUtc;
			}

			TimeSpan offset;
			if (utcOffset.HasValue)
			{
				offset = utcOffset.Value;
				// special case: we still output 'Z' for DateTimeOffset with GMT offset, since we cannot distinguish with values set to UTC
				// => we may mix up times set to GMT offset with UTC times, but if the server is set to GMT without any DST, this should not change the actual instant
				if (offset == TimeSpan.Zero && !forceLocal)
				{
					return SuffixUtc;
				}
			}
			else if (kind == DateTimeKind.Local)
			{
				offset = TimeZoneInfo.Local.GetUtcOffset(date);
			}
			else
			{
				return SuffixNone;
			}

			offsetMinutes = (int) (offset.Ticks / TimeSpan.TicksPerMinute);
			return SuffixOffset;
		}

		/// <summary>Number of characters that <see cref="WriteIso8601"/> writes</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int ComputeSize(uint nanos, bool hasTime, int suffix, char quotes)
		{
			// "YYYY-MM-DD" is 10, "THH:mm:ss" is 9, ".fffffff" is 8, "ff" is 2, "Z" is 1, "+HH:MM" is 6
			return (quotes != '\0' ? 2 : 0)
				+ 10
				+ (hasTime ? 9 + (nanos != 0 ? 8 : 0) + (nanos % 100 != 0 ? 2 : 0) : 0)
				+ (suffix == SuffixUtc ? 1 : suffix == SuffixOffset ? 6 : 0);
		}

		/// <summary>Writes an ISO 8601 date at <paramref name="ptr"/></summary>
		/// <param name="ptr">Position of the first character to write; the caller reserves <see cref="ComputeSize"/> characters</param>
		/// <param name="days">Day number since 0001-01-01</param>
		/// <param name="secondOfDay">Second within the day (0 to 86399)</param>
		/// <param name="nanos">Nanoseconds within the second: seven fraction digits when non-zero, nine when the last two digits are non-zero</param>
		/// <param name="hasTime">If <see langword="false"/>, only the date is written</param>
		/// <param name="suffix">One of <see cref="SuffixNone"/>, <see cref="SuffixUtc"/> or <see cref="SuffixOffset"/></param>
		/// <param name="offsetMinutes">Offset in minutes, for <see cref="SuffixOffset"/></param>
		/// <param name="quotes">Character written before and after the date, or the null character for none</param>
		private static void WriteIso8601(ref char ptr, int days, uint secondOfDay, uint nanos, bool hasTime, int suffix, int offsetMinutes, char quotes)
		{
			if (quotes != '\0')
			{
				ptr = quotes;
				ptr = ref Unsafe.Add(ref ptr, 1);
			}

			var day = new DateTime(days * TimeSpan.TicksPerDay);
#if NET8_0_OR_GREATER
			day.Deconstruct(out int year, out int month, out int dayOfMonth);
#else
			int year = day.Year;
			int month = day.Month;
			int dayOfMonth = day.Day;
#endif
			ptr = ref FormatDatePart(ref ptr, (uint) year, (uint) month, (uint) dayOfMonth);

			if (hasTime)
			{
				ptr = 'T';
				ptr = ref Unsafe.Add(ref ptr, 1);
				ptr = ref FormatTimePart(ref ptr, secondOfDay, nanos);
			}

			if (suffix == SuffixUtc)
			{
				ptr = 'Z';
				ptr = ref Unsafe.Add(ref ptr, 1);
			}
			else if (suffix == SuffixOffset)
			{
				ptr = ref FormatTimeZoneOffset(ref ptr, offsetMinutes);
			}

			if (quotes != '\0')
			{
				ptr = quotes;
			}
		}

		/// <summary>Writes the two decimal digits of a value between 0 and 99</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void WriteTwoDigits(ref char ptr, uint value)
		{
			Contract.Debug.Requires(value < 100);
			ref byte digits = ref Unsafe.Add(ref MemoryMarshal.GetReference(TwoDigits), (nint) (value * 2));
			ptr = (char) digits;
			Unsafe.Add(ref ptr, 1) = (char) Unsafe.Add(ref digits, 1);
		}

		private static ref char FormatDatePart(ref char ptr, uint year, uint month, uint day)
		{
			Paranoid.Requires(year <= 9999 && month is >= 1 and <= 12 && day is >= 1 and <= 31);

			// "YYYY-MM-DD"
			uint century = year / 100;
			WriteTwoDigits(ref ptr, century);
			WriteTwoDigits(ref Unsafe.Add(ref ptr, 2), year - (century * 100));
			Unsafe.Add(ref ptr, 4) = '-';
			WriteTwoDigits(ref Unsafe.Add(ref ptr, 5), month);
			Unsafe.Add(ref ptr, 7) = '-';
			WriteTwoDigits(ref Unsafe.Add(ref ptr, 8), day);

			return ref Unsafe.Add(ref ptr, 10);
		}

		private static ref char FormatTimePart(ref char ptr, uint secondOfDay, uint nanos)
		{
			Paranoid.Requires(secondOfDay < 86400 && nanos < 1_000_000_000);

			// x / 60 and x / 3600 as fixed-point multiplications, exact for any second of a day.
			// The minute and the second come from the identity x mod 60 == (x + 4 * (x / 60)) mod 64,
			// which turns each remainder into a shift, an add, and a mask.
			uint minuteOfDay = (uint) (((ulong) secondOfDay * 71_582_789) >> 32);
			uint hour = (uint) (((ulong) secondOfDay * 1_193_047) >> 32);
			uint second = (secondOfDay + (minuteOfDay << 2)) & 63;
			uint minute = (minuteOfDay + (hour << 2)) & 63;

			// "HH:mm:ss"
			WriteTwoDigits(ref ptr, hour);
			Unsafe.Add(ref ptr, 2) = ':';
			WriteTwoDigits(ref Unsafe.Add(ref ptr, 3), minute);
			Unsafe.Add(ref ptr, 5) = ':';
			WriteTwoDigits(ref Unsafe.Add(ref ptr, 6), second);
			ptr = ref Unsafe.Add(ref ptr, 8);

			if (nanos != 0)
			{ // ".fffffff" (ticks), plus "ff" when there are nanoseconds below the tick

				uint ticks = nanos / 100;
				uint subTick = nanos - (ticks * 100);

				// split the seven tick digits into three pairs and a unit: "ab" "cd" "ef" "g"
				uint high = ticks / 1000;            // "abcd"
				uint low = ticks - (high * 1000);    // "efg"
				uint ab = high / 100;
				uint cd = high - (ab * 100);
				uint ef = low / 10;
				uint g = low - (ef * 10);

				ptr = '.';
				WriteTwoDigits(ref Unsafe.Add(ref ptr, 1), ab);
				WriteTwoDigits(ref Unsafe.Add(ref ptr, 3), cd);
				WriteTwoDigits(ref Unsafe.Add(ref ptr, 5), ef);
				Unsafe.Add(ref ptr, 7) = (char) ('0' + g);
				ptr = ref Unsafe.Add(ref ptr, 8);

				if (subTick != 0)
				{
					WriteTwoDigits(ref ptr, subTick);
					ptr = ref Unsafe.Add(ref ptr, 2);
				}
			}

			return ref ptr;
		}

		/// <summary>Writes a time zone offset as <c>+HH:MM</c> or <c>-HH:MM</c></summary>
		private static ref char FormatTimeZoneOffset(ref char ptr, int offsetMinutes)
		{
			Unsafe.Add(ref ptr, 0) = offsetMinutes >= 0 ? '+' : '-';

			uint total = (uint) Math.Abs(offsetMinutes);
			uint hours = total / 60;
			WriteTwoDigits(ref Unsafe.Add(ref ptr, 1), hours);
			Unsafe.Add(ref ptr, 3) = ':';
			WriteTwoDigits(ref Unsafe.Add(ref ptr, 4), total - (hours * 60));
			return ref Unsafe.Add(ref ptr, 6);
		}

		public static void GetDateParts(long ticks, out int year, out int month, out int day, out int hour, out int minute, out int second, out int remainder)
		{
			var date = new DateTime(ticks);
#if NET8_0_OR_GREATER
			date.Deconstruct(out year, out month, out day);
#else
			year = date.Year;
			month = date.Month;
			day = date.Day;
#endif
			SplitTicks(ticks, out _, out uint secondOfDay, out uint nanos);
			uint minuteOfDay = (uint) (((ulong) secondOfDay * 71_582_789) >> 32);
			uint h = (uint) (((ulong) secondOfDay * 1_193_047) >> 32);
			hour = (int) h;
			minute = (int) ((minuteOfDay + (h << 2)) & 63);
			second = (int) ((secondOfDay + (minuteOfDay << 2)) & 63);
			remainder = (int) (nanos / 100);
		}

	}

}
