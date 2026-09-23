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
	public class EncodingPerformanceFacts
	{

		private const string Usings = """
			using System;
			using System.Text;
			using System.Threading;
			using System.Threading.Tasks;
			using FoundationDB.Client;
			using SnowBank.Data.Json;

			""";

		private static DiagnosticResult Buffer(int location, string value, string replacement) => Verify.Diagnostic(FdbDescriptors.IntermediateValueBuffer).WithLocation(location).WithArguments(value, replacement);

		private static DiagnosticResult Eager(int location, string key) => Verify.Diagnostic(FdbDescriptors.EagerKeySlice).WithLocation(location).WithArguments(key);

		#region FDB2001...

		[Test]
		public Task Reports_FromString_As_A_Set_Value() => Verify.Analyzer<EncodingPerformanceAnalyzer>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, IKeySubspace subspace, string json) => tr.Set(subspace.Key("a"), {|#0:Slice.FromString(json)|});
			}
			""", Buffer(0, "Slice.FromString(json)", "FdbValue.ToTextUtf8(json)"));

		[Test]
		public Task Reports_Each_Factory_Of_The_Table() => Verify.Analyzer<EncodingPerformanceAnalyzer>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, IKeySubspace subspace, Slice key, string s, long n, Guid g, object obj, byte[] bytes)
				{
					tr.Set(key, {|#0:Slice.FromStringUtf8(s)|});
					tr.Set(key, {|#1:Slice.FromStringAscii(s)|});
					tr.Set(key, {|#2:Encoding.UTF8.GetBytes(s)|});
					tr.AtomicAdd(key, {|#3:Slice.FromFixed64(n)|});
					tr.AtomicMax(key, {|#4:Slice.FromGuid(g)|});
					tr.AtomicMin(key, {|#5:CrystalJson.ToSlice(obj)|});
					tr.SetVersionStampedValue(subspace.Key("v"), {|#6:CrystalJson.ToBytes(obj)|});
					tr.Set(key, {|#7:bytes.AsSpan(4, 16).ToArray()|});
				}
			}
			""",
			Buffer(0, "Slice.FromStringUtf8(s)", "FdbValue.ToTextUtf8(s)"),
			Buffer(1, "Slice.FromStringAscii(s)", "FdbValue.ToTextUtf8(s)"),
			Buffer(2, "Encoding.UTF8.GetBytes(s)", "FdbValue.ToTextUtf8(s)"),
			Buffer(3, "Slice.FromFixed64(n)", "FdbValue.ToFixed64LittleEndian(n)"),
			Buffer(4, "Slice.FromGuid(g)", "FdbValue.ToUuid128(g)"),
			Buffer(5, "CrystalJson.ToSlice(obj)", "FdbValue.ToJson(obj)"),
			Buffer(6, "CrystalJson.ToBytes(obj)", "FdbValue.ToJson(obj)"),
			Buffer(7, "bytes.AsSpan(4, 16).ToArray()", "FdbValue.ToBytes(bytes, 4, 16)"));

		[Test]
		public Task Reports_A_Local_Written_Once() => Verify.Analyzer<EncodingPerformanceAnalyzer>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, Slice key, string json)
				{
					var v = Slice.FromString(json);
					tr.Set(key, {|#0:v|});
				}
			}
			""", Buffer(0, "Slice.FromString(json)", "FdbValue.ToTextUtf8(json)"));

		[Test]
		public Task Ignores_A_Local_Reused_By_Several_Writes() => Verify.Analyzer<EncodingPerformanceAnalyzer>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, Slice k1, Slice k2, string json)
				{
					var v = Slice.FromString(json);
					tr.Set(k1, v);
					tr.Set(k2, v);
				}
			}
			""");

		[Test]
		public Task Ignores_Typed_Values_And_Keys() => Verify.Analyzer<EncodingPerformanceAnalyzer>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, Slice key, Slice value, string s)
				{
					tr.Set(key, FdbValue.ToTextUtf8(s));
					tr.Set(key, value);
					tr.Set(Slice.FromString(s), value);
					Console.WriteLine(Slice.FromString(s));
				}
			}
			""");

		[Test]
		public Task Fixes_FromString_To_ToTextUtf8() => Verify.CodeFix<EncodingPerformanceAnalyzer, EncodingPerformanceCodeFix>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, IKeySubspace subspace, string json) => tr.Set(subspace.Key("a"), {|#0:Slice.FromString(json)|});
			}
			""", Usings + """
			class C
			{
				void M(IFdbTransaction tr, IKeySubspace subspace, string json) => tr.Set(subspace.Key("a"), FdbValue.ToTextUtf8(json));
			}
			""", Buffer(0, "Slice.FromString(json)", "FdbValue.ToTextUtf8(json)"));

		[Test]
		public Task Fixes_A_Copied_Chunk_To_ToBytes() => Verify.CodeFix<EncodingPerformanceAnalyzer, EncodingPerformanceCodeFix>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, Slice key, byte[] bytes, int offset) => tr.Set(key, {|#0:bytes.AsSpan(offset, 1000).ToArray()|});
			}
			""", Usings + """
			class C
			{
				void M(IFdbTransaction tr, Slice key, byte[] bytes, int offset) => tr.Set(key, FdbValue.ToBytes(bytes, offset, 1000));
			}
			""", Buffer(0, "bytes.AsSpan(offset, 1000).ToArray()", "FdbValue.ToBytes(bytes, offset, 1000)"));

		[Test]
		public Task Fixes_The_Initializer_Of_A_Local() => Verify.CodeFix<EncodingPerformanceAnalyzer, EncodingPerformanceCodeFix>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, Slice key, Guid g)
				{
					var v = Slice.FromGuid(g);
					tr.Set(key, {|#0:v|});
				}
			}
			""", Usings + """
			class C
			{
				void M(IFdbTransaction tr, Slice key, Guid g)
				{
					var v = FdbValue.ToUuid128(g);
					tr.Set(key, v);
				}
			}
			""", Buffer(0, "Slice.FromGuid(g)", "FdbValue.ToUuid128(g)"));

		[Test]
		public Task Offers_No_Fix_For_A_Local_Typed_Slice() => Verify.CodeFix<EncodingPerformanceAnalyzer, EncodingPerformanceCodeFix>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, Slice key, string s)
				{
					Slice v = Slice.FromString(s);
					tr.Set(key, {|#0:v|});
				}
			}
			""", Usings + """
			class C
			{
				void M(IFdbTransaction tr, Slice key, string s)
				{
					Slice v = Slice.FromString(s);
					tr.Set(key, {|#0:v|});
				}
			}
			""", Buffer(0, "Slice.FromString(s)", "FdbValue.ToTextUtf8(s)"));

		#endregion

		#region FDB2002...

		[Test]
		public Task Reports_ToSlice_On_A_Set_Key() => Verify.Analyzer<EncodingPerformanceAnalyzer>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, IKeySubspace subspace, Slice value) => tr.Set({|#0:subspace.Key("a", 1).ToSlice()|}, value);
			}
			""", Eager(0, "subspace.Key(\"a\", 1)"));

		[Test]
		public Task Reports_A_Local_Key() => Verify.Analyzer<EncodingPerformanceAnalyzer>(Usings + """
			class C
			{
				async Task<Slice> M(IFdbReadOnlyTransaction tr, IKeySubspace subspace, Guid id)
				{
					var k = subspace.Key(id).ToSlice();
					return await tr.GetAsync({|#0:k|});
				}
			}
			""", Eager(0, "subspace.Key(id)"));

		[Test]
		public Task Reports_Other_Key_Positions() => Verify.Analyzer<EncodingPerformanceAnalyzer>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, IKeySubspace subspace, Slice value, CancellationToken ct)
				{
					tr.Clear({|#0:subspace.Key("a").ToSlice()|});
					tr.ClearRange({|#1:subspace.Key("a").ToSlice()|}, {|#2:subspace.Key("b").ToSlice()|});
					tr.AtomicAdd({|#3:subspace.Key("n").ToSlice()|}, value);
					tr.SetVersionStampedKey({|#4:subspace.Key(tr.CreateVersionStamp()).ToSlice()|}, value);
					var w = tr.Watch({|#5:subspace.Key("w").ToSlice()|}, ct);
				}
			}
			""", Eager(0, "subspace.Key(\"a\")"), Eager(1, "subspace.Key(\"a\")"), Eager(2, "subspace.Key(\"b\")"), Eager(3, "subspace.Key(\"n\")"), Eager(4, "subspace.Key(tr.CreateVersionStamp())"), Eager(5, "subspace.Key(\"w\")"));

		[Test]
		public Task Ignores_ToSlice_For_Logging_Or_As_A_Value() => Verify.Analyzer<EncodingPerformanceAnalyzer>(Usings + """
			class C
			{
				async Task M(IFdbTransaction tr, IKeySubspace subspace, Slice key)
				{
					Console.WriteLine(subspace.Key("a").ToSlice());
					tr.Set(key, subspace.Key("a").ToSlice());
					var k = subspace.Key("b").ToSlice();
					Console.WriteLine(k);
					await tr.GetAsync(k);
				}
			}
			""");

		[Test]
		public Task Fixes_By_Removing_ToSlice() => Verify.CodeFix<EncodingPerformanceAnalyzer, EncodingPerformanceCodeFix>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, IKeySubspace subspace, Slice value) => tr.Set({|#0:subspace.Key("a", 1).ToSlice()|}, value);
			}
			""", Usings + """
			class C
			{
				void M(IFdbTransaction tr, IKeySubspace subspace, Slice value) => tr.Set(subspace.Key("a", 1), value);
			}
			""", Eager(0, "subspace.Key(\"a\", 1)"));

		[Test]
		public Task Fixes_The_Initializer_Of_A_Local_Key() => Verify.CodeFix<EncodingPerformanceAnalyzer, EncodingPerformanceCodeFix>(Usings + """
			class C
			{
				async Task<Slice> M(IFdbReadOnlyTransaction tr, IKeySubspace subspace, Guid id)
				{
					var k = subspace.Key(id).ToSlice();
					return await tr.GetAsync({|#0:k|});
				}
			}
			""", Usings + """
			class C
			{
				async Task<Slice> M(IFdbReadOnlyTransaction tr, IKeySubspace subspace, Guid id)
				{
					var k = subspace.Key(id);
					return await tr.GetAsync(k);
				}
			}
			""", Eager(0, "subspace.Key(id)"));

		#endregion

	}
}
