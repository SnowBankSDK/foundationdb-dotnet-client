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
	using System.Runtime.CompilerServices;

	/// <summary>Represents a single token in a prompt.</summary>
	/// <remarks>
	/// <para>A token correspond to a single "word" in a prompt, separated by a spaces.</para>
	/// <para>
	/// A token can be subdivided into one or multiple fragments.
	/// If a token has a uniform color, it will be composed of a single fragment.
	/// If a token uses multiple colors, it will split into has many fragments as required.
	/// </para>
	/// </remarks>
	/// <seealso cref="PromptMarkupFragment"/>
	[PublicAPI]
	public readonly record struct PromptToken
	{
		public static readonly PromptToken Empty = new ("", "", []);

		public readonly string Type; //TODO!!

		public readonly string Text;

		public readonly ImmutableArray<PromptMarkupFragment> Markup;

		/// <summary>Create an undecorated token</summary>
		/// <param name="type">Type of the token</param>
		/// <param name="text">Raw text</param>
		/// <returns>Token without any markup details (will be completed at a later stage)</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static PromptToken Create(string? type, string? text) => new (type ?? "", text ?? "", []);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static PromptToken Create(string? type, PromptMarkupFragment fragment) => new(type ?? "", fragment.Literal, [fragment]);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static PromptToken Create(string? type, ImmutableArray<PromptMarkupFragment> fragments) => new (type ?? "", GetRawText(fragments.AsSpan()), fragments);

		internal static string GetRawText(ReadOnlySpan<PromptMarkupFragment> fragments)
		{
			var sw = new ValueStringWriter();
			foreach (var frag in fragments)
			{
				sw.Write(frag.Literal);
			}
			return sw.ToStringAndDispose();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static PromptToken Create(string? type, string? text, string? foreground, string? background = null) => new(type ?? "", text ?? "", [ PromptMarkupFragment.Create(text, foreground, background) ]);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public PromptToken(string type, string text, ImmutableArray<PromptMarkupFragment> markup)
		{
			Contract.Debug.Requires(type != null && text != null);
			this.Type = type;
			this.Text = text;
			this.Markup = markup;
		}

		public int Length => this.Text?.Length ?? 0;

		public bool HasMarkup => this.Markup.Length > 0;

		public override string ToString() => !string.IsNullOrEmpty(this.Type) ? $"<{this.Type}:'{this.Text}'>" : "<empty>";

		public override int GetHashCode() => HashCode.Combine(this.Type ?? "", this.Text ?? "");

		public bool Equals(PromptToken other) => this.Type.AsSpan().SequenceEqual(other.Type) && this.Text.AsSpan().SequenceEqual(other.Text);

		public PromptToken WithMarkup(string literal, string? foreground = null, string? background = null)
		{
			return new(this.Type, this.Text, [ PromptMarkupFragment.Create(literal, foreground, background) ]);
		}
	}

}
