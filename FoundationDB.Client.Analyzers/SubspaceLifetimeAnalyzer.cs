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
	using System.Collections.Concurrent;
	using System.Collections.Immutable;
	using System.Linq;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp;
	using Microsoft.CodeAnalysis.CSharp.Syntax;
	using Microsoft.CodeAnalysis.Diagnostics;
	using Microsoft.CodeAnalysis.Operations;

	/// <summary>Reports a subspace or a layer state kept across transactions: in a field, an auto-property, or a singleton registration (FDB1001), or returned by a ReadAsync or ReadWriteAsync handler (FDB1002).</summary>
	/// <remarks>
	/// <para>The state types of the layers implemented in the compilation, and the types that are only created inside a transaction, are known only once every type has been seen, so both rules report at compilation end.</para>
	/// <para>A member is not reported when it cannot outlive a transaction: an interface or abstract member, a readonly struct, a subspace built from a fixed prefix, or a member of a type created only inside a transaction (a per-transaction state).
	/// A subspace returned by a handler is reported only when it escapes: used in a later transaction, returned by the method, or stored in a field or a property.</para>
	/// </remarks>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class SubspaceLifetimeAnalyzer : DiagnosticAnalyzer
	{

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(FdbDescriptors.SubspaceStoredAcrossTransactions, FdbDescriptors.SubspaceReturnedFromHandler);

		public override void Initialize(AnalysisContext context)
		{
			context.EnableConcurrentExecution();
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.RegisterCompilationStartAction(start =>
			{
				var symbols = FdbSymbols.TryCreate(start.Compilation);
				if (symbols?.IKeySubspace is null) return;
				var state = new State();

				start.RegisterSymbolAction(ctx => CollectLayerState((INamedTypeSymbol) ctx.Symbol, symbols, state), SymbolKind.NamedType);
				start.RegisterSyntaxNodeAction(ctx => AnalyzeField((FieldDeclarationSyntax) ctx.Node, ctx.SemanticModel, symbols, state), SyntaxKind.FieldDeclaration);
				start.RegisterSyntaxNodeAction(ctx => AnalyzeProperty((PropertyDeclarationSyntax) ctx.Node, ctx.SemanticModel, symbols, state), SyntaxKind.PropertyDeclaration);
				start.RegisterOperationAction(ctx => AnalyzeInvocation((IInvocationOperation) ctx.Operation, symbols, state), OperationKind.Invocation);
				start.RegisterOperationAction(ctx => AnalyzeCreation((IObjectCreationOperation) ctx.Operation, ctx.ContainingSymbol, symbols, state), OperationKind.ObjectCreation);

				start.RegisterCompilationEndAction(end =>
				{
					foreach (var (member, type, location) in state.Members.OrderBy(m => m.Location.SourceSpan.Start))
					{
						if (!IsSubspaceLike(type, symbols, state)) continue;
						if (member.ContainingType is { } owner && (state.LayerStates.ContainsKey(owner) || IsTransactionScoped(owner, state))) continue;
						end.ReportDiagnostic(Diagnostic.Create(FdbDescriptors.SubspaceStoredAcrossTransactions, location, member.Name));
					}
					foreach (var (name, type, location) in state.Registrations.OrderBy(r => r.Location.SourceSpan.Start))
					{
						if (!IsSubspaceLike(type, symbols, state)) continue;
						end.ReportDiagnostic(Diagnostic.Create(FdbDescriptors.SubspaceStoredAcrossTransactions, location, name));
					}
					foreach (var (type, location) in state.Results.OrderBy(r => r.Location.SourceSpan.Start))
					{
						if (!IsSubspaceLike(type, symbols, state)) continue;
						end.ReportDiagnostic(Diagnostic.Create(FdbDescriptors.SubspaceReturnedFromHandler, location));
					}
				});
			});
		}

		private sealed class State
		{
			/// <summary>TState of every IFdbLayer implementation of the compilation.</summary>
			public readonly ConcurrentDictionary<INamedTypeSymbol, byte> LayerStates = new(SymbolEqualityComparer.Default);
			public readonly ConcurrentBag<(ISymbol Member, ITypeSymbol Type, Location Location)> Members = new();
			public readonly ConcurrentBag<(string Name, ITypeSymbol Type, Location Location)> Registrations = new();
			public readonly ConcurrentBag<(ITypeSymbol Type, Location Location)> Results = new();
			/// <summary>Types declared in the compilation that it creates, mapped to true while every creation seen is inside a transaction.</summary>
			public readonly ConcurrentDictionary<INamedTypeSymbol, bool> Creations = new(SymbolEqualityComparer.Default);
		}

		/// <summary>Every creation of the type is inside a transaction: an instance lives as long as the transaction, like the state of a layer.</summary>
		private static bool IsTransactionScoped(INamedTypeSymbol type, State state) => state.Creations.TryGetValue(type, out var scoped) && scoped;

		/// <summary>The type is a subspace, or the state of a layer implemented in the compilation.</summary>
		private static bool IsSubspaceLike(ITypeSymbol type, FdbSymbols symbols, State state)
			=> FdbSymbols.IsOrImplements(type, symbols.IKeySubspace)
				|| FdbSymbols.IsOrImplements(type, symbols.FdbDirectorySubspace)
				|| (type is INamedTypeSymbol named && state.LayerStates.ContainsKey(named));

		/// <summary>The type is a subspace, or a type declared in the compilation, which the compilation end may recognize as a layer state.</summary>
		private static bool IsCandidateType(ITypeSymbol? type, FdbSymbols symbols)
		{
			if (type is null) return false;
			if (type.NullableAnnotation == NullableAnnotation.Annotated) type = type.WithNullableAnnotation(NullableAnnotation.None);
			return FdbSymbols.IsOrImplements(type, symbols.IKeySubspace)
				|| FdbSymbols.IsOrImplements(type, symbols.FdbDirectorySubspace)
				|| (type is INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct } named && named.Locations.Any(l => l.IsInSource));
		}

		private static void CollectLayerState(INamedTypeSymbol type, FdbSymbols symbols, State state)
		{
			foreach (var iface in type.AllInterfaces)
			{
				var definition = iface.OriginalDefinition;
				if (!SymbolEqualityComparer.Default.Equals(definition, symbols.IFdbLayer1) && !SymbolEqualityComparer.Default.Equals(definition, symbols.IFdbLayer2)) continue;
				if (iface.TypeArguments[0] is INamedTypeSymbol layerState) state.LayerStates.TryAdd(layerState, 0);
			}
		}

		private static void AnalyzeField(FieldDeclarationSyntax declaration, SemanticModel model, FdbSymbols symbols, State state)
		{
			foreach (var declarator in declaration.Declaration.Variables)
			{
				if (model.GetDeclaredSymbol(declarator) is not IFieldSymbol field || field.IsConst || !IsCandidateType(field.Type, symbols)) continue;
				if (IsReadOnlyStruct(field.ContainingType) || IsManualPrefix(declarator.Initializer, model, symbols)) continue;
				state.Members.Add((field, field.Type, declarator.Identifier.GetLocation()));
			}
		}

		private static void AnalyzeProperty(PropertyDeclarationSyntax declaration, SemanticModel model, FdbSymbols symbols, State state)
		{
			// an auto-property: every accessor has neither a body nor an expression body
			if (declaration.ExpressionBody is not null || declaration.AccessorList is not { } accessors) return;
			if (accessors.Accessors.Any(a => a.Body is not null || a.ExpressionBody is not null)) return;
			if (model.GetDeclaredSymbol(declaration) is not { } property || !IsCandidateType(property.Type, symbols)) return;
			// an interface or abstract property declares a member without storing anything
			if (property.IsAbstract || property.ContainingType.TypeKind == TypeKind.Interface) return;
			if (IsReadOnlyStruct(property.ContainingType) || IsManualPrefix(declaration.Initializer, model, symbols)) return;
			state.Members.Add((property, property.Type, declaration.Identifier.GetLocation()));
		}

		/// <summary>A readonly struct is a value (a key, a range) that holds its subspace by design; a field that stores the struct is reported for its own type.</summary>
		private static bool IsReadOnlyStruct(INamedTypeSymbol? type) => type is { IsReadOnly: true, TypeKind: TypeKind.Struct };

		/// <summary>The member is initialized with KeySubspace.FromKey(...) or new KeySubspace(...), a fixed prefix that the directory layer never moves.</summary>
		private static bool IsManualPrefix(EqualsValueClauseSyntax? initializer, SemanticModel model, FdbSymbols symbols)
		{
			if (initializer is null) return false;
			if (model.GetSymbolInfo(initializer.Value).Symbol is IMethodSymbol { Name: "FromKey" } method && SymbolEqualityComparer.Default.Equals(method.ContainingType, symbols.KeySubspace)) return true;
			var value = model.GetOperation(initializer.Value);
			while (value is IConversionOperation conversion) value = conversion.Operand;
			return value is IObjectCreationOperation { Type: { } created } && SymbolEqualityComparer.Default.Equals(created, symbols.KeySubspace);
		}

		private static void AnalyzeCreation(IObjectCreationOperation op, ISymbol containingSymbol, FdbSymbols symbols, State state)
		{
			if (op.Type is not INamedTypeSymbol type || !type.Locations.Any(l => l.IsInSource)) return;
			bool scoped = IsInsideTransaction(op, containingSymbol, symbols);
			state.Creations.AddOrUpdate(type, scoped, (_, previous) => previous && scoped);
		}

		/// <summary>The operation runs inside a lambda, a local function or a method that receives a transaction.</summary>
		private static bool IsInsideTransaction(IOperation op, ISymbol containingSymbol, FdbSymbols symbols)
		{
			for (var current = op.Parent; current is not null; current = current.Parent)
			{
				var parameters = current switch
				{
					IAnonymousFunctionOperation lambda => lambda.Symbol.Parameters,
					ILocalFunctionOperation local => local.Symbol.Parameters,
					_ => ImmutableArray<IParameterSymbol>.Empty,
				};
				if (parameters.Any(p => IsTransaction(p.Type, symbols))) return true;
			}
			return containingSymbol is IMethodSymbol method && method.Parameters.Any(p => IsTransaction(p.Type, symbols));
		}

		private static bool IsTransaction(ITypeSymbol? type, FdbSymbols symbols) => FdbSymbols.IsOrImplements(type, symbols.IFdbReadOnlyTransaction);

		/// <summary>The result of the retry loop leaves the transaction: it is returned by the method, stored in a field or a property, or used in a later transaction.</summary>
		/// <remarks>A result that is only read outside a transaction (logged, printed, compared) cannot point at a stale prefix in a later one.</remarks>
		private static bool Escapes(IInvocationOperation op, FdbSymbols symbols)
		{
			IOperation current = op;
			while (current.Parent is IAwaitOperation or IConversionOperation || current.Parent is IInvocationOperation { TargetMethod.Name: "ConfigureAwait" } configure && configure.Instance == current)
			{
				current = current.Parent!;
			}

			ILocalSymbol? local = null;
			switch (current.Parent)
			{
				case IReturnOperation:
					return true;
				case IArgumentOperation { Parent: IInvocationOperation call }:
					return TakesTransaction(call, symbols);
				case ISimpleAssignmentOperation assignment when assignment.Value == current:
					if (assignment.Target is IFieldReferenceOperation or IPropertyReferenceOperation) return true;
					local = (assignment.Target as ILocalReferenceOperation)?.Local;
					break;
				case IVariableInitializerOperation { Parent: IVariableDeclaratorOperation declarator }:
					local = declarator.Symbol;
					break;
			}
			if (local is null) return false;

			var root = current;
			while (root.Parent is not null) root = root.Parent;
			foreach (var reference in root.Descendants().OfType<ILocalReferenceOperation>())
			{
				if (!SymbolEqualityComparer.Default.Equals(reference.Local, local)) continue;
				IOperation use = reference;
				while (use.Parent is IConversionOperation) use = use.Parent;
				if (use.Parent is IReturnOperation) return true;
				if (use.Parent is ISimpleAssignmentOperation { Target: IFieldReferenceOperation or IPropertyReferenceOperation } store && store.Value == use) return true;
				for (var parent = reference.Parent; parent is not null; parent = parent.Parent)
				{
					if (parent is IAnonymousFunctionOperation lambda && symbols.IsRetryLoopHandler(lambda)) return true;
					if (parent is IInvocationOperation invocation && TakesTransaction(invocation, symbols)) return true;
				}
			}
			return false;
		}

		/// <summary>The invocation runs on a transaction, or receives one as an argument.</summary>
		private static bool TakesTransaction(IInvocationOperation invocation, FdbSymbols symbols)
			=> IsTransaction(FdbSymbols.GetReceiver(invocation)?.Type, symbols) || invocation.Arguments.Any(a => IsTransaction(a.Value.Type, symbols));

		private static void AnalyzeInvocation(IInvocationOperation op, FdbSymbols symbols, State state)
		{
			var method = op.TargetMethod;
			if (method.Name == "AddSingleton" && method.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.Extensions.DependencyInjection"))
			{
				if (op.Syntax is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name: { } name } }) return;
				foreach (var type in method.TypeArguments)
				{
					if (IsCandidateType(type, symbols)) state.Registrations.Add((name.ToString(), type, name.GetLocation()));
				}
				return;
			}

			if (method.Name is not ("ReadAsync" or "ReadWriteAsync") || !symbols.IsRetryLoopInvocation(op)) return;
			ITypeSymbol? result = null;
			for (int i = 0; i < method.TypeParameters.Length; i++)
			{
				if (method.TypeParameters[i].Name == "TResult") result = method.TypeArguments[i];
			}
			if (result is null || !IsCandidateType(result, symbols)) return;
			if (!Escapes(op, symbols)) return;

			var handler = op.Arguments.FirstOrDefault(a => a.Parameter?.Name == "handler")?.Value;
			while (handler is IDelegateCreationOperation or IConversionOperation) handler = handler is IDelegateCreationOperation d ? d.Target : ((IConversionOperation) handler).Operand;
			if (handler is IAnonymousFunctionOperation lambda)
			{
				bool found = false;
				foreach (var returned in lambda.Body.Descendants().OfType<IReturnOperation>())
				{
					if (returned.ReturnedValue is null || FdbSymbols.GetEnclosingLambda(returned) != lambda) continue;
					state.Results.Add((result, returned.ReturnedValue.Syntax.GetLocation()));
					found = true;
				}
				if (found) return;
			}
			state.Results.Add((result, op.Syntax.GetLocation()));
		}

	}
}
