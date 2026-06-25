---
name: crystaljson
description: >-
  How to use CrystalJson, the custom JSON library in SnowBank.Core (namespace SnowBank.Data.Json). Covers the JsonValue
  DOM (JsonObject / JsonArray / JsonString / JsonNumber / JsonBoolean / JsonNull / JsonDateTime), the read-only vs mutable
  model, the CrystalJson static API (Serialize / Parse / Deserialize) and CrystalJsonSettings, the Roslyn source generator
  for fast reflection-free serializers and read-only/writable proxies ([CrystalJsonConverter] / [CrystalJsonSerializable]),
  the IJsonSerializable / IJsonPackable / IJsonDeserializable interfaces, MutableJsonValue / ObservableJsonValue and
  JsonPath. Use whenever code parses, builds, reads, mutates, or serializes JSON with these types, declares a generated
  JSON converter/proxy, or implements custom JSON (de)serialization. CrystalJson is NOT System.Text.Json or Newtonsoft.
---

# CrystalJson (SnowBank.Data.Json)

CrystalJson is a high-performance, allocation-conscious JSON stack. It is **not** `System.Text.Json` or Newtonsoft -
the type names look familiar (`JsonObject`, `JsonArray`, ...) but the API is different. The namespace is
`SnowBank.Data.Json`. Add `using SnowBank.Data.Json;`.

There are **two layers**, used together:

1. **The DOM** - `JsonValue` and its subtypes. A mutable-or-immutable tree you build, parse, navigate, and serialize.
   Use it for schemaless / dynamic JSON (config, arbitrary documents, change records).
2. **The source generator** - `[CrystalJsonConverter]` + `[CrystalJsonSerializable(typeof(T))]` generate fast,
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

// PARSE text/bytes -> DOM (JsonValue)
JsonValue  any = CrystalJson.Parse(json);                              // string, Slice, ReadOnlySpan<char/byte>
JsonObject o   = CrystalJson.Parse(json).AsObject();                   // or .ParseObject(...)
JsonArray  a   = CrystalJson.ParseArray(json);
// Parse a READ-ONLY DOM (cache-safe) by passing read-only settings:
JsonValue roDom = CrystalJson.Parse(json, CrystalJsonSettings.JsonReadOnly);

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

### CrystalJsonSettings

Settings are **immutable and cached**; start from a preset and compose with fluent methods.

Presets: `CrystalJsonSettings.Json` (default), `.JsonCompact`, `.JsonIndented`, `.JsonReadOnly` (parse a read-only DOM),
`.JsonStrict`, `.JsonIgnoreCase` (case-insensitive field matching), and `JavaScript*` variants.

Common fluent options (chainable, e.g. `CrystalJsonSettings.Json.Compacted().CamelCased()`):

- Layout: `.Compacted()`, `.Indented()`, `.Formatted()`
- Naming: `.CamelCased()`, `.PascalCased()`
- Nulls/defaults: `.WithoutNullMembers()` (default), `.WithNullMembers()`, `.WithoutDefaultValues()`
- Enums: `.WithEnumAsStrings()` (default is numbers), `.WithEnumAsNumbers()`
- Dates: `.WithIso8601Dates()` (default), `.WithMicrosoftDates()`
- Read-only result: `.AsReadOnly()`

---

## 5. The source generator (your domain types)

For POCOs, prefer the generator over the DOM: it emits a fast, reflection-free, AOT-friendly converter **and** typed
read-only/writable proxies. (This is how DocStore documents work.)

### Declare

Put `[CrystalJsonSerializable(typeof(T))]` (one per root type) on a `public static partial class` marked
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
[CrystalJsonSerializable(typeof(Book))]
public static partial class MyJson { }       // generated members land here
```

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

(If consuming via NuGet, the analyzer ships with `SnowBank.Core` / its codegen package.) Symptom of a missing
`LangVersion`: `SYSLIB1221` ("language version not supported by the source generator").

### Use

```csharp
// POCO <-> JSON text
string json = MyJson.Book.ToJsonText(book);
Book   back = MyJson.Book.Deserialize(json);

