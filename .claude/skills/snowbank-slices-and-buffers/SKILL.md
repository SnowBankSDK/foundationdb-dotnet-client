---
name: snowbank-slices-and-buffers
description: How to correctly use the Slice type and its companions (SliceReader, SliceWriter, SliceOwner) for binary data in the FoundationDB .NET client / SnowBank.Core codebase. Slice is a readonly struct (namespace System) — the logical equivalent of a ReadOnlyMemory of bytes with many helpers. Use whenever code constructs or reads a Slice, converts between bytes and other types (Slice.FromBytes/FromStringUtf8/FromInt32/FromFixed64/ToInt64/ToStringUtf8/AsSlice/ToArray), builds or parses a binary buffer (SliceWriter/SliceReader), rents pooled buffers (SliceOwner/ArrayPool), or worries about Nil-vs-Empty, endianness, or which integer encoding to use. For the Span-of-byte (Span-first) equivalents and the low-level buffer/pool machinery, see the bundled reference files.
---

# Slice, SliceReader, SliceWriter & friends

`Slice` is the workhorse for binary data in this codebase. It is a **`readonly struct`** (in namespace `System`) that wraps a segment of a `byte[]` — its three fields are `Array` (the backing array, possibly null), `Offset`, and `Count`. It predates `Span<T>` and is the logical equivalent of **`ReadOnlyMemory<byte>`**, but with a large library of helpers for turning bytes into and out of real-world types. Keys and values in the FoundationDB binding are `Slice`s.

> **Two things to internalize first:** (1) a `Slice` is a **view**, not a copy — it shares the backing array. (2) `Slice.Nil` (no array) and `Slice.Empty` (zero-length array) are **different** and the distinction is load-bearing. Both are covered below.

For the Span-first equivalents (`SpanReader`/`SpanWriter`, `ISpanEncodable`) read [`references/span-readers-writers.md`](references/span-readers-writers.md); for pooled buffer-building (`ISliceBufferWriter`, `SlicePool`, `ValueBuffer<T>`, allocators) read [`references/buffers-and-pooling.md`](references/buffers-and-pooling.md).

## 1. Nil vs Empty — the #1 gotcha

| | `Slice.Nil` | `Slice.Empty` |
|---|---|---|
| backing array | none (null-like) | a zero-length array |
| `IsNull` | `true` | `false` |
| `IsEmpty` | `false` | `true` |
| `IsNullOrEmpty` | `true` | `true` |
| `IsPresent` | `false` | `true` |
| `GetBytes()` | returns **`null`** | returns an **empty array** |
| `ToStringUtf8()` | returns **`null`** | returns **`""`** |
| `==` | `Nil != Empty` | distinct |
| `CompareTo` | `Nil` and `Empty` compare **equal** (both sort first) |

`tr.GetAsync(key)` returns **`Slice.Nil`** for a missing key, so the canonical "does it exist?" check is `value.IsNull` (or `IsNullOrEmpty` if an empty value also counts as absent). Use `Nil` to mean *absent* and `Empty` to mean *present but zero-length*.

```csharp
var v = await tr.GetAsync(key);
if (v.IsNull) { /* key does not exist */ }
```

## 2. Slice is a view — copy when you must own it

Constructing a `Slice` from a `byte[]` does **not** copy; the `Slice` references the array, so mutations to the array are visible through the slice (and its `.Span`). When you need an independent owner, copy:

```csharp
byte[] buf = ...;
var view = buf.AsSlice();        // shares buf — buf[i] = x is visible through view
byte[] mine = view.ToArray();    // defensive copy
buf[0] = 0xFF;                   // changes `view`, not `mine`
```

## 3. Constructing a Slice

```csharp
// from arrays / spans
byte[] b = ...;
b.AsSlice();                 b.AsSlice(offset, count);
new ArraySegment<byte>(b, o, n).AsSlice();
Slice.FromBytes("abc"u8);    // copies a ReadOnlySpan<byte>

// from text
Slice.FromStringUtf8("héllo");   Slice.FromString("héllo");   // UTF-8
Slice.FromStringAscii("ABC");    // ASCII only — lossy/throws on chars > 0x7F

// well-known
Slice.Empty;   Slice.Nil;   Slice.Zero(16);   // 16 zero bytes

// guids / uuids / hex
Slice.FromGuid(g);   Slice.FromUuid128(u);   Slice.FromHexString("00ff1234");
```

