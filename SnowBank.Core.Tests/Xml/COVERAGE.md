# CrystalXml coverage ledger - measured corpus traits vs the tests that pin them

Every row of the measured DCS feature matrix (the Acme corpus instrument: 4 802 captures, 82 883
elements, 18 focused probes, measured 2026-08-03) mapped to the test that covers it here. Test
names are given as `Fixture.Method`; fixtures live in `SnowBank.Core.Tests/Xml/` (oracle suite,
byte-compared against a live `DataContractSerializer` via `ReferenceDcsWire`) and in
`SnowBank.Serialization.Json.CodeGen.Tests` (generated-emission suites, structural pins).
Uncovered or reduced traits are named in the Gaps section - a gap named here is a finding, not a
hidden hole.

## 1. Attribute surface (the complete measured set is `nil` and `type`)

| Trait | Covering tests |
|---|---|
| `nil="true"` truth table (null vs empty string vs empty collection vs null nullable) | `DcsWireFidelityFacts.Test_Nil_Truth_Table`; `XmlDataContractEmissionFacts.Test_Nil_Truth_Table` |
| `<EmptyString></EmptyString>` vs `<EmptyList />` byte difference | inside both nil truth tables (literal expected strings) |
| `type=` only when runtime contract differs from declared; element keeps declared name | `DcsWireFidelityFacts.Test_Polymorphism_Discriminator`; `XmlDataContractEmissionFacts.Test_Polymorphism_Annotates_The_Runtime_Contract` |
| `type=` from the ISerializable dialect (object-declared values) | `DcsWireFidelityFacts.Test_ISerializable_Dialect_Keys_Become_Element_Names`, `.Test_Deviation_3_Undeclared_Runtime_Type_In_An_AnyType_Slot`; `XmlDataContractEmissionFacts.Test_ISerializable_Dialect_Names_Elements_After_The_Entry_Keys` |
| no `xmlns`, no prefixes, no `z:Id`/`z:Ref` | every byte-equality fact (the oracle format is prefix-free; any emitted prefix would fail equality) |

## 2. Member ordering

| Trait | Covering tests |
|---|---|
| default ordinal-alphabetical in the output name | `DcsWireFidelityFacts.Test_Member_Order_Default_Is_Ordinal_Alphabetical` |
| unordered members first, then `Order=` groups, alphabetical ties | `DcsWireFidelityFacts.Test_Member_Order_Explicit_Groups_After_Unordered`; `XmlDataContractEmissionFacts.Test_Member_Order_Explicit_Groups_Come_After_The_Unordered_Ones` |
| base-class members first | `DcsWireFidelityFacts.Test_Member_Order_Base_Level_Comes_First`; `XmlDataContractEmissionFacts.Test_Member_Order_Base_Level_Comes_First` |
| ordinal sort on the CONTRACT name (uppercase before lowercase, renames included) | `DcsWireFidelityFacts.Test_Renamed_Contract_And_Members` |

## 3. Collections and dictionaries

| Trait | Covering tests |
|---|---|
| item element = item type's contract name (`string`, `int`, `dateTime`, `Shelf`) | `DcsWireFidelityFacts.Test_Collection_Item_Element_Names`; `XmlDataContractEmissionFacts.Test_Collection_Item_Element_Names` |
| nested list item = `ArrayOfstring` | same two facts (the `Nested` member) |
| empty collection self-closes | nil truth tables + `Test_Collection_Item_Element_Names` |
| dictionary `KeyValueOfXY` + `Key`/`Value` children (`KeyValueOfstringstring`, `KeyValueOfintstring`, `KeyValueOfstringArrayOfstring`, `KeyValueOfstringShelf`) | `DcsWireFidelityFacts.Test_Dictionary_Entry_Shapes`; `XmlDataContractEmissionFacts.Test_Dictionary_Entry_Shapes` |
| namespace-hash digest divergence (acted deviation 1: `KeyValueOfstringShelf`, not `KeyValueOfstringShelfQU_P9Vt29`) | `DcsWireFidelityFacts.Test_Dictionary_Digest_Divergence_Is_Exactly_The_Hash_Suffix` (strip-and-compare); `XmlDataContractEmissionFacts.Test_Deviation_1_Dictionary_Entry_Names_Carry_No_Digest` |
| renamed enum contract in item and entry names | `XmlDataContractEmissionFacts.Test_A_Renamed_Enum_Contract_Names_The_Items_And_The_Dictionary_Entries` |
| composed generic contract names (`XOfY`, `{0}`, `{#}`, digest omitted, encode-once) | `XmlDataContractEmissionFacts.Test_Composed_Contract_Names`, `.Test_Composed_Contract_Names_As_Roots`, `.Test_Named_Generic_Expands_Its_Braces`; `DcsWireFidelityFacts.Test_Named_Generic_Expands_Braces` |

