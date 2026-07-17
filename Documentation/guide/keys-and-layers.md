# Keys, Values & Layers

This is the most important guide to get right, and the most common source of incorrect "vibe-coded" usage. It covers how keys and values are encoded, how subspaces and the Directory layer organize the keyspace, and how to package data access into a reusable *Layer*.

## How data is encoded

FoundationDB stores bytes and sorts keys lexicographically. You almost never deal with those bytes directly. Instead:

- **Keys** are built with `subspace.Key(...)`, which tuple-encodes its arguments behind a subspace prefix. The result is a small, **lazy** struct (`FdbTupleKey<…>`) that remembers its parts and renders to bytes only when handed to the transaction.
- **Values** are produced by the `FdbValue.*` factories (`ToBytes`, `ToTextUtf8`, `FromTuple`, `ToJson`, `ToFixed64LittleEndian`, …).

The golden rule: **build a key/value object and pass it straight to the transaction.** Don't pre-serialize with `.ToSlice()` and pass bytes around; don't concatenate strings or use `BitConverter`; don't reach for `TuPack.EncodeKey` when you have a subspace. Those all break ordering, escaping, or both.

```csharp
// build keys (strongly typed, lazy)
var k = subspace.Key("user", 123);          // prefix + ("user", 123)
Slice value = await tr.GetAsync(k);          // rendered to bytes here, into pooled buffers
tr.Set(subspace.Key("user", 123), FdbValue.FromTuple(("Alice", 30)));
tr.Clear(subspace.Key("user", 123));
```

`.ToSlice()` exists, but only for when you need the *bytes as data* (logging, tests, or storing a key inside a value).

### When the tail isn't known at compile time

For a generic index whose indexed value has an arbitrary type, chain a runtime tuple onto a typed prefix:

```csharp
IVarTuple value = /* built at runtime */;
var indexKey = subspace.Key(INDEXES, indexId).Tuple(value);   // typed prefix (1, idx) + dynamic suffix
```

This is the modern replacement for the older dynamic `subspace.Pack(...)` style.

### Ordered, monotonic keys with VersionStamps

For queues, event logs, and change feeds (anything that needs globally-ordered, collision-free ids), let the database assign a **VersionStamp** at commit time:

```csharp
var stamp = tr.CreateVersionStamp(userVersion);              // an incomplete stamp, filled at commit
tr.SetVersionStampedKey(log.Key(stamp), payload);            // FDB writes the real, monotonic stamp on commit
```

A plain range scan then returns entries in commit order, with no shared counter to contend on. (See [Advanced Layers](advanced-layers.md) for the full change-feed pattern.)

## Ranges and key derivation

Most layers read **ranges**, not single keys. Build ranges from keys and subspaces; never increment bytes by hand.

```csharp
tr.GetRange(subspace.ToRange());                  // everything under the subspace
tr.GetRange(subspace.Key("user", 123).ToRange()); // everything under one prefix
FdbKeyRange.Between(subspace.Key(100), subspace.Key(200));  // [100, 200)
```

