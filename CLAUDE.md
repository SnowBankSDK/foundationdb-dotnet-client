# CLAUDE.md

Guidance for AI agents (and humans) working **in this repository**. If you are instead trying to *use* this library in your own application, read the skills under [`.claude/skills/`](.claude/skills/), they are written for consumers and are the canonical guide to the key/value and transaction APIs.

## What this repository is

A C#/.NET binding for [FoundationDB](https://www.foundationdb.org/) (a distributed, ordered key/value store), plus the general-purpose libraries it is built on. The main solution is **`FoundationDB.Client.slnx`** (the new XML `.slnx` format). Current version: see [`Common/VersionInfo.props`](Common/VersionInfo.props) (`7.4.x`).

The repo holds **two distinct product families**:

- **`SnowBank.*`**, general-purpose foundation, *not* FoundationDB-specific. `SnowBank.Core` is the bedrock (Slices, the Tuple encoding, the CrystalJson stack, collections, async LINQ, UUIDs). Also `SnowBank.Shell`, `SnowBank.Serialization.Json.CodeGen` (a Roslyn source generator), `SnowBank.Networking.*`, `SnowBank.Testing.*`.
- **`FoundationDB.*`**, the actual binding. `FoundationDB.Client` is the core (native interop, transactions, keys/values, subspaces, the Directory layer, tenants, DI). `FoundationDB.Layers.Common` holds demo layers (Map, Index, Vector, Queue, Counter, Blob, …). Aspire, FakeDb, Testing, BindingTester, and the `Fdb*` tools sit around it.

### Dependency direction (do not invert)

```
SnowBank.Core  ◄─  FoundationDB.Client  ◄─  FoundationDB.Layers.Common  ◄─  Layers.Experimental, Linq.Providers
     ▲                                                                  
SnowBank.Shell, SnowBank.Networking.*, SnowBank.Testing.*, SnowBank.Serialization.Json.CodeGen
```

`SnowBank.Core` has **no** project references and must never depend on FoundationDB. `FoundationDB.Client` depends only on `SnowBank.Core`. Tooling/tests reference downward only.

## Build & test

```bash
dotnet build FoundationDB.Client.slnx                  # DEBUG build of everything
dotnet test <Project>.csproj -f net11.0                # ONE suite; always scope the TFM, see "Tests"
```

