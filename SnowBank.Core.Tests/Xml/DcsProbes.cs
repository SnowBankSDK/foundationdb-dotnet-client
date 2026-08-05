#region Copyright (c) 2023-2026 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

// This file IS compiled for the net472 validation target: see the remark on ReferenceDcsWire.cs.

// note: probe types mirror, family by family, the design spike's own probe set (Probes.cs), whose wire was measured
// against a LIVE DataContractSerializer. Every expectation in DcsWireFidelityFacts is measured against that live
// oracle (ReferenceDcsWire), never assumed. Acme-flavored naming is kept, as in the spike.
// note: this namespace (SnowBank.Data.Xml.Tests.Acme) is NOT the spike's own, and the difference is wire-safe only
// because of two things: the reference pipeline's StrippingXmlWriter erases namespaces entirely, and the one test that
// looks at a contract namespace (the dictionary-digest divergence) regex-matches whatever 8-char digest it finds
// rather than a literal. A test that ever hard-codes a namespace-derived string must not assume the spike's value.
namespace SnowBank.Data.Xml.Tests.Acme
{
	using System.Runtime.Serialization;
	using System.Text.Json.Serialization;
	using SnowBank.Data.Json;
	using SnowBank.Data.Xml;

	#region Probe types...

	[DataContract]
	public sealed class NilProbe
	{
		[DataMember] public string? NullString;
		[DataMember] public string? EmptyString;
		[DataMember] public string? SetString;
		[DataMember] public int? NullNullableInt;
		[DataMember] public int? SetNullableInt;
		[DataMember] public Shelf? NullShelf;
		[DataMember] public Shelf? SetShelf;
		[DataMember] public List<string>? NullList;
		[DataMember] public List<string>? EmptyList;
		[DataMember] public List<string>? FullList;
		[DataMember] public byte[]? NullBytes;
		[DataMember] public byte[]? EmptyBytes;
	}

	[DataContract]
	public sealed class Shelf
	{
		[DataMember] public string? Label;
	}

	[DataContract]
	public sealed class OrderDefaultProbe
	{
		[DataMember] public string? Zulu;
		[DataMember] public string? Alpha;
		[DataMember] public string? Mike;
		[DataMember] public string? Bravo;
	}

	[DataContract]
	public sealed class OrderExplicitProbe
	{
		[DataMember(Order = 3)] public string? Alpha;
		[DataMember(Order = 1)] public string? Zulu;
		[DataMember(Order = 2)] public string? Mike;
		[DataMember(Order = 2)] public string? Bravo;
		[DataMember] public string? NoOrderYankee;
		[DataMember] public string? NoOrderCharlie;
	}

	[DataContract]
	public class OrderBase
	{
		[DataMember] public string? ZuluFromBase;
		[DataMember(Order = 1)] public string? OrderedFromBase;
	}

	[DataContract]
	public sealed class OrderDerivedProbe : OrderBase
	{
		[DataMember] public string? AlphaFromDerived;
	}

	[DataContract]
	public sealed class EmitDefaultProbe
	{
		[DataMember(EmitDefaultValue = false)] public int OmittedZeroInt;
		[DataMember(EmitDefaultValue = true)] public int KeptZeroInt;
		[DataMember(EmitDefaultValue = false)] public string? OmittedNullString;
		[DataMember(EmitDefaultValue = true)] public string? KeptNullString;
		[DataMember(EmitDefaultValue = false)] public bool OmittedFalseBool;
		[DataMember(EmitDefaultValue = false)] public int? OmittedNullNullableInt;
		[DataMember(EmitDefaultValue = false)] public DateTime OmittedDefaultDate;
		[DataMember(EmitDefaultValue = false)] public List<string>? OmittedNullList;
		[DataMember(EmitDefaultValue = false)] public int SetInt;
	}

	/// <summary>[CollectionDataContract]-annotated list: kept for reference alongside <see cref="CollectionProbe"/>, but never
	/// enrolled in the DataContract-compat XML container. The generator refuses [CollectionDataContract] members on that
	/// profile with diagnostic CXML0010 (a pre-existing, documented decision from Task 9): the element names it drives
	/// (<c>TheItems</c>/<c>TheItem</c>) are read from an attribute the generated wire never inspects, so honoring it would
	/// silently diverge from the reference wire it is trying to imitate. This family is therefore excluded from
	/// <c>DcsWireFidelityFacts</c>, not exercised as a false red.</summary>
	[CollectionDataContract(Name = "TheItems", ItemName = "TheItem")]
	public sealed class NamedItems : List<string>;

