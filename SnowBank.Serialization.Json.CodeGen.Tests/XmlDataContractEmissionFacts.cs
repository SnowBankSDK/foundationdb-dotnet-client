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

namespace SnowBank.Serialization.Json.CodeGen.Tests.AcmeLegacy
{
	using System.Runtime.Serialization;
	using System.Text.Json.Serialization;
	using SnowBank.Data.Xml;

	#region Probe types...

	// note: these types mirror, family by family, the probe set of the design spike, whose wire was measured against a
	// LIVE DataContractSerializer. Every expected string in the fixture below is that measured output, quoted as such.
	// They are attributed in THIS project, which references the generator as an analyzer on itself, so each fact runs
	// the code the generator emitted for them.

	/// <summary>The nil truth table: null, empty and set, over every shape that renders differently when empty</summary>
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

	/// <summary>Explicit <c>Order</c> groups, which come AFTER the unordered members</summary>
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

	/// <summary>Every member of the base level, ordered rules included, comes before the derived level</summary>
	[DataContract]
	public sealed class OrderDerivedProbe : OrderBase
	{
		[DataMember] public string? AlphaFromDerived;
	}

	/// <summary>A member at its type's default is ABSENT under <c>EmitDefaultValue = false</c>, not nil</summary>
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

	/// <summary>Items named after the ITEM type's contract, including a nested list whose items are <c>ArrayOfstring</c></summary>
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
	}

	/// <summary>The <c>KeyValueOfKV</c> entry shape, over three key/value pairings</summary>
	[DataContract]
	public sealed class DictionaryProbe
	{
		[DataMember] public Dictionary<string, string>? PlainMap;
		[DataMember] public Dictionary<int, string>? IntKeyMap;
		[DataMember] public Dictionary<string, List<string>>? ListMap;
		[DataMember] public Dictionary<string, string>? EmptyMap;
	}

	/// <summary>A dictionary whose value type is not built-in: the reference wire hashes its entry name, this one does not</summary>
	[DataContract]
	public sealed class HashedDictionaryProbe
	{
		[DataMember] public Dictionary<string, Shelf>? ObjectMap;
	}

	[DataContract]
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

	/// <summary>The contract annotation: written when the runtime contract differs from the declared one, never otherwise</summary>
	[DataContract]
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

	/// <summary>A renamed contract and renamed members, which sort by their WIRE name</summary>
	[DataContract(Name = "RenamedContract", Namespace = "urn:acme:renamed")]
	public sealed class RenameProbe
	{
		[DataMember(Name = "renamed_member")] public string? Original;
		[DataMember(Name = "with-dash")] public string? Dashed;
		[DataMember(IsRequired = true)] public string? Required;
		[DataMember] public string? Plain;
	}

	public enum Medium { Print = 0, Audio = 1, Digital = 2 }

	/// <summary>A <c>[DataContract]</c> enum: only the <c>[EnumMember]</c> members are serializable</summary>
	[DataContract]
	public enum FlaggedMedium
	{
		[EnumMember(Value = "imprime")] Print = 0,
		[EnumMember] Audio = 1,
		Braille = 2,
	}

	[Flags]
	public enum Sections { None = 0, Adult = 1, Youth = 2, Heritage = 4 }

	/// <summary>One member per lexical family the DCS forms cover</summary>
	[DataContract]
	public sealed class ScalarProbe
	{
		[DataMember] public bool TrueBool;
		[DataMember] public bool FalseBool;
		[DataMember] public decimal Decimal;
		[DataMember] public decimal DecimalTrailingZero;
		[DataMember] public double Double;
		[DataMember] public double DoubleNaN;
		[DataMember] public double DoubleInfinity;
		[DataMember] public double DoubleExponent;
		[DataMember] public float Float;
		[DataMember] public DateTime DateUnspecified;
		[DataMember] public DateTime DateUtc;
		[DataMember] public DateTimeOffset DateOffset;
		[DataMember] public TimeSpan Duration;
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
		[DataMember] public string? ControlChars;
	}

	/// <summary>A generic contract name whose <c>{0}</c> expands to the argument's own contract name</summary>
	[DataContract(Name = "Envelope{0}")]
	public sealed class NamedGenericProbe<T>
	{
		[DataMember] public T Payload = default!;
	}

	/// <summary>Clean-room equivalent of the measured key-flattening wrapper: each dictionary KEY becomes a
	/// <see cref="SerializationInfo"/> entry name, so the wire emits one element per key</summary>
	[Serializable]
	public sealed class KeyedBag : ISerializable
	{

		private Dictionary<string, string> Inner { get; } = [ ];

		public void Add(string key, string value) => this.Inner.Add(key, value);

		void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
		{
			foreach (var kv in this.Inner)
			{
				info.AddValue(kv.Key, kv.Value);
			}
		}

	}

	[DataContract]
	public sealed class KeyedBagProbe
	{
		[DataMember] public KeyedBag? Properties;
	}

	/// <summary>An enum whose contract is RENAMED: the rename has to survive into every composed name, not just its own root</summary>
	[DataContract(Name = "Support")]
	public enum RenamedMedium
	{
		[EnumMember] Print = 0,
		[EnumMember(Value = "numerique")] Digital = 1,
	}

	/// <summary>A renamed enum as a collection item, as a dictionary key, and as a dictionary value</summary>
	[DataContract]
	public sealed class RenamedEnumProbe
	{
		[DataMember] public List<RenamedMedium>? Media;
		[DataMember] public Dictionary<RenamedMedium, string>? ByMedium;
		[DataMember] public Dictionary<string, RenamedMedium>? ToMedium;
		[DataMember] public RenamedMedium Single;
		[DataMember] public List<Medium>? Plain;
	}

	/// <summary>Holder of a nested type, which the DataContract wire names <c>Outer.Inner</c></summary>
	public static class Outer
	{

		[DataContract]
		public sealed class Inner
		{
			[DataMember] public string? Label;
		}

	}

	/// <summary>A generic type with NO declared name: the wire composes it as <c>BoxOf</c> + the arguments' contract names</summary>
	[DataContract]
	public sealed class Box<T>
	{
		// note: T (not T?), like NamedGenericProbe above: a T? member on an unconstrained T is a shape the JSON emission does not handle
		[DataMember] public T Payload = default!;
	}

	/// <summary>A contract name that needs escaping, so composing it into a generic name must not escape it TWICE</summary>
	[DataContract(Name = "with space")]
	public sealed class SpacedName
	{
		[DataMember] public string? Label;
	}

	/// <summary>A declared generic name carrying both placeholders: <c>{0}</c> expands, <c>{#}</c> (the digest) does not</summary>
	[DataContract(Name = "Digested{0}{#}")]
	public sealed class DigestedProbe<T>
	{
		[DataMember] public T Payload = default!;
	}

	/// <summary>Every composed contract name, as the ITEM name of a collection (the one place a composed name reaches the wire)</summary>
	[DataContract]
	public sealed class CompositionProbe
	{
		[DataMember] public Outer.Inner? Nested;
		[DataMember] public List<Outer.Inner>? NestedItems;
		[DataMember] public List<Box<string>>? Boxes;
		[DataMember] public List<Box<SpacedName>>? SpacedBoxes;
		[DataMember] public List<SpacedName>? Spaced;
	}

	/// <summary>A self-referencing contract: the shape whose CYCLIC instances have no representation on this wire either</summary>
	/// <remarks>The reference serializer answers a cycle with a <c>SerializationException</c>; this profile answers it with <c>CrystalXmlCycleException</c>.</remarks>
	[DataContract]
	public sealed class CycleNode
	{
		[DataMember] public string? Label;
		[DataMember] public CycleNode? Next;
	}

	[CrystalJsonConverter(CrystalJsonSerializerDefaults.DataContractCompat)]
	[CrystalXmlOutput]
	[CrystalJsonSerializable(typeof(CycleNode))]
	[CrystalJsonSerializable(typeof(NilProbe))]
	[CrystalJsonSerializable(typeof(Shelf))]
	[CrystalJsonSerializable(typeof(OrderExplicitProbe))]
	[CrystalJsonSerializable(typeof(OrderDerivedProbe))]
	[CrystalJsonSerializable(typeof(EmitDefaultProbe))]
	[CrystalJsonSerializable(typeof(CollectionProbe))]
	[CrystalJsonSerializable(typeof(DictionaryProbe))]
	[CrystalJsonSerializable(typeof(HashedDictionaryProbe))]
	[CrystalJsonSerializable(typeof(CatalogItem))]
	[CrystalJsonSerializable(typeof(AudioBook))]
	[CrystalJsonSerializable(typeof(PrintedBook))]
	[CrystalJsonSerializable(typeof(PolymorphicProbe))]
	[CrystalJsonSerializable(typeof(RenameProbe))]
	[CrystalJsonSerializable(typeof(ScalarProbe))]
	[CrystalJsonSerializable(typeof(NamedGenericProbe<bool>))]
	[CrystalJsonSerializable(typeof(KeyedBagProbe))]
	[CrystalJsonSerializable(typeof(RenamedEnumProbe))]
	[CrystalJsonSerializable(typeof(Outer.Inner))]
	[CrystalJsonSerializable(typeof(SpacedName))]
	[CrystalJsonSerializable(typeof(Box<string>))]
	// note: qualified, because inside this class body the generated nested holder LegacySerializers.SpacedName shadows the probe type
	[CrystalJsonSerializable(typeof(Box<SnowBank.Serialization.Json.CodeGen.Tests.AcmeLegacy.SpacedName>))]
	[CrystalJsonSerializable(typeof(DigestedProbe<string>))]
	[CrystalJsonSerializable(typeof(DigestedProbe<RenamedMedium>))]
	[CrystalJsonSerializable(typeof(CompositionProbe))]
	public static partial class LegacySerializers
	{
	}

	#endregion

}

