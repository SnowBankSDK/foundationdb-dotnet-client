---
name: snowbank-distributed-testing
description: How to write, run, and especially DIAGNOSE multi-node integration tests built on the SnowBank distributed-test framework (SnowBank.Testing.Framework / SnowBank.Testing.Common — a general-purpose harness, NOT FoundationDB-specific). Covers DistributedTest/MakeItSo + virtual hosts (AddSimpleLan/WithMinimalWebHost), the unified in-memory Timeline journal that every test prints (its column format and kind/level vocabulary), and the controls for cranking diagnostic detail when a test regresses — per-host WithLogLevel, SetTimelineLogLevel, the always-on HTTP packet capture, and the RegisterTimelineEvent extension point that lets a library surface its own tagged ILogger events as a journal kind. Use whenever you write or run a DistributedTest, read or interpret the "TEST JOURNAL" block in test output, need MORE logging to troubleshoot a flaky/failing distributed test (HTTP packets, wire/protocol traces, fdb traces), or want a library's diagnostics to show up in the journal. For a specific layer's own test probes (e.g. a sync layer's debugger probes, chaos/fuzz hooks), see that layer's testing skill in the consuming repo.
---

# SnowBank Distributed Testing & the Unified Test Journal

`SnowBank.Testing.Framework` is a **general-purpose** harness for multi-node integration tests: it spins up several in-process "virtual hosts" on a **simulated network** (no real sockets, no Docker), each with its own isolated DI container and ASP.NET host, so you can test how nodes talk to each other deterministically. It is **not** FoundationDB-specific — anything that runs on a `WebApplication` can be a host.

Its single most important diagnostic output is the **unified Timeline journal**: one chronologically-ordered, correlation-tagged event stream merging every host's logs, HTTP packets, and any library-registered traces. When a distributed test misbehaves, the journal is where you look first — and this skill is mostly about reading it and cranking its detail.

This skill is layer-agnostic. A specific layer's own probes (e.g. a state-synchronization layer's debugger surface, a BUGGIFY apply-barrier, replay/chaos fuzzing) live in **that layer's testing skill in the consuming repo**, not here.

## Version & compatibility

This skill is versioned **with the code**: it lives in the same repo (`foundationdb-dotnet-client`) as `SnowBank.Testing.Framework` / `FoundationDB.Client`, and every NuGet release is a git **tag whose name is the package version** (`7.4.1`, `7.4.0`, `7.3.2`, …). This copy tracks the repo **tip** — the current API, which may be ahead of any released package.

If your project pins an **older** package, read the version-matched skill rather than this one — the concepts here are stable across versions, but individual members/signatures are not:

```bash
git show <your-package-version>:.claude/skills/snowbank-distributed-testing/SKILL.md
# e.g. git show 7.4.1:.claude/skills/snowbank-distributed-testing/SKILL.md
```

(or browse that path at the matching tag on GitHub). As a rule, **confirm a specific member exists in the assemblies you actually reference** before relying on it. For orientation, the whole surface described below — always-on HTTP packet capture, the three log-level knobs, `GetNetworkPackets(...)`, and `RegisterTimelineEvent(...)` — is already present as of **7.4.1**; if something here seems missing, you are most likely looking for it in the journal (e.g. packet *bodies*) instead of calling the API that exposes it.

## Running a distributed test

These projects use the **NUnit Microsoft.Testing.Platform (MTP) runner**. The test assembly is a self-contained executable, so the most direct way to run one is to build and then launch it, which also skips a rebuild in a tight iteration loop:

```bash
dotnet build <Solution>.slnx
dotnet artifacts/bin/<Project>/debug_net11.0/<Project>.dll \
  --filter "FullyQualifiedName~<NamePart>" \
  --output Detailed
```

`--output Detailed` gives per-assert output; the journal is printed regardless (see below). Use `--filter "FullyQualifiedName~Foo"` (not `--treenode-filter`).

`dotnet test` also works, **but only if the repository opts into the MTP mode of `dotnet test`** in its `global.json`:

```json
{ "test": { "runner": "Microsoft.Testing.Platform" } }
```

Without that entry, `dotnet test` on the .NET 10+ SDK takes the old VSTest path, which MTP v2 refuses outright with *"Testing with VSTest target is no longer supported by Microsoft.Testing.Platform"*. The `TestingPlatformDotnetTestSupport` MSBuild property was the v1-era bridge for that path and no longer has any effect on .NET 10+. The same `--filter` syntax works through `dotnet test`, with or without a `--` separator.

