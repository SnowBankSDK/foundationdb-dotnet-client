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

namespace SnowBank.Testing.Framework
{
	using Microsoft.AspNetCore.SignalR.Client;
	using SnowBank.Networking.Http;
	using SnowBank.Networking.PacketCapture;

	/// <summary>Collection of features attached to a test environment</summary>
	/// <remarks>Note: other API likes test knobs and dynamic parameters are implemented on top of features</remarks>
	[PublicAPI]
	public interface IDistributedTestFeatureCollection
	{

		/// <summary>Retrieve a feature attached to this test environment</summary>
		/// <typeparam name="TFeature">Type of the feature</typeparam>
		/// <param name="feature">Receives the feature if it was found</param>
		/// <returns>Returns <c>true</c> if the feature exists; otherwise, <c>false</c>.</returns>
		bool TryGetFeature<TFeature>([MaybeNullWhen(false)] out TFeature feature);

		/// <summary>Attach a new feature to this test environment</summary>
		/// <typeparam name="TFeature">Type of the feature (must be unique)</typeparam>
		/// <param name="feature">Instance of the feature</param>
		void SetFeature<TFeature>(TFeature feature);

		/// <summary>Test if a feature is declared on this test environment</summary>
		/// <typeparam name="TFeature">Type of the feature</typeparam>
		bool HasFeature<TFeature>();

	}

	/// <summary>Represents the top level builder for a complete test environment</summary>
	[PublicAPI]
	public interface IDistributedTestEnvironmentBuilder : IDistributedTestFeatureCollection
	{

		/// <summary>Token that will be alive for the duration of the test</summary>
		/// <remarks>This token should be used by components who need to spin off background threads or workers that also need to be cancelled when the test is finished.</remarks>
		CancellationToken Lifetime { get; }

		/// <summary>Network Topology of the test environment</summary>
		IVirtualNetworkTopology Topology { get; }

		/// <summary>Sets a custom clock for the test environment</summary>
		IClock Clock { get; set; }

		/// <summary>Add a new network location to the current <see cref="Topology">network topology</see> of the test environment</summary>
		IVirtualNetworkLocation AddLocation(string id, string name, VirtualNetworkType type, Action<IDistributedTestNetworkBuilder> configureHosts, Action<VirtualNetworkLocationOptions>? configureNetwork = null);

		/// <summary>Mappings from a log EventName to a distinct Timeline kind, registered via <see cref="RegisterTimelineEvent"/>.</summary>
		IReadOnlyDictionary<string, TimelineEventRule> TimelineEventRules { get; }

		/// <summary>Registers a mapping from a log <paramref name="eventName"/> (the name of an <c>ILogger</c> <c>EventId</c>) to a
		/// distinct Timeline kind, so a library's specially-tagged trace events are surfaced in the unified journal - captured whenever
		/// emitted (gated only by the logger level), independent of the regular timeline log-level threshold.</summary>
		/// <remarks>This keeps the generic test framework free of any knowledge of a specific library that produces such events.</remarks>
		/// <param name="eventName">The <c>EventId</c> name the producing library tags its events with (e.g. <c>"WireOut"</c>).</param>
		/// <param name="category">The journal kind to record these events under (e.g. <c>"MSG"</c>, <c>"FDB"</c>).</param>
		/// <param name="formatLabel">Optional formatter turning the log message into the journal label; defaults to the message.</param>
		void RegisterTimelineEvent(string eventName, string category, Func<string?, string>? formatLabel = null);

		/// <summary>Hooks invoked when a test completes (before its hosts are torn down), registered via <see cref="OnTestCompleted"/>.</summary>
		IReadOnlyList<DistributedTestCompletedHook> TestCompletedHooks { get; }

		/// <summary>Registers a hook invoked when a test completes, BEFORE the hosts are torn down (their services can still be resolved).</summary>
		/// <remarks>
		/// <para>This is how a library's test base attaches its own diagnostics to the test lifecycle without this generic
		/// framework knowing about the library (the lifecycle analog of <see cref="RegisterTimelineEvent"/>): the typical hook
		/// dumps library-specific state when <see cref="DistributedTestOutcome.Failed"/> is set.</para>
		/// <para>A hook that throws is reported to the error output but can never mask the outcome of the test itself.</para>
		/// </remarks>
		void OnTestCompleted(DistributedTestCompletedHook hook);

	}

