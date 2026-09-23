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
	using System.Linq;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.Diagnostics;
	using Microsoft.CodeAnalysis.Operations;

	/// <summary>Reports a watch created with the token of its own transaction (FDB0002), and a watch awaited inside the retry-loop handler that created it (FDB0003).</summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class WatchAnalyzer : DiagnosticAnalyzer
	{

		/// <summary>Property of an FDB0002 diagnostic: the name of the retry loop's own token, present when the code fix can substitute it.</summary>
		public const string TokenProperty = "token";

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(FdbDescriptors.WatchTransactionToken, FdbDescriptors.WatchAwaitedInHandler);

		public override void Initialize(AnalysisContext context)
		{
			context.EnableConcurrentExecution();
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.RegisterCompilationStartAction(start =>
			{
				var symbols = FdbSymbols.TryCreate(start.Compilation);
				if (symbols is null) return;
				start.RegisterOperationAction(ctx => AnalyzeInvocation(ctx, (IInvocationOperation) ctx.Operation, symbols), OperationKind.Invocation);
				start.RegisterOperationAction(ctx => AnalyzeAwait(ctx, (IAwaitOperation) ctx.Operation, symbols), OperationKind.Await);
			});
		}

		private static void AnalyzeInvocation(OperationAnalysisContext ctx, IInvocationOperation op, FdbSymbols symbols)
		{
			if (op.TargetMethod.Name != "Watch") return;
			var receiver = FdbSymbols.GetReceiver(op);
			if (receiver is null || !FdbSymbols.IsOrImplements(receiver.Type, symbols.IFdbTransaction)) return;
			var transaction = FdbSymbols.GetReferencedSymbol(receiver);
			if (transaction is null) return;
			var token = op.Arguments.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.Parameter?.Type, symbols.CancellationToken));
			if (token is null || !IsTransactionToken(token.Value, transaction, op.SemanticModel)) return;

			var properties = ImmutableDictionary<string, string?>.Empty;
			if (FindRetryLoopToken(op, symbols) is { } name) properties = properties.Add(TokenProperty, name);
			ctx.ReportDiagnostic(Diagnostic.Create(FdbDescriptors.WatchTransactionToken, token.Value.Syntax.GetLocation(), properties));
		}

		/// <summary>The value is transaction.Cancellation, or a local initialized with it.</summary>
		private static bool IsTransactionToken(IOperation value, ISymbol transaction, SemanticModel? model)
		{
			value = FdbSymbols.Unwrap(value);
			if (value is ILocalReferenceOperation local && FdbSymbols.GetLocalInitializer(local.Local, model) is { } initializer) value = FdbSymbols.Unwrap(initializer);
			return value is IPropertyReferenceOperation { Property.Name: "Cancellation", Instance: { } instance }
				&& SymbolEqualityComparer.Default.Equals(FdbSymbols.GetReferencedSymbol(instance), transaction);
		}

		/// <summary>The name of the local or parameter passed as the token of the enclosing retry loop, when there is one.</summary>
		private static string? FindRetryLoopToken(IInvocationOperation op, FdbSymbols symbols)
		{
			var lambda = FdbSymbols.GetEnclosingLambda(op);
			if (lambda is null || symbols.GetRetryLoopInvocation(lambda) is not { } invocation) return null;
			foreach (var argument in invocation.Arguments)
			{
				if (!SymbolEqualityComparer.Default.Equals(argument.Parameter?.Type, symbols.CancellationToken)) continue;
				return FdbSymbols.Unwrap(argument.Value) switch
				{
					ILocalReferenceOperation local => local.Local.Name,
					IParameterReferenceOperation parameter => parameter.Parameter.Name,
					_ => null,
				};
			}
			return null;
		}

		private static void AnalyzeAwait(OperationAnalysisContext ctx, IAwaitOperation op, FdbSymbols symbols)
		{
			var operand = FdbSymbols.Unwrap(op.Operation);
			bool isWatch = SymbolEqualityComparer.Default.Equals(operand.Type, symbols.FdbWatch)
				|| (operand is IInvocationOperation { TargetMethod: { Name: "WaitAsync" } method } && SymbolEqualityComparer.Default.Equals(method.ContainingType, symbols.FdbWatch));
			if (!isWatch) return;
			var lambda = FdbSymbols.GetEnclosingLambda(op);
			if (lambda is null || !symbols.IsRetryLoopHandler(lambda)) return;
			ctx.ReportDiagnostic(Diagnostic.Create(FdbDescriptors.WatchAwaitedInHandler, op.Syntax.GetLocation()));
		}

	}
}
