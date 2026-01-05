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
	using Spectre.Console;
	using Console = System.Console;

	public class AnsiConsolePromptRenderer : IPromptRenderer
	{

		private static readonly string[] CachedClearMasks = GenerateClearMasks();

		private static string[] GenerateClearMasks()
		{
			var res = new string[10];
			for (int i = 0; i < res.Length; i++)
			{
				res[i] = new string(' ', i);
			}
			return res;
		}

		private string GetClearMask(int count) => (uint) count < AnsiConsolePromptRenderer.CachedClearMasks.Length ? AnsiConsolePromptRenderer.CachedClearMasks[count] : new string(' ', count);

		public void Render(PromptState state, RenderState? prev)
		{
			Contract.Debug.Requires(state != null && state.Render != null);

			// hide the cursor while we are moving around during repaint
			Console.CursorVisible = false;

			var render = state.Render!;

			int skippedRows = 0;

			#region Main Prompt Line:

			// render the prompt
			Console.CursorLeft = 0;
			AnsiConsole.Markup(render.PromptMarkup);

			// render the user input
			int cursorStart = Console.CursorLeft;
			AnsiConsole.Markup(render.TextMarkup);

			int removed = prev == null ? 0 : (prev.TextRaw.Length + prev.Extra) - (render.TextRaw.Length + render.Extra);
			if (removed > 0)
			{
				Console.Write(this.GetClearMask(removed));
			}

			#endregion

			#region Extra Rows...

			//TODO: clear any difference!

			for (int i = 0; i < render.Rows.Length; i++)
			{
				var row = render.Rows[i];
				Console.WriteLine();
				++skippedRows;
				AnsiConsole.Markup(row.Markup);
				removed = prev == null || i >= prev.Rows.Length ? 0 : prev.Rows[i].Raw.Length - row.Raw.Length;
				if (removed > 0)
				{
					Console.Write(this.GetClearMask(removed));
				}
			}

			// clear out any rows from the previous state that don't exist anymore
			if (prev != null)
			{
				for (int i = render.Rows.Length; i < prev.Rows.Length; i++)
				{
					if (prev.Rows[i].Raw.Length > 0)
					{
						Console.WriteLine();
						++skippedRows;
						Console.Write(this.GetClearMask(prev.Rows[i].Raw.Length));
					}
				}
			}

			#endregion

			// now go back up to the main prompt line
			if (skippedRows > 0)
			{
				Console.CursorTop -= skippedRows;
			}

			// and set the cursor to the expected position
			Console.CursorLeft = cursorStart + render.Cursor;
			Console.CursorVisible = true;

		}


		public static string EscapeMarkup(string literal)
		{
			// note: SpectreConsole only provide an extension method for strings, not ReadOnlySpans!
			// => it only encode [ into [[ and ] into ]] so we just replicate it here
			if (literal.AsSpan().ContainsAny('[', ']'))
			{
				return EscapeMarkupSlow(literal);
			}

			return literal;

			[MethodImpl(MethodImplOptions.NoInlining)]
			static string EscapeMarkupSlow(string literal) => literal
				.Replace("[", "[[", StringComparison.Ordinal)
				.Replace("]", "]]", StringComparison.Ordinal);
		}

		public static void WriteEscaped(ref ValueStringWriter destination, ReadOnlySpan<char> literal)
		{
			if (literal.ContainsAny('[', ']'))
			{
				WriteEscapedSlow(ref destination, literal);
			}
			else
			{
				destination.Write(literal);
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			static void WriteEscapedSlow(ref ValueStringWriter destination, ReadOnlySpan<char> literal)
			{
				foreach (var c in literal)
				{
					switch (c)
					{
						case '[':
						{
							destination.Write("[[");
							break;
						}
						case ']':
						{
							destination.Write("]]");
							break;
						}
						default:
						{
							destination.Write(c);
							break;
						}
					}
				}
			}
		}

		private static void BeginColor(ref ValueStringWriter output, ReadOnlySpan<char> foreground, ReadOnlySpan<char> background)
		{
			if (foreground.Length != 0)
			{
				if (background.Length == 0)
				{ // "[FGND]"
					output.Write('[');
					output.Write(foreground);
					output.Write(']');
				}
				else
				{ // "[FGND on BGND]"
					output.Write('[');
					output.Write(foreground);
					output.Write(" on ");
					output.Write(background);
					output.Write(']');
				}
			}
			else if (background.Length == 0)
			{ // "[on BGND]"
				output.Write("[ on");
				output.Write(background);
				output.Write(']');
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void EndColor(ref ValueStringWriter output)
		{
			output.Write("[/]");
		}

		private static void Write(ref ValueStringWriter output, ReadOnlySpan<char> literal, ReadOnlySpan<char> foreground, ReadOnlySpan<char> background)
		{
			if (foreground.Length == 0 && background.Length == 0)
			{
				WriteEscaped(ref output, literal);
			}
			else
			{
				BeginColor(ref output, foreground, background);
				WriteEscaped(ref output, literal);
				EndColor(ref output);
			}
		}

		public void ToMarkup(ref ValueStringWriter destination, PromptTokenStack tokens, IPromptTheme theme)
		{
			var foreground = theme.DefaultForeground ?? "";
			var background = theme.DefaultBackground ?? "";
			bool hasDefaultColors = !string.IsNullOrEmpty(foreground) || !string.IsNullOrEmpty(background);

			if (hasDefaultColors)
			{
				BeginColor(ref destination, foreground, background);
			}

			for(int i = 0; i < tokens.Count; i++)
			{
				if (i > 0)
				{
					destination.Write(' ');
				}
				foreach (var frag in tokens[i].Markup)
				{
					var f = frag.Foreground ?? "";
					if (f == foreground) f = null;
					var b = frag.Background ?? "";
					if (b == background) b = null;

					Write(ref destination, frag.Literal, f, b);
				}
			}

			if (hasDefaultColors)
			{
				EndColor(ref destination);
			}
		}

	}

}
