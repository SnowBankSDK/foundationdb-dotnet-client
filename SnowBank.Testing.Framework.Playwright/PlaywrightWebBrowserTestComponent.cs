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

namespace SnowBank.Testing.Framework.Playwright
{
	using System.Net.WebSockets;
	using System.Text;
	using Microsoft.AspNetCore.Builder;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.DependencyInjection.Extensions;
	using Microsoft.Playwright;
	using SnowBank.Diagnostics.Contracts;
	using SnowBank.Networking;
	using SnowBank.Networking.PacketCapture;

	/// <summary>Simulated web browser test component that drives a real Chromium instance (via Playwright) and routes all of its
	/// HTTP traffic onto the virtual network, so that pages it navigates to are served by the other simulated hosts.</summary>
	public sealed class PlaywrightWebBrowserTestComponent : WebBrowserTestComponent
	{

		#region Lifecycle...

		internal List<Action<WebApplicationBuilder>> ConfigureServicesHandlers { get; set; } = [];

		internal List<Action<WebApplication>> ConfigureApplicationHandlers { get; set; } = [];

		internal List<Action<BrowserTypeLaunchOptions>> BrowserOptionMutators { get; set; } = [];

		internal List<Action<BrowserNewContextOptions>> ContextOptionMutators { get; set; } = [];

		internal List<string> InitScripts { get; set; } = [];

		internal Func<IConsoleMessage, string?>? ConsoleFormatter { get; set; }

		internal bool SnapshotsEnabled { get; set; }

		internal PlaywrightSnapshotOptions? SnapshotOptions { get; set; }

		/// <summary>Snapshot writer for this browser, once started with <c>WithSnapshots()</c>; otherwise <see langword="null"/>.</summary>
		public PlaywrightSnapshotWriter? Snapshots { get; private set; }

		internal Func<PlaywrightWebBrowserTestComponent, CancellationToken, ValueTask> StartingHandler { get; set; } = (_, _) => default;

		internal Func<PlaywrightWebBrowserTestComponent, CancellationToken, ValueTask> StoppingHandler { get; set; } = (_, _) => default;

		internal Func<ValueTask> DestroyingHandler { get; set; } = () => default;

		/// <summary>When <see langword="true"/> (the default), requests forwarded onto the virtual network route through the
		/// capturing path so they show up as packets in the test journal. Set to <see langword="false"/> to bypass capture.</summary>
		public bool CaptureTraffic { get; set; } = true;

		/// <summary>When <see langword="true"/>, the page runs under a VIRTUAL clock (Playwright's Clock API): <c>Date</c>,
		/// <c>setTimeout</c>/<c>setInterval</c>, <c>performance.now</c> and <c>requestAnimationFrame</c> are faked, seeded
		/// from this component's <c>IClock</c> so browser and virtual hosts share the same epoch, and time only moves when
		/// the test calls <see cref="AdvanceBrowserClockAsync"/> / <see cref="FastForwardBrowserClockAsync"/>.</summary>
		/// <remarks>This is the browser-side twin of the hosts' fake <c>TimeProvider</c>: advance BOTH in lockstep (in small
		/// steps, with real-time pumps in between so cross-boundary I/O can land) to emulate passage of time across the
		/// whole topology; advance only one side to deliberately inject timeout/skew scenarios. Vue's render scheduling
		/// rides on microtasks (not timers), so components still repaint normally under the fake clock.</remarks>
		public bool UseVirtualClock { get; set; }

		/// <summary>Optional TCP port on which Chromium exposes its remote debugging (CDP) endpoint.</summary>
		/// <remarks>
		/// <para>This is the ONE deliberate crack in the bubble: the endpoint is a REAL loopback socket, so that an external
		/// controller (an inspector, an agent-driven Playwright client, ...) can attach to the SAME browser with
		/// <c>connectOverCDP</c> and co-drive the pages while this component keeps owning all routing, everything the
		/// external controller triggers still flows through the virtual network, the packet capture and the journal.</para>
		/// <para>Pick a unique port per parked test; startup fails fast if the endpoint does not answer (port already in use).</para>
		/// </remarks>
		public int? RemoteDebuggingPort { get; set; }

		/// <summary>Remote debugging (CDP) endpoint of the running browser, when <see cref="RemoteDebuggingPort"/> was set; otherwise <see langword="null"/>.</summary>
		public Uri? RemoteDebuggingEndpoint { get; private set; }

		public PlaywrightWebBrowserTestComponent(string id, IVirtualNetworkLocation location, CancellationToken lifetime,
			IDistributedTestComponent? parent = null)
			: base(id, location, lifetime, parent)
		{
		}

		protected override void ConfigureServices(WebApplicationBuilder builder)
		{
			builder.Services.TryAddSingleton<BrowserTypeLaunchOptions>(new BrowserTypeLaunchOptions
			{
				Headless = true
			});

			builder.Services.TryAddSingleton<BrowserNewContextOptions>(new BrowserNewContextOptions
			{
				UserAgent = "Mozilla/5.0 (SnowBank Virtual Browser Component Framework)"
			});

			// configuration logic customized for this specific instance
			foreach (var handler in this.ConfigureServicesHandlers)
			{
				handler(builder);
			}
		}

		protected override void ConfigureApplication(WebApplication app)
		{
			foreach (var handler in this.ConfigureApplicationHandlers)
			{
				handler(app);
			}
		}

