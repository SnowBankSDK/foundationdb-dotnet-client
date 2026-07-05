# CLAUDE.md

Guidance for AI agents (and humans) working **in this repository**. If you are instead trying to *use* this library in your own application, read the skills under [`.claude/skills/`](.claude/skills/) — they are written for consumers and are the canonical guide to the key/value and transaction APIs.

## What this repository is

A C#/.NET binding for [FoundationDB](https://www.foundationdb.org/) (a distributed, ordered key/value store), plus the general-purpose libraries it is built on. The main solution is **`FoundationDB.Client.slnx`** (the new XML `.slnx` format). Current version: see [`Common/VersionInfo.props`](Common/VersionInfo.props) (`7.4.x`).

The repo holds **two distinct product families**:

- **`SnowBank.*`** — general-purpose foundation, *not* FoundationDB-specific. `SnowBank.Core` is the bedrock (Slices, the Tuple encoding, the CrystalJson stack, collections, async LINQ, UUIDs). Also `SnowBank.Shell`, `SnowBank.Serialization.Json.CodeGen` (a Roslyn source generator), `SnowBank.Networking.*`, `SnowBank.Testing.*`.
- **`FoundationDB.*`** — the actual binding. `FoundationDB.Client` is the core (native interop, transactions, keys/values, subspaces, the Directory layer, tenants, DI). `FoundationDB.Layers.Common` holds demo layers (Map, Index, Vector, Queue, Counter, Blob, …). Aspire, FakeDb, Testing, BindingTester, and the `Fdb*` tools sit around it.

### Dependency direction (do not invert)

```
SnowBank.Core  ◄─  FoundationDB.Client  ◄─  FoundationDB.Layers.Common  ◄─  Layers.Experimental, Linq.Providers
     ▲                                                                  
SnowBank.Shell, SnowBank.Networking.*, SnowBank.Testing.*, SnowBank.Serialization.Json.CodeGen
```

`SnowBank.Core` has **no** project references and must never depend on FoundationDB. `FoundationDB.Client` depends only on `SnowBank.Core`. Tooling/tests reference downward only.

## Build & test

```bash
dotnet build FoundationDB.Client.slnx          # DEBUG build of everything
dotnet test  FoundationDB.Client.slnx          # run all tests
```

