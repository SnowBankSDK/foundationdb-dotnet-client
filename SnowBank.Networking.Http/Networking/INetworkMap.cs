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

namespace SnowBank.Networking
{
	using System.ComponentModel;
	using System.Net.NetworkInformation;
	using System.Net.Sockets;
	using SnowBank.Networking.Http;

	/// <summary>Provides services to interact with the real (or simulated) network</summary>
	/// <remarks>
	/// <para>This type is a wrapper that allows injecting virtual networks and hosts when the code under test and running inside a "virtual sandbox"</para>
	/// </remarks>
	[PublicAPI]
	public interface INetworkMap
	{

		/// <summary>Clock used to generate timestamps or measure elapsed time</summary>
		IClock Clock { get; }

		[Obsolete("Use CreateBetterHttpHandler instead")]
		HttpMessageHandler? CreateHttpHandler(string hostOrAddress, int port);

		/// <summary>Creates a new <see cref="HttpMessageHandler"/> that can send queries to the specified remote host</summary>
		/// <param name="baseAddress">Base address of the host (ex: <c>"https://api.acme.com"</c>), without any path or querystring</param>
		/// <param name="options">Configuration options for the client</param>
		/// <returns>Handler that will be configured for the remote host</returns>
		/// <remarks>In a test environment, can return a virtual handler that will emulate the HTTP request "in process" to the corresponding virtual host</remarks>
		HttpMessageHandler CreateBetterHttpHandler(Uri baseAddress, BetterHttpClientOptions options);

		/// <summary>Resolves the public IP address of the specified target, as seen by the local host</summary>
		/// <param name="hostNameOrAddress">Host name of IP address</param>
		/// <param name="ct">Token used to cancel the request</param>
		/// <returns>The resolved IP address of the host, the address if it is already resolved, or <see langword="null"/> if the resolution failed</returns>
		// ReSharper disable once InconsistentNaming
		ValueTask<IPAddress?> GetPublicIPAddressForHost(string hostNameOrAddress, CancellationToken ct);

		/// <summary>Performs a DNS resolution for the specified host name</summary>
		/// <param name="hostNameOrAddress">Host name or IP address</param>
		/// <param name="family">Type of DNS record (A, AAA, ...)</param>
		/// <param name="ct">Token used to cancel the request</param>
		/// <returns>Result of the DNS resolution</returns>
		Task<IPHostEntry> DnsLookup(string hostNameOrAddress, AddressFamily? family, CancellationToken ct);

		/// <summary>Lists all network adapter present on this host</summary>
		IReadOnlyList<NetworkAdaptorDescriptor> GetNetworkAdaptors();

		/// <summary>Queries metadata about the <see cref="EndPointQuality">quality</see> of an endpoint</summary>
		/// <return>Quality of the endpoint, or null if no connection attempts has been performed yet</return>
		EndPointQuality? GetEndpointQuality(IPEndPoint endpoint);

		/// <summary>Records a connection attempt to a specific endpoint</summary>
		/// <param name="endpoint">Target endpoint of the connection</param>
		/// <param name="duration">Duration of the connection attempt</param>
		/// <param name="error">Error encountered while attempting the connection, or <c>null</c> if it was a success</param>
		/// <param name="hostName"></param>
		/// <param name="localAddress">Local address used to connect to this host (if known)</param>
		/// <returns>Updated quality of the endpoint</returns>
		EndPointQuality RecordEndpointConnectionAttempt(IPEndPoint endpoint, TimeSpan duration, Exception? error, string? hostName, IPAddress? localAddress);

		/// <summary>Tries to get details about which endpoint and local address was used to talk to the specific host</summary>
		bool TryGetLastEndpointForHost(string hostName, out (IPEndPoint? EndPoint, IPAddress? LocalAddress, Instant Timestamp) infos);

	}

	/// <summary>Details about a network adapter</summary>
	public sealed record NetworkAdaptorDescriptor
	{
		/// <summary>Identifier of the adapter</summary>
		public required string Id { get; init; }

