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

// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
namespace SnowBank.Text.Tests
{
	using System.Web;

	/// <summary>Tests for the static KTL class</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	[SetInvariantCulture]
	public class UrlEncoderTest : SimpleTest
	{
		// the netstandard2.0 build formats floating-point values with "R" (17 significant digits on netfx)
		private static readonly string PiDoubleText = Math.PI.ToString("R", System.Globalization.CultureInfo.InvariantCulture);


		[Test]
		public void Test_EncodeData()
		{
			// This function encodes the values into a query string (ie, what comes after the '?')

			Assert.Multiple(() =>
			{
				Assert.That(UrlEncoding.EncodeData(null), Is.EqualTo(string.Empty));
				Assert.That(UrlEncoding.EncodeData(string.Empty), Is.EqualTo(string.Empty));
				Assert.That(UrlEncoding.EncodeData(" "), Is.EqualTo("+"), "' ' -> '+'");
				Assert.That(UrlEncoding.EncodeData("+"), Is.EqualTo("%2b"), "'+' -> '%2b'");
				Assert.That(UrlEncoding.EncodeData("%"), Is.EqualTo("%25"), "'%' -> '%25'");
				Assert.That(UrlEncoding.EncodeData("/"), Is.EqualTo("%2f"), "'/' -> '%2f'");

				Assert.That(UrlEncoding.EncodeData("fooBAR"), Is.EqualTo("fooBAR"));
				Assert.That(UrlEncoding.EncodeData("All Your Bases Are Belong To Us"), Is.EqualTo("All+Your+Bases+Are+Belong+To+Us"));

				Assert.That(UrlEncoding.EncodeData("http://foobar.com/path to/the file.ext"), Is.EqualTo("http%3a%2f%2ffoobar.com%2fpath+to%2fthe+file.ext"));
				Assert.That(UrlEncoding.EncodeData("a + b = c"), Is.EqualTo("a+%2b+b+%3d+c"));
				Assert.That(UrlEncoding.EncodeData("!.:$^*@="), Is.EqualTo("!.%3a%24%5e*%40%3d"));
				Assert.That(UrlEncoding.EncodeData("%%2%z"), Is.EqualTo("%25%252%25z"));
			});
		}

		[Test]
		public void Test_EncodeData_UTF8_MultiBytes()
		{
			// UTF-8 is encoded on 2 bytes (or more)
			// cf http://www.ltg.ed.ac.uk/~richard/utf-8.cgi
			Assert.That(UrlEncoding.EncodeData("á"), Is.EqualTo("%c3%a1"));
			Assert.That(UrlEncoding.EncodeData("ن"), Is.EqualTo("%d9%86"), "ن, U0646 (1606), 'ARABIC LETTER NOON'");
			Assert.That(UrlEncoding.EncodeData("€"), Is.EqualTo("%e2%82%ac"), "€, U20AC (8364), 'EURO SIGN'");
			Assert.That(UrlEncoding.EncodeData("思"), Is.EqualTo("%e6%80%9d"), "思, U601D (24605), Kanji for OMOU (think)");
		}

		[Test]
		public void Test_EncodeData_With_Custom_Encoding()
		{
			// lossless
			Assert.That(UrlEncoding.EncodeData("é", Encoding.UTF8), Is.EqualTo("%c3%a9"), "UTF-8");
			Assert.That(UrlEncoding.EncodeData("é", Encoding.Unicode), Is.EqualTo("%e9%00"), "UTF-16");
			Assert.That(UrlEncoding.EncodeData("é", Encoding.GetEncoding("iso-8859-1")), Is.EqualTo("%e9"), "Latin-1");
			Assert.That(UrlEncoding.EncodeData("é", Encoding.GetEncoding(437)), Is.EqualTo("%82"), "OEM United States");

			// lossy
			Assert.That(UrlEncoding.EncodeData("é", Encoding.ASCII), Is.EqualTo("%3f"), "ASCII changes 'é' to '?'");
		}

