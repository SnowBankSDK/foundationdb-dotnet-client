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
	using System.Linq;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp.Syntax;
	using Microsoft.CodeAnalysis.Diagnostics;
	using Microsoft.CodeAnalysis.Operations;

	/// <summary>Reports Contract.Requires, Contract.Assert, or Contract.Debug.Requires used to check an argument of a public member (SBK1004).</summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class ContractArgumentAnalyzer : DiagnosticAnalyzer
	{

		/// <summary>Property of the diagnostic: the call that replaces the reported one.</summary>
		public const string ReplacementProperty = "replacement";

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(SbkDescriptors.ContractOnPublicArgument);

		public override void Initialize(AnalysisContext context)
		{
			context.EnableConcurrentExecution();
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.RegisterCompilationStartAction(start =>
			{
				var contract = start.Compilation.GetTypeByMetadataName("SnowBank.Diagnostics.Contracts.Contract");
				var debug = start.Compilation.GetTypeByMetadataName("SnowBank.Diagnostics.Contracts.Contract+Debug");
				if (contract is null) return;

				start.RegisterOperationAction(ctx =>
				{
					var op = (IInvocationOperation) ctx.Operation;
					var method = op.TargetMethod;
					if (method.Name is not ("Requires" or "Assert")) return;
					var owner = method.ContainingType;
					if (!SymbolEqualityComparer.Default.Equals(owner, contract) && !SymbolEqualityComparer.Default.Equals(owner, debug)) return;
					// a custom message is not carried over by the rewrite, so the call is left alone
					if (op.Arguments.Count(a => a.ArgumentKind == ArgumentKind.Explicit) != 1) return;
					if (ctx.ContainingSymbol is not IMethodSymbol member || !IsPublicSurface(member)) return;

					var condition = Unwrap(op.Arguments[0].Value);
					if (GetShape(condition, member, contract, ctx.Compilation) is not var (target, parameter, extra)) return;

					string receiver = GetReceiverText(op.Syntax, SymbolEqualityComparer.Default.Equals(owner, debug));
					string replacement = $"{receiver}{target}({parameter.Name}{extra})";
					var properties = ImmutableDictionary<string, string?>.Empty.Add(ReplacementProperty, replacement);
					ctx.ReportDiagnostic(Diagnostic.Create(SbkDescriptors.ContractOnPublicArgument, op.Syntax.GetLocation(), properties, replacement));
				}, OperationKind.Invocation);
			});
		}

		/// <summary>The member is public or protected, and every type around it is visible outside the assembly.</summary>
		private static bool IsPublicSurface(IMethodSymbol member)
		{
			if (member.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal)) return false;
			for (var type = member.ContainingType; type is not null; type = type.ContainingType)
			{
				if (type.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal)) return false;
			}
			return true;
		}

		/// <summary>The Contract method that replaces the condition, the parameter it checks, and the extra argument text; null for a condition outside the listed shapes.</summary>
		private static (string Target, IParameterSymbol Parameter, string Extra)? GetShape(IOperation condition, IMethodSymbol member, INamedTypeSymbol contract, Compilation compilation)
		{
			switch (condition)
			{
				case IBinaryOperation { OperatorKind: BinaryOperatorKind.NotEquals } notEquals:
				{
					// p != null, null != p
					var parameter = IsNull(notEquals.RightOperand) ? AsParameter(notEquals.LeftOperand, member) : IsNull(notEquals.LeftOperand) ? AsParameter(notEquals.RightOperand, member) : null;
					return parameter is null ? null : ("NotNull", parameter, "");
				}
				case IIsPatternOperation { Pattern: INegatedPatternOperation { Pattern: IConstantPatternOperation constant } } pattern when IsNull(constant.Value):
				{
					// p is not null
					var parameter = AsParameter(pattern.Value, member);
					return parameter is null ? null : ("NotNull", parameter, "");
				}
				case IUnaryOperation { OperatorKind: UnaryOperatorKind.Not, Operand: IInvocationOperation { TargetMethod: { IsStatic: true, ContainingType.SpecialType: SpecialType.System_String, Name: "IsNullOrEmpty" or "IsNullOrWhiteSpace" } test, Arguments.Length: 1 } call }:
				{
					// !string.IsNullOrEmpty(p), !string.IsNullOrWhiteSpace(p)
					var parameter = AsParameter(call.Arguments[0].Value, member);
					return parameter is null ? null : (test.Name == "IsNullOrEmpty" ? "NotNullOrEmpty" : "NotNullOrWhiteSpace", parameter, "");
				}
				case IBinaryOperation { OperatorKind: BinaryOperatorKind.GreaterThan or BinaryOperatorKind.GreaterThanOrEqual or BinaryOperatorKind.LessThan or BinaryOperatorKind.LessThanOrEqual } compare:
				{
					// p > 0, 0 < p, p >= 0, 0 <= p
					bool reversed = compare.OperatorKind is BinaryOperatorKind.LessThan or BinaryOperatorKind.LessThanOrEqual;
					var (value, bound) = reversed ? (compare.RightOperand, compare.LeftOperand) : (compare.LeftOperand, compare.RightOperand);
					if (!IsZero(bound) || AsParameter(value, member) is not { } parameter) return null;
					bool strict = compare.OperatorKind is BinaryOperatorKind.GreaterThan or BinaryOperatorKind.LessThan;
					if (strict)
					{
						return HasPositiveOverload(contract, parameter.Type, compilation) ? ("Positive", parameter, "") : null;
					}
					return IsSignedNumber(parameter.Type) ? ("GreaterOrEqual", parameter, ", 0") : null;
				}
				default:
					return null;
			}
		}

		/// <summary>The expression reads a parameter of the member, or null.</summary>
		private static IParameterSymbol? AsParameter(IOperation op, IMethodSymbol member)
			=> Unwrap(op) is IParameterReferenceOperation { Parameter: { } parameter } && SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, member) ? parameter : null;

		private static bool IsNull(IOperation op) => Unwrap(op).ConstantValue is { HasValue: true, Value: null };

		private static bool IsZero(IOperation op) => Unwrap(op) is { ConstantValue: { HasValue: true, Value: { } value } } && value is 0 or 0L or 0.0 or 0.0f or 0m;

		/// <summary>Contract.Positive accepts the parameter type (int, long, double, float, or a type that converts to one of them).</summary>
		private static bool HasPositiveOverload(INamedTypeSymbol contract, ITypeSymbol type, Compilation compilation)
			=> contract.GetMembers("Positive").OfType<IMethodSymbol>().Any(m => m.Parameters.Length > 0 && compilation.ClassifyCommonConversion(type, m.Parameters[0].Type).IsImplicit);

		/// <summary>Contract.GreaterOrEqual(p, 0) infers its type argument from these types.</summary>
		private static bool IsSignedNumber(ITypeSymbol type)
			=> type.SpecialType is SpecialType.System_Int16 or SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Single or SpecialType.System_Double or SpecialType.System_Decimal;

		/// <summary>The receiver text of the call including the trailing dot ('Contract.', a qualified name, or empty under a static import), without the Debug part.</summary>
		private static string GetReceiverText(SyntaxNode syntax, bool debug)
		{
			if (syntax is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access }) return "";
			var receiver = access.Expression;
			if (debug && receiver is MemberAccessExpressionSyntax inner) receiver = inner.Expression;
			return receiver.ToString() + ".";
		}

		private static IOperation Unwrap(IOperation op)
		{
			while (op is IConversionOperation { IsImplicit: true } conversion) op = conversion.Operand;
			return op;
		}

	}
}
