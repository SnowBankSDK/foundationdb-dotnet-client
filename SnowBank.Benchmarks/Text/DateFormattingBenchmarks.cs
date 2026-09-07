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


namespace SnowBank.Benchmarks
{
	using System.Globalization;
	using System.IO;
	using BenchmarkDotNet.Attributes;
	using NodaTime;
	using SnowBank.Data.Json;

	/// <summary>Measures the ISO 8601 date formatting of the CrystalJson writer, per value, for the types that reach
	/// <see cref="CrystalJsonWriter.WriteValue(DateTime)"/> and its siblings. Each invocation writes a batch of values into a
	/// writer backed by <see cref="TextWriter.Null"/>, so the cost of the writer itself is amortized. The BCL "O" format is the
	/// reference point.</summary>
	[MemoryDiagnoser]
	public class DateFormattingBenchmarks
	{

		private const int BatchSize = 1000;

		private static readonly DateTime UtcWithFraction = new(2025, 6, 16, 17, 46, 17, 934, DateTimeKind.Utc);
		private static readonly DateTime UtcWholeSecond = new(2025, 6, 16, 17, 46, 17, DateTimeKind.Utc);
		private static readonly DateTime UnspecifiedWithFraction = new(2025, 6, 16, 17, 46, 17, 934, DateTimeKind.Unspecified);
		private static readonly DateTimeOffset OffsetWithFraction = new(2025, 6, 16, 17, 46, 17, 934, TimeSpan.FromHours(2));
		private static readonly Instant InstantTicks = Instant.FromUtc(2025, 6, 16, 17, 46, 17) + Duration.FromMilliseconds(934);
		private static readonly Instant InstantNanos = Instant.FromUtc(2025, 6, 16, 17, 46, 17) + Duration.FromNanoseconds(934_567_891);
		private static readonly DateOnly DateOnlyValue = new(2025, 6, 16);

		// one writer for the whole run, flushed after each batch: a fresh writer per batch would grow its buffer from 1 KB to 64 KB every time
		private CrystalJsonWriter Writer = null!;

		[GlobalSetup]
		public void Setup() => this.Writer = new CrystalJsonWriter(TextWriter.Null, 0, CrystalJsonSettings.Json, CrystalJson.DefaultResolver);

		[Benchmark(OperationsPerInvoke = BatchSize)]
		public void DateTime_Utc_Fraction()
		{
			var writer = this.Writer;
			for (int i = 0; i < BatchSize; i++) writer.WriteValue(UtcWithFraction);
			writer.Flush();
		}

		[Benchmark(OperationsPerInvoke = BatchSize)]
		public void DateTime_Utc_WholeSecond()
		{
			var writer = this.Writer;
			for (int i = 0; i < BatchSize; i++) writer.WriteValue(UtcWholeSecond);
			writer.Flush();
		}

		[Benchmark(OperationsPerInvoke = BatchSize)]
		public void DateTime_Unspecified_Fraction()
		{
			var writer = this.Writer;
			for (int i = 0; i < BatchSize; i++) writer.WriteValue(UnspecifiedWithFraction);
			writer.Flush();
		}

		[Benchmark(OperationsPerInvoke = BatchSize)]
		public void DateTimeOffset_Fraction()
		{
			var writer = this.Writer;
			for (int i = 0; i < BatchSize; i++) writer.WriteValue(OffsetWithFraction);
			writer.Flush();
		}

		[Benchmark(OperationsPerInvoke = BatchSize)]
		public void Instant_Ticks()
		{
			var writer = this.Writer;
			for (int i = 0; i < BatchSize; i++) writer.WriteValue(InstantTicks);
			writer.Flush();
		}

		[Benchmark(OperationsPerInvoke = BatchSize)]
		public void Instant_Nanos()
		{
			var writer = this.Writer;
			for (int i = 0; i < BatchSize; i++) writer.WriteValue(InstantNanos);
			writer.Flush();
		}

		[Benchmark(OperationsPerInvoke = BatchSize)]
		public void DateOnly_Value()
		{
			var writer = this.Writer;
			for (int i = 0; i < BatchSize; i++) writer.WriteValue(DateOnlyValue);
			writer.Flush();
		}

		[Benchmark]
		public JsonValue JsonString_Return_DateTime_Utc_Fraction() => JsonString.Return(UtcWithFraction);

		[Benchmark]
		public JsonValue JsonString_Return_DateTimeOffset_Fraction() => JsonString.Return(OffsetWithFraction);

		[Benchmark]
		public JsonValue JsonString_Return_Instant_Ticks() => JsonString.Return(InstantTicks);

		[Benchmark]
		public JsonValue JsonString_Return_DateOnly() => JsonString.Return(DateOnlyValue);

		private static readonly JsonValue TextInstantTicks = JsonString.Return("2025-06-16T17:46:17.9340000Z");
		private static readonly JsonValue TextInstantNanos = JsonString.Return("2025-06-16T17:46:17.934567891Z");
		private static readonly JsonValue TextInstantOffset = JsonString.Return("2025-06-16T19:46:17.9340000+02:00");
		private static readonly JsonValue TextDateTimeUtc = JsonString.Return("2025-06-16T17:46:17.9340000Z");
		private static readonly JsonValue TextDateTimeOffset = JsonString.Return("2025-06-16T19:46:17.9340000+02:00");

		[Benchmark]
		public Instant JsonString_ToInstant_Ticks() => TextInstantTicks.ToInstant();

		[Benchmark]
		public Instant JsonString_ToInstant_Nanos() => TextInstantNanos.ToInstant();

		[Benchmark]
		public Instant JsonString_ToInstant_Offset() => TextInstantOffset.ToInstant();

		[Benchmark]
		public DateTime JsonString_ToDateTime_Utc() => TextDateTimeUtc.ToDateTime();

		[Benchmark]
		public DateTimeOffset JsonString_ToDateTimeOffset() => TextDateTimeOffset.ToDateTimeOffset();

		// the previous JsonString factories called these directly (plus the JsonString allocation)
		[Benchmark]
		public string Bcl_DateTime_ToString_O() => UtcWithFraction.ToString("O", CultureInfo.InvariantCulture);

		[Benchmark]
		public string Bcl_DateTimeOffset_ToString_O() => OffsetWithFraction.ToString("O", CultureInfo.InvariantCulture);

		[Benchmark]
		public string Noda_Instant_ExtendedIso_Format() => NodaTime.Text.InstantPattern.ExtendedIso.Format(InstantTicks);

		[Benchmark]
		public string Bcl_DateOnly_ToString_O() => DateOnlyValue.ToString("O", CultureInfo.InvariantCulture);

		[Benchmark(OperationsPerInvoke = BatchSize, Baseline = true)]
		public int Bcl_RoundTripFormat()
		{
			Span<char> buf = stackalloc char[40];
			int total = 0;
			for (int i = 0; i < BatchSize; i++)
			{
				UtcWithFraction.TryFormat(buf, out int n, "O", CultureInfo.InvariantCulture);
				total += n;
			}
			return total;
		}

	}

}
