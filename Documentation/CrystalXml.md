# CrystalXml: generated XML output for CrystalJson

CrystalXml is a write-only XML output overlay for the CrystalJson source generator. A container
that already generates JSON serializers can opt in to XML with one attribute, and every type it
enrolls gains a family of `ToXmlText` / `WriteXmlTo` outputs generated at compile time: zero
runtime reflection, no `System.Xml.Serialization`, and byte-exact output on the text sinks.

It exists to let an application replace `DataContractSerializer`-based XML production (the
"DCS wire") with generated code while keeping byte compatibility with the documents its
consumers (for example an XSLT rendering layer) already parse - and, independently, to give
modern JSON-first containers a clean XML projection.

There is deliberately no `FromXml`: CrystalXml writes XML, it never reads it.

## Declarative vocabulary

Three attributes, two levels. The existing JSON vocabulary is not modified.

```csharp
// CONTAINER level: one marker attribute per output format
[CrystalJsonConverter(CrystalJsonSerializerDefaults.DataContractCompat)]
[CrystalXmlOutput]                       // opt-in: every type of the container gets XML output
[CrystalJsonSerializable(typeof(ClientAccount))]
public static partial class LegacyRenderSerializers { }
```

`[CrystalXmlOutput]` options:

| Option | Meaning |
|---|---|
| `Profile` | XML variant; derived from the container's JSON profile by default (`DataContractCompat` gives the DCS wire, standard/Web gives the modern profile); explicit override allowed; an incoherent combination (a naming policy next to the DCS wire) is a build error (CXML0001) |
| `DictionaryFormat` | container default for the dictionary shape (see the modern profile below) |

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
        |   WriteXml<TEmitter>(ref TEmitter emitter, T value)   where TEmitter : struct, IXmlEmitter
        v
  IXmlEmitter    -- event vocabulary: StartElement / Attribute / Text / EndElement / RawAscii
        |
        +-- CrystalXmlWriter<TRune, TWriter>       TEXT: the single char + byte implementation
        |     where TRune : unmanaged (char|byte)    byte-exact forms; always passed by ref
        |     where TWriter : struct, IBufferWriter<TRune>
        |
        +-- XDocumentEmitter                        INFOSET: builds the DOM directly
        +-- XmlWriterEmitter                        INFOSET: delegates to System.Xml (interop)
```

Element and attribute names are precomputed by the generator in dual representation (a string
plus a frozen UTF-8 literal) inside static `XmlName` fields, so the byte path never transcodes a
name at run time. Non-public members go through the same `[UnsafeAccessor]` thunks as the JSON
side. Polymorphism is a generated switch over the graph's known derived types; a runtime type
outside the graph raises a typed exception.

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
time) and `ICrystalXmlSerializable` (instance hook: the type writes its own XML).

## The compat profile: the DCS wire

The executable spec is a 28-family byte-equality suite against a live `DataContractSerializer`
oracle (`SnowBank.Core.Tests/Xml/DcsWireFidelityFacts.cs`; coverage ledger next to it in
`COVERAGE.md`). Highlights:

- Root and contract names: `[DataContract(Name=)]` honored, generics compose `XOfY` with
  `{0}`/`{#}` expansion (namespace digest deliberately omitted), nested types `Outer.Inner`,
  `XmlConvert.EncodeLocalName` applied.
- Member order: base class first (recursive), members without `Order=` in ordinal-alphabetical
  order of the wire name, then `Order=` groups ascending with alphabetical ties.
- Null members: `<X nil="true" />` by default; `[DataMember(EmitDefaultValue = false)]` makes the
  member absent when at its CLR default.
- Collections: the item element is named after the item type's contract name (`<string>`,
  `<int>`, `<dateTime>`, `<Shelf>`, `<ArrayOfstring>` for a nested list); empty collection
  self-closes, empty string keeps a start+end tag pair.
