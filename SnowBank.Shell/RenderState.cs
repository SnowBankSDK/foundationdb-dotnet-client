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

	/// <summary>Represent the state of what should be rendered</summary>
	public sealed record RenderState : ICanExplain
	{

		public required string PromptRaw { get; init; }

		/// <summary>User Input (without any markup)</summary>
		public required string TextRaw { get; init; }

		public required int Extra { get; init; }

		/// <summary>Prompt decorated with markup code (not part of the user input)</summary>
		public required string PromptMarkup { get; init; }

		/// <summary>List of completed tokens in the prompt</summary>
		/// <remarks>
		/// <para>Does not include the token currently being edited!</para>
		/// </remarks>
		public required PromptTokenStack Tokens { get; init; }

		/// <summary>User Input decorated with markup code</summary>
		public required string TextMarkup { get; init; }

		/// <summary>Position of the cursor (relative to the start of the user input)</summary>
		public required int Cursor { get; init; }

		public required ImmutableArray<(string Raw, string Markup)> Rows { get; init; }

		public void Explain(ExplanationBuilder builder)
		{
			builder.WriteLine($"Raw:    '{this.PromptRaw}{this.TextRaw}'");
			builder.WriteLine($"Markup: '{this.PromptMarkup}' '{this.TextMarkup}'");
			builder.WriteLine($"Tokens: {this.Tokens}");
		}

	}

}
