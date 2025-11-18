#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

//#define ENABLE_SOURCE_POSITION

namespace SnowBank.Data.Json
{
	using SnowBank.Text;

	/// <summary>Tokenizer for JSON text documents</summary>
	/// <remarks>Must always be passed "by ref" !</remarks>
	public struct CrystalJsonTokenizer<TReader> : IDisposable
		where TReader : struct, IJsonReader
	{

		private const char EMPTY_TOKEN = '\0';

		/// <summary>Source JSON (si mode Stream)</summary>
		public TReader Source;

		/// <summary>Buffer used to "push back" a previously read character (look ahead)</summary>
		private char Token;

		/// <summary>JSON settings used by the tokenizer</summary>
		public readonly CrystalJsonSettings Settings;

#if ENABLE_SOURCE_POSITION
		private long m_offset;
		private int m_position;
		private int m_line;
#endif

		private readonly CrystalJsonSettings.StringInterning InternMode;

		/// <summary>Default comparator for JSON Object key names</summary>
		public readonly IEqualityComparer<string> FieldComparer;

		private StringTable? Cache;

		public CrystalJsonTokenizer(TReader source, CrystalJsonSettings? settings)
			: this()
		{
			this.Source = source;
			this.Settings = settings ?? CrystalJsonSettings.Json;
			this.InternMode = this.Settings.InterningMode;
			this.FieldComparer = this.Settings.GetKeyComparer();
		}

		/// <inheritdoc />
		public void Dispose()
		{
			this.Source = default;
			if (this.Cache != null)
			{
				this.Cache.Dispose();
				this.Cache = null;
			}
		}

#if ENABLE_SOURCE_POSITION

		/// <summary>Number of characters read from the start of the document</summary>
		public long Offset => m_offset;

		/// <summary>Number of characters read from the start of the current line (0-based)</summary>
		public long Position => m_position;

		/// <summary>Number of lines read from the start of the document (0-based)</summary>
		public long Line => m_line;
#endif

		/// <summary>Reads the next non-white space character</summary>
		/// <returns>Next character, or <see langword="-1"/> if there are no more characters to read</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal char ReadNextToken()
		{
			// if a character was "pushed back" we return it; otherwise, we read from the buffer
			
			char c = this.Token;
			if (c == EMPTY_TOKEN)
			{ // no cached character, read from the stream
				c = ReadOne();
			}
			else
			{ // return the cached character
				this.Token = EMPTY_TOKEN;
			}

#if NET8_0_OR_GREATER
			return !CrystalJsonParser.WhiteCharsMap.Contains(c) ? c : ConsumeWhiteSpaces();
#else
			return c >= CrystalJsonParser.WHITE_CHAR_MAP ? c : ConsumeWhiteSpaces(c);
#endif
		}

#if NET8_0_OR_GREATER
		
		[MethodImpl(MethodImplOptions.NoInlining)]
		private char ConsumeWhiteSpaces()
		{
			var wc = CrystalJsonParser.WhiteCharsMap;
			// caller already checked that c is a whitespace, but there could be more...
			char c;
			do
			{
				c = ReadOne();
			}
			while (wc.Contains(c));
			
			return c;
		}
		
#else

		[MethodImpl(MethodImplOptions.NoInlining)]
		private char ConsumeWhiteSpaces(char token)
		{
			var wc = CrystalJsonParser.WhiteCharsMap;

			char c = token;
			// caller only guessed, we have to check if c is a whitespace
			while (wc[c])
			{
				c = ReadOne();
				if (c >= CrystalJsonParser.WHITE_CHAR_MAP) break;
			}
			return c;
		}
		
#endif

		/// <summary>Puts back a character that was previously read</summary>
		/// <remarks>
		/// <para>This character will be returned by the next call to <see cref="ReadOne"/></para>
		/// <para>Only one character can be pushed until the next read.</para>
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void Push(char c)
		{
			Paranoid.Requires(this.Token == EMPTY_TOKEN, "Cannot push more than one character");
			this.Token = c;
		}

		internal bool HasReadNextToken() => this.Token != EMPTY_TOKEN;

		/// <summary>Reads the next character from the stream</summary>
		/// <returns>Next character, of <see langword="-1"/> if there are no more characters to read.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal char ReadOne()
		{
			char c = (char) this.Source.Read();
#if ENABLE_SOURCE_POSITION
			UpdateSourcePosition(c);
#endif
			return c;
		}

#if ENABLE_SOURCE_POSITION
		[MethodImpl(MethodImplOptions.NoInlining)]
		private void UpdateSourcePosition(char c)
		{
			if (c == '\n')
			{
				++m_line;
				m_position = 0;
				++m_offset;
			}
			else if (c != CrystalJsonParser.EndOfStream)
			{
				++m_position;
				++m_offset;
			}
		}
#endif

		internal void ReadExpectedKeyword(ReadOnlySpan<char> values)
		{
			char c;
			foreach (char value in values)
			{
				c = ReadOne();
				if (c != value)
				{
					if (c == CrystalJsonParser.EndOfStream)
					{
						throw FailUnexpectedEndOfStream(null);
					}
					else
					{
						throw FailInvalidSyntax($"Invalid character '{c}' found while expecting '{value}'");
					}
				}
			}
			
			// must be followed either by the end of stream, or a separator/terminator
			c = ReadOne();
			if (c == CrystalJsonParser.EndOfStream)
			{
				return;
			}

			// put back the character
			Push(c);

			if (char.IsLetterOrDigit(c))
			{
				throw FailInvalidSyntax($"Invalid character '{c}' found after expected keyword");
			}
		}

#if ENABLE_SOURCE_POSITION

		[Pure]
		internal JsonSyntaxException FailInvalidSyntax(string reason) => new("Invalid JSON syntax", reason, m_offset - 1, m_line + 1, m_position);

		[Pure]
		internal JsonSyntaxException FailInvalidSyntax(ref DefaultInterpolatedStringHandler reason) => new("Invalid JSON syntax", reason.ToStringAndClear(), m_offset - 1, m_line + 1, m_position);

		[Pure]
		internal JsonSyntaxException FailUnexpectedEndOfStream(string? reason) => new("Unexpected end of stream", reason, m_offset - 1, m_line + 1, m_position);

#else

		[Pure]
		internal readonly JsonSyntaxException FailInvalidSyntax(string reason) => new("Invalid JSON syntax", reason);

		[Pure]
		internal readonly JsonSyntaxException FailInvalidSyntax(ref DefaultInterpolatedStringHandler reason) => new("Invalid JSON syntax", reason.ToStringAndClear());

		[Pure]
		internal readonly JsonSyntaxException FailUnexpectedEndOfStream(string? reason) => new("Unexpected end of stream", reason);

#endif

		internal StringTable? GetStringTable(JsonLiteralKind kind)
		{
			switch (this.InternMode)
			{
				case CrystalJsonSettings.StringInterning.Default:
				{ // Default: allow fields
					if (kind != JsonLiteralKind.Field)
					{
						return null;
					}
					break;
				}
				case CrystalJsonSettings.StringInterning.IncludeNumbers:
				{ // Numbers: allow fields and numbers
					if (kind == JsonLiteralKind.Value)
					{
						return null;
					}
					break;
				}
				case CrystalJsonSettings.StringInterning.Disabled:
				{ // Disabled: nothing
					return null;
				}
			}
			
			return this.Cache ??= StringTable.GetInstance();
		}

	}

}
