# How the cluster processes a transaction

The design of a sophisticated layer falls out of understanding how the cluster actually processes a
transaction. This page explains that model: the roles, the flow, and why the rules you live with
(the 5-second limit, conflicts, the global clock) are direct consequences of it. [Advanced Layers](how-to.md)
applies the model to performance and distributed patterns; read this first. It assumes
[Keys, Values & Layers](../keys-and-layers/index.md) and [Transactions](../transactions/index.md).

## How a transaction is processed

FoundationDB splits responsibilities across several roles (this is the published FoundationDB architecture; the constraints you live with are direct consequences of it):

| Role | Responsibility |
|---|---|
| **Coordinators** | Small Paxos group; elect the cluster controller and hold the cluster file. Clients bootstrap here. |
| **Cluster Controller** | Recruits/monitors every other role; drives recovery. |
| **Master / Sequencer** | Hands out **monotonically increasing versions**: read versions and commit versions. This is the global logical clock. |
| **GRV proxies** | Serve *get-read-version*: ask the master for the latest committed version and confirm the transaction logs are still live (so a read version is never stale after a recovery). Throttled by Ratekeeper. |
| **Commit proxies** | Drive commits: get a commit version from the master, send conflict ranges to the resolvers, make mutations durable on the transaction logs. |
| **Resolvers** | Hold the **last ~5 seconds of committed writes** in memory and compare a committing transaction's read-conflict ranges against them. This is where conflicts (`not_committed`, 1020) are decided. |
| **Transaction logs (tlogs)** | Durable, replicated write-ahead log; receive mutations in version order and only acknowledge once **fsync'd** on a quorum. |
| **Storage servers** | Hold the sharded, replicated data; keep ~5 seconds of mutations in memory plus an on-disk copy "as of 5 seconds ago"; serve reads via MVCC. |
| **Ratekeeper** / **Data Distributor** | Throttle transaction-start rate near saturation / keep shards balanced across storage servers. |

A **read-write transaction** flows like this:

1. **Get read version (GRV).** The first read fetches a read version from a GRV proxy (a recent committed version, quorum-confirmed).
2. **Reads** go *directly to the storage servers* at that version. The client caches the shard→server map and can issue reads in parallel. Read-conflict ranges accumulate client-side, unless you use snapshot reads.
3. **Writes** are buffered *in the client*; nothing hits the cluster yet.
4. **Commit.** The client sends mutations and conflict ranges to a commit proxy → it gets a commit version from the master → the resolvers check for conflicts → if clean, the mutations are made durable on the tlogs → the proxy acknowledges with the commit version (which is what fills your `VersionStamp`s).
5. Storage servers asynchronously pull and apply the committed mutations from the tlogs.

### Why the rules exist

- **Read version = the sequencer's clock.** It's the one notion of "now" that every node agrees on, which is exactly why it's the right basis for cross-node coordination (and why local wall clocks are not; see *The global clock* below).
- **`VersionStamp` = the commit version.** Globally ordered and monotonic, ideal for logs and feeds.
- **Conflicts = resolver verdicts** on read-conflict ranges. Snapshot reads (no read-conflict added) and atomic operations (no read at all) avoid them.
- **The 5-second limit = the MVCC window** the resolvers and storage servers retain. A read version older than that yields `transaction_too_old`. It's also why a recovery "fast-forwards" time and aborts in-flight transactions. Keep transactions short; page long scans across many of them.
- **Reads scale horizontally** across storage servers; **commits funnel** through proxies → resolvers → tlogs. So read-heavy workloads scale easily, while commit throughput is the thing to economize: keep write sets small and batch writes.

## The global clock

The sequencer is the only source of "now" that every node agrees on. Use it; never use node-local wall clocks for cross-node decisions.

- `tr.GetReadVersionAsync()` gives the read version (a monotonic, cluster-wide logical clock). Use it for leases, ordering, and "as-of" reasoning.
- `tr.CreateVersionStamp()` + `SetVersionStampedKey/Value` give the commit version, for ordered logs and feeds.

Two traps, both real:

1. **Local wall clocks have no shared "now."** Comparing a timestamp minted on one node against another node's `DateTime.UtcNow` is meaningless: skew, drift, NTP steps, and VM pauses make it like comparing times across relativistic frames. A node with a fast clock evicts live peers; one with a slow clock never evicts dead ones.
2. **The version tick-rate is not constant** (~1,000,000/s, but it drifts and slows when the cluster is idle). So do **not** convert a version delta into a duration. Instead, store a database-sourced token and test it for **change** (equality), and measure elapsed time only as the gap between an observer's *own* consecutive local reads.

A shared clock removes *skew*, but not the fundamental **failure-detector impossibility**: you can never be certain whether a peer is slow or dead. Liveness is therefore always a policy (a threshold) backed by **evict-and-resync**, not a proof.
