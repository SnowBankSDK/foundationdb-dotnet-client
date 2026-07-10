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

#if NET8_0_OR_GREATER

namespace SnowBank.Networking
{
	using System.ComponentModel;
	using System.Net;
	using System.Net.Http;
	using System.Net.NetworkInformation;
	using System.Net.Sockets;
	using SnowBank.Networking.Http;

	/// <summary>Default implementation of a <see cref="IVirtualNetworkMap">virtual network map</see>, as seen from a <see cref="IVirtualNetworkHost">virtual host</see></summary>
	/// <remarks>There should be one instance of this map per virtual host.</remarks>
	public class VirtualNetworkMap : NetworkMap, IVirtualNetworkMap
	{

		public VirtualNetworkMap(VirtualNetworkTopology topology, VirtualNetworkTopology.SimulatedHost host)
		{
			Contract.NotNull(topology);
			Contract.NotNull(host);

			this.Topology = topology;
			this.Host = host;
		}

		/// <summary>Topology of the virtualized network</summary>
		public VirtualNetworkTopology Topology { get; }

		/// <inheritdoc />
		IVirtualNetworkTopology IVirtualNetworkMap.Topology => this.Topology;

		/// <summary>Local virtual host</summary>
		public VirtualNetworkTopology.SimulatedHost Host { get; }

		/// <inheritdoc />
		IVirtualNetworkHost IVirtualNetworkMap.Host => this.Host;

		/// <inheritdoc />
		IVirtualNetworkHost? IVirtualNetworkMap.FindHost(string hostOrAddress) => FindHost(hostOrAddress);

		/// <summary>Lookup the Virtual Host that correspond to the given hostname or IP address</summary>
		/// <param name="hostOrAddress">Host name, or IP address of the host.</param>
		/// <returns>Corresponding <see cref="VirtualNetworkTopology.SimulatedHost"/>, or <c>null</c> if no match was found.</returns>
		/// <exception cref="InvalidOperationException">If there was no match for a hostname that ends with <c>".simulated"</c>, or an IP address in the range <c>83.73.77.0/24</c>.</exception>
		public VirtualNetworkTopology.SimulatedHost? FindHost(string hostOrAddress)
		{
			if (IPAddress.TryParse(hostOrAddress, out var ip))
			{
				if (IPAddress.IsLoopback(ip))
				{ // localhost!
					return this.Host;
				}
			}
			else
			{
				if (string.Equals(hostOrAddress, "localhost", StringComparison.OrdinalIgnoreCase))
				{ // localhost!
					return this.Host;
				}
			}

			// a load-balanced alias resolves per SOURCE host (this map's owner), so a test can pin each client to a backend
			if (this.Topology.TryResolveLoadBalancer(hostOrAddress, this.Host.Id, out var balanced))
			{
				return balanced;
			}

			if (!this.Topology.HostsByNameOrAddress.TryGetValue(hostOrAddress, out var hostId))
			{
#if DEBUG
				// if this looks like a simulated host, and it does not match anything... it's very probable that this is either a typo, or the test setup forgot to register this host!
				if (hostOrAddress.EndsWith(".simulated", StringComparison.OrdinalIgnoreCase) || hostOrAddress.StartsWith("83.73.77.", StringComparison.Ordinal))
				{
					if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
					throw new InvalidOperationException($"You probably forgot to register simulated device '{hostOrAddress}' during startup of the test!");
				}
#endif
				return null;
			}

			if (!this.Topology.HostsById.TryGetValue(hostId, out var host))
			{
				throw new InvalidOperationException($"Inconsistent host indexing in virtual network: '{hostOrAddress}' references host '{hostId}' which is not known??");
			}

			return host;
		}

		/// <inheritdoc/>
		public IPAddress? GetPublicIPAddressForHost(IVirtualNetworkHost target)
		{
			Contract.NotNull(target);

			// use localhost for self-referencing requests
			if (ReferenceEquals(target, this.Host))
			{
				return IPAddress.Loopback;
			}

			foreach (var adapter in this.Host.Adapters)
			{
				if (target.Locations.Contains(adapter.Location) && adapter.UnicastAddresses.Length > 0)
				{
					//BUGBUG: IPv4 or IPv6 first?
					return adapter.UnicastAddresses[0].Address;
				}
			}

			return null;
		}

		public (VirtualNetworkTopology.SimulatedNetwork? Local, VirtualNetworkTopology.SimulatedNetwork? Remote) FindNetworkPath(VirtualNetworkTopology.SimulatedHost target, string hostOrAddress)
		{
			if (target.Equals(this.Host))
			{ // localhost!
				if (hostOrAddress == "127.0.0.1" || hostOrAddress == "::1" || string.Equals(hostOrAddress, "localhost", StringComparison.OrdinalIgnoreCase)) //BUGBUG: TODO: proper check!
				{
					return (this.Host.Loopback, this.Host.Loopback);
				}
				else
				{
					return (this.Host.Locations[0], this.Host.Locations[0]); //HACKHACK
				}
			}

			// for now, we only support "direct" routing
			foreach (var loc in target.Locations.Intersect(this.Host.Locations))
			{
				//TODO: check if the two hosts can talk to each other!
				return (loc, loc);
			}

			foreach (var loc in target.Locations)
			{
				//HACKHACK: we assume that a "cloud" network is reachable by everyone!
				if (loc.Type == VirtualNetworkType.Cloud) return (this.Host.Locations[0], loc);
			}

			return (null, null);
		}

