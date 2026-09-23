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

	/// <summary>Rewrites a comparison with Slice.Empty to IsNull (negated for a difference), and renames an IsEmpty test to IsNull (FDB1005).</summary>
	[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
	public sealed class MissingKeyTestCodeFix : CodeFixProvider
	{

		public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("FDB1005");

		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var diagnostic = context.Diagnostics[0];
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			var node = root?.FindNode(context.Span, getInnermostNodeForTie: true);
			if (root is null) return;

			ExpressionSyntax? replacement = null;
			if (node is MemberAccessExpressionSyntax access)
			{
				replacement = access.WithName(IdentifierName("IsNull").WithTriviaFrom(access.Name));
			}
			else if (node is BinaryExpressionSyntax comparison && diagnostic.Properties.TryGetValue(MissingKeyTestAnalyzer.ValueSideProperty, out var side))
			{
				var value = side == "left" ? comparison.Left : comparison.Right;
				var test = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, Parenthesize(value.WithoutTrivia()), IdentifierName("IsNull"));
				replacement = comparison.IsKind(SyntaxKind.NotEqualsExpression) ? PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, test) : test;
				replacement = replacement.WithTriviaFrom(comparison);
			}
			if (replacement is null || node is null) return;

			context.RegisterCodeFix(
				CodeAction.Create("Test with IsNull", _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(node, replacement))), equivalenceKey: "FDB1005"),
				diagnostic);
		}

		/// <summary>The expression as the receiver of a member access: an await or any other lower-precedence form gets parentheses.</summary>
		private static ExpressionSyntax Parenthesize(ExpressionSyntax value) => value switch
		{
			IdentifierNameSyntax or MemberAccessExpressionSyntax or InvocationExpressionSyntax or ElementAccessExpressionSyntax or ParenthesizedExpressionSyntax or ThisExpressionSyntax => value,
			_ => ParenthesizedExpression(value),
		};

	}
}
