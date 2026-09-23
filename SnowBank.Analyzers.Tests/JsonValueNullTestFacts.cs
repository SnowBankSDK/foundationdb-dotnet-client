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
	public class JsonValueNullTestFacts
	{

		private static DiagnosticResult Expected(int location) => Verify.Diagnostic(SbkDescriptors.JsonValueNullTest).WithLocation(location);

		[Test]
		public Task Reports_Equality_With_Null() => Verify.Analyzer<JsonValueNullTestAnalyzer>("""
			#nullable enable
			using SnowBank.Data.Json;
			class C
			{
				bool M(JsonObject obj) => {|#0:obj["name"] == null|};
			}
			""", Expected(0));

		[Test]
		public Task Reports_All_Null_Test_Shapes() => Verify.Analyzer<JsonValueNullTestAnalyzer>("""
			#nullable enable
			using SnowBank.Data.Json;
			class C
			{
				void M(JsonObject obj)
				{
					JsonValue v = obj["name"];
					_ = {|#0:v != null|};
					_ = {|#1:v is null|};
					_ = {|#2:v is not null|};
					_ = {|#3:v ?? JsonNull.Null|};
					_ = {|#4:v?.ToString()|};
				}
			}
			""", Expected(0), Expected(1), Expected(2), Expected(3), Expected(4));

		[Test]
		public Task Ignores_Nullable_Declarations() => Verify.Analyzer<JsonValueNullTestAnalyzer>("""
			#nullable enable
			using SnowBank.Data.Json;
			class C
			{
				bool M(JsonValue? maybe) => maybe == null;
			}
			""");

		[Test]
		public Task Ignores_Code_Without_Nullable_Context() => Verify.Analyzer<JsonValueNullTestAnalyzer>("""
			#nullable disable
			using SnowBank.Data.Json;
			class C
			{
				bool M(JsonObject obj) => obj["name"] == null;
			}
			""");

		[Test]
		public Task Ignores_The_Correct_Form() => Verify.Analyzer<JsonValueNullTestAnalyzer>("""
			#nullable enable
			using SnowBank.Data.Json;
			class C
			{
				bool M(JsonObject obj) => obj["name"].IsNullOrMissing();
			}
			""");

		[Test]
		public Task Fixes_Equality() => Verify.CodeFix<JsonValueNullTestAnalyzer, JsonValueNullTestCodeFix>("""
			#nullable enable
			using SnowBank.Data.Json;
			class C
			{
				bool M(JsonObject obj) => {|#0:obj["name"] == null|};
			}
			""", """
			#nullable enable
			using SnowBank.Data.Json;
			class C
			{
				bool M(JsonObject obj) => obj["name"].IsNullOrMissing();
			}
			""", Expected(0));

		[Test]
		public Task Fixes_Inequality_And_Patterns() => Verify.CodeFix<JsonValueNullTestAnalyzer, JsonValueNullTestCodeFix>("""
			#nullable enable
			using SnowBank.Data.Json;
			class C
			{
				void M(JsonValue v)
				{
					_ = {|#0:v != null|};
					_ = {|#1:v is null|};
					_ = {|#2:v is not null|};
				}
			}
			""", """
			#nullable enable
			using SnowBank.Data.Json;
			class C
			{
				void M(JsonValue v)
				{
					_ = !v.IsNullOrMissing();
					_ = v.IsNullOrMissing();
					_ = !v.IsNullOrMissing();
				}
			}
			""", Expected(0), Expected(1), Expected(2));

		[Test]
		public Task Fix_Adds_The_Using_When_Missing() => Verify.CodeFix<JsonValueNullTestAnalyzer, JsonValueNullTestCodeFix>("""
			#nullable enable
			class C
			{
				bool M(SnowBank.Data.Json.JsonObject obj) => {|#0:obj["name"] == null|};
			}
			""", """
			using SnowBank.Data.Json;
			#nullable enable
			class C
			{
				bool M(SnowBank.Data.Json.JsonObject obj) => obj["name"].IsNullOrMissing();
			}
			""", Expected(0));

	}
}
