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
	public class WatchFacts
	{

		private static DiagnosticResult Token(int location) => Verify.Diagnostic(FdbDescriptors.WatchTransactionToken).WithLocation(location);

		private static DiagnosticResult Awaited(int location) => Verify.Diagnostic(FdbDescriptors.WatchAwaitedInHandler).WithLocation(location);

		private const string Usings = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using FoundationDB.Client;

			""";

		#region FDB0002...

		[Test]
		public Task Reports_The_Transaction_Token() => Verify.Analyzer<WatchAnalyzer>(Usings + """
			class C
			{
				Task M(IFdbDatabase db, Slice key, CancellationToken ct) => db.WriteAsync(tr => { var w = tr.Watch(key, {|#0:tr.Cancellation|}); }, ct);
			}
			""", Token(0));

		[Test]
		public Task Reports_A_Local_Copy_Of_The_Transaction_Token() => Verify.Analyzer<WatchAnalyzer>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, Slice key)
				{
					var token = tr.Cancellation;
					var w = tr.Watch(key, {|#0:token|});
				}
			}
			""", Token(0));

		[Test]
		public Task Reports_The_Typed_Key_Overload_Outside_A_Retry_Loop() => Verify.Analyzer<WatchAnalyzer>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, IKeySubspace subspace)
				{
					var w = tr.Watch(subspace.Key("watched"), {|#0:tr.Cancellation|});
				}
			}
			""", Token(0));

		[Test]
		public Task Ignores_The_Outer_Token() => Verify.Analyzer<WatchAnalyzer>(Usings + """
			class C
			{
				Task M(IFdbDatabase db, Slice key, CancellationToken ct) => db.WriteAsync(tr => { var w = tr.Watch(key, ct); }, ct);
			}
			""");

		[Test]
		public Task Ignores_The_Token_Of_Another_Transaction() => Verify.Analyzer<WatchAnalyzer>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, IFdbTransaction other, Slice key)
				{
					var w = tr.Watch(key, other.Cancellation);
				}
			}
			""");

		[Test]
		public Task Fixes_The_Token_With_The_Retry_Loop_Token() => Verify.CodeFix<WatchAnalyzer, WatchTokenCodeFix>(Usings + """
			class C
			{
				Task M(IFdbDatabase db, Slice key, CancellationToken ct) => db.WriteAsync(tr => { var w = tr.Watch(key, {|#0:tr.Cancellation|}); }, ct);
			}
			""", Usings + """
			class C
			{
				Task M(IFdbDatabase db, Slice key, CancellationToken ct) => db.WriteAsync(tr => { var w = tr.Watch(key, ct); }, ct);
			}
			""", Token(0));

		[Test]
		public Task Offers_No_Fix_Outside_A_Retry_Loop() => Verify.CodeFix<WatchAnalyzer, WatchTokenCodeFix>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, Slice key)
				{
					var w = tr.Watch(key, {|#0:tr.Cancellation|});
				}
			}
			""", Usings + """
			class C
			{
				void M(IFdbTransaction tr, Slice key)
				{
					var w = tr.Watch(key, {|#0:tr.Cancellation|});
				}
			}
			""", Token(0));

		#endregion

		#region FDB0003...

		[Test]
		public Task Reports_A_Watch_Awaited_In_The_Handler() => Verify.Analyzer<WatchAnalyzer>(Usings + """
			class C
			{
				Task M(IFdbDatabase db, Slice key, CancellationToken ct) => db.WriteAsync(async tr => { var w = tr.Watch(key, ct); {|#0:await w|}; }, ct);
			}
			""", Awaited(0));

		[Test]
		public Task Reports_WaitAsync_In_The_Handler() => Verify.Analyzer<WatchAnalyzer>(Usings + """
			class C
			{
				Task M(IFdbDatabase db, Slice key, CancellationToken ct) => db.WriteAsync(async tr => { var w = tr.Watch(key, ct); {|#0:await w.WaitAsync(ct)|}; }, ct);
			}
			""", Awaited(0));

		[Test]
		public Task Ignores_A_Watch_Awaited_After_The_Handler() => Verify.Analyzer<WatchAnalyzer>(Usings + """
			class C
			{
				async Task M(IFdbDatabase db, Slice key, CancellationToken ct)
				{
					FdbWatch watch = await db.ReadWriteAsync(tr => Task.FromResult(tr.Watch(key, ct)), ct);
					await watch;
				}
			}
			""");

		[Test]
		public Task Ignores_A_Watch_Awaited_In_Another_Lambda() => Verify.Analyzer<WatchAnalyzer>(Usings + """
			class C
			{
				Task M(FdbWatch watch, CancellationToken ct) => Task.Run(async () => await watch.WaitAsync(ct), ct);
			}
			""");

		#endregion

	}
}
