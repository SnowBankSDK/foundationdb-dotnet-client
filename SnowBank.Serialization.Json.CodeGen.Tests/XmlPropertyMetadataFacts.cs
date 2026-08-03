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

namespace SnowBank.Serialization.Json.CodeGen.Tests
{
	using System.Collections.Immutable;
	using Microsoft.CodeAnalysis;

	/// <summary>Pins how the generator reads the MEMBER-level XML vocabulary: <c>[XmlProperty]</c> (including the <c>@</c> name sugar), the DCS ordering and default-emission flags of <c>[DataMember]</c>, the data contract's own name values, and the five member-level refusals (<c>CXML0003</c> to <c>CXML0007</c>)</summary>
	/// <remarks>Parsing only: nothing is emitted for XML yet, so every assertion reads the metadata the parser resolved (through the driver's tracked steps) or the diagnostics it reported.</remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class XmlPropertyMetadataFacts : SimpleTest
	{

		/// <summary>The container attributes of a probe that produces the Modern XML wire (the default shape these facts exercise)</summary>
		private const string ModernContainer = """
				[SnowBank.Data.Json.CrystalJsonConverter]
				[SnowBank.Data.Xml.CrystalXmlOutput]
			""";

		/// <summary>The container attributes of a probe that produces the DataContract XML wire</summary>
		private const string DataContractContainer = """
				[SnowBank.Data.Json.CrystalJsonConverter(SnowBank.Data.Json.CrystalJsonSerializerDefaults.DataContractCompat)]
				[SnowBank.Data.Xml.CrystalXmlOutput]
			""";

		/// <summary>The container attributes of a probe that produces NO XML at all (where the whole XML vocabulary must stay inert)</summary>
		private const string JsonOnlyContainer = """
				[SnowBank.Data.Json.CrystalJsonConverter]
			""";

		/// <summary>Wraps a set of DTO members into a compilable probe (the DTO, a couple of satellite types, and the container that enrols it)</summary>
		private static string Probe(string members, string containerAttributes = ModernContainer, string dtoAttributes = "") => $$"""
			namespace Probe
			{

				public enum ProbeColor
				{
					Red = 0,
					Blue = 1,
				}

				public sealed record ProbePart
				{
					public int Value { get; set; }
				}

			{{dtoAttributes}}
				public sealed record ProbeDto
				{
			{{members}}
				}

			{{containerAttributes}}
				[SnowBank.Data.Json.CrystalJsonSerializable(typeof(ProbeDto))]
				public static partial class ProbeConverters
				{
				}

			}
			""";

		private (Dictionary<string, CrystalJsonContainerMetadata> Containers, ImmutableArray<Diagnostic> Diagnostics) RunOn(string source)
		{
			var compilation = GeneratorProbeHarness.Compile(source);
			Assert.That(
				compilation.GetDiagnostics().Where(static d => d.Severity >= DiagnosticSeverity.Warning),
				Is.Empty,
				"the probe source must compile clean on its own");

			var (containers, diagnostics) = GeneratorProbeHarness.RunGeneratorAndCaptureContainers(compilation);
			foreach (var d in diagnostics)
			{
				Log($"generator: [{d.Severity}] {d}");
			}
			foreach (var (name, metadata) in containers)
			{
				Log($"container: {name}: XmlProfile={metadata.XmlProfile ?? "<null>"}; XmlDictionaryFormat={metadata.XmlDictionaryFormat ?? "<null>"}");
				foreach (var includedType in metadata.IncludedTypes)
				{
					Log($"  type {includedType.Name}: DataContractName={includedType.DataContractName ?? "<null>"}; DataContractNamespace={includedType.DataContractNamespace ?? "<null>"}");
					foreach (var member in includedType.Members)
					{
						Log($"    member {member.MemberName}: Name={member.Name}; XmlName={member.XmlName ?? "<null>"}; XmlIsAttribute={member.XmlIsAttribute}; XmlItemName={member.XmlItemName ?? "<null>"}; XmlDictionaryFormat={member.XmlDictionaryFormat ?? "<null>"}; DataMemberOrder={member.DataMemberOrder?.ToString() ?? "<null>"}; EmitDefaultValue={member.EmitDefaultValue}");
					}
				}
			}
			return (containers, diagnostics);
		}

		/// <summary>Reads back the parsed type metadata of the probe's DTO, asserting the parser accepted the whole probe</summary>
		private CrystalJsonTypeMetadata TypeOf(string source)
		{
			var (containers, diagnostics) = RunOn(source);
			Assert.That(diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error), Is.Empty, "the probe must not be refused by the generator");
			Assert.That(containers.ContainsKey("ProbeConverters"), Is.True, "the parser must have produced metadata for the probe's container");

			var dto = containers["ProbeConverters"].IncludedTypes.SingleOrDefault(static t => t.Name == "ProbeDto");
			Assert.That(dto, Is.Not.Null, "the parser must have produced metadata for the probe's DTO");
			return dto!;
		}

		/// <summary>Reads back the parsed metadata of one member of the probe's DTO</summary>
		private CrystalJsonMemberMetadata MemberOf(string source, string memberName)
		{
			var dto = TypeOf(source);
			var member = dto.Members.SingleOrDefault(m => m.MemberName == memberName);
			Assert.That(member, Is.Not.Null, $"the parser must have produced metadata for member '{memberName}'");
			return member!;
		}

