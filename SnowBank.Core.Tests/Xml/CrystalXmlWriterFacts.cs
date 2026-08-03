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

namespace SnowBank.Data.Xml.Tests
{
	using System.Buffers;
	using NUnit.Framework;
	using SnowBank.Data.Xml;

	/// <summary>Pins the byte-exact wire forms emitted by <see cref="CrystalXmlWriter{TRune,TWriter}"/></summary>
	/// <remarks>
	/// <para>Every rule below was measured against <c>DataContractSerializer</c> writing through a namespace-stripping
	/// <c>XmlWriter</c> (<c>CheckCharacters = false</c>, <c>OmitXmlDeclaration = true</c>, no indentation) followed by an
	/// invalid-character filter. They are not style preferences: a change here changes the wire.</para>
	/// <para>Each assertion replays the same event sequence on both cores (<c>char</c> and <c>byte</c>) and decodes the UTF-8
	/// output before comparing, so every rule doubles as a char/byte parity check.</para>
	/// <para>All non-ASCII characters are written as <c>\uXXXX</c> escapes on purpose: in a fixture about exact bytes, the
	/// expected values must not depend on how the source file itself is decoded.</para>
	/// </remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-XML")]
	[Parallelizable(ParallelScope.All)]
	public sealed class CrystalXmlWriterFacts : SimpleTest
	{

		#region Helpers...

		private static readonly XmlName Root = XmlName.Create("r");

		private static readonly XmlName Child = XmlName.Create("c");

		private static readonly XmlName Attr = XmlName.Create("a");

		/// <summary>An event sequence, written once and replayed on both cores</summary>
		private interface IXmlScenario
		{
			void Run<TRune, TWriter>(ref CrystalXmlWriter<TRune, TWriter> writer)
				where TRune : unmanaged
				where TWriter : struct, IBufferWriter<TRune>;
		}

		/// <summary>Runs <paramref name="scenario"/> on the char core, and returns the document</summary>
		private static string RenderChars(IXmlScenario scenario, bool strictControlCharacters = false)
		{
			var sink = new ArrayBufferWriter<char>();
			var inner = new XmlEmitterConformance.SinkRef<char>(sink);
			var writer = new CrystalXmlWriter<char, XmlEmitterConformance.SinkRef<char>>(ref inner, strictControlCharacters);
			scenario.Run(ref writer);
			return sink.WrittenSpan.ToString();
		}

		/// <summary>Runs <paramref name="scenario"/> on the byte core, and returns the raw UTF-8 bytes</summary>
		private static byte[] RenderBytes(IXmlScenario scenario, bool strictControlCharacters = false)
		{
			var sink = new ArrayBufferWriter<byte>();
			var inner = new XmlEmitterConformance.SinkRef<byte>(sink);
			var writer = new CrystalXmlWriter<byte, XmlEmitterConformance.SinkRef<byte>>(ref inner, strictControlCharacters);
			scenario.Run(ref writer);
			return sink.WrittenSpan.ToArray();
		}

		/// <summary>Asserts that both cores produce <paramref name="expected"/> for the same event sequence</summary>
		private static void AssertDocument(IXmlScenario scenario, string expected, bool strictControlCharacters = false)
		{
			Assert.That(RenderChars(scenario, strictControlCharacters), Is.EqualTo(expected), "char core");
			Assert.That(Encoding.UTF8.GetString(RenderBytes(scenario, strictControlCharacters)), Is.EqualTo(expected), "byte core, decoded from UTF-8");
		}

		/// <summary>Asserts that <paramref name="text"/> written as element content produces <c>&lt;r&gt;...&lt;/r&gt;</c></summary>
		private static void AssertText(string text, string expectedContent, bool strictControlCharacters = false)
			=> AssertDocument(new TextScenario(text), "<r>" + expectedContent + "</r>", strictControlCharacters);

