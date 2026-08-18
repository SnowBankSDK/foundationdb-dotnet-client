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
	using System.Globalization;
	using Microsoft.Extensions.Configuration;
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

		/// <summary>Per-name configuration callbacks for the registered clients.</summary>
		public Dictionary<string, Action<BetterHttpClientOptions>> NamedConfigures { get; } = new(StringComparer.Ordinal);

		/// <summary>Configuration sections registered by <c>AddBetterHttpClientConfiguration</c>, applied on top of the code layers (the section's <c>Defaults</c> first, then <c>Clients:&lt;name&gt;</c>).</summary>
		public List<IConfiguration> ConfigurationSections { get; } = [ ];

	}

	/// <summary>Extensions methods for working with <see cref="BetterHttpClient"/> and other related types.</summary>
	[PublicAPI]
	public static class BetterHttpClientExtensions
	{

		/// <summary>Name of the default (dynamic / by-URI) client.</summary>
		/// <remarks>
		/// <para>This is the name used when no explicit client name is given (e.g. <see cref="IBetterHttpClientFactory.CreateClient()"/>), and the name to pass to <see cref="System.Net.Http.IHttpMessageHandlerFactory.CreateHandler"/> to obtain a bare, pooled handler that carries the default pipeline (packet capture included).</para>
		/// </remarks>
		public const string DefaultClientName = "SnowBank.Networking.Http.BetterHttpClient";

		/// <summary>Service key under which a higher layer can register an outer "capture" delegating handler that rides every pooled chain.</summary>
		/// <remarks>
		/// <para>The <c>SnowBank.Networking.PacketCapture</c> layer registers its in-chain capture handler under this key (keyed + transient). When present, the chain setup inserts it as the outermost handler of every client (above the <see cref="BetterHttpPipelineHandler"/> and any application handler), so capture observes the entire request/response for any consumer of the pooled chain.</para>
		/// <para>This is a DI-key seam because <c>SnowBank.Networking.Http</c> must not depend on the packet-capture layer (the dependency runs the other way).</para>
		/// </remarks>
		public const string CaptureHandlerServiceKey = "SnowBank.Networking.Http.CaptureHandler";

		/// <summary>Retired: use <see cref="AddBetterHttpClientDefaults"/>, which routes every factory client through the network map, not just the default client.</summary>
		/// <remarks>The old overload wired only the default (dynamic) client, so a plain <c>AddHttpClient(...)</c> escaped the map. <see cref="AddBetterHttpClientDefaults"/> hooks every factory client (named, typed, or default) with no per-client enrollment.</remarks>
		[Obsolete("Use AddBetterHttpClientDefaults(configure): it routes EVERY factory client through the network map (a plain AddHttpClient too), not just the default client. This overload wired only the default client, so stock clients escaped the map (and the test sandbox).", error: true)]
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
		/// <para>This is the recommended default registration. It wires the map's transport plus the standard pipeline onto every factory client (named, typed via <c>AddHttpClient&lt;TClient&gt;</c>, keyed via <c>AddAsKeyed()</c>, or the default), so a plain <c>services.AddHttpClient("weather")</c> is routed with no enrollment. Inside a distributed test this sandboxes every factory client by construction.</para>
		/// <para>The global <paramref name="configure"/> sets the baseline (transport, default headers, TLS trust, timeout) for every client; a per-name registration made with <see cref="AddBetterHttpClient(IServiceCollection, string, Action{BetterHttpClientOptions})"/> overrides or extends that baseline for its own client.</para>
		/// </remarks>
		public static IServiceCollection AddBetterHttpClientDefaults(this IServiceCollection services, Action<BetterHttpClientOptions>? configure = null)
		{
			RegisterCore(services);
			if (configure != null)
			{
				// each call composes its configure onto the global baseline, so repeated registration is safe
				services
					.AddOptions<BetterHttpClientOptionsBuilder>()
					.Configure(options => options.Configure += configure);
			}
			return services;
		}

		/// <summary>Registers the configuration override layer: the given section can override the BetterHttp options of any client, without a rebuild.</summary>
		/// <param name="services">Service collection</param>
		/// <param name="configuration">Configuration root (or section) that carries the override section.</param>
		/// <param name="sectionName">Name of the override section (defaults to <c>"BetterHttp"</c>).</param>
		/// <remarks>
		/// <para>The section is a pure override: when it is absent the code-configured behavior runs unchanged. Its <c>Defaults</c> sub-section overrides the global baseline for every client, and <c>Clients:&lt;name&gt;</c> overrides one named client, both applied after the code layers (configuration always has the last word).</para>
		/// <para>A knob can carry the value <c>"inherit"</c> to cancel every override below the global layers, so the effective value falls back to the code-global baseline (for a knob in <c>Clients:&lt;name&gt;</c>, this also cancels the client's own code configure).</para>
		/// <para>Only the operation-safe subset binds from configuration: <c>Timeout</c>, <c>AllowAutoRedirect</c>, <c>AutomaticDecompression</c>, and <c>Tls:Mode</c> (<c>System</c>, <c>AcceptSelfSigned</c>, <c>AcceptAny</c>). Credentials, filters, handlers and callbacks are code-only by construction.</para>
		/// <para>Repeated calls compose: each registered section is applied in registration order.</para>
		/// </remarks>
		public static IServiceCollection AddBetterHttpClientConfiguration(this IServiceCollection services, IConfiguration configuration, string sectionName = "BetterHttp")
		{
			Contract.NotNull(configuration);
			Contract.NotNullOrWhiteSpace(sectionName);
			RegisterCore(services);
			services
				.AddOptions<BetterHttpClientOptionsBuilder>()
				.Configure(options => options.ConfigurationSections.Add(configuration.GetSection(sectionName)));
			return services;
		}

		/// <summary>Registers a named HTTP client with its own BetterHttp policy (TLS, credentials, timeout), on top of the global defaults.</summary>
		/// <param name="services">Service collection</param>
		/// <param name="name">Name of the client. A name carries policy, not an origin: the call site provides the absolute target URI at run time.</param>
		/// <param name="configure">Optional callback used to configure the options for this client, applied on top of the global defaults.</param>
		/// <returns>The native <see cref="IHttpClientBuilder"/> for this name, so the standard registration APIs (<c>AddHttpMessageHandler</c>, <c>AddAsKeyed</c>, <c>ConfigureHttpClient</c>, typed clients) chain on.</returns>
		/// <remarks>
		/// <para>This is exactly <c>services.AddHttpClient(name)</c> plus the per-name options layer: the client is a regular factory client, reachable through every door (<see cref="System.Net.Http.IHttpClientFactory"/>, a typed or keyed client, <see cref="System.Net.Http.IHttpMessageHandlerFactory.CreateHandler"/>, or <see cref="IBetterHttpClientFactory"/>), and every door carries the same policy.</para>
		/// <para>A client with no BetterHttp-specific policy does not need this method: a plain <c>AddHttpClient(name)</c> is already fully enrolled by <see cref="AddBetterHttpClientDefaults"/>.</para>
		/// </remarks>
		public static IHttpClientBuilder AddBetterHttpClient(this IServiceCollection services, string name, Action<BetterHttpClientOptions>? configure = null)
		{
			Contract.NotNullOrWhiteSpace(name);
			if (string.Equals(name, DefaultClientName, StringComparison.Ordinal)) throw new ArgumentException("This name is reserved for the default client.", nameof(name));

			RegisterCore(services);
			if (configure != null)
			{
				services
					.AddOptions<BetterHttpClientOptionsBuilder>()
					.Configure(options =>
					{
						// compose repeated registrations for the same name (like the default overload's Configure +=), instead of
						// the previous last-wins: several call sites can contribute policies to one client.
						options.NamedConfigures[name] = options.NamedConfigures.TryGetValue(name, out var previous)
							? (opts => { previous(opts); configure(opts); })
							: configure;
					});
			}
			return services.AddHttpClient(name);
		}

		/// <summary>Ensures the factory, the options builder, the M.E.Http infrastructure and the per-name chain setup are registered.</summary>
		/// <remarks>
		/// <para>The chain setup applies to every factory client name (registered through this API or through a plain <c>AddHttpClient</c>): once any <c>AddBetterHttpClient*</c> registration ran, the whole factory is enrolled. There is no per-name wiring left: the setup resolves each name's options when that name's chain is built.</para>
		/// </remarks>
		private static void RegisterCore(IServiceCollection services)
		{
			services.TryAddSingleton<IBetterHttpClientFactory, DefaultBetterHttpClientFactory>();
			services.AddOptions<BetterHttpClientOptionsBuilder>();
			services.AddHttpClient(); // registers IHttpClientFactory / IHttpMessageHandlerFactory
			services.AddHttpClient(DefaultClientName); // the default (dynamic / by-URI) client is a regular named client
			// the two setups below are idempotent by implementation type (TryAddEnumerable)
			services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<HttpClientFactoryOptions>, BetterHttpLifetimeSetup>());
			services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<HttpClientFactoryOptions>, BetterHttpChainSetup>());
		}

		/// <summary>Disables the platform's periodic handler rotation for every factory client.</summary>
		/// <remarks>
		/// <para>The transport bounds DNS staleness by itself (<c>PooledConnectionLifetime</c>, set by the map's <see cref="INetworkMap.CreateTransportHandler"/>): platform chain rotation stacked on top would pay socket-pool cold starts against every active origin every ~2 minutes and buy nothing.</para>
		/// <para>This runs at the configure stage (not post-configure), so a client that wants periodic chain rebuild can still opt back in with the stock M.E.Http API, after the BetterHttp registration: <c>services.AddHttpClient(name).SetHandlerLifetime(...)</c>.</para>
		/// </remarks>
		private sealed class BetterHttpLifetimeSetup : IConfigureNamedOptions<HttpClientFactoryOptions>
		{
			public void Configure(HttpClientFactoryOptions options) => Configure(Options.DefaultName, options);

			public void Configure(string? name, HttpClientFactoryOptions options)
			{
				options.HandlerLifetime = Timeout.InfiniteTimeSpan;
			}
		}

		/// <summary>Assembles, for every factory client name, the BetterHttp chain (map transport, pipeline handler, capture) and the client defaults, from that name's resolved options.</summary>
		/// <remarks>
		/// <para>This is the piece that makes every door equivalent: whether a client is consumed as a typed client, a keyed client, a plain <see cref="System.Net.Http.IHttpClientFactory"/> client, a bare handler, or an <see cref="IBetterHttpClientFactory"/> shell, it rides this chain.</para>
		/// <para>It is a post-configure on purpose: its handler action must run after every application <c>AddHttpMessageHandler</c> action, so application handlers land between the capture handler (outermost) and the <see cref="BetterHttpPipelineHandler"/> (innermost delegating handler, right above the transport).</para>
		/// <para>The primary handler is always the map's transport, built from the name's resolved options: this is the sandboxing guarantee inside a distributed test. Per-name transport customization goes through the options (TLS, proxy, <see cref="BetterHttpClientOptions.Handlers"/>), not through <c>ConfigurePrimaryHttpMessageHandler</c>.</para>
		/// </remarks>
		private sealed class BetterHttpChainSetup : IPostConfigureOptions<HttpClientFactoryOptions>
		{

			public BetterHttpChainSetup(IServiceProvider services)
			{
				this.Services = services;
			}

			/// <summary>Root service provider, used by the client actions (the handler actions use the builder's own provider).</summary>
			private IServiceProvider Services { get; }

			public void PostConfigure(string? name, HttpClientFactoryOptions options)
			{
				var clientName = name ?? Microsoft.Extensions.Options.Options.DefaultName;

				options.HttpMessageHandlerBuilderActions.Add(builder =>
				{
					var services = builder.Services;
					var clientOptions = ResolveBundleOptions(services, builder.Name ?? clientName);

					var map = services.GetService<INetworkMap>() ?? throw new InvalidOperationException($"You must register an implementation for {nameof(INetworkMap)} during startup, in order to use the BetterHttpClient stack.");

					// raw transport (sockets in prod, virtual network in tests), plus the transport-level wrappers (credentials, custom handlers, filter wrappers)
					var transport = map.CreateTransportHandler(clientOptions);
					builder.PrimaryHandler = clientOptions.BuildTransportPipeline(transport, services);

					// the request-stage handler, innermost delegating handler (right above the transport), so it sees the request
					// after the client's default headers AND after any application handler has run.
					// Clock and time provider come from the host's DI first: inside a distributed test they are the test's
					// (possibly fake) clock, while the virtual map's own Clock is always the system clock.
					var timeProvider = services.GetService<TimeProvider>() ?? TimeProvider.System;
					var clock = services.GetService<IClock>() ?? map.Clock;
					builder.AdditionalHandlers.Add(new BetterHttpPipelineHandler(builder.Name ?? clientName, clientOptions, services, clock, timeProvider));

					// If a higher layer registered an outer capture handler (e.g. packet capture) under CaptureHandlerServiceKey,
					// insert it as the outermost handler of this chain, so capture rides the whole pipeline (a bare handler obtained
					// from IHttpMessageHandlerFactory is captured too, not just client sends). Resolved per chain build, so it
					// rotates with the pooled chain; Insert(0) means no extra handler when capture is absent.
					if (services.GetKeyedService<DelegatingHandler>(CaptureHandlerServiceKey) is { } capture)
					{
						builder.AdditionalHandlers.Insert(0, capture);
					}
				});

				// client defaults (headers, request version) applied to every client instance the factory hands out, whatever the
				// door (typed, keyed, or CreateClient). Insert(0): an application's own AddHttpClient(name, client => ...) action
				// runs after this one, so explicit application code wins over the options-sourced defaults.
				var rootServices = this.Services;
				options.HttpClientActions.Insert(0, client =>
				{
					var clientOptions = ResolveBundleOptions(rootServices, clientName);
					client.DefaultRequestVersion = clientOptions.DefaultRequestVersion;
					client.DefaultVersionPolicy = clientOptions.DefaultVersionPolicy;
					clientOptions.DefaultRequestHeaders.Apply(client.DefaultRequestHeaders);
				});
			}

		}

		/// <summary>Resolves the effective <see cref="BetterHttpClientOptions"/> for a client name: global filters/handlers, the global configure, then the per-name configure. A fresh instance is returned each call (so nothing is mutated across calls).</summary>
		/// <remarks>
		/// <para>Consumers may use this to INSPECT a client's effective policy (e.g. whether it carries a custom certificate-validation callback); mutating the returned instance has no effect on the client.</para>
		/// <para>Caveat: the pooled pipeline (this method, when the chain is stitched) and a shell's runtime resolve two separate options instances for the same name. Filters therefore must keep per-request state in <c>context.State</c> - never in instance fields that try to coordinate their <c>Wrap</c> with their stage callbacks, since the two runs see different option objects.</para>
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

			// the configuration override layers run last: the section's Defaults, then Clients:<name>.
			// "inherit" restores a knob to its value at the layer named by the lazy snapshot: for a Defaults knob
			// that is the code-global baseline; for a per-name knob it is the code-global baseline plus the
			// configuration Defaults (skipping the client's own code configure).
			if (builder.ConfigurationSections.Count > 0)
			{
				var codeGlobal = new Lazy<BetterHttpClientOptions>(() =>
				{
					var g = new BetterHttpClientOptions();
					builder.Configure?.Invoke(g);
					return g;
				});
				foreach (var section in builder.ConfigurationSections)
				{
					ApplyConfigurationSection(options, section.GetSection("Defaults"), codeGlobal);
				}
				var configGlobal = new Lazy<BetterHttpClientOptions>(() =>
				{
					var g = new BetterHttpClientOptions();
					builder.Configure?.Invoke(g);
					foreach (var section in builder.ConfigurationSections)
					{
						ApplyConfigurationSection(g, section.GetSection("Defaults"), codeGlobal);
					}
					return g;
				});
				foreach (var section in builder.ConfigurationSections)
				{
					ApplyConfigurationSection(options, section.GetSection("Clients").GetSection(name), configGlobal);
				}
			}

			return options;
		}

		/// <summary>Applies one configuration override section onto resolved options. A missing section is a no-op; the value <c>"inherit"</c> restores a knob from the <paramref name="inherited"/> snapshot.</summary>
		private static void ApplyConfigurationSection(BetterHttpClientOptions options, IConfigurationSection section, Lazy<BetterHttpClientOptions> inherited)
		{
			if (!section.Exists()) return;

			if (section["Timeout"] is { } timeout)
			{
				options.Timeout = IsInherit(timeout) ? inherited.Value.Timeout : TimeSpan.Parse(timeout, CultureInfo.InvariantCulture);
			}

			if (section["AllowAutoRedirect"] is { } redirect)
			{
				options.AllowAutoRedirect = IsInherit(redirect) ? inherited.Value.AllowAutoRedirect : bool.Parse(redirect);
			}

			if (section["AutomaticDecompression"] is { } decompression)
			{
				options.AutomaticDecompression = IsInherit(decompression) ? inherited.Value.AutomaticDecompression : Enum.Parse<DecompressionMethods>(decompression, ignoreCase: true);
			}

			if (section["Tls"] is { } tlsScalar && IsInherit(tlsScalar))
			{ // the whole TLS group falls back to the inherited layer
				options.ServerCertificateCustomValidationCallback = inherited.Value.ServerCertificateCustomValidationCallback;
			}
			else
			{
				var tls = section.GetSection("Tls");
				if (tls["Mode"] is { } mode)
				{
					if (IsInherit(mode))
					{
						options.ServerCertificateCustomValidationCallback = inherited.Value.ServerCertificateCustomValidationCallback;
					}
					else if (string.Equals(mode, "System", StringComparison.OrdinalIgnoreCase))
					{
						options.ServerCertificateCustomValidationCallback = null;
					}
					else if (string.Equals(mode, "AcceptSelfSigned", StringComparison.OrdinalIgnoreCase))
					{
						options.AcceptSelfSignedServerCertificates();
					}
					else if (string.Equals(mode, "AcceptAny", StringComparison.OrdinalIgnoreCase))
					{
#pragma warning disable CS0618 // the operator opted in from configuration; the application decides its own auditing
						options.DangerousAcceptAnyServerCertificate();
#pragma warning restore CS0618
					}
					else if (string.Equals(mode, "TrustRoots", StringComparison.OrdinalIgnoreCase))
					{
						throw new NotSupportedException("Tls:Mode 'TrustRoots' is not bindable from configuration yet; pin the roots in code with TrustServerCertificates(...).");
					}
					else
					{
						throw new InvalidOperationException($"Unknown Tls:Mode '{mode}': expected System, AcceptSelfSigned, AcceptAny or inherit.");
					}
				}
			}
		}

		private static bool IsInherit(string value)
			=> string.Equals(value, "inherit", StringComparison.OrdinalIgnoreCase);

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
