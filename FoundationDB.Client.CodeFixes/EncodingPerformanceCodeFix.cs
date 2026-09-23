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

namespace FoundationDB.Analyzers
{
	using System.Collections.Immutable;
	using System.Composition;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CodeActions;
	using Microsoft.CodeAnalysis.CodeFixes;
	using Microsoft.CodeAnalysis.CSharp.Syntax;
	using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

	/// <summary>Replaces a Core factory value with its FdbValue equivalent (FDB2001), and removes ToSlice() from a typed key (FDB2002).</summary>
	/// <remarks>The reported node is the argument itself, or a local declared with var: the fix then rewrites the local's initializer. A local with an explicit Slice type gets no fix.</remarks>
	[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
	public sealed class EncodingPerformanceCodeFix : CodeFixProvider
	{

		public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("FDB2001", "FDB2002");

		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var diagnostic = context.Diagnostics[0];
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			if (root?.FindNode(context.Span, getInnermostNodeForTie: true) is not ExpressionSyntax node) return;
			var target = await ResolveTarget(context.Document, node, context.CancellationToken).ConfigureAwait(false);
			if (target is null) return;

			ExpressionSyntax replacement;
			string title;
			if (diagnostic.Id == "FDB2001")
			{
				if (!diagnostic.Properties.TryGetValue(EncodingPerformanceAnalyzer.ReplacementProperty, out var text) || text is null) return;
				replacement = ParseExpression(text);
				title = $"Pass {text}";
			}
			else
			{
				if (target is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ToSlice" } access }) return;
				replacement = access.Expression;
				title = "Remove .ToSlice()";
			}
			var newRoot = root.ReplaceNode(target, replacement.WithTriviaFrom(target));
			context.RegisterCodeFix(
				CodeAction.Create(title, _ => Task.FromResult(context.Document.WithSyntaxRoot(newRoot)), equivalenceKey: diagnostic.Id),
				diagnostic);
		}

		/// <summary>The expression to rewrite: the node itself, or the initializer of the local it names when that local is declared with var.</summary>
		private static async Task<ExpressionSyntax?> ResolveTarget(Document document, ExpressionSyntax node, CancellationToken ct)
		{
			if (node is not IdentifierNameSyntax) return node;
			var model = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
			if (model?.GetSymbolInfo(node, ct).Symbol is not ILocalSymbol local || local.DeclaringSyntaxReferences.Length != 1) return null;
			if (local.DeclaringSyntaxReferences[0].GetSyntax(ct) is not VariableDeclaratorSyntax { Initializer.Value: { } initializer, Parent: VariableDeclarationSyntax { Type.IsVar: true } }) return null;
			return initializer;
		}

	}
}
