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
	using System.Globalization;
	using System.Text;
	using SnowBank.Buffers.Text;
#if NET8_0_OR_GREATER
	using System.Buffers;
#endif

	public static class JavaScriptEncoding
	{
		public static class Tokens
		{
			public const string Null = "null";
			public const string True = "true";
			public const string False = "false";
			public const string Quote = "'";
			public const string CloseParens = ")";
			public const string BeginDate = "new Date(";
			public const string EmptyString = "''";
			public const string Zero = "0";
			public const string DecimalZero = "0.0";
			public const string DotZero = ".0";
			public const string NaN = "Number.NaN";
			public const string InfinityPos = "Number.POSITIVE_INFINITY";
			public const string InfinityNeg = "Number.NEGATIVE_INFINITY";
		}

		/// <summary>Test if a javascript string would require escaping or not</summary>
		/// <param name="s">String to inspect</param>
		/// <returns><c>true</c> if all characters are valid</returns>
		public static bool IsCleanJavaScript(ReadOnlySpan<char> s)
		{
			foreach(var c in s)
			{
				//PERF: TODO: use SearchValues to speed this up!
				if (!((c >= 'a' && c <= 'z') || c == ' ' || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == ',' || c == '-' || c == '_' || c == ':' || c == '/' || (c >= 880 && c <= 2047) || (c >= 12352 && c <= 12591)))
				{ // not allowed!
					return false;
				}
			}
			return true;
		}

		/// <summary>Test if a javascript string would require escaping or not</summary>
		/// <param name="s">String to inspect</param>
		/// <returns><c>true</c> if all characters are valid</returns>
		public static bool IsCleanJavaScriptPropertyName(ReadOnlySpan<char> s)
		{
			int n = s.Length;
			if (n == 0) return true;

			if (char.IsDigit(s[0])) return false;

			foreach(var c in s)
			{
				//PERF: TODO: use SearchValues to speed this up!
				if (!(c is >= 'a' and <= 'z' || c is >= 'A' and <= 'Z' || c is >= '0' and <= '9' || c == '_'))
				{ // not allowed!
					return false;
				}
			}
			return true;
		}

		/// <summary>Encode a Javascript string known to contain at least one invalid character</summary>
		public static unsafe StringBuilder EncodeSlow(StringBuilder sb, ReadOnlySpan<char> s, bool includeQuotes)
		{
			int n = s.Length;
			if (includeQuotes) sb.Append('\'');
			//PERF: rewrite this to use ref byte + Unsafe.Add ?
			fixed (char* p = s)
			{
				char* ptr = p;
				while (n-- > 0)
				{
					char c = *ptr++;
					if ((c >= 'a' && c <= 'z') || c == ' ' || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == ',' || c == '-' || c == '_' || c == ':' || c == '/' || (c >= 880 && c <= 2047) || (c >= 12352 && c <= 12591))
						sb.Append(c);
					else if (c == '\n')
						sb.Append(@"\n");
					else if (c == '\r')
						sb.Append(@"\r");
					else if (c == '\t')
						sb.Append(@"\t");
					else if (c > 127) // 4 byte unicode
						sb.Append(@"\u").Append(((int) c).ToString("x4", CultureInfo.InvariantCulture));
					else // 2 byte ascii
						sb.Append(@"\x").Append(((int) c).ToString("x2", CultureInfo.InvariantCulture));
				}
			}
			if (includeQuotes) sb.Append('\'');
			return sb;
		}

		/// <summary>Encode a Javascript string known to contain at least one invalid character</summary>
		public static unsafe void EncodeSlow(ref ValueStringWriter sb, ReadOnlySpan<char> s, bool includeQuotes)
		{
			int n = s.Length;
			if (includeQuotes) sb.Write('\'');
			//PERF: rewrite this to use ref byte + Unsafe.Add ?
			fixed (char* p = s)
			{
				char* ptr = p;
				while (n-- > 0)
				{
					char c = *ptr++;
					if ((c >= 'a' && c <= 'z') || c == ' ' || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == ',' || c == '-' || c == '_' || c == ':' || c == '/' || (c >= 880 && c <= 2047) || (c >= 12352 && c <= 12591))
						sb.Write(c);
					else if (c == '\n')
						sb.Write('\n');
					else if (c == '\r')
						sb.Write('\r');
					else if (c == '\t')
						sb.Write('\t');
					else if (c > 127) // 4 byte unicode
						sb.Write(@"\u", ((int) c).ToString("x4", CultureInfo.InvariantCulture));
					else // 2 byte ascii
						sb.Write(@"\x", ((int) c).ToString("x2", CultureInfo.InvariantCulture));
				}
			}
			if (includeQuotes) sb.Write('\'');
		}

		/// <summary>Writes an encoded a JavaScript string literal</summary>
		/// <param name="output">Destination</param>
		/// <param name="text">Text to encode</param>
		/// <param name="includeQuotes">Si <c>true</c>, automatically add quotes around the string (<c>'...'</c>)</param>
		/// <returns>Encoded string</returns>
		/// <remarks>This method will not allocate memory if the original string is printable as-is, and <paramref name="includeQuotes"/> is <c>false</c></remarks>
		public static void EncodeTo(ref ValueStringWriter output, ReadOnlySpan<char> text, bool includeQuotes)
		{
			if (text.Length == 0)
			{
				if (includeQuotes)
				{
					output.Write(Tokens.EmptyString);
				}
				return;
			}

			// first pass to check if there escaping is required...
			if (IsCleanJavaScript(text))
			{ // nothing to escape
				if (includeQuotes)
				{
					output.Write('\'');
					output.Write(text);
					output.Write('\'');
				}
				else
				{
					output.Write(text);
				}
				return;
			}

			// second pass to escape all non-printable characters
			//PERF: optimize this use case!
			EncodeSlow(ref output, text, includeQuotes);
		}


		/// <summary>Encode a JavaScript string</summary>
		/// <param name="text">Text to encode</param>
		/// <param name="includeQuotes">Si <c>true</c>, automatically add quotes around the string (<c>'...'</c>)</param>
		/// <returns>Encoded string</returns>
		/// <remarks>This method will not allocate memory if the original string is printable as-is, and <paramref name="includeQuotes"/> is <c>false</c></remarks>
		public static string Encode(string? text, bool includeQuotes)
		{
			if (text == null) return includeQuotes ? Tokens.Null : string.Empty;
			if (text.Length == 0) return includeQuotes ? Tokens.EmptyString : string.Empty;
			// first pass to check if there escaping is required...
			if (IsCleanJavaScript(text.AsSpan())) return includeQuotes ? string.Concat(Tokens.Quote, text, Tokens.Quote) : text; // nothing to escape
			// second pass to escape all non-printable characters
			return EncodeSlow(new StringBuilder(text.Length + 16), text.AsSpan(), includeQuotes).ToString();
		}

		/// <summary>Encode the name of an object's property or field</summary>
		/// <param name="name">Name of a property or field of a Javascript object</param>
		/// <returns>If the name is "clean", it will be written as-is. Otherwise, it will be escaped</returns>
		/// <exception cref="System.ArgumentNullException">If 'name' is null</exception>
		/// <example>
		/// EncodePropertyName("foo") => "foo"
		/// EncodePropertyName("foo bar") => "'foo bar'"
		/// EncodePropertyName("foo'bar") => "'foo\'bar'"
		/// EncodePropertyName("") => "''"
		/// EncodePropertyName(null) => ArgumentNullException
		/// </example>
		public static string EncodePropertyName(string name)
		{
			Contract.NotNull(name);

			return IsValidIdentifier(name) ? name : Encode(name, true);
		}

		/// <summary>writes the encoded name of an object's property or field</summary>
		/// <param name="output">Destination</param>
		/// <param name="name">Name of a property or field of a Javascript object</param>
		/// <returns>If the name is "clean", it will be written as-is. Otherwise, it will be escaped</returns>
		/// <exception cref="System.ArgumentNullException">If 'name' is null</exception>
		/// <example>
		/// EncodePropertyName("foo") => "foo"
		/// EncodePropertyName("foo bar") => "'foo bar'"
		/// EncodePropertyName("foo'bar") => "'foo\'bar'"
		/// EncodePropertyName("") => "''"
		/// EncodePropertyName(null) => ArgumentNullException
		/// </example>
		public static void EncodePropertyNameTo(ref ValueStringWriter output, ReadOnlySpan<char> name)
		{
			if (IsValidIdentifier(name))
			{
				output.Write(name);
			}
			else
			{
				EncodeTo(ref output, name, true);
			}
		}

#if NET8_0_OR_GREATER
		private static readonly SearchValues<char> s_validIdentifierStartAscii = SearchValues.Create("$_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ");
		private static readonly SearchValues<char> s_validIdentifierPartAscii = SearchValues.Create("$_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");
#endif

		/// <summary>Tests if a character is valid as the first character in a Javascript identifier ("identifierStart")</summary>
		private static bool IsValidIdentifierStart(char c)
		{
			/* identifierStart:unicodeLetter | DOLLAR | UNDERSCORE | unicodeEscapeSequence  */

#if NET8_0_OR_GREATER
			if (c < 0x80 && s_validIdentifierStartAscii.Contains(c))
			{ // ASCII Letter, $ or '_'
				return true;
			}
#else
			if (c < 0x080 && ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '$' || c == '_'))
			{ // ASCII Letter, $ or '_'
				return true;
			}
#endif

			// Otherwise, this must be a "Unicode Letter"
			/* any character in the unicode categories:
			   "uppercase letter (Lu)",
			   "lowercase letter (Li)",
			   "titlecase letter (Lt)",
			   "modifier letter (Lm)",
			   "other letter (lo)",
			   "letter number (NI)" */
			return char.GetUnicodeCategory(c) is (
				   UnicodeCategory.LowercaseLetter 
				or UnicodeCategory.UppercaseLetter
				or UnicodeCategory.TitlecaseLetter
				or UnicodeCategory.ModifierLetter
				or UnicodeCategory.OtherLetter
				or UnicodeCategory.LetterNumber
			);
		}

		/// <summary>Tests if a character (that is not the first one) is valid as part of a JavaScript identifier ("identifierPart")</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsValidIdentifierPart(char c)
		{
#if NET8_0_OR_GREATER
			if (c < 0x80 && s_validIdentifierPartAscii.Contains(c))
			{ // ASCII Letter, Digit, $ or '_'
				return true;
			}
#else
			if (c < 0x80 && ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '$' || c == '_'))
			{ // ASCII Letter, Digit, $ or '_'
				return true;
			}
