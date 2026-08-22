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

	/// <summary>Pins the two flavors of the non-public accessor thunks (<c>[UnsafeAccessor]</c> on modern TFMs, reflection accessors where the attribute is absent) and the <c>CJSON0012</c> internal-unannotated nudge</summary>
	/// <remarks>The runtime behavior of the thunks is pinned by <see cref="JsonIncludeProbeFacts"/>, which executes the REAL generated code of this test project; this fixture pins flavor selection and diagnostics through the in-process harness.</remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class NonPublicGenerationFacts : SimpleTest
	{

		private const string IncludeProbe = """
			namespace Probe
			{

				public sealed record ProbeDto
				{
					[System.Text.Json.Serialization.JsonInclude]
					private string? Secret { get; set; }

					public int Plain { get; set; }
				}

				[SnowBank.Data.Json.CrystalJsonConverter]
				[SnowBank.Data.CrystalSerializable(typeof(ProbeDto))]
				public static partial class ProbeConverters
				{
				}

			}
			""";

		[Test]
		public void Test_Modern_Targets_Use_UnsafeAccessor_Thunks()
		{
			var compilation = GeneratorProbeHarness.Compile(IncludeProbe);
			var (output, diagnostics) = GeneratorProbeHarness.RunGenerator(compilation);

			Assert.That(diagnostics.Where(static d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty, "no diagnostic: the non-public member is supported (SYSLIB1038 is retired)");

			var generated = string.Join("\n", output.SyntaxTrees.Skip(1).Select(static t => t.ToString()));
			Assert.That(generated, Does.Contain("UnsafeAccessor"), "a compilation whose core library defines [UnsafeAccessor] gets the zero-cost thunks");
			Assert.That(generated, Does.Not.Contain("BindingFlags"), "no reflection fallback on a modern TFM");

			Assert.That(output.GetDiagnostics().Where(static d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty, "the emitted thunks must compile clean");
		}

		[Test]
		public void Test_Internal_Unannotated_Member_Gets_The_CJSON0012_Nudge()
		{
			var compilation = GeneratorProbeHarness.Compile("""
				namespace Probe
				{

					public sealed record ProbeDto
					{
						internal string? Hidden { get; set; }

						public int Plain { get; set; }
					}

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.CrystalSerializable(typeof(ProbeDto))]
					public static partial class ProbeConverters
					{
					}

				}
				""");
			var (_, diagnostics) = GeneratorProbeHarness.RunGenerator(compilation);

			var nudge = diagnostics.SingleOrDefault(static d => d.Id == "CJSON0012");
			Assert.That(nudge, Is.Not.Null, "an internal member with no include/exclude signal diverges between the paths, and the divergence must be observable");
			Assert.That(nudge!.Severity, Is.EqualTo(DiagnosticSeverity.Warning), "a suppressible warning, not an error: existing generated outputs depend on the inclusion");
			var message = nudge.GetMessage();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(message, Does.Contain("Hidden"), "the message names the member");
				Assert.That(message, Does.Contain("JsonInclude"), "the message names the include resolution");
				Assert.That(message, Does.Contain("JsonIgnore"), "the message names the exclude resolution");
			}
		}

		[Test]
		public void Test_Internal_Member_With_A_Pinned_Intent_Gets_No_Nudge()
		{
			var compilation = GeneratorProbeHarness.Compile("""
				namespace Probe
				{

					public sealed record ProbeDto
					{
						[System.Text.Json.Serialization.JsonInclude]
						internal string? Included { get; set; }

						[System.Text.Json.Serialization.JsonIgnore]
						internal string? Excluded { get; set; }

						public int Plain { get; set; }
					}

					[SnowBank.Data.Json.CrystalJsonConverter]
					[SnowBank.Data.CrystalSerializable(typeof(ProbeDto))]
					public static partial class ProbeConverters
					{
					}

				}
				""");
			var (_, diagnostics) = GeneratorProbeHarness.RunGenerator(compilation);
			Assert.That(diagnostics.Where(static d => d.Id == "CJSON0012"), Is.Empty, "an explicit signal pins the intent on both paths: nothing to nudge");
		}

		[Test]
		public void Test_Downlevel_Targets_Fall_Back_To_Reflection_Accessors()
		{
			// a netstandard2.0 compilation has no [UnsafeAccessor]: the generator must emit the reflection
			// flavor instead of refusing (CJ3-6). Since Q9, the proxy surface (ToReadOnly/ToMutable, the
			// ReadOnly/Writable proxy types) is gated off by SupportsJsonProxies when the consumer cannot
			// see it, which is exactly the case here (the lite netstandard2.0 build of SnowBank.Core has no
			// proxy interfaces): so the FULL generated output is expected to compile clean against the lite
			// reference set, not just the reflection-accessor flavor selection pinned below.
			var references = TryBuildNetStandard20References();
			Assume.That(references, Is.Not.Null, "needs the netstandard2.0 reference pack in the nuget cache AND a SnowBank.Core netstandard2.0 build in artifacts (run the lite-target gate first)");

			var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
			var compilation = CSharpCompilation.Create(
				"DownlevelProbe",
				syntaxTrees:
				[
					CSharpSyntaxTree.ParseText(IncludeProbe, parseOptions),
					// netstandard2.0 has no System.Text.Json: consumers declare the attribute lookalike, matched by name
					CSharpSyntaxTree.ParseText("""
						namespace System.Text.Json.Serialization
						{
							[System.AttributeUsage(System.AttributeTargets.Property | System.AttributeTargets.Field)]
							public sealed class JsonIncludeAttribute : System.Attribute
							{
							}
						}
						""", parseOptions),
				],
				references,
				new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

			var (output, diagnostics) = GeneratorProbeHarness.RunGenerator(compilation);
			Assert.That(diagnostics.Where(static d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty, "the downlevel flavor is a fallback, never a refusal (CJ3-6)");

			var generated = string.Join("\n", output.SyntaxTrees.Skip(2).Select(static t => t.ToString()));
			Assert.That(generated, Does.Contain("TypeMapper"), "the generator must have produced the container code");
			Assert.That(generated, Does.Contain("BindingFlags"), "no [UnsafeAccessor] in the core library: the thunks use reflection accessors");
			Assert.That(generated, Does.Not.Contain("UnsafeAccessor"), "the zero-cost flavor must not be emitted where the attribute does not exist");

			// the CodeGen-level lite assertion: the FULL generated output (not just the accessor flavor) must
			// actually compile against the lite netstandard2.0 build, proxy surface excluded
			var errors = output.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error).ToList();
			foreach (var diagnostic in errors)
			{
				Log(diagnostic.ToString());
			}
			Assert.That(errors, Is.Empty, "the full generated output must compile against the lite netstandard2.0 build of SnowBank.Core, with the proxy surface excluded");
		}

		private const string MetadataDtoSource = """
			namespace Probe
			{

				[System.Runtime.Serialization.DataContract]
				public sealed class MetadataDto
				{
					[System.Runtime.Serialization.DataMember]
					private string? Secret { get; set; }

					[System.Runtime.Serialization.DataMember]
					public int Counted { get; private set; }

					[System.Runtime.Serialization.DataMember]
					public string? Plain { get; set; }
				}

			}
			""";

		private const string MetadataContainerSource = """
			namespace Probe
			{

				[SnowBank.Data.CrystalConverter]
				[SnowBank.Data.Json.CrystalJsonOutput(SnowBank.Data.Json.CrystalJsonSerializerDefaults.DataContractCompat)]
				[SnowBank.Data.Xml.CrystalXmlOutput]
				[SnowBank.Data.CrystalSerializable(typeof(Probe.MetadataDto))]
				public static partial class MetadataConverters
				{
				}

			}
			""";

		[Test]
		public void Test_A_Private_DataMember_In_A_Referenced_Assembly_Is_Generable()
		{
			// a certification host references the product as compiled assemblies, and Roslyn's default metadata
			// import drops private members: a private [DataMember] vanishes from the contract and a private setter
			// makes its property look read-only (CXML0013). The harness imports all members, and this fact is what
			// notices that option regressing.
			var dtoReference = GeneratorProbeHarness.CompileToReference(MetadataDtoSource, "ProbeDtoAssembly");

			var compilation = GeneratorProbeHarness.Compile(MetadataContainerSource, assemblyName: "ProbeConsumerAssembly").AddReferences(dtoReference);
			var (output, diagnostics) = GeneratorProbeHarness.RunGenerator(compilation);

			Assert.That(diagnostics.Where(static d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty, "every member of the referenced contract is visible, so nothing is refused and nothing nudges");

			var generated = string.Join("\n", output.SyntaxTrees.Skip(1).Select(static t => t.ToString()));
			Assert.That(generated, Does.Contain("Secret"), "the private [DataMember] of the referenced type must be part of the contract");
			Assert.That(generated, Does.Contain("Counted"), "the private-setter [DataMember] of the referenced type must be writable, not read-only");
			Assert.That(generated, Does.Contain("UnsafeAccessor"), "the non-public members go through the zero-cost thunks");

			Assert.That(output.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error), Is.Empty, "the emitted thunks must compile clean against the metadata reference");
		}

		[Test]
		public void Test_The_Default_Metadata_Import_Loses_The_Same_Members()
		{
			// the measurement this fixture's .All import exists to fix, pinned so the contrast stays observable: the
			// same contract through a DEFAULT-import compilation loses its private member and refuses the
			// private-setter property as read-only. If Roslyn ever changes the default, this fact says so.
			var dtoReference = GeneratorProbeHarness.CompileToReference(MetadataDtoSource, "ProbeDtoAssembly");

			var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
			var compilation = CSharpCompilation.Create(
				"ProbeConsumerAssembly",
				syntaxTrees: [ CSharpSyntaxTree.ParseText(MetadataContainerSource, parseOptions) ],
				references: [ ..GeneratorProbeHarness.References, dtoReference ],
				options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
			var (output, diagnostics) = GeneratorProbeHarness.RunGenerator(compilation);

			Assert.That(diagnostics.Select(static d => d.Id), Does.Contain("CXML0013"), "under the default import the private setter is invisible, so the property looks read-only and is refused");

			var generated = string.Join("\n", output.SyntaxTrees.Skip(1).Select(static t => t.ToString()));
			Assert.That(generated, Does.Not.Contain("Secret"), "under the default import the private member does not exist at all");
		}

		/// <summary>Builds a netstandard2.0 reference set (the SDK's bundled `ref/netstandard.dll` facade + the lite build of SnowBank.Core and its own dependency closure), or <see langword="null"/> when either is absent</summary>
		/// <remarks>The whole output directory is referenced, not just <c>SnowBank.Core.dll</c>: its netstandard2.0 build pulls in <c>System.Memory</c>, <c>System.Buffers</c> and <c>System.Runtime.CompilerServices.Unsafe</c> (the BCL polyfill packages), and the generated code (spans, <c>ArrayPool</c>, <c>Unsafe</c>) resolves the same types from those assemblies exactly as a real consumer project would.</remarks>
		private static IReadOnlyList<MetadataReference>? TryBuildNetStandard20References()
		{
			// the SDK bundles the flattened netstandard2.0 reference facade at sdk/<version>/ref/netstandard.dll;
			// resolve the dotnet root from the running muxer, and take the highest SDK that carries the facade
			var muxer = Environment.ProcessPath;
			if (string.IsNullOrEmpty(muxer)) return null;
			var sdkRoot = Path.Combine(Path.GetDirectoryName(muxer)!, "sdk");
			if (!Directory.Exists(sdkRoot)) return null;
			var facade = Directory.GetDirectories(sdkRoot)
				.Select(static sdk => Path.Combine(sdk, "ref", "netstandard.dll"))
				.Where(File.Exists)
				.OrderByDescending(static x => x, StringComparer.OrdinalIgnoreCase)
				.FirstOrDefault();
			if (facade is null) return null;

			// artifacts/bin/<TestProject>/debug_net10.0 -> artifacts/bin/SnowBank.Core/debug_netstandard2.0
			var coreDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "SnowBank.Core", "debug_netstandard2.0"));
			var corePath = Path.Combine(coreDir, "SnowBank.Core.dll");
			if (!File.Exists(corePath)) return null;

			var references = new List<MetadataReference> { MetadataReference.CreateFromFile(facade) };
			references.AddRange(Directory.GetFiles(coreDir, "*.dll").Select(static path => (MetadataReference) MetadataReference.CreateFromFile(path)));
			return references;
		}

	}

}