		// nullable backing properties: teardown must be able to observe "never assigned" without throwing,
		// when the startup sequence failed midway (e.g. Chromium install failure after the driver was created)

		private IPlaywright? DriverCore { get; set; }

		private IBrowser? BrowserCore { get; set; }

		private IBrowserContext? BrowserContextCore { get; set; }

		private IPage? PageCore { get; set; }

		public IPlaywright Driver
		{
			get => this.DriverCore ?? throw new InvalidOperationException("Browser is not ready yet");
			set => this.DriverCore = value;
		}

		public IBrowser Browser
		{
			get => this.BrowserCore ?? throw new InvalidOperationException("Browser is not ready yet");
			set => this.BrowserCore = value;
		}

		public IBrowserContext BrowserContext
		{
			get => this.BrowserContextCore ?? throw new InvalidOperationException("Browser is not ready yet");
			set => this.BrowserContextCore = value;
		}

		public IPage Page
		{
			get => this.PageCore ?? throw new InvalidOperationException("Browser is not ready yet");
			set => this.PageCore = value;
		}

		protected sealed override async ValueTask OnStarting(CancellationToken ct)
		{
			var packet = this.Services.GetService<PacketCaptureManager>();
			if (packet != null && !packet.IsRunning) packet.Start();

			this.Driver = await Playwright.CreateAsync();

			// 2. Resolve configured execution mechanics from the isolated node container
			var launchOptions = this.Services.GetRequiredService<BrowserTypeLaunchOptions>();
			if (this.BrowserOptionMutators.Count > 0)
			{ // apply on a clone so the shared DI singleton is never mutated (mirrors the remote-debugging clone below)
				launchOptions = new BrowserTypeLaunchOptions(launchOptions);
				foreach (var mutate in this.BrowserOptionMutators) mutate(launchOptions);
			}

			var contextOptions = this.Services.GetRequiredService<BrowserNewContextOptions>();
			if (this.ContextOptionMutators.Count > 0)
			{
				contextOptions = new BrowserNewContextOptions(contextOptions);
				foreach (var mutate in this.ContextOptionMutators) mutate(contextOptions);
			}

			// 3. Make sure Chromium is installed (self-healing, concurrency-safe), then launch a standalone worker thread
			await this.EnsureBrowserAvailableAsync(ct);
			if (this.RemoteDebuggingPort is int debugPort)
			{ // clone: the registered options object is a shared singleton, it must not be mutated in place
				launchOptions = new BrowserTypeLaunchOptions(launchOptions)
				{
					Args = [ ..(launchOptions.Args ?? [ ]), $"--remote-debugging-port={debugPort}" ],
				};
			}
			this.Browser = await this.LaunchChromiumWithAutoInstallAsync(launchOptions, ct);
			if (this.RemoteDebuggingPort is int port)
			{
				await this.VerifyRemoteDebuggingEndpointAsync(port, ct);
			}
			this.BrowserContext = await this.Browser.NewContextAsync(contextOptions);

			// page-side network tracker (re-injected into every new document): feeds the adaptive readiness wait
			// (PlaywrightPageExtensions.WaitForPageReadyAsync), replacing fixed delays in tests
			await this.BrowserContext.AddInitScriptAsync(PlaywrightPageExtensions.NetworkTrackerInitScript);

			// 4. Establish the catch-all pipe intercept loop to bypass socket port bindings, forwarding through the base mesh helper
			await BindMeshNetworkRoutingAsync(this.BrowserContext);

			// 4b. Intercept page WebSockets the same way, bridging their frames to the target virtual host's in-memory TestServer
			await BindMeshWebSocketRoutingAsync(this.BrowserContext);

			// consumer init scripts (W2): after the package's own scripts (network tracker + WebSocket shim), before the first page
			foreach (var script in this.InitScripts)
			{
				await this.BrowserContext.AddInitScriptAsync(script);
			}

			// 5. Instantiate the base automation page context
			this.Page = await this.BrowserContext.NewPageAsync();

			if (this.UseVirtualClock)
			{ // must install before anything runs in the page, so scripts only ever see the faked time sources
				var epoch = this.Clock.GetCurrentInstant().ToDateTimeOffset().UtcDateTime;
				await this.Page.Clock.InstallAsync(new() { TimeDate = epoch });
				this.Log($"# <CLOCK> virtual page clock installed, epoch={epoch:O}");
			}

			// hook-up the Javascript Console logger
			this.Page.Console += (sender, e) =>
			{
				if (this.ConsoleFormatter is { } formatter)
				{
					var line = formatter(e);
					if (line is not null) this.Log(line);
					return;
				}

				string msg = e.Text;
				if (e.Type == "error")
				{
					this.Log($"! <JS> {e.Type}: {msg.Replace("\n", "\n!> ")}, Location={e.Location}");
				}
				else
				{
					this.Log($"# <JS> {e.Type}: {msg.Replace("\n", "\n#> ")}, Location={e.Location}");
				}
			};

			if (this.SnapshotsEnabled)
			{
				this.Snapshots = new PlaywrightSnapshotWriter(this.Id, this.SnapshotOptions ?? new PlaywrightSnapshotOptions());
			}

			await this.StartingHandler(this, ct);
		}

		private async Task BindMeshNetworkRoutingAsync(IBrowserContext context)
		{
			await context.RouteAsync("**/*", async route =>
			{
				var browserRequest = route.Request;
				try
				{
					var headers = browserRequest.Headers.Select(h => new KeyValuePair<string, string>(h.Key, h.Value));
					byte[]? body = string.IsNullOrEmpty(browserRequest.PostData) ? null : Encoding.UTF8.GetBytes(browserRequest.PostData);
					var contentType = browserRequest.Headers.TryGetValue("content-type", out var ct) ? ct : null;

					var response = await this.ForwardToMeshAsync(
						new HttpMethod(browserRequest.Method),
						new Uri(browserRequest.Url, UriKind.Absolute),
						headers,
						body,
						contentType,
						this.CaptureTraffic,
						this.Cancellation);

					// RouteFulfillOptions.Headers is typed IEnumerable<KeyValuePair<string, string>> (not a Dictionary), so
					// passing response.Headers straight through (instead of collapsing it with ToDictionary, which would
					// throw on a duplicate key) is strictly better and lets several entries with the same header name
					// survive AT OUR LAYER.
					//
					// KNOWN PLAYWRIGHT LIMITATION (verified against Microsoft.Playwright 1.61.0's own source,
					// Core/Route.cs, NormalizeFulfillParametersAsync): internally, route.FulfillAsync re-collapses this
					// same IEnumerable into a `Dictionary<string, string>` keyed by the lowercased header name
					// ("resultHeaders[header.Key.ToLowerInvariant()] = header.Value"), so a response with multiple
					// Set-Cookie headers still only delivers the LAST one to the browser - confirmed empirically too
					// (see CaptureCorrectnessFacts.Test_Multiple_SetCookie_Headers_Documented_Playwright_Limitation). This collapse happens
					// inside the Playwright .NET binding itself, downstream of anything we pass here, so it cannot be
					// worked around via RouteFulfillOptions - only a manual BrowserContext.AddCookiesAsync() injection
					// (parsing each Set-Cookie value ourselves) could fully preserve multiple cookies, which is a much
					// larger change than this transparent-proxy path is scoped for.
					await route.FulfillAsync(new RouteFulfillOptions
					{
						Status = (int) response.Status,
						Headers = response.Headers,
						BodyBytes = response.Body,
					});
				}
				catch (Exception ex)
				{
					this.Log($"! <MESH> forward failed for {browserRequest.Method} {browserRequest.Url}: {ex}");
					await route.AbortAsync();
				}
			});
		}

		#region WebSocket Mesh Bridge...

		/// <summary>Init script that forces every page-side <c>WebSocket.close()</c> to carry a close code and reason.</summary>
		/// <remarks>
		/// <para>Microsoft.Playwright 1.61 crashes on any WebSocket close event that omits <c>code</c>/<c>reason</c> (which the
		/// protocol allows, the TS client types them <c>number | undefined</c>): <c>WebSocketRoute.OnMessage</c> reads them with
		/// the throwing <c>JsonElement.GetProperty</c>, and the exception escapes <c>Connection.Dispatch</c>, poisoning the WHOLE
		/// driver connection (the browser dies, every later call throws <c>TargetClosedException</c>). Reported upstream (2026-07).</para>
		/// <para>The <c>@microsoft/signalr</c> client (and many others) call <c>ws.close()</c> with no arguments during cleanup,
		/// so without this shim any such close kills the entire test mid-flight. It must be registered AFTER
		/// <c>RouteWebSocketAsync</c>: the route installs its own init script that REPLACES <c>window.WebSocket</c> with a mock
		/// class, and the patch must land on that mock's prototype, not on the native one.</para>
		/// </remarks>
		private const string WebSocketCloseArgumentsShim =
			"""
			{
				const WS = window.WebSocket;
				if (WS && WS.prototype && WS.prototype.close)
				{
					const origClose = WS.prototype.close;
					WS.prototype.close = function(code, reason) { return origClose.call(this, code ?? 1000, reason ?? 'client-closed'); };
				}
			}
			""";

		private object WebSocketBridgeLock { get; } = new();

		/// <summary>Bridged websocket routes that are still connected: teardown closes them with an explicit code before
		/// the browser goes away, so no codeless close event reaches the 1.61 binding while the test is still running.</summary>
		private List<IWebSocketRoute> LiveWebSocketRoutes { get; } = [];

		private async Task BindMeshWebSocketRoutingAsync(IBrowserContext context)
		{
			await context.RouteWebSocketAsync("**/*", this.BridgePageWebSocket);

			// must come after the route registration, so the shim patches the mock WebSocket class (see remarks on the shim)
			await context.AddInitScriptAsync(WebSocketCloseArgumentsShim);
		}

		/// <summary>Bridges one page-created WebSocket to the in-memory <c>TestServer</c> of the virtual host its URL points to.</summary>
		/// <remarks>
		/// <para>This is the WebSocket analog of <see cref="BindMeshNetworkRoutingAsync"/>: the page believes it holds a real
		/// socket, but frames are pumped between the Playwright mock and a <c>TestServer.CreateWebSocketClient()</c> connection,
		/// no socket is bound, no DNS lookup happens.</para>
		/// <para>The page side is wired BEFORE the server connect completes: early frames (e.g. the SignalR handshake, sent from
		/// <c>onopen</c>) are queued on a send chain that starts when the server socket is up, awaiting the connect first
		/// silently drops them, and the server then kills the connection after its handshake timeout.</para>
		/// <para>Connect-time offline states reject the connection (like the HTTP transport), and both endpoints' online
		/// tokens sever an ESTABLISHED bridge (close code 4001). Directional link cuts (<c>VirtualNetworkCutEdge</c>) are NOT
		/// yet honored, the topology's edge API is internal; take this bridge there when it moves into the framework.</para>
		/// </remarks>
		private void BridgePageWebSocket(IWebSocketRoute pageWs)
		{
			var uri = new Uri(pageWs.Url, UriKind.Absolute);

			// resolve the target host like the virtual DNS would
			var host = this.NetworkMap.FindHost(uri.Host);
			if (host is null)
			{
				this.Log($"! <WS> found no host matching '{uri.Host}' visible from '{this.Id}' for {uri}");
				_ = pageWs.CloseAsync(new() { Code = 4404, Reason = $"simulated name resolution failure for '{uri.Host}'" });
				return;
			}

			// offline endpoints reject NEW connections (established ones are severed by the online tokens below)
			if (host.Offline || this.NetworkMap.Host.Offline)
			{
				this.Log($"! <WS> host '{host.Id}' or '{this.Id}' is offline, rejecting {uri}");
				_ = pageWs.CloseAsync(new() { Code = 4503, Reason = $"simulated connection failure: host '{host.Id}' is offline" });
				return;
			}

			// find the component that owns this simulated host, to reach its in-memory TestServer
			var target = this.Context.FindComponents<DistributedTestComponent>()
				.FirstOrDefault(c => !ReferenceEquals(c, this) && host.Equals(c.NetworkMap.Host));
			if (target is null)
			{
				this.Log($"! <WS> found no test component owning host '{host.Id}' for {uri}");
				_ = pageWs.CloseAsync(new() { Code = 4502, Reason = $"simulated connection failure: no server on host '{host.Id}'" });
				return;
			}

			this.Log($"# <WS> bridging {uri} to virtual host '{target.Id}'");

			// sever the established bridge if either endpoint goes offline mid-flight (like a cut TCP link)
			var severTokens = new List<CancellationToken>(3) { this.Cancellation };
			if (this.NetworkMap.Host is VirtualNetworkTopology.SimulatedHost self) severTokens.Add(self.OnlineToken);
			if (host is VirtualNetworkTopology.SimulatedHost remote) severTokens.Add(remote.OnlineToken);
			var severed = CancellationTokenSource.CreateLinkedTokenSource([ ..severTokens ]);

			var wsClient = target.Server.CreateWebSocketClient();
			foreach (var protocol in pageWs.Protocols)
			{
				wsClient.SubProtocols.Add(protocol);
			}
			var serverWsTask = wsClient.ConnectAsync(uri, severed.Token);

			lock (this.WebSocketBridgeLock) { this.LiveWebSocketRoutes.Add(pageWs); }

			// page -> server: wired synchronously, sends chained in order behind the connect (see remarks)
			var sendChain = (Task) serverWsTask;
			pageWs.OnMessage(frame =>
			{
				sendChain = sendChain.ContinueWith(async _ =>
				{
					try
					{
						var serverWs = await serverWsTask.ConfigureAwait(false);
						if (frame.Text is not null)
						{
							await serverWs.SendAsync(Encoding.UTF8.GetBytes(frame.Text), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None).ConfigureAwait(false);
						}
						else
						{
							await serverWs.SendAsync(frame.Binary!, WebSocketMessageType.Binary, endOfMessage: true, CancellationToken.None).ConfigureAwait(false);
						}
					}
					catch (Exception ex)
					{
						this.Log($"! <WS> page->server send failed for {pageWs.Url}: {ex.Message}");
					}
				}, TaskScheduler.Default).Unwrap();
			});

			// page -> server: close (chained after any pending sends, so it cannot overtake them)
			pageWs.OnClose((code, reason) =>
			{
				this.Log($"# <WS> page closed ({code?.ToString() ?? "no code"}) for {pageWs.Url}");
				sendChain = sendChain.ContinueWith(async _ =>
				{
					try
					{
						var serverWs = await serverWsTask.ConfigureAwait(false);
						if (serverWs.State is WebSocketState.Open or WebSocketState.CloseReceived)
						{
							await serverWs.CloseOutputAsync((WebSocketCloseStatus) (code ?? 1000), reason ?? "", CancellationToken.None).ConfigureAwait(false);
						}
					}
					catch
					{
						// the server socket may already be gone; nothing to propagate
					}
				}, TaskScheduler.Default).Unwrap();
			});

			// server -> page pump
			_ = Task.Run(() => PumpServerToPageAsync(pageWs, serverWsTask, severed), CancellationToken.None);
		}

		private async Task PumpServerToPageAsync(IWebSocketRoute pageWs, Task<WebSocket> serverWsTask, CancellationTokenSource severed)
		{
			WebSocket? serverWs = null;
			try
			{
				serverWs = await serverWsTask.ConfigureAwait(false);
				var buffer = new byte[64 * 1024];
				using var ms = new MemoryStream();
				while (serverWs.State is WebSocketState.Open or WebSocketState.CloseSent)
				{
					var result = await serverWs.ReceiveAsync(new ArraySegment<byte>(buffer), severed.Token).ConfigureAwait(false);
					if (result.MessageType == WebSocketMessageType.Close)
					{
						this.Log($"# <WS> server closed ({(int?) result.CloseStatus}) for {pageWs.Url}");
						var (code, reason) = CoercePageCloseArguments((int?) result.CloseStatus, result.CloseStatusDescription);
						await pageWs.CloseAsync(new() { Code = code, Reason = reason }).ConfigureAwait(false);
						break;
					}
					ms.Write(buffer, 0, result.Count);
					if (result.EndOfMessage)
					{
						if (result.MessageType == WebSocketMessageType.Text)
						{
							pageWs.Send(Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int) ms.Length));
						}
						else
						{
							pageWs.Send(ms.ToArray());
						}
						ms.SetLength(0);
					}
				}
			}
			catch (OperationCanceledException)
			{ // severed: an endpoint went offline, or the test is shutting down
				this.Log($"# <WS> bridge severed for {pageWs.Url}");
				try { await pageWs.CloseAsync(new() { Code = 4001, Reason = "simulated connection severed" }).ConfigureAwait(false); }
				catch { /* the page may already be gone */ }
			}
			catch (Exception ex)
			{
				this.Log($"! <WS> server->page pump failed for {pageWs.Url}: {ex.Message}");
				try { await pageWs.CloseAsync(new() { Code = 4502, Reason = "simulated connection failure" }).ConfigureAwait(false); }
				catch { /* the page may already be gone */ }
			}
			finally
			{
				serverWs?.Dispose();
				severed.Dispose();
				lock (this.WebSocketBridgeLock) { this.LiveWebSocketRoutes.Remove(pageWs); }
			}
		}