		/// <summary>Index of the adapter</summary>
		public required int Index { get; init; }

		/// <summary>Type of network adapter</summary>
		public required NetworkInterfaceType Type { get; init; }

		/// <summary>Name of the adapter</summary>
		public required string Name { get; init; }

		/// <summary>Description of the adapter</summary>
		public required string Description { get; init; }

		/// <summary>MAC address of the adapter (if applicable)</summary>
		public string? PhysicalAddress { get; init; }

		/// <summary>DNS suffix for this adapter (if applicable)</summary>
		public string? DnsSuffix { get; init; }

		/// <summary>List of unicast addresses bound to this adapter (or empty if not applicable)</summary>
		public required UnicastAddressDescriptor[] UnicastAddresses { get; init; }

		/// <summary>Estimated network speed of this adapter (if known)</summary>
		public long? Speed { get; init; }

		public sealed record UnicastAddressDescriptor
		{

			public required IPAddress Address { get; init; }

			// ReSharper disable once InconsistentNaming
			public IPAddress? IPv4Mask { get; init; }

			public int PrefixLength { get; init; }

		}

	}

	/// <summary>Map of the global network topology</summary>
	/// <remarks>Lists all the virtual network locations and how they are interconnected</remarks>
	[PublicAPI]
	public interface IVirtualNetworkTopology
	{

		/// <summary>Registers a new virtual network location</summary>
		/// <param name="id">Unique identifier for this location</param>
		/// <param name="name">Friendly name</param>
		/// <param name="type">Type of network location</param>
		/// <param name="options">Configuration options for this location</param>
		/// <returns>Instance that represents this new location</returns>
		IVirtualNetworkLocation RegisterLocation(string id, string name, VirtualNetworkType type, VirtualNetworkLocationOptions options);

		/// <summary>Gets the network location with the specified identifier</summary>
		/// <param name="id">Id of the network location</param>
		/// <returns>The corresponding <see cref="IVirtualNetworkLocation"/> if it exists, or an exception if it does not.</returns>
		/// <exception cref="InvalidOperationException">If there is no network location with the given <paramref name="id"/></exception>
		IVirtualNetworkLocation GetLocation(string id);

		/// <summary>Gets the host with the specified identifier</summary>
		IVirtualNetworkHost GetHost(string id);

		/// <summary>Simulates a DNS resolution request for the specified host name or address</summary>
		/// <param name="hostNameOrAddress">Host name, or IP address</param>
		/// <param name="sourceHost">If specified, the virtual host that is performing the request (which may influence the result if the host cannot see the target)</param>
		/// <param name="family">Type of DNS record (A, AAA, etc...)</param>
		/// <param name="ct">Cancellation token</param>
		/// <returns>Simulated response from the virtual DNS server</returns>
		Task<IPHostEntry> DnsResolve(string hostNameOrAddress, IVirtualNetworkHost? sourceHost, AddressFamily? family, CancellationToken ct);

		/// <summary>Generates a human-readable description of the network topology, for logging/troubleshooting purpose</summary>
		/// <remarks>The generated string may contain multiple lines, and end with a carriage return.</remarks>
		string Dump();

	}

	/// <summary>Represents the view of the network, relative to a specific host</summary>
	[PublicAPI]
	public interface IVirtualNetworkMap : INetworkMap
	{
		/// <summary>Network topology</summary>
		IVirtualNetworkTopology Topology { get; }

		/// <summary>Local virtual host</summary>
		IVirtualNetworkHost Host { get; }

		/// <summary>Finds a virtual host, given its host name or IP address, as understood from the current host</summary>
		/// <param name="hostOrAddress">Host name, FQDN or IP address of the host in the virtual network</param>
		/// <returns>Corresponding host, or <see langword="null"/> if not found (or not in the same context as the current host)</returns>
		IVirtualNetworkHost? FindHost(string hostOrAddress);

