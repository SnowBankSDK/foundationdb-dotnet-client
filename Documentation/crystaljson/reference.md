# CrystalJson reference

The lookup tables for everyday work: the source generator setup, the attributes you put on a type,
the settings you pass to a call, and the build diagnostics you might hit. For the task guides see
[Working with CrystalJson](serializing.md); for the design see [What it is and why](index.md). Where
a behavior changed between releases, the [7.4.2 to 7.4.3 migration guide](../migrations/7.4.2-to-7.4.3.md)
carries the full story, and this page links to it rather than repeating it.

Every example uses `using SnowBank.Data.Json;`.

## Setup

CrystalJson's runtime types (`JsonValue`, `CrystalJson`, `CrystalJsonSettings`) live in
**`SnowBank.Core`**. A project that references `SnowBank.Core` serializes, parses and uses the DOM
through the reflection path with no further setup.

Generated converters and typed proxies need the source generator as well. It ships as a separate
package that the compiler runs as a Roslyn analyzer. It is a build-time tool, not part of your
shipped application; `SnowBank.Core` is the only runtime dependency:

```xml
<!-- runtime: the JsonValue DOM and the CrystalJson API -->
<PackageReference Include="SnowBank.Core" />
<!-- build-time only: the source generator, a Roslyn analyzer, not redistributed with your app -->
<PackageReference Include="SnowBank.Serialization.Json.CodeGen" />
```

Give both the same version as your other SnowBank packages, or omit the version under central package
management. The generator requires **C# 9 or later**: below that it reports `SYSLIB1221` and emits
nothing, JSON included, so set `<LangVersion>` to 9 or higher (the common trigger is a ported project
still pinning a .NET Framework era `<LangVersion>7.3</LangVersion>`).

A container is a `partial` class that declares which types it serializes. The generated members land
inside it:

```csharp
[CrystalJsonConverter]
[CrystalSerializable(typeof(Book))]
public static partial class AcmeSerializers { }

string json = AcmeSerializers.Book.ToJsonText(book);
Book back = AcmeSerializers.Book.Deserialize(json);
```

## Container attributes

The container declares three independent things: which class hosts the code, which formats it
produces, and which types it enrolls.

