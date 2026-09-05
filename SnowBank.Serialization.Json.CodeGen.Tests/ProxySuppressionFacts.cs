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
	using System.Threading;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp;

	/// <summary>Pins that a type whose format the generator does not control gets no <c>ReadOnly</c>/<c>Writable</c> proxy, and that CJSON0025 says so</summary>
	/// <remarks>A proxy is a typed view of a shape the generator knows. For a type that answers a facet itself, or whose facet an author hooked, the generator does not know the shape, so it must not claim to offer a view of it.</remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class ProxySuppressionFacts : SimpleTest
	{

		/// <summary>Three registered types: one that packs itself, one whose write facet an author hooked, and one plain member-based type that keeps its proxies</summary>
		private const string ProbeSource = """
			namespace Probe
			{
				public sealed record SelfPacked : SnowBank.Data.Json.IJsonPackable
				{
					public string? Name { get; set; }

					SnowBank.Data.Json.JsonValue SnowBank.Data.Json.IJsonPackable.JsonPack(SnowBank.Data.Json.CrystalJsonSettings settings, SnowBank.Data.Json.ICrystalJsonTypeResolver resolver)
						=> SnowBank.Data.Json.JsonArray.Create(SnowBank.Data.Json.JsonString.Return(this.Name));
				}

				public sealed record Hooked
				{
					public string? Label { get; set; }
				}

				public sealed record Deep : SnowBank.Data.Json.IJsonPackable
				{
					public int Depth { get; set; }

					SnowBank.Data.Json.JsonValue SnowBank.Data.Json.IJsonPackable.JsonPack(SnowBank.Data.Json.CrystalJsonSettings settings, SnowBank.Data.Json.ICrystalJsonTypeResolver resolver)
						=> SnowBank.Data.Json.JsonNumber.Return(this.Depth);
				}

				public sealed record Plain
				{
					public string? Title { get; set; }

					public SelfPacked? Nested { get; set; }

					public Deep? Reached { get; set; }
				}

				[SnowBank.Data.Json.CrystalJsonConverter]
				[SnowBank.Data.CrystalSerializable(typeof(Probe.SelfPacked))]
				[SnowBank.Data.CrystalSerializable(typeof(Probe.Hooked))]
				[SnowBank.Data.CrystalSerializable(typeof(Probe.Plain))]
				public static partial class Host
				{
					public static partial class Hooked
					{
						public static void Serialize(SnowBank.Data.Json.CrystalJsonWriter writer, Probe.Hooked? instance) { }
					}
				}
			}
			""";

		private static (string[] Ids, string Generated, Diagnostic[] Diagnostics) Run()
		{
			var compilation = GeneratorProbeHarness.Compile(ProbeSource);
			var (output, diagnostics) = GeneratorProbeHarness.RunGenerator(compilation);
			foreach (var diagnostic in diagnostics) { Log($"generator: {diagnostic}"); }
			var generated = string.Concat(output.SyntaxTrees.Skip(1).Select(static t => t.ToString()));
			return (diagnostics.Select(static d => d.Id).ToArray(), generated, diagnostics.ToArray());
		}

		[Test]
		public void Test_A_Self_Packing_Type_Loses_Its_Proxies()
		{
			var (ids, generated, _) = Run();

			using (Assert.EnterMultipleScope())
			{
				Assert.That(ids, Does.Contain("CJSON0025"), "the suppression must be reported, not silent");
				Assert.That(generated, Does.Not.Contain("Host.SelfPacked.ReadOnly"), "no read-only proxy record for a type that packs itself");
				Assert.That(generated, Does.Not.Contain("Host.SelfPacked.Writable"), "no writable proxy record for a type that packs itself");
			}
		}

		[Test]
		public void Test_A_Hooked_Type_Loses_Its_Proxies()
		{
			var (_, generated, _) = Run();

			using (Assert.EnterMultipleScope())
			{
				Assert.That(generated, Does.Not.Contain("Host.Hooked.ReadOnly"), "no read-only proxy record for a hooked type");
				Assert.That(generated, Does.Not.Contain("Host.Hooked.Writable"), "no writable proxy record for a hooked type");
			}
		}

		[Test]
		public void Test_A_Plain_Type_In_The_Same_Container_Keeps_Its_Proxies()
		{
			var (_, generated, diagnostics) = Run();

			using (Assert.EnterMultipleScope())
			{
				Assert.That(generated, Does.Contain("Host.Plain.ReadOnly"), "suppression is per type, not per container");
				Assert.That(generated, Does.Contain("Host.Plain.Writable"), "suppression is per type, not per container");
				Assert.That(
					diagnostics.Where(static d => d.Id == "CJSON0025").Select(static d => d.GetMessage()).Where(static m => m.Contains("Plain")),
					Is.Empty,
					"a type that keeps its proxies must not be reported");
			}
		}

		[Test]
		public void Test_The_Entry_Points_Go_With_The_Records()
		{
			var (_, generated, _) = Run();

			// nothing may be left pointing at a proxy type that is no longer emitted
			using (Assert.EnterMultipleScope())
			{
				Assert.That(generated, Does.Not.Contain("Host.SelfPacked.ReadOnly"), "no ToReadOnly entry point, and no reference from anywhere else");
				Assert.That(generated, Does.Not.Contain("Host.SelfPacked.Writable"), "no ToMutable entry point, and no reference from anywhere else");
				Assert.That(generated, Does.Not.Contain("Host.Hooked.ReadOnly"), "no ToReadOnly entry point for a hooked type");
				Assert.That(generated, Does.Not.Contain("Host.Hooked.Writable"), "no ToMutable entry point for a hooked type");
			}
		}

		[Test]
		public void Test_A_Member_Of_A_Suppressed_Type_Does_Not_Reference_Its_Proxy()
		{
			// Plain.Nested is a SelfPacked, so Plain's own proxy would have exposed SelfPackedReadOnly
			var (_, generated, _) = Run();

			var compilation = GeneratorProbeHarness.Compile(ProbeSource);
			var (output, _) = GeneratorProbeHarness.RunGenerator(compilation);
			var errors = output.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error).ToArray();
			foreach (var error in errors) { Log($"compile: {error}"); }

			using (Assert.EnterMultipleScope())
			{
				Assert.That(errors, Is.Empty, "the generated code must still compile once a member's proxy type is gone");
				Assert.That(generated, Does.Contain("Host.Plain.ReadOnly"), "the containing type keeps its own proxy");
			}
		}

		[Test]
		public void Test_A_Transitively_Discovered_Type_Is_Named_Without_Its_Member_Nullability()
		{
			// Deep is reached only through the nullable member Plain.Reached, so the crawled symbol carries that
			// member's annotation; the annotation belongs to the member and has no business in the type's name
			var (_, _, diagnostics) = Run();
			var messages = diagnostics.Where(static d => d.Id == "CJSON0025").Select(static d => d.GetMessage()).ToArray();
			foreach (var message in messages) { Log($"message: {message}"); }

			using (Assert.EnterMultipleScope())
			{
				Assert.That(messages.Any(static m => m.Contains("'Probe.Deep'")), Is.True, "the transitively-discovered type must be reported");
				Assert.That(messages.Any(static m => m.Contains("Probe.Deep?")), Is.False, "and named without the nullable annotation of the member it was reached through");
			}
		}

		[Test]
		public void Test_The_Message_Explains_Why()
		{
			var (_, _, diagnostics) = Run();
			var message = diagnostics.First(static d => d.Id == "CJSON0025").GetMessage();
			Log($"message: {message}");

			using (Assert.EnterMultipleScope())
			{
				Assert.That(message, Does.Contain("proxy"), "the message must name what was suppressed");
				Assert.That(message, Does.Contain("shape"), "the message must say why: the generator does not know the shape");
			}
		}

		[Test]
		public void Test_No_Warning_Where_The_Container_Has_No_Proxy_Surface()
		{
			// below C# 11 the container never had proxies to lose, so a warning about suppressing them is noise
			var compilation = GeneratorProbeHarness.Compile(ProbeSource, GeneratorProbeHarness.FloorParseOptions);
			var (_, diagnostics) = GeneratorProbeHarness.RunGenerator(compilation, GeneratorProbeHarness.FloorParseOptions);
			foreach (var diagnostic in diagnostics) { Log($"generator: {diagnostic}"); }

			Assert.That(
				diagnostics.Select(static d => d.Id),
				Does.Not.Contain("CJSON0025"),
				"a container with no proxy surface must not warn about suppressing one");
		}

		/// <summary>Diagnostics actually reported to the user for CJSON0025</summary>
		/// <remarks>A <c>#pragma</c> marks the diagnostic <see cref="Diagnostic.IsSuppressed"/> and the compiler drops it; a severity of <c>none</c> removes it from the array outright. One predicate covers both shapes.</remarks>
		private static Diagnostic[] Reported(IEnumerable<Diagnostic> diagnostics)
			=> diagnostics.Where(static d => d.Id == "CJSON0025" && !d.IsSuppressed).ToArray();

		[Test]
		public void Test_CJSON0025_Is_Reported_On_The_Probe_Tree()
		{
			var compilation = GeneratorProbeHarness.Compile(ProbeSource);
			var (_, diagnostics) = GeneratorProbeHarness.RunGenerator(compilation);
			var reported = Reported(diagnostics);

			using (Assert.EnterMultipleScope())
			{
				Assert.That(reported, Is.Not.Empty, "the unsuppressed control must still warn");
				Assert.That(reported.Select(static d => d.Location.SourceTree), Is.All.SameAs(compilation.SyntaxTrees.Single()), "the location must be bound to the probe's tree, which is what #pragma and .editorconfig are applied through");
				Assert.That(reported.Select(static d => d.Location.Kind), Is.All.EqualTo(LocationKind.SourceFile));
			}
		}

		[Test]
		public void Test_CJSON0025_Honors_Pragma_Warning_Disable()
		{
			var compilation = GeneratorProbeHarness.Compile("#pragma warning disable CJSON0025\n" + ProbeSource);
			var (_, diagnostics) = GeneratorProbeHarness.RunGenerator(compilation);
			foreach (var diagnostic in diagnostics) { Log($"generator: {diagnostic} (suppressed: {diagnostic.IsSuppressed})"); }

			Assert.That(Reported(diagnostics), Is.Empty, "a #pragma at the top of the file covers every declaration it reports on");
		}

		[Test]
		public void Test_CJSON0025_Honors_EditorConfig_Severity_None()
		{
			var root = TestContext.CurrentContext.TestDirectory;
			var config = AnalyzerConfig.Parse("[*.cs]\ndotnet_diagnostic.CJSON0025.severity = none\n", Path.Combine(root, ".editorconfig"));
			var treeOptions = AnalyzerConfigSet.Create(new[] { config }).GetOptionsForSourcePath(Path.Combine(root, "Probe.cs")).TreeOptions;

			Compilation compilation = GeneratorProbeHarness.Compile(ProbeSource);
			var tree = compilation.SyntaxTrees.Single();
			compilation = compilation.WithOptions(compilation.Options.WithSyntaxTreeOptionsProvider(new SingleTreeOptions(tree, treeOptions)));

			var (_, diagnostics) = GeneratorProbeHarness.RunGenerator((CSharpCompilation) compilation);

			Assert.That(Reported(diagnostics), Is.Empty, "dotnet_diagnostic.<id>.severity = none must reach a generator diagnostic");
		}

		/// <summary>The per-tree options the compiler derives from an .editorconfig, for one tree</summary>
		private sealed class SingleTreeOptions : SyntaxTreeOptionsProvider
		{

			public SingleTreeOptions(SyntaxTree tree, ImmutableDictionary<string, ReportDiagnostic> options)
			{
				this.Tree = tree;
				this.Options = options;
			}

			private SyntaxTree Tree { get; }

			private ImmutableDictionary<string, ReportDiagnostic> Options { get; }

			public override GeneratedKind IsGenerated(SyntaxTree tree, CancellationToken cancellationToken) => GeneratedKind.Unknown;

			public override bool TryGetDiagnosticValue(SyntaxTree tree, string diagnosticId, CancellationToken cancellationToken, out ReportDiagnostic severity)
			{
				if (ReferenceEquals(tree, this.Tree) && this.Options.TryGetValue(diagnosticId, out severity)) return true;
				severity = ReportDiagnostic.Default;
				return false;
			}

			public override bool TryGetGlobalDiagnosticValue(string diagnosticId, CancellationToken cancellationToken, out ReportDiagnostic severity)
			{
				severity = ReportDiagnostic.Default;
				return false;
			}

		}

		[Test]
		public void Test_CJSON0025_Honors_NoWarn()
		{
			Compilation compilation = GeneratorProbeHarness.Compile(ProbeSource);
			compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions([ new KeyValuePair<string, ReportDiagnostic>("CJSON0025", ReportDiagnostic.Suppress) ]));

			var (_, diagnostics) = GeneratorProbeHarness.RunGenerator((CSharpCompilation) compilation);

			Assert.That(Reported(diagnostics), Is.Empty, "<NoWarn> is a compilation option and needs no tree; it worked before the fix and must keep working");
		}

	}

}