- **SDK**: pinned in [`global.json`](global.json) to a **.NET 11 preview** SDK (`rollForward: latestMinor`). `LangVersion` is `preview`.
- **Target frameworks**: libraries multi-target `net11.0;net10.0;net8.0` (see [`Directory.Build.props`](Directory.Build.props)). Each project builds once per target. Several projects **also build a `netstandard2.0` "lite" variant**, see [The netstandard2.0 / net472 "lite" targets](#the-netstandard20--net472-lite-targets) below; keeping it green is part of any change to those projects.
- **Build output** goes to `artifacts/` (`ArtifactsPath`), not per-project `bin/obj`.
- **Central package management**: all versions live in [`Directory.Packages.props`](Directory.Packages.props). Add/bump packages there, not in `.csproj` files. **Version policy**: this SDK is a BOM for downstream applications, each pin should be the **latest version that still supports its TFM bucket** (the .NET platform wave is multi-TFM, so net8/net9/net10 usually share one version; ASP.NET Core packages are single-TFM and stay capped per bucket). Deliberate exception: `Microsoft.CodeAnalysis.*` stays low, because it sets the minimum Roslyn/SDK for consumers of the `SnowBank.Serialization.Json.CodeGen` source generator.
- **As a submodule**: a parent repo can override targets via `CoreSdkVersions` (or the finer `CoreSdkRuntimeVersions` / `CoreSdkToolsVersions` / `CloudSdkRuntimeVersions`), and disable the lite targets via `CoreSdkNetStandardEnabled=false`, in its own `Directory.Build.props`. The override import is gated on a `.git` *file* check; bypass with `FDB_BUILD_PROPS_OVERRIDE=1`.
- **Standalone (isolated) build**: to build, test, or pack THIS repo with its own COMPLETE target set (net8/net10/net11 + netstandard2.0 + the net472 validation targets) while it sits under a target-trimming parent, force `CORESDK_STANDALONE_BUILD=true`, which makes `Directory.Build.props` ignore the parent import. **Pass it as an MSBuild `-p:` property, NEVER as an `$env:`/`export`**: a persistent env var leaks into the shell and silently forces every later `dotnet` command into a standalone build (this bit us once; the pack/build scripts were fixed to pass `-p:`). Use [`scripts/build.ps1`](scripts/build.ps1) / [`scripts/build.sh`](scripts/build.sh) for an isolated `restore`+`clean`+`build`, and [`scripts/pack.ps1`](scripts/pack.ps1) / [`scripts/pack.sh`](scripts/pack.sh) for packaging (packing MUST be standalone, or the `.nupkg` ships an incomplete target set).
- **Shared-restore gotcha**: `restore` writes `artifacts/obj/**/project.assets.json` for whatever mode it ran in, and that state is shared. `restore`, `build`, and `clean` MUST use the SAME mode; mixing them fails with `NETSDK1005` ("assets file doesn't have a target for &lt;TFM&gt;"). After a standalone build, the parent must re-run `dotnet restore` to reset the submodule's assets to its trimmed set (and vice-versa); to switch modes cleanly, re-run `dotnet restore` in the target mode (or wipe `artifacts/obj`).

### Tests

- **NUnit 4**, and the runner **must be 64-bit** (the native client is 64-bit only).
- ⚠️ **`global.json` opts into the MTP mode of `dotnet test`** (`"test": { "runner": "Microsoft.Testing.Platform" }`) and that opt-in is load-bearing: these projects use the NUnit MTP runner, and **MTP v2 refuses to run under the old VSTest bridge on the .NET 10+ SDK**, failing with *"Testing with VSTest target is no longer supported by Microsoft.Testing.Platform"*. Every test project declares `EnableNUnitRunner=true` in its OWN `.csproj`, next to `IsTestProject`, `IsPackable=false` and `RunAnalyzersDuringBuild=false`, so all targets use MTP uniformly. This block is deliberately per-project and NOT shared from `Directory.Build.props`: a shared block can only be conditioned on the project name, and a test project is not required to be named `*.Tests`. **A new test project must carry the block**, or it falls back to VSTest and exits 0 having run nothing, which is indistinguishable from a passing suite.
- **Scoping `dotnet test` to a single project and TFM** (`dotnet test <Project>.csproj -f <tfm>`) is the standard invocation. A solution-wide `dotnet test FoundationDB.Client.slnx` is not supported (MTP requires every project in the run to use the same runner, and the solution includes non-test projects).
- **Running the assembly directly also works and skips the build**, which is faster in a tight loop: `dotnet artifacts/bin/<Project>/debug_net11.0/<Project>.dll`, filtering with `--filter "FullyQualifiedName~<NamePart>"` (not `--treenode-filter`) and `--output Detailed` for per-assert output. The same `--filter` works through `dotnet test`, with or without the `--` separator.
- ⚠️ **Exclude the benchmark categories from normal suite runs**: append `--filter "TestCategory!=LongRunning&TestCategory!=Benchmark"`. The benchmark fixtures are correctly categorised and interactive runners filter them by default, an agent running the assembly UNFILTERED is the only consumer that pays for them (measured: `SnowBank.Core.Tests` net10 5m19s → 4s, net472 6m20s → 8s, keeping 98.4% of tests). Two exceptions: benchmarks stay runnable on purpose via `--filter "TestCategory=Benchmark"`, and a run whose *subject* needs GC/allocation pressure (e.g. the SliceReader GC-safety detector) runs unfiltered, a filtered run is structurally blind to that class.
- `SnowBank.*.Tests` are pure and need no database.
- **`FoundationDB.Tests` requires a running local FoundationDB cluster** and the native `fdb_c` library (`libfdb_c.dylib` on macOS, `libfdb_c.so` on Linux, `fdb_c.dll` on Windows). `FoundationDB.Client.Native` redistributes these.
  - ⚠️ Tests write to a dedicated subspace but **can corrupt data**, only point them at a throwaway local cluster.
- `FoundationDB.FakeDb` provides an in-memory fake for tests that don't want a real cluster.
- Test classes use the `*Facts` naming convention (e.g. `FdbKeyFacts`, `TuPackFacts`).

### Test project layout (which tests go where)

Three test projects, split by what they exercise:

- **`FoundationDB.FakeDb.Tests`**, the FakeDb emulator's OWN tests only: conformance against real FDB semantics, internals, and the differential scenario harness. Docker-free. (FdbLite's emulator tests will land here too.)
- **`FoundationDB.Tests`**, the client binding: transactions, keys, subspaces, values, tenants, error handling, the native handler, plus "don't know where else to put it" tests. Docker-backed (Testcontainers). The Directory-layer tests (`DirectoryFacts`, `FdbPathFacts`) live here and are SPECIAL: they open the database manually at the root, because a test *of* the Directory layer cannot use the Directory-layer-based partition helper (that would test the layer through itself).
- **`FoundationDB.Layers.Tests`**, the home for every LAYER suite (`Layers.Common`, `Layers.Experimental`). Each suite is written ONCE and runs against BOTH the in-memory FakeDb (fast) and a real cluster (validation).