## 4. EmitDefaultValue, absent vs null

| Trait | Covering tests |
|---|---|
| `EmitDefaultValue=false` members absent at CLR default (null, zero, false, MinValue) | `DcsWireFidelityFacts.Test_EmitDefaultValue_False_Omits_Default_And_Null`; `XmlDataContractEmissionFacts.Test_EmitDefaultValue_False_Omits_Default_And_Null` |
| three distinguishable member states (value / nil / absent) | the two facts above plus the nil truth tables |
| `WithoutNullMembers()` drops nil elements (opt-in, audited) | `XmlDataContractEmissionFacts.Test_Without_Null_Members_Drops_The_Nil_Elements`, `.Test_A_Null_Root_Without_Null_Members_Is_An_Empty_Element` (the `AcmeRenderFacts` all-null run exercises nil members, NOT this setting) |

## 5. Scalar lexical forms

| Trait | Covering tests |
|---|---|
| the full scalar table (bool lowercase, decimal scale, `1.2E-09`, `INF`/`NaN`, dates per Kind, `PT1H33M30S`, char as code point, base64, enum labels, Uri, Guid) | `DcsWireFidelityFacts.Test_Scalar_Lexical_Forms`; `XmlDataContractEmissionFacts.Test_Scalar_Lexical_Forms`; unit-level: `ScalarFormatterFacts.*` (per-formatter, both profiles) |
| `DateTimeKind.Local` machine-offset dependence reproduced (known product defect kept) | `DcsWireFidelityFacts.Test_Local_Kind_DateTime_Is_Machine_Dependent_And_Reproduced`; `ScalarFormatterFacts.Test_DateTime_Local_Kind_Uses_The_Machine_Offset` |
| `DateTimeOffset` two-element `{DateTime, OffsetMinutes}` structure | inside both `Test_Scalar_Lexical_Forms` facts |
| `[EnumMember(Value=)]` labels; undeclared enum values; flags | `XmlDataContractEmissionFacts.Test_Deviation_3_An_Undeclared_Enum_Value_Raises_A_Typed_Exception`, `.Test_Deviation_3_An_Undeclared_Flags_Combination_Raises_A_Typed_Exception` (compat refuses, deviation 3); modern numeric fallback: `XmlModernEmissionFacts.Test_An_Undeclared_Enum_Value_Falls_Back_To_Its_Numeric_Form`, `.Test_An_Undeclared_Flags_Combination_Is_Refused_Loudly` |
| escaping (`&lt;` `&amp;`, bare quotes in text, raw CRLF, tab) | runtime writer suites (Core-XML subset, ported byte-for-byte from the spike escaping spec); end-to-end inside every byte-equality fact |
| control characters sanitized at the value (acted deviation 2) + strict reproduction mode | `DcsWireFidelityFacts.Test_Deviation_2_Sanitized_Control_Characters_Differ_From_The_Reference_Wire`, `.Test_Deviation_2_StrictControlCharacters_Mode_Matches_The_Reference_Wire_Exactly`; `XmlDataContractEmissionFacts.Test_Deviation_2_Control_Characters_Are_Sanitized_At_The_Value` |

## 6. Nesting, self-reference, shared instances, cycles

| Trait | Covering tests |
|---|---|
| structural recursion on self-referential types; shared instance written twice in full | `DcsWireFidelityFacts.Test_SelfReference_And_Shared_Instances` |
| a cycle throws in both pipelines (acted deviation 3: `SerializationException` vs `CrystalXmlCycleException`) | `DcsWireFidelityFacts.Test_Cycle_Throws_In_Both_Pipelines`; `XmlDataContractEmissionFacts.Test_A_Reference_Cycle_Throws_Instead_Of_Overflowing_The_Stack` (+ modern twin) |
| deep acyclic graphs: cap boundary pinned (exactly `MaxDepth` serializes, `MaxDepth+1` throws) | `XmlDataContractEmissionFacts.Test_A_Deep_Acyclic_Chain_Up_To_The_Cap_Is_Written_In_Full`, `.Test_A_Deep_Acyclic_Chain_Past_The_Cap_Throws_The_Same_Typed_Exception` (+ modern twins) |

## 7. Contract-shape details