Useful derivations (extension methods on any key): `key.Successor()` (the next key, an exclusive lower bound), `key.NextSibling()` (first key that doesn't have `key` as a prefix, an exclusive upper bound over its children), `subspace.First()` / `subspace.Last()`, and the `KeySelector`s `FirstGreaterOrEqual()` / `LastLessOrEqual()`.

## Decoding

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

## Subspaces, locations, and the Directory layer

You should never invent or hard-code a prefix. Instead you declare a logical **path** and let the **Directory layer** map it to a short, dense binary prefix:

```csharp
ISubspaceLocation location = db.Root["Tenants"]["ACME"]["Documents"]["Books"];

await db.WriteAsync(async tr =>
{
    IKeySubspace subspace = await location.Resolve(tr);   // queries the Directory layer
    tr.Set(subspace.Key("BOOK_123"), FdbValue.FromTuple(("Title", "ISBN")));
}, ct);
```

A few things to know:

- **Resolve every transaction.** The prefix is stable in practice but not guaranteed forever; caching it yourself defeats the Directory layer and risks corruption.
- **Resolve opens; it does not create.** `Resolve` throws if the directory does not exist yet. Create it the first time with `location.CreateOrOpenAsync(tr)` in a read-write transaction, which is what a layer does on setup.
- **The `db.Root[...]` indexer descends one *segment* at a time.** `db.Root["a", "b"]` is *not* two segments: the two-argument overload is `(name, layerId)`. Chain the indexer (`db.Root["a"]["b"]`) or pass an `FdbPath`.

### Why paths instead of raw prefixes

Think of a location as a folder in a file system, and the Directory layer as the table that maps a folder path to an i-node number. Your code thinks in readable paths; the database stores a short integer prefix. If `/Tenant/ACME/MyApp/v1/Documents/Books` is assigned prefix `42`, a key in it is stored as `(42, "BOOK_123")` instead of the full `("Tenant", "ACME", "MyApp", "v1", "Documents", "Books", "BOOK_123")`, saving dozens of bytes on every key.

Prefixes are themselves tuple-encoded, so decoding a stored key with `TuPack.Unpack(...)` yields the prefix as the first element followed by your key's own elements (here, `(42, "BOOK_123")`). With **Directory Partitions**, the prefix is several integers, one per partition level, adding a few bytes per level.

In bytes, the complete stored key is just the prefix followed by your own tuple:

```fdb-bytes
tuple: (42, "BOOK_123")
int  .15 2A                # dir prefix · 42
str  .02 'BOOK_123' .00    # string "BOOK_123"
```

That prefix is allocated dynamically and is not known until the directory is first created, so throughout the rest of these docs we fold it into a leading `...` and write a layer's own key as `(..., "BOOK_123")`. It is the same idea as a relative path `./BOOK_123` instead of the absolute `/Tenant/ACME/MyApp/v1/Documents/Books/BOOK_123`: the prefix bytes are still there, we just do not spell out a value that changes per deployment.

```fdb-bytes
tuple: (..., "BOOK_123")
dir  ...                   # dir prefix
str  .02 'BOOK_123' .00    # string "BOOK_123"
```

Only when a page is specifically about the complete key, or about the Directory layer itself, do we spell the prefix out.

## Encoding values

| Need | Use |
|---|---|
| Raw bytes / blob | `FdbValue.ToBytes(slice)` |
| Empty value (index entries) | `FdbValue.Empty` |
| Text | `FdbValue.ToTextUtf8(s)` / `ToTextUtf16(s)` |
| A counter you'll mutate atomically | `FdbValue.ToFixed64LittleEndian(n)` (fixed little-endian is required for `AtomicAdd64`) |
| A tuple | `FdbValue.FromTuple(("a", 1))` |
| JSON document | `FdbValue.ToJson(obj)` (CrystalJson) |

Reading back: `slice.ToInt64()`, `slice.ToStringUtf8()`, `CrystalJson.Deserialize<T>(slice)` (which maps a missing/empty key to `null`), etc.

## Writing a Layer

Rather than scatter database access across controllers and pages, wrap it in a **Layer**. A **Layer** is the FoundationDB equivalent of a small data-access component (a map, an index, a document collection). Every layer in `FoundationDB.Layers.Common` and in the larger SnowBank layers follows the same shape:

1. The layer class is a **thin, reusable wrapper** over an `ISubspaceLocation` (plus codecs/options). It holds **no per-transaction state**.
2. It implements `IFdbLayer<TState>`. `Resolve(tr)` resolves the location and returns a **`State`** holding the resolved `IKeySubspace`. Memoize it in `tr.Context` so repeated `Resolve(tr)` calls in one transaction are cheap.
3. All real work is methods that take a transaction and use the `State`'s subspace to build keys.
4. **The `State` must never escape the transaction**: don't store it in a field or reuse it across retries. (`tr.Context` local data is per-transaction, so memoizing there is safe; a layer field is not.)

### Worked example: a document store with a secondary index

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

### Composing layers in one transaction

Because a layer's methods take a transaction rather than opening their own, one retry loop can drive several layers atomically. Insert a document, queue a background job, and publish an event in the same `WriteAsync`, and either all of them commit or none do:

```csharp
await db.WriteAsync(async tr =>
{
    await books.InsertAsync(tr, book);
    await workers.QueueAsync(tr, new GenerateThumbnails(book.Id));
    await feed.PublishAsync(tr, new BookCreated(book.Id));
}, ct);
```

If the transaction fails to commit, it is as if the request never happened: no document, no job, no event.

### Maintaining a secondary index correctly

Index entries are **derived data**: your code, not the database, keeps them in sync. This is where layers most often go wrong:

- **To change the index you must know the OLD indexed value**, and you can only learn it from the **stored document**, never from an object the caller hands you (it may be stale, leaving an orphaned index entry). `Update`/`Patch`/`Delete` therefore read the current document and derive the old index key from *that*.
- **Mutate the index in the same transaction as the document**, so it can never drift out of sync on a partial failure.
- **Only rewrite the index when the indexed value actually changed.** For frequently-updated documents whose indexed field is stable, this avoids needless writes (and the conflicts they cause).

Concretely, changing a book's author rewrites the document **in place** and **moves** its index entry, both in one transaction. The document keeps its key, so only its value changes; the index key is genuinely different, so the old entry is deleted and a new one inserted:

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

### Making layer keys human-readable

Raw keys are opaque bytes. Tools (the FQL shell, `FdbShell`, dumps, the transaction logger) can render them as friendly tuples if the layer publishes a schema. Implement `IFdbLayerSchemaMapper` (often as a nested class) and return one `FqlTemplateExpression` per key family:

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

With that schema published, a raw key stops being opaque bytes and reads as a friendly tuple. The two families render as (`D`/`I` are the display names for subspaces `0`/`1`, and `...` stands for the resolved Directory prefix):

```fdb-fql
// a book document
(..., D:0, <id:string>) = <json>

// by-author index entry (empty value)
(..., I:1, <author:string>, <id:string>) = ''
```

The value hint can also be a **function of the decoded key** (`(SpanTuple t) => t.Get<string>(0) switch { … }`) when the value's type depends on the key.

## Migrating from the old dynamic key API

Older code used a dynamic subspace API (`IDynamicKeySubspace`, `subspace.Encode(...)` / `.Pack(...)`), which has been replaced by the strongly-typed `subspace.Key(...)` family. Translate mechanically:

| Old (dynamic) | New (typed) |
|---|---|
| `subspace.Encode(a, b, c)` | `subspace.Key(a, b, c)` |
| `subspace.Pack(STuple.Create(a, b).Concat(value))` | `subspace.Key(a, b).Tuple(value)` |
| `subspace.EncodeRange(a, b)` | `subspace.Key(a, b).ToRange()` |
| `global.Partition.ByKey(p)` | `global.Key(p).ToSubspace()` |
| field/return type `IDynamicKeySubspace` | `IKeySubspace` |

## Reference layers to imitate

When in doubt, read the real implementations in `FoundationDB.Layers.Common/`: `FdbMap` (key→value), `FdbIndex` (composite `(value, id)` keys), `FdbVector` (integer index keys), `FdbHighContentionCounter` (write-contention avoidance), `FdbBlob` (chunking large values), `FdbStringIntern` (bidirectional maps).

Next: **[Transactions](transactions.md)** for the retry-loop semantics these layers run inside.
