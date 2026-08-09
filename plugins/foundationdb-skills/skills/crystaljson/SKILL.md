---
name: crystaljson
description: >-
  How to use CrystalJson, the custom JSON library in SnowBank.Core (namespace SnowBank.Data.Json). Covers the JsonValue
  DOM (JsonObject / JsonArray / JsonString / JsonNumber / JsonBoolean / JsonNull / JsonDateTime), the read-only vs mutable
  model, the CrystalJson static API (Serialize / Parse / Deserialize) and CrystalJsonSettings, the Roslyn source generator
  for fast reflection-free serializers and read-only/writable proxies ([CrystalJsonConverter] / [CrystalSerializable]),
  the IJsonSerializable / IJsonPackable / IJsonDeserializable interfaces, MutableJsonValue / ObservableJsonValue and
  JsonPath. Use whenever code parses, builds, reads, mutates, or serializes JSON with these types, reads optional fields
  with defaults, declares a generated JSON converter/proxy, or implements custom JSON (de)serialization. Use it even when
  the request only says "serializer", "converter", or "serialize/deserialize a record, document, or model" without naming
  JSON: in SnowBank-based code (document stores, sync layers, Layers, models) document and message (de)serialization goes through
  CrystalJson, not System.Text.Json or Newtonsoft.
---

# CrystalJson (SnowBank.Data.Json)

CrystalJson is a high-performance, allocation-conscious JSON stack. It is **not** `System.Text.Json` or Newtonsoft -
the type names look familiar (`JsonObject`, `JsonArray`, ...) but the API is different. The namespace is
`SnowBank.Data.Json`. Add `using SnowBank.Data.Json;`.

There are **two layers**, used together:

1. **The DOM** - `JsonValue` and its subtypes. A mutable-or-immutable tree you build, parse, navigate, and serialize.
   Use it for schemaless / dynamic JSON (config, arbitrary documents, change records).
2. **The source generator** - `[CrystalJsonConverter]` + `[CrystalSerializable(typeof(T))]` generate fast,
   reflection-free, AOT-friendly converters for your POCOs, plus typed **read-only / writable proxies** over the DOM.
   Use it for your domain types.

`CrystalJson` (static class) is the entry point for serialize/parse/deserialize regardless of layer.

---

## 1. The JsonValue DOM

`JsonValue` is the abstract base. Concrete types and their `JsonType`:

| Type | `JsonType` | Notes |
|---|---|---|
| `JsonObject` | `Object` | key -> value map; mutable **or** read-only |
| `JsonArray` | `Array` | ordered list; mutable **or** read-only |
| `JsonString` | `String` | immutable |
| `JsonNumber` | `Number` | immutable; small ints cached |
| `JsonBoolean` | `Boolean` | immutable; only `True`/`False` singletons |
| `JsonDateTime` | `DateTime` | immutable; serialized as an ISO string |
| `JsonNull` | `Null` | three distinct singletons (below) |

**The three nulls - this trips people up:**

- `JsonNull.Null` - an **explicit** null that was present in the JSON (`{"x": null}`).
- `JsonNull.Missing` - a field that **was not there** (`obj["absent"]`) or an out-of-range array read.
- `JsonNull.Error` - result of an **invalid** access (e.g. indexing a non-array).

All three report `value.IsNull == true`. Distinguish them with `value.IsNullOrMissing()`, `value.IsMissing()`,
`value.IsError()`, or `ReferenceEquals(value, JsonNull.Missing)`. Parsing an empty/whitespace/`null` input gives
`JsonNull.Missing`; parsing the literal `"null"` gives `JsonNull.Null`.

Other useful singletons: `JsonBoolean.True/False`, `JsonNumber.Zero/One`, `JsonObject.ReadOnly.Empty`,
`JsonArray.ReadOnly.Empty`.

---

## 2. Read-only vs mutable - the core mental model

This is the most important concept. `JsonObject` and `JsonArray` can each be **mutable** or **read-only**
(`value.IsReadOnly`). Scalars (string/number/bool/null/datetime) are always read-only.

