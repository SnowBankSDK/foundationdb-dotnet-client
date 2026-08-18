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
	using System.Runtime.CompilerServices;

	/// <summary>Per-client runtime that the factory attaches to a <see cref="BetterHttpClient"/>, and that the request pipeline reads back at send time.</summary>
	/// <remarks>Holds the resolved policy bundle (options), the clock, the service provider and the client identity - everything the <see cref="BetterHttpRequestExtensions">send extensions</see> need to build the per-request context. This lives OFF the client (see <see cref="BetterHttpClientRuntime"/>) so the client type itself stays an empty shell.</remarks>
	internal sealed record BetterHttpClientRuntimeInfo
	{

		/// <summary>Resolved options for the policy bundle this client belongs to (filters, hooks, credentials, ...)</summary>
		public required BetterHttpClientOptions Options { get; init; }

		/// <summary>Clock used to measure the timestamps of the requests sent by this client</summary>
		public required IClock Clock { get; init; }

		/// <summary>Service provider used to resolve dependencies for the filters of this client</summary>
		public required IServiceProvider Services { get; init; }

		/// <summary>Unique id of this client, used as a prefix for per-request correlation ids</summary>
		public required string Id { get; init; }

	}

	/// <summary>Associates the per-client <see cref="BetterHttpClientRuntimeInfo"/> with a <see cref="BetterHttpClient"/> instance, without adding any state to the client type itself.</summary>
	/// <remarks>The factory <see cref="Attach"/>es the runtime right after building a client; the send extensions <see cref="Resolve"/> it at the start of every request. A <see cref="ConditionalWeakTable{TKey,TValue}"/> keeps the client an empty shell (no instance fields) while still letting the pipeline find its bundle.</remarks>
	internal static class BetterHttpClientRuntime
	{

		private static readonly ConditionalWeakTable<HttpClient, BetterHttpClientRuntimeInfo> Table = new();

		/// <summary>Attaches the runtime to a client. Called by the factory right after the client is built.</summary>
		public static void Attach(HttpClient client, BetterHttpClientRuntimeInfo info)
		{
			Contract.Debug.Requires(client is not null && info is not null);
			Table.AddOrUpdate(client, info);
		}

		/// <summary>Resolves the runtime attached to a client by the factory, if there is one.</summary>
		/// <remarks>A client without a runtime is not an error anymore: the send extensions start such a request with placeholder options, and the in-chain <see cref="BetterHttpPipelineHandler"/> fills in the name's resolved options when the request traverses the pooled chain.</remarks>
		public static bool TryResolve(HttpClient client, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out BetterHttpClientRuntimeInfo info)
		{
			Contract.Debug.Requires(client is not null);
			return Table.TryGetValue(client, out info);
		}

		/// <summary>Minimal empty <see cref="IServiceProvider"/> used while a request's context has not yet been filled by the pipeline.</summary>
		internal sealed class EmptyServiceProvider : IServiceProvider
		{
			public static readonly EmptyServiceProvider Instance = new();

			public object? GetService(Type serviceType) => null;
		}

	}

}