		/// <summary>Asserts that <paramref name="value"/> written as an attribute produces <c>&lt;r a="..." /&gt;</c></summary>
		private static void AssertAttribute(string value, string expectedValue, bool strictControlCharacters = false)
			=> AssertDocument(new AttributeScenario(value), "<r a=\"" + expectedValue + "\" />", strictControlCharacters);

		private sealed class TextScenario(string text) : IXmlScenario
		{
			public void Run<TRune, TWriter>(ref CrystalXmlWriter<TRune, TWriter> writer)
				where TRune : unmanaged
				where TWriter : struct, IBufferWriter<TRune>
			{
				writer.WriteStartElement(in Root);
				writer.WriteText(text.AsSpan());
				writer.WriteEndElement(in Root);
			}
		}

		private sealed class AttributeScenario(string value) : IXmlScenario
		{
			public void Run<TRune, TWriter>(ref CrystalXmlWriter<TRune, TWriter> writer)
				where TRune : unmanaged
				where TWriter : struct, IBufferWriter<TRune>
			{
				writer.WriteStartElement(in Root);
				writer.WriteAttribute(in Attr, value.AsSpan());
				writer.WriteEndElement(in Root);
			}
		}

		#endregion

		#region Rule 1: no XML declaration, no prefixes, no xmlns, ever...

		[Test]
		public void Test_No_Declaration_No_Prefix_No_Namespace()
		{
			AssertDocument(new DeclarationScenario(), "<r c=\"1\"><c>hello</c></r>");

			string doc = RenderChars(new DeclarationScenario());
			Assert.That(doc, Does.Not.Contain("<?xml"));
			Assert.That(doc, Does.Not.Contain("xmlns"));
			Assert.That(doc, Does.Not.Contain(":"), "no prefix is ever emitted");
			Assert.That(doc[0], Is.EqualTo('<'), "the document starts with the root element");
		}

		private sealed class DeclarationScenario : IXmlScenario
		{
			public void Run<TRune, TWriter>(ref CrystalXmlWriter<TRune, TWriter> writer)
				where TRune : unmanaged
				where TWriter : struct, IBufferWriter<TRune>
			{
				writer.WriteStartElement(in Root);
				writer.WriteAttribute(in Child, "1".AsSpan());
				writer.WriteStartElement(in Child);
				writer.WriteText("hello".AsSpan());
				writer.WriteEndElement(in Child);
				writer.WriteEndElement(in Root);
			}
		}

		#endregion

		#region Rule 2: attributes keep their local name only...

		[Test]
		public void Test_Attributes_Keep_Local_Name_Only()
		{
			// the two attributes the compat wire actually emits: `nil` and `type`, both bare, never `i:nil` or `z:type`
			AssertDocument(new NilAndTypeScenario(), "<r nil=\"true\" type=\"Foo\" />");
		}

		private sealed class NilAndTypeScenario : IXmlScenario
		{
			private static readonly XmlName Nil = XmlName.Create("nil");

			private static readonly XmlName TypeName = XmlName.Create("type");

			public void Run<TRune, TWriter>(ref CrystalXmlWriter<TRune, TWriter> writer)
				where TRune : unmanaged
				where TWriter : struct, IBufferWriter<TRune>
			{
				writer.WriteStartElement(in Root);
				writer.WriteAttribute(in Nil, "true".AsSpan());
				writer.WriteAttribute(in TypeName, "Foo".AsSpan());
				writer.WriteEndElement(in Root);
			}
		}

		#endregion

		#region Rule 3: text content escapes & < > (but not " nor ')...

		[Test]
		public void Test_Text_Escapes_Amp_Lt_Gt()
		{
			AssertText("a&b<c>d", "a&amp;b&lt;c&gt;d");

			// the quote characters are NOT escaped in text content
			AssertText("say \"hi\" & don't", "say \"hi\" &amp; don't");

			// an already-escaped entity is escaped again: the input is text, not markup
			AssertText("&amp;", "&amp;amp;");

			// nothing to escape: pure passthrough
			AssertText("hello world", "hello world");
		}

