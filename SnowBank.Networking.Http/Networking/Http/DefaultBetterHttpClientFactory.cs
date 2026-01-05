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
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Logging.Abstractions;
	using Microsoft.Extensions.Options;

	/// <summary>Factory for creating <see cref="BetterHttpClient"/> instances with the global defaults</summary>
	public class DefaultBetterHttpClientFactory : IBetterHttpClientFactory
	{

		private INetworkMap? Map { get; }

		private BetterHttpClientOptionsBuilder Builder { get; }

		private ILogger<BetterHttpClient> Logger { get; }

		private NodaTime.IClock Clock { get; }

		private IServiceProvider Services { get; }

		public DefaultBetterHttpClientFactory(INetworkMap? map, IOptions<BetterHttpClientOptionsBuilder> optionsBuilder, ILogger<BetterHttpClient>? logger, NodaTime.IClock? clock, IServiceProvider services)
		{
			this.Map = map;
			this.Builder = optionsBuilder.Value;
			this.Logger = logger ?? NullLogger<BetterHttpClient>.Instance;
			this.Clock = clock ?? NodaTime.SystemClock.Instance;
			this.Services = services;
		}

		/// <inheritdoc />
		public HttpMessageHandler CreateHttpHandler(Uri hostAddress, BetterHttpClientOptions options)
		{
			options.Filters.AddRange(this.Builder.GlobalFilters);
			options.Handlers.AddRange(this.Builder.GlobalHandlers);
			this.Builder.Configure?.Invoke(options);

			if (this.Map == null) throw new InvalidOperationException($"You must register an implementation for {nameof(INetworkMap)} during startup, in order to use this method.");
			var handler = this.Map.CreateBetterHttpHandler(hostAddress, options);

			// add our own delegating handler that will be able to hook into the request lifecycle
			handler = new BetterHttpClient.MagicalHandler(handler);

			// add any optional wrappers on top of that
			if (options.Handlers.Count > 0)
			{
				foreach (var factory in options.Handlers)
				{
					handler = factory(handler, this.Services);
				}
			}

			return handler;
		}

		/// <inheritdoc />
		public BetterHttpClient CreateClient(Uri hostAddress, BetterHttpClientOptions options, HttpMessageHandler? handler = null)
		{
			Contract.NotNull(hostAddress);
			Contract.NotNull(options);

			options.Filters.AddRange(this.Builder.GlobalFilters);
			options.Handlers.AddRange(this.Builder.GlobalHandlers);
			this.Builder.Configure?.Invoke(options);

			if (handler == null)
			{
				if (this.Map == null) throw new InvalidOperationException($"You must register an implementation for {nameof(INetworkMap)} during startup, in order to use this method.");
				handler = this.Map.CreateBetterHttpHandler(hostAddress, options);
				Contract.Debug.Assert(handler != null);
			}

			return new BetterHttpClient(
				hostAddress,
				options,
				handler,
				this.Logger,
				this.Clock,
				this.Services
			);
		}

	}

}
