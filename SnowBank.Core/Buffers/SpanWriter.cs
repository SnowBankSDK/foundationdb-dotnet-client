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
	using System.Text;
	using SnowBank.Buffers.Binary;
	using SnowBank.Data.Binary;

	/// <summary>Helper type for writing binary data into a <see cref="Span{T}"/> of bytes</summary>
	[PublicAPI]
	public ref struct SpanWriter
#if NET9_0_OR_GREATER
		: ISpanEncodable
#endif
	{

		/// <summary>Buffer where to write bytes</summary>
		public readonly Span<byte> Buffer;

		/// <summary>Current position in the buffer</summary>
		public int Position;

		/// <summary>Constructs a <see cref="SpanWriter"/> with an initial capacity</summary>
		/// <param name="capacity">Capacity of the initial buffer</param>
		public SpanWriter(int capacity)
		{
			Contract.Positive(capacity);
			this.Buffer = new byte[capacity];
			this.Position = 0;
		}

		/// <summary>Constructs a <see cref="SpanWriter"/> with an already allocated initial buffer</summary>
		/// <param name="buffer">Buffer that should be used by this writer</param>
		public SpanWriter(Span<byte> buffer)
		{
			this.Buffer = buffer;
			this.Position = 0;
		}

		/// <summary>Return the rest of the buffer, starting from the current position</summary>
		public readonly Span<byte> Tail
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Buffer[this.Position..];
		}

		/// <summary>Returns a span of everything that was written so far</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly ReadOnlySpan<byte> ToSpan()
		{
			return this.Buffer[..this.Position];
		}

		/// <summary>Copies everything that was written so far into a destination span</summary>
		public void CopyTo(Span<byte> destination)
		{
			this.Buffer[..this.Position].CopyTo(destination);
		}

		/// <summary>Copies everything that was written so far into a destination span, if it is large enough</summary>
		public bool TryCopyTo(Span<byte> destination)
		{
			return this.Buffer[..this.Position].TryCopyTo(destination);
		}

		/// <summary>Copies everything that was written so far into a destination span, if it is large enough</summary>
		public bool TryCopyTo(Span<byte> destination, out int bytesWritten)
		{
			var pos = this.Position;
			if (!this.Buffer[..pos].TryCopyTo(destination))
			{
				bytesWritten = 0;
				return false;
			}
			bytesWritten = pos;
			return true;
		}

		#region ISpanEncodable...

#if NET9_0_OR_GREATER

		/// <inheritdoc />
		bool ISpanEncodable.TryGetSpan(out ReadOnlySpan<byte> span)
		{
			span = ToSpan();
			return true;
		}

		/// <inheritdoc />
		bool ISpanEncodable.TryGetSizeHint(out int sizeHint)
		{
			sizeHint = this.Position;
			return true;
		}

		/// <inheritdoc />
		bool ISpanEncodable.TryEncode(scoped Span<byte> destination, out int bytesWritten)
		{
			var pos = this.Position;
			if (!this.Buffer[..pos].TryCopyTo(destination))
			{
				bytesWritten = 0;
				return false;
			}
			bytesWritten = pos;
			return true;
		}

