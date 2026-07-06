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

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class SliceReaderFacts : SimpleTest
	{

		[Test]
		public void Test_ToSliceReader()
		{
			{
				var reader = Slice.Empty.ToSliceReader();
				Assert.That(reader.Position, Is.EqualTo(0));
				Assert.That(reader.Buffer, Is.EqualTo(Slice.Empty));
				Assert.That(reader.Remaining, Is.EqualTo(0));
				Assert.That(reader.HasMore, Is.False);
				Assert.That(reader.Head, Is.EqualTo(Slice.Empty));
				Assert.That(reader.Tail, Is.EqualTo(Slice.Empty));
			}
			{
				var reader = Slice.FromString("Hello World").ToSliceReader();
				Assert.That(reader.Position, Is.EqualTo(0));
				Assert.That(reader.Buffer, Is.EqualTo(Slice.FromString("Hello World")));
				Assert.That(reader.Buffer.Offset, Is.EqualTo(0));
				Assert.That(reader.Buffer.Count, Is.EqualTo(11));
				Assert.That(reader.Remaining, Is.EqualTo(11));
				Assert.That(reader.HasMore, Is.True);
				Assert.That(reader.Head, Is.EqualTo(Slice.Empty));
				Assert.That(reader.Tail, Is.EqualTo(Slice.FromString("Hello World")));
			}
			{
				var reader = Slice.FromString("XxXxHello WorldxXxX").Substring(4, 11).ToSliceReader();
				Assert.That(reader.Position, Is.EqualTo(0));
				Assert.That(reader.Buffer, Is.EqualTo(Slice.FromString("Hello World")));
				Assert.That(reader.Buffer.Offset, Is.EqualTo(4));
				Assert.That(reader.Buffer.Count, Is.EqualTo(11));
				Assert.That(reader.Remaining, Is.EqualTo(11));
				Assert.That(reader.HasMore, Is.True);
				Assert.That(reader.Head, Is.EqualTo(Slice.Empty));
				Assert.That(reader.Tail, Is.EqualTo(Slice.FromString("Hello World")));
			}
		}

		[Test]
		public void Test_ReadByte()
		{
			var data = Slice.FromString("ABCD");
			var reader = data.ToSliceReader();
			Assert.That(reader.ReadByte(), Is.EqualTo('A'));
			Assert.That(reader.Position, Is.EqualTo(1));
			Assert.That(reader.ReadByte(), Is.EqualTo('B'));
			Assert.That(reader.Position, Is.EqualTo(2));
			Assert.That(reader.ReadByte(), Is.EqualTo('C'));
			Assert.That(reader.Position, Is.EqualTo(3));
			Assert.That(reader.ReadByte(), Is.EqualTo('D'));
			Assert.That(reader.Position, Is.EqualTo(4));
			Assert.That(() => reader.ReadByte(), Throws.InstanceOf<FormatException>());
			Assert.That(reader.Position, Is.EqualTo(4));
		}

		[Test]
		public void Test_ReadBytes()
		{
			var data = Slice.FromString("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
			var reader = data.ToSliceReader();
			Assert.That(reader.ReadBytes(4), Is.EqualTo(Slice.FromString("ABCD")));
			Assert.That(reader.Position, Is.EqualTo(4));
			Assert.That(reader.ReadBytes(4), Is.EqualTo(Slice.FromString("EFGH")));
			Assert.That(reader.Position, Is.EqualTo(8));
			Assert.That(reader.ReadBytes(1), Is.EqualTo(Slice.FromString("I")));
			Assert.That(reader.Position, Is.EqualTo(9));
			Assert.That(reader.ReadBytes(2), Is.EqualTo(Slice.FromString("JK")));
			Assert.That(reader.Position, Is.EqualTo(11));
			Assert.That(reader.ReadBytes(0), Is.EqualTo(Slice.Empty));
			Assert.That(reader.Position, Is.EqualTo(11));
			Assert.That(reader.ReadBytes(14), Is.EqualTo(Slice.FromString("LMNOPQRSTUVWXY")));
			Assert.That(reader.Position, Is.EqualTo(25));
			Assert.That(() => reader.ReadBytes(2), Throws.InstanceOf<FormatException>());
			Assert.That(reader.Position, Is.EqualTo(25));
			Assert.That(reader.ReadBytes(1), Is.EqualTo(Slice.FromString("Z")));
			Assert.That(reader.Position, Is.EqualTo(26));
			Assert.That(reader.ReadBytes(0), Is.EqualTo(Slice.Empty));
			Assert.That(reader.Position, Is.EqualTo(26));
			Assert.That(() => reader.ReadBytes(1), Throws.InstanceOf<FormatException>());
			Assert.That(reader.Position, Is.EqualTo(26));
			Assert.That(() => reader.ReadBytes(-4), Throws.InstanceOf<FormatException>());
			Assert.That(reader.Position, Is.EqualTo(26));
		}

		[Test]
		public void Test_ReadFixed16()
		{
			var data = Slice.FromString("01234");
			var reader = data.ToSliceReader();
			Assert.That(reader.ReadUInt16(), Is.EqualTo(0x3130));
			Assert.That(reader.Position, Is.EqualTo(2));
			Assert.That(reader.ReadUInt16BE(), Is.EqualTo(0x3233));
			Assert.That(reader.Position, Is.EqualTo(4));
			Assert.That(() => reader.ReadUInt16(), Throws.InstanceOf<FormatException>());
			Assert.That(reader.Position, Is.EqualTo(4));
			Assert.That(reader.Tail, Is.EqualTo(Slice.FromString("4")));
		}

		[Test]
		public void Test_ReadFixed24()
		{
			var data = Slice.FromString("01234567");
			var reader = data.ToSliceReader();
			Assert.That(reader.ReadUInt24(), Is.EqualTo(0x323130));
			Assert.That(reader.Position, Is.EqualTo(3));
			Assert.That(reader.ReadUInt24BE(), Is.EqualTo(0x333435));
			Assert.That(reader.Position, Is.EqualTo(6));
			Assert.That(() => reader.ReadUInt24(), Throws.InstanceOf<FormatException>());
			Assert.That(reader.Position, Is.EqualTo(6));
			Assert.That(reader.Tail, Is.EqualTo(Slice.FromString("67")));
		}

		[Test]
		public void Test_ReadFixed32()
		{
			var data = Slice.FromString("0123456789");
			var reader = data.ToSliceReader();
			Assert.That(reader.ReadUInt32(), Is.EqualTo(0x33323130));
			Assert.That(reader.Position, Is.EqualTo(4));
			Assert.That(reader.ReadUInt32BE(), Is.EqualTo(0x34353637));
			Assert.That(reader.Position, Is.EqualTo(8));
			Assert.That(() => reader.ReadUInt32(), Throws.InstanceOf<FormatException>());
			Assert.That(reader.Position, Is.EqualTo(8));
			Assert.That(reader.Tail, Is.EqualTo(Slice.FromString("89")));
		}

		[Test]
		public void Test_ReadFixed64()
		{
			var data = Slice.FromString("0123456789ABCDEFGH");
			var reader = data.ToSliceReader();
			Assert.That(reader.ReadUInt64(), Is.EqualTo(0x3736353433323130));
			Assert.That(reader.Position, Is.EqualTo(8));
			Assert.That(reader.ReadUInt64BE(), Is.EqualTo(0x3839414243444546));
			Assert.That(reader.Position, Is.EqualTo(16));
			Assert.That(() => reader.ReadUInt64(), Throws.InstanceOf<FormatException>());
			Assert.That(reader.Position, Is.EqualTo(16));
			Assert.That(reader.Tail, Is.EqualTo(Slice.FromString("GH")));
		}

#if NET8_0_OR_GREATER

		[Test]
		public void Test_ReadFixed128()
		{
			var data = Slice.FromString("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdef");
			var reader = data.ToSliceReader();
			Assert.That(reader.ReadUInt128(), Is.EqualTo(new UInt128(0x4645444342413938, 0x3736353433323130)));
			Assert.That(reader.Position, Is.EqualTo(16));
			Assert.That(reader.ReadUInt128BE(), Is.EqualTo(new UInt128(0x4748494A4B4C4D4E, 0x4F50515253545556)));
			Assert.That(reader.Position, Is.EqualTo(32));
			Assert.That(() => reader.ReadUInt128(), Throws.InstanceOf<FormatException>());
			Assert.That(reader.Position, Is.EqualTo(32));
			Assert.That(reader.Tail, Is.EqualTo(Slice.FromString("WXYZabcdef")));

			// Try* variants must not over-read: a window shorter than 16 bytes must fail cleanly, without reading past the slice
			{
				var r15 = data.Substring(0, 15).ToSliceReader(); // 15-byte window inside a larger buffer
				Assert.That(r15.TryReadUInt128(out _), Is.False, "TryReadUInt128 requires 16 bytes");
				Assert.That(r15.Position, Is.EqualTo(0), "a failed TryReadUInt128 must not advance the cursor");
				Assert.That(r15.TryReadInt128(out _), Is.False, "TryReadInt128 requires 16 bytes");

				var r16 = data.Substring(0, 16).ToSliceReader();
				Assert.That(r16.TryReadUInt128(out var v128), Is.True, "16 bytes is enough for a UInt128");
				Assert.That(v128, Is.EqualTo(new UInt128(0x4645444342413938, 0x3736353433323130)));
				Assert.That(r16.Position, Is.EqualTo(16));
			}
		}

#endif

		[Test]
		public void Test_ReadUuid64()
		{
			var data = Slice.FromString("0123456789ABCDEFGH");
			var reader = data.ToSliceReader();
			Assert.That(reader.ReadUuid64().ToString(), Is.EqualTo("30313233-34353637"));
			Assert.That(reader.Position, Is.EqualTo(8));
			Assert.That(reader.ReadUuid64().ToString(), Is.EqualTo("38394142-43444546"));
			Assert.That(reader.Position, Is.EqualTo(16));
			Assert.That(() => reader.ReadUuid64(), Throws.InstanceOf<FormatException>());
			Assert.That(reader.Position, Is.EqualTo(16));
			Assert.That(reader.Tail, Is.EqualTo(Slice.FromString("GH")));
		}

		[Test]
		public void Test_ReadUuid128()
		{
			var data = Slice.FromString("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ");
			var reader = data.ToSliceReader();
			Assert.That(reader.ReadUuid128().ToString(), Is.EqualTo("30313233-3435-3637-3839-414243444546"));
			Assert.That(reader.Position, Is.EqualTo(16));
			Assert.That(reader.ReadUuid128().ToString(), Is.EqualTo("4748494a-4b4c-4d4e-4f50-515253545556"));
			Assert.That(reader.Position, Is.EqualTo(32));
			Assert.That(() => reader.ReadUuid128(), Throws.InstanceOf<FormatException>());
			Assert.That(reader.Position, Is.EqualTo(32));
			Assert.That(reader.Tail, Is.EqualTo(Slice.FromString("WXYZ")));
		}

		[Test]
		public void Test_ReadVarInt()
		{
			// valid varints
			Assert.That(Slice.FromBytes([ 0x00 ]).ToSliceReader().ReadVarInt32(), Is.EqualTo(0u));
			Assert.That(Slice.FromBytes([ 0x7F ]).ToSliceReader().ReadVarInt32(), Is.EqualTo(127u));
			Assert.That(Slice.FromBytes([ 0x80, 0x01 ]).ToSliceReader().ReadVarInt32(), Is.EqualTo(128u));
			{
				var reader = Slice.FromBytes([ 0x80, 0x01, 0x2A ]).ToSliceReader();
				Assert.That(reader.ReadVarInt32(), Is.EqualTo(128u));
				Assert.That(reader.Position, Is.EqualTo(2), "must consume exactly the varint bytes");
				Assert.That(reader.ReadByte(), Is.EqualTo(0x2A));
			}

			// a varint whose continuation runs to the end of the slice is truncated and must fail, without reading past the slice.
			// the window is just [0x80]; the following bytes (which would complete the varint) are OUTSIDE the window.
			{
				var truncated = Slice.FromBytes([ 0x80, 0x01, 0x02, 0x03, 0x04 ]).Substring(0, 1);
				Assert.That(() => truncated.ToSliceReader().ReadVarInt32(), Throws.InstanceOf<FormatException>(), "truncated varint must not read past the slice");

				var reader = truncated.ToSliceReader();
				Assert.That(reader.TryReadVarInt32(out _), Is.False, "TryReadVarInt32 must fail on a truncated varint");
				Assert.That(reader.Position, Is.EqualTo(0), "a failed TryReadVarInt32 must not advance the cursor");
			}
		}

		[Test]
		public void Test_TryPeek()
		{
			// exactly 2 bytes remaining must still be peekable as a 16-bit integer
			{
				var reader = Slice.FromString("AB").ToSliceReader(); // 'A'=0x41, 'B'=0x42
				Assert.That(reader.TryPeekUInt16(out var u16), Is.True, "2 bytes should be peekable as a UInt16");
				Assert.That(u16, Is.EqualTo(0x4241));
				Assert.That(reader.TryPeekInt16(out _), Is.True);
				Assert.That(reader.TryPeekUInt16BE(out var u16be), Is.True);
				Assert.That(u16be, Is.EqualTo(0x4142));
				Assert.That(reader.Position, Is.EqualTo(0), "peek must not advance the cursor");
			}

			// only 1 byte remaining: a 16-bit peek must fail
			Assert.That(Slice.FromString("A").ToSliceReader().TryPeekUInt16(out _), Is.False);

			// 32/64-bit peeks at their exact boundary
			Assert.That(Slice.FromString("ABCD").ToSliceReader().TryPeekUInt32(out var u32), Is.True);
			Assert.That(u32, Is.EqualTo(0x44434241));
			Assert.That(Slice.FromString("ABC").ToSliceReader().TryPeekUInt32(out _), Is.False);
			Assert.That(Slice.FromString("01234567").ToSliceReader().TryPeekUInt64(out _), Is.True);
			Assert.That(Slice.FromString("0123456").ToSliceReader().TryPeekUInt64(out _), Is.False);
		}

	}

}
