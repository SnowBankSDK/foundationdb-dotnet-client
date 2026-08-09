---
name: foundationdb-keys-and-layers
description: >-
  How to correctly encode keys and values, use subspaces and the Directory layer, and write custom "Layers"
  with the FoundationDB .NET client (FoundationDB.Client / SnowBank). Covers the lazy strongly-typed key
  structs (subspace.Key(...), FdbTupleKey, FdbRawKey, FdbKey.Increment / Successor / Dump), the tuple encoding
  (TuPack, IVarTuple, STuple), value encodings (FdbValue.ToBytes / ToTextUtf8 / FromTuple / ToJson /
  ToFixed64LittleEndian and which one a given access pattern requires), subspaces and ISubspaceLocation, the
  Directory layer (FdbDirectoryLayer, FdbPath, FdbDirectorySubspace), and the IFdbLayer contract with its
  per-transaction Resolve(tr) State that must never escape the transaction. Use whenever code reads or writes
  FoundationDB keys, builds or decodes a tuple key, picks a value encoding, resolves a subspace or location,
  designs a key layout for a table / secondary index / queue / document collection, reasons about key ordering,
  prefixes and range boundaries, or defines a class that stores data in FoundationDB. Also use it for "how do I
  model X in FoundationDB", for range scans that return too much or nothing, and for keys that are too big.
  Read this BEFORE writing or reviewing any such code.
---

# FoundationDB .NET — Keys, Values & Layers

FoundationDB is a **single, flat, ordered key/value store**. Both keys and values are byte strings. Keys are sorted **lexicographically by their raw bytes**, and that ordering is the *only* structure the database gives you — every "table", "index", "queue", or "document collection" is an illusion built by choosing key bytes carefully. Getting the key encoding right is therefore the whole game. This skill explains the idiomatic, type-safe API this client provides so you don't reinvent (incorrectly) what already exists.

> If you remember one thing: **you almost never touch raw bytes.** You build keys with `subspace.Key(...)` and values with `FdbValue.*`, and you pass those objects *directly* to the transaction. The library renders them to bytes for you, efficiently, at the last moment.

---

## 1. The mental model

- A **subspace** is a key prefix. All keys you create live "inside" it. You get subspaces from a **location** resolved against a transaction (see §6). Never hard-code prefixes.
- **Tuple encoding** (`TuPack`) turns typed elements — strings, integers, GUIDs, booleans, `VersionStamp`, … — into bytes whose **byte order matches the logical order** of the values. `(42, "hello")` always sorts before `(42, "world")` and before `(43, …)`. This order-preservation is why tuples are the default key encoding.
- A **key** in this API is a small **struct** (e.g. `FdbTupleKey<string,int>`) that *remembers its subspace and its elements* and knows how to render itself. It is **lazy**: no bytes exist until the transaction asks for them.
- A **value** is likewise a lightweight encodable (e.g. `FdbRawValue`, `FdbVarTupleValue`, `FdbJsonValue`) produced by `FdbValue.*`.

---

## 2. Golden rules (this is where vibe-coded usage goes wrong)

✅ **DO**

- Build keys with `subspace.Key(a, b, c)` and pass the result straight into `tr.GetAsync(...)` / `tr.Set(...)`.
- Build values with `FdbValue.ToBytes(...)`, `FdbValue.FromTuple(...)`, `FdbValue.ToTextUtf8(...)`, `FdbValue.ToFixed64LittleEndian(...)`, `FdbValue.ToJson(...)`.
- Decode keys with `subspace.Decode<T1,...>(key)` / `DecodeLast<T>` and values with the matching `FdbValue`/`TuPack` reader.
- Get a subspace by `await location.Resolve(tr)` *inside* the transaction.
- Use atomic mutations (`tr.AtomicAdd64`, `AtomicIncrement64`, …) for counters instead of read-modify-write.

❌ **DON'T**