namespace SnowBank.Serialization.Json.CodeGen.Tests
{
	using System.Buffers;
	using System.Globalization;
	using System.Text;
	using System.Xml;
	using System.Xml.Linq;
	using Microsoft.CodeAnalysis;
	using SnowBank.Data.Xml;
	using SnowBank.Serialization.Json.CodeGen.Tests.AcmeLegacy;

	/// <summary>Runs the code the generator emitted for the DATACONTRACT (compat) XML profile</summary>
	/// <remarks>
	/// <para>Every expected string below was produced by a LIVE <c>DataContractSerializer</c> writing through the
	/// namespace-stripping pipeline of the design spike, over the very same probe shape, and is quoted here as a literal.
	/// The live oracle itself lands in the next task; what this fixture pins is that the GENERATED code reproduces those
	/// documents structurally, family by family.</para>
	/// <para>Three strings deliberately differ from that measured output, and each has its own fact saying so: the
	/// unhashed dictionary entry names, the sanitized control characters, and the typed exceptions.</para>
	/// </remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class XmlDataContractEmissionFacts : SimpleTest
	{

		#region Nil, order, defaults...

		[Test]
		public void Test_Nil_Truth_Table()
		{
			var probe = new NilProbe
			{
				NullString = null,
				EmptyString = "",
				SetString = "value",
				NullNullableInt = null,
				SetNullableInt = 42,
				NullShelf = null,
				SetShelf = new Shelf { Label = "novels" },
				NullList = null,
				EmptyList = [ ],
				FullList = [ "a", "b" ],
				NullBytes = null,
				EmptyBytes = [ ],
			};

			string xml = LegacySerializers.NilProbe.ToXmlText(probe);
			Log($"XML : {xml}");

			// measured: a null is nil, an empty string forces the expanded form, an empty list and an empty byte array
			// both self-close, and the members are sorted by their wire name
			Assert.That(xml, Is.EqualTo("""<NilProbe><EmptyBytes /><EmptyList /><EmptyString></EmptyString><FullList><string>a</string><string>b</string></FullList><NullBytes nil="true" /><NullList nil="true" /><NullNullableInt nil="true" /><NullShelf nil="true" /><NullString nil="true" /><SetNullableInt>42</SetNullableInt><SetShelf><Label>novels</Label></SetShelf><SetString>value</SetString></NilProbe>"""));
		}

		[Test]
		public void Test_Member_Order_Explicit_Groups_Come_After_The_Unordered_Ones()
		{
			var probe = new OrderExplicitProbe { Alpha = "a3", Zulu = "z1", Mike = "m2", Bravo = "b2", NoOrderYankee = "y", NoOrderCharlie = "c" };

			string xml = LegacySerializers.OrderExplicitProbe.ToXmlText(probe);
			Log($"XML : {xml}");

			// measured: the two unordered members first (ordinal by wire name), then Order=1, then Order=2 with an
			// ordinal tie-break, then Order=3
			Assert.That(xml, Is.EqualTo("""<OrderExplicitProbe><NoOrderCharlie>c</NoOrderCharlie><NoOrderYankee>y</NoOrderYankee><Zulu>z1</Zulu><Bravo>b2</Bravo><Mike>m2</Mike><Alpha>a3</Alpha></OrderExplicitProbe>"""));
		}

		[Test]
		public void Test_Member_Order_Base_Level_Comes_First()
		{
			var probe = new OrderDerivedProbe { ZuluFromBase = "bz", OrderedFromBase = "bo", AlphaFromDerived = "da" };

			string xml = LegacySerializers.OrderDerivedProbe.ToXmlText(probe);
			Log($"XML : {xml}");

			// measured: the WHOLE base level (its ordered member included) precedes the derived level, so "Alpha" comes last
			Assert.That(xml, Is.EqualTo("""<OrderDerivedProbe><ZuluFromBase>bz</ZuluFromBase><OrderedFromBase>bo</OrderedFromBase><AlphaFromDerived>da</AlphaFromDerived></OrderDerivedProbe>"""));
		}

		[Test]
		public void Test_EmitDefaultValue_False_Omits_Default_And_Null()
		{
			string xml = LegacySerializers.EmitDefaultProbe.ToXmlText(new EmitDefaultProbe { SetInt = 7 });
			Log($"XML : {xml}");

			// measured: only the two EmitDefaultValue=true members and the one non-default value survive
			Assert.That(xml, Is.EqualTo("""<EmitDefaultProbe><KeptNullString nil="true" /><KeptZeroInt>0</KeptZeroInt><SetInt>7</SetInt></EmitDefaultProbe>"""));
		}

		[Test]
		public void Test_Without_Null_Members_Drops_The_Nil_Elements()
		{
			// the profile default is ShowNullMembers ON; a caller can turn it off, which is what an XSLT existence test sees
			string xml = LegacySerializers.NilProbe.ToXmlText(new NilProbe { SetString = "value" }, CrystalJsonSettings.Json.WithoutNullMembers());
			Log($"XML : {xml}");

			Assert.That(xml, Is.EqualTo("""<NilProbe><SetString>value</SetString></NilProbe>"""));
		}

		#endregion

		#region Collections and dictionaries...

		[Test]
		public void Test_Collection_Item_Element_Names()
		{
			var probe = new CollectionProbe
			{
				Strings = [ "s1", "s2" ],
				Ints = [ 1, 2, 3 ],
				StringArray = [ "arr1", "arr2" ],
				ShelfArray = [ new Shelf { Label = "sa1" } ],
				Shelves = [ new Shelf { Label = "sh1" }, new Shelf { Label = "sh2" } ],
				EmptyShelves = [ ],
				Nested = [ [ "x" ], [ "y", "z" ] ],
				Dates = [ new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Unspecified) ],
				WithNullItem = [ "present", null! ],
			};

			string xml = LegacySerializers.CollectionProbe.ToXmlText(probe);
			Log($"XML : {xml}");

			// measured: every item is named after the ITEM type's contract, a nested list is an ArrayOfstring item, an
			// empty collection self-closes, and a null item is nil
			Assert.That(xml, Is.EqualTo("""<CollectionProbe><Dates><dateTime>2026-01-02T03:04:05</dateTime></Dates><EmptyShelves /><Ints><int>1</int><int>2</int><int>3</int></Ints><Nested><ArrayOfstring><string>x</string></ArrayOfstring><ArrayOfstring><string>y</string><string>z</string></ArrayOfstring></Nested><ShelfArray><Shelf><Label>sa1</Label></Shelf></ShelfArray><Shelves><Shelf><Label>sh1</Label></Shelf><Shelf><Label>sh2</Label></Shelf></Shelves><StringArray><string>arr1</string><string>arr2</string></StringArray><Strings><string>s1</string><string>s2</string></Strings><WithNullItem><string>present</string><string nil="true" /></WithNullItem></CollectionProbe>"""));
		}

