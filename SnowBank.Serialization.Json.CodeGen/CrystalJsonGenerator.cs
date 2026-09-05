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

//#define LAUNCH_DEBUGGER

//#define FULL_DEBUG

namespace SnowBank.Serialization.Json.CodeGen
{
	using System;
	using System.Diagnostics;
	using System.Threading;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp.Syntax;

	[Generator(LanguageNames.CSharp)]
	public partial class CrystalJsonSourceGenerator : IIncrementalGenerator
	{

		/// <summary>Message for <c>CJSON0015</c>, kept identical to <c>CrystalJson.Errors.CallbackStreamingContextNotSupported</c></summary>
		/// <remarks>An analyzer cannot reference SnowBank.Core, so the string exists in both assemblies. It is public here so a test can assert the two copies are equal: a build error whose text does not match the documented migration recipe is one nobody can grep for.</remarks>
		public const string CallbackStreamingContextNotSupportedMessage = "Remove the StreamingContext parameter from serialization callback '{0}', or replace it with JsonValue, JsonObject or JsonArray. The legacy DataContractJsonSerializer callback signature is not supported.";

		/// <summary>Message for <c>CJSON0015</c> on any other unusable callback signature, kept identical to <c>CrystalJson.Errors.CallbackSignatureNotSupported</c></summary>
		public const string CallbackSignatureNotSupportedMessage = "Serialization callback '{0}' must be parameterless, or take a single JsonValue, JsonObject or JsonArray parameter.";

		/// <summary>Message for <c>CJSON0017</c>, kept identical to <c>CrystalJson.Errors.BooleanLiteralTypeNotSupported</c></summary>
		public const string BooleanLiteralTypeNotSupportedMessage = "The [JsonBooleanLiterals] argument '{0}' is of type {1}, which has no JSON representation: use a string, a bool, or a numeric value.";

		private const string CrystalJsonConverterAttributeFullName = KnownTypeSymbols.CrystalJsonNamespace + ".CrystalJsonConverterAttribute";

		private const string CrystalJsonSerializableAttributeFullName = KnownTypeSymbols.CrystalJsonNamespace + ".CrystalJsonSerializableAttribute";

		/// <summary>The container markers, in the order that decides which one owns a container that (wrongly) carries several</summary>
		/// <remarks>
		/// <para><see cref="ForAttributeWithMetadataName"/> matches an EXACT metadata name and never a derived attribute, so each marker needs its own registration: the fact that the two aliases derive from <c>CrystalConverterAttribute</c> is documentation for the reader, not something the pipeline can see.</para>
		/// <para>A container is meant to carry exactly one of them. When it carries several, all of its pipelines match, and each one asks <see cref="GetOwningContainerMarker"/> who owns it: only the owner parses (so the container is emitted once), and the owner is the one that reports <c>CRYS0003</c>.</para>
		/// </remarks>
		private static readonly string[] ContainerMarkerAttributeFullNames =
		[
			KnownTypeSymbols.CrystalConverterAttributeFullName,
			CrystalJsonConverterAttributeFullName,
			KnownTypeSymbols.CrystalXmlConverterAttributeFullName,
		];

		private const string CrystalJsonSelfSerializableAttributeFullName = KnownTypeSymbols.CrystalJsonNamespace + ".CrystalJsonSelfSerializableAttribute";

		/// <summary>Name of the single nested scope that hosts ALL the code generated for a self-serializable type (ex: <c>Widget.Json.ReadOnly</c>)</summary>
		/// <remarks>This is the only member name the generator reserves inside the entity; a future generator for another format would claim a sibling scope (ex: <c>Widget.Cbor</c>).</remarks>
		internal const string SelfScopeName = "Json";

#if FULL_DEBUG
#pragma warning disable RS1035
		private static readonly string ProcessIdentifier = "[" + Process.GetCurrentProcess().ProcessName + ":" + Process.GetCurrentProcess().Id.ToString() + "]";
#pragma warning restore RS1035
#endif

		[Conditional("FULL_DEBUG")]
		public static void Kenobi(string msg)
		{
#if FULL_DEBUG
			System.Diagnostics.Debug.WriteLine(msg);
#pragma warning disable RS1035
			Console.WriteLine(msg);
			for (int i = 0; i < 4; i++)
			{
				try
				{
					System.IO.File.AppendAllText(@"c:\temp\analyzer.log", $"{ProcessIdentifier} [{DateTime.Now:O}] {msg}\r\n");
					break;
				}
				catch (IOException)
				{
					Thread.Sleep(15);
				}
			}
#pragma warning restore RS1035
#endif
		}

