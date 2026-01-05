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

#pragma warning disable NUnit2009
#pragma warning disable NUnit2010
#pragma warning disable NUnit2043
#pragma warning disable CA1825
#pragma warning disable CA1861
#pragma warning disable CS1718
#pragma warning disable IDE0230
// ReSharper disable AccessToDisposedClosure
// ReSharper disable ImplicitlyCapturedClosure
// ReSharper disable PreferConcreteValueOverDefault
// ReSharper disable RedundantCast
// ReSharper disable RedundantExplicitParamsArrayCreation
// ReSharper disable StringLiteralTypo
// ReSharper disable UseArrayEmptyMethod
// ReSharper disable VariableLengthStringHexEscapeSequence

namespace SnowBank.Buffers.Tests
{

	//README:IMPORTANT! This source file is expected to be stored as UTF-8! If the encoding is changed, some tests below may fail because they rely on specific code points!

	using System.Buffers;
	using System.Diagnostics.CodeAnalysis;
	using System.Runtime.InteropServices;
	using SnowBank.Data.Binary;
	using static SnowBank.Testing.TestVariables;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class SliceFacts : SimpleTest
	{

		[Test]
		public void Test_Slice_Nil()
		{
			// Slice.Nil is the equivalent of 'default(byte[])'

			Assert.That(Slice.Nil.Count, Is.EqualTo(0));
			Assert.That(Slice.Nil.Offset, Is.EqualTo(0));
			Assert.That(Slice.Nil.Array, Is.Null);

			Assert.That(Slice.Nil.IsNull, Is.True);
			Assert.That(Slice.Nil.HasValue, Is.False);
			Assert.That(Slice.Nil.IsEmpty, Is.False);
			Assert.That(Slice.Nil.IsNullOrEmpty, Is.True);
			Assert.That(Slice.Nil.IsPresent, Is.False);

			Assert.That(Slice.Nil.GetBytes(), Is.Null);
			Assert.That(Slice.Nil.ToArray(), Is.Not.Null.And.Length.EqualTo(0));
			Assert.That(Slice.Nil.ToByteString(), Is.Null);
			Assert.That(Slice.Nil.ToUnicode(), Is.Null);
			Assert.That(Slice.Nil.PrettyPrint(), Is.EqualTo(String.Empty));
		}

		[Test]
		public void Test_Slice_Empty()
		{
			// Slice.Empty is the equivalent of 'new byte[0]'

			Assert.That(Slice.Empty.Count, Is.EqualTo(0));
			Assert.That(Slice.Empty.Offset, Is.EqualTo(0));
			Assert.That(Slice.Empty.Array, Is.Not.Null);
			Assert.That(Slice.Empty.Array.Length, Is.GreaterThan(0), "The backing array for Slice.Empty should not be empty, in order to work properly with the fixed() operator!");

			Assert.That(Slice.Empty.IsNull, Is.False);
			Assert.That(Slice.Empty.HasValue, Is.True);
			Assert.That(Slice.Empty.IsEmpty, Is.True);
			Assert.That(Slice.Empty.IsNullOrEmpty, Is.True);
			Assert.That(Slice.Empty.IsPresent, Is.False);

			Assert.That(Slice.Empty.GetBytes(), Is.Not.Null.And.Length.EqualTo(0));
			Assert.That(Slice.Empty.ToArray(), Is.Not.Null.And.Length.EqualTo(0));
			Assert.That(Slice.Empty.ToByteString(), Is.EqualTo(String.Empty));
			Assert.That(Slice.Empty.ToUnicode(), Is.EqualTo(String.Empty));
			Assert.That(Slice.Empty.PrettyPrint(), Is.EqualTo("''"));
		}

		[Test]
		public void Test_Slice_With_Content()
		{
			var slice = Slice.FromStringAscii("ABC");

			Assert.That(slice.Count, Is.EqualTo(3));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Array, Is.Not.Null);
			Assert.That(slice.Array.Length, Is.GreaterThanOrEqualTo(3));

			Assert.That(slice.IsNull, Is.False);
			Assert.That(slice.HasValue, Is.True);
			Assert.That(slice.IsEmpty, Is.False);
			Assert.That(slice.IsNullOrEmpty, Is.False);
			Assert.That(slice.IsPresent, Is.True);

			Assert.That(slice.GetBytes(), Is.EqualTo("ABC"u8.ToArray()));
			Assert.That(slice.ToArray(), Is.EqualTo("ABC"u8.ToArray()));
			Assert.That(slice.ToByteString(), Is.EqualTo("ABC"));
			Assert.That(slice.ToUnicode(), Is.EqualTo("ABC"));
			Assert.That(slice.PrettyPrint(), Is.EqualTo("'ABC'"));
		}

		[Test]
		public void Test_Slice_Zero_With_Capacity()
		{
			Assert.That(Slice.Zero(0).GetBytes(), Is.EqualTo(Array.Empty<byte>()));
			Assert.That(Slice.Zero(16).GetBytes(), Is.EqualTo(new byte[16]));

			Assert.That(() => Slice.Zero(-1), Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		public void Test_Slice_Create_With_Byte_Array()
		{
			Assert.That(default(byte[]).AsSlice().GetBytes(), Is.EqualTo(null));
			Assert.That(Array.Empty<byte>().AsSlice().GetBytes(), Is.EqualTo(Array.Empty<byte>()));
			Assert.That(new byte[] { 1, 2, 3 }.AsSlice().GetBytes(), Is.EqualTo(new byte[] { 1, 2, 3 }));

			// the array return by GetBytes() should not be the same array that was passed to Create !
			byte[] tmp = Guid.NewGuid().ToByteArray(); // create a 16-byte array
			var slice = tmp.AsSlice();
			Assert.That(slice.Array, Is.SameAs(tmp));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(tmp.Length));
			// they should be equal, but not the same !
			Assert.That(slice.GetBytes(), Is.EqualTo(tmp));
			Assert.That(slice.GetBytes(), Is.Not.SameAs(tmp));

			// create from a slice of the array
			slice = tmp.AsSlice(4, 7);
			Assert.That(slice.Array, Is.SameAs(tmp));
			Assert.That(slice.Offset, Is.EqualTo(4));
			Assert.That(slice.Count, Is.EqualTo(7));
			var buf = new byte[7];
			Array.Copy(tmp, 4, buf, 0, 7);
			Assert.That(slice.GetBytes(), Is.EqualTo(buf));

			Assert.That(default(byte[]).AsSlice(), Is.EqualTo(Slice.Nil));
			Assert.That(Array.Empty<byte>().AsSlice(), Is.EqualTo(Slice.Empty));
		}

		[Test]
		public void Test_Slice_Create_Validates_Arguments()
		{
			// null array only allowed with offset=0 and count=0
			// ReSharper disable AssignNullToNotNullAttribute
			Assert.That(() => default(byte[]).AsSlice(0, 1), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => default(byte[]).AsSlice(1, 0), Throws.Nothing, "Count 0 ignores offset");
			Assert.That(() => default(byte[]).AsSlice(1, 1), Throws.InstanceOf<ArgumentException>());
			// ReSharper restore AssignNullToNotNullAttribute

			// empty array only allowed with offset=0 and count=0
			Assert.That(() => Array.Empty<byte>().AsSlice(0, 1), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => Array.Empty<byte>().AsSlice(1, 0), Throws.Nothing, "Count 0 ignores offset");
			Assert.That(() => Array.Empty<byte>().AsSlice(1, 1), Throws.InstanceOf<ArgumentException>());

			// last item must fit in the buffer
			Assert.That(() => new byte[3].AsSlice(0, 4), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => new byte[3].AsSlice(1, 3), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => new byte[3].AsSlice(3, 1), Throws.InstanceOf<ArgumentException>());

			// negative arguments
			Assert.That(() => new byte[3].AsSlice(-1, 1), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => new byte[3].AsSlice(0, -1), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => new byte[3].AsSlice(-1, -1), Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		public void Test_Slice_Create_With_ArraySegment()
		{
			byte[] tmp = Guid.NewGuid().ToByteArray();

			Slice slice = new ArraySegment<byte>(tmp).AsSlice();
			Assert.That(slice.Array, Is.SameAs(tmp));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(tmp.Length));
			// they should be equal, but not the same !
			Assert.That(slice.GetBytes(), Is.EqualTo(tmp));
			Assert.That(slice.GetBytes(), Is.Not.SameAs(tmp));

			slice = new ArraySegment<byte>(tmp, 4, 7).AsSlice();
			Assert.That(slice.Array, Is.SameAs(tmp));
			Assert.That(slice.Offset, Is.EqualTo(4));
			Assert.That(slice.Count, Is.EqualTo(7));
			var buf = new byte[7];
			Array.Copy(tmp, 4, buf, 0, 7);
			Assert.That(slice.GetBytes(), Is.EqualTo(buf));

			Assert.That(default(ArraySegment<byte>).AsSlice(), Is.EqualTo(Slice.Nil));
			Assert.That(new ArraySegment<byte>([ ]).AsSlice(), Is.EqualTo(Slice.Empty));
		}

		[Test]
		public void Test_Slice_Pseudo_Random()
		{
			var rng = new Random();

			var slice = Slice.Random(rng, 16);
			Assert.That(slice.Array, Is.Not.Null);
			Assert.That(slice.Array.Length, Is.GreaterThanOrEqualTo(16));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(16));
			// can't really test random data, apart from checking that it's not filled with zeroes
			Assert.That(slice.GetBytes(), Is.Not.All.EqualTo(0));

			Assert.That(Slice.Random(rng, 0), Is.EqualTo(Slice.Empty));

			// ReSharper disable once AssignNullToNotNullAttribute
			Assert.That(() => Slice.Random(default(Random)!, 16), Throws.ArgumentNullException);
			Assert.That(() => Slice.Random(rng, -1), Throws.InstanceOf<ArgumentOutOfRangeException>());
		}

		[Test]
		public void Test_Slice_Cryptographic_Random()
		{
			var rng = System.Security.Cryptography.RandomNumberGenerator.Create();

			// normal
			var slice = Slice.Random(rng, 16);
			Assert.That(slice.Array, Is.Not.Null);
			Assert.That(slice.Array.Length, Is.GreaterThanOrEqualTo(16));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(16));
			// can't really test random data, apart from checking that it's not filled with zeroes
			Assert.That(slice.GetBytes(), Is.Not.All.EqualTo(0));

			// non-zero bytes
			// we can't 100% test that, unless with a lot of iterations...
			for (int i = 0; i < 256; i++)
			{
				Assert.That(
					Slice.Random(rng, 256, nonZeroBytes: true).GetBytes(),
					Is.All.Not.EqualTo(0)
				);
			}

			Assert.That(Slice.Random(rng, 0), Is.EqualTo(Slice.Empty));
			// ReSharper disable once AssignNullToNotNullAttribute
			Assert.That(() => Slice.Random(default(System.Security.Cryptography.RandomNumberGenerator)!, 16), Throws.ArgumentNullException);
			Assert.That(() => Slice.Random(rng, -1), Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		public void Test_Slice_FromStringAscii()
		{
			Assert.That(Slice.FromStringAscii(default(string)).GetBytes(), Is.Null);
			Assert.That(Slice.FromStringAscii(string.Empty).GetBytes(), Is.EqualTo(Array.Empty<byte>()));
			Assert.That(Slice.FromStringAscii("A").GetBytes(), Is.EqualTo("A"u8.ToArray()));
			Assert.That(Slice.FromStringAscii("AB").GetBytes(), Is.EqualTo("AB"u8.ToArray()));
			Assert.That(Slice.FromStringAscii("ABC").GetBytes(), Is.EqualTo("ABC"u8.ToArray()));
			Assert.That(Slice.FromStringAscii("ABCD").GetBytes(), Is.EqualTo("ABCD"u8.ToArray()));
			Assert.That(Slice.FromStringAscii("\xFF/ABC").GetBytes(), Is.EqualTo(new byte[] { 0xFF, 0x2F, 0x41, 0x42, 0x43 }));
			Assert.That(Slice.FromStringAscii("héllô").GetBytes(), Is.EqualTo(new byte[] { (byte)'h', 0xE9, (byte)'l', (byte)'l', 0xF4 }));
			Assert.That(Slice.FromStringAscii("This is a test of the emergency encoding system").GetBytes(), Is.EqualTo("This is a test of the emergency encoding system"u8.ToArray()));

			// if the string contains non-ASCII chars, it would be corrupted so FromAscii() should throw
			// note: the line below should contain two kanjis. If your editor displays '??' or squares, it is probably not able to display unicode chars properly
			Assert.That(() => Slice.FromStringAscii("hello 世界"), Throws.Exception, "String that contains code points >= 0x80 should throw");
		}

		[Test]
		public void Test_Slice_ToStringAscii()
		{
			Assert.That(Slice.Nil.ToStringAscii(), Is.Null);
			Assert.That(Slice.Empty.ToStringAscii(), Is.EqualTo(String.Empty));
			Assert.That("A"u8.ToArray().AsSlice().ToStringAscii(), Is.EqualTo("A"));
			Assert.That("AB"u8.ToArray().AsSlice().ToStringAscii(), Is.EqualTo("AB"));
			Assert.That("ABC"u8.ToArray().AsSlice().ToStringAscii(), Is.EqualTo("ABC"));
			Assert.That("ABCD"u8.ToArray().AsSlice().ToStringAscii(), Is.EqualTo("ABCD"));
			Assert.That(new byte[] { 0x7F, 0x00, 0x1F }.AsSlice().ToStringAscii(), Is.EqualTo("\x7F\x00\x1F"));
			Assert.That("ABCDEF"u8.ToArray().AsSlice(2, 3).ToStringAscii(), Is.EqualTo("CDE"));
			Assert.That("This is a test of the emergency encoding system"u8.ToArray().AsSlice().ToStringAscii(), Is.EqualTo("This is a test of the emergency encoding system"));

			// If the slice contain anything other than 7+bit ASCII, it should throw!
			Assert.That(() => new byte[] { 0xFF, 0x41, 0x42, 0x43 }.AsSlice().ToStringAscii(), Throws.Exception, "\\xFF is not valid in 7-bit ASCII strings!");
			Assert.That(() => Encoding.Default.GetBytes("héllô").AsSlice().ToStringAscii(), Throws.Exception, "String that contain code points >= 0x80 should throw");
			Assert.That(() => "héllo 世界"u8.ToArray().AsSlice().ToStringAscii(), Throws.Exception, "String that contains code points >= 0x80 should throw");
		}

		[Test]
		public void Test_Slice_FromByteString()
		{
			Assert.That(Slice.FromByteString(default(string)).GetBytes(), Is.Null);
			Assert.That(Slice.FromByteString(string.Empty).GetBytes(), Is.EqualTo(Array.Empty<byte>()));
			Assert.That(Slice.FromByteString("ABC").GetBytes(), Is.EqualTo("ABC"u8.ToArray()));
			Assert.That(Slice.FromByteString("\xFF/ABC").GetBytes(), Is.EqualTo(new [] { (byte) 0xFF, (byte) '/', (byte) 'A', (byte) 'B', (byte) 'C' }));
			Assert.That(Slice.FromByteString("héllô").GetBytes(), Is.EqualTo(new byte[] { (byte)'h', 0xE9, (byte)'l', (byte)'l', 0xF4 }));

			// if the caller likes to live dangerously and call, then the data should be corrupted
			var slice = Slice.FromByteString("hello 世界"); // DON'T EVER DO THAT!
			Assume.That('世' & 0xFF, Is.EqualTo(0x16));
			Assume.That('界' & 0xFF, Is.EqualTo(0x4C));
			Assert.That(slice, Is.EqualTo(Slice.Unescape("hello <16><4C>")));
			Assert.That(slice.ToByteString(), Is.EqualTo("hello \x16L"), "non-ASCII chars should be corrupted after decoding");
			Assert.That(slice.Count, Is.EqualTo(8));

		}

		[Test]
		public void Test_Slice_FromStringAnsi()
		{
			//note: FromStringAnsi uses Encoding.Default which varies from system to system! (win-1252, utf8-, ...)

			Assert.That(Slice.FromStringAnsi(default(string)).GetBytes(), Is.Null);
			Assert.That(Slice.FromStringAnsi(string.Empty).GetBytes(), Is.EqualTo(Array.Empty<byte>()));
			Assert.That(Slice.FromStringAnsi("ABC").GetBytes(), Is.EqualTo("ABC"u8.ToArray()));
			Assert.That(Slice.FromStringAnsi("\xFF/ABC").GetBytes(), Is.EqualTo(Encoding.Default.GetBytes("\xFF/ABC")));
			Assert.That(Slice.FromStringAnsi("héllô").GetBytes(), Is.EqualTo(Encoding.Default.GetBytes("héllô"))); //note: this depends on your OS locale!

			// if the string contains chars that are not supported by the Default encoding, it will be corrupted
			var slice = Slice.FromStringAnsi("hello 世界"); // 8 'letters'
			var expected = Encoding.Default.GetBytes("hello 世界");
			Assert.That(slice.GetBytes(), Is.EqualTo(expected)); //note: this depends on your OS locale!
			Assert.That(slice.ToStringAnsi(), Is.EqualTo(Encoding.Default.GetString(expected)));
		}

		[Test]
		public void Test_Slice_ToStringAnsi()
		{
			//note: FromStringAnsi uses Encoding.Default which varies from system to system! (win-1252, utf8-, ...)

			Assert.That(Slice.Nil.ToStringAnsi(), Is.Null);
			Assert.That(Slice.Empty.ToStringAnsi(), Is.EqualTo(String.Empty));
			Assert.That("ABC"u8.ToArray().AsSlice().ToStringAnsi(), Is.EqualTo("ABC"));
			Assert.That(Encoding.Default.GetBytes("héllô").AsSlice().ToStringAnsi(), Is.EqualTo("héllô")); //note: this depends on your OS locale!
			Assert.That(new[] { (byte) 0xFF, (byte) '/', (byte) 'A', (byte) 'B', (byte) 'C' }.AsSlice().ToStringAnsi(), Is.EqualTo(Encoding.Default.GetString(new[] { (byte) 0xFF, (byte) '/', (byte) 'A', (byte) 'B', (byte) 'C' })));
		}

		[Test]
		public void Test_Slice_FromString_Uses_UTF8()
		{
			Assert.That(Slice.FromString(default(string)).GetBytes(), Is.Null);
			Assert.That(Slice.FromString(string.Empty).GetBytes(), Is.EqualTo(Array.Empty<byte>()));
			Assert.That(Slice.FromString("ABC").GetBytes(), Is.EqualTo("ABC"u8.ToArray()));
			Assert.That(Slice.FromString("é").GetBytes(), Is.EqualTo("é"u8.ToArray()));

			// if the string contains UTF-8 characters, it should be encoded properly
			// note: the line below should contain two kanjis. If your editor displays '??' or squares, it is probably not able to display unicode chars properly
			var slice = Slice.FromString("héllø 世界"); // 8 'letters'
			Assert.That(slice.GetBytes(), Is.EqualTo("héllø 世界"u8.ToArray()));
			Assert.That(slice.ToUnicode(), Is.EqualTo("héllø 世界"), "non-ASCII chars should not be corrupted");
			Assert.That(slice.Count, Is.EqualTo(14));

			// UTF8 does not map \xFF or \xFE directly to a single byte (but at least it should round-trip)
			Assert.That(Slice.FromString("\xFF").GetBytes(), Is.EqualTo("ÿ"u8.ToArray()));
			Assert.That(Slice.FromString("\xFE").GetBytes(), Is.EqualTo("þ"u8.ToArray()));
			Assert.That("ÿ"u8.ToArray().AsSlice().ToUnicode(), Is.EqualTo("\xFF"));
			Assert.That("þ"u8.ToArray().AsSlice().ToUnicode(), Is.EqualTo("\xFE"));
		}

		[Test]
		public void Test_Slice_FromStringUtf8()
		{
			Assert.That(Slice.FromStringUtf8(default(string)).GetBytes(), Is.Null);
			Assert.That(Slice.FromStringUtf8(string.Empty).GetBytes(), Is.EqualTo(Array.Empty<byte>()));
			Assert.That(Slice.FromStringUtf8("ABC").GetBytes(), Is.EqualTo("ABC"u8.ToArray()));
			Assert.That(Slice.FromStringUtf8("é").GetBytes(), Is.EqualTo("é"u8.ToArray()));

			// if the string contains UTF-8 characters, it should be encoded properly
			// note: the line below should contain two kanjis. If your editor displays '??' or squares, it is probably not able to display unicode chars properly
			var slice = Slice.FromStringUtf8("héllø 世界"); // 8 'letters'
			Assert.That(slice.GetBytes(), Is.EqualTo("héllø 世界"u8.ToArray()));
			Assert.That(slice.ToStringUtf8(), Is.EqualTo("héllø 世界"), "non-ASCII chars should not be corrupted");
			Assert.That(slice.ToUnicode(), Is.EqualTo("héllø 世界"), "non-ASCII chars should not be corrupted");
			Assert.That(slice.Count, Is.EqualTo(14));

			// UTF8 does not map \xFF or \xFE directly to a single byte (but at least it should round-trip)
			Assert.That(Slice.FromStringUtf8("\xFF").GetBytes(), Is.EqualTo("ÿ"u8.ToArray()));
			Assert.That(Slice.FromStringUtf8("\xFE").GetBytes(), Is.EqualTo("þ"u8.ToArray()));
			Assert.That("ÿ"u8.ToArray().AsSlice().ToStringUtf8(), Is.EqualTo("\xFF"));
			Assert.That("ÿ"u8.ToArray().AsSlice().ToUnicode(), Is.EqualTo("\xFF"));
			Assert.That("þ"u8.ToArray().AsSlice().ToStringUtf8(), Is.EqualTo("\xFE"));
			Assert.That("þ"u8.ToArray().AsSlice().ToUnicode(), Is.EqualTo("\xFE"));
		}

		[Test]
		public void Test_Slice_ToStringUtf8()
		{
			Assert.That(Slice.Nil.ToStringUtf8(), Is.Null);
			Assert.That(Slice.Empty.ToStringUtf8(), Is.EqualTo(String.Empty));
			Assert.That("ABC"u8.ToArray().AsSlice().ToStringUtf8(), Is.EqualTo("ABC"));
			Assert.That("héllô"u8.ToArray().AsSlice().ToStringUtf8(), Is.EqualTo("héllô")); //note: this depends on your OS locale!
			Assert.That("世界"u8.ToArray().AsSlice().ToStringUtf8(), Is.EqualTo("世界"));

			// should remove the bom!
			Assert.That(new byte[] { 0xEF, 0xBB, 0xBF, (byte) 'A', (byte) 'B', (byte) 'C' }.AsSlice().ToStringUtf8(), Is.EqualTo("ABC"), "BOM should be removed");
			Assert.That(new byte[] { 0xEF, 0xBB, 0xBF }.AsSlice().ToStringUtf8(), Is.EqualTo(String.Empty), "BOM should also be removed for empty string");
			Assert.That(new byte[] { 0xEF, 0xBB, 0xBF, 0xEF, 0xBB, 0xBF, (byte) 'A', (byte) 'B', (byte) 'C' }.AsSlice().ToStringUtf8(), Is.EqualTo("\uFEFFABC"), "Only one BOM should be removed");

			// corrupted UTF-8
			Assert.That(() => new byte[] { 0xEF, 0xBB }.AsSlice().ToStringUtf8(), Throws.Exception, "Partial BOM should fail to decode");
			Assert.That(() => new byte[] { (byte) 'A', 0xc3, 0x28, (byte) 'B' }.AsSlice().ToStringUtf8(), Throws.Exception, "Invalid 2-byte sequence");
			Assert.That(() => new byte[] { (byte) 'A', 0xe2, 0x28, 0xa1, (byte) 'B' }.AsSlice().ToStringUtf8(), Throws.Exception, "Invalid 3-byte sequence");
			Assert.That(() => new byte[] { (byte) 'A', 0xf0, 0x28, 0x8c, 0x28, (byte) 'B' }.AsSlice().ToStringUtf8(), Throws.Exception, "Invalid 4-byte sequence");
			Assert.That(() => new byte[] { (byte) 'A', 0xf0, 0x28, /*..SNIP..*/ }.AsSlice().ToStringUtf8(), Throws.Exception, "Truncated 4-byte sequence");
		}

		[Test]
		public void Test_Slice_FromStringUtf8WithBom()
		{
			Assert.That(Slice.FromStringUtf8WithBom(default(string)).GetBytes(), Is.Null);
			Assert.That(Slice.FromStringUtf8WithBom(string.Empty).GetBytes(), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
			Assert.That(Slice.FromStringUtf8WithBom("ABC").GetBytes(), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF, 0x41, 0x42, 0x43 }));
			Assert.That(Slice.FromStringUtf8WithBom("é").GetBytes(), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF, 0xC3, 0xA9 }));