	/// <summary>Same CXML0010 exclusion as <see cref="NamedItems"/>, for the dictionary shape.</summary>
	[CollectionDataContract(Name = "TheMap", ItemName = "Entry", KeyName = "TheKey", ValueName = "TheValue")]
	public sealed class NamedMap : Dictionary<string, string>;

	[DataContract]
	public sealed class CollectionProbe
	{
		[DataMember] public List<string>? Strings;
		[DataMember] public List<int>? Ints;
		[DataMember] public string[]? StringArray;
		[DataMember] public Shelf[]? ShelfArray;
		[DataMember] public List<Shelf>? Shelves;
		[DataMember] public List<Shelf>? EmptyShelves;
		[DataMember] public List<List<string>>? Nested;
		[DataMember] public List<DateTime>? Dates;
		[DataMember] public List<string>? WithNullItem;
		[DataMember] public IList<string>? DeclaredAsInterface;
	}

	[DataContract]
	public sealed class DictionaryProbe
	{
		[DataMember] public Dictionary<string, string>? PlainMap;
		[DataMember] public Dictionary<int, string>? IntKeyMap;
		[DataMember] public Dictionary<string, string>? EmptyMap;
	}

	/// <summary>Value type from a non-built-in namespace: its dictionary entry name carries a digest on the reference wire.</summary>
	[DataContract]
	public sealed class HashedDictionaryProbe
	{
		[DataMember] public Dictionary<string, Shelf>? ObjectMap;
	}

	// note: the spike declares polymorphism through [KnownType] alone. The live DCS oracle still needs that attribute
	// (it knows nothing about [JsonDerivedType]), but the generator's own polymorphic map is driven by
	// [JsonDerivedType] instead (the same attribute the modern JSON profile reads) -- so both are kept here, one per
	// consumer. This mirrors the adaptation already made by Task 9's own XmlDataContractEmissionFacts.cs probes.
	[DataContract]
	[KnownType(typeof(AudioBook))]
	[KnownType(typeof(PrintedBook))]
	[JsonDerivedType(typeof(AudioBook), "audio")]
	[JsonDerivedType(typeof(PrintedBook), "printed")]
	public class CatalogItem
	{
		[DataMember] public string? Title;
	}

	[DataContract]
	public sealed class AudioBook : CatalogItem
	{
		[DataMember] public int DurationMinutes;
	}

	[DataContract]
	public sealed class PrintedBook : CatalogItem
	{
		[DataMember] public string? Isbn;
	}

	// note: [KnownType(typeof(List<string>))] is added here (absent from the spike): without it, the LIVE oracle
	// itself refuses a List<string> dropped into an object-declared slot with the same "type not expected" refusal
	// this test wants to pin as CrystalXml-only. DCS's own closure for an anyType slot is not full reflection after
	// all -- it needs the type declared too, just through [KnownType]/[ServiceKnownType] rather than a source
	// generator's own registration list. Declaring it here makes DCS succeed for List<string> (matching the acted
	// deviation's premise) while leaving CrystalXml's own closed switch (this container's registered types) refuse it.
	[DataContract]
	[KnownType(typeof(List<string>))]
	public sealed class PolymorphicProbe
	{
		[DataMember] public CatalogItem? DeclaredBaseHoldingBase;
		[DataMember] public CatalogItem? DeclaredBaseHoldingDerived;
		[DataMember] public AudioBook? DeclaredExact;
		[DataMember] public List<CatalogItem>? Zoo;
		[DataMember] public object? AsObjectString;
		[DataMember] public object? AsObjectInt;
		[DataMember] public object? AsObjectLong;
		[DataMember] public object? AsObjectNull;
	}

	[DataContract(Name = "RenamedContract", Namespace = "urn:acme:renamed")]
	public sealed class RenameProbe
	{
		[DataMember(Name = "renamed_member")] public string? Original;
		[DataMember(Name = "with-dash")] public string? Dashed;
		[DataMember(IsRequired = true)] public string? Required;
		[DataMember] public string? Plain;
	}

	public enum Medium { Print = 0, Audio = 1, Digital = 2 }

	[DataContract]
	public enum FlaggedMedium
	{
		[EnumMember(Value = "imprime")] Print = 0,
		[EnumMember] Audio = 1,
	}

