---
name: foundationdb-transactions
description: >-
  How to correctly run transactions with the FoundationDB .NET client (FoundationDB.Client / SnowBank): the
  db.ReadAsync / WriteAsync / ReadWriteAsync retry loop and why a handler must be safe to run more than once,
  the 5-second and size limits, conflicts and how to avoid them, snapshot reads, explicit conflict ranges,
  atomic mutations (AtomicAdd32/64, AtomicIncrement, AtomicMin/Max and the lexicographic ByteMin/ByteMax,
  AtomicAnd/Or/Xor, AtomicCompareAndClear, AtomicAppendIfFits), and watches. Use whenever code opens a
  transaction or calls BeginTransaction, writes a read-modify-write, increments a counter, waits on a key with
  a watch, pages a large range scan across transactions, or hits an FdbException: NotCommitted,
  TransactionTooOld ("Transaction is too old to perform reads"), CommitUnknownResult, TransactionTimedOut,
  transaction_too_large. Also use it for "why does my transaction keep retrying / conflict / run twice", for
  high-contention or write-hot keys, and before deciding that a value must be read and written back. Pairs
  with the foundationdb-keys-and-layers skill.
---

# FoundationDB .NET — Transactions & the Retry Loop

FoundationDB gives you **serializable, ACID transactions** over the whole keyspace. The catch: a transaction may **conflict** and need to be retried, and it has hard limits (time and size). The .NET client handles retries for you via a **retry loop** — but only if you use it correctly. The single biggest source of bugs is writing a transaction handler that is **not safe to run more than once**.

If you are encoding keys/values inside the transaction, read the **`foundationdb-keys-and-layers`** skill too.

---

## 1. Always use the retry loop

Don't manually `BeginTransaction` / `CommitAsync` in application code. Use the retryable methods on `IFdbDatabase` (or `IFdbDatabaseProvider`). Pick the narrowest one:

| Method | Transaction type | Use for |
|---|---|---|
| `db.ReadAsync(handler, ct)` | `IFdbReadOnlyTransaction` | reads only; returns a result |
| `db.WriteAsync(handler, ct)` | `IFdbTransaction` | mutations that return **nothing** (the handler may still read — its transaction is a full read/write one) |
| `db.ReadWriteAsync(handler, ct)` | `IFdbTransaction` | mutations that must **return a value** out of the transaction |

> The split between `WriteAsync` and `ReadWriteAsync` is about the **return value**, not about whether you read. Both hand you a full `IFdbTransaction`. `ReadWriteAsync` has no "returns nothing" overload — if your handler returns no value, use `WriteAsync`.

```csharp
// READ
Book? book = await db.ReadAsync(async tr =>
{
    var bytes = await tr.GetAsync(subspace.Key("D", id));
    return bytes.IsNull ? null : CrystalJson.Deserialize<Book>(bytes);
}, ct);

// WRITE (no reads, nothing to return)
await db.WriteAsync(tr =>
{
    tr.Set(subspace.Key("D", book.Id), FdbValue.ToJson(book));
}, ct);

// READ-MODIFY-WRITE (need a result and/or read before write)
long newBalance = await db.ReadWriteAsync(async tr =>
{
    long current = (await tr.GetAsync(accountKey)).ToInt64();
    long updated = current + amount;
    tr.Set(accountKey, FdbValue.ToFixed64LittleEndian(updated));
    return updated;
}, ct);
```

The retry loop **commits for you** (you never call `CommitAsync` inside the handler) and re-runs the handler on retryable errors until it succeeds, the `CancellationToken` fires, or a non-retryable error is thrown.

There is a `state` overload (`db.ReadAsync(state, (tr, state) => …, ct)`) that lets you pass captured data without allocating a closure — prefer it in hot paths.

---

## 2. THE rule: your handler must be idempotent

> The handler lambda **can and will run multiple times.** Treat it as a pure function of the database state.

❌ **Never mutate external/global state inside the handler.** No incrementing in-memory counters, no adding to caches/lists, no logging "done", no sending messages, no `static` field writes. On a retry, those side effects happen again — but the earlier attempt's database writes were discarded.

✅ Do all such work **after** the loop returns successfully:

```csharp
// WRONG — _cache is mutated even on attempts that never commit
await db.WriteAsync(tr => { tr.Set(k, v); _cache[id] = book; }, ct);

// RIGHT — only touch external state after the transaction has committed
await db.WriteAsync(tr => tr.Set(k, v), ct);
_cache[id] = book;
```

