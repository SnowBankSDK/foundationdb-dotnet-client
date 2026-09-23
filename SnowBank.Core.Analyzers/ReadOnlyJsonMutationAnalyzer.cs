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

	/// <summary>Reports an indexer assignment or a mutating call on a local assigned once from a read-only JSON factory, ToReadOnly(), or Freeze() (SBK0002).</summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class ReadOnlyJsonMutationAnalyzer : DiagnosticAnalyzer
	{

		private static readonly ImmutableHashSet<string> MutatingMethods = ImmutableHashSet.Create("Add", "Set", "Remove", "Clear", "Insert", "AddRange");

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(SbkDescriptors.ReadOnlyJsonMutation);

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
					var op = (ISimpleAssignmentOperation) ctx.Operation;
					if (op.Target is IPropertyReferenceOperation { Property.IsIndexer: true, Instance: ILocalReferenceOperation local } && IsReadOnlyLocal(local, jsonValue))
					{
						Report(ctx, op, local);
					}
				}, OperationKind.SimpleAssignment);

				start.RegisterOperationAction(ctx =>
				{
					var op = (IInvocationOperation) ctx.Operation;
					if (MutatingMethods.Contains(op.TargetMethod.Name) && op.Instance is ILocalReferenceOperation local && IsReadOnlyLocal(local, jsonValue))
					{
						Report(ctx, op, local);
					}
				}, OperationKind.Invocation);
			});
		}

		private static void Report(OperationAnalysisContext ctx, IOperation op, ILocalReferenceOperation local)
			=> ctx.ReportDiagnostic(Diagnostic.Create(SbkDescriptors.ReadOnlyJsonMutation, op.Syntax.GetLocation(), local.Local.Name));

		/// <summary>The local is a JsonValue assigned only by its declaration, from a read-only factory, ToReadOnly(), or Freeze().</summary>
		private static bool IsReadOnlyLocal(ILocalReferenceOperation reference, INamedTypeSymbol jsonValue)
		{
			var local = reference.Local;
			if (!Derives(local.Type, jsonValue)) return false;
			if (local.DeclaringSyntaxReferences.Length != 1
				|| local.DeclaringSyntaxReferences[0].GetSyntax() is not VariableDeclaratorSyntax { Initializer.Value: { } initializerSyntax }
				|| reference.SemanticModel?.GetOperation(initializerSyntax) is not { } initializer)
			{
				return false;
			}
			if (!IsReadOnlySource(Unwrap(initializer), jsonValue)) return false;

			// a later assignment can replace the frozen instance by a mutable one
			var root = (IOperation) reference;
			while (root.Parent is not null) root = root.Parent;
			return !root.Descendants().Any(o => o is IAssignmentOperation { Target: ILocalReferenceOperation target } && SymbolEqualityComparer.Default.Equals(target.Local, local));
		}

		/// <summary>The expression is a call or member of a nested ReadOnly class of the JSON types, or a call to ToReadOnly() or Freeze().</summary>
		private static bool IsReadOnlySource(IOperation source, INamedTypeSymbol jsonValue)
		{
			switch (source)
			{
				case IInvocationOperation { TargetMethod: { } method }:
					if (method.Name is "ToReadOnly" or "Freeze" && method.Parameters.IsEmpty && Derives(method.ContainingType, jsonValue)) return true;
					return IsReadOnlyClass(method.ContainingType, jsonValue);
				case IPropertyReferenceOperation property:
					return IsReadOnlyClass(property.Property.ContainingType, jsonValue);
				case IFieldReferenceOperation field:
					return IsReadOnlyClass(field.Field.ContainingType, jsonValue);
				default:
					return false;
			}
		}

		private static bool IsReadOnlyClass(INamedTypeSymbol? type, INamedTypeSymbol jsonValue)
			=> type is { Name: "ReadOnly", IsStatic: true, ContainingType: { } owner } && Derives(owner, jsonValue);

		private static bool Derives(ITypeSymbol? type, INamedTypeSymbol baseType)
		{
			for (var t = type; t is not null; t = t.BaseType)
			{
				if (SymbolEqualityComparer.Default.Equals(t, baseType)) return true;
			}
			return false;
		}

		private static IOperation Unwrap(IOperation op)
		{
			while (op is IConversionOperation { IsImplicit: true } conversion) op = conversion.Operand;
			return op;
		}

	}
}
