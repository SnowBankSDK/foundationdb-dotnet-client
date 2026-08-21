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

namespace SnowBank.Data.Xml.Tests
{
	using System.Runtime.Serialization;
	using NUnit.Framework;
	using SnowBank.Buffers.Text;
	using SnowBank.Data.Xml;
	using SnowBank.Data.Xml.Tests.Acme;

	/// <summary>
	/// Stage-A equivalence oracle: every test serializes the same instance through the LIVE
	/// <see cref="System.Runtime.Serialization.DataContractSerializer"/> reference pipeline (<see cref="ReferenceDcsWire"/>)
	/// and through the generated CrystalXml DataContract-compat container (<see cref="DcsProbeSerializers"/>), and asserts
	/// byte equality. The three acted, deliberate divergences (unhashed dictionary entry names, sanitized control
	/// characters, typed exceptions in place of the reference serializer's own exception types) get their own pinning
	/// test instead of a plain equality assertion.
	/// </summary>
	/// <remarks>Structured family by family, the fact per probe family.</remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-XML")]
	public sealed class DcsWireFidelityFacts : SimpleTest
	{

		/// <summary>Compares BOTH outputs of the DataContract profile against the live-DCS oracle in its two modes</summary>
		/// <param name="value">Instance to serialize</param>
		/// <param name="toXmlText">The default output, which carries contract namespaces</param>
		/// <param name="toSchemalessXmlText">The output under <c>Schemaless = true</c>, which carries none</param>
		/// <remarks>
		/// <para>Two outputs, two acceptance rules, one instance and one oracle:</para>
		/// <list type="bullet">
		/// <item>the DEFAULT output is compared to the unstripped wire on EXPANDED NAMES. Byte equality is the wrong rule
		/// there: this emission omits the declarations it can prove unused and writes the rest on the first element that needs
		/// them, so its bytes differ from the reference serializer's while every element and attribute resolves to the same
		/// (namespace, local name) pair. That is what a reader sees, and it is what is asserted.</item>
		/// <item>the SCHEMALESS output is compared to the stripped wire BYTE FOR BYTE. There is nothing to normalize away
		/// once the namespaces are gone, and those bytes are what the stored documents of a consuming application contain.</item>
		/// </list>
		/// <para>Both wires are logged, so a failure on either rule is read against the other two.</para>
		/// </remarks>
		private static void AssertSameWire<T>(T? value, Func<T?, string> toXmlText, Func<T?, string> toSchemalessXmlText)
		{
			string standard = ReferenceDcsWire.Serialize(value, typeof(T), strip: false);
			string actual = toXmlText(value);
			Log("reference (standard) : " + standard);
			Log("generated (default)  : " + actual);
			XmlExpandedNameComparison.AssertEquivalent(standard, actual, $"The default DataContract output of {typeof(T).Name} must be expanded-name equivalent to the standard wire.");

			string stripped = ReferenceDcsWire.Serialize(value, typeof(T), strip: true);
			string schemaless = toSchemalessXmlText(value);
			Log("reference (stripped) : " + stripped);
			Log("generated (schemaless): " + schemaless);
			Assert.That(schemaless, Is.EqualTo(stripped), "The schemaless DataContract output must stay byte-identical to the stripped wire.");
		}

		#region Nil, order, defaults...

