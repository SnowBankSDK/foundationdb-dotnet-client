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

// ReSharper disable AssignNullToNotNullAttribute
// ReSharper disable StringLiteralTypo
// ReSharper disable SuspiciousTypeConversion.Global
// ReSharper disable RedundantCast
#pragma warning disable NUnit2010

namespace SnowBank.Core.Tests
{

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class Uuid48Facts : SimpleTest
	{

		[Test]
		public void Test_Uuid48_Empty()
		{
			Assert.Multiple(() =>
			{
				Assert.That(Uuid48.Empty.ToString(), Is.EqualTo("0000-00000000"));
				Assert.That(Uuid48.Empty, Is.EqualTo(default(Uuid48)));
				Assert.That(Uuid48.Empty, Is.EqualTo(new Uuid48(0L)));
				Assert.That(Uuid48.Empty.ToUInt64(), Is.EqualTo(0));
				Assert.That(Uuid48.Empty.ToInt64(), Is.EqualTo(0));
				Assert.That(Uuid48.Empty.ToByteArray(), Is.EqualTo(new byte[6]));
				Assert.That(Uuid48.Empty.ToSlice(), Is.EqualTo(Slice.Zero(6)));

				Assert.That(Uuid48.Empty.ToUInt64(), Is.EqualTo(0));
				Assert.That(Uuid48.Empty.ToInt64(), Is.EqualTo(0));
				Assert.That(Uuid48.Empty.Lower16, Is.EqualTo(0));
				Assert.That(Uuid48.Empty.Lower32, Is.EqualTo(0));
				Assert.That(Uuid48.Empty.Upper16, Is.EqualTo(0));
				Assert.That(Uuid48.Empty.Upper32, Is.EqualTo(0));

				var (a, b, c) = Uuid48.Empty;
				Assert.That(a, Is.Zero);
				Assert.That(b, Is.Zero);
				Assert.That(c, Is.Zero);
			});
		}

		[Test]
		public void Test_Uuid48_MaxValue()
		{
			Assert.Multiple(() =>
			{
				Assert.That(Uuid48.MaxValue.ToString(), Is.EqualTo("FFFF-FFFFFFFF"));
				Assert.That(Uuid48.MaxValue, Is.EqualTo(new Uuid48((1UL << 48) - 1)));
				Assert.That(Uuid48.MaxValue.ToUInt64(), Is.EqualTo((1UL << 48) - 1));
				Assert.That(Uuid48.MaxValue.ToInt64(), Is.EqualTo((1L << 48) - 1));
				Assert.That(Uuid48.MaxValue.ToByteArray(), Is.EqualTo(new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff }));
				Assert.That(Uuid48.MaxValue.ToSlice(), Is.EqualTo(Slice.Repeat(0xFF, 6)));

				Assert.That(Uuid48.MaxValue.ToUInt64(), Is.EqualTo(0xFFFF_FFFFFFFF));
				Assert.That(Uuid48.MaxValue.ToInt64(), Is.EqualTo(0xFFFF_FFFFFFFF));
				Assert.That(Uuid48.MaxValue.Lower16, Is.EqualTo(0xFFFF));
				Assert.That(Uuid48.MaxValue.Lower32, Is.EqualTo(0xFFFFFFFF));
				Assert.That(Uuid48.MaxValue.Upper16, Is.EqualTo(0xFFFF));
				Assert.That(Uuid48.MaxValue.Upper32, Is.EqualTo(0xFFFFFFFF));

				var (a, b, c) = Uuid48.MaxValue;
				Assert.That(a, Is.EqualTo(0xFFFF));
				Assert.That(b, Is.EqualTo(0xFFFF));
				Assert.That(c, Is.EqualTo(0xFFFF));
			});
		}

		[Test]
		public void Test_Uuid48_One()
		{
			Assert.That(Uuid48.One, Is.EqualTo(new Uuid48(1)));
			Assert.That(Uuid48.One.ToString(), Is.EqualTo("0000-00000001"));
			Assert.That(Uuid48.One.ToUInt64(), Is.EqualTo(1));
			Assert.That(Uuid48.One.ToInt64(), Is.EqualTo(1));
			Assert.That(Uuid48.One.ToByteArray(), Is.EqualTo(new byte[] { 0, 0, 0, 0, 0, 1 }));

			Assert.That(Uuid48.One, Is.GreaterThan(Uuid48.MinValue));
			Assert.That(Uuid48.One, Is.LessThan(new Uuid48(2)));
		}

