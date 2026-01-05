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

#pragma warning disable CS0618 // Type or member is obsolete

namespace SnowBank.Text
{
	using System.Globalization;
	using SnowBank.IO.Hashing;
	using SnowBank.Buffers.Text;

	/// <summary>Helper class for formatting binary blobs into hexadecimal for logging or troubleshooting</summary>
	[PublicAPI]
	public static class HexaDump
	{
		/// <summary>Formatting options</summary>
		[Flags]
		public enum Options
		{
			/// <summary>Standard display</summary>
			Default = 0,

			/// <summary>Do not display the ASCII preview</summary>
			NoPreview = 1,

			/// <summary>Do not add any headers</summary>
			NoHeader = 2,

			/// <summary>Do not add any footers</summary>
			NoFooter = 4,

			/// <summary>Display the byte distribution graph</summary>
			ShowBytesDistribution = 8,

			/// <summary>The content is most probably text</summary>
			Text = 16,

			/// <summary>Do not include the last <c>\r\n</c> for the last line</summary>
			/// <remarks>Allows <c>WriteLine(HexaDump.Format(...))</c> to dump to the console or log, without adding an extra new line</remarks>
			OmitLastNewLine = 32,

			IdentWithSpaces = 64,

			DoubleWidth = 128,
		}

		private static void DumpHexaLine(ref FastStringBuilder sb, ReadOnlySpan<byte> bytes, int pad)
		{
			Contract.Debug.Requires(bytes.Length <= pad);

			foreach (byte b in bytes)
			{
				sb.Append(' ');
				sb.AppendHex(b, 2);
			}

			if (bytes.Length < pad)
			{
				sb.Append(' ', (pad - bytes.Length) * 3);
			}
		}

		private static void DumpRawLine(ref FastStringBuilder sb, ReadOnlySpan<byte> bytes, int pad)
		{
			Contract.Debug.Requires(bytes.Length <= pad);

			foreach (byte b in bytes)
			{
				if (b == 0)
				{
					sb.Append('\u00B7'); // '·'
				}
				else if (b < 0x20)
				{ // C0 Controls
					if (b <= 9) sb.Append((char)(0x2080 + b)); // subscript '₁' to '₉'
					else sb.Append('\u02DA'); // '˚'
				}
				else
				{ // C1 Controls
					if (b <= 0x7E) sb.Append((char) b); // ASCII
					else if (b <= 0x9F | b == 0xAD | b == 0xA0) sb.Append('\u02DA'); // '˚'
					else if (b == 255) sb.Append('\uFB00'); // 'ﬀ'
					else sb.Append((char) b); // Latin-1
				}
			}
			if (bytes.Length < pad)
			{
				sb.Append(' ', (pad - bytes.Length));
			}
		}

		private static void DumpTextLine(ref FastStringBuilder sb, ReadOnlySpan<byte> bytes, int pad)
		{
			Contract.Debug.Requires(bytes.Length <= pad);

			foreach (byte b in bytes)
			{
				if (b < 0x20)
				{ // C0 Controls
					if (b == 10) sb.Append('\u240A'); // '␊'
					else if (b == 13) sb.Append('\u240D'); // '␍'
					else sb.Append('\u00B7'); // '·'
				}
				else
				{ // C1 Controls
					if (b <= 0x7E) sb.Append((char)b); // ASCII
					else if (b <= 0x9F | b == 0xAD | b == 0xA0) sb.Append('\u00B7'); // '·'
					else sb.Append((char)b); // Latin-1
				}
			}
			if (bytes.Length < pad) sb.Append(' ', (pad - bytes.Length));
		}

		/// <summary>Dumps a byte array into hexadecimal, formatted as 16 bytes per lines</summary>
		public static string Format(byte[] bytes, Options options = Options.Default, int indent = 0)
		{
			Contract.NotNull(bytes);

			return Format(bytes.AsSlice(), options, indent);
		}

		/// <summary>Dumps a byte slice into hexadecimal, formatted as 16 bytes per lines</summary>
		public static string Format(Slice bytes, Options options = Options.Default, int indent = 0)
		{
			return Format(bytes.Span, options, indent);
		}