- Dictionaries: `<KeyValueOfstringstring><Key>..</Key><Value>..</Value></KeyValueOfstringstring>`,
  and `<KeyValueOfstringShelf>` when the value is a contract type.
- Polymorphism: `type="<contract name>"` attribute only when the runtime contract differs from
  the declared one; the element name stays that of the declared type. An instance of a CONCRETE
  polymorphic root writes its own body, unannotated - which is what the oracle does, and where
  this profile deliberately parts ways with the modern one (see below).
- `ISerializable` dialect: each `SerializationInfo` entry becomes an element named after the
  (encoded) key, values declared `object` carry a `type=` discriminator.
- Scalars: DCS lexical forms (ISO dates truncated per `DateTimeKind`, ISO 8601 durations,
  `char` as its code point, `decimal` keeping scale, round-trip doubles, enums by
  `[EnumMember(Value=)]` or name via a generated switch, `DateTimeOffset` as the two-element
  `{DateTime, OffsetMinutes}` structure, `byte[]` as base64).
- Text: no XML declaration, no namespaces or prefixes (`i:nil` arrives as `nil`, `i:type` as
  `type`), self-closing `<X />` with a space, text line endings as raw CRLF.

Three deliberate deviations from raw DCS, each pinned by a dedicated test, are requirements:

1. Dictionary entry names carry no namespace-hash digest (`KeyValueOfstringShelf`, not
   `KeyValueOfstringShelfQU_P9Vt29`). Measured: zero consumers of the digest.
2. Control characters are sanitized at the value level (raw DCS emits `&#x1;`, a document a
   conformant parser rejects). A strict reproduction mode exists for certification harnesses.
   **Text sinks only**: this filter lives in `CrystalXmlWriter`, which is what produces the wire.
   The infoset emitters (`XDocumentEmitter`, `XmlWriterEmitter`) apply none of it - the DOM sees
   the characters verbatim, and `XmlWriter` answers for them under its own `CheckCharacters`.
3. Typed exceptions (`CrystalXmlCycleException`, `CrystalXmlUnknownTypeException`,
   `CrystalXmlNotSupportedException`, `CrystalXmlInvalidNameException`) replace
   `SerializationException`.

## The modern profile: the XML a JSON reader would predict

| JSON | Modern XML |
|---|---|
| `{"title": "x"}` | `<title>x</title>` - same naming ladder as the JSON |
| root | the type name through the same ladder; optional `rootName` per call |
| null member | absent (like JSON by default); `WithNullMembers()` gives `<x nil="true" />`; per-member `[JsonIgnore(Condition = ...)]` honored |
| `"tags": ["a","b"]` | unwrapped by default: `<tags>a</tags><tags>b</tags>`; `[XmlProperty(ItemName = "tag")]` wraps: `<tags><tag>a</tag>...</tags>`; a bare nested collection (`List<List<T>>`) is a build error (CXML0006) - introduce an intermediate type |
| dictionary | `XmlDictionaryFormat { Default, Direct, KeyAttribute, KeyValueAttributes, KeyValueElements }`; modern default is `Direct` (`<scores><math>12</math></scores>`, non-NCName key = typed runtime exception) |
| `"$type": "cat"` | `type="cat"` attribute - the discriminator is an annotation |
| `[XmlProperty("@id")]` | `<book id="42">` - data as an attribute, scalars only; forbidden on the compat profile (DCS has no user attributes) |

An instance of a CONCRETE polymorphic root is refused here with
`CrystalXmlUnknownTypeException`, where the compat profile writes the root's own body. This wire
matches the JSON side, which carries no discriminator for that value either: a reader could not
tell it from a subtype whose annotation went missing, so it is refused rather than written under
a shape nobody can interpret.

## Example

