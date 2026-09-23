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
	using System.Linq;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.Diagnostics;
	using Microsoft.CodeAnalysis.Operations;

	/// <summary>Reports a transaction write whose value comes from a Core factory (FDB2001), and a typed key serialized with ToSlice() before a transaction call (FDB2002).</summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class EncodingPerformanceAnalyzer : DiagnosticAnalyzer
	{

		/// <summary>Property of an FDB2001 diagnostic: the FdbValue expression that replaces the factory call, present when the code fix can rewrite it.</summary>
		public const string ReplacementProperty = "replacement";

		/// <summary>Transaction methods whose value argument the FDB2001 rule inspects.</summary>
		private static readonly ImmutableHashSet<string> WriteMethods = ImmutableHashSet.Create("Set", "SetVersionStampedValue", "AtomicAdd", "AtomicMax", "AtomicMin");

		/// <summary>Transaction methods whose key arguments the FDB2002 rule inspects (plus every Atomic* method).</summary>
		private static readonly ImmutableHashSet<string> KeyMethods = ImmutableHashSet.Create("GetAsync", "Set", "Clear", "ClearRange", "GetRange", "Watch", "AddConflictRange", "SetVersionStampedKey");

		/// <summary>Parameters of the key methods that receive a key.</summary>
		private static readonly ImmutableHashSet<string> KeyParameters = ImmutableHashSet.Create("key", "beginKeyInclusive", "endKeyExclusive");

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(FdbDescriptors.IntermediateValueBuffer, FdbDescriptors.EagerKeySlice);

		public override void Initialize(AnalysisContext context)
		{
			context.EnableConcurrentExecution();
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.RegisterCompilationStartAction(start =>
			{
				var symbols = FdbSymbols.TryCreate(start.Compilation);
				if (symbols is null || symbols.FdbValue is null) return;
				start.RegisterOperationAction(ctx =>
				{
					var op = (IInvocationOperation) ctx.Operation;
					if (FdbSymbols.GetReceiver(op) is not { } receiver || !FdbSymbols.IsOrImplements(receiver.Type, symbols.IFdbReadOnlyTransaction)) return;
					string name = op.TargetMethod.Name;
					if (WriteMethods.Contains(name)) AnalyzeWrite(ctx, op, symbols);
					if (KeyMethods.Contains(name) || name.StartsWith("Atomic", System.StringComparison.Ordinal)) AnalyzeKeys(ctx, op, symbols);
				}, OperationKind.Invocation);
			});
		}

		#region FDB2001...

		private static void AnalyzeWrite(OperationAnalysisContext ctx, IInvocationOperation op, FdbSymbols symbols)
		{
			var argument = op.Arguments.FirstOrDefault(a => a.Parameter?.Name == "value");
			if (argument is null) return;
			var source = FdbSymbols.Unwrap(argument.Value);
			if (source is ILocalReferenceOperation local)
			{
				// a Slice local reused by several writes amortizes its buffer, so only a single reference is reported
				if (FdbSymbols.GetLocalInitializer(local.Local, op.SemanticModel) is not { } initializer || CountReferences(op, local.Local) > 1) return;
				source = FdbSymbols.Unwrap(initializer);
			}
			var match = GetReplacement(source, symbols);
			if (match is null) return;
			var (method, arguments) = match.Value;

			string replacement = $"FdbValue.{method}({arguments})";
			var properties = ImmutableDictionary<string, string?>.Empty;
			// a consumer that compiles against the netstandard2.0 build has no ToUuid128, so the fix is offered only when the member exists
			if (!symbols.FdbValue!.GetMembers(method).IsEmpty) properties = properties.Add(ReplacementProperty, replacement);
			ctx.ReportDiagnostic(Diagnostic.Create(FdbDescriptors.IntermediateValueBuffer, argument.Value.Syntax.GetLocation(), properties, source.Syntax.ToString(), replacement));
		}

		/// <summary>The FdbValue method and its argument text that replace a Core factory call, or null for any other expression.</summary>
		private static (string Method, string Arguments)? GetReplacement(IOperation source, FdbSymbols symbols)
		{
			if (source is not IInvocationOperation call) return null;
			var method = call.TargetMethod;
			var type = method.ContainingType;
			// optional parameters left to their default (CrystalJson settings) are not written by the caller
			var arguments = call.Arguments.Where(a => a.ArgumentKind == ArgumentKind.Explicit).ToArray();

			if (arguments.Length == 1)
			{
				string text = arguments[0].Value.Syntax.ToString();
				if (SymbolEqualityComparer.Default.Equals(type, symbols.Slice))
				{
					switch (method.Name)
					{
						case "FromString" or "FromStringUtf8" or "FromStringAscii": return ("ToTextUtf8", text);
						case "FromFixed64": return ("ToFixed64LittleEndian", text);
						case "FromGuid": return ("ToUuid128", text);
					}
				}
				if (SymbolEqualityComparer.Default.Equals(type, symbols.CrystalJson) && method.Name is "ToSlice" or "ToBytes") return ("ToJson", text);
				if (method.Name == "GetBytes"
					&& SymbolEqualityComparer.Default.Equals(type, symbols.Encoding)
					&& call.Instance is IPropertyReferenceOperation { Property: { IsStatic: true, Name: "UTF8" } utf8 }
					&& SymbolEqualityComparer.Default.Equals(utf8.ContainingType, symbols.Encoding))
				{
					return ("ToTextUtf8", text);
				}
				return null;
			}

			// bytes.AsSpan(start, length).ToArray() copies the chunk; FdbValue.ToBytes(bytes, start, length) wraps it
			if (arguments.Length == 0
				&& method.Name == "ToArray"
				&& call.Instance is IInvocationOperation { TargetMethod.Name: "AsSpan", Arguments: { Length: 3 } span } asSpan
				&& SymbolEqualityComparer.Default.Equals(asSpan.TargetMethod.ContainingType, symbols.MemoryExtensions)
				&& span[0].Value.Type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte })
			{
				return ("ToBytes", $"{span[0].Value.Syntax}, {span[1].Value.Syntax}, {span[2].Value.Syntax}");
			}
			return null;
		}

		#endregion

		#region FDB2002...

		private static void AnalyzeKeys(OperationAnalysisContext ctx, IInvocationOperation op, FdbSymbols symbols)
		{
			foreach (var argument in op.Arguments)
			{
				if (argument.Parameter is null || !KeyParameters.Contains(argument.Parameter.Name)) continue;
				var value = FdbSymbols.Unwrap(argument.Value);
				IInvocationOperation? call;
				if (value is ILocalReferenceOperation local)
				{
					call = FdbSymbols.GetLocalInitializer(local.Local, op.SemanticModel) is { } initializer ? AsToSlice(FdbSymbols.Unwrap(initializer), symbols) : null;
					// every use of the local must be a key argument: a logged or stored key keeps its Slice
					if (call is null || !EnumerateReferences(op, local.Local).All(r => IsKeyArgument(r, symbols))) continue;
				}
				else
				{
					call = AsToSlice(value, symbols);
					if (call is null) continue;
				}
				var key = FdbSymbols.GetReceiver(call);
				if (key is null) continue;
				ctx.ReportDiagnostic(Diagnostic.Create(FdbDescriptors.EagerKeySlice, argument.Value.Syntax.GetLocation(), key.Syntax.ToString()));
			}
		}

		/// <summary>The operation as a parameterless ToSlice() call on a typed key (the FdbKeyExtensions method or a key struct's own), or null.</summary>
		private static IInvocationOperation? AsToSlice(IOperation op, FdbSymbols symbols)
		{
			if (op is not IInvocationOperation { TargetMethod: { Name: "ToSlice" } method } call) return null;
			var type = method.ContainingType;
			// the extension form keeps the key as its first argument; the pooled overload takes a second one
			bool typedKey = method.IsExtensionMethod
				? call.Arguments.Length == 1 && SymbolEqualityComparer.Default.Equals(type, symbols.FdbKeyExtensions)
				: method.Parameters.IsEmpty && type.TypeKind == TypeKind.Struct && FdbSymbols.IsOrImplements(type, symbols.IFdbKey);
			return typedKey ? call : null;
		}

		/// <summary>The reference is a key argument of one of the transaction methods the rule covers.</summary>
		private static bool IsKeyArgument(ILocalReferenceOperation reference, FdbSymbols symbols)
		{
			IOperation? parent = reference.Parent;
			while (parent is IConversionOperation) parent = parent.Parent;
			return parent is IArgumentOperation { Parameter: { } parameter, Parent: IInvocationOperation invocation }
				&& KeyParameters.Contains(parameter.Name)
				&& (KeyMethods.Contains(invocation.TargetMethod.Name) || invocation.TargetMethod.Name.StartsWith("Atomic", System.StringComparison.Ordinal))
				&& FdbSymbols.GetReceiver(invocation) is { } receiver
				&& FdbSymbols.IsOrImplements(receiver.Type, symbols.IFdbReadOnlyTransaction);
		}

		#endregion

		private static int CountReferences(IOperation op, ILocalSymbol local) => EnumerateReferences(op, local).Count();

		/// <summary>Every reference to the local in the enclosing method or lambda body.</summary>
		private static IEnumerable<ILocalReferenceOperation> EnumerateReferences(IOperation op, ILocalSymbol local)
		{
			var root = op;
			while (root.Parent is not null) root = root.Parent;
			return root.Descendants().OfType<ILocalReferenceOperation>().Where(r => SymbolEqualityComparer.Default.Equals(r.Local, local));
		}

	}
}
