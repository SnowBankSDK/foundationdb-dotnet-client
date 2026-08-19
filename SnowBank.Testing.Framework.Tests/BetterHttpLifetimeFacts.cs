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
	using System.Net;
	using System.Net.Http;
	using System.Net.Security;
	using System.Security.Cryptography.X509Certificates;
	using Microsoft.AspNetCore.Builder;
	using Microsoft.AspNetCore.Http;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Http;
	using Microsoft.Extensions.Options;
	using NUnit.Framework;
	using SnowBank.Networking;
	using SnowBank.Networking.Http;
	using SnowBank.Networking.PacketCapture;

	/// <summary>Late-binding self-tests for the unified transport seam (<see cref="INetworkMap.CreateTransportHandler"/>):
	/// ONE long-lived client, built once over the virtual transport, must resolve its target PER-REQUEST against the live
	/// network - so a mid-test host stop/start reroutes the SAME client, and an unresolvable name fails per-request with the
	/// virtual network's historical DNS-failure shape. This is what makes near-singleton clients over pooled handlers correct
	/// (creation-time target binding would freeze the first outcome for the client's whole lifetime).</summary>
	[TestFixture]
	public class BetterHttpLifetimeFacts : DistributedTest
	{

		[Test]
		public async Task Test_Restart_Reroutes_A_Live_Client()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/ping", (HttpContext _) => "pong"));
				});
			}));
			var web = context.GetWebHost("WEB");

			// ONE transport handler + ONE client, held across the whole stop/start cycle. disposeHandler:false models the
			// long-lived transport whose lifetime the client does NOT own (the point of the seam: clients become cheap shells).
			var map = web.GetRequiredService<INetworkMap>();
			using var client = new HttpClient(map.CreateTransportHandler(new BetterHttpClientOptions()), disposeHandler: false);

			var uri = web.GetUri("/ping");
			Assert.That(await client.GetStringAsync(uri, this.Cancellation), Is.EqualTo("pong"), "healthy host answers");

			await web.StopHost(this.Cancellation);
			// the SAME client's next request must now fail: the target host is resolved per-request against the LIVE map, which
			// sees the node as down (a stopped node drops off the network -> simulated connect failure), not against a target
			// captured when the handler was built.
			Assert.ThrowsAsync<HttpRequestException>(async () => await client.GetStringAsync(uri, this.Cancellation),
				"a stopped host must fail the SAME client's next request");

			await web.StartHost(this.Cancellation);
			Assert.That(await client.GetStringAsync(uri, this.Cancellation), Is.EqualTo("pong"), "restart must reroute the SAME client");
		}

		[Test]
		public async Task Test_Unknown_Host_Fails_Per_Request_With_Dns_Shape()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/ping", (HttpContext _) => "pong"));
				});
			}));
			var web = context.GetWebHost("WEB");
			var map = web.GetRequiredService<INetworkMap>();
			using var client = new HttpClient(map.CreateTransportHandler(new BetterHttpClientOptions()), disposeHandler: false);

			// same client: a resolvable host works ...
			Assert.That(await client.GetStringAsync(web.GetUri("/ping"), this.Cancellation), Is.EqualTo("pong"), "resolvable host answers");

			// ... and an intended DNS failure fails per-request with the virtual network's DNS-failure shape. The hard egress
			// defense turns an unregistered real name into a LOUD alarm, so the sanctioned way to get a quiet simulated DNS
			// failure is an explicit Cut(pattern, NameResolution) at the resolution layer.
			context.Topology.Cut("does-not-exist.acme.invalid", VirtualNetworkFault.NameResolution);
			var ex = Assert.ThrowsAsync<HttpRequestException>(async () =>
				await client.GetStringAsync(new Uri("https://does-not-exist.acme.invalid/ping"), this.Cancellation),
				"an intended DNS cut must fail the SAME client's request");

			// mirror the exact shape produced by VirtualHttpClientHandler.SimulateNameResolutionError: an HttpRequestException
			// wrapping a WebException whose status is NameResolutionFailure.
			Assert.That(ex!.InnerException, Is.InstanceOf<WebException>(), "a DNS failure must wrap a WebException");
			Assert.That(((WebException) ex.InnerException!).Status, Is.EqualTo(WebExceptionStatus.NameResolutionFailure),
				"an unresolvable name must surface as a name-resolution failure");
		}

		[Test]
		public async Task Test_Disposing_Clients_Does_Not_Kill_The_Pooled_Chain()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/ping", (HttpContext _) => "pong"));
				});
			}));
			var web = context.GetWebHost("WEB");
			// the test deliberately exercises the obsolete shell-factory surface.
