# Getting Started samples

Runnable companions to [Documentation/getting-started.md](../../Documentation/getting-started.md).

- `console/` connects, reads the cluster version, then writes and reads a key.
- `web/` does the same over a Minimal API, with a Scalar dashboard at `/scalar/v1`.

Both expect a local FoundationDB 7.4 cluster reachable at `docker:docker@127.0.0.1:4500`. Set one up with [Cluster setup](../../Documentation/cluster-setup.md), then run:

```console
dotnet run --project console
dotnet run --project web
```

These are standalone reference projects: they use the published NuGet packages and are not part of `FoundationDB.Client.slnx`. Copy either folder into your own solution as a starting point.