		/// <summary>Asserts that a probe was refused with the expected diagnostic, and returns it (a refusal of the whole wire is worse than a build failure, so all five are errors)</summary>
		private Diagnostic AssertRefusal(string source, string id)
		{
			var (_, diagnostics) = RunOn(source);
			var refusal = diagnostics.FirstOrDefault(d => d.Id == id);
			Assert.That(refusal, Is.Not.Null, $"the probe must have been refused with {id}");
			Assert.That(refusal!.Severity, Is.EqualTo(DiagnosticSeverity.Error), "a silently wrong wire is worse than a build failure");
			return refusal;
		}

		/// <summary>Asserts that a probe was NOT refused with a given diagnostic, and that NO other XML diagnostic fired either (the non-trigger side of every rule)</summary>
		/// <remarks>The second half is what makes these tests worth having: a probe that dodges the rule under test only to trip a neighbouring one is not an accepted shape, and asserting on a single id would let that through.</remarks>
		private void AssertNotReported(string source, string id)
		{
			var (_, diagnostics) = RunOn(source);
			using (Assert.EnterMultipleScope())
			{
				Assert.That(diagnostics.Where(d => d.Id == id), Is.Empty, $"{id} must not fire on this shape");
				Assert.That(diagnostics.Where(static d => d.Id.StartsWith("CXML", StringComparison.Ordinal)), Is.Empty, "and the shape must be accepted whole: no other XML diagnostic either");
			}
		}

		#region The new metadata fields...

		[Test]
		public void Test_XmlProperty_Name_Is_Carried_As_The_Xml_Name()
		{
			// the plain rename: the XML name is carried SEPARATELY from the JSON one, which stays untouched
			var member = MemberOf(Probe("""
						[SnowBank.Data.Xml.XmlProperty("identifier")]
						public int Id { get; set; }
				"""), "Id");

			using (Assert.EnterMultipleScope())
			{
				Assert.That(member.XmlName, Is.EqualTo("identifier"));
				Assert.That(member.Name, Is.EqualTo("Id"), "the JSON name is never touched by the XML vocabulary");
				Assert.That(member.XmlIsAttribute, Is.False, "a plain name projects an element");
			}
		}

		[Test]
		public void Test_XmlProperty_Name_Can_Also_Be_Given_As_A_Named_Argument()
		{
			// the attribute has both a constructor and a settable property: the two spellings must resolve identically
			var member = MemberOf(Probe("""
						[SnowBank.Data.Xml.XmlProperty(Name = "identifier")]
						public int Id { get; set; }
				"""), "Id");

			Assert.That(member.XmlName, Is.EqualTo("identifier"));
		}

		[Test]
		public void Test_XmlProperty_Attribute_Flag_Is_Carried()
		{
			var member = MemberOf(Probe("""
						[SnowBank.Data.Xml.XmlProperty(Attribute = true)]
						public int Id { get; set; }
				"""), "Id");

			using (Assert.EnterMultipleScope())
			{
				Assert.That(member.XmlIsAttribute, Is.True);
				Assert.That(member.XmlName, Is.Null, "no name was given: the XML name still falls back to the JSON one downstream");
			}
		}

		[Test]
		public void Test_XmlProperty_ItemName_Is_Carried()
		{
			var member = MemberOf(Probe("""
						[SnowBank.Data.Xml.XmlProperty("tags", ItemName = "tag")]
						public System.Collections.Generic.List<string>? Tags { get; set; }
				"""), "Tags");

			using (Assert.EnterMultipleScope())
			{
				Assert.That(member.XmlName, Is.EqualTo("tags"));
				Assert.That(member.XmlItemName, Is.EqualTo("tag"));
			}
		}

		[Test]
		public void Test_XmlProperty_DictionaryFormat_Is_Carried_As_Its_Enum_Member_Name()
		{
			// same convention as the container-level format: the NAME is stored, so reordering the runtime enum cannot
			// silently change what the generator resolved
			var member = MemberOf(Probe("""
						[SnowBank.Data.Xml.XmlProperty(DictionaryFormat = SnowBank.Data.Xml.XmlDictionaryFormat.KeyValueAttributes)]
						public System.Collections.Generic.Dictionary<string, int>? Map { get; set; }
				"""), "Map");

			Assert.That(member.XmlDictionaryFormat, Is.EqualTo("KeyValueAttributes"));
		}

		[Test]
		public void Test_A_Member_Without_XmlProperty_Carries_No_Xml_Metadata()
		{
			// the whole vocabulary is per-member opt-in: an un-annotated member of an XML container carries nothing,
			// and everything it needs is derived downstream from its JSON name
			var member = MemberOf(Probe("""
						public int Id { get; set; }
				"""), "Id");

			using (Assert.EnterMultipleScope())
			{
				Assert.That(member.XmlName, Is.Null);
				Assert.That(member.XmlIsAttribute, Is.False);
				Assert.That(member.XmlItemName, Is.Null);
				Assert.That(member.XmlDictionaryFormat, Is.Null);
			}
		}

		#endregion

