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
	using System.Net.Http;
	using Microsoft.AspNetCore.Builder;
	using Microsoft.AspNetCore.Http;
	using Microsoft.Extensions.DependencyInjection;
	using NUnit.Framework;
	using SnowBank.Networking.Http;

	/// <summary>Tests for the raw-client tripwire: a <c>DiagnosticListener</c> subscriber on <c>HttpRequestOut.Start</c> that
	/// detects (and names, by callstack) the requests that escape the virtual network - a <c>new HttpClient()</c> with no DI, or
	/// a third-party package that sets its own primary handler - because those open a real socket. Opt-in per test, warn-first
	/// then fail, with a callstack/target-host white-list escape hatch.</summary>
	[TestFixture]
	public class RawClientTripwireFacts : DistributedTest
	{

		// shared raw client, so its SocketsHttpHandler chain is built once (see WarmTheRawHandler). Static because the fixture is
		// InstancePerTestCase; disposeHandler false and disposed explicitly at fixture teardown.
		private static readonly HttpClient RawClient = new(new SocketsHttpHandler(), disposeHandler: false);

		[OneTimeSetUp]
		public static async Task WarmTheRawHandler()
		{
			// the first request on a fresh SocketsHttpHandler builds its handler chain on a threadpool continuation, which loses
			// the caller frame from the HttpRequestOut.Start callstack. Warm the shared handler once up front so later requests
			// fire the diagnostic synchronously in the caller's thread, keeping the frame that names the culprit.
			try
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
				await RawClient.GetStringAsync(new Uri("https://203.0.113.254/x"), cts.Token);
			}
			catch { }
		}

		[OneTimeTearDown]
		public static void DisposeRawHandler() => RawClient.Dispose();

		/// <summary>Opens a real socket to a reserved, unroutable TEST-NET address (RFC 5737), so nothing leaves the machine.</summary>
		private async Task OpenRealSocketToTestNet(string host)
		{
			try
			{
				using var cts = CancellationTokenSource.CreateLinkedTokenSource(this.Cancellation);
				cts.CancelAfter(TimeSpan.FromMilliseconds(400));
				await RawClient.GetStringAsync(new Uri($"https://{host}/x"), cts.Token);
			}
			catch { /* connect fails/cancels: we only need HttpRequestOut.Start, which fires before the connect */ }
		}

		[Test]
		public async Task Test_Tripwire_Catches_A_Raw_Real_Socket_Client_With_A_Callstack()
		{
			using var tripwire = new RawClientTripwire();
			await OpenRealSocketToTestNet("203.0.113.1");
			await tripwire.DrainAsync(this.Cancellation);

			var egress = tripwire.Egress.FirstOrDefault(e => e.Uri.Host == "203.0.113.1");
			Assert.That(egress, Is.Not.Null, "a raw real-socket client must be caught by the tripwire");
			Assert.That(egress!.Callstack, Does.Contain(nameof(OpenRealSocketToTestNet)),
				"the tripwire must attach the originating callstack, which names the method that opened the socket");
		}

		[Test]
		public async Task Test_Tripwire_Stays_Silent_For_A_Fully_Virtual_Test()
		{
			using var tripwire = new RawClientTripwire();
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
				lan.WithMinimalWebHost("WEB", host => host.ConfigureApplication(app => app.MapGet("/ping", (HttpContext _) => "pong")))));
			var web = context.GetWebHost("WEB");
			using (var client = web.GetRequiredService<IBetterHttpClientFactory>().CreateClient(web.GetUri("/")))
			{
				Assert.That(await client.GetStringAsync(web.GetUri("/ping"), this.Cancellation), Is.EqualTo("pong"));
			}
			await tripwire.DrainAsync(this.Cancellation);

			Assert.That(tripwire.Egress, Is.Empty, "a fully virtual test opens no real sockets, so the tripwire stays silent");
		}

		[Test]
		public async Task Test_Tripwire_Allowlists_Loopback()
		{
			// the test infrastructure's own IPC (MTP runner) is loopback: it must never trip the wire
			using var tripwire = new RawClientTripwire();
			await OpenRealSocketToTestNet("127.0.0.1");
			await tripwire.DrainAsync(this.Cancellation);

			Assert.That(tripwire.Egress.Any(e => e.Uri.IsLoopback), Is.False, "loopback egress (the runner's own IPC) must be allowlisted");
		}

		[Test]
		public async Task Test_Tripwire_Target_Host_Whitelist_Suppresses_A_Known_Endpoint()
		{
			using var tripwire = new RawClientTripwire().AllowHost("telemetry.*.com");
			await OpenRealSocketToTestNet("203.0.113.1");   // not whitelisted -> caught
			await tripwire.DrainAsync(this.Cancellation);
			Assert.That(tripwire.Egress.Any(e => e.Uri.Host == "203.0.113.1"), Is.True, "an un-whitelisted host is still caught");

			using var whitelisted = new RawClientTripwire().AllowHost("203.0.*");
			await OpenRealSocketToTestNet("203.0.113.1");
			await whitelisted.DrainAsync(this.Cancellation);
			Assert.That(whitelisted.Egress, Is.Empty, "a whitelisted target host must be suppressed (real egress the test accepts)");
		}

		[Test]
		public async Task Test_Tripwire_Callstack_Whitelist_Suppresses_A_Known_Culprit()
		{
			using var tripwire = new RawClientTripwire().AllowCallstack("*OpenRealSocketToTestNet*");
			await OpenRealSocketToTestNet("203.0.113.1");
			await tripwire.DrainAsync(this.Cancellation);

			Assert.That(tripwire.Egress, Is.Empty, "egress from a whitelisted callstack signature must be suppressed");
		}

		[Test]
		public async Task Test_Tripwire_Warn_First_Reports_But_Does_Not_Fail_Fail_Mode_Throws()
		{
			// warn-first: record + report, but Verify does not throw, so infra traffic can be catalogued before flipping to fail
			var reported = new List<string>();
			using (var warn = new RawClientTripwire(RawClientTripwireAction.Warn, reported.Add))
			{
				await OpenRealSocketToTestNet("203.0.113.1");
				await warn.DrainAsync(this.Cancellation);
				Assert.That(reported, Has.Some.Contains("203.0.113.1"), "warn mode reports the egress");
				Assert.That(() => warn.Verify(), Throws.Nothing, "warn mode never fails the run");
			}

			// fail mode: Verify throws, naming the URI and the callstack
			using (var fail = new RawClientTripwire(RawClientTripwireAction.Fail))
			{
				await OpenRealSocketToTestNet("203.0.113.1");
				await fail.DrainAsync(this.Cancellation);
				Assert.That(() => fail.Verify(),
					Throws.Exception.With.Message.Contains("203.0.113.1").And.Message.Contains(nameof(OpenRealSocketToTestNet)),
					"fail mode fails the run with the URI and the callstack that names the culprit");
			}
		}

	}

}
