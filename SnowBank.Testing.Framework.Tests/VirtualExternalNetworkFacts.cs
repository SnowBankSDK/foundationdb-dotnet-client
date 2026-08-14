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
	using System.Net.Http;
	using Microsoft.AspNetCore.Builder;
	using Microsoft.AspNetCore.Http;
	using Microsoft.Extensions.DependencyInjection;
	using NUnit.Framework;
	using SnowBank.Networking;
	using SnowBank.Networking.Http;

	/// <summary>Tests for the <see cref="VirtualNetworkType.External"/> network: a simulated endpoint reachable by its real
	/// base URI (e.g. <c>https://api.partner.com</c>) so the system under test needs no config edit, the routing that lets an
	/// external host and a <c>lan</c>/<c>cloud</c> host reach each other both ways, and the naming guard that keeps real TLDs
	/// off the simulated networks and <c>.simulated</c> names off the external network.</summary>
	[TestFixture]
	public class VirtualExternalNetworkFacts : DistributedTest
	{

		[Test]
		public async Task Test_External_Host_Registers_With_Real_Fqdn_And_Ext_Ip()
		{
			var context = await MakeItSo(env =>
			{
				env.AddSimpleLan(lan => lan.WithMinimalWebHost("APP", host => host.ConfigureApplication(app => app.MapGet("/ping", (HttpContext _) => "pong"))));
				env.AddSimpleExternal(ext => ext.WithMinimalWebHost("PARTNER", host =>
				{
					host.Identity.Fqdn = "api.partner.com"; // a real TLD, legal only on the external network
					host.ConfigureApplication(app => app.MapGet("/pay", (HttpContext _) => "charged"));
				}));
			});

			var partner = ((IVirtualNetworkTopology) context.Topology).GetHost("PARTNER");
			Assert.That(partner.Fqdn, Is.EqualTo("api.partner.com"), "an external host answers to its real FQDN");
			Assert.That(partner.Addresses.Any(a => a.ToString().StartsWith("69.88.84.", StringComparison.Ordinal)), Is.True,
				"external hosts draw from the distinctive 69.88.84 EXT block, so external traffic is spottable by address");
		}

		[Test]
		public async Task Test_Lan_Host_Reaches_External_Mock_By_Its_Real_Uri()
		{
			// the SUT (a lan host) calls a third-party endpoint by its unmodified real URI; the external mock answers.
			var context = await MakeItSo(env =>
			{
				env.AddSimpleLan(lan => lan.WithMinimalWebHost("APP", host => host.ConfigureApplication(app => app.MapGet("/ping", (HttpContext _) => "pong"))));
				env.AddSimpleExternal(ext => ext.WithMinimalWebHost("PARTNER", host =>
				{
					host.Identity.Fqdn = "api.partner.com";
					host.ConfigureApplication(app => app.MapGet("/pay", (HttpContext _) => "charged"));
				}));
			});
			var app = context.GetWebHost("APP");

			using var client = app.GetRequiredService<IBetterHttpClientFactory>().CreateClient(new Uri("https://api.partner.com"));
			Assert.That(await client.GetStringAsync(new Uri("https://api.partner.com/pay"), this.Cancellation), Is.EqualTo("charged"),
				"a lan host must reach the external mock by its real URI (no config edit on the SUT)");
		}

		[Test]
		public async Task Test_External_Host_Webhooks_Back_Into_A_Lan_Host()
		{
			// the reverse route: the external endpoint calls back into the lan SUT, using its own component DI so the call
			// rides the virtual map and stays sandboxed (a raw new HttpClient() in the callback would escape).
			var webhookBodies = new System.Collections.Concurrent.ConcurrentQueue<string>();
			var context = await MakeItSo(env =>
			{
				env.AddSimpleLan(lan => lan.WithMinimalWebHost("APP", host => host.ConfigureApplication(app => app.MapPost("/webhook", async (HttpContext ctx) =>
				{
					using var reader = new StreamReader(ctx.Request.Body);
					webhookBodies.Enqueue(await reader.ReadToEndAsync(ctx.RequestAborted));
					return "ack";
				}))));
				env.AddSimpleExternal(ext => ext.WithMinimalWebHost("PARTNER", host =>
				{
					host.Identity.Fqdn = "api.partner.com";
					host.ConfigureApplication(app => app.MapPost("/notify", async (HttpContext ctx) =>
					{
						var factory = ctx.RequestServices.GetRequiredService<IBetterHttpClientFactory>();
						using var client = factory.CreateClient(new Uri("https://app.lan.simulated"));
						await client.PostAsync(new Uri("https://app.lan.simulated/webhook"), new StringContent("event-42"), ctx.RequestAborted);
						return "sent";
					}));
				}));
			});
			var app = context.GetWebHost("APP");

			using var trigger = app.GetRequiredService<IBetterHttpClientFactory>().CreateClient(new Uri("https://api.partner.com"));
			var res = await trigger.PostAsync(new Uri("https://api.partner.com/notify"), new StringContent(""), this.Cancellation);
			Assert.That(res.IsSuccessStatusCode, Is.True, "the external endpoint must answer the trigger");

			Assert.That(webhookBodies, Does.Contain("event-42"),
				"the external host must webhook back into the lan host through its DI-drawn client (routing the third network type both ways)");
		}

		[Test]
		public void Test_Real_Tld_On_Lan_Fails_Registration()
		{
			var ex = Assert.CatchAsync(async () => await MakeItSo(env => env.AddSimpleLan(lan => lan.WithMinimalWebHost("BADAPP", host =>
			{
				host.Identity.Fqdn = "api.partner.com"; // a real TLD on a simulated network
				host.ConfigureApplication(_ => { });
			}))));
			Assert.That(ex!.ToString(), Does.Contain("api.partner.com").And.Contain("simulated"),
				"registering a real-TLD FQDN on a lan host must be rejected at registration");
		}

		[Test]
		public void Test_Simulated_Name_On_External_Fails_Registration()
		{
			var ex = Assert.CatchAsync(async () => await MakeItSo(env => env.AddSimpleExternal(ext => ext.WithMinimalWebHost("BADEXT", host =>
			{
				host.Identity.Fqdn = "mock.simulated"; // a .simulated name on the external network
				host.ConfigureApplication(_ => { });
			}))));
			Assert.That(ex!.ToString(), Does.Contain("mock.simulated").And.Contain("external"),
				"registering a .simulated FQDN on an external host must be rejected at registration");
		}

		[Test]
		public void Test_Real_Tld_Alias_On_Lan_Fails_Registration()
		{
			// the guard classifies every name a host answers to, not just its FQDN
			var ex = Assert.CatchAsync(async () => await MakeItSo(env => env.AddSimpleLan(lan => lan.WithMinimalWebHost("APP", host =>
			{
				host.Identity.Aliases.Add("api.partner.com"); // a real-TLD alias on a simulated host
				host.ConfigureApplication(_ => { });
			}))));
			Assert.That(ex!.ToString(), Does.Contain("api.partner.com"),
				"a real-TLD alias on a lan host must be rejected at registration");
		}

		[Test]
		public async Task Test_Unregistered_Real_Name_Trips_The_Loud_Alarm_On_Request()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
				lan.WithMinimalWebHost("APP", host => host.ConfigureApplication(app => app.MapGet("/ping", (HttpContext _) => "pong")))));
			var app = context.GetWebHost("APP");
			using var client = app.GetRequiredService<IBetterHttpClientFactory>().CreateClient(new Uri("https://api.leak.com"));

			// a real name nobody registered leaked into the test: a loud, specific alarm, not a quiet simulated DNS failure
			var ex = Assert.CatchAsync(async () => await client.GetStringAsync(new Uri("https://api.leak.com/x"), this.Cancellation));
			Assert.That(ex!.ToString(), Does.Contain("api.leak.com").And.Contain("Real URI"),
				"an unregistered real name reaching the virtual network must trip the loud egress alarm");
		}

		[Test]
		public async Task Test_Unregistered_Real_Name_Trips_The_Loud_Alarm_On_Resolve()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
				lan.WithMinimalWebHost("APP", host => host.ConfigureApplication(_ => { }))));
			var map = context.GetWebHost("APP").GetRequiredService<INetworkMap>();

			var ex = Assert.CatchAsync(async () => await map.DnsLookup("api.leak.com", null, this.Cancellation));
			Assert.That(ex!.ToString(), Does.Contain("api.leak.com").And.Contain("Real URI"),
				"an unregistered real name must trip the loud alarm at resolve-time too");
		}

		[Test]
		public async Task Test_Cut_Pattern_Gives_Quiet_Dns_Failure_Not_The_Alarm()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
				lan.WithMinimalWebHost("APP", host => host.ConfigureApplication(app => app.MapGet("/ping", (HttpContext _) => "pong")))));
			var app = context.GetWebHost("APP");

			// opt a real name out of the alarm: the sanctioned negative path, a quiet simulated DNS failure
			context.Topology.Cut("*.partner.com", VirtualNetworkFault.NameResolution);
			using var client = app.GetRequiredService<IBetterHttpClientFactory>().CreateClient(new Uri("https://api.partner.com"));

			var ex = Assert.ThrowsAsync<HttpRequestException>(async () =>
				await client.GetStringAsync(new Uri("https://api.partner.com/pay"), this.Cancellation));
			Assert.That(ex!.InnerException, Is.InstanceOf<WebException>(), "an intended Cut gives the normal quiet DNS-failure shape");
			Assert.That(((WebException) ex.InnerException!).Status, Is.EqualTo(WebExceptionStatus.NameResolutionFailure), "a Cut name resolves as a name-resolution failure");
			Assert.That(ex.ToString(), Does.Not.Contain("Real URI"), "a Cut name must NOT trip the loud alarm");
		}

		[Test]
		public async Task Test_Unregistered_Simulated_Name_Gives_Friendly_Forgot_To_Register()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
				lan.WithMinimalWebHost("APP", host => host.ConfigureApplication(app => app.MapGet("/ping", (HttpContext _) => "pong")))));
			var app = context.GetWebHost("APP");
			using var client = app.GetRequiredService<IBetterHttpClientFactory>().CreateClient(new Uri("https://ghost.lan.simulated"));

			var ex = Assert.CatchAsync(async () => await client.GetStringAsync(new Uri("https://ghost.lan.simulated/x"), this.Cancellation));
			Assert.That(ex!.ToString(), Does.Contain("ghost.lan.simulated").And.Contain("forgot to register"),
				"an unregistered .simulated name must give the friendly forgot-to-register error, not the loud real-URI alarm");
		}

		[Test]
		public async Task Test_External_Host_Without_Fqdn_Does_Not_Mint_Simulated()
		{
			// the default-FQDN mint is per-network: an external host with no explicit FQDN must not get a .simulated name, which
			// would then fail its own egress guard.
			var context = await MakeItSo(env => env.AddSimpleExternal(ext =>
				ext.WithMinimalWebHost("PARTNER", host => host.ConfigureApplication(app => app.MapGet("/x", (HttpContext _) => "ok")))));
			var partner = ((IVirtualNetworkTopology) context.Topology).GetHost("PARTNER");
			Assert.That(partner.Fqdn, Does.Not.EndWith(".simulated"),
				"an external host with no explicit FQDN must not mint a .simulated name");
		}

		[Test]
		public async Task Test_SetAlias_Vip_Must_Match_The_Target_Host_Network()
		{
			var context = await MakeItSo(env =>
			{
				env.AddSimpleLan(lan => lan.WithMinimalWebHost("WEB", host => host.ConfigureApplication(app => app.MapGet("/x", (HttpContext _) => "ok"))));
				env.AddSimpleExternal(ext => ext.WithMinimalWebHost("PARTNER", host =>
				{
					host.Identity.Fqdn = "api.partner.com";
					host.ConfigureApplication(_ => { });
				}));
			});
			var topo = context.Topology;

			// a VIP carries the network of the host it points at
			Assert.That(() => topo.SetAlias("vip.lan.simulated", "WEB"), Throws.Nothing, "a .simulated VIP fits a lan host");
			Assert.That(() => topo.SetAlias("api.evil.com", "WEB"), Throws.ArgumentException, "a real-TLD VIP must not attach to a lan host");
			Assert.That(() => topo.SetAlias("vip.partner.com", "PARTNER"), Throws.Nothing, "a real VIP fits an external host");
			Assert.That(() => topo.SetAlias("mock.simulated", "PARTNER"), Throws.ArgumentException, "a .simulated VIP must not attach to an external host");
		}

	}

}
