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

		private INetworkMap? Map { get; }

		private NodaTime.IClock Clock { get; }

		private IServiceProvider Services { get; }

		public DefaultBetterHttpClientFactory(IHttpMessageHandlerFactory handlerFactory, INetworkMap? map, NodaTime.IClock? clock, IServiceProvider services)
		{
			this.HandlerFactory = handlerFactory;
			this.Map = map;
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

		private BetterHttpClient CreateClientCore(string name, Uri? baseAddress)
		{
			// pooled handler chain (transport + pipeline), owned and rotated by the platform
			var handler = this.HandlerFactory.CreateHandler(name);

			var options = BetterHttpClientExtensions.ResolveBundleOptions(this.Services, name);

			var client = new BetterHttpClient(handler);
			if (baseAddress is not null)
			{
				client.BaseAddress = baseAddress;
			}
			client.DefaultRequestVersion = options.DefaultRequestVersion;
			client.DefaultVersionPolicy = options.DefaultVersionPolicy;
			options.DefaultRequestHeaders.Apply(client.DefaultRequestHeaders);

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

		/// <inheritdoc />
		[Obsolete("Register a named policy bundle with AddBetterHttpClient(name, ...) and resolve the pooled chain via IHttpMessageHandlerFactory.CreateHandler(name) instead.")]
		public HttpMessageHandler CreateHttpHandler(Uri hostAddress, BetterHttpClientOptions options)
		{
			Contract.NotNull(hostAddress);
			Contract.NotNull(options);

			ApplyGlobals(options);

			if (this.Map == null) throw ErrorNoNetworkMap();
			var handler = this.Map.CreateTransportHandler(options);

			return options.WrapHandler(handler, this.Services);
		}

		/// <inheritdoc />
		[Obsolete("Register a named policy bundle with AddBetterHttpClient(name, ...) and use CreateClient(name)/CreateClient(uri) instead.")]
		public BetterHttpClient CreateClient(Uri hostAddress, BetterHttpClientOptions options, HttpMessageHandler? handler = null)
		{
			Contract.NotNull(hostAddress);
			Contract.NotNull(options);

			ApplyGlobals(options);

			if (handler == null)
			{
				if (this.Map == null) throw ErrorNoNetworkMap();
				handler = this.Map.CreateTransportHandler(options);
			}

			// build the full one-shot pipeline over the provided transport (transport wrappers + MagicalHandler)
			var wrapped = options.WrapHandler(handler, this.Services);
			var client = new BetterHttpClient(wrapped)
			{
				BaseAddress = hostAddress,
				DefaultRequestVersion = options.DefaultRequestVersion,
				DefaultVersionPolicy = options.DefaultVersionPolicy,
			};
			options.DefaultRequestHeaders.Apply(client.DefaultRequestHeaders);

			BetterHttpClientRuntime.Attach(client, new BetterHttpClientRuntimeInfo()
			{
				Options = options,
				Clock = this.Clock,
				Services = this.Services,
				Id = CorrelationIdGenerator.GetNextId(),
			});

			return client;
		}

		/// <summary>Merges the process-wide global filters/handlers and the global configure into the caller-provided options (used by the transitional per-call creation path).</summary>
		private void ApplyGlobals(BetterHttpClientOptions options)
		{
			var builder = this.Services.GetService<Microsoft.Extensions.Options.IOptions<BetterHttpClientOptionsBuilder>>()?.Value;
			if (builder is null) return;
			options.Filters.AddRange(builder.GlobalFilters);
			options.Handlers.AddRange(builder.GlobalHandlers);
			builder.Configure?.Invoke(options);
		}

		private static InvalidOperationException ErrorNoNetworkMap()
			=> new($"You must register an implementation for {nameof(INetworkMap)} during startup, in order to use this method.");

	}

}
