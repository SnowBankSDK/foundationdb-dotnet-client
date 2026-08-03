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
	using System.Xml;
	using System.Xml.Linq;
	using NUnit.Framework;
	using SnowBank.Data.Json;
	using SnowBank.Data.Xml;

	/// <summary>Pins the five <see cref="CrystalXml"/> output entry points, driven by a hand-written <see cref="ICrystalXmlSerializer{T}"/></summary>
	/// <remarks>
	/// <para>None of these tests care about the byte-exact escaping rules pinned in <c>CrystalXmlWriterFacts</c>, or the
	/// infoset-equivalence rules pinned in <c>InfosetEmitterFacts</c>: those are already covered elsewhere. What is new
	/// here, and not covered by either fixture, is that all five outputs are reachable from the same
	/// <see cref="ICrystalXmlSerializer{T}"/> call and agree with one another, and that <c>settings</c>/<c>rootName</c>
	/// actually reach the serializer rather than being silently dropped by the plumbing.</para>
	/// </remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-XML")]
	[Parallelizable(ParallelScope.All)]
	public sealed class OutputHelperFacts : SimpleTest
	{

		#region Test fixture: a hand-written ICrystalXmlSerializer<Book>...

		private sealed class Book
		{
			public required string Title { get; init; }

			public char Rating { get; init; }
		}

		private static readonly XmlName DefaultRoot = XmlName.Create("Book");

		private static readonly XmlName TitleName = XmlName.Create("Title");

		private static readonly XmlName RatingName = XmlName.Create("Rating");

		/// <summary>Hand-written stub: one root element, an optional child, using both <c>rootName</c> and <c>settings</c></summary>
		private sealed class BookXmlSerializer : ICrystalXmlSerializer<Book>
		{

			public void WriteXml<TEmitter>(ref TEmitter emitter, Book? value, CrystalJsonSettings? settings = null, string? rootName = null)
				where TEmitter : struct, IXmlEmitter
			{
				var root = rootName is not null ? XmlName.Create(rootName) : DefaultRoot;
				bool dcs = settings is not null && !settings.EnumsAsString;
				emitter.WriteStartElement(in root);
				if (value is not null)
				{
					emitter.WriteStartElement(in TitleName);
					emitter.WriteText(value.Title);
					emitter.WriteEndElement(in TitleName);

					emitter.WriteStartElement(in RatingName);
					emitter.WriteRawAscii(dcs ? CrystalXmlFormatters.FormatDcsChar(value.Rating) : CrystalXmlFormatters.FormatModernChar(value.Rating));
					emitter.WriteEndElement(in RatingName);
				}
				emitter.WriteEndElement(in root);
			}

		}

		private static readonly BookXmlSerializer Serializer = new();

		private static Book MakeBook() => new() { Title = "A & B <Tale>", Rating = 'A' };

		/// <summary>Writes a partial element, then throws: proves the sink-lifecycle cleanup in <see cref="CrystalXml"/> does not swallow or wrap the original exception</summary>
		private sealed class ThrowingSerializer : ICrystalXmlSerializer<Book>
		{

			public void WriteXml<TEmitter>(ref TEmitter emitter, Book? value, CrystalJsonSettings? settings = null, string? rootName = null)
				where TEmitter : struct, IXmlEmitter
			{
				emitter.WriteStartElement(in DefaultRoot);
				emitter.WriteText("partial");
				throw new InvalidOperationException("boom");
			}

		}

		/// <summary>Writes more than one buffer's worth of text (forcing at least one real growth-triggered drain, since the sink proxies default to a 4 KB rent), then throws</summary>
		/// <remarks>Exercises the exact failure mode the review called out: an exception AFTER the pooled buffer has
		/// already been grown/drained at least once, not just before the first rent.</remarks>
		private sealed class ThrowingAfterALargeChunkSerializer : ICrystalXmlSerializer<Book>
		{

			public void WriteXml<TEmitter>(ref TEmitter emitter, Book? value, CrystalJsonSettings? settings = null, string? rootName = null)
				where TEmitter : struct, IXmlEmitter
			{
				emitter.WriteStartElement(in DefaultRoot);
				emitter.WriteText(new string('x', 5000));
				throw new InvalidOperationException("boom-after-drain");
			}

		}

		#endregion

		#region The five outputs agree...

		[Test]
		public void Test_All_Five_Outputs_Produce_The_Same_Document()
		{
			var book = MakeBook();

			string text = CrystalXml.ToText(Serializer, book);
			Assert.That(text, Is.EqualTo("<Book><Title>A &amp; B &lt;Tale&gt;</Title><Rating>A</Rating></Book>"));

			Slice slice = CrystalXml.ToSlice(Serializer, book);
			Assert.That(Encoding.UTF8.GetString(slice.ToArray()), Is.EqualTo(text), "ToSlice, UTF-8 decoded");

			byte[] bytes = CrystalXml.ToBytes(Serializer, book);
			Assert.That(Encoding.UTF8.GetString(bytes), Is.EqualTo(text), "ToBytes, UTF-8 decoded");

			using (var ms = new MemoryStream())
			{
				CrystalXml.WriteTo(ms, Serializer, book);
				Assert.That(Encoding.UTF8.GetString(ms.ToArray()), Is.EqualTo(text), "WriteTo(Stream), UTF-8 decoded");
				Assert.That(ms.CanWrite, Is.True, "WriteTo(Stream) must not close or otherwise take ownership of the caller's stream");
			}

			var sw = new StringWriter();
			CrystalXml.WriteTo(sw, Serializer, book);
			Assert.That(sw.ToString(), Is.EqualTo(text), "WriteTo(TextWriter)");

			var bufferWriter = new XmlEmitterConformance.GrowableBuffer<byte>();
			CrystalXml.WriteTo(new XmlEmitterConformance.SinkRef<byte>(bufferWriter), Serializer, book);
			Assert.That(Encoding.UTF8.GetString(bufferWriter.WrittenSpan), Is.EqualTo(text), "WriteTo(IBufferWriter<byte>), UTF-8 decoded");

			XDocument fromText = XDocument.Parse(text);

			XDocument doc = CrystalXml.ToXDocument(Serializer, book);
			Assert.That(XNode.DeepEquals(doc, fromText), Is.True, "ToXDocument deep-equals the parsed text");

			var sb = new StringBuilder();
			var xmlWriterSettings = new XmlWriterSettings { OmitXmlDeclaration = true };
			using (var xmlWriter = XmlWriter.Create(sb, xmlWriterSettings))
			{
				CrystalXml.WriteTo(xmlWriter, Serializer, book);
			}
			Assert.That(XNode.DeepEquals(XDocument.Parse(sb.ToString()), fromText), Is.True, "WriteTo(XmlWriter) deep-equals the parsed text");
		}

		[Test]
		public void Test_Null_Value_Self_Closes()
		{
			string text = CrystalXml.ToText<Book>(Serializer, null);
			Assert.That(text, Is.EqualTo("<Book />"));

			Slice slice = CrystalXml.ToSlice<Book>(Serializer, null);
			Assert.That(Encoding.UTF8.GetString(slice.ToArray()), Is.EqualTo(text));

			XDocument doc = CrystalXml.ToXDocument<Book>(Serializer, null);
			Assert.That(XNode.DeepEquals(doc, XDocument.Parse(text)), Is.True);
		}

		#endregion

		#region rootName plumbing...

		[Test]
		public void Test_RootName_Override_Applies_To_Text_Output()
		{
			string text = CrystalXml.ToText(Serializer, MakeBook(), rootName: "Livre");
			Assert.That(text, Does.StartWith("<Livre>"));
			Assert.That(text, Does.EndWith("</Livre>"));
		}

		[Test]
		public void Test_RootName_Override_Applies_To_XDocument_Output()
		{
			XDocument doc = CrystalXml.ToXDocument(Serializer, MakeBook(), rootName: "Livre");
			Assert.That(doc.Root!.Name.LocalName, Is.EqualTo("Livre"));
		}

		[Test]
		public void Test_RootName_Defaults_To_The_Type_Own_Name_When_Not_Overridden()
		{
			string text = CrystalXml.ToText(Serializer, MakeBook());
			Assert.That(text, Does.StartWith("<Book>"));
		}

		#endregion

		#region settings plumbing...

		[Test]
		public void Test_Settings_Reach_The_Serializer_And_Select_The_Lexical_Profile()
		{
			var book = MakeBook();

			// no settings: the stub picks the "Modern" profile -> the character itself
			string modern = CrystalXml.ToText(Serializer, book);
			Assert.That(modern, Does.Contain("<Rating>A</Rating>"));

			// DataContractCompat sets EnumsAsNumbers (EnumsAsString == false): the stub picks the "Dcs" profile -> the UTF-16 code unit
			string dcs = CrystalXml.ToText(Serializer, book, settings: CrystalJsonSettings.DataContractCompat);
			Assert.That(dcs, Does.Contain("<Rating>65</Rating>"));

			Assert.That(dcs, Is.Not.EqualTo(modern), "settings must actually reach the serializer, not be silently dropped by the plumbing");
		}

		#endregion

		#region large documents cross the initial 4 KB buffer (regression: TextWriterBufferProxy grow/drain)...

		[Test]
		public void Test_Large_Document_Crosses_The_Initial_Buffer_On_WriteTo_TextWriter()
		{
			// an 8000-char title, plus markup, is comfortably more than one 4 KB rent's worth of char data: this
			// forces at least one growth-triggered Drain() mid-document, which is exactly where the reviewed bug
			// (NullReferenceException, then a double-return once naively null-guarded) used to live
			var book = new Book { Title = new string('y', 8000), Rating = 'A' };
			string expectedText = CrystalXml.ToText(Serializer, book);

			var sw = new StringWriter();
			CrystalXml.WriteTo(sw, Serializer, book);
			Assert.That(sw.ToString(), Is.EqualTo(expectedText));
		}

		[Test]
		public void Test_Large_Document_Crosses_The_Initial_Buffer_On_WriteTo_Stream()
		{
			var book = new Book { Title = new string('y', 8000), Rating = 'A' };
			string expectedText = CrystalXml.ToText(Serializer, book);

			using (var ms = new MemoryStream())
			{
				CrystalXml.WriteTo(ms, Serializer, book);
				Assert.That(Encoding.UTF8.GetString(ms.ToArray()), Is.EqualTo(expectedText));
			}
		}

		#endregion

		#region exception safety...

		[Test]
		public void Test_Exceptions_During_Serialization_Propagate_Unchanged()
		{
			var thrower = new ThrowingSerializer();

			Assert.That(() => CrystalXml.ToText<Book>(thrower, null), Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("boom"));

			var sw = new StringWriter();
			Assert.That(() => CrystalXml.WriteTo<Book>(sw, thrower, null), Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("boom"));

			using (var ms = new MemoryStream())
			{
				Assert.That(() => CrystalXml.WriteTo<Book>(ms, thrower, null), Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("boom"));
			}
		}

		[Test]
		public void Test_Exception_After_A_Real_Drain_Still_Propagates_Unchanged_And_Returns_The_Pool()
		{
			// the 5000-char chunk forces at least one growth-triggered Drain() BEFORE the throw, so this exercises
			// the failure path from a state where the pool has already been touched once - not just the "never
			// grew" case Test_Exceptions_During_Serialization_Propagate_Unchanged covers
			var thrower = new ThrowingAfterALargeChunkSerializer();

			var sw = new StringWriter();
			Assert.That(() => CrystalXml.WriteTo<Book>(sw, thrower, null), Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("boom-after-drain"));

			using (var ms = new MemoryStream())
			{
				Assert.That(() => CrystalXml.WriteTo<Book>(ms, thrower, null), Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("boom-after-drain"));
			}

			// pool sanity: if either failure path above had double-returned or corrupted a rented array, the shared
			// ArrayPool could hand out an aliased or too-small buffer next time around, which a large, unrelated,
			// entirely successful call below would be very likely to trip over (it rents/grows from the same shared
			// pools the two failing calls just abandoned)
			var book = new Book { Title = new string('z', 9000), Rating = 'A' };
			string text = CrystalXml.ToText(Serializer, book);
			var sw2 = new StringWriter();
			CrystalXml.WriteTo(sw2, Serializer, book);
			Assert.That(sw2.ToString(), Is.EqualTo(text));
		}

		#endregion

		#region ToText / ToSlice / ToBytes reject a null serializer...

		[Test]
		public void Test_ToText_Rejects_Null_Serializer()
			=> Assert.That(() => CrystalXml.ToText<Book>(null!, null), Throws.InstanceOf<ArgumentNullException>());

		[Test]
		public void Test_ToSlice_Rejects_Null_Serializer()
			=> Assert.That(() => CrystalXml.ToSlice<Book>(null!, null), Throws.InstanceOf<ArgumentNullException>());

		[Test]
		public void Test_WriteTo_Stream_Rejects_Null_Destination()
			=> Assert.That(() => CrystalXml.WriteTo<Book>((Stream) null!, Serializer, null), Throws.InstanceOf<ArgumentNullException>());

		#endregion

		#region XmlName validation...

		[Test]
		public void Test_XmlName_Create_Accepts_Valid_Name()
		{
			var name = XmlName.Create("Book");
			Assert.That(name.Text, Is.EqualTo("Book"));
			Assert.That(name.Utf8.ToArray(), Is.EqualTo(Encoding.UTF8.GetBytes("Book")));
		}

		[Test]
		public void Test_XmlName_Create_Rejects_Name_With_Space()
			=> Assert.That(() => XmlName.Create("in valid"), Throws.InstanceOf<CrystalXmlInvalidNameException>());

		[Test]
		public void Test_XmlName_Create_Rejects_Leading_Digit()
			=> Assert.That(() => XmlName.Create("1abc"), Throws.InstanceOf<CrystalXmlInvalidNameException>());

		[Test]
		public void Test_XmlName_Create_Rejects_Colon()
			=> Assert.That(() => XmlName.Create("a:b"), Throws.InstanceOf<CrystalXmlInvalidNameException>());

		[Test]
		public void Test_XmlName_Create_Rejects_Empty()
			=> Assert.That(() => XmlName.Create(""), Throws.InstanceOf<CrystalXmlInvalidNameException>());

		[Test]
		public void Test_XmlName_Create_Rejects_Null()
			=> Assert.That(() => XmlName.Create(null!), Throws.InstanceOf<ArgumentNullException>());

		#endregion

	}

}