		[Test]
		public void Test_Dictionary_Entry_Shapes()
		{
			var probe = new DictionaryProbe
			{
				PlainMap = new() { ["k1"] = "v1", ["k2"] = "v2" },
				IntKeyMap = new() { [7] = "seven" },
				ListMap = new() { ["tags"] = [ "a", "b" ] },
				EmptyMap = [ ],
			};

			string xml = LegacySerializers.DictionaryProbe.ToXmlText(probe);
			Log($"XML : {xml}");

			// measured, MINUS the entry-name digest (deviation 1, pinned on its own below): the reference wire spells the
			// third entry <KeyValueOfstringArrayOfstringty7Ep6D1>
			Assert.That(xml, Is.EqualTo("""<DictionaryProbe><EmptyMap /><IntKeyMap><KeyValueOfintstring><Key>7</Key><Value>seven</Value></KeyValueOfintstring></IntKeyMap><ListMap><KeyValueOfstringArrayOfstring><Key>tags</Key><Value><string>a</string><string>b</string></Value></KeyValueOfstringArrayOfstring></ListMap><PlainMap><KeyValueOfstringstring><Key>k1</Key><Value>v1</Value></KeyValueOfstringstring><KeyValueOfstringstring><Key>k2</Key><Value>v2</Value></KeyValueOfstringstring></PlainMap></DictionaryProbe>"""));
		}

