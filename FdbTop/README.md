FdbTop
======

A command-line tool for monitoring a live FoundationDB cluster: a `top`-style live view of what the cluster is doing.

# Install

`FdbTop` is a [.NET tool](https://learn.microsoft.com/dotnet/core/tools/global-tools). Install it globally:

```
dotnet tool install --global FdbTop
```

# Usage

```
fdbtop
```

It shows a continuously-updating dashboard of throughput, latencies, and process/role health, handy for watching a cluster under load during development or testing.

# Connecting

By default `fdbtop` uses the standard FoundationDB cluster file. A few options make it easy to point it elsewhere, in particular at a cluster running locally in Docker:

- a positional path, or `-c`, `-C`, `--connfile <path>`: path to a cluster file.
- `--connStr <string>`: a connection string, instead of a file.
- `--docker <port>`: connect to a local FoundationDB running in Docker on the given port.
- `--aspire`: connect to a local FoundationDB Docker instance managed by .NET Aspire.
- `--api <version>`: the API version level to use.
- `--timeout <seconds>` (`-t`): default transaction timeout.

```
fdbtop                          # the default cluster file
fdbtop /path/to/fdb.cluster     # a specific cluster file
fdbtop --docker 4550            # a cluster running in Docker on port 4550
fdbtop --aspire                 # a cluster managed by .NET Aspire
```

> Requires the native FoundationDB client (`fdb_c` / `libfdb_c`) matching your cluster's version.
