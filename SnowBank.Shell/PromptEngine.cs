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

	public interface IPromptInput
	{

		/// <summary>Attempts to read the next key, if one is available</summary>
		/// <param name="key">When the method returns <see langword="true"/>, receives the next key in the buffer, or <see langword="null"/> if the input buffer has completed</param>
		/// <returns><see langword="true"/> if a key was present in the buffer; or <see langword="false"/> if there are no keys available yet.</returns>
		bool TryReadKey(out ConsoleKeyInfo? key);

		/// <summary>Reads the next key from the buffer</summary>
		/// <param name="ct">Token used to cancel the read</param>
		/// <returns>Task that will return either the next key in the buffer, or <see langword="null"/> if the input buffer has completed.</returns>
		Task<ConsoleKeyInfo?> ReadKey(CancellationToken ct);

	}

	public class DefaultConsoleInput : IPromptInput
	{

		public bool TryReadKey(out ConsoleKeyInfo? key)
		{
			if (Console.KeyAvailable)
			{
				key = Console.ReadKey(intercept: true);
				return true;
			}

			key = default;
			return false;
		}

		public async Task<ConsoleKeyInfo?> ReadKey(CancellationToken ct)
		{
			//HACKHACK: TODO: is there a better way to do a cancellable key read, without doing polling, or spinning a new thread everytime ??

			while (true)
			{
				ct.ThrowIfCancellationRequested();

				// Read the next key
				if (!Console.KeyAvailable)
				{
					await Task.Delay(15, ct);
					continue;
				}

				var key = Console.ReadKey(intercept: true);
				return key;
			}
		}

	}

	public class PromptEngine
	{

		public required IPromptInput Input { get; init; }

		public required IPromptKeyHandler KeyHandler { get; init; }

		public required IPromptTheme Theme { get; init; }

		public required IPromptRenderer Renderer { get; init; }

		public required IPromptAutoCompleter AutoCompleter { get; init; }

		public PromptState? State { get; private set; }

		// HOOKS

		public Action<PromptState>? OnBefore { get; init; }

		public Action<ConsoleKeyInfo, PromptState>? OnAfter { get; init; }

		public Func<PromptState, ConsoleKeyInfo, PromptState>? OnUserInput { get; init; }

		public Func<PromptState, PromptState>? OnCommandUpdate { get; init; }

		public Func<PromptState, RenderState?, PromptState>? OnBeforeRender { get; init; }

		public async Task<PromptState> Prompt(CancellationToken ct)
		{
			// this is the complete text, (used to render the prompt up to the cursor)

			// this contains the last "token" that is currently being typed,
			// example: "hello, th_" => the token is "th"

			var root = this.AutoCompleter.Root;

			var state = PromptState.CreateEmpty(root, this.Theme);

			state = this.Theme.Paint(state);

			// initial render
			this.Renderer.Render(state, null);

			// record the new state
			this.State = state;

			// capture the current render state (used by the render to patch the screen, instead of performing a full repaint)
			var lastRenderState = state.Render;

			while (!ct.IsCancellationRequested && !state.IsDone())
			{

				this.OnBefore?.Invoke(state);

				// wait for the next user input...
				var keyOrStop = await this.Input.ReadKey(ct);

				// no more input? => consider this is the same as the "ENTER" key
				var key = keyOrStop ?? new ConsoleKeyInfo('\n', ConsoleKey.Enter, false, false, false);

				if (this.OnUserInput != null)
				{
					state = this.OnUserInput(state, key);
					Contract.Debug.Assert(state != null);
				}

				// update the state with this key
				var newState = this.KeyHandler.HandleKeyPress(state, key);

				if (!ReferenceEquals(state, newState) && !ReferenceEquals(newState, state.Parent))
				{
					newState = newState.CommandBuilder.Update(newState);
					newState = this.AutoCompleter.HandleAutoComplete(newState);

					if (this.OnCommandUpdate != null)
					{
						state = this.OnCommandUpdate(state);
						Contract.Debug.Assert(state != null);
					}
				}

				// repaint the state
				state = this.Theme.Paint(newState);

				if (this.OnBeforeRender != null)
				{
					state = this.OnBeforeRender(state, lastRenderState);
					Contract.Debug.Assert(state != null);
				}

				// if the changed produced actual UI change, we need to render it
				this.Renderer.Render(state, this.State?.Render);

				this.OnAfter?.Invoke(key, state);

				lastRenderState = state.Render;
				this.State = state;
			}

			return state;
		}

	}

}
