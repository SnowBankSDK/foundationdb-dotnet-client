# The Aspire integration: what it is and why

[.NET Aspire](https://aspire.dev/) starts a local FoundationDB cluster for you and hands its
connection to every service that needs it. This page explains what the integration is and why it is
shaped this way; for the step-by-step first run see [Getting started](getting-started.md), for the
task recipes see [How-to guides](how-to.md), and for the parameter and modifier tables see the
[reference](reference.md).

Without Aspire you start a container, wait for it, configure the database, and copy a cluster file
into each service by hand (the [Cluster setup](../cluster-setup.md) page walks that path). The Aspire
integration does all of it: the AppHost describes the cluster once, and each service reads its
connection from configuration.

## The host and client split

The integration ships as two packages, and each goes in a different kind of project:

- **`FoundationDB.Aspire.Hosting`** goes in the **AppHost**. It defines the cluster resource with
  `AddFoundationDb` (a container Aspire runs for you) or `AddFoundationDbCluster` (an existing
  cluster you connect to).
- **`FoundationDB.Aspire`** goes in **each service** that talks to the cluster. It reads the injected
  connection and registers the `IFdbDatabaseProvider` singleton the rest of your code resolves.

The split follows Aspire's own model: the AppHost is the orchestrator that knows every resource and
how they connect, and a service knows only its own configuration. A service never names a host, a
port, or a cluster file; it names the resource (`"fdb"`), and the AppHost supplies the rest.

## How the connection flows

One name ties the two sides together. `AddFoundationDb("fdb", ...)` in the AppHost declares a
resource called `"fdb"`. `WithReference(fdb)` on a project injects that resource's connection string
into the project under the same name. `AddFoundationDb("fdb")` in the service reads the connection
string back by that name and registers the provider:

```
AppHost:   AddFoundationDb("fdb", ...)          defines the cluster
           project.WithReference(fdb)           injects the "fdb" connection string
Service:   AddFoundationDb("fdb")               reads "fdb", registers IFdbDatabaseProvider
```

The connection string is the only thing that crosses the boundary, so the same service code connects
to a local container in development and to a production cluster in staging. Only the AppHost changes.
`WaitFor(fdb)` holds a dependent project until the cluster reports healthy, so a service does not
start against a database that cannot yet answer.

## A fresh cluster provisions itself

A FoundationDB database on a brand-new storage volume is not usable until it is configured once. Run
`status` against it and it reports "The database is unavailable"; a client that opens it waits, with
no error, for a configuration step that never comes. On a first run this reads as a hang: the AppHost
sits at near-zero CPU and nothing starts.

The integration removes that first-run trap. When the cluster container starts on a fresh volume, the
AppHost runs `configure new single ssd` inside it, then holds every resource that waits on the cluster
until the database answers. An already-configured database is left untouched, so a restart with a
persistent volume skips the step and logs that the database is already configured. The work is
idempotent and safe when two starters race; two AppHosts against one fresh volume converge on one
configured database, not two conflicting ones.

Two properties make this safe to leave on by default. The wait is **bounded**, not infinite: if the
database does not become available in time, the AppHost fails and names the manual
`fdbcli --exec "configure new single ssd"` recipe, rather than hanging in silence. And the happy path
**logs** that it configured the database, so a first run is never quiet. Turn the behavior off with
`WithAutoProvisioning(false)` on the cluster resource when you want to manage configuration yourself.

The same provisioning primitive backs the test harness. A freshly created FoundationDB test container
self-provisions its database on first start, so a test suite needs no manual `fdbcli` step.

## Local or external

Two AppHost methods cover the two cases, and a service cannot tell them apart:

- `AddFoundationDb(...)` runs a FoundationDB container from the `foundationdb/foundationdb` Docker
  image. Use it for local development. It needs Docker on the development machine.
- `AddFoundationDbCluster(...)` starts no container. It passes a cluster file you supply to the
  referencing projects. Use it for staging, production, or any cluster Aspire did not start.

Both inject a connection string under the resource name, so a service written against one works
against the other with no code change.

## Version compatibility

FoundationDB couples the native client to the cluster, and that version is separate from the SDK
packages. The native `fdb_c` client a service loads (`FoundationDB.Client.Native`) must match the
running cluster within a `major.minor` version, and the API version a service selects must be at or
below the cluster version. The AppHost picks the cluster version (`clusterVersion`) and the API
version (`apiVersion`) in one call, so those two agree; the service's native package is a separate pin
the developer keeps matching to that cluster version. The `FoundationDB.Aspire` and
`FoundationDB.Aspire.Hosting` packages carry the SDK's own version, unrelated to the cluster. The
[How it connects](../foundationdb-101.md) page covers the client and cluster versioning in full.

## Where it sits

The integration's job ends where the other guides begin: it registers the `IFdbDatabaseProvider` that
[Getting Started](../getting-started.md) and the [Guide](../guide/index.md) assume you already have.
From the provider you open the database and read and write exactly as those pages describe.

One detail leaks through in local development: Aspire maps the cluster to its own host port, not the
`4500` the plain-Docker walkthroughs use, so connect with the address Aspire prints in its dashboard
rather than a hard-coded one.
