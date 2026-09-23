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

namespace SnowBank.Analyzers.Tests
{
	using Microsoft.CodeAnalysis.CodeFixes;
	using Microsoft.CodeAnalysis.CSharp.Testing;
	using Microsoft.CodeAnalysis.Diagnostics;

	/// <summary>Runs an analyzer or a code fix over C# snippets compiled against the real libraries.</summary>
	internal static class Verify
	{

		public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor) => new(descriptor);

		public static Task Analyzer<TAnalyzer>(string source, params DiagnosticResult[] expected)
			where TAnalyzer : DiagnosticAnalyzer, new()
			=> Analyzer<TAnalyzer>(source, TestReferences.Net100, OutputKind.DynamicallyLinkedLibrary, expected);

		public static Task Analyzer<TAnalyzer>(string source, ReferenceAssemblies references, OutputKind outputKind, params DiagnosticResult[] expected)
			where TAnalyzer : DiagnosticAnalyzer, new()
		{
			var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
			{
				TestCode = source,
				ReferenceAssemblies = references,
			};
			test.TestState.OutputKind = outputKind;
			test.TestState.AdditionalReferences.AddRange(TestReferences.Libraries);
			test.ExpectedDiagnostics.AddRange(expected);
			return test.RunAsync();
		}

		public static Task CodeFix<TAnalyzer, TFix>(string source, string fixedSource, params DiagnosticResult[] expected)
			where TAnalyzer : DiagnosticAnalyzer, new()
			where TFix : CodeFixProvider, new()
			=> CodeFix<TAnalyzer, TFix>(source, fixedSource, TestReferences.Net100, OutputKind.DynamicallyLinkedLibrary, expected);

		public static Task CodeFix<TAnalyzer, TFix>(string source, string fixedSource, ReferenceAssemblies references, OutputKind outputKind, params DiagnosticResult[] expected)
			where TAnalyzer : DiagnosticAnalyzer, new()
			where TFix : CodeFixProvider, new()
			=> CodeFix<TAnalyzer, TFix>(source, fixedSource, references, outputKind, CodeFixTestBehaviors.None, expected);

		/// <summary>Code fix of a compilation-end diagnostic: the testing library rejects a fix for a non-local diagnostic unless told otherwise.</summary>
		public static Task CodeFixAtCompilationEnd<TAnalyzer, TFix>(string source, string fixedSource, ReferenceAssemblies references, OutputKind outputKind, params DiagnosticResult[] expected)
			where TAnalyzer : DiagnosticAnalyzer, new()
			where TFix : CodeFixProvider, new()
			=> CodeFix<TAnalyzer, TFix>(source, fixedSource, references, outputKind, CodeFixTestBehaviors.SkipLocalDiagnosticCheck, expected);

		private static Task CodeFix<TAnalyzer, TFix>(string source, string fixedSource, ReferenceAssemblies references, OutputKind outputKind, CodeFixTestBehaviors behaviors, DiagnosticResult[] expected)
			where TAnalyzer : DiagnosticAnalyzer, new()
			where TFix : CodeFixProvider, new()
		{
			var test = new CSharpCodeFixTest<TAnalyzer, TFix, DefaultVerifier>
			{
				TestCode = source,
				FixedCode = fixedSource,
				ReferenceAssemblies = references,
				CodeFixTestBehaviors = behaviors,
			};
			// FixedState inherits the references and output kind of TestState (default inheritance mode).
			test.TestState.OutputKind = outputKind;
			test.TestState.AdditionalReferences.AddRange(TestReferences.Libraries);
			test.ExpectedDiagnostics.AddRange(expected);
			return test.RunAsync();
		}

	}
}
