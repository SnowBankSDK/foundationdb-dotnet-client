# CrystalXml: generated XML output for CrystalJson

CrystalXml is a write-only XML output overlay for the CrystalJson source generator. A container
that already generates JSON serializers can opt in to XML with one attribute, and every type it
enrolls gains a family of `ToXmlText` / `WriteXmlTo` outputs generated at compile time: zero
runtime reflection, no `System.Xml.Serialization`, and byte-exact output on the text sinks.

It exists to let an application replace `DataContractSerializer`-based XML production (the
"DCS format") with generated code while keeping byte compatibility with the documents its
consumers (for example an XSLT rendering layer) already parse - and, independently, to give
modern JSON-first containers a clean XML projection.

There is deliberately no `FromXml`: CrystalXml writes XML, it never reads it.

## Declarative vocabulary

Two levels: the container says WHICH formats it produces, the members say how they look on the XML one.

A container is a format-neutral marker plus one attribute per output format. The types it serializes
are enrolled once, format-neutrally: the same enrollment feeds every format the container produces.

```csharp
// container level: the neutral marker, then one output attribute per format
[CrystalConverter]                                    // "this class hosts generated code"
[CrystalJsonOutput(CrystalJsonSerializerDefaults.DataContractCompat)]
[CrystalXmlOutput]                                    // opt-in: every type of the container gets XML output
[CrystalSerializable(typeof(ClientAccount))]          // format-neutral enrollment
public static partial class LegacyRenderSerializers { }
```

| Attribute | Namespace | Role |
|---|---|---|
| `[CrystalConverter]` | `SnowBank.Data` | the container marker; says nothing about the formats |
| `[CrystalSerializable(typeof(T))]` | `SnowBank.Data` | enrolls a root type; repeatable; feeds every output format |
| `[CrystalJsonOutput(...)]` | `SnowBank.Data.Json` | requests the JSON format, and carries its parameters (profile, naming policy, case-insensitivity) |
| `[CrystalXmlOutput(...)]` | `SnowBank.Data.Xml` | requests the XML format, and carries its parameters (`Profile`, `DictionaryFormat`) |
| `[CrystalJsonConverter(...)]` | `SnowBank.Data.Json` | mono-format alias: `[CrystalConverter]` + `[CrystalJsonOutput]` with the same parameters |
| `[CrystalXmlConverter(...)]` | `SnowBank.Data.Xml` | mono-format alias: `[CrystalConverter]` + `[CrystalXmlOutput]` with the same parameters |

`[CrystalJsonSerializable(typeof(T))]` is the former spelling of `[CrystalSerializable]`. It still works
(and generates byte-identical code) but is `[Obsolete]`: enrollment never was JSON-specific.

### The truth table

| Container attributes | Generated |
|---|---|
| `[CrystalConverter]` + `[CrystalJsonOutput]` | JSON only |
| `[CrystalJsonConverter]` | JSON only (alias of the row above) |
| `[CrystalConverter]` + `[CrystalXmlOutput]` | **XML only**: no `Serialize`/`Pack`/`Unpack`, no JSON proxies, no `IJsonConverter` facet, no `TypeMapper` |
| `[CrystalXmlConverter]` | XML only (alias of the row above) |
| `[CrystalConverter]` + both outputs | both formats, from one set of enrolled types |
| `[CrystalJsonConverter]` + `[CrystalXmlOutput]` | **refused** (CRYS0002): the mono-format aliases do not combine |
| `[CrystalXmlConverter]` + `[CrystalJsonOutput]` | **refused** (CRYS0002), symmetrically |
| `[CrystalConverter]` alone | **refused** (CRYS0001): a container that names no output format generates nothing |
| several container markers on one class | **refused** (CRYS0003) |

An XML-only container has no JSON profile to derive from, so an unspecified `Profile` resolves to the
modern one, and its element names are the declared member names (the naming policy is a `[CrystalJsonOutput]`
parameter). A container that needs both a JSON naming policy and its XML mirror declares both outputs.

`[CrystalXmlOutput]` / `[CrystalXmlConverter]` options:

| Option | Meaning |
|---|---|
| `Profile` | XML variant; derived from the container's JSON profile by default (`DataContractCompat` gives the DCS format, standard/Web gives the modern profile; no JSON output means the modern profile); explicit override allowed; an incoherent combination (a naming policy next to the DCS format) is a build error (CXML0001) |
| `DictionaryFormat` | container default for the dictionary shape (see the modern profile below) |
| `Schemaless` | DCS format only: reproduces the namespace-free stripped wire, byte for byte. On the modern profile the option is inert, and CXML0012 says so |

```csharp
// MEMBER level: everything XML lives in [XmlProperty] (namespace SnowBank.Data.Xml)
[XmlProperty("@id")]                     // sugar: normalized at build time to Name="id" + Attribute=true
[XmlProperty(ItemName = "tag")]          // wrapped collection form, entry naming for dictionaries
```

Per-setting resolution ladder (never all-or-nothing):

1. the container profile's defaults (compat or modern);
2. `[JsonProperty]` / `[JsonPropertyName]`: provide the name, taken verbatim (never re-shaped
   by the naming policy);
3. `[XmlProperty]`: final override, option by option (an `ItemName` alone leaves the name to
   fall back to step 2, then to the .NET member name through the naming policy).

`ItemName` is a purely XML concept: it never joins `[JsonProperty]`.

Absolute rule: no output form is ever chosen by a heuristic on the data. If the output varies,
an attribute or an option asked for it explicitly upstream. Every inexpressible case is a build
error (the CXML diagnostic range) or a typed runtime exception, never a silent fallback.

## Execution pipeline

```
   generated code (one body per type)
        |   WriteXml<TEmitter>(ref TEmitter emitter, T value)   where TEmitter : struct, ICrystalXmlEmitter
        v
  ICrystalXmlEmitter    -- event vocabulary: StartElement / Attribute / Text / EndElement / RawAscii
        |
        +-- CrystalXmlWriter<TRune, TWriter>       TEXT: the single char + byte implementation
        |     where TRune : unmanaged (char|byte)    byte-exact forms; always passed by ref
        |     where TWriter : struct, IBufferWriter<TRune>
        |
        +-- CrystalXDocumentEmitter                        infoset: builds the DOM directly
        +-- CrystalXmlWriterEmitter                        infoset: delegates to System.Xml (interop)
```

Element and attribute names are precomputed by the generator in dual representation (a string
plus a frozen UTF-8 literal) inside static `CrystalXmlName` fields, with the contract namespace
baked into the name on the DCS format, so the byte path never transcodes a name at run time. A
name never holds a prefix: the emitter assigns prefixes by what is in scope at its depth.
Non-public members go through the same `[UnsafeAccessor]` thunks as the JSON side. Polymorphism
is a generated switch over the graph's known derived types; a runtime type outside the graph
raises a typed exception.

Public outputs on the generated holder (none goes through another):

| Output | Real path |
|---|---|
| `ToXmlText(value)` | char core over `IBufferWriter<char>` |
| `WriteXmlTo(TextWriter, value)` | adapter to the char core |
| `ToXmlSlice(value)` / `ToXmlBytes(value)` | byte core (UTF-8, no intermediate string) |
| `WriteXmlTo(Stream / IBufferWriter<byte>, value)` | byte core |
| `ToXDocument(value)` / `WriteXmlTo(XmlWriter, value)` | infoset emitters - infoset-level guarantees only, never byte-exact |

Every output accepts an optional `rootName` and optional `CrystalJsonSettings` (defaults come
from the container profile; `ShowNullMembers`, date/duration/enum formats).

Mirror interfaces of the JSON side: `ICrystalXmlSerializer<T>` (the facet implemented by
generated holders; extension point for per-member custom converters, verified at generation
time), `ICrystalXmlElementSerializer<T>` (its composition extension: `WriteXmlElement` plus the
two names a caller composes with, implemented by every generated converter) and
`ICrystalXmlSerializable` (instance hook: the type writes its own XML).

### Collection and scalar roots

A bare collection or scalar cannot be enrolled (CJSON0019 refuses it: enroll the element type,
not the collection). Those documents go through entry points on `CrystalXml` instead, mirroring
the eight outputs above:

```csharp
// a sequence of contract items, composed out of the item type's facet
string xml = CrystalXml.ToText(LegacySerializers.Shelf.Default, shelves);
// <ArrayOfShelf xmlns="..."><Shelf>...</Shelf><Shelf>...</Shelf></ArrayOfShelf>

// a bare scalar root, on the nested Scalar class
string xml = CrystalXml.Scalar.ToText("hello");
// <string xmlns="http://schemas.microsoft.com/2003/10/Serialization/">hello</string>
```

The root name is resolved, never guessed: the caller's `rootName` wins; the DCS format falls back
to its `ArrayOfX` convention, in the item contract's namespace; the modern profile has no
convention, so a collection root without a `rootName` raises `CrystalXmlRootNameException`. The
item elements keep the item type's own element name, and `itemName` overrides it. The scalar
entry points write the reference wire of the xsd lexical types (the lexical name in the
Serialization namespace, nil when null); a type outside that set raises
`CrystalXmlUnknownTypeException`. Scalars live on the nested `CrystalXml.Scalar` class rather
than as overloads: a generic method taking a bare `T?` would capture every call the serializer
overloads do not, and a mistyped argument must fail to compile rather than fail at write time.

## The compat profile: the DCS format

The executable spec is a suite against a live `DataContractSerializer` oracle
(`SnowBank.Core.Tests/Xml/DcsWireFidelityFacts.cs`, with the namespace rules pinned in
`DcsNamespaceReferenceFacts.cs`; coverage ledger next to them in `COVERAGE.md`), under two
acceptance rules. The default output is held to the standard wire on expanded names: this emission
omits the declarations it can prove unused and writes the rest on the first element that needs
them, so its bytes differ from the reference serializer's while every element and attribute
resolves to the same (namespace, local name) pair. The `Schemaless = true` output is held to the
stripped wire byte for byte. Highlights:

- Root and contract names: `[DataContract(Name=)]` honored, generics compose `XOfY` with
  `{0}`/`{#}` expansion (namespace digest deliberately omitted), nested types `Outer.Inner`,
  `XmlConvert.EncodeLocalName` applied.
- Member order: base class first (recursive), members without `Order=` in ordinal-alphabetical
  order of the output name, then `Order=` groups ascending with alphabetical ties.
- Read-only members: a get-only property, or a property with a private setter and no opt-in,
  never reaches the output - matching what the reference serializer's reflection path takes on a
  plain POCO. On a `[DataContract]` type that same shape carrying `[DataMember]` is refused at
  generation time instead (CXML0013): the reference serializer's no-set-method check rejects it
  outright (`InvalidDataContractException`, "No set method for property"), so there is no format to
  match either way. A `readonly` `[DataMember]` **field** is a different shape - that check is
  property-only - and does reach the output, byte for byte with the live oracle.
- Null members: `<X nil="true" />` by default; `[DataMember(EmitDefaultValue = false)]` makes the
  member absent when at its CLR default.
- Collections: the item element is named after the item type's contract name (`<string>`,
  `<int>`, `<dateTime>`, `<Shelf>`, `<ArrayOfstring>` for a nested list); empty collection
  self-closes, empty string keeps a start+end tag pair.
- Dictionaries: `<KeyValueOfstringstring><Key>..</Key><Value>..</Value></KeyValueOfstringstring>`,
  and `<KeyValueOfstringShelf>` when the value is a contract type.
- Namespaces: the root element's contract namespace (`[DataContract(Namespace = ...)]`, else
  `http://schemas.datacontract.org/2004/07/` plus the CLR namespace) is its default namespace,
  and a member element lives in the namespace of the contract that declares it. Five built-in
  namespaces cover what no CLR namespace derives: XMLSchema-instance (the `i:nil` and `i:type`
  attributes), XMLSchema (the QName of a boxed primitive's `i:type`), Arrays (unannotated generic
  collections and dictionaries), Serialization (bare scalar roots), and the System contract
  (`DateTimeOffset`). A name holds the local name and the namespace, never a prefix: the writer
  assigns prefixes, keeps declarations in scope, and declares each namespace on the first element
  that needs it. The instance namespace hoists to the root when two or more subtrees can carry a
  nil or type marker.
