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
namespace SnowBank.Core.Tests
{

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class Uuid80Facts : SimpleTest
	{

		[Test]
		public void Test_Uuid80_Empty()
		{
			Assert.Multiple(() =>
			{
				Assert.That(Uuid80.Empty.ToString(), Is.EqualTo("0000-00000000-00000000"));
				Assert.That(Uuid80.Empty, Is.EqualTo(default(Uuid80)));
				Assert.That(Uuid80.Empty, Is.EqualTo(new Uuid80(0, 0UL)));
				Assert.That(Uuid80.Empty, Is.EqualTo(new Uuid80(0, 0U, 0U)));
				Assert.That(Uuid80.Empty, Is.EqualTo(new Uuid80(0, 0L)));
				Assert.That(Uuid80.Empty, Is.EqualTo(new Uuid80(0, 0, 0)));
				Assert.That(Uuid80.Empty, Is.EqualTo(Uuid80.Read(new byte[10])));

				Assert.That(Uuid80.Empty.Upper16, Is.EqualTo(0));
				Assert.That(Uuid80.Empty.Upper32, Is.EqualTo(0));
				Assert.That(Uuid80.Empty.Upper48, Is.EqualTo(0));
				Assert.That(Uuid80.Empty.Upper64, Is.EqualTo(0));

				Assert.That(Uuid80.Empty.Lower16, Is.EqualTo(0));
				Assert.That(Uuid80.Empty.Lower32, Is.EqualTo(0));
				Assert.That(Uuid80.Empty.Lower48, Is.EqualTo(0));
				Assert.That(Uuid80.Empty.Lower64, Is.EqualTo(0));
			});
		}

		[Test]
		public void Test_Uuid80_ToString()
		{
			Assert.Multiple(() =>
			{
				var guid = new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL);
				Assert.That(guid.ToString(), Is.EqualTo("ABCD-BADC0FFE-E0DDF00D"));
				Assert.That(guid.ToString("d"), Is.EqualTo("abcd-badc0ffe-e0ddf00d"));
				Assert.That(guid.ToString("X"), Is.EqualTo("ABCDBADC0FFEE0DDF00D"));
				Assert.That(guid.ToString("x"), Is.EqualTo("abcdbadc0ffee0ddf00d"));
				Assert.That(guid.ToString("B"), Is.EqualTo("{ABCD-BADC0FFE-E0DDF00D}"));
				Assert.That(guid.ToString("b"), Is.EqualTo("{abcd-badc0ffe-e0ddf00d}"));

				Assert.That(guid.Upper16, Is.EqualTo(0xABCDu));
				Assert.That(guid.Upper32, Is.EqualTo(0xABCDBADCu));
				Assert.That(guid.Upper48, Is.EqualTo(0xABCDBADC0FFEul));
				Assert.That(guid.Upper64, Is.EqualTo(0xABCDBADC0FFEE0DDul));

				Assert.That(guid.Lower16, Is.EqualTo(0xF00D));
				Assert.That(guid.Lower32, Is.EqualTo(0xE0DDF00D));
				Assert.That(guid.Lower48, Is.EqualTo(0x0FFEE0DDF00D));
				Assert.That(guid.Lower64, Is.EqualTo(0xBADC0FFEE0DDF00D));
			});
		}

