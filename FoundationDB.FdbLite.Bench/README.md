# FoundationDB.FdbLite.Bench

A replay harness for the FdbLite storage engine. One fixed matrix of 33 workloads (inserts, replaces, deletes, point reads, range scans, a ledger shape) is replayed identically on every engine, and the harness either checks the result (`verify`) or times it (`measure`). It needs no Docker and no native client. The BenchmarkDotNet suites live in `FoundationDB.FdbLite.Benchmarks`.

Run from this folder, in Release, and pick the runtime with `-f net11.0` or `-f net10.0`:

```bash
dotnet run -c Release -f net11.0 -- verify smoke
```

## Modes

| Mode | What it does | Exit code |
|---|---|---|
| `verify` (default) | Replays every workload on every engine next to an in-memory reference model and runs the checks below | 0 when every check passes, 1 otherwise |
| `measure` | Times every workload on every engine: warm-up passes, then the timed passes, engines interleaved | 0 |
| `profile` | Runs each workload in its own named frame with idle gaps, for a sampling profiler | 0 |
| `list` | Prints the matrix at the selected scale | 0 |
| `compare old.json new.json` | Prints the delta between two measure files | 0 |

Invalid arguments and I/O errors exit with 2.

The scales are `smoke` (20,000 keys, seconds), `standard` (500,000 keys, minutes) and `large` (2,000,000 keys). A third positional argument filters the workloads by name substring:

```bash
dotnet run -c Release -f net11.0 -- measure standard range
```

## Engines

| Name | What runs |
|---|---|
| `fdblite` | The raw engine on a memory-mapped file: `FdbLiteEngine`, writer and cursor, no transaction layer |
| `fdblite-db` | The same file store through the FoundationDB binding API (`FdbLiteStore`, `IFdbDatabase`) |
| `fdblite-mem` | The engine on the heap pager, every version retained, through the binding API |
| `fakedb` | The in-memory FakeDb store through the binding API |

The default set is `fdblite,fdblite-db,fakedb`. `fdblite` next to `fdblite-db` isolates the cost of the transaction layer; `fdblite-mem` next to `fakedb` compares the two committed stores behind one API.

Every FdbLite store runs with pre-commit consolidation pinned to `Off`, and the header of every run says so. A file store opens with the `Adaptive` policy by default, which is wall-clock driven, so a run that inherited it would differ from the previous one for reasons unrelated to the change under test.

## The verify checks

For each workload and engine, after the replay:

1. the read checksum of the timed steps equals the model's;
2. a full scan of the store equals the model's content, pair by pair;
3. `FdbLiteTreeAudit.Check` reports no problem in the committed tree (FdbLite engines);
4. after a close and a reopen from disk, the full scan still equals the model (file engines).

A failed check prints the engine, the check and the first differences, and the run exits with 1. An engine that throws during a workload counts as a failure of that workload. Transaction semantics (conflicts, atomic mutations, versionstamps) are covered by the conformance suite in `FoundationDB.FdbLite.Tests`; verify covers the stored content.

## Measuring and comparing runs

```bash
# before the change
dotnet run -c Release -f net11.0 -- measure standard --out before.json

# after the change: the same table, then the delta row by row
dotnet run -c Release -f net11.0 -- measure standard --out after.json --compare before.json

# the delta alone, from the two files
dotnet run -c Release -f net11.0 -- compare before.json after.json
```

Each row carries the median ns/op with its min..max spread, the managed bytes allocated per operation (exact, the replay is single-threaded), the file size and a few engine counters. The JSON file holds every sample, every counter, and the host, runtime, page size and scale of the run. The comparison marks a delta beyond 10% in either direction and warns when the two files differ in host, runtime, page size or scale.

Tiered compilation is the main source of false findings: the first few hundred thousand operations of a process run in tier-0 code. The warm-up runs passes until a pass no longer beats the best one by more than 5%, up to `--warmup` passes. A spread far wider than the delta means the delta is noise.

## Options

| Option | Default | Meaning |
|---|---|---|
| `--engines a,b,c` | `fdblite,fdblite-db,fakedb` | Engines to run |
| `--page-log2 N` | `15` (32 KiB) | Page size as a power of two, 12 to 16 |
| `--repeats N` | `3` | Timed passes per workload and engine |
| `--warmup N` | `3` | Ceiling on warm-up passes |
| `--dir path` | `<temp>/fdblite-bench` | Folder of the store files (deleted after each workload) |
| `--out file.json` | | Write the measure results to this file |
| `--compare old.json` | | Print the delta against an earlier file after the measure |
| `--latency` | off | Per-operation latency histograms: p50, p99 and p999 per operation kind, and the burst pattern of the slow operations. Two timestamps per operation, so the headline ns/op is a few percent higher with it on |

## Profiling

```bash
dotnet run -c Release -f net11.0 -- profile smoke replace --engines fdblite
```

After a warm-up, each selected workload runs 3 rounds inside its own `Workload_<name>` frame (never inlined), with 2-second gaps, and a `###` marker with the wall-clock time before and after each round. A profiler timeline then shows one named burst per round, and the call tree splits by workload. A new workload needs its frame added to `ProfileMode.RunNamed`; until then it runs under the plain replay frame and the header says so.
