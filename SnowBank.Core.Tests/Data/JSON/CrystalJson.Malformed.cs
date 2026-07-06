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

	public partial class CrystalJsonTest
	{

		#region Malformed input corpus

		// A corpus of edge-case and hostile inputs modelled on the well-known JSON conformance suites (the json.org
		// "json_checker" vectors and Nicolas Seriot's JSONTestSuite y_/n_/i_ cases), plus classic injection/DoS shapes.
		//
		// The invariant under test: for ANY input, the parser must either return a value or throw a *catchable*
		// JsonSyntaxException / InvalidDataException. It must never leak an internal exception (IndexOutOfRange,
		// Overflow, NullReference, ...), crash the process, or hang.
		//
		// Each case carries an expected verdict:
		//   true  => must parse (return a value)
		//   false => must be rejected (throw)
		//   null  => implementation-defined: CrystalJson's dialect is intentionally lenient (comments, trailing commas,
		//            NaN/Infinity, ...), so either outcome is acceptable as long as it stays clean.

		private static readonly (string Name, string Json, bool? MustParse)[] TextCorpus =
		[
			// --- valid documents (must parse) ---
			("empty input", "", true), // parses to JsonNull.Missing
			("whitespace only", "  \t\r\n  ", true),
			("empty object", "{}", true),
			("empty array", "[]", true),
			("simple object", "{\"a\":1}", true),
			("nested mix", "{\"a\":[1,2,{\"b\":true,\"c\":null}]}", true),
			("string", "\"hello world\"", true),
			("string with escapes", "\"line1\\nline2\\ttab\\\"quote\\\\slash\"", true),
			("unicode escape", "\"caf\\u00e9\"", true),
			("surrogate pair escape", "\"\\uD83D\\uDE00\"", true),
			("nul via escape", "\"a\\u0000b\"", true),
			("integer", "123", true),
			("negative decimal exponent", "-1.25e-3", true),
			("zero", "0", true),
			("big integer (2^64)", "18446744073709551616", true),
			("booleans and null", "[true,false,null]", true),
			("deeply nested within limit", new string('[', 60) + new string(']', 60), true),
			("long string value", "\"" + new string('x', 20_000) + "\"", true),
			("long key", "{\"" + new string('k', 10_000) + "\":1}", true),

			// --- structurally invalid (must be rejected) ---
			("open array", "[", false),
			("array missing tail", "[1", false),
			("open object", "{", false),
			("object missing value", "{\"a\":", false),
			("object missing colon", "{\"a\" 1}", false),
			("stray close bracket", "]", false),
			("stray close brace", "}", false),
			("array double comma", "[1,,2]", false),
			("array leading comma", "[,1]", false),
			("array missing comma", "[1 2]", false),
			("object missing comma", "{\"a\":1 \"b\":2}", false),
			("lone comma", ",", false),
			("lone colon", ":", false),
			("truncated true", "tru", false),
			("truncated null", "nul", false),
			("capitalized True", "True", false),
			("bare word", "hello", false),
			("unquoted key", "{a:1}", false),
			("deeply nested beyond limit", new string('[', 5_000), false),

			// --- invalid numbers (must be rejected) ---
			("number trailing dot", "1.", false),
			("number double dot", "1.2.3", false),
			("number exponent no digits", "1e", false),
			("number exponent sign no digits", "1e+", false),
			("number double exponent", "1e2e3", false),
			("number hex prefix", "0x1F", false),
			("lone minus", "-", false),

			// --- invalid strings (must be rejected) ---
			("unterminated string", "\"abc", false),
			("string bad escape", "\"a\\x41\"", false),
			("string bad unicode escape", "\"\\uZZZZ\"", false),
			("string short unicode escape", "\"\\u12\"", false),
			("escaped quote then EOF", "\"\\\"", false),

			// --- implementation-defined / lenient dialect (either, but must be clean) ---
			("plus-prefixed number", "+1", null),
			("leading dot number", ".5", null),
			("leading zero", "01", null),
			("NaN", "NaN", null),
			("Infinity", "Infinity", null),
			("negative Infinity", "-Infinity", null),
			("huge exponent", "1e999", null),
			("400-digit integer", new string('9', 400), null),
			("trailing comma array", "[1,]", null),
			("trailing comma object", "{\"a\":1,}", null),
			("duplicate keys", "{\"a\":1,\"a\":2}", null),
			("line comment", "{\"a\":1 // c\n}", null),
			("block comment", "{ /* c */ \"a\":1}", null),
			("raw tab in string", "\"a\tb\"", null),
			("raw newline in string", "\"a\nb\"", null),
			("lone high surrogate escape", "\"\\uD800\"", null),
			("lone low surrogate escape", "\"\\uDC00\"", null),
			("trailing garbage after value", "1 garbage", false),
			("two top-level values", "[1][2]", false),
			("object then garbage", "{} xyz", false),
		];

		private static readonly (string Name, byte[] Bytes, bool? MustParse)[] ByteCorpus =
		[
			// --- valid ---
			("utf-8 BOM only", [0xEF, 0xBB, 0xBF], true), // -> Missing
			("utf-8 BOM + object", [0xEF, 0xBB, 0xBF, 0x7B, 0x7D], true), // {}
			("emoji bytes in string", [0x22, 0xF0, 0x9F, 0x98, 0x80, 0x22], true), // "😀"

			// --- malformed UTF-8 (must be rejected) ---
			("bare continuation byte", [0x80], false),
			("bare lead byte", [0xC3], false),
			("string truncated 2-byte lead", [0x22, 0xC3], false),
			("string invalid continuation", [0x22, 0xC3, 0x28, 0x22], false),
			("string truncated 4-byte sequence", [0x22, 0xF0, 0x9F, 0x22], false),
			("overlong 2-byte slash", [0x22, 0xC0, 0xAF, 0x22], false), // C0 AF is an overlong encoding of '/'
			("overlong 2-byte NUL", [0x22, 0xC0, 0x80, 0x22], false), // C0 80 is an overlong encoding of U+0000
			("overlong 3-byte slash", [0x22, 0xE0, 0x80, 0xAF, 0x22], false), // E0 80 AF is an overlong encoding of '/'
			("utf-8 encoded surrogate", [0x22, 0xED, 0xA0, 0x80, 0x22], false), // ED A0 80 encodes the surrogate U+D800
			("code point above U+10FFFF", [0x22, 0xF5, 0x80, 0x80, 0x80, 0x22], false), // F5 .. is beyond U+10FFFF

			// --- implementation-defined ---
			("utf-16 LE bytes (not utf-8)", [0x7B, 0x00, 0x7D, 0x00], null), // {\0}\0
		];

		[Test]
		public void Test_Parse_MalformedCorpus_Text()
		{
			using var _ = Assert.EnterMultipleScope();
			foreach (var (name, json, mustParse) in TextCorpus)
			{
				CheckClean(name, () => JsonValue.Parse(json), mustParse);
			}
		}

		[Test]
		public void Test_Parse_MalformedCorpus_Bytes()
		{
			using var _ = Assert.EnterMultipleScope();
			foreach (var (name, bytes, mustParse) in ByteCorpus)
			{
				// exercise both byte parse paths: byte[] (JsonSliceReader) and ReadOnlySpan<byte> (JsonUnmanagedReader)
				CheckClean($"{name} [byte[]]", () => JsonValue.Parse(bytes), mustParse);
				CheckClean($"{name} [span]", () => ParseSpan(bytes), mustParse);
			}
		}

		private static JsonValue ParseSpan(byte[] bytes) => JsonValue.Parse(bytes.AsSpan());

		[Test]
		public void Test_Parse_TrailingData_Is_Rejected_By_Default()
		{
			using var _ = Assert.EnterMultipleScope();

			// a single value followed only by whitespace is fine
			Assert.That(JsonValue.Parse("{}\r\n  ").Type, Is.EqualTo(JsonType.Object), "trailing whitespace is allowed");

			// any non-whitespace content after the first value is a syntax error (a second document, or garbage)
			Assert.That(() => JsonValue.Parse("1 garbage"), Throws.InstanceOf<JsonSyntaxException>(), "trailing garbage");
			Assert.That(() => JsonValue.Parse("[1][2]"), Throws.InstanceOf<JsonSyntaxException>(), "second document");
			Assert.That(() => JsonValue.Parse("{\"a\":1} {\"a\":2}"), Throws.InstanceOf<JsonSyntaxException>(), "two objects");
			Assert.That(() => JsonValue.Parse("null garbage"), Throws.InstanceOf<JsonSyntaxException>(), "keyword then garbage");

			// opt-out: WithTrailingData() parses only the first value and ignores the rest
			Assert.That(JsonValue.Parse("1 garbage", CrystalJsonSettings.Json.WithTrailingData()).As<int>(), Is.EqualTo(1), "opt-out parses first value");
			Assert.That(JsonValue.Parse("[1][2]", CrystalJsonSettings.Json.WithTrailingData()).ToJsonText(CrystalJsonSettings.JsonCompact), Is.EqualTo("[1]"), "opt-out parses first array");

			// the flag round-trips
			Assert.That(CrystalJsonSettings.Json.AllowTrailingData, Is.False, "default rejects trailing data");
			Assert.That(CrystalJsonSettings.JsonStrict.AllowTrailingData, Is.False, "JsonStrict rejects trailing data");
			Assert.That(CrystalJsonSettings.Json.WithTrailingData().AllowTrailingData, Is.True);
			Assert.That(CrystalJsonSettings.Json.WithTrailingData().WithoutTrailingData().AllowTrailingData, Is.False);
		}

		/// <summary>Runs <paramref name="parse"/> and asserts it either returns a value or throws a clean, catchable JSON exception, matching the expected verdict.</summary>
		private static void CheckClean(string name, Func<JsonValue> parse, bool? mustParse)
		{
			JsonValue? result = null;
			Exception? error = null;
			try
			{
				result = parse();
			}
			catch (Exception e)
			{
				error = e;
			}

			// A rejection must always use a clean, catchable JSON exception — never a leaked internal exception
			// (IndexOutOfRange, Overflow, NullReference, ...), and never a crash or a hang.
			if (error is not null)
			{
				Assert.That(error, Is.InstanceOf<JsonSyntaxException>().Or.InstanceOf<InvalidDataException>(), $"[{name}] must be rejected with a JSON syntax error, not {error.GetType().Name}: {error.Message}");
			}

			switch (mustParse)
			{
				case true:
				{
					Assert.That(error, Is.Null, $"[{name}] should parse, but was rejected: {error?.Message}");
					break;
				}
				case false:
				{
					Assert.That(error, Is.Not.Null, $"[{name}] should be rejected, but parsed to a {result?.Type}");
					break;
				}
				case null:
				{
					// implementation-defined: either outcome is acceptable, the clean-exception check above still applies
					break;
				}
			}
		}

		#endregion

	}

}
