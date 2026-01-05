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

namespace SnowBank.Text.Xml
{
	using System.Text;
	using SnowBank.Runtime.Converters;
	using SnowBank.Text;

	/// <summary>XPATH Codec</summary>
	[PublicAPI]
	public static class XPathEncoder
	{
		/// <summary>Return the empty string corresponding to the specified quote character</summary>
		[Pure]
		private static string EmptyString(char quote) => quote switch
		{
			'"' => "\"\"",
			'\'' => "''",
			'\0' => string.Empty,
			_ => new string(quote, 2)
		};

		/// <summary>Return the number of characters required to fully encode the specified string into an XPATH string literal (including escaping characters, and the surrounding quotes)</summary>
		/// <returns>Can be used to pre-allocate the buffer capacity required to fully encode a string, before calling one of the encode helpers</returns>
		/// <remarks>
		/// Please note that the <c>null</c> string is encoded as "null" and so required 4 characters.
		/// </remarks>
		[Pure]
		public static int GetEncodedLength(string? literal)
		{
			if (literal == null) return 4; // "null"
			if (literal.Length == 0) return 2;

			int count = 2;
			foreach (var c in literal)
			{
				count += (c == '\'' | c == '"' | c == '\\') ? 2 : 1;
			}
			return count;
		}

		/// <summary>Return the number of characters required to fully a string literal in the worst possible</summary>
		/// <returns>In the worst case, the string requires <code>2 + LENGTH * 2</code> characters to encode.</returns>
		public static int GetMaxEncodedLength(int count)
		{
			// worst case required each character to be escape, plus 2 surrounding quotes
			return checked(count * 2 + 2);
		}

		/// <summary>Encode a string literal for inclusion in a XPATH query</summary>
		/// <remarks>
		/// Encode les <c>'</c>, <c>"</c> et <c>\</c> en ajoutant un <c>\</c> devant chaque occurence (ie: <c>'</c> => <c>\'</c>).
		/// Rajoute optionellement le caractère <paramref name="quote"/> en début et fin du token.
		/// Si <paramref name="literal"/> est null, retourne soit <code>"null"</code> (si <paramref name="quote"/> est spécifié), soit chaine vide (si égal à <c>'\0'</c>)
		/// </remarks>
		/// <example>
		/// - EncodeLiteral("Hello World") => `Hello World`
		/// - EncodeLiteral("Hello World", quote: '"') => `"Hello World"`
		/// - EncodeLiteral("Hello World", quote: '\'') => `'Hello World'`
		/// - EncodeLiteral("A'B"C&amp;D\\E", quote: '\'') => `'A\'B\"C&amp;D\\E'`
		/// </example>
		[Pure]
		public static string EncodeLiteral(string? literal, char quote = '"')
		{
			if (literal == null)
			{ // null  string
				return quote != '\0' ? "null" : string.Empty;
			}
			if (literal.Length == 0)
			{ // empty string
				return EmptyString(quote);
			}

			var sb = StringBuilderCache.Acquire(literal.Length + 4);
			if (quote != '\0') sb.Append(quote);
			foreach (var c in literal)
			{
				if (c == '\'' | c == '"' | c == '\\')
				{
					sb.Append('\\');
				}
				sb.Append(c);
			}
			if (quote != '\0') sb.Append(quote);
			return StringBuilderCache.GetStringAndRelease(sb);
		}

		/// <summary>Encode a string literal for inclusion in a XPATH query</summary>
		/// <remarks>
		/// Encode les <c>'</c>, <c>"</c> et <c>\</c> en ajoutant un <c>\</c> devant chaque occurence (ie: <c>'</c> => <c>\'</c>).
		/// Rajoute optionellement le caractère <paramref name="quote"/> en début et fin du token.
		/// Si <paramref name="literal"/> est null, retourne soit <code>"null"</code> (si <paramref name="quote"/> est spécifié), soit chaine vide (si égal à <c>'\0'</c>)
		/// </remarks>
		/// <example>
		/// - EncodeLiteral("Hello World") => `Hello World`
		/// - EncodeLiteral("Hello World", quote: '"') => `"Hello World"`
		/// - EncodeLiteral("Hello World", quote: '\'') => `'Hello World'`
		/// - EncodeLiteral("A'B"C&amp;D\\E", quote: '\'') => `'A\'B\"C&amp;D\\E'`
		/// </example>
		[Pure]
		public static string EncodeLiteral(ReadOnlySpan<char> literal, char quote = '"')
		{
			if (literal.Length == 0)
			{ // empty string
				return EmptyString(quote);
			}

			var sb = StringBuilderCache.Acquire(literal.Length + 4);
			if (quote != '\0') sb.Append(quote);
			foreach (var c in literal)
			{
				if (c == '\'' | c == '"' | c == '\\')
				{
					sb.Append('\\');
				}
				sb.Append(c);
			}
			if (quote != '\0') sb.Append(quote);
			return StringBuilderCache.GetStringAndRelease(sb);
		}

		/// <summary>Encode a boolean into an XPATH literal</summary>
		/// <returns>Either <code>"true"</code> or <code>"false"</code></returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string EncodeLiteral(bool value)
		{
			return value ? "true" : "false";
		}

		/// <summary>Encode a number into an XPATH literal</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string EncodeLiteral(int value)
		{
			return StringConverters.ToString(value);
		}

		/// <summary>Encode a number into an XPATH literal</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string EncodeLiteral(long value)
		{
			return StringConverters.ToString(value);
		}

		/// <summary>Encode a number into an XPATH literal</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string EncodeLiteral(float value)
		{
			return StringConverters.ToString(value);
		}

		/// <summary>Encode a number into an XPATH literal</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string EncodeLiteral(double value)
		{
			return StringConverters.ToString(value);
		}

		/// <summary>Encode a <see cref="Guid"/> into an XPATH literal</summary>
		/// <remarks>The <see cref="Guid.Empty">empty Guid</see> is encoded as the empty string!</remarks>
		[Pure]
		public static string EncodeLiteral(Guid guid, char quote = '"')
		{
			var lit = guid != Guid.Empty ? guid.ToString() : "";
			switch (quote)
			{
				case '"':  return "\"" + lit + "\"";
				case '\'': return "'" + lit + "'";
				case '\0': return lit;
				default:   return StringBuilderCache.GetStringAndRelease(StringBuilderCache.Acquire(1 + 36 + 1).Append(quote).Append(lit).Append(quote));
			}
		}

		/// <summary>Append a string literal for inclusion in a XPATH query</summary>
		public static StringBuilder AppendLiteral(StringBuilder sb, string? literal, char quote = '"')
		{
			if (literal == null)
			{
				if (quote != '\0') sb.Append("null");
				return sb;
			}

			if (quote != '\0') sb.Append(quote);
			if (literal.Length > 0)
			{
				foreach (var c in literal)
				{
					if (c == '\'' | c == '"' | c == '\\')
					{
						sb.Append('\\');
					}

					sb.Append(c);
				}
			}
			if (quote != '\0') sb.Append(quote);
			return sb;
		}

		/// <summary>Append a string literal for inclusion in a XPATH query</summary>
		public static StringBuilder AppendLiteral(StringBuilder sb, ReadOnlySpan<char> literal, char quote = '"')
		{
			if (quote != '\0') sb.Append(quote);
			if (literal.Length > 0)
			{
				foreach (var c in literal)
				{
					if (c == '\'' | c == '"' | c == '\\')
					{
						sb.Append('\\');
					}

					sb.Append(c);
				}
			}
			if (quote != '\0') sb.Append(quote);
			return sb;
		}

	}
}
