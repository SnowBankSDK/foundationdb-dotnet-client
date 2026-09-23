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
	public class ProviderUnwrapFacts
	{

		private const string Usings = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using FoundationDB.Client;

			""";

		private static DiagnosticResult Unwrapped(int location) => Verify.Diagnostic(FdbDescriptors.ProviderUnwrapped).WithLocation(location);

		[Test]
		public Task Reports_A_Database_Used_Only_For_A_Retry_Loop() => Verify.Analyzer<ProviderUnwrapAnalyzer>(Usings + """
			class C
			{
				async Task<long> M(IFdbDatabaseProvider provider, CancellationToken ct)
				{
					var db = {|#0:await provider.GetDatabase(ct)|};
					return await db.ReadAsync(tr => tr.GetReadVersionAsync(), ct);
				}
			}
			""", Unwrapped(0));

		[Test]
		public Task Reports_Uses_Of_Root_And_WriteAsync() => Verify.Analyzer<ProviderUnwrapAnalyzer>(Usings + """
			class C
			{
				async Task M(IFdbDatabaseProvider provider, Slice key, Slice value, CancellationToken ct)
				{
					var db = {|#0:await provider.GetDatabase(ct)|};
					var root = db.Root;
					await db.WriteAsync(tr => tr.Set(key, value), ct);
					await db.ReadWriteAsync(async tr => await tr.GetAsync(key), ct);
				}
			}
			""", Unwrapped(0));

		[Test]
		public Task Ignores_A_Database_Used_Elsewhere() => Verify.Analyzer<ProviderUnwrapAnalyzer>(Usings + """
			class C
			{
				async Task<long> M(IFdbDatabaseProvider provider, CancellationToken ct)
				{
					var db = await provider.GetDatabase(ct);
					Console.WriteLine(db.Name);
					return await db.ReadAsync(tr => tr.GetReadVersionAsync(), ct);
				}
			}
			""");

		[Test]
		public Task Ignores_A_Retry_Loop_The_Provider_Lacks() => Verify.Analyzer<ProviderUnwrapAnalyzer>(Usings + """
			class C
			{
				async Task<long> M(IFdbDatabaseProvider provider, CancellationToken ct)
				{
					var db = await provider.GetDatabase(ct);
					return await db.ReadAsync(42, (tr, state) => tr.GetReadVersionAsync(), ct);
				}
			}
			""");

		[Test]
		public Task Fixes_By_Calling_The_Provider() => Verify.CodeFix<ProviderUnwrapAnalyzer, ProviderUnwrapCodeFix>(Usings + """
			class C
			{
				async Task<long> M(IFdbDatabaseProvider provider, CancellationToken ct)
				{
					var db = {|#0:await provider.GetDatabase(ct)|};
					return await db.ReadAsync(tr => tr.GetReadVersionAsync(), ct);
				}
			}
			""", Usings + """
			class C
			{
				async Task<long> M(IFdbDatabaseProvider provider, CancellationToken ct)
				{
					return await provider.ReadAsync(tr => tr.GetReadVersionAsync(), ct);
				}
			}
			""", Unwrapped(0));

		[Test]
		public Task Fixes_Every_Use() => Verify.CodeFix<ProviderUnwrapAnalyzer, ProviderUnwrapCodeFix>(Usings + """
			class C
			{
				private IFdbDatabaseProvider Provider { get; set; } = null!;

				async Task M(Slice key, Slice value, CancellationToken ct)
				{
					var db = {|#0:await this.Provider.GetDatabase(ct)|};
					var root = db.Root;
					await db.WriteAsync(tr => tr.Set(key, value), ct);
				}
			}
			""", Usings + """
			class C
			{
				private IFdbDatabaseProvider Provider { get; set; } = null!;

				async Task M(Slice key, Slice value, CancellationToken ct)
				{
					var root = this.Provider.Root;
					await this.Provider.WriteAsync(tr => tr.Set(key, value), ct);
				}
			}
			""", Unwrapped(0));

		[Test]
		public Task Offers_No_Fix_When_The_Provider_Is_A_Call() => Verify.CodeFix<ProviderUnwrapAnalyzer, ProviderUnwrapCodeFix>(Usings + """
			class C
			{
				IFdbDatabaseProvider GetProvider() => null!;

				async Task<long> M(CancellationToken ct)
				{
					var db = {|#0:await GetProvider().GetDatabase(ct)|};
					return await db.ReadAsync(tr => tr.GetReadVersionAsync(), ct);
				}
			}
			""", Usings + """
			class C
			{
				IFdbDatabaseProvider GetProvider() => null!;

				async Task<long> M(CancellationToken ct)
				{
					var db = {|#0:await GetProvider().GetDatabase(ct)|};
					return await db.ReadAsync(tr => tr.GetReadVersionAsync(), ct);
				}
			}
			""", Unwrapped(0));

	}
}
