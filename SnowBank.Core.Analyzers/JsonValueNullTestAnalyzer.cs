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
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.Diagnostics;
	using Microsoft.CodeAnalysis.Operations;

	/// <summary>Reports null tests on JsonValue expressions that the compiler knows are never null.</summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class JsonValueNullTestAnalyzer : DiagnosticAnalyzer
	{

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(SbkDescriptors.JsonValueNullTest);

		public override void Initialize(AnalysisContext context)
		{
			context.EnableConcurrentExecution();
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.RegisterCompilationStartAction(start =>
			{
				var jsonValue = start.Compilation.GetTypeByMetadataName("SnowBank.Data.Json.JsonValue");
				if (jsonValue is null) return;

				start.RegisterOperationAction(ctx =>
				{
					var op = (IBinaryOperation) ctx.Operation;
					if (op.OperatorKind is not (BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals)) return;
					if (op.OperatorMethod is not null) return;
					var value = IsNullLiteral(op.RightOperand) ? op.LeftOperand : IsNullLiteral(op.LeftOperand) ? op.RightOperand : null;
					if (value is not null && IsNeverNull(value, jsonValue, ctx)) Report(ctx, op);
				}, OperationKind.Binary);

				start.RegisterOperationAction(ctx =>
				{
					var op = (IIsPatternOperation) ctx.Operation;
					var pattern = op.Pattern is INegatedPatternOperation negated ? negated.Pattern : op.Pattern;
					if (pattern is IConstantPatternOperation constant && IsNullLiteral(constant.Value) && IsNeverNull(op.Value, jsonValue, ctx)) Report(ctx, op);
				}, OperationKind.IsPattern);

				start.RegisterOperationAction(ctx =>
				{
					var op = (ICoalesceOperation) ctx.Operation;
					if (IsNeverNull(op.Value, jsonValue, ctx)) Report(ctx, op);
				}, OperationKind.Coalesce);

				start.RegisterOperationAction(ctx =>
				{
					var op = (IConditionalAccessOperation) ctx.Operation;
					if (IsNeverNull(op.Operation, jsonValue, ctx)) Report(ctx, op);
				}, OperationKind.ConditionalAccess);
			});
		}

		private static void Report(OperationAnalysisContext ctx, IOperation op) => ctx.ReportDiagnostic(Diagnostic.Create(SbkDescriptors.JsonValueNullTest, op.Syntax.GetLocation()));

		private static bool IsNullLiteral(IOperation op)
		{
			while (op is IConversionOperation { IsImplicit: true } conversion) op = conversion.Operand;
			return op.ConstantValue is { HasValue: true, Value: null };
		}

		/// <summary>The expression has a JsonValue type, in a nullable context, and is either declared non-nullable or has a non-null flow state.</summary>
		/// <remarks>The declared type matters because an earlier null test on the same variable turns its flow state to maybe-null.</remarks>
		private static bool IsNeverNull(IOperation op, INamedTypeSymbol jsonValue, OperationAnalysisContext ctx)
		{
			while (op is IConversionOperation { IsImplicit: true } conversion) op = conversion.Operand;
			if (!InheritsFrom(op.Type, jsonValue)) return false;

			// library types are annotated whatever the caller's context: outside a nullable context the caller made no promise
			var model = op.SemanticModel;
			if (model is null) return false;
			var context = model.GetNullableContext(op.Syntax.SpanStart);
			if (!context.AnnotationsEnabled() && !context.WarningsEnabled()) return false;

			var declared = op switch
			{
				ILocalReferenceOperation local => local.Local.Type.NullableAnnotation,
				IParameterReferenceOperation parameter => parameter.Parameter.Type.NullableAnnotation,
				IFieldReferenceOperation field => field.Field.Type.NullableAnnotation,
				IPropertyReferenceOperation property => property.Property.Type.NullableAnnotation,
				IInvocationOperation invocation => invocation.TargetMethod.ReturnType.NullableAnnotation,
				_ => NullableAnnotation.None,
			};
			if (declared == NullableAnnotation.NotAnnotated) return true;

			var info = model.GetTypeInfo(op.Syntax, ctx.CancellationToken);
			return info.Nullability.FlowState == NullableFlowState.NotNull;
		}

		private static bool InheritsFrom(ITypeSymbol? type, INamedTypeSymbol baseType)
		{
			for (var t = type; t is not null; t = t.BaseType)
			{
				if (SymbolEqualityComparer.Default.Equals(t, baseType)) return true;
			}
			return false;
		}

	}
}
