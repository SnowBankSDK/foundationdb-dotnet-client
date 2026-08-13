---
name: foundationdb-aspire
description: How to run a FoundationDB cluster and connect to it from .NET — getting the IFdbDatabaseProvider that the keys/transactions/layers skills assume you already have. Covers the ways to get a provider (plain DI services.AddFoundationDb, FdbDatabaseProvider.Create, or Aspire), the Aspire AppHost integration (FoundationDB.Aspire.Hosting — builder.AddFoundationDb starts a Docker cluster, AddFoundationDbCluster connects to an existing one), the Aspire client integration (FoundationDB.Aspire — builder.AddFoundationDb reads the injected connection), wiring with WithReference and WaitFor, launching with the aspire CLI vs plain dotnet run plus launchSettings, the native client (libfdb_c via UseNativeClient, the platforms FoundationDB.Client.Native ships, and the macOS system-library fallback), and the client-vs-cluster version-compatibility rule. Use whenever code opens or connects to a cluster, registers AddFoundationDb, runs an Aspire host, or hits libfdb_c load failures or transactions that hang on a fresh cluster.
---

# FoundationDB — Running a cluster & connecting from .NET (Aspire, the native client)

The **`foundationdb-keys-and-layers`**, **`foundationdb-transactions`**, and **`foundationdb-advanced-layers`** skills all start from an `IFdbDatabase` / `IFdbDatabaseProvider` you already have. This skill is about getting there: standing up a cluster, loading the native client correctly, and connecting — including the .NET Aspire integration shipped in this repo (`FoundationDB.Aspire.Hosting` + `FoundationDB.Aspire`).

---

## 1. Getting an `IFdbDatabaseProvider`

Application code resolves an **`IFdbDatabaseProvider`** (then `await provider.GetDatabase(ct)` or, more commonly, the retry-loop methods). Three ways to create one:

| Way | API | When |
|---|---|---|
| **Plain DI** | `services.AddFoundationDb(apiVersion, opt => …)` → `IFdbDatabaseProviderBuilder` (registers `IFdbDatabaseProvider`) | a regular host, you supply the cluster file via config |
| **No DI** | `FdbDatabaseProvider.Create(new FdbDatabaseProviderOptions { ApiVersion = 730, … })` | tools/tests/standalone; call `provider.Start()` |
| **Aspire client** | `builder.AddFoundationDb("fdb", settings, options)` | the connection string is injected by an Aspire AppHost (see §3) |

`apiVersion` selects the client API level and **caps the features you can use**: `730` ⇒ FoundationDB 7.3 semantics, `740` ⇒ 7.4. This client accepts **610 to 740**, and `Fdb.DefaultApiVersion` is **730**, which is why 730 appears in most examples. Pick the level whose semantics you actually rely on, not the newest available: raising it is a behavior change, and it must stay ≤ what the cluster supports. The provider also carries a **root** `FdbPath` (a directory-layer partition); everything else resolves relative to it.

Plain-DI / no-DI namespaces: `FoundationDB.DependencyInjection`. These all still need the **native client** loaded (§5).

---

## 2. The Aspire AppHost integration (`FoundationDB.Aspire.Hosting`)

On a `IDistributedApplicationBuilder` (the AppHost), namespace `Aspire.Hosting`:

```csharp
// starts a FoundationDB cluster as a Docker container, and emits a connection string named "fdb"
var fdb = builder.AddFoundationDb("fdb",
        apiVersion: 730,
        root: "/MyApp")                            // clusterVersion omitted on purpose - see below
    .WithLifetime(ContainerLifetime.Persistent);   // reuse the container across runs (faster restarts)

builder.AddProject<Projects.MyApp>("app")
    .WithReference(fdb)                            // injects the "fdb" connection string into the app
    .WaitFor(fdb);                                 // hold the app until the cluster is healthy
```

