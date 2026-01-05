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

// ReSharper disable UseUtf8StringLiteral
#pragma warning disable IDE0230
#pragma warning disable CS8602 // Dereference of a possibly null reference.

namespace SnowBank.Data.Binary.Tests
{
	using System.Buffers;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	[SetInvariantCulture]
	public class SpanEncoderFacts : SimpleTest
	{

		private static void Verify<TEncoder, TValue>(TValue? value, Slice expected)
#if NET9_0_OR_GREATER
			where TValue : allows ref struct
#endif
			where TEncoder : ISpanEncoder<TValue>, new()
		{
			var buffer = new byte[expected.Count + 32];

			//Log($"{typeof(TValue).Name} [{string.Join(", ", ((IEnumerable<byte>) expected.ToArray()).Reverse().Select(x => $"0x{x:X02}"))}]");
			//return;

			// buffer large enough
			var chunk = buffer.AsSpan();
			chunk.Fill(0x55);
			buffer.AsSpan(chunk.Length).Fill(0xAA);
			Assert.That(TEncoder.TryEncode(chunk, out int bytesWritten, in value), Is.True.WithOutput(bytesWritten).EqualTo(expected.Count));
			Assert.That(chunk[..bytesWritten].ToSlice(), Is.EqualTo(expected));

			// buffer with exact size
			chunk = buffer.AsSpan(0, expected.Count);
			chunk.Fill(0x55);
			buffer.AsSpan(chunk.Length).Fill(0xAA);
			Assert.That(TEncoder.TryEncode(chunk, out bytesWritten, in value), Is.True.WithOutput(bytesWritten).EqualTo(expected.Count));
			Assert.That(chunk[..bytesWritten].ToSlice(), Is.EqualTo(expected));

			// buffer that is too small by 1 byte
			if (expected.Count > 0)
			{
				chunk = buffer.AsSpan(0, expected.Count - 1);
				chunk.Fill(0x55);
				buffer.AsSpan(chunk.Length).Fill(0xAA);
				Assert.That(TEncoder.TryEncode(chunk, out bytesWritten, in value), Is.False.WithOutput(bytesWritten).Zero);
			}

			// buffer that is about 50% the required capacity
			if (expected.Count > 2)
			{
				chunk = buffer.AsSpan(0, expected.Count / 2);
				chunk.Fill(0x55);
				buffer.AsSpan(chunk.Length).Fill(0xAA);
				Assert.That(TEncoder.TryEncode(chunk, out bytesWritten, in value), Is.False.WithOutput(bytesWritten).Zero);
			}

			// ToSlice()
			var slice = SpanEncoders.ToSlice<TEncoder, TValue>(in value);
			Assert.That(slice, Is.EqualTo(expected));
			if (TEncoder.TryGetSpan(value, out var span))
			{
				Assert.That(span.ToSlice(), Is.EqualTo(expected));
			}
			else
			{
				Assert.That(span.Length, Is.Zero);
			}

			if (TEncoder.TryGetSizeHint(value, out int size))
			{
				Assert.That(size, Is.GreaterThanOrEqualTo(expected.Count));
			}
			else
			{
				Assert.That(size, Is.Zero);
			}

			// ToSliceOwner()
			var so = SpanEncoders.ToSlice<TEncoder, TValue>(in value, ArrayPool<byte>.Shared);
			Assert.That(so.Data, Is.EqualTo(expected));
			so.Dispose();
		}

		[Test]
		public void Test_SpanEncoders_RawEncoder_Slice()
		{
			var helloWorld = Slice.FromBytes("Hello, World!"u8);
			var large = Slice.Random(this.Rnd, 1025);

			Verify<SpanEncoders.RawEncoder, Slice>(Slice.Nil, Slice.Empty);
			Verify<SpanEncoders.RawEncoder, Slice>(Slice.Empty, Slice.Empty);
			Verify<SpanEncoders.RawEncoder, Slice>(helloWorld, helloWorld);
			Verify<SpanEncoders.RawEncoder, Slice>(large, large);
		}

		[Test]
		public void Test_SpanEncoders_RawEncoder_ReadOnlyMemory()
		{
			var helloWorld = Slice.FromBytes("Hello, World!"u8);
			var large = Slice.Random(this.Rnd, 1025);

			Verify<SpanEncoders.RawEncoder, ReadOnlyMemory<byte>>(default, Slice.Empty);
			Verify<SpanEncoders.RawEncoder, ReadOnlyMemory<byte>>(helloWorld.Memory, helloWorld);
			Verify<SpanEncoders.RawEncoder, ReadOnlyMemory<byte>>(large.Memory, large);
		}

#if NET9_0_OR_GREATER

		[Test]
		public void Test_SpanEncoders_RawEncoder_ReadOnlySpan()
		{
			var helloWorld = Slice.FromBytes("Hello, World!"u8);
			var large = Slice.Random(this.Rnd, 1025);

			Verify<SpanEncoders.RawEncoder, ReadOnlySpan<byte>>(default, Slice.Empty);
			Verify<SpanEncoders.RawEncoder, ReadOnlySpan<byte>>(helloWorld.Span, helloWorld);
			Verify<SpanEncoders.RawEncoder, ReadOnlySpan<byte>>(large.Span, large);
		}

#endif