#endif
			return IsValidIndentifierPartUnicode(c);

			static bool IsValidIndentifierPartUnicode(char c) => char.GetUnicodeCategory(c) is
			(
				// unicodeLetter
				UnicodeCategory.LowercaseLetter or UnicodeCategory.UppercaseLetter or UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.LetterNumber
				// unicodeDigit
				or UnicodeCategory.DecimalDigitNumber
				// uniceeCombiningMark
				or UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
				// unicodeConnectorPunctuation
				or UnicodeCategory.ConnectorPunctuation
			);
		}

		/// <summary>Quickly test if a name can be used as a Javascript object's property name without escaping, or if it must be escaped first</summary>
		/// <param name="name">Name of a property or field</param>
		/// <returns>Return <c>true</c> if this is a valid name, and that it only contains ASCII. <c>false</c> it is invalid or if it contains Unicode that requires escaping</returns>
		/// <remarks>This can return <c>false</c> even for valid identifiers! The goal is to decide QUICKLY if escaping is required. A false negative would only use a bit more cpu but still produce a correct result.</remarks>
		public static bool IsValidIdentifier(string? name) => name != null && IsValidIdentifier(name.AsSpan());

		/// <summary>Quickly test if a name can be used as a Javascript object's property name without escaping, or if it must be escaped first</summary>
		/// <param name="name">Name of a property or field</param>
		/// <returns>Return <c>true</c> if this is a valid name, and that it only contains ASCII. <c>false</c> it is invalid or if it contains Unicode that requires escaping</returns>
		/// <remarks>This can return <c>false</c> even for valid identifiers! The goal is to decide QUICKLY if escaping is required. A false negative would only use a bit more cpu but still produce a correct result.</remarks>
		public static bool IsValidIdentifier(ReadOnlySpan<char> name)
		{
			/* According to ECMA-262 "ECMAScript Language Specification", section 7.6:

				Identifier :: 
					IdentifierName but not ReservedWord

				IdentifierName :: 
					IdentifierStart 
					IdentifierName IdentifierPart 

				IdentifierStart :: 
					UnicodeLetter 
					$ 
					_ 
					\ UnicodeEscapeSequence 

				IdentifierPart :: 
					IdentifierStart 
					UnicodeCombiningMark 
					UnicodeDigit 
					UnicodeConnectorPunctuation 
					\ UnicodeEscapeSequence 

				UnicodeLetter 
					any character in the Unicode categories Uppercase letter (Lu), Lowercase letter (Ll), Titlecase letter (Lt), 
					Modifier letter (Lm), Other letter (Lo), or Letter number (Nl). 

				UnicodeCombiningMark 
					any character in the Unicode categories Non-spacing mark (Mn) or Combining spacing mark (Mc) 

				UnicodeDigit 
					any character in the Unicode category Decimal number (Nd) 

				UnicodeConnectorPunctuation 
					any character in the Unicode category Connector punctuation (Pc) 

				UnicodeEscapeSequence 
					see 7.8.4. 

				HexDigit :: one of 
					0 1 2 3 4 5 6 7 8 9 a b c d e f A B C D E F
			*/

			if (name.Length == 0) return false;

			// First character must be a letter, '$' or '_'
			char first = name[0];
			if (!IsValidIdentifierStart(first))
			{
				return false;
			}

			// Remaining characters allow digits and other types of Unicode symbols
			var tail = name[1..];
			if (tail.Length > 0)
			{
				foreach(var c in tail)
				{
					if (!IsValidIdentifierPart(c))
					{ // not allowed!
						return false;
					}
				}
			}

			// The identifier must not be a reserved keyword ("if", "delete", ...)
			return !IsReservedKeyword(name);
		}

		internal static bool IsReservedKeyword(ReadOnlySpan<char> name)
		{
			if (name.Length == 0) return false;

			// we have to return false for any of the following keywords (case-sensitive):
			// "instanceof", "typeof", "break", "do", "new", "var", "case", "else", "return", "void", "catch", "finally", "continue", "for", "switch", "while", "this", "with", "debugger", "function", "throw", "default", "if", "try", "delete", "in"

			//TODO: FUTURE: We cannot use SearchValues<string> because, as of .NET 9, it only has a Contains(string) method, and no Contains(ReadOnlySpan<char>) overload (see https://github.com/dotnet/runtime/issues/104977)
			
			//note: the JIT should replace with an optimized "binary search"
			return name switch
			{
				"break" => true,
				"case" => true,
				"catch" => true,
				"continue" => true,
				"debugger" => true,
				"default" => true,
				"delete" => true,
				"do" => true,
				"else" => true,
				"finally" => true,
				"for" => true,
				"function" => true,
				"if" => true,
				"in" => true,
				"instanceof" => true,
				"new" => true,
				"return" => true,
				"switch" => true,
				"this" => true,
				"throw" => true,
				"try" => true,
				"typeof" => true,
				"var" => true,
				"void" => true,
				"while" => true,
				"with" => true,
				_ => false
			};
		}

	}

}
