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
	public class ContractArgumentFacts
	{

		private const string Usings = """
			using System;
			using SnowBank.Diagnostics.Contracts;

			""";

		private static DiagnosticResult Expected(int location, string replacement) => Verify.Diagnostic(SbkDescriptors.ContractOnPublicArgument).WithLocation(location).WithArguments(replacement);

		[Test]
		public Task Reports_Requires_On_A_Public_Parameter() => Verify.Analyzer<ContractArgumentAnalyzer>(Usings + """
			public class C
			{
				public void M(string name)
				{
					{|#0:Contract.Requires(name != null)|};
				}
			}
			""", Expected(0, "Contract.NotNull(name)"));

		[Test]
		public Task Reports_Each_Shape_And_Entry_Point() => Verify.Analyzer<ContractArgumentAnalyzer>(Usings + """
			public class C
			{
				public C(string name)
				{
					{|#0:Contract.Requires(name is not null)|};
				}
				protected void M(string name, int count, long size, double ratio)
				{
					{|#1:Contract.Requires(!string.IsNullOrEmpty(name))|};
					{|#2:Contract.Assert(!string.IsNullOrWhiteSpace(name))|};
					{|#3:Contract.Debug.Requires(count > 0)|};
					{|#4:Contract.Requires(0 < size)|};
					{|#5:Contract.Requires(ratio >= 0)|};
					{|#6:Contract.Requires(null != name)|};
				}
				public string Name
				{
					set { {|#7:Contract.Requires(value != null)|}; }
				}
				public class Nested
				{
					protected internal void N(object item) => {|#8:Contract.Requires(item != null)|};
				}
			}
			""",
			Expected(0, "Contract.NotNull(name)"),
			Expected(1, "Contract.NotNullOrEmpty(name)"),
			Expected(2, "Contract.NotNullOrWhiteSpace(name)"),
			Expected(3, "Contract.Positive(count)"),
			Expected(4, "Contract.Positive(size)"),
			Expected(5, "Contract.GreaterOrEqual(ratio, 0)"),
			Expected(6, "Contract.NotNull(name)"),
			Expected(7, "Contract.NotNull(value)"),
			Expected(8, "Contract.NotNull(item)"));

		[Test]
		public Task Ignores_Members_That_Are_Not_Public() => Verify.Analyzer<ContractArgumentAnalyzer>(Usings + """
			public class C
			{
				private void M(string name) => Contract.Requires(name != null);
				internal void N(string name) => Contract.Requires(name != null);
				public string Name { private set { Contract.Requires(value != null); } get => ""; }
				private class Inner
				{
					public void O(string name) => Contract.Requires(name != null);
				}
			}
			internal class D
			{
				public void M(string name) => Contract.Requires(name != null);
			}
			""");

		[Test]
		public Task Ignores_Other_Shapes() => Verify.Analyzer<ContractArgumentAnalyzer>(Usings + """
			public class C
			{
				private string? field;
				public void M(string name, int count, int max, decimal amount)
				{
					Contract.Requires(count > max);
					Contract.Requires(name != null && count > 0);
					Contract.Requires(name != null, "name is required");
					Contract.Requires(amount > 0);
					var copy = name;
					Contract.Requires(copy != null);
					Contract.Requires(this.field != null);
					Contract.NotNull(name);
					Contract.Positive(count);
				}
			}
			""");

		[Test]
		public Task Fixes_Each_Shape() => Verify.CodeFix<ContractArgumentAnalyzer, ContractArgumentCodeFix>(Usings + """
			public class C
			{
				public void M(string name, int count, long size, double ratio)
				{
					{|#0:Contract.Requires(name != null)|};
					{|#1:Contract.Requires(!string.IsNullOrEmpty(name))|};
					{|#2:Contract.Assert(!string.IsNullOrWhiteSpace(name))|};
					{|#3:Contract.Debug.Requires(count > 0)|};
					{|#4:Contract.Requires(size >= 0)|};
					{|#5:SnowBank.Diagnostics.Contracts.Contract.Requires(ratio > 0)|};
				}
			}
			""", Usings + """
			public class C
			{
				public void M(string name, int count, long size, double ratio)
				{
					Contract.NotNull(name);
					Contract.NotNullOrEmpty(name);
					Contract.NotNullOrWhiteSpace(name);
					Contract.Positive(count);
					Contract.GreaterOrEqual(size, 0);
					SnowBank.Diagnostics.Contracts.Contract.Positive(ratio);
				}
			}
			""",
			Expected(0, "Contract.NotNull(name)"),
			Expected(1, "Contract.NotNullOrEmpty(name)"),
			Expected(2, "Contract.NotNullOrWhiteSpace(name)"),
			Expected(3, "Contract.Positive(count)"),
			Expected(4, "Contract.GreaterOrEqual(size, 0)"),
			Expected(5, "SnowBank.Diagnostics.Contracts.Contract.Positive(ratio)"));

	}
}
