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
	using System.Threading;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.Diagnostics;
	using Microsoft.CodeAnalysis.Operations;

	/// <summary>Reports IFdbDatabase requested from dependency injection in a compilation that calls AddFoundationDb and registers no IFdbDatabase.</summary>
	/// <remarks>AddFoundationDb registers IFdbDatabaseProvider only. The decision needs every registration of the compilation, so the rule reports at compilation end.</remarks>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class DatabaseInjectionAnalyzer : DiagnosticAnalyzer
	{

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(FdbDescriptors.DatabaseInjection);

		public override void Initialize(AnalysisContext context)
		{
			context.EnableConcurrentExecution();
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.RegisterCompilationStartAction(start =>
			{
				var symbols = FdbSymbols.TryCreate(start.Compilation);
				if (symbols is null) return;
				var endpointExtensions = start.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Builder.EndpointRouteBuilderExtensions");
				var fromServices = start.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.FromServicesAttribute");
				var state = new State();

				start.RegisterOperationAction(ctx => AnalyzeInvocation((IInvocationOperation) ctx.Operation, symbols, state), OperationKind.Invocation);
				start.RegisterOperationAction(ctx => AnalyzeLambda((IAnonymousFunctionOperation) ctx.Operation, symbols, endpointExtensions, fromServices, state), OperationKind.AnonymousFunction);
				start.RegisterSymbolAction(ctx => AnalyzeMethod((IMethodSymbol) ctx.Symbol, symbols, fromServices, state), SymbolKind.Method);

				start.RegisterCompilationEndAction(end =>
				{
					if (Volatile.Read(ref state.CallsAddFoundationDb) == 0 || Volatile.Read(ref state.RegistersDatabase) != 0) return;
					foreach (var type in state.RegisteredTypes.Keys)
					{
						foreach (var ctor in type.InstanceConstructors)
						{
							foreach (var parameter in ctor.Parameters)
							{
								if (SymbolEqualityComparer.Default.Equals(parameter.Type, symbols.IFdbDatabase)) AddParameter(parameter, state);
							}
						}
					}
					foreach (var location in state.Candidates.Keys.OrderBy(l => l.SourceSpan.Start))
					{
						end.ReportDiagnostic(Diagnostic.Create(FdbDescriptors.DatabaseInjection, location));
					}
				});
			});
		}

		private sealed class State
		{
			public int CallsAddFoundationDb;
			public int RegistersDatabase;
			public readonly ConcurrentDictionary<Location, byte> Candidates = new();
			public readonly ConcurrentDictionary<INamedTypeSymbol, byte> RegisteredTypes = new(SymbolEqualityComparer.Default);
		}

		private static void AnalyzeInvocation(IInvocationOperation op, FdbSymbols symbols, State state)
		{
			var method = op.TargetMethod;
			var container = method.ContainingType;
			if (method.Name == "AddFoundationDb"
				&& (SymbolEqualityComparer.Default.Equals(container, symbols.ServiceCollectionExtensions) || SymbolEqualityComparer.Default.Equals(container, symbols.AspireComponentExtensions)))
			{
				Interlocked.Exchange(ref state.CallsAddFoundationDb, 1);
				return;
			}

			string containerName = container?.ToDisplayString() ?? "";
			bool isRegistration = (method.Name.StartsWith("Add") || method.Name.StartsWith("TryAdd"))
				&& containerName.StartsWith("Microsoft.Extensions.DependencyInjection.");
			if (isRegistration)
			{
				bool mentionsDatabase = method.TypeArguments.Any(t => SymbolEqualityComparer.Default.Equals(t, symbols.IFdbDatabase))
					|| op.Arguments.Any(a => a.Value is ITypeOfOperation typeOf && SymbolEqualityComparer.Default.Equals(typeOf.TypeOperand, symbols.IFdbDatabase));
				if (mentionsDatabase) Interlocked.Exchange(ref state.RegistersDatabase, 1);

				// AddSingleton<TImplementation>(), AddSingleton<TService, TImplementation>(), AddHostedService<THostedService>()
				if (method.TypeArguments.Length > 0 && method.TypeArguments[method.TypeArguments.Length - 1] is INamedTypeSymbol implementation && implementation.Locations.Any(l => l.IsInSource))
				{
					state.RegisteredTypes.TryAdd(implementation, 0);
				}
				return;
			}

			if (method.Name is "GetRequiredService" or "GetService"
				&& method.TypeArguments.Length == 1
				&& SymbolEqualityComparer.Default.Equals(method.TypeArguments[0], symbols.IFdbDatabase))
			{
				state.Candidates.TryAdd(op.Syntax.GetLocation(), 0);
			}
		}

		private static void AnalyzeLambda(IAnonymousFunctionOperation op, FdbSymbols symbols, INamedTypeSymbol? endpointExtensions, INamedTypeSymbol? fromServices, State state)
		{
			bool isEndpoint = endpointExtensions is not null && IsArgumentOfEndpointMapping(op, endpointExtensions);
			foreach (var parameter in op.Symbol.Parameters)
			{
				if (!SymbolEqualityComparer.Default.Equals(parameter.Type, symbols.IFdbDatabase)) continue;
				if (isEndpoint || HasAttribute(parameter, fromServices)) AddParameter(parameter, state);
			}
		}

		private static void AnalyzeMethod(IMethodSymbol method, FdbSymbols symbols, INamedTypeSymbol? fromServices, State state)
		{
			if (fromServices is null) return;
			foreach (var parameter in method.Parameters)
			{
				if (SymbolEqualityComparer.Default.Equals(parameter.Type, symbols.IFdbDatabase) && HasAttribute(parameter, fromServices)) AddParameter(parameter, state);
			}
		}

		private static bool IsArgumentOfEndpointMapping(IAnonymousFunctionOperation op, INamedTypeSymbol endpointExtensions)
		{
			IOperation? current = op.Parent;
			while (current is IDelegateCreationOperation or IConversionOperation) current = current.Parent;
			return current is IArgumentOperation { Parent: IInvocationOperation invocation }
				&& invocation.TargetMethod.Name.StartsWith("Map")
				&& SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, endpointExtensions);
		}

		private static bool HasAttribute(IParameterSymbol parameter, INamedTypeSymbol? attribute)
			=> attribute is not null && parameter.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attribute));

		private static void AddParameter(IParameterSymbol parameter, State state)
		{
			foreach (var reference in parameter.DeclaringSyntaxReferences)
			{
				state.Candidates.TryAdd(reference.GetSyntax().GetLocation(), 0);
			}
		}

	}
}