- ❌ Don't eagerly serialize: `var k = subspace.Key("a", 1).ToSlice(); tr.Set(k, ...)`. Pass the key object itself; `.ToSlice()` forces an allocation you don't need. (`.ToSlice()` is for tests/logging/storing a key as a *value*.)
- ❌ Don't build keys by hand with string concatenation, `Encoding.UTF8.GetBytes`, `BitConverter`, `+`, or manual separators (`"\x00"`, `":"`, `"/"`). You will break ordering and escaping. Use tuples.
- ❌ Don't hard-code or cache a subspace **prefix** across transactions. Prefixes can change (Directory layer); resolve every transaction.
- ❌ Don't cache the resolved subspace / layer `State` outside the transaction that produced it (see §7).
- ❌ Don't reach for `TuPack.EncodeKey(...)` to make database keys directly when you have a subspace — `subspace.Key(...)` already applies the prefix and tuple-encodes. (`TuPack` is the lower-level primitive; use it for values or when you genuinely have no subspace.)
- ❌ Don't store a `Guid` in a key without realizing it uses the custom tuple type codes (`0x30` for 128-bit) — that's handled for you by tuple encoding; just don't re-encode it as bytes yourself.
- ❌ Don't use raw, unnamed literals to discriminate the sub-parts of a subspace (data vs index vs metadata). Use **named constants**, and prefer **small integers** for compactness: `0` packs to 1 byte (`0x14`), `1` to 2 bytes (`0x15 0x01`), whereas `"D"` costs 3 bytes (`0x02 'D' 0x00`) on every key. (Short *named* strings like `"T"`/`"S"` are a legitimate choice some layers make for readability in raw dumps — just be deliberate and consistent; see §7.)
- ❌ Don't trust a caller-supplied entity to locate **index** keys for an update/delete. Its indexed fields may be stale, leaving orphaned index entries. Read the stored document and derive the old index key from *that* (see §7).

---

## 3. Building keys

`subspace.Key(...)` is the workhorse. It packs the subspace prefix followed by the tuple-encoded arguments, and returns a strongly-typed key:

```csharp
IKeySubspace subspace = /* resolved, see §6 */;

FdbTupleKey<string>            k1 = subspace.Key("hello");                 // prefix + ("hello",)
FdbTupleKey<string, int>       k2 = subspace.Key("hello", 123);           // prefix + ("hello", 123)
FdbTupleKey<string, int, Guid> k3 = subspace.Key("user", 123, userId);
FdbSubspaceKey                 kp = subspace.Key();                        // the prefix itself (a key)
```

Other constructors:

```csharp
// From an existing tuple value (STuple or ValueTuple):
subspace.Tuple(STuple.Create("hello", 123));
subspace.Tuple(("hello", 123));

// A raw binary suffix appended to the subspace (NOT tuple-encoded) — use only for interop
// with externally-defined byte layouts:
FdbSuffixKey raw = subspace.Bytes(someReadOnlySpanOfBytes);

// A fully pre-encoded standalone key (no subspace):
FdbRawKey r = FdbKey.FromBytes(slice);
FdbTupleKey<string,int> t = FdbKey.FromTuple(("hello", 123));   // packs in the root keyspace

// System / special keys (\xFF...):
FdbSystemKey sys = FdbKey.ToSystemKey("/metadataVersion");
```

Keys implement `IFdbKey` and are accepted directly by every transaction method. The key is rendered into pooled buffers (or the stack, for short keys) inside the call — you don't manage that.

```csharp
Slice value = await tr.GetAsync(subspace.Key("user", 123));
tr.Set(subspace.Key("user", 123), FdbValue.FromTuple(("Alice", 30)));
tr.Clear(subspace.Key("user", 123));
```

`.ToSlice()` exists, but only use it when you need the *bytes as data* (logging, tests, or storing a key inside a value).

### Typed prefix + a runtime tuple (dynamic tails)

When part of the key is fixed and typed but the **tail isn't known at compile time** — e.g. a generic index whose indexed value is an arbitrary `IVarTuple` — chain `.Tuple(...)` onto a typed `.Key(...)`:

```csharp
IVarTuple value = collation.Collate(rawValue);          // built at runtime, arbitrary element types
var indexKey = subspace.Key(INDEXES, indexId).Tuple(value);   // typed prefix (1, idx) + dynamic suffix
```

`subspace.Key(a, b)` gives the strongly-typed prefix; `.Tuple(ivarTuple)` appends the runtime elements. This is the idiomatic replacement for hand-packing — and for the **old** `subspace.Pack(STuple.Create(a, b).Concat(value))` style (see §11). Convert a key into a child subspace with `key.ToSubspace()` (e.g. `subspace.Key(tenantId).ToSubspace()` to partition by tenant).

### VersionStamp keys — globally-ordered, monotonic ids

For queues, event logs, inboxes — anything that needs **insertion-ordered, collision-free** keys across all writers — append a **VersionStamp** assigned by the database **at commit time**:

```csharp
var stamp = tr.CreateVersionStamp(userVersion);          // an *incomplete* stamp (filled in at commit)
tr.SetVersionStampedKey(inbox.Key(stamp), payload);      // FDB writes the real, monotonic stamp on commit
```