		/// <summary>Coerces a server-side close into arguments the Playwright mock accepts.</summary>
		/// <remarks>The injected mock validates close codes like a browser does: only <c>1000</c> or <c>3000..4999</c> are
		/// allowed. Out-of-window codes (e.g. <c>1001 GoingAway</c>) are folded to <c>1000</c> with the original code
		/// prepended to the reason, so tests can still observe what the server actually sent.</remarks>
		private static (int Code, string Reason) CoercePageCloseArguments(int? code, string? reason)
		{
			if (code is int c && (c == 1000 || (c is >= 3000 and <= 4999)))
			{
				return (c, reason ?? "");
			}
			return (1000, string.IsNullOrEmpty(reason) ? $"[{code?.ToString() ?? "?"}]" : $"[{code}] {reason}");
		}

		/// <summary>Advances the page's virtual clock by <paramref name="delta"/>, firing EVERY timer that falls due along
		/// the way (like real time passing): an interval scheduled every second fires ~N times over an N-second advance.</summary>
		/// <remarks>Requires <see cref="UseVirtualClock"/>. To move a whole topology through time, advance the hosts' fake
		/// <c>TimeProvider</c> and this clock in lockstep, in small steps with real-time pumps in between (the network
		/// between browser and hosts still runs on real time, so in-flight frames need real milliseconds to land).</remarks>
		public async Task AdvanceBrowserClockAsync(TimeSpan delta, CancellationToken ct)
		{
			// note: the Playwright protocol has no cancellation for clock calls; ct only guards entry
			ct.ThrowIfCancellationRequested();
			if (!this.UseVirtualClock) throw new InvalidOperationException("The virtual page clock is not enabled on this browser component (see UseVirtualClock / WithVirtualClock).");
			await this.Page.Clock.RunForAsync((long) delta.TotalMilliseconds);
		}

