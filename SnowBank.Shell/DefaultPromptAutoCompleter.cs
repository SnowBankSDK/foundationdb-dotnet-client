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
	public class DefaultPromptAutoCompleter : IPromptAutoCompleter
	{

		public required RootPromptCommand Root { get; init; }

		/// <summary>Updates a <see cref="PromptState"/> with up-to-date auto-complete context</summary>
		public PromptState HandleAutoComplete(PromptState state)
		{
			if (state.IsDone())
			{ // nothing to do anymore
				return state with
				{
					Candidates = [ ],
					ExactMatch = null,
					CommonPrefix = null,
				};
			}

			var text = state.RawText;
			var token = state.Tokens.Last.Text;

			//if (commandToken == null && state.TokenStart > 0)
			//{ // we just validated the command
			//	commandToken = text[..state.TokenStart].Trim();
			//	command = this.Root.Commands.FirstOrDefault(c => c.Token == commandToken);
			//	// may not exist!
			//}

			// get matching tokens
			string? exactMatch = null;
			string? commonPrefix = null;
			var candidates = new List<string>();

			string? smallest = null;

			state.Command.GetTokens(candidates, text, token);

			foreach (var candidate in candidates)
			{
				if (candidate == token)
				{
					exactMatch = candidate;
				}

				if (smallest == null)
				{
					smallest = candidate;
				}
				else if (candidate.Length < smallest.Length)
				{
					smallest = candidate;
				}
			}

			if (candidates.Count > 0 && exactMatch is null && smallest is not null)
			{
				// find a common prefix to all candidates!
				//HACKHACK: better algo ?
				int commonLetters = 0;
				for(int i = 0; i < smallest.Length; i++)
				{
					var l = candidates[0][i];
					bool match = true;
					for(int j = 1; j < candidates.Count; j++)
					{
						if (candidates[j][i] != l)
						{ // nope!
							match = false;
							break;
						}
					}

					if (!match)
					{
						break;
					}
					++commonLetters;
				}

				commonPrefix = commonLetters == 0 ? null : candidates[0][..commonLetters];
			}

			return state with
			{
				Candidates = candidates.ToArray(),
				ExactMatch = exactMatch,
				CommonPrefix = commonPrefix,
			};
		}

	}

}