// POCO <-> JsonValue (DOM)
JsonValue packed = MyJson.Book.Pack(book);
Book      from   = MyJson.Book.Unpack(jsonObject);

// runtime resolver (pass to CrystalJson APIs / DocStore / Teleport so they can resolve your converters)
ICrystalJsonTypeResolver resolver = MyJson.GetResolver();
```

### Read-only / writable proxies (zero-copy typed views over the DOM)

```csharp
MyJson.Book.ReadOnly ro = MyJson.Book.ToReadOnly(book);   // typed read-only view over a JsonValue
string title = ro.Title;                                  // typed property read
JsonValue dom = ro.ToJsonValue();                         // underlying (read-only) JsonValue
Book poco = ro.ToValue();                                 // materialize the POCO

// edit via copy-on-write: the original proxy is unchanged, you get a new frozen proxy
MyJson.Book.ReadOnly edited = ro.With(m => { m.Year = 2025; });

// or an explicit mutable proxy
MyJson.Book.Writable w = ro.ToMutable();
w.Year = 2025;
```

---

## 6. Mutating JSON: MutableJsonValue (and ObservableJsonValue)

`MutableJsonValue` is a mutation proxy used inside "write" closures (DocStore updates, Teleport `doc.Write(root => ...)`).
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

`JsonPack` (to DOM) and `JsonDeserialize` (from DOM) must be inverses - round-trip them in a test. Build values with
`JsonString.Return(...)`, `JsonNumber.Return(...)`, `JsonArray.ReadOnly.Create(...)`. Handle null/missing defensively in
`JsonDeserialize`. (Example in the wild: a compact id type packed as a `JsonArray` of its parts.)

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

## 9. Golden rules & gotchas

✅ **DO**
- Use the **source generator** for domain POCOs; use the **DOM** for dynamic/schemaless JSON.
- Decide read-only vs mutable deliberately: build read-only for cached/shared values; `ToMutable()` before editing.
- Read with `Get<T>(key, default)` (optional) or `Get<T>(key)` / `Required<T>()` (required); chain indexers freely
  (missing hops yield `JsonNull.Missing`, not exceptions).
- Pass your generator's `GetResolver()` to APIs that serialize your types (so they can find the generated converters).
- Round-trip-test any manual `IJsonPackable`/`IJsonDeserializable<T>` implementation.

⚠️ **GOTCHAS**
- **Not System.Text.Json / Newtonsoft.** `JsonObject`/`JsonArray` here are `SnowBank.Data.Json`. Don't mix attributes or
  APIs from the other libraries (though `[JsonProperty]`, `[Key]`, and some `System.Text.Json` polymorphism attributes
  are recognized by the generator).
- **`JsonNull.Missing` ≠ `JsonNull.Null` ≠ `JsonNull.Error`.** All are "null", but distinct; use `IsNullOrMissing()` /
  `IsMissing()` to tell them apart. Empty/whitespace input parses to `Missing`; literal `"null"` parses to `Null`.
- **Mutating a read-only `JsonObject`/`JsonArray` throws.** `ToMutable()` first, or build mutable.
- **`Equals` is loose, `StrictEquals` is exact.** `JsonNumber(123).Equals(JsonString("123"))` is `true`;
  `StrictEquals` is `false`. Don't use `JsonObject`/`JsonArray` as dictionary keys (hash is not value-stable).
- **`Add("field", x)` vs `["field"].Add(x)`** - field-set (throws on existing) vs array-append. Pick the right one.
- **Don't retain a `MutableJsonValue` child across a parent mutation** - it goes stale.
- **`Deserialize<T>` of `null` throws** for a non-nullable `T` unless you pass a `defaultValue`.
- Numbers/dates are **InvariantCulture**; dates default to ISO 8601.