		/// <summary>Jumps the page's virtual clock forward by <paramref name="delta"/>, firing pending timers AT MOST ONCE
		/// (like a laptop waking from sleep): an interval scheduled every second fires a single time over any jump.</summary>
		/// <remarks>Requires <see cref="UseVirtualClock"/>. Use this to emulate suspend/resume; use
		/// <see cref="AdvanceBrowserClockAsync"/> to emulate time genuinely passing.</remarks>
		public async Task FastForwardBrowserClockAsync(TimeSpan delta, CancellationToken ct)
		{
			// note: the Playwright protocol has no cancellation for clock calls; ct only guards entry
			ct.ThrowIfCancellationRequested();
			if (!this.UseVirtualClock) throw new InvalidOperationException("The virtual page clock is not enabled on this browser component (see UseVirtualClock / WithVirtualClock).");
			await this.Page.Clock.FastForwardAsync((long) delta.TotalMilliseconds);
		}

		/// <summary>Waits for the Chromium remote debugging endpoint to answer, and records it in <see cref="RemoteDebuggingEndpoint"/>.</summary>
		/// <remarks>Deliberately probes over a REAL loopback socket: the CDP endpoint exists precisely so that EXTERNAL
		/// tools can attach to the browser (see <see cref="RemoteDebuggingPort"/>), a stale listener or a port conflict
		/// must fail the component start with an actionable message, not surface later as a confusing connect error.</remarks>
		private async Task VerifyRemoteDebuggingEndpointAsync(int port, CancellationToken ct)
		{
			var endpoint = new Uri($"http://127.0.0.1:{port}");
			using var probe = new HttpClient { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(2) };
			for (int attempt = 0; attempt < 10; attempt++)
			{
				try
				{
					var version = await probe.GetStringAsync("/json/version", ct);
					this.RemoteDebuggingEndpoint = endpoint;
					this.Log($"# <CDP> remote debugging endpoint ready at {endpoint} :: {version.Replace("\n", " ").Replace("\r", "")}");
					return;
				}
				catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
				{
					await Task.Delay(200, ct);
				}
			}
			throw new InvalidOperationException($"The Chromium remote debugging endpoint did not answer on {endpoint}, the port may already be bound by another process (a previous parked session?); pick a unique port per test.");
		}

