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

namespace SnowBank.Data.Tuples.Binary
{
	using SnowBank.Buffers;

	/// <summary>Reads bytes from a contiguous region of arbitrary memory</summary>
	[DebuggerDisplay("{Cursor}/{Input.Length} @ {Depth}")]
	[DebuggerNonUserCode]
	public ref struct TupleReader
	{
		// This reader maintains a cursor in the original input buffer, in order to be able to format error messages with the absolute byte offset "Invalid XYZ at offset CURSOR", instead of simply repeatedly slicing the buffer.
		// We also always return "Range" instead of a ReadOnlySpan<char> so that we avoid any potential compiler warnings 

		/// <summary>Input buffer containing a packed tuple</summary>
		public readonly ReadOnlySpan<byte> Input;

		/// <summary>Cursor of the next bytes that will be read from <see cref="Input"/></summary>
		public int Cursor;

		/// <summary>Current decoding depth (0 = top level, 1+ = embedded tuple)</summary>
		public int Depth;

		public TupleReader(ReadOnlySpan<byte> input, int depth = 0, int cursor = 0)
		{
			Contract.Debug.Requires(depth >= 0 && cursor >= 0);
			this.Input = input;
			this.Cursor = cursor;
			this.Depth = depth;
		}

		/// <summary>Number of bytes remaining in the input buffer</summary>
		public readonly int Remaining => this.Input.Length - this.Cursor;

		/// <summary>Tests if there is more data remaining in the buffer</summary>
		public readonly bool HasMore => this.Cursor < this.Input.Length;

		/// <summary>Peek the next byte in the buffer, without advancing the cursor</summary>
		/// <returns>Value of the next byte in the buffer.</returns>
		/// <exception cref="IndexOutOfRangeException">If the cursor is outside the bounds of the buffer (no more bytes, advanced too far, ...)</exception>
		public readonly int Peek() => this.Input[this.Cursor];

		/// <summary>Peek at a byte further away in the buffer, without advancing the cursor</summary>
		/// <param name="offset">Offset (from the cursor) of the byte to return</param>
		/// <returns>Value of the byte at the specified offset from the cursor.</returns>
		/// <exception cref="IndexOutOfRangeException">If the cursor plus offset falls outside the bounds of the buffer (no more bytes, advanced too far, ...)</exception>
		public readonly int PeekAt([Positive] int offset) => this.Input[this.Cursor + offset];

		/// <summary>Advance the cursor by the specified amount of bytes</summary>
		/// <param name="bytes">Number of bytes to skip</param>
		public void Advance(int bytes) => this.Cursor += bytes;

		/// <summary>Tries to read the specified amount of bytes from the buffer</summary>
		/// <param name="count">Number of bytes to read</param>
		/// <param name="token">Receives the corresponding range in the original buffer, if there was enough bytes</param>
		/// <param name="error">Receives an exception if there was not enough bytes</param>
		/// <returns><see langword="true"/> if the read was successful, in which case <paramref name="token"/> receives the corresponding range and the cursor is advanced by <paramref name="count"/> bytes, or <see langword="false"/> if there was not enough bytes remaining and <paramref name="error"/> receives an exception that can be re-thrown by the caller.</returns>
		public bool TryReadBytes(int count, out Range token, [NotNullWhen(false)] out Exception? error)
		{
			int start = this.Cursor;
			int end = start + count;
			if (end > this.Input.Length)
			{
				token = default;
				error = SliceReader.NotEnoughBytes(count);
				return false;
			}

			token = new Range(start, end);
			this.Cursor = end;
			error = null;
			return true;
		}

		/// <summary>Tries to read an encoded null-terminated byte string (which contains escaped NUL-bytes)</summary>
		/// <param name="token">Receives the range that starts from the current cursor up until the end of the string</param>
		/// <param name="error">Receives an exception if the string is incomplete</param>
		/// <returns><see langword="true"/> if the string was successfully read, or <see langword="false"/> if the string was truncated, or if there was no more data in the buffer</returns>
		public bool TryReadByteString(out Range token, [NotNullWhen(false)] out Exception? error)
		{
			int p = this.Cursor;
			var buffer = this.Input;
			int end = buffer.Length;
			while (p < end)
			{
				byte b = buffer[p++];
				if (b == 0)
				{
					if (p < end && buffer[p] == 0xFF)
					{
						// skip the next byte and continue
						p++;
						continue;
					}

					token = new Range(this.Cursor, p);
					this.Cursor = p;
					error = null;
					return true;
				}
			}

			token = default;
			error = new FormatException("Truncated byte string (expected terminal NUL not found)");
			return false;
		}

		/// <summary>Tries to read a positive Big Integer</summary>
		/// <param name="token">Receives the range that starts from the current cursor up until the end of the string</param>
		/// <param name="error">Receives an exception if the string is incomplete</param>
		/// <returns><see langword="true"/> if the big integer was successfully read, or <see langword="false"/> if the string was truncated, or if there was no more data in the buffer</returns>
		public bool TryReadPositiveBigInteger(out Range token, [NotNullWhen(false)] out Exception? error)
		{
			int start = this.Cursor;
			var buffer = this.Input;
			// we need at least 2 bytes (header + length)

			int end = checked(start + 2);
			if (end > buffer.Length)
			{
				goto too_small;
			}

			Contract.Debug.Assert(buffer[0] is TupleTypes.PositiveBigInteger);

			int len = buffer[start + 1];
			end = checked(start + 2 + len);
			if (end > buffer.Length)
			{
				goto too_small;
			}

			token = new Range(start, end);
			this.Cursor = end;
			error = null;
			return true;

		too_small:
			token = default;
			error = SliceReader.NotEnoughBytes(end - start);
			return false;
		}

		/// <summary>Tries to read a positive Big Integer</summary>
		/// <param name="token">Receives the range that starts from the current cursor up until the end of the string</param>
		/// <param name="error">Receives an exception if the string is incomplete</param>
		/// <returns><see langword="true"/> if the big integer was successfully read, or <see langword="false"/> if the string was truncated, or if there was no more data in the buffer</returns>
		public bool TryReadNegativeBigInteger(out Range token, [NotNullWhen(false)] out Exception? error)
		{
			int start = this.Cursor;
			var buffer = this.Input;
			// we need at least 2 bytes (header + length)

			int end = checked(start + 2);
			if (end > buffer.Length)
			{
				goto too_small;
			}

			Contract.Debug.Assert(buffer[0] is TupleTypes.NegativeBigInteger);

			int len = buffer[start + 1];
			if (len == 0) goto too_small;
			len ^= 0xFF;

			end = checked(start + 2 + len);
			if (end > buffer.Length)
			{
				goto too_small;
			}

			token = new Range(start, end);
			this.Cursor = end;
			error = null;
			return true;

		too_small:
			token = default;
			error = SliceReader.NotEnoughBytes(end - start);
			return false;
		}

		/// <summary>Unpack the content of an embedded tuple</summary>
		/// <param name="packed">Packed tuple (starts with <c>0x02</c> and ends with <c>0x00</c></param>
		/// <returns></returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TupleReader UnpackEmbeddedTuple(ReadOnlySpan<byte> packed)
		{
			Contract.Debug.Requires(packed.Length >= 2 && packed[0] == TupleTypes.EmbeddedTuple && packed[^1] == 0);
			return new TupleReader(packed.Slice(1, packed.Length - 2));
		}

	}

}
