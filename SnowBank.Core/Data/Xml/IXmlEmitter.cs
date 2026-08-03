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

	/// <summary>Vocabulary of XML output events emitted by generated serializers</summary>
	/// <remarks>
	/// <para>This is the only surface the generated <c>WriteXml</c> bodies talk to. It is deliberately tiny and
	/// stateless-looking: element nesting, self-closing decisions and character escaping are the implementation's
	/// business, not the caller's.</para>
	/// <para>Implementations are always value types, reached through a <c>where TEmitter : struct, IXmlEmitter</c>
	/// constraint so that every call devirtualizes. They carry mutable state and <b>must always be passed by ref</b>:
	/// passing one by value silently discards everything written through the copy.</para>
	/// <para>Two families implement it: the text emitter <see cref="CrystalXmlWriter{TRune,TWriter}"/>, which produces
	/// a byte-exact wire, and the infoset emitters, which build a DOM or delegate to <c>System.Xml</c> and therefore
	/// only guarantee infoset equivalence.</para>
	/// </remarks>
	[PublicAPI]
	public interface IXmlEmitter
	{

		/// <summary>Opens a new element</summary>
		/// <param name="name">Name of the element, in both its text and UTF-8 representations</param>
		/// <remarks>The start tag stays open until content is written or the element is closed, so that
		/// <see cref="WriteAttribute"/> can still append attributes to it.</remarks>
		void WriteStartElement(in XmlName name);

		/// <summary>Appends an attribute to the start tag that is currently open</summary>
		/// <param name="name">Name of the attribute, in both its text and UTF-8 representations</param>
		/// <param name="value">Value of the attribute, escaped by the implementation</param>
		/// <remarks>Only valid between a <see cref="WriteStartElement"/> and the first content event or
		/// <see cref="WriteEndElement"/> of that element.</remarks>
		void WriteAttribute(in XmlName name, ReadOnlySpan<char> value);

		/// <summary>Appends text content to the element that is currently open, escaping it as needed</summary>
		/// <param name="text">Raw text; the implementation escapes it</param>
		/// <remarks>Writing text always counts as content, so an <b>empty</b> span still forces the expanded
		/// <c>&lt;Name&gt;&lt;/Name&gt;</c> form instead of the self-closing one.</remarks>
		void WriteText(ReadOnlySpan<char> text);

		/// <summary>Appends content that is already known to be valid, unescaped ASCII</summary>
		/// <param name="ascii">Pre-validated ASCII content: a formatted number, a date, a base64 payload, ...</param>
		/// <remarks>
		/// <para>This bypasses the escaper entirely, which is the point: these forms cannot contain a character that
		/// would need escaping. Passing arbitrary user text here would emit malformed XML.</para>
		/// <para>Like <see cref="WriteText"/>, this counts as content.</para>
		/// </remarks>
		void WriteRawAscii(ReadOnlySpan<char> ascii);

		/// <summary>Closes the element that is currently open</summary>
		/// <param name="name">Name of the element being closed, in both its text and UTF-8 representations</param>
		/// <remarks>The name is passed back by the caller (the generated code always knows it statically) so that
		/// implementations do not have to allocate a stack of names.</remarks>
		void WriteEndElement(in XmlName name);

	}

}
