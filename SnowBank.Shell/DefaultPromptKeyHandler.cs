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

	public class DefaultPromptKeyHandler : IPromptKeyHandler
	{

		/// <inheritdoc />
		public PromptState HandleKeyPress(PromptState state, ConsoleKeyInfo key)
		{
			switch (key.Key)
			{
				case ConsoleKey.Enter:
				{
					return state with
					{
						Change = PromptChange.Done,
						LastKey = '\n',
						Parent = null,
						RawText = state.RawText.Trim(),
					};
				}
				case ConsoleKey.Escape:
				{
					return state with
					{
						Change = PromptChange.Aborted,
						LastKey = '\e',
						Parent = null,
						RawText = "",
						RawToken = "",
						Tokens = PromptTokenStack.Empty,
					};
				}
				case ConsoleKey.Tab:
				{
					var candidates = state.Candidates;
					if (candidates.Length == 0)
					{ // nothing to complete?
						return state;
					}

					if (state.ExactMatch != null)
					{ // we already have written the word, only add a space!

						if (state.Change == PromptChange.Completed)
						{ // double "TAB" after an auto-complete, equivalent to a space
							//TODO: implemented looping through all the possible candidates
							return state;
						}

						if (string.IsNullOrEmpty(state.RawText))
						{
							return state;
						}

						state = state with
						{
							Change = PromptChange.Completed,
							LastKey = '\t',
							Parent = state,
							RawText = state.RawText,
						};
						break;
					}

					if (candidates.Length == 1)
					{ // we have only one candidate, output it
						var c = candidates[0];
						var text = state.Tokens.Count == 1 ? c : (state.RawText[..^state.RawToken.Length] + c);
						state = state with
						{
							Change = PromptChange.Completed,
							LastKey = '\t',
							Parent = state,
							RawText = text,
							RawToken = c,
						};
						break;
					}

					if (state.CommonPrefix != null)
					{ // we have a common prefix with all candidates, we can jump forward
						var partial = state.CommonPrefix[state.Tokens.Last.Length..];
						var text = state.RawText + partial;
						state = state with
						{
							Change = PromptChange.Completed,
							LastKey = '\t',
							Parent = state,
							RawText = text,
							RawToken = partial,
						};
						break;
					}

					return state;
				}
				case ConsoleKey.Backspace:
				{
					if (state.Parent == null)
					{ // nothing to delete
						return state;
					}

					return state.Parent;
				}
				default:
				{
					char c = key.KeyChar;

					var rawText = state.RawText;
					var rawToken = state.RawToken;
					// simply add the character

					rawText += c;
					rawToken += c;

					state = state with
					{
						Change = PromptChange.Add,
						LastKey = c,
						Parent = state,
						RawText = rawText,
						RawToken = rawToken,
					};

					break;
				}
			}

			return state;
		}
		
	}

}
