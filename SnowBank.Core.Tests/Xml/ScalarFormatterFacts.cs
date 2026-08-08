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

namespace SnowBank.Data.Xml.Tests
{
	using System.Xml;
	using NUnit.Framework;
	using SnowBank.Data.Xml;

	/// <summary>Pins the exact lexical text produced by <see cref="CrystalXmlFormatters"/> for both XML profiles</summary>
	/// <remarks>
	/// <para>The DCS forms below were measured against a live <c>DataContractSerializer</c>.
	/// They are hardcoded as literal expected strings, not
	/// recomputed via <see cref="XmlConvert"/> in the test, so a change in the wrapped BCL behaviour on any target
	/// framework would be caught here instead of silently matching itself.</para>
	/// <para>The one exception is the machine-dependent UTC offset of a <see cref="DateTimeKind.Local"/>
	/// <see cref="DateTime"/>: that case compares against <see cref="XmlConvert.ToString(DateTime, XmlDateTimeSerializationMode)"/>
	/// computed in-test, per the task brief, instead of a hardcoded offset.</para>
	/// </remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-XML")]
	[Parallelizable(ParallelScope.All)]
	public sealed class ScalarFormatterFacts : SimpleTest
	{

		#region Boolean...

		[Test]
		public void Test_Boolean()
		{
			Assert.That(CrystalXmlFormatters.FormatBoolean(true), Is.EqualTo("true"));
			Assert.That(CrystalXmlFormatters.FormatBoolean(false), Is.EqualTo("false"));
			Assert.That(CrystalXmlFormatters.FormatBoolean(true), Is.EqualTo("true"));
			Assert.That(CrystalXmlFormatters.FormatBoolean(false), Is.EqualTo("false"));
		}

		#endregion

		#region Integers...

		[Test]
		public void Test_Int32()
		{
			Assert.That(CrystalXmlFormatters.FormatInt32(42), Is.EqualTo("42"));
			Assert.That(CrystalXmlFormatters.FormatInt32(-42), Is.EqualTo("-42"));
			Assert.That(CrystalXmlFormatters.FormatInt32(42), Is.EqualTo("42"));
		}

		[Test]
		public void Test_Int64()
		{
			Assert.That(CrystalXmlFormatters.FormatInt64(9_000_000_000_000_000_000L), Is.EqualTo("9000000000000000000"));
			Assert.That(CrystalXmlFormatters.FormatInt64(-1L), Is.EqualTo("-1"));
		}

		[Test]
		public void Test_Int16()
		{
			Assert.That(CrystalXmlFormatters.FormatInt16((short) -12345), Is.EqualTo("-12345"));
			Assert.That(CrystalXmlFormatters.FormatInt16((short) 12345), Is.EqualTo("12345"));
		}

		[Test]
		public void Test_SByte()
		{
			Assert.That(CrystalXmlFormatters.FormatSByte((sbyte) -128), Is.EqualTo("-128"));
			Assert.That(CrystalXmlFormatters.FormatSByte((sbyte) 127), Is.EqualTo("127"));
		}

		[Test]
		public void Test_Byte()
		{
			Assert.That(CrystalXmlFormatters.FormatByte((byte) 255), Is.EqualTo("255"));
			Assert.That(CrystalXmlFormatters.FormatByte((byte) 0), Is.EqualTo("0"));
		}

		[Test]
		public void Test_UInt16()
		{
			Assert.That(CrystalXmlFormatters.FormatUInt16((ushort) 65535), Is.EqualTo("65535"));
			Assert.That(CrystalXmlFormatters.FormatUInt16((ushort) 0), Is.EqualTo("0"));
		}

		[Test]
		public void Test_UInt32()
		{
			Assert.That(CrystalXmlFormatters.FormatUInt32(4_000_000_000U), Is.EqualTo("4000000000"));
			Assert.That(CrystalXmlFormatters.FormatUInt32(0U), Is.EqualTo("0"));
		}

		[Test]
		public void Test_UInt64()
		{
			Assert.That(CrystalXmlFormatters.FormatUInt64(18_000_000_000_000_000_000UL), Is.EqualTo("18000000000000000000"));
			Assert.That(CrystalXmlFormatters.FormatUInt64(0UL), Is.EqualTo("0"));
		}

