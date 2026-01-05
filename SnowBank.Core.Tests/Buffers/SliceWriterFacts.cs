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

namespace SnowBank.Buffers.Tests
{
	using SnowBank.Data.Binary;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class SliceWriterFacts : SimpleTest
	{

		private static string Clean(string value)
		{
			var sb = new StringBuilder(value.Length + 8);
			foreach (var c in value)
			{
				if (c < ' ') sb.Append("\\x").Append(((int)c).ToString("x2")); else sb.Append(c);
			}
			return sb.ToString();
		}

		private delegate void TestHandler<in T>(ref SliceWriter writer, T value);

		private static void PerformWriterTest<T>(TestHandler<T?> action, T? value, string expectedResult, string? message = null)
		{
			var writer = default(SliceWriter);
			action(ref writer, value);

			Assert.That(
				writer.ToSlice().ToHexString(' '),
				Is.EqualTo(expectedResult),
				$"Value {(value == null ? "<null>" : value is string s ? Clean(s) : value.ToString())} ({(value == null ? "null" : value.GetType().Name)}) was not properly packed. {message}"
			);
		}

		[Test]
		public void Test_Empty_Writer()
		{
			var writer = default(SliceWriter);
			Assert.That(writer.Position, Is.EqualTo(0));
			Assert.That(writer.HasData, Is.False);
			Assert.That(writer.Buffer, Is.Null);
			Assert.That(writer.ToSlice(), Is.EqualTo(Slice.Empty));
		}

		[Test]
		public void Test_WriteBytes()
		{
			{
				static void Test(ref SliceWriter writer, byte[]? value) => writer.WriteBytes(value);

				PerformWriterTest(Test, default(byte[]), "");
				PerformWriterTest(Test, Array.Empty<byte>(), "");
				PerformWriterTest(Test, "B"u8.ToArray(), "42");
				PerformWriterTest(Test, "ABC"u8.ToArray(), "41 42 43");
			}
			{
				static void Test(ref SliceWriter writer, Slice value) => writer.WriteBytes(value);

				PerformWriterTest(Test, Slice.Nil, "");
				PerformWriterTest(Test, Slice.Empty, "");
				PerformWriterTest(Test, Slice.FromByte(66), "42");
				PerformWriterTest(Test, "ABC"u8.ToArray().AsSlice(), "41 42 43");
				PerformWriterTest(Test, "ABCDE"u8.ToArray().AsSlice(1, 3), "42 43 44");
			}
		}

		[Test]
		public void Test_WriteByte_Unsigned()
		{
			static void Test(ref SliceWriter writer, byte value) => writer.WriteByte(value);

			PerformWriterTest<byte>(Test, 0, "00");
			PerformWriterTest<byte>(Test, 1, "01");
			PerformWriterTest<byte>(Test, 42, "2A");
			PerformWriterTest<byte>(Test, 255, "FF");
		}

		[Test]
		public void Test_WriteByte_Signed()
		{
			static void Test(ref SliceWriter writer, sbyte value) => writer.WriteByte(value);

			PerformWriterTest<sbyte>(Test, 0, "00");
			PerformWriterTest<sbyte>(Test, 1, "01");
			PerformWriterTest<sbyte>(Test, 42, "2A");
			PerformWriterTest<sbyte>(Test, sbyte.MaxValue, "7F");
			PerformWriterTest<sbyte>(Test, -1, "FF");
			PerformWriterTest<sbyte>(Test, sbyte.MinValue, "80");
		}

		[Test]
		public void Test_WriteFixed16_Unsigned()
		{
			static void Test(ref SliceWriter writer, ushort value) => writer.WriteUInt16(value);

			PerformWriterTest<ushort>(Test, 0, "00 00");
			PerformWriterTest<ushort>(Test, 1, "01 00");
			PerformWriterTest<ushort>(Test, 0x12, "12 00");
			PerformWriterTest<ushort>(Test, 0x1234, "34 12");
			PerformWriterTest<ushort>(Test, ushort.MaxValue, "FF FF");
		}

		[Test]
		public void Test_WriteFixed16_Signed()
		{
			static void Test(ref SliceWriter writer, short value) => writer.WriteInt16(value);

			PerformWriterTest<short>(Test, 0, "00 00");
			PerformWriterTest<short>(Test, 1, "01 00");
			PerformWriterTest<short>(Test, 0x12, "12 00");
			PerformWriterTest<short>(Test, 0x1234, "34 12");
			PerformWriterTest<short>(Test, short.MaxValue, "FF 7F");
			PerformWriterTest<short>(Test, -1, "FF FF");
			PerformWriterTest<short>(Test, short.MinValue, "00 80");
		}

		[Test]
		public void Test_WriteFixed16BE_Unsigned()
		{
			static void Test(ref SliceWriter writer, ushort value) => writer.WriteUInt16BE(value);

			PerformWriterTest<ushort>(Test, 0, "00 00");
			PerformWriterTest<ushort>(Test, 1, "00 01");
			PerformWriterTest<ushort>(Test, 0x12, "00 12");
			PerformWriterTest<ushort>(Test, 0x1234, "12 34");
			PerformWriterTest<ushort>(Test, ushort.MaxValue, "FF FF");
		}

		[Test]
		public void Test_WriteFixed16BE_Signed()
		{
			static void Test(ref SliceWriter writer, short value) => writer.WriteInt16BE(value);

			PerformWriterTest<short>(Test, 0, "00 00");
			PerformWriterTest<short>(Test, 1, "00 01");
			PerformWriterTest<short>(Test, 0x12, "00 12");
			PerformWriterTest<short>(Test, 0x1234, "12 34");
			PerformWriterTest<short>(Test, short.MaxValue, "7F FF");
			PerformWriterTest<short>(Test, -1, "FF FF");
			PerformWriterTest<short>(Test, short.MinValue, "80 00");
		}

		[Test]
		public void Test_WriteFixed32_Unsigned()
		{
			static void Test(ref SliceWriter writer, uint value) => writer.WriteUInt32(value);

			PerformWriterTest<uint>(Test, 0U, "00 00 00 00");
			PerformWriterTest<uint>(Test, 1U, "01 00 00 00");
			PerformWriterTest<uint>(Test, 0x12U, "12 00 00 00");
			PerformWriterTest<uint>(Test, 0x1234U, "34 12 00 00");
			PerformWriterTest<uint>(Test, ushort.MaxValue, "FF FF 00 00");
			PerformWriterTest<uint>(Test, 0x123456U, "56 34 12 00");
			PerformWriterTest<uint>(Test, 0xDEADBEEF, "EF BE AD DE");
			PerformWriterTest<uint>(Test, uint.MaxValue, "FF FF FF FF");
		}

		[Test]
		public void Test_WriteFixed32_Signed()
		{
			static void Test(ref SliceWriter writer, int value) => writer.WriteInt32(value);

			PerformWriterTest<int>(Test, 0, "00 00 00 00");
			PerformWriterTest<int>(Test, 1, "01 00 00 00");
			PerformWriterTest<int>(Test, 0x12, "12 00 00 00");
			PerformWriterTest<int>(Test, 0x1234, "34 12 00 00");
			PerformWriterTest<int>(Test, short.MaxValue, "FF 7F 00 00");
			PerformWriterTest<int>(Test, ushort.MaxValue, "FF FF 00 00");
			PerformWriterTest<int>(Test, 0x123456, "56 34 12 00");
			PerformWriterTest<int>(Test, unchecked((int)0xDEADBEEF), "EF BE AD DE");
			PerformWriterTest<int>(Test, int.MaxValue, "FF FF FF 7F");
			PerformWriterTest<int>(Test, -1, "FF FF FF FF");
			PerformWriterTest<int>(Test, short.MinValue, "00 80 FF FF");
			PerformWriterTest<int>(Test, int.MinValue, "00 00 00 80");

		}

		[Test]
		public void Test_WriteFixed32BE_Unsigned()
		{
			static void Test(ref SliceWriter writer, uint value) => writer.WriteUInt32BE(value);

			PerformWriterTest<uint>(Test, 0U, "00 00 00 00");
			PerformWriterTest<uint>(Test, 1U, "00 00 00 01");
			PerformWriterTest<uint>(Test, 0x12U, "00 00 00 12");
			PerformWriterTest<uint>(Test, 0x1234U, "00 00 12 34");
			PerformWriterTest<uint>(Test, ushort.MaxValue, "00 00 FF FF");
			PerformWriterTest<uint>(Test, 0x123456U, "00 12 34 56");
			PerformWriterTest<uint>(Test, 0xDEADBEEF, "DE AD BE EF");
			PerformWriterTest<uint>(Test, uint.MaxValue, "FF FF FF FF");
		}

		[Test]
		public void Test_WriteFixed32BE_Signed()
		{
			static void Test(ref SliceWriter writer, int value) => writer.WriteInt32BE(value);

			PerformWriterTest<int>(Test, 0, "00 00 00 00");
			PerformWriterTest<int>(Test, 1, "00 00 00 01");
			PerformWriterTest<int>(Test, 0x12, "00 00 00 12");
			PerformWriterTest<int>(Test, 0x1234, "00 00 12 34");
			PerformWriterTest<int>(Test, short.MaxValue, "00 00 7F FF");
			PerformWriterTest<int>(Test, ushort.MaxValue, "00 00 FF FF");
			PerformWriterTest<int>(Test, 0x123456, "00 12 34 56");
			PerformWriterTest<int>(Test, unchecked((int)0xDEADBEEF), "DE AD BE EF");
			PerformWriterTest<int>(Test, int.MaxValue, "7F FF FF FF");
			PerformWriterTest<int>(Test, -1, "FF FF FF FF");
			PerformWriterTest<int>(Test, short.MinValue, "FF FF 80 00");
			PerformWriterTest<int>(Test, int.MinValue, "80 00 00 00");

		}

		[Test]
		public void Test_WriteFixed64_Unsigned()
		{
			static void Test(ref SliceWriter writer, ulong value) => writer.WriteUInt64(value);

			PerformWriterTest<ulong>(Test, 0UL, "00 00 00 00 00 00 00 00");
			PerformWriterTest<ulong>(Test, 1UL, "01 00 00 00 00 00 00 00");
			PerformWriterTest<ulong>(Test, 0x12UL, "12 00 00 00 00 00 00 00");
			PerformWriterTest<ulong>(Test, 0x1234UL, "34 12 00 00 00 00 00 00");
			PerformWriterTest<ulong>(Test, ushort.MaxValue, "FF FF 00 00 00 00 00 00");
			PerformWriterTest<ulong>(Test, 0x123456UL, "56 34 12 00 00 00 00 00");
			PerformWriterTest<ulong>(Test, 0x12345678UL, "78 56 34 12 00 00 00 00");
			PerformWriterTest<ulong>(Test, uint.MaxValue, "FF FF FF FF 00 00 00 00");
			PerformWriterTest<ulong>(Test, 0x123456789AUL, "9A 78 56 34 12 00 00 00");
			PerformWriterTest<ulong>(Test, 0x123456789ABCUL, "BC 9A 78 56 34 12 00 00");
			PerformWriterTest<ulong>(Test, 0x123456789ABCDEUL, "DE BC 9A 78 56 34 12 00");
			PerformWriterTest<ulong>(Test, 0xBADC0FFEE0DDF00DUL, "0D F0 DD E0 FE 0F DC BA");
			PerformWriterTest<ulong>(Test, ulong.MaxValue, "FF FF FF FF FF FF FF FF");
		}

		[Test]
		public void Test_WriteFixed64_Signed()
		{
			static void Test(ref SliceWriter writer, long value) => writer.WriteInt64(value);

			PerformWriterTest<long>(Test, 0L, "00 00 00 00 00 00 00 00");
			PerformWriterTest<long>(Test, 1L, "01 00 00 00 00 00 00 00");
			PerformWriterTest<long>(Test, 0x12L, "12 00 00 00 00 00 00 00");
			PerformWriterTest<long>(Test, 0x1234L, "34 12 00 00 00 00 00 00");
			PerformWriterTest<long>(Test, short.MaxValue, "FF 7F 00 00 00 00 00 00");
			PerformWriterTest<long>(Test, ushort.MaxValue, "FF FF 00 00 00 00 00 00");
			PerformWriterTest<long>(Test, 0x123456L, "56 34 12 00 00 00 00 00");
			PerformWriterTest<long>(Test, 0x12345678L, "78 56 34 12 00 00 00 00");
			PerformWriterTest<long>(Test, int.MaxValue, "FF FF FF 7F 00 00 00 00");
			PerformWriterTest<long>(Test, uint.MaxValue, "FF FF FF FF 00 00 00 00");
			PerformWriterTest<long>(Test, 0x123456789AL, "9A 78 56 34 12 00 00 00");
			PerformWriterTest<long>(Test, 0x123456789ABCL, "BC 9A 78 56 34 12 00 00");
			PerformWriterTest<long>(Test, 0x123456789ABCDEL, "DE BC 9A 78 56 34 12 00");
			PerformWriterTest<long>(Test, unchecked((long) 0xBADC0FFEE0DDF00D), "0D F0 DD E0 FE 0F DC BA");
			PerformWriterTest<long>(Test, long.MaxValue, "FF FF FF FF FF FF FF 7F");
			PerformWriterTest<long>(Test, -1L, "FF FF FF FF FF FF FF FF");
			PerformWriterTest<long>(Test, short.MinValue, "00 80 FF FF FF FF FF FF");
			PerformWriterTest<long>(Test, int.MinValue, "00 00 00 80 FF FF FF FF");
			PerformWriterTest<long>(Test, long.MinValue, "00 00 00 00 00 00 00 80");
		}

		[Test]
		public void Test_WriteFixed64BE_Unsigned()
		{
			static void Test(ref SliceWriter writer, ulong value) => writer.WriteUInt64BE(value);

			PerformWriterTest<ulong>(Test, 0UL, "00 00 00 00 00 00 00 00");
			PerformWriterTest<ulong>(Test, 1UL, "00 00 00 00 00 00 00 01");
			PerformWriterTest<ulong>(Test, 0x12UL, "00 00 00 00 00 00 00 12");
			PerformWriterTest<ulong>(Test, 0x1234UL, "00 00 00 00 00 00 12 34");
			PerformWriterTest<ulong>(Test, ushort.MaxValue, "00 00 00 00 00 00 FF FF");
			PerformWriterTest<ulong>(Test, 0x123456UL, "00 00 00 00 00 12 34 56");
			PerformWriterTest<ulong>(Test, 0x12345678UL, "00 00 00 00 12 34 56 78");
			PerformWriterTest<ulong>(Test, uint.MaxValue, "00 00 00 00 FF FF FF FF");
			PerformWriterTest<ulong>(Test, 0x123456789AUL, "00 00 00 12 34 56 78 9A");
			PerformWriterTest<ulong>(Test, 0x123456789ABCUL, "00 00 12 34 56 78 9A BC");
			PerformWriterTest<ulong>(Test, 0x123456789ABCDEUL, "00 12 34 56 78 9A BC DE");
			PerformWriterTest<ulong>(Test, 0xBADC0FFEE0DDF00DUL, "BA DC 0F FE E0 DD F0 0D");
			PerformWriterTest<ulong>(Test, ulong.MaxValue, "FF FF FF FF FF FF FF FF");
		}

		[Test]
		public void Test_WriteFixed64BE_Signed()
		{
			static void Test(ref SliceWriter writer, long value) => writer.WriteInt64BE(value);

			PerformWriterTest<long>(Test, 0L, "00 00 00 00 00 00 00 00");
			PerformWriterTest<long>(Test, 1L, "00 00 00 00 00 00 00 01");
			PerformWriterTest<long>(Test, 0x12L, "00 00 00 00 00 00 00 12");
			PerformWriterTest<long>(Test, 0x1234L, "00 00 00 00 00 00 12 34");
			PerformWriterTest<long>(Test, short.MaxValue, "00 00 00 00 00 00 7F FF");
			PerformWriterTest<long>(Test, ushort.MaxValue, "00 00 00 00 00 00 FF FF");
			PerformWriterTest<long>(Test, 0x123456L, "00 00 00 00 00 12 34 56");
			PerformWriterTest<long>(Test, 0x12345678L, "00 00 00 00 12 34 56 78");
			PerformWriterTest<long>(Test, int.MaxValue, "00 00 00 00 7F FF FF FF");
			PerformWriterTest<long>(Test, uint.MaxValue, "00 00 00 00 FF FF FF FF");
			PerformWriterTest<long>(Test, 0x123456789AL, "00 00 00 12 34 56 78 9A");
			PerformWriterTest<long>(Test, 0x123456789ABCL, "00 00 12 34 56 78 9A BC");
			PerformWriterTest<long>(Test, 0x123456789ABCDEL, "00 12 34 56 78 9A BC DE");
			PerformWriterTest<long>(Test, unchecked((long)0xBADC0FFEE0DDF00D), "BA DC 0F FE E0 DD F0 0D");
			PerformWriterTest<long>(Test, long.MaxValue, "7F FF FF FF FF FF FF FF");
			PerformWriterTest<long>(Test, -1L, "FF FF FF FF FF FF FF FF");
			PerformWriterTest<long>(Test, short.MinValue, "FF FF FF FF FF FF 80 00");
			PerformWriterTest<long>(Test, int.MinValue, "FF FF FF FF 80 00 00 00");
			PerformWriterTest<long>(Test, long.MinValue, "80 00 00 00 00 00 00 00");
		}

#if NET8_0_OR_GREATER

		[Test]
		public void Test_WriteFixed128_Unsigned()
		{
			static void Test(ref SliceWriter writer, UInt128 value) => writer.WriteUInt128(value);

			PerformWriterTest<UInt128>(Test, 0UL, "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00");
			PerformWriterTest<UInt128>(Test, 1UL, "01 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00");
			PerformWriterTest<UInt128>(Test, 0x12UL, "12 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00");
			PerformWriterTest<UInt128>(Test, 0x1234UL, "34 12 00 00 00 00 00 00 00 00 00 00 00 00 00 00");
			PerformWriterTest<UInt128>(Test, ushort.MaxValue, "FF FF 00 00 00 00 00 00 00 00 00 00 00 00 00 00");
			PerformWriterTest<UInt128>(Test, 0x123456UL, "56 34 12 00 00 00 00 00 00 00 00 00 00 00 00 00");
			PerformWriterTest<UInt128>(Test, 0x12345678UL, "78 56 34 12 00 00 00 00 00 00 00 00 00 00 00 00");
			PerformWriterTest<UInt128>(Test, uint.MaxValue, "FF FF FF FF 00 00 00 00 00 00 00 00 00 00 00 00");
			PerformWriterTest<UInt128>(Test, 0x123456789AUL, "9A 78 56 34 12 00 00 00 00 00 00 00 00 00 00 00");
			PerformWriterTest<UInt128>(Test, 0x123456789ABCUL, "BC 9A 78 56 34 12 00 00 00 00 00 00 00 00 00 00");
			PerformWriterTest<UInt128>(Test, 0x123456789ABCDEUL, "DE BC 9A 78 56 34 12 00 00 00 00 00 00 00 00 00");
			PerformWriterTest<UInt128>(Test, 0xBADC0FFEE0DDF00DUL, "0D F0 DD E0 FE 0F DC BA 00 00 00 00 00 00 00 00");
			PerformWriterTest<UInt128>(Test, ulong.MaxValue, "FF FF FF FF FF FF FF FF 00 00 00 00 00 00 00 00");
			PerformWriterTest<UInt128>(Test, UInt128.MaxValue, "FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF");
		}

#endif

		[Test]
		public void Test_WriteVarint32()
		{
			static void Test(ref SliceWriter writer, uint value) => writer.WriteVarInt32(value);

			PerformWriterTest(Test, 0U, "00");
			PerformWriterTest(Test, 1U, "01");
			PerformWriterTest(Test, 127U, "7F");
			PerformWriterTest(Test, 128U, "80 01");
			PerformWriterTest(Test, 255U, "FF 01");
			PerformWriterTest(Test, 256U, "80 02");
			PerformWriterTest(Test, 16383U, "FF 7F");
			PerformWriterTest(Test, 16384U, "80 80 01");
			PerformWriterTest(Test, 2097151U, "FF FF 7F");
			PerformWriterTest(Test, 2097152U, "80 80 80 01");
			PerformWriterTest(Test, 268435455U, "FF FF FF 7F");
			PerformWriterTest(Test, 268435456U, "80 80 80 80 01");
			PerformWriterTest(Test, uint.MaxValue, "FF FF FF FF 0F");
		}

		[Test]
		public void Test_WriteVarint64()
		{
			static void Test(ref SliceWriter writer, ulong value) => writer.WriteVarInt64(value);

			PerformWriterTest(Test, 0UL, "00");
			PerformWriterTest(Test, 1UL, "01");
			PerformWriterTest(Test, 127UL, "7F");
			PerformWriterTest(Test, 128UL, "80 01");
			PerformWriterTest(Test, 255UL, "FF 01");
			PerformWriterTest(Test, 256UL, "80 02");
			PerformWriterTest(Test, 16383UL, "FF 7F");
			PerformWriterTest(Test, 16384UL, "80 80 01");
			PerformWriterTest(Test, 2097151UL, "FF FF 7F");
			PerformWriterTest(Test, 2097152UL, "80 80 80 01");
			PerformWriterTest(Test, 268435455UL, "FF FF FF 7F");
			PerformWriterTest(Test, 268435456UL, "80 80 80 80 01");
			PerformWriterTest(Test, 34359738367UL, "FF FF FF FF 7F");
			PerformWriterTest(Test, 34359738368UL, "80 80 80 80 80 01");
			PerformWriterTest(Test, 4398046511103UL, "FF FF FF FF FF 7F");
			PerformWriterTest(Test, 4398046511104UL, "80 80 80 80 80 80 01");
			PerformWriterTest(Test, 562949953421311UL, "FF FF FF FF FF FF 7F");
			PerformWriterTest(Test, 562949953421312UL, "80 80 80 80 80 80 80 01");
			PerformWriterTest(Test, 72057594037927935UL, "FF FF FF FF FF FF FF 7F");
			PerformWriterTest(Test, 72057594037927936UL, "80 80 80 80 80 80 80 80 01");
			PerformWriterTest(Test, 9223372036854775807UL, "FF FF FF FF FF FF FF FF 7F");
			PerformWriterTest(Test, 9223372036854775808UL, "80 80 80 80 80 80 80 80 80 01");
			PerformWriterTest(Test, ulong.MaxValue, "FF FF FF FF FF FF FF FF FF 01");
		}

#if NET8_0_OR_GREATER

		[Test]
		public void Test_WriteVarint128()
		{
			static void Test(ref SliceWriter writer, UInt128 value) => writer.WriteVarInt128(value);

			PerformWriterTest(Test, (UInt128) 0UL, "00");
			PerformWriterTest(Test, (UInt128) 1UL, "01");
			PerformWriterTest(Test, (UInt128) 127UL, "7F");
			PerformWriterTest(Test, (UInt128) 128UL, "80 01");
			PerformWriterTest(Test, (UInt128) 255UL, "FF 01");
			PerformWriterTest(Test, (UInt128) 256UL, "80 02");
			PerformWriterTest(Test, (UInt128) 16383UL, "FF 7F");
			PerformWriterTest(Test, (UInt128) 16384UL, "80 80 01");
			PerformWriterTest(Test, (UInt128) 2097151UL, "FF FF 7F");
			PerformWriterTest(Test, (UInt128) 2097152UL, "80 80 80 01");
			PerformWriterTest(Test, (UInt128) 268435455UL, "FF FF FF 7F");
			PerformWriterTest(Test, (UInt128) 268435456UL, "80 80 80 80 01");
			PerformWriterTest(Test, (UInt128) 34359738367UL, "FF FF FF FF 7F");
			PerformWriterTest(Test, (UInt128) 34359738368UL, "80 80 80 80 80 01");
			PerformWriterTest(Test, (UInt128) 4398046511103UL, "FF FF FF FF FF 7F");
			PerformWriterTest(Test, (UInt128) 4398046511104UL, "80 80 80 80 80 80 01");
			PerformWriterTest(Test, (UInt128) 562949953421311UL, "FF FF FF FF FF FF 7F");
			PerformWriterTest(Test, (UInt128) 562949953421312UL, "80 80 80 80 80 80 80 01");
			PerformWriterTest(Test, (UInt128) 72057594037927935UL, "FF FF FF FF FF FF FF 7F");
			PerformWriterTest(Test, (UInt128) 72057594037927936UL, "80 80 80 80 80 80 80 80 01");
			PerformWriterTest(Test, (UInt128) 9223372036854775807UL, "FF FF FF FF FF FF FF FF 7F");
			PerformWriterTest(Test, (UInt128) 9223372036854775808UL, "80 80 80 80 80 80 80 80 80 01");
			PerformWriterTest(Test, (UInt128) ulong.MaxValue, "FF FF FF FF FF FF FF FF FF 01");
			PerformWriterTest(Test, UInt128.MaxValue, "FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF FF 03");
		}

#endif

		[Test]
		public void Test_WriteVarBytes()
		{
			static void Test(ref SliceWriter writer, Slice value) => writer.WriteVarBytes(value);

			PerformWriterTest(Test, Slice.Nil, "00");
			PerformWriterTest(Test, Slice.Empty, "00");
			PerformWriterTest(Test, Slice.FromByte(42), "01 2A");
			PerformWriterTest(Test, Slice.FromByte(255), "01 FF");
			PerformWriterTest(Test, Slice.FromString("ABC"), "03 41 42 43");
			PerformWriterTest(Test, Slice.FromFixedU32(0xDEADBEEF), "04 EF BE AD DE");
		}

		[Test]
		public void Test_WriteBase10_Signed()
		{
			static void Test(ref SliceWriter writer, int value) => writer.WriteBase10(value);

			// positive numbers
			PerformWriterTest(Test, 0, "30");
			PerformWriterTest(Test, 1, "31");
			PerformWriterTest(Test, 9, "39");
			PerformWriterTest(Test, 10, "31 30");
			PerformWriterTest(Test, 42, "34 32");
			PerformWriterTest(Test, 99, "39 39");
			PerformWriterTest(Test, 100, "31 30 30");
			PerformWriterTest(Test, 123, "31 32 33");
			PerformWriterTest(Test, 999, "39 39 39");
			PerformWriterTest(Test, 1000, "31 30 30 30");
			PerformWriterTest(Test, 1234, "31 32 33 34");
			PerformWriterTest(Test, 9999, "39 39 39 39");
			PerformWriterTest(Test, 10000, "31 30 30 30 30");
			PerformWriterTest(Test, 12345, "31 32 33 34 35");
			PerformWriterTest(Test, 99999, "39 39 39 39 39");
			PerformWriterTest(Test, 100000, "31 30 30 30 30 30");
			PerformWriterTest(Test, 123456, "31 32 33 34 35 36");
			PerformWriterTest(Test, 999999, "39 39 39 39 39 39");
			PerformWriterTest(Test, 1000000, "31 30 30 30 30 30 30");
			PerformWriterTest(Test, 1234567, "31 32 33 34 35 36 37");
			PerformWriterTest(Test, 9999999, "39 39 39 39 39 39 39");
			PerformWriterTest(Test, 10000000, "31 30 30 30 30 30 30 30");
			PerformWriterTest(Test, 12345678, "31 32 33 34 35 36 37 38");
			PerformWriterTest(Test, 99999999, "39 39 39 39 39 39 39 39");
			PerformWriterTest(Test, 100000000, "31 30 30 30 30 30 30 30 30");
			PerformWriterTest(Test, 123456789, "31 32 33 34 35 36 37 38 39");
			PerformWriterTest(Test, 999999999, "39 39 39 39 39 39 39 39 39");
			PerformWriterTest(Test, int.MaxValue, "32 31 34 37 34 38 33 36 34 37");

			// negative numbers
			PerformWriterTest(Test, -1, "2D 31");
			PerformWriterTest(Test, -9, "2D 39");
			PerformWriterTest(Test, -10, "2D 31 30");
			PerformWriterTest(Test, -42, "2D 34 32");
			PerformWriterTest(Test, -99, "2D 39 39");
			PerformWriterTest(Test, -100, "2D 31 30 30");
			PerformWriterTest(Test, -123, "2D 31 32 33");
			PerformWriterTest(Test, -999, "2D 39 39 39");
			PerformWriterTest(Test, -1000, "2D 31 30 30 30");
			PerformWriterTest(Test, -1234, "2D 31 32 33 34");
			PerformWriterTest(Test, -9999, "2D 39 39 39 39");
			PerformWriterTest(Test, -10000, "2D 31 30 30 30 30");
			PerformWriterTest(Test, -12345, "2D 31 32 33 34 35");
			PerformWriterTest(Test, -99999, "2D 39 39 39 39 39");
			PerformWriterTest(Test, -100000, "2D 31 30 30 30 30 30");
			PerformWriterTest(Test, -123456, "2D 31 32 33 34 35 36");
			PerformWriterTest(Test, -999999, "2D 39 39 39 39 39 39");
			PerformWriterTest(Test, -1000000, "2D 31 30 30 30 30 30 30");
			PerformWriterTest(Test, -1234567, "2D 31 32 33 34 35 36 37");
			PerformWriterTest(Test, -9999999, "2D 39 39 39 39 39 39 39");
			PerformWriterTest(Test, -10000000, "2D 31 30 30 30 30 30 30 30");
			PerformWriterTest(Test, -12345678, "2D 31 32 33 34 35 36 37 38");
			PerformWriterTest(Test, -99999999, "2D 39 39 39 39 39 39 39 39");
			PerformWriterTest(Test, -100000000, "2D 31 30 30 30 30 30 30 30 30");
			PerformWriterTest(Test, -123456789, "2D 31 32 33 34 35 36 37 38 39");
			PerformWriterTest(Test, -999999999, "2D 39 39 39 39 39 39 39 39 39");
			PerformWriterTest(Test, int.MinValue, "2D 32 31 34 37 34 38 33 36 34 38");
		}

		[Test]
		public void Test_WriteBase10_Unsigned()
		{
			static void Test(ref SliceWriter writer, uint value) => writer.WriteBase10(value);

			// positive numbers
			PerformWriterTest<uint>(Test, 0, "30");
			PerformWriterTest<uint>(Test, 1, "31");
			PerformWriterTest<uint>(Test, 9, "39");
			PerformWriterTest<uint>(Test, 10, "31 30");
			PerformWriterTest<uint>(Test, 42, "34 32");
			PerformWriterTest<uint>(Test, 99, "39 39");
			PerformWriterTest<uint>(Test, 100, "31 30 30");
			PerformWriterTest<uint>(Test, 123, "31 32 33");
			PerformWriterTest<uint>(Test, 999, "39 39 39");
			PerformWriterTest<uint>(Test, 1000, "31 30 30 30");
			PerformWriterTest<uint>(Test, 1234, "31 32 33 34");
			PerformWriterTest<uint>(Test, 9999, "39 39 39 39");
			PerformWriterTest<uint>(Test, 10000, "31 30 30 30 30");
			PerformWriterTest<uint>(Test, 12345, "31 32 33 34 35");
			PerformWriterTest<uint>(Test, 99999, "39 39 39 39 39");
			PerformWriterTest<uint>(Test, 100000, "31 30 30 30 30 30");
			PerformWriterTest<uint>(Test, 123456, "31 32 33 34 35 36");
			PerformWriterTest<uint>(Test, 999999, "39 39 39 39 39 39");
			PerformWriterTest<uint>(Test, 1000000, "31 30 30 30 30 30 30");
			PerformWriterTest<uint>(Test, 1234567, "31 32 33 34 35 36 37");
			PerformWriterTest<uint>(Test, 9999999, "39 39 39 39 39 39 39");
			PerformWriterTest<uint>(Test, 10000000, "31 30 30 30 30 30 30 30");
			PerformWriterTest<uint>(Test, 12345678, "31 32 33 34 35 36 37 38");
			PerformWriterTest<uint>(Test, 99999999, "39 39 39 39 39 39 39 39");
			PerformWriterTest<uint>(Test, 100000000, "31 30 30 30 30 30 30 30 30");
			PerformWriterTest<uint>(Test, 123456789, "31 32 33 34 35 36 37 38 39");
			PerformWriterTest<uint>(Test, 999999999, "39 39 39 39 39 39 39 39 39");
			PerformWriterTest<uint>(Test, int.MaxValue, "32 31 34 37 34 38 33 36 34 37");
			PerformWriterTest<uint>(Test, uint.MaxValue, "34 32 39 34 39 36 37 32 39 35");
		}

		[Test]
		public void Test_Indexer()
		{
			var slice = new SliceWriter();
			slice.WriteUInt64(0xBADC0FFEE0DDF00DUL);

			Assert.That(slice[0], Is.EqualTo(0x0D));
			Assert.That(slice[1], Is.EqualTo(0xF0));
			Assert.That(slice[2], Is.EqualTo(0xDD));
			Assert.That(slice[3], Is.EqualTo(0xE0));
			Assert.That(slice[4], Is.EqualTo(0xFE));
			Assert.That(slice[5], Is.EqualTo(0x0F));
			Assert.That(slice[6], Is.EqualTo(0xDC));
			Assert.That(slice[7], Is.EqualTo(0xBA));

			Assert.That(slice[-1], Is.EqualTo(0xBA));
			Assert.That(slice[-2], Is.EqualTo(0xDC));
			Assert.That(slice[-3], Is.EqualTo(0x0F));
			Assert.That(slice[-4], Is.EqualTo(0xFE));
			Assert.That(slice[-5], Is.EqualTo(0xE0));
			Assert.That(slice[-6], Is.EqualTo(0xDD));
			Assert.That(slice[-7], Is.EqualTo(0xF0));
			Assert.That(slice[-8], Is.EqualTo(0x0D));

			Assert.That(() => slice[8], Throws.InstanceOf<IndexOutOfRangeException>());
			Assert.That(() => slice[-9], Throws.InstanceOf<IndexOutOfRangeException>());
		}

		[Test]
		public void Test_Flush()
		{
			var writer = new SliceWriter();
			writer.WriteBytes(Slice.FromString("hello world!"));
			Assert.That(writer.Position, Is.EqualTo(12));

			writer.Flush(5);
			Assert.That(writer.Position, Is.EqualTo(7));
			Assert.That(writer.ToSlice().ToString(), Is.EqualTo(" world!"));

			writer.Flush(1);
			Assert.That(writer.Position, Is.EqualTo(6));
			Assert.That(writer.ToSlice().ToString(), Is.EqualTo("world!"));

			writer.Flush(0);
			Assert.That(writer.Position, Is.EqualTo(6));
			Assert.That(writer.ToSlice().ToString(), Is.EqualTo("world!"));

			// REVIEW: should we throw if we flush more bytes than in the writer? (currently, it just clears it)
			writer.Flush(7);
			Assert.That(writer.Position, Is.EqualTo(0));
			Assert.That(writer.ToSlice(), Is.EqualTo(Slice.Empty));
		}

		[Test]
		public void Test_Skip()
		{
			var writer = new SliceWriter();
			writer.WriteBytes(Slice.FromString("hello"));
			Assert.That(writer.Position, Is.EqualTo(5));
			Assert.That(writer.ToSlice().ToString(), Is.EqualTo("hello"));

			// default pad is 255
			Assert.That(writer.Skip(3), Is.EqualTo(5));
			Assert.That(writer.Position, Is.EqualTo(8));
			Assert.That(writer.ToSlice().ToString(), Is.EqualTo("hello<FF><FF><FF>"));

			writer.WriteBytes(Slice.FromString("world"));
			Assert.That(writer.Position, Is.EqualTo(13));
			Assert.That(writer.ToSlice().ToString(), Is.EqualTo("hello<FF><FF><FF>world"));

			// custom pad
			Assert.That(writer.Skip(5, 42), Is.EqualTo(13));
			Assert.That(writer.Position, Is.EqualTo(18));
			Assert.That(writer.ToSlice().ToString(), Is.EqualTo("hello<FF><FF><FF>world*****"));
		}

		[Test]
		public void Test_ToSlice()
		{
			var writer = new SliceWriter(64);
			var slice = writer.ToSlice();
			//note: slice.Array is not guaranteed to be equal to writer.Buffer
			Assert.That(slice.Count, Is.EqualTo(0));
			Assert.That(slice.Offset, Is.EqualTo(0));

			writer.WriteBytes(Slice.FromString("hello world!"));
			slice = writer.ToSlice();
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(12));
			Assert.That(slice.ToStringAscii(), Is.EqualTo("hello world!"));

			writer.WriteBytes(Slice.FromString("foo"));
			slice = writer.ToSlice();
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(15));
			Assert.That(slice.ToStringAscii(), Is.EqualTo("hello world!foo"));
		}

		[Test]
		public void Test_Head()
		{
			var writer = new SliceWriter(64);
			var slice = writer.Head(0);
			Assert.That(slice.Count, Is.EqualTo(0));
			Assert.That(slice.Offset, Is.EqualTo(0));
			//note: slice.Array is not guaranteed to be equal to writer.Buffer
			Assert.That(() => writer.Head(1), Throws.InstanceOf<ArgumentOutOfRangeException>());

			writer.WriteBytes(Slice.FromString("hello world!"));
			slice = writer.Head(5);
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(5));
			Assert.That(slice.ToStringAscii(), Is.EqualTo("hello"));

			slice = writer.Head(12);
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(12));
			Assert.That(slice.ToStringAscii(), Is.EqualTo("hello world!"));

			Assert.That(() => writer.Head(13), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => writer.Head(-1), Throws.InstanceOf<ArgumentOutOfRangeException>());

			writer.WriteBytes(Slice.FromString("foo"));
			slice = writer.Head(3);
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(3));
			Assert.That(slice.ToStringAscii(), Is.EqualTo("hel"));

			slice = writer.Head(15);
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(15));
			Assert.That(slice.ToStringAscii(), Is.EqualTo("hello world!foo"));

			Assert.That(() => writer.Head(16), Throws.InstanceOf<ArgumentOutOfRangeException>());

		}

		[Test]
		public void Test_Tail()
		{
			var writer = new SliceWriter(64);
			var slice = writer.Tail(0);
			Assert.That(slice.Count, Is.EqualTo(0));
			Assert.That(slice.Offset, Is.EqualTo(0));
			//note: slice.Array is not guaranteed to be equal to writer.Buffer
			Assert.That(() => writer.Head(1), Throws.InstanceOf<ArgumentOutOfRangeException>());

			writer.WriteBytes(Slice.FromString("hello world!"));
			slice = writer.Tail(6);
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(6));
			Assert.That(slice.Count, Is.EqualTo(6));
			Assert.That(slice.ToStringAscii(), Is.EqualTo("world!"));

			slice = writer.Tail(12);
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(12));
			Assert.That(slice.ToStringAscii(), Is.EqualTo("hello world!"));

			Assert.That(() => writer.Tail(13), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => writer.Tail(-1), Throws.InstanceOf<ArgumentOutOfRangeException>());

			writer.WriteBytes(Slice.FromString("foo"));
			slice = writer.Tail(3);
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(12));
			Assert.That(slice.Count, Is.EqualTo(3));
			Assert.That(slice.ToStringAscii(), Is.EqualTo("foo"));

			slice = writer.Tail(15);
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(15));
			Assert.That(slice.ToStringAscii(), Is.EqualTo("hello world!foo"));

			Assert.That(() => writer.Tail(16), Throws.InstanceOf<ArgumentOutOfRangeException>());

		}

		[Test]
		public void Test_AppendBytes()
		{
			var writer = new SliceWriter(64);
			var slice = writer.AppendBytes(Slice.Empty);
			//note: slice.Array is not guaranteed to be equal to writer.Buffer
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(0));

			slice = writer.AppendBytes(Slice.FromString("hello world!"));
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(12));
			Assert.That(slice.ToStringUtf8(), Is.EqualTo("hello world!"));
			Assert.That(writer.ToSlice().ToStringUtf8(), Is.EqualTo("hello world!"));

			var foo = Slice.FromString("foo");
			slice = writer.AppendBytes(foo);
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(12));
			Assert.That(slice.Count, Is.EqualTo(3));
			Assert.That(slice.ToStringUtf8(), Is.EqualTo("foo"));
			Assert.That(writer.ToSlice().ToStringUtf8(), Is.EqualTo("hello world!foo"));

			var bar = Slice.FromString("bar");
			slice = writer.AppendBytes(bar.Span);
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(15));
			Assert.That(slice.Count, Is.EqualTo(3));
			Assert.That(slice.ToStringUtf8(), Is.EqualTo("bar"));
			Assert.That(writer.ToSlice().ToStringUtf8(), Is.EqualTo("hello world!foobar"));

			var baz = Slice.FromString("baz");
			unsafe
			{
				fixed (byte* ptr = &baz.DangerousGetPinnableReference())
				{
					slice = writer.AppendBytes(new ReadOnlySpan<byte>(ptr, 3));
				}
			}
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(18));
			Assert.That(slice.Count, Is.EqualTo(3));
			Assert.That(slice.ToStringUtf8(), Is.EqualTo("baz"));
			Assert.That(writer.ToSlice().ToStringUtf8(), Is.EqualTo("hello world!foobarbaz"));

			slice = writer.AppendBytes(ReadOnlySpan<byte>.Empty);
			//note: slice.Array is not guaranteed to be equal to writer.Buffer
			Assert.That(slice.Offset, Is.EqualTo(0)); //REVIEW: should we return (Buffer, Position, 0) instead of (EmptyArray, 0, 0) ?
			Assert.That(slice.Count, Is.EqualTo(0));
		}

		[Test]
		public void Test_WriteBytes_Resize_Buffer()
		{

			// check buffer resize occurs as intended
			var original = new byte[32];
			var writer = new SliceWriter(original);
			Assert.That(writer.Buffer, Is.SameAs(original));

			// first write should not resize the buffer
			writer.WriteBytes(Slice.Repeat((byte)'a', 24));
			Assert.That(writer.Buffer, Is.SameAs(original));
			Assert.That(writer.ToSlice().ToStringAscii(), Is.EqualTo("aaaaaaaaaaaaaaaaaaaaaaaa"));

			// second write should resize the buffer
			writer.WriteBytes(Slice.Repeat((byte)'b', 24));
			// buffer should have been replaced with larger one
			Assert.That(writer.Buffer, Is.Not.SameAs(original));
			Assert.That(writer.Buffer!.Length, Is.GreaterThanOrEqualTo(48));

			//but the content should be unchanged
			Assert.That(writer.ToSlice().ToStringAscii(), Is.EqualTo("aaaaaaaaaaaaaaaaaaaaaaaabbbbbbbbbbbbbbbbbbbbbbbb"));

			// adding exactly what is missing should not resize the buffer
			writer = new SliceWriter(original);
			writer.WriteBytes(Slice.Repeat((byte)'c', original.Length));
			Assert.That(writer.Buffer, Is.SameAs(original));
			Assert.That(writer.ToSlice().ToStringAscii(), Is.EqualTo("cccccccccccccccccccccccccccccccc"));

			// adding nothing should not resize the buffer
			writer.WriteBytes(Slice.Empty);
			Assert.That(writer.Buffer, Is.SameAs(original));
			Assert.That(writer.ToSlice().ToStringAscii(), Is.EqualTo("cccccccccccccccccccccccccccccccc"));

			// adding a single byte should resize the buffer
			writer.WriteBytes(Slice.FromChar('Z'));
			Assert.That(writer.Buffer, Is.Not.SameAs(original));
			Assert.That(writer.Buffer!.Length, Is.GreaterThanOrEqualTo(33));
			Assert.That(writer.ToSlice().ToStringAscii(), Is.EqualTo("ccccccccccccccccccccccccccccccccZ"));
		}

		[Test]
		public void Test_AppendBytes_Resize_Buffer()
		{

			// check buffer resize occurs as intended
			var original = new byte[32];
			var writer = new SliceWriter(original);
			Assert.That(writer.Buffer, Is.SameAs(original));

			// first write should not resize the buffer
			var aaa = writer.AppendBytes(Slice.Repeat((byte) 'a', 24));
			Assert.That(aaa.Array, Is.SameAs(original));

			// second write should resize the buffer
			var bbb = writer.AppendBytes(Slice.Repeat((byte) 'b', 24));
			Assert.That(bbb.Array, Is.SameAs(writer.Buffer));
			//note: buffer should have been copied between both calls, so 'aaa' should point to the OLD buffer
			Assert.That(bbb.Array, Is.Not.SameAs(original));
			//but the content should be unchanged
			Assert.That(writer.ToSlice().ToStringAscii(), Is.EqualTo("aaaaaaaaaaaaaaaaaaaaaaaabbbbbbbbbbbbbbbbbbbbbbbb"));
			// => mutating aaa should not change the buffer
			aaa.Array[aaa.Offset] = (byte) 'Z';
			Assert.That(writer.ToSlice().ToStringAscii(), Is.EqualTo("aaaaaaaaaaaaaaaaaaaaaaaabbbbbbbbbbbbbbbbbbbbbbbb"));
			// => but mutating bbb should change the buffer
			bbb.Array[bbb.Offset] = (byte)'Z';
			Assert.That(writer.ToSlice().ToStringAscii(), Is.EqualTo("aaaaaaaaaaaaaaaaaaaaaaaaZbbbbbbbbbbbbbbbbbbbbbbb"));
		}

		[Test]
		public void Test_WriteBase10()
		{
			static void Verify(int value, ReadOnlySpan<byte> expected)
			{
				var writer = new SliceWriter();
				writer.WriteBase10(value);
				var res = writer.ToSlice();
				if (!res.Equals(expected))
				{
					DumpVersus(res.Span, expected);
					Assert.Fail($"Expected '{expected.PrettyPrint()}' but got '{res.PrettyPrint()}'");
				}
			}

			Verify(0, "0"u8);

			Verify(1, "1"u8);
			Verify(9, "9"u8);
			Verify(12, "12"u8);
			Verify(123, "123"u8);
			Verify(1_234, "1234"u8);
			Verify(12_345, "12345"u8);
			Verify(123_456, "123456"u8);
			Verify(1_234_567, "1234567"u8);
			Verify(12_345_678, "12345678"u8);
			Verify(123_456_789, "123456789"u8);
			Verify(1_234_567_890, "1234567890"u8);
			Verify(int.MaxValue, "2147483647"u8);

			Verify(-1, "-1"u8);
			Verify(-9, "-9"u8);
			Verify(-12, "-12"u8);
			Verify(-123, "-123"u8);
			Verify(-1_234, "-1234"u8);
			Verify(-12_345, "-12345"u8);
			Verify(-123_456, "-123456"u8);
			Verify(-1_234_567, "-1234567"u8);
			Verify(-12_345_678, "-12345678"u8);
			Verify(-123_456_789, "-123456789"u8);
			Verify(-1_234_567_890, "-1234567890"u8);
			Verify(int.MinValue, "-2147483648"u8);
		}

		[Test]
		public void Test_Pooled_Basics()
		{
			var pool = HistoryRecordingArrayPool
				.Create<byte>()
#if FULL_DEBUG
				.WithFilter(
					(e) => Log($"# allocated {e.AllocatedSize} as <{e.Tag}>"),
					(e) => Log($"# returned <{e.Tag}>")
				)
#endif
				;

			Log("Creating initial empty reader");
			var writer = new SliceWriter(pool);
			pool.Dump();
			Assert.That(writer.Pool, Is.SameAs(pool));
			Assert.That(writer.Position, Is.Zero);
			Assert.That(writer.Capacity, Is.Zero);
			Log();

			Log("Write some text...");
			writer.WriteStringUtf8("Hello, there!");
			pool.Dump();
			Assert.That(pool.Rented, Has.Count.EqualTo(1), "Should have rented the first buffer");
			pool.AssertRented(1);
			pool.Rented[^1].AssertSame(writer.Buffer, "Writer should use the pooled buffer");
			pool.AssertReturned(0);
			pool.AssertPending(1);
			Log();

			Log("Write some large data...");
			writer.Allocate(100 * 1024);
			pool.Dump();
			pool.AssertRented(2);
			pool.Rented[^1].AssertSame(writer.Buffer, "Should use the new rented buffer");
			pool.AssertReturned(1);
			pool.AssertPending(1);
			Log();

			Log("Dispose...");
			writer.Dispose();
			pool.Dump();
			Assert.That(writer.Pool, Is.SameAs(pool));
			Assert.That(writer.Position, Is.Zero);
			Assert.That(writer.Capacity, Is.Zero);
			Assert.That(writer.Buffer, Is.Empty);
			pool.AssertAllReturned();
			Log();

			// writer can be reused

			Log("Reuse after dispose...");
			writer.WriteStringUtf8("dQw4w9WgXcQ");
			pool.Dump();
			Assert.That(pool.Rented, Has.Count.EqualTo(3), "Should have rented the first buffer");
			pool.Rented[^1].AssertSame(writer.Buffer);
			Assert.That(pool.NotReturned, Has.Count.EqualTo(1));
			Log();

			writer.Dispose();
			pool.Dump();
			Assert.That(writer.Pool, Is.SameAs(pool));
			Assert.That(writer.Position, Is.Zero);
			Assert.That(writer.Capacity, Is.Zero);
			Assert.That(writer.Buffer, Is.Empty);
			pool.AssertAllReturned();
		}

		[Test]
		public void Test_Pooled_Slow_Growth()
		{
			const int N = 1_000_000;

			var pool = HistoryRecordingArrayPool
				.Create<byte>()
				//.WithStackTraces()
				;

			Log($"Writing {N:N0} small values...");

			var writer = new SliceWriter(pool);

			for (int i = 0; i < N; i++)
			{
				writer.WriteInt32(i);
			}

			pool.Dump();
			Assert.That(pool.Rented, Has.Count.GreaterThan(0));

			writer.Dispose();
			pool.AssertAllReturned();
		}

		[Test]
		public void Test_Pooled_ToSliceOwner()
		{
			// test writing to a pooled writer, and extracting the content as a SliceOwner
			// => the writer should release its "ownership" of the pooled buffer

			var pool = HistoryRecordingArrayPool
				.Create<byte>()
				.WithStackTraces()
				;

			Log("Write some text...");
			var writer = new SliceWriter(pool);
			writer.WriteStringUtf8("Hello, there!");
			pool.Dump();
			pool.Rented[^1].AssertSame(writer.Buffer, "Writer should use the pooled buffer");
			Log();

			Log("Get the pooled buffer out of the writer...");
			var slice = writer.ToSliceOwner();
			pool.Dump();
			Assert.That(slice.Pool, Is.SameAs(pool));
			pool.Rented[^1].AssertSame(slice.Data.Array, "Buffer should have transferred the buffer to the slice");
			Assert.That(writer.Buffer, Is.Empty, "Buffer should have released the buffer");
			Log();

			Log("Dispose the writer...");
			writer.Dispose();
			pool.Dump();
			Assert.That(pool.Returned, Has.Count.Zero, "Nothing should have been returned to the pool");
			Log();

			Log("Dispose the extracted slice...");
			slice.Dispose();
			pool.Dump();
			Assert.That(pool.Returned, Has.Count.EqualTo(1), "The buffer should have been returned to the pool");
			Log();

			pool.AssertAllReturned();
		}

		[Test]
		public void Test_SliceWriter_SpanEncodable()
		{

			var sw = new SliceWriter();

			Assert.That(((ISpanEncodable) sw).TryGetSpan(out var span), Is.True);
			Assert.That(span.Length, Is.Zero);

			Assert.That(((ISpanEncodable) sw).TryGetSizeHint(out var size), Is.True.WithOutput(size).Zero);

			var buf = new byte[32];
			buf.AsSpan().Fill(0xAA);
			Assert.That(((ISpanEncodable) sw).TryEncode(buf, out var written), Is.True.WithOutput(written).Zero);
			Assert.That(buf.AsSpan().ContainsAnyExcept((byte)0xAA), Is.False);

			sw.WriteStringUtf8("Hello");

			Assert.That(((ISpanEncodable) sw).TryGetSpan(out span), Is.True);
			Assert.That(span.Length, Is.EqualTo(sw.Position));
			Assert.That(span.ToArray(), Is.EqualTo("Hello"u8.ToArray()));

			Assert.That(((ISpanEncodable) sw).TryGetSizeHint(out size), Is.True.WithOutput(size).EqualTo(sw.Position));

			// large buffer
			buf.AsSpan().Fill(0xAA);
			Assert.That(((ISpanEncodable) sw).TryEncode(buf, out written), Is.True);
			Assert.That(buf[..written], Is.EqualTo(sw.ToSpan().ToArray()));
			Assert.That(buf.AsSpan(written).ContainsAnyExcept((byte) 0xAA), Is.False);

			// exact buffer size
			buf.AsSpan().Fill(0xAA);
			Assert.That(((ISpanEncodable) sw).TryEncode(buf.AsSpan(0, sw.Position), out written), Is.True);
			Assert.That(buf[..written], Is.EqualTo(sw.ToSpan().ToArray()));
			Assert.That(buf.AsSpan(written).ContainsAnyExcept((byte) 0xAA), Is.False);

			// buffer too small
			buf.AsSpan().Fill(0xAA);
			Assert.That(((ISpanEncodable) sw).TryEncode(buf.AsSpan(0, sw.Position - 1), out written), Is.False.WithOutput(written).Zero);
			Assert.That(buf.AsSpan().ContainsAnyExcept((byte) 0xAA), Is.False);
		}

	}

}