		#region [DataMember] Order and EmitDefaultValue...

		[Test]
		public void Test_DataMember_Order_Is_Carried()
		{
			var dto = TypeOf(Probe(
				"""
							[System.Runtime.Serialization.DataMember(Order = 2)]
							public int Second { get; set; }

							[System.Runtime.Serialization.DataMember(Order = 0)]
							public int First { get; set; }
					""",
				dtoAttributes: "	[System.Runtime.Serialization.DataContract]"));

			using (Assert.EnterMultipleScope())
			{
				Assert.That(dto.Members.Single(static m => m.MemberName == "Second").DataMemberOrder, Is.EqualTo(2));
				Assert.That(dto.Members.Single(static m => m.MemberName == "First").DataMemberOrder, Is.EqualTo(0), "order zero is a real order, not an absent one");
			}
		}

		[Test]
		public void Test_Absent_DataMember_Order_Is_Carried_As_Unset()
		{
			var member = MemberOf(Probe(
				"""
							[System.Runtime.Serialization.DataMember]
							public int Id { get; set; }
					""",
				dtoAttributes: "	[System.Runtime.Serialization.DataContract]"), "Id");

			Assert.That(member.DataMemberOrder, Is.Null, "an unordered member sorts by name, which is a different rule from ordering at zero");
		}

		[Test]
		public void Test_Negative_DataMember_Order_Is_Carried_As_Unset()
		{
			// DataContractSerializer treats a negative order exactly like an absent one
			var member = MemberOf(Probe(
				"""
							[System.Runtime.Serialization.DataMember(Order = -1)]
							public int Id { get; set; }
					""",
				dtoAttributes: "	[System.Runtime.Serialization.DataContract]"), "Id");

			Assert.That(member.DataMemberOrder, Is.Null);
		}

		[Test]
		public void Test_EmitDefaultValue_Defaults_To_True()
		{
			// the DCS default: only an explicit EmitDefaultValue = false flips it, so the flag cannot be inverted by accident
			var dto = TypeOf(Probe(
				"""
							[System.Runtime.Serialization.DataMember]
							public int Annotated { get; set; }
					""",
				dtoAttributes: "	[System.Runtime.Serialization.DataContract]"));

			Assert.That(dto.Members.Single(static m => m.MemberName == "Annotated").EmitDefaultValue, Is.True);
		}

		[Test]
		public void Test_EmitDefaultValue_False_Is_Carried()
		{
			var member = MemberOf(Probe(
				"""
							[System.Runtime.Serialization.DataMember(EmitDefaultValue = false)]
							public int Id { get; set; }
					""",
				dtoAttributes: "	[System.Runtime.Serialization.DataContract]"), "Id");

			using (Assert.EnterMultipleScope())
			{
				Assert.That(member.EmitDefaultValue, Is.False);
				Assert.That(member.IgnoreCondition, Is.Null, "the raw flag is carried as-is: how it interacts with the JSON ignore conditions is resolved downstream");
			}
		}

		[Test]
		public void Test_A_Member_Without_DataMember_Emits_Its_Default_Value()
		{
			var member = MemberOf(Probe("""
						public int Id { get; set; }
				"""), "Id");

			using (Assert.EnterMultipleScope())
			{
				Assert.That(member.EmitDefaultValue, Is.True);
				Assert.That(member.DataMemberOrder, Is.Null);
			}
		}

		#endregion

		#region The data contract's own name values...

		[Test]
		public void Test_DataContract_Name_And_Namespace_Values_Are_Carried()
		{
			// the DataContract XML wire names the root element after the contract, so the VALUES have to reach the metadata
			// (until now only the attribute's presence was read, which was all the JSON wire needed)
			var dto = TypeOf(Probe(
				"""
							[System.Runtime.Serialization.DataMember]
							public int Id { get; set; }
					""",
				dtoAttributes: """	[System.Runtime.Serialization.DataContract(Name = "Widget", Namespace = "http://acme.example/contracts")]"""));

			using (Assert.EnterMultipleScope())
			{
				Assert.That(dto.DataContractName, Is.EqualTo("Widget"));
				Assert.That(dto.DataContractNamespace, Is.EqualTo("http://acme.example/contracts"));
			}
		}

		[Test]
		public void Test_A_Bare_DataContract_Carries_No_Name_Values()
		{
			var dto = TypeOf(Probe(
				"""
							[System.Runtime.Serialization.DataMember]
							public int Id { get; set; }
					""",
				dtoAttributes: "	[System.Runtime.Serialization.DataContract]"));

			using (Assert.EnterMultipleScope())
			{
				Assert.That(dto.DataContractName, Is.Null, "the contract name defaults to the type name, which is derived downstream");
				Assert.That(dto.DataContractNamespace, Is.Null);
			}
		}

		[Test]
		public void Test_A_Type_Without_DataContract_Carries_No_Name_Values()
		{
			var dto = TypeOf(Probe("""
						public int Id { get; set; }
				"""));

			using (Assert.EnterMultipleScope())
			{
				Assert.That(dto.DataContractName, Is.Null);
				Assert.That(dto.DataContractNamespace, Is.Null);
			}
		}

		#endregion

		#region The '@' name sugar...

