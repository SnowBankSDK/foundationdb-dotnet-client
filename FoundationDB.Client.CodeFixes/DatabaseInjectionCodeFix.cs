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
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CodeActions;
	using Microsoft.CodeAnalysis.CodeFixes;
	using Microsoft.CodeAnalysis.CSharp;
	using Microsoft.CodeAnalysis.CSharp.Syntax;
	using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

	/// <summary>Changes an injected IFdbDatabase parameter to IFdbDatabaseProvider, when the parameter is used only for members that both types have.</summary>
	[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
	public sealed class DatabaseInjectionCodeFix : CodeFixProvider
	{

		/// <summary>Members that IFdbDatabaseProvider also has (ReadAsync, WriteAsync, ReadWriteAsync are extensions of IFdbDatabaseScopeProvider).</summary>
		private static readonly ImmutableHashSet<string> SharedMembers = ImmutableHashSet.Create("ReadAsync", "WriteAsync", "ReadWriteAsync", "Root");

		public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("FDB0001");

		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
			if (root is null || model is null) return;
			if (root.FindNode(context.Span, getInnermostNodeForTie: true)?.FirstAncestorOrSelf<ParameterSyntax>() is not { Type: not null } parameter) return;
			if (!UsesOnlySharedMembers(parameter, model, context.CancellationToken)) return;

			context.RegisterCodeFix(
				CodeAction.Create("Inject IFdbDatabaseProvider", ct => FixAsync(context.Document, parameter, ct), equivalenceKey: "FDB0001"),
				context.Diagnostics);
		}

		private static bool UsesOnlySharedMembers(ParameterSyntax parameter, SemanticModel model, CancellationToken ct)
		{
			var symbol = model.GetDeclaredSymbol(parameter, ct);
			if (symbol is null) return false;
			// the scope of a lambda parameter is the lambda, the scope of a (primary) constructor parameter is its type
			SyntaxNode? scope = parameter.FirstAncestorOrSelf<SyntaxNode>(n => n is AnonymousFunctionExpressionSyntax or TypeDeclarationSyntax or BaseMethodDeclarationSyntax);
			if (scope is null) return false;
			if (scope is ConstructorDeclarationSyntax) scope = scope.Parent;
			if (scope is null) return false;

			bool used = false;
			foreach (var identifier in scope.DescendantNodes().OfType<IdentifierNameSyntax>())
			{
				if (identifier.Identifier.ValueText != symbol.Name) continue;
				if (!SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier, ct).Symbol, symbol)) continue;
				if (identifier.Parent is not MemberAccessExpressionSyntax access || access.Expression != identifier) return false;
				if (!SharedMembers.Contains(access.Name.Identifier.ValueText)) return false;
				used = true;
			}
			return used;
		}

		private static async Task<Document> FixAsync(Document document, ParameterSyntax parameter, CancellationToken ct)
		{
			var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
			if (root is null || parameter.Type is null) return document;
			// IFdbDatabaseProvider lives in the same namespace as IFdbDatabase, which the parameter already names
			var newType = IdentifierName("IFdbDatabaseProvider").WithTriviaFrom(parameter.Type);
			return document.WithSyntaxRoot(root.ReplaceNode(parameter.Type, newType));
		}

	}
}
