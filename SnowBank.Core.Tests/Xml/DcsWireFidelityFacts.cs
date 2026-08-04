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

// This file is not compiled for the net472 validation target: see the remark on ReferenceDcsWire.cs.
#if !NETFRAMEWORK

namespace SnowBank.Data.Xml.Tests
{
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
	/// <remarks>Ported from the design spike's own <c>WireFidelityFacts.cs</c>, family by family.</remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-XML")]
	public sealed class DcsWireFidelityFacts : SimpleTest
	{

		/// <summary>Compares the live-DCS reference wire against a generated <c>ToXmlText</c> call, byte for byte</summary>
		private static void AssertSameWire<T>(T? value, Func<T?, string> toXmlText)
		{
			string expected = ReferenceDcsWire.Serialize(value, typeof(T));
			string actual = toXmlText(value);
			Assert.That(actual, Is.EqualTo(expected));
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
			}, v => DcsProbeSerializers.NilProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Member_Order_Default_Is_Ordinal_Alphabetical()
		{
			// Declared Zulu, Alpha, Mike, Bravo; the wire emits Alpha, Bravo, Mike, Zulu.
			AssertSameWire(new OrderDefaultProbe { Zulu = "z", Alpha = "a", Mike = "m", Bravo = "b" }, v => DcsProbeSerializers.OrderDefaultProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Member_Order_Explicit_Groups_After_Unordered()
		{
			// Expected wire order: NoOrderCharlie, NoOrderYankee, Zulu(1), Bravo(2), Mike(2), Alpha(3).
			AssertSameWire(new OrderExplicitProbe
			{
				Alpha = "a3", Zulu = "z1", Mike = "m2", Bravo = "b2", NoOrderYankee = "y", NoOrderCharlie = "c",
			}, v => DcsProbeSerializers.OrderExplicitProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Member_Order_Base_Level_Comes_First()
		{
			// Base members (including its ordered ones) come before derived members.
			AssertSameWire(new OrderDerivedProbe { ZuluFromBase = "bz", OrderedFromBase = "bo", AlphaFromDerived = "da" }, v => DcsProbeSerializers.OrderDerivedProbe.ToXmlText(v));
		}

		[Test]
		public void Test_EmitDefaultValue_False_Omits_Default_And_Null()
		{
			// Only KeptZeroInt (0), KeptNullString (nil) and SetInt (7) appear on the wire.
			AssertSameWire(new EmitDefaultProbe { SetInt = 7 }, v => DcsProbeSerializers.EmitDefaultProbe.ToXmlText(v));
		}

		#endregion

		#region Collections and dictionaries...

		[Test]
		public void Test_Collection_Item_Element_Names()
		{
			// Items are named after the item type contract: <string>, <int>, <dateTime>, <Shelf>, <ArrayOfstring> for
			// nested lists.
			// note: the spike's [CollectionDataContract]-named collection member is excluded here; see NamedItems'
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
			}, v => DcsProbeSerializers.CollectionProbe.ToXmlText(v));
		}

		// note: the spike's "List<Shelf> as root" and "string as root" families are not exercised here: registering a
		// bare collection or scalar type directly with [CrystalJsonSerializable] (no declaring DTO) produces generator
		// output that fails to compile. See the note above DcsProbeSerializers in DcsProbes.cs; reported to the main
		// session as a finding, not fixed here, and not a Task 9 DataContract-XML-specific defect (nothing in that
		// generated, broken code is profile-specific).

		[Test]
		public void Test_Root_Null_Is_Nil()
		{
			AssertSameWire<Shelf>(null, v => DcsProbeSerializers.Shelf.ToXmlText(v));
		}

		[Test]
		public void Test_Dictionary_Entry_Shapes()
		{
			// <KeyValueOfstringstring><Key>..</Key><Value>..</Value></KeyValueOfstringstring>, <KeyValueOfintstring> for
			// int keys, self-closing empty map.
			// note: the spike's [CollectionDataContract]-named map member is excluded here for the same CXML0010 reason
			// as the collection family above.
			AssertSameWire(new DictionaryProbe
			{
				PlainMap = new() { ["k1"] = "v1", ["k2"] = "v2" },
				IntKeyMap = new() { [7] = "seven" },
				EmptyMap = [],
			}, v => DcsProbeSerializers.DictionaryProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Dictionary_Digest_Divergence_Is_Exactly_The_Hash_Suffix()
		{
			// KNOWN DELIBERATE DIVERGENCE 1: the reference wire appends an 8-char namespace digest to the entry name
			// when the value type's contract namespace is not built-in; CrystalXml emits the unhashed name (no measured
			// consumer reads any KeyValueOf* element). This test pins the divergence to exactly that: stripping the
			// digest from the reference output yields the CrystalXml output, byte for byte.
			var value = new HashedDictionaryProbe { ObjectMap = new() { ["o1"] = new Shelf { Label = "ol1" } } };
			string reference = ReferenceDcsWire.Serialize(value, typeof(HashedDictionaryProbe));
			string actual = DcsProbeSerializers.HashedDictionaryProbe.ToXmlText(value);

			var digest = System.Text.RegularExpressions.Regex.Match(reference, "KeyValueOfstringShelf([0-9A-Za-z_]{8})");
			Assert.That(digest.Success, Is.True, "the reference wire no longer hashes; revisit the seam");
			string dehashed = reference.Replace("KeyValueOfstringShelf" + digest.Groups[1].Value, "KeyValueOfstringShelf");
			Assert.That(actual, Is.EqualTo(dehashed));
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
			}, v => DcsProbeSerializers.PolymorphicProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Renamed_Contract_And_Members()
		{
			// Root <RenamedContract>; members sorted by their WIRE name ordinally: Plain, Required, renamed_member,
			// with-dash.
			AssertSameWire(new RenameProbe { Original = "o", Dashed = "d", Required = "r", Plain = "p" }, v => DcsProbeSerializers.RenameProbe.ToXmlText(v));
		}

		#endregion

		#region Scalars...

		[Test]
		public void Test_Scalar_Lexical_Forms()
		{
			// bool lowercase; decimal keeps scale (1.50); double 1.2E-09 / NaN / INF; DateTime by Kind with minimal
			// fraction; DateTimeOffset as {DateTime, OffsetMinutes}; TimeSpan as ISO 8601 duration; char as its code
			// point (65); enums by name or [EnumMember]; flags space-separated; byte[] as base64; Uri escaped; control
			// chars as character references (the measured defect, reproduced by default).
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
			}, v => DcsProbeSerializers.ScalarProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Local_Kind_DateTime_Is_Machine_Dependent_And_Reproduced()
		{
			// Kind=Local appends the machine's UTC offset; both pipelines run on the same machine so the bytes must
			// still agree (the non-determinism itself is a recorded product finding, not something this test hides).
			AssertSameWire(new ScalarProbe { DateUnspecified = new DateTime(2026, 8, 3, 14, 5, 6, 789, DateTimeKind.Local) }, v => DcsProbeSerializers.ScalarProbe.ToXmlText(v));
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
			}, v => DcsProbeSerializers.SelfRefProbe.ToXmlText(v));
		}

		// note: NOT ported as a runnable test. The spike's cycle family expects CrystalXml to throw
		// CrystalXmlCycleException on a genuine reference cycle (Node.Next pointing back into its own ancestry). The
		// generated WriteXmlElement for a self-referencing type has no depth guard or visited-set at all: writing a
		// cyclic SelfRefProbe recurses until the native stack is exhausted, which is an unrecoverable
		// StackOverflowException that terminates the WHOLE test process, not a catchable exception -- confirmed by
		// running it (it took the entire assembly down). CrystalXmlCycleException is declared in
		// SnowBank.Core/Data/Xml/CrystalXmlExceptions.cs but is never thrown anywhere in
		// SnowBank.Serialization.Json.CodeGen (grepped the whole generator: zero hits). This is a Task 8/9 defect,
		// reported in full in the Task 10 report; it is not fixed here, and this test is deliberately left
		// unexercised rather than committed in a form that would crash CI.

		#endregion

		#region Empty contract, POCO mode...

		[Test]
		public void Test_Empty_Contract_Self_Closes()
		{
			AssertSameWire(new EmptyContractProbe(), v => DcsProbeSerializers.EmptyContractProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Poco_Mode_Public_ReadWrite_Alphabetical()
		{
			// No [DataContract]: public get+set properties only, alphabetical.
			AssertSameWire(new PocoProbe { Zulu = "z", Alpha = "a", Number = 5, Items = ["i1"] }, v => DcsProbeSerializers.PocoProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Poco_Mode_Null_Member()
		{
			// Pins whether POCO mode emits nil for a null member (measured against the live oracle, not assumed).
			AssertSameWire(new PocoProbe { Zulu = null, Alpha = "a", Number = 0, Items = null }, v => DcsProbeSerializers.PocoProbe.ToXmlText(v));
		}

		[Test]
		public void Test_IgnoreDataMember_And_Unannotated_Are_Absent()
		{
			AssertSameWire(new IgnoreProbe { Kept = "k", Ignored = "i", NotAnnotated = "n" }, v => DcsProbeSerializers.IgnoreProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Private_DataMember_Is_Serialized()
		{
			// [DataMember] on a private field IS on the wire (measured DCS behavior on [DataContract] types).
			var probe = new PrivateMemberProbe { Visible = "v" };
			probe.SetSecret("s3cr3t");
			AssertSameWire(probe, v => DcsProbeSerializers.PrivateMemberProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Named_Generic_Expands_Braces()
		{
			// [DataContract(Name = "Envelope{0}")] on a generic type: {0} expands to the argument's contract name ->
			// root <Envelopeboolean>.
			AssertSameWire(new NamedGenericProbe<bool> { Payload = true }, v => DcsProbeSerializers.NamedGenericProbe_Boolean.ToXmlText(v));
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
			AssertSameWire(probe, v => DcsProbeSerializers.KeyedBagProbe.ToXmlText(v));
		}

		[Test]
		public void Test_ISerializable_Dialect_Non_NCName_Key()
		{
			// Pins what the reference wire does with a key that is not a valid XML name (space inside): both pipelines
			// must agree byte for byte, whatever that behavior is.
			var probe = new KeyedBagProbe { Properties = new KeyedBag<string>() };
			probe.Properties.Add("not a name", "v");
			AssertSameWire(probe, v => DcsProbeSerializers.KeyedBagProbe.ToXmlText(v));
		}

		[Test]
		public void Test_Deviation_3_Undeclared_Runtime_Type_In_An_AnyType_Slot()
		{
			// ACTED DEVIATION 3. The anyType switch generated code can dispatch on is closed at generation time to this
			// container's own known types. A List<string> value dropped into an object-declared slot (here,
			// PolymorphicProbe.AsObjectString) needs the wire name "ArrayOfstring", which reflection-free code has no
			// way to compute for a type this container never registers as an object-slot candidate: the live oracle
			// succeeds (it always can, via reflection), CrystalXml refuses instead of guessing.
			// note: the spike names this family after KeyedBag<List<string>> instead; porting that exact shape hits a
			// DIFFERENT, build-time refusal here (see the note on KeyedBagProbe in DcsProbes.cs) and never reaches
			// runtime at all, so this test pins the same acted deviation through the shape Task 9's own fixture
			// already exercises for it (Test_A_Runtime_Type_The_Container_Cannot_Name_Is_Refused_In_An_AnyType_Slot).
			var probe = new PolymorphicProbe { AsObjectString = new List<string> { "a" } };

			string reference = ReferenceDcsWire.Serialize(probe, typeof(PolymorphicProbe));
			Log($"reference (DCS succeeds): {reference}");

			Assert.That(
				() => DcsProbeSerializers.PolymorphicProbe.ToXmlText(probe),
				Throws.InstanceOf<CrystalXmlNotSupportedException>(),
				"the family is not reproduced: CrystalXml refuses the undeclared runtime shape instead of guessing its wire name");
		}

		#endregion

		#region Root name override and control-character sanitization (acted deviation 2)...

		[Test]
		public void Test_Root_Name_Override()
		{
			// The rootName override renames the root element only; body unchanged.
			string actual = DcsProbeSerializers.Shelf.ToXmlText(new Shelf { Label = "x" }, rootName: "data");
			Assert.That(actual, Is.EqualTo("""<data><Label>x</Label></data>"""));
		}

		[Test]
		public void Test_Deviation_2_Sanitized_Control_Characters_Differ_From_The_Reference_Wire()
		{
			// ACTED DEVIATION 2. The reference wire writes control characters as character references under
			// CheckCharacters=false, which its own post-filter (built to catch escaped invalid characters, not raw
			// control bytes hiding behind an entity) lets straight through: <Label>before&#x1;&#x8;after</Label>, a
			// document no conformant XML reader accepts. CrystalXml drops them at the value level instead, by default.
			var value = new Shelf { Label = "beforeafter" };
			string reference = ReferenceDcsWire.Serialize(value, typeof(Shelf));
			string actual = DcsProbeSerializers.Shelf.ToXmlText(value);

			Assert.That(reference, Is.EqualTo("""<Shelf><Label>before&#x1;&#x8;after</Label></Shelf>"""), "the reference wire still emits the unparseable character references");
			Assert.That(actual, Is.EqualTo("""<Shelf><Label>beforeafter</Label></Shelf>"""), "the sanitized default drops the control characters at the value level");
		}

		[Test]
		public void Test_Deviation_2_StrictControlCharacters_Mode_Matches_The_Reference_Wire_Exactly()
		{
			// The escape hatch back to the (defective) reference behavior: constructing the byte-exact writer directly
			// with strictControlCharacters:true reproduces the reference wire's character references byte for byte,
			// for a certification harness that wants to compare against captured legacy output on purpose.
			var value = new Shelf { Label = "beforeafter" };
			string reference = ReferenceDcsWire.Serialize(value, typeof(Shelf));

			var sink = new ValueStringWriter();
			var emitter = new CrystalXmlWriter<char, ValueStringWriter>(ref sink, strictControlCharacters: true);
			string strict;
			try
			{
				DcsProbeSerializers.Shelf.Default.WriteXml(ref emitter, value);
				strict = emitter.Writer.ToStringAndDispose();
			}
			catch
			{
				emitter.Writer.Dispose();
				throw;
			}

			Assert.That(strict, Is.EqualTo(reference), "strictControlCharacters:true reproduces the reference wire's defect exactly");
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

#endif