		[Test]
		public void Test_From_Upper_And_Lower_Parts()
		{
			Assert.Multiple(() =>
			{
				var guid = new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL);

				Assert.That(Uuid80.FromUpper16Lower64(0xABCD, 0xBADC0FFEE0DDF00D), Is.EqualTo(guid));
				Assert.That(Uuid80.FromUpper32Lower48(0xABCDBADC, 0x0FFEE0DDF00D), Is.EqualTo(guid));
				Assert.That(Uuid80.FromUpper48Lower32(0xABCDBADC0FFE, 0xE0DDF00D), Is.EqualTo(guid));
				Assert.That(Uuid80.FromUpper64Lower16(0xABCDBADC0FFEE0DD, 0xF00D), Is.EqualTo(guid));
			});
		}

		[Test]
		public void Test_Uuid80_Parse_Hexa16()
		{
			// string

			Assert.That(Uuid80.Parse("abcd-badc0ffe-e0ddf00d"), Is.EqualTo(new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL)));
			Assert.That(Uuid80.Parse("ABCD-BADC0FFE-E0DDF00D"), Is.EqualTo(new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL)), "Should be case-insensitive");

			Assert.That(Uuid80.Parse("abcdbadc0ffee0ddf00d"), Is.EqualTo(new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL)));
			Assert.That(Uuid80.Parse("ABCDBADC0FFEE0DDF00D"), Is.EqualTo(new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL)), "Should be case-insensitive");

			Assert.That(Uuid80.Parse("  abcd-badc0ffe-e0ddf00d"), Is.EqualTo(new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL)), "Leading white spaces are allowed");
			Assert.That(Uuid80.Parse("abcd-badc0ffe-e0ddf00d\r\n"), Is.EqualTo(new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL)), "Trailing white spaces are allowed");

			Assert.That(Uuid80.Parse("{abcd-badc0ffe-e0ddf00d}"), Is.EqualTo(new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL)));
			Assert.That(Uuid80.Parse("{ABCD-BADC0FFE-E0DDF00D}"), Is.EqualTo(new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL)), "Should be case-insensitive");

			Assert.That(Uuid80.Parse("{abcdbadc0ffee0ddf00d}"), Is.EqualTo(new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL)));
			Assert.That(Uuid80.Parse("{ABCDBADC0FFEE0DDF00D}"), Is.EqualTo(new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL)), "should be case-insensitive");

			Assert.That(Uuid80.Parse("ffff-00000000-deadbeef"), Is.EqualTo(new Uuid80(0xFFFF, 0, 0xDEADBEEFU)));
			Assert.That(Uuid80.Parse("{ffff-00000000-deadbeef}"), Is.EqualTo(new Uuid80(0xFFFF, 0, 0xDEADBEEFU)));

			// errors
			Assert.That(() => Uuid80.Parse(default(string)!), Throws.ArgumentNullException);
			Assert.That(() => Uuid80.Parse(""), Throws.InstanceOf<FormatException>(), "Empty string not allowed");
			Assert.That(() => Uuid80.Parse("   "), Throws.InstanceOf<FormatException>(), "Only white spaces not allowed");
			Assert.That(() => Uuid80.Parse("hello"), Throws.InstanceOf<FormatException>());
			Assert.That(() => Uuid80.Parse("abcd-12345678-9ABCDEFG"), Throws.InstanceOf<FormatException>(), "Invalid hexa character 'G'");
			Assert.That(() => Uuid80.Parse("0000-00000000-0000000 "), Throws.InstanceOf<FormatException>(), "Too short + extra space");
			Assert.That(() => Uuid80.Parse("zzzz-zzzzzzzz-zzzzzzzz"), Throws.InstanceOf<FormatException>(), "Invalid char");
			Assert.That(() => Uuid80.Parse("abcd-badc0ffe-e0ddf00"), Throws.InstanceOf<FormatException>(), "Missing last char");
			Assert.That(() => Uuid80.Parse("abcdbaadc0ffe-e0ddf00"), Throws.InstanceOf<FormatException>(), "'-' at invalid position");
			Assert.That(() => Uuid80.Parse("abcd-badc0fe-ee0ddf00d"), Throws.InstanceOf<FormatException>(), "'-' at invalid position");
			Assert.That(() => Uuid80.Parse("abcdb-adc0ffe-e0ddf00d"), Throws.InstanceOf<FormatException>(), "'-' at invalid position");

			// span from string

			Assert.That(Uuid80.Parse("abcd-badc0ffe-e0ddf00d".AsSpan()), Is.EqualTo(new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL)));
			Assert.That(Uuid80.Parse("abcdbadc0ffee0ddf00d".AsSpan()), Is.EqualTo(new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL)));
			Assert.That(Uuid80.Parse("hello abcd-badc0ffe-e0ddf00d world!".AsSpan().Slice(6, 22)), Is.EqualTo(new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL)));
			Assert.That(Uuid80.Parse("hello abcdbadc0ffee0ddf00d world!".AsSpan().Slice(6, 20)), Is.EqualTo(new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL)));

			// span from char[]

			Assert.That(Uuid80.Parse("abcd-badc0ffe-e0ddf00d".ToCharArray().AsSpan()), Is.EqualTo(new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL)));
			Assert.That(Uuid80.Parse("abcdbadc0ffee0ddf00d".ToCharArray().AsSpan()), Is.EqualTo(new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL)));
			Assert.That(Uuid80.Parse("hello abcd-badc0ffe-e0ddf00d world!".ToCharArray().AsSpan().Slice(6, 22)), Is.EqualTo(new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL)));
			Assert.That(Uuid80.Parse("hello abcdbadc0ffee0ddf00d world!".ToCharArray().AsSpan().Slice(6, 20)), Is.EqualTo(new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL)));

			// span from stackalloc

			unsafe
			{
				char* buf = stackalloc char[64];
				var span = new Span<char>(buf, 64);

				span.Clear();
				"abcd-badc0ffe-e0ddf00d".AsSpan().CopyTo(span);
				Assert.That(Uuid80.Parse(span.Slice(0, 22)), Is.EqualTo(new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL)));

				span.Clear();
				"abcdbadc0ffee0ddf00d".AsSpan().CopyTo(span);
				Assert.That(Uuid80.Parse(span.Slice(0, 20)), Is.EqualTo(new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL)));

				span.Clear();
				"{abcd-badc0ffe-e0ddf00d}".AsSpan().CopyTo(span);
				Assert.That(Uuid80.Parse(span.Slice(0, 24)), Is.EqualTo(new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL)));

				span.Clear();
				"{abcdbadc0ffee0ddf00d}".AsSpan().CopyTo(span);
				Assert.That(Uuid80.Parse(span.Slice(0, 22)), Is.EqualTo(new Uuid80(0xABCD, 0xBADC0FFEE0DDF00DUL)));
			}
		}

		[Test]
		public void Test_Uuid80_NewUid()
		{
			var a = Uuid80.NewUuid();
			var b = Uuid80.NewUuid();
			Assert.That(a, Is.Not.EqualTo(b));

			const int N = 1_000;
			var uids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < N; i++)
			{
				var uid = Uuid80.NewUuid();
				string s = uid.ToString();
				if (uids.Contains(s)) Assert.Fail($"Duplicate Uuid80 generated: {uid}");
				uids.Add(s);
			}
			Assert.That(uids.Count, Is.EqualTo(N));
		}

		[Test]
		public void Test_Uuid80RandomGenerator_NewUid()
		{
			var gen = Uuid80.RandomGenerator.Default;
			Assert.That(gen, Is.Not.Null);

			var a = gen.NewUuid();
			var b = gen.NewUuid();
			Assert.That(a, Is.Not.EqualTo(b));

			const int N = 1_000;
			var uids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < N; i++)
			{
				var uid = gen.NewUuid();
				string s = uid.ToString();
				if (uids.Contains(s)) Assert.Fail($"Duplicate Uuid80 generated: {uid}");
				uids.Add(s);
			}
			Assert.That(uids.Count, Is.EqualTo(N));
		}

		[Test]
		public void Test_Uuid80_Equality_Check()
		{
			var a = new Uuid80(123, 42);
			var b = new Uuid80(123, 42);
			var c = new Uuid80(123, 40) + 2;
			var d = new Uuid80(123, 0xDEADBEEF);
			var e = new Uuid80(124, 42);

			// Equals(Uuid80)
			Assert.That(a.Equals(a), Is.True, "a == a");
			Assert.That(a.Equals(b), Is.True, "a == b");
			Assert.That(a.Equals(c), Is.True, "a == c");
			Assert.That(a.Equals(d), Is.False, "a != d");
			Assert.That(a.Equals(e), Is.False, "a != e");

			// == Uuid80
			Assert.That(a == b, Is.True, "a == b");
			Assert.That(a == c, Is.True, "a == c");
			Assert.That(a == d, Is.False, "a != d");
			Assert.That(a == e, Is.False, "a != e");

			// != Uuid80
			Assert.That(a != b, Is.False, "a == b");
			Assert.That(a != c, Is.False, "a == c");
			Assert.That(a != d, Is.True, "a != d");
			Assert.That(a != e, Is.True, "a != e");

			// Equals(objecct)
			Assert.That(a.Equals((object)a), Is.True, "a == a");
			Assert.That(a.Equals((object)b), Is.True, "a == b");
			Assert.That(a.Equals((object)c), Is.True, "a == c");
			Assert.That(a.Equals((object)d), Is.False, "a != d");
			Assert.That(a.Equals((object)e), Is.False, "a != e");
		}

		[Test]
		public void Test_Uuid80_Ordering()
		{
			var a = new Uuid80(123, 42);
			var a2 = new Uuid80(123, 42);
			var b = new Uuid80(123, 77);
			var c = new Uuid80(124, 41);

			Assert.That(a.CompareTo(a), Is.EqualTo(0));
			Assert.That(a.CompareTo(b), Is.EqualTo(-1));
			Assert.That(b.CompareTo(a), Is.EqualTo(+1));
			Assert.That(c.CompareTo(a), Is.EqualTo(+1));

			Assert.That(a < b, Is.True, "a < b");
			Assert.That(a <= b, Is.True, "a <= b");
			Assert.That(a < a2, Is.False, "a < a");
			Assert.That(a <= a2, Is.True, "a <= a");
			Assert.That(a < c, Is.True, "a < c");

			Assert.That(a > b, Is.False, "a > b");
			Assert.That(a >= b, Is.False, "a >= b");
			Assert.That(a > a2, Is.False, "a > a");
			Assert.That(a >= a2, Is.True, "a >= a");
			Assert.That(a > c, Is.False, "a > c");

			// parsed from string
			Assert.That(Uuid80.Parse("1234-137bcf31-0c8873a2") < Uuid80.Parse("1234-604bdf8a-2512b4ad"), Is.True);
			Assert.That(Uuid80.Parse("1234-d8f17a26-82adb1a4") < Uuid80.Parse("1234-22abbf33-1b2c1db0"), Is.False);
			Assert.That(Uuid80.Parse("{1234-137bcf31-0c8873a2}") > Uuid80.Parse("{1234-604bdf8a-2512b4ad}"), Is.False);
			Assert.That(Uuid80.Parse("{1234-d8f17a26-82adb1a4}") > Uuid80.Parse("{1234-22abbf33-1b2c1db0}"), Is.True);

			// verify byte ordering
			var d = new Uuid80(0x0001, 0x0000000200000003);
			var e = new Uuid80(0x0003, 0x0000000200000001);
			Assert.That(d.CompareTo(e), Is.LessThan(0));
			Assert.That(e.CompareTo(d), Is.GreaterThan(0));

			// verify that we can sort an array of Uuid80
			var uids = new Uuid80[100];
			for (int i = 0; i < uids.Length; i++)
			{
				uids[i] = Uuid80.NewUuid();
			}
			Assume.That(uids, Is.Not.Ordered, "This can happen with a very small probability. Please try again");
			Array.Sort(uids);
			Assert.That(uids, Is.Ordered);

			// ordering should be preserved in integer or textual form

			Assert.That(uids.Select(x => x.ToString()), Is.Ordered.Using<string>(StringComparer.Ordinal), "order should be preserved when ordering by text (hexa)");
		}

		[Test]
		public void Test_Uuid80_Arithmetic()
		{
			var uid = Uuid80.Empty;

			Assert.That(uid + 42L, Is.EqualTo(new Uuid80(0, 42)));
			Assert.That(uid + 42UL, Is.EqualTo(new Uuid80(0, 42)));
			uid++;
			Assert.That(uid.ToString(), Is.EqualTo("0000-00000000-00000001"));
			uid++;
			Assert.That(uid.ToString(), Is.EqualTo("0000-00000000-00000002"));
			uid--;
			Assert.That(uid.ToString(), Is.EqualTo("0000-00000000-00000001"));
			uid--;
			Assert.That(uid.ToString(), Is.EqualTo("0000-00000000-00000000"));

			uid = new Uuid80(42, ulong.MaxValue - 99);
			Assert.That(uid + 123L, Is.EqualTo(new Uuid80(43, 23)));
			Assert.That(uid + 123UL, Is.EqualTo(new Uuid80(43, 23)));

			uid = new Uuid80(42, 99);
			Assert.That(uid - 123L, Is.EqualTo(new Uuid80(41, ulong.MaxValue - 23)));
			Assert.That(uid - 123UL, Is.EqualTo(new Uuid80(41, ulong.MaxValue - 23)));
		}

		[Test]
		public void Test_Uuid80_Read_From_Bytes()
		{
			// test buffer with included padding
			byte[] buf = { 0x55, 0x55, 0x55, 0x55, /* start */ 0x1E, 0x2D, 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF, /* stop */ 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA };
			var original = Uuid80.Parse("1E2D-01234567-89ABCDEF");
			(var hi, var lo) = original;
			Assume.That(hi, Is.EqualTo(0x1E2D));
			Assume.That(lo, Is.EqualTo(0x0123456789ABCDEF));

			// ReadOnlySpan<byte>
			Assert.That(Uuid80.Read(buf.AsSpan(4, 10)), Is.EqualTo(original));

			// Slice
			Assert.That(Uuid80.Read(buf.AsSlice(4, 10)), Is.EqualTo(original));

			// byte[]
			Assert.That(Uuid80.Read(buf.AsSlice(4, 10).ToArray()), Is.EqualTo(original));

			unsafe
			{
				fixed (byte* ptr = &buf[4])
				{
					Assert.That(Uuid80.Read(new ReadOnlySpan<byte>(ptr, 10)), Is.EqualTo(original));
				}
			}
		}

		[Test]
		public void Test_Uuid80_WriteTo()
		{
			static byte[] Repeat(byte value, int count)
			{
				var tmp = new byte[count];
				tmp.AsSpan().Fill(value);
				return tmp;
			}

			var original = Uuid80.Parse("1E2D-01234567-89ABCDEF");
			var (hi, lo) = original;
			Assume.That(hi, Is.EqualTo(0x1E2D));
			Assume.That(lo, Is.EqualTo(0x0123456789ABCDEF));

			// span with more space
			var scratch = Repeat(0xAA, 20);
			original.WriteTo(scratch.AsSpan());
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("1E 2D 01 23 45 67 89 AB CD EF AA AA AA AA AA AA AA AA AA AA"));

			// span with no offset and exact size
			scratch = Repeat(0xAA, 20);
			original.WriteTo(scratch.AsSpan(0, 10));
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("1E 2D 01 23 45 67 89 AB CD EF AA AA AA AA AA AA AA AA AA AA"));

			// span with offset
			scratch = Repeat(0xAA, 20);
			original.WriteTo(scratch.AsSpan(4));
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("AA AA AA AA 1E 2D 01 23 45 67 89 AB CD EF AA AA AA AA AA AA"));

			// span with offset and exact size
			scratch = Repeat(0xAA, 20);
			original.WriteTo(scratch.AsSpan(4, 10));
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("AA AA AA AA 1E 2D 01 23 45 67 89 AB CD EF AA AA AA AA AA AA"));

			// errors

			Assert.That(() => original.WriteTo(Span<byte>.Empty), Throws.InstanceOf<ArgumentException>(), "Target buffer is empty");

			scratch = Repeat(0xAA, 16);
			Assert.That(() => original.WriteTo(scratch.AsSpan(0, 9)), Throws.InstanceOf<ArgumentException>(), "Target buffer is too small");
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA"), "Buffer should not have been overwritten!");
		}

		[Test]
		public void Test_Uuid80_TryWriteTo()
		{
			static byte[] Repeat(byte value, int count)
			{
				var tmp = new byte[count];
				tmp.AsSpan().Fill(value);
				return tmp;
			}

			var original = Uuid80.Parse("1E2D-01234567-89ABCDEF");
			var (hi, lo) = original;
			Assume.That(hi, Is.EqualTo(0x1E2D));
			Assume.That(lo, Is.EqualTo(0x0123456789ABCDEF));

			// span with more space
			var scratch = Repeat(0xAA, 20);
			Assert.That(original.TryWriteTo(scratch.AsSpan()), Is.True);
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("1E 2D 01 23 45 67 89 AB CD EF AA AA AA AA AA AA AA AA AA AA"));

			// span with no offset and exact size
			scratch = Repeat(0xAA, 20);
			Assert.That(original.TryWriteTo(scratch.AsSpan(0, 10)), Is.True);
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("1E 2D 01 23 45 67 89 AB CD EF AA AA AA AA AA AA AA AA AA AA"));

			// span with offset
			scratch = Repeat(0xAA, 20);
			Assert.That(original.TryWriteTo(scratch.AsSpan(4)), Is.True);
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("AA AA AA AA 1E 2D 01 23 45 67 89 AB CD EF AA AA AA AA AA AA"));

			// span with offset and exact size
			scratch = Repeat(0xAA, 20);
			Assert.That(original.TryWriteTo(scratch.AsSpan(4, 10)), Is.True);
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("AA AA AA AA 1E 2D 01 23 45 67 89 AB CD EF AA AA AA AA AA AA"));

			// errors

			Assert.That(original.TryWriteTo(Span<byte>.Empty), Is.False, "Target buffer is empty");

			scratch = Repeat(0xAA, 20);
			Assert.That(original.TryWriteTo(scratch.AsSpan(0, 9)), Is.False, "Target buffer is too small");
			Assert.That(scratch.AsSlice().ToString("X"), Is.EqualTo("AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA AA"), "Buffer should not have been overwritten!");

		}

	}

}
