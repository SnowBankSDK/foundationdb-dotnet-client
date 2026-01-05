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

namespace SnowBank.Shell.Prompt
{
	using System.Collections.Immutable;
	using System.Diagnostics;
	using SnowBank.Buffers;

	/// <summary>Snapshot of a prompt at a point in time</summary>
	/// <remarks>Any keystroke transforms a current prompt state into a new prompt state (which could be the same)</remarks>
	public sealed record PromptState : ICanExplain
	{

		public static PromptState CreateEmpty(RootPromptCommand root, IPromptTheme theme) => new()
		{
			Change = PromptChange.Empty,
			Render = null, // not yet rendered
			LastKey = '\0',
			RawText = "",
			RawToken = "",
			Tokens = PromptTokenStack.Empty,
			Candidates = [ ],
			Command = root,
			CommandBuilder = root.StartNew(),
			Theme = theme,
		};

		/// <summary>The change due to the last user action (from the previous state to this state)</summary>
		/// <remarks>
		/// <para>For example, if the change is <see cref="PromptChange.Completed"/>, we can use this to perform a "soft space" when typing text right after a TAB</para>
		/// <para>Ex: typing <c>hel[TAB]wor[TAB]</c> will autocomplete to <c>hello world</c> without having to type the space between both tokens</para>
		/// </remarks>
		public required PromptChange Change { get; init; }

		public required RenderState? Render { get; init; }

		public char LastKey { get; init; }

		public bool IsDone() => this.Change is PromptChange.Done or PromptChange.Aborted;

		public PromptState? Parent { get; init; }

		/// <summary>List of completed tokens in the prompt</summary>
		/// <remarks>
		/// <para>Does not include the token currently being edited!</para>
		/// </remarks>
		public required PromptTokenStack Tokens { get; init; }

		/// <summary>The raw text of the whole prompt</summary>
		public required string RawText { get; init; }

		/// <summary>The raw text of the currently edited token</summary>
		public required string RawToken { get; init; }

		public required IPromptCommandDescriptor Command { get; init; }

		public required IPromptCommandBuilder CommandBuilder { get; init; }

		public required IPromptTheme Theme { get; init; }

		/// <summary>List of all auto-complete candidates</summary>
		public required string[] Candidates { get; init; }
		/// <summary>There is an exact match with an auto-complete</summary>
		public string? ExactMatch { get; init; }

		/// <summary>There is a common prefix to all remaining auto-complete candidates</summary>
		public string? CommonPrefix { get; init; }

