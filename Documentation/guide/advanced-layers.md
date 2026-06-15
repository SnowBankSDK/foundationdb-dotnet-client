# Advanced Layers

This guide is for building *sophisticated* layers: performance-sensitive code, and distributed patterns that span multiple nodes (change feeds, pub/sub, observable views). It assumes [Keys, Values & Layers](keys-and-layers.md) and [Transactions](transactions.md).

The recurring theme is that the right design falls out of understanding **how the cluster actually processes a transaction**. So we start there.

## How a transaction is processed

FoundationDB splits responsibilities across several roles (this is the published FoundationDB architecture; the constraints you live with are direct consequences of it):

| Role | Responsibility |
|---|---|
| **Coordinators** | Small Paxos group; elect the cluster controller and hold the cluster file. Clients bootstrap here. |
| **Cluster Controller** | Recruits/monitors every other role; drives recovery. |
| **Master / Sequencer** | Hands out **monotonically increasing versions** — read versions and commit versions. This is the global logical clock. |
| **GRV proxies** | Serve *get-read-version*: ask the master for the latest committed version and confirm the transaction logs are still live (so a read version is never stale after a recovery). Throttled by Ratekeeper. |
| **Commit proxies** | Drive commits: get a commit version from the master, send conflict ranges to the resolvers, make mutations durable on the transaction logs. |
| **Resolvers** | Hold the **last ~5 seconds of committed writes** in memory and compare a committing transaction's read-conflict ranges against them. This is where conflicts (`not_committed`, 1020) are decided. |
| **Transaction logs (tlogs)** | Durable, replicated write-ahead log; receive mutations in version order and only acknowledge once **fsync'd** on a quorum. |
| **Storage servers** | Hold the sharded, replicated data; keep ~5 seconds of mutations in memory plus an on-disk copy "as of 5 seconds ago"; serve reads via MVCC. |
| **Ratekeeper** / **Data Distributor** | Throttle transaction-start rate near saturation / keep shards balanced across storage servers. |

A **read-write transaction** flows like this:

1. **Get read version (GRV).** The first read fetches a read version from a GRV proxy — a recent committed version, quorum-confirmed.
2. **Reads** go *directly to the storage servers* at that version. The client caches the shard→server map and can issue reads in parallel. Read-conflict ranges accumulate client-side — unless you use snapshot reads.
3. **Writes** are buffered *in the client*; nothing hits the cluster yet.
4. **Commit.** The client sends mutations and conflict ranges to a commit proxy → it gets a commit version from the master → the resolvers check for conflicts → if clean, the mutations are made durable on the tlogs → the proxy acknowledges with the commit version (which is what fills your `VersionStamp`s).
5. Storage servers asynchronously pull and apply the committed mutations from the tlogs.

### Why the rules exist

- **Read version = the sequencer's clock.** It's the one notion of "now" that every node agrees on — which is exactly why it's the right basis for cross-node coordination (and why local wall clocks are not; see *The global clock* below).
- **`VersionStamp` = the commit version.** Globally ordered and monotonic — ideal for logs and feeds.
- **Conflicts = resolver verdicts** on read-conflict ranges. Snapshot reads (no read-conflict added) and atomic operations (no read at all) avoid them.
- **The 5-second limit = the MVCC window** the resolvers and storage servers retain. A read version older than that yields `transaction_too_old`. It's also why a recovery "fast-forwards" time and aborts in-flight transactions. Keep transactions short; page long scans across many of them.
- **Reads scale horizontally** across storage servers; **commits funnel** through proxies → resolvers → tlogs. So read-heavy workloads scale easily, while commit throughput is the thing to economize: keep write sets small and batch writes.

## Performance: minimize round-trips

The native client **pipelines** concurrent requests, so the enemy of latency is a **serial data dependency** — reading a key, inspecting the result, and only then issuing the next read. Each such hop is a full client↔cluster round-trip that cannot be hidden.

```csharp
// ❌ N round-trips — each await blocks on the previous
foreach (var id in ids) results.Add(await tr.GetAsync(subspace.Key(id)));

// ✅ one batched multi-read
Slice[] values = await tr.GetValuesAsync(ids.Select(id => subspace.Key(id)));

// ✅ or issue independent reads concurrently so they pipeline into ~one round-trip
Slice[] vs = await Task.WhenAll(tr.GetAsync(k1), tr.GetAsync(k2), tr.GetAsync(k3));
```

`tr.GetValuesAsync(keys)` reads many independent keys in one batch (it's exactly what a document store's "fetch these metadata keys" does). For ranges, `GetRangeAsync(range, options)` returns a page per round-trip — tune `FdbRangeOptions` (`WantAll`, `WithLimit`, streaming mode) to the access pattern.

The most valuable habit is to **collapse "read → decide → read" dependencies.** If you read key A only to decide whether/how to read B, ask whether the information can be *encoded* so a single read carries it. (The change feed below does exactly this: rather than "read a trim marker, then range-read the feed," the trim signal is a tombstone *inside* the feed, so one `GetRange` returns both the data and the signal.) When you genuinely can't, issue both in parallel with `Task.WhenAll` and discard the wasted one in the rare case.

