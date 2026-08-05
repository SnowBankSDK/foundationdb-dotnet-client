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
	/// <para>The other infoset sink for <see cref="ICrystalXmlEmitter"/> (the DOM-building one is <see cref="CrystalXDocumentEmitter"/>).
	/// Every event is a direct passthrough to the wrapped <see cref="System.Xml.XmlWriter"/>, which already implements the
	/// XML infoset (attribute ordering, escaping, self-closing). Only infoset equivalence is guaranteed, not a byte-exact
	/// format: the concrete bytes depend entirely on the <see cref="XmlWriterSettings"/> the caller configured.</para>
	/// <para>The attribute-ordering precondition from <see cref="ICrystalXmlEmitter"/> is already enforced by the wrapped
	/// writer, which throws on a misordered <see cref="XmlWriter.WriteAttributeString(string?,string?)"/>; this type adds no
	/// redundant check.</para>
	/// <para><b><see cref="WriteRawAscii(ReadOnlySpan{char})"/> routes through <see cref="XmlWriter.WriteString(string?)"/>,
	/// not <see cref="XmlWriter.WriteRaw(string?)"/>.</b> Measured: <c>WriteRaw(string.Empty)</c> writes nothing and lets the
	/// element self-close, dropping the "even empty content counts" rule of
	/// <see cref="ICrystalXmlEmitter.WriteRawAscii(ReadOnlySpan{char})"/>, whereas <c>WriteString(string.Empty)</c> forces the
	/// expanded form. Pre-validated ASCII never contains a character the escaper treats specially, so the output is identical
	/// for every non-empty value.</para>
	/// <para>Also measured against the BCL: the wrapped writer already applies XML 1.0 §2.11 end-of-line normalization on
	/// write and already entitizes TAB/LF/CR inside attribute values, so neither needs any help from this type.</para>
	/// <para><b>The format core's control-character sanitization does NOT apply here.</b> The characters XML 1.0 cannot
	/// represent (C0 controls, unpaired surrogate halves, U+FFFE/U+FFFF) reach the wrapped writer unchanged, which answers
	/// for them under its own <see cref="XmlWriterSettings.CheckCharacters"/> (throws by default); content that may carry
	/// them must be sanitized before it reaches the emitter.</para>
	/// <para>This type owns none of the writer's lifetime: the caller flushes and disposes it. Like every
	/// <see cref="ICrystalXmlEmitter"/>, it must still be passed by <see langword="ref"/> per the interface remarks.</para>
	/// </remarks>
	[PublicAPI]
	public readonly struct CrystalXmlWriterEmitter : ICrystalXmlEmitter
	{

		/// <summary>Destination writer that every event is forwarded to, unowned by this emitter</summary>
		public XmlWriter Writer { get; }

		/// <summary>Wraps an existing <see cref="System.Xml.XmlWriter"/></summary>
		/// <param name="writer">Destination writer. Not owned: the caller flushes and disposes it, and configures its
		/// <see cref="XmlWriterSettings"/> (indentation, encoding, conformance level, ...).</param>
		public CrystalXmlWriterEmitter(XmlWriter writer)
		{
			Contract.NotNull(writer);
			this.Writer = writer;
		}

		/// <inheritdoc />
		public void WriteStartElement(in CrystalXmlName name) => this.Writer.WriteStartElement(name.Text);

		/// <inheritdoc />
		public void WriteAttribute(in CrystalXmlName name, ReadOnlySpan<char> value) => this.Writer.WriteAttributeString(name.Text, value.ToString());

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
#if NET8_0_OR_GREATER
			Contract.Debug.Requires(System.Text.Ascii.IsValid(ascii), "Raw content must be pre-validated ASCII");
#else
			Contract.Debug.Requires(SnowBank.Buffers.Binary.UnsafeHelpers.IsAsciiString(ascii), "Raw content must be pre-validated ASCII");
#endif
			// see the type remarks: WriteString, not WriteRaw, so that an empty value still counts as content
			this.Writer.WriteString(ascii.ToString());
		}

		/// <inheritdoc />
		public void WriteRawAscii(string? ascii)
		{
			if (ascii is not null)
			{
				// delegate to the span overload, like CrystalXDocumentEmitter and CrystalXmlWriter do, so the ASCII
				// precondition above is not bypassed for callers going through this null-tolerant overload
				WriteRawAscii(ascii.AsSpan());
			}
		}

		/// <inheritdoc />
		public void WriteEndElement(in CrystalXmlName name) => this.Writer.WriteEndElement();

	}

}
