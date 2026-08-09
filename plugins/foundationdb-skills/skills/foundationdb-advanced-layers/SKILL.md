---
name: foundationdb-advanced-layers
description: Advanced engineering for sophisticated FoundationDB layers with the .NET client (FoundationDB.Client / SnowBank) — the cluster model and transaction lifecycle (proxies, resolvers, tlogs, storage servers, the sequencer/version clock), latency/throughput optimization (batching reads with GetValuesAsync / Task.WhenAll, removing round-trip dependencies, snapshot reads), high-contention avoidance, and distributed patterns (change feeds, version-stamp logs, watch fan-out, version-as-clock leases, retention, fencing/tombstones). Use when building or reviewing a performance-sensitive or distributed layer (a change feed, queue, pub/sub, worker pool, multi-node observable view), tuning transaction latency/throughput, or reasoning about conflicts, the 5-second limit, or cross-node liveness. Builds on the foundationdb-keys-and-layers and foundationdb-transactions skills — read those first.
---

# FoundationDB .NET — Advanced Layer Engineering

This is the *advanced* tier. The **`foundationdb-keys-and-layers`** skill (key encoding, subspaces, the `IFdbLayer<TState>` pattern) and **`foundationdb-transactions`** skill (retry loop, idempotency, atomics, watches) are prerequisites — this skill assumes them and explains the *why* underneath, plus the patterns for performant, distributed, multi-node layers.

A fully worked, compile-checked reference for everything in §5–§6 lives in [`samples/SkillValidation/BookStore.cs`](../../../samples/SkillValidation/BookStore.cs) + [`BookStore.ChangeFeed.cs`](../../../samples/SkillValidation/BookStore.ChangeFeed.cs).

---

## 1. The cluster model — how a transaction is actually processed