			// if the string contains UTF-8 characters, it should be encoded properly
			// note: the line below should contain two kanjis. If your editor displays '??' or squares, it is probably not able to display unicode chars properly
			var slice = Slice.FromStringUtf8WithBom("héllø 世界"); // 8 'letters'
			Assert.That(slice.GetBytes(), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }.Concat("héllø 世界"u8.ToArray()).ToArray()));
			Assert.That(slice.ToStringUtf8(), Is.EqualTo("héllø 世界"), "The BOM should be removed");
			Assert.That(slice.ToUnicode(), Is.EqualTo("\xFEFFhéllø 世界"), "The BOM should be preserved");
			Assert.That(slice.Count, Is.EqualTo(3 + 14));

			// UTF8 does not map \xFF or \xFE directly to a single byte (but at least it should round-trip)
			Assert.That(Slice.FromStringUtf8WithBom("\xFF").GetBytes(), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF, 0xC3, 0xBF }));
			Assert.That(Slice.FromStringUtf8WithBom("\xFE").GetBytes(), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF, 0xC3, 0xBE }));
			Assert.That(new byte[] { 0xEF, 0xBB, 0xBF, 0xC3, 0xBF }.AsSlice().ToStringUtf8(), Is.EqualTo("\xFF"));
			Assert.That(new byte[] { 0xEF, 0xBB, 0xBF, 0xC3, 0xBF }.AsSlice().ToUnicode(), Is.EqualTo("\uFEFF\xFF"));
			Assert.That(new byte[] { 0xEF, 0xBB, 0xBF, 0xC3, 0xBE }.AsSlice().ToStringUtf8(), Is.EqualTo("\xFE"));
			Assert.That(new byte[] { 0xEF, 0xBB, 0xBF, 0xC3, 0xBE }.AsSlice().ToUnicode(), Is.EqualTo("\uFEFF\xFE"));
		}

		[Test]
		public void Test_Slice_FromChar_Uses_UTF8()
		{
			// from 0 to 127 is regular single-byte ASCII
			Assert.That(Slice.FromChar('\0').GetBytes(), Is.EqualTo(new byte[] { 0 }));
			Assert.That(Slice.FromChar('\x01').GetBytes(), Is.EqualTo(new byte[] { 1 }));
			Assert.That(Slice.FromChar('0').GetBytes(), Is.EqualTo(new byte[] { 48 }));
			Assert.That(Slice.FromChar('A').GetBytes(), Is.EqualTo(new byte[] { 65 }));
			Assert.That(Slice.FromChar('a').GetBytes(), Is.EqualTo(new byte[] { 97 }));
			Assert.That(Slice.FromChar('~').GetBytes(), Is.EqualTo(new byte[] { 126 }));
			Assert.That(Slice.FromChar('\x7F').GetBytes(), Is.EqualTo(new byte[] { 127 }));

			// 128 and above is multibyte UTF-8
			Assert.That(Slice.FromChar('\x80').GetBytes(), Is.EqualTo(new byte[] { 0xC2, 0x80 }));
			Assert.That(Slice.FromChar('é').GetBytes(), Is.EqualTo("é"u8.ToArray()));
			Assert.That(Slice.FromChar('\u221E').GetBytes(), Is.EqualTo("\u221e"u8.ToArray()));
			Assert.That(Slice.FromChar('\uFFFE').GetBytes(), Is.EqualTo("\ufffe"u8.ToArray()));
		}

		#region Signed...

		#region 16-bits

		#region Little-Endian

		[Test]
		public void Test_Slice_FromInt16()
		{
			// 16-bit integers should be encoded in little endian, and with 1 or 2 bytes

			static void Verify(short value, string expected)
			{
				Assert.That(Slice.FromInt16(value).ToHexString(), Is.EqualTo(expected), $"Invalid encoding for {value}");
			}

			Verify(0x12, "12");
			Verify(0x1234, "3412");

			Verify(0, "00");
			Verify(1, "01");
			Verify(255, "FF");
			Verify(256, "0001");
			Verify(short.MaxValue, "FF7F");
			Verify(short.MinValue, "0080");
		}

		[Test]
		public void Test_Slice_FromFixed16()
		{
			// FromFixed64 always produce 2 bytes and uses Little Endian

			Assert.That(Slice.FromFixed16(0).GetBytes(), Is.EqualTo(new byte[2]));
			Assert.That(Slice.FromFixed16(1).GetBytes(), Is.EqualTo(new byte[] { 1, 0 }));
			Assert.That(Slice.FromFixed16(256).GetBytes(), Is.EqualTo(new byte[] { 0, 1 }));
			Assert.That(Slice.FromFixed16(short.MaxValue).GetBytes(), Is.EqualTo(new byte[] { 255, 127 }));

			Assert.That(Slice.FromFixed16(-1).GetBytes(), Is.EqualTo(new byte[] { 255, 255 }));
			Assert.That(Slice.FromFixed16(-256).GetBytes(), Is.EqualTo(new byte[] { 0, 255  }));
			Assert.That(Slice.FromFixed16(short.MinValue).GetBytes(), Is.EqualTo(new byte[] { 0, 128 }));

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				short x = (short) rnd.Next(-32768, 32767);
				var s = Slice.FromFixed16(x);
				Assert.That(s.Count, Is.EqualTo(2));
				Assert.That(s.ToInt16(), Is.EqualTo(x));
			}
		}

		[Test]
		public void Test_Slice_ToInt16()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToInt16(), Is.EqualTo(0x12));
			Assert.That(new byte[] { 0x34, 0x12 }.AsSlice().ToInt16(), Is.EqualTo(0x1234));

			Assert.That(new byte[] { }.AsSlice().ToInt16(), Is.EqualTo(0));
			Assert.That(new byte[] { 0 }.AsSlice().ToInt16(), Is.EqualTo(0));
			Assert.That(new byte[] { 255 }.AsSlice().ToInt16(), Is.EqualTo(255));
			Assert.That(new byte[] { 0, 1 }.AsSlice().ToInt16(), Is.EqualTo(256));
			Assert.That(new byte[] { 255, 127 }.AsSlice().ToInt16(), Is.EqualTo(32767));
			Assert.That(new byte[] { 0, 128 }.AsSlice().ToInt16(), Is.EqualTo(-32768));
			Assert.That(new byte[] { 0, 255 }.AsSlice().ToInt16(), Is.EqualTo(-256));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToInt16(), Is.EqualTo(-1));

			Assert.That(() => new byte[3].AsSlice().ToInt16(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#region Big Endian

		[Test]
		public void Test_Slice_FromInt16BE()
		{
			// 32-bit integers should be encoded in little endian, and with 1, 2 or 4 bytes

			static void Verify(short value, string expected)
			{
				Assert.That(Slice.FromInt16BE(value).ToHexString(), Is.EqualTo(expected), $"Invalid encoding for {value}");
			}

			Verify(0x12, "12");
			Verify(0x1234, "1234");

			Verify(0, "00");
			Verify(1, "01");
			Verify(255, "FF");
			Verify(256, "0100");
			Verify(-1, "FFFF");
			Verify(-256, "FF00");
			Verify(short.MaxValue, "7FFF");
			Verify(short.MinValue, "8000");
		}

		[Test]
		public void Test_Slice_FromFixed16BE()
		{
			// FromFixed32 always produce 4 bytes and uses Little Endian

			Assert.That(Slice.FromFixed16BE(0).GetBytes(), Is.EqualTo(new byte[2]));
			Assert.That(Slice.FromFixed16BE(1).GetBytes(), Is.EqualTo(new byte[] { 0, 1 }));
			Assert.That(Slice.FromFixed16BE(256).GetBytes(), Is.EqualTo(new byte[] { 1, 0 }));
			Assert.That(Slice.FromFixed16BE(short.MaxValue).GetBytes(), Is.EqualTo(new byte[] { 127, 255 }));

			Assert.That(Slice.FromFixed16BE(-1).GetBytes(), Is.EqualTo(new byte[] { 255, 255 }));
			Assert.That(Slice.FromFixed16BE(-256).GetBytes(), Is.EqualTo(new byte[] { 255, 0 }));
			Assert.That(Slice.FromFixed16BE(short.MinValue).GetBytes(), Is.EqualTo(new byte[] { 128, 0 }));

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				short x = (short) rnd.Next(-32768, 32767);
				var s = Slice.FromFixed16BE(x);
				Assert.That(s.Count, Is.EqualTo(2));
				Assert.That(s.ToInt16BE(), Is.EqualTo(x));
			}
		}

		[Test]
		public void Test_Slice_ToInt16BE()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToInt16BE(), Is.EqualTo(0x12));
			Assert.That(new byte[] { 0x12, 0x34 }.AsSlice().ToInt16BE(), Is.EqualTo(0x1234));

			Assert.That(new byte[] { }.AsSlice().ToInt16BE(), Is.EqualTo(0));
			Assert.That(new byte[] { 0 }.AsSlice().ToInt16BE(), Is.EqualTo(0));
			Assert.That(new byte[] { 255 }.AsSlice().ToInt16BE(), Is.EqualTo(255));
			Assert.That(new byte[] { 1, 0 }.AsSlice().ToInt16BE(), Is.EqualTo(256));
			Assert.That(new byte[] { 127, 255 }.AsSlice().ToInt16BE(), Is.EqualTo(32767));
			Assert.That(new byte[] { 128, 0 }.AsSlice().ToInt16BE(), Is.EqualTo(-32768));
			Assert.That(new byte[] { 255, 0 }.AsSlice().ToInt16BE(), Is.EqualTo(-256));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToInt16BE(), Is.EqualTo(-1));

			Assert.That(() => new byte[5].AsSlice().ToInt16BE(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#endregion

		#region 24-bits

		#region Little-Endian

		[Test]
		public void Test_Slice_ToInt24()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToInt24(), Is.EqualTo(0x12));
			Assert.That(new byte[] { 0x34, 0x12 }.AsSlice().ToInt24(), Is.EqualTo(0x1234));
			Assert.That(new byte[] { 0x34, 0x12, 0x00 }.AsSlice().ToInt24(), Is.EqualTo(0x1234));
			Assert.That(new byte[] { 0x56, 0x34, 0x12 }.AsSlice().ToInt24(), Is.EqualTo(0x123456));

			Assert.That(new byte[] { }.AsSlice().ToInt24(), Is.EqualTo(0));
			Assert.That(new byte[] { 0 }.AsSlice().ToInt24(), Is.EqualTo(0));
			Assert.That(new byte[] { 127 }.AsSlice().ToInt24(), Is.EqualTo(127));
			Assert.That(new byte[] { 255 }.AsSlice().ToInt24(), Is.EqualTo(255));
			Assert.That(new byte[] { 0, 1 }.AsSlice().ToInt24(), Is.EqualTo(256));
			Assert.That(new byte[] { 255, 127 }.AsSlice().ToInt24(), Is.EqualTo(32767));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToInt24(), Is.EqualTo(65535));
			Assert.That(new byte[] { 0, 0, 1 }.AsSlice().ToInt24(), Is.EqualTo(1 << 16));
			Assert.That(new byte[] { 255, 255, 127 }.AsSlice().ToInt24(), Is.EqualTo((1 << 23) - 1));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToInt24(), Is.EqualTo((1 << 24) - 1));

			Assert.That(() => new byte[4].AsSlice().ToInt24(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_Slice_ToUInt24()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToUInt24(), Is.EqualTo(0x12));
			Assert.That(new byte[] { 0x34, 0x12 }.AsSlice().ToUInt24(), Is.EqualTo(0x1234));
			Assert.That(new byte[] { 0x34, 0x12, 0x00 }.AsSlice().ToUInt24(), Is.EqualTo(0x1234));
			Assert.That(new byte[] { 0x56, 0x34, 0x12 }.AsSlice().ToUInt24(), Is.EqualTo(0x123456));

			Assert.That(new byte[] { }.AsSlice().ToUInt24(), Is.EqualTo(0));
			Assert.That(new byte[] { 0 }.AsSlice().ToUInt24(), Is.EqualTo(0));
			Assert.That(new byte[] { 127 }.AsSlice().ToUInt24(), Is.EqualTo(127));
			Assert.That(new byte[] { 255 }.AsSlice().ToUInt24(), Is.EqualTo(255));
			Assert.That(new byte[] { 0, 1 }.AsSlice().ToUInt24(), Is.EqualTo(256));
			Assert.That(new byte[] { 255, 127 }.AsSlice().ToUInt24(), Is.EqualTo(32767));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToUInt24(), Is.EqualTo(65535));
			Assert.That(new byte[] { 0, 0, 1 }.AsSlice().ToUInt24(), Is.EqualTo(1 << 16));
			Assert.That(new byte[] { 255, 255, 127 }.AsSlice().ToUInt24(), Is.EqualTo((1 << 23) - 1));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToUInt24(), Is.EqualTo((1 << 24) - 1));

			Assert.That(() => new byte[4].AsSlice().ToUInt24(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#region Big Endian

		[Test]
		public void Test_Slice_ToInt24BE()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToInt24BE(), Is.EqualTo(0x12));
			Assert.That(new byte[] { 0x12, 0x34 }.AsSlice().ToInt24BE(), Is.EqualTo(0x1234));
			Assert.That(new byte[] { 0x12, 0x34, 0x56 }.AsSlice().ToInt24BE(), Is.EqualTo(0x123456));

			Assert.That(new byte[] { }.AsSlice().ToInt24BE(), Is.EqualTo(0));
			Assert.That(new byte[] { 0 }.AsSlice().ToInt24BE(), Is.EqualTo(0));
			Assert.That(new byte[] { 127 }.AsSlice().ToInt24BE(), Is.EqualTo(127));
			Assert.That(new byte[] { 255 }.AsSlice().ToInt24BE(), Is.EqualTo(255));
			Assert.That(new byte[] { 1, 0 }.AsSlice().ToInt24BE(), Is.EqualTo(256));
			Assert.That(new byte[] { 127, 255 }.AsSlice().ToInt24BE(), Is.EqualTo(32767));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToInt24BE(), Is.EqualTo(65535));
			Assert.That(new byte[] { 1, 0, 0 }.AsSlice().ToInt24BE(), Is.EqualTo(1 << 16));
			Assert.That(new byte[] { 127, 255, 255 }.AsSlice().ToInt24BE(), Is.EqualTo((1 << 23) - 1));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToInt24BE(), Is.EqualTo((1 << 24) - 1));

			Assert.That(() => new byte[4].AsSlice().ToInt24BE(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_Slice_ToUInt24BE()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToUInt24BE(), Is.EqualTo(0x12));
			Assert.That(new byte[] { 0x12, 0x34 }.AsSlice().ToUInt24BE(), Is.EqualTo(0x1234));
			Assert.That(new byte[] { 0x12, 0x34, 0x56 }.AsSlice().ToUInt24BE(), Is.EqualTo(0x123456));

			Assert.That(new byte[] { }.AsSlice().ToUInt24BE(), Is.EqualTo(0));
			Assert.That(new byte[] { 0 }.AsSlice().ToUInt24BE(), Is.EqualTo(0));
			Assert.That(new byte[] { 127 }.AsSlice().ToUInt24BE(), Is.EqualTo(127));
			Assert.That(new byte[] { 255 }.AsSlice().ToUInt24BE(), Is.EqualTo(255));
			Assert.That(new byte[] { 1, 0 }.AsSlice().ToUInt24BE(), Is.EqualTo(256));
			Assert.That(new byte[] { 127, 255 }.AsSlice().ToUInt24BE(), Is.EqualTo(32767));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToUInt24BE(), Is.EqualTo(65535));
			Assert.That(new byte[] { 1, 0, 0 }.AsSlice().ToUInt24BE(), Is.EqualTo(1 << 16));
			Assert.That(new byte[] { 127, 255, 255 }.AsSlice().ToUInt24BE(), Is.EqualTo((1 << 23) - 1));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToUInt24BE(), Is.EqualTo((1 << 24) - 1));

			Assert.That(() => new byte[4].AsSlice().ToUInt24BE(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#endregion

		#region 32-bits

		#region Little-Endian

		[Test]
		public void Test_Slice_FromInt32()
		{
			// 32-bit integers should be encoded in little endian, and with 1, 2 or 4 bytes

			static void Verify(int value, string expected)
			{
				Assert.That(Slice.FromInt32(value).ToHexString(), Is.EqualTo(expected), $"Invalid encoding for {value}");
			}

			Verify(0x12, "12");
			Verify(0x1234, "3412");
			Verify(0x123456, "563412");
			Verify(0x12345678, "78563412");

			Verify(0, "00");
			Verify(1, "01");
			Verify(255, "FF");
			Verify(256, "0001");
			Verify(65535, "FFFF");
			Verify(65536, "000001");
			Verify(16777215, "FFFFFF");
			Verify(16777216, "00000001");
			Verify(int.MaxValue, "FFFFFF7F");
			Verify(int.MinValue, "00000080");
		}

		[Test]
		public void Test_Slice_FromFixed32()
		{
			// FromFixed32 always produce 4 bytes and uses Little Endian

			Assert.That(Slice.FromFixed32(0).GetBytes(), Is.EqualTo(new byte[4]));
			Assert.That(Slice.FromFixed32(1).GetBytes(), Is.EqualTo(new byte[] { 1, 0, 0, 0 }));
			Assert.That(Slice.FromFixed32(256).GetBytes(), Is.EqualTo(new byte[] { 0, 1, 0, 0 }));
			Assert.That(Slice.FromFixed32(65536).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 1, 0 }));
			Assert.That(Slice.FromFixed32(16777216).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 1 }));
			Assert.That(Slice.FromFixed32(short.MaxValue).GetBytes(), Is.EqualTo(new byte[] { 255, 127, 0, 0 }));
			Assert.That(Slice.FromFixed32(int.MaxValue).GetBytes(), Is.EqualTo(new byte[] { 255, 255, 255, 127 }));

			Assert.That(Slice.FromFixed32(-1).GetBytes(), Is.EqualTo(new byte[] { 255, 255, 255, 255 }));
			Assert.That(Slice.FromFixed32(-256).GetBytes(), Is.EqualTo(new byte[] { 0, 255, 255, 255 }));
			Assert.That(Slice.FromFixed32(-65536).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 255, 255 }));
			Assert.That(Slice.FromFixed32(-16777216).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 255 }));
			Assert.That(Slice.FromFixed32(int.MinValue).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 128 }));

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				int x = rnd.Next() * (rnd.Next(2) == 0 ? +1 : -1);
				var s = Slice.FromFixed32(x);
				Assert.That(s.Count, Is.EqualTo(4));
				Assert.That(s.ToInt32(), Is.EqualTo(x));
			}
		}

		[Test]
		public void Test_Slice_ToInt32()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToInt32(), Is.EqualTo(0x12));
			Assert.That(new byte[] { 0x34, 0x12 }.AsSlice().ToInt32(), Is.EqualTo(0x1234));
			Assert.That(new byte[] { 0x56, 0x34, 0x12 }.AsSlice().ToInt32(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x56, 0x34, 0x12, 0x00 }.AsSlice().ToInt32(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToInt32(), Is.EqualTo(0x12345678));

			Assert.That(new byte[] { }.AsSlice().ToInt32(), Is.EqualTo(0));
			Assert.That(new byte[] { 0 }.AsSlice().ToInt32(), Is.EqualTo(0));
			Assert.That(new byte[] { 255 }.AsSlice().ToInt32(), Is.EqualTo(255));
			Assert.That(new byte[] { 0, 1 }.AsSlice().ToInt32(), Is.EqualTo(256));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToInt32(), Is.EqualTo(65535));
			Assert.That(new byte[] { 0, 0, 1 }.AsSlice().ToInt32(), Is.EqualTo(1 << 16));
			Assert.That(new byte[] { 0, 0, 1, 0 }.AsSlice().ToInt32(), Is.EqualTo(1 << 16));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToInt32(), Is.EqualTo((1 << 24) - 1));
			Assert.That(new byte[] { 0, 0, 0, 1 }.AsSlice().ToInt32(), Is.EqualTo(1 << 24));
			Assert.That(new byte[] { 255, 255, 255, 127 }.AsSlice().ToInt32(), Is.EqualTo(int.MaxValue));

			Assert.That(() => new byte[5].AsSlice().ToInt32(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#region Big Endian

		[Test]
		public void Test_Slice_FromInt32BE()
		{
			// 32-bit integers should be encoded in little endian, and with 1, 2 or 4 bytes

			static void Verify(int value, string expected)
			{
				Assert.That(Slice.FromInt32BE(value).ToHexString(), Is.EqualTo(expected), $"Invalid encoding for {value}");
			}

			Verify(0x12, "12");
			Verify(0x1234, "1234");
			Verify(0x123456, "123456");
			Verify(0x12345678, "12345678");

			Verify(0, "00");
			Verify(1, "01");
			Verify(255, "FF");
			Verify(256, "0100");
			Verify(65535, "FFFF");
			Verify(65536, "010000");
			Verify(16777215, "FFFFFF");
			Verify(16777216, "01000000");
			Verify(int.MaxValue, "7FFFFFFF");
			Verify(int.MinValue, "80000000");
		}

		[Test]
		public void Test_Slice_FromFixed32BE()
		{
			// FromFixed32 always produce 4 bytes and uses Little Endian

			Assert.That(Slice.FromFixed32BE(0).GetBytes(), Is.EqualTo(new byte[4]));
			Assert.That(Slice.FromFixed32BE(1).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 1 }));
			Assert.That(Slice.FromFixed32BE(256).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 1, 0 }));
			Assert.That(Slice.FromFixed32BE(65536).GetBytes(), Is.EqualTo(new byte[] { 0, 1, 0, 0 }));
			Assert.That(Slice.FromFixed32BE(16777216).GetBytes(), Is.EqualTo(new byte[] { 1, 0, 0, 0 }));
			Assert.That(Slice.FromFixed32BE(short.MaxValue).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 127, 255 }));
			Assert.That(Slice.FromFixed32BE(int.MaxValue).GetBytes(), Is.EqualTo(new byte[] { 127, 255, 255, 255 }));

			Assert.That(Slice.FromFixed32BE(-1).GetBytes(), Is.EqualTo(new byte[] { 255, 255, 255, 255 }));
			Assert.That(Slice.FromFixed32BE(-256).GetBytes(), Is.EqualTo(new byte[] { 255, 255, 255, 0 }));
			Assert.That(Slice.FromFixed32BE(-65536).GetBytes(), Is.EqualTo(new byte[] { 255, 255, 0, 0 }));
			Assert.That(Slice.FromFixed32BE(-16777216).GetBytes(), Is.EqualTo(new byte[] { 255, 0, 0, 0 }));
			Assert.That(Slice.FromFixed32BE(int.MinValue).GetBytes(), Is.EqualTo(new byte[] { 128, 0, 0, 0 }));

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				int x = rnd.Next() * (rnd.Next(2) == 0 ? +1 : -1);
				var s = Slice.FromFixed32BE(x);
				Assert.That(s.Count, Is.EqualTo(4));
				Assert.That(s.ToInt32BE(), Is.EqualTo(x));
			}
		}

		[Test]
		public void Test_Slice_ToInt32BE()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToInt32BE(), Is.EqualTo(0x12));
			Assert.That(new byte[] { 0x12, 0x34 }.AsSlice().ToInt32BE(), Is.EqualTo(0x1234));
			Assert.That(new byte[] { 0x12, 0x34, 0x56 }.AsSlice().ToInt32BE(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x00, 0x12, 0x34, 0x56 }.AsSlice().ToInt32BE(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78 }.AsSlice().ToInt32BE(), Is.EqualTo(0x12345678));

			Assert.That(new byte[] { }.AsSlice().ToInt32BE(), Is.EqualTo(0));
			Assert.That(new byte[] { 0 }.AsSlice().ToInt32BE(), Is.EqualTo(0));
			Assert.That(new byte[] { 255 }.AsSlice().ToInt32BE(), Is.EqualTo(255));
			Assert.That(new byte[] { 1, 0 }.AsSlice().ToInt32BE(), Is.EqualTo(256));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToInt32BE(), Is.EqualTo(65535));
			Assert.That(new byte[] { 1, 0, 0 }.AsSlice().ToInt32BE(), Is.EqualTo(1 << 16));
			Assert.That(new byte[] { 0, 1, 0, 0 }.AsSlice().ToInt32BE(), Is.EqualTo(1 << 16));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToInt32BE(), Is.EqualTo((1 << 24) - 1));
			Assert.That(new byte[] { 1, 0, 0, 0 }.AsSlice().ToInt32BE(), Is.EqualTo(1 << 24));
			Assert.That(new byte[] { 127, 255, 255, 255 }.AsSlice().ToInt32BE(), Is.EqualTo(int.MaxValue));

			Assert.That(() => new byte[5].AsSlice().ToInt32BE(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#endregion

		#region 64-bits

		#region Little-Endian

		[Test]
		public void Test_Slice_FromInt64()
		{
			// 64-bit integers should be encoded in little endian, and with 1, 2, 4 or 8 bytes

			static void Verify(long value, string expected)
			{
				Assert.That(Slice.FromInt64(value).ToHexString(), Is.EqualTo(expected), $"Invalid encoding for {value}");
			}

			Verify(0x12, "12");
			Verify(0x1234, "3412");
			Verify(0x123456, "563412");
			Verify(0x12345678, "78563412");
			Verify(0x123456789A, "9A78563412");
			Verify(0x123456789ABC, "BC9A78563412");
			Verify(0x123456789ABCDE, "DEBC9A78563412");
			Verify(0x123456789ABCDEF0, "F0DEBC9A78563412");

			Verify(0, "00");
			Verify(1, "01");
			Verify(255, "FF");
			Verify(256, "0001");
			Verify(65535, "FFFF");
			Verify(65536, "000001");
			Verify(16777215, "FFFFFF");
			Verify(16777216, "00000001");
			Verify(int.MaxValue, "FFFFFF7F");
			Verify(int.MinValue, "00000080FFFFFFFF");
			Verify(1L + int.MaxValue, "00000080");
			Verify(long.MaxValue, "FFFFFFFFFFFFFF7F");
			Verify(long.MinValue, "0000000000000080");

		}

		[Test]
		public void Test_Slice_FromFixed64()
		{
			// FromFixed64 always produce 8 bytes and uses Little Endian

			static void Verify(long value, byte[] expected)
			{
				Assert.That(Slice.FromFixed64(value).GetBytes(), Is.EqualTo(expected), $"Invalid encoding for {value}");
			}

			Verify(0L, new byte[8]);
			Verify(1L, [ 1, 0, 0, 0, 0, 0, 0, 0 ]);
			Verify(1L << 8, [ 0, 1, 0, 0, 0, 0, 0, 0 ]);
			Verify(1L << 16, [ 0, 0, 1, 0, 0, 0, 0, 0 ]);
			Verify(1L << 24, [ 0, 0, 0, 1, 0, 0, 0, 0 ]);
			Verify(1L << 32, [ 0, 0, 0, 0, 1, 0, 0, 0 ]);
			Verify(1L << 40, [ 0, 0, 0, 0, 0, 1, 0, 0 ]);
			Verify(1L << 48, [ 0, 0, 0, 0, 0, 0, 1, 0 ]);
			Verify(1L << 56, [ 0, 0, 0, 0, 0, 0, 0, 1 ]);
			Verify(short.MaxValue, [ 255, 127, 0, 0, 0, 0, 0, 0 ]);
			Verify(int.MaxValue, [ 255, 255, 255, 127, 0, 0, 0, 0 ]);
			Verify(long.MaxValue, [ 255, 255, 255, 255, 255, 255, 255, 127 ]);

			Verify(-1L, [ 255, 255, 255, 255, 255, 255, 255, 255 ]);
			Verify(-256L, [ 0, 255, 255, 255, 255, 255, 255, 255 ]);
			Verify(-65536L, [ 0, 0, 255, 255, 255, 255, 255, 255 ]);
			Verify(-16777216L, [ 0, 0, 0, 255, 255, 255, 255, 255 ]);
			Verify(-4294967296L, [ 0, 0, 0, 0, 255, 255, 255, 255 ]);
			Verify(long.MinValue, [ 0, 0, 0, 0, 0, 0, 0, 128 ]);

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				long x = (long)rnd.Next() * rnd.Next() * (rnd.Next(2) == 0 ? +1 : -1);
				var s = Slice.FromFixed64(x);
				Assert.That(s.Count, Is.EqualTo(8));
				Assert.That(s.ToInt64(), Is.EqualTo(x));
			}
		}

		[Test]
		public void Test_Slice_ToInt64()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToInt64(), Is.EqualTo(0x12));
			Assert.That(new byte[] { 0x34, 0x12 }.AsSlice().ToInt64(), Is.EqualTo(0x1234));
			Assert.That(new byte[] { 0x56, 0x34, 0x12 }.AsSlice().ToInt64(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x56, 0x34, 0x12, 0x00 }.AsSlice().ToInt64(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToInt64(), Is.EqualTo(0x12345678));
			Assert.That(new byte[] { 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToInt64(), Is.EqualTo(0x123456789A));
			Assert.That(new byte[] { 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToInt64(), Is.EqualTo(0x123456789ABC));
			Assert.That(new byte[] { 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToInt64(), Is.EqualTo(0x123456789ABCDE));
			Assert.That(new byte[] { 0xF0, 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToInt64(), Is.EqualTo(0x123456789ABCDEF0));

			Assert.That(new byte[] { }.AsSlice().ToInt64(), Is.EqualTo(0L));
			Assert.That(new byte[] { 0 }.AsSlice().ToInt64(), Is.EqualTo(0L));
			Assert.That(new byte[] { 255 }.AsSlice().ToInt64(), Is.EqualTo(255L));
			Assert.That(new byte[] { 0, 1 }.AsSlice().ToInt64(), Is.EqualTo(256L));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToInt64(), Is.EqualTo(65535L));
			Assert.That(new byte[] { 0, 0, 1 }.AsSlice().ToInt64(), Is.EqualTo(1L << 16));
			Assert.That(new byte[] { 0, 0, 1, 0 }.AsSlice().ToInt64(), Is.EqualTo(1L << 16));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToInt64(), Is.EqualTo((1L << 24) - 1));
			Assert.That(new byte[] { 0, 0, 0, 1 }.AsSlice().ToInt64(), Is.EqualTo(1L << 24));
			Assert.That(new byte[] { 0, 0, 0, 0, 1 }.AsSlice().ToInt64(), Is.EqualTo(1L << 32));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 1 }.AsSlice().ToInt64(), Is.EqualTo(1L << 40));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 1 }.AsSlice().ToInt64(), Is.EqualTo(1L << 48));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }.AsSlice().ToInt64(), Is.EqualTo(1L << 56));
			Assert.That(new byte[] { 255, 255, 255, 127 }.AsSlice().ToInt64(), Is.EqualTo(int.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 127 }.AsSlice().ToInt64(), Is.EqualTo(long.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }.AsSlice().ToInt64(), Is.EqualTo(-1L));

			// should validate the arguments
			var x = new byte[] { 0xF0, 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice();
			Assert.That(() => MutateOffset(x, -1).ToInt64(), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => MutateCount(x, 9).ToInt64(), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => MutateArray(x, null!).ToInt64(), Throws.InstanceOf<ArgumentOutOfRangeException>());
		}

		#endregion

		#region Big-Endian

		[Test]
		public void Test_Slice_FromInt64BE()
		{
			// 64-bit integers should be encoded in little endian, and with 1, 2, 4 or 8 bytes

			static void Verify(long value, string expected)
			{
				Assert.That(Slice.FromInt64BE(value).ToHexString(), Is.EqualTo(expected), $"Invalid encoding for {value}");
			}

			Verify(0x12, "12");
			Verify(0x1234, "1234");
			Verify(0x123456, "123456");
			Verify(0x12345678, "12345678");
			Verify(0x123456789A, "123456789A");
			Verify(0x123456789ABC, "123456789ABC");
			Verify(0x123456789ABCDE, "123456789ABCDE");
			Verify(0x123456789ABCDEF0, "123456789ABCDEF0");

			Verify(0, "00");
			Verify(1, "01");
			Verify(127, "7F");
			Verify(128, "80");

			Verify(1L << 8, "0100");
			Verify(1L << 16, "010000");
			Verify(1L << 24, "01000000");
			Verify(1L << 32, "0100000000");
			Verify(1L << 40, "010000000000");
			Verify(1L << 48, "01000000000000");
			Verify(1L << 56, "0100000000000000");

			Verify((1L << 8) - 1, "FF");
			Verify((1L << 16) - 1, "FFFF");
			Verify((1L << 24) - 1, "FFFFFF");
			Verify((1L << 32) - 1, "FFFFFFFF");
			Verify((1L << 40) - 1, "FFFFFFFFFF");
			Verify((1L << 48) - 1, "FFFFFFFFFFFF");
			Verify((1L << 56) - 1, "FFFFFFFFFFFFFF");
			Verify(long.MaxValue, "7FFFFFFFFFFFFFFF");

			Verify(-1, "FFFFFFFFFFFFFFFF");
			Verify(-2, "FFFFFFFFFFFFFFFE");
			Verify(-256, "FFFFFFFFFFFFFF00");
			Verify(-65536, "FFFFFFFFFFFF0000");
			Verify(-16777216, "FFFFFFFFFF000000");
			Verify(int.MinValue, "FFFFFFFF80000000");
			Verify(long.MinValue, "8000000000000000");

		}

		[Test]
		public void Test_Slice_FromFixed64BE()
		{
			// FromFixed64 always produce 8 bytes and uses Little Endian

			static void Verify(long value, byte[] expected)
			{
				Assert.That(Slice.FromFixed64BE(value).GetBytes(), Is.EqualTo(expected), $"Invalid encoding for {value}");
			}

			Verify(0L, new byte[8]);
			Verify(1L, [ 0, 0, 0, 0, 0, 0, 0, 1 ]);
			Verify(1L << 8, [ 0, 0, 0, 0, 0, 0, 1, 0 ]);
			Verify(1L << 16, [ 0, 0, 0, 0, 0, 1, 0, 0 ]);
			Verify(1L << 24, [ 0, 0, 0, 0, 1, 0, 0, 0 ]);
			Verify(1L << 32, [ 0, 0, 0, 1, 0, 0, 0, 0 ]);
			Verify(1L << 40, [ 0, 0, 1, 0, 0, 0, 0, 0 ]);
			Verify(1L << 48, [ 0, 1, 0, 0, 0, 0, 0, 0 ]);
			Verify(1L << 56, [ 1, 0, 0, 0, 0, 0, 0, 0 ]);
			Verify(short.MaxValue, [ 0, 0, 0, 0, 0, 0, 127, 255 ]);
			Verify(int.MaxValue, [ 0, 0, 0, 0, 127, 255, 255, 255 ]);
			Verify(long.MaxValue, [ 127, 255, 255, 255, 255, 255, 255, 255 ]);

			Verify(-1L, [ 255, 255, 255, 255, 255, 255, 255, 255 ]);
			Verify(-256L, [ 255, 255, 255, 255, 255, 255, 255, 0 ]);
			Verify(-65536L, [ 255, 255, 255, 255, 255, 255, 0, 0 ]);
			Verify(-16777216L, [ 255, 255, 255, 255, 255, 0, 0, 0 ]);
			Verify(-4294967296L, [ 255, 255, 255, 255, 0, 0, 0, 0 ]);
			Verify(long.MinValue, [ 128, 0, 0, 0, 0, 0, 0, 0 ]);

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				long x = (long)rnd.Next() * rnd.Next() * (rnd.Next(2) == 0 ? +1 : -1);
				var s = Slice.FromFixed64BE(x);
				Assert.That(s.Count, Is.EqualTo(8));
				Assert.That(s.ToInt64BE(), Is.EqualTo(x));
			}
		}

		[Test]
		public void Test_Slice_ToInt64BE()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToInt64BE(), Is.EqualTo(0x12));
			Assert.That(new byte[] { 0x12, 0x34 }.AsSlice().ToInt64BE(), Is.EqualTo(0x1234));
			Assert.That(new byte[] { 0x12, 0x34, 0x56 }.AsSlice().ToInt64BE(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x00, 0x12, 0x34, 0x56 }.AsSlice().ToInt64BE(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78 }.AsSlice().ToInt64BE(), Is.EqualTo(0x12345678));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A }.AsSlice().ToInt64BE(), Is.EqualTo(0x123456789A));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC }.AsSlice().ToInt64BE(), Is.EqualTo(0x123456789ABC));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE }.AsSlice().ToInt64BE(), Is.EqualTo(0x123456789ABCDE));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 }.AsSlice().ToInt64BE(), Is.EqualTo(0x123456789ABCDEF0));

			Assert.That(new byte[] { }.AsSlice().ToInt64BE(), Is.EqualTo(0L));
			Assert.That(new byte[] { 0 }.AsSlice().ToInt64BE(), Is.EqualTo(0L));
			Assert.That(new byte[] { 255 }.AsSlice().ToInt64BE(), Is.EqualTo(255L));
			Assert.That(new byte[] { 1, 0 }.AsSlice().ToInt64BE(), Is.EqualTo(256L));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToInt64BE(), Is.EqualTo(65535L));
			Assert.That(new byte[] { 1, 0, 0 }.AsSlice().ToInt64BE(), Is.EqualTo(1L << 16));
			Assert.That(new byte[] { 0, 1, 0, 0 }.AsSlice().ToInt64BE(), Is.EqualTo(1L << 16));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToInt64BE(), Is.EqualTo((1L << 24) - 1));
			Assert.That(new byte[] { 1, 0, 0, 0 }.AsSlice().ToInt64BE(), Is.EqualTo(1L << 24));
			Assert.That(new byte[] { 1, 0, 0, 0, 0 }.AsSlice().ToInt64BE(), Is.EqualTo(1L << 32));
			Assert.That(new byte[] { 1, 0, 0, 0, 0, 0 }.AsSlice().ToInt64BE(), Is.EqualTo(1L << 40));
			Assert.That(new byte[] { 1, 0, 0, 0, 0, 0, 0 }.AsSlice().ToInt64BE(), Is.EqualTo(1L << 48));
			Assert.That(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }.AsSlice().ToInt64BE(), Is.EqualTo(1L << 56));
			Assert.That(new byte[] { 127, 255, 255, 255 }.AsSlice().ToInt64BE(), Is.EqualTo(int.MaxValue));
			Assert.That(new byte[] { 127, 255, 255, 255, 255, 255, 255, 255 }.AsSlice().ToInt64BE(), Is.EqualTo(long.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }.AsSlice().ToInt64BE(), Is.EqualTo(-1L));

			// should validate the arguments
			var x = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 }.AsSlice();
			Assert.That(() => MutateOffset(x, -1).ToInt64BE(), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => MutateCount(x, 9).ToInt64BE(), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => MutateArray(x, null!).ToInt64BE(), Throws.InstanceOf<ArgumentOutOfRangeException>());
		}

		#endregion

		#endregion

		#region 128-bits

		#region Little-Endian