		[Test]
		public void Test_Nil_Truth_Table()
		{
			// Nulls become <X nil="true" />, empty string becomes <X></X>, empty collections and empty byte arrays
			// self-close, set values render normally.
			AssertSameWire(new NilProbe
			{
				NullString = null,
				EmptyString = "",
				SetString = "value",
				NullNullableInt = null,
				SetNullableInt = 42,
				NullShelf = null,
				SetShelf = new Shelf { Label = "novels" },
				NullList = null,
				EmptyList = [],
				FullList = ["a", "b"],
				NullBytes = null,
				EmptyBytes = [],
			}, v => DcsProbeSerializers.NilProbe.ToXmlText(v), v => DcsProbeSchemalessSerializers.NilProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Member_Order_Default_Is_Ordinal_Alphabetical()
		{
			// Declared Zulu, Alpha, Mike, Bravo; the output emits Alpha, Bravo, Mike, Zulu.
			AssertSameWire(new OrderDefaultProbe { Zulu = "z", Alpha = "a", Mike = "m", Bravo = "b" }, v => DcsProbeSerializers.OrderDefaultProbe.ToXmlText(v), v => DcsProbeSchemalessSerializers.OrderDefaultProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Member_Order_Explicit_Groups_After_Unordered()
		{
			// Expected format order: NoOrderCharlie, NoOrderYankee, Zulu(1), Bravo(2), Mike(2), Alpha(3).
			AssertSameWire(new OrderExplicitProbe
			{
				Alpha = "a3", Zulu = "z1", Mike = "m2", Bravo = "b2", NoOrderYankee = "y", NoOrderCharlie = "c",
			}, v => DcsProbeSerializers.OrderExplicitProbe.ToXmlText(v), v => DcsProbeSchemalessSerializers.OrderExplicitProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Member_Order_Base_Level_Comes_First()
		{
			// Base members (including its ordered ones) come before derived members.
			AssertSameWire(new OrderDerivedProbe { ZuluFromBase = "bz", OrderedFromBase = "bo", AlphaFromDerived = "da" }, v => DcsProbeSerializers.OrderDerivedProbe.ToXmlText(v), v => DcsProbeSchemalessSerializers.OrderDerivedProbe.ToXmlText(v));
		}

		[Test]
		public void Test_EmitDefaultValue_False_Omits_Default_And_Null()
		{
			// Only KeptZeroInt (0), KeptNullString (nil) and SetInt (7) appear in the output.
			AssertSameWire(new EmitDefaultProbe { SetInt = 7 }, v => DcsProbeSerializers.EmitDefaultProbe.ToXmlText(v), v => DcsProbeSchemalessSerializers.EmitDefaultProbe.ToXmlText(v));
		}

		[Test]
		public void Test_A_Json_Rename_On_A_Plain_Dto_Does_Not_Rename_The_Xml_Element()
		{
			// the reference serializer reads the data contract and nothing else, and a plain DTO has none: the element
			// keeps the declared member name whatever the JSON attribute spells
			AssertSameWire(new PocoJsonRenamedProbe { SubscriptionCode = "sc-1", Label = "l" }, v => DcsProbeSerializers.PocoJsonRenamedProbe.ToXmlText(v), v => DcsProbeSchemalessSerializers.PocoJsonRenamedProbe.ToXmlText(v));
		}

		[Test]
		public void Test_A_Json_Rename_On_A_Plain_Dto_Still_Names_The_Json_Member()
		{
			// the other half of the same rule: the JSON output keeps the name its own attribute gives it
			string json = DcsProbeSerializers.PocoJsonRenamedProbe.ToJsonText(new PocoJsonRenamedProbe { SubscriptionCode = "sc-1", Label = "l" });
			Log(json);

			Assert.That(json, Does.Contain("\"SUBSCRIPTION_CODE\""), "the [JsonProperty] name names the JSON member");
			Assert.That(json, Does.Not.Contain("\"SubscriptionCode\""), "the declared member name is the XML name, not the JSON one");
		}

		[Test]
		public void Test_An_Overridden_Member_Is_Written_At_The_Level_That_Declares_It()
		{
			// three levels, the middle one overriding a member of the base: the reference serializer writes the member
			// once, with the base level's members, not with the level that overrides it
			AssertSameWire(new OverrideLeafProbe { Shared = "s", Zulu = "z", Alpha = "a", Kilo = "k" }, v => DcsProbeSerializers.OverrideLeafProbe.ToXmlText(v), v => DcsProbeSchemalessSerializers.OverrideLeafProbe.ToXmlText(v));
		}

		#endregion

		#region Collections and dictionaries...

		[Test]
		public void Test_Collection_Item_Element_Names()
		{
			// Items are named after the item type contract: <string>, <int>, <dateTime>, <Shelf>, <ArrayOfstring> for
			// nested lists.
			// note: the [CollectionDataContract]-named collection member is excluded here; see NamedItems'
			// summary in DcsProbes.cs for why (CXML0010, a pre-existing Task 9 decision).
			AssertSameWire(new CollectionProbe
			{
				Strings = ["s1", "s2"],
				Ints = [1, 2, 3],
				StringArray = ["arr1", "arr2"],
				ShelfArray = [new Shelf { Label = "sa1" }],
				Shelves = [new() { Label = "sh1" }, new() { Label = "sh2" }],
				EmptyShelves = [],
				Nested = [["x"], ["y", "z"]],
				Dates = [new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Unspecified)],
				WithNullItem = ["present", null!],
				DeclaredAsInterface = new List<string> { "via-interface" },
			}, v => DcsProbeSerializers.CollectionProbe.ToXmlText(v), v => DcsProbeSchemalessSerializers.CollectionProbe.ToXmlText(v));
		}

		// note: enrolling a bare collection or scalar type directly stays refused by design (CJSON0019: enroll the
		// element type, not the collection). Those roots are written by the native root entry points on CrystalXml
		// instead: the scalar and collection families are exercised below, against the same live oracle.

		[Test]
		public void Test_Root_Null_Is_Nil()
		{
			AssertSameWire<Shelf>(null, v => DcsProbeSerializers.Shelf.ToXmlText(v), v => DcsProbeSchemalessSerializers.Shelf.ToXmlText(v));
		}

		[Test]
		public void Test_Scalar_Root_Matches_The_Reference_Wire()
		{
			// the native scalar entry points write the reference wire of a bare scalar root: the xsd lexical name in
			// the Serialization namespace, as pinned by the root facts of DcsNamespaceReferenceFacts. Equivalence is
			// on expanded names, the namespaced profile's rule.
			string reference = ReferenceDcsWire.Serialize("hello", typeof(string), strip: false);
			string actual = CrystalXml.Scalar.ToText("hello");
			Log("reference : " + reference);
			Log("generated : " + actual);
			XmlExpandedNameComparison.AssertEquivalent(reference, actual, "A bare string root must carry the xsd lexical name in the Serialization namespace.");

			reference = ReferenceDcsWire.Serialize(42, typeof(int), strip: false);
			actual = CrystalXml.Scalar.ToText(42);
			Log("reference : " + reference);
			Log("generated : " + actual);
			XmlExpandedNameComparison.AssertEquivalent(reference, actual, "A bare int root must carry the xsd lexical name in the Serialization namespace.");

			// the eight sinks share one writing core: the byte-exact sinks agree byte for byte
			Assert.That(CrystalXml.Scalar.ToSlice(42).ToStringUtf8(), Is.EqualTo(actual));
		}

		[Test]
		public void Test_Scalar_Root_Null_Is_Nil()
		{
			string reference = ReferenceDcsWire.Serialize(null, typeof(string), strip: false);
			string actual = CrystalXml.Scalar.ToText<string>(null);
			Log("reference : " + reference);
			Log("generated : " + actual);
			XmlExpandedNameComparison.AssertEquivalent(reference, actual, "A null scalar root must write the empty element marked nil.");
		}

		[Test]
		public void Test_Scalar_Root_Name_Override_Keeps_The_Serialization_Namespace()
		{
			// the caller names the root element, not the shape: the name changes and the namespace does not
			string actual = CrystalXml.Scalar.ToText("hello", rootName: "Greeting");
			Log(actual);
			XmlExpandedNameComparison.AssertEquivalent(
				"""<Greeting xmlns="http://schemas.microsoft.com/2003/10/Serialization/">hello</Greeting>""",
				actual,
				"The rootName override must rename the element and keep the Serialization namespace.");
		}

		[Test]
		public void Test_Scalar_Root_Refuses_A_Type_Outside_The_Lexical_Set()
		{
			// a contract type roots a document through its own serializer, and DateTimeOffset is a two-member
			// contract rather than a text scalar: neither has a scalar root wire, and a name is never guessed
			Assert.That(() => CrystalXml.Scalar.ToText(new Shelf { Label = "x" }), Throws.InstanceOf<CrystalXmlUnknownTypeException>());
			// the epoch is spelled out: DateTimeOffset.UnixEpoch does not exist on the net472 CLR this file also runs on
			Assert.That(() => CrystalXml.Scalar.ToText(new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)), Throws.InstanceOf<CrystalXmlUnknownTypeException>());
		}

		[Test]
		public void Test_Collection_Root_Matches_The_Reference_Wire()
		{
			// the collection entry points compose the item facet under the profile's ArrayOfX root, and the two
			// outputs obey the same two acceptance rules as every member wire: expanded names for the namespaced
			// default, bytes for Schemaless
			var items = new List<Shelf> { new() { Label = "novels" }, new() { Label = "essays" } };

			string standard = ReferenceDcsWire.Serialize(items, typeof(List<Shelf>), strip: false);
			string actual = CrystalXml.ToText(DcsProbeSerializers.Shelf.Default, items);
			Log("reference (standard) : " + standard);
			Log("generated (default)  : " + actual);
			XmlExpandedNameComparison.AssertEquivalent(standard, actual, "A List<Shelf> root must be ArrayOfShelf in the item's contract namespace, holding bare Shelf items.");

			string stripped = ReferenceDcsWire.Serialize(items, typeof(List<Shelf>), strip: true);
			string schemaless = CrystalXml.ToText(DcsProbeSchemalessSerializers.Shelf.Default, items);
			Log("reference (stripped) : " + stripped);
			Log("generated (schemaless): " + schemaless);
			Assert.That(schemaless, Is.EqualTo(stripped), "The schemaless collection root must stay byte-identical to the stripped wire.");
		}

		[Test]
		public void Test_Collection_Root_Null_And_Empty()
		{
			// same truth table as a member: a null sequence is the nil root, an empty one is the empty root
			string standard = ReferenceDcsWire.Serialize(null, typeof(List<Shelf>), strip: false);
			string actual = CrystalXml.ToText(DcsProbeSerializers.Shelf.Default, (IEnumerable<Shelf>?) null);
			Log("reference (standard, null) : " + standard);
			Log("generated (default, null)  : " + actual);
			XmlExpandedNameComparison.AssertEquivalent(standard, actual, "A null sequence must write the nil ArrayOfShelf root.");

			string stripped = ReferenceDcsWire.Serialize(null, typeof(List<Shelf>), strip: true);
			string schemaless = CrystalXml.ToText(DcsProbeSchemalessSerializers.Shelf.Default, (IEnumerable<Shelf>?) null);
			Log("reference (stripped, null) : " + stripped);
			Log("generated (schemaless, null): " + schemaless);
			Assert.That(schemaless, Is.EqualTo(stripped));

			stripped = ReferenceDcsWire.Serialize(new List<Shelf>(), typeof(List<Shelf>), strip: true);
			schemaless = CrystalXml.ToText(DcsProbeSchemalessSerializers.Shelf.Default, new List<Shelf>());
			Log("reference (stripped, empty) : " + stripped);
			Log("generated (schemaless, empty): " + schemaless);
			Assert.That(schemaless, Is.EqualTo(stripped));
		}

		[Test]
		public void Test_Collection_Root_Name_And_Item_Name_Overrides()
		{
			// the caller renames the root, the items, or both: the names change and the namespace does not (shown
			// on the schemaless output, where the bytes are the whole story)
			var items = new List<Shelf> { new() { Label = "x" } };

			string schemaless = CrystalXml.ToText(DcsProbeSchemalessSerializers.Shelf.Default, items, rootName: "Shelves");
			Assert.That(schemaless, Is.EqualTo("""<Shelves><Shelf><Label>x</Label></Shelf></Shelves>"""));

			schemaless = CrystalXml.ToText(DcsProbeSchemalessSerializers.Shelf.Default, items, rootName: "Shelves", itemName: "Item");
			Assert.That(schemaless, Is.EqualTo("""<Shelves><Item><Label>x</Label></Item></Shelves>"""));

			// cross-sink agreement: the byte-exact sinks share the writing core
			Assert.That(CrystalXml.ToSlice(DcsProbeSchemalessSerializers.Shelf.Default, items, rootName: "Shelves", itemName: "Item").ToStringUtf8(), Is.EqualTo(schemaless));
		}

		[Test]
		public void Test_Dictionary_Entry_Shapes()
		{
			// <KeyValueOfstringstring><Key>..</Key><Value>..</Value></KeyValueOfstringstring>, <KeyValueOfintstring> for
			// int keys, self-closing empty map.
			// note: the [CollectionDataContract]-named map member is excluded here for the same CXML0010 reason
			// as the collection family above.
			AssertSameWire(new DictionaryProbe
			{
				PlainMap = new() { ["k1"] = "v1", ["k2"] = "v2" },
				IntKeyMap = new() { [7] = "seven" },
				EmptyMap = [],
			}, v => DcsProbeSerializers.DictionaryProbe.ToXmlText(v), v => DcsProbeSchemalessSerializers.DictionaryProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Dictionary_Digest_Divergence_Is_Exactly_The_Hash_Suffix()
		{
			// KNOWN DELIBERATE DIVERGENCE 1: the reference format appends an 8-char namespace digest to the entry name
			// when the value type's contract namespace is not built-in; CrystalXml emits the unhashed name (no measured
			// consumer reads any KeyValueOf* element). This test pins the divergence to exactly that: stripping the
			// digest from the reference output yields the CrystalXml output, byte for byte.
			var value = new HashedDictionaryProbe { ObjectMap = new() { ["o1"] = new Shelf { Label = "ol1" } } };
			string reference = ReferenceDcsWire.Serialize(value, typeof(HashedDictionaryProbe));
			string actual = DcsProbeSchemalessSerializers.HashedDictionaryProbe.ToXmlText(value);
			Log("reference (standard) : " + ReferenceDcsWire.Serialize(value, typeof(HashedDictionaryProbe), strip: false));
			Log("reference (stripped) : " + reference);
			Log("generated (schemaless): " + actual);
			Log("generated (default)   : " + DcsProbeSerializers.HashedDictionaryProbe.ToXmlText(value));

			var digest = System.Text.RegularExpressions.Regex.Match(reference, "KeyValueOfstringShelf([0-9A-Za-z_]{8})");
			Assert.That(digest.Success, Is.True, "the reference format no longer hashes; revisit the seam");
			string dehashed = reference.Replace("KeyValueOfstringShelf" + digest.Groups[1].Value, "KeyValueOfstringShelf");
			Assert.That(actual, Is.EqualTo(dehashed));

			// the divergence is the digest and nothing else, which the namespaced output has to show too: de-hash the
			// standard wire the same way, and the two documents must resolve to the same expanded names. Without this the
			// entry elements could sit in the wrong namespace and the byte comparison above would never notice, because it
			// runs on the output that has no namespaces at all.
			string standard = ReferenceDcsWire.Serialize(value, typeof(HashedDictionaryProbe), strip: false);
			var standardDigest = System.Text.RegularExpressions.Regex.Match(standard, "KeyValueOfstringShelf([0-9A-Za-z_]{8})");
			Assert.That(standardDigest.Success, Is.True, "the reference format no longer hashes; revisit the seam");
			string standardDehashed = standard.Replace("KeyValueOfstringShelf" + standardDigest.Groups[1].Value, "KeyValueOfstringShelf");

			XmlExpandedNameComparison.AssertEquivalent(standardDehashed, DcsProbeSerializers.HashedDictionaryProbe.ToXmlText(value), "Once the digest is off both names, the namespaced output must resolve to the same expanded names as the standard wire.");
		}

		#endregion

		#region Polymorphism and naming...

		[Test]
		public void Test_Polymorphism_Discriminator()
		{
			// type="AudioBook" when a derived instance sits in a base-declared member, none when exact; boxed
			// primitives in object members carry type="string"/"int"/"long"; object null is nil.
			AssertSameWire(new PolymorphicProbe
			{
				DeclaredBaseHoldingBase = new CatalogItem { Title = "Generic" },
				DeclaredBaseHoldingDerived = new AudioBook { Title = "Heard", DurationMinutes = 90 },
				DeclaredExact = new AudioBook { Title = "Exact", DurationMinutes = 5 },
				Zoo = [new AudioBook { Title = "A", DurationMinutes = 1 }, new PrintedBook { Title = "P", Isbn = "i" }, new CatalogItem { Title = "Plain" }],
				AsObjectString = "boxed string",
				AsObjectInt = 123,
				AsObjectLong = 123L,
				AsObjectNull = null,
			}, v => DcsProbeSerializers.PolymorphicProbe.ToXmlText(v), v => DcsProbeSchemalessSerializers.PolymorphicProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Renamed_Contract_And_Members()
		{
			// Root <RenamedContract>; members sorted by their WIRE name ordinally: Plain, Required, renamed_member,
			// with-dash.
			AssertSameWire(new RenameProbe { Original = "o", Dashed = "d", Required = "r", Plain = "p" }, v => DcsProbeSerializers.RenameProbe.ToXmlText(v), v => DcsProbeSchemalessSerializers.RenameProbe.ToXmlText(v));
		}

		#endregion

		#region Scalars...

		[Test]
		public void Test_Scalar_Lexical_Forms()
		{
			// bool lowercase; decimal keeps scale (1.50); double 1.2E-09 / NaN / INF; DateTime by Kind with minimal
			// fraction; DateTimeOffset as {DateTime, OffsetMinutes}; TimeSpan as ISO 8601 duration; char as its code
			// point (65); enums by name or [EnumMember]; flags space-separated; byte[] as base64; Uri escaped.
			// note: control characters are NOT part of this family. The ControlChars member below is kept inert, and the
			// measured character-reference defect is pinned by the two dedicated deviation-2 tests instead.
			AssertSameWire(new ScalarProbe
			{
				TrueBool = true,
				FalseBool = false,
				Decimal = 12.34m,
				DecimalTrailingZero = 1.50m,
				DecimalNegative = -0.05m,
				Double = 1.5,
				DoubleNaN = double.NaN,
				DoubleInfinity = double.PositiveInfinity,
				DoubleExponent = 1.2e-9,
				Float = 2.5f,
				DateUnspecified = new DateTime(2026, 8, 3, 14, 5, 6, 789, DateTimeKind.Unspecified),
				DateUtc = new DateTime(2026, 8, 3, 14, 5, 6, 789, DateTimeKind.Utc),
				DateMinValue = DateTime.MinValue,
				DateNoFraction = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Unspecified),
				DateOffset = new DateTimeOffset(2026, 8, 3, 14, 5, 6, 789, TimeSpan.FromHours(2)),
				Duration = new TimeSpan(1, 33, 30),
				DurationFine = TimeSpan.FromTicks(1234567891234567L),
				Guid = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"),
				Char = 'A',
				Bytes = [0xDE, 0xAD, 0xBE, 0xEF],
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
				Unicode = "café, éèê, 中文, emoji 😀",
				ControlChars = "beforeafter", // note: kept inert here; the real control chars are pinned in the dedicated Shelf-based deviation 2 tests below
				InitOnlyMember = "init-value", // MINOR pin: init-only is a different flag from read-only; DCS emits it, and so does the generated format
			}, v => DcsProbeSerializers.ScalarProbe.ToXmlText(v), v => DcsProbeSchemalessSerializers.ScalarProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Local_Kind_DateTime_Is_Machine_Dependent_And_Reproduced()
		{
			// Kind=Local appends the machine's UTC offset; both pipelines run on the same machine so the bytes must
			// still agree (the non-determinism itself is a recorded product finding, not something this test hides).
			AssertSameWire(new ScalarProbe { DateUnspecified = new DateTime(2026, 8, 3, 14, 5, 6, 789, DateTimeKind.Local) }, v => DcsProbeSerializers.ScalarProbe.ToXmlText(v), v => DcsProbeSchemalessSerializers.ScalarProbe.ToXmlText(v));
		}

		#endregion

		#region Self-reference and cycles...

		[Test]
		public void Test_SelfReference_And_Shared_Instances()
		{
			// Recursion by structure; a shared instance is written out twice in full (no z:Id/z:Ref).
			var shared = new Node { Label = "shared" };
			AssertSameWire(new SelfRefProbe
			{
				Root = new Node
				{
					Label = "root",
					Next = new Node { Label = "next" },
					Children = [new() { Label = "c1" }, new() { Label = "c2", Children = [new() { Label = "c2a" }] }],
				},
				SharedA = shared,
				SharedB = shared,
			}, v => DcsProbeSerializers.SelfRefProbe.ToXmlText(v), v => DcsProbeSchemalessSerializers.SelfRefProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Cycle_Throws_In_Both_Pipelines()
		{
			// ACTED DEVIATION 3 (the typed-exception family): the two pipelines agree that a genuine reference cycle has
			// no representation at all, and disagree only on the exception TYPE. The reference serializer raises
			// SerializationException; CrystalXml raises its own CrystalXmlCycleException.
			// This family was withheld from the first cut of this suite: before the emission carried a depth guard, the
			// generated recursion exhausted the native stack and killed the whole test process instead of throwing.
			var a = new Node { Label = "a" };
			var b = new Node { Label = "b", Next = a };
			a.Next = b;
			var probe = new SelfRefProbe { Root = a };

			using (Assert.EnterMultipleScope())
			{
				Assert.That(() => ReferenceDcsWire.Serialize(probe, typeof(SelfRefProbe)), Throws.InstanceOf<SerializationException>(), "the reference serializer refuses a cycle with its own exception type");
				Assert.That(() => DcsProbeSerializers.SelfRefProbe.ToXmlText(probe), Throws.InstanceOf<CrystalXmlCycleException>().With.Property("Type").EqualTo(typeof(Node)), "CrystalXml refuses it with the typed counterpart, naming the type it stopped on");
			}
		}

		#endregion

		#region Empty contract, POCO mode...

		[Test]
		public void Test_Empty_Contract_Self_Closes()
		{
			AssertSameWire(new EmptyContractProbe(), v => DcsProbeSerializers.EmptyContractProbe.ToXmlText(v), v => DcsProbeSchemalessSerializers.EmptyContractProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Poco_Mode_Public_ReadWrite_Alphabetical()
		{
			// No [DataContract]: public get+set properties only, alphabetical.
			AssertSameWire(new PocoProbe { Zulu = "z", Alpha = "a", Number = 5, Items = ["i1"] }, v => DcsProbeSerializers.PocoProbe.ToXmlText(v), v => DcsProbeSchemalessSerializers.PocoProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Poco_Mode_Null_Member()
		{
			// Pins whether POCO mode emits nil for a null member (measured against the live oracle, not assumed).
			AssertSameWire(new PocoProbe { Zulu = null, Alpha = "a", Number = 0, Items = null }, v => DcsProbeSerializers.PocoProbe.ToXmlText(v), v => DcsProbeSchemalessSerializers.PocoProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Poco_Mode_ReadOnly_Member_Is_Absent()
		{
			// POCO mode is "public get+set only": PocoProbe.ReadOnlyIgnored is get-only, so neither format carries it.
			// (On a [DataContract] type the same shape is not even a valid contract: the reference serializer raises
			// InvalidDataContractException, "No set method for property".)
			var probe = new PocoProbe { Zulu = "z", Alpha = "a", Number = 5, Items = ["i1"] };

			string actual = DcsProbeSchemalessSerializers.PocoProbe.ToXmlText(probe);
			using (Assert.EnterMultipleScope())
			{
				Assert.That(actual, Is.EqualTo(ReferenceDcsWire.Serialize(probe, typeof(PocoProbe))));
				Assert.That(actual, Does.Not.Contain("ReadOnlyIgnored"), "a get-only member is never on the DataContract format");
			}
		}

		[Test]
		public void Test_IgnoreDataMember_And_Unannotated_Are_Absent()
		{
			AssertSameWire(new IgnoreProbe { Kept = "k", Ignored = "i", NotAnnotated = "n" }, v => DcsProbeSerializers.IgnoreProbe.ToXmlText(v), v => DcsProbeSchemalessSerializers.IgnoreProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Private_DataMember_Is_Serialized()
		{
			// [DataMember] on a private field IS in the output (measured DCS behavior on [DataContract] types).
			var probe = new PrivateMemberProbe { Visible = "v" };
			probe.SetSecret("s3cr3t");
			AssertSameWire(probe, v => DcsProbeSerializers.PrivateMemberProbe.ToXmlText(v), v => DcsProbeSchemalessSerializers.PrivateMemberProbe.ToXmlText(v));
		}

		[Test]
		public void Test_ReadOnly_DataMember_Field_Is_On_The_Wire()
		{
			// CRITICAL FIX: the reference serializer's no-set-method check is property-only (it never looks at
			// fields), so a readonly [DataMember] FIELD on a [DataContract] type IS in the output. The generator's own
			// read-only filter used to drop every read-only member regardless of field-vs-property, which silently
			// dropped this member from the compat format.
			AssertSameWire(new ReadOnlyFieldProbe("fixed-value") { Normal = "n" }, v => DcsProbeSerializers.ReadOnlyFieldProbe.ToXmlText(v), v => DcsProbeSchemalessSerializers.ReadOnlyFieldProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Named_Generic_Expands_Braces()
		{
			// [DataContract(Name = "Envelope{0}")] on a generic type: {0} expands to the argument's contract name ->
			// root <Envelopeboolean>.
			AssertSameWire(new NamedGenericProbe<bool> { Payload = true }, v => DcsProbeSerializers.NamedGenericProbe_Boolean.ToXmlText(v), v => DcsProbeSchemalessSerializers.NamedGenericProbe_Boolean.ToXmlText(v));
		}

		#endregion

		#region The ISerializable dialect and acted deviation 3...

		[Test]
		public void Test_ISerializable_Dialect_Keys_Become_Element_Names()
		{
			// The measured dialect: each entry of the bag is an element NAMED AFTER THE KEY, with type="string" on the
			// value (declared object).
			var probe = new KeyedBagProbe { Properties = new KeyedBag<string>() };
			probe.Properties.Add("origin", "acme-main");
			probe.Properties.Add("channel", "web");
			AssertSameWire(probe, v => DcsProbeSerializers.KeyedBagProbe.ToXmlText(v), v => DcsProbeSchemalessSerializers.KeyedBagProbe.ToXmlText(v));
		}

		[Test]
		public void Test_ISerializable_Dialect_Non_NCName_Key()
		{
			// Pins what the reference format does with a key that is not a valid XML name (space inside): both pipelines
			// must agree byte for byte, whatever that behavior is.
			var probe = new KeyedBagProbe { Properties = new KeyedBag<string>() };
			probe.Properties.Add("not a name", "v");
			AssertSameWire(probe, v => DcsProbeSerializers.KeyedBagProbe.ToXmlText(v), v => DcsProbeSchemalessSerializers.KeyedBagProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Deviation_3_Undeclared_Runtime_Type_In_An_AnyType_Slot()
		{
			// ACTED DEVIATION 3. The anyType switch generated code can dispatch on is closed at generation time to this
			// container's own known types. A List<string> value dropped into an object-declared slot (here,
			// PolymorphicProbe.AsObjectString) needs the output name "ArrayOfstring", which reflection-free code has no
			// way to compute for a type this container never registers as an object-slot candidate: the live oracle
			// succeeds (it always can, via reflection), CrystalXml refuses instead of guessing.
			// note: naming the family this KeyedBag<after<string>> instead would hit a
			// DIFFERENT, build-time refusal here (see the note on KeyedBagProbe in DcsProbes.cs) and never reaches
			// runtime at all, so this test pins the same acted deviation through the shape Task 9's own fixture
			// already exercises for it (Test_A_Runtime_Type_The_Container_Cannot_Name_Is_Refused_In_An_AnyType_Slot).
			var probe = new PolymorphicProbe { AsObjectString = new List<string> { "a" } };

			string reference = ReferenceDcsWire.Serialize(probe, typeof(PolymorphicProbe));
			Log($"reference (DCS succeeds): {reference}");

			// the oracle half is an ASSERTION, not just a log line: this family only pins a DIVERGENCE for as long as the
			// reference format keeps succeeding, so the day DCS starts refusing the shape too, this test must say so
			Assert.That(
				reference,
				Is.EqualTo("""<PolymorphicProbe><AsObjectInt nil="true" /><AsObjectLong nil="true" /><AsObjectNull nil="true" /><AsObjectString type="ArrayOfstring"><string>a</string></AsObjectString><DeclaredBaseHoldingBase nil="true" /><DeclaredBaseHoldingDerived nil="true" /><DeclaredExact nil="true" /><Zoo nil="true" /></PolymorphicProbe>"""),
				"the reference format still succeeds, naming the undeclared runtime type ArrayOfstring");

			Assert.That(
				() => DcsProbeSerializers.PolymorphicProbe.ToXmlText(probe),
				Throws.InstanceOf<NotSupportedException>(),
				"the family is not reproduced: CrystalXml refuses the undeclared runtime shape instead of guessing its format name");
		}

		[Test]
		public void Test_Collection_Of_Object_Items_Are_AnyType()
		{
			// UNTESTED CORNER (coverage ledger gap 5): a declared List<object> member. Each ITEM goes through the same
			// per-item anyType switch already exercised by PolymorphicProbe.AsObjectString/AsObjectInt (both boxed
			// built-ins, both already reproduced), not the "undeclared runtime type" refusal pinned above -- that one
			// fires only when the OBJECT SLOT ITSELF holds an unregistered collection/composed type (List<string>
			// needing "ArrayOfstring"). Here the outer type (List<object>) is declared; only its items are object-typed,
			// null items nil, non-null items carrying a type= discriminator (string/int), item element named anyType.
			AssertSameWire(new AnyTypeCollectionProbe
			{
				Results = [null!, "s1", 42],
			}, v => DcsProbeSerializers.AnyTypeCollectionProbe.ToXmlText(v), v => DcsProbeSchemalessSerializers.AnyTypeCollectionProbe.ToXmlText(v));
		}

		#endregion

		#region Root name override and control-character sanitization (acted deviation 2)...

		[Test]
		public void Test_Root_Name_Override()
		{
			// The rootName override renames the root element only; body unchanged.
			string actual = DcsProbeSchemalessSerializers.Shelf.ToXmlText(new Shelf { Label = "x" }, rootName: "data");
			Assert.That(actual, Is.EqualTo("""<data><Label>x</Label></data>"""));
		}

		[Test]
		public void Test_Deviation_4_A_Shadowed_Member_Is_Written_Once()
		{
			// a member shadowed by a 'new' one is two contract members to the reference serializer, which writes both:
			// the base one (null, only the derived one is settable through the derived type) and the derived one.
			// Generated code reads one accessor per member name, so it writes the derived member only (acted deviation 4)
			var value = new ShadowLeafProbe { Shared = "s", Zulu = "z", Alpha = "a" };

			string reference = ReferenceDcsWire.Serialize(value, typeof(ShadowLeafProbe));
			string actual = DcsProbeSchemalessSerializers.ShadowLeafProbe.ToXmlText(value);
			Log($"reference: {reference}");
			Log($"actual:    {actual}");

			Assert.That(reference, Is.EqualTo("""<ShadowLeafProbe><Shared nil="true" /><Zulu>z</Zulu><Alpha>a</Alpha><Shared>s</Shared></ShadowLeafProbe>"""), "the reference format still writes the shadowed member twice");
			Assert.That(actual, Is.EqualTo("""<ShadowLeafProbe><Zulu>z</Zulu><Alpha>a</Alpha><Shared>s</Shared></ShadowLeafProbe>"""), "the generated format writes the member the accessor reads, once");
		}

		[Test]
		public void Test_Deviation_2_Sanitized_Control_Characters_Differ_From_The_Reference_Wire()
		{
			// ACTED DEVIATION 2. The reference format writes control characters as character references under
			// CheckCharacters=false, which its own post-filter (built to catch escaped invalid characters, not raw
			// control bytes hiding behind an entity) lets straight through: <Label>before&#x1;&#x8;after</Label>, a
			// document no conformant XML reader accepts. CrystalXml drops them at the value level instead, by default.
			var value = new Shelf { Label = "before\u0001\u0008after" };
			string reference = ReferenceDcsWire.Serialize(value, typeof(Shelf));
			string actual = DcsProbeSchemalessSerializers.Shelf.ToXmlText(value);

			Assert.That(reference, Is.EqualTo("""<Shelf><Label>before&#x1;&#x8;after</Label></Shelf>"""), "the reference format still emits the unparseable character references");
			Assert.That(actual, Is.EqualTo("""<Shelf><Label>beforeafter</Label></Shelf>"""), "the sanitized default drops the control characters at the value level");
		}

		[Test]
		public void Test_Deviation_2_StrictControlCharacters_Mode_Matches_The_Reference_Wire_Exactly()
		{
			// The escape hatch back to the (defective) reference behavior: constructing the byte-exact writer directly
			// with strictControlCharacters:true reproduces the reference format's character references byte for byte,
			// for a certification harness that wants to compare against captured legacy output on purpose.
			var value = new Shelf { Label = "before\u0001\u0008after" };
			string reference = ReferenceDcsWire.Serialize(value, typeof(Shelf));

			var sink = new ValueStringWriter();
			var emitter = new CrystalXmlWriter<char, ValueStringWriter>(ref sink, strictControlCharacters: true);
			string strict;
			try
			{
				DcsProbeSchemalessSerializers.Shelf.Default.WriteXml(ref emitter, value);
				strict = emitter.Writer.ToStringAndDispose();
			}
			catch
			{
				emitter.Writer.Dispose();
				throw;
			}

			Assert.That(strict, Is.EqualTo(reference), "strictControlCharacters:true reproduces the reference format's defect exactly");
		}

		#endregion

		#region The worked example: the two wires of one profile, side by side...

		[Test]
		public void Test_The_Worked_Example_Writes_Both_Wires()
		{
			// One instance, one oracle, two outputs, and the whole namespace vocabulary in a single document. Both
			// documents are pinned verbatim as well as compared to the oracle: they are what the profile promises, so a
			// change to either one has to be read and accepted, not merely re-measured.
			var value = new WorkedLibrary
			{
				Name = "Centrale",
				Owner = new WorkedContact { Email = "x@y.fr" },
				NullChild = null,
				CritBase = new WorkedCriterion(),
				CritDerived = new WorkedRangeCriterion { Min = 3 },
				Tags = ["red", "green"],
			};

			AssertSameWire(value, v => DcsProbeSerializers.WorkedLibrary.ToXmlText(v), v => DcsProbeSchemalessSerializers.WorkedLibrary.ToXmlText(v));

			// the whole difference from the reference wire above, and the whole of it: NullChild and CritBase have lost a
			// d2p1 declaration that nothing under them ever used, and the root declares its two namespaces in the other
			// order. A reader resolves both documents to the same names.
			Assert.That(
				DcsProbeSerializers.WorkedLibrary.ToXmlText(value),
				Is.EqualTo(
					"""
					<Library xmlns="urn:acme:biblio" xmlns:i="http://www.w3.org/2001/XMLSchema-instance"><Name>Centrale</Name><Owner xmlns:d2p1="urn:acme:annuaire"><d2p1:Email>x@y.fr</d2p1:Email></Owner><NullChild i:nil="true" /><CritBase /><CritDerived xmlns:d2p1="urn:acme:recherche" i:type="d2p1:RangeCriterion"><d2p1:Min>3</d2p1:Min></CritDerived><Tags xmlns:d2p1="http://schemas.microsoft.com/2003/10/Serialization/Arrays"><d2p1:string>red</d2p1:string><d2p1:string>green</d2p1:string></Tags></Library>
					"""),
				"the default output");

			// and the same model under the option: the DCS-isms stay (nil, type), the namespaces are gone, and the type
			// annotation has lost the namespace half of its qualified name
			Assert.That(
				DcsProbeSchemalessSerializers.WorkedLibrary.ToXmlText(value),
				Is.EqualTo(
					"""
					<Library><Name>Centrale</Name><Owner><Email>x@y.fr</Email></Owner><NullChild nil="true" /><CritBase /><CritDerived type="RangeCriterion"><Min>3</Min></CritDerived><Tags><string>red</string><string>green</string></Tags></Library>
					"""),
				"the schemaless output");
		}

		#endregion

		#region Sanity: the generated container round-trips through every output entry point...

		[Test]
		public void Test_Container_Byte_Core_Matches_Its_Own_Text_Output()
		{
			var probe = new NilProbe { SetString = "value", FullList = ["a"], EmptyBytes = [] };

			string text = DcsProbeSerializers.NilProbe.ToXmlText(probe);
			var slice = DcsProbeSerializers.NilProbe.ToXmlSlice(probe);
			byte[] bytes = DcsProbeSerializers.NilProbe.ToXmlBytes(probe);

			using (Assert.EnterMultipleScope())
			{
				Assert.That(slice.ToStringUtf8(), Is.EqualTo(text), "the byte core, decoded");
				Assert.That(Encoding.UTF8.GetString(bytes), Is.EqualTo(text), "the byte array");
			}
		}

		#endregion

	}

}