		#endregion

		#region Floating point and decimal...

		[Test]
		public void Test_Decimal_Keeps_Scale()
		{
			Assert.That(CrystalXmlFormatters.FormatDecimal(1.50m), Is.EqualTo("1.50"));
			Assert.That(CrystalXmlFormatters.FormatDecimal(1.50m), Is.EqualTo("1.50"));
			Assert.That(CrystalXmlFormatters.FormatDecimal(-3.000m), Is.EqualTo("-3.000"));
		}

		[Test]
		public void Test_Double_Round_Trip_Forms()
		{
			Assert.That(CrystalXmlFormatters.FormatDouble(1.2e-9), Is.EqualTo("1.2E-09"));
			Assert.That(CrystalXmlFormatters.FormatDouble(1.2e-9), Is.EqualTo("1.2E-09"));
			Assert.That(CrystalXmlFormatters.FormatDouble(double.NaN), Is.EqualTo("NaN"));
			Assert.That(CrystalXmlFormatters.FormatDouble(double.PositiveInfinity), Is.EqualTo("INF"));
			Assert.That(CrystalXmlFormatters.FormatDouble(double.NegativeInfinity), Is.EqualTo("-INF"));
			Assert.That(CrystalXmlFormatters.FormatDouble(0.0), Is.EqualTo("0"));
		}

		[Test]
		public void Test_Single_Round_Trip_Forms()
		{
			Assert.That(CrystalXmlFormatters.FormatSingle(float.NaN), Is.EqualTo("NaN"));
			Assert.That(CrystalXmlFormatters.FormatSingle(float.PositiveInfinity), Is.EqualTo("INF"));
			Assert.That(CrystalXmlFormatters.FormatSingle(float.NegativeInfinity), Is.EqualTo("-INF"));
		}

		#endregion

		#region DateTime...

		[Test]
		public void Test_DateTime_MinValue_Has_No_Fraction()
		{
			Assert.That(CrystalXmlFormatters.FormatDateTime(DateTime.MinValue), Is.EqualTo("0001-01-01T00:00:00"));
			Assert.That(CrystalXmlFormatters.FormatDateTime(DateTime.MinValue), Is.EqualTo("0001-01-01T00:00:00"));
		}

		[Test]
		public void Test_DateTime_Unspecified_Kind_Has_No_Suffix()
		{
			var dt = new DateTime(2026, 8, 3, 12, 34, 56, DateTimeKind.Unspecified);
			Assert.That(CrystalXmlFormatters.FormatDateTime(dt), Is.EqualTo("2026-08-03T12:34:56"));
		}

		[Test]
		public void Test_DateTime_Utc_Kind_Has_Z_Suffix()
		{
			var dt = new DateTime(2026, 8, 3, 12, 34, 56, DateTimeKind.Utc);
			Assert.That(CrystalXmlFormatters.FormatDateTime(dt), Is.EqualTo("2026-08-03T12:34:56Z"));
			Assert.That(CrystalXmlFormatters.FormatDateTime(dt), Is.EqualTo("2026-08-03T12:34:56Z"));
		}

		[Test]
		public void Test_DateTime_Local_Kind_Uses_The_Machine_Offset()
		{
			// the offset is machine-dependent: compare against XmlConvert computed in-test, never a hardcoded offset
			var dt = new DateTime(2026, 8, 3, 12, 34, 56, DateTimeKind.Local);
			string expected = XmlConvert.ToString(dt, XmlDateTimeSerializationMode.RoundtripKind);
			Assert.That(CrystalXmlFormatters.FormatDateTime(dt), Is.EqualTo(expected));
			Assert.That(CrystalXmlFormatters.FormatDateTime(dt), Is.EqualTo(expected));
		}

		[Test]
		public void Test_DateTime_Keeps_Fractional_Seconds()
		{
			var dt = new DateTime(2026, 8, 3, 12, 34, 56, DateTimeKind.Utc).AddTicks(7890123);
			Assert.That(CrystalXmlFormatters.FormatDateTime(dt), Is.EqualTo("2026-08-03T12:34:56.7890123Z"));
		}

		#endregion

		#region TimeSpan (duration)...

