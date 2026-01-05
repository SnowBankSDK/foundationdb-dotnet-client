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
namespace SnowBank.Core.Tests
{

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class Uuid64Facts : SimpleTest
	{

		[Test]
		public void Test_Uuid64_Empty()
		{
			Assert.That(Uuid64.Empty.ToString(), Is.EqualTo("00000000-00000000"));
			Assert.That(Uuid64.Empty, Is.EqualTo(default(Uuid64)));
			Assert.That(Uuid64.Empty, Is.EqualTo(new Uuid64(0L)));
			Assert.That(Uuid64.Empty.ToUInt64(), Is.EqualTo(0));
			Assert.That(Uuid64.Empty.ToInt64(), Is.EqualTo(0));
			Assert.That(Uuid64.Empty.ToByteArray(), Is.EqualTo(new byte[8]));
			Assert.That(Uuid64.Empty.ToSlice(), Is.EqualTo(Slice.Zero(8)));
			Assert.That(Uuid64.Empty, Is.EqualTo(Uuid64.MinValue));
		}

		[Test]
		public void Test_Uuid64_AllBitsSet()
		{
			Assert.That(Uuid64.AllBitsSet.ToString(), Is.EqualTo("FFFFFFFF-FFFFFFFF"));
			Assert.That(Uuid64.AllBitsSet, Is.EqualTo(new Uuid64(ulong.MaxValue)));
			Assert.That(Uuid64.AllBitsSet.ToUInt64(), Is.EqualTo(ulong.MaxValue));
			Assert.That(Uuid64.AllBitsSet.ToInt64(), Is.EqualTo(-1));
			Assert.That(Uuid64.AllBitsSet.ToByteArray(), Is.EqualTo(new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff }));
			Assert.That(Uuid64.AllBitsSet.ToSlice(), Is.EqualTo(Slice.Repeat(0xFF, 8)));
			Assert.That(Uuid64.AllBitsSet, Is.EqualTo(Uuid64.MaxValue));
		}

		[Test]
		public void Test_Uuid64_One()
		{
			Assert.That(Uuid64.One, Is.EqualTo(new Uuid64(1)));
			Assert.That(Uuid64.One.ToString(), Is.EqualTo("00000000-00000001"));
			Assert.That(Uuid64.One.ToUInt64(), Is.EqualTo(1));
			Assert.That(Uuid64.One.ToInt64(), Is.EqualTo(1));
			Assert.That(Uuid64.One.ToByteArray(), Is.EqualTo(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }));

