# Getting Started

This page takes you from a running cluster to your first read and write. It assumes you have already:

- installed the [prerequisites](prerequisites.md) (.NET 10, plus Docker if you need a local cluster), and
- obtained a cluster and its connection string from [Cluster setup](cluster-setup.md).

If you skipped those, start there. The examples below use the local Docker cluster from Cluster setup (`docker:docker@127.0.0.1:4500`, FoundationDB 7.4). Using your own cluster instead? Swap the connection string and match the API level (see [Cluster setup](cluster-setup.md)).

> Runnable versions of the samples on this page are in [`samples/getting-started/`](../samples/getting-started/).

## 1. Install the packages

Install the managed binding (always use the latest):

```console
dotnet add package FoundationDB.Client
```

Then install the native client, **pinned to your cluster's `major.minor` version**. This is the step that catches people: the native package sets the wire protocol and must match your cluster, or nothing connects (see [How it connects](foundationdb-101.md)).

For a **7.4** cluster (including the local Docker one from [Cluster setup](cluster-setup.md)):

```console
dotnet add package FoundationDB.Client.Native --version "7.4.*"
```

For a **7.3** cluster:

```console
dotnet add package FoundationDB.Client.Native --version "7.3.*"
```

`7.4.*` resolves to the latest `7.4.x` release (and `7.3.*` to the latest `7.3.x`), so you get the newest patch for your cluster's version without pinning an exact build. If you do not know which version your cluster is, [Cluster setup](cluster-setup.md) shows how to find out. Installing the wrong one is the single most common reason the tutorial below "connects" but then times out on every operation.

> FoundationDB's native client is **64-bit only**, so your process must run as 64-bit (the default on modern runtimes).

## 2. It works: connect and read the version

Start with a console app. This is the smallest program that proves your setup end to end: it connects and asks the cluster for its current read version.

A console app also needs the dependency-injection container package (a web app, later, gets it from the web SDK):

```console
dotnet add package Microsoft.Extensions.DependencyInjection
```

```csharp
using FoundationDB.Client;
using FoundationDB.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Register FoundationDB in the DI container. 740 is the API level: keep it at or
// below your cluster's version (see "How it connects").
services.AddFoundationDb(740, options =>
{
    // The coordinators to connect to. This is the connection string from "Cluster setup".
    options.ConnectionOptions.ConnectionString = "docker:docker@127.0.0.1:4500";
});

using var provider = services.BuildServiceProvider();

// The one service you use to talk to the database. In a real app you inject it instead of resolving it by hand.
var db = provider.GetRequiredService<IFdbDatabaseProvider>();

// ReadAsync runs the lambda in a read transaction and retries it on conflicts.
// GetReadVersionAsync is a cheap round-trip to the cluster, so a value here means the connection works.
long readVersion = await db.ReadAsync(tr => tr.GetReadVersionAsync(), CancellationToken.None);
Console.WriteLine($"Connected. Cluster read version = {readVersion}");
```

You should see a line like:

```
Connected. Cluster read version = 143602331405
```

That is your "it works" moment: the read version is a live value from the cluster, so a real connection happened.

If instead it **hangs for a few seconds and then times out**, your app is not talking to the cluster. The usual cause is a version mismatch between the native package and the cluster: check [How it connects](foundationdb-101.md), and confirm the connection string from [Cluster setup](cluster-setup.md).

## 3. Your first read and write

Always go through the **retry loop** (`ReadAsync` / `WriteAsync`) rather than managing transactions by hand: it handles FoundationDB's conflict-and-retry model for you. Continuing with the same `db`:

```csharp
// A directory location: a readable path the Directory layer maps to a short binary key
// prefix. Nothing is stored yet; you resolve it inside each transaction.
var location = db.Root["Examples"]["Hello"];

// WriteAsync runs the lambda in a read-write transaction and commits it, retrying on conflicts.
await db.WriteAsync(async tr =>
{
    // Create the directory the first time, or open it if it already exists, inside the
    // transaction. (Resolve, used for the read below, only opens an existing directory and
    // throws if it is missing.) Do not cache the subspace.
    var subspace = await location.CreateOrOpenAsync(tr);

    // subspace.Key(...) builds a tuple-encoded key under the prefix, and FdbValue.ToTextUtf8
    // encodes the value. Hand both straight to the transaction; no manual byte work.
    tr.Set(subspace.Key("greeting"), FdbValue.ToTextUtf8("Hello, World!"));
}, CancellationToken.None);

string? greeting = await db.ReadAsync(async tr =>
{
    // resolve this location to its key subspace
    var subspace = await location.Resolve(tr);

    // Read one key. ToStringUtf8() returns null if the key is missing (a nil slice).
    var value = await tr.GetAsync(subspace.Key("greeting"));
    return value.ToStringUtf8();
}, CancellationToken.None);

Console.WriteLine($"Read back: {greeting}");
```

Three things worth noticing, all explained in the [Guide](guide/keys-and-layers/index.md):