		[Test]
		public void Test_Uuid48_NonZero()
		{
			Assert.Multiple(() =>
			{
				var guid = new Uuid48(0x123456789ABC);

				Assert.That(guid.ToString(), Is.EqualTo("1234-56789ABC"));
				Assert.That(guid, Is.EqualTo(new Uuid48(0x1234, 0x56789ABC)));
				Assert.That(guid.ToUInt64(), Is.EqualTo(0x123456789ABC));
				Assert.That(guid.ToInt64(), Is.EqualTo(0x123456789ABC));
				Assert.That(guid.ToByteArray(), Is.EqualTo(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9a, 0xbc }));
				Assert.That(guid.ToSlice(), Is.EqualTo(Slice.FromBytes([ 0x12, 0x34, 0x56, 0x78, 0x9a, 0xbc ])));

				Assert.That(guid.ToUInt64(), Is.EqualTo(0x123456789ABC));
				Assert.That(guid.ToInt64(), Is.EqualTo(0x123456789ABC));
				Assert.That(guid.Lower16, Is.EqualTo(0x9ABC));
				Assert.That(guid.Lower32, Is.EqualTo(0x56789ABC));
				Assert.That(guid.Upper16, Is.EqualTo(0x1234));
				Assert.That(guid.Upper32, Is.EqualTo(0x12345678));

				var (a, b, c) = guid;
				Assert.That(a, Is.EqualTo(0x1234));
				Assert.That(b, Is.EqualTo(0x5678));
				Assert.That(c, Is.EqualTo(0x9ABC));
			});
		}

		[Test]
		public void Test_Uuid48_Casting()
		{
			// explicit
			Uuid48 a = (Uuid48) 0L;
			Uuid48 b = (Uuid48) 42L;
			Uuid48 c = (Uuid48) 0x0000_12345678ul;
			Uuid48 d = (Uuid48) 0x1234_56789ABCu;
			Uuid48 e = (Uuid48) 0xFFFF_FFFFFFFFul;

			// ToUInt64
			Assert.That(a.ToUInt64(), Is.EqualTo(0UL));
			Assert.That(b.ToUInt64(), Is.EqualTo(42UL));
			Assert.That(c.ToUInt64(), Is.EqualTo(305_419_896UL));
			Assert.That(d.ToUInt64(), Is.EqualTo(20_015_998_343_868UL));
			Assert.That(e.ToUInt64(), Is.EqualTo(281_474_976_710_655UL));

			// ToInt64
			Assert.That(a.ToInt64(), Is.EqualTo(0L));
			Assert.That(b.ToInt64(), Is.EqualTo(42L));
			Assert.That(c.ToInt64(), Is.EqualTo(305_419_896L));
			Assert.That(d.ToInt64(), Is.EqualTo(20_015_998_343_868L));
			Assert.That(e.ToInt64(), Is.EqualTo(281_474_976_710_655L));

			// explicit
			Assert.That((long) a, Is.EqualTo(0L));
			Assert.That((long) b, Is.EqualTo(42L));
			Assert.That((long) c, Is.EqualTo(305_419_896L));
			Assert.That((ulong) d, Is.EqualTo(20_015_998_343_868UL));
			Assert.That((ulong) e, Is.EqualTo(281_474_976_710_655UL));
			Assert.That((long) e, Is.EqualTo(281_474_976_710_655L));
		}

		[Test]
		public void Test_Uuid48_ToString()
		{
			Assert.Multiple(() =>
			{
				var guid = Uuid48.Empty;
				Assert.That(guid.ToUInt64(), Is.EqualTo(0));
				Assert.That(guid.ToString(), Is.EqualTo("0000-00000000"));
				Assert.That(guid.ToString("X"), Is.EqualTo("000000000000"));
				Assert.That(guid.ToString("x"), Is.EqualTo("000000000000"));
				Assert.That(guid.ToString("B"), Is.EqualTo("{0000-00000000}"));
				Assert.That(guid.ToString("b"), Is.EqualTo("{0000-00000000}"));
				Assert.That(guid.ToString("C"), Is.EqualTo("0"));
			});

			Assert.Multiple(() =>
			{
				var guid = new Uuid48(0x1234_56789ABC);
				Assert.That(guid.ToUInt64(), Is.EqualTo(0x1234_56789ABC));
				Assert.That(guid.ToString(), Is.EqualTo("1234-56789ABC"));
				Assert.That(guid.ToString("D"), Is.EqualTo("1234-56789ABC"));
				Assert.That(guid.ToString("d"), Is.EqualTo("1234-56789abc"));
				Assert.That(guid.ToString("N"), Is.EqualTo("123456789ABC"));
				Assert.That(guid.ToString("n"), Is.EqualTo("123456789abc"));
				Assert.That(guid.ToString("X"), Is.EqualTo("123456789ABC"));
				Assert.That(guid.ToString("x"), Is.EqualTo("123456789abc"));
				Assert.That(guid.ToString("B"), Is.EqualTo("{1234-56789ABC}"));
				Assert.That(guid.ToString("b"), Is.EqualTo("{1234-56789abc}"));
				Assert.That(guid.ToString("V"), Is.EqualTo("12-34-56-78-9A-BC"));
				Assert.That(guid.ToString("v"), Is.EqualTo("12-34-56-78-9a-bc"));
				Assert.That(guid.ToString("M"), Is.EqualTo("12:34:56:78:9A:BC"));
				Assert.That(guid.ToString("m"), Is.EqualTo("12:34:56:78:9a:bc"));
				Assert.That(guid.ToString("C"), Is.EqualTo("5gOMDDhc"));
				Assert.That(guid.ToString("c"), Is.EqualTo("5gOMDDhc"));
				Assert.That(guid.ToString("Z"), Is.EqualTo("05gOMDDhc"));
				Assert.That(guid.ToString("z"), Is.EqualTo("05gOMDDhc"));
				Assert.That(guid.ToString("R"), Is.EqualTo("20015998343868")); // 20015998343868 = 0x123456789ABC
				Assert.That(guid.ToString("r"), Is.EqualTo("20015998343868"));
			});

			Assert.Multiple(() =>
			{
				var guid = new Uuid48(0x0000, 0x12345678);
				Assert.That(guid.ToUInt64(), Is.EqualTo(0x12345678));
				Assert.That(guid.ToString(), Is.EqualTo("0000-12345678"));
				Assert.That(guid.ToString("D"), Is.EqualTo("0000-12345678"));
				Assert.That(guid.ToString("d"), Is.EqualTo("0000-12345678"));
				Assert.That(guid.ToString("X"), Is.EqualTo("000012345678"));
				Assert.That(guid.ToString("x"), Is.EqualTo("000012345678"));
				Assert.That(guid.ToString("B"), Is.EqualTo("{0000-12345678}"));
				Assert.That(guid.ToString("b"), Is.EqualTo("{0000-12345678}"));
				Assert.That(guid.ToString("C"), Is.EqualTo("KfVfM"));
			});

			Assert.Multiple(() =>
			{
				var guid = Uuid48.MaxValue;
				Assert.That(guid.ToUInt64(), Is.EqualTo(0xFFFF_FFFFFFFF));
				Assert.That(guid.ToString(), Is.EqualTo("FFFF-FFFFFFFF"));
				Assert.That(guid.ToString("X"), Is.EqualTo("FFFFFFFFFFFF"));
				Assert.That(guid.ToString("x"), Is.EqualTo("ffffffffffff"));
				Assert.That(guid.ToString("B"), Is.EqualTo("{FFFF-FFFFFFFF}"));
				Assert.That(guid.ToString("b"), Is.EqualTo("{ffff-ffffffff}"));
				Assert.That(guid.ToString("C"), Is.EqualTo("1HvWXNAa7"));
			});
		}

		[Test]
		public void Test_Uuid48_TryFormat()
		{
			Assert.Multiple(() =>
			{
				var guid = new Uuid48(0x1234_56789ABC);
				Assert.That($"value: `{guid}`", Is.EqualTo("value: `1234-56789ABC`"));
				Assert.That($"value: `{guid:D}`", Is.EqualTo("value: `1234-56789ABC`"));
				Assert.That($"value: `{guid:d}`", Is.EqualTo("value: `1234-56789abc`"));
				Assert.That($"value: `{guid:N}`", Is.EqualTo("value: `123456789ABC`"));
				Assert.That($"value: `{guid:n}`", Is.EqualTo("value: `123456789abc`"));
				Assert.That($"value: `{guid:X}`", Is.EqualTo("value: `123456789ABC`"));
				Assert.That($"value: `{guid:x}`", Is.EqualTo("value: `123456789abc`"));
				Assert.That($"value: `{guid:B}`", Is.EqualTo("value: `{1234-56789ABC}`"));
				Assert.That($"value: `{guid:b}`", Is.EqualTo("value: `{1234-56789abc}`"));
				Assert.That($"value: `{guid:V}`", Is.EqualTo("value: `12-34-56-78-9A-BC`"));
				Assert.That($"value: `{guid:v}`", Is.EqualTo("value: `12-34-56-78-9a-bc`"));
				Assert.That($"value: `{guid:M}`", Is.EqualTo("value: `12:34:56:78:9A:BC`"));
				Assert.That($"value: `{guid:m}`", Is.EqualTo("value: `12:34:56:78:9a:bc`"));
				Assert.That($"value: `{guid:C}`", Is.EqualTo("value: `5gOMDDhc`"));
				Assert.That($"value: `{guid:c}`", Is.EqualTo("value: `5gOMDDhc`"));
				Assert.That($"value: `{guid:Z}`", Is.EqualTo("value: `05gOMDDhc`"));
				Assert.That($"value: `{guid:z}`", Is.EqualTo("value: `05gOMDDhc`"));
				Assert.That($"value: `{guid:R}`", Is.EqualTo("value: `20015998343868`")); // 20015998343868 = 0x123456789ABC
				Assert.That($"value: `{guid:r}`", Is.EqualTo("value: `20015998343868`"));
			});
		}

		[Test]
		public void Test_Uuid48_Parse_Hexa16()
		{
			static void CheckSuccess(string literal, ulong expected)
			{
				// string
				Assert.That(Uuid48.Parse(literal).ToUInt64(), Is.EqualTo(expected), $"Should parse: {literal}");
				Assert.That(Uuid48.Parse(literal, CultureInfo.InvariantCulture).ToUInt64(), Is.EqualTo(expected), $"Should parse: {literal}");
				Assert.That(Uuid48.Parse(literal.ToUpperInvariant()).ToUInt64(), Is.EqualTo(expected), $"Should be case-insensitive: {literal}");
				Assert.That(Uuid48.Parse(literal.ToUpperInvariant()).ToUInt64(), Is.EqualTo(expected), $"Should be case-insensitive: {literal}");
				Assert.That(Uuid48.TryParse(literal, out var res), Is.True, $"Should parse: {literal}");
				Assert.That(res.ToUInt64(), Is.EqualTo(expected), $"Should parse: {literal}");
				Assert.That(Uuid48.TryParse(literal, CultureInfo.InvariantCulture, out res), Is.True, $"Should parse: {literal}");
				Assert.That(res.ToUInt64(), Is.EqualTo(expected), $"Should parse: {literal}");
				// ReadOnlySpan<char>
				Assert.That(Uuid48.Parse(literal.AsSpan()).ToUInt64(), Is.EqualTo(expected), $"Should parse: {literal}");
				Assert.That(Uuid48.Parse(literal.AsSpan(), CultureInfo.InvariantCulture).ToUInt64(), Is.EqualTo(expected), $"Should parse: {literal}");
				Assert.That(Uuid48.Parse(literal.ToUpperInvariant().AsSpan()).ToUInt64(), Is.EqualTo(expected), $"Should be case-insensitive: {literal}");
				Assert.That(Uuid48.Parse(literal.ToUpperInvariant().AsSpan(), CultureInfo.InvariantCulture).ToUInt64(), Is.EqualTo(expected), $"Should be case-insensitive: {literal}");
				Assert.That(Uuid48.TryParse(literal.AsSpan(), out res), Is.True, $"Should parse: {literal}");
				Assert.That(res.ToUInt64(), Is.EqualTo(expected), $"Should parse: {literal}");
				Assert.That(Uuid48.TryParse(literal.AsSpan(), CultureInfo.InvariantCulture, out res), Is.True, $"Should parse: {literal}");
				Assert.That(res.ToUInt64(), Is.EqualTo(expected), $"Should parse: {literal}");
			}

			CheckSuccess("000000000000", 0);
			CheckSuccess("0123456789ab", 0x0123_456789ABUL);
			CheckSuccess("  0123456789ab", 0x0123_456789ABUL); // leading white spaces are ignored
			CheckSuccess("0123456789ab\r\n", 0x0123_456789ABUL); // trailing white spaces are ignored
			CheckSuccess("{0000-00000000}", 0);
			CheckSuccess("{0123-456789ab}", 0x0123_456789ABUL);
			CheckSuccess("\t{0123-456789ab}\r\n", 0x0123_456789ABUL); // white spaces are ignored
			CheckSuccess("{000000000000}", 0);
			CheckSuccess("{0123456789ab}", 0x01234_56789ABUL);
			CheckSuccess("0000-00000000", 0);
			CheckSuccess("0123-456789ab", 0x0123_456789ABUL);
			CheckSuccess("0000-deadbeef", 0x0000_DEADBEEFUL);
			CheckSuccess("dead-beef0000", 0xDEAD_BEEF0000UL);

			static void CheckFailure(string? literal, string message)
			{
				if (literal == null)
				{
					Assert.That(() => Uuid48.Parse(null!), Throws.ArgumentNullException, message);
					Assert.That(() => Uuid48.Parse(null!, CultureInfo.InvariantCulture), Throws.ArgumentNullException, message);
				}
				else
				{
					Assert.That(() => Uuid48.Parse(literal), Throws.InstanceOf<FormatException>(), message);
					Assert.That(() => Uuid48.Parse(literal, CultureInfo.InvariantCulture), Throws.InstanceOf<FormatException>(), message);
					Assert.That(() => Uuid48.Parse(literal.AsSpan()), Throws.InstanceOf<FormatException>(), message);
					Assert.That(() => Uuid48.Parse(literal.AsSpan(), CultureInfo.InvariantCulture), Throws.InstanceOf<FormatException>(), message);

					Assert.That(Uuid48.TryParse(literal, out var res), Is.False, message);
					Assert.That(res, Is.EqualTo(default(Uuid48)), message);
					Assert.That(Uuid48.TryParse(literal, CultureInfo.InvariantCulture, out res), Is.False, message);
					Assert.That(res, Is.EqualTo(default(Uuid48)), message);

					Assert.That(Uuid48.TryParse(literal.AsSpan(), out res), Is.False, message);
					Assert.That(res, Is.EqualTo(default(Uuid48)), message);
					Assert.That(Uuid48.TryParse(literal.AsSpan(), CultureInfo.InvariantCulture, out res), Is.False, message);
					Assert.That(res, Is.EqualTo(default(Uuid48)), message);
				}
			}

			CheckFailure(default, "Null string is invalid");
			CheckFailure("", "Only whitespaces");
			CheckFailure("  ", "Only whitespaces");
			CheckFailure("hello", "random text string");
			CheckFailure("hello 1234-56789abc", "random text prefix");
			CheckFailure("1234-56789abc world", "random text suffix");
			CheckFailure("1234-56789abcg", "Invalid hexa character 'g'");
			CheckFailure("0000-0000000", "Too short");
			CheckFailure("0000-0000000 ", "Too short + extra space");
			CheckFailure("zzzz-zzzzzzzz", "Invalid char");
			CheckFailure("1234-56789ab", "Missing last char");
			CheckFailure("12345-6789abc", "'-' at invalid position");
			CheckFailure("123-456789abc", "'-' at invalid position");
		}

		[Test]
		public void Test_Uuid48_ToString_Base62()
		{
			ReadOnlySpan<char> chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
			Assert.That(chars.Length, Is.EqualTo(62));

			// single digit
			for (int i = 0; i < 62;i++)
			{
				Assert.That(new Uuid48(i).ToString("C"), Is.EqualTo(chars[i].ToString()));
				Assert.That(new Uuid48(i).ToString("Z"), Is.EqualTo("00000000" + chars[i]));
			}

			// two digits
			for (int j = 1; j < 62; j++)
			{
				var prefix = chars[j].ToString();
				for (int i = 0; i < 62; i++)
				{
					Assert.That(new Uuid48(j * 62 + i).ToString("C"), Is.EqualTo(prefix + chars[i]));
					Assert.That(new Uuid48(j * 62 + i).ToString("Z"), Is.EqualTo("0000000" + prefix + chars[i]));
				}
			}

			// 4 digits
			var rnd = new Random();
			for (int i = 0; i < 100_000; i++)
			{
				var a = rnd.Next(2) == 0 ? 0 : rnd.Next(62);
				var b = rnd.Next(2) == 0 ? 0 : rnd.Next(62);
				var c = rnd.Next(2) == 0 ? 0 : rnd.Next(62);
				var d = rnd.Next(62);

				ulong x = (ulong)a;
				x += 62 * (ulong)b;
				x += 62 * 62 * (ulong)c;
				x += 62 * 62 * 62 * (ulong)d;
				var uuid = new Uuid48(x);

				// no padding
				string expected =
					d > 0 ? ("" + chars[d] + chars[c] + chars[b] + chars[a]) :
					c > 0 ? ("" + chars[c] + chars[b] + chars[a]) :
					b > 0 ? ("" + chars[b] + chars[a]) :
					("" + chars[a]);
				Assert.That(uuid.ToString("C"), Is.EqualTo(expected));

				// padding
				Assert.That(uuid.ToString("Z"), Is.EqualTo("00000" + chars[d] + chars[c] + chars[b] + chars[a]));
			}

			// Numbers of the form 62^n should be encoded as '1' followed by n x '0', for n from 0 to 8
			ulong val = 1;
			for (int i = 0; i <= 8; i++)
			{
				Assert.That(new Uuid48(val).ToString("C"), Is.EqualTo("1" + new string('0', i)), $"62^{i}");
				val *= 62;
			}

			// Numbers of the form 62^n - 1 should be encoded as n x 'z', for n from 1 to 8
			val = 0;
			for (int i = 1; i <= 8; i++)
			{
				val += 61;
				Assert.That(new Uuid48(val).ToString("C"), Is.EqualTo(new string('z', i)), $"62^{i} - 1");
				val *= 62;
			}

			// well known values
			Assert.That(new Uuid48(0xB45B07).ToString("C"), Is.EqualTo("narf"));
			Assert.That(new Uuid48(0xE0D0ED).ToString("C"), Is.EqualTo("zort"));
			Assert.That(new Uuid48(0xDEADBEEF).ToString("C"), Is.EqualTo("44pZgF"));
			Assert.That(new Uuid48(0xDEADBEEF).ToString("Z"), Is.EqualTo("00044pZgF"));
			Assert.That(new Uuid48(0x1234_56789ABCul).ToString("C"), Is.EqualTo("5gOMDDhc"));
			Assert.That(new Uuid48(0x1234_56789ABCul).ToString("Z"), Is.EqualTo("05gOMDDhc"));
			Assert.That(new Uuid48(0xFFFF_FFFFFFFFul).ToString("C"), Is.EqualTo("1HvWXNAa7"));
			Assert.That(new Uuid48(0xFFFF_FFFFFFFFul).ToString("Z"), Is.EqualTo("1HvWXNAa7"));

			Assert.That(new Uuid48(255).ToString("C"), Is.EqualTo("47"));
			Assert.That(new Uuid48(ushort.MaxValue).ToString("C"), Is.EqualTo("H31"));
			Assert.That(new Uuid48(uint.MaxValue).ToString("C"), Is.EqualTo("4gfFC3"));
			Assert.That(new Uuid48(0xFFFF_FFFFFFFE).ToString("C"), Is.EqualTo("1HvWXNAa6"));
			Assert.That(new Uuid48(0xFFFF_FFFFFFFF).ToString("C"), Is.EqualTo("1HvWXNAa7"));
		}

		[Test]
		public void Test_Uuid48_Parse_Base62()
		{

			Assert.That(Uuid48.FromBase62("").ToUInt64(), Is.EqualTo(0));
			Assert.That(Uuid48.FromBase62("0").ToUInt64(), Is.EqualTo(0));
			Assert.That(Uuid48.FromBase62("9").ToUInt64(), Is.EqualTo(9));
			Assert.That(Uuid48.FromBase62("A").ToUInt64(), Is.EqualTo(10));
			Assert.That(Uuid48.FromBase62("Z").ToUInt64(), Is.EqualTo(35));
			Assert.That(Uuid48.FromBase62("a").ToUInt64(), Is.EqualTo(36));
			Assert.That(Uuid48.FromBase62("z").ToUInt64(), Is.EqualTo(61));
			Assert.That(Uuid48.FromBase62("10").ToUInt64(), Is.EqualTo(62));
			Assert.That(Uuid48.FromBase62("zz").ToUInt64(), Is.EqualTo(3843));
			Assert.That(Uuid48.FromBase62("100").ToUInt64(), Is.EqualTo(3844));
			Assert.That(Uuid48.FromBase62("zzzzzzzz").ToUInt64(), Is.EqualTo(218340105584895));
			Assert.That(Uuid48.FromBase62("100000000").ToUInt64(), Is.EqualTo(218340105584896));
			Assert.That(Uuid48.FromBase62("1HvWXNAa7").ToUInt64(), Is.EqualTo((1UL << 48) - 1));

			// well known values

			Assert.That(Uuid48.FromBase62("narf").ToUInt64(), Is.EqualTo(0xB45B07));
			Assert.That(Uuid48.FromBase62("zort").ToUInt64(), Is.EqualTo(0xE0D0ED));
			Assert.That(Uuid48.FromBase62("44pZgF").ToUInt64(), Is.EqualTo(0xDEADBEEF));
			Assert.That(Uuid48.FromBase62("00044pZgF").ToUInt64(), Is.EqualTo(0xDEADBEEF));

			Assert.That(Uuid48.FromBase62("4gfFC3").ToUInt64(), Is.EqualTo(uint.MaxValue));
			Assert.That(Uuid48.FromBase62("0004gfFC3").ToUInt64(), Is.EqualTo(uint.MaxValue));

			// invalid chars
			Assert.That(() => Uuid48.FromBase62("/"), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid48.FromBase62("@"), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid48.FromBase62("["), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid48.FromBase62("`"), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid48.FromBase62("{"), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid48.FromBase62("zaz/"), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid48.FromBase62("z/o&r=g"), Throws.InstanceOf<FormatException>());

			// overflow
			Assert.That(() => Uuid48.FromBase62("zzzzzzzzz"), Throws.InstanceOf<FormatException>(), "62^9 - 1 => OVERFLOW");
			Assert.That(() => Uuid48.FromBase62("1HvWXNAa8"), Throws.InstanceOf<FormatException>(), "ulong.MaxValue + 1 => OVERFLOW");

			// invalid length
			Assert.That(() => Uuid48.FromBase62(default(string)!), Throws.ArgumentNullException);
			Assert.That(() => Uuid48.FromBase62("1000000000"), Throws.InstanceOf<FormatException>(), "62^9 => TOO BIG");

		}

		[Test]
		public void Test_Uuid48_NewUid()
		{
			var a = Uuid48.NewUuid();
			var b = Uuid48.NewUuid();
			Assert.That(a.ToUInt64(), Is.Not.EqualTo(b.ToUInt64()));
			Assert.That(a, Is.Not.EqualTo(b));

			const int N = 10_000;

			//note: this test fails from time to time because of bad luck (

			List<Uuid48> collisions = [ ];
			var uids = new HashSet<ulong>();
			for (int i = 0; i < N; i++)
			{
				var uid = Uuid48.NewUuid();
				if (!uids.Add(uid.ToUInt64()))
				{
					collisions.Add(uid);
					LogError($"!!! collision on {uid}");
				}
				uids.Add(uid.ToUInt64());
			}

			// verify the distribution of 4 bit "digits"
			var occurrences = new int[16];
			foreach (var x in uids)
			{
				var s = x.ToString("X012");
				foreach (var c in s)
				{
					occurrences[c - (c <= '9' ? 48 : 55)]++;
				}
			}

			long total = occurrences.Sum();
			Log($"# Distribution of {total:N0} digits:");
			double[] distrib = occurrences.Select(x => 100.0 * x / total).ToArray();
			for (int i = 0; i < occurrences.Length; i++)
			{
				var p = distrib[i];
#if NET8_0_OR_GREATER
				Log($"  - digit '{i:X}' ({i:B04}) : {occurrences[i],6:N0} : {p,5:N01}% : {new string('#', (int) Math.Round(p))}");
#else
				// the binary format specifier ("B") is .NET 8+
				Log($"  - digit '{i:X}' ({Convert.ToString(i, 2).PadLeft(4, '0')}) : {occurrences[i],6:N0} : {p,5:N01}% : {new string('#', (int) Math.Round(p))}");
#endif
			}

			Log($"> Range from {distrib.Min():N03}% to {distrib.Max():N03}%");

			Assert.That(collisions, Is.Empty, "Found duplicate random Uuid48");
			Assert.That(uids.Count, Is.EqualTo(N));

			// check that this falls in an acceptable range
			Assert.That(distrib, Is.All.GreaterThan(5.75).And.LessThan(6.75), $"Distribution of digits from 0 to F should be 6.25% +/- 0.5%, but smallest is {distrib.Min()} and largest is {distrib.Max()}");
		}

		[Test]
		public void Test_Uuid48_Random()
		{
			// note: this uses a generic random number generator that does not guarantee uniqueness
			var rng = this.Rnd;

			Uuid48 uuid;
			for (int i = 0; i < 10_000; i++)
			{
				uuid = Uuid48.Random(rng);
				Assert.That(uuid.ToUInt64(), Is.GreaterThanOrEqualTo(0).And.LessThan(ulong.MaxValue));

				uuid = Uuid48.Random(rng, 0x1234);
				Assert.That(uuid.ToUInt64(), Is.GreaterThanOrEqualTo(0).And.LessThan(0x1234));

				uuid = Uuid48.Random(rng, 0x123456789);
				Assert.That(uuid.ToUInt64(), Is.GreaterThanOrEqualTo(0).And.LessThan(0x123456789));

				uuid = Uuid48.Random(rng, 0x1_0000_00000000);
				Assert.That(uuid.ToUInt64(), Is.GreaterThanOrEqualTo(0).And.LessThanOrEqualTo(ulong.MaxValue));

				uuid = Uuid48.Random(rng, 0xffff_ffffffff);
				Assert.That(uuid.ToUInt64(), Is.GreaterThanOrEqualTo(0).And.LessThan(ulong.MaxValue));

				uuid = Uuid48.Random(rng, 0xffff_fffffffe);
				Assert.That(uuid.ToUInt64(), Is.GreaterThanOrEqualTo(0).And.LessThan(ulong.MaxValue - 1));

				uuid = Uuid48.Random(rng, 0x1234, 0x5678);
				Assert.That(uuid.ToUInt64(), Is.GreaterThanOrEqualTo(0x1234).And.LessThan(0x5678));
			}
		}

		[Test]
		public void Test_Uuid48RandomGenerator_NewUid()
		{
			// this is expected to generate _distinct_ values, with a very low probability of duplicates
			// => if this test fail with duplicate, maybe you are very (un)lucky. Try to run this test multiple times!

			var gen = Uuid48.RandomGenerator.Default;
			Assert.That(gen, Is.Not.Null);

			var a = gen.NewUuid();
			var b = gen.NewUuid();
			Assert.That(a.ToUInt64(), Is.Not.EqualTo(b.ToUInt64()));
			Assert.That(a, Is.Not.EqualTo(b));

			const int N = 1_000;
			var uids = new HashSet<ulong>();
			for (int i = 0; i < N; i++)
			{
				var uid = gen.NewUuid();
				if (uids.Contains(uid.ToUInt64())) Assert.Fail($"Duplicate Uuid48 generated: {uid}");
				uids.Add(uid.ToUInt64());
			}
			Assert.That(uids.Count, Is.EqualTo(N));
		}

		[Test]
		public void Test_Uuid48_Equality_Check()
		{
			Assert.Multiple(() =>
			{
				var a = new Uuid48(42);
				var b = new Uuid48(42);
				var c = new Uuid48(40) + 2;
				var d = new Uuid48(0xDEADBEEF);

				// Equals(Uuid48)
				Assert.That(a.Equals(a), Is.True, "a == a");
				Assert.That(a.Equals(b), Is.True, "a == b");
				Assert.That(a.Equals(c), Is.True, "a == c");
				Assert.That(a.Equals(d), Is.False, "a != d");

				// == Uuid48
				Assert.That(a == b, Is.True, "a == b");
				Assert.That(a == c, Is.True, "a == c");
				Assert.That(a == d, Is.False, "a != d");

				// != Uuid48
				Assert.That(a != b, Is.False, "a == b");
				Assert.That(a != c, Is.False, "a == c");
				Assert.That(a != d, Is.True, "a != d");

				// Equals(numbers)
				Assert.That(a.Equals(42), Is.True, "a == 42");
				Assert.That(a.Equals(42U), Is.True, "a == 42");
				Assert.That(a.Equals(42L), Is.True, "a == 42");
				Assert.That(a.Equals(42UL), Is.True, "a == 42");
				Assert.That(d.Equals(42), Is.False, "d != 42");
				Assert.That(d.Equals(42U), Is.False, "d != 42");
				Assert.That(d.Equals(42L), Is.False, "d != 42");
				Assert.That(d.Equals(42UL), Is.False, "d != 42");

				// == numbers
				Assert.That(a == 42, Is.True, "a == 42");
				Assert.That(a == 42U, Is.True, "a == 42");
				Assert.That(a == 42L, Is.True, "a == 42");
				Assert.That(a == 42UL, Is.True, "a == 42");
				Assert.That(d == 42, Is.False, "d != 42");
				Assert.That(d == 42U, Is.False, "d != 42");
				Assert.That(d == 42L, Is.False, "d != 42");
				Assert.That(d == 42UL, Is.False, "d != 42");

				// != numbers
				Assert.That(a != 42, Is.False, "a == 42");
				Assert.That(a != 42U, Is.False, "a == 42");
				Assert.That(a != 42L, Is.False, "a == 42");
				Assert.That(a != 42UL, Is.False, "a == 42");
				Assert.That(d != 42, Is.True, "d != 42");
				Assert.That(d != 42U, Is.True, "d != 42");
				Assert.That(d != 42L, Is.True, "d != 42");
				Assert.That(d != 42UL, Is.True, "d != 42");

				// Equals(Slice)
				Assert.That(a.Equals(a.ToSlice()), Is.True);
				Assert.That(d.Equals(a.ToSlice()), Is.False);
				Assert.That(d.Equals(d.ToSlice()), Is.True);

				// Equals(ReadOnlySpan<byte>)
				Assert.That(a.Equals(a.ToSlice().Span), Is.True);
				Assert.That(d.Equals(a.ToSlice().Span), Is.False);
				Assert.That(d.Equals(d.ToSlice().Span), Is.True);

				// Equals(object)
				Assert.That(a.Equals((object?) a), Is.True, "a == a");
				Assert.That(a.Equals((object?) b), Is.True, "a == b");
				Assert.That(a.Equals((object?) c), Is.True, "a == c");
				Assert.That(a.Equals((object?) d), Is.False, "a != d");
				Assert.That(a.Equals((object?) 42), Is.True, "a == 42");
				Assert.That(a.Equals((object?) 42U), Is.True, "a == 42");
				Assert.That(a.Equals((object?) 42L), Is.True, "a == 42");
				Assert.That(a.Equals((object?) 42UL), Is.True, "a == 42");
				Assert.That(d.Equals((object?) 42), Is.False, "d != 42");
				Assert.That(d.Equals((object?) 42U), Is.False, "d != 42");
				Assert.That(d.Equals((object?) 42L), Is.False, "d != 42");
				Assert.That(d.Equals((object?) 42UL), Is.False, "d != 42");
			});
		}

		[Test]
		public void Test_Uuid48_Ordering()
		{
			var a = new Uuid48(42);
			var a2 = new Uuid48(42);
			var b = new Uuid48(77);

			Assert.That(a.CompareTo(a), Is.EqualTo(0));
			Assert.That(a.CompareTo(b), Is.EqualTo(-1));
			Assert.That(b.CompareTo(a), Is.EqualTo(+1));

			Assert.That(a < b, Is.True, "a < b");
			Assert.That(a <= b, Is.True, "a <= b");
			Assert.That(a < a2, Is.False, "a < a");
			Assert.That(a <= a2, Is.True, "a <= a");

			Assert.That(a > b, Is.False, "a > b");
			Assert.That(a >= b, Is.False, "a >= b");
			Assert.That(a > a2, Is.False, "a > a");
			Assert.That(a >= a2, Is.True, "a >= a");

			// parsed from string
			Assert.That(Uuid48.Parse("137b-0c8873a2") < Uuid48.Parse("604b-2512b4ad"), Is.True);
			Assert.That(Uuid48.Parse("d8f1-82adb1a4") < Uuid48.Parse("22ab-1b2c1db0"), Is.False);
			Assert.That(Uuid48.Parse("{137b-0c8873a2}") > Uuid48.Parse("{604b-2512b4ad}"), Is.False);
			Assert.That(Uuid48.Parse("{d8f1-82adb1a4}") > Uuid48.Parse("{22ab-1b2c1db0}"), Is.True);
			Assert.That(Uuid48.FromBase62("0VCTjiXVp") < Uuid48.FromBase62("110UnyZ1Q"), Is.True);
			Assert.That(Uuid48.FromBase62("1258JY2RS") > Uuid48.FromBase62("0XPaNaEUc"), Is.True);

			// verify byte ordering
			var c = new Uuid48(0x000100000002);
			var d = new Uuid48(0x000200000001);
			Assert.That(c.CompareTo(d), Is.EqualTo(-1));
			Assert.That(d.CompareTo(c), Is.EqualTo(+1));

			// verify that we can sort an array of Uuid48
			var uids = new Uuid48[100];
			for (int i = 0; i < uids.Length; i++)
			{
				uids[i] = Uuid48.NewUuid();
			}
			Assume.That(uids, Is.Not.Ordered, "This can happen with a very small probability. Please try again");
			Array.Sort(uids);
			Assert.That(uids, Is.Ordered);

			// ordering should be preserved in integer or textual form

			Assert.That(uids.Select(x => x.ToUInt64()), Is.Ordered, "order should be preserved when ordering by unsigned value");
			//note: ToInt64() will not work because of negative values
			Assert.That(uids.Select(x => x.ToString()), Is.Ordered.Using<string>(StringComparer.Ordinal), "order should be preserved when ordering by text (hexa)");
			Assert.That(uids.Select(x => x.ToString("Z")), Is.Ordered.Using<string>(StringComparer.Ordinal), "order should be preserved when ordering by text (base62)");
			//note: ToString("C") will not work for ordering because it will produce "z" > "aa", instead of expected "0z" < "aa"
		}

		[Test]
		public void Test_Uuid48_Arithmetic()
		{
			{
				var uid = Uuid48.Empty;

				Assert.That(uid + 42L, Is.EqualTo(new Uuid48(42)));
				Assert.That(uid + 42UL, Is.EqualTo(new Uuid48(42)));
				uid++;
				Assert.That(uid.ToInt64(), Is.EqualTo(1));
				uid++;
				Assert.That(uid.ToInt64(), Is.EqualTo(2));
				uid--;
				Assert.That(uid.ToInt64(), Is.EqualTo(1));
				uid--;
				Assert.That(uid.ToInt64(), Is.Zero);
			}

			Assert.Multiple(() =>
			{
				var uid = Uuid48.NewUuid();
				Assert.That(uid + 123, Is.EqualTo(new Uuid48(uid.ToInt64() + 123)));
				Assert.That(uid + 123U, Is.EqualTo(new Uuid48(uid.ToUInt64() + 123)));
				Assert.That(uid + 123L, Is.EqualTo(new Uuid48(uid.ToInt64() + 123)));
				Assert.That(uid + 123UL, Is.EqualTo(new Uuid48(uid.ToUInt64() + 123)));

				Assert.That(uid - 123, Is.EqualTo(new Uuid48(uid.ToInt64() - 123)));
				Assert.That(uid - 123U, Is.EqualTo(new Uuid48(uid.ToUInt64() - 123)));
				Assert.That(uid - 123L, Is.EqualTo(new Uuid48(uid.ToInt64() - 123)));
				Assert.That(uid - 123UL, Is.EqualTo(new Uuid48(uid.ToUInt64() - 123)));
			});

			Assert.Multiple(() =>
			{ // checked vs unchecked

				Assert.That(unchecked(Uuid48.MaxValue + 1), Is.EqualTo(Uuid48.MinValue));
				Assert.That(unchecked(Uuid48.MaxValue + 1U), Is.EqualTo(Uuid48.MinValue));
				Assert.That(unchecked(Uuid48.MaxValue + 1L), Is.EqualTo(Uuid48.MinValue));
				Assert.That(unchecked(Uuid48.MaxValue + 1UL), Is.EqualTo(Uuid48.MinValue));
				Assert.That(() => checked(Uuid48.MaxValue + 1), Throws.InstanceOf<OverflowException>());
				Assert.That(() => checked(Uuid48.MaxValue + 1U), Throws.InstanceOf<OverflowException>());
				Assert.That(() => checked(Uuid48.MaxValue + 1L), Throws.InstanceOf<OverflowException>());
				Assert.That(() => checked(Uuid48.MaxValue + 1UL), Throws.InstanceOf<OverflowException>());

				Assert.That(unchecked(Uuid48.MinValue - 1), Is.EqualTo(Uuid48.AllBitsSet));
				Assert.That(unchecked(Uuid48.MinValue - 1U), Is.EqualTo(Uuid48.AllBitsSet));
				Assert.That(unchecked(Uuid48.MinValue - 1L), Is.EqualTo(Uuid48.AllBitsSet));
				Assert.That(unchecked(Uuid48.MinValue - 1UL), Is.EqualTo(Uuid48.AllBitsSet));
				Assert.That(() => checked(Uuid48.MinValue - 1), Throws.InstanceOf<OverflowException>());
				Assert.That(() => checked(Uuid48.MinValue - 1U), Throws.InstanceOf<OverflowException>());
				Assert.That(() => checked(Uuid48.MinValue - 1L), Throws.InstanceOf<OverflowException>());
				Assert.That(() => checked(Uuid48.MinValue - 1UL), Throws.InstanceOf<OverflowException>());
			});

			Assert.Multiple(() =>
			{
				var uid = Uuid48.NewUuid();
				Assert.That(Uuid48.Empty & Uuid48.Empty, Is.EqualTo(Uuid48.Empty));
				Assert.That(Uuid48.Empty & Uuid48.AllBitsSet, Is.EqualTo(Uuid48.Empty));
				Assert.That(Uuid48.AllBitsSet & Uuid48.Empty, Is.EqualTo(Uuid48.Empty));
				Assert.That(Uuid48.AllBitsSet & Uuid48.AllBitsSet, Is.EqualTo(Uuid48.AllBitsSet));

				Assert.That(Uuid48.Empty | Uuid48.Empty, Is.EqualTo(Uuid48.Empty));
				Assert.That(Uuid48.Empty | Uuid48.AllBitsSet, Is.EqualTo(Uuid48.AllBitsSet));
				Assert.That(Uuid48.AllBitsSet | Uuid48.Empty, Is.EqualTo(Uuid48.AllBitsSet));
				Assert.That(Uuid48.AllBitsSet | Uuid48.AllBitsSet, Is.EqualTo(Uuid48.AllBitsSet));

				Assert.That(Uuid48.Empty ^ Uuid48.Empty, Is.EqualTo(Uuid48.Empty));
				Assert.That(Uuid48.Empty ^ Uuid48.AllBitsSet, Is.EqualTo(Uuid48.AllBitsSet));
				Assert.That(Uuid48.AllBitsSet ^ Uuid48.Empty, Is.EqualTo(Uuid48.AllBitsSet));
				Assert.That(Uuid48.AllBitsSet ^ Uuid48.AllBitsSet, Is.EqualTo(Uuid48.Empty));

				Assert.That(uid & Uuid48.Empty, Is.EqualTo(Uuid48.Empty));
				Assert.That(uid & uid, Is.EqualTo(uid));
				Assert.That(uid & Uuid48.AllBitsSet, Is.EqualTo(uid));

				Assert.That(uid | Uuid48.Empty, Is.EqualTo(uid));
				Assert.That(uid | uid, Is.EqualTo(uid));
				Assert.That(uid | Uuid48.AllBitsSet, Is.EqualTo(Uuid48.AllBitsSet));

				Assert.That(uid ^ Uuid48.Empty, Is.EqualTo(uid));
				Assert.That(uid ^ uid, Is.EqualTo(Uuid48.Empty));
				Assert.That(uid ^ Uuid48.AllBitsSet, Is.EqualTo(Uuid48.FromLower48(~uid.ToUInt64())));

				Assert.That(uid & Uuid48.FromInt64(0xFFFF), Is.EqualTo(Uuid48.FromUpper32Lower16(0, uid.Lower16)));
				Assert.That(uid & Uuid48.FromInt64(0xFFFFFFFF), Is.EqualTo(Uuid48.FromUpper16Lower32(0, uid.Lower32)));
				Assert.That(uid | Uuid48.FromInt64(0xFFFF), Is.EqualTo(Uuid48.FromUpper32Lower16(uid.Upper32, ushort.MaxValue)));
				Assert.That(uid | Uuid48.FromInt64(0xFFFFFFFF), Is.EqualTo(Uuid48.FromUpper16Lower32(uid.Upper16, uint.MaxValue)));
			});
		}

		[Test]
		public void Test_Uuid48_Read_From_Bytes()
		{
			// test buffer with included padding
			byte[] buf = [ 0x55, 0x55, 0x55, 0x55, /* start */ 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, /* stop */ 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA ];
			Uuid48 original = Uuid48.Parse("0123-456789AB");
			Assume.That(original.ToUInt64(), Is.EqualTo(0x0123456789AB));
			Uuid48 res;

			Assert.Multiple(() =>
			{
				// ReadOnlySpan<byte>
				Assert.That(Uuid48.Read(buf.AsSpan(4, 6)), Is.EqualTo(original));
				Assert.That(() => Uuid48.Read(buf.AsSpan(4, 5)), Throws.Exception);
				Assert.That(() => Uuid48.Read(buf.AsSpan(4, 4)), Throws.Exception);
				Assert.That(() => Uuid48.Read(buf.AsSpan(4, 3)), Throws.Exception);
				Assert.That(() => Uuid48.Read(buf.AsSpan(4, 2)), Throws.Exception);
				Assert.That(() => Uuid48.Read(buf.AsSpan(4, 1)), Throws.Exception);
				Assert.That(Uuid48.Read(buf.AsSpan(4, 0)), Is.EqualTo(Uuid48.Empty));

				Assert.That(Uuid48.TryRead(buf.AsSpan(4, 6), out res), Is.True.WithOutput(res).EqualTo(original));
				Assert.That(() => Uuid48.TryRead(buf.AsSpan(4, 5), out res), Is.False);
				Assert.That(() => Uuid48.TryRead(buf.AsSpan(4, 4), out res), Is.False);
				Assert.That(() => Uuid48.TryRead(buf.AsSpan(4, 3), out res), Is.False);
				Assert.That(() => Uuid48.TryRead(buf.AsSpan(4, 2), out res), Is.False);
				Assert.That(() => Uuid48.TryRead(buf.AsSpan(4, 1), out res), Is.False);
				Assert.That(Uuid48.TryRead(buf.AsSpan(4, 0), out res), Is.True.WithOutput(res).EqualTo(Uuid48.Empty));

				// Slice
				Assert.That(Uuid48.Read(buf.AsSlice(4, 6)), Is.EqualTo(original));
				Assert.That(() => Uuid48.Read(buf.AsSlice(4, 5)), Throws.Exception);
				Assert.That(() => Uuid48.Read(buf.AsSlice(4, 4)), Throws.Exception);
				Assert.That(() => Uuid48.Read(buf.AsSlice(4, 3)), Throws.Exception);
				Assert.That(() => Uuid48.Read(buf.AsSlice(4, 2)), Throws.Exception);
				Assert.That(() => Uuid48.Read(buf.AsSlice(4, 1)), Throws.Exception);
				Assert.That(Uuid48.Read(buf.AsSlice(4, 0)), Is.EqualTo(Uuid48.Empty));

				Assert.That(Uuid48.TryRead(buf.AsSlice(4, 6), out res), Is.True.WithOutput(res).EqualTo(original));
				Assert.That(() => Uuid48.TryRead(buf.AsSlice(4, 5), out res), Is.False);
				Assert.That(() => Uuid48.TryRead(buf.AsSlice(4, 4), out res), Is.False);
				Assert.That(() => Uuid48.TryRead(buf.AsSlice(4, 3), out res), Is.False);
				Assert.That(() => Uuid48.TryRead(buf.AsSlice(4, 2), out res), Is.False);
				Assert.That(() => Uuid48.TryRead(buf.AsSlice(4, 1), out res), Is.False);
				Assert.That(Uuid48.TryRead(buf.AsSlice(4, 0), out res), Is.True.WithOutput(res).EqualTo(Uuid48.Empty));

				// byte[]
				Assert.That(Uuid48.Read(buf.AsSpan(4, 6).ToArray()), Is.EqualTo(original));
				Assert.That(() => Uuid48.Read(buf.AsSpan(4, 5).ToArray()), Throws.Exception);
				Assert.That(() => Uuid48.Read(buf.AsSpan(4, 4).ToArray()), Throws.Exception);
				Assert.That(() => Uuid48.Read(buf.AsSpan(4, 3).ToArray()), Throws.Exception);
				Assert.That(() => Uuid48.Read(buf.AsSpan(4, 2).ToArray()), Throws.Exception);
				Assert.That(() => Uuid48.Read(buf.AsSpan(4, 1).ToArray()), Throws.Exception);
				Assert.That(Uuid48.Read(buf.AsSpan(4, 0).ToArray()), Is.EqualTo(Uuid48.Empty));

				Assert.That(Uuid48.TryRead(buf.AsSpan(4, 6).ToArray(), out res), Is.True.WithOutput(res).EqualTo(original));
			});

		}

		[Test]
		public void Test_Uuid48_WriteTo()
		{
			static byte[] Repeat(byte value, int count)
			{
				var tmp = new byte[count];
				tmp.AsSpan().Fill(value);
				return tmp;
			}

			var original = Uuid48.Parse("0123-456789AB");
			Assume.That(original.ToUInt64(), Is.EqualTo(0x0123456789AB));

			// span with more space
			var scratch = Repeat(0xAA, 16);
			original.WriteTo(scratch.AsSpan());
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("01 23 45 67 89 AB AA AA AA AA AA AA AA AA AA AA"));

			// span with no offset and exact size
			scratch = Repeat(0xAA, 16);
			original.WriteTo(scratch.AsSpan(0, 6));
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("01 23 45 67 89 AB AA AA AA AA AA AA AA AA AA AA"));

			// span with offset
			scratch = Repeat(0xAA, 16);
			original.WriteTo(scratch.AsSpan(4));
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("AA AA AA AA 01 23 45 67 89 AB AA AA AA AA AA AA"));

			// span with offset and exact size
			scratch = Repeat(0xAA, 16);
			original.WriteTo(scratch.AsSpan(4, 6));
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("AA AA AA AA 01 23 45 67 89 AB AA AA AA AA AA AA"));

			// errors

			Assert.That(() => original.WriteTo(Span<byte>.Empty), Throws.InstanceOf<ArgumentException>(), "Target buffer is empty");

			scratch = Repeat(0xAA, 16);
			Assert.That(() => original.WriteTo(scratch.AsSpan(0, 5)), Throws.InstanceOf<ArgumentException>(), "Target buffer is too small");
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA"), "Buffer should not have been overwritten!");

		}

		[Test]
		public void Test_Uuid48_TryWriteTo()
		{
			static byte[] Repeat(byte value, int count)
			{
				var tmp = new byte[count];
				tmp.AsSpan().Fill(value);
				return tmp;
			}

			var original = Uuid48.Parse("0123-456789AB");
			Assume.That(original.ToUInt64(), Is.EqualTo(0x0123456789AB));

			// span with more space
			var scratch = Repeat(0xAA, 16);
			Assert.That(original.TryWriteTo(scratch.AsSpan()), Is.True);
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("01 23 45 67 89 AB AA AA AA AA AA AA AA AA AA AA"));

			// span with no offset and exact size
			scratch = Repeat(0xAA, 16);
			Assert.That(original.TryWriteTo(scratch.AsSpan(0, 8)), Is.True);
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("01 23 45 67 89 AB AA AA AA AA AA AA AA AA AA AA"));

			// span with offset
			scratch = Repeat(0xAA, 16);
			Assert.That(original.TryWriteTo(scratch.AsSpan(4)), Is.True);
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("AA AA AA AA 01 23 45 67 89 AB AA AA AA AA AA AA"));

			// span with offset and exact size
			scratch = Repeat(0xAA, 16);
			Assert.That(original.TryWriteTo(scratch.AsSpan(4, 6)), Is.True);
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("AA AA AA AA 01 23 45 67 89 AB AA AA AA AA AA AA"));

			// errors

			Assert.That(original.TryWriteTo(Span<byte>.Empty), Is.False, "Target buffer is empty");

			scratch = Repeat(0xAA, 16);
			Assert.That(original.TryWriteTo(scratch.AsSpan(0, 5)), Is.False, "Target buffer is too small");
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA"), "Buffer should not have been overwritten!");
		}

	}

}
