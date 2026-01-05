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

#if DISABLED

namespace SnowBank.Shell.Prompt
{
	using System.Collections;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;

	/// <summary>Represents a complete prompt, made up of one or more <see cref="PromptToken"/> (or "words"), composed of one or more <see cref="PromptMarkupFragment"/> of different colors.</summary>
	/// <remarks>Each command will appends one or more several tokens, with optional internal subdivisions for syntax color highlighting.</remarks>
	public sealed class PromptBuilder
	{

		public PromptBuilder(IPromptTheme theme, ReadOnlySpan<PromptToken> tokens = default)
		{
			Contract.NotNull(theme);

			this.Buffer = new(tokens.Length);
			this.Buffer.AddRange(tokens);
			this.Theme = theme;
		}

		/// <summary>Internal buffer used to add tokens</summary>
		private List<PromptToken> Buffer { get; }

		public readonly IPromptTheme Theme;

		/// <summary>Adds a new token</summary>
		/// <param name="token">Token added to the end of the prompt</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(PromptToken token)
		{
			this.Buffer.Add(token);
		}

		/// <summary>Adds a new token composed of a single text fragment</summary>
		/// <param name="literal">Raw literal</param>
		/// <param name="foreground">Optional foreground color</param>
		/// <param name="background">Optional background color</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(string type, string literal, string? foreground = null, string? background = null)
			=> this.Add(PromptToken.Create(type, literal, foreground, background));

		/// <summary>Adds a new token composed of a single text fragment</summary>
		/// <param name="fragment">Fragment that makes up the new token</param>
		public void Add(string type, PromptMarkupFragment fragment) => this.Add(PromptToken.Create(type, fragment));

		/// <summary>Adds a new token composed of one or more text fragments</summary>
		/// <param name="fragments">Fragments that make up the new token</param>
		public void Add(string type, scoped ReadOnlySpan<PromptMarkupFragment> fragments) => this.Add(PromptToken.Create(type, fragments));

		/// <summary>Number of tokens in the prompt</summary>
		public int Count => this.Buffer.Count;

		/// <summary>Returns the token at the specified index</summary>
		public PromptToken this[int index] => this.Buffer[index];

		/// <summary>Returns the token at the specified index</summary>
		public PromptToken this[Index index] => this.Buffer[index];

		/// <summary>Span of all the tokens</summary>
		public ReadOnlySpan<PromptToken> Tokens => CollectionsMarshal.AsSpan(this.Buffer);

		/// <summary>Last token (or default if no tokens available yet)</summary>
		public PromptToken Last => this.Buffer.Count > 0 ? this.Buffer[^1] : default;

		public PromptToken[] ToArray() => this.Buffer.ToArray();

		public override string ToString()
		{
			var sw = new ValueStringWriter();
			var tokens = this.Tokens;
			for (int i = 0; i < tokens.Length; i++)
			{
				if (i > 0) sw.Write(' ');
				tokens[i].AppendRawTo(ref sw);
			}
			return sw.ToStringAndDispose();
		}

		public PromptTokens Build()
		{
			var rawText = this.ToString();
			var tokens = this.Buffer.ToArray();
			return new(rawText.AsMemory(), tokens, this.Theme);
		}

	}

	/// <summary>Represents a complete prompt, made up of one or more <see cref="PromptToken"/> (or "words"), composed of one or more <see cref="PromptMarkupFragment"/> of different colors.</summary>
	/// <remarks>Each command will appends one or more several tokens, with optional internal subdivisions for syntax color highlighting.</remarks>
	public sealed record PromptTokens : IReadOnlyList<PromptToken>
	{

		public static PromptTokens Create(IPromptTheme theme) => new(default, default, theme);

		public PromptTokens(ReadOnlyMemory<char> rawText, ReadOnlyMemory<PromptToken> tokens, IPromptTheme theme)
		{
			Contract.NotNull(rawText);
			Contract.NotNull(theme);

			this.Tokens = tokens;
			this.RawText = rawText;
			this.Theme = theme;
		}

		private ReadOnlyMemory<PromptToken> Tokens { get; }

		public ReadOnlyMemory<char> RawText { get; init; }

		public IPromptTheme Theme { get; init; }

		public string? Background { get; init; }

		/// <summary>Number of tokens in the prompt</summary>
		public int Count => this.Tokens.Length;

		/// <summary>Returns a span with all the tokens in this prompt</summary>
		public ReadOnlySpan<PromptToken> Span => this.Tokens.Span;

		/// <summary>Returns the token at the specified index</summary>
		public PromptToken this[int index] => this.Tokens.Span[index];

		/// <summary>Returns the token at the specified index</summary>
		public PromptToken this[Index index] => this.Tokens.Span[index];

		/// <summary>Last token (or default if no tokens available yet)</summary>
		public PromptToken Last
		{
			get
			{
				var items = this.Tokens.Span;
				return items.Length != 0 ? items[^1] : default;
			}
		}

		public PromptToken[] ToArray() => this.Tokens.ToArray();

		public string Render(IPromptRenderer renderer)
		{
			var sw = new ValueStringWriter();
			renderer.ToMarkup(ref sw, this);
			return sw.ToStringAndDispose();
		}

		public ReadOnlySpan<PromptToken>.Enumerator GetEnumerator() => this.Tokens.Span.GetEnumerator();

		IEnumerator<PromptToken> IEnumerable<PromptToken>.GetEnumerator() => MemoryMarshal.ToEnumerable(this.Tokens).GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => MemoryMarshal.ToEnumerable(this.Tokens).GetEnumerator();

		public override string ToString() => this.RawText.ToString();

		public PromptBuilder ToBuilder()
		{
			return new PromptBuilder(this.Theme, this.Tokens.Span);
		}

	}

}

#endif
