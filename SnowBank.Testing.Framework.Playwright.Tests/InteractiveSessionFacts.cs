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
	using Microsoft.Playwright;
	using NUnit.Framework;
	using SnowBank.Testing.Framework;

	/// <summary>Parks a live "bubble universe" (virtual web host + browser with an exposed CDP endpoint) so a human or an
	/// agent-driven external controller can attach to the browser (<c>connectOverCDP</c>) and explore interactively, while
	/// the component keeps owning all routing — everything the external controller does still flows through the virtual
	/// network, the packet capture and the journal.</summary>
	[TestFixture]
	[Explicit("Parks a live bubble universe for interactive debugging — opt-in via SNOWBANK_INTERACTIVE=1")]
	public class InteractiveSessionFacts : DistributedTest
	{

		[Test]
		public async Task Test_Interactive_Session_Parks_Until_Stopped()
		{
			if (Environment.GetEnvironmentVariable("SNOWBANK_INTERACTIVE") != "1")
			{ // guard against a filter sweep accidentally parking a CI run for half an hour
				Assert.Ignore("Set SNOWBANK_INTERACTIVE=1 to park an interactive session.");
			}

			const int CdpPort = 19223;

			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", WebSocketBridgeFacts.ConfigureEchoWebHost);
				lan.WithPlaywrightBrowser("BROWSER", browser => browser.WithRemoteDebugging(CdpPort));
			}));

			var web = context.GetWebHost("WEB");
			var browser = context.GetPlaywrightBrowser("BROWSER");
			await browser.Page.GotoAsync(web.GetUri("/").ToString(), new() { WaitUntil = WaitUntilState.Load });

			string stopFile = Path.Combine(Path.GetTempPath(), "snowbank-interactive", "stop.txt");
			Directory.CreateDirectory(Path.GetDirectoryName(stopFile)!);
			File.Delete(stopFile);

			Log("INTERACTIVE SESSION PARKED");
			Log($"  CDP endpoint : {browser.RemoteDebuggingEndpoint} (attach with connectOverCDP)");
			Log($"  page         : {web.GetUri("/")} (virtual: only reachable through the parked browser)");
			Log($"  stop file    : create '{stopFile}' to end the session (auto-stops after 30 min)");

			var deadline = DateTime.UtcNow.AddMinutes(30);
			while (!File.Exists(stopFile) && DateTime.UtcNow < deadline)
			{
				await Task.Delay(500, this.Cancellation);
			}

			Log("interactive session ended");
		}

	}

}