### Three integer encodings — pick deliberately

This is a classic source of bugs. They are **not** interchangeable:

| Factory | Encoding | Size (int32) | Read back with |
|---|---|---|---|
| `Slice.FromInt32(v)` | minimal little-endian (leading zero bytes dropped) | 1–4 bytes | `slice.ToInt32()` |
| `Slice.FromFixed32(v)` | fixed little-endian | always 4 bytes | `slice.ToInt32()` |
| `Slice.FromVarint32(v)` | 7-bit LEB128 varint | 1–5 bytes | (via `SliceReader.ReadVarInt32`) |

Every variant has a **big-endian** twin (`FromInt32BE`, `FromFixed32BE`, …) and 16/24/64/128-bit widths, plus floats (`FromSingle`/`FromDouble`) and `FromDecimal`. Big-endian fixed encodings are what you want when a number must **sort** correctly as a key. The minimal `FromInt32` is for standalone values you read whole with `ToInt32()` — it is *not* self-delimiting, so don't use it mid-stream (in a `SliceWriter`, use the fixed-width `WriteInt32`/`WriteInt64` or `WriteVarInt*` there; see §6).

> ⚠️ **Naming differs between `Slice` and the writer/reader.** On `Slice` (standalone), `FromFixed32` = 4 bytes and `FromInt32` = minimal. On `SliceWriter`/`SliceReader` (streams), the fixed-width method is plain **`WriteInt32`/`ReadInt32`** (4 bytes LE; `*BE` for big-endian), and the varint is **`WriteVarInt32`/`ReadVarInt32`**. (`WriteFixed32`/`ReadFixed32` exist but are `[Obsolete]` — use `WriteInt32`/`ReadInt32`.)

## 4. Reading values back

```csharp
slice.ToInt64();   slice.ToInt32BE();   slice.ToGuid();   slice.ToUuid128();
slice.ToStringUtf8();    // Nil -> null, Empty -> ""
slice.ToArray();         // defensive copy to byte[]
slice.ToHexString();

// zero-copy access to the bytes
ReadOnlySpan<byte> span = slice.Span;
ReadOnlyMemory<byte> mem = slice.Memory;

// slicing (negative indices count from the end)
slice.Substring(7, 6);   slice[2..5];   slice[^1..];
```

## 5. Comparison & equality

`Slice` compares **lexicographically by raw bytes** (the same order FoundationDB sorts keys), is offset/array-independent (equal content compares equal regardless of backing array or offset), and supports `==`, `<`, `>`, `CompareTo`, `StartsWith`, `EndsWith`, `IndexOf`. For dictionaries/sorted sets, use `Slice.Comparer.Default` (an `IComparer<Slice>` + `IEqualityComparer<Slice>`).

```csharp
a.CompareTo(b) < 0;            // a sorts before b
key.StartsWith(prefix);        // prefix match
var set = new SortedSet<Slice>(Slice.Comparer.Default);
```

## 6. SliceWriter — build a buffer

`SliceWriter` is a **mutable, growable** builder (`struct`, `IBufferWriter<byte>`, `IDisposable`). Start from `default(SliceWriter)` (heap-backed, grows as needed) or `new SliceWriter(pool)` (rents from an `ArrayPool<byte>`):

```csharp
var w = new SliceWriter();
w.WriteInt32(42);                    // fixed 4 bytes LE  (self-delimiting)
w.WriteVarInt32(1000);               // LEB128            (self-delimiting)
w.WriteVarString("hello");           // length-prefixed UTF-8
w.WriteStringUtf8("raw");            // raw UTF-8, NO length prefix
w.WriteBytes(payload);               // append bytes
Slice result = w.ToSlice();          // the written region (a view into the writer's buffer)
```

- Use **self-delimiting** writes (fixed-width `WriteInt32`/`WriteInt64`/…, `WriteVarInt*`, `WriteVarString`) for anything you'll parse back sequentially. A raw `WriteStringUtf8`/`WriteBytes` has no length, so the reader must already know the length.
- `Position`, `Reset()`, `Rewind()`, `Skip(n)`, `Allocate(n)`/`AllocateSpan(n)` (reserve space to fill in place).
- **Pooling caveat:** if you pass an `ArrayPool<byte>`, you must either `Dispose()` the writer or hand the buffer off with `ToSliceOwner()` — otherwise the rented array is never returned. `ToSlice()` returns a *view into the writer's buffer*; if the writer (or its pooled buffer) is disposed/reused, that view becomes invalid — `ToArray()` or `ToSliceOwner()` it to keep it.

