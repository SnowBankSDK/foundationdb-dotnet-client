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
	using Microsoft.AspNetCore.Builder;
	using Microsoft.AspNetCore.Http;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Http;
	using NUnit.Framework;
	using SnowBank.Networking.Http;
	using SnowBank.Threading;

	/// <summary>Tests the rework's core invariant: a client name maps to one policy, and every consumer observes it
	/// identically, because the request stages (filters, credentials, hooks) now run inside the pooled handler chain via
	/// the <see cref="BetterHttpPipelineHandler"/>, built once per client name.</summary>
	[TestFixture]
	public class BetterHttpClientEquivalenceFacts : DistributedTest
	{

		/// <summary>Reads a single response header value, or <see langword="null"/> when the header was not sent</summary>
		private static string? GetHeader(HttpResponseMessage response, string name)
			=> response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

		/// <summary>Asserts that a client observed the credentials marker, and (for the clients that go through an <see cref="HttpClient"/>
		/// carrying the name's default headers) the client-level default header.</summary>
		private static void AssertClientObservedPolicy(HttpResponseMessage response, string client, bool expectPolicyHeader)
		{
			Assert.That(GetHeader(response, "x-echo-marker"), Is.EqualTo("signed"),
				$"{client}: the per-request credentials marker must reach the server (the pipeline handler runs the credentials stage for every client)");
			if (expectPolicyHeader)
			{
				Assert.That(GetHeader(response, "x-echo-policy-header"), Is.EqualTo("from-options"),
					$"{client}: the name's default request header must reach the server");
			}
		}

		[Test]
		public async Task Test_Every_Client_Kind_Observes_The_Same_Named_Policy()
		{
			// One minimal host exposes /probe, which echoes the two headers the test cares about back as response headers,
			// so each client's outcome can be read from the HttpResponseMessage it gets back.
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/probe", (HttpContext ctx) =>
					{
						ctx.Response.Headers["x-echo-policy-header"] = ctx.Request.Headers.TryGetValue("x-policy-header", out var policy) ? policy.ToString() : "";
						ctx.Response.Headers["x-echo-marker"] = ctx.Request.Headers.TryGetValue("x-marker", out var marker) ? marker.ToString() : "";
						return Results.Ok();
					}));
					host.ConfigureServices(builder =>
					{
						builder.Services
							.AddBetterHttpClient("policy", o =>
							{
								o.DefaultRequestHeaders["x-policy-header"] = "from-options";
								o.Credentials = new MarkerCredentials();
							})
							.AddAsKeyed();
						builder.Services.AddHttpClient<ProbeTypedClient>("policy");
					});
				});
			}));
			var web = context.GetWebHost("WEB");
			var uri = web.GetUri("/probe");

			// Client 1: IHttpClientFactory.CreateClient("policy")
			var factory = web.GetRequiredService<IHttpClientFactory>();
			using var client1 = factory.CreateClient("policy");
			using (var response = await client1.GetAsync(uri, this.Cancellation))
			{
				AssertClientObservedPolicy(response, "the plain factory client", expectPolicyHeader: true);
			}

			// Client 2: the keyed HttpClient (AddAsKeyed). AddAsKeyed() registers the keyed HttpClient as a scoped service
			// (so it disposes with its scope), which cannot be resolved from the component's root provider directly; a
			// scope is created here purely to satisfy that DI lifetime rule, not because the client itself needs one.
			using var scope = web.Services.CreateScope();
			var client2 = scope.ServiceProvider.GetRequiredKeyedService<HttpClient>("policy");
			using (var response = await client2.GetAsync(uri, this.Cancellation))
			{
				AssertClientObservedPolicy(response, "the keyed client", expectPolicyHeader: true);
			}

			// Client 3: the typed client (AddHttpClient<TClient>)
			var typed = web.GetRequiredService<ProbeTypedClient>();
			using (var response = await typed.Client.GetAsync(uri, this.Cancellation))
			{
				AssertClientObservedPolicy(response, "the typed client", expectPolicyHeader: true);
			}

			// Client 4: a bare handler from IHttpMessageHandlerFactory, wrapped in a plain HttpClient. Client-level default
			// headers never reach this client (they are applied by the factory to an HttpClient instance, not to the handler
			// chain), so only the credentials marker (which the chain itself applies) is expected here.
			using var handler = web.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler("policy");
			using var client4 = new HttpClient(handler, disposeHandler: false);
			using (var response = await client4.GetAsync(uri, this.Cancellation))
			{
				AssertClientObservedPolicy(response, "the bare handler", expectPolicyHeader: false);
			}

			// Client 5: client 1 again, through the rich send extension.
			var (marker, policy) = await client1.SendAsync(client1.CreateGetRequest(uri), ctx =>
			{
				ctx.EnsureSuccessStatusCode();
				return (Marker: GetHeader(ctx.Response, "x-echo-marker"), Policy: GetHeader(ctx.Response, "x-echo-policy-header"));
			}, this.Cancellation);
			Assert.That(marker, Is.EqualTo("signed"), "the rich send extension: the credentials marker must reach the server");
			Assert.That(policy, Is.EqualTo("from-options"), "the rich send extension: the name's default request header must reach the server");
		}

		[Test]
		public async Task Test_Fake_Time_Advances_Past_A_Client_Timeout()
		{
			// the test clock is a fake-backed NodaTimeProvider, set before MakeItSo so the whole environment (the map, the
			// pipeline handler's timeout, and the virtual transport) shares this one advanceable time source.
			var fake = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
			this.Clock = new NodaTimeProvider(fake);

			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/never", async (HttpContext ctx) =>
					{
						// never completes on its own; only the connection's abort (the caller giving up, here via the timeout)
						// unblocks it, so the host tears down cleanly instead of leaving a handler parked forever.
						var tcs = new TaskCompletionSource();
						await using var registration = ctx.RequestAborted.Register(() => tcs.TrySetResult());
						await tcs.Task;
					}));
					host.ConfigureServices(builder =>
					{
						builder.Services.AddBetterHttpClient("slow", o => o.Timeout = TimeSpan.FromSeconds(30));
					});
				});
			}));

			var web = context.GetWebHost("WEB");
			var uri = web.GetUri("/never");
			var factory = web.GetRequiredService<IHttpClientFactory>();
			using var client1 = factory.CreateClient("slow");

			var task = client1.GetAsync(uri, this.Cancellation);

			// crank virtual time past the configured Timeout: the pipeline handler's timeoutCts (armed on map.Time) fires,
			// which must reach the virtual transport as a real OperationCanceledException from SendAsync (see the
			// VirtualHttpClientHandler.SendAsync fix), so the pipeline handler's own catch produces the canonical shape.
			await AdvanceAndPump(fake, TimeSpan.FromSeconds(31));

			var ex = Assert.ThrowsAsync<TaskCanceledException>(async () => await task);
			Assert.That(ex!.InnerException, Is.InstanceOf<TimeoutException>(), "a client Timeout must surface as a TaskCanceledException wrapping a TimeoutException, the same shape as a real HttpClient.Timeout");
		}

		/// <summary>Typed client used to prove <c>AddHttpClient&lt;TClient&gt;("policy")</c> carries the same named policy as the other clients.</summary>
		public sealed class ProbeTypedClient
		{
			public ProbeTypedClient(HttpClient client) => this.Client = client;

			public HttpClient Client { get; }
		}

		/// <summary>Per-request-only credential that stamps a marker header on every request, standing in for a message signer.</summary>
		private sealed class MarkerCredentials : IBetterCredentials
		{
			public bool IsPerRequestOnly => true;

			HttpMessageHandler IBetterCredentials.Configure(HttpMessageHandler handler, BetterHttpClientOptions options, IServiceProvider services) => handler;

			ValueTask IBetterCredentials.OnBeforeRequest(BetterHttpClientContext context)
			{
				context.Request.Headers.Add("x-marker", "signed");
				return default;
			}
		}

	}

}