		[Test]
		public void Test_EncodeData_Foreign_Languages()
		{
			// Unicode languages
			// note: obtained by copy-pasting the unicode text into Google's search field, and extracting the 'q=' parameter from the produced search url (via "Page Info"), converted to LowerCase (CTRL-U)
			// WARNING: ffox replaces ' ' with '%20' in the case of unicode auto-encoding!
			Log(HttpUtility.UrlPathEncode("الصفحة الرئيسية"));
			Log(HttpUtility.UrlEncode("الصفحة الرئيسية"));
			Log(Uri.EscapeDataString("الصفحة الرئيسية"));
			Log(UrlEncoding.EncodeData("الصفحة الرئيسية"));

			Assert.That(UrlEncoding.EncodeData("الصفحة الرئيسية"), Is.EqualTo("%d8%a7%d9%84%d8%b5%d9%81%d8%ad%d8%a9+%d8%a7%d9%84%d8%b1%d8%a6%d9%8a%d8%b3%d9%8a%d8%a9")); // Arabic
			Assert.That(UrlEncoding.EncodeData("メインページ"), Is.EqualTo("%e3%83%a1%e3%82%a4%e3%83%b3%e3%83%9a%e3%83%bc%e3%82%b8")); // Japanese
			Assert.That(UrlEncoding.EncodeData("首页"), Is.EqualTo("%e9%a6%96%e9%a1%b5")); // Chinese
			Assert.That(UrlEncoding.EncodeData("대문"), Is.EqualTo("%eb%8c%80%eb%ac%b8")); // Korean
			Assert.That(UrlEncoding.EncodeData("Κύρια Σελίδα"), Is.EqualTo("%ce%9a%cf%8d%cf%81%ce%b9%ce%b1+%ce%a3%ce%b5%ce%bb%ce%af%ce%b4%ce%b1")); // Greek
		}

		[Test]
		public void Test_Encode_DataObject()
		{
			// ValueTypes
			Assert.That(UrlEncoding.EncodeDataObject(123), Is.EqualTo("123"));
			Assert.That(UrlEncoding.EncodeDataObject(int.MinValue), Is.EqualTo("-2147483648"));
			Assert.That(UrlEncoding.EncodeDataObject(int.MaxValue), Is.EqualTo("2147483647"));

			Assert.That(UrlEncoding.EncodeDataObject(123456L), Is.EqualTo("123456"));
			Assert.That(UrlEncoding.EncodeDataObject(long.MinValue), Is.EqualTo("-9223372036854775808"));
			Assert.That(UrlEncoding.EncodeDataObject(long.MaxValue), Is.EqualTo("9223372036854775807"));

			Assert.That(UrlEncoding.EncodeDataObject(123.4f), Is.EqualTo("123.4"));
			Assert.That(UrlEncoding.EncodeDataObject(float.NaN), Is.EqualTo("NaN"));

			Assert.That(UrlEncoding.EncodeDataObject(123.456d), Is.EqualTo("123.456"));
			Assert.That(UrlEncoding.EncodeDataObject(double.NaN), Is.EqualTo("NaN"));
			Assert.That(UrlEncoding.EncodeDataObject(Math.PI), Is.EqualTo(PiDoubleText));

			Assert.That(UrlEncoding.EncodeDataObject(byte.MaxValue), Is.EqualTo("255"));
			Assert.That(UrlEncoding.EncodeDataObject(sbyte.MaxValue), Is.EqualTo("127"));
			Assert.That(UrlEncoding.EncodeDataObject(ushort.MaxValue), Is.EqualTo("65535"));
			Assert.That(UrlEncoding.EncodeDataObject(short.MaxValue), Is.EqualTo("32767"));
			Assert.That(UrlEncoding.EncodeDataObject(uint.MaxValue), Is.EqualTo("4294967295"));
			Assert.That(UrlEncoding.EncodeDataObject(ulong.MaxValue), Is.EqualTo("18446744073709551615"));

			Assert.That(UrlEncoding.EncodeDataObject(decimal.One), Is.EqualTo("1"));

			// Date & Times
			Assert.That(UrlEncoding.EncodeDataObject(TimeSpan.FromSeconds(1)), Is.EqualTo("1"));
			Assert.That(UrlEncoding.EncodeDataObject(TimeSpan.FromMinutes(1)), Is.EqualTo("60"));
			Assert.That(UrlEncoding.EncodeDataObject(TimeSpan.FromHours(1)), Is.EqualTo("3600"));
			Assert.That(UrlEncoding.EncodeDataObject(TimeSpan.FromDays(1)), Is.EqualTo("86400"));
			Assert.That(UrlEncoding.EncodeDataObject(TimeSpan.FromMilliseconds(1)), Is.EqualTo("0.001"));
			Assert.That(UrlEncoding.EncodeDataObject(TimeSpan.FromTicks(1)), Is.EqualTo("1E-07"));
			// Dates must use the shortest format possible
			Assert.That(UrlEncoding.EncodeDataObject(new DateTime(2013, 2, 28)), Is.EqualTo("20130228"));
			Assert.That(UrlEncoding.EncodeDataObject(new DateTime(2013, 2, 28, 14, 58, 32)), Is.EqualTo("20130228145832"));
			Assert.That(UrlEncoding.EncodeDataObject(new DateTime(2013, 2, 28, 14, 58, 32, 678)), Is.EqualTo("20130228145832678"));

			// Guid
			Assert.That(UrlEncoding.EncodeDataObject(Guid.Parse("d46272b7-4263-450a-824a-e3283c6a6a46")), Is.EqualTo("d46272b7-4263-450a-824a-e3283c6a6a46"));
			//TODO: encode Guid.Empty as String.Empty?
			Assert.That(UrlEncoding.EncodeDataObject(Guid.Empty), Is.EqualTo("00000000-0000-0000-0000-000000000000"));

			// Enums
			Assert.That(UrlEncoding.EncodeDataObject(DateTimeKind.Unspecified), Is.EqualTo("Unspecified"));
			Assert.That(UrlEncoding.EncodeDataObject(DateTimeKind.Utc), Is.EqualTo("Utc"));
			Assert.That(UrlEncoding.EncodeDataObject(DateTimeKind.Local), Is.EqualTo("Local"));
			Assert.That(UrlEncoding.EncodeDataObject((DateTimeKind)123), Is.EqualTo("123"));

			// Nullables
			Assert.That(UrlEncoding.EncodeDataObject((int?)123), Is.EqualTo("123"));
			Assert.That(UrlEncoding.EncodeDataObject(default(int?)), Is.EqualTo(string.Empty));
			Assert.That(UrlEncoding.EncodeDataObject((long?)123456), Is.EqualTo("123456"));
			Assert.That(UrlEncoding.EncodeDataObject(default(long?)), Is.EqualTo(string.Empty));
			Assert.That(UrlEncoding.EncodeDataObject((double?)Math.PI), Is.EqualTo(PiDoubleText));
			Assert.That(UrlEncoding.EncodeDataObject(default(double?)), Is.EqualTo(string.Empty));
			Assert.That(UrlEncoding.EncodeDataObject((DateTimeKind?) DateTimeKind.Utc), Is.EqualTo("Utc"));
			Assert.That(UrlEncoding.EncodeDataObject(default(DateTimeKind?)), Is.EqualTo(string.Empty));
		}