		/// <summary>Resolves the public IP address of the specified target, as seen by the local host</summary>
		/// <param name="target">Remote target that we will attempt to reach other the network</param>
		/// <returns>The IP address of the local host that would be used to connect to <paramref name="target"/> other the simulated network</returns>
		/// <remarks>
		/// <para>If <paramref name="target"/> is the same as the local host, this method will return <see cref="IPAddress.Loopback"/>.</para>
		/// <para>If the local host only has one "virtual NIC", the main address of this adapter will be used.</para>
		/// <para>Please note that this method does not consider complex routing between virtual networks (NAT, reverse proxy, load balancers, ...).</para>
		/// </remarks>
		// ReSharper disable once InconsistentNaming
		IPAddress? GetPublicIPAddressForHost(IVirtualNetworkHost target);

	}

	/// <summary>Configuration options for a <see cref="IVirtualNetworkLocation">virtual network location</see></summary>
	public record VirtualNetworkLocationOptions
	{
		public string? IpRange { get; set; }
		//TODO: how to model ip ranges?

		public string? DnsSuffix { get; set; }

		public bool AllowsIncoming { get; set; } = true;

		public bool AutoDhcp { get; set; } = true;

	}

	/// <summary>Type of virtual test network</summary>
	public enum VirtualNetworkType
	{
		Unspecified = 0,
		/// <summary>Localhost (127.0.0.0/8)</summary>
		Loopback,
		/// <summary>Local Area Network</summary>
		LocalNetwork,
		/// <summary>Servers in a private datacenter or private cloud</summary>
		DataCenter,
		/// <summary>Servers in a public cloud</summary>
		Cloud,
	}

	/// <summary>Defines the identity of a virtual host in a virtual test network</summary>
	public sealed record VirtualHostIdentity
	{

		/// <summary>Name of the host (ex: <c>"PC042"</c>)</summary>
		public string? HostName { get; set; }

		/// <summary>Domain of the host (ex: <c>"acme.local"</c>)</summary>
		public string? DnsSuffix { get; set; }

		/// <summary>Fully qualified domain name of the host (ex: <c>"pc042.acme.local"</c>)</summary>
		public string? Fqdn { get; set; }

		/// <summary>List of aliases for this host (optional)</summary>
		public List<string> Aliases { get; set; } = new List<string>();

		/// <summary>List of IP addresses of this host</summary>
		public List<IPAddress> Addresses { get; set; } = new List<IPAddress>();

		/// <summary>If <see langword="true"/>, this is an actual physical device on the network, and requests will be sent over the network. If <see langword="false"/>, this is a virtualized host.</summary>
		public bool PassthroughToPhysicalNetwork { get; set; }

		/// <summary>If <see langword="true"/>, the host should start in the 'offline' state.</summary>
		public bool StartAsOffline { get; set; }

	}

	/// <summary>Represents a virtual network location, such as "LAN", a Cloud DMZ, or the Loopback interface, as well as all its services (DNS, DHCP, ...).</summary>
	/// <remarks>
	/// <para>Hosts belonging to the same network location can talk to each other directly.</para>
	/// <para>Hosts from different network locations may require virtual routing to be configured</para>
	/// </remarks>
	[PublicAPI]
	public interface IVirtualNetworkLocation : IEquatable<IVirtualNetworkLocation>
	{

		/// <summary>Global network topology</summary>
		/// <remarks>Describes how the different virtual network locations are interconnected</remarks>
		IVirtualNetworkTopology Topology { get; }

		/// <summary>Unique id of this network location</summary>
		string Id { get; }

		/// <summary>Friendly name of this network location</summary>
		string Name { get; }

		/// <summary>Type of network location</summary>
		VirtualNetworkType Type { get; }

		/// <summary>Configuration options for this network location</summary>
		VirtualNetworkLocationOptions Options { get; }

