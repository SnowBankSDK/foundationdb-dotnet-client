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
	using Microsoft.AspNetCore.Builder;
	using Microsoft.AspNetCore.Http;
	using NUnit.Framework;
	using SnowBank.Networking;
	using SnowBank.Networking.Http;

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

			// ... and an unknown name fails per-request with the virtual network's DNS-failure shape. We deliberately use a name
			// OUTSIDE the ".simulated" convention: an unregistered ".simulated" name trips a DEBUG "you forgot to register" guard
			// in FindHost (a test-setup aid), whereas a plain unresolvable name exercises the real name-resolution fault path.
			var ex = Assert.ThrowsAsync<HttpRequestException>(async () =>
				await client.GetStringAsync(new Uri("https://does-not-exist.acme.invalid/ping"), this.Cancellation),
				"an unknown host must fail the SAME client's request");

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
			var factory = web.GetRequiredService<IBetterHttpClientFactory>();
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

	}

}
