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
	public class MutationEncodingFacts
	{

		private const string Usings = """
			using System;
			using System.Threading;
			using System.Threading.Tasks;
			using FoundationDB.Client;

			""";

		private static DiagnosticResult Atomic(int location, string value) => Verify.Diagnostic(FdbDescriptors.ByteOrderAtomic).WithLocation(location).WithArguments(value);

		private static DiagnosticResult Stamped(int location) => Verify.Diagnostic(FdbDescriptors.VersionStampedKeySet).WithLocation(location);

		#region FDB1003...

		[Test]
		public Task Reports_Utf8_Text_On_AtomicMax() => Verify.Analyzer<MutationEncodingAnalyzer>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, IKeySubspace subspace, string name) => tr.AtomicMax(subspace.Key("name"), {|#0:FdbValue.ToTextUtf8(name)|});
			}
			""", Atomic(0, "FdbValue.ToTextUtf8(name)"));

		[Test]
		public Task Reports_A_String_Slice_On_AtomicMin() => Verify.Analyzer<MutationEncodingAnalyzer>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, Slice key, string s) => tr.AtomicMin(key, {|#0:Slice.FromString(s)|});
			}
			""", Atomic(0, "Slice.FromString(s)"));

		[Test]
		public Task Reports_Other_Byte_Order_Encodings() => Verify.Analyzer<MutationEncodingAnalyzer>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, Slice key, string name, Guid g)
				{
					tr.AtomicMax(key, {|#0:FdbValue.ToTextUtf16(name)|});
					tr.AtomicMax(key, {|#1:Slice.FromGuid(g)|});
					tr.AtomicMin(key, {|#2:Slice.FromStringAscii(name)|});
				}
			}
			""", Atomic(0, "FdbValue.ToTextUtf16(name)"), Atomic(1, "Slice.FromGuid(g)"), Atomic(2, "Slice.FromStringAscii(name)"));

		[Test]
		public Task Ignores_The_Byte_Order_Mutation() => Verify.Analyzer<MutationEncodingAnalyzer>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, IKeySubspace subspace, string name) => tr.Atomic(subspace.Key("name"), FdbValue.ToTextUtf8(name), FdbMutationType.ByteMax);
			}
			""");

		[Test]
		public Task Ignores_A_Plain_Value() => Verify.Analyzer<MutationEncodingAnalyzer>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, Slice key, Slice value, long n)
				{
					tr.AtomicMax(key, value);
					tr.AtomicMin(key, Slice.FromFixed64(n));
				}
			}
			""");

		[Test]
		public Task Fixes_AtomicMax_With_A_Typed_Key() => Verify.CodeFix<MutationEncodingAnalyzer, MutationEncodingCodeFix>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, IKeySubspace subspace, string name) => tr.AtomicMax(subspace.Key("name"), {|#0:FdbValue.ToTextUtf8(name)|});
			}
			""", Usings + """
			class C
			{
				void M(IFdbTransaction tr, IKeySubspace subspace, string name) => tr.Atomic(subspace.Key("name"), FdbValue.ToTextUtf8(name), FdbMutationType.ByteMax);
			}
			""", Atomic(0, "FdbValue.ToTextUtf8(name)"));

		[Test]
		public Task Fixes_AtomicMin_With_Slices() => Verify.CodeFix<MutationEncodingAnalyzer, MutationEncodingCodeFix>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, Slice key, string s) => tr.AtomicMin(key, {|#0:Slice.FromString(s)|});
			}
			""", Usings + """
			class C
			{
				void M(IFdbTransaction tr, Slice key, string s) => tr.Atomic(key, Slice.FromString(s), FdbMutationType.ByteMin);
			}
			""", Atomic(0, "Slice.FromString(s)"));

		[Test]
		public Task Offers_No_Fix_For_A_Slice_Key_With_A_Typed_Value() => Verify.CodeFix<MutationEncodingAnalyzer, MutationEncodingCodeFix>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, Slice key, string name) => tr.AtomicMax(key, {|#0:FdbValue.ToTextUtf8(name)|});
			}
			""", Usings + """
			class C
			{
				void M(IFdbTransaction tr, Slice key, string name) => tr.AtomicMax(key, {|#0:FdbValue.ToTextUtf8(name)|});
			}
			""", Atomic(0, "FdbValue.ToTextUtf8(name)"));

		#endregion

		#region FDB1006...

		[Test]
		public Task Reports_Set_With_A_Stamp_Through_A_Local() => Verify.Analyzer<MutationEncodingAnalyzer>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, IKeySubspace inbox, Slice payload)
				{
					var stamp = tr.CreateVersionStamp();
					tr.{|#0:Set|}(inbox.Key(stamp), payload);
				}
			}
			""", Stamped(0));

		[Test]
		public Task Reports_Set_With_A_Direct_Stamp() => Verify.Analyzer<MutationEncodingAnalyzer>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, IKeySubspace inbox, Slice payload) => tr.{|#0:Set|}(inbox.Key(tr.CreateUniqueVersionStamp()), payload);
			}
			""", Stamped(0));

		[Test]
		public Task Ignores_SetVersionStampedKey() => Verify.Analyzer<MutationEncodingAnalyzer>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, IKeySubspace inbox, Slice payload)
				{
					var stamp = tr.CreateVersionStamp();
					tr.SetVersionStampedKey(inbox.Key(stamp), payload);
				}
			}
			""");

		[Test]
		public Task Ignores_A_Stamp_From_Another_Method() => Verify.Analyzer<MutationEncodingAnalyzer>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, IKeySubspace inbox, VersionStamp stamp, Slice payload) => tr.Set(inbox.Key(stamp), payload);
			}
			""");

		[Test]
		public Task Fixes_Set_To_SetVersionStampedKey() => Verify.CodeFix<MutationEncodingAnalyzer, MutationEncodingCodeFix>(Usings + """
			class C
			{
				void M(IFdbTransaction tr, IKeySubspace inbox, Slice payload)
				{
					var stamp = tr.CreateVersionStamp();
					tr.{|#0:Set|}(inbox.Key(stamp), payload);
				}
			}
			""", Usings + """
			class C
			{
				void M(IFdbTransaction tr, IKeySubspace inbox, Slice payload)
				{
					var stamp = tr.CreateVersionStamp();
					tr.SetVersionStampedKey(inbox.Key(stamp), payload);
				}
			}
			""", Stamped(0));

		#endregion

	}
}