	/// <summary>Outcome of a completed distributed test, as seen by a <see cref="DistributedTestCompletedHook"/></summary>
	/// <param name="Failed">Whether the test failed (at least one assertion failure or error)</param>
	/// <param name="FailCount">Number of failures reported by the test runner</param>
	/// <param name="AssertCount">Number of assertions executed by the test</param>
	[PublicAPI]
	public readonly record struct DistributedTestOutcome(bool Failed, int FailCount, int AssertCount);

	/// <summary>Hook invoked when a distributed test completes, before its hosts are torn down</summary>
	/// <param name="context">Live test context: the hosts are still up, so their services can be resolved for a final dump</param>
	/// <param name="outcome">Outcome of the test (a typical hook only acts when <see cref="DistributedTestOutcome.Failed"/> is set)</param>
	/// <param name="ct">Token bounding the time budget allowed to the hooks</param>
	[PublicAPI]
	public delegate ValueTask DistributedTestCompletedHook(IDistributedTestContext context, DistributedTestOutcome outcome, CancellationToken ct);

	/// <summary>Represents a single network environment</summary>
	/// <remarks>All test components in this build will share the same "virtual" network</remarks>
	[PublicAPI]
	public interface IDistributedTestNetworkBuilder : IDistributedTestFeatureCollection
	{

		/// <summary>Represents the global test environment (common to all networks)</summary>
		IDistributedTestEnvironmentBuilder Top { get; }

		/// <summary>Virtual network that is used by this builder</summary>
		IVirtualNetworkLocation Location { get; }

		/// <summary>Add a new test component to the current virtual network</summary>
		IDistributedTestComponent RegisterComponent(IDistributedTestComponent component);

		/// <summary>Declares a virtual load balancer on this network: a public endpoint (<paramref name="alias"/>) that routes each incoming connection to one of several backend hosts, under full test control</summary>
		/// <param name="id">Identifier of the balancer (e.g. <c>"CLUSTER"</c>), used to retrieve it later via the topology</param>
		/// <param name="alias">Public name the clients connect to (e.g. <c>"cluster.lan.simulated"</c>)</param>
		/// <param name="backends">Ids of the backend hosts (they may be declared later in the setup; resolution is lazy)</param>
		/// <remarks>Routing is deterministic and test-driven: see <see cref="VirtualLoadBalancer.Route"/>, <see cref="VirtualLoadBalancer.ForceNextTarget"/> and <see cref="VirtualLoadBalancer.UseSelector"/>.</remarks>
		VirtualLoadBalancer WithLoadBalancer(string id, string alias, params string[] backends);

	}

	/// <summary>Container for a global Test Knob</summary>
	[PublicAPI]
	public sealed record TestKnob
	{

		public TestKnob(string key, object? value)
		{
			this.Key = key;
			this.Value = value;
		}

		/// <summary>Id of the knob</summary>
		public string Key { get; }

		/// <summary>Value of the knob</summary>
		public object? Value { get; set; }

		/// <summary>Gets the value of this know</summary>
		public TValue Get<TValue>() => this.Value is null ? default! : (TValue) this.Value;

	}

	/// <summary>Represents a virtualized host that exists inside a test environment</summary>
	public interface IDistributedTestComponent: IAsyncDisposable
	{

		string Id { get; }

		/// <summary>Pre-processes any network or initial configuration for this component</summary>
		/// <remarks>This is called once for all components, so that they can acquire their network identity, and collect details about other components nearby</remarks>
		ValueTask Prepare(IDistributedTestContext context, CancellationToken startToken);

		/// <summary>Initializes all the services and objects that will be used by this component</summary>
		/// <remarks>This is where each component can build a dedicated <see cref="IServiceProvider">service provider</see> and initializes all objects and singletons required.</remarks>
		ValueTask Init(CancellationToken startToken);

