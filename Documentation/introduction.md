# Introduction

This library is a C#/.NET binding for [FoundationDB](https://www.foundationdb.org/), wrapping the native `fdb_c` client and exposing an idiomatic, allocation-conscious, `async`/`await` API.

## What FoundationDB is

FoundationDB is a distributed, **ordered key/value store** with **serializable, ACID transactions** across the entire keyspace. It is intentionally minimal: it stores byte-string keys mapped to byte-string values, keeps keys sorted, and lets you read and write many of them atomically. Everything higher-level — tables, indexes, queues, document collections, pub/sub — you build yourself, as a **Layer** on top of that primitive.

That minimalism is the source of both its power and its sharp edges. Get the key encoding and the transaction model right and you get a rock-solid distributed foundation; get them wrong and you get subtle data corruption. The goal of this documentation is to keep you firmly on the first path.

## What this binding gives you

- **Strongly-typed, lazy keys** — `subspace.Key("user", 123)` builds a small struct that tuple-encodes itself only when handed to a transaction. No manual byte wrangling.
- **A retry loop** — `db.ReadAsync` / `WriteAsync` / `ReadWriteAsync` handle FoundationDB's conflict-and-retry model for you.
- **The Directory layer** — map human-readable paths to short, dense key prefixes.
- **Layers** — a small contract (`IFdbLayer<TState>`) for packaging data access into reusable, composable components.
- **Allocation-consciousness** — `Slice`, pooled buffers, and `struct` keys/values keep the hot path free of needless `byte[]` allocations.

## Where to go next

- [Getting Started](getting-started.md) — install the packages and run your first read and write.
- [Guide → Keys, Values & Layers](guide/keys-and-layers.md) — the most important thing to learn first.
- The [README](../README.md) covers installation, dependency injection, .NET Aspire, and deployment in full.

> **A note on scope:** FoundationDB itself has well-known limits you should design around from day one — transactions last at most ~5 seconds, values are capped at 100 KB, keys at 10 KB, and a single transaction may write at most 10 MB. The [Transactions](guide/transactions.md) guide explains why these exist and how to live within them.
