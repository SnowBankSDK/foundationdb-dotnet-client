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
	public class RemovedApiFacts
	{

		private const string Usings = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using FoundationDB.Client;

			""";

		private static DiagnosticResult Fdb(int location, string name, string advice) => Verify.Diagnostic(FdbDescriptors.RemovedApi).WithLocation(location).WithArguments(name, advice);

		private static DiagnosticResult Sbk(int location, string name, string advice) => Verify.Diagnostic(SbkDescriptors.RemovedSliceApi).WithLocation(location).WithArguments(name, advice);

		private static DiagnosticResult Compiler(string id, int location) => DiagnosticResult.CompilerError(id).WithLocation(location);

		#region FDB0100...

		[Test]
		public Task Reports_Pack_At_The_Compiler_Error_Span() => Verify.Analyzer<RemovedApiAnalyzer>(Usings + """
			class C
			{
				object M(IKeySubspace subspace, int id) => subspace.{|#0:Pack|}(id);
			}
			""", Compiler("CS1061", 0), Fdb(0, "Pack", "Use subspace.Key(...) instead."));

		[Test]
		public Task Reports_Every_Subspace_Member() => Verify.Analyzer<RemovedApiAnalyzer>(Usings + """
			class C
			{
				void M(IKeySubspace subspace, string a, int b)
				{
					object o1 = subspace.{|#0:Encode|}(a);
					object o2 = subspace.{|#1:EncodeRange|}(a);
					object o3 = subspace.{|#2:Partition|};
					object o4 = subspace.{|#3:ByKey|}(a);
					object o5 = {|#4:subspace[a, b]|};
				}
			}
			""",
			Compiler("CS1061", 0), Fdb(0, "Encode", "Use subspace.Key(...) instead."),
			Compiler("CS1061", 1), Fdb(1, "EncodeRange", "Use subspace.Key(...).ToRange() instead."),
			Compiler("CS1061", 2), Fdb(2, "Partition", "Use subspace.Key(x).ToSubspace() instead."),
			Compiler("CS1061", 3), Fdb(3, "ByKey", "Use subspace.Key(a, b) instead."),
			Compiler("CS0021", 4), Fdb(4, "subspace[...]", "Use subspace.Key(a, b) instead."));

		[Test]
		public Task Reports_A_Subspace_Implementation() => Verify.Analyzer<RemovedApiAnalyzer>(Usings + """
			class C
			{
				object M(FdbDirectorySubspace subspace, int id) => subspace.{|#0:Pack|}(id);
			}
			""", Compiler("CS1061", 0), Fdb(0, "Pack", "Use subspace.Key(...) instead."));

		[Test]
		public Task Reports_Database_Provider_Location_And_Selector_Members() => Verify.Analyzer<RemovedApiAnalyzer>(Usings + """
			class C
			{
				object M1(IFdbDatabase db) => db.{|#0:Directory|};
				object M2(IFdbDatabaseProvider provider, CancellationToken ct) => provider.{|#1:GetDatabaseAsync|}(ct);
				object M3(ISubspaceLocation location, IFdbReadOnlyTransaction tr) => location.{|#2:ResolveAsync|}(tr);
				object M4(Slice key) => KeySelector.{|#3:FirstAfter|}(key);
			}
			""",
			Compiler("CS1061", 0), Fdb(0, "Directory", "Use db.Root[...] or db.DirectoryLayer instead."),
			Compiler("CS1061", 1), Fdb(1, "GetDatabaseAsync", "Use GetDatabase(ct), or call ReadAsync, WriteAsync, or ReadWriteAsync on the provider directly."),
			Compiler("CS1061", 2), Fdb(2, "ResolveAsync", "Use Resolve(tr) instead."),
			Compiler("CS0117", 3), Fdb(3, "FirstAfter", "Use KeySelector.FirstGreaterThan(key) instead."));

		[Test]
		public Task Ignores_The_Same_Name_On_Another_Type() => Verify.Analyzer<RemovedApiAnalyzer>(Usings + """
			class Other
			{
				public object Pack(int id) => id;
			}
			class C
			{
				object M(Other other) => other.Pack(1);
				object N(Other other) => other.{|#0:Encode|}(1);
			}
			""", Compiler("CS1061", 0));

		[Test]
		public Task Ignores_An_Unknown_Member_On_A_Subspace() => Verify.Analyzer<RemovedApiAnalyzer>(Usings + """
			class C
			{
				object M(IKeySubspace subspace) => subspace.{|#0:Frobnicate|}();
			}
			""", Compiler("CS1061", 0));

		[Test]
		public Task Fixes_Renames() => Verify.CodeFixOfCompilerError<RemovedApiAnalyzer, RemovedApiCodeFix>(Usings + """
			class C
			{
				object M1(IKeySubspace subspace, int id) => subspace.{|#0:Pack|}(id);
				object M2(IKeySubspace subspace, int id) => subspace.{|#1:Encode|}(id);
				object M3(IKeySubspace subspace, int id) => subspace.{|#2:ByKey|}(id);
				object M4(IFdbDatabaseProvider provider, CancellationToken ct) => provider.{|#3:GetDatabaseAsync|}(ct);
				object M5(ISubspaceLocation location, IFdbReadOnlyTransaction tr) => location.{|#4:ResolveAsync|}(tr);
				object M6(Slice key) => KeySelector.{|#5:FirstAfter|}(key);
			}
			""", Usings + """
			class C
			{
				object M1(IKeySubspace subspace, int id) => subspace.Key(id);
				object M2(IKeySubspace subspace, int id) => subspace.Key(id);
				object M3(IKeySubspace subspace, int id) => subspace.Key(id);
				object M4(IFdbDatabaseProvider provider, CancellationToken ct) => provider.GetDatabase(ct);
				object M5(ISubspaceLocation location, IFdbReadOnlyTransaction tr) => location.Resolve(tr);
				object M6(Slice key) => KeySelector.FirstGreaterThan(key);
			}
			""",
			Compiler("CS1061", 0), Fdb(0, "Pack", "Use subspace.Key(...) instead."),
			Compiler("CS1061", 1), Fdb(1, "Encode", "Use subspace.Key(...) instead."),
			Compiler("CS1061", 2), Fdb(2, "ByKey", "Use subspace.Key(a, b) instead."),
			Compiler("CS1061", 3), Fdb(3, "GetDatabaseAsync", "Use GetDatabase(ct), or call ReadAsync, WriteAsync, or ReadWriteAsync on the provider directly."),
			Compiler("CS1061", 4), Fdb(4, "ResolveAsync", "Use Resolve(tr) instead."),
			Compiler("CS0117", 5), Fdb(5, "FirstAfter", "Use KeySelector.FirstGreaterThan(key) instead."));

		[Test]
		public Task Fixes_EncodeRange_And_The_Indexer() => Verify.CodeFixOfCompilerError<RemovedApiAnalyzer, RemovedApiCodeFix>(Usings + """
			class C
			{
				object M1(IKeySubspace subspace, int id) => subspace.{|#0:EncodeRange|}(id);
				object M2(IKeySubspace subspace, string a, int b) => {|#1:subspace[a, b]|};
			}
			""", Usings + """
			class C
			{
				object M1(IKeySubspace subspace, int id) => subspace.Key(id).ToRange();
				object M2(IKeySubspace subspace, string a, int b) => subspace.Key(a, b);
			}
			""",
			Compiler("CS1061", 0), Fdb(0, "EncodeRange", "Use subspace.Key(...).ToRange() instead."),
			Compiler("CS0021", 1), Fdb(1, "subspace[...]", "Use subspace.Key(a, b) instead."));

		[Test]
		public Task Offers_No_Fix_For_Partition() => Verify.CodeFix<RemovedApiAnalyzer, RemovedApiCodeFix>(Usings + """
			class C
			{
				object M(IKeySubspace subspace) => subspace.{|#0:Partition|};
			}
			""", Usings + """
			class C
			{
				object M(IKeySubspace subspace) => subspace.{|#0:Partition|};
			}
			""", Compiler("CS1061", 0), Fdb(0, "Partition", "Use subspace.Key(x).ToSubspace() instead."));

		#endregion

		#region SBK0100...

		[Test]
		public Task Reports_Removed_Slice_Factories() => Verify.Analyzer<RemovedSliceApiAnalyzer>("""
			using System;
			class C
			{
				Slice M1(byte[] bytes) => Slice.{|#0:Create|}(bytes);
				Slice M2(ReadOnlySpan<byte> span) => Slice.{|#1:FromSpan|}(span);
				Slice M3(string text) => Slice.{|#2:FromAscii|}(text);
			}
			""",
			Compiler("CS7036", 0), Sbk(0, "Create", "Use Slice.Copy(bytes) or Slice.FromBytes(span) instead."),
			Compiler("CS0117", 1), Sbk(1, "FromSpan", "Use Slice.FromBytes(span) instead."),
			Compiler("CS0117", 2), Sbk(2, "FromAscii", "Use Slice.FromStringAscii(text) instead."));

		[Test]
		public Task Reports_Create_With_A_Size_Without_A_Fix() => Verify.CodeFix<RemovedSliceApiAnalyzer, RemovedSliceApiCodeFix>("""
			using System;
			class C
			{
				Slice M() => Slice.{|#0:Create|}(8);
			}
			""", """
			using System;
			class C
			{
				Slice M() => Slice.{|#0:Create|}(8);
			}
			""", Compiler("CS7036", 0), Sbk(0, "Create", "Use Slice.Copy(bytes) or Slice.FromBytes(span) instead."));

		[Test]
		public Task Ignores_An_Unknown_Slice_Member() => Verify.Analyzer<RemovedSliceApiAnalyzer>("""
			using System;
			class C
			{
				Slice M(byte[] bytes) => Slice.{|#0:Frobnicate|}(bytes);
			}
			""", Compiler("CS0117", 0));

		[Test]
		public Task Fixes_Slice_Factory_Renames() => Verify.CodeFixOfCompilerError<RemovedSliceApiAnalyzer, RemovedSliceApiCodeFix>("""
			using System;
			class C
			{
				Slice M1(byte[] bytes) => Slice.{|#0:Create|}(bytes);
				Slice M2(ReadOnlySpan<byte> span) => Slice.{|#1:FromSpan|}(span);
				Slice M3(string text) => Slice.{|#2:FromAscii|}(text);
			}
			""", """
			using System;
			class C
			{
				Slice M1(byte[] bytes) => Slice.Copy(bytes);
				Slice M2(ReadOnlySpan<byte> span) => Slice.FromBytes(span);
				Slice M3(string text) => Slice.FromStringAscii(text);
			}
			""",
			Compiler("CS7036", 0), Sbk(0, "Create", "Use Slice.Copy(bytes) or Slice.FromBytes(span) instead."),
			Compiler("CS0117", 1), Sbk(1, "FromSpan", "Use Slice.FromBytes(span) instead."),
			Compiler("CS0117", 2), Sbk(2, "FromAscii", "Use Slice.FromStringAscii(text) instead."));

		#endregion

	}
}