		public void Initialize(IncrementalGeneratorInitializationContext context)
		{
#if LAUNCH_DEBUGGER
            System.Diagnostics.Debugger.Launch();
#endif

			Kenobi("------- INITIALIZE -------------");

			var knownTypeSymbols = context
				.CompilationProvider
				.Select((compilation, _) => new KnownTypeSymbols(compilation));

			// find all possible containers (partial classes with a container marker: the neutral one, or one of the two mono-format aliases)
			foreach (var markerAttributeFullName in ContainerMarkerAttributeFullNames)
			{
				var marker = markerAttributeFullName;
				var converterTypes = context.SyntaxProvider
					.ForAttributeWithMetadataName(
						marker,
						(node, _) => node is ClassDeclarationSyntax,
						// a container carrying several markers matches several pipelines: only the owning one parses it, and
						// the answer is computed here so that no symbol travels inside the cached value of the pipeline
						(ctx, _) => (ContextClass: (ClassDeclarationSyntax) ctx.TargetNode, ctx.SemanticModel, IsOwner: ctx.TargetSymbol is INamedTypeSymbol symbol && GetOwningContainerMarker(symbol) == marker)
					)
					.Where(static candidate => candidate.IsOwner)
					.Combine(knownTypeSymbols)
					.Select((tuple, ct) =>
					{
						var parser = new Parser(tuple.Right);
						var contextGenerationSpec = parser.ParseContainerMetadata(tuple.Left.ContextClass, tuple.Left.SemanticModel, marker, ct);
						var diagnostics = parser.Diagnostics.ToImmutableEquatableArray();
						return (Metadata: contextGenerationSpec, Diagnostics: diagnostics);
					})
					.WithTrackingName("CrystalJsonSpec")
					;

				RegisterOutputs(context, converterTypes);
			}

			// find all self-serializable types: partial types decorated with an attribute whose class carries the
			// [CrystalJsonSelfSerializable] meta-marker (the marker cannot be matched by name here, because the
			// decorating attribute belongs to the application or to another layer, not to this generator)
			var selfSerializableTypes = context.SyntaxProvider
				.CreateSyntaxProvider(
					static (node, _) => node is TypeDeclarationSyntax { AttributeLists.Count: > 0 },
					static (ctx, ct) => GetSelfSerializableCandidate(ctx, ct)
				)
				.Where(static candidate => candidate.TypeDeclaration is not null)
				.Combine(knownTypeSymbols)
				.Select(static (tuple, ct) =>
				{
					var parser = new Parser(tuple.Right);
					var containerMetadata = parser.ParseSelfSerializableMetadata(tuple.Left.TypeDeclaration!, tuple.Left.SemanticModel!, ct);
					var diagnostics = parser.Diagnostics.ToImmutableEquatableArray();
					return (Metadata: containerMetadata, Diagnostics: diagnostics);
				})
				.WithTrackingName("CrystalJsonSelfSpec")
				;

			RegisterOutputs(context, selfSerializableTypes);

			// find the types that ask for XML output without hosting any generated serializer: neither pipeline above
			// ever sees them, so the attribute would be silently inert (CXML0002)
			var orphanXmlOutputTypes = context.SyntaxProvider
				.ForAttributeWithMetadataName(
					KnownTypeSymbols.CrystalXmlOutputAttributeFullName,
					static (node, _) => node is TypeDeclarationSyntax,
					// the answer is computed here so that no symbol travels inside the cached value of the pipeline
					static (ctx, _) => (TypeDeclaration: (TypeDeclarationSyntax) ctx.TargetNode,
						IsOrphan: ctx.TargetSymbol is INamedTypeSymbol symbol && !HostsGeneratedSerializer(symbol),
						DisplayName: ctx.TargetSymbol.ToDisplayString())
				)
				.Where(static candidate => candidate.IsOrphan)
				.Combine(knownTypeSymbols)
				.Select(static (tuple, _) =>
				{
					var parser = new Parser(tuple.Right);
					parser.ReportOrphanXmlOutput(tuple.Left.TypeDeclaration, tuple.Left.DisplayName);
					return (Metadata: (CrystalJsonContainerMetadata?) null, Diagnostics: parser.Diagnostics.ToImmutableEquatableArray());
				})
				.WithTrackingName("CrystalXmlOrphanSpec")
				;

			RegisterOutputs(context, orphanXmlOutputTypes);
		}

