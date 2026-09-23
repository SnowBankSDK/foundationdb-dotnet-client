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
	public class ReadOnlyJsonMutationFacts
	{

		private const string Usings = """
			using System;
			using SnowBank.Data.Json;

			""";

		private static DiagnosticResult Expected(int location, string name) => Verify.Diagnostic(SbkDescriptors.ReadOnlyJsonMutation).WithLocation(location).WithArguments(name);

		[Test]
		public Task Reports_An_Indexer_Assignment_On_A_ReadOnly_Object() => Verify.Analyzer<ReadOnlyJsonMutationAnalyzer>(Usings + """
			class C
			{
				void M()
				{
					var frozen = JsonObject.ReadOnly.Create("Year", 1966);
					{|#0:frozen["Year"] = 1967|};
				}
			}
			""", Expected(0, "frozen"));

		[Test]
		public Task Reports_Each_Mutating_Call() => Verify.Analyzer<ReadOnlyJsonMutationAnalyzer>(Usings + """
			class C
			{
				void M(JsonArray source, JsonObject obj)
				{
					var arr = source.ToReadOnly();
					{|#0:arr.Add(1)|};
					{|#1:arr.Clear()|};
					var frozen = obj.Freeze();
					{|#2:frozen.Set("a", 1)|};
					{|#3:frozen.Remove("a")|};
					var empty = JsonObject.ReadOnly.Empty;
					{|#4:empty.AddRange(obj)|};
					var parsed = JsonArray.ReadOnly.Parse("[1, 2]");
					{|#5:parsed.Insert(0, 3)|};
				}
			}
			""", Expected(0, "arr"), Expected(1, "arr"), Expected(2, "frozen"), Expected(3, "frozen"), Expected(4, "empty"), Expected(5, "parsed"));

		[Test]
		public Task Ignores_A_Mutable_Copy() => Verify.Analyzer<ReadOnlyJsonMutationAnalyzer>(Usings + """
			class C
			{
				void M()
				{
					var frozen = JsonObject.ReadOnly.Create("Year", 1966);
					var draft = frozen.ToMutable();
					draft["Year"] = 1967;
					var plain = JsonObject.Create("Year", 1966);
					plain["Year"] = 1967;
					var copy = frozen.Copy();
					copy.Add("Month", 6);
				}
			}
			""");

		[Test]
		public Task Ignores_A_Local_Assigned_Again() => Verify.Analyzer<ReadOnlyJsonMutationAnalyzer>(Usings + """
			class C
			{
				void M()
				{
					var frozen = JsonObject.ReadOnly.Create("Year", 1966);
					frozen = frozen.ToMutable();
					frozen["Year"] = 1967;
				}
			}
			""");

		[Test]
		public Task Ignores_A_Parameter_Or_A_Direct_Receiver() => Verify.Analyzer<ReadOnlyJsonMutationAnalyzer>(Usings + """
			class C
			{
				void M(JsonObject frozen)
				{
					frozen["Year"] = 1967;
					JsonObject.ReadOnly.Empty["Year"] = 1967;
					_ = frozen["Year"];
				}
			}
			""");

	}
}
