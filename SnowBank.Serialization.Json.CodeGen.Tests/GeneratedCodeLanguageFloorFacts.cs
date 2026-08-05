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
	public sealed class GeneratedCodeLanguageFloorFacts : SimpleTest
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
				[SnowBank.Data.CrystalSerializable(typeof(ProbeUser))]
				[SnowBank.Data.CrystalSerializable(typeof(ProbeAnimal))]
				public static partial class ProbeConverters
				{
				}

				// the same graph, published as XML too: the emitted XML members must be just as clean, including the ones
				// with NO XML projection at all (a JsonObject, a DateTimeOffset), which emit a call that fails at write time
				[SnowBank.Data.CrystalConverter]
				[SnowBank.Data.Json.CrystalJsonOutput]
				[SnowBank.Data.Xml.CrystalXmlOutput]
				[SnowBank.Data.CrystalSerializable(typeof(ProbeUser))]
				[SnowBank.Data.CrystalSerializable(typeof(ProbeItem))]
				[SnowBank.Data.CrystalSerializable(typeof(ProbeAnimal))]
				public static partial class ProbeXmlConverters
				{
				}

				// a container that resolves to the DataContract XML wire, whose emission does not exist yet: it must degrade
				// to a clean JSON-only container (no half-written XML surface), which is what compiling it here proves
				[SnowBank.Data.CrystalConverter]
				[SnowBank.Data.Json.CrystalJsonOutput(SnowBank.Data.Json.CrystalJsonSerializerDefaults.DataContractCompat)]
				[SnowBank.Data.Xml.CrystalXmlOutput]
				[SnowBank.Data.CrystalSerializable(typeof(ProbeItem))]
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
				[SnowBank.Data.CrystalSerializable(typeof(LegacyProbeDto))]
				public static partial class LegacyProbeConverters
				{
				}

				// and the oblivious DTO published as XML: every null test the XML members emit sits on an oblivious member type
				[SnowBank.Data.CrystalConverter]
				[SnowBank.Data.Json.CrystalJsonOutput]
				[SnowBank.Data.Xml.CrystalXmlOutput]
				[SnowBank.Data.CrystalSerializable(typeof(LegacyProbeDto))]
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

		/// <summary>A modern compilation sees the proxy interfaces and sits at or above C# 11, so it must not get the CJSON0020 proxy-surface-loss nudge</summary>
		[Test]
		public void Test_Proxy_Surface_Loss_Is_Not_Reported_On_A_Modern_Compilation()
		{
			var compilation = GeneratorProbeHarness.Compile(ProbeSource);
			var (_, generatorDiagnostics) = GeneratorProbeHarness.RunGenerator(compilation);

			Assert.That(
				generatorDiagnostics.Select(static d => d.Id),
				Does.Not.Contain("CJSON0020"),
				"a modern compilation supports the JSON proxy surface, so its loss must not be reported");
		}

		/// <summary>A legacy consumer's DTO graph, written in plain C# 7.3 style: no records, no <c>init</c>, no <c>required</c>, no nullable annotations, no target-typed <c>new</c></summary>
		/// <remarks>This is what an old-style .NET Framework project being migrated looks like. It publishes the same graph as JSON and as XML, so the facts below cover BOTH emitters.</remarks>
		private const string LegacyProbeSource = """
			namespace Probe.Legacy
			{

				public enum LegacyKind
				{
					None = 0,
					Alpha = 1,
				}

				public sealed class LegacyItem
				{
					public string Id { get; set; }
					public int Level { get; set; }
					public bool? Disabled { get; set; }
				}

				[System.Runtime.Serialization.DataContract]
				public sealed class LegacyAccount
				{
					[System.Runtime.Serialization.DataMember(Order = 1)] public string Id { get; set; }
					[System.Runtime.Serialization.DataMember(Order = 2)] public LegacyKind Kind { get; set; }
					[System.Runtime.Serialization.DataMember(Order = 3)] public System.DateTime Created { get; set; }
					[System.Runtime.Serialization.DataMember(Order = 4)] public decimal Balance { get; set; }
					[System.Runtime.Serialization.DataMember(Order = 5)] public System.Guid Ticket { get; set; }
					[System.Runtime.Serialization.DataMember(Order = 6)] public byte[] Blob { get; set; }
					[System.Runtime.Serialization.DataMember(Order = 7)] public System.Collections.Generic.List<LegacyItem> Items { get; set; }
					[System.Runtime.Serialization.DataMember(Order = 8)] public System.Collections.Generic.Dictionary<string, LegacyItem> Index { get; set; }
					[SnowBank.Data.Xml.XmlProperty(Attribute = true)] [System.Runtime.Serialization.DataMember(Order = 9)] public int Revision { get; set; }
					// not-null, no default, interface-typed collection member: the setter must fall back to an empty
					// instance when handed a null value, and that empty instance must be a valid expression for an
					// INTERFACE type (Array.Empty<T>()), not "new IList<string>()" (CS0144)
					[System.Runtime.Serialization.DataMember(Order = 10)] public System.Collections.Generic.IList<string> Names { get; set; }
				}

				[SnowBank.Data.CrystalConverter]
				[SnowBank.Data.Json.CrystalJsonOutput]
				[SnowBank.Data.Xml.CrystalXmlOutput]
				[SnowBank.Data.CrystalSerializable(typeof(LegacyAccount))]
				[SnowBank.Data.CrystalSerializable(typeof(LegacyItem))]
				public static partial class LegacyXmlConverters
				{
				}

			}
			""";

		/// <summary>The emitted code must compile in a consumer sitting on the generator's language floor (C# 9), XML members included</summary>
		/// <remarks>
		/// <para>The <c>netstandard2.0</c>/<c>net472</c> "lite" path is in scope for CrystalXml exactly as it is for CrystalJson (see this repo's CLAUDE.md), and its whole point is a legacy application that has not been modernized yet. Such an application raises <c>LangVersion</c> to the generator's floor and no further, so a modern-only construct in the EMITTED source (UTF-8 string literals, collection expressions, raw strings) would make XML output cost strictly more language than JSON output does, for no functional reason.</para>
		/// <para>The floor itself is C# 9 and is enforced by the parser (<c>SYSLIB1221</c>, pinned by the fact below); this one pins that XML emission does not raise it.</para>
		/// <para>This fact fails on errors only: warnings are covered by the modern language-floor fact above, and an older-language compilation legitimately reports informational noise the modern one does not.</para>
		/// </remarks>
		[Test]
		public void Test_Generated_Code_Compiles_At_The_Supported_Language_Floor()
		{
			var compilation = GeneratorProbeHarness.Compile(LegacyProbeSource, GeneratorProbeHarness.FloorParseOptions);

			// the probe source itself must be valid at the floor BEFORE the generator runs, so any error below is the generator's
			Assert.That(
				compilation.GetDiagnostics().Where(static d => d.Severity == DiagnosticSeverity.Error),
				Is.Empty,
				"the legacy probe source must be valid at the supported language floor on its own");

			var (outputCompilation, generatorDiagnostics) = GeneratorProbeHarness.RunGenerator(compilation, GeneratorProbeHarness.FloorParseOptions);

			foreach (var diagnostic in generatorDiagnostics)
			{
				Log($"generator: {diagnostic}");
			}
			Assert.That(generatorDiagnostics.Where(static d => d.Severity >= DiagnosticSeverity.Warning), Is.Empty, "the generator must not report diagnostics for the legacy probe types");

			var generatedTrees = outputCompilation.SyntaxTrees.Skip(1).ToList();
			Assert.That(generatedTrees, Is.Not.Empty, "the generator must emit sources for the legacy probe container");
			Assert.That(generatedTrees.Any(static tree => tree.ToString().Contains("WriteXml")), Is.True, "the emitted code must include the container's XML members");

			var errors = outputCompilation.GetDiagnostics()
				.Where(static d => d.Severity == DiagnosticSeverity.Error)
				.ToList();
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
			Assert.That(errors, Is.Empty, "generated code must compile in a consumer project sitting on the supported language floor");
		}

		/// <summary>A floor-level (C# 9) compilation cannot host the proxy surface (it needs C# 11): the container must get the CJSON0020 informational nudge, exactly once</summary>
		[Test]
		public void Test_Proxy_Surface_Loss_Is_Reported_Once_Per_Container_Below_The_Proxy_Floor()
		{
			var compilation = GeneratorProbeHarness.Compile(LegacyProbeSource, GeneratorProbeHarness.FloorParseOptions);
			var (_, generatorDiagnostics) = GeneratorProbeHarness.RunGenerator(compilation, GeneratorProbeHarness.FloorParseOptions);

			var proxyDiagnostics = generatorDiagnostics.Where(static d => d.Id == "CJSON0020").ToList();
			foreach (var diagnostic in proxyDiagnostics)
			{
				Log(diagnostic.ToString());
			}

			Assert.That(proxyDiagnostics, Has.Count.EqualTo(1), "the legacy probe declares a single container, so the nudge must fire exactly once");
			Assert.That(proxyDiagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Info), "the nudge is informational: the container still gets its converter, TypeMapper and XML output");
		}

		/// <summary>An XML-only container, below the C# 11 proxy floor</summary>
		/// <remarks>Same graph as <see cref="LegacyProbeSource"/>, but the container only ever asks for the XML wire, via the <c>[CrystalXmlConverter]</c> alias.</remarks>
		private const string LegacyXmlOnlyProbeSource = """
			namespace Probe.LegacyXmlOnly
			{

				public enum LegacyKind
				{
					None = 0,
					Alpha = 1,
				}

				public sealed class LegacyItem
				{
					public string Id { get; set; }
					public int Level { get; set; }
					public bool? Disabled { get; set; }
				}

				[System.Runtime.Serialization.DataContract]
				public sealed class LegacyAccount
				{
					[System.Runtime.Serialization.DataMember(Order = 1)] public string Id { get; set; }
					[System.Runtime.Serialization.DataMember(Order = 2)] public LegacyKind Kind { get; set; }
					[System.Runtime.Serialization.DataMember(Order = 3)] public System.Collections.Generic.List<LegacyItem> Items { get; set; }
				}

				[SnowBank.Data.Xml.CrystalXmlConverter]
				[SnowBank.Data.CrystalSerializable(typeof(LegacyAccount))]
				[SnowBank.Data.CrystalSerializable(typeof(LegacyItem))]
				public static partial class LegacyXmlOnlyConverters
				{
				}

			}
			""";

		/// <summary>An XML-only container never had a proxy surface to lose, so the CJSON0020 nudge must stay silent for it even below the C# 11 proxy floor</summary>
		/// <remarks>Pins the guard in the parser that only calls into the CJSON0020 reporter when the container generates JSON (<c>formats.GeneratesJson</c>): a regression that re-enables the call unconditionally would make an XML-only container get a nudge about a surface (<c>ToReadOnly</c>/<c>ToMutable</c>, the proxy types) that is meaningless for it, since it never generates JSON at all.</remarks>
		[Test]
		public void Test_Proxy_Surface_Loss_Is_Not_Reported_For_An_Xml_Only_Container_Below_The_Proxy_Floor()
		{
			var compilation = GeneratorProbeHarness.Compile(LegacyXmlOnlyProbeSource, GeneratorProbeHarness.FloorParseOptions);
			var (_, generatorDiagnostics) = GeneratorProbeHarness.RunGenerator(compilation, GeneratorProbeHarness.FloorParseOptions);

			foreach (var diagnostic in generatorDiagnostics)
			{
				Log(diagnostic.ToString());
			}

			Assert.That(
				generatorDiagnostics.Select(static d => d.Id),
				Does.Not.Contain("CJSON0020"),
				"an XML-only container never had a JSON proxy surface, so its absence below the proxy floor must not be reported");
		}

		/// <summary>Below the floor, the generator refuses with <c>SYSLIB1221</c> instead of emitting code the consumer cannot compile</summary>
		/// <remarks>The floor is inherited from the System.Text.Json generator (same diagnostic id, same message shape) and applies to the container as a whole: enabling XML output on a container does not change it, and a consumer below the floor gets no JSON serializer either.</remarks>
		[Test]
		public void Test_Generator_Refuses_Below_The_Supported_Language_Floor()
		{
			var compilation = GeneratorProbeHarness.Compile(LegacyProbeSource, GeneratorProbeHarness.BelowFloorParseOptions);

			var (outputCompilation, generatorDiagnostics) = GeneratorProbeHarness.RunGenerator(compilation, GeneratorProbeHarness.BelowFloorParseOptions);

			foreach (var diagnostic in generatorDiagnostics)
			{
				Log($"generator: {diagnostic}");
			}

			Assert.That(
				generatorDiagnostics.Select(static d => d.Id),
				Does.Contain("SYSLIB1221"),
				"a consumer below the language floor must be told so, by the same diagnostic System.Text.Json uses");

			// and the refusal must be total: no half-emitted container that would then fail to compile
			Assert.That(
				outputCompilation.SyntaxTrees.Skip(1).Where(static tree => tree.ToString().Contains("WriteXml")),
				Is.Empty,
				"a refused container must not emit XML members");
		}

		/// <summary>Pins the generator-side UTF-8 encoder (<see cref="SnowBank.SourceAnalysis.CSharpCodeBuilder.Utf8Constant"/>) against a non-ASCII name</summary>
		/// <remarks>Backs the XML name table (<c>[XmlProperty]</c> / <c>[DataMember]</c> names): the byte array it spells out must be byte-for-byte what <see cref="System.Text.Encoding.UTF8"/> itself produces, since the wire cannot depend on how the generator encoded the literal.</remarks>
		[Test]
		public void Test_Utf8Constant_Encodes_Non_Ascii_Names()
		{
			// "Clé" forces a multi-byte UTF-8 sequence for the accented letter
			const string name = "Clé";
			var expectedBytes = System.Text.Encoding.UTF8.GetBytes(name);
			var expectedExpr = "new byte[] { " + string.Join(", ", expectedBytes.Select(static b => $"0x{b:X2}")) + " }";

			var expr = SnowBank.SourceAnalysis.CSharpCodeBuilder.Utf8Constant(name);

			Assert.That(expr, Is.EqualTo(expectedExpr));
		}

	}

}
