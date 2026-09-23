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

	/// <summary>Rewrites AtomicMax or AtomicMin to Atomic with FdbMutationType.ByteMax or ByteMin (FDB1003), and renames Set to SetVersionStampedKey (FDB1006).</summary>
	/// <remarks>FdbMutationType lives in the namespace of the transaction extensions that the fixed call already uses, so the fix adds no using directive.</remarks>
	[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
	public sealed class MutationEncodingCodeFix : CodeFixProvider
	{

		public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create("FDB1003", "FDB1006");

		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var diagnostic = context.Diagnostics[0];
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			var node = root?.FindNode(context.Span, getInnermostNodeForTie: true);
			if (root is null || node is null) return;

			if (diagnostic.Id == "FDB1006")
			{
				if (node is not SimpleNameSyntax name) return;
				var renamed = root.ReplaceNode(name, IdentifierName("SetVersionStampedKey").WithTriviaFrom(name));
				context.RegisterCodeFix(
					CodeAction.Create("Use SetVersionStampedKey", _ => Task.FromResult(context.Document.WithSyntaxRoot(renamed)), equivalenceKey: "FDB1006"),
					diagnostic);
				return;
			}

			if (!diagnostic.Properties.TryGetValue(MutationEncodingAnalyzer.MutationProperty, out var mutation) || mutation is null) return;
			// the node is the value argument; its own call (FdbValue.ToTextUtf8(...)) is an invocation too, so climb through the argument
			if (node.FirstAncestorOrSelf<ArgumentSyntax>()?.Parent?.Parent is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } invocation) return;

			var mutationArgument = Argument(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("FdbMutationType"), IdentifierName(mutation)));
			var rewritten = invocation
				.WithExpression(access.WithName(IdentifierName("Atomic").WithTriviaFrom(access.Name)))
				.WithArgumentList(invocation.ArgumentList.AddArguments(mutationArgument));
			var newRoot = root.ReplaceNode(invocation, rewritten);
			context.RegisterCodeFix(
				CodeAction.Create($"Use Atomic with FdbMutationType.{mutation}", _ => Task.FromResult(context.Document.WithSyntaxRoot(newRoot)), equivalenceKey: "FDB1003"),
				diagnostic);
		}

	}
}