		#endregion

		#region Rule 4: text newlines (\r\n, \r, \n) all normalize to a RAW \r\n...

		[Test]
		public void Test_Text_Newlines_Normalize_To_Raw_Crlf()
		{
			// each of the three line endings becomes a raw CRLF, never a character reference
			AssertText("a\r\nb", "a\r\nb");
			AssertText("a\rb", "a\r\nb");
			AssertText("a\nb", "a\r\nb");

			// a lone CR at the end of the text still becomes a full CRLF
			AssertText("a\r", "a\r\n");

			// only \r\n pairs up: \n\r is two separate line endings, so two CRLFs
			AssertText("a\n\rb", "a\r\n\r\nb");

			// TAB stays raw in text content (it is legal XML 1.0 whitespace)
			AssertText("a\tb", "a\tb");
		}

		#endregion

		#region Rule 5: attribute values escape & < " but NOT >...

		[Test]
		public void Test_Attribute_Escapes_Amp_Lt_Quote_But_Not_Gt()
		{
			AssertAttribute("a&b<c>d\"e'f", "a&amp;b&lt;c>d&quot;e'f");
			AssertAttribute("plain", "plain");
		}

		#endregion

		#region Rule 6: attribute values entitize TAB, LF and CR...

		[Test]
		public void Test_Attribute_Entitizes_Tab_Lf_Cr()
		{
			AssertAttribute("a\tb\nc\rd", "a&#x9;b&#xA;c&#xD;d");

			// inside an attribute, CRLF is NOT normalized: both code units are entitized, in order
			AssertAttribute("a\r\nb", "a&#xD;&#xA;b");
		}

		#endregion

		#region Rule 7: C0 control characters are DROPPED by default, entitized in strict mode...

		[Test]
		public void Test_Control_Characters_Are_Dropped_By_Default()
		{
			// the acted deviation from the legacy wire: a character reference to a C0 control is not parseable by any
			// conformant XML reader, so by default the value is sanitized at the source instead
			AssertText("a\u0001b\u001Fc\u000Bd", "abcd");
			AssertText("a\u0000b", "ab");
			AssertAttribute("a\u0001b", "ab");
		}

		[Test]
		public void Test_Control_Characters_Are_Entitized_In_Strict_Mode()
		{
			// strictControlCharacters reproduces the legacy defect, byte for byte, for the certification harness
			AssertText("a\u0001b\u001Fc\u000Bd", "a&#x1;b&#x1F;c&#xB;d", strictControlCharacters: true);
			AssertText("a\u0000b", "a&#x0;b", strictControlCharacters: true);
			AssertAttribute("a\u0001b", "a&#x1;b", strictControlCharacters: true);

			// TAB / LF / CR are never "control characters": they keep their own rules in both modes
			AssertText("a\tb\nc", "a\tb\r\nc", strictControlCharacters: true);
			AssertAttribute("a\tb\nc", "a&#x9;b&#xA;c", strictControlCharacters: true);
		}

		#endregion

		#region Rule 8: unpaired surrogate halves are dropped, valid pairs pass through...

		[Test]
		public void Test_Unpaired_Surrogates_Are_Dropped_And_Pairs_Are_Preserved()
		{
			// U+1F600 GRINNING FACE: a well-formed pair survives intact
			AssertText("a😀b", "a😀b");

			// lone high surrogate, lone low surrogate, high at end of input, high followed by a non-low char
			AssertText("a\uD83Db", "ab");
			AssertText("a\uDE00b", "ab");
			AssertText("a\uD83D", "a");
			AssertText("a\uD83Dz", "az");
			AssertText("\uDE00\uD83D", "");

			// same rules inside an attribute value
			AssertAttribute("a😀b", "a😀b");
			AssertAttribute("a\uD83Db", "ab");
		}

