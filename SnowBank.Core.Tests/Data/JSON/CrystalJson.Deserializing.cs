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

// ReSharper disable StringLiteralTypo

#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
#pragma warning disable JSON001 // JSON issue: 'x' unexpected

namespace SnowBank.Data.Json.Tests
{
	using System.Collections;
	using System.Net;
	using SnowBank.Data.Tuples;
	using SnowBank.Runtime.Converters;
	using SnowBank.Text;

	public partial class CrystalJsonTest
	{

		[Test]
		public void Test_JsonDeserialize_Null()
		{
			Assert.That(CrystalJson.Deserialize<string?>("null", null), Is.Null);
			Assert.That(CrystalJson.Deserialize<string?>("", null), Is.Null);
			Assert.That(CrystalJson.Deserialize<string?>("  ", null), Is.Null);

			Assert.That(CrystalJson.Deserialize<int?>("null", null), Is.Null);
			Assert.That(CrystalJson.Deserialize<int?>("", null), Is.Null);
			Assert.That(CrystalJson.Deserialize<int?>("  ", null), Is.Null);

			Assert.That(CrystalJson.Deserialize<string>("null", "not_found"), Is.EqualTo("not_found"));
			Assert.That(CrystalJson.Deserialize<string>("", "not_found"), Is.EqualTo("not_found"));
			Assert.That(CrystalJson.Deserialize<string>("  ", "not_found"), Is.EqualTo("not_found"));

			Assert.That(CrystalJson.Deserialize<int>("null", 123), Is.EqualTo(123));
			Assert.That(CrystalJson.Deserialize<int>("", 123), Is.EqualTo(123));
			Assert.That(CrystalJson.Deserialize<int>("  ", 123), Is.EqualTo(123));

			Assert.That(() => CrystalJson.Deserialize<string>("null"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => CrystalJson.Deserialize<string>(""), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => CrystalJson.Deserialize<int?>("null"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => CrystalJson.Deserialize<int?>(""), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => CrystalJson.Deserialize<int>("null"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => CrystalJson.Deserialize<int>(""), Throws.InstanceOf<JsonBindingException>());

		}

		[Test]
		public void Test_JsonDeserialize_Boolean()
		{
			// direct

			Assert.That(CrystalJson.Deserialize<bool>("true"), Is.True);
			Assert.That(CrystalJson.Deserialize<bool>("false"), Is.False);

			// implicit convert
			Assert.That(CrystalJson.Deserialize<bool>("0"), Is.False);
			Assert.That(CrystalJson.Deserialize<bool>("1"), Is.True);
			Assert.That(CrystalJson.Deserialize<bool>("123"), Is.True);
			Assert.That(CrystalJson.Deserialize<bool>("-1"), Is.True);

			Assert.That(CrystalJson.Deserialize<bool>("\"true\""), Is.True);
			Assert.That(CrystalJson.Deserialize<bool>("\"false\""), Is.False);

			// must reject other types
			Assert.That(() => CrystalJson.Deserialize<bool>("null"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => CrystalJson.Deserialize<bool>("\"foo\""), Throws.InstanceOf<FormatException>());
			Assert.That(() => CrystalJson.Deserialize<bool>("{ }"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => CrystalJson.Deserialize<bool>("[ ]"), Throws.InstanceOf<JsonBindingException>());
		}

		[Test]
		public void Test_JsonDeserialize_String()
		{
			// directed deserialization

			Assert.That(() => CrystalJson.Deserialize<string>("null"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(CrystalJson.Deserialize<string?>("null", null), Is.Null);
			Assert.That(CrystalJson.Deserialize<string?>(@"""Hello World""", null), Is.EqualTo("Hello World"));

			// with implicit conversion
			Assert.That(CrystalJson.Deserialize<string>("123"), Is.EqualTo("123"));
			Assert.That(CrystalJson.Deserialize<string>("1.23"), Is.EqualTo("1.23"));
			Assert.That(CrystalJson.Deserialize<string>("true"), Is.EqualTo("true"));

			// must reject other type
			Assert.That(() => CrystalJson.Deserialize<string>("{ }"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => CrystalJson.Deserialize<string>("[ ]"), Throws.InstanceOf<JsonBindingException>());

			// an array of strings is NOT a string!
			Assert.That(() => CrystalJson.Deserialize<string>("[ \"foo\" ]"), Throws.InstanceOf<JsonBindingException>());
		}

		[Test]
		public void Test_JsonDeserialize_Number()
		{
			// integers
			Assert.That(CrystalJson.Deserialize<int>("0"), Is.EqualTo(0));
			Assert.That(CrystalJson.Deserialize<int>("1"), Is.EqualTo(1));
			Assert.That(CrystalJson.Deserialize<int>("123"), Is.EqualTo(123));
			Assert.That(CrystalJson.Deserialize<int>("-1"), Is.EqualTo(-1));
			Assert.That(CrystalJson.Deserialize<int>("-123"), Is.EqualTo(-123));
			Assert.That(CrystalJson.Deserialize<int>("1E1"), Is.EqualTo(10));
			Assert.That(CrystalJson.Deserialize<int>("1E2"), Is.EqualTo(100));
			Assert.That(CrystalJson.Deserialize<int>("1.23E2"), Is.EqualTo(123));

			// double
			Assert.That(CrystalJson.Deserialize<double>("0"), Is.EqualTo(0));
			Assert.That(CrystalJson.Deserialize<double>("1"), Is.EqualTo(1));
			Assert.That(CrystalJson.Deserialize<double>("123"), Is.EqualTo(123));
			Assert.That(CrystalJson.Deserialize<double>("-1"), Is.EqualTo(-1));
			Assert.That(CrystalJson.Deserialize<double>("-123"), Is.EqualTo(-123));
			Assert.That(CrystalJson.Deserialize<double>("0.1"), Is.EqualTo(0.1));
			Assert.That(CrystalJson.Deserialize<double>("1.23"), Is.EqualTo(1.23));
			Assert.That(CrystalJson.Deserialize<double>("-0.1"), Is.EqualTo(-0.1));
			Assert.That(CrystalJson.Deserialize<double>("-1.23"), Is.EqualTo(-1.23));
			Assert.That(CrystalJson.Deserialize<double>("1E1"), Is.EqualTo(10));
			Assert.That(CrystalJson.Deserialize<double>("1E2"), Is.EqualTo(100));
			Assert.That(CrystalJson.Deserialize<double>("1.23E2"), Is.EqualTo(123));
			Assert.That(CrystalJson.Deserialize<double>("1E1"), Is.EqualTo(10));
			Assert.That(CrystalJson.Deserialize<double>("1E-1"), Is.EqualTo(0.1));
			Assert.That(CrystalJson.Deserialize<double>("1E-2"), Is.EqualTo(0.01));

			// special
			Assert.That(CrystalJson.Deserialize<int>("2147483647"), Is.EqualTo(int.MaxValue));
			Assert.That(CrystalJson.Deserialize<int>("-2147483648"), Is.EqualTo(int.MinValue));
			Assert.That(CrystalJson.Deserialize<long>("9223372036854775807"), Is.EqualTo(long.MaxValue));
			Assert.That(CrystalJson.Deserialize<long>("-9223372036854775808"), Is.EqualTo(long.MinValue));
			Assert.That(CrystalJson.Deserialize<float>("NaN"), Is.EqualTo(float.NaN));
			Assert.That(CrystalJson.Deserialize<float>("Infinity"), Is.EqualTo(float.PositiveInfinity));
			Assert.That(CrystalJson.Deserialize<float>("-Infinity"), Is.EqualTo(float.NegativeInfinity));
			Assert.That(CrystalJson.Deserialize<double>("NaN"), Is.EqualTo(double.NaN));
			Assert.That(CrystalJson.Deserialize<double>("Infinity"), Is.EqualTo(double.PositiveInfinity));
			Assert.That(CrystalJson.Deserialize<double>("-Infinity"), Is.EqualTo(double.NegativeInfinity));

			// decimal
			Assert.That(CrystalJson.Deserialize<decimal>("0"), Is.EqualTo(0m));
			Assert.That(CrystalJson.Deserialize<decimal>("1"), Is.EqualTo(1m));
			Assert.That(CrystalJson.Deserialize<decimal>("123"), Is.EqualTo(123m));
			Assert.That(CrystalJson.Deserialize<decimal>("1.23"), Is.EqualTo(1.23m));

			// implicit string conversion
			Assert.That(CrystalJson.Deserialize<decimal>("\"123\""), Is.EqualTo(123));
			Assert.That(CrystalJson.Deserialize<int>("\"123\""), Is.EqualTo(123));
			Assert.That(CrystalJson.Deserialize<long>("\"123\""), Is.EqualTo(123L));
			Assert.That(CrystalJson.Deserialize<float>("\"1.23\""), Is.EqualTo(1.23f));
			Assert.That(CrystalJson.Deserialize<double>("\"1.23\""), Is.EqualTo(1.23d));
			Assert.That(CrystalJson.Deserialize<float>("\"NaN\""), Is.EqualTo(float.NaN));
			Assert.That(CrystalJson.Deserialize<double>("\"NaN\""), Is.EqualTo(double.NaN));
			Assert.That(CrystalJson.Deserialize<float>("\"Infinity\""), Is.EqualTo(float.PositiveInfinity));
			Assert.That(CrystalJson.Deserialize<double>("\"Infinity\""), Is.EqualTo(double.PositiveInfinity));
			Assert.That(CrystalJson.Deserialize<float>("\"-Infinity\""), Is.EqualTo(float.NegativeInfinity));
			Assert.That(CrystalJson.Deserialize<double>("\"-Infinity\""), Is.EqualTo(double.NegativeInfinity));

			// must reject other types
			Assert.That(() => CrystalJson.Deserialize<int>("{ }"), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => CrystalJson.Deserialize<int>("[ ]"), Throws.InstanceOf<JsonBindingException>());

		}

		[Test]
		public void Test_JsonDeserialize_DateTime()
		{
			// Unix Epoch (1970-1-1 UTC)
			Assert.That(CrystalJson.Deserialize<DateTime>("\"\\/Date(0)\\/\""), Is.EqualTo(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

			// Min/Max Value
			Assert.That(CrystalJson.Deserialize<DateTime>("\"\""), Is.EqualTo(DateTime.MinValue), "DateTime.MinValue");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"0001-01-01T00:00:00.0000000\""), Is.EqualTo(DateTime.MinValue), "DateTime.MinValue");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"0001-01-01T00:00:00\""), Is.EqualTo(DateTime.MinValue), "DateTime.MinValue");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"\\/Date(-62135596800000)\\/\""), Is.EqualTo(DateTime.MinValue), "DateTime.MinValue");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"0001-01-01T00:00:00.0000000Z\""), Is.EqualTo(DateTime.MinValue), "DateTime.MinValue");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"0001-01-01T00:00:00Z\""), Is.EqualTo(DateTime.MinValue), "DateTime.MinValue");

			Assert.That(CrystalJson.Deserialize<DateTime>("\"9999-12-31T23:59:59.9999999\""), Is.EqualTo(DateTime.MaxValue), "DateTime.MaxValue");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"9999-12-31T23:59:59.9999999Z\""), Is.EqualTo(DateTime.MaxValue), "DateTime.MaxValue");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"\\/Date(253402300799999)\\/\""), Is.EqualTo(DateTime.MaxValue), "DateTime.MaxValue (auto-adjusted)"); // note: should automatically add the missing .99999 ms

			// 2000-01-01 (winter time)
			Assert.That(CrystalJson.Deserialize<DateTime>("\"2000-01-01T00:00:00.0000000Z\""), Is.EqualTo(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)), "2000-01-01 UTC");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"2000-01-01T00:00:00Z\""), Is.EqualTo(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)), "2000-01-01 UTC");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"\\/Date(946684800000)\\/\""), Is.EqualTo(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)), "2000-01-01 UTC");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"2000-01-01T00:00:00.0000000\""), Is.EqualTo(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local)), "2000-01-01 GMT+1 (Paris)");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"2000-01-01T00:00:00\""), Is.EqualTo(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local)), "2000-01-01 GMT+1 (Paris)");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"2000-01-01T00:00:00.0000000+01:00\""), Is.EqualTo(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local)), "2000-01-01 GMT+1 (Paris)");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"2000-01-01T00:00:00+01:00\""), Is.EqualTo(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local)), "2000-01-01 GMT+1 (Paris)");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"\\/Date(946681200000+0100)\\/\""), Is.EqualTo(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local)), "2000-01-01 GMT+1 (Paris)");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"2000-01-01T00:00:00.0000000-01:00\""), Is.EqualTo(new DateTime(2000, 1, 1, 2, 0, 0, DateTimeKind.Local)), "2000-01-01 GMT-1");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"2000-01-01T00:00:00-01:00\""), Is.EqualTo(new DateTime(2000, 1, 1, 2, 0, 0, DateTimeKind.Local)), "2000-01-01 GMT-1");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"\\/Date(946681200000-0100)\\/\""), Is.EqualTo(new DateTime(2000, 1, 1, 2, 0, 0, DateTimeKind.Local)), "2000-01-01 GMT-1");

			// 2000-09-01 (summertime)
			Assert.That(CrystalJson.Deserialize<DateTime>("\"\\/Date(967766400000)\\/\""), Is.EqualTo(new DateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Utc)), "2000-09-01 UTC");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"\\/Date(967759200000+0200)\\/\""), Is.EqualTo(new DateTime(2000, 9, 1, 0, 0, 0, DateTimeKind.Local)), "2000-09-01 GMT+2 (Paris, DST)");

			// RoundTrip !
			DateTime utcNow = DateTime.UtcNow;
			Assert.That(utcNow.Kind, Is.EqualTo(DateTimeKind.Utc));
			// /!\ JsonDateTime has a resolution to the millisecond, but UtcNow has a resolution up to the 'tick', which mean we have to truncate the value to milliseconds or else it will not roundtrip properly
			var utcRoundTrip = CrystalJson.Deserialize<DateTime>(CrystalJson.Serialize(utcNow));
			Assert.That(utcRoundTrip, Is.EqualTo(utcNow), "RoundTrip DateTime.UtcNow");

			DateTime localNow = DateTime.Now;
			Assert.That(localNow.Kind, Is.EqualTo(DateTimeKind.Local));
			var localRoundTrip = CrystalJson.Deserialize<DateTime>(CrystalJson.Serialize(localNow));
			Assert.That(localRoundTrip, Is.EqualTo(localNow), "RoundTrip DateTime.Now");

			// direct deserialization
			Assert.That(CrystalJson.Deserialize<DateTime>("\"\\/Date(-62135596800000)\\/\""), Is.EqualTo(DateTime.MinValue), "DateTime.MinValue");
			Assert.That(CrystalJson.Deserialize<DateTime>("\"\\/Date(946681200000+0100)\\/\""), Is.EqualTo(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local)), "2000-01-01 GMT+1 (Paris)");
			// YYYYMMDD
			Assert.That(CrystalJson.Deserialize<DateTime>("\"20000101\""), Is.EqualTo(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local)), "2000-01-01 GMT+1 (Paris)");
			// YYYYMMDDHHMMSS
			Assert.That(CrystalJson.Deserialize<DateTime>("\"20000101123456\""), Is.EqualTo(new DateTime(2000, 1, 1, 12, 34, 56, DateTimeKind.Local)), "2000-01-01 12:34:56 GMT+1 (Paris)");
			// ISO 8601
			Assert.That(CrystalJson.Deserialize<DateTime>("\"2000-01-01T12:34:56Z\""), Is.EqualTo(new DateTime(2000, 1, 1, 12, 34, 56, DateTimeKind.Utc)), "2000-01-01 12:34:56 GMT");
			// ISO 8601 with time offset
			Assert.That(CrystalJson.Deserialize<DateTime>("\"2000-01-01T12:34:56+01:00\""), Is.EqualTo(new DateTime(2000, 1, 1, 12, 34, 56)), "2000-01-01 12:34:56 (CET)");
		}

		[Test]
		public void Test_JsonDeserialize_DateTimeOffset()
		{
			// Unix Epoch (1970-1-1 UTC)
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"\\/Date(0)\\/\""), Is.EqualTo(new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)));

			// Min/Max Value
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"\""), Is.EqualTo(DateTimeOffset.MinValue));
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"0001-01-01T00:00:00.0000000\""), Is.EqualTo(DateTimeOffset.MinValue));
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"0001-01-01T00:00:00\""), Is.EqualTo(DateTimeOffset.MinValue));
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"\\/Date(-62135596800000)\\/\""), Is.EqualTo(DateTimeOffset.MinValue));
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"0001-01-01T00:00:00.0000000Z\""), Is.EqualTo(DateTimeOffset.MinValue));
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"0001-01-01T00:00:00Z\""), Is.EqualTo(DateTimeOffset.MinValue));

			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"9999-12-31T23:59:59.9999999\""), Is.EqualTo(DateTimeOffset.MaxValue));
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"9999-12-31T23:59:59.9999999Z\""), Is.EqualTo(DateTimeOffset.MaxValue));
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"\\/Date(253402300799999)\\/\""), Is.EqualTo(DateTimeOffset.MaxValue)); // note: should automatically add the missing .99999 ms

			// 2000-01-01 (winter time)
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"2000-01-01T00:00:00.0000000Z\""), Is.EqualTo(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero)), "2000-01-01 UTC");
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"2000-01-01T00:00:00Z\""), Is.EqualTo(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero)), "2000-01-01 UTC");
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"\\/Date(946684800000)\\/\""), Is.EqualTo(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero)), "2000-01-01 UTC");
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"2000-01-01T00:00:00.0000000\""), Is.EqualTo(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(1))), "2000-01-01 GMT+1 (Paris)");
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"2000-01-01T00:00:00\""), Is.EqualTo(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(1))), "2000-01-01 GMT+1 (Paris)");
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"2000-01-01T00:00:00.0000000+01:00\""), Is.EqualTo(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(1))), "2000-01-01 GMT+1 (Paris)");
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"2000-01-01T00:00:00+01:00\""), Is.EqualTo(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(1))), "2000-01-01 GMT+1 (Paris)");
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"\\/Date(946681200000+0100)\\/\""), Is.EqualTo(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(1))), "2000-01-01 GMT+1 (Paris)");
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"2000-01-01T00:00:00.0000000-01:00\""), Is.EqualTo(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(-1))), "2000-01-01 GMT-1");
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"2000-01-01T00:00:00-01:00\""), Is.EqualTo(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(-1))), "2000-01-01 GMT-1");
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"\\/Date(946681200000-0100)\\/\""), Is.EqualTo(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(-1))), "2000-01-01 GMT-1");

			// 2000-09-01 (summertime)
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"\\/Date(967766400000)\\/\""), Is.EqualTo(new DateTimeOffset(2000, 9, 1, 0, 0, 0, TimeSpan.Zero)), "2000-09-01 UTC");
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"\\/Date(967759200000+0200)\\/\""), Is.EqualTo(new DateTimeOffset(2000, 9, 1, 0, 0, 0, TimeSpan.FromHours(2))), "2000-09-01 GMT+2 (Paris, DST)");

			// RoundTrip !
			var utcNow = DateTimeOffset.UtcNow;
			// /!\ JsonDateTime has a resolution to the millisecond, but UtcNow has a resolution up to the 'tick', which mean we have to truncate the value to milliseconds or else it will not roundtrip properly
			var utcRoundTrip = CrystalJson.Deserialize<DateTimeOffset>(CrystalJson.Serialize(utcNow));
			Assert.That(utcRoundTrip, Is.EqualTo(utcNow), "RoundTrip DateTimeOffset.UtcNow");

			var localNow = DateTimeOffset.Now;
			var localRoundTrip = CrystalJson.Deserialize<DateTimeOffset>(CrystalJson.Serialize(localNow));
			Assert.That(localRoundTrip, Is.EqualTo(localNow), "RoundTrip DateTimeOffset.Now");

			// direct deserialization
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"\\/Date(-62135596800000)\\/\""), Is.EqualTo(DateTimeOffset.MinValue));
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"\\/Date(946681200000+0200)\\/\""), Is.EqualTo(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(2))), "2000-01-01 GMT+2 (Paris, DST on)");
			// YYYYMMDD
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"20000101\""), Is.EqualTo(new DateTimeOffset(new(2000, 1, 1, 0, 0, 0, DateTimeKind.Local))), "2000-01-01 Local");
			// YYYYMMDDHHMMSS
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"20000101123456\""), Is.EqualTo(new DateTimeOffset(new (2000, 1, 1, 12, 34, 56, DateTimeKind.Local))), "2000-01-01 12:34:56 Local");
			// ISO 8601
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"2000-01-01T12:34:56Z\""), Is.EqualTo(new DateTimeOffset(2000, 1, 1, 12, 34, 56, TimeSpan.Zero)), "2000-01-01 12:34:56 GMT");
			// ISO 8601 with time offset
			Assert.That(CrystalJson.Deserialize<DateTimeOffset>("\"2000-01-01T12:34:56+01:00\""), Is.EqualTo(new DateTimeOffset(2000, 1, 1, 12, 34, 56, TimeSpan.FromHours(1))), "2000-01-01 12:34:56 (CET)");
		}

		[Test]
		public void Test_JsonDeserialize_NodaTime_Types()
		{
			#region Instant

			Assert.That(CrystalJson.Deserialize<NodaTime.Instant>("\"1970-01-01T00:00:00Z\""), Is.EqualTo(default(NodaTime.Instant)));
			Assert.That(CrystalJson.Deserialize<NodaTime.Instant>("\"1854-06-25T21:33:54.352Z\""), Is.EqualTo(NodaTime.Instant.FromUtc(1854, 06, 25, 21, 33, 54) + NodaTime.Duration.FromMilliseconds(352)));
			Assert.That(CrystalJson.Deserialize<NodaTime.Instant>("\"-1254-06-25T21:33:54.352Z\""), Is.EqualTo(NodaTime.Instant.FromUtc(-1254, 06, 25, 21, 33, 54) + NodaTime.Duration.FromMilliseconds(352)));

			// ensure that it roundtrips
			var now = NodaTime.SystemClock.Instance.GetCurrentInstant();
			Assert.That(CrystalJson.Deserialize<NodaTime.Instant>(CrystalJson.Serialize(now)), Is.EqualTo(now), "Instant roundtrip");

			// ensure that we can also read dates with offsets
			Assert.That(CrystalJson.Deserialize<NodaTime.Instant>("\"2015-07-17T00:00:00+02:00\""), Is.EqualTo(NodaTime.Instant.FromUtc(2015, 7, 16, 22, 0, 0)));
			Assert.That(CrystalJson.Deserialize<NodaTime.Instant>("\"2015-07-17T00:00:00-02:00\""), Is.EqualTo(NodaTime.Instant.FromUtc(2015, 7, 17, 2, 0, 0)));
			// and local dates
			Assert.That(CrystalJson.Deserialize<NodaTime.Instant>("\"2015-07-17T00:00:00\""), Is.EqualTo(NodaTime.Instant.FromDateTimeUtc(new DateTime(2015,7,17, 0, 0, 0, DateTimeKind.Local).ToUniversalTime())));

			#endregion

			#region Duration

			Assert.That(CrystalJson.Deserialize<NodaTime.Duration>("0"), Is.EqualTo(NodaTime.Duration.Zero), "Duration.Zero");
			Assert.That(CrystalJson.Deserialize<NodaTime.Duration>("3258"), Is.EqualTo(NodaTime.Duration.FromSeconds(3258)), "Duration (seconds)");
			Assert.That(CrystalJson.Deserialize<NodaTime.Duration>("5682.452"), Is.EqualTo(NodaTime.Duration.FromMilliseconds(5682452)), "Duration (seconds + miliseconds)");
			Assert.That(CrystalJson.Deserialize<NodaTime.Duration>("1E-9"), Is.EqualTo(NodaTime.Duration.Epsilon), "Duration (epsilon)");

			NodaTime.Duration elapsed = now - NodaTime.Instant.FromDateTimeUtc(new DateTime(2014, 7, 22, 23, 04, 00, DateTimeKind.Utc));
			Assert.That(CrystalJson.Deserialize<NodaTime.Duration>(CrystalJson.Serialize(elapsed)), Is.EqualTo(elapsed), "Duration roundtrip");

			#endregion

			#region ZonedDateTime

			// note: a ZonedDateTime is an Instant + DateTimeZone + Offset, but it can also be represented by an Instant (ticks) + a time zone ID
			// (http://stackoverflow.com/questions/14802672/serialize-nodatime-json#comment20786350_14830400)

			var dtz = NodaTime.DateTimeZoneProviders.Tzdb["Europe/Paris"];
			Assert.That(CrystalJson.Deserialize<NodaTime.ZonedDateTime>("\"0001-01-01T00:00:00Z UTC\""), Is.EqualTo(default(NodaTime.ZonedDateTime)));
			Assert.That(CrystalJson.Deserialize<NodaTime.ZonedDateTime>("\"1954-06-25T21:33:54.352+01:00 Europe/Paris\""), Is.EqualTo(new NodaTime.ZonedDateTime(NodaTime.Instant.FromUtc(1954, 06, 25, 20, 33, 54) + NodaTime.Duration.FromMilliseconds(352), dtz)));
			//note: if TZID is missing, it is impossible to deserialize a ZonedDatetime!
			Assert.That(() => CrystalJson.Deserialize<NodaTime.ZonedDateTime>("\"1954-06-25T21:33:54.352+01:00\""), Throws.InstanceOf<FormatException>(), "Missing TimeZone ID should fail");
			//note: if the offset is not valid for this date, it is not possible to deserialize a ZonedDatetime!
			Assert.That(() => CrystalJson.Deserialize<NodaTime.ZonedDateTime>("\"1854-06-25T21:33:54.352+01:00 Europe/Paris\""), Throws.InstanceOf<FormatException>(), "Paris was on a different offset in 1854 !");

			// ensure that it roundtrips
			var dtzNow = new NodaTime.ZonedDateTime(now, dtz);
			Assert.That(CrystalJson.Deserialize<NodaTime.ZonedDateTime>(CrystalJson.Serialize(dtzNow)), Is.EqualTo(dtzNow), "ZonedDateTime roundtripping");

			#endregion

			#region LocalDateTime

			Assert.That(CrystalJson.Deserialize<NodaTime.LocalDateTime>("\"0001-01-01T00:00:00\""), Is.EqualTo(default(NodaTime.LocalDateTime)));
			Assert.That(CrystalJson.Deserialize<NodaTime.LocalDateTime>("\"1854-06-25T21:33:54.352\""), Is.EqualTo(new NodaTime.LocalDateTime(1854, 06, 25, 21, 33, 54, 352)));
			Assert.That(CrystalJson.Deserialize<NodaTime.LocalDateTime>("\"-1254-06-25T21:33:54.352\""), Is.EqualTo(new NodaTime.LocalDateTime(-1254, 06, 25, 21, 33, 54, 352)));

			// ensure that it roundtrips
			var ldtNow = dtzNow.LocalDateTime;
			Assert.That(CrystalJson.Deserialize<NodaTime.LocalDateTime>(CrystalJson.Serialize(ldtNow)), Is.EqualTo(ldtNow), "LocalDatetime roundtripping");

			// ensure that we can deserialize an Instant into a local date time
			Assert.That(CrystalJson.Deserialize<NodaTime.LocalDateTime>("\"2017-06-21T12:34:56Z\""), Is.EqualTo(NodaTime.Instant.FromUtc(2017, 6, 21, 12, 34, 56).InZone(NodaTime.DateTimeZoneProviders.Tzdb.GetSystemDefault()).LocalDateTime));

			#endregion

			#region DateTimeZone

			var rnd = new Random();

			// from tzdb
			string id = NodaTime.DateTimeZoneProviders.Tzdb.Ids[rnd.Next(NodaTime.DateTimeZoneProviders.Tzdb.Ids.Count)];
			Assert.That(CrystalJson.Deserialize<NodaTime.DateTimeZone>(JsonEncoding.Encode(id)), Is.EqualTo(NodaTime.DateTimeZoneProviders.Tzdb.GetZoneOrNull(id)));

			// roundtrip
			Assert.That(CrystalJson.Deserialize<NodaTime.DateTimeZone>(CrystalJson.Serialize(dtz)), Is.EqualTo(dtz), "DateTimeZone roundtrip");

			#endregion

			#region OffsetDateTime

			Assert.That(
				CrystalJson.Deserialize<NodaTime.OffsetDateTime>("\"2012-01-02T03:04:05.0060007Z\""),
				Is.EqualTo(new NodaTime.LocalDateTime(2012, 1, 2, 3, 4, 5, 6).PlusTicks(7).WithOffset(NodaTime.Offset.Zero)),
				"Offset of 0 means UTC"
			);

			Assert.That(
				CrystalJson.Deserialize<NodaTime.OffsetDateTime>("\"2012-01-02T03:04:05.0060007+02:00\""),
				Is.EqualTo(new NodaTime.LocalDateTime(2012, 1, 2, 3, 4, 5, 6).PlusTicks(7).WithOffset(NodaTime.Offset.FromHours(2))),
				"Only HH:MM for the timezone offset"
			);

			Assert.That(
				CrystalJson.Deserialize<NodaTime.OffsetDateTime>("\"2012-01-02T03:04:05.0060007-01:30\""),
				Is.EqualTo(new NodaTime.LocalDateTime(2012, 1, 2, 3, 4, 5, 6).PlusTicks(7).WithOffset(NodaTime.Offset.FromHoursAndMinutes(-1, -30))),
				"Allow negative offsets"
			);

			#endregion

			#region Offset

			Assert.That(CrystalJson.Deserialize<NodaTime.Offset>("\"+00\""), Is.EqualTo(NodaTime.Offset.Zero));
			Assert.That(CrystalJson.Deserialize<NodaTime.Offset>("\"+00:00\""), Is.EqualTo(NodaTime.Offset.Zero));
			Assert.That(CrystalJson.Deserialize<NodaTime.Offset>("\"+02\""), Is.EqualTo(NodaTime.Offset.FromHours(2)));
			Assert.That(CrystalJson.Deserialize<NodaTime.Offset>("\"+01:30\""), Is.EqualTo(NodaTime.Offset.FromHoursAndMinutes(1, 30)));
			Assert.That(CrystalJson.Deserialize<NodaTime.Offset>("\"-01:30\""), Is.EqualTo(NodaTime.Offset.FromHoursAndMinutes(-1, -30)));

			#endregion
		}

		[Test]
		public void Test_JsonDeserialize_Array()
		{
			{ // empty
				var res = CrystalJson.Deserialize<int[]>("[]");
				Assert.That(res, Has.Length.EqualTo(0));
				//Assert.That(res, Is.SameAs(Array.Empty<int[]>()));
			}
			{ // empty, with extra spaces
				var res = CrystalJson.Deserialize<int[]>("[\t\t \t]");
				Assert.That(res, Has.Length.EqualTo(0));
				//Assert.That(res, Is.SameAs(Array.Empty<int[]>()));
			}

			// single value
			Assert.That(CrystalJson.Deserialize<int[]>("[1]"), Is.EqualTo(new[] { 1 }));
			Assert.That(CrystalJson.Deserialize<int[]>("[ 1 ]"), Is.EqualTo(new[] { 1 }));

			// multiple value
			Assert.That(CrystalJson.Deserialize<int[]>("[1,2,3]"), Is.EqualTo(new[] { 1, 2, 3 }));
			Assert.That(CrystalJson.Deserialize<int[]>("[ 1, 2, 3 ]"), Is.EqualTo(new[] { 1, 2, 3 }));

			// strings
			Assert.That(CrystalJson.Deserialize<string[]>(@"[""foo"",""bar""]"), Is.EqualTo(new[] { "foo", "bar" }));

			// mixed array
			Assert.That(CrystalJson.Deserialize<object[]>(@"[123,true,""foo""]"), Is.EqualTo(new object[] { 123, true, "foo" }));

			// jagged arrays
			Assert.That(CrystalJson.Deserialize<object[]>(@"[ [1,2,3], [true,false], [""foo"",""bar""] ]"), Is.EqualTo(new object[] {
				new[] { 1, 2, 3 },
				new[] { true, false },
				new[] { "foo", "bar" }
			}));

			// directed
			Assert.That(CrystalJson.Deserialize<int[]>("[1,2,3]"), Is.EqualTo(new [] { 1, 2, 3 }));
			Assert.That(CrystalJson.Deserialize<long[]>("[1,2,3]"), Is.EqualTo(new [] { 1L, 2L, 3L }));
			Assert.That(CrystalJson.Deserialize<float[]>("[1.1,2.2,3.3]"), Is.EqualTo(new [] { 1.1f, 2.2f, 3.3f }));
			Assert.That(CrystalJson.Deserialize<double[]>("[1.1,2.2,3.3]"), Is.EqualTo(new [] { 1.1d, 2.2d, 3.3d }));
			Assert.That(CrystalJson.Deserialize<bool[]>("[true,false,true]"), Is.EqualTo(new [] { true, false, true }));
			Assert.That(CrystalJson.Deserialize<string[]>(@"[""foo"",""bar"",""baz""]"), Is.EqualTo(new [] { "foo", "bar", "baz" }));

			// nested
			Assert.That(CrystalJson.Deserialize<int[][]>("[[1,2],[3,4]]"), Is.EqualTo(new int[][] { [ 1, 2 ], [ 3, 4 ] }));

		}

		[Test]
		public void Test_JsonDeserialize_STuples()
		{
			// STuple<...>
			Assert.That(CrystalJson.Deserialize<IVarTuple>("[ ]"), Is.EqualTo(STuple.Empty));
			Assert.That(CrystalJson.Deserialize<IVarTuple>("[ 123 ]"), Is.EqualTo(STuple.Create(123)));
			Assert.That(CrystalJson.Deserialize<IVarTuple>("[ 123, \"Hello\" ]"), Is.EqualTo(STuple.Create(123, "Hello")));
			Assert.That(CrystalJson.Deserialize<IVarTuple>("[ 123, \"Hello\", true ]"), Is.EqualTo(STuple.Create(123, "Hello", true)));
			Assert.That(CrystalJson.Deserialize<IVarTuple>("[ 123, \"Hello\", true, -1.5 ]"), Is.EqualTo(STuple.Create(123, "Hello", true, -1.5)));
			//note: since 3.13, NUnit handles calls to IStructuralEquatable.Equals(...) with its own comparer (that does not merge char and string)
			// => we must pass our own custom comparer for this to work!
			Assert.That(CrystalJson.Deserialize<IVarTuple>("[ 123, \"Hello\", true, -1.5, \"Z\" ]"), Is.EqualTo(STuple.Create(123, "Hello", true, -1.5, 'Z')).Using((IEqualityComparer) SimilarValueComparer.Default));
			Assert.That(CrystalJson.Deserialize<IVarTuple>("[ 123, \"Hello\", true, -1.5, \"Z\" ]"), Is.EqualTo(STuple.Create(123, "Hello", true, -1.5, 'Z')).Using((IEqualityComparer) SimilarValueComparer.Relaxed));
			Assert.That(CrystalJson.Deserialize<IVarTuple>("[ 123, \"Hello\", true, -1.5, \"Z\", \"World\" ]"), Is.EqualTo(STuple.Create(123, "Hello", true, -1.5, 'Z', "World")).Using((IEqualityComparer) SimilarValueComparer.Default));
			Assert.That(CrystalJson.Deserialize<IVarTuple>("[ 123, \"Hello\", true, -1.5, \"Z\", \"World\" ]"), Is.EqualTo(STuple.Create(123, "Hello", true, -1.5, 'Z', "World")).Using((IEqualityComparer) SimilarValueComparer.Relaxed));
			Assert.That(CrystalJson.Deserialize<IVarTuple>("[ 123, \"Hello\", true, -1.5, \"Z\", \"World\", 456 ]"), Is.EqualTo(STuple.Create(123, "Hello", true, -1.5, 'Z', "World", 456)).Using((IEqualityComparer) SimilarValueComparer.Default));
			Assert.That(CrystalJson.Deserialize<IVarTuple>("[ 123, \"Hello\", true, -1.5, \"Z\", \"World\", 456 ]"), Is.EqualTo(STuple.Create(123, "Hello", true, -1.5, 'Z', "World", 456)).Using((IEqualityComparer) SimilarValueComparer.Relaxed));
		}

		[Test]
		public void Test_JsonDeserialize_ValueTuples()
		{
			// ValueTuple
			Assert.That(CrystalJson.Deserialize<ValueTuple>("[ ]").Equals(ValueTuple.Create()), Is.True);

			// ValueTuple<...>
			Assert.That(CrystalJson.Deserialize<ValueTuple<int>>("[ 123 ]"), Is.EqualTo(ValueTuple.Create(123)));
			Assert.That(CrystalJson.Deserialize<ValueTuple<int, string>>("[ 123, \"Hello\" ]"), Is.EqualTo(ValueTuple.Create(123, "Hello")));
			Assert.That(CrystalJson.Deserialize<ValueTuple<int, string, bool>>("[ 123, \"Hello\", true ]"), Is.EqualTo(ValueTuple.Create(123, "Hello", true)));
			Assert.That(CrystalJson.Deserialize<ValueTuple<int, string, bool, double>>("[ 123, \"Hello\", true, -1.5 ]"), Is.EqualTo(ValueTuple.Create(123, "Hello", true, -1.5)));
			Assert.That(CrystalJson.Deserialize<ValueTuple<int, string, bool, double, char>>("[ 123, \"Hello\", true, -1.5, \"Z\" ]"), Is.EqualTo(ValueTuple.Create(123, "Hello", true, -1.5, 'Z')));
			Assert.That(CrystalJson.Deserialize<ValueTuple<int, string, bool, double, char, string>>("[ 123, \"Hello\", true, -1.5, \"Z\", \"World\" ]"), Is.EqualTo(ValueTuple.Create(123, "Hello", true, -1.5, 'Z', "World")));
			Assert.That(CrystalJson.Deserialize<ValueTuple<int, string, bool, double, char, string, int>>("[ 123, \"Hello\", true, -1.5, \"Z\", \"World\", 456 ]"), Is.EqualTo(ValueTuple.Create(123, "Hello", true, -1.5, 'Z', "World", 456)));
		}

		[Test]
		public void Test_JsonDeserialize_IPAddress()
		{
			Assert.That(CrystalJson.Deserialize<IPAddress>("\"127.0.0.1\""), Is.EqualTo(IPAddress.Loopback));
			Assert.That(CrystalJson.Deserialize<IPAddress>("\"0.0.0.0\""), Is.EqualTo(IPAddress.Any));

			Assert.That(CrystalJson.Deserialize<IPAddress>("\"::1\""), Is.EqualTo(IPAddress.IPv6Loopback));
			Assert.That(CrystalJson.Deserialize<IPAddress>("\"::\""), Is.EqualTo(IPAddress.IPv6Any));

			Assert.That(CrystalJson.Deserialize<IPAddress>("\"172.16.10.194\""), Is.EqualTo(IPAddress.Parse("172.16.10.194")));
			Assert.That(CrystalJson.Deserialize<IPAddress>("\"fe80::fd0b:d3d6:5a2:4549%13\""), Is.EqualTo(IPAddress.Parse("fe80::fd0b:d3d6:5a2:4549%13")));

			// we also must accept the syntax with brackets (ex: '[::1]') for IPv6 because it is found in some URLs
			Assert.That(CrystalJson.Deserialize<IPAddress>("\"[::1]\""), Is.EqualTo(IPAddress.IPv6Loopback));
			Assert.That(CrystalJson.Deserialize<IPAddress>("\"[::]\""), Is.EqualTo(IPAddress.IPv6Any));
			Assert.That(CrystalJson.Deserialize<IPAddress>("\"[fe80::fd0b:d3d6:5a2:4549%13]\""), Is.EqualTo(IPAddress.Parse("fe80::fd0b:d3d6:5a2:4549%13")));
		}

		[Test]
		public void Test_JsonDeserialize_Version()
		{
			Assert.That(CrystalJson.Deserialize<Version>("\"1.0\""), Is.EqualTo(new Version(1, 0)));
			Assert.That(CrystalJson.Deserialize<Version>("\"1.2.3\""), Is.EqualTo(new Version(1, 2, 3)));
			Assert.That(CrystalJson.Deserialize<Version>("\"1.2.3.4\""), Is.EqualTo(new Version(1, 2, 3, 4)));
		}

		[Test]
		public void Test_JsonDeserialize_SimpleObject()
		{
			{
				var res = CrystalJson.Deserialize<IDictionary<string, object>>("{}");
				Assert.That(res, Has.Count.EqualTo(0));
			}
			{
				var res = CrystalJson.Deserialize<IDictionary<string, object>>("{\r\n\t\t \r\n}");
				Assert.That(res, Has.Count.EqualTo(0));
			}
			{
				var res = CrystalJson.Deserialize<IDictionary<string, object>>("""{ "Name":"James Bond" }""");
				Assert.That(res, Has.Count.EqualTo(1));
				Assert.That(res.ContainsKey("Name"), Is.True);
				Assert.That(res["Name"], Is.EqualTo("James Bond"));
			}
			{
				var res = CrystalJson.Deserialize<IDictionary<string, object>>("""{ "Id":7, "Name":"James Bond", "IsDeadly":true }""");
				Assert.That(res, Has.Count.EqualTo(3));
				Assert.That(res["Name"], Is.EqualTo("James Bond"));
				Assert.That(res["Id"], Is.EqualTo(7));
				Assert.That(res["IsDeadly"], Is.True);
			}
			{
				var res = CrystalJson.Deserialize<IDictionary<string, object>>("""{ "Id":7, "Name":"James Bond", "IsDeadly":true, "Created":"\/Date(-52106400000+0200)\/", "Weapons":[{"Name":"Walter PPK"}] }""");
				Assert.That(res, Has.Count.EqualTo(5));
				Assert.That(res["Name"], Is.EqualTo("James Bond"));
				Assert.That(res["Id"], Is.EqualTo(7));
				Assert.That(res["IsDeadly"], Is.True);
				//Assert.That(res["Created"], Is.EqualTo(new DateTime(1968, 5, 8)));
				Assert.That(res["Created"], Is.EqualTo("/Date(-52106400000+0200)/")); //BUGBUG: handle the auto-detection of dates when converting from string to object ?
				var weapons = (IList<object>) res["Weapons"];
				Assert.That(weapons, Is.Not.Null);
				Assert.That(weapons, Has.Count.EqualTo(1));
				var weapon = (IDictionary<string, object>) weapons[0];
				Assert.That(weapon, Is.Not.Null);
				Assert.That(weapon["Name"], Is.EqualTo("Walter PPK"));
			}
		}

		[Test]
		public void Test_JsonDeserialize_CustomClass()
		{
			string jsonText = """{ "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08T00:00:00Z", "Modified": "2010-10-28T15:39:00Z", "DateOfBirth": "1920-11-11", "State": 42, "RatioOfStuff": 8641975.23 }""";
			var x = CrystalJson.Deserialize<DummyJsonClass>(jsonText);
			Assert.That(x, Is.Not.Null, jsonText);
			Assert.That(x, Is.InstanceOf<DummyJsonClass>());

			Assert.That(x.Valid, Is.True, "x.Valid");
			Assert.That(x.Name, Is.EqualTo("James Bond"), "x.Name");
			Assert.That(x.Index, Is.EqualTo(7), "x.Index");
			Assert.That(x.Size, Is.EqualTo(123456789), "x.Size");
			Assert.That(x.Height, Is.EqualTo(1.8f), "x.Height");
			Assert.That(x.Amount, Is.EqualTo(0.07d), "x.Amount");
			Assert.That(x.Created, Is.EqualTo(new DateTime(1968, 5, 8)), "x.Created");
			Assert.That(x.Modified, Is.EqualTo(new DateTime(2010, 10, 28, 15, 39, 0)), "x.Modified");
			Assert.That(x.DateOfBirth, Is.EqualTo(new DateOnly(1920, 11, 11)), "x.DateOfBirth");
			Assert.That(x.State, Is.EqualTo(DummyJsonEnum.Bar), "x.State");
			Assert.That(x.RatioOfStuff, Is.EqualTo(0.07d * 123456789), "x.RatioOfStuff");

			// it should round trip!
			string roundtripText = CrystalJson.Serialize(x);
			Assert.That(roundtripText, Is.EqualTo(jsonText), "LOOP 2!");
			var hibachi = CrystalJson.Deserialize<DummyJsonClass>(roundtripText);
			Assert.That(hibachi, Is.EqualTo(x), "TRUE LAST BOSS !!!");
		}

		[Test]
		public void Test_JsonDeserialize_CustomClass_Polymorphic()
		{
			{ // without any "$type" field, we will bind using the type specified in the parent container, if it is constructible (not abstract, not an interface)
				string jsonText = """{ "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08T00:00:00Z", "Modified": "2010-10-28T15:39:00Z", "DateOfBirth": "1920-11-11", "State": 42, "RatioOfStuff": 8641975.23 }""";
				var x = CrystalJson.Deserialize<DummyJsonBaseClass>(jsonText);
				Assert.That(x, Is.Not.Null, jsonText);
				Assert.That(x, Is.InstanceOf<DummyJsonBaseClass>());

				Assert.That(x.Valid, Is.True, "x.Valid");
				Assert.That(x.Name, Is.EqualTo("James Bond"), "x.Name");
				Assert.That(x.Index, Is.EqualTo(7), "x.Index");
				Assert.That(x.Size, Is.EqualTo(123456789), "x.Size");
				Assert.That(x.Height, Is.EqualTo(1.8f), "x.Height");
				Assert.That(x.Amount, Is.EqualTo(0.07d), "x.Amount");
				Assert.That(x.Created, Is.EqualTo(new DateTime(1968, 5, 8)), "x.Created");
				Assert.That(x.Modified, Is.EqualTo(new DateTime(2010, 10, 28, 15, 39, 0)), "x.Modified");
				Assert.That(x.DateOfBirth, Is.EqualTo(new DateOnly(1920, 11, 11)), "x.DateOfBirth");
				Assert.That(x.State, Is.EqualTo(DummyJsonEnum.Bar), "x.State");
				Assert.That(x.RatioOfStuff, Is.EqualTo(0.07d * 123456789), "x.RatioOfStuff");
			}

			{ // with a "$type" field, we should construct the exact type
				string jsonText = """{ "$type": "agent", "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08T00:00:00Z", "Modified": "2010-10-28T15:39:00Z", "DateOfBirth": "1920-11-11", "State": 42, "RatioOfStuff": 8641975.23 }""";
				var x = CrystalJson.Deserialize<DummyJsonBaseClass>(jsonText);
				Assert.That(x, Is.Not.Null, jsonText);
				Assert.That(x, Is.InstanceOf<DummyJsonBaseClass>());

				Assert.That(x.Valid, Is.True, "x.Valid");
				Assert.That(x.Name, Is.EqualTo("James Bond"), "x.Name");
				Assert.That(x.Index, Is.EqualTo(7), "x.Index");
				Assert.That(x.Size, Is.EqualTo(123456789), "x.Size");
				Assert.That(x.Height, Is.EqualTo(1.8f), "x.Height");
				Assert.That(x.Amount, Is.EqualTo(0.07d), "x.Amount");
				Assert.That(x.Created, Is.EqualTo(new DateTime(1968, 5, 8)), "x.Created");
				Assert.That(x.Modified, Is.EqualTo(new DateTime(2010, 10, 28, 15, 39, 0)), "x.Modified");
				Assert.That(x.DateOfBirth, Is.EqualTo(new DateOnly(1920, 11, 11)), "x.DateOfBirth");
				Assert.That(x.State, Is.EqualTo(DummyJsonEnum.Bar), "x.State");
				Assert.That(x.RatioOfStuff, Is.EqualTo(0.07d * 123456789), "x.RatioOfStuff");

				// it should round trip!
				string roundtripText = CrystalJson.Serialize(x);
				Assert.That(roundtripText, Is.EqualTo(jsonText), "LOOP 2!");
				var hibachi = CrystalJson.Deserialize<DummyJsonBaseClass>(roundtripText);
				Assert.That(hibachi, Is.EqualTo(x), "TRUE LAST BOSS !!!");
			}

			{ // with a "$type" field, we should construct the exact type (again, with a more derived type)
				string jsonText = """{ "$type": "spy", "DoubleAgentName": "Janov Bondovicz", "Valid": true, "Name": "James Bond", "Index": 7, "Size": 123456789, "Height": 1.8, "Amount": 0.07, "Created": "1968-05-08T00:00:00Z", "Modified": "2010-10-28T15:39:00Z", "DateOfBirth": "1920-11-11", "State": 42, "RatioOfStuff": 8641975.23 }""";
				var x = CrystalJson.Deserialize<DummyDerivedJsonClass>(jsonText);
				Assert.That(x, Is.Not.Null, jsonText);
				Assert.That(x, Is.InstanceOf<DummyJsonBaseClass>());

				Assert.That(x.Valid, Is.True, "x.Valid");
				Assert.That(x.Name, Is.EqualTo("James Bond"), "x.Name");
				Assert.That(x.Index, Is.EqualTo(7), "x.Index");
				Assert.That(x.Size, Is.EqualTo(123456789), "x.Size");
				Assert.That(x.Height, Is.EqualTo(1.8f), "x.Height");
				Assert.That(x.Amount, Is.EqualTo(0.07d), "x.Amount");
				Assert.That(x.Created, Is.EqualTo(new DateTime(1968, 5, 8)), "x.Created");
				Assert.That(x.Modified, Is.EqualTo(new DateTime(2010, 10, 28, 15, 39, 0)), "x.Modified");
				Assert.That(x.DateOfBirth, Is.EqualTo(new DateOnly(1920, 11, 11)), "x.DateOfBirth");
				Assert.That(x.State, Is.EqualTo(DummyJsonEnum.Bar), "x.State");
				Assert.That(x.RatioOfStuff, Is.EqualTo(0.07d * 123456789), "x.RatioOfStuff");
				Assert.That(x.DoubleAgentName, Is.EqualTo("Janov Bondovicz"));

				// it should round trip!
				string roundtripText = CrystalJson.Serialize(x);
				Assert.That(roundtripText, Is.EqualTo(jsonText), "LOOP 2!");
				var hibachi = CrystalJson.Deserialize<DummyJsonBaseClass>(roundtripText);
				Assert.That(hibachi, Is.EqualTo(x), "TRUE LAST BOSS !!!");
			}

		}

		[Test]
		public void Test_JsonDeserialize_CustomStruct()
		{
			string jsonText = "{ \"Valid\": true, \"Name\": \"James Bond\", \"Index\": 7, \"Size\": 123456789, \"Height\": 1.8, \"Amount\": 0.07, \"Created\": \"1968-05-08T00:00:00Z\", \"Modified\": \"2010-10-28T15:39:00Z\", \"DateOfBirth\": \"1920-11-11\", \"State\": 42, \"RatioOfStuff\": 8641975.23 }";
			var x = CrystalJson.Deserialize<DummyJsonStruct>(jsonText);
			Assert.That(x, Is.InstanceOf<DummyJsonStruct>());

			Assert.That(x.Valid, Is.True, "x.Valid");
			Assert.That(x.Name, Is.EqualTo("James Bond"), "x.Name");
			Assert.That(x.Index, Is.EqualTo(7), "x.Index");
			Assert.That(x.Size, Is.EqualTo(123456789), "x.Size");
			Assert.That(x.Height, Is.EqualTo(1.8f), "x.Height");
			Assert.That(x.Amount, Is.EqualTo(0.07d), "x.Amount");
			Assert.That(x.Created, Is.EqualTo(new DateTime(1968, 5, 8)), "x.Created");
			Assert.That(x.Modified, Is.EqualTo(new DateTime(2010, 10, 28, 15, 39, 0)), "x.Modified");
			Assert.That(x.DateOfBirth, Is.EqualTo(new DateOnly(1920, 11, 11)), "x.DateOfBirth");
			Assert.That(x.State, Is.EqualTo(DummyJsonEnum.Bar), "x.State");
			Assert.That(x.RatioOfStuff, Is.EqualTo(0.07d * 123456789), "x.RatioOfStuff");

			// round trip !
			string roundtripText = CrystalJson.Serialize(x);
			Assert.That(roundtripText, Is.EqualTo(jsonText), "LOOP 2!");
			var hibachi = CrystalJson.Deserialize<DummyJsonStruct>(roundtripText);
			Assert.That(hibachi, Is.EqualTo(x), "TRUE LAST BOSS !!!");
		}

	}

}