		public void Explain(ExplanationBuilder builder)
		{
			builder.WriteLine($"Prompt:  '{this.RawText}'");
			builder.WriteLine($"Change:  {this.Change}, Char={(this.LastKey < 32 ? $"0x{(int) this.LastKey:x02}" : $"'{this.LastKey}'")}");
			builder.WriteLine($"Tokens:  {this.Tokens.ToString()} (count={this.Tokens.Count})");
			builder.WriteLine($"Command: <{this.Command.GetType().GetFriendlyName()}>, '{this.Command.SyntaxHint}'");
			builder.WriteLine($"Builder: <{this.CommandBuilder?.GetType().GetFriendlyName()}>, {this.CommandBuilder}");
			if (this.CommandBuilder is ICanExplain explain)
			{
				builder.ExplainChild(explain);
			}

			if (this.Candidates.Length > 0)
			{
				builder.WriteLine($"Candidates: ({this.Candidates.Length}) [ {string.Join(", ", this.Candidates.Select(c => $"'{c}'"))} ]");
			}

			if (this.ExactMatch is not null)
			{
				builder.WriteLine($"Match: '{this.ExactMatch}' (exact)");
			}
			if (this.CommonPrefix is not null)
			{
				builder.WriteLine($"Prefix: '{this.CommonPrefix}'");
			}
			builder.ExplainChild(this.Render, "Render: ");
		}

	}

	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct PromptTokenStack : IReadOnlyList<PromptToken>
	{
		// in most of the cases, only the last token is changed from one prompt state to the other,
		// so if we had an immutable array with all the tokens, we would need to copy it every time.
		// It is faster to split it into two parts:
		// - the array with the "completed" tokens (that only changes when a new token is completed)
		// - the last token (that changes on every keystroke)
		//

		public static readonly PromptTokenStack Empty = new([], PromptToken.Empty);

		private readonly ImmutableArray<PromptToken> Head;

		public readonly PromptToken Last;

		public static PromptTokenStack Create(ReadOnlySpan<PromptToken> tokens) => tokens.Length switch
		{
			0 => PromptTokenStack.Empty,
			1 => new([], tokens[0]),
			_ => new([..tokens[..^1]], tokens[^1])
		};

		public static PromptTokenStack Create(IEnumerable<PromptToken> tokens)
		{
			return Create(tokens.TryGetSpan(out var span) ? span : new ReadOnlySpan<PromptToken>(tokens.ToArray()));
		}

		public PromptTokenStack(ImmutableArray<PromptToken> head, PromptToken last)
		{
			this.Head = head;
			this.Last = last;
		}

		public int Count => this.Head.Length + 1;

		public PromptToken this[int index]
		{
			[Pure]
			get
			{
				var headCount = this.Head.Length;
				if ((uint) index < (uint) headCount)
				{
					return this.Head[index];
				}

				if (index == headCount)
				{
					return this.Last;
				}

				throw new IndexOutOfRangeException();
			}
		}

		[MustUseReturnValue]
		public PromptTokenStack Push(PromptToken token)
		{
			if (this.Head.Length > 0)
			{
				return new([ ..this.Head, token ], PromptToken.Empty);
			}
			else
			{
				return new([ token ], PromptToken.Empty);
			}
		}

		/// <summary>Replace the last token of the list</summary>
		[MustUseReturnValue]
		public PromptTokenStack Update(PromptToken last) => new(this.Head, last);

		/// <summary>Replace the last token of the list</summary>
		[MustUseReturnValue]
		public PromptTokenStack Update(string type, string literal) => new(this.Head, PromptToken.Create(type, literal));

		public string GetRawText()
		{
			var writer = new ValueStringWriter();
			foreach (var token in this.Head)
			{
				if (writer.Count != 0) writer.Write(' ');
				writer.Write(token.Text);
			}
			if (writer.Count != 0) writer.Write(' ');
			writer.Write(this.Last.Text);
			return writer.ToStringAndDispose();
		}

		public override string ToString()
		{
			var writer = new ValueStringWriter();
			writer.Write('[');
			foreach (var token in this.Head)
			{
				writer.Write(writer.Count > 1 ? ", " : " ");
				writer.Write(token.ToString());
			}
			writer.Write(writer.Count > 1 ? ", " : " ");
			writer.Write(this.Last.ToString());
			writer.Write(" ]");
			return writer.ToStringAndDispose();
		}

		public PromptToken[] ToArray() => [..this.Head, this.Last];

		public PromptToken[] ToList() => [..this.Head, this.Last];

		public Enumerator GetEnumerator() => new(this.Head, this.Last);

		IEnumerator<PromptToken> IEnumerable<PromptToken>.GetEnumerator() => this.GetEnumerator();

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();

		public struct Enumerator : IEnumerator<PromptToken>
		{
			private int Index;
			private readonly ImmutableArray<PromptToken> Head;
			private readonly PromptToken Last;

			public Enumerator(ImmutableArray<PromptToken> head, PromptToken last)
			{
				this.Index = -1;
				this.Head = head;
				this.Last = last;
			}

			public bool MoveNext()
			{
				int index = this.Index + 1;
				if ((uint) index <= (uint) this.Head.Length)
				{
					this.Index = index;
					return true;
				}
				return false;
			}

			public void Reset() => this.Index = -1;

			public void Dispose() => this.Index = -2;

			public PromptToken Current
			{
				get
				{
					int index = this.Index;
					if ((uint) index < (uint) this.Head.Length)
					{
						return this.Head[index];
					}

					if (index == this.Head.Length)
					{
						return this.Last;
					}
					return default;
				}
			}

			object System.Collections.IEnumerator.Current => this.Current;

		}

	}

}