- The stamp is monotonic and globally ordered, so a plain range scan returns items in commit order — no client-side sequence number, no contention on a shared counter.
- `CreateVersionStamp(int userVersion)` (vs the arg-less form) disambiguates **multiple** stamped keys written in the **same** transaction — pass an incrementing `userVersion` per item.
- You must use `SetVersionStampedKey` (not `Set`) so the database knows to substitute the real stamp; the key you build carries the *incomplete* stamp as a placeholder.
- To put the stamp in the **value** instead (e.g. a "last write" marker), use `tr.SetVersionStampedValue(key, tr.CreateVersionStamp())`.
- Read it back with `VersionStamp.ReadFrom(...)` / `subspace.Decode<…, VersionStamp>(key)`, and trim consumed entries with a cursor range (`key.ToHeadRangeInclusive(cursor)`).

---

## 4. Ranges & key derivation

Most layers read **ranges**, not single keys. Build ranges from keys/subspaces — never by manually incrementing bytes.

```csharp
// Everything under the subspace:
tr.GetRange(subspace.ToRange());                       // prefix\x00 .. prefix\xFF  (children)
subspace.ToRange(inclusive: true);                     // also includes the prefix key itself

// Everything under a key prefix (e.g. all entries for one user):
tr.GetRange(subspace.Key("user", 123).ToRange());      // ("user",123, *) 

// Explicit bounds:
FdbKeyRange.Single(subspace.Key(123));                 // exactly that one key
FdbKeyRange.Between(subspace.Key(100), subspace.Key(200));   // [100, 200)
subspace.ToHeadRange(cursor);                          // subspace start .. cursor
subspace.ToTailRange(cursor);                          // cursor .. subspace end
```

Derivation helpers (extension methods on any key) — these produce *new keys/selectors* with correct byte math:

| Helper | Meaning |
|---|---|
| `key.Successor()` | `key + \x00` — the immediately following key (e.g. exclusive lower bound) |
| `key.NextSibling()` | first key that does **not** have `key` as a prefix (i.e. `Increment`) — exclusive upper bound over `key`'s children |
| `subspace.First()` / `subspace.Last()` | first / last possible child key of the subspace |
| `key.FirstGreaterOrEqual()` / `key.LastLessOrEqual()` | `KeySelector`s for `GetKey`/range bounds |

Idiom for "all index entries with value `v`, ordered":

```csharp
// inclusive range over (v, *):
tr.GetRangeKeys(subspace.Key(v).ToRange(inclusive: true), subspace, (s, k) => s.DecodeLast<TId>(k)!);
```

---

## 5. Decoding keys back to values

When you read a range, you get raw key bytes. Turn them back into typed values with the **same subspace** that produced them:

```csharp
foreach (var kv in chunk)
{
    // full key (elements are nullable: STuple<string?, int?>):
    var (name, id) = subspace.Decode<string, int>(kv.Key);
    // just the first / last element of the tuple after the prefix:
    int id2  = subspace.DecodeLast<int>(kv.Key);
    string n = subspace.DecodeFirst<string>(kv.Key);
    // or the whole thing as a tuple:
    IVarTuple t = subspace.Unpack(kv.Key);
}
```

`Decode<T...>` strips the subspace prefix and tuple-decodes the remainder. Use it; don't slice bytes by hand.

---

## 6. Subspaces, locations, and the Directory layer

You should **never** invent a prefix. You declare a logical **path** and let the **Directory layer** map it to a short, dense binary prefix:

```csharp
// A location is a logical pointer; resolving it yields the actual subspace+prefix for THIS transaction.
// db.Root is indexed by path SEGMENT — chain the indexer to descend one segment at a time:
ISubspaceLocation location = db.Root["Tenants"]["ACME"]["Documents"]["Books"];
// or build the path explicitly:
//   db.Root[FdbPath.Relative("Tenants", "ACME", "Documents", "Books")]

await db.WriteAsync(async tr =>
{
    IKeySubspace subspace = await location.Resolve(tr);   // queries the Directory layer, caches per-tx
    tr.Set(subspace.Key("BOOK_123"), FdbValue.FromTuple(("Title", "ISBN")));
}, ct);
```

> ⚠️ **Indexer gotcha:** `db.Root["a", "b"]` is **not** two path segments — the 2-string overload is `this[string name, string layerId]`, so `"b"` is interpreted as a *layer id* on segment `"a"`. For multiple segments, **chain** the indexer (`db.Root["a"]["b"]`) or pass an `FdbPath`.

- `location.Resolve(tr)` returns the resolved `IKeySubspace`. `TryResolve(tr)` returns `null` if the directory doesn't exist yet.
- The resolved prefix (e.g. two bytes like `\x15\x2A`) is shared by every key in the subspace, so keys stay tiny.
- **Resolve every transaction.** The mapping is stable in practice but is *not* guaranteed forever; caching the prefix yourself defeats the Directory layer and risks corruption.
- For ad-hoc/manual prefixes (tests, fixed system areas) you can use `KeySubspace.FromKey(prefix)` or `db.Root.WithPrefix(...)`, but prefer named paths in real applications.

