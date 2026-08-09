# Transaction Basics

A transaction is the type that lets you interact with the database, reading the values of keys and/or modifying them. This page is the low-level reference for the transaction API; for the practical rules of using transactions well (idempotency, the 5-second limit, conflicts, atomic operations, watches), read the [Transactions guide](guide/transactions.md).

There are two kinds of transaction:

- **Read-only** transactions can only read, and never need to commit. They obtain a *read version* from the cluster and perform all reads at that point in time.
- **Read/write** transactions can also mutate the database at commit time. If any key they read was changed concurrently by another transaction, they fail to commit with a conflict and must be retried.

These are exposed via `IFdbReadOnlyTransaction` and `IFdbTransaction` (which extends it), so the compiler helps you stay within what each allows.

## Manual transactions

You *can* create and manage transactions by hand. Note that `BeginTransaction`/`BeginReadOnlyTransaction` are synchronous, and `Set`/`Clear` stage mutations locally; nothing is durable until `CommitAsync()`:

```csharp
CancellationToken ct = /* ... */;

// read-only transaction
using (IFdbReadOnlyTransaction tr = db.BeginReadOnlyTransaction(ct))
{
    Slice value1 = await tr.GetAsync(key1);
    Slice value2 = await tr.GetAsync(key2);
    var values = await tr.GetRange(beginInclusive, endExclusive).ToListAsync();
}

// read/write transaction
using (IFdbTransaction tr = db.BeginTransaction(ct))
{
    Slice value1 = await tr.GetAsync(key1);   // we can read
    tr.Set(key2, value2);                     // stage a write
    tr.Clear(key3);                           // stage a delete
    tr.ClearRange(beginInclusive, endExclusive); // or delete a range

    await tr.CommitAsync();                   // nothing changes in the database until this succeeds
}
```

**Do not write application code this way.** Managing the transaction lifetime yourself, and deciding which errors are retryable and for how long, is error-prone:

- Conflicts between transactions are a normal, expected outcome for many algorithms; when they happen the transaction must be retried.
- Many transient errors can temporarily prevent a commit but would succeed on the next attempt.

## Retry loops (use these)

Every `IFdbDatabase` provides retry-loop helpers that handle all of the above for you: `ReadAsync`, `WriteAsync`, and `ReadWriteAsync`.

- `ReadAsync`: read-only transactions; any attempt to write throws.
- `WriteAsync`: read/write transactions that return no result (a `void`-like operation).
- `ReadWriteAsync`: read/write transactions that return a result.

The handler you pass is executed **at least once**; only the result of the last (successful) iteration is returned. On a retryable error the transaction is reset and the handler runs again; on a non-retryable error the loop aborts and rethrows. Every loop takes a caller-provided `CancellationToken`, often the only way to abort a transaction blocked on an outage.

```csharp
CancellationToken ct = /* ... */;

// read the value of a key
Slice result1 = await db.ReadAsync(tr => tr.GetAsync(key1), ct);

// change a key (no result)
await db.WriteAsync(tr => tr.Set(key1, value1), ct);

// read one key and change another, returning a value
Slice result2 = await db.ReadWriteAsync(tr =>
{
    tr.Set(key2, value2);
    return tr.GetAsync(key1);
}, ct);
```

> ⚠️ The handler can run more than once, so it **must not mutate state outside the database** (no caches, counters, logging, or messaging inside the lambda). Do that work after the loop returns. This and the rest of the rules are covered in the [Transactions guide](guide/transactions.md).

**Why both `WriteAsync` and `ReadWriteAsync`?** It's a C# type-resolution limitation: "write-only" handlers return `Task` and "read/write" handlers return `Task<T>` (down-castable to `Task`), which causes overload ambiguity. Separate names avoid it. By convention, anything named `Read…` returns a value.
