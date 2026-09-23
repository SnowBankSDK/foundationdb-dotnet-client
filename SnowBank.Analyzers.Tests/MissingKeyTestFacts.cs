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

	[TestFixture]
	public class MissingKeyTestFacts
	{

		private const string Usings = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using FoundationDB.Client;

			""";

		private static DiagnosticResult Missing(int location) => Verify.Diagnostic(FdbDescriptors.MissingKeyEmptyTest).WithLocation(location);

		[Test]
		public Task Reports_Equality_With_Empty_Through_A_Local() => Verify.Analyzer<MissingKeyTestAnalyzer>(Usings + """
			class C
			{
				async Task<bool> M(IFdbReadOnlyTransaction tr, Slice key)
				{
					var v = await tr.GetAsync(key);
					if ({|#0:v == Slice.Empty|}) return false;
					return {|#1:Slice.Empty != v|};
				}
			}
			""", Missing(0), Missing(1));

		[Test]
		public Task Reports_Inequality_On_A_Direct_Read() => Verify.Analyzer<MissingKeyTestAnalyzer>(Usings + """
			class C
			{
				async Task<bool> M(IFdbReadOnlyTransaction tr, IKeySubspace subspace) => {|#0:(await tr.GetAsync(subspace.Key("a"))) != Slice.Empty|};
			}
			""", Missing(0));

		[Test]
		public Task Reports_IsEmpty_As_A_Condition() => Verify.Analyzer<MissingKeyTestAnalyzer>(Usings + """
			class C
			{
				async Task<int> M(IFdbReadOnlyTransaction tr, Slice key)
				{
					var v = await tr.GetAsync(key);
					if ({|#0:v.IsEmpty|}) return 0;
					while (!{|#1:v.IsEmpty|}) v = v.Substring(1);
					return {|#2:v.IsEmpty|} ? 1 : 2;
				}
			}
			""", Missing(0), Missing(1), Missing(2));

		[Test]
		public Task Ignores_IsNull_And_IsNullOrEmpty() => Verify.Analyzer<MissingKeyTestAnalyzer>(Usings + """
			class C
			{
				async Task<bool> M(IFdbReadOnlyTransaction tr, Slice key)
				{
					var v = await tr.GetAsync(key);
					if (v.IsNull) return false;
					return !v.IsNullOrEmpty;
				}
			}
			""");

		[Test]
		public Task Ignores_IsEmpty_Outside_A_Whole_Condition() => Verify.Analyzer<MissingKeyTestAnalyzer>(Usings + """
			class C
			{
				async Task<bool> M(IFdbReadOnlyTransaction tr, Slice key)
				{
					var v = await tr.GetAsync(key);
					if (v.IsNull || v.IsEmpty) return false;
					bool empty = v.IsEmpty;
					return empty;
				}
			}
			""");

		[Test]
		public Task Ignores_A_Slice_From_Elsewhere() => Verify.Analyzer<MissingKeyTestAnalyzer>(Usings + """
			class C
			{
				bool M(Slice v) => v == Slice.Empty || v.IsEmpty;
			}
			""");

		[Test]
		public Task Fixes_Equality_To_IsNull() => Verify.CodeFix<MissingKeyTestAnalyzer, MissingKeyTestCodeFix>(Usings + """
			class C
			{
				async Task<bool> M(IFdbReadOnlyTransaction tr, Slice key)
				{
					var v = await tr.GetAsync(key);
					return {|#0:v == Slice.Empty|};
				}
			}
			""", Usings + """
			class C
			{
				async Task<bool> M(IFdbReadOnlyTransaction tr, Slice key)
				{
					var v = await tr.GetAsync(key);
					return v.IsNull;
				}
			}
			""", Missing(0));

		[Test]
		public Task Fixes_Inequality_To_A_Negated_IsNull() => Verify.CodeFix<MissingKeyTestAnalyzer, MissingKeyTestCodeFix>(Usings + """
			class C
			{
				async Task<bool> M(IFdbReadOnlyTransaction tr, Slice key) => {|#0:await tr.GetAsync(key) != Slice.Empty|};
			}
			""", Usings + """
			class C
			{
				async Task<bool> M(IFdbReadOnlyTransaction tr, Slice key) => !(await tr.GetAsync(key)).IsNull;
			}
			""", Missing(0));

		[Test]
		public Task Fixes_IsEmpty_To_IsNull() => Verify.CodeFix<MissingKeyTestAnalyzer, MissingKeyTestCodeFix>(Usings + """
			class C
			{
				async Task<int> M(IFdbReadOnlyTransaction tr, Slice key)
				{
					var v = await tr.GetAsync(key);
					if ({|#0:v.IsEmpty|}) return 0;
					return 1;
				}
			}
			""", Usings + """
			class C
			{
				async Task<int> M(IFdbReadOnlyTransaction tr, Slice key)
				{
					var v = await tr.GetAsync(key);
					if (v.IsNull) return 0;
					return 1;
				}
			}
			""", Missing(0));

	}
}