		/// <summary>Dumps a byte span into hexadecimal, formatted as 16 bytes per lines</summary>
		public static string Format(ReadOnlySpan<byte> bytes, Options options = Options.Default, int indent = 0)
		{
			var sb = new FastStringBuilder();
			bool preview = !options.HasFlag(Options.NoPreview);
			bool doubleWidth = options.HasFlag(Options.DoubleWidth);

			string prefix = indent == 0 ? string.Empty : new string(options.HasFlag(Options.IdentWithSpaces) ? ' ' : '\t', indent);

			if (!options.HasFlag(Options.NoHeader))
			{
				sb.Append(prefix);
				if (doubleWidth)
				{
					sb.Append("     : -0 -1 -2 -3 -4 -5 -6 -7 -8 -9 -A -B -C -D -E -F 10 11 12 13 14 15 16 17 18 19 1A 1B 1C 1D 1E 1F");
					if (preview) sb.Append(" : 0123456789ABCDEF0123456789ABCDEF :");
				}
				else
				{
					sb.Append("     : -0 -1 -2 -3 -4 -5 -6 -7 -8 -9 -A -B -C -D -E -F");
					if (preview) sb.Append(" : 0123456789ABCDEF :");
				}
				sb.AppendLine();
			}

			int p = 0;
			int offset = 0;
			int count = bytes.Length;
			int w = doubleWidth ? 32 : 16;
			while (count > 0)
			{
				int n = Math.Min(count, w);
				sb.Append(prefix);
				sb.AppendHex(p >> 4, 3);
				sb.Append("x |");
				var chunk = bytes.Slice(offset, n);
				DumpHexaLine(ref sb, chunk, w);
				if (preview)
				{
					sb.Append(" | ");
					if (!options.HasFlag(Options.Text))
					{
						DumpRawLine(ref sb, chunk, w);
					}
					else
					{
						DumpTextLine(ref sb, chunk, w);
					}
					sb.Append(" |");
				}
				sb.AppendLine();
				count -= n;
				p += n;
				offset += n;
			}

			if (!options.HasFlag(Options.NoFooter))
			{
				sb.Append(prefix);
				sb.AppendFormat(
					"---- : Size = {0:N0} / 0x{0:X} : Hash = 0x{1:X8}",
					bytes.Length,
					XxHash32.Compute(bytes)
				);
				sb.AppendLine();
			}
			if (options.HasFlag(Options.ShowBytesDistribution))
			{
				sb.Append(prefix);
				sb.AppendFormat("---- [{0}]", ComputeBytesDistribution(bytes, 2));
				sb.AppendLine();
			}

			if (options.HasFlag(Options.OmitLastNewLine))
			{
				switch (Environment.NewLine)
				{
					case "\r\n":
					{
						if (sb.Length >= 2)
						{
							sb.Length -= 2;
						}

						break;
					}
					case "\n":
					{
						if (sb.Length >= 1)
						{
							sb.Length -= 1;
						}

						break;
					}
				}
			}

			return sb.ToString();
		}

		/// <summary>Dump two byte arrays, side-by-side, formatted as 16 bytes per line</summary>
		public static string Versus(byte[] left, byte[] right, Options options = Options.Default)
		{
			Contract.NotNull(left);
			Contract.NotNull(right);
			return Versus(left.AsSpan(), right.AsSpan(), options);
		}

		/// <summary>Dump two byte slices, side-by-side, formatted as 16 bytes per line</summary>
		public static string Versus(Slice left, Slice right, Options options = Options.Default)
		{
			return Versus(left.OrEmpty().Span, right.OrEmpty().Span);
		}

		/// <summary>Dump two byte spans, side-by-side, formatted as 16 bytes per line</summary>
		public static string Versus(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right, Options options = Options.Default, int indent = 0)
		{
			var sb = new FastStringBuilder();

			string prefix = indent == 0 ? string.Empty : new string(options.HasFlag(Options.IdentWithSpaces) ? ' ' : '\t', indent);

			bool preview = !options.HasFlag(Options.NoPreview);
			if (options.HasFlag(Options.DoubleWidth))
			{
				throw new ArgumentException("Double Width mode is not supported in Versus mode.");
			}

			if ((options & Options.NoHeader) == 0)
			{
				sb.Append(prefix);
				sb.Append("     : -0 -1 -2 -3 -4 -5 -6 -7 -8 -9 -A -B -C -D -E -F : -0 -1 -2 -3 -4 -5 -6 -7 -8 -9 -A -B -C -D -E -F");
				if (preview) sb.Append(" :      : 0123456789ABCDEF : 0123456789ABCDEF : 0123456789ABCDEF");
				sb.AppendLine();
			}

			int p = 0;
			int lr = left.Length;
			int rr = right.Length;
			int count = Math.Max(lr, rr);
			while (count > 0)
			{
				sb.Append($"{prefix}{p >> 4:x03}x |");

				int ln = Math.Min(lr, 16);
				int rn = Math.Min(rr, 16);

				var lChunk = ln > 0 ? left.Slice(p, ln) : Span<byte>.Empty;
				var rChunk = rn > 0 ? right.Slice(p, rn) : Span<byte>.Empty;

				bool same = !preview || lChunk.SequenceEqual(rChunk);
				DumpHexaLine(ref sb, lChunk, 16);
				sb.Append(" │");
				DumpHexaLine(ref sb, rChunk, 16);

				if (preview)
				{
					sb.Append($" ║ {p >> 4:x03}x | ");
					DumpRawLine(ref sb, lChunk, 16);
					sb.Append(" │ ");
					DumpRawLine(ref sb, rChunk, 16);
					if (!same)
					{
						sb.Append(" │ ");
						int mn = Math.Max(ln, rn);
						for (int i = 0; i < mn; i++)
						{
							sb.Append(i >= ln || i >= rn ? '+' : lChunk[i] == rChunk[i] ? '·' : lChunk[i] == 0 ? '@' : rChunk[i] == 0 ? 'x':  '#');
						}
					}
					else
					{
						sb.Append(" │ ················");
					}
				}

				sb.AppendLine();
				lr -= ln;
				rr -= rn;
				count -= 16;
				p += 16;
			}

			if (!options.HasFlag(Options.NoHeader))
			{
				if (!options.HasFlag(Options.ShowBytesDistribution))
				{
					sb.Append(prefix);
					sb.Append(CultureInfo.InvariantCulture, $"<<<< {left.Length:N0} bytes; 0x{XxHash32.Compute(left):X8}");
					sb.AppendLine();
					sb.Append(prefix);
					sb.Append(CultureInfo.InvariantCulture, $">>>> {right.Length:N0} bytes; 0x{XxHash32.Compute(right):X8}");
					sb.AppendLine();
				}
				else
				{
					sb.Append(prefix);
					sb.Append(CultureInfo.InvariantCulture, $"<<<< [{ComputeBytesDistribution(left, 1)}] {left.Length:N0} bytes; 0x{XxHash32.Compute(left):X8}");
					sb.AppendLine();
					sb.Append(prefix);
					sb.Append(CultureInfo.InvariantCulture, $">>>> [{ComputeBytesDistribution(left, 1)}] {right.Length:N0} bytes; 0x{XxHash32.Compute(right):X8}");
					sb.AppendLine();
				}
			}

			return sb.ToString();
		}

