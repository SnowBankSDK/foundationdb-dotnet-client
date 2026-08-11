# Working with CrystalJson

The everyday gestures, one section per task. This page assumes you know what CrystalJson is and
why it has a DOM (Document Object Model), proxies and two serialization paths; if not, read
[the explanation](index.md) first. Porting a `DataContractJsonSerializer` or Newtonsoft estate is
its own project; the [migration guide](../migrations/7.4.2-to-7.4.3.md) covers the diagnostics and
behavior changes you will hit. The complete attribute, settings and diagnostics tables are in the
[reference](reference.md).

All examples use `using SnowBank.Data.Json;`.

> **Tip: make it a global using.** Other libraries in a typical project also declare a type
> named `JsonObject` (`System.Text.Json.Nodes` in particular). The first time a file mentions
> `JsonObject` without the right `using`, the IDE's autocompletion offers to add one, and picking
> the wrong namespace produces confusing errors: the methods look the same-ish but take different
> arguments. Declaring `global using SnowBank.Data.Json;` once in the project's `GlobalUsings.cs`
> removes the ambiguity for every file at once.

## Serialize and deserialize a type

No setup is required. The reflection path builds a serialization contract at run time from the
type itself:

```csharp
public sealed record Book
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public int Year { get; init; }
}

string json = CrystalJson.Serialize(book);
// => { "Id": "B123", "Title": "Dune", "Year": 1965 }

// throws if the JSON is null or empty
Book back = CrystalJson.Deserialize<Book>(json);

// null instead of throwing
Book? maybe = CrystalJson.Deserialize<Book>(json, defaultValue: null);

// UTF-8 bytes for a database value
Slice bytes = CrystalJson.ToSlice(book);
```

For a type you own and serialize often, prefer the **source generator**: declare a container
once, and the compiler emits the converter that the reflection path would otherwise rebuild at
run time:

```csharp
[CrystalJsonConverter]
[CrystalSerializable(typeof(Book))]
public static partial class AcmeSerializers { }        // generated members land here

string json = AcmeSerializers.Book.ToJsonText(book);
Book   back = AcmeSerializers.Book.Deserialize(json);
```

The generated converter is reflection-free (works under AOT and trimming), faster, and brings
the typed proxies used later on this page. The project consuming the generator needs
`LangVersion` 9 or later and the generator referenced as an analyzer; both are one-time setup,
detailed in the [reference](reference.md).

Both routes produce the same bytes for the same type, so you can start ad hoc and adopt the
generator later without changing any stored document. The reflection path stays the right tool
when there is no typed schema to declare, or when quick and dirty is fine: a script, a test, a
one-off tool.

## Parse and navigate unknown JSON

When the shape is dynamic or partially known, parse into the DOM and navigate. The parse entry
point states how a wrong top-level shape is handled. When a non-object payload would be a bug in
the producer, parse straight to the type and let it throw; when it is an ordinary case your code
must handle, parse to `JsonValue` and inspect:

```csharp
// a non-object payload is extraordinary here: parse to the type, it throws otherwise
JsonObject obj = JsonObject.Parse(json);

// a non-object payload is an ordinary case here: handle it and move on
JsonValue value = JsonValue.Parse(json);
if (value is not JsonObject o)
{
    // not an object: reject the request, skip the entry, ...
    return;
}
// from here, o is the typed object
```

`JsonArray.Parse(...)` is the array twin, and the naming is a pattern, not a coincidence: every
DOM type nests a `ReadOnly` class with the same entry points (`JsonValue.ReadOnly.Parse`,
`JsonObject.ReadOnly.Parse`, `JsonValue.ReadOnly.FromValue`), returning a frozen, cache-safe
document instead of a mutable one. The `CrystalJson` static class serves the POCO route
(`Serialize`, `Deserialize`, `ToSlice`); the DOM parses through the DOM types themselves.

Once parsed, navigation has **null propagation built in**: an indexer never throws on a missing
member, it returns a null object (`JsonNull.Missing`) that the next hop accepts, so a whole
chain is safe with zero manual null checks:

```csharp
JsonValue city = obj["user"]["address"]["city"];   // Missing if any hop is absent, no NRE
bool present   = !city.IsNullOrMissing();

int    age  = obj.Get<int>("age", 0);              // default if absent
string name = obj.Get<string>("name");             // required: throws if absent
if (obj.TryGet<string>("email", out var email)) { /* ... */ }

JsonObject meta  = obj.GetObjectOrEmpty("meta");   // never null; empty if absent
JsonArray  items = obj.GetArray("items");          // throws if not an array
foreach (var item in items.AsObjects()) { /* JsonObject items only */ }
```

The generated proxies, later on this page, propagate absence the same way: a chain through a
missing inner object keeps navigating (`proxy.Metadata.IsNullOrMissing()` tells you), an optional
member reads as its default, and a `required` member absent from the document throws a
`JsonBindingException`. Never a `NullReferenceException`, and never a manual null check.

One distinction matters when you test for null: `JsonNull.Null` is an explicit `null` in the
document, `JsonNull.Missing` is a member that was not there, and `JsonNull.Error` is an invalid
access (indexing a non-array). All three report `IsNull == true`; use `IsNullOrMissing()` or
`IsMissing()` when the difference matters.

## Build a JSON document

Build with the `Create` factories; implicit conversions cover the scalar values:

```csharp
var obj = JsonObject.Create([
    ("name", "Alice"),
    ("age", 30),
    ("tags", JsonArray.Create("admin", "user")),
    ("point", JsonObject.Create([ ("x", 1), ("y", 2) ])),
]);

var arr = JsonArray.Create(1, 2, 3);
```

The naming follows the same pattern as `Parse`: every factory has a `ReadOnly` twin
(`JsonObject.ReadOnly.Create`, `JsonArray.ReadOnly.Create`) that produces a frozen value with the
same call shape. The collection-initializer form (`new JsonObject { ["name"] = "Alice" }`)
compiles too; these pages use the factories, which read identically in their mutable and frozen
forms.

A value you intend to cache or share across threads must be read-only. Freeze a mutable one, or
build read-only directly:

```csharp
// deep read-only copy (self if already frozen); any attempt to modify frozen will throw
var frozen = obj.ToReadOnly();

// read-only from the start, with ("key", value) tuples
var ro = JsonObject.ReadOnly.Create([
    ("name", "Alice"),
    ("tags", JsonArray.ReadOnly.Create(["admin", "user"])),
]);
```

Mutating a read-only container throws `InvalidOperationException`, which is the feature: a cached
document cannot be corrupted by a caller. To go from a CLR value to the DOM without text in
between, use `JsonValue.FromValue(poco)` (or `JsonValue.ReadOnly.FromValue(poco)`).

## Edit a document

A mutable DOM edits in place, with the indexer (through the implicit conversions to `JsonValue`)
or the fluent, generic `Set`, which accepts any value the serializer knows, a whole POCO
included:

```csharp
obj["status"] = "online";        // set or replace a field
obj["point"]["x"] = 123;         // when "point" exists and is an object
obj.Remove("obsolete");
arr.Add(4);

obj                              // fluent Set, one edit per line
    .Set("count", 42)
    .Set("unit", "pages")
    .Set("author", author);      // Set<TValue> serializes any value, a POCO included
```

To build a DOM value from an existing collection, the `FromValues` helpers cover spans, arrays,
`IEnumerable<T>` (with an optional selector) and dictionaries, on both the mutable types and
their `ReadOnly` twins:

```csharp
JsonArray  tags   = JsonArray.FromValues(book.Tags);
JsonArray  titles = JsonArray.FromValues(books, b => b.Title);
JsonObject scores = JsonObject.ReadOnly.FromValues(scoresByName);   // frozen
```

There is no auto-creation on the raw DOM: assigning through a missing intermediate
(`obj["missing"]["x"] = 1`) throws. Create the child object first.