#pragma warning disable CS0618
			var factory = web.GetRequiredService<IBetterHttpClientFactory>();
#pragma warning restore CS0618
			var uri = web.GetUri("/ping");

			// create, use, dispose N clients: every request must succeed - disposing a shell must never tear down the shared chain
			for (int i = 0; i < 5; i++)
			{
				using var client = factory.CreateClient(uri);
				var res = await client.SendAsync(client.CreateGetRequest(uri), async (ctx) =>
					await ctx.Response.Content.ReadAsStringAsync(this.Cancellation), this.Cancellation);
				Assert.That(res, Is.EqualTo("pong"), $"iteration {i}");
			}
		}

		[Test]
		public async Task Test_Pipeline_Handler_Is_Rebuilt_On_Rotation()
		{
			// A pooled handler chain is owned by the platform (Microsoft.Extensions.Http) and ROTATED once its HandlerLifetime
			// elapses: the NEXT request past the lifetime rebuilds the whole chain - including our pipeline handlers. That is the
			// point of moving sockets to the platform (the client no longer owns the connection pool's lifetime), so we guard it:
			// a counting DelegatingHandler on a dedicated bundle must be re-constructed across a rotation. We assert on a static
			// construction counter (>= 2), never on wall-clock precision.
			RotationProbeHandler.ResetConstructionCount();

			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/ping", (HttpContext _) => "pong"));
					host.ConfigureServices(builder =>
					{
						// a dedicated policy bundle whose pipeline includes the counting handler; 1s is M.E.Http's documented
						// minimum handler lifetime, so the chain rotates on the first request that arrives after it elapses.
						builder.Services.AddBetterHttpClient("rotation", options => options.WithDelegatingHandler<RotationProbeHandler>());
						builder.Services.AddHttpClient("rotation").SetHandlerLifetime(TimeSpan.FromSeconds(1));
					});
				});
			}));
			var web = context.GetWebHost("WEB");
			// the test deliberately exercises the obsolete shell-factory surface.
#pragma warning disable CS0618
			var factory = web.GetRequiredService<IBetterHttpClientFactory>();
