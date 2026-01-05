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

// ReSharper disable AccessToDisposedClosure
// ReSharper disable AccessToModifiedClosure
// ReSharper disable ImplicitlyCapturedClosure
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
namespace SnowBank.Buffers.Text.Tests
{
	using SnowBank.Text;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class Utf8StringFacts : SimpleTest
	{

		[Test]
		public void Test_Nil_String()
		{
			var str = Utf8String.Nil;
			Assert.That(str.Length, Is.EqualTo(0));
			Assert.That(str.ToString(), Is.EqualTo(string.Empty));
			Assert.That(str.GetHashCode(), Is.EqualTo(0));
			Assert.That(str.IsNull, Is.True);
			Assert.That(str.IsNullOrEmpty, Is.True);
			Assert.That(str.IsAscii, Is.True);

			Assert.That(str.ToString(null), Is.EqualTo(string.Empty));
			Assert.That(str.ToString(null, null), Is.EqualTo(string.Empty));
			Assert.That(str.ToString("D", null), Is.EqualTo(string.Empty));
			Assert.That(str.ToString("P", null), Is.EqualTo("null"));

			using (var it = str.GetEnumerator())
			{
				Assert.That(it.MoveNext(), Is.False);
			}

			Assert.That(str.Equals(default(Utf8String)), Is.True);
			Assert.That(str.Equals(string.Empty), Is.True);
			Assert.That(str.Equals(Slice.Nil), Is.True);
		}

		[Test]
		public void Test_Empty_String()
		{
			var str = Utf8String.Empty;
			Assert.That(str.Length, Is.EqualTo(0));
			Assert.That(str.ToString(), Is.EqualTo(string.Empty));
			Assert.That(str.GetHashCode(), Is.EqualTo(0));
			Assert.That(str.IsNull, Is.False);
			Assert.That(str.IsNullOrEmpty, Is.True);
			Assert.That(str.IsAscii, Is.True);

			Assert.That(str.ToString(null), Is.EqualTo(string.Empty));
			Assert.That(str.ToString(null, null), Is.EqualTo(string.Empty));
			Assert.That(str.ToString("D", null), Is.EqualTo(string.Empty));
			Assert.That(str.ToString("P", null), Is.EqualTo(@""""""));

			using (var it = str.GetEnumerator())
			{
				Assert.That(it.MoveNext(), Is.False);
			}

			Assert.That(str.Equals(default(Utf8String)), Is.False);
			Assert.That(str.Equals(Utf8String.Empty), Is.True);
			Assert.That(str.Equals(string.Empty), Is.True);
			Assert.That(str.Equals(Slice.Empty), Is.True);
		}

		[Test]
		public void Test_String_Only_Ascii_Chars()
		{
			const string TEXT = "Hello, World!";
			var data = Slice.FromString(TEXT);

			var str = Utf8String.FromBuffer(data);
			Assert.That(str.Length, Is.EqualTo(TEXT.Length));
			Assert.That(str.ToString(), Is.EqualTo(TEXT));
			Assert.That(str.IsAscii, Is.True);
			Assert.That(str.GetCachedHashCode(), Is.Not.Zero);
			Assert.That(str.GetHashCode(), Is.EqualTo(str.GetCachedHashCode()));

			Assert.That(str.ToString(null), Is.EqualTo(TEXT));
			Assert.That(str.ToString(null, null), Is.EqualTo(TEXT));
			Assert.That(str.ToString("D", null), Is.EqualTo(TEXT));
			Assert.That(str.ToString("P"), Is.EqualTo(@"""Hello, World!"""));

			Assert.That(str.ToCharArray(), Is.EqualTo(TEXT.ToCharArray()));

			var chars = str.Select(cp => (char) cp).ToArray();
			Assert.That(chars, Is.EqualTo(TEXT.ToCharArray()));

			using (var it = str.GetEnumerator())
			{
				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo('H'));
				Assert.That(it.ByteOffset, Is.EqualTo(0));
				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo('e'));
				Assert.That(it.ByteOffset, Is.EqualTo(1));
				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo('l'));
				Assert.That(it.ByteOffset, Is.EqualTo(2));
				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo('l'));
				Assert.That(it.ByteOffset, Is.EqualTo(3));
				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo('o'));
				Assert.That(it.ByteOffset, Is.EqualTo(4));
				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo(','));
				Assert.That(it.ByteOffset, Is.EqualTo(5));
				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo(' '));
				Assert.That(it.ByteOffset, Is.EqualTo(6));
				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo('W'));
				Assert.That(it.ByteOffset, Is.EqualTo(7));
				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo('o'));
				Assert.That(it.ByteOffset, Is.EqualTo(8));
				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo('r'));
				Assert.That(it.ByteOffset, Is.EqualTo(9));
				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo('l'));
				Assert.That(it.ByteOffset, Is.EqualTo(10));
				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo('d'));
				Assert.That(it.ByteOffset, Is.EqualTo(11));
				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo('!'));
				Assert.That(it.ByteOffset, Is.EqualTo(12));
				Assert.That(it.MoveNext(), Is.False);

				it.Reset();

				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo('H'));
				Assert.That(it.ByteOffset, Is.EqualTo(0));
			}

			// compute length
			Assert.That(Utf8Encoder.TryGetLength(data, out int len), Is.True, "Should be able to get length of ASCII string");
			Assert.That(len, Is.EqualTo(TEXT.Length), "Should be able to get length of ASCII string");

			// with no hash code
			Assert.That(Utf8String.FromBuffer(data, noHashCode: true).GetCachedHashCode(), Is.Zero, "Utf8String.GetCachedHashCode() with noHashCode:true should return 0");
			Assert.That(Utf8String.FromBuffer(data, noHashCode: true).GetHashCode(), Is.Not.Zero.And.EqualTo(str.GetHashCode()), "Utf8String.GetHashCode() with noHashCode:true should compute the hashcode on the fly");
		}

		[Test]
		public void Test_String_With_Unicode_Chars()
		{
			const string TEXT = "Héllø, 世界!";
			var data = Slice.FromString(TEXT);

			var str = Utf8String.FromBuffer(data);
			Assert.That(str.Length, Is.EqualTo(TEXT.Length));
			Assert.That(str.ToString(), Is.EqualTo(TEXT));

			Assert.That(str.IsAscii, Is.False);
			Assert.That(str.GetCachedHashCode(), Is.Not.Zero);
			Assert.That(str.GetHashCode(), Is.Not.Zero.And.EqualTo(str.GetCachedHashCode()));

			Assert.That(str.ToString(null), Is.EqualTo(TEXT));
			Assert.That(str.ToString(null, null), Is.EqualTo(TEXT));
			Assert.That(str.ToString("D", null), Is.EqualTo(TEXT));
			Assert.That(str.ToString("P"), Is.EqualTo(@"""H\xe9ll\xf8, \u4e16\u754c!"""));

			Assert.That(str.ToCharArray(), Is.EqualTo(TEXT.ToCharArray()));

			using (var it = str.GetEnumerator())
			{
				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo('H'));
				Assert.That(it.ByteOffset, Is.EqualTo(0));
				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo('é'));
				Assert.That(it.ByteOffset, Is.EqualTo(1));
				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo('l'));
				Assert.That(it.ByteOffset, Is.EqualTo(3));
				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo('l'));
				Assert.That(it.ByteOffset, Is.EqualTo(4));
				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo('ø'));
				Assert.That(it.ByteOffset, Is.EqualTo(5));
				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo(','));
				Assert.That(it.ByteOffset, Is.EqualTo(7));
				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo(' '));
				Assert.That(it.ByteOffset, Is.EqualTo(8));
				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo('世'));
				Assert.That(it.ByteOffset, Is.EqualTo(9));
				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo('界'));
				Assert.That(it.ByteOffset, Is.EqualTo(12));
				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo('!'));
				Assert.That(it.ByteOffset, Is.EqualTo(15));
				Assert.That(it.MoveNext(), Is.False);
				Assert.That(it.ByteOffset, Is.EqualTo(16));

				it.Reset();

				Assert.That(it.MoveNext(), Is.True);
				Assert.That((char) it.Current, Is.EqualTo('H'));
				Assert.That(it.ByteOffset, Is.EqualTo(0));
			}

			var chars = str.Select(cp => (char) cp).ToArray();
			Assert.That(chars, Is.EqualTo(TEXT.ToCharArray()));

			// compute length
			Assert.That(Utf8Encoder.TryGetLength(data, out int len), Is.True, "Should be able to get length of UTF-8 string");
			Assert.That(len, Is.EqualTo(TEXT.Length), "Should be able to get length of UTF-8 string");

			// with no hash code
			Assert.That(Utf8String.FromBuffer(data, noHashCode: true).GetCachedHashCode(), Is.Zero, "Utf8String.GetCachedHashCode() with noHashCode:true should return 0");
			Assert.That(Utf8String.FromBuffer(data, noHashCode: true).GetHashCode(), Is.Not.Zero.And.EqualTo(str.GetHashCode()), "Utf8String.GetHashCode() with noHashCode:true should compute the hashcode on the fly");
		}

		[Test]
		public void Test_String_With_Invalid_UTF8_Code_Units()
		{
			var data = Slice.Unescape("<FF><FF>");
			Assert.That(() => Utf8String.FromBuffer(data), Throws.InstanceOf<DecoderFallbackException>()); //TODO: which type of exception?
			data = Slice.Unescape("Hell<FF><FF>, World!");
			Assert.That(() => Utf8String.FromBuffer(data), Throws.InstanceOf<DecoderFallbackException>()); //TODO: which type of exception?
			data = Slice.Unescape("Hello, World!<FF><FF>");
			Assert.That(() => Utf8String.FromBuffer(data), Throws.InstanceOf<DecoderFallbackException>()); //TODO: which type of exception?

			// compute length
			Assert.That(Utf8Encoder.TryGetLength(data, out int _), Is.False, "Should not be able to get length of invalid UTF-8 string");

		}

		[Test]
		public void Test_String_With_Utf8Bom()
		{
			// with
			var data = Slice.Unescape("<EF><BB><BF>Hello, World!");
			var str = Utf8String.FromBuffer(data, discardBom: false);
			string txt = str.ToString();
			Assert.That(txt[0], Is.EqualTo('\uFEFF'), "Decoded string should start with the Unicode BOM");
			Assert.That(txt, Is.EqualTo("\uFEFFHello, World!"));
			Assert.That(str.Length, Is.EqualTo(13 + 1));
			Assert.That(str.GetBuffer().Array, Is.SameAs(data.Array));
			Assert.That(str.GetBuffer().Offset, Is.EqualTo(data.Offset));
			Assert.That(str.GetBuffer().Count, Is.EqualTo(data.Count));

			// without
			str = Utf8String.FromBuffer(data, discardBom: true);
			txt = str.ToString();
			Assert.That(txt[0], Is.Not.EqualTo('\uFEFF'), "Decoded string should not start with the Unicode BOM");
			Assert.That(txt, Is.EqualTo("Hello, World!"));
			Assert.That(str.Length, Is.EqualTo(13));
			Assert.That(str.GetBuffer().Array, Is.SameAs(data.Array));
			Assert.That(str.GetBuffer().Offset, Is.EqualTo(data.Offset + 3));
			Assert.That(str.GetBuffer().Count, Is.EqualTo(data.Count - 3));
		}

		[Test]
		public void Test_FromString()
		{
			Assert.That(Utf8String.FromString(default(string)), Is.EqualTo(Utf8String.Nil));
			Assert.That(Utf8String.FromString(string.Empty), Is.EqualTo(Utf8String.Empty));

			Assert.That(Utf8String.FromString("A").GetBytes(), Is.EqualTo(new byte[] { 65 }));
			Assert.That(Utf8String.FromString("Hello").GetBytes(), Is.EqualTo("Hello"u8.ToArray()));
			Assert.That(Utf8String.FromString("Héllö").GetBytes(), Is.EqualTo("Héllö"u8.ToArray()));

			Assert.That(Utf8String.FromString("Héllø, 世界!".AsSpan(0, 5)).GetBytes(), Is.EqualTo("Héllø"u8.ToArray()));
			Assert.That(Utf8String.FromString("Héllø, 世界!".AsSpan(7, 2)).GetBytes(), Is.EqualTo("世界"u8.ToArray()));
			Assert.That(Utf8String.FromString("Héllø, 世界!".AsSpan(0, 10)).GetBytes(), Is.EqualTo("Héllø, 世界!"u8.ToArray()));

			// pre-allocated buffer

			byte[]? tmp = null;
			var str = Utf8String.FromString("Hello, World!".AsSpan(), ref tmp);
			Assert.That(str.ToString(), Is.EqualTo("Hello, World!"));
			Assert.That(tmp, Is.Not.Null);
			Assert.That(str.GetBuffer().Array, Is.SameAs(tmp));

			// reuse buffer if large enough
			byte[] orig = tmp!;
			str = Utf8String.FromString("FFFFöööööööööööö!!!!".AsSpan(3, 4), ref tmp);
			Assert.That(str.ToString(), Is.EqualTo("Fööö"));
			Assert.That(tmp, Is.SameAs(orig));
			Assert.That(str.GetBuffer().Array, Is.SameAs(orig));

			// cannot reuse buffer if too small
			str = Utf8String.FromString("This is a test of the emergency broadcast system!".AsSpan(), ref tmp);
			Assert.That(str.ToString(), Is.EqualTo("This is a test of the emergency broadcast system!"));
			Assert.That(tmp, Is.Not.SameAs(orig));
			Assert.That(str.GetBuffer().Array, Is.SameAs(tmp));
		}

		[Test]
		public void Test_FromBuffer()
		{
			Assert.That(Utf8String.FromBuffer(Slice.Nil), Is.EqualTo(Utf8String.Nil));
			Assert.That(Utf8String.FromBuffer(Slice.Empty), Is.EqualTo(Utf8String.Empty));

			Assert.That(Utf8String.FromBuffer(Slice.FromString("A")).ToString(), Is.EqualTo("A"));
			Assert.That(Utf8String.FromBuffer(Slice.FromString("Hello")).ToString(), Is.EqualTo("Hello"));
			Assert.That(Utf8String.FromBuffer(Slice.FromString("Héllö")).ToString(), Is.EqualTo("Héllö"));

			Assert.That(Utf8String.FromBuffer(Slice.FromString("ZZZHelloXXX").Substring(3, 1 + 1 + 1 + 1 + 1)).ToString(), Is.EqualTo("Hello"));
			Assert.That(Utf8String.FromBuffer(Slice.FromString("ZZZHéllöXXX").Substring(3, 1 + 2 + 1 + 1 + 2)).ToString(), Is.EqualTo("Héllö"));
		}

		[Test]
		public void Test_Plus_Operator()
		{
			// Utf8String + Utf8String
			Assert.That(Utf8String.FromString("Hello, ") + Utf8String.FromString("World!"), Is.EqualTo(Utf8String.FromString("Hello, World!")));
			Assert.That(Utf8String.FromString("Héllö, ") + Utf8String.FromString("世界!"), Is.EqualTo(Utf8String.FromString("Héllö, 世界!")));
			Assert.That(Utf8String.FromString("Héllö") + Utf8String.Empty, Is.EqualTo(Utf8String.FromString("Héllö")));
			Assert.That(Utf8String.Empty + Utf8String.FromString("Héllö"), Is.EqualTo(Utf8String.FromString("Héllö")));

			// Utf8String + string
			Assert.That(Utf8String.FromString("Hello, ") + "World!", Is.EqualTo(Utf8String.FromString("Hello, World!")));
			Assert.That(Utf8String.FromString("Héllö, ") + "世界!", Is.EqualTo(Utf8String.FromString("Héllö, 世界!")));
			Assert.That(Utf8String.FromString("Héllö") + string.Empty, Is.EqualTo(Utf8String.FromString("Héllö")));
			Assert.That(Utf8String.Empty + "Héllö", Is.EqualTo(Utf8String.FromString("Héllö")));
		}

		[Test]
		public void Test_IndexOf()
		{
			const int NOT_FOUND = -1;
			// IndexOf(Utf8String, ...)
			//TODO!

			// IndexOf(string, ...)
			//TODO!

			// IndexOf(char, ...)
			{
				var str = Utf8String.FromString("Hello, World!");
				Assert.That(str.IndexOf('o'), Is.EqualTo(4));
				Assert.That(str.IndexOf('o', 4), Is.EqualTo(4));
				Assert.That(str.IndexOf('o', 5), Is.EqualTo(8));
				Assert.That(str.IndexOf('o', 9), Is.EqualTo(NOT_FOUND));
				Assert.That(str.IndexOf('O'), Is.EqualTo(NOT_FOUND));
				Assert.That(str.IndexOf(','), Is.EqualTo(5));
				Assert.That(str.IndexOf(',', 4), Is.EqualTo(5));
				Assert.That(str.IndexOf(',', 6), Is.EqualTo(NOT_FOUND));
				Assert.That(str.IndexOf('Z'), Is.EqualTo(NOT_FOUND));
				Assert.That(str.IndexOf('Z', 4), Is.EqualTo(NOT_FOUND));
				Assert.That(str.IndexOf('無'), Is.EqualTo(NOT_FOUND));
				Assert.That(str.IndexOf('無', 4), Is.EqualTo(NOT_FOUND));

				Assert.That(() => str.IndexOf('o', -1), Throws.InstanceOf<ArgumentException>());
				Assert.That(() => str.IndexOf('o', 13), Throws.InstanceOf<ArgumentException>());
				Assert.That(() => str.IndexOf('o', 14), Throws.InstanceOf<ArgumentException>());
			}

			{
				var str = Utf8String.FromString("Héllo, 世界!");
				Assert.That(str.IndexOf('é'), Is.EqualTo(1));
				Assert.That(str.IndexOf('é', 1), Is.EqualTo(1));
				Assert.That(str.IndexOf('é', 2), Is.EqualTo(NOT_FOUND));
				Assert.That(str.IndexOf('o'), Is.EqualTo(4));
				Assert.That(str.IndexOf('o', 4), Is.EqualTo(4));
				Assert.That(str.IndexOf('o', 5), Is.EqualTo(NOT_FOUND));
				Assert.That(str.IndexOf('O', 0), Is.EqualTo(NOT_FOUND));
				Assert.That(str.IndexOf('世'), Is.EqualTo(7));
				Assert.That(str.IndexOf('界'), Is.EqualTo(8));
				Assert.That(str.IndexOf('界', 7), Is.EqualTo(8));
				Assert.That(str.IndexOf('界', 9), Is.EqualTo(NOT_FOUND));
				Assert.That(str.IndexOf('無'), Is.EqualTo(NOT_FOUND));
				Assert.That(str.IndexOf('無', 4), Is.EqualTo(NOT_FOUND));

				Assert.That(() => str.IndexOf('界', -1), Throws.InstanceOf<ArgumentException>());
				Assert.That(() => str.IndexOf('界', 10), Throws.InstanceOf<ArgumentException>());
				Assert.That(() => str.IndexOf('界', 11), Throws.InstanceOf<ArgumentException>());
			}
		}

		[Test]
		public void Test_Contains()
		{
			// Contains(Utf8String, ...)
			//TODO!

			// Contains(string, ...)
			//TODO!

			// Contains(char, ...)
			Assert.That(Utf8String.FromString("Hello, World!").Contains('o'), Is.True);
			Assert.That(Utf8String.FromString("Hello, World!").Contains(','), Is.True);
			Assert.That(Utf8String.FromString("Hello, World!").Contains('Z'), Is.False);

			Assert.That(Utf8String.FromString("Hello, World!").Contains('o', 0), Is.True);
			Assert.That(Utf8String.FromString("Hello, World!").Contains('o', 4), Is.True);
			Assert.That(Utf8String.FromString("Hello, World!").Contains('o', 5), Is.True);
			Assert.That(Utf8String.FromString("Hello, World!").Contains('o', 9), Is.False);
			Assert.That(Utf8String.FromString("Hello, World!").Contains(',', 4), Is.True);
			Assert.That(Utf8String.FromString("Hello, World!").Contains(',', 6), Is.False);
			Assert.That(Utf8String.FromString("Hello, World!").Contains('Z', 0), Is.False);

			Assert.That(Utf8String.FromString("Héllo, 世界!").Contains('é'), Is.True);
			Assert.That(Utf8String.FromString("Héllo, 世界!").Contains('o'), Is.True);
			Assert.That(Utf8String.FromString("Héllo, 世界!").Contains('界'), Is.True);
			Assert.That(Utf8String.FromString("Héllo, 世界!").Contains('無'), Is.False);

			Assert.That(Utf8String.FromString("ZZZZHello, World!").Substring(4).Contains('Z'), Is.False);
			Assert.That(Utf8String.FromString("Hello, World!").Substring(4).Contains('e'), Is.False);
		}

		[Test]
		public void Test_StartsWith()
		{
			{
				const string TEXT = "Hello, World!";
				var data = Slice.FromString(TEXT);
				var str = Utf8String.FromBuffer(data);

				// StartsWith(string)

				Assert.That(str.StartsWith(default(string)), Is.True);
				Assert.That(str.StartsWith(string.Empty), Is.True);
				Assert.That(str.StartsWith("H"), Is.True);
				Assert.That(str.StartsWith("He"), Is.True);
				Assert.That(str.StartsWith("Hello"), Is.True);
				Assert.That(str.StartsWith("Hello, World"), Is.True);
				Assert.That(str.StartsWith("Hello, World!"), Is.True);

				Assert.That(str.StartsWith("hello"), Is.False);
				Assert.That(str.StartsWith("HEllo"), Is.False);
				Assert.That(str.StartsWith("Hello;"), Is.False);
				Assert.That(str.StartsWith("Hello, World!!"), Is.False);

				// StartsWith(Utf8String)

				Assert.That(str.StartsWith(default(Utf8String)), Is.True);
				Assert.That(str.StartsWith(Utf8String.Empty), Is.True);
				Assert.That(str.StartsWith(Utf8String.FromString("H")), Is.True);
				Assert.That(str.StartsWith(Utf8String.FromString("He")), Is.True);
				Assert.That(str.StartsWith(Utf8String.FromString("Hello")), Is.True);
				Assert.That(str.StartsWith(Utf8String.FromString("Hello, World")), Is.True);
				Assert.That(str.StartsWith(Utf8String.FromString("Hello, World!")), Is.True);

				Assert.That(str.StartsWith(Utf8String.FromString("hello")), Is.False);
				Assert.That(str.StartsWith(Utf8String.FromString("Héllo")), Is.False);
				Assert.That(str.StartsWith(Utf8String.FromString("Hello;")), Is.False);
				Assert.That(str.StartsWith(Utf8String.FromString("Hello, World!!")), Is.False);
				Assert.That(str.StartsWith(Utf8String.FromString("\uFEFFHello")), Is.False);
			}

			{
				const string TEXT = "Héllø, 世界!";
				var data = Slice.FromString(TEXT);
				var str = Utf8String.FromBuffer(data);

				// StartsWith(string)

				Assert.That(str.StartsWith(default(string)), Is.True);
				Assert.That(str.StartsWith(string.Empty), Is.True);
				Assert.That(str.StartsWith("H"), Is.True);
				Assert.That(str.StartsWith("Hé"), Is.True);
				Assert.That(str.StartsWith("Héllø"), Is.True);
				Assert.That(str.StartsWith("Héllø, 世界"), Is.True);
				Assert.That(str.StartsWith("Héllø, 世界!"), Is.True);

				Assert.That(str.StartsWith("hé"), Is.False);
				Assert.That(str.StartsWith("HéL"), Is.False);
				Assert.That(str.StartsWith("Héllø;"), Is.False);
				Assert.That(str.StartsWith("Héllø, 世界!!"), Is.False);

				// StartsWith(string, int, int)
				// ReSharper disable once AssignNullToNotNullAttribute
				Assert.That(str.StartsWith(default(string), 0, 0), Is.True);
				Assert.That(str.StartsWith(string.Empty, 0, 0), Is.True);
				Assert.That(str.StartsWith("********", 4, 0), Is.True);
				Assert.That(str.StartsWith("****H****", 4, 1), Is.True);
				Assert.That(str.StartsWith("****Héllø****", 4, 5), Is.True);
				Assert.That(str.StartsWith("****Héllø, 世界!****", 4, 10), Is.True);

				Assert.That(str.StartsWith("****X****", 4, 1), Is.False);
				Assert.That(str.StartsWith("****H****", 4, 2), Is.False);
				Assert.That(str.StartsWith("****Hellø****", 4, 5), Is.False);
				Assert.That(str.StartsWith("****Héllø, 世界!!****", 4, 11), Is.False);

				// StartsWith(Utf8String)

				Assert.That(str.StartsWith(default(Utf8String)), Is.True);
				Assert.That(str.StartsWith(Utf8String.Empty), Is.True);
				Assert.That(str.StartsWith(Utf8String.FromString("H")), Is.True);
				Assert.That(str.StartsWith(Utf8String.FromString("Hé")), Is.True);
				Assert.That(str.StartsWith(Utf8String.FromString("Héllø")), Is.True);
				Assert.That(str.StartsWith(Utf8String.FromString("Héllø, 世界")), Is.True);
				Assert.That(str.StartsWith(Utf8String.FromString("Héllø, 世界!")), Is.True);

				Assert.That(str.StartsWith(Utf8String.FromString("hellø")), Is.False);
				Assert.That(str.StartsWith(Utf8String.FromString("HéLlø")), Is.False);
				Assert.That(str.StartsWith(Utf8String.FromString("Hellø;")), Is.False);
				Assert.That(str.StartsWith(Utf8String.FromString("Hellø, 世界!!")), Is.False);
				Assert.That(str.StartsWith(Utf8String.FromString("\uFEFFHellø")), Is.False);
			}
		}

		[Test]
		public void Test_EndsWith()
		{
			{ // String with only ASCII
				const string TEXT = "Hello, World!";
				var data = Slice.FromString(TEXT);
				var str = Utf8String.FromBuffer(data);

				// EndsWith(string)

				Assert.That(str.EndsWith(default(string)), Is.True);
				Assert.That(str.EndsWith(string.Empty), Is.True);
				Assert.That(str.EndsWith("!"), Is.True);
				Assert.That(str.EndsWith("d!"), Is.True);
				Assert.That(str.EndsWith("World!"), Is.True);
				Assert.That(str.EndsWith(", World!"), Is.True);
				Assert.That(str.EndsWith("Hello, World!"), Is.True);

				Assert.That(str.EndsWith("world!"), Is.False);
				Assert.That(str.EndsWith("Wörld!"), Is.False);
				Assert.That(str.EndsWith("World!;"), Is.False);
				Assert.That(str.EndsWith("Hello, World!!"), Is.False);

				// EndsWith(Utf8String)

				Assert.That(str.EndsWith(default(Utf8String)), Is.True);
				Assert.That(str.EndsWith(Utf8String.Empty), Is.True);
				Assert.That(str.EndsWith(Utf8String.FromString("!")), Is.True);
				Assert.That(str.EndsWith(Utf8String.FromString("d!")), Is.True);
				Assert.That(str.EndsWith(Utf8String.FromString("World!")), Is.True);
				Assert.That(str.EndsWith(Utf8String.FromString(", World!")), Is.True);
				Assert.That(str.EndsWith(Utf8String.FromString("Hello, World!")), Is.True);

				Assert.That(str.EndsWith(Utf8String.FromString("world!")), Is.False);
				Assert.That(str.EndsWith(Utf8String.FromString("Wörld!")), Is.False);
				Assert.That(str.EndsWith(Utf8String.FromString("World!;")), Is.False);
				Assert.That(str.EndsWith(Utf8String.FromString("Hello, World!!")), Is.False);
				Assert.That(str.EndsWith(Utf8String.FromString("\uFEFFWorld!")), Is.False);
			}

			{ // String with UTF-8 code points
				const string TEXT = "Héllø, 世界!";
				var data = Slice.FromString(TEXT);
				var str = Utf8String.FromBuffer(data);

				// StartsWith(string)

				Assert.That(str.EndsWith(default(string)), Is.True);
				Assert.That(str.EndsWith(string.Empty), Is.True);
				Assert.That(str.EndsWith("!"), Is.True);
				Assert.That(str.EndsWith("界!"), Is.True);
				Assert.That(str.EndsWith("世界!"), Is.True);
				Assert.That(str.EndsWith(", 世界!"), Is.True);
				Assert.That(str.EndsWith("Héllø, 世界!"), Is.True);

				Assert.That(str.EndsWith("héllø, 世界!"), Is.False);
				Assert.That(str.EndsWith("HéLLø, 世界!"), Is.False);
				Assert.That(str.EndsWith("世界!;"), Is.False);
				Assert.That(str.EndsWith("Héllø, 世界!!"), Is.False);

				// StartsWith(string, int, int)
				// ReSharper disable once AssignNullToNotNullAttribute
				Assert.That(str.EndsWith(default(string), 0, 0), Is.True);
				Assert.That(str.EndsWith(string.Empty, 0, 0), Is.True);
				Assert.That(str.EndsWith("********", 4, 0), Is.True);
				Assert.That(str.EndsWith("****!****", 4, 1), Is.True);
				Assert.That(str.EndsWith("****世界!****", 4, 3), Is.True);
				Assert.That(str.EndsWith("****Héllø, 世界!****", 4, 10), Is.True);

				Assert.That(str.EndsWith("****X****", 4, 1), Is.False);
				Assert.That(str.EndsWith("****!****", 4, 2), Is.False);
				Assert.That(str.EndsWith("****世界****", 4, 2), Is.False);
				Assert.That(str.EndsWith("****Héllø, 世界!!****", 4, 11), Is.False);

				// StartsWith(Utf8String)

				Assert.That(str.EndsWith(default(Utf8String)), Is.True);
				Assert.That(str.EndsWith(Utf8String.Empty), Is.True);
				Assert.That(str.EndsWith(Utf8String.FromString("!")), Is.True);
				Assert.That(str.EndsWith(Utf8String.FromString("界!")), Is.True);
				Assert.That(str.EndsWith(Utf8String.FromString("世界!")), Is.True);
				Assert.That(str.EndsWith(Utf8String.FromString(", 世界!")), Is.True);
				Assert.That(str.EndsWith(Utf8String.FromString("Héllø, 世界!")), Is.True);

				Assert.That(str.EndsWith(Utf8String.FromString("héllø, 世界!")), Is.False);
				Assert.That(str.EndsWith(Utf8String.FromString("HéLLø, 世界!")), Is.False);
				Assert.That(str.EndsWith(Utf8String.FromString("世界!;")), Is.False);
				Assert.That(str.EndsWith(Utf8String.FromString("Hellø, 世界!!")), Is.False);
				Assert.That(str.EndsWith(Utf8String.FromString("\uFEFF世界!")), Is.False);
			}
		}

		[Test]
		public void Test_Substring()
		{
			{ // Empty
				Assert.That(Utf8String.Empty.Substring(0), Is.EqualTo(Utf8String.Empty));
				Assert.That(() => Utf8String.Empty.Substring(1), Throws.InstanceOf<ArgumentException>());
				Assert.That(() => Utf8String.Empty.Substring(-1), Throws.InstanceOf<ArgumentException>());
			}

			{ // ASCII only

				const string TXT = "Hello, World!";
				var str = Utf8String.FromString(TXT);
				Assume.That(str.Length, Is.EqualTo(TXT.Length));
				Assume.That(str.ToString(), Is.EqualTo(TXT));
				Assume.That(str.GetBuffer().Count, Is.EqualTo(TXT.Length));

				var sub = str.Substring(0);
				Assert.That(sub.Length, Is.EqualTo(str.Length));
				Assert.That(sub.ToString(), Is.EqualTo(TXT));
				Assert.That(sub.GetBuffer(), Is.EqualTo(str.GetBuffer()));

				sub = str.Substring(1);
				Assert.That(sub.Length, Is.EqualTo(str.Length - 1));
				Assert.That(sub.ToString(), Is.EqualTo("ello, World!"));
				Assert.That(sub.GetBuffer(), Is.EqualTo(str.GetBuffer().Substring(1)));

				sub = str.Substring(7);
				Assert.That(sub.Length, Is.EqualTo(str.Length - 7));
				Assert.That(sub.ToString(), Is.EqualTo("World!"));
				Assert.That(sub.GetBuffer(), Is.EqualTo(str.GetBuffer().Substring(7)));

				sub = str.Substring(TXT.Length);
				Assert.That(sub.Length, Is.EqualTo(0));
				Assert.That(sub.ToString(), Is.EqualTo(String.Empty));
				Assert.That(sub.GetBuffer(), Is.EqualTo(Slice.Empty));

				Assert.That(() => str.Substring(-1), Throws.Exception);
				Assert.That(() => str.Substring(TXT.Length + 1), Throws.Exception);
			}

			{ // With Unicode

				const string TXT = "Héllø, 世界!";
				var str = Utf8String.FromString(TXT);
				Assume.That(str.Length, Is.EqualTo(TXT.Length));
				Assume.That(str.ToString(), Is.EqualTo(TXT));
				Assume.That(str.GetBuffer().Count, Is.GreaterThan(TXT.Length));

				var sub = str.Substring(0);
				Assert.That(sub.Length, Is.EqualTo(str.Length));
				Assert.That(sub.ToString(), Is.EqualTo(TXT));
				Assert.That(sub.GetBuffer(), Is.EqualTo(str.GetBuffer()));

				sub = str.Substring(2);
				Assert.That(sub.Length, Is.EqualTo(str.Length - 2));
				Assert.That(sub.GetBuffer().Array, Is.SameAs(str.GetBuffer().Array));
				Assert.That(sub.GetBuffer().Offset, Is.EqualTo(str.GetBuffer().Offset + 3)); // 3 = UTF8("Hé").Length
				Assert.That(sub.GetBuffer().Count, Is.EqualTo(str.GetBuffer().Count - 3));
				Assert.That(sub.ToString(), Is.EqualTo("llø, 世界!"));
				Assert.That(sub.GetBuffer(), Is.EqualTo(str.GetBuffer().Substring(3)));

				sub = str.Substring(7);
				Assert.That(sub.Length, Is.EqualTo(str.Length - 7));
				Assert.That(sub.ToString(), Is.EqualTo("世界!"));
				Assert.That(sub.GetBuffer(), Is.EqualTo(str.GetBuffer().Substring(9)));

				sub = str.Substring(TXT.Length);
				Assert.That(sub.Length, Is.EqualTo(0));
				Assert.That(sub.ToString(), Is.EqualTo(String.Empty));
				Assert.That(sub.GetBuffer(), Is.EqualTo(Slice.Empty));

				Assert.That(() => str.Substring(-1), Throws.Exception);
				Assert.That(() => str.Substring(TXT.Length + 1), Throws.Exception);
			}

		}

		[Test]
		public void Test_Substring_With_Count()
		{
			{ // Empty
				Assert.That(Utf8String.Empty.Substring(0, 0), Is.EqualTo(Utf8String.Empty));
				Assert.That(() => Utf8String.Empty.Substring(0, 1), Throws.InstanceOf<ArgumentException>());
				Assert.That(() => Utf8String.Empty.Substring(1, 0), Throws.InstanceOf<ArgumentException>());
			}

			{ // ASCII only

				const string TXT = "Hello, World!";
				var str = Utf8String.FromString(TXT);
				Assume.That(str.Length, Is.EqualTo(TXT.Length));
				Assume.That(str.ToString(), Is.EqualTo(TXT));
				Assume.That(str.GetBuffer().Count, Is.EqualTo(TXT.Length));

				var sub = str.Substring(0, TXT.Length);
				Assert.That(sub.Length, Is.EqualTo(str.Length));
				Assert.That(sub.ToString(), Is.EqualTo(TXT));
				Assert.That(sub.GetBuffer(), Is.EqualTo(str.GetBuffer()));

				sub = str.Substring(1, TXT.Length - 1);
				Assert.That(sub.Length, Is.EqualTo(str.Length - 1));
				Assert.That(sub.ToString(), Is.EqualTo("ello, World!"));
				Assert.That(sub.GetBuffer(), Is.EqualTo(str.GetBuffer().Substring(1)));

				sub = str.Substring(0, TXT.Length - 1);
				Assert.That(sub.Length, Is.EqualTo(str.Length - 1));
				Assert.That(sub.ToString(), Is.EqualTo("Hello, World"));
				Assert.That(sub.GetBuffer(), Is.EqualTo(str.GetBuffer().Substring(0, TXT.Length - 1)));

				sub = str.Substring(7, 5);
				Assert.That(sub.Length, Is.EqualTo(5));
				Assert.That(sub.ToString(), Is.EqualTo("World"));
				Assert.That(sub.GetBuffer(), Is.EqualTo(str.GetBuffer().Substring(7, 5)));

				sub = str.Substring(TXT.Length, 0);
				Assert.That(sub.Length, Is.EqualTo(0));
				Assert.That(sub.ToString(), Is.EqualTo(String.Empty));
				Assert.That(sub.GetBuffer(), Is.EqualTo(Slice.Empty));

				Assert.That(() => str.Substring(-1, 1), Throws.Exception);
				Assert.That(() => str.Substring(-1, TXT.Length + 1), Throws.Exception);
				Assert.That(() => str.Substring(TXT.Length + 1, -2), Throws.Exception);
			}

			{ // With Unicode

				const string TXT = "Héllø, 世界!";
				var str = Utf8String.FromString(TXT);
				Assume.That(str.Length, Is.EqualTo(TXT.Length));
				Assume.That(str.ToString(), Is.EqualTo(TXT));
				Assume.That(str.GetBuffer().Count, Is.GreaterThan(TXT.Length));

				var sub = str.Substring(0, TXT.Length);
				Assert.That(sub.Length, Is.EqualTo(str.Length));
				Assert.That(sub.ToString(), Is.EqualTo(TXT));
				Assert.That(sub.GetBuffer(), Is.EqualTo(str.GetBuffer()));

				sub = str.Substring(2, TXT.Length - 2);
				Assert.That(sub.Length, Is.EqualTo(str.Length - 2));
				Assert.That(sub.ToString(), Is.EqualTo("llø, 世界!"));
				Assert.That(sub.GetBuffer(), Is.EqualTo(str.GetBuffer().Substring(3)));

				sub = str.Substring(0, TXT.Length - 2);
				Assert.That(sub.Length, Is.EqualTo(str.Length - 2));
				Assert.That(sub.ToString(), Is.EqualTo("Héllø, 世"));
				Assert.That(sub.GetBuffer(), Is.EqualTo(str.GetBuffer().Substring(0, 12)));

				sub = str.Substring(7, 2);
				Assert.That(sub.Length, Is.EqualTo(2));
				Assert.That(sub.ToString(), Is.EqualTo("世界"));
				Assert.That(sub.GetBuffer(), Is.EqualTo(str.GetBuffer().Substring(9, 6)));

				sub = str.Substring(TXT.Length, 0);
				Assert.That(sub.Length, Is.EqualTo(0));
				Assert.That(sub.ToString(), Is.EqualTo(String.Empty));
				Assert.That(sub.GetBuffer(), Is.EqualTo(Slice.Empty));

				Assert.That(() => str.Substring(-1, 1), Throws.Exception);
				Assert.That(() => str.Substring(-1, TXT.Length + 1), Throws.Exception);
				Assert.That(() => str.Substring(TXT.Length + 1, -2), Throws.Exception);
			}

		}

	}

}
