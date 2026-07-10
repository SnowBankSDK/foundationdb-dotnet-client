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

	/// <summary>Per-shell (per-client) options: what a call site may legitimately customize for ONE transient <see cref="BetterHttpClient"/> shell, without touching the shared pooled transport.</summary>
	/// <remarks>
	/// <para>This is the per-call surface of the options model. Wire policy - TLS/certificates, proxy, cookies, redirects, decompression, credentials, filters and pipeline handlers - is a property of the POLICY BUNDLE, registered once at startup with <c>AddBetterHttpClient(name, ...)</c>; it cannot be set per call (the pooled chain is shared).</para>
	/// <para>The typical use is per-connection default headers (e.g. authentication headers on a transient client): <c>factory.CreateClient(uri, new BetterHttpShellOptions { ... })</c>.</para>
	/// </remarks>
	[PublicAPI]
	public sealed record BetterHttpShellOptions
	{

		/// <summary>Default headers applied to this shell's requests (on top of the bundle's default headers)</summary>
		public BetterDefaultHeaders DefaultRequestHeaders { get; set; } = new();

		/// <summary>Overrides the bundle's default initial HTTP version for this shell's requests, when set</summary>
		public Version? DefaultRequestVersion { get; set; }

		/// <summary>Overrides the bundle's policy for selecting the HTTP version of a request, when set</summary>
		public HttpVersionPolicy? DefaultVersionPolicy { get; set; }

		/// <summary>Custom options added to the <see cref="HttpRequestMessage.Options"/> of this shell's requests (appended after the bundle's own)</summary>
		public List<KeyValuePair<string, object?>>? RequestOptions { get; set; }

		/// <summary>Overrides the bundle's hooks for this shell, when set</summary>
		/// <remarks>Mostly used for unit testing or low-level debugging</remarks>
		public IBetterHttpHooks? Hooks { get; set; }

		/// <summary>Per-request-only credentials for this shell (e.g. a message signer stamping a different identity per transient client)</summary>
		/// <remarks>The credentials must declare <see cref="IBetterCredentials.IsPerRequestOnly"/>: a transport-coupled credential configures the shared pooled handler and belongs to the policy bundle - attaching one here throws.</remarks>
		public IBetterCredentials? Credentials { get; set; }

	}

}