		[Test]
		public void Test_Deviation_1_Dictionary_Entry_Names_Carry_No_Digest()
		{
			var probe = new HashedDictionaryProbe { ObjectMap = new() { ["o1"] = new Shelf { Label = "ol1" } } };

			string xml = LegacySerializers.HashedDictionaryProbe.ToXmlText(probe);
			Log($"XML : {xml}");

			// ACTED DEVIATION 1. The reference wire appends an 8-character digest of the argument namespaces when one of
			// them is not built-in (measured: <KeyValueOfstringShelfQU_P9Vt29>, and the digest is not even stable across
			// assemblies). This wire emits the unhashed name: the algorithm is an undocumented internal, and no measured
			// consumer reads any KeyValueOf* element.
			Assert.That(xml, Is.EqualTo("""<HashedDictionaryProbe><ObjectMap><KeyValueOfstringShelf><Key>o1</Key><Value><Label>ol1</Label></Value></KeyValueOfstringShelf></ObjectMap></HashedDictionaryProbe>"""));
			Assert.That(xml, Does.Not.Contain("KeyValueOfstringShelfQ"), "the digest must not come back");
		}

		#endregion

		#region Polymorphism and naming...

		[Test]
		public void Test_An_Instance_Of_A_Concrete_Polymorphic_Root_Writes_Its_Own_Body()
		{
			// DELIBERATE DIVERGENCE from the modern wire, which refuses this exact value with
			// CrystalXmlUnknownTypeException. Here the live DCS oracle writes the root's own body, unannotated (the
			// runtime contract IS the declared one), and byte compatibility is what this profile exists for.
			Assert.That(
				LegacySerializers.CatalogItem.ToXmlText(new CatalogItem { Title = "Generic" }),
				Is.EqualTo("""<CatalogItem><Title>Generic</Title></CatalogItem>"""));
		}

		[Test]
		public void Test_Polymorphism_Annotates_The_Runtime_Contract()
		{
			var probe = new PolymorphicProbe
			{
				DeclaredBaseHoldingBase = new CatalogItem { Title = "Generic" },
				DeclaredBaseHoldingDerived = new AudioBook { Title = "Heard", DurationMinutes = 90 },
				DeclaredExact = new AudioBook { Title = "Exact", DurationMinutes = 5 },
				Zoo = [ new AudioBook { Title = "A", DurationMinutes = 1 }, new PrintedBook { Title = "P", Isbn = "i" }, new CatalogItem { Title = "Plain" } ],
				AsObjectString = "boxed string",
				AsObjectInt = 123,
				AsObjectLong = 123L,
				AsObjectNull = null,
			};

			string xml = LegacySerializers.PolymorphicProbe.ToXmlText(probe);
			Log($"XML : {xml}");

			// measured: type="AudioBook" when a derived instance sits in a base-declared slot, NOTHING when the declared
			// type is exact, the items of the list are named after the DECLARED item contract, and a boxed primitive in an
			// object slot carries the contract of its own runtime type
			Assert.That(xml, Is.EqualTo("""<PolymorphicProbe><AsObjectInt type="int">123</AsObjectInt><AsObjectLong type="long">123</AsObjectLong><AsObjectNull nil="true" /><AsObjectString type="string">boxed string</AsObjectString><DeclaredBaseHoldingBase><Title>Generic</Title></DeclaredBaseHoldingBase><DeclaredBaseHoldingDerived type="AudioBook"><Title>Heard</Title><DurationMinutes>90</DurationMinutes></DeclaredBaseHoldingDerived><DeclaredExact><Title>Exact</Title><DurationMinutes>5</DurationMinutes></DeclaredExact><Zoo><CatalogItem type="AudioBook"><Title>A</Title><DurationMinutes>1</DurationMinutes></CatalogItem><CatalogItem type="PrintedBook"><Title>P</Title><Isbn>i</Isbn></CatalogItem><CatalogItem><Title>Plain</Title></CatalogItem></Zoo></PolymorphicProbe>"""));
		}

		[Test]
		public void Test_Renamed_Contract_And_Members()
		{
			string xml = LegacySerializers.RenameProbe.ToXmlText(new RenameProbe { Original = "o", Dashed = "d", Required = "r", Plain = "p" });
			Log($"XML : {xml}");

			// measured: the root takes the contract name, and the members sort by their WIRE name in ORDINAL order, which
			// puts the two capitalized ones before the two lowercase ones
			Assert.That(xml, Is.EqualTo("""<RenamedContract><Plain>p</Plain><Required>r</Required><renamed_member>o</renamed_member><with-dash>d</with-dash></RenamedContract>"""));
		}

		[Test]
		public void Test_Named_Generic_Expands_Its_Braces()
		{
			string xml = LegacySerializers.NamedGenericProbe_Boolean.ToXmlText(new NamedGenericProbe<bool> { Payload = true });
			Log($"XML : {xml}");

			// measured: [DataContract(Name = "Envelope{0}")] on a generic type expands {0} to the ARGUMENT's contract name
			Assert.That(xml, Is.EqualTo("""<Envelopeboolean><Payload>true</Payload></Envelopeboolean>"""));
		}

