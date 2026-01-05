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

	/// <summary>JSON Text reader that wraps an in-memory buffer of characters</summary>
	[DebuggerDisplay("Remaining={Remaining}")]
	public unsafe struct JsonCharReader : IJsonReader
	{

		private char* Cursor;
		private readonly char* End;
		private readonly char* Start;

		/// <summary>Create a new char reader from an unmanaged memory buffer</summary>
		/// <param name="buffer">Buffer containing decoded characters</param>
		/// <param name="count">Size (in characters) of the buffer</param>
		/// <param name="autoDetectBom">If <see langword="true"/>, skip the BOM if found ('\xFEFF')</param>
		public JsonCharReader(char* buffer, int count, bool autoDetectBom = true)
		{
			this.Cursor = buffer + (autoDetectBom && count >= 1 && buffer[0] == 0xFEFF ? 1 : 0);
			this.End = buffer + count;
			this.Start = buffer;
		}

		/// <inheritdoc />
		public int Read()
		{
			var cursor = this.Cursor;
			if (cursor >= this.End)
			{
				return -1;
			}
			this.Cursor = cursor + 1;
			return *cursor;
		}

		/// <inheritdoc />
		public bool? HasMore => this.Cursor < this.End;

		/// <inheritdoc />
		public int? Remaining => this.Cursor < this.End ? (int) (this.End - this.Cursor) : 0;

		/// <summary>Number of characters consumed from the start</summary>
		public int Consumed => checked((int) (this.Cursor - this.Start));

	}

}
