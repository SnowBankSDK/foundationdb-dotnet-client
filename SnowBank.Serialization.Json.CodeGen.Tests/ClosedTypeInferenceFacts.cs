#region Copyright (c) 2023-2026 SnowBank SAS
// All rights reserved.
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
#endregion

namespace SnowBank.Serialization.Json.CodeGen.Tests
{
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp;

	/// <summary>Pins the inference of polymorphism from a C# 15 <c>closed</c> class, on <c>[JsonPolymorphic(InferClosedTypePolymorphism = true)]</c>: the derived types and their discriminators, and the diagnostics CJSON0028 to CJSON0030</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class ClosedTypeInferenceFacts : SimpleTest
	{

		/// <summary>The probes declare <c>closed</c> classes, a C# 15 feature</summary>
		private static readonly CSharpParseOptions PreviewParseOptions = new(LanguageVersion.Preview);

		/// <summary>Builds a probe that enrolls <paramref name="roots"/> in a converter container</summary>
		/// <remarks>The test project targets net10.0. Its runtime has no <c>IsClosedTypeAttribute</c>, and its System.Text.Json has no <c>InferClosedTypePolymorphism</c>, so the probe declares both. The generator matches the polymorphic attribute by full name, so the probe's copy stands in for the System.Text.Json 11 type.</remarks>
		private static string Probe(string types, params string[] roots) => $$"""
			#nullable enable
			namespace System.Runtime.CompilerServices
			{
				[System.AttributeUsage(System.AttributeTargets.Class, Inherited = false)]
				internal sealed class IsClosedTypeAttribute : System.Attribute
				{
					public System.Type[] DerivedTypes { get; set; } = new System.Type[0];
				}
			}
			namespace System.Text.Json.Serialization
			{
				[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Interface, Inherited = false)]
				internal sealed class JsonPolymorphicAttribute : System.Attribute
				{
					public string? TypeDiscriminatorPropertyName { get; set; }
					public bool InferClosedTypePolymorphism { get; set; }
				}
			}
			namespace Probe
			{
				using System.Text.Json.Serialization;

			{{types}}

				[SnowBank.Data.CrystalConverter]
				[SnowBank.Data.Json.CrystalJsonOutput]
			{{string.Join("\n", roots.Select(static r => $"\t[SnowBank.Data.CrystalSerializable(typeof({r}))]"))}}
				public static partial class ProbeConverters
				{
				}
			}
			""";

		/// <summary>Runs the generator over a probe, and returns the parsed container with the generator's diagnostics</summary>
		private (CrystalJsonContainerMetadata Container, List<Diagnostic> Diagnostics) Run(string source)
		{
			var compilation = GeneratorProbeHarness.Compile(source, PreviewParseOptions);
			Assert.That(
				compilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error),
				Is.Empty,
				"the probe source must compile clean on its own");

			var (containers, diagnostics) = GeneratorProbeHarness.RunGeneratorAndCaptureContainers(compilation, PreviewParseOptions);
			foreach (var diagnostic in diagnostics) { Log($"generator: [{diagnostic.Severity}] {diagnostic}"); }
			return (containers["ProbeConverters"], diagnostics.ToList());
		}

		/// <summary>Returns the discriminators of a polymorphic root, keyed by the full name of each derived type</summary>
		private static Dictionary<string, object?> DerivedTypesOf(CrystalJsonContainerMetadata container, string rootName)
		{
			var root = container.IncludedTypes.Single(t => t.Name == rootName);
			Assert.That(root.IsPolymorphicRoot, Is.True, $"{rootName} is a polymorphic root");
			return root.DerivedTypes.ToDictionary(static x => x.Type.FullName, static x => x.Discriminator);
		}

		private static List<string> IdsOf(List<Diagnostic> diagnostics) => diagnostics.Select(static d => d.Id).Where(static id => id is "CJSON0028" or "CJSON0029" or "CJSON0030").ToList();

		[Test]
		public void Test_Closed_Root_Infers_Derived_Types_With_Simple_Names()
		{
			var (container, diagnostics) = Run(Probe("""
					[JsonPolymorphic(InferClosedTypePolymorphism = true)]
					public closed class CartEvent { public string? CartId { get; set; } }
					public sealed class CartCreated : CartEvent { public string? Owner { get; set; } }
					public sealed class ItemAdded : CartEvent { public int Quantity { get; set; } }
				""", "CartEvent"));

			Assert.That(IdsOf(diagnostics), Is.Empty);
			Assert.That(DerivedTypesOf(container, "CartEvent"), Is.EquivalentTo(new Dictionary<string, object?>
			{
				["Probe.CartCreated"] = "CartCreated",
				["Probe.ItemAdded"] = "ItemAdded",
			}));
			// the derived types are crawled into the container, like the [JsonDerivedType] ones
			Assert.That(container.IncludedTypes.Select(static t => t.Name), Is.SupersetOf(new[] { "CartCreated", "ItemAdded" }));
		}

