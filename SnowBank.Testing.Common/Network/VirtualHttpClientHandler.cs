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
	using System.Net;
	using System.Net.Http;
	using SnowBank.Networking.Http;

	/// <summary>HTTP handler implementation that emulates requests to <see cref="IVirtualNetworkHost">virtual hosts</see> through a <see cref="IVirtualNetworkMap">virtual network</see></summary>
	internal class VirtualHttpClientHandler : DelegatingHandler
	{

		public VirtualHttpClientHandler(VirtualNetworkMap map, BetterHttpClientOptions options)
		{
			this.Map = map;
			this.Options = options;
		}

		/// <summary>Virtual network</summary>
		public VirtualNetworkMap Map { get; }

		/// <summary>Options used for the request</summary>
		public BetterHttpClientOptions Options { get; }

		/// <summary>Real-network handler used to service <see cref="IVirtualNetworkHost.Passthrough">passthrough</see> hosts.</summary>
		/// <remarks>Created lazily on the first passthrough request and reused for the life of this transport: the previous code
		/// built a fresh <see cref="HttpClientHandler"/> (and <see cref="HttpMessageInvoker"/>) on EVERY request, leaking a
		/// socket pool each time. Disposed in <see cref="Dispose(bool)"/>.</remarks>
		private HttpMessageHandler? PassthroughHandler { get; set; }

		/// <summary>Guards the lazy creation of <see cref="PassthroughHandler"/>.</summary>
		private readonly object PassthroughLock = new();

		/// <summary>Returns the shared real-network handler for passthrough hosts, creating and configuring it once.</summary>
		private HttpMessageHandler GetOrCreatePassthroughHandler()
		{
			var handler = this.PassthroughHandler;
			if (handler is not null) return handler;

			lock (this.PassthroughLock)
			{
				return this.PassthroughHandler ??= this.Options.Configure(new HttpClientHandler());
			}
		}

		/// <summary>Simulated source port for this handler, used only to give the host a distinct peer address in the
		/// <c>X-SBK-ORIGIN</c> tag (see <see cref="SendAsync"/>).</summary>
		/// <remarks>Stable for the life of the handler - i.e. one port per virtualized client/channel. This handler is the
		/// generic transport for ALL virtual HTTP and cannot observe the real socket/connection lifecycle from inside
		/// <c>SendAsync</c>, so it does NOT try to model "a reconnect opens a new ephemeral port". Distinguishing a
		/// reconnecting peer from a new one is the protocol's job (a stable connection id in the handshake), not the
		/// transport's.</remarks>
		private int SourcePort { get; } = AllocateSourcePort();

		private static int SourcePortCounter;

		/// <summary>Allocates a distinct simulated source port (in the 49152-65535 range) for a new virtualized client.</summary>
		private static int AllocateSourcePort() => 49152 + (System.Threading.Interlocked.Increment(ref SourcePortCounter) & 0x3FFF);

		protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			// note: we _could_ support non-async request, but I think this would be enough pressure to force the caller to transition to async requests!
			throw new NotImplementedException("Non async client not supported. Why do you need it anyway?");
		}

		/// <summary>Create an exception that replicates a failed Host Name or DNS resolution.</summary>
		/// <param name="hostName">Name of the host that could not be resolved (as part of the request URI)</param>
		/// <param name="debugReason">Text description of the cause of this error (for troubleshooting)</param>
		/// <returns>Returns an <see cref="HttpRequestException"/> with message <c>"An error occurred while sending the request."</c>, with an inner <see cref="WebException"/> with status <see cref="WebExceptionStatus.NameResolutionFailure"/> and message <c>"The remote name could not be resolved: '<paramref name="hostName"/>'"</c></returns>
		public static Exception SimulateNameResolutionError(string hostName, string debugReason)
		{
			var webEx = new WebException($"The remote name could not be resolved: '{hostName}'", WebExceptionStatus.NameResolutionFailure);
			return new HttpRequestException($"An error occurred while sending the request. [{debugReason}]", webEx);
		}

		/// <summary>Create an exception that replicates a tcp connection to a remote host that is alive, but with no remote service bound to the specified port</summary>
		/// <param name="hostName">Name of the remote host (part of the exception message)</param>
		/// <param name="port">Port of the service (part of the exception message)</param>
		/// <param name="debugReason">Text description of the cause of this error (for troubleshooting)</param>
		/// <returns>Returns an <see cref="HttpRequestException"/> with message <c>"An error occurred while sending the request."</c>, with an inner <see cref="WebException"/> with status <see cref="WebExceptionStatus.ConnectFailure"/> and message <c>"No connection could be made because the target machine actively refused it <paramref name="hostName"/>:<paramref name="port"/>"</c></returns>
		public static Exception SimulatePortNotBoundFailure(string hostName, int port, string debugReason)
		{
			var webEx = new WebException($"No connection could be made because the target machine actively refused it {hostName}:{port}", WebExceptionStatus.ConnectFailure);
			return new HttpRequestException($"An error occurred while sending the request. [{debugReason}]", webEx);
		}

		/// <summary>Create an exception that replicates a tcp socket connection timeout</summary>
		/// <param name="debugReason">Text description of the cause of this error (for troubleshooting)</param>
		/// <returns>Returns an <see cref="HttpRequestException"/> with message <c>"An error occurred while sending the request."</c>, with an inner <see cref="WebException"/> with status <see cref="WebExceptionStatus.ConnectFailure"/> and message <c>"Unable to connect to the remove server"</c>, and itself with an inner <see cref="System.Net.Sockets.SocketException"/> with error code <c>10060</c> (<c>TimedOut</c>)</returns>
		public static Exception SimulateConnectFailure(string debugReason)
		{
			var sockEx = new System.Net.Sockets.SocketException(10060); // TimedOut
			var webEx = new System.Net.WebException("Unable to connect to the remove server", sockEx, WebExceptionStatus.ConnectFailure, null);
			return new HttpRequestException($"An error occurred while sending the request. [{debugReason}]", webEx);
		}

		/// <inheritdoc />
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			// The transport is target-agnostic: nothing about the destination is captured at construction, so the request MUST
			// carry an absolute URI (HttpClient resolves relative URIs against its BaseAddress before the handler runs). A bare
			// invoker with a relative/missing URI has no base to resolve against - fail loudly rather than silently mis-route.
			var uri = request.RequestUri;
			if (uri is null || !uri.IsAbsoluteUri)
			{
				throw new InvalidOperationException("The virtual transport requires an absolute request URI (set HttpClient.BaseAddress or send an absolute URI).");
			}

			// From the host name in the URI of the request, we will check:
			// - if the remote host "exists" in the virtual network topology,
			// - if we can resolve this into an IP address,
			// - if we know the network location that correspond to this IP address,
			// - if the source host can reach this network location,
			// - if the remote host is offline or online
			// - if the port on the remote host is bound to any HTTP service
			// If all the conditions are met, then we will generate a virtual http handler that invokes that service.
			// If not, then we will throw an exception that attempts to replicate the actual exception that would happen in "real life".

			string hostName = uri.DnsSafeHost;
			var host = this.Map.FindHost(hostName);
			if (host == null)
			{ // this host is not defined in the network map
				throw SimulateNameResolutionError(hostName, $"Found no matching host for name '{hostName}' visible from simulated host '{this.Map.Host.Id}' ({this.Map.Host.Fqdn})");
			}

			// The in-process virtual transport has no real socket, so the server's HttpContext.Connection.RemoteIpAddress
			// (and therefore gRPC's ServerCallContext.Peer) would be unset - making every client look like the same "unknown"
			// peer. Tag the request with this host's address as an X-SBK-ORIGIN header so VirtualNetworkProxyMiddleware can
			// reconstruct RemoteIpAddress/RemotePort on the server side (the "Forwarded-For" trick). We only add it when the
			// caller did not already set it: REST/SignalR clients tag via BetterHttpClientOptions.DefaultRequestHeaders, but
			// the gRPC channel bypasses those (it uses the raw handler), so this is what makes distinct gRPC peers possible.
			if (!request.Headers.Contains("X-SBK-ORIGIN"))
			{
				// Use this host's own primary address as the simulated source. We deliberately do NOT use
				// GetPublicIPAddressForHost (which only resolves a source IP for same-network peers and returns null across
				// networks, e.g. @lan -> @cloud): for peer DISTINCTION any stable, unique-per-connection address works, and
				// the origin's own address + SourcePort is distinct across hosts AND across connections from the same host.
				var originIp = this.Map.Host.Addresses.Length > 0 ? this.Map.Host.Addresses[0] : System.Net.IPAddress.Loopback;
				request.Headers.TryAddWithoutValidation(
					"X-SBK-ORIGIN",
					string.CreateInvariant($"\"{this.Map.Host.Id}\"; host=\"{this.Map.Host.Fqdn}\"; peer=\"{originIp}:{this.SourcePort}\"")
				);
			}

			if (host.Passthrough)
			{ // this is an actual real physical host, and the request will be sent "to the real world".
				// Reuse ONE cached real-network handler (see PassthroughHandler) instead of building a fresh handler + invoker
				// - and leaking its socket pool - on every request. The invoker is a cheap throwaway wrapper; disposeHandler:
				// false keeps the cached handler alive across calls.
				var invoker = new HttpMessageInvoker(GetOrCreatePassthroughHandler(), disposeHandler: false);
				return invoker.SendAsync(request, cancellationToken);
			}

			// attempt to create a path from this host to the remote host
			// - "local" corresponds to the local network adapter by which the request would be sent
			// - "remote" corresponds to the remote network adapter on which the request would arrive
			// - if they are both the same, it means they are on the same physical network (or both localhost)
			var (local, remote) = this.Map.FindNetworkPath(host, hostName);

			if (local != null && remote != null)
			{ // we found a valid path through the virtual network!

				// Offline/started state is resolved PER-REQUEST against the live host, so a client held across a node stop/start
				// sees the outage (and the recovery) on its very next request. This now runs on every path, not just non-LAN
				// hops: a stopped node tears down its listeners and drops off the network in every direction - previously a
				// stopped host reached over the LAN leaked the binding's "server not ready" error instead of a network fault.
				// The simulated fault shapes are unchanged.
				if (this.Map.Host.Offline)
				{ // the local host has no network -> it cannot send anything
					//TODO: maybe this would be a different error? if the local host has no online network adapter, the error may be different

					// => for now, simply simulate a generic connection timeout
					throw SimulateConnectFailure($"Local virtual host '{this.Map.Host.Id}' is currently marked as offline and cannot send any request to remote host {host.Id}.");
				}

				if (host.Offline)
				{ // the remote host is offline (stopped? rebooting? disconnected from ethernet/Wi-Fi?)

					//TODO: depending on the situation, we should either simulate a name resolution failure, OR a tcp connect timeout:
					// - if the DNS entry for the host is statically assigned, OR the caller still as the IP in cache from an earlier query, it would attempt to connect with the remote host, and fail with a timeout.
					// - if the host use DHCP, and/or has a very short TTL, and/or use WINS, then the caller would fail with a name resolution error.

					// => for now, simply assume that the DNS is static and/or already cached, and simulate a socket connection timeout (ie: the host was alive at some point and now suddenly became offline)
					throw SimulateConnectFailure($"Remote virtual host '{host.Id}' is currently marked as offline and will not respond to any request.");
				}

				// ask the remote host if it can respond on the specified port
				int port = uri.Port;
				var factory = host.FindHandler(remote, port);
				if (factory != null)
				{
					var handler = factory();
					handler = this.Options.Configure(handler);
					var invoker = new HttpMessageInvoker(handler);

					// Link the call to BOTH endpoints' "online" tokens, so that if either host goes offline mid-flight, the
					// in-flight request - and, for a long-lived gRPC duplex stream, the whole connection in both directions -
					// is aborted, like a severed TCP link. The Offline checks above only reject NEW connections; this is what
					// severs ESTABLISHED ones (the connect path never re-runs for a live stream). Tokens are captured here, so
					// a later offline/online cycle leaves this connection aborted (latched) while a freshly-opened one uses
					// the renewed token.
					var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.Map.Host.OnlineToken, host.OnlineToken);
					// `linked` registers callbacks on the hosts' long-lived OnlineTokens, so it MUST be disposed or they pile
					// up. It has to outlive SendAsync (a streaming body is read after the headers return), so we release it when
					// the body is done: its read stream ends/errors/disposes, or the response is disposed (see SendAndReleaseAsync).
					return SendAndReleaseAsync(invoker, request, linked);
				}

				throw SimulatePortNotBoundFailure(hostName, port, $"Found no port {port} bound on location '{remote}' of target host '{host.Id}', visible from host '{this.Map.Host.Id}' ({this.Map.Host.Fqdn})");
			}

			// we don't have a valid path between the local and remote host
			if (IPAddress.TryParse(hostName, out var ip))
			{ // request included the IP ("https://1.2.3.4/...") so it would fail with a socket connection timeout or maybe a "bad gateway"
				throw SimulateConnectFailure($"Found not matching host for IP {ip} visible from host '{this.Map.Host.Id}' ({this.Map.Host.Fqdn})");
			}
			else
			{ // request included a host name ("https://somehost/...") so it would most probably fail the name resolution
				throw SimulateNameResolutionError(hostName, $"Found no matching host for name '{hostName}' visible from simulated host '{this.Map.Host.Id}' ({this.Map.Host.Fqdn})");
			}
		}

		/// <summary>Sends the request and releases <paramref name="linked"/> (unlinking it from the hosts' long-lived
		/// OnlineTokens) when the response is disposed. The linked source must outlive <c>SendAsync</c> (a streaming body is
		/// read after the headers return), so its lifetime is tied to the response rather than to this call.</summary>
		/// <remarks>NOTE: this still relies on the consumer disposing the response. A long-lived connection whose consumer
		/// never disposes its response (e.g. a SignalR connection kept open for the host's whole life) holds its linked
		/// source until GC. That is bounded (one per live connection) and not a per-request leak - completed and
		/// reconnected calls release deterministically. Releasing those at host-teardown would require either the sinks
		/// disposing their calls/connections, or a host-teardown signal plumbed into the virtual transport.</remarks>
		private static async Task<HttpResponseMessage> SendAndReleaseAsync(HttpMessageInvoker invoker, HttpRequestMessage request, CancellationTokenSource linked)
		{
			try
			{
				var response = await invoker.SendAsync(request, linked.Token).ConfigureAwait(false);
				if (response.Content is not null)
				{
					response.Content = new ReleasingContent(response.Content, linked);
				}
				else
				{ // no body to attach the lifetime to -> release now
					linked.Dispose();
				}
				return response;
			}
			catch
			{ // SendAsync faulted before ownership was handed off -> release now
				linked.Dispose();
				throw;
			}
		}

		/// <inheritdoc />
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				this.PassthroughHandler?.Dispose();
				this.PassthroughHandler = null;
			}
			base.Dispose(disposing);
		}

		/// <summary>Forwards an <see cref="HttpContent"/> and disposes the linked token source when the content is disposed.</summary>
		private sealed class ReleasingContent : HttpContent
		{
			private readonly HttpContent Inner;
			private readonly CancellationTokenSource Linked;

			public ReleasingContent(HttpContent inner, CancellationTokenSource linked)
			{
				this.Inner = inner;
				this.Linked = linked;
				foreach (var header in inner.Headers)
				{
					this.Headers.TryAddWithoutValidation(header.Key, header.Value);
				}
			}

			protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => this.Inner.CopyToAsync(stream);

			protected override Task<Stream> CreateContentReadStreamAsync() => this.Inner.ReadAsStreamAsync();

			protected override bool TryComputeLength(out long length)
			{
				var len = this.Inner.Headers.ContentLength;
				length = len ?? 0;
				return len.HasValue;
			}

			protected override void Dispose(bool disposing)
			{
				if (disposing)
				{
					this.Inner.Dispose();
					this.Linked.Dispose();
				}
				base.Dispose(disposing);
			}
		}

	}

}

#endif
