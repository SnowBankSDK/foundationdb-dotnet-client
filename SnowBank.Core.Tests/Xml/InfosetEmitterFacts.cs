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
	using System.Xml;
	using System.Xml.Linq;
	using NUnit.Framework;
	using SnowBank.Data.Xml;

	/// <summary>Pins infoset equivalence for the two DOM/BCL-backed emitters, <see cref="CrystalXDocumentEmitter"/> and <see cref="CrystalXmlWriterEmitter"/></summary>
	/// <remarks>
	/// <para>Every scenario is written once, against the <c>where TEmitter : struct, ICrystalXmlEmitter</c> constraint, and
	/// replayed against three things: the byte-exact char core (<see cref="CrystalXmlWriter{TRune,TWriter}"/>, used only as
	/// a reference oracle here - it is pinned in its own right by <c>CrystalXmlWriterFacts</c>), <see cref="CrystalXDocumentEmitter"/>,
	/// and <see cref="CrystalXmlWriterEmitter"/>. The reference oracle's format is parsed back into an <see cref="XDocument"/> and
	/// compared against each infoset emitter's own output via <see cref="XNode.DeepEquals(XNode?,XNode?)"/> - never a raw
	/// string comparison, since only infoset equivalence is guaranteed here, not a byte-exact output.</para>
	/// <para><b>Scope.</b> These two emitters do not replicate the output core's deliberate legacy-compat deviations: dropping
	/// C0 control characters, dropping unpaired surrogate halves, dropping U+FFFE/U+FFFF. Those rules exist only to
	/// reproduce a byte-exact legacy format (see the remarks on <c>CrystalXmlWriterFacts</c>), and a well-formed XML document
	/// - or a conformant <see cref="System.Xml.XmlWriter"/>, which validates characters by default - has no use for them.
	/// So no scenario below feeds such content in; those cases stay pinned exclusively against the output core.</para>
	/// </remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-XML")]
	[Parallelizable(ParallelScope.All)]
	public sealed class InfosetEmitterFacts : SimpleTest
	{

		#region Helpers...

		private static readonly CrystalXmlName Root = CrystalXmlName.Create("r");

		private static readonly CrystalXmlName Child = CrystalXmlName.Create("c");

		private static readonly CrystalXmlName Attr = CrystalXmlName.Create("a");

		/// <summary>An event sequence, written once and replayed against any <see cref="ICrystalXmlEmitter"/></summary>
		private interface IEmitterScenario
		{
			void Run<TEmitter>(ref TEmitter emitter) where TEmitter : struct, ICrystalXmlEmitter;
		}

		/// <summary>Renders <paramref name="scenario"/> through the byte-exact char core: the reference this fixture treats as ground truth</summary>
		private static string RenderReferenceWire(IEmitterScenario scenario)
		{
			var sink = new CrystalXmlEmitterConformance.GrowableBuffer<char>();
			var inner = new CrystalXmlEmitterConformance.SinkRef<char>(sink);
			var writer = new CrystalXmlWriter<char, CrystalXmlEmitterConformance.SinkRef<char>>(ref inner);
			scenario.Run(ref writer);
			return sink.WrittenSpan.ToString();
		}

		/// <summary>Renders <paramref name="scenario"/> through <see cref="CrystalXDocumentEmitter"/></summary>
		private static XDocument RenderXDocument(IEmitterScenario scenario)
		{
			var emitter = new CrystalXDocumentEmitter();
			scenario.Run(ref emitter);
			return emitter.ToDocument();
		}

		/// <summary>Renders <paramref name="scenario"/> through <see cref="CrystalXmlWriterEmitter"/>, into a <see cref="StringBuilder"/></summary>
		private static string RenderXmlWriterOutput(IEmitterScenario scenario)
		{
			var sb = new StringBuilder();
			var settings = new XmlWriterSettings { OmitXmlDeclaration = true };
			using (var writer = XmlWriter.Create(sb, settings))
			{
				var emitter = new CrystalXmlWriterEmitter(writer);
				scenario.Run(ref emitter);
			}
			return sb.ToString();
		}

		/// <summary>Asserts that both infoset emitters produce a node tree equivalent to a parse of the reference format</summary>
		/// <remarks>Comparison is always on parsed trees via <see cref="XNode.DeepEquals(XNode?,XNode?)"/>, never on raw
		/// text: infoset equivalence, not byte equivalence, is what this fixture pins.</remarks>
		private static void AssertInfosetEquivalent(IEmitterScenario scenario)
		{
			string referenceWire = RenderReferenceWire(scenario);
			var expected = XDocument.Parse(referenceWire);

			var fromXDocument = RenderXDocument(scenario);
			Assert.That(
				XNode.DeepEquals(fromXDocument, expected),
				Is.True,
				() => $"CrystalXDocumentEmitter infoset mismatch.\nReference format: {referenceWire}\nExpected: {expected}\nActual:   {fromXDocument}"
			);

			string xmlWriterOutput = RenderXmlWriterOutput(scenario);
			var fromXmlWriter = XDocument.Parse(xmlWriterOutput);
			Assert.That(
				XNode.DeepEquals(fromXmlWriter, expected),
				Is.True,
				() => $"CrystalXmlWriterEmitter infoset mismatch.\nReference format: {referenceWire}\nXmlWriter output: {xmlWriterOutput}\nExpected: {expected}\nActual:   {fromXmlWriter}"
			);
		}

		private sealed class TextScenario(string text) : IEmitterScenario
		{
			public void Run<TEmitter>(ref TEmitter emitter) where TEmitter : struct, ICrystalXmlEmitter
			{
				emitter.WriteStartElement(in Root);
				emitter.WriteText(text.AsSpan());
				emitter.WriteEndElement(in Root);
			}
		}

		private sealed class AttributeScenario(string value) : IEmitterScenario
		{
			public void Run<TEmitter>(ref TEmitter emitter) where TEmitter : struct, ICrystalXmlEmitter
			{
				emitter.WriteStartElement(in Root);
				emitter.WriteAttribute(in Attr, value.AsSpan());
				emitter.WriteEndElement(in Root);
			}
		}

		/// <summary>Adapts <see cref="CrystalXmlEmitterConformance.EmitTextThroughInterface{TEmitter}"/> into an <see cref="IEmitterScenario"/></summary>
		private sealed class TextThroughInterfaceScenario(string? text) : IEmitterScenario
		{
			public void Run<TEmitter>(ref TEmitter emitter) where TEmitter : struct, ICrystalXmlEmitter
				=> CrystalXmlEmitterConformance.EmitTextThroughInterface(ref emitter, text);
		}

		/// <summary>Adapts <see cref="CrystalXmlEmitterConformance.EmitRawThroughInterface{TEmitter}"/> into an <see cref="IEmitterScenario"/></summary>
		private sealed class RawThroughInterfaceScenario(string? ascii) : IEmitterScenario
		{
			public void Run<TEmitter>(ref TEmitter emitter) where TEmitter : struct, ICrystalXmlEmitter
				=> CrystalXmlEmitterConformance.EmitRawThroughInterface(ref emitter, ascii);
		}

		#endregion

		#region Same event sequence as the char core...

		[Test]
		public void Test_Kitchen_Sink_Matches_Char_Core_Infoset()
		{
			AssertInfosetEquivalent(new KitchenSinkScenario());
		}

		private sealed class KitchenSinkScenario : IEmitterScenario
		{
			public void Run<TEmitter>(ref TEmitter emitter) where TEmitter : struct, ICrystalXmlEmitter
			{
				emitter.WriteStartElement(in Root);
				// attribute escapes: & < " are escaped, > is not (rule 5, pinned for the output core; here just exercised)
				emitter.WriteAttribute(in Attr, "café&\"<>".AsSpan());

				emitter.WriteStartElement(in Child);
				// 3-byte UTF-8, a 4-byte surrogate pair, and a character that needs escaping
				emitter.WriteText("1 € <😀>".AsSpan());
				emitter.WriteEndElement(in Child);

				// self-closed, then the expanded empty-string form
				emitter.WriteStartElement(in Child);
				emitter.WriteEndElement(in Child);
				emitter.WriteStartElement(in Child);
				// an explicit empty SPAN, not a null string: WriteText(ReadOnlySpan<char>) always counts as content
				emitter.WriteText("".AsSpan());
				emitter.WriteEndElement(in Child);

				// pre-validated content
				emitter.WriteStartElement(in Child);
				emitter.WriteRawAscii("3.14".AsSpan());
				emitter.WriteEndElement(in Child);

				emitter.WriteEndElement(in Root);
			}
		}

		[Test]
		public void Test_Newlines_Normalize_Like_A_Conformant_Parser()
		{
			// each of the three line-ending forms collapses to a single LF once parsed, per XML 1.0 section 2.11 - this is
			// exactly what forces CrystalXDocumentEmitter to normalize by hand instead of storing the input verbatim (see its remarks)
			AssertInfosetEquivalent(new TextScenario("a\r\nb"));
			AssertInfosetEquivalent(new TextScenario("a\rb"));
			AssertInfosetEquivalent(new TextScenario("a\nb"));
			AssertInfosetEquivalent(new TextScenario("a\r"));
			AssertInfosetEquivalent(new TextScenario("a\n\rb"));
		}

		[Test]
		public void Test_Attribute_Content_Matches_Char_Core_Infoset()
		{
			AssertInfosetEquivalent(new AttributeScenario("a\tb\nc\rd"));
			AssertInfosetEquivalent(new AttributeScenario("a&b<c>d\"e'f"));
			AssertInfosetEquivalent(new AttributeScenario(""));
		}

		#endregion

		#region Conformance suite: WriteText(string?)/WriteRawAscii(string?) through the interface, on both infoset emitters...

		[Test]
		public void Test_Conformance_Suite_Text_Cases_Through_The_Interface()
		{
			foreach (var (text, _) in CrystalXmlEmitterConformance.TextCases)
			{
				AssertInfosetEquivalent(new TextThroughInterfaceScenario(text));
			}
		}

		[Test]
		public void Test_Conformance_Suite_Raw_Cases_Through_The_Interface()
		{
			foreach (var (ascii, _) in CrystalXmlEmitterConformance.RawCases)
			{
				AssertInfosetEquivalent(new RawThroughInterfaceScenario(ascii));
			}
		}

		#endregion

		#region Explicit null-vs-empty decision, pinned directly on CrystalXDocumentEmitter...

		[Test]
		public void Test_XDocumentEmitter_WriteText_Null_Adds_No_Content()
		{
			var emitter = new CrystalXDocumentEmitter();
			emitter.WriteStartElement(in Root);
			emitter.WriteText(null);
			emitter.WriteEndElement(in Root);
			var doc = emitter.ToDocument();

			Assert.That(doc.Root!.IsEmpty, Is.True, "no content at all should leave the element self-closable, like <r />");
			Assert.That(doc.Root.Nodes().Count(), Is.Zero);
			Assert.That(XNode.DeepEquals(doc, XDocument.Parse("<r />")), Is.True);
		}

		[Test]
		public void Test_XDocumentEmitter_WriteText_Empty_String_Adds_Empty_Text_Node()
		{
			var emitter = new CrystalXDocumentEmitter();
			emitter.WriteStartElement(in Root);
			emitter.WriteText(string.Empty);
			emitter.WriteEndElement(in Root);
			var doc = emitter.ToDocument();

			Assert.That(doc.Root!.IsEmpty, Is.False, "an empty string still counts as content and forces the expanded <r></r> form");
			Assert.That(XNode.DeepEquals(doc, XDocument.Parse("<r></r>")), Is.True);
		}

		[Test]
		public void Test_XDocumentEmitter_WriteRawAscii_Null_Adds_No_Content()
		{
			var emitter = new CrystalXDocumentEmitter();
			emitter.WriteStartElement(in Root);
			emitter.WriteRawAscii(null);
			emitter.WriteEndElement(in Root);
			var doc = emitter.ToDocument();

			Assert.That(doc.Root!.IsEmpty, Is.True);
			Assert.That(XNode.DeepEquals(doc, XDocument.Parse("<r />")), Is.True);
		}

		[Test]
		public void Test_XDocumentEmitter_WriteRawAscii_Empty_String_Adds_Empty_Content()
		{
			var emitter = new CrystalXDocumentEmitter();
			emitter.WriteStartElement(in Root);
			emitter.WriteRawAscii(string.Empty);
			emitter.WriteEndElement(in Root);
			var doc = emitter.ToDocument();

			Assert.That(doc.Root!.IsEmpty, Is.False);
			Assert.That(XNode.DeepEquals(doc, XDocument.Parse("<r></r>")), Is.True);
		}

		#endregion

		#region Namespaces...

		/// <summary>Adapts a shared conformance scenario into an <see cref="IEmitterScenario"/></summary>
		private sealed class SharedScenario(CrystalXmlEmitterConformance.IScenario inner) : IEmitterScenario
		{
			public void Run<TEmitter>(ref TEmitter emitter) where TEmitter : struct, ICrystalXmlEmitter
				=> inner.Run(ref emitter);
		}

		[Test]
		public void Test_Namespace_Cases_Are_Equivalent_On_Both_Infoset_Emitters()
		{
			// the same event sequences the byte-exact core is pinned against, compared here on expanded names instead of
			// bytes. XNode.DeepEquals cannot serve: it compares namespace declarations as ordinary attributes, and these two
			// emitters place their own declarations (the DOM one derives them at serialization time and holds none in the
			// tree at all), so a document identical in meaning fails it. That is the difference the expanded-name comparer
			// exists to ignore, and it is the same acceptance rule the namespaced format is certified under.
			foreach (var (label, scenario, expected) in CrystalXmlEmitterConformance.NamespaceCases)
			{
				Log($"{label}:");
				Log($"  {expected}");

				XmlExpandedNameComparison.AssertEquivalent(expected, RenderXDocument(new SharedScenario(scenario)).ToString(), $"CrystalXDocumentEmitter, {label}");
				XmlExpandedNameComparison.AssertEquivalent(expected, RenderXmlWriterOutput(new SharedScenario(scenario)), $"CrystalXmlWriterEmitter, {label}");
			}
		}

		[Test]
		public void Test_A_Namespaced_Name_Reaches_The_Dom_As_An_Expanded_Name()
		{
			// the namespace is not decoration: it is half of the name the DOM keys the node by
			var document = RenderXDocument(new SharedScenario(CrystalXmlEmitterConformance.NamespaceCase("becomes the default namespace").Scenario));
			Log(document.ToString());

			var root = document.Root!;
			Assert.That(root.Name.NamespaceName, Is.EqualTo("urn:acme:biblio"));
			Assert.That(root.Name.LocalName, Is.EqualTo("Library"));
			Assert.That(root.Elements().Single().Name.NamespaceName, Is.EqualTo("urn:acme:biblio"), "a child in the same namespace inherits it, prefix or no prefix");
		}

		[Test]
		public void Test_A_Namespaced_Attribute_Keeps_Its_Namespace_And_The_Default_One_Does_Not_Apply()
		{
			// an unprefixed attribute is in NO namespace whatever default is in scope, which is why an attribute needs its own
			// resolution rule and cannot borrow the element's
			var document = RenderXDocument(new SharedScenario(CrystalXmlEmitterConformance.NamespaceCase("conventional prefix").Scenario));
			Log(document.ToString());

			var nil = document.Root!.Attributes().Single(a => !a.IsNamespaceDeclaration);
			Assert.That(nil.Name.NamespaceName, Is.EqualTo("http://www.w3.org/2001/XMLSchema-instance"));
			Assert.That(nil.Name.LocalName, Is.EqualTo("nil"));
			Assert.That(nil.Value, Is.EqualTo("true"));
		}

		[Test]
		public void Test_A_Qualified_Name_Value_Resolves_To_The_Same_Pair_On_Every_Emitter()
		{
			// a qualified name means the PAIR (namespace, local name), so what has to agree across emitters is the pair the
			// value resolves to, never the alias the value happens to be spelled with
			foreach (var (fragment, expectedNamespace) in new[] { ("bare local name", "urn:acme:biblio"), ("from another namespace is prefixed", "urn:acme:recherche") })
			{
				var (label, scenario, _) = CrystalXmlEmitterConformance.NamespaceCase(fragment);
				Log(label);

				AssertQNameResolvesTo(XDocument.Parse(RenderReferenceWire(new SharedScenario(scenario))), expectedNamespace, "the text emitter");
				AssertQNameResolvesTo(RenderXDocument(new SharedScenario(scenario)), expectedNamespace, "CrystalXDocumentEmitter");
				AssertQNameResolvesTo(XDocument.Parse(RenderXmlWriterOutput(new SharedScenario(scenario))), expectedNamespace, "CrystalXmlWriterEmitter");
			}
		}

		/// <summary>Resolves the <c>i:type</c> value of the root through the declarations in scope, and asserts the pair it names</summary>
		private static void AssertQNameResolvesTo(XDocument document, string expectedNamespace, string who)
		{
			Log($"  {who}: {document.Root}");

			var root = document.Root!;
			string value = root.Attribute(XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance"))!.Value;

			int colon = value.IndexOf(':');
			string localName = colon < 0 ? value : value.Substring(colon + 1);
			var ns = colon < 0 ? root.GetDefaultNamespace() : root.GetNamespaceOfPrefix(value.Substring(0, colon))!;

			Assert.That(localName, Is.EqualTo("RangeCriterion"), who);
			Assert.That(ns.NamespaceName, Is.EqualTo(expectedNamespace), who);
		}

		#endregion

		#region Type plumbing...

		[Test]
		public void Test_XDocumentEmitter_ToDocument_Requires_Every_Element_Closed()
		{
			var emitter = new CrystalXDocumentEmitter();
			emitter.WriteStartElement(in Root);
			Assert.That(() => emitter.ToDocument(), Throws.InstanceOf<AssertionException>());
		}

		[Test]
		public void Test_XDocumentEmitter_ToDocument_Requires_Something_Written()
		{
			var emitter = new CrystalXDocumentEmitter();
			Assert.That(() => emitter.ToDocument(), Throws.InstanceOf<AssertionException>());
		}

		[Test]
		public void Test_XDocumentEmitter_Nested_Elements_Round_Trip()
		{
			AssertInfosetEquivalent(new NestedScenario());
		}

		private sealed class NestedScenario : IEmitterScenario
		{
			public void Run<TEmitter>(ref TEmitter emitter) where TEmitter : struct, ICrystalXmlEmitter
			{
				emitter.WriteStartElement(in Root);
				emitter.WriteStartElement(in Child);
				emitter.WriteStartElement(in Child);
				emitter.WriteText("deep".AsSpan());
				emitter.WriteEndElement(in Child);
				emitter.WriteEndElement(in Child);
				emitter.WriteEndElement(in Root);
			}
		}

		[Test]
		public void Test_XmlWriterEmitter_Constructor_Rejects_Null_Writer()
		{
			Assert.That(() => new CrystalXmlWriterEmitter(null!), Throws.InstanceOf<ArgumentNullException>());
		}

		#endregion

		#region Review round 1 fixes...

		[Test]
#if !DEBUG
		[Ignore("Only works in DEBUG mode")]
#endif
		public void Test_XDocumentEmitter_WriteEndElement_Requires_An_Open_Element()
		{
			// balance guard: calling WriteEndElement with nothing open must not silently pop an empty stack
			var emitter = new CrystalXDocumentEmitter();
			Assert.That(() => emitter.WriteEndElement(in Root), Throws.InstanceOf<AssertionException>());
		}

		[Test]
#if !DEBUG
		[Ignore("Only works in DEBUG mode")]
#endif
		public void Test_XmlWriterEmitter_WriteRawAscii_String_Overload_Enforces_Ascii_Precondition()
		{
			// the string? overload must delegate to the span overload, so the same ASCII precondition applies to both -
			// this is what FIX 1 (review round 1) restored: it previously called this.Writer.WriteString(ascii) directly,
			// bypassing the Contract.Debug.Requires(IsAscii(...)) guard that only lived on the span overload
			var sb = new StringBuilder();
			using var writer = XmlWriter.Create(sb, new XmlWriterSettings { OmitXmlDeclaration = true });
			var emitter = new CrystalXmlWriterEmitter(writer);
			Assert.That(() => emitter.WriteRawAscii("café"), Throws.InstanceOf<AssertionException>());
		}

		#endregion

	}

}
