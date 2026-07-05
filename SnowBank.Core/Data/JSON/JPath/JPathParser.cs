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

namespace SnowBank.Data.Json.JPath
{
	using System.Linq.Expressions;
	using SnowBank.Text;

	/// <summary>Parser for JPath expressions</summary>
	[DebuggerDisplay("Cursor={Cursor}, Start={Start}, SubStart={SubStart}")]
	[DebuggerNonUserCode]
	internal ref struct JPathParser
	{
		public readonly ReadOnlySpan<char> Path;
		public int Cursor;
		public int Start;
		public int SubStart;
		public ReadOnlySpan<char> Literal;
		public long? Integer;
		public double? Number;

		public JPathParser(ReadOnlySpan<char> path)
		{
			if (path.IsEmpty || path.IsWhiteSpace())
			{
				throw SyntaxError("JPath query cannot be empty.");
			}
			this.Path = path;
		}

		private const char EOF = '\xFFFF';

		private enum Token
		{
			/// <summary>Reached end of string</summary>
			End,
			/// <summary>White space</summary>
			WhiteSpace,
			/// <summary>Identifier (usually a property name)</summary>
			Identifier,
			/// <summary>'.' is used to represent property indexing</summary>
			Dot,
			/// <summary>String literal ("'foo'", "'hello\'world'", ...)</summary>
			StringLiteral,
			/// <summary>Integer literal (array indexer)</summary>
			NumberLiteral,
			/// <summary>'..' means "any direct or indirect child" </summary>
			DotDot,
			/// <summary>'[' starts an array indexing or filter sub-expression</summary>
			OpenBracket,
			/// <summary>']' ends an array indexing or filter sub-expression</summary>
			CloseBracket,
			/// <summary>'(' is used to start a sub-expression</summary>
			OpenParens,
			/// <summary>')' is used to end a sub-expression</summary>
			CloseParens,
			/// <summary>'?' is used to mark the start of a conditional expression</summary>
			Conditional,
			/// <summary>'??' is used to coalesce a null value (provides a default value)</summary>
			Coalesce,
			/// <summary>':' is used to separate indexes in ranges</summary>
			Colon,
			/// <summary>'!' means "when the following expression is NOT true"</summary>
			NotSymbol,
			/// <summary>'==' operator</summary>
			EqualTo,
			/// <summary>'!=' operator</summary>
			NotEqualTo,
			/// <summary>'&lt;' operator</summary>
			LessThan,
			/// <summary>'&lt;=' operator</summary>
			LessThanOrEqual,
			/// <summary>'>' operator</summary>
			GreaterThan,
			/// <summary>'>=' operator</summary>
			GreaterThanOrEqual,
			/// <summary>'&amp;&amp;' operator means both sides are true.</summary>
			AndAlso,
			/// <summary>'||' operator means at least one is true.</summary>
			OrElse,
		}

		private enum State
		{
			Expression,
			ExpectIdentifier,
			BracketStart,
			ExpectCloseBracket,
			ExpectCloseParens,
		}

		/// <summary>Reads the next character from the string</summary>
		private char ReadNextChar()
		{
			int p = this.Cursor;
			var s = this.Path;
			if (p >= s.Length) return EOF;
			this.Cursor = p + 1;
			return s[p];
		}

		/// <summary>Peek at the next character in the string</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private char PeekNextChar()
		{
			int p = this.Cursor;
			var s = this.Path;
			return (uint) p >= (uint) s.Length ? EOF : s[p];
		}

		/// <summary>Peek at the last read character</summary>
		/// <remarks>Should mostly be used by error handlers to customize message according to the context</remarks>
		private char PeekPreviousChar()
		{
			int p = this.Cursor - 1;
			var s = this.Path;
			return (uint) p >= (uint) s.Length ? EOF : s[p];
		}

		/// <summary>Skip to the next character in the string</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Advance()
		{
			this.Cursor++;
		}

		/// <summary>Parse the next token</summary>
		private Token ReadNextToken()
		{
			this.SubStart = this.Cursor;
			char tok = PeekNextChar();
			switch (tok)
			{
				case '.':
				{ // '.' or '..' ?

					// may be followed by another '.'?
					Advance();

					if (PeekNextChar() == '.')
					{ // yes, double dot
						Advance();
						return Token.DotDot;
					}
					// no, single dot
					return Token.Dot;
				}
				case '[':
				{
					Advance();
					return Token.OpenBracket;
				}
				case ']':
				{
					Advance();
					return Token.CloseBracket;
				}
				case '\'':
				{
					ReadString();
					return Token.StringLiteral;
				}
				case '<':
				{
					Advance();
					if (PeekNextChar() == '=')
					{
						Advance();
						return Token.LessThanOrEqual;
					}
					return Token.LessThan;
				}
				case '>':
				{
					Advance();
					if (PeekNextChar() == '=')
					{
						Advance();
						return Token.GreaterThanOrEqual;
					}
					return Token.GreaterThan;
				}
				case '=':
				{
					Advance();
					// accept both '=' and '==' !
					if (PeekNextChar() == '=')
					{
						Advance();
					}
					return Token.EqualTo;
				}
				case '!':
				{
					Advance();
					if (PeekNextChar() == '=')
					{
						Advance();
						return Token.NotEqualTo;
					}
					return Token.NotSymbol;
				}
				case '(':
				{
					Advance();
					return Token.OpenParens;
				}
				case ')':
				{
					Advance();
					return Token.CloseParens;
				}
				case '?':
				{
					Advance();
					if (PeekNextChar() == '?')
					{
						Advance();
						return Token.Coalesce;
					}
					return Token.Conditional;
				}
				case ':':
				{
					Advance();
					return Token.Colon;
				}
				case '&':
				{
					Advance();
					if (PeekNextChar() == '&')
					{
						Advance();
						return Token.AndAlso;
					}
					throw SyntaxError("Unexpected token '&'. Did you mean '&&'?");
				}
				case '|':
				{
					Advance();
					if (PeekNextChar() == '|')
					{
						Advance();
						return Token.OrElse;
					}
					throw SyntaxError("Unexpected token '|'. Did you mean '||'?");
				}
				case EOF:
				{
					return Token.End;
				}
			}

			if (char.IsDigit(tok) || tok == '-' || tok == '+')
			{
				if (PeekPreviousChar() == '[')
				{
					ReadInteger();
				}
				else
				{
					ReadNumber();
				}
				return Token.NumberLiteral;
			}

			if (tok == '^' && PeekPreviousChar() == '[')
			{ // "^1"
				ReadInteger();
				return Token.NumberLiteral;
			}

			if (IsIdentifierToken(tok))
			{
				ReadIdentifier();
				return Token.Identifier;
			}

			if (char.IsWhiteSpace(tok))
			{
				do
				{
					Advance();
					tok = PeekNextChar();
				}
				while (char.IsWhiteSpace(tok));
				return Token.WhiteSpace;
			}

			throw SyntaxError($"Unexpected character '{tok}'");
		}

		/// <summary>Test if the token is an identifier (letter, <c>_</c>, <c>$</c> or <c>@</c>)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsIdentifierToken(char tok) => char.IsLetter(tok) || tok == '_' || tok == '$' || tok == '@';

		/// <summary>Read an identifier from the string</summary>
		private void ReadIdentifier()
		{
			// Identifiers starts with '_' or letter, and is only composed of letter, digits, '-' or '_', and stops on the first invalid character
			// Valid: "abc", "_abc", "abc123", "_123", "a-b", "a-", "______"
			// Invalid: "1abc", "-abc", "@hello", "*abc", ....

			Advance();

			while (true)
			{
				char c = PeekNextChar();
				if (!char.IsDigit(c) && !char.IsLetter(c) && c != '_' && c != '-')
				{
					break;
				}
				Advance();
			}

			this.Literal = this.Path.Slice(this.SubStart, this.Cursor - this.SubStart);
		}

		/// <summary>Read an integer (array index) from the string</summary>
		private void ReadInteger()
		{
			// Integers are numbers, with optional '-' sign. We recognize '^1' as '-1' (to allow c#-like index syntax)
			// Valid: "1", "123456789", "-42", "^1"

			long value = 0;
			char c = ReadNextChar();

			bool negative = c == '-' || c == '^';
			if (!negative)
			{
				value = c - '0';
			}
			while (true)
			{
				c = PeekNextChar();
				if (!char.IsDigit(c))
				{
					break;
				}
				value = checked(value * 10 + (c - '0'));
				Advance();
			}

			this.Integer = negative ? -value : value;
		}

		/// <summary>Read a number literal from the string</summary>
		private void ReadNumber()
		{
			// Numbers include integers and floating point numbers, possibly in scientific notation
			// Valid: "0", "1", "123456789", "-42", "+666", "1.23", "1E10", "-42E-666"

			int start = this.Cursor;

			bool hasDot = false;
			bool hasExp = false;
			bool hasSign = false;
			long value = 0;
			while (true)
			{
				char c = PeekNextChar();

				//TODO: handle '.' !
				if (char.IsDigit(c))
				{
					value = value * 10 + (c - '0');
					Advance();
					continue;
				}

				if (c is '.')
				{
					if (hasDot) break;
					hasDot = true;
					Advance();
					continue;
				}
				if (c is 'E' && !hasExp)
				{
					hasExp = true;
					hasSign = false;
					Advance();
					continue;
				}
				if (c is '-' or '+' && !hasSign)
				{
					hasSign = true;
					if (!hasExp) value = -value;
					Advance();
					continue;
				}

				if (c is not (EOF or ')' or ']') && !char.IsWhiteSpace(c))
				{
					throw SyntaxError("Invalid number literal.");
				}

				if (hasDot || hasExp)
				{
#if NET5_0_OR_GREATER
					this.Number = double.Parse(this.Path.Slice(start, this.Cursor - start));
#else
					// span-based parse is not on netstandard2.0
					this.Number = double.Parse(this.Path.Slice(start, this.Cursor - start).ToString());
#endif
				}
				else
				{
					this.Integer = hasSign ? -value : value;
				}

				break;
			}
		}

		/// <summary>Read a string literal from the string</summary>
		private void ReadString()
		{
			// skip first quote
			Advance();
			int start = this.Cursor;

			while (true)
			{
				char c = ReadNextChar();
				if (c == '\'')
				{
					break;
				}
				if (c == EOF)
				{
					throw SyntaxError("Unterminated string literal");
				}
			}
			this.Literal = this.Path.Slice(start, this.Cursor - start - 1);
		}

		/// <summary>Parse an expression (or sub-expression)</summary>
		/// <exception cref="FormatException">If the parsed expression is invalid</exception>
		public JPathExpression ParseExpression(JPathExpression top, char type = '\0')
		{
			//TODO: refactor this monstrosity!
			var state = State.Expression;
			var expr = top;
			var andExpr = default(JPathExpression); // left side of currently parsed '&&' expression
			var orExpr = default(JPathExpression); // left side of currently parsed '||' expression
			var cmpExpr = default(JPathExpression); // left side of currently parsed comparison expression ('==', '<=', ...)
			ExpressionType cmpOp = 0; // currently parsed comparison operation ('==', '<', '<=' ...)
			while (true)
			{
				Token tok = ReadNextToken();
				//Console.WriteLine($"=> {tok} (Next='{context.Next}', Lit='{context.Literal}', Int={context.Integer})");
				switch (tok)
				{
					case Token.End:
					{
						if (state == State.ExpectIdentifier)
						{
							throw SyntaxError("Missing expected identifier.");
						}
						if (state == State.ExpectCloseBracket)
						{
							throw SyntaxError("Missing expected ']' token.");
						}
						if (state == State.ExpectCloseParens)
						{
							throw SyntaxError("Missing expected ')' token.");
						}
						if (type == '[')
						{
							throw SyntaxError("Missing expected ']' token.");
						}
						if (type == '(')
						{
							throw SyntaxError("Missing expected ')' token.");
						}
						goto done;
					}
					case Token.WhiteSpace:
					{
						if (state == State.ExpectIdentifier)
						{
							throw SyntaxError("Missing expected identifier.");
						}
						break;
					}
					case Token.Dot:
					{
						if (state != State.Expression)
						{
							throw SyntaxError("Unexpected '.' token.");
						}
						state = State.ExpectIdentifier;
						break;
					}
					case Token.Identifier:
					{
						if (state != State.ExpectIdentifier && state != State.Expression)
						{
							throw SyntaxError($"Unexpected identifier '{this.Literal.ToString()}'.");
						}

						var name = this.Literal;
						this.Literal = null;

						if (name.Length == 1 && name[0] is ('$' or '@'))
						{
							// token cannot be another identifier!
							if (!ReferenceEquals(expr, top))
							{
								throw SyntaxError($"Special identifier '{name[0]}' is only allowed at the start of an expression or sub-expression.");
							}

							if (IsIdentifierToken(PeekNextChar()))
							{
								++this.Cursor;
								throw SyntaxError($"Special identifier '{name[0]}' cannot be followed by another identifier.");
							}
							expr = name[0] == '$' ? JPathExpression.Root : JPathExpression.Current;
							state = State.Expression;
							continue;
						}

						if (PeekNextChar() == '(')
						{ // start of function or operator?
							Advance();
							var prevStart = this.Start;
							this.Start = this.Cursor;
							var subExpr = ParseExpression(JPathExpression.Current, type: '(');
							//note: ')' should already have been consumed
							if (name is "not")
							{
								// not(..) does not have a "source", so it can only be found at the start of an expression
								if (expr is not JPathSpecialToken)
								{
									throw SyntaxError("Operator not(..) must be at the start of an expression of sub-expression");
								}
								expr = JPathExpression.Not(subExpr);
								break;
							}
							this.Start = prevStart;
							throw SyntaxError($"Unsupported function '{name.ToString()}'");
						}

						expr = JPathExpression.Property(expr, name.ToString());
						state = State.Expression;
						break;
					}

					case Token.OpenBracket:
					{
						// possible cases:
						// - arr index: '...[1]', '...[123]', '...[-1]', '...[^1]'
						// - half range: '...[:123]'
						// - subexpression: '...[(anything else)]'
						char c = PeekNextChar();

						if (char.IsDigit(c) || c == '-' || c == '^')
						{ // looks like an array indexer (or the start of a range)
							state = State.BracketStart;
							break;
						}

						if (c == ']')
						{ // '[]' means All items of array
							Advance();
							expr = expr.All();
							state = State.Expression;
							break;
						}

						if (c == EOF)
						{ // unclosed bracket at the end of the query
							throw SyntaxError("Truncated '[' token.");
						}

						// it should be a filter expression
						var prevStart = this.Start;
						this.Start = this.Cursor;
						var subExpr = ParseExpression(JPathExpression.Current, type: '[');
						expr = expr.Matching(subExpr);
						this.Start = prevStart;

						//note: reading the filter expression consumes the ']' so we are back on track!
						state = State.Expression;
						break;
					}

					case Token.CloseBracket:
					{
						if (state != State.ExpectCloseBracket)
						{
							if (type == '[' && state == State.Expression)
							{
								goto done;
							}

							throw PeekPreviousChar() == ']'
								? SyntaxError("Redundant ']' token.")
								: SyntaxError("Unexpected ']' token.");
						}
						state = State.Expression;
						break;
					}

					case Token.NumberLiteral:
					{
						if (state == State.BracketStart)
						{ // we are parsing an array indexer, like "...[123]"
							if (!this.Integer.HasValue)
							{ // We had something like [123.4] which is not legal
								if (this.Number.HasValue)
								{
									throw SyntaxError("Cannot using decimal number as array indexer.");
								}
								else
								{
									throw SyntaxError("Unexpected array indexer literal.");
								}
							}
							expr = expr.At(checked((int) this.Integer.Value));
							this.Integer = null;
							state = State.ExpectCloseBracket;
							break;
						}

						if (cmpExpr != null)
						{
							if (this.Integer == null && this.Number == null) throw SyntaxError("Unexpected number literal.");
							expr = JPathExpression.BinaryOperator(cmpOp, cmpExpr, this.Integer != null ? JsonNumber.Return(this.Integer.Value) : JsonNumber.Return(this.Number!.Value));
							this.Integer = null;
							this.Number = null;
							cmpExpr = null;
							break;
						}

						throw SyntaxError("Unexpected integer literal.");
					}

					case Token.StringLiteral:
					{
						if (cmpExpr != null)
						{
							expr = JPathExpression.BinaryOperator(cmpOp, cmpExpr, this.Literal.ToString());
							cmpExpr = null;
							break;
						}
						throw SyntaxError("Unexpected string literal outside of expression");
					}

					case Token.OpenParens:
					{
						if (state != State.Expression)
						{
							throw SyntaxError("Unexpected '(' token.");
						}
						if (!ReferenceEquals(expr, top))
						{
							throw SyntaxError("Cannot quote part of a sub-expression.");
						}

						// here it is probably a quote that will look like "(subexpr)[..]"
						var prevStart = this.Start;
						this.Start = this.Cursor;
						var subExpr = ParseExpression(top, type: '(');
						expr = JPathExpression.Quote(subExpr);
						this.Start = prevStart;
						break;
					}
					case Token.CloseParens:
					{
						if (state != State.ExpectCloseParens)
						{
							if (type == '(' && state == State.Expression && !ReferenceEquals(top, expr))
							{ // and we're done!
								goto done;
							}

							switch (PeekPreviousChar())
							{
								case '(': throw SyntaxError("Empty '()' clause is invalid.");
								case ')': throw SyntaxError("Redundant ')' token.");
								default:  throw SyntaxError("Unexpected ')' token.");
							}
						}
						state = State.Expression;
						break;
					}

					case Token.EqualTo:
					{ // "=="
						cmpExpr = expr;
						expr = top;
						cmpOp = ExpressionType.Equal;
						break;
					}
					case Token.NotEqualTo:
					{ // "!="
						cmpExpr = expr;
						expr = top;
						cmpOp = ExpressionType.NotEqual;
						break;
					}
					case Token.LessThan:
					{ // "<"
						cmpExpr = expr;
						expr = top;
						cmpOp = ExpressionType.LessThan;
						break;
					}
					case Token.LessThanOrEqual:
					{ // "<="
						cmpExpr = expr;
						expr = top;
						cmpOp = ExpressionType.LessThanOrEqual;
						break;
					}
					case Token.GreaterThan:
					{ // ">"
						cmpExpr = expr;
						expr = top;
						cmpOp = ExpressionType.GreaterThan;
						break;
					}
					case Token.GreaterThanOrEqual:
					{ // ">="
						cmpExpr = expr;
						cmpOp = ExpressionType.GreaterThanOrEqual;
						expr = top;
						break;
					}

					case Token.AndAlso:
					{
						if (cmpExpr != null)
						{
							throw SyntaxError($"Missing right of side of {cmpOp} operator in sub-expression.");
						}

						if (andExpr != null)
						{ // close previous '&&'
							expr = JPathExpression.AndAlso(andExpr, expr);
						}

						andExpr = expr;
						expr = top;
						break;
					}
					case Token.OrElse:
					{
						if (cmpExpr != null)
						{
							throw SyntaxError($"Missing right of side of {cmpOp} operator in sub-expression.");
						}

						if (andExpr != null)
						{ // close previous '&&'
							expr = JPathExpression.AndAlso(andExpr, expr);
							andExpr = null;
						}

						if (orExpr != null)
						{ // close previous '||'
							expr = JPathExpression.OrElse(orExpr, expr);
						}
						orExpr = expr;
						expr = top;
						break;
					}
					default:
					{
						throw SyntaxError($"Unexpected {tok} token.");
					}
				}
			}

		done:
			if (cmpExpr != null) expr = JPathExpression.BinaryOperator(cmpOp, cmpExpr, expr); //REVIEW: or bug?
			if (andExpr != null) expr = JPathExpression.AndAlso(andExpr, expr);
			if (orExpr != null) expr = JPathExpression.OrElse(orExpr, expr);
			if (type != '\0' && ReferenceEquals(expr, top))
			{
				throw SyntaxError($"Truncated '{type}' token.");
			}
			return expr;
		}

		private Exception SyntaxError(string message)
		{
			var start = this.Start;
			var subStart = this.SubStart;
			int pos = this.Cursor;
			var sb = StringBuilderCache.Acquire(64);
			sb.Append("Syntax error in JPath expression at offset ").Append(pos).Append(": ").Append(message)
			  .Append("\r\nPath: ").Append(this.Path.ToString());

			if (pos > 0)
			{
				// "   *-----^^^^"
				//     ^____________ Start of current "context" (top, of [...])
				//           ^______ Start of current token
				//              ^___ End of current token
				sb.Append("\r\n      ").Append(' ', start);
				int r = subStart - start;
				if (r > 0) sb.Append('*').Append('-', r - 1);
				sb.Append('^', Math.Max(pos - subStart, 1));
			}
			return new FormatException(StringBuilderCache.GetStringAndRelease(sb));
		}

	}

}
