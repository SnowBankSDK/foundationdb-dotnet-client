#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace SnowBank.Data.Tuples.Binary
{
	using System.Buffers;
	using System.Buffers.Binary;
	using System.Numerics;
	using System.Runtime.InteropServices;
	using System.Text;
	using SnowBank.Data.Tuples;

	/// <summary>Buffer for writing tuples into a <see cref="Span{T}">Span&lt;byte&gt;</see></summary>
	[DebuggerDisplay("{ToString(),nq}"), DebuggerTypeProxy(typeof(DebugView))]
	public ref struct TupleSpanWriter
	{

		/// <summary>Fixed-size buffer</summary>
		public readonly Span<byte> Buffer;

		/// <summary>Current depth of the tuple (0 = top)</summary>
		public int Depth;

		/// <summary>Number of bytes written so far</summary>
		/// <remarks>The next free byte is at Buffer[BytesWritten]</remarks>
		public int BytesWritten;

		[SkipLocalsInit]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TupleSpanWriter(Span<byte> buffer, int depth)
		{
			this.Buffer = buffer;
			this.Depth = depth;
		}

		/// <summary>Allocates a span with an exact size</summary>
		/// <param name="size">Requested size</param>
		/// <param name="span">Span of that size</param>
		/// <returns><c>true</c> if the buffer was large enough</returns>
		/// <remarks>The cursor will be advanced by exactly <paramref name="size"/> bytes</remarks>
		[MustUseReturnValue]
		public bool TryAllocateSpan(int size, out Span<byte> span)
		{
			int pos = this.BytesWritten;
			int newPos = checked(this.BytesWritten + size);
			if (newPos > this.Buffer.Length)
			{
				span = default;
				return false;
			}

			span = this.Buffer.Slice(pos, size);
			this.BytesWritten = newPos;
			return true;
		}

		/// <summary>Gets a span of all the remaining space in the buffer, if it is not full</summary>
		/// <remarks>This does not advance the cursor. You must call <see cref="TryAdvance"/> or <see cref="AdvanceUnsafe"/> to "commit" the bytes</remarks>
		[MustUseReturnValue]
		public readonly bool TryGetTail(out Span<byte> span)
		{
			int pos = this.BytesWritten;
			if (pos >= this.Buffer.Length)
			{
				span = default;
				return false;
			}

			span = this.Buffer[pos..];
			return true;
		}

		/// <summary>Gets a span that can fit at least the given minimum capacity</summary>
		/// <param name="size">Minimum size requested</param>
		/// <param name="span">Receives the remaining space in the buffer, which should be at least of length <paramref name="size"/></param>
		/// <returns><c>true</c> if the buffer has enough remaining capacity</returns>
		/// <remarks>This does not advance the cursor. You must call <see cref="TryAdvance"/> or <see cref="AdvanceUnsafe"/> to "commit" the bytes</remarks>
		[MustUseReturnValue]
		public readonly bool TryGetSpan(int size, out Span<byte> span)
		{
			int pos = this.BytesWritten;

			if (checked(this.BytesWritten + size) > this.Buffer.Length)
			{
				span = default;
				return false;
			}

			span = this.Buffer[pos..];
			return true;
		}

		/// <summary>Advance the cursor by the given offset, if the buffer is large enough</summary>
		/// <param name="offset">Number of bytes consumed after a previous call to <see cref="TryGetSpan"/> or <see cref="TryGetTail"/></param>
		/// <returns><c>true</c> if the buffer was large enough</returns>
		[MustUseReturnValue]
		public bool TryAdvance(int offset)
		{
			int pos = checked(this.BytesWritten + offset);
			if (pos > this.Buffer.Length)
			{
				return false;
			}

			this.BytesWritten = pos;
			return true;
		}

		/// <summary>Advance the cursor by the given offset</summary>
		/// <param name="offset">Number of bytes consumed after a previous call to <see cref="TryGetSpan"/> or <see cref="TryGetTail"/></param>
		/// <remarks>The caller <b>MUST</b> already have validated the size, otherwise the state of the writer could become corrupted!</remarks>
		public void AdvanceUnsafe(int offset)
		{
			this.BytesWritten += offset;
		}

		/// <summary>Writes a Null element, and advance the cursor</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.NoInlining)]
		public bool TryWriteNil()
		{
			int pos = this.BytesWritten;
			if (this.Depth == 0)
			{
				if (pos < this.Buffer.Length)
				{
					this.Buffer[pos] = TupleTypes.Nil;
					this.BytesWritten = pos + 1;
					return true;
				}
			}
			else
			{
				if (pos + 1 < this.Buffer.Length)
				{
					this.Buffer[pos] = TupleTypes.Nil;
					this.Buffer[pos + 1] = 0xFF;
					this.BytesWritten = pos + 2;
					return true;
				}
			}

			return false;
		}

		/// <summary>Writes a single-byte literal, and advance the cursor</summary>
		[MustUseReturnValue]
		public bool TryWriteLiteral(byte byte1)
		{
			int pos = this.BytesWritten;
			if (pos < this.Buffer.Length)
			{
				this.Buffer[pos] = byte1;
				this.BytesWritten = pos + 1;
				return true;
			}

			return false;
		}

		/// <summary>Writes a two-bytes literal, and advance the cursor</summary>
		[MustUseReturnValue]
		public bool TryWriteLiteral(byte byte1, byte byte2)
		{
			int pos = this.BytesWritten;
			if (pos + 1 < this.Buffer.Length)
			{
				this.Buffer[pos] = byte1;
				this.Buffer[pos + 1] = byte2;
				this.BytesWritten = pos + 2;
				return true;
			}

			return false;
		}

		/// <summary>Writes a bytes literal, and advance the cursor</summary>
		[MustUseReturnValue]
		public bool TryWriteLiteral(scoped ReadOnlySpan<byte> bytes)
		{
			int pos = this.BytesWritten;
			if (!bytes.TryCopyTo(this.Buffer[pos..]))
			{
				return false;
			}
			this.BytesWritten = pos + bytes.Length;
			return true;
		}

		/// <summary>Returns a view of the bytes that have been written so far</summary>
		public readonly ReadOnlySpan<byte> GetWrittenSpan() => this.Buffer[..this.BytesWritten];

		public override string ToString() => Slice.Dump(GetWrittenSpan());

		private readonly struct DebugView
		{

			public DebugView(TupleSpanWriter tw) => this.Data = Slice.FromBytes(tw.GetWrittenSpan());

			public readonly Slice Data;

			public int BytesWritten => this.Data.Count;

			public string Text => Slice.Dump(this.Data);

			public string Hex => this.Data.ToHexString(' ');

		}

	}

	/// <summary>Helper class that contains low-level encoders for the tuple binary format</summary>
	[DebuggerNonUserCode]
	public static class TupleParser
	{

		#region Serialization...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteNil(ref TupleSpanWriter writer)
			=> writer.TryWriteNil();

		/// <summary>Writes a null value at the end, and advance the cursor</summary>
		public static void WriteNil(this TupleWriter writer)
		{
			if (writer.Depth == 0)
			{ // at the top level, NILs are escaped as <00>
				writer.Output.WriteByte(TupleTypes.Nil);
			}
			else
			{ // inside a tuple, NILs are escaped as <00><FF>
				writer.Output.WriteBytes(TupleTypes.Nil, 0xFF);
			}
		}

		#region Boolean...

		public static bool TryWriteBool(ref TupleSpanWriter writer, in bool value)
		{
			// false => 26
			// true  => 27
			return writer.TryWriteLiteral(value ? TupleTypes.True : TupleTypes.False);
		}

		public static void WriteBool(this TupleWriter writer, bool value)
		{
			// false => 26
			// true  => 27
			//note: old versions used to encode bool as integer 0 or 1
			writer.Output.WriteByte(value ? TupleTypes.True : TupleTypes.False);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteBool(ref TupleSpanWriter writer, in bool? value)
		{
			// null  => 00
			// false => 26
			// true  => 27
			return writer.TryWriteLiteral(value is null ? TupleTypes.Nil : value.Value ? TupleTypes.True : TupleTypes.False);
		}

		public static void WriteBool(this TupleWriter writer, bool? value)
		{
			// null  => 00
			// false => 26
			// true  => 27
			if (value != null)
			{
				writer.Output.WriteByte(value.Value ? TupleTypes.True : TupleTypes.False);
			}
			else
			{
				WriteNil(writer);
			}
		}

		#endregion

		#region Byte...

		/// <summary>Writes an UInt8 at the end, and advance the cursor</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Unsigned BYTE, 32 bits</param>
		public static bool TryWriteByte(ref TupleSpanWriter writer, in byte value)
		{
			return value == 0
				// zero
				? writer.TryWriteLiteral(TupleTypes.IntZero)
				// 1..255: frequent for array index
				: writer.TryWriteLiteral(TupleTypes.IntPos1, value);
		}

		/// <summary>Writes an UInt8 at the end, and advance the cursor</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Unsigned BYTE, 32 bits</param>
		public static void WriteByte(this TupleWriter writer, byte value)
		{
			if (value == 0)
			{ // zero
				writer.Output.WriteByte(TupleTypes.IntZero);
			}
			else
			{ // 1..255: frequent for array index
				writer.Output.WriteBytes(TupleTypes.IntPos1, value);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteByte(ref TupleSpanWriter writer, in byte? value)
			=> value is null ? writer.TryWriteNil() : TryWriteByte(ref writer, value.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteByte(this TupleWriter writer, byte? value)
		{
			if (!value.HasValue) WriteNil(writer); else WriteByte(writer, value.Value);
		}

		#endregion

		#region SByte...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteSByte(ref TupleSpanWriter writer, in sbyte value)
			=> TryWriteInt32(ref writer, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteSByte(this TupleWriter writer, sbyte value)
			=> WriteInt32(writer, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteSByte(ref TupleSpanWriter writer, in sbyte? value)
			=> value is null ? writer.TryWriteNil() : TryWriteInt32(ref writer, value.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteSByte(this TupleWriter writer, sbyte? value)
		{
			if (!value.HasValue) WriteNil(writer); else WriteInt32(writer, value.Value);
		}

		#endregion

		#region Int16...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteInt16(ref TupleSpanWriter writer, in short value)
			=> TryWriteInt32(ref writer, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteInt16(this TupleWriter writer, short value)
			=> WriteInt32(writer, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteInt16(ref TupleSpanWriter writer, in short? value)
			=> value is null ? writer.TryWriteNil() : TryWriteInt32(ref writer, value.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteInt16(this TupleWriter writer, short? value)
		{
			if (!value.HasValue) WriteNil(writer); else WriteInt32(writer, value.Value);
		}

		#endregion

		#region UInt16...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteUInt16(ref TupleSpanWriter writer, in ushort value)
			=> TryWriteUInt32(ref writer, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteUInt16(this TupleWriter writer, ushort value)
			=> WriteUInt32(writer, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteUInt16(ref TupleSpanWriter writer, in ushort? value)
			=> value is null ? writer.TryWriteNil() : TryWriteUInt32(ref writer, value.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteUInt16(this TupleWriter writer, ushort? value)
		{
			if (!value.HasValue) WriteNil(writer); else WriteUInt32(writer, value.Value);
		}

		#endregion

		#region Int32...

		/// <summary>Writes an Int32 at the end, and advance the cursor</summary>
		public static bool TryWriteInt32(ref TupleSpanWriter writer, in int value)
		{
			if (value <= 255)
			{
				if (value == 0)
				{ // zero
					return writer.TryWriteLiteral(TupleTypes.IntZero);
				}

				if (value > 0)
				{ // 1..255: frequent for array index
					return writer.TryWriteLiteral(TupleTypes.IntPos1, (byte) value);
				}

				if (value > -256)
				{ // -255..-1
					return writer.TryWriteLiteral(TupleTypes.IntNeg1, (byte) (255 + value));
				}
			}

			return TryWriteInt64Slow(ref writer, value);
		}

		/// <summary>Writes an Int32 at the end, and advance the cursor</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Signed DWORD, 32 bits, High Endian</param>
		public static void WriteInt32(this TupleWriter writer, int value)
		{
			if (value <= 255)
			{
				if (value == 0)
				{ // zero
					writer.Output.WriteByte(TupleTypes.IntZero);
					return;
				}

				if (value > 0)
				{ // 1..255: frequent for array index
					writer.Output.WriteBytes(TupleTypes.IntPos1, (byte) value);
					return;
				}

				if (value > -256)
				{ // -255..-1
					writer.Output.WriteBytes(TupleTypes.IntNeg1, (byte) (255 + value));
					return;
				}
			}

			WriteInt64Slow(writer, value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteInt32(ref TupleSpanWriter writer, in int? value)
			=> value is null ? writer.TryWriteNil() : TryWriteInt32(ref writer, value.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteInt32(this TupleWriter writer, int? value)
		{
			if (!value.HasValue) WriteNil(writer); else WriteInt32(writer, value.Value);
		}

		#endregion

		#region Int64...

		/// <summary>Writes an Int64 at the end, and advance the cursor</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Signed QWORD, 64 bits, High Endian</param>
		public static bool TryWriteInt64(ref TupleSpanWriter writer, in long value)
		{
			if (value <= 255)
			{
				if (value == 0)
				{ // zero
					return writer.TryWriteLiteral(TupleTypes.IntZero);
				}

				if (value > 0)
				{ // 1..255: frequent for array index
					return writer.TryWriteLiteral(TupleTypes.IntPos1, (byte) value);
				}

				if (value > -256)
				{ // -255..-1
					return writer.TryWriteLiteral(TupleTypes.IntNeg1, (byte) (255 + value));
				}
			}

			return TryWriteInt64Slow(ref writer, value);
		}

		/// <summary>Writes an Int64 at the end, and advance the cursor</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Signed QWORD, 64 bits, High Endian</param>
		public static void WriteInt64(this TupleWriter writer, long value)
		{
			if (value <= 255)
			{
				if (value == 0)
				{ // zero
					writer.Output.WriteByte(TupleTypes.IntZero);
					return;
				}

				if (value > 0)
				{ // 1..255: frequent for array index
					writer.Output.WriteBytes(TupleTypes.IntPos1, (byte) value);
					return;
				}

				if (value > -256)
				{ // -255..-1
					writer.Output.WriteBytes(TupleTypes.IntNeg1, (byte) (255 + value));
					return;
				}
			}

			WriteInt64Slow(writer, value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteInt64(ref TupleSpanWriter writer, in long? value)
			=> value is null ? writer.TryWriteNil() : TryWriteInt64(ref writer, value.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteInt64(this TupleWriter writer, long? value)
		{
			if (!value.HasValue) WriteNil(writer); else WriteInt64(writer, value.Value);
		}

		private static bool TryWriteInt64Slow(ref TupleSpanWriter writer, long value)
		{
			// we are only called for values <= -256 or >= 256

			// determine the number of bytes needed to encode the absolute value
			int bytes = NumberOfBytes(value);

			if (!writer.TryAllocateSpan(bytes + 1, out var buffer))
			{
				return false;
			}

			int p = 0;

			ulong v;
			if (value > 0)
			{ // simple case
				buffer[p++] = (byte) (TupleTypes.IntZero + bytes);
				v = (ulong) value;
			}
			else
			{ // we will encode the one's complement of the absolute value
				// -1 => 0xFE
				// -256 => 0xFFFE
				// -65536 => 0xFFFFFE
				buffer[p++] = (byte) (TupleTypes.IntZero - bytes);
				v = (ulong) (~(-value));
			}

			if (bytes > 0)
			{
				// head
				--bytes;
				int shift = bytes << 3;

				while (bytes-- > 0)
				{
					buffer[p++] = (byte) (v >> shift);
					shift -= 8;
				}
				// last
				buffer[p++] = (byte) v;
			}

			Contract.Debug.Ensures(p == buffer.Length);

			return true;
		}

		private static void WriteInt64Slow(TupleWriter writer, long value)
		{
			// we are only called for values <= -256 or >= 256

			// determine the number of bytes needed to encode the absolute value
			int bytes = NumberOfBytes(value);

			var buffer = writer.Output.EnsureBytes(bytes + 1);
			int p = writer.Output.Position;

			ulong v;
			if (value > 0)
			{ // simple case
				buffer[p++] = (byte)(TupleTypes.IntZero + bytes);
				v = (ulong)value;
			}
			else
			{ // we will encode the one's complement of the absolute value
				// -1 => 0xFE
				// -256 => 0xFFFE
				// -65536 => 0xFFFFFE
				buffer[p++] = (byte)(TupleTypes.IntZero - bytes);
				v = (ulong)(~(-value));
			}

			if (bytes > 0)
			{
				// head
				--bytes;
				int shift = bytes << 3;

				while (bytes-- > 0)
				{
					buffer[p++] = (byte)(v >> shift);
					shift -= 8;
				}
				// last
				buffer[p++] = (byte)v;
			}
			writer.Output.Position = p;
		}

		#endregion

		#region UInt32...

		/// <summary>Writes an UInt32 at the end, and advance the cursor</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Signed DWORD, 32 bits, High Endian</param>
		public static bool TryWriteUInt32(ref TupleSpanWriter writer, in uint value)
		{
			if (value <= 255)
			{
				if (value == 0)
				{ // 0
					return writer.TryWriteLiteral(TupleTypes.IntZero);
				}
				else
				{ // 1..255
					return writer.TryWriteLiteral(TupleTypes.IntPos1, (byte)value);
				}
			}
			else
			{ // >= 256
				return TryWriteUInt64Slow(ref writer, value);
			}
		}

		/// <summary>Writes an UInt32 at the end, and advance the cursor</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Signed DWORD, 32 bits, High Endian</param>
		public static void WriteUInt32(this TupleWriter writer, uint value)
		{
			if (value <= 255)
			{
				if (value == 0)
				{ // 0
					writer.Output.WriteByte(TupleTypes.IntZero);
				}
				else
				{ // 1..255
					writer.Output.WriteBytes(TupleTypes.IntPos1, (byte)value);
				}
			}
			else
			{ // >= 256
				WriteUInt64Slow(writer, value);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteUInt32(ref TupleSpanWriter writer, in uint? value)
			=> value is null ? writer.TryWriteNil() : TryWriteUInt32(ref writer, value.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteUInt32(this TupleWriter writer, uint? value)
		{
			if (!value.HasValue) WriteNil(writer); else WriteUInt32(writer, value.Value);
		}

		#endregion

		#region UInt64...

		/// <summary>Writes an UInt64 at the end, and advance the cursor</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Signed QWORD, 64 bits, High Endian</param>
		public static bool TryWriteUInt64(ref TupleSpanWriter writer, in ulong value)
		{
			if (value <= 255)
			{
				if (value == 0)
				{ // 0
					return writer.TryWriteLiteral(TupleTypes.IntZero);
				}
				else
				{ // 1..255
					return writer.TryWriteLiteral(TupleTypes.IntPos1, (byte) value);
				}
			}
			else
			{ // >= 256
				return TryWriteUInt64Slow(ref writer, value);
			}
		}

		/// <summary>Writes an UInt64 at the end, and advance the cursor</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Signed QWORD, 64 bits, High Endian</param>
		public static void WriteUInt64(this TupleWriter writer, ulong value)
		{
			if (value <= 255)
			{
				if (value == 0)
				{ // 0
					writer.Output.WriteByte(TupleTypes.IntZero);
				}
				else
				{ // 1..255
					writer.Output.WriteBytes(TupleTypes.IntPos1, (byte) value);
				}
			}
			else
			{ // >= 256
				WriteUInt64Slow(writer, value);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteUInt64(ref TupleSpanWriter writer, in ulong? value)
			=> value is null ? writer.TryWriteNil() : TryWriteUInt64(ref writer, value.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteUInt64(this TupleWriter writer, ulong? value)
		{
			if (!value.HasValue) WriteNil(writer); else WriteUInt64(writer, value.Value);
		}

		private static bool TryWriteUInt64Slow(ref TupleSpanWriter writer, ulong value)
		{
			// We are only called for values >= 256

			// determine the number of bytes needed to encode the value
			int bytes = NumberOfBytes(value);

			if (!writer.TryAllocateSpan(bytes + 1, out var buffer))
			{
				return false;
			}
			int p = 0;

			// simple case (ulong can only be positive)
			buffer[p++] = (byte) (TupleTypes.IntZero + bytes);

			if (bytes > 0)
			{
				// head
				--bytes;
				int shift = bytes << 3;

				while (bytes-- > 0)
				{
					buffer[p++] = (byte)(value >> shift);
					shift -= 8;
				}
				// last
				buffer[p++] = (byte)  value;
			}

			Contract.Debug.Ensures(p == buffer.Length);

			return true;
		}

		private static void WriteUInt64Slow(this TupleWriter writer, ulong value)
		{
			// We are only called for values >= 256

			// determine the number of bytes needed to encode the value
			int bytes = NumberOfBytes(value);

			var buffer = writer.Output.EnsureBytes(bytes + 1);
			int p = writer.Output.Position;

			// simple case (ulong can only be positive)
			buffer[p++] = (byte) (TupleTypes.IntZero + bytes);

			if (bytes > 0)
			{
				// head
				--bytes;
				int shift = bytes << 3;

				while (bytes-- > 0)
				{
					buffer[p++] = (byte)(value >> shift);
					shift -= 8;
				}
				// last
				buffer[p++] = (byte)value;
			}

			writer.Output.Position = p;
		}

		#endregion

		#region Single...

		/// <summary>Writes a Single at the end, and advance the cursor</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">IEEE Floating point, 32 bits, High Endian</param>
		public static bool TryWriteSingle(ref TupleSpanWriter writer, in float value)
		{
			// The double is converted to its Big-Endian IEEE binary representation
			// - If the sign bit is set, flip all the bits
			// - If the sign bit is not set, just flip the sign bit
			// This ensures that all negative numbers have their first byte < 0x80, and all positive numbers have their first byte >= 0x80

			// Special case for NaN: All variants are normalized to float.NaN !
			float f = float.IsNaN(value) ? float.NaN : value;
			uint bits = BitConverter.SingleToUInt32Bits(f);

			if ((bits & 0x80000000U) != 0)
			{ // negative
				bits = ~bits;
			}
			else
			{ // positive
				bits |= 0x80000000U;
			}

			if (!writer.TryAllocateSpan(5, out var buffer))
			{
				return false;
			}
			buffer[0] = TupleTypes.Single;
			BinaryPrimitives.WriteUInt32BigEndian(buffer[1..], bits);

			return true;
		}

		/// <summary>Writes a Single at the end, and advance the cursor</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">IEEE Floating point, 32 bits, High Endian</param>
		public static void WriteSingle(this TupleWriter writer, float value)
		{
			// The double is converted to its Big-Endian IEEE binary representation
			// - If the sign bit is set, flip all the bits
			// - If the sign bit is not set, just flip the sign bit
			// This ensures that all negative numbers have their first byte < 0x80, and all positive numbers have their first byte >= 0x80

			// Special case for NaN: All variants are normalized to float.NaN !
			if (float.IsNaN(value)) value = float.NaN;

			// note: there is no BitConverter.SingleToInt32Bits(...), so we have to do it ourselves...
			uint bits;
			unsafe { bits = *((uint*)&value); }

			if ((bits & 0x80000000U) != 0)
			{ // negative
				bits = ~bits;
			}
			else
			{ // positive
				bits |= 0x80000000U;
			}
			var buffer = writer.Output.Allocate(5);
			buffer[0] = TupleTypes.Single;
			BinaryPrimitives.WriteUInt32BigEndian(buffer[1..], bits);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteSingle(ref TupleSpanWriter writer, in float? value)
			=> value is null ? writer.TryWriteNil() : TryWriteSingle(ref writer, value.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteSingle(this TupleWriter writer, float? value)
		{
			if (!value.HasValue) WriteNil(writer); else WriteSingle(writer, value.Value);
		}

		#endregion

		#region Double...

		/// <summary>Writes a Double at the end, and advance the cursor</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">IEEE Floating point, 64 bits, High Endian</param>
		public static bool TryWriteDouble(ref TupleSpanWriter writer, in double value)
		{
			// The double is converted to its Big-Endian IEEE binary representation
			// - If the sign bit is set, flip all the bits
			// - If the sign bit is not set, just flip the sign bit
			// This ensures that all negative numbers have their first byte < 0x80, and all positive numbers have their first byte >= 0x80

			// Special case for NaN: All variants are normalized to float.NaN !
			var d = double.IsNaN(value) ? double.NaN : value;
			ulong bits = BitConverter.DoubleToUInt64Bits(d);

			if ((bits & 0x8000000000000000UL) != 0)
			{ // negative
				bits = ~bits;
			}
			else
			{ // positive
				bits |= 0x8000000000000000UL;
			}

			if (!writer.TryAllocateSpan(9, out var buffer))
			{
				return false;
			}
			buffer[0] = TupleTypes.Double;
			BinaryPrimitives.WriteUInt64BigEndian(buffer[1..], bits);

			return true;
		}

		/// <summary>Writes a Double at the end, and advance the cursor</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">IEEE Floating point, 64 bits, High Endian</param>
		public static void WriteDouble(this TupleWriter writer, double value)
		{
			// The double is converted to its Big-Endian IEEE binary representation
			// - If the sign bit is set, flip all the bits
			// - If the sign bit is not set, just flip the sign bit
			// This ensures that all negative numbers have their first byte < 0x80, and all positive numbers have their first byte >= 0x80

			// Special case for NaN: All variants are normalized to float.NaN !
			if (double.IsNaN(value)) value = double.NaN;

			// note: we could use BitConverter.DoubleToInt64Bits(...), but it does the same thing, and also it does not exist for floats...
			ulong bits;
			unsafe { bits = *((ulong*)&value); }

			if ((bits & 0x8000000000000000UL) != 0)
			{ // negative
				bits = ~bits;
			}
			else
			{ // positive
				bits |= 0x8000000000000000UL;
			}
			var buffer = writer.Output.AllocateSpan(9);
			buffer[0] = TupleTypes.Double;
			BinaryPrimitives.WriteUInt64BigEndian(buffer[1..], bits);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteDouble(ref TupleSpanWriter writer, in double? value)
			=> value is null ? writer.TryWriteNil() : TryWriteDouble(ref writer, value.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteDouble(this TupleWriter writer, double? value)
		{
			if (!value.HasValue) WriteNil(writer); else WriteDouble(writer, value.Value);
		}

		#endregion

		public static bool TryWriteQuadruple(ref TupleSpanWriter writer, in decimal value)
		{
			//TODO: implement with when Decimal128 is available (in .NET 11)
			throw new NotSupportedException();
		}

		public static void WriteQuadruple(this TupleWriter writer, decimal value)
		{
			//TODO: implement with when Decimal128 is available (in .NET 11)
			throw new NotSupportedException();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteQuadruple(ref TupleSpanWriter writer, in decimal? value)
			=> value is null ? writer.TryWriteNil() : TryWriteQuadruple(ref writer, value.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteQuadruple(this TupleWriter writer, decimal? value)
		{
			if (!value.HasValue) WriteNil(writer); else WriteQuadruple(writer, value.Value);
		}

#if NET8_0_OR_GREATER

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteInt128(this TupleWriter writer, Int128 value)
		{
			if (value >= 0)
			{
				WriteUInt128(writer, (UInt128) value);
			}
			else
			{
				WriteNegativeInt128(writer, value);
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			static void WriteNegativeInt128(TupleWriter writer, Int128 value)
			{

				if (value >= long.MinValue)
				{
					WriteInt64(writer, (long) value);
					return;
				}

				// Negative values are stored as one's complement (-1 => 0xFE)
				// - -256 ➜ -1 => 0B 01 00 00 ➜ 0B 01 FE

				// To get a compact representation, we need to get rid of the leading "FF",
				// BUT, to key ordering, we also need to reverse the length!
				// - we format the number as the one's complement of the absolute value, so -1 will be ... FF FF FF FE
				// - we remove all the leading 'FF', so we end up with only 'FE', which would take one byte to encode
				// - we compute 256 - LENGTH = 255, and use 0xFF as the "count"

				// get the one's complement of the absolute value (-1 => ... FF FE)
				value = ~(-value);

				// write the value into a tmp buffer, so that we can extract the minimum length
				Span<byte> tmp = stackalloc byte[16];
				BinaryPrimitives.WriteInt128BigEndian(tmp, value);

				// count the number of leading zeros
				int p = tmp.IndexOfAnyExcept((byte) 0xFF);
				if (p > 0)
				{
					tmp = tmp[p..];
				}

				var buffer = writer.Output.AllocateSpan(1 + 1 + tmp.Length);
				buffer[0] = TupleTypes.NegativeBigInteger;
				buffer[1] = (byte) (tmp.Length ^ 0xFF);
				tmp.CopyTo(buffer[2..]);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteInt128(this TupleWriter writer, Int128? value)
		{
			if (!value.HasValue) WriteNil(writer); else WriteInt128(writer, value.Value);
		}

		public static void WriteUInt128(this TupleWriter writer, UInt128 value)
		{
			if (value == 0)
			{ // zero is encoded as a 0-length number
				writer.Output.WriteByte(TupleTypes.IntZero);
			}
			else if (value <= ulong.MaxValue)
			{ // "small big" integer fits in a single byte
				WriteUInt64(writer, (ulong) value);
			}
			else
			{
				WriteUInt128Slow(writer, value);
			}

			static void WriteUInt128Slow(TupleWriter writer, UInt128 value)
			{

				// write the value into a tmp buffer, so that we can extract the minimum length
				Span<byte> tmp = stackalloc byte[16];
				BinaryPrimitives.WriteUInt128BigEndian(tmp, value);

				// count the number of leading zeros
				int p = tmp.IndexOfAnyExcept((byte) 0);
				if (p > 0)
				{
					tmp = tmp[p..];
				}

				// we don't know the size yet, but the maximum length will be 18 bytes (header, count, 16 bytes)
				var buffer = writer.Output.AllocateSpan(1 + 1 + tmp.Length);
				buffer[0] = TupleTypes.PositiveBigInteger;
				buffer[1] = (byte) tmp.Length;
				tmp.CopyTo(buffer[2..]);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteUInt128(this TupleWriter writer, UInt128? value)
		{
			if (!value.HasValue) WriteNil(writer); else WriteUInt128(writer, value.Value);
		}

#endif

		public static bool TryWriteBigInteger(ref TupleSpanWriter writer, in BigInteger value)
		{
			// lala
			if (value.IsZero)
			{ // zero 
				return writer.TryWriteLiteral(TupleTypes.IntZero);
			}

			if (value.Sign >= 0)
			{
				if (value <= ulong.MaxValue)
				{ // "small big number"
					return TryWriteUInt64(ref writer, (ulong) value);
				}

				int count = value.GetByteCount();
				if (!writer.TryGetSpan(checked(2 + count), out var buffer))
				{
					return false;
				}

				value.TryWriteBytes(buffer[2..], out int written, isUnsigned: true, isBigEndian: true);

				buffer[0] = TupleTypes.PositiveBigInteger;
				buffer[1] = (byte) written;

				return writer.TryAdvance(2 + written);
			}
			else
			{
				if (value >= long.MinValue)
				{ // "small big number"
					return TryWriteInt64(ref writer, (long) value);
				}

				//TODO: handle the range between -(2^63 - 1) and -(2^64 - 1), we still are in "IntNeg8" !

				var minusOne = value - 1;

				int count = minusOne.GetByteCount();
				if (!writer.TryGetSpan(checked(2 + count), out var buffer))
				{
					return false;
				}

				minusOne.TryWriteBytes(buffer[2..], out int written, isUnsigned: false, isBigEndian: true);

				buffer[0] = TupleTypes.NegativeBigInteger;
				buffer[1] = (byte) (written ^ 0xFF);

				return writer.TryAdvance(2 + written);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteBigInteger(ref TupleSpanWriter writer, in BigInteger? value)
			=> value is null ? writer.TryWriteNil() : TryWriteBigInteger(ref writer, value.Value);

		public static void WriteBigInteger(this TupleWriter writer, BigInteger value)
		{
			// lala
			if (value.IsZero)
			{ // zero 
				writer.Output.WriteByte(TupleTypes.IntZero);
				return;
			}

			if (value.Sign >= 0)
			{
				if (value <= ulong.MaxValue)
				{ // "small big number"
					WriteUInt64(writer, (ulong) value);
					return;
				}

				int count = value.GetByteCount();
				var buffer = writer.Output.GetSpan(checked(2 + count));

				value.TryWriteBytes(buffer[2..], out int written, isUnsigned: true, isBigEndian: true);

				buffer[0] = TupleTypes.PositiveBigInteger;
				buffer[1] = (byte) written;

				writer.Output.Advance(2 + written);
			}
			else
			{
				if (value >= long.MinValue)
				{ // "small big number"
					WriteInt64(writer, (long) value);
					return;
				}

				//TODO: handle the range between -(2^63 - 1) and -(2^64 - 1), we still are in "IntNeg8" !

				--value;

				int count = value.GetByteCount();
				var buffer = writer.Output.GetSpan(checked(2 + count));

				value.TryWriteBytes(buffer[2..], out int written, isUnsigned: false, isBigEndian: true);

				buffer[0] = TupleTypes.NegativeBigInteger;
				buffer[1] = (byte) (written ^ 0xFF);

				writer.Output.Advance(2 + written);
			}

		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteBigInteger(this TupleWriter writer, BigInteger? value)
		{
			if (!value.HasValue) WriteNil(writer); else WriteBigInteger(writer, value.Value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteTimeSpan(ref TupleSpanWriter writer, in TimeSpan value)
		{
			// We have the same precision problem with storing DateTimes:
			// - Storing the number of ticks keeps the exact value, but is Windows-centric
			// - Storing the number of milliseconds as an integer will round the precision to 1 millisecond, which is not acceptable
			// - We could store the number of milliseconds as a floating point value, which would require support of Floating Points in the Tuple Encoding (currently a Draft)
			// - It is frequent for JSON APIs and other database engines to represent durations as a number of SECONDS, using a floating point number.

			// Right now, we will store the duration as the number of seconds, using a 64-bit float

			return TryWriteDouble(ref writer, value.TotalSeconds);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteTimeSpan(this TupleWriter writer, TimeSpan value)
		{
			// We have the same precision problem with storing DateTimes:
			// - Storing the number of ticks keeps the exact value, but is Windows-centric
			// - Storing the number of milliseconds as an integer will round the precision to 1 millisecond, which is not acceptable
			// - We could store the number of milliseconds as a floating point value, which would require support of Floating Points in the Tuple Encoding (currently a Draft)
			// - It is frequent for JSON APIs and other database engines to represent durations as a number of SECONDS, using a floating point number.

			// Right now, we will store the duration as the number of seconds, using a 64-bit float

			WriteDouble(writer, value.TotalSeconds);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteTimeSpan(ref TupleSpanWriter writer, in TimeSpan? value)
			=> value is null ? writer.TryWriteNil() : TryWriteTimeSpan(ref writer, value.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteTimeSpan(this TupleWriter writer, TimeSpan? value)
		{
			if (!value.HasValue) WriteNil(writer); else WriteTimeSpan(writer, value.Value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteDateTime(ref TupleSpanWriter writer, in DateTime value)
		{
			// The problem of serializing DateTime: TimeZone? Precision?
			// - Since we are going to lose the TimeZone infos anyway, we can just store everything in UTC and let the caller deal with it
			// - DateTime in .NET uses Ticks which produce numbers too large to fit in the 56 bits available in JavaScript
			// - Most other *nix uses the number of milliseconds since 1970-Jan-01 UTC, but if we store as an integer we will lose some precision (rounded to nearest millisecond)
			// - We could store the number of milliseconds as a floating point value, which would require support of Floating Points in the Tuple Encoding (currently a Draft)
			// - Other database engines store dates as a number of DAYS since Epoch, using a floating point number. This allows for quickly extracting the date by truncating the value, and the time by using the decimal part

			// Right now, we will store the date as the number of DAYS since Epoch, using a 64-bit float.
			// => storing a number of ticks would be MS-only anyway (56-bit limit in JS)
			// => JS binding MAY support decoding of 64-bit floats in the future, in which case the value would be preserved exactly.

			const long UNIX_EPOCH_EPOCH = 621355968000000000L;
			return TryWriteDouble(ref writer, (value.ToUniversalTime().Ticks - UNIX_EPOCH_EPOCH) / (double) TimeSpan.TicksPerDay);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteDateTime(this TupleWriter writer, DateTime value)
		{
			// The problem of serializing DateTime: TimeZone? Precision?
			// - Since we are going to lose the TimeZone infos anyway, we can just store everything in UTC and let the caller deal with it
			// - DateTime in .NET uses Ticks which produce numbers too large to fit in the 56 bits available in JavaScript
			// - Most other *nix uses the number of milliseconds since 1970-Jan-01 UTC, but if we store as an integer we will lose some precision (rounded to nearest millisecond)
			// - We could store the number of milliseconds as a floating point value, which would require support of Floating Points in the Tuple Encoding (currently a Draft)
			// - Other database engines store dates as a number of DAYS since Epoch, using a floating point number. This allows for quickly extracting the date by truncating the value, and the time by using the decimal part

			// Right now, we will store the date as the number of DAYS since Epoch, using a 64-bit float.
			// => storing a number of ticks would be MS-only anyway (56-bit limit in JS)
			// => JS binding MAY support decoding of 64-bit floats in the future, in which case the value would be preserved exactly.

			const long UNIX_EPOCH_EPOCH = 621355968000000000L;
			WriteDouble(writer, (value.ToUniversalTime().Ticks - UNIX_EPOCH_EPOCH) / (double) TimeSpan.TicksPerDay);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteDateTime(ref TupleSpanWriter writer, in DateTime? value)
			=> value is null ? writer.TryWriteNil() : TryWriteDateTime(ref writer, value.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteDateTime(this TupleWriter writer, DateTime? value)
		{
			if (!value.HasValue) WriteNil(writer); else WriteDateTime(writer, value.Value);
		}

		/// <summary>Writes a DateTimeOffset converted to the number of days since the Unix Epoch and stored as a 64-bit decimal</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteDateTimeOffset(ref TupleSpanWriter writer, in DateTimeOffset value)
		{
			// The problem of serializing DateTimeOffset: TimeZone? Precision?
			// - Since we are going to lose the TimeZone infos anyway, we can just store everything in UTC and let the caller deal with it
			// - DateTimeOffset in .NET uses Ticks which produce numbers too large to fit in the 56 bits available in JavaScript
			// - Most other *nix uses the number of milliseconds since 1970-Jan-01 UTC, but if we store as an integer we will lose some precision (rounded to nearest millisecond)
			// - We could store the number of milliseconds as a floating point value, which would require support of Floating Points in the Tuple Encoding (currently a Draft)
			// - Other database engines store dates as a number of DAYS since Epoch, using a floating point number. This allows for quickly extracting the date by truncating the value, and the time by using the decimal part

			// Right now, we will store the date as the number of DAYS since Epoch, using a 64-bit float.
			// => storing a number of ticks would be MS-only anyway (56-bit limit in JS)
			// => JS binding MAY support decoding of 64-bit floats in the future, in which case the value would be preserved exactly.

			//REVIEW: why not use an embedded tuple: (ElapsedDays, TimeZoneOffset) ?
			// - pros: keeps the timezone offset
			// - cons: would not be compatible with DateTime

			const long UNIX_EPOCH_EPOCH = 621355968000000000L;
			return TryWriteDouble(ref writer, (value.ToUniversalTime().Ticks - UNIX_EPOCH_EPOCH) / (double) TimeSpan.TicksPerDay);
		}

		/// <summary>Writes a DateTimeOffset converted to the number of days since the Unix Epoch and stored as a 64-bit decimal</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteDateTimeOffset(this TupleWriter writer, DateTimeOffset value)
		{
			// The problem of serializing DateTimeOffset: TimeZone? Precision?
			// - Since we are going to lose the TimeZone infos anyway, we can just store everything in UTC and let the caller deal with it
			// - DateTimeOffset in .NET uses Ticks which produce numbers too large to fit in the 56 bits available in JavaScript
			// - Most other *nix uses the number of milliseconds since 1970-Jan-01 UTC, but if we store as an integer we will lose some precision (rounded to nearest millisecond)
			// - We could store the number of milliseconds as a floating point value, which would require support of Floating Points in the Tuple Encoding (currently a Draft)
			// - Other database engines store dates as a number of DAYS since Epoch, using a floating point number. This allows for quickly extracting the date by truncating the value, and the time by using the decimal part

			// Right now, we will store the date as the number of DAYS since Epoch, using a 64-bit float.
			// => storing a number of ticks would be MS-only anyway (56-bit limit in JS)
			// => JS binding MAY support decoding of 64-bit floats in the future, in which case the value would be preserved exactly.

			//REVIEW: why not use an embedded tuple: (ElapsedDays, TimeZoneOffset) ?
			// - pros: keeps the timezone offset
			// - cons: would not be compatible with DateTime

			const long UNIX_EPOCH_EPOCH = 621355968000000000L;
			WriteDouble(writer, (value.ToUniversalTime().Ticks - UNIX_EPOCH_EPOCH) / (double) TimeSpan.TicksPerDay);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteDateTimeOffset(ref TupleSpanWriter writer, in DateTimeOffset? value)
			=> value is null ? writer.TryWriteNil() : TryWriteDateTimeOffset(ref writer, value.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteDateTimeOffset(this TupleWriter writer, DateTimeOffset? value)
		{
			if (value is null) WriteNil(writer); else WriteDateTimeOffset(writer, value.Value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteInstant(this TupleWriter writer, NodaTime.Instant value)
			=> WriteDateTime(writer, value.ToDateTimeUtc());

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteInstant(this TupleWriter writer, NodaTime.Instant? value)
		{
			if (!value.HasValue) WriteNil(writer); else WriteDateTime(writer, value.Value.ToDateTimeUtc());
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteInstant(ref TupleSpanWriter writer, in NodaTime.Instant value)
			=> TryWriteDateTime(ref writer, value.ToDateTimeUtc());

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteInstant(ref TupleSpanWriter writer, in NodaTime.Instant? value)
			=> value is null ? writer.TryWriteNil() : TryWriteDateTimeOffset(ref writer, value.Value.ToDateTimeUtc());

		/// <summary>Writes a string encoded in UTF-8</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteString(ref TupleSpanWriter writer, in string? value)
			=> value is null ? writer.TryWriteNil() : TryWriteString(ref writer, value.AsSpan());

		/// <summary>Writes a string encoded in UTF-8</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteString(ref TupleSpanWriter writer, in ReadOnlyMemory<char> value)
			=> TryWriteString(ref writer, value.Span);

		/// <summary>Writes a string encoded in UTF-8</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteString(ref TupleSpanWriter writer, in Memory<char> value)
			=> TryWriteString(ref writer, value.Span);

		/// <summary>Writes a string encoded in UTF-8</summary>
		public static bool TryWriteString(ref TupleSpanWriter writer, in ReadOnlySpan<char> value)
		{

			if (value.Length == 0)
			{ // "02 00"
				return writer.TryWriteLiteral(TupleTypes.Utf8, 0);
			}

			if (!writer.TryWriteLiteral(TupleTypes.Utf8))
			{
				return false;
			}

			// only \0 can produce a 0x00 when encoded to UTF-8. Any other codepoint will not produce a 0
			// which means that we need to worry about escaping only if the string contains a '\0' character

			var remaining = value;
			do
			{
				int p = remaining.IndexOf('\0');

				if (!writer.TryGetTail(out var tail) || !Encoding.UTF8.TryGetBytes(p < 0 ? remaining : remaining[..p], tail, out var written))
				{
					return false;
				}
				writer.AdvanceUnsafe(written);

				if (p < 0)
				{
					break;
				}

				if (!writer.TryWriteLiteral(0x00, 0xFF))
				{
					return false;
				}

				remaining = remaining[(p + 1)..];
			}
			while (remaining.Length > 0);

			return writer.TryWriteLiteral(0);
		}

		/// <summary>Writes a string encoded in UTF-8</summary>
		public static unsafe void WriteString(this TupleWriter writer, string? value)
		{
			if (value == null)
			{ // "00"
				WriteNil(writer);
			}
			else
			{
				WriteString(writer, value.AsSpan());
			}
		}

		/// <summary>Writes a string encoded in UTF-8</summary>
		public static void WriteString(this TupleWriter writer, ReadOnlyMemory<char> value)
			=> WriteString(writer, value.Span);

		/// <summary>Writes a string encoded in UTF-8</summary>
		public static void WriteString(this TupleWriter writer, Memory<char> value)
			=> WriteString(writer, value.Span);

		/// <summary>Writes a string encoded in UTF-8</summary>
		public static void WriteString(this TupleWriter writer, ReadOnlySpan<char> value)
		{
			if (value.Length == 0)
			{ // "02 00"
				writer.Output.WriteBytes(TupleTypes.Utf8, 0x00);
			}
			else
			{
				if (!TryWriteUnescapedUtf8String(writer, value))
				{ // the string contains \0 chars, we need to do it the hard way
					WriteStringSlow(ref writer, value);
				}
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			static void WriteStringSlow(ref TupleWriter writer, ReadOnlySpan<char> value)
			{
				const int MAX_STACK_CAPACITY = 256;

				// we need a tmp buffer to encode the string into UTF-8 bytes,
				int capacity = Encoding.UTF8.GetByteCount(value);

				if (capacity <= MAX_STACK_CAPACITY)
				{ // use the stack
					Span<byte> buffer = stackalloc byte[capacity];
					int writtenBytes = Encoding.UTF8.GetBytes(value, buffer);
					WriteNulEscapedBytes(writer, TupleTypes.Utf8, buffer[..writtenBytes]);
				}
				else
				{ // use a pooled buffer
					byte[] buffer = ArrayPool<byte>.Shared.Rent(capacity);
					try
					{
						int writtenBytes = Encoding.UTF8.GetBytes(value, buffer);
						WriteNulEscapedBytes(writer, TupleTypes.Utf8, buffer.AsSpan(0, writtenBytes));
					}
					finally
					{
						ArrayPool<byte>.Shared.Return(buffer, true);
					}
				}
			}
		}

		private static unsafe void WriteUnescapedAsciiChars(this TupleWriter writer, char* chars, int count)
		{
			//PERF: TODO: change this to use ReadOnlySpan<char>

			Contract.Debug.Requires(chars != null && count >= 0);

			// copy and convert an ASCII string directly into the destination buffer

			var buffer = writer.Output.EnsureBytes(2 + count);
			int pos = writer.Output.Position;
			char* end = chars + count;
			fixed (byte* outPtr = buffer)
			{
				outPtr[pos++] = TupleTypes.Utf8;
				//OPTIMIZE: copy 2 or 4 chars at once, unroll loop?
				while(chars < end)
				{
					outPtr[pos++] = (byte)(*chars++);
				}
				outPtr[pos++] = 0x00;
				writer.Output.Position = pos;
			}
		}

		private static bool TryWriteUnescapedUtf8String(this TupleWriter writer, ReadOnlySpan<char> chars)
		{
			unsafe
			{
				fixed (char* ptr = chars)
				{
					return TryWriteUnescapedUtf8String(writer, ptr, chars.Length);
				}
			}
		}
		private static unsafe bool TryWriteUnescapedUtf8String(this TupleWriter writer, char* chars, int count)
		{
			//PERF: TODO: change this to use ReadOnlySpan<char>

			// Several observations:
			// * Most strings will be keywords or ASCII-only with no zeroes. These can be copied directly to the buffer
			// * We will only attempt to optimize strings that don't have any 00 to escape to 00 FF. For these, we will fall back to converting to byte[] then escaping.
			// * Since .NET's strings are UTF-16, the max possible UNICODE value to encode is 0xFFFF, which takes 3 bytes in UTF-8 (0xEFBFBF)
			// * Most western europe languages have only a few non-ASCII chars here and there, and most of them will only use 2 bytes (ex: 'é' => 'C3 A9')
			// * More complex scripts with dedicated symbol pages (Kanji, Arabic, ....) will take 2 or 3 bytes for each character.

			// We will first do a pass to check for the presence of 00 and non-ASCII chars
			// => if we find at least on 00, we fall back to escaping the result of Encoding.UTF8.GetBytes()
			// => if we find only ASCII (1..127) chars, we have an optimized path that will truncate the chars to bytes
			// => if not, we will use an UTF8Encoder to convert the string to UTF-8, in chunks, using a small buffer allocated on the stack

			#region First pass: look for \0 and non-ASCII chars

			// fastest way to check for non-ASCII, is to OR all the chars together, and look at bits 7 to 15. If they are not all zero, there is at least ONE non-ASCII char.
			// also, we abort as soon as we find a \0

			char* ptr = chars;
			char* end = chars + count;
			char mask = '\0', c;
			while (ptr < end && (c = *ptr) != '\0') { mask |= c; ++ptr; }

			if (ptr < end) return false; // there is at least one \0 in the string

			// bit 7-15 all unset means the string is pure ASCII
			if ((mask >> 7) == 0)
			{ // => directly dump the chars to the buffer
				WriteUnescapedAsciiChars(writer, chars, count);
				return true;
			}

			#endregion

			#region Second pass: encode the string to UTF-8, in chunks

			// Here we know that there is at least one unicode char, and that there are no \0
			// We will iterate through the string, filling as much of the buffer as possible

			bool done;
			int remaining = count;
			ptr = chars;

			// We need at most 3 * CHUNK_SIZE to encode the chunk
			// > For small strings, we will allocated exactly string.Length * 3 bytes, and will be done in one chunk
			// > For larger strings, we will call encoder.Convert(...) until it says it is done.
			const int CHUNK_SIZE = 1024;
			int bufLen = Encoding.UTF8.GetMaxByteCount(Math.Min(count, CHUNK_SIZE));
			byte* buf = stackalloc byte[bufLen];

			// We can not really predict the final size of the encoded string, but:
			// * Western languages have a few chars that usually need 2 bytes. If we pre-allocate 50% more bytes, it should fit most of the time, without too much waste
			// * Eastern languages will have all chars encoded to 3 bytes. If we also pre-allocated 50% more, we should only need one resize of the buffer (150% x 2 = 300%), which is acceptable
			writer.Output.EnsureBytes(checked(2 + count + (count >> 1))); // preallocate 150% of the string + 2 bytes
			writer.Output.UnsafeWriteByte(TupleTypes.Utf8);

			var encoder = Encoding.UTF8.GetEncoder();
			// note: encoder.Convert() tries to fill up the buffer as much as possible with complete chars, and will set 'done' to true when all chars have been converted.
			do
			{
				encoder.Convert(ptr, remaining, buf, bufLen, true, out int charsUsed, out int bytesUsed, out done);
				if (bytesUsed > 0)
				{
					writer.Output.WriteBytes(new ReadOnlySpan<byte>(buf, bytesUsed));
				}
				remaining -= charsUsed;
				ptr += charsUsed;
			}
			while (!done);
			Contract.Debug.Assert(remaining == 0 && ptr == end);

			// close the string
			writer.Output.WriteByte(0x00);

			#endregion

			return true;
		}

		/// <summary>Writes a char encoded in UTF-8</summary>
		public static bool TryWriteChar(ref TupleSpanWriter writer, in char value)
		{
			if (value == 0)
			{ // NUL => "00 0F"
				// note: \0 is the only unicode character that will produce a zero byte when converted in UTF-8
				return writer.TryWriteLiteral([ TupleTypes.Utf8, 0x00, 0xFF, 0x00 ]);
			}

			var c = value;
			return TryWriteString(ref writer, MemoryMarshal.CreateSpan(ref c, 1));
		}

		/// <summary>Writes a char encoded in UTF-8</summary>
		public static void WriteChar(this TupleWriter writer, char value)
		{
			if (value == 0)
			{ // NUL => "00 0F"
				// note: \0 is the only unicode character that will produce a zero byte when converted in UTF-8
				writer.Output.WriteBytes(TupleTypes.Utf8, 0x00, 0xFF, 0x00);
			}
			else if (value < 0x80)
			{ // 0x00..0x7F => 0xxxxxxx
				writer.Output.WriteBytes(TupleTypes.Utf8, (byte)value, 0x00);
			}
			else if (value <  0x800)
			{ // 0x80..0x7FF => 110xxxxx 10xxxxxx => two bytes
				writer.Output.WriteBytes(TupleTypes.Utf8, (byte)(0xC0 | (value >> 6)), (byte)(0x80 | (value & 0x3F)), 0x00);
			}
			else
			{ // 0x800..0xFFFF => 11110xxx 10xxxxxx 10xxxxxx 10xxxxxx
				// note: System.Char is 16 bits, and thus cannot represent UNICODE chars above 0xFFFF.
				// => This means that a System.Char will never take more than 3 bytes in UTF-8 !
				var tmp = Encoding.UTF8.GetBytes(new string(value, 1));
				writer.Output.EnsureBytes(tmp.Length + 2);
				writer.Output.UnsafeWriteByte(TupleTypes.Utf8);
				writer.Output.UnsafeWriteBytes(tmp.AsSpan());
				writer.Output.UnsafeWriteByte(0x00);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteChar(ref TupleSpanWriter writer, in char? value)
			=> value is null ? writer.TryWriteNil() : TryWriteChar(ref writer, value.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteChar(this TupleWriter writer, char? value)
		{
			if (!value.HasValue) WriteNil(writer); else WriteChar(writer, value.Value);
		}

		/// <summary>Writes a binary string</summary>
		public static bool TryWriteBytes(ref TupleSpanWriter writer, in byte[]? value)
			=> value is null
				? writer.TryWriteNil()
				: TryWriteBytes(ref writer, value.AsSpan());

		/// <summary>Writes a binary string</summary>
		public static bool TryWriteBytes(ref TupleSpanWriter writer, in ReadOnlyMemory<byte> value)
			=> TryWriteBytes(ref writer, value.Span);

		/// <summary>Writes a binary string</summary>
		public static bool TryWriteBytes(ref TupleSpanWriter writer, in Memory<byte> value)
			=> TryWriteBytes(ref writer, value.Span);

		/// <summary>Writes a binary string</summary>
		public static void WriteBytes(this TupleWriter writer, byte[]? value)
		{
			if (value is null)
			{
				WriteNil(writer);
			}
			else
			{
				WriteNulEscapedBytes(writer, TupleTypes.Bytes, value.AsSpan());
			}
		}

		/// <summary>Writes a binary string</summary>
		public static void WriteBytes(this TupleWriter writer, ReadOnlyMemory<byte> value)
			=> WriteNulEscapedBytes(writer, TupleTypes.Bytes, value.Span);

		/// <summary>Writes a binary string</summary>
		public static void WriteBytes(this TupleWriter writer, Memory<byte> value)
			=> WriteNulEscapedBytes(writer, TupleTypes.Bytes, value.Span);

		/// <summary>Writes a binary string</summary>
		public static bool TryWriteBytes(ref TupleSpanWriter writer, in ArraySegment<byte> value)
		{
			if (value.Count == 0 && value.Array == null)
			{ // default(ArraySegment<byte>) ~= null
				return writer.TryWriteNil();
			}
			else
			{
				return TryWriteNulEscapedBytes(ref writer, TupleTypes.Bytes, value.AsSpan());
			}
		}

		/// <summary>Writes a binary string</summary>
		public static bool TryWriteBytes(ref TupleSpanWriter writer, in ArraySegment<byte>? value)
			=> value is null ? writer.TryWriteNil() : TryWriteBytes(ref writer, value.Value);

		/// <summary>Writes a binary string</summary>
		public static void WriteBytes(this TupleWriter writer, ArraySegment<byte> value)
		{
			if (value.Count == 0 && value.Array == null)
			{ // default(ArraySegment<byte>) ~= null
				WriteNil(writer);
			}
			else
			{
				WriteNulEscapedBytes(writer, TupleTypes.Bytes, value.AsSpan());
			}
		}

		/// <summary>Writes a binary string</summary>
		public static void WriteBytes(this TupleWriter writer, ArraySegment<byte>? value)
		{
			if (value is null) { WriteNil(writer); } else { WriteBytes(writer, value.Value); }
		}

		/// <summary>Writes a binary string</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteBytes(ref TupleSpanWriter writer, in Slice value)
			=> value.IsNull ? writer.TryWriteNil() : TryWriteNulEscapedBytes(ref writer, TupleTypes.Bytes, value.Span);

		/// <summary>Writes a binary string</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteBytes(ref TupleSpanWriter writer, in Slice? value)
			=> value is null || value.Value.IsNull ? writer.TryWriteNil() : TryWriteNulEscapedBytes(ref writer, TupleTypes.Bytes, value.Value.Span);

		/// <summary>Writes a binary string</summary>
		public static void WriteBytes(this TupleWriter writer, Slice value)
		{
			if (value.IsNull) { WriteNil(writer); } else { WriteNulEscapedBytes(writer, TupleTypes.Bytes, value.Span); }
		}

		/// <summary>Writes a binary string</summary>
		public static void WriteBytes(this TupleWriter writer, Slice? value)
		{
			if (value is null || value.Value.IsNull) { WriteNil(writer); } else { WriteNulEscapedBytes(writer, TupleTypes.Bytes, value.Value.Span); }
		}

		/// <summary>Writes a binary string</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteBytes(ref TupleSpanWriter writer, in ReadOnlySpan<byte> value)
		{
			return TryWriteNulEscapedBytes(ref writer, TupleTypes.Bytes, value);
		}

		/// <summary>Writes a binary string</summary>
		public static void WriteBytes(this TupleWriter writer, ReadOnlySpan<byte> value)
		{
			WriteNulEscapedBytes(writer, TupleTypes.Bytes, value);
		}

		/// <summary>Writes a buffer with all instances of 0 escaped as '00 FF'</summary>
		internal static bool TryWriteNulEscapedBytes(ref TupleSpanWriter writer, byte type, ReadOnlySpan<byte> value)
		{
			int len = value.Length;

			if (len == 0)
			{
				return writer.TryWriteLiteral(type, 0x00);
			}

			// we need to know if there are any NUL chars (\0) that need escaping...
			// (we will also need to add 1 byte to the buffer size per NUL)
			int numZeros = value.Count((byte) 0);
			len = checked(len + numZeros);

			if (!writer.TryGetSpan(len + 2, out var buffer))
			{
				return false;
			}

			if (numZeros == 0)
			{ // no NULs in the string, can copy all at once
				buffer[0] = type;
				value.CopyTo(buffer[1..]);
				buffer[value.Length + 1] = 0x00;
				writer.AdvanceUnsafe(value.Length + 2);
				return true;
			}
			
			// we need to escape all NULs
			buffer[0] = type;
			int p = 1;
			foreach(var b in value)
			{
				//TODO: optimize this!
				buffer[p++] = b;
				if (b == 0) buffer[p++] = 0xFF;
			}
			buffer[p++] = 0x00;

			Contract.Debug.Ensures(p == len + 2);

			writer.AdvanceUnsafe(p);

			return true;
		}

		/// <summary>Writes a buffer with all instances of 0 escaped as '00 FF'</summary>
		internal static void WriteNulEscapedBytes(this TupleWriter writer, byte type, ReadOnlySpan<byte> value)
		{
			int n = value.Length;

			// we need to know if there are any NUL chars (\0) that need escaping...
			// (we will also need to add 1 byte to the buffer size per NUL)
			foreach(var b in value)
			{
				//TODO: optimize this!
				if (b == 0) ++n;
			}

			var buffer = writer.Output.EnsureBytes(n + 2);
			int p = writer.Output.Position;
			buffer[p++] = type;
			if (n > 0)
			{
				if (n == value.Length)
				{ // no NULs in the string, can copy all at once
					value.CopyTo(buffer.AsSpan(p));
					p += n;
				}
				else
				{ // we need to escape all NULs
					foreach(var b in value)
					{
						//TODO: optimize this!
						buffer[p++] = b;
						if (b == 0) buffer[p++] = 0xFF;
					}
				}
			}
			buffer[p] = 0x00;
			writer.Output.Position = p + 1;
		}

		/// <summary>Writes an RFC 4122 encoded 16-byte Microsoft GUID</summary>
		public static bool TryWriteGuid(ref TupleSpanWriter writer, in Guid value)
		{
			if (!writer.TryAllocateSpan(17, out var span))
			{
				return false;
			}

			span[0] = TupleTypes.Uuid128;
			// Guids should be stored using the RFC 4122 standard, so we need to swap some parts of the System.Guid (handled by Uuid128)
			new Uuid128(value).WriteTo(span[1..]);

			return true;
		}

		/// <summary>Writes an RFC 4122 encoded 16-byte Microsoft GUID</summary>
		public static void WriteGuid(this TupleWriter writer, Guid value)
		{
			var span = writer.Output.AllocateSpan(17);
			span[0] = TupleTypes.Uuid128;
			// Guids should be stored using the RFC 4122 standard, so we need to swap some parts of the System.Guid (handled by Uuid128)
			new Uuid128(value).WriteTo(span.Slice(1));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteGuid(ref TupleSpanWriter writer, in Guid? value)
			=> value is null ? writer.TryWriteNil() : TryWriteGuid(ref writer, value.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteGuid(this TupleWriter writer, Guid? value)
		{
			if (!value.HasValue)
			{
				WriteNil(writer);
			}
			else
			{
				WriteGuid(writer, value.Value);
			}
		}

		/// <summary>Writes an RFC 4122 encoded 128-bit UUID</summary>
		public static bool TryWriteUuid128(ref TupleSpanWriter writer, in Uuid128 value)
		{
			if (!writer.TryAllocateSpan(17, out var span))
			{
				return false;
			}

			span[0] = TupleTypes.Uuid128;
			value.WriteTo(span[1..]);

			return true;
		}

		/// <summary>Writes an RFC 4122 encoded 128-bit UUID</summary>
		public static void WriteUuid128(this TupleWriter writer, Uuid128 value)
		{
			var span = writer.Output.AllocateSpan(17);
			span[0] = TupleTypes.Uuid128;
			value.WriteTo(span.Slice(1));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteUuid128(ref TupleSpanWriter writer, in Uuid128? value)
			=> value is null ? writer.TryWriteNil() : TryWriteUuid128(ref writer, value.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteUuid128(this TupleWriter writer, Uuid128? value)
		{
			if (!value.HasValue)
			{
				WriteNil(writer);
			}
			else
			{
				WriteUuid128(writer, value.Value);
			}
		}

		/// <summary>Writes a 96-bit UUID</summary>
		public static bool TryWriteUuid96(ref TupleSpanWriter writer, in Uuid96 value)
		{
			if (!writer.TryAllocateSpan(13, out var span))
			{
				return false;
			}

			span[0] = TupleTypes.Uuid96;
			value.WriteTo(span[1..]);

			return true;
		}

		/// <summary>Writes a 96-bit UUID</summary>
		public static void WriteUuid96(this TupleWriter writer, Uuid96 value)
		{
			var span = writer.Output.AllocateSpan(13);
			span[0] = TupleTypes.Uuid96;
			value.WriteTo(span.Slice(1));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteUuid96(ref TupleSpanWriter writer, in Uuid96? value)
			=> value is null ? writer.TryWriteNil() : TryWriteUuid96(ref writer, value.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteUuid96(this TupleWriter writer, Uuid96? value)
		{
			if (!value.HasValue)
			{
				WriteNil(writer);
			}
			else
			{
				WriteUuid96(writer, value.Value);
			}
		}

		/// <summary>Writes an 80-bit UUID</summary>
		public static bool TryWriteUuid80(ref TupleSpanWriter writer, in Uuid80 value)
		{
			if (!writer.TryAllocateSpan(11, out var span))
			{
				return false;
			}

			span[0] = TupleTypes.Uuid80;
			value.WriteTo(span[1..]);

			return true;
		}

		/// <summary>Writes an 80-bit UUID</summary>
		public static void WriteUuid80(this TupleWriter writer, Uuid80 value)
		{
			var span = writer.Output.AllocateSpan(11);
			span[0] = TupleTypes.Uuid80;
			value.WriteToUnsafe(span.Slice(1));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteUuid80(ref TupleSpanWriter writer, in Uuid80? value)
			=> value is null ? writer.TryWriteNil() : TryWriteUuid80(ref writer, value.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteUuid80(this TupleWriter writer, Uuid80? value)
		{
			if (!value.HasValue)
			{
				WriteNil(writer);
			}
			else
			{
				WriteUuid80(writer, value.Value);
			}
		}

		/// <summary>Writes a 64-bit UUID</summary>
		public static bool TryWriteUuid64(ref TupleSpanWriter writer, in Uuid64 value)
		{
			if (!writer.TryAllocateSpan(9, out var span))
			{
				return false;
			}

			span[0] = TupleTypes.Uuid64;
			value.WriteTo(span[1..]);

			return true;
		}

		/// <summary>Writes a 64-bit UUID</summary>
		public static void WriteUuid64(this TupleWriter writer, Uuid64 value)
		{
			var span = writer.Output.AllocateSpan(9);
			span[0] = TupleTypes.Uuid64;
			value.WriteToUnsafe(span.Slice(1));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteUuid64(ref TupleSpanWriter writer, in Uuid64? value)
			=> value is null ? writer.TryWriteNil() : TryWriteUuid64(ref writer, value.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteUuid64(this TupleWriter writer, Uuid64? value)
		{
			if (!value.HasValue)
			{
				WriteNil(writer);
			}
			else
			{
				WriteUuid64(writer, value.Value);
			}
		}

		/// <summary>Writes a 48-bit UUID</summary>
		public static bool TryWriteUuid48(ref TupleSpanWriter writer, in Uuid48 value) => TryWriteUInt64(ref writer, value.ToUInt64());

		/// <summary>Writes a 48-bit UUID</summary>
		public static void WriteUuid48(this TupleWriter writer, Uuid48 value) => WriteUInt64(writer, value.ToUInt64());

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteUuid48(ref TupleSpanWriter writer, in Uuid48? value)
			=> value is null ? writer.TryWriteNil() : TryWriteUuid48(ref writer, value.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteUuid48(this TupleWriter writer, Uuid48? value)
		{
			if (!value.HasValue)
			{
				WriteNil(writer);
			}
			else
			{
				WriteUInt64(writer, value.Value.ToUInt64());
			}
		}

		public static bool TryWriteVersionStamp(ref TupleSpanWriter writer, in VersionStamp value)
		{
			Span<byte> span;
			if (value.HasUserVersion)
			{ // 96-bits VersionStamp
				if (!writer.TryAllocateSpan(13, out span))
				{
					return false;
				}
				span[0] = TupleTypes.Uuid96;
			}
			else
			{ // 80-bits VersionStamp
				if (!writer.TryAllocateSpan(11, out span))
				{
					return false;
				}
				span[0] = TupleTypes.Uuid80;
			}

			VersionStamp.WriteUnsafe(span[1..], in value);
			return true;
		}

		public static void WriteVersionStamp(this TupleWriter writer, VersionStamp value)
		{
			if (value.HasUserVersion)
			{ // 96-bits VersionStamp
				var span = writer.Output.AllocateSpan(13);
				span[0] = TupleTypes.Uuid96;
				VersionStamp.WriteUnsafe(span.Slice(1), in value);
			}
			else
			{ // 80-bits VersionStamp
				var span = writer.Output.AllocateSpan(11);
				span[0] = TupleTypes.Uuid80;
				VersionStamp.WriteUnsafe(span.Slice(1), in value);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryWriteVersionStamp(ref TupleSpanWriter writer, in VersionStamp? value)
			=> value is null ? writer.TryWriteNil() : TryWriteVersionStamp(ref writer, value.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteVersionStamp(this TupleWriter writer, VersionStamp? value)
		{
			if (!value.HasValue)
			{
				WriteNil(writer);
			}
			else
			{
				WriteVersionStamp(writer, value.Value);
			}
		}

		public static bool TryWriteUserType(ref TupleSpanWriter writer, in TuPackUserType? value)
		{
			if (value == null)
			{
				return writer.TryWriteNil();
			}

			ref readonly Slice arg = ref value.Value;
			if (arg.Count == 0)
			{
				return writer.TryWriteLiteral((byte) value.Type);
			}

			return writer.TryWriteLiteral((byte) value.Type) && writer.TryWriteLiteral(arg.Span);
		}

		public static void WriteUserType(this TupleWriter writer, TuPackUserType? value)
		{
			if (value == null)
			{
				WriteNil(writer);
				return;
			}

			ref readonly Slice arg = ref value.Value;
			if (arg.Count == 0)
			{
				writer.Output.WriteByte((byte) value.Type);
			}
			else
			{
				writer.Output.EnsureBytes(checked(1 + arg.Count));
				writer.Output.UnsafeWriteByte((byte) value.Type);
				writer.Output.UnsafeWriteBytes(arg);
			}
		}

		public static bool TryWriteIpAddress(ref TupleSpanWriter writer, in System.Net.IPAddress? value)
		{
			return value is null
				? writer.TryWriteNil()
				: TryWriteBytes(ref writer, value.GetAddressBytes());
		}

		public static void WriteIpAddress(this TupleWriter writer, System.Net.IPAddress? value)
		{
			if (value is null)
			{
				writer.WriteNil();
			}
			else
			{
				writer.WriteBytes(value.GetAddressBytes());
			}
		}

		/// <summary>Mark the start of a new embedded tuple</summary>
		[MustUseReturnValue]
		public static TupleWriter BeginTuple(this TupleWriter writer)
		{
			writer.Output.WriteByte(TupleTypes.EmbeddedTuple);
			return new(ref writer.Output, writer.Depth + 1);
		}

		/// <summary>Mark the end of an embedded tuple</summary>
		public static void EndTuple(this TupleWriter writer)
		{
			writer.Output.WriteByte(0x00);
		}

		/// <summary>Opens a new embedded tuple</summary>
		[MustUseReturnValue]
		public static bool TryBeginTuple(ref TupleSpanWriter writer)
		{
			if (!writer.TryWriteLiteral(TupleTypes.EmbeddedTuple))
			{
				return false;
			}

			writer.Depth++;
			return true;
		}

		/// <summary>Closes an embedded tuple</summary>
		public static bool TryEndTuple(ref TupleSpanWriter writer)
		{
			Contract.Debug.Requires(writer.Depth > 0);

			if (!writer.TryWriteLiteral(0x00))
			{
				return false;
			}

			--writer.Depth;
			return true;
		}

		#endregion

		#region Deserialization...

		/// <summary>Parse a tuple segment containing a signed 64-bit integer</summary>
		/// <remarks>This method should only be used by custom decoders.</remarks>
		public static long ParseInt64(int type, Slice slice)
		{
			int bytes = type - TupleTypes.IntZero;
			if (bytes == 0) return 0L;

			bool neg = false;
			if (bytes < 0)
			{
				bytes = -bytes;
				neg = true;
			}

			if (bytes > 8) throw new FormatException("Invalid size for tuple integer");
			long value = (long) slice.ReadUInt64BE(1, bytes);

			if (neg)
			{ // the value is encoded as the one's complement of the absolute value
				value = (-(~value));
				if (bytes < 8) value |= (-1L << (bytes << 3));
			}

			return value;
		}

		/// <summary>Parse a tuple segment containing a signed 64-bit integer</summary>
		/// <remarks>This method should only be used by custom decoders.</remarks>
		public static bool TryParseInt64(int type, ReadOnlySpan<byte> slice, out long value)
		{
			value = 0L;

			int bytes = type - TupleTypes.IntZero;
			if (bytes == 0)
			{
				return true;
			}

			bool neg = false;
			if (bytes < 0)
			{
				bytes = -bytes;
				neg = true;
			}

			if (bytes > 8)
			{
				return false;
			}

			ulong v = slice.Slice(1, bytes).ToUInt64BE();

			if (neg)
			{ // the value is encoded as the one's complement of the absolute value
				value = (-(~(unchecked((long) v))));
				if (bytes < 8) value |= (-1L << (bytes << 3));
			}
			else
			{
				if (v > long.MaxValue)
				{
					return false;
				}
				value = unchecked((long) v);
			}

			return true;
		}

		/// <summary>Parse a tuple segment containing a signed 64-bit integer</summary>
		/// <remarks>This method should only be used by custom decoders.</remarks>
		public static long ParseInt64(int type, ReadOnlySpan<byte> slice)
		{
			int bytes = type - TupleTypes.IntZero;
			if (bytes == 0) return 0L;

			bool neg = false;
			if (bytes < 0)
			{
				bytes = -bytes;
				neg = true;
			}
			if (bytes > 8)
			{
				throw new FormatException("Invalid size for tuple integer");
			}

			long value = (long) slice.Slice(1, bytes).ToUInt64BE();

			if (neg)
			{ // the value is encoded as the one's complement of the absolute value
				value = (-(~value));
				if (bytes < 8) value |= (-1L << (bytes << 3));
			}

			return value;
		}

		/// <summary>Parse a tuple segment containing an unsigned 64-bit integer</summary>
		/// <remarks>This method should only be used by custom decoders.</remarks>
		public static bool TryParseUInt64(int type, ReadOnlySpan<byte> slice, out ulong value)
		{
			value = 0L;

			int bytes = type - TupleTypes.IntZero;
			if (bytes == 0)
			{
				return true;
			}
			if (bytes is < 0 or > 8)
			{
				return false;
			}

			value = slice.Slice(1, bytes).ToUInt64BE();

			return true;
		}

		/// <summary>Parse a tuple segment containing an unsigned 64-bit integer</summary>
		/// <remarks>This method should only be used by custom decoders.</remarks>
		public static ulong ParseUInt64(int type, ReadOnlySpan<byte> slice)
		{
			int bytes = type - TupleTypes.IntZero;
			return bytes switch
			{
				0 => 0L,
				< 0 or > 8 => throw new FormatException("Invalid size for tuple integer"),
				_ => slice.Slice(1, bytes).ToUInt64BE()
			};
		}

		private static bool ShouldUnescapeByteString(ReadOnlySpan<byte> buffer)
		{
			// check for nulls

			foreach (var b in buffer)
			{
				if (b == 0)
				{ // found a 0, switch to slow path
					return true;
				}
			}

			// buffer is clean, we can return it as-is
			return true;
		}

		private static bool TryUnescapeByteString(ReadOnlySpan<byte> buffer, Span<byte> output, out int bytesWritten)
		{
			int p = 0;
			for(int i = 0; i < buffer.Length; i++)
			{
				byte b = buffer[i];
				if (b == 0)
				{ // skip next FF
					//TODO: check that next byte really is 0xFF
					++i;
				}

				if (p >= output.Length) goto too_small;
				output[p++] = b;
			}

			bytesWritten = p;
			return true;
		too_small:
			bytesWritten = 0;
			return false;
		}

		/// <summary>Parse a tuple segment containing a byte array</summary>
		[Pure]
		public static Slice ParseBytes(Slice slice)
		{
			Contract.Debug.Requires(slice.HasValue && slice[0] == TupleTypes.Bytes && slice[slice.Count - 1] == 0);
			if (slice.Count <= 2) return Slice.Empty;

			var chunk = slice.Substring(1, slice.Count - 2);
			if (!ShouldUnescapeByteString(chunk.Span))
			{
				return chunk;
			}

			var span = new byte[chunk.Count];
			if (!TryUnescapeByteString(chunk.Span, span, out int written))
			{ // should never happen since decoding can only reduce the size!?
				throw new InvalidOperationException();
			}
			Contract.Debug.Requires(written <= span.Length);

			return new Slice(span, 0, written);
		}

		/// <summary>Parse a tuple segment containing a byte array</summary>
		[Pure]
		public static bool TryParseBytes(ReadOnlySpan<byte> slice, out Slice value)
		{
			Contract.Debug.Requires(slice.Length > 1 && slice[0] == TupleTypes.Bytes && slice[^1] == 0);
			if (slice.Length <= 2)
			{
				value = Slice.Empty;
				return true;
			}

			var chunk = slice.Slice(1, slice.Length - 2);
			if (!ShouldUnescapeByteString(chunk))
			{
				value = Slice.FromBytes(chunk);
				return true;
			}

			var span = new byte[chunk.Length];
			if (!TryUnescapeByteString(chunk, span, out int written))
			{ // should never happen since decoding can only reduce the size!?
				value = default;
				return false;
			}
			Contract.Debug.Requires(written <= span.Length);

			value = new Slice(span, 0, written);
			return true;
		}

		/// <summary>Parse a tuple segment containing a byte array</summary>
		[Pure]
		public static Slice ParseBytes(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 1 && slice[0] == TupleTypes.Bytes && slice[^1] == 0);
			if (slice.Length <= 2) return Slice.Empty;

			var chunk = slice.Slice(1, slice.Length - 2);
			if (!ShouldUnescapeByteString(chunk))
			{
				return Slice.FromBytes(chunk);
			}

			var span = new byte[chunk.Length];
			if (!TryUnescapeByteString(chunk, span, out int written))
			{ // should never happen since decoding can only reduce the size!?
				throw new InvalidOperationException();
			}
			Contract.Debug.Requires(written <= span.Length);

			return new Slice(span, 0, written);
		}

		/// <summary>Parse a tuple segment containing an ASCII string stored as a byte array</summary>
		[Pure]
		public static bool TryParseAscii(ReadOnlySpan<byte> slice, [MaybeNullWhen(false)] out string value)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.Bytes && slice[^1] == 0);

			if (slice.Length <= 2)
			{
				value = string.Empty;
				return true;
			}

			var chunk = slice.Slice(1, slice.Length - 2);
			if (!ShouldUnescapeByteString(chunk))
			{
				value = Encoding.Default.GetString(chunk);
				return true;
			}

			var buffer = ArrayPool<byte>.Shared.Rent(chunk.Length);
			try
			{
				if (!TryUnescapeByteString(chunk, buffer, out int written))
				{
					// should never happen since decoding can only reduce the size!?
					value = null;
					return false;
				}

				value = Encoding.Default.GetString(buffer, 0, written);
				return true;
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer);
			}
		}

		/// <summary>Parse a tuple segment containing an ASCII string stored as a byte array</summary>
		[Pure]
		public static string ParseAscii(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.Bytes && slice[^1] == 0);

			if (slice.Length <= 2) return string.Empty;

			var chunk = slice.Slice(1, slice.Length - 2);
			if (!ShouldUnescapeByteString(chunk))
			{
				return Encoding.Default.GetString(chunk);
			}

			var buffer = ArrayPool<byte>.Shared.Rent(chunk.Length);
			try
			{
				if (!TryUnescapeByteString(chunk, buffer, out int written))
				{
					// should never happen since decoding can only reduce the size!?
					throw new InvalidOperationException();
				}

				return Encoding.Default.GetString(buffer, 0, written);
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer);
			}
		}

		/// <summary>Parse a tuple segment containing a UTF-8 string</summary>
		[Pure]
		public static bool TryParseUtf8(ReadOnlySpan<byte> slice, [MaybeNullWhen(false)] out string value)
		{
			Contract.Debug.Requires(slice.Length > 1 && slice[0] == TupleTypes.Utf8 && slice[^1] == 0);

			if (slice.Length <= 2)
			{
				value = string.Empty;
				return true;
			}

			var chunk = slice.Slice(1, slice.Length - 2);
			if (!ShouldUnescapeByteString(chunk))
			{
				value = Encoding.UTF8.GetString(chunk); //TODO: "Try" decoding?
				return true;
			}

			var buffer = ArrayPool<byte>.Shared.Rent(chunk.Length);
			try
			{
				if (!TryUnescapeByteString(chunk, buffer, out int written))
				{
					// should never happen since decoding can only reduce the size!?
					value = null;
					return false;
				}

				value = Encoding.UTF8.GetString(buffer, 0, written); //TODO: "Try" decoding?
				return true;
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer);
			}
		}

		/// <summary>Parse a tuple segment containing a UTF-8 string</summary>
		[Pure]
		public static string ParseUtf8(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 1 && slice[0] == TupleTypes.Utf8 && slice[^1] == 0);

			if (slice.Length <= 2) return string.Empty;

			var chunk = slice.Slice(1, slice.Length - 2);
			if (!ShouldUnescapeByteString(chunk))
			{
				return Encoding.UTF8.GetString(chunk);
			}
			var buffer = ArrayPool<byte>.Shared.Rent(chunk.Length);
			if (!TryUnescapeByteString(chunk, buffer, out int written))
			{ // should never happen since decoding can only reduce the size!?
				throw new InvalidOperationException();
			}

			var s = Encoding.UTF8.GetString(buffer, 0, written);
			ArrayPool<byte>.Shared.Return(buffer);
			return s;
		}

		/// <summary>Parse a tuple segment containing an embedded tuple</summary>
		[Pure]
		public static IVarTuple ParseEmbeddedTuple(Slice slice)
		{
			Contract.Debug.Requires(slice.Count > 0 && slice[0] == TupleTypes.EmbeddedTuple && slice[^1] == 0);
			if (slice.Count <= 2) return STuple.Empty;

			var chunk = slice.Substring(1, slice.Count - 2);
			var reader = new TupleReader(chunk.Span, depth: 1);
			return TuplePackers.Unpack(ref reader).ToTuple(chunk);
		}

		/// <summary>Parse a tuple segment containing an embedded tuple</summary>
		[Pure]
		public static bool TryParseEmbeddedTuple(ReadOnlySpan<byte> slice, out SpanTuple value)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.EmbeddedTuple && slice[^1] == 0);
			if (slice.Length <= 2)
			{
				value = SpanTuple.Empty;
				return true;
			}

			var reader = new TupleReader(slice.Slice(1, slice.Length - 2), depth: 1);
			return TuplePackers.TryUnpack(ref reader, out value, out _);
		}

		/// <summary>Parse a tuple segment containing an embedded tuple</summary>
		[Pure]
		public static SpanTuple ParseEmbeddedTuple(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.EmbeddedTuple && slice[^1] == 0);
			if (slice.Length <= 2) return SpanTuple.Empty;

			var reader = new TupleReader(slice.Slice(1, slice.Length - 2), depth: 1);
			return TuplePackers.Unpack(ref reader);
		}

		/// <summary>Parse a tuple segment containing a negative big integer</summary>
		public static bool TryParseNegativeBigInteger(ReadOnlySpan<byte> slice, out BigInteger value)
		{
			Contract.Debug.Requires(slice.Length > 2 && slice[0] == TupleTypes.NegativeBigInteger);

			if (slice.Length < 3)
			{
				value = default;
				return false;
			}

			int len = slice[1];
			len ^= 0xFF;
			if (slice.Length != len + 2)
			{
				value = default;
				return false;
			}

			value = new BigInteger(slice[2..], isUnsigned: false, isBigEndian: true);
			value++;

			return true;
		}

		/// <summary>Parse a tuple segment containing a negative big integer</summary>
		public static BigInteger ParseNegativeBigInteger(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 2 && slice[0] == TupleTypes.NegativeBigInteger);

			if (slice.Length < 3)
			{
				throw new FormatException("Slice has invalid size for a Big Integer containing a UInt128");
			}

			int len = slice[1];
			len ^= 0xFF;
			if (slice.Length != len + 2)
			{
				throw new FormatException("Slice length does not match the embedded length for a Big Integer");
			}

			var value = new BigInteger(slice[2..], isUnsigned: false, isBigEndian: true);

			return value + 1;
		}

		/// <summary>Parse a tuple segment containing a positive big integer</summary>
		public static bool TryParsePositiveBigInteger(ReadOnlySpan<byte> slice, out BigInteger value)
		{
			Contract.Debug.Requires(slice.Length > 1 && slice[0] == TupleTypes.PositiveBigInteger);

			if (slice.Length < 2)
			{
				value = default;
				return false;
			}

			int len = slice[1];
			if (slice.Length != len + 2)
			{
				value = default;
				return false;
			}

			value = new BigInteger(slice[2..], isUnsigned: true, isBigEndian: true);
			return true;
		}

		/// <summary>Parse a tuple segment containing a positive big integer</summary>
		public static BigInteger ParsePositiveBigInteger(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 1 && slice[0] == TupleTypes.PositiveBigInteger);

			if (slice.Length < 2)
			{
				throw new FormatException("Slice has invalid size for a Big Integer containing a UInt128");
			}

			int len = slice[1];
			if (slice.Length != len + 2)
			{
				throw new FormatException("Slice length does not match the embedded length for a Big Integer");
			}

			return new BigInteger(slice[2..], isUnsigned: true, isBigEndian: true);
		}

		/// <summary>Parse a tuple segment containing a single precision number (float32)</summary>
		[Pure]
		public static bool TryParseSingle(ReadOnlySpan<byte> slice, out float value)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.Single);

			if (slice.Length != 5)
			{
				value = float.NaN;
				return false;
			}

			// We need to reverse encoding process: if first byte < 0x80 then it is negative (bits need to be flipped), else it is positive (highest bit must be set to 0)

			// read the raw bits
			uint bits = BinaryPrimitives.ReadUInt32BigEndian(slice.Slice(1, 4));

			if ((bits & 0x80000000U) == 0)
			{ // negative
				bits = ~bits;
			}
			else
			{ // positive
				bits ^= 0x80000000U;
			}

			unsafe { value = *((float*)&bits); }
			return true;
		}

		/// <summary>Parse a tuple segment containing a single precision number (float32)</summary>
		[Pure]
		public static float ParseSingle(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.Single);

			if (slice.Length != 5)
			{
				throw new FormatException("Slice has invalid size for a Single");
			}

			// We need to reverse encoding process: if first byte < 0x80 then it is negative (bits need to be flipped), else it is positive (highest bit must be set to 0)

			// read the raw bits
			uint bits = BinaryPrimitives.ReadUInt32BigEndian(slice.Slice(1, 4));

			if ((bits & 0x80000000U) == 0)
			{ // negative
				bits = ~bits;
			}
			else
			{ // positive
				bits ^= 0x80000000U;
			}

			float value;
			unsafe { value = *((float*)&bits); }

			return value;
		}

		/// <summary>Parse a tuple segment containing a double precision number (float64)</summary>
		[Pure]
		public static bool TryParseDouble(ReadOnlySpan<byte> slice, out double value)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.Double);

			if (slice.Length != 9)
			{
				value = double.NaN;
				return false;
			}

			// We need to reverse encoding process: if first byte < 0x80 then it is negative (bits need to be flipped), else it is positive (highest bit must be set to 0)

			// read the raw bits
			ulong bits = BinaryPrimitives.ReadUInt64BigEndian(slice.Slice(1, 8));

			if ((bits & 0x8000000000000000UL) == 0)
			{ // negative
				bits = ~bits;
			}
			else
			{ // positive
				bits ^= 0x8000000000000000UL;
			}

			// note: we could use BitConverter.Int64BitsToDouble(...), but it does the same thing, and also it does not exist for floats...
			unsafe { value = *((double*)&bits); }

			return true;
		}

		/// <summary>Parse a tuple segment containing a double precision number (float64)</summary>
		[Pure]
		public static double ParseDouble(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.Double);

			if (slice.Length != 9)
			{
				throw new FormatException("Slice has invalid size for a Double");
			}

			// We need to reverse encoding process: if first byte < 0x80 then it is negative (bits need to be flipped), else it is positive (highest bit must be set to 0)

			// read the raw bits
			ulong bits = BinaryPrimitives.ReadUInt64BigEndian(slice.Slice(1, 8));

			if ((bits & 0x8000000000000000UL) == 0)
			{ // negative
				bits = ~bits;
			}
			else
			{ // positive
				bits ^= 0x8000000000000000UL;
			}

			// note: we could use BitConverter.Int64BitsToDouble(...), but it does the same thing, and also it does not exist for floats...
			double value;
			unsafe { value = *((double*)&bits); }

			return value;
		}

		/// <summary>Parse a tuple segment containing a quadruple precision floating-point number (aka "binary128")</summary>
		[Pure]
		public static bool TryParseQuadruple(ReadOnlySpan<byte> slice, out decimal value)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.Quadruple);

			if (slice.Length != 17)
			{
				value = 0;
				return false;
			}

			//BUGBUG: TODO: implement this when Decimal128 drops in .NET 11 (

			value = 0;
			return false;
		}

		/// <summary>Parse a tuple segment containing a quadruple precision floating-point number (aka "binary128")</summary>
		[Pure]
		public static decimal ParseQuadruple(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.Quadruple);

			if (slice.Length != 17)
			{
				throw new FormatException("Slice has invalid size for a Decimal");
			}

			//BUGBUG: TODO: implement this when Decimal128 drops in .NET 11 (
			throw new NotImplementedException();
		}

#if NET8_0_OR_GREATER

		/// <summary>Parse a tuple segment containing a positive big integer into a <see cref="UInt128"/></summary>
		[Pure]
		public static UInt128 ParsePositiveUInt128(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.PositiveBigInteger);

			if (slice.Length is < 2 or > 18)
			{
				throw new FormatException("Slice has invalid size for a Big Integer containing a UInt128");
			}

			int len = slice[1];
			if (slice.Length != len + 2)
			{
				throw new FormatException("Slice length does not match the embedded length for a Big Integer");
			}

			if (len >= 16)
			{
				return BinaryPrimitives.ReadUInt128BigEndian(slice[2..]);
			}

			// we need to copy it to add back the leading zeroes
			Span<byte> tmp = stackalloc byte[16];
			tmp.Clear();
			slice[2..].CopyTo(tmp[(16 - len)..]);
			return BinaryPrimitives.ReadUInt128BigEndian(tmp);
		}

		/// <summary>Parse a tuple segment containing a negative big integer into a <see cref="Int128"/></summary>
		[Pure]
		public static Int128 ParseNegativeInt128(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.NegativeBigInteger);

			if (slice.Length is < 2 or > 18)
			{
				throw new FormatException("Slice has invalid size for a negative Big Integer containing a Int128");
			}

			int len = slice[1];
			if (len == 0)
			{
				throw new FormatException("Slice cannot have an embedded length of 0 for a negative Big Integer");
			}

			len ^= 0xFF;
			if (slice.Length != len + 2)
			{
				throw new FormatException("Slice length does not match the embedded length for a negative Big Integer");
			}

			Int128 value;
			if (len >= 16)
			{
				value = BinaryPrimitives.ReadInt128BigEndian(slice[2..]);
			}
			else
			{
				// we need to copy it to add back the leading zeroes
				Span<byte> tmp = stackalloc byte[16];
				tmp.Fill(0xFF);
				slice[2..].CopyTo(tmp[(16 - len)..]);
				value = BinaryPrimitives.ReadInt128BigEndian(tmp);
			}

			// inverse the one's complement (0xFE -> -1)
			value += 1;

			return value;
		}

 #endif

		/// <summary>Parse a tuple segment containing a 128-bit GUID</summary>
		[Pure]
		public static bool TryParseGuid(ReadOnlySpan<byte> slice, out Guid value)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.Uuid128);

			if (slice.Length != 17)
			{
				value = Guid.Empty;
				return false;
			}

			// We store them in RFC 4122 under the hood, so we need to reverse them to the MS format
			value = Uuid128.ReadUnsafe(slice[1..]);
			return true;
		}

		/// <summary>Parse a tuple segment containing a 128-bit GUID</summary>
		[Pure]
		public static Guid ParseGuid(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.Uuid128);

			if (slice.Length != 17)
			{
				throw new FormatException("Slice has invalid size for a GUID");
			}

			// We store them in RFC 4122 under the hood, so we need to reverse them to the MS format
			return Uuid128.ReadUnsafe(slice[1..]);
		}

		/// <summary>Parse a tuple segment containing a 128-bit UUID</summary>
		[Pure]
		public static Uuid128 ParseUuid128(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.Uuid128);

			if (slice.Length != 17)
			{
				throw new FormatException("Slice has invalid size for a 128-bit UUID");
			}

			return Uuid128.Read(slice[1..]);
		}

		/// <summary>Parse a tuple segment containing a 64-bit UUID</summary>
		[Pure]
		public static bool TryParseUuid64(ReadOnlySpan<byte> slice, out Uuid64 value)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.Uuid64);

			if (slice.Length != 9)
			{
				value = default;
				return false;
			}

			value = Uuid64.Read(slice.Slice(1, 8));
			return true;
		}

		/// <summary>Parse a tuple segment containing a 64-bit UUID</summary>
		[Pure]
		public static Uuid64 ParseUuid64(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.Uuid64);

			if (slice.Length != 9)
			{
				throw new FormatException("Slice has invalid size for a 64-bit UUID");
			}

			return Uuid64.Read(slice.Slice(1, 8));
		}

		/// <summary>Parses a tuple segment containing either an 80-bit or 96-bit VersionStamp</summary>
		[Pure]
		public static VersionStamp ParseVersionStamp(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && (slice[0] is TupleTypes.Uuid80 or TupleTypes.Uuid96));

			if (slice.Length != 11 && slice.Length != 13)
			{
				throw new FormatException("Slice has invalid size for a VersionStamp");
			}

			return VersionStamp.ReadFrom(slice[1..]);
		}

		/// <summary>Parses a tuple segment containing an 80-bit UUID</summary>
		[Pure]
		public static Uuid80 ParseUuid80(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] is TupleTypes.Uuid80);

			if (slice.Length != 11)
			{
				throw new FormatException("Slice has invalid size for a Uuid80");
			}

			return Uuid80.Read(slice[1..]);
		}

		/// <summary>Parses a tuple segment containing an 96-bit UUID</summary>
		[Pure]
		public static Uuid96 ParseUuid96(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] is TupleTypes.Uuid96);

			if (slice.Length != 13)
			{
				throw new FormatException("Slice has invalid size for a Uuid96");
			}

			return Uuid96.Read(slice[1..]);
		}

		#endregion

		#region Parsing...

		/// <summary>Decode the next token from a packed tuple</summary>
		/// <returns>Token decoded, or Slice.Nil if there was no more data in the buffer</returns>
		public static bool TryParseNext(ref TupleReader reader, out Range token, out Exception? error)
		{
			int r = reader.Remaining;
			if (r <= 0)
			{ // End of Stream
				token = default;
				error = null;
				return false;
			}
			int type = reader.Peek();
			switch (type)
			{
				case TupleTypes.Nil:
				{ // <00> / <00><FF> => null
					if (reader.Depth > 0)
					{ // must be <00><FF> inside an embedded tuple
						if (r > 1 && reader.PeekAt(1) == 0xFF)
						{ // this is a Nil entry
							token = new Range(reader.Cursor, reader.Cursor + 1);
							reader.Advance(2);
							error = null;
							return true;
						}
						else
						{ // this is the end of the embedded tuple
							reader.Advance(1);
							token = default;
							error = null;
							return false;
						}
					}
					else
					{ // can be <00> outside an embedded tuple
						token = new Range(reader.Cursor, reader.Cursor + 1);
						reader.Advance(1);
						error = null;
						return true;
					}
				}

				case TupleTypes.Bytes:
				{ // <01>(bytes)<00>
					return reader.TryReadByteString(out token, out error);
				}

				case TupleTypes.Utf8:
				{ // <02>(utf8 bytes)<00>
					return reader.TryReadByteString(out token, out error);
				}

				case TupleTypes.LegacyTupleStart:
				{ // <03>(packed tuple)<04>

					//note: this format is NOT SUPPORTED ANYMORE, because it was not compatible with the current spec (<03>...<00> instead of <03>...<04> and is replaced by <05>....<00>)
					//we prefer throwing here instead of still attempting to decode the tuple, because it could silently break layers (if we read an old-style key and update it with the new-style format)
					token = default;
					error = TupleParser.FailLegacyTupleNotSupported();
					return false;
				}
				case TupleTypes.EmbeddedTuple:
				{ // <05>(packed tuple)<00>
					//PERF: currently, we will first scan to get all the bytes of this tuple, and parse it later.
					// This means that we may need to scan multiple times the bytes, which may not be efficient if there are multiple embedded tuples inside each other
					return TryReadEmbeddedTupleBytes(ref reader, out token, out error);
				}

				case TupleTypes.NegativeBigInteger:
				{ // <0B><##>(bytes)
					return reader.TryReadNegativeBigInteger(out token, out error);
				}

				case TupleTypes.PositiveBigInteger:
				{ // <1D><##>(bytes)
					return reader.TryReadPositiveBigInteger(out token, out error);
				}

				case TupleTypes.Single:
				{ // <20>(4 bytes)
					return reader.TryReadBytes(5, out token, out error);
				}

				case TupleTypes.Double:
				{ // <21>(8 bytes)
					return reader.TryReadBytes(9, out token, out error);
				}

				case TupleTypes.Triple:
				{ // <22>(10 bytes)
					return reader.TryReadBytes(11, out token, out error);
				}

				case TupleTypes.Quadruple:
				{ // <23>(16 bytes)
					return reader.TryReadBytes(17, out token, out error);
				}

				case TupleTypes.False:
				{ // <26>
					return reader.TryReadBytes(1, out token, out error);
				}
				case TupleTypes.True:
				{ // <27>
					return reader.TryReadBytes(1, out token, out error);
				}

				case TupleTypes.Uuid128:
				{ // <30>(16 bytes)
					return reader.TryReadBytes(17, out token, out error);
				}

				case TupleTypes.Uuid64:
				{ // <31>(8 bytes)
					return reader.TryReadBytes(9, out token, out error);
				}

				case TupleTypes.Uuid80:
				{ // <32>(10 bytes)
					return reader.TryReadBytes(11, out token, out error);
				}

				case TupleTypes.Uuid96:
				{ // <33>(12 bytes)
					return reader.TryReadBytes(13, out token, out error);
				}

				case TupleTypes.Directory:
				{ // <FE>
					return reader.TryReadBytes(1, out token, out error);
				}
				case TupleTypes.Escape:
				{ // <FF>....

					// if <FF> and this is the first byte, we are reading a system key like "\xFF/something"
					if (reader.Cursor == 0)
					{
						token = new(reader.Cursor, reader.Input.Length);
						reader.Advance(reader.Remaining);
						error = null;
						return true;
					}

					return reader.TryReadBytes(1, out token, out error);
				}
			}

			if (type is <= TupleTypes.IntPos8 and >= TupleTypes.IntNeg8)
			{
				int bytes = type - TupleTypes.IntZero;
				if (bytes < 0) bytes = -bytes;

				return reader.TryReadBytes(1 + bytes, out token, out error);
			}

			token = default;
			error = new FormatException($"Invalid tuple type byte {type} at index {reader.Cursor}/{reader.Input.Length}");
			return false;
		}

		/// <summary>Read an embedded tuple, without parsing it</summary>
		private static bool TryReadEmbeddedTupleBytes(ref TupleReader reader, out Range token, out Exception? error)
		{
			// The current embedded tuple starts here, and stops on a <00>, but itself can contain more embedded tuples, and could have a <00> bytes as part of regular items (like bytes, strings, that end with <00> or could contain a <00><FF> ...)
			// This means that we have to parse the tuple recursively, discard the tokens, and note where the cursor ended. The parsing of the tuple itself will be processed later.

			++reader.Depth;
			int start = reader.Cursor;
			reader.Advance(1);

			while(reader.HasMore)
			{
				if (!TryParseNext(ref reader, out token, out error))
				{
					if (error != null)
					{
						return false;
					}

					// the token will be Nil for either the end of the stream, or the end of the tuple
					// => since we already tested Input.HasMore, we know we are in the later case
					if (token.Equals(default))
					{
						//note: ParseNext() has already eaten the <00>
						--reader.Depth;
						int end = reader.Cursor;
						token = new Range(start, end);
						error = null;
						return true;
					}
					// else: ignore this token, it will be processed later if the tuple is unpacked and accessed
				}
			}

			token = default;
			error = new FormatException($"Truncated embedded tuple started at index {start}/{reader.Input.Length}");
			return false;
		}

		/// <summary>Skip a number of tokens</summary>
		/// <param name="reader">Cursor in the packed tuple to decode</param>
		/// <param name="count">Number of tokens to skip</param>
		/// <returns>True if there was <paramref name="count"/> tokens, false if the reader was too small.</returns>
		/// <remarks>Even if this method return true, you need to check that the reader has not reached the end before reading more token!</remarks>
		public static bool Skip(ref TupleReader reader, int count)
		{
			while (count-- > 0)
			{
				if (!reader.HasMore) return false;
				if (!TryParseNext(ref reader, out _, out var error))
				{
					if (error != null) throw error;
					return false;
				}
			}
			return true;
		}

#if NET9_0_OR_GREATER

		/// <summary>Visit the different tokens of a packed tuple</summary>
		/// <param name="reader">Reader positioned at the start of a packed tuple</param>
		/// <param name="visitor">Lambda called for each segment of a tuple. Returns true to continue parsing, or false to stop</param>
		/// <returns>Number of tokens that have been visited until either <paramref name="visitor"/> returned false, or <paramref name="reader"/> reached the end.</returns>
		public static T VisitNext<T>(ref TupleReader reader, Func<ReadOnlySpan<byte>, TupleSegmentType, T> visitor)
		{
			if (!reader.HasMore) throw new InvalidOperationException("The reader has already reached the end");
			if (!TryParseNext(ref reader, out var token, out var error))
			{
				if (error != null) throw error;
			}

			var slice = reader.Input[token];
			return visitor(slice, TupleTypes.DecodeSegmentType(slice));
		}

#endif

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static Exception FailLegacyTupleNotSupported()
		{
			return new FormatException("Old style embedded tuples (0x03) are not supported anymore.");
		}

		#endregion

		#region Bits Twiddling...

#if !NET6_0_OR_GREATER
		/// <summary>Lookup table used to compute the index of the most significant bit</summary>
		private static readonly int[] MultiplyDeBruijnBitPosition =
		[
			0, 9, 1, 10, 13, 21, 2, 29, 11, 14, 16, 18, 22, 25, 3, 30,
			8, 12, 20, 28, 15, 17, 24, 7, 19, 27, 23, 6, 26, 5, 4, 31
		];
#endif

		/// <summary>Returns the minimum number of bytes needed to represent a value</summary>
		/// <remarks>Note: will return 1 even for <param name="v"/> == 0</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int NumberOfBytes(long v)
		{
			return v >= 0 ? NumberOfBytes((ulong) v) : v != long.MinValue ? NumberOfBytes((ulong) - v) : 8;
		}

		/// <summary>Returns the minimum number of bytes needed to represent a value</summary>
		/// <returns>Note: will return 1 even for <param name="v"/> == 0</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int NumberOfBytes(ulong v)
		{
#if NET6_0_OR_GREATER
			return (BitOperations.Log2(v) + 8) >> 3;
#else
			int msb = 0;

			if (v > 0xFFFFFFFF)
			{ // for 64-bit values, shift everything by 32 bits to the right
				msb += 32;
				v >>= 32;
			}
			msb += MostSignificantBit((uint) v);
			return (msb + 8) >> 3;
#endif
		}

#if !NET6_0_OR_GREATER
		/// <summary>Returns the position of the most significant bit (0-based) in a 32-bit integer</summary>
		/// <param name="v">32-bit integer</param>
		/// <returns>Index of the most significant bit (0-based)</returns>
		private static int MostSignificantBit(uint v)
		{
			// from: http://graphics.stanford.edu/~seander/bithacks.html#IntegerLogDeBruijn

			v |= v >> 1; // first round down to one less than a power of 2
			v |= v >> 2;
			v |= v >> 4;
			v |= v >> 8;
			v |= v >> 16;

			var r = (v * 0x07C4ACDDU) >> 27;
			return MultiplyDeBruijnBitPosition[r];
		}
#endif

		#endregion

	}

}