		[Test]
		public void Test_Byte_Core_Encodes_Surrogate_Pairs_As_Four_Utf8_Bytes()
		{
			// the byte core transcodes while escaping: it must emit the 4-byte form, not CESU-8 (two 3-byte sequences)
			byte[] bytes = RenderBytes(new TextScenario("😀"));
			Assert.That(bytes, Is.EqualTo(new byte[] { 0x3C, 0x72, 0x3E, 0xF0, 0x9F, 0x98, 0x80, 0x3C, 0x2F, 0x72, 0x3E }));
		}

		#endregion

		#region Rule 9: U+FFFE and U+FFFF are dropped...

		[Test]
		public void Test_Non_Characters_Fffe_And_Ffff_Are_Dropped()
		{
			AssertText("a\uFFFEb\uFFFFc", "abc");
			AssertAttribute("a\uFFFEb\uFFFFc", "abc");

			// U+FFFD REPLACEMENT CHARACTER is perfectly valid XML, and must NOT be dropped
			AssertText("a\uFFFDb", "a\uFFFDb");
		}

		#endregion

		#region Rule 10: an element with no content self-closes as `<Name />`...

		[Test]
		public void Test_Element_Without_Content_Self_Closes_With_A_Space()
		{
			AssertDocument(new EmptyScenario(), "<r />");
			AssertDocument(new NestedEmptyScenario(), "<r><c /></r>");
		}

		[Test]
		public void Test_Empty_String_Content_Expands_Element()
		{
			// <r></r> for an empty string, <r /> for an element with no content at all: the two measured wire forms
			AssertText("", "");
			AssertDocument(new EmptyScenario(), "<r />");
		}

		private sealed class EmptyScenario : IXmlScenario
		{
			public void Run<TRune, TWriter>(ref CrystalXmlWriter<TRune, TWriter> writer)
				where TRune : unmanaged
				where TWriter : struct, IBufferWriter<TRune>
			{
				writer.WriteStartElement(in Root);
				writer.WriteEndElement(in Root);
			}
		}

		private sealed class NestedEmptyScenario : IXmlScenario
		{
			public void Run<TRune, TWriter>(ref CrystalXmlWriter<TRune, TWriter> writer)
				where TRune : unmanaged
				where TWriter : struct, IBufferWriter<TRune>
			{
				writer.WriteStartElement(in Root);
				writer.WriteStartElement(in Child);
				writer.WriteEndElement(in Child);
				writer.WriteEndElement(in Root);
			}
		}

		#endregion

		#region Pre-validated (raw) content...

		[Test]
		public void Test_Raw_Ascii_Is_Written_Verbatim()
		{
			// numbers, dates and base64 are pre-validated forms: they must not go through the escaper
			AssertDocument(new RawScenario("-1.5E+10"), "<r>-1.5E+10</r>");
			AssertDocument(new RawScenario("2026-08-03T12:34:56.7890123Z"), "<r>2026-08-03T12:34:56.7890123Z</r>");
			AssertDocument(new RawScenario("SGVsbG8gV29ybGQ="), "<r>SGVsbG8gV29ybGQ=</r>");

			// raw content still closes a pending start tag, and still counts as content
			AssertDocument(new RawAfterAttributeScenario(), "<r a=\"1\">42</r>");
		}

		private sealed class RawScenario(string ascii) : IXmlScenario
		{
			public void Run<TRune, TWriter>(ref CrystalXmlWriter<TRune, TWriter> writer)
				where TRune : unmanaged
				where TWriter : struct, IBufferWriter<TRune>
			{
				writer.WriteStartElement(in Root);
				writer.WriteRawAscii(ascii.AsSpan());
				writer.WriteEndElement(in Root);
			}
		}

		private sealed class RawAfterAttributeScenario : IXmlScenario
		{
			public void Run<TRune, TWriter>(ref CrystalXmlWriter<TRune, TWriter> writer)
				where TRune : unmanaged
				where TWriter : struct, IBufferWriter<TRune>
			{
				writer.WriteStartElement(in Root);
				writer.WriteAttribute(in Attr, "1".AsSpan());
				writer.WriteRawAscii("42".AsSpan());
				writer.WriteEndElement(in Root);
			}
		}