		/// <summary>Generate a string that represents the byte distribution (0..255) of a buffer</summary>
		/// <param name="bytes">Buffer to map</param>
		/// <param name="shrink">Shrink factor between 0 and 8. The byte value is divided by 2^<paramref name="shrink"/> to get the index of the corresponding counter</param>
		/// <returns>ASCII string (of size 256 >> <paramref name="shrink"/>) that shows the distribution of <paramref name="bytes"/></returns>
		public static string ComputeBytesDistribution(byte[] bytes, int shrink = 0)
		{
			Contract.NotNull(bytes);
			return ComputeBytesDistribution(bytes.AsSpan(), shrink);
		}

		/// <summary>Generate a string that represents the byte distribution (0..255) of a buffer</summary>
		/// <param name="bytes">Buffer to map</param>
		/// <param name="shrink">Shrink factor between 0 and 8. The byte value is divided by 2^<paramref name="shrink"/> to get the index of the corresponding counter</param>
		/// <returns>ASCII string (of size 256 >> <paramref name="shrink"/>) that shows the distribution of <paramref name="bytes"/></returns>
		public static string ComputeBytesDistribution(Slice bytes, int shrink = 0)
		{
			return ComputeBytesDistribution(bytes.Span, shrink);
		}

		/// <summary>Generate a string that represents the byte distribution (0..255) of a buffer</summary>
		/// <param name="bytes">Buffer to map</param>
		/// <param name="shrink">Shrink factor between 0 and 8. The byte value is divided by 2^<paramref name="shrink"/> to get the index of the corresponding counter</param>
		/// <returns>ASCII string (of size 256 >> <paramref name="shrink"/>) that shows the distribution of <paramref name="bytes"/></returns>
		public static string ComputeBytesDistribution(ReadOnlySpan<byte> bytes, int shrink = 0)

		{
			if (shrink < 0 || shrink > 8) throw new ArgumentOutOfRangeException(nameof(shrink));

#if DEBUG_ASCII_PALETTE
			ReadOnlySpan<char> brush = "0123456789ABCDE";
#else
			ReadOnlySpan<char> brush = " .-:;~+=omMB$#@";
			//note: tweaked to get a better result when using Consolas (VS output, notepad, ...)
#endif

			if (bytes.Length <= 0)
			{
				return string.Empty;
			}


			int[] counters = new int[256 >> shrink];

			var sb = new FastStringBuilder(counters.Length);

			foreach(var b in bytes)
			{
				++counters[b >> shrink];
			}
			int[] cpy = counters.Where(c => c > 0).ToArray();
			Array.Sort(cpy);
			int max = cpy[^1];
			int half = cpy.Length >> 1;
			int med = cpy[half];
			if (cpy.Length % 2 == 1)
			{
				med = cpy.Length == 1 ? cpy[0] : (med + cpy[half + 1]) / 2;
			}

			foreach (var c in counters)
			{
				if (c == 0)
				{
					sb.Append(brush[0]);
				}
				else if (c == max)
				{
					sb.Append(brush[14]);
				}
				else if (c >= med)
				{ // 8..15
					double p = (c - med) * 6.5 / (max - med);
					sb.Append(brush[(int) Math.Round(p + 7, MidpointRounding.AwayFromZero)]);
				}
				else
				{ // 0..7
					double p = (c * 6.5) / med;
					sb.Append(brush[(int) Math.Round(p + 0.5, MidpointRounding.AwayFromZero)]);
				}
			}
			return sb.ToString();
		}

	}

}
