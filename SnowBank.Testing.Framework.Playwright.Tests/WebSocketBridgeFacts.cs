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
	using System.Net.WebSockets;
	using System.Text;
	using Microsoft.AspNetCore.Builder;
	using Microsoft.AspNetCore.Http;
	using Microsoft.Playwright;
	using NUnit.Framework;
	using SnowBank.Testing.Framework;

	/// <summary>Facts for the WebSocket bridge of <see cref="PlaywrightWebBrowserTestComponent"/>: a WebSocket opened by
	/// page javascript is intercepted (<c>RouteWebSocketAsync</c>) and its frames are bridged to the target virtual host's
	/// in-memory <c>TestServer</c>, so the page talks to a purely virtual server exactly like it does for HTTP requests —
	/// no socket is ever bound, no DNS lookup ever happens.</summary>
	[TestFixture]
	[Explicit("Requires Chromium (auto-installed on first run)")]
	public class WebSocketBridgeFacts : DistributedTest
	{

		/// <summary>Serves the test page (a relative-URL WebSocket client writing every event into <c>#log</c>) and a
		/// <c>/ws</c> echo endpoint that also closes on demand with an application close code.</summary>
		/// <remarks>Also reused by <see cref="InteractiveSessionFacts"/> as the content of the parked bubble.</remarks>
		internal static void ConfigureEchoWebHost(MinimalWebHostTestBuilder host)
		{
			host.ConfigureApplication(app =>
			{
				app.UseWebSockets();

				app.MapGet("/", (HttpContext _) => Results.Content(
					"""
					<!DOCTYPE html>
					<html>
					<head><title>ws bridge test</title></head>
					<body>
						<ul id="log"></ul>
						<script>
							const log = (m) => {
								const li = document.createElement('li');
								li.textContent = m;
								document.getElementById('log').appendChild(li);
							};
							const ws = new WebSocket((location.protocol === 'https:' ? 'wss://' : 'ws://') + location.host + '/ws');
							ws.onopen = () => { log('open'); ws.send('hello-bubble'); };
							ws.onmessage = (e) => log('recv:' + e.data);
							ws.onclose = (e) => log('close:' + e.code + ':' + (e.reason || ''));
							ws.onerror = () => log('error');
							window.wsSend = (m) => ws.send(m);
							window.wsClose = () => ws.close(); // deliberately argument-less: exercises the close-arguments shim
						</script>
					</body>
					</html>
					""",
					"text/html"));

				app.Map("/ws", async (HttpContext ctx) =>
				{
					if (!ctx.WebSockets.IsWebSocketRequest)
					{
						ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
						return;
					}
					using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
					var buffer = new byte[64 * 1024];
					while (ws.State == WebSocketState.Open)
					{
						var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ctx.RequestAborted);
						if (result.MessageType == WebSocketMessageType.Close)
						{ // complete the close handshake with whatever the client sent
							await ws.CloseAsync(ws.CloseStatus ?? WebSocketCloseStatus.NormalClosure, ws.CloseStatusDescription, ctx.RequestAborted);
							break;
						}
						var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
						if (text.StartsWith("please-close:", StringComparison.Ordinal))
						{ // server-initiated close with an application close code (fits the mock's 1000/3000-4999 window)
							await ws.CloseAsync((WebSocketCloseStatus) int.Parse(text["please-close:".Length..]), "server-says-bye", ctx.RequestAborted);
							break;
						}
						await ws.SendAsync(Encoding.UTF8.GetBytes("echo:" + text), WebSocketMessageType.Text, endOfMessage: true, ctx.RequestAborted);
					}
				});
			});
		}

		[Test]
		public async Task Test_WebSocket_Echo_Through_The_Bubble()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", ConfigureEchoWebHost);
				lan.WithPlaywrightBrowser("BROWSER");
			}));

			var web = context.GetWebHost("WEB");
			var browser = context.GetPlaywrightBrowser("BROWSER");

			var response = await browser.Page.GotoAsync(web.GetUri("/").ToString(), new() { WaitUntil = WaitUntilState.Load });
			Assert.That(response, Is.Not.Null);
			Assert.That(response!.Ok, $"Response failed {response.Status}");

			// the page's own onload sequence: open, send 'hello-bubble', receive the echo from the virtual server
			await browser.Page.WaitForFunctionAsync(
				"() => document.getElementById('log').textContent.includes('recv:echo:hello-bubble')",
				null,
				new() { Timeout = 15_000 });

			var log = await browser.Page.Locator("#log").InnerTextAsync();
			Assert.That(log, Does.Contain("open"), "the websocket should have opened");
			Assert.That(log, Does.Not.Contain("error"), "the websocket should not have errored");
		}

		[Test]
		public async Task Test_Server_Initiated_Close_Propagates_To_The_Page()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", ConfigureEchoWebHost);
				lan.WithPlaywrightBrowser("BROWSER");
			}));

			var web = context.GetWebHost("WEB");
			var browser = context.GetPlaywrightBrowser("BROWSER");

			await browser.Page.GotoAsync(web.GetUri("/").ToString(), new() { WaitUntil = WaitUntilState.Load });
			await browser.Page.WaitForFunctionAsync(
				"() => document.getElementById('log').textContent.includes('recv:echo:hello-bubble')",
				null,
				new() { Timeout = 15_000 });

			// ask the virtual server to close with an application code; the page must observe it in onclose
			await browser.Page.EvaluateAsync("() => window.wsSend('please-close:4000')");
			await browser.Page.WaitForFunctionAsync(
				"() => document.getElementById('log').textContent.includes('close:4000:server-says-bye')",
				null,
				new() { Timeout = 15_000 });
		}

		[Test]
		public async Task Test_Page_Side_Close_Does_Not_Poison_The_Session()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("WEB", ConfigureEchoWebHost);
				lan.WithPlaywrightBrowser("BROWSER");
			}));

			var web = context.GetWebHost("WEB");
			var browser = context.GetPlaywrightBrowser("BROWSER");

			await browser.Page.GotoAsync(web.GetUri("/").ToString(), new() { WaitUntil = WaitUntilState.Load });
			await browser.Page.WaitForFunctionAsync(
				"() => document.getElementById('log').textContent.includes('recv:echo:hello-bubble')",
				null,
				new() { Timeout = 15_000 });

			// close from page javascript WITHOUT arguments: on Microsoft.Playwright 1.61 a codeless close event
			// crashes the driver connection (upstream bug) unless the component's close-arguments shim defaults it
			await browser.Page.EvaluateAsync("() => window.wsClose()");
			await browser.Page.WaitForFunctionAsync(
				"() => document.getElementById('log').textContent.includes('close:1000:')",
				null,
				new() { Timeout = 15_000 });

			// the driver connection must have survived the close: the page is still scriptable
			var alive = await browser.Page.EvaluateAsync<int>("() => 6 * 7");
			Assert.That(alive, Is.EqualTo(42), "the playwright session should still be alive after a page-side close");
		}

	}

}
