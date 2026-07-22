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

		private const string CrystalJsonConverterAttributeFullName = KnownTypeSymbols.CrystalJsonNamespace + ".CrystalJsonConverterAttribute";

		private const string CrystalJsonSerializableAttributeFullName = KnownTypeSymbols.CrystalJsonNamespace + ".CrystalJsonSerializableAttribute";

		private const string CrystalJsonSelfSerializableAttributeFullName = KnownTypeSymbols.CrystalJsonNamespace + ".CrystalJsonSelfSerializableAttribute";

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

			// find all possible converters (partial classes with a [CrystalJsonConverter] attribute)
			var converterTypes = context.SyntaxProvider
				.ForAttributeWithMetadataName(
					CrystalJsonConverterAttributeFullName,
					(node, _) => node is ClassDeclarationSyntax,
					(ctx, _) => (ContextClass: (ClassDeclarationSyntax) ctx.TargetNode, ctx.SemanticModel, ctx.Attributes)
				)
				.Combine(knownTypeSymbols)
				.Select(static (tuple, ct) =>
				{
					var parser = new Parser(tuple.Right);
					var contextGenerationSpec = parser.ParseContainerMetadata(tuple.Left.ContextClass, tuple.Left.SemanticModel, tuple.Left.Attributes, ct);
					var diagnostics = parser.Diagnostics.ToImmutableEquatableArray();
					return (Metadata: contextGenerationSpec, Diagnostics: diagnostics);
				})
				.WithTrackingName("CrystalJsonSpec")
				;

			context.RegisterSourceOutput(converterTypes, EmitSourceCode);

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

			context.RegisterSourceOutput(selfSerializableTypes, EmitSourceCode);
		}

		/// <summary>Filters a type declaration, keeping only self-serializable types (one of their attributes carries the meta-marker)</summary>
		private static (TypeDeclarationSyntax? TypeDeclaration, SemanticModel? SemanticModel) GetSelfSerializableCandidate(GeneratorSyntaxContext ctx, CancellationToken ct)
		{
			var typeDeclaration = (TypeDeclarationSyntax) ctx.Node;

			if (ctx.SemanticModel.GetDeclaredSymbol(typeDeclaration, ct) is not INamedTypeSymbol symbol)
			{
				return default;
			}

			bool found = false;
			foreach (var attribute in symbol.GetAttributes())
			{
				var attributeClass = attribute.AttributeClass;
				if (attributeClass is null) continue;

				foreach (var marker in attributeClass.GetAttributes())
				{
					if (marker.AttributeClass?.ToDisplayString() == CrystalJsonSelfSerializableAttributeFullName)
					{
						found = true;
						break;
					}
				}
				if (found) break;
			}
			if (!found)
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

		private void EmitSourceCode(SourceProductionContext ctx, (CrystalJsonContainerMetadata? Metadata, ImmutableEquatableArray<DiagnosticInfo> Diagnostics) args)
		{
			try
			{
				foreach (DiagnosticInfo diagnostic in args.Diagnostics)
				{
					ctx.ReportDiagnostic(diagnostic.CreateDiagnostic());
				}

				if (args.Metadata is not null)
				{
					var emitter = new Emitter(ctx, args.Metadata);
					emitter.GenerateCode();
				}
			}
			catch (Exception ex)
			{
				Kenobi("CRASH: " + ex.ToString());
			}
		}

	}

}
