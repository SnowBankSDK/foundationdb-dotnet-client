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
	using NUnit.Framework;
	using SnowBank.Testing.Framework;

	/// <summary>W3: snapshot writer output directory + HTML contact sheet.</summary>
	[TestFixture]
	public class SnapshotFacts : DistributedTest
	{
		[Test] // pure, no browser: runs in the default suite
		public void Test_ContactSheet_Html_Lists_Every_Shot()
		{
			var shots = new[]
			{
				new PlaywrightSnapshotWriter.SnapshotRecord("001-login.png", "login", "2026-07-13T10:00:00.0000000Z"),
				new PlaywrightSnapshotWriter.SnapshotRecord("002-home.png", "home", "2026-07-13T10:00:05.0000000Z"),
			};

			string html = PlaywrightSnapshotWriter.BuildContactSheetHtml(shots);

			Assert.That(html, Does.Contain("001-login.png"));
			Assert.That(html, Does.Contain("002-home.png"));
			Assert.That(html, Does.Contain("login"));
			Assert.That(html, Does.Contain("home"));
			Assert.That(System.Text.RegularExpressions.Regex.Matches(html, "<img").Count, Is.EqualTo(2), "one <img> per shot");
		}

		[Test]
		[Explicit("Requires Chromium (auto-installed on first run)")]
		public async Task Test_Snapshots_Are_Written_To_The_Output_Directory()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", host => host.ConfigureApplication(app =>
					app.MapGet("/", (Microsoft.AspNetCore.Http.HttpContext _) => Microsoft.AspNetCore.Http.Results.Content("<html><body>snap</body></html>", "text/html"))));
				lan.WithPlaywrightBrowser("BROWSER", b => b.WithSnapshots());
			}));

			var browser = context.GetPlaywrightBrowser("BROWSER");
			await browser.Page.GotoAsync(context.GetWebHost("WEB").GetUri("/").ToString(), new() { WaitUntil = Microsoft.Playwright.WaitUntilState.Load });

			Assert.That(browser.Snapshots, Is.Not.Null, "WithSnapshots should have created the writer");
			await browser.Snapshots!.CaptureAsync(browser.Page, "home", this.Cancellation);
			await browser.Snapshots!.CaptureAsync(browser.Page, "home again", this.Cancellation);
			await browser.Snapshots!.WriteContactSheetAsync(this.Cancellation);

			var dir = browser.Snapshots!.OutputDirectory;
			Assert.That(Directory.GetFiles(dir, "*.png").Length, Is.EqualTo(2), "two PNGs written");
			Assert.That(File.Exists(Path.Combine(dir, "index.html")), Is.True, "contact sheet written");
		}
	}
}
