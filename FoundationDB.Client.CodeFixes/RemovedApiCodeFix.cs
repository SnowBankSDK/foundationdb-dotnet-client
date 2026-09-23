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
	using System.Threading.Tasks;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CodeActions;
	using Microsoft.CodeAnalysis.CodeFixes;
	using Microsoft.CodeAnalysis.CSharp;
	using Microsoft.CodeAnalysis.CSharp.Syntax;
	using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

	/// <summary>Rewrites a removed member to its replacement: a rename, EncodeRange(a) to Key(a).ToRange(), or subspace[a, b] to subspace.Key(a, b).</summary>
	/// <remarks>The analyzer names the replacement in the diagnostic properties; without one there is no fix.</remarks>
	[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
	public sealed class RemovedApiCodeFix : CodeFixProvider
	{

		public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("FDB0100");

		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var diagnostic = context.Diagnostics[0];
			if (!diagnostic.Properties.TryGetValue(RemovedApiAnalyzer.ReplacementProperty, out var replacement) || replacement is null) return;
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			var node = root?.FindNode(context.Span, getInnermostNodeForTie: true);
			if (root is null || node is null) return;

			SyntaxNode? target;
			SyntaxNode? rewritten;
			string title;
			switch (replacement)
			{
				case RemovedApiAnalyzer.IndexerRewrite:
					target = node.FirstAncestorOrSelf<ElementAccessExpressionSyntax>();
					rewritten = target is ElementAccessExpressionSyntax element ? Call(element.Expression, "Key", element.ArgumentList.Arguments) : null;
					title = "Use Key(...)";
					break;
				case RemovedApiAnalyzer.RangeRewrite:
					target = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
					rewritten = target is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } invocation
						? Call(Call(access.Expression, "Key", invocation.ArgumentList.Arguments), "ToRange", default)
						: null;
					title = "Use Key(...).ToRange()";
					break;
				default:
					target = node as SimpleNameSyntax;
					rewritten = IdentifierName(replacement);
					title = $"Use {replacement}";
					break;
			}
			if (target is null || rewritten is null) return;

			var newRoot = root.ReplaceNode(target, rewritten.WithTriviaFrom(target));
			context.RegisterCodeFix(
				CodeAction.Create(title, _ => Task.FromResult(context.Document.WithSyntaxRoot(newRoot)), equivalenceKey: "FDB0100"),
				diagnostic);
		}

		private static InvocationExpressionSyntax Call(ExpressionSyntax receiver, string name, SeparatedSyntaxList<ArgumentSyntax> arguments)
			=> InvocationExpression(
				MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, receiver.WithoutTrivia(), IdentifierName(name)),
				ArgumentList(arguments));

	}
}
