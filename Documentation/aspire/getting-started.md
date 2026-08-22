# Getting started with Aspire

This page starts a local FoundationDB cluster with .NET Aspire and connects one service to it. When
you are done, the service reads a live value from the cluster on startup, and the first run
configures the database for you. Ten minutes, one path. For the design behind the two packages and
the connection flow, read [the explanation](index.md); for single tasks like connecting to an
existing cluster or pinning a version, see the [how-to guides](how-to.md); for the full parameter
tables, see the [reference](reference.md).

This page assumes you already have an Aspire solution with an AppHost project (`Acme.AppHost`) and one
service project (`Acme.Backend`). If you do not, create one first with the
[Aspire templates](https://aspire.dev/), then come back. You also need Docker running, because the
AppHost starts the cluster in a container.

## 1. Install the SDK packages

The host package goes in the AppHost, the client package in the service. Both carry the library
version:

```console
# in Acme.AppHost
dotnet add package FoundationDB.Aspire.Hosting

# in Acme.Backend
dotnet add package FoundationDB.Aspire
```

Or add the references to your project files directly:

```xml
<!-- Acme.AppHost.csproj -->
<ItemGroup>
  <!-- other packages -->
  <PackageReference Include="FoundationDB.Aspire.Hosting" Version="7.4.2" />
</ItemGroup>

<!-- Acme.Backend.csproj -->
<ItemGroup>
  <!-- other packages -->
  <PackageReference Include="FoundationDB.Aspire" Version="7.4.2" />
</ItemGroup>
```

The native client (`FoundationDB.Client.Native`) comes in step 3, after you choose the cluster
version, because its version tracks the cluster and not these packages.

## 2. Declare the cluster in the AppHost

In `Acme.AppHost/Program.cs`, add the cluster and give the backend a reference to it:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Start a local single-node FoundationDB cluster in Docker.
// apiVersion is the API level the services request; clusterVersion is the Docker image tag;
// root is the directory-layer path the services resolve keys under.
var fdb = builder.AddFoundationDb("fdb",
    apiVersion: 740,
    root: "/Sandbox/Acme",
    clusterVersion: "7.4.6",
    rollForward: FdbVersionPolicy.Exact);

// Give the backend a reference to the cluster, and start it only once the cluster is healthy.
builder.AddProject<Projects.Acme_Backend>("backend")
    .WithReference(fdb)
    .WaitFor(fdb);

builder.Build().Run();
```

`AddFoundationDb` runs the cluster as a Docker container. `WithReference(fdb)` passes the connection
string to the backend under the resource name (`"fdb"`), and `WaitFor(fdb)` holds the backend until
the cluster reports healthy.

## 3. Install the native client to match the cluster

The cluster you declared is 7.4 (`clusterVersion: "7.4.6"`, `apiVersion: 740`). Install the native
client at that same `major.minor` in the service:

```console
# in Acme.Backend
dotnet add package FoundationDB.Client.Native --version "7.4.*"
```

Or add the reference to the service project file:

```xml
<!-- Acme.Backend.csproj -->
<ItemGroup>
  <!-- other packages -->
  <PackageReference Include="FoundationDB.Client.Native" Version="7.4.*" />
</ItemGroup>
```

The native client sets the output protocol, so its version tracks the cluster, not the SDK packages.
`FoundationDB.Aspire` stays at the library version (`7.4.2`), and `FoundationDB.Client.Native` follows
`clusterVersion`. If you later target a 7.3 cluster, set `apiVersion` to `730` and `clusterVersion` to
a `7.3.x` in step 2, and change this pin to `7.3.*`: the two always move together. A mismatch is the
usual reason a service connects and then times out on every operation; see
[How it connects](../foundationdb-101.md).

## 4. Read the connection in the service

In `Acme.Backend/Program.cs`, register FoundationDB from the injected connection, then add an endpoint
that reads from it:

```csharp
using FoundationDB.Client;
using FoundationDB.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// standard Aspire wiring (telemetry, health checks)
builder.AddServiceDefaults();

// "fdb" matches the name used in AddFoundationDb(...) in the AppHost.
// This registers the IFdbDatabaseProvider singleton.
builder.AddFoundationDb("fdb");

var app = builder.Build();

// Prove the connection: GetReadVersionAsync is a cheap round-trip, so a value here means it works.
app.MapGet("/readversion", async (IFdbDatabaseProvider db, CancellationToken ct) =>
{
    long readVersion = await db.ReadAsync(tr => tr.GetReadVersionAsync(), ct);
    return Results.Ok(new { readVersion });
});

app.Run();
```

`AddFoundationDb("fdb")` reads the injected connection string and registers the `IFdbDatabaseProvider`
singleton, already pointed at the cluster the AppHost started. From here the read and write API is the
same as [Getting Started](../getting-started.md); the endpoint above only reads the cluster's current
read version to prove the connection.

## 5. Run it

Start the AppHost with the `aspire` CLI, which provisions the dashboard and ports for you:

```console
dotnet tool install --global aspire.cli    # one time
aspire run --apphost Acme.AppHost/Acme.AppHost.csproj
```

The dashboard opens in your browser. On this first run the `fdb` resource starts on a fresh volume, so
the AppHost configures the database once and logs that it created it; the resource then turns healthy,
and the backend starts (it waited for the cluster). The whole first start is unattended: no manual
`fdbcli` step.

Open the backend's `/readversion` endpoint from the dashboard. You get a live number:

```json
{ "readVersion": 143602331405 }
```

That is the success: the read version is a value from the cluster, so a real connection happened.

Stop the AppHost and run it again. The second run reuses nothing by default (a fresh container and a
fresh volume), so it provisions again. To keep the container and its data across runs, add
`.WithLifetime(ContainerLifetime.Persistent)` to the cluster; then a later run finds the database
already configured and skips the step. The [how-to guides](how-to.md) cover that and the other common
tasks.

## Where to go next

You have a cluster Aspire starts for you and a service that connects to it through
`IFdbDatabaseProvider`.

- [How-to guides](how-to.md): connect to an existing cluster, reuse the container across runs, pin or
  roll the cluster version, turn off autoprovisioning.
- [Getting Started](../getting-started.md): the read and write API you use once the provider is
  registered.
- [What it is and why](index.md): the host and client split, the connection flow, and how a fresh
  cluster provisions itself.
