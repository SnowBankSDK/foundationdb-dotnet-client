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
	using System.Buffers;
	using System.Runtime.InteropServices;

	/// <summary>XML text emitter that produces a byte-exact output, over either UTF-16 or UTF-8 output</summary>
	/// <typeparam name="TRune">Output unit: <see cref="char"/> for UTF-16 text, or <see cref="byte"/> for UTF-8. Any other
	/// type is rejected by the constructor.</typeparam>
	/// <typeparam name="TWriter">Destination buffer writer. Must be a value type so that every write devirtualizes.</typeparam>
	/// <remarks>
	/// <para>Replicates, byte for byte, the output produced by <c>DataContractSerializer</c> writing through an
	/// <c>XmlWriter</c> (settings <c>CheckCharacters = false</c>, <c>OmitXmlDeclaration = true</c>, no indentation) followed
	/// by an invalid-character filter.</para>
	/// <para>Byte-compatibility rules, all measured against that reference implementation and all in force under the default
	/// <see cref="CrystalXmlSettings"/> (compact, self-closing, no declaration):</para>
	/// <list type="number">
	/// <item>No XML declaration: the document starts with the root element. A caller that sets
	/// <see cref="CrystalXmlSettings.WriteXmlDeclaration"/> gets one <c>&lt;?xml version="1.0" encoding="..."?&gt;</c> line
	/// before the root, naming whatever encoding the finished output is in; version and encoding only, no <c>standalone</c>.</item>
	/// <item>A prefix and an <c>xmlns</c> declaration appear exactly where a caller names a namespace, and nowhere else, so a
	/// caller that names none produces a document with no prefix and no declaration in it. This writer decides what a prefix
	/// is called and where a missing declaration goes (see the section on namespaces below); it does not decide whether an
	/// element has a namespace.</item>
	/// <item>Text content escapes <c>&amp;</c> <c>&lt;</c> <c>&gt;</c>, and only those; the quote characters stay raw.</item>
	/// <item>In text content, every line ending (<c>\r\n</c>, a lone <c>\r</c>, a lone <c>\n</c>) becomes a <b>raw</b>
	/// <c>\r\n</c>, never a character reference; a TAB stays raw. This normalization is fixed and does not depend on
	/// <see cref="CrystalXmlSettings.NewLine"/>, which only ever governs the structural whitespace <see cref="CrystalXmlSettings.Indented"/>
	/// inserts between elements, never a value's own text.</item>
	/// <item>Attribute values escape <c>&amp;</c> <c>&lt;</c> <c>&quot;</c> but <b>not</b> <c>&gt;</c>.</item>
	/// <item>Attribute values write TAB, LF and CR as the character references <c>&amp;#x9;</c>, <c>&amp;#xA;</c> and
	/// <c>&amp;#xD;</c>; no line-ending normalization happens inside an attribute.</item>
	/// <item>C0 control characters, which XML 1.0 forbids outright, are <b>dropped</b>. This is a deliberate deviation: the
	/// reference writer emits them as character references under <c>CheckCharacters = false</c> and its post-filter lets
	/// those through (they are plain ASCII once escaped), producing a format that no conformant reader can parse. Set
	/// <see cref="StrictControlCharacters"/> to reproduce that defect exactly.</item>
	/// <item>Unpaired surrogate halves are dropped; a valid surrogate pair passes through whole (and becomes a single
	/// 4-byte sequence on the UTF-8 core, never CESU-8).</item>
	/// <item>U+FFFE and U+FFFF are dropped, like the reference filter does.</item>
	/// <item>An element with no content self-closes as <c>&lt;Name /&gt;</c>, with a space before the slash; writing an
	/// <b>empty</b> string as content forces the expanded form <c>&lt;Name&gt;&lt;/Name&gt;</c>. A caller that sets
	/// <see cref="CrystalXmlSettings.EmptyElementStyle"/> to <see cref="CrystalXmlEmptyElementStyle.Paired"/> gets the
	/// expanded form for the genuinely empty case too, so every element with no content reads <c>&lt;Name&gt;&lt;/Name&gt;</c>.</item>
	/// <item>When <see cref="CrystalXmlSettings.Indented"/> is set, every child element starts on its own line, indented one
	/// TAB per nesting level, and an element's end tag gets its own line too, but only when that element opened at least one
	/// child element. An element whose content is text (or nothing) always stays on one line: indentation never touches a
	/// value. The writer is forward-only and cannot retract output it already wrote, so an element whose first child element
	/// precedes its own text keeps the indentation already written before that child; the child text values themselves are
	/// never altered.</item>
	/// </list>
	/// <para><b>Namespaces and prefixes.</b> A caller names namespaces; this writer names prefixes. Two rules, both chosen so
	/// that a document reads like the one the reference implementation writes:</para>
	/// <list type="number">
	/// <item>The XML Schema instance namespace takes the prefix <c>i</c> and the object-graph serialization namespace takes
	/// <c>z</c>, the spellings the reference implementation uses for them. Every other namespace takes
	/// <c>d{depth}p{n}</c>, where <c>depth</c> is the depth of the element carrying the declaration, counted from 1 at the
	/// root, and <c>n</c> counts the declarations on that element.</item>
	/// <item>A namespace that is used and not in scope is declared on the element that is currently open, which is the first
	/// element that uses it. The first namespace declared in a document with no default namespace in scope becomes the
	/// DEFAULT namespace, so a root writes <c>&lt;Library xmlns="urn:biblio"&gt;</c> and its unprefixed children inherit it.
	/// A caller that wants a declaration higher up asks for it with
	/// <see cref="WriteNamespaceDeclaration"/>.</item>
	/// </list>
	/// <para>An alias is not part of a document's meaning: a reader resolves a prefix through the declarations in scope and
	/// matches the namespace, so two documents that differ only in their aliases and in where the declarations sit read back
	/// identically. That is what lets this writer choose both.</para>
	/// <para><b>Always pass this struct by ref, and abandon the writer variable you constructed it from.</b> The emitter
	/// holds the element state and the destination writer inline, so a copy silently loses every write made through it.
	/// The constructor <b>copies</b> the writer into <see cref="Writer"/>: with a pooled sink (for instance <c>SliceWriter</c>)
	/// the caller's variable becomes a second claim on the same buffer, so disposing, resetting or reusing it is double
	/// ownership and, after the emitter's buffer has grown or been returned, a use-after-return. Everything after
	/// construction, including reading the output and disposing the sink, goes through <see cref="Writer"/>:</para>
	/// <code>
	/// var sink = new ValueStringWriter();
	/// var emitter = new CrystalXmlWriter&lt;char, ValueStringWriter&gt;(ref sink);
	/// Emit(ref emitter);                       // always by ref
	/// string xml = emitter.Writer.ToString();  // read back HERE, never from `sink`
	/// </code>
	/// <para>The <typeparamref name="TRune"/> divergence is concentrated in a handful of leaf primitives (ASCII literal,
	/// escaped text, precomputed name, character reference); everything above them is written once. The
	/// <c>typeof(TRune) == typeof(char)</c> tests are folded away by the JIT, so neither core pays for the other.</para>
	/// </remarks>
	[PublicAPI]
	[DebuggerDisplay("Depth={Depth}, TagPending={TagPending}")]
	public struct CrystalXmlWriter<TRune, TWriter> : ICrystalXmlEmitter
		where TRune : unmanaged
		where TWriter : struct, IBufferWriter<TRune>
	{

		/// <summary>Largest number of UTF-16 code units transcoded to UTF-8 in a single buffer request</summary>
		/// <remarks>Bounds the size of the span asked from the writer for an arbitrarily long value, at 3 bytes per code
		/// unit (the worst case for the BMP; a surrogate pair is 4 bytes for 2 units, so it stays under the bound).</remarks>
		private const int Utf8ChunkSize = 512;

		/// <summary>Destination writer, held inline; the only live view of the output</summary>
		/// <remarks>This is where the caller reads the output back and disposes the sink; the writer variable passed to the
		/// constructor is a dead copy: see the remarks on <see cref="CrystalXmlWriter{TRune,TWriter}"/>.</remarks>
		public TWriter Writer;

		/// <summary>When <see langword="true"/>, C0 control characters are emitted as character references instead of being dropped</summary>
		/// <remarks>Reproduces a defect of the legacy format, and produces XML that no conformant reader accepts. Only the
		/// certification harness, which compares against captured legacy output, should turn this on.</remarks>
		public readonly bool StrictControlCharacters;

		/// <summary>When <see langword="true"/>, the output is pretty-printed across multiple lines instead of written compact on one line</summary>
		/// <remarks>Element content only: an element whose content is text (or nothing) always stays on one line, so a value
		/// never gains or loses whitespace because of this setting. See <see cref="CrystalXmlSettings.Indented"/>.</remarks>
		private readonly bool Indented;

		/// <summary>Structural line ending written between elements, consulted only when <see cref="Indented"/> is set</summary>
		private readonly CrystalXmlNewLine NewLine;

		/// <summary>How an element with no content at all is written</summary>
		private readonly CrystalXmlEmptyElementStyle EmptyElementStyle;

		/// <summary>True until the <c>&lt;?xml ...?&gt;</c> declaration has been written, or for the whole document when none is requested</summary>
		private bool DeclarationPending;

		/// <summary>IANA name written into the declaration's <c>encoding</c> attribute</summary>
		/// <remarks>Fixed at construction: the char core always writes <c>utf-16</c> (a <see cref="string"/> is UTF-16), and
		/// the byte core writes <c>utf-8</c> unless the caller names a different target encoding for a later transcoding
		/// pass over the finished buffer.</remarks>
		private readonly string DeclarationEncoding;

		/// <summary>Number of elements currently open</summary>
		public int Depth { get; private set; }

		/// <summary>True when the current start tag is still open, so attributes may still be written</summary>
		private bool TagPending;

		/// <summary>True when the current element has received content (text, raw content, or a child element)</summary>
		private bool HasContent;

		/// <summary>True when the current element has received at least one child element (as opposed to only text or raw content)</summary>
		/// <remarks>Decides indentation before an element's own end tag: an element whose content is text stays on one line,
		/// so only an element that opened at least one child gets its end tag pushed to its own indented line. Reset for
		/// each new element the same way <see cref="HasContent"/> is, and read back by <see cref="WriteEndElement"/> before
		/// it is overwritten for the parent.</remarks>
		private bool HasElementContent;

		/// <summary>Namespace declarations currently in scope, outermost first, or <see langword="null"/> while the document has used no namespace</summary>
		/// <remarks>Allocated on the first declaration, so a document without namespaces (which is every document the general
		/// format produces) pays nothing for this machinery.</remarks>
		private XmlScope[]? Scopes;

		/// <summary>Number of live entries at the front of <see cref="Scopes"/></summary>
		private int ScopeCount;

		/// <summary>Prefix each open element was written under, indexed by its depth minus one</summary>
		/// <remarks>Read back by <see cref="WriteEndElement"/>, so that a close tag repeats the prefix of its own open tag
		/// instead of resolving the namespace a second time. An absent entry reads as "no prefix", which is why the array is
		/// only allocated once an element actually takes one.</remarks>
		private XmlPrefix[]? ElementPrefixes;

		/// <summary>Whether the element at a given depth has received direct text or raw content, indexed by depth minus one</summary>
		/// <remarks>Unlike <see cref="HasContent"/> and <see cref="HasElementContent"/>, which are flat fields safely shared
		/// across nesting levels (every child element unconditionally forces them true for its parent on close, so any value
		/// a deeper element left behind is re-asserted true regardless), this one cannot be flat: a later sibling must see
		/// whether the CURRENT element has text, not whether some earlier child happened to have text of its own. Tracked
		/// per depth for the same reason <see cref="ElementPrefixes"/> is.</remarks>
		private bool[]? HasTextContentByDepth;

		/// <summary>Number of depth-numbered prefixes already minted on the start tag that is still open</summary>
		/// <remarks>This is the <c>n</c> of a <c>d{depth}p{n}</c> prefix. It counts only depth-numbered prefixes: a
		/// conventional prefix and the default declaration take no number, so they do not shift the ones that do.</remarks>
		private int PendingDeclarations;

		/// <summary>Constructs an emitter writing into <paramref name="writer"/></summary>
		/// <param name="writer">Destination buffer writer, <b>consumed</b> by this constructor: it is copied into
		/// <see cref="Writer"/>, and the caller's variable must not be read, disposed or reused afterwards. The <c>ref</c>
		/// only avoids copying a large struct as an argument; no aliasing is established (a <c>ref</c> field is impossible
		/// on the <c>netstandard2.0</c> and <c>net8.0</c> targets).</param>
		/// <param name="strictControlCharacters">When <see langword="true"/>, reproduce the legacy character-reference
		/// treatment of C0 control characters instead of dropping them. See <see cref="StrictControlCharacters"/>.</param>
		/// <param name="settings">Writer-level output options: <see cref="CrystalXmlSettings.Indented"/>,
		/// <see cref="CrystalXmlSettings.NewLine"/>, <see cref="CrystalXmlSettings.EmptyElementStyle"/> and
		/// <see cref="CrystalXmlSettings.WriteXmlDeclaration"/>. Defaults to <see cref="CrystalXmlSettings.General"/>, which
		/// reproduces today's compact, self-closing, declaration-less output.</param>
		/// <param name="declarationEncoding">IANA name written into the declaration's <c>encoding</c> attribute, when
		/// <see cref="CrystalXmlSettings.WriteXmlDeclaration"/> is set. Defaults to <c>utf-16</c> on the char core and
		/// <c>utf-8</c> on the byte core; a caller transcoding the finished byte buffer to another encoding passes that
		/// encoding's own name here instead.</param>
		/// <exception cref="NotSupportedException">If <typeparamref name="TRune"/> is neither <see cref="char"/> nor <see cref="byte"/>.</exception>
		public CrystalXmlWriter(ref TWriter writer, bool strictControlCharacters = false, CrystalXmlSettings settings = default, string? declarationEncoding = null)
		{
			if (typeof(TRune) != typeof(char) && typeof(TRune) != typeof(byte))
			{
				throw ErrorUnsupportedRune();
			}

			this.Writer = writer;
			this.StrictControlCharacters = strictControlCharacters;
			this.Indented = settings.Indented;
			this.NewLine = settings.NewLine;
			this.EmptyElementStyle = settings.EmptyElementStyle;
			this.DeclarationPending = settings.WriteXmlDeclaration;
			this.DeclarationEncoding = declarationEncoding ?? (typeof(TRune) == typeof(char) ? "utf-16" : "utf-8");
			this.Depth = 0;
			this.TagPending = false;
			this.HasContent = false;
			this.HasElementContent = false;
			this.Scopes = null;
			this.ScopeCount = 0;
			this.ElementPrefixes = null;
			this.HasTextContentByDepth = null;
			this.PendingDeclarations = 0;
		}

		private static NotSupportedException ErrorUnsupportedRune()
			=> new($"{nameof(CrystalXmlWriter<TRune, TWriter>)} only supports 'char' (UTF-16) or 'byte' (UTF-8) as its output unit, but was instantiated with '{typeof(TRune).Name}'.");

		#region Events...

		/// <inheritdoc />
		public void WriteStartElement(in CrystalXmlName name) => WriteStartElement(in name, name.Namespace);

		/// <inheritdoc />
		public void WriteStartElement(in CrystalXmlName name, in CrystalXmlNamespace ns)
		{
			CloseTagIfPending();
			int depth = this.Depth + 1;

			if (depth == 1)
			{ // the root element: the declaration, if any, comes first and nothing else precedes it
				WriteDeclarationIfPending();
			}
			else if (this.Indented && !GetHasTextContent(this.Depth))
			{ // every other element starts its own line, indented one level past its parent, unless the parent already
			  // holds text: indentation never touches a value, so a mixed-content parent stays compact from here on
				WriteNewLineAndIndent(this.Depth);
			}

			this.PendingDeclarations = 0;

			// the prefix has to be known before the name is written, and the declaration that binds it can only be written
			// after: hence the two steps, with the decision taken first and emitted second
			var prefix = ResolveElementPrefix(in ns, depth, out bool declare, out bool asDefault);

			WriteAscii("<");
			WritePrefix(in prefix);
			WriteName(in name);

			this.Depth = depth;
			this.TagPending = true;
			this.HasContent = false;
			this.HasElementContent = false;
			if (this.Indented)
			{ // the flags are only ever read while indenting, so a compact document never allocates the per-depth array
				SetHasTextContent(depth, false);
			}
			SetElementPrefix(depth, in prefix);

			if (declare)
			{
				EmitDeclaration(depth, in ns, in prefix, asDefault);
			}
		}

		/// <inheritdoc />
		public void WriteAttribute(in CrystalXmlName name, ReadOnlySpan<char> value) => WriteAttribute(in name, name.Namespace, value);

		/// <inheritdoc />
		public void WriteAttribute(in CrystalXmlName name, in CrystalXmlNamespace ns, ReadOnlySpan<char> value)
		{
			Contract.Debug.Requires(this.TagPending, "Attributes can only be written while the start tag is still open");
			// resolved (and declared, if it comes to that) BEFORE the attribute starts: a declaration is an attribute of its
			// own, and one cannot be written in the middle of another
			var prefix = ResolveAttributePrefix(in ns);
			WriteAscii(" ");
			WritePrefix(in prefix);
			WriteName(in name);
			WriteAscii("=\"");
			WriteEscaped(value, inAttribute: true);
			WriteAscii("\"");
		}

		/// <inheritdoc />
		public void WriteQNameAttribute(in CrystalXmlName name, in CrystalXmlName value) => WriteQNameAttribute(in name, name.Namespace, in value);

		/// <inheritdoc />
		public void WriteQNameAttribute(in CrystalXmlName name, in CrystalXmlNamespace ns, in CrystalXmlName value)
		{
			Contract.Debug.Requires(this.TagPending, "Attributes can only be written while the start tag is still open");
			Contract.Debug.Requires(value.Text is not null, "The qualified name was never initialized");

			// BOTH namespaces are resolved first, for the same reason as above, and the attribute's own comes first so that
			// the declarations appear in the order the attributes that use them do
			var prefix = ResolveAttributePrefix(in ns);
			var valuePrefix = ResolveQNameValuePrefix(value.Namespace);

			WriteAscii(" ");
			WritePrefix(in prefix);
			WriteName(in name);
			WriteAscii("=\"");
			WritePrefix(in valuePrefix);
			WriteName(in value);
			WriteAscii("\"");
		}

		/// <inheritdoc />
		public void WriteNamespaceDeclaration(in CrystalXmlNamespace ns)
		{
			Contract.Debug.Requires(this.TagPending, "A namespace declaration can only be written while the start tag is still open");

			if (ns.Text is not { Length: > 0 } uri)
			{ // no prefix can stand for the empty namespace, so this is the one URI that is always the default declaration
				WriteDefaultNamespaceDeclaration(in ns);
				return;
			}

			if (TryLookupPrefix(uri, forAttribute: false, out _))
			{ // already in scope for this subtree: a second declaration would bind a second alias to one namespace, and a
			  // caller asking for a declaration is asking for the namespace to be usable below, which it already is
				return;
			}

			var prefix = NewPrefix(uri, this.Depth);
			EmitDeclaration(this.Depth, in ns, in prefix, asDefault: false);
		}

		/// <inheritdoc />
		public void WriteDefaultNamespaceDeclaration(in CrystalXmlNamespace ns)
		{
			Contract.Debug.Requires(this.TagPending, "A namespace declaration can only be written while the start tag is still open");
			EmitDeclaration(this.Depth, in ns, XmlPrefix.Default, asDefault: true);
		}

		/// <inheritdoc />
		public void WriteText(ReadOnlySpan<char> text)
		{
			CloseTagIfPending();
			// even an empty span counts: it is what forces the expanded <Name></Name> form
			this.HasContent = true;
			if (this.Indented)
			{
				SetHasTextContent(this.Depth, true);
			}
			WriteEscaped(text, inAttribute: false);
		}

		/// <inheritdoc />
		public void WriteText(string? text)
		{
			if (text is not null)
			{
				WriteText(text.AsSpan());
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
			CloseTagIfPending();
			this.HasContent = true;
			if (this.Indented)
			{
				SetHasTextContent(this.Depth, true);
			}
			WriteAscii(ascii);
		}

		/// <inheritdoc />
		public void WriteRawAscii(string? ascii)
		{
			if (ascii is not null)
			{
				WriteRawAscii(ascii.AsSpan());
			}
		}

		/// <inheritdoc />
		public void WriteEndElement(in CrystalXmlName name)
		{
			Contract.Debug.Requires(this.Depth > 0, "There is no open element to close");
			int depth = this.Depth;
			--this.Depth;
			bool hadElementContent = this.HasElementContent;
			bool hadTextContent = GetHasTextContent(depth);

			if (this.TagPending && !this.HasContent && this.EmptyElementStyle == CrystalXmlEmptyElementStyle.SelfClosing)
			{
				// no content at all, and the caller wants the self-closing form: including the space the reference writer emits
				WriteAscii(" />");
				this.TagPending = false;
			}
			else
			{
				CloseTagIfPending();
				if (this.Indented && hadElementContent && !hadTextContent)
				{ // this element had at least one child element and no text of its own, so its end tag gets its own
				  // indented line; text content, wherever it fell among the children, keeps the whole element compact
					WriteNewLineAndIndent(depth - 1);
				}
				WriteAscii("</");
				WritePrefix(GetElementPrefix(depth));
				WriteName(in name);
				WriteAscii(">");
			}

			// whatever happens, the parent element now has content: this child
			this.HasContent = true;
			this.HasElementContent = true;

			// the declarations this element carried go out of scope with it
			PopScopes(depth);
		}

		private void CloseTagIfPending()
		{
			if (this.TagPending)
			{
				WriteAscii(">");
				this.TagPending = false;
			}
		}

		/// <summary>Writes the <c>&lt;?xml ...?&gt;</c> declaration once, if the caller asked for one, before the root element</summary>
		private void WriteDeclarationIfPending()
		{
			if (!this.DeclarationPending) return;
			this.DeclarationPending = false;

			WriteAscii("<?xml version=\"1.0\" encoding=\"");
			WriteAscii(this.DeclarationEncoding.AsSpan());
			WriteAscii("\"?>");

			if (this.Indented)
			{ // compact output has no separator at all: the root element follows immediately
				WriteStructuralNewLine();
			}
		}

		/// <summary>Writes <see cref="NewLine"/></summary>
		private void WriteStructuralNewLine() => WriteAscii(this.NewLine == CrystalXmlNewLine.Lf ? "\n" : "\r\n");

		/// <summary>Writes <see cref="NewLine"/> followed by <paramref name="tabs"/> TAB characters, one per nesting level</summary>
		private void WriteNewLineAndIndent(int tabs)
		{
			WriteStructuralNewLine();
			for (int i = 0; i < tabs; i++)
			{
				WriteAscii("\t");
			}
		}

		#endregion

		#region Namespaces...

		/// <summary>Prefix a name is written under: a fixed spelling, or a depth-numbered one</summary>
		/// <remarks>The <see langword="default"/> value is the ABSENT prefix, which is what an element in the default
		/// namespace and an attribute in no namespace both take. That is what lets an unrecorded element read back as
		/// unprefixed without any bookkeeping.</remarks>
		private readonly struct XmlPrefix
		{

			/// <summary>Fixed spelling (<c>i</c>, <c>z</c>), or <see langword="null"/> for a depth-numbered prefix</summary>
			public readonly string? Literal;

			/// <summary>Depth of the element carrying the declaration, for a depth-numbered prefix</summary>
			public readonly int Depth;

			/// <summary>Rank of the declaration on that element, for a depth-numbered prefix</summary>
			public readonly int Number;

			public XmlPrefix(string? literal, int depth, int number)
			{
				this.Literal = literal;
				this.Depth = depth;
				this.Number = number;
			}

			/// <summary>The absent prefix</summary>
			public static XmlPrefix Default => default;

			/// <summary>Whether this is the absent prefix, so that nothing at all is written before the local name</summary>
			public bool IsDefault => this.Literal is null && this.Depth == 0;

		}

		/// <summary>One namespace declaration, and the element it was written on</summary>
		private readonly struct XmlScope
		{

			/// <summary>Depth of the element carrying the declaration, counted from 1 at the root</summary>
			public readonly int Depth;

			/// <summary>URI this declaration binds, the empty string for the empty namespace</summary>
			public readonly string Uri;

			/// <summary>Prefix this declaration binds <see cref="Uri"/> to</summary>
			public readonly XmlPrefix Prefix;

			public XmlScope(int depth, string uri, in XmlPrefix prefix)
			{
				this.Depth = depth;
				this.Uri = uri;
				this.Prefix = prefix;
			}

		}

		/// <summary>Returns the prefix <paramref name="uri"/> is in scope under</summary>
		/// <param name="uri">Namespace URI to resolve, the empty string for the empty namespace</param>
		/// <param name="forAttribute">
		/// <see langword="true"/> to resolve the name of an ATTRIBUTE, which skips the default namespace: an unprefixed
		/// attribute is in no namespace whatever default is in scope, so a default declaration cannot serve one.
		/// </param>
		/// <param name="prefix">Prefix found, when this returns <see langword="true"/></param>
		/// <returns><see langword="false"/> when nothing in scope binds <paramref name="uri"/>, so the caller has to declare it</returns>
		/// <remarks>Innermost declaration first, which is what shadowing means: the same URI declared again deeper wins.</remarks>
		private bool TryLookupPrefix(string uri, bool forAttribute, out XmlPrefix prefix)
		{
			var scopes = this.Scopes;
			for (int i = this.ScopeCount - 1; i >= 0; i--)
			{
				var scope = scopes![i];
				if (forAttribute && scope.Prefix.IsDefault) continue;
				if (string.Equals(scope.Uri, uri, StringComparison.Ordinal))
				{
					prefix = scope.Prefix;
					return true;
				}
			}

			prefix = XmlPrefix.Default;
			return false;
		}

		/// <summary>Whether any default namespace declaration is in scope, whatever URI it binds</summary>
		/// <remarks>This is what decides between declaring a new namespace as the default and declaring it under a prefix: the
		/// first namespace of a document takes the default, and everything after it takes a prefix.</remarks>
		private bool HasDefaultNamespaceInScope()
		{
			var scopes = this.Scopes;
			for (int i = this.ScopeCount - 1; i >= 0; i--)
			{
				if (scopes![i].Prefix.IsDefault) return true;
			}
			return false;
		}

		/// <summary>Picks the prefix a new declaration of <paramref name="uri"/> binds, on an element at <paramref name="depth"/></summary>
		private XmlPrefix NewPrefix(string uri, int depth)
			=> CrystalXmlNamespaces.GetConventionalPrefix(uri) is { } conventional
				? new(conventional, 0, 0)
				: new(null, depth, this.PendingDeclarations + 1);

		/// <summary>Resolves the prefix an ELEMENT in <paramref name="ns"/> takes, and whether that element has to declare it</summary>
		/// <param name="ns">Namespace of the element</param>
		/// <param name="depth">Depth of the element, counted from 1 at the root</param>
		/// <param name="declare">Set to <see langword="true"/> when the element has to carry a declaration</param>
		/// <param name="asDefault">Set to <see langword="true"/> when that declaration is the default one (<c>xmlns="..."</c>)</param>
		private XmlPrefix ResolveElementPrefix(in CrystalXmlNamespace ns, int depth, out bool declare, out bool asDefault)
		{
			declare = false;
			asDefault = false;
			string uri = ns.Text ?? string.Empty;

			if (this.ScopeCount == 0 && uri.Length == 0)
			{ // the common case by far: a document that names no namespace at all
				return XmlPrefix.Default;
			}

			if (TryLookupPrefix(uri, forAttribute: false, out var prefix))
			{
				return prefix;
			}

			if (uri.Length == 0 && !HasDefaultNamespaceInScope())
			{ // there is no inherited default to cancel, so an unprefixed name is already in no namespace
				return XmlPrefix.Default;
			}

			declare = true;
			// the empty namespace can only be bound as the default (no prefix may stand for it), and so is the first
			// namespace of a document, so that a root and the children that share its namespace both read unprefixed
			asDefault = uri.Length == 0 || !HasDefaultNamespaceInScope();
			return asDefault ? XmlPrefix.Default : NewPrefix(uri, depth);
		}

		/// <summary>Resolves the prefix an ATTRIBUTE in <paramref name="ns"/> takes, declaring the namespace on the open element when nothing in scope binds it</summary>
		private XmlPrefix ResolveAttributePrefix(in CrystalXmlNamespace ns)
		{
			string uri = ns.Text ?? string.Empty;

			if (uri.Length == 0)
			{ // an unprefixed attribute is in no namespace, so there is nothing to resolve and nothing to declare
				return XmlPrefix.Default;
			}

			if (TryLookupPrefix(uri, forAttribute: true, out var prefix))
			{
				return prefix;
			}

			prefix = NewPrefix(uri, this.Depth);
			EmitDeclaration(this.Depth, in ns, in prefix, asDefault: false);
			return prefix;
		}

		/// <summary>Resolves the prefix the VALUE of a qualified-name attribute takes</summary>
		/// <remarks>A qualified name resolves an absent prefix against the DEFAULT namespace, unlike an attribute name, so the
		/// lookup is the element one: a derived type in the same namespace as the slot it fills writes a bare local name,
		/// which is what the reference implementation writes.</remarks>
		private XmlPrefix ResolveQNameValuePrefix(in CrystalXmlNamespace ns)
		{
			string uri = ns.Text ?? string.Empty;

			if (TryLookupPrefix(uri, forAttribute: false, out var prefix))
			{
				return prefix;
			}

			if (uri.Length == 0)
			{
				return XmlPrefix.Default;
			}

			prefix = NewPrefix(uri, this.Depth);
			EmitDeclaration(this.Depth, in ns, in prefix, asDefault: false);
			return prefix;
		}

		/// <summary>Writes one namespace declaration on the start tag that is open, and puts it in scope</summary>
		private void EmitDeclaration(int depth, in CrystalXmlNamespace ns, in XmlPrefix prefix, bool asDefault)
		{
			Contract.Debug.Requires(this.TagPending, "A namespace declaration is an attribute of the element that is currently open");

			WriteAscii(" xmlns");
			if (!asDefault)
			{
				WriteAscii(":");
				WritePrefixText(in prefix);
			}
			WriteAscii("=\"");
			WriteNamespaceUri(in ns);
			WriteAscii("\"");

			if (!asDefault && prefix.Literal is null)
			{ // only a depth-numbered prefix takes a number, so a conventional prefix and the default declaration do not
			  // shift the numbering: an element carrying xmlns:i and one foreign namespace spells the second one d{depth}p1
				++this.PendingDeclarations;
			}

			PushScope(depth, ns.Text ?? string.Empty, in prefix);
		}

		private void PushScope(int depth, string uri, in XmlPrefix prefix)
		{
			var scopes = this.Scopes;
			if (scopes is null)
			{
				this.Scopes = scopes = new XmlScope[8];
			}
			else if (this.ScopeCount == scopes.Length)
			{
				var grown = new XmlScope[scopes.Length * 2];
				Array.Copy(scopes, grown, this.ScopeCount);
				this.Scopes = scopes = grown;
			}

			scopes[this.ScopeCount++] = new(depth, uri, in prefix);
		}

		/// <summary>Drops every declaration carried by the element at <paramref name="depth"/> or deeper</summary>
		private void PopScopes(int depth)
		{
			while (this.ScopeCount > 0 && this.Scopes![this.ScopeCount - 1].Depth >= depth)
			{
				--this.ScopeCount;
			}
		}

		/// <summary>Records the prefix the element at <paramref name="depth"/> was written under, for its close tag</summary>
		private void SetElementPrefix(int depth, in XmlPrefix prefix)
		{
			var prefixes = this.ElementPrefixes;

			if (prefix.IsDefault && (prefixes is null || depth > prefixes.Length))
			{ // an absent entry already reads as the absent prefix, so there is nothing to record and nothing to allocate
				return;
			}

			if (prefixes is null)
			{
				this.ElementPrefixes = prefixes = new XmlPrefix[Math.Max(depth, 8)];
			}
			else if (depth > prefixes.Length)
			{
				var grown = new XmlPrefix[Math.Max(depth, prefixes.Length * 2)];
				Array.Copy(prefixes, grown, prefixes.Length);
				this.ElementPrefixes = prefixes = grown;
			}

			prefixes[depth - 1] = prefix;
		}

		/// <summary>Returns the prefix the element at <paramref name="depth"/> was written under</summary>
		private XmlPrefix GetElementPrefix(int depth)
		{
			var prefixes = this.ElementPrefixes;
			return prefixes is not null && depth <= prefixes.Length ? prefixes[depth - 1] : XmlPrefix.Default;
		}

		/// <summary>Records whether the element at <paramref name="depth"/> has received direct text or raw content</summary>
		private void SetHasTextContent(int depth, bool value)
		{
			if (depth == 0)
			{ // depth 0 is outside the document element and has no slot in the array; top-level text has nothing to record
				return;
			}

			var flags = this.HasTextContentByDepth;
			if (flags is null)
			{
				if (!value)
				{ // an absent array already reads as false everywhere, so there is nothing to allocate for
					return;
				}
				this.HasTextContentByDepth = flags = new bool[Math.Max(depth, 8)];
			}
			else if (depth > flags.Length)
			{
				var grown = new bool[Math.Max(depth, flags.Length * 2)];
				Array.Copy(flags, grown, flags.Length);
				this.HasTextContentByDepth = flags = grown;
			}

			flags[depth - 1] = value;
		}

		/// <summary>Returns whether the element at <paramref name="depth"/> has received direct text or raw content</summary>
		private bool GetHasTextContent(int depth)
		{
			var flags = this.HasTextContentByDepth;
			return flags is not null && depth <= flags.Length && flags[depth - 1];
		}

		#endregion

		#region Escaping...

		/// <summary>Writes <paramref name="text"/>, escaping and filtering it according to the measured rules</summary>
		/// <param name="text">Raw text to escape</param>
		/// <param name="inAttribute">
		/// <see langword="true"/> inside an attribute value: <c>"</c> is escaped, <c>&gt;</c> is not, and TAB/LF/CR become
		/// character references. <see langword="false"/> in element content: <c>&gt;</c> is escaped, <c>"</c> is not, TAB
		/// stays raw and line endings normalize to a raw CRLF.
		/// </param>
		/// <remarks>Characters that need no special treatment accumulate into a run, flushed in one go, so the common case
		/// costs a single bulk copy (or a single bulk transcode on the UTF-8 core) per value.</remarks>
		private void WriteEscaped(ReadOnlySpan<char> text, bool inAttribute)
		{
			int start = 0;

			for (int i = 0; i < text.Length; i++)
			{
				char c = text[i];

				if (c >= 0x20)
				{
					switch (c)
					{
						case '&':
						{
							FlushRun(text, start, i);
							WriteAscii("&amp;");
							start = i + 1;
							continue;
						}
						case '<':
						{
							FlushRun(text, start, i);
							WriteAscii("&lt;");
							start = i + 1;
							continue;
						}
						case '>' when !inAttribute:
						{
							FlushRun(text, start, i);
							WriteAscii("&gt;");
							start = i + 1;
							continue;
						}
						case '"' when inAttribute:
						{
							FlushRun(text, start, i);
							WriteAscii("&quot;");
							start = i + 1;
							continue;
						}
					}

					if (char.IsHighSurrogate(c))
					{
						if (i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
						{
							// valid pair: both code units stay inside the current run
							++i;
							continue;
						}

						// isolated half: dropped, like the reference filter
						FlushRun(text, start, i);
						start = i + 1;
						continue;
					}

					if (char.IsLowSurrogate(c) || c is '\uFFFE' or '\uFFFF')
					{
						// isolated low half, or a code unit that is not a character: dropped
						FlushRun(text, start, i);
						start = i + 1;
						continue;
					}

					// nothing to do: stays in the run
					continue;
				}

				switch (c)
				{
					case '\t' when !inAttribute:
					{
						// legal XML 1.0 whitespace: stays raw, and stays in the run
						continue;
					}
					case '\t':
					{
						FlushRun(text, start, i);
						WriteAscii("&#x9;");
						start = i + 1;
						continue;
					}
					case '\n' when inAttribute:
					{
						FlushRun(text, start, i);
						WriteAscii("&#xA;");
						start = i + 1;
						continue;
					}
					case '\r' when inAttribute:
					{
						FlushRun(text, start, i);
						WriteAscii("&#xD;");
						start = i + 1;
						continue;
					}
					case '\r' or '\n':
					{
						// text line endings are normalized to a fixed CRLF, the same rule DataContractSerializer's
						// NewLineHandling.Replace applies under a CRLF NewLineChars: a \r\n, a lone \r, or a lone \n
						// all become a raw \r\n, never a character reference. The CRLF is fixed, not Environment.NewLine,
						// so the output never depends on the platform.
						FlushRun(text, start, i);
						if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
						{
							++i;
						}
						WriteAscii("\r\n");
						start = i + 1;
						continue;
					}
					default:
					{
						// C0 control character, forbidden by XML 1.0. Dropped by default; emitted as a character reference
						// when reproducing the legacy defect.
						FlushRun(text, start, i);
						if (this.StrictControlCharacters)
						{
							WriteCharRef(c);
						}
						start = i + 1;
						continue;
					}
				}
			}

			FlushRun(text, start, text.Length);
		}

		/// <summary>Writes the pending run of characters that need no escaping</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void FlushRun(ReadOnlySpan<char> text, int start, int end)
		{
			if (end > start)
			{
				WriteVerbatim(text.Slice(start, end - start));
			}
		}

		/// <summary>Writes characters that need no escaping, transcoding to UTF-8 if that is the output unit</summary>
		/// <remarks>The caller guarantees that <paramref name="chars"/> contains no forbidden code unit and no unpaired
		/// surrogate half, which is what lets the UTF-8 path transcode without any validation.</remarks>
		private void WriteVerbatim(ReadOnlySpan<char> chars)
		{
			if (chars.Length == 0) return;

			if (typeof(TRune) == typeof(char))
			{
				var buffer = MemoryMarshal.Cast<TRune, char>(this.Writer.GetSpan(chars.Length));
				chars.CopyTo(buffer);
				this.Writer.Advance(chars.Length);
			}
			else
			{
				WriteVerbatimUtf8(chars);
			}
		}

		/// <summary>Transcodes characters to UTF-8 straight into the destination buffer, in bounded chunks</summary>
		private void WriteVerbatimUtf8(ReadOnlySpan<char> chars)
		{
			while (chars.Length > 0)
			{
				int take = chars.Length <= Utf8ChunkSize ? chars.Length : Utf8ChunkSize;
				if (take < chars.Length && char.IsHighSurrogate(chars[take - 1]))
				{
					// never split a surrogate pair across two chunks
					--take;
				}
				Contract.Debug.Assert(take > 0);

				var source = chars.Slice(0, take);
				// worst case is 3 bytes per code unit: a surrogate pair is 4 bytes for 2 units, so it stays under the bound
				var buffer = MemoryMarshal.Cast<TRune, byte>(this.Writer.GetSpan(take * 3));

				int p = 0;
				for (int i = 0; i < source.Length; i++)
				{
					char c = source[i];
					if (c < 0x80)
					{
						buffer[p++] = (byte) c;
					}
					else if (c < 0x800)
					{
						buffer[p++] = (byte) (0xC0 | (c >> 6));
						buffer[p++] = (byte) (0x80 | (c & 0x3F));
					}
					else if (char.IsHighSurrogate(c))
					{
						// the escaper only ever lets well-formed pairs reach here, and the chunking above never splits one,
						// so the low half is always present
						Contract.Debug.Assert(i + 1 < source.Length && char.IsLowSurrogate(source[i + 1]));
						int cp = char.ConvertToUtf32(c, source[++i]);
						buffer[p++] = (byte) (0xF0 | (cp >> 18));
						buffer[p++] = (byte) (0x80 | ((cp >> 12) & 0x3F));
						buffer[p++] = (byte) (0x80 | ((cp >> 6) & 0x3F));
						buffer[p++] = (byte) (0x80 | (cp & 0x3F));
					}
					else
					{
						buffer[p++] = (byte) (0xE0 | (c >> 12));
						buffer[p++] = (byte) (0x80 | ((c >> 6) & 0x3F));
						buffer[p++] = (byte) (0x80 | (c & 0x3F));
					}
				}

				this.Writer.Advance(p);
				chars = chars.Slice(take);
			}
		}

		/// <summary>Writes a hexadecimal character reference, in the uppercase unpadded form the reference writer produces</summary>
		private void WriteCharRef(int value)
		{
			Contract.Debug.Requires(value is >= 0 and <= 0xFFFF);

			Span<char> buffer = stackalloc char[8];
			buffer[0] = '&';
			buffer[1] = '#';
			buffer[2] = 'x';

			int p = 3;
			bool started = false;
			for (int shift = 12; shift >= 0; shift -= 4)
			{
				int digit = (value >> shift) & 0xF;
				if (digit == 0 && !started && shift > 0) continue;
				started = true;
				buffer[p++] = (char) (digit < 10 ? '0' + digit : 'A' + (digit - 10));
			}

			buffer[p++] = ';';
			WriteAscii(buffer.Slice(0, p));
		}

		#endregion

		#region Leaf primitives...

		/// <summary>Writes an element or attribute name, using whichever precomputed representation matches the output unit</summary>
		private void WriteName(in CrystalXmlName name)
		{
			// a default(CrystalXmlName) would emit `<>` on the char core and `<>` on the byte core: malformed either way, and a bug
			// in whatever produced the name rather than something to paper over here
			Contract.Debug.Requires(name.Text is not null, "The name was never initialized");

			if (typeof(TRune) == typeof(char))
			{
				var text = name.Text.AsSpan();
				var buffer = MemoryMarshal.Cast<TRune, char>(this.Writer.GetSpan(text.Length));
				text.CopyTo(buffer);
				this.Writer.Advance(text.Length);
			}
			else
			{
				var utf8 = name.Utf8;
				var buffer = MemoryMarshal.Cast<TRune, byte>(this.Writer.GetSpan(utf8.Length));
				utf8.CopyTo(buffer);
				this.Writer.Advance(utf8.Length);
			}
		}

		/// <summary>Writes a prefix and the colon that separates it from the local name, or nothing for the absent prefix</summary>
		private void WritePrefix(in XmlPrefix prefix)
		{
			if (prefix.IsDefault) return;

			WritePrefixText(in prefix);
			WriteAscii(":");
		}

		/// <summary>Writes the characters of a prefix, without the colon</summary>
		/// <remarks>A depth-numbered prefix is composed straight into a stack buffer: it is ASCII by construction, so it costs
		/// one bulk copy and no allocation, on either output unit.</remarks>
		private void WritePrefixText(in XmlPrefix prefix)
		{
			if (prefix.Literal is { } literal)
			{
				WriteAscii(literal.AsSpan());
				return;
			}

			Span<char> buffer = stackalloc char[24];
			int p = 0;
			buffer[p++] = 'd';
			p += WriteDigits(buffer.Slice(p), prefix.Depth);
			buffer[p++] = 'p';
			p += WriteDigits(buffer.Slice(p), prefix.Number);
			WriteAscii(buffer.Slice(0, p));
		}

		/// <summary>Writes the decimal digits of a positive value into <paramref name="buffer"/>, and returns how many</summary>
		private static int WriteDigits(Span<char> buffer, int value)
		{
			Contract.Debug.Requires(value > 0, "A depth and a declaration rank both start at one");

			int length = 0;
			for (int v = value; v != 0; v /= 10)
			{
				++length;
			}

			for (int i = length - 1, v = value; i >= 0; i--, v /= 10)
			{
				buffer[i] = (char) ('0' + (v % 10));
			}

			return length;
		}

		/// <summary>Writes a namespace URI as the value of a declaration attribute, using whichever precomputed representation matches the output unit</summary>
		/// <remarks>Verbatim, with no escaping: <see cref="CrystalXmlNamespace.Create"/> refuses the four characters an
		/// attribute value would have to escape, so a URI is one bulk copy exactly like a name.</remarks>
		private void WriteNamespaceUri(in CrystalXmlNamespace ns)
		{
			if (ns.Text is not { Length: > 0 })
			{ // the empty namespace declares an empty value: xmlns=""
				return;
			}

			if (typeof(TRune) == typeof(char))
			{
				var text = ns.Text.AsSpan();
				var buffer = MemoryMarshal.Cast<TRune, char>(this.Writer.GetSpan(text.Length));
				text.CopyTo(buffer);
				this.Writer.Advance(text.Length);
			}
			else
			{
				var utf8 = ns.Utf8;
				var buffer = MemoryMarshal.Cast<TRune, byte>(this.Writer.GetSpan(utf8.Length));
				utf8.CopyTo(buffer);
				this.Writer.Advance(utf8.Length);
			}
		}

		/// <summary>Writes ASCII-only content: markup punctuation, entity references, or pre-validated values</summary>
		/// <remarks>Every XML escape is ASCII, so on the UTF-8 core each character is exactly one byte. Callers must not
		/// route non-ASCII text here.</remarks>
		private void WriteAscii(ReadOnlySpan<char> ascii)
		{
			int count = ascii.Length;
			if (count == 0) return;

			if (typeof(TRune) == typeof(char))
			{
				var buffer = MemoryMarshal.Cast<TRune, char>(this.Writer.GetSpan(count));
				ascii.CopyTo(buffer);
			}
			else
			{
				var buffer = MemoryMarshal.Cast<TRune, byte>(this.Writer.GetSpan(count));
				for (int i = 0; i < count; i++)
				{
					buffer[i] = (byte) ascii[i];
				}
			}

			this.Writer.Advance(count);
		}

		#endregion

	}

}