A read-only document cannot be edited in place, by design. "Editing" one is a copy-on-write
cycle: take a mutable copy, edit it, refreeze. The frozen original stands, so every cached
reference to it stays valid:

```csharp
var frozen = obj.ToReadOnly();

var draft = frozen.ToMutable();      // mutable copy; the original is untouched
draft["status"] = "offline";
var updated = draft.ToReadOnly();    // a second frozen document, carrying the edit
```

One copy is avoidable: when a method builds a document and returns it frozen, `Freeze()` marks
the instance itself read-only instead of copying it, the builder pattern for frozen documents.
Use it only on a value the method exclusively owns, since every reference to the instance
becomes read-only with it:

```csharp
static JsonObject BuildManifest()
{
    var m = JsonObject.Create();
    m["version"] = 3;
    // ...build freely, then freeze in place: no defensive copy
    return m.Freeze();
}
```

## Read and edit through the generated proxies

When a document has forty fields and the code needs three, do not deserialize it. Wrap the parsed
DOM in the generated read-only proxy and read the fields you need, typed:

```csharp
JsonObject doc = JsonObject.Parse(bytes);

// a typed view over the parsed document; nothing is copied or bound
AcmeSerializers.Book.ReadOnly book = AcmeSerializers.Book.ToReadOnly(doc);

// typed read, with IntelliSense
string title = book.Title;

// materialize the POCO, only if needed
Book poco = book.ToValue();
```

Edits on a read-only proxy go through copy-on-write: the original stays frozen, you get a new
frozen proxy (or take an explicit mutable one):

```csharp
// copy-on-write: the original proxy stays frozen
AcmeSerializers.Book.ReadOnly edited = book.With(m => { m.Year = 1966; });

// or take an explicit mutable proxy
AcmeSerializers.Book.Writable w = book.ToMutable();
w.Year = 1966;
```

Because the document underneath keeps every field it arrived with, a round-trip through a proxy
never drops the fields this version of the code does not know, which is the silent-truncation
problem [the explanation](index.md) opens with.

## Harden parsing for untrusted input

The parser is deliberately permissive by default (JavaScript comments and trailing commas are
accepted), which is wrong for input you do not control. Tighten it:

```csharp
var settings = CrystalJsonSettings.JsonStrict          // no comments, no trailing commas
    .ThrowOnDuplicateFields();          // a repeated key is an error, not last-wins

JsonValue value = JsonValue.Parse(payload, settings);
```

`JsonStrict` does not cover duplicate fields on its own; add `ThrowOnDuplicateFields()` when a
repeated key must fail. Content after the top-level value is rejected by default; to read several
consecutive documents out of one buffer, use `CrystalJson.ParseFragment`, not `WithTrailingData()`
(which parses the first value and silently drops the rest).

## Change how one member serializes

Most per-member needs are covered by attributes, with no code to write. Reach for these first:

```csharp
[JsonProperty("id")]                                  // rename on the output
public required string Id { get; init; }

[JsonProperty(DefaultValue = "draft")]                // the member's declared default
public string Status { get; init; } = "draft";

[JsonProperty(EnumFormat = JsonEnumFormat.Number)]    // this one enum stays numeric
public BookGenre Genre { get; init; }

[JsonProperty(NumberFormat = JsonNumberFormat.String)]
public long AccountId { get; init; }                  // "12345678901234567": JS-safe

[JsonBooleanLiterals("0", "1")]                       // legacy booleans, tolerant reads
public bool Enabled { get; set; }

[JsonBooleanLiterals(null, true)]                     // writes true, or omits the member
public bool Flagged { get; set; }
```

When no attribute covers the need, the member's type has its own compact form, or the value must
cross a legacy shape, write a **member converter** and attach it with CrystalJson's own
attribute. A realistic case: a coordinates struct stored as a compact `[lat, lon]` array instead
of an object with two named fields:

```csharp
public readonly record struct GpsPosition(double Latitude, double Longitude);

public sealed class GpsPositionConverter : IJsonMemberConverter<GpsPosition>
{
    public JsonValue Pack(
        GpsPosition value,
        CrystalJsonSettings? settings = null,
        ICrystalJsonTypeResolver? resolver = null)
    {
        return JsonArray.Create(value.Latitude, value.Longitude);
    }

    public GpsPosition Unpack(JsonValue value, ICrystalJsonTypeResolver? resolver)
    {
        var arr = value.AsArray();
        return new GpsPosition(arr.Get<double>(0), arr.Get<double>(1));
    }
}

[JsonConvertWith(typeof(GpsPositionConverter))]
public GpsPosition Position { get; init; }
// => "position": [ 48.8584, 2.2945 ], on both paths and in both directions
```

Converters never see null or missing: the pipeline handles those before the converter runs. A
converter that implements only one of `IJsonPacker<T>` / `IJsonDeserializer<T>` serves that
direction and default handling covers the other. Naming a type that implements neither is a loud
build error (`CJSON0010`), never a silent fallback.

## Custom-serialize a whole type

When the shape itself is hand-tuned (a compact id packed as an array of its parts), implement the
interfaces directly on the type:

```csharp
public interface IJsonPackable
{
    JsonValue JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver);
}

public interface IJsonDeserializable<TSelf>
{
    static abstract TSelf JsonDeserialize(
        JsonValue value,
        ICrystalJsonTypeResolver? resolver);
}
```

A concrete case: an order id made of a region and a sequence number, serialized as one compact
string (`"EU-000123"`) instead of an object with two named fields:

```csharp
public readonly record struct OrderId(string Region, int Number)
    : IJsonPackable, IJsonDeserializable<OrderId>
{
    public JsonValue JsonPack(
        CrystalJsonSettings settings,
        ICrystalJsonTypeResolver resolver)
    {
        return JsonString.Return($"{this.Region}-{this.Number:D6}");
    }

    public static OrderId JsonDeserialize(
        JsonValue value,
        ICrystalJsonTypeResolver? resolver = null)
    {
        // defensive: Required<string>() throws a JsonBindingException on null or missing
        string literal = value.Required<string>();
        int dash = literal.IndexOf('-');
        return new OrderId(literal[..dash], int.Parse(literal[(dash + 1)..]));
    }
}

string json = CrystalJson.Serialize(new OrderId("EU", 123));
// => "EU-000123", everywhere the type appears: standalone, as a member, in collections
```

The same interfaces produce a hand-tuned **object** shape. A date range whose upper bound is
optional decides its own members: `"to"` exists only when the range is closed, whatever the
settings say about null members:

```csharp
public sealed record DateRange(DateOnly From, DateOnly? To)
    : IJsonPackable, IJsonDeserializable<DateRange>
{
    public JsonValue JsonPack(
        CrystalJsonSettings settings,
        ICrystalJsonTypeResolver resolver)
    {
        var obj = JsonObject.Create("from", JsonString.Return(this.From));
        if (this.To is not null)
        {
            obj["to"] = JsonString.Return(this.To.Value);
        }
        return obj;
    }

    public static DateRange JsonDeserialize(
        JsonValue value,
        ICrystalJsonTypeResolver? resolver = null)
    {
        var obj = value.AsObject();
        return new DateRange(
            obj.Get<DateOnly>("from"),
            obj.Get<DateOnly?>("to", null));
    }
}

// => { "from": "2026-01-01" }                      open-ended
// => { "from": "2026-01-01", "to": "2026-03-31" }  closed
```

Declaring the resolver parameter with a default value is the convention (callers can omit it,
and the interface is still satisfied). `JsonPack` and `JsonDeserialize` must be inverses; pin
the round-trip in a test. Build values with the factories (`JsonString.Return(...)`,
`JsonNumber.Return(...)`, `JsonArray.ReadOnly.Create(...)`), and handle null or missing input
defensively, as `Required<string>()` does above. A type-level converter attached with
`[JsonConvertWith]` on the type is the alternative when you cannot modify the type itself.