		[Test]
		public void Test_SpanEncoders_RawEncoder_ByteArray()
		{
			var helloWorld = Slice.FromBytes("Hello, World!"u8);
			var large = Slice.Random(this.Rnd, 1025);

			Verify<SpanEncoders.RawEncoder, byte[]>(null, Slice.Empty);
			Verify<SpanEncoders.RawEncoder, byte[]>([ ], Slice.Empty);
			Verify<SpanEncoders.RawEncoder, byte[]>(helloWorld.ToArray(), helloWorld);
			Verify<SpanEncoders.RawEncoder, byte[]>(large.ToArray(), large);
		}

		[Test]
		public void Test_SpanEncoders_RawEncoder_MemoryStream()
		{
			var helloWorld = Slice.FromBytes("Hello, World!"u8);
			var large = Slice.Random(this.Rnd, 1025);

			Verify<SpanEncoders.RawEncoder, MemoryStream>(null, Slice.Empty);
			Verify<SpanEncoders.RawEncoder, MemoryStream>(new MemoryStream(), Slice.Empty);
			{
				var ms = new MemoryStream();
				ms.Write(helloWorld.Span);
				Verify<SpanEncoders.RawEncoder, MemoryStream>(ms, helloWorld);
			}
			{
				var ms = new MemoryStream();
				ms.Write(large.Span);
				Verify<SpanEncoders.RawEncoder, MemoryStream>(ms, large);
			}
		}

		[Test]
		public void Test_SpanEncoders_Utf8Encoder_String()
		{
			var helloWorld = "Hello, World!";
			var unicode = "こんにちは世界";
			var large = GetRandomHexString(1025);

			Verify<SpanEncoders.Utf8Encoder, string>(null, Slice.Empty);
			Verify<SpanEncoders.Utf8Encoder, string>("", Slice.Empty);
			Verify<SpanEncoders.Utf8Encoder, string>(helloWorld, Slice.FromBytes(Encoding.UTF8.GetBytes(helloWorld)));
			Verify<SpanEncoders.Utf8Encoder, string>(unicode, Slice.FromBytes(Encoding.UTF8.GetBytes(unicode)));
			Verify<SpanEncoders.Utf8Encoder, string>(large, Slice.FromBytes(Encoding.UTF8.GetBytes(large)));
		}

		[Test]
		public void Test_SpanEncoders_Utf8Encoder_ReadOnlyMemory()
		{
			var helloWorld = "Hello, World!";
			var unicode = "こんにちは世界";
			var large = GetRandomHexString(1025);

			Verify<SpanEncoders.Utf8Encoder, ReadOnlyMemory<char>>("".AsMemory(), Slice.Empty);
			Verify<SpanEncoders.Utf8Encoder, ReadOnlyMemory<char>>(helloWorld.AsMemory(), Slice.FromBytes(Encoding.UTF8.GetBytes(helloWorld)));
			Verify<SpanEncoders.Utf8Encoder, ReadOnlyMemory<char>>(unicode.AsMemory(), Slice.FromBytes(Encoding.UTF8.GetBytes(unicode)));
			Verify<SpanEncoders.Utf8Encoder, ReadOnlyMemory<char>>(large.AsMemory(), Slice.FromBytes(Encoding.UTF8.GetBytes(large)));
		}

#if NET9_0_OR_GREATER

		[Test]
		public void Test_SpanEncoders_Utf8Encoder_ReadOnlySpan()
		{
			var helloWorld = "Hello, World!";
			var unicode = "こんにちは世界";
			var large = GetRandomHexString(1025);

			Verify<SpanEncoders.Utf8Encoder, ReadOnlySpan<char>>(default, Slice.Empty);
			Verify<SpanEncoders.Utf8Encoder, ReadOnlySpan<char>>(helloWorld.AsSpan(), Slice.FromBytes(Encoding.UTF8.GetBytes(helloWorld)));
			Verify<SpanEncoders.Utf8Encoder, ReadOnlySpan<char>>(unicode.AsSpan(), Slice.FromBytes(Encoding.UTF8.GetBytes(unicode)));
			Verify<SpanEncoders.Utf8Encoder, ReadOnlySpan<char>>(large.AsSpan(), Slice.FromBytes(Encoding.UTF8.GetBytes(large)));
		}

#endif

		[Test]
		public void Test_SpanEncoders_Utf8Encoder_StringBuilder()
		{
			var helloWorld = "Hello, World!";
			var unicode = "こんにちは世界";
			var large = GetRandomHexString(1025);

			Verify<SpanEncoders.Utf8Encoder, StringBuilder>(null, Slice.Empty);
			Verify<SpanEncoders.Utf8Encoder, StringBuilder>(new(), Slice.Empty);
			{
				var sb = new StringBuilder();
				sb.Append(helloWorld);
				Verify<SpanEncoders.Utf8Encoder, StringBuilder>(sb, Slice.FromBytes(Encoding.UTF8.GetBytes(helloWorld)));
			}
			{
				var sb = new StringBuilder();
				sb.Append(unicode);
				Verify<SpanEncoders.Utf8Encoder, StringBuilder>(sb, Slice.FromBytes(Encoding.UTF8.GetBytes(unicode)));
			}
			{
				var sb = new StringBuilder();
				sb.Append(large);
				Verify<SpanEncoders.Utf8Encoder, StringBuilder>(sb, Slice.FromBytes(Encoding.UTF8.GetBytes(large)));
			}
		}

