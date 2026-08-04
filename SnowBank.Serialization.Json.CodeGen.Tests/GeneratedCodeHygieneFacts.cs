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
	using System.IO;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp;

	/// <summary>Compiles the generator's output in an environment WITHOUT <c>ImplicitUsings</c>, and with every warning surfaced</summary>
	/// <remarks>The generated files emit no <c>using</c> directives, so every BCL name they reference must be fully qualified (<c>global::System.*</c>), and they must be warning-free under <c>#nullable enable</c>: a consumer project with <c>ImplicitUsings</c> disabled and warnings-as-errors must compile them as-is.</remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class GeneratedCodeHygieneFacts : SimpleTest
	{

		/// <summary>Probe DTOs covering the emitted surface: converters, proxies (read-only, writable, observable), the TypeMapper, polymorphism, enums, collections, and nullable members</summary>
		/// <remarks>The second, nullable-oblivious half mirrors a legacy (pre-nullable) consumer codebase: the generated files force <c>#nullable enable</c> on themselves, so they must be warning-free even when the annotated member types come from an oblivious context.</remarks>
		private const string ProbeSource = """
			#nullable enable
			namespace Probe
			{

				public enum ProbeKind
				{
					None = 0,
					Alpha = 1,
					Beta = 2,
				}

				public sealed record ProbeItem
				{
					public required string Id { get; init; }
					public int Level { get; init; }
					public bool? Disabled { get; init; }
				}

				[System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
				[System.Text.Json.Serialization.JsonDerivedType(typeof(ProbeDog), "dog")]
				public abstract record ProbeAnimal
				{
					public required System.Guid Id { get; init; }
					public required string Name { get; init; }
				}

				public sealed record ProbeDog : ProbeAnimal
				{
					public bool IsGoodDog { get; init; }
				}

				public sealed record ProbeUser
				{
					public required string Id { get; init; }
					public string? Description { get; init; }
					public ProbeKind Kind { get; init; }
					public System.DateTimeOffset? Modified { get; init; }
					public string[]? Roles { get; init; }
					public System.Collections.Generic.List<ProbeItem>? Items { get; init; }
					public System.Collections.Generic.Dictionary<string, ProbeItem>? Index { get; init; }
					public SnowBank.Data.Json.JsonObject? Extras { get; init; }
				}

				[SnowBank.Data.Json.CrystalJsonConverter]
				[SnowBank.Data.Json.CrystalJsonSerializable(typeof(ProbeUser))]
				[SnowBank.Data.Json.CrystalJsonSerializable(typeof(ProbeAnimal))]
				public static partial class ProbeConverters
				{
				}

				// the same graph, published as XML too: the emitted XML members must be just as clean, including the ones
				// with NO XML projection at all (a JsonObject, a DateTimeOffset), which emit a call that fails at write time
				[SnowBank.Data.Json.CrystalJsonConverter]
				[SnowBank.Data.Xml.CrystalXmlOutput]
				[SnowBank.Data.Json.CrystalJsonSerializable(typeof(ProbeUser))]
				[SnowBank.Data.Json.CrystalJsonSerializable(typeof(ProbeItem))]
				[SnowBank.Data.Json.CrystalJsonSerializable(typeof(ProbeAnimal))]
				public static partial class ProbeXmlConverters
				{
				}

				// a container that resolves to the DataContract XML wire, whose emission does not exist yet: it must degrade
				// to a clean JSON-only container (no half-written XML surface), which is what compiling it here proves
				[SnowBank.Data.Json.CrystalJsonConverter(SnowBank.Data.Json.CrystalJsonSerializerDefaults.DataContractCompat)]
				[SnowBank.Data.Xml.CrystalXmlOutput]
				[SnowBank.Data.Json.CrystalJsonSerializable(typeof(ProbeItem))]
				public static partial class ProbeCompatXmlConverters
				{
				}

			}

			#nullable disable
			namespace Probe.Oblivious
			{

				public sealed record LegacyProbeDto
				{
					public string Name { get; set; }
					public string[] Tags { get; set; }
					public System.Collections.Generic.List<string> Items { get; set; }
					public System.Collections.Generic.Dictionary<string, string> Labels { get; set; }
					public string Fixed { get; set; } = "";
					public System.DateTime When { get; set; }
				}

				[SnowBank.Data.Json.CrystalJsonConverter]
				[SnowBank.Data.Json.CrystalJsonSerializable(typeof(LegacyProbeDto))]
				public static partial class LegacyProbeConverters
				{
				}

				// and the oblivious DTO published as XML: every null test the XML members emit sits on an oblivious member type
				[SnowBank.Data.Json.CrystalJsonConverter]
				[SnowBank.Data.Xml.CrystalXmlOutput]
				[SnowBank.Data.Json.CrystalJsonSerializable(typeof(LegacyProbeDto))]
				public static partial class LegacyProbeXmlConverters
				{
				}

			}
			""";

		[Test]
		public void Test_Generated_Code_Compiles_Without_ImplicitUsings_And_Without_Warnings()
		{
			Assert.That(GeneratorProbeHarness.References, Is.Not.Empty, "the trusted-platform-assemblies list must resolve");

			var compilation = GeneratorProbeHarness.Compile(ProbeSource);

			// the probe source itself must be clean BEFORE the generator runs, so any diagnostic below is the generator's
			Assert.That(
				compilation.GetDiagnostics().Where(static d => d.Severity >= DiagnosticSeverity.Warning),
				Is.Empty,
				"the probe source must compile clean on its own");

			var (outputCompilation, generatorDiagnostics) = GeneratorProbeHarness.RunGenerator(compilation);

			foreach (var diagnostic in generatorDiagnostics)
			{
				Log($"generator: {diagnostic}");
			}
			Assert.That(generatorDiagnostics.Where(static d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty, "the generator must not report diagnostics for the probe types");

			// the generator must actually have produced code (an empty output would make the assertions below vacuous)
			var generatedTrees = outputCompilation.SyntaxTrees.Skip(1).ToList();
			Assert.That(generatedTrees, Is.Not.Empty, "the generator must emit sources for the probe container");
			Assert.That(generatedTrees.Any(static tree => tree.ToString().Contains("TypeMapper")), Is.True, "the emitted code must include the container's TypeMapper");

			var diagnostics = outputCompilation.GetDiagnostics()
				.Where(static d => d.Severity >= DiagnosticSeverity.Warning)
				.ToList();
			foreach (var diagnostic in diagnostics)
			{
				Log(diagnostic.ToString());
				// the offending generated line, so the failure is actionable without re-running the generator by hand
				if (diagnostic.Location.SourceTree is { } tree)
				{
					var span = diagnostic.Location.GetLineSpan();
					var text = tree.GetText();
					if (span.StartLinePosition.Line < text.Lines.Count)
					{
						Log($"    | {text.Lines[span.StartLinePosition.Line].ToString().Trim()}");
					}
				}
			}
			Assert.That(diagnostics, Is.Empty, "generated code must compile without ImplicitUsings, and without warnings under '#nullable enable'");
		}

	}

}