		/// <summary>Registers the two outputs of a container pipeline: the emitted code, cached on the metadata alone, and the diagnostics, bound to the trees of the current compilation</summary>
		private static void RegisterOutputs(IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<(CrystalJsonContainerMetadata? Metadata, ImmutableEquatableArray<DiagnosticInfo> Diagnostics)> containers)
		{
			// the code depends on the metadata only, so it stays cached while the rest of the compilation changes
			context.RegisterSourceOutput(
				containers.Select(static (c, _) => c.Metadata).Where(static m => m is not null),
				static (ctx, metadata) => EmitSourceCode(ctx, metadata!));

			// the compiler applies #pragma and .editorconfig through the tree of a location, so each diagnostic is
			// bound to the tree of the compilation being reported on; this node re-runs on every compilation, and
			// only for the containers that have something to report
			context.RegisterSourceOutput(
				containers.Select(static (c, _) => c.Diagnostics).Where(static d => d.Count > 0).Combine(context.CompilationProvider),
				static (ctx, pair) =>
				{
					foreach (var diagnostic in pair.Left) ctx.ReportDiagnostic(diagnostic.CreateDiagnostic(pair.Right));
				});
		}

		/// <summary>Returns the container marker that owns this type, or <see langword="null"/> when it carries none</summary>
		/// <remarks>A type carrying several markers is rejected (<c>CRYS0003</c>), but it must be rejected ONCE: the first marker of <see cref="ContainerMarkerAttributeFullNames"/> that it carries is the one that parses it and reports.</remarks>
		private static string? GetOwningContainerMarker(INamedTypeSymbol symbol)
		{
			string? owner = null;
			int best = int.MaxValue;

			foreach (var attribute in symbol.GetAttributes())
			{
				var name = attribute.AttributeClass?.ToDisplayString();
				if (name is null) continue;

				for (int i = 0; i < ContainerMarkerAttributeFullNames.Length; i++)
				{
					if (ContainerMarkerAttributeFullNames[i] == name && i < best)
					{
						best = i;
						owner = name;
					}
				}
			}

			return owner;
		}

		/// <summary>Tests whether a type hosts source-generated serialization code: a container (any marker), or a self-serializable type</summary>
		private static bool HostsGeneratedSerializer(INamedTypeSymbol symbol)
			=> GetOwningContainerMarker(symbol) is not null || IsSelfSerializable(symbol);

		/// <summary>Tests whether a type is self-serializable: one of its attributes carries the <c>[CrystalJsonSelfSerializable]</c> meta-marker</summary>
		/// <remarks>The decorating attribute belongs to the application or to another layer, not to this generator, so it cannot be matched by name: only the meta-marker it carries can.</remarks>
		private static bool IsSelfSerializable(INamedTypeSymbol symbol)
		{
			foreach (var attribute in symbol.GetAttributes())
			{
				var attributeClass = attribute.AttributeClass;
				if (attributeClass is null) continue;

				foreach (var marker in attributeClass.GetAttributes())
				{
					if (marker.AttributeClass?.ToDisplayString() == CrystalJsonSelfSerializableAttributeFullName)
					{
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>Filters a type declaration, keeping only self-serializable types (one of their attributes carries the meta-marker)</summary>
		private static (TypeDeclarationSyntax? TypeDeclaration, SemanticModel? SemanticModel) GetSelfSerializableCandidate(GeneratorSyntaxContext ctx, CancellationToken ct)
		{
			var typeDeclaration = (TypeDeclarationSyntax) ctx.Node;

			if (ctx.SemanticModel.GetDeclaredSymbol(typeDeclaration, ct) is not INamedTypeSymbol symbol)
			{
				return default;
			}

			if (!IsSelfSerializable(symbol))
			{
				return default;
			}

			// a partial type with attributes on several parts matches once per part: only generate for the first
			// attributed declaration, so that the source is emitted exactly once
			foreach (var syntaxRef in symbol.DeclaringSyntaxReferences)
			{
				if (syntaxRef.GetSyntax(ct) is TypeDeclarationSyntax { AttributeLists.Count: > 0 } candidate)
				{
					return ReferenceEquals(candidate, typeDeclaration) ? (typeDeclaration, ctx.SemanticModel) : default;
				}
			}

			return default;
		}

		private static void EmitSourceCode(SourceProductionContext ctx, CrystalJsonContainerMetadata metadata)
		{
			try
			{
				var emitter = new Emitter(ctx, metadata);
				emitter.GenerateCode();
			}
			catch (Exception ex)
			{
				Kenobi("CRASH: " + ex.ToString());
			}
		}

	}

}
