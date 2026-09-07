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

namespace SnowBank.Data.Json
{
	using System.IO;
	using SnowBank.Text;

	/// <summary>JSON text reader that reads from UTF-8 encoded bytes in native memory</summary>
	[DebuggerDisplay("Remaining={Remaining}")]
	public unsafe struct JsonUnmanagedReader : IJsonReader
	{

		private byte* Cursor;

		private readonly byte* End;

		/// <summary>Create a new UTF-8 reader from an unmanaged memory buffer</summary>
		/// <param name="buffer">Buffer containing UTF-8 encoded bytes</param>
		/// <param name="length">Length of the buffer (in bytes)</param>
		/// <param name="autoDetectBom">If true, skip the UTF-8 BOM if found (EF BB BF)</param>
		public JsonUnmanagedReader(byte* buffer, int length, bool autoDetectBom = true)
		{
			this.Cursor = buffer + ((autoDetectBom && length >= 3 && (buffer[0] == 0xEF & buffer[1] == 0xBB & buffer[2] == 0xBF)) ? 3 : 0);
			this.End = buffer + length;
		}

		/// <inheritdoc />
		public int Read()
		{
			var cursor = this.Cursor;
			if (cursor < this.End)
			{
				byte c = *cursor;
				if (c < 0x80)
				{ // ASCII character
					this.Cursor = cursor + 1;
					return c;
				}
			}
			return ReadSlow();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		int ReadSlow()
		{
			var cursor = this.Cursor;
			if (cursor >= this.End)
			{ // EOF
				return -1;
			}

			//TODO: PERF: we already know the first byte is >= 0x80, so maybe we can optimize the decoding ?
			if (!Utf8Encoder.TryDecodeCodePoint(cursor, this.End, out UnicodeCodePoint cp, out int len))
			{
				throw ErrorBufferContainsMalformedUtf8Character();
			}
			this.Cursor = cursor + len;

			// note: we need to convert from the uint value, otherwise \uFFFF will be returned as -1 (instead of 65535), and confused for the EOF marker
			return (int) cp.Value;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidDataException ErrorBufferContainsMalformedUtf8Character() => new("Buffer contains malformed UTF-8 character");

		/// <inheritdoc />
		public bool? HasMore => this.Cursor < this.End;

		/// <inheritdoc />
		public int? Remaining => this.Cursor < this.End ? checked((int) (this.End - this.Cursor)) : 0;


#if NET8_0_OR_GREATER
		/// <summary>Reads the rest of a string literal up to its closing quote, when it holds only ASCII and no escape sequence</summary>
		/// <param name="table">Table used to intern the result, or <see langword="null"/> to allocate it</param>
		/// <param name="result">Receives the decoded literal (without the quotes)</param>
		/// <returns><see langword="false"/> if the literal has an escape, a non-ASCII byte or no closing quote: nothing is consumed, the caller reads it character by character</returns>
		internal bool TryReadPlainString(StringTable? table, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out string result)
		{
			var span = new ReadOnlySpan<byte>(this.Cursor, (int) (this.End - this.Cursor));
			int idx = span.IndexOfAny((byte) '"', (byte) '\\');
			if (idx < 0 || span[idx] != (byte) '"')
			{
				result = null;
				return false;
			}
			var body = span[..idx];
			if (!System.Text.Ascii.IsValid(body))
			{
				result = null;
				return false;
			}
			this.Cursor += idx + 1;
			result = body.Length == 0 ? string.Empty : table != null ? table.Add(body) : System.Text.Encoding.ASCII.GetString(body);
			return true;
		}
#endif

	}

}
