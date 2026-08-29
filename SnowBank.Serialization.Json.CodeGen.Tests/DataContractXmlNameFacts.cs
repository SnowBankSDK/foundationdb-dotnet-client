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
	using Microsoft.CodeAnalysis;

	// note: the Newtonsoft.Json.JsonPropertyAttribute the probes reference is defined once in the test assembly
	// (see StjParityMatrixFacts.cs) and imported here, so the probe source must not redefine it (that would be CS0436)

	/// <summary>Pins that the data contract names the element of the DataContract XML format, and that a member cannot carry a JSON name that disagrees with it</summary>
	/// <remarks>
	/// <para>The contract name is <c>[DataMember(Name = "...")]</c> when it is spelled and the declared member name when it is not: a bare <c>[DataMember]</c> is still a naming decision, and it is the name <c>DataContractSerializer</c> writes. A JSON naming attribute that disagrees with it makes one type serve two format contracts, which is rejected (<c>CJSON0011</c>) on both paths; the remedy is to split the DTO.</para>
	/// <para>The bare form used to slip under that rejection: the JSON name won the whole name resolution silently, and was written into the XML document as well.</para>
	/// <para>A plain DTO is a different case, with no diagnostic: it has no data contract for a <c>[JsonProperty]</c> to disagree with, so the JSON member is renamed and the element keeps the member's own name, which is what the reference serializer writes.</para>
	/// </remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class DataContractXmlNameFacts : SimpleTest
	{

		/// <summary>The container attributes of a probe that produces the DataContract XML format next to the DCJS JSON format</summary>
		private const string BothOutputsContainer = """
				[SnowBank.Data.CrystalConverter]
				[SnowBank.Data.Json.CrystalJsonOutput(SnowBank.Data.Json.CrystalJsonSerializerDefaults.DataContractCompat)]
				[SnowBank.Data.Xml.CrystalXmlOutput]
			""";

		/// <summary>The attribute that puts the probe DTO under a data contract; the empty string makes it a plain DTO</summary>
		private const string DataContract = "	[System.Runtime.Serialization.DataContract]";

		private static string Probe(string members, string dtoAttributes) => $$"""
			#nullable enable
			namespace Probe
			{

			{{dtoAttributes}}
				public sealed class ProbeDto
				{
			{{members}}
				}

			{{BothOutputsContainer}}
				[SnowBank.Data.CrystalSerializable(typeof(ProbeDto))]
				public static partial class ProbeConverters
				{
				}

			}
			""";

		private (List<Diagnostic> GeneratorDiagnostics, List<Diagnostic> Errors, string Generated) RunOn(string members, string dtoAttributes = DataContract)
		{
			var compilation = GeneratorProbeHarness.Compile(Probe(members, dtoAttributes));

			Assert.That(
				compilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error),
				Is.Empty,
				"the probe source must compile clean on its own");

			var (outputCompilation, generatorDiagnostics) = GeneratorProbeHarness.RunGenerator(compilation);
			foreach (var diagnostic in generatorDiagnostics) { Log($"generator: [{diagnostic.Severity}] {diagnostic}"); }

			var errors = outputCompilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error).ToList();
			foreach (var diagnostic in errors) { Log($"compiler: {diagnostic}"); }

			return (generatorDiagnostics.ToList(), errors, string.Concat(outputCompilation.SyntaxTrees.Skip(1).Select(static t => t.ToString())));
		}

		private static void AssertNamingConflict(List<Diagnostic> diagnostics, string contractName, string jsonName)
		{
			var conflict = diagnostics.SingleOrDefault(static d => d.Id == "CJSON0011");
			Assert.That(conflict, Is.Not.Null, "the generator must reject a member whose JSON name disagrees with its contract name");
			Assert.That(conflict!.Severity, Is.EqualTo(DiagnosticSeverity.Error), "two format contracts on one type is an error, not a warning");
			Assert.That(conflict.GetMessage(), Does.Contain(contractName), "the message must name the contract name");
			Assert.That(conflict.GetMessage(), Does.Contain(jsonName), "the message must name the JSON name");
			Assert.That(conflict.GetMessage(), Does.Contain("one DTO per serializer"), "the remedy is the split (the same greppable phrase as the rest of the conflicting-name family)");
		}

		[Test]
		public void Test_A_Json_Name_Diverging_From_A_Bare_Data_Member_Is_Rejected()
		{
			// a bare [DataMember] names the member after itself, and that is the name the reference serializer writes:
			// a [JsonProperty] spelling it differently is a second format contract on one type
			var (diagnostics, _, _) = RunOn("""
						[System.Runtime.Serialization.DataMember]
						[SnowBank.Data.Json.JsonProperty("SUBSCRIPTION_CODE")]
						public string? SubscriptionCode { get; set; }
				""");

			AssertNamingConflict(diagnostics, "SubscriptionCode", "SUBSCRIPTION_CODE");
		}

		[Test]
		public void Test_A_Foreign_Json_Name_Diverging_From_A_Bare_Data_Member_Is_Rejected()
		{
			var (diagnostics, _, _) = RunOn("""
						[System.Runtime.Serialization.DataMember]
						[Newtonsoft.Json.JsonProperty("ACTIF")]
						public string? Enabled { get; set; }
				""");

			AssertNamingConflict(diagnostics, "Enabled", "ACTIF");
		}

		[Test]
		public void Test_A_Json_Name_Diverging_From_A_Contract_Rename_Is_Rejected()
		{
			var (diagnostics, _, _) = RunOn("""
						[System.Runtime.Serialization.DataMember(Name = "code")]
						[Newtonsoft.Json.JsonProperty("ACTIF")]
						public string? Code { get; set; }
				""");

			AssertNamingConflict(diagnostics, "code", "ACTIF");
		}

		[Test]
		public void Test_An_Agreeing_Json_Name_Is_Allowed_And_Names_Both_Formats()
		{
			var (diagnostics, errors, generated) = RunOn("""
						[System.Runtime.Serialization.DataMember]
						[SnowBank.Data.Json.JsonProperty("SubscriptionCode")]
						public string? SubscriptionCode { get; set; }
				""");

			Assert.That(diagnostics.Where(static d => d.Id == "CJSON0011"), Is.Empty, "one name on both attributes is one contract, not two");
			Assert.That(errors, Is.Empty, "the generated container must compile");
			Assert.That(generated, Does.Contain("__xml_SubscriptionCode"), "the element takes the contract name");
			Assert.That(generated, Does.Contain("\"SubscriptionCode\""), "and so does the JSON member");
		}

		[Test]
		public void Test_An_Agreeing_Contract_Rename_Is_Allowed_And_Names_Both_Formats()
		{
			var (diagnostics, errors, generated) = RunOn("""
						[System.Runtime.Serialization.DataMember(Name = "code")]
						[SnowBank.Data.Json.JsonProperty("code")]
						public string? SubscriptionCode { get; set; }
				""");

			Assert.That(diagnostics.Where(static d => d.Id == "CJSON0011"), Is.Empty, "a rename both attributes agree on is one contract");
			Assert.That(errors, Is.Empty, "the generated container must compile");
			Assert.That(generated, Does.Contain("__xml_code"), "the element takes the renamed contract name");
			Assert.That(generated, Does.Not.Contain("__xml_SubscriptionCode"), "the declared member name is not written once the contract renames it");
		}

		[Test]
		public void Test_A_Data_Member_Rename_Names_The_Xml_Element()
		{
			var (_, errors, generated) = RunOn("""
						[System.Runtime.Serialization.DataMember(Name = "code")]
						public string? SubscriptionCode { get; set; }
				""");

			Assert.That(errors, Is.Empty, "the generated container must compile");
			Assert.That(generated, Does.Contain("__xml_code"), "[DataMember(Name)] is the data contract's own name, so it names the element");
			Assert.That(generated, Does.Not.Contain("__xml_SubscriptionCode"), "the declared member name is not written once the contract renames it");
		}

		[Test]
		public void Test_A_Json_Rename_On_A_Plain_Dto_Does_Not_Rename_The_Xml_Element()
		{
			// no [DataContract], so there is no contract name for the JSON one to disagree with: the JSON member is
			// renamed and the element keeps the member's own name, which is what the reference serializer writes
			var (diagnostics, errors, generated) = RunOn("""
						[SnowBank.Data.Json.JsonProperty("SUBSCRIPTION_CODE")]
						public string? SubscriptionCode { get; set; }
				""",
				dtoAttributes: "");

			Assert.That(diagnostics.Where(static d => d.Id == "CJSON0011"), Is.Empty, "a plain DTO carries one contract, and the JSON attribute is it");
			Assert.That(errors, Is.Empty, "the generated container must compile");
			Assert.That(generated, Does.Contain("__xml_SubscriptionCode"), "the element takes the declared member name");
			Assert.That(generated, Does.Not.Contain("__xml_SUBSCRIPTION_CODE"), "the JSON name must not reach the XML format");
			Assert.That(generated, Does.Contain("\"SUBSCRIPTION_CODE\""), "the JSON member keeps the name its attribute gives it");
		}

		[Test]
		public void Test_Two_Members_Whose_Contract_Names_Collide_Are_Rejected()
		{
			// the collision check reads the name the emitter writes, which is the contract name and not the resolved
			// JSON one
			var (diagnostics, _, _) = RunOn("""
						[System.Runtime.Serialization.DataMember]
						public string? Label { get; set; }

						[System.Runtime.Serialization.DataMember(Name = "Label")]
						public string? Title { get; set; }
				""");

			var collision = diagnostics.SingleOrDefault(static d => d.Id == "CXML0005");
			Assert.That(collision, Is.Not.Null, "two members writing the same element name must be rejected");
			Assert.That(collision!.GetMessage(), Does.Contain("Label"), "the message must name the element both members claim");
		}

	}

}
