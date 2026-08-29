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

	/// <summary>Pins the container truth table: which output formats a set of container attributes produces, and which combinations are rejected</summary>
	/// <remarks>
	/// <para>One fact per row of the table. A row that generates asserts BOTH presence and absence: "generates XML" is only meaningful next to "and no JSON entry point", since the failure mode being guarded against is a format that quietly rides along.</para>
	/// <para>A row that is rejected asserts the diagnostic id AND that the message names the remedy: a build error nobody can act on is barely better than silence.</para>
	/// </remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class ContainerOutputMatrixFacts : SimpleTest
	{

		/// <summary>Wraps a set of container attributes into a compilable probe (a DTO plus the container that registers it)</summary>
		private static string Probe(string containerAttributes) => $$"""
			namespace Probe
			{

				public sealed record ProbeDto
				{
					public int Plain { get; set; }

					public string? Label { get; set; }
				}

			{{containerAttributes}}
				[SnowBank.Data.CrystalSerializable(typeof(ProbeDto))]
				public static partial class ProbeConverters
				{
				}

			}
			""";

		/// <summary>Runs the generator over a probe, returning the whole generated source and the generator's diagnostics</summary>
		private (string Generated, ImmutableArray<Diagnostic> Diagnostics) RunOn(string containerAttributes)
		{
			var compilation = GeneratorProbeHarness.Compile(Probe(containerAttributes));
			var (output, diagnostics) = GeneratorProbeHarness.RunGenerator(compilation);

			foreach (var d in diagnostics)
			{
				Log($"generator: [{d.Severity}] {d}");
			}

			// the probe's own tree is the first one: everything after it was generated
			var generated = string.Join("\n", output.SyntaxTrees.Skip(1).Select(static t => t.ToString()));
			return (generated, diagnostics);
		}

		/// <summary>Runs a probe that must be accepted, and asserts that the generated code compiles clean</summary>
		private string GenerateOf(string containerAttributes)
		{
			var (generated, diagnostics) = RunOn(containerAttributes);

			Assert.That(diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error), Is.Empty, "this row of the table must be accepted");
			Assert.That(generated, Is.Not.Empty, "the container must have produced code");

			return generated;
		}

		/// <summary>The members that only exist on the JSON surface of a generated holder</summary>
		private static void AssertHasJsonSurface(string generated, bool expected)
		{
			var constraint = expected ? "must" : "must not";
			using (Assert.EnterMultipleScope())
			{
				Assert.That(generated.Contains("public static void Serialize("), Is.EqualTo(expected), $"the holder {constraint} expose the JSON Serialize entry point");
				Assert.That(generated.Contains(" Pack("), Is.EqualTo(expected), $"the holder {constraint} expose Pack");
				Assert.That(generated.Contains(" Unpack("), Is.EqualTo(expected), $"the holder {constraint} expose Unpack");
				Assert.That(generated.Contains("ToJsonText("), Is.EqualTo(expected), $"the holder {constraint} expose the JSON text output");
				Assert.That(generated.Contains("IJsonConverter"), Is.EqualTo(expected), $"the converter {constraint} carry the JSON facet");
				Assert.That(generated.Contains("TypeMapper"), Is.EqualTo(expected), $"the container {constraint} expose the JSON resolver");
			}
		}

		/// <summary>The members that only exist on the XML surface of a generated holder</summary>
		private static void AssertHasXmlSurface(string generated, bool expected)
		{
			var constraint = expected ? "must" : "must not";
			using (Assert.EnterMultipleScope())
			{
				Assert.That(generated.Contains("public void WriteXml<TEmitter>"), Is.EqualTo(expected), $"the converter {constraint} carry the XML write body");
				Assert.That(generated.Contains("ToXmlText("), Is.EqualTo(expected), $"the holder {constraint} expose the XML text output");
				Assert.That(generated.Contains("ICrystalXmlElementSerializer"), Is.EqualTo(expected), $"the converter {constraint} carry the XML element facet");
				Assert.That(generated.Contains("ElementName =>"), Is.EqualTo(expected), $"the converter {constraint} expose its element name");
				Assert.That(generated.Contains("CollectionRootName =>"), Is.EqualTo(expected), $"the converter {constraint} expose its collection root name");
			}
		}

		#region Rows that generate...

		[Test]
		public void Test_Neutral_Marker_With_Json_Output_Generates_Json_Only()
		{
			var generated = GenerateOf("""
					[SnowBank.Data.CrystalConverter]
					[SnowBank.Data.Json.CrystalJsonOutput]
				""");

			AssertHasJsonSurface(generated, true);
			AssertHasXmlSurface(generated, false);
		}

		[Test]
		public void Test_Json_Alias_Generates_Json_Only()
		{
			// the alias is the neutral marker plus the JSON output: the same surface, spelled shorter
			var generated = GenerateOf("""
					[SnowBank.Data.Json.CrystalJsonConverter]
				""");

			AssertHasJsonSurface(generated, true);
			AssertHasXmlSurface(generated, false);
		}

		[Test]
		public void Test_Json_Alias_And_Neutral_Marker_Produce_The_Same_Code()
		{
			// "alias" is a promise about the generated code, not about the vocabulary: the two spellings must be indistinguishable
			var viaAlias = GenerateOf("""
					[SnowBank.Data.Json.CrystalJsonConverter(SnowBank.Data.Json.CrystalJsonSerializerDefaults.Web)]
				""");

			var viaOutput = GenerateOf("""
					[SnowBank.Data.CrystalConverter]
					[SnowBank.Data.Json.CrystalJsonOutput(SnowBank.Data.Json.CrystalJsonSerializerDefaults.Web)]
				""");

			Assert.That(viaAlias, Is.EqualTo(viaOutput), "the alias must generate exactly what the explicit spelling generates, parameters included");
		}

		[Test]
		public void Test_Neutral_Marker_With_Xml_Output_Generates_Xml_Only()
		{
			// the row this whole wave exists for: an XML-only container carries no JSON surface at all
			var generated = GenerateOf("""
					[SnowBank.Data.CrystalConverter]
					[SnowBank.Data.Xml.CrystalXmlOutput]
				""");

			AssertHasXmlSurface(generated, true);
			AssertHasJsonSurface(generated, false);
		}

		[Test]
		public void Test_Xml_Alias_Generates_Xml_Only()
		{
			var generated = GenerateOf("""
					[SnowBank.Data.Xml.CrystalXmlConverter]
				""");

			AssertHasXmlSurface(generated, true);
			AssertHasJsonSurface(generated, false);
		}

		[Test]
		public void Test_Xml_Alias_And_Neutral_Marker_Produce_The_Same_Code()
		{
			var viaAlias = GenerateOf("""
					[SnowBank.Data.Xml.CrystalXmlConverter(DictionaryFormat = SnowBank.Data.Xml.CrystalXmlDictionaryFormat.KeyValueElements)]
				""");

			var viaOutput = GenerateOf("""
					[SnowBank.Data.CrystalConverter]
					[SnowBank.Data.Xml.CrystalXmlOutput(DictionaryFormat = SnowBank.Data.Xml.CrystalXmlDictionaryFormat.KeyValueElements)]
				""");

			Assert.That(viaAlias, Is.EqualTo(viaOutput), "the alias must generate exactly what the explicit spelling generates, parameters included");
		}

		[Test]
		public void Test_Neutral_Marker_With_Both_Outputs_Generates_Both()
		{
			var generated = GenerateOf("""
					[SnowBank.Data.CrystalConverter]
					[SnowBank.Data.Json.CrystalJsonOutput]
					[SnowBank.Data.Xml.CrystalXmlOutput]
				""");

			AssertHasJsonSurface(generated, true);
			AssertHasXmlSurface(generated, true);
		}

		[Test]
		public void Test_An_Xml_Only_Container_Compiles()
		{
			// the absence assertions above only prove that the JSON surface is gone; this one proves that what remains stands on its own
			var compilation = GeneratorProbeHarness.Compile(Probe("""
					[SnowBank.Data.Xml.CrystalXmlConverter]
				"""));
			var (output, _) = GeneratorProbeHarness.RunGenerator(compilation);

			var errors = output.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error).Select(static d => d.ToString()).ToList();
			foreach (var error in errors)
			{
				Log($"error: {error}");
			}

			Assert.That(errors, Is.Empty, "the XML-only emission must compile on its own, with no JSON surface to lean on");
		}

		#endregion

		#region Rows that are rejected...

		[Test]
		public void Test_Json_Alias_With_Xml_Output_Is_Rejected()
		{
			var (_, diagnostics) = RunOn("""
					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.Xml.CrystalXmlOutput]
				""");

			var rejection = diagnostics.SingleOrDefault(static d => d.Id == "CRYS0002");
			Assert.That(rejection, Is.Not.Null, "a mono-format alias cannot host a second output format");
			Assert.That(rejection!.Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(rejection.GetMessage(), Does.Contain("[CrystalConverter]"), "the remedy names the marker that does combine");
		}

		[Test]
		public void Test_Xml_Alias_With_Json_Output_Is_Rejected()
		{
			var (_, diagnostics) = RunOn("""
					[SnowBank.Data.Xml.CrystalXmlConverter]
					[SnowBank.Data.Json.CrystalJsonOutput]
				""");

			var rejection = diagnostics.SingleOrDefault(static d => d.Id == "CRYS0002");
			Assert.That(rejection, Is.Not.Null, "the rejection is symmetrical: neither alias combines");
			Assert.That(rejection!.Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(rejection.GetMessage(), Does.Contain("[CrystalConverter]"), "the remedy names the marker that does combine");
		}

		[Test]
		public void Test_Json_Alias_With_Both_Outputs_Is_Rejected()
		{
			var (_, diagnostics) = RunOn("""
					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.Json.CrystalJsonOutput]
					[SnowBank.Data.Xml.CrystalXmlOutput]
				""");

			var rejection = diagnostics.SingleOrDefault(static d => d.Id == "CRYS0002");
			Assert.That(rejection, Is.Not.Null, "a mono-format alias cannot host either output attribute, let alone both");
			Assert.That(rejection!.Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(rejection.GetMessage(), Does.Contain("[CrystalJsonOutput] and [CrystalXmlOutput]"), "both names are reported, each bracketed on its own, not one pair of brackets wrapped around both");
			Assert.That(rejection.GetMessage(), Does.Not.Contain("[[").And.Not.Contain("]]"), "the message must never double up brackets around the pair");
			Assert.That(rejection.GetMessage(), Does.Contain("[CrystalConverter]"), "the remedy names the marker that does combine");
		}

		[Test]
		public void Test_Neutral_Marker_Without_Any_Output_Is_Rejected()
		{
			var (_, diagnostics) = RunOn("""
					[SnowBank.Data.CrystalConverter]
				""");

			var rejection = diagnostics.SingleOrDefault(static d => d.Id == "CRYS0001");
			Assert.That(rejection, Is.Not.Null, "a container that produces nothing is never what the author meant");
			Assert.That(rejection!.Severity, Is.EqualTo(DiagnosticSeverity.Error));
			using (Assert.EnterMultipleScope())
			{
				// the message lists the concrete options, so the reader never has to go looking for the vocabulary
				Assert.That(rejection.GetMessage(), Does.Contain("[CrystalJsonOutput]"));
				Assert.That(rejection.GetMessage(), Does.Contain("[CrystalXmlOutput]"));
				Assert.That(rejection.GetMessage(), Does.Contain("[CrystalJsonConverter]"));
				Assert.That(rejection.GetMessage(), Does.Contain("[CrystalXmlConverter]"));
			}
		}

		[Test]
		public void Test_Several_Container_Markers_Are_Rejected_Once()
		{
			var (generated, diagnostics) = RunOn("""
					[SnowBank.Data.CrystalConverter]
					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.Json.CrystalJsonOutput]
				""");

			var rejections = diagnostics.Where(static d => d.Id == "CRYS0003").ToList();
			Assert.That(rejections, Has.Count.EqualTo(1), "several markers means several matching pipelines, but exactly one rejection");
			Assert.That(rejections[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(generated, Is.Empty, "a rejected container generates nothing at all");
		}

		#endregion

	}

}
