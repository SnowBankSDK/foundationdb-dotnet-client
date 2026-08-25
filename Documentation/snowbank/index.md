# The SnowBank libraries

The `SnowBank.*` libraries are general-purpose .NET libraries with no dependency on FoundationDB. The FoundationDB client and the rest of the SDK build on them, and each library also works on its own.

This is the SnowBank part of the [SnowBank SDK](../index.md). If you came for the FoundationDB database client, see [FoundationDB for .NET](../fdb/introduction.md).

## What is included

Documented here:

- **[Slices and buffers](slices-and-buffers.md)**: `Slice`, a read-only view over bytes, with `SliceReader` and `SliceWriter`, pooled buffers, and the integer encodings. The byte-level toolkit under the rest.
- **[Tuples](tuples.md)**: `TuPack` and `IVarTuple`, the tuple encoding that turns typed values into bytes whose order matches the values. FoundationDB keys use it; it stands alone.
- **[JSON](crystaljson/index.md)**: CrystalJson, an in-memory JSON model (`JsonValue`) with a source generator for reflection-free serializers.
- **[XML](CrystalXml.md)**: CrystalXml, compile-time XML output for CrystalJson types, byte-compatible with `DataContractSerializer`.

Shipped, not yet documented here:

- **`SnowBank.Shell`**: a command shell and prompt engine.
- **`SnowBank.Networking.Http`**: an HTTP client stack with retry and instrumentation.
- **`SnowBank.Serialization.Json.CodeGen`**: the Roslyn source generator behind CrystalJson's generated converters.
- **`SnowBank.Testing`**: a distributed-test framework.

Every library ships from the same repository as the FoundationDB client, under the `SnowBank` namespace root.
