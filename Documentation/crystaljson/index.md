# CrystalJson: what it is and why

CrystalJson is the JSON stack of `SnowBank.Core` (namespace `SnowBank.Data.Json`). It parses,
builds, reads, mutates and serializes JSON, and it runs on its own: `SnowBank.Core` has no project
dependencies, so any .NET application can use it without the rest of the SDK. This page explains
what it is and why it is shaped this way; for the task guides see
[Working with CrystalJson](serializing.md), and for the complete attribute, settings and
diagnostics tables see the [reference](reference.md).

It is **not** `System.Text.Json` and not Newtonsoft. The type names look familiar (`JsonObject`,
`JsonArray`) but the API is different, and the differences are the point.

## Why: the POCO round-trip problem

Many applications handle a lot of data represented as JSON. The classic
way to work with it, deserialize into a POCO (Plain Old CLR Object, a concept also met as DTO,
Data Transfer Object, or view model), use the object, serialize it back, has two drawbacks at
this scale:

- **It can be inefficient.** Binding a whole document costs allocation and CPU even when the code
  consumes three of its forty fields, or wants to patch one.
- **It silently truncates under schema evolution.** A POCO keeps only the fields it declares. When
  component versions coexist, an older component that reads, modifies and writes back a document
  through its older POCO drops every field it does not know:

```csharp
// the stored document was written by a newer component:
// {"id":"B123","title":"Dune","rating":4.5}

// this component's model predates "rating"
record Book(string Id, string Title);

var book = CrystalJson.Deserialize<Book>(json);
// => book carries no "rating"; modify it, write it back:
//    the stored document has now LOST "rating"
```

CrystalJson answers with a ladder of three representations, chosen per use, not once per
application:

1. **The DOM** (Document Object Model: `JsonObject`, `JsonArray`, ...) is the safe end: parsing
   pays no binding cost, and
   no field is ever dropped, because nothing is projected. The price is typing: DOM code reads
   more like JavaScript than C#.
2. **Generated proxies** are the middle ground: the source generator emits read-only and writable
   views that expose a strongly typed shape *on top of* the DOM. Code reads `proxy.Title` with
   IntelliSense and compile-time checking, while the document underneath keeps every field it
   arrived with. Nothing is materialized until something asks for the whole POCO.
3. **POCOs** stay available for the cases they fit: a type the current component fully owns, or a
   boundary where the document's lifetime ends anyway.

Two properties of the DOM support the ladder. A `JsonObject` or `JsonArray` is either **mutable or
read-only**: a read-only value is deeply immutable, so frequently requested documents can be
cached in memory and shared across threads with no risk of corruption, and **copy-on-write** is
the pattern for "mutating" one (edit a copy, the frozen original stands). And the DOM has
**observable wrappers** that record which fields were read or written, which reactive layers
built on this stack use for subscriptions and patch generation; those wrappers belong to the
layers that hand them out, and their documentation lives with those layers.

Two smaller commitments round out the design. Values parse from and serialize to `Slice` / UTF-8
spans without an intermediate `string` (the neighbors in this stack speak bytes). And navigation
has **null propagation built in**: a missing field reads as `JsonNull.Missing` instead of
throwing, each read states its own policy (a default, or a required read that throws), and the
generated proxies propagate absence the same way. Out of the box, this removes a whole class of
production `NullReferenceException`s, and the null-check boilerplate that guards against them.

## The two-layer model

CrystalJson is two layers used together. The static `CrystalJson` class is the entry point of
the POCO route (`Serialize`, `Deserialize`); the DOM types parse and build themselves
(`JsonValue.Parse`, `JsonObject.Parse`, `JsonValue.FromValue`):

- **The DOM** (`JsonValue` and its subtypes): a tree you parse, navigate, build and mutate. Use it
  for schemaless or dynamic JSON: configuration, arbitrary documents, change records.
- **The source generator** (`SnowBank.Serialization.Json.CodeGen`): for your own domain types.
  A container class (or the type itself, in self-serializable mode) declares which types it
  serializes, and the generator emits reflection-free converters at compile time, plus the typed
  read-only and writable proxies from the ladder above.

A type with no generated converter still serializes: the **reflection path** builds a contract at
run time from the same attributes. The two paths are held to the same output byte for byte, and
where an attribute combination would give them two different answers, the policy is to refuse it
loudly rather than let the output depend on which path serialized the value.

## One type, one output

That refusal policy has a name because it targets a specific legacy pattern: the **dual-output
DTO**. Some estates grew types annotated for two serializers at once, so the same class produced
two different documents depending on which library serialized it:

```csharp
public class Order
{
    [DataMember(Name = "order_id")]     // the name DataContractJsonSerializer emitted
    [JsonProperty("orderId")]           // the name Newtonsoft emitted
    public string? Id { get; set; }

    [DataMember]                        // present on the DCJS output...
    [JsonIgnore]                        // ...hidden from the Newtonsoft output
    public string? InternalCode { get; set; }
}
```

This was always a hack, not a supported technique: it holds only while every call site carefully
picks the right serializer, and one wrong pick sends a consumer the other consumer's document.
CrystalJson cannot honor it even in principle, because it has two serialization paths of its own
(reflection and generated), and "which document do I get" must never depend on the path. So both
members above are **build errors**, not choices: the double name is refused (`CJSON0011`), and
the include-plus-unconditional-ignore pair is refused (`CJSON0008`). The remedy is always the
split: one DTO per format contract, each carrying a single coherent set of attributes. The same
policy rejects the `DataContractJsonSerializer`-era callback signature rather than approximating
it. The [migration guide](../releases/7.4.3.md) documents each refusal with its
diagnostic id and remedy.

Note that the dual-output DTO is a different need than serving legacy and modern consumers from
the same types, which is supported and is the next section's subject: there, the *types* are
shared and the *containers* differ, so each output stays a complete, coherent contract.

## The migration bridge

Legacy estates (`DataContractJsonSerializer`, Newtonsoft) usually cannot change their JSON format
on the day they modernize: frozen consumers still parse the old bytes. CrystalJson treats that as
a supported migration path rather than an obstacle:

- **Reading is tolerant, always.** Numeric or string enums, both dictionary shapes, both duration
  forms, and the Microsoft date format are accepted on read regardless of settings. Producers and
  consumers move independently.
- **The compat profile reproduces the legacy output.** `CrystalJsonSettings.DataContractCompat`
  emits what `DataContractJsonSerializer` emitted, byte for byte, with a short documented list of
  differences. A component adopts CrystalJson first, and its consumers see the same bytes.
- **The modern format comes second, on your schedule.** A dual-container setup serves both formats
  from the same types, so the switch can be big-bang, one component at a time, or per request
  (selected by a header, a user agent, anything that tells a legacy consumer from a modern one).
  Delete the compat container when the last legacy consumer is gone.

## Where it sits in this stack

CrystalJson is a `SnowBank.Core` component with no dependency on the rest of the SDK, so any .NET
application can use it on its own. [CrystalXml](../CrystalXml.md) reuses the same containers,
enrollment and settings to emit XML from the same types. Layers in this SDK that store or transmit
documents serialize through CrystalJson, so a document keeps one representation across the layers
that handle it.