		[Test]
		public void Test_EncodeUri()
		{
			// This function validates the encoding of the PATH of a URI (ie, what comes before the '?'), that is, it ensures it is correctly encoded. For example it does not encode the '/'!
			// Not to be confused with EncodePath() which encodes a value for integration into a path (ie, which encodes the '/')
			// It is used to generate the url to a file, for example

			// Check some samples
			Assert.That(UrlEncoding.EncodeUri(null), Is.EqualTo(string.Empty));
			Assert.That(UrlEncoding.EncodeUri(String.Empty), Is.EqualTo(string.Empty));
			Assert.That(UrlEncoding.EncodeUri(" "), Is.EqualTo("%20"), "' ' -> '%20'");
			Assert.That(UrlEncoding.EncodeUri("+"), Is.EqualTo("+"), "'+' -> '+'");
			Assert.That(UrlEncoding.EncodeUri("%"), Is.EqualTo("%"), "'%' -> '%'");
			Assert.That(UrlEncoding.EncodeUri("/"), Is.EqualTo("/"), "'/' -> '/'");

			Assert.That(UrlEncoding.EncodeUri("fooBAR"), Is.EqualTo("fooBAR"));
			Assert.That(UrlEncoding.EncodeUri("All Your Bases Are Belong To Us"), Is.EqualTo("All%20Your%20Bases%20Are%20Belong%20To%20Us"));

			Assert.That(UrlEncoding.EncodeUri("http://foobar.com/path to/the file.ext"), Is.EqualTo("http://foobar.com/path%20to/the%20file.ext"));
			Assert.That(UrlEncoding.EncodeUri("a + b = c"), Is.EqualTo("a%20+%20b%20=%20c"));
			Assert.That(UrlEncoding.EncodeUri("!.:$^*@="), Is.EqualTo("!.:$^*@="));
			Assert.That(UrlEncoding.EncodeUri("%%2%z"), Is.EqualTo("%%2%z"));

			// UTF-8 is encoded on 2 bytes (or more)
			// cf http://www.ltg.ed.ac.uk/~richard/utf-8.cgi
			Assert.That(UrlEncoding.EncodeUri("á"), Is.EqualTo("%c3%a1"));
			Assert.That(UrlEncoding.EncodeUri("ن"), Is.EqualTo("%d9%86"), "ن, U0646 (1606), 'ARABIC LETTER NOON'");
			Assert.That(UrlEncoding.EncodeUri("€"), Is.EqualTo("%e2%82%ac"), "€, U20AC (8364), 'EURO SIGN'");
			Assert.That(UrlEncoding.EncodeUri("思"), Is.EqualTo("%e6%80%9d"), "思, U601D (24605), Kanji for OMOU (think)");

			// Unicode languages
			// note: obtained by copy-pasting the unicode text into Google's search field, and extracting the 'q=' parameter from the produced search url (via "Page Info"), converted to LowerCase (CTRL-U)
			Assert.That(UrlEncoding.EncodeUri("الصفحة الرئيسية"), Is.EqualTo("%d8%a7%d9%84%d8%b5%d9%81%d8%ad%d8%a9%20%d8%a7%d9%84%d8%b1%d8%a6%d9%8a%d8%b3%d9%8a%d8%a9")); // Arabic
			Assert.That(UrlEncoding.EncodeUri("メインページ"), Is.EqualTo("%e3%83%a1%e3%82%a4%e3%83%b3%e3%83%9a%e3%83%bc%e3%82%b8")); // Japanese
			Assert.That(UrlEncoding.EncodeUri("首页"), Is.EqualTo("%e9%a6%96%e9%a1%b5")); // Chinese
			Assert.That(UrlEncoding.EncodeUri("대문"), Is.EqualTo("%eb%8c%80%eb%ac%b8")); // Korean
			Assert.That(UrlEncoding.EncodeUri("Κύρια Σελίδα"), Is.EqualTo("%ce%9a%cf%8d%cf%81%ce%b9%ce%b1%20%ce%a3%ce%b5%ce%bb%ce%af%ce%b4%ce%b1")); // Greek
		}

