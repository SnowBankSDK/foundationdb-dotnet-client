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

namespace SnowBank.Buffers.Binary.Tests
{
	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public unsafe class UnsafeHelpersFacts : SimpleTest
	{

		private static void Wipe(byte[] data)
		{
			data.AsSpan().Fill((byte) 'X');
		}

		private static string ToHex(byte[] data)
		{
			return string.Join(" ", data.Select(b => b.ToString("X02")));
		}

		private void EnsureIsAllSameByte(byte* buffer, uint count, byte b)
		{
			byte* ptr = buffer;
			byte* stop = ptr + count;
			while (ptr < stop)
			{
				if (*ptr != b) Assert.That(*ptr, Is.EqualTo(b), $"Unexpected byte at offset {ptr - buffer}");
				ptr++;
			}
		}

		[Test]
		public void Test_UnsafeHelpers_ByteSwap()
		{
			// unsigned
			Assert.That(UnsafeHelpers.ByteSwap16((ushort) 0x1234), Is.EqualTo(0x3412));
			Assert.That(UnsafeHelpers.ByteSwap32((uint) 0x12345678), Is.EqualTo(0x78563412));
			Assert.That(UnsafeHelpers.ByteSwap64((ulong) 0x123456789ABCDEF0), Is.EqualTo(0xF0DEBC9A78563412));

			// signed (positive)
			Assert.That(UnsafeHelpers.ByteSwap16((short) 0x1234), Is.EqualTo(0x3412));
			Assert.That(UnsafeHelpers.ByteSwap32((int) 0x12345678), Is.EqualTo(0x78563412));
			Assert.That(UnsafeHelpers.ByteSwap64((long) 0x123456789ABCDEF0), Is.EqualTo(unchecked((long) 0xF0DEBC9A78563412)));
			Assert.That(UnsafeHelpers.ByteSwap16(UnsafeHelpers.ByteSwap16((short) 0x1234)), Is.EqualTo((short) 0x1234));
			Assert.That(UnsafeHelpers.ByteSwap32(UnsafeHelpers.ByteSwap32((int) 0x12345678)), Is.EqualTo((int) 0x12345678));
			Assert.That(UnsafeHelpers.ByteSwap64(UnsafeHelpers.ByteSwap64((long) 0x123456789ABCDEF0)), Is.EqualTo((long) 0x123456789ABCDEF0));

			// signed (negative)
			Assert.That(UnsafeHelpers.ByteSwap16((short) -0x1234), Is.EqualTo(unchecked((short) 0xCCED)));
			Assert.That(UnsafeHelpers.ByteSwap32((int) -0x12345678), Is.EqualTo(unchecked((int) 0x88A9CBED)));
			Assert.That(UnsafeHelpers.ByteSwap64((long) -0x123456789ABCDEF0), Is.EqualTo(0x1021436587A9CBED));
			Assert.That(UnsafeHelpers.ByteSwap16(UnsafeHelpers.ByteSwap16((short) -0x1234)), Is.EqualTo((short) -0x1234));
			Assert.That(UnsafeHelpers.ByteSwap32(UnsafeHelpers.ByteSwap32((int) -0x12345678)), Is.EqualTo((int) -0x12345678));
			Assert.That(UnsafeHelpers.ByteSwap64(UnsafeHelpers.ByteSwap64((long) -0x123456789ABCDEF0)), Is.EqualTo((long) -0x123456789ABCDEF0));
		}

		[Test]
		public void Test_UnsafeHelpers_Loads_And_Stores()
		{
			const int CAPA = 128;
			const int PAD = 16;
			byte* buf = stackalloc byte[CAPA];
			byte* ptr = buf + PAD; // safety padding

			// UInt16 LE
			UnsafeHelpers.FillUnsafe(buf, CAPA, 0xAA);
			UnsafeHelpers.StoreUInt16LE((ushort*) ptr, 0x1234);
			Assert.That(ptr[0], Is.EqualTo(0x34));
			Assert.That(ptr[1], Is.EqualTo(0x12));
			EnsureIsAllSameByte(buf, PAD, 0xAA);
			EnsureIsAllSameByte(buf + PAD + sizeof(ushort), CAPA - PAD - sizeof(ushort), 0xAA);
			Assert.That(UnsafeHelpers.LoadUInt16LE((ushort*) ptr), Is.EqualTo(0x1234));

			// UInt16 BE
			UnsafeHelpers.FillUnsafe(buf, CAPA, 0xAA);
			UnsafeHelpers.StoreUInt16BE((ushort*) ptr, 0x1234);
			Assert.That(ptr[0], Is.EqualTo(0x12));
			Assert.That(ptr[1], Is.EqualTo(0x34));
			EnsureIsAllSameByte(buf, PAD, 0xAA);
			EnsureIsAllSameByte(buf + PAD + sizeof(ushort), CAPA - PAD - sizeof(ushort), 0xAA);
			Assert.That(UnsafeHelpers.LoadUInt16BE((ushort*) ptr), Is.EqualTo(0x1234));

			// UInt32 LE
			UnsafeHelpers.FillUnsafe(buf, CAPA, 0xAA);
			UnsafeHelpers.StoreUInt32LE((uint*) ptr, 0x12345678);
			Assert.That(ptr[0], Is.EqualTo(0x78));
			Assert.That(ptr[1], Is.EqualTo(0x56));
			Assert.That(ptr[2], Is.EqualTo(0x34));
			Assert.That(ptr[3], Is.EqualTo(0x12));
			EnsureIsAllSameByte(buf, PAD, 0xAA);
			EnsureIsAllSameByte(buf + PAD + sizeof(uint), CAPA - PAD - sizeof(uint), 0xAA);
			Assert.That(UnsafeHelpers.LoadUInt32LE((uint*) ptr), Is.EqualTo(0x12345678));

			// UInt32 BE
			UnsafeHelpers.FillUnsafe(buf, CAPA, 0xAA);
			UnsafeHelpers.StoreUInt32BE((uint*) ptr, 0x12345678);
			Assert.That(ptr[0], Is.EqualTo(0x12));
			Assert.That(ptr[1], Is.EqualTo(0x34));
			Assert.That(ptr[2], Is.EqualTo(0x56));
			Assert.That(ptr[3], Is.EqualTo(0x78));
			EnsureIsAllSameByte(buf, PAD, 0xAA);
			EnsureIsAllSameByte(buf + PAD + sizeof(uint), CAPA - PAD - sizeof(uint), 0xAA);
			Assert.That(UnsafeHelpers.LoadUInt32BE((uint*) ptr), Is.EqualTo(0x12345678));

			// UInt64 LE
			UnsafeHelpers.FillUnsafe(buf, CAPA, 0xAA);
			UnsafeHelpers.StoreUInt64LE((ulong*) ptr, 0x0123456789ABCDEF);
			Assert.That(ptr[0], Is.EqualTo(0xEF));
			Assert.That(ptr[1], Is.EqualTo(0xCD));
			Assert.That(ptr[2], Is.EqualTo(0xAB));
			Assert.That(ptr[3], Is.EqualTo(0x89));
			Assert.That(ptr[4], Is.EqualTo(0x67));
			Assert.That(ptr[5], Is.EqualTo(0x45));
			Assert.That(ptr[6], Is.EqualTo(0x23));
			Assert.That(ptr[7], Is.EqualTo(0x01));
			EnsureIsAllSameByte(buf, PAD, 0xAA);
			EnsureIsAllSameByte(buf + PAD + sizeof(ulong), CAPA - PAD - sizeof(ulong), 0xAA);
			Assert.That(UnsafeHelpers.LoadUInt64LE((ulong*) ptr), Is.EqualTo(0x0123456789ABCDEF));

			// UInt64 BE
			UnsafeHelpers.FillUnsafe(buf, CAPA, 0xAA);
			UnsafeHelpers.StoreUInt64BE((ulong*) ptr, 0x0123456789ABCDEF);
			Assert.That(ptr[0], Is.EqualTo(0x01));
			Assert.That(ptr[1], Is.EqualTo(0x23));
			Assert.That(ptr[2], Is.EqualTo(0x45));
			Assert.That(ptr[3], Is.EqualTo(0x67));
			Assert.That(ptr[4], Is.EqualTo(0x89));
			Assert.That(ptr[5], Is.EqualTo(0xAB));
			Assert.That(ptr[6], Is.EqualTo(0xCD));
			Assert.That(ptr[7], Is.EqualTo(0xEF));
			EnsureIsAllSameByte(buf, PAD, 0xAA);
			EnsureIsAllSameByte(buf + PAD + sizeof(ulong), CAPA - PAD - sizeof(ulong), 0xAA);
			Assert.That(UnsafeHelpers.LoadUInt64BE((ulong*) ptr), Is.EqualTo(0x0123456789ABCDEF));
		}

		[Test]
		public void Test_UnsafeHelpers_ComputeHashCode()
		{
			//note: if everything fails, check that the hashcode algorithm hasn't changed also !

			// managed

			Assert.That(UnsafeHelpers.ComputeHashCode(ReadOnlySpan<byte>.Empty), Is.EqualTo(-2128831035));
			Assert.That(UnsafeHelpers.ComputeHashCode(new ReadOnlySpan<byte>(new byte[1], 0, 1)), Is.EqualTo(84696351));
			Assert.That(UnsafeHelpers.ComputeHashCode(new ReadOnlySpan<byte>(new byte[2], 0, 1)), Is.EqualTo(84696351));
			Assert.That(UnsafeHelpers.ComputeHashCode(new ReadOnlySpan<byte>(new byte[2], 1, 1)), Is.EqualTo(84696351));
			Assert.That(UnsafeHelpers.ComputeHashCode(new ReadOnlySpan<byte>(new byte[2], 0, 2)), Is.EqualTo(292984781));
			Assert.That(UnsafeHelpers.ComputeHashCode(new ReadOnlySpan<byte>(Encoding.Default.GetBytes("hello"), 0, 5)), Is.EqualTo(1335831723));

			// unmanaged

			unsafe
			{
				Assert.That(UnsafeHelpers.ComputeHashCode(null, 0), Is.EqualTo(-2128831035));
				fixed (byte* ptr = new byte[1])
				{
					Assert.That(UnsafeHelpers.ComputeHashCode(ptr,  1), Is.EqualTo(84696351));
				}
				fixed (byte* ptr = new byte[2])
				{
					Assert.That(UnsafeHelpers.ComputeHashCode(ptr, 1), Is.EqualTo(84696351));
					Assert.That(UnsafeHelpers.ComputeHashCode(ptr + 1, 1), Is.EqualTo(84696351));
					Assert.That(UnsafeHelpers.ComputeHashCode(ptr, 2), Is.EqualTo(292984781));
				}
				fixed (byte* ptr = Encoding.Default.GetBytes("hello"))
				{
					Assert.That(UnsafeHelpers.ComputeHashCode(ptr, 5), Is.EqualTo(1335831723));
				}

				Assert.That(() => UnsafeHelpers.ComputeHashCode(null, 1), Throws.InstanceOf<ArgumentException>());
			}
		}

		#region VarInt

		[Test]
		public void Test_UnsafeHelpers_VarInt32()
		{
			void Verify(uint value, int size, string expected)
			{
				Assert.That(expected.Length, Is.EqualTo(size * 3 - 1));

				byte[] data = new byte[16];
				{
					Wipe(data);
					int cursor = UnsafeHelpers.WriteVarInt32(data, value);
					Assert.That(ToHex(data), Is.EqualTo(expected + string.Join("", Enumerable.Repeat(" 58", data.Length - size))));
					Assert.That(cursor, Is.EqualTo(size));

					cursor = UnsafeHelpers.ReadVarint32(data, out uint decoded);
					Assert.That(cursor, Is.EqualTo(size), $"Read({value} => {expected}) size");
					Assert.That(decoded, Is.EqualTo(value), $"Read({value} => {expected}) value");
				}
			}

			Verify(0, 1, "00");
			Verify(1, 1, "01");

			Verify(127, 1, "7F");

			Verify(1 << 7, 2, "80 01");
			Verify(255, 2, "FF 01");
			Verify(0x1234, 2, "B4 24");

			Verify(1 << 14, 3, "80 80 01");
			Verify(0xDEAD, 3, "AD BD 03");
			Verify(ushort.MaxValue, 3, "FF FF 03");
			Verify(0x123456, 3, "D6 E8 48");

			Verify(1 << 21, 4, "80 80 80 01");
			Verify(0x1234567, 4, "E7 8A 8D 09");

			Verify(1 << 28, 5, "80 80 80 80 01");
			Verify(0xDEADBEEF, 5, "EF FD B6 F5 0D");
			Verify(uint.MaxValue, 5, "FF FF FF FF 0F");

			//TODO: verify throws if value > 5 bytes
		}

		[Test]
		public void Test_UnsafeHelpers_VarInt64()
		{
			void Verify(ulong value, int size, string expected)
			{
				Assert.That(expected.Length, Is.EqualTo(size * 3 - 1));

				byte[] data = new byte[16];
				{
					Wipe(data);
					int cursor = UnsafeHelpers.WriteVarInt64(data, value);
					Assert.That(ToHex(data), Is.EqualTo(expected + string.Join("", Enumerable.Repeat(" 58", data.Length - size))));
					Assert.That(cursor, Is.EqualTo(size));

					cursor = UnsafeHelpers.ReadVarint64(data, out var decoded);
					Assert.That(cursor, Is.EqualTo(size), $"Read({value} => {expected}) size");
					Assert.That(decoded, Is.EqualTo(value), $"Read({value} => {expected}) value");

				}
			}

			Verify(0, 1, "00");
			Verify(1, 1, "01");

			Verify(127, 1, "7F");

			Verify(1UL << 7, 2, "80 01");
			Verify(255, 2, "FF 01");

			Verify(1UL << 14, 3, "80 80 01");
			Verify(0xDEAD, 3, "AD BD 03");
			Verify(ushort.MaxValue, 3, "FF FF 03");

			Verify(1UL << 21, 4, "80 80 80 01");

			Verify(1UL << 28, 5, "80 80 80 80 01");
			Verify(0xDEADBEEF, 5, "EF FD B6 F5 0D");
			Verify(uint.MaxValue, 5, "FF FF FF FF 0F");

			Verify(1UL << 35, 6, "80 80 80 80 80 01");
			Verify(1UL << 42, 7, "80 80 80 80 80 80 01");
			Verify(1UL << 49, 8, "80 80 80 80 80 80 80 01");
			Verify(1UL << 56, 9, "80 80 80 80 80 80 80 80 01");
			Verify(1UL << 63, 10, "80 80 80 80 80 80 80 80 80 01");

			Verify(0xBADC0FFEE0DDF00DUL, 10, "8D E0 F7 86 EE FF 83 EE BA 01");

			Verify(ulong.MaxValue, 10, "FF FF FF FF FF FF FF FF FF 01");

			//TODO: verify throws if value > 10 bytes
		}

		[Test]
		public void Test_UnsafeHelpers_SizeOfVarint()
		{
			// 32-bit
			Assert.That(UnsafeHelpers.SizeOfVarInt(0), Is.EqualTo(1));
			Assert.That(UnsafeHelpers.SizeOfVarInt(1), Is.EqualTo(1));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1 << 7) - 1), Is.EqualTo(1));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1 << 7)), Is.EqualTo(2));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1 << 7) + 1), Is.EqualTo(2));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1 << 14) - 1), Is.EqualTo(2));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1 << 14)), Is.EqualTo(3));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1 << 14) + 1), Is.EqualTo(3));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1 << 21) - 1), Is.EqualTo(3));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1 << 21)), Is.EqualTo(4));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1 << 21) + 1), Is.EqualTo(4));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1 << 28) - 1), Is.EqualTo(4));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1 << 28)), Is.EqualTo(5));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1 << 28) + 1), Is.EqualTo(5));
			Assert.That(UnsafeHelpers.SizeOfVarInt(uint.MaxValue), Is.EqualTo(5));

			// 64-bit
			Assert.That(UnsafeHelpers.SizeOfVarInt(0L), Is.EqualTo(1));
			Assert.That(UnsafeHelpers.SizeOfVarInt(1L), Is.EqualTo(1));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 7) - 1), Is.EqualTo(1));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 7)), Is.EqualTo(2));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 7) + 1), Is.EqualTo(2));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 14) - 1), Is.EqualTo(2));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 14)), Is.EqualTo(3));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 14) + 1), Is.EqualTo(3));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 21) - 1), Is.EqualTo(3));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 21)), Is.EqualTo(4));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 21) + 1), Is.EqualTo(4));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 28) - 1), Is.EqualTo(4));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 28)), Is.EqualTo(5));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 28) + 1), Is.EqualTo(5));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 35) - 1), Is.EqualTo(5));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 35)), Is.EqualTo(6));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 35) + 1), Is.EqualTo(6));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 42) - 1), Is.EqualTo(6));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 42)), Is.EqualTo(7));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 42) + 1), Is.EqualTo(7));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 49) - 1), Is.EqualTo(7));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 49)), Is.EqualTo(8));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 49) + 1), Is.EqualTo(8));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 56) - 1), Is.EqualTo(8));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 56)), Is.EqualTo(9));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 56) + 1), Is.EqualTo(9));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 63) - 1), Is.EqualTo(9));
			Assert.That(UnsafeHelpers.SizeOfVarInt((1UL << 63)), Is.EqualTo(10));
			Assert.That(UnsafeHelpers.SizeOfVarInt(ulong.MaxValue), Is.EqualTo(10));
		}

		[Test]
		public void Test_UnsafeHelpers_SizeOfVarBytes()
		{
			Assert.That(UnsafeHelpers.SizeOfVarBytes(0), Is.EqualTo(1 + 0));
			Assert.That(UnsafeHelpers.SizeOfVarBytes(1), Is.EqualTo(1 + 1));
			Assert.That(UnsafeHelpers.SizeOfVarBytes((1 << 7) - 1), Is.EqualTo(1 + ((1 << 7) - 1)));
			Assert.That(UnsafeHelpers.SizeOfVarBytes((1 << 7)), Is.EqualTo(2 + (1 << 7)));
			Assert.That(UnsafeHelpers.SizeOfVarBytes((1 << 7) + 1), Is.EqualTo(2 + (1 << 7) + 1));
			Assert.That(UnsafeHelpers.SizeOfVarBytes((1 << 14) - 1), Is.EqualTo(2 + ((1 << 14) - 1)));
			Assert.That(UnsafeHelpers.SizeOfVarBytes((1 << 14)), Is.EqualTo(3 + (1 << 14)));
			Assert.That(UnsafeHelpers.SizeOfVarBytes((1 << 14) + 1), Is.EqualTo(3 + ((1 << 14) + 1)));
			Assert.That(UnsafeHelpers.SizeOfVarBytes((1 << 21) - 1), Is.EqualTo(3 + ((1 << 21) - 1)));
			Assert.That(UnsafeHelpers.SizeOfVarBytes((1 << 21)), Is.EqualTo(4 + (1 << 21)));
			Assert.That(UnsafeHelpers.SizeOfVarBytes((1 << 21) + 1), Is.EqualTo(4 + ((1 << 21) + 1)));
			Assert.That(UnsafeHelpers.SizeOfVarBytes((1 << 28) - 1), Is.EqualTo(4 + ((1 << 28) - 1)));
			Assert.That(UnsafeHelpers.SizeOfVarBytes((1 << 28)), Is.EqualTo(5 + (1 << 28)));
			Assert.That(UnsafeHelpers.SizeOfVarBytes((1 << 28) + 1), Is.EqualTo(5 + ((1 << 28) + 1)));
			Assert.That(UnsafeHelpers.SizeOfVarBytes(uint.MaxValue - 5), Is.EqualTo(uint.MaxValue));
			// If size >= uint.MaxValue - 4, it should overflow
			Assert.That(() => UnsafeHelpers.SizeOfVarBytes(uint.MaxValue - 4), Throws.InstanceOf<OverflowException>());
			Assert.That(() => UnsafeHelpers.SizeOfVarBytes(uint.MaxValue - 3), Throws.InstanceOf<OverflowException>());
			Assert.That(() => UnsafeHelpers.SizeOfVarBytes(uint.MaxValue - 2), Throws.InstanceOf<OverflowException>());
			Assert.That(() => UnsafeHelpers.SizeOfVarBytes(uint.MaxValue - 1), Throws.InstanceOf<OverflowException>());
			Assert.That(() => UnsafeHelpers.SizeOfVarBytes(uint.MaxValue), Throws.InstanceOf<OverflowException>());
		}

		[Test]
		public void Test_UnsafeHelpers_VarBytes()
		{
			const int CAPA = 1024;
			byte[] data = new byte[CAPA];
			{
				{
					const string TXT = "Hello, world!";
					var bytes = Encoding.UTF8.GetBytes(TXT);
					{
						Wipe(data);

						int cursor = UnsafeHelpers.WriteVarBytes(data, bytes);
						Assert.That(cursor, Is.EqualTo(1 + bytes.Length));
						Assert.That(data[0], Is.EqualTo(bytes.Length));
						Assert.That(Encoding.UTF8.GetString(data.AsSpan(1, cursor - 1)), Is.EqualTo(TXT));

						var encoded = data.AsSpan(0, cursor);
						cursor = UnsafeHelpers.ReadVarBytes(encoded, out var res);
						Assert.That(cursor, Is.EqualTo(1 + bytes.Length));
						Assert.That(res.Length, Is.EqualTo(bytes.Length));
						Assert.That(res.ToStringUnicode(), Is.EqualTo(TXT));

						Wipe(data);
						cursor = UnsafeHelpers.WriteZeroTerminatedVarBytes(data, bytes);
						Assert.That(cursor, Is.EqualTo(1 + bytes.Length + 1));
						Assert.That(data[0], Is.EqualTo(bytes.Length + 1));
						Assert.That(Encoding.UTF8.GetString(data.AsSpan(1, cursor - 1)), Is.EqualTo(TXT + '\0'));
					}
				}
				{
					const string TXT = "This is a test of the emergency broadcast system to check if it is larger than 128 characters in order to have its length be encoded as a 2-byte VarInt. kthnxbye.";
					var bytes = Encoding.UTF8.GetBytes(TXT);
					{
						Wipe(data);
						int cursor = UnsafeHelpers.WriteVarBytes(data, bytes);
						Assert.That(cursor, Is.EqualTo(2 + bytes.Length));
						Assert.That(data[0], Is.EqualTo(0x80 | (bytes.Length & 0x7F)));
						Assert.That(data[1], Is.EqualTo((bytes.Length >> 7)));
						Assert.That(Encoding.UTF8.GetString(data.AsSpan(2, cursor - 2)), Is.EqualTo(TXT));

						var encoded = data.AsSpan(0, cursor);
						cursor = UnsafeHelpers.ReadVarBytes(encoded, out var res);
						Assert.That(res.Length, Is.EqualTo(bytes.Length));
						Assert.That(cursor, Is.EqualTo(2 + bytes.Length));
						Assert.That(res.ToStringUtf8(), Is.EqualTo(TXT));

						Wipe(data);
						cursor = UnsafeHelpers.WriteZeroTerminatedVarBytes(data, bytes);
						Assert.That(cursor, Is.EqualTo(2 + bytes.Length + 1));
						Assert.That(data[0], Is.EqualTo(bytes.Length + 1));
						Assert.That(Encoding.UTF8.GetString(data.AsSpan(2, cursor - 2)), Is.EqualTo(TXT + '\0'));
					}
				}
			}
		}

		#endregion

		#region Compact Unordered

		[Test]
		public void Test_UnsafeHelpers_Compact16_SizeOf()
		{
			Assert.That(UnsafeHelpers.SizeOfCompact16(0), Is.EqualTo(1));
			Assert.That(UnsafeHelpers.SizeOfCompact16(0xFF), Is.EqualTo(1));
			Assert.That(UnsafeHelpers.SizeOfCompact16(0x100), Is.EqualTo(2));
			Assert.That(UnsafeHelpers.SizeOfCompact16(0xFFFF), Is.EqualTo(2));
		}

		[Test]
		public void Test_UnsafeHelpers_Compact32_SizeOf()
		{
			Assert.That(UnsafeHelpers.SizeOfCompact32(0U), Is.EqualTo(1));
			Assert.That(UnsafeHelpers.SizeOfCompact32(0xFFU), Is.EqualTo(1));
			Assert.That(UnsafeHelpers.SizeOfCompact32(0x100U), Is.EqualTo(2));
			Assert.That(UnsafeHelpers.SizeOfCompact32(0xFFFFU), Is.EqualTo(2));
			Assert.That(UnsafeHelpers.SizeOfCompact32(0x10000U), Is.EqualTo(3));
			Assert.That(UnsafeHelpers.SizeOfCompact32(0xFFFFFFU), Is.EqualTo(3));
			Assert.That(UnsafeHelpers.SizeOfCompact32(0x1000000U), Is.EqualTo(4));
			Assert.That(UnsafeHelpers.SizeOfCompact32(0xFFFFFFFFU), Is.EqualTo(4));
		}

		[Test]
		public void Test_UnsafeHelpers_Compact64_SizeOf()
		{
			Assert.That(UnsafeHelpers.SizeOfCompact64(0UL), Is.EqualTo(1));
			Assert.That(UnsafeHelpers.SizeOfCompact64(0xFFUL), Is.EqualTo(1));
			Assert.That(UnsafeHelpers.SizeOfCompact64(0x100UL), Is.EqualTo(2));
			Assert.That(UnsafeHelpers.SizeOfCompact64(0xFFFFUL), Is.EqualTo(2));
			Assert.That(UnsafeHelpers.SizeOfCompact64(0x10000UL), Is.EqualTo(3));
			Assert.That(UnsafeHelpers.SizeOfCompact64(0xFFFFFFUL), Is.EqualTo(3));
			Assert.That(UnsafeHelpers.SizeOfCompact64(0x1000000UL), Is.EqualTo(4));
			Assert.That(UnsafeHelpers.SizeOfCompact64(0xFFFFFFFFUL), Is.EqualTo(4));
			Assert.That(UnsafeHelpers.SizeOfCompact64(0x100000000UL), Is.EqualTo(5));
			Assert.That(UnsafeHelpers.SizeOfCompact64(0xFFFFFFFFFFUL), Is.EqualTo(5));
			Assert.That(UnsafeHelpers.SizeOfCompact64(0x10000000000UL), Is.EqualTo(6));
			Assert.That(UnsafeHelpers.SizeOfCompact64(0xFFFFFFFFFFFFUL), Is.EqualTo(6));
			Assert.That(UnsafeHelpers.SizeOfCompact64(0x1000000000000UL), Is.EqualTo(7));
			Assert.That(UnsafeHelpers.SizeOfCompact64(0xFFFFFFFFFFFFFFUL), Is.EqualTo(7));
			Assert.That(UnsafeHelpers.SizeOfCompact64(0x100000000000000UL), Is.EqualTo(8));
			Assert.That(UnsafeHelpers.SizeOfCompact64(0xFFFFFFFFFFFFFFFFUL), Is.EqualTo(8));
		}

		[Test]
		public void Test_UnsafeHelpers_Compact32_Write()
		{
			byte[] data = new byte[8];
			fixed (byte* ptr = &data[0])
			{
				byte* ptr2;

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact32Unsafe(ptr, 0x12);
				Assert.That(ToHex(data), Is.EqualTo("12 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 1)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact32Unsafe(ptr, 0x01234567);
				Assert.That(ToHex(data), Is.EqualTo("67 45 23 01 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 4)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact32Unsafe(ptr + 2, 0x01234567);
				Assert.That(ToHex(data), Is.EqualTo("58 58 67 45 23 01 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 2 + 4)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact32Unsafe(ptr, 0);
				Assert.That(ToHex(data), Is.EqualTo("00 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 1)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact32Unsafe(ptr, 0xFF);
				Assert.That(ToHex(data), Is.EqualTo("FF 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 1)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact32Unsafe(ptr, 0x100);
				Assert.That(ToHex(data), Is.EqualTo("00 01 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 2)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact32Unsafe(ptr, 0xFFFF);
				Assert.That(ToHex(data), Is.EqualTo("FF FF 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 2)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact32Unsafe(ptr, 0x10000);
				Assert.That(ToHex(data), Is.EqualTo("00 00 01 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 3)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact32Unsafe(ptr, 0xFFFFFF);
				Assert.That(ToHex(data), Is.EqualTo("FF FF FF 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 3)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact32Unsafe(ptr, 0x1000000);
				Assert.That(ToHex(data), Is.EqualTo("00 00 00 01 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 4)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact32Unsafe(ptr, 0xFFFFFFFF);
				Assert.That(ToHex(data), Is.EqualTo("FF FF FF FF 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 4)));

			}
		}

		[Test]
		public void Test_UnsafeHelpers_Compact32BE_Write()
		{
			byte[] data = new byte[8];
			fixed (byte* ptr = &data[0])
			{
				byte* ptr2;

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact32BEUnsafe(ptr, 0x12);
				Assert.That(ToHex(data), Is.EqualTo("12 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 1)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact32BEUnsafe(ptr, 0x01234567);
				Assert.That(ToHex(data), Is.EqualTo("01 23 45 67 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 4)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact32BEUnsafe(ptr + 2, 0x01234567);
				Assert.That(ToHex(data), Is.EqualTo("58 58 01 23 45 67 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 2 + 4)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact32BEUnsafe(ptr, 0);
				Assert.That(ToHex(data), Is.EqualTo("00 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 1)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact32BEUnsafe(ptr, 0xFF);
				Assert.That(ToHex(data), Is.EqualTo("FF 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 1)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact32BEUnsafe(ptr, 0x100);
				Assert.That(ToHex(data), Is.EqualTo("01 00 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 2)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact32BEUnsafe(ptr, 0xFFFF);
				Assert.That(ToHex(data), Is.EqualTo("FF FF 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 2)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact32BEUnsafe(ptr, 0x10000);
				Assert.That(ToHex(data), Is.EqualTo("01 00 00 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 3)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact32BEUnsafe(ptr, 0xFFFFFF);
				Assert.That(ToHex(data), Is.EqualTo("FF FF FF 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 3)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact32BEUnsafe(ptr, 0x1000000);
				Assert.That(ToHex(data), Is.EqualTo("01 00 00 00 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 4)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact32BEUnsafe(ptr, 0xFFFFFFFF);
				Assert.That(ToHex(data), Is.EqualTo("FF FF FF FF 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 4)));
			}
		}

		[Test]
		public void Test_UnsafeHelpers_Compact64_Write()
		{
			byte[] data = new byte[16];
			fixed (byte* ptr = &data[0])
			{
				byte* ptr2;

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0);
				Assert.That(ToHex(data), Is.EqualTo("00 58 58 58 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 1)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0x12);
				Assert.That(ToHex(data), Is.EqualTo("12 58 58 58 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 1)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0xFF);
				Assert.That(ToHex(data), Is.EqualTo("FF 58 58 58 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 1)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0x100);
				Assert.That(ToHex(data), Is.EqualTo("00 01 58 58 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 2)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0x1234);
				Assert.That(ToHex(data), Is.EqualTo("34 12 58 58 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 2)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0xFFFF);
				Assert.That(ToHex(data), Is.EqualTo("FF FF 58 58 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 2)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0x10000);
				Assert.That(ToHex(data), Is.EqualTo("00 00 01 58 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 3)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0x123456);
				Assert.That(ToHex(data), Is.EqualTo("56 34 12 58 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 3)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0xFFFFFF);
				Assert.That(ToHex(data), Is.EqualTo("FF FF FF 58 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 3)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0x1000000);
				Assert.That(ToHex(data), Is.EqualTo("00 00 00 01 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 4)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0x12345678);
				Assert.That(ToHex(data), Is.EqualTo("78 56 34 12 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 4)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0xFFFFFFFF);
				Assert.That(ToHex(data), Is.EqualTo("FF FF FF FF 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 4)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0x100000000);
				Assert.That(ToHex(data), Is.EqualTo("00 00 00 00 01 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 5)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0x123456789A);
				Assert.That(ToHex(data), Is.EqualTo("9A 78 56 34 12 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 5)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0xFFFFFFFFFF);
				Assert.That(ToHex(data), Is.EqualTo("FF FF FF FF FF 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 5)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0x10000000000);
				Assert.That(ToHex(data), Is.EqualTo("00 00 00 00 00 01 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 6)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0x123456789ABC);
				Assert.That(ToHex(data), Is.EqualTo("BC 9A 78 56 34 12 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 6)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0xFFFFFFFFFFFF);
				Assert.That(ToHex(data), Is.EqualTo("FF FF FF FF FF FF 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 6)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0x1000000000000);
				Assert.That(ToHex(data), Is.EqualTo("00 00 00 00 00 00 01 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 7)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0x123456789ABCDE);
				Assert.That(ToHex(data), Is.EqualTo("DE BC 9A 78 56 34 12 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 7)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0xFFFFFFFFFFFFFF);
				Assert.That(ToHex(data), Is.EqualTo("FF FF FF FF FF FF FF 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 7)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0x100000000000000);
				Assert.That(ToHex(data), Is.EqualTo("00 00 00 00 00 00 00 01 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 8)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0x123456789ABCDEF0);
				Assert.That(ToHex(data), Is.EqualTo("F0 DE BC 9A 78 56 34 12 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 8)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64Unsafe(ptr, 0xFFFFFFFFFFFFFFFF);
				Assert.That(ToHex(data), Is.EqualTo("FF FF FF FF FF FF FF FF 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 8)));
			}
		}

		[Test]
		public void Test_UnsafeHelpers_Compact64BE_Write()
		{
			byte[] data = new byte[16];
			fixed (byte* ptr = &data[0])
			{
				byte* ptr2;

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0);
				Assert.That(ToHex(data), Is.EqualTo("00 58 58 58 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 1)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0x12);
				Assert.That(ToHex(data), Is.EqualTo("12 58 58 58 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 1)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0xFF);
				Assert.That(ToHex(data), Is.EqualTo("FF 58 58 58 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 1)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0x100);
				Assert.That(ToHex(data), Is.EqualTo("01 00 58 58 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 2)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0x1234);
				Assert.That(ToHex(data), Is.EqualTo("12 34 58 58 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 2)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0xFFFF);
				Assert.That(ToHex(data), Is.EqualTo("FF FF 58 58 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 2)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0x10000);
				Assert.That(ToHex(data), Is.EqualTo("01 00 00 58 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 3)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0x123456);
				Assert.That(ToHex(data), Is.EqualTo("12 34 56 58 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 3)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0xFFFFFF);
				Assert.That(ToHex(data), Is.EqualTo("FF FF FF 58 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 3)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0x1000000);
				Assert.That(ToHex(data), Is.EqualTo("01 00 00 00 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 4)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0x12345678);
				Assert.That(ToHex(data), Is.EqualTo("12 34 56 78 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 4)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0xFFFFFFFF);
				Assert.That(ToHex(data), Is.EqualTo("FF FF FF FF 58 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 4)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0x100000000);
				Assert.That(ToHex(data), Is.EqualTo("01 00 00 00 00 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 5)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0x123456789A);
				Assert.That(ToHex(data), Is.EqualTo("12 34 56 78 9A 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 5)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0xFFFFFFFFFF);
				Assert.That(ToHex(data), Is.EqualTo("FF FF FF FF FF 58 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 5)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0x10000000000);
				Assert.That(ToHex(data), Is.EqualTo("01 00 00 00 00 00 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 6)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0x123456789ABC);
				Assert.That(ToHex(data), Is.EqualTo("12 34 56 78 9A BC 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 6)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0xFFFFFFFFFFFF);
				Assert.That(ToHex(data), Is.EqualTo("FF FF FF FF FF FF 58 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 6)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0x1000000000000);
				Assert.That(ToHex(data), Is.EqualTo("01 00 00 00 00 00 00 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 7)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0x123456789ABCDE);
				Assert.That(ToHex(data), Is.EqualTo("12 34 56 78 9A BC DE 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 7)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0xFFFFFFFFFFFFFF);
				Assert.That(ToHex(data), Is.EqualTo("FF FF FF FF FF FF FF 58 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 7)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0x100000000000000);
				Assert.That(ToHex(data), Is.EqualTo("01 00 00 00 00 00 00 00 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 8)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0x123456789ABCDEF0);
				Assert.That(ToHex(data), Is.EqualTo("12 34 56 78 9A BC DE F0 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 8)));

				Wipe(data); ptr2 = UnsafeHelpers.WriteCompact64BEUnsafe(ptr, 0xFFFFFFFFFFFFFFFF);
				Assert.That(ToHex(data), Is.EqualTo("FF FF FF FF FF FF FF FF 58 58 58 58 58 58 58 58"));
				Assert.That((long) ptr2, Is.EqualTo((long) (ptr + 8)));


			}
		}

		#endregion

		#region Compact Ordered

		[Test]
		public void Test_UnsafeHelpers_CompactOrderedUInt32()
		{
			byte[] tmp = new byte[1024]; // only 5 needed in practice
			fixed (byte* ptr = &tmp[0])
			{
				// test around all the powers of 2
				for (int k = 0; k <= 32; k++)
				{
					for (int i = -1; i <= 1; i++)
					{
						if ((k == 0 && i < -1) || (k == 32 && i >= 0)) continue;

						tmp.AsSpan(0, 16).Clear();
						uint x = (uint)((1L << k) + i);
						byte* c1 = UnsafeHelpers.WriteOrderedUInt32Unsafe(ptr, x);
						var res = new ReadOnlySpan<byte>(ptr, (int)(c1 - ptr));
						Log($"x = 1 << {k,2} {i:+ 0;- 0;'   '} = 0x{x:X8} = {x,10} => ({res.Length}) {res.ToString("X")}");
						Assert.That(c1 - ptr, Is.GreaterThan(0).And.LessThanOrEqualTo(5), "Should emit between 1 and 5 bytes");
						Assert.That(UnsafeHelpers.SizeOfOrderedUInt32(x), Is.EqualTo(res.Length), $"SizeOf({x}) does not match encoded size");

						uint y;
						byte* c2 = UnsafeHelpers.ReadOrderedUInt32Unsafe(ptr, out y);
						if (y != x) Assert.Fail($"== FAILED: {y} != {x}");
						if (c2 != c1) Assert.Fail($"== FAILED: read {c2 - ptr} instead of {c1 - ptr}");
					}
				}

				// test a bunch of random values
				var rnd = new Random(12345);
				var xs = Enumerable.Range(0, 1000).Select(_ => (uint) (uint.MaxValue * Math.Pow(rnd.NextDouble(), 6))).Distinct().ToArray();
				Array.Sort(xs);
				string? prev = null;
				for (int i = 0; i < xs.Length; i++)
				{
					// write
					Array.Clear(tmp, 0, 16);
					uint x = xs[i];
					byte* c1 = UnsafeHelpers.WriteOrderedUInt32Unsafe(ptr, x);
					var res = new ReadOnlySpan<byte>(ptr, (int)(c1 - ptr));
					Assert.That(c1 - ptr, Is.GreaterThan(0).And.LessThanOrEqualTo(5), "Should emit between 1 and 5 bytes");
					Assert.That(UnsafeHelpers.SizeOfOrderedUInt32(x), Is.EqualTo(res.Length), $"SizeOf({x} does not match encoded size");

					// read it back
					uint y;
					byte* c2 = UnsafeHelpers.ReadOrderedUInt32Unsafe(ptr, out y);
					//Log($"{x} => {new USlice(ptr, (uint)(c1 - ptr)):X} => {y}");
					if (y != x) Assert.Fail($"== FAILED: {y} != {x}");
					if (c2 != c1) Assert.Fail($"== FAILED: read {c2 - ptr} instead of {c1 - ptr}");

					// should be > the previous one
					string s = res.ToString("X");
					if (prev != null) Assert.That(s, Is.GreaterThan(prev), $"Encoded({x}) should be greater than Encoded({xs[i - 1]})");
					prev = s;
				}
			}
		}

		[Test]
		public void Test_UnsafeHelpers_CompactOrderedUInt64()
		{
			byte[] tmp = new byte[1024]; // only 8 needed in practice
			fixed (byte* ptr = &tmp[0])
			{
				// test around all the powers of 2
				for (int k = 0; k <= 61; k++)
				{
					for (int i = -1; i <= 1; i++)
					{
						if ((k == 0 && i < -1) || (k == 61 && i >= 0)) continue;

						tmp.AsSpan(0, 16).Clear();
						ulong x = (ulong)((1L << k) + i);
						byte* c1 = UnsafeHelpers.WriteOrderedUInt64Unsafe(ptr, x);
						var res = new ReadOnlySpan<byte>(ptr, (int)(c1 - ptr));
						Log($"x = 1 << {k,2} {i:+ 0;- 0;'   '} = 0x{x:X16} = {x,19} => ({res.Length}) {res.ToString("X")}");
						Assert.That(c1 - ptr, Is.GreaterThan(0).And.LessThanOrEqualTo(8), "Should emit between 1 and 8 bytes");
						Assert.That(UnsafeHelpers.SizeOfOrderedUInt64(x), Is.EqualTo(res.Length), $"SizeOf({x}) does not match encoded size");

						ulong y;
						byte* c2 = UnsafeHelpers.ReadOrderedUInt64Unsafe(ptr, out y);
						if (y != x) Assert.Fail($"== FAILED: {y} != {x} : {res.ToString("X")}");
						if (c2 != c1) Assert.Fail($"== FAILED: read {c2 - ptr} instead of {c1 - ptr} : {res.ToString("X")}");
					}
				}

				// test a bunch of random values
				var rnd = new Random(12345);
				var xs = Enumerable.Range(0, 1000).Select(_ => (ulong)((1UL << 61) * Math.Pow(rnd.NextDouble(), 6))).Distinct().ToArray();
				Array.Sort(xs);
				string? prev = null;
				for (int i = 0; i < xs.Length; i++)
				{
					// write
					Array.Clear(tmp, 0, 16);
					ulong x = xs[i];
					byte* c1 = UnsafeHelpers.WriteOrderedUInt64Unsafe(ptr, x);
					var res = new ReadOnlySpan<byte>(ptr, (int) (c1 - ptr));
					Assert.That(c1 - ptr, Is.GreaterThan(0).And.LessThanOrEqualTo(8), "Should emit between 1 and 8 bytes");
					Assert.That(UnsafeHelpers.SizeOfOrderedUInt64(x), Is.EqualTo(res.Length), $"SizeOf({x}) does not match encoded size");

					// read it back
					ulong y;
					byte* c2 = UnsafeHelpers.ReadOrderedUInt64Unsafe(ptr, out y);
					//Log($"{x} => {res:X} => {y}");
					if (y != x) Assert.Fail($"{y} != {x} : {res.PrettyPrint()}");
					if (c2 != c1) Assert.Fail($"read {c2 - ptr} bytes instead of {c1 - ptr} for x={x} : {res.PrettyPrint()}");

					// should be > the previous one
					string s = res.ToString("X");
					if (prev != null) Assert.That(s, Is.GreaterThan(prev), $"Encoded({x}) should be greater than Encoded({xs[i - 1]})");
					prev = s;
				}
			}
		}

		#endregion

	}

}
