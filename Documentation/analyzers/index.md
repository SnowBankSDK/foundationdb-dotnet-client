# Analyzers

The `SnowBank.Core` and `FoundationDB.Client` packages include Roslyn analyzers. They run in every project that references the packages, and report code that compiles but fails at run time, can lose data, or does more work than needed. Each diagnostic names the fix. Most have a code fix, in the IDE and through `dotnet format analyzers --diagnostics <ID>`.

A project that references `FoundationDB.Client` runs both analyzers. A project that references only `SnowBank.Core` runs the `SBK` rules.

| Tier | Severity | IDs |
|---|---|---|
| Fails every time at run time, or does not compile | Error | `SBK0xxx`, `FDB0xxx` |
| Can corrupt or lose data | Warning | `SBK1xxx`, `FDB1xxx` |
| Correct but slower | Warning | `SBK2xxx`, `FDB2xxx` |

Mute a whole tier in `.editorconfig`:

```ini
dotnet_analyzer_diagnostic.category-FdbPerformance.severity = none
dotnet_analyzer_diagnostic.category-SnowBankPerformance.severity = none
```

A `dotnet_diagnostic.<ID>.severity` line for one rule takes precedence over the category line.

## Rules

| ID | Title | Severity | Analyzer |
|---|---|---|---|
| [FDB0001](FDB0001.md) | IFdbDatabase requested from dependency injection | Error | FoundationDB.Client |
| [FDB0002](FDB0002.md) | Watch created with the transaction's own token | Error | FoundationDB.Client |
| [FDB0003](FDB0003.md) | Watch awaited inside the handler that created it | Error | FoundationDB.Client |
| [FDB0100](FDB0100.md) | Removed API | Error | FoundationDB.Client |
| [FDB1001](FDB1001.md) | Subspace or layer state stored across transactions | Warning (correctness) | FoundationDB.Client |
| [FDB1002](FDB1002.md) | Subspace returned out of a retry-loop handler | Warning (correctness) | FoundationDB.Client |
| [FDB1003](FDB1003.md) | Byte-order atomic on a value that is not a little-endian number | Warning (correctness) | FoundationDB.Client |
| [FDB1004](FDB1004.md) | Id or clock read inside a retry-loop handler | Warning (correctness) | FoundationDB.Client |
| [FDB1005](FDB1005.md) | Missing key tested with Slice.Empty | Warning (correctness) | FoundationDB.Client |
| [FDB1006](FDB1006.md) | Version-stamped key written with Set | Warning (correctness) | FoundationDB.Client |
| [FDB2001](FDB2001.md) | Value built with a Core factory in a transaction write | Warning (performance) | FoundationDB.Client |
| [FDB2002](FDB2002.md) | Key serialized with .ToSlice() before a transaction call | Warning (performance) | FoundationDB.Client |
| [FDB2003](FDB2003.md) | Database obtained only to run a retry loop | Warning (performance) | FoundationDB.Client |
| [SBK0100](SBK0100.md) | Removed API | Error | SnowBank.Core |
| [SBK1003](SBK1003.md) | Null test on a JSON value | Warning (correctness) | SnowBank.Core |
