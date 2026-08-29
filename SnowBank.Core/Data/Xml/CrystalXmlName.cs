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
	using System.Text;
	using System.Xml;

	/// <summary>Name of an XML element or attribute, held in both its text and UTF-8 representations, with an optional namespace</summary>
	/// <remarks>
	/// <para>The dual representation exists so that neither output core ever has to convert: the <c>char</c> core copies
	/// <see cref="Text"/>, the <c>byte</c> core copies <see cref="Utf8"/>. Names are written far more often than any other
	/// token in a document, so transcoding them at write time would dominate the cost.</para>
	/// <para>Generated code emits one cached <c>static readonly</c> instance per name, built from a frozen UTF-8 literal so
	/// that no transcoding happens at all, not even once, via the public constructor below, which is the
	/// <b>trusted, non-validating</b> path: the generator already validated the literal at compile time, so re-validating
	/// it on every process start would be pure waste.</para>
	/// <code>private static readonly CrystalXmlName TagsName = new("Tags", "Tags"u8.ToArray());</code>
	/// <para>Use <see cref="Create(string)"/> instead for names that are not known at compile time (a caller's <c>rootName</c>
	/// override, a dictionary key written under <see cref="CrystalXmlDictionaryFormat.Direct"/>). Unlike the constructor,
	/// <see cref="Create(string)"/> <b>validates</b> that the name is a legal XML NCName and raises
	/// <see cref="XmlException"/> if it is not: this is the one place user-supplied text turns into a
	/// name, and a bad name must throw rather than corrupt the document.</para>
	/// <para><see cref="Namespace"/> is optional and defaults to <see cref="CrystalXmlNamespace.None"/>, so a name built the
	/// way it always was keeps meaning exactly what it did. A name holds the <b>local name and the namespace</b> and never a
	/// prefix: both of those are properties of the name itself, while the prefix depends on the depth of the element that
	/// declares the namespace and on what is in scope there, so the emitter assigns it. That is what keeps a name cacheable:
	/// one static instance serves every document and every depth.</para>
	/// </remarks>
	[PublicAPI]
	[DebuggerDisplay("{Text,nq}")]
	public readonly struct CrystalXmlName
	{

		/// <summary>UTF-8 representation, kept as a <see cref="ReadOnlyMemory{T}"/> because a span cannot be a field</summary>
		private readonly ReadOnlyMemory<byte> Bytes;

		/// <summary>Constructs a name from its two representations</summary>
		/// <param name="text">Text representation of the name, as written in the document by the <c>char</c> core</param>
		/// <param name="utf8">UTF-8 representation of <paramref name="text"/>, as written by the <c>byte</c> core</param>
		/// <remarks>The caller is responsible for the two representations agreeing: nothing checks that
		/// <paramref name="utf8"/> is the UTF-8 encoding of <paramref name="text"/>. Prefer <see cref="Create(string)"/> unless you
		/// are emitting a frozen literal pair. The name gets no namespace, which is what every name of a document without
		/// namespaces is.</remarks>
		public CrystalXmlName(string text, ReadOnlyMemory<byte> utf8)
		{
			this.Text = text;
			this.Bytes = utf8;
			this.Namespace = CrystalXmlNamespace.None;
		}

		/// <summary>Constructs a namespaced name from its two representations</summary>
		/// <param name="text">Text representation of the LOCAL name, without any prefix, as written by the <c>char</c> core</param>
		/// <param name="utf8">UTF-8 representation of <paramref name="text"/>, as written by the <c>byte</c> core</param>
		/// <param name="ns">Namespace of the name</param>
		/// <inheritdoc cref="CrystalXmlName(string,ReadOnlyMemory{byte})" path="/remarks"/>
		public CrystalXmlName(string text, ReadOnlyMemory<byte> utf8, in CrystalXmlNamespace ns)
		{
			this.Text = text;
			this.Bytes = utf8;
			this.Namespace = ns;
		}

		/// <summary>Text representation of the local name, without any prefix</summary>
		public string Text { get; }

		/// <summary>UTF-8 representation of the local name</summary>
		public ReadOnlySpan<byte> Utf8 => this.Bytes.Span;

		/// <summary>Namespace of the name, or <see cref="CrystalXmlNamespace.None"/> when it has none</summary>
		public CrystalXmlNamespace Namespace { get; }

		/// <summary>Builds a name from its text, validating it and computing the UTF-8 representation now</summary>
		/// <param name="text">Text representation of the name</param>
		/// <returns>Name usable by both output cores</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="text"/> is <see langword="null"/></exception>
		/// <exception cref="XmlException">If <paramref name="text"/> is not a valid XML NCName (a colon, a leading digit, embedded whitespace, ...)</exception>
		/// <exception cref="ArgumentException">If <paramref name="text"/> is empty</exception>
		/// <remarks>This transcodes, validates and allocates, so it belongs at setup time (a cached static, a <c>rootName</c>
		/// override), never on a per-element path. For a frozen literal already known to be valid at compile time, use the
		/// constructor instead: see the type remarks.</remarks>
		public static CrystalXmlName Create(string text)
		{
			Contract.NotNull(text);
			// XmlException for a malformed name; ArgumentException for an empty string (VerifyNCName special-cases it)
			XmlConvert.VerifyNCName(text);
			return new(text, Encoding.UTF8.GetBytes(text));
		}

		/// <summary>Builds a namespaced name, validating both halves and computing the UTF-8 representations now</summary>
		/// <param name="text">Text representation of the LOCAL name, without any prefix</param>
		/// <param name="namespaceUri">Namespace URI, or <see langword="null"/> for no namespace</param>
		/// <returns>Name usable by both output cores</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="text"/> is <see langword="null"/></exception>
		/// <exception cref="XmlException">If <paramref name="text"/> is not a valid XML NCName</exception>
		/// <exception cref="ArgumentException">If <paramref name="text"/> is empty, or <paramref name="namespaceUri"/> is neither empty nor a well-formed absolute URI</exception>
		/// <remarks>The two halves are validated by different rules, which is why they cannot share one check: the local name
		/// is an NCName and therefore carries no colon, while a namespace URI does (<c>urn:acme:catalog:1</c>). See
		/// <see cref="CrystalXmlNamespace.Create"/> for the namespace rule.</remarks>
		public static CrystalXmlName Create(string text, string? namespaceUri)
		{
			Contract.NotNull(text);
			XmlConvert.VerifyNCName(text);
			var ns = namespaceUri is null ? CrystalXmlNamespace.None : CrystalXmlNamespace.Create(namespaceUri);
			return new(text, Encoding.UTF8.GetBytes(text), in ns);
		}

		/// <inheritdoc />
		public override string ToString() => this.Text ?? string.Empty;

	}

}
