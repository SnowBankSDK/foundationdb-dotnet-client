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

	/// <summary>Set of XML output events emitted by generated serializers</summary>
	/// <remarks>
	/// <para>This is the only surface the generated <c>WriteXml</c> bodies talk to: element nesting, self-closing
	/// decisions and character escaping are the implementation's business, not the caller's.</para>
	/// <para>Implementations are always value types, reached through a <c>where TEmitter : struct, ICrystalXmlEmitter</c>
	/// constraint so that every call devirtualizes. They carry mutable state and <b>must always be passed by ref</b>:
	/// passing one by value silently discards everything written through the copy.</para>
	/// <para>Two families implement it: the text emitter <see cref="CrystalXmlWriter{TRune,TWriter}"/>, which produces
	/// a byte-exact output, and the infoset emitters, which build a DOM or delegate to <c>System.Xml</c> and therefore
	/// only guarantee infoset equivalence.</para>
	/// <para><b>Prefixes never cross this interface.</b> A caller names a namespace, never an alias: the members below take a
	/// <see cref="CrystalXmlNamespace"/> and the implementation decides which prefix stands for it, because a prefix depends
	/// on the depth of the element that declares it and on what is already in scope, and neither is knowledge the caller has.
	/// Any alias reads back to the same expanded name, so nothing in the document's meaning depends on the choice.</para>
	/// <para>An implementation declares a namespace it needs and has not got, on the element that is currently open, so a
	/// caller that writes nothing but elements and attributes still produces a well-formed document.
	/// <see cref="WriteNamespaceDeclaration"/> and <see cref="WriteDefaultNamespaceDeclaration"/> exist for the caller that
	/// wants a declaration placed higher than its first use, which is what keeps a repeated namespace declared once.</para>
	/// </remarks>
	[PublicAPI]
	public interface ICrystalXmlEmitter
	{

		/// <summary>Opens a new element</summary>
		/// <param name="name">Name of the element, in both its text and UTF-8 representations</param>
		/// <remarks>The start tag stays open until content is written or the element is closed, so that
		/// <see cref="WriteAttribute(in CrystalXmlName,ReadOnlySpan{char})"/> can still append attributes to it.</remarks>
		void WriteStartElement(in CrystalXmlName name);

		/// <summary>Opens a new element in an explicit namespace</summary>
		/// <param name="name">Local name of the element, in both its text and UTF-8 representations</param>
		/// <param name="ns">Namespace of the element, which takes precedence over the one <paramref name="name"/> carries</param>
		/// <remarks>This overload exists so that one cached name can be written in more than one namespace: the item name
		/// <c>string</c> is in the collections namespace under a <c>List&lt;string&gt;</c> and in the XML Schema namespace as the
		/// value of a type annotation, and caching it twice would be caching the same three bytes twice.</remarks>
		void WriteStartElement(in CrystalXmlName name, in CrystalXmlNamespace ns);

		/// <summary>Appends an attribute to the start tag that is currently open</summary>
		/// <param name="name">Name of the attribute, in both its text and UTF-8 representations</param>
		/// <param name="value">Value of the attribute, escaped by the implementation</param>
		/// <remarks>Only valid between a <see cref="WriteStartElement(in CrystalXmlName)"/> and the first content event or
		/// <see cref="WriteEndElement"/> of that element.</remarks>
		void WriteAttribute(in CrystalXmlName name, ReadOnlySpan<char> value);

		/// <summary>Appends an attribute in an explicit namespace to the start tag that is currently open</summary>
		/// <param name="name">Local name of the attribute, in both its text and UTF-8 representations</param>
		/// <param name="ns">Namespace of the attribute, which takes precedence over the one <paramref name="name"/> carries</param>
		/// <param name="value">Value of the attribute, escaped by the implementation</param>
		/// <remarks>An attribute is never in the default namespace: a namespaced attribute always takes a prefix, which is why
		/// this overload and the one above are not the same call with an empty namespace.</remarks>
		void WriteAttribute(in CrystalXmlName name, in CrystalXmlNamespace ns, ReadOnlySpan<char> value);

		/// <summary>Appends an attribute whose VALUE is a qualified name</summary>
		/// <param name="name">Name of the attribute, whose own namespace is the one it carries</param>
		/// <param name="value">The qualified name the attribute carries: its local name, and the namespace it belongs to</param>
		/// <inheritdoc cref="WriteQNameAttribute(in CrystalXmlName,in CrystalXmlNamespace,in CrystalXmlName)" path="/remarks"/>
		void WriteQNameAttribute(in CrystalXmlName name, in CrystalXmlName value);

		/// <summary>Appends an attribute in an explicit namespace whose VALUE is a qualified name</summary>
		/// <param name="name">Local name of the attribute (<c>type</c>, on the DataContract format)</param>
		/// <param name="ns">Namespace of the attribute, which takes precedence over the one <paramref name="name"/> carries</param>
		/// <param name="value">The qualified name the attribute carries: its local name, and the namespace it belongs to</param>
		/// <remarks>Separate from <see cref="WriteAttribute(in CrystalXmlName,in CrystalXmlNamespace,ReadOnlySpan{char})"/>
		/// because a qualified name is a namespace and a local name, not text: the implementation resolves the namespace to a
		/// prefix in scope and writes <c>prefix:Local</c>, so nothing formats a string to describe a name the implementation
		/// already holds.</remarks>
		void WriteQNameAttribute(in CrystalXmlName name, in CrystalXmlNamespace ns, in CrystalXmlName value);

		/// <summary>Declares a namespace on the start tag that is currently open, under a prefix the implementation picks</summary>
		/// <param name="ns">Namespace to declare</param>
		/// <remarks>
		/// <para>The declaration covers the open element and everything inside it, so this is how a caller places one
		/// declaration above several uses instead of letting each use declare its own. A collection wrapper naming its items'
		/// namespace, or an element whose nested contract lives in another namespace than its own name, are the two shapes
		/// that need it: without them each child declares the same namespace again.</para>
		/// <para>Asking for a namespace that is already in scope writes NOTHING. The call means "this namespace is usable
		/// inside this element", and an inherited declaration already says so; binding a second alias to one namespace would
		/// only add bytes. So a caller can ask unconditionally, which is what lets one generated body serve both a root
		/// element (whose namespace its caller already declared) and a nested one (whose caller did not).</para>
		/// <para>Only valid while the start tag is still open.</para>
		/// </remarks>
		void WriteNamespaceDeclaration(in CrystalXmlNamespace ns);

		/// <summary>Declares a namespace as the DEFAULT namespace on the start tag that is currently open</summary>
		/// <param name="ns">Namespace to declare, or the empty namespace to cancel an inherited default</param>
		/// <remarks>
		/// <para>The default namespace covers the ELEMENTS of the open element's subtree, never their attributes. An element
		/// whose own namespace is the default therefore needs no prefix, which is what makes a document readable.</para>
		/// <para><b>It does not change the namespace of the element that carries it.</b> An element gets its namespace when it
		/// is opened, and nothing afterwards moves it. So an element that is itself in the namespace it declares says so at
		/// <see cref="WriteStartElement(in CrystalXmlName,in CrystalXmlNamespace)"/>, which also declares it when nothing in
		/// scope binds it. The text emitter would produce the same bytes either way, but the infoset implementations have
		/// committed the name by then, and a call that means one thing on one sink and another on the next is worth no bytes.</para>
		/// </remarks>
		void WriteDefaultNamespaceDeclaration(in CrystalXmlNamespace ns);

		/// <summary>Appends text content to the element that is currently open, escaping it as needed</summary>
		/// <param name="text">Raw text; the implementation escapes it</param>
		/// <remarks>Writing text always counts as content, so an <b>empty</b> span still forces the expanded
		/// <c>&lt;Name&gt;&lt;/Name&gt;</c> form instead of the self-closing one.</remarks>
		void WriteText(ReadOnlySpan<char> text);

		/// <summary>Appends text content to the element that is currently open, treating <see langword="null"/> as "no content at all"</summary>
		/// <param name="text">Raw text. <see langword="null"/> writes nothing, leaving the element free to self-close as
		/// <c>&lt;Name /&gt;</c>; an <b>empty</b> string counts as content and forces the expanded
		/// <c>&lt;Name&gt;&lt;/Name&gt;</c> form.</param>
		/// <remarks>This member is on the interface, and not merely an overload on the concrete emitters, because generated
		/// bodies only see interface members through the <c>where TEmitter : struct, ICrystalXmlEmitter</c> constraint. Were it
		/// absent, <c>emitter.WriteText(someString)</c> would bind <see cref="WriteText(ReadOnlySpan{char})"/> through the
		/// implicit <c>string</c> conversion, turning a <see langword="null"/> into an empty span and flipping the output from
		/// <c>&lt;Name /&gt;</c> to <c>&lt;Name&gt;&lt;/Name&gt;</c>.</remarks>
		void WriteText(string? text);

		/// <summary>Appends content that is already known to be valid, unescaped ASCII</summary>
		/// <param name="ascii">Pre-validated ASCII content: a formatted number, a date, a base64 payload, ...</param>
		/// <remarks>
		/// <para>This bypasses the escaper entirely, which is the point: these forms cannot contain a character that
		/// would need escaping. Passing arbitrary user text here would emit malformed XML.</para>
		/// <para>Like <see cref="WriteText(ReadOnlySpan{char})"/>, this counts as content.</para>
		/// </remarks>
		void WriteRawAscii(ReadOnlySpan<char> ascii);

		/// <summary>Appends pre-validated ASCII content, treating <see langword="null"/> as "no content at all"</summary>
		/// <param name="ascii">Pre-validated ASCII content. <see langword="null"/> writes nothing, leaving the element free
		/// to self-close; an <b>empty</b> string counts as content and forces the expanded form.</param>
		/// <remarks>On the interface for the same reason as <see cref="WriteText(string?)"/>: an interface-constrained
		/// caller must get the same format as a caller holding the concrete struct.</remarks>
		void WriteRawAscii(string? ascii);

		/// <summary>Closes the element that is currently open</summary>
		/// <param name="name">Name of the element being closed, in both its text and UTF-8 representations</param>
		/// <remarks>The name is passed back by the caller (the generated code always knows it statically) so that
		/// implementations do not have to allocate a stack of names.</remarks>
		void WriteEndElement(in CrystalXmlName name);

	}

}
