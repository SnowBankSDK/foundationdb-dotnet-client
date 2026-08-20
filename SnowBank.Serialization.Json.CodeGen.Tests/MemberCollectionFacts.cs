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

	/// <summary>Pins which members the generator collects into a contract: an indexer is excluded, an explicit interface implementation is excluded unless a data contract opts it in, and a member redeclared down the hierarchy is collected once, at the declaring level</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class MemberCollectionFacts : SimpleTest
	{

		// --- indexers -------------------------------------------------------------------------------------------

		/// <summary>A collection-flavored type exposing a keyed indexer alongside normal instance members</summary>
		private const string ProbeSource = """
			#nullable enable
			namespace Probe
			{
				public sealed class ObjectBag
				{
					private readonly System.Collections.Generic.Dictionary<string, string> Items = new();

					// an indexer: not serialization state, and "this[]" is not a legal C# identifier (CS1001)
					public string this[string key]
					{
						get => this.Items[key];
						set => this.Items[key] = value;
					}

					// an overload, to prove the filter drops every indexer and not just the first one
					public string this[int position] => this.Label ?? "";

					// normal instance members
					public string? Label { get; set; }
				}

				public sealed record Shelf
				{
					public string? Name { get; init; }
					public ObjectBag? Bag { get; init; }
				}

				[SnowBank.Data.CrystalConverter]
				[SnowBank.Data.Json.CrystalJsonOutput]
				[SnowBank.Data.Xml.CrystalXmlOutput]
				[SnowBank.Data.CrystalSerializable(typeof(Shelf))]
				[SnowBank.Data.CrystalSerializable(typeof(ObjectBag))]
				public static partial class ShelfConverters
				{
				}
			}
			""";

		/// <summary>Pins that an indexer is never treated as serialization state: its member name is <c>this[]</c>, which is not an identifier a generated constant can carry (CS1001)</summary>
		/// <remarks>An indexer is not serialization state on any wire, and the reference serializer ignores it. One shared collection type carrying an indexer breaks every container that reaches it.</remarks>
		[Test]
		public void Test_An_Indexer_Is_Not_Serialized_And_Generated_Code_Compiles()
		{
			var compilation = GeneratorProbeHarness.Compile(ProbeSource);

			Assert.That(
				compilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error),
				Is.Empty,
				"the probe source must compile clean on its own");

			var (outputCompilation, generatorDiagnostics) = GeneratorProbeHarness.RunGenerator(compilation);
			foreach (var diagnostic in generatorDiagnostics) { Log($"generator: [{diagnostic.Severity}] {diagnostic}"); }

			var errors = outputCompilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error).ToList();
			foreach (var diagnostic in errors)
			{
				Log(diagnostic.ToString());
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

			// an indexer collected as a member declares "const string this[]" in PropertyNames, which does not parse
			Assert.That(errors.Where(static d => d.Id == "CS1001"), Is.Empty, "an indexer must not be emitted as a member constant (CS1001)");
			Assert.That(errors, Is.Empty, "the generated container must compile with indexers excluded");

			var generated = string.Concat(outputCompilation.SyntaxTrees.Skip(1).Select(static t => t.ToString()));
			Assert.That(generated, Does.Not.Contain("this[]"), "the indexer must not reach the generated code under any spelling");
			Assert.That(generated, Does.Contain("\"Label\""), "the normal members of the same type must still be serialized");
		}

	}

}
