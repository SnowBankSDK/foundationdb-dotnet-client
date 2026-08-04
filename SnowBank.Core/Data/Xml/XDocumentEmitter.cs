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
	using System.Text;
	using System.Xml.Linq;

	/// <summary>XML infoset emitter that builds an in-memory <see cref="XDocument"/></summary>
	/// <remarks>
	/// <para>One of the two infoset sinks for <see cref="IXmlEmitter"/> (the other wraps a <see cref="System.Xml.XmlWriter"/>
	/// in <see cref="XmlWriterEmitter"/>). Unlike <see cref="CrystalXmlWriter{TRune,TWriter}"/>, this emitter makes no
	/// byte-exactness promise: there is no wire, no indentation, and no CRLF convention to reproduce, only a node tree that
	/// is infoset-equivalent to what a conformant XML parser would build from the same logical content.</para>
	/// <para><b>The wire core's control-character sanitization does NOT apply here.</b> <see cref="CrystalXmlWriter{TRune,TWriter}"/> drops the characters
	/// XML 1.0 cannot represent (C0 controls, unpaired surrogate halves, U+FFFE/U+FFFF) as part of reproducing a byte-exact
	/// legacy wire; that is deviation 2 of the compat profile, and it is a property of the TEXT sinks only. This emitter
	/// applies no such filter: whatever the caller writes lands in the node tree verbatim, and a document built from such content is not one a conformant parser would accept back. Content that may carry those characters and must survive
	/// any sink has to be sanitized before it reaches the emitter.</para>
	/// <para>Elements are built bottom-up: <see cref="WriteStartElement"/> pushes a fresh, unparented <see cref="XElement"/>
	/// onto an internal stack, attributes and text accumulate directly on the element at the top of that stack, and
	/// <see cref="WriteEndElement"/> pops it and appends it to its new parent (or, for the outermost element, records it as
	/// the document root). Attributes therefore are not literally "buffered" the way a text writer would have to buffer
	/// them until a start tag can be closed: <see cref="XElement"/> accepts an attribute at any time, so
	/// <see cref="WriteAttribute"/> just calls <see cref="XElement.SetAttributeValue(XName,object?)"/> directly. The
	/// ordering precondition from <see cref="IXmlEmitter"/> is still asserted in DEBUG, both to catch a caller bug and to
	/// keep this emitter's behavior consistent with the other two.</para>
	/// <para><b>The null-vs-empty distinction survives into the DOM.</b> <see cref="XContainer.Add(object)"/> with an empty
	/// string still creates expanded, non-self-closing content (<see cref="XElement.IsEmpty"/> becomes <see langword="false"/>,
	/// matching what <see cref="XDocument.Parse(string)"/> builds from <c>&lt;x&gt;&lt;/x&gt;</c>), while an element that never
	/// received any <see cref="WriteText(ReadOnlySpan{char})"/>/<see cref="WriteRawAscii(ReadOnlySpan{char})"/>/child-element call stays <see cref="XElement.IsEmpty"/>
	/// <see langword="true"/> (matching a parse of <c>&lt;x /&gt;</c>). So <see cref="WriteText(string?)"/> and
	/// <see cref="WriteRawAscii(string?)"/> must add <b>nothing at all</b> for <see langword="null"/> (not even an empty node),
	/// to preserve that distinction. This was measured directly against the BCL, not assumed: see the remarks on
	/// <see cref="WriteText(ReadOnlySpan{char})"/>.</para>
	/// <para><b>Line-ending normalization is applied by hand.</b> <see cref="XContainer.Add(object)"/> stores a string
	/// argument verbatim; it does not apply the end-of-line normalization that XML 1.0 §2.11 requires of a conformant
	/// processor (every <c>\r\n</c> or lone <c>\r</c> becomes a single <c>\n</c>). <see cref="XDocument.Parse(string)"/>
	/// does apply it, because parsing text into a tree is exactly the operation the rule governs. Since this emitter builds
	/// the tree directly rather than through a parser, it replicates that normalization itself, so that
	/// <c>XNode.DeepEquals(emitter.ToDocument(), XDocument.Parse(referenceWire))</c> holds for any text containing a line
	/// break, regardless of which of the three forms was written.</para>
	/// <para>Not thread-safe, and, like every <see cref="IXmlEmitter"/>, must be passed by <see langword="ref"/>: see the
	/// interface remarks. In this implementation every mutable field is a reference type (a <see cref="Stack{T}"/>, an
	/// <see cref="XElement"/>), so a copy would still observe pushes and appends made through the original - except for
	/// <see cref="Root"/> itself, which is a plain field assignment made once, by <see cref="WriteEndElement"/>, on the
	/// outermost element. A copy taken before that point would never see it, which is the one place the ref requirement
	/// actually bites for this type.</para>
	/// </remarks>
	[PublicAPI]
	public struct XDocumentEmitter : IXmlEmitter
	{

		/// <summary>Elements that have been started but not yet closed, root-most first</summary>
		private readonly Stack<XElement> OpenElements;

		/// <summary>The completed root element, set once by <see cref="WriteEndElement"/> when it closes the outermost element</summary>
		private XElement? Root;

		/// <summary>Constructs an emitter that accumulates events into a fresh, empty document</summary>
		public XDocumentEmitter()
		{
			this.OpenElements = new();
			this.Root = null;
		}

		/// <summary>Returns the document built from the events written so far</summary>
		/// <returns>A new <see cref="XDocument"/> wrapping the completed root element</returns>
		/// <exception cref="ContractException">If any element written via <see cref="WriteStartElement"/> has not been
		/// matched by a <see cref="WriteEndElement"/> yet, or if nothing was ever written</exception>
		public readonly XDocument ToDocument()
		{
			Contract.Requires(this.OpenElements.Count == 0, "The document is incomplete: not every element has been closed.");
			Contract.Requires(this.Root is not null, "No element was written.");
			return new(this.Root);
		}

		/// <inheritdoc />
		public readonly void WriteStartElement(in XmlName name)
		{
			this.OpenElements.Push(new(name.Text));
		}

		/// <inheritdoc />
		public readonly void WriteAttribute(in XmlName name, ReadOnlySpan<char> value)
		{
			var current = this.OpenElements.Peek();
			Contract.Debug.Requires(current.IsEmpty, "Attributes can only be written while the start tag is still open");
			current.SetAttributeValue(name.Text, value.ToString());
		}

		/// <inheritdoc />
		/// <remarks>Even an empty span counts as content, exactly as documented on <see cref="IXmlEmitter.WriteText(ReadOnlySpan{char})"/>:
		/// <c>this.OpenElements.Peek().Add(string.Empty)</c> was measured to still flip <see cref="XElement.IsEmpty"/> to
		/// <see langword="false"/>, which is what keeps this call, and the null-tolerant overload that guards it, honest.</remarks>
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
			Contract.Debug.Requires(XmlCharHelpers.IsAscii(ascii), "Raw content must be pre-validated ASCII");
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
		public void WriteEndElement(in XmlName name)
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

		/// <summary>Normalizes every line ending to a single <c>\n</c>, matching the XML 1.0 §2.11 rule a conformant parser applies</summary>
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
