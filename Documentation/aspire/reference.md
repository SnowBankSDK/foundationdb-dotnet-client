# Aspire reference

The types, methods, parameters, and defaults of the FoundationDB Aspire integration. For a first run
see [Getting started](getting-started.md); for single tasks see the [how-to guides](how-to.md); for
the design see [the explanation](index.md).

## Packages

| Package | Project | Provides |
|---|---|---|
| `FoundationDB.Aspire.Hosting` | AppHost | `AddFoundationDb`, `AddFoundationDbCluster`, and the cluster-resource modifiers |
| `FoundationDB.Aspire` | each service | `AddFoundationDb(connectionName)`, which registers `IFdbDatabaseProvider` |
| `FoundationDB.Client.Native` | each service | the native `fdb_c` client, pinned to the cluster's `major.minor` |

## AddFoundationDb

Defines a FoundationDB cluster that Aspire runs as a Docker container, and returns an
`IResourceBuilder<FdbClusterResource>`.

```csharp
IResourceBuilder<FdbClusterResource> AddFoundationDb(
    this IDistributedApplicationBuilder builder,
    string name,
    int apiVersion,
    string root,
    int? port = null,
    string? clusterVersion = null,
    FdbVersionPolicy? rollForward = null)
```

| Parameter | Meaning | Default |
|---|---|---|
| `name` | the resource name, reused by the service's `AddFoundationDb` | required |
| `apiVersion` | the API level the application requests, at or below the cluster version | required |
| `root` | the directory-layer root the application resolves paths against | required |
| `port` | the host port the container binds | `4550` |
| `clusterVersion` | the target image version (`"7.4.6"`, `"7.4.*"`, `"7.*"`) | derived from `apiVersion` |
| `rollForward` | how far a newer image may be selected | derived from `clusterVersion` |

An overload takes `root` as an `FdbPath` and adds `imageRegistry` (default `"docker.io"`).

## AddFoundationDbCluster

Defines a connection to an existing cluster from a cluster file. It starts no container, and returns an
`IResourceBuilder<FdbConnectionResource>`.

```csharp
IResourceBuilder<FdbConnectionResource> AddFoundationDbCluster(
    this IDistributedApplicationBuilder builder,
    string name,
    int apiVersion,
    string root,
    string? clusterFile = null,
    string? clusterVersion = null)
```

| Parameter | Meaning | Default |
|---|---|---|
| `name` | the resource name, reused by the service's `AddFoundationDb` | required |
| `apiVersion` | the API level the application requests | required |
| `root` | the directory-layer root the application resolves paths against | required |
| `clusterFile` | the path to the cluster file passed to referencing services | `null` |
| `clusterVersion` | the client library version to use | `null` |

An overload takes `root` as an `FdbPath`.

## Cluster resource modifiers

Applied to the builder that `AddFoundationDb` or `AddFoundationDbCluster` returns.

| Modifier | Applies to | Effect |
|---|---|---|
| `WithAutoProvisioning(bool enabled = true)` | `FdbClusterResource` | turns first-start provisioning on or off (on by default) |
| `WithClusterVersion(string version)` | `FdbConnectionResource` | sets the client library version |
| `WithLifetime(ContainerLifetime.Persistent)` | container resources (Aspire) | reuses the container and its volume across runs |
| `WithReference(fdb)` | a project (Aspire) | injects the cluster's connection string under its name |
| `WaitFor(fdb)` | a project (Aspire) | holds the project until the cluster reports healthy |

## FdbVersionPolicy

The roll-forward policy for the Docker image, in the `Aspire.Hosting.ApplicationModel` namespace.

| Value | Selects |
|---|---|
| `Exact` | the exact version requested |
| `Latest` | the latest version in the registry, compatibility not guaranteed |
| `LatestMajor` | the latest compatible version at or above the request, across majors |
| `LatestMinor` | the latest compatible minor at or above the request, within the major |
| `LatestPatch` | the latest patch within the requested minor |

## clusterVersion forms

The string form of `clusterVersion` sets the version and a default `rollForward`.

| Form | Example | Version | Default rollForward |
|---|---|---|---|
| exact | `"7.4.6"` | that version | `Exact` |
| patch wildcard | `"7.4.*"` | `7.4` | `LatestPatch` |
| minor wildcard | `"7.*"` | `7` | `LatestMinor` |
| omitted or `"*"` | (none) | derived from `apiVersion` | `LatestMajor` |

An `apiVersion` of level `740` maps to version `7.4` (the last digit of the level is normally `0`).

## AddFoundationDb (service)

Reads the injected connection named `connectionName` and registers the `IFdbDatabaseProvider`
singleton. In the `FoundationDB.Aspire` package.

```csharp
IHostApplicationBuilder AddFoundationDb(
    this IHostApplicationBuilder builder,
    string connectionName,
    Action<FdbClientSettings>? configureSettings = null,
    Action<FdbDatabaseProviderOptions>? configureProvider = null)
```

`configureSettings` adjusts the client settings read from configuration; `configureProvider` adjusts
the provider options. Both are optional.

## Fdb.Provisioning.EnsureDatabaseConfiguredAsync

Configures a fresh cluster's database and returns once it is available. In `FoundationDB.Client`; the
AppHost and the test harness both call it.

```csharp
Task EnsureDatabaseConfiguredAsync(
    FdbCliRunner runFdbCli,
    TimeSpan timeout,
    string configuration = "single ssd",
    TimeSpan? probeInterval = null,
    Action<string>? log = null,
    CancellationToken ct = default)
```

| Parameter | Meaning | Default |
|---|---|---|
| `runFdbCli` | a delegate that runs `fdbcli` with the given arguments and returns its exit code and output | required |
| `timeout` | the bound on the wait; the method throws if the database is not available in time | required |
| `configuration` | the argument to `configure new` | `"single ssd"` |
| `probeInterval` | the delay between availability checks | an internal default |
| `log` | a sink for progress lines | `null` |
| `ct` | the cancellation token | `default` |

The call is idempotent: an already-configured database is left untouched.

## Container defaults

| Setting | Value |
|---|---|
| host and container port | `4550` (both equal, Aspire proxy off) |
| image | `foundationdb/foundationdb` |
| image registry | `docker.io` |
| data volume | `fdb_data` mounted at `/var/fdb/data` |

## Telemetry

`FoundationDB.Aspire` adds `AddFoundationDbInstrumentation` to both `TracerProviderBuilder` and
`MeterProviderBuilder`, so a service's OpenTelemetry pipeline can collect the binding's traces and
metrics.
