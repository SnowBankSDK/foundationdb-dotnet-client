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
	using System.Linq;
	using Microsoft.CodeAnalysis;

	/// <summary>Pins that enrolling a COLLECTION, a DICTIONARY or a SCALAR type in a generated container produces no converter for it, compiles clean, and reports the <c>CJSON0019</c> guidance</summary>
	/// <remarks>
	/// <para>CrystalJson serializes collections, dictionaries and scalars natively, root included; the source generator emits converters for POCO types ONLY. Enumerating such a type as if it were a POCO used to walk its indexer as a member, and the emitted holder declared a nameless indexer that did not compile (CS0106/CS0720/CS0548/CS1551).</para>
	/// <para>The guard sits on the ENROLMENT decision. The last two fixtures pin the non-triggers: a POCO enrollment still generates, and a collection MEMBER inside a POCO is untouched (the crawler already descends to the element type and never enqueues the collection itself).</para>
	/// </remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class NativeEnrolmentGuardFacts : SimpleTest
	{

		/// <summary>A plain POCO the probes enroll next to the native type, so that the container is never empty (an empty container reports CJSON0002, which would mask what these tests assert)</summary>
		private const string ShelfDto = """
				public sealed record Shelf
				{
					public string? Label { get; set; }
					public int Height { get; set; }
				}
			""";

		/// <summary>Runs the generator over a probe, logs everything, and returns both the generator's diagnostics and the diagnostics of the OUTPUT compilation (which is what the nameless-indexer crash used to show up in)</summary>
		private (ImmutableArray<Diagnostic> Generator, List<Diagnostic> Output, Compilation Compilation) RunOn(string source)
		{
			var compilation = GeneratorProbeHarness.Compile("namespace Probe\n{\n" + source + "\n}\n");
			Assert.That(
				compilation.GetDiagnostics().Where(static d => d.Severity >= DiagnosticSeverity.Warning),
				Is.Empty,
				"the probe source must compile clean on its own");

			var (output, generatorDiagnostics) = GeneratorProbeHarness.RunGenerator(compilation);
			foreach (var d in generatorDiagnostics)
			{
				Log($"generator: [{d.Severity}] {d}");
			}

			var outputDiagnostics = output.GetDiagnostics().Where(static d => d.Severity >= DiagnosticSeverity.Warning).ToList();
			foreach (var d in outputDiagnostics)
			{
				Log($"compile: {d}");
			}

			return (generatorDiagnostics, outputDiagnostics, output);
		}

		/// <summary>Concatenation of every source tree the generator added to the compilation</summary>
		private static string GeneratedSources(Compilation output) => string.Join("\n", output.SyntaxTrees.Skip(1).Select(static tree => tree.ToString()));

		/// <summary>Full names of the types the parser RESOLVED for the probe's container, which is the list the emitter generates a converter for</summary>
		private List<string> IncludedTypesOf(string source)
		{
			var compilation = GeneratorProbeHarness.Compile("namespace Probe\n{\n" + source + "\n}\n");
			var (containers, _) = GeneratorProbeHarness.RunGeneratorAndCaptureContainers(compilation);
			Assert.That(containers.ContainsKey("ProbeConverters"), Is.True, "the probe container must be parsed");
			var included = containers["ProbeConverters"].IncludedTypes.Select(static t => t.Type.FullName.TrimEnd('?')).ToList();
			Log($"included: {string.Join(", ", included)}");
			return included;
		}

		[Test]
		public void Test_Enrolled_Collection_Root_Generates_Nothing_And_Compiles()
		{
			//note: the enrolled element type is written 'global::Probe.Shelf' because the container's own generated
			// nested holder is also named 'Shelf', and inside the container declaration the nested name wins
			const string Probe = """

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.CrystalSerializable(typeof(Shelf))]
					[SnowBank.Data.CrystalSerializable(typeof(System.Collections.Generic.List<global::Probe.Shelf>))]
					public static partial class ProbeConverters
					{
					}
				""";

			var (generator, output, compilation) = RunOn(ShelfDto + Probe);

			Assert.That(output, Is.Empty, "the generated code must compile clean: enrolling a collection used to emit a nameless indexer");

			var cjson0019 = generator.Where(static d => d.Id == "CJSON0019").ToList();
			Assert.That(cjson0019, Has.Count.EqualTo(1), "the enrollment must report the guidance diagnostic");
			Assert.That(cjson0019[0].Severity, Is.EqualTo(DiagnosticSeverity.Warning));
			Assert.That(cjson0019[0].GetMessage(), Does.Contain("collections").And.Contain("natively"));
			Assert.That(generator.Where(static d => d.Id != "CJSON0019" && d.Severity >= DiagnosticSeverity.Warning), Is.Empty, "nothing else is reported");

			Assert.That(IncludedTypesOf(ShelfDto + Probe), Is.EqualTo(new[] { "Probe.Shelf" }), "the collection type gets no converter; the POCO enrolled next to it still does");
			Assert.That(GeneratedSources(compilation), Does.Contain("Shelf"), "the POCO enrolled next to the collection still generates");
		}

		[Test]
		public void Test_Enrolled_Dictionary_Root_Generates_Nothing_And_Compiles()
		{
			const string Probe = """

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.CrystalSerializable(typeof(Shelf))]
					[SnowBank.Data.CrystalSerializable(typeof(System.Collections.Generic.Dictionary<string, global::Probe.Shelf>))]
					public static partial class ProbeConverters
					{
					}
				""";

			var (generator, output, _) = RunOn(ShelfDto + Probe);

			Assert.That(output, Is.Empty, "the generated code must compile clean");

			var cjson0019 = generator.Where(static d => d.Id == "CJSON0019").ToList();
			Assert.That(cjson0019, Has.Count.EqualTo(1), "the enrollment must report the guidance diagnostic");
			Assert.That(cjson0019[0].GetMessage(), Does.Contain("dictionaries"));

			Assert.That(IncludedTypesOf(ShelfDto + Probe), Is.EqualTo(new[] { "Probe.Shelf" }), "the dictionary type gets no converter");
		}

		[Test]
		public void Test_Enrolled_Scalar_Root_Generates_Nothing_And_Compiles()
		{
			const string Probe = """

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.CrystalSerializable(typeof(Shelf))]
					[SnowBank.Data.CrystalSerializable(typeof(string))]
					public static partial class ProbeConverters
					{
					}
				""";

			var (generator, output, _) = RunOn(ShelfDto + Probe);

			Assert.That(output, Is.Empty, "the generated code must compile clean: a scalar root used to fail the same way a collection root did");

			var cjson0019 = generator.Where(static d => d.Id == "CJSON0019").ToList();
			Assert.That(cjson0019, Has.Count.EqualTo(1), "the enrollment must report the guidance diagnostic");
			Assert.That(cjson0019[0].GetMessage(), Does.Contain("scalars"));

			Assert.That(IncludedTypesOf(ShelfDto + Probe), Is.EqualTo(new[] { "Probe.Shelf" }), "the scalar type gets no converter");
		}

		[Test]
		public void Test_Enrolled_Poco_Is_Not_Affected()
		{
			var (generator, output, compilation) = RunOn(ShelfDto + """

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.CrystalSerializable(typeof(Shelf))]
					public static partial class ProbeConverters
					{
					}
				""");

			Assert.That(output, Is.Empty, "the generated code must compile clean");
			Assert.That(generator.Where(static d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty, "a POCO enrollment reports nothing");
			Assert.That(GeneratedSources(compilation), Does.Contain("Shelf"), "the POCO still gets its generated converter");
		}

		[Test]
		public void Test_Collection_Member_Of_An_Enrolled_Poco_Is_Not_Affected()
		{
			// the guard targets the enrolment decision, not the crawler: a List<T> or Dictionary<K,V> reached as a MEMBER
			// type has always been handled by the member paths, which descend to the element / value type and never
			// enqueue the container type itself
			var (generator, output, compilation) = RunOn(ShelfDto + """

					public sealed record Aisle
					{
						public System.Collections.Generic.List<Shelf>? Shelves { get; set; }
						public System.Collections.Generic.Dictionary<string, Shelf>? ByLabel { get; set; }
						public string? Name { get; set; }
					}

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.CrystalSerializable(typeof(Aisle))]
					public static partial class ProbeConverters
					{
					}
				""");

			Assert.That(output, Is.Empty, "the generated code must compile clean");
			Assert.That(generator.Where(static d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty, "a collection or dictionary MEMBER reports nothing");

			var generated = GeneratedSources(compilation);
			Assert.That(generated, Does.Contain("Aisle"), "the enrolled POCO generates");
			Assert.That(generated, Does.Contain("Shelf"), "and so does the element type the crawler reached through the collection member");
		}

	}

}
