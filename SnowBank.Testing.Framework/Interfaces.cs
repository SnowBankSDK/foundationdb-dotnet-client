#region Copyright (c) 2023-2026 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
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

	}

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

		/// <summary>Returns an HTTP client that will talk to this virtual host</summary>
		/// <param name="options">Options used to configure the HTTP client</param>
		/// <returns>Client that will be setup to execute requests <i>locally</i> from the host to itself, bypassing any injected errors or network connectivity issues.</returns>
		BetterHttpClient GetLocalBetterHttpClient(BetterHttpClientOptions? options = null);

		/// <summary>Returns an HTTP client that will talk to the specified host or address</summary>
		/// <param name="remote">Remote host</param>
		/// <param name="options">Options used to configure the HTTP client</param>
		/// <returns>Client that will be setup to execute requests <i>from</i> the current host, <i>to</i> the remote host, while emulating any injected errors or network connectivity issues.</returns>
		BetterHttpClient GetBetterHttpClient(IDistributedWebTestComponent remote, BetterHttpClientOptions? options = null);

		/// <summary>Returns an HTTP client that will talk to the specified host or address</summary>
		/// <param name="hostOrAddress">Address of the remote host (note: only the hostname part of the URI is used)</param>
		/// <param name="options">Options used to configure the HTTP client</param>
		/// <returns>Client that will be setup to execute requests <i>from</i> the current host, <i>to</i> the remote host, while emulating any injected errors or network connectivity issues.</returns>
		BetterHttpClient GetBetterHttpClient(Uri hostOrAddress, BetterHttpClientOptions? options = null);

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
