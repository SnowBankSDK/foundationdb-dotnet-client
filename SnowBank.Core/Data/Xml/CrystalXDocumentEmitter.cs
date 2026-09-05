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

namespace SnowBank.Data.Xml
{
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text;
	using System.Xml.Linq;

	/// <summary>XML infoset emitter that builds an in-memory <see cref="XDocument"/></summary>
	/// <remarks>
	/// <para>One of the two infoset sinks for <see cref="ICrystalXmlEmitter"/> (the other wraps a <see cref="System.Xml.XmlWriter"/>
	/// in <see cref="CrystalXmlWriterEmitter"/>). Unlike <see cref="CrystalXmlWriter{TRune,TWriter}"/>, this emitter makes no
	/// byte-exactness promise: only a node tree that is infoset-equivalent to what a conformant parser would build from the
	/// same logical content.</para>
	/// <para><b>The format core's control-character sanitization does NOT apply here.</b> The characters XML 1.0 cannot
	/// represent (C0 controls, unpaired surrogate halves, U+FFFE/U+FFFF) land in the node tree verbatim, producing a
	/// document no conformant parser would accept back; content that may carry them must be sanitized before it reaches the
	/// emitter.</para>
	/// <para>Elements are built bottom-up: <see cref="WriteStartElement(in CrystalXmlName)"/> pushes an unparented <see cref="XElement"/> onto a
	/// stack, attributes and text accumulate on the element at the top, and <see cref="WriteEndElement"/> pops it and appends
	/// it to its parent (or records it as the document root). The attribute-ordering precondition from
	/// <see cref="ICrystalXmlEmitter"/> is still asserted in DEBUG, for consistency with the other emitters.</para>
	/// <para><b>The null-vs-empty distinction survives into the DOM.</b> Adding an empty string flips
	/// <see cref="XElement.IsEmpty"/> to <see langword="false"/> (matching a parse of <c>&lt;x&gt;&lt;/x&gt;</c>), while an
	/// element that never received content stays <see cref="XElement.IsEmpty"/> (matching <c>&lt;x /&gt;</c>). So
	/// <see cref="WriteText(string?)"/> and <see cref="WriteRawAscii(string?)"/> must add <b>nothing at all</b> for
	/// <see langword="null"/>, not even an empty node.</para>
	/// <para><b>Line-ending normalization is applied by hand.</b> <see cref="XContainer.Add(object)"/> stores a string
	/// verbatim, without the XML 1.0 section 2.11 end-of-line normalization (<c>\r\n</c> or lone <c>\r</c> becomes <c>\n</c>) that
	/// <see cref="XDocument.Parse(string)"/> applies. This emitter replicates it itself, so that
	/// <c>XNode.DeepEquals(emitter.ToDocument(), XDocument.Parse(referenceOutput))</c> holds for any text containing a line
	/// break.</para>
	/// <para><b>Namespace declarations are the DOM's business, not this emitter's.</b> An element and an attribute each carry
	/// their namespace in their <see cref="XName"/>, and <see cref="XNode.ToString()"/> derives the declarations and the
	/// prefixes a document needs from those names when it serializes the tree. So
	/// <see cref="WriteNamespaceDeclaration"/> and <see cref="WriteDefaultNamespaceDeclaration"/> do nothing here: a caller
	/// placing a declaration high in a document is expressing where the TEXT should carry it, and this emitter produces no
	/// text. The one exception is a qualified name inside an attribute value, whose namespace the DOM cannot see; see
	/// <see cref="WriteQNameAttribute(in CrystalXmlName, in CrystalXmlName)"/>.</para>
	/// <para>Not thread-safe, and, like every <see cref="ICrystalXmlEmitter"/>, must be passed by <see langword="ref"/>:
	/// <see cref="Root"/> is a plain field assigned once by the final <see cref="WriteEndElement"/>, so a copy taken before
	/// that point would never see it.</para>
	/// </remarks>
	[PublicAPI]
	public struct CrystalXDocumentEmitter : ICrystalXmlEmitter
	{