- **SDK**: pinned in [`global.json`](global.json) to a **.NET 11 preview** SDK (`rollForward: latestMinor`). `LangVersion` is `preview`.
- **Target frameworks**: libraries multi-target `net11.0;net10.0;net8.0` (see [`Directory.Build.props`](Directory.Build.props)). Each project builds once per target. Several projects **also build a `netstandard2.0` "lite" variant** — see [The netstandard2.0 / net472 "lite" targets](#the-netstandard20--net472-lite-targets) below; keeping it green is part of any change to those projects.
- **Build output** goes to `artifacts/` (`ArtifactsPath`), not per-project `bin/obj`.
- **Central package management**: all versions live in [`Directory.Packages.props`](Directory.Packages.props). Add/bump packages there, not in `.csproj` files. **Version policy**: this SDK is a BOM for downstream applications — each pin should be the **latest version that still supports its TFM bucket** (the .NET platform wave is multi-TFM, so net8/net9/net10 usually share one version; ASP.NET Core packages are single-TFM and stay capped per bucket). Deliberate exception: `Microsoft.CodeAnalysis.*` stays low, because it sets the minimum Roslyn/SDK for consumers of the `SnowBank.Serialization.Json.CodeGen` source generator.
- **As a submodule**: a parent repo can override targets via `CoreSdkVersions` (or the finer `CoreSdkRuntimeVersions` / `CoreSdkToolsVersions` / `CloudSdkRuntimeVersions`) in its own `Directory.Build.props`. The override import is gated on a `.git` *file* check; bypass with `FDB_BUILD_PROPS_OVERRIDE=1`.

### Tests

- **NUnit 4**, and the runner **must be 64-bit** (the native client is 64-bit only).
- ⚠️ **On the .NET 10+ SDK, `dotnet test` can fail** for these NUnit/Microsoft.Testing.Platform projects with *"Testing with VSTest target is no longer supported by Microsoft.Testing.Platform"*. Fallback: build, then **run the test assembly directly** — `dotnet artifacts/bin/<Project>/debug_net11.0/<Project>.dll`, filtering with `--filter "FullyQualifiedName~<NamePart>"` (not `--treenode-filter`) and `--output Detailed` for per-assert output.
- `SnowBank.*.Tests` are pure and need no database.
- **`FoundationDB.Tests` requires a running local FoundationDB cluster** and the native `fdb_c` library (`libfdb_c.dylib` on macOS, `libfdb_c.so` on Linux, `fdb_c.dll` on Windows). `FoundationDB.Client.Native` redistributes these.
  - ⚠️ Tests write to a dedicated subspace but **can corrupt data** — only point them at a throwaway local cluster.
- `FoundationDB.FakeDb` provides an in-memory fake for tests that don't want a real cluster.
- Test classes use the `*Facts` naming convention (e.g. `FdbKeyFacts`, `TuPackFacts`).

### The netstandard2.0 / net472 "lite" targets

`SnowBank.Core`, `FoundationDB.Client`, `FoundationDB.Client.Native`, `FoundationDB.Layers.Common`, and `FoundationDB.FakeDb` **also build for `netstandard2.0`** (gated by `CoreSdkNetStandardEnabled`, default `true`). This is a deliberate stopgap for applications migrating from .NET Framework 4.7.2 to modern .NET: their legacy `main` branch can already consume the modern APIs (`Contract.NotNull`, CrystalJson, `Slice`, `subspace.Key(...)`), which reduces merge pain with their modernized `vnext` branch. The lite build must **run** on the net472 CLR (including the native `fdb_c` interop), not just compile — and it must produce **byte-identical keys/values**: what is written to the database must never depend on the TFM.

**Any change to these projects must keep the `netstandard2.0` target building** (`dotnet build <proj>.csproj -f netstandard2.0`). When code needs something the old BCL lacks:

1. **Missing BCL API with many call sites** → prefer a polyfill in [`SnowBank.Core/Polyfills/`](SnowBank.Core/Polyfills/) so call sites compile unchanged. Public extension polyfills live in the `SnowBank.Compat` namespace (imported via a guarded `global using` in each project's `GlobalUsings.cs`; this includes C# 14 static extension members like `string.Create(provider, $"...")` and `Span<char>.TryWrite($"...")`). Statics that can't be extensions are internal `*Compat` classes redirected per-file: `using CollectionsMarshal = SnowBank.Compat.CollectionsMarshalCompat;` under `#if NETSTANDARD2_0`.
2. **Point fallback** → `#if`/`#else`/`#endif` with the original implementation FIRST under `#if NETX_0_OR_GREATER` (X = the version that introduced the feature/API), and the fallback in `#else` with a comment on the next line stating what is missing and the cost ("X is not available, doing Y instead (which allocates)").
3. **Needs runtime support — cannot be polyfilled** (default interface members, `static abstract` interface members, covariant return types, ref fields, `allows ref struct`) → guard the member out, or exclude the whole feature from the lite build (e.g. `FdbValue<TValue, TEncoder>` and `FdbValueCodec.FixedSize`, the FQL `Query/` engine, the JSON source-gen proxies).
4. **Portable `Span<T>` gotchas**: on netstandard2.0, `Span<T>` cannot wrap raw pointers/refs of types containing references *or pointers* (e.g. `Span<FdbKeyValue>`), and `MemoryMarshal.CreateSpan` shims only support unmanaged types — use direct pointer indexing or a temporary array instead.

**Validation**: `net472` test targets exist on `SnowBank.Core.Tests`, `FoundationDB.Tests`, and `FoundationDB.FakeDb.Tests` (they consume the netstandard2.0 builds and run on the real .NET Framework CLR). Test files covering excluded features are guarded with `#if !NETFRAMEWORK` (or `<Compile Remove>` in the csproj for whole folders). ⚠️ `dotnet test -f net472` hits the VSTest/MTP issue for `FoundationDB.Tests` — run it with the NUnit console runner instead: `~/.nuget/packages/nunit.consolerunner/<version>/tools/nunit3-console.exe artifacts/bin/FoundationDB.Tests/debug_net472/FoundationDB.Tests.dll`. On net472 the fdb test container is driven through the docker CLI (Docker.DotNet cannot run on the netfx CLR); a freshly created test container needs a one-time `docker exec <container> fdbcli --exec "configure new single ssd"`.

Known accepted netfx differences (do not "fix" by changing shared code): doubles may format with 17 digits instead of shortest-roundtrip (`FastDtoa` is excluded), and `IVarTuple` ↔ `ValueTuple`/`ITuple` interop is unavailable (`System.Runtime.CompilerServices.ITuple` is not visible to netstandard2.0).

## Coding conventions

Style is enforced by [`.editorconfig`](.editorconfig) and the `.DotSettings` files. Match the surrounding code; notable points:

- **Tabs, not spaces.** (This is deliberate and non-negotiable per the README.)
- **Block-scoped namespaces** (`namespace Foo { … }`), not file-scoped. Every file opens with the BSD-3-Clause copyright header `#region`.
- `Nullable` is **enabled**; `ImplicitUsings` is enabled (shared usings live in each project's `GlobalUsings.cs`).
- Public API is documented with XML doc comments (`///`) — keep them accurate and add `<remarks>`/`<example>` for non-obvious behavior, as the existing code does heavily.
- Allocation-consciousness is a core value: prefer `Slice`/`ReadOnlySpan<byte>`, pooled buffers, and `struct` keys/values over `byte[]`. Many hot types are `readonly struct` implementing span-based interfaces (`ISpanEncodable`).
- Don't break public surface casually. Retire APIs with `[Obsolete]` (the codebase uses `error: true` for hard-removed ones) rather than deleting outright.

## Where things live (FoundationDB.Client)

| Area | Path | Notes |
|---|---|---|
| Entry/factory, well-known keys | [`FdbKey.cs`](FoundationDB.Client/FdbKey.cs) | `FromBytes`, `FromTuple`, `ToSystemKey`, `Increment`, `Dump` |
| Strongly-typed keys | [`Keys/`](FoundationDB.Client/Keys/) | `FdbTupleKey<…>`, `FdbRawKey`, `FdbSuffixKey`, derivations; extensions in `FdbKeyExtensions.cs` |
| Values | [`Values/`](FoundationDB.Client/Values/), [`FdbValue.cs`](FoundationDB.Client/FdbValue.cs) | `FdbValue.ToBytes/ToTextUtf8/FromTuple/ToFixed64LittleEndian/ToJson/…` |
| Subspaces | [`Subspaces/`](FoundationDB.Client/Subspaces/) | `IKeySubspace`, `KeySubspace`, `ISubspaceLocation` |
| Directory layer | [`Layers/Directories/`](FoundationDB.Client/Layers/Directories/) | `FdbDirectoryLayer`, `FdbPath`, `FdbDirectorySubspace` |
| Transactions | [`FdbTransaction*.cs`](FoundationDB.Client/), `IFdb*Transaction.cs` | retry loops on `IFdbDatabase` (`IFdbRetryable`) |
| Layer contract | [`IFdbLayer.cs`](FoundationDB.Client/IFdbLayer.cs) | `IFdbLayer<TState>`, `layer.ReadAsync/WriteAsync/ReadWriteAsync` |
| Native interop | [`Native/`](FoundationDB.Client/Native/) | P/Invoke, `SafeHandle`s, `FdbFuture`→`Task` |
| DI / Aspire | [`DependencyInjection/`](FoundationDB.Client/DependencyInjection/) | `AddFoundationDb`, `IFdbDatabaseProvider` |

In `SnowBank.Core`: the Tuple encoding lives in [`Data/Tuples/`](SnowBank.Core/Data/Tuples/) (`TuPack`, `STuple<…>`, `IVarTuple`); JSON in [`Data/JSON/`](SnowBank.Core/Data/JSON/) (`CrystalJson`); `Slice` and buffers in [`Buffers/`](SnowBank.Core/Buffers/).

## Working on the key/value API or layers

The single most important thing to get right (and the most common source of incorrect "vibe-coded" usage) is **how keys are encoded and how a custom Layer is structured**. The rules:

- Keys are built with `subspace.Key(item1, …)` and friends, returning **lazy strongly-typed key structs** (`FdbTupleKey<…>`). They are rendered to bytes only when handed to the transaction (`tr.GetAsync(key)`, `tr.Set(key, …)`). Do **not** eagerly call `.ToSlice()` and pass bytes around.
- A Layer is a thin wrapper over an `ISubspaceLocation`; it implements `IFdbLayer<TState>`, and `Resolve(tr)` returns a per-transaction `State` holding the resolved `IKeySubspace`. The state must **never** escape the transaction.

Full guidance, worked examples, and the list of reference layers to imitate are in the skill **[`.claude/skills/foundationdb-keys-and-layers/SKILL.md`](.claude/skills/foundationdb-keys-and-layers/SKILL.md)**. Transaction/retry semantics are in **[`.claude/skills/foundationdb-transactions/SKILL.md`](.claude/skills/foundationdb-transactions/SKILL.md)**. For sophisticated/distributed layers — cluster internals, read-batching/latency, change feeds, version-as-clock leases, retention and fencing — see **[`.claude/skills/foundationdb-advanced-layers/SKILL.md`](.claude/skills/foundationdb-advanced-layers/SKILL.md)**. For the `Slice` type and its companions (`SliceReader`/`SliceWriter`/`SliceOwner`, Span-first I/O, pooled buffers) that underlie all keys/values, see **[`.claude/skills/snowbank-slices-and-buffers/SKILL.md`](.claude/skills/snowbank-slices-and-buffers/SKILL.md)**. For standing up a cluster and connecting to it (the .NET Aspire hosting & client integrations, the native `libfdb_c` client, client⇄cluster version compatibility) — i.e. getting the `IFdbDatabaseProvider` the other skills assume — see **[`.claude/skills/foundationdb-aspire/SKILL.md`](.claude/skills/foundationdb-aspire/SKILL.md)**. Read these before writing or reviewing key-encoding or layer code, even when working inside this repo.

These skills are also packaged as a Claude Code **plugin** ([`plugins/foundationdb-skills/`](plugins/foundationdb-skills/)) so consumers can install them (`/plugin marketplace add SnowBankSDK/foundationdb-dotnet-client`). The canonical skills live in `.claude/skills/`; the plugin's `skills/` is a **committed copy** kept in sync by the sync script ([`scripts/sync-plugin-skills.sh`](scripts/sync-plugin-skills.sh), or [`scripts/sync-plugin-skills.ps1`](scripts/sync-plugin-skills.ps1) on Windows) — **after editing any skill, run that script** (`--check`/`-Check` verifies they haven't drifted) and bump the `version` in `plugins/foundationdb-skills/.claude-plugin/plugin.json` and `.claude-plugin/marketplace.json`.

## Docs

Human-facing docs (docfx) live in [`Documentation/`](Documentation/) (`getting-started.md`, `Tuples.md`, `Transaction_Basics.md`). The top-level [`README.md`](README.md) is the authoritative getting-started narrative and is packed into the NuGet package.
