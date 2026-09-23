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

	/// <summary>Reports a Slice decoded through ToArray() or GetBytes(), and text encoded to a Slice through a temporary byte array (SBK2002).</summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class SliceRoundTripAnalyzer : DiagnosticAnalyzer
	{

		/// <summary>Property of the diagnostic: the expression that replaces the reported one.</summary>
		public const string ReplacementProperty = "replacement";

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(SbkDescriptors.SliceRoundTrip);

		public override void Initialize(AnalysisContext context)
		{
			context.EnableConcurrentExecution();
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.RegisterCompilationStartAction(start =>
			{
				var slice = start.Compilation.GetTypeByMetadataName("System.Slice");
				var encoding = start.Compilation.GetTypeByMetadataName("System.Text.Encoding");
				var bitConverter = start.Compilation.GetTypeByMetadataName("System.BitConverter");
				if (slice is null || encoding is null || bitConverter is null) return;

				start.RegisterOperationAction(ctx =>
				{
					var op = (IInvocationOperation) ctx.Operation;
					var replacement = GetReplacement(op, slice, encoding, bitConverter);
					if (replacement is null) return;
					var (method, text) = replacement.Value;
					var properties = ImmutableDictionary<string, string?>.Empty;
					// the fix is offered only when the target framework of the consumer has the replacement member
					if (!slice.GetMembers(method).IsEmpty) properties = properties.Add(ReplacementProperty, text);
					ctx.ReportDiagnostic(Diagnostic.Create(SbkDescriptors.SliceRoundTrip, op.Syntax.GetLocation(), properties, op.Syntax.ToString(), text));
				}, OperationKind.Invocation);
			});
		}

		/// <summary>The Slice member that replaces the call and the replacement text, or null for any other call.</summary>
		private static (string Method, string Text)? GetReplacement(IInvocationOperation op, INamedTypeSymbol slice, INamedTypeSymbol encoding, INamedTypeSymbol bitConverter)
		{
			var method = op.TargetMethod;
			var owner = method.ContainingType;
			var arguments = op.Arguments;

			// Encoding.UTF8.GetString(s.ToArray())
			if (method.Name == "GetString" && arguments.Length == 1 && IsUtf8(op.Instance, encoding))
			{
				return GetCopiedSlice(arguments[0].Value, slice) is { } source ? ("ToStringUtf8", $"{source.Syntax}.ToStringUtf8()") : null;
			}

			// BitConverter.ToInt64(s.ToArray(), 0)
			if (method.Name == "ToInt64" && arguments.Length == 2 && SymbolEqualityComparer.Default.Equals(owner, bitConverter) && IsZero(arguments[1].Value))
			{
				return GetCopiedSlice(arguments[0].Value, slice) is { } source ? ("ToInt64", $"{source.Syntax}.ToInt64()") : null;
			}

			// Encoding.UTF8.GetBytes(text).AsSlice()
			if (method.Name == "AsSlice" && method.IsExtensionMethod && arguments.Length == 1 && SymbolEqualityComparer.Default.Equals(owner.ContainingAssembly, slice.ContainingAssembly))
			{
				return GetUtf8Text(arguments[0].Value, encoding) is { } text ? ("FromStringUtf8", $"Slice.FromStringUtf8({text.Syntax})") : null;
			}

			// Slice.Copy(Encoding.UTF8.GetBytes(text)), Slice.FromBytes(Encoding.UTF8.GetBytes(text))
			if (method.Name is "Copy" or "FromBytes" && method.IsStatic && arguments.Length == 1 && SymbolEqualityComparer.Default.Equals(owner, slice))
			{
				if (GetUtf8Text(arguments[0].Value, encoding) is not { } text) return null;
				string receiver = op.Syntax is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } ? access.Expression.ToString() : "Slice";
				return ("FromStringUtf8", $"{receiver}.FromStringUtf8({text.Syntax})");
			}

			return null;
		}

		/// <summary>The Slice that s.ToArray() or s.GetBytes() copies, or null.</summary>
		private static IOperation? GetCopiedSlice(IOperation argument, INamedTypeSymbol slice)
			=> Unwrap(argument) is IInvocationOperation { TargetMethod: { Name: "ToArray" or "GetBytes", Parameters.IsEmpty: true }, Instance: { } instance } && SymbolEqualityComparer.Default.Equals(instance.Type, slice)
				? instance
				: null;

		/// <summary>The string that Encoding.UTF8.GetBytes(text) encodes, or null.</summary>
		private static IOperation? GetUtf8Text(IOperation argument, INamedTypeSymbol encoding)
			=> Unwrap(argument) is IInvocationOperation { TargetMethod.Name: "GetBytes", Arguments: { Length: 1 } arguments } call
				&& IsUtf8(call.Instance, encoding)
				&& arguments[0].Value.Type?.SpecialType == SpecialType.System_String
				? arguments[0].Value
				: null;

		private static bool IsUtf8(IOperation? instance, INamedTypeSymbol encoding)
			=> instance is IPropertyReferenceOperation { Property: { IsStatic: true, Name: "UTF8" } property } && SymbolEqualityComparer.Default.Equals(property.ContainingType, encoding);

		private static bool IsZero(IOperation op) => Unwrap(op).ConstantValue is { HasValue: true, Value: 0 };

		private static IOperation Unwrap(IOperation op)
		{
			while (op is IConversionOperation { IsImplicit: true } conversion) op = conversion.Operand;
			return op;
		}

	}
}