	[Flags]
	public enum Sections { None = 0, Adult = 1, Youth = 2, Heritage = 4 }

	[DataContract]
	public sealed class ScalarProbe
	{
		[DataMember] public bool TrueBool;
		[DataMember] public bool FalseBool;
		[DataMember] public decimal Decimal;
		[DataMember] public decimal DecimalTrailingZero;
		[DataMember] public decimal DecimalNegative;
		[DataMember] public double Double;
		[DataMember] public double DoubleNaN;
		[DataMember] public double DoubleInfinity;
		[DataMember] public double DoubleExponent;
		[DataMember] public float Float;
		[DataMember] public DateTime DateUnspecified;
		[DataMember] public DateTime DateUtc;
		[DataMember] public DateTime DateMinValue;
		[DataMember] public DateTime DateNoFraction;
		[DataMember] public DateTimeOffset DateOffset;
		[DataMember] public TimeSpan Duration;
		[DataMember] public TimeSpan DurationFine;
		[DataMember] public Guid Guid;
		[DataMember] public char Char;
		[DataMember] public byte[]? Bytes;
		[DataMember] public Medium EnumValue;
		[DataMember] public FlaggedMedium EnumRenamed;
		[DataMember] public Sections FlagsCombo;
		[DataMember] public Uri? Uri;
		[DataMember] public long Long;
		[DataMember] public ulong UnsignedLong;
		[DataMember] public short Short;
		[DataMember] public byte UnsignedByte;
		[DataMember] public sbyte SignedByte;
		[DataMember] public string? SpecialChars;
		[DataMember] public string? Newlines;
		[DataMember] public string? Unicode;
		[DataMember] public string? ControlChars;
	}

	[DataContract]
	public sealed class Node
	{
		[DataMember] public string? Label;
		[DataMember] public Node? Next;
		[DataMember] public List<Node>? Children;
	}

	[DataContract]
	public sealed class SelfRefProbe
	{
		[DataMember] public Node? Root;
		[DataMember] public Node? SharedA;
		[DataMember] public Node? SharedB;
	}

	[DataContract]
	public sealed class EmptyContractProbe;

	public sealed class PocoProbe
	{
		public string? Zulu { get; set; }
		public string? Alpha { get; set; }
		public int Number { get; set; }
		public List<string>? Items { get; set; }

		// get-only property: POCO mode is "public get+set only", so the live oracle omits this member entirely.
		// It used to be dropped from this probe because the generated JSON deserializer (emitted alongside the XML
		// writer by the same container, even though this profile never calls it) assigned it in its object-initializer
		// form, which does not compile (CS0200). The generator now skips read-only members on deserialization.
		public string ReadOnlyIgnored => "never";
	}

	[DataContract]
	public sealed class IgnoreProbe
	{
		[DataMember] public string? Kept;
		[IgnoreDataMember] public string? Ignored;
		public string? NotAnnotated;
	}

	[DataContract]
	public sealed class PrivateMemberProbe
	{
		[DataMember] private string? secret = "hidden-but-serialized";
		[DataMember] public string? Visible;

		public void SetSecret(string? value) => this.secret = value;
	}

	[DataContract(Name = "Envelope{0}")]
	public sealed class NamedGenericProbe<T>
	{
		// T? on an unconstrained T is "defaultable T", NOT Nullable<T>: a value-type instantiation (here bool) keeps a
		// plain bool member. The emission used to spell the substituted type "bool?" and fail to assign it (CS0266).
		[DataMember] public T? Payload;
	}

	/// <summary>
	/// Clean-room equivalent of the measured key-flattening pattern: an ISerializable wrapper whose GetObjectData turns
	/// each dictionary KEY into a SerializationInfo entry name, so the reference wire emits one element PER KEY with a
	/// type discriminator on the value.
	/// </summary>
	[Serializable]
	public sealed class KeyedBag<T> : ISerializable
	{
		private readonly Dictionary<string, T> inner = [];

		public KeyedBag() { }

		public void Add(string key, T value) => this.inner.Add(key, value);

		void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
		{
			foreach (var (key, value) in this.inner)
			{
				info.AddValue(key, value);
			}
		}
	}

