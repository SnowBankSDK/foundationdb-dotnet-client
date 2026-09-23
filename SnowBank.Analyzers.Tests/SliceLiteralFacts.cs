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
	public class SliceLiteralFacts
	{

		private const string Usings = """
			using System;

			""";

		private static DiagnosticResult Ascii(int location, string text) => Verify.Diagnostic(SbkDescriptors.AsciiLiteralNotEncodable).WithLocation(location).WithArguments(text);

		private static DiagnosticResult Prefix(int location, string text) => Verify.Diagnostic(SbkDescriptors.BinaryPrefixAsUtf8).WithLocation(location).WithArguments(text);

		#region SBK0001...

		[Test]
		public Task Reports_FromStringAscii_With_A_Character_Above_0xFF() => Verify.Analyzer<SliceLiteralAnalyzer>(Usings + """
			class C
			{
				const string Prefix = "\u4e2d";
				Slice M() => Slice.FromStringAscii({|#0:"caf\u00e9 \u4e2d"|});
				Slice N() => Slice.FromStringAscii({|#1:Prefix + "/a"|});
			}
			""", Ascii(0, "\"caf\\u00e9 \\u4e2d\""), Ascii(1, "Prefix + \"/a\""));

		[Test]
		public Task Ignores_FromStringAscii_Of_Latin1_Text() => Verify.Analyzer<SliceLiteralAnalyzer>(Usings + """
			class C
			{
				Slice M() => Slice.FromStringAscii("hello");
				Slice N() => Slice.FromStringAscii("caf\u00e9 \xFF");
				Slice O(string text) => Slice.FromStringAscii(text);
			}
			""");

		[Test]
		public Task Fixes_FromStringAscii_To_FromStringUtf8() => Verify.CodeFix<SliceLiteralAnalyzer, SliceLiteralCodeFix>(Usings + """
			class C
			{
				Slice M() => Slice.FromStringAscii({|#0:"caf\u00e9 \u4e2d"|});
			}
			""", Usings + """
			class C
			{
				Slice M() => Slice.FromStringUtf8("caf\u00e9 \u4e2d");
			}
			""", Ascii(0, "\"caf\\u00e9 \\u4e2d\""));

		#endregion

		#region SBK1002...

		[Test]
		public Task Reports_FromString_With_A_Binary_Prefix() => Verify.Analyzer<SliceLiteralAnalyzer>(Usings + """
			class C
			{
				Slice M() => Slice.FromString({|#0:"\xFF/metadataVersion"|});
				Slice N() => Slice.FromStringUtf8({|#1:"\x80/abc"|});
				Slice O(string name) => Slice.FromString({|#2:$"\xFF/{name}"|});
			}
			""", Prefix(0, "\"\\xFF/metadataVersion\""), Prefix(1, "\"\\x80/abc\""), Prefix(2, "$\"\\xFF/{name}\""));

		[Test]
		public Task Ignores_FromString_Of_Text() => Verify.Analyzer<SliceLiteralAnalyzer>(Usings + """
			class C
			{
				Slice M() => Slice.FromString("hello");
				Slice N() => Slice.FromStringUtf8("caf\u00e9");
				Slice O(string name) => Slice.FromString($"{name}/\xFF");
				Slice P(string name) => Slice.FromString(name);
				Slice Q() => Slice.FromByteString("\xFF/metadataVersion");
				Slice R() => Slice.FromString("");
			}
			""");

		[Test]
		public Task Fixes_FromString_To_FromByteString() => Verify.CodeFix<SliceLiteralAnalyzer, SliceLiteralCodeFix>(Usings + """
			class C
			{
				Slice M() => Slice.FromString({|#0:"\xFF/metadataVersion"|});
				Slice N(string name) => Slice.FromStringUtf8({|#1:$"\xFE/{name}"|});
			}
			""", Usings + """
			class C
			{
				Slice M() => Slice.FromByteString("\xFF/metadataVersion");
				Slice N(string name) => Slice.FromByteString($"\xFE/{name}");
			}
			""", Prefix(0, "\"\\xFF/metadataVersion\""), Prefix(1, "$\"\\xFE/{name}\""));

		#endregion

	}
}
