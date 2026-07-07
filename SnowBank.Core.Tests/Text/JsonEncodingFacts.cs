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
// ReSharper disable VariableLengthStringHexEscapeSequence

namespace SnowBank.Text.Tests
{
	using SnowBank.Buffers;
	using SnowBank.Buffers.Text;

	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	[SetInvariantCulture]
	public class JsonEncodingFacts : SimpleTest
	{

		[Test]
		public void Test_JsonEncoding_Encode()
		{
			Assert.Multiple(() =>
			{
				Assert.That(JsonEncoding.Encode(null), Is.EqualTo("null"));
				Assert.That(JsonEncoding.Encode(""), Is.EqualTo(@""""""));
				Assert.That(JsonEncoding.Encode("foo"), Is.EqualTo(@"""foo"""));
				Assert.That(JsonEncoding.Encode("'"), Is.EqualTo(@"""'"""));
				Assert.That(JsonEncoding.Encode("\""), Is.EqualTo(@"""\"""""));
				Assert.That(JsonEncoding.Encode("A\""), Is.EqualTo(@"""A\"""""));
				Assert.That(JsonEncoding.Encode("\"A"), Is.EqualTo(@"""\""A"""));
				Assert.That(JsonEncoding.Encode("A\"A"), Is.EqualTo(@"""A\""A"""));
				Assert.That(JsonEncoding.Encode("AA\""), Is.EqualTo(@"""AA\"""""));
				Assert.That(JsonEncoding.Encode("AAA\""), Is.EqualTo(@"""AAA\"""""));
				Assert.That(JsonEncoding.Encode("A\0"), Is.EqualTo("\"A\\u0000\""));
				Assert.That(JsonEncoding.Encode("\0A"), Is.EqualTo("\"\\u0000A\""));
				Assert.That(JsonEncoding.Encode("A\0A"), Is.EqualTo("\"A\\u0000A\""));
				Assert.That(JsonEncoding.Encode("AA\0"), Is.EqualTo("\"AA\\u0000\""));
				Assert.That(JsonEncoding.Encode("AAA\0"), Is.EqualTo("\"AAA\\u0000\""));
				Assert.That(JsonEncoding.Encode("All Your Bases Are Belong To Us"), Is.EqualTo(@"""All Your Bases Are Belong To Us"""));
				Assert.That(JsonEncoding.Encode("<script>alert('narf!');</script>"), Is.EqualTo(@"""<script>alert('narf!');</script>"""));
				Assert.That(JsonEncoding.Encode("<script>alert(\"zort!\");</script>"), Is.EqualTo(@"""<script>alert(\""zort!\"");</script>"""));
				Assert.That(JsonEncoding.Encode("Test de text normal avec juste a la fin un '"), Is.EqualTo(@"""Test de text normal avec juste a la fin un '"""));
				Assert.That(JsonEncoding.Encode("Test de text normal avec juste a la fin un \""), Is.EqualTo(@"""Test de text normal avec juste a la fin un \"""""));
				Assert.That(JsonEncoding.Encode("'Test de text normal avec des quotes autour'"), Is.EqualTo(@"""'Test de text normal avec des quotes autour'"""));
				Assert.That(JsonEncoding.Encode("\"Test de text normal avec des double quotes autour\""), Is.EqualTo(@"""\""Test de text normal avec des double quotes autour\"""""));
				Assert.That(JsonEncoding.Encode("Test'de\"text'avec\"les'deux\"types"), Is.EqualTo("\"Test'de\\\"text'avec\\\"les'deux\\\"types\""));
				Assert.That(JsonEncoding.Encode("/"), Is.EqualTo(@"""/""")); // the slash must be left as-is (we reserve \/ for dates)
				Assert.That(JsonEncoding.Encode(@"/\/\\//\\\///"), Is.EqualTo(@"""/\\/\\\\//\\\\\\///"""));
				Assert.That(JsonEncoding.Encode("\x00\x01\x02\x03\x04\x05\x06\x07\x08\x09\x0A\x0B\x0C\x0D\x0E\x0F"), Is.EqualTo(@"""\u0000\u0001\u0002\u0003\u0004\u0005\u0006\u0007\b\t\n\u000b\f\r\u000e\u000f"""), "ASCII 0..15");
				Assert.That(JsonEncoding.Encode("\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1A\x1B\x1C\x1D\x1E\x1F"), Is.EqualTo(@"""\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001a\u001b\u001c\u001d\u001e\u001f"""), "ASCII 16..31");
				Assert.That(JsonEncoding.Encode(" !\"#$%&'()*+,-./"), Is.EqualTo(@""" !\""#$%&'()*+,-./"""), "ASCII 32..47");
				Assert.That(JsonEncoding.Encode(":;<=>?@"), Is.EqualTo(@""":;<=>?@"""), "ASCII 58..64");
				Assert.That(JsonEncoding.Encode("[\\]^_`"), Is.EqualTo(@"""[\\]^_`"""), "ASCII 91..96");
				Assert.That(JsonEncoding.Encode("{|}~"), Is.EqualTo(@"""{|}~"""), "ASCII 123..126");
				Assert.That(JsonEncoding.Encode("\x7F"), Is.EqualTo("\"\x7F\""), "ASCI 127");
				Assert.That(JsonEncoding.Encode("\x80"), Is.EqualTo("\"\x80\""), "ASCI 128");
				Assert.That(JsonEncoding.Encode("üéâäàåçêëèïîìÄÅÉæÆôöòûùÿÖÜø£Ø×ƒáíóúñÑªº¿®¬½¼¡«»░▒▓│┤ÁÂÀ©╣║╗╝¢¥┐└┴┬├─┼ãÃ"), Is.EqualTo(@"""üéâäàåçêëèïîìÄÅÉæÆôöòûùÿÖÜø£Ø×ƒáíóúñÑªº¿®¬½¼¡«»░▒▓│┤ÁÂÀ©╣║╗╝¢¥┐└┴┬├─┼ãÃ"""), "ASCI 129-199");
				Assert.That(JsonEncoding.Encode("╚╔╩╦╠═╬¤ðÐÊËÈıÍÎÏ┘┌█▄¦Ì▀ÓßÔÒõÕµþÞÚÛÙýÝ¯´­±‗¾¶§÷¸°¨·¹³²■"), Is.EqualTo(@"""╚╔╩╦╠═╬¤ðÐÊËÈıÍÎÏ┘┌█▄¦Ì▀ÓßÔÒõÕµþÞÚÛÙýÝ¯´­±‗¾¶§÷¸°¨·¹³²■"""), "ASCI 200-254");
				Assert.That(JsonEncoding.Encode("\xFF"), Is.EqualTo("\"\xFF\""), "ASCI 255");
				Assert.That(JsonEncoding.Encode("الصفحة_الرئيسية"), Is.EqualTo(@"""الصفحة_الرئيسية""")); // Arabic
				Assert.That(JsonEncoding.Encode("メインページ"), Is.EqualTo(@"""メインページ""")); // Japanese
				Assert.That(JsonEncoding.Encode("首页"), Is.EqualTo(@"""首页""")); // Chinese
				Assert.That(JsonEncoding.Encode("대문"), Is.EqualTo(@"""대문""")); // Korean
				Assert.That(JsonEncoding.Encode("Κύρια Σελίδα"), Is.EqualTo(@"""Κύρια Σελίδα""")); // Ellenika
				Assert.That(JsonEncoding.Encode("\uD7FF"), Is.EqualTo("\"\uD7FF\""), "Just before the non-BMP (D7FF)");
				Assert.That(JsonEncoding.Encode("\uD800\uDFFF"), Is.EqualTo(@"""\ud800\udfff"""), "non-BMP range: D800-DFFF");
				Assert.That(JsonEncoding.Encode("\uE000"), Is.EqualTo("\"\uE000\""), "Juste after the non-BMP (E000)");
				Assert.That(JsonEncoding.Encode("\uDF34"), Is.EqualTo("\"\\udf34\""));
				Assert.That(JsonEncoding.Encode("\xFFFE"), Is.EqualTo("\"\\ufffe\""), "BOM UTF-16 LE (FFFE)");
				Assert.That(JsonEncoding.Encode("\xFFFF"), Is.EqualTo("\"\\uffff\""), "BOM UTF-16 BE (FFFF)");
			});

			Assert.Multiple(() =>
			{
				// encoding unrolls by 8, then 4, then last 3, so we will move the invalid char allong the path
				Assert.That(JsonEncoding.Encode("\uFFFE0123456789abcd"), Is.EqualTo("\"\\ufffe0123456789abcd\""));
				Assert.That(JsonEncoding.Encode("0\uFFFE123456789abcd"), Is.EqualTo("\"0\\ufffe123456789abcd\""));
				Assert.That(JsonEncoding.Encode("01\uFFFE23456789abcd"), Is.EqualTo("\"01\\ufffe23456789abcd\""));
				Assert.That(JsonEncoding.Encode("012\uFFFE3456789abcd"), Is.EqualTo("\"012\\ufffe3456789abcd\""));
				Assert.That(JsonEncoding.Encode("0123\uFFFE456789abcd"), Is.EqualTo("\"0123\\ufffe456789abcd\""));
				Assert.That(JsonEncoding.Encode("01234\uFFFE56789abcd"), Is.EqualTo("\"01234\\ufffe56789abcd\""));
				Assert.That(JsonEncoding.Encode("012345\uFFFE6789abcd"), Is.EqualTo("\"012345\\ufffe6789abcd\""));
				Assert.That(JsonEncoding.Encode("0123456\uFFFE789abcd"), Is.EqualTo("\"0123456\\ufffe789abcd\""));
				Assert.That(JsonEncoding.Encode("01234567\uFFFE89abcd"), Is.EqualTo("\"01234567\\ufffe89abcd\""));
				Assert.That(JsonEncoding.Encode("012345678\uFFFE9abcd"), Is.EqualTo("\"012345678\\ufffe9abcd\""));
				Assert.That(JsonEncoding.Encode("0123456789\uFFFEabcd"), Is.EqualTo("\"0123456789\\ufffeabcd\""));
				Assert.That(JsonEncoding.Encode("0123456789A\uFFFEbcd"), Is.EqualTo("\"0123456789A\\ufffebcd\""));
				Assert.That(JsonEncoding.Encode("0123456789AB\uFFFEcd"), Is.EqualTo("\"0123456789AB\\ufffecd\""));
				Assert.That(JsonEncoding.Encode("0123456789ABC\uFFFEd"), Is.EqualTo("\"0123456789ABC\\ufffed\""));
				Assert.That(JsonEncoding.Encode("0123456789ABCD\uFFFE"), Is.EqualTo("\"0123456789ABCD\\ufffe\""));

				Assert.That(JsonEncoding.Encode("\\0123456789abcd"), Is.EqualTo("\"\\\\0123456789abcd\""));
				Assert.That(JsonEncoding.Encode("0\\123456789abcd"), Is.EqualTo("\"0\\\\123456789abcd\""));
				Assert.That(JsonEncoding.Encode("01\\23456789abcd"), Is.EqualTo("\"01\\\\23456789abcd\""));
				Assert.That(JsonEncoding.Encode("012\\3456789abcd"), Is.EqualTo("\"012\\\\3456789abcd\""));
				Assert.That(JsonEncoding.Encode("0123\\456789abcd"), Is.EqualTo("\"0123\\\\456789abcd\""));
				Assert.That(JsonEncoding.Encode("01234\\56789abcd"), Is.EqualTo("\"01234\\\\56789abcd\""));
				Assert.That(JsonEncoding.Encode("012345\\6789abcd"), Is.EqualTo("\"012345\\\\6789abcd\""));
				Assert.That(JsonEncoding.Encode("0123456\\789abcd"), Is.EqualTo("\"0123456\\\\789abcd\""));
				Assert.That(JsonEncoding.Encode("01234567\\89abcd"), Is.EqualTo("\"01234567\\\\89abcd\""));
				Assert.That(JsonEncoding.Encode("012345678\\9abcd"), Is.EqualTo("\"012345678\\\\9abcd\""));
				Assert.That(JsonEncoding.Encode("0123456789\\abcd"), Is.EqualTo("\"0123456789\\\\abcd\""));
				Assert.That(JsonEncoding.Encode("0123456789A\\bcd"), Is.EqualTo("\"0123456789A\\\\bcd\""));
				Assert.That(JsonEncoding.Encode("0123456789AB\\cd"), Is.EqualTo("\"0123456789AB\\\\cd\""));
				Assert.That(JsonEncoding.Encode("0123456789ABC\\d"), Is.EqualTo("\"0123456789ABC\\\\d\""));
				Assert.That(JsonEncoding.Encode("0123456789ABCD\\"), Is.EqualTo("\"0123456789ABCD\\\\\""));

				Assert.That(JsonEncoding.Encode("\"0123456789abcd"), Is.EqualTo("\"\\\"0123456789abcd\""));
				Assert.That(JsonEncoding.Encode("0\"123456789abcd"), Is.EqualTo("\"0\\\"123456789abcd\""));
				Assert.That(JsonEncoding.Encode("01\"23456789abcd"), Is.EqualTo("\"01\\\"23456789abcd\""));
				Assert.That(JsonEncoding.Encode("012\"3456789abcd"), Is.EqualTo("\"012\\\"3456789abcd\""));
				Assert.That(JsonEncoding.Encode("0123\"456789abcd"), Is.EqualTo("\"0123\\\"456789abcd\""));
				Assert.That(JsonEncoding.Encode("01234\"56789abcd"), Is.EqualTo("\"01234\\\"56789abcd\""));
				Assert.That(JsonEncoding.Encode("012345\"6789abcd"), Is.EqualTo("\"012345\\\"6789abcd\""));
				Assert.That(JsonEncoding.Encode("0123456\"789abcd"), Is.EqualTo("\"0123456\\\"789abcd\""));
				Assert.That(JsonEncoding.Encode("01234567\"89abcd"), Is.EqualTo("\"01234567\\\"89abcd\""));
				Assert.That(JsonEncoding.Encode("012345678\"9abcd"), Is.EqualTo("\"012345678\\\"9abcd\""));
				Assert.That(JsonEncoding.Encode("0123456789\"abcd"), Is.EqualTo("\"0123456789\\\"abcd\""));
				Assert.That(JsonEncoding.Encode("0123456789A\"bcd"), Is.EqualTo("\"0123456789A\\\"bcd\""));
				Assert.That(JsonEncoding.Encode("0123456789AB\"cd"), Is.EqualTo("\"0123456789AB\\\"cd\""));
				Assert.That(JsonEncoding.Encode("0123456789ABC\"d"), Is.EqualTo("\"0123456789ABC\\\"d\""));
				Assert.That(JsonEncoding.Encode("0123456789ABCD\""), Is.EqualTo("\"0123456789ABCD\\\"\""));
			});
		}

		[Test]
		public void Test_JsonEncoding_TryEncodeTo()
		{
			static void Verify(string? s)
			{
				var expectedChars = JsonEncoding.Encode(s);
				var expectedBytes = Encoding.UTF8.GetBytes(expectedChars);

				// to chars
				var chars = new char[expectedChars.Length + 16];
				Assert.That(JsonEncoding.TryEncodeTo(s, chars, out int charsWritten), Is.True, $"'{s}' => '{expectedChars}'");
				Assert.That(chars.AsSpan(0, charsWritten).ToString(), Is.EqualTo(expectedChars), $"'{s}' => '{expectedChars}'");

				var vsw = new ValueStringWriter(chars.Length);
				JsonEncoding.EncodeTo(ref vsw, s);
				Assert.That(vsw.ToString(), Is.EqualTo(expectedChars), $"'{s}' => '{expectedChars}'");
				vsw.Dispose();

				// to utf8
				var bytes = new byte[expectedBytes.Length + 16];
				Assert.That(JsonEncoding.TryEncodeTo(s, bytes, out int bytesWritten), Is.True, $"'{s}' => '{expectedChars}'");
				if (!bytes.AsSpan(0, bytesWritten).SequenceEqual(expectedBytes))
				{
					DumpVersus(bytes.AsSpan(0, bytesWritten), expectedBytes);
					Assert.That(bytes.AsSpan(0, bytesWritten).ToArray(), Is.EqualTo(expectedBytes), $"'{s}' => '{expectedChars}'");
				}

				var writer = new SliceWriter();
				JsonEncoding.EncodeTo(ref writer, s);
				if (!writer.ToSlice().Equals(expectedBytes))
				{
					DumpVersus(writer.ToSpan(), expectedBytes);
					Assert.That(writer.GetBytes(), Is.EqualTo(expectedBytes));
				}
			}

			Assert.Multiple(() =>
			{
				Verify("");
				Verify("foo");
				Verify("'");
				Verify("\"");
				Verify("A\"");
				Verify("\"A");
				Verify("A\"A");
				Verify("AA\"");
				Verify("AAA\"");
				Verify("A\0");
				Verify("\0A");
				Verify("A\0A");
				Verify("AA\0");
				Verify("AAA\0");
				Verify("All Your Bases Are Belong To Us");
				Verify("<script>alert('narf!');</script>");
				Verify("<script>alert(\"zort!\");</script>");
				Verify("Test de text normal avec juste a la fin un '");
				Verify("Test de text normal avec juste a la fin un \"");
				Verify("'Test de text normal avec des quotes autour'");
				Verify("\"Test de text normal avec des double quotes autour\"");
				Verify("Test'de\"text'avec\"les'deux\"types");
				Verify("/");
				Verify(@"/\/\\//\\\///");
				Verify("\x00\x01\x02\x03\x04\x05\x06\x07\x08\x09\x0A\x0B\x0C\x0D\x0E\x0F");
				Verify("\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1A\x1B\x1C\x1D\x1E\x1F");
				Verify(" !\"#$%&'()*+,-./");
				Verify(":;<=>?@");
				Verify("[\\]^_`");
				Verify("{|}~");
				Verify("\x7F");
				Verify("\x80");
				Verify("üéâäàåçêëèïîìÄÅÉæÆôöòûùÿÖÜø£Ø×ƒáíóúñÑªº¿®¬½¼¡«»░▒▓│┤ÁÂÀ©╣║╗╝¢¥┐└┴┬├─┼ãÃ");
				Verify("╚╔╩╦╠═╬¤ðÐÊËÈıÍÎÏ┘┌█▄¦Ì▀ÓßÔÒõÕµþÞÚÛÙýÝ¯´­±‗¾¶§÷¸°¨·¹³²■");
				Verify("\xFF");
				Verify("الصفحة_الرئيسية");
				Verify("メインページ");
				Verify("首页");
				Verify("대문");
				Verify("Κύρια Σελίδα");
				Verify("\uD7FF");
				Verify("\uD800\uDFFF");
				Verify("\uE000");
				Verify("\xFFFE");
				Verify("\xFFFF");
			});

			Assert.Multiple(() =>
			{
				// encoding unrolls by 8, then 4, then last 3, so we will move the invalid char allong the path
				Verify("\uFFFE0123456789abcd");
				Verify("0\uFFFE123456789abcd");
				Verify("01\uFFFE23456789abcd");
				Verify("012\uFFFE3456789abcd");
				Verify("0123\uFFFE456789abcd");
				Verify("01234\uFFFE56789abcd");
				Verify("012345\uFFFE6789abcd");
				Verify("0123456\uFFFE789abcd");
				Verify("01234567\uFFFE89abcd");
				Verify("012345678\uFFFE9abcd");
				Verify("0123456789\uFFFEabcd");
				Verify("0123456789A\uFFFEbcd");
				Verify("0123456789AB\uFFFEcd");
				Verify("0123456789ABC\uFFFEd");
				Verify("0123456789ABCD\uFFFE");

				Verify("\\0123456789abcd");
				Verify("0\\123456789abcd");
				Verify("01\\23456789abcd");
				Verify("012\\3456789abcd");
				Verify("0123\\456789abcd");
				Verify("01234\\56789abcd");
				Verify("012345\\6789abcd");
				Verify("0123456\\789abcd");
				Verify("01234567\\89abcd");
				Verify("012345678\\9abcd");
				Verify("0123456789\\abcd");
				Verify("0123456789A\\bcd");
				Verify("0123456789AB\\cd");
				Verify("0123456789ABC\\d");
				Verify("0123456789ABCD\\");

				Verify("\"0123456789abcd");
				Verify("0\"123456789abcd");
				Verify("01\"23456789abcd");
				Verify("012\"3456789abcd");
				Verify("0123\"456789abcd");
				Verify("01234\"56789abcd");
				Verify("012345\"6789abcd");
				Verify("0123456\"789abcd");
				Verify("01234567\"89abcd");
				Verify("012345678\"9abcd");
				Verify("0123456789\"abcd");
				Verify("0123456789A\"bcd");
				Verify("0123456789AB\"cd");
				Verify("0123456789ABC\"d");
				Verify("0123456789ABCD\"");
			});
		}

		[Test]
		public void Test_JsonEncoding_ComputeEscapedSize()
		{

			// with capacity hint (note: may be too large!)
			static unsafe void VerifyEscapingCapacity(string s)
			{
				string encoded = JsonEncoding.Encode(s);
				int capacity = JsonEncoding.ComputeEscapedSize(s, withQuotes: true);
				if (capacity == s.Length)
				{ // no encoding required
					Assert.That(encoded, Is.EqualTo("\"" + s + "\""), $"No escaping required for '{s}' => '{encoded}'");
				}
				else
				{
					Assert.That(capacity, Is.EqualTo(encoded.Length), $"Escaping required for '{s}' => '{encoded}'");
				}

				Span<char> buf = stackalloc char[2 + capacity + s.Length * 6];

				// with more than required
				buf.Fill('#');
				Assert.That(JsonEncoding.TryEncodeTo(s, buf, out int written), Is.True, $"Should have more than enough to encode '{s}' => '{encoded}'");
				Assert.That(written, Is.EqualTo(capacity));
				Assert.That(buf.Slice(0, written).SequenceEqual(encoded));
#if NET8_0_OR_GREATER
				Assert.That(buf.Slice(capacity + 2).IndexOfAnyExcept('#'), Is.EqualTo(-1), $"Should not have overflowed the buffer: {buf.Slice(capacity + 2).ToString()}");
#endif

				// with just enough
				buf.Fill('#');
				Assert.That(JsonEncoding.TryEncodeTo(s, buf.Slice(0, capacity), out written), Is.True, $"Should have EXACTLY enough to encode '{s}' => '{encoded}'");
				Assert.That(written, Is.EqualTo(capacity));
				Assert.That(buf.Slice(0, written).SequenceEqual(encoded));
#if NET8_0_OR_GREATER
				Assert.That(buf.Slice(capacity).IndexOfAnyExcept('#'), Is.EqualTo(-1), "Should not have overflowed the buffer");
#endif

				// not enough for the last quote
				buf.Fill('#');
				Assert.That(JsonEncoding.TryEncodeTo(s, buf.Slice(0, capacity - 1), out written), Is.False, $"Should have ONE LESS than required to encode '{s}' => '{encoded}'");
#if NET8_0_OR_GREATER
				Assert.That(buf.Slice(capacity - 1).IndexOfAnyExcept('#'), Is.EqualTo(-1), "Should not have overflowed the buffer");
#endif

				// with half required
				if (capacity > 2)
				{
					buf.Fill('#');
					Assert.That(JsonEncoding.TryEncodeTo(s, buf.Slice(0, capacity >> 1), out written), Is.False, $"Should NOT have enough to encode '{s}' => '{encoded}'");
#if NET8_0_OR_GREATER
					Assert.That(buf.Slice(capacity >> 1).IndexOfAnyExcept('#'), Is.EqualTo(-1), "Should not have overflowed the buffer");
#endif
				}

			}

			VerifyEscapingCapacity("a");
			VerifyEscapingCapacity("aaa");
			VerifyEscapingCapacity("aaaaaaa");
			VerifyEscapingCapacity("aaaaaaaa");
			VerifyEscapingCapacity("aaaaaaaaa");
			VerifyEscapingCapacity("aaaaaaaaaa");
			VerifyEscapingCapacity("aaaaaaaaaaa");
			VerifyEscapingCapacity("hello, world!");
			VerifyEscapingCapacity("hello\"world\\!!!");
			VerifyEscapingCapacity("A\0A");
			VerifyEscapingCapacity("AAAAAAAA\0");
			VerifyEscapingCapacity("AAAAAAAAA\0");
			VerifyEscapingCapacity("AAAAAAAA\0\0");
			VerifyEscapingCapacity("AAAAAAAAAA\0");
			VerifyEscapingCapacity("AAAAAAAA\0\0\0");
			VerifyEscapingCapacity("\0AAAAAAA");
			VerifyEscapingCapacity("\0\0AAAAAA");
			VerifyEscapingCapacity("\0\0\0AAAAA");
			VerifyEscapingCapacity("\0\0\0\0AAAA");
			VerifyEscapingCapacity("\x00\x01\x02\x03\x04\x05\x06\x07\x08\x09\x0A\x0B\x0C\x0D\x0E\x0F\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1A\x1B\x1C\x1D\x1E\x1F");
			VerifyEscapingCapacity("\uD7FF\uD800\uE000\uDF34\uFFFE\uFFFF");
		}

		[Test]
		public void Test_JsonEncoding_NeedsEscaping()
		{
			Assert.Multiple(() =>
			{
				Assert.That(JsonEncoding.NeedsEscaping("a"), Is.False, "LOWER CASE 'a'");
				Assert.That(JsonEncoding.NeedsEscaping("\""), Is.True, "DOUBLE QUOTE");
				Assert.That(JsonEncoding.NeedsEscaping("\\"), Is.True, "ANTI SLASH");
				Assert.That(JsonEncoding.NeedsEscaping("\x00"), Is.True, "ASCII NULL");
				Assert.That(JsonEncoding.NeedsEscaping("\x07"), Is.True, "ASCII 7");
				Assert.That(JsonEncoding.NeedsEscaping("\x1F"), Is.True, "ASCII 31");
				Assert.That(JsonEncoding.NeedsEscaping(" "), Is.False, "SPACE");
				Assert.That(JsonEncoding.NeedsEscaping("\uD7FF"), Is.False, "UNICODE 0xD7FF");
				Assert.That(JsonEncoding.NeedsEscaping("\uD800"), Is.True, "UNICODE 0xD800");
				Assert.That(JsonEncoding.NeedsEscaping("\uE000"), Is.False, "UNICODE 0xE000");
				Assert.That(JsonEncoding.NeedsEscaping("\uFFFD"), Is.False, "UNICODE 0xFFFD");
				Assert.That(JsonEncoding.NeedsEscaping("\uFFFE"), Is.True, "UNICODE 0xFFFE");
				Assert.That(JsonEncoding.NeedsEscaping("\uFFFF"), Is.True, "UNICODE 0xFFFF");

				Assert.That(JsonEncoding.NeedsEscaping("aa"), Is.False);
				Assert.That(JsonEncoding.NeedsEscaping("aaa"), Is.False);
				Assert.That(JsonEncoding.NeedsEscaping("aaaa"), Is.False);
				Assert.That(JsonEncoding.NeedsEscaping("aaaaa"), Is.False);
				Assert.That(JsonEncoding.NeedsEscaping("aaaaaa"), Is.False);
				Assert.That(JsonEncoding.NeedsEscaping("aaaaaaa"), Is.False);
				Assert.That(JsonEncoding.NeedsEscaping("a\""), Is.True);
				Assert.That(JsonEncoding.NeedsEscaping("aa\""), Is.True);
				Assert.That(JsonEncoding.NeedsEscaping("aaa\""), Is.True);
				Assert.That(JsonEncoding.NeedsEscaping("aaaa\""), Is.True);
				Assert.That(JsonEncoding.NeedsEscaping("aaaaa\""), Is.True);
				Assert.That(JsonEncoding.NeedsEscaping("aaaaaa\""), Is.True);
			});
		}

		[Test]
		public void Test_JsonEncoding_IndexOfFirstInvalidChar()
		{
			Assert.Multiple(() =>
			{
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("A"), Is.EqualTo(-1));
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("AA"), Is.EqualTo(-1));
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("AAA"), Is.EqualTo(-1));
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("AAAA"), Is.EqualTo(-1));
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("AAAAA"), Is.EqualTo(-1));
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("AAAAAAAA"), Is.EqualTo(-1));
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("AAAAAAAAAAAA"), Is.EqualTo(-1));
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("AAAAAAAAAAAAAAA"), Is.EqualTo(-1));
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("\""), Is.EqualTo(0));
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("A\""), Is.EqualTo(1));
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("AA\""), Is.EqualTo(2));
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("AAA\""), Is.EqualTo(3));
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("AAAA\""), Is.EqualTo(4));
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("AAAAAA\""), Is.EqualTo(6));
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("AAAAAAAAAAA\""), Is.EqualTo(11));
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("AAAAAAAAAAAAAA\""), Is.EqualTo(14));
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("\udf34"), Is.EqualTo(0));
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("A\udf34"), Is.EqualTo(1));
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("AA\udf34"), Is.EqualTo(2));
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("AAA\udf34"), Is.EqualTo(3));
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("AAAA\udf34"), Is.EqualTo(4));
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("AAAAAA\udf34"), Is.EqualTo(6));
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("AAAAAAAAAAA\udf34"), Is.EqualTo(11));
				Assert.That(JsonEncoding.IndexOfFirstInvalidChar("AAAAAAAAAAAAAA\udf34"), Is.EqualTo(14));
			});
		}

	}

}
