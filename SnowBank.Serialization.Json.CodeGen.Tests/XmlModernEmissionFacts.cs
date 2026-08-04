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

namespace NodaTime
{
	using SnowBank.Data.Xml;

	/// <summary>A type the crawler deliberately leaves out (its namespace is one of the three it skips), which still writes its own XML content</summary>
	/// <remarks>Declared HERE, in a namespace the parser never enrols, so that the member-level <c>ICrystalXmlSerializable</c> dispatch of the emitter is the code that runs: an enrolled type would reach its own hook through its own generated body instead, leaving that branch unexecuted.</remarks>
	public readonly struct Stamp : ICrystalXmlSerializable
	{

		public Stamp(string text) => this.Text = text;

		public string Text { get; }

		public void WriteXml<TEmitter>(ref TEmitter emitter)
			where TEmitter : struct, IXmlEmitter
		{
			emitter.WriteText(this.Text);
		}

	}

}

namespace SnowBank.Serialization.Json.CodeGen.Tests.Acme
{
	using System.Runtime.Serialization;
	using System.Text.Json.Serialization;
	using SnowBank.Data.Xml;

	#region Probe types...

	// note: these types are attributed in THIS project, which references the generator as an analyzer on itself, so the
	// code every fact below executes is the code the generator emitted for them, compiled by the same build

	/// <summary>The design document's own example DTO, verbatim: an attribute-projected scalar, a string, a wrapped
	/// collection, a dictionary in the modern default shape, and a null member that stays out of both formats</summary>
	public sealed record Book
	{

		[XmlProperty("@id")]
		public required int Id { get; init; }

		public required string Title { get; init; }

		[XmlProperty(ItemName = "tag")]
		public List<string> Tags { get; init; } = [ ];

		public Dictionary<string, int> Scores { get; init; } = [ ];

		public string? Subtitle { get; init; }

	}

	/// <summary>Collections with NO item name: the default unwrapped shape, whose empty case writes nothing at all</summary>
	public sealed record Shelf
	{

		public List<string>? Labels { get; init; }

		public int[]? Codes { get; init; }

	}

	/// <summary>The four dictionary shapes, each spelled out on its own member</summary>
	public sealed record Ledger
	{

		[XmlProperty(DictionaryFormat = XmlDictionaryFormat.Direct)]
		public Dictionary<string, int> Direct { get; init; } = [ ];

		[XmlProperty(ItemName = "score", DictionaryFormat = XmlDictionaryFormat.KeyAttribute)]
		public Dictionary<string, int> Tagged { get; init; } = [ ];

		[XmlProperty(ItemName = "score", DictionaryFormat = XmlDictionaryFormat.KeyValueAttributes)]
		public Dictionary<string, int> Paired { get; init; } = [ ];

		[XmlProperty(DictionaryFormat = XmlDictionaryFormat.KeyValueElements)]
		public Dictionary<string, int> Nested { get; init; } = [ ];

	}

	public enum Genre
	{
		Unknown = 0,
		SciFi = 1,
	}

	/// <summary>One member per scalar family the formatters cover, so the formatter selection is pinned type by type</summary>
	public sealed record Sample
	{

		public bool Flag { get; init; }

		public double Ratio { get; init; }

		public decimal Price { get; init; }

		public char Initial { get; init; }

		public Genre Kind { get; init; }

		public DateTime Stamp { get; init; }

		public TimeSpan Span { get; init; }

		public Guid Key { get; init; }

		public byte[]? Blob { get; init; }

		public Uri? Link { get; init; }

		public int? Maybe { get; init; }

	}

	/// <summary>Writes its own XML content, reached as a MEMBER of another DTO</summary>
	public sealed record Signature : ICrystalXmlSerializable
	{

		public string? Note { get; init; }

		public void WriteXml<TEmitter>(ref TEmitter emitter)
			where TEmitter : struct, IXmlEmitter
		{
			emitter.WriteText(this.Note);
		}

	}

	/// <summary>Writes its own XML content, and IS enrolled: the generated body owns the element shell, the hook the content</summary>
	public sealed record Marker : ICrystalXmlSerializable
	{

		public string? Note { get; init; }

		public void WriteXml<TEmitter>(ref TEmitter emitter)
			where TEmitter : struct, IXmlEmitter
		{
			emitter.WriteText(this.Note);
		}

	}

	/// <summary>A DTO holding another generated DTO, and a type that writes its own content</summary>
	public sealed record Wrapper
	{

		public Book? Inner { get; init; }

		public Signature? Mark { get; init; }

		/// <summary>A member type the crawler never enrols, so the member-level hook dispatch is what writes it</summary>
		public global::NodaTime.Stamp? Tick { get; init; }

	}

	/// <summary>An enum whose wire label is a domain code, declared on its own field (the DataContract spelling the JSON side already honors)</summary>
	public enum Courier
	{

		[EnumMember(Value = "on-foot")]
		Foot = 0,

		Bike = 1,

	}

	/// <summary>A combinable enum: a declared combination has a label of its own, an undeclared one has none</summary>
	[Flags]
	public enum Access
	{
		None = 0,
		Read = 1,
		Write = 2,
		Full = Read | Write,
	}

	/// <summary>The three enum wire forms: the label, the numeric override, and the combinable case</summary>
	public sealed record Dispatch
	{