		#endregion

		#region char/byte parity...

		[Test]
		public void Test_Char_And_Byte_Cores_Produce_Identical_Documents()
		{
			// the same event sequence on both cores: the UTF-8 output, decoded, must equal the char output
			string fromChars = RenderChars(new KitchenSinkScenario());
			string fromBytes = Encoding.UTF8.GetString(RenderBytes(new KitchenSinkScenario()));
			Assert.That(fromBytes, Is.EqualTo(fromChars));

			// ... and the shape is the measured one, not merely self-consistent
			Assert.That(
				fromChars,
				Is.EqualTo("<r a=\"café&#x9;&amp;&quot;>\"><c>1 € &lt;😀&gt;\r\nligne 2</c><c /><c></c><c>3.14</c></r>")
			);
		}

		[Test]
		public void Test_Char_And_Byte_Cores_Agree_On_Every_Bmp_Code_Point()
		{
			// exhaustive parity over the whole BMP (minus the surrogate range, which rule 8 already covers): the byte core
			// transcodes inline while escaping, so a single mis-sized sequence would show up here
			var buffer = new StringBuilder(0x800);
			for (int block = 0; block < 0x10000; block += 0x800)
			{
				buffer.Clear();
				for (int c = block; c < block + 0x800; c++)
				{
					if (c is >= 0xD800 and <= 0xDFFF) continue;
					buffer.Append((char) c);
				}

				var scenario = new TextScenario(buffer.ToString());
				string fromChars = RenderChars(scenario);
				string fromBytes = Encoding.UTF8.GetString(RenderBytes(scenario));
				Assert.That(fromBytes, Is.EqualTo(fromChars), $"text block U+{block:X4}");

				string fromCharsAttr = RenderChars(new AttributeScenario(buffer.ToString()));
				string fromBytesAttr = Encoding.UTF8.GetString(RenderBytes(new AttributeScenario(buffer.ToString())));
				Assert.That(fromBytesAttr, Is.EqualTo(fromCharsAttr), $"attribute block U+{block:X4}");
			}
		}

		private sealed class KitchenSinkScenario : IXmlScenario
		{
			public void Run<TRune, TWriter>(ref CrystalXmlWriter<TRune, TWriter> writer)
				where TRune : unmanaged
				where TWriter : struct, IBufferWriter<TRune>
			{
				writer.WriteStartElement(in Root);
				// 2-byte UTF-8, an entitized TAB, and the attribute escapes (including the unescaped '>')
				writer.WriteAttribute(in Attr, "café\t&\">".AsSpan());

				writer.WriteStartElement(in Child);
				// 3-byte UTF-8, a 4-byte pair, text escapes, a dropped control char, and a normalized newline
				writer.WriteText("1 € <\u0001😀>\nligne 2".AsSpan());
				writer.WriteEndElement(in Child);

				// self-closed, then the expanded empty-string form
				writer.WriteStartElement(in Child);
				writer.WriteEndElement(in Child);
				writer.WriteStartElement(in Child);
				// note: an explicit empty SPAN, not `default`, which would bind to the string overload and mean "null"
				writer.WriteText("".AsSpan());
				writer.WriteEndElement(in Child);

				// pre-validated content
				writer.WriteStartElement(in Child);
				writer.WriteRawAscii("3.14".AsSpan());
				writer.WriteEndElement(in Child);

				writer.WriteEndElement(in Root);
			}
		}