		/// <summary>Registers a new host to this network location</summary>
		/// <remarks>Should only be called by the test framework, during the setup phase</remarks>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		IVirtualNetworkMap RegisterHost(string id, VirtualHostIdentity identity);

		/// <summary>Returns the host with the given id, from this location.</summary>
		IVirtualNetworkHost? GetHost(string id);

		/// <summary>Registers a static IP address with the virtual DHCP server of this location</summary>
		/// <param name="address">IP address that is marked as "in use"</param>
		/// <remarks>This address will not be assigned by the virtual DHCP</remarks>
		void RegisterIpAddress(IPAddress address);

		/// <summary>Allocates a new IP address using the virtual DHCP server of this location</summary>
		IPAddress AllocateIpAddress();

		/// <summary>Allocates a new MAC address on this network location</summary>
		/// <param name="ouiPrefix">First 24 bits of the MAC address, in the format <c>"XX:XX:XX"</c> (with X = hex digit)</param>
		/// <param name="seed">Optional seed used to generate a deterministic address (like the host name, its IP address, etc...). If <see langword="null"/>, a randomly generated seed will be used instead.</param>
		/// <returns>A newly generated MAC address, in the format <c>"XX:XX:XX:YY:YY:YY"</c> with X coming from <paramref name="ouiPrefix"/> and Y being generated from the <paramref name="seed"/></returns>
		/// <example><c>AllocateMacAddress("12:34:56") => "12:34:56:C0:FF:EE"</c></example>
		string AllocateMacAddress(string ouiPrefix, string? seed = null);

		/// <summary>Generates a new virtual Serial Number from a pattern</summary>
		/// <param name="pattern">Pattern where '#' are replaced with digits and '?' are replaced with uppercase letters (ex: 'ABC###??#####Z')</param>
		/// <param name="seed">Deterministic seed, or null to generate a truly random address</param>
		/// <returns>Serial number (ex: (ex: 'ABC666XY42069Z')</returns>
		string AllocateSerialNumber(string pattern, string? seed = null);

		/// <summary>Tests if this virtual network can send packets to a <paramref name="target"/> network</summary>
		bool CanSendTo(IVirtualNetworkLocation target);

		/// <summary>Tests if this virtual network can receive packets from a <paramref name="source"/> network</summary>
		bool CanReceiveFrom(IVirtualNetworkLocation source);

		/// <summary>Registers a new service available in this virtual network</summary>
		/// <param name="serviceType">Type of service (ex: "endpoint:wes")</param>
		/// <param name="componentId">Id of the test component that handles this service (ex: "SMITH")</param>
		/// <param name="argument">Optional argument that depends on the type of service (ex: "agents:SMITH:wes:uri")</param>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		void RegisterNetworkService(string serviceType, string componentId, string? argument);

		/// <summary>Returns of list of hosts that support a given service type in this virtual network</summary>
		/// <param name="serviceType">Type of service (ex: "endpoint:wes")</param>
		/// <returns>List of matching hosts, or an empty array if none were found</returns>
		(string Id, string? Argument)[] BrowseNetworkService(string serviceType);

		/// <summary>Opens a door to the real world by allowing hosts in this virtual network, to talk to a real physical host.</summary>
		/// <param name="id"></param>
		/// <param name="identity"></param>
		/// <returns>
		/// <para>This should _only_ be used to interfact with resources that <i>cannot</i> be virtualized.</para>
		/// <para>Tests that use this feature relly on the availability of this resources and probably cannot be run in parallel or in a different environment.</para>
		/// </returns>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		IVirtualNetworkHost AddHostPassthrough(string id, VirtualHostIdentity identity);

	}

	/// <summary>Represents a virtual host</summary>
	[PublicAPI]
	public interface IVirtualNetworkHost : IEquatable<IVirtualNetworkHost>
	{

		/// <summary>List of virtual network adapters available to this host</summary>
		IReadOnlyList<IVirtualNetworkAdapter> Adapters { get; }

