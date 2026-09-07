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


namespace SnowBank.Data.Json.Tests
{
	using System.Globalization;
	using NodaTime;

	/// <summary>Pins the round-trip guarantee of the ISO 8601 date formatting: a <see cref="DateTime"/>, a
	/// <see cref="DateTimeOffset"/> or an <see cref="Instant"/> serialized then deserialized is the same value, tick for tick
	/// (nanosecond for nanosecond for instants), with or without a fractional part. The BCL custom format strings are the
	/// oracle for the text itself.</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	[SetInvariantCulture]
	public sealed class CrystalJsonDateRoundTripFacts : SimpleTest
	{

		private const int Samples = 20_000;

		private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

		/// <summary>Expected text for a date: seven fraction digits when the fraction is non-zero, none otherwise</summary>
		private static string Expected(DateTime dt, string suffix)
		{
			long frac = dt.Ticks % TimeSpan.TicksPerSecond;
			return "\"" + dt.ToString("yyyy-MM-dd'T'HH:mm:ss", Inv) + (frac != 0 ? "." + frac.ToString("D7", Inv) : "") + suffix + "\"";
		}

		private static string ExpectedOffset(TimeSpan offset)
		{
			int minutes = (int) Math.Abs(offset.Ticks / TimeSpan.TicksPerMinute);
			return (offset < TimeSpan.Zero ? "-" : "+") + (minutes / 60).ToString("D2", Inv) + ":" + (minutes % 60).ToString("D2", Inv);
		}

		/// <summary>Random ticks over the whole DateTime range, biased so that half the values have a whole second</summary>
		private static long RandomTicks(Random rnd)
		{
			long ticks = rnd.NextInt64(1, DateTime.MaxValue.Ticks);
			return (rnd.Next(2) == 0) ? ticks - (ticks % TimeSpan.TicksPerSecond) : ticks;
		}

		[Test]
		public void Test_DateTime_Utc_RoundTrips()
		{
			var rnd = CreateRandomizer();
			for (int i = 0; i < Samples; i++)
			{
				var dt = new DateTime(RandomTicks(rnd), DateTimeKind.Utc);
				string json = CrystalJson.Serialize(dt);
				Assert.That(json, Is.EqualTo(Expected(dt, "Z")), $"ticks={dt.Ticks}");
				var back = CrystalJson.Deserialize<DateTime>(json);
				Assert.That(back.Ticks, Is.EqualTo(dt.Ticks), $"json={json}");
				Assert.That(back.Kind, Is.EqualTo(DateTimeKind.Utc), $"json={json}");
			}
		}

		[Test]
		public void Test_DateTime_Unspecified_RoundTrips()
		{
			var rnd = CreateRandomizer();
			for (int i = 0; i < Samples; i++)
			{
				var dt = new DateTime(RandomTicks(rnd), DateTimeKind.Unspecified);
				string json = CrystalJson.Serialize(dt);
				// an unspecified date at midnight is written as a date only
				string expected = dt.TimeOfDay == TimeSpan.Zero ? "\"" + dt.ToString("yyyy-MM-dd", Inv) + "\"" : Expected(dt, "");
				Assert.That(json, Is.EqualTo(expected), $"ticks={dt.Ticks}");
				var back = CrystalJson.Deserialize<DateTime>(json);
				Assert.That(back.Ticks, Is.EqualTo(dt.Ticks), $"json={json}");
				Assert.That(back.Kind, Is.EqualTo(DateTimeKind.Unspecified), $"json={json}");
			}
		}

		[Test]
		public void Test_DateTimeOffset_RoundTrips()
		{
			var rnd = CreateRandomizer();
			for (int i = 0; i < Samples; i++)
			{
				// offsets are whole minutes within +/- 14 hours, and the UTC instant must stay in range
				var offset = TimeSpan.FromMinutes(rnd.Next(-14 * 60, 14 * 60 + 1));
				long ticks = Math.Clamp(RandomTicks(rnd), TimeSpan.TicksPerDay, DateTime.MaxValue.Ticks - TimeSpan.TicksPerDay);
				var dto = new DateTimeOffset(ticks, offset);
				string json = CrystalJson.Serialize(dto);
				Assert.That(json, Is.EqualTo(Expected(dto.DateTime, ExpectedOffset(offset))), $"ticks={ticks} offset={offset}");
				var back = CrystalJson.Deserialize<DateTimeOffset>(json);
				Assert.That(back.Ticks, Is.EqualTo(dto.Ticks), $"json={json}");
				Assert.That(back.Offset, Is.EqualTo(dto.Offset), $"json={json}");
			}
		}

