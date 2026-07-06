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

// ReSharper disable StringLiteralTypo

namespace SnowBank.Data.Json.Tests
{

	/// <summary>Tests that the CrystalJson text parser handles malformed or hostile input safely: buffer over-reads, unbounded recursion, integer overflow, and edge-case Unicode code points.</summary>
	public partial class CrystalJsonTest
	{

		#region Nesting depth

		// Object and array parsing is recursive, so a deeply nested document could exhaust the thread stack; a
		// StackOverflowException cannot be caught and would terminate the process. Nesting is therefore bounded by
		// CrystalJsonParser<TReader>.MaximumDepth, and anything deeper is rejected with a catchable JsonSyntaxException.

		[Test]
		public void Test_Parse_DeeplyNested_Array_Should_Throw_And_Not_Crash()
		{
			string deep = new string('[', 100_000);
			Assert.That(() => JsonValue.Parse(deep), Throws.InstanceOf<JsonSyntaxException>());
		}

		[Test]
		public void Test_Parse_DeeplyNested_Object_Should_Throw_And_Not_Crash()
		{
			// nesting reaches the depth limit before the document even ends: {"a":{"a":{"a": ...
			var sb = new StringBuilder(600_000);
			for (int i = 0; i < 100_000; i++)
			{
				sb.Append("{\"a\":");
			}
			string deep = sb.ToString();
			Assert.That(() => JsonValue.Parse(deep), Throws.InstanceOf<JsonSyntaxException>());
		}

		[Test]
		public void Test_Parse_ModeratelyNested_Should_Succeed()
		{
			// a legitimately deep-but-reasonable document parses fine: 50 levels is well under the limit
			const int Depth = 50;
			string doc = new string('[', Depth) + new string(']', Depth);
			Assert.That(() => JsonValue.Parse(doc), Throws.Nothing);
		}

		#endregion

		#region Malformed UTF-8

		// A slice may be a window into a larger buffer, so decoding must never read past the slice bounds. A multi-byte
		// UTF-8 sequence that is truncated at the end of the slice is malformed and is rejected with an
		// InvalidDataException, without consulting any byte beyond the slice.

		[Test]
		public void Test_Parse_Slice_TruncatedUtf8_Should_Not_Read_Past_Slice()
		{
			// the backing array holds a complete "é" (22 C3 A9 22), but the parser is handed only a 2-byte window: the
			// opening quote and the lead byte 0xC3. The continuation byte 0xA9 and the closing quote are outside the
			// window, so the string is truncated mid-sequence and must be rejected without reading backing[2].
			byte[] backing = [ (byte) '"', 0xC3, 0xA9, (byte) '"' ];
			Slice window = backing.AsSlice(0, 2);

			Assert.That(() => JsonValue.Parse(window), Throws.InstanceOf<InvalidDataException>());
		}

		[Test]
		public void Test_Parse_Slice_TruncatedUtf8_Result_Must_Not_Depend_On_OutOfSlice_Bytes()
		{
			// the same 2-byte window ["  0xC3] in two buffers whose next byte differs; that byte is outside the document,
			// so it must not influence the outcome: both inputs are truncated mid-sequence and rejected the same way.
			byte[] backingValidCont   = [ (byte) '"', 0xC3, 0xA9, (byte) '"' ]; // 0xA9 is a valid continuation byte
			byte[] backingInvalidCont = [ (byte) '"', 0xC3, 0x2E, (byte) '"' ]; // 0x2E ('.') is NOT a continuation byte

			using var _ = Assert.EnterMultipleScope();
			Assert.That(() => JsonValue.Parse(backingValidCont.AsSlice(0, 2)), Throws.InstanceOf<InvalidDataException>(), "valid-continuation trailing byte");
			Assert.That(() => JsonValue.Parse(backingInvalidCont.AsSlice(0, 2)), Throws.InstanceOf<InvalidDataException>(), "invalid-continuation trailing byte");
		}

		#endregion

		#region Large integers

		// An integer literal preserves its magnitude even when it does not fit in a UInt64. Values are checked against
		// the true magnitude (via double.Parse) so that a wrap-around in the digit accumulator would be caught.

		[Test]
		public void Test_Parse_Number_Overflowing_UInt64_Must_Not_Silently_Wrap()
		{
			// 2^64 does not fit in a UInt64
			const string Literal = "18446744073709551616";
			double expected = double.Parse(Literal, CultureInfo.InvariantCulture);
			var parsed = JsonValue.Parse(Literal);

			using var _ = Assert.EnterMultipleScope();
			Assert.That(parsed, Is.Not.EqualTo(JsonNumber.Zero), "2^64 must not wrap to 0");
			Assert.That(parsed.As<double>(), Is.EqualTo(expected).Within(0.0001).Percent, "parsed magnitude must match the literal");
		}

		[Test]
		public void Test_Parse_Number_20DigitInteger_Must_Not_Silently_Wrap()
		{
			// 10^20 - 1 exceeds UInt64.MaxValue
			const string Literal = "99999999999999999999";
			double expected = double.Parse(Literal, CultureInfo.InvariantCulture);
			Assert.That(JsonValue.Parse(Literal).As<double>(), Is.EqualTo(expected).Within(0.0001).Percent);
		}

		#endregion

		#region Unicode code points