```csharp
[CrystalJsonConverter(CrystalJsonSerializerDefaults.Web)]   // camelCase
[CrystalXmlOutput]                                          // derived Profile: modern
[CrystalJsonSerializable(typeof(Book))]
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

A type present in two containers has two serializers, each with its profile's wire. Generic code
takes the facet: `void Export<T>(ICrystalXmlSerializer<T> serializer, T value, IBufferWriter<byte> output)`.

## Diagnostics and runtime guards

Three refusal mechanisms, and which one applies is a rule, not a case-by-case choice:

| Mechanism | When |
|---|---|
| **CXML diagnostic** | the refusal is decidable at generation time from the DECLARATIONS alone (an attribute, a type, a contract name). It points at the offending declaration and carries a remedy. One member of the range, CXML0012, is an Info instead: it does not refuse anything, it names a setting the resolved wire never consults. |
| **`#error` in the emitted source** | a structural impossibility discovered inside emission, which no declaration could have predicted. Also kept as an unreachable backstop under a diagnostic that already covers the case. |
| **typed exception** | the refusal is DATA-dependent: only the value being written can decide it (a runtime type outside the graph, a non-NCName dictionary key, an undeclared enum value, a graph deeper than the cap). |

Build-time diagnostics live in the CXML range:

| Id | Refuses |
|---|---|
| CXML0001 | profile/policy incoherence on the container: a naming policy (camelCase and friends) next to the DataContract XML wire, whose element names come from the data contract. `PropertyNameCaseInsensitive` is NOT a trigger: it decides how an incoming name is matched when reading JSON, and this overlay never reads |
| CXML0002 | enrollment shape |
| CXML0003 | attribute projection of a member with no lexical form |
| CXML0004 | the XML naming vocabulary on the compat profile |
| CXML0005 | two members resolving to the same XML name, discriminator included |
| CXML0006 | a bare nested collection on the modern profile |
| CXML0007 | any name that is not a legal NCName: a declared `[XmlProperty]` name or `ItemName`, a bare `@`, the `"@x"` + `Attribute = false` contradiction, a member's name DERIVED from its JSON name, and a `[DataContract(Name = ...)]` that would name the root element. Modern profile only for the derived and root cases: the compat wire encodes every name through `XmlConvert.EncodeLocalName` |
| CXML0008 | a member converter without the XML facet |
| CXML0009 | an attribute-projected member with a custom converter |
| CXML0010 | `[CollectionDataContract]` on a compat member's type |
| CXML0011 | a dictionary whose resolved shape carries the value as text (`KeyAttribute`, `KeyValueAttributes`) while the value type has no lexical form |
| CXML0012 | **Info, not an error** - a setting that was written explicitly, resolved, and then never consulted: an `[XmlProperty(ItemName = ...)]` on a member with no items, a `[JsonIgnore(Condition = Never)]` on an attribute-projected member (an attribute has no nil form, so a null one is absent either way), and a `[CrystalXmlOutput(DictionaryFormat = ...)]` on a container whose resolved profile is the compat one (which has a single dictionary shape) |

At run time, graphs deeper than `CrystalXml.MaxDepth` (256 levels of generated recursion) raise
`CrystalXmlCycleException` - the guard cannot distinguish a genuine cycle from a legitimately
deeper acyclic graph, and its message says so. The depth counter cannot cross a call into
`ICrystalXmlSerializer<T>.WriteXml` or `ICrystalXmlSerializable.WriteXml`: a cycle running
entirely through such hooks is not covered by the guard.

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

The generated XML code emits UTF-8 string literals (`"..."u8`) for the cached element and
attribute names, so a container that enables XML output must compile at **`LangVersion` 11 or
later**. Projects targeting .NET 8 or later meet this by default; older-style projects (including
.NET Framework, whose default is C# 7.3) must set `LangVersion` explicitly. JSON-only containers
are unaffected. The XML certification suite itself runs on modern .NET only: whether the lite
(`netstandard2.0`/`net472`) path supports XML output at all is an open product question, and until
it is answered the XML fixtures are excluded from the `net472` validation targets rather than
shipped unvalidated.
