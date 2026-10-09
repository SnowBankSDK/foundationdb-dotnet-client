# FoundationDB.FdbLite.Benchmarks

BenchmarkDotNet suites for the FdbLite storage engine. They measure time and allocations only. To check that FdbLite works, and to compare two runs workload by workload, use `FoundationDB.FdbLite.Bench`.

Run from this folder, in Release: BenchmarkDotNet stops on a Debug build. Pick the runtime with `-f net11.0` or `-f net10.0`.

```bash
dotnet run -c Release -f net11.0 -- --list flat
```

## Suites

| Class | What it measures | Disk |
|---|---|---|
| `FdbLiteGeometryMatrixBenchmarks` | Point reads, a full scan, a tiny commit and a 100 KB commit for the four candidate page geometries, on an in-memory pager (CPU and layout cost only) | No |
| `CommittedStoreScanBenchmarks` | Full scans of the FakeDb committed store, per loop shape: `IEnumerable`, interface cursor, generic struct cursor | No |
| `FdbLiteCommittedStoreScanBenchmarks` | The same scan loops over an FdbLite store (in-memory pager), plus the raw engine cursor | No |
| `FdbLiteDurabilityBenchmarks` | The cost of a durable commit and of the flush primitive on this machine | Yes |
| `FdbLiteFileStoreBenchmarks` | Point reads and a 100k aggregate on a 1 GiB file store, at the `cold` and `restart` tiers | Yes |
| `FdbLiteFileStoreHotBenchmarks` | The same reads at steady state (`hot` tier) | Yes |

The geometries are `u16K`, `u32K`, `u64K` and `b16K/p64K` (`split` is an alias of `b16K/p64K`). The file-store suites build one store file of about 1 GiB per geometry under `<temp>/fdblite-bench/` on the first run, and reuse it afterwards.

## Recipes

```bash
# one suite
dotnet run -c Release -f net11.0 -- --filter "*Durability*"

# one cell of the file-store matrix: one geometry, one tier
dotnet run -c Release -f net11.0 -- --filter "*FileStoreBenchmarks*" --geometry=u32K --tier=restart

# the hot tier is a separate class
dotnet run -c Release -f net11.0 -- --filter "*FileStoreHot*" --geometry=u32K
```

`--geometry=` and `--tier=` restrict the file-store matrix to one cell per run, so the full matrix is assembled from short runs. A cell disturbed by background activity is then re-run alone. A value that matches nothing throws, instead of running the whole matrix.

A `cold` or `restart` cell also writes a cache-cliff log next to the BenchmarkDotNet reports: the throughput of each block of 10,000 reads over the store. To produce that log on its own:

```bash
dotnet run -c Release -f net11.0 -- --chunklog u32K cold
```

## Cold tier

The `cold` tier empties the operating system file cache before each iteration. Every purge method needs elevation, so run the cold series from an elevated shell:

- Windows: `RAMMap -Et` when RAMMap is on the `PATH`, otherwise `NtSetSystemInformation` (the process needs `SeProfileSingleProcessPrivilege`). Both empty the standby list.
- macOS: `purge`, which may ask for `sudo`.
- Linux: `drop_caches`, as root.

To use another tool, put its command line in the `FDBLITE_CACHE_PURGE_CMD` environment variable. To check that the purge works on this machine:

```bash
dotnet run -c Release -f net11.0 -- --purgecheck
```

When the purge fails, the run prints `COLD TIER NOT COLD`, and those rows measure the `restart` tier instead.
