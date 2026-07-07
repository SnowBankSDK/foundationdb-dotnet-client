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

namespace SnowBank.Testing.Framework.Playwright.Tests
{
	using Microsoft.AspNetCore.Builder;
	using Microsoft.AspNetCore.Http;
	using Microsoft.Playwright;
	using NUnit.Framework;
	using SnowBank.Testing.Framework;

	/// <summary>Verifies that the Playwright browser's traffic capture records every asset requested by the page, not just the top-level navigation.</summary>
	[TestFixture]
	[Explicit("Requires Chromium (auto-installed on first run)")]
	public class CaptureCorrectnessFacts : DistributedTest
	{

		[Test]
		public async Task Test_Capture_Records_All_Assets()
		{
			const int N = 3;

			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host =>
				{
					host.ConfigureApplication(app => AssetPageHost.MapAssetPage(app, N));
				});
				lan.WithPlaywrightBrowser("BROWSER");
			}));

			var web = context.GetWebHost("WEB");
			var browser = context.GetPlaywrightBrowser("BROWSER");
			browser.CaptureTraffic = true;

			var response = await browser.Page.GotoAsync(web.GetUri("/").ToString(), new() { WaitUntil = WaitUntilState.Load });
			Assert.That(response!.Ok);

			// 1 page + N css + N js + N png
			var packets = context.GetNetworkPackets(_ => true).ToList();
			Log($"captured {packets.Count} packets");
			Assert.That(packets.Count, Is.GreaterThanOrEqualTo(1 + 3 * N), "every asset should be captured");
		}

		/// <summary>Documents a verified Playwright .NET limitation: when a response carries multiple <c>Set-Cookie</c>
		/// headers, only the LAST one reaches the browser through <c>route.FulfillAsync</c>.</summary>
		/// <remarks>
		/// <para>This is not a bug in <c>PlaywrightWebBrowserTestComponent</c>. <c>RouteFulfillOptions.Headers</c> is
		/// typed <c>IEnumerable&lt;KeyValuePair&lt;string, string&gt;&gt;</c> (not a <c>Dictionary</c>), so our own code
		/// passes both <c>Set-Cookie</c> entries through un-collapsed (see <c>BindMeshNetworkRoutingAsync</c>) - but
		/// Microsoft.Playwright 1.61.0's own <c>Core.Route.NormalizeFulfillParametersAsync</c> re-collapses that same
		/// enumerable into a <c>Dictionary&lt;string, string&gt;</c> keyed by lowercased header name before it is sent
		/// to the browser, so duplicate header names still lose all but the last value. This happens inside the
		/// Playwright binding itself, downstream of anything this component can influence.</para>
		/// <para>This test asserts the current (limited) behavior rather than the ideal one, so that a future
		/// Playwright upgrade which starts preserving multi-value headers will fail this test and prompt an update to
		/// this comment (and to <c>BindMeshNetworkRoutingAsync</c>'s), instead of the improvement going unnoticed.</para>
		/// </remarks>
		[Test]
		public async Task Test_Multiple_SetCookie_Headers_Documented_Playwright_Limitation()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/cookies", (HttpContext ctx) =>
					{
						ctx.Response.Cookies.Append("first", "one");
						ctx.Response.Cookies.Append("second", "two");
						return Results.Content("<html><body>cookies set</body></html>", "text/html");
					}));
				});
				lan.WithPlaywrightBrowser("BROWSER");
			}));

			var web = context.GetWebHost("WEB");
			var browser = context.GetPlaywrightBrowser("BROWSER");

			var response = await browser.Page.GotoAsync(web.GetUri("/cookies").ToString(), new() { WaitUntil = WaitUntilState.Load });
			Assert.That(response!.Ok);

			var cookies = await browser.BrowserContext.CookiesAsync();
			Log($"browser holds {cookies.Count} cookie(s): {string.Join(", ", cookies.Select(c => $"{c.Name}={c.Value}"))}");

			// Only the last Set-Cookie header ("second") survives route.FulfillAsync's internal Dictionary collapse.
			Assert.That(cookies.Count, Is.EqualTo(1), "only one cookie is expected to survive - see remarks on this test");
			Assert.That(cookies.Any(c => c.Name == "second" && c.Value == "two"), "the LAST Set-Cookie header should win");
		}

	}

}