		[Test]
		public void Test_EncodePath()
		{
			// This function encodes the data used to build the PATH of a URI (ie, what comes before the '?'). For example it encodes the '/', the '.', etc...
			// It is the one used in the case of URLs that contain a parameter

			Assert.Multiple(() =>
			{

				// Check some samples
				Assert.That(UrlEncoding.EncodePath(null), Is.EqualTo(string.Empty));
				Assert.That(UrlEncoding.EncodePath(string.Empty), Is.EqualTo(string.Empty));
				Assert.That(UrlEncoding.EncodePath(" "), Is.EqualTo("%20"), "' ' -> '%20'");
				Assert.That(UrlEncoding.EncodePath("+"), Is.EqualTo("%2B"), "'+' -> '%2B'");
				Assert.That(UrlEncoding.EncodePath("%"), Is.EqualTo("%25"), "'%' -> '%'");
				Assert.That(UrlEncoding.EncodePath("/"), Is.EqualTo("%2F"), "'/' -> '%2F'");

				Assert.That(UrlEncoding.EncodePath("fooBAR"), Is.EqualTo("fooBAR"));
				Assert.That(UrlEncoding.EncodeUri("All Your Bases Are Belong To Us"), Is.EqualTo("All%20Your%20Bases%20Are%20Belong%20To%20Us"));

				Assert.That(UrlEncoding.EncodePath("http://foobar.com/"), Is.EqualTo("http%3A%2F%2Ffoobar.com%2F"));
				Assert.That(UrlEncoding.EncodePath("/path to/the file.ext"), Is.EqualTo("%2Fpath%20to%2Fthe%20file.ext"));
				Assert.That(UrlEncoding.EncodePath("a + b = c"), Is.EqualTo("a%20%2B%20b%20%3D%20c"));
				Assert.That(UrlEncoding.EncodePath("!.:$^*@="), Is.EqualTo("!.%3A$%5E*@%3D"));
				Assert.That(UrlEncoding.EncodePath("%%2%z"), Is.EqualTo("%25%252%25z"));

				// UTF-8 is encoded on 2 bytes (or more)
				// cf http://www.ltg.ed.ac.uk/~richard/utf-8.cgi
				Assert.That(UrlEncoding.EncodePath("á"), Is.EqualTo("%C3%A1"));
				Assert.That(UrlEncoding.EncodePath("ن"), Is.EqualTo("%D9%86"), "ن, U0646 (1606), 'ARABIC LETTER NOON'");
				Assert.That(UrlEncoding.EncodePath("€"), Is.EqualTo("%E2%82%AC"), "€, U20AC (8364), 'EURO SIGN'");
				Assert.That(UrlEncoding.EncodePath("思"), Is.EqualTo("%E6%80%9D"), "思, U601D (24605), Kanji for OMOU (think)");

				// Unicode languages
				// note: obtained by copy-pasting the unicode text into Google's search field, and extracting the 'q=' parameter from the produced search url (via "Page Info"), converted to LowerCase (CTRL-U)
				Assert.That(UrlEncoding.EncodePath("الصفحة الرئيسية"), Is.EqualTo("%D8%A7%D9%84%D8%B5%D9%81%D8%AD%D8%A9%20%D8%A7%D9%84%D8%B1%D8%A6%D9%8A%D8%B3%D9%8A%D8%A9")); // Arabic
				Assert.That(UrlEncoding.EncodePath("メインページ"), Is.EqualTo("%E3%83%A1%E3%82%A4%E3%83%B3%E3%83%9A%E3%83%BC%E3%82%B8")); // Japanese
				Assert.That(UrlEncoding.EncodePath("首页"), Is.EqualTo("%E9%A6%96%E9%A1%B5")); // Chinese
				Assert.That(UrlEncoding.EncodePath("대문"), Is.EqualTo("%EB%8C%80%EB%AC%B8")); // Korean
				Assert.That(UrlEncoding.EncodePath("Κύρια Σελίδα"), Is.EqualTo("%CE%9A%CF%8D%CF%81%CE%B9%CE%B1%20%CE%A3%CE%B5%CE%BB%CE%AF%CE%B4%CE%B1")); // Greek
			});
		}

