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
// ReSharper disable ImplicitlyCapturedClosure

namespace SnowBank.Buffers.Tests
{
	//README:IMPORTANT! This source file is expected to be stored as UTF-8! If the encoding is changed, some tests below may fail because they rely on specific code points!

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class SpanDecoderFacts : SimpleTest
	{

		[Test]
		public void Test_Span_Empty()
		{
			ReadOnlySpan<byte> empty = default;

			Assume.That(empty.Length, Is.EqualTo(0));

			Assert.That(empty.ToByteString(), Is.EqualTo(string.Empty));
			Assert.That(empty.ToStringUnicode(), Is.EqualTo(string.Empty));
			Assert.That(empty.ToStringAscii(), Is.EqualTo(string.Empty));
			Assert.That(empty.ToStringUtf8(), Is.EqualTo(string.Empty));
			Assert.That(empty.PrettyPrint(), Is.EqualTo("''"));
		}

		[Test]
		public void Test_Span_With_Content()
		{
			var span = Slice.FromStringAscii("ABC").Span;

			Assert.That(span.Length, Is.EqualTo(3));

			Assert.That(span.ToByteString(), Is.EqualTo("ABC"));
			Assert.That(span.ToStringUnicode(), Is.EqualTo("ABC"));
			Assert.That(span.ToStringAscii(), Is.EqualTo("ABC"));
			Assert.That(span.ToStringUtf8(), Is.EqualTo("ABC"));
			Assert.That(span.PrettyPrint(), Is.EqualTo("'ABC'"));
		}

		[Test]
		public void Test_Span_ToStringAscii()
		{
			Assert.That(default(ReadOnlySpan<byte>).ToStringAscii(), Is.EqualTo(string.Empty));
			Assert.That("A"u8.ToStringAscii(), Is.EqualTo("A"));
			Assert.That("AB"u8.ToStringAscii(), Is.EqualTo("AB"));
			Assert.That("ABC"u8.ToStringAscii(), Is.EqualTo("ABC"));
			Assert.That("ABCD"u8.ToStringAscii(), Is.EqualTo("ABCD"));
			Assert.That(((byte[]) [ 0x7F, 0x00, 0x1F ]).AsSpan().ToStringAscii(), Is.EqualTo("\x7F\x00\x1F"));
			Assert.That("ABCDEF"u8.ToArray().AsSpan(2, 3).ToStringAscii(), Is.EqualTo("CDE"));
			Assert.That("This is a test of the emergency encoding system"u8.ToStringAscii(), Is.EqualTo("This is a test of the emergency encoding system"));

			// If the slice contain anything other than 7+bit ASCII, it should throw!
			Assert.That(() => ((byte[]) [ 0xFF, 0x41, 0x42, 0x43 ]).AsSpan().ToStringAscii(), Throws.Exception, "\\xFF is not valid in 7-bit ASCII strings!");
			Assert.That(() => Encoding.Default.GetBytes("héllô").AsSpan().ToStringAscii(), Throws.Exception, "String that contain code points >= 0x80 should trow");
			Assert.That(() => "héllo 世界"u8.ToStringAscii(), Throws.Exception, "String that contains code points >= 0x80 should throw");
		}

		[Test]
		public void Test_Span_ToStringAnsi()
		{
			//note: FromStringAnsi uses Encoding.Default which varies from system to system! (win-1252, utf8-, ...)

			Assert.That(default(ReadOnlySpan<byte>).ToStringAnsi(), Is.EqualTo(String.Empty));
			Assert.That("ABC"u8.ToStringAnsi(), Is.EqualTo("ABC"));
			Assert.That(Encoding.Default.GetBytes("héllô").AsSpan().ToStringAnsi(), Is.EqualTo("héllô")); //note: this depends on your OS locale!
			Assert.That(new[] { (byte) 0xFF, (byte) '/', (byte) 'A', (byte) 'B', (byte) 'C' }.AsSpan().ToStringAnsi(), Is.EqualTo(Encoding.Default.GetString(new[] { (byte) 0xFF, (byte) '/', (byte) 'A', (byte) 'B', (byte) 'C' })));
		}

		[Test]
		public void Test_Span_ToStringUtf8()
		{
			Assert.That(default(ReadOnlySpan<byte>).ToStringUtf8(), Is.EqualTo(string.Empty));
			Assert.That("ABC"u8.ToStringUtf8(), Is.EqualTo("ABC"));
			Assert.That("héllô"u8.ToStringUtf8(), Is.EqualTo("héllô")); //note: this depends on your OS locale!
			Assert.That("世界"u8.ToStringUtf8(), Is.EqualTo("世界"));

			Assert.That(default(Span<byte>).ToStringUtf8(), Is.EqualTo(string.Empty));
			Assert.That("ABC"u8.ToArray().AsSpan().ToStringUtf8(), Is.EqualTo("ABC"));
			Assert.That("héllô"u8.ToArray().AsSpan().ToStringUtf8(), Is.EqualTo("héllô")); //note: this depends on your OS locale!
			Assert.That("世界"u8.ToArray().AsSpan().ToStringUtf8(), Is.EqualTo("世界"));

			// should remove the bom!
			Assert.That(new byte[] { 0xEF, 0xBB, 0xBF, (byte) 'A', (byte) 'B', (byte) 'C' }.AsSpan().ToStringUtf8(), Is.EqualTo("ABC"), "BOM should be removed");
			Assert.That(new byte[] { 0xEF, 0xBB, 0xBF }.AsSpan().ToStringUtf8(), Is.EqualTo(String.Empty), "BOM should also be removed for empty string");
			Assert.That(new byte[] { 0xEF, 0xBB, 0xBF, 0xEF, 0xBB, 0xBF, (byte) 'A', (byte) 'B', (byte) 'C' }.AsSpan().ToStringUtf8(), Is.EqualTo("\uFEFFABC"), "Only one BOM should be removed");

			// corrupted UTF-8
			Assert.That(() => new byte[] { 0xEF, 0xBB }.AsSpan().ToStringUtf8(), Throws.Exception, "Partial BOM should fail to decode");
			Assert.That(() => new byte[] { (byte) 'A', 0xc3, 0x28, (byte) 'B' }.AsSpan().ToStringUtf8(), Throws.Exception, "Invalid 2-byte sequence");
			Assert.That(() => new byte[] { (byte) 'A', 0xe2, 0x28, 0xa1, (byte) 'B' }.AsSpan().ToStringUtf8(), Throws.Exception, "Invalid 3-byte sequence");
			Assert.That(() => new byte[] { (byte) 'A', 0xf0, 0x28, 0x8c, 0x28, (byte) 'B' }.AsSpan().ToStringUtf8(), Throws.Exception, "Invalid 4-byte sequence");
			Assert.That(() => new byte[] { (byte) 'A', 0xf0, 0x28, /*..SNIP..*/ }.AsSpan().ToStringUtf8(), Throws.Exception, "Truncated 4-byte sequence");
		}

		#region Signed...

		#region 24-bits

		[Test]
		public void Test_Span_ToInt24()
		{
			void Verify(byte[] bytes, int expected)
			{
				string hexa = bytes.AsSlice().ToHexString(' ');
				Assert.That(bytes.AsSpan().ToInt24(), Is.EqualTo(expected), $"Invalid decoding for {hexa}");
				Assert.That(new ReadOnlySpan<byte>(bytes).ToInt24(), Is.EqualTo(expected), $"Invalid decoding for {hexa}");
			}

			Verify([ 0x12 ], 0x12);
			Verify([ 0x34, 0x12 ], 0x1234);
			Verify([ 0x34, 0x12, 0x00 ], 0x1234);
			Verify([ 0x56, 0x34, 0x12 ], 0x123456);

			Verify([ ], 0);
			Verify([ 0 ], 0);
			Verify([ 127 ], 127);
			Verify([ 255 ], 255);
			Verify([ 0, 1 ], 256);
			Verify([ 255, 127 ], 32767);
			Verify([ 255, 255 ], 65535);
			Verify([ 0, 0, 1 ], 1 << 16);
			Verify([ 255, 255, 127 ], (1 << 23) - 1);
			Verify([ 255, 255, 255 ], (1 << 24) - 1);

			Assert.That(() => new byte[4].AsSpan().ToInt24(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_Span_ToInt24BE()
		{
			void Verify(byte[] bytes, int expected)
			{
				string hexa = bytes.AsSlice().ToHexString(' ');
				Assert.That(bytes.AsSpan().ToInt24BE(), Is.EqualTo(expected), $"Invalid decoding for {hexa}");
				Assert.That(new ReadOnlySpan<byte>(bytes).ToInt24BE(), Is.EqualTo(expected), $"Invalid decoding for {hexa}");
			}

			Verify([ 0x12 ], 0x12);
			Verify([ 0x12, 0x34 ], 0x1234);
			Verify([ 0x12, 0x34, 0x56 ], 0x123456);

			Verify([ ], 0);
			Verify([ 0 ], 0);
			Verify([ 127 ], 127);
			Verify([ 255 ], 255);
			Verify([ 1, 0 ], 256);
			Verify([ 127, 255 ], 32767);
			Verify([ 255, 255 ], 65535);
			Verify([ 1, 0, 0 ], 1 << 16);
			Verify([ 127, 255, 255 ], (1 << 23) - 1);
			Verify([ 255, 255, 255 ], (1 << 24) - 1);

			Assert.That(() => new byte[4].AsSpan().ToInt24BE(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#region 32-bits

		[Test]
		public void Test_Span_ToInt32()
		{
			void Verify(byte[] bytes, int expected)
			{
				string hexa = bytes.AsSlice().ToHexString(' ');
				Assert.That(bytes.AsSpan().ToInt32(), Is.EqualTo(expected), $"Invalid decoding for {hexa}");
				Assert.That(new ReadOnlySpan<byte>(bytes).ToInt32(), Is.EqualTo(expected), $"Invalid decoding for {hexa}");
			}

			Verify([ 0x12 ], 0x12);
			Verify([ 0x34, 0x12 ], 0x1234);
			Verify([ 0x56, 0x34, 0x12 ], 0x123456);
			Verify([ 0x56, 0x34, 0x12, 0x00 ], 0x123456);
			Verify([ 0x78, 0x56, 0x34, 0x12 ], 0x12345678);

			Verify([ ], 0);
			Verify([ 0 ], 0);
			Verify([ 255 ], 255);
			Verify([ 0, 1 ], 256);
			Verify([ 255, 255 ], 65535);
			Verify([ 0, 0, 1 ], 1 << 16);
			Verify([ 0, 0, 1, 0 ], 1 << 16);
			Verify([ 255, 255, 255 ], (1 << 24) - 1);
			Verify([ 0, 0, 0, 1 ], 1 << 24);
			Verify([ 255, 255, 255, 127 ], int.MaxValue);

			Assert.That(() => new byte[5].AsSpan().ToInt32(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_Span_ToInt32BE()
		{
			void Verify(byte[] bytes, int expected)
			{
				string hexa = bytes.AsSlice().ToHexString(' ');
				Assert.That(bytes.AsSpan().ToInt32BE(), Is.EqualTo(expected), $"Invalid decoding for {hexa}");
				Assert.That((new ReadOnlySpan<byte>(bytes)).ToInt32BE(), Is.EqualTo(expected), $"Invalid decoding for {hexa}");
			}

			Verify([ 0x12 ], 0x12);
			Verify([ 0x12, 0x34 ], 0x1234);
			Verify([ 0x12, 0x34, 0x56 ], 0x123456);
			Verify([ 0x00, 0x12, 0x34, 0x56 ], 0x123456);
			Verify([ 0x12, 0x34, 0x56, 0x78 ], 0x12345678);

			Verify([ ], 0);
			Verify([ 0 ], 0);
			Verify([ 255 ], 255);
			Verify([ 1, 0 ], 256);
			Verify([ 255, 255 ], 65535);
			Verify([ 1, 0, 0 ], 1 << 16);
			Verify([ 0, 1, 0, 0 ], 1 << 16);
			Verify([ 255, 255, 255 ], (1 << 24) - 1);
			Verify([ 1, 0, 0, 0 ], 1 << 24);
			Verify([ 127, 255, 255, 255 ], int.MaxValue);

			Assert.That(() => new byte[5].AsSpan().ToInt32BE(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#region 64-bits

		[Test]
		public void Test_Span_ToInt64()
		{
			void Verify(byte[] bytes, long expected)
			{
				string hexa = bytes.AsSlice().ToHexString(' ');
				Assert.That(bytes.AsSpan().ToInt64(), Is.EqualTo(expected), $"Invalid decoding for {hexa}");
				Assert.That(new ReadOnlySpan<byte>(bytes).ToInt64(), Is.EqualTo(expected), $"Invalid decoding for {hexa}");
			}

			Verify([ 0x12 ], 0x12);
			Verify([ 0x34, 0x12 ], 0x1234);
			Verify([ 0x56, 0x34, 0x12 ], 0x123456);
			Verify([ 0x56, 0x34, 0x12, 0x00 ], 0x123456);
			Verify([ 0x78, 0x56, 0x34, 0x12 ], 0x12345678);
			Verify([ 0x9A, 0x78, 0x56, 0x34, 0x12 ], 0x123456789A);
			Verify([ 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 ], 0x123456789ABC);
			Verify([ 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 ], 0x123456789ABCDE);
			Verify([ 0xF0, 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 ], 0x123456789ABCDEF0);

			Verify([ ], 0L);
			Verify([ 0 ], 0L);
			Verify([ 255 ], 255L);
			Verify([ 0, 1 ], 256L);
			Verify([ 255, 255 ], 65535L);
			Verify([ 0, 0, 1 ], 1L << 16);
			Verify([ 0, 0, 1, 0 ], 1L << 16);
			Verify([ 255, 255, 255 ], (1L << 24) - 1);
			Verify([ 0, 0, 0, 1 ], 1L << 24);
			Verify([ 0, 0, 0, 0, 1 ], 1L << 32);
			Verify([ 0, 0, 0, 0, 0, 1 ], 1L << 40);
			Verify([ 0, 0, 0, 0, 0, 0, 1 ], 1L << 48);
			Verify([ 0, 0, 0, 0, 0, 0, 0, 1 ], 1L << 56);
			Verify([ 255, 255, 255, 127 ], int.MaxValue);
			Verify([ 255, 255, 255, 255, 255, 255, 255, 127 ], long.MaxValue);
			Verify([ 255, 255, 255, 255, 255, 255, 255, 255 ], -1L);
		}

		[Test]
		public void Test_Span_ToInt64BE()
		{
			void Verify(byte[] bytes, long expected)
			{
				string hexa = bytes.AsSlice().ToHexString(' ');
				Assert.That(bytes.AsSpan().ToInt64BE(), Is.EqualTo(expected), $"Invalid decoding for {hexa}");
				Assert.That(new ReadOnlySpan<byte>(bytes).ToInt64BE(), Is.EqualTo(expected), $"Invalid decoding for {hexa}");
			}

			Verify([ 0x12 ], 0x12);
			Verify([ 0x12, 0x34 ], 0x1234);
			Verify([ 0x12, 0x34, 0x56 ], 0x123456);
			Verify([ 0x00, 0x12, 0x34, 0x56 ], 0x123456);
			Verify([ 0x12, 0x34, 0x56, 0x78 ], 0x12345678);
			Verify([ 0x12, 0x34, 0x56, 0x78, 0x9A ], 0x123456789A);
			Verify([ 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC ], 0x123456789ABC);
			Verify([ 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE ], 0x123456789ABCDE);
			Verify([ 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 ], 0x123456789ABCDEF0);

			Verify([ ], 0L);
			Verify([ 0 ], 0L);
			Verify([ 255 ], 255L);
			Verify([ 1, 0 ], 256L);
			Verify([ 255, 255 ], 65535L);
			Verify([ 1, 0, 0 ], 1L << 16);
			Verify([ 0, 1, 0, 0 ], 1L << 16);
			Verify([ 255, 255, 255 ], (1L << 24) - 1);
			Verify([ 1, 0, 0, 0 ], 1L << 24);
			Verify([ 1, 0, 0, 0, 0 ], 1L << 32);
			Verify([ 1, 0, 0, 0, 0, 0 ], 1L << 40);
			Verify([ 1, 0, 0, 0, 0, 0, 0 ], 1L << 48);
			Verify([ 1, 0, 0, 0, 0, 0, 0, 0 ], 1L << 56);
			Verify([ 127, 255, 255, 255 ], int.MaxValue);
			Verify([ 127, 255, 255, 255, 255, 255, 255, 255 ], long.MaxValue);
			Verify([ 255, 255, 255, 255, 255, 255, 255, 255 ], -1L);
		}

		#endregion

		#endregion

		#region Unsigned...

		#region 32-bits

		[Test]
		public void Test_Span_ToUInt32()
		{
			void Verify(byte[] bytes, uint expected)
			{
				string hexa = bytes.AsSlice().ToHexString(' ');
				Assert.That(bytes.AsSpan().ToUInt32(), Is.EqualTo(expected), $"Invalid decoding for {hexa}");
				Assert.That(new ReadOnlySpan<byte>(bytes).ToUInt32(), Is.EqualTo(expected), $"Invalid decoding for {hexa}");
			}

			Verify([ 0x12 ], 0x12U);
			Verify([ 0x34, 0x12 ], 0x1234U);
			Verify([ 0x56, 0x34, 0x12 ], 0x123456U);
			Verify([ 0x56, 0x34, 0x12, 0x00 ], 0x123456U);
			Verify([ 0x78, 0x56, 0x34, 0x12 ], 0x12345678U);

			Verify([ ], 0U);
			Verify([ 0 ], 0U);
			Verify([ 255 ], 255U);
			Verify([ 0, 1 ], 256U);
			Verify([ 255, 255 ], 65535U);
			Verify([ 0, 0, 1 ], 1U << 16);
			Verify([ 0, 0, 1, 0 ], 1U << 16);
			Verify([ 255, 255, 255 ], (1U << 24) - 1U);
			Verify([ 0, 0, 0, 1 ], 1U << 24);
			Verify([ 255, 255, 255, 127 ], int.MaxValue);
			Verify([ 255, 255, 255, 255 ], uint.MaxValue);

			Assert.That(() => new byte[5].AsSpan().ToUInt32(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_Span_ToUInt32BE()
		{
			void Verify(byte[] bytes, uint expected)
			{
				string hexa = bytes.AsSlice().ToHexString(' ');
				Assert.That(bytes.AsSpan().ToUInt32BE(), Is.EqualTo(expected), $"Invalid decoding for {hexa}");
				Assert.That(new ReadOnlySpan<byte>(bytes).ToUInt32BE(), Is.EqualTo(expected), $"Invalid decoding for {hexa}");
			}

			Verify([ 0x12 ], 0x12U);
			Verify([ 0x12, 0x34 ], 0x1234U);
			Verify([ 0x12, 0x34, 0x56 ], 0x123456U);
			Verify([ 0x00, 0x12, 0x34, 0x56 ], 0x123456U);
			Verify([ 0x12, 0x34, 0x56, 0x78 ], 0x12345678U);

			Verify([ ], 0U);
			Verify([ 0 ], 0U);
			Verify([ 255 ], 255U);
			Verify([ 1, 0 ], 256U);
			Verify([ 255, 255 ], 65535U);
			Verify([ 1, 0, 0 ], 1U << 16);
			Verify([ 0, 1, 0, 0 ], 1U << 16);
			Verify([ 255, 255, 255 ], (1U << 24) - 1U);
			Verify([ 1, 0, 0, 0 ], 1U << 24);
			Verify([ 127, 255, 255, 255 ], int.MaxValue);
			Verify([ 255, 255, 255, 255 ], uint.MaxValue);

			Assert.That(() => new byte[5].AsSpan().ToUInt32BE(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#region 64-bits

		[Test]
		public void Test_Span_ToUInt64()
		{
			void Verify(byte[] bytes, ulong expected)
			{
				string hexa = bytes.AsSlice().ToHexString(' ');
				Assert.That(bytes.AsSpan().ToUInt64(), Is.EqualTo(expected), $"Invalid decoding for {hexa}");
				Assert.That(new ReadOnlySpan<byte>(bytes).ToUInt64(), Is.EqualTo(expected), $"Invalid decoding for {hexa}");
			}

			Verify([ 0x12 ], 0x12);
			Verify([ 0x34, 0x12 ], 0x1234);
			Verify([ 0x56, 0x34, 0x12 ], 0x123456);
			Verify([ 0x56, 0x34, 0x12, 00 ], 0x123456);
			Verify([ 0x78, 0x56, 0x34, 0x12 ], 0x12345678);
			Verify([ 0x9A, 0x78, 0x56, 0x34, 0x12 ], 0x123456789A);
			Verify([ 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 ], 0x123456789ABC);
			Verify([ 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 ], 0x123456789ABCDE);
			Verify([ 0xF0, 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 ], 0x123456789ABCDEF0);

			Verify([ ], 0UL);
			Verify([ 0 ], 0UL);
			Verify([ 255 ], 255UL);
			Verify([ 0, 1 ], 256UL);
			Verify([ 255, 255 ], 65535UL);
			Verify([ 0, 0, 1 ], 1UL << 16);
			Verify([ 0, 0, 1, 0 ], 1UL << 16);
			Verify([ 255, 255, 255 ], (1UL << 24) - 1);
			Verify([ 0, 0, 0, 1 ], 1UL << 24);
			Verify([ 0, 0, 0, 0, 1 ], 1UL << 32);
			Verify([ 0, 0, 0, 0, 0, 1 ], 1UL << 40);
			Verify([ 0, 0, 0, 0, 0, 0, 1 ], 1UL << 48);
			Verify([ 0, 0, 0, 0, 0, 0, 0, 1 ], 1UL << 56);
			Verify([ 255, 255, 255, 127 ], int.MaxValue);
			Verify([ 255, 255, 255, 255 ], uint.MaxValue);
			Verify([ 255, 255, 255, 255, 255, 255, 255, 127 ], long.MaxValue);
			Verify([ 255, 255, 255, 255, 255, 255, 255, 255 ], ulong.MaxValue);
		}

		[Test]
		public void Test_Span_ToUInt64BE()
		{
			void Verify(byte[] bytes, ulong expected)
			{
				string hexa = bytes.AsSlice().ToHexString(' ');
				Assert.That(bytes.AsSpan().ToUInt64BE(), Is.EqualTo(expected), $"Invalid decoding for {hexa}");
				Assert.That(new ReadOnlySpan<byte>(bytes).ToUInt64BE(), Is.EqualTo(expected), $"Invalid decoding for {hexa}");
			}

			Verify([ 0x12 ], 0x12);
			Verify([ 0x12, 0x34 ], 0x1234);
			Verify([ 0x12, 0x34, 0x56 ], 0x123456);
			Verify([ 0x00, 0x12, 0x34, 0x56 ], 0x123456);
			Verify([ 0x12, 0x34, 0x56, 0x78 ], 0x12345678);
			Verify([ 0x12, 0x34, 0x56, 0x78, 0x9A ], 0x123456789A);
			Verify([ 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC ], 0x123456789ABC);
			Verify([ 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE ], 0x123456789ABCDE);
			Verify([ 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 ], 0x123456789ABCDEF0);

			Verify([ ], 0L);
			Verify([ 0 ], 0L);
			Verify([ 255 ], 255L);
			Verify([ 1, 0 ], 256L);
			Verify([ 255, 255 ], 65535L);
			Verify([ 1, 0, 0 ], 1L << 16);
			Verify([ 0, 1, 0, 0 ], 1L << 16);
			Verify([ 255, 255, 255 ], (1L << 24) - 1);
			Verify([ 1, 0, 0, 0 ], 1L << 24);
			Verify([ 1, 0, 0, 0, 0 ], 1L << 32);
			Verify([ 1, 0, 0, 0, 0, 0 ], 1L << 40);
			Verify([ 1, 0, 0, 0, 0, 0, 0 ], 1L << 48);
			Verify([ 1, 0, 0, 0, 0, 0, 0, 0 ], 1L << 56);
			Verify([ 127, 255, 255, 255 ], int.MaxValue);
			Verify([ 255, 255, 255, 255 ], uint.MaxValue);
			Verify([ 127, 255, 255, 255, 255, 255, 255, 255 ], long.MaxValue);
			Verify([ 255, 255, 255, 255, 255, 255, 255, 255 ], ulong.MaxValue);
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
		public void Test_Span_ToSingle()
		{
			void Verify(string value, float expected)
			{
				Assert.That(Slice.FromHexString(value).GetBytes().AsSpan().ToSingle(), Is.EqualTo(expected), $"Invalid decoding for '{value}' (Little Endian)");
				Assert.That(Slice.FromHexString(SwapHexa(value)).Span.ToSingleBE(), Is.EqualTo(expected), $"Invalid decoding for '{value}' (Big Endian)");
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

			Assert.That(() => new byte[5].AsSpan().ToSingle(), Throws.InstanceOf<FormatException>());
			Assert.That(() => new byte[5].AsSpan().ToSingle(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_Span_ToDouble()
		{
			void Verify(string value, double expected)
			{
				Assert.That(Slice.FromHexString(value).GetBytes().AsSpan().ToDouble(), Is.EqualTo(expected), $"Invalid decoding for '{value}' (Little Endian)");
				Assert.That(Slice.FromHexString(SwapHexa(value)).Span.ToDoubleBE(), Is.EqualTo(expected), $"Invalid decoding for '{value}' (Big Endian)");
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

			Assert.That(() => new byte[9].AsSpan().ToDouble(), Throws.InstanceOf<FormatException>());
			Assert.That(() => new byte[7].AsSpan().ToDouble(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_Span_ToDecimal()
		{
			void Verify(string value, decimal expected)
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

			Assert.That(() => new byte[15].AsSpan().ToDecimal(), Throws.InstanceOf<FormatException>());
			Assert.That(() => new byte[17].AsSpan().ToDecimal(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#region UUIDs...

		[Test]
		public void Test_Span_ToGuid()
		{
			// nil or empty should return Guid.Empty
			Assert.That(default(ReadOnlySpan<byte>).ToGuid(), Is.EqualTo(Guid.Empty));

			// all zeroes should also return Guid.Empty
			Assert.That(new byte[16].AsSpan().ToGuid(), Is.EqualTo(Guid.Empty));

			// RFC 4122 encoded UUIDs should be properly reversed when converted to System.GUID
			Assert.That(Slice.FromHexString("00112233445566778899aabbccddeeff").Span.ToGuid().ToString(), Is.EqualTo("00112233-4455-6677-8899-aabbccddeeff"), "slice.ToGuid() should convert RFC 4122 encoded UUIDs into native System.Guid");

			// round-trip
			var guid = Guid.NewGuid();
			Assert.That(Slice.FromGuid(guid).Span.ToGuid(), Is.EqualTo(guid));

			Assert.That(Slice.FromStringAscii(guid.ToString()).Span.ToGuid(), Is.EqualTo(guid), "String literals should also be converted if they match the expected format");

			Assert.That(() => Slice.FromStringAscii("random text").Span.ToGuid(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_Span_ToUuid128()
		{
			// empty should return Uuid128.Empty
			Assert.That(default(ReadOnlySpan<byte>).ToUuid128(), Is.EqualTo(Uuid128.Empty));

			// all zeroes should also return Uuid128.Empty
			Assert.That(new byte[16].AsSpan().ToUuid128(), Is.EqualTo(Uuid128.Empty));

			// RFC 4122 encoded UUIDs should not keep the byte ordering
			Assert.That(Slice.FromHexString("00112233445566778899aabbccddeeff").Span.ToUuid128().ToString(), Is.EqualTo("00112233-4455-6677-8899-aabbccddeeff"), "slice.ToUuid() should preserve RFC 4122 ordering");

			// round-trip
			var uuid = Uuid128.NewUuid();
			Assert.That(Slice.FromUuid128(uuid).Span.ToUuid128(), Is.EqualTo(uuid));

			Assert.That(Slice.FromStringAscii(uuid.ToString()).Span.ToUuid128(), Is.EqualTo(uuid), "String literals should also be converted if they match the expected format");

			Assert.That(() => Slice.FromStringAscii("random text").Span.ToUuid128(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_Span_ToUuid96()
		{
			// empty should return Uuid96.Empty
			Assert.That(default(ReadOnlySpan<byte>).ToUuid96(), Is.EqualTo(Uuid96.Empty));

			// all zeroes should also return Uuid96.Empty
			Assert.That(new byte[12].AsSpan().ToUuid96(), Is.EqualTo(Uuid96.Empty));

			// hexadecimal text representation
			(uint x, ulong y) = Slice.FromHexString("0123456789abcdef4255AA69").Span.ToUuid96();
			Assert.That(x, Is.EqualTo(0x01234567), "slice.ToUuid96() should preserve ordering (highest 16 bits)");
			Assert.That(y, Is.EqualTo(0x89abcdef4255AA69), "slice.ToUuid96() should preserve ordering (lowest 64 bits)");

			// round-trip
			var uuid = Uuid96.NewUuid();
			Assert.That(Slice.FromUuid96(uuid).Span.ToUuid96(), Is.EqualTo(uuid));

			Assert.That(Slice.FromStringAscii(uuid.ToString()).Span.ToUuid96(), Is.EqualTo(uuid), "String literals should also be converted if they match the expected format");

			Assert.That(() => Slice.FromStringAscii("random text").Span.ToUuid96(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_Span_ToUuid80()
		{
			// empty should return Uuid80.Empty
			Assert.That(default(ReadOnlySpan<byte>).ToUuid80(), Is.EqualTo(Uuid80.Empty));

			// all zeroes should also return Uuid80.Empty
			Assert.That(new byte[10].AsSpan().ToUuid80(), Is.EqualTo(Uuid80.Empty));

			// hexadecimal text representation
			(ushort x, ulong y) = Slice.FromHexString("0123456789abcdef4255").Span.ToUuid80();
			Assert.That(x, Is.EqualTo(0x0123), "slice.ToUuid80() should preserve ordering (highest 16 bits)");
			Assert.That(y, Is.EqualTo(0x456789abcdef4255), "slice.ToUuid80() should preserve ordering (lowest 64 bits)");

			// round-trip
			var uuid = Uuid80.NewUuid();
			Assert.That(Slice.FromUuid80(uuid).Span.ToUuid80(), Is.EqualTo(uuid));

			Assert.That(Slice.FromStringAscii(uuid.ToString()).Span.ToUuid80(), Is.EqualTo(uuid), "String literals should also be converted if they match the expected format");

			Assert.That(() => Slice.FromStringAscii("random text").Span.ToUuid80(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_Span_ToUuid64()
		{
			// nil or empty should return Uuid64.Empty
			Assert.That(default(ReadOnlySpan<byte>).ToUuid64(), Is.EqualTo(Uuid64.Empty));

			// all zeroes should also return Uuid64.Empty
			Assert.That(new byte[8].AsSpan().ToUuid64(), Is.EqualTo(Uuid64.Empty));

			// hexadecimal text representation
			Assert.That(Slice.FromHexString("0123456789abcdef").Span.ToUuid64().ToInt64(), Is.EqualTo(0x123456789abcdef), "slice.ToUuid64() should preserve ordering");

			// round-trip
			var uuid = Uuid64.NewUuid();
			Assert.That(Slice.FromUuid64(uuid).Span.ToUuid64(), Is.EqualTo(uuid));

			Assert.That(Slice.FromStringAscii(uuid.ToString()).Span.ToUuid64(), Is.EqualTo(uuid), "String literals should also be converted if they match the expected format");

			Assert.That(() => Slice.FromStringAscii("random text").ToUuid64(), Throws.InstanceOf<FormatException>());
		}

		#endregion

		#region TryAppendAndAdvance...

		[Test]
		public void Test_TryAppendAndAdvance()
		{
			var buffer = new char[16];
			buffer.AsSpan().Fill('∅');
			Span<char> source = buffer.AsSpan(0, 10);
			source.Fill('_');

			var cursor = source;
			Assume.That(cursor.Length, Is.EqualTo(source.Length));

			Assert.That(cursor.TryAppendAndAdvance('A'), Is.True);
			Assert.That(new string(buffer), Is.EqualTo("A_________∅∅∅∅∅∅"));
			Assert.That(cursor.Length, Is.EqualTo(source.Length - 1));
			Assert.That(cursor[0], Is.EqualTo('_'));

			Assert.That(cursor.TryAppendAndAdvance('B'), Is.True);
			Assert.That(new string(buffer), Is.EqualTo("AB________∅∅∅∅∅∅"));
			Assert.That(source[1], Is.EqualTo('B'));
			Assert.That(cursor.Length, Is.EqualTo(source.Length - 2));
			Assert.That(cursor[0], Is.EqualTo('_'));

			Assert.That(cursor.TryAppendAndAdvance("CDEFGHIJ"), Is.True);
			Assert.That(new string(buffer), Is.EqualTo("ABCDEFGHIJ∅∅∅∅∅∅"));
			Assert.That(cursor.Length, Is.Zero);

			Assert.That(cursor.TryAppendAndAdvance('K'), Is.False);
			Assert.That(new string(buffer), Is.EqualTo("ABCDEFGHIJ∅∅∅∅∅∅"));
			Assert.That(cursor.Length, Is.Zero);

			Assert.That(cursor.TryAppendAndAdvance("KLM"), Is.False);
			Assert.That(new string(buffer), Is.EqualTo("ABCDEFGHIJ∅∅∅∅∅∅"));
			Assert.That(cursor.Length, Is.Zero);

			Assert.That(cursor.TryAppendAndAdvance(""), Is.True);
			Assert.That(new string(buffer), Is.EqualTo("ABCDEFGHIJ∅∅∅∅∅∅"));
			Assert.That(cursor.Length, Is.Zero);
		}

		[Test]
		public void Test_TryCopyTo()
		{
			var buffer = new char[16];
			buffer.AsSpan().Fill('∅');
			var source = buffer.AsSpan(0, 10);

			int written;

			{ // empty
				source.Fill('_');
				Assert.That("".TryCopyTo(source, out written), Is.True.WithOutput(written).EqualTo(0));
				Assert.That(new string(buffer), Is.EqualTo("__________∅∅∅∅∅∅"));
			}
			{ // single
				source.Fill('_');
				Assert.That("H".TryCopyTo(source, out written), Is.True.WithOutput(written).EqualTo(1));
				Assert.That(new string(buffer), Is.EqualTo("H_________∅∅∅∅∅∅"));
			}
			{ // partial
				source.Fill('_');
				Assert.That("Hello".TryCopyTo(source, out written), Is.True.WithOutput(written).EqualTo(5));
				Assert.That(new string(buffer), Is.EqualTo("Hello_____∅∅∅∅∅∅"));
			}
			{ // exact fit
				source.Fill('_');
				Assert.That("HelloWorld".TryCopyTo(source, out written), Is.True.WithOutput(written).EqualTo(10));
				Assert.That(new string(buffer), Is.EqualTo("HelloWorld∅∅∅∅∅∅"));
			}
			{ // destination too small
				source.Fill('_');
				Assert.That("HelloWorld!".TryCopyTo(source, out written), Is.False.WithOutput(written).EqualTo(0));
				Assert.That(new string(buffer), Is.EqualTo("__________∅∅∅∅∅∅"));
			}

		}

		#endregion

	}

}