| Trait | Covering tests |
|---|---|
| `[DataContract(Name=)]` renames the root | `DcsWireFidelityFacts.Test_Renamed_Contract_And_Members`; `XmlDataContractEmissionFacts.Test_Renamed_Contract_And_Members` |
| `[DataMember(Name=)]` verbatim, including non-C# names (`with-dash`) | same two facts |
| `[IgnoreDataMember]` and unannotated members absent | `DcsWireFidelityFacts.Test_IgnoreDataMember_And_Unannotated_Are_Absent` |
| `IsRequired=true` changes nothing on write | inside `Test_Renamed_Contract_And_Members` (the `Required` member) |
| private `[DataMember]` serialized (UnsafeAccessor thunks) | `DcsWireFidelityFacts.Test_Private_DataMember_Is_Serialized` |
| POCO mode (no `[DataContract]`): public read/write, alphabetical | `DcsWireFidelityFacts.Test_Poco_Mode_Public_ReadWrite_Alphabetical`, `.Test_Poco_Mode_Null_Member` |
| POCO mode omits a get-only property (family 20) | `DcsWireFidelityFacts.Test_Poco_Mode_ReadOnly_Member_Is_Absent` |
| `[DataContract]` with no members self-closes | `DcsWireFidelityFacts.Test_Empty_Contract_Self_Closes` |
| root name override | `DcsWireFidelityFacts.Test_Root_Name_Override`; `XmlDataContractEmissionFacts.Test_The_Root_Name_Can_Be_Overridden` |
| ISerializable dialect: keys become element names, non-NCName keys encoded | `DcsWireFidelityFacts.Test_ISerializable_Dialect_Keys_Become_Element_Names`, `.Test_ISerializable_Dialect_Non_NCName_Key` |

## End to end (the Acme simulation)

`AcmeSimulation/AcmeRenderFacts.cs`: a 35-member account-shaped graph through all output sinks
(byte equality on char/byte sinks, tree equality on infoset sinks), and one corpus-pattern XSLT
(`Loans[not(@nil)]`, `count(Loan[IsLate = 'false'])`, `ArrayOf*` read, `Service[@type != ...]`
and `[@type = ...]` discriminator reads) transforming BOTH the CrystalXml format and the live-DCS
format into identical HTML, for a populated and an all-null account, plus direct whole-document
format equality.

## Gaps, reductions, and by-design divergences

Named, with owners:

1. NOT REPRODUCED BY DESIGN - a collection (or any type the container does not declare) in an
   `object`-typed slot: reflection-free generated code cannot name `ArrayOfstring` at run time;
   refused with a typed exception, pinned by
   `DcsWireFidelityFacts.Test_Deviation_3_Undeclared_Runtime_Type_In_An_AnyType_Slot` and
   `XmlDataContractEmissionFacts.Test_A_Runtime_Type_The_Container_Cannot_Name_Is_Refused_In_An_AnyType_Slot`.
2. NOT REPRODUCED BY DESIGN - `[CollectionDataContract]` on a compat member's type: refused with
   CXML0010 (`XmlPropertyMetadataFacts.Test_A_CollectionDataContract_Member_On_A_DataContract_Container_Is_A_Build_Error`)
   rather than half-honored. The matrix measures 182 source declarations; migrating call sites
   that rely on them will surface this diagnostic and must introduce an explicit shape.
3. Two distinct sub-cases, per the owner-pinned design ruling (2026-08-04):
   (a) NATIVE-PATH FAMILIES, out of this suite's scope - bare collection/scalar roots
   (`List<Shelf>`, `string` as root: matrix families "List as root", "named collection as root",
   "string as root"). CrystalJson serializes collections, dictionaries and scalars NATIVELY,
   root included; the source generator emits converters for POCO types only, and an owner-driven
   follow-up makes it detect and skip such enrollments instead of crashing on them. These
   families therefore belong to a native XML root path, which CrystalXml DOES NOT currently
   have (its XML surface is exclusively generated per-type facets): they are out of scope for
   this generated-converter certification suite and do not count against its family coverage.
   The reference wire for these roots is measured all the same: three oracle-only facts in
   `DcsNamespaceReferenceFacts` pin the `ArrayOfX` root names and their namespaces (Arrays for
   lexical items, the item's contract namespace for contract items, Serialization for a bare
   string or scalar root).
   The measured corpus does contain `ArrayOf*` roots in real captures, so the stage-B fidelity
   harness will quantify the need; a native XML root path (composing the item type's
   `ICrystalXmlSerializer<T>` facet under an `ArrayOfX` root) is the natural follow-up if the
   captures demand it - parked as an owner question.
   (b) COVERED - the POCO read-only-property trait (`ReadOnlyIgnored`, family 20). It was
   defect-gated: the JSON deserializer emission failed to compile (CS0200) before the XML overlay
   was reached. Upstream commit `8ce07e52` makes a get-only member serialization-only (skipped on
   deserialization), so `PocoProbe.ReadOnlyIgnored` is back, and the DataContract XML profile drops
   read-only members too - measured, not assumed: the reference serializer omits them on a POCO,
   and rejects a read-only `[DataMember]` on a `[DataContract]` type outright
   (`InvalidDataContractException`, "No set method for property"). Pinned by
   `DcsWireFidelityFacts.Test_Poco_Mode_ReadOnly_Member_Is_Absent`.
