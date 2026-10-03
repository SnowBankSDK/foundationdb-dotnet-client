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

namespace FoundationDB.Client.Tests
{
	using System.IO;
	using System.Net;
	using System.Net.Sockets;

	[TestFixture]
	public class ProbeFacts : FdbTest
	{

		[Test]
		public async Task Test_Probe_Returns_The_Protocol_Version_Of_The_Test_Cluster()
		{
			using var db = await OpenTestDatabaseAsync();
			var connectionString = await Fdb.System.GetCoordinatorsAsync(db, this.Cancellation);
			Log($"Coordinators: {connectionString}");

			var result = await Fdb.ProbeAsync(connectionString, WithTimeout());
			Log($"Probed: {result}");
			Assert.That(result.EndPoint, Is.EqualTo(connectionString.Coordinators[0]));

			// the native client is the reference
			var expected = await db.GetServerProtocolVersionAsync(this.Cancellation);
			Log($"Native: {expected} ({expected.ToVersion()})");
			Assert.That(result.ProtocolVersion.GetNormalizedVersion(), Is.EqualTo(expected.GetNormalizedVersion()));
			Assert.That(result.ProtocolVersion.ToVersion(), Is.EqualTo(expected.ToVersion()));
		}

		[Test]
		public async Task Test_Probe_Accepts_An_IPEndPoint()
		{
			var coordinator = await GetFirstCoordinatorAsync();

			var result = await Fdb.ProbeAsync(new IPEndPoint(coordinator.Address, coordinator.Port), WithTimeout());
			Log($"Probed: {result}");
			Assert.That(result.EndPoint, Is.EqualTo(coordinator));
			Assert.That(result.ProtocolVersion.IsValid(), Is.True);
		}

		[Test]
		public async Task Test_Probe_Resolves_A_DnsEndPoint()
		{
			var coordinator = await GetFirstCoordinatorAsync();
			Assume.That(coordinator.Address, Is.EqualTo(IPAddress.Loopback), "The test cluster must listen on 127.0.0.1");
			var resolved = await Dns.GetHostAddressesAsync("localhost");
			Log($"localhost resolves to: {string.Join(", ", resolved.Select(address => address.ToString()))}");

			// the test container listens on 127.0.0.1 only: when localhost resolves to ::1 first, the probe moves on to the next address
			var result = await Fdb.ProbeAsync(new DnsEndPoint("localhost", coordinator.Port), WithTimeout());
			Log($"Probed: {result}");
			Assert.That(result.EndPoint, Is.EqualTo(coordinator));
			Assert.That(result.ProtocolVersion.IsValid(), Is.True);
		}

		[Test]
		public void Test_Probe_Fails_When_The_Host_Name_Is_Unknown()
		{
			Assert.That(
				async () => await Fdb.ProbeAsync(new DnsEndPoint("fdb-probe-test.invalid", 4500), WithTimeout()),
				Throws.InstanceOf<SocketException>()
			);
		}

		[Test]
		public void Test_Probe_Fails_When_The_Reply_Is_Not_A_Handshake()
		{
			using var server = StartServer(static (client) => client.GetStream().Write("HTTP/1.1 400 Bad Request\r\n\r\n"u8.ToArray(), 0, 28));
			Assert.That(
				async () => await Fdb.ProbeAsync(server.EndPoint, this.Cancellation),
				Throws.InstanceOf<InvalidDataException>().With.Message.Contains("did not answer with a FoundationDB handshake")
			);
		}

		[Test]
		public void Test_Probe_Fails_When_The_Server_Closes_Without_Reply()
		{
			using var server = StartServer(static (_) => { });
			Assert.That(
				async () => await Fdb.ProbeAsync(server.EndPoint, this.Cancellation),
				Throws.InstanceOf<InvalidDataException>().With.Message.Contains("closed the connection without a FoundationDB handshake")
			);
		}

		[Test]
		public void Test_Probe_Is_Cancelled_When_The_Server_Does_Not_Answer()
		{
			using var server = StartServer(static (_) => Thread.Sleep(5_000));
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(this.Cancellation);
			cts.CancelAfter(TimeSpan.FromMilliseconds(500));
			Assert.That(
				async () => await Fdb.ProbeAsync(server.EndPoint, cts.Token),
				Throws.InstanceOf<OperationCanceledException>()
			);
		}

		[Test]
		public void Test_Probe_Fails_When_Nothing_Listens()
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			int port = ((IPEndPoint) listener.LocalEndpoint).Port;
			listener.Stop();

			Assert.That(
				async () => await Fdb.ProbeAsync(new FdbEndPoint(IPAddress.Loopback, port, tls: false), this.Cancellation),
				Throws.InstanceOf<SocketException>()
			);
		}

		[Test]
		public void Test_Probe_Does_Not_Support_Tls()
		{
			Assert.That(
				async () => await Fdb.ProbeAsync(new FdbEndPoint(IPAddress.Loopback, 4500, tls: true), this.Cancellation),
				Throws.InstanceOf<NotSupportedException>()
			);
		}

		private CancellationToken WithTimeout()
		{
			var cts = CancellationTokenSource.CreateLinkedTokenSource(this.Cancellation);
			cts.CancelAfter(TimeSpan.FromSeconds(10));
			return cts.Token;
		}

		private async Task<FdbEndPoint> GetFirstCoordinatorAsync()
		{
			using var db = await OpenTestDatabaseAsync();
			var connectionString = await Fdb.System.GetCoordinatorsAsync(db, this.Cancellation);
			return connectionString.Coordinators[0];
		}

		/// <summary>Accepts one connection on a local port, reads the handshake, runs <paramref name="reply"/>, then closes the connection</summary>
		private static FakeServer StartServer(Action<TcpClient> reply)
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			_ = Task.Run(async () =>
			{
				using var client = await listener.AcceptTcpClientAsync();
				var buffer = new byte[44];
				int received = 0, n;
				while (received < buffer.Length && (n = await client.GetStream().ReadAsync(buffer, received, buffer.Length - received)) > 0)
				{
					received += n;
				}
				reply(client);
			});
			return new FakeServer(listener);
		}

		private sealed class FakeServer : IDisposable
		{
			public FakeServer(TcpListener listener)
			{
				this.Listener = listener;
				this.EndPoint = new FdbEndPoint(IPAddress.Loopback, ((IPEndPoint) listener.LocalEndpoint).Port, tls: false);
			}

			private TcpListener Listener { get; }

			public FdbEndPoint EndPoint { get; }

			public void Dispose() => this.Listener.Stop();
		}

	}

}
