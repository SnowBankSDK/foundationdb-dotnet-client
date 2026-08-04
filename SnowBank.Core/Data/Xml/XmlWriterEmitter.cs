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
	using System.Xml;

	/// <summary>XML infoset emitter that forwards events straight into a <see cref="System.Xml.XmlWriter"/></summary>
	/// <remarks>
	/// <para>The other infoset sink for <see cref="IXmlEmitter"/> (the DOM-building one is <see cref="XDocumentEmitter"/>).
	/// This one does not implement any escaping, buffering or self-closing logic of its own: every event is a direct,
	/// one-line passthrough to the wrapped <see cref="System.Xml.XmlWriter"/>, which already implements the XML
	/// infoset correctly (attribute ordering, escaping, self-closing). Only infoset equivalence is guaranteed, not a
	/// byte-exact wire: the concrete bytes depend entirely on the <see cref="XmlWriterSettings"/> the caller configured
	/// (indentation, encoding, newline handling, ...), none of which this type inspects or controls.</para>
	/// <para><b>Attributes are never literally buffered here</b>, unlike the byte-exact <see cref="CrystalXmlWriter{TRune,TWriter}"/>:
	/// <see cref="System.Xml.XmlWriter"/> itself only accepts <see cref="XmlWriter.WriteAttributeString(string?,string?)"/>
	/// calls between a <see cref="XmlWriter.WriteStartElement(string?)"/> and the first content-producing call, and throws
	/// if that ordering is violated. The precondition from <see cref="IXmlEmitter"/> is therefore already enforced by the
	/// wrapped writer; this type adds no redundant check of its own.</para>
	/// <para><b><see cref="WriteRawAscii(ReadOnlySpan{char})"/> routes through <see cref="XmlWriter.WriteString(string?)"/>,
	/// not <see cref="XmlWriter.WriteRaw(string?)"/>.</b> This was measured, not assumed: <c>WriteRaw(string.Empty)</c> writes
	/// zero characters and lets the element self-close, silently dropping the "even empty content counts" rule that
	/// <see cref="IXmlEmitter.WriteRawAscii(ReadOnlySpan{char})"/> documents, whereas <c>WriteString(string.Empty)</c> forces
	/// the expanded form, matching <see cref="WriteText(ReadOnlySpan{char})"/>. Pre-validated ASCII content never contains a
	/// character the escaper treats specially, so routing it through the same escaping path as ordinary text produces
	/// identical output to <see cref="XmlWriter.WriteRaw(string?)"/> for every non-empty value, at no cost beyond what the
	/// infoset-only guarantee already allows.</para>
	/// <para>Also measured directly against the BCL: the wrapped writer already applies XML 1.0 §2.11 end-of-line
	/// normalization on write (every line ending becomes a raw <c>\r\n</c> under the default <see cref="NewLineHandling"/>,
	/// matching <see cref="CrystalXmlWriter{TRune,TWriter}"/>'s own rule 4) and already entitizes TAB/LF/CR inside attribute
	/// values (matching rule 6), so neither needs any help from this type.</para>
	/// <para><b>The wire core's control-character sanitization does NOT apply here.</b> <see cref="CrystalXmlWriter{TRune,TWriter}"/> drops the characters
	/// XML 1.0 cannot represent (C0 controls, unpaired surrogate halves, U+FFFE/U+FFFF) as part of reproducing a byte-exact
	/// legacy wire; that is deviation 2 of the compat profile, and it is a property of the TEXT sinks only. This emitter
	/// applies no such filter: the wrapped writer sees the characters unchanged and answers for them under its own <see cref="XmlWriterSettings.CheckCharacters"/>, which throws by default. Content that may carry those characters and must survive
	/// any sink has to be sanitized before it reaches the emitter.</para>
	/// <para>This type owns none of the writer's lifetime: the caller is responsible for flushing and disposing it once
	/// done. Like every <see cref="IXmlEmitter"/>, it must still be passed by <see langword="ref"/> per the interface
	/// remarks, even though every field here is a reference to the same wrapped writer, so a copy would not actually lose
	/// anything for this particular implementation.</para>
	/// </remarks>
	[PublicAPI]
	public readonly struct XmlWriterEmitter : IXmlEmitter
	{

		/// <summary>Destination writer that every event is forwarded to, unowned by this emitter</summary>
		public XmlWriter Writer { get; }

		/// <summary>Wraps an existing <see cref="System.Xml.XmlWriter"/></summary>
		/// <param name="writer">Destination writer. Not owned: the caller flushes and disposes it, and configures its
		/// <see cref="XmlWriterSettings"/> (indentation, encoding, conformance level, ...).</param>
		public XmlWriterEmitter(XmlWriter writer)
		{
			Contract.NotNull(writer);
			this.Writer = writer;
		}

		/// <inheritdoc />
		public void WriteStartElement(in XmlName name) => this.Writer.WriteStartElement(name.Text);

		/// <inheritdoc />
		public void WriteAttribute(in XmlName name, ReadOnlySpan<char> value) => this.Writer.WriteAttributeString(name.Text, value.ToString());

		/// <inheritdoc />
		public void WriteText(ReadOnlySpan<char> text) => this.Writer.WriteString(text.ToString());

		/// <inheritdoc />
		public void WriteText(string? text)
		{
			if (text is not null)
			{
				this.Writer.WriteString(text);
			}
		}

		/// <inheritdoc />
		public void WriteRawAscii(ReadOnlySpan<char> ascii)
		{
			Contract.Debug.Requires(XmlCharHelpers.IsAscii(ascii), "Raw content must be pre-validated ASCII");
			// see the type remarks: WriteString, not WriteRaw, so that an empty value still counts as content
			this.Writer.WriteString(ascii.ToString());
		}

		/// <inheritdoc />
		public void WriteRawAscii(string? ascii)
		{
			if (ascii is not null)
			{
				// delegate to the span overload, like XDocumentEmitter and CrystalXmlWriter do, so the ASCII
				// precondition above is not bypassed for callers going through this null-tolerant overload
				WriteRawAscii(ascii.AsSpan());
			}
		}

		/// <inheritdoc />
		public void WriteEndElement(in XmlName name) => this.Writer.WriteEndElement();

	}

}
