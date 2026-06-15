# Buffers, writers & pooling (allocation-conscious)

Allocation-consciousness is a core value of this codebase, so there's a family of buffer-builders and pools beyond `SliceWriter`. Reach for these when profiling shows allocation pressure (lots of short-lived keys/values, hot serialization loops); for everyday work `SliceWriter` + `SliceOwner` (see `SKILL.md`) are enough.

> **Disposal discipline:** every pooled type here is `IDisposable` and **must** be disposed (or its slabs/arrays never return to the pool, which *degrades* pool performance for everyone). Pair each with `using`.

## `SliceOwner` — a rented Slice (recap)

The result of `SliceWriter.ToSliceOwner()`, `Slice.FromBytes(span, pool)`, or `SliceOwner.Create/Copy/Wrap`. It owns a (possibly pooled) buffer and returns it on `Dispose`. `Data`/`Span`/`Count`/`IsValid`/`Pool`. **MUST** be disposed; its data **MUST NOT** be used afterward. This is the type you return from an API that produces bytes but wants to stay allocation-free.

## `ISliceBufferWriter` — `IBufferWriter<byte>` that also vends Slices

`ISliceBufferWriter : IBufferWriter<byte>` adds `GetSlice(...)` (returns an `ArraySegment<byte>`) on top of the standard `GetSpan(sizeHint)` / `Advance(count)` protocol. Three implementations, differing in how they manage memory:

| Type | Memory | Use when |
|---|---|---|
| `ArraySliceWriter` | one **contiguous** heap array, grown by copying | you need the final bytes contiguous and don't want pooling |
| `SlabSliceWriter` | **slabs** from a pool (or heap), kept until `Dispose`/clear; **not** contiguous | high throughput, you consume per-chunk and don't need one span |
| `PooledSliceWriter` | one contiguous array **rented from a pool**, grown by copying | contiguous output *and* pooling |

All follow the standard writer protocol and integrate with anything that takes an `IBufferWriter<byte>` (e.g. `Utf8JsonWriter`):

```csharp
using var w = new SlabSliceWriter();
Span<byte> span = w.GetSpan(64);    // request space
// ... write into span ...
w.Advance(written);                 // commit it
// consume via w.GetSlice(...) / the IBufferWriter surface; Dispose returns slabs to the pool
```

## `ISliceAllocator` — many short-lived slices (an arena)

When you allocate a *lot* of slices that won't outlive a single operation (e.g. building all the keys for one transaction), an allocator is faster than repeatedly growing a `SliceWriter`. Instead of N independent array allocations you allocate a few big slabs, sub-allocate from them, then release them all together:

```csharp
using var alloc = new ArraySliceAllocator();   // ISliceAllocator : IDisposable
ArraySegment<byte> seg = alloc.Allocate(16);   // carved from a shared slab
// ... fill seg.AsSpan() ...
```

- **`ArraySliceAllocator`** — slabs from the heap.
- **`PooledSliceAllocator`** — slabs rented from an `ArrayPool`; **must** be disposed to return them (failing to dispose *hurts* pool performance for everyone).

Both implement `ISliceAllocator : IDisposable` with `Allocate(int) → ArraySegment<byte>`. The mental model is a per-request / per-transaction arena.

> The older **`SlicePool`** type is `[Obsolete]` — use an `ISliceAllocator` (above) or an `ISliceBufferWriter` instead.

## `ValueBuffer<T>` / `SegmentedValueBuffer<T>` / `PooledBuffer<T>` — accumulators

Value-type, growable accumulators — a `List<T>` you can seed with stack memory and that avoids heap allocation until it has to:

```csharp
// seed with stack space; only spills to a pooled array if it outgrows the seed
using var buf = new ValueBuffer<int>(stackalloc int[16]);   // or new ValueBuffer<int>(capacity)
buf.Add(1);
buf.AddRange(more);
Span<int> items = buf.GetSpan();   // contiguous view of everything added
int[] copy = buf.ToArray();
```

- **`ValueBuffer<T>`** (`ref struct`) — final items are a **single contiguous** `Span<T>`. Great for "collect an unknown number of items, then process them once."
- **`SegmentedValueBuffer<T>`** (`ref struct`) — same idea but **segmented**; faster when you *don't* need one contiguous span (you iterate the segments).
- **`PooledBuffer<T>`** (`struct`, `IBufferWriter<T>` + `IDisposable`) — a pooled accumulator usable as an `IBufferWriter<T>` (so it plugs into APIs that write into one). Dispose to return the rented array.

## Choosing, in one line

- Build a key/value and hand it off → **`SliceWriter` → `ToSlice()`/`ToSliceOwner()`**.
- Write into a buffer you already hold, on the stack → **`SpanWriter`** (see the Span reference).
- Build *many* throwaway slices in one operation → **`ISliceAllocator`** (`ArraySliceAllocator` / `PooledSliceAllocator`).
- Accumulate an unknown number of items, then process once → **`ValueBuffer<T>`** (contiguous) or **`SegmentedValueBuffer<T>`** (segmented).
- Need an `IBufferWriter<byte>` for some other API → **`SlabSliceWriter` / `ArraySliceWriter` / `PooledBuffer<T>`**.
