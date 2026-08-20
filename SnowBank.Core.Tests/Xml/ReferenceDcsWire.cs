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

// This file IS compiled for the net472 validation target: the lite (netstandard2.0/net472) path is in scope for
// CrystalXml, so the certification suite runs there too, against the netstandard2.0 build of SnowBank.Core on the
// real .NET Framework CLR. The oracle below is a LIVE DataContractSerializer, so on that target it is the netfx
// DCS that produces the reference format: every fixture comparing the CrystalXml emitter to it is therefore also a
// measurement that the two CLRs agree, and all of them pass byte for byte.

namespace SnowBank.Data.Xml.Tests
{
	using System.Runtime.Serialization;
	using System.Xml;

	/// <summary>
	/// Live-DCS oracle: the actual <see cref="DataContractSerializer"/> writing through a namespace-stripping
	/// <see cref="XmlWriter"/> decorator, followed by an invalid-character filter.
	/// </summary>
	/// <remarks>
	/// <para>This is the clean-room reimplementation of the reference pipeline that was measured against a real
	/// <see cref="DataContractSerializer"/> (writer with 2 args, <c>CheckCharacters = false</c>,
	/// <c>OmitXmlDeclaration = true</c>, followed by a post-serialization filter). Every format fidelity test in
	/// <c>DcsWireFidelityFacts</c> compares the CrystalXml emitter's output against THIS output, byte for byte: the
	/// suite stays provable without any dependency on application-specific behavior.</para>
	/// <para>Recorded the of a live DataContractSerializer, used as the conformance oracle; comments translated to
	/// English.</para>
	/// </remarks>
	internal static class ReferenceDcsWire
	{
		/// <summary>Serializes <paramref name="value"/> through the live <see cref="DataContractSerializer"/> reference pipeline</summary>
		public static string Serialize(object? value, Type declaredType)
		{
			var ser = new DataContractSerializer(declaredType);
			var sb = new StringBuilder();
			// pin the newline convention to CRLF: XmlWriter defaults NewLineChars to Environment.NewLine, so without
			// this the oracle emits "\n" on macOS/Linux and disagrees with CrystalXml's fixed CRLF. Fixing it here
			// keeps the oracle platform-independent.
			var settings = new XmlWriterSettings { CheckCharacters = false, OmitXmlDeclaration = true, NewLineChars = "\r\n" };
			using (var writer = new StrippingXmlWriter(XmlWriter.Create(sb, settings)))
			{
				ser.WriteObject(writer, value);
			}
			return RemoveInvalidXmlChars(sb.ToString());
		}

		/// <summary>Equivalent of the reference filter: keeps valid XML 1.0 <c>Char</c> code points and valid surrogate pairs</summary>
		private static string RemoveInvalidXmlChars(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return text;
			}
			var sb = new StringBuilder(text.Length);
			for (int i = 0; i < text.Length; i++)
			{
				if (XmlConvert.IsXmlChar(text[i]))
				{
					sb.Append(text[i]);
				}
				else if (i + 1 < text.Length && XmlConvert.IsXmlSurrogatePair(text[i + 1], text[i]))
				{
					sb.Append(text[i]).Append(text[i + 1]);
					i++;
				}
			}
			return sb.ToString();
		}

		/// <summary>
		/// <see cref="XmlWriter"/> decorator that forces an empty prefix and namespace on every element and attribute,
		/// and drops <c>xmlns</c> declarations entirely (value included).
		/// </summary>
		private sealed class StrippingXmlWriter(XmlWriter inner) : XmlWriter
		{
			private bool skipValue;

			public override WriteState WriteState => inner.WriteState;

			public override void WriteStartElement(string? prefix, string localName, string? ns)
				=> inner.WriteStartElement(string.Empty, localName, string.Empty);

			public override void WriteStartAttribute(string? prefix, string localName, string? ns)
			{
				if (prefix == "xmlns" || (string.IsNullOrEmpty(prefix) && localName == "xmlns"))
				{
					this.skipValue = true;
					return;
				}
				inner.WriteStartAttribute(string.Empty, localName, string.Empty);
			}

			public override void WriteEndAttribute()
			{
				if (this.skipValue)
				{
					this.skipValue = false;
					return;
				}
				inner.WriteEndAttribute();
			}

			public override void WriteString(string? text)
			{
				if (this.skipValue)
				{
					return;
				}
				inner.WriteString(text);
			}

			public override void WriteQualifiedName(string localName, string? ns)
				=> inner.WriteQualifiedName(localName, string.Empty);

			public override void WriteEndElement() => inner.WriteEndElement();
			public override void WriteFullEndElement() => inner.WriteFullEndElement();
			public override void WriteStartDocument() { }
			public override void WriteStartDocument(bool standalone) { }
			public override void WriteEndDocument() => inner.WriteEndDocument();
			public override void WriteDocType(string name, string? pubid, string? sysid, string? subset) { }
			public override void WriteBase64(byte[] buffer, int index, int count) => inner.WriteBase64(buffer, index, count);
			public override void WriteCData(string? text) => inner.WriteCData(text);
			public override void WriteCharEntity(char ch) => inner.WriteCharEntity(ch);
			public override void WriteChars(char[] buffer, int index, int count) => inner.WriteChars(buffer, index, count);
			public override void WriteComment(string? text) => inner.WriteComment(text);
			public override void WriteEntityRef(string name) => inner.WriteEntityRef(name);
			public override void WriteProcessingInstruction(string name, string? text) => inner.WriteProcessingInstruction(name, text);
			public override void WriteRaw(char[] buffer, int index, int count) => inner.WriteRaw(buffer, index, count);
			public override void WriteRaw(string data) => inner.WriteRaw(data);
			public override void WriteSurrogateCharEntity(char lowChar, char highChar) => inner.WriteSurrogateCharEntity(lowChar, highChar);
			public override void WriteWhitespace(string? ws) => inner.WriteWhitespace(ws);
			public override string? LookupPrefix(string ns) => inner.LookupPrefix(ns);
			public override void Flush() => inner.Flush();

			protected override void Dispose(bool disposing)
			{
				if (disposing)
				{
					inner.Dispose();
				}
				base.Dispose(disposing);
			}
		}
	}

}