		/// <summary>Matches the exception signature of the playwright-dotnet 1.61 WebSocketRoute close-event bug (see
		/// <see cref="WebSocketCloseArgumentsShim"/>): once the driver connection is poisoned, teardown calls surface it as
		/// <c>TargetClosedException</c>/<c>PlaywrightException</c> wrapping "The given key was not present in the dictionary".</summary>
		private static bool IsKnownWebSocketRouteCloseBug(Exception ex) =>
			ex is KeyNotFoundException
			|| (ex is PlaywrightException && ex.Message.Contains("The given key was not present", StringComparison.Ordinal))
			|| (ex.InnerException is not null && IsKnownWebSocketRouteCloseBug(ex.InnerException));

		#endregion

		// Chromium install must happen at most once per process even if several browser
		// components (or NUnit parallel tests) reach the missing-executable path together.
		private static readonly SemaphoreSlim InstallGate = new(1, 1);

		private async Task<IBrowser> LaunchChromiumWithAutoInstallAsync(BrowserTypeLaunchOptions launchOptions, CancellationToken ct)
		{
			try
			{
				return await this.Driver.Chromium.LaunchAsync(launchOptions);
			}
			catch (PlaywrightException ex) when (ex.Message.Contains("Executable doesn't exist"))
			{
				// serialize installs in-process (double-checked)
				await InstallGate.WaitAsync(ct);
				try
				{
					// re-check inside the gate: another caller may have installed while we waited
					try
					{
						return await this.Driver.Chromium.LaunchAsync(launchOptions);
					}
					catch (PlaywrightException ex2) when (ex2.Message.Contains("Executable doesn't exist"))
					{
						// still missing => this caller does the install
					}

					this.Log("Chromium not installed ; running 'playwright install chromium'...");
					// cross-process guard: if the suite is ever sharded across processes, a named
					// system mutex stops two processes installing into the same dir at once.
					using var mutex = new Mutex(false, @"Global\snowbank-playwright-install-chromium");
					var held = false;
					try
					{
						try { held = mutex.WaitOne(TimeSpan.FromMinutes(5)); }
						catch (AbandonedMutexException) { held = true; } // prior holder crashed; re-install is idempotent
						int exitCode = Microsoft.Playwright.Program.Main([ "install", "chromium" ]);
						if (exitCode != 0)
						{
							// fail HERE with an actionable message, instead of falling through to the retry-launch
							// below which would only report the generic "Executable doesn't exist" again
							throw new InvalidOperationException($"Automatic 'playwright install chromium' failed with exit code {exitCode}. Run it manually via 'pwsh bin/Debug/<tfm>/playwright.ps1 install chromium'.", ex);
						}
					}
					finally
					{
						if (held) mutex.ReleaseMutex();
					}

					return await this.Driver.Chromium.LaunchAsync(launchOptions);
				}
				finally
				{
					InstallGate.Release();
				}
			}
		}

