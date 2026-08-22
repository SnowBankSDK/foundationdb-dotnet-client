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
	using System.Text.RegularExpressions;
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
		/// <remarks>An indexer is not serialization state on any output, and the reference serializer ignores it. One shared collection type carrying an indexer breaks every container that reaches it.</remarks>
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

		// --- redeclared members -----------------------------------------------------------------------------------
		//
		// Pins that a member redeclared further down the hierarchy (override, or new) is collected once, at the
		// level the contract declares it.
		//
		// The generated PropertyNames class declares one constant per member name, so a member collected at two
		// levels declares the same constant twice (CS0102).
		//
		// An override is the same contract member as the one it overrides, and the reference serializer writes it
		// at the level that declares it, whether or not the override repeats [DataMember]. A 'new' member is a
		// distinct contract member, written at its own level; the reference serializer writes both copies, which
		// one C# accessor per name cannot reproduce.

		private static string Probe(string types) => $$"""
			#nullable enable
			namespace Probe
			{
			{{types}}

				[SnowBank.Data.CrystalConverter]
				[SnowBank.Data.Json.CrystalJsonOutput(SnowBank.Data.Json.CrystalJsonSerializerDefaults.DataContractCompat)]
				[SnowBank.Data.Xml.CrystalXmlOutput]
				[SnowBank.Data.CrystalSerializable(typeof(ProbeLeaf))]
				public static partial class ProbeConverters
				{
				}
			}
			""";

		private (List<Diagnostic> Errors, string Generated) RunOn(string types)
		{
			var compilation = GeneratorProbeHarness.Compile(Probe(types));

			Assert.That(
				compilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error),
				Is.Empty,
				"the probe source must compile clean on its own");

			var (outputCompilation, generatorDiagnostics) = GeneratorProbeHarness.RunGenerator(compilation);
			foreach (var diagnostic in generatorDiagnostics) { Log($"generator: [{diagnostic.Severity}] {diagnostic}"); }

			var errors = outputCompilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error).ToList();
			foreach (var diagnostic in errors) { Log($"compiler: {diagnostic}"); }

			return (errors, string.Concat(outputCompilation.SyntaxTrees.Skip(1).Select(static t => t.ToString())));
		}

		/// <summary>Reads the resolved member list of the leaf type, as <c>name@level</c> pairs in collection order (level 0 is <c>System.Object</c>, so the first declaring type sits at 1)</summary>
		private List<string> ReadLeafMembers(string types)
		{
			var compilation = GeneratorProbeHarness.Compile(Probe(types));
			var (containers, diagnostics) = GeneratorProbeHarness.RunGeneratorAndCaptureContainers(compilation);
			foreach (var d in diagnostics) { Log($"generator: [{d.Severity}] {d}"); }

			var leaf = containers["ProbeConverters"].IncludedTypes.Single(static t => t.Name == "ProbeLeaf");
			var members = leaf.Members.Select(static m => $"{m.MemberName}@{m.InheritanceLevel}").ToList();
			foreach (var member in members) { Log($"member: {member}"); }
			return members;
		}

		private const string OverrideHierarchy = """
				[System.Runtime.Serialization.DataContract]
				public class ProbeBase
				{
					[System.Runtime.Serialization.DataMember]
					public virtual string? Shared { get; set; }

					[System.Runtime.Serialization.DataMember]
					public string? Zulu { get; set; }
				}

				[System.Runtime.Serialization.DataContract]
				public class ProbeMiddle : ProbeBase
				{
					[System.Runtime.Serialization.DataMember]
					public override string? Shared { get; set; }

					[System.Runtime.Serialization.DataMember]
					public string? Alpha { get; set; }
				}

				[System.Runtime.Serialization.DataContract]
				public sealed class ProbeLeaf : ProbeMiddle
				{
					[System.Runtime.Serialization.DataMember]
					public string? Kilo { get; set; }
				}
			""";

		private const string ShadowHierarchy = """
				[System.Runtime.Serialization.DataContract]
				public class ProbeBase
				{
					[System.Runtime.Serialization.DataMember]
					public string? Shared { get; set; }

					[System.Runtime.Serialization.DataMember]
					public string? Zulu { get; set; }
				}

				[System.Runtime.Serialization.DataContract]
				public sealed class ProbeLeaf : ProbeBase
				{
					[System.Runtime.Serialization.DataMember]
					public new string? Shared { get; set; }

					[System.Runtime.Serialization.DataMember]
					public string? Alpha { get; set; }
				}
			""";

		[Test]
		public void Test_An_Overridden_Member_Compiles_And_Is_Emitted_Once()
		{
			var (errors, generated) = RunOn(OverrideHierarchy);

			Assert.That(errors.Where(static d => d.Id == "CS0102"), Is.Empty, "the member must not be declared twice in PropertyNames (CS0102)");
			Assert.That(errors, Is.Empty, "the generated container must compile over a hierarchy that overrides a member");

			Assert.That(
				Regex.Matches(generated, @"public const string Shared = ").Count,
				Is.EqualTo(1),
				"one member name declares one constant, whatever the number of levels that redeclare it");
		}

		[Test]
		public void Test_An_Overridden_Member_Stays_At_The_Level_That_Declares_It()
		{
			// the reference serializer writes the override with the base level's members, which is what the compat
			// profile orders by; taking the derived level would move the element in the document
			Assert.That(ReadLeafMembers(OverrideHierarchy), Is.EqualTo(new[] { "Shared@1", "Zulu@1", "Alpha@2", "Kilo@3" }));
		}

		[Test]
		public void Test_A_Shadowed_Member_Compiles_And_Is_Emitted_Once()
		{
			var (errors, generated) = RunOn(ShadowHierarchy);

			Assert.That(errors.Where(static d => d.Id == "CS0102"), Is.Empty, "the member must not be declared twice in PropertyNames (CS0102)");
			Assert.That(errors, Is.Empty, "the generated container must compile over a hierarchy that shadows a member");

			Assert.That(
				Regex.Matches(generated, @"public const string Shared = ").Count,
				Is.EqualTo(1),
				"one member name declares one constant, whatever the number of levels that redeclare it");
		}

		[Test]
		public void Test_A_Shadowed_Member_Takes_The_Level_That_Redeclares_It()
		{
			// a 'new' member is its own contract member: the accessor reads the derived one, and that is the level
			// the reference serializer writes it at
			Assert.That(ReadLeafMembers(ShadowHierarchy), Is.EqualTo(new[] { "Shared@2", "Zulu@1", "Alpha@2" }));
		}

		// --- explicit interface implementations ------------------------------------------------------------------
		//
		// Pins how an explicit interface implementation is handled: excluded from a plain DTO, refused with
		// CJSON0022 when a [DataContract] type opts one into the contract.
		//
		// The member name of an explicit implementation is the qualified interface member
		// (Acme.Contracts.IIdentified.Key). It is neither a legal C# identifier for the generated constant and
		// accessor thunk, nor the metadata name the thunk's [UnsafeAccessor] needs, so a collected one breaks the
		// generated code (a cascade of syntax errors around const string IIdentified.Key, and CS0246 on the
		// __get_ thunk).
		//
		// On a plain DTO the reference serializer ignores an explicit implementation, because the member is
		// private in metadata, so excluding it silently is correct. On a [DataContract] type membership is
		// accessibility-blind, and the reference serializer writes the member under the qualified name; silently
		// dropping it would produce a document short of one element, so the declaration is refused instead.

		private const string Interface = """
			#nullable enable
			namespace Probe
			{
				public interface IIdentified
				{
					string? Key { get; set; }
				}

			""";

		private const string Container = """

				[SnowBank.Data.CrystalConverter]
				[SnowBank.Data.Json.CrystalJsonOutput]
				[SnowBank.Data.Xml.CrystalXmlOutput]
				[SnowBank.Data.CrystalSerializable(typeof(ProbeDto))]
				public static partial class ProbeConverters
				{
				}
			}
			""";

		private (Compilation Output, List<Diagnostic> GeneratorDiagnostics, List<Diagnostic> Errors) RunOnInterfaceProbe(string dtoSource)
		{
			var compilation = GeneratorProbeHarness.Compile(Interface + dtoSource + Container);

			Assert.That(
				compilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error),
				Is.Empty,
				"the probe source must compile clean on its own");

			var (outputCompilation, generatorDiagnostics) = GeneratorProbeHarness.RunGenerator(compilation);
			foreach (var diagnostic in generatorDiagnostics) { Log($"generator: [{diagnostic.Severity}] {diagnostic}"); }

			var errors = outputCompilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error).ToList();
			foreach (var diagnostic in errors) { Log($"compiler: {diagnostic}"); }

			return (outputCompilation, generatorDiagnostics.ToList(), errors);
		}

		[Test]
		public void Test_An_Explicit_Implementation_On_A_Plain_Dto_Is_Excluded()
		{
			var (output, generatorDiagnostics, errors) = RunOnInterfaceProbe("""
					public sealed class ProbeDto : IIdentified
					{
						string? IIdentified.Key { get; set; }

						public string? Label { get; set; }
					}
				""");

			Assert.That(errors.Where(static d => d.Id == "CS0246"), Is.Empty, "the qualified member name must not reach an accessor thunk (CS0246)");
			Assert.That(errors, Is.Empty, "the generated container must compile with the explicit implementation excluded");
			Assert.That(generatorDiagnostics.Where(static d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty, "excluding it matches the reference serializer, so there is nothing to report");

			var generated = string.Concat(output.SyntaxTrees.Skip(1).Select(static t => t.ToString()));
			Assert.That(generated, Does.Not.Contain("IIdentified.Key"), "the explicit implementation must not reach the generated code");
			Assert.That(generated, Does.Contain("\"Label\""), "the normal members of the same type must still be serialized");
		}

		[Test]
		public void Test_An_Explicit_Implementation_Opted_Into_A_Data_Contract_Is_Refused()
		{
			var (_, generatorDiagnostics, errors) = RunOnInterfaceProbe("""
					[System.Runtime.Serialization.DataContract]
					public sealed class ProbeDto : IIdentified
					{
						[System.Runtime.Serialization.DataMember]
						string? IIdentified.Key { get; set; }

						[System.Runtime.Serialization.DataMember]
						public string? Label { get; set; }
					}
				""");

			var refusal = generatorDiagnostics.SingleOrDefault(static d => d.Id == "CJSON0022");
			Assert.That(refusal, Is.Not.Null, "the generator must refuse a [DataMember] on an explicit interface implementation");
			Assert.That(refusal!.Severity, Is.EqualTo(DiagnosticSeverity.Error), "the member belongs to the contract, so dropping it silently would write a short document");
			Assert.That(refusal.GetMessage(), Does.Contain("IIdentified.Key"), "the message must name the member");

			Assert.That(errors, Is.Empty, "the refused member is not emitted, so the refusal replaces the broken generated code instead of coming on top of it");
		}

		// --- abstract declared types ------------------------------------------------------------------------------
		//
		// Pins CJSON0023: a member whose DECLARED type is abstract or an interface, and whose declared type carries
		// no [JsonPolymorphic], is written with no discriminator. The writer emits the members of the runtime value,
		// and nothing in the document names the type that produced them, so a reader handed the declared type back
		// cannot rebuild the value.
		//
		// The warning covers that one shape. A declared type that already carries [JsonPolymorphic] gets its
		// discriminator, and a collection-shaped interface (IList<T> and friends) is a shape the writer projects
		// natively: neither is an untagged slot, so neither warns.

		private static string AbstractProbe(string types) => $$"""
			#nullable enable
			namespace Probe
			{
			{{types}}

				[SnowBank.Data.CrystalConverter]
				[SnowBank.Data.Json.CrystalJsonOutput]
				[SnowBank.Data.CrystalSerializable(typeof(ProbeHolder))]
				public static partial class HolderConverters
				{
				}
			}
			""";

		/// <summary>Runs the generator over a probe holding one member, and returns the CJSON0023 warnings it reported</summary>
		private List<Diagnostic> RunOnAbstractProbe(string types)
		{
			var compilation = GeneratorProbeHarness.Compile(AbstractProbe(types));

			Assert.That(
				compilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error),
				Is.Empty,
				"the probe source must compile clean on its own");

			var (_, generatorDiagnostics) = GeneratorProbeHarness.RunGenerator(compilation);
			foreach (var diagnostic in generatorDiagnostics) { Log($"generator: [{diagnostic.Severity}] {diagnostic}"); }

			return generatorDiagnostics.Where(static d => d.Id == "CJSON0023").ToList();
		}

		[Test]
		public void Test_An_Abstract_Member_Type_Without_A_Discriminator_Is_Reported()
		{
			var warnings = RunOnAbstractProbe("""
					public abstract class Shape
					{
						public string? Label { get; set; }
					}

					public sealed class Circle : Shape
					{
						public double Radius { get; set; }
					}

					public sealed record ProbeHolder
					{
						public Shape? Outline { get; init; }
					}
				""");

			Assert.That(warnings, Has.Count.EqualTo(1), "the abstract declared type is the only untagged slot in the probe");
			Assert.That(warnings[0].Severity, Is.EqualTo(DiagnosticSeverity.Warning), "the document is still written, so this is a warning and not a refusal");

			var message = warnings[0].GetMessage();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(message, Does.Contain("Probe.ProbeHolder.Outline"), "the message names the member");
				Assert.That(message, Does.Contain("Probe.Shape"), "the message names the declared type");
				Assert.That(message, Does.Contain("[JsonPolymorphic]"), "the message names the attribute that declares the tree");
				Assert.That(message, Does.Contain("[JsonDerivedType]"), "the message names the attribute that declares each derived type");
			}
		}

		[Test]
		public void Test_An_Interface_Member_Type_Without_A_Discriminator_Is_Reported()
		{
			// an interface slot is the same untagged slot as an abstract class: the writer has one runtime type to
			// emit and no name to write for it
			var warnings = RunOnAbstractProbe("""
					public interface IOutline
					{
						string? Label { get; set; }
					}

					public sealed record ProbeHolder
					{
						public IOutline? Outline { get; init; }
					}
				""");

			Assert.That(warnings, Has.Count.EqualTo(1), "the interface declared type is the only untagged slot in the probe");
			Assert.That(warnings[0].GetMessage(), Does.Contain("Probe.IOutline"), "the message names the declared type");
		}

		[Test]
		public void Test_A_Polymorphic_Member_Type_Is_Not_Reported()
		{
			var warnings = RunOnAbstractProbe("""
					[System.Text.Json.Serialization.JsonPolymorphic]
					[System.Text.Json.Serialization.JsonDerivedType(typeof(Circle), "circle")]
					public abstract class Shape
					{
						public string? Label { get; set; }
					}

					public sealed class Circle : Shape
					{
						public double Radius { get; set; }
					}

					public sealed record ProbeHolder
					{
						public Shape? Outline { get; init; }
					}
				""");

			Assert.That(warnings, Is.Empty, "the declared type carries a discriminator, so the document names the derived type that produced the members");
		}

		[Test]
		public void Test_A_Collection_Shaped_Interface_Member_Is_Not_Reported()
		{
			// IList<T> is abstract, but the writer projects it as an array: the element type is what carries the
			// values, and there is no derived type to name
			var warnings = RunOnAbstractProbe("""
					public sealed record ProbeHolder
					{
						public System.Collections.Generic.IList<string>? Tags { get; init; }
					}
				""");

			Assert.That(warnings, Is.Empty, "a collection shape is not polymorphism");
		}

	}

}
