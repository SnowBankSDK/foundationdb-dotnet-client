FoundationDB.FdbLite
====================

A single-process, on-disk key/value store that speaks the FoundationDB transaction API without a
cluster. It fills the role SQLite fills for SQL: an application, an edge node or a test opens a file
(or an in-memory store) and runs unchanged FoundationDB layer code against it.

# Concept

The store is a copy-on-write B+tree over a memory-mapped file. Commits are crash-safe. On top of
the engine runs the same read-your-writes transaction machinery as the in-memory `FoundationDB.FakeDb`
emulator: snapshot reads, read-conflict tracking, atomic mutations, versionstamps, watches and key
selectors, with the semantics of a real cluster. The two differ in what they keep: FakeDb keeps
every published version by default, FdbLite keeps a recent-version window and reclaims the pages of
older versions, so its file stays bounded.

The store is single-process: one process opens a given file at a time. There is no network, no
replication and no multi-tenant isolation; a real cluster remains the target for shared data.

# How to use

```
dotnet add package FoundationDB.FdbLite
```

```xml
<ItemGroup>
  <PackageReference Include="FoundationDB.FdbLite" Version="8.0.0" />
</ItemGroup>
```

With dependency injection, call `AddFdbLite(...)` where a cloud node calls `AddFoundationDb(...)`.
The registration provides the same `IFdbDatabaseProvider`, so the code that opens transactions does
not change:

```c#
using FoundationDB.FdbLite;

var builder = Host.CreateApplicationBuilder(args);

// a file-backed store, created on first start
builder.Services.AddFdbLite(730, path: "/var/lib/acme/agent/state.fdblite");

var app = builder.Build();
```

```c#
// the consumer side is the regular FoundationDB client API
var db = await provider.GetDatabase(ct);
await db.WriteAsync(async tr =>
{
	// a fresh store has no directories: ResolveOrCreate makes ours on the first write
	var subspace = await db.Root["state"].ResolveOrCreate(tr);
	tr.Set(subspace.Key("hello"), FdbValue.ToTextUtf8("world"));
}, ct);
```

`AddFdbLite(730)` without a path opens an in-memory store that dies with the provider. The
`configure` callback sets the geometry of a new file (`Geometry`, default 32 KiB pages), the
retention policy (`Retention`), the time source (`Time`), a read-only mode or transaction logging.

Several providers can share one store, for example two emulated processes in one test:

```c#
using var store = FdbLiteStore.OpenOrCreateFile(path, FdbLiteGeometry.Default, apiVersion: 730);
services1.AddFdbLite(store);
services2.AddFdbLite(store);
// the store belongs to the test and outlives both containers
```

Without dependency injection, open the store and a database on it directly:

```c#
using var store = FdbLiteStore.OpenOrCreateFile(path, FdbLiteGeometry.Default, apiVersion: 730);
using var db = store.OpenDatabase(FdbPath.Root, readOnly: false);
```

The geometry of a file is fixed when the file is created; an existing file keeps the geometry in
its header and ignores the argument.

Requires .NET 10 or later.