| Attribute | Namespace | Role |
|---|---|---|
| `[CrystalConverter]` | `SnowBank.Data` | the container marker; names no format on its own |
| `[CrystalSerializable(typeof(T))]` | `SnowBank.Data` | enrolls a type; repeatable; feeds every format the container produces |
| `[CrystalJsonOutput(...)]` | `SnowBank.Data.Json` | requests the JSON format and carries its parameters (profile, naming policy) |
| `[CrystalJsonConverter(...)]` | `SnowBank.Data.Json` | alias: `[CrystalConverter]` + `[CrystalJsonOutput]` with the same parameters, for a JSON-only container |
| `[CrystalJsonSelfSerializable]` | `SnowBank.Data.Json` | meta-attribute for self-serializable types (a type acts as its own container); see the [migration guide](../migrations/7.4.2-to-7.4.3.md#new-apis) |

A profile passed to `[CrystalJsonOutput(...)]` or `[CrystalJsonConverter(...)]` sets the container's
default output form, `CrystalJsonSerializerDefaults.Web` for camelCase, `.DataContractCompat` for the
legacy `DataContractJsonSerializer` output. Settings passed at a call site replace the profile for
that call.

`[CrystalSerializable]` replaces the obsolete `[CrystalJsonSerializable]`; enrollment is
format-neutral now. XML output has its own attributes and its own page,
[CrystalXml](../CrystalXml.md).

## Member attributes

Put these on a property or field to change how that one member serializes. All are honored on both
the reflection path and the generated path.

| Attribute | Effect |
|---|---|
| `[JsonProperty("name")]` | renames the member on the output |
| `[JsonProperty(DefaultValue = ...)]` | declares the member's default, used by the `WhenWritingDefault` ignore condition |
| `[JsonProperty(EnumFormat = JsonEnumFormat.Number)]` | writes this enum as its number instead of its name |
| `[JsonProperty(NumberFormat = JsonNumberFormat.String)]` | writes this number as a string (`"12345678901234567"`), which protects 64-bit values from JavaScript precision loss |
| `[JsonBooleanLiterals(whenFalse, whenTrue)]` | custom literals for a boolean; a `null` false literal omits the member when false. Arguments are a string, a bool, or a number |
| `[JsonIgnore]` | excludes the member (unconditional) |
| `[JsonIgnore(Condition = ...)]` | conditional exclusion; see the table below |
| `[JsonConvertWith(typeof(X))]` | serializes the member through the converter `X` (implements `IJsonPacker<T>` and/or `IJsonDeserializer<T>`) |
| `[JsonInclude]` | includes a non-public member on a type that has no `[DataContract]` |
| `[IgnoreDataMember]` | excludes the member on a type that has no `[DataContract]` |

`[JsonIgnore(Condition = ...)]` reads `JsonIgnoreCondition`, following the System.Text.Json meaning.
Note the naming trap: `Never` means "never ignore".

| Condition | Effect |
|---|---|
| `Always` (the default) | member excluded |
| `Never` | member always emitted, overriding the settings-level null and default discards |
| `WhenWritingNull` | omitted only when the value is null |
| `WhenWritingDefault` | omitted only when the value equals the member default |

For `[DataContract]` types, `[DataMember(Name = ...)]` renames and `[DataMember(IsRequired = true)]`
makes an absent member throw on read. Generated containers apply the DataContract membership model as
of 7.4.3; the [migration guide](../migrations/7.4.2-to-7.4.3.md#breaking-changes) has the details.

### Attributes from other serializers

CrystalJson reads the attributes an existing DTO already carries from System.Text.Json and
Newtonsoft.Json (JSON.NET), so a ported type serializes without re-annotation, the same on both paths:

| Foreign attribute | CrystalJson treats it as |
|---|---|
| System.Text.Json `[JsonPropertyName("x")]` | a rename, like `[JsonProperty("x")]` |
| Newtonsoft `[JsonProperty("x")]` | a rename |
| `[JsonIgnore]`, either spelling | exclude the member |
| System.Text.Json `[JsonIgnore(Condition = ...)]` | conditional exclusion, the conditions above |
| System.Text.Json `[JsonInclude]` | include a non-public member |
| `[JsonConverter(typeof(X))]`, either spelling | run `X`, when it implements `IJsonPacker<T>` and/or `IJsonDeserializer<T>` |
| `[DataContract]` / `[DataMember]` | the DataContract membership model |

When several naming attributes agree, the effective name comes from the highest priority one:
CrystalJson `[JsonProperty]`, then `[JsonPropertyName]`, then Newtonsoft `[JsonProperty]`. Two naming
attributes that disagree are a build error (`CJSON0011`): one type cannot serve two output contracts.
A foreign `[JsonConverter]` naming a type that does not implement the CrystalJson converter contract is
ignored, not an error, so a half-ported DTO stays serializable. The
[migration guide](../migrations/7.4.2-to-7.4.3.md) has the full interop rules.

## Settings

Pass a `CrystalJsonSettings` to a `Serialize`, `Parse`, or `Deserialize` call. Start from a preset
and add fluent modifiers; each modifier returns a new cached instance.

Presets:

| Preset | Output |
|---|---|
| `CrystalJsonSettings.Json` | the default: readable JSON |
| `CrystalJsonSettings.JsonCompact` | no whitespace |
| `CrystalJsonSettings.JsonIndented` | multi-line, indented |
| `CrystalJsonSettings.JsonStrict` | rejects comments and trailing commas on read |
| `CrystalJsonSettings.JsonReadOnly` | parses to frozen values |
| `CrystalJsonSettings.DataContractCompat` | reproduces the `DataContractJsonSerializer` output |

Common modifiers:

| Modifier | Effect |
|---|---|
| `.ThrowOnDuplicateFields()` | a repeated key is an error on read, not last-wins |
| `.WithoutComments()` | reject JavaScript comments on read |
| `.WithoutTrailingCommas()` | reject a trailing comma on read |
| `.WithEnumAsNumbers()` / `.WithEnumAsStrings()` | write enums as their number, or their name (the default) |
| `.WithNullMembers()` / `.WithoutNullMembers()` | emit or omit members whose value is null |
| `.WithoutDefaultValues()` | omit members that equal their default |
| `.WithMicrosoftDates()` / `.WithIso8601Dates()` | date format on the output |
| `.WithIso8601Durations()` / `.WithNumericDurations()` | `TimeSpan` as `"P1DT2H3M4S"`, or as a number of seconds (the default) |
| `.WithDictionariesAsPairArrays()` / `.WithDictionariesAsMaps()` | a dictionary as an array of `{"Key":..,"Value":..}`, or as a JSON object map (the default) |

`JsonStrict` does not cover duplicate fields; add `.ThrowOnDuplicateFields()` when a repeated key must
fail. To harden the parser for untrusted input, see
[Harden parsing for untrusted input](serializing.md#harden-parsing-for-untrusted-input).

To read several consecutive documents out of one buffer, use `CrystalJson.ParseFragment`, not
`WithTrailingData()` (which parses the first value and drops the rest).

## Defaults

- **Enums serialize as their name**, not their number. Reading accepts names (case-insensitive),
  numbers, and numeric strings regardless of settings.
- **The parser is permissive by default**: JavaScript comments and trailing commas are accepted. This
  is wrong for input you do not control; tighten it with `JsonStrict`.
- **Numbers keep their source literal** on the DOM route until you read them as a typed value.

## Diagnostics

The `CJSON####` codes below are the ones a normal author hits while writing DTOs. Each is reported at
the same place by both paths: the generator emits the diagnostic, and the reflection path throws the
same message when it builds the type's contract. The
[migration guide](../migrations/7.4.2-to-7.4.3.md) has the full treatment of each.

| Id | Severity | Refuses | Remedy |
|---|---|---|---|
| `CJSON0008` | Error | an unconditional `[JsonIgnore]` next to an include signal (`[DataMember]`, `[JsonInclude]`, a naming attribute) | split into one DTO per format, or remove one of the two attributes |
| `CJSON0010` | Error | `[JsonConvertWith]` names a type that implements neither `IJsonPacker<T>` nor `IJsonDeserializer<T>` | implement a converter facet, or fix the named type |
| `CJSON0011` | Error | a member declares two different names for two serializers | one DTO per format, each with one coherent set of attributes |
| `CJSON0012` | Warning | an `internal` member with no include or exclude signal, serialized by the generator but invisible to the reflection path | add `[JsonInclude]` or `[JsonIgnore]` to pin the intent |
| `CJSON0013` | Error | the `DataContractCompat` profile combined with a naming policy | drop the naming policy; the profile fixes the names |
| `CJSON0015` | Error | a serialization callback that takes a `StreamingContext` | remove the parameter, or replace it with `JsonValue`, `JsonObject`, or `JsonArray` |
| `CJSON0016` | Error | `[OnDeserializing]` on a type with a `required` or `init`-only member | drop `[OnDeserializing]`, or make the member settable |
| `CJSON0017` | Error | a `[JsonBooleanLiterals]` argument that is not a string, bool, or number | use a valid literal |
| `CJSON0018` | Warning | `StrictLiterals = true` with a `null` false literal (nothing to enforce on the false side) | remove `StrictLiterals`, or give the member a real false literal |
| `CJSON0019` | Warning | a `[CrystalSerializable]` enrollment of a type CrystalJson already serializes natively | remove the enrollment |

The self-serializable diagnostics (`CJSON0004` to `CJSON0007`, `CJSON0020`, `CJSON0021`) and the XML
generator codes (`CRYS####`, `CXML####`) are covered in the
[migration guide](../migrations/7.4.2-to-7.4.3.md) and [CrystalXml](../CrystalXml.md).