			Assert.That(Uuid64.One, Is.GreaterThan(Uuid64.MinValue));
			Assert.That(Uuid64.One, Is.LessThan(new Uuid64(2)));
		}

		[Test]
		public void Test_Uuid64_Casting()
		{
			// explicit
			Uuid64 a = (Uuid64) 0L;
			Uuid64 b = (Uuid64) 42L;
			Uuid64 c = (Uuid64) 0xDEADBEEFL;
			Uuid64 d = (Uuid64) 0xBADC0FFEE0DDF00DUL;
			Uuid64 e = (Uuid64) ulong.MaxValue;

			// ToUInt64
			Assert.That(a.ToUInt64(), Is.EqualTo(0UL));
			Assert.That(b.ToUInt64(), Is.EqualTo(42UL));
			Assert.That(c.ToUInt64(), Is.EqualTo(3735928559UL));
			Assert.That(d.ToUInt64(), Is.EqualTo(13464654573299691533UL));
			Assert.That(e.ToUInt64(), Is.EqualTo(ulong.MaxValue));

			// ToInt64
			Assert.That(a.ToInt64(), Is.EqualTo(0L));
			Assert.That(b.ToInt64(), Is.EqualTo(42L));
			Assert.That(c.ToInt64(), Is.EqualTo(3735928559L));
			Assert.That(d.ToInt64(), Is.EqualTo(-4982089500409860083L));
			Assert.That(e.ToInt64(), Is.EqualTo(-1L));

			// explicit
			Assert.That((long)a, Is.EqualTo(0));
			Assert.That((long)b, Is.EqualTo(42));
			Assert.That((long)c, Is.EqualTo(0xDEADBEEF));
			Assert.That((ulong)d, Is.EqualTo(13464654573299691533UL));
			Assert.That((ulong)e, Is.EqualTo(ulong.MaxValue));
			Assert.That((long)e, Is.EqualTo(-1L));
		}

		[Test]
		public void Test_Uuid64_ToString()
		{
			Assert.Multiple(() =>
			{
				var guid = new Uuid64(0xBADC0FFEE0DDF00DUL);
				Assert.That(guid.ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL));
				Assert.That(guid.ToString(), Is.EqualTo("BADC0FFE-E0DDF00D"));
				Assert.That(guid.ToString("X"), Is.EqualTo("BADC0FFEE0DDF00D"));
				Assert.That(guid.ToString("B"), Is.EqualTo("{BADC0FFE-E0DDF00D}"));
				Assert.That(guid.ToString("C"), Is.EqualTo("G2eGAUq82Hd"));
			});

			Assert.Multiple(() =>
			{
				var guid = new Uuid64(0xDEADBEEFUL);
				Assert.That(guid.ToUInt64(), Is.EqualTo(0xDEADBEEFUL));
				Assert.That(guid.ToString(), Is.EqualTo("00000000-DEADBEEF"));
				Assert.That(guid.ToString("X"), Is.EqualTo("00000000DEADBEEF"));
				Assert.That(guid.ToString("B"), Is.EqualTo("{00000000-DEADBEEF}"));
				Assert.That(guid.ToString("C"), Is.EqualTo("44pZgF"));
			});
		}

		[Test]
		public void Test_From_Upper_And_Lower_Parts()
		{
			Assert.Multiple(() =>
			{
				var guid = new Uuid64(0xBADC0FFEE0DDF00DUL);

				Assert.That(Uuid64.FromUpper16Lower48(0xBADC, 0x0FFEE0DDF00D), Is.EqualTo(guid));
				Assert.That(Uuid64.FromUpper32Lower32(0xBADC0FFE, 0xE0DDF00D), Is.EqualTo(guid));
				Assert.That(Uuid64.FromUpper48Lower16(0xBADC0FFEE0DD, 0xF00D), Is.EqualTo(guid));
			});
		}

		[Test]
		public void Test_Uuid64_Parse_Hexa16()
		{
			static void CheckSuccess(string literal, ulong expected)
			{
				// string
				Assert.That(Uuid64.Parse(literal).ToUInt64(), Is.EqualTo(expected), $"Should parse: {literal}");
				Assert.That(Uuid64.Parse(literal, CultureInfo.InvariantCulture).ToUInt64(), Is.EqualTo(expected), $"Should parse: {literal}");
				Assert.That(Uuid64.Parse(literal.ToUpperInvariant()).ToUInt64(), Is.EqualTo(expected), $"Should be case-insensitive: {literal}");
				Assert.That(Uuid64.Parse(literal.ToUpperInvariant()).ToUInt64(), Is.EqualTo(expected), $"Should be case-insensitive: {literal}");
				Assert.That(Uuid64.TryParse(literal, out var res), Is.True, $"Should parse: {literal}");
				Assert.That(res.ToUInt64(), Is.EqualTo(expected), $"Should parse: {literal}");
				Assert.That(Uuid64.TryParse(literal, CultureInfo.InvariantCulture, out res), Is.True, $"Should parse: {literal}");
				Assert.That(res.ToUInt64(), Is.EqualTo(expected), $"Should parse: {literal}");
				// ReadOnlySpan<char>
				Assert.That(Uuid64.Parse(literal.AsSpan()).ToUInt64(), Is.EqualTo(expected), $"Should parse: {literal}");
				Assert.That(Uuid64.Parse(literal.AsSpan(), CultureInfo.InvariantCulture).ToUInt64(), Is.EqualTo(expected), $"Should parse: {literal}");
				Assert.That(Uuid64.Parse(literal.ToUpperInvariant().AsSpan()).ToUInt64(), Is.EqualTo(expected), $"Should be case-insensitive: {literal}");
				Assert.That(Uuid64.Parse(literal.ToUpperInvariant().AsSpan(), CultureInfo.InvariantCulture).ToUInt64(), Is.EqualTo(expected), $"Should be case-insensitive: {literal}");
				Assert.That(Uuid64.TryParse(literal.AsSpan(), out res), Is.True, $"Should parse: {literal}");
				Assert.That(res.ToUInt64(), Is.EqualTo(expected), $"Should parse: {literal}");
				Assert.That(Uuid64.TryParse(literal.AsSpan(), CultureInfo.InvariantCulture, out res), Is.True, $"Should parse: {literal}");
				Assert.That(res.ToUInt64(), Is.EqualTo(expected), $"Should parse: {literal}");
			}

			CheckSuccess("0000000000000000", 0);
			CheckSuccess("0123456789abcdef", 0x01234567_89ABCDEFUL);
			CheckSuccess("  0123456789abcdef", 0x01234567_89ABCDEFUL); // leading white spaces are ignored
			CheckSuccess("0123456789abcdef\r\n", 0x01234567_89ABCDEFUL); // trailing white spaces are ignored
			CheckSuccess("badc0ffee0ddf00d", 0xBADC0FFEE_0DDF00DUL);
			CheckSuccess("{00000000-00000000}", 0);
			CheckSuccess("{01234567-89abcdef}", 0x01234567_89ABCDEFUL);
			CheckSuccess("\t{01234567-89abcdef}\r\n", 0x01234567_89ABCDEFUL); // white spaces are ignored
			CheckSuccess("{badc0ffe-e0ddf00d}", 0xBADC0FFEE_0DDF00DUL);
			CheckSuccess("{0000000000000000}", 0);
			CheckSuccess("{0123456789abcdef}", 0x01234567_89ABCDEFUL);
			CheckSuccess("{badc0ffee0ddf00d}", 0xBADC0FFEE_0DDF00DUL);
			CheckSuccess("00000000-00000000", 0);
			CheckSuccess("01234567-89abcdef", 0x01234567_89ABCDEFUL);
			CheckSuccess("badc0ffe-e0ddf00d", 0xBADC0FFEE_0DDF00DUL);
			CheckSuccess("00000000-deadbeef", 0x00000000_DEADBEEFUL);
			CheckSuccess("deadbeef-00000000", 0xDEADBEEF_00000000UL);

			static void CheckFailure(string? literal, string message)
			{
				if (literal == null)
				{
					Assert.That(() => Uuid64.Parse(null!), Throws.ArgumentNullException, message);
					Assert.That(() => Uuid64.Parse(null!, CultureInfo.InvariantCulture), Throws.ArgumentNullException, message);
				}
				else
				{
					Assert.That(() => Uuid64.Parse(literal), Throws.InstanceOf<FormatException>(), message);
					Assert.That(() => Uuid64.Parse(literal, CultureInfo.InvariantCulture), Throws.InstanceOf<FormatException>(), message);
					Assert.That(() => Uuid64.Parse(literal.AsSpan()), Throws.InstanceOf<FormatException>(), message);
					Assert.That(() => Uuid64.Parse(literal.AsSpan(), CultureInfo.InvariantCulture), Throws.InstanceOf<FormatException>(), message);

					Assert.That(Uuid64.TryParse(literal, out var res), Is.False, message);
					Assert.That(res, Is.EqualTo(default(Uuid64)), message);
					Assert.That(Uuid64.TryParse(literal, CultureInfo.InvariantCulture, out res), Is.False, message);
					Assert.That(res, Is.EqualTo(default(Uuid64)), message);

					Assert.That(Uuid64.TryParse(literal.AsSpan(), out res), Is.False, message);
					Assert.That(res, Is.EqualTo(default(Uuid64)), message);
					Assert.That(Uuid64.TryParse(literal.AsSpan(), CultureInfo.InvariantCulture, out res), Is.False, message);
					Assert.That(res, Is.EqualTo(default(Uuid64)), message);
				}
			}

			CheckFailure(default, "Null string is invalid");
			CheckFailure("", "Only whitespaces");
			CheckFailure("  ", "Only whitespaces");
			CheckFailure("hello", "random text string");
			CheckFailure("hello badc0ffe-e0ddf00d", "random text prefix");
			CheckFailure("badc0ffe-e0ddf00d world", "random text suffix");
			CheckFailure("12345678-9ABCDEFG", "Invalid hexa character 'G'");
			CheckFailure("00000000-0000000", "Too short");
			CheckFailure("00000000-0000000 ", "Too short + extra space");
			CheckFailure("zzzzzzzz-zzzzzzzz", "Invalid char");
			CheckFailure("badc0ffe-e0ddf00", "Missing last char");
			CheckFailure("baadc0ffe-e0ddf00", "'-' at invalid position");
			CheckFailure("badc0fe-ee0ddf00d", "'-' at invalid position");
		}

		[Test]
		public void Test_Uuid64_ToString_Base62()
		{
			ReadOnlySpan<char> chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
			Assert.That(chars.Length, Is.EqualTo(62));

			// single digit
			for (int i = 0; i < 62;i++)
			{
				Assert.That(new Uuid64(i).ToString("C"), Is.EqualTo(chars[i].ToString()));
				Assert.That(new Uuid64(i).ToString("Z"), Is.EqualTo("0000000000" + chars[i]));
			}

			// two digits
			for (int j = 1; j < 62; j++)
			{
				var prefix = chars[j].ToString();
				for (int i = 0; i < 62; i++)
				{
					Assert.That(new Uuid64(j * 62 + i).ToString("C"), Is.EqualTo(prefix + chars[i]));
					Assert.That(new Uuid64(j * 62 + i).ToString("Z"), Is.EqualTo("000000000" + prefix + chars[i]));
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
				var uuid = new Uuid64(x);

				// no padding
				string expected =
					d > 0 ? ("" + chars[d] + chars[c] + chars[b] + chars[a]) :
					c > 0 ? ("" + chars[c] + chars[b] + chars[a]) :
					b > 0 ? ("" + chars[b] + chars[a]) :
					("" + chars[a]);
				Assert.That(uuid.ToString("C"), Is.EqualTo(expected));

				// padding
				Assert.That(uuid.ToString("Z"), Is.EqualTo("0000000" + chars[d] + chars[c] + chars[b] + chars[a]));
			}

			// Numbers of the form 62^n should be encoded as '1' followed by n x '0', for n from 0 to 10
			ulong val = 1;
			for (int i = 0; i <= 10; i++)
			{
				Assert.That(new Uuid64(val).ToString("C"), Is.EqualTo("1" + new string('0', i)), $"62^{i}");
				val *= 62;
			}

			// Numbers of the form 62^n - 1 should be encoded as n x 'z', for n from 1 to 10
			val = 0;
			for (int i = 1; i <= 10; i++)
			{
				val += 61;
				Assert.That(new Uuid64(val).ToString("C"), Is.EqualTo(new string('z', i)), $"62^{i} - 1");
				val *= 62;
			}

			// well known values
			Assert.That(new Uuid64(0xB45B07).ToString("C"), Is.EqualTo("narf"));
			Assert.That(new Uuid64(0xE0D0ED).ToString("C"), Is.EqualTo("zort"));
			Assert.That(new Uuid64(0xDEADBEEF).ToString("C"), Is.EqualTo("44pZgF"));
			Assert.That(new Uuid64(0xDEADBEEF).ToString("Z"), Is.EqualTo("0000044pZgF"));
			Assert.That(new Uuid64(0xBADC0FFEE0DDF00DUL).ToString("C"), Is.EqualTo("G2eGAUq82Hd"));
			Assert.That(new Uuid64(0xBADC0FFEE0DDF00DUL).ToString("Z"), Is.EqualTo("G2eGAUq82Hd"));

			Assert.That(new Uuid64(255).ToString("C"), Is.EqualTo("47"));
			Assert.That(new Uuid64(ushort.MaxValue).ToString("C"), Is.EqualTo("H31"));
			Assert.That(new Uuid64(uint.MaxValue).ToString("C"), Is.EqualTo("4gfFC3"));
			Assert.That(new Uuid64(ulong.MaxValue - 1).ToString("C"), Is.EqualTo("LygHa16AHYE"));
			Assert.That(new Uuid64(ulong.MaxValue).ToString("C"), Is.EqualTo("LygHa16AHYF"));
		}

		[Test]
		public void Test_Uuid64_Parse_Base62()
		{

			Assert.That(Uuid64.FromBase62("").ToUInt64(), Is.EqualTo(0));
			Assert.That(Uuid64.FromBase62("0").ToUInt64(), Is.EqualTo(0));
			Assert.That(Uuid64.FromBase62("9").ToUInt64(), Is.EqualTo(9));
			Assert.That(Uuid64.FromBase62("A").ToUInt64(), Is.EqualTo(10));
			Assert.That(Uuid64.FromBase62("Z").ToUInt64(), Is.EqualTo(35));
			Assert.That(Uuid64.FromBase62("a").ToUInt64(), Is.EqualTo(36));
			Assert.That(Uuid64.FromBase62("z").ToUInt64(), Is.EqualTo(61));
			Assert.That(Uuid64.FromBase62("10").ToUInt64(), Is.EqualTo(62));
			Assert.That(Uuid64.FromBase62("zz").ToUInt64(), Is.EqualTo(3843));
			Assert.That(Uuid64.FromBase62("100").ToUInt64(), Is.EqualTo(3844));
			Assert.That(Uuid64.FromBase62("zzzzzzzzzz").ToUInt64(), Is.EqualTo(839299365868340223UL));
			Assert.That(Uuid64.FromBase62("10000000000").ToUInt64(), Is.EqualTo(839299365868340224UL));
			Assert.That(Uuid64.FromBase62("LygHa16AHYF").ToUInt64(), Is.EqualTo(ulong.MaxValue), "ulong.MaxValue in base 62");

			// well known values

			Assert.That(Uuid64.FromBase62("narf").ToUInt64(), Is.EqualTo(0xB45B07));
			Assert.That(Uuid64.FromBase62("zort").ToUInt64(), Is.EqualTo(0xE0D0ED));
			Assert.That(Uuid64.FromBase62("44pZgF").ToUInt64(), Is.EqualTo(0xDEADBEEF));
			Assert.That(Uuid64.FromBase62("0000044pZgF").ToUInt64(), Is.EqualTo(0xDEADBEEF));

			Assert.That(Uuid64.FromBase62("G2eGAUq82Hd").ToUInt64(), Is.EqualTo(0xBADC0FFEE0DDF00DUL));

			Assert.That(Uuid64.FromBase62("4gfFC3").ToUInt64(), Is.EqualTo(uint.MaxValue));
			Assert.That(Uuid64.FromBase62("000004gfFC3").ToUInt64(), Is.EqualTo(uint.MaxValue));


			// invalid chars
			Assert.That(() => Uuid64.FromBase62("/"), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid64.FromBase62("@"), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid64.FromBase62("["), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid64.FromBase62("`"), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid64.FromBase62("{"), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid64.FromBase62("zaz/"), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid64.FromBase62("z/o&r=g"), Throws.InstanceOf<FormatException>());

			// overflow
			Assert.That(() => Uuid64.FromBase62("zzzzzzzzzzz"), Throws.InstanceOf<OverflowException>(), "62^11 - 1 => OVERFLOW");
			Assert.That(() => Uuid64.FromBase62("LygHa16AHYG"), Throws.InstanceOf<OverflowException>(), "ulong.MaxValue + 1 => OVERFLOW");

			// invalid length
			Assert.That(() => Uuid64.FromBase62(default(string)!), Throws.ArgumentNullException);
			Assert.That(() => Uuid64.FromBase62("100000000000"), Throws.InstanceOf<FormatException>(), "62^11 => TOO BIG");

		}

		[Test]
		public void Test_Uuid64_NewUid()
		{
			var a = Uuid64.NewUuid();
			var b = Uuid64.NewUuid();
			Assert.That(a.ToUInt64(), Is.Not.EqualTo(b.ToUInt64()));
			Assert.That(a, Is.Not.EqualTo(b));

			const int N = 1_000;
			var uids = new HashSet<ulong>();
			for (int i = 0; i < N; i++)
			{
				var uid = Uuid64.NewUuid();
				if (uids.Contains(uid.ToUInt64())) Assert.Fail($"Duplicate Uuid64 generated: {uid}");
				uids.Add(uid.ToUInt64());
			}
			Assert.That(uids.Count, Is.EqualTo(N));
		}

		[Test]
		public void Test_Uuid64_Random()
		{
			// note: this uses a generic random number generator that does not guarantee uniqueness
			var rng = this.Rnd;

			Uuid64 uuid;
			for (int i = 0; i < 10_000; i++)
			{
				uuid = Uuid64.Random(rng);
				Assert.That(uuid.ToUInt64(), Is.GreaterThanOrEqualTo(0).And.LessThan(ulong.MaxValue));

				uuid = Uuid64.Random(rng, 0x1234);
				Assert.That(uuid.ToUInt64(), Is.GreaterThanOrEqualTo(0).And.LessThan(0x1234));

				uuid = Uuid64.Random(rng, 0x123456789);
				Assert.That(uuid.ToUInt64(), Is.GreaterThanOrEqualTo(0).And.LessThan(0x123456789));

				uuid = Uuid64.Random(rng, ulong.MaxValue);
				Assert.That(uuid.ToUInt64(), Is.GreaterThanOrEqualTo(0).And.LessThan(ulong.MaxValue));

				uuid = Uuid64.Random(rng, ulong.MaxValue - 1);
				Assert.That(uuid.ToUInt64(), Is.GreaterThanOrEqualTo(0).And.LessThan(ulong.MaxValue - 1));

				uuid = Uuid64.Random(rng, 0x1234, 0x5678);
				Assert.That(uuid.ToUInt64(), Is.GreaterThanOrEqualTo(0x1234).And.LessThan(0x5678));

				uuid = Uuid64.Random(rng, 0x1_0000_00000000, 0x2_0000_00000000);
				Assert.That(uuid.ToUInt64(), Is.GreaterThanOrEqualTo(0x1_0000_00000000).And.LessThan(0x2_0000_00000000));

				uuid = Uuid64.Random(rng, 0x1_0000_00000000, 0xFFFF_0000_00000000);
				Assert.That(uuid.ToUInt64(), Is.GreaterThanOrEqualTo(0x1234).And.LessThan(0xFFFF_0000_00000000));

				uuid = Uuid64.Random(rng, 0xFFFE_0000_00000000, 0xFFFF_0000_00000000);
				Assert.That(uuid.ToUInt64(), Is.GreaterThanOrEqualTo(0xFFFE_0000_00000000).And.LessThan(0xFFFF_0000_00000000));

			}
		}

		[Test]
		public void Test_Uuid64RandomGenerator_NewUid()
		{
			// this is expected to generate _distinct_ values, with a very low probability of duplicates
			// => if this test fail with duplicate, maybe you are very (un)lucky. Try to run this test multiple times!

			var gen = Uuid64.RandomGenerator.Default;
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
				if (uids.Contains(uid.ToUInt64())) Assert.Fail($"Duplicate Uuid64 generated: {uid}");
				uids.Add(uid.ToUInt64());
			}
			Assert.That(uids.Count, Is.EqualTo(N));
		}

		[Test]
		public void Test_Uuid64_Equality_Check()
		{
			var a = new Uuid64(42);
			var b = new Uuid64(42);
			var c = new Uuid64(40) + 2;
			var d = new Uuid64(0xDEADBEEF);

			// Equals(Uuid64)
			Assert.That(a.Equals(a), Is.True, "a == a");
			Assert.That(a.Equals(b), Is.True, "a == b");
			Assert.That(a.Equals(c), Is.True, "a == c");
			Assert.That(a.Equals(d), Is.False, "a != d");

			// == Uuid64
			Assert.That(a == b, Is.True, "a == b");
			Assert.That(a == c, Is.True, "a == c");
			Assert.That(a == d, Is.False, "a != d");

			// != Uuid64
			Assert.That(a != b, Is.False, "a == b");
			Assert.That(a != c, Is.False, "a == c");
			Assert.That(a != d, Is.True, "a != d");

			// == numbers
			Assert.That(a == 42L, Is.True, "a == 42");
			Assert.That(a == 42UL, Is.True, "a == 42");
			Assert.That(d == 42L, Is.False, "d != 42");
			Assert.That(d == 42UL, Is.False, "d != 42");

			// != numbers
			Assert.That(a != 42L, Is.False, "a == 42");
			Assert.That(a != 42UL, Is.False, "a == 42");
			Assert.That(d != 42L, Is.True, "d != 42");
			Assert.That(d != 42UL, Is.True, "d != 42");

			// Equals(objecct)
			Assert.That(a.Equals((object?) a), Is.True, "a == a");
			Assert.That(a.Equals((object?) b), Is.True, "a == b");
			Assert.That(a.Equals((object?) c), Is.True, "a == c");
			Assert.That(a.Equals((object?) d), Is.False, "a != d");
			Assert.That(a.Equals((object?) 42L), Is.True, "a == 42");
			Assert.That(a.Equals((object?) 42UL), Is.True, "a == 42");
			Assert.That(d.Equals((object?) 42L), Is.False, "d != 42");
			Assert.That(d.Equals((object?) 42UL), Is.False, "d != 42");

		}

		[Test]
		public void Test_Uuid64_Ordering()
		{
			var a = new Uuid64(42);
			var a2 = new Uuid64(42);
			var b = new Uuid64(77);

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
			Assert.That(Uuid64.Parse("137bcf31-0c8873a2") < Uuid64.Parse("604bdf8a-2512b4ad"), Is.True);
			Assert.That(Uuid64.Parse("d8f17a26-82adb1a4") < Uuid64.Parse("22abbf33-1b2c1db0"), Is.False);
			Assert.That(Uuid64.Parse("{137bcf31-0c8873a2}") > Uuid64.Parse("{604bdf8a-2512b4ad}"), Is.False);
			Assert.That(Uuid64.Parse("{d8f17a26-82adb1a4}") > Uuid64.Parse("{22abbf33-1b2c1db0}"), Is.True);
			Assert.That(Uuid64.FromBase62("2w6CTjUiXVp") < Uuid64.FromBase62("DVM0UnynZ1Q"), Is.True);
			Assert.That(Uuid64.FromBase62("0658JY2ORSJ") > Uuid64.FromBase62("FMPaNaMEUWc"), Is.False);

			// verify byte ordering
			var c = new Uuid64(0x0000000100000002);
			var d = new Uuid64(0x0000000200000001);
			Assert.That(c.CompareTo(d), Is.EqualTo(-1));
			Assert.That(d.CompareTo(c), Is.EqualTo(+1));

			// verify that we can sort an array of Uuid64
			var uids = new Uuid64[100];
			for (int i = 0; i < uids.Length; i++)
			{
				uids[i] = Uuid64.NewUuid();
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
		public void Test_Uuid64_Arithmetic()
		{
			var uid = Uuid64.Empty;

			Assert.That(uid + 42L, Is.EqualTo(new Uuid64(42)));
			Assert.That(uid + 42UL, Is.EqualTo(new Uuid64(42)));
			uid++;
			Assert.That(uid.ToInt64(), Is.EqualTo(1));
			uid++;
			Assert.That(uid.ToInt64(), Is.EqualTo(2));
			uid--;
			Assert.That(uid.ToInt64(), Is.EqualTo(1));
			uid--;
			Assert.That(uid.ToInt64(), Is.EqualTo(0));

			uid = Uuid64.NewUuid();

			Assert.That(uid + 123L, Is.EqualTo(new Uuid64(uid.ToInt64() + 123)));
			Assert.That(uid + 123UL, Is.EqualTo(new Uuid64(uid.ToUInt64() + 123)));

			Assert.That(uid - 123L, Is.EqualTo(new Uuid64(uid.ToInt64() - 123)));
			Assert.That(uid - 123UL, Is.EqualTo(new Uuid64(uid.ToUInt64() - 123)));
		}

		[Test]
		public void Test_Uuid64_Read_From_Bytes()
		{
			// test buffer with included padding
			byte[] buf = { 0x55, 0x55, 0x55, 0x55, /* start */ 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF, /* stop */ 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA };
			var original = Uuid64.Parse("01234567-89ABCDEF");
			Assume.That(original.ToUInt64(), Is.EqualTo(0x0123456789ABCDEF));
			Uuid64 res;

			// ReadOnlySpan<byte>
			Assert.That(Uuid64.Read(buf.AsSpan(4, 8)), Is.EqualTo(original));
			Assert.That(() => Uuid64.Read(buf.AsSpan(4, 7)), Throws.ArgumentException);
			Assert.That(() => Uuid64.Read(buf.AsSpan(4, 1)), Throws.ArgumentException);
			Assert.That(Uuid64.Read(buf.AsSpan(4, 0)), Is.EqualTo(Uuid64.Empty));
			Assert.That(Uuid64.TryRead(buf.AsSpan(4, 8), out res), Is.True.WithOutput(res).EqualTo(original));
			Assert.That(Uuid64.TryRead(buf.AsSpan(4, 7), out res), Is.False);
			Assert.That(Uuid64.TryRead(buf.AsSpan(4, 1), out res), Is.False);
			Assert.That(Uuid64.TryRead(buf.AsSpan(4, 0), out res), Is.True.WithOutput(res).EqualTo(Uuid64.Empty));

			// Slice
			Assert.That(Uuid64.Read(buf.AsSlice(4, 8)), Is.EqualTo(original));
			Assert.That(Uuid64.TryRead(buf.AsSlice(4, 8), out res), Is.True.WithOutput(res).EqualTo(original));

			// byte[]
			Assert.That(Uuid64.Read(buf.AsSlice(4, 8).ToArray()), Is.EqualTo(original));
			Assert.That(Uuid64.Read(Array.Empty<byte>()), Is.EqualTo(Uuid64.Empty));
			Assert.That(Uuid64.Read(null), Is.EqualTo(Uuid64.Empty));
			Assert.That(Uuid64.TryRead(buf.AsSlice(4, 8).ToArray(), out res), Is.True.WithOutput(res).EqualTo(original));
		}

		[Test]
		public void Test_UUid64_WriteTo()
		{
			static byte[] Repeat(byte value, int count)
			{
				var tmp = new byte[count];
				tmp.AsSpan().Fill(value);
				return tmp;
			}

			var original = Uuid64.Parse("01234567-89ABCDEF");
			Assume.That(original.ToUInt64(), Is.EqualTo(0x0123456789ABCDEF));

			// span with more space
			var scratch = Repeat(0xAA, 16);
			original.WriteTo(scratch.AsSpan());
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("01 23 45 67 89 AB CD EF AA AA AA AA AA AA AA AA"));

			// span with no offset and exact size
			scratch = Repeat(0xAA, 16);
			original.WriteTo(scratch.AsSpan(0, 8));
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("01 23 45 67 89 AB CD EF AA AA AA AA AA AA AA AA"));

			// span with offset
			scratch = Repeat(0xAA, 16);
			original.WriteTo(scratch.AsSpan(4));
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("AA AA AA AA 01 23 45 67 89 AB CD EF AA AA AA AA"));

			// span with offset and exact size
			scratch = Repeat(0xAA, 16);
			original.WriteTo(scratch.AsSpan(4, 8));
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("AA AA AA AA 01 23 45 67 89 AB CD EF AA AA AA AA"));

			// errors

			Assert.That(() => original.WriteTo(Span<byte>.Empty), Throws.InstanceOf<ArgumentException>(), "Target buffer is empty");

			scratch = Repeat(0xAA, 16);
			Assert.That(() => original.WriteTo(scratch.AsSpan(0, 7)), Throws.InstanceOf<ArgumentException>(), "Target buffer is too small");
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA"), "Buffer should not have been overwritten!");

		}

		[Test]
		public void Test_Uuid64_TryWriteTo()
		{
			static byte[] Repeat(byte value, int count)
			{
				var tmp = new byte[count];
				tmp.AsSpan().Fill(value);
				return tmp;
			}

			var original = Uuid64.Parse("01234567-89ABCDEF");
			Assume.That(original.ToUInt64(), Is.EqualTo(0x0123456789ABCDEF));

			// span with more space
			var scratch = Repeat(0xAA, 16);
			Assert.That(original.TryWriteTo(scratch.AsSpan()), Is.True);
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("01 23 45 67 89 AB CD EF AA AA AA AA AA AA AA AA"));

			// span with no offset and exact size
			scratch = Repeat(0xAA, 16);
			Assert.That(original.TryWriteTo(scratch.AsSpan(0, 8)), Is.True);
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("01 23 45 67 89 AB CD EF AA AA AA AA AA AA AA AA"));

			// span with offset
			scratch = Repeat(0xAA, 16);
			Assert.That(original.TryWriteTo(scratch.AsSpan(4)), Is.True);
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("AA AA AA AA 01 23 45 67 89 AB CD EF AA AA AA AA"));

			// span with offset and exact size
			scratch = Repeat(0xAA, 16);
			Assert.That(original.TryWriteTo(scratch.AsSpan(4, 8)), Is.True);
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("AA AA AA AA 01 23 45 67 89 AB CD EF AA AA AA AA"));

			// errors

			Assert.That(original.TryWriteTo(Span<byte>.Empty), Is.False, "Target buffer is empty");

			scratch = Repeat(0xAA, 16);
			Assert.That(original.TryWriteTo(scratch.AsSpan(0, 7)), Is.False, "Target buffer is too small");
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA"), "Buffer should not have been overwritten!");

		}

	}

}
