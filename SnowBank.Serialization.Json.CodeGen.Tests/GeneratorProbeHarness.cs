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
	using System.IO;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp;
	using SnowBank.SourceAnalysis;

	/// <summary>Drives the source generator in-process over a probe source, the way a consumer project's compilation would</summary>
	internal static class GeneratorProbeHarness
	{

		/// <summary>Every assembly the runtime trusts (framework + the test's own dependencies, including SnowBank.Core): what a consumer project references, minus any MSBuild-injected global usings</summary>
		public static readonly IReadOnlyList<MetadataReference> References =
			((string) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
			.Split(Path.PathSeparator)
			.Where(static path => !string.IsNullOrEmpty(path) && File.Exists(path))
			.Select(static path => (MetadataReference) MetadataReference.CreateFromFile(path))
			.ToList();

		public static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

		/// <summary>Parses a probe source into a compilation with NO global usings and nullable enabled</summary>
		public static CSharpCompilation Compile(string source, string assemblyName = "ProbeAssembly")
			=> CSharpCompilation.Create(
				assemblyName,
				syntaxTrees: [ CSharpSyntaxTree.ParseText(source, ParseOptions) ],
				references: References,
				options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

		/// <summary>Runs the generator over the compilation, returning the updated compilation and the generator's own diagnostics</summary>
		public static (Compilation Output, ImmutableArray<Diagnostic> GeneratorDiagnostics) RunGenerator(CSharpCompilation compilation)
		{
			var driver = CSharpGeneratorDriver.Create([ new CrystalJsonSourceGenerator().AsSourceGenerator() ], parseOptions: ParseOptions);
			driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
			return (output, diagnostics);
		}

		/// <summary>Names of the generator's tracked parsing steps: one output per container (converter containers and self-serializable types)</summary>
		private static readonly string[] ContainerTrackingNames = [ "CrystalJsonSpec", "CrystalJsonSelfSpec" ];

		/// <summary>Runs the generator, returning the container metadata the parser produced for each container of the probe, keyed by container name</summary>
		/// <remarks>Reads the driver's tracked incremental steps: this observes what the parser RESOLVED, which is the contract of a parsing-only change, before any of it reaches the emitted source.</remarks>
		public static (Dictionary<string, CrystalJsonContainerMetadata> Containers, ImmutableArray<Diagnostic> GeneratorDiagnostics) RunGeneratorAndCaptureContainers(CSharpCompilation compilation)
		{
			GeneratorDriver driver = CSharpGeneratorDriver.Create(
				[ new CrystalJsonSourceGenerator().AsSourceGenerator() ],
				additionalTexts: null,
				parseOptions: ParseOptions,
				optionsProvider: null,
				//note: 'None' disables NO output (the diagnostics are reported through the source output, so they must stay enabled)
				driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

			driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

			var containers = new Dictionary<string, CrystalJsonContainerMetadata>(StringComparer.Ordinal);
			foreach (var generatorResult in driver.GetRunResult().Results)
			{
				foreach (var trackingName in ContainerTrackingNames)
				{
					if (!generatorResult.TrackedSteps.TryGetValue(trackingName, out var steps)) continue;

					foreach (var step in steps)
					{
						foreach (var output in step.Outputs)
						{
							// the step emits the (Metadata, Diagnostics) tuple produced by the parser; the metadata is null when the container was refused
							if (output.Value is ValueTuple<CrystalJsonContainerMetadata?, ImmutableEquatableArray<DiagnosticInfo>> { Item1: { } metadata })
							{
								// the key is the container's SIMPLE name: two probe containers sharing one must fail here, instead of silently overwriting the entry the test then asserts on
								if (containers.TryGetValue(metadata.Name, out var previous))
								{
									if (previous != metadata) throw new InvalidOperationException($"The probe declares two different containers named '{metadata.Name}'; give them distinct names, since this harness keys them by simple name.");
								}
								else
								{
									containers.Add(metadata.Name, metadata);
								}
							}
						}
					}
				}
			}

			return (containers, diagnostics);
		}

	}

}
