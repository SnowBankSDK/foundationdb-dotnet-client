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
	using Spectre.Console;

	public class AnsiConsolePromptTheme : IPromptTheme
	{

		/// <summary>The prompt, decorated with markup, (part before the user input)</summary>
		public required string Prompt { get; init; }

		public required int MaxRows { get; init; } = 5;

		public string? DefaultForeground { get; init; }

		public string? DefaultBackground { get; init; }

		/// <inheritdoc />
		public virtual PromptState Paint(PromptState state)
		{
			// we normaly only need to update the last token, IF it has changed
			var tokens = state.Tokens;
			var last = tokens.Last;
			if (!last.HasMarkup)
			{
				switch (last.Type)
				{
					case "command":
					{
						last = last.WithMarkup(last.Text, "darkcyan");
						break;
					}
					case "fql":
					{
						last = last.WithMarkup(last.Text, "cyan");
						break;
					}
					case "argument":
					{
						last = last.WithMarkup(last.Text, "yellow");
						break;
					}
					default:
					{
						last = last.WithMarkup(last.Text, null);
						break;
					}
				}
				tokens = state.Tokens.Update(last);
			}

			// for each of the token, use its type to generate the decorated markup fragments
			// - some tokens will use a single fragment (ex: a command name, a literal value, ...)
			// - others will be split into fragments with different colors, such as a query, a path, etc...

			var promptMarkup = "[gray]" + this.Prompt + "[/]";
			var text = state.RawText;
			var token = state.RawToken;
			var candidates = state.Candidates;
			var rows = new List<(string Raw, string Markup)>();

			// if we have multiple candidates, first jump ahead, write them, and then go back up!
			if (candidates.Length > 0)
			{
				for (var i = 0; i < candidates.Length; i++)
				{
					if (i >= this.MaxRows) break;

					var c = candidates[i];
					if (c == state.ExactMatch)
					{
						rows.Add((c, $"[bold cyan]{c}[/]"));
					}
					else if (state.CommonPrefix == null || state.CommonPrefix.Length == token.Length)
					{
						rows.Add((c, $"[cyan]{c[..token.Length]}[/][gray]{c[token.Length..]}[/]"));
					}
					else
					{
						rows.Add((c, $"[cyan]{c[..token.Length]}[/][darkcyan]{state.CommonPrefix[token.Length..]}[/][gray]{c[state.CommonPrefix.Length..]}[/]"));
					}

				}
			}

			string markup = "";
			if (state.Tokens.Count > 1)
			{
				markup += (!state.CommandBuilder.IsValid() ? "[red]" : "[silver]") + text[..^state.RawToken.Length] + "[/]";
			}

			int extra = 0;

			if (state.RawToken.Length > 0)
			{
				// if we have an exact match => green
				// if we have a single incomplete match => cyan
				// if we have multiple incomplete matches => yellow
				// otherwise, white
				string color =
					state.ExactMatch != null ? "[green]"
					: candidates.Length == 1 ? "[cyan]"
					: candidates.Length != 0 ? "[yellow]"
					: "[white]";

				markup += color + text[^state.RawToken.Length..] + "[/]";

				if (candidates.Length == 1)
				{ // we have a candidate, write a "ghost" with the remainder (TAB will auto-complete)
					var suffix = candidates[0][token.Length..];
					markup = markup + "[darkcyan]" + suffix + "[/]";
					extra = suffix.Length;
				}
			}

			return state with
			{
				Render = new()
				{
					PromptRaw = this.Prompt,
					TextRaw = text,
					PromptMarkup = promptMarkup,
					TextMarkup = markup,
					Tokens = tokens,
					Extra = extra,
					Cursor = text.Length,
					Rows = [..rows],
				},
			};
		}

	}

}