		/// <summary>List or virtual network locations that his host can reach</summary>
		IReadOnlyList<IVirtualNetworkLocation> Locations { get; }

		/// <summary>Virtual loopback network adapter</summary>
		IVirtualNetworkLocation? Loopback { get; }

		/// <summary>Unique identifier of this host</summary>
		string Id { get; }

		/// <summary>Fully qualified domain name (fqdn) of this host (ex: pc123.domain.local)</summary>
		/// <remarks>By convention, should be lowercase.</remarks>
		string Fqdn { get; }

		/// <summary>Primary host name, excluding the domain (ex: PC123)</summary>
		/// <remarks>By convention, should be UPPERCASE.</remarks>
		string HostName { get; }

		/// <summary>Additional names for this host (excluding the primary host name and fqdn)</summary>
		string[] Aliases { get; }

		/// <summary>Static ip address (optional)</summary>
		/// <remarks>By convention, IP addresses in the range 83.73.77.0/24 are used for simulated hosts, in order to ensure that no real packet "escapes" from the test context to a real network.</remarks>
		IPAddress[] Addresses { get; }

		/// <summary>Binds a virtual port on one of the network locations of this host</summary>
		/// <param name="location">Location corresponding to the bound ip address</param>
		/// <param name="port">TCP Port number</param>
		/// <param name="handler">Handler called for any connection to this port from this network location</param>
		void Bind(IVirtualNetworkLocation location, int port, Func<HttpMessageHandler> handler);

		/// <summary>Finds an HTTP connection handler that is listening on the specified port number and network location</summary>
		/// <param name="location">Network location from which the request is coming from</param>
		/// <param name="port">TCP port number of the connection</param>
		/// <returns>Handler that can respond to the HTTP request, if found; otherwise, <see langword="null"/></returns>
		Func<HttpMessageHandler>? FindHandler(IVirtualNetworkLocation location, int port);

		/// <summary>Flag that is <see langword="true"/> when this host is 'offline' and should not respond to any external request.</summary>
		bool Offline { get; }

		/// <summary>Changes the <see cref="Offline">offline state</see> of this host</summary>
		/// <param name="offline">Mark the host as offline if <see langword="true"/>, or back online if <see langword="false"/>.</param>
		/// <remarks>If the host is offline, all simulated requests will start to fail</remarks>
		void SetOffline(bool offline);

	}

	/// <summary>Represents a virtual network adapter that is used by a <see cref="IVirtualNetworkHost">virtual host</see> to talk to a <see cref="IVirtualNetworkLocation"/> virtual network location</summary>
	[PublicAPI]
	public interface IVirtualNetworkAdapter
	{

		/// <summary>Virtual network location which is reachable by this network adapter</summary>
		IVirtualNetworkLocation Location { get; }

		/// <summary>Id of this network adapter (locally unique)</summary>
		string Id { get; }

		/// <summary>Type of network adapter</summary>
		NetworkInterfaceType Type { get; }

		/// <summary>Friendly name of this network adapter (locally unique)</summary>
		string Name { get; }

		/// <summary>Description of this network adapter</summary>
		string Description { get; }

		/// <summary>List of unicast addresses assigned to this network adapter</summary>
		(IPAddress Address, IPAddress Mask, int PrefixLength)[] UnicastAddresses { get; }

	}

	[PublicAPI]
	public static class NetworkMapExtensions
	{

		/// <summary>Creates and registers a new loopback adapter for the specified virtual host</summary>
		public static IVirtualNetworkLocation RegisterLoopbackNetwork(this IVirtualNetworkTopology topology, string id, string name)
		{
			return topology.RegisterLocation(
				id,
				name,
				VirtualNetworkType.LocalNetwork,
				new VirtualNetworkLocationOptions()
				{
					AllowsIncoming = false,
					IpRange = "127.0.0.0/8",
					DnsSuffix = ".localhost"
				}
			);
		}

	}

}