---

## 7. Writing a custom Layer

A **Layer** is the FoundationDB equivalent of a small data-access component (a map, an index, a queue, a document collection). The canonical shape, used by every layer in `FoundationDB.Layers.Common`, is:

1. The layer class is a **thin, reusable wrapper** over an `ISubspaceLocation` (+ codecs/options). It holds **no per-transaction state**.
2. It implements `IFdbLayer<TState>`. `Resolve(tr)` resolves the location and returns a **`State`** object that holds the resolved `IKeySubspace` (and a back-reference to the layer). Memoize it in `tr.Context` (via `TryGetLocalData`/`GetOrCreateLocalData`) so repeated `Resolve(tr)` calls within one transaction are free — every production layer does this.
3. All real work is methods that take an `IFdbTransaction` / `IFdbReadOnlyTransaction` and use the `State`'s subspace to build keys.
4. **The `State` must never escape the transaction** — don't store it in a field, don't return it to the caller, don't reuse it in the next retry. (`tr.Context` local data is per-transaction, so memoizing there is safe; a layer field is not.)

### Worked example — a typed document collection

```csharp
using FoundationDB.Client;
using SnowBank.Data.Tuples;   // STuple, TuPack
using SnowBank.Data.Json;     // CrystalJson
using SnowBank.Linq;          // IAsyncQuery

/// <summary>Stores Book documents (as JSON) keyed by their Id, plus a secondary index by author.</summary>
public sealed class BookStore : IFdbLayer<BookStore.State>
{
    // Discriminate the sub-parts of the subspace with small INTEGER constants, not strings.
    // Tuple-packed, 0 is 1 byte (0x14) and 1 is 2 bytes (0x15 0x01); the string "D" would be
    // 3 bytes (0x02 'D' 0x00) on *every* key. Name them so call sites read clearly.
    private const int SUBSPACE_DOCUMENTS = 0;      // (0, <id>)            -> json document
    private const int SUBSPACE_INDEX_AUTHOR = 1;   // (1, <author>, <id>) -> empty (index entry)

    /// <summary>LayerId advertised to the Directory layer, linking subspaces to the SchemaMapper.</summary>
    public const string LayerId = "docstore.Books";

    public BookStore(ISubspaceLocation location)
    {
        this.Location = location;
    }

    public ISubspaceLocation Location { get; }

    public string Name => nameof(BookStore);

    private const string LocalDataKey = nameof(BookStore);

    // Resolve once per transaction; memoize in the transaction's local data so repeated
    // Resolve(tr) calls in the same tx reuse the same State. Never cache it OUTSIDE the tx.
    public ValueTask<State> Resolve(IFdbReadOnlyTransaction tr)
    {
        if (tr.Context.TryGetLocalData(LocalDataKey, out State? state))
        {
            return new ValueTask<State>(state);
        }
        return ResolveSlow(this, tr);

        static async ValueTask<State> ResolveSlow(BookStore self, IFdbReadOnlyTransaction tr)
        {
            var subspace = await self.Location.Resolve(tr);
            return tr.Context.GetOrCreateLocalData(LocalDataKey, new State(self, subspace));
        }
    }

    public sealed class State
    {
        private readonly BookStore Layer;
        public IKeySubspace Subspace { get; }

        internal State(BookStore layer, IKeySubspace subspace)
        {
            this.Layer = layer;
            this.Subspace = subspace;
        }

        /// <summary>Inserts a brand-new book (assumes the Id does not already exist).</summary>
        public void Insert(IFdbTransaction tr, Book book)
        {
            tr.Set(this.Subspace.Key(SUBSPACE_DOCUMENTS, book.Id), FdbValue.ToJson(book));
            tr.Set(this.Subspace.Key(SUBSPACE_INDEX_AUTHOR, book.Author, book.Id), FdbValue.Empty);
        }

        /// <summary>Reads a book by Id, or null if missing.</summary>
        public async Task<Book?> GetAsync(IFdbReadOnlyTransaction tr, string id)
        {
            var bytes = await tr.GetAsync(this.Subspace.Key(SUBSPACE_DOCUMENTS, id));
            // CrystalJson.Deserialize maps a Nil/empty slice (missing key) to null already —
            // no `bytes.IsNull ? null : ...` guard needed.
            return CrystalJson.Deserialize<Book>(bytes);
        }

        /// <summary>Updates a book by re-reading the stored document to learn the old indexed value.
        /// Use when the caller does NOT already hold the original. The re-read costs no extra network
        /// round-trip if the key was already read in this tx, but does re-deserialize the JSON.</summary>
        public async Task UpdateAsync(IFdbTransaction tr, Book book)
        {
            Book? old = CrystalJson.Deserialize<Book>(
                await tr.GetAsync(this.Subspace.Key(SUBSPACE_DOCUMENTS, book.Id)));

            tr.Set(this.Subspace.Key(SUBSPACE_DOCUMENTS, book.Id), FdbValue.ToJson(book));
            ReindexAuthor(tr, book.Id, old?.Author, book.Author);
        }

        /// <summary>Updates a book when the caller ALREADY holds the original.
        /// CONTRACT: <paramref name="original"/> MUST be the exact value returned by GetAsync(...) on
        /// THIS SAME transaction — that read registers the conflict that keeps the index consistent.
        /// Passing a stale or foreign "original" WILL corrupt the index. No read is done here.</summary>
        public Task UpdateAsync(IFdbTransaction tr, Book updated, Book original)
        {
            tr.Set(this.Subspace.Key(SUBSPACE_DOCUMENTS, updated.Id), FdbValue.ToJson(updated));
            ReindexAuthor(tr, updated.Id, original.Author, updated.Author);
            return Task.CompletedTask;
        }

        /// <summary>Reads, mutates and saves a book in one call. <paramref name="patch"/> gets the current
        /// document and returns the modified copy (records: a `with` expression). Nothing is written if the
        /// patch is a no-op; the index is only touched if the author changed. Ideal for cheap field bumps.</summary>
        public async Task<Book?> PatchAsync(IFdbTransaction tr, string id, Func<Book, Book> patch)
        {
            Book? current = CrystalJson.Deserialize<Book>(
                await tr.GetAsync(this.Subspace.Key(SUBSPACE_DOCUMENTS, id)));
            if (current is null) return null; // nothing to patch

            Book updated = patch(current);

            if (updated == current) return current; // no-op: records compare by value -> skip all writes

            if (!string.Equals(updated.Id, id, StringComparison.Ordinal))
                throw new InvalidOperationException("A patch must not change the document Id.");

            tr.Set(this.Subspace.Key(SUBSPACE_DOCUMENTS, id), FdbValue.ToJson(updated));
            ReindexAuthor(tr, id, current.Author, updated.Author);
            return updated;
        }

        /// <summary>Deletes a book by Id (and its index entry). Takes only the Id — see note below.</summary>
        public async Task<bool> DeleteAsync(IFdbTransaction tr, string id)
        {
            // Trust ONLY the stored document for the indexed value, never a caller-supplied Book.
            Book? existing = CrystalJson.Deserialize<Book>(
                await tr.GetAsync(this.Subspace.Key(SUBSPACE_DOCUMENTS, id)));
            if (existing is null) return false; // nothing to delete

            tr.Clear(this.Subspace.Key(SUBSPACE_DOCUMENTS, id));
            tr.Clear(this.Subspace.Key(SUBSPACE_INDEX_AUTHOR, existing.Author, id));
            return true;
        }

        /// <summary>Ids of all books by an author, in order.</summary>
        public IAsyncQuery<string> FindIdsByAuthor(IFdbReadOnlyTransaction tr, string author)
        {
            // range over (1, author, *), pull the <id> back out of each key
            return tr
                .GetRange(this.Subspace.Key(SUBSPACE_INDEX_AUTHOR, author).ToRange())
                .Select(kv => this.Subspace.DecodeLast<string>(kv.Key)!);
        }

        // Moves the author index entry for `id`, but only when the indexed value actually changed.
        private void ReindexAuthor(IFdbTransaction tr, string id, string? oldAuthor, string newAuthor)
        {
            if (oldAuthor is null)
            {
                tr.Set(this.Subspace.Key(SUBSPACE_INDEX_AUTHOR, newAuthor, id), FdbValue.Empty);
            }
            else if (!string.Equals(oldAuthor, newAuthor, StringComparison.Ordinal))
            {
                tr.Clear(this.Subspace.Key(SUBSPACE_INDEX_AUTHOR, oldAuthor, id));
                tr.Set(this.Subspace.Key(SUBSPACE_INDEX_AUTHOR, newAuthor, id), FdbValue.Empty);
            }
            // else: unchanged -> leave the index alone (no wasted write, no extra conflict)
        }
    }

    /// <summary>Describes this layer's key layout so tools (db dumps, the FQL shell, loggers) can render
    /// raw keys as friendly tuples. Any directory subspace tagged with <see cref="LayerId"/> uses these rules.</summary>
    public sealed class SchemaMapper : IFdbLayerSchemaMapper
    {
        public string LayerId => BookStore.LayerId;

        public IEnumerable<FqlTemplateExpression> GetRules()
        {
            // (0, <id:string>) -> a JSON document
            yield return new FqlTemplateExpression(
                "document",
                FqlTupleExpression.Create().Integer(SUBSPACE_DOCUMENTS).VarString("id"),
                FdbValueTypeHint.Json);

            // (1, <author:string>, <id:string>) -> empty index entry
            yield return new FqlTemplateExpression(
                "index.author",
                FqlTupleExpression.Create().Integer(SUBSPACE_INDEX_AUTHOR).VarString("author").VarString("id"),
                FdbValueTypeHint.None);
        }
    }
}

public sealed record Book
{
    public required string Id { get; init; }
    public required string Author { get; init; }
    public required string Title { get; init; }
}
```