#endif

		#endregion

		/// <summary>Ensures that the buffer can store <paramref name="count"/> additional bytes</summary>
		/// <param name="count">Number of bytes expected to be written soon.</param>
		/// <returns>Span that maps the corresponding free space in the buffer. The length of the span will be equal to <paramref name="count"/>, even if there are more space available in the buffer.</returns>
		/// <remarks>This does not advance the cursor.</remarks>
		/// <exception cref="ArgumentException">If the buffer is too small to fit <paramref name="count"/> additional bytes.</exception>
		/// <seealso cref="Allocate"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Span<byte> EnsureBytes(int count)
		{
			int pos = this.Position;
			return this.Buffer.Length - pos >= count ? this.Buffer.Slice(pos, count) : ThrowBufferTooSmall(nameof(count));
		}

		/// <summary>Allocate a span of bytes </summary>
		/// <param name="count">Size of the span (in bytes) to allocate.</param>
		/// <returns>Span that maps the corresponding free space in the buffer. The length of the span will be equal to <paramref name="count"/>, even if there are more space available in the buffer.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Span<byte> Allocate(int count)
		{
			int pos = this.Position;
			if (this.Buffer.Length - pos < count) return ThrowBufferTooSmall(nameof(count));
			this.Position = pos + count;
			return this.Buffer.Slice(pos, count);
		}

		[DoesNotReturn, StackTraceHidden]
		private static Span<byte> ThrowBufferTooSmall(string paramName)
		{
			throw new ArgumentException("Buffer is too small", paramName);
		}

		/// <summary>Adds a byte at the end of the buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteByte(byte value)
		{
			EnsureBytes(1)[0] = value;
			this.Position++;
		}

		/// <summary>Adds a byte at the end of the buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteByte(sbyte value)
		{
			EnsureBytes(1)[0] = (byte) value;
			this.Position++;
		}

		/// <summary>Adds a byte at the end of the buffer</summary>
		/// <remarks>Only the lowest 8 bits will be used.</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteByte(int value)
		{
			EnsureBytes(1)[0] = (byte) value;
			this.Position++;
		}

		/// <summary>Adds a byte at the end of the buffer</summary>
		/// <remarks>Only the lowest 8 bits will be used.</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteByte(char value)
		{
			EnsureBytes(1)[0] = (byte) value;
			this.Position++;
		}

		#region 16-bits

		/// <summary>Writes a 16-bit little-endian integer at the end of buffer, and advance the cursor by 2.</summary>
		public void WriteInt16(short value)
		{
			var chunk = EnsureBytes(2);
			chunk[0] = (byte) value;
			chunk[1] = (byte) (value >> 8);
			this.Position += 2;
		}

		/// <summary>Writes a 16-bit little-endian integer at the start of a span of bytes.</summary>
		public static void WriteInt16(in Span<byte> span, short value)
		{
			if (span.Length < 2) throw new ArgumentException();
			span[0] = (byte) value;
			span[1] = (byte) (value >> 8);
		}

		/// <summary>Writes a 16-bit little-endian integer at the end of buffer, and advance the cursor by 2.</summary>
		public void WriteUInt16(ushort value)
		{
			var chunk = EnsureBytes(2);
			chunk[0] = (byte) value;
			chunk[1] = (byte) (value >> 8);
			this.Position += 2;
		}

		/// <summary>Writes a 16-bit little-endian integer at the start of a span of bytes.</summary>
		public static void WriteUInt16(in Span<byte> span, ushort value)
		{
			if (span.Length < 2) throw new ArgumentException();
			span[0] = (byte) value;
			span[1] = (byte) (value >> 8);
		}

		/// <summary>Writes a 16-bit big-endian integer at the end of buffer, and advance the cursor by 2.</summary>
		public void WriteInt16BE(short value)
		{
			var chunk = EnsureBytes(2);
			chunk[0] = (byte) (value >> 8);
			chunk[1] = (byte) value;
			this.Position += 2;
		}

		/// <summary>Writes a 16-bit big-endian integer at the start of a span of bytes.</summary>
		public static void WriteInt16BE(in Span<byte> span, short value)
		{
			if (span.Length < 2) throw new ArgumentException();
			span[0] = (byte) (value >> 8);
			span[1] = (byte) value;
		}

		/// <summary>Writes a 16-bit big-endian integer at the end of buffer, and advance the cursor by 2.</summary>
		public void WriteUInt16BE(ushort value)
		{
			var chunk = EnsureBytes(2);
			chunk[0] = (byte) (value >> 8);
			chunk[1] = (byte) value;
			this.Position += 2;
		}

		/// <summary>Writes a 16-bit big-endian integer at the start of a span of bytes.</summary>
		public static void WriteUInt16BE(in Span<byte> span, ushort value)
		{
			if (span.Length < 2) throw new ArgumentException();
			span[0] = (byte) (value >> 8);
			span[1] = (byte) value;
		}

		#endregion

		#region 32-bits...

		/// <summary>Writes a 32-bit little-endian integer at the end of buffer, and advance the cursor by 4.</summary>
		public void WriteInt32(int value)
		{
			var chunk = EnsureBytes(4);
			chunk[0] = (byte) value;
			chunk[1] = (byte) (value >> 8);
			chunk[2] = (byte) (value >> 16);
			chunk[3] = (byte) (value >> 24);
			this.Position += 4;
		}

		/// <summary>Writes a 32-bit little-endian integer at the end of buffer, and advance the cursor by 4.</summary>
		public void WriteUInt32(uint value)
		{
			var chunk = EnsureBytes(4);
			chunk[0] = (byte) value;
			chunk[1] = (byte) (value >> 8);
			chunk[2] = (byte) (value >> 16);
			chunk[3] = (byte) (value >> 24);
			this.Position += 4;
		}

		/// <summary>Writes a 32-bit little-endian integer at the start of a span of bytes.</summary>
		public static void WriteInt32(in Span<byte> span, int value)
		{
			if (span.Length < 4) throw new ArgumentException();
			span[0] = (byte) value;
			span[1] = (byte) (value >> 8);
			span[2] = (byte) (value >> 16);
			span[3] = (byte) (value >> 24);
		}

		/// <summary>Writes a 32-bit little-endian integer at the start of a span of bytes.</summary>
		public static void WriteUInt32(in Span<byte> span, uint value)
		{
			if (span.Length < 4) throw new ArgumentException();
			span[0] = (byte) value;
			span[1] = (byte) (value >> 8);
			span[2] = (byte) (value >> 16);
			span[3] = (byte) (value >> 24);
		}

		/// <summary>Writes a 32-bit big-endian integer at the end of buffer, and advance the cursor by 4.</summary>
		public void WriteInt32BE(int value)
		{
			var chunk = EnsureBytes(4);
			chunk[0] = (byte) (value >> 24);
			chunk[1] = (byte) (value >> 16);
			chunk[2] = (byte) (value >> 8);
			chunk[3] = (byte) value;
			this.Position += 4;
		}

		/// <summary>Writes a 32-bit big-endian integer at the end of buffer, and advance the cursor by 4.</summary>
		public void WriteUInt32BE(uint value)
		{
			var chunk = EnsureBytes(4);
			chunk[0] = (byte) (value >> 24);
			chunk[1] = (byte) (value >> 16);
			chunk[2] = (byte) (value >> 8);
			chunk[3] = (byte) value;
			this.Position += 4;
		}

		/// <summary>Writes a 32-bit big-endian integer at the start of a span of bytes.</summary>
		public static void WriteInt32BE(in Span<byte> span, int value)
		{
			if (span.Length < 4) throw new ArgumentException();
			span[0] = (byte) (value >> 24);
			span[1] = (byte) (value >> 16);
			span[2] = (byte) (value >> 8);
			span[3] = (byte) value;
		}

		/// <summary>Writes a 32-bit big-endian integer at the start of a span of bytes.</summary>
		public static void WriteUInt32BE(in Span<byte> span, uint value)
		{
			if (span.Length < 4) throw new ArgumentException();
			span[0] = (byte) (value >> 24);
			span[1] = (byte) (value >> 16);
			span[2] = (byte) (value >> 8);
			span[3] = (byte) value;
		}

		#endregion

		#region 64-bits...

		/// <summary>Writes a 64-bit little-endian integer at the end of buffer, and advance the cursor by 8.</summary>
		public void WriteInt64(long value)
		{
			var chunk = EnsureBytes(8);
			chunk[0] = (byte) value;
			chunk[1] = (byte) (value >> 8);
			chunk[2] = (byte) (value >> 16);
			chunk[3] = (byte) (value >> 24);
			chunk[4] = (byte) (value >> 32);
			chunk[5] = (byte) (value >> 40);
			chunk[6] = (byte) (value >> 48);
			chunk[7] = (byte) (value >> 56);
			this.Position += 8;
		}

		/// <summary>Writes a 64-bit little-endian integer at the end of buffer, and advance the cursor by 8.</summary>
		public void WriteUInt64(ulong value)
		{
			var chunk = EnsureBytes(8);
			chunk[0] = (byte) value;
			chunk[1] = (byte) (value >> 8);
			chunk[2] = (byte) (value >> 16);
			chunk[3] = (byte) (value >> 24);
			chunk[4] = (byte) (value >> 32);
			chunk[5] = (byte) (value >> 40);
			chunk[6] = (byte) (value >> 48);
			chunk[7] = (byte) (value >> 56);
			this.Position += 8;
		}

		/// <summary>Writes a 64-bit little-endian integer at the start of a span of bytes.</summary>
		public static void WriteInt64(in Span<byte> span, long value)
		{
			if (span.Length < 8) throw new ArgumentException();
			span[0] = (byte) value;
			span[1] = (byte) (value >> 8);
			span[2] = (byte) (value >> 16);
			span[3] = (byte) (value >> 24);
			span[4] = (byte) (value >> 32);
			span[5] = (byte) (value >> 40);
			span[6] = (byte) (value >> 48);
			span[7] = (byte) (value >> 56);
		}

		/// <summary>Writes a 64-bit little-endian integer at the start of a span of bytes.</summary>
		public static void WriteUInt64(in Span<byte> span, ulong value)
		{
			if (span.Length < 8) throw new ArgumentException();
			span[0] = (byte) value;
			span[1] = (byte) (value >> 8);
			span[2] = (byte) (value >> 16);
			span[3] = (byte) (value >> 24);
			span[4] = (byte) (value >> 32);
			span[5] = (byte) (value >> 40);
			span[6] = (byte) (value >> 48);
			span[7] = (byte) (value >> 56);
		}

		/// <summary>Writes a 64-bit big-endian integer at the end of buffer, and advance the cursor by 8.</summary>
		public void WriteInt64BE(long value)
		{
			var chunk = EnsureBytes(8);
			chunk[0] = (byte) (value >> 56);
			chunk[1] = (byte) (value >> 48);
			chunk[2] = (byte) (value >> 40);
			chunk[3] = (byte) (value >> 32);
			chunk[4] = (byte) (value >> 24);
			chunk[5] = (byte) (value >> 16);
			chunk[6] = (byte) (value >> 8);
			chunk[7] = (byte) value;
			this.Position += 8;
		}

		/// <summary>Writes a 64-bit big-endian integer at the end of buffer, and advance the cursor by 8.</summary>
		public void WriteUInt64BE(ulong value)
		{
			var chunk = EnsureBytes(8);
			chunk[0] = (byte) (value >> 56);
			chunk[1] = (byte) (value >> 48);
			chunk[2] = (byte) (value >> 40);
			chunk[3] = (byte) (value >> 32);
			chunk[4] = (byte) (value >> 24);
			chunk[5] = (byte) (value >> 16);
			chunk[6] = (byte) (value >> 8);
			chunk[7] = (byte) value;
			this.Position += 8;
		}

		/// <summary>Writes a 64-bit big-endian integer at the start of a span of bytes.</summary>
		public static void WriteInt64BE(in Span<byte> span, long value)
		{
			if (span.Length < 8) throw new ArgumentException();
			span[0] = (byte) (value >> 56);
			span[1] = (byte) (value >> 48);
			span[2] = (byte) (value >> 40);
			span[3] = (byte) (value >> 32);
			span[4] = (byte) (value >> 24);
			span[5] = (byte) (value >> 16);
			span[6] = (byte) (value >> 8);
			span[7] = (byte) value;
		}

		/// <summary>Writes a 64-bit big-endian integer at the start of a span of bytes.</summary>
		public static void WriteUInt64BE(in Span<byte> span, ulong value)
		{
			if (span.Length < 8) throw new ArgumentException();
			span[0] = (byte) (value >> 56);
			span[1] = (byte) (value >> 48);
			span[2] = (byte) (value >> 40);
			span[3] = (byte) (value >> 32);
			span[4] = (byte) (value >> 24);
			span[5] = (byte) (value >> 16);
			span[6] = (byte) (value >> 8);
			span[7] = (byte) value;
		}

		#endregion

		#region VarInts...

		/// <summary>Writes a 7-bit encoded unsigned int (aka 'Varint16') at the end, and advances the cursor</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteVarInt16(ushort value)
		{
			if (value < (1 << 7))
			{
				WriteByte((byte) value);
			}
			else
			{
				WriteVarInt16Slow(value);
			}
		}

		private void WriteVarInt16Slow(ushort value)
		{
			const uint MASK = 128;
			//note: value is known to be >= 128
			if (value < (1 << 14))
			{
				WriteBytes(
					(byte) (value | MASK),
					(byte) (value >> 7)
				);
			}
			else
			{
				WriteBytes(
					(byte) (value | MASK),
					(byte) ((value >> 7) | MASK),
					(byte) (value >> 14)
				);
			}

		}

		/// <summary>Writes a 7-bit encoded unsigned int (aka 'Varint32') at the end, and advances the cursor</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteVarInt32(uint value)
		{
			if (value < (1 << 7))
			{
				WriteByte((byte) value);
			}
			else
			{
				WriteVarInt32Slow(value);
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void WriteVarInt32Slow(uint value)
		{
			const uint MASK = 128;
			//note: value is known to be >= 128
			if (value < (1 << 14))
			{
				WriteBytes(
					(byte) (value | MASK),
					(byte) (value >> 7)
				);
			}
			else if (value < (1 << 21))
			{
				WriteBytes(
					(byte) (value | MASK),
					(byte) ((value >> 7) | MASK),
					(byte) (value >> 14)
				);
			}
			else if (value < (1 << 28))
			{
				WriteBytes(
					(byte) (value | MASK),
					(byte) ((value >> 7) | MASK),
					(byte) ((value >> 14) | MASK),
					(byte) (value >> 21)
				);
			}
			else
			{
				WriteBytes(
					(byte) (value | MASK),
					(byte) ((value >> 7) | MASK),
					(byte) ((value >> 14) | MASK),
					(byte) ((value >> 21) | MASK),
					(byte) (value >> 28)
				);
			}
		}

		/// <summary>Writes a 7-bit encoded unsigned long (aka 'Varint64') at the end, and advances the cursor</summary>
		public void WriteVarInt64(ulong value)
		{
			//note: if the size is 64-bits, we probably expect values to always be way above 128 so no need to optimize for this case here

			const uint MASK = 128;
			// max encoded size is 10 bytes
			var buffer = EnsureBytes((int) UnsafeHelpers.SizeOfVarInt(value));
			int p = this.Position;
			while (value >= MASK)
			{
				buffer[p++] = (byte) ((value & (MASK - 1)) | MASK);
				value >>= 7;
			}
			buffer[p++] = (byte) value;
			this.Position = p;
		}

		/// <summary>Return the size (in bytes) that a 32-bit number would need when encoded as a VarInt</summary>
		/// <param name="value">Number that needs to be encoded</param>
		/// <returns>Number of bytes needed (1-5)</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int SizeOfVarInt(int value)
		{
			return (uint) value < (1U << 7) ? 1 : SizeOfVarIntSlow(value);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static int SizeOfVarIntSlow(int value)
			{
				if (value < 0) throw new ArgumentException("Size cannot be negative", nameof(value));
				// count is already known to be >= 128
				if (value < (1U << 14)) return 2;
				if (value < (1U << 21)) return 3;
				if (value < (1U << 28)) return 4;
				return 5;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int WriteVarInt32Unsafe(Span<byte> buffer, int value)
		{
			//note: value is known to be >= 128
			if ((uint) value < (1U << 7))
			{
				buffer[0] = (byte) value;
				return 1;
			}
			return WriteVarInt32UnsafeSlow(buffer, value);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static int WriteVarInt32UnsafeSlow(Span<byte> buffer, int value)
			{
				Contract.Debug.Requires(value > 127);

				const uint MASK = 128;
				if (value < (1 << 14))
				{
					buffer[0] = (byte) (value | MASK);
					buffer[1] = (byte) (value >> 7);
					return 2;
				}

				if (value < (1 << 21))
				{
					buffer[0] = (byte) (value | MASK);
					buffer[1] = (byte) ((value >> 7) | MASK);
					buffer[2] = (byte) (value >> 14);
					return 3;
				}

				if (value < (1 << 28))
				{
					buffer[0] = (byte) (value | MASK);
					buffer[1] = (byte) ((value >> 7) | MASK);
					buffer[2] = (byte) ((value >> 14) | MASK);
					buffer[3] = (byte) (value >> 21);
					return 4;
				}

				buffer[0] = (byte) (value | MASK);
				buffer[1] = (byte) ((value >> 7) | MASK);
				buffer[2] = (byte) ((value >> 14) | MASK);
				buffer[3] = (byte) ((value >> 21) | MASK);
				buffer[4] = (byte) (value >> 28);
				return 5;
			}
		}

		#endregion

		/// <summary>Adds a span of bytes at the end of the buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBytes(in ReadOnlySpan<byte> bytes)
		{
			bytes.CopyTo(EnsureBytes(bytes.Length));
			this.Position += bytes.Length;
		}

		/// <summary>Adds a span of bytes at the end of the buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBytes(in ReadOnlyMemory<byte> bytes)
		{
			bytes.Span.CopyTo(EnsureBytes(bytes.Length));
			this.Position += bytes.Length;
		}

		/// <summary>Adds two bytes at the end of the buffer</summary>
		public void WriteBytes(byte value1, byte value2)
		{
			var chunk = EnsureBytes(2);
			chunk[0] = value1;
			chunk[1] = value2;
			this.Position += 2;
		}

		/// <summary>Adds three bytes at the end of the buffer</summary>
		public void WriteBytes(byte value1, byte value2, byte value3)
		{
			var chunk = EnsureBytes(3);
			chunk[0] = value1;
			chunk[1] = value2;
			chunk[2] = value3;
			this.Position += 3;
		}

		/// <summary>Adds four bytes at the end of the buffer</summary>
		public void WriteBytes(byte value1, byte value2, byte value3, byte value4)
		{
			var chunk = EnsureBytes(4);
			chunk[0] = value1;
			chunk[1] = value2;
			chunk[2] = value3;
			chunk[3] = value4;
			this.Position += 4;
		}

		/// <summary>Adds five bytes at the end of the buffer</summary>
		public void WriteBytes(byte value1, byte value2, byte value3, byte value4, byte value5)
		{
			var chunk = EnsureBytes(5);
			chunk[0] = value1;
			chunk[1] = value2;
			chunk[2] = value3;
			chunk[3] = value4;
			chunk[4] = value5;
			this.Position += 5;
		}

		/// <summary>Adds a variable-length string encoded as UTF-8, and preceded by its length encoded as a VarInt32</summary>
		/// <seealso cref="WriteVarInt32"/>
		public void WriteVarStringUtf8(ReadOnlySpan<char> text)
		{
			if (text.Length == 0)
			{
				WriteByte(0);
				return;
			}

			int n = Encoding.UTF8.GetByteCount(text);
			var buf = EnsureBytes(n + SizeOfVarInt(n));
			int p = WriteVarInt32Unsafe(buf, n);
			Encoding.UTF8.GetBytes(text, buf[p..]);
			this.Position += n;
		}

	}
}