		[Test]
		public void Test_Instant_RoundTrips_With_Nanoseconds()
		{
			var rnd = CreateRandomizer();
			for (int i = 0; i < Samples; i++)
			{
				// unix nanoseconds over 1970..2262, one third whole seconds, one third tick precision, one third full nanoseconds
				long nanos = rnd.NextInt64(0, long.MaxValue);
				switch (i % 3)
				{
					case 0: nanos -= nanos % 1_000_000_000; break;
					case 1: nanos -= nanos % 100; break;
				}
				var instant = Instant.FromUnixTimeSeconds(0).PlusNanoseconds(nanos);

				string json = CrystalJson.Serialize(instant);
				var back = CrystalJson.Deserialize<Instant>(json);
				Assert.That(back, Is.EqualTo(instant), $"text route: json={json} nanos={nanos}");

				var dom = JsonValue.FromValue(instant);
				Assert.That(dom.ToInstant(), Is.EqualTo(instant), $"dom route: json={dom.ToJsonText()} nanos={nanos}");
			}
		}

		[Test]
		public void Test_Instant_Text_Matches_Bcl_When_Tick_Precision()
		{
			// an instant with tick precision must keep the same text as the equivalent UTC DateTime
			var rnd = CreateRandomizer();
			for (int i = 0; i < Samples; i++)
			{
				var dt = new DateTime(RandomTicks(rnd), DateTimeKind.Utc);
				var instant = Instant.FromDateTimeUtc(dt);
				Assert.That(CrystalJson.Serialize(instant), Is.EqualTo(Expected(dt, "Z")), $"ticks={dt.Ticks}");
			}
		}

		[Test]
		public void Test_Every_Second_Of_A_Day_Formats_Like_Bcl()
		{
			var start = new DateTime(2024, 2, 29, 0, 0, 0, DateTimeKind.Utc); // a leap day, in a leap year
			for (int s = 0; s < 86400; s++)
			{
				var dt = start.AddSeconds(s);
				Assert.That(CrystalJson.Serialize(dt), Is.EqualTo(Expected(dt, "Z")), $"second={s}");
			}
		}

		[Test]
		public void Test_Every_Day_Of_The_Range_Formats_Like_Bcl()
		{
			// one value per day from year 1 to year 9999, with a time part that exercises every digit
			var dt = new DateTime(1, 1, 1, 23, 58, 57, 987, DateTimeKind.Utc).AddTicks(6543);
			while (dt.Year < 10000)
			{
				Assert.That(CrystalJson.Serialize(dt), Is.EqualTo(Expected(dt, "Z")), $"ticks={dt.Ticks}");
				if (dt.Year == 9999 && dt.Month == 12 && dt.Day == 31) break;
				dt = dt.AddDays(1);
			}
		}

