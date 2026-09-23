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
	using System.Collections.Generic;
	using System.Collections.Immutable;
	using System.Composition;
	using System.Linq;
	using System.Threading.Tasks;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CodeActions;
	using Microsoft.CodeAnalysis.CodeFixes;
	using Microsoft.CodeAnalysis.CSharp;
	using Microsoft.CodeAnalysis.CSharp.Syntax;
	using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

	/// <summary>Hoists a Guid factory call out of a retry-loop handler into a local declared before the statement that runs the loop.</summary>
	/// <remarks>The analyzer marks the hoistable calls in the diagnostic properties; a static lambda or a call with arguments gets no fix.</remarks>
	[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
	public sealed class RetryLoopValueCodeFix : CodeFixProvider
	{

		public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("FDB1004");

		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var diagnostic = context.Diagnostics[0];
			if (!diagnostic.Properties.ContainsKey(RetryLoopValueAnalyzer.HoistProperty)) return;
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			if (root?.FindNode(context.Span, getInnermostNodeForTie: true) is not InvocationExpressionSyntax call) return;
			var lambda = call.FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>();
			var statement = lambda?.FirstAncestorOrSelf<StatementSyntax>();
			if (lambda is null || statement is null) return;
			// the statement must run in the same body as the handler: an outer lambda between the two would capture the local
			if (lambda.Ancestors().TakeWhile(a => a != statement).OfType<AnonymousFunctionExpressionSyntax>().Any()) return;

			string name = PickName(statement);
			var eol = root.DescendantTrivia().FirstOrDefault(t => t.IsKind(SyntaxKind.EndOfLineTrivia));
			if (eol == default) eol = LineFeed;
			var declaration = ParseStatement($"var {name} = {call.WithoutTrivia()};")
				.WithLeadingTrivia(statement.GetLeadingTrivia())
				.WithTrailingTrivia(eol);
			var rewritten = statement.ReplaceNode(call, IdentifierName(name).WithTriviaFrom(call));
			var newRoot = root.ReplaceNode(statement, new SyntaxNode[] { declaration, rewritten });
			context.RegisterCodeFix(
				CodeAction.Create($"Compute {name} before the retry loop", _ => Task.FromResult(context.Document.WithSyntaxRoot(newRoot)), equivalenceKey: "FDB1004"),
				diagnostic);
		}

		/// <summary>The name id, or the first of id2, id3, ... that no local or parameter of the enclosing member uses.</summary>
		private static string PickName(StatementSyntax statement)
		{
			SyntaxNode scope = statement.FirstAncestorOrSelf<MemberDeclarationSyntax>() ?? statement.SyntaxTree.GetRoot();
			var taken = new HashSet<string>();
			foreach (var node in scope.DescendantNodes())
			{
				switch (node)
				{
					case VariableDeclaratorSyntax v: taken.Add(v.Identifier.ValueText); break;
					case ParameterSyntax p: taken.Add(p.Identifier.ValueText); break;
					case SingleVariableDesignationSyntax d: taken.Add(d.Identifier.ValueText); break;
					case ForEachStatementSyntax f: taken.Add(f.Identifier.ValueText); break;
				}
			}
			if (!taken.Contains("id")) return "id";
			for (int i = 2; ; i++)
			{
				if (!taken.Contains("id" + i)) return "id" + i;
			}
		}

	}
}