		[Test]
		public void Test_Leading_At_Is_Normalized_Into_A_Name_Plus_The_Attribute_Flag()
		{
			// the sugar exists so that the common case reads like the XML it produces; it is resolved HERE, so nothing
			// downstream ever sees a '@' in a name
			var member = MemberOf(Probe("""
						[SnowBank.Data.Xml.XmlProperty("@id")]
						public int Id { get; set; }
				"""), "Id");

			using (Assert.EnterMultipleScope())
			{
				Assert.That(member.XmlName, Is.EqualTo("id"), "the '@' is stripped at parse time");
				Assert.That(member.XmlIsAttribute, Is.True);
			}
		}

		[Test]
		public void Test_Leading_At_Plus_An_Explicit_Attribute_True_Is_Redundant_But_Legal()
		{
			// both spellings say the same thing: saying it twice is noise, not a contradiction
			var member = MemberOf(Probe("""
						[SnowBank.Data.Xml.XmlProperty("@id", Attribute = true)]
						public int Id { get; set; }
				"""), "Id");

			using (Assert.EnterMultipleScope())
			{
				Assert.That(member.XmlName, Is.EqualTo("id"));
				Assert.That(member.XmlIsAttribute, Is.True);
			}
		}

		[Test]
		public void Test_Leading_At_Plus_An_Explicit_Attribute_False_Is_A_Build_Error()
		{
			// the two spellings genuinely disagree, and picking either silently gives a wire the author did not ask for
			var refusal = AssertRefusal(Probe("""
						[SnowBank.Data.Xml.XmlProperty("@id", Attribute = false)]
						public int Id { get; set; }
				"""), "CXML0007");

			Assert.That(refusal.GetMessage(), Does.Contain("Attribute = false"), "the message names the contradiction, not just the name");
		}

		[Test]
		public void Test_An_At_On_Its_Own_Is_A_Build_Error()
		{
			var refusal = AssertRefusal(Probe("""
						[SnowBank.Data.Xml.XmlProperty("@")]
						public int Id { get; set; }
				"""), "CXML0007");

			Assert.That(refusal.GetMessage(), Does.Contain("Id"), "the message names the member the author has to fix");
		}

		[Test]
		public void Test_A_Name_That_Is_Invalid_After_Stripping_The_At_Is_A_Build_Error()
		{
			// the validation applies to what is LEFT: "@1st" strips to "1st", which no XML parser accepts as a name
			AssertRefusal(Probe("""
						[SnowBank.Data.Xml.XmlProperty("@1st")]
						public int Id { get; set; }
				"""), "CXML0007");
		}

		[Test]
		public void Test_An_Invalid_Plain_Name_Is_A_Build_Error()
		{
			// a space in an element name produces unparseable XML: refusing at build time is the only place it can be caught
			AssertRefusal(Probe("""
						[SnowBank.Data.Xml.XmlProperty("not a name")]
						public int Id { get; set; }
				"""), "CXML0007");
		}

		[Test]
		public void Test_A_Name_With_A_Colon_Is_A_Build_Error()
		{
			// a colon is the namespace-prefix separator: a member cannot invent a prefix, so an NCName is required
			AssertRefusal(Probe("""
						[SnowBank.Data.Xml.XmlProperty("ns:id")]
						public int Id { get; set; }
				"""), "CXML0007");
		}

		[Test]
		public void Test_An_Invalid_ItemName_Is_A_Build_Error()
		{
			// the item name lands in the document exactly like the member name does, so it gets the same validation
			AssertRefusal(Probe("""
						[SnowBank.Data.Xml.XmlProperty("tags", ItemName = "not a name")]
						public System.Collections.Generic.List<string>? Tags { get; set; }
				"""), "CXML0007");
		}

		[Test]
		public void Test_A_Valid_Name_Is_Not_Reported()
		{
			// the non-triggering shape: names that are legal NCNames, including the underscore and dash forms
			AssertNotReported(Probe("""
						[SnowBank.Data.Xml.XmlProperty("_private-id.v2")]
						public int Id { get; set; }
				"""), "CXML0007");
		}

		#endregion

		#region CXML0003: Attribute=true on a member the XML wire cannot render as an attribute...

		[Test]
		public void Test_Attribute_On_A_Scalar_Member_Is_Accepted()
		{
			// the whole scalar family the XML formatters cover, plus the two the profile adds (string and enums):
			// every one of them has a lexical form, which is what an attribute value is
			AssertNotReported(Probe("""
						[SnowBank.Data.Xml.XmlProperty("@i")] public int Number { get; set; }
						[SnowBank.Data.Xml.XmlProperty("@s")] public string? Text { get; set; }
						[SnowBank.Data.Xml.XmlProperty("@e")] public ProbeColor Color { get; set; }
						[SnowBank.Data.Xml.XmlProperty("@g")] public System.Guid Key { get; set; }
						[SnowBank.Data.Xml.XmlProperty("@d")] public System.DateTime When { get; set; }
						[SnowBank.Data.Xml.XmlProperty("@t")] public System.TimeSpan Duration { get; set; }
						[SnowBank.Data.Xml.XmlProperty("@u")] public System.Uri? Link { get; set; }
						[SnowBank.Data.Xml.XmlProperty("@b")] public byte[]? Blob { get; set; }
						[SnowBank.Data.Xml.XmlProperty("@n")] public int? Optional { get; set; }
				"""), "CXML0003");
		}