#pragma warning restore CS0618
			var uri = web.GetUri("/ping");

			// build #1 of the "rotation" chain (constructs the probe handler once) and use it
			using (var client = factory.CreateClient(uri, "rotation"))
			{
				var res = await client.SendAsync(client.CreateGetRequest(uri), (ctx) => ctx.Response.Content.ReadAsStringAsync(this.Cancellation), this.Cancellation);
				Assert.That(res, Is.EqualTo("pong"), "the counting handler must pass the request through to the target");
			}
			Assert.That(RotationProbeHandler.ConstructionCount, Is.EqualTo(1), "the pipeline handler is built once for the first chain");

			// wait past the 1s handler lifetime so the platform expires the active chain ...
			await Task.Delay(TimeSpan.FromMilliseconds(1500), this.Cancellation);

			// ... the next client asks for a handler, so the platform rebuilds the chain and re-constructs the pipeline handler
			using (var client = factory.CreateClient(uri, "rotation"))
			{
				var res = await client.SendAsync(client.CreateGetRequest(uri), (ctx) => ctx.Response.Content.ReadAsStringAsync(this.Cancellation), this.Cancellation);
				Assert.That(res, Is.EqualTo("pong"), "the rebuilt chain must still reach the target");
			}
			Assert.That(RotationProbeHandler.ConstructionCount, Is.GreaterThanOrEqualTo(2), "rotating the pooled chain must rebuild the pipeline handler");
		}

		/// <summary>Pass-through <see cref="DelegatingHandler"/> that counts how many times it has been constructed, so a test can
		/// observe the platform rebuilding a bundle's pipeline when the pooled handler chain rotates.</summary>
		public sealed class RotationProbeHandler : DelegatingHandler
		{
			private static int ConstructionCounter;

			public RotationProbeHandler()
			{
				System.Threading.Interlocked.Increment(ref ConstructionCounter);
			}

			/// <summary>Number of times this handler has been constructed since the last <see cref="ResetConstructionCount"/>.</summary>
			public static int ConstructionCount => System.Threading.Volatile.Read(ref ConstructionCounter);

			/// <summary>Resets the construction counter (call at the start of a test).</summary>
			public static void ResetConstructionCount() => System.Threading.Interlocked.Exchange(ref ConstructionCounter, 0);
		}

		[Test]
		public void Test_Wired_Bundles_Default_To_Infinite_Handler_Lifetime()
		{
			// DNS staleness is already bounded by the transport itself (PooledConnectionLifetime on the SocketsHttpHandler,
			// set by NetworkMap.CreateTransportHandler): the platform's default 2-minute chain rotation stacked on top of it
			// paid pool cold-starts against every active origin and bought nothing. Wired bundles therefore default to an
			// INFINITE handler lifetime; a bundle that wants periodic chain rebuild opts back in with the stock M.E.Http API
			// (services.AddHttpClient(name).SetHandlerLifetime(...) AFTER the bundle registration - which is exactly what
			// Test_Pipeline_Handler_Is_Rebuilt_On_Rotation does, and why it stays green).
			var services = new ServiceCollection();
			services.AddBetterHttpClientDefaults();
			services.AddBetterHttpClient("custom");
			using var provider = services.BuildServiceProvider();

			var factoryOptions = provider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>();
			Assert.That(factoryOptions.Get(BetterHttpClientExtensions.DefaultClientName).HandlerLifetime, Is.EqualTo(Timeout.InfiniteTimeSpan),
				"the default bundle must not rotate its chain (the transport owns DNS freshness)");
			Assert.That(factoryOptions.Get("custom").HandlerLifetime, Is.EqualTo(Timeout.InfiniteTimeSpan),
				"a named bundle must not rotate its chain (the transport owns DNS freshness)");
		}

		[Test]
		public void Test_Plain_HttpClientFactory_Is_A_Full_Door_To_A_Policy_Client()
		{
			// The plain IHttpClientFactory used to be a guarded "wrong door" for a policy client: a plain HttpClient over the
			// pooled chain had no BetterHttp runtime, so the client's filters, hooks and credentials silently never ran, and
			// the registration threw to prevent that silent skip. The request stages now live in the chain itself (the
			// BetterHttpPipelineHandler creates the per-request context when none is attached), so a plain factory client
			// carries the full policy and the guard is gone: every door hands out the same behavior.
			var services = new ServiceCollection();
			services.AddSingleton<INetworkMap, NetworkMap>();
			services.AddBetterHttpClientDefaults();
			services.AddBetterHttpClient("signed");
			using var provider = services.BuildServiceProvider();

			var door = provider.GetRequiredService<IHttpClientFactory>();
			Assert.That(() => door.CreateClient("signed").Dispose(), Throws.Nothing,
				"a policy client must be resolvable through the plain factory (the request stages ride the chain now)");
			Assert.That(() => door.CreateClient(BetterHttpClientExtensions.DefaultClientName).Dispose(), Throws.Nothing,
				"the default client is a regular named client, resolvable through every door");
			Assert.That(() => door.CreateClient("not-a-bundle").Dispose(), Throws.Nothing,
				"the plain factory keeps working normally for names that carry no per-name policy");

			// and the chain behind that door carries the request-stage handler
			var handlerFactory = provider.GetRequiredService<System.Net.Http.IHttpMessageHandlerFactory>();
			using var handler = handlerFactory.CreateHandler("signed");
			HttpMessageHandler? h = handler;
			bool found = false;
			while (h is not null)
			{
				if (h.GetType().Name == "BetterHttpPipelineHandler") { found = true; break; }
				h = (h as DelegatingHandler)?.InnerHandler;
			}
			Assert.That(found, Is.True, "the policy client's chain must carry the request-stage pipeline handler");
		}

		[Test]
		public void Test_Certificate_Callback_Maps_Onto_SslOptions_Without_Reflection()
		{
			// The server-certificate callback is a per-bundle transport policy applied to the shared SocketsHttpHandler. Its
			// shape deliberately has NO HttpRequestMessage argument: the callback validates a CONNECTION (SslStream knows no
			// request), which is what lets it map DIRECTLY onto SslOptions.RemoteCertificateValidationCallback - a
			// request-shaped signature cannot be honored there without reflecting over runtime internals.
			using var handler = new BetterHttpClientHandler(new NetworkMap());

			Func<X509Certificate2?, X509Chain?, SslPolicyErrors, bool> callback = static (_, _, _) => true;
			handler.ServerCertificateCustomValidationCallback = callback;
			Assert.That(handler.ServerCertificateCustomValidationCallback, Is.SameAs(callback), "the handler must report the assigned callback");
			Assert.That(handler.Sockets.SslOptions.RemoteCertificateValidationCallback, Is.Not.Null, "the callback must be mapped onto the socket transport's SslOptions");
			Assert.That(handler.Sockets.SslOptions.RemoteCertificateValidationCallback!.Invoke(this, null, null, SslPolicyErrors.RemoteCertificateChainErrors), Is.True,
				"the mapped callback must invoke the assigned validation logic");

			handler.ServerCertificateCustomValidationCallback = null;
			Assert.That(handler.Sockets.SslOptions.RemoteCertificateValidationCallback, Is.Null, "clearing the callback must clear the socket transport's SslOptions");
		}

		[Test]
		public async Task Test_Remap_Reroutes_A_Live_Client_To_A_Different_Node()
		{
			// two distinct backends behind one public VIP; the origin (CLIENT) only lends its map for resolution.
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("CLIENT", host => host.ConfigureApplication(_ => { }));
				lan.WithMinimalWebHost("NODE_A", host => host.ConfigureApplication(app => app.MapGet("/whoami", (HttpContext _) => "NODE_A")));
				lan.WithMinimalWebHost("NODE_B", host => host.ConfigureApplication(app => app.MapGet("/whoami", (HttpContext _) => "NODE_B")));
			}));

			// The client resolves through CLIENT's map; the topology (its hosts AND the mutable VIP alias) is shared by every
			// host on the network. SetAlias lives on the concrete VirtualNetworkTopology - the mutable-VIP / reverse-proxy seam.
			var map = (VirtualNetworkMap) context.GetWebHost("CLIENT").GetRequiredService<INetworkMap>();
			map.Topology.SetAlias("cluster.lan.simulated", "NODE_A");

			using var client = new HttpClient(map.CreateTransportHandler(new BetterHttpClientOptions()), disposeHandler: false);
			var vip = new Uri("https://cluster.lan.simulated/whoami");
			Assert.That(await client.GetStringAsync(vip, this.Cancellation), Is.EqualTo("NODE_A"), "the VIP initially resolves to NODE_A");

			// re-point the VIP to NODE_B: because the transport resolves the target PER-REQUEST against the live topology, the
			// SAME client's next request lands on NODE_B - no client re-creation (a reconnecting peer meeting a different backend
			// behind the same public name).
			map.Topology.SetAlias("cluster.lan.simulated", "NODE_B");
			Assert.That(await client.GetStringAsync(vip, this.Cancellation), Is.EqualTo("NODE_B"), "re-pointing the VIP reroutes the SAME client");
		}

		[Test]
		public async Task Test_Per_Call_Protocol_Configure_Rejects_Wire_Policy()
		{
			// The contract: per-call configure = protocol/client behavior only; wire policy (TLS, proxy, cookies, filters,
			// pipeline handlers) = the policy bundle, registered at startup. A per-call TLS callback or filter CANNOT reach
			// the shared pooled transport, and silently ignoring it would be a silent security/behavior break: it must
			// fail loudly, naming the offending member.
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/ping", (HttpContext _) => "pong"));
				});
			}));
			var web = context.GetWebHost("WEB");
			var factory = web.GetRequiredService<RestHttpProtocolFactory>();
			var uri = web.GetUri("/ping");

			Assert.That(() => factory.CreateClient(uri, o => o.AcceptSelfSignedServerCertificates()),
				Throws.InvalidOperationException.With.Message.Contains(nameof(BetterHttpClientOptions.ServerCertificateCustomValidationCallback)),
				"a per-call TLS relaxation must throw (it would silently not reach the wire)");
			// the test deliberately exercises the obsolete Filters surface (still the transport-tier wire policy at this call).