		protected sealed override async ValueTask OnStopping(CancellationToken ct)
		{
			var packet = this.Services.GetService<PacketCaptureManager>();
			if (packet != null && packet.IsRunning) packet.PrepareShutdown();

			// close the still-bridged websockets with an explicit code while the driver connection is healthy:
			// letting them die with the page would emit codeless close events that crash the 1.61 binding
			// (see WebSocketCloseArgumentsShim remarks); the page-destruction ones are contained in OnDisposing.
			IWebSocketRoute[] liveRoutes;
			lock (this.WebSocketBridgeLock) { liveRoutes = this.LiveWebSocketRoutes.ToArray(); }
			foreach (var ws in liveRoutes)
			{
				try { await ws.CloseAsync(new() { Code = 1000, Reason = "test shutdown" }); }
				catch { /* the browser (or the driver connection) may already be gone */ }
			}

			if (this.Snapshots is { } snapshots)
			{
				try { await snapshots.WriteContactSheetAsync(ct); }
				catch (Exception ex) { this.Log($"# <SNAP> contact sheet write failed (ignored): {ex.Message}"); }
			}

			await this.StoppingHandler(this, ct);
		}

		protected sealed override async ValueTask OnDisposing()
		{
			// read the nullable cores: a failed startup can leave some of these unset, and a throwing
			// getter here would mask the original startup exception with a secondary teardown failure
			using (this.DriverCore)
			{
				try
				{
					await using (this.BrowserCore)
					await using (this.BrowserContextCore)
					{
						var packet = this.Services.GetService<PacketCaptureManager>();
						packet?.Shutdown();

						await this.DestroyingHandler();
					}
				}
				catch (Exception ex) when (IsKnownWebSocketRouteCloseBug(ex))
				{
					// destroying a page that ever had a routed WebSocket makes the upstream dispatcher emit
					// closePage/closeServer with ONLY wasClean (no code/reason), even for sockets that closed
					// cleanly earlier, and the 1.61 binding poisons the driver connection on it. Unavoidable
					// from user code; the browser is being discarded anyway, so contain the known signature
					// (the driver process itself is reaped by disposing DriverCore above).
					this.Log($"# <WS> teardown hit the known playwright-dotnet 1.61 WebSocketRoute close-event bug (contained): {ex.GetType().Name}");
				}
			}
		}

