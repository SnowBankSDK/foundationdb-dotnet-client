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

	/// <summary>Event sequences and pin cases shared by every <see cref="ICrystalXmlEmitter"/> conformance fixture</summary>
	/// <remarks>
	/// <para>Drives an emitter exactly the way generated code does: through a <c>where TEmitter : struct, ICrystalXmlEmitter</c>
	/// constraint, where only interface members are visible. This is what proves the null-tolerant
	/// <see cref="ICrystalXmlEmitter.WriteText(string?)"/> / <see cref="ICrystalXmlEmitter.WriteRawAscii(string?)"/> members are reachable
	/// and bind correctly, on every emitter family - not merely on whichever one a hand-written test happened to hold as
	/// its concrete type.</para>
	/// <para><see cref="TextCases"/> and <see cref="RawCases"/> are the same three pins for all three emitter families
	/// (<see cref="CrystalXmlWriter{TRune,TWriter}"/>, <see cref="Data.Xml.CrystalXDocumentEmitter"/>,
	/// <see cref="Data.Xml.CrystalXmlWriterEmitter"/>): <see langword="null"/> must self-close, an empty string must force the
	/// expanded form, and a normal value must go through the escaper/passthrough as usual. Each fixture renders and
	/// compares its own family's output however is appropriate for it (exact string for the byte-exact writer, DOM/re-parse
	/// equivalence for the infoset emitters), but they all replay the identical case data, so the three families cannot
	/// silently drift apart.</para>
	/// </remarks>
	internal static class CrystalXmlEmitterConformance
	{

		/// <summary>Root element name used by every conformance scenario</summary>
		public static readonly CrystalXmlName Root = CrystalXmlName.Create("r");

		/// <summary><see cref="ICrystalXmlEmitter.WriteText(string?)"/> pin cases: <see langword="null"/> self-closes, <c>""</c> expands, ordinary text escapes</summary>
		public static readonly (string? Text, string ExpectedWire)[] TextCases =
		[
			(null, "<r />"),
			("", "<r></r>"),
			("a<b", "<r>a&lt;b</r>"),
		];

		/// <summary><see cref="ICrystalXmlEmitter.WriteRawAscii(string?)"/> pin cases: <see langword="null"/> self-closes, <c>""</c> expands, pre-validated ASCII passes through</summary>
		public static readonly (string? Ascii, string ExpectedWire)[] RawCases =
		[
			(null, "<r />"),
			("", "<r></r>"),
			("42", "<r>42</r>"),
		];

		/// <summary>Writes <c>&lt;r&gt;...&lt;/r&gt;</c> through the interface-constrained <see cref="ICrystalXmlEmitter.WriteText(string?)"/> member</summary>
		public static void EmitTextThroughInterface<TEmitter>(ref TEmitter emitter, string? text)
			where TEmitter : struct, ICrystalXmlEmitter
		{
			emitter.WriteStartElement(in Root);
			emitter.WriteText(text);
			emitter.WriteEndElement(in Root);
		}

		/// <summary>Writes <c>&lt;r&gt;...&lt;/r&gt;</c> through the interface-constrained <see cref="ICrystalXmlEmitter.WriteRawAscii(string?)"/> member</summary>
		public static void EmitRawThroughInterface<TEmitter>(ref TEmitter emitter, string? ascii)
			where TEmitter : struct, ICrystalXmlEmitter
		{
			emitter.WriteStartElement(in Root);
			emitter.WriteRawAscii(ascii);
			emitter.WriteEndElement(in Root);
		}

		#region Namespaces...

		/// <summary>An event sequence, written once and replayed against any emitter family</summary>
		/// <remarks>Only interface members are visible inside <see cref="Run{TEmitter}"/>, which is the whole point: a scenario
		/// that compiles here is a scenario generated code could write.</remarks>
		public interface IScenario
		{
			void Run<TEmitter>(ref TEmitter emitter) where TEmitter : struct, ICrystalXmlEmitter;
		}

		/// <summary>Contract namespace of the root type of the namespace scenarios</summary>
		public static readonly CrystalXmlNamespace Biblio = CrystalXmlNamespace.Create("urn:acme:biblio");

		/// <summary>Contract namespace of the nested type of the namespace scenarios</summary>
		public static readonly CrystalXmlNamespace Annuaire = CrystalXmlNamespace.Create("urn:acme:annuaire");

		/// <summary>Contract namespace of the polymorphic hierarchy of the namespace scenarios</summary>
		public static readonly CrystalXmlNamespace Recherche = CrystalXmlNamespace.Create("urn:acme:recherche");

		/// <summary>Namespace pin cases: an event sequence, and the byte-exact document the text emitter produces for it</summary>
		/// <remarks>
		/// <para>Replayed by <c>CrystalXmlWriterFacts</c> against the exact wire, and by <c>InfosetEmitterFacts</c> against the
		/// parse of that wire, so the three emitter families cannot drift apart on namespaces any more than they can on text.</para>
		/// <para>Each expected wire states a decision of the text emitter: which prefix a namespace gets, where a missing
		/// declaration lands, and what an inherited default does to a name that has no namespace of its own.</para>
		/// </remarks>
		public static readonly (string Label, IScenario Scenario, string ExpectedWire)[] NamespaceCases =
		[
			("the root's own namespace becomes the default namespace", new RootNamespaceScenario(), """<Library xmlns="urn:acme:biblio"><Name>Centrale</Name></Library>"""),
			("a cross-namespace grandchild declares a prefix numbered by ITS depth", new CrossNamespaceScenario(), """<Library xmlns="urn:acme:biblio"><Owner><d3p1:Email xmlns:d3p1="urn:acme:annuaire">x@y.fr</d3p1:Email></Owner></Library>"""),
			("a declaration asked for on the member element moves the prefix up one level", new HoistedCrossNamespaceScenario(), """<Library xmlns="urn:acme:biblio"><Owner xmlns:d2p1="urn:acme:annuaire"><d2p1:Email>x@y.fr</d2p1:Email></Owner></Library>"""),
			("a namespaced attribute takes the conventional prefix of its namespace", new NilAttributeScenario(), """<r xmlns:i="http://www.w3.org/2001/XMLSchema-instance" i:nil="true" />"""),
			("a qualified name in the slot's own namespace is a bare local name", new SameNamespaceQNameScenario(), """<CritDerived xmlns="urn:acme:biblio" xmlns:i="http://www.w3.org/2001/XMLSchema-instance" i:type="RangeCriterion" />"""),
			("a qualified name from another namespace is prefixed, and declares that prefix", new OtherNamespaceQNameScenario(), """<CritDerived xmlns="urn:acme:biblio" xmlns:i="http://www.w3.org/2001/XMLSchema-instance" xmlns:d1p1="urn:acme:recherche" i:type="d1p1:RangeCriterion" />"""),
			("a namespace used by two disjoint subtrees can be declared once at the root", new HoistedInstanceScenario(), """<Library xmlns="urn:acme:biblio" xmlns:i="http://www.w3.org/2001/XMLSchema-instance"><A i:nil="true" /><B i:nil="true" /></Library>"""),
			("two declarations on one element are numbered p1 and p2", new TwoDeclarationsScenario(), """<Library xmlns="urn:acme:biblio"><Owner xmlns:d2p1="urn:acme:annuaire" xmlns:d2p2="urn:acme:recherche" /></Library>"""),
			("a declaration goes out of scope with its element, so a sibling declares its own", new SiblingScopesScenario(), """<Library xmlns="urn:acme:biblio"><A xmlns:d2p1="urn:acme:annuaire" /><B xmlns:d2p1="urn:acme:annuaire" /></Library>"""),
			("a name with no namespace cancels the inherited default", new CancelDefaultScenario(), """<Library xmlns="urn:acme:biblio"><plain xmlns="" /></Library>"""),
			("the whole shape at once: default namespace, nested namespace, nil, a qualified name, and a collection", new LibraryScenario(), """<Library xmlns="urn:acme:biblio" xmlns:i="http://www.w3.org/2001/XMLSchema-instance"><Name>Centrale</Name><Owner xmlns:d2p1="urn:acme:annuaire"><d2p1:Email>x@y.fr</d2p1:Email></Owner><NullChild i:nil="true" /><CritBase /><CritDerived xmlns:d2p1="urn:acme:recherche" i:type="d2p1:RangeCriterion"><d2p1:Min>3</d2p1:Min></CritDerived><Tags xmlns:d2p1="http://schemas.microsoft.com/2003/10/Serialization/Arrays"><d2p1:string>red</d2p1:string><d2p1:string>green</d2p1:string></Tags></Library>"""),
		];

		/// <summary>Returns the namespace case whose label contains <paramref name="fragment"/></summary>
		/// <remarks>Selected by label rather than by index, so that adding a case cannot silently repoint a fixture at a
		/// different scenario.</remarks>
		public static (string Label, IScenario Scenario, string ExpectedWire) NamespaceCase(string fragment)
		{
			foreach (var entry in NamespaceCases)
			{
				if (entry.Label.Contains(fragment, StringComparison.Ordinal)) return entry;
			}
			throw new InvalidOperationException($"No namespace conformance case has a label containing '{fragment}'.");
		}

		private static readonly CrystalXmlName Library = CrystalXmlName.Create("Library");

		private static readonly CrystalXmlName Name = CrystalXmlName.Create("Name");

		private static readonly CrystalXmlName Owner = CrystalXmlName.Create("Owner");

		private static readonly CrystalXmlName Email = CrystalXmlName.Create("Email");

		private static readonly CrystalXmlName NullChild = CrystalXmlName.Create("NullChild");

		private static readonly CrystalXmlName CritBase = CrystalXmlName.Create("CritBase");

		private static readonly CrystalXmlName CritDerived = CrystalXmlName.Create("CritDerived");

		private static readonly CrystalXmlName Min = CrystalXmlName.Create("Min");

		private static readonly CrystalXmlName Tags = CrystalXmlName.Create("Tags");

		private static readonly CrystalXmlName StringItem = CrystalXmlName.Create("string");

		private static readonly CrystalXmlName Nil = CrystalXmlName.Create("nil");

		private static readonly CrystalXmlName TypeName = CrystalXmlName.Create("type");

		private static readonly CrystalXmlName A = CrystalXmlName.Create("A");

		private static readonly CrystalXmlName B = CrystalXmlName.Create("B");

		/// <summary>Contract name of the derived type a qualified-name annotation carries, in the hierarchy's own namespace</summary>
		private static readonly CrystalXmlName RangeCriterionInRecherche = CrystalXmlName.Create("RangeCriterion", "urn:acme:recherche");

		/// <inheritdoc cref="RangeCriterionInRecherche"/>
		private static readonly CrystalXmlName RangeCriterionInBiblio = CrystalXmlName.Create("RangeCriterion", "urn:acme:biblio");

		private sealed class RootNamespaceScenario : IScenario
		{
			public void Run<TEmitter>(ref TEmitter emitter) where TEmitter : struct, ICrystalXmlEmitter
			{
				emitter.WriteStartElement(in Library, in Biblio);
				emitter.WriteStartElement(in Name, in Biblio);
				emitter.WriteText("Centrale");
				emitter.WriteEndElement(in Name);
				emitter.WriteEndElement(in Library);
			}
		}

		private sealed class CrossNamespaceScenario : IScenario
		{
			public void Run<TEmitter>(ref TEmitter emitter) where TEmitter : struct, ICrystalXmlEmitter
			{
				emitter.WriteStartElement(in Library, in Biblio);
				emitter.WriteStartElement(in Owner, in Biblio);
				emitter.WriteStartElement(in Email, in Annuaire);
				emitter.WriteText("x@y.fr");
				emitter.WriteEndElement(in Email);
				emitter.WriteEndElement(in Owner);
				emitter.WriteEndElement(in Library);
			}
		}

		private sealed class HoistedCrossNamespaceScenario : IScenario
		{
			public void Run<TEmitter>(ref TEmitter emitter) where TEmitter : struct, ICrystalXmlEmitter
			{
				emitter.WriteStartElement(in Library, in Biblio);
				emitter.WriteStartElement(in Owner, in Biblio);
				emitter.WriteNamespaceDeclaration(in Annuaire);
				emitter.WriteStartElement(in Email, in Annuaire);
				emitter.WriteText("x@y.fr");
				emitter.WriteEndElement(in Email);
				emitter.WriteEndElement(in Owner);
				emitter.WriteEndElement(in Library);
			}
		}

		private sealed class NilAttributeScenario : IScenario
		{
			public void Run<TEmitter>(ref TEmitter emitter) where TEmitter : struct, ICrystalXmlEmitter
			{
				emitter.WriteStartElement(in Root);
				emitter.WriteAttribute(in Nil, in CrystalXmlNamespaces.XmlSchemaInstance, "true".AsSpan());
				emitter.WriteEndElement(in Root);
			}
		}

		private sealed class SameNamespaceQNameScenario : IScenario
		{
			public void Run<TEmitter>(ref TEmitter emitter) where TEmitter : struct, ICrystalXmlEmitter
			{
				emitter.WriteStartElement(in CritDerived, in Biblio);
				emitter.WriteQNameAttribute(in TypeName, in CrystalXmlNamespaces.XmlSchemaInstance, in RangeCriterionInBiblio);
				emitter.WriteEndElement(in CritDerived);
			}
		}

		private sealed class OtherNamespaceQNameScenario : IScenario
		{
			public void Run<TEmitter>(ref TEmitter emitter) where TEmitter : struct, ICrystalXmlEmitter
			{
				emitter.WriteStartElement(in CritDerived, in Biblio);
				emitter.WriteQNameAttribute(in TypeName, in CrystalXmlNamespaces.XmlSchemaInstance, in RangeCriterionInRecherche);
				emitter.WriteEndElement(in CritDerived);
			}
		}

		/// <summary>Two subtrees that are not ancestors of each other both write <c>i:nil</c>, so one declaration serves both</summary>
		private sealed class HoistedInstanceScenario : IScenario
		{
			public void Run<TEmitter>(ref TEmitter emitter) where TEmitter : struct, ICrystalXmlEmitter
			{
				emitter.WriteStartElement(in Library, in Biblio);
				emitter.WriteNamespaceDeclaration(in CrystalXmlNamespaces.XmlSchemaInstance);

				emitter.WriteStartElement(in A, in Biblio);
				emitter.WriteAttribute(in Nil, in CrystalXmlNamespaces.XmlSchemaInstance, "true".AsSpan());
				emitter.WriteEndElement(in A);

				emitter.WriteStartElement(in B, in Biblio);
				emitter.WriteAttribute(in Nil, in CrystalXmlNamespaces.XmlSchemaInstance, "true".AsSpan());
				emitter.WriteEndElement(in B);

				emitter.WriteEndElement(in Library);
			}
		}

		private sealed class TwoDeclarationsScenario : IScenario
		{
			public void Run<TEmitter>(ref TEmitter emitter) where TEmitter : struct, ICrystalXmlEmitter
			{
				emitter.WriteStartElement(in Library, in Biblio);
				emitter.WriteStartElement(in Owner, in Biblio);
				emitter.WriteNamespaceDeclaration(in Annuaire);
				emitter.WriteNamespaceDeclaration(in Recherche);
				emitter.WriteEndElement(in Owner);
				emitter.WriteEndElement(in Library);
			}
		}

		private sealed class SiblingScopesScenario : IScenario
		{
			public void Run<TEmitter>(ref TEmitter emitter) where TEmitter : struct, ICrystalXmlEmitter
			{
				emitter.WriteStartElement(in Library, in Biblio);
				emitter.WriteStartElement(in A, in Biblio);
				emitter.WriteNamespaceDeclaration(in Annuaire);
				emitter.WriteEndElement(in A);
				emitter.WriteStartElement(in B, in Biblio);
				emitter.WriteNamespaceDeclaration(in Annuaire);
				emitter.WriteEndElement(in B);
				emitter.WriteEndElement(in Library);
			}
		}

		private sealed class CancelDefaultScenario : IScenario
		{
			private static readonly CrystalXmlName Plain = CrystalXmlName.Create("plain");

			public void Run<TEmitter>(ref TEmitter emitter) where TEmitter : struct, ICrystalXmlEmitter
			{
				emitter.WriteStartElement(in Library, in Biblio);
				emitter.WriteStartElement(in Plain);
				emitter.WriteEndElement(in Plain);
				emitter.WriteEndElement(in Library);
			}
		}

		/// <summary>The shape of the design's worked example: one document that exercises every namespace rule at once</summary>
		private sealed class LibraryScenario : IScenario
		{
			public void Run<TEmitter>(ref TEmitter emitter) where TEmitter : struct, ICrystalXmlEmitter
			{
				emitter.WriteStartElement(in Library, in Biblio);
				// the instance namespace is used by two subtrees that are not ancestors of each other, so it is declared once
				// at the root instead of twice below
				emitter.WriteNamespaceDeclaration(in CrystalXmlNamespaces.XmlSchemaInstance);

				emitter.WriteStartElement(in Name, in Biblio);
				emitter.WriteText("Centrale");
				emitter.WriteEndElement(in Name);

				emitter.WriteStartElement(in Owner, in Biblio);
				emitter.WriteNamespaceDeclaration(in Annuaire);
				emitter.WriteStartElement(in Email, in Annuaire);
				emitter.WriteText("x@y.fr");
				emitter.WriteEndElement(in Email);
				emitter.WriteEndElement(in Owner);

				emitter.WriteStartElement(in NullChild, in Biblio);
				emitter.WriteAttribute(in Nil, in CrystalXmlNamespaces.XmlSchemaInstance, "true".AsSpan());
				emitter.WriteEndElement(in NullChild);

				emitter.WriteStartElement(in CritBase, in Biblio);
				emitter.WriteEndElement(in CritBase);

				emitter.WriteStartElement(in CritDerived, in Biblio);
				emitter.WriteNamespaceDeclaration(in Recherche);
				emitter.WriteQNameAttribute(in TypeName, in CrystalXmlNamespaces.XmlSchemaInstance, in RangeCriterionInRecherche);
				emitter.WriteStartElement(in Min, in Recherche);
				emitter.WriteRawAscii("3");
				emitter.WriteEndElement(in Min);
				emitter.WriteEndElement(in CritDerived);

				emitter.WriteStartElement(in Tags, in Biblio);
				emitter.WriteNamespaceDeclaration(in CrystalXmlNamespaces.Arrays);
				emitter.WriteStartElement(in StringItem, in CrystalXmlNamespaces.Arrays);
				emitter.WriteText("red");
				emitter.WriteEndElement(in StringItem);
				emitter.WriteStartElement(in StringItem, in CrystalXmlNamespaces.Arrays);
				emitter.WriteText("green");
				emitter.WriteEndElement(in StringItem);
				emitter.WriteEndElement(in Tags);

				emitter.WriteEndElement(in Library);
			}
		}

		#endregion

		/// <summary>Struct adapter that lets a <see cref="GrowableBuffer{T}"/> (a class) satisfy a <c>TWriter : struct</c> constraint</summary>
		/// <remarks>Holds a reference to the buffer, so every copy of the struct appends to the same underlying array. The
		/// production sinks (<c>ValueStringWriter</c>, <c>SliceWriter</c>) keep their state inline instead, which is exactly
		/// why an emitter must always be passed by ref; this adapter is test-only glue, not a shape to imitate.</remarks>
		internal readonly struct SinkRef<T> : IBufferWriter<T>
		{

			private readonly GrowableBuffer<T> Buffer;

			public SinkRef(GrowableBuffer<T> buffer) => this.Buffer = buffer;

			public void Advance(int count) => this.Buffer.Advance(count);

			public Memory<T> GetMemory(int sizeHint = 0) => this.Buffer.GetMemory(sizeHint);

			public Span<T> GetSpan(int sizeHint = 0) => this.Buffer.GetSpan(sizeHint);

		}

		/// <summary>Minimal growable buffer standing in for <see cref="ArrayBufferWriter{T}"/>, which is unavailable on <c>net472</c></summary>
		/// <remarks>
		/// <para><see cref="ArrayBufferWriter{T}"/> itself compiles against <c>netstandard2.0</c>, but the standalone
		/// <c>System.Memory</c> package that provides it there ships the type as <see langword="internal"/> rather than
		/// <see langword="public"/> (it only became public as part of the .NET Core 3.0+ shared framework) - so a project
		/// that targets <c>net472</c> against that package sees <c>error CS0122: 'ArrayBufferWriter&lt;T&gt;' is inaccessible
		/// due to its protection level</c>. This repo's own <c>netstandard2.0</c> "lite" build is validated by running its
		/// consuming tests on the real <c>net472</c> CLR (see this repo's CLAUDE.md), so the byte-exact output rules pinned in
		/// this namespace need a sink that compiles and runs there too, instead of skipping netfx coverage entirely.</para>
		/// <para>Same shape as <see cref="IBufferWriter{T}"/> expects: <see cref="GetSpan"/>/<see cref="GetMemory"/> grow the
		/// backing array (doubling) when the requested size does not fit, and <see cref="Advance"/> only moves the cursor -
		/// exactly what <see cref="ArrayBufferWriter{T}"/> itself does, minus the accessibility problem.</para>
		/// </remarks>
		internal sealed class GrowableBuffer<T>
		{

			private T[] Storage;

			private int Count;

			public GrowableBuffer(int initialCapacity = 256)
			{
				this.Storage = new T[Math.Max(initialCapacity, 16)];
				this.Count = 0;
			}

			/// <summary>The portion of the buffer that has been written to so far</summary>
			public ReadOnlySpan<T> WrittenSpan => this.Storage.AsSpan(0, this.Count);

			public void Advance(int count) => this.Count += count;

			public Memory<T> GetMemory(int sizeHint = 0)
			{
				EnsureCapacity(sizeHint);
				return this.Storage.AsMemory(this.Count);
			}

			public Span<T> GetSpan(int sizeHint = 0)
			{
				EnsureCapacity(sizeHint);
				return this.Storage.AsSpan(this.Count);
			}

			private void EnsureCapacity(int sizeHint)
			{
				int needed = Math.Max(sizeHint, 1);
				if (this.Storage.Length - this.Count >= needed)
				{
					return;
				}

				int newSize = this.Storage.Length * 2;
				while (newSize - this.Count < needed)
				{
					newSize *= 2;
				}

				var newStorage = new T[newSize];
				Array.Copy(this.Storage, newStorage, this.Count);
				this.Storage = newStorage;
			}

		}

	}

}