		/// <summary>Elements that have been started but not yet closed, root-most first</summary>
		private readonly Stack<XElement> OpenElements;

		/// <summary>The completed root element, set once by <see cref="WriteEndElement"/> when it closes the outermost element</summary>
		private XElement? Root;

		/// <summary>Constructs an emitter that accumulates events into a fresh, empty document</summary>
		public CrystalXDocumentEmitter()
		{
			this.OpenElements = new();
			this.Root = null;
		}

		/// <summary>Returns the document built from the events written so far</summary>
		/// <returns>A new <see cref="XDocument"/> wrapping the completed root element</returns>
		/// <exception cref="ContractException">If any element written via <see cref="WriteStartElement(in CrystalXmlName)"/> has not been
		/// matched by a <see cref="WriteEndElement"/> yet, or if nothing was ever written</exception>
		public readonly XDocument ToDocument()
		{
			Contract.Requires(this.OpenElements.Count == 0, "The document is incomplete: not every element has been closed.");
			Contract.Requires(this.Root is not null, "No element was written.");
			return new(this.Root);
		}

		/// <inheritdoc />
		public readonly void WriteStartElement(in CrystalXmlName name) => WriteStartElement(in name, name.Namespace);

		/// <inheritdoc />
		public readonly void WriteStartElement(in CrystalXmlName name, in CrystalXmlNamespace ns)
		{
			this.OpenElements.Push(new(ToXName(in name, in ns)));
		}

		/// <inheritdoc />
		public readonly void WriteAttribute(in CrystalXmlName name, ReadOnlySpan<char> value) => WriteAttribute(in name, name.Namespace, value);

		/// <inheritdoc />
		public readonly void WriteAttribute(in CrystalXmlName name, in CrystalXmlNamespace ns, ReadOnlySpan<char> value)
		{
			var current = this.OpenElements.Peek();
			Contract.Debug.Requires(current.IsEmpty, "Attributes can only be written while the start tag is still open");
			current.SetAttributeValue(ToXName(in name, in ns), value.ToString());
		}

		/// <inheritdoc />
		public readonly void WriteQNameAttribute(in CrystalXmlName name, in CrystalXmlName value) => WriteQNameAttribute(in name, name.Namespace, in value);

		/// <inheritdoc />
		public readonly void WriteQNameAttribute(in CrystalXmlName name, in CrystalXmlNamespace ns, in CrystalXmlName value)
		{
			var current = this.OpenElements.Peek();
			Contract.Debug.Requires(current.IsEmpty, "Attributes can only be written while the start tag is still open");
			current.SetAttributeValue(ToXName(in name, in ns), ResolveQName(current, this.OpenElements.Count, in value));
		}

		/// <inheritdoc />
		/// <remarks>Does nothing: see the type remarks on why this emitter lets the DOM derive its own declarations.</remarks>
		public readonly void WriteNamespaceDeclaration(in CrystalXmlNamespace ns)
		{ }

		/// <inheritdoc />
		/// <inheritdoc cref="WriteNamespaceDeclaration" path="/remarks"/>
		public readonly void WriteDefaultNamespaceDeclaration(in CrystalXmlNamespace ns)
		{ }

		/// <summary>Combines a local name and a namespace into the <see cref="XName"/> the DOM keys nodes by</summary>
		private static XName ToXName(in CrystalXmlName name, in CrystalXmlNamespace ns)
			=> ns.Text is { Length: > 0 } uri ? XNamespace.Get(uri) + name.Text : XName.Get(name.Text);