#if NET8_0_OR_GREATER

		[Test]
		public void Test_Slice_FromInt128()
		{
			// 64-bit integers should be encoded in little endian, and with 1, 2, 4 or 8 bytes

			static void Verify(Int128 value, string expected)
			{
				var s = Slice.FromInt128(value);
				if (s.ToHexString() != expected)
				{
					var x = Slice.FromHexString(expected);
					DumpVersus(s, x);
					Assert.That(s.ToHexString(), Is.EqualTo(expected), $"Invalid encoding for {value}");
				}
			}

			Verify(0x12, "12");
			Verify(0x1234, "3412");
			Verify(0x123456, "563412");
			Verify(0x12345678, "78563412");
			Verify(0x123456789A, "9A78563412");
			Verify(0x123456789ABC, "BC9A78563412");
			Verify(0x123456789ABCDE, "DEBC9A78563412");
			Verify(0x123456789ABCDEF0, "F0DEBC9A78563412");
			Verify(0xDEADBEEFBADC0FEEUL, "EE0FDCBAEFBEADDE");
			Verify(new Int128(0x12, 0), "000000000000000012");
			Verify(new Int128(0x1234, 0), "00000000000000003412");
			Verify(new Int128(0x123456, 0), "0000000000000000563412");
			Verify(new Int128(0x12345678, 0), "000000000000000078563412");
			Verify(new Int128(0x123456789A, 0), "00000000000000009A78563412");
			Verify(new Int128(0x123456789ABC, 0), "0000000000000000BC9A78563412");
			Verify(new Int128(0x123456789ABCDE, 0), "0000000000000000DEBC9A78563412");
			Verify(new Int128(0x123456789ABCDEF0, 0), "0000000000000000F0DEBC9A78563412");

			Verify(0, "00");
			Verify(1, "01");
			Verify(255, "FF");
			Verify(256, "0001");
			Verify(65535, "FFFF");
			Verify(65536, "000001");
			Verify(16777215, "FFFFFF");
			Verify(16777216, "00000001");
			Verify(int.MaxValue, "FFFFFF7F");
			Verify(1L + int.MaxValue, "00000080");
			Verify(long.MaxValue, "FFFFFFFFFFFFFF7F");
			Verify(int.MinValue, "00000080FFFFFFFFFFFFFFFFFFFFFFFF");
			Verify(long.MinValue, "0000000000000080FFFFFFFFFFFFFFFF");

			var rnd = new Random();
			for (int i = 0; i < 100; i++)
			{
				int n = 1 + rnd.Next(16);
				Int128 x;
				if (n <= 8)
				{
					ulong lo = n == 8 ? NextUInt64(rnd) : NextUInt64(rnd, 1UL << (n * 8));
					lo |= 1ul << ((n - 1) * 8);
					x = new Int128(0, lo);
				}
				else
				{
					ulong hi = n == 16 ? NextUInt64(rnd) : NextUInt64(rnd, 1UL << ((n - 8) * 8));
					hi |= 1ul << ((n - 9) * 8);
					ulong lo = NextUInt64(rnd);
					x = new Int128(hi, lo);
				}

				var s = Slice.FromInt128(x);
				Assert.That(s.Count, Is.EqualTo(n));
				Assert.That(s.ToInt128(), Is.EqualTo(x));
			}
		}

		[Test]
		public void Test_Slice_FromFixed128()
		{
			// FromFixed64 always produce 8 bytes and uses Little Endian

			static void Verify(Int128 value, byte[] expected)
			{
				var s = Slice.FromFixed128(value);
				Assert.That(s.Count, Is.EqualTo(16));
				if (!s.Equals(expected))
				{
					Log("Invalid encoding (actual vs expected):");
					DumpVersus(s.Span, expected);
					Assert.That(s.ToHexString(), Is.EqualTo(expected.AsSlice().ToHexString()), $"Invalid encoding for {value}");
				}
			}

			Verify((Int128) 0, new byte[16]);

			Verify((Int128) 1, [ 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 ]);
			Verify((Int128) 1 << 8, [ 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 ]);
			Verify((Int128) 1 << 16, [ 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 ]);
			Verify((Int128) 1 << 24, [ 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 ]);
			Verify((Int128) 1 << 32, [ 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 ]);
			Verify((Int128) 1 << 40, [ 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 ]);
			Verify((Int128) 1 << 48, [ 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 ]);
			Verify((Int128) 1 << 56, [ 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0 ]);
			Verify((Int128) 1 << 64, [ 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0 ]);
			Verify((Int128) 1 << 72, [ 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0 ]);
			Verify((Int128) 1 << 80, [ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0 ]);
			Verify((Int128) 1 << 88, [ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0 ]);
			Verify((Int128) 1 << 96, [ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0 ]);
			Verify((Int128) 1 << 104, [ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0 ]);
			Verify((Int128) 1 << 112, [ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0 ]);
			Verify((Int128) 1 << 120, [ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 ]);

			Verify(sbyte.MaxValue, [ 127, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 ]);
			Verify(short.MaxValue, [ 255, 127, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 ]);
			Verify(int.MaxValue, [ 255, 255, 255, 127, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 ]);
			Verify(long.MaxValue, [ 255, 255, 255, 255, 255, 255, 255, 127, 0, 0, 0, 0, 0, 0, 0, 0 ]);
			Verify(new Int128((ulong) sbyte.MaxValue, ulong.MaxValue), [ 255, 255, 255, 255, 255, 255, 255, 255, 127, 0, 0, 0, 0, 0, 0, 0 ]);
			Verify(new Int128((ulong) short.MaxValue, ulong.MaxValue), [ 255, 255, 255, 255, 255, 255, 255, 255, 255, 127, 0, 0, 0, 0, 0, 0 ]);
			Verify(new Int128((ulong) int.MaxValue, ulong.MaxValue), [ 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 127, 0, 0, 0, 0 ]);
			Verify(new Int128((ulong) long.MaxValue, ulong.MaxValue), [ 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 127 ]);

			Verify(byte.MaxValue, [ 255, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 ]);
			Verify(ushort.MaxValue, [ 255, 255, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 ]);
			Verify(uint.MaxValue, [ 255, 255, 255, 255, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 ]);
			Verify(ulong.MaxValue, [ 255, 255, 255, 255, 255, 255, 255, 255, 0, 0, 0, 0, 0, 0, 0, 0 ]);
			Verify(new Int128(byte.MaxValue, ulong.MaxValue), [ 255, 255, 255, 255, 255, 255, 255, 255, 255, 0, 0, 0, 0, 0, 0, 0 ]);
			Verify(new Int128(ushort.MaxValue, ulong.MaxValue), [ 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 0, 0, 0, 0, 0, 0 ]);
			Verify(new Int128(uint.MaxValue, ulong.MaxValue), [ 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 0, 0, 0, 0 ]);
			Verify(new Int128(ulong.MaxValue, ulong.MaxValue), [ 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 ]);

			Verify(-1L, [ 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 ]);
			Verify(-256L, [ 0, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 ]);
			Verify(-65536L, [ 0, 0, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 ]);
			Verify(-16777216L, [ 0, 0, 0, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 ]);
			Verify(-4294967296L, [ 0, 0, 0, 0, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 ]);
			Verify(-1099511627776L, [ 0, 0, 0, 0, 0, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 ]);
			Verify(-281474976710656L, [ 0, 0, 0, 0, 0, 0, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 ]);
			Verify(-72057594037927936L, [ 0, 0, 0, 0, 0, 0, 0, 255, 255, 255, 255, 255, 255, 255, 255, 255 ]);

			Verify(long.MinValue, [ 0, 0, 0, 0, 0, 0, 0, 128, 255, 255, 255, 255, 255, 255, 255, 255 ]);
			Verify(Int128.MinValue, [ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 128 ]);

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				ulong hi = NextRawBits64(rnd);
				ulong lo = NextRawBits64(rnd);
				var x = new Int128(hi, lo);
				// (hi, lo)
				var s = Slice.FromFixed128(hi, lo);
				Assert.That(s.Count, Is.EqualTo(16));
				Assert.That(s.ToInt128(), Is.EqualTo(x));
				Assert.That(s[..8].ToUInt64(), Is.EqualTo(lo));
				Assert.That(s[8..].ToUInt64(), Is.EqualTo(hi));
				// (Int128)
				s = Slice.FromFixed128(x);
				Assert.That(s.Count, Is.EqualTo(16));
				Assert.That(s.ToInt128(), Is.EqualTo(x));
				Assert.That(s[..8].ToUInt64(), Is.EqualTo(lo));
				Assert.That(s[8..].ToUInt64(), Is.EqualTo(hi));
			}
		}

		[Test]
		public void Test_Slice_ToInt128()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToInt128(), Is.EqualTo((Int128) 0x12));
			Assert.That(new byte[] { 0x34, 0x12 }.AsSlice().ToInt128(), Is.EqualTo((Int128) 0x1234));
			Assert.That(new byte[] { 0x56, 0x34, 0x12 }.AsSlice().ToInt128(), Is.EqualTo((Int128) 0x123456));
			Assert.That(new byte[] { 0x56, 0x34, 0x12, 0x00 }.AsSlice().ToInt128(), Is.EqualTo((Int128) 0x123456));
			Assert.That(new byte[] { 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToInt128(), Is.EqualTo((Int128) 0x12345678));
			Assert.That(new byte[] { 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToInt128(), Is.EqualTo((Int128) 0x123456789A));
			Assert.That(new byte[] { 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToInt128(), Is.EqualTo((Int128) 0x123456789ABC));
			Assert.That(new byte[] { 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToInt128(), Is.EqualTo((Int128) 0x123456789ABCDE));
			Assert.That(new byte[] { 0xF0, 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToInt128(), Is.EqualTo((Int128) 0x123456789ABCDEF0));

			Assert.That(new byte[] { }.AsSlice().ToInt128(), Is.EqualTo((Int128) 0L));
			Assert.That(new byte[] { 0 }.AsSlice().ToInt128(), Is.EqualTo((Int128) 0L));
			Assert.That(new byte[] { 255 }.AsSlice().ToInt128(), Is.EqualTo((Int128) 255L));
			Assert.That(new byte[] { 0, 1 }.AsSlice().ToInt128(), Is.EqualTo((Int128) 256L));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToInt128(), Is.EqualTo((Int128) 65535L));
			Assert.That(new byte[] { 0, 0, 1 }.AsSlice().ToInt128(), Is.EqualTo((Int128) 1L << 16));
			Assert.That(new byte[] { 0, 0, 1, 0 }.AsSlice().ToInt128(), Is.EqualTo((Int128) 1L << 16));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToInt128(), Is.EqualTo((Int128) ((1L << 24) - 1)));
			Assert.That(new byte[] { 0, 0, 0, 1 }.AsSlice().ToInt128(), Is.EqualTo((Int128) 1L << 24));
			Assert.That(new byte[] { 0, 0, 0, 0, 1 }.AsSlice().ToInt128(), Is.EqualTo((Int128) 1L << 32));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 1 }.AsSlice().ToInt128(), Is.EqualTo((Int128) 1L << 40));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 1 }.AsSlice().ToInt128(), Is.EqualTo((Int128) 1L << 48));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }.AsSlice().ToInt128(), Is.EqualTo((Int128) 1L << 56));
			Assert.That(new byte[] { 255, 255, 255, 127 }.AsSlice().ToInt128(), Is.EqualTo((Int128) int.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 127 }.AsSlice().ToInt128(), Is.EqualTo((Int128) long.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }.AsSlice().ToInt128(), Is.EqualTo((Int128) ulong.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 127 }.AsSlice().ToInt128(), Is.EqualTo(Int128.MaxValue));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 128 }.AsSlice().ToInt128(), Is.EqualTo(Int128.MinValue));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 }.AsSlice().ToInt128(), Is.EqualTo((Int128) (-1L)));

			// should validate the arguments
			var x = new byte[] { 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xA, 0xB, 0xC, 0xD, 0xE, 0xF }.AsSlice();
			Assert.That(() => MutateOffset(x, -1).ToInt128(), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => MutateCount(x, 17).ToInt128(), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => MutateArray(x, null!).ToInt128(), Throws.InstanceOf<ArgumentOutOfRangeException>());
		}

