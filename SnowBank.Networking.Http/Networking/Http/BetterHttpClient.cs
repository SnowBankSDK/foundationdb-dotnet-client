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

namespace SnowBank.Networking.Http
{

	/// <summary><see cref="HttpClient"/>, but with a Better<i>*</i> API</summary>
	/// <remarks>
	/// <para>This is a thin, empty subclass of <see cref="HttpClient"/> created by <see cref="IBetterHttpClientFactory"/> over a POOLED handler chain (built with <c>disposeHandler: false</c>): creating and disposing instances is cheap, and disposing a client NEVER tears down the shared handler chain or its sockets.</para>
	/// <para>The request pipeline - the IoC callback lifecycle (<c>SendAsync(request, ctx =&gt; ..., ct)</c>), the request builders (<c>CreateGetRequest</c>, ...) and the typed helpers - lives in the <see cref="BetterHttpRequestExtensions"/> extension methods on <see cref="HttpClient"/>. Add <c>using SnowBank.Networking.Http;</c> to bring them into scope.</para>
	/// </remarks>
	[PublicAPI]
	[Obsolete("The SendAsync extensions work on any HttpClient obtained from the factory doors; a dedicated shell type is no longer needed.", error: false)]
	public sealed class BetterHttpClient : HttpClient
	{

		/// <summary>Creates a new client over a pooled handler chain.</summary>
		/// <param name="handler">Pooled handler chain that owns the sockets. It is NOT disposed when this client is disposed.</param>
		internal BetterHttpClient(HttpMessageHandler handler)
			: base(handler, disposeHandler: false)
		{ }

	}

}