		public Courier Mode { get; init; }

		[JsonProperty(EnumFormat = JsonEnumFormat.Number)]
		public Courier Code { get; init; }

		[XmlProperty("@via")]
		public Courier Via { get; init; }

		[XmlProperty("@tally")]
		[JsonProperty(EnumFormat = JsonEnumFormat.Number)]
		public Courier Tally { get; init; }

		public Access Rights { get; init; }

	}

	/// <summary>The member's enum vocabulary held against a member whose value is a COLLECTION (or a dictionary) of enums</summary>
	public sealed record Route
	{

		[JsonProperty(EnumFormat = JsonEnumFormat.Number)]
		[XmlProperty(ItemName = "hop")]
		public List<Courier>? Hops { get; init; }

		[JsonProperty(EnumFormat = JsonEnumFormat.Number)]
		public Dictionary<string, Courier>? Legs { get; init; }

	}

	/// <summary>An enum backed by a byte: its numeric form must go through the byte family, not a forced cast to int</summary>
	public enum Weight : byte
	{
		Light = 1,
		Heavy = 200,
	}

	/// <summary>An enum backed by a long, whose declared value does not fit in an int at all</summary>
	public enum Distance : long
	{
		Near = 1,
		Far = 9007199254740993,
	}

	/// <summary>The two non-int-backed enums, in both wire forms (the label, and the numeric override)</summary>
	public sealed record Parcel
	{

		public Weight Heft { get; init; }

		[JsonProperty(EnumFormat = JsonEnumFormat.Number)]
		public Weight Code { get; init; }

		public Distance Reach { get; init; }

		[JsonProperty(EnumFormat = JsonEnumFormat.Number)]
		public Distance Span { get; init; }

	}

	/// <summary>The per-member overrides of the null rule, in the JSON vocabulary the XML wire reuses as-is</summary>
	public sealed record Flags
	{

		[JsonIgnore(Condition = JsonIgnoreCondition.Never)]
		public string? Always { get; init; }

		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Sometimes { get; init; }

		public string? Plain { get; init; }

	}

	[JsonDerivedType(typeof(Ebook), "ebook")]
	[JsonDerivedType(typeof(Paper), "paper")]
	public abstract record Media
	{
		public required string Title { get; init; }
	}

	public sealed record Ebook : Media
	{
		public int Bytes { get; init; }
	}

	public sealed record Paper : Media
	{
		public int Pages { get; init; }
	}

	/// <summary>Derives from <see cref="Media"/> but is declared nowhere: the runtime type the switch cannot place</summary>
	public sealed record Audio : Media
	{
		public int Seconds { get; init; }
	}

	/// <summary>A CONCRETE polymorphic root: an instance of the root type itself is a case the modern switch deliberately does not carry</summary>
	[JsonDerivedType(typeof(Coupe), "coupe")]
	public record Vehicle
	{
		public required string Plate { get; init; }
	}

	public sealed record Coupe : Vehicle
	{
		public int Doors { get; init; }
	}

	/// <summary>Member converter answering for BOTH formats: the JSON literal pair, and the XML element it writes itself</summary>
	public sealed class BitFlagConverter : IJsonMemberConverter<bool>, ICrystalXmlSerializer<bool>
	{

		public JsonValue Pack(bool instance, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> JsonString.Return(instance ? "1" : "0");

		public bool Unpack(JsonValue value, ICrystalJsonTypeResolver? resolver)
			=> value is JsonString s ? s.Value is "1" : value.ToBoolean();

		public void WriteXml<TEmitter>(ref TEmitter emitter, bool value, CrystalJsonSettings? settings = null, string? rootName = null)
			where TEmitter : struct, IXmlEmitter
		{
			var name = XmlName.Create(rootName ?? "bit");
			emitter.WriteStartElement(in name);
			emitter.WriteRawAscii(value ? "1" : "0");
			emitter.WriteEndElement(in name);
		}

	}

	public sealed record Switchboard
	{

		[JsonConvertWith(typeof(BitFlagConverter))]
		public bool Live { get; init; }

	}

	/// <summary>A member with no XML projection at all: no lexical form, no generated serializer, no hook</summary>
	public sealed record Exotic
	{

		public DateTimeOffset When { get; init; }

	}

	/// <summary>A self-referencing DTO: the shape whose CYCLIC instances have no XML representation at all</summary>
	/// <remarks><c>Next</c> is settable (not <c>init</c>) on purpose, since building a cycle means assigning it after the fact.</remarks>
	public sealed record Chain
	{

		public string? Label { get; init; }

		public Chain? Next { get; set; }

	}

	[CrystalJsonConverter(CrystalJsonSerializerDefaults.Web)]
	[CrystalXmlOutput]
	[CrystalJsonSerializable(typeof(Book))]
	[CrystalJsonSerializable(typeof(Shelf))]
	[CrystalJsonSerializable(typeof(Ledger))]
	[CrystalJsonSerializable(typeof(Sample))]
	[CrystalJsonSerializable(typeof(Marker))]
	[CrystalJsonSerializable(typeof(Wrapper))]
	[CrystalJsonSerializable(typeof(Flags))]
	[CrystalJsonSerializable(typeof(Media))]
	[CrystalJsonSerializable(typeof(Ebook))]
	[CrystalJsonSerializable(typeof(Paper))]
	[CrystalJsonSerializable(typeof(Vehicle))]
	[CrystalJsonSerializable(typeof(Coupe))]
	[CrystalJsonSerializable(typeof(Switchboard))]
	[CrystalJsonSerializable(typeof(Exotic))]
	[CrystalJsonSerializable(typeof(Chain))]
	[CrystalJsonSerializable(typeof(Dispatch))]
	[CrystalJsonSerializable(typeof(Route))]
	[CrystalJsonSerializable(typeof(Parcel))]
	public static partial class AcmeSerializers
	{
	}

