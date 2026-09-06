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

	/// <summary>Pins how a generated converter constructs a type that has no parameterless constructor: the primary constructor of a positional record, the single public constructor of a class, or the one marked <c>[JsonConstructor]</c>, with each parameter bound to the serialized member of the same name</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class ConstructorBindingFacts : SimpleTest
	{

		private const string PositionalRecordSource = """
			namespace Probe
			{
				public sealed record Toy(string Name, int Size = 3);

				[SnowBank.Data.Json.CrystalJsonConverter]
				[SnowBank.Data.CrystalSerializable(typeof(Probe.Toy))]
				public static partial class Host { }
			}
			""";

		private const string SingleConstructorClassSource = """
			namespace Probe
			{
				public sealed class Bowl
				{
					public Bowl(string material) { this.Material = material; }
					public string Material { get; }
					public int Capacity { get; set; }
				}

				[SnowBank.Data.Json.CrystalJsonConverter]
				[SnowBank.Data.CrystalSerializable(typeof(Probe.Bowl))]
				public static partial class Host { }
			}
			""";

		private const string AmbiguousConstructorsSource = """
			namespace Probe
			{
				public sealed class Bowl
				{
					public Bowl(string material) { this.Material = material; }
					public Bowl(string material, int capacity) { this.Material = material; this.Capacity = capacity; }
					public string Material { get; }
					public int Capacity { get; }
				}

				[SnowBank.Data.Json.CrystalJsonConverter]
				[SnowBank.Data.CrystalSerializable(typeof(Probe.Bowl))]
				public static partial class Host { }
			}
			""";

		/// <summary>The attribute is matched by name, so a consumer's own <c>JsonConstructorAttribute</c> works as well as the System.Text.Json one</summary>
		private const string MarkedConstructorSource = """
			namespace Acme.Serialization
			{
				[System.AttributeUsage(System.AttributeTargets.Constructor)]
				public sealed class JsonConstructorAttribute : System.Attribute { }
			}

			namespace Probe
			{
				public sealed class Bowl
				{
					public Bowl(string material) { this.Material = material; }
					[Acme.Serialization.JsonConstructor]
					public Bowl(string material, int capacity) { this.Material = material; this.Capacity = capacity; }
					public string Material { get; }
					public int Capacity { get; }
				}

				[SnowBank.Data.Json.CrystalJsonConverter]
				[SnowBank.Data.CrystalSerializable(typeof(Probe.Bowl))]
				public static partial class Host { }
			}
			""";

		private const string UnmatchedParameterSource = """
			namespace Probe
			{
				public sealed class Bowl
				{
					public Bowl(string material, int weightInGrams) { this.Material = material; }
					public string Material { get; }
				}

				[SnowBank.Data.Json.CrystalJsonConverter]
				[SnowBank.Data.CrystalSerializable(typeof(Probe.Bowl))]
				public static partial class Host { }
			}
			""";

		private static (string Generated, List<Diagnostic> Errors, System.Collections.Immutable.ImmutableArray<Diagnostic> GeneratorDiagnostics) Run(string source)
		{
			var compilation = GeneratorProbeHarness.Compile(source);
			var (output, diagnostics) = GeneratorProbeHarness.RunGenerator(compilation);
			foreach (var diagnostic in diagnostics) { Log($"generator: {diagnostic}"); }
			var generated = string.Join("\n", output.SyntaxTrees.Skip(1).Select(static t => t.ToString()));
			var errors = output.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error).ToList();
			foreach (var error in errors) { Log($"compiler: {error}"); }
			return (generated, errors, diagnostics);
		}

		[Test]
		public void Test_Positional_Record_Binds_Through_Its_Primary_Constructor()
		{
			var (generated, errors, diagnostics) = Run(PositionalRecordSource);

			using (Assert.EnterMultipleScope())
			{
				Assert.That(diagnostics.Where(static d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty, "a positional record is a supported shape: no diagnostic");
				Assert.That(errors, Is.Empty, "the generated Unpack must compile: it calls Toy(string, int), not a parameterless constructor");
				Assert.That(generated, Does.Contain("new ("), "the instance is built with constructor arguments");
				Assert.That(generated, Does.Contain(", 3)"), "an absent optional parameter takes the parameter's own default value");
			}
		}

		[Test]
		public void Test_Class_With_A_Single_Parameterized_Constructor_Binds_Through_It()
		{
			var (generated, errors, diagnostics) = Run(SingleConstructorClassSource);

			using (Assert.EnterMultipleScope())
			{
				Assert.That(diagnostics.Where(static d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty);
				Assert.That(errors, Is.Empty, "Bowl(string) is the only public constructor and its parameter matches Material");
				Assert.That(generated, Does.Match(@"Capacity = /\*"), "the settable member that no parameter covers stays in the object initializer");
			}
		}

		[Test]
		public void Test_Several_Matching_Constructors_Are_Rejected()
		{
			var (_, _, diagnostics) = Run(AmbiguousConstructorsSource);

			var rejection = diagnostics.SingleOrDefault(static d => d.Id == "CJSON0027");
			Assert.That(rejection, Is.Not.Null, "two public constructors both match the members: the generator cannot pick one");
			Assert.That(rejection!.Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(rejection.GetMessage(), Does.Contain("JsonConstructor"), "the message names the remedy");
		}

		[Test]
		public void Test_JsonConstructor_Selects_The_Constructor()
		{
			var (generated, errors, diagnostics) = Run(MarkedConstructorSource);

			using (Assert.EnterMultipleScope())
			{
				Assert.That(diagnostics.Where(static d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty);
				Assert.That(errors, Is.Empty);
				Assert.That(generated, Does.Contain("return new (").And.Contain("PropertyNames.Capacity, 0))"), "the marked two-parameter constructor is the one called, Capacity as its last argument");
				Assert.That(generated, Does.Not.Match(@"Capacity = /\*"), "a member the constructor covers leaves the object initializer (every initializer entry carries a /* tag */ comment)");
			}
		}

		[Test]
		public void Test_A_Parameter_That_Matches_No_Member_Is_Rejected()
		{
			var (_, _, diagnostics) = Run(UnmatchedParameterSource);

			var rejection = diagnostics.SingleOrDefault(static d => d.Id == "CJSON0027");
			Assert.That(rejection, Is.Not.Null, "weightInGrams matches no serialized member, so no constructor can be called from the document");
			Assert.That(rejection!.GetMessage(), Does.Contain("Probe.Bowl"));
		}

	}

}
