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
	[PublicAPI]
	public interface IPromptRenderer
	{

		/// <summary>Generate the markup for or more tokens</summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="tokens">Builder lists all the tokens in the prompt</param>
		/// <remarks>Writes the token literals, decorated with markup, to the destination buffer</remarks>
		void ToMarkup(ref ValueStringWriter destination, PromptTokenStack tokens, IPromptTheme theme);

		/// <summary>Render the prompt to the screen</summary>
		/// <param name="state">New prompt state (with up to date <see cref="PromptState.Render"/> data)</param>
		/// <param name="prev"><see cref="RenderState"/> of the previous state, or <see langword="null"/> on the first render</param>
		/// <remarks>Note that <paramref name="prev"/> may be different from the render state of the <see cref="PromptState.Parent"/> of <paramref name="state"/>, when rolling back a change (backspace, escape, ...)</remarks>
		void Render(PromptState state, RenderState? prev);

	}

}
