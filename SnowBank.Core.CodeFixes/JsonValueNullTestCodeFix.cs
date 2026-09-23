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
	using System.Collections.Immutable;
	using System.Composition;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CodeActions;
	using Microsoft.CodeAnalysis.CodeFixes;
	using Microsoft.CodeAnalysis.CSharp;
	using Microsoft.CodeAnalysis.CSharp.Syntax;
	using Microsoft.CodeAnalysis.Simplification;
	using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

	/// <summary>Rewrites a null test on a JsonValue into IsNullOrMissing().</summary>
	[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
	public sealed class JsonValueNullTestCodeFix : CodeFixProvider
	{

		private const string JsonNamespace = "SnowBank.Data.Json";

		public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("SBK1003");

		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			if (root?.FindNode(context.Span, getInnermostNodeForTie: true) is not ExpressionSyntax node) return;
			if (!TryDecompose(node, out _, out _)) return;

			context.RegisterCodeFix(
				CodeAction.Create("Use IsNullOrMissing()", ct => FixAsync(context.Document, node, ct), equivalenceKey: "SBK1003"),
				context.Diagnostics);
		}

		/// <summary>Splits a null test into the tested value and whether the test is negated. The ?? and ?. forms have no fix.</summary>
		private static bool TryDecompose(ExpressionSyntax node, out ExpressionSyntax value, out bool negate)
		{
			switch (node)
			{
				case BinaryExpressionSyntax b when b.IsKind(SyntaxKind.EqualsExpression) || b.IsKind(SyntaxKind.NotEqualsExpression):
				{
					value = b.Right.IsKind(SyntaxKind.NullLiteralExpression) ? b.Left : b.Right;
					negate = b.IsKind(SyntaxKind.NotEqualsExpression);
					return true;
				}
				case IsPatternExpressionSyntax { Pattern: ConstantPatternSyntax } p:
				{
					value = p.Expression;
					negate = false;
					return true;
				}
				case IsPatternExpressionSyntax { Pattern: UnaryPatternSyntax { Pattern: ConstantPatternSyntax } } p:
				{
					value = p.Expression;
					negate = true;
					return true;
				}
				default:
				{
					value = null!;
					negate = false;
					return false;
				}
			}
		}

		private static async Task<Document> FixAsync(Document document, ExpressionSyntax node, CancellationToken ct)
		{
			TryDecompose(node, out var value, out bool negate);
			var model = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
			var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
			if (model is null || root is null) return document;

			// the simplifier removes the parentheses when the value does not need them
			ExpressionSyntax call = InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					ParenthesizedExpression(value.WithoutTrivia()).WithAdditionalAnnotations(Simplifier.Annotation),
					IdentifierName("IsNullOrMissing")));
			if (negate) call = PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, call);
			var newRoot = root.ReplaceNode(node, call.WithTriviaFrom(node));

			var valueType = model.GetTypeInfo(value, ct).Type;
			bool inScope = valueType is not null && model.LookupSymbols(value.SpanStart, valueType, "IsNullOrMissing", includeReducedExtensionMethods: true).Any();
			if (!inScope && newRoot is CompilationUnitSyntax unit)
			{
				// reuse the line ending of the document, so the new line matches the others
				var eol = root.DescendantTrivia().FirstOrDefault(t => t.IsKind(SyntaxKind.EndOfLineTrivia));
				if (eol == default) eol = CarriageReturnLineFeed;
				newRoot = unit.AddUsings(UsingDirective(ParseName(JsonNamespace)).WithTrailingTrivia(eol));
			}
			return document.WithSyntaxRoot(newRoot);
		}

	}
}
