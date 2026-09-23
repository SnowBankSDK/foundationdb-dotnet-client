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
	using Microsoft.CodeAnalysis.CSharp;
	using Microsoft.CodeAnalysis.CSharp.Syntax;
	using Microsoft.CodeAnalysis.Diagnostics;
	using Microsoft.CodeAnalysis.Operations;

	/// <summary>Reports an id or clock read inside a retry-loop handler whose result reaches a transaction call (FDB1004).</summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class RetryLoopValueAnalyzer : DiagnosticAnalyzer
	{

		/// <summary>Property of an FDB1004 diagnostic, present when the code fix can hoist the call to a local declared before the retry loop.</summary>
		public const string HoistProperty = "hoist";

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(FdbDescriptors.RetryLoopValue);

		public override void Initialize(AnalysisContext context)
		{
			context.EnableConcurrentExecution();
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.RegisterCompilationStartAction(start =>
			{
				var symbols = FdbSymbols.TryCreate(start.Compilation);
				if (symbols is null) return;
				start.RegisterOperationAction(ctx => Analyze(ctx, symbols), OperationKind.Invocation, OperationKind.PropertyReference);
			});
		}

		private static void Analyze(OperationAnalysisContext ctx, FdbSymbols symbols)
		{
			var op = ctx.Operation;
			if (!IsIdOrClock(op, symbols, out bool hoistable)) return;
			var lambda = FdbSymbols.GetEnclosingLambda(op);
			if (lambda is null || !symbols.IsRetryLoopHandler(lambda)) return;
			if (!ReachesTransactionCall(op, lambda, symbols)) return;

			var properties = ImmutableDictionary<string, string?>.Empty;
			if (hoistable && !IsStaticLambda(lambda)) properties = properties.Add(HoistProperty, "true");
			ctx.ReportDiagnostic(Diagnostic.Create(FdbDescriptors.RetryLoopValue, op.Syntax.GetLocation(), properties, op.Syntax.ToString()));
		}

		/// <summary>The operation reads a fresh id or the clock; hoistable when it is a parameterless Guid factory.</summary>
		private static bool IsIdOrClock(IOperation op, FdbSymbols symbols, out bool hoistable)
		{
			hoistable = false;
			switch (op)
			{
				case IInvocationOperation { TargetMethod: { IsStatic: true, Name: "NewGuid" or "CreateVersion7" } method } call when SymbolEqualityComparer.Default.Equals(method.ContainingType, symbols.Guid):
					hoistable = call.Arguments.IsEmpty;
					return true;
				case IInvocationOperation { TargetMethod: { Name: "GetUtcNow" } method } when FdbSymbols.IsOrImplements(method.ContainingType, symbols.TimeProvider):
					return true;
				case IPropertyReferenceOperation { Property: { IsStatic: true, Name: "UtcNow" or "Now" } property }
					when SymbolEqualityComparer.Default.Equals(property.ContainingType, symbols.DateTime) || SymbolEqualityComparer.Default.Equals(property.ContainingType, symbols.DateTimeOffset):
					return true;
				default:
					return false;
			}
		}

		/// <summary>The value is an argument of a transaction call, directly or through locals declared in the handler (at most three hops).</summary>
		private static bool ReachesTransactionCall(IOperation op, IAnonymousFunctionOperation lambda, FdbSymbols symbols, int depth = 0)
		{
			for (var current = op.Parent; current is not null and not IAnonymousFunctionOperation; current = current.Parent)
			{
				if (current is IArgumentOperation { Parent: IInvocationOperation invocation }
					&& FdbSymbols.GetReceiver(invocation) is { } receiver
					&& FdbSymbols.IsOrImplements(receiver.Type, symbols.IFdbReadOnlyTransaction))
				{
					return true;
				}
				if (current is IVariableInitializerOperation { Parent: IVariableDeclaratorOperation declarator })
				{
					if (depth >= 3) return false;
					return lambda.Body.Descendants().OfType<ILocalReferenceOperation>().Any(r => SymbolEqualityComparer.Default.Equals(r.Local, declarator.Symbol) && ReachesTransactionCall(r, lambda, symbols, depth + 1));
				}
			}
			return false;
		}

		private static bool IsStaticLambda(IAnonymousFunctionOperation lambda)
			=> lambda.Syntax is AnonymousFunctionExpressionSyntax syntax && syntax.Modifiers.Any(SyntaxKind.StaticKeyword);

	}
}