		/// <summary>Builds the <c>prefix:Local</c> text of a qualified name, declaring a prefix on <paramref name="element"/> when nothing in scope binds its namespace</summary>
		/// <remarks>The one place this emitter writes a declaration by hand. The DOM derives the declarations of elements and
		/// attributes on its own, from the namespaces their names carry, but a namespace used inside an attribute's TEXT is
		/// invisible to it, so the alias that text refers to has to exist first. The alias follows the conventional prefix when
		/// there is one, and the same <c>d{depth}p{n}</c> shape as the text emitter otherwise, so a document from either one
		/// carries the same qualified names.</remarks>
		private static string ResolveQName(XElement element, int depth, in CrystalXmlName value)
		{
			if (value.Namespace.Text is not { Length: > 0 } uri)
			{ // no namespace: a bare local name, which resolves against whatever default is in scope
				return value.Text;
			}

			var ns = XNamespace.Get(uri);
			string? prefix = element.GetPrefixOfNamespace(ns);
			if (prefix is null)
			{
				prefix = CrystalXmlNamespaces.GetConventionalPrefix(uri);
				if (prefix is null)
				{
					int rank = 1;
					foreach (var attribute in element.Attributes())
					{
						if (attribute.IsNamespaceDeclaration) ++rank;
					}
					prefix = "d" + depth.ToString(CultureInfo.InvariantCulture) + "p" + rank.ToString(CultureInfo.InvariantCulture);
				}
				element.SetAttributeValue(XNamespace.Xmlns + prefix, uri);
			}

			return prefix + ":" + value.Text;
		}

		/// <inheritdoc />
		/// <remarks>Even an empty span counts as content, per <see cref="ICrystalXmlEmitter.WriteText(ReadOnlySpan{char})"/>:
		/// adding an empty string still flips <see cref="XElement.IsEmpty"/> to <see langword="false"/> (measured against the BCL).</remarks>
		public readonly void WriteText(ReadOnlySpan<char> text)
		{
			this.OpenElements.Peek().Add(NormalizeLineEndings(text));
		}

		/// <inheritdoc />
		public readonly void WriteText(string? text)
		{
			if (text is not null)
			{
				WriteText(text.AsSpan());
			}
		}

		/// <inheritdoc />
		public readonly void WriteRawAscii(ReadOnlySpan<char> ascii)
		{
#if NET8_0_OR_GREATER
			Contract.Debug.Requires(System.Text.Ascii.IsValid(ascii), "Raw content must be pre-validated ASCII");
#else
			Contract.Debug.Requires(SnowBank.Buffers.Binary.UnsafeHelpers.IsAsciiString(ascii), "Raw content must be pre-validated ASCII");
#endif
			this.OpenElements.Peek().Add(ascii.ToString());
		}

		/// <inheritdoc />
		public readonly void WriteRawAscii(string? ascii)
		{
			if (ascii is not null)
			{
				WriteRawAscii(ascii.AsSpan());
			}
		}

		/// <inheritdoc />
		public void WriteEndElement(in CrystalXmlName name)
		{
			Contract.Debug.Requires(this.OpenElements.Count > 0, "There is no open element to close");
			var element = this.OpenElements.Pop();
			if (this.OpenElements.Count > 0)
			{
				this.OpenElements.Peek().Add(element);
			}
			else
			{
				this.Root = element;
			}
		}

		/// <summary>Normalizes every line ending to a single <c>\n</c>, matching the XML 1.0 section 2.11 rule a conformant parser applies</summary>
		/// <remarks>See the type remarks: this is what keeps a directly-built document infoset-equivalent to a parse of the
		/// same logical text, since <see cref="XContainer.Add(object)"/> would otherwise store the input verbatim.</remarks>
		private static string NormalizeLineEndings(ReadOnlySpan<char> text)
		{
			if (text.IndexOf('\r') < 0)
			{
				// fast path: nothing to normalize
				return text.ToString();
			}

			var sb = new StringBuilder(text.Length);
			for (int i = 0; i < text.Length; i++)
			{
				char c = text[i];
				if (c != '\r')
				{
					sb.Append(c);
					continue;
				}

				sb.Append('\n');
				if (i + 1 < text.Length && text[i + 1] == '\n')
				{
					// a \r\n pair collapses to a single \n
					++i;
				}
			}
			return sb.ToString();
		}

	}

}
