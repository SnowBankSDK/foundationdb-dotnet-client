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
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.Diagnostics;
	using Microsoft.CodeAnalysis.Operations;

	/// <summary>Reports a Slice.FromStringAscii literal with a character above 0xFF (SBK0001), and a Slice.FromString or FromStringUtf8 literal that starts with a binary prefix (SBK1002).</summary>
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class SliceLiteralAnalyzer : DiagnosticAnalyzer
	{

		/// <summary>Property of the diagnostic: the Slice factory that replaces the called one.</summary>
		public const string ReplacementProperty = "replacement";

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(SbkDescriptors.AsciiLiteralNotEncodable, SbkDescriptors.BinaryPrefixAsUtf8);

		public override void Initialize(AnalysisContext context)
		{
			context.EnableConcurrentExecution();
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.RegisterCompilationStartAction(start =>
			{
				var slice = start.Compilation.GetTypeByMetadataName("System.Slice");
				if (slice is null) return;

				start.RegisterOperationAction(ctx =>
				{
					var op = (IInvocationOperation) ctx.Operation;
					var method = op.TargetMethod;
					if (!method.IsStatic || !SymbolEqualityComparer.Default.Equals(method.ContainingType, slice) || op.Arguments.Length != 1) return;
					switch (method.Name)
					{
						case "FromStringAscii":
						{
							if (GetHead(op.Arguments[0].Value) is { } text && HasCharAbove(text, 0xFF))
							{
								Report(ctx, SbkDescriptors.AsciiLiteralNotEncodable, op.Arguments[0], "FromStringUtf8");
							}
							break;
						}
						case "FromString" or "FromStringUtf8":
						{
							if (GetHead(op.Arguments[0].Value) is { Length: > 0 } text && text[0] >= 0x80 && text[0] <= 0xFF)
							{
								Report(ctx, SbkDescriptors.BinaryPrefixAsUtf8, op.Arguments[0], "FromByteString");
							}
							break;
						}
					}
				}, OperationKind.Invocation);
			});
		}

		private static void Report(OperationAnalysisContext ctx, DiagnosticDescriptor descriptor, IArgumentOperation argument, string replacement)
		{
			var properties = ImmutableDictionary<string, string?>.Empty.Add(ReplacementProperty, replacement);
			ctx.ReportDiagnostic(Diagnostic.Create(descriptor, argument.Value.Syntax.GetLocation(), properties, argument.Value.Syntax.ToString()));
		}

		/// <summary>The constant value of the argument, or the literal head of an interpolated string; null when the text is not known at compile time.</summary>
		private static string? GetHead(IOperation value)
		{
			while (value is IConversionOperation { IsImplicit: true } conversion) value = conversion.Operand;
			if (value.ConstantValue is { HasValue: true, Value: string constant }) return constant;
			if (value is IInterpolatedStringOperation { Parts.Length: > 0 } interpolated
				&& interpolated.Parts[0] is IInterpolatedStringTextOperation { Text.ConstantValue: { HasValue: true, Value: string head } })
			{
				return head;
			}
			return null;
		}

		private static bool HasCharAbove(string text, int max)
		{
			foreach (char c in text)
			{
				if (c > max) return true;
			}
			return false;
		}

	}
}
