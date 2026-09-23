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

namespace SnowBank.Analyzers
{
	using System.Collections.Generic;
	using System.Collections.Immutable;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp;
	using Microsoft.CodeAnalysis.CSharp.Syntax;
	using Microsoft.CodeAnalysis.Diagnostics;

	/// <summary>Reports a Slice factory that version 7 removed, next to the compiler error, with its replacement (SBK0100).</summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class RemovedSliceApiAnalyzer : DiagnosticAnalyzer
	{

		/// <summary>Property of the diagnostic: the new method name.</summary>
		public const string ReplacementProperty = "replacement";

		private static readonly Dictionary<string, (string Advice, string Fix)> Members = new()
		{
			["Create"] = ("Use Slice.Copy(bytes) or Slice.FromBytes(span) instead.", "Copy"),
			["FromSpan"] = ("Use Slice.FromBytes(span) instead.", "FromBytes"),
			["FromAscii"] = ("Use Slice.FromStringAscii(text) instead.", "FromStringAscii"),
		};

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(SbkDescriptors.RemovedSliceApi);

		public override void Initialize(AnalysisContext context)
		{
			context.EnableConcurrentExecution();
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.RegisterCompilationStartAction(start =>
			{
				var slice = start.Compilation.GetTypeByMetadataName("System.Slice");
				if (slice is null) return;

				start.RegisterSyntaxNodeAction(ctx =>
				{
					var node = (MemberAccessExpressionSyntax) ctx.Node;
					string name = node.Name.Identifier.ValueText;
					if (!Members.TryGetValue(name, out var entry)) return;
					var model = ctx.SemanticModel;
					var info = model.GetSymbolInfo(node, ctx.CancellationToken);
					if (info.Symbol is not null) return;
					// the receiver is the type name Slice
					if (!SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(node.Expression, ctx.CancellationToken).Symbol, slice)) return;

					string? fix = entry.Fix;
					if (name == "Create")
					{
						// Slice.Create<TState>(int, TState, SpanAction) still exists: the removed overloads took a single argument, an array or a size
						if (info.CandidateReason != CandidateReason.OverloadResolutionFailure) return;
						if (node.Parent is not InvocationExpressionSyntax { ArgumentList.Arguments: { Count: 1 } arguments }) return;
						if (model.GetTypeInfo(arguments[0].Expression, ctx.CancellationToken).Type?.TypeKind != TypeKind.Array) fix = null;
					}
					else if (!info.CandidateSymbols.IsEmpty)
					{
						return;
					}

					var properties = ImmutableDictionary<string, string?>.Empty;
					if (fix is not null) properties = properties.Add(ReplacementProperty, fix);
					ctx.ReportDiagnostic(Diagnostic.Create(SbkDescriptors.RemovedSliceApi, node.Name.GetLocation(), properties, name, entry.Advice));
				}, SyntaxKind.SimpleMemberAccessExpression);
			});
		}

	}
}