		#endregion

	}

	public class PlaywrightWebBrowserTestComponentBuilder : IHostTestBuilder
	{
		/// <inheritdoc/>
		public string Id => this.Component.Id;

		public required PlaywrightWebBrowserTestComponent Component { get; init; }

		/// <inheritdoc/>
		IDistributedTestComponent IHostTestBuilder.Component => this.Component;

		/// <inheritdoc/>
		public required IDistributedTestNetworkBuilder Parent { get; init; }

		/// <inheritdoc/>
		public IDistributedTestContext Context => this.Component.Context;

		public IVirtualNetworkLocation Location => this.Component.Location;

		/// <inheritdoc/>
		public VirtualHostIdentity Identity => this.Component.NetworkIdentity;

		internal List<Action<WebApplicationBuilder>> ServiceHandlers { get; } = [];

		internal List<Action<WebApplication>> ApplicationHandlers { get; } = [];

		internal List<Action<BrowserTypeLaunchOptions>> BrowserOptionMutators { get; } = [];

		internal List<Action<BrowserNewContextOptions>> ContextOptionMutators { get; } = [];

		internal List<string> InitScripts { get; } = [];

		internal Func<IConsoleMessage, string?>? ConsoleFormatter { get; private set; }

		internal bool SnapshotsEnabled { get; private set; }

		internal PlaywrightSnapshotOptions SnapshotOptions { get; } = new();

		internal int? RemoteDebuggingPort { get; private set; }

		internal bool UseVirtualClock { get; private set; }

		/// <summary>Exposes the browser's remote debugging (CDP) endpoint on a real loopback port, so an external controller
		/// (an inspector, an agent-driven Playwright client, ...) can attach to the same browser with <c>connectOverCDP</c>
		/// and co-drive it while the component keeps owning all (virtual) routing.</summary>
		/// <param name="port">Loopback TCP port to expose; must be unique per concurrently-running test.</param>
		public void WithRemoteDebugging(int port)
		{
			Contract.GreaterThan(port, 0);
			this.RemoteDebuggingPort = port;
		}

		/// <summary>Runs the page under a virtual clock, advanced explicitly by the test (see
		/// <see cref="PlaywrightWebBrowserTestComponent.UseVirtualClock"/> for semantics and the lockstep recipe).</summary>
		public void WithVirtualClock()
		{
			this.UseVirtualClock = true;
		}

		/// <summary>Mutates the browser launch options ON TOP of the component's defaults (does not replace them). Multiple calls compose in registration order.</summary>
		public void WithBrowserOptions(Action<BrowserTypeLaunchOptions> configure)
		{
			Contract.NotNull(configure);
			this.BrowserOptionMutators.Add(configure);
		}

		/// <summary>Mutates the browser context options (viewport, UserAgent, ...) ON TOP of the component's defaults (does not replace them). Multiple calls compose in registration order.</summary>
		public void WithContextOptions(Action<BrowserNewContextOptions> configure)
		{
			Contract.NotNull(configure);
			this.ContextOptionMutators.Add(configure);
		}

		/// <summary>Injects a context-level init script, evaluated in every document AFTER the package's own scripts and BEFORE the first page. Multiple calls compose in registration order.</summary>
		public void WithInitScript(string script)
		{
			Contract.NotNullOrEmpty(script);
			this.InitScripts.Add(script);
		}

		/// <summary>Replaces the default JS-console log line with <paramref name="formatter"/>: the returned string is written to the test journal, or the message is dropped when it returns <see langword="null"/>. When not set, the default formatting is used.</summary>
		public void WithConsoleFormatter(Func<IConsoleMessage, string?> formatter)
		{
			Contract.NotNull(formatter);
			this.ConsoleFormatter = formatter;
		}

		/// <summary>Enables browser snapshots (a full-page PNG per <c>Snapshots.CaptureAsync(...)</c> call, plus an HTML contact sheet written at teardown) into the per-test output directory.</summary>
		public void WithSnapshots(Action<PlaywrightSnapshotOptions>? configure = null)
		{
			this.SnapshotsEnabled = true;
			configure?.Invoke(this.SnapshotOptions);
		}

		internal Func<PlaywrightWebBrowserTestComponent, CancellationToken, ValueTask>? StartingHandler { get; private set; }

		internal Func<PlaywrightWebBrowserTestComponent, CancellationToken, ValueTask>? StoppingHandler { get; private set; }

		/// <summary>Registers a callback that will be able to add custom services to this host</summary>
		/// <remarks>This method can be called multiple times to register multiple callbacks. They will execute in the same order as they were registered.</remarks>
		public void ConfigureServices(Action<WebApplicationBuilder> handler) => this.ServiceHandlers.Add(handler);

		/// <summary>Registers a callback that will be able to configure the simulated application host</summary>
		/// <remarks>This method can be called multiple times to register multiple callbacks. They will execute in the same order as they were registered.</remarks>
		public void ConfigureApplication(Action<WebApplication> handler) => this.ApplicationHandlers.Add(handler);

		/// <summary>Adds a subcomponent to this component</summary>
		public void AddSubComponent(IDistributedTestComponent component)
		{
			this.Component.AddSubComponent(component);
		}

		/// <summary>Registers a callback that will run when the host becomes ready, but before the test method can use it.</summary>
		/// <remarks>Please note that each simulated host can start in a non-deterministic order, and this method cannot rely on other hosts to be ready as well.</remarks>
		public void OnStartup(Action handler) => this.StartingHandler = (_, ct) =>
		{
			if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
			handler();
			return default;
		};

		/// <summary>Registers a callback that will run when the host becomes ready, but before the test method can use it.</summary>
		/// <remarks>Please note that each simulated host can start in a non-deterministic order, and this method cannot rely on other hosts to be ready as well.</remarks>
		public void OnStartup(Action<IServiceProvider> handler) => this.StartingHandler = (host, ct) =>
		{
			if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
			handler(host.Services);
			return default;
		};

		/// <summary>Registers a callback that will run when the host becomes ready, but before the test method can use it.</summary>
		/// <remarks>Please note that each simulated host can start in a non-deterministic order, and this method cannot rely on other hosts to be ready as well.</remarks>
		public void OnStartup(Func<PlaywrightWebBrowserTestComponent, CancellationToken, ValueTask> handler) => this.StartingHandler = handler;

		/// <summary>Registers a callback that will run when the host becomes ready, but before the test method can use it.</summary>
		/// <remarks>Please note that all simulated hosts will start in a non-deterministic order, so this callback cannot rely on other hosts to be ready as well.</remarks>
		public void OnStartup(Delegate handler)
		{
			var magic = MagicDelegate.Create(handler);
			this.StartingHandler = (host, ct) =>
			{
				if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
				magic.Invoke(host.Services);
				return default;
			};
		}

		/// <summary>Registers a callback that will run when the test has completed (successfully or not), to help release any resources used by this host.</summary>
		/// <remarks>Please note that all simulated hosts will stop in a non-deterministic order, so this callback cannot on other hosts still being alive.</remarks>
		public void OnShutdown(Action handler) => this.StoppingHandler = (_, ct) =>
		{
			if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
			handler();
			return default;
		};

		/// <summary>Registers a callback that will run when the test has completed (successfully or not), to help release any resources used by this host.</summary>
		/// <remarks>Please note that all simulated hosts will stop in a non-deterministic order, so this callback cannot on other hosts still being alive.</remarks>
		public void OnShutdown(Action<PlaywrightWebBrowserTestComponent> handler) => this.StoppingHandler = (host, ct) =>
		{
			if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
			handler(host);
			return default;
		};

		/// <summary>Registers a callback that will run when the test has completed (successfully or not), to help release any resources used by this host.</summary>
		/// <remarks>Please note that all simulated hosts will stop in a non-deterministic order, so this callback cannot on other hosts still being alive.</remarks>
		public void OnShutdown(Func<PlaywrightWebBrowserTestComponent, CancellationToken, ValueTask> handler) => this.StoppingHandler = handler;

		/// <summary>Registers a callback that will run when the test has completed (successfully or not), to help release any resources used by this host.</summary>
		/// <remarks>Please note that all simulated hosts will stop in a non-deterministic order, so this callback cannot on other hosts still being alive.</remarks>
		public void OnShutdown(Delegate handler)
		{
			var magic = MagicDelegate.Create(handler);
			this.StoppingHandler = (host, ct) =>
			{
				if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
				magic.Invoke(host.Services);
				return default;
			};
		}

		public void Activate(PlaywrightWebBrowserTestComponent host)
		{
			Contract.NotNull(host);

			host.ConfigureServicesHandlers.AddRange(this.ServiceHandlers);
			host.ConfigureApplicationHandlers.AddRange(this.ApplicationHandlers);
			host.BrowserOptionMutators.AddRange(this.BrowserOptionMutators);
			host.ContextOptionMutators.AddRange(this.ContextOptionMutators);
			host.InitScripts.AddRange(this.InitScripts);
			host.ConsoleFormatter = this.ConsoleFormatter ?? host.ConsoleFormatter;
			host.SnapshotsEnabled = this.SnapshotsEnabled || host.SnapshotsEnabled;
			if (this.SnapshotsEnabled) host.SnapshotOptions = this.SnapshotOptions;
			host.StartingHandler = this.StartingHandler ?? host.StartingHandler;
			host.StoppingHandler = this.StoppingHandler ?? host.StoppingHandler;
			host.RemoteDebuggingPort = this.RemoteDebuggingPort ?? host.RemoteDebuggingPort;
			host.UseVirtualClock = this.UseVirtualClock || host.UseVirtualClock;

			this.Parent.RegisterComponent(host);
			this.Parent.SetNamedComponent(this.Id, host);

			this.Parent.Location.RegisterNetworkService("host", this.Id, "browser");
			this.Parent.Location.RegisterNetworkService("browser", this.Id, null);
		}

	}

	public static class PlaywrightWebBrowserTestComponentExtensions
	{

		/// <summary>Adds a simulated <see cref="PlaywrightWebBrowserTestComponent">Playwright web browser</see> to this virtual network</summary>
		public static PlaywrightWebBrowserTestComponent WithPlaywrightBrowser(this IDistributedTestNetworkBuilder builder, string id, Action<PlaywrightWebBrowserTestComponentBuilder>? configure = null)
		{
			Contract.NotNull(builder);
			Contract.NotNullOrEmpty(id);

			var host = new PlaywrightWebBrowserTestComponent(id, builder.Location, builder.Top.Lifetime);
			var hostBuilder = new PlaywrightWebBrowserTestComponentBuilder() { Component = host, Parent = builder };
			configure?.Invoke(hostBuilder);

			hostBuilder.Activate(host);
			return host;
		}

		/// <summary>Returns a simulated <see cref="PlaywrightWebBrowserTestComponent">Playwright web browser</see> hosted by this test environment</summary>
		public static PlaywrightWebBrowserTestComponent GetPlaywrightBrowser(this IDistributedTestContext env, string id)
		{
			return env.GetComponent<PlaywrightWebBrowserTestComponent>(id);
		}

	}

}