Other levers: GRV has a real cost (sequencer + proxy quorum, rate-limited), so don't split work into needless tiny transactions; use snapshot reads where staleness is fine; keep keys and values small; prefer compact internal ids over repeating long keys.

## High contention

Because conflicts are decided at the resolvers on read-conflict ranges, a key that many transactions read-then-write becomes a hotspot. Avoid it with **atomic mutations** (no read, no conflict), **snapshot reads** (no read-conflict), and **sharding** of write-hot keys across many sub-keys that you aggregate on read. A single global counter is a guaranteed bottleneck; `FdbHighContentionCounter` shows the sharded alternative.

## The global clock

The sequencer is the only source of "now" that every node agrees on. Use it; never use node-local wall clocks for cross-node decisions.

- `tr.GetReadVersionAsync()` gives the read version — a monotonic, cluster-wide logical clock. Use it for leases, ordering, and "as-of" reasoning.
- `tr.CreateVersionStamp()` + `SetVersionStampedKey/Value` give the commit version — for ordered logs and feeds.

Two traps, both real:

1. **Local wall clocks have no shared "now."** Comparing a timestamp minted on one node against another node's `DateTime.UtcNow` is meaningless — skew, drift, NTP steps, and VM pauses make it like comparing times across relativistic frames. A node with a fast clock evicts live peers; one with a slow clock never evicts dead ones.
2. **The version tick-rate is not constant** (~1,000,000/s, but it drifts and slows when the cluster is idle). So do **not** convert a version delta into a duration. Instead, store a database-sourced token and test it for **change** (equality), and measure elapsed time only as the gap between an observer's *own* consecutive local reads.

A shared clock removes *skew*, but not the fundamental **failure-detector impossibility**: you can never be certain whether a peer is slow or dead. Liveness is therefore always a policy (a threshold) backed by **evict-and-resync**, not a proof.

## Capstone: building a change feed

A change feed lets other nodes observe a stream of changes and maintain an in-memory view of remote state. It composes every primitive above. A full, compile-checked implementation lives in [`samples/SkillValidation/BookStore.ChangeFeed.cs`](../../samples/SkillValidation/BookStore.ChangeFeed.cs).

**1. Append and signal, in the mutation's own transaction.** Every mutation appends a change under a commit-ordered `VersionStamp` and bumps one watched signal key — all in the same transaction as the data write, so the feed can never disagree with the data:

```csharp
var stamp = tr.CreateUniqueVersionStamp();
tr.SetVersionStampedKey(subspace.Key(SUBSPACE_FEED, stamp), FdbValue.ToJson(change));
tr.AtomicIncrement64(subspace.Key(SUBSPACE_SIGNAL));   // wake every subscriber; conflict-free
```

**2. Subscribe by streaming from a cursor.** The consumer reads pages of changes after its cursor; when caught up, it watches the signal key, awaits the watch outside the transaction, then re-reads. The `VersionStamp` of the last entry is the resume cursor. Expose this as an `IAsyncEnumerable<T>` and wrap it thinly as a `Channel<T>` or a callback.

**3. Retention, with liveness that doesn't compare clocks.** A version-stamped log grows forever, so a GC must trim it — but only up to what every *live* subscriber has consumed. "Live" is decided without comparing clocks: each subscriber renews a database-sourced token on a local interval; an observer reads those tokens on *its own* local interval and evicts a subscriber whose token hasn't **changed** across several polls. ("Unchanged for N polls" ≈ "N × the observer's own local delay" — an equality check plus a local elapsed-time measurement, never a cross-node timestamp comparison.) The observer's reads are non-snapshot, so a subscriber that renews concurrently conflicts the GC and is spared.

**4. Fencing: detecting "I fell behind" in one round-trip.** A subscriber frozen long enough is evicted and the GC reclaims past its cursor — it has now *missed changes* and its view is untrustworthy. It must be told. The efficient signal is a **tombstone**: when the GC reclaims up to a horizon, it leaves a single empty-value entry at the horizon's versionstamp. A resuming subscriber whose cursor is older than the horizon reads that tombstone *first* in its normal range read — an empty value deserializes to `null` (a real change is always non-null), so it's detected with **no extra read**. The subscriber throws a typed `ChangeFeedOutOfSyncException`, which the consumer catches to **reload current state and re-subscribe from "now."**

This is the same contract as Kafka's `OffsetOutOfRange` or DynamoDB Streams' `TrimmedDataAccessException`: you cannot *prevent* a too-slow consumer from missing data, but you can detect it cleanly and force a resync.

## A review checklist for distributed layers

- No serial *read → decide → read* chains on the hot path — batched, parallel, or encoded into one read?
- Independent reads issued concurrently, never `await`-ed in a loop?
- Write-hot keys sharded or using atomics; snapshot reads where serialization isn't needed?
- Long scans paged across transactions; large values chunked; bulk via `Fdb.Bulk.*`?
- Cross-node time uses the database clock (read version / versionstamp), never local wall clocks?
- Liveness via token change-detection plus local inter-poll elapsed time, not version-to-duration math?
- Unbounded logs/feeds have a retention path, and consumers can detect a gap and resync?
- Transaction handlers still idempotent; resolved layer `State` confined to the transaction?