One caveat if your solution still has **`net472` legs**: MTP mode requires every project in the run to use the MTP runner, and a net472 leg is a VSTest project, so an unscoped `dotnet test` is rejected with *"All projects must use that test runner"* (exit code 8). Scope the run to a modern target (`-f net10.0`) and keep running the net472 leg with its own runner. Launching the assembly directly, as above, is unaffected either way.

### `SNOWBANK_TEST_LOG` — pick the output shape

Every event is available two ways: **live**, streamed per-event as it happens (nice for a human watching in an IDE), and **consolidated**, as the single end-of-test journal. Emitting both duplicates the output — fine interactively, noise in a captured CI/agent log. The `SNOWBANK_TEST_LOG` env var (resolved once per process) picks:

| Value | Live per-event stream | End-of-test journal | For |
|-------|:---:|:---:|-----|
| `stream` | ✅ | on **failure** only | interactive (ReSharper / VS / debugger) |
| `report` | ❌ | ✅ | CI consoles, **AI agents reading the file** |
| `both` | ✅ | ✅ | deep debugging |

- **Unset** → auto: an interactive runner (`testhost`/debugger, *not* TeamCity/`CI`) resolves to `stream`; everything else (CI, a plain `dotnet …dll` run, an AI agent) to `report`.
- **A failing test always emits the full journal**, in every mode — the post-mortem is never hidden.
- Orthogonally, the default ASP.NET console provider is cleared in test hosts, so the per-event view is the framework's own single-line format (`# T+t LEVEL @host [src] "msg"`), not a second two-line copy. In a real terminal (`!Console.IsOutputRedirected`) warnings/errors are ANSI-colored; captured/redirected output (VS, files, agents) stays plain.

```bash
SNOWBANK_TEST_LOG=report dotnet artifacts/bin/<Project>/debug_net11.0/<Project>.dll --output Detailed
```

## Writing one (the entry points)

A test derives from `DistributedTest` and builds an environment with `MakeItSo`:

```csharp
[TestFixture]
public class MyFacts : DistributedTest
{
    [Test]
    public async Task Two_Nodes_Talk()
    {
        var context = await MakeItSo(env => env.AddSimpleLan(lan =>
        {
            lan.WithMinimalWebHost("SERVER", host =>
            {
                host.ConfigureServices(b => { /* register services */ });
                host.ConfigureApplication(app => { /* map endpoints */ });
                host.OnStartup(async (SomeService svc) => { /* warm up */ });
            });

            lan.WithMinimalWebHost("CLIENT", host =>
            {
                host.ConfigureServices(b => { /* ... */ });
            });
        }));

        var server = context.GetWebHost("SERVER");
        // ... drive the test via server.GetRequiredService<T>(), assert outcomes ...
    }
}
```

- `MakeItSo(Action<IDistributedTestEnvironmentBuilder>)` — builds the topology, runs `Prepare`/`Init`/`Start` on every host, returns the live `DistributedTestContext`.
- `AddSimpleLan(...)` — a ready-made `192.168.1.0/24` LAN with `*.lan.simulated` DNS. (`AddLocation(...)` for custom topologies.)
- `WithMinimalWebHost(id, configure)` — one virtual host; `ConfigureServices` / `ConfigureApplication` / `OnStartup`. Each host has its OWN DI container (a restart builds a fresh one).
- `WithPlaywrightBrowser(id, configure)` (from the `SnowBank.Testing.Framework.Playwright` package): a real headless Chromium on the virtual network, driven via `browser.Page` (a standard Playwright `IPage`). Builder hooks: `WithVirtualClock`, `WithRemoteDebugging(port)`, `WithBrowserOptions` / `WithContextOptions` (tweak the launch and context options on top of the package defaults), `WithInitScript(js)`, `WithConsoleFormatter(msg => ...)` (reformat or drop JS-console lines), and `WithSnapshots(...)` (full-page PNGs plus an HTML contact sheet into the per-test output dir). `IPage.WaitForPageReadyAsync(ct, ..., readyPredicate)` waits for DOM plus network-quiet, plus an optional application-readiness predicate. These tests need Chromium (auto-installed on first run) and are usually `[Explicit]`.
- Use the injected **`IClock`** for time inside the simulated nodes (it can be a fake clock); use `context.RealClock` only for wall-clock measurements.

## The virtual network: the external network and egress safety

Every simulated host's outbound HTTP rides the virtual network. Three networks and their DNS conventions:

- `AddSimpleLan`: `192.168.1.0/24`, `*.lan.simulated`.
- `AddSimpleCloud`: a distinctive non-LAN block, `*.cloud.simulated`.
- `AddSimpleExternal`: `69.88.84.0/24` ("EXT" in ASCII), REAL names only. This is how you mock a third-party endpoint reachable by its real URI, so the system under test needs no config edit:

```csharp
env.AddSimpleExternal(ext => ext.WithMinimalWebHost("PARTNER", host =>
{
    host.Identity.Fqdn = "api.partner.com";                       // a real name, legal only on external
    host.ConfigureApplication(app => app.MapGet("/pay", () => "charged"));
}));
// a lan host now reaches it by its real URI, and PARTNER can webhook back into a lan host through its own DI client
```

Naming guard (`VirtualNetworkTopology.RegisterHost`): the `lan`/`cloud` networks carry only `.simulated` names, the `external` network only real names. A host name, alias, or `SetAlias` VIP that violates its network is rejected at registration.

The `.simulated` defense (always on). A name that reaches the resolver or the virtual HTTP handler and is neither a registered host, nor `.simulated`, nor an intended `Cut` is a REAL URI that leaked into the sandbox: it throws a loud, specific error and calls `Debugger.Break` under a debugger. An unregistered `.simulated` name keeps the friendly "you forgot to register" error. To get a QUIET simulated DNS failure on purpose (the sanctioned negative path, and the opt-out a test of the harness itself uses), cut the name at the resolution layer:

```csharp
context.Topology.Cut("api.*.partner.com", VirtualNetworkFault.NameResolution); // glob, topology-wide
context.Topology.Cut("*", VirtualNetworkFault.NameResolution);                 // disable the alarm broadly
```

Raw-client tripwire (`RawClientTripwire`, opt-in). A `new HttpClient()` with no DI, or a package that sets its own primary handler, bypasses the map and opens a REAL socket. `RawClientTripwire` subscribes to `System.Net.Http.HttpRequestOut.Start`, which fires ONLY for real-socket requests, so a fully virtual test stays silent and any event is a genuine escape. It is opt-in per test and, because the listener is process-wide, must run serialized; loopback is allowlisted. Roll out warn-first, then flip to fail:

```csharp
using var tripwire = new RawClientTripwire(RawClientTripwireAction.Fail);
// ... run the test ...
tripwire.Verify(); // throws, naming each escaping URI and its callstack
```

White-list an accepted real endpoint with `AllowHost("telemetry.vendor.com")` (steadier) or `AllowCallstack("*SomeVendor.Telemetry.*")` (best-effort: a cold first socket loses the caller frame, warm requests keep it). A white-listed client really opens a socket, so that test depends on the real network and belongs in an Explicit/Ignore lane.

## The unified Timeline journal

Every test prints its journal at teardown (`DistributedTest.OnAfterEachTest` → `Timeline.DumpReport` → `context.LogOutput`), bracketed by grep-able markers so it's findable even under parallel runs:

```
===== TEST JOURNAL START test=<fully-qualified-name> =====
# columns: <gutter> #seq | T+elapsed | level | kind | source | detail   ::   ...legend...
  #0021 | T+  0.033 | info  | L | SERVER   | [MyService] "started"
!!#0042 | T+  0.117 | ERROR | L | CLIENT   | [Pump] "boom": [InvalidOperationException] ...
  #0043 | T+  0.118 | ..... | H | CLIENT   | 200 GET https://server.lan.simulated/x (... => json) [pkt-7]
===== TEST JOURNAL END test=<fully-qualified-name> =====
```

Each row is: **`<gutter> #seq | T+elapsed | level | kind | source | detail [ (duration ms) ] [ <correlationId> ]`**

- **gutter** — `!!` for error/fatal, `! ` for warning, blank otherwise (so loud events stand out while scrolling).
- **#seq** — monotonic per-test sequence number; the tiebreaker for events sharing a tick.
- **T+elapsed** — seconds from test start, on the **real/wall clock** (ordering uses a high-res monotonic tick, immune to a fake `IClock`). Span-like events are placed at completion and show `(N ms)`.
- **level** — `FATAL` / `ERROR` / `WARN ` / `info ` / `-----` (debug) / `.....` (trace); blank for structural events that carry no log level.
- **kind** — a single letter (see vocabulary below).
- **source** — the host id that emitted it (`SERVER`, `CLIENT`, …).
- **detail** — the message/label; `<...>` appends a correlation id when present.

### Kind vocabulary

