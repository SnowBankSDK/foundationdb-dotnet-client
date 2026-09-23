; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
FDB0001 | FdbCorrectness | Error | DatabaseInjectionAnalyzer
FDB0002 | FdbCorrectness | Error | WatchAnalyzer
FDB0003 | FdbCorrectness | Error | WatchAnalyzer
FDB0100 | FdbCorrectness | Error | RemovedApiAnalyzer
FDB1001 | FdbCorrectness | Warning | SubspaceLifetimeAnalyzer
FDB1002 | FdbCorrectness | Warning | SubspaceLifetimeAnalyzer
FDB1003 | FdbCorrectness | Warning | MutationEncodingAnalyzer
FDB1004 | FdbCorrectness | Warning | RetryLoopValueAnalyzer
FDB1005 | FdbCorrectness | Warning | MissingKeyTestAnalyzer
FDB1006 | FdbCorrectness | Warning | MutationEncodingAnalyzer
FDB2001 | FdbPerformance | Warning | EncodingPerformanceAnalyzer
FDB2002 | FdbPerformance | Warning | EncodingPerformanceAnalyzer
FDB2003 | FdbPerformance | Warning | ProviderUnwrapAnalyzer
