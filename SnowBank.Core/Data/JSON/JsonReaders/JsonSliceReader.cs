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

	/// <summary>JSON text reader that reads UTF-8 encoded bytes from an in-memory buffer</summary>
	[DebuggerDisplay("Remaining={" + nameof(Remaining) + "}")]
	public struct JsonSliceReader : IJsonReader
	{

		/// <summary>Current position, in <see cref="Array"/></summary>
		private int Cursor;

		/// <summary>End offset </summary>
		private readonly int End;

		/// <summary>Buffer containing the UTF-8 encoded string</summary>
		private readonly byte[] Array;

		/// <summary>Create a new UTF-8 reader from an unmanaged memory buffer</summary>
		/// <param name="buffer">Buffer containing UTF-8 encoded bytes</param>
		/// <param name="autoDetectBom">If true, skip the UTF-8 BOM if found (EF BB BF)</param>
		public JsonSliceReader(Slice buffer, bool autoDetectBom = true)
		{
			this.Cursor = buffer.Offset + (autoDetectBom && buffer.Count >= 3 && (buffer[0] == 0xEF & buffer[1] == 0xBB & buffer[2] == 0xBF) ? 3 : 0);
			this.End = buffer.Offset + buffer.Count;
			this.Array = buffer.Array;
		}

		/// <inheritdoc />
		public int Read()
		{
			int cursor = this.Cursor;
			if (cursor < this.End)
			{
				byte c = this.Array[cursor];
				if (c < 0x80)
				{
					// ASCII character
					this.Cursor = cursor + 1;
					return c;
				}
			}
			return ReadSlow();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private int ReadSlow()
		{
			int index = this.Cursor;
			int end = this.End;
			if (index >= end)
			{ // EOF
				this.Cursor = end;
				return -1;
			}

			if (!Utf8Encoder.TryDecodeCodePoint(this.Array, index, end - index, out var cp, out var len))
			{
				throw new InvalidDataException("Buffer contains malformed UTF-8 characters.");
			}
			this.Cursor = index + len;

			// note: we need to convert from the uint value, otherwise \uFFFF will be returned as -1 (instead of 65535), and confused for the EOF marker
			return (int) cp.Value;
		}

		/// <inheritdoc />
		public readonly bool? HasMore => this.Cursor < this.End;

		/// <inheritdoc />
		public readonly int? Remaining => this.Cursor < this.End ? (this.End - this.Cursor) : 0;

	}

}
