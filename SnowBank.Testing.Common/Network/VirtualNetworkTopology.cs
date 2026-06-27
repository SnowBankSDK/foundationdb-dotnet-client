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
	using System.Diagnostics.CodeAnalysis;
	using System.Net;
	using System.Net.Http;
	using System.Net.NetworkInformation;
	using System.Net.Sockets;
	using SnowBank.IO.Hashing;

	/// <summary>Default implementation of <see cref="IVirtualNetworkTopology"/></summary>
	public class VirtualNetworkTopology : IVirtualNetworkTopology
	{

		/// <summary>Represents a virtual network adapter that is used by a <see cref="SimulatedHost"/> to talk to a <see cref="SimulatedNetwork"/></summary>
		public sealed record SimulatedNetworkAdapter : IVirtualNetworkAdapter
		{

			public required SimulatedNetwork Location { get; init; }

			/// <inheritdoc />
			IVirtualNetworkLocation IVirtualNetworkAdapter.Location => this.Location;

			/// <inheritdoc />
			public required string Id { get; init; }

			/// <inheritdoc />
			public required NetworkInterfaceType Type { get; init; }

			/// <inheritdoc />
			public required string Name { get; init; }

			/// <inheritdoc />
			public required string Description { get; init; }

			/// <inheritdoc />
			public required (IPAddress Address, IPAddress Mask, int PrefixLength)[] UnicastAddresses { get; init; }

			public string? PhysicalAddress { get; init; }

		}

		// By convention:
		// - If hostname is a fqdns that ends in ".simulated", it is a virtual device
		// - If hostname is an IPv4 in the range 83.73.77.0/24 ("SIM*" in ascii), it is also a virtual device

		/// <summary>Represents a virtual host in a <see cref="SimulatedNetwork"/></summary>
		[DebuggerDisplay("{ToString(),nq}")]
		public sealed class SimulatedHost : IVirtualNetworkHost
		{

			/// <inheritdoc />
			public string Id { get; }

			/// <inheritdoc cref="IVirtualNetworkHost.Adapters"/>
			public SimulatedNetworkAdapter[] Adapters { get; }

			/// <inheritdoc />
			IReadOnlyList<IVirtualNetworkAdapter> IVirtualNetworkHost.Adapters => this.Adapters;

			/// <inheritdoc cref="IVirtualNetworkHost.Locations"/>
			public SimulatedNetwork[] Locations { get; }

			/// <inheritdoc />
			IReadOnlyList<IVirtualNetworkLocation> IVirtualNetworkHost.Locations => this.Locations;

			/// <inheritdoc cref="IVirtualNetworkHost.Loopback"/>
			public SimulatedNetwork? Loopback { get; }

			/// <inheritdoc />
			IVirtualNetworkLocation? IVirtualNetworkHost.Loopback => this.Loopback;

			/// <inheritdoc />
			public string Fqdn { get; }

			/// <inheritdoc />
			public string HostName { get; }

			/// <inheritdoc />
			public string[] Aliases { get; }

			/// <inheritdoc />
			public IPAddress[] Addresses { get; }

			public bool Passthrough { get; }

			/// <inheritdoc />
			public bool Offline { get; private set; }

			/// <summary>Source of cancellation that is tripped when this host goes offline, and renewed when it comes back online.</summary>
			/// <remarks>Linked into every virtual connection that originates from or terminates at this host (see <see cref="SnowBank.Networking.VirtualHttpClientHandler"/>), so that going offline aborts all in-flight connections - like a severed link - while connections opened after coming back online use a fresh token.</remarks>
			private CancellationTokenSource OnlineCts { get; set; } = new();

			/// <summary>Token that stays valid while this host is online and is cancelled when it goes offline. Captured per-connection, so an offline/online cycle leaves connections opened before the cut aborted (latched), while connections opened after are allowed.</summary>
			public CancellationToken OnlineToken => this.OnlineCts.Token;

			/// <summary>Map of all handlers attached to each network location</summary>
			/// <remarks>The key is the network location id, and the value is the map of the ports that are bound: <c>Location => (Port => Handler)</c></remarks>
			public Dictionary<string, Dictionary<int, Func<HttpMessageHandler>>> Handlers { get; } = new(StringComparer.Ordinal);

			/// <summary>Constructs a new simulated host</summary>
			/// <param name="adapters">List of the network adapters that are available for this host (note: must include at least one adapter for 'localhost')</param>
			/// <param name="id">Unique id of this host</param>
			/// <param name="hostName">Primary host name (ex: "pc042")</param>
			/// <param name="fqdn">Primary fully qualified domain name (ex: "pc042.acme.local")</param>
			/// <param name="aliases">Optional list of aliases (including short names or other fqdn)</param>
			/// <param name="addresses">List of IP addresses owned by this host</param>
			/// <param name="passthrough">If true, this host represents an actual physical host, accessible on the network by the testing framework, and all requests will be forwarded to this physical host (instead of being simulated).</param>
			/// <param name="offline">If true, this host starts in "offline" mode, and will not respond to requests.</param>
			public SimulatedHost(SimulatedNetworkAdapter[] adapters, string id, string hostName, string fqdn, string[] aliases, IPAddress[] addresses, bool passthrough, bool offline)
			{
				Contract.Debug.Requires(adapters != null && adapters.Length != 0 && id != null && hostName != null && fqdn != null && aliases != null && addresses != null);
				this.Adapters = adapters;
				this.Locations = adapters.Select(x => x.Location).ToArray();
				this.Loopback = adapters.SingleOrDefault(l => l.Location.Type == VirtualNetworkType.Loopback)?.Location;
				this.Id = id;
				this.HostName = hostName;
				this.Fqdn = fqdn;
				this.Aliases = aliases;
				this.Addresses = addresses;
				this.Passthrough = passthrough;
				this.Offline = offline;
			}

			/// <inheritdoc />
			public void SetOffline(bool offline)
			{
				//TODO: maybe add a parameter to specify which kind of "offline" fault the host should simulate: powered-down? ethernet is off? currently rebooting but not yet ready? some big crash?
				if (offline)
				{
					if (!this.Offline)
					{
						this.Offline = true;
						// abort every in-flight connection that originates from or terminates at this host (severed link)
						this.OnlineCts.Cancel();
					}
				}
				else
				{
					if (this.Offline)
					{
						this.Offline = false;
						// renew the token: connections opened from now on are allowed again, while those cut above stay cut (latched)
						this.OnlineCts = new CancellationTokenSource();
					}
				}
			}

			/// <inheritdoc />
			/// <exception cref="InvalidOperationException">If the host is not able to bind virtual socket (ex: passthrough host)</exception>
			public void Bind(IVirtualNetworkLocation location, int port, Func<HttpMessageHandler> handler)
			{
				if (this.Passthrough) throw new InvalidOperationException("Cannot bind ports to passthrough hosts!");

				lock (this.Handlers)
				{
					if (!this.Handlers.TryGetValue(location.Id, out var ports))
					{
						ports = new();
						this.Handlers[location.Id] = ports;
					}

					ports.Add(port, handler);
				}
			}

			/// <summary>Finds a message handler that is bound to the specified port</summary>
			/// <param name="location">Network location (ie: network adapter) from which the request is coming</param>
			/// <param name="port">Port of the connection attempt</param>
			/// <returns>If the port is bound, return a new HTTP message handler that will process request. If the port is unassigned, it will return <see langword="null"/>.</returns>
			public Func<HttpMessageHandler>? FindHandler(IVirtualNetworkLocation location, int port)
			{
				// si c'est un vrai host, on n'a pas de handler custom
				if (this.Passthrough) return null;

				lock (this.Handlers)
				{
					if (!this.Handlers.TryGetValue(location.Id, out var ports)) return null;
					// is there an exact match for this host?
					if (ports.TryGetValue(port, out var handler)) return handler;
					// if port 0 is defined, it captures all ports for this host
					if (port != 0 && ports.TryGetValue(0, out handler)) return handler;
					return null;
				}
			}

			/// <summary>Returns all the variations of host names, fqdn and aliases that this host responds to.</summary>
			public IEnumerable<string> GetHostKeys()
			{
				if (!string.IsNullOrEmpty(this.Fqdn))
				{
					yield return this.Fqdn;
				}

				if (!string.IsNullOrEmpty(this.HostName) && !string.Equals(this.Fqdn, this.HostName, StringComparison.OrdinalIgnoreCase))
				{
					yield return this.HostName;
				}

				foreach (var name in this.Aliases)
				{
					if (name != null!) yield return name;
				}
				foreach (var addr in this.Addresses)
				{
					if (addr != null!) yield return addr.ToString();
				}
			}

			/// <inheritdoc />
			public override string ToString()
			{
				return $"Host<{this.Id}>(Fqdn={this.Fqdn}, IP={string.Join<IPAddress>(", ", this.Addresses)}, Aliases={string.Join<string>(", ", this.Aliases)})";
			}

			/// <inheritdoc />
			public override bool Equals(object? obj) => obj is IVirtualNetworkHost host && Equals(host);

			/// <inheritdoc />
			public bool Equals(IVirtualNetworkHost? other) => ReferenceEquals(other, this) || (!ReferenceEquals(other, null) && other.Id == this.Id);

			/// <inheritdoc />
			public override int GetHashCode() => this.Id.GetHashCode();

		}

		public Dictionary<string, SimulatedHost> HostsById { get; } = new(StringComparer.Ordinal);

		public Dictionary<string, string> HostsByNameOrAddress { get; } = new(StringComparer.OrdinalIgnoreCase);

		/// <summary>Represents a virtual network location, such as "LAN", a Cloud DMZ, or the Loopback interface, as well as all its services (DNS, DHCP, ...).</summary>
		/// <remarks>
		/// <para>Hosts belonging to the same network location can talk to each other directly.</para>
		/// <para>Hosts from different network locations may require virtual routing to be configured</para>
		/// </remarks>
		[DebuggerDisplay("Id={Id}, Name={Name}, Type={Type}, IpRange={Options.IpRange}, DnsSuffix={Options.DnsSuffix}")]
		public sealed class SimulatedNetwork : IVirtualNetworkLocation
		{

			public SimulatedNetwork(VirtualNetworkTopology topology, string id, string name, VirtualNetworkType type, VirtualNetworkLocationOptions options)
			{
				Contract.Debug.Requires(id != null && name != null && options != null);
				this.Id = id;
				this.Name = name;
				this.Type = type;
				this.Options = options;
				this.Topology = topology;

				if (type != VirtualNetworkType.Loopback && options.IpRange != null && options.AutoDhcp)
				{
					IPAddressHelpers.DecodeIPRange(options.IpRange, out var first, out var last);
					this.DhcpAddressFirst = first;
					this.DhcpAddressLast = last;
					this.DhcpAddressNextFree = first;
				}
			}

			/// <inheritdoc />
			public string Id { get; }

			/// <inheritdoc />
			public string Name { get; }

			/// <inheritdoc />
			public VirtualNetworkType Type { get; }

			/// <inheritdoc />
			public VirtualNetworkLocationOptions Options { get; }

			/// <inheritdoc cref="IVirtualNetworkLocation.Topology" />
			public VirtualNetworkTopology Topology { get; }

			/// <inheritdoc />
			IVirtualNetworkTopology IVirtualNetworkLocation.Topology => this.Topology;

			/// <summary>Host présent dans cet emplacement</summary>
			public Dictionary<string, SimulatedHost> HostsById { get; } = new(StringComparer.OrdinalIgnoreCase);

			public Dictionary<string, string> HostsByNameOrAddress { get; } = new(StringComparer.OrdinalIgnoreCase);

			public Dictionary<string, List<(string Id, string? Argument)>> NetworkServices { get; } = new(StringComparer.Ordinal);

			/// <inheritdoc />
			IVirtualNetworkMap IVirtualNetworkLocation.RegisterHost(string id, VirtualHostIdentity identity) => RegisterHost(id, identity);

			/// <inheritdoc cref="IVirtualNetworkLocation.RegisterHost" />
			public VirtualNetworkMap RegisterHost(string id, VirtualHostIdentity identity)
			{
				var host = this.Topology.RegisterHost(this, id, identity);
				this.HostsById.Add(id, host);
				foreach (var key in host.GetHostKeys())
				{
					this.HostsByNameOrAddress.Add(key, id);
				}
				return new VirtualNetworkMap(this.Topology, host);
			}

			/// <inheritdoc />
			IVirtualNetworkHost IVirtualNetworkLocation.AddHostPassthrough(string id, VirtualHostIdentity identity) => AddHostPassthrough(id, identity);

			/// <inheritdoc cref="IVirtualNetworkLocation.AddHostPassthrough" />
			public SimulatedHost AddHostPassthrough(string id, VirtualHostIdentity identity)
			{
				identity.PassthroughToPhysicalNetwork = true;
				return this.Topology.RegisterHost(this, id, identity);
			}

			/// <inheritdoc />
			public IVirtualNetworkHost? GetHost(string id)
			{
				return this.HostsById.GetValueOrDefault(id);
			}

			/// <inheritdoc />
			public bool CanSendTo(IVirtualNetworkLocation target)
			{
				if (this.Equals(target)) return true;
				//TODO: do we have a valid gateway, or are we isolated from the rest of the world?
				return true; // open by default
			}

			/// <inheritdoc />
			public bool CanReceiveFrom(IVirtualNetworkLocation source)
			{
				if (this.Equals(source)) return true;
				return this.Options.AllowsIncoming;
			}

			/// <inheritdoc />
			public void RegisterNetworkService(string serviceType, string componentId, string? argument)
			{
				Contract.NotNullOrEmpty(serviceType);
				Contract.NotNullOrEmpty(componentId);

				if (!this.NetworkServices.TryGetValue(serviceType, out var services))
				{
					services = [ ];
					this.NetworkServices[serviceType] = services;
				}
				services.Add((componentId, argument));
			}

			/// <inheritdoc />
			public (string Id, string? Argument)[] BrowseNetworkService(string serviceType)
			{
				return !this.NetworkServices.TryGetValue(serviceType, out var services)
					? [ ]
					: services.ToArray();
			}

			/// <inheritdoc />
			public override string ToString() => $"Network<{this.Id}>(Type={this.Type}, Name={this.Name}, IP={this.Options.IpRange})";

			/// <inheritdoc />
			public override bool Equals(object? obj) => obj is IVirtualNetworkLocation net && Equals(net);

			/// <inheritdoc />
			public bool Equals(IVirtualNetworkLocation? other) => ReferenceEquals(this, other) || (!ReferenceEquals(other, null) && other.Id == this.Id);

			/// <inheritdoc />
			public override int GetHashCode() => this.Id.GetHashCode();

			private SortedSet<IPAddress> AllocatedAddresses { get; } = new(IPAddressComparer.Default);

			private IPAddress? DhcpAddressFirst { get; }

			private IPAddress? DhcpAddressLast { get; }

			private IPAddress? DhcpAddressNextFree { get; set; }

			/// <inheritdoc />
			public void RegisterIpAddress(IPAddress address)
			{
				if (!this.AllocatedAddresses.Add(address))
				{
					throw new InvalidOperationException($"IP Address {address} is already allocated in Network Location {this.Id} ({this.Name})");
				}
			}

			/// <inheritdoc />
			public IPAddress AllocateIpAddress()
			{
				if (this.Type == VirtualNetworkType.Loopback) throw new InvalidOperationException($"Network Location {this.Id} ({this.Name}) does not support DHCP because it is a loopback adaptaer!");
				if (this.DhcpAddressFirst == null) throw new InvalidOperationException($"Network Location {this.Id} ({this.Name}) does not have any IP range for address allocation");

				var candidate = this.DhcpAddressNextFree;
				while (candidate != null)
				{
					var next = IPAddressHelpers.AddOffset(candidate, 1);
					if (IPAddressComparer.Default.Compare(next, this.DhcpAddressLast!) > 0)
					{
						next = null;
					}
					this.DhcpAddressNextFree = next;
					if (this.AllocatedAddresses.Add(candidate))
					{
						return candidate;
					}
					candidate = next;
				}
				throw new InvalidOperationException($"Network Location {this.Id} ({this.Name}) allocation pool is full!");
			}

			/// <inheritdoc />
			public string AllocateMacAddress(string ouiPrefix, string? seed = null)
			{
				Contract.NotNullOrEmpty(ouiPrefix);
				if (ouiPrefix.Length != 8) throw new ArgumentException("OUI prefix must be of the form 'XX:XX:XX'", nameof(ouiPrefix));

				seed ??= Guid.NewGuid().ToString();
				ulong h = Fnv1Hash32.FromString(seed);
				// We only need 3 bytes, so we will fold the 32 bits down to 24 bits.
				int tail = (int) ((h & 0xFFFFFFUL) ^ ((h >> 24) & 0xFFFFFFUL) ^ ((h >> 48) & 0xFFFFUL));
				return $"{ouiPrefix}:{((tail >> 16) & 0xFF):X02}:{((tail >> 8) & 0xFF):X02}:{(tail & 0xFF):X02}";
			}

			/// <summary>Generates a new unique serial number, within this network location</summary>
			/// <param name="pattern">Pattern string where any '#' will be replaced by a digit (0-9), and '?' by an uppercase letter (A-Z)</param>
			/// <param name="seed">Optional seed used to generate a deterministic serial numbers (like the host name, its IP address, etc...). If <see langword="null"/>, a randomly generated seed will be used instead.</param>
			/// <returns>A string where all '#' and '?' in the <paramref name="pattern"/> have been replaced.</returns>
			/// <exception cref="ArgumentException">If the pattern did not include any '#' or '?'</exception>
			/// <example><c>AllocateSerialNumber("ACME-???###-T") => "ACME-ZOB420-T"</c></example>
			public string AllocateSerialNumber(string pattern, string? seed = null)
			{
				Contract.NotNullOrEmpty(pattern);

				seed ??= Guid.NewGuid().ToString();
				ulong h = Fnv1Hash32.FromString(seed);

				bool changed = false;
				var buffer = pattern.ToCharArray();
				for (int i = 0; i < buffer.Length; i++)
				{
					char c = buffer[i];
					if (c == '#')
					{
						changed = true;
						buffer[i] = (char) ('0' + (int) (h % 10));
						h /= 10;
					}
					else if (c == '?')
					{
						changed = true;
						buffer[i] = (char) ('A' + (int) (h % 26));
						h /= 26;
					}
				}
				Contract.Debug.Ensures(h != 0, "Ran out of bits! Please reduce the number of replaced characters!");

				if (!changed) throw new ArgumentException("Invalid pattern: must be of the form 'A###??##' with '#' replaced with digits, and '?' replaced with letters.", nameof(pattern));
				return new string(buffer);
			}

		}

		internal ReaderWriterLockSlim Lock { get; } = new ReaderWriterLockSlim();

		internal Dictionary<string, SimulatedNetwork> Locations { get; } = new Dictionary<string, SimulatedNetwork>(StringComparer.Ordinal);

		/// <inheritdoc />
		public IVirtualNetworkLocation RegisterLocation(string id, string name, VirtualNetworkType type, VirtualNetworkLocationOptions options)
		{
			Contract.Debug.Requires(id != null && name != null && options != null);
			Contract.Debug.Requires(options.DnsSuffix == null || options.DnsSuffix.StartsWith('.'));

			using (this.Lock.GetWriteLock())
			{
				if (this.Locations.TryGetValue(id, out var previous))
				{
					throw new InvalidOperationException($"There is already a network location with id '{previous.Id}' ('{previous.Name}')");
				}

				var location = new SimulatedNetwork(this, id, name, type, options);
				this.Locations.Add(location.Id, location);
				return location;
			}
		}

		/// <inheritdoc />
		public IVirtualNetworkLocation GetLocation(string id)
		{
			using (this.Lock.GetReadLock())
			{
				return this.Locations.TryGetValue(id, out var location)
					? location
					: throw new InvalidOperationException($"There is no network location '{id}' in the test environment!");
			}
		}

		/// <inheritdoc />
		public string Dump()
		{
			using (this.Lock.GetReadLock())
			{
				var sb = new StringBuilder();
				sb.AppendLine("# Network Topology:");
				sb.AppendLine($"# - Hosts: {this.HostsById.Count:N0}");
				foreach (var host in this.HostsById.Values)
				{
					sb.AppendLine($"#   - {host}:");
					foreach (var adapter in host.Adapters)
					{
						host.Handlers.TryGetValue(adapter.Location.Id, out var ports);
						sb.AppendLine($"#     - {adapter.Location.Id}: {(ports != null ? string.Join(", ", ports.Keys) : "<none>")}");
					}
				}

				sb.AppendLine($"# - Locations: {this.Locations.Count:N0}");
				foreach (var loc in this.Locations.Values)
				{
					sb.AppendLine("#   - " + loc);
					foreach (var host in loc.HostsById.Values)
					{
						sb.AppendLine($"#     - {host.Id}: {host.Fqdn}, {string.Join<IPAddress>(", ", host.Addresses)}, {string.Join<string>(", ", host.Aliases)}");
					}
				}

				return sb.ToString();
			}
		}

		/// <summary>Registers a new host in the global network topology</summary>
		/// <param name="location">Network location where this host is located</param>
		/// <param name="id">Unique id of this host</param>
		/// <param name="identity">Configuration of this host</param>
		public SimulatedHost RegisterHost(SimulatedNetwork location, string id, VirtualHostIdentity identity)
		{
			Contract.NotNull(location);
			Contract.NotNull(id);
			Contract.NotNull(identity);

			if (location.Topology != this)
			{
				throw new ArgumentException("Network location is attached to a different network topology", nameof(location));
			}

			if (location.Topology.HostsById.TryGetValue(id, out var previous))
			{
				throw new ArgumentException($"There is already an host with id '{id}' defined on this network: {previous.Fqdn}", nameof(id));
			}

			var hostName = identity.HostName;
			var fqdn = identity.Fqdn;

			if (hostName == null)
			{
				if (fqdn != null)
				{
					int p = fqdn.IndexOf('.');
					hostName = p < 1 ? identity.Fqdn : fqdn[..(p - 1)];
				}
				else
				{
					hostName = id.ToLowerInvariant();
				}
				Contract.Debug.Assert(hostName != null);
			}

			fqdn ??= hostName + (identity.DnsSuffix ?? ".simulated");

			var aliases = identity.Aliases.ToArray();
			var addresses = identity.Addresses.ToArray();
			var netMask = IPAddress.Parse("255.0.0.0"); //HACKHACK: BUGBUG: must parse from the IP range!
			var prefixLen = 8; //HACKHACK: BUGBUG: must parse from the IP range!

			var offline = identity.StartAsOffline;

			var adapters = new List<SimulatedNetworkAdapter>();

			using (this.Lock.GetWriteLock())
			{
				adapters.Add(new SimulatedNetworkAdapter()
				{
					Location = location,
					Id = "ethernet",
					Name = "Local Area Connection",
					Description = "Ethernet Network Adapter (virtual)",
					Type = NetworkInterfaceType.Ethernet,
					UnicastAddresses = addresses.Select(ip => (ip, netMask, prefixLen)).ToArray(),
					PhysicalAddress =  "00:11:22:33:44:55", //TODO: BUGBUG: !!
				});

				SimulatedNetwork? loopback = null;
				if (location.Type != VirtualNetworkType.Loopback && !identity.PassthroughToPhysicalNetwork)
				{ // génère automatiquement un "localhost" attaché à ce host
					loopback = new SimulatedNetwork(
						this,
						id + ":loopback",
						"Loopback for " + id,
						VirtualNetworkType.Loopback,
						new() { AllowsIncoming = false, IpRange = "127.0.0.1/24" }
					);
					this.Locations.Add(loopback.Id, loopback);
					adapters.Add(new SimulatedNetworkAdapter()
					{
						Location = loopback,
						Id = "loopback",
						Name = "Loopback",
						Description = "Loopback Network Adapter (virtual)",
						Type = NetworkInterfaceType.Loopback,
						UnicastAddresses = [ (IPAddress.Loopback, IPAddress.Parse("255.0.0.0"), 8) ],
						PhysicalAddress = null //REVIEW: should localhost have a MAC Address?
					});
				}

				var host = new SimulatedHost(adapters.ToArray(), id, hostName, fqdn, aliases, addresses, identity.PassthroughToPhysicalNetwork, offline);
				this.HostsById.Add(host.Id, host);
				foreach (var key in host.GetHostKeys())
				{
					this.HostsByNameOrAddress.Add(key, host.Id);
				}

				if (loopback != null)
				{
					loopback.HostsById.Add(host.Id, host);
					loopback.HostsByNameOrAddress.Add("localhost", host.Id);
					loopback.HostsByNameOrAddress.Add("localhost.localdomain", host.Id);
					loopback.HostsByNameOrAddress.Add("127.0.0.1", host.Id);
					loopback.HostsByNameOrAddress.Add("::1", host.Id);
				}

				return host;
			}
		}

		/// <inheritdoc />
		IVirtualNetworkHost IVirtualNetworkTopology.GetHost(string id) => GetHost(id);

		private static InvalidOperationException ErrorMissingHost(string id) => new($"Simulated host '{id}' does not exists");

		/// <summary>Gets a virtual host, given its identifier</summary>
		/// <param name="id">Id of the virtual host</param>
		/// <returns>The corresponding <see cref="IVirtualNetworkHost"/> if it exists, or an exception if it does not.</returns>
		/// <exception cref="InvalidOperationException">If there is no virtual host with the given <paramref name="id"/></exception>
		public SimulatedHost GetHost(string id)
		{
			using (this.Lock.GetReadLock())
			{
				return this.HostsById.TryGetValue(id, out var host) ? host : throw ErrorMissingHost(id);
			}
		}

		/// <summary>Gets a virtual host, given its IP address</summary>
		/// <param name="address">Known IP address of the host</param>
		/// <param name="host">Receives the host if there is a match.</param>
		/// <returns><see langword="true"/> if the host was found; otherwise, <see langword="false"/></returns>
		public bool TryGetHostByIpAddress(IPAddress address, [MaybeNullWhen(false)] out SimulatedHost host)
		{
			using (this.Lock.GetReadLock())
			{
				if (!this.HostsByNameOrAddress.TryGetValue(address.ToString(), out var hostId))
				{
					host = null;
					return false;
				}
				if (!this.HostsById.TryGetValue(hostId, out host))
				{
					throw ErrorMissingHost(hostId);
				}
				return true;
			}
		}

		/// <summary>Gets a virtual host, given its host name</summary>
		/// <param name="hostName">Known name of the host (could be host name, fqdn, one of its aliases, ...)</param>
		/// <param name="host">Receives the host if there is a match.</param>
		/// <returns><see langword="true"/> if the host was found; otherwise, <see langword="false"/></returns>
		public bool TryGetHostByHostName(string hostName, [MaybeNullWhen(false)] out SimulatedHost host)
		{
			using (this.Lock.GetReadLock())
			{
				if (!this.HostsByNameOrAddress.TryGetValue(hostName, out var hostId))
				{
					host = null;
					return false;
				}
				if (!this.HostsById.TryGetValue(hostId, out host)) throw ErrorMissingHost(hostId);
				return true;
			}
		}

		/// <summary>Performs a simulated DNS resolution of the given host name or ip address, from the point of view of a simulated host.</summary>
		/// <param name="hostNameOrAddress">Host name, fqdn, or IP address</param>
		/// <param name="source">Virtual host that is performing the DNS resolution</param>
		/// <param name="family">If specified, the type of address resolved (A, AAA, ...)</param>
		/// <param name="ct">Token used to cancel the DNS resolution</param>
		/// <returns>Result of the resolution.</returns>
		/// <exception cref="SocketException">Simulated socket exception, if the resolution has failed</exception>
		/// <exception cref="ArgumentException">If any argument is invalid.</exception>
		/// <remarks>This method attempts to emulate the behavior of <see cref="Dns.GetHostEntryAsync(System.Net.IPAddress)"/></remarks>
		public async Task<IPHostEntry> DnsResolve(string hostNameOrAddress, IVirtualNetworkHost? source, AddressFamily? family, CancellationToken ct)
		{
			Contract.NotNullOrEmpty(hostNameOrAddress);

			ct.ThrowIfCancellationRequested();

			if (!this.HostsByNameOrAddress.TryGetValue(hostNameOrAddress, out var hostId))
			{
				throw new SocketException(11001);
			}

			// simulate context switch
			await Task.Yield();

			var host = GetHost(hostId);

			var addresses = (family ?? AddressFamily.Unspecified) switch
			{
				AddressFamily.Unspecified    => host.Addresses.ToArray(),
				AddressFamily.InterNetwork   => host.Addresses.Where(x => x.AddressFamily == AddressFamily.InterNetwork).ToArray(),
				AddressFamily.InterNetworkV6 => host.Addresses.Where(x => x.AddressFamily == AddressFamily.InterNetworkV6).ToArray(),
				_                            => throw new ArgumentException("Unsupported address family", nameof(family))
			};

			return new IPHostEntry()
			{
				HostName = host.Fqdn,
				Aliases = host.Aliases.ToArray(),
				AddressList = addresses,
			};
		}

	}

}

#endif
