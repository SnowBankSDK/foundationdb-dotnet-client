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
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.Diagnostics;
	using Microsoft.CodeAnalysis.Operations;

	/// <summary>Reports a GetAsync result compared with Slice.Empty or tested with IsEmpty, which a missing key never satisfies (FDB1005).</summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class MissingKeyTestAnalyzer : DiagnosticAnalyzer
	{

		/// <summary>Property of an FDB1005 diagnostic on a comparison: "left" or "right", the operand that holds the read value.</summary>
		public const string ValueSideProperty = "value";

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(FdbDescriptors.MissingKeyEmptyTest);

		public override void Initialize(AnalysisContext context)
		{
			context.EnableConcurrentExecution();
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.RegisterCompilationStartAction(start =>
			{
				var symbols = FdbSymbols.TryCreate(start.Compilation);
				if (symbols is null || symbols.Slice is null) return;
				start.RegisterOperationAction(ctx => AnalyzeComparison(ctx, (IBinaryOperation) ctx.Operation, symbols), OperationKind.Binary);
				start.RegisterOperationAction(ctx => AnalyzeIsEmpty(ctx, (IPropertyReferenceOperation) ctx.Operation, symbols), OperationKind.PropertyReference);
			});
		}

		private static void AnalyzeComparison(OperationAnalysisContext ctx, IBinaryOperation op, FdbSymbols symbols)
		{
			if (op.OperatorKind is not (BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals)) return;
			if (!SymbolEqualityComparer.Default.Equals(op.OperatorMethod?.ContainingType, symbols.Slice)) return;
			string? side = IsSliceEmpty(op.RightOperand, symbols) && IsReadValue(op.LeftOperand, symbols) ? "left"
				: IsSliceEmpty(op.LeftOperand, symbols) && IsReadValue(op.RightOperand, symbols) ? "right"
				: null;
			if (side is null) return;
			var properties = ImmutableDictionary<string, string?>.Empty.Add(ValueSideProperty, side);
			ctx.ReportDiagnostic(Diagnostic.Create(FdbDescriptors.MissingKeyEmptyTest, op.Syntax.GetLocation(), properties));
		}

		private static void AnalyzeIsEmpty(OperationAnalysisContext ctx, IPropertyReferenceOperation op, FdbSymbols symbols)
		{
			if (op.Property.Name != "IsEmpty" || !SymbolEqualityComparer.Default.Equals(op.Property.ContainingType, symbols.Slice)) return;
			if (op.Instance is null || !IsReadValue(op.Instance, symbols) || !IsWholeCondition(op)) return;
			ctx.ReportDiagnostic(Diagnostic.Create(FdbDescriptors.MissingKeyEmptyTest, op.Syntax.GetLocation()));
		}

		private static bool IsSliceEmpty(IOperation op, FdbSymbols symbols)
			=> FdbSymbols.Unwrap(op) is IFieldReferenceOperation { Field: { IsStatic: true, Name: "Empty" } field } && SymbolEqualityComparer.Default.Equals(field.ContainingType, symbols.Slice);

		/// <summary>The value is an awaited GetAsync on a transaction, or a local initialized with one.</summary>
		private static bool IsReadValue(IOperation op, FdbSymbols symbols)
		{
			op = FdbSymbols.Unwrap(op);
			if (op is ILocalReferenceOperation local && FdbSymbols.GetLocalInitializer(local.Local, op.SemanticModel) is { } initializer) op = FdbSymbols.Unwrap(initializer);
			return op is IAwaitOperation { Operation: IInvocationOperation { TargetMethod.Name: "GetAsync" } read }
				&& FdbSymbols.GetReceiver(read) is { } receiver
				&& FdbSymbols.IsOrImplements(receiver.Type, symbols.IFdbReadOnlyTransaction);
		}

		/// <summary>The test is the whole condition of an if, a while, or a conditional expression, or its negation.</summary>
		private static bool IsWholeCondition(IOperation op)
		{
			var test = op.Parent is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } not ? not : op;
			return test.Parent switch
			{
				IConditionalOperation conditional => conditional.Condition == test,
				IWhileLoopOperation loop => loop.Condition == test,
				_ => false,
			};
		}

	}
}
