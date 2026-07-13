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

	/// <summary>W4: WithConsoleFormatter replaces the default JS-console formatting and can drop lines.</summary>
	[TestFixture]
	[Explicit("Requires Chromium (auto-installed on first run)")]
	public class ConsoleFormatterFacts : DistributedTest
	{
		[Test]
		public async Task Test_ConsoleFormatter_Sees_Messages_And_Controls_Logging()
		{
			var seen = new List<(string Type, string Text, bool Kept)>();

			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host =>
				{
					host.ConfigureApplication(app => app.MapGet("/", (HttpContext _) => Results.Content(
						"<html><body><script>console.log('hello');console.error('boom');window.__done=true;</script></body></html>", "text/html")));
				});
				lan.WithPlaywrightBrowser("BROWSER", b => b.WithConsoleFormatter(msg =>
				{
					bool keep = msg.Type == "error";
					lock (seen) seen.Add((msg.Type, msg.Text, keep));
					return keep ? $"ERR {msg.Text}" : null; // drop logs, keep errors
				}));
			}));

			var web = context.GetWebHost("WEB");
			var browser = context.GetPlaywrightBrowser("BROWSER");
			await browser.Page.GotoAsync(web.GetUri("/").ToString(), new() { WaitUntil = WaitUntilState.Load });
			await browser.Page.WaitForFunctionAsync("() => window.__done === true");

			// console events are delivered to the .NET handler slightly after; poll briefly
			for (int i = 0; i < 40 && (CountOf(seen, "log") == 0 || CountOf(seen, "error") == 0); i++)
			{
				await Task.Delay(50);
			}

			Assert.That(seen.Any(s => s.Type == "log" && s.Text == "hello" && !s.Kept), Is.True, "log seen and dropped");
			Assert.That(seen.Any(s => s.Type == "error" && s.Text == "boom" && s.Kept), Is.True, "error seen and kept");
		}

		private static int CountOf(List<(string Type, string Text, bool Kept)> seen, string type)
		{
			lock (seen) return seen.Count(s => s.Type == type);
		}
	}
}
