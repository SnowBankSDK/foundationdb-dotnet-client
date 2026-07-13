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
	using System.Diagnostics;
	using Microsoft.AspNetCore.Builder;
	using Microsoft.AspNetCore.Http;
	using Microsoft.Playwright;
	using NUnit.Framework;
	using SnowBank.Testing.Framework;

	/// <summary>W5: the readyPredicate overload gates readiness on an app-side condition.</summary>
	[TestFixture]
	[Explicit("Requires Chromium (auto-installed on first run)")]
	public class PageReadyPredicateFacts : DistributedTest
	{
		private static string PageThatFlipsAfter(int ms) =>
			$"<html><body><script>window.__appReady=false;setTimeout(()=>window.__appReady=true,{ms});</script></body></html>";

		[Test]
		public async Task Test_Predicate_Holds_Marks_Ready_Only_After_It_Flips()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host => host.ConfigureApplication(app =>
					app.MapGet("/", (HttpContext _) => Results.Content(PageThatFlipsAfter(500), "text/html"))));
				lan.WithPlaywrightBrowser("BROWSER");
			}));

			var browser = context.GetPlaywrightBrowser("BROWSER");
			await browser.Page.GotoAsync(context.GetWebHost("WEB").GetUri("/").ToString(), new() { WaitUntil = WaitUntilState.Load });

			var sw = Stopwatch.StartNew();
			var result = await browser.Page.WaitForPageReadyAsync(this.Cancellation,
				readyPredicate: (p, c) => p.EvaluateAsync<bool>("() => window.__appReady === true"));
			sw.Stop();

			Assert.That(result.Settled, Is.True);
			Assert.That(result.Detail, Is.EqualTo("ready"));
			Assert.That(sw.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(400), "should have waited for the app flag");
		}

		[Test]
		public async Task Test_Predicate_Never_Holds_Reports_App_Not_Ready()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host => host.ConfigureApplication(app =>
					app.MapGet("/", (HttpContext _) => Results.Content("<html><body>x</body></html>", "text/html"))));
				lan.WithPlaywrightBrowser("BROWSER");
			}));

			var browser = context.GetPlaywrightBrowser("BROWSER");
			await browser.Page.GotoAsync(context.GetWebHost("WEB").GetUri("/").ToString(), new() { WaitUntil = WaitUntilState.Load });

			var result = await browser.Page.WaitForPageReadyAsync(this.Cancellation,
				networkTimeoutMs: 500,
				readyPredicate: (p, c) => p.EvaluateAsync<bool>("() => window.__never === true"));

			Assert.That(result.Settled, Is.True, "phase 1 (DOM) still succeeded");
			Assert.That(result.Detail, Is.EqualTo("app-not-ready"));
		}
	}
}
