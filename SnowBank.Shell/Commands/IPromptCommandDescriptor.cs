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

	/// <summary>Base interface for command builders</summary>
	[PublicAPI]
	public interface IPromptCommandDescriptor
	{

		/// <summary>Token for the command</summary>
		/// <remarks>Ex: <c>"set"</c>, <c>"get"</c>, <c>"cd"</c>, <c>"query"</c>, <c>"quit"</c>, <c>"help"</c>, ...</remarks>
		string Token { get; }

		/// <summary>One line description shown in the help list</summary>
		/// <remarks>Ex: <c>"Sets the value of a key"</c>, <c>"Change the current directory"</c>, <c>"Display help for a command"</c></remarks>
		string Description { get; }

		/// <summary>Hint shown during auto-complete</summary>
		/// <remarks>Ex: <c>"set &lt;key&gt; = &lt;value&gt;"</c>, <c>"cd &lt;path&gt;"</c>, <c>"help &lt;command&gt;"</c></remarks>
		string SyntaxHint { get; }

		/// <summary>Called to create a new context for this command</summary>
		/// <remarks>Called when the name of the command has been typed, and updated when the user continues typing arguments or options</remarks>
		IPromptCommandBuilder StartNew();

		/// <summary>Lists all possible candidates for auto-completion</summary>
		/// <param name="candidates">List where candidates should be added</param>
		/// <param name="command">Current complete command</param>
		/// <param name="tok">Current token being typed (can be empty)</param>
		void GetTokens(List<string> candidates, string command, string tok);

	}

}