		[Test]
		public void Test_Long_Text_Crosses_Buffer_Chunks()
		{
			// the byte core transcodes in bounded chunks: a payload much larger than one chunk, with multi-byte characters
			// and escapes straddling the boundaries, must still come out identical to the char core
			var buffer = new StringBuilder();
			for (int i = 0; i < 5000; i++)
			{
				buffer.Append("aé€😀<&>\r\n");
			}

			var scenario = new TextScenario(buffer.ToString());
			string fromChars = RenderChars(scenario);
			string fromBytes = Encoding.UTF8.GetString(RenderBytes(scenario));
			Assert.That(fromBytes, Is.EqualTo(fromChars));
			Assert.That(fromChars, Does.StartWith("<r>aé€😀&lt;&amp;&gt;\r\naé"));
			Assert.That(fromChars, Does.EndWith("&lt;&amp;&gt;\r\n</r>"));
		}

		#endregion

		#region Type plumbing...

		[Test]
		public void Test_Constructor_Rejects_Unsupported_Rune()
		{
			var sink = new ArrayBufferWriter<int>();
			var inner = new XmlEmitterConformance.SinkRef<int>(sink);
			Assert.That(
				() => { _ = new CrystalXmlWriter<int, XmlEmitterConformance.SinkRef<int>>(ref inner); },
				Throws.InstanceOf<NotSupportedException>()
			);
		}

		[Test]
		public void Test_Depth_Tracks_The_Open_Elements()
		{
			var sink = new ArrayBufferWriter<char>();
			var inner = new XmlEmitterConformance.SinkRef<char>(sink);
			var writer = new CrystalXmlWriter<char, XmlEmitterConformance.SinkRef<char>>(ref inner);

			Assert.That(writer.Depth, Is.Zero);
			writer.WriteStartElement(in Root);
			Assert.That(writer.Depth, Is.EqualTo(1));
			writer.WriteStartElement(in Child);
			Assert.That(writer.Depth, Is.EqualTo(2));
			writer.WriteEndElement(in Child);
			Assert.That(writer.Depth, Is.EqualTo(1));
			writer.WriteEndElement(in Root);
			Assert.That(writer.Depth, Is.Zero);

			Assert.That(sink.WrittenSpan.ToString(), Is.EqualTo("<r><c /></r>"));
		}

		[Test]
		public void Test_XmlName_Exposes_Both_Representations()
		{
			var name = XmlName.Create("Tags");
			Assert.That(name.Text, Is.EqualTo("Tags"));
			Assert.That(name.Utf8.ToArray(), Is.EqualTo("Tags"u8.ToArray()));

			// the generator emits the frozen literal form instead of transcoding at runtime
			var frozen = new XmlName("Tags", "Tags"u8.ToArray());
			Assert.That(frozen.Text, Is.EqualTo(name.Text));
			Assert.That(frozen.Utf8.ToArray(), Is.EqualTo(name.Utf8.ToArray()));

			// non-ASCII names keep both representations in sync, and are emitted verbatim by both cores
			var accented = XmlName.Create("Clé");
			Assert.That(accented.Text, Is.EqualTo("Clé"));
			Assert.That(accented.Utf8.ToArray(), Is.EqualTo(new byte[] { 0x43, 0x6C, 0xC3, 0xA9 }));
			AssertDocument(new NameScenario(accented), "<Clé />");
		}

		private sealed class NameScenario(XmlName name) : IXmlScenario
		{
			public void Run<TRune, TWriter>(ref CrystalXmlWriter<TRune, TWriter> writer)
				where TRune : unmanaged
				where TWriter : struct, IBufferWriter<TRune>
			{
				writer.WriteStartElement(in name);
				writer.WriteEndElement(in name);
			}
		}

