# Keys, values, and Layers how-to guides

Each section here is one task against the key/value API. They assume you already have an open database
(an `IFdbDatabaseProvider`), as set up in [Getting started](../../getting-started.md). For why keys
are tuples, what a subspace is, and why a Layer holds no per-transaction state, see
[the explanation](index.md); for the tuple encoding in depth, see the [reference](reference.md).

## Build a key

Build keys with `subspace.Key(...)`, which tuple-encodes its arguments behind the subspace prefix.
Build values with the `FdbValue.*` factories. Pass the key or value object straight to the
transaction; do not pre-serialize it with `.ToSlice()`.

```csharp
// build keys (strongly typed, lazy)
var k = subspace.Key("user", 123);          // prefix + ("user", 123)
Slice value = await tr.GetAsync(k);          // rendered to bytes here, into pooled buffers
tr.Set(subspace.Key("user", 123), FdbValue.FromTuple(("Alice", 30)));
tr.Clear(subspace.Key("user", 123));
```

`.ToSlice()` exists, but only for when you need the *bytes as data* (logging, tests, or storing a key
inside a value). For why manual concatenation breaks ordering and escaping, see
[the explanation](index.md).

## Build a key whose tail is dynamic

For a generic index whose indexed value has an arbitrary type, chain a runtime tuple onto a typed
prefix:

```csharp
IVarTuple value = /* built at runtime */;
var indexKey = subspace.Key(INDEXES, indexId).Tuple(value);   // typed prefix (1, idx) + dynamic suffix
```

This is the modern replacement for the older dynamic `subspace.Pack(...)` style.

## Create ordered, collision-free ids

For queues, event logs, and change feeds (anything that needs globally-ordered, collision-free ids),
let the database assign a **VersionStamp** at commit time:

```csharp
var stamp = tr.CreateVersionStamp(userVersion);              // an incomplete stamp, filled at commit
tr.SetVersionStampedKey(log.Key(stamp), payload);            // FDB writes the real, monotonic stamp on commit
```

A plain range scan then returns entries in commit order, with no shared counter to contend on. See
[Advanced Layers](../advanced-layers/index.md) for the full change-feed pattern.

## Read a range

Most layers read **ranges**, not single keys. Build ranges from keys and subspaces; never increment
bytes by hand.

```csharp
tr.GetRange(subspace.ToRange());                  // everything under the subspace
tr.GetRange(subspace.Key("user", 123).ToRange()); // everything under one prefix
FdbKeyRange.Between(subspace.Key(100), subspace.Key(200));  // [100, 200)
```

Useful derivations (extension methods on any key): `key.Successor()` (the next key, an exclusive lower
bound), `key.NextSibling()` (first key that doesn't have `key` as a prefix, an exclusive upper bound
over its children), `subspace.First()` / `subspace.Last()`, and the `KeySelector`s
`FirstGreaterOrEqual()` / `LastLessOrEqual()`.

## Decode keys from a range

Read a range, get raw key bytes back, and decode them with the **same subspace** that produced them:

```csharp
foreach (var kv in chunk)
{
    var (name, id) = subspace.Decode<string, int>(kv.Key);  // STuple<string?, int?>
    int idOnly     = subspace.DecodeLast<int>(kv.Key);
    IVarTuple all  = subspace.Unpack(kv.Key);
}
```

Use `Decode`/`DecodeLast`/`Unpack`; never slice bytes by hand.

## Resolve a subspace through the Directory layer

You never hard-code a prefix. Declare a logical **path**, and resolve it to a subspace through the
Directory layer inside the transaction:

```csharp
ISubspaceLocation location = db.Root["Tenants"]["ACME"]["Documents"]["Books"];

await db.WriteAsync(async tr =>
{
    IKeySubspace subspace = await location.Resolve(tr);   // queries the Directory layer
    tr.Set(subspace.Key("BOOK_123"), FdbValue.FromTuple(("Title", "ISBN")));
}, ct);
```

Three rules, and [the explanation](index.md) covers why the prefix is dynamic in the first place:

- **Resolve every transaction.** The prefix is stable in practice but not guaranteed forever; caching
  it yourself defeats the Directory layer and risks corruption.
- **Resolve opens; it does not create.** `Resolve` throws if the directory does not exist yet. Create
  it the first time with `location.CreateOrOpenAsync(tr)` in a read-write transaction, which is what a
  layer does on setup.
- **The `db.Root[...]` indexer descends one *segment* at a time.** `db.Root["a", "b"]` is *not* two
  segments: the two-argument overload is `(name, layerId)`. Chain the indexer (`db.Root["a"]["b"]`) or
  pass an `FdbPath`.

## Pick a value encoding

Values are produced by the `FdbValue.*` factories. Pick the factory that matches the access pattern:

