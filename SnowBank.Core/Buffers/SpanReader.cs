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

namespace SnowBank.Buffers
{
	using System.Buffers.Binary;
	using System.Text;

	/// <summary>Helper type for ready binary data from a <see cref="Span{T}"/> of bytes</summary>
	[DebuggerDisplay("Pos={Position}/{Buffer.Length}, Remaining={Remaining}")]
	[PublicAPI]
	[DebuggerNonUserCode]
	public ref struct SpanReader
	{

		/// <summary>Buffer containing the bytes to read</summary>
		public readonly ReadOnlySpan<byte> Buffer;

		/// <summary>Current position inside the buffer</summary>
		public int Position;

		/// <summary>Constructs a <see cref="SpanReader"/> that will read from a place in memory</summary>
		/// <param name="buffer">Slice that will be used as the underlying buffer</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public SpanReader(in ReadOnlyMemory<byte> buffer)
		{
			this.Buffer = buffer.Span;
			this.Position = 0;
		}

		/// <summary>Constructs a <see cref="SpanReader"/> that will read from a span of bytes in memory</summary>
		/// <param name="buffer">Span of bytes that will be used as the underlying buffer</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public SpanReader(ReadOnlySpan<byte> buffer)
		{
			this.Buffer = buffer;
			this.Position = 0;
		}

		/// <summary>Returns true if there are more bytes to parse</summary>
		public readonly bool HasMore => this.Position < this.Buffer.Length;

		/// <summary>Returns the number of bytes remaining</summary>
		public readonly int Remaining => Math.Max(0, this.Buffer.Length - this.Position);

		/// <summary>Returns a slice with all the bytes read so far in the buffer</summary>
		public readonly ReadOnlySpan<byte> Head => this.Buffer[..this.Position];

		/// <summary>Returns a slice with all the remaining bytes in the buffer</summary>
		public readonly ReadOnlySpan<byte> Tail => this.Buffer[this.Position..];

		/// <summary>Ensure that there are at least <paramref name="count"/> bytes remaining in the buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureBytes(int count)
		{
			if (count < 0 || checked(this.Position + count) > this.Buffer.Length) throw ThrowNotEnoughBytes(count);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception ThrowNotEnoughBytes(int count)
		{
			return ThrowHelper.FormatException($"The buffer does not have enough data to satisfy a read of {count} byte(s)");
		}

		/// <summary>Returns the value of the next byte in the buffer, or -1 if we reached the end</summary>
		public readonly int PeekByte()
		{
			int p = this.Position;
			return (uint) p < this.Buffer.Length ? this.Buffer[p] : -1;
		}

		/// <summary>Returns the value of the byte at a specified offset from the current position, or -1 if this is after the end, or before the start</summary>
		public readonly int PeekByteAt(int offset)
		{
			int p = this.Position + offset;
			return (uint) p < this.Buffer.Length && p >= 0 ? this.Buffer[p] : -1;
		}

		/// <summary>Returns the value of the next bytes from the current pointer in the buffer</summary>
		/// <param name="count">Number of bytes to peek</param>
		/// <returns>Span of the next bytes</returns>
		/// <exception cref="ArgumentException">If there is not enough bytes remaining in the buffer</exception>
		public readonly ReadOnlySpan<byte> PeekBytes(int count)
		{
			return this.Buffer.Slice(this.Position, count);
		}

		/// <summary>Attempt to peek at the next <paramref name="count"/> bytes from the reader, without advancing the pointer</summary>
		/// <param name="count">Number of bytes to peek</param>
		/// <param name="bytes">Receives the corresponding slice if there are enough bytes remaining.</param>
		/// <returns>If <c>true</c>, the next <paramref name="count"/> are available in <paramref name="bytes"/>. If <c>false</c>, there are not enough bytes remaining in the buffer.</returns>
		public readonly bool TryPeekBytes(int count, out ReadOnlySpan<byte> bytes)
		{
			if (this.Remaining < count)
			{
				bytes = default;
				return false;
			}
			bytes = this.Buffer.Slice(this.Position, count);
			return true;
		}

		/// <summary>Skip the next <paramref name="count"/> bytes of the buffer</summary>
		public void Skip(int count)
		{
			EnsureBytes(count);

			this.Position += count;
		}

		/// <summary>Read the next byte from the buffer</summary>
		public byte ReadByte()
		{
			int p = this.Position;
			if ((uint) (p + 1) > (uint) this.Buffer.Length) throw ThrowNotEnoughBytes(1);
			this.Position = p + 1;
			return this.Buffer[p];
		}

		/// <summary>Read the next 2 bytes from the buffer</summary>
		public ReadOnlySpan<byte> ReadTwoBytes()
		{
			int p = this.Position;
			if ((uint) (p + 2) > (uint) this.Buffer.Length) throw ThrowNotEnoughBytes(2);
			this.Position = p + 2;
			return this.Buffer.Slice(p, 2);
		}

		/// <summary>Read the next 4 bytes from the buffer</summary>
		public ReadOnlySpan<byte> ReadFourBytes()
		{
			int p = this.Position;
			if ((uint) (p + 4) > (uint) this.Buffer.Length) throw ThrowNotEnoughBytes(4);
			this.Position = p + 4;
			return this.Buffer.Slice(p, 4);
		}

		/// <summary>Read the next 8 bytes from the buffer</summary>
		public ReadOnlySpan<byte> ReadEightBytes()
		{
			int p = this.Position;
			if ((uint) (p + 8) > (uint) this.Buffer.Length) throw ThrowNotEnoughBytes(8);
			this.Position = p + 8;
			return this.Buffer.Slice(p, 8);
		}

		/// <summary>Read the next 16 bytes from the buffer</summary>
		public ReadOnlySpan<byte> ReadSixteenBytes()
		{
			int p = this.Position;
			if ((uint) (p + 16) > (uint) this.Buffer.Length) throw ThrowNotEnoughBytes(16);
			this.Position = p + 16;
			return this.Buffer.Slice(p, 16);
		}

		/// <summary>Read the next <paramref name="count"/> bytes from the buffer</summary>
		public ReadOnlySpan<byte> ReadBytes(int count)
		{
			if (count == 0) return default;
			int p = this.Position;
			if (count < 0 || checked(p + count) > this.Buffer.Length) throw ThrowNotEnoughBytes(count);
			this.Position = p + count;
			return this.Buffer.Slice(p, count);
		}

		/// <summary>Read the next <paramref name="count"/> bytes from the buffer, unless we already reached the end.</summary>
		public bool TryReadBytes(int count, out ReadOnlySpan<byte> value)
		{
			int p = this.Position;
			if ((uint) (p + count) > (uint) this.Buffer.Length)
			{
				value = default;
				return false;
			}
			this.Position = p + count;
			// this way will not re-validate the arguments a second time
			value = this.Buffer.Slice(p, count);
			return true;
		}

		/// <summary>Read the next <paramref name="count"/> bytes from the buffer, unless we already reached the end.</summary>
		public bool TryCopyBytes(int count, Span<byte> buffer)
		{
			int p = this.Position;
			var span = this.Buffer;
			if ((uint) (p + count) > (uint) span.Length)
			{
				return false;
			}
			this.Position = p + count;
			span.Slice(p, count).CopyTo(buffer);
			return true;
		}

		/// <summary>Read the next <paramref name="count"/> bytes from the buffer</summary>
		public ReadOnlySpan<byte> ReadBytes(uint count)
		{
			int n = checked((int) count);
			int p = this.Position;
			if (checked(p + n) > this.Buffer.Length) throw ThrowNotEnoughBytes(n);
			this.Position = p + n;
			return this.Buffer.Slice(p, n);
		}

		/// <summary>Read until <paramref name="handler"/> returns true, or we reach the end of the buffer</summary>
		public ReadOnlySpan<byte> ReadWhile(Func<byte, int, bool> handler)
		{
			unsafe
			{
				int start = this.Position;
				int count = 0;
				fixed (byte* bytes = this.Buffer)
				{
					byte* ptr = bytes;
					byte* end = bytes + this.Remaining;
					while (ptr < end)
					{
						if (!handler(*ptr, count))
						{
							break;
						}
						++ptr;
						++count;
					}
					this.Position = start + count;
					return this.Buffer.Slice(start, count);
				}
			}
		}

		/// <summary>Reads all the remaining bytes in the buffer</summary>
		/// <returns>Rest of the buffer</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ReadOnlySpan<byte> ReadToEnd() => ReadBytes(this.Remaining);

		#region Little Endian (aka INTEL)

		/// <summary>Read the next 2 bytes as an unsigned 16-bit integer, encoded in little-endian</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public short ReadInt16()
			=> BinaryPrimitives.ReadInt16LittleEndian(ReadTwoBytes());

		/// <summary>Read the next 2 bytes as an unsigned 16-bit integer, encoded in little-endian</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ushort ReadUInt16()
			=> BinaryPrimitives.ReadUInt16LittleEndian(ReadTwoBytes());

		/// <summary>Read the next 3 bytes as a signed 24-bit integer, encoded in little-endian</summary>
		/// <remarks>Bits 24 to 31 will sign expanded</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int ReadInt24()
			=> ReadBytes(3).ToInt24();

		/// <summary>Read the next 3 bytes as an unsigned 24-bit integer, encoded in little-endian</summary>
		/// <remarks>Bits 24 to 31 will always be zero</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public uint ReadUInt24()
			=> ReadBytes(3).ToUInt24();

		/// <summary>Read the next 4 bytes as a signed 32-bit integer, encoded in little-endian</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int ReadInt32()
			=> BinaryPrimitives.ReadInt32LittleEndian(ReadFourBytes());

		/// <summary>Read the next 4 bytes as an unsigned 32-bit integer, encoded in little-endian</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public uint ReadUInt32()
			=> BinaryPrimitives.ReadUInt32LittleEndian(ReadFourBytes());

		/// <summary>Read the next 8 bytes as a signed 64-bit integer, encoded in little-endian</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public long ReadInt64()
			=> BinaryPrimitives.ReadInt64LittleEndian(ReadEightBytes());

		/// <summary>Read the next 8 bytes as an unsigned 64-bit integer, encoded in little-endian</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ulong ReadUInt64()
			=> BinaryPrimitives.ReadUInt64LittleEndian(ReadEightBytes());

		#endregion

		#region Big Endian (aka MOTOROLA / network)

		/// <summary>Read the next 2 bytes as a signed 16-bit integer, encoded in big-endian</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public short ReadInt16BE()
			=> BinaryPrimitives.ReadInt16BigEndian(ReadTwoBytes());

		/// <summary>Read the next 2 bytes as an unsigned 16-bit integer, encoded in big-endian</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ushort ReadUInt16BE()
			=> BinaryPrimitives.ReadUInt16BigEndian(ReadTwoBytes());

		/// <summary>Read the next 3 bytes as a signed 24-bit integer, encoded in big-endian</summary>
		/// <remarks>Bits 24 to 31 will sign expanded</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int ReadInt24BE()
		{
			return ReadBytes(3).ToInt24BE();
		}

		/// <summary>Read the next 3 bytes as an unsigned 24-bit integer, encoded in big-endian</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public uint ReadUInt24BE()
		{
			return ReadBytes(3).ToUInt24BE();
		}

		/// <summary>Read the next 4 bytes as a signed 32-bit integer, encoded in big-endian</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int ReadInt32BE()
			=> BinaryPrimitives.ReadInt32BigEndian(ReadFourBytes());

		/// <summary>Read the next 4 bytes as an unsigned 32-bit integer, encoded in big-endian</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public uint ReadUInt32BE()
			=> BinaryPrimitives.ReadUInt32BigEndian(ReadFourBytes());

		/// <summary>Read the next 8 bytes as a signed 64-bit integer, encoded in big-endian</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public long ReadInt64BE()
			=> BinaryPrimitives.ReadInt64BigEndian(ReadEightBytes());

		/// <summary>Read the next 8 bytes as an unsigned 64-bit integer, encoded in big-endian</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ulong ReadUInt64BE()
			=> BinaryPrimitives.ReadUInt64BigEndian(ReadEightBytes());

		#endregion

		/// <summary>Read the next 4 bytes as an IEEE 32-bit floating point number</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public float ReadSingle()
			=> BinaryPrimitives.ReadSingleLittleEndian(ReadFourBytes());

		/// <summary>Read the next 8 bytes as an IEEE 64-bit floating point number</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public double ReadDouble()
			=> BinaryPrimitives.ReadDoubleLittleEndian(ReadEightBytes());

		/// <summary>Read an encoded nul-terminated byte array from the buffer</summary>
		public ReadOnlySpan<byte> ReadByteString()
		{
			var buffer = this.Buffer;
			int start = this.Position;
			int p = start;
			int end = buffer.Length;

			while (p < end)
			{
				byte b = buffer[p++];
				if (b == 0)
				{
					//TODO: decode \0\xFF ?
					if (p < end && buffer[p] == 0xFF)
					{
						// skip the next byte and continue
						p++;
						continue;
					}

					this.Position = p;
					return buffer.Slice(start, p - start);
				}
			}

			throw ThrowHelper.FormatException("Truncated byte string (expected terminal NUL not found)");
		}

		/// <summary>Read an encoded nul-terminated byte array from the buffer</summary>
		[Pure]
		public bool TryReadByteString(out ReadOnlySpan<byte> bytes)
		{
			var buffer = this.Buffer;
			int start = this.Position;
			int p = start;
			int end = this.Buffer.Length;

			while (p < end)
			{
				byte b = buffer[p++];
				if (b == 0)
				{
					//TODO: decode \0\xFF ?
					if (p < end && buffer[p] == 0xFF)
					{
						// skip the next byte and continue
						p++;
						continue;
					}

					this.Position = p;
					bytes = buffer.Slice(start, p - start);
					return true;
				}
			}

			bytes = default;
			return false;
		}
		/// <summary>Reads a 7-bit encoded unsigned int (aka 'Varint16') from the buffer, and advances the cursor</summary>
		/// <remarks>Can Read up to 3 bytes from the input</remarks>
		public ushort ReadVarInt16()
		{
			//note: this could read up to 21 bits of data, so we check for overflow
			return checked((ushort) ReadVarInt(3));
		}

		/// <summary>Reads a 7-bit encoded unsigned int (aka 'Varint32') from the buffer, and advances the cursor</summary>
		/// <remarks>Can Read up to 5 bytes from the input</remarks>
		public uint ReadVarInt32()
		{
			//note: this could read up to 35 bits of data, so we check for overflow
			return checked((uint) ReadVarInt(5));
		}

		/// <summary>Reads a 7-bit encoded unsigned long (aka 'Varint32') from the buffer, and advances the cursor</summary>
		/// <remarks>Can Read up to 10 bytes from the input</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ulong ReadVarInt64()
		{
			return ReadVarInt(10);
		}

		/// <summary>Reads a Base 128 Varint from the input</summary>
		/// <param name="count">Maximum number of bytes allowed (5 for 32 bits, 10 for 64 bits)</param>
		private ulong ReadVarInt(int count)
		{
			var buffer = this.Buffer;
			int p = this.Position;
			int end = buffer.Length;

			ulong x = 0;
			int s = 0;

			// read bytes until the MSB is unset
			while (count-- > 0)
			{
				if (p > end) throw ThrowHelper.FormatException("Truncated Varint");
				byte b = buffer[p++];

				x |= (b & 0x7FUL) << s;
				if (b < 0x80)
				{
					this.Position = p;
					return x;
				}
				s += 7;
			}
			throw ThrowHelper.FormatException("Malformed Varint");
		}

		/// <summary>Reads a variable sized slice, by first reading its size (stored as a Varint32) and then the data</summary>
		public ReadOnlySpan<byte> ReadVarBytes()
		{
			uint size = ReadVarInt32();
			if (size > int.MaxValue) throw ThrowHelper.FormatException("Malformed variable-sized array");
			if (size == 0) return default;
			return ReadBytes((int) size);
		}

		/// <summary>Reads an utf-8 encoded string prefixed by a variable-sized length</summary>
		public string ReadVarString()
		{
			var str = ReadVarBytes();
			return str.Length == 0 ? string.Empty : Encoding.UTF8.GetString(str);
		}

		/// <summary>Reads a string prefixed by a variable-sized length, using the specified encoding</summary>
		/// <remarks>Encoding used for this string (or UTF-8 if null)</remarks>
		public string ReadVarString(Encoding? encoding)
		{
			// generic decoding
			var bytes = ReadVarBytes();
			return bytes.Length == 0 ? string.Empty : (encoding ?? Encoding.UTF8).GetString(bytes);
		}

		/// <summary>Reads a 128-bit Guid</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Guid ReadGuid()
		{
			return ReadSixteenBytes().ToGuid();
		}

		/// <summary>Reads a 128-bit UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128 ReadUuid128()
		{
			return ReadSixteenBytes().ToUuid128();
		}

		/// <summary>Reads a 64-bit UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid64 ReadUuid64()
		{
			return ReadEightBytes().ToUuid64();
		}

		// for debugger only
		private readonly Slice BufferReadable => Slice.FromBytes(this.Buffer);

		// for debugger only
		private readonly Slice TailReadable => Slice.FromBytes(this.Tail);

	}

}
