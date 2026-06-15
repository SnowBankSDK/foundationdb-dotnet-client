# Getting Started

This page gets you from zero to a working read and write. For the full story on dependency injection, .NET Aspire, and deployment, see the [README](../README.md); for how to model your data, see the [Guide](guide/index.md).

## 1. Install the packages

```console
dotnet add package FoundationDB.Client
dotnet add package FoundationDB.Client.Native
```

`FoundationDB.Client` is the binding; `FoundationDB.Client.Native` redistributes the native `fdb_c` library for your platform (Windows/Linux/macOS, x64 and arm64). You also need access to a running FoundationDB cluster — for local development, the easiest route is the [.NET Aspire integration](../README.md#using-aspire), which spins up a local cluster in Docker.

> FoundationDB's native client is **64-bit only**, so your process must run as 64-bit.

## 2. Register the database (dependency injection)

```csharp
using FoundationDB.Client;

var builder = WebApplication.CreateBuilder(args);

// 730 = the API level; it must match your target cluster (here, FoundationDB 7.3+).
builder.Services.AddFoundationDb(730, options =>
{
    options.AutoStart = true;                 // connect on first use
    // options.ClusterFile = "/path/to/fdb.cluster";
    // options.Root = FdbPath.Parse("/Tenant/ACME/MyApp/v1");
});
```

This registers an `IFdbDatabaseProvider` you can inject anywhere.

## 3. Your first read and write

Always go through the **retry loop** (`ReadAsync` / `WriteAsync` / `ReadWriteAsync`) rather than managing transactions by hand — it handles FoundationDB's conflict-and-retry model for you.

```csharp
public sealed class HelloService(IFdbDatabaseProvider db)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var location = db.Root["Examples"]["Hello"];

        // write
        await db.WriteAsync(async tr =>
        {
            var subspace = await location.Resolve(tr);     // resolve the subspace inside the transaction
            tr.Set(subspace.Key("greeting"), FdbValue.ToTextUtf8("Hello, World!"));
        }, ct);

        // read
        string? greeting = await db.ReadAsync(async tr =>
        {
            var subspace = await location.Resolve(tr);
            var value = await tr.GetAsync(subspace.Key("greeting"));
            return value.IsNull ? null : value.ToStringUtf8();
        }, ct);
    }
}
```

A few things worth noticing, all explained in the Guide:

- You build the key with **`subspace.Key("greeting")`** and pass that object straight to the transaction — no manual byte encoding.
- You **resolve the subspace inside the transaction** (`location.Resolve(tr)`), never caching the prefix.
- The lambda you pass to `ReadAsync`/`WriteAsync` **may run more than once**, so it must not mutate state outside the database.

## Next steps

- [Keys, Values & Layers](guide/keys-and-layers.md) — how to model and encode your data, and how to package it into a Layer.
- [Transactions](guide/transactions.md) — the retry loop, conflicts, atomic operations, and watches.
- [Advanced Layers](guide/advanced-layers.md) — performance, the cluster model, and distributed patterns.