#endif

		#endregion

		#endregion

		#endregion

		#region Unsigned...

		#region 32-bits

		#region Little-Endian

		[Test]
		public void Test_Slice_FromUInt32()
		{
			// 32-bit integers should be encoded in little endian, and with 1, 2 or 4 bytes

			static void Verify(uint value, string expected)
			{
				Assert.That(Slice.FromUInt32(value).ToHexString(), Is.EqualTo(expected), $"Invalid encoding for {value}");
			}

			Verify(0x12, "12");
			Verify(0x1234, "3412");
			Verify(0x123456, "563412");
			Verify(0x12345678, "78563412");

			Verify(0, "00");
			Verify(1, "01");
			Verify(255, "FF");
			Verify(256, "0001");
			Verify(65535, "FFFF");
			Verify(65536, "000001");
			Verify(int.MaxValue, "FFFFFF7F");
			Verify(uint.MaxValue, "FFFFFFFF");
		}

		[Test]
		public void Test_Slice_FromFixedU32()
		{
			// FromFixed32 always produce 4 bytes and uses Little Endian

			static void Verify(uint value, byte[] expected)
			{
				Assert.That(Slice.FromFixedU32(value).GetBytes(), Is.EqualTo(expected), $"Invalid encoding for {value}");
			}

			Verify(0, new byte[4]);
			Verify(1, [ 1, 0, 0, 0 ]);
			Verify(256, [ 0, 1, 0, 0 ]);
			Verify(ushort.MaxValue, [ 255, 255, 0, 0 ]);
			Verify(65536, [ 0, 0, 1, 0 ]);
			Verify(16777216, [ 0, 0, 0, 1 ]);
			Verify(int.MaxValue, [ 255, 255, 255, 127 ]);
			Verify(uint.MaxValue, [ 255, 255, 255, 255 ]);

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				uint x = (uint) rnd.Next() + (uint) rnd.Next();
				var s = Slice.FromFixedU32(x);
				Assert.That(s.Count, Is.EqualTo(4));
				Assert.That(s.ToUInt32(), Is.EqualTo(x));
			}
		}

		[Test]
		public void Test_Slice_ToUInt32()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToUInt32(), Is.EqualTo(0x12U));
			Assert.That(new byte[] { 0x34, 0x12 }.AsSlice().ToUInt32(), Is.EqualTo(0x1234U));
			Assert.That(new byte[] { 0x56, 0x34, 0x12 }.AsSlice().ToUInt32(), Is.EqualTo(0x123456U));
			Assert.That(new byte[] { 0x56, 0x34, 0x12, 0x00 }.AsSlice().ToUInt32(), Is.EqualTo(0x123456U));
			Assert.That(new byte[] { 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToUInt32(), Is.EqualTo(0x12345678U));

			Assert.That(new byte[] { }.AsSlice().ToUInt32(), Is.EqualTo(0U));
			Assert.That(new byte[] { 0 }.AsSlice().ToUInt32(), Is.EqualTo(0U));
			Assert.That(new byte[] { 255 }.AsSlice().ToUInt32(), Is.EqualTo(255U));
			Assert.That(new byte[] { 0, 1 }.AsSlice().ToUInt32(), Is.EqualTo(256U));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToUInt32(), Is.EqualTo(65535U));
			Assert.That(new byte[] { 0, 0, 1 }.AsSlice().ToUInt32(), Is.EqualTo(1U << 16));
			Assert.That(new byte[] { 0, 0, 1, 0 }.AsSlice().ToUInt32(), Is.EqualTo(1U << 16));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToUInt32(), Is.EqualTo((1U << 24) - 1U));
			Assert.That(new byte[] { 0, 0, 0, 1 }.AsSlice().ToUInt32(), Is.EqualTo(1U << 24));
			Assert.That(new byte[] { 255, 255, 255, 127 }.AsSlice().ToUInt32(), Is.EqualTo((uint)int.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255 }.AsSlice().ToUInt32(), Is.EqualTo(uint.MaxValue));

			Assert.That(() => new byte[5].AsSlice().ToUInt32(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#region Big-Endian

		[Test]
		public void Test_Slice_FromUInt32BE()
		{
			// 32-bit integers should be encoded in big endian, and with 1, 2 or 4 bytes

			static void Verify(uint value, string expected)
			{
				Assert.That(Slice.FromUInt32BE(value).ToHexString(), Is.EqualTo(expected), $"Invalid encoding for {value}");
			}

			Verify(0x12, "12");
			Verify(0x1234, "1234");
			Verify(0x123456, "123456");
			Verify(0x12345678, "12345678");

			Verify(0, "00");
			Verify(1, "01");
			Verify(255, "FF");
			Verify(256, "0100");
			Verify(65535, "FFFF");
			Verify(65536, "010000");
			Verify(int.MaxValue, "7FFFFFFF");
			Verify(uint.MaxValue, "FFFFFFFF");
		}

		[Test]
		public void Test_Slice_FromFixedU32BE()
		{
			// FromFixedU32BE always produce 4 bytes and uses Big Endian

			static void Verify(uint value, byte[] expected)
			{
				Assert.That(Slice.FromFixedU32BE(value).GetBytes(), Is.EqualTo(expected), $"Invalid encoding for {value}");
			}

			Verify(0, new byte[4]);
			Verify(1, [ 0, 0, 0, 1 ]);
			Verify(256, [ 0, 0, 1, 0 ]);
			Verify(ushort.MaxValue, [ 0, 0, 255, 255 ]);
			Verify(65536, [ 0, 1, 0, 0 ]);
			Verify(16777216, [ 1, 0, 0, 0 ]);
			Verify(int.MaxValue, [ 127, 255, 255, 255 ]);
			Verify(uint.MaxValue, [ 255, 255, 255, 255 ]);

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				uint x = (uint)rnd.Next() + (uint)rnd.Next();
				var s = Slice.FromFixedU32BE(x);
				Assert.That(s.Count, Is.EqualTo(4));
				Assert.That(s.ToUInt32BE(), Is.EqualTo(x));
			}
		}

		[Test]
		public void Test_Slice_ToUInt32BE()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToUInt32BE(), Is.EqualTo(0x12U));
			Assert.That(new byte[] { 0x12, 0x34 }.AsSlice().ToUInt32BE(), Is.EqualTo(0x1234U));
			Assert.That(new byte[] { 0x12, 0x34, 0x56 }.AsSlice().ToUInt32BE(), Is.EqualTo(0x123456U));
			Assert.That(new byte[] { 0x00, 0x12, 0x34, 0x56 }.AsSlice().ToUInt32BE(), Is.EqualTo(0x123456U));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78 }.AsSlice().ToUInt32BE(), Is.EqualTo(0x12345678U));

			Assert.That(new byte[] { }.AsSlice().ToUInt32BE(), Is.EqualTo(0U));
			Assert.That(new byte[] { 0 }.AsSlice().ToUInt32BE(), Is.EqualTo(0U));
			Assert.That(new byte[] { 255 }.AsSlice().ToUInt32BE(), Is.EqualTo(255U));
			Assert.That(new byte[] { 1, 0 }.AsSlice().ToUInt32BE(), Is.EqualTo(256U));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToUInt32BE(), Is.EqualTo(65535U));
			Assert.That(new byte[] { 1, 0, 0 }.AsSlice().ToUInt32BE(), Is.EqualTo(1U << 16));
			Assert.That(new byte[] { 0, 1, 0, 0 }.AsSlice().ToUInt32BE(), Is.EqualTo(1U << 16));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToUInt32BE(), Is.EqualTo((1U << 24) - 1U));
			Assert.That(new byte[] { 1, 0, 0, 0 }.AsSlice().ToUInt32BE(), Is.EqualTo(1U << 24));
			Assert.That(new byte[] { 127, 255, 255, 255 }.AsSlice().ToUInt32BE(), Is.EqualTo((uint)int.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255 }.AsSlice().ToUInt32BE(), Is.EqualTo(uint.MaxValue));

			Assert.That(() => new byte[5].AsSlice().ToUInt32BE(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#endregion

		#region 64-bits

		[Test]
		public void Test_Slice_FromUInt64()
		{
			// 64-bit integers should be encoded in little endian, and with 1, 2, 4 or 8 bytes

			static void Verify(ulong value, string expected)
			{
				Assert.That(Slice.FromUInt64(value).ToHexString(), Is.EqualTo(expected), $"Invalid encoding for {value}");
			}

			Verify(0x12UL, "12");
			Verify(0x1234UL, "3412");
			Verify(0x123456UL, "563412");
			Verify(0x12345678UL, "78563412");
			Verify(0x123456789AUL, "9A78563412");
			Verify(0x123456789ABCUL, "BC9A78563412");
			Verify(0x123456789ABCDEUL, "DEBC9A78563412");
			Verify(0x123456789ABCDEF0UL, "F0DEBC9A78563412");

			Verify(0UL, "00");
			Verify(1UL, "01");
			Verify(255UL, "FF");
			Verify(256UL, "0001");
			Verify(ushort.MaxValue, "FFFF");
			Verify(65536UL, "000001");
			Verify(16777215UL, "FFFFFF");
			Verify(16777216UL, "00000001");
			Verify(int.MaxValue, "FFFFFF7F");
			Verify(16777216UL, "00000001");
			Verify(uint.MaxValue + 1UL, "0000000001");
			Verify(long.MaxValue, "FFFFFFFFFFFFFF7F");
			Verify(ulong.MaxValue, "FFFFFFFFFFFFFFFF");

		}

		[Test]
		public void Test_Slice_FromFixedU64()
		{
			// FromFixed64 always produce 8 bytes and uses Little Endian

			static void Verify(ulong value, byte[] expected)
			{
				Assert.That(Slice.FromFixedU64(value).GetBytes(), Is.EqualTo(expected), $"Invalid encoding for {value}");
			}

			Verify(0UL, new byte[8]);
			Verify(1UL, [ 1, 0, 0, 0, 0, 0, 0, 0 ]);
			Verify(1UL << 8, [ 0, 1, 0, 0, 0, 0, 0, 0 ]);
			Verify(1UL << 16, [ 0, 0, 1, 0, 0, 0, 0, 0 ]);
			Verify(1UL << 24, [ 0, 0, 0, 1, 0, 0, 0, 0 ]);
			Verify(1UL << 32, [ 0, 0, 0, 0, 1, 0, 0, 0 ]);
			Verify(1UL << 40, [ 0, 0, 0, 0, 0, 1, 0, 0 ]);
			Verify(1UL << 48, [ 0, 0, 0, 0, 0, 0, 1, 0 ]);
			Verify(1UL << 56, [ 0, 0, 0, 0, 0, 0, 0, 1 ]);
			Verify(ushort.MaxValue, [ 255, 255, 0, 0, 0, 0, 0, 0 ]);
			Verify(int.MaxValue, [ 255, 255, 255, 127, 0, 0, 0, 0 ]);
			Verify(uint.MaxValue, [ 255, 255, 255, 255, 0, 0, 0, 0 ]);
			Verify(long.MaxValue, [ 255, 255, 255, 255, 255, 255, 255, 127 ]);
			Verify(ulong.MaxValue, [ 255, 255, 255, 255, 255, 255, 255, 255 ]);

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				ulong x = (ulong)rnd.Next() * (ulong)rnd.Next();
				var s = Slice.FromFixedU64(x);
				Assert.That(s.Count, Is.EqualTo(8));
				Assert.That(s.ToUInt64(), Is.EqualTo(x));
			}
		}

		[Test]
		public void Test_Slice_ToUInt64()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToUInt64(), Is.EqualTo(0x12));
			Assert.That(new byte[] { 0x34, 0x12 }.AsSlice().ToUInt64(), Is.EqualTo(0x1234));
			Assert.That(new byte[] { 0x56, 0x34, 0x12 }.AsSlice().ToUInt64(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x56, 0x34, 0x12, 00 }.AsSlice().ToUInt64(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToUInt64(), Is.EqualTo(0x12345678));
			Assert.That(new byte[] { 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToUInt64(), Is.EqualTo(0x123456789A));
			Assert.That(new byte[] { 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToUInt64(), Is.EqualTo(0x123456789ABC));
			Assert.That(new byte[] { 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToUInt64(), Is.EqualTo(0x123456789ABCDE));
			Assert.That(new byte[] { 0xF0, 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToUInt64(), Is.EqualTo(0x123456789ABCDEF0));

			Assert.That(new byte[] { }.AsSlice().ToUInt64(), Is.EqualTo(0UL));
			Assert.That(new byte[] { 0 }.AsSlice().ToUInt64(), Is.EqualTo(0UL));
			Assert.That(new byte[] { 255 }.AsSlice().ToUInt64(), Is.EqualTo(255UL));
			Assert.That(new byte[] { 0, 1 }.AsSlice().ToUInt64(), Is.EqualTo(256UL));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToUInt64(), Is.EqualTo(65535UL));
			Assert.That(new byte[] { 0, 0, 1 }.AsSlice().ToUInt64(), Is.EqualTo(1UL << 16));
			Assert.That(new byte[] { 0, 0, 1, 0 }.AsSlice().ToUInt64(), Is.EqualTo(1UL << 16));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToUInt64(), Is.EqualTo((1UL << 24) - 1));
			Assert.That(new byte[] { 0, 0, 0, 1 }.AsSlice().ToUInt64(), Is.EqualTo(1UL << 24));
			Assert.That(new byte[] { 0, 0, 0, 0, 1 }.AsSlice().ToUInt64(), Is.EqualTo(1UL << 32));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 1 }.AsSlice().ToUInt64(), Is.EqualTo(1UL << 40));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 1 }.AsSlice().ToUInt64(), Is.EqualTo(1UL << 48));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }.AsSlice().ToUInt64(), Is.EqualTo(1UL << 56));
			Assert.That(new byte[] { 255, 255, 255, 127 }.AsSlice().ToUInt64(), Is.EqualTo(int.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255 }.AsSlice().ToUInt64(), Is.EqualTo(uint.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 127 }.AsSlice().ToUInt64(), Is.EqualTo(long.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }.AsSlice().ToUInt64(), Is.EqualTo(ulong.MaxValue));

			// should validate the arguments
			var x = new byte[] { 0xF0, 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice();
			Assert.That(() => MutateOffset(x, -1).ToUInt64(), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => MutateCount(x, 9).ToUInt64(), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => MutateArray(x, null!).ToUInt64(), Throws.InstanceOf<ArgumentOutOfRangeException>());
		}

#if NET8_0_OR_GREATER

		[Test]
		public void Test_Slice_ToUInt128()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 0x12));
			Assert.That(new byte[] { 0x34, 0x12 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 0x1234));
			Assert.That(new byte[] { 0x56, 0x34, 0x12 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 0x123456));
			Assert.That(new byte[] { 0x56, 0x34, 0x12, 00 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 0x123456));
			Assert.That(new byte[] { 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 0x12345678));
			Assert.That(new byte[] { 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 0x123456789A));
			Assert.That(new byte[] { 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 0x123456789ABC));
			Assert.That(new byte[] { 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 0x123456789ABCDE));
			Assert.That(new byte[] { 0xF0, 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 0x123456789ABCDEF0));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00 }.AsSlice().ToUInt128(), Is.EqualTo(new UInt128(0x0011223344556677UL, 0)));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00 }.AsSlice().ToUInt128(), Is.EqualTo(new UInt128(0x0011223344556677UL, 0x8800000000000000UL)));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00 }.AsSlice().ToUInt128(), Is.EqualTo(new UInt128(0x0011223344556677UL, 0x8899000000000000UL)));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0xAA, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00 }.AsSlice().ToUInt128(), Is.EqualTo(new UInt128(0x0011223344556677UL, 0x8899AA0000000000UL)));
			Assert.That(new byte[] { 0, 0, 0, 0, 0xBB, 0xAA, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00 }.AsSlice().ToUInt128(), Is.EqualTo(new UInt128(0x0011223344556677UL, 0x8899AABB00000000UL)));
			Assert.That(new byte[] { 0, 0, 0, 0xCC, 0xBB, 0xAA, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00 }.AsSlice().ToUInt128(), Is.EqualTo(new UInt128(0x0011223344556677UL, 0x8899AABBCC000000UL)));
			Assert.That(new byte[] { 0, 0, 0xDD, 0xCC, 0xBB, 0xAA, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00 }.AsSlice().ToUInt128(), Is.EqualTo(new UInt128(0x0011223344556677UL, 0x8899AABBCCDD0000UL)));
			Assert.That(new byte[] { 0, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00 }.AsSlice().ToUInt128(), Is.EqualTo(new UInt128(0x0011223344556677UL, 0x8899AABBCCDDEE00UL)));
			Assert.That(new byte[] { 0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA, 0x99, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11, 0x00 }.AsSlice().ToUInt128(), Is.EqualTo(new UInt128(0x0011223344556677UL, 0x8899AABBCCDDEEFFUL)));

			Assert.That(new byte[] { }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 0));
			Assert.That(new byte[] { 0 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 0));
			Assert.That(new byte[] { 255 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 255));
			Assert.That(new byte[] { 0, 1 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 256));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 65535));
			Assert.That(new byte[] { 0, 0, 1 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 1 << 16));
			Assert.That(new byte[] { 0, 0, 1, 0 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 1 << 16));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToUInt128(), Is.EqualTo(((UInt128) 1 << 24) - 1));
			Assert.That(new byte[] { 0, 0, 0, 1 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 1 << 24));
			Assert.That(new byte[] { 0, 0, 0, 0, 1 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 1 << 32));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 1 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 1 << 40));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 1 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 1 << 48));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 1 << 56));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 1 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 1 << 64));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 1 << 72));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 1 << 80));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 1 << 88));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 1 << 96));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 1 << 104));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 1 << 112));
			Assert.That(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) 1 << 120));
			Assert.That(new byte[] { 255, 255, 255, 127 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) int.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) uint.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255, 0, 0, }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) uint.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255, 0, 0, 0, 0 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) uint.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 127 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) long.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) ulong.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 0, 0, 0, 0 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) ulong.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 0, 0, 0, 0, 0, 0, 0, 0 }.AsSlice().ToUInt128(), Is.EqualTo((UInt128) ulong.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255 }.AsSlice().ToUInt128(), Is.EqualTo(((UInt128) 1 << 72) - 1));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 }.AsSlice().ToUInt128(), Is.EqualTo(((UInt128) 1 << 80) - 1));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 }.AsSlice().ToUInt128(), Is.EqualTo(((UInt128) 1 << 88) - 1));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 }.AsSlice().ToUInt128(), Is.EqualTo(((UInt128) 1 << 96) - 1));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 }.AsSlice().ToUInt128(), Is.EqualTo(((UInt128) 1 << 104) - 1));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 }.AsSlice().ToUInt128(), Is.EqualTo(((UInt128) 1 << 112) - 1));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 }.AsSlice().ToUInt128(), Is.EqualTo(((UInt128) 1 << 120) - 1));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 }.AsSlice().ToUInt128(), Is.EqualTo(UInt128.MaxValue));

			// should validate the arguments
			var x = new byte[] { 0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xA, 0xB, 0xC, 0xD, 0xE, 0xF }.AsSlice();
			Assert.That(() => MutateOffset(x, -1).ToUInt128(), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => MutateCount(x, 17).ToUInt128(), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => MutateArray(x, null!).ToUInt128(), Throws.InstanceOf<ArgumentOutOfRangeException>());
		}