#pragma warning disable CS0618
			Assert.That(() => factory.CreateClient(uri, o => o.Filters.Add(new NoopFilter())),
				Throws.InvalidOperationException.With.Message.Contains(nameof(BetterHttpClientOptions.Filters)),
				"a per-call filter must throw (the pooled pipeline is built from the bundle options)");
#pragma warning restore CS0618
			Assert.That(() => factory.CreateClient(uri, o => o.Proxy = new WebProxy("http://proxy.lan.simulated:8080")),
				Throws.InvalidOperationException.With.Message.Contains(nameof(BetterHttpClientOptions.Proxy)),
				"a per-call proxy must throw (socket knobs are per-bundle transport policy)");
			Assert.That(() => factory.CreateClient(uri, o => o.Credentials = new WrappedCredentials(new NetworkCredential("user", "password"))),
				Throws.InvalidOperationException.With.Message.Contains(nameof(BetterHttpClientOptions.Credentials)),
				"per-call TRANSPORT-COUPLED credentials must throw (their handler-configure half cannot run on the pooled chain)");
		}

		[Test]
		public async Task Test_Per_Call_Protocol_Configure_Blesses_Client_Level_Behavior()
		{
			// The blessed half of the same contract: per-connection default headers (the real-world load-bearing per-call
			// usage - e.g. auth headers on a transient protocol client) apply to the SHELL and must reach the server.
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/echo-auth", (HttpContext ctx) => ctx.Request.Headers["X-Acme-Auth"].ToString()));
				});
			}));
			var web = context.GetWebHost("WEB");
			var factory = web.GetRequiredService<RestHttpProtocolFactory>();
			var uri = web.GetUri("/echo-auth");

			using var protocol = factory.CreateClient(uri, o => o.DefaultRequestHeaders.Add("X-Acme-Auth", "token-42"));
			Assert.That(await protocol.GetTextAsync(uri, this.Cancellation), Is.EqualTo("token-42"),
				"per-call default headers are client-level behavior and must reach the server");
		}

		[Test]
		public async Task Test_Factory_Shell_Options_Apply_To_The_Shell_Only()
		{
			// The factory's per-call surface is the SHELL tier: default headers (and hooks/request options) for THIS client,
			// over the shared pooled chain. A sibling client of the same bundle must not see them.
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/echo-tag", (HttpContext ctx) => ctx.Request.Headers["X-Tag"].ToString()));
				});
			}));
			var web = context.GetWebHost("WEB");
			// the test deliberately exercises the obsolete shell-factory surface.
