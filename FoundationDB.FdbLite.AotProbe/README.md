# FoundationDB.FdbLite.AotProbe

A trim/AoT probe for the FdbLite persistent store. It roots the FdbLite public surface so the IL
trimmer analyzes the reachable closure: open an in-memory store and a file-backed store, run a write
transaction with tuple keys, read a key back, run a range read, clear a range, and round-trip a
composite tuple key.

It is cluster-free and native-client-free (FdbLite is pure managed), so it runs to completion under a
self-contained or trimmed publish. It is not part of `FoundationDB.Client.slnx`; publish it directly.

## Run

Debug run (roots the same surface, no trimming):

```
dotnet build FoundationDB.FdbLite.AotProbe -f net11.0
dotnet exec artifacts/bin/FoundationDB.FdbLite.AotProbe/debug/FoundationDB.FdbLite.AotProbe.dll
```

Trim-warning pass (about 1 to 2 minutes, no ILC):

```
dotnet publish FoundationDB.FdbLite.AotProbe -r win-x64 -p:ProbeMode=scd -c Release
```

then grep the output for `warning IL`. `ProbeMode` selects the publish shape: `sc` (self-contained,
no trim), `scd` (self-contained + trimmed), `aot` (Native AoT). The flags are project-scoped, driven
by `ProbeMode`, never passed as global `-p:` properties: a global `PublishTrimmed`/`PublishAot` leaks
onto the netstandard2.0 source generator and fails it.

## What it shows

With tuple keys, the whole closure builds trim-warning-clean except for the shared `SnowBank.Core`
reflective tuple codec (`TuplePackers.SerializeObjectTo` and the boxed-encoder table, which reach the
CrystalJson reflection resolver). Swap the tuple keys for raw byte keys and the closure is
warning-clean: FdbLite and `FoundationDB.Client` add no trim hazard of their own.
