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
	using NUnit.Framework;
	using SnowBank.Testing.Framework;

	/// <summary>Facts for the remote debugging (CDP) endpoint of <see cref="PlaywrightWebBrowserTestComponent"/>, the one
	/// deliberate real loopback socket that lets an external controller attach to the bubble's browser.</summary>
	[TestFixture]
	[Explicit("Requires Chromium (auto-installed on first run)")]
	public class RemoteDebuggingFacts : DistributedTest
	{

		[Test]
		public async Task Test_Remote_Debugging_Endpoint_Responds()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host => host.ConfigureApplication(app =>
					app.MapGet("/", (HttpContext _) => Results.Content("<html><body>cdp</body></html>", "text/html"))));
				lan.WithPlaywrightBrowser("BROWSER", browser => browser.WithRemoteDebugging(19222));
			}));

			var browser = context.GetPlaywrightBrowser("BROWSER");
			Assert.That(browser.RemoteDebuggingEndpoint, Is.Not.Null, "the component should have verified and recorded the CDP endpoint");

			// this is what an external controller does first when attaching over CDP
			using var client = new HttpClient() { BaseAddress = browser.RemoteDebuggingEndpoint };
			var version = await client.GetStringAsync("/json/version", this.Cancellation);
			Assert.That(version, Does.Contain("webSocketDebuggerUrl"), "the CDP endpoint should describe the browser websocket url");
		}

	}

}
