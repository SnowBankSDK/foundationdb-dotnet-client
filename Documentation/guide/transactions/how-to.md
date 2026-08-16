# Transactions how-to guides

Each section here is one task against a FoundationDB transaction. They assume you already have an `IFdbDatabase` or `IFdbDatabaseProvider`, as [Getting started](../../getting-started.md) sets up. For the low-level transaction API see the [reference](reference.md); for why the loop retries and why a handler must be idempotent, see [the explanation](index.md).

## Run the retry loop

Do not call `BeginTransaction` and `CommitAsync` by hand in application code. Use the retryable methods on `IFdbDatabase` (or `IFdbDatabaseProvider`), and pick the narrowest one for the job:

| Method | Transaction | Use for |
|---|---|---|
| `db.ReadAsync(handler, ct)` | `IFdbReadOnlyTransaction` | reads only; returns a result |
| `db.WriteAsync(handler, ct)` | `IFdbTransaction` | mutations that return **nothing** (the handler may still read) |
| `db.ReadWriteAsync(handler, ct)` | `IFdbTransaction` | mutations that must **return a value** |

```csharp
// READ
Book? book = await db.ReadAsync(async tr =>
{
    var bytes = await tr.GetAsync(subspace.Key("D", id));
    return CrystalJson.Deserialize<Book>(bytes);   // empty/missing -> null
}, ct);

// WRITE (nothing to return)
await db.WriteAsync(tr => tr.Set(subspace.Key("D", book.Id), FdbValue.ToJson(book)), ct);

// READ-MODIFY-WRITE (need a result)
long balance = await db.ReadWriteAsync(async tr =>
{
    long current = (await tr.GetAsync(accountKey)).ToInt64();
    long updated = current + amount;
    tr.Set(accountKey, FdbValue.ToFixed64LittleEndian(updated));
    return updated;
}, ct);
```

The loop commits for you, so never call `CommitAsync` inside the handler. `WriteAsync` and `ReadWriteAsync` both hand you a full read/write transaction; the split is only about whether you return a value, not about whether you read. Keep every external side effect out of the handler: it can run more than once, and the earlier attempt's writes are discarded on a retry, so touch caches, counters, and logging only after the loop returns (see [why the handler must be idempotent](index.md#your-handler-must-be-idempotent)). Do not catch retryable `FdbException` codes such as a conflict inside the handler; the loop handles them. Throw your own exception out of the handler for a genuine application error: it aborts the transaction and propagates, with no commit and no retry.

## Increment a counter with atomic mutations

An atomic mutation changes a value on the cluster without reading it first, so it adds nothing to the read set and never conflicts with another atomic mutation, even under heavy contention. Reach for one instead of a read-modify-write whenever the new value is a function of the old one:

```csharp
tr.AtomicAdd64(counterKey, +1);            // value stored as fixed little-endian 64-bit
tr.AtomicIncrement64(counterKey);
tr.AtomicDecrement64(counterKey, clearIfZero: true);
tr.AtomicMax(key, v); tr.AtomicMin(key, v); tr.AtomicAnd/Or/Xor(key, mask);
```

An atomic write avoids the read-conflict, but a single frequently-written key still serializes its writers at the resolver; the [shard recipe](#spread-a-write-hot-key-across-shards) spreads that load.

## Read stale data with a snapshot read

A snapshot read returns a key's value without adding it to the transaction's read set, so a concurrent write to that key does not make this transaction conflict. Use `tr.Snapshot.GetAsync` or `tr.Snapshot.GetRange` when a slightly stale value is acceptable, such as counting shards or gathering statistics.

## Spread a write-hot key across shards

A single key that many transactions write serializes all of them at the resolver. Spread the writes across N sub-keys and add them up on read. `FdbHighContentionCounter` implements this pattern.

## Watch a key for changes

`tr.Watch(key, ct)` returns an `FdbWatch` that completes when the key's value changes after the transaction commits. Create it inside a transaction and `await` it **outside**:

```csharp
FdbWatch watch = await db.ReadWriteAsync(async tr => tr.Watch(signalKey, ct), ct);
await watch;   // resolves when signalKey changes
```

- Pass an **application/outer** `CancellationToken` to `Watch`, **not** `tr.Cancellation`: the watch outlives the transaction.
- A watch only **notifies** that the key changed; it does not deliver the new value. When it fires, re-read.
- Watches are limited per database, so use them for low-frequency signals, not high-throughput streaming.

The canonical use is a **signal-key fan-out**: a producer bumps a single watched key with `AtomicIncrement` in the same transaction as its data write; consumers watch that key and re-read when it fires. This is the backbone of pub/sub and change feeds (see [Advanced Layers](../advanced-layers/index.md)).

## Page a large range across transactions

A range scan that might be large must not run as one long read: it will hit the five-second limit and fail with `transaction_too_old`. Page it across transactions, resuming each one from the last key's `Successor()`. For a bulk import or export, use the `Fdb.Bulk.*` helpers, which manage the batching and the time window for you.

## Compose several layers in one transaction

A Layer resolves its per-transaction `State` inside the handler and uses it there. Resolving several layers in the same handler makes them commit together or not at all:

```csharp
await db.WriteAsync(async tr =>
{
    var books   = await bookStore.Resolve(tr);
    var counter = await statsCounter.Resolve(tr);
    books.Insert(tr, book);
    counter.Add(tr, 1);          // both commit atomically, or neither does
}, ct);
```

When `Resolve` needs an argument, most often a tenant, implement `IFdbLayer<TState, TOptions>`, whose `Resolve(tr, options)` and retry-loop helpers take that argument. For how to write a Layer, see [Keys, Values & Layers](../keys-and-layers/index.md).