		[Test]
		public void Test_Attribute_On_A_Collection_Is_A_Build_Error()
		{
			var refusal = AssertRefusal(Probe("""
						[SnowBank.Data.Xml.XmlProperty(Attribute = true)]
						public System.Collections.Generic.List<string>? Tags { get; set; }
				"""), "CXML0003");

			Assert.That(refusal.GetMessage(), Does.Contain("Tags"), "the message names the member");
		}

		[Test]
		public void Test_Attribute_On_A_Dictionary_Is_A_Build_Error()
		{
			AssertRefusal(Probe("""
						[SnowBank.Data.Xml.XmlProperty(Attribute = true)]
						public System.Collections.Generic.Dictionary<string, int>? Map { get; set; }
				"""), "CXML0003");
		}

		[Test]
		public void Test_Attribute_On_A_Complex_Type_Is_A_Build_Error()
		{
			// a nested object has no lexical form at all: an attribute could only ever hold a mangled rendering of it
			AssertRefusal(Probe("""
						[SnowBank.Data.Xml.XmlProperty(Attribute = true)]
						public ProbePart? Part { get; set; }
				"""), "CXML0003");
		}

		[Test]
		public void Test_Attribute_Resolved_Through_The_At_Sugar_Is_Checked_Too()
		{
			// the check runs on the RESOLVED flag, so the sugar cannot be used to sneak past it
			AssertRefusal(Probe("""
						[SnowBank.Data.Xml.XmlProperty("@tags")]
						public System.Collections.Generic.List<string>? Tags { get; set; }
				"""), "CXML0003");
		}

		[Test]
		public void Test_A_Collection_Without_The_Attribute_Flag_Is_Not_Reported()
		{
			AssertNotReported(Probe("""
						[SnowBank.Data.Xml.XmlProperty("tags", ItemName = "tag")]
						public System.Collections.Generic.List<string>? Tags { get; set; }
				"""), "CXML0003");
		}

		#endregion

		#region CXML0004: the member-level XML vocabulary on a DataContract-profile container...

		[Test]
		public void Test_At_Sugar_On_A_DataContract_Container_Is_A_Build_Error()
		{
			// the DataContract wire has no notion of a user-data attribute: everything is an element named by the contract
			var refusal = AssertRefusal(
				Probe(
					"""
								[System.Runtime.Serialization.DataMember]
								[SnowBank.Data.Xml.XmlProperty("@id")]
								public int Id { get; set; }
						""",
					containerAttributes: DataContractContainer,
					dtoAttributes: "	[System.Runtime.Serialization.DataContract]"),
				"CXML0004");

			Assert.That(refusal.GetMessage(), Does.Contain("DataContract"), "the message names the wire that cannot honor the request");
		}

		[Test]
		public void Test_Attribute_True_On_A_DataContract_Container_Is_A_Build_Error()
		{
			AssertRefusal(
				Probe(
					"""
								[System.Runtime.Serialization.DataMember]
								[SnowBank.Data.Xml.XmlProperty(Attribute = true)]
								public int Id { get; set; }
						""",
					containerAttributes: DataContractContainer,
					dtoAttributes: "	[System.Runtime.Serialization.DataContract]"),
				"CXML0004");
		}

		[Test]
		public void Test_An_Xml_Name_On_A_DataContract_Container_Is_A_Build_Error()
		{
			// the contract already decides the element name: a second, XML-only name would silently lose
			AssertRefusal(
				Probe(
					"""
								[System.Runtime.Serialization.DataMember]
								[SnowBank.Data.Xml.XmlProperty("identifier")]
								public int Id { get; set; }
						""",
					containerAttributes: DataContractContainer,
					dtoAttributes: "	[System.Runtime.Serialization.DataContract]"),
				"CXML0004");
		}

		[Test]
		public void Test_An_ItemName_On_A_DataContract_Container_Is_A_Build_Error()
		{
			// the compat wire derives item names from the contract too ("ArrayOfstring" / "string"), so an override
			// would break the very compatibility the profile exists for
			AssertRefusal(
				Probe(
					"""
								[System.Runtime.Serialization.DataMember]
								[SnowBank.Data.Xml.XmlProperty(ItemName = "tag")]
								public System.Collections.Generic.List<string>? Tags { get; set; }
						""",
					containerAttributes: DataContractContainer,
					dtoAttributes: "	[System.Runtime.Serialization.DataContract]"),
				"CXML0004");
		}

		[Test]
		public void Test_The_Same_Vocabulary_On_A_Modern_Container_Is_Not_Reported()
		{
			// the non-triggering shape: exactly what the Modern wire exists to honor
			AssertNotReported(Probe("""
						[SnowBank.Data.Xml.XmlProperty("@id")]
						public int Id { get; set; }

						[SnowBank.Data.Xml.XmlProperty("tags", ItemName = "tag")]
						public System.Collections.Generic.List<string>? Tags { get; set; }
				"""), "CXML0004");
		}

