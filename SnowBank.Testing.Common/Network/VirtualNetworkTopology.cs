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
	using System.Collections.Concurrent;
	using System.Diagnostics.CodeAnalysis;
	using System.Net;
	using System.Net.Http;
	using System.Net.NetworkInformation;
	using System.Net.Sockets;
	using System.Text.RegularExpressions;
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

		// Naming convention, enforced by the egress guard (ValidateEgressName) and the resolve/request defense (ClassifyUnresolvedName):
		// - a name that ends in ".simulated" is a simulated device on a lan/cloud network
		// - a real name (never ".simulated") is a device on an external network; its address draws from the 69.88.84.0/24 EXT block

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
				// if this is a real host, we don't have a custom handler
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

		/// <summary>Names registered via <see cref="SetAlias"/> (mutable VIPs), distinguished from the hosts' own immutable keys</summary>
		private HashSet<string> DynamicAliases { get; } = new(StringComparer.OrdinalIgnoreCase);

		/// <summary>Virtual load balancers registered on this topology, by id</summary>
		private Dictionary<string, VirtualLoadBalancer> LoadBalancersById { get; } = new(StringComparer.OrdinalIgnoreCase);

		/// <summary>Virtual load balancers registered on this topology, by their public alias</summary>
		private Dictionary<string, VirtualLoadBalancer> LoadBalancersByAlias { get; } = new(StringComparer.OrdinalIgnoreCase);

		/// <summary>Registers a virtual load balancer: a public alias that routes each incoming connection to one of several backend hosts, under full test control</summary>
		/// <param name="id">Identifier of the balancer (e.g. <c>"CLUSTER"</c>)</param>
		/// <param name="alias">Public name the clients connect to (e.g. <c>"cluster.lan.simulated"</c>); must not be the name of a real host</param>
		/// <param name="backends">Ids of the backend hosts; they may be registered later in the setup (resolution is lazy)</param>
		public VirtualLoadBalancer RegisterLoadBalancer(string id, string alias, string[] backends)
		{
			Contract.NotNullOrEmpty(id);
			Contract.NotNullOrEmpty(alias);
			Contract.NotNullOrEmpty(backends);

			using (this.Lock.GetWriteLock())
			{
				if (this.LoadBalancersById.ContainsKey(id))
				{
					throw new ArgumentException($"There is already a load balancer with id '{id}' on this network", nameof(id));
				}
				if (this.HostsByNameOrAddress.ContainsKey(alias) || this.LoadBalancersByAlias.ContainsKey(alias))
				{
					throw new ArgumentException($"Cannot register load balancer alias '{alias}': this name is already taken", nameof(alias));
				}

				var lb = new VirtualLoadBalancer(id, alias, backends);
				this.LoadBalancersById.Add(id, lb);
				this.LoadBalancersByAlias.Add(alias, lb);
				return lb;
			}
		}

		/// <summary>Gets a registered virtual load balancer, given its identifier</summary>
		public VirtualLoadBalancer GetLoadBalancer(string id)
		{
			using (this.Lock.GetReadLock())
			{
				return this.LoadBalancersById.TryGetValue(id, out var lb) ? lb : throw new InvalidOperationException($"There is no load balancer with id '{id}' on this network");
			}
		}

		/// <summary>Resolves a load-balanced alias into the backend host that must serve a connection from the given source</summary>
		/// <param name="alias">Name being resolved (only matches if it is a registered balancer alias)</param>
		/// <param name="sourceHostId">Id of the connecting host, or <c>null</c> when unknown</param>
		/// <param name="host">Receives the backend host chosen by the balancer's routing rules</param>
		/// <returns><c>true</c> if the name is a balancer alias (the connection must go to <paramref name="host"/>); <c>false</c> if it is not (fall through to the regular name resolution)</returns>
		internal bool TryResolveLoadBalancer(string alias, string? sourceHostId, [MaybeNullWhen(false)] out SimulatedHost host)
		{
			VirtualLoadBalancer? lb;
			using (this.Lock.GetReadLock())
			{
				if (!this.LoadBalancersByAlias.TryGetValue(alias, out lb))
				{
					host = null;
					return false;
				}
			}

			var target = lb.ResolveTarget(sourceHostId);
			using (this.Lock.GetReadLock())
			{
				if (!this.HostsById.TryGetValue(target, out host))
				{
					throw new InvalidOperationException($"Load balancer '{lb.Id}' routed '{sourceHostId ?? "?"}' to backend '{target}', but no such host is registered (yet?)");
				}
			}
			return true;
		}

		/// <summary>Registers (or re-points) a mutable alias - a virtual VIP - that resolves to the given host</summary>
		/// <param name="alias">Name the clients connect to (e.g. <c>"cluster.lan.simulated"</c>); must not be the name of a real host</param>
		/// <param name="hostId">Id of the host that the alias resolves to from now on</param>
		/// <remarks>
		/// <para>This emulates a load balancer re-routing a public endpoint: host resolution happens per request (see
		/// <c>VirtualHttpClientHandler.SendAsync</c>), so re-pointing the alias affects the next connection while any
		/// already-established stream keeps flowing to its original host - exactly like a real VIP change, which never
		/// migrates live TCP connections.</para>
		/// </remarks>
		public void SetAlias(string alias, string hostId)
		{
			Contract.NotNullOrEmpty(alias);
			Contract.NotNullOrEmpty(hostId);

			using (this.Lock.GetWriteLock())
			{
				if (!this.HostsById.TryGetValue(hostId, out var host))
				{
					throw ErrorMissingHost(hostId);
				}
				// a VIP carries the egress rule of the host it points at: a real-TLD VIP can attach only to an external host, a
				// .simulated VIP only to a simulated host.
				ValidateEgressName(PrimaryNetworkType(host), alias, hostId);
				if (this.HostsByNameOrAddress.ContainsKey(alias) && !this.DynamicAliases.Contains(alias))
				{
					throw new ArgumentException($"Cannot register alias '{alias}': this name already belongs to a real host", nameof(alias));
				}
				this.HostsByNameOrAddress[alias] = hostId;
				this.DynamicAliases.Add(alias);
			}
		}

		#region Fault Injection (directional link cuts)...

		/// <summary>Time source used to schedule the deferred fault behaviors (blackhole connect budgets and read/write notice deadlines)</summary>
		/// <remarks>Tests running on virtual time should point this to their advanceable provider before injecting <see cref="VirtualNetworkFaultKind.Blackhole"/> faults: a parked connect/read/write then fails only when the fake clock is advanced past its budget, so "30 seconds of silence" costs zero real time.</remarks>
		public TimeProvider Time { get; set; } = TimeProvider.System;

		/// <summary>Live state of all the directional edges of this network, created lazily per (from, to) pair that talks (or gets cut)</summary>
		private ConcurrentDictionary<(string From, string To), VirtualNetworkCutEdge> CutEdges { get; } = new();

		/// <summary>Gets (or lazily creates) the live state of the directional edge carrying the traffic from one host to another</summary>
		/// <remarks>The transport resolves this per-request, and captures the edge's <see cref="VirtualNetworkCutEdge.CutToken"/> per connection - which is why the edge object must be long-lived: a cut applied later must still be able to abort the streams established while the edge was healthy.</remarks>
		internal VirtualNetworkCutEdge GetOrCreateCutEdge(string fromId, string toId)
		{
			return this.CutEdges.GetOrAdd((fromId, toId), static (k) => new VirtualNetworkCutEdge(k.From, k.To));
		}

		/// <summary>Cuts the directional link carrying the traffic from one host to another, with the given failure mode</summary>
		/// <param name="fromId">Id of the host whose outgoing traffic (towards <paramref name="toId"/>) is affected</param>
		/// <param name="toId">Id of the host that becomes unreachable from <paramref name="fromId"/></param>
		/// <param name="fault">how the link fails, as observed by <paramref name="fromId"/> (see <see cref="VirtualNetworkFault"/>)</param>
		/// <remarks>
		/// <para>Only this direction is affected: traffic from <paramref name="toId"/> to <paramref name="fromId"/> keeps flowing,
		/// which is the whole point (asymmetric faults are where failure detectors earn their keep). Use <see cref="CutBoth"/>
		/// for a symmetric cut. <see cref="SimulatedHost.SetOffline"/> remains the degenerate "every edge of this host, both
		/// directions" case.</para>
		/// <para>A <see cref="VirtualNetworkFaultKind.Severed"/> cut also aborts the established connections that were initiated
		/// over this edge (the transport links each connection to the edge's <see cref="VirtualNetworkCutEdge.CutToken"/>); the
		/// other fault kinds only affect new connection attempts and (for <see cref="VirtualNetworkFaultKind.Blackhole"/>) the
		/// byte flow of established connections in this direction.</para>
		/// </remarks>
		public void Cut(string fromId, string toId, VirtualNetworkFault fault)
		{
			Contract.NotNullOrEmpty(fromId);
			Contract.NotNullOrEmpty(toId);
			Contract.NotNull(fault);

			using (this.Lock.GetReadLock())
			{
				if (!this.HostsById.ContainsKey(fromId)) throw ErrorMissingHost(fromId);
				if (!this.HostsById.ContainsKey(toId)) throw ErrorMissingHost(toId);
			}

			GetOrCreateCutEdge(fromId, toId).Apply(fault);
		}

		/// <summary>Restores the directional link carrying the traffic from one host to another (the "cable is plugged back in")</summary>
		/// <remarks>New connections are allowed again, and operations parked on a <see cref="VirtualNetworkFaultKind.Blackhole"/> resume; connections aborted by a <see cref="VirtualNetworkFaultKind.Severed"/> cut stay dead (latched), like after a real link flap.</remarks>
		public void Restore(string fromId, string toId)
		{
			Contract.NotNullOrEmpty(fromId);
			Contract.NotNullOrEmpty(toId);

			if (this.CutEdges.TryGetValue((fromId, toId), out var edge))
			{
				edge.Restore();
			}
		}

		/// <summary>Cuts both directions of the link between two hosts, with the given failure mode (see <see cref="Cut"/>)</summary>
		public void CutBoth(string hostA, string hostB, VirtualNetworkFault fault)
		{
			Cut(hostA, hostB, fault);
			Cut(hostB, hostA, fault);
		}

		/// <summary>Restores both directions of the link between two hosts (see <see cref="Restore"/>)</summary>
		public void RestoreBoth(string hostA, string hostB)
		{
			Restore(hostA, hostB);
			Restore(hostB, hostA);
		}

		/// <summary>Disables DNS resolution for every name matching a glob pattern, topology-wide: a matching name gives the normal simulated name-resolution failure (quiet), instead of the loud "real URI leaked" alarm.</summary>
		/// <param name="pattern">A glob over the host name, where <c>*</c> matches any run of characters (e.g. <c>"api.*.partner.com"</c>, or <c>"*"</c> for every name).</param>
		/// <param name="fault">Must be <see cref="VirtualNetworkFault.NameResolution"/>: a name-pattern cut only models an intended DNS failure.</param>
		/// <remarks>This is the sanctioned way to get a simulated DNS failure for an unregistered name, and the opt-out a test of the harness itself uses (<c>Cut("*", VirtualNetworkFault.NameResolution)</c> disables the egress alarm broadly). The cut is topology-global by design; a source-scoped variant can follow if a test needs one.</remarks>
		public void Cut(string pattern, VirtualNetworkFault fault)
		{
			Contract.NotNullOrEmpty(pattern);
			Contract.NotNull(fault);
			if (fault.Kind != VirtualNetworkFaultKind.NameResolution)
			{
				throw new ArgumentException($"A name-pattern cut only models an intended DNS failure: pass {nameof(VirtualNetworkFault)}.{nameof(VirtualNetworkFault.NameResolution)}, not {fault.Kind}.", nameof(fault));
			}

			using (this.Lock.GetWriteLock())
			{
				this.NameResolutionCuts.Add(GlobToRegex(pattern));
			}
		}

		/// <summary>Compiled glob patterns whose matching names fail DNS resolution on purpose (a quiet, intended failure, never the loud alarm).</summary>
		private List<Regex> NameResolutionCuts { get; } = [ ];

		/// <summary>Tests whether a name is disabled by a <see cref="Cut(string, VirtualNetworkFault)">name-pattern NameResolution cut</see>.</summary>
		private bool IsNameResolutionCut(string name)
		{
			using (this.Lock.GetReadLock())
			{
				foreach (var rx in this.NameResolutionCuts)
				{
					if (rx.IsMatch(name)) return true;
				}
			}
			return false;
		}

		/// <summary>Translates a simple glob (only <c>*</c> is special) into an anchored, case-insensitive regex.</summary>
		private static Regex GlobToRegex(string pattern)
			=> new("^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		/// <summary>Classifies a name that did not resolve to a registered host and throws the right error, unless the failure is intended (a name-pattern NameResolution cut, or a raw IP), in which case it returns and the caller produces the normal quiet failure.</summary>
		/// <remarks>Always on (not DEBUG-only): a real name that leaks into the sandbox must be loud so the harness bug is found, instead of a request silently reaching a real server.</remarks>
		internal void ClassifyUnresolvedName(string name)
		{
			// an intended DNS failure (the sanctioned negative path, and the harness self-test opt-out): stay quiet
			if (IsNameResolutionCut(name)) return;

			// a ".simulated" name, or an address in the reserved EXT block, with no registration is almost always a typo or a
			// forgotten mock: a friendly, specific error naming the missing device.
			if (name.EndsWith(SimulatedSuffix, StringComparison.OrdinalIgnoreCase) || name.StartsWith("69.88.84.", StringComparison.Ordinal))
			{
				if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
				throw new InvalidOperationException($"You probably forgot to register simulated device '{name}' during startup of the test!");
			}

			// a raw IP that resolves to nothing is a connect scenario, not a name leak: leave the caller's normal failure path
			if (IPAddress.TryParse(name, out _)) return;

			// anything else is a real name that leaked into the sandbox: not a ".simulated" host, not a registered external host,
			// and not an intended Cut. Loud, so the harness bug is found instead of a request reaching a real server.
			if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
			throw new InvalidOperationException($"Real URI '{name}' reached the virtual network: not a '.simulated' host, not a registered external host, and not an intended Cut(pattern, NameResolution). This is a test-harness bug; inspect the code path that produced it.");
		}

		#endregion

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

			/// <summary>Host present in this location</summary>
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
				sb.AppendLineInvariant($"# - Hosts: {this.HostsById.Count:N0}");
				foreach (var host in this.HostsById.Values)
				{
					sb.AppendLineInvariant($"#   - {host}:");
					foreach (var adapter in host.Adapters)
					{
						host.Handlers.TryGetValue(adapter.Location.Id, out var ports);
						sb.AppendLineInvariant($"#     - {adapter.Location.Id}: {(ports != null ? string.Join(", ", ports.Keys) : "<none>")}");
					}
				}

				sb.AppendLineInvariant($"# - Locations: {this.Locations.Count:N0}");
				foreach (var loc in this.Locations.Values)
				{
					sb.AppendLineInvariant($"#   - {loc}");
					foreach (var host in loc.HostsById.Values)
					{
						sb.AppendLineInvariant($"#     - {host.Id}: {host.Fqdn}, {string.Join<IPAddress>(", ", host.Addresses)}, {string.Join<string>(", ", host.Aliases)}");
					}
				}

				return sb.ToString();
			}
		}

		/// <summary>The DNS suffix that marks a simulated (virtual) host. A name that ends in it routes virtually; a real name never does.</summary>
		internal const string SimulatedSuffix = ".simulated";

		/// <summary>Default DNS suffix minted for a host with no explicit FQDN on a network of the given type: the simulated networks mint <c>.simulated</c>; the external network mints nothing, because its hosts carry real names.</summary>
		public static string DefaultDnsSuffix(VirtualNetworkType type)
			=> type == VirtualNetworkType.External ? "" : SimulatedSuffix;

		/// <summary>Validates one name a host answers to against the egress rule of its network: the <c>lan</c>/<c>cloud</c> networks carry only <c>.simulated</c> names; the <c>external</c> network carries only real names.</summary>
		/// <exception cref="ArgumentException">If the name violates the network's egress rule.</exception>
		internal static void ValidateEgressName(VirtualNetworkType type, string name, string hostId)
		{
			// only FQDN-shaped names (with a domain part) carry the simulated/real distinction; a bare host name is exempt
			if (name.IndexOf('.') < 0) return;

			bool isSimulated = name.EndsWith(SimulatedSuffix, StringComparison.OrdinalIgnoreCase);
			switch (type)
			{
				case VirtualNetworkType.External:
				{
					if (isSimulated) throw new ArgumentException($"Cannot register '{name}' for host '{hostId}' on the external network: external hosts carry real names, never '{SimulatedSuffix}'.", nameof(name));
					break;
				}
				case VirtualNetworkType.LocalNetwork:
				case VirtualNetworkType.Cloud:
				{
					if (!isSimulated) throw new ArgumentException($"Cannot register '{name}' for host '{hostId}' on a simulated network: a real name reached the sandbox. A simulated host name must end in '{SimulatedSuffix}'; put a real endpoint on an external network (AddSimpleExternal).", nameof(name));
					break;
				}
				// Loopback / DataCenter / Unspecified carry no egress rule
			}
		}

		/// <summary>Returns the primary (non-loopback) network type of a host, used to classify the names and VIPs that point at it.</summary>
		internal static VirtualNetworkType PrimaryNetworkType(SimulatedHost host)
		{
			foreach (var loc in host.Locations)
			{
				if (loc.Type != VirtualNetworkType.Loopback) return loc.Type;
			}
			return VirtualNetworkType.Unspecified;
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

			fqdn ??= hostName + (identity.DnsSuffix ?? DefaultDnsSuffix(location.Type));

			var aliases = identity.Aliases.ToArray();
			var addresses = identity.Addresses.ToArray();

			// egress naming guard: keep real names off the simulated networks, and ".simulated" off the external network, so a
			// test cannot register a host whose name would let a request escape (or a real endpoint masquerade as simulated).
			// Validate before mutating any map so a rejection leaves no partial state.
			ValidateEgressName(location.Type, fqdn, id);
			foreach (var alias in aliases)
			{
				if (alias != null!) ValidateEgressName(location.Type, alias, id);
			}

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
				{ // automatically generates a "localhost" attached to this host
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
				ClassifyUnresolvedName(hostNameOrAddress); // loud if a real name leaked, friendly for an unregistered ".simulated"
				throw new SocketException(11001);          // an intended Cut or a raw IP: the normal quiet DNS failure
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