		[Test]
		public void Test_A_Renamed_Enum_Contract_Names_The_Items_And_The_Dictionary_Entries()
		{
			var probe = new RenamedEnumProbe
			{
				Media = [ RenamedMedium.Print, RenamedMedium.Digital ],
				ByMedium = new() { [RenamedMedium.Digital] = "d" },
				ToMedium = new() { ["k"] = RenamedMedium.Print },
				Single = RenamedMedium.Digital,
				Plain = [ Medium.Digital ],
			};

			string xml = LegacySerializers.RenamedEnumProbe.ToXmlText(probe);
			Log($"XML : {xml}");

			// measured, MINUS the entry-name digest (deviation 1): the reference wire spells the two entries
			// <KeyValueOfSupportstringVjK_S7bsW> and <KeyValueOfstringSupport4kHBl5Pd>. [DataContract(Name = "Support")]
			// on the ENUM renames it everywhere its contract name is composed: as a collection item, and on both sides of
			// a KeyValueOfXY. An enum with no [DataContract] keeps its declaration name (<Medium>).
			Assert.That(xml, Is.EqualTo("""<RenamedEnumProbe><ByMedium><KeyValueOfSupportstring><Key>numerique</Key><Value>d</Value></KeyValueOfSupportstring></ByMedium><Media><Support>Print</Support><Support>numerique</Support></Media><Plain><Medium>Digital</Medium></Plain><Single>numerique</Single><ToMedium><KeyValueOfstringSupport><Key>k</Key><Value>Print</Value></KeyValueOfstringSupport></ToMedium></RenamedEnumProbe>"""));
		}

		[Test]
		public void Test_Composed_Contract_Names()
		{
			var probe = new CompositionProbe
			{
				Nested = new() { Label = "n" },
				NestedItems = [ new() { Label = "a" } ],
				Boxes = [ new() { Payload = "s" } ],
				SpacedBoxes = [ new() { Payload = new() { Label = "b" } } ],
				Spaced = [ new() { Label = "i" } ],
			};

			string xml = LegacySerializers.CompositionProbe.ToXmlText(probe);
			Log($"XML : {xml}");

			// measured, MINUS the digest the reference wire appends to a generic name whose argument is not built-in
			// (<BoxOfwith_x0020_spaceCmwZw7JZ>), which is the same deviation 1 as the dictionary entry names. A nested type
			// is "Outer.Inner", a generic with no declared name is "BoxOf" + its arguments, and a contract name that needs
			// escaping is escaped EXACTLY ONCE when it is composed into one ("with_x0020_space", never "with_x005F_x0020_space").
			Assert.That(xml, Is.EqualTo("""<CompositionProbe><Boxes><BoxOfstring><Payload>s</Payload></BoxOfstring></Boxes><Nested><Label>n</Label></Nested><NestedItems><Outer.Inner><Label>a</Label></Outer.Inner></NestedItems><Spaced><with_x0020_space><Label>i</Label></with_x0020_space></Spaced><SpacedBoxes><BoxOfwith_x0020_space><Payload><Label>b</Label></Payload></BoxOfwith_x0020_space></SpacedBoxes></CompositionProbe>"""));
		}

		[Test]
		public void Test_Composed_Contract_Names_As_Roots()
		{
			string nested = LegacySerializers.Inner.ToXmlText(new Outer.Inner { Label = "x" });
			string boxed = LegacySerializers.Box_String.ToXmlText(new Box<string> { Payload = "s" });
			string spaced = LegacySerializers.SpacedName.ToXmlText(new SpacedName { Label = "l" });
			string digested = LegacySerializers.DigestedProbe_String.ToXmlText(new DigestedProbe<string> { Payload = "s" });
			string digestedEnum = LegacySerializers.DigestedProbe_RenamedMedium.ToXmlText(new DigestedProbe<RenamedMedium> { Payload = RenamedMedium.Digital });

			Log($"nested       : {nested}");
			Log($"boxed        : {boxed}");
			Log($"spaced       : {spaced}");
			Log($"digested     : {digested}");
			Log($"digested enum: {digestedEnum}");

			using (Assert.EnterMultipleScope())
			{
				// measured: the root takes the same composed contract name the item elements above carry
				Assert.That(nested, Is.EqualTo("""<Outer.Inner><Label>x</Label></Outer.Inner>"""), "a nested type is named after its declaration chain");
				Assert.That(boxed, Is.EqualTo("""<BoxOfstring><Payload>s</Payload></BoxOfstring>"""), "a generic with no declared name composes XOfY");
				Assert.That(spaced, Is.EqualTo("""<with_x0020_space><Label>l</Label></with_x0020_space>"""), "a declared name that is not an XML name is encoded, not refused");
				Assert.That(digested, Is.EqualTo("""<Digestedstring><Payload>s</Payload></Digestedstring>"""), "{0} expands to the argument's contract name, {#} to nothing");
				// deviation 1 again: the reference wire writes <DigestedSupportCmwZw7JZ>, because {#} asks for the digest
				Assert.That(digestedEnum, Is.EqualTo("""<DigestedSupport><Payload>numerique</Payload></DigestedSupport>"""), "{0} reads the ENUM's renamed contract, and {#} stays empty");
			}
		}

		[Test]
		public void Test_A_Null_Root_Is_A_Nil_Element()
		{
			string xml = LegacySerializers.Shelf.ToXmlText(null);
			Log($"XML : {xml}");

			Assert.That(xml, Is.EqualTo("""<Shelf nil="true" />"""));
		}

		[Test]
		public void Test_A_Null_Root_Without_Null_Members_Is_An_Empty_Element()
		{
			// NOT a fidelity claim: the reference wire has no equivalent of WithoutNullMembers and always writes the nil
			// attribute. What is pinned here is that the setting reaches the ROOT too, and that a document is still
			// produced (an empty output would be unparseable, which is worse than a nil-less element)
			string xml = LegacySerializers.Shelf.ToXmlText(null, CrystalJsonSettings.Json.WithoutNullMembers());
			Log($"XML : {xml}");

			Assert.That(xml, Is.EqualTo("""<Shelf />"""));
		}

		[Test]
		public void Test_The_Root_Name_Can_Be_Overridden()
		{
			string xml = LegacySerializers.Shelf.ToXmlText(new Shelf { Label = "x" }, rootName: "data");
			Log($"XML : {xml}");

			Assert.That(xml, Is.EqualTo("""<data><Label>x</Label></data>"""));
		}

		#endregion

		#region Scalars...

