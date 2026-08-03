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

	/// <summary>Pins how the generator reads <c>[CrystalXmlOutput]</c>: the container opt-in, the resolution of the XML wire profile against the JSON profile, the dictionary-format default, and the two refusals (<c>CXML0001</c>, <c>CXML0002</c>)</summary>
	/// <remarks>Parsing only: nothing is emitted for XML yet, so every assertion reads the metadata the parser resolved (through the driver's tracked steps) or the diagnostics it reported.</remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class XmlOutputProfileFacts : SimpleTest
	{

		/// <summary>Wraps a container declaration into a compilable probe (a DTO plus the container that enrols it)</summary>
		private static string Probe(string containerAttributes, string containerName = "ProbeConverters") => $$"""
			namespace Probe
			{

				public sealed record ProbeDto
				{
					public int Plain { get; set; }
				}

			{{containerAttributes}}
				[SnowBank.Data.Json.CrystalJsonSerializable(typeof(ProbeDto))]
				public static partial class {{containerName}}
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
				Log($"container: {name}: XmlProfile={metadata.XmlProfile ?? "<null>"}; XmlDictionaryFormat={metadata.XmlDictionaryFormat ?? "<null>"}; WireProfile={metadata.WireProfile ?? "<null>"}");
			}
			return (containers, diagnostics);
		}

		/// <summary>Reads back the metadata of the single container of a probe (a missing container means the parser refused it, which is itself the failure to report)</summary>
		private CrystalJsonContainerMetadata RunOnSingleContainer(string source, string containerName = "ProbeConverters")
		{
			var (containers, diagnostics) = RunOn(source);
			Assert.That(diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error), Is.Empty, "the probe must not be refused by the generator");
			Assert.That(containers.ContainsKey(containerName), Is.True, $"the parser must have produced metadata for container '{containerName}'");
			return containers[containerName];
		}

		#region Resolution matrix...

		[Test]
		public void Test_Absent_Attribute_Leaves_The_Container_Without_Any_Xml_Output()
		{
			// the XML vocabulary is strictly opt-in: a container that never mentions it is untouched
			var metadata = RunOnSingleContainer(Probe("""
					[SnowBank.Data.Json.CrystalJsonConverter]
				"""));

			using (Assert.EnterMultipleScope())
			{
				Assert.That(metadata.XmlProfile, Is.Null, "no [CrystalXmlOutput] means no XML output at all");
				Assert.That(metadata.XmlDictionaryFormat, Is.Null, "and no dictionary format to carry");
			}
		}

		[Test]
		public void Test_Default_Profile_Derives_DataContract_From_A_DataContractCompat_Container()
		{
			// the derivation that makes the attribute worth having: a container that already serves the DCJS
			// wire gets the matching XML wire, with nothing else to say
			var metadata = RunOnSingleContainer(Probe("""
					[SnowBank.Data.Json.CrystalJsonConverter(SnowBank.Data.Json.CrystalJsonSerializerDefaults.DataContractCompat)]
					[SnowBank.Data.Xml.CrystalXmlOutput]
				"""));

			using (Assert.EnterMultipleScope())
			{
				Assert.That(metadata.WireProfile, Is.EqualTo("DataContractCompat"), "sanity: the JSON profile the derivation reads");
				Assert.That(metadata.XmlProfile, Is.EqualTo("DataContract"), "the DCJS JSON wire derives the DataContract XML wire");
			}
		}

		[Test]
		public void Test_Default_Profile_Derives_Modern_From_A_General_Container()
		{
			// the standard container: no JSON profile, so the XML follows the JSON a modern reader predicts
			var metadata = RunOnSingleContainer(Probe("""
					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.Xml.CrystalXmlOutput]
				"""));

			using (Assert.EnterMultipleScope())
			{
				Assert.That(metadata.WireProfile, Is.Null, "sanity: the standard JSON wire");
				Assert.That(metadata.XmlProfile, Is.EqualTo("Modern"));
			}
		}

		[Test]
		public void Test_Default_Profile_Derives_Modern_From_A_Web_Container()
		{
			// the Web defaults are a naming policy, not a wire profile: the XML stays Modern (and the
			// camelCase names it will use are exactly the ones its JSON uses)
			var metadata = RunOnSingleContainer(Probe("""
					[SnowBank.Data.Json.CrystalJsonConverter(SnowBank.Data.Json.CrystalJsonSerializerDefaults.Web)]
					[SnowBank.Data.Xml.CrystalXmlOutput]
				"""));

			using (Assert.EnterMultipleScope())
			{
				Assert.That(metadata.PropertyNamingPolicy, Is.EqualTo("camel"), "sanity: the Web defaults did apply");
				Assert.That(metadata.XmlProfile, Is.EqualTo("Modern"));
			}
		}

		[Test]
		public void Test_Explicit_Modern_Overrides_The_Derivation_From_A_DataContractCompat_Container()
		{
			// the override that exists for the portage: keep serving the legacy JSON, publish modern XML
			var metadata = RunOnSingleContainer(Probe("""
					[SnowBank.Data.Json.CrystalJsonConverter(SnowBank.Data.Json.CrystalJsonSerializerDefaults.DataContractCompat)]
					[SnowBank.Data.Xml.CrystalXmlOutput(Profile = SnowBank.Data.Xml.XmlOutputProfile.Modern)]
				"""));

			using (Assert.EnterMultipleScope())
			{
				Assert.That(metadata.WireProfile, Is.EqualTo("DataContractCompat"), "the JSON wire is unchanged");
				Assert.That(metadata.XmlProfile, Is.EqualTo("Modern"), "the explicit XML profile wins over the derivation");
			}
		}

		[Test]
		public void Test_Explicit_DataContract_Overrides_The_Derivation_From_A_Standard_Container()
		{
			// the other direction: modern JSON, but an XML consumer that reads the DataContractSerializer wire
			var metadata = RunOnSingleContainer(Probe("""
					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.Xml.CrystalXmlOutput(Profile = SnowBank.Data.Xml.XmlOutputProfile.DataContract)]
				"""));

			using (Assert.EnterMultipleScope())
			{
				Assert.That(metadata.WireProfile, Is.Null, "the JSON wire is unchanged");
				Assert.That(metadata.XmlProfile, Is.EqualTo("DataContract"));
			}
		}

		[Test]
		public void Test_Explicit_Default_Profile_Derives_Like_An_Unspecified_One()
		{
			// spelling out the default must not become a third behavior
			var metadata = RunOnSingleContainer(Probe("""
					[SnowBank.Data.Json.CrystalJsonConverter(SnowBank.Data.Json.CrystalJsonSerializerDefaults.DataContractCompat)]
					[SnowBank.Data.Xml.CrystalXmlOutput(Profile = SnowBank.Data.Xml.XmlOutputProfile.Default)]
				"""));

			Assert.That(metadata.XmlProfile, Is.EqualTo("DataContract"));
		}

		#endregion

		#region Dictionary format...

		[Test]
		public void Test_Unspecified_DictionaryFormat_Is_Carried_As_Default()
		{
			// the container carries 'Default', not the profile's resolved shape: the per-profile default and the
			// per-member override both resolve later, and flattening them here would lose the "unset" state
			var metadata = RunOnSingleContainer(Probe("""
					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.Xml.CrystalXmlOutput]
				"""));

			Assert.That(metadata.XmlDictionaryFormat, Is.EqualTo("Default"));
		}

		[Test]
		public void Test_Explicit_DictionaryFormat_Is_Carried_Through_As_Its_Enum_Member_Name()
		{
			var metadata = RunOnSingleContainer(Probe("""
					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.Xml.CrystalXmlOutput(DictionaryFormat = SnowBank.Data.Xml.XmlDictionaryFormat.KeyValueAttributes)]
				"""));

			Assert.That(metadata.XmlDictionaryFormat, Is.EqualTo("KeyValueAttributes"));
		}

		[Test]
		public void Test_DictionaryFormat_Is_Carried_Independently_Of_The_Profile()
		{
			// the two named arguments are read independently: setting one must not reset the other
			var metadata = RunOnSingleContainer(Probe("""
					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.Xml.CrystalXmlOutput(Profile = SnowBank.Data.Xml.XmlOutputProfile.DataContract, DictionaryFormat = SnowBank.Data.Xml.XmlDictionaryFormat.KeyValueElements)]
				"""));

			using (Assert.EnterMultipleScope())
			{
				Assert.That(metadata.XmlProfile, Is.EqualTo("DataContract"));
				Assert.That(metadata.XmlDictionaryFormat, Is.EqualTo("KeyValueElements"));
			}
		}

		#endregion

		#region CXML0001: a naming policy cannot be combined with the DataContract XML wire...

		private void AssertNamingPolicyRefusal(ImmutableArray<Diagnostic> diagnostics)
		{
			var refusal = diagnostics.SingleOrDefault(static d => d.Id == "CXML0001");
			Assert.That(refusal, Is.Not.Null, "the DataContract XML wire next to a naming option must be refused at build time");
			Assert.That(refusal!.Severity, Is.EqualTo(DiagnosticSeverity.Error), "a silently wrong wire is worse than a build failure");
			Assert.That(refusal.GetMessage(), Does.Contain("DataContract"), "the message names the XML wire that cannot honor the naming option");
		}

		[Test]
		public void Test_CamelCase_Naming_Policy_Plus_Explicit_DataContract_Xml_Is_A_Build_Error()
		{
			// the DataContract wire uses the data contract names: a camelCase policy next to it is a contradiction
			var (containers, diagnostics) = RunOn(Probe("""
					[SnowBank.Data.Json.CrystalJsonConverter(PropertyNamingPolicy = SnowBank.Data.Json.CrystalJsonKnownNamingPolicy.CamelCase)]
					[SnowBank.Data.Xml.CrystalXmlOutput(Profile = SnowBank.Data.Xml.XmlOutputProfile.DataContract)]
				"""));

			AssertNamingPolicyRefusal(diagnostics);
			Assert.That(containers["ProbeConverters"].XmlProfile, Is.Null, "the refused XML request produces no XML output (the container's JSON is untouched)");
		}

		[Test]
		public void Test_CaseInsensitive_Names_Plus_Explicit_DataContract_Xml_Is_A_Build_Error()
		{
			// the second trigger shape: case-insensitive matching is just as incompatible with the contract names
			var (_, diagnostics) = RunOn(Probe("""
					[SnowBank.Data.Json.CrystalJsonConverter(PropertyNameCaseInsensitive = true)]
					[SnowBank.Data.Xml.CrystalXmlOutput(Profile = SnowBank.Data.Xml.XmlOutputProfile.DataContract)]
				"""));

			AssertNamingPolicyRefusal(diagnostics);
		}

		[Test]
		public void Test_Web_Defaults_Plus_Explicit_DataContract_Xml_Is_A_Build_Error()
		{
			// the shape a caller actually writes: the Web defaults bring the naming policy along
			var (_, diagnostics) = RunOn(Probe("""
					[SnowBank.Data.Json.CrystalJsonConverter(SnowBank.Data.Json.CrystalJsonSerializerDefaults.Web)]
					[SnowBank.Data.Xml.CrystalXmlOutput(Profile = SnowBank.Data.Xml.XmlOutputProfile.DataContract)]
				"""));

			AssertNamingPolicyRefusal(diagnostics);
		}

		[Test]
		public void Test_DataContract_Xml_Without_Any_Naming_Option_Is_Accepted()
		{
			// the non-triggering shape that matters most: the derived DataContract wire is the normal case
			var (containers, diagnostics) = RunOn(Probe("""
					[SnowBank.Data.Json.CrystalJsonConverter(SnowBank.Data.Json.CrystalJsonSerializerDefaults.DataContractCompat)]
					[SnowBank.Data.Xml.CrystalXmlOutput]
				"""));

			using (Assert.EnterMultipleScope())
			{
				Assert.That(diagnostics.Where(static d => d.Id == "CXML0001"), Is.Empty, "no naming option is in play");
				Assert.That(containers["ProbeConverters"].XmlProfile, Is.EqualTo("DataContract"));
			}
		}

		[Test]
		public void Test_Modern_Xml_With_A_CamelCase_Policy_Is_Accepted()
		{
			// the Modern wire follows the naming policy instead of fighting it: nothing to refuse
			var (containers, diagnostics) = RunOn(Probe("""
					[SnowBank.Data.Json.CrystalJsonConverter(SnowBank.Data.Json.CrystalJsonSerializerDefaults.Web)]
					[SnowBank.Data.Xml.CrystalXmlOutput(Profile = SnowBank.Data.Xml.XmlOutputProfile.Modern)]
				"""));

			using (Assert.EnterMultipleScope())
			{
				Assert.That(diagnostics.Where(static d => d.Id == "CXML0001"), Is.Empty, "a naming policy is exactly what the Modern wire honors");
				Assert.That(containers["ProbeConverters"].XmlProfile, Is.EqualTo("Modern"));
			}
		}

		#endregion

		#region CXML0002: [CrystalXmlOutput] on a class that hosts no generated serializer...

		[Test]
		public void Test_Xml_Output_On_A_Class_That_Is_Not_A_Container_Is_A_Build_Error()
		{
			// without a container marker, NOTHING is generated for the class: the attribute would be silently
			// inert, which is the failure mode this diagnostic exists to prevent
			var compilation = GeneratorProbeHarness.Compile("""
				namespace Probe
				{

					[SnowBank.Data.Xml.CrystalXmlOutput]
					public static partial class OrphanConverters
					{
					}

				}
				""");
			var (_, diagnostics) = GeneratorProbeHarness.RunGeneratorAndCaptureContainers(compilation);
			foreach (var d in diagnostics)
			{
				Log($"generator: [{d.Severity}] {d}");
			}

			var refusal = diagnostics.SingleOrDefault(static d => d.Id == "CXML0002");
			Assert.That(refusal, Is.Not.Null, "an inert [CrystalXmlOutput] must be reported, not ignored");
			Assert.That(refusal!.Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(refusal.GetMessage(), Does.Contain("CrystalJsonConverter"), "the remedy names the marker the class is missing");
		}

		[Test]
		public void Test_Xml_Output_On_A_Converter_Container_Is_Not_Reported()
		{
			var (_, diagnostics) = RunOn(Probe("""
					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.Xml.CrystalXmlOutput]
				"""));

			Assert.That(diagnostics.Where(static d => d.Id == "CXML0002"), Is.Empty);
		}

		[Test]
		public void Test_Xml_Output_On_A_Self_Serializable_Type_Is_Not_Reported_And_Resolves_Its_Profile()
		{
			// a self-serializable entity hosts its own generated code, so it is a legitimate XML host too; it
			// declares no JSON wire profile, so the derivation lands on Modern
			var (containers, diagnostics) = RunOn("""
				namespace Probe
				{

					[System.AttributeUsage(System.AttributeTargets.Class)]
					[SnowBank.Data.Json.CrystalJsonSelfSerializable]
					public sealed class ProbeDocumentAttribute : System.Attribute
					{
					}

					[ProbeDocument]
					[SnowBank.Data.Xml.CrystalXmlOutput]
					public sealed partial record ProbeEntity
					{
						public int Plain { get; set; }
					}

				}
				""");

			using (Assert.EnterMultipleScope())
			{
				Assert.That(diagnostics.Where(static d => d.Id == "CXML0002"), Is.Empty, "the entity IS its own container");
				Assert.That(containers.ContainsKey("ProbeEntity"), Is.True, "sanity: the self-serializable type was parsed");
			}
			Assert.That(containers["ProbeEntity"].XmlProfile, Is.EqualTo("Modern"), "a self-serializable type has no JSON wire profile, so the XML derivation lands on Modern");
		}

		[Test]
		public void Test_Explicit_Profile_On_A_Self_Serializable_Type_Wins()
		{
			var (containers, _) = RunOn("""
				namespace Probe
				{

					[System.AttributeUsage(System.AttributeTargets.Class)]
					[SnowBank.Data.Json.CrystalJsonSelfSerializable]
					public sealed class ProbeDocumentAttribute : System.Attribute
					{
					}

					[ProbeDocument]
					[SnowBank.Data.Xml.CrystalXmlOutput(Profile = SnowBank.Data.Xml.XmlOutputProfile.DataContract, DictionaryFormat = SnowBank.Data.Xml.XmlDictionaryFormat.KeyValueElements)]
					public sealed partial record ProbeEntity
					{
						public int Plain { get; set; }
					}

				}
				""");

			using (Assert.EnterMultipleScope())
			{
				Assert.That(containers["ProbeEntity"].XmlProfile, Is.EqualTo("DataContract"));
				Assert.That(containers["ProbeEntity"].XmlDictionaryFormat, Is.EqualTo("KeyValueElements"));
			}
		}

		[Test]
		public void Test_Self_Serializable_Type_Without_Xml_Output_Has_No_Xml_Profile()
		{
			var (containers, _) = RunOn("""
				namespace Probe
				{

					[System.AttributeUsage(System.AttributeTargets.Class)]
					[SnowBank.Data.Json.CrystalJsonSelfSerializable]
					public sealed class ProbeDocumentAttribute : System.Attribute
					{
					}

					[ProbeDocument]
					public sealed partial record ProbeEntity
					{
						public int Plain { get; set; }
					}

				}
				""");

			Assert.That(containers["ProbeEntity"].XmlProfile, Is.Null);
		}

		#endregion

	}

}