| Letter | Category | Produced by |
|---|---|---|
| `L` | `LOG` | regular `ILogger` lines (gated by `MinimumTimelineLogLevel`, default `Information`) |
| `H` | `HTTP` | the always-on HTTP **packet capture** (one summary line per request) |
| `T` | `TEST` / `TML` | framework lifecycle + harness events (host start/stop, waits) |
| `M` | `MSG` | a **library-registered** event kind (e.g. protocol wire messages) |
| `F` | `FDB` | a **library-registered** event kind (e.g. fdb transaction summaries) |
| `X` | `PROBE` / `HOOK` | a **library-registered** diagnostic probe kind |
| other | any | first letter of the category, uppercased |

`L`, `H`, and `T` are built into the framework. `M` / `F` / `X` (and any other) only appear if a **library registered** the producing event — see "Surfacing your library's events" below; for the concrete rules a given layer uses, read that layer's testing skill.

## Diagnosing a regression: cranking the detail

There are **three independent knobs**. Know which one you need:

**1. `host.WithLogLevel(LogLevel level)`** — sets what a host's logger **emits at all** (`logBuilder.SetMinimumLevel`). Trace-tagged diagnostics (protocol wire messages, fdb summaries, etc.) are emitted only at `Trace`, so to even produce them:

```csharp
lan.WithMinimalWebHost("CLIENT", host =>
{
    host.WithLogLevel(LogLevel.Trace); // emit everything this host can, incl. Trace-level library traces
    ...
});
```

**2. `component.SetTimelineLogLevel(LogLevel level)`** — sets which **regular `L` log lines** enter the journal (default `Information`; per-component; can be called mid-test). Lower it to capture more, raise it to cut spam on a test that intentionally logs errors:

```csharp
context.GetWebHost("CLIENT").SetTimelineLogLevel(LogLevel.Debug);
```

> Knob 1 vs 2: `WithLogLevel` controls **emission**; `SetTimelineLogLevel` controls **journal inclusion of `L` lines**. To see Trace-level `L` logs in the journal you need BOTH (`WithLogLevel(Trace)` to emit + `SetTimelineLogLevel(Trace)` to admit). **Library-registered events bypass `SetTimelineLogLevel`** — they are captured whenever emitted (so for those, only knob 1 matters).

**3. HTTP packets are captured by default** (`PacketCapture:Enabled=true`). Each request appears as a one-line `H` summary in the journal (`status method uri (contentType => contentType) [packetId]`), interleaved with everything else, so you can see exactly when a call happened relative to the logs. The **full request/response bodies** are kept in `context.GetNetworkPackets(...)` and dumped as a separate block to `LogOutputError` **only when the test fails** (addressable by the `[packetId]` shown in the journal line). To inspect packets programmatically:

```csharp
foreach (var p in context.GetNetworkPackets(p => p.Metadata.Uri.Contains("/x"))) { /* ... */ }
```

**What is captured (and what is not).** Capture rides the pooled handler chain as an in-chain handler on *every* registered policy bundle, so it observes any consumer of that chain — not only `BetterHttpClient` send-extension calls, but also a **bare `HttpMessageHandler`** drawn from `IHttpMessageHandlerFactory.CreateHandler(name)`, the shape gRPC channels and SignalR connections use. Their traffic therefore shows up as `H` lines too. Only the deliberately-raw transport (`INetworkMap.CreateTransportHandler`, taken where a path opts out of the pipeline on purpose) stays uncaptured. A long-lived **streaming** response — `application/grpc*` (a gRPC duplex body) or `text/event-stream` (Server-Sent Events) — is captured **at headers only**: request metadata plus response status/headers, the body never mirrored and flagged `Streaming` on the packet, so the live stream is never interposed on or torn. Every **finite** response keeps its exact, complete body capture.

### Reading order, fast

- Find the failure: scan the **`!!` gutter** for the first `ERROR`/`FATAL`.
- Follow causality across hosts via the **`<correlationId>`** suffix and the monotonic `#seq`.
- Cross-reference an `L` log against the `H` packet just before/after it (same `T+elapsed` neighbourhood) to see whether a call was sent, received, or never happened.
- If the relevant traces are missing, you probably need knob 1 (`WithLogLevel(Trace)`) on the host that should have produced them.

## Surfacing your library's own events in the journal

The framework has **no knowledge of any specific library**. If your library emits trace events via `ILogger` tagged with a well-known `EventId` name, register a rule so they appear as their own journal kind — captured whenever emitted (gated only by the logger level, i.e. knob 1), independent of `SetTimelineLogLevel`.

