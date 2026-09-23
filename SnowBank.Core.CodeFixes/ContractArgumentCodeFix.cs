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
	using System.Threading.Tasks;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CodeActions;
	using Microsoft.CodeAnalysis.CodeFixes;
	using Microsoft.CodeAnalysis.CSharp.Syntax;
	using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

	/// <summary>Replaces a Contract.Requires argument check with the Contract method that throws the matching ArgumentException (SBK1004).</summary>
	[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
	public sealed class ContractArgumentCodeFix : CodeFixProvider
	{

		public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("SBK1004");

		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var diagnostic = context.Diagnostics[0];
			if (!diagnostic.Properties.TryGetValue(ContractArgumentAnalyzer.ReplacementProperty, out var replacement) || replacement is null) return;
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			if (root?.FindNode(context.Span) is not InvocationExpressionSyntax node) return;

			var newRoot = root.ReplaceNode(node, ParseExpression(replacement).WithTriviaFrom(node));
			context.RegisterCodeFix(
				CodeAction.Create($"Use {replacement}", _ => Task.FromResult(context.Document.WithSyntaxRoot(newRoot)), equivalenceKey: "SBK1004"),
				diagnostic);
		}

	}
}
