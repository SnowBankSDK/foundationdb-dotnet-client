FoundationDB .NET Client
=======================

C#/.NET binding for the [FoundationDB](https://www.foundationdb.org/) client library.

[![License](https://img.shields.io/github/license/SnowBankSDK/foundationdb-dotnet-client)](LICENSE.md)
[![NuGet version](https://img.shields.io/nuget/v/FoundationDB.Client.svg)](https://www.nuget.org/packages/FoundationDB.Client/)
![Platform](https://img.shields.io/badge/platform-windows%20%7C%20linux%20%7C%20macos-blue)
[![Build](https://img.shields.io/github/actions/workflow/status/SnowBankSDK/foundationdb-dotnet-client/dotnetcore.yml)](https://github.com/SnowBankSDK/foundationdb-dotnet-client/actions/workflows/dotnetcore.yml)
[![Last Commit](https://img.shields.io/github/last-commit/SnowBankSDK/foundationdb-dotnet-client)](https://github.com/SnowBankSDK/foundationdb-dotnet-client/commits/master/)

> **New to FoundationDB, or just trying it out?** The [documentation](Documentation/introduction.md) walks you through it end to end: [Prerequisites](Documentation/prerequisites.md), [How it connects](Documentation/foundationdb-101.md), [Cluster setup](Documentation/cluster-setup.md), then [Getting Started](Documentation/getting-started.md). It covers installing .NET and a local cluster, plus the one pitfall (matching the `FoundationDB.Client.Native` package to your cluster version) that otherwise shows up as mysterious timeouts.
>
> Already know FoundationDB? The steps below get you running, and [Getting Started](Documentation/getting-started.md) has the first read and write.

# How to use

Install the managed binding and the native client. Pin the native package to your cluster's `major.minor` version (see [Cluster setup](Documentation/cluster-setup.md)); it is the piece that must match your cluster:

```console
dotnet add package FoundationDB.Client
dotnet add package FoundationDB.Client.Native --version "7.4.*"   # match your cluster's version
```

Register FoundationDB with the dependency-injection container in your `Program.cs`. This adds an `IFdbDatabaseProvider` singleton. Point it at a cluster one of three ways, most-preferred first:

```csharp
// 1. Recommended: with the .NET Aspire integration, the AppHost supplies the connection.
//    Reference the FoundationDB.Aspire package, then:
builder.AddFoundationDb("fdb");

// 2. Manual, from an fdb.cluster file on disk:
builder.Services.AddFoundationDb(740, options =>
    options.ConnectionOptions.ClusterFile = "/etc/foundationdb/fdb.cluster");

// 3. Manual, from the cluster's connection string:
builder.Services.AddFoundationDb(740, options =>
    options.ConnectionOptions.ConnectionString = "docker:docker@127.0.0.1:4500");
```

The recommended path is [.NET Aspire](Documentation/aspire.md), which provisions a local cluster and injects the connection for you.

Inject that provider (it arrives as the `db` parameter below) and go through the retry loop. Here are two Minimal API endpoints: one lists every greeting, the other adds one.

```csharp
// GET /greetings : read them all back
app.MapGet("/greetings", (IFdbDatabaseProvider db, CancellationToken ct) =>
    db.ReadAsync(async tr =>
    {
        // resolve the subspace where the Greetings collection's keys are stored
        var subspace = await db.Root["Examples"]["Greetings"].Resolve(tr);

        // scan every key under that subspace and decode each value back to text
        return await tr.GetRange(subspace.ToRange())
            .Select(kv => kv.Value.ToStringUtf8())
            .ToListAsync();
    }, ct));

// POST /greetings?text=... : add a new one
app.MapPost("/greetings", async (IFdbDatabaseProvider db, string text, CancellationToken ct) =>
{
    await db.WriteAsync(async tr =>
    {
        // resolve the Greetings subspace, creating the directory the first time
        // (a plain Resolve would only open one that already exists)
        var subspace = await db.Root["Examples"]["Greetings"].CreateOrOpenAsync(tr);

        // store the greeting under a fresh, unique key
        tr.Set(subspace.Key(Guid.NewGuid()), FdbValue.ToTextUtf8(text));
    }, ct);
});
```

Keys are built with `subspace.Key(...)` and stay lazily tuple-encoded until the transaction runs, the retry loop handles FoundationDB's conflict-and-retry model, and the Directory layer (`db.Root[...]`) maps readable paths to short binary prefixes so your keyspace stays tidy.

**Full walkthrough:** the [documentation](Documentation/introduction.md) takes you from zero (installing .NET, running a local cluster) to your first read and write, then into modeling data with the Directory layer and Layers, and running it all with [.NET Aspire](Documentation/aspire.md).

What you get:

- **Strongly-typed, lazy keys**: `subspace.Key("user", 123)` tuple-encodes itself only when handed to a transaction. No manual byte wrangling.
- **A retry loop**: `ReadAsync` / `WriteAsync` / `ReadWriteAsync` handle FoundationDB's conflict-and-retry model for you.
- **The Directory layer**: map readable paths to short, dense key prefixes.
- **Layers**: a small contract (`IFdbLayer<TState>`) for reusable, composable data access.
- **Allocation-consciousness**: `Slice`, pooled buffers, and `struct` keys/values keep the hot path free of needless `byte[]`.
- **.NET Aspire**: start a local cluster and inject the connection automatically. See [Aspire](Documentation/aspire.md).

# Deployment

## Docker containers

The easiest way to deploy is to use one of the [ASP.NET Core Runtime docker images](https://hub.docker.com/r/microsoft/dotnet-aspnet/) provided by Microsoft, such as `mcr.microsoft.com/dotnet/aspnet:10.0` or newer.

In order to function, the FoundationDB Native client library (`fdb_c.dll` on Windows, `libfdb_c.so`) needs to be present in the container image. The easiest way is to simply copy them from the [FoundationDB Docker image](https://hub.docker.com/r/foundationdb/foundationdb) that contains these files.

Example of a `Dockerfile` that will grab v7.4.x binaries and inject them into you application container:

```Dockerfile
# Version of the FoundationDB Client Library
ARG FDB_VERSION=7.4.6

# We will need the official fdb docker image to obtain the client binaries
FROM foundationdb/foundationdb:${FDB_VERSION} as fdb

FROM mcr.microsoft.com/dotnet/aspnet:10.0

# copy the binary from the official fdb image into our target image.
COPY --from=fdb /usr/lib/libfdb_c.so /usr/lib

WORKDIR /App

COPY . /App

ENTRYPOINT ["dotnet", "MyWebApp.dll"]
```

## Manual deployment

The easiest solution is to install the `foundationdb-clients-X.Y.Z` packages from `https://apple.github.io/foundationdb/downloads.html`. Only the client packages should be installed, unless you also intend to run the cluster locally.

If you are manually copying your application files to the destination, either by unzip into a folder, or using a single-exe deployment, it is still necessary to also copy the `fdb_c.dll` or `libfdb_c.so` binaries to the destination

If, for any reason, you cannot copy the client binary to the default platform location (ex: `/usr/lib` on Linux), you can specify the full path to the library by settings the `NativeLibraryPath` option, or setting the `Aspire:FoundationDb:Client:NativeLibraryPath` key in the `appSettings.json` file (see the `FdbClientSettings` class other available settings).

If you need to troubleshoot the connection to the FoundationDB cluster, from the point of view of your application, it is also recommended to install `fdbcli` (comes with the `foundationdb-clients` package, needs to be manually deployed if not).

# AI-assisted development (Claude Code / Agent Skills)

This repository ships a set of **Agent Skills** that teach AI coding agents how to use this library *correctly*, the parts that are easy to get wrong when "vibe-coded": key/value encoding, subspaces and the Directory layer, writing custom Layers, the transaction retry loop, advanced/distributed patterns (change feeds, leases, retention, fencing), and the `Slice` binary toolkit. They live under [`.claude/skills/`](.claude/skills/) and follow the open [Agent Skills](https://agentskills.io) `SKILL.md` format, so they also work with other agents that support it.

The skills are **not** included in the NuGet packages. The easiest way to get them into your own projects (whether you consume this library via NuGet or as a submodule) is to install them as a **Claude Code plugin** from this repository, which doubles as a plugin marketplace:

```text
/plugin marketplace add SnowBankSDK/foundationdb-dotnet-client
/plugin install foundationdb-dotnet-skills@snowbank
```

Update later with `/plugin update foundationdb-dotnet-skills@snowbank`.

Alternatives, if you'd rather not use the plugin system:
- **Personal (all your projects):** copy the skill folders into `~/.claude/skills/`.
- **Per-project:** copy them into your app's `.claude/skills/` and commit them.
- **Submodule users:** point Claude at the checked-out submodule with `claude --add-dir path/to/foundationdb-dotnet-client` (or `/add-dir`), which loads its `.claude/skills/`.

The same material is available as human-readable documentation under [`Documentation/guide/`](Documentation/guide/) for developers (and agents that don't support Agent Skills).

# How to build

## Visual Studio Solution

You will need Visual Studio 2022 version 17.14 or above to build the solution (C# 14 and .NET 10.0 support is required).

### From the Command Line

You can also build, test and compile the NuGet packages from the command line using the `dotnet` CLI:

- `dotnet build` to build (in DEBUG) all the projects in the solution
- `dotnet test` to run the unit tests (requires a working local FoundationDB cluster).

The `scripts/` folder contains helpers for common tasks:
- `scripts/build.ps1` / `scripts/build.sh`: run a fully isolated **standalone** restore, clean and build (this repo's complete target set), even when it is checked out as a sub-module (see the *As a sub-module* section below). Add `Release` for a Release build.
- `scripts/pack.ps1` / `scripts/pack.sh`: build and validate the NuGet packages (they do not push to any feed).

### As a sub-module

Most projects in this repository are targeting multiple frameworks, meaning that each project will be build several times, one for each target.

When consuming this repository as a sub-module inside another repository, all the included projects will still want to build for all these targets, even if your parent solution only targets one framework (or a different subset).

This can also cause issues if you application is targeting an older .NET runtime and SDK (for example `net10.0` using the .NET 10.0.x SDK), which do not support more recent targets from this repo (ex: `net11.0`).

By default, the `Directory.Build.props` will attempt to detect when it is inside a git sub-module, and import any `Directory.Build.props` in the parent directory.

> **Note:** Some CI build environments may checkout sub-module in non-standard way. If this happens, you can set the environment variable `FDB_BUILD_PROPS_OVERRIDE` to `1` in order to bypass the check.

This parent props file can then override a series of msbuild variables that are injected in the `TargetFrameworks` property of all `.csproj` in this repo:
- `CoreSdkVersions`: overrides the value of all the other variables at once. Use this is you are single-targeting.

If you are multi-targeting and need more fine grained precision, you can use the following variables:
- `CoreSdkRuntimeVersions`: targets for all the core libraries (FoundationDB.Client.dll, ...) that are redistributed
- `CoreSdkToolsVersions`: targets for all the tools and executables (FdbShell, FdbTop, ...) that are redistributed
- `CoreSdkUtilityVersions`: targets for all the internal tools and executables that are only used for building, testing, and are not expected to be redistributed.
- `CloudSdkRuntimeVersions`: targets for all libraries that reference .NET Aspire (which is only supports .NET 8 or later).

If you parent repository is also multi-targeting, you can specify several targets, like for example `net10.0;net11.0`. Please note that is you target a more recent framework that is not supported by this repo, they may fail to build properly!

An example of a parent `Directory.Build.props` that overrides the build to only target `net10.0`:
```xml
<Project>
	<PropertyGroup>

		<!-- Force all projects in the FoundationDB sub-module to target net10.0 -->
		<CoreSdkVersions>net10.0</CoreSdkVersions>

	</PropertyGroup>
</Project>
```

An example of a parent `Directory.Build.props` that multi-targets `net10.0` and `net11.0`, but only want to build the tools for `net11.0`:
```xml
<Project>
	<PropertyGroup>

		<!-- If you are using FoundationDB.Client, SnowBank.Core, etc... -->
		<CoreSdkRuntimeVersions>net10.0;net11.0</CoreSdkRuntimeVersions>

		<!-- If you are using the FoundationDB .NET Aspire Integration -->
		<CloudSdkRuntimeVersions>net10.0;net11.0</CloudSdkRuntimeVersions>

		<!-- If you are using any of the tools (FdbShell, FdbTop) -->
		<CoreSdkToolsVersions>net11.0</CoreSdkToolsVersions>

	</PropertyGroup>
</Project>
```

### Building this repository on its own (standalone)

When you want to build, test, or package **this repository on its own** while it is still checked out inside such a parent (for example to validate the `net8.0` or `netstandard2.0` targets that a target-trimming parent would otherwise hide, or to produce a complete NuGet package), force a **standalone** build with the `CORESDK_STANDALONE_BUILD` property set to `true`. This makes the `Directory.Build.props` ignore the parent overrides entirely and restore this repo's own complete target set.

Pass it as an MSBuild property, not as an environment variable, so that it stays scoped to a single command and does not leak into your shell:

```
dotnet build FoundationDB.Client.slnx -p:CORESDK_STANDALONE_BUILD=true
```

Or use the helper scripts, which run a consistent `restore`, `clean` and `build` in standalone mode (add `Release` for a Release configuration):

```
./scripts/build.ps1
./scripts/build.sh
```

> **Important:** the restore step writes the shared `artifacts/obj/**/project.assets.json` files for the standalone (complete) target set, which is a superset of what the parent restores. Because the restore state is shared, the next build **from the parent** must re-run `dotnet restore` first, otherwise it fails with `NETSDK1005` (the assets file has no target for the parent's framework). Keep `restore`, `build` and `clean` on the same mode; when you switch modes, re-run `dotnet restore`.

# How to test

The test projects are using NUnit 4, and the test running must run as a 64-bit process (32-bit is not supported).

> In order to run the tests, you will also need to obtain the 'fdb_c.dll'/`libfdb_c.so` native library.

You can either run the tests from Visual Studio or Visual Studio Code, using any extension (like ReSharper), or from the command line via `dotnet test`.

> WARNING: All the tests try to run in a dedicated subspace, but there is a possibility of data corruption if they are running against a test or staging cluster! You should run the test against a local cluster where all the data is considered expandable!

# Implementation Notes

Please refer to https://apple.github.io/foundationdb/ to get an overview on the FoundationDB API, if you haven't already.

This .NET binding has been modeled to be as close as possible to the other bindings (Python especially), while still having a '.NET' style API. 

There were a few design goals, that you may agree with or not:
* Reducing the need to allocate `byte[]` as much as possible. To achieve that, I'm using a `Slice` struct that is the logical equivalent of `ReadOnlyMemory<byte>`, but more versatile.
* Mapping FoundationDB's Future into `Task<T>` to be able to use `async`/`await`. 
* Reducing the risks of memory leaks in long running server processes by wrapping all FDB_xxx handles with .NET `SafeHandle`. This adds a little overhead when P/Invoking into native code, but will guarantee that all handles get released at some time (during the next GC).
* The Tuple layer has also been optimized to reduce the number of allocations required, and cache the packed bytes of often used tuples (in subspaces, for example).

However, there are some key differences between Python and .NET that may cause problems:
* Python's dynamic types and auto casting of Tuples values, are difficult to model in .NET (without relying on the DLR). The Tuple implementation try to be as dynamic as possible, but if you want to be safe, please try to only use strings, longs, bools and byte[] to be 100% compatible with other bindings. You should refrain from using the untyped `tuple[index]` indexer (that returns an object), and instead use the generic `tuple.Get<T>(index)` that will try to adapt the underlying type into a T.
* The Tuple layer uses ASCII and Unicode strings, while .NET only have Unicode strings. That means that all strings in .NET will be packed with prefix type 0x02 and byte arrays with prefix type 0x01. An ASCII string packed in Python will be seen as a byte[] unless you use `ITuple.Get<string>()` that will automatically convert it to Unicode.
* There is no dedicated 'UUID' type prefix, so that means that `System.Guid` would be serialized as byte arrays, and all instances of byte 0 would need to be escaped. Since `System.Guid` are frequently used as primary keys, I added a new custom type prefix (0x30) for 128-bits UUIDs and (0x31) for 64-bits UUIDs. This simplifies packing/unpacking and speeds up writing/reading/comparing Guid keys.

The following files will be required by your application
* `FoundationDB.Client.dll` : Contains the core types (FdbDatabase, FdbTransaction, ...) and infrastructure to connect to a FoundationDB cluster and execute basic queries, as well as the Tuple and Subspace layers.
* `FoundationDB.Layers.Commmon.dll` : Contains common Layers that emulates Tables, Indexes, Document Collections, Blobs, ...
* `fdb_c.dll`/`libfdb_c.so` : The native C client that you will need to obtain from the official FoundationDB Windows setup or Linux client packages.

# Known Limitations

* Since the native FoundationDB client is 64-bit only, this .NET library is also for 64-bit only applications! Even though it targets AnyCPU, it would fail at runtime. _Don't forget to disable the `Prefer 32-bit` option in your project Build properties, that is enabled by default!_ 
* You cannot unload the fdb C native client from the process once the network thread has started. You can stop the network thread once, but it does not support being restarted. This can cause problems when running under ASP.NET.
* FoundationDB does not support long running batch or range queries if they take too much time. Such queries will fail with a 'past_version' error. The current maximum duration for read transactions is 5 seconds.
* FoundationDB has a maximum allowed size of 100,000 bytes for values, and 10,000 bytes for keys. Larger values must be split into multiple keys
* FoundationDB has a maximum allowed size of 10,000,000 bytes for writes per transactions (some of all key+values that are mutated). You need multiple transaction if you need to store more data. There is a Bulk API (`Fdb.Bulk.*`) to help for the most common cases (import, export, backup/restore, ...)
* See https://apple.github.io/foundationdb/known-limitations.html for other known limitations of the FoundationDB database.

# License

This code is licensed under the 3-clause BSD License.

# Contributing

* Yes, we use tabs! Get over it.
* Style rules are encoded in `.editorconfig` which is supported by most IDEs (or via extensions).
* You can visit the FoundationDB forums for generic questions (not .NET): https://forums.foundationdb.org/
