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

// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo
namespace SnowBank.Runtime.Converters.Tests
{
	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class StringConvertersFacts : SimpleTest
	{

		[Test]
		public void Test_ToInt64()
		{
			// success
			Assert.That(StringConverters.ToInt64("0", 666), Is.EqualTo(0));
			Assert.That(StringConverters.ToInt64("1", 666), Is.EqualTo(1));
			Assert.That(StringConverters.ToInt64("777", 666), Is.EqualTo(777));
			Assert.That(StringConverters.ToInt64("22091978", 666), Is.EqualTo(22091978));
			Assert.That(StringConverters.ToInt64("09221978", 666), Is.EqualTo(9221978));
			Assert.That(StringConverters.ToInt64("10737418235", 666), Is.EqualTo(10737418235));
			Assert.That(StringConverters.ToInt64("-273", 666), Is.EqualTo(-273));
			Assert.That(StringConverters.ToInt64("+273", 666), Is.EqualTo(273));
			Assert.That(StringConverters.ToInt64("-0", 666), Is.EqualTo(0));
			Assert.That(StringConverters.ToInt64("+0", 666), Is.EqualTo(0));
			Assert.That(StringConverters.ToInt64(" 456", 666), Is.EqualTo(456));
			Assert.That(StringConverters.ToInt64("456 ", 666), Is.EqualTo(456));
			Assert.That(StringConverters.ToInt64(" 456 ", 666), Is.EqualTo(456));

			// failures
			Assert.That(StringConverters.ToInt64(null, 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64("", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64("A", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64(".", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64("-", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64("+", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64("true", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64("false", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64("narfzort", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64(".123", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64("=123", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64("123A", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt64("123 A", 666), Is.EqualTo(666));
		}

		[Test]
		public void Test_ToInt32()
		{
			// success
			Assert.That(StringConverters.ToInt32("0", 666), Is.EqualTo(0));
			Assert.That(StringConverters.ToInt32("1", 666), Is.EqualTo(1));
			Assert.That(StringConverters.ToInt32("777", 666), Is.EqualTo(777));
			Assert.That(StringConverters.ToInt32("22091978", 666), Is.EqualTo(22091978));
			Assert.That(StringConverters.ToInt32("09221978", 666), Is.EqualTo(9221978));
			Assert.That(StringConverters.ToInt32("-273", 666), Is.EqualTo(-273));
			Assert.That(StringConverters.ToInt32("+273", 666), Is.EqualTo(273));
			Assert.That(StringConverters.ToInt32("-0", 666), Is.EqualTo(0));
			Assert.That(StringConverters.ToInt32("+0", 666), Is.EqualTo(0));
			Assert.That(StringConverters.ToInt32(" 456", 666), Is.EqualTo(456));
			Assert.That(StringConverters.ToInt32("456 ", 666), Is.EqualTo(456));
			Assert.That(StringConverters.ToInt32(" 456 ", 666), Is.EqualTo(456));

			// failures
			Assert.That(StringConverters.ToInt32(null, 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32("", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32("10737418235", 666), Is.EqualTo(666)); // overflow!
			Assert.That(StringConverters.ToInt32("A", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32(".", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32("-", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32("+", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32("true", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32("false", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32("narfzort", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32(".123", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32("=123", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32("123A", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToInt32("123 A", 666), Is.EqualTo(666));
		}

		[Test]
		public void Test_ToBoolean()
		{
			// success
			Assert.That(StringConverters.ToBoolean("true", false), Is.True);
			Assert.That(StringConverters.ToBoolean("false", true), Is.False);
			Assert.That(StringConverters.ToBoolean("True", false), Is.True);
			Assert.That(StringConverters.ToBoolean("False", true), Is.False);
			Assert.That(StringConverters.ToBoolean("TRUE", false), Is.True);
			Assert.That(StringConverters.ToBoolean("FALSE", true), Is.False);
			Assert.That(StringConverters.ToBoolean("tRuE", false), Is.True);
			Assert.That(StringConverters.ToBoolean("FaLsE", true), Is.False);
			Assert.That(StringConverters.ToBoolean("on", false), Is.True);
			Assert.That(StringConverters.ToBoolean("off", true), Is.False);
			Assert.That(StringConverters.ToBoolean("yes", false), Is.True);
			Assert.That(StringConverters.ToBoolean("no", true), Is.False);
			Assert.That(StringConverters.ToBoolean("1", false), Is.True);
			Assert.That(StringConverters.ToBoolean("0", true), Is.False);

			Assert.That(StringConverters.ToBoolean("true"), Is.True);
			Assert.That(StringConverters.ToBoolean("false"), Is.False);
			Assert.That(StringConverters.ToBoolean("True"), Is.True);
			Assert.That(StringConverters.ToBoolean("False"), Is.False);
			Assert.That(StringConverters.ToBoolean("TRUE"), Is.True);
			Assert.That(StringConverters.ToBoolean("FALSE"), Is.False);
			Assert.That(StringConverters.ToBoolean("tRuE"), Is.True);
			Assert.That(StringConverters.ToBoolean("FaLsE"), Is.False);
			Assert.That(StringConverters.ToBoolean("on"), Is.True);
			Assert.That(StringConverters.ToBoolean("off"), Is.False);
			Assert.That(StringConverters.ToBoolean("yes"), Is.True);
			Assert.That(StringConverters.ToBoolean("no"), Is.False);
			Assert.That(StringConverters.ToBoolean("1"), Is.True);
			Assert.That(StringConverters.ToBoolean("0"), Is.False);

			// failures
			Assert.That(StringConverters.ToBoolean(null, true), Is.True);
			Assert.That(StringConverters.ToBoolean("", true), Is.True);
			Assert.That(StringConverters.ToBoolean("a", true), Is.True);
			Assert.That(StringConverters.ToBoolean("a", false), Is.False);
			Assert.That(StringConverters.ToBoolean("666", true), Is.True);
			Assert.That(StringConverters.ToBoolean("666", false), Is.False);

			Assert.That(StringConverters.ToBoolean(null), Is.Null);
			Assert.That(StringConverters.ToBoolean(""), Is.Null);
			Assert.That(StringConverters.ToBoolean("a"), Is.Null);
			Assert.That(StringConverters.ToBoolean("a"), Is.Null);
			Assert.That(StringConverters.ToBoolean("666"), Is.Null);
			Assert.That(StringConverters.ToBoolean("666"), Is.Null);

		}

		[Test]
		public void Test_ToDouble()
		{
			// success

			Assert.That(StringConverters.ToDouble("0", 666), Is.EqualTo(0.0));
			Assert.That(StringConverters.ToDouble("1", 666), Is.EqualTo(1.0));
			Assert.That(StringConverters.ToDouble("777", 666), Is.EqualTo(777.0));
			Assert.That(StringConverters.ToDouble("22091978", 666), Is.EqualTo(22091978.0));
			Assert.That(StringConverters.ToDouble("09221978", 666), Is.EqualTo(9221978.0));
			Assert.That(StringConverters.ToDouble("10737418235", 666), Is.EqualTo(10737418235.0));
			Assert.That(StringConverters.ToDouble("-273", 666), Is.EqualTo(-273.0));
			Assert.That(StringConverters.ToDouble("+273", 666), Is.EqualTo(273.0));
			Assert.That(StringConverters.ToDouble("-0", 666), Is.EqualTo(0.0));
			Assert.That(StringConverters.ToDouble("+0", 666), Is.EqualTo(0.0));
			Assert.That(StringConverters.ToDouble("3.14", 666), Is.EqualTo(3.14));
			Assert.That(StringConverters.ToDouble("3.1415", 666), Is.EqualTo(3.1415));
			Assert.That(StringConverters.ToDouble("12.34", 666), Is.EqualTo(12.34));
			Assert.That(StringConverters.ToDouble("123.0", 666), Is.EqualTo(123.0));
			Assert.That(StringConverters.ToDouble("0.0000", 666), Is.EqualTo(0.0));
			Assert.That(StringConverters.ToDouble("1.2345E4", 666), Is.EqualTo(12345));
			Assert.That(StringConverters.ToDouble("1.2345e4", 666), Is.EqualTo(12345));
			Assert.That(StringConverters.ToDouble("123E-3", 666), Is.EqualTo(0.123));
			Assert.That(StringConverters.ToDouble("NaN", 666), Is.EqualTo(double.NaN));
			Assert.That(StringConverters.ToDouble("Infinity", 666), Is.EqualTo(double.PositiveInfinity));
			Assert.That(StringConverters.ToDouble("-Infinity", 666), Is.EqualTo(double.NegativeInfinity));
			Assert.That(StringConverters.ToDouble("∞", 666), Is.EqualTo(double.PositiveInfinity));
			Assert.That(StringConverters.ToDouble("-∞", 666), Is.EqualTo(double.NegativeInfinity));
			Assert.That(StringConverters.ToDouble(" 456", 666), Is.EqualTo(456.0));
			Assert.That(StringConverters.ToDouble("456 ", 666), Is.EqualTo(456.0));
			Assert.That(StringConverters.ToDouble(" 456 ", 666), Is.EqualTo(456.0));
			Assert.That(StringConverters.ToDouble("3.1415 ", 666), Is.EqualTo(3.1415));
			Assert.That(StringConverters.ToDouble("  3.1415", 666), Is.EqualTo(3.1415));

			Assert.That(StringConverters.ToDouble("0"), Is.EqualTo(0.0));
			Assert.That(StringConverters.ToDouble("1"), Is.EqualTo(1.0));
			Assert.That(StringConverters.ToDouble("777"), Is.EqualTo(777.0));
			Assert.That(StringConverters.ToDouble("22091978"), Is.EqualTo(22091978.0));
			Assert.That(StringConverters.ToDouble("09221978"), Is.EqualTo(9221978.0));
			Assert.That(StringConverters.ToDouble("10737418235"), Is.EqualTo(10737418235.0));
			Assert.That(StringConverters.ToDouble("-273"), Is.EqualTo(-273.0));
			Assert.That(StringConverters.ToDouble("+273"), Is.EqualTo(273.0));
			Assert.That(StringConverters.ToDouble("-0"), Is.EqualTo(0.0));
			Assert.That(StringConverters.ToDouble("+0"), Is.EqualTo(0.0));
			Assert.That(StringConverters.ToDouble("3.14"), Is.EqualTo(3.14));
			Assert.That(StringConverters.ToDouble("3.1415"), Is.EqualTo(3.1415));
			Assert.That(StringConverters.ToDouble("12.34"), Is.EqualTo(12.34));
			Assert.That(StringConverters.ToDouble("123.0"), Is.EqualTo(123.0));
			Assert.That(StringConverters.ToDouble("0.0000"), Is.EqualTo(0.0));
			Assert.That(StringConverters.ToDouble("1.2345E4"), Is.EqualTo(12345));
			Assert.That(StringConverters.ToDouble("1.2345e4"), Is.EqualTo(12345));
			Assert.That(StringConverters.ToDouble("123E-3"), Is.EqualTo(0.123));
			Assert.That(StringConverters.ToDouble("NaN"), Is.EqualTo(double.NaN));
			Assert.That(StringConverters.ToDouble("∞"), Is.EqualTo(double.PositiveInfinity));
			Assert.That(StringConverters.ToDouble("-∞"), Is.EqualTo(double.NegativeInfinity));
			Assert.That(StringConverters.ToDouble("Infinity"), Is.EqualTo(double.PositiveInfinity));
			Assert.That(StringConverters.ToDouble("-Infinity"), Is.EqualTo(double.NegativeInfinity));
			Assert.That(StringConverters.ToDouble(" 456"), Is.EqualTo(456.0));
			Assert.That(StringConverters.ToDouble("456 "), Is.EqualTo(456.0));
			Assert.That(StringConverters.ToDouble(" 456 "), Is.EqualTo(456.0));
			Assert.That(StringConverters.ToDouble("3.1415 "), Is.EqualTo(3.1415));
			Assert.That(StringConverters.ToDouble("  3.1415"), Is.EqualTo(3.1415));

			// failures

			Assert.That(StringConverters.ToDouble(null, 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToDouble("", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToDouble("E", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToDouble("A", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToDouble(".", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToDouble(",", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToDouble("-", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToDouble("+", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToDouble("E", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToDouble("3,1415", 666), Is.EqualTo(666));

			Assert.That(StringConverters.ToDouble(null), Is.Null);
			Assert.That(StringConverters.ToDouble(""), Is.Null);
			Assert.That(StringConverters.ToDouble("A"), Is.Null);
			Assert.That(StringConverters.ToDouble("hello"), Is.Null);
			Assert.That(StringConverters.ToDouble("."), Is.Null);
			Assert.That(StringConverters.ToDouble(","), Is.Null);
			Assert.That(StringConverters.ToDouble("-"), Is.Null);
			Assert.That(StringConverters.ToDouble("+"), Is.Null);
			Assert.That(StringConverters.ToDouble("E"), Is.Null);
			Assert.That(StringConverters.ToDouble("3,1415"), Is.Null);
		}

		[Test]
		public void Test_ToSingle()
		{
			// success

			Assert.That(StringConverters.ToSingle("0", 666), Is.EqualTo(0.0f));
			Assert.That(StringConverters.ToSingle("1", 666), Is.EqualTo(1.0f));
			Assert.That(StringConverters.ToSingle("777", 666), Is.EqualTo(777.0f));
			Assert.That(StringConverters.ToSingle("0123", 666), Is.EqualTo(123.0f));
			Assert.That(StringConverters.ToSingle("12345678", 666), Is.EqualTo(12345678.0f));
			Assert.That(StringConverters.ToSingle("-273", 666), Is.EqualTo(-273.0f));
			Assert.That(StringConverters.ToSingle("+273", 666), Is.EqualTo(273.0f));
			Assert.That(StringConverters.ToSingle("-0", 666), Is.EqualTo(0.0));
			Assert.That(StringConverters.ToSingle("+0", 666), Is.EqualTo(0.0));
			Assert.That(StringConverters.ToSingle("3.14", 666), Is.EqualTo(3.14f));
			Assert.That(StringConverters.ToSingle("3.1415", 666), Is.EqualTo(3.1415f));
			Assert.That(StringConverters.ToSingle("12.34", 666), Is.EqualTo(12.34f));
			Assert.That(StringConverters.ToSingle("123.0", 666), Is.EqualTo(123.0f));
			Assert.That(StringConverters.ToSingle("0.0000", 666), Is.EqualTo(0.0f));
			Assert.That(StringConverters.ToSingle("1.2345E4", 666), Is.EqualTo(12345f));
			Assert.That(StringConverters.ToSingle("1.2345e4", 666), Is.EqualTo(12345f));
			Assert.That(StringConverters.ToSingle("123E-3", 666), Is.EqualTo(0.123f));
			Assert.That(StringConverters.ToSingle("NaN", 666), Is.EqualTo(float.NaN));
			Assert.That(StringConverters.ToSingle("Infinity", 666), Is.EqualTo(float.PositiveInfinity));
			Assert.That(StringConverters.ToSingle("-Infinity", 666), Is.EqualTo(float.NegativeInfinity));
			Assert.That(StringConverters.ToSingle("∞", 666), Is.EqualTo(float.PositiveInfinity));
			Assert.That(StringConverters.ToSingle("-∞", 666), Is.EqualTo(float.NegativeInfinity));
			Assert.That(StringConverters.ToSingle(" 456", 666), Is.EqualTo(456.0f));
			Assert.That(StringConverters.ToSingle("456 ", 666), Is.EqualTo(456.0f));
			Assert.That(StringConverters.ToSingle(" 456 ", 666), Is.EqualTo(456.0f));
			Assert.That(StringConverters.ToSingle("3.1415 ", 666), Is.EqualTo(3.1415f));
			Assert.That(StringConverters.ToSingle("  3.1415", 666), Is.EqualTo(3.1415f));

			Assert.That(StringConverters.ToSingle("0"), Is.EqualTo(0.0f));
			Assert.That(StringConverters.ToSingle("1"), Is.EqualTo(1.0f));
			Assert.That(StringConverters.ToSingle("777"), Is.EqualTo(777.0f));
			Assert.That(StringConverters.ToSingle("0123"), Is.EqualTo(123.0f));
			Assert.That(StringConverters.ToSingle("12345678"), Is.EqualTo(12345678.0f));
			Assert.That(StringConverters.ToSingle("-273"), Is.EqualTo(-273.0f));
			Assert.That(StringConverters.ToSingle("+273"), Is.EqualTo(273.0f));
			Assert.That(StringConverters.ToSingle("-0"), Is.EqualTo(0.0f));
			Assert.That(StringConverters.ToSingle("+0"), Is.EqualTo(0.0f));
			Assert.That(StringConverters.ToSingle("3.14"), Is.EqualTo(3.14f));
			Assert.That(StringConverters.ToSingle("3.1415"), Is.EqualTo(3.1415f));
			Assert.That(StringConverters.ToSingle("12.34"), Is.EqualTo(12.34f));
			Assert.That(StringConverters.ToSingle("123.0"), Is.EqualTo(123.0f));
			Assert.That(StringConverters.ToSingle("0.0000"), Is.EqualTo(0.0f));
			Assert.That(StringConverters.ToSingle("1.2345E4"), Is.EqualTo(12345f));
			Assert.That(StringConverters.ToSingle("1.2345e4"), Is.EqualTo(12345f));
			Assert.That(StringConverters.ToSingle("123E-3"), Is.EqualTo(0.123f));
			Assert.That(StringConverters.ToSingle("NaN"), Is.EqualTo(float.NaN));
			Assert.That(StringConverters.ToSingle("∞"), Is.EqualTo(float.PositiveInfinity));
			Assert.That(StringConverters.ToSingle("-∞"), Is.EqualTo(float.NegativeInfinity));
			Assert.That(StringConverters.ToSingle("Infinity"), Is.EqualTo(float.PositiveInfinity));
			Assert.That(StringConverters.ToSingle("-Infinity"), Is.EqualTo(float.NegativeInfinity));
			Assert.That(StringConverters.ToSingle(" 456"), Is.EqualTo(456.0f));
			Assert.That(StringConverters.ToSingle("456 "), Is.EqualTo(456.0f));
			Assert.That(StringConverters.ToSingle(" 456 "), Is.EqualTo(456.0f));
			Assert.That(StringConverters.ToSingle("3.1415 "), Is.EqualTo(3.1415f));
			Assert.That(StringConverters.ToSingle("  3.1415"), Is.EqualTo(3.1415f));

			// failures

			Assert.That(StringConverters.ToSingle(null, 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToSingle("", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToSingle("E", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToSingle("A", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToSingle(".", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToSingle(",", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToSingle("-", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToSingle("+", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToSingle("E", 666), Is.EqualTo(666));
			Assert.That(StringConverters.ToSingle("3,1415", 666), Is.EqualTo(666));

			Assert.That(StringConverters.ToSingle(null), Is.Null);
			Assert.That(StringConverters.ToSingle(""), Is.Null);
			Assert.That(StringConverters.ToSingle("A"), Is.Null);
			Assert.That(StringConverters.ToSingle("hello"), Is.Null);
			Assert.That(StringConverters.ToSingle("."), Is.Null);
			Assert.That(StringConverters.ToSingle(","), Is.Null);
			Assert.That(StringConverters.ToSingle("-"), Is.Null);
			Assert.That(StringConverters.ToSingle("+"), Is.Null);
			Assert.That(StringConverters.ToSingle("E"), Is.Null);
			Assert.That(StringConverters.ToSingle("3,1415"), Is.Null);
		}

		[Test]
		public void Test_ToDateString()
		{

			//YYYYMMDD
			Assert.That(StringConverters.ToDateString(new DateTime(1978, 09, 22)), Is.EqualTo("19780922"));
			Assert.That(StringConverters.ToDateString(new DateTime(1978, 09, 22, 23, 59, 59, 999)), Is.EqualTo("19780922"));
			Assert.That(StringConverters.ToDateString(new DateTime(2078, 09, 22)), Is.EqualTo("20780922")); // Y2K ?
			Assert.That(StringConverters.ToDateString(new DateTime(6978, 09, 22)), Is.EqualTo("69780922"));  // Unix time ?
			Assert.That(StringConverters.ToDateString(DateTime.MinValue), Is.EqualTo("00010101"));
			Assert.That(StringConverters.ToDateString(DateTime.MaxValue), Is.EqualTo("99991231"));
		}

		[Test]
		public void Test_ToDateTimeString()
		{
			//YYYYMMDDHHMMSS
			Assert.That(StringConverters.ToDateTimeString(new DateTime(1978, 09, 22)), Is.EqualTo("19780922000000")); // without time
			Assert.That(StringConverters.ToDateTimeString(new DateTime(1978, 09, 22, 23, 59, 59, 999)), Is.EqualTo("19780922235959"));
			Assert.That(StringConverters.ToDateTimeString(new DateTime(2078, 09, 22, 12, 34, 56, 666)), Is.EqualTo("20780922123456")); // Y2K ?
			Assert.That(StringConverters.ToDateTimeString(new DateTime(6978, 09, 22, 01, 23, 45, 555)), Is.EqualTo("69780922012345"));  // Unix time ?
			Assert.That(StringConverters.ToDateTimeString(new DateTime(1978, 09, 22, 12, 30, 00)), Is.EqualTo("19780922123000")); // normal
			Assert.That(StringConverters.ToDateTimeString(new DateTime(1978, 09, 22, 18, 30, 00)), Is.EqualTo("19780922183000")); // ampm
			Assert.That(StringConverters.ToDateTimeString(new DateTime(1978, 09, 22, 23, 59, 59, 999)), Is.EqualTo("19780922235959")); // almost next day
			Assert.That(StringConverters.ToDateTimeString(DateTime.MinValue), Is.EqualTo("00010101000000"));
			Assert.That(StringConverters.ToDateTimeString(DateTime.MaxValue), Is.EqualTo("99991231235959"));
		}

		[Test]
		public void Test_CountDigits_Int32()
		{
			Assert.That(StringConverters.CountDigits(0), Is.EqualTo(1));
			Assert.That(StringConverters.CountDigits(1), Is.EqualTo(1));
			Assert.That(StringConverters.CountDigits(9), Is.EqualTo(1));
			Assert.That(StringConverters.CountDigits(10), Is.EqualTo(2));
			Assert.That(StringConverters.CountDigits(100), Is.EqualTo(3));
			Assert.That(StringConverters.CountDigits(1_000), Is.EqualTo(4));
			Assert.That(StringConverters.CountDigits(10_000), Is.EqualTo(5));
			Assert.That(StringConverters.CountDigits(100_000), Is.EqualTo(6));
			Assert.That(StringConverters.CountDigits(1_000_000), Is.EqualTo(7));
			Assert.That(StringConverters.CountDigits(10_000_000), Is.EqualTo(8));
			Assert.That(StringConverters.CountDigits(100_000_000), Is.EqualTo(9));
			Assert.That(StringConverters.CountDigits(1_000_000_000), Is.EqualTo(10));
			Assert.That(StringConverters.CountDigits(int.MaxValue), Is.EqualTo(10));
			Assert.That(StringConverters.CountDigits(-1), Is.EqualTo(2));
			Assert.That(StringConverters.CountDigits(-9), Is.EqualTo(2));
			Assert.That(StringConverters.CountDigits(-10), Is.EqualTo(3));
			Assert.That(StringConverters.CountDigits(-100), Is.EqualTo(4));
			Assert.That(StringConverters.CountDigits(-1_000), Is.EqualTo(5));
			Assert.That(StringConverters.CountDigits(-10_000), Is.EqualTo(6));
			Assert.That(StringConverters.CountDigits(-100_000), Is.EqualTo(7));
			Assert.That(StringConverters.CountDigits(-1_000_000), Is.EqualTo(8));
			Assert.That(StringConverters.CountDigits(-10_000_000), Is.EqualTo(9));
			Assert.That(StringConverters.CountDigits(-100_000_000), Is.EqualTo(10));
			Assert.That(StringConverters.CountDigits(-1_000_000_000), Is.EqualTo(11));
			Assert.That(StringConverters.CountDigits(int.MinValue), Is.EqualTo(11));
		}

		[Test]
		public void Test_CountDigits_UInt32()
		{
			Assert.That(StringConverters.CountDigits(0U), Is.EqualTo(1));
			Assert.That(StringConverters.CountDigits(1U), Is.EqualTo(1));
			Assert.That(StringConverters.CountDigits(9U), Is.EqualTo(1));
			Assert.That(StringConverters.CountDigits(10U), Is.EqualTo(2));
			Assert.That(StringConverters.CountDigits(100U), Is.EqualTo(3));
			Assert.That(StringConverters.CountDigits(1_000U), Is.EqualTo(4));
			Assert.That(StringConverters.CountDigits(10_000U), Is.EqualTo(5));
			Assert.That(StringConverters.CountDigits(100_000U), Is.EqualTo(6));
			Assert.That(StringConverters.CountDigits(1_000_000U), Is.EqualTo(7));
			Assert.That(StringConverters.CountDigits(10_000_000U), Is.EqualTo(8));
			Assert.That(StringConverters.CountDigits(100_000_000U), Is.EqualTo(9));
			Assert.That(StringConverters.CountDigits(1_000_000_000U), Is.EqualTo(10));
			Assert.That(StringConverters.CountDigits(uint.MaxValue), Is.EqualTo(10));
		}

		[Test]
		public void Test_CountDigits_Int64()
		{

			Assert.That(StringConverters.CountDigits(0L), Is.EqualTo(1));
			Assert.That(StringConverters.CountDigits(1L), Is.EqualTo(1));
			Assert.That(StringConverters.CountDigits(9L), Is.EqualTo(1));
			Assert.That(StringConverters.CountDigits(10L), Is.EqualTo(2));
			Assert.That(StringConverters.CountDigits(100L), Is.EqualTo(3));
			Assert.That(StringConverters.CountDigits(1_000L), Is.EqualTo(4));
			Assert.That(StringConverters.CountDigits(10_000L), Is.EqualTo(5));
			Assert.That(StringConverters.CountDigits(100_000L), Is.EqualTo(6));
			Assert.That(StringConverters.CountDigits(1_000_000L), Is.EqualTo(7));
			Assert.That(StringConverters.CountDigits(10_000_000L), Is.EqualTo(8));
			Assert.That(StringConverters.CountDigits(100_000_000L), Is.EqualTo(9));
			Assert.That(StringConverters.CountDigits(1_000_000_000L), Is.EqualTo(10));
			Assert.That(StringConverters.CountDigits(10_000_000_000L), Is.EqualTo(11));
			Assert.That(StringConverters.CountDigits(100_000_000_000L), Is.EqualTo(12));
			Assert.That(StringConverters.CountDigits(1_000_000_000_000L), Is.EqualTo(13));
			Assert.That(StringConverters.CountDigits(10_000_000_000_000L), Is.EqualTo(14));
			Assert.That(StringConverters.CountDigits(100_000_000_000_000L), Is.EqualTo(15));
			Assert.That(StringConverters.CountDigits(1_000_000_000_000_000L), Is.EqualTo(16));
			Assert.That(StringConverters.CountDigits(10_000_000_000_000_000L), Is.EqualTo(17));
			Assert.That(StringConverters.CountDigits(100_000_000_000_000_000L), Is.EqualTo(18));
			Assert.That(StringConverters.CountDigits(1_000_000_000_000_000_000L), Is.EqualTo(19));
			Assert.That(StringConverters.CountDigits(10_000_000_000_000_000_000L), Is.EqualTo(20));
			Assert.That(StringConverters.CountDigits(long.MaxValue), Is.EqualTo(19));
			Assert.That(StringConverters.CountDigits(-1L), Is.EqualTo(2));
			Assert.That(StringConverters.CountDigits(-9L), Is.EqualTo(2));
			Assert.That(StringConverters.CountDigits(-10L), Is.EqualTo(3));
			Assert.That(StringConverters.CountDigits(-100L), Is.EqualTo(4));
			Assert.That(StringConverters.CountDigits(-1_000L), Is.EqualTo(5));
			Assert.That(StringConverters.CountDigits(-10_000L), Is.EqualTo(6));
			Assert.That(StringConverters.CountDigits(-100_000L), Is.EqualTo(7));
			Assert.That(StringConverters.CountDigits(-1_000_000L), Is.EqualTo(8));
			Assert.That(StringConverters.CountDigits(-10_000_000L), Is.EqualTo(9));
			Assert.That(StringConverters.CountDigits(-100_000_000L), Is.EqualTo(10));
			Assert.That(StringConverters.CountDigits(-1_000_000_000L), Is.EqualTo(11));
			Assert.That(StringConverters.CountDigits(-10_000_000_000L), Is.EqualTo(12));
			Assert.That(StringConverters.CountDigits(-100_000_000_000L), Is.EqualTo(13));
			Assert.That(StringConverters.CountDigits(-1_000_000_000_000L), Is.EqualTo(14));
			Assert.That(StringConverters.CountDigits(-10_000_000_000_000L), Is.EqualTo(15));
			Assert.That(StringConverters.CountDigits(-100_000_000_000_000L), Is.EqualTo(16));
			Assert.That(StringConverters.CountDigits(-1_000_000_000_000_000L), Is.EqualTo(17));
			Assert.That(StringConverters.CountDigits(-10_000_000_000_000_000L), Is.EqualTo(18));
			Assert.That(StringConverters.CountDigits(-100_000_000_000_000_000L), Is.EqualTo(19));
			Assert.That(StringConverters.CountDigits(-1_000_000_000_000_000_000L), Is.EqualTo(20));
			Assert.That(StringConverters.CountDigits(long.MinValue), Is.EqualTo(20));
		}

		[Test]
		public void Test_CountDigits_UInt64()
		{
			Assert.That(StringConverters.CountDigits(0UL), Is.EqualTo(1));
			Assert.That(StringConverters.CountDigits(1UL), Is.EqualTo(1));
			Assert.That(StringConverters.CountDigits(9UL), Is.EqualTo(1));
			Assert.That(StringConverters.CountDigits(10UL), Is.EqualTo(2));
			Assert.That(StringConverters.CountDigits(100UL), Is.EqualTo(3));
			Assert.That(StringConverters.CountDigits(1_000UL), Is.EqualTo(4));
			Assert.That(StringConverters.CountDigits(10_000UL), Is.EqualTo(5));
			Assert.That(StringConverters.CountDigits(100_000UL), Is.EqualTo(6));
			Assert.That(StringConverters.CountDigits(1_000_000UL), Is.EqualTo(7));
			Assert.That(StringConverters.CountDigits(10_000_000UL), Is.EqualTo(8));
			Assert.That(StringConverters.CountDigits(100_000_000UL), Is.EqualTo(9));
			Assert.That(StringConverters.CountDigits(1_000_000_000UL), Is.EqualTo(10));
			Assert.That(StringConverters.CountDigits(10_000_000_000UL), Is.EqualTo(11));
			Assert.That(StringConverters.CountDigits(100_000_000_000UL), Is.EqualTo(12));
			Assert.That(StringConverters.CountDigits(1_000_000_000_000UL), Is.EqualTo(13));
			Assert.That(StringConverters.CountDigits(10_000_000_000_000UL), Is.EqualTo(14));
			Assert.That(StringConverters.CountDigits(100_000_000_000_000UL), Is.EqualTo(15));
			Assert.That(StringConverters.CountDigits(1_000_000_000_000_000UL), Is.EqualTo(16));
			Assert.That(StringConverters.CountDigits(10_000_000_000_000_000UL), Is.EqualTo(17));
			Assert.That(StringConverters.CountDigits(100_000_000_000_000_000UL), Is.EqualTo(18));
			Assert.That(StringConverters.CountDigits(1_000_000_000_000_000_000UL), Is.EqualTo(19));
			Assert.That(StringConverters.CountDigits(10_000_000_000_000_000_000UL), Is.EqualTo(20));
			Assert.That(StringConverters.CountDigits(ulong.MaxValue), Is.EqualTo(20));
		}

		[Test]
		public void Test_ParseDateTime()
		{
			//YYYYMMDD
			Assert.That(StringConverters.ParseDateTime("19780922"), Is.EqualTo(new DateTime(1978, 09, 22)));
			Assert.That(StringConverters.ParseDateTime("20780922"), Is.EqualTo(new DateTime(2078, 09, 22))); // Y2K ?
			Assert.That(StringConverters.ParseDateTime("69780922"), Is.EqualTo(new DateTime(6978, 09, 22)));  // Unix time ?
			Assert.That(StringConverters.ParseDateTime("00010101"), Is.EqualTo(DateTime.MinValue));
			Assert.That(StringConverters.ParseDateTime("99991231"), Is.EqualTo(DateTime.MaxValue.Date));
			Assert.That(StringConverters.ParseDateTime("1978"), Is.EqualTo(new DateTime(1978, 01, 01)));
			Assert.That(StringConverters.ParseDateTime("2078"), Is.EqualTo(new DateTime(2078, 01, 01)));
			Assert.That(StringConverters.ParseDateTime("6978"), Is.EqualTo(new DateTime(6978, 01, 01)));
			Assert.That(StringConverters.ParseDateTime("197809"), Is.EqualTo(new DateTime(1978, 09, 01)));
			Assert.That(StringConverters.ParseDateTime("207809"), Is.EqualTo(new DateTime(2078, 09, 01)));
			Assert.That(StringConverters.ParseDateTime("697809"), Is.EqualTo(new DateTime(6978, 09, 01)));

			// Default Values
			Assert.That(StringConverters.ParseDateTime("narfzort", DateTime.MinValue, CultureInfo.InvariantCulture), Is.EqualTo(DateTime.MinValue));
			Assert.That(StringConverters.ParseDateTime("narfzort", DateTime.MaxValue, CultureInfo.InvariantCulture), Is.EqualTo(DateTime.MaxValue));
			Assert.That(StringConverters.ParseDateTime("Z20060101", new DateTime(2006, 2, 2), CultureInfo.InvariantCulture), Is.EqualTo(new DateTime(2006, 2, 2)));
			Assert.That(StringConverters.ParseDateTime("20060101", new DateTime(2006, 2, 2), CultureInfo.InvariantCulture), Is.EqualTo(new DateTime(2006, 1, 1)));
			Assert.That(StringConverters.ParseDateTime("20070230", DateTime.MaxValue, CultureInfo.InvariantCulture), Is.EqualTo(DateTime.MaxValue));

			//YYYYMMDDHHMMSS
			Assert.That(StringConverters.ParseDateTime("19780922123000"), Is.EqualTo(new DateTime(1978, 09, 22, 12, 30, 00))); // normal
			Assert.That(StringConverters.ParseDateTime("19780922183000"), Is.EqualTo(new DateTime(1978, 09, 22, 18, 30, 00))); // ampm
			Assert.That(StringConverters.ParseDateTime("19780922000000"), Is.EqualTo(new DateTime(1978, 09, 22))); // without time
			Assert.That(StringConverters.ParseDateTime("19780922235959"), Is.EqualTo(new DateTime(1978, 09, 22, 23, 59, 59))); // almost next day

			//exceptions
			Assert.That(() => StringConverters.ParseDateTime("20070230"), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => StringConverters.ParseDateTime(null), Throws.InstanceOf<ArgumentNullException>());
			Assert.That(() => StringConverters.ParseDateTime("206"), Throws.InstanceOf<FormatException>());
			Assert.That(() => StringConverters.ParseDateTime("Z20060101"), Throws.InstanceOf<FormatException>());

			Assert.That(StringConverters.ParseDateTime(null, DateTime.MaxValue, CultureInfo.InvariantCulture), Is.EqualTo(DateTime.MaxValue));
			Assert.That(StringConverters.ParseDateTime("206", DateTime.MaxValue, CultureInfo.InvariantCulture), Is.EqualTo(DateTime.MaxValue));
			Assert.That(StringConverters.ParseDateTime("Z20060101", DateTime.MaxValue, CultureInfo.InvariantCulture), Is.EqualTo(DateTime.MaxValue));
		}

		[Test]
		public void Test_TryParseDateTime()
		{
			// TryParse
			Assert.That(StringConverters.TryParseDateTime("19780922", CultureInfo.InvariantCulture, out _, false), Is.True);
			Assert.That(StringConverters.TryParseDateTime("20780922", CultureInfo.InvariantCulture, out _, false), Is.True); // Y2K ?
			Assert.That(StringConverters.TryParseDateTime("69780922", CultureInfo.InvariantCulture, out _, false), Is.True);  // Unix time ?
			Assert.That(StringConverters.TryParseDateTime("00010101", CultureInfo.InvariantCulture, out _, false), Is.True);
			Assert.That(StringConverters.TryParseDateTime("99991231", CultureInfo.InvariantCulture, out _, false), Is.True);
			Assert.That(StringConverters.TryParseDateTime("1978", CultureInfo.InvariantCulture, out _, false), Is.True);
			Assert.That(StringConverters.TryParseDateTime("2078", CultureInfo.InvariantCulture, out _, false), Is.True);
			Assert.That(StringConverters.TryParseDateTime("6978", CultureInfo.InvariantCulture, out _, false), Is.True);
			Assert.That(StringConverters.TryParseDateTime("197809", CultureInfo.InvariantCulture, out _, false), Is.True);
			Assert.That(StringConverters.TryParseDateTime("207809", CultureInfo.InvariantCulture, out _, false), Is.True);
			Assert.That(StringConverters.TryParseDateTime("697809", CultureInfo.InvariantCulture, out _, false), Is.True);

			// exceptions ...
			Assert.That(StringConverters.TryParseDateTime(null, CultureInfo.InvariantCulture, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("206", CultureInfo.InvariantCulture, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("Z20060101", CultureInfo.InvariantCulture, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("20070230", CultureInfo.InvariantCulture, out _, false), Is.False);

			// Really messed-up dates
			Assert.That(StringConverters.TryParseDateTime("0000", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("000001", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("200700", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("00000000", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("20070000", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("20070100", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("20071301", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("20071232", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("20070230", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("00000000000000", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("20070000000000", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("20070100000000", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("20070103250000", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("20070103237500", null, out _, false), Is.False);
			Assert.That(StringConverters.TryParseDateTime("20070103235960", null, out _, false), Is.False);

		}

		[Test]
		public void Test_ToDateTime()
		{
			// Cultures
			Assert.That(StringConverters.ToDateTime("22/09/1978 12:30:45", DateTime.MinValue, new CultureInfo("fr-FR", false)), Is.EqualTo(new DateTime(1978, 09, 22, 12, 30, 45)));
			Assert.That(StringConverters.ToDateTime("22-09-1978 12:30:45", DateTime.MinValue, new CultureInfo("fr-FR", false)), Is.EqualTo(new DateTime(1978, 09, 22, 12, 30, 45)));
			Assert.That(StringConverters.ToDateTime("22/9/1978 12:30", DateTime.MinValue, new CultureInfo("fr-FR", false)), Is.EqualTo(new DateTime(1978, 09, 22, 12, 30, 00)));
			Assert.That(StringConverters.ToDateTime("22/09/1978", DateTime.MinValue, new CultureInfo("fr-FR", false)), Is.EqualTo(new DateTime(1978, 09, 22, 00, 00, 00)));
			Assert.That(StringConverters.ToDateTime("9/22/1978 12:30:45", DateTime.MinValue, new CultureInfo("en-US", false)), Is.EqualTo(new DateTime(1978, 09, 22, 12, 30, 45)));
			Assert.That(StringConverters.ToDateTime("9-22-1978 12:30:45", DateTime.MinValue, new CultureInfo("en-US", false)), Is.EqualTo(new DateTime(1978, 09, 22, 12, 30, 45)));
			Assert.That(StringConverters.ToDateTime("9/22/1978 3:30pm", DateTime.MinValue, new CultureInfo("en-US", false)), Is.EqualTo(new DateTime(1978, 09, 22, 15, 30, 00)));
			Assert.That(StringConverters.ToDateTime("9/22/1978", DateTime.MinValue, new CultureInfo("en-US", false)), Is.EqualTo(new DateTime(1978, 09, 22, 00, 00, 00)));
			Assert.That(StringConverters.ToDateTime("1978-9-22 12:30:45", DateTime.MinValue, new CultureInfo("de-DE", false)), Is.EqualTo(new DateTime(1978, 09, 22, 12, 30, 45)));
			Assert.That(StringConverters.ToDateTime("1978-9-22", DateTime.MinValue, new CultureInfo("de-DE", false)), Is.EqualTo(new DateTime(1978, 09, 22, 00, 00, 00)));

			// Timestamps with culture
			Assert.That(StringConverters.ToDateTime("19780922123045", DateTime.MinValue, new CultureInfo("fr-FR", false)), Is.EqualTo(new DateTime(1978, 09, 22, 12, 30, 45)));
			Assert.That(StringConverters.ToDateTime("19780922123045", DateTime.MinValue, new CultureInfo("en-US", false)), Is.EqualTo(new DateTime(1978, 09, 22, 12, 30, 45)));
			Assert.That(StringConverters.ToDateTime("19780922123045", DateTime.MinValue, new CultureInfo("de-DE", false)), Is.EqualTo(new DateTime(1978, 09, 22, 12, 30, 45)));

			// Messed-up dates
			Assert.That(StringConverters.ToDateTime("22 Septembre 1978 13:30:45", DateTime.MinValue, new CultureInfo("fr-FR", false)), Is.EqualTo(new DateTime(1978, 09, 22, 13, 30, 45)), "22 Septembre 1978 13:30:45");
			Assert.That(StringConverters.ToDateTime("22 September 1978 1:30:45 PM", DateTime.MinValue, new CultureInfo("en-US", false)), Is.EqualTo(new DateTime(1978, 09, 22, 13, 30, 45)), "22 September 1978 1:30:45 PM");
			Assert.That(StringConverters.ToDateTime("22. September 1978 13:30:45", DateTime.MinValue, new CultureInfo("de-DE", false)), Is.EqualTo(new DateTime(1978, 09, 22, 13, 30, 45)), "22. September 1978 13:30:45");

			// Even more messed-up dates
			Assert.That(StringConverters.ToDateTime("Vendredi 22 Septembre 1978 13:30:45", DateTime.MinValue, new CultureInfo("fr-FR", false)), Is.EqualTo(new DateTime(1978, 09, 22, 13, 30, 45)), "Vendredi 22 Septembre 1978 13:30:45");
			Assert.That(StringConverters.ToDateTime("Vendredi 22 Septembre 1978", DateTime.MinValue, new CultureInfo("fr-FR", false)), Is.EqualTo(new DateTime(1978, 09, 22, 0, 0, 0)), "Vendredi 22 Septembre 1978");
			Assert.That(StringConverters.ToDateTime("Friday, September 22, 1978 1:30:45 PM", DateTime.MinValue, new CultureInfo("en-US", false)), Is.EqualTo(new DateTime(1978, 09, 22, 13, 30, 45)), "Friday, September 22, 1978 1:30:45 PM");
			Assert.That(StringConverters.ToDateTime("Friday, September 22, 1978", DateTime.MinValue, new CultureInfo("en-US", false)), Is.EqualTo(new DateTime(1978, 09, 22, 0, 0, 0)), "Friday, September 22, 1978");
			Assert.That(StringConverters.ToDateTime("Freitag, 22. September 1978 13:30:45", DateTime.MinValue, new CultureInfo("de-DE", false)), Is.EqualTo(new DateTime(1978, 09, 22, 13, 30, 45)), "Freitag, 22. September 1978 13:30:45");
			Assert.That(StringConverters.ToDateTime("Freitag, 22. September 1978", DateTime.MinValue, new CultureInfo("de-DE", false)), Is.EqualTo(new DateTime(1978, 09, 22, 0, 0, 0)), "Freitag, 22. September 1978");
		}

	}

}