		[Test]
		public void Test_Duration_Large_Value_With_Fractional_Seconds()
		{
			var ts = TimeSpan.FromTicks(1234567891234567);
			Assert.That(CrystalXmlFormatters.FormatDuration(ts), Is.EqualTo("P1428DT21H33M9.1234567S"));
			Assert.That(CrystalXmlFormatters.FormatDuration(ts), Is.EqualTo("P1428DT21H33M9.1234567S"));
		}

		[Test]
		public void Test_Duration_Whole_Seconds_No_Fraction()
		{
			var ts = new TimeSpan(1, 33, 30);
			Assert.That(CrystalXmlFormatters.FormatDuration(ts), Is.EqualTo("PT1H33M30S"));
			Assert.That(CrystalXmlFormatters.FormatDuration(ts), Is.EqualTo("PT1H33M30S"));
		}

		[Test]
		public void Test_Duration_Zero()
		{
			Assert.That(CrystalXmlFormatters.FormatDuration(TimeSpan.Zero), Is.EqualTo("PT0S"));
		}

		[Test]
		public void Test_Duration_Negative()
		{
			Assert.That(CrystalXmlFormatters.FormatDuration(TimeSpan.FromMinutes(-90)), Is.EqualTo("-PT1H30M"));
		}

		#endregion

		#region Guid...

		[Test]
		public void Test_Guid_Lowercase_Hyphenated()
		{
			var guid = Guid.Parse("01234567-89AB-CDEF-0123-456789ABCDEF");
			Assert.That(CrystalXmlFormatters.FormatGuid(guid), Is.EqualTo("01234567-89ab-cdef-0123-456789abcdef"));
			Assert.That(CrystalXmlFormatters.FormatGuid(guid), Is.EqualTo("01234567-89ab-cdef-0123-456789abcdef"));
			Assert.That(CrystalXmlFormatters.FormatGuid(Guid.Empty), Is.EqualTo("00000000-0000-0000-0000-000000000000"));
		}

		#endregion

		#region char (the one true divergence between the two profiles)...

		[Test]
		public void Test_Char_Dcs_Is_The_Code_Point()
		{
			Assert.That(CrystalXmlFormatters.FormatDcsChar('A'), Is.EqualTo("65"));
			Assert.That(CrystalXmlFormatters.FormatDcsChar('\0'), Is.EqualTo("0"));
			Assert.That(CrystalXmlFormatters.FormatDcsChar('é'), Is.EqualTo("233"));
		}

		[Test]
		public void Test_Char_Modern_Is_The_Character_Itself()
		{
			Assert.That(CrystalXmlFormatters.FormatModernChar('A'), Is.EqualTo("A"));
			Assert.That(CrystalXmlFormatters.FormatModernChar('é'), Is.EqualTo("é"));
		}

		#endregion

		#region byte[] (base64)...

		[Test]
		public void Test_Base64()
		{
			byte[] bytes = [0xDE, 0xAD, 0xBE, 0xEF];
			Assert.That(CrystalXmlFormatters.FormatBase64(bytes), Is.EqualTo("3q2+7w=="));
			Assert.That(CrystalXmlFormatters.FormatBase64(bytes), Is.EqualTo("3q2+7w=="));
			Assert.That(CrystalXmlFormatters.FormatBase64([]), Is.EqualTo(""));
		}

		[Test]
		public void Test_Base64_Rejects_Null()
		{
			Assert.That(() => CrystalXmlFormatters.FormatBase64(null!), Throws.InstanceOf<ArgumentNullException>());
		}

		#endregion

		#region Uri (raw text; XML escaping happens at the writer, not here)...

		[Test]
		public void Test_Uri_Escapes_Space_But_Leaves_Ampersand_Raw()
		{
			var uri = new Uri("https://acme.example/a b?x=1&y=2");
			Assert.That(CrystalXmlFormatters.FormatUri(uri), Is.EqualTo("https://acme.example/a%20b?x=1&y=2"));
			Assert.That(CrystalXmlFormatters.FormatUri(uri), Is.EqualTo("https://acme.example/a%20b?x=1&y=2"));
		}

		[Test]
		public void Test_Uri_Rejects_Null()
		{
			Assert.That(() => CrystalXmlFormatters.FormatUri(null!), Throws.InstanceOf<ArgumentNullException>());
		}

		#endregion

	}

}
