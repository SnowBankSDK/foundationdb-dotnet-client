# Binary Data: Slice & Buffers

Almost everything in this stack eventually becomes bytes: keys, values, tuple-encoded elements. The type you use to hold and manipulate those bytes is **`Slice`**, and this guide explains how to use it (and its companions `SliceReader`, `SliceWriter`, `SliceOwner`) correctly. It's the foundation under [Keys, Values & Layers](../fdb/guide/keys-and-layers/index.md).

## What `Slice` is

`Slice` is a **`readonly struct`** (in namespace `System`) that wraps a segment of a `byte[]` (three fields: the backing `Array`, an `Offset`, and a `Count`). It predates `Span<T>` and is the logical equivalent of **`ReadOnlyMemory<byte>`**, but carries a large library of helpers for converting bytes to and from real types.

Two properties shape everything else:

- **A `Slice` is a view, not a copy.** Creating one from a `byte[]` shares the array; mutating the array is visible through the slice (and its `.Span`). When you need to own the bytes, copy with `.ToArray()` (or `.ToSliceOwner()` for a pooled copy).
- **`Slice.Nil` and `Slice.Empty` are different**, and the difference matters.

## Nil vs Empty

`Slice.Nil` has **no** backing array (null-like); `Slice.Empty` has a **zero-length** array.

| | `Slice.Nil` | `Slice.Empty` |
|---|---|---|
| `IsNull` | `true` | `false` |
| `IsEmpty` | `false` | `true` |
| `IsNullOrEmpty` | `true` | `true` |
| `IsPresent` | `false` | `true` |
| `GetBytes()` | `null` | empty array |
| `ToStringUtf8()` | `null` | `""` |
| `==` | distinct (`Nil != Empty`) | |
| `CompareTo` | equal (both sort first) | |

`tr.GetAsync(key)` returns **`Slice.Nil`** when a key doesn't exist, so the idiomatic "does it exist?" test is `value.IsNull`:

```csharp
var v = await tr.GetAsync(key);
if (v.IsNull) { /* key not found */ }
```

Use `Nil` to mean *absent* and `Empty` to mean *present but empty*.

## Constructing a Slice

```csharp
byte[] b = ...;
b.AsSlice();                 b.AsSlice(offset, count);     // views over an array
Slice.FromBytes("abc"u8);                                 // copy a ReadOnlySpan<byte>
Slice.FromStringUtf8("héllo");   Slice.FromString("x");   // UTF-8
Slice.FromStringAscii("ABC");                             // ASCII only (lossy/throws > 0x7F)
Slice.Empty;   Slice.Nil;   Slice.Zero(16);
Slice.FromGuid(g);   Slice.FromUuid128(u);   Slice.FromHexString("00ff");
```

### Three integer encodings: choose deliberately

Three encodings are easy to confuse. On a standalone `Slice`:

| Factory | Encoding | int32 size |
|---|---|---|
| `Slice.FromInt32(v)` | minimal little-endian (drops leading zeros) | 1-4 bytes |
| `Slice.FromFixed32(v)` | fixed little-endian | always 4 |
| `Slice.FromVarint32(v)` | 7-bit LEB128 varint | 1-5 |

Each has a big-endian twin (`…BE`); **big-endian fixed** is what sorts correctly as a key. Read back with `slice.ToInt32()` / `ToInt32BE()` etc.

> **Heads-up:** the *streaming* `SliceWriter`/`SliceReader` name these differently: there, the fixed-width method is plain `WriteInt32`/`ReadInt32` (4 bytes LE) and the varint is `WriteVarInt32`/`ReadVarInt32`. (`WriteFixed32`/`ReadFixed32` are obsolete aliases.)

## Reading values & slicing

```csharp
slice.ToInt64();   slice.ToGuid();   slice.ToStringUtf8();   slice.ToArray();
ReadOnlySpan<byte> span = slice.Span;       // zero-copy
ReadOnlyMemory<byte> mem = slice.Memory;
slice.Substring(7, 6);   slice[2..5];   slice[^1..];   // negative/Range indexing
```