**Dual-backend pattern** (see `FullText/FdbTextIndexFacts.cs`, and `SmokeConformanceFacts` for the original): a layer suite is an `abstract XxxFacts : FdbTest` whose bodies open an isolated partition with `OpenTestPartitionAsync()` and clear it with `CleanLocation(db, location)`, backend-agnostic. Two thin concrete fixtures pick the backend:

```csharp
public abstract class FooFacts : FdbTest { /* the tests, via OpenTestPartitionAsync() / CleanLocation() */ }

[TestFixture] // FAST default: in-memory FakeDb, no Docker, no native client
public sealed class FooFakeDbFacts : FooFacts
{
    private FakeDbTestBackend Backend { get; } = new();
    protected override bool UseRealServer => false;
    [TearDown] public void ResetBackend() => this.Backend.Reset();
    protected override Task<IFdbDatabase> OpenTestDatabaseAsync(bool readOnly = false) => this.Backend.OpenAsync(FdbPath.Root, readOnly);
    protected override Task<IFdbDatabase> OpenTestPartitionAsync(string? testMethod = null) => this.Backend.OpenAsync(GetTestPartitionPath(testMethod));
}

[TestFixture, Explicit("Requires a local Docker daemon"), Category("RealCluster")]
public sealed class FooRealClusterFacts : FooFacts { } // inherits the real-cluster FdbTest behavior
```

