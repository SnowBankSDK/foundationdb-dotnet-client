# Span-first reading & writing

`Slice`/`SliceReader`/`SliceWriter` are the everyday types (see `SKILL.md`). When you already hold a **caller-owned `Span<byte>`/`ReadOnlySpan<byte>`** — a `stackalloc` buffer, a rented array, or a destination handed to you — work over it directly with the `Span*` equivalents. They are **`ref struct`s** (stack-only, zero allocation) and do **not** own or grow memory.

## SpanReader — parse a `ReadOnlySpan<byte>`

A forward cursor over a fixed span. Same shape as `SliceReader`, but returns `ReadOnlySpan<byte>` slices instead of `Slice` and cannot escape the stack.

```csharp
var r = new SpanReader(span);                 // span is ReadOnlySpan<byte>
int n        = r.ReadInt32();                 // also ReadInt32BE, ReadInt16/24/64, ReadUInt*, ReadSingle/Double
ReadOnlySpan<byte> four = r.ReadFourBytes();  // ReadTwoBytes / ReadFourBytes / ReadEightBytes / ReadSixteenBytes
Guid g       = r.ReadGuid();                  // ReadUuid128 / ReadUuid64
var rest     = r.ReadToEnd();
// non-advancing: r.PeekByte(), r.PeekBytes(n); cursor state via r.Remaining / r.Position
```

## SpanWriter — write into a `Span<byte>`

Writes into either a caller buffer or an internally-sized one; `ToSpan()` returns the written region. It will **not grow** a caller-provided buffer — it throws if you overflow it (that's the point: bounded, allocation-free).

```csharp
Span<byte> scratch = stackalloc byte[64];
var w = new SpanWriter(scratch);              // or new SpanWriter(capacity)
w.WriteInt32(42);                             // fixed-width LE; *BE variants for big-endian
w.WriteUInt64BE(version);
w.WriteByte(0xFF);
w.WriteBytes(payload);
Span<byte> room = w.Allocate(16);             // reserve, fill in place
ReadOnlySpan<byte> written = w.ToSpan();
```

There are also `static` `SpanWriter.WriteInt32(span, value)`-style helpers when you just need to poke a value into a span at a known position without a cursor.

## When to prefer Span-first vs Slice-based

- **`SpanReader`/`SpanWriter`** — you have a fixed, caller-owned buffer and want zero allocation, and the work stays on the stack. Ideal inside `ISpanEncodable.TryEncode`, parsers, and hot loops.
- **`SliceReader`/`SliceWriter`** — you need the buffer to **grow**, or to **hand the result off** as a `Slice` that outlives the current stack frame (store it, return it, put it in a value). `SliceWriter` can rent/grow and produce a `Slice`/`SliceOwner`; a `ref struct` `SpanWriter` cannot.

## The `ISpanEncodable` contract

Hot types (keys, values, the writers) implement **`ISpanEncodable`** so they can render themselves into a caller's buffer with no intermediate `Slice`:

```csharp
public interface ISpanEncodable
{
    bool TryGetSpan(out ReadOnlySpan<byte> span);   // already-contiguous? hand it over (zero copy)
    bool TryGetSizeHint(out int sizeHint);          // how big will TryEncode need?
    bool TryEncode(Span<byte> destination, out int bytesWritten);  // write into the caller's buffer
}
```

This is exactly how `subspace.Key(...)` and `FdbValue.*` get rendered into pooled buffers at the last moment, instead of allocating a `Slice` per key. When you write your own key/value type, implementing `ISpanEncodable` lets it participate in that zero-allocation path.

## Typed span encoders/decoders

- **`SpanDecoderExtensions`** — extension methods to decode a `ReadOnlySpan<byte>` straight into types (`ToInt64`, `ToGuid`, …): the span-based mirror of `Slice.ToXxx`. Use when you have a span and don't want to wrap it in a `Slice` first.
- **`ISpanEncoder<T>` / `ISpanDecoder<T>`** and the static **`SpanEncoders`** (in `SnowBank.Data.Binary`) — the strategy types the value layer uses to encode/decode `T ⇄ bytes` generically (e.g. `FdbValue<T, TEncoder>`). You rarely call these directly, but they're what makes `FdbValue.ToFixed64LittleEndian`/`ToTextUtf8`/etc. allocation-free and composable.