- **Mutating a read-only container throws** `InvalidOperationException` ("Cannot mutate ... because it is marked as
  read-only").
- A read-only value is safe to **cache and share across threads**.
- Conversions:
  - `value.ToReadOnly()` - returns self if already read-only, else a deep read-only copy.
  - `value.ToMutable()` - returns a mutable copy (minimal copying); use before editing a possibly-frozen value.
  - `value.Copy(deep: true, readOnly: false)` - explicit copy.
  - `value.Freeze()` - freezes in place (only on values you exclusively own).

**Build mutable, then optionally freeze; or build read-only directly with the `ReadOnly` factory.**

```csharp
using SnowBank.Data.Json;

// mutable (collection initializer)
var obj = new JsonObject
{
    ["name"]  = "Alice",          // implicit conversions from string/int/bool/double/...
    ["age"]   = 30,
    ["tags"]  = new JsonArray { "admin", "user" },
    ["point"] = new JsonObject { ["x"] = 1, ["y"] = 2 },
};
var arr = new JsonArray { 1, 2, 3 };

// read-only directly (good for cached/shared constants) - note the ("key", value) tuple form
var ro = JsonObject.ReadOnly.Create([
    ("name", "Alice"),
    ("age", 30),
    ("tags", JsonArray.ReadOnly.Create(["admin", "user"])),
]);

// from a CLR value (POCO, collection, primitive)
JsonValue v   = JsonValue.FromValue(myPoco);              // mutable
JsonValue rov = JsonValue.ReadOnly.FromValue(myPoco);     // read-only

obj.ToReadOnly();   // freeze for caching
ro.ToMutable();     // get a mutable copy to edit
```

---

## 3. Reading and navigating

Indexers **never throw** on a missing key/index - they return `JsonNull.Missing` (or `JsonNull.Error`), so you can chain
safely:

```csharp
JsonValue city = obj["user"]["address"]["city"];   // Missing if any hop is absent; no NRE
bool present   = !obj["user"].IsNullOrMissing();
```

**Read + convert in one step** (the everyday API):

```csharp
// Get<T>: optional with default, or required (throws JsonBindingException if null/missing/incompatible)
int    age   = obj.Get<int>("age", 0);          // default if absent
string name  = obj.Get<string>("name");         // throws if absent/null
Guid   id    = obj.Get<Guid>("id");

// TryGet
if (obj.TryGet<string>("email", out var email)) { /* ... */ }

// typed children
JsonObject child = obj.GetObjectOrEmpty("meta");   // never null; empty read-only object if absent
JsonArray  items = obj.GetArray("items");          // throws if not an array
if (obj.TryGetObject("meta", out var meta)) { /* ... */ }

// arrays
int count = items.Count;
string first = items.Get<string>(0);
foreach (var item in items) { /* JsonValue */ }
foreach (var o in items.AsObjects()) { /* JsonObject items only */ }
```

**Convert a `JsonValue` to a CLR type** (when you already hold the value):

```csharp
string? s = jv.As<string>();          // default(T) (null) if the value is null/missing
int     n = jv.As<int>(-1);           // custom default if null/missing
string  r = jv.Required<string>();    // throws if null/missing
```

`As<T>` / `Get<T>` support primitives, `Guid`/`Uuid*`, `DateTime`/`DateTimeOffset`/`DateOnly`/`TimeSpan`,
NodaTime `Instant`/`Duration`, `Uri`, `byte[]`/`Slice`, arrays/`List<T>`, and your POCOs. Numbers/dates use
**InvariantCulture**.

---

## 4. CrystalJson: serialize / parse / deserialize

```csharp
using SnowBank.Data.Json;

// SERIALIZE a CLR value -> JSON
string json   = CrystalJson.Serialize(value);                          // formatted, single line
string compact= CrystalJson.Serialize(value, CrystalJsonSettings.JsonCompact);
string pretty = CrystalJson.Serialize(value, CrystalJsonSettings.JsonIndented);
Slice  bytes  = CrystalJson.ToSlice(value, CrystalJsonSettings.JsonCompact);   // UTF-8
byte[] raw    = CrystalJson.ToBytes(value);
CrystalJson.SerializeTo(textWriterOrStream, value);                    // streaming

// PARSE text/bytes -> DOM: parse through the DOM types, not through CrystalJson.*
JsonValue  any = JsonValue.Parse(json);      // string, Slice, ReadOnlySpan<char/byte>
JsonObject o   = JsonObject.Parse(json);     // throws if it is not an object
JsonArray  a   = JsonArray.Parse(json);      // throws if it is not an array
// READ-ONLY (cache-safe) twin of each: the nested ReadOnly class, same entry points
JsonValue roDom = JsonValue.ReadOnly.Parse(json);   // also JsonObject.ReadOnly.Parse, etc.

// DESERIALIZE text/bytes -> POCO (parse + bind)
Book book  = CrystalJson.Deserialize<Book>(json);                      // throws if the JSON is null
Book? maybe= CrystalJson.Deserialize<Book>(json, defaultValue: null);  // null instead of throwing

// Serialize a JsonValue back to text/bytes
string s2 = value.ToJsonText();                  // or ToJsonText(settings)
Slice  b2 = value.ToJsonSlice(CrystalJsonSettings.JsonCompact);
```

**Parse (DOM) vs Deserialize (POCO):** `Parse` returns a `JsonValue` tree you navigate; `Deserialize<T>` binds straight
to your type. A `null`/empty/missing input deserializes to `null` -> throws for a non-nullable `T` unless you pass a
`defaultValue`.

The intended split: **`CrystalJson.*` serves the POCO route** (`Serialize`, `Deserialize`, `ToSlice`), **the DOM
parses through the DOM types** (`JsonValue.Parse`, `JsonObject.Parse`, `JsonArray.Parse`, each a `new static`
returning the derived type, throwing when the payload has another shape). Pick `JsonObject.Parse` when a non-object
payload is a bug (let it throw); pick `JsonValue.Parse` plus a type test when it is an ordinary case to handle.
(`CrystalJsonDomWriter.ParseObject(value)` is unrelated: it goes the other way, CLR value -> DOM.)

### CrystalJsonSettings

Settings are **immutable and cached**; start from a preset and compose with fluent methods.

Presets: `CrystalJsonSettings.Json` (default), `.JsonCompact`, `.JsonIndented`, `.JsonReadOnly` (parse a read-only DOM),
`.JsonStrict`, `.JsonIgnoreCase` (case-insensitive field matching), and `JavaScript*` variants.

Common fluent options (chainable, e.g. `CrystalJsonSettings.Json.Compacted().CamelCased()`):

- Layout: `.Compacted()`, `.Indented()`, `.Formatted()`
- Naming: `.CamelCased()`, `.PascalCased()`
- Nulls/defaults: `.WithoutNullMembers()` (default), `.WithNullMembers()`, `.WithoutDefaultValues()`
- Enums: `.WithEnumAsStrings()` (**the default since 7.4.3**), `.WithEnumAsNumbers()` - see *Enums in the output* in section 9 for
  what changed and the recipes that restore numbers
- Dates: `.WithIso8601Dates()` (default), `.WithMicrosoftDates()` (emits `"\/Date(ms)\/"`; **reading** that legacy
  format always works, with or without this setting)
- Durations: `.WithNumericDurations()` (default: `TimeSpan` as a number of seconds), `.WithIso8601Durations()`
  (emits the legacy `"P1DT2H3M4.005S"` duration string; **reading** both forms always works) *(7.4.3+)*
- Dictionaries: `.WithDictionariesAsMaps()` (default, `{"k":v}`), `.WithDictionariesAsPairArrays()` (emits the legacy
  `[{"Key":k,"Value":v}]` shape; again, **reading** both shapes always works) *(7.4.3+)*
- Read-only result: `.AsReadOnly()`

**Parsing leniency** (deserialization only; none of these change what you emit). The parser is deliberately
permissive by default, which is wrong for untrusted input:

| Option | Default | Tighten with | Loosen with |
|---|---|---|---|
| JavaScript comments (`// ...`, `/* ... */`) | accepted | `.WithoutComments()` | `.WithComments()` |
| trailing commas (`[1, 2, ]`) | accepted | `.WithoutTrailingCommas()` | `.WithTrailingCommas()` |
| content after the top-level value | rejected | `.WithoutTrailingData()` | `.WithTrailingData()` |
| duplicate field names | last one wins | `.ThrowOnDuplicateFields()` | `.FlattenDuplicateFields()` |

⚠️ The **property** is `settings.AllowTrailingData`; the fluent **method** that sets it is `.WithTrailingData()`.
There is no `.AllowTrailingData()` method. Same shape for the others: read a `bool` property, set it with a
`With*` / `Without*` method.

`CrystalJsonSettings.JsonStrict` is the shorthand for the first two rows (no comments, no trailing commas). It
does **not** touch duplicate fields, so add `.ThrowOnDuplicateFields()` yourself if a repeated key must be an
error rather than a silent overwrite.

To read several consecutive documents out of one buffer, use `CrystalJson.ParseFragment` or the streaming
reader instead of `.WithTrailingData()`, which parses the first value and silently drops the rest.

---

## 5. The source generator (your domain types)

For POCOs, prefer the generator over the DOM: it emits a fast, reflection-free, AOT-friendly converter **and** typed
read-only/writable proxies. (This is how the document-collection layers built on this stack store their documents.)

### Declare

Put `[CrystalSerializable(typeof(T))]` (one per root type) on a `public static partial class` marked
`[CrystalJsonConverter]`. Nested types are discovered automatically. Use `[JsonProperty("name")]` to rename a field.

```csharp
using SnowBank.Data.Json;

public sealed record Book
{
    [JsonProperty("id")]    public required string Id { get; init; }
    [JsonProperty("title")] public required string Title { get; init; }
    [JsonProperty("year")]  public int Year { get; init; }
    public Author? Author { get; init; }   // nested type: auto-discovered
}

[CrystalJsonConverter]                       // or [CrystalJsonConverter(CrystalJsonSerializerDefaults.Web)] for camelCase + ignore-case
[CrystalSerializable(typeof(Book))]
public static partial class AcmeSerializers { }       // generated members land here
```

#### The container vocabulary *(7.4.4+)*

`[CrystalJsonConverter]` is a **mono-format alias**: it means "this class hosts generated code" **plus**
"produce the JSON format, with these parameters". The two halves also exist separately, which is what a
container producing several formats needs:

| Attribute | Namespace | Role |
|---|---|---|
| `[CrystalConverter]` | `SnowBank.Data` | the container marker; says nothing about the formats |
| `[CrystalSerializable(typeof(T))]` | `SnowBank.Data` | enrolls a root type; repeatable; feeds every output format |
| `[CrystalJsonOutput(...)]` | `SnowBank.Data.Json` | requests the JSON format (profile, naming policy, case-insensitivity) |
| `[CrystalXmlOutput(...)]` | `SnowBank.Data.Xml` | requests the XML format (see `Documentation/CrystalXml.md`) |
| `[CrystalJsonConverter(...)]` | `SnowBank.Data.Json` | alias: `[CrystalConverter]` + `[CrystalJsonOutput]`, JSON only |
| `[CrystalXmlConverter(...)]` | `SnowBank.Data.Xml` | alias: `[CrystalConverter]` + `[CrystalXmlOutput]`, XML only |

Rules the compiler enforces: a `[CrystalConverter]` naming no output format is refused (`CRYS0001`);
a mono-format alias next to an output attribute is refused (`CRYS0002` - use `[CrystalConverter]` with
explicit output attributes instead); several container markers on one class are refused (`CRYS0003`).

```csharp
// a container that produces BOTH formats from one set of enrolled types
[CrystalConverter]
[CrystalJsonOutput(CrystalJsonSerializerDefaults.Web)]
[CrystalXmlOutput]
[CrystalSerializable(typeof(Book))]
public static partial class MyWires { }
```

`[CrystalJsonSerializable(typeof(T))]` is the former spelling of `[CrystalSerializable]`: still working
and byte-identical, but `[Obsolete]` (enrollment never was JSON-specific).

### Self-serializable types: the entity IS its own container *(7.4.3+)*

The container above (`AcmeSerializers`) is one way to enroll a type. The other is to let the type carry its own
generated code, which is what you want when a **layer** owns a vocabulary and should not force every consuming
application to also declare a JSON container.

`[CrystalJsonSelfSerializable]` is a **meta-attribute**: you put it on one of *your own* attribute classes, and
every type decorated with that attribute is opted into generation.

```csharp
// the layer declares its vocabulary ONCE
[CrystalJsonSelfSerializable]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class MyEntityAttribute : Attribute { }

// the application just declares an entity - no container, no [CrystalSerializable]
[MyEntity]
public sealed partial record Widget
{
    public required string Name { get; init; }
    public Author? Author { get; init; }      // referenced types are still crawled
}
```

Everything generated lands inside **one** nested static class named `Json`, so the entity reserves exactly one
member name:

```csharp
Widget.Json.Default          // the converter (the container mode's AcmeSerializers.Widget)
Widget.Json.ReadOnly         // read-only proxy
Widget.Json.Writable         // writable proxy
Widget.Json.PropertyNames    // property-name constants
Widget.Json.GetResolver()    // the per-container resolver, same as the container mode
Widget.Json.ToJsonText(w)    // the static helpers live there too
```

Referenced types nest inside that same scope under their plain names: `Author`'s converter is
`Widget.Json.Author.Default`. Inside the scope they cannot shadow the referenced type in the entity's own
source, which is what the single reserved name buys.

The one-name rule is the design, not an implementation detail: a future generator for another format claims a
sibling scope (`Widget.Cbor`) without renegotiating anything. It also means the `Json` scope is entirely
generated code, so it carries the generated-code attributes (`GeneratedCode`, `DebuggerNonUserCode`,
`ExcludeFromCodeCoverage`, `DynamicallyAccessedMembers`) that could not be put on the entity partial, since
that partial is your source.

This is how a document-collection attribute works in a layer built on this stack: the application writes one
attribute on the entity, and the JSON converter, both proxies and the layer's own generated schema all fall
out of it.

Things to know before you use it:

- The entity must be **`partial`, non-generic, and not nested**. The generator rejects the others with
  `CJSON0004` / `CJSON0005`.
- **Your entity may not declare a member named `Json`.** That is `CJSON0006`, an error, reported at the entity
  declaration in your own source. The message carries the remedies: rename the member and keep its serialized name
  with `[JsonProperty("json")]`, or move the type to a `[CrystalJsonConverter]` container instead.
- **A referenced type named like a scope member** (`Default`, `ReadOnly`, `Writable`, `PropertyNames`, …) is
  `CJSON0007`, a warning. That type is excluded from generation and falls back to runtime serialization, so it
  still works, just without a generated converter.
- Hint names are namespace-qualified in this mode, because entity names collide across namespaces far more
  often than container names do.
- The `[CrystalJsonConverter]` + `[CrystalSerializable]` container path is untouched and still correct.
  Prefer the container when you are enrolling third-party types you cannot annotate, or a set of unrelated
  types; prefer self mode when the type is yours and a layer already marks it.

### csproj wiring (the part most often gotten wrong)

Reference the generator project/package **as an analyzer**, and ensure C# 9+:

```xml
<PropertyGroup>
  <LangVersion>latest</LangVersion>
</PropertyGroup>
<ItemGroup>
  <ProjectReference Include="path/to/SnowBank.Serialization.Json.CodeGen.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

(If consuming via NuGet, the analyzer ships with `SnowBank.Core` / its codegen package.)

**The language floor is C# 9, and the generator checks it explicitly**: below C# 9 it reports `SYSLIB1221`
("language version not supported by the source generator") and generates nothing. `latest` clears the gate with
margin, but the actual requirement is only `>= 9` — the classic trigger is a ported legacy project still pinning
a netfx-era `<LangVersion>7.3</LangVersion>`. No other project setting is required: the generated files fully
qualify every name (they work without `ImplicitUsings`) and are warning-free under `#nullable enable`, including
for members declared in nullable-oblivious code.

### Use

```csharp
// POCO <-> JSON text
string json = AcmeSerializers.Book.ToJsonText(book);
Book   back = AcmeSerializers.Book.Deserialize(json);

// POCO <-> JsonValue (DOM)
JsonValue packed = AcmeSerializers.Book.Pack(book);
Book      from   = AcmeSerializers.Book.Unpack(jsonObject);

// runtime resolver (pass to CrystalJson APIs and to layers that must resolve your converters)
ICrystalJsonTypeResolver resolver = AcmeSerializers.GetResolver();
```

### Read-only / writable proxies (zero-copy typed views over the DOM)

```csharp
AcmeSerializers.Book.ReadOnly ro = AcmeSerializers.Book.ToReadOnly(book);   // typed read-only view over a JsonValue
string title = ro.Title;                                  // typed property read
JsonValue dom = ro.ToJsonValue();                         // underlying (read-only) JsonValue
Book poco = ro.ToValue();                                 // materialize the POCO

// edit via copy-on-write: the original proxy is unchanged, you get a new frozen proxy
AcmeSerializers.Book.ReadOnly edited = ro.With(m => { m.Year = 2025; });

// or an explicit mutable proxy
AcmeSerializers.Book.Writable w = ro.ToMutable();
w.Year = 2025;
```

Absence propagates through a proxy the way it does through the DOM: a chain across a missing inner object keeps
navigating (`proxy.Metadata.IsNullOrMissing()` tells you), an optional member reads as its default, and a
`required` member absent from the document throws `JsonBindingException`, never a `NullReferenceException`
(pinned by `Test_JsonReadOnlyProxy_With_Empty_Object` in `SnowBank.Serialization.Json.CodeGen.Tests`).

Note: `.With(...)` (copy-on-write edit) is a method on the GENERATED typed proxies shown here, not on a raw DOM
`JsonObject`/`JsonArray`. For a plain DOM value there is no `.With(...)`: freeze with `value.ToReadOnly()` and edit a
copy with `value.ToMutable()` (section 2), then set fields via the indexer.

---

## 6. Mutating JSON: MutableJsonValue (and ObservableJsonValue)

`MutableJsonValue` is a mutation proxy used inside "write" closures (document updates in a collection layer, the `doc.Write(root => ...)` closures of a reactive layer).
`ObservableJsonValue` is the read side that tracks which fields were read (for reactive views). You usually interact via
the `root` handed to a write callback:

```csharp
doc.Write(root =>
{
    root["status"].Set("online");                 // set a scalar field
    root.Set("count", 42);                         // typed set (auto-converts the CLR value)
    root["point"]["x"].Set(123);                   // nested set (intermediate objects auto-created via GetOrCreateObject)
    root.Set(JsonPath.Create("a.b[0]"), "deep");   // path-based set
    root["items"].Add("newItem");                  // APPEND to the array at root["items"]
});
```

**Footgun - `Add` means different things on objects vs arrays:**

- `root.Add("field", value)` adds a **field** to the object (throws if the field already exists).
- `root["field"].Add(value)` **appends** to the array at `root["field"]`.

They are not interchangeable. Re-creating an existing field with `Add` throws; to append, index into the array first.

**Don't hold a child proxy across a parent mutation** - it goes stale. Re-get it, or do it in one chain:

```csharp
// stale:
var s = root["settings"]; root["settings"].Set(newSettings); s["k"].Set(v);   // BUG: s is stale
// good:
root["settings"]["k"].Set(v);
```

---

## 7. Custom (de)serialization: the IJson* interfaces

When the generator can't cover a type (e.g. a hand-tuned encoding), implement these directly:

```csharp
public interface IJsonSerializable          { void JsonSerialize(CrystalJsonWriter writer); }
public interface IJsonPackable              { JsonValue JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver); }
public interface IJsonDeserializable<TSelf> { static abstract TSelf JsonDeserialize(JsonValue value, ICrystalJsonTypeResolver? resolver); }
```

By convention the concrete `JsonDeserialize` implementation declares the resolver with a default (`ICrystalJsonTypeResolver? resolver = null`)
so callers can omit it; that still satisfies the interface. `JsonPack` (to DOM) and `JsonDeserialize` (from DOM) must be inverses - round-trip them in a test. Build values with
`JsonString.Return(...)`, `JsonNumber.Return(...)`, `JsonArray.ReadOnly.Create(...)`. Handle null/missing defensively in
`JsonDeserialize`. (Example in the wild: a compact id type packed as a `JsonArray` of its parts.)

### Member converters: converting ONE member, not the whole type *(7.4.3+)*

The interfaces above take over an entire type. When you only need to change how **one member** is written, attach the
behaviour to that member instead - and **reach for a built-in attribute before writing any code.**

### Start here: the built-in, `[JsonBooleanLiterals]`

Booleans written as `"0"`/`"1"` (or `0`/`1`) are the common legacy case, and there is nothing to implement:

```csharp
[JsonBooleanLiterals("0", "1")]                          // strings; tolerant read
[JsonBooleanLiterals("0", "1", StrictLiterals = true)]   // rejects genuine true/false
[JsonBooleanLiterals(0, 1)]                              // numbers
[JsonBooleanLiterals(null, "1")]                         // true -> "1", false -> member ABSENT
[JsonBooleanLiterals(null, true)]                        // true -> true, false -> member ABSENT
public bool Enabled { get; set; }
```

A **null** first argument means the member is **not emitted at all** when the value is false, for a consumer that
expects either the member absent or the true literal. `[JsonBooleanLiterals(null, true)]` is the idiom for
"emit true, or emit nothing": the output stays ordinary JSON booleans and the only thing the attribute changes is
the omission, which says the intent in C# better than a condition whose "default" the reader has to decode as
false. It is exactly equivalent to `[JsonBooleanLiterals("0","1")]` plus
`[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]` (pinned byte-identical by a test), and
`[JsonIgnore(Condition = ...)]` remains the general mechanism for every other member type. `[JsonBooleanLiterals(false, true)]`
is legal and does nothing: both literals are what a `bool` serializes to anyway.

Arguments accept a **string, a bool, or a numeric value**, and the two do not have to match
(`[JsonBooleanLiterals("0", 1)]` is legal, because legacy formats are not always consistent). Anything else is
refused: the reflection path throws when the contract is built, the generator reports **`CJSON0017`**, same
message. The parameters are typed `object` so that `null` can carry the omit meaning, which is why that check
exists at all: it replaces the compiler check the old typed constructors gave you.

With no false literal, a present JSON `false` reads as **false even under `StrictLiterals`**: absence already
means false in that shape, so an explicit `false` is the same state spelled out. Strict still rejects anything
that is not a configured literal. That combination is incoherent though, so the generator warns (`CJSON0018`).

**Why that one is generator-only, when the other diagnostics exist on both paths:** `CJSON0015` and `CJSON0016`
refuse constructs that would otherwise **behave differently** on the two paths, so they have to exist on both, and
the reflection path throws the identical message at contract build. `CJSON0018` changes no behaviour at all: it is
advice about a pointless combination, and a compile-time nudge is the whole feature. There is no sensible moment
to emit a warning during contract build, and throwing would be wildly out of proportion.

Reads are tolerant by default (the configured literals, case-insensitively, **and** genuine `true`/`false`), so a
modernized producer needs no redeploy; an unknown literal is a `JsonBindingException`, never a silent `false`. Applying
it to a non-boolean member fails loudly (reflection: at metadata build; generator: error `CJSON0009`).

### When no built-in covers it: a member converter

For anything else (a domain scalar as a string, a packed identifier), write a converter. Recognition is **per facet**: a
converter is honored for whichever of `IJsonPacker<T>` (writing) and `IJsonDeserializer<T>` (reading) it implements, so
a write-only or read-only type implements one side. `IJsonMemberConverter<T>` is the convenience bundle for the
symmetric case, never a requirement:

```csharp
public interface IJsonMemberConverter<T> : IJsonPacker<T>, IJsonDeserializer<T> { }   // zero new members

// shown on a bool purely because it is the shortest possible body -
// for THIS behaviour use [JsonBooleanLiterals] above and write no converter at all
public sealed class BoolAsBitStringConverter : IJsonMemberConverter<bool>
{
    public JsonValue Pack(bool v, CrystalJsonSettings? s = null, ICrystalJsonTypeResolver? r = null) => JsonString.Return(v ? "1" : "0");
    public bool Unpack(JsonValue v, ICrystalJsonTypeResolver? r) => /* tolerant read */;
}

[JsonConvertWith(typeof(BoolAsBitStringConverter))]   // CrystalJson's own attribute - preferred
public bool Enabled { get; set; }                     // output: "1" / "0", both paths, both directions
```

Converters never see `null`: null and missing are handled before the converter runs (and a converter written for `T`
lifts over a `T?` member for free). Using the **missing** direction fails loudly, naming the facet to add - there is no
silent fallback to the default path. A source-generated `IJsonConverter<T>` implements both parents, so a generated
whole-type converter can be named on a member with no adapter.

**Nullable members** *(7.4.3+)*: a converter may be declared for the nullable form itself
(`IJsonMemberConverter<DateTime?>` on a `DateTime?` member). The probe order is **exact form first, then the lift**, on
both paths, so a `T?` declaration takes over every **present** value and can answer "no value" for a
present-but-unreadable input (`""`, an unparseable token) - distinctly from `default(T)`, which stays a real value, and
from JSON null/missing, which stay the pipeline's on both sides. The `T?` declaration transfers the **read side only**:
`Pack` still never sees null, and the null-member format follows the settings (`WithNullMembers()` etc.), so *a member
converter cannot represent absence for a nullable member whose absent form is not JSON null, unless it is declared for
the nullable type itself - and even then only when reading*. A `T?` converter named on a NON-nullable member is refused
loudly (reflection: at metadata build; generator: `CJSON0010`).

**Three spellings, in precedence order** - all feed the same per-member slot, honored by the reflection path *and* the
generator (including the generated proxies). Precedence is the order the runtime resolves them, not a recommendation:
prefer a built-in when one fits, then `[JsonConvertWith]`.

| Spelling | When to use it | Names a type with neither facet |
|---|---|---|
| `[JsonConvertWith(typeof(X))]` | your own converter; CrystalJson's own attribute, which System.Text.Json never inspects | **fails loudly** (reflection: on metadata build; generator: error `CJSON0010`) |
| `[JsonBooleanLiterals("0", "1")]` | **prefer this for booleans** - built in, no converter to write | n/a |
| STJ / Newtonsoft `[JsonConverter(typeof(X))]` | only when the named type is a **real dual-shape converter** both serializers can execute | **silently ignored** (keeps a half-migrated DTO serializable; starts working once `X` gains a facet) |

Both spellings also work **on a type**, covering standalone values, DTO members and collection elements on all paths; a
type-level converter wins over the duck-typed `JsonSerialize`/`JsonPack` methods and the `IJson*` interfaces.

### Generator diagnostics

| Id | Level | Meaning |
|---|---|---|
| `CJSON0004` / `CJSON0005` | error | self-serializable entity is generic, nested, or not `partial` (section 5) |
| `CJSON0006` | error | the entity declares a member named `Json`, colliding with the generated scope (section 5) |
| `CJSON0007` | warning | a referenced type is named like a scope member; excluded from generation, falls back to runtime |
| `CJSON0008` | **error** | an unconditional `[JsonIgnore]` on a member that also carries `[DataMember]`, `[JsonInclude]` or `[JsonProperty]`: a dual-output DTO is not supported - split it, one DTO per serializer (the reflection path throws the same refusal at contract build). The `[IgnoreDataMember]` sibling shape stays a warning |
| `CJSON0009` | error | `[JsonBooleanLiterals]` on a non-boolean member |
| `CJSON0010` | error | `[JsonConvertWith]` names a type implementing neither converter facet |
| `CJSON0012` | warning (suppressible) | an `internal` member with no include/exclude signal: serialized by generated converters, invisible to reflection; pin the intent with `[JsonInclude]` or `[JsonIgnore]` |
| `CJSON0015` | error | a serialization callback (`[OnSerializing]` and friends) declares the legacy `StreamingContext` parameter; drop it, or replace it with `JsonValue`/`JsonObject`/`JsonArray`. The reflection path throws the same message at contract build |
| `CJSON0016` | error | a type declares `[OnDeserializing]` alongside a `required` or `init`-only member. The pre-populate callback must observe an unpopulated instance, so members are assigned after construction, which neither of those allows; the message names the member and the remedy for its specific construct |
| `CJSON0017` | error | a `[JsonBooleanLiterals]` argument has a type with no JSON format form; use a string, a bool, or a numeric value. The reflection path throws the same message when the contract is built |
| `CJSON0018` | warning (suppressible) | `StrictLiterals` combined with a null false literal: strict mode enforces the configured literals, and with no false literal there is nothing on the false side to enforce. Generator-only on purpose, because it changes no behaviour (see below) |
| `CJSON0013` | error | a format profile (`DataContractCompat`) combined with a naming policy (camelCase and friends); use the dual-container pattern instead. `PropertyNameCaseInsensitive` is NOT a trigger: it is a read-side tolerance for incoming member names and changes nothing about what the profile writes |

---

## 8. JsonPath

`JsonPath` addresses a nested location with dot/bracket notation:

```csharp
var p = JsonPath.Create("user.address.city");
var q = JsonPath.Create("items[0]");
var last = JsonPath.Create(^1);     // last item; ^0 is the append position

JsonValue v = obj.GetPathValueOrDefault(p);
root.Set(p, "new value");           // on a MutableJsonValue
```

---

## 9. Attributes: what each path honors (and legacy interop)

There are **two independent (de)serialization paths, with different attribute vocabularies**. Getting this wrong
silently changes the output, so check the matrix before annotating a DTO:

- **Reflection** (`CrystalJsonTypeResolver`) - what `CrystalJson.Serialize` / `Deserialize<T>` / `JsonValue.FromValue`
  use for a type with no generated converter.
- **Source generator** - what `[CrystalJsonConverter]` + `[CrystalSerializable(typeof(T))]` emit.

| Attribute | Reflection | Source generator |
|---|---|---|
| `[JsonProperty("x")]` (SnowBank; also `DefaultValue`, `EnumFormat`) | YES (all three) | YES (all three) *(`EnumFormat` since 7.4.3, proxy setters included)* |
| STJ `[JsonPropertyName("x")]` | YES | YES |
| STJ `[JsonIgnore]` / `[JsonIgnore(Condition = ...)]` | YES, full STJ semantics *(7.4.3+)* | YES, full STJ semantics *(7.4.3+)* |
| `[IgnoreDataMember]` | YES *(7.4.3+)* - excludes the member on a non-`[DataContract]` type; on a `[DataContract]` type the `[DataMember]` opt-in still governs (DCJS's own precedence) | counts as an ignore signal *(7.4.3+)* |
| STJ `[JsonInclude]` (non-public members/accessors) | YES *(7.4.3+)*, a superset of STJ | YES *(7.4.3+)* - reached through accessor thunks (`[UnsafeAccessor]` where the TFM has it, reflection accessors downlevel); `SYSLIB1038` is retired. An `internal` member with NO include/exclude signal still diverges (generated-only inclusion, kept for format compatibility) and gets the suppressible warning `CJSON0012` nudging an explicit `[JsonInclude]` or `[JsonIgnore]` |
| STJ `[JsonPolymorphic]` + `[JsonDerivedType]` | YES (discriminator name **and** values are free-form) | YES |
| `[JsonConvertWith(typeof(X))]` (SnowBank), members **and** types | YES *(7.4.3+)* | YES *(7.4.3+)* |
| `[JsonBooleanLiterals]` (SnowBank) | YES *(7.4.3+)* | YES *(7.4.3+)* |
| STJ / Newtonsoft `[JsonConverter(typeof(X))]`, members **and** types | YES *(7.4.3+)* when `X` implements a converter facet; **silently ignored** otherwise (see section 7) | YES *(7.4.3+)*, same posture, facet checked at compile time |
| Enum custom literals: `[JsonStringEnumMemberName]` (STJ 9+) / `[EnumMember(Value=...)]` on enum fields | YES *(7.4.3+)*, both directions | YES *(7.4.3+)*, both directions |
| `[Key]` (DataAnnotations) | flag only | YES (drives the proxy Id) |
| C# `required` | **enforced** on read *(7.4.3+)*: null-or-missing throws `JsonBindingException` | **enforced** (throws on a missing member) |
| `[DataContract]` + `[DataMember]` (opt-in + `Name=`) | YES, **including non-public members and accessors** *(7.4.3+, the hybrid rule: the attribute pair is the explicit opt-in and accessibility does not filter it, matching DCJS; `[JsonInclude]` alone still grants nothing there)* | YES *(7.4.3+)*, the same model: `[DataMember]` is the sole membership opt-in, accessibility does not filter (non-public members are reached through accessor thunks), and `Name=` renames are honoured |
| `[DataMember]`'s `IsRequired = true` | **read side enforced** *(7.4.3+)*, DCJS-faithful: an ABSENT member throws, an explicit `null` satisfies (deliberately distinct from C# `required`, where null also throws). Write side unchanged (`WithNullMembers()` stays the frozen-reader recipe) | YES *(7.4.3+)*, same semantics, emitted as one presence guard per member |
| `[DataMember]`'s `Order` / `EmitDefaultValue` | no | no |
| `[OnSerializing]` / `[OnSerialized]` / `[OnDeserializing]` / `[OnDeserialized]` | YES *(7.4.3+)*, `void M()` on all four and `void M(JsonValue\|JsonObject\|JsonArray)` on the deserialize pair; the legacy `void M(StreamingContext)` throws at contract build | same shapes accepted; the legacy `StreamingContext` form is build error `CJSON0015` at the callsite |
| STJ `IJsonOnSerializing` / `IJsonOnSerialized` / `IJsonOnDeserializing` / `IJsonOnDeserialized` interfaces | no, deliberately (see *Lifecycle* below) | no, deliberately |
| `[CollectionDataContract]`'s `Name` / `ItemName` / `KeyName` / `ValueName` | no, and **neither does DCJS**: those four names shape the XML format only, so there is nothing to reproduce (see below) | no |
| Newtonsoft `[JsonProperty]` (name only) | YES | no |
| `[XmlIgnore]` / `[XmlElement]` / `[XmlAttribute]` | YES (non-`[DataContract]` types) | no |
| STJ `[JsonPropertyOrder]`, `[JsonNumberHandling]`, `[JsonExtensionData]`, `[JsonConstructor]` | no | no |

`[JsonIgnore(Condition = ...)]` follows System.Text.Json *(7.4.3+, both paths, and both the text writer and
`JsonValue.FromValue`/`Pack`)*:

| Condition | Effect on serialization |
|---|---|
| `Always` (the default, i.e. bare `[JsonIgnore]`) | member excluded from **both** directions |
| `Never` | always emitted, even null/default - overrides `WithoutDefaultValues()` / `WithoutNullMembers()` |
| `WhenWritingNull` | omitted when null, whatever the settings say |
| `WhenWritingDefault` | omitted when equal to the member's default, whatever the settings say |

Two intentional deviations from STJ: `WhenWritingDefault` compares against the member's **declared** default (so
`[JsonProperty(DefaultValue = 7)]` composes with it), and `WhenWritingNull` on a non-nullable value type is inert
instead of throwing.

### Enums in the output *(the 7.4.2 -> 7.4.3 format change)*

**Enums serialize as their string literal by default, in every form** - the text writer, the DOM
(`JsonValue.FromValue`, reflection `Pack`) and generated converters, which now agree byte-for-byte. This is a
deliberate divergence from System.Text.Json (whose default is numeric), chosen so payloads are self-describing and
friendly to JavaScript clients. **Reading is unchanged and tolerant in both directions**: names and custom tokens bind
case-insensitively (`"fooBar"` == `"FooBar"` == `"FOOBAR"`), and numbers and numeric strings are still accepted - so
every pre-7.4.3 payload still deserializes, and mixed fleets can roll forward incrementally.

| Value | 7.4.2 output | 7.4.3 output |
|---|---|---|
| `DayOfWeek.Friday` through `CrystalJson.Serialize` | `5` | `"Friday"` |
| `DayOfWeek.Friday` through `JsonValue.FromValue` | `"Friday"` (it always stringified, ignoring the settings) | `"Friday"` (settings now honored) |
| Undeclared flags combination through `JsonValue.FromValue` | `"7"` (the number, in a string) | `"ReadWrite, Delete"` (composed) |
| Enum field carrying `[EnumMember(Value="C")]` | `0` (the attribute was ignored) | `"C"` |

Flags compose exactly like `Enum.ToString("G")`. Custom literals are read off the enum's own fields, by attribute
name (no STJ package reference or version floor): `[JsonStringEnumMemberName("C")]` (STJ 9+) and `[EnumMember(Value="C")]`
(the DataContract spelling Newtonsoft also honors). When both are present STJ wins for **writing**; every spelling is
accepted on **read**. Attribute-set tokens are never camelCased.

**To get numbers back**, pick the narrowest scope that fixes the break:

- **Per call / per endpoint:** `CrystalJsonSettings.Json.WithEnumAsNumbers()` (composes with every other option, e.g.
  `CrystalJsonSettings.JsonCompact.WithEnumAsNumbers()`).
- **Per member:** `[JsonProperty(EnumFormat = JsonEnumFormat.Number)]`.
- **System.Text.Json consumers of your payloads:** STJ's default reader rejects enum *names*. Either add
  `new JsonStringEnumConverter()` to their `JsonSerializerOptions`, or keep those endpoints on `WithEnumAsNumbers()`.
- **Frozen `DataContractJsonSerializer` clients:** DCJS emits numbers and ignores `[EnumMember]` in JSON, so endpoints
  that must stay byte-compatible with a DCJS producer use `WithEnumAsNumbers()`.
- **Stored documents** written before 7.4.3 (numeric) read back unchanged; new writes carry names. If byte-stable
  output matters (hashing, dedup), pin the settings explicitly on that path.

⚠️ The raw flag `CrystalJsonSettings.OptionFlags.EnumsAsString` is retired with an **error-level** `[Obsolete]`: the bit
is now `EnumsAsNumbers`, with inverted meaning. The `EnumsAsString` *property* and the `WithEnumAsStrings()` /
`WithEnumAsNumbers()` methods keep their names and meanings - only the default flipped.

**Migrating a legacy `[DataContract]` DTO** (e.g. from `DataContractJsonSerializer`): the reflection path already
reproduces its member selection and `Name=` renames, so an un-touched DTO keeps its output - **but only on that path**
(a generated container applies the same model, so enrolling one is supported and produces the same format). Useful equivalences:

- `EmitDefaultValue = false` -> `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]`
- a non-public `[DataMember]` -> nothing *(7.4.3+)*: on a `[DataContract]` type it serializes and binds
  automatically on the reflection path (the interim `[JsonInclude]` opt-in keeps working, now redundant)
- `[CollectionDataContract(ItemName = "entry", KeyName = "code", ValueName = "label")]` -> **nothing to do**: those
  names exist in the XML format only. DCJS itself writes a `List<string>` subclass as `["x","y"]` and a
  `Dictionary<string,string>` subclass as `[{"Key":"c1","Value":"L1"}]`, with the literal `Key`/`Value` and the
  configured names nowhere in the output. The attribute's only JSON-visible meaning is "this type is a collection",
  which section 10 already covers for subclasses of `List<T>` / `Collection<T>` / `Dictionary<K,V>`: they bind back
  as themselves. Do not go looking for a naming knob to port.
- `[KnownType]` -> `[JsonPolymorphic(TypeDiscriminatorPropertyName = "__type")]` + `[JsonDerivedType(typeof(X), "Name:Ns")]`
- `Order` -> nothing (member order is not part of the contract); `IsRequired = true` -> nothing *(7.4.3+)*: the
  reflection path enforces it on read with DCJS semantics (absent throws, explicit null satisfies). For a
  stricter contract use C# `required` (null-or-missing throws, both paths).
- Never put `[DataMember]` (or `[JsonInclude]`, or a `[JsonProperty]`-style rename) and an unconditional
  `[JsonIgnore]` on the same member: the combination is **refused loudly** on both paths *(7.4.3+)* - the
  reflection path throws when the type's contract is built, and the generator fails the build with error
  `CJSON0008`. This is the dual-output DTO (one format carried the member, the other did not), which is not
  supported: split the type, one DTO per serializer; if the pair is a slip, remove one of the two attributes.
  Do NOT resolve it by adding a `Condition` - that flips the member to included-with-a-write-rule and ships it
  onto the second format. A conditional `[JsonIgnore(Condition = ...)]` next to `[DataMember]` stays legal (it is
  the `EmitDefaultValue` recipe).

**Settings that restore legacy-reader compatibility** (the recipe column, per construct):

| Legacy expectation | Setting |
|---|---|
| `"member": null` emitted rather than omitted | `WithNullMembers()` |
| `"\/Date(ms)\/"` dates emitted | `WithMicrosoftDates()` |
| numeric enums | `WithEnumAsNumbers()` |
| `[{"Key":k,"Value":v}]` dictionaries emitted | `WithDictionariesAsPairArrays()` |
| `"P1DT2H3M4.005S"` ISO 8601 duration strings for `TimeSpan` *(7.4.3+; default is a number of seconds)* | `WithIso8601Durations()` |

**`CrystalJsonSettings.DataContractCompat`** *(7.4.3+)* is the named, cached preset composing all five: an
endpoint using it reproduces the COMPLETE `DataContractJsonSerializer` format with one settings reference.
Scope it per endpoint (`WithNullMembers()` is pure verbosity for any consumer that is not a frozen legacy
reader); reading never needs it, every legacy shape is accepted on read by default.

**For teams coming from DCJS: the container-level profile** *(7.4.3+)*. A source-generated container can bake
the preset as its default format: `[CrystalJsonConverter(CrystalJsonSerializerDefaults.DataContractCompat)]`.
Its generated entry points then emit the legacy format when the caller passes no settings; explicitly passed
settings always replace the profile ENTIRELY (no merging). The profile governs value formats only and is
independent of membership: it serves **plain DTOs** (the DCJS POCO opt-out rules) and **`[DataContract]`
types** (the DataContract model) alike, exactly as an unprofiled container does. For the types it serves, **a DataContractCompat
container produces what DCJS would have produced, with the documented differences** - member order
(declaration order here; DCJS wrote POCO members alphabetically and honored `Order=`), no `\/` escaping
outside `\/Date()\/` literals, `-0.0` serialized as `0`, and the dual-output /
double-contract shapes DCJS resolved silently are refused loudly. Combining the profile with a camelCase (or
other) naming policy is a build error (`CJSON0013`): the DCJS format has no naming policy. `PropertyNameCaseInsensitive`
next to the profile is NOT refused - it only changes how an incoming member name is matched when reading JSON,
and the profile still writes the declared names untouched: strict on output, lenient on input. **The dual-container pattern is the intended
shape for a progressive WCF portage**: not-yet-ported services serialize through the compat container,
modernized services through a default or `Web` container over the SAME types; when the portage completes,
delete the legacy container.

⚠️ The one genuine hazard when a **frozen** legacy client reads your output: a **null `[DataMember(IsRequired = true)]`
member**. CrystalJson omits nulls by default and the legacy reader **throws** on the missing member. Put
`WithNullMembers()` on those endpoints.

### One type serialized by BOTH CrystalJson and System.Text.Json

Climb this ladder, stop at the first rung that holds:

1. **The attributes coexist** - each serializer ignores the other's - so keep **one type**. `[JsonConvertWith]`,
   `[JsonBooleanLiterals]`, `[JsonProperty]` and `[EnumMember]` are all invisible to STJ. The two outputs may *differ*
   per serializer; that is fine.
2. **A conflict a dual-shape converter resolves** - keep **one type** and write ONE converter class valid for both:
   derive STJ's `JsonConverter<T>` *and* implement `IJsonMemberConverter<T>`. Recognition is structural, each
   serializer uses its own facet.
3. **The attributes genuinely conflict** - **duplicate the type**, one DTO per serializer; never contort one type to
   serve both. The canonical case: the STJ-spelled `[JsonConverter(typeof(X))]` where `X` is a CrystalJson-only
   converter. CrystalJson tolerates it, but **STJ does not reciprocate** - it inspects that attribute, sees a type that
   does not derive its own `JsonConverter`, and throws `InvalidOperationException` while building the type's metadata.

That asymmetry is why `[JsonConvertWith]` is the right attribute for a CrystalJson-only converter: STJ never looks at it.

**Legacy format tolerance (read side, always on, no setting needed):** `"\/Date(ms)\/"` and `"\/Date(ms+HHMM)\/"` dates
bind to `DateTime`/`DateTimeOffset` (the ms are always UTC; the suffix only carries the producer's offset), and the
`[{"Key":k,"Value":v}]` dictionary shape binds to `Dictionary<K,V>` *(7.4.3+, strict: every element must be an object
with exactly `Key` and `Value`)*. To **emit** either legacy shape, opt in with `WithMicrosoftDates()` /
`WithDictionariesAsPairArrays()` (section 4).

**Lifecycle:** deserialization runs the **parameterless constructor** (public or not), then assigns members; missing
and explicit-null fields are skipped, so ctor-initialised state survives.

The four `[OnSerializing]` / `[OnSerialized]` / `[OnDeserializing]` / `[OnDeserialized]` callbacks **are invoked**
*(7.4.3+)*, on both write routes (text and DOM) and on read, so a `DataContractJsonSerializer` estate keeps the
behaviour it had. **Only the modern signatures are accepted:**

| Signature | Accepted on |
|---|---|
| `void M()` | all four |
| `void M(JsonValue)` / `void M(JsonObject)` / `void M(JsonArray)` | `[OnDeserializing]` / `[OnDeserialized]`, receives the document being bound |
| `void M(StreamingContext)` | **refused**, both paths (see below) |

The document-taking form is a capability `DataContractJsonSerializer` never had, and neither does System.Text.Json:
their reader is forward-only with no document materialized, so by the time a callback runs there is nothing to hand
it. Prefer the parameterless form; take the document only when the callback genuinely needs to inspect the payload.

⚠️ **`void M(StreamingContext)` is refused**, which is a deliberate breaking change for ported DCJS code: the generator
reports **`CJSON0015`** at the callsite, and the reflection path throws the same message when it builds the type's
contract (once per type, never per call). The fix is mechanical, and it has one precondition: **DCJS *requires* that
parameter**, so converting a callback costs the type its DCJS compatibility. A type still serialized by DCJS anywhere
cannot be converted on its own. See the migration guide for the sweep recipe.

**Where they fire, exactly** (pinned by tests on both paths, not by assertion here):

| | |
|---|---|
| Write | `[OnSerializing]` strictly before the first member is written, `[OnSerialized]` strictly after the last, on the text route AND the DOM route |
| Read | `[OnDeserializing]` on a constructed but **unpopulated** instance, `[OnDeserialized]` on the fully populated one |
| Member converters | run **inside** the bracket, since they are part of writing/reading a member |
| **Proxies** | **no lifecycle at all.** A proxy is a lazy typed *view* over the DOM: nothing is constructed and nothing is populated, so there is no interval to bracket. The callbacks run at **materialisation** (`ToValue()`, `Deserialize`, `Unpack`) |

⚠️ That last row matters if you have adopted "stay on the proxy" as a principle: a callback will not run for
you until something calls `ToValue()`. If a type's invariants depend on a callback, read it as a value.

The strictness of the write bracket is the point for the one workload callbacks genuinely serve: a **latch**
that suppresses cache invalidation while two views of the same data are populated. Without an exact bracket the
second member to arrive clobbers the first, and which one that is depends on member order.

Note that lifecycle callbacks are a pattern to move away from rather than toward. A DTO should be a dumb type with
its preparation already done before it reaches the serializer; a callback bolts serialization onto a type that was
never designed for it, which is why such types end up needing cleanup halves and locks. The supported remedy is to
adapt the not-meant-for-serialization type into a plain DTO, at which point the callbacks are not needed at all.
The attribute support exists to make a large legacy estate portable, not to encourage the pattern. (This is also why
the four System.Text.Json callback *interfaces* - `IJsonOnSerializing` and friends - are deliberately **not**
honoured: adding a second, cleaner-looking spelling would advertise the pattern rather than retire it.)

Alternatively, take over the type entirely with `IJsonPackable` / `IJsonDeserializable<T>` (section 7) or a
`ctor(JsonValue[, ICrystalJsonTypeResolver])`.

---

## 10. Collections *(7.4.3+)*

One contract governs every collection: **you get an instance of the declared member type, or a
`JsonBindingException` / `JsonSerializationException` naming the type and the reason.** A wrong-shaped value is never
returned, because the compiled member setter uses a lenient cast that would turn it into a silent `null`.

So declare the collection type you actually want and let it bind:

```csharp
public sealed class StoreDto { public Collection<int>? Codes { get; set; } }
CrystalJson.Deserialize<StoreDto>("""{ "Codes": [ 1, 2, 3 ] }""").Codes   // Collection<int> { 1, 2, 3 }
```

**What binds.** Anything concrete implementing `ICollection<T>` with a public parameterless constructor is constructed
and filled through `Add()`, like a collection initializer - which covers `Collection<T>`,
`ObservableCollection<T>`, `KeyedCollection<K,V>`, `LinkedList<T>`, `SortedSet<T>`, the `Queue`/`Stack`/`Concurrent*`
family, and **your own subclasses** of any of them (`ProductList : List<Product>` receives a `ProductList`). Immutables,
dictionaries (including `ReadOnlyDictionary<K,V>` and subclasses), the non-generic legacy types (`Hashtable`,
`ArrayList`, `StringCollection`), and duck-typed `IEnumerable<T>` + public `Add(T)` types all round-trip too. Interface
members bind to a sensible concrete type (`IDictionary<K,V>` -> `Dictionary<K,V>`, `IReadOnlySet<T>` -> `HashSet<T>`).

Getter-only properties (`public Collection<int> Items { get; }`) are populated through the adder rather than assigned.
A `KeyedCollection<K,V>` rebuilds its by-key index as it fills (`GetKeyForItem` runs on every `Add`), and duplicate
keys in the document fail loudly instead of dropping an item.

**In generated code you will see `UnpackCollection`.** You do not call it yourself - it is what a generated converter
emits for a collection member whose element type is also generated, so the element decoding (and therefore the
container's naming policy, e.g. camelCase under the Web defaults) is applied by *your* generated element converter
rather than by the runtime fallback. Recognising the name in generated source or a stack trace is the point:

```csharp
// SnowBank.Data.Json, extension methods on IJsonDeserializer<T>
TCollection? UnpackCollection<TCollection, T>(this IJsonDeserializer<T> serializer, JsonValue? value, TCollection? defaultValue = null, ...)
TCollection  UnpackRequiredCollection<TCollection, T>(this IJsonDeserializer<T> serializer, JsonValue? value, ...)
```

`UnpackRequiredCollection` is the flavour behind a `required` member: absent or null throws rather than yielding the
default. Both construct the declared collection type and fill it with `Add`, which is why a subclass arrives as itself.

**Format and round-trip semantics** (pinned by tests):

```
Queue<T>          [ 1, 2, 3 ]   front first; round-trip preserves order
Stack<T>          [ 3, 2, 1 ]   top first; round-trip PRESERVES the order (legacy serializers reversed stacks)
ConcurrentBag<T>  [ .. ]        unordered by contract: round-trip preserves CONTENT, not order
Hashtable         { "a": 1 }    a JSON object; ALSO reads the legacy [ { "Key": "a", "Value": 1 } ] format
ArrayList         [ 1, "two" ]  elements bind as CLR objects
```

⚠️ **Refused, loudly and everywhere** - these have no correct behavior, so they throw rather than guess:

| Shape | Why, and what to write instead |
|---|---|
| `int[,]` and other multi-dimensional arrays | no JSON spelling; use jagged `T[][]` |
| `NameValueCollection` (serializing) | one key maps to many values; project it yourself |
| `ReadOnlyObservableCollection<T>` | no sane construction path; bind the inner collection |
| `StringDictionary` (deserializing) | serializes as a KVP array but cannot be rebuilt; use `Dictionary<string,string>` |

## 11. Golden rules & gotchas

✅ **DO**
- Use the **source generator** for domain POCOs; use the **DOM** for dynamic/schemaless JSON.
- Decide read-only vs mutable deliberately: build read-only for cached/shared values; `ToMutable()` before editing.
- Read with `Get<T>(key, default)` (optional) or `Get<T>(key)` / `Required<T>()` (required); chain indexers freely
  (missing hops yield `JsonNull.Missing`, not exceptions).
- Pass your generator's `GetResolver()` to APIs that serialize your types (so they can find the generated converters).
- Round-trip-test any manual `IJsonPackable`/`IJsonDeserializable<T>` implementation.

⚠️ **GOTCHAS**
- **Not System.Text.Json / Newtonsoft.** `JsonObject`/`JsonArray` here are `SnowBank.Data.Json`, and the *APIs* are not
  interchangeable with the other libraries. Several of their **attributes** are honored though - see section 9 for the
  exact per-path matrix, and mind that the two paths do not honor the same set.
- **Enums serialize as STRINGS by default since 7.4.3** (they were numbers), everywhere. Reading stays tolerant
  both ways, so old payloads are fine - but anything comparing bytes, or any frozen STJ/DCJS reader, sees the change.
  `WithEnumAsNumbers()` restores it (section 9).
- **Legacy `[DataContract]` types belong to the reflection path** (see section 9): it honors the `[DataMember]` opt-in
  and renames in full, and *(7.4.3+)* a generated container applies the same model, so the two paths agree on
  membership, names, non-public members and `IsRequired`.
- **`JsonNull.Missing` ≠ `JsonNull.Null` ≠ `JsonNull.Error`.** All are "null", but distinct; use `IsNullOrMissing()` /
  `IsMissing()` to tell them apart. Empty/whitespace input parses to `Missing`; literal `"null"` parses to `Null`.
- **Mutating a read-only `JsonObject`/`JsonArray` throws.** `ToMutable()` first, or build mutable.
- **`Equals` is loose, `StrictEquals` is exact.** `JsonNumber(123).Equals(JsonString("123"))` is `true`;
  `StrictEquals` is `false`. Don't use `JsonObject`/`JsonArray` as dictionary keys (hash is not value-stable).
- **`Add("field", x)` vs `["field"].Add(x)`** - field-set (throws on existing) vs array-append. Pick the right one.
- **Don't retain a `MutableJsonValue` child across a parent mutation** - it goes stale.
- **`Deserialize<T>` of `null` throws** for a non-nullable `T` unless you pass a `defaultValue`.
- **A custom converter is honored per FACET.** A type implementing only `IJsonPacker<T>` writes but does not read;
  using the missing direction throws (naming the facet to add) rather than silently falling back. Prefer
  `[JsonConvertWith]` over the STJ-spelled `[JsonConverter]`, which is *silently ignored* when it names a type
  CrystalJson cannot execute (section 7).
- **Multi-dimensional arrays are refused**, not flattened; `NameValueCollection` will not serialize (section 10).
- **`[JsonIgnore]` means "exclude"; `[JsonIgnore(Condition = ...)]` does NOT** - it is a per-member serialization rule
  (section 9). Reading is unaffected in both cases except for `Always`.
- Numbers/dates are **InvariantCulture**; dates default to ISO 8601.