**Workflow**: iterate against the `*FakeDbFacts` fixtures (Docker-free, instant); then run the `*RealClusterFacts` fixtures (opt-in via the `RealCluster` category or the Unit Test Sessions UI) to validate against a real cluster. Real-cluster runs require Docker and the native `fdb_c` client (one-time per worktree: `FoundationDB.Client.Native/DownloadBinaries.ps1` on Windows, or `DownloadBinaries.sh` on macOS/Linux, which needs `curl` + `python3`; pass `--rid osx-arm64` to fetch only the current platform's binaries). The same suites double as a ready-made realistic test bed for a future FdbLite backend (a third `*FdbLiteFacts` subclass).

### The netstandard2.0 / net472 "lite" targets

`SnowBank.Core`, `FoundationDB.Client`, `FoundationDB.Client.Native`, and `FoundationDB.Layers.Common` **also build for `netstandard2.0`** (gated by `CoreSdkNetStandardEnabled`, default `true`). This is the client stack, and only the client stack: `FoundationDB.FakeDb` is the in-memory configuration of the storage engine and is modern-targets-only, as are the two suites that reference it (`FoundationDB.FakeDb.Tests`, `FoundationDB.Layers.Tests`). This is a deliberate stopgap for applications migrating from .NET Framework 4.7.2 to modern .NET: their legacy `main` branch can already consume the modern APIs (`Contract.NotNull`, CrystalJson, `Slice`, `subspace.Key(...)`), which reduces merge pain with their modernized `vnext` branch. The lite build must **run** on the net472 CLR (including the native `fdb_c` interop), not just compile, and it must produce **byte-identical keys/values**: what is written to the database must never depend on the TFM.

**Any change to these projects must keep the `netstandard2.0` target building** (`dotnet build <proj>.csproj -f netstandard2.0`). When code needs something the old BCL lacks:

1. **Missing BCL API with many call sites** → prefer a polyfill in [`SnowBank.Core/Polyfills/`](SnowBank.Core/Polyfills/) so call sites compile unchanged. Public extension polyfills live in the `SnowBank.Compat` namespace (imported via a guarded `global using` in each project's `GlobalUsings.cs`; this includes C# 14 static extension members like `string.Create(provider, $"...")` and `Span<char>.TryWrite($"...")`). Statics that can't be extensions are internal `*Compat` classes redirected per-file: `using CollectionsMarshal = SnowBank.Compat.CollectionsMarshalCompat;` under `#if NETSTANDARD2_0`.
2. **Point fallback** → `#if`/`#else`/`#endif` with the original implementation FIRST under `#if NETX_0_OR_GREATER` (X = the version that introduced the feature/API), and the fallback in `#else` with a comment on the next line stating what is missing and the cost ("X is not available, doing Y instead (which allocates)").
3. **Needs runtime support, cannot be polyfilled** (default interface members, `static abstract` interface members, covariant return types, ref fields, `allows ref struct`) → guard the member out, or exclude the whole feature from the lite build (e.g. `FdbValue<TValue, TEncoder>` and `FdbValueCodec.FixedSize`, the FQL `Query/` engine, the JSON source-gen proxies).
4. **Portable `Span<T>` gotchas**: on netstandard2.0, `Span<T>` cannot wrap raw pointers/refs of types containing references *or pointers* (e.g. `Span<FdbKeyValue>`), and `MemoryMarshal.CreateSpan` shims only support unmanaged types, use direct pointer indexing or a temporary array instead.

**Validation**: `net472` test targets exist on `SnowBank.Core.Tests` and `FoundationDB.Tests` (they consume the netstandard2.0 builds and run on the real .NET Framework CLR). Test files covering excluded features are guarded with `#if !NETFRAMEWORK` (or `<Compile Remove>` in the csproj for whole folders). Run them with `dotnet test <Project>.csproj -f net472 --no-build` (all TFMs use MTP uniformly now). On net472 the fdb test container is driven through the docker CLI (Docker.DotNet cannot run on the netfx CLR). On every TFM, a freshly created test container self-provisions its database on first start (`Fdb.Provisioning.EnsureDatabaseConfiguredAsync`, called from `FdbServerTestContainer.StartContainer`); no manual `fdbcli` step is needed.

Known accepted netfx differences (do not "fix" by changing shared code): doubles may format with 17 digits instead of shortest-roundtrip (`FastDtoa` is excluded), and `IVarTuple` ↔ `ValueTuple`/`ITuple` interop is unavailable (`System.Runtime.CompilerServices.ITuple` is not visible to netstandard2.0).

## Releasing

The published NuGet packages are cut from [`Common/VersionInfo.props`](Common/VersionInfo.props). The reference **sample and sandbox projects are not part of the release**: they pin *published* `FoundationDB.*` package versions (so a reader can `restore` them like their own app), which means they legitimately lag the in-development version. Fold this sweep into the pre-tag routine:

- **Before the tag**, write [`Documentation/releases/<N>.md`](Documentation/releases/) from the commit log (which follows the `Area: summary` house form: `git log <prev-tag>..HEAD --format='- %s'`, grouped by area prefix, then curated into prose, never the raw log). It is the single release-notes and upgrade document: every change from the previous STABLE version (not the previous rc), ordered by what applications use, each behavior change inlining what to do about it, and a final Breaking changes section for the rest. There is no separate migration guide. Follow [`Documentation/writing-style.md`](Documentation/writing-style.md), and add a `releases/<N>.md` entry to [`Documentation/toc.yml`](Documentation/toc.yml). The GitHub release body stays slim: highlights and the breaking-change list, pointing to `releases/<N>.md` for the full detail.
- **Before packing**, bump the pinned `FoundationDB.*` versions in the standalone samples ([`samples/getting-started/`](samples/getting-started/)) and any sandbox projects to the version being released. This puts the correct pins in the commit that gets packed and tagged, so the tag (which targets the packed commit, for SourceLink) also carries samples that point at the release. The bump is a pin, not a verification: the samples do not restore until the packages are live, which is expected. **After** upload, when the packages are live, `restore` + build the samples to confirm they work against the shipping packages. Do NOT defer the pin bump to after upload: the pin then lands in a commit after the packed one, and the tag ships stale sample pins (7.4.4 did this, tag `292796a7` pins the samples to 7.4.3).

## Coding conventions

Style is enforced by [`.editorconfig`](.editorconfig) and the `.DotSettings` files. Match the surrounding code; notable points:

- **Tabs, not spaces.** (This is deliberate and non-negotiable per the README.)
- **Block-scoped namespaces** (`namespace Foo { … }`), not file-scoped. Every file opens with the BSD-3-Clause copyright header `#region`.
- `Nullable` is **enabled**; `ImplicitUsings` is enabled (shared usings live in each project's `GlobalUsings.cs`).
- Public API is documented with XML doc comments (`///`), keep them accurate and add `<remarks>`/`<example>` for non-obvious behavior, as the existing code does heavily.
- **House writing style** (design docs, notes, documentation, code comments, commit messages). Principles: one idea per sentence, prefer short; active voice with a named actor; concrete words, numbers over adjectives; the same word for the same thing (no synonym rotation); every "this"/"it"/"that" has a clear referent; noun stacks capped at three, broken with prepositions; positive form, no double negatives; no trailing "-ing" pile-ups. Banned everywhere: em-dashes, shouted CAPS mid-sentence, emoji, and the `no-slop` word list (delve, leverage, robust, seamless, ...). In comments only, a beacon at the start is a review marker kept verbatim: `REVIEW`, `BUGBUG`, `HACKHACK`, `PERF`, `OPTIMIZE`, `NOTE`. Comments explain the code; they do not point at design, spike, or campaign documents (state the fact or rationale inline). This is a blacklist plus principles: no approved-word dictionary, no length audit, no sign-off process. When in doubt, write the plainest true sentence.
- Allocation-consciousness is a core value: prefer `Slice`/`ReadOnlySpan<byte>`, pooled buffers, and `struct` keys/values over `byte[]`. Many hot types are `readonly struct` implementing span-based interfaces (`ISpanEncodable`).
- **Guard clauses and assertions use the `Contract.*` family** (`SnowBank.Diagnostics.Contracts`, a global using in these projects), **not** raw `throw new ArgumentNullException(...)`, `ArgumentNullException.ThrowIfNull(...)`, `ArgumentOutOfRangeException.ThrowIf*`, or `System.Diagnostics.Debug.Assert(...)`. Always-on preconditions: `Contract.NotNull(x)`, `Contract.NotNullOrEmpty(...)`, `Contract.Positive(n)`, `Contract.Requires(cond)`. Debug-only invariants: `Contract.Debug.Requires(...)` / `Contract.Debug.Assert(...)` (and `Paranoid.*` for the hottest paths). They carry `[CallerArgumentExpression]` param names and integrate with the test harness's contract-failure interceptor.
- Don't break public surface casually. Retire APIs with `[Obsolete]` (the codebase uses `error: true` for hard-removed ones) rather than deleting outright.

## Working on the key/value API or layers

The single most important thing to get right (and the most common source of incorrect "vibe-coded" usage) is **how keys are encoded and how a custom Layer is structured**. The rules:

- Keys are built with `subspace.Key(item1, …)` and friends, returning **lazy strongly-typed key structs** (`FdbTupleKey<…>`). They are rendered to bytes only when handed to the transaction (`tr.GetAsync(key)`, `tr.Set(key, …)`). Do **not** eagerly call `.ToSlice()` and pass bytes around.
- A Layer is a thin wrapper over an `ISubspaceLocation`; it implements `IFdbLayer<TState>`, and `Resolve(tr)` returns a per-transaction `State` holding the resolved `IKeySubspace`. The state must **never** escape the transaction.

Full guidance, worked examples, and the list of reference layers to imitate are in the skill **[`.claude/skills/foundationdb-keys-and-layers/SKILL.md`](.claude/skills/foundationdb-keys-and-layers/SKILL.md)**. Transaction/retry semantics are in **[`.claude/skills/foundationdb-transactions/SKILL.md`](.claude/skills/foundationdb-transactions/SKILL.md)**. For sophisticated/distributed layers (cluster internals, read-batching/latency, change feeds, version-as-clock leases, retention and fencing), see **[`.claude/skills/foundationdb-advanced-layers/SKILL.md`](.claude/skills/foundationdb-advanced-layers/SKILL.md)**. For the `Slice` type and its companions (`SliceReader`/`SliceWriter`/`SliceOwner`, Span-first I/O, pooled buffers) that underlie all keys/values, see **[`.claude/skills/snowbank-slices-and-buffers/SKILL.md`](.claude/skills/snowbank-slices-and-buffers/SKILL.md)**. For standing up a cluster and connecting to it (the .NET Aspire hosting & client integrations, the native `libfdb_c` client, client⇄cluster version compatibility), i.e. getting the `IFdbDatabaseProvider` the other skills assume, see **[`.claude/skills/foundationdb-aspire/SKILL.md`](.claude/skills/foundationdb-aspire/SKILL.md)**. Read these before writing or reviewing key-encoding or layer code, even when working inside this repo.

These skills are also packaged as a Claude Code **plugin** ([`plugins/foundationdb-skills/`](plugins/foundationdb-skills/)) so consumers can install them (`/plugin marketplace add SnowBankSDK/foundationdb-dotnet-client`). The canonical skills live in `.claude/skills/`; the plugin's `skills/` is a **committed copy** kept in sync by the sync script ([`scripts/sync-plugin-skills.sh`](scripts/sync-plugin-skills.sh), or [`scripts/sync-plugin-skills.ps1`](scripts/sync-plugin-skills.ps1) on Windows), **after editing any skill, run that script** (`--check`/`-Check` verifies they haven't drifted) and bump the `version` in `plugins/foundationdb-skills/.claude-plugin/plugin.json` and `.claude-plugin/marketplace.json`.

## Docs

Human-facing docs live in [`Documentation/`](Documentation/) as Markdown (listed in [`Documentation/toc.yml`](Documentation/toc.yml)), rendered to a static site by [`SnowBank.DocGen`](SnowBank.DocGen/), a standalone .NET tool in this repo. It is **not** part of `FoundationDB.Client.slnx`; build and run it on its own:

```bash
dotnet run --project SnowBank.DocGen        # renders to artifacts/_site
```

The tool builds the guide pages, an API reference reflected from the built assemblies (`SnowBank.Core`, `FoundationDB.Client`, `FoundationDB.Aspire`, `FoundationDB.Aspire.Hosting`, so build those in Release first for the reference to appear), and a French twin under `/fr/` from the `*.fr.md` files. It shells out to Node for two build-time passes: mermaid diagrams to static SVG (needs a Chrome, pointed to by `CHROME_PATH`) and Shiki syntax highlighting; run `npm ci` in [`SnowBank.DocGen/tools`](SnowBank.DocGen/tools) once. The [`docs.yml`](.github/workflows/docs.yml) workflow runs all of this and publishes to GitHub Pages (manual dispatch).

The top-level [`README.md`](README.md) is the authoritative getting-started narrative, and it is packed into the NuGet package, so treat it as shipped surface.