		[Test]
		public void Test_String_Overloads_Handle_Null_And_Empty()
		{
			// null means "no content written": the element self-closes, exactly like a missing WriteText
			{
				var sink = new ArrayBufferWriter<char>();
				var inner = new XmlEmitterConformance.SinkRef<char>(sink);
				var writer = new CrystalXmlWriter<char, XmlEmitterConformance.SinkRef<char>>(ref inner);
				writer.WriteStartElement(in Root);
				writer.WriteText(null);
				writer.WriteEndElement(in Root);
				Assert.That(sink.WrittenSpan.ToString(), Is.EqualTo("<r />"));
			}

			// an empty string still forces the expanded form
			{
				var sink = new ArrayBufferWriter<byte>();
				var inner = new XmlEmitterConformance.SinkRef<byte>(sink);
				var writer = new CrystalXmlWriter<byte, XmlEmitterConformance.SinkRef<byte>>(ref inner);
				writer.WriteStartElement(in Root);
				writer.WriteText(string.Empty);
				writer.WriteEndElement(in Root);
				Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan.ToArray()), Is.EqualTo("<r></r>"));
			}

			// a non-empty string still goes through the escaper
			{
				var sink = new ArrayBufferWriter<char>();
				var inner = new XmlEmitterConformance.SinkRef<char>(sink);
				var writer = new CrystalXmlWriter<char, XmlEmitterConformance.SinkRef<char>>(ref inner);
				writer.WriteStartElement(in Root);
				writer.WriteText("a<b");
				writer.WriteEndElement(in Root);
				Assert.That(sink.WrittenSpan.ToString(), Is.EqualTo("<r>a&lt;b</r>"));
			}
		}

		#endregion

		#region Interface-constrained callers (the shape generated code uses)...

		/// <summary>Writes a document through a <c>TEmitter : struct, IXmlEmitter</c> constraint, exactly like generated code</summary>
		/// <remarks>Only interface members are visible here. That is the whole point: if the null-tolerant
		/// <c>WriteText(string?)</c> were not on the interface, a <c>string?</c> argument would bind the span overload through
		/// the implicit string conversion, and a null would silently emit <c>&lt;r&gt;&lt;/r&gt;</c> instead of <c>&lt;r /&gt;</c>.
		/// The event sequences and the null/""/typical-value pin cases live on <see cref="XmlEmitterConformance"/>, shared with
		/// the infoset emitters' own fixture, so all three families are proven against the identical cases.</remarks>
		[Test]
		public void Test_Interface_Constrained_Caller_Gets_The_Same_Wire_As_The_Struct()
		{
			// null through the interface must self-close, and an empty string must expand, on BOTH cores
			foreach (var (text, expected) in XmlEmitterConformance.TextCases)
			{
				AssertInterfaceText(text, expected);
			}

			foreach (var (ascii, expected) in XmlEmitterConformance.RawCases)
			{
				AssertInterfaceRaw(ascii, expected);
			}
		}

		private static void AssertInterfaceText(string? text, string expected)
		{
			{
				var sink = new ArrayBufferWriter<char>();
				var inner = new XmlEmitterConformance.SinkRef<char>(sink);
				var emitter = new CrystalXmlWriter<char, XmlEmitterConformance.SinkRef<char>>(ref inner);
				XmlEmitterConformance.EmitTextThroughInterface(ref emitter, text);
				Assert.That(sink.WrittenSpan.ToString(), Is.EqualTo(expected), "char core, through the interface");
			}
			{
				var sink = new ArrayBufferWriter<byte>();
				var inner = new XmlEmitterConformance.SinkRef<byte>(sink);
				var emitter = new CrystalXmlWriter<byte, XmlEmitterConformance.SinkRef<byte>>(ref inner);
				XmlEmitterConformance.EmitTextThroughInterface(ref emitter, text);
				Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan.ToArray()), Is.EqualTo(expected), "byte core, through the interface");
			}
		}

		private static void AssertInterfaceRaw(string? ascii, string expected)
		{
			{
				var sink = new ArrayBufferWriter<char>();
				var inner = new XmlEmitterConformance.SinkRef<char>(sink);
				var emitter = new CrystalXmlWriter<char, XmlEmitterConformance.SinkRef<char>>(ref inner);
				XmlEmitterConformance.EmitRawThroughInterface(ref emitter, ascii);
				Assert.That(sink.WrittenSpan.ToString(), Is.EqualTo(expected), "char core, through the interface");
			}
			{
				var sink = new ArrayBufferWriter<byte>();
				var inner = new XmlEmitterConformance.SinkRef<byte>(sink);
				var emitter = new CrystalXmlWriter<byte, XmlEmitterConformance.SinkRef<byte>>(ref inner);
				XmlEmitterConformance.EmitRawThroughInterface(ref emitter, ascii);
				Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan.ToArray()), Is.EqualTo(expected), "byte core, through the interface");
			}
		}

		#endregion

		#region Inline-state sink (proves the documented read-back contract)...

		/// <summary>Buffer writer whose <b>position</b> lives inline in the struct, like the production sinks</summary>
		/// <remarks><see cref="SinkRef{T}"/> hides the hazard because it defers everything to a class. Here the cursor is a
		/// field, so a copy of the struct stops tracking the real position: this is what makes
		/// <c>emitter.Writer</c> the only valid place to read the output from.</remarks>
		private struct InlineSink<T> : IBufferWriter<T>
		{

			private readonly T[] Storage;

			private int Count;

			public InlineSink(int capacity)
			{
				this.Storage = new T[capacity];
				this.Count = 0;
			}

			public readonly ReadOnlySpan<T> WrittenSpan => this.Storage.AsSpan(0, this.Count);

			public void Advance(int count) => this.Count += count;

			public readonly Memory<T> GetMemory(int sizeHint = 0) => this.Storage.AsMemory(this.Count);

			public readonly Span<T> GetSpan(int sizeHint = 0)
			{
				var span = this.Storage.AsSpan(this.Count);
				if (span.Length < sizeHint) throw new InvalidOperationException($"InlineSink capacity exceeded: needed {sizeHint}, {span.Length} left");
				return span;
			}

		}

		[Test]
		public void Test_Inline_State_Sink_Char_Core_Reads_Back_Through_Emitter()
		{
			var sink = new InlineSink<char>(256);
			var emitter = new CrystalXmlWriter<char, InlineSink<char>>(ref sink);
			new KitchenSinkScenario().Run(ref emitter);

			// the ONLY live view of the output
			Assert.That(
				emitter.Writer.WrittenSpan.ToString(),
				Is.EqualTo("<r a=\"café&#x9;&amp;&quot;>\"><c>1 € &lt;😀&gt;\r\nligne 2</c><c /><c></c><c>3.14</c></r>")
			);

			// and the variable handed to the constructor never advanced: it is a dead copy, exactly as documented
			Assert.That(sink.WrittenSpan.Length, Is.Zero, "the constructor consumed the writer variable");
		}

		[Test]
		public void Test_Inline_State_Sink_Byte_Core_Reads_Back_Through_Emitter()
		{
			var sink = new InlineSink<byte>(512);
			var emitter = new CrystalXmlWriter<byte, InlineSink<byte>>(ref sink);
			new KitchenSinkScenario().Run(ref emitter);

			Assert.That(
				Encoding.UTF8.GetString(emitter.Writer.WrittenSpan.ToArray()),
				Is.EqualTo("<r a=\"café&#x9;&amp;&quot;>\"><c>1 € &lt;😀&gt;\r\nligne 2</c><c /><c></c><c>3.14</c></r>")
			);

			Assert.That(sink.WrittenSpan.Length, Is.Zero, "the constructor consumed the writer variable");
		}

		#endregion

		#region Empty-value edges...

		[Test]
		public void Test_Empty_Attribute_Value_Does_Not_Count_As_Content()
		{
			// an attribute, empty or not, is not content: the element still self-closes
			AssertDocument(new AttributeScenario(""), "<r a=\"\" />");
			AssertAttribute("", "");
		}

		[Test]
		public void Test_Empty_Raw_Ascii_Counts_As_Content()
		{
			// consistent with WriteText(""): writing content, even nothing, forces the expanded form
			AssertDocument(new RawScenario(""), "<r></r>");
		}

		#endregion

	}

}
