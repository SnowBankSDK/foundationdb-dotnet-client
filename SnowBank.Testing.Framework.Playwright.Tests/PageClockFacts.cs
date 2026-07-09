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

	/// <summary>Facts for the virtual page clock of <see cref="PlaywrightWebBrowserTestComponent"/>: with
	/// <c>WithVirtualClock()</c> the page's timers only move when the test advances them — the browser-side twin of the
	/// hosts' fake <c>TimeProvider</c>, enabling whole-topology time travel (or deliberate one-sided skew).</summary>
	[TestFixture]
	[Explicit("Requires Chromium (auto-installed on first run)")]
	public class PageClockFacts : DistributedTest
	{

		private const string ClockPageHtml =
			"""
			<!DOCTYPE html>
			<html>
			<head><title>page clock test</title></head>
			<body>
				<div>ticks=<span id="ticks">0</span></div>
				<div>once=<span id="once">pending</span></div>
				<script>
					let ticks = 0;
					setInterval(() => { ticks++; document.getElementById('ticks').textContent = ticks; }, 1000);
					setTimeout(() => { document.getElementById('once').textContent = 'fired'; }, 5000);
				</script>
			</body>
			</html>
			""";

		private Task<DistributedTestContext> MakeClockTopology()
		{
			return MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host => host.ConfigureApplication(app =>
					app.MapGet("/", (HttpContext _) => Results.Content(ClockPageHtml, "text/html"))));
				lan.WithPlaywrightBrowser("BROWSER", browser => browser.WithVirtualClock());
			}));
		}

		[Test]
		public async Task Test_Virtual_Clock_Freezes_Then_Advance_Fires_Every_Timer()
		{
			var context = await MakeClockTopology();

			var web = context.GetWebHost("WEB");
			var browser = context.GetPlaywrightBrowser("BROWSER");

			await browser.Page.GotoAsync(web.GetUri("/").ToString(), new() { WaitUntil = WaitUntilState.Load });

			// frozen virtual time: a generous REAL settle must not fire any page timer
			await Wait(700);
			Assert.That(await browser.Page.Locator("#ticks").TextContentAsync(), Is.EqualTo("0"), "a frozen virtual clock must not tick on the wall clock");
			Assert.That(await browser.Page.Locator("#once").TextContentAsync(), Is.EqualTo("pending"));

			// advancing fires every timer that falls due along the way (RunFor semantics)
			await browser.AdvanceBrowserClockAsync(TimeSpan.FromSeconds(3), this.Cancellation);
			Assert.That(await browser.Page.Locator("#ticks").TextContentAsync(), Is.EqualTo("3"), "a 3s advance must fire the 1s interval 3 times");
			Assert.That(await browser.Page.Locator("#once").TextContentAsync(), Is.EqualTo("pending"), "the 5s timeout must not have fired yet");

			await browser.AdvanceBrowserClockAsync(TimeSpan.FromSeconds(3), this.Cancellation);
			Assert.That(await browser.Page.Locator("#ticks").TextContentAsync(), Is.EqualTo("6"));
			Assert.That(await browser.Page.Locator("#once").TextContentAsync(), Is.EqualTo("fired"), "the 5s timeout must have fired during the second advance");
		}

		[Test]
		public async Task Test_Fast_Forward_Fires_Pending_Timers_At_Most_Once()
		{
			var context = await MakeClockTopology();

			var web = context.GetWebHost("WEB");
			var browser = context.GetPlaywrightBrowser("BROWSER");

			await browser.Page.GotoAsync(web.GetUri("/").ToString(), new() { WaitUntil = WaitUntilState.Load });

			// a 10s jump emulates suspend/resume: the 1s interval fires ONCE, not 10 times
			await browser.FastForwardBrowserClockAsync(TimeSpan.FromSeconds(10), this.Cancellation);
			Assert.That(await browser.Page.Locator("#ticks").TextContentAsync(), Is.EqualTo("1"), "fast-forward fires interval timers at most once (laptop-lid semantics)");
			Assert.That(await browser.Page.Locator("#once").TextContentAsync(), Is.EqualTo("fired"), "the pending one-shot timeout fires on resume");
		}

	}

}