		[Test]
		public void Test_UrlPathEncode_Should_Be_Identical_To_HttpUtility()
		{
			void Check(string value)
			{
				string expected = HttpUtility.UrlPathEncode(value);
				string current = UrlEncoding.EncodeUri(value);

				if (expected != current)
				{
					Dump("VALUE:", value);
					Dump("CURRENT:", current);
					Dump("EXPECTED:", expected);
					Assert.Fail("Encoded path does not match reference implementation!");
				}
			}

			Check("\u0080");

			// Check ALL UNICODE characters!
			for (int i = 0; i < 65536; i++)
			{
				string s = new string((char) i, 1);
				Check(s);
			}

			// Fuzzing! Generate random strings, to slightly increase the probability of hitting weird cases
			var chars = new char[16];
			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				for (int j = 0; j < chars.Length; j++)
				{
					chars[j] = (char) rnd.Next(65536);
				}

				Check(new string(chars));
			}

		}

		[Test]
		public void Test_Decode_Basics()
		{
			Assert.That(UrlEncoding.Decode(null), Is.EqualTo(string.Empty));
			Assert.That(UrlEncoding.Decode(string.Empty), Is.EqualTo(string.Empty));
			Assert.That(UrlEncoding.Decode("fooBAR"), Is.EqualTo("fooBAR"));
			Assert.That(UrlEncoding.Decode("+"), Is.EqualTo(" "));
			Assert.That(UrlEncoding.Decode("%20"), Is.EqualTo(" "));
			Assert.That(UrlEncoding.Decode("%2b"), Is.EqualTo("+"));
			Assert.That(UrlEncoding.Decode("%25"), Is.EqualTo("%"));
			Assert.That(UrlEncoding.Decode("All+Your%20Bases+Are%20Belong+To+Us"), Is.EqualTo("All Your Bases Are Belong To Us"));
			Assert.That(UrlEncoding.Decode("a+%2b+b+%3d+c"), Is.EqualTo("a + b = c"));
			Assert.That(UrlEncoding.Decode("a%20%2b%20b%20%3d%20c"), Is.EqualTo("a + b = c"));
			Assert.That(UrlEncoding.Decode("http%3A%2F%2Ffoobar.com%2F"), Is.EqualTo("http://foobar.com/"));
			Assert.That(UrlEncoding.Decode("http%3a%2f%2Ffoobar.com%2f"), Is.EqualTo("http://foobar.com/"));
		}