		[Test]
		public void Test_Dom_Route_Has_The_Same_Text_As_The_Writer()
		{
			// JsonString.Return(...) and the writer must spell a value the same way, so that two documents built either way compare equal
			var rnd = CreateRandomizer();
			for (int i = 0; i < Samples; i++)
			{
				var utc = new DateTime(RandomTicks(rnd), DateTimeKind.Utc);
				Assert.That(JsonValue.FromValue(utc).ToJsonText(), Is.EqualTo(CrystalJson.Serialize(utc)), $"ticks={utc.Ticks}");
				Assert.That(JsonValue.FromValue(utc).ToDateTime(), Is.EqualTo(utc));

				var unspecified = new DateTime(RandomTicks(rnd), DateTimeKind.Unspecified);
				Assert.That(JsonValue.FromValue(unspecified).ToJsonText(), Is.EqualTo(CrystalJson.Serialize(unspecified)), $"ticks={unspecified.Ticks}");
				Assert.That(JsonValue.FromValue(unspecified).ToDateTime(), Is.EqualTo(unspecified));

				var offset = TimeSpan.FromMinutes(rnd.Next(-14 * 60, 14 * 60 + 1));
				var dto = new DateTimeOffset(Math.Clamp(RandomTicks(rnd), TimeSpan.TicksPerDay, DateTime.MaxValue.Ticks - TimeSpan.TicksPerDay), offset);
				Assert.That(JsonValue.FromValue(dto).ToJsonText(), Is.EqualTo(CrystalJson.Serialize(dto)), $"ticks={dto.Ticks} offset={offset}");
				Assert.That(JsonValue.FromValue(dto).ToDateTimeOffset(), Is.EqualTo(dto));

				var instant = Instant.FromUnixTimeSeconds(0).PlusNanoseconds(rnd.NextInt64(1, long.MaxValue));
				Assert.That(JsonValue.FromValue(instant).ToJsonText(), Is.EqualTo(CrystalJson.Serialize(instant)), $"instant={instant}");
			}

			// the sentinels spell the same way too
			using (Assert.EnterMultipleScope())
			{
				Assert.That(JsonValue.FromValue(DateTime.MaxValue).ToJsonText(), Is.EqualTo(CrystalJson.Serialize(DateTime.MaxValue)));
				Assert.That(JsonValue.FromValue(DateTimeOffset.MaxValue).ToJsonText(), Is.EqualTo(CrystalJson.Serialize(DateTimeOffset.MaxValue)));
				Assert.That(JsonValue.FromValue(DateOnly.MaxValue).ToJsonText(), Is.EqualTo(CrystalJson.Serialize(DateOnly.MaxValue)));
				Assert.That(JsonValue.FromValue(Instant.MaxValue).ToJsonText(), Is.EqualTo(CrystalJson.Serialize(Instant.MaxValue)));
				Assert.That(JsonValue.FromValue(DateTime.MinValue).ToJsonText(), Is.EqualTo(CrystalJson.Serialize(DateTime.MinValue)));
				Assert.That(JsonValue.FromValue(DateTimeOffset.MinValue).ToJsonText(), Is.EqualTo(CrystalJson.Serialize(DateTimeOffset.MinValue)));
				// the default instant (the Unix epoch) is the empty string on both routes, and NodaTime's MinValue is a regular date that round-trips
				Assert.That(CrystalJson.Serialize(default(Instant)), Is.EqualTo("\"\""));
				Assert.That(JsonValue.FromValue(default(Instant)).ToJsonText(), Is.EqualTo("\"\""));
				Assert.That(CrystalJson.Deserialize<Instant>("\"\""), Is.EqualTo(default(Instant)));
				Assert.That(JsonValue.FromValue(Instant.MinValue).ToJsonText(), Is.EqualTo(CrystalJson.Serialize(Instant.MinValue)));
				Assert.That(CrystalJson.Deserialize<Instant>(CrystalJson.Serialize(Instant.MinValue)), Is.EqualTo(Instant.MinValue));
				Assert.That(JsonValue.FromValue(Instant.MinValue).ToInstant(), Is.EqualTo(Instant.MinValue));
			}
		}

		[Test]
		public void Test_Instant_Literals_Parse_On_Every_Route()
		{
			// the fast path handles "Z" and "+HH:MM"; negative years stay on the NodaTime pattern; no suffix goes through DateTimeOffset
			var expected = Instant.FromUtc(2025, 6, 16, 17, 46, 17) + Duration.FromNanoseconds(934_567_891);
			using (Assert.EnterMultipleScope())
			{
				Assert.That(JsonString.Return("2025-06-16T17:46:17.934567891Z").ToInstant(), Is.EqualTo(expected));
				Assert.That(JsonString.Return("2025-06-16T19:46:17.934567891+02:00").ToInstant(), Is.EqualTo(expected));
				Assert.That(JsonString.Return("2025-06-16T09:16:17.934567891-08:30").ToInstant(), Is.EqualTo(expected));
				Assert.That(JsonString.Return("2025-06-16T17:46:17Z").ToInstant(), Is.EqualTo(Instant.FromUtc(2025, 6, 16, 17, 46, 17)));
				Assert.That(JsonString.Return("2025-06-16T17:46:17.934Z").ToInstant(), Is.EqualTo(Instant.FromUtc(2025, 6, 16, 17, 46, 17) + Duration.FromMilliseconds(934)));
				Assert.That(JsonString.Return("0001-01-01T00:00:00Z").ToInstant(), Is.EqualTo(NodaConstants.BclEpoch));
				Assert.That(JsonString.Return("9999-12-31T23:59:59.999999999Z").ToInstant(), Is.EqualTo(Instant.MaxValue));
				Assert.That(JsonString.Return("-0052-08-27T12:12:00Z").ToInstant(), Is.EqualTo(Instant.FromUtc(-52, 8, 27, 12, 12)));
				Assert.That(JsonString.Return("2025-06-16T17:46:17.934").ToInstant(), Is.EqualTo(Instant.FromDateTimeOffset(new DateTimeOffset(new DateTime(2025, 6, 16, 17, 46, 17, 934, DateTimeKind.Unspecified)))));
				Assert.That(((JsonString) JsonString.Return("2025-06-16T17:46:17.934567891Z")).TryConvertToInstant(out var viaTry), Is.True);
				Assert.That(viaTry, Is.EqualTo(expected));
				Assert.That(((JsonString) JsonString.Return("not a date")).TryConvertToInstant(out _), Is.False);
			}
		}

