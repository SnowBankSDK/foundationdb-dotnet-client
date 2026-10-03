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

namespace FoundationDB.Client
{
	using System.Buffers.Binary;
	using System.IO;
	using System.Net;
	using System.Net.Sockets;

	public static partial class Fdb
	{

		// probe handshake: 6.1 protocol version, with the object serializer flag: no supported cluster runs it, and every server from 6.3 to 8.0 answers it with its own handshake
		private const ulong PROBE_HANDSHAKE_PROTOCOL_VERSION = 0x1FDB00B061000000UL;

		// connectPacketLength, protocolVersion, canonicalRemotePort, connectionId, canonicalRemoteIp4, flags, canonicalRemoteIp6
		private const int PROBE_HANDSHAKE_BODY_SIZE = 8 + 2 + 8 + 4 + 2 + 16;

		/// <summary>Checks that a coordinator of the cluster answers, and returns its address and the protocol version of the cluster</summary>
		/// <param name="connectionString">Connection string of the cluster (the content of its cluster file)</param>
		/// <param name="ct">Token used to cancel the probe. A coordinator that accepts the connection but never answers blocks the probe until this token is cancelled, so pass a token with a timeout.</param>
		/// <returns>The first coordinator that answered, and the protocol version of the cluster</returns>
		/// <exception cref="NotSupportedException">The coordinator expects TLS connections.</exception>
		/// <exception cref="InvalidDataException">The coordinator did not answer with a FoundationDB handshake.</exception>
		/// <exception cref="SocketException">The coordinator could not be reached.</exception>
		/// <exception cref="AggregateException">None of the coordinators answered: the inner exceptions hold the error of each coordinator.</exception>
		/// <remarks><inheritdoc cref="ProbeAsync(EndPoint, CancellationToken)" path="/remarks/node()"/></remarks>
		public static Task<FdbProbeResult> ProbeAsync(FdbClusterConnectionString connectionString, CancellationToken ct)
		{
			Contract.NotNull(connectionString);
			return ProbeFirstAsync(connectionString.Coordinators, $"coordinators of cluster '{connectionString.Description}'", ct);
		}

		/// <summary>Checks that a FoundationDB process answers at this address, and returns the address that answered and its protocol version</summary>
		/// <param name="endPoint">Address of a coordinator, or of any process of the cluster: an <see cref="IPEndPoint"/>, a <see cref="DnsEndPoint"/> or an <see cref="FdbEndPoint"/></param>
		/// <param name="ct">Token used to cancel the probe. A process that accepts the connection but never answers blocks the probe until this token is cancelled, so pass a token with a timeout.</param>
		/// <returns>The address that answered (for a <see cref="DnsEndPoint"/>, the first resolved IP address that answered), and its protocol version</returns>
		/// <exception cref="NotSupportedException">The address expects TLS connections, or <paramref name="endPoint"/> is another kind of endpoint.</exception>
		/// <exception cref="InvalidDataException">The process did not answer with a FoundationDB handshake.</exception>
		/// <exception cref="SocketException">The process could not be reached, or the host name could not be resolved.</exception>
		/// <exception cref="AggregateException">The host name resolved to several addresses, and none answered: the inner exceptions hold the error of each address.</exception>
		/// <remarks>
		/// <para>The probe opens a TCP connection and sends the connection handshake of a FoundationDB 6.1 process. Every server from 6.3 to 8.0 answers an incompatible client with its own handshake, which starts with its protocol version: this is how the multi-version client discovers the version of a cluster.</para>
		/// <para>The probe reads only the length and the protocol version at the start of the reply, which have the same layout from 6.3 to 8.0. It does not need the native client library, nor <see cref="Start()"/>.</para>
		/// <para>An answer proves that a FoundationDB process listens at the address. It does not prove that the database accepts transactions: a cluster in recovery, or with a single coordinator up, also answers.</para>
		/// <para>Each probe appears on the server as an incompatible connection: a <c>ConnectionRejected</c> trace event, and an entry in the <c>incompatible_connections</c> list of the status for a while.</para>
		/// </remarks>
		public static async Task<FdbProbeResult> ProbeAsync(EndPoint endPoint, CancellationToken ct)
		{
			Contract.NotNull(endPoint);

			switch (endPoint)
			{
				case FdbEndPoint fdb:
				{
					return new(fdb, await ProbeAddressAsync(fdb, ct).ConfigureAwait(false));
				}
				case IPEndPoint ip:
				{
					var fdb = new FdbEndPoint(ip.Address, ip.Port, tls: false);
					return new(fdb, await ProbeAddressAsync(fdb, ct).ConfigureAwait(false));
				}
				case DnsEndPoint dns:
				{
#if NET6_0_OR_GREATER
					var addresses = await Dns.GetHostAddressesAsync(dns.Host, ct).ConfigureAwait(false);
#else
					var addresses = await Dns.GetHostAddressesAsync(dns.Host).ConfigureAwait(false);
#endif
					var candidates = addresses
						.Where(address => dns.AddressFamily == AddressFamily.Unspecified || address.AddressFamily == dns.AddressFamily)
						.Select(address => new FdbEndPoint(address, dns.Port, tls: false))
						.ToArray();
					if (candidates.Length == 0)
					{
						throw new SocketException((int) SocketError.HostNotFound);
					}
					return await ProbeFirstAsync(candidates, $"addresses of '{dns.Host}'", ct).ConfigureAwait(false);
				}
				default:
				{
					throw new NotSupportedException($"Probing an endpoint of type {endPoint.GetType().Name} is not supported. Pass an IPEndPoint, a DnsEndPoint or an FdbEndPoint.");
				}
			}
		}

