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
	using JetBrains.Annotations;

	/// <summary>Generates the <see cref="RenderState"/> that represents a current prompt state, using a specific theme or layout</summary>
	/// <remarks>This is similar to generating a "virtual dom" that is not rendered yet, but only describes what should be visible</remarks>
	[PublicAPI]
	public interface IPromptTheme
	{

		/// <summary>Default foreground color for all tokens</summary>
		/// <remarks>
		/// <para>Any text fragment that does not define a foreground color will use this color</para>
		/// <para>If <see langword="null"/>, then the default shell color will be used.</para>
		/// </remarks>
		string? DefaultForeground { get; }

		/// <summary>Default background color for all tokens</summary>
		/// <remarks>
		/// <para>Any text fragment that does not define a background color will use this color</para>
		/// <para>If <see langword="null"/>, then the default shell color will be used.</para>
		/// </remarks>
		string? DefaultBackground { get; }

		/// <summary>Generates the <see cref="RenderState"/> that corresponds to a <see cref="PromptState"/></summary>
		PromptState Paint(PromptState state);

	}

}