The handler may read whatever it needs from the transaction; it just must not affect anything outside it. (See also the `success` callback overloads, which run once after a successful commit.)

---

## 3. Hard limits you must design around

| Limit | Value | Consequence |
|---|---|---|
| Transaction lifetime | **5 seconds** | Long reads/range scans fail with `past_version` (error 1007). Don't iterate huge ranges in one tx. |
| Value size | **100,000 bytes** | Split large blobs across keys (see `FdbBlob`). |
| Key size | **10,000 bytes** | Keep tuple keys reasonable. |
| Total writes per tx | **10,000,000 bytes** | Batch large imports across many transactions. |

For bulk operations that exceed these, use the **`Fdb.Bulk.*`** helpers (import/export/batch) instead of one giant transaction, and the `FdbKey.Batched(...)` helpers to split index ranges into chunks.

A range scan that might be large should be **paged** across transactions (resume from the last key's `Successor()`), not run as one 5-second read.

---

## 4. Conflicts & how to avoid them

A read-write transaction conflicts if another transaction commits a write to a key this transaction **read**, between this transaction's read version and commit. The retry loop hides the retry, but conflicts cost latency. To reduce them:

- **Use atomic mutations instead of read-modify-write** where possible — they don't create read conflicts:

  ```csharp
  tr.AtomicAdd64(counterKey, +1);          // value stored as fixed little-endian 64-bit
  tr.AtomicIncrement64(counterKey);
  tr.AtomicDecrement64(counterKey, clearIfZero: true);
  tr.AtomicMax(key, v); tr.AtomicMin(key, v);
  tr.AtomicAnd/Or/Xor(key, mask);
  ```
  (Counters stored for atomic add **must** be fixed-width little-endian: `FdbValue.ToFixed64LittleEndian` / `Slice.FromFixed64`.)

  ⚠️ **`AtomicMin` / `AtomicMax` compare LITTLE-ENDIAN, not lexicographically.** They also zero-extend or truncate the stored value to the length of your parameter first. That is correct for a fixed-width little-endian counter and **wrong for everything else**: on a tuple-encoded value, a UTF-8 string, a big-endian number or a `VersionStamp`, they will happily store the "larger" of two values under a comparison that has nothing to do with your ordering, and silently corrupt the key.

  For a byte-string ordering (which is what tuple-encoded keys, UUIDs and version stamps use), you want the lexicographic pair:

  ```csharp
  tr.Atomic(key, value, FdbMutationType.ByteMax);   // keep the lexicographically larger value
  tr.Atomic(key, value, FdbMutationType.ByteMin);   // keep the lexicographically smaller one
  ```

  There is deliberately **no `AtomicByteMax` / `AtomicByteMin` helper**: go through `tr.Atomic(...)` with the explicit `FdbMutationType`. Unlike `Min`/`Max`, these do no padding or truncation, and an absent key simply stores your parameter. They need **API level 520 or higher** (fdb 5.2, the same wave as `AppendIfFits`); below that the client throws `NotSupportedException` rather than degrading.

  Rule of thumb: fixed-width little-endian number, use `AtomicMin`/`AtomicMax`; anything you would compare with `Slice.CompareTo`, use `ByteMin`/`ByteMax`.

- **Snapshot reads** (`tr.Snapshot.GetAsync(...)`, `tr.Snapshot.GetRange(...)`) read without creating a read-conflict on those keys. Use them when a stale read is acceptable (e.g. counting shards, statistics). Don't use snapshot reads for values you then use to compute a write that needs consistency.

- **Sharding for write-hot keys**: a single frequently-incremented key serializes all writers. Spread writes across random sub-keys and sum on read — exactly what `FdbHighContentionCounter` does.

- You can add explicit conflict ranges with `tr.AddConflictRange(begin, end, FdbConflictRangeType.Read|Write)` when you need conflict behavior that differs from what your reads/writes imply (advanced).

---

## 5. Watches — reacting to changes

`tr.Watch(key, ct)` returns an `FdbWatch` that completes when the key's value changes after the transaction commits. Use it for change notification without polling. Create the watch inside a transaction (the handler is `async`; there is no synchronous return overload):

```csharp
FdbWatch watch = await db.ReadWriteAsync(
    async tr => tr.Watch(signalKey, ct),   // optionally read/set first, then return the watch
    ct);

await watch;   // resolves when signalKey's value changes after this tx commits
```

- ⚠️ Pass an **application/outer** `CancellationToken` to `Watch` — **not** the transaction's own `tr.Cancellation`. The watch outlives the transaction, so binding it to the transaction's token is rejected.
- A watch only **notifies** that the key changed — it does not deliver the new value. When it fires you must **re-read**.
- Watches are limited in number per database and should be used for low-frequency signals, not high-throughput streaming.

### The signal-key + watch pattern (producer/consumer)

This is how real layers (e.g. a pub/sub firehose) push work between nodes without polling:

- **Producer**, in the same transaction that writes the data, **bumps a single "signal" key** the consumer watches: `tr.AtomicIncrement32(subscriber.Key("WATCH"))`. `AtomicIncrement` guarantees the value changes (so the watch always fires) and never conflicts with other producers.
- **Consumer** loops: read a batch; if empty, return a watch on the signal key, `await` it **outside** the transaction, then loop and re-read.

```csharp
while (!ct.IsCancellationRequested)
{
    var (batch, watch) = await db.ReadWriteAsync(async tr =>
    {
        var sub = await location.Resolve(tr);
        // snapshot read: scanning the queue shouldn't conflict with producers
        var msgs = await tr.Snapshot.GetRangeAsync(sub.Key("INBOX").ToRange(), FdbRangeOptions.WantAll.WithLimit(100));
        if (msgs.Count == 0)
            return ((FdbRangeChunk?) null, (FdbWatch?) tr.Watch(sub.Key("WATCH"), ct));  // outer token!
        tr.ClearRange(msgs.First, FdbKey.Successor(msgs.Last));   // consume exactly what we read
        return (msgs, (FdbWatch?) null);
    }, ct);

    if (watch != null) { await watch; continue; }   // notified -> loop, re-read
    // dispatch batch...
}
```

Order messages with **commit-time VersionStamps** so they sort in publish order across all producers: `var stamp = tr.CreateVersionStamp(i); tr.SetVersionStampedKey(inbox.Key(stamp), payload);` (see the keys/layers skill).

---

## 6. Layers inside transactions

A **Layer** resolves its per-transaction `State` inside the handler and uses it there (see `foundationdb-keys-and-layers`). Two equivalent styles:

```csharp
// (a) Layer helper methods resolve State for you:
await store.WriteAsync(db, (tr, state) => state.Insert(tr, book), ct);

// (b) Resolve manually to compose several layers atomically in one tx:
await db.WriteAsync(async tr =>
{
    var books = await bookStore.Resolve(tr);
    var index = await authorIndex.Resolve(tr);
    books.Insert(tr, book);
    index.Add(tr, book.Author, book.Id);
}, ct);
```

Composing layers in one transaction is the core advantage: all writes commit together or not at all.

> The resolved `State` is valid **only** for the transaction that produced it. Because the handler may retry, never hoist `Resolve(tr)` out of the loop or stash the `State` in a field.

---

## 7. Errors

- `FdbException` carries an `FdbError` code. Retryable codes (conflicts, `past_version`, etc.) are handled by the retry loop automatically — don't catch and swallow them inside the handler.
- A `not_committed` (1020) conflict is normal under contention; it's retried for you.
- Throwing your **own** exception out of the handler aborts the transaction and propagates (no commit, no retry). Use this for genuine application errors.
- Don't catch `FdbException` *inside* the handler just to retry manually — that fights the loop.
- **`FdbErrorDebugger` is not for application code.** It exposes the native client's error
  translation (`GetErrorMessage`, `MapToException`, `TestErrorPredicate`) as a **test oracle**, for
  harnesses that need to pin what the real client answers. Calling
  `TestErrorPredicate(FdbErrorPredicate.Retryable, code)` to decide whether to retry re-implements
  the retry loop by hand: the loop already applies those predicates. Reach for it in a conformance
  suite, never in a handler.

---

## 8. Quick self-check before committing transaction code

- [ ] Am I using `db.ReadAsync/WriteAsync/ReadWriteAsync` (not manual begin/commit)?
- [ ] Is the handler free of external side effects (caches, counters, logging, messaging)? Side effects after the loop only.
- [ ] Did I avoid `await`-ing unrelated long work inside the handler (5-second budget)?
- [ ] For counters/aggregates, am I using atomic ops (and fixed-LE values) instead of read-modify-write?
- [ ] Are large ranges paged across transactions, and large values chunked?
- [ ] Are resolved layer `State`s confined to the handler?
