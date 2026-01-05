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
	using System.Runtime.CompilerServices;

	/// <summary>Represents a fragment of a token in a prompt.</summary>
	/// <remarks>
	/// <para>A fragment correspond to a span of contiguous letters that have the same colors.</para>
	/// </remarks>
	/// <seealso cref="PromptToken"/>
	[PublicAPI]
	public readonly record struct PromptMarkupFragment
	{

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static PromptMarkupFragment Create(string? literal, string? foreground = null, string? background = null)
			=> new(literal ?? "", !string.IsNullOrEmpty(foreground) ? foreground : null, !string.IsNullOrEmpty(background) ? background : null);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private PromptMarkupFragment(string literal, string? foreground = null, string? background = null)
		{
			this.Literal = literal;
			this.Foreground = foreground;
			this.Background = background;
		}

		public readonly string Literal;

		public readonly string? Foreground;

		public readonly string? Background;

		public override string ToString() => this.Literal.ToString();

		public bool HasColor => !string.IsNullOrEmpty(this.Foreground) || !string.IsNullOrEmpty(this.Background);

		public bool Equals(PromptMarkupFragment other) => other.Literal == this.Literal && other.Foreground == this.Foreground && other.Background == this.Background;

		public override int GetHashCode() => HashCode.Combine(this.Literal, this.Foreground, this.Background);

	}

}
