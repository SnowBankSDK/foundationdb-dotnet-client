# FoundationDB for .NET

`FoundationDB.Client` is the .NET client for [FoundationDB](https://www.foundationdb.org/), the distributed ordered key/value store. It wraps the native `fdb_c` client and exposes an idiomatic, allocation-conscious, `async`/`await` API.

This page is the entry point for the FoundationDB client. The client is one part of the [SnowBank SDK](../index.md), which also ships a set of general-purpose libraries (the `SnowBank.*` packages) that the client is built on. You do not need to know the rest of the SDK to use the database client: if you came for FoundationDB, read on. The [overview](../index.md) maps out both parts if you want the wider picture.

## What FoundationDB is

FoundationDB is a distributed, **ordered key/value store** with **serializable, ACID transactions** across the entire keyspace. It is intentionally minimal: it stores byte-string keys mapped to byte-string values, keeps keys sorted, and lets you read and write many of them atomically. Everything higher-level (tables, indexes, queues, document collections, pub/sub) you build yourself, as a **Layer** on top of that primitive.

The same minimalism puts two responsibilities on you: the key encoding and the transaction model. Get them right and the store is dependable; get them wrong and the result is subtle data corruption. This documentation teaches both.

## What this binding gives you

- **Strongly-typed, lazy keys**: `subspace.Key("user", 123)` builds a small struct that tuple-encodes itself only when handed to a transaction. You never assemble key bytes by hand.
- **A retry loop**: `db.ReadAsync` / `WriteAsync` / `ReadWriteAsync` handle FoundationDB's conflict-and-retry model for you.
- **The Directory layer**: map human-readable paths to short, dense key prefixes.
- **Layers**: a small contract (`IFdbLayer<TState>`) for packaging data access into reusable, composable components.
- **Allocation-consciousness**: `Slice`, pooled buffers, and `struct` keys/values keep the hot path free of needless `byte[]` allocations.

## Where to go next

- **New to FoundationDB?** Start with [Prerequisites](prerequisites.md), then [How it connects](foundationdb-101.md) and [Cluster setup](cluster-setup.md).
- [Getting Started](getting-started.md): install the packages and run your first read and write.
- [Guide → Keys, Values & Layers](guide/keys-and-layers/index.md): the most important thing to learn first.
- [Aspire](aspire/index.md): run a local cluster and wire it into your services automatically. The [README](../../README.md) has deployment and build details.

> **A note on scope:** FoundationDB itself has well-known limits you should design around from day one: transactions last at most ~5 seconds, values are capped at 100 KB, keys at 10 KB, and a single transaction may write at most 10 MB. The [Transactions](guide/transactions/index.md) guide explains why these exist and how to live within them.
