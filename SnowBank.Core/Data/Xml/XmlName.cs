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

	/// <summary>Name of an XML element or attribute, held in both its text and UTF-8 representations</summary>
	/// <remarks>
	/// <para>The dual representation exists so that neither output core ever has to convert: the <c>char</c> core copies
	/// <see cref="Text"/>, the <c>byte</c> core copies <see cref="Utf8"/>. Names are written far more often than any other
	/// token in a document, so transcoding them at write time would dominate the cost.</para>
	/// <para>Generated code emits one cached <c>static readonly</c> instance per name, built from a frozen UTF-8 literal so
	/// that no transcoding happens at all, not even once, via the public constructor below, which is the
	/// <b>trusted, non-validating</b> path: the generator already validated the literal at compile time, so re-validating
	/// it on every process start would be pure waste.</para>
	/// <code>private static readonly XmlName TagsName = new("Tags", "Tags"u8.ToArray());</code>
	/// <para>Use <see cref="Create"/> instead for names that are not known at compile time - typically a <c>rootName</c>
	/// override coming from a caller, or a dictionary key written under <see cref="XmlDictionaryFormat.Direct"/>. Unlike
	/// the constructor, <see cref="Create"/> <b>validates</b> that the name is a legal XML NCName (no colon, no leading
	/// digit, no whitespace, ...) and raises <see cref="CrystalXmlInvalidNameException"/> if it is not: this is the one
	/// place user-supplied text turns into a name, so a silent pass-through would let a single bad rootName or key
	/// corrupt the whole document into unparseable XML instead of failing loudly at the source.</para>
	/// </remarks>
	[PublicAPI]
	[DebuggerDisplay("{Text,nq}")]
	public readonly struct XmlName
	{

		/// <summary>UTF-8 representation, kept as a <see cref="ReadOnlyMemory{T}"/> because a span cannot be a field</summary>
		private readonly ReadOnlyMemory<byte> Bytes;

		/// <summary>Constructs a name from its two representations</summary>
		/// <param name="text">Text representation of the name, as written in the document by the <c>char</c> core</param>
		/// <param name="utf8">UTF-8 representation of <paramref name="text"/>, as written by the <c>byte</c> core</param>
		/// <remarks>The caller is responsible for the two representations agreeing: nothing checks that
		/// <paramref name="utf8"/> is the UTF-8 encoding of <paramref name="text"/>. Prefer <see cref="Create"/> unless you
		/// are emitting a frozen literal pair.</remarks>
		public XmlName(string text, ReadOnlyMemory<byte> utf8)
		{
			this.Text = text;
			this.Bytes = utf8;
		}

		/// <summary>Text representation of the name</summary>
		public string Text { get; }

		/// <summary>UTF-8 representation of the name</summary>
		public ReadOnlySpan<byte> Utf8 => this.Bytes.Span;

		/// <summary>Builds a name from its text, validating it and computing the UTF-8 representation now</summary>
		/// <param name="text">Text representation of the name</param>
		/// <returns>Name usable by both output cores</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="text"/> is <see langword="null"/></exception>
		/// <exception cref="CrystalXmlInvalidNameException">If <paramref name="text"/> is not a valid XML NCName (empty, a colon, a leading digit, embedded whitespace, ...)</exception>
		/// <remarks>This transcodes, validates and allocates, so it belongs at setup time (a cached static, a <c>rootName</c>
		/// override), never on a per-element path. For a frozen literal already known to be valid at compile time, use the
		/// constructor instead: see the type remarks.</remarks>
		public static XmlName Create(string text)
		{
			Contract.NotNull(text);
			try
			{
				XmlConvert.VerifyNCName(text);
			}
			catch (Exception ex) when (ex is XmlException or ArgumentException)
			{
				// XmlException for a malformed name (space, leading digit, colon, ...); ArgumentException for an
				// empty string specifically (XmlConvert.VerifyNCName special-cases that one via ThrowIfNullOrEmpty
				// instead of going through its usual XmlException path). Either way, forward VerifyNCName's own
				// message so the diagnostic says WHY the name was rejected, not just that it was.
				throw new CrystalXmlInvalidNameException(text, $"'{text}' is not a valid XML name: {ex.Message}");
			}
			return new(text, Encoding.UTF8.GetBytes(text));
		}

		/// <inheritdoc />
		public override string ToString() => this.Text ?? string.Empty;

	}

}