		/// <summary>Starts any background service, process or thread that will be required by this component</summary>
		/// <remarks>Ideally, the method should wait until the component is fully "ready", in order to make life easier for the test methods.</remarks>
		ValueTask Start(CancellationToken startToken);

		/// <summary>Stops any background service, process, thread or pending request managed by this component</summary>
		/// <remarks>The component should make sure that nothing remains running, because it could interfere with the next test method</remarks>
		ValueTask Stop(CancellationToken stopToken);

		/// <summary>Gets a required object or service from the DI container of this component</summary>
		/// <typeparam name="TService">Type of service requested</typeparam>
		/// <returns>Instance for this service, or an exception if the service is not defined, or if the component is not in the running state.</returns>
		/// <remarks>This can only be called after <see cref="Init"/> has completed, and before <see cref="Stop"/> is invoked.</remarks>
		TService GetRequiredService<TService>() where TService : notnull;

		/// <summary>Global test context that is managing this host</summary>
		IDistributedTestContext Context { get; }

		/// <summary>Map of the network, as seen by this virtual host</summary>
		IVirtualNetworkMap NetworkMap { get; }

		/// <summary>Network location that is used by this component</summary>
		IVirtualNetworkLocation Location { get; }

		/// <summary>Flag that is <see langword="true"/> when this host is 'offline' and should not respond to any external request.</summary>
		bool Offline { get; }

		/// <summary>Changes the <see cref="Offline">offline state</see> of this host</summary>
		/// <param name="offline">if <see langword="true"/>, the host behind this component will be marked as offline and start rejected any incoming requests. If <see langword="false"/>, the host will become available again.</param>
		void SetOffline(bool offline);

		/// <summary>List of subcomponents (processes, services, browsers, ...) running under this virtual host</summary>
		IReadOnlyList<IDistributedTestComponent> SubComponents { get; }

	}

	[PublicAPI]
	public interface IDistributedWebTestComponent : IDistributedTestComponent
	{
		Uri GetUri(string? path = null);

		#region BetterHttpClient...

		// the shell-based doors below still return the legacy BetterHttpClient/BetterHttpShellOptions surface for
		// consumers that have not migrated to a factory-door client with the SendAsync extensions.
#pragma warning disable CS0618

		/// <summary>Returns an HTTP client that will talk to this virtual host</summary>
		/// <param name="options">Options used to configure the HTTP client</param>
		/// <returns>Client that will be setup to execute requests <i>locally</i> from the host to itself, bypassing any injected errors or network connectivity issues.</returns>
		BetterHttpClient GetLocalBetterHttpClient(BetterHttpShellOptions? options = null);

		/// <summary>Returns an HTTP client that will talk to the specified host or address</summary>
		/// <param name="remote">Remote host</param>
		/// <param name="options">Options used to configure the HTTP client</param>
		/// <returns>Client that will be setup to execute requests <i>from</i> the current host, <i>to</i> the remote host, while emulating any injected errors or network connectivity issues.</returns>
		BetterHttpClient GetBetterHttpClient(IDistributedWebTestComponent remote, BetterHttpShellOptions? options = null);

		/// <summary>Returns an HTTP client that will talk to the specified host or address</summary>
		/// <param name="hostOrAddress">Address of the remote host (note: only the hostname part of the URI is used)</param>
		/// <param name="options">Options used to configure the HTTP client</param>
		/// <returns>Client that will be setup to execute requests <i>from</i> the current host, <i>to</i> the remote host, while emulating any injected errors or network connectivity issues.</returns>
		BetterHttpClient GetBetterHttpClient(Uri hostOrAddress, BetterHttpShellOptions? options = null);

#pragma warning restore CS0618

		#endregion

		#region RestHttp...

		/// <summary>Returns a REST http client that will talk to this virtual host</summary>
		RestHttpProtocol GetLocalRestClient(Action<RestHttpClientOptions>? configure = null);