*(FoundationDB's published architecture; the constraints below fall out of it directly.)*

| Role | Responsibility |
|---|---|
| **Coordinators** | Small Paxos group; elect the cluster controller, hold the cluster file. Clients bootstrap here. |
| **Cluster Controller** | Singleton; recruits/monitors all other roles, drives recovery. |
| **Master / Sequencer** | Hands out **monotonically increasing versions** — read versions and commit versions. **The global logical clock.** |
| **GRV proxies** | Serve *get-read-version*: ask the master for the latest committed version, confirm the tlogs are still live (so a read version is never stale after a recovery); throttled by Ratekeeper. |
| **Commit proxies** | Drive commits: get a commit version from the master, send conflict ranges to resolvers, make mutations durable on the tlogs. |
| **Resolvers** | Hold the **last ~5 s of committed writes** in memory; compare a committing tx's read-conflict ranges against them → this is where **conflicts** (`not_committed`, 1020) are decided. |
| **Transaction Logs (tlogs)** | Durable, replicated WAL; receive mutations in version order and only ack once **fsync'd** on a quorum. |
| **Storage servers** | Hold the sharded, replicated data; keep ~5 s of mutations in memory + on-disk data "as of 5 s ago"; serve reads via MVCC. |
| **Ratekeeper** / **Data Distributor** | Singletons: throttle transaction start rate near saturation / keep shards balanced across storage servers. |

**Lifecycle of a read-write transaction:**
1. **GRV** — first read fetches a *read version* from a GRV proxy (the recent committed version, quorum-confirmed).
2. **Reads** go *directly to storage servers* at that read version (the client caches the shard→server map and can issue reads in parallel). Read-conflict ranges accumulate client-side — unless you use **snapshot** reads.
3. **Writes** are buffered *client-side* — nothing hits the cluster until commit.
4. **Commit** — the client sends mutations + conflict ranges to a commit proxy → it gets a *commit version* from the master → **resolvers** check conflicts → if clean, mutations are made durable on the **tlogs** → ack with the commit version (which fills your **VersionStamps**).
5. Storage servers asynchronously pull and apply the mutations from the tlogs.

**Why the rules you already follow exist:**
- **Read version = the sequencer's clock** → it's the one sound *shared* clock across nodes (see §4).
- **VersionStamp = the commit version** → a globally ordered, monotonic id (see §5).
- **Conflicts = resolver verdicts** on read-conflict ranges → snapshot reads and atomics avoid them (§3).
- **The 5-second limit = the MVCC window** (resolver memory + storage-server history). A read version older than ~5 s ⇒ `transaction_too_old` (1007). It's also why a recovery "fast-forwards 90 s" and aborts in-flight transactions. → keep transactions short; page long scans across many transactions.
- **Reads scale horizontally** (storage servers); **commits funnel** through proxies→resolvers→tlogs. → read-heavy is cheap; commit throughput is the bottleneck, so batch writes and keep write sets small.

---

## 2. Performance: minimize round-trips

The native client **pipelines** concurrent requests. The enemy of latency is a **serial data dependency** — code that reads, inspects the result, then reads again. Each such hop is a full client↔cluster round-trip that cannot be hidden.

**Batch independent reads — never `await` them in a loop:**

```csharp
// ❌ N round-trips (each await blocks on the previous)
foreach (var id in ids) results.Add(await tr.GetAsync(subspace.Key(id)));

// ✅ one batched multi-read
Slice[] values = await tr.GetValuesAsync(ids.Select(id => subspace.Key(id)));   // GetValuesAsync<TKey>(...)

// ✅ or issue concurrently and let them pipeline into ~one round-trip
Slice[] vs = await Task.WhenAll(tr.GetAsync(k1), tr.GetAsync(k2), tr.GetAsync(k3));
```

`tr.GetValuesAsync(keys)` reads many independent keys in one logical batch (this is what a document store's metadata fetch uses). For ranges, `GetRangeAsync(range, options)` returns a page per round-trip — tune `FdbRangeOptions` (`WantAll`, `WithLimit`, streaming mode) to your access pattern.

**Collapse read→decide→read dependencies.** If you find yourself reading key A only to decide whether/how to read B, ask whether the information can be encoded so a *single* read carries it. (The change-feed in §5 does exactly this: instead of "read the trim marker, then range-read the feed," the trim signal is a **tombstone inside the feed**, so one `GetRange` returns both the data and the eviction signal — see §5.4.) If you genuinely can't, issue both in parallel with `Task.WhenAll` and discard the wasted one in the rare case.

**Other levers:**
- **GRV has a cost** (sequencer + proxy quorum, Ratekeeper-throttled). One transaction amortizes it across all its reads; a flood of tiny transactions pays it repeatedly. Reuse the read version within a transaction; don't split work into needless transactions.
- **Snapshot reads** (`tr.Snapshot.GetAsync/GetRange`) skip read-conflict tracking — cheaper *and* conflict-free; use when a slightly stale read is acceptable.
- **Bulk** import/export/scan via `Fdb.Bulk.*` (it manages batching and the 5-second window for you).
- Keep keys and values small (every byte rides the tlog/storage path); prefer compact internal ids over repeating long keys (§ keys-and-layers advanced techniques).

---

## 3. High contention & conflict avoidance

Conflicts are resolver verdicts on **read-conflict ranges**. A key that many transactions read-then-write serializes there. Avoid it:

- **Atomic mutations** (`AtomicAdd64`, `AtomicIncrement32/64`, `AtomicMax/Min`, `AtomicOr/And/Xor`) — they don't read, so they create **no read-conflict** and never conflict with each other. Counters, statistics, signal keys.
- **Snapshot reads** for values you don't need to serialize on.
- **Shard write-hot keys** across N sub-keys and aggregate on read — the high-contention counter and the change-feed's per-subscriber keys both do this so writers never collide. A single global counter is a guaranteed conflict hotspot.
- Add explicit conflict ranges (`AddConflictRange`) only when your reads/writes don't already imply the semantics you need.

---

## 4. The global clock: versions & version-stamps

The sequencer is the **only** source of "now" that every node agrees on. Use it; never use node-local wall clocks for cross-node decisions.

- **`tr.GetReadVersionAsync()`** → the read version: a monotonic, cluster-wide logical clock, identical regardless of which node reads it. Use it for **leases / liveness**, ordering, "as-of" reasoning.
- **`tr.CreateVersionStamp()` + `SetVersionStampedKey/Value`** → the commit version, assigned atomically at commit. Globally ordered, collision-free → the backbone of queues, logs, and change feeds (§5).
- **`GetVersionStampAsync()` / `GetCommittedVersion()`** → recover the version a transaction committed at.

> ⚠️ **Two clock traps** (both real, both bite):
> 1. **Local wall clocks have no shared "now."** Comparing a timestamp minted on node A against node B's `DateTime.UtcNow` is meaningless (skew, drift, NTP steps, VM pauses) — like comparing times across relativistic frames. Cross-node liveness must use the database clock.
> 2. **The version tick-rate is not constant** (~1e6/s but it drifts; idle clusters advance slower). So do **not** convert a version delta into a duration (`now - lease > N_versions` is unsound). Instead, store a DB-sourced token and test it for **change** (equality), and measure elapsed time only as the gap between an observer's *own* consecutive local reads (§5.3).

A shared clock removes *skew*, but not the fundamental **failure-detector impossibility**: you still cannot distinguish "slow" from "dead." So liveness is always a policy (a threshold) backed by **evict-and-resync**, never a proof.

---

## 5. Capstone — building a change feed

A change feed lets other nodes observe a stream of changes and maintain an in-memory view. It composes every primitive above. Full compile-checked code: [`BookStore.ChangeFeed.cs`](../../../samples/SkillValidation/BookStore.ChangeFeed.cs).

### 5.1 Append-and-signal (in the mutation's own transaction)
Each mutation appends a change under a **commit-ordered VersionStamp** and bumps a single watched **signal key** — all in the same transaction as the data write, so the feed can never disagree with the data:
```csharp
var stamp = tr.CreateUniqueVersionStamp();                       // distinct per change, even several per tx
tr.SetVersionStampedKey(subspace.Key(SUBSPACE_FEED, stamp), FdbValue.ToJson(change));
tr.AtomicIncrement64(subspace.Key(SUBSPACE_SIGNAL));             // wake every subscriber; conflict-free
```

### 5.2 Subscribe — cursor streaming with a watch tail
The consumer reads pages after its cursor; when caught up, it watches the signal key (outer token, not `tr.Cancellation`), awaits **outside** the transaction, then re-reads. Expose it as `IAsyncEnumerable<T>` and wrap thinly as a `Channel<T>` or a callback. The VersionStamp of the last entry **is** the resume cursor.

### 5.3 Retention without unbounded growth, and liveness without clock skew
A version-stamped log grows forever, so a GC must trim it. Trim everything consumed by all **live** subscribers (up to the slowest live cursor); if nobody's live, drop the backlog. "Live" is decided *without comparing clocks*:
- each subscriber renews a **DB-sourced token** (its read version) on a local interval;
- an observer reads those tokens on its own local interval and watches for tokens that **don't change** across several polls (`unchanged for N polls ≈ N × the observer's own local delay`) — equality-check only, never version→time, never cross-node timestamp comparison;
- the observer's reads are **non-snapshot**, so a subscriber that renews concurrently conflicts the GC and is spared.

### 5.4 Fencing — detecting "I fell out of the window" in one round-trip
A subscriber frozen long enough gets evicted and the GC reclaims past its cursor → it **missed changes** and its view is untrustworthy. It must be told. The efficient signal is a **tombstone**: when the GC reclaims `(·, horizon]`, it leaves one empty-value entry at the horizon's versionstamp.
- A resumer whose cursor is older than the horizon reads that tombstone **first** in its normal `GetRange` — an empty value deserializes to `null` (a real change is always non-null JSON), so it's detected with **no extra read / no serial dependency**.
- It throws a typed `ChangeFeedOutOfSyncException` that propagates through the enumerable / channel / callback; the consumer catches it, **reloads current state, and re-subscribes from "now."**
- Bonus: the throw aborts the same transaction that would have renewed the stale cursor, so an evicted subscriber never re-registers a misleading cursor.

This is the same contract as Kafka's `OffsetOutOfRange` / DynamoDB Streams' `TrimmedDataAccessException`: you can't *prevent* a too-slow consumer from missing data — you detect it cleanly and force a resync.

---

## 6. Distributed-layer review checklist

- [ ] No **serial read→decide→read** chains on the hot path — batched (`GetValuesAsync`), parallel (`Task.WhenAll`), or encoded into one read (tombstone-style)?
- [ ] Independent reads issued **concurrently**, never `await`-ed in a loop?
- [ ] Write-hot keys **sharded** / using **atomics**; snapshot reads where serialization isn't needed?
- [ ] Long scans **paged across transactions** (5-second window), large values chunked, bulk via `Fdb.Bulk.*`?
- [ ] Cross-node time uses the **database clock** (read version / versionstamp), never local wall clocks?
- [ ] Liveness via **token change-detection + local inter-poll elapsed**, not version→duration math?
- [ ] Unbounded logs/feeds have a **retention/GC** path, and consumers can **detect a gap and resync** (fencing)?
- [ ] Transaction handlers still **idempotent** (no external side effects); resolved layer `State` confined to the transaction?