## 7. SliceReader — parse a buffer

`SliceReader` is a **forward cursor** over a `Slice`. Pair each read with the matching write:

```csharp
var r = result.ToSliceReader();
int n     = r.ReadInt32();           // <-> WriteInt32  (fixed 4 bytes)
uint k    = r.ReadVarInt32();        // <-> WriteVarInt32
string s  = r.ReadVarString();       // <-> WriteVarString
// raw / fixed-length string written without a prefix: read the known number of bytes
string raw = r.ReadBytes(3).ToStringUtf8();
Slice rest = r.ReadToEnd();
```

`Remaining`, `HasMore`, `Head` (bytes already read), `Tail` (bytes not yet read), and non-advancing `PeekByte()`/`PeekBytes(n)` round out the API. There is **no** `ReadStringUtf8(n)` — use `ReadBytes(n).ToStringUtf8()`.

## 8. SliceOwner — pooled, disposable Slices

`SliceOwner` is a rented `Slice` that returns its buffer to an `ArrayPool<byte>` on `Dispose` — the allocation-free analogue of `IMemoryOwner<byte>`. The contract: **you MUST `Dispose` it, and MUST NOT use its data afterward.**

```csharp
using (var owner = Slice.FromBytes(payload, ArrayPool<byte>.Shared))
{
    Slice data = owner.Data;     // valid only inside the using
    Use(data.Span);
}   // buffer returned to the pool here
```

`owner.IsValid`, `owner.Count`, `owner.Span`, `owner.Pool`; `SliceOwner.Wrap/Create/Copy` and `writer.ToSliceOwner()` produce them. Don't let an owner's `Data` escape the `using`.

## 9. Span / Memory interop & `ISpanEncodable`

`Slice` interops freely with the modern primitives: `slice.Span` (`ReadOnlySpan<byte>`), `slice.Memory` (`ReadOnlyMemory<byte>`), `byte[].AsSlice()`. Many hot types (keys, values, the writers) implement **`ISpanEncodable`** so they can be rendered into a caller's buffer with no intermediate `Slice` allocation — `TryGetSpan(out span)` / `TryGetSizeHint(out size)` / `TryEncode(dest, out written)`. That interface is how `subspace.Key(...)`/`FdbValue.*` write themselves into pooled buffers at the last moment.

For working directly over `Span<byte>` (a caller-owned, fixed buffer) instead of `Slice`, use `SpanReader`/`SpanWriter` — see [`references/span-readers-writers.md`](references/span-readers-writers.md).

## 10. Round-trip example

```csharp
// build
var w = new SliceWriter();
w.WriteInt32(order.Id);
w.WriteVarString(order.Customer);
w.WriteVarInt64(order.Total);
Slice packed = w.ToSlice();

// parse
var r = packed.ToSliceReader();
int id        = r.ReadInt32();
string cust   = r.ReadVarString();
long total    = (long) r.ReadVarInt64();
```

## 11. Self-check

- [ ] Did I use `IsNull`/`IsNullOrEmpty` (not `== Slice.Empty`) to test for a missing value?
- [ ] Am I treating `Slice` as a **view** — copying with `ToArray()`/`ToSliceOwner()` before mutating shared arrays or outliving a pooled buffer?
- [ ] Did I pick the right integer encoding (`Fixed*`/`*BE` for sortable keys; `VarInt*`/`Fixed*` for self-delimiting stream fields; `FromInt32` only for standalone whole-slice values)?
- [ ] Do my `SliceWriter` writes and `SliceReader` reads pair up (`WriteInt32`↔`ReadInt32`, `VarInt`↔`VarInt`, `VarString`↔`VarString`)?
- [ ] If I rented from an `ArrayPool` (`SliceWriter(pool)` / `SliceOwner`), did I `Dispose`/`ToSliceOwner()` so the buffer returns to the pool — and not use the data after disposal?