#pragma warning disable CS0618
			var factory = web.GetRequiredService<IBetterHttpClientFactory>();
			var uri = web.GetUri("/echo-tag");

			var shell = new BetterHttpShellOptions();
			shell.DefaultRequestHeaders.Add("X-Tag", "42");
			using (var tagged = factory.CreateClient(uri, shell))
			{
				var res = await tagged.SendAsync(tagged.CreateGetRequest(uri), (ctx) => ctx.Response.Content.ReadAsStringAsync(this.Cancellation), this.Cancellation);
				Assert.That(res, Is.EqualTo("42"), "the shell options' default headers must ride this client's requests");
			}
			using (var plain = factory.CreateClient(uri))
			{
				var res = await plain.SendAsync(plain.CreateGetRequest(uri), (ctx) => ctx.Response.Content.ReadAsStringAsync(this.Cancellation), this.Cancellation);
				Assert.That(res, Is.Empty, "a sibling client of the same bundle must not inherit another shell's headers");
			}
#pragma warning restore CS0618
		}

		/// <summary>No-op filter used to prove that per-call filter registration is rejected</summary>
		// the test deliberately exercises the obsolete filter surface.
#pragma warning disable CS0618
		private sealed class NoopFilter : IBetterHttpFilter
		{
			public string Name => "noop";
		}