### Maintaining a secondary index correctly

The index entries (`(1, <author>, <id>)`) are **derived data** — your code, not the database, keeps them in sync with the documents. This is where layers most often go wrong:

- **Prefix sub-parts with named constants — prefer small integers.** `subspace.Key(SUBSPACE_DOCUMENTS, id)` packs the discriminator in **1 byte**; `subspace.Key("D", id)` costs **3 bytes on every key** (and every index key). Define `private const int …` for compactness. Short named strings are a valid readability tradeoff (some production layers use `"T"`/`"S"`), but never inline raw literals.
- **To change the index you must know the OLD indexed value.** And you can only obtain it from the **stored document**, never from an object the caller hands you (it may be stale or mutated, which would leave an **orphaned index entry**). The shared `ReindexAuthor(...)` helper moves the entry *only when the value actually changed*, so an update that doesn't touch the author writes nothing to the index.
- Every index mutation happens in the **same transaction** as the document mutation, so the index can never drift out of sync on a partial failure.

The example offers three update flavors, trading a read against caller obligations:

| Method | Reads the old doc? | When to use |
|---|---|---|
| `UpdateAsync(tr, book)` | yes (re-reads) | Caller built a fresh `Book` and doesn't hold the original. Simple and safe. The re-read is free on the wire if the key was already read this tx, but re-deserializes. |
| `UpdateAsync(tr, updated, original)` | no | Caller already read `original` via `GetAsync` **in the same transaction**. Skips the read. ⚠️ Contract: if `original` isn't that exact stored value, the index corrupts. |
| `PatchAsync(tr, id, patch)` | yes | Cheap field bumps (`LastAccessed`, `UseCount`). The callback returns the new value; a no-op (`updated == current`, by record value-equality) writes nothing at all. |

