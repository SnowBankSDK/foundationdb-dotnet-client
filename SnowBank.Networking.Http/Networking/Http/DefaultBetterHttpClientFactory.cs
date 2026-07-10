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
	using Microsoft.Extensions.DependencyInjection;

	/// <summary>Factory that hands out <see cref="BetterHttpClient"/> shells over pooled handler chains sourced from <see cref="IHttpMessageHandlerFactory"/>.</summary>
	public class DefaultBetterHttpClientFactory : IBetterHttpClientFactory
	{

		private IHttpMessageHandlerFactory HandlerFactory { get; }

		private NodaTime.IClock Clock { get; }

		private IServiceProvider Services { get; }

		public DefaultBetterHttpClientFactory(IHttpMessageHandlerFactory handlerFactory, NodaTime.IClock? clock, IServiceProvider services)
		{
			this.HandlerFactory = handlerFactory;
			this.Clock = clock ?? NodaTime.SystemClock.Instance;
			this.Services = services;
		}

		/// <inheritdoc />
		public BetterHttpClient CreateClient()
			=> CreateClientCore(BetterHttpClientExtensions.DefaultClientName, baseAddress: null);

		/// <inheritdoc />
		public BetterHttpClient CreateClient(string name)
		{
			Contract.NotNullOrWhiteSpace(name);
			return CreateClientCore(name, baseAddress: null);
		}

		/// <inheritdoc />
		public BetterHttpClient CreateClient(Uri baseAddress, string? name = null)
		{
			Contract.NotNull(baseAddress);
			return CreateClientCore(name ?? BetterHttpClientExtensions.DefaultClientName, baseAddress);
		}

		/// <inheritdoc />
		public BetterHttpClient CreateClient(Uri baseAddress, BetterHttpShellOptions shell, string? name = null)
		{
			Contract.NotNull(baseAddress);
			Contract.NotNull(shell);
			return CreateClientCore(name ?? BetterHttpClientExtensions.DefaultClientName, baseAddress, shell);
		}

		private BetterHttpClient CreateClientCore(string name, Uri? baseAddress, BetterHttpShellOptions? shell = null)
		{
			// pooled handler chain (transport + pipeline), owned and rotated by the platform
			var handler = this.HandlerFactory.CreateHandler(name);

			var options = BetterHttpClientExtensions.ResolveBundleOptions(this.Services, name);

			if (shell is not null)
			{ // overlay the per-shell tier onto the freshly-resolved (per-client, mutation-safe) bundle options
				if (shell.DefaultRequestVersion is not null) options.DefaultRequestVersion = shell.DefaultRequestVersion;
				if (shell.DefaultVersionPolicy is not null) options.DefaultVersionPolicy = shell.DefaultVersionPolicy.Value;
				if (shell.Hooks is not null) options.Hooks = shell.Hooks;
				if (shell.RequestOptions is not null) (options.Options ??= [ ]).AddRange(shell.RequestOptions);
				if (shell.Credentials is { } credentials)
				{ // only the per-request half can act for a shell: a transport-coupled credential would silently skip its configure half
					if (!credentials.IsPerRequestOnly)
					{
						throw new InvalidOperationException($"Per-shell credentials must be per-request only: '{credentials.GetType().Name}' requires transport configuration, which belongs to a policy bundle registered at startup with AddBetterHttpClient(name, ...).");
					}
					options.Credentials = credentials;
				}
			}

			var client = new BetterHttpClient(handler);
			if (baseAddress is not null)
			{
				client.BaseAddress = baseAddress;
			}
			client.DefaultRequestVersion = options.DefaultRequestVersion;
			client.DefaultVersionPolicy = options.DefaultVersionPolicy;
			options.DefaultRequestHeaders.Apply(client.DefaultRequestHeaders);
			shell?.DefaultRequestHeaders.Apply(client.DefaultRequestHeaders); // the shell's headers ride on top of the bundle's

			// attach the runtime that the send extensions read back at request time
			BetterHttpClientRuntime.Attach(client, new BetterHttpClientRuntimeInfo()
			{
				Options = options,
				Clock = this.Clock,
				Services = this.Services,
				Id = CorrelationIdGenerator.GetNextId(),
			});

			return client;
		}

	}

}
