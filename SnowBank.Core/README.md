SnowBank SDK - Core Library
===========================

The foundation library of the SnowBank SDK and the [FoundationDB .NET client](https://github.com/SnowBankSDK/foundationdb-dotnet-client). It provides the high-performance, allocation-conscious building blocks that every other package in the SDK is built on, and that any application consuming the SDK will end up shipping.

`SnowBank.Core` has **no dependency on FoundationDB** (or on any database): it is a general-purpose toolkit that can be used entirely on its own.

# What's inside

## Slice, SliceReader and SliceWriter

`Slice` is a `readonly struct`, a better `ReadOnlyMemory<byte>` purpose-built for binary data, with a large set of encoders and decoders. `SliceWriter` builds a buffer, `SliceReader` walks one back, and `SliceOwner` lets a `Slice` own a pooled backing array so hot paths stay allocation-light.

```c#
// simple encoders and decoders on Slice itself
Slice hello = Slice.FromStringUtf8("hello");
string text = hello.ToStringUtf8();      // "hello"
Slice number = Slice.FromInt32(12345);   // also fixed-width, hex, base64, GUIDs, ...

// build a buffer with SliceWriter
var writer = new SliceWriter(64);
writer.WriteFixed32(0xDEADBEEF);         // 4 bytes, little-endian (WriteFixed32BE for big-endian)
writer.WriteVarInt32(12345);             // variable-length integer
writer.WriteStringUtf8("hello");
Slice buffer = writer.ToSlice();

// walk it back with SliceReader, in the same order
var reader = new SliceReader(buffer);
uint magic = reader.ReadFixed32();
Slice rest = reader.ReadToEnd();
```

For hot paths, rent the writer's backing array from a pool and hand the result off as a `SliceOwner`:

```c#
var writer = new SliceWriter(ArrayPool<byte>.Shared);
writer.WriteStringUtf8("...");

SliceOwner owner = writer.ToSliceOwner();  // ownership of the pooled buffer moves to the owner
Consume(owner.Data);                       // owner.Data is the resulting Slice
owner.Dispose();                           // returns the buffer to the pool
```

## Tuples

An implementation of tuples close to FoundationDB's Python layer: a compact, **order-preserving** binary encoding for composite values. `TuPack` packs a set of values into a `Slice` and unpacks it back, keeping both type and sort order.

```c#
Slice packed = TuPack.EncodeKey("users", 42, "email");

var t = TuPack.Unpack(packed);
string first = t.Get<string>(0);     // "users"
int second   = t.Get<int>(1);        // 42
```

This is ideal for **values**, and for any non-FoundationDB use of an ordered composite encoding. Note that a FoundationDB **key** is not just a raw packed tuple: keys are built relative to a Directory subspace (which supplies a location prefix) using the key encoders in `FoundationDB.Client`. Packing a raw tuple with `TuPack.EncodeKey` and using it directly as a key skips that prefix, and is unsafe. See the `FoundationDB.Client` package for how to build keys correctly.

## CrystalJson, a fast JSON stack

A supercharged JSON implementation: a `JsonValue` DOM (`JsonObject`, `JsonArray`, ...) with a read-only vs. mutable model, a parser and binder, and a Roslyn **source generator** that emits reflection-free serializers and read-only/writable proxies for hot paths.

```c#
// serialize any object
string json = CrystalJson.Serialize(new { name = "Java Depp", beans = 81 });

// parse into a queryable DOM
JsonValue doc = CrystalJson.Parse(json);
int beans = doc["beans"].ToInt32();

// or deserialize straight into a type
var obj = CrystalJson.Deserialize<MyRecord>(json);
```

For the fast path, declare a partial container that lists the types you want generated, and the source generator produces reflection-free serializers plus typed **read-only** and **writable proxies** over a `JsonObject`:

```c#
[CrystalJsonConverter]
[CrystalJsonSerializable(typeof(Person))]
public static partial class GeneratedConverters { }

public sealed record Person
{
    public required string FirstName { get; init; }
    public required string FamilyName { get; init; }
}

var person = new Person { FirstName = "James", FamilyName = "Bond" };

// a read-only proxy: typed access over an immutable JsonObject, no reflection
var ro = GeneratedConverters.Person.ToReadOnly(person);
string first = ro.FirstName;                 // "James"

// a writable proxy: mutate fields, then read the JSON or decode back to a Person
var w = GeneratedConverters.Person.ToMutable(person);
w.FirstName = "Jim";
JsonValue json = w.ToJsonValue();            // { "firstName": "Jim", "familyName": "Bond" }
Person updated = w.ToValue();
```

## UUIDs and compact identifiers

`Uuid128` (and its `Uuid96`, `Uuid80`, `Uuid64` siblings) are value types for compact, sortable identifiers, with encoders and decoders that match the rest of the SDK (`Slice`, spans, `SliceWriter`).

```c#
Uuid128 id = Uuid128.NewUuid();          // random (version 4)

// text round-trip
string s = id.ToString();
Uuid128 back = Uuid128.Parse(s);

// binary (16 bytes): to a Slice, a caller-owned span, or a SliceWriter
Slice slice = id.ToSlice();
Span<byte> buf = stackalloc byte[16];
id.WriteTo(buf);
Uuid128 fromSpan = Uuid128.Read(buf);    // also Read(Slice) / Read(byte[])

// compose from, and split into, fixed-size chunks
Uuid128 composed = Uuid128.FromUpper64Lower64(hi: 0x0123456789ABCDEFUL, low: 0xFEDCBA9876543210UL);
var (high, low) = composed;              // two 64-bit halves (Uuid64)
var (a, b, c, d) = composed;             // four 32-bit chunks
```

## Contracts, lightweight guards

`Contract` provides concise argument and state guards. They throw the right exception with the parameter name filled in automatically (via `CallerArgumentExpression`).

```c#
public void Send(string channel, ReadOnlySpan<byte> payload, int offset, int count)
{
    Contract.NotNullOrEmpty(channel);                        // ArgumentException if null or empty
    Contract.Positive(count);                                // must be > 0
    Contract.GreaterThan(offset, -1);                        // must be > -1
    Contract.DoesNotOverflow(payload.Length, offset, count); // offset + count must fit the buffer
    // ...
}
```

`NotNull`, `NotNullOrEmpty`, `NotNullOrWhiteSpace`, `Positive`, `GreaterThan` / `GreaterOrEqual` / `LessThan`, `Between`, and `DoesNotOverflow` cover the common cases.

For invariants that should only be checked in debug builds (and compiled out of Release), use `Contract.Debug`:

```c#
Contract.Debug.Requires(offset >= 0);                    // precondition, checked in DEBUG only
// ...
Contract.Debug.Ensures(this.Position <= this.Length);    // postcondition
```

## And more

- **Collections**: cache-oblivious ordered stores and other specialized collections.
- **Hashing**: fast non-cryptographic hashing (`XxHash`).
- **Runtime helpers**: type converters, and shared interfaces implemented by the higher-level packages.

# Bridging a migration from .NET Framework (netstandard2.0)

Besides `net8.0` / `net10.0` / `net11.0`, `SnowBank.Core` also multi-targets **`netstandard2.0`**. This is not about running new code on .NET Framework as an end goal. It exists to **smooth the migration of a large legacy application to modern .NET**.

Porting a big codebase is almost never a single commit. In practice the legacy branch (still on .NET Framework 4.7.2) and the modernized branch (targeting, say, .NET 10) have to **coexist for a while**: the team keeps shipping fixes and features from the old branch while the new one is brought up, and those changes have to be **continuously merged** across.

That merge is where the cost is, and where the `netstandard2.0` build helps. Because the legacy branch can reference `SnowBank.Core` too, the *modern* APIs it provides (`Slice`, CrystalJson, the Tuple encoding, `Contract.NotNull`, ...) **compile and run on .NET Framework as well**. So the code that uses them reads **identically on both branches**, which sharply cuts merge conflicts, and lets you **backport the C# modernization** (newer language features, the modern API shapes) onto the old branch incrementally, instead of maintaining two divergent styles until the very end.

The encodings are **byte-identical across all targets** (a `Slice`, a packed tuple, or a JSON document produced on `netstandard2.0` matches the one produced on `net10.0`), so data written by either branch stays interchangeable throughout the transition. Once the migration lands and the legacy branch is retired, the `netstandard2.0` target can simply be dropped.

# Part of a larger SDK

`SnowBank.Core` is the base of the whole family: `FoundationDB.Client` (the FoundationDB binding) and the CloudLayer layers (DocStore, Teleport, ...) are all built on top of it. See the [repository](https://github.com/SnowBankSDK/foundationdb-dotnet-client) for the full picture and the getting-started guide.
