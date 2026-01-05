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

namespace SnowBank.Text
{
	using System.Text;

	/// <summary>Helper type for using the <b>Base32</b> encoding</summary>
	/// <remarks>
	/// <para>This encoding only uses alphanumeric characters, with uppercase letters <c>A</c> to <c>Z</c> and digits 2 to 7. The digits <c>0</c> and <c>1</c> are omitted since they could be confused with the letters <c>O</c>, <c>I</c> or <c>l</c></para>
	/// </remarks>
	[PublicAPI]
	public static class Base32Encoding
	{

		private static readonly string Base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

		/// <summary>Encodes a byte array into a Base32 string literal</summary>
		public static string ToBase32(byte[] input)
		{
			Contract.NotNull(input);
			return ToBase32(input.AsSpan());
		}

		/// <summary>Encodes a byte buffer into a Base32 string literal</summary>
		public static string ToBase32(Slice input)
		{
			return ToBase32(input.Span);
		}

		/// <summary>Encodes a byte buffer into a Base32 string literal</summary>
		public static string ToBase32(ReadOnlySpan<byte> input)
		{
			var sb = new StringBuilder();
			for (int offset = 0; offset < input.Length;)
			{
				byte a, b, c, d, e, f, g, h;
				int numCharsToOutput = GetNextGroup(input, ref offset, out a, out b, out c, out d, out e, out f, out g, out h);

				sb.Append((numCharsToOutput >= 1) ? Base32Chars[a] : '=');
				sb.Append((numCharsToOutput >= 2) ? Base32Chars[b] : '=');
				sb.Append((numCharsToOutput >= 3) ? Base32Chars[c] : '=');
				sb.Append((numCharsToOutput >= 4) ? Base32Chars[d] : '=');
				sb.Append((numCharsToOutput >= 5) ? Base32Chars[e] : '=');
				sb.Append((numCharsToOutput >= 6) ? Base32Chars[f] : '=');
				sb.Append((numCharsToOutput >= 7) ? Base32Chars[g] : '=');
				sb.Append((numCharsToOutput >= 8) ? Base32Chars[h] : '=');
			}

			return sb.ToString();
		}

		/// <summary>Decodes a Base32 string literal into a byte array</summary>
		public static byte[] FromBase32(string input)
		{
			Contract.NotNull(input);
			return FromBase32(input.AsSpan());
		}

		/// <summary>Decodes a Base32 string literal into a byte array</summary>
		public static byte[] FromBase32(ReadOnlySpan<char> input)
		{
			input = input.TrimEnd('=');
			if (input.Length == 0)
			{
				return [ ];
			}

			var output = new byte[input.Length * 5 / 8];
			var bitIndex = 0;
			var inputIndex = 0;
			var outputBits = 0;
			var outputIndex = 0;
			var base32Chars = Base32Chars;
			while (outputIndex < output.Length)
			{
				var byteIndex = base32Chars.IndexOf(char.ToUpperInvariant(input[inputIndex]));
				if (byteIndex < 0)
				{
					throw new FormatException();
				}

				var bits = Math.Min(5 - bitIndex, 8 - outputBits);
				output[outputIndex] <<= bits;
				output[outputIndex] |= (byte)(byteIndex >> (5 - (bitIndex + bits)));

				bitIndex += bits;
				if (bitIndex >= 5)
				{
					inputIndex++;
					bitIndex = 0;
				}

				outputBits += bits;
				if (outputBits >= 8)
				{
					outputIndex++;
					outputBits = 0;
				}
			}
			return output;
		}

		// returns the number of bytes that were output
		private static int GetNextGroup(ReadOnlySpan<byte> input, ref int offset, out byte a, out byte b, out byte c, out byte d, out byte e, out byte f, out byte g, out byte h)
		{
			uint b1, b2, b3, b4, b5;

			int retVal = (offset - input.Length) switch
			{
				1 => 2,
				2 => 4,
				3 => 5,
				4 => 7,
				_ => 8
			};

			b1 = (offset < input.Length) ? input[offset++] : 0U;
			b2 = (offset < input.Length) ? input[offset++] : 0U;
			b3 = (offset < input.Length) ? input[offset++] : 0U;
			b4 = (offset < input.Length) ? input[offset++] : 0U;
			b5 = (offset < input.Length) ? input[offset++] : 0U;

			a = (byte)(b1 >> 3);
			b = (byte)(((b1 & 0x07) << 2) | (b2 >> 6));
			c = (byte)((b2 >> 1) & 0x1f);
			d = (byte)(((b2 & 0x01) << 4) | (b3 >> 4));
			e = (byte)(((b3 & 0x0f) << 1) | (b4 >> 7));
			f = (byte)((b4 >> 2) & 0x1f);
			g = (byte)(((b4 & 0x3) << 3) | (b5 >> 5));
			h = (byte)(b5 & 0x1f);

			return retVal;
		}

	}

}
