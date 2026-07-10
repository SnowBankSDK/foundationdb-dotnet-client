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

namespace FoundationDB.Client.Tests
{
	using System.Text;
	using SnowBank.Buffers;

	/// <summary>Reversible text encoding for the keys and values captured in scenario definitions and golden traces.</summary>
	/// <remarks>
	/// <para>Printable ASCII (0x20..0x7E) is written as-is, except the backslash which is doubled (<c>\\</c>); every other byte is written as a lowercase hex escape (<c>\xNN</c>).</para>
	/// <para>Unlike <see cref="Slice"/>'s pretty-printers (<c>ToString("K")</c>, ...), this encoding round-trips: <see cref="Decode"/> always recovers the exact original bytes, which golden traces require.</para>
	/// <para><see cref="Slice.Nil"/> maps to <see langword="null"/> and <see cref="Slice.Empty"/> to the empty string, preserving the nil/empty distinction across JSON.</para>
	/// </remarks>
	public static class ScenarioText
	{

		/// <summary>Encodes a slice into a reversible display string.</summary>
		public static string? Encode(Slice bytes)
		{
			if (bytes.IsNull) return null;
			if (bytes.Count == 0) return "";

			var sb = new StringBuilder(bytes.Count + 8);
			foreach (byte b in bytes.Span)
			{
				if (b == (byte) '\\')
				{
					sb.Append(@"\\");
				}
				else if (b is >= 0x20 and <= 0x7E)
				{
					sb.Append((char) b);
				}
				else
				{
					sb.Append(@"\x").Append(b.ToString("x2"));
				}
			}
			return sb.ToString();
		}

		/// <summary>Decodes a string produced by <see cref="Encode"/> back into the exact original bytes.</summary>
		/// <exception cref="FormatException">If the text contains a malformed escape sequence.</exception>
		public static Slice Decode(string? text)
		{
			if (text is null) return Slice.Nil;
			if (text.Length == 0) return Slice.Empty;

			var writer = new SliceWriter(text.Length);
			for (int i = 0; i < text.Length; i++)
			{
				char c = text[i];
				if (c != '\\')
				{
					if (c is < '\x20' or > '\x7E') throw new FormatException($"Invalid character '{c}' at offset {i}: only printable ASCII and escapes are allowed.");
					writer.WriteByte((byte) c);
					continue;
				}

				if (i + 1 >= text.Length) throw new FormatException($"Truncated escape sequence at offset {i}.");
				char e = text[++i];
				switch (e)
				{
					case '\\':
					{
						writer.WriteByte((byte) '\\');
						break;
					}
					case 'x':
					{
						if (i + 2 >= text.Length) throw new FormatException($"Truncated hex escape at offset {i - 1}.");
						int hi = ParseHexDigit(text[i + 1]);
						int lo = ParseHexDigit(text[i + 2]);
						if (hi < 0 || lo < 0) throw new FormatException($"Malformed hex escape at offset {i - 1}.");
						writer.WriteByte((byte) ((hi << 4) | lo));
						i += 2;
						break;
					}
					default:
					{
						throw new FormatException($"Unknown escape '\\{e}' at offset {i - 1}.");
					}
				}
			}
			return writer.ToSlice();
		}

		private static int ParseHexDigit(char c) => c switch
		{
			>= '0' and <= '9' => c - '0',
			>= 'a' and <= 'f' => c - 'a' + 10,
			>= 'A' and <= 'F' => c - 'A' + 10,
			_ => -1,
		};

	}

}
