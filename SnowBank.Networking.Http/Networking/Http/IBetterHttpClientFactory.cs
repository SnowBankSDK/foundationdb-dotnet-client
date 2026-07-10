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

	/// <summary>Type that can create <see cref="BetterHttpClient"/> instances over pooled handler chains</summary>
	/// <remarks>
	/// <para>A client is a thin, transient shell over a POOLED handler chain: creating and disposing them is cheap, and disposing one never tears down the shared sockets. A registered NAME is a policy bundle (TLS, filters, pipeline) - never an origin; the call site provides the absolute target URI at send time.</para>
	/// </remarks>
	public interface IBetterHttpClientFactory
	{

		/// <summary>Creates a new client over the default policy bundle.</summary>
		/// <returns>A transient shell over the pooled default chain. The call site provides the absolute target URI on each request.</returns>
		BetterHttpClient CreateClient();

		/// <summary>Creates a new client over the named policy bundle.</summary>
		/// <param name="name">Name of the policy bundle registered with <c>AddBetterHttpClient(name, ...)</c></param>
		/// <returns>A transient shell over the pooled chain for that bundle. The call site provides the absolute target URI on each request.</returns>
		BetterHttpClient CreateClient(string name);

		/// <summary>Creates a new client bound to a base address, over a policy bundle.</summary>
		/// <param name="baseAddress">Base address applied to the transient shell (relative request paths resolve against it). Never touches the pooled chain.</param>
		/// <param name="name">Optional name of the policy bundle; the default bundle is used when <c>null</c>.</param>
		/// <returns>A transient shell whose <see cref="HttpClient.BaseAddress"/> is set to <paramref name="baseAddress"/>.</returns>
		BetterHttpClient CreateClient(Uri baseAddress, string? name = null);

		/// <summary>Creates a new client bound to a base address, with per-shell options, over a policy bundle.</summary>
		/// <param name="baseAddress">Base address applied to the transient shell (relative request paths resolve against it). Never touches the pooled chain.</param>
		/// <param name="shell">Per-shell options (default headers, request version, hooks, request options) applied to THIS client only. Wire policy stays on the bundle.</param>
		/// <param name="name">Optional name of the policy bundle; the default bundle is used when <c>null</c>.</param>
		/// <returns>A transient shell carrying the per-shell options over the bundle's pooled chain.</returns>
		BetterHttpClient CreateClient(Uri baseAddress, BetterHttpShellOptions shell, string? name = null);

		/// <summary>Creates a new <see cref="HttpMessageHandler"/> that can be used to connect to the specified host</summary>
		/// <param name="hostAddress">Host name or IP address of the remote target</param>
		/// <param name="options">Custom options used to customize the handler</param>
		/// <returns>Configured handler that will connect to the specified host</returns>
		[Obsolete("Register a named policy bundle with AddBetterHttpClient(name, ...) and resolve the pooled chain via IHttpMessageHandlerFactory.CreateHandler(name) instead.")]
		HttpMessageHandler CreateHttpHandler(Uri hostAddress, BetterHttpClientOptions options);

		/// <summary>Creates a new <see cref="BetterHttpClient"/> that can be used to send requests to the specified host</summary>
		/// <param name="hostAddress">Host name or IP address of the remote target</param>
		/// <param name="options">Custom options used to customize the client</param>
		/// <param name="handler">HTTP handler that should be used. If <c>null</c>, a new handler will be created and configured automatically.</param>
		/// <returns>Configured client that will send requests to the specified host</returns>
		[Obsolete("Register a named policy bundle with AddBetterHttpClient(name, ...) and use CreateClient(name)/CreateClient(uri) instead.")]
		BetterHttpClient CreateClient(Uri hostAddress, BetterHttpClientOptions options, HttpMessageHandler? handler = null);

	}

}
