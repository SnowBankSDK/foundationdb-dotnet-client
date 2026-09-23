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
	public class SliceRoundTripFacts
	{

		private const string Usings = """
			using System;
			using System.Text;

			""";

		private static DiagnosticResult Expected(int location, string expression, string replacement) => Verify.Diagnostic(SbkDescriptors.SliceRoundTrip).WithLocation(location).WithArguments(expression, replacement);

		[Test]
		public Task Reports_Read_Round_Trips() => Verify.Analyzer<SliceRoundTripAnalyzer>(Usings + """
			class C
			{
				void M(Slice s, (Slice Key, Slice Value) pair)
				{
					_ = {|#0:Encoding.UTF8.GetString(s.ToArray())|};
					_ = {|#1:Encoding.UTF8.GetString(pair.Value.GetBytes())|};
					_ = {|#2:BitConverter.ToInt64(s.ToArray(), 0)|};
					_ = {|#3:BitConverter.ToInt64(s.GetBytes(), 0)|};
				}
			}
			""",
			Expected(0, "Encoding.UTF8.GetString(s.ToArray())", "s.ToStringUtf8()"),
			Expected(1, "Encoding.UTF8.GetString(pair.Value.GetBytes())", "pair.Value.ToStringUtf8()"),
			Expected(2, "BitConverter.ToInt64(s.ToArray(), 0)", "s.ToInt64()"),
			Expected(3, "BitConverter.ToInt64(s.GetBytes(), 0)", "s.ToInt64()"));

		[Test]
		public Task Reports_Write_Round_Trips() => Verify.Analyzer<SliceRoundTripAnalyzer>(Usings + """
			class C
			{
				void M(string text)
				{
					_ = {|#0:Encoding.UTF8.GetBytes(text).AsSlice()|};
					_ = {|#1:Slice.Copy(Encoding.UTF8.GetBytes(text))|};
					_ = {|#2:Slice.FromBytes(Encoding.UTF8.GetBytes(text + "!"))|};
				}
			}
			""",
			Expected(0, "Encoding.UTF8.GetBytes(text).AsSlice()", "Slice.FromStringUtf8(text)"),
			Expected(1, "Slice.Copy(Encoding.UTF8.GetBytes(text))", "Slice.FromStringUtf8(text)"),
			Expected(2, "Slice.FromBytes(Encoding.UTF8.GetBytes(text + \"!\"))", "Slice.FromStringUtf8(text + \"!\")"));

		[Test]
		public Task Ignores_Other_Forms() => Verify.Analyzer<SliceRoundTripAnalyzer>(Usings + """
			class C
			{
				void M(Slice s, byte[] bytes, string text)
				{
					_ = Encoding.UTF8.GetString(s.Span);
					_ = Encoding.UTF8.GetString(bytes);
					_ = Encoding.ASCII.GetString(s.ToArray());
					_ = BitConverter.ToInt64(s.ToArray(), 8);
					_ = BitConverter.ToInt32(s.ToArray(), 0);
					_ = new Guid(s.ToArray());
					_ = s.ToStringUtf8();
					_ = s.ToInt64();
					_ = Slice.FromStringUtf8(text);
					_ = Encoding.UTF8.GetBytes(text);
					_ = Slice.Copy(bytes);
					_ = Encoding.ASCII.GetBytes(text).AsSlice();
				}
			}
			""");

		[Test]
		public Task Fixes_Read_And_Write_Forms() => Verify.CodeFix<SliceRoundTripAnalyzer, SliceRoundTripCodeFix>(Usings + """
			class C
			{
				void M(Slice s, string text)
				{
					var a = {|#0:Encoding.UTF8.GetString(s.ToArray())|};
					var b = {|#1:BitConverter.ToInt64(s.GetBytes(), 0)|};
					var c = {|#2:Encoding.UTF8.GetBytes(text).AsSlice()|};
					var d = {|#3:System.Slice.Copy(Encoding.UTF8.GetBytes(text))|};
				}
			}
			""", Usings + """
			class C
			{
				void M(Slice s, string text)
				{
					var a = s.ToStringUtf8();
					var b = s.ToInt64();
					var c = Slice.FromStringUtf8(text);
					var d = System.Slice.FromStringUtf8(text);
				}
			}
			""",
			Expected(0, "Encoding.UTF8.GetString(s.ToArray())", "s.ToStringUtf8()"),
			Expected(1, "BitConverter.ToInt64(s.GetBytes(), 0)", "s.ToInt64()"),
			Expected(2, "Encoding.UTF8.GetBytes(text).AsSlice()", "Slice.FromStringUtf8(text)"),
			Expected(3, "System.Slice.Copy(Encoding.UTF8.GetBytes(text))", "System.Slice.FromStringUtf8(text)"));

	}
}
