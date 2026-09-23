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
| [SBK1003](SBK1003.md) | Null test on a JSON value | Warning (correctness) | SnowBank.Core |
