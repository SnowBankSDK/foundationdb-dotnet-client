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
	using Microsoft.CodeAnalysis.CSharp.Syntax;
	using Microsoft.CodeAnalysis.Editing;
	using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

	/// <summary>Redirects the retry loops and Root of a database local to the provider it came from, and removes the local (FDB2003).</summary>
	/// <remarks>The analyzer names the provider expression in the diagnostic properties; a provider that is not a plain reference gets no fix.</remarks>
	[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
	public sealed class ProviderUnwrapCodeFix : CodeFixProvider
	{

		public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("FDB2003");

		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var diagnostic = context.Diagnostics[0];
			if (!diagnostic.Properties.TryGetValue(ProviderUnwrapAnalyzer.ProviderProperty, out var provider) || provider is null) return;
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			var statement = root?.FindNode(context.Span, getInnermostNodeForTie: true).FirstAncestorOrSelf<LocalDeclarationStatementSyntax>();
			if (statement is null || statement.Declaration.Variables.Count != 1) return;

			context.RegisterCodeFix(
				CodeAction.Create($"Call {provider} directly", ct => Rewrite(context.Document, statement, provider, ct), equivalenceKey: "FDB2003"),
				diagnostic);
		}

		private static async Task<Document> Rewrite(Document document, LocalDeclarationStatementSyntax statement, string provider, CancellationToken ct)
		{
			var editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);
			var model = editor.SemanticModel;
			var local = model.GetDeclaredSymbol(statement.Declaration.Variables[0], ct);
			SyntaxNode scope = statement.FirstAncestorOrSelf<MemberDeclarationSyntax>() ?? editor.OriginalRoot;
			foreach (var name in scope.DescendantNodes().OfType<IdentifierNameSyntax>())
			{
				if (!SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name, ct).Symbol, local)) continue;
				editor.ReplaceNode(name, ParseExpression(provider).WithTriviaFrom(name));
			}
			editor.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
			return editor.GetChangedDocument();
		}

	}
}
