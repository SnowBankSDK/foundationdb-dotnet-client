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
	using Microsoft.Extensions.Http;
	using Microsoft.Extensions.Options;

	/// <summary>Helper for building <see cref="BetterHttpClientOptions"/></summary>
	public record BetterHttpClientOptionsBuilder
	{

		public Action<BetterHttpClientOptions>? Configure { get; set; }

		/// <summary>List of global filters that will be applied to all requests performed by the client</summary>
		public List<IBetterHttpFilter> GlobalFilters { get; set; } = [ ];

		/// <summary>List of global handlers that will be called to configure the HTTP Handlers of all requests performed by the client</summary>
		public List<Func<HttpMessageHandler, BetterHttpClientOptions, IServiceProvider, HttpMessageHandler>> GlobalHandlers { get; set; } = [ ];

		/// <summary>Per-name configuration callbacks for the registered policy bundles.</summary>
		public Dictionary<string, Action<BetterHttpClientOptions>> NamedConfigures { get; } = new(StringComparer.Ordinal);

	}

	/// <summary>Extensions methods for working with <see cref="BetterHttpClient"/> and other related types.</summary>
	[PublicAPI]
	public static class BetterHttpClientExtensions
	{

		/// <summary>Name of the default (dynamic / by-URI) policy bundle.</summary>
		/// <remarks>
		/// <para>We use an explicit named bundle (rather than <c>ConfigureHttpClientDefaults</c>) so the default chain is wired exactly like a named one - one <c>AddHttpClient</c> registration over the map's transport seam, with the pipeline handler on top.</para>
		/// <para>This is the name to pass to <see cref="System.Net.Http.IHttpMessageHandlerFactory.CreateHandler"/> to obtain a bare, pooled handler that carries the full default pipeline (packet capture included).</para>
		/// </remarks>
		public const string DefaultClientName = "SnowBank.Networking.Http.BetterHttpClient";

		/// <summary>Service key under which a higher layer can register an outer "capture" delegating handler that rides every pooled bundle chain.</summary>
		/// <remarks>
		/// <para>The <c>SnowBank.Networking.PacketCapture</c> layer registers its in-chain capture handler under this key (keyed + transient). When present, <see cref="WireBundle"/> inserts it as the outermost handler of the bundle (above the pipeline's <see cref="MagicalHandler"/>), so capture observes the entire request/response for any consumer of the pooled chain.</para>
		/// <para>This is a DI-key seam because <c>SnowBank.Networking.Http</c> must not depend on the packet-capture layer (the dependency runs the other way).</para>
		/// </remarks>
		public const string CaptureHandlerServiceKey = "SnowBank.Networking.Http.CaptureHandler";

		/// <summary>Retired: use <see cref="AddBetterHttpClientDefaults"/>, which routes every factory client through the network map, not just the default bundle.</summary>
		/// <remarks>The old overload wired only the default (dynamic) bundle, so a plain <c>AddHttpClient(...)</c> escaped the map. <see cref="AddBetterHttpClientDefaults"/> hooks every factory client (named, typed, or default) with no per-client enrollment.</remarks>
		[Obsolete("Use AddBetterHttpClientDefaults(configure): it routes EVERY factory client through the network map (a plain AddHttpClient too), not just the default bundle. This overload wired only the default bundle, so stock clients escaped the map (and the test sandbox).", error: true)]
		public static IServiceCollection AddBetterHttpClient(this IServiceCollection services, Action<BetterHttpClientOptions>? configure = null)
		{
			RegisterCore(services);
			if (configure != null)
			{
				services
					.AddOptions<BetterHttpClientOptionsBuilder>()
					.Configure(options => options.Configure += configure);
			}
			return services;
		}

		/// <summary>Routes every factory client through the network map and the standard pipeline, so a plain <see cref="System.Net.Http.IHttpClientFactory"/> client needs no per-client enrollment.</summary>
		/// <param name="services">Service collection</param>
		/// <param name="configure">Optional callback used to configure the global options that form the baseline for every client.</param>
		/// <remarks>
		/// <para>This is the recommended default registration. It installs a <c>ConfigureHttpClientDefaults</c> hook that wires the map's transport plus the standard pipeline onto every factory client (named, typed via <c>AddHttpClient&lt;TClient&gt;</c>, or the default), so a plain <c>services.AddHttpClient("weather")</c> is routed with no enrollment. Inside a distributed test this sandboxes every factory client by construction.</para>
		/// <para>The global <paramref name="configure"/> sets the baseline (transport, default headers, TLS trust, filters, credentials) for every client; a named bundle registered with <see cref="AddBetterHttpClient(IServiceCollection, string, Action{BetterHttpClientOptions})"/> overrides or extends that baseline for its own client.</para>
		/// <para>Reach for <see cref="AddBetterHttpClient(IServiceCollection, string, Action{BetterHttpClientOptions})"/> when a specific client needs its own certificates, credentials or filters, or the bare-handler-by-name seam.</para>
		/// </remarks>
		public static IServiceCollection AddBetterHttpClientDefaults(this IServiceCollection services, Action<BetterHttpClientOptions>? configure = null)
		{
			// The global hook owns the shared pipeline (the MagicalHandler + the outer capture) for the whole factory, so each
			// bundle contributes only its per-name primary + options (see WireBundle). Recording it before RegisterCore lets
			// WireBundle's build-time check read the final state whatever the registration order.
			var wired = GetWiredBundles(services);
			bool alreadyInstalled = wired.DefaultsHookInstalled;
			wired.DefaultsHookInstalled = true;

			RegisterCore(services);
			if (configure != null)
			{
				// each call composes its configure onto the global baseline, so repeated registration is safe
				services
					.AddOptions<BetterHttpClientOptionsBuilder>()
					.Configure(options => options.Configure += configure);
			}

			// install the shared hook once: a repeated call (a framework base plus an app, say) must not stack a second pipeline
			// handler + capture, which would run every request's filters/credentials twice (e.g. sign a request twice).
			if (alreadyInstalled) return services;

			services.ConfigureHttpClientDefaults(builder =>
			{
				// parity with WireBundle: the transport bounds DNS staleness by itself (PooledConnectionLifetime on the shared
				// SocketsHttpHandler), so periodic chain rotation buys nothing and pays socket-pool cold starts.
				builder.SetHandlerLifetime(Timeout.InfiniteTimeSpan);

				// primary = the map's transport (sockets in prod, the virtual network in tests), built from the global options.
				// A named bundle's own ConfigurePrimaryHttpMessageHandler runs after this (per-name beats defaults) and reassigns
				// the primary with its per-name options, so a bundle keeps its own transport pipeline.
				builder.ConfigurePrimaryHttpMessageHandler((sp) =>
				{
					var map = sp.GetService<INetworkMap>() ?? throw new InvalidOperationException($"You must register an implementation for {nameof(INetworkMap)} during startup, in order to use {nameof(IBetterHttpClientFactory)}.");
					var options = ResolveBundleOptions(sp, DefaultClientName);
					var transport = map.CreateTransportHandler(options);
					return options.BuildTransportPipeline(transport, sp);
				});

				// the shared pipeline handler, plus the optional OUTER capture handler (above the pipeline), for every client.
				// Built once here for the whole factory, so a bundle must not add its own (see WireBundle's build-time skip).
				builder.ConfigureAdditionalHttpMessageHandlers((handlers, sp) =>
				{
					handlers.Add(new MagicalHandler());
					if (sp.GetKeyedService<DelegatingHandler>(CaptureHandlerServiceKey) is { } capture)
					{
						handlers.Insert(0, capture);
					}
				});
			});

			return services;
		}

		/// <summary>Adds a named HTTP policy bundle (TLS, filters, pipeline) and support for <see cref="IBetterHttpClientFactory"/></summary>
		/// <param name="services">Service collection</param>
		/// <param name="name">Name of the policy bundle. A registered name is a bundle of policies, not an origin: the call site provides the absolute target URI at run time.</param>
		/// <param name="configure">Optional callback used to configure the options for this bundle.</param>
		public static IServiceCollection AddBetterHttpClient(this IServiceCollection services, string name, Action<BetterHttpClientOptions>? configure = null)
		{
			Contract.NotNullOrWhiteSpace(name);
			if (string.Equals(name, DefaultClientName, StringComparison.Ordinal)) throw new ArgumentException("This name is reserved for the default policy bundle.", nameof(name));

			RegisterCore(services);
			if (configure != null)
			{
				services
					.AddOptions<BetterHttpClientOptionsBuilder>()
					.Configure(options =>
					{
						// compose repeated registrations for the same name (like the default overload's Configure +=), instead of
						// the previous last-wins: several call sites can contribute policies to one bundle.
						options.NamedConfigures[name] = options.NamedConfigures.TryGetValue(name, out var previous)
							? (opts => { previous(opts); configure(opts); })
							: configure;
					});
			}
			WireBundle(services, name);
			return services;
		}

		/// <summary>Ensures the factory, the options builder, the M.E.Http infrastructure and the default policy bundle are registered.</summary>
		private static void RegisterCore(IServiceCollection services)
		{
			services.TryAddSingleton<IBetterHttpClientFactory, DefaultBetterHttpClientFactory>();
			services.AddOptions<BetterHttpClientOptionsBuilder>();
			services.AddHttpClient(); // registers IHttpClientFactory / IHttpMessageHandlerFactory
			WireBundle(services, DefaultClientName);
		}

		/// <summary>Wires a named M.E.Http bundle: primary handler = the map's transport seam, plus the <see cref="MagicalHandler"/> pipeline on top. Idempotent per name.</summary>
		private static void WireBundle(IServiceCollection services, string name)
		{
			if (!GetWiredBundles(services).Names.Add(name)) return; // already wired

			services
				.AddHttpClient(name)
				// the transport bounds DNS staleness by itself (PooledConnectionLifetime, set by the map's CreateTransportHandler
				// on the shared SocketsHttpHandler): platform chain rotation stacked on top would pay socket-pool cold starts
				// against every active origin every ~2 minutes and buy nothing. A bundle that wants periodic chain rebuild
				// (e.g. to re-evaluate its configure callbacks) opts back in with the stock M.E.Http API, AFTER registration:
				//     services.AddHttpClient(name).SetHandlerLifetime(...)
				.SetHandlerLifetime(Timeout.InfiniteTimeSpan)
				.ConfigurePrimaryHttpMessageHandler((sp) =>
				{
					var map = sp.GetService<INetworkMap>() ?? throw new InvalidOperationException($"You must register an implementation for {nameof(INetworkMap)} during startup, in order to use {nameof(IBetterHttpClientFactory)}.");
					var options = ResolveBundleOptions(sp, name);
					// raw transport (sockets in prod, virtual network in tests), plus the transport-level wrappers (credentials, custom handlers, filter wrappers)
					var transport = map.CreateTransportHandler(options);
					return options.BuildTransportPipeline(transport, sp);
				});

			services.Configure<HttpClientFactoryOptions>(name, options =>
			{
				options.HttpMessageHandlerBuilderActions.Add(builder =>
				{
					// When AddBetterHttpClientDefaults installed the global hook, it owns the shared pipeline handler and the
					// outer capture handler for EVERY factory client; a bundle adding its own would run the pipeline (and record
					// every request) twice. The check is at build time (builder.Services is the built container), so it is
					// order-independent: it does not matter whether this bundle was registered before or after the defaults hook.
					if (IsDefaultsHookInstalled(builder.Services)) return;

					// no defaults hook: this bundle owns its full pipeline. The MagicalHandler runs the bundle's filters/hooks.
					builder.AdditionalHandlers.Add(new MagicalHandler());

					// If a higher layer registered an outer capture handler (e.g. packet capture) under CaptureHandlerServiceKey,
					// insert it as the outermost handler of this bundle - above the MagicalHandler - so capture rides the whole
					// pipeline (a bare handler obtained from IHttpMessageHandlerFactory is captured too, not just BetterHttpClient
					// sends). Resolved per chain build, so it rotates with the pooled chain; Insert(0) means no extra handler when
					// capture is absent.
					if (builder.Services.GetKeyedService<DelegatingHandler>(CaptureHandlerServiceKey) is { } capture)
					{
						builder.AdditionalHandlers.Insert(0, capture);
					}
				});

				// HttpClientActions run only for clients built by the plain IHttpClientFactory.CreateClient(name) - the
				// supported doors (IBetterHttpClientFactory shells, IHttpMessageHandlerFactory.CreateHandler) never hit
				// them. A plain HttpClient over a bundle's chain has no BetterHttp runtime: the bundle's filters, hooks
				// and credentials never run at the request stage, so e.g. an auth-signing bundle would silently not
				// sign. Fail the wrong door loudly instead.
				options.HttpClientActions.Add(_ => throw new InvalidOperationException(
					$"'{name}' is a BetterHttpClient policy bundle: create clients through {nameof(IBetterHttpClientFactory)} (typed shells carrying the bundle's filters, hooks and credentials), or draw a bare pooled handler from IHttpMessageHandlerFactory.CreateHandler(name). A plain HttpClient from IHttpClientFactory would silently skip the bundle's request pipeline (e.g. authentication signing)."));
			});
		}

		/// <summary>Resolves the effective <see cref="BetterHttpClientOptions"/> for a policy bundle: global filters/handlers, the global configure, then the per-name configure. A fresh instance is returned each call (so nothing is mutated across calls).</summary>
		/// <remarks>
		/// <para>Consumers may use this to INSPECT a bundle's effective policy (e.g. whether it carries a custom certificate-validation callback); mutating the returned instance has no effect on the bundle.</para>
		/// <para>Caveat: the pooled pipeline (this method, when the primary handler is stitched) and the client runtime (the send extensions) resolve two separate options instances for the same bundle. Filters therefore must keep per-request state in <c>context.State</c> - never in instance fields that try to coordinate their <c>Wrap</c> with their stage callbacks, since the two runs see different option objects.</para>
		/// </remarks>
		public static BetterHttpClientOptions ResolveBundleOptions(IServiceProvider services, string name)
		{
			var builder = services.GetRequiredService<IOptions<BetterHttpClientOptionsBuilder>>().Value;

			var options = new BetterHttpClientOptions();
			options.Filters.AddRange(builder.GlobalFilters);
			options.Handlers.AddRange(builder.GlobalHandlers);
			builder.Configure?.Invoke(options);

			if (!string.Equals(name, DefaultClientName, StringComparison.Ordinal) && builder.NamedConfigures.TryGetValue(name, out var configure))
			{
				configure(options);
			}

			return options;
		}

		/// <summary>Registration-time state shared across the <c>AddBetterHttpClient*</c> calls: the set of wired bundle names, and whether the global defaults hook was installed. Registered as a singleton instance so it is also readable at chain-build time.</summary>
		private static WiredBundles GetWiredBundles(IServiceCollection services)
		{
			var existing = (WiredBundles?) services.FirstOrDefault(d => d.ServiceType == typeof(WiredBundles))?.ImplementationInstance;
			if (existing is null)
			{
				existing = new WiredBundles();
				services.AddSingleton(existing);
			}
			return existing;
		}

		/// <summary>Reads, at chain-build time, whether <see cref="AddBetterHttpClientDefaults"/> installed the global hook (which then owns the shared pipeline handler + capture for every factory client).</summary>
		private static bool IsDefaultsHookInstalled(IServiceProvider services)
			=> services.GetService<WiredBundles>()?.DefaultsHookInstalled ?? false;

		private sealed class WiredBundles
		{
			/// <summary>Names of the policy bundles already wired, so repeated <c>AddBetterHttpClient</c> calls do not stack pipeline handlers.</summary>
			public HashSet<string> Names { get; } = new(StringComparer.Ordinal);

			/// <summary>True once <see cref="AddBetterHttpClientDefaults"/> installed the global hook; then that hook owns the shared <see cref="MagicalHandler"/> + capture for the whole factory, and each bundle skips its own copy.</summary>
			public bool DefaultsHookInstalled { get; set; }
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
		/// <returns>Client that will send requests to the remote host at <paramref name="baseAddress"/>, using the specified protocol.</returns>
		public static TProtocol CreateClient<TProtocol, TOptions>(this IBetterHttpProtocolFactory<TProtocol, TOptions> factory, string baseAddress, Action<TOptions>? configure = null)
			where TProtocol : IBetterHttpProtocol
			where TOptions : BetterHttpClientOptions
		{
			return factory.CreateClient(new Uri(baseAddress, UriKind.Absolute), configure);
		}

	}

}
