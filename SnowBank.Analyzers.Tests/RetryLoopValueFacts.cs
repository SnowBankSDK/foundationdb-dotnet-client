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
	public class RetryLoopValueFacts
	{

		private const string Usings = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using FoundationDB.Client;

			""";

		private static DiagnosticResult Value(int location, string expression) => Verify.Diagnostic(FdbDescriptors.RetryLoopValue).WithLocation(location).WithArguments(expression);

		[Test]
		public Task Reports_A_Guid_In_A_Key() => Verify.Analyzer<RetryLoopValueAnalyzer>(Usings + """
			class C
			{
				Task M(IFdbDatabase db, IKeySubspace subspace, Slice value, CancellationToken ct) => db.WriteAsync(tr => tr.Set(subspace.Key({|#0:Guid.NewGuid()|}), value), ct);
			}
			""", Value(0, "Guid.NewGuid()"));

		[Test]
		public Task Reports_A_Guid_Through_A_Local() => Verify.Analyzer<RetryLoopValueAnalyzer>(Usings + """
			class C
			{
				Task M(IFdbDatabase db, IKeySubspace subspace, Slice value, CancellationToken ct) => db.WriteAsync(tr =>
				{
					var id = {|#0:Guid.NewGuid()|};
					tr.Set(subspace.Key(id), value);
				}, ct);
			}
			""", Value(0, "Guid.NewGuid()"));

		[Test]
		public Task Reports_Clocks_In_A_Value() => Verify.Analyzer<RetryLoopValueAnalyzer>(Usings + """
			class C
			{
				Task M(IFdbDatabase db, IKeySubspace subspace, TimeProvider clock, CancellationToken ct) => db.WriteAsync(tr =>
				{
					tr.Set(subspace.Key("a"), FdbValue.ToFixed64LittleEndian({|#0:DateTime.UtcNow|}.Ticks));
					tr.Set(subspace.Key("b"), FdbValue.ToFixed64LittleEndian({|#1:DateTimeOffset.Now|}.Ticks));
					tr.Set(subspace.Key("c"), FdbValue.ToFixed64LittleEndian({|#2:clock.GetUtcNow()|}.Ticks));
					tr.Set(subspace.Key("d"), FdbValue.ToUuid128({|#3:Guid.CreateVersion7()|}));
				}, ct);
			}
			""", Value(0, "DateTime.UtcNow"), Value(1, "DateTimeOffset.Now"), Value(2, "clock.GetUtcNow()"), Value(3, "Guid.CreateVersion7()"));

		[Test]
		public Task Ignores_An_Id_Computed_Before_The_Loop() => Verify.Analyzer<RetryLoopValueAnalyzer>(Usings + """
			class C
			{
				Task M(IFdbDatabase db, IKeySubspace subspace, Slice value, CancellationToken ct)
				{
					var id = Guid.NewGuid();
					return db.WriteAsync(tr => tr.Set(subspace.Key(id), value), ct);
				}
			}
			""");

		[Test]
		public Task Ignores_A_Value_That_Stays_Out_Of_The_Transaction() => Verify.Analyzer<RetryLoopValueAnalyzer>(Usings + """
			class C
			{
				Task M(IFdbDatabase db, IKeySubspace subspace, Slice value, CancellationToken ct) => db.WriteAsync(tr =>
				{
					Console.WriteLine(Guid.NewGuid());
					var started = DateTime.UtcNow;
					tr.Set(subspace.Key("a"), value);
					Console.WriteLine(started);
				}, ct);
			}
			""");

		[Test]
		public Task Ignores_A_Call_Outside_A_Retry_Loop() => Verify.Analyzer<RetryLoopValueAnalyzer>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, IKeySubspace subspace, Slice value) => tr.Set(subspace.Key(Guid.NewGuid()), value);
			}
			""");

		[Test]
		public Task Fixes_By_Hoisting_The_Guid() => Verify.CodeFix<RetryLoopValueAnalyzer, RetryLoopValueCodeFix>(Usings + """
			class C
			{
				async Task M(IFdbDatabase db, IKeySubspace subspace, Slice value, CancellationToken ct)
				{
					await db.WriteAsync(tr => tr.Set(subspace.Key({|#0:Guid.NewGuid()|}), value), ct);
				}
			}
			""", Usings + """
			class C
			{
				async Task M(IFdbDatabase db, IKeySubspace subspace, Slice value, CancellationToken ct)
				{
					var id = Guid.NewGuid();
					await db.WriteAsync(tr => tr.Set(subspace.Key(id), value), ct);
				}
			}
			""", Value(0, "Guid.NewGuid()"));

		[Test]
		public Task Fixes_With_A_Fresh_Name() => Verify.CodeFix<RetryLoopValueAnalyzer, RetryLoopValueCodeFix>(Usings + """
			class C
			{
				async Task M(IFdbDatabase db, IKeySubspace subspace, Guid id, CancellationToken ct)
				{
					await db.WriteAsync(tr =>
					{
						var key = subspace.Key(id, {|#0:Guid.CreateVersion7()|});
						tr.Set(key, FdbValue.ToUuid128(id));
					}, ct);
				}
			}
			""", Usings + """
			class C
			{
				async Task M(IFdbDatabase db, IKeySubspace subspace, Guid id, CancellationToken ct)
				{
					var id2 = Guid.CreateVersion7();
					await db.WriteAsync(tr =>
					{
						var key = subspace.Key(id, id2);
						tr.Set(key, FdbValue.ToUuid128(id));
					}, ct);
				}
			}
			""", Value(0, "Guid.CreateVersion7()"));

		[Test]
		public Task Offers_No_Fix_In_A_Static_Lambda() => Verify.CodeFix<RetryLoopValueAnalyzer, RetryLoopValueCodeFix>(Usings + """
			class C
			{
				async Task M(IFdbDatabase db, CancellationToken ct)
				{
					await db.WriteAsync(static tr => tr.Set(Slice.FromString("k"), FdbValue.ToUuid128({|#0:Guid.NewGuid()|})), ct);
				}
			}
			""", Usings + """
			class C
			{
				async Task M(IFdbDatabase db, CancellationToken ct)
				{
					await db.WriteAsync(static tr => tr.Set(Slice.FromString("k"), FdbValue.ToUuid128({|#0:Guid.NewGuid()|})), ct);
				}
			}
			""", Value(0, "Guid.NewGuid()"));

	}
}
