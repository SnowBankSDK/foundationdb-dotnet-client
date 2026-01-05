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
	using System.Diagnostics.CodeAnalysis;

	public class RootPromptCommand : IPromptCommandDescriptor
	{

		public List<IPromptCommandDescriptor> Commands { get; init; } = [];

		string IPromptCommandDescriptor.Token => "";

		string IPromptCommandDescriptor.Description => "";

		string IPromptCommandDescriptor.SyntaxHint => "";

		public IPromptCommandBuilder StartNew()
		{
			return new Builder(this);
		}


		public void GetTokens(List<string> candidates, string command, string tok)
		{
			foreach (var cmd in this.Commands)
			{
				if (cmd.Token.StartsWith(tok))
				{
					candidates.Add(cmd.Token);
				}
			}
		}

		public sealed record Builder : IPromptCommandBuilder, ICanExplain
		{

			internal Builder(RootPromptCommand root)
			{
				this.Descriptor = root;
			}

			public string CommandName { get; init; } = "";

			public IPromptCommandDescriptor? Command { get; init; }

			Type IPromptCommandBuilder.GetCommandType() => null!;

			public override string ToString() => $"Root {{ Name = \"{this.CommandName}\" }}";

			public bool IsValid() => this.Command is not null;

			public bool TryGetCommand(string token, [MaybeNullWhen(false)] out IPromptCommandDescriptor command)
			{
				command = this.Descriptor.Commands.FirstOrDefault(x => x.Token == token);
				return command is not null;
			}

			public RootPromptCommand Descriptor { get; }

			public PromptState Update(PromptState state)
			{
				Contract.Debug.Requires(ReferenceEquals(state.CommandBuilder, this));

				// first we have to detect if we change to the next token, by looking for a space in the token:
				// - "hello_" we are still writing the token (maybe there are more to come)
				// - "hello _" we have written the command, we must switch to it

				if (state.Change == PromptChange.Done)
				{
					if (this.Command is not null)
					{ // we either have a command that does not have any argument or options, or the caller forgot to pass any!

						var builder = this.Command.StartNew();
						Contract.Debug.Assert(builder != null);
						state = state with
						{
							Change = PromptChange.Done,
							Command = this.Command,
							RawToken = "",
							Tokens = state.Tokens.Push(PromptToken.Create("command", state.RawToken.Trim())),
							CommandBuilder = builder,
						};

						return builder.Update(state);
					}
				}

				if (state.Tokens.Count > 1)
				{ // this is not supposed to happen, unless we are parsing an invalid command?
					throw new NotImplementedException();
				}

				if (state.RawToken.Length == 0)
				{ // we are empty, or became empty again!
					return state with
					{
						CommandBuilder = this with { CommandName = "", Command = null }
					};
				}

				if (state.RawToken[^1] == ' ')
				{ // we completed the command name

					if (this.Command is null)
					{ // no command with this name!
						return state with
						{
							Tokens = state.Tokens.Update("error", state.RawToken), // TODO: use color names? like "@invalid" or "@error" ?
							CommandBuilder = this with
							{
								CommandName = state.RawToken[..^1],
							}
						};
					}

					// switch to this command
					return state with
					{
						Change = PromptChange.NextToken,
						Command = this.Command,
						RawToken = "",
						Tokens = state.Tokens.Push(PromptToken.Create("command", state.RawToken.Trim())),
						CommandBuilder = this.Command.StartNew(),
					};
				}

				if (this.TryGetCommand(state.RawToken, out var cmd))
				{ // we typed a name that matches a command
					return state with
					{
						Tokens = state.Tokens.Update("command", cmd.Token),
						CommandBuilder = this with
						{
							CommandName = cmd.Token,
							Command = cmd,
						}
					};
				}

				// TODO: partial, vs unknown
				return state with
				{
					Tokens = state.Tokens.Update("incomplete", state.RawToken),
					CommandBuilder = this with
					{
						CommandName = state.RawToken,
						Command = null,
					}
				};

			}

			public IPromptCommand Build(PromptState state)
			{
				throw new NotSupportedException("No valid command was specified.");
			}

			public void Explain(ExplanationBuilder builder)
			{
				builder.WriteLine($"CommandName: '{this.CommandName}'");
				//TODO!
			}

		}

	}

}
