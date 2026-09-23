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

	/// <summary>Reports a database awaited from a provider whose only uses are the retry loops and Root that the provider offers itself (FDB2003).</summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class ProviderUnwrapAnalyzer : DiagnosticAnalyzer
	{

		/// <summary>Property of an FDB2003 diagnostic: the provider expression, present when the code fix can redirect the calls to it.</summary>
		public const string ProviderProperty = "provider";

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(FdbDescriptors.ProviderUnwrapped);

		public override void Initialize(AnalysisContext context)
		{
			context.EnableConcurrentExecution();
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.RegisterCompilationStartAction(start =>
			{
				var symbols = FdbSymbols.TryCreate(start.Compilation);
				if (symbols?.IFdbDatabaseScopeProvider is null) return;
				start.RegisterOperationAction(ctx => Analyze(ctx, (IVariableDeclaratorOperation) ctx.Operation, symbols), OperationKind.VariableDeclarator);
			});
		}

		private static void Analyze(OperationAnalysisContext ctx, IVariableDeclaratorOperation declarator, FdbSymbols symbols)
		{
			if (declarator.Initializer?.Value is not { } initializer) return;
			if (FdbSymbols.Unwrap(initializer) is not IAwaitOperation { Operation: IInvocationOperation { TargetMethod: { Name: "GetDatabase" } method, Instance: { } provider } }) return;
			if (!FdbSymbols.IsOrImplements(method.ContainingType, symbols.IFdbDatabaseScopeProvider)) return;

			var root = (IOperation) declarator;
			while (root.Parent is not null) root = root.Parent;
			var references = root.Descendants().OfType<ILocalReferenceOperation>().Where(r => SymbolEqualityComparer.Default.Equals(r.Local, declarator.Symbol)).ToList();
			if (references.Count == 0 || !references.All(IsProviderCapableUse)) return;

			var properties = ImmutableDictionary<string, string?>.Empty;
			// the fix repeats the provider expression at each use, so it takes only a plain reference
			if (FdbSymbols.GetReferencedSymbol(provider) is not null) properties = properties.Add(ProviderProperty, provider.Syntax.ToString());
			ctx.ReportDiagnostic(Diagnostic.Create(FdbDescriptors.ProviderUnwrapped, initializer.Syntax.GetLocation(), properties));
		}

		/// <summary>The reference is the receiver of Root, or of a two-argument ReadAsync, WriteAsync, or ReadWriteAsync (the overloads the provider has: a state or a success callback has none).</summary>
		private static bool IsProviderCapableUse(ILocalReferenceOperation reference)
		{
			IOperation? parent = reference.Parent;
			while (parent is IConversionOperation) parent = parent.Parent;
			switch (parent)
			{
				case IPropertyReferenceOperation { Property.Name: "Root" } property:
					return property.Instance == reference;
				case IInvocationOperation { TargetMethod: { Name: "ReadAsync" or "WriteAsync" or "ReadWriteAsync" } method } invocation:
					return invocation.Instance == reference
						&& invocation.Arguments.Length == 2
						&& (method.Name != "ReadAsync" || method.IsGenericMethod);
				default:
					return false;
			}
		}

	}
}