		private static ScalarProbe MakeScalarProbe() => new()
		{
			TrueBool = true,
			FalseBool = false,
			Decimal = 12.34m,
			DecimalTrailingZero = 1.50m,
			Double = 1.5,
			DoubleNaN = double.NaN,
			DoubleInfinity = double.PositiveInfinity,
			DoubleExponent = 1.2e-9,
			Float = 2.5f,
			DateUnspecified = new DateTime(2026, 8, 3, 14, 5, 6, 789, DateTimeKind.Unspecified),
			DateUtc = new DateTime(2026, 8, 3, 14, 5, 6, 789, DateTimeKind.Utc),
			DateOffset = new DateTimeOffset(2026, 8, 3, 14, 5, 6, 789, TimeSpan.FromHours(2)),
			Duration = new TimeSpan(1, 33, 30),
			Guid = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"),
			Char = 'A',
			Bytes = [ 0xDE, 0xAD, 0xBE, 0xEF ],
			EnumValue = Medium.Digital,
			EnumRenamed = FlaggedMedium.Print,
			FlagsCombo = Sections.Adult | Sections.Heritage,
			Uri = new Uri("https://acme.example/a b?x=1&y=2"),
			Long = 9007199254740993L,
			UnsignedLong = ulong.MaxValue,
			Short = -12,
			UnsignedByte = 200,
			SignedByte = -100,
			SpecialChars = "<tag> & \"quote\" 'apos' ]]>",
			Newlines = "line1\r\nline2\ttab",
			ControlChars = "beforeafter",
		};

		[Test]
		public void Test_Scalar_Lexical_Forms()
		{
			string xml = LegacySerializers.ScalarProbe.ToXmlText(MakeScalarProbe());
			Log($"XML : {xml}");

			// measured: char as its code point (65), decimal keeping its scale, double in the "R" forms (1.2E-09 / INF /
			// NaN), DateTime by Kind, DateTimeOffset as the two-member structure, TimeSpan as an ISO 8601 duration, an enum
			// by its [EnumMember] token, a flags combination joined by a space, byte[] as base64, a Uri percent-escaped
			Assert.That(xml, Is.EqualTo("<ScalarProbe><Bytes>3q2+7w==</Bytes><Char>65</Char><ControlChars>beforeafter</ControlChars><DateOffset><DateTime>2026-08-03T12:05:06.789Z</DateTime><OffsetMinutes>120</OffsetMinutes></DateOffset><DateUnspecified>2026-08-03T14:05:06.789</DateUnspecified><DateUtc>2026-08-03T14:05:06.789Z</DateUtc><Decimal>12.34</Decimal><DecimalTrailingZero>1.50</DecimalTrailingZero><Double>1.5</Double><DoubleExponent>1.2E-09</DoubleExponent><DoubleInfinity>INF</DoubleInfinity><DoubleNaN>NaN</DoubleNaN><Duration>PT1H33M30S</Duration><EnumRenamed>imprime</EnumRenamed><EnumValue>Digital</EnumValue><FalseBool>false</FalseBool><FlagsCombo>Adult Heritage</FlagsCombo><Float>2.5</Float><Guid>0f8fad5b-d9cb-469f-a165-70867728950e</Guid><Long>9007199254740993</Long><Newlines>line1\r\nline2\ttab</Newlines><Short>-12</Short><SignedByte>-100</SignedByte><SpecialChars>&lt;tag&gt; &amp; \"quote\" 'apos' ]]&gt;</SpecialChars><TrueBool>true</TrueBool><UnsignedByte>200</UnsignedByte><UnsignedLong>18446744073709551615</UnsignedLong><Uri>https://acme.example/a%20b?x=1&amp;y=2</Uri></ScalarProbe>"));
		}

		[Test]
		public void Test_Deviation_2_Control_Characters_Are_Sanitized_At_The_Value()
		{
			var probe = MakeScalarProbe();
			probe.ControlChars = "beforeafter";

			string xml = LegacySerializers.ScalarProbe.ToXmlText(probe);
			Log($"XML : {xml}");

			// ACTED DEVIATION 2. The reference wire writes <ControlChars>before&#x1;&#x8;after</ControlChars>, which no
			// conformant XML reader accepts (its post-filter runs on the ALREADY escaped text and so misses them). Here the
			// characters are dropped at the value level: a document that fails to parse today cannot regress.
			Assert.That(xml, Does.Contain("<ControlChars>beforeafter</ControlChars>"));
			Assert.That(xml, Does.Not.Contain("&#x1;"));
		}

		#endregion

		#region The ISerializable dialect...

		[Test]
		public void Test_ISerializable_Dialect_Names_Elements_After_The_Entry_Keys()
		{
			var probe = new KeyedBagProbe { Properties = new KeyedBag() };
			probe.Properties.Add("origin", "acme-main");
			probe.Properties.Add("channel", "web");
			probe.Properties.Add("not a name", "escaped");

			string xml = LegacySerializers.KeyedBagProbe.ToXmlText(probe);
			Log($"XML : {xml}");

			// measured: each SerializationInfo entry is an element NAMED AFTER THE KEY, in insertion order, and the value
			// is declared as anyType so it carries its own contract. A key that is not an XML name is escaped by
			// XmlConvert.EncodeLocalName, exactly as the reference serializer escapes it.
			Assert.That(xml, Is.EqualTo("""<KeyedBagProbe><Properties><origin type="string">acme-main</origin><channel type="string">web</channel><not_x0020_a_x0020_name type="string">escaped</not_x0020_a_x0020_name></Properties></KeyedBagProbe>"""));
		}

		#endregion

		#region Typed refusals (acted deviation 3)...

		[Test]
		public void Test_Deviation_3_An_Undeclared_Enum_Value_Raises_A_Typed_Exception()
		{
			var probe = MakeScalarProbe();
			probe.EnumRenamed = FlaggedMedium.Braille; // declared in C#, but with no [EnumMember] on a [DataContract] enum

			// ACTED DEVIATION 3: the reference serializer raises SerializationException here ("Enum value 'Braille' is
			// invalid for type ... and cannot be serialized"); this wire raises its own typed exception instead.
			Assert.That(
				() => LegacySerializers.ScalarProbe.ToXmlText(probe),
				Throws.InstanceOf<CrystalXmlNotSupportedException>().With.Message.Contains("FlaggedMedium"));
		}