		[Test]
		public void Test_Decode_Foreign_Languages()
		{
			Assert.That(UrlEncoding.Decode("%D8%A7%D9%84%D8%B5%D9%81%D8%AD%D8%A9_%D8%A7%D9%84%D8%B1%D8%A6%D9%8A%D8%B3%D9%8A%D8%A9"), Is.EqualTo("الصفحة_الرئيسية")); // Arabic
			Assert.That(UrlEncoding.Decode("%E3%83%A1%E3%82%A4%E3%83%B3%E3%83%9A%E3%83%BC%E3%82%B8"), Is.EqualTo("メインページ")); // Japanese
			Assert.That(UrlEncoding.Decode("%E9%A6%96%E9%A1%B5"), Is.EqualTo("首页")); // Chinese
			Assert.That(UrlEncoding.Decode("%EB%8C%80%EB%AC%B8"), Is.EqualTo("대문")); // Korean
			Assert.That(UrlEncoding.Decode("%CE%9A%CF%8D%CF%81%CE%B9%CE%B1%20%CE%A3%CE%B5%CE%BB%CE%AF%CE%B4%CE%B1"), Is.EqualTo("Κύρια Σελίδα")); // Greek
		}

		[Test]
		public void Test_Decode_Percent_Encoded()
		{
			var enc = Encoding.UTF8;
			for (int i = 0; i <= 0xFFFD; i++)
			{
				// Careful, we ignore Surrogates (which normally expect something after), because it breaks the UTF8 decoder when there is only a single char
				if (i >= 0xD800 && i < 0xE000) continue;

				string original = new string((char)i, 1);
				var bytes = enc.GetBytes(original);
				string encoded = string.Empty;
				foreach (var b in bytes) { encoded += "%" + b.ToString("X2"); }
				string decoded = UrlEncoding.Decode(encoded);
				Assert.That(decoded, Is.EqualTo(original), $"{i.ToString()} '{original}' => '{encoded}' {(int)decoded[0]}");
			}
		}

		[Test]
		public void Test_Decode_Unicode_Percent_Encoded()
		{
			for (int i = 128; i <= 0xFFFD; i++)
			{
				// Careful, we ignore Surrogates (which normally expect something after), because it breaks the UTF8 decoder when there is only a single char
				if (i >= 0xD800 && i < 0xE000) continue;

				// Uppercase
				string original = new string((char)i, 1);
				string encoded = "%u" + i.ToString("X4");
				string decoded = UrlEncoding.Decode(encoded);
				Assert.That(decoded, Is.EqualTo(original), $"{i} '{original}' => '{encoded}' {(int)decoded[0]}");
			}
		}

		[Test]
		public void Test_Do_Not_Decode_Malformed_Percent_Encoded()
		{
			// Check that we don't change malformed "%XX"

			// Percent Encoded
			Assert.That(UrlEncoding.Decode("foo%2"), Is.EqualTo("foo%2"));
			Assert.That(UrlEncoding.Decode("foo%"), Is.EqualTo("foo%"));
			Assert.That(UrlEncoding.Decode("%"), Is.EqualTo("%"));
			Assert.That(UrlEncoding.Decode("%G"), Is.EqualTo("%G"));
			Assert.That(UrlEncoding.Decode("%GG"), Is.EqualTo("%GG"));
			Assert.That(UrlEncoding.Decode("%1G"), Is.EqualTo("%1G"));
			Assert.That(UrlEncoding.Decode("%G1"), Is.EqualTo("%G1"));
			Assert.That(UrlEncoding.Decode("%2%41"), Is.EqualTo("%2A"));
		}

		[Test]
		public void Test_Do_Not_Decode_Malformed_Unicode_Percent_Encoded()
		{
			// Check that we don't change malformed "%uXXXX"

			Assert.That(UrlEncoding.Decode("foo%u123"), Is.EqualTo("foo%u123"));
			Assert.That(UrlEncoding.Decode("foo%u12"), Is.EqualTo("foo%u12"));
			Assert.That(UrlEncoding.Decode("foo%u1"), Is.EqualTo("foo%u1"));
			Assert.That(UrlEncoding.Decode("foo%u"), Is.EqualTo("foo%u"));

			Assert.That(UrlEncoding.Decode("%u"), Is.EqualTo("%u"));
			Assert.That(UrlEncoding.Decode("%u1"), Is.EqualTo("%u1"));
			Assert.That(UrlEncoding.Decode("%u12"), Is.EqualTo("%u12"));
			Assert.That(UrlEncoding.Decode("%u123"), Is.EqualTo("%u123"));

			Assert.That(UrlEncoding.Decode("%uABCG"), Is.EqualTo("%uABCG"));
			Assert.That(UrlEncoding.Decode("%uABGD"), Is.EqualTo("%uABGD"));
			Assert.That(UrlEncoding.Decode("%uAGCD"), Is.EqualTo("%uAGCD"));
			Assert.That(UrlEncoding.Decode("%uGBCD"), Is.EqualTo("%uGBCD"));
		}