> The "blind" alternative — always clear+set the index without reading the old value — is only safe if the indexed value can never change (e.g. it's derived solely from the id). Otherwise you'd orphan the previous entry. When in doubt, read.

### Making layer keys human-readable (`IFdbLayerSchemaMapper`)

Raw FoundationDB keys are opaque bytes. Tools like the FQL shell, `FdbShell`, database dumps, and the transaction logger can render them as friendly tuples *if* the layer publishes a **schema**. Implement `IFdbLayerSchemaMapper` (often as a nested class) and return one `FqlTemplateExpression` per key family from `GetRules()`:

- `LayerId` ties the rules to any Directory subspace created with that layer id (the `layer` argument to `CreateOrOpenAsync`), so the tooling knows which mapper to apply. Use a namespaced id like `"Acme:Documents:Collection"`.
- Build each template with `FqlTupleExpression.Create()` and the fluent API:
  - constants: `.Integer(0)` / `.String("…")`; name a constant for nicer output with `.Integer(CHUNKS, "CHUNKS")`.
  - captures: `.VarString("id")`, `.VarInteger("n")`, `.VarUuid("id")`, `.VarAny("value")` (any type).
  - `.MaybeMore()` for a variadic tail (matches zero or more trailing elements).
- The third `FqlTemplateExpression` argument is the **value** hint. It can be a fixed `FdbValueTypeHint` *or* a **function of the decoded key** when the value type depends on the key:

```csharp
yield return new(
    "GlobalAttributes",
    FqlTupleExpression.Create().VarString("attr").MaybeMore(),
    (SpanTuple t) => t.Get<string>(0) switch     // hint chosen from the key's first element
    {
        "Count" or "SchemaVersion" => FdbValueTypeHint.IntegerLittleEndian,
        "Name" or "Type"           => FdbValueTypeHint.Utf8,
        _                          => FdbValueTypeHint.None,
    });
```

In the example above, `BookStore.SchemaMapper` declares `(0, <id>) → Json` and `(1, <author>, <id>) → (empty)`, so a dump shows the keys under the `document` / `index.author` templates instead of raw bytes. A layer can expose several mappers (e.g. one for the collection subspace, one for a database-level subspace). (The FQL types require `net8.0`+.)

### Using the layer

The `IFdbLayer<TState>` extension helpers resolve the state for you and run it inside a retry loop:

```csharp
var store = new BookStore(db.Root["Documents"]["Books"]);

// write
await store.WriteAsync(db, (tr, state) =>
    state.Insert(tr, new Book { Id = "B1", Author = "Asimov", Title = "Foundation" }), ct);

// read
Book? b = await store.ReadAsync(db, (tr, state) => state.GetAsync(tr, "B1"), ct);
```

Or compose multiple layers in **one** transaction by resolving each inside the same handler (this is the whole point of layers — atomic composition):

```csharp
await db.WriteAsync(async tr =>
{
    var books   = await bookStore.Resolve(tr);
    var counter = await statsCounter.Resolve(tr);
    books.Insert(tr, newBook);
    counter.Add(tr, 1);          // both commit together, or neither does
}, ct);
```

### Parameterized resolution — `IFdbLayer<TState, TOptions>`

When `Resolve` needs an argument — most commonly a **tenant** in a multi-tenant layer — implement the two-type-param interface `IFdbLayer<TState, TOptions>`, whose `Resolve(tr, options)` takes that argument. The retry-loop helpers gain an `options` parameter too:

```csharp
public sealed class TenantCounter : IFdbLayer<TenantCounter.State, TenantToken>
{
    public async ValueTask<State> Resolve(IFdbReadOnlyTransaction tr, TenantToken tenant)
    {
        var global = await this.Location.Resolve(tr);
        var subspace = global.Key(tenant.Id).ToSubspace();   // partition the subspace by tenant
        return new State(subspace);
    }
    // ...
}

await counter.WriteAsync(db, tenant, (tr, state) => state.Bump(tr, "hits"), ct);
```

This is how a production document-collection layer resolves a per-tenant subspace under a shared collection location.

---

## 8. Encoding values

| Need | Use | Notes |
|---|---|---|
| Raw bytes / blob | `FdbValue.ToBytes(slice)` | also `ReadOnlySpan<byte>`, `MemoryStream` |
| Empty value (index entries) | `FdbValue.Empty` | a `Slice`; index keys often carry no value |
| A tuple | `FdbValue.FromTuple(("a", 1))` | order-preserving (rarely needed in *values*) |
| Text | `FdbValue.ToTextUtf8(s)` / `ToTextUtf16(s)` | |
| Counter / number you'll mutate atomically | `FdbValue.ToFixed64LittleEndian(n)` | **little-endian fixed-width** is required for `AtomicAdd64` |
| Compact integer | `FdbValue.ToCompactLittleEndian(n)` | variable length |
| GUID | `FdbValue.ToUuid128(g)` | |
| JSON document | `FdbValue.ToJson(obj)` / `ToJson<T>(obj)` | uses CrystalJson |

Reading values: `slice.ToInt64()`, `slice.ToStringUtf8()`, `TuPack.DecodeKey<long>(slice)`, `CrystalJson.Deserialize<T>(slice)`, etc.

> **Counters:** store with `FdbValue.ToFixed64LittleEndian` (or `Slice.FromFixed64`) and mutate with `tr.AtomicAdd64(key, delta)` / `tr.AtomicIncrement64(key)`. This avoids read-modify-write conflicts entirely. See `FdbCounterMap` and `FdbHighContentionCounter`.

> **The value encoding you pick decides which atomic comparison is legal on it.** `AtomicMin`/`AtomicMax` compare little-endian, so they are only correct on a fixed-width little-endian number. For any value whose ordering is byte-lexicographic (a tuple encoding, UTF-8 text, a UUID, a `VersionStamp`) the correct mutation is `FdbMutationType.ByteMin` / `ByteMax` via `tr.Atomic(...)`. Getting this pair backwards silently stores the wrong value rather than failing. The `foundationdb-transactions` skill has the full rule.

---

## 9. Reference layers to imitate

When in doubt, read the real implementations in `FoundationDB.Layers.Common/` — they are the ground truth for idiomatic usage:

| Layer | File | Teaches |
|---|---|---|
| Map (dictionary) | `Collections/FdbMap`2.cs` | basic key→value, codecs, range scan |
| Index (inverted) | `Indexes/FdbIndex`2.cs` | composite `(value, id)` keys, empty values, `DecodeLast`, ordered lookups |
| Multimap | `Collections/FdbMultimap`2.cs` | counted set, atomic inc/dec, `clearIfZero` |
| Vector (sparse array) | `Collections/FdbVector`1.cs` | integer index keys, reverse range, pop/push |
| Queue | `Collections/FdbQueue`1.cs` | FIFO with `VersionStamp`-style ordering |
| Ranked set | `Collections/FdbRankedSet.cs` | skip-list over keys |
| Counter (sharded) | `Counters/FdbHighContentionCounter.cs` | write-contention avoidance, random shard keys, coalescing |
| Counter map | `Counters/FdbCounterMap`2.cs` | `AtomicAdd64` per key |
| Blob | `Blobs/FdbBlob.cs` | chunking a large value across many keys, offset keys |
| String interning | `Interning/FdbStringIntern.cs` | bidirectional id↔string maps with a cache |

For the transaction/retry-loop semantics these layers run inside (idempotency, the 5-second limit, conflicts, atomic ops), see the **`foundationdb-transactions`** skill.

---

## 10. Quick self-check before you commit key code

- [ ] Did I build keys with `subspace.Key(...)` (not string/byte concatenation)?
- [ ] Am I passing the key **object** to `tr`, not an eagerly-computed `.ToSlice()`?
- [ ] Did I resolve the subspace **inside** the transaction via `location.Resolve(tr)`?
- [ ] Is my `State`/subspace confined to the transaction (not cached in a field)?
- [ ] Did I decode with `subspace.Decode<...>` instead of slicing bytes?
- [ ] Are sub-part discriminators **small integer constants** (not string literals)?
- [ ] For secondary indexes, do `Update`/`Delete` derive the old indexed value from the **stored document**, not from a caller-supplied object, and mutate the index in the same transaction?
- [ ] For counters, am I using fixed-LE values + `AtomicAdd64` rather than read-modify-write?

---

## 11. Migrating from the old dynamic key API

Older code (and older docs) used a **dynamic** subspace API — `IDynamicKeySubspace` with `subspace.Encode(...)` / `.Pack(...)` / `.EncodeRange(...)`, and `subspace.Partition.ByKey(...)`. That API has been **replaced** by the strongly-typed `subspace.Key(...)` family. If you're porting a pre-revamp layer, translate mechanically:

| Old (dynamic `IDynamicKeySubspace`) | New (typed `IKeySubspace`) |
|---|---|
| `subspace.Encode(a, b, c)` | `subspace.Key(a, b, c)` |
| `subspace.Encode(name)` | `subspace.Key(name)` |
| `subspace.Pack(STuple.Create(a, b).Concat(value))` | `subspace.Key(a, b).Tuple(value)`  *(typed prefix + runtime `IVarTuple`)* |
| `subspace.EncodeRange(a, b)` | `subspace.Key(a, b).ToRange()` |
| `subspace.Encode(a, b, TuPackUserType.System)` (upper bound) | `subspace.Key(a, b, TuPackUserType.System)` or `subspace.Key(a, b).Last()` |
| `KeyRange.Create(begin, end)` from two `Encode(...)`s | `FdbKeyRange.Between(keyA, keyB).ToKeyRange()` |
| `subspace.DecodeLast<long, int, int>(packed)` | unchanged — `subspace.DecodeLast<…>(packed)` |
| `global.Partition.ByKey(tenant.Prefix)` | `global.Key(tenant.Prefix).ToSubspace()` |
| field/return type `IDynamicKeySubspace` | `IKeySubspace` |

Notes:
- The biggest win is **type safety**: `subspace.Key(CHUNKS, rid, chunkId, count)` returns `FdbTupleKey<int,long,int,int>`, caught at compile time, and stays lazy until handed to `tr`.
- Keep using `subspace.Key(prefix...).Tuple(runtimeTuple)` wherever the old code packed a fixed prefix followed by a runtime `IVarTuple` (the common case for generic indexes).
- Scalar metadata setters (`tr.SetValueString/Int32/Int64/...`) are unchanged and still the most convenient way to write scalar values.

## 12. Advanced techniques (production layers)

Patterns seen in mature layers; reach for them when you actually need them:

- **Boundary keys to isolate range scans.** Writing an empty value at the subspace prefix and at a trailing system sentinel — `tr.Set(subspace.GetPrefix(), Slice.Empty)` and `tr.Set(subspace.Key(TuPackUserType.System), Slice.Empty)` — fences `GetRange` over the subspace from a neighbouring subspace's keys, so an empty collection still has stable range boundaries.
- **Compact internal ids.** Instead of repeating a long external `PrimaryKey` in every key, allocate a small monotonic **record id** (e.g. via `FdbHighContentionAllocator`) and store `document(recordId) → data` plus `index(externalKey) → recordId`. Keys stay tiny and indexes point at the compact id. (See `FdbHighContentionAllocator`; a document-collection layer allocates its record ids this way.)
- **Cross-transaction metadata cache (`TCache`).** A layer may cache expensive resolved metadata (schema, index map) across transactions — but it must **re-validate** on each use: compare the cached prefix to the freshly resolved one (`cached.Prefix.Equals(subspace.GetPrefix())`) and/or attach FDB value-checks, dropping the cache if the directory moved. Only the *cache* is long-lived; the per-transaction `State` is still rebuilt each transaction (see `IFdbLayer<TState>`'s remarks: cache a `TCache`, not the `TState`).