		[Test]
		public void Test_Deviation_3_An_Undeclared_Flags_Combination_Raises_A_Typed_Exception()
		{
			var probe = MakeScalarProbe();
			probe.FlagsCombo = (Sections) 8; // a bit no declared member covers

			Assert.That(
				() => LegacySerializers.ScalarProbe.ToXmlText(probe),
				Throws.InstanceOf<CrystalXmlNotSupportedException>().With.Message.Contains("Sections"));
		}

		[Test]
		public void Test_A_Runtime_Type_The_Container_Cannot_Name_Is_Refused_In_An_AnyType_Slot()
		{
			// the anyType switch is closed at generation time (the lexical types, plus this container's own types): a
			// value outside it has no contract name this emission can compute, so it fails loudly instead of guessing
			var probe = new PolymorphicProbe { AsObjectString = new List<string> { "a" } };

			Assert.That(
				() => LegacySerializers.PolymorphicProbe.ToXmlText(probe),
				Throws.InstanceOf<CrystalXmlNotSupportedException>().With.Message.Contains("AsObjectString"));
		}

		/// <summary>Runs the generator over a probe and returns the <c>#error</c> directives its output carries</summary>
		/// <remarks>An <c>#error</c> is not a generator diagnostic: it only exists once the emitted source is compiled, where it
		/// surfaces as CS1029. A probe that carries one cannot live in this fixture's own container, which has to keep building.</remarks>
		private static List<string> EmissionErrorsOf(string source)
		{
			var compilation = GeneratorProbeHarness.Compile("namespace Probe\n{\n" + source + "\n}\n");
			Assert.That(
				compilation.GetDiagnostics().Where(static d => d.Severity >= DiagnosticSeverity.Warning),
				Is.Empty,
				"the probe source must compile clean on its own");

			var (output, _) = GeneratorProbeHarness.RunGenerator(compilation);
			var errors = output.GetDiagnostics().Where(static d => d.Id == "CS1029").Select(static d => d.GetMessage()).ToList();
			foreach (var error in errors)
			{
				Log($"#error: {error}");
			}
			return errors;
		}

		[Test]
		public void Test_A_Generic_Argument_That_Is_Itself_A_Closed_Generic_Fails_The_Build()
		{
			// a type argument reaches the emitter as a TypeRef, which carries no element type: naming Envelope<List<string>>
			// would produce "EnvelopeList" where the reference wire writes "EnvelopeArrayOfstring". A wrong name is the
			// silent divergence this profile exists to prevent, so the emission refuses instead of guessing
			var errors = EmissionErrorsOf("""
					[System.Runtime.Serialization.DataContract]
					public sealed class ProbeDto<T>
					{
						[System.Runtime.Serialization.DataMember]
						public T? Payload { get; set; }
					}

					[SnowBank.Data.Json.CrystalJsonConverter(SnowBank.Data.Json.CrystalJsonSerializerDefaults.DataContractCompat)]
					[SnowBank.Data.Xml.CrystalXmlOutput]
					[SnowBank.Data.Json.CrystalJsonSerializable(typeof(ProbeDto<System.Collections.Generic.List<string>>))]
					public static partial class ProbeConverters
					{
					}
				""");

			Assert.That(errors, Has.Exactly(1).Contains("ProbeDto"), "the message names the container whose contract name cannot be derived");
		}

		[Test]
		public void Test_The_AnyType_Switch_Of_An_Undeclared_Hierarchy_Still_Compiles()
		{
			// two registered types in an UNDECLARED base/derived relationship (no [JsonDerivedType], so neither is part of a
			// polymorphic map) both take a case in the switch of an object-typed slot. A case on the BASE emitted first
			// captures the subclass, which makes the subclass's own case unreachable: CS8120, i.e. a build failure of the
			// generated code, from source that is perfectly legal. The cases are therefore emitted most-derived-first.
			var compilation = GeneratorProbeHarness.Compile("""
				namespace Probe
				{

					[System.Runtime.Serialization.DataContract]
					public class ProbeBase
					{
						[System.Runtime.Serialization.DataMember]
						public string? Title { get; set; }
					}

					[System.Runtime.Serialization.DataContract]
					public sealed class ProbeDerived : ProbeBase
					{
						[System.Runtime.Serialization.DataMember]
						public int Extra { get; set; }
					}

					[System.Runtime.Serialization.DataContract]
					public sealed class ProbeDto
					{
						[System.Runtime.Serialization.DataMember]
						public object? Slot { get; set; }
					}

					[SnowBank.Data.Json.CrystalJsonConverter(SnowBank.Data.Json.CrystalJsonSerializerDefaults.DataContractCompat)]
					[SnowBank.Data.Xml.CrystalXmlOutput]
					[SnowBank.Data.Json.CrystalJsonSerializable(typeof(ProbeBase))]
					[SnowBank.Data.Json.CrystalJsonSerializable(typeof(ProbeDerived))]
					[SnowBank.Data.Json.CrystalJsonSerializable(typeof(ProbeDto))]
					public static partial class ProbeConverters
					{
					}

				}
				""");

			var (output, _) = GeneratorProbeHarness.RunGenerator(compilation);
			var errors = output.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error).Select(static d => $"{d.Id}: {d.GetMessage()}").ToList();
			foreach (var error in errors)
			{
				Log($"generated: {error}");
			}