		[Test]
		public void Test_Decode_Parts_Of_The_String()
		{
			Assert.That(UrlEncoding.Decode("Hello+World+!", 0, 5), Is.EqualTo("Hello"));
			Assert.That(UrlEncoding.Decode("Hello+World+!", 6, 5), Is.EqualTo("World"));
		}

		[Test]
		public void Test_Decode_With_Custom_Encoding()
		{
			// lossless
			Assert.That(UrlEncoding.Decode("%c3%a9", Encoding.UTF8), Is.EqualTo("é"), "UTF-8");
			Assert.That(UrlEncoding.Decode("%e9%00", Encoding.Unicode), Is.EqualTo("é"), "UTF-16");
			Assert.That(UrlEncoding.Decode("%e9", Encoding.GetEncoding("iso-8859-1")), Is.EqualTo("é"), "Latin-1");
			Assert.That(UrlEncoding.Decode("%82", Encoding.GetEncoding(437)), Is.EqualTo("é"), "OEM United States");
#pragma warning disable SYSLIB0001
			Assert.That(UrlEncoding.Decode("%2bAOk-", Encoding.UTF7), Is.EqualTo("é"), "UTF-7");
#pragma warning restore SYSLIB0001
		}

		[Test]
		public void Test_With_Wrong_Encodings()
		{
			// Check that encodings other than UTF-8 also work

			string encoded = UrlEncoding.EncodeData("é", Encoding.UTF8);
			Assert.That(encoded, Is.EqualTo("%c3%a9"));
			// If we get the encoding wrong, we should get the classic 'Ã©' which is the UTF-8 'é' seen as ANSI.
			string decoded = UrlEncoding.Decode(encoded, Encoding.GetEncoding(1252));
			Assert.That(decoded, Is.EqualTo("Ã©"));
		}

		[Test]
		public void Test_Decode_Huge_Strings()
		{
			const string CHUNK = "0123456789_ABCDEFGHJIKLMNOPQRSTUVWXYZéè@=-+./&%µ£¨§~#{[|}]èçàïô!";

			// test decoding with a very large string
			// note: (note: the implementation only uses the stack up to 1K, here we check that the codepath which uses memory also works

			string original = String.Empty;
			for (int i = 0; i < 32; i++) original += CHUNK;

			string encoded = UrlEncoding.EncodeData(original);
			string decoded = UrlEncoding.Decode(encoded);

			Assert.That(decoded, Is.EqualTo(original));
		}

		[Test]
		public void Test_ParseQueryString_Empty_Or_Null()
		{

			var values = UrlEncoding.ParseQueryString(null);
			Assert.That(values, Is.Not.Null, "values");
			Assert.That(values.Count, Is.EqualTo(0));

			values = UrlEncoding.ParseQueryString(String.Empty);
			Assert.That(values, Is.Not.Null, "values");
			Assert.That(values.Count, Is.EqualTo(0));

			values = UrlEncoding.ParseQueryString("?");
			Assert.That(values, Is.Not.Null, "values");
			Assert.That(values.Count, Is.EqualTo(0));
		}

		[Test]
		public void Test_ParseQueryString_SimpleParam()
		{

			var values = UrlEncoding.ParseQueryString("foo=hello+world");
			Assert.That(values, Is.Not.Null);
			Assert.That(values.Count, Is.EqualTo(1));
			Assert.That(values.Get("foo"), Is.EqualTo("hello world"));
			Assert.That(values.Get("bar"), Is.Null);

			// We must be able to absorb the leading '?' (in case of bad splitting)
			values = UrlEncoding.ParseQueryString("?foo=hello+world");
			Assert.That(values, Is.Not.Null);
			Assert.That(values.Count, Is.EqualTo(1));
			Assert.That(values.Get("foo"), Is.EqualTo("hello world"));
		}

