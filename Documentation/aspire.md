# .NET Aspire

[.NET Aspire](https://aspire.dev/) is the recommended way to run FoundationDB during local development and to wire it into your services. Instead of starting a container and copying a cluster file by hand (as in [Cluster setup](cluster-setup.md)), Aspire starts a local cluster for you and injects the connection string into every project that references it.

It is also one of the main patterns for building modern .NET applications on FoundationDB: an AppHost describes your cluster and services, and each service reads its connection from configuration.

## The two packages

Aspire splits into a host side and a client side:

- **`FoundationDB.Aspire.Hosting`** goes in your **AppHost** project. It defines the cluster resource.
- **`FoundationDB.Aspire`** goes in **each service** that connects to the cluster. It reads the injected connection and registers `IFdbDatabaseProvider`.

```console
# in the AppHost project
dotnet add package FoundationDB.Aspire.Hosting

# in each project that connects to the cluster
dotnet add package FoundationDB.Aspire
```

Each service still needs the `FoundationDB.Client.Native` package matching the cluster version (see [How it connects](foundationdb-101.md)).

## Start a local cluster (AppHost)

In the AppHost's `Program.cs`, declare a cluster and hand a reference to the projects that need it:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Start a local single-node FoundationDB cluster in Docker.
var fdb = builder.AddFoundationDb("fdb",
    // API level, at or below the cluster version
    apiVersion: 740,
    // the directory-layer root your app resolves paths against
    root: "/Tenant/ACME/MyApp/v1",
    // the Docker image tag
    clusterVersion: "7.4.6",
    rollForward: FdbVersionPolicy.Exact);

// Give a project a reference to the cluster; its connection string is injected.
builder.AddProject<Projects.MyApp>("backend")
    // inject the "fdb" connection
    .WithReference(fdb)
    // hold the app until the cluster is healthy
    .WaitFor(fdb);

builder.Build().Run();
```

`AddFoundationDb(...)` runs the cluster as a Docker container. `WithReference(fdb)` passes the connection string to the referenced project under the same name (`"fdb"`), and `WaitFor(fdb)` delays startup until the cluster is ready. Add `.WithLifetime(ContainerLifetime.Persistent)` to the cluster to reuse the container (and its data) across runs.

> On the very first start, a fresh cluster has no database until it is configured once. Use Docker Desktop (or `docker exec`) to run `fdbcli --exec "configure new single ssd"` inside the container, then restart the AppHost. While it runs, the cluster is reachable at `docker:docker@127.0.0.1:4550`.

## Connect to an existing cluster instead

For staging or production, or any cluster you did not start with Aspire, use `AddFoundationDbCluster` with a cluster file instead of `AddFoundationDb`:

```csharp
var fdb = builder.AddFoundationDbCluster("fdb",
    apiVersion: 730,
    root: "/Tenant/ACME/MyApp/v1",
    clusterFile: "/path/to/testing.cluster");
```

This starts no container; it just passes the cluster file to the projects that reference it.

## Read the connection (each service)

In a service that references the cluster, register FoundationDB from the injected connection:

```csharp
var builder = WebApplication.CreateBuilder(args);

// standard Aspire wiring (telemetry, health checks, ...)
builder.AddServiceDefaults();

// "fdb" matches the name used in AddFoundationDb(...) in the AppHost.
builder.AddFoundationDb("fdb");

var app = builder.Build();
```

`AddFoundationDb("fdb")` reads the injected connection string and registers the `IFdbDatabaseProvider` singleton, configured for the local or external cluster the AppHost defined. From here you use it exactly as in [Getting Started](getting-started.md).

## Running the AppHost

The `aspire` CLI provisions the dashboard and ports for you:

```console
dotnet tool install --global aspire.cli    # one time
aspire run --apphost path/to/MyApp.AppHost.csproj
```

A plain `dotnet run` on the AppHost also works if you provide a `Properties/launchSettings.json` with the dashboard and OTLP endpoints. The `aspire` CLI does not need that file.

## Next

- [Getting Started](getting-started.md): the read and write API you use once the provider is registered.
- [Keys, Values & Layers](guide/keys-and-layers.md): how to model your data.
