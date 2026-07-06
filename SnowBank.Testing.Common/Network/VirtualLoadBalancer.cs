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

	/// <summary>Virtual network load balancer (L4-style): a public alias that routes each incoming connection to one of several backend hosts, under full test control</summary>
	/// <remarks>
	/// <para>This emulates the "public endpoint in front of a cluster" scenario: clients connect to the <see cref="Alias"/>
	/// and are silently routed to one of the <see cref="Backends"/>. Routing is DETERMINISTIC and test-driven: it only
	/// changes when the test calls <see cref="Route"/> / <see cref="ForceNextTarget"/> (or installs a
	/// <see cref="UseSelector">selector</see>); the balancer never rotates on its own.</para>
	/// <para>⚠ Resolution happens per REQUEST (the virtual network has no connection concept), so a selector MUST be a pure
	/// function of the client identity: a stateful selector (e.g. call-counting round-robin) would split one logical
	/// session's requests across different backends, which no real connection-level balancer would ever do.</para>
	/// <para>Backends are resolved lazily: registering the balancer does not require the backend hosts to exist yet (they
	/// are usually created later in the test setup); resolving a route to a missing host throws at connection time.</para>
	/// </remarks>
	[PublicAPI]
	public sealed class VirtualLoadBalancer
	{

		internal VirtualLoadBalancer(string id, string alias, string[] backends)
		{
			Contract.NotNullOrEmpty(id);
			Contract.NotNullOrEmpty(alias);
			Contract.NotNullOrEmpty(backends);

			this.Id = id;
			this.Alias = alias;
			this.Backends = backends;
		}

		/// <summary>Identifier of this balancer in the topology</summary>
		public string Id { get; }

		/// <summary>Public name the clients connect to (e.g. <c>"cluster.lan.simulated"</c>)</summary>
		public string Alias { get; }

		/// <summary>Ids of the backend hosts this balancer can route to</summary>
		public IReadOnlyList<string> Backends { get; }

		/// <summary>Guards the routing rules</summary>
		private object RulesLock { get; } = new();

		/// <summary>Per-client routing rules (client host id, or <c>"*"</c> for the wildcard, to backend host id)</summary>
		private Dictionary<string, string> Rules { get; } = new(StringComparer.OrdinalIgnoreCase);

		/// <summary>Optional callback deciding the backend for each resolution (takes precedence over the static rules)</summary>
		private Func<string?, VirtualLoadBalancer, string?>? Selector { get; set; }

		/// <summary>Raised whenever a routing rule changes (client, backend); the test framework surfaces these in the journal</summary>
		public event Action<VirtualLoadBalancer, string, string>? RouteChanged;

		/// <summary>Routes all future connections from a specific client host to a backend</summary>
		/// <param name="clientHostId">Id of the connecting host (e.g. <c>"AGENT-42"</c>), or <c>"*"</c> for every client without an exact rule</param>
		/// <param name="backendHostId">Id of the backend host that must serve this client from now on</param>
		/// <remarks>Only affects the NEXT connections: streams already established keep flowing to their original backend, like a real balancer that never migrates live connections.</remarks>
		public void Route(string clientHostId, string backendHostId)
		{
			Contract.NotNullOrEmpty(clientHostId);
			Contract.NotNullOrEmpty(backendHostId);
			if (!this.Backends.Contains(backendHostId, StringComparer.OrdinalIgnoreCase))
			{
				throw new ArgumentException($"Host '{backendHostId}' is not a backend of load balancer '{this.Id}' (backends: {string.Join(", ", this.Backends)})", nameof(backendHostId));
			}

			lock (this.RulesLock)
			{
				this.Rules[clientHostId] = backendHostId;
			}
			this.RouteChanged?.Invoke(this, clientHostId, backendHostId);
		}

		/// <summary>Routes all future connections (from clients without an exact rule) to a backend; shortcut for <c>Route("*", ...)</c></summary>
		public void ForceNextTarget(string backendHostId) => Route("*", backendHostId);

		/// <summary>Installs (or clears, with <c>null</c>) a callback deciding the backend for each resolution</summary>
		/// <param name="selector">Receives the connecting host id (or <c>null</c> when the source is unknown) and this balancer; returns the backend host id, or <c>null</c> to fall through to the static rules</param>
		/// <remarks>⚠ Must be a PURE function of the client identity (see the class remarks): resolution happens per request, so a stateful selector would split a session across backends.</remarks>
		public void UseSelector(Func<string?, VirtualLoadBalancer, string?>? selector)
		{
			lock (this.RulesLock)
			{
				this.Selector = selector;
			}
		}

		/// <summary>Resolves the backend that must serve a connection from the given client</summary>
		/// <param name="clientHostId">Id of the connecting host, or <c>null</c> when the source is not known</param>
		/// <returns>Id of the backend host (selector first, then exact rule, then wildcard rule, then the first backend)</returns>
		public string ResolveTarget(string? clientHostId)
		{
			lock (this.RulesLock)
			{
				if (this.Selector is { } selector)
				{
					var chosen = selector(clientHostId, this);
					if (chosen is not null)
					{
						return chosen;
					}
				}
				if (clientHostId is not null && this.Rules.TryGetValue(clientHostId, out var target))
				{
					return target;
				}
				if (this.Rules.TryGetValue("*", out target))
				{
					return target;
				}
				return this.Backends[0];
			}
		}

		/// <inheritdoc />
		public override string ToString() => $"LoadBalancer<{this.Id}>(Alias={this.Alias}, Backends=[{string.Join(", ", this.Backends)}])";

	}

}

#endif