		// A JSON string may contain any Unicode code point, including the U+FFFF noncharacter and characters outside the
		// BMP. U+FFFF is not confused with end-of-stream, and astral characters round-trip as a UTF-16 surrogate pair.

		[Test]
		public void Test_Parse_String_Containing_U_FFFF_Is_Not_EndOfStream()
		{
			using var _ = Assert.EnterMultipleScope();
			// U+FFFF is a valid (noncharacter) code point and is legal inside a JSON string
			Assert.That(() => JsonValue.Parse("\"￿\""), Throws.Nothing, "U+FFFF inside a string must not be treated as end-of-stream");
			Assert.That(() => JsonValue.Parse("\"￿\"").As<string>(), Is.EqualTo("￿"));
		}

		[Test]
		public void Test_Parse_Astral_Character_From_Utf8_Is_Not_Truncated()
		{
			// U+1F600 (😀) encodes to the 4 UTF-8 bytes F0 9F 98 80, i.e. the UTF-16 surrogate pair D83D DE00
			const string Emoji = "😀";
			byte[] utf8 = Encoding.UTF8.GetBytes("\"" + Emoji + "\"");

			using var _ = Assert.EnterMultipleScope();
			// exercise both byte parse paths: byte[] (JsonSliceReader) and ReadOnlySpan<byte> (JsonUnmanagedReader)
			Assert.That(JsonValue.Parse(utf8).As<string>(), Is.EqualTo(Emoji), "byte[] parse path");
			Assert.That(JsonValue.Parse(utf8.AsSpan()).As<string>(), Is.EqualTo(Emoji), "ReadOnlySpan<byte> parse path");
		}

		#endregion

		#region Comments

		[Test]
		public void Test_Parse_Comment_With_NonAscii_Content_Is_Skipped()
		{
			// a comment body may contain any character, including U+FFFF and astral code points; they are skipped like
			// any other comment content and do not terminate the comment early
			using var _ = Assert.EnterMultipleScope();
			Assert.That(JsonValue.Parse("{ /* x￿y */ \"a\": 1 }")["a"].As<int>(), Is.EqualTo(1), "multi-line + U+FFFF");
			Assert.That(JsonValue.Parse("{ /* x😀y */ \"a\": 1 }")["a"].As<int>(), Is.EqualTo(1), "multi-line + astral");
			Assert.That(JsonValue.Parse("{ \"a\": 1 // x￿y\n }")["a"].As<int>(), Is.EqualTo(1), "single-line + U+FFFF");
			Assert.That(JsonValue.Parse("{ \"a\": 1 // x😀y\n }")["a"].As<int>(), Is.EqualTo(1), "single-line + astral");
		}

		[Test]
		public void Test_Parse_MultiLineComment_Closes_On_Repeated_Stars()
		{
			// a run of '*' immediately before the closing '/' still closes the comment
			using var _ = Assert.EnterMultipleScope();
			Assert.That(JsonValue.Parse("{ /**/ \"a\": 1 }")["a"].As<int>(), Is.EqualTo(1), "/**/");
			Assert.That(JsonValue.Parse("{ /* x **/ \"a\": 1 }")["a"].As<int>(), Is.EqualTo(1), "**/");
			Assert.That(JsonValue.Parse("{ /* x ***/ \"a\": 1 }")["a"].As<int>(), Is.EqualTo(1), "***/");
		}

		[Test]
		public void Test_Parse_Comments_Can_Be_Rejected_Via_Settings()
		{
			const string WithBlock = "{ /* c */ \"a\": 1 }";
			const string WithLine = "{ \"a\": 1 // c\n }";

			using var _ = Assert.EnterMultipleScope();

			// comments are allowed by default, including when no settings are supplied
			Assert.That(JsonValue.Parse(WithBlock)["a"].As<int>(), Is.EqualTo(1), "default allows block comments");
			Assert.That(JsonValue.Parse(WithBlock, default(CrystalJsonSettings))["a"].As<int>(), Is.EqualTo(1), "null settings allow comments");

			// JsonStrict rejects comments
			Assert.That(() => JsonValue.Parse(WithBlock, CrystalJsonSettings.JsonStrict), Throws.InstanceOf<JsonSyntaxException>(), "JsonStrict rejects block comments");
			Assert.That(() => JsonValue.Parse(WithLine, CrystalJsonSettings.JsonStrict), Throws.InstanceOf<JsonSyntaxException>(), "JsonStrict rejects line comments");

			// explicit opt-out, and opt back in on top of a strict base
			Assert.That(() => JsonValue.Parse(WithBlock, CrystalJsonSettings.Json.WithoutComments()), Throws.InstanceOf<JsonSyntaxException>(), "WithoutComments() rejects comments");
			Assert.That(JsonValue.Parse(WithBlock, CrystalJsonSettings.JsonStrict.WithComments())["a"].As<int>(), Is.EqualTo(1), "WithComments() re-enables comments");

			// the flag round-trips
			Assert.That(CrystalJsonSettings.Json.DenyComments, Is.False, "default DenyComments is false");
			Assert.That(CrystalJsonSettings.JsonStrict.DenyComments, Is.True, "JsonStrict DenyComments is true");
			Assert.That(CrystalJsonSettings.Json.WithoutComments().DenyComments, Is.True);
			Assert.That(CrystalJsonSettings.JsonStrict.WithComments().DenyComments, Is.False);
		}

		#endregion

	}

}