#pragma warning restore CS0618

		[Test]
		public async Task Test_Per_Request_Only_Credentials_Are_Shell_Tier()
		{
			// Credentials have two halves: a handler-configure half (transport tier, bundle-only) and a per-request half
			// (OnBeforeRequest - e.g. a message signer stamping auth onto each request). A credential that declares itself
			// per-request only (IsPerRequestOnly) is CLIENT behavior - the "different identity per transient client" pattern -
			// and must ride the shell; a transport-coupled one (e.g. NTLM via WrappedCredentials) must be rejected loudly.
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/echo-stamp", (HttpContext ctx) => ctx.Request.Headers["X-Stamped"].ToString()));
				});
			}));
			var web = context.GetWebHost("WEB");
			// the test deliberately exercises the obsolete shell-factory surface.
#pragma warning disable CS0618
			var factory = web.GetRequiredService<IBetterHttpClientFactory>();
			var uri = web.GetUri("/echo-stamp");

			// the per-request half of a shell credential must run for this client's requests
			using (var client = factory.CreateClient(uri, new BetterHttpShellOptions() { Credentials = new HeaderStampCredentials() }))
			{
				var res = await client.SendAsync(client.CreateGetRequest(uri), (ctx) => ctx.Response.Content.ReadAsStringAsync(this.Cancellation), this.Cancellation);
				Assert.That(res, Is.EqualTo("stamped"), "a per-request-only shell credential must be invoked for this client's requests");
			}

			// a transport-coupled credential cannot work per-shell: its configure half will never see the pooled chain
			Assert.That(() => factory.CreateClient(uri, new BetterHttpShellOptions() { Credentials = new WrappedCredentials(new NetworkCredential("user", "password")) }),
				Throws.InvalidOperationException.With.Message.Contains(nameof(WrappedCredentials)),
				"transport-coupled credentials on the shell must fail loudly, not silently skip their configure half");