4. REDUCED - the ISerializable family lost its `KeyedBag<List<string>>` half (gap 1's shape);
   `type="ArrayOfstring"` on an ISerializable value is pinned by the deviation-3 fact instead.
5. COVERED (this ledger's own probe) - `List<object>` as a collection member (matrix:
   `<Results><anyType nil="true" /></Results>`): item element named `anyType`, null items nil,
   non-null items discriminated. Reproduced byte-for-byte: each item goes through the same
   per-item `anyType` switch already proven for boxed built-ins (`PolymorphicProbe.AsObjectString`/
   `AsObjectInt`), not the undeclared-runtime-type error of gap 1 (which fires only when the
   OBJECT SLOT ITSELF holds an unregistered collection/composed type, not a plain boxed scalar
   inside a declared `List<object>`). Pinned by
   `DcsWireFidelityFacts.Test_Collection_Of_Object_Items_Are_AnyType`.
6. OUT OF SCOPE, MEASURED ABSENT - `[DataContract(IsReference=true)]` / `z:Id`/`z:Ref` object
   tracking: exactly one declaration in the measured solution, zero format occurrences, no support
   and no test.
7. TIMEZONE - `DateTimeKind.Local` format output depends on the machine timezone (reproduced
   product behavior, matrix XW-6); the covering tests compute the expected offset at run time.
8. GAP, DISCLOSED - deviation-2 control-character sanitization is a property of the TEXT sinks
   only. `CrystalXmlWriter` drops C0 controls, unpaired surrogate halves and U+FFFE/U+FFFF;
   `CrystalXDocumentEmitter` and `CrystalXmlWriterEmitter` apply no such filter and disclose it in their
   `<remarks>`. Deliberate (the rule exists to reproduce a byte-exact legacy format, which an
   infoset sink does not produce), but it means the same value can serialize through one sink and
   throw through another. Not tested on the infoset side: `InfosetEmitterFacts` feeds no such
   content by design.
9. GAP, DISCLOSED - a cycle running through `ICrystalXmlSerializer<T>.WriteXml` or
   `ICrystalXmlSerializable.WriteXml` is UNGUARDED: the depth counter is a generated parameter and
   does not cross a hook call, so it resets to 0 on the other side and `CrystalXmlCycleException`
   never fires. Stated in the runtime docs; no test pins it either way.
10. PARTLY RECOVERED - this entry used to name two probe reductions forced by the same JSON
   generator; one is fixed, one is still open.
   (a) RECOVERED - `NamedGenericProbe<T>.Payload` is declared `T?` again (likewise `Box<T>` and
   `DigestedProbe<T>` in `XmlDataContractEmissionFacts`). Upstream commit `8ce07e52` stops
   spelling an unconstrained `T?` as `Nullable<T>` when the substitution is a value type, so the
   value-type instantiations (`NamedGenericProbe<bool>`, `DigestedProbe<RenamedMedium>`) compile
   and the nullable-generic trait is carried again.
   (b) STILL OPEN - `AnyTypeCollectionProbe` holds a `List<object>` and not a `List<object?>`.
   Re-checked after the upstream fix: the generated deserializer still materializes a
   `List<object>` and assigns it to the annotated member, which is a CS8619 in generated code
   ("Nullability of reference types in value of type `List<object>` doesn't match target type
   `List<object?>`"). The `anyType` trait is covered; the nullable-item annotation is not.
11. GAP, DISCLOSED - `GetXmlDcsContractNameOfArgument`'s fallback uses the argument's plain type
   name, dropping a non-enum `[DataContract(Name = ...)]` and the declaring-type chain of a nested
   type. It is only reachable through one narrow escape, and needs ALL THREE conditions at once:
   the argument type is NOT registered in the container, AND it implements
   `ICrystalXmlSerializable` (which is what keeps emission from refusing outright), AND it is
   renamed by a contract or nested - so its composed name would differ from its plain one. Any
   registered type, or any unregistered one without the hook, takes a different branch. Named
   here rather than refused, per the ruling on that finding.