- Polymorphism: `i:type="<contract QName>"` attribute only when the runtime contract differs from
  the declared one; the element name stays that of the declared type, in the declaring contract's
  namespace. A derived type in the slot's own namespace writes a bare local name; one in another
  namespace writes a prefixed QName and declares the prefix on the same element. An instance of a
  concrete polymorphic root writes its own body, unannotated - which is what the oracle does, and
  where this profile deliberately parts ways with the modern one (see below).
- `ISerializable` dialect: each `SerializationInfo` entry becomes an element named after the
  (encoded) key, values declared `object` carry a `type=` discriminator.
- Scalars: DCS lexical forms (ISO dates truncated per `DateTimeKind`, ISO 8601 durations,
  `char` as its code point, `decimal` keeping scale, round-trip doubles, enums by
  `[EnumMember(Value=)]` or name via a generated switch, `DateTimeOffset` as the two-element
  `{DateTime, OffsetMinutes}` structure, `byte[]` as base64).
- Text: no XML declaration, self-closing `<X />` with a space, text line endings as raw CRLF.
- `Schemaless = true`: the namespaces, prefixes and declarations disappear (`i:nil` arrives as
  `nil`, `i:type` as `type`, the discriminator keeps only its local name). This is the historical
  stripped wire some consumers store and parse, kept as an explicit, byte-certified option.

Three deliberate deviations from raw DCS, each pinned by a dedicated test, are requirements:

1. Dictionary entry names carry no namespace-hash digest (`KeyValueOfstringShelf`, not
   `KeyValueOfstringShelfQU_P9Vt29`). Measured: zero consumers of the digest.
2. Control characters are sanitized at the value level (raw DCS emits `&#x1;`, a document a
   conformant parser rejects). A strict reproduction mode exists for certification harnesses.
   **Text sinks only**: this filter lives in `CrystalXmlWriter`, which is what produces the output.
   The infoset emitters (`CrystalXDocumentEmitter`, `CrystalXmlWriterEmitter`) apply none of it - the DOM sees
   the characters verbatim, and `XmlWriter` answers for them under its own `CheckCharacters`.
3. Typed exceptions (`CrystalXmlCycleException`, `CrystalXmlUnknownTypeException`,
   `CrystalXmlRootNameException`, `NotSupportedException`, `XmlException`) replace
   `SerializationException`.

## The modern profile: the XML a JSON reader would predict

| JSON | Modern XML |
|---|---|
| `{"title": "x"}` | `<title>x</title>` - same naming ladder as the JSON |
| root | the type name through the same ladder; optional `rootName` per call |
| null member | absent (like JSON by default); `WithNullMembers()` gives `<x nil="true" />`; per-member `[JsonIgnore(Condition = ...)]` honored |
| `"tags": ["a","b"]` | unwrapped by default: `<tags>a</tags><tags>b</tags>`; `[XmlProperty(ItemName = "tag")]` wraps: `<tags><tag>a</tag>...</tags>`; a bare nested collection (`List<List<T>>`) is a build error (CXML0006) - introduce an intermediate type |
| dictionary | `CrystalXmlDictionaryFormat { Default, Direct, KeyAttribute, KeyValueAttributes, KeyValueElements }`; modern default is `Direct` (`<scores><math>12</math></scores>`, non-NCName key = typed runtime exception) |
| `"$type": "cat"` | `type="cat"` attribute - the discriminator is an annotation |
| `[XmlProperty("@id")]` | `<book id="42">` - data as an attribute, scalars only; forbidden on the compat profile (DCS has no user attributes) |

An instance of a concrete polymorphic root is refused here with
`CrystalXmlUnknownTypeException`, where the compat profile writes the root's own body. This format
matches the JSON side, which carries no discriminator for that value either: a reader could not
tell it from a subtype whose annotation went missing, so it is refused rather than written under
a shape nobody can interpret.

Unlike the compat profile, the modern format carries no read-only restriction at all: a get-only
property, a `readonly` field, and an init-only member are all emitted, mirroring the JSON format
(which never filters by read-only-ness either - only the generated deserializer skips assigning
one back).

## Example

