#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

		public VirtualHttpClientHandler(VirtualNetworkMap map, Uri baseAddress, BetterHttpClientOptions options)
		{
			this.Map = map;
			this.BaseAddress = baseAddress;
			this.Options = options;
		}

		/// <summary>Virtual network</summary>
		public VirtualNetworkMap Map { get; }

		/// <summary>Base address of the HTTP request</summary>
		public Uri BaseAddress { get; }

		/// <summary>Options used for the request</summary>
		public BetterHttpClientOptions Options { get; }

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
			var uri = request.RequestUri;
			if (uri == null)
			{
				uri = this.BaseAddress;
			}
			else if (!uri.IsAbsoluteUri)
			{
				uri = new Uri(this.BaseAddress, uri);
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

			if (host.Passthrough)
			{ // this is an actual real physical host, and the request will be sent "to the real world".
				var handler = new HttpClientHandler()
				{
					// ??
				};
				var configuredHandler = this.Options.Configure(handler);
				var invoker = new HttpMessageInvoker(configuredHandler);
				return invoker.SendAsync(request, cancellationToken);
			}

			// attempt to create a path from this host to the remote host
			// - "local" corresponds to the local network adapter by which the request would be sent
			// - "remote" corresponds to the remote network adapter on which the request would arrive
			// - if they are both the same, it means they are on the same physical network (or both localhost)
			var (local, remote) = this.Map.FindNetworkPath(host, hostName);

			if (local != null && remote != null)
			{ // we found a valid path through the virtual network!

				if (local.Type != VirtualNetworkType.LocalNetwork)
				{
					if (this.Map.Host.Offline)
					{ // We are offline!
						//TODO: maybe this would be a different error? if the local host has no online network adapter, the error may be different

						// => for now, simply simulate a generic connection timeout
						throw SimulateConnectFailure($"Local virtual host '{this.Map.Host.Id}' is currently marked as offline and cannot send any request to remote host {host.Id}.");
					}

					if (host.Offline)
					{ // the host is offline (rebooting? disconnected from ethernet/Wi-Fi?)

						//TODO: depending on the situation, we should either simulate a name resolution failure, OR a tcp connect timeout:
						// - if the DNS entry for the host is statically assigned, OR the caller still as the IP in cache from an earlier query, it would attempt to connect with the remote host, and fail with a timeout.
						// - if the host use DHCP, and/or has a very short TTL, and/or use WINS, then the caller would fail with a name resolution error.

						// => for now, simply assume that the DNS is static and/or already cached, and simulate a socket connection timeout (ie: the host was alive at some point and now suddenly became offline)
						throw SimulateConnectFailure($"Remote virtual host '{host.Id}' is currently marked as offline and will not respond to any request.");
					}
				}

				// ask the remote host if it can respond on the specified port
				int port = uri.Port;
				var factory = host.FindHandler(remote, port);
				if (factory != null)
				{
					var handler = factory();
					handler = this.Options.Configure(handler);
					var invoker = new HttpMessageInvoker(handler);
					return invoker.SendAsync(request, cancellationToken);
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

	}

}

#endif