		[Test]
		public void Test_Nested_Closed_Class_Is_Replaced_By_Its_Derived_Types()
		{
			var (container, diagnostics) = Run(Probe("""
					[JsonPolymorphic(InferClosedTypePolymorphism = true)]
					public closed class Shape { }
					public closed class Polygon : Shape { public int Sides { get; set; } }
					public sealed class Triangle : Polygon { }
					public sealed class Square : Polygon { }
					public sealed class Circle : Shape { public double Radius { get; set; } }
				""", "Shape"));

			Assert.That(IdsOf(diagnostics), Is.Empty);
			Assert.That(DerivedTypesOf(container, "Shape"), Is.EquivalentTo(new Dictionary<string, object?>
			{
				["Probe.Circle"] = "Circle",
				["Probe.Square"] = "Square",
				["Probe.Triangle"] = "Triangle",
			}));
		}

		[Test]
		public void Test_Generic_Closed_Root_Binds_Derived_Types_To_Its_Type_Arguments()
		{
			var (container, diagnostics) = Run(Probe("""
					[JsonPolymorphic(InferClosedTypePolymorphism = true)]
					public closed class Result<T> { }
					public sealed class Success<T> : Result<T> { public T? Value { get; set; } }
					public sealed class IntFailure : Result<int> { public string? Error { get; set; } }
				""", "Result<int>"));

			Assert.That(IdsOf(diagnostics), Is.Empty);
			var derived = container.IncludedTypes.Single(static t => t.Name.StartsWith("Result", StringComparison.Ordinal)).DerivedTypes;
			Assert.That(derived.Select(static x => (x.Symbol.ToDisplayString(), x.Discriminator)), Is.EquivalentTo(new[]
			{
				("Probe.Success<int>", (object?) "Success"),
				("Probe.IntFailure", (object?) "IntFailure"),
			}));
		}

		[Test]
		public void Test_Closed_Class_Without_OptIn_Is_Not_Polymorphic()
		{
			var (container, diagnostics) = Run(Probe("""
					[JsonPolymorphic(InferClosedTypePolymorphism = true)]
					public closed class Inferred { }
					public sealed class InferredLeaf : Inferred { }

					public closed class Plain { }
					public sealed class PlainLeaf : Plain { }

					public sealed class Holder
					{
						public Inferred? A { get; set; }
						public PlainLeaf? B { get; set; }
					}
				""", "Holder"));

			Assert.That(IdsOf(diagnostics), Is.Empty);
			Assert.That(container.IncludedTypes.Single(static t => t.Name == "Inferred").IsPolymorphicRoot, Is.True, "control: the opt-in makes a polymorphic root");
			Assert.That(container.IncludedTypes.Single(static t => t.Name == "PlainLeaf").IsPolymorphicRoot, Is.False);
			Assert.That(container.IncludedTypes.Any(static t => t.Name == "Plain"), Is.False, "nothing pulls the closed base of PlainLeaf into the container");
		}

		[Test]
		public void Test_OptIn_On_A_Class_That_Is_Not_Closed_Is_Reported()
		{
			var (_, diagnostics) = Run(Probe("""
					[JsonPolymorphic(InferClosedTypePolymorphism = true)]
					public abstract class Shape { }
					public sealed class Circle : Shape { }
				""", "Shape"));

			Assert.That(IdsOf(diagnostics), Is.EqualTo(new[] { "CJSON0028" }));
			Assert.That(diagnostics.Single(static d => d.Id == "CJSON0028").Severity, Is.EqualTo(DiagnosticSeverity.Error));
		}

		[Test]
		public void Test_OptIn_With_Explicit_Derived_Types_Is_Reported_And_Uses_The_Explicit_List()
		{
			var (container, diagnostics) = Run(Probe("""
					[JsonPolymorphic(InferClosedTypePolymorphism = true)]
					[JsonDerivedType(typeof(Circle), "circle")]
					public closed class Shape { }
					public sealed class Circle : Shape { }
					public sealed class Square : Shape { }
				""", "Shape"));

			Assert.That(IdsOf(diagnostics), Is.EqualTo(new[] { "CJSON0029" }));
			Assert.That(diagnostics.Single(static d => d.Id == "CJSON0029").Severity, Is.EqualTo(DiagnosticSeverity.Warning));
			Assert.That(DerivedTypesOf(container, "Shape"), Is.EquivalentTo(new Dictionary<string, object?> { ["Probe.Circle"] = "circle" }));
		}

