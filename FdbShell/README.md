FdbShell
========

A command-line shell for exploring and querying a live FoundationDB cluster.

# Install

`FdbShell` is a [.NET tool](https://learn.microsoft.com/dotnet/core/tools/global-tools). Install it globally:

```
dotnet tool install --global FdbShell
```

# Usage

```
fdbshell
```

It connects using the default cluster file (or one you point it at), and drops you into an interactive prompt. From there you can browse the **Directory Layer**, list and read keys and ranges, run queries, and inspect the cluster's status. A convenient way to poke at a cluster during development.

# Connecting

By default `fdbshell` uses the standard FoundationDB cluster file. A few options make it easy to point it elsewhere, in particular at a cluster running locally in Docker:

- `-c`, `-C`, `--connfile <path>`: path to a cluster file.
- `--connStr <string>`: a connection string, instead of a file.
- `--docker <port>`: connect to a local FoundationDB running in Docker on the given port.
- `--aspire`: connect to a local FoundationDB Docker instance managed by .NET Aspire.
- `--api <version>`: the API version level to use.
- `--partition <name>` (`-p`): open a named database partition.
- `--timeout <seconds>` (`-t`), `--retries <n>` (`-r`): transaction defaults.
- `--exec "<command>"`: run a single command and exit (non-interactive).

```
fdbshell --docker 4550             # a cluster running in Docker on port 4550
fdbshell --aspire                  # a cluster managed by .NET Aspire
fdbshell --exec "dir ls /"         # run one command and exit
```

> Requires the native FoundationDB client (`fdb_c` / `libfdb_c`) matching your cluster's version.
