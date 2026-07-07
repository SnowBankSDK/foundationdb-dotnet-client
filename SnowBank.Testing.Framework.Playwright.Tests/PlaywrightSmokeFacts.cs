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

	/// <summary>Smoke test for <see cref="PlaywrightWebBrowserTestComponent"/>: a real Chromium instance navigates to a page
	/// served by a simulated web host over the virtual network, and renders it.</summary>
	[TestFixture]
	[Explicit("Requires Chromium (auto-installed on first run)")]
	public class PlaywrightSmokeFacts : DistributedTest
	{

		[Test]
		public async Task Test_Browser_Renders_Simple_Page()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/", (HttpContext _) => Results.Content("<html><body>hello</body></html>", "text/html")));
				});
				lan.WithPlaywrightBrowser("BROWSER");
			}));

			var web = context.GetWebHost("WEB");
			var browser = context.GetPlaywrightBrowser("BROWSER");

			var response = await browser.Page.GotoAsync(web.GetUri("/").ToString(), new() { WaitUntil = WaitUntilState.Load });
			Assert.That(response, Is.Not.Null);
			Assert.That(response!.Ok, $"Response failed {response.Status}");

			var body = await browser.Page.ContentAsync();
			Assert.That(body, Does.Contain("hello"));
		}

	}

}