		[Test]
		public void Test_Duplicate_Simple_Names_Are_Reported()
		{
			var (_, diagnostics) = Run(Probe("""
					[JsonPolymorphic(InferClosedTypePolymorphism = true)]
					public closed class Shape { }
					public sealed class OuterA : Shape { public sealed class Same : Shape { } }
					public sealed class OuterB : Shape { public sealed class Same : Shape { } }
				""", "Shape"));

			Assert.That(IdsOf(diagnostics), Is.EqualTo(new[] { "CJSON0030" }));
			Assert.That(diagnostics.Single(static d => d.Id == "CJSON0030").GetMessage(), Does.Contain("'Same'"));
		}

		[Test]
		public void Test_Closed_Root_Without_Derived_Types_Is_Reported()
		{
			var (_, diagnostics) = Run(Probe("""
					[JsonPolymorphic(InferClosedTypePolymorphism = true)]
					public closed class Shape { }
				""", "Shape"));

			Assert.That(IdsOf(diagnostics), Is.EqualTo(new[] { "CJSON0030" }));
		}

		[Test]
		public void Test_Closed_Hierarchy_Inside_A_Generic_Type_Is_Reported()
		{
			var (_, diagnostics) = Run(Probe("""
					public static class Outer<T>
					{
						[JsonPolymorphic(InferClosedTypePolymorphism = true)]
						public closed class Node { }
						public sealed class Leaf : Node { public T? Value { get; set; } }
					}
				""", "Outer<int>.Node"));

			Assert.That(diagnostics.Select(static d => d.Id), Does.Not.Contain("CJSON0001"), "the generator does not crash");
			Assert.That(IdsOf(diagnostics), Is.EqualTo(new[] { "CJSON0030" }));
			Assert.That(diagnostics.Single(static d => d.Id == "CJSON0030").GetMessage(), Does.Contain("generic type"));
		}

		[Test]
		public void Test_Generic_Constraint_Violation_Is_Reported()
		{
			var (_, diagnostics) = Run(Probe("""
					[JsonPolymorphic(InferClosedTypePolymorphism = true)]
					public closed class Box<T> { }
					public sealed class StructBox<T> : Box<T> where T : struct { }
					public sealed class AnyBox<T> : Box<T> { }
				""", "Box<string>"));

			Assert.That(IdsOf(diagnostics), Is.EqualTo(new[] { "CJSON0030" }));
			Assert.That(diagnostics.Single(static d => d.Id == "CJSON0030").GetMessage(), Does.Contain("constraints"));
		}

		[Test]
		public void Test_Generic_Constraints_That_Hold_Are_Accepted()
		{
			var (container, diagnostics) = Run(Probe("""
					[JsonPolymorphic(InferClosedTypePolymorphism = true)]
					public closed class Box<T> { }
					public sealed class StructBox<T> : Box<T> where T : struct, System.IComparable<T> { }
					public sealed class AnyBox<T> : Box<T> where T : new() { }
				""", "Box<int>"));

			Assert.That(IdsOf(diagnostics), Is.Empty);
			var derived = container.IncludedTypes.Single(static t => t.Name.StartsWith("Box", StringComparison.Ordinal)).DerivedTypes;
			Assert.That(derived.Select(static x => x.Discriminator), Is.EquivalentTo(new object?[] { "StructBox", "AnyBox" }));
		}

		[Test]
		public void Test_Inaccessible_Derived_Type_Is_Reported()
		{
			var (_, diagnostics) = Run(Probe("""
					[JsonPolymorphic(InferClosedTypePolymorphism = true)]
					public closed class Shape
					{
						private sealed class Hidden : Shape { }
						public sealed class Visible : Shape { }
					}
				""", "Shape"));

			Assert.That(IdsOf(diagnostics), Is.EqualTo(new[] { "CJSON0030" }));
			Assert.That(diagnostics.Single(static d => d.Id == "CJSON0030").GetMessage(), Does.Contain("not accessible"));
		}

		[Test]
		public void Test_Generated_Code_For_A_Closed_Hierarchy_Compiles()
		{
			var compilation = GeneratorProbeHarness.Compile(Probe("""
					[JsonPolymorphic(InferClosedTypePolymorphism = true)]
					public closed class CartEvent { public string? CartId { get; set; } }
					public sealed class CartCreated : CartEvent { public string? Owner { get; set; } }
					public sealed class ItemAdded : CartEvent { public int Quantity { get; set; } }
				""", "CartEvent"), PreviewParseOptions);

			var (output, diagnostics) = GeneratorProbeHarness.RunGenerator(compilation, PreviewParseOptions);
			Assert.That(diagnostics.Where(static d => d.Severity == DiagnosticSeverity.Error), Is.Empty);
			var errors = output.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error).ToList();
			foreach (var error in errors) { Log($"compiler: {error}"); }
			Assert.That(errors, Is.Empty);
			Assert.That(output.SyntaxTrees.Count(), Is.GreaterThan(1), "control: the generator emitted code");
		}

	}

}