		[Test]
		public void Test_An_Explicit_DictionaryFormat_On_A_DataContract_Container_Is_A_Build_Error()
		{
			// the compat wire has exactly ONE dictionary shape: a format override there changes nothing, and a setting
			// that silently changes nothing is the failure mode this whole family of diagnostics exists to prevent
			var refusal = AssertRefusal(
				Probe(
					"""
								[System.Runtime.Serialization.DataMember]
								[SnowBank.Data.Xml.XmlProperty(DictionaryFormat = SnowBank.Data.Xml.XmlDictionaryFormat.KeyValueAttributes)]
								public System.Collections.Generic.Dictionary<string, int>? Map { get; set; }
						""",
					containerAttributes: DataContractContainer,
					dtoAttributes: "	[System.Runtime.Serialization.DataContract]"),
				"CXML0004");

			Assert.That(refusal.GetMessage(), Does.Contain("KeyValueAttributes"), "the message names the setting that cannot be honored");
		}

		[Test]
		public void Test_An_Explicitly_Default_DictionaryFormat_On_A_DataContract_Container_Is_Not_Reported()
		{
			// spelling out 'Default' asks to INHERIT, which the compat wire honors perfectly: it is not an override
			AssertNotReported(
				Probe(
					"""
								[System.Runtime.Serialization.DataMember]
								[SnowBank.Data.Xml.XmlProperty(DictionaryFormat = SnowBank.Data.Xml.XmlDictionaryFormat.Default)]
								public System.Collections.Generic.Dictionary<string, int>? Map { get; set; }
						""",
					containerAttributes: DataContractContainer,
					dtoAttributes: "	[System.Runtime.Serialization.DataContract]"),
				"CXML0004");
		}

