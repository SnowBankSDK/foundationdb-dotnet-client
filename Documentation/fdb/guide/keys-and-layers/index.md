# Keys, values, and Layers: what they are and why

This is the first topic to get right, and the most common source of incorrect usage. The database is
one flat map of bytes, and every table, index, and document collection is a pattern you build on top
of it. This page explains why keys are tuple-encoded, what a subspace and the Directory layer are,
and what a Layer is; for the task recipes (building keys, resolving subspaces, writing a Layer,
picking a value encoding) see [How-to guides](how-to.md), and for the tuple encoding in depth see the
[reference](../../../snowbank/tuples.md).

## One flat, sorted map of bytes

FoundationDB gives you a single, ordered, transactional key/value store and asks you to build
everything else on top of it. The database is one flat, sorted map of bytes to bytes. Keys sort
lexicographically by their raw bytes, and that ordering is the only structure you get. Every table,
index, queue, and document collection is a pattern you build by choosing key bytes carefully. Choose
the key bytes well and a range scan returns exactly the rows you want, in the order you want; choose
them wrong and the same scan returns too much, too little, or the wrong order.

The database stores bytes and never inspects them. You almost never handle those bytes yourself. You
build a key object, hand it to the transaction, and the binding renders it to bytes at the last
moment. The reason to keep it that way is correctness: manual string or byte concatenation breaks the
byte ordering or the escaping that the sort depends on. [The how-to page](how-to.md) gives the
building recipes; this page explains why they are the only safe ones.

## Why keys are tuples

Tuples are how you choose the key bytes. The tuple encoding turns typed values (strings, integers,
GUIDs, `VersionStamp`s) into bytes whose order matches the logical order of the values. `(42, "a")`
always sorts before `(42, "b")`, which sorts before `(43, ...)`. That property is why tuples are the
default key encoding: the sort you get for free from the database is the sort your application wants,
as long as the bytes came from the tuple encoder.

Each element opens with a one-byte type marker, then its value bytes. The markers sort the types into
a fixed order, and the value bytes order values within a type. This is the whole mechanism behind
ordered keys; the [reference](../../../snowbank/tuples.md) covers the encoding, the tuple variants, and the decoding
helpers in full.

Keys are also lazy. `subspace.Key("user", 123)` is a small struct that remembers its parts and renders
to bytes only when the transaction needs it. You pass the key object straight to `tr.GetAsync`,
`tr.Set`, or `tr.Clear`; you do not pre-serialize it with `.ToSlice()` and pass bytes around. The lazy
struct lets the binding render into pooled buffers at the point of use, and it keeps the typed parts
available for as long as possible.

## Subspaces and the Directory layer

A subspace is a key prefix. All of a component's keys live inside one subspace, so its keys never
collide with another component's. You never invent or hard-code that prefix. Instead you declare a
logical path and let the Directory layer map it to a short, dense binary prefix.

Think of a location as a folder in a file system, and the Directory layer as the table that maps a
folder path to an i-node number. Your code thinks in readable paths; the database stores a short
integer prefix. If `/Tenant/ACME/MyApp/v1/Documents/Books` is assigned prefix `42`, a key in it is
stored as `(42, "BOOK_123")` instead of the full path tuple, which saves dozens of bytes on every key.

```fdb-bytes
tuple: (42, "BOOK_123")
int  .15 2A                # dir prefix · 42
str  .02 'BOOK_123' .00    # string "BOOK_123"
```

The prefix is allocated dynamically and is not known until the directory is first created, so
throughout these docs the prefix folds into a leading `...` and a layer's own key reads as
`(..., "BOOK_123")`. It is the same idea as a relative path `./BOOK_123` instead of the absolute
`/Tenant/ACME/MyApp/v1/Documents/Books/BOOK_123`: the prefix bytes are still there, the docs just do
not spell out a value that changes per deployment.

```fdb-bytes
tuple: (..., "BOOK_123")
dir  ...                   # dir prefix
str  .02 'BOOK_123' .00    # string "BOOK_123"
```

Only a page specifically about the complete key, or about the Directory layer itself, spells the
prefix out. Two consequences follow, and [the how-to page](how-to.md) turns them into recipes: you
resolve the location once per transaction rather than caching the prefix (caching it yourself risks
corruption), and resolving opens an existing directory rather than creating one.

## What a Layer is, and why

A Layer is the FoundationDB equivalent of a small data-access component: a map, an index, a document
collection. Rather than scatter database access across controllers and pages, you wrap it in a Layer,
and every layer in `FoundationDB.Layers.Common` and in the larger SnowBank layers follows the same
shape.

A Layer is a thin, reusable wrapper over an `ISubspaceLocation`. It holds no per-transaction state. It
implements `IFdbLayer<TState>`, and `Resolve(tr)` resolves the location and returns a `State` that
holds the resolved `IKeySubspace`. All real work is methods that take a transaction and use the
State's subspace to build keys.

One rule makes the pattern safe: the State must never escape the transaction. The resolved subspace is
only valid inside the transaction that produced it, so a layer never stores the State in a field or
reuses it across retries. Memoizing the State in `tr.Context` is safe because that local data is
per-transaction; a layer field is not. Because a layer's methods take a transaction rather than
opening their own, one retry loop can drive several layers atomically: insert a document, queue a job,
and publish an event in the same transaction, and either all of them commit or none do.

The layer shape rests on the transaction retry loop, which runs a handler more than once and is the
subject of [Transactions](../transactions/index.md). [The how-to page](how-to.md) shows a worked layer
with a secondary index, how to compose layers, and how to keep an index consistent.
