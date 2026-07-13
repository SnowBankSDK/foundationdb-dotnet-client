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

// ReSharper disable MethodHasAsyncOverload
namespace SnowBank.Testing.Framework
{
	using System.Globalization;
	using System.Net;
	using System.Net.Http;
	using System.Net.Http.Headers;
	using System.Reflection;
	using JetBrains.Annotations;
	using Microsoft.AspNetCore.Builder;
	using Microsoft.AspNetCore.Hosting;
	using Microsoft.AspNetCore.Http.Connections;
	using Microsoft.AspNetCore.ResponseCompression;
	using Microsoft.AspNetCore.Routing;
	using Microsoft.AspNetCore.Routing.Internal;
	using Microsoft.AspNetCore.SignalR.Client;
	using Microsoft.AspNetCore.TestHost;
	using Microsoft.Extensions.Configuration;
	using Microsoft.Extensions.FileProviders;
	using Microsoft.Extensions.Hosting;
	using Microsoft.Extensions.Logging;
	using SnowBank.Messaging.Events;
	using SnowBank.Networking.Http;
	using SnowBank.Networking.PacketCapture;
	using SnowBank.Runtime.Converters;

	/// <summary>Lifecycle state of a <see cref="DistributedTestComponent"/> (a virtual host).</summary>
	[PublicAPI]
	public enum TestComponentState
	{
		/// <summary>A lifecycle phase (Init or Start) threw: the component is broken and cannot run.</summary>
		Failed = -2,

		/// <summary>The component has been stopped and disposed for good (terminal teardown).</summary>
		Destroyed = -1,

		/// <summary>Default uninitialized value; not a real lifecycle state.</summary>
		Invalid = 0,

		/// <summary>The component has been constructed but not yet prepared (no context or network identity yet).</summary>
		Building = 1,

		/// <summary>The component is being prepared: it acquires its context, clock and network identity (IP address).</summary>
		Preparing = 2,

		/// <summary>The component is being initialized: it builds its WebApplication, DI container and TestServer.</summary>
		Initializing = 3,

		/// <summary>The component is starting: it runs its startup handlers and enables normal logging.</summary>
		Starting = 4,

		/// <summary>The component is up and running, ready to accept requests.</summary>
		Started = 5,

		/// <summary>The component is being stopped (draining and tearing down its host).</summary>
		Stopping = 6,

		/// <summary>The host's incarnation has been stopped via StopHost, but the component can be (re)started via StartHost
		/// (it keeps its identity, network registration, RestartCount and Data bag).</summary>
		Stopped = 7,
	}

	/// <summary>Coarse public status of a virtual host, used by test methods (a simplification of the internal <see cref="TestComponentState"/>).</summary>
	public enum HostStatus
	{
		/// <summary>The host is being (re)started but is not yet ready.</summary>
		Starting,
		/// <summary>The host is up and ready.</summary>
		Started,
		/// <summary>The host is being stopped.</summary>
		Stopping,
		/// <summary>The host is stopped (the initial state, or after StopHost). It can be (re)started.</summary>
		Stopped,
	}

	/// <summary>Options controlling how a virtual host is (re)started
	/// via <see cref="DistributedTestComponent.StartHost(HostStartOptions?, CancellationToken)"/>.</summary>
	/// <remarks>Currently a placeholder. Future properties will control what happens to the host's NETWORK IDENTITY on restart: by default the host keeps the same IP and re-registers on the same ports (today's behavior); a future option will model the fake DHCP assigning a DIFFERENT IP, to verify that the system keys on the logical identity (handshake) and NOT on the IP address (so that two hosts swapping IPs across restarts does not cause havoc).</remarks>
	public sealed record HostStartOptions
	{
		// (placeholder for future network-identity / restart options)
	}

	/// <summary>Represents an independent "actor" in the test environment (ex: a client, a backend or API server, a web browser or mobile app, an IoT device, ...)</summary>
	[PublicAPI]
	[DebuggerDisplay("Id={Id}")]
	public abstract class DistributedTestComponent : IDistributedWebTestComponent
	{

		public TestComponentState State => m_state;
		private TestComponentState m_state;

		public TestServer Server => m_server ?? throw new InvalidOperationException("Server not yet initialized");
		private TestServer? m_server;

		/// <summary>The web application host that owns the DI container (and the TestServer). Retained so it can be
		/// ASYNC-disposed at teardown, which disposes the container and its IAsyncDisposable singletons (e.g. a TeleportHub
		/// and its sinks/connections). Disposing only the TestServer leaves those alive until GC.</summary>
		private WebApplication? m_host;

		public IDistributedTestContext Context => m_context ?? throw new InvalidOperationException("Context not yet initialized");
		private IDistributedTestContext? m_context;

		protected SimpleTest Test => m_context?.TestSubject ?? throw new InvalidOperationException("Context not yet initialized");

		public IVirtualNetworkLocation Location { get; }

		public IConfiguration Configuration => m_configuration ?? throw new InvalidOperationException("Configuration not yet initialized");
		private IConfiguration? m_configuration;

		/// <summary>Map of the network, as seen by this virtual host</summary>
		public IVirtualNetworkMap NetworkMap
		{
			get => m_networkMap ?? throw new InvalidOperationException($"There is no Network Map declared for test component {this.Id}.");
			set => m_networkMap = value;
		}
		private IVirtualNetworkMap? m_networkMap;

		/// <summary>List of subcomponents (processes, services, browsers, ...) running under this virtual host</summary>
		public List<IDistributedTestComponent> SubComponents { get; init; } = [];

		IReadOnlyList<IDistributedTestComponent> IDistributedTestComponent.SubComponents => this.SubComponents;

		protected DistributedTestComponent(string id, IVirtualNetworkLocation location, CancellationToken lifetime, IDistributedTestComponent? parent = null)
		{
			Contract.NotNullOrEmpty(id);
			this.Id = id;
			this.Location = location;
			this.Parent = parent;
			this.Lifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
			this.NetworkIdentity.HostName = id.ToLowerInvariant();
			this.NetworkIdentity.DnsSuffix = location.Options.DnsSuffix;
			this.RealClock = SystemClock.Instance;
			this.Clock = this.RealClock; // we don't have the test context yet, we will use the "real time clock" for now
			m_state = TestComponentState.Building;

			// generates a "fake" process id in the range 5x_xxxx, for APIs that would require one
			this.ProcessId = 50000 + ((location.Id + ":" + id).GetHashCode() % 10000);
		}

		public string Id { get; }

		/// <summary>Fake process id that can be used by services that require one</summary>
		/// <remarks>This is NOT guaranteed to be unique, and COULD conflict with an actual process with the same ID that would happen to run on the host while test suite is running!</remarks>
		protected int ProcessId { get; }

		public IDistributedTestComponent? Parent { get; }

		public VirtualHostIdentity NetworkIdentity { get; set; } = new ();

		private CancellationTokenSource Lifetime { get; }

		public CancellationToken Cancellation => this.Lifetime.Token;

		private IServiceProvider? m_services;
		public IServiceProvider Services => m_services ?? throw new InvalidOperationException("Test server is not ready on this component");

		public IClock Clock { get; private set; }

		public IClock RealClock { get; }

		/// <summary>Number of times this host has been restarted (0 = the initial start, 1 = after the first restart, ...). Stable across restarts.</summary>
		public int RestartCount { get; private set; }

		/// <summary>True on the initial start of this host (<see cref="RestartCount"/> == 0).</summary>
		public bool IsFirstStart => this.RestartCount == 0;

		/// <summary>True if this host has been restarted at least once (<see cref="RestartCount"/> &gt; 0).</summary>
		public bool IsRestart => this.RestartCount > 0;

		/// <summary>Opaque per-host "handoff" bag that PERSISTS across restarts (disposed and cleared only at the end of the test).</summary>
		/// <remarks>
		/// <para>An in-memory stand-in for what a real process would keep on disk: config files, data files, or other asset files.
		/// Use it to model the application's DURABLE local state that a restarted process inherits,
		/// like a fresh "clone" picking up its predecessor's local work.</para>
		/// <para>Store ONLY POCOs or handles your test owns. Do NOT use it for process-scoped framework or connection state
		/// (e.g. a ConnectionId): a restart is a NEW process and is supposed to reset that.</para>
		/// <para>NEVER store anything resolved from the host's DI container: it is disposed when the incarnation stops,
		/// leaving a dangling reference. <see cref="IDisposable"/> / <see cref="IAsyncDisposable"/> values are disposed at final teardown.</para>
		/// </remarks>
		public Dictionary<object, object> Data { get; } = new();

		public IEventBus EventBus => this.Services.GetRequiredService<IEventBus>();

		/// <summary>List of handlers called just before Init</summary>
		private List<Action> DeferredInitHandlers { get; } = [ ];

		/// <summary>List of handlers called just before Starting</summary>
		private List<Action> DeferredStartingHandlers { get; } = [ ];

		/// <summary>Minimum Log level for adding logs to the timeline</summary>
		private LogLevel MinimumTimelineLogLevel = LogLevel.Information;

		/// <summary>Changes the minimum level for logs generated by the test to be included in the Timeline</summary>
		/// <remarks>
		/// <para>The default value is <see cref="LogLevel.Information"/>. Please note that each distributed component can have its own minimum log level, and should be configured to emit logs at this level, otherwise they will not be visible.</para>
		/// <para>This can help reduce the spam for a tests that intentionally generates error conditions, or temporarily include ALL logs while troubleshooting an issue.</para>
		/// <para>This method can be called <i>during</i> the test execution, but due to the asynchronous nature of loggers, it may not apply immediately (and you may miss some logs, or include unwanted logs)</para>
		/// </remarks>
		public void SetTimelineLogLevel(LogLevel level) => this.MinimumTimelineLogLevel = level;

		protected void AddInitHandler(Action handler) => this.DeferredInitHandlers.Add(handler);

		protected void AddDeferredStartingHandler(Action handler) => this.DeferredStartingHandlers.Add(handler);

		public void AddSubComponent(IDistributedTestComponent component)
		{
			Contract.NotNull(component);

			if (this.State != TestComponentState.Building)
			{
				throw new InvalidOperationException($"Cannot add a new sub-component to a component that is in the {this.State} state");
			}

			if (component == this) throw new InvalidOperationException("Cannot add a component to itself");
			if (this.SubComponents.Contains(component)) throw new InvalidOperationException("Component is already defined");
			if (component.SubComponents.Contains(this)) throw new InvalidOperationException("Cannot add a component that would include itself");

			this.SubComponents.Add(component);
		}

		/// <summary>Returns the absolute uri for an endpoint hosted by this virtual host</summary>
		/// <param name="path">Relative path, with optional query string (ex: <c>"/api/hello?world=42"</c>)</param>
		/// <returns>Corresponding external uri (ex: <c>"https://host.domain.simulated/hello?world=42"</c>)</returns>
		/// <remarks>Returns just the scheme and FQDN if <paramref name="path"/> is null (ex: <c>"https://host.domain.simulated"</c>)</remarks>
		public Uri GetUri(string? path = null) => new($"https://{this.NetworkMap.Host.Fqdn}{path}");

