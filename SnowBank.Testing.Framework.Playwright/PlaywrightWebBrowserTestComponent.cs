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

		internal Func<PlaywrightWebBrowserTestComponent, CancellationToken, ValueTask> StartingHandler { get; set; } = (_, _) => default;

		internal Func<PlaywrightWebBrowserTestComponent, CancellationToken, ValueTask> StoppingHandler { get; set; } = (_, _) => default;

		internal Func<ValueTask> DestroyingHandler { get; set; } = () => default;

		/// <summary>When <see langword="true"/> (the default), requests forwarded onto the virtual network route through the
		/// capturing path so they show up as packets in the test journal. Set to <see langword="false"/> to bypass capture.</summary>
		public bool CaptureTraffic { get; set; } = true;

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
			var contextOptions = this.Services.GetRequiredService<BrowserNewContextOptions>();

			// 3. Make sure Chromium is installed (self-healing, concurrency-safe), then launch a standalone worker thread
			await this.EnsureBrowserAvailableAsync(ct);
			this.Browser = await this.LaunchChromiumWithAutoInstallAsync(launchOptions, ct);
			this.BrowserContext = await this.Browser.NewContextAsync(contextOptions);

			// page-side network tracker (re-injected into every new document): feeds the adaptive readiness wait
			// (PlaywrightPageExtensions.WaitForPageReadyAsync), replacing fixed delays in tests
			await this.BrowserContext.AddInitScriptAsync(PlaywrightPageExtensions.NetworkTrackerInitScript);

			// 4. Establish the catch-all pipe intercept loop to bypass socket port bindings, forwarding through the base mesh helper
			await BindMeshNetworkRoutingAsync(this.BrowserContext);

			// 5. Instantiate the base automation page context
			this.Page = await this.BrowserContext.NewPageAsync();

			// hook-up the Javascript Console logger
			var logger = this.CreateLogger<PlaywrightWebBrowserTestComponent>();
			this.Page.Console += (sender, e) =>
			{
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
						Microsoft.Playwright.Program.Main([ "install", "chromium" ]);
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

			await this.StoppingHandler(this, ct);
		}

		protected sealed override async ValueTask OnDisposing()
		{
			// read the nullable cores: a failed startup can leave some of these unset, and a throwing
			// getter here would mask the original startup exception with a secondary teardown failure
			using (this.DriverCore)
			await using (this.BrowserCore)
			await using (this.BrowserContextCore)
			{
				var packet = this.Services.GetService<PacketCaptureManager>();
				packet?.Shutdown();

				await this.DestroyingHandler();
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
			host.StartingHandler = this.StartingHandler ?? host.StartingHandler;
			host.StoppingHandler = this.StoppingHandler ?? host.StoppingHandler;

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