		[Test]
		public void Test_DateTime_Literals_Parse_Like_The_Bcl()
		{
			// the span parser must agree with DateTime.TryParse(RoundtripKind) on ticks and kind, offsets converted to local time
			foreach (var literal in new[] { "2025-06-16T17:46:17.9340000Z", "2025-06-16T17:46:17Z", "2025-06-16T17:46:17.934", "2025-06-16", "2025-06-16T19:46:17.9340000+02:00", "2025-06-16T09:16:17.934-08:30", "2025-10-26T02:30:00+01:00", "9999-12-31T23:59:59.9999999Z", "0001-01-01T00:00:00Z" })
			{
				Assert.That(CrystalJsonParser.TryParseIso8601DateTime(literal, out var parsed), Is.True, literal);
				var expected = DateTime.Parse(literal, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
				Assert.That(parsed.Ticks, Is.EqualTo(expected.Ticks), literal);
				Assert.That(parsed.Kind, Is.EqualTo(expected.Kind), literal);
			}

			// nine fraction digits (an instant with nanoseconds) convert to a DateTime by truncation to the tick
			Assert.That(JsonString.Return("2025-06-16T17:46:17.934567891Z").ToDateTime(), Is.EqualTo(new DateTime(2025, 6, 16, 17, 46, 17, DateTimeKind.Utc).AddTicks(9345678)));

			// a sign or a space inside a field is not a digit
			using (Assert.EnterMultipleScope())
			{
				Assert.That(CrystalJsonParser.TryParseDateTimeOffsetComponents("2025-06-16T17:46:+7Z", out _, out _, out _, out _, out _), Is.False);
				Assert.That(CrystalJsonParser.TryParseDateTimeOffsetComponents("2025-06-16T17:46: 7Z", out _, out _, out _, out _, out _), Is.False);
				Assert.That(CrystalJsonParser.TryParseDateTimeOffsetComponents("2025-06-16T17:46:17+1:00", out _, out _, out _, out _, out _), Is.False);
				Assert.That(CrystalJsonParser.TryParseDateTimeOffsetComponents("2025-06-16T17:46:17.Z", out _, out _, out _, out _, out _), Is.False);
			}
		}

		[Test]
		public void Test_Leap_Second_Literal_Is_Rejected_Without_Throwing()
		{
			// neither DateTime ticks nor Instant can represent 23:59:60, so the parser must reject it, not throw
			using (Assert.EnterMultipleScope())
			{
				Assert.That(CrystalJsonParser.TryParseIso8601DateTimeOffset("2016-12-31T23:59:60Z", out _), Is.False);
				Assert.That(CrystalJsonParser.TryParseIso8601DateTimeOffset("2016-12-31T23:59:60+00:00", out _), Is.False);
				Assert.That(CrystalJsonParser.TryParseDateTimeOffsetComponents("2016-12-31T23:59:60Z", out _, out _, out _, out _, out _), Is.False);
				Assert.That(CrystalJsonParser.TryParseIso8601DateTime("2016-12-31T23:59:60Z", out _), Is.False);
				Assert.That(((JsonString) JsonValue.Parse("\"2016-12-31T23:59:60Z\"")).TryConvertToDateTimeOffset(out _), Is.False);
			}
		}

	}

}
