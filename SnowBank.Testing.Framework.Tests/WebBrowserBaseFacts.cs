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
	using System.Text;
	using Microsoft.AspNetCore.Builder;
	using Microsoft.AspNetCore.Http;
	using NUnit.Framework;
	using SnowBank.Networking;

	/// <summary>Self-tests for the <see cref="WebBrowserTestComponent"/> base class:
	/// the install hook (<see cref="WebBrowserTestComponent.EnsureBrowserAvailableAsync"/>) runs once on start,
	/// and <see cref="WebBrowserTestComponent.ForwardToMeshAsync"/> forwards requests onto the virtual network
	/// in both capturing and non-capturing modes.</summary>
	[TestFixture]
	public class WebBrowserBaseFacts : DistributedTest
	{

		/// <summary>Playwright-free browser double that exposes the protected base surface.</summary>
		private sealed class FakeBrowserTestComponent : WebBrowserTestComponent
		{
			public FakeBrowserTestComponent(string id, IVirtualNetworkLocation location, CancellationToken lifetime)
				: base(id, location, lifetime)
			{ }

			public int InstallCalls { get; private set; }

			protected override ValueTask EnsureBrowserAvailableAsync(CancellationToken ct)
			{
				this.InstallCalls++;
				return default;
			}

			protected override void ConfigureServices(WebApplicationBuilder builder) { }

			protected override void ConfigureApplication(WebApplication app) { }

			protected override async ValueTask OnStarting(CancellationToken ct)
			{
				await this.EnsureBrowserAvailableAsync(ct);
			}

			public Task<MeshResponse> Fetch(Uri url, bool capture, CancellationToken ct)
				=> this.ForwardToMeshAsync(HttpMethod.Get, url, [ ], null, null, capture, ct);
		}

		[Test]
		public async Task Test_Base_Forwards_To_Mesh_And_Calls_Install_Hook()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/hello", (HttpContext _) => "world"));
				});

				// register the fake browser as a top-level LAN node
				var browser = new FakeBrowserTestComponent("BROWSER", lan.Location, lan.Top.Lifetime);
				lan.RegisterComponent(browser);
				lan.SetNamedComponent("BROWSER", browser);
				lan.Location.RegisterNetworkService("browser", "BROWSER", null);
			}));

			var web = context.GetWebHost("WEB");
			var browser = context.GetComponent<FakeBrowserTestComponent>("BROWSER");

			Assert.That(browser.InstallCalls, Is.EqualTo(1), "EnsureBrowserAvailableAsync should run once on start");

			foreach (var capture in new[] { true, false })
			{
				var res = await browser.Fetch(web.GetUri("/hello"), capture, this.Cancellation);
				Assert.That(res.Status, Is.EqualTo(HttpStatusCode.OK), $"capture={capture}");
				Assert.That(Encoding.UTF8.GetString(res.Body), Is.EqualTo("world"), $"capture={capture}");
			}
		}

		/// <summary>Regression test for the comma-folding bug in <c>WebBrowserTestComponent.BuildResponse</c>: multiple
		/// <c>Set-Cookie</c> headers on the same response must survive <see cref="WebBrowserTestComponent.ForwardToMeshAsync"/>
		/// as separate <see cref="WebBrowserTestComponent.MeshResponse.Headers"/> entries, not folded into one
		/// comma-joined (and therefore malformed) value. Other multi-value headers are still expected to fold with a
		/// comma, per RFC 9110 §5.3.</summary>
		[Test]
		public async Task Test_Multiple_SetCookie_Headers_Are_Not_Comma_Folded()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/cookies", (HttpContext ctx) =>
					{
						ctx.Response.Cookies.Append("first", "one");
						ctx.Response.Cookies.Append("second", "two");
						return Results.Content("ok", "text/plain");
					}));
				});

				var browser = new FakeBrowserTestComponent("BROWSER", lan.Location, lan.Top.Lifetime);
				lan.RegisterComponent(browser);
				lan.SetNamedComponent("BROWSER", browser);
				lan.Location.RegisterNetworkService("browser", "BROWSER", null);
			}));

			var web = context.GetWebHost("WEB");
			var browser = context.GetComponent<FakeBrowserTestComponent>("BROWSER");

			foreach (var capture in new[] { true, false })
			{
				var res = await browser.Fetch(web.GetUri("/cookies"), capture, this.Cancellation);
				Assert.That(res.Status, Is.EqualTo(HttpStatusCode.OK), $"capture={capture}");

				var setCookies = res.Headers.Where(h => string.Equals(h.Key, "Set-Cookie", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).ToList();
				Assert.That(setCookies, Has.Count.EqualTo(2), $"capture={capture}: expected 2 separate Set-Cookie entries, not one comma-folded value");
				Assert.That(setCookies, Has.Some.StartsWith("first=one"), $"capture={capture}");
				Assert.That(setCookies, Has.Some.StartsWith("second=two"), $"capture={capture}");
			}
		}

	}

}