- You build the key with **`subspace.Key("greeting")`** and pass that object straight to the transaction. No manual byte encoding.
- You **resolve the subspace inside the transaction** (`location.Resolve(tr)`) and never cache the prefix.
- The lambda you pass to `ReadAsync` / `WriteAsync` **may run more than once**, so it must not change state outside the database.

Storing the data under `db.Root["Examples"]["Hello"]` rather than at a raw key is deliberate: the Directory layer keeps your keyspace tidy and collision-free, instead of scattering loose keys at the root. That is the first step towards "thinking in layers", covered next.

> **Resist the urge to build keys by hand.** Do not format keys as raw `byte[]`, UTF-8-encoded strings, or hand-assembled `Slice`s. Always compose them from directory subspaces, tuples, and the typed key helpers (`subspace.Key(...)`), which get ordering and escaping right for you. The [Keys, Values & Layers](guide/keys-and-layers/index.md) guide explains why this matters and how the encoding works.

## 4. A small HTTP API you can click through

Because the database is a singleton `IFdbDatabaseProvider` in DI, you can inject it into endpoints. Let's turn the greeting into a small collection you can create, list, and delete over HTTP, with an interactive UI so you can try it without writing a client.

Two extra packages give you that UI: the built-in OpenAPI document generator, and [Scalar](https://github.com/scalar/scalar), which renders it as a clickable dashboard.

```console
dotnet add package Microsoft.AspNetCore.OpenApi
dotnet add package Scalar.AspNetCore
```

```csharp
using FoundationDB.Client;
using FoundationDB.DependencyInjection;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFoundationDb(740, options =>
{
    options.ConnectionOptions.ConnectionString = "docker:docker@127.0.0.1:4500";
});

// describe the endpoints in the OpenAPI format
builder.Services.AddOpenApi();

var app = builder.Build();

// Create the demo directory once at startup, so every endpoint below can just Resolve it.
{
    var startup = app.Services.GetRequiredService<IFdbDatabaseProvider>();
    await startup.WriteAsync(tr => startup.Root["Examples"]["Greetings"].CreateOrOpenAsync(tr), CancellationToken.None);
}

// serve the OpenAPI document at /openapi/v1.json
app.MapOpenApi();

// render it as a clickable UI at /scalar/v1
app.MapScalarApiReference();

// Liveness: proves the app can reach the cluster.
app.MapGet("/readversion", async (IFdbDatabaseProvider db, CancellationToken ct) =>
{
    long readVersion = await db.ReadAsync(tr => tr.GetReadVersionAsync(), ct);
    return Results.Ok(new { readVersion });
});

app.MapPost("/greetings", async (NewGreeting input, IFdbDatabaseProvider db, CancellationToken ct) =>
{
    // Generate the id OUTSIDE the retry loop, so a retry reuses the same id.
    var id = Guid.NewGuid().ToString("N");
    var location = db.Root["Examples"]["Greetings"];
    await db.WriteAsync(async tr =>
    {
        // resolve the subspace where the Greetings collection's keys are stored
        var subspace = await location.Resolve(tr);
        tr.Set(subspace.Key(id), FdbValue.ToTextUtf8(input.Text));
    }, ct);
    return Results.Created($"/greetings/{id}", new Greeting(id, input.Text));
});

app.MapGet("/greetings", async (IFdbDatabaseProvider db, CancellationToken ct) =>
{
    var location = db.Root["Examples"]["Greetings"];
    var greetings = await db.ReadAsync(async tr =>
    {
        var subspace = await location.Resolve(tr);

        // scan the whole subspace: decode each key back into its id, read the text from the
        // value, and ToListAsync() pulls the matches into a List
        return await tr.GetRange(subspace.ToRange())
            .Select(kv => new Greeting(subspace.DecodeLast<string>(kv.Key)!, kv.Value.ToStringUtf8()!))
            .ToListAsync();
    }, ct);
    return Results.Ok(greetings);
});

app.MapDelete("/greetings/{id}", async (string id, IFdbDatabaseProvider db, CancellationToken ct) =>
{
    var location = db.Root["Examples"]["Greetings"];
    await db.WriteAsync(async tr =>
    {
        var subspace = await location.Resolve(tr);
        // remove a single key
        tr.Clear(subspace.Key(id));
    }, ct);
    return Results.NoContent();
});

app.Run();

record NewGreeting(string Text);
record Greeting(string Id, string Text);
```

Run the app and open **`/scalar/v1`** in your browser. You get a dashboard listing every endpoint: `POST` a greeting or two, `GET /greetings` to see them come back from FoundationDB, then `DELETE` one by its id. No `curl`, no separate client.

## Where next

You now have a working connection, a read, a write, and an HTTP endpoint. Two directions from here:

- **Think in layers.** [Keys, Values & Layers](guide/keys-and-layers/index.md) is the most important thing to learn next: how keys are tuple-encoded, how subspaces and the Directory layer organize the keyspace, and how to package data access into a reusable Layer. Then [Transactions](guide/transactions/index.md) for the retry loop, conflicts, and atomic operations.
- **A production-shaped setup.** Let .NET Aspire start the cluster and inject the connection for you, alongside a proper API backend. See [Aspire](aspire/index.md).
