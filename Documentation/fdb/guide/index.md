# Building on FoundationDB: A Developer's Guide

FoundationDB gives you a single, ordered, transactional key/value store and asks you to build everything else on top of it. That freedom is the whole point, and the main source of mistakes. This guide is a practical, opinionated walkthrough of how to use this .NET binding (`FoundationDB.Client` / `SnowBank`) *well*: how to encode keys, how transactions actually behave, and how to build sophisticated, distributed "Layers" without falling into the classic traps.

It is organized in four parts:

1. **[Keys, Values & Layers](keys-and-layers/index.md)**: how data is encoded, and how to package data access into a reusable *Layer*. Start here.
2. **[Transactions](transactions/index.md)**: the retry loop, idempotency, conflicts, atomic operations, and watches.
3. **[Advanced Layers](advanced-layers/index.md)**: how the cluster processes a transaction, how to make layers fast, and the hard distributed-systems patterns (change feeds, leases, retention, fencing).
4. **[Binary Data (Slice & Buffers)](../../snowbank/slices-and-buffers.md)**: the byte-level toolkit beneath everything else, `Slice` and `SliceReader`/`SliceWriter`, pooled buffers, and the integer encodings. Reach for it when you write custom value codecs.

> These guides are the human-facing companion to the agent-oriented skills under [`.claude/skills/`](../../../.claude/skills/), and every code example mirrors the compile-checked samples in [`samples/SkillValidation/`](../../../samples/SkillValidation/).

## The mental model in one screen

- The database is **one flat, sorted map of bytes → bytes.** Keys sort lexicographically by their raw bytes, and that ordering is the *only* structure you get. Every table, index, queue, and document collection is an illusion you build by choosing key bytes carefully.
- **Tuples are how you choose those bytes.** The tuple encoding turns typed values (strings, integers, GUIDs, `VersionStamp`s) into bytes whose order matches the logical order of the values. `(42, "a")` always sorts before `(42, "b")` before `(43, …)`. This is why tuples are the default key encoding.
- A **subspace** is a key prefix you get by resolving a logical *location* (usually through the Directory layer). All your keys live inside it.
- A **transaction** is serializable and ACID, but may need to be retried, and is bounded to **5 seconds** and **10 MB** of writes.
- A **Layer** is a small, reusable component that turns the raw key/value API into a meaningful abstraction (a map, an index, a document store, a change feed).

## The big lessons (learned the hard way)

These recur throughout the guide; they're worth internalizing up front.

- **Never touch raw bytes.** Build keys with `subspace.Key(...)` and values with `FdbValue.*`, and hand those objects straight to the transaction. Manual string/byte concatenation breaks ordering and escaping.
- **Keys are lazy.** `subspace.Key("a", 1)` is a small struct that remembers its parts; it's rendered to bytes only when the transaction needs it. Don't eagerly call `.ToSlice()`.
- **Your transaction handler runs more than once.** It must be a pure function of database state: no external side effects (caches, counters, logging) inside it.
- **Use atomic operations for contention.** A single hot counter serializes all writers at the resolver; `AtomicAdd64` and sharding don't.
- **There is no global wall clock.** Different nodes' clocks can't be compared. When you need a shared notion of time or order, use the database's **read version** (a monotonic clock from the cluster's sequencer), never `DateTime.UtcNow` across nodes.
- **Latency is round-trips.** The client pipelines, so batch independent reads (`GetValuesAsync`, `Task.WhenAll`) and avoid "read, decide, read again" chains.
- **Unbounded logs must be trimmed, and consumers must be able to detect they fell behind.** A change feed isn't done until it has retention *and* a way to tell a stalled subscriber to resync.

If a piece of code you're writing or reviewing touches keys, transactions, or multi-node coordination, the relevant guide below has the idiomatic pattern, and the reasoning behind it.