```csharp
[CrystalConverter]
[CrystalJsonOutput(CrystalJsonSerializerDefaults.Web)]      // camelCase
[CrystalXmlOutput]                                          // derived Profile: modern
[CrystalSerializable(typeof(Book))]
public static partial class AcmeSerializers { }

public sealed record Book
{
	[XmlProperty("@id")]
	public required int Id { get; init; }

	public required string Title { get; init; }

	[XmlProperty(ItemName = "tag")]
	public List<string> Tags { get; init; } = [];

	public Dictionary<string, int> Scores { get; init; } = [];

	public string? Subtitle { get; init; }
}

var book = new Book { Id = 42, Title = "Dune", Tags = ["sf", "space"], Scores = { ["math"] = 12 } };
string xml = AcmeSerializers.Book.ToXmlText(book);
// <book id="42"><title>Dune</title><tags><tag>sf</tag><tag>space</tag></tags><scores><math>12</math></scores></book>
```

A type present in two containers has two serializers, each with its profile's format. Generic code
takes the facet: `void Export<T>(ICrystalXmlSerializer<T> serializer, T value, IBufferWriter<byte> output)`.

## Diagnostics and runtime guards

Three ways a construct is refused, and which one applies is a rule, not a case-by-case choice:

| Mechanism | When |
|---|---|
| **CXML diagnostic** | the construct is refused at generation time, decidable from the DECLARATIONS alone (an attribute, a type, a contract name). It points at the offending declaration and carries a remedy. One member of the range, CXML0012, is an Info instead: it does not refuse anything, it names a setting the resolved format never consults. |
| **`#error` in the emitted source** | a structural impossibility discovered inside emission, which no declaration could have predicted. Also kept as an unreachable backstop under a diagnostic that already covers the case. |
| **typed exception** | the decision is data-dependent: only the value being written can make it (a runtime type outside the graph, a non-NCName dictionary key, an undeclared enum value, a graph deeper than the cap, a collection root that neither the caller nor the profile names). |

The rules about the container as a whole - which output formats it names, and whether its markers
combine - are not about either format, so they carry a neutral id instead:

| Id | Refuses |
|---|---|
| CRYS0001 | `[CrystalConverter]` naming no output format: the container would generate nothing |
| CRYS0002 | a mono-format alias (`[CrystalJsonConverter]`, `[CrystalXmlConverter]`) next to an output attribute: the alias IS the format choice |
| CRYS0003 | several container markers on one class |

Build-time diagnostics about the XML format itself live in the CXML range:

| Id | Refuses |
|---|---|
| CXML0001 | profile/policy incoherence on the container: a naming policy (camelCase and friends) next to the DataContract XML format, whose element names come from the data contract. `PropertyNameCaseInsensitive` is NOT a trigger: it decides how an incoming name is matched when reading JSON, and this overlay never reads |
| CXML0002 | enrollment shape: `[CrystalXmlOutput]` on a class that hosts no generated serializer |
| CXML0003 | attribute projection of a member with no lexical form |
| CXML0004 | the XML naming vocabulary on the compat profile |
| CXML0005 | two members resolving to the same XML name, discriminator included |
| CXML0006 | a bare nested collection on the modern profile |
| CXML0007 | any name that is not a legal NCName: a declared `[XmlProperty]` name or `ItemName`, a bare `@`, the `"@x"` + `Attribute = false` contradiction, a member's name DERIVED from its JSON name, and a `[DataContract(Name = ...)]` that would name the root element. Modern profile only for the derived and root cases: the compat format encodes every name through `XmlConvert.EncodeLocalName` |
| CXML0008 | a member converter without the XML facet |
| CXML0009 | an attribute-projected member with a custom converter |
| CXML0010 | `[CollectionDataContract]` on a compat member's type |
| CXML0011 | a dictionary whose resolved shape carries the value as text (`KeyAttribute`, `KeyValueAttributes`) while the value type has no lexical form |
| CXML0012 | **Info, not an error** - a setting that was written explicitly, resolved, and then never consulted: an `[XmlProperty(ItemName = ...)]` on a member with no items, on a member whose RESOLVED dictionary shape is `Direct` (whose entries are named after their own key), or on a member whose type writes its own XML content (`ICrystalXmlSerializable`, which also makes a member-level `DictionaryFormat` inert - only the element NAME still comes from the member there); a `[JsonIgnore(Condition = Never)]` on an attribute-projected member (an attribute has no nil form, so a null one is absent either way); a `[CrystalXmlOutput(DictionaryFormat = ...)]` on a container whose resolved profile is the compat one (which has a single dictionary shape); and a `[CrystalXmlOutput(Schemaless = true)]` on a container whose resolved profile is the modern one (the stripped wire is a variant of the DCS format) |
| CXML0013 | compat profile only - a read-only (get-only, or non-public-setter with no opt-in) PROPERTY carrying `[DataMember]` on a `[DataContract]` type: the reference serializer rejects that contract outright (`InvalidDataContractException`, "No set method for property"), so there is no format to reproduce. Does not fire on a `readonly` `[DataMember]` FIELD (DCS's check is property-only) or on an init-only member (a different flag; DCS emits it) |

At run time, graphs deeper than `CrystalXml.MaxDepth` (64 levels of generated recursion, the
System.Text.Json default) raise
`CrystalXmlCycleException` - the guard cannot distinguish a genuine cycle from a legitimately
deeper acyclic graph, and its message says so. The depth counter cannot cross a call into
`ICrystalXmlSerializer<T>.WriteXml` or `ICrystalXmlSerializable.WriteXml`: a cycle running
entirely through such hooks is not covered by the guard.

The generated JSON formats share the same cap, `CrystalJsonWriter.MaxDepth` (`CrystalXml.MaxDepth`
is an alias for it). On the generated `Pack` path the guards travel inside a
`CrystalJsonPackContext` that `IJsonPacker<T>.Pack` takes by ref, so they survive the
collection/dictionary helpers (`PackObject`/`PackArray`/`PackList`/`PackEnumerable` in
`JsonSerializerExtensions`) and custom member converters alike: a cycle running through a
`List<T>` or `Dictionary<TKey, TValue>` member raises the recursion error there too.

Serialization lifecycle callbacks (`[OnSerializing]` / `[OnSerialized]`) **are** invoked on the
XML path, in the same place and through the same generated call as on the JSON path: the two
formats are two renderings of one serialization, so a callback that prepares the members runs
for both, once per write. `OnSerializing` runs after the element is opened but before anything
reads the value (attribute-projected members included), so its mutations are what the document
carries; `OnSerialized` runs just before the element is closed. On the compat profile's
`ISerializable` dialect the pair brackets the `GetObjectData` call, which is where the reference
serializer fires them too. There is no `OnDeserializing` / `OnDeserialized` counterpart, since
CrystalXml never reads.

## Consumer requirements

**Enabling XML output costs a container nothing that JSON output did not already cost.** The
generator as a whole requires the consumer project to compile at **`LangVersion` 9 or later**
(below that it refuses with `SYSLIB1221`, the same diagnostic and floor as the System.Text.Json
generator, and emits nothing at all, JSON included). The emitted XML code stays inside that
floor: the cached element and attribute names are spelled as `byte[]` array literals rather than
as `"..."u8` UTF-8 string literals, which would have raised the bar to C# 11 for XML containers
only. An old-style project (.NET Framework defaults to C# 7.3) therefore has exactly one thing to
do, and it is the same thing a JSON-only container asks of it: set `LangVersion` to 9 or later.

**The lite (`netstandard2.0` / `net472`) path is supported.** The CrystalXml runtime builds for
`netstandard2.0`, and the generated XML code compiles and runs on the .NET Framework CLR. Two
parts of a generated container are conditional there, and neither is XML:

- the JSON `ReadOnly` / `Writable` **proxies** are not emitted, because their interfaces need
  static abstract interface members, which the netfx CLR cannot support (they are equally absent
  below C# 11). The converters, the `TypeMapper` and the whole XML surface are emitted normally;
- the `[DynamicallyAccessedMembers]` trimming annotations are dropped when the attribute is not
  visible to the consumer, which only matters to a trimming/AOT publish the lite path does not do.

The XML certification suite runs on `net472` as well as on modern .NET, including the fixtures
that compare the emitter's output to a **live `DataContractSerializer`**. Those fixtures pass byte
for byte on both, so the netfx and modern DCS formats agree on every family the suite covers.