		/// <inheritdoc />
		public override HttpMessageHandler CreateTransportHandler(BetterHttpClientOptions options)
		{
			// target-agnostic virtual transport: the destination host is resolved per-request (inside SendAsync) against the
			// LIVE map, so a client held across a node stop/start/remap reroutes on its very next request.
			return new VirtualHttpClientHandler(this, options);
		}

		/// <inheritdoc />
		public override ValueTask<IPAddress?> GetPublicIPAddressForHost(string hostNameOrAddress, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return ValueTask.FromCanceled<IPAddress?>(ct);

			if (hostNameOrAddress is ("127.0.0.1" or "localhost" or "localhost.localdomain"))
			{
				return new (IPAddress.Loopback);
			}
			if (hostNameOrAddress == "::1")
			{
				return new (IPAddress.IPv6Loopback);
			}
			//HACKHACK: we assume that the first location is the lan!
			return new (this.Host.Addresses[0]);
		}

		/// <inheritdoc />
		public override Task<IPHostEntry> DnsLookup(string hostNameOrAddress, AddressFamily? family, CancellationToken ct)
		{
			return this.Topology.DnsResolve(hostNameOrAddress, this.Host, family, ct);
		}

		/// <inheritdoc />
		public override IReadOnlyList<NetworkAdaptorDescriptor> GetNetworkAdaptors()
		{
			var res = new List<NetworkAdaptorDescriptor>();
			int idx = 0;
			foreach (var net in this.Host.Adapters)
			{
				res.Add(new()
				{
					Id = net.Id,
					Index = ++idx,
					Name = net.Name,
					Description = net.Type.ToString(),
					DnsSuffix = net.Location.Options.DnsSuffix,
					PhysicalAddress = net.PhysicalAddress,
					Speed = null, // TODO
					Type = NetworkInterfaceType.Ethernet,
					UnicastAddresses = net.UnicastAddresses.Select(x => new NetworkAdaptorDescriptor.UnicastAddressDescriptor()
					{
						Address = x.Address,
						IPv4Mask = x.Mask,
						PrefixLength = x.PrefixLength,
					}).ToArray(),
				});
			}
			return res;
		}

	}

	/// <summary>Handler that emulates a non-existing host on the network</summary>
	/// <remarks>All requests using this handler will throw with an <see cref="HttpRequestException"/> emulating various error codes that are typically observed when interacting with missing or failed hosts.</remarks>
	public class VirtualDeadHttpClientHandler : HttpClientHandler
	{

		/// <summary>Creates the appropriate exception to emulate the error condition</summary>
		private Func<Uri?, Exception> Handler { get; }

		public VirtualDeadHttpClientHandler(Func<Uri?, Exception> handler)
		{
			Contract.NotNull(handler);
			this.Handler = handler;
		}

		/// <summary>Simulates a host that is alive, but that is not listening to the requested port.</summary>
		/// <remarks>This is typically the case when either the host is in the process of starting up and the remote service has not started yet, the service failed to start, or the port configuration is incorrect.</remarks>
		public static VirtualDeadHttpClientHandler SimulatePortNotBoundFailure(string debugReason) => new((uri) =>
		{
			var webEx = new WebException($"No connection could be made because the target machine actively refused it {uri?.DnsSafeHost ?? "<unknown>"}:{uri?.Port}", WebExceptionStatus.ConnectFailure);
			return new HttpRequestException($"An error occurred while sending the request. [{debugReason}]", webEx);
		});

		/// <summary>Simulates a host that does not exist, or that cannot be resolved via DNS.</summary>
		/// <remarks>This is typically the case when the url points to a non-existing host, or the DNS search domain is misconfigured on the local node, or when DNS is not available.</remarks>
		public static VirtualDeadHttpClientHandler SimulateNameResolutionFailure(string debugReason) => new((uri) =>
		{
			var webEx = new WebException($"The remote name could not be resolved: '{uri?.Host ?? "<unknown>"}'", WebExceptionStatus.NameResolutionFailure);
			return new HttpRequestException($"An error occurred while sending the request. [{debugReason}]", webEx);
		});

		/// <summary>Simulates a network connectivity issue.</summary>
		/// <returns>This assumes that the IP address is known but the host is not responding, either because it is offline, or there is a network connectivity issue preventing communication both ways.</returns>
		public static VirtualDeadHttpClientHandler SimulateConnectFailure(string debugReason) => new((_) =>
		{
			var sockEx = new SocketException(10060); // TimedOut
			var webEx = new WebException("Unable to connect to the remove server", sockEx, WebExceptionStatus.ConnectFailure, null);
			return new HttpRequestException($"An error occurred while sending the request. [{debugReason}]", webEx);
		});

		/// <inheritdoc />
		protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			throw this.Handler(request.RequestUri);
		}

		/// <inheritdoc />
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			throw this.Handler(request.RequestUri);
		}

	}

}

#endif