- **Prefer omitting `clusterVersion`.** Left out, the hosting package picks its own last-known-good Docker tag for the major it selects (the `FdbAspireHostingExtensions.LatestVersionNN` constants, refreshed from Docker Hub by `scripts/check-fdb-image-tags.ps1`). A tag you hardcode is a number that rots in your AppHost: pin one only when you need a specific image, and then treat it as something to review, alongside `rollForward: FdbVersionPolicy.Exact`.
- `AddFoundationDb(...)` → `IResourceBuilder<FdbClusterResource>` **runs a container**.
- **A fresh volume self-provisions.** On first start the integration runs `configure new single ssd` inside the container (a brand-new cluster has no database until then), and `WaitFor(fdb)` holds dependents until the database answers. An already-configured database is left untouched. Opt out with `.WithAutoProvisioning(false)`; run mode only.
- `AddFoundationDbCluster(name, apiVersion, root, clusterFile?, clusterVersion?)` → `IResourceBuilder<FdbConnectionResource>` instead **connects to an already-running cluster** (no container) given a cluster file.
- `FdbVersionPolicy` (`Exact`, etc.) lives in `Aspire.Hosting.ApplicationModel`. `.WithDefaults(timeout, retryLimit, tracing)` tunes the connection.

**AppHost project setup:** `Sdk="Aspire.AppHost.Sdk/<ver>"`; reference `FoundationDB.Aspire.Hosting` with `IsAspireProjectResource="false"` (it's a *compile* dependency for `AddFoundationDb`); reference each app project normally (those are launched as resources — their TFM need not match the AppHost).

---

## 3. The Aspire client integration (`FoundationDB.Aspire`)

In the **app** (not the AppHost), on an `IHostApplicationBuilder`, namespace `Microsoft.Extensions.Hosting`:

```csharp
// reads the "fdb" connection string injected by WithReference(fdb) and registers IFdbDatabaseProvider
builder.AddFoundationDb("fdb",
    settings => { /* FdbClientSettings: tracing, health checks, … */ },
    options  => options.UseNativeClient());        // load libfdb_c (see §5)
```

The app references `FoundationDB.Aspire`. The injected connection string looks like `ApiVersion=730;Root=/MyApp;ClusterFileContents=…;ClusterVersion=7.4.6` (the `ClusterVersion` is whatever tag the AppHost resolved, so do not read the one in this example as a recommendation). Standalone (no AppHost), provide the cluster file via configuration instead.

> Don't confuse the two `AddFoundationDb`s: the **AppHost** one (`IDistributedApplicationBuilder`, starts the container) vs the **client** one (`IHostApplicationBuilder`, consumes the connection). They have different receivers, so they never collide.

---

## 4. Launching the AppHost

**Preferred: the `aspire` CLI** — it provisions the dashboard, OTLP, and ports for you:

```bash
dotnet tool install --global aspire.cli          # one-time
aspire run --apphost path/to/MyApp.AppHost.csproj
# headless / CI: aspire run --apphost … --detach --non-interactive --format Json
#   → returns { appHostPid, dashboardUrl (with login token), logFile }
```

**Fallback: plain `dotnet run`** (or IDE F5) on the AppHost. This requires a **`Properties/launchSettings.json`** providing the dashboard + OTLP endpoints, e.g.:

```json
{ "profiles": { "http": { "commandName": "Project",
  "applicationUrl": "http://localhost:15200",
  "environmentVariables": {
    "DOTNET_DASHBOARD_OTLP_ENDPOINT_URL": "http://localhost:19200",
    "DOTNET_RESOURCE_SERVICE_ENDPOINT_URL": "http://localhost:20200",
    "ASPIRE_ALLOW_UNSECURED_TRANSPORT": "true" } } } }
```

Without it, a bare `dotnet run` on the AppHost crashes at startup: *"Failed to configure dashboard resource because ASPNETCORE_URLS … was not set / … OTLP endpoint must be provided."* The `aspire` CLI does not need this file.

---

## 5. The native client (`libfdb_c`) — `UseNativeClient()`

The .NET client talks to the cluster through the native **`libfdb_c`** library. `options.UseNativeClient(allowSystemFallback: false)` loads **only** the copy shipped by the `FoundationDB.Client.Native` package.

⚠️ **That package ships `libfdb_c` only for `linux-arm64`, `linux-x64`, and `win-x64` — there is no macOS build.** On macOS you must:

1. install a system `libfdb_c.dylib` (Homebrew `foundationdb`, or the official client package) **of a matching version (§6) and matching CPU arch** (an arm64 process cannot load an x64 dylib), and
2. call `UseNativeClient(allowSystemFallback: true)`.

The loader searches the **app's output directory** and the standard dyld paths — but **not** `/usr/local/lib` by bare name. The reliable fix is to place (or copy) `libfdb_c.dylib` next to the built app. Symptom when it's missing: `DllNotFoundException: Unable to load shared library 'fdb_c'`.

---

## 6. Client ⇄ cluster version compatibility (the silent hang)

The **client library version must be compatible with the cluster version.** A 7.3 client **cannot** talk to a 7.4 cluster.

- Symptom: transactions **hang** (no exception); `fdbcli` against the cluster reports *"One or more processes … incompatible"* and *"database is unavailable"*, even though the cluster is internally healthy.
- Fix: make the AppHost `clusterVersion` (the Docker image tag) match the `libfdb_c` available to the app. Check the client with `fdbcli --version`, the server with `docker exec <container> fdbcli --version`. If you did not pin a tag, the mismatch is almost always a **system** `libfdb_c` shadowing the redistributed one (see §5), not the container.
- The `apiVersion` (e.g. `730`) must be ≤ the cluster's supported level; `730` ⇒ 7.3, `740` ⇒ 7.4.

For talking to **multiple** cluster versions from one process, FoundationDB's multi-version client loads several external `libfdb_c` libraries — but the simplest demo/dev setup is: one cluster version, one matching client library.

---

## 7. Golden rules & gotchas

✅ **DO**
- Pick the provider source by context: Aspire client integration under an AppHost; plain `services.AddFoundationDb(...)` / `FdbDatabaseProvider.Create(...)` otherwise.
- Launch the AppHost with `aspire run`; keep a `launchSettings.json` only as the `dotnet run`/F5 fallback.
- Keep the cluster image and the client library on the **same major.minor** (7.4 with 7.4). Omitting `clusterVersion` lets the hosting package pair them for you against the `libfdb_c` that `FoundationDB.Client.Native` redistributes; pin it only when you supply your own client library, and then verify both with `fdbcli --version`.
- On macOS, ensure a matching-arch `libfdb_c.dylib` is loadable (next to the app) and use `UseNativeClient(allowSystemFallback: true)`.

⚠️ **GOTCHAS**
- **Transactions that hang on a brand-new cluster** are almost always a client/cluster **version mismatch**, not a deadlock — check versions first.
- **`DllNotFoundException: fdb_c`** ⇒ no native client found; on macOS the package ships none (see §5).
- **AppHost `dotnet run` crash about ASPNETCORE_URLS / OTLP** ⇒ missing `launchSettings.json`, or just use `aspire run`.
- **`AddFoundationDb` "ambiguous" / wrong overload** ⇒ you imported the wrong namespace; the AppHost call is on `IDistributedApplicationBuilder` (`Aspire.Hosting`), the client call on `IHostApplicationBuilder` (`Microsoft.Extensions.Hosting`).
- `WithLifetime(ContainerLifetime.Persistent)` keeps the cluster's data across runs — handy in dev, but remember to `docker rm -f` it when you want a clean slate.

---

## 8. File map

| What | Where |
|---|---|
| Aspire AppHost integration | `FoundationDB.Aspire.Hosting/FdbAspireHostingExtensions.cs` (`AddFoundationDb`, `AddFoundationDbCluster`, `FdbVersionPolicy`) |
| Aspire client integration | `FoundationDB.Aspire/FdbAspireComponentExtensions.cs` (`AddFoundationDb`), `FdbClientSettings.cs` |
| Plain DI / no-DI provider | `FoundationDB.Client/DependencyInjection/FdbDatabaseServiceCollectionExtensions.cs`, `Implementation/FdbDatabaseProvider.cs` |
| Native client loader | `FoundationDB.Client.Native/` (`FdbClientNativeExtensions.cs` → `UseNativeClient`; `runtimes/` holds the shipped libs) |
