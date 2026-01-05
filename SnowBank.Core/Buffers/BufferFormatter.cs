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
	using System.Buffers;
	using System.Text;

	/// <summary>Helper type for writing binary data into a <see cref="IBufferWriter{T}"/> of bytes</summary>
	[PublicAPI]
	[DebuggerDisplay("Buffered={Position}/{Current.Length}")]
	public ref struct BufferFormatter : ISpanBufferWriter<byte> //TODO: find a better name!
	{

		/// <summary>Destination writer</summary>
		public readonly IBufferWriter<byte> Writer;

		/// <summary>Current position in the buffer</summary>
		private int Position; // relative to Current!

		/// <summary>Current buffer to write bytes</summary>
		private Span<byte> Current;

		/// <summary>Constructs a <see cref="BufferFormatter"/> that will output to the specified <see cref="IBufferWriter{T}"/></summary>
		/// <param name="writer"></param>
		public BufferFormatter(IBufferWriter<byte> writer)
		{
			Contract.Debug.Requires(writer != null);
			this.Writer = writer;
			this.Position = 0;
			this.Current = default;
		}

		/// <summary>Flush all pending bytes to the writer</summary>
		public void Flush()
		{
			int pos = this.Position;
			if (pos > 0)
			{
				this.Writer.Advance(pos);
				this.Current = default;
				this.Position = 0;
			}
		}

		/// <summary>Ensures that the buffer can store <paramref name="count"/> additional bytes</summary>
		/// <param name="count">Number of bytes expected to be written soon.</param>
		/// <returns>Span that maps the corresponding free space in the buffer. The length of the span will be equal to <paramref name="count"/>, even if there are more space available in the buffer.</returns>
		/// <remarks>This does not advance the cursor.</remarks>
		/// <exception cref="ArgumentException">If the buffer is too small to fit <paramref name="count"/> additional bytes.</exception>
		/// <see cref="Advance"/>
		/// <see cref="GetSpan"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Span<byte> EnsureBytes(int count)
		{
			int pos = this.Position;
			return this.Current.Length - pos >= count ? this.Current.Slice(pos, count) : EnsureBytesSlow(count);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private Span<byte> EnsureBytesSlow(int count)
		{
			this.Writer.Advance(this.Position);
			this.Current = this.Writer.GetSpan(count);
			this.Position = 0;
			return this.Current.Slice(0, count);
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Span<byte> GetSpan(int count)
		{
			int pos = this.Position;
			return this.Current.Length - pos >= count ? this.Current.Slice(pos) : GetSpanSlow(count);
		}
		
		[MethodImpl(MethodImplOptions.NoInlining)]
		private Span<byte> GetSpanSlow(int count)
		{
			this.Writer.Advance(this.Position);
			this.Current = this.Writer.GetSpan(count);
			this.Position = 0;
			return this.Current;
		}

		/// <inheritdoc />
		/// <see cref="GetSpan"/>
		/// <see cref="EnsureBytes"/>
		public void Advance(int count)
		{
			int pos = this.Position + count;
			if (pos > this.Current.Length) throw new InvalidOperationException("Cannot advance past buffer size");
			this.Position = pos;
		}

		/// <summary>Returns a <see cref="SpanWriter"/> that can be used to populate the next chunk of bytes</summary>
		public SpanWriter GetSpanWriter(int count)
		{
			return new SpanWriter(EnsureBytes(count));
		}

		/// <summary>Writes a byte to the buffer</summary>
		public void WriteByte(byte value)
		{
			EnsureBytes(1)[0] = value;
			this.Position++;
		}

		/// <summary>Writes a byte to the buffer</summary>
		public void WriteByte(int value)
		{
			EnsureBytes(1)[0] = (byte) value;
			this.Position++;
		}

		/// <summary>Writes a byte to the buffer</summary>
		public void WriteByte(char value)
		{
			EnsureBytes(1)[0] = (byte) value;
			this.Position++;
		}

		/// <summary>Writes a 16-bit little-endian integer to the buffer</summary>
		public void WriteUInt16(ushort value)
		{
			var chunk = EnsureBytes(2);
			chunk[0] = (byte) value;
			chunk[1] = (byte) (value >> 8);
			this.Position += 2;
		}

		/// <summary>Writes a 16-bit little-endian integer to the buffer</summary>
		public static void WriteUInt16(in Span<byte> span, ushort value)
		{
			if (span.Length < 2) throw new ArgumentException();
			span[0] = (byte) value;
			span[1] = (byte) (value >> 8);
		}

		/// <summary>Writes a 16-bit big-endian integer to the buffer</summary>
		public void WriteUInt16BE(ushort value)
		{
			var chunk = EnsureBytes(2);
			chunk[0] = (byte) (value >> 8);
			chunk[1] = (byte) value;
			this.Position += 2;
		}

		/// <summary>Writes a 16-bit big-endian integer to the buffer</summary>
		public static void WriteUInt16BE(in Span<byte> span, ushort value)
		{
			if (span.Length < 2) throw new ArgumentException();
			span[0] = (byte)(value >> 8);
			span[1] = (byte)value;
		}

		/// <summary>Writes a 32-bit little-endian integer to the buffer</summary>
		public void WriteUInt32(uint value)
		{
			var chunk = EnsureBytes(4);
			chunk[0] = (byte) value;
			chunk[1] = (byte) (value >> 8);
			chunk[2] = (byte) (value >> 16);
			chunk[3] = (byte) (value >> 24);
			this.Position += 4;
		}

		/// <summary>Writes a 32-bit little-endian integer to the buffer</summary>
		public static void WriteUInt32(in Span<byte> span, uint value)
		{
			if (span.Length < 4) throw new ArgumentException();
			span[0] = (byte)value;
			span[1] = (byte)(value >> 8);
			span[2] = (byte)(value >> 16);
			span[3] = (byte)(value >> 24);
		}

		/// <summary>Writes a 32-bit big-endian integer to the buffer</summary>
		public void WriteUInt32BE(uint value)
		{
			var chunk = EnsureBytes(4);
			chunk[0] = (byte)(value >> 24);
			chunk[1] = (byte)(value >> 16);
			chunk[2] = (byte)(value >> 8);
			chunk[3] = (byte)value;
			this.Position += 4;
		}

		/// <summary>Writes a 32-bit big-endian integer to the buffer</summary>
		public static void WriteUInt32BE(in Span<byte> span, uint value)
		{
			if (span.Length < 4) throw new ArgumentException();
			span[0] = (byte) (value >> 24);
			span[1] = (byte) (value >> 16);
			span[2] = (byte) (value >> 8);
			span[3] = (byte) value;
		}

		/// <summary>Writes a span of bytes to the buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBytes(in ReadOnlySpan<byte> bytes)
		{
			bytes.CopyTo(EnsureBytes(bytes.Length));
			this.Position += bytes.Length;
		}

		/// <summary>Writes a string encoded as UTF-8 to the buffer</summary>
		public void WriteUtf8Text(in ReadOnlySpan<char> text)
		{
			int byteCount = Encoding.UTF8.GetByteCount(text);
			var chunk = EnsureBytes(byteCount);
			Encoding.UTF8.GetBytes(text, chunk);
			Advance(byteCount);
		}

	}

}