| Need | Use |
|---|---|
| Raw bytes / blob | `FdbValue.ToBytes(slice)` |
| Empty value (index entries) | `FdbValue.Empty` |
| Text | `FdbValue.ToTextUtf8(s)` / `ToTextUtf16(s)` |
| A counter you'll mutate atomically | `FdbValue.ToFixed64LittleEndian(n)` (fixed little-endian is required for `AtomicAdd64`) |
| A tuple | `FdbValue.FromTuple(("a", 1))` |
| JSON document | `FdbValue.ToJson(obj)`, see [CrystalJson](../../crystaljson/index.md) |

Reading back: `slice.ToInt64()`, `slice.ToStringUtf8()`, `CrystalJson.Deserialize<T>(slice)` (which
maps a missing/empty key to `null`), etc.

For a JSON value, `FdbValue.ToJson(obj)` serializes an object through CrystalJson, the SDK's JSON
stack, and `CrystalJson.Deserialize<T>(slice)` reads it back:

```csharp
tr.Set(subspace.Key("D", book.Id), FdbValue.ToJson(book));
Book? loaded = CrystalJson.Deserialize<Book>(await tr.GetAsync(subspace.Key("D", book.Id)));
```

CrystalJson is a general-purpose JSON stack with its own guide: the DOM, the source generator and
the settings are in [CrystalJson](../../crystaljson/index.md).

## Write a Layer

Wrap database access in a **Layer**: a thin wrapper over an `ISubspaceLocation` that holds no
per-transaction state. For why the pattern is shaped this way, see [the explanation](index.md). Every
layer follows the same shape:

1. The layer class is a **thin, reusable wrapper** over an `ISubspaceLocation` (plus codecs/options).
   It holds **no per-transaction state**.
2. It implements `IFdbLayer<TState>`. `Resolve(tr)` resolves the location and returns a **`State`**
   holding the resolved `IKeySubspace`. Memoize it in `tr.Context` so repeated `Resolve(tr)` calls in
   one transaction are cheap.
3. All real work is methods that take a transaction and use the `State`'s subspace to build keys.
4. **The `State` must never escape the transaction**: don't store it in a field or reuse it across
   retries. (`tr.Context` local data is per-transaction, so memoizing there is safe; a layer field is
   not.)

### A document store with a secondary index

```csharp
public sealed partial class BookStore : IFdbLayer<BookStore.State>
{
    // Discriminate sub-parts of the subspace with small INTEGER constants, not strings:
    // 0 packs to 1 byte (0x14), 1 to 2 bytes (0x15 0x01), whereas "D" is 3 bytes (0x02 'D' 0x00) on every key.
    private const int SUBSPACE_DOCUMENTS = 0;      // (0, <id>)            -> json document
    private const int SUBSPACE_INDEX_AUTHOR = 1;   // (1, <author>, <id>) -> empty (index entry)

    public BookStore(ISubspaceLocation location) => this.Location = location;
    public ISubspaceLocation Location { get; }
    public string Name => nameof(BookStore);

    private const string LocalDataKey = nameof(BookStore);
    public ValueTask<State> Resolve(IFdbReadOnlyTransaction tr)
    {
        if (tr.Context.TryGetLocalData(LocalDataKey, out State? s)) return new(s);
        return ResolveSlow(this, tr);
        static async ValueTask<State> ResolveSlow(BookStore self, IFdbReadOnlyTransaction tr)
        {
            var subspace = await self.Location.Resolve(tr);
            return tr.Context.GetOrCreateLocalData(LocalDataKey, new State(self, subspace));
        }
    }

    public sealed partial class State
    {
        public IKeySubspace Subspace { get; }
        internal State(BookStore layer, IKeySubspace subspace) => this.Subspace = subspace;

        public void Insert(IFdbTransaction tr, Book book)
        {
            tr.Set(this.Subspace.Key(SUBSPACE_DOCUMENTS, book.Id), FdbValue.ToJson(book));
            tr.Set(this.Subspace.Key(SUBSPACE_INDEX_AUTHOR, book.Author, book.Id), FdbValue.Empty);
        }

        public async Task<Book?> GetAsync(IFdbReadOnlyTransaction tr, string id)
            => CrystalJson.Deserialize<Book>(await tr.GetAsync(this.Subspace.Key(SUBSPACE_DOCUMENTS, id)));

        public IAsyncQuery<string> FindIdsByAuthor(IFdbReadOnlyTransaction tr, string author)
            => tr.GetRange(this.Subspace.Key(SUBSPACE_INDEX_AUTHOR, author).ToRange())
                 .Select(kv => this.Subspace.DecodeLast<string>(kv.Key)!);
    }
}
```

Used through the retry-loop helpers, which resolve the state for you:

```csharp
var store = new BookStore(db.Root["Documents"]["Books"]);
await store.WriteAsync(db, (tr, st) => st.Insert(tr, book), ct);
Book? b = await store.ReadAsync(db, (tr, st) => st.GetAsync(tr, "B1"), ct);
```

## Compose several layers in one transaction

Because a layer's methods take a transaction rather than opening their own, one retry loop can drive
several layers atomically. Insert a document, queue a background job, and publish an event in the same
`WriteAsync`, and either all of them commit or none do:

```csharp
await db.WriteAsync(async tr =>
{
    await books.InsertAsync(tr, book);
    await workers.QueueAsync(tr, new GenerateThumbnails(book.Id));
    await feed.PublishAsync(tr, new BookCreated(book.Id));
}, ct);
```

If the transaction fails to commit, it is as if the request never happened: no document, no job, no
event.

## Maintain a secondary index

Index entries are **derived data**: your code, not the database, keeps them in sync. This is where
layers most often go wrong:

- **To change the index you must know the OLD indexed value**, and you can only learn it from the
  **stored document**, never from an object the caller hands you (it may be stale, leaving an orphaned
  index entry). `Update`/`Patch`/`Delete` therefore read the current document and derive the old index
  key from *that*.
- **Mutate the index in the same transaction as the document**, so it can never drift out of sync on a
  partial failure.
- **Only rewrite the index when the indexed value actually changed.** For frequently-updated documents
  whose indexed field is stable, this avoids needless writes (and the conflicts they cause).

Concretely, changing a book's author rewrites the document **in place** and **moves** its index entry,
both in one transaction. The document keeps its key, so only its value changes; the index key is
genuinely different, so the old entry is deleted and a new one inserted:

```fdb-diff
title: change a book's author  ·  Tolkien to J.R.R. Tolkien
~ (..., D:0, "hobbit") = { "title": "The Hobbit", "author": -"Tolkien" +"J.R.R. Tolkien" }
- (..., I:1, "Tolkien", "hobbit") = ''
+ (..., I:1, "J.R.R. Tolkien", "hobbit") = ''
```

The example offers three update flavors, trading a read against caller obligations:

| Method | Reads the old doc? | Use when |
|---|---|---|
| `UpdateAsync(tr, book)` | yes | the caller built a fresh `Book` and doesn't hold the original |
| `UpdateAsync(tr, updated, original)` | no | the caller already read `original` **in the same transaction** (its read provides the conflict that keeps the index consistent; passing a stale `original` corrupts the index) |
| `PatchAsync(tr, id, patch)` | yes | cheap field bumps; a no-op patch (`updated == current`, by record value equality) writes nothing |

## Make layer keys human-readable

Raw keys are opaque bytes. Tools (the FQL shell, `FdbShell`, dumps, the transaction logger) can render
them as friendly tuples if the layer publishes a schema. Implement `IFdbLayerSchemaMapper` (often as a
nested class) and return one `FqlTemplateExpression` per key family:

```csharp
public sealed class SchemaMapper : IFdbLayerSchemaMapper
{
    public string LayerId => "docstore.Books";
    public IEnumerable<FqlTemplateExpression> GetRules()
    {
        yield return new("document",
            FqlTupleExpression.Create().Integer(SUBSPACE_DOCUMENTS, "D").VarString("id"),
            FdbValueTypeHint.Json);
        yield return new("index.author",
            FqlTupleExpression.Create().Integer(SUBSPACE_INDEX_AUTHOR, "I").VarString("author").VarString("id"),
            FdbValueTypeHint.None);
    }
}
```

With that schema published, a raw key stops being opaque bytes and reads as a friendly tuple. The two
families render as (`D`/`I` are the display names for subspaces `0`/`1`, and `...` stands for the
resolved Directory prefix):

```fdb-fql
// a book document
(..., D:0, <id:string>) = <json>

// by-author index entry (empty value)
(..., I:1, <author:string>, <id:string>) = ''
```

The value hint can also be a **function of the decoded key** (`(SpanTuple t) => t.Get<string>(0) switch { … }`)
when the value's type depends on the key.

## Migrate from the old dynamic key API

Older code used a dynamic subspace API (`IDynamicKeySubspace`, `subspace.Encode(...)` / `.Pack(...)`),
which has been replaced by the strongly-typed `subspace.Key(...)` family. Translate mechanically:

| Old (dynamic) | New (typed) |
|---|---|
| `subspace.Encode(a, b, c)` | `subspace.Key(a, b, c)` |
| `subspace.Pack(STuple.Create(a, b).Concat(value))` | `subspace.Key(a, b).Tuple(value)` |
| `subspace.EncodeRange(a, b)` | `subspace.Key(a, b).ToRange()` |
| `global.Partition.ByKey(p)` | `global.Key(p).ToSubspace()` |
| field/return type `IDynamicKeySubspace` | `IKeySubspace` |

## Reference layers to imitate

When in doubt, read the real implementations in `FoundationDB.Layers.Common/`: `FdbMap` (key→value),
`FdbIndex` (composite `(value, id)` keys), `FdbVector` (integer index keys), `FdbHighContentionCounter`
(write-contention avoidance), `FdbBlob` (chunking large values), `FdbStringIntern` (bidirectional
maps).

Next: **[Transactions](../transactions/index.md)** for the retry-loop semantics these layers run
inside.