		/// <summary>Returns a REST http client that will talk to the specified remote host</summary>
		RestHttpProtocol GetRestClient(IDistributedWebTestComponent remote, Action<RestHttpClientOptions>? configure = null);


		/// <summary>Returns a REST http client that will talk to the specified remote host</summary>
		RestHttpProtocol GetRestClient(Uri hostOrAddress, Action<RestHttpClientOptions>? configure = null);

		#endregion

		#region SignalR...

		IHubConnectionBuilder GetHubConnectionBuilder(string path, Func<Task<string?>>? accessTokenProvider = null);

		IHubConnectionBuilder GetHubConnectionBuilder(IDistributedWebTestComponent remote, string path, Func<Task<string?>>? accessTokenProvider = null);

		#endregion

	}

	/// <summary>Represents the execution context of a test environment</summary>
	[PublicAPI]
	public interface IDistributedTestContext : IDistributedTestFeatureCollection
	{

		CancellationTokenSource Lifetime { get; }

		TComponent GetComponent<TComponent>(string id) where TComponent : IDistributedTestComponent;

		IEnumerable<TComponent> FindComponents<TComponent>(Func<TComponent, bool>? predicate = null)
			where TComponent : IDistributedTestComponent;

		/// <summary>Simulated Clock</summary>
		/// <remarks>
		/// <para>This clock can be controlled by the test</para>
		/// <para>>This instance will be injected to all the simulated nodes, and should be used for actions that happen "inside" the simulated nodes.</para>
		/// </remarks>
		IClock Clock { get; }

		/// <summary>Real Clock</summary>
		/// <remarks>
		/// <para>This is the system clock of the host that runs the test.</para>
		/// <para>This instance is able to measure actual elapsed time, and should only be used for actions that happen "outside" of the simulated nodes.</para>
		/// </remarks>
		IClock RealClock { get; }

		/// <summary>Timeline of all the events that occured during the test execution</summary>
		Timeline Timeline { get; }

		/// <summary>Mappings from a log EventName to a distinct Timeline kind (registered by libraries via the environment builder).</summary>
		IReadOnlyDictionary<string, TimelineEventRule> TimelineEventRules { get; }

		/// <summary>Hooks invoked when the test completes, before its hosts are torn down (registered by libraries via the environment builder).</summary>
		IReadOnlyList<DistributedTestCompletedHook> TestCompletedHooks { get; }

		/// <summary>Instant when the test environment was created (but not started)</summary>
		Instant CreatedAt { get; }

		/// <summary>Instant when the test run was started</summary>
		Instant StartedAt { get; }

		/// <summary>Instant when the test run was complete</summary>
		Instant CompletedAt { get; }

		/// <summary>Describes the topology of the simulated networks that are part of the test environment</summary>
		IVirtualNetworkTopology Topology { get; }

		void EmitNetworkPackets(ReadOnlyMemory<CapturedPacket> packets);

		/// <summary>Returns a list of all the network packets that were captured</summary>
		List<CapturedPacket> GetNetworkPackets(Func<CapturedPacket, bool>? filter = null);

		DistributedTest TestSubject { get; }

		void Log(string? message = null);
		void Log(ref DefaultInterpolatedStringHandler handler);

		/// <summary>Output that can be used to write any log or debug data</summary>
		TextWriter LogOutput { get; }

		/// <summary>Output that can be used to write any error or warning</summary>
		TextWriter LogOutputError { get; }

	}

	[PublicAPI]
	public interface IHostTestBuilder
	{

		/// <summary>Id of the component being configured</summary>
		string Id { get; }

		/// <summary>The test component being configured</summary>
		IDistributedTestComponent Component { get; }

		/// <summary>The network builder that will host this component</summary>
		IDistributedTestNetworkBuilder Parent { get; }

		/// <summary>Global test context</summary>
		IDistributedTestContext Context { get; }

		/// <summary>Network identity of the host that will run this component</summary>
		VirtualHostIdentity Identity { get; }

		/// <summary>Adds a subcomponent to this component</summary>
		/// <param name="component">Component that will run on the same virtual host</param>
		void AddSubComponent(IDistributedTestComponent component);

	}

}
