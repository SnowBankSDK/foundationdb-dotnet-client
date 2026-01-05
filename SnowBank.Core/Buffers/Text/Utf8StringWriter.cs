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

namespace SnowBank.Buffers.Text
{
	using System.Globalization;
	using System.IO;
	using System.Text;
	using SnowBank.Text;

	/// <summary><see cref="TextWriter"/> that writes UTF-8 bytes into an in-memory buffer.</summary>
	[PublicAPI]
	public sealed class Utf8StringWriter : TextWriter
	{

		/// <inheritdoc />
		public override Encoding Encoding => Encoding.UTF8;

		private SliceWriter Writer;

		/// <summary>Constructs a new writer using an existing destination buffer.</summary>
		public Utf8StringWriter(SliceWriter writer)
			: base(CultureInfo.InvariantCulture)
		{
			this.Writer = writer;
		}

		/// <summary>Constructs a new writer with a minimum initial capacity</summary>
		public Utf8StringWriter(int capacity)
		{
			this.Writer = new SliceWriter(capacity);
		}

		/// <inheritdoc />
		public override void Write(char value)
		{
			if (value < 0x80)
			{
				this.Writer.WriteByte((byte) value);
			}
			else if (!Utf8Encoder.TryWriteCodePoint(ref this.Writer, (UnicodeCodePoint) value))
			{
				throw new DecoderFallbackException("Failed to encode invalid Unicode CodePoint into UTF-8");
			}
		}

		/// <inheritdoc />
		public override void Write(char[]? buffer)
		{
			if (buffer != null)
			{
				this.Writer.WriteStringUtf8(buffer);
			}
		}

		/// <inheritdoc />
		public override void Write(string? value)
		{
			if (value != null)
			{
				this.Writer.WriteStringUtf8(value);
			}
		}

		/// <inheritdoc />
		public override void Write(int value) => this.Writer.WriteBase10(value);

		/// <inheritdoc />
		public override void Write(long value) => this.Writer.WriteBase10(value);

		/// <inheritdoc />
		public override void Write(uint value) => this.Writer.WriteBase10(value);

		/// <inheritdoc />
		public override void Write(ulong value) => this.Writer.WriteBase10(value);

		/// <summary>Returns a new byte array with the contents of the buffer</summary>
		[Pure]
		public byte[] ToArray() => this.Writer.GetBytes();

		/// <summary>Returns a <see cref="Slice"/> of the buffer</summary>
		[Pure]
		public Slice GetBuffer() => this.Writer.ToSlice();

	}

}
