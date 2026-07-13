SnowBank.Testing.Framework.Playwright
=====================================

A virtual web browser component for the SnowBank distributed-test framework, backed by a real [Playwright](https://playwright.dev/dotnet/)-driven Chromium.

# Concept

The SnowBank distributed-test framework (`SnowBank.Testing.Framework`) lets you spin up several simulated hosts (web hosts, agents, services) on an in-memory **virtual network** inside a single test, with all of their traffic routed in-process, without real sockets or TCP ports.

This package adds a **browser** to that network: a real Chromium instance, driven through Playwright, whose HTTP and WebSocket traffic is routed onto the same virtual network. So a genuine browser renders and drives the pages served by your simulated web hosts, end-to-end, with no external server to start and no free port to allocate.

The browser is exposed as an ordinary Playwright `IPage` (via `browser.Page`), so everything you already know from Playwright works unchanged (`GotoAsync`, locators, `ClickAsync`, `ContentAsync`, screenshots).

Chromium is downloaded automatically the first time a test runs.

# How to use

Add the package to your test project (it pulls in `Microsoft.Playwright`):

```
dotnet add package SnowBank.Testing.Framework.Playwright
```

Then, in a `DistributedTest`, put a web host and a browser on the same virtual LAN and drive the browser with the standard Playwright API:

```c#
public class MyBrowserFacts : DistributedTest
{
    [Test]
    public async Task Browser_Renders_A_Page_Served_On_The_Virtual_Network()
    {
        var context = await MakeItSo(env => env.AddSimpleLan(lan =>
        {
            // a simulated web host serving a page
            lan.WithMinimalWebHost("WEB", host =>
            {
                host.ConfigureApplication(app =>
                    app.MapGet("/", () => Results.Content("<html><body>hello</body></html>", "text/html")));
            });

            // a real Chromium on the same virtual network
            lan.WithPlaywrightBrowser("BROWSER");
        }));

        var web = context.GetWebHost("WEB");
        var browser = context.GetPlaywrightBrowser("BROWSER");

        // `browser.Page` is a standard Playwright IPage; the request is routed over the virtual network
        var response = await browser.Page.GotoAsync(web.GetUri("/").ToString(), new() { WaitUntil = WaitUntilState.Load });
        Assert.That(response!.Ok, Is.True);

        var body = await browser.Page.ContentAsync();
        Assert.That(body, Does.Contain("hello"));
    }
}
```

## Options

`WithPlaywrightBrowser(id, configure)` exposes a few options through its builder:

- **`WithVirtualClock()`**: run the page under a virtual clock, advanced explicitly by the test, for deterministic time-dependent UI.
- **`WithRemoteDebugging(port)`**: expose the browser's Chrome DevTools Protocol (CDP) endpoint on a real loopback port, so an external controller (an inspector, or an agent-driven Playwright client) can attach with `connectOverCDP` and co-drive the same browser while the component keeps owning all virtual routing.
- **`WithBrowserOptions(o => ...)` / `WithContextOptions(o => ...)`**: tweak the Chromium launch and browser-context options (viewport, UserAgent, ...) on top of the component's defaults, without replacing the whole options object.
- **`WithInitScript(js)`**: inject a context-level init script, evaluated before the first page, for application-specific instrumentation (runs after the package's own scripts).
- **`WithConsoleFormatter(msg => ...)`**: reformat or drop JS-console lines routed to the test journal (return `null` to drop a line); the default formatting applies when this is not set.
- **`WithSnapshots(o => ...)`**: capture full-page screenshots via `browser.Snapshots.CaptureAsync(page, "label", ct)` into the per-test output directory, plus an `index.html` contact sheet written at teardown.
- **`ConfigureServices` / `ConfigureApplication`** and **`OnStartup` / `OnShutdown`**: the usual host-configuration and lifecycle hooks.

The `PlaywrightPageExtensions` helpers add convenience methods on top of the raw `IPage`, for example `WaitForPageReadyAsync` (wait until a page has settled: DOM ready, network quiet, and optionally an application-readiness predicate you pass).

> **Note:** these tests drive a real Chromium, so they are heavier than pure unit tests and are usually marked `[Explicit]`. Chromium is installed automatically on first run.