#pragma warning restore CS0618
		}

		/// <summary>Per-request-only credential that stamps a header on each request (a stand-in for a message signer)</summary>
		private sealed class HeaderStampCredentials : IBetterCredentials
		{
			public bool IsPerRequestOnly => true;

			HttpMessageHandler IBetterCredentials.Configure(HttpMessageHandler handler, BetterHttpClientOptions options, IServiceProvider services) => handler;

			ValueTask IBetterCredentials.OnBeforeRequest(BetterHttpClientContext context)
			{
				context.Request!.Headers.Add("X-Stamped", "stamped");
				return default;
			}
		}

		[Test]
		public async Task Test_Bare_Handler_From_Factory_Is_Captured()
		{
			// A bare handler straight from the pooled factory carries the FULL pipeline, packet
			// capture included. A plain HttpClient over IHttpMessageHandlerFactory.CreateHandler(default bundle) - the shape a
			// gRPC/SignalR consumer uses - never goes through the BetterHttpClient send extension, yet its traffic must
			// still land in the journal. This is the pinned regression for the historical half-applied-hooks dark spot, where
			// capture only fired for requests that went through the send extension.
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/ping", (HttpContext _) => "pong"));
				});
			}));
			var web = context.GetWebHost("WEB");

			// a bare handler from the factory, wrapped in a PLAIN HttpClient (no BetterHttpClient, no send extension).
			var handlerFactory = web.GetRequiredService<IHttpMessageHandlerFactory>();
			using var client = new HttpClient(handlerFactory.CreateHandler(BetterHttpClientExtensions.DefaultClientName), disposeHandler: false);

			var uri = web.GetUri("/ping");
			Assert.That(await client.GetStringAsync(uri, this.Cancellation), Is.EqualTo("pong"), "the bare handler must still reach the target");

			// the manager emits through an async channel: poll briefly until the packet lands (like WebBrowserBaseFacts).
			List<CapturedPacket> packets = [ ];
			for (int i = 0; i < 40 && packets.Count == 0; i++)
			{
				packets = context.GetNetworkPackets(p => p.Metadata.Uri != null && p.Metadata.Uri.Contains("/ping", StringComparison.Ordinal));
				if (packets.Count == 0) await Task.Delay(25, this.Cancellation);
			}

			Assert.That(packets, Is.Not.Empty, "a bare handler obtained from IHttpMessageHandlerFactory must ride the full pipeline and be captured");
			Assert.That(packets[0].Metadata.Uri, Does.Contain(uri.Host), "the captured packet must record the request host");
			Assert.That(packets[0].Metadata.Uri, Does.Contain("/ping"), "the captured packet must record the request path");
		}

		[Test]
		public async Task Test_Streaming_Response_Is_Captured_At_Headers_Without_Tearing_The_Stream()
		{
			// A long-lived streaming response (here a Server-Sent Events feed; a gRPC duplex body behaves the same way) must
			// ride the CAPTURED pooled chain without being torn. Its body stays open for the connection's whole life, so the
			// capture layer must record the packet at headers (metadata only) and pass the live body through completely
			// untouched. Mirroring such a body would never emit a packet (the body never ends), grow without bound, and couple
			// the capture wrapper's disposal to the transport's live stream - which is exactly what tears real duplex streams.
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/events", async (HttpContext ctx) =>
					{
						// an SSE feed: emit an event, flush, repeat, until the client goes away
						ctx.Response.Headers.ContentType = "text/event-stream";
						var stop = ctx.RequestAborted;
						try
						{
							for (int n = 0; !stop.IsCancellationRequested; n++)
							{
								await ctx.Response.WriteAsync($"data: event-{n}\n\n", stop);
								await ctx.Response.Body.FlushAsync(stop);
								await Task.Delay(20, stop);
							}
						}
						catch (OperationCanceledException) { /* client disconnected: the normal end of an SSE feed */ }
					}));
				});
			}));
			var web = context.GetWebHost("WEB");

			// consume the feed through a CAPTURED bundle handler (the shape a gRPC/SignalR sink uses), headers-first so the body
			// streams live instead of being buffered.
			var handlerFactory = web.GetRequiredService<IHttpMessageHandlerFactory>();
			using var client = new HttpClient(handlerFactory.CreateHandler(BetterHttpClientExtensions.DefaultClientName), disposeHandler: false);

			var uri = web.GetUri("/events");
			using var consumer = CancellationTokenSource.CreateLinkedTokenSource(this.Cancellation);
			consumer.CancelAfter(TimeSpan.FromSeconds(30)); // hard cap so a buffering transport fails fast instead of hanging

			var events = new List<string>();
			using (var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, consumer.Token))
			{
				Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), "the streaming endpoint must answer");
				Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/event-stream"), "the response must be an SSE feed");

				// (a) several events must be read LIVE - a torn/disposed body would throw here instead of yielding events.
				using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(consumer.Token));
				while (events.Count < 3)
				{
					var line = await reader.ReadLineAsync(consumer.Token);
					if (line is null) break; // the stream ended (would mean it was torn); the assert below then fails
					if (line.StartsWith("data: ", StringComparison.Ordinal)) events.Add(line);
				}
				Assert.That(events, Has.Count.EqualTo(3), "several live events must be read without the stream being torn");
				Assert.That(events[0], Is.EqualTo("data: event-0"), "the first live event must arrive intact");

				// (b) the packet must ALREADY be captured while the feed is still open - proving it was emitted at headers, not
				// on the (never-arriving) close of the stream. The manager emits via an async channel, so poll briefly.
				List<CapturedPacket> packets = [ ];
				for (int i = 0; i < 40 && packets.Count == 0; i++)
				{
					packets = context.GetNetworkPackets(p => p.Metadata.Uri != null && p.Metadata.Uri.Contains("/events", StringComparison.Ordinal));
					if (packets.Count == 0) await Task.Delay(25, this.Cancellation);
				}
				Assert.That(packets, Is.Not.Empty, "a streaming response must be captured at headers, while it is still open");
				Assert.That(packets[0].Metadata.Response.Status, Is.EqualTo(200), "the response status must be captured at headers");
				Assert.That(packets[0].Metadata.Response.Streaming, Is.True, "the streaming body must be flagged as streaming, not mirrored");
				Assert.That(packets[0].Metadata.Response.HasBody, Is.False, "the streaming body must NOT be captured");
			}

			// (c)/(d) reading through the untouched body never raised ObjectDisposedException, and disposing the response above
			// closed the feed cleanly (the server's RequestAborted fired). Cancel any residual just to be tidy.
			consumer.Cancel();
		}

	}

}