		[Test]
		public void Test_Several_Refused_Settings_Are_Named_In_One_Diagnostic()
		{
			// an author who wrote two refused settings has to see BOTH: fixing the first only to rebuild into the second
			// is the kind of drip-feed that makes a build error feel arbitrary
			var (_, diagnostics) = RunOn(Probe(
				"""
							[System.Runtime.Serialization.DataMember]
							[SnowBank.Data.Xml.XmlProperty("tags", ItemName = "tag")]
							public System.Collections.Generic.List<string>? Tags { get; set; }
					""",
				containerAttributes: DataContractContainer,
				dtoAttributes: "	[System.Runtime.Serialization.DataContract]"));

			var refusals = diagnostics.Where(static d => d.Id == "CXML0004").ToList();
			Assert.That(refusals, Has.Count.EqualTo(1), "the two refused settings belong to one member, so they belong to one diagnostic");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(refusals[0].GetMessage(), Does.Contain("Name = \"tags\""));
				Assert.That(refusals[0].GetMessage(), Does.Contain("ItemName = \"tag\""));
			}
		}

		[Test]
		public void Test_A_DataContract_Container_Without_Any_XmlProperty_Is_Not_Reported()
		{
			AssertNotReported(
				Probe(
					"""
								[System.Runtime.Serialization.DataMember]
								public int Id { get; set; }
						""",
					containerAttributes: DataContractContainer,
					dtoAttributes: "	[System.Runtime.Serialization.DataContract]"),
				"CXML0004");
		}

		#endregion

		#region CXML0005: two members resolving to one XML name...

		[Test]
		public void Test_Two_Members_Resolving_To_The_Same_Element_Name_Is_A_Build_Error()
		{
			// two elements with one name is not an error in XML, which is exactly the problem: the document parses,
			// and the consumer silently reads one of the two
			var refusal = AssertRefusal(Probe("""
						public int Alpha { get; set; }

						[SnowBank.Data.Xml.XmlProperty("Alpha")]
						public int Other { get; set; }
				"""), "CXML0005");

			Assert.That(refusal.GetMessage(), Does.Contain("Alpha"), "the message names the collision");
		}

		[Test]
		public void Test_Two_Attributes_Resolving_To_The_Same_Name_Is_A_Build_Error()
		{
			// duplicated attributes, unlike duplicated elements, would make the document itself unparseable
			AssertRefusal(Probe("""
						[SnowBank.Data.Xml.XmlProperty("@id")]
						public int Id { get; set; }

						[SnowBank.Data.Xml.XmlProperty("@id")]
						public int Other { get; set; }
				"""), "CXML0005");
		}

		[Test]
		public void Test_An_Attribute_And_An_Element_Sharing_A_Name_Is_Not_Reported()
		{
			// an attribute and a child element live in different namespaces in XML: refusing this pair would be a
			// false positive on a perfectly readable document
			AssertNotReported(Probe("""
						public int Alpha { get; set; }

						[SnowBank.Data.Xml.XmlProperty("@Alpha")]
						public int Other { get; set; }
				"""), "CXML0005");
		}

		[Test]
		public void Test_The_Collision_Remedy_Points_At_XmlProperty_On_A_Modern_Container()
		{
			var refusal = AssertRefusal(Probe("""
						public int Alpha { get; set; }

						[SnowBank.Data.Xml.XmlProperty("Alpha")]
						public int Other { get; set; }
				"""), "CXML0005");

			Assert.That(refusal.GetMessage(), Does.Contain("XmlProperty"), "the Modern wire honors an XML-only rename, so that is the remedy to name");
		}

		[Test]
		public void Test_The_Collision_Remedy_Points_At_DataMember_On_A_DataContract_Container()
		{
			// the remedy has to be one the author can actually apply: an [XmlProperty] rename on this wire is itself
			// refused by CXML0004, so suggesting it would send them from one build error straight into another
			var refusal = AssertRefusal(
				Probe(
					"""
								[System.Runtime.Serialization.DataMember(Name = "same")]
								public int Alpha { get; set; }

								[System.Runtime.Serialization.DataMember(Name = "same")]
								public int Other { get; set; }
						""",
					containerAttributes: DataContractContainer,
					dtoAttributes: "	[System.Runtime.Serialization.DataContract]"),
				"CXML0005");

			using (Assert.EnterMultipleScope())
			{
				Assert.That(refusal.GetMessage(), Does.Contain("DataMember"), "on the compat wire the names come from the contract, so that is where the fix goes");
				Assert.That(refusal.GetMessage(), Does.Not.Contain("XmlProperty"), "and the remedy CXML0004 would refuse must not be suggested");
			}
		}

		[Test]
		public void Test_Distinct_Xml_Names_Are_Not_Reported()
		{
			AssertNotReported(Probe("""
						public int Alpha { get; set; }

						[SnowBank.Data.Xml.XmlProperty("beta")]
						public int Other { get; set; }
				"""), "CXML0005");
		}

		#endregion

		#region CXML0006: a bare nested collection...

		[Test]
		public void Test_A_List_Of_Lists_Is_A_Build_Error()
		{
			// the inner sequence has no name to give its own items: the shape is undecidable, not merely awkward
			var refusal = AssertRefusal(Probe("""
						public System.Collections.Generic.List<System.Collections.Generic.List<int>>? Matrix { get; set; }
				"""), "CXML0006");

			Assert.That(refusal.GetMessage(), Does.Contain("Matrix"), "the message names the member");
		}

		[Test]
		public void Test_A_Jagged_Array_Is_A_Build_Error()
		{
			AssertRefusal(Probe("""
						public string[][]? Rows { get; set; }
				"""), "CXML0006");
		}

		[Test]
		public void Test_An_Array_Of_Byte_Arrays_Is_Not_Reported()
		{
			// a byte[] is a scalar on this wire (base64 text), so an array of them is a plain collection of scalars
			AssertNotReported(Probe("""
						public byte[][]? Blobs { get; set; }
				"""), "CXML0006");
		}

		[Test]
		public void Test_A_List_Of_Strings_Is_Not_Reported()
		{
			// a string is enumerable in C# but scalar on the wire: the check must not confuse the two
			AssertNotReported(Probe("""
						public System.Collections.Generic.List<string>? Tags { get; set; }
				"""), "CXML0006");
		}

		[Test]
		public void Test_A_List_Of_A_Complex_Type_Is_Not_Reported()
		{
			// the intermediate type is exactly the remedy the diagnostic asks for
			AssertNotReported(Probe("""
						public System.Collections.Generic.List<ProbePart>? Parts { get; set; }
				"""), "CXML0006");
		}

		[Test]
		public void Test_A_List_Of_Dictionaries_Is_Not_Reported()
		{
			// a dictionary is a sequence too, but not a BARE one: the resolved dictionary format names its entries, so
			// the inner items are not nameless, which is the only thing this diagnostic is about
			AssertNotReported(Probe("""
						public System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, int>>? Maps { get; set; }
				"""), "CXML0006");
		}

		[Test]
		public void Test_A_List_Of_Lists_On_A_DataContract_Container_Is_Not_Reported()
		{
			// the compat wire DOES have a name for every level (an inner sequence of ints becomes 'ArrayOfint' holding
			// 'int' items), so the shape is decidable there: refusing it would block porting a legacy DTO that
			// DataContractSerializer serializes today, which is the one thing this profile exists to avoid
			AssertNotReported(
				Probe(
					"""
								public System.Collections.Generic.List<System.Collections.Generic.List<int>>? Matrix { get; set; }
						""",
					containerAttributes: DataContractContainer),
				"CXML0006");
		}

		[Test]
		public void Test_A_Refused_Member_Does_Not_Also_Collect_The_Nested_Sequence_Refusal()
		{
			// one member, one error: a bad name on a shape that is ALSO a bare nested sequence must not report twice,
			// because the second message would be noise until the first is fixed
			var (_, diagnostics) = RunOn(Probe("""
						[SnowBank.Data.Xml.XmlProperty("not a name")]
						public System.Collections.Generic.List<System.Collections.Generic.List<int>>? Matrix { get; set; }
				"""));

			var reported = diagnostics.Where(static d => d.Id.StartsWith("CXML", StringComparison.Ordinal)).Select(static d => d.Id).ToList();
			Assert.That(reported, Is.EqualTo((string[]) [ "CXML0007" ]), "the name refusal is the one to report; CXML0006 must not stack on top of it");
		}

		[Test]
		public void Test_A_JsonArray_Member_Is_Not_Reported()
		{
			// the near-miss worth pinning: a JsonArray is a list of JsonValue, and a JsonValue is not itself enumerable,
			// so the DOM types must not read as nested sequences
			AssertNotReported(Probe("""
						public SnowBank.Data.Json.JsonArray? Items { get; set; }

						public SnowBank.Data.Json.JsonObject? Extra { get; set; }
				"""), "CXML0006");
		}

		#endregion

		#region CXML0008: a member converter with no XML facet...

		/// <summary>Wraps a converter for <c>bool</c> implementing the facets named in <paramref name="interfaces"/>, applied to a member of the probe's DTO</summary>
		private static string ConverterProbe(string interfaces, string containerAttributes = ModernContainer) => $$"""
			namespace Probe
			{

				public sealed class ProbeBoolConverter : {{interfaces}}
				{

					public SnowBank.Data.Json.JsonValue Pack(bool instance, SnowBank.Data.Json.CrystalJsonSettings? settings = null, SnowBank.Data.Json.ICrystalJsonTypeResolver? resolver = null)
						=> SnowBank.Data.Json.JsonString.Return(instance ? "1" : "0");

					public bool Unpack(SnowBank.Data.Json.JsonValue value, SnowBank.Data.Json.ICrystalJsonTypeResolver? resolver) => value.ToBoolean();

					public void WriteXml<TEmitter>(ref TEmitter emitter, bool value, SnowBank.Data.Json.CrystalJsonSettings? settings = null, string? rootName = null)
						where TEmitter : struct, SnowBank.Data.Xml.IXmlEmitter
					{
						var name = SnowBank.Data.Xml.XmlName.Create(rootName ?? "bit");
						emitter.WriteStartElement(in name);
						emitter.WriteRawAscii(value ? "1" : "0");
						emitter.WriteEndElement(in name);
					}

				}

				public sealed record ProbeDto
				{
					[SnowBank.Data.Json.JsonConvertWith(typeof(ProbeBoolConverter))]
					public bool Live { get; set; }
				}

			{{containerAttributes}}
				[SnowBank.Data.Json.CrystalJsonSerializable(typeof(ProbeDto))]
				public static partial class ProbeConverters
				{
				}

			}
			""";

		[Test]
		public void Test_A_Member_Converter_Without_An_Xml_Facet_Is_A_Build_Error()
		{
			// the converter owns the member's JSON form; its XML form would be written by the very rules the converter
			// was declared to replace, so the two wires would disagree with nothing in the source saying so
			var refusal = AssertRefusal(ConverterProbe("SnowBank.Data.Json.IJsonMemberConverter<bool>"), "CXML0008");

			using (Assert.EnterMultipleScope())
			{
				Assert.That(refusal.GetMessage(), Does.Contain("ProbeBoolConverter"), "the message names the converter");
				Assert.That(refusal.GetMessage(), Does.Contain("ICrystalXmlSerializer<bool>"), "and the facet it is missing, for the member's own type");
			}
		}

		[Test]
		public void Test_A_Member_Converter_With_An_Xml_Facet_Is_Not_Reported()
		{
			// the same converter, now answering for both wires: nothing is left to be decided behind the author's back
			AssertNotReported(ConverterProbe("SnowBank.Data.Json.IJsonMemberConverter<bool>, SnowBank.Data.Xml.ICrystalXmlSerializer<bool>"), "CXML0008");
		}

		[Test]
		public void Test_A_Member_Converter_Without_An_Xml_Facet_Is_Not_Reported_On_A_Json_Only_Container()
		{
			// the rule exists because the container publishes TWO wires: with only one of them, the converter owns all of it
			AssertNotReported(ConverterProbe("SnowBank.Data.Json.IJsonMemberConverter<bool>", JsonOnlyContainer), "CXML0008");
		}

		#endregion

		#region A JSON-only container never sees a CXML diagnostic...

		[Test]
		public void Test_The_Whole_Xml_Vocabulary_Is_Inert_On_A_Json_Only_Container()
		{
			// the attribute is a no-op without the container's opt-in, and a no-op must not produce build errors:
			// every shape below would be refused on an XML container, and all of them are silent here
			var (containers, diagnostics) = RunOn(Probe(
				"""
							[SnowBank.Data.Xml.XmlProperty("@")]
							public int Bad { get; set; }

							[SnowBank.Data.Xml.XmlProperty("Alpha")]
							public int Other { get; set; }

							public int Alpha { get; set; }

							[SnowBank.Data.Xml.XmlProperty(Attribute = true)]
							public System.Collections.Generic.List<string>? Tags { get; set; }

							public System.Collections.Generic.List<System.Collections.Generic.List<int>>? Matrix { get; set; }
					""",
				containerAttributes: JsonOnlyContainer));

			Assert.That(diagnostics.Where(static d => d.Id.StartsWith("CXML", StringComparison.Ordinal)), Is.Empty, "a JSON-only container must never see an XML diagnostic");

			var dto = containers["ProbeConverters"].IncludedTypes.Single(static t => t.Name == "ProbeDto");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(containers["ProbeConverters"].XmlProfile, Is.Null, "sanity: the container really produces no XML");
				Assert.That(dto.Members.Where(static m => m.XmlName is not null), Is.Empty, "nothing is parsed out of an inert attribute");
				Assert.That(dto.Members.Where(static m => m.XmlIsAttribute), Is.Empty);
			}
		}

		#endregion

	}

}