		[Test]
		public void Test_ParseQueryString_MultipleParams()
		{
			var values = UrlEncoding.ParseQueryString("foo=bar&narf=zort");
			Assert.That(values, Is.Not.Null);
			Assert.That(values.Count, Is.EqualTo(2));
			Assert.That(values["foo"], Is.EqualTo("bar"));
			Assert.That(values["narf"], Is.EqualTo("zort"));
		}

		[Test]
		public void Test_ParseQueryString_EmptyParams()
		{
			// By convention "foo" on its own is present, but is null
			var values = UrlEncoding.ParseQueryString("foo");
			Assert.That(values, Is.Not.Null);
			Assert.That(values.Count, Is.EqualTo(1));
			Assert.That(values.AllKeys.Contains("foo"), Is.True);
			Assert.That(values["foo"], Is.Null);

			// By convention "foo=" on its own is present, and is String.Empty
			values = UrlEncoding.ParseQueryString("foo=");
			Assert.That(values, Is.Not.Null);
			Assert.That(values.Count, Is.EqualTo(1));
			Assert.That(values.AllKeys.Contains("foo"), Is.True);
			Assert.That(values["foo"], Is.EqualTo(String.Empty));

			// The presence of other params right after must not change anything
			values = UrlEncoding.ParseQueryString("foo&bar=&baz=123");
			Assert.That(values, Is.Not.Null);
			Assert.That(values.Count, Is.EqualTo(3));
			Assert.That(values.AllKeys.Contains("foo"), Is.True);
			Assert.That(values.AllKeys.Contains("bar"), Is.True);
			Assert.That(values["foo"], Is.Null);
			Assert.That(values["bar"], Is.EqualTo(String.Empty));
			Assert.That(values["baz"], Is.EqualTo("123"));

			// a trailing '&' is ignored
			values = UrlEncoding.ParseQueryString("foo=123&");
			Assert.That(values.Count, Is.EqualTo(1));
			Assert.That(values["foo"], Is.EqualTo("123"));
		}

		[Test]
		public void Test_ParseQueryString_MultipleValues()
		{
			var values = UrlEncoding.ParseQueryString("foo=1&foo=2&foo=3");
			Assert.That(values, Is.Not.Null);
			Assert.That(values.Count, Is.EqualTo(1));
			Assert.That(values["foo"], Is.EqualTo("1,2,3"), "this[string] should return all values joined");
			Assert.That(values.GetValues("foo"), Is.EqualTo(new [] { "1", "2", "3" }), "GetValues() should return all values in the same order");
			Assert.That(values.Get("foo"), Is.EqualTo("1,2,3"), "Get() should return all values joined");

			values = UrlEncoding.ParseQueryString("foo=1,2,3&bar=4");
			Assert.That(values, Is.Not.Null);
			Assert.That(values.Count, Is.EqualTo(2));
		}

		[Test]
		public void Test_ParseQueryString_PercentEncoded()
		{
			var values = UrlEncoding.ParseQueryString("foo+bar%26baz=narf%3Dzort");
			Assert.That(values.Count, Is.EqualTo(1));
			Assert.That(values["foo bar&baz"], Is.EqualTo("narf=zort"));
		}

		[Test]
		public void Test_ParseQueryString_RealLife_Google()
		{

			// real life examples
			var values = UrlEncoding.ParseQueryString("sclient=psy&hl=en&safe=off&site=&source=hp&q=Hello+World&aq=f&aqi=g5&aql=&oq=&pbx=1&bav=on.2,or.r_gc.r_pw.&fp=333cf9de8b26bb16");
			Assert.That(values, Is.Not.Null);
			Assert.That(values.Count, Is.EqualTo(13));
			Assert.That(values["sclient"], Is.EqualTo("psy"));
			Assert.That(values["hl"], Is.EqualTo("en"));
			Assert.That(values["safe"], Is.EqualTo("off"));
			Assert.That(values["site"], Is.EqualTo(string.Empty));
			Assert.That(values["source"], Is.EqualTo("hp"));
			Assert.That(values["q"], Is.EqualTo("Hello World"));
			Assert.That(values["aq"], Is.EqualTo("f"));
			Assert.That(values["aqi"], Is.EqualTo("g5"));
			Assert.That(values["aql"], Is.EqualTo(string.Empty));
			Assert.That(values["oq"], Is.EqualTo(string.Empty));
			Assert.That(values["pbx"], Is.EqualTo("1"));
			Assert.That(values["bav"], Is.EqualTo("on.2,or.r_gc.r_pw."));
			Assert.That(values["fp"], Is.EqualTo("333cf9de8b26bb16"));
		}

	}
}
