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

namespace SnowBank.Testing.Framework.Tests
{
	using System.IO;
	using System.Net;
	using System.Net.Sockets;
	using Microsoft.AspNetCore.Builder;
	using Microsoft.AspNetCore.Http;
	using NUnit.Framework;
	using SnowBank.Networking;
	using SnowBank.Networking.Http;

	/// <summary>Self-tests for the DIRECTIONAL fault injection of the virtual network (<see cref="VirtualNetworkTopology.Cut"/>):
	/// cutting the A→B edge must fail only A's traffic towards B - the reverse direction keeps flowing, which is what makes
	/// asymmetric-fault scenarios (the reason failure detectors exist) representable. The IMMEDIATE fault kinds must produce
	/// the same exception shapes as their real-life socket counterparts, and a severed edge must also abort the streams that
	/// were established over it while it was healthy.</summary>
	[TestFixture]
	public class VirtualNetworkFaultFacts : DistributedTest
	{

		[Test]
		public async Task Test_Directional_Cut_Fails_Only_The_Cut_Direction()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("ALICE", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/ping", (HttpContext _) => "pong-alice"));
				});
				lan.WithMinimalWebHost("BOB", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/ping", (HttpContext _) => "pong-bob"));
				});
			}));
			var alice = context.GetWebHost("ALICE");
			var bob = context.GetWebHost("BOB");

			// one long-lived client per direction, resolving through each side's own map (like a real process would)
			var mapA = (VirtualNetworkMap) alice.GetRequiredService<INetworkMap>();
			var mapB = (VirtualNetworkMap) bob.GetRequiredService<INetworkMap>();
			using var clientA = new HttpClient(mapA.CreateTransportHandler(new BetterHttpClientOptions()), disposeHandler: false);
			using var clientB = new HttpClient(mapB.CreateTransportHandler(new BetterHttpClientOptions()), disposeHandler: false);

			var uriBob = bob.GetUri("/ping");
			var uriAlice = alice.GetUri("/ping");

			// both directions healthy first
			Assert.That(await clientA.GetStringAsync(uriBob, this.Cancellation), Is.EqualTo("pong-bob"), "A -> B must flow before the cut");
			Assert.That(await clientB.GetStringAsync(uriAlice, this.Cancellation), Is.EqualTo("pong-alice"), "B -> A must flow before the cut");

			// cut ONLY the A -> B direction
			context.Topology.Cut("ALICE", "BOB", VirtualNetworkFault.Severed);

			var ex = Assert.ThrowsAsync<HttpRequestException>(async () => await clientA.GetStringAsync(uriBob, this.Cancellation),
				"the cut direction must fail");
			// the Severed shape is "connection reset by peer": a WebException wrapping a SocketException(ConnectionReset)
			Assert.That(ex!.InnerException, Is.InstanceOf<WebException>(), "a severed link must wrap a WebException");
			Assert.That(ex.InnerException!.InnerException, Is.InstanceOf<SocketException>(), "a severed link must carry a socket error");
			Assert.That(((SocketException) ex.InnerException.InnerException!).SocketErrorCode, Is.EqualTo(SocketError.ConnectionReset),
				"a severed link must read as 'connection reset by peer'");

			// the OTHER direction is untouched: this is what makes the cut DIRECTIONAL
			Assert.That(await clientB.GetStringAsync(uriAlice, this.Cancellation), Is.EqualTo("pong-alice"),
				"the reverse direction must keep flowing while A -> B is cut");

			// plugging the cable back in heals the SAME client (per-request evaluation, no client re-creation)
			context.Topology.Restore("ALICE", "BOB");
			Assert.That(await clientA.GetStringAsync(uriBob, this.Cancellation), Is.EqualTo("pong-bob"),
				"restoring the edge must heal the SAME client's next request");
		}

		[Test]
		public async Task Test_Immediate_Fault_Shapes_Match_Their_Real_Life_Counterparts()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("CLI", host => host.ConfigureApplication(_ => { }));
				lan.WithMinimalWebHost("API", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/ping", (HttpContext _) => "pong"));
				});
			}));
			var cli = context.GetWebHost("CLI");
			var api = context.GetWebHost("API");

			var map = (VirtualNetworkMap) cli.GetRequiredService<INetworkMap>();
			using var client = new HttpClient(map.CreateTransportHandler(new BetterHttpClientOptions()), disposeHandler: false);
			var uri = api.GetUri("/ping");

			Assert.That(await client.GetStringAsync(uri, this.Cancellation), Is.EqualTo("pong"), "healthy link answers");

			// Refused: the target actively refuses new connections (RST on SYN)
			context.Topology.Cut("CLI", "API", VirtualNetworkFault.Refused);
			var refused = Assert.ThrowsAsync<HttpRequestException>(async () => await client.GetStringAsync(uri, this.Cancellation),
				"a refused link must fail new connections");
			Assert.That(refused!.InnerException, Is.InstanceOf<WebException>());
			Assert.That(((WebException) refused.InnerException!).Status, Is.EqualTo(WebExceptionStatus.ConnectFailure),
				"refused must read as a connect failure");
			Assert.That(refused.InnerException!.InnerException, Is.InstanceOf<SocketException>());
			Assert.That(((SocketException) refused.InnerException.InnerException!).SocketErrorCode, Is.EqualTo(SocketError.ConnectionRefused),
				"refused must read as 'actively refused'");

			// NameResolution: the name no longer resolves (stale DNS, dead resolver)
			context.Topology.Cut("CLI", "API", VirtualNetworkFault.NameResolution);
			var dns = Assert.ThrowsAsync<HttpRequestException>(async () => await client.GetStringAsync(uri, this.Cancellation),
				"a dns-cut link must fail new connections");
			Assert.That(dns!.InnerException, Is.InstanceOf<WebException>());
			Assert.That(((WebException) dns.InnerException!).Status, Is.EqualTo(WebExceptionStatus.NameResolutionFailure),
				"the dns flavor must read as a name-resolution failure");

			// restore heals
			context.Topology.Restore("CLI", "API");
			Assert.That(await client.GetStringAsync(uri, this.Cancellation), Is.EqualTo("pong"), "restore must heal the link");
		}

		[Test]
		public async Task Test_Severed_Cut_Aborts_An_Established_Stream()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("CLI", host => host.ConfigureApplication(_ => { }));
				lan.WithMinimalWebHost("API", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/stream", async (HttpContext ctx) =>
					{
						// a long-lived streaming response ("ticks" forever), like an SSE feed or a duplex pump
						ctx.Response.ContentType = "text/plain";
						while (!ctx.RequestAborted.IsCancellationRequested)
						{
							await ctx.Response.WriteAsync("tick\n", ctx.RequestAborted);
							await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
							await Task.Delay(25, ctx.RequestAborted);
						}
					}));
				});
			}));
			var cli = context.GetWebHost("CLI");
			var api = context.GetWebHost("API");

			var map = (VirtualNetworkMap) cli.GetRequiredService<INetworkMap>();
			using var client = new HttpClient(map.CreateTransportHandler(new BetterHttpClientOptions()), disposeHandler: false);

			using var response = await client.GetAsync(api.GetUri("/stream"), HttpCompletionOption.ResponseHeadersRead, this.Cancellation);
			await using var stream = await response.Content.ReadAsStreamAsync(this.Cancellation);

			// the stream is established and flowing
			var buffer = new byte[256];
			int n = await stream.ReadAsync(buffer, this.Cancellation);
			Assert.That(n, Is.GreaterThan(0), "the stream must deliver data while the link is healthy");

			// cut the edge UNDER the established stream: the connection was initiated over CLI -> API, so it must abort
			context.Topology.Cut("CLI", "API", VirtualNetworkFault.Severed);

			Assert.CatchAsync(async () =>
			{
				// a chunk already buffered in the in-process pipe may still deliver (same as bytes already in the kernel
				// buffer when a real link is severed), but the abort must surface within a few reads - never an endless feed
				for (int i = 0; i < 100; i++)
				{
					int read = await stream.ReadAsync(buffer, this.Cancellation);
					if (read == 0)
					{ // a clean EOF is also an accepted "connection died" observation
						throw new EndOfStreamException();
					}
				}
			}, "severing the edge must abort the established stream");
		}

		[Test]
		public async Task Test_Blackhole_Connect_Parks_Silently_Then_Times_Out_On_Virtual_Time()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("CLI", host => host.ConfigureApplication(_ => { }));
				lan.WithMinimalWebHost("API", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/ping", (HttpContext _) => "pong"));
				});
			}));
			var cli = context.GetWebHost("CLI");
			var api = context.GetWebHost("API");

			var map = (VirtualNetworkMap) cli.GetRequiredService<INetworkMap>();
			using var client = new HttpClient(map.CreateTransportHandler(new BetterHttpClientOptions()), disposeHandler: false);
			var uri = api.GetUri("/ping");

			Assert.That(await client.GetStringAsync(uri, this.Cancellation), Is.EqualTo("pong"), "healthy link answers");

			// the blackhole budgets run on the topology's clock: point it to an advanceable fake, so 21 "seconds" of
			// silence cost zero real time (the park-then-throw pattern - the fault injector never cancels anything)
			var fake = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
			context.Topology.Time = fake;

			context.Topology.Cut("CLI", "API", VirtualNetworkFault.Blackhole(connectTimeout: TimeSpan.FromSeconds(21)));

			var pending = client.GetStringAsync(uri, this.Cancellation);

			// SILENCE first: a generous REAL settle must observe... nothing. No error, no completion - like a yanked cable.
			await Task.Delay(200, this.Cancellation);
			Assert.That(pending.IsCompleted, Is.False, "a blackholed connect must park silently (no error, no bytes) while virtual time is frozen");

			// ... then the exception "pops up" only when the victim's own budget elapses on VIRTUAL time
			fake.Advance(TimeSpan.FromSeconds(22));
			for (int i = 0; i < 100 && !pending.IsCompleted; i++)
			{
				await Task.Delay(10, this.Cancellation);
			}
			Assert.That(pending.IsCompleted, Is.True, "advancing virtual time past the connect budget must trip the timeout");

			var ex = Assert.ThrowsAsync<HttpRequestException>(async () => await pending, "the parked connect must fail once its budget elapses");
			Assert.That(ex!.InnerException, Is.InstanceOf<WebException>(), "a blackholed connect must fail with the connect-timeout shape");
			Assert.That(((WebException) ex.InnerException!).Status, Is.EqualTo(WebExceptionStatus.ConnectFailure));
			Assert.That(ex.InnerException!.InnerException, Is.InstanceOf<SocketException>());
			Assert.That(((SocketException) ex.InnerException.InnerException!).SocketErrorCode, Is.EqualTo(SocketError.TimedOut),
				"a blackholed connect must read as a socket TIMEOUT (not a reset, not a refusal)");
		}

		[Test]
		public async Task Test_Blackhole_Restore_Before_The_Budget_Lets_The_Connect_Proceed()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("CLI", host => host.ConfigureApplication(_ => { }));
				lan.WithMinimalWebHost("API", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/ping", (HttpContext _) => "pong"));
				});
			}));
			var cli = context.GetWebHost("CLI");
			var api = context.GetWebHost("API");

			var map = (VirtualNetworkMap) cli.GetRequiredService<INetworkMap>();
			using var client = new HttpClient(map.CreateTransportHandler(new BetterHttpClientOptions()), disposeHandler: false);
			var uri = api.GetUri("/ping");

			var fake = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
			context.Topology.Time = fake;

			context.Topology.Cut("CLI", "API", VirtualNetworkFault.Blackhole(connectTimeout: TimeSpan.FromSeconds(21)));

			var pending = client.GetStringAsync(uri, this.Cancellation);
			await Task.Delay(100, this.Cancellation);
			Assert.That(pending.IsCompleted, Is.False, "the connect must be parked while the edge is blackholed");

			// plug the cable back in BEFORE the budget elapses: the parked connect must proceed and succeed, like a SYN
			// retransmit that finally lands - with ZERO virtual time advanced (restore is event-driven, only the timeout
			// is time-driven)
			context.Topology.Restore("CLI", "API");

			for (int i = 0; i < 100 && !pending.IsCompleted; i++)
			{
				await Task.Delay(10, this.Cancellation);
			}
			Assert.That(pending.IsCompleted, Is.True, "restoring the edge must release the parked connect");
			Assert.That(await pending, Is.EqualTo("pong"), "the late connect must succeed against the restored edge");
		}

		[Test]
		public async Task Test_Blackhole_Silences_An_Established_Stream_Then_The_Read_Times_Out_On_Virtual_Time()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("CLI", host => host.ConfigureApplication(_ => { }));
				lan.WithMinimalWebHost("API", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/stream", async (HttpContext ctx) =>
					{
						ctx.Response.ContentType = "text/plain";
						while (!ctx.RequestAborted.IsCancellationRequested)
						{
							await ctx.Response.WriteAsync("tick\n", ctx.RequestAborted);
							await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
							await Task.Delay(25, ctx.RequestAborted);
						}
					}));
				});
			}));
			var cli = context.GetWebHost("CLI");
			var api = context.GetWebHost("API");

			var map = (VirtualNetworkMap) cli.GetRequiredService<INetworkMap>();
			using var client = new HttpClient(map.CreateTransportHandler(new BetterHttpClientOptions()), disposeHandler: false);

			using var response = await client.GetAsync(api.GetUri("/stream"), HttpCompletionOption.ResponseHeadersRead, this.Cancellation);
			await using var stream = await response.Content.ReadAsStreamAsync(this.Cancellation);

			var buffer = new byte[256];
			int n = await stream.ReadAsync(buffer, this.Cancellation);
			Assert.That(n, Is.GreaterThan(0), "the stream must deliver data while the link is healthy");

			var fake = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
			context.Topology.Time = fake;

			// The response bytes flow API -> CLI, so silencing THAT direction parks the client's reads - even though the
			// CLI -> API direction stays perfectly healthy. This is byte-flow directionality: one direction of the cable.
			context.Topology.Cut("API", "CLI", VirtualNetworkFault.Blackhole(noticeAfter: TimeSpan.FromSeconds(30)));

			var pendingRead = stream.ReadAsync(buffer, this.Cancellation).AsTask();

			// SILENCE first: no error, no bytes, just a read that does not complete - however long we really wait
			await Task.Delay(250, this.Cancellation);
			Assert.That(pendingRead.IsCompleted, Is.False, "a read over the blackholed direction must park silently (the connection LOOKS alive)");

			// ... then the read timeout pops up in the reader's own stack, once the notice window elapses on VIRTUAL time
			fake.Advance(TimeSpan.FromSeconds(31));
			for (int i = 0; i < 100 && !pendingRead.IsCompleted; i++)
			{
				await Task.Delay(10, this.Cancellation);
			}
			Assert.That(pendingRead.IsCompleted, Is.True, "advancing virtual time past the notice window must trip the read timeout");

			var ex = Assert.CatchAsync(async () => await pendingRead, "the parked read must fail once the notice window elapses");
			// the shape is a socket read timeout: an IOException carrying a SocketException(TimedOut)
			var chain = ex;
			SocketException? sockEx = null;
			while (chain is not null && (sockEx = chain as SocketException) is null) { chain = chain.InnerException; }
			Assert.That(sockEx, Is.Not.Null, $"the failure must carry a SocketException in its chain, but was: {ex}");
			Assert.That(sockEx!.SocketErrorCode, Is.EqualTo(SocketError.TimedOut), "a blackholed read must surface as a socket TIMEOUT");

			// and the healthy direction was never disturbed: a fresh CLI -> API request still answers
			// (the fresh request needs a virtual-time-free path: restore nothing, the forward edge was never cut)
			var mapCheck = (VirtualNetworkMap) cli.GetRequiredService<INetworkMap>();
			using var probe = new HttpClient(mapCheck.CreateTransportHandler(new BetterHttpClientOptions()), disposeHandler: false);
			// (the /stream endpoint streams forever; use a HEAD-less quick probe against the same host root returning 404 quickly)
			using var probeResponse = await probe.GetAsync(api.GetUri("/does-not-exist"), this.Cancellation);
			Assert.That((int) probeResponse.StatusCode, Is.EqualTo(404), "the CLI -> API direction must keep answering while API -> CLI is blackholed");
		}

		[Test]
		public async Task Test_Blackhole_Restore_Releases_A_Parked_Read()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("CLI", host => host.ConfigureApplication(_ => { }));
				lan.WithMinimalWebHost("API", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/stream", async (HttpContext ctx) =>
					{
						ctx.Response.ContentType = "text/plain";
						while (!ctx.RequestAborted.IsCancellationRequested)
						{
							await ctx.Response.WriteAsync("tick\n", ctx.RequestAborted);
							await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
							await Task.Delay(25, ctx.RequestAborted);
						}
					}));
				});
			}));
			var cli = context.GetWebHost("CLI");
			var api = context.GetWebHost("API");

			var map = (VirtualNetworkMap) cli.GetRequiredService<INetworkMap>();
			using var client = new HttpClient(map.CreateTransportHandler(new BetterHttpClientOptions()), disposeHandler: false);

			using var response = await client.GetAsync(api.GetUri("/stream"), HttpCompletionOption.ResponseHeadersRead, this.Cancellation);
			await using var stream = await response.Content.ReadAsStreamAsync(this.Cancellation);

			var buffer = new byte[256];
			Assert.That(await stream.ReadAsync(buffer, this.Cancellation), Is.GreaterThan(0), "the stream must deliver data while the link is healthy");

			var fake = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
			context.Topology.Time = fake;
			context.Topology.Cut("API", "CLI", VirtualNetworkFault.Blackhole(noticeAfter: TimeSpan.FromSeconds(30)));

			var pendingRead = stream.ReadAsync(buffer, this.Cancellation).AsTask();
			await Task.Delay(150, this.Cancellation);
			Assert.That(pendingRead.IsCompleted, Is.False, "the read must be parked while the direction is blackholed");

			// plug the cable back in: the parked read resumes and delivers the data that piled up - with ZERO virtual
			// time advanced (restore is event-driven, only the timeout is time-driven)
			context.Topology.Restore("API", "CLI");
			for (int i = 0; i < 100 && !pendingRead.IsCompleted; i++)
			{
				await Task.Delay(10, this.Cancellation);
			}
			Assert.That(pendingRead.IsCompleted, Is.True, "restoring the direction must release the parked read");
			Assert.That(await pendingRead, Is.GreaterThan(0), "the released read must deliver the buffered data");
		}

		[Test]
		public async Task Test_Blackhole_Silences_A_Streaming_Upload_Then_The_Write_Times_Out_On_Virtual_Time()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("CLI", host => host.ConfigureApplication(_ => { }));
				lan.WithMinimalWebHost("API", host =>
				{
					host.ConfigureApplication(app => app.MapPost("/drain", async (HttpContext ctx) =>
					{
						// drain the (streaming) request body until it ends
						var buf = new byte[4096];
						while (await ctx.Request.Body.ReadAsync(buf, ctx.RequestAborted) > 0) { }
						await ctx.Response.WriteAsync("drained", ctx.RequestAborted);
					}));
				});
			}));
			var cli = context.GetWebHost("CLI");
			var api = context.GetWebHost("API");

			var map = (VirtualNetworkMap) cli.GetRequiredService<INetworkMap>();
			using var client = new HttpClient(map.CreateTransportHandler(new BetterHttpClientOptions()), disposeHandler: false);

			var fake = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
			context.Topology.Time = fake;

			// a request body that streams "forever" (a chunk every 25ms), like an upload or a duplex pump: its writes
			// flow CLI -> API, so blackholing THAT direction parks the writer mid-body. The observable is the WRITER'S OWN
			// CALL STACK (where a real send() timeout surfaces) - the fate of the enclosing request task is in-process
			// plumbing detail (the TestServer does not fail the exchange on a client-side body fault).
			var firstChunkSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			var pumpOutcome = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
			var content = new PushContent(async (stream, ct) =>
			{
				var chunk = new byte[1024];
				try
				{
					while (true)
					{
						await stream.WriteAsync(chunk, ct);
						await stream.FlushAsync(ct);
						firstChunkSent.TrySetResult();
						await Task.Delay(25, ct);
					}
				}
				catch (Exception e)
				{
					pumpOutcome.TrySetResult(e);
					throw;
				}
			});

			var pending = client.PostAsync(api.GetUri("/drain"), content, this.Cancellation);
			_ = pending.ContinueWith(t => _ = t.Exception, TaskScheduler.Default); // observe whatever fate the plumbing gives the exchange

			// the upload must be established and flowing before the cut
			await firstChunkSent.Task.WaitAsync(TimeSpan.FromSeconds(10), this.Cancellation);

			context.Topology.Cut("CLI", "API", VirtualNetworkFault.Blackhole(noticeAfter: TimeSpan.FromSeconds(30)));

			// SILENCE first: the writer parks - no error pops up in its stack, however long we really wait
			await Task.Delay(250, this.Cancellation);
			Assert.That(pumpOutcome.Task.IsCompleted, Is.False, "a write over the blackholed direction must park silently (the upload just stalls)");

			// ... then the WRITE timeout pops up in the writer's own stack, once the notice window elapses on virtual time
			fake.Advance(TimeSpan.FromSeconds(31));
			for (int i = 0; i < 100 && !pumpOutcome.Task.IsCompleted; i++)
			{
				await Task.Delay(10, this.Cancellation);
			}
			Assert.That(pumpOutcome.Task.IsCompleted, Is.True, "advancing virtual time past the notice window must trip the write timeout");

			var ex = await pumpOutcome.Task;
			// the shape is a socket write timeout: an IOException carrying a SocketException(TimedOut)
			Assert.That(ex, Is.InstanceOf<IOException>(), $"the write must fail with an IO error, but was: {ex}");
			Assert.That(ex.Message, Does.Contain("write"), "the failure must read as a WRITE timeout");
			Exception? chain = ex;
			SocketException? sockEx = null;
			while (chain is not null && (sockEx = chain as SocketException) is null) { chain = chain.InnerException; }
			Assert.That(sockEx, Is.Not.Null, $"the failure must carry a SocketException in its chain, but was: {ex}");
			Assert.That(sockEx!.SocketErrorCode, Is.EqualTo(SocketError.TimedOut), "a blackholed write must surface as a socket TIMEOUT");
		}

		/// <summary>Minimal push-based streaming content (like a gRPC duplex request body): writes chunks into the transport for its whole life</summary>
		private sealed class PushContent : System.Net.Http.HttpContent
		{
			private readonly Func<Stream, CancellationToken, Task> Pump;

			public PushContent(Func<Stream, CancellationToken, Task> pump) => this.Pump = pump;

			protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) => this.Pump(stream, CancellationToken.None);

			protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context, CancellationToken cancellationToken) => this.Pump(stream, cancellationToken);

			protected override bool TryComputeLength(out long length) { length = 0; return false; }
		}

	}

}
