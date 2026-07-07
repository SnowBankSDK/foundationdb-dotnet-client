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
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Playwright;
	using NUnit.Framework;
	using SnowBank.Networking.PacketCapture;
	using SnowBank.Testing.Framework;

	/// <summary>Measures the wall-clock cost of rendering an asset-heavy page through the Playwright browser component,
	/// contrasting the raw forwarding path (<c>CaptureTraffic=false</c>) with the capturing <c>BetterHttpClient</c> path
	/// (<c>CaptureTraffic=true</c>, which fires the packet-capture filter) across a growing number of assets.</summary>
	/// <remarks>This is a diagnostic benchmark, not a pass/fail test; it exists to attribute the packet-capture overhead
	/// observed in production. It is <see cref="ExplicitAttribute">Explicit</see> and multi-minute at N=200.</remarks>
	[TestFixture]
	[Explicit("Perf benchmark; requires Chromium; multi-minute runtime")]
	public class CaptureBenchmarkFacts : DistributedTest
	{

		[TestCase(false, TestName = "Render_Raw_NoFilter")]
		[TestCase(true, TestName = "Render_Capturing_ViaFilter")]
		public async Task Measure_Render_Cost(bool capture)
		{
			foreach (int n in new[] { 3, 50, 200 })
			{
				var context = await MakeItSo(env => env.AddSimpleLan(lan =>
				{
					lan.WithMinimalWebHost("WEB", host =>
					{
						host.ConfigureApplication(app => AssetPageHost.MapAssetPage(app, n));
					});
					lan.WithPlaywrightBrowser("BROWSER");
				}));

				var web = context.GetWebHost("WEB");
				var browser = context.GetPlaywrightBrowser("BROWSER");
				browser.CaptureTraffic = capture;

				var sw = Stopwatch.StartNew();
				var response = await browser.Page.GotoAsync(web.GetUri("/").ToString(), new() { WaitUntil = WaitUntilState.Load, Timeout = 120_000 });
				sw.Stop();
				Assert.That(response!.Ok);

				int packets = context.GetNetworkPackets(_ => true).Count();
				double perReq = n > 0 ? sw.ElapsedMilliseconds / (double) (1 + 3 * n) : 0;
				Log($"capture={capture,-5} N={n,4} assets={1 + 3 * n,4} elapsed={sw.ElapsedMilliseconds,6}ms per_req={perReq,7:F2}ms packets={packets}");
			}
		}

		/// <summary>Step-3 escalation: re-runs the capturing case while post-configuring <see cref="PacketCaptureOptions"/>
		/// to split the remaining per-request cost between stack-trace capture and body capture (headers-only).</summary>
		/// <remarks>If <c>PostConfigure</c> does not move the timings at all, that is itself a finding: the running options
		/// were snapshotted at manager construction and cannot be tuned per-run.</remarks>
		[TestCase(false, TestName = "Render_Capturing_NoStackTraces")]
		[TestCase(true, TestName = "Render_Capturing_HeadersOnly")]
		public async Task Measure_Render_Cost_Variant(bool headersOnly)
		{
			foreach (int n in new[] { 3, 50, 200 })
			{
				var context = await MakeItSo(env => env.AddSimpleLan(lan =>
				{
					lan.WithMinimalWebHost("WEB", host =>
					{
						host.ConfigureApplication(app => AssetPageHost.MapAssetPage(app, n));
					});
					lan.WithPlaywrightBrowser("BROWSER", b => b.ConfigureServices(builder =>
						builder.Services.PostConfigure<PacketCaptureOptions>(o =>
						{
							o.CaptureStackTraces = false;
							if (headersOnly)
							{
								o.AllowedFields = CapturedHttpFields.RequestPropertiesAndHeaders | CapturedHttpFields.ResponsePropertiesAndHeaders;
							}
						})));
				}));

				var web = context.GetWebHost("WEB");
				var browser = context.GetPlaywrightBrowser("BROWSER");
				browser.CaptureTraffic = true;

				var sw = Stopwatch.StartNew();
				var response = await browser.Page.GotoAsync(web.GetUri("/").ToString(), new() { WaitUntil = WaitUntilState.Load, Timeout = 120_000 });
				sw.Stop();
				Assert.That(response!.Ok);

				int packets = context.GetNetworkPackets(_ => true).Count();
				double perReq = n > 0 ? sw.ElapsedMilliseconds / (double) (1 + 3 * n) : 0;
				Log($"variant headersOnly={headersOnly,-5} N={n,4} assets={1 + 3 * n,4} elapsed={sw.ElapsedMilliseconds,6}ms per_req={perReq,7:F2}ms packets={packets}");
			}
		}

		/// <summary>Heavy bodies: renders a page whose every css/js asset is a realistic ~2 MB body,
		/// raw vs capturing, to test whether the <c>InterceptedHttpContent.ToArray()</c> double-copy + unbounded retention
		/// becomes expensive at real asset sizes (the synthetic assets in the main benchmark are ~256 B).</summary>
		[TestCase(false, TestName = "Heavy_Raw_NoFilter")]
		[TestCase(true, TestName = "Heavy_Capturing_ViaFilter")]
		public async Task Measure_Render_Cost_HeavyBodies(bool capture)
		{
			const int BodyBytes = 2 * 1024 * 1024; // ~2 MB per css/js asset
			foreach (int n in new[] { 50 })
			{
				var context = await MakeItSo(env => env.AddSimpleLan(lan =>
				{
					lan.WithMinimalWebHost("WEB", host =>
					{
						host.ConfigureApplication(app => AssetPageHost.MapAssetPage(app, n, BodyBytes));
					});
					lan.WithPlaywrightBrowser("BROWSER");
				}));

				var web = context.GetWebHost("WEB");
				var browser = context.GetPlaywrightBrowser("BROWSER");
				browser.CaptureTraffic = capture;

				var sw = Stopwatch.StartNew();
				var response = await browser.Page.GotoAsync(web.GetUri("/").ToString(), new() { WaitUntil = WaitUntilState.Load, Timeout = 120_000 });
				sw.Stop();
				Assert.That(response!.Ok);

				int packets = context.GetNetworkPackets(_ => true).Count();
				double ms = sw.Elapsed.TotalMilliseconds;
				double perReq = ms / (1 + 3 * n);
				Log($"heavy capture={capture,-5} N={n,4} assets={1 + 3 * n,4} bodyKB={BodyBytes / 1024,6} elapsed={ms,9:F1}ms per_req={perReq,8:F2}ms packets={packets}");
			}
		}

		/// <summary>Stack traces: re-runs the capturing case with <c>CaptureStackTraces=true</c> (the default is
		/// <see langword="false"/>, so the main capturing benchmark never pays this), measuring the cost of the
		/// <c>new StackTrace(2).ToString()</c> taken per request in <c>PacketCaptureHttpFilter.Configure</c>.</summary>
		[Test]
		public async Task Measure_Render_Cost_StackTracesOn()
		{
			foreach (int n in new[] { 50, 200 })
			{
				var context = await MakeItSo(env => env.AddSimpleLan(lan =>
				{
					lan.WithMinimalWebHost("WEB", host =>
					{
						host.ConfigureApplication(app => AssetPageHost.MapAssetPage(app, n));
					});
					lan.WithPlaywrightBrowser("BROWSER", b => b.ConfigureServices(builder =>
						builder.Services.PostConfigure<PacketCaptureOptions>(o => o.CaptureStackTraces = true)));
				}));

				var web = context.GetWebHost("WEB");
				var browser = context.GetPlaywrightBrowser("BROWSER");
				browser.CaptureTraffic = true;

				var sw = Stopwatch.StartNew();
				var response = await browser.Page.GotoAsync(web.GetUri("/").ToString(), new() { WaitUntil = WaitUntilState.Load, Timeout = 120_000 });
				sw.Stop();
				Assert.That(response!.Ok);

				int packets = context.GetNetworkPackets(_ => true).Count();
				double ms = sw.Elapsed.TotalMilliseconds;
				double perReq = ms / (1 + 3 * n);
				Log($"stacktraces=ON  N={n,4} assets={1 + 3 * n,4} elapsed={ms,9:F1}ms per_req={perReq,8:F2}ms packets={packets}");
			}
		}

	}

}
