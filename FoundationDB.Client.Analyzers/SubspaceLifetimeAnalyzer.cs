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
	/// <remarks>The state types of the layers implemented in the compilation are known only once every type has been seen, so both rules report at compilation end.</remarks>
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

				start.RegisterCompilationEndAction(end =>
				{
					foreach (var (member, type, location) in state.Members.OrderBy(m => m.Location.SourceSpan.Start))
					{
						if (!IsSubspaceLike(type, symbols, state)) continue;
						if (member.ContainingType is { } owner && state.LayerStates.ContainsKey(owner)) continue;
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
		}

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
				if (IsKeyStruct(field.ContainingType, symbols) || IsManualPrefix(declarator.Initializer, model, symbols)) continue;
				state.Members.Add((field, field.Type, declarator.Identifier.GetLocation()));
			}
		}

		private static void AnalyzeProperty(PropertyDeclarationSyntax declaration, SemanticModel model, FdbSymbols symbols, State state)
		{
			// an auto-property: every accessor has neither a body nor an expression body
			if (declaration.ExpressionBody is not null || declaration.AccessorList is not { } accessors) return;
			if (accessors.Accessors.Any(a => a.Body is not null || a.ExpressionBody is not null)) return;
			if (model.GetDeclaredSymbol(declaration) is not { } property || !IsCandidateType(property.Type, symbols)) return;
			if (IsKeyStruct(property.ContainingType, symbols) || IsManualPrefix(declaration.Initializer, model, symbols)) return;
			state.Members.Add((property, property.Type, declaration.Identifier.GetLocation()));
		}

		/// <summary>A readonly struct that implements IFdbKey holds its subspace by design.</summary>
		private static bool IsKeyStruct(INamedTypeSymbol? type, FdbSymbols symbols)
			=> type is { IsReadOnly: true, TypeKind: TypeKind.Struct } && FdbSymbols.IsOrImplements(type, symbols.IFdbKey);

		/// <summary>The member is initialized with KeySubspace.FromKey(...), a fixed prefix that the directory layer never moves.</summary>
		private static bool IsManualPrefix(EqualsValueClauseSyntax? initializer, SemanticModel model, FdbSymbols symbols)
			=> initializer is not null
				&& model.GetSymbolInfo(initializer.Value).Symbol is IMethodSymbol { Name: "FromKey" } method
				&& SymbolEqualityComparer.Default.Equals(method.ContainingType, symbols.KeySubspace);

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
