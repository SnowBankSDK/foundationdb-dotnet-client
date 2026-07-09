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

	/// <summary>Facts for <see cref="PlaywrightPageExtensions.WaitForPageReadyAsync"/>: the adaptive page-readiness wait
	/// driven by the network tracker init-script that the browser component injects into every document.</summary>
	[TestFixture]
	[Explicit("Requires Chromium (auto-installed on first run)")]
	public class PageReadyFacts : DistributedTest
	{

		private async Task<(IDistributedTestContext Context, PlaywrightWebBrowserTestComponent Browser, Uri Root)> StartAsync(Action<WebApplication> configure)
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host =>
				{
					host.ConfigureApplication(configure);
				});
				lan.WithPlaywrightBrowser("BROWSER");
			}));

			var web = context.GetWebHost("WEB");
			var browser = context.GetPlaywrightBrowser("BROWSER");
			return (context, browser, web.GetUri("/"));
		}

		[Test]
		public async Task Test_Ready_Settles_Fast_On_Quiet_Page()
		{
			// a plain static page: DOM completes and no tracked request is ever in flight,
			// so the wait must settle on its own signals (no fixed delay) and report "ready"
			var (_, browser, root) = await StartAsync(app =>
			{
				app.MapGet("/", (HttpContext _) => Results.Content("<html><body>quiet</body></html>", "text/html"));
			});

			var response = await browser.Page.GotoAsync(root.ToString(), new() { WaitUntil = WaitUntilState.Load });
			Assert.That(response, Is.Not.Null.And.Property("Ok").True);

			var result = await browser.Page.WaitForPageReadyAsync(this.Cancellation);

			Assert.That(result.Settled, Is.True, "DOM phase should settle");
			Assert.That(result.Detail, Is.EqualTo("ready"), "network phase should be quiet");
		}

		[Test]
		public async Task Test_Ready_Waits_For_InFlight_Tracked_Fetch()
		{
			// the page fires a fetch on startup that only completes server-side after a delay, then mutates the DOM;
			// the "load" event does NOT wait for that fetch, so only the injected tracker can tell the wait to hold on.
			// If the tracker init-script were missing, the quiet-phase would settle immediately and the marker would
			// still read "pending".
			var (_, browser, root) = await StartAsync(app =>
			{
				app.MapGet("/", (HttpContext _) => Results.Content(
					"""
					<html><body><div id='status'>pending</div>
					<script>
					fetch('/data').then(() => { document.getElementById('status').textContent = 'loaded'; });
					</script>
					</body></html>
					""", "text/html"));

				app.MapGet("/data", async (HttpContext ctx) =>
				{
					await Task.Delay(700, ctx.RequestAborted);
					return Results.Json(new { ok = true });
				});
			});

			var response = await browser.Page.GotoAsync(root.ToString(), new() { WaitUntil = WaitUntilState.Load });
			Assert.That(response, Is.Not.Null.And.Property("Ok").True);

			var result = await browser.Page.WaitForPageReadyAsync(this.Cancellation);

			Assert.That(result.Settled, Is.True, "DOM phase should settle");
			Assert.That(result.Detail, Is.EqualTo("ready"), "the delayed fetch should complete within the network phase");

			var status = await browser.Page.EvaluateAsync<string>("() => document.getElementById('status').textContent");
			Assert.That(status, Is.EqualTo("loaded"), "the wait should have covered the in-flight fetch AND its DOM mutation");
		}

		[Test]
		public async Task Test_Ready_Ignores_Hub_Style_Requests()
		{
			// hub/signalr connections are long-lived by design and never complete; the tracker must NOT count them,
			// otherwise the network-quiet phase would always exhaust its timeout and report "network-busy"
			// (this is precisely what made Playwright's native NetworkIdle unusable in this harness)
			var (_, browser, root) = await StartAsync(app =>
			{
				app.MapGet("/", (HttpContext _) => Results.Content(
					"""
					<html><body><h1>hub</h1>
					<script>
					window.__hubDone = false;
					fetch('/signalr/hubs').finally(() => { window.__hubDone = true; });
					</script>
					</body></html>
					""", "text/html"));

				app.MapGet("/signalr/hubs", async (HttpContext ctx) =>
				{
					// must OUTLIVE the network-quiet timeout (3s), so that a tracker counting this request by mistake
					// would provably degrade the verdict to "network-busy"; bounded so the request still drains before teardown
					await Task.Delay(6_000, ctx.RequestAborted);
					return Results.NoContent();
				});
			});

			var response = await browser.Page.GotoAsync(root.ToString(), new() { WaitUntil = WaitUntilState.Load });
			Assert.That(response, Is.Not.Null.And.Property("Ok").True);

			var result = await browser.Page.WaitForPageReadyAsync(this.Cancellation);

			Assert.That(result.Settled, Is.True, "DOM phase should settle");
			Assert.That(result.Detail, Is.EqualTo("ready"), "the pending hub request must be ignored by the tracker");

			// drain the long-lived request before teardown, so it does not abort mid-flight in the mesh
			await browser.Page.WaitForFunctionAsync("() => window.__hubDone === true", null, new() { Timeout = 10_000 });
		}

		[Test]
		public async Task Test_Explicit_Ready_Flag_Short_Circuits_Dom_Heuristic()
		{
			// a page held hostage by a slow resource (readyState never reaches 'complete') can still declare
			// itself ready EXPLICITLY by setting the well-known flag; the wait must honor it instead of timing out
			var (_, browser, root) = await StartAsync(app =>
			{
				app.MapGet("/", (HttpContext _) => Results.Content(
					$$"""
					<html><head><script>window.{{PlaywrightPageExtensions.ReadyFlagName}} = true;</script></head>
					<body><h1>spa</h1><img src='/slow.png'/></body></html>
					""", "text/html"));

				app.MapGet("/slow.png", async (HttpContext ctx) =>
				{
					try { await Task.Delay(10_000, ctx.RequestAborted); }
					catch (OperationCanceledException) { }
					return Results.NotFound();
				});
			});

			// WaitUntil=Load would hang on the slow image; the explicit flag is exactly for this shape of page
			var response = await browser.Page.GotoAsync(root.ToString(), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
			Assert.That(response, Is.Not.Null.And.Property("Ok").True);

			var result = await browser.Page.WaitForPageReadyAsync(this.Cancellation, timeoutMs: 3_000);

			Assert.That(result.Settled, Is.True, "the explicit flag should short-circuit the DOM heuristic");
			Assert.That(result.Detail, Is.EqualTo("ready"), "the slow <img> is not an XHR/fetch and must not be counted");

			// prove the flag (not the DOM heuristic) is what settled the wait
			var readyState = await browser.Page.EvaluateAsync<string>("() => document.readyState");
			Assert.That(readyState, Is.Not.EqualTo("complete"), "the document should still be waiting on the slow image");
		}

	}

}
