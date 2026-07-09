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
	using System.Net.NetworkInformation;
	using System.Net.Sockets;
	using SnowBank.Networking.Http;

	/// <summary>Implementation of an <see cref="INetworkMap"/> that interacts a real network</summary>
	/// <remarks>This method will pass-though all requests to the actual network stack of the host.</remarks>
	[PublicAPI]
	public class NetworkMap : INetworkMap
	{

		public NetworkMap(IClock? clock = null)
		{
			this.Clock = clock ?? SystemClock.Instance;
		}

		/// <inheritdoc />
		public IClock Clock { get; }

		/// <inheritdoc />
		public virtual HttpMessageHandler CreateTransportHandler(BetterHttpClientOptions options)
		{
			// socket-backed transport for the real network; the request URI decides the destination per-request. No filters or
			// per-request hooks are applied here (those belong to the client pipeline, ABOVE the transport) - only the per-bundle
			// socket-level knobs, applied ONCE on this shared handler from the resolved options.
			var handler = new BetterHttpClientHandler(this);

			// bound how stale a warm pooled connection's DNS may get: the platform pools connections per-origin, so without a
			// lifetime a long-lived client would keep reusing a cached endpoint. Set once, on the shared SocketsHttpHandler.
			handler.Sockets.PooledConnectionLifetime = BetterHttpDefaults.PooledConnectionLifetime;

			// wire the multi-IP racing / heatmap connect callback: it reads the endpoint heatmap from THIS map (map-scoped) and
			// the per-request context from request.Options - nothing per-client is stashed on the shared handler.
			handler.Setup();

			// per-bundle socket-level knobs (TLS/certs/cookies/proxy/redirects/decompression) from the resolved bundle options.
			return options.ConfigureTransport(handler);
		}

		[Obsolete("Use CreateTransportHandler instead")]
		public virtual HttpMessageHandler? CreateHttpHandler(string hostOrAddress, int port)
		{
			//by default, let the caller use its own handler
			return null;
		}

		/// <inheritdoc />
		[Obsolete("Use CreateTransportHandler instead")]
		public virtual HttpMessageHandler CreateBetterHttpHandler(Uri baseAddress, BetterHttpClientOptions options)
		{
			// Legacy bridge. The base address is ignored: the transport is target-agnostic and the request URI decides the
			// destination per-request. Kept behaviourally identical to the previous creation path (options.Configure over the
			// raw transport) so existing callers are unchanged until policy application relocates fully into the pipeline.
			return options.Configure(CreateTransportHandler(options));
		}

		/// <inheritdoc />
		public virtual async ValueTask<IPAddress?> GetPublicIPAddressForHost(string hostNameOrAddress, CancellationToken ct)
		{
			//REVIEW: this method is somewhat problematic, because of the following reasons:
			// => the DNS server may not be reachable, or may not be able to resolve the host name at this point in time
			// => the remote host may have a short-lived DHCP lease, and may change IP address while the current process is running
			// => the remote host may not be online when we call this method, and WINS or ARP-based resolution may fail
			// => Wi-Fi/4G connection may be down (on the road/train, going through a tunnel, ...)

			var hostEntry = await Dns.GetHostEntryAsync(hostNameOrAddress, ct).ConfigureAwait(false);
			if (hostEntry.AddressList.Length == 0)
			{
				return null;
			}

			if (!IPAddressHelpers.TryGetLocalAddressForRemoteAddress(hostEntry.AddressList.First(ip => ip.AddressFamily == AddressFamily.InterNetwork), out var localAddress))
			{
				return null;
			}
			return localAddress;
		}

		/// <inheritdoc />
		public virtual Task<IPHostEntry> DnsLookup(string hostNameOrAddress, AddressFamily? family, CancellationToken ct)
		{
			return Dns.GetHostEntryAsync(hostNameOrAddress, family ?? AddressFamily.Unspecified, ct);
		}

		/// <inheritdoc />
		public virtual IReadOnlyList<NetworkAdaptorDescriptor> GetNetworkAdaptors()
		{
			return NetworkInterface.GetAllNetworkInterfaces().Where(IsValidNetworkInterface).Select(x =>
			{
				var props = x.GetIPProperties();
				return new NetworkAdaptorDescriptor
				{
					Id = x.Id,
					Index = props.GetIPv4Properties().Index,
					Type = x.NetworkInterfaceType,
					Name = x.Name,
					Description = x.Description,
					PhysicalAddress = x.GetPhysicalAddress().ToString(),
					DnsSuffix = props.DnsSuffix,
					UnicastAddresses = props.UnicastAddresses
						.Select(info => new NetworkAdaptorDescriptor.UnicastAddressDescriptor()
						{
							Address = info.Address,
							IPv4Mask = info.IPv4Mask,
							PrefixLength = info.PrefixLength,
						})
						.ToArray(),
					Speed = x.Speed > 0 ? x.Speed : null,
				};
			}).ToArray();

		}

#if NET9_0_OR_GREATER
		private readonly Lock Lock = new();
#else
		private readonly object Lock = new();
#endif

		/// <summary>Map that stores, for each endpoint that was recently used, the <see cref="EndPointQuality"/> metrics for the connection.</summary>
		protected Dictionary<IPEndPoint, EndPointQuality> HeatMap { get; } = new(IPEndPointComparer.Default);

		/// <summary>Map that stores, for each target host, the details about the most recent successful connection</summary>
		protected Dictionary<string, (string HostName, IPEndPoint RemoteEndPoint, IPAddress? Local, Instant Timestamp)> LastEndPointMapping { get; } = new (StringComparer.Ordinal);

		/// <inheritdoc />
		public EndPointQuality? GetEndpointQuality(IPEndPoint endpoint)
		{
			lock (this.Lock)
			{
				return this.HeatMap.GetValueOrDefault(endpoint);
			}
		}

		/// <inheritdoc />
		public EndPointQuality RecordEndpointConnectionAttempt(IPEndPoint endpoint, TimeSpan duration, Exception? error, string? hostName, IPAddress? localAddress)
		{
			var now = this.Clock.GetCurrentInstant();
			lock (this.Lock)
			{
				if (!this.HeatMap.TryGetValue(endpoint, out var quality))
				{
					quality = new EndPointQuality(endpoint, historySize: 5);
					this.HeatMap[endpoint] = quality;
				}
				quality.Record(now, duration, error, hostName, localAddress);

				if (error == null)
				{ // if success, maybe update the details about this hostname...
					if (hostName != null)
					{
						this.LastEndPointMapping[hostName] = (hostName, endpoint, localAddress, now);
					}
				}

				return quality;
			}
		}

		/// <inheritdoc />
		public bool TryGetLastEndpointForHost(string hostName, out (IPEndPoint? EndPoint, IPAddress? LocalAddress, Instant Timestamp) infos)
		{
			lock (this.Lock)
			{
				if (!this.LastEndPointMapping.TryGetValue(hostName, out var details))
				{
					infos = default;
					return false;
				}

				infos = (details.RemoteEndPoint, details.Local, details.Timestamp);
				return true;
			}
		}

		/// <summary>Tests if a <see cref="NetworkInterface"/> is operational and supports IPv4</summary>
		public bool IsValidNetworkInterface(NetworkInterface net)
		{
			if (net.NetworkInterfaceType == NetworkInterfaceType.Loopback)
			{
				//REVIEW: in prod we don't report loopback adapters as valid "public" networks, but this may be required in dev mode when using Aspire, where all hosts use 127.0.0.1?
				return false;
			}

			if (!net.Supports(NetworkInterfaceComponent.IPv4))
			{
				return false;
			}

			if (net.OperationalStatus != OperationalStatus.Up)
			{
				return false;
			}

			return true;
		}

	}

	/// <summary>Details about the "quality" of a connection between this host, and a remote endpoint</summary>
	[DebuggerDisplay("EndPoint={EndPoint}, Score={Score}, Total={TotalConnections}, Errors={TotalErrors}")]
	[PublicAPI]
	public sealed class EndPointQuality
	{

		public EndPointQuality(IPEndPoint ep, int historySize)
		{
			Contract.NotNull(ep);
			Contract.GreaterThan(historySize, 0);

			this.EndPoint = ep;
			this.MovingDuration = new MovingSum<long>(historySize);
			this.MovingErrors = new MovingSum<double>(historySize);
		}

		/// <summary>Remote endpoint</summary>
		public IPEndPoint EndPoint { get; }

		/// <summary>Last computed score for this endpoint (lower == better)</summary>
		public double Score { get; private set; }

		/// <summary>Total number of connection attempts</summary>
		public long TotalConnections { get; private set; }

		/// <summary>Total number of failed connection attempts</summary>
		public long TotalErrors { get; private set; }

		/// <summary>Moving sum of the last connection attempts</summary>
		private MovingSum<long> MovingDuration;

		/// <summary>Moving sum of the last connection attempts (between 0 and 1, can be interpreted as the probability that the connection attempts fails)</summary>
		private MovingSum<double> MovingErrors;

		/// <summary>Time of the last attempt</summary>
		public Instant LastAttempt { get; private set; }

		/// <summary>Time of the end of a mandatory cooldown</summary>
		/// <remarks>The end point will have a very high score handicap until the end of the cooldown</remarks>
		public Instant? Cooldown { get; private set; }

		/// <summary>Last known local address used to connect to this endpoint</summary>
		public IPAddress? LastLocalAddress { get; private set; }

		/// <summary>Error if the last connection failed, or <see langword="null"/> if it was successfull</summary>
		public Exception? LastError { get; private set; }

		/// <summary>Last hostname that resolved to this endpoint</summary>
		public string? LastHostName { get; private set; }

		/// <summary>Computes a score that attempts to quantify the likelihood that is endpoint will be available and responsive, at the given time (lower == better)</summary>
		/// <param name="now">Moment in time when the connection attempt will be made (which could either right now, or sometime in the future once a retry cooldown expires)</param>
		/// <returns>Score that is lower if the host is expected to be reachable and fast, or higher if it is less likely (failed to resolve or timed out in the recent past)</returns>
		public double ComputeScore(Instant now)
		{
			var score = this.Score;
			if (this.Cooldown != null)
			{
				if (now < this.Cooldown.Value)
				{
					score += (this.Cooldown.Value - now).TotalSeconds;
				}
				else
				{
					this.Cooldown = null;
				}
			}
			return score;
		}

		/// <summary>Average duration of recent connection attempts</summary>
		public TimeSpan AverageDuration => this.TotalConnections == 0 ? TimeSpan.Zero : TimeSpan.FromTicks(this.MovingDuration.Total / this.MovingDuration.Count);

		/// <summary>Error rate recent connection attempts (normalized 0..1)</summary>
		public double AverageErrors => this.TotalConnections == 0 ? 0 : (this.MovingErrors.Total / this.MovingErrors.Count);

		/// <summary>Duration of the last connection attempt</summary>
		public TimeSpan LastDuration => TimeSpan.FromTicks(this.MovingDuration.Last);

		/// <summary>Has the last connection attempt failed?</summary>
		public bool LastFailed => this.MovingDuration.Last > 0.0;

		/// <summary>Records a new connection attempt</summary>
		/// <param name="now">Time of the attempt (usually when the connection was established, or the timeout expired)</param>
		/// <param name="duration">Duration of the connection attempt</param>
		/// <param name="error">Exception recorded if the attempt failed</param>
		/// <param name="hostName">Host name or ip address used for the connection attempt</param>
		/// <param name="localAddress">Address of the local network adapter used if known; otherwise, <see langword="null"/></param>
		public void Record(Instant now, TimeSpan duration, Exception? error, string? hostName, IPAddress? localAddress)
		{
			this.LastAttempt = now;
			this.TotalConnections++;
			if (error != null)
			{
				this.TotalErrors++;
			}

			var deltaError = error == null ? 0.0 : 1.0; //TODO: change the malus depending on the type and gravity of errors?
			var movingError = this.MovingErrors.Add(deltaError);
			var deltaDuration = duration.Ticks;
			var movingDuration = this.MovingDuration.Add(deltaDuration);

			this.Score = (TimeSpan.FromTicks(deltaDuration + (movingDuration.Total / 2)).TotalSeconds / movingDuration.Samples) * Math.Pow(1 + deltaError + (movingError.Total / 2), 2);
			if (error != null)
			{
				this.LastError = error;
				this.Cooldown = now.Plus(Duration.FromMinutes(2)); //TODO: use a different cooldown time depending on the type and gravity of errors?
			}
			else
			{
				this.LastError = null;
				this.Cooldown = null;
			}

			if (localAddress != null)
			{
				this.LastLocalAddress = localAddress;
			}

			if (hostName != null)
			{
				this.LastHostName = hostName;
			}
		}

	}

}