		[Test]
		public void Test_SpanEncoders_FixedSizeLittleEndianEncoder_Int32()
		{
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, int>(0, Slice.FromBytes([0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, int>(0x12, Slice.FromBytes([0x12, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, int>(0x1234, Slice.FromBytes([0x34, 0x12, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, int>(0x123456, Slice.FromBytes([0x56, 0x34, 0x12, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, int>(0x12345678, Slice.FromBytes([0x78, 0x56, 0x34, 0x12]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, int>(int.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0x7F]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, int>(-1, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, int>(int.MinValue, Slice.FromBytes([0x00, 0x00, 0x00, 0x80]));
		}

		[Test]
		public void Test_SpanEncoders_FixedSizeLittleEndianEncoder_UInt32()
		{
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, uint>(0, Slice.FromBytes([0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, uint>(0x12, Slice.FromBytes([0x12, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, uint>(0x1234, Slice.FromBytes([0x34, 0x12, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, uint>(0x123456, Slice.FromBytes([0x56, 0x34, 0x12, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, uint>(0x12345678, Slice.FromBytes([0x78, 0x56, 0x34, 0x12]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, uint>(int.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0x7F]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, uint>(uint.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF]));
		}

		[Test]
		public void Test_SpanEncoders_FixedSizeLittleEndianEncoder_Int64()
		{
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, long>(0, Slice.FromBytes([0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, long>(0x12, Slice.FromBytes([0x12, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, long>(0x1234, Slice.FromBytes([0x34, 0x12, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, long>(0x123456, Slice.FromBytes([0x56, 0x34, 0x12, 0x00, 0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, long>(0x12345678, Slice.FromBytes([0x78, 0x56, 0x34, 0x12, 0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, long>(0x123456789A, Slice.FromBytes([0x9A, 0x78, 0x56, 0x34, 0x12, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, long>(0x123456789ABC, Slice.FromBytes([0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, long>(0x123456789ABCDE, Slice.FromBytes([0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, long>(0x123456789ABCDEF0, Slice.FromBytes([0xF0, 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, long>(long.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, long>(-1, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, long>(uint.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, long>(long.MinValue, Slice.FromBytes([0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80]));
		}

		[Test]
		public void Test_SpanEncoders_FixedSizeLittleEndianEncoder_UInt64()
		{
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, ulong>(0, Slice.FromBytes([0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, ulong>(0x12, Slice.FromBytes([0x12, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, ulong>(0x1234, Slice.FromBytes([0x34, 0x12, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, ulong>(0x123456, Slice.FromBytes([0x56, 0x34, 0x12, 0x00, 0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, ulong>(0x12345678, Slice.FromBytes([0x78, 0x56, 0x34, 0x12, 0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, ulong>(0x123456789A, Slice.FromBytes([0x9A, 0x78, 0x56, 0x34, 0x12, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, ulong>(0x123456789ABC, Slice.FromBytes([0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, ulong>(0x123456789ABCDE, Slice.FromBytes([0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, ulong>(0x123456789ABCDEF0, Slice.FromBytes([0xF0, 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, ulong>(uint.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, ulong>(long.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, ulong>(ulong.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]));
		}

		[Test]
		public void Test_SpanEncoders_FixedSizeLittleEndianEncoder_Single()
		{
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, float>(0f, Slice.FromBytes([0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, float>(1f, Slice.FromBytes([0x00, 0x00, 0x80, 0x3F]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, float>(1.23f, Slice.FromBytes([0xA4, 0x70, 0x9D, 0x3F]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, float>(MathF.PI, Slice.FromBytes([0xDB, 0x0F, 0x49, 0x40]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, float>(float.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0x7F, 0x7F]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, float>(float.MinValue, Slice.FromBytes([0xFF, 0xFF, 0x7F, 0xFF]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, float>(float.Epsilon, Slice.FromBytes([0x01, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, float>(float.NaN, Slice.FromBytes([0x00, 0x00, 0xC0, 0xFF]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, float>(float.PositiveInfinity, Slice.FromBytes([0x00, 0x00, 0x80, 0x7F]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, float>(float.NegativeInfinity, Slice.FromBytes([0x00, 0x00, 0x80, 0xFF]));
		}

		[Test]
		public void Test_SpanEncoders_FixedSizeLittleEndianEncoder_Double()
		{
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, double>(0, Slice.FromBytes([0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, double>(1, Slice.FromBytes([0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, double>(1.23, Slice.FromBytes([0xAE, 0x47, 0xE1, 0x7A, 0x14, 0xAE, 0xF3, 0x3F]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, double>(Math.PI, Slice.FromBytes([0x18, 0x2D, 0x44, 0x54, 0xFB, 0x21, 0x09, 0x40]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, double>(double.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xEF, 0x7F]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, double>(double.MinValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xEF, 0xFF]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, double>(double.Epsilon, Slice.FromBytes([0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, double>(double.NaN, Slice.FromBytes([0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF8, 0xFF]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, double>(double.PositiveInfinity, Slice.FromBytes([0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x7F]));
			Verify<SpanEncoders.FixedSizeLittleEndianEncoder, double>(double.NegativeInfinity, Slice.FromBytes([0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0xFF]));
		}

		[Test]
		public void Test_SpanEncoders_FixedSizeBigEndianEncoder_Int32()
		{
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, int>(0, Slice.FromBytes([0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, int>(0x12, Slice.FromBytes([0x00, 0x00, 0x00, 0x12]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, int>(0x1234, Slice.FromBytes([0x00, 0x00, 0x12, 0x34]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, int>(0x123456, Slice.FromBytes([0x00, 0x12, 0x34, 0x56]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, int>(0x12345678, Slice.FromBytes([0x12, 0x34, 0x56, 0x78]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, int>(int.MaxValue, Slice.FromBytes([0x7F, 0xFF, 0xFF, 0xFF]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, int>(-1, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, int>(int.MinValue, Slice.FromBytes([0x80, 0x00, 0x00, 0x00]));
		}

		[Test]
		public void Test_SpanEncoders_FixedSizeBigEndianEncoder_UInt32()
		{
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, uint>(0, Slice.FromBytes([0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, uint>(0x12, Slice.FromBytes([0x00, 0x00, 0x00, 0x12]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, uint>(0x1234, Slice.FromBytes([0x00, 0x00, 0x12, 0x34]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, uint>(0x123456, Slice.FromBytes([0x00, 0x12, 0x34, 0x56]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, uint>(0x12345678, Slice.FromBytes([0x12, 0x34, 0x56, 0x78]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, uint>(uint.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF]));
		}

		[Test]
		public void Test_SpanEncoders_FixedSizeBigEndianEncoder_Int64()
		{
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, long>(0, Slice.FromBytes([0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, long>(0x12, Slice.FromBytes([0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x12]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, long>(0x1234, Slice.FromBytes([0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x12, 0x34]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, long>(0x123456, Slice.FromBytes([0x00, 0x00, 0x00, 0x00, 0x00, 0x12, 0x34, 0x56]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, long>(0x12345678, Slice.FromBytes([0x00, 0x00, 0x00, 0x00, 0x12, 0x34, 0x56, 0x78]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, long>(0x123456789A, Slice.FromBytes([0x00, 0x00, 0x00, 0x12, 0x34, 0x56, 0x78, 0x9A]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, long>(0x123456789ABC, Slice.FromBytes([0x00, 0x00, 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, long>(0x123456789ABCDE, Slice.FromBytes([0x00, 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, long>(0x123456789ABCDEF0, Slice.FromBytes([0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, long>(long.MaxValue, Slice.FromBytes([0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, long>(-1, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, long>(uint.MaxValue, Slice.FromBytes([0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, long>(long.MinValue, Slice.FromBytes([0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]));
		}

		[Test]
		public void Test_SpanEncoders_FixedSizeBigEndianEncoder_UInt64()
		{
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, ulong>(0, Slice.FromBytes([0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, ulong>(0x12, Slice.FromBytes([0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x12]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, ulong>(0x1234, Slice.FromBytes([0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x12, 0x34]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, ulong>(0x123456, Slice.FromBytes([0x00, 0x00, 0x00, 0x00, 0x00, 0x12, 0x34, 0x56]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, ulong>(0x12345678, Slice.FromBytes([0x00, 0x00, 0x00, 0x00, 0x12, 0x34, 0x56, 0x78]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, ulong>(0x123456789A, Slice.FromBytes([0x00, 0x00, 0x00, 0x12, 0x34, 0x56, 0x78, 0x9A]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, ulong>(0x123456789ABC, Slice.FromBytes([0x00, 0x00, 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, ulong>(0x123456789ABCDE, Slice.FromBytes([0x00, 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, ulong>(0x123456789ABCDEF0, Slice.FromBytes([0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, ulong>(uint.MaxValue, Slice.FromBytes([0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, ulong>(ulong.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]));
		}

		[Test]
		public void Test_SpanEncoders_FixedSizeBigEndianEncoder_Single()
		{
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, float>(0f, Slice.FromBytes([0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, float>(1f, Slice.FromBytes([0x3F, 0x80, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, float>(1.23f, Slice.FromBytes([0x3F, 0x9D, 0x70, 0xA4]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, float>(MathF.PI, Slice.FromBytes([0x40, 0x49, 0x0F, 0xDB]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, float>(float.MaxValue, Slice.FromBytes([0x7F, 0x7F, 0xFF, 0xFF]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, float>(float.MinValue, Slice.FromBytes([0xFF, 0x7F, 0xFF, 0xFF]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, float>(float.Epsilon, Slice.FromBytes([0x00, 0x00, 0x00, 0x01]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, float>(float.NaN, Slice.FromBytes([0xFF, 0xC0, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, float>(float.PositiveInfinity, Slice.FromBytes([0x7F, 0x80, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, float>(float.NegativeInfinity, Slice.FromBytes([0xFF, 0x80, 0x00, 0x00]));
		}

		[Test]
		public void Test_SpanEncoders_FixedSizeBigEndianEncoder_Double()
		{
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, double>(0, Slice.FromBytes([0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, double>(1, Slice.FromBytes([0x3F, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, double>(1.23, Slice.FromBytes([0x3F, 0xF3, 0xAE, 0x14, 0x7A, 0xE1, 0x47, 0xAE]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, double>(Math.PI, Slice.FromBytes([0x40, 0x09, 0x21, 0xFB, 0x54, 0x44, 0x2D, 0x18]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, double>(double.MaxValue, Slice.FromBytes([0x7F, 0xEF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, double>(double.MinValue, Slice.FromBytes([0xFF, 0xEF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, double>(double.Epsilon, Slice.FromBytes([0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, double>(double.NaN, Slice.FromBytes([0xFF, 0xF8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, double>(double.PositiveInfinity, Slice.FromBytes([0x7F, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]));
			Verify<SpanEncoders.FixedSizeBigEndianEncoder, double>(double.NegativeInfinity, Slice.FromBytes([0xFF, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]));
		}

		[Test]
		public void Test_SpanEncoders_CompactLittleEndianEncoder_Int16()
		{
			Verify<SpanEncoders.CompactLittleEndianEncoder, short>(0, Slice.FromBytes([0x00]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, short>(0x12, Slice.FromBytes([0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, short>(0x1234, Slice.FromBytes([0x34, 0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, short>(short.MaxValue, Slice.FromBytes([0xFF, 0x7F]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, short>(-1, Slice.FromBytes([0xFF, 0xFF]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, short>(short.MinValue, Slice.FromBytes([0x00, 0x80]));
		}

		[Test]
		public void Test_SpanEncoders_CompactLittleEndianEncoder_UInt16()
		{
			Verify<SpanEncoders.CompactLittleEndianEncoder, ushort>(0, Slice.FromBytes([0x00]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, ushort>(0x12, Slice.FromBytes([0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, ushort>(0x1234, Slice.FromBytes([0x34, 0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, ushort>(0x7FFF, Slice.FromBytes([0xFF, 0x7F]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, ushort>(ushort.MaxValue, Slice.FromBytes([0xFF, 0xFF]));
		}

		[Test]
		public void Test_SpanEncoders_CompactLittleEndianEncoder_Int32()
		{
			Verify<SpanEncoders.CompactLittleEndianEncoder, int>(0, Slice.FromBytes([0x00]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, int>(0x12, Slice.FromBytes([0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, int>(0x1234, Slice.FromBytes([0x34, 0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, int>(0x123456, Slice.FromBytes([0x56, 0x34, 0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, int>(0x12345678, Slice.FromBytes([0x78, 0x56, 0x34, 0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, int>(int.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0x7F]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, int>(-1, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, int>(int.MinValue, Slice.FromBytes([0x00, 0x00, 0x00, 0x80]));
		}

		[Test]
		public void Test_SpanEncoders_CompactLittleEndianEncoder_UInt32()
		{
			Verify<SpanEncoders.CompactLittleEndianEncoder, uint>(0, Slice.FromBytes([0x00]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, uint>(0x12, Slice.FromBytes([0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, uint>(0x1234, Slice.FromBytes([0x34, 0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, uint>(0x123456, Slice.FromBytes([0x56, 0x34, 0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, uint>(0x12345678, Slice.FromBytes([0x78, 0x56, 0x34, 0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, uint>(int.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0x7F]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, uint>(uint.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF]));
		}

		[Test]
		public void Test_SpanEncoders_CompactLittleEndianEncoder_Int64()
		{
			Verify<SpanEncoders.CompactLittleEndianEncoder, long>(0, Slice.FromBytes([0x00]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, long>(0x12, Slice.FromBytes([0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, long>(0x1234, Slice.FromBytes([0x34, 0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, long>(0x123456, Slice.FromBytes([0x56, 0x34, 0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, long>(0x12345678, Slice.FromBytes([0x78, 0x56, 0x34, 0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, long>(0x123456789A, Slice.FromBytes([0x9A, 0x78, 0x56, 0x34, 0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, long>(0x123456789ABC, Slice.FromBytes([0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, long>(0x123456789ABCDE, Slice.FromBytes([0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, long>(0x123456789ABCDEF0, Slice.FromBytes([0xF0, 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, long>(long.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, long>(-1, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, long>(uint.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, long>(long.MinValue, Slice.FromBytes([0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80]));
		}

		[Test]
		public void Test_SpanEncoders_CompactLittleEndianEncoder_UInt64()
		{
			Verify<SpanEncoders.CompactLittleEndianEncoder, ulong>(0, Slice.FromBytes([0x00]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, ulong>(0x12, Slice.FromBytes([0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, ulong>(0x1234, Slice.FromBytes([0x34, 0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, ulong>(0x123456, Slice.FromBytes([0x56, 0x34, 0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, ulong>(0x12345678, Slice.FromBytes([0x78, 0x56, 0x34, 0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, ulong>(0x123456789A, Slice.FromBytes([0x9A, 0x78, 0x56, 0x34, 0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, ulong>(0x123456789ABC, Slice.FromBytes([0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, ulong>(0x123456789ABCDE, Slice.FromBytes([0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, ulong>(0x123456789ABCDEF0, Slice.FromBytes([0xF0, 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, ulong>(uint.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, ulong>(long.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F]));
			Verify<SpanEncoders.CompactLittleEndianEncoder, ulong>(ulong.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]));
		}

		[Test]
		public void Test_SpanEncoders_CompactBigEndianEncoder_Int16()
		{
			Verify<SpanEncoders.CompactBigEndianEncoder, short>(0, Slice.FromBytes([0x00]));
			Verify<SpanEncoders.CompactBigEndianEncoder, short>(0x12, Slice.FromBytes([0x12]));
			Verify<SpanEncoders.CompactBigEndianEncoder, short>(0x1234, Slice.FromBytes([0x12, 0x34]));
			Verify<SpanEncoders.CompactBigEndianEncoder, short>(short.MaxValue, Slice.FromBytes([0x7F, 0xFF]));
			Verify<SpanEncoders.CompactBigEndianEncoder, short>(-1, Slice.FromBytes([0xFF, 0xFF]));
			Verify<SpanEncoders.CompactBigEndianEncoder, short>(short.MinValue, Slice.FromBytes([0x80, 0x00]));
		}

		[Test]
		public void Test_SpanEncoders_CompactBigEndianEncoder_UInt16()
		{
			Verify<SpanEncoders.CompactBigEndianEncoder, ushort>(0, Slice.FromBytes([0x00]));
			Verify<SpanEncoders.CompactBigEndianEncoder, ushort>(0x12, Slice.FromBytes([0x12]));
			Verify<SpanEncoders.CompactBigEndianEncoder, ushort>(0x1234, Slice.FromBytes([0x12, 0x34]));
			Verify<SpanEncoders.CompactBigEndianEncoder, ushort>(0x7FFF, Slice.FromBytes([0x7F, 0xFF]));
			Verify<SpanEncoders.CompactBigEndianEncoder, ushort>(ushort.MaxValue, Slice.FromBytes([0xFF, 0xFF]));
		}

		[Test]
		public void Test_SpanEncoders_CompactBigEndianEncoder_Int32()
		{
			Verify<SpanEncoders.CompactBigEndianEncoder, int>(0, Slice.FromBytes([0x00]));
			Verify<SpanEncoders.CompactBigEndianEncoder, int>(0x12, Slice.FromBytes([0x12]));
			Verify<SpanEncoders.CompactBigEndianEncoder, int>(0x1234, Slice.FromBytes([0x12, 0x34]));
			Verify<SpanEncoders.CompactBigEndianEncoder, int>(0x123456, Slice.FromBytes([0x12, 0x34, 0x56]));
			Verify<SpanEncoders.CompactBigEndianEncoder, int>(0x12345678, Slice.FromBytes([0x12, 0x34, 0x56, 0x78]));
			Verify<SpanEncoders.CompactBigEndianEncoder, int>(int.MaxValue, Slice.FromBytes([0x7F, 0xFF, 0xFF, 0xFF]));
			Verify<SpanEncoders.CompactBigEndianEncoder, int>(-1, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF]));
			Verify<SpanEncoders.CompactBigEndianEncoder, int>(int.MinValue, Slice.FromBytes([0x80, 0x00, 0x00, 0x00]));
		}

		[Test]
		public void Test_SpanEncoders_CompactBigEndianEncoder_UInt32()
		{
			Verify<SpanEncoders.CompactBigEndianEncoder, uint>(0, Slice.FromBytes([0x00]));
			Verify<SpanEncoders.CompactBigEndianEncoder, uint>(0x12, Slice.FromBytes([0x12]));
			Verify<SpanEncoders.CompactBigEndianEncoder, uint>(0x1234, Slice.FromBytes([0x12, 0x34]));
			Verify<SpanEncoders.CompactBigEndianEncoder, uint>(0x123456, Slice.FromBytes([0x12, 0x34, 0x56]));
			Verify<SpanEncoders.CompactBigEndianEncoder, uint>(0x12345678, Slice.FromBytes([0x12, 0x34, 0x56, 0x78]));
			Verify<SpanEncoders.CompactBigEndianEncoder, uint>(uint.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF]));
		}

		[Test]
		public void Test_SpanEncoders_CompactBigEndianEncoder_Int64()
		{
			Verify<SpanEncoders.CompactBigEndianEncoder, long>(0, Slice.FromBytes([0x00]));
			Verify<SpanEncoders.CompactBigEndianEncoder, long>(0x12, Slice.FromBytes([0x12]));
			Verify<SpanEncoders.CompactBigEndianEncoder, long>(0x1234, Slice.FromBytes([0x12, 0x34]));
			Verify<SpanEncoders.CompactBigEndianEncoder, long>(0x123456, Slice.FromBytes([0x12, 0x34, 0x56]));
			Verify<SpanEncoders.CompactBigEndianEncoder, long>(0x12345678, Slice.FromBytes([0x12, 0x34, 0x56, 0x78]));
			Verify<SpanEncoders.CompactBigEndianEncoder, long>(0x123456789A, Slice.FromBytes([0x12, 0x34, 0x56, 0x78, 0x9A]));
			Verify<SpanEncoders.CompactBigEndianEncoder, long>(0x123456789ABC, Slice.FromBytes([0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC]));
			Verify<SpanEncoders.CompactBigEndianEncoder, long>(0x123456789ABCDE, Slice.FromBytes([0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE]));
			Verify<SpanEncoders.CompactBigEndianEncoder, long>(0x123456789ABCDEF0, Slice.FromBytes([0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0]));
			Verify<SpanEncoders.CompactBigEndianEncoder, long>(long.MaxValue, Slice.FromBytes([0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]));
			Verify<SpanEncoders.CompactBigEndianEncoder, long>(-1, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]));
			Verify<SpanEncoders.CompactBigEndianEncoder, long>(uint.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF]));
			Verify<SpanEncoders.CompactBigEndianEncoder, long>(long.MinValue, Slice.FromBytes([0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]));
		}

		[Test]
		public void Test_SpanEncoders_CompactBigEndianEncoder_UInt64()
		{
			Verify<SpanEncoders.CompactBigEndianEncoder, ulong>(0, Slice.FromBytes([0x00]));
			Verify<SpanEncoders.CompactBigEndianEncoder, ulong>(0x12, Slice.FromBytes([0x12]));
			Verify<SpanEncoders.CompactBigEndianEncoder, ulong>(0x1234, Slice.FromBytes([0x12, 0x34]));
			Verify<SpanEncoders.CompactBigEndianEncoder, ulong>(0x123456, Slice.FromBytes([0x12, 0x34, 0x56]));
			Verify<SpanEncoders.CompactBigEndianEncoder, ulong>(0x12345678, Slice.FromBytes([0x12, 0x34, 0x56, 0x78]));
			Verify<SpanEncoders.CompactBigEndianEncoder, ulong>(0x123456789A, Slice.FromBytes([0x12, 0x34, 0x56, 0x78, 0x9A]));
			Verify<SpanEncoders.CompactBigEndianEncoder, ulong>(0x123456789ABC, Slice.FromBytes([0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC]));
			Verify<SpanEncoders.CompactBigEndianEncoder, ulong>(0x123456789ABCDE, Slice.FromBytes([0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE]));
			Verify<SpanEncoders.CompactBigEndianEncoder, ulong>(0x123456789ABCDEF0, Slice.FromBytes([0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0]));
			Verify<SpanEncoders.CompactBigEndianEncoder, ulong>(uint.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF]));
			Verify<SpanEncoders.CompactBigEndianEncoder, ulong>(ulong.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]));
		}

		[Test]
		public void Test_SpanEncoders_VarIntEncoder_UInt16()
		{
			Verify<SpanEncoders.VarIntEncoder, ushort>(0, Slice.FromBytes([ 0x00 ]));
			Verify<SpanEncoders.VarIntEncoder, ushort>(0x12, Slice.FromBytes([ 0x12 ]));
			Verify<SpanEncoders.VarIntEncoder, ushort>(0x1234, Slice.FromBytes([ 0xB4, 0x24 ]));
			Verify<SpanEncoders.VarIntEncoder, ushort>(0x7FFF, Slice.FromBytes([ 0xFF, 0xFF, 0x01 ]));
			Verify<SpanEncoders.VarIntEncoder, ushort>(ushort.MaxValue, Slice.FromBytes([ 0xFF, 0xFF, 0x03 ]));
		}

		[Test]
		public void Test_SpanEncoders_VarIntEncoder_UInt32()
		{
			Verify<SpanEncoders.VarIntEncoder, uint>(0, Slice.FromBytes([ 0x00 ]));
			Verify<SpanEncoders.VarIntEncoder, uint>(0x12, Slice.FromBytes([ 0x12 ]));
			Verify<SpanEncoders.VarIntEncoder, uint>(0x1234, Slice.FromBytes([ 0xB4, 0x24 ]));
			Verify<SpanEncoders.VarIntEncoder, uint>(0x123456, Slice.FromBytes([ 0xD6, 0xE8, 0x48]));
			Verify<SpanEncoders.VarIntEncoder, uint>(0x12345678, Slice.FromBytes([ 0xF8, 0xAC, 0xD1, 0x91, 0x01]));
			Verify<SpanEncoders.VarIntEncoder, uint>(int.MaxValue, Slice.FromBytes([ 0xFF, 0xFF, 0xFF, 0xFF, 0x07 ]));
			Verify<SpanEncoders.VarIntEncoder, uint>(uint.MaxValue, Slice.FromBytes([ 0xFF, 0xFF, 0xFF, 0xFF, 0x0F ]));
		}

		[Test]
		public void Test_SpanEncoders_VarIntEncoder_UInt64()
		{
			Verify<SpanEncoders.VarIntEncoder, ulong>(0, Slice.FromBytes([0x00]));
			Verify<SpanEncoders.VarIntEncoder, ulong>(0x12, Slice.FromBytes([0x12]));
			Verify<SpanEncoders.VarIntEncoder, ulong>(0x1234, Slice.FromBytes([0xB4, 0x24]));
			Verify<SpanEncoders.VarIntEncoder, ulong>(0x123456, Slice.FromBytes([0xD6, 0xE8, 0x48]));
			Verify<SpanEncoders.VarIntEncoder, ulong>(0x12345678, Slice.FromBytes([0xF8, 0xAC, 0xD1, 0x91, 0x01]));
			Verify<SpanEncoders.VarIntEncoder, ulong>(0x123456789A, Slice.FromBytes([0x9A, 0xF1, 0xD9, 0xA2, 0xA3, 0x02]));
			Verify<SpanEncoders.VarIntEncoder, ulong>(0x123456789ABC, Slice.FromBytes([0xBC, 0xB5, 0xE2, 0xB3, 0xC5, 0xC6, 0x04]));
			Verify<SpanEncoders.VarIntEncoder, ulong>(0x123456789ABCDE, Slice.FromBytes([0xDE, 0xF9, 0xEA, 0xC4, 0xE7, 0x8A, 0x8D, 0x09]));
			Verify<SpanEncoders.VarIntEncoder, ulong>(0x123456789ABCDEF0, Slice.FromBytes([0xF0, 0xBD, 0xF3, 0xD5, 0x89, 0xCF, 0x95, 0x9A, 0x12]));
			Verify<SpanEncoders.VarIntEncoder, ulong>(int.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0x07]));
			Verify<SpanEncoders.VarIntEncoder, ulong>(uint.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0x0F]));
			Verify<SpanEncoders.VarIntEncoder, ulong>(long.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F]));
			Verify<SpanEncoders.VarIntEncoder, ulong>(ulong.MaxValue, Slice.FromBytes([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01]));
		}

		[Test]
		public void Test_SpanEncoders_UuidEncoder_Guid()
		{
			Verify<SpanEncoders.FixedSizeUuidEncoder, Guid>(Guid.Empty, Slice.FromBytes([ 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 ]));
			Verify<SpanEncoders.FixedSizeUuidEncoder, Guid>(Guid.Parse("d1f21a51-6502-4a06-bcd4-bb59fc01c56d"), Slice.FromBytes([ 0xD1, 0xF2, 0x1A, 0x51, 0x65, 0x02, 0x4A, 0x06, 0xBC, 0xD4, 0xBB, 0x59, 0xFC, 0x01, 0xC5, 0x6D ]));
			Verify<SpanEncoders.FixedSizeUuidEncoder, Guid>(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), Slice.FromBytes([ 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF ]));
		}

		[Test]
		public void Test_SpanEncoders_UuidEncoder_Uuid128()
		{
			Verify<SpanEncoders.FixedSizeUuidEncoder, Uuid128>(Uuid128.Empty, Slice.FromBytes([ 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 ]));
			Verify<SpanEncoders.FixedSizeUuidEncoder, Uuid128>(Uuid128.Parse("d1f21a51-6502-4a06-bcd4-bb59fc01c56d"), Slice.FromBytes([ 0xD1, 0xF2, 0x1A, 0x51, 0x65, 0x02, 0x4A, 0x06, 0xBC, 0xD4, 0xBB, 0x59, 0xFC, 0x01, 0xC5, 0x6D ]));
			Verify<SpanEncoders.FixedSizeUuidEncoder, Uuid128>(Uuid128.AllBitsSet, Slice.FromBytes([ 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF ]));
		}

		[Test]
		public void Test_SpanEncoders_UuidEncoder_Uuid96()
		{
			Verify<SpanEncoders.FixedSizeUuidEncoder, Uuid96>(Uuid96.Empty, Slice.FromBytes([ 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 ]));
			Verify<SpanEncoders.FixedSizeUuidEncoder, Uuid96>(Uuid96.Parse("d1f21a51-65024a06-bcd4bb59"), Slice.FromBytes([ 0xD1, 0xF2, 0x1A, 0x51, 0x65, 0x02, 0x4A, 0x06, 0xBC, 0xD4, 0xBB, 0x59 ]));
			Verify<SpanEncoders.FixedSizeUuidEncoder, Uuid96>(Uuid96.AllBitsSet, Slice.FromBytes([ 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF ]));
		}

		[Test]
		public void Test_SpanEncoders_UuidEncoder_Uuid80()
		{
			Verify<SpanEncoders.FixedSizeUuidEncoder, Uuid80>(Uuid80.Empty, Slice.FromBytes([ 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 ]));
			Verify<SpanEncoders.FixedSizeUuidEncoder, Uuid80>(Uuid80.Parse("d1f2-1a516502-4a06bcd4"), Slice.FromBytes([ 0xD1, 0xF2, 0x1A, 0x51, 0x65, 0x02, 0x4A, 0x06, 0xBC, 0xD4 ]));
			Verify<SpanEncoders.FixedSizeUuidEncoder, Uuid80>(Uuid80.AllBitsSet, Slice.FromBytes([ 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF ]));
		}

		[Test]
		public void Test_SpanEncoders_UuidEncoder_Uuid64()
		{
			Verify<SpanEncoders.FixedSizeUuidEncoder, Uuid64>(Uuid64.Empty, Slice.FromBytes([ 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 ]));
			Verify<SpanEncoders.FixedSizeUuidEncoder, Uuid64>(Uuid64.Parse("1a516502-4a06bcd4"), Slice.FromBytes([ 0x1A, 0x51, 0x65, 0x02, 0x4A, 0x06, 0xBC, 0xD4 ]));
			Verify<SpanEncoders.FixedSizeUuidEncoder, Uuid64>(Uuid64.AllBitsSet, Slice.FromBytes([ 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF ]));
		}

		[Test]
		public void Test_SpanEncoders_UuidEncoder_Uuid48()
		{
			Verify<SpanEncoders.FixedSizeUuidEncoder, Uuid48>(Uuid48.Empty, Slice.FromBytes([ 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 ]));
			Verify<SpanEncoders.FixedSizeUuidEncoder, Uuid48>(Uuid48.Parse("1a51-65024a06"), Slice.FromBytes([ 0x1A, 0x51, 0x65, 0x02, 0x4A, 0x06 ]));
			Verify<SpanEncoders.FixedSizeUuidEncoder, Uuid48>(Uuid48.AllBitsSet, Slice.FromBytes([ 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF ]));
		}

		[Test]
		public void Test_SpanEncoders_UuidEncoder_VersionStamp()
		{
			Verify<SpanEncoders.FixedSizeUuidEncoder, VersionStamp>(VersionStamp.None, Slice.FromBytes([ 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 ]));

			Verify<SpanEncoders.FixedSizeUuidEncoder, VersionStamp>(VersionStamp.Incomplete(), Slice.FromBytes([ 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF ]));
			Verify<SpanEncoders.FixedSizeUuidEncoder, VersionStamp>(VersionStamp.Complete(0x0123456789ABCDEF, 0x1234), Slice.FromBytes([ 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF, 0x12, 0x34 ]));

			Verify<SpanEncoders.FixedSizeUuidEncoder, VersionStamp>(VersionStamp.Incomplete(0x1234), Slice.FromBytes([ 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x12, 0x34 ]));
			Verify<SpanEncoders.FixedSizeUuidEncoder, VersionStamp>(VersionStamp.Complete(0x0123456789ABCDEF, 0x1234, 0x5678), Slice.FromBytes([ 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF, 0x12, 0x34, 0x56, 0x78 ]));
		}

	}

}