## Comparison

`Slice` orders **lexicographically by raw bytes** (the same order FoundationDB sorts keys) and is offset-independent (equal content is equal regardless of backing array). It supports `==`, `<`, `>`, `CompareTo`, `StartsWith`, `EndsWith`, `IndexOf`, and `Slice.Comparer.Default` for dictionaries/sorted sets.

## Building & parsing: SliceWriter / SliceReader

`SliceWriter` is a growable builder; `SliceReader` is a forward cursor. Pair each write with the matching read, and prefer **self-delimiting** writes (fixed-width or varint or length-prefixed string) for anything parsed back sequentially:

```csharp
var w = new SliceWriter();
w.WriteInt32(order.Id);            // fixed 4 bytes LE
w.WriteVarString(order.Customer);  // length-prefixed UTF-8
w.WriteVarInt64(order.Total);
Slice packed = w.ToSlice();

var r = packed.ToSliceReader();
int id      = r.ReadInt32();
string cust = r.ReadVarString();
long total  = (long) r.ReadVarInt64();
```

`ToSlice()` returns a *view into the writer's buffer*; copy it (`ToArray()`/`ToSliceOwner()`) if it must outlive the writer. There is no `ReadStringUtf8(n)`. For a raw (un-prefixed) string of known length, use `r.ReadBytes(n).ToStringUtf8()`.

## Pooling: SliceOwner & ArrayPool

To stay allocation-free, rent buffers. A `SliceWriter` constructed with an `ArrayPool<byte>` must be disposed or handed off via `ToSliceOwner()`. `SliceOwner` is a rented `Slice` that returns its buffer to the pool on `Dispose`; **you must dispose it and must not use its data afterward**:

```csharp
using (var owner = Slice.FromBytes(payload, ArrayPool<byte>.Shared))
{
    Use(owner.Data.Span);   // valid only inside the using
}   // buffer returned to the pool here
```

## Modern interop & `ISpanEncodable`

`Slice` converts freely to/from `ReadOnlySpan<byte>` (`.Span`), `ReadOnlyMemory<byte>` (`.Memory`), and `byte[]` (`.AsSlice()`). Hot types (keys, values, the writers) implement **`ISpanEncodable`** (`TryGetSpan` / `TryGetSizeHint` / `TryEncode`) so they can be written into a caller's buffer with no intermediate `Slice`. That's how `subspace.Key(...)`/`FdbValue.*` render themselves into pooled buffers at the last moment.

## Going lower-level

For performance-sensitive code there's more:

- **`SpanReader` / `SpanWriter`**: `ref struct` readers/writers that work directly over a caller-owned `Span<byte>` (a `stackalloc` or rented buffer), with zero allocation. Use them when you already hold a fixed buffer and the work stays on the stack; use the `Slice`-based ones when you need to grow or hand the result off.
- **`ISliceBufferWriter`** (`ArraySliceWriter` contiguous, `SlabSliceWriter` slab-based, `PooledSliceWriter`): `IBufferWriter<byte>` implementations that also vend `Slice`s; plug into APIs like `Utf8JsonWriter`.
- **`ISliceAllocator`** (`ArraySliceAllocator` / `PooledSliceAllocator`): sub-allocate many short-lived slices from shared slabs (a per-request arena). (The older `SlicePool` is obsolete.)
- **`ValueBuffer<T>` / `SegmentedValueBuffer<T>` / `PooledBuffer<T>`**: value-type accumulators you can seed with stack memory, for collecting an unknown number of items without heap allocation.

These are documented in depth for agents in the `snowbank-slices-and-buffers` skill's reference files; reach for them only when profiling shows it's worth it. Everyday code does fine with `Slice` + `SliceWriter`/`SliceReader` + `SliceOwner`.
