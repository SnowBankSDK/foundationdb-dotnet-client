# Aspire how-to guides

Each section here is one task against the FoundationDB Aspire integration. They assume you already have
an AppHost that calls `AddFoundationDb` and a service that calls `AddFoundationDb`, as in
[Getting started](getting-started.md). For the parameter and modifier tables, see the
[reference](reference.md); for why the pieces fit together, see [the explanation](index.md).

## Connect to an existing cluster

For staging, production, or any cluster Aspire did not start, use `AddFoundationDbCluster` with a
cluster file instead of `AddFoundationDb`. It starts no container; it passes the cluster file to the
services that reference it:

```csharp
var fdb = builder.AddFoundationDbCluster("fdb",
    apiVersion: 730,
    root: "/Sandbox/Acme",
    clusterFile: "/etc/foundationdb/fdb.cluster");

builder.AddProject<Projects.Acme_Backend>("backend")
    .WithReference(fdb);
```

A service reads the connection the same way in both cases (`AddFoundationDb("fdb")`), so the service
code does not change between a local container and an external cluster.

## Reuse the cluster and its data across runs

By default `AddFoundationDb` creates a fresh container on every run, so the data does not survive a
restart. To keep the container and its volume, mark the cluster resource persistent:

```csharp
var fdb = builder.AddFoundationDb("fdb", apiVersion: 740, root: "/Sandbox/Acme", clusterVersion: "7.4.6")
    .WithLifetime(ContainerLifetime.Persistent);
```

A later run finds the same volume, so the database is already configured and the provisioning step is
skipped.

## Pin or roll the cluster version

`clusterVersion` selects the Docker image tag, and `rollForward` decides how far a newer image may be
taken. The string form of `clusterVersion` sets a default `rollForward` you can override:

```csharp
// exact image, never rolls forward
builder.AddFoundationDb("fdb", apiVersion: 740, root: "/Sandbox/Acme", clusterVersion: "7.4.6");

// latest 7.4 patch
builder.AddFoundationDb("fdb", apiVersion: 740, root: "/Sandbox/Acme", clusterVersion: "7.4.*");

// latest 7.x minor
builder.AddFoundationDb("fdb", apiVersion: 740, root: "/Sandbox/Acme", clusterVersion: "7.*");
```

Omit `clusterVersion` and the integration derives the version from `apiVersion` (level 740 gives 7.4)
and rolls forward to the latest compatible major. The [reference](reference.md#fdbversionpolicy) lists
every policy value and the default each version string implies.

## Turn off autoprovisioning

The integration configures a fresh database on first start. To manage that yourself, turn it off on
the cluster resource:

```csharp
var fdb = builder.AddFoundationDb("fdb", apiVersion: 740, root: "/Sandbox/Acme", clusterVersion: "7.4.6")
    .WithAutoProvisioning(false);
```

A fresh volume then has no database, and any service that opens it waits with no error until you
configure it by hand:

```console
docker exec <container> fdbcli --exec "configure new single ssd"
```

Leave autoprovisioning on unless you have a reason to run the configure step yourself; with it off, a
first run against a fresh volume hangs until the manual step runs.

## Match the native client to the cluster

A service loads the native client (`FoundationDB.Client.Native`), and its version must match the
running cluster within `major.minor`. Pin it in the service project to the cluster's version:

```console
dotnet add package FoundationDB.Client.Native --version "7.4.*"
```

Or in the service project file:

```xml
<ItemGroup>
  <!-- other packages -->
  <PackageReference Include="FoundationDB.Client.Native" Version="7.4.*" />
</ItemGroup>
```

`7.4.*` resolves to the latest 7.4 patch. This version tracks the cluster, not the SDK: the AppHost's
`clusterVersion` and the service's native package must agree on `major.minor`, while the
`FoundationDB.Aspire` package keeps its own library version. Change the cluster and you change this
pin: a 7.3 cluster (`apiVersion: 730`, `clusterVersion: "7.3.x"`) needs `FoundationDB.Client.Native`
at `7.3.*`. When the two disagree, the service connects and then times out on every operation.

## Choose the container's host port

`AddFoundationDb` binds the container to host port `4550` by default. Pass `port` to change it:

```csharp
builder.AddFoundationDb("fdb", apiVersion: 740, root: "/Sandbox/Acme", clusterVersion: "7.4.6", port: 4560);
```

The host port and the container port are always the same, and the Aspire proxy is off for this
resource: the FoundationDB node advertises its own port to clients, so a remapped host port would send
them to an address that does not exist. Pick a free port; do not expect Aspire to remap it.

## Run the AppHost without the aspire CLI

The `aspire` CLI provisions the dashboard and telemetry endpoints. A plain `dotnet run` on the AppHost
also works if you supply a `Properties/launchSettings.json` with the dashboard and OTLP endpoints that
the CLI would otherwise inject. Use the CLI unless you have a reason to manage those endpoints
yourself.

## Provision a database outside Aspire

The AppHost and the test harness both configure a fresh database through one primitive,
`Fdb.Provisioning.EnsureDatabaseConfiguredAsync`. Call it directly from a script or a custom harness
when you provision a cluster yourself. It takes a delegate that runs `fdbcli`, a timeout, and returns
once the database is available:

```csharp
await Fdb.Provisioning.EnsureDatabaseConfiguredAsync(
    runFdbCli,                       // your delegate: runs fdbcli, returns (exit code, output)
    timeout: TimeSpan.FromSeconds(30),
    ct: cancellationToken);
```

It is idempotent: an already-configured database is left untouched. If the database is not available
before the timeout, it throws rather than waiting forever. The test harness calls this on every
freshly created test container, so a test suite needs no manual `fdbcli` step.