#endif

		[Test]
		public void Test_Slice_FromUInt64BE()
		{
			// 64-bit integers should be encoded in big endian, and using from 1 to 8 bytes

			static void Verify(ulong value, string expected)
			{
				Assert.That(Slice.FromUInt64BE(value).ToHexString(), Is.EqualTo(expected), $"Invalid encoding for {value}");
			}

			Verify(0x12UL, "12");
			Verify(0x1234UL, "1234");
			Verify(0x123456UL, "123456");
			Verify(0x12345678UL, "12345678");
			Verify(0x123456789AUL, "123456789A");
			Verify(0x123456789ABCUL, "123456789ABC");
			Verify(0x123456789ABCDEUL, "123456789ABCDE");
			Verify(0x123456789ABCDEF0UL, "123456789ABCDEF0");

			Verify(0UL, "00");
			Verify(1UL, "01");
			Verify(255UL, "FF");
			Verify(256UL, "0100");
			Verify(ushort.MaxValue, "FFFF");
			Verify(65536UL, "010000");
			Verify(16777215UL, "FFFFFF");
			Verify(16777216UL, "01000000");
			Verify(int.MaxValue, "7FFFFFFF");
			Verify(16777216UL, "01000000");
			Verify(uint.MaxValue + 1UL, "0100000000");
			Verify(long.MaxValue, "7FFFFFFFFFFFFFFF");
			Verify(ulong.MaxValue, "FFFFFFFFFFFFFFFF");

		}

		[Test]
		public void Test_Slice_FromFixedU64BE()
		{
			// FromFixed64 always produce 8 bytes and uses Big Endian

			static void Verify(ulong value, byte[] expected)
			{
				Assert.That(Slice.FromFixedU64BE(value).GetBytes(), Is.EqualTo(expected), $"Invalid encoding for {value}");
			}

			Verify(0UL, new byte[8]);
			Verify(1L, [ 0, 0, 0, 0, 0, 0, 0, 1 ]);
			Verify(1L << 8, [ 0, 0, 0, 0, 0, 0, 1, 0 ]);
			Verify(1L << 16, [ 0, 0, 0, 0, 0, 1, 0, 0 ]);
			Verify(1L << 24, [ 0, 0, 0, 0, 1, 0, 0, 0 ]);
			Verify(1L << 32, [ 0, 0, 0, 1, 0, 0, 0, 0 ]);
			Verify(1L << 40, [ 0, 0, 1, 0, 0, 0, 0, 0 ]);
			Verify(1L << 48, [ 0, 1, 0, 0, 0, 0, 0, 0 ]);
			Verify(1L << 56, [ 1, 0, 0, 0, 0, 0, 0, 0 ]);
			Verify(ushort.MaxValue, [ 0, 0, 0, 0, 0, 0, 255, 255 ]);
			Verify(int.MaxValue, [ 0, 0, 0, 0, 127, 255, 255, 255 ]);
			Verify(uint.MaxValue, [ 0, 0, 0, 0, 255, 255, 255, 255 ]);
			Verify(long.MaxValue, [ 127, 255, 255, 255, 255, 255, 255, 255 ]);
			Verify(ulong.MaxValue, [ 255, 255, 255, 255, 255, 255, 255, 255 ]);

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				ulong x = (ulong)rnd.Next() * (ulong)rnd.Next();
				var s = Slice.FromFixedU64BE(x);
				Assert.That(s.Count, Is.EqualTo(8));
				Assert.That(s.ToUInt64BE(), Is.EqualTo(x));
			}
		}

		[Test]
		public void Test_Slice_ToUInt64BE()
		{
			Assert.That(new byte[] { 0x12 }.AsSlice().ToUInt64BE(), Is.EqualTo(0x12));
			Assert.That(new byte[] { 0x12, 0x34 }.AsSlice().ToUInt64BE(), Is.EqualTo(0x1234));
			Assert.That(new byte[] { 0x12, 0x34, 0x56 }.AsSlice().ToUInt64BE(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x00, 0x12, 0x34, 0x56 }.AsSlice().ToUInt64BE(), Is.EqualTo(0x123456));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78 }.AsSlice().ToUInt64BE(), Is.EqualTo(0x12345678));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A }.AsSlice().ToUInt64BE(), Is.EqualTo(0x123456789A));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC }.AsSlice().ToUInt64BE(), Is.EqualTo(0x123456789ABC));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE }.AsSlice().ToUInt64BE(), Is.EqualTo(0x123456789ABCDE));
			Assert.That(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 }.AsSlice().ToUInt64BE(), Is.EqualTo(0x123456789ABCDEF0));

			Assert.That(new byte[] { }.AsSlice().ToUInt64BE(), Is.EqualTo(0L));
			Assert.That(new byte[] { 0 }.AsSlice().ToUInt64BE(), Is.EqualTo(0L));
			Assert.That(new byte[] { 255 }.AsSlice().ToUInt64BE(), Is.EqualTo(255L));
			Assert.That(new byte[] { 1, 0 }.AsSlice().ToUInt64BE(), Is.EqualTo(256L));
			Assert.That(new byte[] { 255, 255 }.AsSlice().ToUInt64BE(), Is.EqualTo(65535L));
			Assert.That(new byte[] { 1, 0, 0 }.AsSlice().ToUInt64BE(), Is.EqualTo(1L << 16));
			Assert.That(new byte[] { 0, 1, 0, 0 }.AsSlice().ToUInt64BE(), Is.EqualTo(1L << 16));
			Assert.That(new byte[] { 255, 255, 255 }.AsSlice().ToUInt64BE(), Is.EqualTo((1L << 24) - 1));
			Assert.That(new byte[] { 1, 0, 0, 0 }.AsSlice().ToUInt64BE(), Is.EqualTo(1L << 24));
			Assert.That(new byte[] { 1, 0, 0, 0, 0 }.AsSlice().ToUInt64BE(), Is.EqualTo(1L << 32));
			Assert.That(new byte[] { 1, 0, 0, 0, 0, 0 }.AsSlice().ToUInt64BE(), Is.EqualTo(1L << 40));
			Assert.That(new byte[] { 1, 0, 0, 0, 0, 0, 0 }.AsSlice().ToUInt64BE(), Is.EqualTo(1L << 48));
			Assert.That(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }.AsSlice().ToUInt64BE(), Is.EqualTo(1L << 56));
			Assert.That(new byte[] { 127, 255, 255, 255 }.AsSlice().ToUInt64BE(), Is.EqualTo(int.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255 }.AsSlice().ToUInt64BE(), Is.EqualTo(uint.MaxValue));
			Assert.That(new byte[] { 127, 255, 255, 255, 255, 255, 255, 255 }.AsSlice().ToUInt64BE(), Is.EqualTo(long.MaxValue));
			Assert.That(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }.AsSlice().ToUInt64BE(), Is.EqualTo(ulong.MaxValue));

			// should validate the arguments
			var x = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 }.AsSlice();
			Assert.That(() => MutateOffset(x, -1).ToUInt64BE(), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => MutateCount(x, 9).ToUInt64BE(), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => MutateArray(x, null!).ToUInt64BE(), Throws.InstanceOf<ArgumentOutOfRangeException>());
		}

		#endregion

		#endregion

		#region Floating Point...

		private static string SwapHexa(string hexa)
		{
			char[] res = new char[hexa.Length];
			int p = 0;
			for (int i = hexa.Length - 2; i >= 0; i -= 2, p += 2)
			{
				res[i + 0] = hexa[p + 0];
				res[i + 1] = hexa[p + 1];
			}
			return new string(res);
		}

		[Test]
		public void Test_Slice_FromSingle()
		{
			static void Verify(float value, string expected)
			{
				Assert.That(Slice.FromSingle(value).ToHexString(), Is.EqualTo(expected), $"Invalid encoding for {value} (Little Endian)");
				Assert.That(Slice.FromSingleBE(value).ToHexString(), Is.EqualTo(SwapHexa(expected)), $"Invalid encoding for {value} (Big Endian)");
			}

			Verify(0f, "00000000");
			Verify(1f, "0000803F");
			Verify(-1f, "000080BF");
			Verify(10f, "00002041");
			Verify(0.1f, "CDCCCC3D");
			Verify(0.5f, "0000003F");

			Verify(1f / 3f, "ABAAAA3E");
			Verify((float) Math.PI, "DB0F4940");
			Verify((float) Math.E, "54F82D40");

			Verify(float.NaN, "0000C0FF");
			Verify(float.Epsilon, "01000000");
			Verify(float.MaxValue, "FFFF7F7F");
			Verify(float.MinValue, "FFFF7FFF");
			Verify(float.PositiveInfinity, "0000807F");
			Verify(float.NegativeInfinity, "000080FF");
		}

		[Test]
		public void Test_Slice_ToSingle()
		{
			static void Verify(string value, float expected)
			{
				Assert.That(Slice.FromHexString(value).ToSingle(), Is.EqualTo(expected), $"Invalid decoding for '{value}' (Little Endian)");
				Assert.That(Slice.FromHexString(SwapHexa(value)).ToSingleBE(), Is.EqualTo(expected), $"Invalid decoding for '{value}' (Big Endian)");
			}

			Assert.That(Slice.Empty.ToSingle(), Is.EqualTo(0d));
			Verify("00000000", 0f);
			Verify("0000803F", 1f);
			Verify("000080BF", -1f);
			Verify("00002041", 10f);
			Verify("CDCCCC3D", 0.1f);
			Verify("0000003F", 0.5f);

			Verify("ABAAAA3E", 1f / 3f);
			Verify("DB0F4940", (float) Math.PI);
			Verify("54F82D40", (float) Math.E);

			Verify("0000C0FF", float.NaN);
			Verify("01000000", float.Epsilon);
			Verify("FFFF7F7F", float.MaxValue);
			Verify("FFFF7FFF", float.MinValue);
			Verify("0000807F", float.PositiveInfinity);
			Verify("000080FF", float.NegativeInfinity);

			Assert.That(() => new byte[5].AsSlice().ToSingle(), Throws.InstanceOf<FormatException>());
			Assert.That(() => new byte[5].AsSlice().ToSingle(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_Slice_FromDouble()
		{
			static void Verify(double value, string expected)
			{
				Assert.That(Slice.FromDouble(value).ToHexString(), Is.EqualTo(expected), $"Invalid encoding for {value} (Little Endian)");
				Assert.That(Slice.FromDoubleBE(value).ToHexString(), Is.EqualTo(SwapHexa(expected)), $"Invalid encoding for {value} (Big Endian)");
			}

			Verify(0d, "0000000000000000");
			Verify(1d, "000000000000F03F");
			Verify(-1d, "000000000000F0BF");
			Verify(10d, "0000000000002440");
			Verify(0.1d, "9A9999999999B93F");
			Verify(0.5d, "000000000000E03F");

			Verify(1d / 3d, "555555555555D53F");
			Verify(Math.PI, "182D4454FB210940");
			Verify(Math.E, "6957148B0ABF0540");

			Verify(double.NaN, "000000000000F8FF");
			Verify(double.Epsilon, "0100000000000000");
			Verify(double.MaxValue, "FFFFFFFFFFFFEF7F");
			Verify(double.MinValue, "FFFFFFFFFFFFEFFF");
			Verify(double.PositiveInfinity, "000000000000F07F");
			Verify(double.NegativeInfinity, "000000000000F0FF");

		}

		[Test]
		public void Test_Slice_ToDouble()
		{
			static void Verify(string value, double expected)
			{
				Assert.That(Slice.FromHexString(value).ToDouble(), Is.EqualTo(expected), $"Invalid decoding for '{value}' (Little Endian)");
				Assert.That(Slice.FromHexString(SwapHexa(value)).ToDoubleBE(), Is.EqualTo(expected), $"Invalid decoding for '{value}' (Big Endian)");
			}

			Verify("", 0d);
			Verify("0000000000000000", 0d);
			Verify("000000000000F03F", 1d);
			Verify("000000000000F0BF", -1d);
			Verify("0000000000002440", 10d);
			Verify("9A9999999999B93F", 0.1d);
			Verify("000000000000E03F", 0.5d);

			Verify("555555555555D53F", 1d / 3d);
			Verify("182D4454FB210940", Math.PI);
			Verify("6957148B0ABF0540", Math.E);

			Verify("000000000000F8FF", double.NaN);
			Verify("0100000000000000", double.Epsilon);
			Verify("FFFFFFFFFFFFEF7F", double.MaxValue);
			Verify("FFFFFFFFFFFFEFFF", double.MinValue);
			Verify("000000000000F07F", double.PositiveInfinity);
			Verify("000000000000F0FF", double.NegativeInfinity);

			Assert.That(() => new byte[9].AsSlice().ToDouble(), Throws.InstanceOf<FormatException>());
			Assert.That(() => new byte[7].AsSlice().ToDouble(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_Slice_FromDecimal()
		{
			static void Verify(decimal value, string expected)
			{
				Assert.That(Slice.FromDecimal(value).ToHexString(), Is.EqualTo(expected), $"Invalid encoding for {value}");
			}

			Verify(0m, "00000000000000000000000000000000");
			Verify(1m, "00000000000000000100000000000000");
			Verify(-1m, "00000080000000000100000000000000");
			Verify(10m, "00000000000000000A00000000000000");
			Verify(0.1m, "00000100000000000100000000000000");
			Verify(0.5m, "00000100000000000500000000000000");

			Verify(1m / 3m, "00001C00CA44C50A55555505CB00B714");
			Verify((decimal) Math.PI, "00000E000000000083246AE7B91D0100");
			Verify((decimal)Math.E, "00000E0000000000D04947EE39F70000");

			Verify(decimal.MaxValue, "00000000FFFFFFFFFFFFFFFFFFFFFFFF");
			Verify(decimal.MinValue, "00000080FFFFFFFFFFFFFFFFFFFFFFFF");

		}

		[Test]
		public void Test_Slice_ToDecimal()
		{
			static void Verify(string value, decimal expected)
			{
				Assert.That(Slice.FromHexString(value).ToDecimal(), Is.EqualTo(expected), $"Invalid decoding for '{value}'");
			}

			Verify("", 0m);
			Verify("00000000000000000000000000000000", 0m);
			Verify("00000000000000000100000000000000", 1m);
			Verify("00000080000000000100000000000000", -1m);
			Verify("00000000000000000A00000000000000", 10m);
			Verify("00000100000000000100000000000000", 0.1m);
			Verify("00000100000000000500000000000000", 0.5m);

			Verify("00001C00CA44C50A55555505CB00B714", 1m / 3m);
			Verify("00000E000000000083246AE7B91D0100", (decimal) Math.PI);
			Verify("00000E0000000000D04947EE39F70000", (decimal) Math.E);

			Verify("00000000FFFFFFFFFFFFFFFFFFFFFFFF", decimal.MaxValue);
			Verify("00000080FFFFFFFFFFFFFFFFFFFFFFFF", decimal.MinValue);

			Assert.That(() => new byte[15].AsSlice().ToDecimal(), Throws.InstanceOf<FormatException>());
			Assert.That(() => new byte[17].AsSlice().ToDecimal(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#region UUIDs...

		[Test]
		public void Test_Slice_FromGuid()
		{
			// Verify that System.GUID are stored as UUIDs using RFC 4122, and not their natural in-memory format

			// empty guid should be all zeroes
			var slice = Slice.FromGuid(Guid.Empty);
			Assert.That(slice.ToHexString(), Is.EqualTo("00000000000000000000000000000000"));

			// GUIDs should be stored using RFC 4122 (big endian)
			var guid = new Guid("00112233-4455-6677-8899-aabbccddeeff");

			// byte order should follow the string!
			slice = Slice.FromGuid(guid);
			Assert.That(slice.ToHexString(), Is.EqualTo("00112233445566778899AABBCCDDEEFF"), "Slice.FromGuid() should use the RFC 4122 encoding");

			// but guid in memory should follow MS format
			slice = guid.ToByteArray().AsSlice(); // <-- this is BAD, don't try this at home !
			Assert.That(slice.ToHexString(), Is.EqualTo("33221100554477668899AABBCCDDEEFF"));
		}

		[Test]
		public void Test_Slice_ToGuid()
		{
			// nil or empty should return Guid.Empty
			Assert.That(Slice.Nil.ToGuid(), Is.EqualTo(Guid.Empty));
			Assert.That(Slice.Empty.ToGuid(), Is.EqualTo(Guid.Empty));

			// all zeroes should also return Guid.Empty
			var slice = new byte[16].AsSlice();
			Assert.That(slice.ToGuid(), Is.EqualTo(Guid.Empty));

			// RFC 4122 encoded UUIDs should be properly reversed when converted to System.GUID
			slice = Slice.FromHexString("00112233445566778899aabbccddeeff");
			Guid guid = slice.ToGuid();
			Assert.That(guid.ToString(), Is.EqualTo("00112233-4455-6677-8899-aabbccddeeff"), "slice.ToGuid() should convert RFC 4122 encoded UUIDs into native System.Guid");

			// round-trip
			guid = Guid.NewGuid();
			Assert.That(Slice.FromGuid(guid).ToGuid(), Is.EqualTo(guid));

			Assert.That(Slice.FromStringAscii(guid.ToString()).ToGuid(), Is.EqualTo(guid), "String literals should also be converted if they match the expected format");

			Assert.That(() => Slice.FromStringAscii("random text").ToGuid(), Throws.InstanceOf<FormatException>());

			// should validate the arguments
			var x = Slice.FromGuid(guid);
			Assert.That(() => MutateOffset(x, -1).ToGuid(), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => MutateCount(x, 17).ToGuid(), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => MutateArray(x, null!).ToGuid(), Throws.InstanceOf<ArgumentOutOfRangeException>());

		}

		[Test]
		public void Test_Slice_FromUuid128()
		{
			// Verify that FoundationDb.Client.Uuid are stored as 128-bit UUIDs using RFC 4122

			// empty guid should be all zeroes
			var slice = Slice.FromUuid128(Uuid128.Empty);
			Assert.That(slice.ToHexString(), Is.EqualTo("00000000000000000000000000000000"));

			// UUIDs should be stored using RFC 4122 (big endian)
			var uuid = Uuid128.Parse("00112233-4455-6677-8899-aabbccddeeff");

			// byte order should follow the string!
			slice = Slice.FromUuid128(uuid);
			Assert.That(slice.ToHexString(), Is.EqualTo("00112233445566778899AABBCCDDEEFF"), "Slice.FromUuid() should preserve RFC 4122 ordering");

			// ToByteArray() should also be safe
			slice = uuid.ToByteArray().AsSlice();
			Assert.That(slice.ToHexString(), Is.EqualTo("00112233445566778899AABBCCDDEEFF"));
		}

		[Test]
		public void Test_Slice_ToUuid128()
		{
			// nil or empty should return Uuid128.Empty
			Uuid128 uuid = Slice.Nil.ToUuid128();
			Assert.That(uuid, Is.EqualTo(Uuid128.Empty));
			uuid = Slice.Empty.ToUuid128();
			Assert.That(uuid, Is.EqualTo(Uuid128.Empty));

			// all zeroes should also return Uuid128.Empty
			var slice = new byte[16].AsSlice();
			Assert.That(slice.ToUuid128(), Is.EqualTo(Uuid128.Empty));

			// RFC 4122 encoded UUIDs should not keep the byte ordering
			slice = Slice.FromHexString("00112233445566778899aabbccddeeff");
			uuid = slice.ToUuid128();
			Assert.That(uuid.ToString(), Is.EqualTo("00112233-4455-6677-8899-aabbccddeeff"), "slice.ToUuid() should preserve RFC 4122 ordering");

			// round-trip
			uuid = Uuid128.NewUuid();
			Assert.That(Slice.FromUuid128(uuid).ToUuid128(), Is.EqualTo(uuid));

			Assert.That(Slice.FromStringAscii(uuid.ToString()).ToUuid128(), Is.EqualTo(uuid), "String literals should also be converted if they match the expected format");

			Assert.That(() => Slice.FromStringAscii("random text").ToUuid128(), Throws.InstanceOf<FormatException>());

			// should validate the arguments
			var x = Slice.FromUuid128(uuid);
			Assert.That(() => MutateOffset(x, -1).ToUuid128(), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => MutateCount(x, 17).ToUuid128(), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => MutateArray(x, null!).ToUuid128(), Throws.InstanceOf<ArgumentOutOfRangeException>());
		}

		[Test]
		public void Test_Slice_FromUuid64()
		{
			// Verify that FoundationDb.Client.Uuid64 are stored as 64-bit UUIDs in big-endian

			// empty guid should be all zeroes
			var slice = Slice.FromUuid64(Uuid64.Empty);
			Assert.That(slice.ToHexString(), Is.EqualTo("0000000000000000"));

			// UUIDs should be stored in lexicographical order
			var uuid = Uuid64.Parse("01234567-89abcdef");

			// byte order should follow the string!
			slice = Slice.FromUuid64(uuid);
			Assert.That(slice.ToHexString(), Is.EqualTo("0123456789ABCDEF"), "Slice.FromUuid64() should preserve ordering");

			// ToByteArray() should also be safe
			slice = uuid.ToByteArray().AsSlice();
			Assert.That(slice.ToHexString(), Is.EqualTo("0123456789ABCDEF"));
		}

		[Test]
		public void Test_Slice_ToUuid64()
		{
			// nil or empty should return Uuid64.Empty
			Uuid64 uuid = Slice.Nil.ToUuid64();
			Assert.That(uuid, Is.EqualTo(Uuid64.Empty));
			uuid = Slice.Empty.ToUuid64();
			Assert.That(uuid, Is.EqualTo(Uuid64.Empty));

			// all zeroes should also return Uuid64.Empty
			uuid = new byte[8].AsSlice().ToUuid64();
			Assert.That(uuid, Is.EqualTo(Uuid64.Empty));

			// hexadecimal text representation
			uuid = Slice.FromHexString("0123456789abcdef").ToUuid64();
			Assert.That(uuid.ToInt64(), Is.EqualTo(0x123456789abcdef), "slice.ToUuid64() should preserve ordering");

			// round-trip
			uuid = Uuid64.NewUuid();
			Assert.That(Slice.FromUuid64(uuid).ToUuid64(), Is.EqualTo(uuid));

			Assert.That(Slice.FromStringAscii(uuid.ToString()).ToUuid64(), Is.EqualTo(uuid), "String literals should also be converted if they match the expected format");

			Assert.That(() => Slice.FromStringAscii("random text").ToUuid64(), Throws.InstanceOf<FormatException>());

			// should validate the arguments
			var x = Slice.FromUuid64(uuid);
			Assert.That(() => MutateOffset(x, -1).ToUuid64(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(x, 9).ToUuid64(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(x, null!).ToUuid64(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		[Test]
		public void Test_Slice_FromBase64()
		{
			// null string is Nil slice
			var slice = Slice.FromBase64(default(string)!);
			Assert.That(slice, Is.EqualTo(Slice.Nil));

			// empty string is empty slice
			slice = Slice.FromBase64("");
			Assert.That(slice, Is.EqualTo(Slice.Empty));

			// UUIDs should be stored in lexicographical order
			slice = Slice.FromBase64(Convert.ToBase64String("Hello, World!"u8.ToArray()));
			Assert.That(slice.ToUnicode(), Is.EqualTo("Hello, World!"));

			// malformed
			Assert.That(() => Slice.FromBase64(Convert.ToBase64String("Hello, World!"u8.ToArray()).Substring(1)), Throws.InstanceOf<FormatException>());
			Assert.That(() => Slice.FromBase64("This is not a base64 string!"), Throws.InstanceOf<FormatException>());
		}

		#region Equality / Comparison / HashCodes...

		[Test]
		[SuppressMessage("ReSharper", "EqualExpressionComparison")]
		public void Test_Slice_Equality()
		{
			// a == b == c && x != y && a != x
			var a = new byte[] { 1, 2, 3 }.AsSlice();
			var b = new byte[] { 1, 2, 3 }.AsSlice();
			var c = new byte[] { 0, 1, 2, 3, 4 }.AsSlice(1, 3);
			var x = new byte[] { 4, 5, 6 }.AsSlice();
			var y = new byte[] { 1, 2, 3 }.AsSlice(0, 2);
			var z = new byte[] { 1, 2, 3, 4 }.AsSlice();

			// IEquatable<Slice>
			// equals
			Assert.That(a, Is.EqualTo(a));
			Assert.That(a, Is.EqualTo(b));
			Assert.That(a, Is.EqualTo(c));
			Assert.That(b, Is.EqualTo(a));
			Assert.That(b, Is.EqualTo(b));
			Assert.That(b, Is.EqualTo(c));
			Assert.That(c, Is.EqualTo(a));
			Assert.That(c, Is.EqualTo(b));
			Assert.That(c, Is.EqualTo(c));
			// not equals
			Assert.That(a, Is.Not.EqualTo(x));
			Assert.That(a, Is.Not.EqualTo(y));
			Assert.That(a, Is.Not.EqualTo(z));

			// Default Comparer
			// equals
			Assert.That(Slice.Comparer.Default.Equals(a, a), Is.True);
			Assert.That(Slice.Comparer.Default.Equals(a, b), Is.True);
			Assert.That(Slice.Comparer.Default.Equals(a, c), Is.True);
			Assert.That(Slice.Comparer.Default.Equals(b, a), Is.True);
			Assert.That(Slice.Comparer.Default.Equals(b, b), Is.True);
			Assert.That(Slice.Comparer.Default.Equals(b, c), Is.True);
			Assert.That(Slice.Comparer.Default.Equals(c, a), Is.True);
			Assert.That(Slice.Comparer.Default.Equals(c, b), Is.True);
			Assert.That(Slice.Comparer.Default.Equals(c, c), Is.True);
			// not equals
			Assert.That(Slice.Comparer.Default.Equals(a, x), Is.False);
			Assert.That(Slice.Comparer.Default.Equals(a, y), Is.False);
			Assert.That(Slice.Comparer.Default.Equals(a, z), Is.False);

			// Operators
			// ==
			Assert.That(a == a, Is.True);
			Assert.That(a == b, Is.True);
			Assert.That(a == c, Is.True);
			Assert.That(b == a, Is.True);
			Assert.That(b == b, Is.True);
			Assert.That(b == c, Is.True);
			Assert.That(c == a, Is.True);
			Assert.That(c == b, Is.True);
			Assert.That(c == c, Is.True);
			Assert.That(a == x, Is.False);
			Assert.That(a == y, Is.False);
			Assert.That(a == z, Is.False);
			// !=
			Assert.That(a != a, Is.False);
			Assert.That(a != b, Is.False);
			Assert.That(a != c, Is.False);
			Assert.That(b != a, Is.False);
			Assert.That(b != b, Is.False);
			Assert.That(b != c, Is.False);
			Assert.That(c != a, Is.False);
			Assert.That(c != b, Is.False);
			Assert.That(c != c, Is.False);
			Assert.That(a != x, Is.True);
			Assert.That(a != y, Is.True);
			Assert.That(a != z, Is.True);
		}

		[Test]
		public void Test_Slice_Equals_Slice()
		{

			var a = new byte[] { 1, 2, 3 }.AsSlice();
			var b = new byte[] { 1, 2, 3 }.AsSlice();
			var c = new byte[] { 0, 1, 2, 3, 4 }.AsSlice(1, 3);
			var x = new byte[] { 4, 5, 6 }.AsSlice();
			var y = new byte[] { 1, 2, 3 }.AsSlice(0, 2);
			var z = new byte[] { 1, 2, 3, 4 }.AsSlice();

			// equals
			Assert.That(a.Equals(a), Is.True);
			Assert.That(a.Equals(b), Is.True);
			Assert.That(a.Equals(c), Is.True);
			Assert.That(b.Equals(a), Is.True);
			Assert.That(b.Equals(b), Is.True);
			Assert.That(b.Equals(c), Is.True);
			Assert.That(c.Equals(a), Is.True);
			Assert.That(c.Equals(b), Is.True);
			Assert.That(c.Equals(c), Is.True);
			Assert.That(Slice.Nil.Equals(Slice.Nil), Is.True);
			Assert.That(Slice.Empty.Equals(Slice.Empty), Is.True);

			// not equals
			Assert.That(a.Equals(x), Is.False);
			Assert.That(a.Equals(y), Is.False);
			Assert.That(a.Equals(z), Is.False);
			Assert.That(a.Equals(Slice.Nil), Is.False);
			Assert.That(a.Equals(Slice.Empty), Is.False);
			Assert.That(Slice.Empty.Equals(Slice.Nil), Is.False);
			Assert.That(Slice.Nil.Equals(Slice.Empty), Is.False);
		}

		[Test]
		public void Test_Slice_Equality_Corner_Cases()
		{
			Assert.That(default(byte[]).AsSlice(), Is.EqualTo(Slice.Nil));
			Assert.That(new byte[0].AsSlice(), Is.EqualTo(Slice.Empty));

			Assert.That(default(byte[]).AsSlice() == Slice.Nil, Is.True, "null == Nil");
			Assert.That(default(byte[]).AsSlice() == Slice.Empty, Is.False, "null != Empty");
			Assert.That(new byte[0].AsSlice() == Slice.Empty, Is.True, "[0] == Empty");
			Assert.That(new byte[0].AsSlice() == Slice.Nil, Is.False, "[0] != Nil");

			// "slice == null" should be the equivalent to "slice.IsNull" so only true for Slice.Nil
#pragma warning disable CS0618 // Type or member is obsolete
			Assert.That(Slice.Nil == null, Is.True, "'Slice.Nil == null' is true");
			Assert.That(Slice.Empty == null, Is.False, "'Slice.Empty == null' is false");
			Assert.That(Slice.FromByte(1) == null, Is.False, "'[1] == null' is false");
			Assert.That(null == Slice.Nil, Is.True, "'Slice.Nil == null' is true");
			Assert.That(null == Slice.Empty, Is.False, "'Slice.Empty == null' is false");
			Assert.That(null == Slice.FromByte(1), Is.False, "'[1] == null' is false");

			// "slice != null" should be the equivalent to "slice.HasValue" so only false for Slice.Nil
			Assert.That(Slice.Nil != null, Is.False, "'Slice.Nil != null' is false");
			Assert.That(Slice.Empty != null, Is.True, "'Slice.Empty != null' is true");
			Assert.That(Slice.FromByte(1) != null, Is.True, "'[1] != null' is true");
			Assert.That(null != Slice.Nil, Is.False, "'Slice.Nil != null' is false");
			Assert.That(null != Slice.Empty, Is.True, "'Slice.Empty != null' is true");
			Assert.That(null != Slice.FromByte(1), Is.True, "'[1] != null' is true");
#pragma warning restore CS0618 // Type or member is obsolete
		}

		[Test]
		public void Test_Slice_Equality_TwoByteArrayWithSameContentShouldReturnTrue()
		{
			var s1 = Slice.FromStringAscii("abcd");
			var s2 = Slice.FromStringAscii("abcd");
			Assert.That(s1.Equals(s2), Is.True, "'abcd' should equals 'abcd'");
		}

		[Test]
		public void Test_Slice_Equality_TwoByteArrayWithSameContentFromSameOriginalBufferShouldReturnTrue()
		{
			var origin = "abcdabcd"u8.ToArray();
			var a1 = new ArraySegment<byte>(origin, 0, 4); //"abcd", refer first part of origin buffer
			var s1 = a1.AsSlice(); //
			var a2 = new ArraySegment<byte>(origin, 4, 4);//"abcd", refer second part of origin buffer
			var s2 = a2.AsSlice();
			Assert.That(s1.Equals(s2), Is.True, "'abcd' should equals 'abcd'");
		}

		[Test]
		public void Test_Slice_Equality_Malformed()
		{
			var good = Slice.FromStringAscii("good");
			var evil = Slice.FromStringAscii("evil");

			// argument should be validated
			Assert.That(() => good.Equals(MutateOffset(evil, -1)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.Equals(MutateCount(evil, 666)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.Equals(MutateArray(evil, null!)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.Equals(MutateOffset(MutateCount(evil, 5), -1)), Throws.InstanceOf<FormatException>());

			// instance should also be validated
			Assert.That(() => MutateOffset(evil, -1).Equals(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(evil, 666).Equals(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(evil, null!).Equals(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateOffset(MutateCount(evil, 5), -1).Equals(good), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_Slice_Hash_Code()
		{
			// note: the test values MAY change if the hashcode algorithm is modified.
			// That means that if all the asserts in this test fail, you should probably ensure that the expected results are still valid.

			Assert.That(Slice.Nil.GetHashCode(), Is.EqualTo(0), "Nil hashcode should always be 0");
			Assert.That(Slice.Empty.GetHashCode(), Is.Not.EqualTo(0), "Empty hashcode should not be equal to 0");

			Assert.That(Slice.FromString("abc").GetHashCode(), Is.EqualTo(Slice.FromString("abc").GetHashCode()), "Hashcode should not depend on the backing array");
			Assert.That(Slice.FromString("zabcz").Substring(1, 3).GetHashCode(), Is.EqualTo(Slice.FromString("abc").GetHashCode()), "Hashcode should not depend on the offset in the array");
			Assert.That(Slice.FromString("abc").GetHashCode(), Is.Not.EqualTo(Slice.FromString("abcd").GetHashCode()), "Hashcode should include all the bytes");

			Assert.That(Slice.Comparer.Default.GetHashCode(Slice.Nil), Is.EqualTo(0), "Nil hashcode should always be 0");
			Assert.That(Slice.Comparer.Default.GetHashCode(Slice.Empty), Is.Not.EqualTo(0), "Empty hashcode should not be equal to 0");
			Assert.That(Slice.Comparer.Default.GetHashCode(Slice.FromString("abc")), Is.EqualTo(Slice.FromString("abc").GetHashCode()), "Hashcode should not depend on the backing array");
			Assert.That(Slice.Comparer.Default.GetHashCode(Slice.FromString("zabcz").Substring(1, 3)), Is.EqualTo(Slice.FromString("abc").GetHashCode()), "Hashcode should not depend on the offset in the array");
			Assert.That(Slice.Comparer.Default.GetHashCode(Slice.FromString("abc")), Is.Not.EqualTo(Slice.FromString("abcd").GetHashCode()), "Hashcode should include all the bytes");

			// should validate the arguments
			var x = Slice.FromString("evil");
			Assert.That(() => MutateOffset(x, -1).GetHashCode(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(x, 17).GetHashCode(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(x, null!).GetHashCode(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		[SuppressMessage("ReSharper", "EqualExpressionComparison")]
		public void Test_Slice_Comparison()
		{
#pragma warning disable 1718
			var a = Slice.FromStringAscii("a");
			var ab = Slice.FromStringAscii("ab");
			var abc = Slice.FromStringAscii("abc");
			var abc2 = Slice.FromStringAscii("abc"); // same bytes but different buffer
			var b = Slice.FromStringAscii("b");

			// CompareTo
			// a = b
			Assert.That(a.CompareTo(a), Is.EqualTo(0));
			Assert.That(ab.CompareTo(ab), Is.EqualTo(0));
			Assert.That(abc.CompareTo(abc), Is.EqualTo(0));
			Assert.That(abc.CompareTo(abc2), Is.EqualTo(0));
			// a < b
			Assert.That(a.CompareTo(b), Is.LessThan(0));
			Assert.That(a.CompareTo(ab), Is.LessThan(0));
			Assert.That(a.CompareTo(abc), Is.LessThan(0));
			// a > b
			Assert.That(b.CompareTo(a), Is.GreaterThan(0));
			Assert.That(b.CompareTo(ab), Is.GreaterThan(0));
			Assert.That(b.CompareTo(abc), Is.GreaterThan(0));

			// Default Comparer
			// a = b
			Assert.That(Slice.Comparer.Default.Compare(a, a), Is.EqualTo(0));
			Assert.That(Slice.Comparer.Default.Compare(ab, ab), Is.EqualTo(0));
			Assert.That(Slice.Comparer.Default.Compare(abc, abc), Is.EqualTo(0));
			Assert.That(Slice.Comparer.Default.Compare(abc, abc2), Is.EqualTo(0));
			// a < b
			Assert.That(Slice.Comparer.Default.Compare(a, b), Is.LessThan(0));
			Assert.That(Slice.Comparer.Default.Compare(a, ab), Is.LessThan(0));
			Assert.That(Slice.Comparer.Default.Compare(a, abc), Is.LessThan(0));
			// a > b
			Assert.That(Slice.Comparer.Default.Compare(b, a), Is.GreaterThan(0));
			Assert.That(Slice.Comparer.Default.Compare(b, ab), Is.GreaterThan(0));
			Assert.That(Slice.Comparer.Default.Compare(b, abc), Is.GreaterThan(0));

			// Operators
			// <
			Assert.That(a < a, Is.False);
			Assert.That(a < ab, Is.True);
			Assert.That(ab < b, Is.True);
			Assert.That(a < b, Is.True);
			Assert.That(ab < a, Is.False);
			Assert.That(b < ab, Is.False);
			Assert.That(b < a, Is.False);
			Assert.That(abc < abc2, Is.False);
			// <=
			Assert.That(a <= a, Is.True);
			Assert.That(a <= ab, Is.True);
			Assert.That(ab <= b, Is.True);
			Assert.That(a <= b, Is.True);
			Assert.That(ab <= a, Is.False);
			Assert.That(b <= ab, Is.False);
			Assert.That(b <= a, Is.False);
			Assert.That(abc <= abc2, Is.True);
			// >
			Assert.That(a > a, Is.False);
			Assert.That(ab > a, Is.True);
			Assert.That(b > ab, Is.True);
			Assert.That(b > a, Is.True);
			Assert.That(a > ab, Is.False);
			Assert.That(ab > b, Is.False);
			Assert.That(a > b, Is.False);
			Assert.That(abc > abc2, Is.False);
			// >=
			Assert.That(a >= a, Is.True);
			Assert.That(ab >= a, Is.True);
			Assert.That(b >= ab, Is.True);
			Assert.That(b >= a, Is.True);
			Assert.That(a >= ab, Is.False);
			Assert.That(ab >= b, Is.False);
			Assert.That(a >= b, Is.False);
			Assert.That(abc >= abc2, Is.True);
#pragma warning restore 1718
		}

		[Test]
		public void Test_Slice_Comparison_Corner_Cases()
		{
			// Nil == Empty
			Assert.That(Slice.Nil.CompareTo(Slice.Nil), Is.EqualTo(0));
			Assert.That(Slice.Empty.CompareTo(Slice.Empty), Is.EqualTo(0));
			Assert.That(Slice.Nil.CompareTo(Slice.Empty), Is.EqualTo(0));
			Assert.That(Slice.Empty.CompareTo(Slice.Nil), Is.EqualTo(0));

			// X > NULL, NULL < X
			var abc = Slice.FromStringAscii("abc");
			Assert.That(abc.CompareTo(Slice.Nil), Is.GreaterThan(0));
			Assert.That(abc.CompareTo(Slice.Empty), Is.GreaterThan(0));
			Assert.That(Slice.Nil.CompareTo(abc), Is.LessThan(0));
			Assert.That(Slice.Empty.CompareTo(abc), Is.LessThan(0));
		}

		[Test]
		public void Test_Slice_Comparison_Malformed()
		{
			var good = Slice.FromStringAscii("good");
			var evil = Slice.FromStringAscii("evil");

			// argument should be validated
			Assert.That(() => good.CompareTo(MutateOffset(evil, -1)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.CompareTo(MutateCount(evil, 666)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.CompareTo(MutateArray(evil, null!)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.CompareTo(MutateOffset(MutateCount(evil, 5), -1)), Throws.InstanceOf<FormatException>());

			// instance should also be validated
			Assert.That(() => MutateOffset(evil, -1).CompareTo(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(evil, 666).CompareTo(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(evil, null!).CompareTo(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateOffset(MutateCount(evil, 5), -1).CompareTo(good), Throws.InstanceOf<FormatException>());
		}

		#endregion

		private const string UNICODE_TEXT = "Thïs Ïs à strîng thât contaÎns somé ùnicodè charactêrs and should be encoded in UTF-8: よろしくお願いします";
		private static readonly byte[] UNICODE_BYTES = "Thïs Ïs à strîng thât contaÎns somé ùnicodè charactêrs and should be encoded in UTF-8: よろしくお願いします"u8.ToArray();

		[Test]
		public void Test_Slice_FromStream()
		{
			Slice slice;

			using(var ms = new MemoryStream(UNICODE_BYTES))
			{
				slice = Slice.FromStream(ms);
			}
			Assert.That(slice.Count, Is.EqualTo(UNICODE_BYTES.Length));
			Assert.That(slice.GetBytes(), Is.EqualTo(UNICODE_BYTES));
			Assert.That(slice.ToUnicode(), Is.EqualTo(UNICODE_TEXT));

			// ReSharper disable once AssignNullToNotNullAttribute
			Assert.That(() => Slice.FromStream(null!), Throws.ArgumentNullException, "Should throw if null");
			Assert.That(Slice.FromStream(Stream.Null), Is.EqualTo(Slice.Nil), "Stream.Null should return Slice.Nil");

			using(var ms = new MemoryStream())
			{
				ms.Close();
				Assert.That(() => Slice.FromStream(ms), Throws.InstanceOf<InvalidOperationException>(), "Reading from a disposed stream should throw");
			}
		}

		[Test]
		public async Task Test_Slice_FromStreamAsync()
		{
			Slice slice;

			// Reading from a MemoryStream should use the non-async path
			using (var ms = new MemoryStream(UNICODE_BYTES))
			{
				slice = await Slice.FromStreamAsync(ms, this.Cancellation);
			}
			Assert.That(slice.Count, Is.EqualTo(UNICODE_BYTES.Length));
			Assert.That(slice.GetBytes(), Is.EqualTo(UNICODE_BYTES));
			Assert.That(slice.ToUnicode(), Is.EqualTo(UNICODE_TEXT));

			// Reading from a FileStream should use the async path
			var tmp = Path.GetTempFileName();
			try
			{
				await File.WriteAllBytesAsync(tmp, UNICODE_BYTES, this.Cancellation);
				using (var fs = File.OpenRead(tmp))
				{
					slice = await Slice.FromStreamAsync(fs, this.Cancellation);
				}
			}
			finally
			{
				File.Delete(tmp);
			}

			Assert.That(slice.Count, Is.EqualTo(UNICODE_BYTES.Length));
			Assert.That(slice.GetBytes(), Is.EqualTo(UNICODE_BYTES));
			Assert.That(slice.ToUnicode(), Is.EqualTo(UNICODE_TEXT));
		}

		[Test]
		public void Test_Slice_Substring()
		{
			Assert.That(Slice.Empty.Substring(0), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Empty.Substring(0, 0), Is.EqualTo(Slice.Empty));
			Assert.That(() => Slice.Empty.Substring(0, 1), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => Slice.Empty.Substring(1), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => Slice.Empty.Substring(1, 0), Throws.Nothing, "We allow out of bound substring if count == 0");

			// Substring(offset)
			Assert.That(Value("Hello, World!").Substring(0), Is.EqualTo(Value("Hello, World!")));
			Assert.That(Value("Hello, World!").Substring(7), Is.EqualTo(Value("World!")));
			Assert.That(Value("Hello, World!").Substring(12), Is.EqualTo(Value("!")));
			Assert.That(Value("Hello, World!").Substring(13), Is.EqualTo(Slice.Empty));
			Assert.That(() => Value("Hello, World!").Substring(14), Throws.InstanceOf<ArgumentOutOfRangeException>());

			// Substring(offset, count)
			Assert.That(Value("Hello, World!").Substring(0, 5), Is.EqualTo(Value("Hello")));
			Assert.That(Value("Hello, World!").Substring(7, 5), Is.EqualTo(Value("World")));
			Assert.That(Value("Hello, World!").Substring(7, 6), Is.EqualTo(Value("World!")));
			Assert.That(Value("Hello, World!").Substring(12, 1), Is.EqualTo(Value("!")));
			Assert.That(Value("Hello, World!").Substring(13, 0), Is.EqualTo(Slice.Empty));
			Assert.That(() => Value("Hello, World!").Substring(7, 7), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => Value("Hello, World!").Substring(13, 1), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => Value("Hello, World!").Substring(7, -1), Throws.InstanceOf<ArgumentOutOfRangeException>());

			// Substring(offset) negative indexing
			Assert.That(Value("Hello, World!").Substring(-1), Is.EqualTo(Value("!")));
			Assert.That(Value("Hello, World!").Substring(-2), Is.EqualTo(Value("d!")));
			Assert.That(Value("Hello, World!").Substring(-6), Is.EqualTo(Value("World!")));
			Assert.That(Value("Hello, World!").Substring(-13), Is.EqualTo(Value("Hello, World!")));
			Assert.That(() => Value("Hello, World!").Substring(-14), Throws.InstanceOf<ArgumentOutOfRangeException>());

			// Slice - int
			Assert.That(Value("Hello, World!") - 0, Is.EqualTo(Value("Hello, World!")));
			Assert.That(Value("Hello, World!") - 1, Is.EqualTo(Value("Hello, World")));
			Assert.That(Value("Hello, World!") - 8, Is.EqualTo(Value("Hello")));
			Assert.That(Value("Hello, World!") - 12, Is.EqualTo(Value("H")));
			Assert.That(Value("Hello, World!") - 13, Is.EqualTo(Slice.Empty));
			Assert.That(() => Value("Hello, World!") - 14, Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => Value("Hello, World!") - (-1), Throws.InstanceOf<ArgumentOutOfRangeException>());
		}

		[Test]
		public void Test_Slice_Concat()
		{
			var a = Value("a");
			var b = Value("b");
			var c = Value("c");
			var ab = Value("ab");
			var bc = Value("bc");
			var abc = Value("abc");

			#region Concat1...

			Assert.That(a.Concat(a), Is.EqualTo(Value("aa")));
			Assert.That(a.Concat(b), Is.EqualTo(Value("ab")));
			Assert.That(a.Concat(bc), Is.EqualTo(Value("abc")));

			Assert.That(a.Concat("bc"u8), Is.EqualTo(Value("abc")));
			Assert.That(ab.Concat("c"u8), Is.EqualTo(Value("abc")));
			Assert.That(ab.Concat(""u8), Is.EqualTo(Value("ab")));

			#endregion

			#region Concat2...

			Assert.That(Slice.Concat(a, a).ToUnicode(), Is.EqualTo("aa"));
			Assert.That(Slice.Concat(a, b).ToUnicode(), Is.EqualTo("ab"));
			Assert.That(Slice.Concat(b, c).ToUnicode(), Is.EqualTo("bc"));

			Assert.That(Slice.Concat(ab, ab).ToUnicode(), Is.EqualTo("abab"));
			Assert.That(Slice.Concat(ab, c).ToUnicode(), Is.EqualTo("abc"));
			Assert.That(Slice.Concat(a, bc).ToUnicode(), Is.EqualTo("abc"));
			Assert.That(Slice.Concat(a, b, c).ToUnicode(), Is.EqualTo("abc"));

			Assert.That(Slice.Concat(abc[0, 2], c).ToUnicode(), Is.EqualTo("abc"));
			Assert.That(Slice.Concat(a, abc[1, 3]).ToUnicode(), Is.EqualTo("abc"));
			Assert.That(Slice.Concat(abc[0, 1], abc[1, 2], abc[2, 3]).ToUnicode(), Is.EqualTo("abc"));

			Assert.That(Slice.Concat(Slice.Empty, Slice.Empty), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Concat(Slice.Nil, Slice.Empty), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Concat(Slice.Empty, Slice.Nil), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Concat(Slice.Nil, Slice.Nil), Is.EqualTo(Slice.Empty));

			Assert.That(Slice.Concat(abc, Slice.Empty), Is.EqualTo(abc));
			Assert.That(Slice.Concat(abc, Slice.Nil), Is.EqualTo(abc));
			Assert.That(Slice.Concat(Slice.Empty, abc), Is.EqualTo(abc));
			Assert.That(Slice.Concat(Slice.Nil, abc), Is.EqualTo(abc));

			Assert.That(Slice.Concat(""u8, ""u8), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Concat("foo"u8, "bar"u8), Is.EqualTo(Value("foobar")));
			Assert.That(Slice.Concat(""u8, "foo"u8), Is.EqualTo(Value("foo")));
			Assert.That(Slice.Concat("foo"u8, ""u8), Is.EqualTo(Value("foo")));

			#endregion

			#region Concat3...

			Assert.That(Slice.Concat(Slice.Empty, b, c), Is.EqualTo(bc));
			Assert.That(Slice.Concat(ab, Slice.Empty, c), Is.EqualTo(abc));
			Assert.That(Slice.Concat(a, b, Slice.Empty), Is.EqualTo(ab));
			Assert.That(Slice.Concat(a, Slice.Empty, Slice.Nil), Is.EqualTo(a));
			Assert.That(Slice.Concat(Slice.Empty, b, Slice.Nil), Is.EqualTo(b));
			Assert.That(Slice.Concat(Slice.Nil, Slice.Empty, c), Is.EqualTo(c));

			Assert.That(Slice.Concat(Slice.Nil, Slice.Nil, Slice.Nil), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Concat(Slice.Empty, Slice.Empty, Slice.Empty), Is.EqualTo(Slice.Empty));

			Assert.That(Slice.Concat(""u8, ""u8, ""u8), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Concat("foo"u8, "bar"u8, "baz"u8), Is.EqualTo(Value("foobarbaz")));
			Assert.That(Slice.Concat(""u8, "foo"u8, "bar"u8), Is.EqualTo(Value("foobar")));
			Assert.That(Slice.Concat("foo"u8, ""u8, "bar"u8), Is.EqualTo(Value("foobar")));
			Assert.That(Slice.Concat("foo"u8, "bar"u8, ""u8), Is.EqualTo(Value("foobar")));

			Assert.That(Slice.Concat(Slice.Nil, Slice.Nil, Slice.Nil, Slice.Nil), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Concat(Slice.Empty, Slice.Empty, Slice.Empty, Slice.Empty), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Concat("foo"u8, "bar"u8, "baz"u8, "jazz"u8), Is.EqualTo(Value("foobarbazjazz")));
			Assert.That(Slice.Concat(Slice.FromBytes("foo"u8), Slice.FromBytes("bar"u8), Slice.FromBytes("baz"u8), Slice.FromBytes("jazz"u8)), Is.EqualTo(Value("foobarbazjazz")));

			#endregion

			#region Concat4...

			Assert.That(Slice.Concat(""u8, ""u8, ""u8, ""u8), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Concat("foo"u8, "bar"u8, "baz"u8, "jazz"u8), Is.EqualTo(Value("foobarbazjazz")));
			Assert.That(Slice.Concat(""u8, "foo"u8, "bar"u8, "baz"u8), Is.EqualTo(Value("foobarbaz")));
			Assert.That(Slice.Concat("foo"u8, ""u8, "bar"u8, "baz"u8), Is.EqualTo(Value("foobarbaz")));
			Assert.That(Slice.Concat("foo"u8, "bar"u8, ""u8, "baz"u8), Is.EqualTo(Value("foobarbaz")));
			Assert.That(Slice.Concat("foo"u8, "bar"u8, "baz"u8, ""u8), Is.EqualTo(Value("foobarbaz")));

			#endregion

			#region ConcatN...

			Assert.That(Slice.Concat(Array.Empty<Slice>()), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Concat([]), Is.EqualTo(Slice.Empty));

			Assert.That(Slice.Concat(a, b, c, ab, bc, abc), Is.EqualTo(Value("abcabbcabc")));
			Assert.That(Slice.Concat([ a, b, c, ab, bc, abc ]), Is.EqualTo(Value("abcabbcabc")));

			Slice[] arr = [ a, b, c, ab, bc, abc ];
			Assert.That(Slice.Concat(arr), Is.EqualTo(Value("abcabbcabc")));
			Assert.That(Slice.Concat(arr.AsSpan()), Is.EqualTo(Value("abcabbcabc")));
			Assert.That(Slice.Concat(new List<Slice>(arr)), Is.EqualTo(Value("abcabbcabc")));
			Assert.That(Slice.Concat((IEnumerable<Slice>) arr), Is.EqualTo(Value("abcabbcabc")));

			#endregion

			#region Slice + Slice...

			Assert.That(a + a, Is.EqualTo(Value("aa")));
			Assert.That(a + b, Is.EqualTo(Value("ab")));
			Assert.That(b + c, Is.EqualTo(Value("bc")));
			Assert.That(ab + ab, Is.EqualTo(Value("abab")));
			Assert.That(ab + c, Is.EqualTo(Value("abc")));
			Assert.That(a + bc, Is.EqualTo(Value("abc")));

			#endregion

			#region Slice + byte

			Assert.That(a + 0, Is.EqualTo(Key("a\x00")));
			Assert.That(a + 1, Is.EqualTo(Key("a\x01")));
			Assert.That(b + (byte)'A', Is.EqualTo(Key("bA")));
			Assert.That(abc + 255, Is.EqualTo(Key("abc\xff")));

			#endregion
		}

		[Test]
		public void Test_Slice_Create_SpanAction()
		{
			{ // length 0 should return empty and not invoke the callback
				var res = Slice.Create(0, "hello", (_, _) => Assert.Fail("Should not be called!"));
				Assert.That(res, Is.EqualTo(Slice.Empty));
			}
			{ // should bubble up any exceptions in the callback
				Assert.That(() => Slice.Create(40, "hello", (_, _) => throw new InvalidOperationException("Ooops")), Throws.InvalidOperationException);
			}
			{ // allocated buffer should have the exact length required

				// fill a buffer with deterministic random bytes
				var expected = new byte[123];
				const int SEED = 0xC0FFEE;
				new Random(SEED).NextBytes(expected);

				var res = Slice.Create(expected.Length, new Random(SEED), (buf, rng) =>
				{
					Assert.That(buf.Length, Is.EqualTo(expected.Length));
					rng.NextBytes(buf);
				});
				Dump(res);
				Assert.That(res, Is.EqualTo(expected.AsSlice()));
			}
			{ // allocated buffer should be cleared before
				var res = Slice.Create(17, "hello", (_, _) => { });
				Assert.That(res, Is.EqualTo(Slice.Zero(17)));
			}
			{ // negative lengthshould throw
				Assert.That(() => Slice.Create(-1, "hello", (_, _) => { }), Throws.InstanceOf<ArgumentException>());
			}
			{ // overflow should throw
				Assert.That(() => Slice.Create(17, "hello", (buf, _) => { buf[17] = 255; }), Throws.InstanceOf<IndexOutOfRangeException>());
			}
		}

		[Test]
		public void Test_Slice_Join_Array()
		{
			var a = Value("A");
			var b = Value("BB");
			var c = Value("CCC");

			// empty separator should just join all slices together
			Assert.That(Slice.Join(Slice.Empty, Array.Empty<Slice>()), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Join(Slice.Empty, new[] { Slice.Empty }), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Join(Slice.Empty, new[] { a }), Is.EqualTo(Value("A")));
			Assert.That(Slice.Join(Slice.Empty, new[] { a, b }), Is.EqualTo(Value("ABB")));
			Assert.That(Slice.Join(Slice.Empty, new[] { a, b, c }), Is.EqualTo(Value("ABBCCC")));
			Assert.That(Slice.Join(Slice.Empty, new[] { a, b, c }).Offset, Is.Zero);
			Assert.That(Slice.Join(Slice.Empty, new[] { a, b, c }).Count, Is.EqualTo(6));

			// single byte separator
			var sep = Slice.FromChar(',');
			Assert.That(Slice.Join(sep, Array.Empty<Slice>()), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Join(sep, new[] { Slice.Empty }), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Join(sep, new[] { a }), Is.EqualTo(Value("A")));
			Assert.That(Slice.Join(sep, new[] { a, b }), Is.EqualTo(Value("A,BB")));
			Assert.That(Slice.Join(sep, new[] { a, b, c }), Is.EqualTo(Value("A,BB,CCC")));
			Assert.That(Slice.Join(sep, new[] { a, b, c }).Offset, Is.Zero);
			Assert.That(Slice.Join(sep, new[] { a, b, c }).Count, Is.EqualTo(8));
			Assert.That(Slice.Join(sep, new[] { a, Slice.Empty, c }), Is.EqualTo(Value("A,,CCC")));
			Assert.That(Slice.Join(sep, new[] { Slice.Empty, b, c }), Is.EqualTo(Value(",BB,CCC")));
			Assert.That(Slice.Join(sep, new[] { Slice.Empty, Slice.Empty, Slice.Empty }), Is.EqualTo(Value(",,")));

			// multiple byte separator, with a non-0 offset
			sep = Value("!<@>!").Substring(1, 3);
			Assert.That(sep.Offset, Is.EqualTo(1));
			Assert.That(Slice.Join(sep, Array.Empty<Slice>()), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Join(sep, new[] { Slice.Empty }), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Join(sep, new[] { a }), Is.EqualTo(Value("A")));
			Assert.That(Slice.Join(sep, new[] { a, b }), Is.EqualTo(Value("A<@>BB")));
			Assert.That(Slice.Join(sep, new[] { a, b, c }), Is.EqualTo(Value("A<@>BB<@>CCC")));
			Assert.That(Slice.Join(sep, new[] { a, b, c }).Offset, Is.Zero);
			Assert.That(Slice.Join(sep, new[] { a, b, c }).Count, Is.EqualTo(12));

			// join slices that use the same underlying buffer
			const string HELLO_WORLD = "hello world!!!";
			byte[] tmp = Encoding.UTF8.GetBytes(HELLO_WORLD);
			var slices = new Slice[tmp.Length];
			for (int i = 0; i < tmp.Length; i++) slices[i] = tmp.AsSlice(i, 1);
			Assert.That(Slice.Join(Slice.Empty, slices), Is.EqualTo(Value(HELLO_WORLD)));
			Assert.That(Slice.Join(Slice.FromChar(':'), slices), Is.EqualTo(Value("h:e:l:l:o: :w:o:r:l:d:!:!:!")));
		}

		[Test]
		public void Test_Slice_Join_Enumerable()
		{
			Assert.That(Slice.Join(Slice.Empty, Enumerable.Empty<Slice>()), Is.EqualTo(Slice.Empty));

			// ReSharper disable PossibleMultipleEnumeration
			var query = Enumerable.Range(1, 3).Select(c => Value(new string((char)(64 + c), c)));
			Assert.That(Slice.Join(Slice.Empty, query), Is.EqualTo(Value("ABBCCC")));
			Assert.That(Slice.Join(Slice.Empty, query).Offset, Is.Zero);
			Assert.That(Slice.Join(Slice.Empty, query).Count, Is.EqualTo(6));

			var sep = Slice.FromChar(',');
			Assert.That(Slice.Join(sep, Enumerable.Empty<Slice>()), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Join(sep, query), Is.EqualTo(Value("A,BB,CCC")));
			Assert.That(Slice.Join(sep, query).Offset, Is.Zero);
			Assert.That(Slice.Join(sep, query).Count, Is.EqualTo(8));

			var arr = query.ToArray();
			Assert.That(Slice.Join(Slice.Empty, (IEnumerable<Slice>)arr), Is.EqualTo(Value("ABBCCC")));
			Assert.That(Slice.Join(Slice.Empty, (IEnumerable<Slice>)arr).Offset, Is.Zero);
			Assert.That(Slice.Join(Slice.Empty, (IEnumerable<Slice>)arr).Count, Is.EqualTo(6));
			// ReSharper restore PossibleMultipleEnumeration
		}

		[Test]
		public void Test_Slice_JoinBytes()
		{
			var sep = Slice.FromChar(' ');
			var tokens = new[] { Value("hello"), Value("world"), Value("!") };

			var joined = Slice.JoinBytes(sep, tokens);
			Assert.That(joined, Is.Not.Null);
			Assert.That(Encoding.ASCII.GetString(joined), Is.EqualTo("hello world !"));

			joined = Slice.JoinBytes(Slice.Empty, tokens);
			Assert.That(joined, Is.Not.Null);
			Assert.That(Encoding.ASCII.GetString(joined), Is.EqualTo("helloworld!"));

			joined = Slice.JoinBytes(sep, tokens, 0, 3);
			Assert.That(joined, Is.Not.Null);
			Assert.That(Encoding.ASCII.GetString(joined), Is.EqualTo("hello world !"));

			joined = Slice.JoinBytes(sep, tokens, 0, 2);
			Assert.That(joined, Is.Not.Null);
			Assert.That(Encoding.ASCII.GetString(joined), Is.EqualTo("hello world"));

			joined = Slice.JoinBytes(sep, tokens, 1, 1);
			Assert.That(joined, Is.Not.Null);
			Assert.That(Encoding.ASCII.GetString(joined), Is.EqualTo("world"));

			joined = Slice.JoinBytes(sep, tokens, 0, 0);
			Assert.That(joined, Is.Not.Null);
			Assert.That(joined.Length, Is.Zero);

			joined = Slice.JoinBytes(sep, [ ], 0, 0);
			Assert.That(joined, Is.Not.Null);
			Assert.That(joined.Length, Is.Zero);

#pragma warning disable IDE0301
			joined = Slice.JoinBytes(sep, Enumerable.Empty<Slice>());
			Assert.That(joined, Is.Not.Null);
			Assert.That(joined.Length, Is.Zero);
#pragma warning restore IDE0301

			// ReSharper disable AssignNullToNotNullAttribute
			Assert.That(() => Slice.JoinBytes(sep, default(Slice[])!, 0, 0), Throws.ArgumentNullException);
			Assert.That(() => Slice.JoinBytes(sep, default(IEnumerable<Slice>)!), Throws.ArgumentNullException);
			// ReSharper restore AssignNullToNotNullAttribute

			Assert.That(() => Slice.JoinBytes(sep, tokens, 0, 4), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => Slice.JoinBytes(sep, tokens, -1, 1), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => Slice.JoinBytes(sep, tokens, 0, -1), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => Slice.JoinBytes(sep, tokens, 3, 1), Throws.InstanceOf<ArgumentOutOfRangeException>());
		}

		[Test]
		public void Test_Slice_Split()
		{
			var a = Value("A");
			var b = Value("BB");
			var c = Value("CCC");
			var comma = Slice.FromChar(',');

			Assert.That(Value("A").Split(comma), Is.EqualTo(new[] { a }));
			Assert.That(Value("A,BB").Split(comma), Is.EqualTo(new[] { a, b }));
			Assert.That(Value("A,BB,CCC").Split(comma), Is.EqualTo(new[] { a, b, c }));

			// empty values should be kept or discarded, depending on the option settings
			Assert.That(Value("A,,CCC").Split(comma), Is.EqualTo(new[] { a, Slice.Empty, c }));
			Assert.That(Value("A,,CCC").Split(comma, StringSplitOptions.RemoveEmptyEntries), Is.EqualTo(new[] { a, c }));

			// edge cases
			// > should behave the same as String.Split()
			Assert.That(Slice.Empty.Split(comma), Is.EqualTo(new [] { Slice.Empty }));
			Assert.That(Slice.Empty.Split(comma, StringSplitOptions.RemoveEmptyEntries), Is.EqualTo(Array.Empty<Slice>()));
			Assert.That(Value("A,").Split(comma), Is.EqualTo(new[] { a, Slice.Empty }));
			Assert.That(Value("A,").Split(comma, StringSplitOptions.RemoveEmptyEntries), Is.EqualTo(new [] { a }));
			Assert.That(Value(",").Split(comma, StringSplitOptions.RemoveEmptyEntries), Is.EqualTo(Array.Empty<Slice>()));
			Assert.That(Value(",,,").Split(comma, StringSplitOptions.RemoveEmptyEntries), Is.EqualTo(Array.Empty<Slice>()));

			// multi-bytes separator with an offset
			var sep = Value("!<@>!").Substring(1, 3);
			Assert.That(Value("A<@>BB<@>CCC").Split(sep), Is.EqualTo(new[] { a, b, c }));
		}

		[Test]
		public void Test_Slice_As_ReadOnlySpan()
		{
			var span = Slice.Nil.Span;
			Assert.That(span.Length, Is.Zero, "Slice.Nil => empty span");

			span = Slice.Empty.Span;
			Assert.That(span.Length, Is.Zero, "Slice.Empty => empty span");

			var buffer = "$$$Hello, World!$$$$$"u8.ToArray();
			var x = buffer.AsSlice(3, 13);
			Assume.That(x.ToStringUtf8(), Is.EqualTo("Hello, World!"));
			var bytes = x.ToArray();

			span = x.Span;
			Assert.That(span.Length, Is.EqualTo(13));
			Assert.That((char)span[0], Is.EqualTo('H'));
			Assert.That(span.ToArray(), Is.EqualTo(bytes));

			span = x.Substring(7).Span;
			Assert.That(span.Length, Is.EqualTo(6));
			Assert.That((char)span[0], Is.EqualTo('W'));
			Assert.That(span.ToArray(), Is.EqualTo("World!"u8.ToArray()));

			span = x.Substring(7, 5).Span;
			Assert.That(span.Length, Is.EqualTo(5));
			Assert.That((char)span[0], Is.EqualTo('W'));
			Assert.That(span.ToArray(), Is.EqualTo("World"u8.ToArray()));

			//note: mutating the slice behind our back should be visible via the span
			span = x.Substring(0, 5).Span;
			Assert.That(span.ToArray(), Is.EqualTo("Hello"u8.ToArray()));
			buffer[4] = (byte) '3';
			Assert.That(span.ToArray(), Is.EqualTo("H3llo"u8.ToArray()));
		}

		[Test]
		public void Test_Slice_FromBytes()
		{
			// byte[]
			Assert.That(Slice.FromBytes(null), Is.EqualTo(Slice.Nil));
			Assert.That(Slice.FromBytes((byte[]) [ ]), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.FromBytes("hello world"u8.ToArray()), Is.EqualTo(Slice.FromStringUtf8("hello world")));

			// byte[], int, int
			Assert.That(Slice.Copy(null, 0, 0), Is.EqualTo(Slice.Nil));
			Assert.That(Slice.Copy((byte[]) [ ], 0, 0), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Copy("xxxhello worldxxx"u8.ToArray(), 3, 11), Is.EqualTo(Slice.FromStringUtf8("hello world")));

			// ReadOnlySpan<byte>
			Assert.That(Slice.FromBytes((ReadOnlySpan<byte>) []), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.FromBytes(""u8), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.FromBytes("xxxhello worldxxx"u8[3..^3]), Is.EqualTo(Slice.FromStringUtf8("hello world")));

			// Span<byte>
			Assert.That(Slice.FromBytes((Span<byte>) [ ]), Is.EqualTo(Slice.Empty));
			{ // stackalloc'ed buffer
				Span<byte> span = stackalloc byte[11];
				"hello world"u8.CopyTo(span);
				Assert.That(Slice.FromBytes(span), Is.EqualTo(Slice.FromStringUtf8("hello world")));
			}

			// ReadOnlySpan<byte> with external buffer
			{
				// buffer is initially empty
				byte[]? buffer = null;
				var slice = Slice.FromBytes("hello world"u8, ref buffer);
				Assert.That(slice, Is.EqualTo(Value("hello world")));
				Assert.That(buffer, Is.Not.Null);
				Assert.That(slice.Array, Is.SameAs(buffer));
				Assert.That(slice.Offset, Is.Zero);

				// reuse the buffer (with a smaller message)
				var prev = buffer;
				var slice2 = Slice.FromBytes("foobar"u8, ref buffer);
				Assert.That(slice2, Is.EqualTo(Value("foobar")));
				Assert.That(buffer, Is.SameAs(prev), "Should have reused the same buffer");
				Assert.That(slice2.Array, Is.SameAs(buffer));
				Assert.That(slice2.Offset, Is.Zero);
				
				//note: the previous slice should be overwritten !
				Assert.That(slice, Is.EqualTo(Value("foobarworld")));

				// replace the buffer if it is too small
				Span<byte> big = stackalloc byte[256];
				big.Fill((byte) '!');
				var slice3 = Slice.FromBytes(big, ref buffer);
				Assert.That(slice3, Is.EqualTo(big.ToArray().AsSlice()));
				Assert.That(buffer, Is.Not.SameAs(prev), "Should have use a bigger buffer");
				Assert.That(slice3.Array, Is.SameAs(buffer));
				Assert.That(slice3.Offset, Is.Zero);

				// previous slices should be left unchanged
				Assert.That(slice, Is.EqualTo(Value("foobarworld")));
				Assert.That(slice2, Is.EqualTo(Value("foobar")));
			}

			// (ReadOnlySpan<byte>).ToSlice()
			Assert.That(""u8.ToSlice(), Is.EqualTo(Slice.Empty));
			Assert.That("hello world"u8.ToSlice(), Is.EqualTo(Slice.FromStringUtf8("hello world")));
			Assert.That("xxxhello worldxxx"u8[3..^3].ToSlice(), Is.EqualTo(Slice.FromStringUtf8("hello world")));

			// (Span<byte>).ToSlice()
			Assert.That(""u8.ToArray().AsSpan().ToSlice(), Is.EqualTo(Slice.Empty));
			Assert.That("hello world"u8.ToArray().AsSpan().ToSlice(), Is.EqualTo(Slice.FromStringUtf8("hello world")));
		}

		[Test]
		public void Test_SliceOwner_FromBytes()
		{
			// just in case, don't pollute the Shared pool
			var pool = ArrayPool<byte>.Create();

			{ // ReadOnlySpan
				using (var owner = Slice.FromBytes("Hello, World!"u8, pool))
				{
					Assert.That(owner.IsValid, Is.True);
					Assert.That(owner.IsPooled, Is.True);
					Assert.That(owner.Pool, Is.SameAs(pool));
					Assert.That(owner.Count, Is.EqualTo(13));
					Assert.That(owner.Data.ToArray(), Is.EqualTo("Hello, World!"u8.ToArray()));
					Assert.That(owner.Span.ToArray(), Is.EqualTo("Hello, World!"u8.ToArray()));
					Assert.That(owner.Memory.ToArray(), Is.EqualTo("Hello, World!"u8.ToArray()));
				}
			}

			{ // byte[]
				using (var owner = Slice.FromBytes("Hello, World!"u8.ToArray(), pool))
				{
					Assert.That(owner.IsValid, Is.True);
					Assert.That(owner.IsPooled, Is.True);
					Assert.That(owner.Pool, Is.SameAs(pool));
					Assert.That(owner.Count, Is.EqualTo(13));
					Assert.That(owner.Data.ToArray(), Is.EqualTo("Hello, World!"u8.ToArray()));
					Assert.That(owner.Span.ToArray(), Is.EqualTo("Hello, World!"u8.ToArray()));
					Assert.That(owner.Memory.ToArray(), Is.EqualTo("Hello, World!"u8.ToArray()));
				}
			}

		}

		[Test]
		public void Test_Slice_CopyTo()
		{
			var array = new byte[100];
			Span<byte> buffer = array;

			{ // Nil
				buffer.Fill(0xAA);
				Slice.Nil.CopyTo(buffer);
				Assert.That(array, Is.All.EqualTo(0xAA));

				Assert.That(Slice.Nil.TryCopyTo(buffer), Is.True);
				Assert.That(array, Is.All.EqualTo(0xAA));

				Assert.That(Slice.Nil.TryCopyTo(buffer, out var written), Is.True);
				Assert.That(written, Is.Zero);
				Assert.That(array, Is.All.EqualTo(0xAA));

			}

			{ // Empty
				buffer.Fill(0xAA);
				Slice.Empty.CopyTo(buffer);
				Assert.That(array, Is.All.EqualTo(0xAA));

				buffer.Fill(0xAA);
				Slice.Empty.CopyTo(buffer, out var written);
				Assert.That(written, Is.Zero);
				Assert.That(array, Is.All.EqualTo(0xAA));

				Assert.That(Slice.Empty.TryCopyTo(buffer), Is.True);
				Assert.That(array, Is.All.EqualTo(0xAA));

				Assert.That(Slice.Empty.TryCopyTo(buffer, out written), Is.True);
				Assert.That(written, Is.Zero);
				Assert.That(array, Is.All.EqualTo(0xAA));
			}

			{ // Larger buffer
				buffer.Fill(0xAA);
				Slice.FromBytes("Hello, World!"u8).CopyTo(buffer);
				Assert.That(buffer[..13].ToArray(), Is.EqualTo("Hello, World!"u8.ToArray()));
				Assert.That(buffer[13..].ToArray(), Is.All.EqualTo(0xAA));

				buffer.Fill(0xAA);
				Slice.FromBytes("Hello, World!"u8).CopyTo(buffer, out var written);
				Assert.That(written, Is.EqualTo(13));
				Assert.That(buffer[..13].ToArray(), Is.EqualTo("Hello, World!"u8.ToArray()));
				Assert.That(buffer[13..].ToArray(), Is.All.EqualTo(0xAA));

				buffer.Fill(0xAA);
				Assert.That(Slice.FromBytes("Hello, World!"u8).TryCopyTo(buffer), Is.True);
				Assert.That(buffer[..13].ToArray(), Is.EqualTo("Hello, World!"u8.ToArray()));
				Assert.That(buffer[13..].ToArray(), Is.All.EqualTo(0xAA));

				buffer.Fill(0xAA);
				Assert.That(Slice.FromBytes("Hello, World!"u8).TryCopyTo(buffer, out written), Is.True);
				Assert.That(written, Is.EqualTo(13));
				Assert.That(buffer[..13].ToArray(), Is.EqualTo("Hello, World!"u8.ToArray()));
				Assert.That(buffer[13..].ToArray(), Is.All.EqualTo(0xAA));
			}

			{ // Exactly sized buffer
				buffer.Fill(0xAA);
				Slice.FromBytes("Hello, World!"u8).CopyTo(buffer[..13]);
				Assert.That(buffer[..13].ToArray(), Is.EqualTo("Hello, World!"u8.ToArray()));
				Assert.That(buffer[13..].ToArray(), Is.All.EqualTo(0xAA));

				buffer.Fill(0xAA);
				Slice.FromBytes("Hello, World!"u8).CopyTo(buffer[..13], out var written);
				Assert.That(written, Is.EqualTo(13));
				Assert.That(buffer[..13].ToArray(), Is.EqualTo("Hello, World!"u8.ToArray()));
				Assert.That(buffer[13..].ToArray(), Is.All.EqualTo(0xAA));

				buffer.Fill(0xAA);
				Assert.That(Slice.FromBytes("Hello, World!"u8).TryCopyTo(buffer[..13]), Is.True);
				Assert.That(buffer[..13].ToArray(), Is.EqualTo("Hello, World!"u8.ToArray()));
				Assert.That(buffer[13..].ToArray(), Is.All.EqualTo(0xAA));

				buffer.Fill(0xAA);
				Assert.That(Slice.FromBytes("Hello, World!"u8).TryCopyTo(buffer[..13], out written), Is.True);
				Assert.That(written, Is.EqualTo(13));
				Assert.That(buffer[..13].ToArray(), Is.EqualTo("Hello, World!"u8.ToArray()));
				Assert.That(buffer[13..].ToArray(), Is.All.EqualTo(0xAA));
			}

			{ // Smaller buffer
				buffer.Fill(0xAA);
				Assert.That(() => Slice.FromBytes("Hello, World!"u8).CopyTo(array.AsSpan(0, 12)), Throws.ArgumentException);
				Assert.That(array, Is.All.EqualTo(0xAA));

				buffer.Fill(0xAA);
				int written = -1;
				Assert.That(() => Slice.FromBytes("Hello, World!"u8).CopyTo(array.AsSpan(0, 12), out written), Throws.ArgumentException);
				Assert.That(written, Is.Zero);
				Assert.That(array, Is.All.EqualTo(0xAA));

				buffer.Fill(0xAA);
				Assert.That(Slice.FromBytes("Hello, World!"u8).TryCopyTo(buffer[..12]), Is.False);
				Assert.That(array, Is.All.EqualTo(0xAA));

				buffer.Fill(0xAA);
				written = -1;
				Assert.That(Slice.FromBytes("Hello, World!"u8).TryCopyTo(buffer[..12], out written), Is.False);
				Assert.That(written, Is.Zero);
				Assert.That(array, Is.All.EqualTo(0xAA));
			}
		}

#if NET8_0_OR_GREATER

		[Test]
		public void Test_Slice_Marshal()
		{
			{ // TryGetSlice
				var data = "hello world"u8.ToArray();

				var memory = data.AsMemory();
				Assert.That(SliceMarshal.TryGetSlice(memory, out var slice), Is.True);
				Assert.That(slice.Count, Is.EqualTo(11));
				Assert.That(slice.Array, Is.SameAs(data));
				Assert.That(slice.Offset, Is.EqualTo(0));
				Assert.That(slice.ToStringUtf8(), Is.EqualTo("hello world"));

				memory = data.AsMemory(6);
				Assert.That(SliceMarshal.TryGetSlice(memory, out slice), Is.True);
				Assert.That(slice.Count, Is.EqualTo(5));
				Assert.That(slice.Array, Is.SameAs(data));
				Assert.That(slice.Offset, Is.EqualTo(6));
				Assert.That(slice.ToStringUtf8(), Is.EqualTo("world"));

				memory = default;
				Assert.That(SliceMarshal.TryGetSlice(memory, out slice), Is.True);
				Assert.That(slice, Is.EqualTo(Slice.Empty));

				memory = data.AsMemory(11, 0);
				Assert.That(SliceMarshal.TryGetSlice(memory, out slice), Is.True);
				Assert.That(slice, Is.EqualTo(Slice.Empty));
			}

			{ // AsRef<int> / Read<int>
				var bytes = Slice.FromFixed32(0x1234);
				ref readonly int ptr = ref SliceMarshal.AsRef<int>(bytes);
				Assert.That(ptr, Is.EqualTo(0x1234));
				// mutate buffer
				bytes.Array[bytes.Offset]++;
				Assert.That(ptr, Is.EqualTo(0x1235));
				// read
				Assert.That(SliceMarshal.Read<int>(bytes), Is.EqualTo(0x1235));
			}
			{ // AsRef<Guid>
				var value = Guid.NewGuid();
				var bytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1)).ToSlice();
				ref readonly Guid ptr = ref SliceMarshal.AsRef<Guid>(bytes);
				Assert.That(ptr, Is.EqualTo(value));
				// read
				Assert.That(SliceMarshal.Read<Guid>(bytes), Is.EqualTo(value));
			}
			{ // AsRef<int> in sub-slice
				Span<int> items = [ 123, 456, 789 ];
				var bytes = MemoryMarshal.AsBytes(items).Slice(4).ToSlice();
				ref readonly int ptr = ref SliceMarshal.AsRef<int>(bytes);
				Assert.That(ptr, Is.EqualTo(456));
				ref readonly int next = ref Unsafe.Add(ref Unsafe.AsRef(in ptr), 1);
				Assert.That(next, Is.EqualTo(789));
				// read
				Assert.That(SliceMarshal.Read<int>(bytes), Is.EqualTo(456));
				Assert.That(SliceMarshal.ReadAt<int>(bytes, 0), Is.EqualTo(456));
				Assert.That(SliceMarshal.ReadAt<int>(bytes, 4), Is.EqualTo(789));
			}

			{ // (Try)GetReference(At|ToLast)
				var bytes = "hello world"u8.ToSlice();

				ref readonly byte start = ref SliceMarshal.GetReference(bytes);
				Assert.That(Unsafe.AreSame(in start, in bytes.Span[0]), Is.True, "start ref should match the start of the slice");
				Assert.That(start, Is.EqualTo('h'));
				Assert.That(SliceMarshal.IsAddressInside(bytes, in start), Is.True);

				ref readonly byte end = ref SliceMarshal.GetReferenceToLast(bytes);
				Assert.That(Unsafe.AreSame(in end, in bytes.Span[^1]), Is.True, "end ref should match the start of the slice");
				Assert.That(end, Is.EqualTo('d'));
				Assert.That(SliceMarshal.IsAddressInside(bytes, in end), Is.True);

				ref readonly byte ptr = ref SliceMarshal.GetReferenceAt(bytes, 6);
				Assert.That(Unsafe.AreSame(in ptr, in bytes.Span[6]), Is.True, "ref should be inside the slice");
				Assert.That(ptr, Is.EqualTo('w'));
				Assert.That(SliceMarshal.IsAddressInside(bytes, in ptr), Is.True);

				ptr = ref SliceMarshal.TryGetReferenceAt(bytes, 6, out var valid);
				Assert.That(valid, Is.True, "Ref should be valid");
				Assert.That(Unsafe.AreSame(in ptr, in bytes.Span[6]), Is.True, "ref should be inside the slice");

				ptr = ref SliceMarshal.TryGetReferenceAt(bytes, 11, out valid);
				Assert.That(valid, Is.False, "Ref should be valid");
				Assert.That(Unsafe.IsNullRef(in ptr), Is.True, "invalid ref should be null");
				Assert.That(SliceMarshal.IsAddressInside(bytes, in Unsafe.Add(ref Unsafe.AsRef(in ptr), 11)), Is.False);

				ptr = ref SliceMarshal.TryGetReferenceAt(bytes, -1, out valid);
				Assert.That(valid, Is.False, "Ref should be valid");
				Assert.That(Unsafe.IsNullRef(in ptr), Is.True, "invalid ref should be null");
				Assert.That(SliceMarshal.IsAddressInside(bytes, in Unsafe.Add(ref Unsafe.AsRef(in ptr), -1)), Is.False);

				Assert.That(() => _ = ref SliceMarshal.GetReferenceAt("hello"u8.ToSlice(), 5), Throws.InstanceOf<ArgumentException>());
				Assert.That(() => _ = ref SliceMarshal.GetReferenceAt("hello"u8.ToSlice(), -1), Throws.InstanceOf<ArgumentException>());

				Assert.That(() => _ = ref SliceMarshal.GetReference(Slice.Nil), Throws.InstanceOf<ArgumentException>());
				Assert.That(() => _ = ref SliceMarshal.GetReference(Slice.Empty), Throws.InstanceOf<ArgumentException>());
				Assert.That(() => _ = ref SliceMarshal.GetReferenceAt(Slice.Nil, 0), Throws.InstanceOf<ArgumentException>());
				Assert.That(() => _ = ref SliceMarshal.GetReferenceAt(Slice.Empty, 0), Throws.InstanceOf<ArgumentException>());
				Assert.That(() => _ = ref SliceMarshal.GetReferenceToLast(Slice.Nil), Throws.InstanceOf<ArgumentException>());
				Assert.That(() => _ = ref SliceMarshal.GetReferenceToLast(Slice.Empty), Throws.InstanceOf<ArgumentException>());
			}

			{ // Cast<int>
				Span<int> items = [ 123, 456, 789 ];
				var bytes = MemoryMarshal.AsBytes(items).ToSlice();
				var span = SliceMarshal.Cast<int>(bytes);
				Assert.That(span.Length, Is.EqualTo(3));
				Assert.That(span[0], Is.EqualTo(123));
				Assert.That(span[1], Is.EqualTo(456));
				Assert.That(span[2], Is.EqualTo(789));
			}

			{ // CopyAsBytes
				Span<int> items = [ 123, 456, 789 ];
				var bytes = SliceMarshal.CopyAsBytes(items);
				Assert.That(bytes.Count, Is.EqualTo(3 * 4));
				DumpHexa(bytes);
				Assert.That(SliceMarshal.ReadAt<int>(bytes, 0), Is.EqualTo(123));
				Assert.That(SliceMarshal.ReadAt<int>(bytes, 4), Is.EqualTo(456));
				Assert.That(SliceMarshal.ReadAt<int>(bytes, 8), Is.EqualTo(789));
			}

		}

#endif

		[Test]
		public void Test_Slice_SpanEncodable()
		{
			{ // Nil
				Assert.That(((ISpanEncodable) Slice.Nil).TryGetSpan(out var span), Is.True);
				Assert.That(span.Length, Is.Zero);

				Assert.That(((ISpanEncodable) Slice.Nil).TryGetSizeHint(out var size), Is.True.WithOutput(size).Zero);

				var buf = new byte[32];
				buf.AsSpan().Fill(0xAA);
				Assert.That(((ISpanEncodable) Slice.Nil).TryEncode(buf, out var written), Is.True.WithOutput(written).Zero);
				Assert.That(buf.AsSpan().ContainsAnyExcept((byte) 0xAA), Is.False);

			}
			{ // Empty
				Assert.That(((ISpanEncodable) Slice.Empty).TryGetSpan(out var span), Is.True);
				Assert.That(span.Length, Is.Zero);

				Assert.That(((ISpanEncodable) Slice.Empty).TryGetSizeHint(out var size), Is.True.WithOutput(size).Zero);

				var buf = new byte[32];
				buf.AsSpan().Fill(0xAA);
				Assert.That(((ISpanEncodable) Slice.Empty).TryEncode(buf, out var written), Is.True.WithOutput(written).Zero);
				Assert.That(buf.AsSpan().ContainsAnyExcept((byte) 0xAA), Is.False);
			}
			{ // Non Empty

				var slice = Slice.FromStringUtf8("Hello");

				Assert.That(((ISpanEncodable) slice).TryGetSpan(out var span), Is.True);
				Assert.That(span.Length, Is.EqualTo(slice.Count));
				Assert.That(span.ToArray(), Is.EqualTo("Hello"u8.ToArray()));

				Assert.That(((ISpanEncodable) slice).TryGetSizeHint(out var size), Is.True.WithOutput(size).EqualTo(slice.Count));

				var buf = new byte[32];

				// large buffer
				buf.AsSpan().Fill(0xAA);
				Assert.That(((ISpanEncodable) slice).TryEncode(buf, out var written), Is.True);
				Assert.That(buf[..written], Is.EqualTo(slice.ToArray()));
				Assert.That(buf.AsSpan(written).ContainsAnyExcept((byte) 0xAA), Is.False);

				// exact buffer size
				buf.AsSpan().Fill(0xAA);
				Assert.That(((ISpanEncodable) slice).TryEncode(buf.AsSpan(0, slice.Count), out written), Is.True);
				Assert.That(buf[..written], Is.EqualTo(slice.ToArray()));
				Assert.That(buf.AsSpan(written).ContainsAnyExcept((byte) 0xAA), Is.False);

				// buffer too small
				buf.AsSpan().Fill(0xAA);
				Assert.That(((ISpanEncodable) slice).TryEncode(buf.AsSpan(0, slice.Count - 1), out written), Is.False.WithOutput(written).Zero);
				Assert.That(buf.AsSpan().ContainsAnyExcept((byte) 0xAA), Is.False);
			}
		}

		[Test]
		public void Test_Slice_ReadExactly_MemoryStream()
		{
			// passing in a memory stream
			var data = "This is a test of the emergency broadcast system."u8.ToArray();
			using (var ms = new MemoryStream(data, 0, data.Length, writable: false, publiclyVisible: true))
			{
				// from the start
				{
					Log($"Reading 14 from position {ms.Position}");
					var chunk = ms.ReadSliceExactly(14);
					Assert.That(chunk.ToStringUtf8(), Is.EqualTo("This is a test"));
					//note: for MemoryStream we expose the original buffer!
					Assert.That(chunk.Array, Is.SameAs(data), "Should expose the original stream buffer");
					Assert.That(chunk.Offset, Is.Zero, ".Offset");
					Assert.That(chunk.Count, Is.EqualTo(14));

					Assert.That(ms.Position, Is.EqualTo(14), "Stream position should be advanced");
				}
				// from current position in stream
				{
					Log($"Reading 34 from position {ms.Position}");
					var chunk = ms.ReadSliceExactly(34);
					Assert.That(chunk.ToStringUtf8(), Is.EqualTo(" of the emergency broadcast system"));
					//note: for MemoryStream we expose the original buffer!
					Assert.That(chunk.Array, Is.SameAs(data), "Should expose the original stream buffer");
					Assert.That(chunk.Offset, Is.EqualTo(14), ".Offset");
					Assert.That(chunk.Count, Is.EqualTo(34));

					Assert.That(ms.Position, Is.EqualTo(48), "Stream position should be advanced");
				}
				// reading past the end
				{
					Log($"Attempting to read 4 from position {ms.Position} (should fail!)");
					Assert.That(() => ms.ReadSliceExactly(4), Throws.Exception, "Reading past the end of the stream should throw!");
					// the position of the stream should be at the end!
					Assert.That(ms.Position, Is.EqualTo(data.Length), "Failed read should have advanced the position to the end of the stream!");
				}
			}
		}

		[Test]
		public async Task Test_Slice_ReadExactly_FileStream()
		{
			string path = GetTemporaryPath("random_stuff.bin");

			try
			{
				if (File.Exists(path)) File.Delete(path);

				// passing in a memory stream
				var data = Slice.Random(new Random(), 123456);
				using (var fs = File.Create(path))
				{
					await fs.WriteAsync(data.Memory, this.Cancellation);
				}

				using (var fs = File.OpenRead(path))
				{
					// from the start
					{
						Log($"Reading 123 from position {fs.Position}");
						var chunk = await fs.ReadSliceExactlyAsync(123, this.Cancellation);
						Assert.That(chunk.Count, Is.EqualTo(123));
						Assert.That(chunk, Is.EqualTo(data.Substring(0, 123)));

						Assert.That(fs.Position, Is.EqualTo(123), "Stream position should be advanced");
					}
					// from current position in stream
					{
						Log($"Reading 122_333 from position {fs.Position}");
						var chunk = fs.ReadSliceExactly(122_333);
						Assert.That(chunk.Count, Is.EqualTo(122_333));
						Assert.That(chunk, Is.EqualTo(data.Substring(123, 122_333)));

						Assert.That(fs.Position, Is.EqualTo(122_456), "Stream position should be advanced");
					}
					// reading past the end
					{
						Log($"Attempting to read 2_000 from position {fs.Position} (should fail!)");
						Assert.That(() => fs.ReadSliceExactly(1_234), Throws.Exception, "Reading past the end of the stream should throw!");
						// the position of the stream should be at the end!
						Assert.That(fs.Position, Is.EqualTo(123456), "Failed read should have advanced the position to the end of the file!");
					}
				}
			}
			finally
			{
				File.Delete(path);
			}
		}

		#region Black Magic Incantations...

		// The Slice struct is not blittable, so we can't take its address and modify it via pointers trickery.
		// Since its ctor is checking the arguments in Debug mode and all its fields are readonly, the only way to inject bad values is to use reflection.

		private static Slice MutateOffset(Slice value, int offset)
		{
			// Don't try this at home !
			object tmp = value;
			typeof(Slice).GetField("Offset")!.SetValue(tmp, offset);
			return (Slice) tmp;
		}

		private static Slice MutateCount(Slice value, int offset)
		{
			// Don't try this at home !
			object tmp = value;
			typeof(Slice).GetField("Offset")!.SetValue(tmp, offset);
			return (Slice) tmp;
		}

		private static Slice MutateArray(Slice value, byte[] array)
		{
			// Don't try this at home !
			object tmp = value;
			typeof(Slice).GetField("Array")!.SetValue(tmp, array);
			return (Slice) tmp;
		}

		#endregion

	}

}