		/// <summary>Probes each address in order, and returns the first one that answers</summary>
		private static async Task<FdbProbeResult> ProbeFirstAsync(FdbEndPoint[] candidates, string description, CancellationToken ct)
		{
			if (candidates.Length == 1)
			{
				return new(candidates[0], await ProbeAddressAsync(candidates[0], ct).ConfigureAwait(false));
			}

			var errors = new List<Exception>(candidates.Length);
			foreach (var candidate in candidates)
			{
				try
				{
					return new(candidate, await ProbeAddressAsync(candidate, ct).ConfigureAwait(false));
				}
				catch (Exception e) when (e is not OperationCanceledException)
				{
					errors.Add(e);
				}
			}
			throw new AggregateException($"None of the {candidates.Length} {description} answered with a FoundationDB handshake.", errors);
		}

		/// <summary>Sends the probe handshake to one address, and decodes the protocol version of the reply</summary>
		private static async Task<FdbProtocolVersion> ProbeAddressAsync(FdbEndPoint endPoint, CancellationToken ct)
		{
			if (endPoint.Tls)
			{
				throw new NotSupportedException($"Probing the coordinator {endPoint} is not supported, because it expects TLS connections.");
			}
			ct.ThrowIfCancellationRequested();

			using var client = new TcpClient(endPoint.AddressFamily);
			// disposing the client aborts a pending connect or read on every target framework
			using var registration = ct.Register(static (state) => ((TcpClient) state!).Dispose(), client);
			try
			{
				await client.ConnectAsync(endPoint.Address, endPoint.Port).ConfigureAwait(false);
				var stream = client.GetStream();

				var request = new byte[4 + PROBE_HANDSHAKE_BODY_SIZE];
				BinaryPrimitives.WriteUInt32LittleEndian(request, PROBE_HANDSHAKE_BODY_SIZE);
				BinaryPrimitives.WriteUInt64LittleEndian(request.AsSpan(4), PROBE_HANDSHAKE_PROTOCOL_VERSION);
				await stream.WriteAsync(request, 0, request.Length, ct).ConfigureAwait(false);

				// the reply starts with the length of the handshake (uint32) and the protocol version of the server (uint64)
				var reply = new byte[12];
				int received = 0;
				while (received < reply.Length)
				{
					int n = await stream.ReadAsync(reply, received, reply.Length - received, ct).ConfigureAwait(false);
					if (n == 0)
					{
						throw new InvalidDataException(received == 0
							? $"The process at {endPoint} closed the connection without a FoundationDB handshake. It may expect TLS connections, or not be a FoundationDB process."
							: $"The process at {endPoint} closed the connection after {received} bytes, in the middle of a FoundationDB handshake: <{BitConverter.ToString(reply, 0, received)}>");
					}
					received += n;
				}

				uint length = BinaryPrimitives.ReadUInt32LittleEndian(reply);
				ulong version = BinaryPrimitives.ReadUInt64LittleEndian(reply.AsSpan(4));
				// every protocol version starts with 0x0FDB00 (after the flags), and the shortest handshake has a 10 bytes body (CONNECT_PACKET_V0_SIZE in FlowTransport.cpp)
				if (((version >> 40) & 0x0FFFFF) != 0x0FDB00 || length < 10 || length > 4096)
				{
					throw new InvalidDataException($"The process at {endPoint} did not answer with a FoundationDB handshake: <{BitConverter.ToString(reply)}>");
				}
				return new FdbProtocolVersion(version);
			}
			catch (Exception e) when (ct.IsCancellationRequested && e is ObjectDisposedException or SocketException or IOException)
			{
				throw new OperationCanceledException(ct);
			}
		}

	}

}