	// note: the spike's second member (KeyedBag<List<string>> Suggestions, meant to pin the "undeclared runtime type"
	// deviation) is dropped here: DECLARING it is enough to fail the build, with the generator's own #error for a
	// closed generic used as a generic argument ("KeyedBagOfArrayOfstring" needs List<string>'s composed name, which
	// this emission refuses to guess) -- the same documented, build-time limitation covered by Task 9's own
	// Test_A_Generic_Argument_That_Is_Itself_A_Closed_Generic_Fails_The_Build. That is a DIFFERENT failure surface
	// than the brief's "reflection-free code cannot name ArrayOfstring at run time" (a RUNTIME refusal): this family
	// never reaches runtime at all, so acted deviation 3 is pinned instead via an object-typed slot holding an
	// undeclared runtime type (DcsWireFidelityFacts.Test_Deviation_3_Undeclared_Runtime_Type_In_An_AnyType_Slot),
	// mirroring Task 9's own Test_A_Runtime_Type_The_Container_Cannot_Name_Is_Refused_In_An_AnyType_Slot. See the
	// Task 10 report for the full judgment call.
	[DataContract]
	public sealed class KeyedBagProbe
	{
		[DataMember] public KeyedBag<string>? Properties;
	}

	/// <summary>Measured corpus shape: a declared <c>List&lt;object&gt;</c> collection member, item element named
	/// <c>anyType</c>, null items carrying <c>nil="true"</c>, non-null items carrying a <c>type=</c> discriminator
	/// for their boxed runtime type (matrix: <c>&lt;Results&gt;&lt;anyType nil="true" /&gt;...&lt;/Results&gt;</c>).</summary>
	[DataContract]
	public sealed class AnyTypeCollectionProbe
	{
		// List<object>, not List<object?>: the generated deserializer materializes a List<object> and assigns it to the
		// member, so the annotated form is a CS8619 in generated code ("Nullability of reference types in value of type
		// 'List<object>' doesn't match target type 'List<object?>'"). Re-checked after the upstream member-form fixes,
		// which cover value-type T? and read-only members but not this collection-element annotation; still open.
		// Null items go in via null!, like WithNullItem.
		[DataMember] public List<object>? Results;
	}

	#endregion

	#region Test container...

	// note: the spike's "List<Shelf> as root" and "string as root" families (a bare collection or scalar type passed
	// directly to [CrystalSerializable], with no declaring DTO) are excluded here, and stay excluded by design:
	// CrystalJson serializes collections, dictionaries and scalars natively, root included, so the generator emits no
	// converter for such an enrolment at all (it reports the CJSON0019 guidance instead, see NativeEnrolmentGuardFacts).
	// There is therefore no generated wire for these two families to compare against the oracle. Enrolling them used to
	// emit code that did not compile (CS0106/CS0720/CS0548/CS1551 on a nameless indexer inside a nested
	// "PropertyEncodedNames" helper); that is the defect the guard closed.
	[CrystalConverter]
	[CrystalJsonOutput(CrystalJsonSerializerDefaults.DataContractCompat)]
	[CrystalXmlOutput]
	[CrystalSerializable(typeof(NilProbe))]
	[CrystalSerializable(typeof(Shelf))]
	[CrystalSerializable(typeof(OrderDefaultProbe))]
	[CrystalSerializable(typeof(OrderExplicitProbe))]
	[CrystalSerializable(typeof(OrderDerivedProbe))]
	[CrystalSerializable(typeof(EmitDefaultProbe))]
	[CrystalSerializable(typeof(CollectionProbe))]
	[CrystalSerializable(typeof(DictionaryProbe))]
	[CrystalSerializable(typeof(HashedDictionaryProbe))]
	[CrystalSerializable(typeof(CatalogItem))]
	[CrystalSerializable(typeof(AudioBook))]
	[CrystalSerializable(typeof(PrintedBook))]
	[CrystalSerializable(typeof(PolymorphicProbe))]
	[CrystalSerializable(typeof(RenameProbe))]
	[CrystalSerializable(typeof(ScalarProbe))]
	[CrystalSerializable(typeof(Node))]
	[CrystalSerializable(typeof(SelfRefProbe))]
	[CrystalSerializable(typeof(EmptyContractProbe))]
	[CrystalSerializable(typeof(PocoProbe))]
	[CrystalSerializable(typeof(IgnoreProbe))]
	[CrystalSerializable(typeof(PrivateMemberProbe))]
	[CrystalSerializable(typeof(NamedGenericProbe<bool>))]
	[CrystalSerializable(typeof(KeyedBagProbe))]
	[CrystalSerializable(typeof(AnyTypeCollectionProbe))]
	public static partial class DcsProbeSerializers
	{
	}

	#endregion

}