		/// <inheritdoc />
		public bool Offline => this.NetworkMap.Host.Offline;

		/// <inheritdoc />
		public void SetOffline(bool offline) => this.NetworkMap.Host.SetOffline(offline);

		/// <summary>Returns a new virtual <see cref="HttpMessageHandler"/> that will talk to this virtual host</summary>
		/// <returns>Handler that routes queries to this host's virtual http server</returns>
		/// <exception cref="InvalidOperationException">If the host is not ready yet.</exception>
		protected HttpMessageHandler CreateHttpHandler()
		{
			var handler = m_server?.CreateHandler();
			//TODO: custom config?
			return handler ?? throw new InvalidOperationException($"Test server is not ready on {this.Id} yet ({this.State})");
		}

		/// <summary>Adds HTTP request headers that tag the source and target virtual host for this request</summary>
		/// <remarks>
		/// <para>Add the <c>X-SBK-ORIGIN-...</c> and <c>X-SBK-TARGET-...</c>headers, that can be used to help track this host as the origin</para>
		/// <para>These headers only exist inside the virtual distributed test framework and are not used or recognized in production!</para>
		/// </remarks>
		protected static void TagPath(HttpRequestHeaders headers, IDistributedTestComponent origin, IDistributedTestComponent? target, Uri? uri)
		{
			string? originPeer = null;
			string? targetPeer = null;

			if (target is not null)
			{
				var sourceIp = origin.NetworkMap.GetPublicIPAddressForHost(target.NetworkMap.Host);
				if (sourceIp is not null)
				{
					int port = 12345; //BUGBUG: dynamically allocate a temporary port?
					originPeer = string.CreateInvariant($"{sourceIp}:{port}");
				}

				var targetIp = target.NetworkMap.GetPublicIPAddressForHost(origin.NetworkMap.Host);
				if (targetIp is not null)
				{
					int port = uri?.Port ?? 443; //REVIEW: require uri to be not null?
					targetPeer = string.CreateInvariant($"{targetIp}:{port}");
				}
			}

			headers.Add("X-SBK-ORIGIN", $"\"{origin.Id}\"; location=\"{origin.Location.Id}\"; host=\"{origin.NetworkMap.Host.Fqdn}\"{(originPeer is not null ? $"; peer=\"{originPeer}\"" : "")}");
			if (target is not null)
			{
				headers.Add("X-SBK-TARGET", $"\"{target.Id}\"; location=\"{target.Location.Id}\"; host=\"{target.NetworkMap.Host.Fqdn}\"{(targetPeer is not null ? $"; peer=\"{targetPeer}\"" : "")}");
			}
		}

		/// <summary>Returns an HTTP client that will send requests to this virtual host</summary>
		/// <param name="options">Options used to configure the HTTP client</param>
		/// <returns>Client that will be setup to execute requests <i>locally</i> from the host to itself, bypassing any injected errors or network connectivity issues.</returns>
		public BetterHttpClient GetLocalBetterHttpClient(BetterHttpShellOptions? options = null) => GetBetterHttpClient(this, options);

		/// <summary>Returns an HTTP client that will send requests from this virtual host to another host in the virtual network</summary>
		/// <param name="remote">Remote host</param>
		/// <param name="options">Per-shell options for this client (default headers, request version, hooks). Wire policy belongs to the bundle.</param>
		/// <returns>Client that will be setup to execute requests <i>from</i> the current host, <i>to</i> the remote host, while emulating any injected errors or network connectivity issues.</returns>
		public BetterHttpClient GetBetterHttpClient(IDistributedWebTestComponent remote, BetterHttpShellOptions? options = null)
		{
			var uri = remote.GetUri();
			var factory = GetRequiredService<IBetterHttpClientFactory>();

			// transient shell over the pooled bundle; per-target tags live on the shell's DefaultRequestHeaders (never the pooled chain)
			var client = options is not null ? factory.CreateClient(uri, options) : factory.CreateClient(uri);

			TagPath(client.DefaultRequestHeaders, this, remote, uri);
			return client;
		}

		/// <summary>Returns an HTTP client that will talk to the specified host or address</summary>
		/// <param name="hostOrAddress">Address of the remote host (note: only the hostname part of the URI is used)</param>
		/// <param name="options">Per-shell options for this client (default headers, request version, hooks). Wire policy belongs to the bundle.</param>
		/// <returns>Client that will be setup to execute requests <i>from</i> the current host, <i>to</i> the remote host, while emulating any injected errors or network connectivity issues.</returns>
		public BetterHttpClient GetBetterHttpClient(Uri hostOrAddress, BetterHttpShellOptions? options = null)
		{
			EnsureStarted();

			var remote = this.NetworkMap.FindHost(hostOrAddress.Host);
			var target = remote is not null ? this.Context.GetHost(remote.Id) : null;

			var factory = GetRequiredService<IBetterHttpClientFactory>();

			// transient shell over the pooled bundle; per-target tags live on the shell's DefaultRequestHeaders (never the pooled chain)
			var client = options is not null ? factory.CreateClient(hostOrAddress, options) : factory.CreateClient(hostOrAddress);

			TagPath(client.DefaultRequestHeaders, this, target, hostOrAddress);
			return client;
		}

		/// <summary>Returns a REST http client that will talk to this virtual host</summary>
		public RestHttpProtocol GetLocalRestClient(Action<RestHttpClientOptions>? configure = null) => this.GetRestClient(this, configure);

		/// <summary>Returns a REST http client that will talk to the specified remote host</summary>
		public RestHttpProtocol GetRestClient(IDistributedWebTestComponent remote, Action<RestHttpClientOptions>? configure = null)
		{
			var factory = GetRequiredService<RestHttpProtocolFactory>();
			var uri = remote.GetUri();
			var client = factory.CreateClient(uri, configure);
			TagPath(client.Http.DefaultRequestHeaders, this, remote, uri);
			return client;
		}

		/// <summary>Returns a REST http client that will talk to the specified remote host</summary>
		public RestHttpProtocol GetRestClient(Uri hostOrAddress, Action<RestHttpClientOptions>? configure = null)
		{
			return GetRequiredService<RestHttpProtocolFactory>().CreateClient(hostOrAddress.GetComponents(UriComponents.SchemeAndServer, UriFormat.SafeUnescaped), configure);
		}

		public IHubConnectionBuilder GetHubConnectionBuilder(string path, Func<Task<string?>>? accessTokenProvider = null) => GetHubConnectionBuilder(this, path, accessTokenProvider);

		public IHubConnectionBuilder GetHubConnectionBuilder(IDistributedWebTestComponent remote, string path, Func<Task<string?>>? accessTokenProvider = null)
		{
			var uri = remote.GetUri(path);

			var builder = new HubConnectionBuilder()
				.WithUrl(uri, options =>
				{
					options.Transports = HttpTransportType.ServerSentEvents;
					options.AccessTokenProvider = accessTokenProvider;
					options.HttpMessageHandlerFactory = (_) =>
					{
						// pooled bundle chain for the SignalR connection: it carries the FULL pipeline (packet capture included), while the
						// target host is still resolved per-request from the request URI. This is what makes SignalR traffic ride capture too.
						var handlerFactory = remote.GetRequiredService<IHttpMessageHandlerFactory>();
						return handlerFactory.CreateHandler(BetterHttpClientExtensions.DefaultClientName);
					};
				});

			return builder;
		}

		/// <summary>Allows the component to register with the global test context</summary>
		protected virtual void OnRegisterComponent(IDistributedTestContext context)
		{
			// can be overloaded
		}

		public ValueTask Prepare(IDistributedTestContext context, CancellationToken startToken)
		{
			startToken.ThrowIfCancellationRequested();
			if (m_state != TestComponentState.Building) throw new InvalidOperationException("Prepare must be called first!");
			var tsStart = this.RealClock.GetCurrentInstant();
			m_state = TestComponentState.Preparing;
			m_context = context;
			this.Clock = context.Clock;

			try
			{

				if (this.Parent == null)
				{ // finalize the host's network identity

					if (this.NetworkIdentity.Addresses.Count == 0)
					{
						this.NetworkIdentity.Addresses.Add(this.Location.AllocateIpAddress());
					}
					else
					{
						foreach (var ip in this.NetworkIdentity.Addresses)
						{
							this.Location.RegisterIpAddress(ip);
						}
					}

					if (this.NetworkIdentity.HostName == null)
					{
						if (this.NetworkIdentity.Fqdn != null)
						{
							int p = this.NetworkIdentity.Fqdn.IndexOf('.');
							this.NetworkIdentity.HostName = p > 0 ? this.NetworkIdentity.Fqdn[..p] : this.NetworkIdentity.Fqdn;
						}
					}

					if (this.NetworkIdentity.Fqdn == null)
					{
						if (this.NetworkIdentity.HostName != null)
						{
							this.NetworkIdentity.Fqdn = this.NetworkIdentity.HostName + (this.NetworkIdentity.DnsSuffix ?? this.Location.Options.DnsSuffix ?? ".simulated");
						}
					}

					if (m_networkMap == null)
					{
						m_networkMap = this.Location.RegisterHost(this.Id, this.NetworkIdentity);
					}
				}
				else
				{ // use the same network location as the parent
					m_networkMap = this.Parent.NetworkMap;
				}

				OnRegisterComponent(context);

				foreach (var component in this.SubComponents)
				{
					component.Prepare(context, startToken);
				}

				this.Context.Timeline.Record(new ()
				{
					Source = this.Id,
					Start = tsStart,
					End = this.RealClock.GetCurrentInstant(),
					Category = "TEST",
					Label = this.Id + " Prepare",
				});

				return default;
			}
			catch (Exception e)
			{
				m_state = TestComponentState.Failed;

				this.Context.Timeline.Record(new ()
				{
					Source = this.Id,
					Start = tsStart,
					End = this.RealClock.GetCurrentInstant(),
					Category = "TEST",
					Label = $"{this.Id} Prepare failure: [{e.GetType().Name}] {e.Message}",
					Failed = true,
				});

				throw;
			}
		}

		/// <summary>Returns the assembly that would normally contain the assets for the emulated application</summary>
		protected virtual Assembly? GetStaticAssetsRuntimeAssembly() => null;

		public async ValueTask Init(CancellationToken startToken)
		{
			startToken.ThrowIfCancellationRequested();
			if (m_state != TestComponentState.Preparing) throw new InvalidOperationException("Init must only be called once Prepare was successful!");
			var tsStart = this.RealClock.GetCurrentInstant();
			m_state = TestComponentState.Initializing;

			try
			{

				#region Assets Paths (WebRoot, ContentRoot, ...)

				string? contentRoot = null;
				string? testAssemblyName = null;

				var runtimeAssemblyKey = GetStaticAssetsRuntimeAssembly()?.GetName().FullName;
				if (runtimeAssemblyKey != null)
				{
					const string MANIFEST_FILE = "MvcTestingAppManifest.json";
					if (!File.Exists(MANIFEST_FILE))
					{
						Assert.Fail("Could not find 'MvcTestingAppManifest.json in test folder!");
					}

					var obj = await JsonObject.LoadFromAsync(MANIFEST_FILE, startToken);
					var path = obj.Get<string?>(runtimeAssemblyKey, null);
					if (path == null)
					{
						Assert.Fail($"Could not find any entry for '{runtimeAssemblyKey}' in {MANIFEST_FILE}");
					}

					contentRoot = path == "~" ? AppContext.BaseDirectory : path;

					// assembly that contains the test method that is calling us
					testAssemblyName = this.Test.GetType().Assembly.GetName().Name;
				}

				#endregion

				var hostBuilder = WebApplication.CreateBuilder(new WebApplicationOptions()
				{
					ContentRootPath = contentRoot,
					EnvironmentName = Environments.Development, // must be set at creation: WebHost.UseEnvironment() after CreateBuilder is no longer supported
				});


				hostBuilder.WebHost.UseTestServer(options =>
				{
					//TODO: if this is a component without any http ports (ex: web browser?) we should not configure this ?
					options.BaseAddress = new Uri("https://" + this.NetworkMap.Host.Fqdn);
				});

				#region Assets Paths (WebRoot, ContentRoot, ...)

				if (runtimeAssemblyKey != null)
				{
					if (!string.IsNullOrEmpty(contentRoot))
					{
						//hostBuilder.WebHost.UseContentRoot(contentRoot);
						hostBuilder.WebHost.UseWebRoot(Path.Combine(contentRoot, "wwwroot"));
					}

					// assembly in which the calling test is located
					if (!string.IsNullOrWhiteSpace(testAssemblyName))
					{
						hostBuilder.WebHost.UseSetting(WebHostDefaults.StaticWebAssetsKey, testAssemblyName + ".staticwebassets.runtime.json");
					}

					hostBuilder.WebHost.UseStaticWebAssets();
				}

				#endregion

				ConfigureHostBuilder(hostBuilder.WebHost);

				// Capture the current test context!
				var testOutput = this.Context.LogOutput;
				var testOutputError = this.Context.LogOutputError;

				hostBuilder.WebHost.UseSetting("foo", "bar");

				// configuration
				{
					//context.HostingEnvironment. ContentRoot + WebRoot !
					var appConfig = new Dictionary<string, string?>();

					// setup packet capture
					appConfig["PacketCapture:Enabled"] = "true";
					appConfig["PacketCapture:Sinks:0"] = "Test";
					//appConfig["PacketCapture:Sinks:1"] = "Debug";

					ConfigureAppConfiguration(appConfig);
					hostBuilder.Configuration.AddInMemoryCollection(appConfig);
				}

				var services = hostBuilder.Services;
				m_configuration = hostBuilder.Configuration;

				// expose the collection of service descriptors so that we can inspect it later (in case the DI fails to start)
				services.AddSingleton(services);

				var testContext = this.Context;
				services.AddSingleton<IDistributedTestContext>(testContext);
				services.AddSingleton<IDistributedTestComponent>(this);
				services.AddSingleton<IDistributedWebTestComponent>(this);
				services.AddSingleton(this.GetType(), this);
				services.AddSingleton<IClock>(testContext.Clock);
				// the same time source through the TimeProvider facade: when the test installed a dual clock
				// (e.g. NodaTimeProvider over a FakeTimeProvider), timers/timeouts and Instants advance together
				services.AddSingleton<TimeProvider>(testContext.Clock as TimeProvider ?? TimeProvider.System);
				services.AddSingleton<IVirtualNetworkTopology>(testContext.Topology);
				services.AddSingleton<INetworkMap>((_) => this.NetworkMap);
				services.AddSingleton<IVirtualNetworkMap>((_) => this.NetworkMap);

				services.AddSingleton<IEventSink>(new TestEventSink(testContext.Timeline, this.Id));

				services.AddSingleton<IPacketCaptureSink, TestPacketCaptureSink>();
				services.AddBetterHttpClient((options) =>
				{
					options.Hooks = new DefaultBetterHttpHooks()
					{
						Failed = ((ctx, ex) =>
						{
							if (ex is HttpRequestException httpEx)
							{
								if (httpEx.HttpRequestError == HttpRequestError.Unknown)
								{
									Log($"HTTP request '{ctx.Request.RequestUri}' failed with status code {(int) ctx.StatusCode:N0} {ctx.StatusCode} (elapsed={ctx.Elapsed.TotalSeconds:N03} sec)");
								}
								else
								{
									Log($"HTTP request '{ctx.Request.RequestUri}' failed with {httpEx.HttpRequestError} '{ex.Message}'. (elapsed={ctx.Elapsed.TotalSeconds:N03} sec, status={(int) ctx.StatusCode:N0} {ctx.StatusCode})");
								}
							}
							else
							{
								Log($"HTTP request '{ctx.Request.RequestUri}' failed [{ex.GetType().Name}] '{ex.Message}'. (elapsed={ctx.Elapsed.TotalSeconds:N03} sec, status={(int) ctx.StatusCode:N0} {ctx.StatusCode})");
							}
						}),
						ResponseCompleted = ((ctx =>
						{
							if (ctx.Elapsed.TotalSeconds >= 2)
							{
								Log($"HTTP request '{ctx.Request.RequestUri}' took more than {ctx.Elapsed.TotalSeconds:N1} sec (status={(int) ctx.StatusCode:N0} {ctx.StatusCode})");
							}
						})),
					};
					//??
				});
				services.AddRestHttpProtocol();

				services.AddPacketCapture((options, sp) =>
				{
					options.ActorId = this.Id;
					// TODO ?
				});

				// Add response compression middleware using gzip
				services.Configure<BrotliCompressionProviderOptions>(options => options.Level = System.IO.Compression.CompressionLevel.Optimal);
				services.Configure<GzipCompressionProviderOptions>(options => options.Level = System.IO.Compression.CompressionLevel.Optimal);
				services.AddResponseCompression(options =>
				{
					options.EnableForHttps = true;
					options.Providers.Add<BrotliCompressionProvider>();
					options.Providers.Add<GzipCompressionProvider>();
					//TODO: add ZStandard ?

					options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat([ "text/vnd.graphviz" ]);
				});

				services.AddLogging(logBuilder =>
				{
					// WebApplication.CreateBuilder wires the default console/debug/eventsource providers; drop them so the
					// journal-feeding NUnit logger is the ONLY sink (the default console provider would otherwise emit a
					// second, uglier, two-line-per-event copy of everything the NUnit logger and the journal already carry).
					logBuilder.ClearProviders();

					logBuilder.AddNUnitLogging(options =>
					{
						options.ActorId = this.Id;
						options.Output = testOutput;
						options.OutputError = testOutputError;
						// report mode suppresses the live per-event stream (the events still feed the end-of-test journal);
						// color only when streaming to a real terminal (a captured/redirected output would show raw escape codes)
						options.EmitToOutput = SimpleTest.LogVerbosity != TestLogVerbosity.Report;
						options.UseColor = !Console.IsOutputRedirected && SimpleTest.LogVerbosity != TestLogVerbosity.Report;
						options.DateOrigin = this.Context.CreatedAt.ToDateTimeOffset().LocalDateTime;
						options.IncludeScopes = false;
						options.LogLevel = LogLevel.Warning; // only while we are starting, will be changed later
						options.MessageHandler = (msg) =>
						{
							// Library-registered trace events (e.g. wire messages, fdb transaction summaries) are surfaced as their own
							// journal kind, captured whenever the producing library emits them (gated only by the logger level, e.g.
							// WithLogLevel(Trace)), independent of MinimumTimelineLogLevel which governs only regular log lines.
							// The mapping (EventName -> kind/label) is registered by the relevant library via the environment builder,
							// so this generic framework needs no knowledge of any specific library that produces such events.
							if (msg.EventName is { } eventName && this.Context.TimelineEventRules.TryGetValue(eventName, out var rule))
							{
								this.Context.Timeline.Record(new()
								{
									Source = this.Id,
									Start = this.RealClock.GetCurrentInstant(),
									Category = rule.Category,
									Label = rule.FormatLabel?.Invoke(msg.Message) ?? msg.Message ?? "",
								});
								return;
							}

							// only include regular logs in the timeline if at or above the minimum level (that can be changed by the test)
							if (msg.Level >= this.MinimumTimelineLogLevel)
							{
								string s = msg.Message ?? msg.LogName;

								// the level is carried on the Datum (Level, set below) and rendered as its own column by the journal, so it is intentionally left out of the label here.

								//note: the "LogName" is formatted as '[...]', but we want to include the event name inside the brackets "[LogName:EventName]"
								var logName = msg.EventName is null ? msg.LogName : $"{msg.LogName.AsSpan()[..^1]}:{msg.EventName}]";

								if (msg.Exception is not null)
								{
									// if this is likely a bug, includes the full stack, otherwise only the message
									if (msg.Exception is NullReferenceException or ArgumentException or IndexOutOfRangeException or KeyNotFoundException)
									{
										//TODO: maybe only include the last N frames of the stack? (enough to be able to locate the failing code)
										s = $"{logName} \"{s}\": [{msg.Exception.GetType().GetFriendlyName()}] {msg.Exception}";
									}
									else
									{
										s = $"{logName} \"{s}\": [{msg.Exception.GetType().GetFriendlyName()}] {msg.Exception.Message}";
									}
								}
								else
								{
									s = $"{logName} \"{s}\"";
								}

								this.Context.Timeline.Record(new()
								{
									Source = this.Id,
									Start = this.RealClock.GetCurrentInstant(),
									Category = "LOG",
									Label = s,
									Level = msg.Level,
									// Failed drives the "!!" (error) gutter in the journal: reserve it for errors;
									// a plain warning gets its own "! " gutter from the Level column
									Failed = msg.Level >= LogLevel.Error,
								});
							}
						};
					});
					logBuilder.SetMinimumLevel(m_logLevel);

					// reduce the log spam caused by ASP.NET Core and other well known libraries...
					logBuilder.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
					logBuilder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
					logBuilder.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
					logBuilder.AddFilter("Grpc.Net.Client", LogLevel.Warning);
				});

				services.AddSingleton<Timeline>(this.Context.Timeline);

				ConfigureServices(hostBuilder);

				// build it (retained in m_host so it - and its DI container - can be async-disposed at teardown)
				var host = hostBuilder.Build();
				m_host = host;

				// configure it
				{
					// grab the services as soon as possible
					m_services = host.Services;

					if (m_networkMap is not null && this.IsFirstStart)
					{
						// Only bind on the FIRST start. The binding resolves m_server live (via CreateHttpHandler), so after a restart it
						// already points at the fresh server - re-binding would throw on the duplicate port (and would be redundant).
						RegisterWithNetwork(m_networkMap);
					}

					try
					{
						ConfigureApplication(host);
					}
					catch (Exception e)
					{
						throw new InvalidOperationException($"Failed to configure applications for test component: {e.Message}", e);
					}
				}

				// start it
				startToken.ThrowIfCancellationRequested();
				await host.StartAsync(startToken);

				m_server = host.GetTestServer();
				// note: the TestServer.Services and IHost.Services property do not return the same instance, and it is not clear which should be used over the other.
				// => we will use the one from the TestServer, as it is more specific to the web application and matches what was done when using WebHostBuilder instead of HostBuilder.
				m_services = m_server.Services;
				m_configuration = m_services.GetRequiredService<IConfiguration>();

				foreach (var handler in this.DeferredInitHandlers)
				{
					handler();
				}

				foreach (var component in this.SubComponents)
				{
					await component.Init(startToken);
				}

				await OnInitialize(m_server, m_configuration, startToken);

				this.Context.Timeline.Record(new ()
				{
					Source = this.Id,
					Start = tsStart,
					End = this.RealClock.GetCurrentInstant(),
					Category = "TEST",
					Label = this.Id + " Init",
				});
			}
			catch (Exception e)
			{
				m_state = TestComponentState.Failed;
				this.Context.Timeline.Record(new ()
				{
					Source = this.Id,
					Start = tsStart,
					End = this.RealClock.GetCurrentInstant(),
					Category = "TEST",
					Label = $"{this.Id} Init failure: [{e.GetType().Name}] {e.Message}",
					Failed = true,
				});
				throw;
			}
		}

		protected virtual ValueTask OnInitialize(TestServer server, IConfiguration config, CancellationToken startToken)
		{
			return default;
		}

		[MustUseReturnValue]
		[MethodImpl(MethodImplOptions.NoInlining)]
		[DebuggerNonUserCode]
		public TService? GetService<TService>()
		{
			if (this.State is TestComponentState.Destroyed or TestComponentState.Invalid)
			{
				throw new InvalidOperationException($"Distributed test component '{this.Id}' cannot be in state {this.State}");
			}

			// in a method with multiple components, it is usually difficult to know which one is improperly configured,
			// so we will wrap the exceptions to add the component ID
			try
			{
				return this.Services.GetService<TService>();
			}
			catch (Exception e)
			{
				throw new InvalidOperationException($"Failed to get service '{typeof(TService).GetFriendlyName()}' on distributed test component '{this.Id}': {e.Message}", e);
			}
		}

		[MustUseReturnValue]
		[MethodImpl(MethodImplOptions.NoInlining)]
		[DebuggerNonUserCode]
		public TService? GetKeyedService<TService>(object? serviceKey)
		{
			if (this.State is TestComponentState.Destroyed or TestComponentState.Invalid)
			{
				throw new InvalidOperationException($"Distributed test component '{this.Id}' cannot be in state {this.State}");
			}

			// in a method with multiple components, it is usually difficult to know which one is improperly configured,
			// so we will wrap the exceptions to add the component ID
			try
			{
				return this.Services.GetKeyedService<TService>(serviceKey);
			}
			catch (Exception e)
			{
				throw new InvalidOperationException($"Failed to get service '{typeof(TService).GetFriendlyName()}' with key '{serviceKey}' on distributed test component '{this.Id}': {e.Message}", e);
			}
		}

		[MustUseReturnValue]
		[MethodImpl(MethodImplOptions.NoInlining)]
		[DebuggerNonUserCode]
		public TService GetRequiredService<TService>() where TService : notnull
		{
			if (this.State is TestComponentState.Destroyed or TestComponentState.Invalid)
			{
				throw new InvalidOperationException($"Distributed test component '{this.Id}' cannot be in state {this.State}");
			}

			// in a method with multiple components, it is usually difficult to know which one is improperly configured,
			// so we will wrap the exceptions to add the component ID
			try
			{
				return this.Services.GetRequiredService<TService>();
			}
			catch (Exception e)
			{
				// one frequent issue is that a top-level service was not added in the host ConfigureServices(..) callback.
				if (!this.Services.GetRequiredService<IServiceProviderIsService>().IsService(typeof(TService)))
				{
					throw new InvalidOperationException($"Service '{typeof(TService).GetFriendlyName()}' does not exist on distributed test component '{this.Id}'. Most likely it was not registered during the setup phase of the component.", e);
				}
				// another issue is that the service as a dependency on another type that is missing

				// generic error ?
				throw new InvalidOperationException($"Failed to get required service '{typeof(TService).GetFriendlyName()}' on distributed test component '{this.Id}': {e.Message}", e);
			}
		}

		[MustUseReturnValue]
		[MethodImpl(MethodImplOptions.NoInlining)]
		[DebuggerNonUserCode]
		public TService GetRequiredKeyedService<TService>(object? serviceKey) where TService : notnull
		{
			if (this.State is TestComponentState.Destroyed or TestComponentState.Invalid)
			{
				throw new InvalidOperationException($"Distributed test component '{this.Id}' cannot be in state {this.State}");
			}

			// in a method with multiple components, it is usually difficult to know which one is improperly configured,
			// so we will wrap the exceptions to add the component ID
			try
			{
				return this.Services.GetRequiredKeyedService<TService>(serviceKey);
			}
			catch (Exception e)
			{
				// one frequent issue is that a top-level service was not added in the host ConfigureServices(..) callback.
				if (!this.Services.GetRequiredService<IServiceProviderIsService>().IsService(typeof(TService)))
				{
					throw new InvalidOperationException($"Service '{typeof(TService).GetFriendlyName()}' does not exist on distributed test component '{this.Id}'. Most likely it was not registered during the setup phase of the component.", e);
				}
				// another issue is that the service as a dependency on another type that is missing

				// generic error ?
				throw new InvalidOperationException($"Failed to get required service '{typeof(TService).GetFriendlyName()}' with key '{serviceKey}' on distributed test component '{this.Id}': {e.Message}", e);
			}
		}

		[MustUseReturnValue]
		[MethodImpl(MethodImplOptions.NoInlining)]
		[DebuggerNonUserCode]
		public TService CreateInstance<TService>(params object[] parameters)
		{
			if (this.State is TestComponentState.Destroyed or TestComponentState.Invalid)
			{
				throw new InvalidOperationException($"Distributed test component '{this.Id}' cannot be in state {this.State}");
			}

			try
			{
				return ActivatorUtilities.CreateInstance<TService>(this.Services, parameters);
			}
			catch (Exception e)
			{
				throw new InvalidOperationException($"Failed to create instance of service '{typeof(TService).GetFriendlyName()}' on distributed test component '{this.Id}': {e.Message}", e);
			}
		}

		[MustUseReturnValue]
		[MethodImpl(MethodImplOptions.NoInlining)]
		[DebuggerNonUserCode]
		public TService GetServiceOrCreateInstance<TService>()
		{
			if (this.State is TestComponentState.Destroyed or TestComponentState.Invalid)
			{
				throw new InvalidOperationException($"Distributed test component '{this.Id}' cannot be in state {this.State}");
			}

			try
			{
				return ActivatorUtilities.GetServiceOrCreateInstance<TService>(this.Services);
			}
			catch (Exception e)
			{
				throw new InvalidOperationException($"Failed to get or create instance of service '{typeof(TService).GetFriendlyName()}' on distributed test component '{this.Id}': {e.Message}", e);
			}
		}

		/// <summary>Creates a logger for the given type</summary>
		[MustUseReturnValue]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public ILogger<T> CreateLogger<T>() => this.Services.GetRequiredService<ILogger<T>>();

		/// <summary>Called before the web host builder is configured</summary>
		protected virtual void ConfigureHostBuilder(ConfigureWebHostBuilder builder)
		{
			// can be overloaded
		}

		/// <summary>Called to configure the configuration (appsettings.json) used by the component</summary>
		protected virtual void ConfigureAppConfiguration(Dictionary<string, string?> config)
		{
			// can be overloaded
		}

		/// <summary>Called to inject custom services into the container used by the component</summary>
		protected abstract void ConfigureServices(WebApplicationBuilder builder);

		/// <summary>Called to configure the application that runs the component</summary>
		protected abstract void ConfigureApplication(WebApplication app);

		/// <summary>Called when the component is ready to register itself with the virtual network</summary>
		protected virtual void RegisterWithNetwork(IVirtualNetworkMap map)
		{
			// can be overloaded
		}

		/// <summary>Called when the component is ready to start</summary>
		protected virtual ValueTask OnStarting(CancellationToken ct)
		{
			// can be overloaded
			return default;
		}

		/// <inheritdoc/>
		public async ValueTask Start(CancellationToken startToken)
		{
			startToken.ThrowIfCancellationRequested();
			if (m_state != TestComponentState.Initializing) throw new InvalidOperationException("Start must only be called once Init was successful!");
			var tsStart = this.RealClock.GetCurrentInstant();
			m_state = TestComponentState.Starting;
			try
			{
				// start any subcomponent before
				foreach (var component in this.SubComponents)
				{
					try
					{
						await component.Start(startToken);
					}
					catch (Exception e)
					{
						throw new InvalidOperationException($"Failed to start sub-component {component.Id} of component {this.Id}", e);
					}
				}

				foreach (var handler in this.DeferredStartingHandlers)
				{
					handler();
				}

				await OnStarting(startToken);

				// enable normal logging while the component is running...
				GetRequiredService<NUnitLoggerProvider>().SetLogLevel(LogLevel.Trace);

				m_state = TestComponentState.Started;
				this.Context.Timeline.Record(new ()
				{
					Source = this.Id,
					Start = tsStart,
					End = this.RealClock.GetCurrentInstant(),
					Category = "TEST",
					Label = this.Id + " Startup",
				});
			}
			catch (Exception e)
			{
				m_state = TestComponentState.Failed;
				this.Context.Timeline.Record(new ()
				{
					Source = this.Id,
					Start = tsStart,
					End = this.RealClock.GetCurrentInstant(),
					Category = "TEST",
					Label = $"{this.Id} Start failed: [{e.GetType().Name}] {e.Message}",
					Failed = true,
				});
				throw;
			}
		}

		/// <summary>Stops this host's current incarnation - tears down the WebApplication, DI container, hub and its peer connections -
		/// while KEEPING the host's identity, network registration, <see cref="RestartCount"/> and <see cref="Data"/> bag,
		/// so it can be brought back up with <see cref="StartHost(CancellationToken)"/>.</summary>
		/// <remarks>Models a node going down (reboot, scale-in).
		/// Any durable backing store (e.g. a shared FakeDb) is external and survives.
		/// Other nodes' connections to this host break; their reconnect loops retry against the fresh incarnation once it starts.</remarks>
		public async ValueTask StopHost(CancellationToken stopToken)
		{
			if (m_state is TestComponentState.Stopped or TestComponentState.Destroyed) return; // already down
			if (m_state != TestComponentState.Started) throw new InvalidOperationException($"Cannot stop host {this.Id}: it is in the {m_state} state.");

			var tsStart = this.RealClock.GetCurrentInstant();
			m_state = TestComponentState.Stopping;

			// stop sub-components first (they run "under" this host, e.g. a browser talking to https://localhost/),
			// so their own hosts are torn down too
			foreach (var sub in this.SubComponents)
			{
				if (sub is DistributedTestComponent dc && dc.State == TestComponentState.Started)
				{
					try { await dc.StopHost(stopToken).ConfigureAwait(false); }
					catch (Exception e) { this.Context.LogOutputError.WriteLine($"Failed to stop sub-component {sub.Id} of host {this.Id}: {e}"); }
				}
			}

			// sever live connections so peers' in-flight calls abort cleanly, like an abrupt node death
			try { this.NetworkMap.Host.SetOffline(true); } catch { /* the network may already be gone */ }

			// drain captured packets into the journal BEFORE the DI container is disposed
			if (m_services is not null)
			{
				try
				{
					var packetManager = m_services.GetService<PacketCaptureManager>();
					if (packetManager is { IsRunning: true }) await packetManager.DrainAsync(stopToken).ConfigureAwait(false);
				}
				catch (Exception e) { this.Context.LogOutputError.WriteLine($"Failed to drain captured HTTP packets for host {this.Id}: {e}"); }

				try { await OnStopping(stopToken).ConfigureAwait(false); }
				catch (Exception e) { this.Context.LogOutputError.WriteLine($"Failed to stop host {this.Id}: {e}"); }
			}

			// tear down the host: disposing the WebApplication disposes the DI container, which ASYNC-disposes the hub (and its sinks/connections)
			var host = m_host;
			m_host = null;
			m_server = null;
			m_services = null;
			m_configuration = null;
			if (host is not null)
			{
				try { await host.StopAsync().ConfigureAwait(false); } catch { /* best-effort graceful stop */ }
				await host.DisposeAsync().ConfigureAwait(false);
			}

			m_state = TestComponentState.Stopped;
			this.Context.Timeline.Record(new() { Source = this.Id, Start = tsStart, End = this.RealClock.GetCurrentInstant(), Category = "TEST", Label = $"### {this.Id} STOP (incarnation #{this.RestartCount}) ###" });
		}

		/// <summary>Brings a stopped host back up (with default options).</summary>
		public ValueTask StartHost(CancellationToken startToken) => StartHost(null, startToken);

		/// <summary>Brings a stopped host back up: re-runs the SAME configuration and startup callbacks
		/// on a fresh WebApplication / DI container / hub, with the SAME identity and network position,
		/// and increments <see cref="RestartCount"/> so callbacks can branch on <see cref="IsRestart"/>.
		/// Sub-components are restarted with it.</summary>
		/// <param name="options">Options controlling the (re)start (currently a placeholder; defaults are used when <c>null</c>).</param>
		/// <param name="startToken">Token used to cancel the (re)start.</param>
		public async ValueTask StartHost(HostStartOptions? options, CancellationToken startToken)
		{
			options ??= new HostStartOptions();

			if (m_state == TestComponentState.Started) return;
			if (m_state != TestComponentState.Stopped) throw new InvalidOperationException($"Cannot start host {this.Id}: it is in the {m_state} state (only a Stopped host can be (re)started).");

			this.RestartCount++;
			this.Context.Timeline.Record(new() { Source = this.Id, Start = this.RealClock.GetCurrentInstant(), Category = "TEST", Label = $"### {this.Id} RESTART #{this.RestartCount} ###" });

			// bring the network back online (renew the OnlineToken so new connections are accepted again)
			try { this.NetworkMap.Host.SetOffline(false); } catch { }

			// re-run the standard bring-up: Init (build host + DI + TestServer) then Start (user startup handler), both of which
			// require the Preparing state and recurse into sub-components - so put the whole subtree back into Preparing first.
			m_state = TestComponentState.Preparing;
			foreach (var sub in this.SubComponents)
			{
				if (sub is DistributedTestComponent dc) dc.PrepareSubtreeForRestart();
			}
			await Init(startToken).ConfigureAwait(false);
			await Start(startToken).ConfigureAwait(false);
		}

		/// <summary>Recursively bumps <see cref="RestartCount"/> and puts this component and its sub-components back into the Preparing state,
		/// so the parent's Init/Start loops can rebuild the whole subtree on a restart.</summary>
		private void PrepareSubtreeForRestart()
		{
			this.RestartCount++;
			m_state = TestComponentState.Preparing;
			foreach (var sub in this.SubComponents)
			{
				if (sub is DistributedTestComponent dc) dc.PrepareSubtreeForRestart();
			}
		}

		/// <summary>Restarts this host (and its sub-components): <see cref="StopHost"/> immediately followed by <see cref="StartHost(CancellationToken)"/>.
		/// For a delayed restart, call StopHost, await your delay, then StartHost.</summary>
		public ValueTask Restart(CancellationToken ct) => Restart(null, ct);

		/// <summary>Restarts this host with the given <paramref name="options"/>.</summary>
		public async ValueTask Restart(HostStartOptions? options, CancellationToken ct)
		{
			await StopHost(ct).ConfigureAwait(false);
			await StartHost(options, ct).ConfigureAwait(false);
		}

		/// <summary>Called when the component is being stopped (whether the test ran successfully or not)</summary>
		protected virtual ValueTask OnStopping(CancellationToken ct)
		{
			return default;
		}

		/// <inheritdoc/>
		public async ValueTask Stop(CancellationToken stopToken)
		{
			var tsStart = this.RealClock.GetCurrentInstant();
			m_state = TestComponentState.Stopping;
			try
			{
				// note: m_services is null if the incarnation was already torn down via StopHost - skip the service-dependent shutdown in that case.
				if (m_services is not null)
				{
					// disable normal logging while we are shutting down...
					GetRequiredService<NUnitLoggerProvider>().SetLogLevel(LogLevel.Warning);

					// drain any remaining HTTP packets that where captured but not yet processed
					var packetManager = this.Services.GetService<PacketCaptureManager>();
					if (packetManager != null && packetManager.IsRunning)
					{
						try
						{
							// TO
							 await packetManager.DrainAsync(stopToken);
						}
						catch (Exception e)
						{
							this.Context.LogOutputError.WriteLine($"Failed to drain captured HTTP packets for component {this.Id}: {e}");
						}
					}
				}

				// stop any subcomponent...
				foreach (var component in this.SubComponents)
				{
					await component.Stop(stopToken);
				}

				// stop the rest of the component
				if (m_services is not null)
				{
					try
					{
						await OnStopping(stopToken);
					}
					catch (Exception e)
					{
						this.Context.LogOutputError.WriteLine($"Failed to stop component {this.Id}: {e}");
					}
				}
			}
			finally
			{
				await this.Lifetime.CancelAsync();

				m_state = TestComponentState.Destroyed;
				m_networkMap = null;
				var tsEnd = this.RealClock.GetCurrentInstant();
				this.Context.Timeline.Record(new ()
				{
					Start = tsStart,
					End = tsEnd,
					Category = "TEST",
					Label = this.Id + " Stop",
					Source = this.Id,
				});
			}
		}

		/// <summary>Called when the component is being disposed.</summary>
		protected virtual ValueTask OnDisposing()
		{
			return default;
		}

		/// <inheritdoc/>
		public async ValueTask DisposeAsync()
		{
			this.Lifetime.Dispose();
			try
			{
				await OnDisposing();
			}
			finally
			{
				m_state = TestComponentState.Destroyed;
				// Dispose the WHOLE host, not just the TestServer: this disposes the DI container and ASYNC-disposes its
				// IAsyncDisposable singletons - notably a TeleportHub, which disposes its sinks and tears down their peer
				// connections. Disposing only the TestServer left the hub (and its open connections) alive until GC.
				var host = m_host;
				m_host = null;
				m_server = null;
				if (host is not null)
				{
					try { await host.StopAsync().ConfigureAwait(false); }
					catch { /* best-effort graceful stop during teardown */ }
					await host.DisposeAsync().ConfigureAwait(false);
				}

				// dispose any IDisposable/IAsyncDisposable handed off via the Data bag (test-owned handles that persisted across restarts)
				foreach (var value in this.Data.Values)
				{
					try
					{
						switch (value)
						{
							case IAsyncDisposable ad: await ad.DisposeAsync().ConfigureAwait(false); break;
							case IDisposable d: d.Dispose(); break;
						}
					}
					catch (Exception e)
					{
						m_context?.LogOutputError.WriteLine($"Failed to dispose a Data entry for host {this.Id}: {e}");
					}
				}
				this.Data.Clear();
			}
		}

		/// <summary>Throws if the component has already started</summary>
		/// <exception cref="InvalidOperationException">The component has already started</exception>
		/// <remarks>Use this when you are attempting to configure the component in such a way that would not possible once it has started</remarks>
		protected void EnsureNotStarted()
		{
			if (m_state is < TestComponentState.Building or >= TestComponentState.Started) throw new InvalidOperationException($"Test component {this.Id} cannot execute this action when in the '{m_state}' state!");
		}

		/// <summary>Throws if the component has not already started (or has already stopped)</summary>
		/// <exception cref="InvalidOperationException">The component has not been started yet, or has already stopped</exception>
		/// <remarks>Use this when you are attempting to configure the component in such a way that would not possible if it is not running</remarks>
		protected void EnsureStarted()
		{
			if (m_state != TestComponentState.Started) throw new InvalidOperationException($"Test component {this.Id} cannot execute this action when in the '{m_state}' state!");
		}

		#region Logging

		private LogLevel m_logLevel = LogLevel.Information;

		/// <summary>Sets the default log level for this component</summary>
		/// <remarks>If the </remarks>
		public DistributedTestComponent WithLogLevel(LogLevel level)
		{
			EnsureNotStarted();
			m_logLevel = level;
			return this;
		}

		protected void Log(string message)
		{
			//REVIEW: do we have a better way to forward logs ?
			SimpleTest.Log($"{this.Id}: {message}");
		}

		protected void Log(ref DefaultInterpolatedStringHandler message)
		{
			//REVIEW: do we have a better way to forward logs ?
			SimpleTest.Log($"{this.Id}: {message.ToStringAndClear()}");
		}

		protected void LogError(string message)
		{
			//REVIEW: do we have a better way to forward logs ?
			SimpleTest.LogError($"{this.Id}: {message}");
		}

		protected void LogError(ref DefaultInterpolatedStringHandler message)
		{
			//REVIEW: do we have a better way to forward logs ?
			SimpleTest.LogError($"{this.Id}: {message.ToStringAndClear()}");
		}

		#endregion

		#region Asset Files...

		/// <summary>Used to access files in under the global 'wwwroot' folder</summary>
		/// <param name="path">Path to file, as if it was accessed by a web browser (ex: "/images/logo/foo.png" or "/css/site.css")</param>
		/// <param name="required">If <c>true</c>, ensures that the file exists. If <c>false</c> does not perform any checks</param>
		/// <returns>The file information. If <paramref name="required"/> is <c>false</c>, caller must check the <see cref="IFileInfo.Exists"/> property.</returns>
		public IFileInfo GetWebFile(string path, bool required = true)
		{
			var info = GetRequiredService<IWebHostEnvironment>().WebRootFileProvider.GetFileInfo(path);
			if (required && !info.Exists)
			{
				Assert.Fail($"This test requires web file '{path}' which has not found!");
			}
			return info;
		}

		/// <summary>Used to access files in under the global content root folder</summary>
		/// <param name="path">Path to file (ex: "/Content/Some/Path/to/data.xyz")</param>
		/// <param name="required">If <c>true</c>, ensures that the file exists. If <c>false</c> does not perform any checks</param>
		/// <returns>The file information. If <paramref name="required"/> is <c>false</c>, caller must check the <see cref="IFileInfo.Exists"/> property.</returns>
		public IFileInfo GetContentFile(string path, bool required = true)
		{
			var info = GetRequiredService<IWebHostEnvironment>().ContentRootFileProvider.GetFileInfo(path);
			if (required && !info.Exists)
			{
				Assert.Fail($"This test requires content file '{path}' which has not found!");
			}
			return info;
		}

		#endregion

		#region Local HTTP...

		/// <summary>Converts a template with arguments into a properly encoded URI</summary>
		/// <param name="pattern">Ex: "/foo/bar/{id}/baz"</param>
		/// <param name="arguments">Ex: new { id = "1234", hello = "world" }</param>
		/// <returns>"/foo/bar/1234/baz?hello=world"</returns>
		public Uri FormatUri(string pattern, Dictionary<string, object?>? arguments = null)
		{
			string url = pattern;
			StringBuilder? query = null;
			if (arguments != null)
			{
				foreach (var kv in arguments)
				{
					string s = $"{{{kv.Key}}}";
					if (pattern.Contains(s, StringComparison.Ordinal))
					{
						url = url.Replace(s, Uri.EscapeDataString(TypeConverters.ToString(kv.Value) ?? string.Empty));
					}
					else
					{
						if (query == null)
						{
							query = new StringBuilder();
							query.Append('?');
						}
						else
						{
							query.Append('&');
						}

						query.Append(kv.Key).Append('=').Append(TypeConverters.ToString(kv.Value));
					}
				}
			}
			return new Uri(url + query?.ToString(), UriKind.RelativeOrAbsolute);
		}

		private static void EnsureIsExternalUri(Uri uri)
		{
			Contract.NotNull(uri);
			if (!uri.IsAbsoluteUri)
			{
				//REVIEW: TODO: maybe accept if hostname is the same as the current node ?
				throw new ArgumentException("The URI must be absolute, as the request will be sent to a different node.");
			}
		}

		private static void EnsureIsLocalUri(string uri)
		{
			Contract.NotNullOrEmpty(uri);
			if (uri[0] != '/')
			{
				//REVIEW: TODO: maybe accept if hostname is the same as the current node ?
				throw new ArgumentException("The uri must not include the host name, must be absolute, and may include a query string (example: '/hello?id=world').");
			}
		}

		/// <summary>Helper for sending HTTP requests to or from this virtual host</summary>
		public HttpHelper Http => new(this);

		public readonly struct HttpHelper
		{

			private readonly DistributedTestComponent Component;

			internal HttpHelper(DistributedTestComponent component)
			{
				this.Component = component;
			}

			/// <summary>Helper methods for send HTTP requests to the host itself</summary>
			public HttpLocalHelper Local => new(this.Component);

			#region Binary...

			/// <summary>Sends an HTTP GET request that expects a binary response from this node</summary>
			/// <remarks>
			/// <para>The request is performed by this host, and will be sent to <paramref name="target"/>.</para>
			/// </remarks>
			public Task<(HttpStatusCode Result, Slice Body)> GetBinaryAsync(DistributedTestComponent target, string pathOnTarget, BetterHttpShellOptions? options = null, CancellationToken ct = default)
				=> target == this.Component
					? this.Local.GetBinaryAsync(pathOnTarget, options, ct)
					: GetBinaryAsync(target.GetUri(pathOnTarget), options, ct);

			/// <summary>Sends an HTTP GET request that expects a binary response from this node</summary>
			/// <remarks>
			/// <para>The request is performed by this host, and will be sent to the host name referenced in <paramref name="uri"/>.</para>
			/// <para>To send a request <i>to</i> this host (from the outside), use the <see cref="Local"/> helper instead.</para>
			/// </remarks>
			public Task<(HttpStatusCode Result, Slice Body)> GetBinaryAsync(string uri, BetterHttpShellOptions? options = null, CancellationToken ct = default)
				=> GetBinaryAsync(new Uri(uri, UriKind.Absolute), options, ct);

			/// <summary>Sends an HTTP GET request that expects a binary response from this node</summary>
			/// <remarks>
			/// <para>The request is performed by this host, and will be sent to the host name referenced in <paramref name="uri"/>.</para>
			/// <para>To send a request <i>to</i> this host (from the outside), use the <see cref="Local"/> helper instead.</para>
			/// </remarks>
			public async Task<(HttpStatusCode Result, Slice Body)> GetBinaryAsync(Uri uri, BetterHttpShellOptions? options = null, CancellationToken ct = default)
			{
				ct = ct.CanBeCanceled ? ct : this.Component.Cancellation;
				ct.ThrowIfCancellationRequested();

				EnsureIsExternalUri(uri);
				using var client = this.Component.GetBetterHttpClient(uri, options);

				return await this.Component.ExecuteHttpGetBinaryAsync(client, uri, ct);
			}

			#endregion

			#region Text...

			/// <summary>Executes an HTTP GET request from this host to another node, and returns the response body decoded as a string</summary>
			/// <remarks>
			/// <para>The request is performed by this host, and will be sent to <paramref name="target"/>.</para>
			/// <para>To send a request <i>to</i> this host (from the outside), use the <see cref="Local"/> helper instead.</para>
			/// </remarks>
			public Task<(HttpStatusCode Result, string? Body)> GetTextAsync(DistributedTestComponent target, string pathOnTarget, BetterHttpShellOptions? options = null, CancellationToken ct = default)
				=> target == this.Component
					? this.Local.GetTextAsync(pathOnTarget, options, ct)
					: GetTextAsync(target.GetUri(pathOnTarget), options, ct);

			/// <summary>Executes an HTTP GET request from this host to another node, and returns the response body decoded as a string</summary>
			/// <remarks>
			/// <para>The request is performed by this host, and will be sent to the host name referenced in <paramref name="uri"/>.</para>
			/// <para>To send a request <i>to</i> this host (from the outside), use the <see cref="Local"/> helper instead.</para>
			/// </remarks>
			public Task<(HttpStatusCode Result, string? Body)> GetTextAsync(string uri, BetterHttpShellOptions? options = null, CancellationToken ct = default)
				=> GetTextAsync(new Uri(uri, UriKind.Absolute), options, ct);

			/// <summary>Executes an HTTP GET request from this host to another node, and returns the response body decoded as a string</summary>
			/// <remarks>
			/// <para>The request is performed by this host, and will be sent to the host name referenced in <paramref name="uri"/>.</para>
			/// <para>To send a request <i>to</i> this host (from the outside), use the <see cref="Local"/> helper instead.</para>
			/// </remarks>
			public async Task<(HttpStatusCode Result, string? Body)> GetTextAsync(Uri uri, BetterHttpShellOptions? options = null, CancellationToken ct = default)
			{
				Assert.That(uri, Is.Not.Null);
				ct = ct.CanBeCanceled ? ct : this.Component.Cancellation;
				ct.ThrowIfCancellationRequested();

				EnsureIsExternalUri(uri);
				using var client = this.Component.GetLocalBetterHttpClient(options);

				return await this.Component.ExecuteHttpGetTextAsync(client, uri, ct);
			}

			/// <summary>Executes an HTTP POST request from this host to another node, and returns the response body decoded as a string</summary>
			/// <remarks>
			/// <para>The request is performed by this host, and will be sent to <paramref name="target"/>.</para>
			/// <para>To send a request <i>to</i> this host (from the outside), use the <see cref="Local"/> helper instead.</para>
			/// </remarks>
			public Task<(HttpStatusCode Result, string? Body)> PostTextAsync(DistributedTestComponent target, string pathOnTarget, string body, Encoding? encoding = null, BetterHttpShellOptions? options = null, CancellationToken ct = default)
				=> target == this.Component
					? this.Local.PostTextAsync(pathOnTarget, body, encoding, options, ct)
					: PostTextAsync(target.GetUri(pathOnTarget), body, encoding, options, ct);

			/// <summary>Executes an HTTP POST request from this host to another node, and returns the response body decoded as a string</summary>
			/// <remarks>
			/// <para>The request is performed by this host, and will be sent to the host name referenced in <paramref name="uri"/>.</para>
			/// <para>To send a request <i>to</i> this host (from the outside), use the <see cref="Local"/> helper instead.</para>
			/// </remarks>
			public Task<(HttpStatusCode Result, string? Body)> PostTextAsync(string uri, string body, Encoding? encoding = null, BetterHttpShellOptions? options = null, CancellationToken ct = default)
				=> PostTextAsync(new Uri(uri, UriKind.Absolute), body, encoding, options, ct);

			/// <summary>Executes an HTTP POST request from this host to another node, and returns the response body decoded as a string</summary>
			/// <remarks>
			/// <para>The request is performed by this host, and will be sent to the host name referenced in <paramref name="uri"/>.</para>
			/// <para>To send a request <i>to</i> this host (from the outside), use the <see cref="Local"/> helper instead.</para>
			/// </remarks>
			public async Task<(HttpStatusCode Result, string? Body)> PostTextAsync(Uri uri, string body, Encoding? encoding = null, BetterHttpShellOptions? options = null, CancellationToken ct = default)
			{
				Assert.That(uri, Is.Not.Null);
				ct = ct.CanBeCanceled ? ct : this.Component.Cancellation;
				ct.ThrowIfCancellationRequested();

				EnsureIsExternalUri(uri);
				using var client = this.Component.GetLocalBetterHttpClient(options);

				return await this.Component.ExecuteHttpPostTextAsync(client, uri, body, encoding, options, ct);
			}

			#endregion

			#region JSON...

			/// <summary>Sends an HTTP GET request that expects a JSON-encoded response from this node</summary>
			public Task<(HttpStatusCode Result, JsonObject Body)> GetJsonAsync(DistributedTestComponent target, string pathOnTarget, BetterHttpShellOptions? options = null, CancellationToken ct = default)
				=> target == this.Component
					? this.Local.GetJsonAsync(pathOnTarget, options, ct)
					: GetJsonAsync(target.GetUri(pathOnTarget), options, ct);

			/// <summary>Sends an HTTP GET request that expects a JSON-encoded response from this node</summary>
			public Task<(HttpStatusCode Status, JsonObject Body)> GetJsonAsync(string uri, BetterHttpShellOptions? options = null, CancellationToken ct = default)
				=> GetJsonAsync(new Uri(uri, UriKind.RelativeOrAbsolute), options, ct);

			/// <summary>Sends an HTTP GET request that expects a JSON-encoded response from this node</summary>
			public async Task<(HttpStatusCode Status, JsonObject Body)> GetJsonAsync(Uri uri, BetterHttpShellOptions? options = null, CancellationToken ct = default)
			{
				ct = ct.CanBeCanceled ? ct : this.Component.Cancellation;
				ct.ThrowIfCancellationRequested();

				EnsureIsExternalUri(uri);
				using var client = this.Component.GetBetterHttpClient(uri, options);

				return await this.Component.ExecuteHttpGetJsonAsync(client, uri, options, ct);
			}

			/// <summary>Sends an HTTP POST request with a JSON-encoded request body to this node, and expects a JSON-encoded response.</summary>
			public Task<(HttpStatusCode Result, JsonObject Body)> PostJsonAsync<T>(DistributedTestComponent target, string pathOnTarget, T body, BetterHttpShellOptions? options = null, CancellationToken ct = default)
				=> target == this.Component
					? this.Local.PostJsonAsync(pathOnTarget, body, options, ct)
					: PostJsonAsync(target.GetUri(pathOnTarget), body, options, ct);

			/// <summary>Sends an HTTP POST request with a JSON-encoded request body to this node, and expects a JSON-encoded response.</summary>
			public Task<(HttpStatusCode Status, JsonObject Body)> PostJsonAsync<T>(string uri, T body, BetterHttpShellOptions? options = null, CancellationToken ct = default)
				=> PostJsonAsync<T>(new Uri(uri, UriKind.RelativeOrAbsolute), body, options, ct);

			/// <summary>Sends an HTTP POST request with a JSON-encoded request body to this node, and expects a JSON-encoded response.</summary>
			public async Task<(HttpStatusCode Status, JsonObject Body)> PostJsonAsync<T>(Uri uri, T body, BetterHttpShellOptions? options = null, CancellationToken ct = default)
			{
				ct = ct.CanBeCanceled ? ct : this.Component.Cancellation;
				ct.ThrowIfCancellationRequested();

				EnsureIsExternalUri(uri);
				using var client = this.Component.GetBetterHttpClient(uri, options);

				return await this.Component.ExecuteHttpPostJsonAsync<T>(client, uri, body, options, ct);
			}

			/// <summary>Sends an HTTP POST request with a JSON-encoded request body to this node, and expects a JSON-encoded response.</summary>
			public Task<HttpStatusCode> PutJsonAsync<T>(DistributedTestComponent target, string pathOnTarget, T body, BetterHttpShellOptions? options = null, CancellationToken ct = default)
				=> target == this.Component
					? this.Local.PutJsonAsync(pathOnTarget, body, options, ct)
					: PutJsonAsync(target.GetUri(pathOnTarget), body, options, ct);

			/// <summary>Sends an HTTP PUT request with a JSON-encoded body to this node</summary>
			public Task<HttpStatusCode> PutJsonAsync<T>(string uri, T body, BetterHttpShellOptions? options = null, CancellationToken ct = default)
				=> PutJsonAsync<T>(new Uri(uri, UriKind.RelativeOrAbsolute), body, options, ct);

			/// <summary>Sends an HTTP PUT request with a JSON-encoded body to this node</summary>
			public async Task<HttpStatusCode> PutJsonAsync<T>(Uri uri, T body, BetterHttpShellOptions? options = null, CancellationToken ct = default)
			{
				ct = ct.CanBeCanceled ? ct : this.Component.Cancellation;
				ct.ThrowIfCancellationRequested();

				EnsureIsExternalUri(uri);
				using var client = this.Component.GetBetterHttpClient(uri, options);

				return await this.Component.ExecuteHttpPutJsonAsync(client, uri, body, options, ct);
			}

			#endregion

		}

		public readonly struct HttpLocalHelper
		{

			private readonly DistributedTestComponent Component;

			internal HttpLocalHelper(DistributedTestComponent component)
			{
				this.Component = component;
			}

			#region Binary...

			public async Task<(HttpStatusCode Result, Slice Body)> GetBinaryAsync(string relativePath, BetterHttpShellOptions? options = null, CancellationToken ct = default)
			{
				ct = ct.CanBeCanceled ? ct : this.Component.Cancellation;
				ct.ThrowIfCancellationRequested();

				EnsureIsLocalUri(relativePath);
				var uri = this.Component.GetUri(relativePath);

				using var client = this.Component.GetLocalBetterHttpClient(options);
				return await this.Component.ExecuteHttpGetBinaryAsync(client, uri, ct);
			}

			#endregion

			#region Text...

			public async Task<(HttpStatusCode Result, string? Body)> GetTextAsync(string relativePath, BetterHttpShellOptions? options = null, CancellationToken ct = default)
			{
				ct = ct.CanBeCanceled ? ct : this.Component.Cancellation;
				ct.ThrowIfCancellationRequested();

				EnsureIsLocalUri(relativePath);
				var uri = this.Component.GetUri(relativePath);

				using var client = this.Component.GetLocalBetterHttpClient(options);
				return await this.Component.ExecuteHttpGetTextAsync(client, uri, ct);
			}

			public async Task<(HttpStatusCode Result, string? Body)> PostTextAsync(string relativePath, string body, Encoding? encoding = null, BetterHttpShellOptions? options = null, CancellationToken ct = default)
			{
				ct = ct.CanBeCanceled ? ct : this.Component.Cancellation;
				ct.ThrowIfCancellationRequested();

				EnsureIsLocalUri(relativePath);
				var uri = this.Component.GetUri(relativePath);

				using var client = this.Component.GetLocalBetterHttpClient(options);
				return await this.Component.ExecuteHttpGetTextAsync(client, uri, ct);
			}

			#endregion

			#region JSON...

			/// <summary>Sends an HTTP GET request that expects a JSON-encoded response from this node</summary>
			public async Task<(HttpStatusCode Status, JsonObject Body)> GetJsonAsync(string relativePath, BetterHttpShellOptions? options = null, CancellationToken ct = default)
			{
				ct = ct.CanBeCanceled ? ct : this.Component.Cancellation;
				ct.ThrowIfCancellationRequested();

				EnsureIsLocalUri(relativePath);
				var uri = this.Component.GetUri(relativePath);

				using var client = this.Component.GetBetterHttpClient(uri, options);
				return await this.Component.ExecuteHttpGetJsonAsync(client, uri, options, ct);
			}

			/// <summary>Sends an HTTP POST request with a JSON-encoded request body to this node, and expects a JSON-encoded response.</summary>
			public async Task<(HttpStatusCode Status, JsonObject Body)> PostJsonAsync<T>(string relativePath, T body, BetterHttpShellOptions? options = null, CancellationToken ct = default)
			{
				ct = ct.CanBeCanceled ? ct : this.Component.Cancellation;
				ct.ThrowIfCancellationRequested();

				EnsureIsLocalUri(relativePath);
				var uri = this.Component.GetUri(relativePath);

				using var client = this.Component.GetBetterHttpClient(uri, options);
				return await this.Component.ExecuteHttpPostJsonAsync<T>(client, uri, body, options, ct);
			}

			/// <summary>Sends an HTTP PUT request with a JSON-encoded body to this node</summary>
			public async Task<HttpStatusCode> PutJsonAsync<T>(string relativePath, T body, BetterHttpShellOptions? options = null, CancellationToken ct = default)
			{
				ct = ct.CanBeCanceled ? ct : this.Component.Cancellation;
				ct.ThrowIfCancellationRequested();

				EnsureIsLocalUri(relativePath);
				var uri = this.Component.GetUri(relativePath);

				using var client = this.Component.GetBetterHttpClient(uri, options);
				return await this.Component.ExecuteHttpPutJsonAsync(client, uri, body, options, ct);
			}

			#endregion
		}

		#region Binary...

		private async Task<(HttpStatusCode Status, Slice Body)> ParseBinaryResponse(HttpResponseMessage res)
		{
			var bytes = await res.Content.ReadAsByteArrayAsync(this.Cancellation);
			return (res.StatusCode, bytes.AsSlice());
		}

		/// <summary>Sends an HTTP GET request that expects a binary response from this node</summary>
		[Obsolete("Use host.Http.GetBinaryAsync() instead")]
		public Task<(HttpStatusCode Status, Slice Body)> HttpGetBinaryAsync(string uri, CancellationToken ct = default)
			=> HttpGetBinaryAsync(new Uri(uri, UriKind.RelativeOrAbsolute), ct);

		/// <summary>Sends an HTTP GET request that expects a binary response from this node</summary>
		[Obsolete("Use host.Http.GetBinaryAsync() instead")]
		public async Task<(HttpStatusCode Status, Slice Body)> HttpGetBinaryAsync(Uri uri, CancellationToken ct = default)
		{
			ct = ct.CanBeCanceled ? ct : this.Cancellation;
			ct.ThrowIfCancellationRequested();
			EnsureStarted();

			EnsureIsExternalUri(uri);

			using var client = this.GetBetterHttpClient(uri);
			return await ExecuteHttpGetBinaryAsync(client, uri, ct);
		}

		private async Task<(HttpStatusCode Status, Slice Body)> ExecuteHttpGetBinaryAsync(BetterHttpClient client, Uri uri, CancellationToken ct)
		{
			var req = new HttpRequestMessage(HttpMethod.Get, uri);
			this.Log($"# => GET {req.RequestUri}");

			return await client.SendAsync(req, async query =>
			{
				var res = query.Response;
				this.Log($"# <= {(int) res.StatusCode} {res.ReasonPhrase} ({res.Content.Headers.ContentType}, {res.Content.Headers.ContentLength:N0} bytes)");
				return await this.ParseBinaryResponse(res);
			}, ct);
		}

		#endregion

		#region Text...

		private async Task<(HttpStatusCode Status, string? Body)> ParseTextResponse(HttpResponseMessage res)
		{
			var text  = await res.Content.ReadAsStringAsync(this.Cancellation);
			return (res.StatusCode, text);
		}

		/// <summary>Executes an HTTP GET request from this host to another node, and returns the response body decoded as a string</summary>
		/// <remarks>
		/// <para>The request is performed by this host, and will be sent to the host name referenced in <paramref name="uri"/>.</para>
		/// <para>To send a request <i>to</i> this host (from the outside), use XXX instead.</para>
		/// </remarks>
		[Obsolete("Use host.Http.GetTextAsync() instead")]
		public Task<(HttpStatusCode Status, string? Body)> HttpGetTextAsync(string uri, BetterHttpShellOptions? options = null, CancellationToken ct = default)
			=> HttpGetTextAsync(new Uri(uri, UriKind.Absolute), options, ct);

		/// <summary>Executes an HTTP GET request from this host to another node, and returns the response body decoded as a string</summary>
		/// <remarks>
		/// <para>The request is performed by this host, and will be sent to the host name referenced in <paramref name="uri"/>.</para>
		/// <para>To send a request <i>to</i> this host (from the outside), use XXX instead.</para>
		/// </remarks>
		[Obsolete("Use host.Http.GetTextAsync() instead")]
		public async Task<(HttpStatusCode Status, string? Body)> HttpGetTextAsync(Uri uri, BetterHttpShellOptions? options = null, CancellationToken ct = default)
		{
			ct = ct.CanBeCanceled ? ct : this.Cancellation;
			ct.ThrowIfCancellationRequested();
			EnsureStarted();

			EnsureIsExternalUri(uri);

			using var client = this.GetBetterHttpClient(uri, options);
			return await ExecuteHttpGetTextAsync(client, uri, ct);
		}

		private async Task<(HttpStatusCode Status, string? Body)> ExecuteHttpGetTextAsync(BetterHttpClient client, Uri uri, CancellationToken ct)
		{
			ct = ct.CanBeCanceled ? ct : this.Cancellation;
			ct.ThrowIfCancellationRequested();
			EnsureStarted();

			var req = new HttpRequestMessage(HttpMethod.Get, uri);
			this.Log($"# => GET {req.RequestUri}");

			return await client.SendAsync(req, async query =>
			{
				var res = query.Response;
				this.Log($"# <= {(int) res.StatusCode} {res.ReasonPhrase} ({res.Content.Headers.ContentType}, {res.Content.Headers.ContentLength:N0} bytes)");
				return await this.ParseTextResponse(res);
			}, ct);
		}

		/// <summary>Executes an HTTP POST request from this host to another node, and returns the response body decoded as a string</summary>
		[Obsolete("Use host.Http.PostTextAsync() instead")]
		public Task<(HttpStatusCode Status, string? Body)> HttpPostTextAsync(string uri, string body, Encoding? encoding = null, BetterHttpShellOptions? options = null, CancellationToken ct = default)
			=> HttpPostTextAsync(new Uri(uri, UriKind.Absolute), body, encoding, options, ct);

		/// <summary>Executes an HTTP POST request from this host to another node, and returns the response body decoded as a string</summary>
		[Obsolete("Use host.Http.PostTextAsync() instead")]
		public async Task<(HttpStatusCode Status, string? Body)> HttpPostTextAsync(Uri uri, string body, Encoding? encoding = null, BetterHttpShellOptions? options = null, CancellationToken ct = default)
		{
			encoding ??= Encoding.UTF8;
			ct = ct.CanBeCanceled ? ct : this.Cancellation;
			ct.ThrowIfCancellationRequested();
			EnsureStarted();

			EnsureIsExternalUri(uri);

			using var client = this.GetBetterHttpClient(uri, options);
			return await ExecuteHttpPostTextAsync(client, uri, body, encoding, options, ct);
		}

		private async Task<(HttpStatusCode Status, string? Body)> ExecuteHttpPostTextAsync(BetterHttpClient client, Uri uri, string body, Encoding? encoding = null, BetterHttpShellOptions? options = null, CancellationToken ct = default)
		{
			var req = new HttpRequestMessage(HttpMethod.Post, uri) { Content = new StringContent(body, encoding) };
			this.Log($"# => POST {req.RequestUri}");

			return await client.SendAsync(req, async query =>
			{
				var res = query.Response;
				this.Log($"# <= {(int) res.StatusCode} {res.ReasonPhrase} ({res.Content.Headers.ContentType}, {res.Content.Headers.ContentLength:N0} bytes)");
				return await this.ParseTextResponse(res);
			}, ct);
		}

		#endregion

		#region JSON...

		private async Task<(HttpStatusCode Status, JsonObject Body)> ParseJsonResponse(HttpResponseMessage res)
		{
			var bytes = await res.Content.ReadAsByteArrayAsync(this.Cancellation);
			var body = bytes.Length > 0 ? JsonObject.Parse(bytes) : null;
			return (res.StatusCode, body!);
		}

		private static readonly MediaTypeWithQualityHeaderValue MediaTypeJson = new("application/json", 1);

		/// <summary>Sends an HTTP GET request that expects a JSON-encoded response from this node</summary>
		[Obsolete("Use host.Http.GetJsonAsync() instead")]
		public Task<(HttpStatusCode Status, JsonObject Body)> HttpGetJsonAsync(string uri, BetterHttpShellOptions? options = null, CancellationToken ct = default)
			=> HttpGetJsonAsync(new Uri(uri, UriKind.RelativeOrAbsolute), options, ct);

		/// <summary>Sends an HTTP GET request that expects a JSON-encoded response from this node</summary>
		[Obsolete("Use host.Http.GetJsonAsync() instead")]
		public async Task<(HttpStatusCode Status, JsonObject Body)> HttpGetJsonAsync(Uri uri, BetterHttpShellOptions? options = null, CancellationToken ct = default)
		{
			ct = ct.CanBeCanceled ? ct : this.Cancellation;
			ct.ThrowIfCancellationRequested();
			EnsureStarted();

			using var client = this.GetBetterHttpClient(uri, options);
			return await ExecuteHttpGetJsonAsync(client, uri, options, ct);
		}

		private async Task<(HttpStatusCode Status, JsonObject Body)> ExecuteHttpGetJsonAsync(BetterHttpClient client, Uri uri, BetterHttpShellOptions? options = null, CancellationToken ct = default)
		{
			var req = new HttpRequestMessage(HttpMethod.Get, uri);
			req.Headers.Accept.Add(MediaTypeJson);
			this.Log($"# => GET {req.RequestUri}");

			return await client.SendAsync(req, async query =>
			{
				var res = query.Response;
				this.Log($"# <= {(int) res.StatusCode} {res.ReasonPhrase} ({res.Content.Headers.ContentType}, {res.Content.Headers.ContentLength:N0} bytes)");
				return await this.ParseJsonResponse(res);
			}, ct);
		}

		/// <summary>Sends an HTTP POST request with a JSON-encoded request body to this node, and expects a JSON-encoded response.</summary>
		[Obsolete("Use host.Http.PostJsonAsync() instead")]
		public Task<(HttpStatusCode Status, JsonObject Body)> HttpPostJsonAsync<T>(string uri, T body, BetterHttpShellOptions? options = null, CancellationToken ct = default)
			=> HttpPostJsonAsync<T>(new Uri(uri, UriKind.RelativeOrAbsolute), body, options, ct);

		/// <summary>Sends an HTTP POST request with a JSON-encoded request body to this node, and expects a JSON-encoded response.</summary>
		[Obsolete("Use host.Http.PostJsonAsync() instead")]
		public async Task<(HttpStatusCode Status, JsonObject Body)> HttpPostJsonAsync<T>(Uri uri, T body, BetterHttpShellOptions? options = null, CancellationToken ct = default)
		{
			ct = ct.CanBeCanceled ? ct : this.Cancellation;
			ct.ThrowIfCancellationRequested();
			EnsureStarted();

			using var client = this.GetBetterHttpClient(uri, options);
			return await ExecuteHttpPostJsonAsync<T>(client, uri, body, options, ct);
		}

		/// <summary>Sends an HTTP POST request with a JSON-encoded request body to this node, and expects a JSON-encoded response.</summary>
		private async Task<(HttpStatusCode Status, JsonObject Body)> ExecuteHttpPostJsonAsync<T>(BetterHttpClient client, Uri uri, T body, BetterHttpShellOptions? options = null, CancellationToken ct = default)
		{
			var req = new HttpRequestMessage(HttpMethod.Post, uri)
			{
				Content = CrystalJsonContent.Create(body)
			};
			this.Log($"# => POST {req.RequestUri} ({req.Content.Headers.ContentType}, {req.Content.Headers.ContentLength:N0} bytes)");

			return await client.SendAsync(req, async query =>
			{
				var res = query.Response;
				this.Log($"# <= {(int) res.StatusCode} {res.ReasonPhrase} ({res.Content.Headers.ContentType}, {res.Content.Headers.ContentLength:N0} bytes)");
				return await this.ParseJsonResponse(res);
			}, ct);
		}

		/// <summary>Sends an HTTP PUT request with a JSON-encoded body to this node</summary>
		[Obsolete("Use host.Http.PutJsonAsync() instead")]
		public Task<HttpStatusCode> HttpPutJsonAsync<T>(string uri, T body, BetterHttpShellOptions? options = null, CancellationToken ct = default)
			=> HttpPutJsonAsync<T>(new Uri(uri, UriKind.RelativeOrAbsolute), body, options, ct);

		/// <summary>Sends an HTTP PUT request with a JSON-encoded body to this node</summary>
		[Obsolete("Use host.Http.PutJsonAsync() instead")]
		public async Task<HttpStatusCode> HttpPutJsonAsync<T>(Uri uri, T body, BetterHttpShellOptions? options = null, CancellationToken ct = default)
		{
			ct = ct.CanBeCanceled ? ct : this.Cancellation;
			ct.ThrowIfCancellationRequested();
			EnsureStarted();

			using var client = this.GetBetterHttpClient(uri, options);
			return await ExecuteHttpPutJsonAsync(client, uri, body, options, ct);
		}

		private async Task<HttpStatusCode> ExecuteHttpPutJsonAsync<T>(BetterHttpClient client, Uri uri, T body, BetterHttpShellOptions? options = null, CancellationToken ct = default)
		{
			var req = new HttpRequestMessage(HttpMethod.Put, uri) { Content = CrystalJsonContent.Create(body) };
			this.Log($"# => PUT {req.RequestUri} ({req.Content.Headers.ContentType}, {req.Content.Headers.ContentLength:N0} bytes)");

			return await client.SendAsync(req, query =>
			{
				var res = query.Response;
				this.Log($"# <= {(int) res.StatusCode} {res.ReasonPhrase}");
				return Task.FromResult(res.StatusCode);
			}, ct);
		}

		#endregion

		#endregion

		/// <summary>Dumps all the routes from the web server, using the 'graphviz' format</summary>
		public string DumpRoutes()
		{
			EnsureStarted();
			using var writer = new StringWriter();

			var gw = this.Services.GetRequiredService<DfaGraphWriter>();
			var ds = this.Services.GetRequiredService<EndpointDataSource>();
			gw.Write(ds, writer);
			return writer.ToString();
		}

	}

	[PublicAPI]
	public static class DistributedTestComponentExtensions
	{

		extension(IHostTestBuilder builder)
		{

			/// <summary>Sets the initial log level of this component</summary>
			public void WithLogLevel(LogLevel level)
			{
				if (builder.Component is DistributedTestComponent dtc)
				{
					dtc.WithLogLevel(level);
				}
			}

		}

	}

}