The producing code logs with a named `EventId`:

```csharp
// in your library's production code (no dependency on the test framework)
this.Logger.LogTrace(new EventId(0, "WireOut"), "{Summary}", FormatSummary(msg));
```

A **test base class** for your library registers the mapping once, via the `OnConfigureEnvironment` hook (so individual tests need no boilerplate):

```csharp
public abstract class MyLayerTest : DistributedTest
{
    protected override void OnConfigureEnvironment(IDistributedTestEnvironmentBuilder builder)
    {
        base.OnConfigureEnvironment(builder);
        // EventName -> journal kind (+ optional label formatter)
        builder.RegisterTimelineEvent("WireOut", "MSG", static m => ">> " + (m ?? ""));
        builder.RegisterTimelineEvent("WireIn",  "MSG", static m => "<< " + (m ?? ""));
        builder.RegisterTimelineEvent("FdbTxn",  "FDB");
    }
}
```

The surface (in `SnowBank.Testing.Framework`):

- `IDistributedTestEnvironmentBuilder.RegisterTimelineEvent(string eventName, string category, Func<string?,string>? formatLabel = null)` — map an `EventId` name to a journal kind. `category` is the kind code (`"MSG"` → `M`, `"FDB"` → `F`, etc.; rendered via the kind vocabulary above). `formatLabel` turns the log message into the journal label (defaults to the message).
- `DistributedTest.OnConfigureEnvironment(IDistributedTestEnvironmentBuilder)` — `virtual` hook called by `MakeItSo` after the test's own `configure` callback; override it in a library test base to register rules for all its tests.
- `TimelineEventRule { string Category; Func<string?,string>? FormatLabel }` and `IDistributedTestContext.TimelineEventRules` — the registry the log handler consults.

Keep these registrations in the **consuming repo's** test code (next to the producing library), never hard-coded in this generic framework.

## Keeping green runs quiet: expected contract failures

The harness installs a global `Contract.ContractFailed` interceptor: any contract violation prints a loud
`#!# ContractFailed: ...` line (with a cleaned-up stack trace) into the test output, and breaks into the debugger when
one is attached. That is deliberate — an UNexpected contract failure is almost always a bug worth loud reporting.

But a test that *deliberately* violates a precondition (asserting that an API rejects bad arguments) would echo that
same loud line into every green run — noise that trains readers (and agents grepping the output for failures) to ignore
contract failures entirely. Wrap ONLY the offending assertions in `SimpleTest.ExpectContractFailure()`:

```csharp
using (ExpectContractFailure())
{ // these deliberately violate preconditions: keep the contract interceptor quiet
	Assert.That(() => builder.Get(default!), Throws.InstanceOf<ArgumentNullException>());
	Assert.That(() => builder.GetDescriptor(-1), Throws.InstanceOf<ArgumentOutOfRangeException>());
}
```

The scope is `AsyncLocal`-based: it flows into the assertion lambdas, nests correctly, and does not leak into tests
running in parallel. Rule of thumb: **if a green run of your suite prints `#!# ContractFailed`, a negative test is
missing this scope** — grep the output for `#!#` after adding precondition tests.

## Gotchas / current limitations

- **`TimelineRenderOptions` (`ShowDetails` / `ShowStartup`) is wired but unused** — `DumpReport` always renders with `Default`. Don't rely on it yet (`//TODO` in `Timeline.cs`).
- **`F` and `M` are not built in** — they only appear if a layer registered the producing `EventId`. There is no FDB-trace capture in the framework itself; a layer wires raw fdb traces in its own test setup (e.g. via the fdb client's `SetDefaultLogHandler`) and/or registers an `EventId`-based summary.
- **HTTP capture is always on** — every test's journal carries `H` lines (for every pooled bundle, bare gRPC/SignalR handlers included); heavy finite bodies are dumped only on failure, and streaming bodies (`application/grpc*` / `text/event-stream`) are captured at headers only.
- The journal is **in-memory only** (no temp files); it is flushed once, to the per-test NUnit output, at teardown.

## See also

- `crystaljson` — the JSON stack (`SnowBank.Data.Json`) that messages/documents serialize through.
- `foundationdb-aspire` / `foundationdb-transactions` — for tests that use a real or fake FoundationDB cluster.
- The consuming repo's **layer testing skill** (e.g. `cloudlayer-*-testing`) — for that layer's own probes, debugger, and the concrete `RegisterTimelineEvent` rules it uses.
