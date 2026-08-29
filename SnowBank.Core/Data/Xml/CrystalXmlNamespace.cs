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

	/// <summary>Namespace URI of an XML element or attribute, held in both its text and UTF-8 representations</summary>
	/// <remarks>
	/// <para>Same dual representation, and for the same reason, as <see cref="CrystalXmlName"/>: a namespace URI reaches the
	/// output as the value of a declaration attribute, so the <c>char</c> core copies <see cref="Text"/> and the
	/// <c>byte</c> core copies <see cref="Utf8"/>, and neither ever transcodes.</para>
	/// <para>A namespace carries no prefix. The prefix an element or attribute is written under depends on the depth of the
	/// element that declares it and on what is already in scope, so it belongs to the emitter and is assigned there; the
	/// same namespace value is reused across documents and depths without change.</para>
	/// <para>Three states, and they are distinct in the output:</para>
	/// <list type="bullet">
	/// <item><see cref="None"/> (the <see langword="default"/> value): no namespace was specified. An element in this state
	/// inherits whatever default namespace is in scope, and an attribute in this state is in no namespace at all.</item>
	/// <item>The empty URI, from <c>Create("")</c>: the absence of a namespace, stated explicitly. This is what
	/// <c>[DataContract(Namespace = "")]</c> means, and declaring it writes <c>xmlns=""</c>.</item>
	/// <item>An absolute URI: the namespace itself.</item>
	/// </list>
	/// </remarks>
	[PublicAPI]
	[DebuggerDisplay("{Text,nq}")]
	public readonly struct CrystalXmlNamespace : IEquatable<CrystalXmlNamespace>
	{

		/// <summary>UTF-8 representation, kept as a <see cref="ReadOnlyMemory{T}"/> because a span cannot be a field</summary>
		private readonly ReadOnlyMemory<byte> Bytes;

		/// <summary>Constructs a namespace from its two representations</summary>
		/// <param name="uri">Text representation of the URI, as written by the <c>char</c> core</param>
		/// <param name="utf8">UTF-8 representation of <paramref name="uri"/>, as written by the <c>byte</c> core</param>
		/// <remarks>The caller is responsible for the two representations agreeing, and for <paramref name="uri"/> being a
		/// legal namespace URI: this is the <b>trusted, non-validating</b> path, for the frozen literal pairs generated code
		/// emits. Prefer <see cref="Create"/> for anything else.</remarks>
		public CrystalXmlNamespace(string uri, ReadOnlyMemory<byte> utf8)
		{
			this.Text = uri;
			this.Bytes = utf8;
		}

		/// <summary>Text representation of the URI, or <see langword="null"/> when no namespace was specified</summary>
		public string? Text { get; }

		/// <summary>UTF-8 representation of the URI</summary>
		public ReadOnlySpan<byte> Utf8 => this.Bytes.Span;

		/// <summary>The absence of a specified namespace: an element inherits the default namespace in scope, an attribute is in no namespace</summary>
		public static CrystalXmlNamespace None => default;

		/// <summary>Whether no namespace was specified at all</summary>
		/// <remarks>Distinct from <see cref="IsEmpty"/>: see the type remarks.</remarks>
		public bool IsNone => this.Text is null;

		/// <summary>Whether this is the empty URI, the explicitly stated absence of a namespace</summary>
		public bool IsEmpty => this.Text is { Length: 0 };

		/// <summary>Builds a namespace from its URI, validating it and computing the UTF-8 representation now</summary>
		/// <param name="uri">Namespace URI, or the empty string for the explicit absence of a namespace</param>
		/// <returns>Namespace usable by both output cores</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="uri"/> is <see langword="null"/></exception>
		/// <exception cref="ArgumentException">If <paramref name="uri"/> is neither empty nor a well-formed absolute URI, or if it contains one of <c>&amp;</c> <c>&lt;</c> <c>&gt;</c> <c>&quot;</c></exception>
		/// <remarks>
		/// <para>This transcodes, validates and allocates, so it belongs at setup time, never on a per-element path. The
		/// URI must be ABSOLUTE, which is what XML namespaces require and what every namespace the DataContract format uses
		/// is (<c>http://schemas.datacontract.org/2004/07/Acme</c>, <c>urn:acme:catalog:1</c>); a relative reference is
		/// rejected here rather than written into a document no reader can resolve.</para>
		/// <para>The four characters an attribute value has to escape are rejected too, so that an emitter writes a URI the
		/// same way it writes a name: one copy of whichever representation matches its output unit, with no scan and no
		/// escaper. A URI that needs one of them percent-encodes it, which is what the URI rules ask for anyway.</para>
		/// </remarks>
		public static CrystalXmlNamespace Create(string uri)
		{
			Contract.NotNull(uri);

			if (uri.Length == 0)
			{ // the explicit absence of a namespace, which is a legal declaration value (xmlns="")
				return new(uri, default);
			}

			if (!Uri.IsWellFormedUriString(uri, UriKind.Absolute))
			{
				throw new ArgumentException($"'{uri}' is not a well-formed absolute URI, so it cannot be an XML namespace.", nameof(uri));
			}

			if (uri.IndexOf('&') >= 0 || uri.IndexOf('<') >= 0 || uri.IndexOf('>') >= 0 || uri.IndexOf('"') >= 0)
			{
				throw new ArgumentException($"'{uri}' contains a character that an XML attribute value has to escape ('&', '<', '>' or '\"'). Percent-encode it.", nameof(uri));
			}

			return new(uri, Encoding.UTF8.GetBytes(uri));
		}

		/// <inheritdoc />
		public bool Equals(CrystalXmlNamespace other) => string.Equals(this.Text, other.Text, StringComparison.Ordinal);

		/// <inheritdoc />
		public override bool Equals(object? obj) => obj is CrystalXmlNamespace other && Equals(other);

		/// <inheritdoc />
		public override int GetHashCode() => this.Text is null ? 0 : StringComparer.Ordinal.GetHashCode(this.Text);

		/// <summary>Tests two namespaces for equality of their URI</summary>
		public static bool operator ==(CrystalXmlNamespace left, CrystalXmlNamespace right) => left.Equals(right);

		/// <summary>Tests two namespaces for inequality of their URI</summary>
		public static bool operator !=(CrystalXmlNamespace left, CrystalXmlNamespace right) => !left.Equals(right);

		/// <inheritdoc />
		public override string ToString() => this.Text ?? string.Empty;

	}

	/// <summary>The namespaces of the DataContract format that no CLR namespace derives</summary>
	/// <remarks>
	/// <para>Five URIs, each chosen by the SHAPE of what is being written rather than by where a type is declared. They are
	/// here, cached and shared, so that a document that uses one does not transcode it and a generated container does not
	/// carry its own copy of the bytes.</para>
	/// <para>Two of them have a conventional prefix, and the emitters use it (see <see cref="GetConventionalPrefix"/>).</para>
	/// </remarks>
	[PublicAPI]
	public static class CrystalXmlNamespaces
	{

		/// <summary>The XML Schema instance namespace, which carries the <c>nil</c> and <c>type</c> attributes</summary>
		public const string XmlSchemaInstanceUri = "http://www.w3.org/2001/XMLSchema-instance";

		/// <summary>Prefix the reference implementation spells <see cref="XmlSchemaInstanceUri"/> with</summary>
		public const string XmlSchemaInstancePrefix = "i";

		/// <summary>The XML Schema namespace, which qualifies the type of a primitive value in an <c>anyType</c> slot</summary>
		public const string XmlSchemaUri = "http://www.w3.org/2001/XMLSchema";

		/// <summary>The namespace of an unannotated generic collection or dictionary</summary>
		public const string ArraysUri = "http://schemas.microsoft.com/2003/10/Serialization/Arrays";

		/// <summary>The object-graph serialization namespace, which carries the <c>Id</c> and <c>Ref</c> attributes</summary>
		public const string SerializationUri = "http://schemas.microsoft.com/2003/10/Serialization/";

		/// <summary>Prefix the reference implementation spells <see cref="SerializationUri"/> with</summary>
		public const string SerializationPrefix = "z";

		/// <summary>The namespace of the built-in contracts of the <c>System</c> types, of which <see cref="DateTimeOffset"/> is the one this format writes</summary>
		public const string SystemContractUri = "http://schemas.datacontract.org/2004/07/System";

		/// <inheritdoc cref="XmlSchemaInstanceUri"/>
		public static readonly CrystalXmlNamespace XmlSchemaInstance = CrystalXmlNamespace.Create(XmlSchemaInstanceUri);

		/// <inheritdoc cref="XmlSchemaUri"/>
		public static readonly CrystalXmlNamespace XmlSchema = CrystalXmlNamespace.Create(XmlSchemaUri);

		/// <inheritdoc cref="ArraysUri"/>
		public static readonly CrystalXmlNamespace Arrays = CrystalXmlNamespace.Create(ArraysUri);

		/// <inheritdoc cref="SerializationUri"/>
		public static readonly CrystalXmlNamespace Serialization = CrystalXmlNamespace.Create(SerializationUri);

		/// <inheritdoc cref="SystemContractUri"/>
		public static readonly CrystalXmlNamespace SystemContract = CrystalXmlNamespace.Create(SystemContractUri);

		/// <summary>Returns the prefix a namespace is conventionally spelled with, or <see langword="null"/> when it has none</summary>
		/// <param name="uri">Namespace URI</param>
		/// <remarks>Every emitter asks this first, so that <c>i:nil</c> reads as <c>i:nil</c> in a document from any of them.
		/// An emitter that has to invent a prefix for anything else is free to, since a reader resolves a prefix through the
		/// declarations in scope and never by its spelling.</remarks>
		internal static string? GetConventionalPrefix(string uri)
			=> uri switch
			{
				XmlSchemaInstanceUri => XmlSchemaInstancePrefix,
				SerializationUri => SerializationPrefix,
				_ => null,
			};

	}

}