			Assert.That(errors, Is.Empty, "the generated code must compile");
		}

		#endregion

		#region Outputs and the JSON side...

		[Test]
		public void Test_All_The_Outputs_Agree()
		{
			var probe = new NilProbe { SetString = "value", FullList = [ "a" ], EmptyBytes = [ ] };

			string text = LegacySerializers.NilProbe.ToXmlText(probe);
			var slice = LegacySerializers.NilProbe.ToXmlSlice(probe);
			byte[] bytes = LegacySerializers.NilProbe.ToXmlBytes(probe);

			var ms = new MemoryStream();
			LegacySerializers.NilProbe.WriteXmlTo(ms, probe);

			var sw = new StringWriter();
			LegacySerializers.NilProbe.WriteXmlTo(sw, probe);

			var buffer = new ArrayBufferWriter<byte>();
			LegacySerializers.NilProbe.WriteXmlTo(buffer, probe);

			var doc = LegacySerializers.NilProbe.ToXDocument(probe);

			var xmlOut = new StringBuilder();
			using (var writer = XmlWriter.Create(xmlOut, new XmlWriterSettings() { OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment }))
			{
				LegacySerializers.NilProbe.WriteXmlTo(writer, probe);
			}

			Log($"text : {text}");

			using (Assert.EnterMultipleScope())
			{
				Assert.That(slice.ToStringUtf8(), Is.EqualTo(text), "the byte core, decoded");
				Assert.That(Encoding.UTF8.GetString(bytes), Is.EqualTo(text), "the byte array");
				Assert.That(Encoding.UTF8.GetString(ms.ToArray()), Is.EqualTo(text), "the stream");
				Assert.That(sw.ToString(), Is.EqualTo(text), "the text writer");
				Assert.That(Encoding.UTF8.GetString(buffer.WrittenSpan), Is.EqualTo(text), "the buffer writer");

				// the two infoset outputs promise equivalence, not bytes, so they are compared as trees
				Assert.That(XNode.DeepEquals(doc.Root, XDocument.Parse(text).Root), Is.True, "the XDocument, as a tree");
				Assert.That(XNode.DeepEquals(XDocument.Parse(xmlOut.ToString()).Root, XDocument.Parse(text).Root), Is.True, "the XmlWriter output, as a tree");
			}
		}

		[Test]
		public void Test_The_Json_Wire_Of_The_Same_Container_Is_Untouched()
		{
			// the two wires disagree on purpose where the two PROFILES disagree: the DCJS JSON writes an enum as a number
			// and a date in the Microsoft form, while the XML of the same value writes the enum's label and the ISO form.
			// The char is the opposite case: both wires spell it as its code point, so it happens to agree.
			var probe = new ScalarProbe { EnumValue = Medium.Digital, Char = 'A' };

			string json = LegacySerializers.ScalarProbe.ToJsonText(probe);
			string xml = LegacySerializers.ScalarProbe.ToXmlText(probe);
			Log($"JSON : {json}");

			using (Assert.EnterMultipleScope())
			{
				Assert.That(json, Does.Contain("\"EnumValue\": 2"), "the DCJS JSON writes an enum as a number");
				Assert.That(xml, Does.Contain("<EnumValue>Digital</EnumValue>"), "the DCS XML writes its label");
				Assert.That(json, Does.Contain("\"Char\": 65"), "both wires spell a char as its code point");
				Assert.That(xml, Does.Contain("<Char>65</Char>"));
				Assert.That(json, Does.Contain("\"DateUtc\": \"\\/Date("), "the DCJS JSON writes a date in the Microsoft form");
				Assert.That(xml, Does.Contain("<DateUtc>0001-01-01T00:00:00</DateUtc>"), "the DCS XML writes it in the ISO form");
			}
		}

		#endregion

		#region Cycles and depth...

		/// <summary>Builds an ACYCLIC chain of <paramref name="length"/> nodes, so the deepest element sits at depth <c>length - 1</c></summary>
		private static CycleNode MakeChain(int length)
		{
			var head = new CycleNode();
			var tail = head;
			for (int i = 1; i < length; i++)
			{
				var next = new CycleNode();
				tail.Next = next;
				tail = next;
			}
			return head;
		}

		private static int CountOf(string text, string token)
		{
			int n = 0;
			for (int i = text.IndexOf(token, StringComparison.Ordinal); i >= 0; i = text.IndexOf(token, i + token.Length, StringComparison.Ordinal))
			{
				++n;
			}
			return n;
		}

		[Test]
		public void Test_A_Reference_Cycle_Throws_Instead_Of_Overflowing_The_Stack()
		{
			// the compat wire has no z:Id/z:Ref form here either, so a cycle is a typed, CATCHABLE error: before the depth
			// guard existed this recursed until the native stack gave out, taking the whole process with it
			var a = new CycleNode { Label = "a" };
			var b = new CycleNode { Label = "b" };
			a.Next = b;
			b.Next = a;

			Assert.That(
				() => LegacySerializers.CycleNode.ToXmlText(a),
				Throws.InstanceOf<CrystalXmlCycleException>().With.Property("Type").EqualTo(typeof(CycleNode)).And.Message.Contains(CrystalXml.MaxDepth.ToString(CultureInfo.InvariantCulture)),
				"the exception names the type the cycle was detected on, and the cap it hit");
		}

		[Test]
		public void Test_A_Deep_Acyclic_Chain_Up_To_The_Cap_Is_Written_In_Full()
		{
			string xml = LegacySerializers.CycleNode.ToXmlText(MakeChain(CrystalXml.MaxDepth));

			using (Assert.EnterMultipleScope())
			{
				Assert.That(xml, Does.StartWith("<CycleNode"), "the root element");
				Assert.That(CountOf(xml, "<Next"), Is.EqualTo(CrystalXml.MaxDepth), "one nested element per node past the root, plus the nil marker the deepest node writes for its own null Next (this profile spells a null member out)");
			}
		}

		[Test]
		public void Test_A_Deep_Acyclic_Chain_Past_The_Cap_Throws_The_Same_Typed_Exception()
		{
			Assert.That(
				() => LegacySerializers.CycleNode.ToXmlText(MakeChain(CrystalXml.MaxDepth + 1)),
				Throws.InstanceOf<CrystalXmlCycleException>().With.Property("Type").EqualTo(typeof(CycleNode)));
		}

		#endregion

	}

}