	/// <summary>A second container over the same DTO, whose CONTAINER default picks another dictionary shape</summary>
	[CrystalJsonConverter(CrystalJsonSerializerDefaults.Web)]
	[CrystalXmlOutput(DictionaryFormat = XmlDictionaryFormat.KeyValueElements)]
	[CrystalJsonSerializable(typeof(Book))]
	public static partial class AcmeLegacySerializers
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
	using SnowBank.Serialization.Json.CodeGen.Tests.Acme;

	/// <summary>Runs the code the generator emitted for the MODERN XML profile</summary>
	/// <remarks>Every fact here executes generated code: the probe types live in this project, which references the
	/// generator as an analyzer on itself, so a wrong emission is either a build failure or a failing assertion, never
	/// a difference of opinion about what the generator would have produced.</remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class XmlModernEmissionFacts : SimpleTest
	{

		private static Book MakeBook() => new()
		{
			Id = 42,
			Title = "Dune",
			Tags = [ "sf", "space" ],
			Scores = { ["math"] = 12 },
		};

		private const string BookXml = """<book id="42"><title>Dune</title><tags><tag>sf</tag><tag>space</tag></tags><scores><math>12</math></scores></book>""";

		#region The design document's example...

		[Test]
		public void Test_The_Design_Example_Produces_The_Documented_Xml()
		{
			var book = MakeBook();

			string xml = AcmeSerializers.Book.ToXmlText(book);
			Log($"XML : {xml}");

			// the byte-for-byte document from the design document, section 6
			Assert.That(xml, Is.EqualTo(BookXml));
		}

		[Test]
		public void Test_The_Json_Of_The_Design_Example_Is_Unchanged()
		{
			var book = MakeBook();

			string json = AcmeSerializers.Book.ToJsonText(book, CrystalJsonSettings.JsonCompact);
			Log($"JSON: {json}");

			// the XML surface is additive: the JSON wire of the same DTO is the one the design document pairs it with
			Assert.That(json, Is.EqualTo("""{"id":42,"title":"Dune","tags":["sf","space"],"scores":{"math":12}}"""));
		}

		#endregion

		#region Null members...

		[Test]
		public void Test_A_Null_Member_Is_Absent_By_Default()
		{
			// Subtitle is null in MakeBook(), and does not appear anywhere in the document
			Assert.That(AcmeSerializers.Book.ToXmlText(MakeBook()), Does.Not.Contain("subtitle"));
		}

		[Test]
		public void Test_A_Null_Member_Becomes_Nil_With_Null_Members()
		{
			string xml = AcmeSerializers.Book.ToXmlText(MakeBook(), CrystalJsonSettings.Json.WithNullMembers());
			Log($"XML : {xml}");

			// nil is the only unambiguous marker: an empty element would be indistinguishable from an empty string
			Assert.That(xml, Is.EqualTo("""<book id="42"><title>Dune</title><tags><tag>sf</tag><tag>space</tag></tags><scores><math>12</math></scores><subtitle nil="true" /></book>"""));
		}

		[Test]
		public void Test_A_Null_Value_Still_Produces_The_Root_Element()
		{
			using (Assert.EnterMultipleScope())
			{
				Assert.That(AcmeSerializers.Book.ToXmlText(null), Is.EqualTo("<book />"), "a document needs a root element");
				Assert.That(AcmeSerializers.Book.ToXmlText(null, CrystalJsonSettings.Json.WithNullMembers()), Is.EqualTo("""<book nil="true" />"""), "and it follows the same nil rule as a null member");
			}
		}

		[Test]
		public void Test_The_Per_Member_Ignore_Conditions_Override_The_Settings()
		{
			using (Assert.EnterMultipleScope())
			{
				// Never = present even as a null, whatever the settings say; WhenWritingNull = absent, same
				Assert.That(AcmeSerializers.Flags.ToXmlText(new Flags()), Is.EqualTo("""<flags><always nil="true" /></flags>"""));
				Assert.That(AcmeSerializers.Flags.ToXmlText(new Flags(), CrystalJsonSettings.Json.WithNullMembers()), Is.EqualTo("""<flags><always nil="true" /><plain nil="true" /></flags>"""));
				Assert.That(AcmeSerializers.Flags.ToXmlText(new Flags() { Always = "a", Sometimes = "b", Plain = "c" }), Is.EqualTo("<flags><always>a</always><sometimes>b</sometimes><plain>c</plain></flags>"));
			}
		}

		#endregion

		#region Collections...

		[Test]
		public void Test_A_Collection_Without_An_Item_Name_Repeats_The_Member_Name()
		{
			string xml = AcmeSerializers.Shelf.ToXmlText(new Shelf { Labels = [ "a", "b" ], Codes = [ 7 ] });
			Log($"XML : {xml}");

			// the Newtonsoft-compatible default: no wrapper element at all
			Assert.That(xml, Is.EqualTo("<shelf><labels>a</labels><labels>b</labels><codes>7</codes></shelf>"));
		}

		[Test]
		public void Test_An_Empty_Collection_Writes_Nothing_When_Unwrapped_And_The_Wrapper_When_Wrapped()
		{
			using (Assert.EnterMultipleScope())
			{
				// unwrapped: there is no element to write, since every element WAS an item
				Assert.That(AcmeSerializers.Shelf.ToXmlText(new Shelf { Labels = [ ], Codes = [ ] }), Is.EqualTo("<shelf />"));

				// wrapped: the wrapper exists independently of its items, so it stays (self-closed)
				Assert.That(AcmeSerializers.Book.ToXmlText(new Book { Id = 1, Title = "x", Tags = [ ] }), Is.EqualTo("""<book id="1"><title>x</title><tags /><scores /></book>"""));
			}
		}

		[Test]
		public void Test_A_Null_Collection_Follows_The_Null_Member_Rules()
		{
			using (Assert.EnterMultipleScope())
			{
				Assert.That(AcmeSerializers.Shelf.ToXmlText(new Shelf()), Is.EqualTo("<shelf />"), "absent by default, like any null member");
				Assert.That(AcmeSerializers.Shelf.ToXmlText(new Shelf(), CrystalJsonSettings.Json.WithNullMembers()), Is.EqualTo("""<shelf><labels nil="true" /><codes nil="true" /></shelf>"""), "and nil when the settings ask");
			}
		}

		#endregion

		#region Dictionaries...

		[Test]
		public void Test_The_Four_Dictionary_Shapes()
		{
			var ledger = new Ledger
			{
				Direct = { ["math"] = 12 },
				Tagged = { ["math"] = 12 },
				Paired = { ["math"] = 12 },
				Nested = { ["math"] = 12 },
			};

			string xml = AcmeSerializers.Ledger.ToXmlText(ledger);
			Log($"XML : {xml}");

			Assert.That(
				xml,
				Is.EqualTo(
					"<ledger>"
					+ "<direct><math>12</math></direct>"
					+ """<tagged><score key="math">12</score></tagged>"""
					+ """<paired><score key="math" value="12" /></paired>"""
					+ "<nested><entry><Key>math</Key><Value>12</Value></entry></nested>"
					+ "</ledger>"));
		}

		[Test]
		public void Test_A_Direct_Dictionary_Key_That_Is_Not_An_Xml_Name_Is_Refused_Loudly()
		{
			// Direct names the element after the key: a key that is not an NCName has no representation at all, and
			// silently mangling it would produce a document that does not parse
			var book = new Book { Id = 1, Title = "x", Scores = { ["not a name"] = 1 } };

			Assert.That(
				() => AcmeSerializers.Book.ToXmlText(book),
				Throws.InstanceOf<CrystalXmlInvalidNameException>().With.Property("Name").EqualTo("not a name"));
		}

		[Test]
		public void Test_The_Container_Default_Selects_The_Dictionary_Shape()
		{
			// the same DTO, in a container whose default is KeyValueElements: no member of Book says anything about
			// dictionaries, so the container's default is what decides
			string xml = AcmeLegacySerializers.Book.ToXmlText(MakeBook());
			Log($"XML : {xml}");

			Assert.That(xml, Does.Contain("<scores><entry><Key>math</Key><Value>12</Value></entry></scores>"));
		}

		#endregion

		#region Scalars...

		[Test]
		public void Test_Every_Scalar_Family_Reaches_Its_Formatter()
		{
			var sample = new Sample
			{
				Flag = true,
				Ratio = 1.5d,
				Price = 12.50m,
				Initial = 'A',
				Kind = Genre.SciFi,
				Stamp = new DateTime(2026, 8, 3, 12, 34, 56, DateTimeKind.Utc),
				Span = TimeSpan.FromMinutes(93),
				Key = Guid.Parse("2f1e4f1a-0000-4000-8000-0123456789ab"),
				Blob = [ 1, 2, 3 ],
				Link = new Uri("http://acme.local/a?x=1&y=2"),
			};

			string xml = AcmeSerializers.Sample.ToXmlText(sample);
			Log($"XML : {xml}");

			Assert.That(
				xml,
				Is.EqualTo(
					"<sample>"
					+ "<flag>true</flag>"
					+ "<ratio>1.5</ratio>"
					+ "<price>12.50</price>"
					+ "<initial>A</initial>"
					+ "<kind>SciFi</kind>"
					+ "<stamp>2026-08-03T12:34:56Z</stamp>"
					+ "<span>PT1H33M</span>"
					+ "<key>2f1e4f1a-0000-4000-8000-0123456789ab</key>"
					+ "<blob>AQID</blob>"
					+ "<link>http://acme.local/a?x=1&amp;y=2</link>"
					+ "</sample>"),
				"a decimal keeps its scale, a char is the character itself (not its code point), a duration is ISO 8601, a byte array is base64, and a URI's ampersand is escaped as text");
		}

		[Test]
		public void Test_A_Nullable_Scalar_Is_Absent_When_Null_And_Written_When_Present()
		{
			using (Assert.EnterMultipleScope())
			{
				Assert.That(AcmeSerializers.Sample.ToXmlText(new Sample()), Does.Not.Contain("maybe"));
				Assert.That(AcmeSerializers.Sample.ToXmlText(new Sample { Maybe = 7 }), Does.Contain("<maybe>7</maybe>"));
			}
		}

		[Test]
		public void Test_A_Member_With_No_Xml_Projection_Fails_At_That_Member()
		{
			// no lexical form, no generated serializer, no hook: there is nothing to write, and inventing a text form for
			// it would be exactly the silent guess this wire refuses. The JSON side of the same container is unaffected.
			using (Assert.EnterMultipleScope())
			{
				Assert.That(
					() => AcmeSerializers.Exotic.ToXmlText(new Exotic()),
					Throws.InstanceOf<CrystalXmlNotSupportedException>().With.Message.Contains("When"),
					"the exception names the member, not just the type");
				Assert.That(AcmeSerializers.Exotic.ToJsonText(new Exotic(), CrystalJsonSettings.JsonCompact), Does.Contain("when"));
			}
		}

		[Test]
		public void Test_Text_Content_Is_Escaped()
		{
			string xml = AcmeSerializers.Book.ToXmlText(new Book { Id = 1, Title = "a < b & c > d" });
			Log($"XML : {xml}");

			Assert.That(xml, Does.Contain("<title>a &lt; b &amp; c &gt; d</title>"));
		}

		#endregion

		#region Enums...

		[Test]
		public void Test_An_Enum_Writes_The_Same_Label_As_The_Json_String_Form()
		{
			var dispatch = new Dispatch { Mode = Courier.Foot, Via = Courier.Foot };

			string xml = AcmeSerializers.Dispatch.ToXmlText(dispatch);
			string json = AcmeSerializers.Dispatch.ToJsonText(dispatch, CrystalJsonSettings.Json.WithEnumAsStrings());
			Log($"XML : {xml}");
			Log($"JSON: {json}");

			using (Assert.EnterMultipleScope())
			{
				// the label comes from the generated switch, not from Enum.ToString(), and it is the token the JSON side
				// writes for the same value: one label, two wires
				Assert.That(xml, Does.Contain("<mode>on-foot</mode>"), "[EnumMember(Value = ...)] renames the value on the XML wire too");
				Assert.That(xml, Does.Contain("via=\"on-foot\""), "and in an attribute position as well");
				Assert.That(json, Does.Contain("\"on-foot\""), "which is exactly the label the JSON string form uses");
			}
		}

		[Test]
		public void Test_An_Enum_Member_With_EnumFormat_Number_Writes_The_Numeric_Form()
		{
			var dispatch = new Dispatch { Mode = Courier.Bike, Code = Courier.Bike, Via = Courier.Foot, Tally = Courier.Bike, Rights = Access.Full };

			string xml = AcmeSerializers.Dispatch.ToXmlText(dispatch);
			string json = AcmeSerializers.Dispatch.ToJsonText(dispatch, CrystalJsonSettings.JsonCompact);
			Log($"XML : {xml}");
			Log($"JSON: {json}");

			using (Assert.EnterMultipleScope())
			{
				// [JsonProperty(EnumFormat = Number)] forces the numeric form on BOTH wires: honoring it on one only is the
				// silent cross-wire divergence this surface refuses
				Assert.That(
					xml,
					Is.EqualTo("<dispatch via=\"on-foot\" tally=\"1\"><mode>Bike</mode><code>1</code><rights>Full</rights></dispatch>"),
					"a value with no token keeps its declared name, a declared flags combination keeps its own label");
				Assert.That(json, Does.Contain("\"code\":1"), "and the JSON side of the same member says the same thing");
			}
		}

		[Test]
		public void Test_An_Undeclared_Enum_Value_Falls_Back_To_Its_Numeric_Form()
		{
			// the same answer Enum.ToString() gives, reached without it: the switch has no case for 7, and the default arm
			// formats the underlying integer
			Assert.That(AcmeSerializers.Dispatch.ToXmlText(new Dispatch { Mode = (Courier) 7 }), Does.Contain("<mode>7</mode>"));
		}

		[Test]
		public void Test_An_Undeclared_Flags_Combination_Is_Refused_Loudly()
		{
			// a reflection-free rendering of an arbitrary flags combination would have to invent a separator this profile
			// never specified, so the wire refuses it at the member instead of guessing one
			Assert.That(
				() => AcmeSerializers.Dispatch.ToXmlText(new Dispatch { Rights = (Access) 5 }),
				Throws.InstanceOf<CrystalXmlNotSupportedException>().With.Message.Contains("Access"));
		}

		[Test]
		public void Test_The_Members_EnumFormat_Does_Not_Leak_To_The_Items_Of_A_Collection()
		{
			var route = new Route { Hops = [ Courier.Foot, Courier.Bike ], Legs = new() { ["first"] = Courier.Bike } };

			string xml = AcmeSerializers.Route.ToXmlText(route);
			Log($"XML : {xml}");

			// [JsonProperty(EnumFormat = Number)] is MEMBER vocabulary, and the member here is the COLLECTION, not its items:
			// an item (or a dictionary value) carries none of it, so it keeps the label form
			Assert.That(xml, Is.EqualTo("<route><hops><hop>on-foot</hop><hop>Bike</hop></hops><legs><first>Bike</first></legs></route>"));
		}

		[Test]
		public void Test_An_Enum_That_Is_Not_Int_Backed_Renders_Through_Its_Own_Underlying_Family()
		{
			var parcel = new Parcel { Heft = Weight.Heavy, Code = Weight.Heavy, Reach = Distance.Far, Span = Distance.Far };

			string xml = AcmeSerializers.Parcel.ToXmlText(parcel);
			Log($"XML : {xml}");

			using (Assert.EnterMultipleScope())
			{
				// a long-backed value forced through the int family would not survive the cast, and a byte-backed one would
				// be widened for no reason: both go through the family their own underlying type names
				Assert.That(xml, Is.EqualTo("<parcel><heft>Heavy</heft><code>200</code><reach>Far</reach><span>9007199254740993</span></parcel>"));

				// and the UNDECLARED fallback arm of the generated switch takes the same family
				Assert.That(
					AcmeSerializers.Parcel.ToXmlText(new Parcel { Heft = (Weight) 250, Reach = (Distance) 5000000000L }),
					Is.EqualTo("<parcel><heft>250</heft><code>0</code><reach>5000000000</reach><span>0</span></parcel>"));
			}
		}

		#endregion

		#region Nested types and hooks...

		[Test]
		public void Test_A_Nested_Generated_Type_Is_Written_Under_The_Member_Name()
		{
			string xml = AcmeSerializers.Wrapper.ToXmlText(new Wrapper { Inner = MakeBook() });
			Log($"XML : {xml}");

			// the child's own root name is NOT used here: the parent names the element, the child fills it
			Assert.That(xml, Is.EqualTo("<wrapper>" + BookXml.Replace("book", "inner") + "</wrapper>"));
		}

		[Test]
		public void Test_A_Member_Whose_Type_Writes_Its_Own_Xml_Dispatches_To_The_Hook()
		{
			string xml = AcmeSerializers.Wrapper.ToXmlText(new Wrapper { Mark = new Signature { Note = "ok" } });
			Log($"XML : {xml}");

			// the parent names the element, and the CONTENT comes from the hook, not from Signature's own members: the
			// crawler enrolls every member type, so Signature has a generated body of its own, and that body is what
			// dispatches to the hook (the member-level dispatch in the emitter is the fallback for a member type the
			// crawler leaves out, which the same rule covers)
			Assert.That(xml, Is.EqualTo("<wrapper><mark>ok</mark></wrapper>"));
		}

		[Test]
		public void Test_A_Member_Type_The_Crawler_Skips_Reaches_Its_Hook_Through_The_Member_Branch()
		{
			// NodaTime.Stamp lives in a namespace the crawler skips, so it has NO generated body of its own: the only
			// code that can write it is the member-level ICrystalXmlSerializable dispatch, which owns the element shell
			string xml = AcmeSerializers.Wrapper.ToXmlText(new Wrapper { Tick = new global::NodaTime.Stamp("now") });
			Log($"XML : {xml}");

			Assert.That(xml, Is.EqualTo("<wrapper><tick>now</tick></wrapper>"));
		}

		[Test]
		public void Test_An_Enrolled_Type_That_Writes_Its_Own_Xml_Keeps_Its_Shell()
		{
			string xml = AcmeSerializers.Marker.ToXmlText(new Marker { Note = "hi" });
			Log($"XML : {xml}");

			// enrolled AND hooked: its generated converter writes the root element, then hands the content to the hook,
			// so its own members are never written by the generated body
			Assert.That(xml, Is.EqualTo("<marker>hi</marker>"));
		}

		[Test]
		public void Test_A_Member_Converter_With_An_Xml_Facet_Owns_The_Member()
		{
			using (Assert.EnterMultipleScope())
			{
				// the converter writes the element itself, under the member's XML name
				Assert.That(AcmeSerializers.Switchboard.ToXmlText(new Switchboard { Live = true }), Is.EqualTo("<switchboard><live>1</live></switchboard>"));
				// and the JSON side keeps going through the same converter's Pack
				Assert.That(AcmeSerializers.Switchboard.ToJsonText(new Switchboard { Live = true }, CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"live":"1"}"""));
			}
		}

		#endregion

		#region Polymorphism...

		[Test]
		public void Test_The_Runtime_Type_Selects_The_Body_And_Carries_A_Discriminator()
		{
			var ebook = new Ebook { Title = "Dune", Bytes = 1024 };
			var paper = new Paper { Title = "Dune", Pages = 412 };

			string fromBase = AcmeSerializers.Media.ToXmlText(ebook);
			string fromPaper = AcmeSerializers.Media.ToXmlText(paper);
			Log($"XML : {fromBase}");
			Log($"XML : {fromPaper}");

			using (Assert.EnterMultipleScope())
			{
				// the discriminator is an ANNOTATION: an attribute, written first, named 'type' (the JSON '$type' is not
				// a legal XML name), and the element keeps the name the CALLER's root resolved to
				Assert.That(fromBase, Is.EqualTo("""<media type="ebook"><title>Dune</title><bytes>1024</bytes></media>"""));
				Assert.That(fromPaper, Is.EqualTo("""<media type="paper"><title>Dune</title><pages>412</pages></media>"""));
			}
		}

		[Test]
		public void Test_A_Derived_Serializer_Called_Directly_Still_Carries_Its_Discriminator()
		{
			Assert.That(
				AcmeSerializers.Ebook.ToXmlText(new Ebook { Title = "Dune", Bytes = 1024 }),
				Is.EqualTo("""<ebook type="ebook"><title>Dune</title><bytes>1024</bytes></ebook>"""));
		}

		[Test]
		public void Test_A_Runtime_Type_Outside_The_Graph_Is_Refused_Loudly()
		{
			// Audio derives from Media but is declared nowhere: there is no generated body to write it, and writing it
			// through the base body would silently drop everything the subtype adds
			Assert.That(
				() => AcmeSerializers.Media.ToXmlText(new Audio { Title = "Dune", Seconds = 3600 }),
				Throws.InstanceOf<CrystalXmlUnknownTypeException>().With.Property("Type").EqualTo(typeof(Audio)));
		}

		[Test]
		public void Test_An_Instance_Of_A_Concrete_Polymorphic_Root_Is_Refused_On_This_Wire()
		{
			// DELIBERATE DIVERGENCE from the compat wire, which writes the root's own body for this exact value.
			// The modern wire matches the JSON side instead: an instance of the root type carries no discriminator
			// there either, so a reader could not tell it from a subtype whose annotation went missing. It is refused
			// rather than written under a shape the reader cannot interpret.
			Assert.That(
				() => AcmeSerializers.Vehicle.ToXmlText(new Vehicle { Plate = "AB-123-CD" }),
				Throws.InstanceOf<CrystalXmlUnknownTypeException>().With.Property("Type").EqualTo(typeof(Vehicle)));
		}

		[Test]
		public void Test_A_Declared_Subtype_Of_A_Concrete_Polymorphic_Root_Is_Written()
		{
			// the other half of the pair: the subtype the switch DOES carry is written, annotated, as usual
			Assert.That(
				AcmeSerializers.Vehicle.ToXmlText(new Coupe { Plate = "AB-123-CD", Doors = 2 }),
				Is.EqualTo("""<vehicle type="coupe"><plate>AB-123-CD</plate><doors>2</doors></vehicle>"""));
		}

		#endregion

		#region Root name override...

		[Test]
		public void Test_A_Root_Name_Override_Replaces_The_Type_Name()
		{
			Assert.That(AcmeSerializers.Book.ToXmlText(MakeBook(), rootName: "data"), Is.EqualTo(BookXml.Replace("book", "data")));
		}

		[Test]
		public void Test_An_Invalid_Root_Name_Override_Is_Refused_Loudly()
		{
			// the one place caller text becomes an XML name, so the one place it is validated
			Assert.That(
				() => AcmeSerializers.Book.ToXmlText(MakeBook(), rootName: "not a name"),
				Throws.InstanceOf<CrystalXmlInvalidNameException>().With.Property("Name").EqualTo("not a name"));
		}

		#endregion

		#region The five outputs...

		[Test]
		public void Test_All_The_Outputs_Agree()
		{
			var book = MakeBook();

			string text = AcmeSerializers.Book.ToXmlText(book);
			var slice = AcmeSerializers.Book.ToXmlSlice(book);
			byte[] bytes = AcmeSerializers.Book.ToXmlBytes(book);

			var ms = new MemoryStream();
			AcmeSerializers.Book.WriteXmlTo(ms, book);

			var sw = new StringWriter();
			AcmeSerializers.Book.WriteXmlTo(sw, book);

			var buffer = new ArrayBufferWriter<byte>();
			AcmeSerializers.Book.WriteXmlTo(buffer, book);

			var doc = AcmeSerializers.Book.ToXDocument(book);

			var xmlOut = new StringBuilder();
			using (var writer = XmlWriter.Create(xmlOut, new XmlWriterSettings() { OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment }))
			{
				AcmeSerializers.Book.WriteXmlTo(writer, book);
			}

			Log($"text  : {text}");
			Log($"slice : {slice.ToStringUtf8()}");
			Log($"stream: {Encoding.UTF8.GetString(ms.ToArray())}");
			Log($"doc   : {doc}");

			using (Assert.EnterMultipleScope())
			{
				Assert.That(text, Is.EqualTo(BookXml), "the char core");
				Assert.That(slice.ToStringUtf8(), Is.EqualTo(BookXml), "the byte core, decoded");
				Assert.That(Encoding.UTF8.GetString(bytes), Is.EqualTo(BookXml), "the byte array");
				Assert.That(Encoding.UTF8.GetString(ms.ToArray()), Is.EqualTo(BookXml), "the stream");
				Assert.That(sw.ToString(), Is.EqualTo(BookXml), "the text writer");
				Assert.That(Encoding.UTF8.GetString(buffer.WrittenSpan), Is.EqualTo(BookXml), "the buffer writer");

				// the two infoset outputs promise equivalence, not bytes, so they are compared as trees
				Assert.That(XNode.DeepEquals(doc.Root, XDocument.Parse(text).Root), Is.True, "the XDocument, as a tree");
				Assert.That(XNode.DeepEquals(XDocument.Parse(xmlOut.ToString()).Root, XDocument.Parse(text).Root), Is.True, "the XmlWriter output, as a tree");
			}
		}

		[Test]
		public void Test_The_Serializer_Facet_Is_On_The_Default_Instance()
		{
			// what makes the generated converter usable by code that only knows the interface
			ICrystalXmlSerializer<Book> serializer = AcmeSerializers.Book.Default;

			Assert.That(CrystalXml.ToText(serializer, MakeBook()), Is.EqualTo(BookXml));
		}

		#endregion

		#region Generation-time refusals...

		/// <summary>Runs the generator over a probe and returns the <c>#error</c> directives its output carries</summary>
		/// <remarks>An <c>#error</c> is not a generator diagnostic: it only exists once the emitted source is compiled, where it
		/// surfaces as CS1029. These two shapes are refused there rather than by a CXML descriptor, so this is where they are pinned.</remarks>
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
		public void Test_An_Attribute_Shaped_Dictionary_With_A_Non_Scalar_Value_Fails_The_Build()
		{
			// the two attribute shapes carry the value as TEXT: a value type with no lexical form has nothing to put there,
			// and the shape is picked in the source, so the answer belongs at build time
			var errors = EmissionErrorsOf("""
					public sealed record ProbePart
					{
						public int Value { get; set; }
					}

					public sealed record ProbeDto
					{
						[SnowBank.Data.Xml.XmlProperty(DictionaryFormat = SnowBank.Data.Xml.XmlDictionaryFormat.KeyValueAttributes)]
						public System.Collections.Generic.Dictionary<string, ProbePart>? Map { get; set; }
					}

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.Xml.CrystalXmlOutput]
					[SnowBank.Data.Json.CrystalJsonSerializable(typeof(ProbeDto))]
					public static partial class ProbeConverters
					{
					}
				""");

			Assert.That(errors, Has.Exactly(1).Contains("KeyValueAttributes"), "the message names the shape that cannot hold the value, and the two that can");
		}

		[Test]
		public void Test_A_DataContract_Name_That_Is_Not_An_Xml_Name_Fails_The_Build()
		{
			// the root name is the one name no [XmlProperty] can override: a contract name written for another wire would
			// produce a document that does not parse, so it is refused where it is resolved
			var errors = EmissionErrorsOf("""
					[System.Runtime.Serialization.DataContract(Name = "not a name")]
					public sealed record ProbeDto
					{
						[System.Runtime.Serialization.DataMember(Name = "id")]
						public int Id { get; set; }
					}

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.Xml.CrystalXmlOutput]
					[SnowBank.Data.Json.CrystalJsonSerializable(typeof(ProbeDto))]
					public static partial class ProbeConverters
					{
					}
				""");

			Assert.That(errors, Has.Exactly(1).Contains("not a name"), "the message quotes the offending name");
		}

		#endregion

		#region Cycles and depth...

		/// <summary>Builds an ACYCLIC chain of <paramref name="length"/> nodes, so the deepest element sits at depth <c>length - 1</c></summary>
		private static Chain MakeChain(int length)
		{
			var head = new Chain();
			var tail = head;
			for (int i = 1; i < length; i++)
			{
				var next = new Chain();
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
			// a cycle has no representation on either wire (there is no z:Id/z:Ref form here), so the emission must stop
			// with a typed, CATCHABLE error: before the depth guard existed this recursed ~2300 frames deep and killed the
			// whole process with a StackOverflowException, which .NET cannot catch
			var a = new Chain { Label = "a" };
			var b = new Chain { Label = "b" };
			a.Next = b;
			b.Next = a;

			Assert.That(
				() => AcmeSerializers.Chain.ToXmlText(a),
				Throws.InstanceOf<CrystalXmlCycleException>().With.Property("Type").EqualTo(typeof(Chain)).And.Message.Contains(CrystalXml.MaxDepth.ToString(CultureInfo.InvariantCulture)),
				"the exception names the type the cycle was detected on, and the cap it hit");
		}

		[Test]
		public void Test_A_Deep_Acyclic_Chain_Up_To_The_Cap_Is_Written_In_Full()
		{
			// the guard must not eat a legitimate (if absurd) deep document: a chain exactly as deep as the cap allows
			// still serializes, which is what pins WHERE the line sits
			string xml = AcmeSerializers.Chain.ToXmlText(MakeChain(CrystalXml.MaxDepth));

			using (Assert.EnterMultipleScope())
			{
				Assert.That(xml, Does.StartWith("<chain"), "the root element");
				Assert.That(CountOf(xml, "<next"), Is.EqualTo(CrystalXml.MaxDepth - 1), "one nested element per node past the root (the deepest one self-closes, hence the open-tag prefix rather than \"<next>\")");
			}
		}

		[Test]
		public void Test_A_Deep_Acyclic_Chain_Past_The_Cap_Throws_The_Same_Typed_Exception()
		{
			// one node deeper than the cap: the guard cannot tell a cycle from a graph that is simply too deep, and both
			// answers are the same typed exception rather than a crash
			Assert.That(
				() => AcmeSerializers.Chain.ToXmlText(MakeChain(CrystalXml.MaxDepth + 1)),
				Throws.InstanceOf<CrystalXmlCycleException>().With.Property("Type").EqualTo(typeof(Chain)));
		}

		#endregion

	}

}
