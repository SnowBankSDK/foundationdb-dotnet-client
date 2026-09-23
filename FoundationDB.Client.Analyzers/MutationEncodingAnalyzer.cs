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
	using Microsoft.CodeAnalysis.CSharp.Syntax;
	using Microsoft.CodeAnalysis.Diagnostics;
	using Microsoft.CodeAnalysis.Operations;

	/// <summary>Reports AtomicMax or AtomicMin on a value whose encoding compares byte by byte (FDB1003), and Set on a key that holds a version stamp placeholder (FDB1006).</summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class MutationEncodingAnalyzer : DiagnosticAnalyzer
	{

		/// <summary>Property of an FDB1003 diagnostic: ByteMax or ByteMin, present when the code fix can rewrite the call to Atomic.</summary>
		public const string MutationProperty = "mutation";

		/// <summary>FdbValue factories whose encoding compares byte by byte.</summary>
		private static readonly ImmutableHashSet<string> ValueEncoders = ImmutableHashSet.Create("ToTextUtf8", "ToTextUtf16", "FromTuple", "ToJson", "ToUuid128");

		/// <summary>Slice factories whose encoding compares byte by byte.</summary>
		private static readonly ImmutableHashSet<string> SliceEncoders = ImmutableHashSet.Create("FromString", "FromStringUtf8", "FromStringAscii", "FromGuid", "FromUuid128", "FromUuid64", "FromUuid96", "FromUuid80", "FromUuid48");

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(FdbDescriptors.ByteOrderAtomic, FdbDescriptors.VersionStampedKeySet);

		public override void Initialize(AnalysisContext context)
		{
			context.EnableConcurrentExecution();
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.RegisterCompilationStartAction(start =>
			{
				var symbols = FdbSymbols.TryCreate(start.Compilation);
				if (symbols is null) return;
				start.RegisterOperationAction(ctx =>
				{
					var op = (IInvocationOperation) ctx.Operation;
					switch (op.TargetMethod.Name)
					{
						case "AtomicMax" or "AtomicMin": AnalyzeAtomic(ctx, op, symbols); break;
						case "Set": AnalyzeSet(ctx, op, symbols); break;
					}
				}, OperationKind.Invocation);
			});
		}

		private static bool IsTransactionCall(IInvocationOperation op, FdbSymbols symbols)
			=> FdbSymbols.GetReceiver(op) is { } receiver && FdbSymbols.IsOrImplements(receiver.Type, symbols.IFdbTransaction);

		private static void AnalyzeAtomic(OperationAnalysisContext ctx, IInvocationOperation op, FdbSymbols symbols)
		{
			if (!IsTransactionCall(op, symbols)) return;
			var argument = op.Arguments.FirstOrDefault(a => a.Parameter?.Name == "value");
			if (argument is null || FdbSymbols.Unwrap(argument.Value) is not IInvocationOperation call) return;
			var factory = call.TargetMethod;
			bool byteOrder = (SymbolEqualityComparer.Default.Equals(factory.ContainingType, symbols.FdbValue) && ValueEncoders.Contains(factory.Name))
				|| (SymbolEqualityComparer.Default.Equals(factory.ContainingType, symbols.Slice) && SliceEncoders.Contains(factory.Name));
			if (!byteOrder) return;

			// Atomic has no overload for a Slice key with a value that is not a Slice, so that shape gets no fix
			var key = op.Arguments.FirstOrDefault(a => a.Parameter?.Name == "key");
			bool fixable = key is not null
				&& !(SymbolEqualityComparer.Default.Equals(key.Parameter?.Type, symbols.Slice) && !SymbolEqualityComparer.Default.Equals(call.Type, symbols.Slice));
			var properties = ImmutableDictionary<string, string?>.Empty;
			if (fixable) properties = properties.Add(MutationProperty, op.TargetMethod.Name == "AtomicMax" ? "ByteMax" : "ByteMin");
			ctx.ReportDiagnostic(Diagnostic.Create(FdbDescriptors.ByteOrderAtomic, argument.Value.Syntax.GetLocation(), properties, argument.Value.Syntax.ToString()));
		}

		private static void AnalyzeSet(OperationAnalysisContext ctx, IInvocationOperation op, FdbSymbols symbols)
		{
			if (!IsTransactionCall(op, symbols)) return;
			var key = op.Arguments.FirstOrDefault(a => a.Parameter?.Name == "key");
			if (key is null || !ContainsStamp(key.Value, symbols, op.SemanticModel)) return;
			var location = op.Syntax is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } ? access.Name.GetLocation() : op.Syntax.GetLocation();
			ctx.ReportDiagnostic(Diagnostic.Create(FdbDescriptors.VersionStampedKeySet, location));
		}

		/// <summary>The expression calls CreateVersionStamp or CreateUniqueVersionStamp on a transaction, directly or through a local of the same method.</summary>
		private static bool ContainsStamp(IOperation key, FdbSymbols symbols, SemanticModel? model)
		{
			foreach (var op in key.DescendantsAndSelf())
			{
				if (op is IInvocationOperation { TargetMethod: { Name: "CreateVersionStamp" or "CreateUniqueVersionStamp" } method } && FdbSymbols.IsOrImplements(method.ContainingType, symbols.IFdbTransaction)) return true;
				if (op is ILocalReferenceOperation local && FdbSymbols.GetLocalInitializer(local.Local, model) is { } initializer && ContainsStamp(initializer, symbols, model)) return true;
			}
			return false;
		}

	}
}
