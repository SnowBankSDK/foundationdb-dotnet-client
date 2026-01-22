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
	using Microsoft.Extensions.DependencyInjection.Extensions;

	/// <summary>Helper for building <see cref="BetterHttpClientOptions"/></summary>
	public record BetterHttpClientOptionsBuilder
	{

		public Action<BetterHttpClientOptions>? Configure { get; set; }

		/// <summary>List of global filters that will be applied to all requests performed by the client</summary>
		public List<IBetterHttpFilter> GlobalFilters { get; set; } = [ ];

		/// <summary>List of global handlers that will be called to configure the HTTP Handlers of all requests performed by the client</summary>
		public List<Func<HttpMessageHandler, BetterHttpClientOptions, IServiceProvider, HttpMessageHandler>> GlobalHandlers { get; set; } = [ ];

	}

	/// <summary>Extensions methods for working with <see cref="BetterHttpClient"/> and other related types.</summary>
	[PublicAPI]
	public static class BetterHttpClientExtensions
	{

		/// <summary>Adds support for <see cref="IBetterHttpClientFactory"/> and configures the global HTTP options</summary>
		/// <remarks>This gives access to the <see cref="IBetterHttpClientFactory"/> singleton.</remarks>
		public static IServiceCollection AddBetterHttpClient(this IServiceCollection services, Action<BetterHttpClientOptions>? configure = null)
		{
			services.TryAddSingleton<IBetterHttpClientFactory, DefaultBetterHttpClientFactory>();
			services
				.AddOptions<BetterHttpClientOptionsBuilder>()
				.Configure(options =>
				{
					if (configure != null)
					{
						options.Configure += configure;
					}
				});
			return services;
		}

		/// <summary>Adds a global <see cref="IBetterHttpFilter">HTTP filter</see> to all clients used by this process</summary>
		/// <typeparam name="TFilter">Type of the <see cref="IBetterHttpFilter"/> implementation</typeparam>
		/// <remarks>The filter will be added to the <see cref="BetterHttpClientOptionsBuilder.GlobalFilters"/> of the default option builder</remarks>
		public static IServiceCollection AddGlobalHttpFilter<TFilter>(this IServiceCollection services, Action<TFilter>? configure = null)
			where TFilter: class, IBetterHttpFilter
		{
#if DEBUG
			if (services.Any(x => x.ServiceType == typeof(TFilter))) throw new InvalidOperationException($"Global HTTP filter '{typeof(TFilter).Name}' has already been registered!");
#endif

			services.TryAddSingleton<TFilter>();
			services
				.AddOptions<BetterHttpClientOptionsBuilder>()
				.Configure<IServiceProvider>((options, sp) =>
				{
					var filter = sp.GetRequiredService<TFilter>();
					configure?.Invoke(filter);
					options.GlobalFilters.Add(filter);
				});
			return services;
		}

		/// <summary>Adds a global HTTP message handler filter to all clients used by this process</summary>
		/// <remarks>The handler will be added to the pipeline, and called whenever a new <see cref="HttpMessageHandler"/> is prepared, before executing a request.</remarks>
		public static IServiceCollection AddGlobalHttpHandler(this IServiceCollection services, Func<HttpMessageHandler, BetterHttpClientOptions, IServiceProvider, HttpMessageHandler> factory)
		{
			services
				.AddOptions<BetterHttpClientOptionsBuilder>()
				.Configure<IServiceProvider>((options, _) =>
				{
					options.GlobalHandlers.Add(factory);
				});
			return services;
		}

		/// <summary>Adds support for a specific <see cref="IBetterHttpProtocol">HTTP protocol handler</see></summary>
		/// <typeparam name="TFactory">Type of the protocol handler factory</typeparam>
		/// <typeparam name="TProtocol">Type of the protocol handler</typeparam>
		/// <typeparam name="TOptions">Type of the options supported by the protocol handler</typeparam>
		/// <remarks>This should be called by implementors of protocols, via a dedicated extension method.</remarks>
		public static IServiceCollection AddBetterHttpProtocol<TFactory, TProtocol, TOptions>(this IServiceCollection services, Action<TOptions>? configure = null)
			where TFactory : class, IBetterHttpProtocolFactory<TProtocol, TOptions>
			where TProtocol : IBetterHttpProtocol
			where TOptions : BetterHttpClientOptions
		{
			services.TryAddSingleton<TFactory>();
			services.Configure<TOptions>(configure ?? (_ => { }));
			return services;
		}

		/// <summary>Creates a new client for use with a specific <see cref="IBetterHttpProtocol"/></summary>
		/// <typeparam name="TProtocol">Type of the protocol handler</typeparam>
		/// <typeparam name="TOptions">Type of the options supported by the protocol handler</typeparam>
		/// <param name="factory">Protocol factory that will create the new client.</param>
		/// <param name="baseAddress">Host name, or IP address of the remote target</param>
		/// <param name="configure">Optional callback used to further configure the client.</param>
		/// <returns>Client that will send requests to the remote host at <see cref="baseAddress"/>, using the specified protocol.</returns>
		public static TProtocol CreateClient<TProtocol, TOptions>(this IBetterHttpProtocolFactory<TProtocol, TOptions> factory, string baseAddress, Action<TOptions>? configure = null)
			where TProtocol : IBetterHttpProtocol
			where TOptions : BetterHttpClientOptions
		{
			return factory.CreateClient(new Uri(baseAddress, UriKind.Absolute), configure);
		}

	}

}
