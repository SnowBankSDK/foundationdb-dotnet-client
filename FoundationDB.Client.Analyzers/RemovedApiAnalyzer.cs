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
	using System.Collections.Generic;
	using System.Collections.Immutable;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp;
	using Microsoft.CodeAnalysis.CSharp.Syntax;
	using Microsoft.CodeAnalysis.Diagnostics;

	/// <summary>Reports a member that version 7 removed, next to the compiler error, with its replacement (FDB0100).</summary>
	/// <remarks>The rule matches a member access or an element access that failed to bind while its receiver bound to one of the known types.</remarks>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class RemovedApiAnalyzer : DiagnosticAnalyzer
	{

		/// <summary>Property of the diagnostic: the new member name for a rename, <see cref="RangeRewrite"/>, or <see cref="IndexerRewrite"/>. Absent when there is no code fix.</summary>
		public const string ReplacementProperty = "replacement";

		/// <summary>Replacement value of EncodeRange(a): Key(a).ToRange().</summary>
		public const string RangeRewrite = "range";

		/// <summary>Replacement value of the indexer subspace[a, b]: subspace.Key(a, b).</summary>
		public const string IndexerRewrite = "indexer";

		private const string IndexerName = "subspace[...]";

		private sealed class Table
		{
			public Table(INamedTypeSymbol type, Dictionary<string, (string Advice, string? Fix)> members)
			{
				this.Type = type;
				this.Members = members;
			}

			public INamedTypeSymbol Type { get; }

			public Dictionary<string, (string Advice, string? Fix)> Members { get; }
		}

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(FdbDescriptors.RemovedApi);

		public override void Initialize(AnalysisContext context)
		{
			context.EnableConcurrentExecution();
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.RegisterCompilationStartAction(start =>
			{
				var symbols = FdbSymbols.TryCreate(start.Compilation);
				if (symbols is null) return;
				var tables = new List<Table>();
				Add(tables, symbols.IKeySubspace, new()
				{
					["Pack"] = ("Use subspace.Key(...) instead.", "Key"),
					["Encode"] = ("Use subspace.Key(...) instead.", "Key"),
					["EncodeRange"] = ("Use subspace.Key(...).ToRange() instead.", RangeRewrite),
					["Partition"] = ("Use subspace.Key(x).ToSubspace() instead.", null),
					["ByKey"] = ("Use subspace.Key(a, b) instead.", "Key"),
					[IndexerName] = ("Use subspace.Key(a, b) instead.", IndexerRewrite),
				});
				Add(tables, symbols.IFdbDatabase, new()
				{
					["Directory"] = ("Use db.Root[...] or db.DirectoryLayer instead.", null),
				});
				Add(tables, symbols.IFdbDatabaseScopeProvider, new()
				{
					["GetDatabaseAsync"] = ("Use GetDatabase(ct), or call ReadAsync, WriteAsync, or ReadWriteAsync on the provider directly.", "GetDatabase"),
				});
				Add(tables, symbols.ISubspaceLocation, new()
				{
					["ResolveAsync"] = ("Use Resolve(tr) instead.", "Resolve"),
				});
				Add(tables, symbols.KeySelector, new()
				{
					["FirstAfter"] = ("Use KeySelector.FirstGreaterThan(key) instead.", "FirstGreaterThan"),
				});

				start.RegisterSyntaxNodeAction(ctx =>
				{
					var node = (MemberAccessExpressionSyntax) ctx.Node;
					if (IsBound(node, ctx)) return;
					Report(ctx, node.Expression, node.Name.Identifier.ValueText, node.Name.GetLocation(), tables);
				}, SyntaxKind.SimpleMemberAccessExpression);

				start.RegisterSyntaxNodeAction(ctx =>
				{
					var node = (ElementAccessExpressionSyntax) ctx.Node;
					if (IsBound(node, ctx)) return;
					Report(ctx, node.Expression, IndexerName, node.GetLocation(), tables);
				}, SyntaxKind.ElementAccessExpression);
			});
		}

		private static void Add(List<Table> tables, INamedTypeSymbol? type, Dictionary<string, (string Advice, string? Fix)> members)
		{
			if (type is not null) tables.Add(new Table(type, members));
		}

		/// <summary>The node binds to a member, or names an existing member with the wrong arguments.</summary>
		private static bool IsBound(ExpressionSyntax node, SyntaxNodeAnalysisContext ctx)
		{
			var info = ctx.SemanticModel.GetSymbolInfo(node, ctx.CancellationToken);
			return info.Symbol is not null || !info.CandidateSymbols.IsEmpty;
		}

		private static void Report(SyntaxNodeAnalysisContext ctx, ExpressionSyntax receiver, string name, Location location, List<Table> tables)
		{
			var model = ctx.SemanticModel;
			ITypeSymbol? type = model.GetTypeInfo(receiver, ctx.CancellationToken).Type;
			// a static receiver is a type name, which binds to a symbol rather than to a type
			if (type is null or IErrorTypeSymbol) type = model.GetSymbolInfo(receiver, ctx.CancellationToken).Symbol as ITypeSymbol;
			if (type is null or IErrorTypeSymbol) return;

			foreach (var table in tables)
			{
				if (!table.Members.TryGetValue(name, out var entry) || !FdbSymbols.IsOrImplements(type, table.Type)) continue;
				var properties = ImmutableDictionary<string, string?>.Empty;
				if (entry.Fix is not null) properties = properties.Add(ReplacementProperty, entry.Fix);
				ctx.ReportDiagnostic(Diagnostic.Create(FdbDescriptors.RemovedApi, location, properties, name, entry.Advice));
				return;
			}
		}

	}
}
