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

	/// <summary>XML text emitter that produces a byte-exact wire, over either UTF-16 or UTF-8 output</summary>
	/// <typeparam name="TRune">Output unit: <see cref="char"/> for UTF-16 text, or <see cref="byte"/> for UTF-8. Any other
	/// type is rejected by the constructor.</typeparam>
	/// <typeparam name="TWriter">Destination buffer writer. Must be a value type so that every write devirtualizes.</typeparam>
	/// <remarks>
	/// <para>Replicates, byte for byte, the wire produced by <c>DataContractSerializer</c> writing through a
	/// namespace-stripping <c>XmlWriter</c> (settings <c>CheckCharacters = false</c>, <c>OmitXmlDeclaration = true</c>, no
	/// indentation) followed by an invalid-character filter.</para>
	/// <para>Byte-compatibility rules, all measured against that reference implementation:</para>
	/// <list type="number">
	/// <item>No XML declaration, no prefixes, no <c>xmlns</c>, ever.</item>
	/// <item>Attributes keep their local name only (<c>nil</c>, <c>type</c>).</item>
	/// <item>Text content escapes <c>&amp;</c> <c>&lt;</c> <c>&gt;</c>, and only those; the quote characters stay raw.</item>
	/// <item>In text content, every line ending (<c>\r\n</c>, a lone <c>\r</c>, a lone <c>\n</c>) becomes a <b>raw</b>
	/// <c>\r\n</c>, never a character reference; a TAB stays raw.</item>
	/// <item>Attribute values escape <c>&amp;</c> <c>&lt;</c> <c>&quot;</c> but <b>not</b> <c>&gt;</c>.</item>
	/// <item>Attribute values write TAB, LF and CR as the character references <c>&amp;#x9;</c>, <c>&amp;#xA;</c> and
	/// <c>&amp;#xD;</c>; no line-ending normalization happens inside an attribute.</item>
	/// <item>C0 control characters, which XML 1.0 forbids outright, are <b>dropped</b>. This is a deliberate deviation: the
	/// reference writer emits them as character references under <c>CheckCharacters = false</c> and its post-filter lets
	/// those through (they are plain ASCII once escaped), producing a wire that no conformant reader can parse. Set
	/// <see cref="StrictControlCharacters"/> to reproduce that defect exactly.</item>
	/// <item>Unpaired surrogate halves are dropped; a valid surrogate pair passes through whole (and becomes a single
	/// 4-byte sequence on the UTF-8 core, never CESU-8).</item>
	/// <item>U+FFFE and U+FFFF are dropped, like the reference filter does.</item>
	/// <item>An element with no content self-closes as <c>&lt;Name /&gt;</c>, with a space before the slash; writing an
	/// <b>empty</b> string as content forces the expanded form <c>&lt;Name&gt;&lt;/Name&gt;</c>.</item>
	/// </list>
	/// <para><b>Always pass this struct by ref, and abandon the writer variable you constructed it from.</b> The emitter holds
	/// both the element state and the destination writer <i>inline</i>, so a copy of the emitter silently loses every write
	/// made through it.</para>
	/// <para>The constructor <b>copies</b> the writer into <see cref="Writer"/>. From that moment there are two live writer
	/// structs sharing one underlying buffer, and only the emitter's copy tracks the real position. The caller's variable is
	/// not merely stale: with a pooled or rented sink (for instance <c>SliceWriter</c>) it is a second claim on the same
	/// buffer, so disposing, resetting or reusing it is double ownership and, after the emitter's buffer has grown or been
	/// returned, a use-after-return. Treat the variable as consumed by the constructor:</para>
	/// <code>
	/// var sink = new ValueStringWriter();
	/// var emitter = new CrystalXmlWriter&lt;char, ValueStringWriter&gt;(ref sink);
	/// Emit(ref emitter);                       // always by ref
	/// string xml = emitter.Writer.ToString();  // read back HERE, never from `sink`
	/// </code>
	/// <para>Everything after construction, including reading the output and disposing the sink, goes through
	/// <see cref="Writer"/> on the very instance that was written to.</para>
	/// <para>The <typeparamref name="TRune"/> divergence is concentrated in a handful of leaf primitives (ASCII literal,
	/// escaped text, precomputed name, character reference). Everything above them, the element stack, the self-closing
	/// decision and the escaping tables, is written once. The <c>typeof(TRune) == typeof(char)</c> tests are folded away by
	/// the JIT when the generic is instantiated, so neither core pays for the other.</para>
	/// </remarks>
	[PublicAPI]
	[DebuggerDisplay("Depth={Depth}, TagPending={TagPending}")]
	public struct CrystalXmlWriter<TRune, TWriter> : IXmlEmitter
		where TRune : unmanaged
		where TWriter : struct, IBufferWriter<TRune>
	{

		/// <summary>Largest number of UTF-16 code units transcoded to UTF-8 in a single buffer request</summary>
		/// <remarks>Bounds the size of the span asked from the writer for an arbitrarily long value, at 3 bytes per code
		/// unit (the worst case for the BMP; a surrogate pair is 4 bytes for 2 units, so it stays under the bound).</remarks>
		private const int Utf8ChunkSize = 512;

		/// <summary>Destination writer, held inline; the only live view of the output</summary>
		/// <remarks>Public because this is where the caller reads the accumulated output back, and where the sink must be
		/// disposed if it owns pooled memory. The writer variable passed to the constructor is a dead copy from that point
		/// on: see the remarks on <see cref="CrystalXmlWriter{TRune,TWriter}"/>.</remarks>
		public TWriter Writer;

		/// <summary>When <see langword="true"/>, C0 control characters are emitted as character references instead of being dropped</summary>
		/// <remarks>Reproduces a defect of the legacy wire, and produces XML that no conformant reader accepts. Only the
		/// certification harness, which compares against captured legacy output, should turn this on.</remarks>
		public readonly bool StrictControlCharacters;

		/// <summary>Number of elements currently open</summary>
		public int Depth { get; private set; }

		/// <summary>True when the current start tag is still open, so attributes may still be written</summary>
		private bool TagPending;

		/// <summary>True when the current element has received content (text, raw content, or a child element)</summary>
		private bool HasContent;

		/// <summary>Constructs an emitter writing into <paramref name="writer"/></summary>
		/// <param name="writer">Destination buffer writer, <b>consumed</b> by this constructor. It is copied into
		/// <see cref="Writer"/>, and the caller's variable must not be read, disposed or reused afterwards: it becomes a
		/// second, position-less claim on the same buffer. Do everything through <see cref="Writer"/> instead.
		/// <para>The <c>ref</c> here only avoids copying a potentially large writer struct as an <i>argument</i>; the field
		/// assignment copies it regardless, and no aliasing is established (a <c>ref</c> field is impossible on the
		/// <c>netstandard2.0</c> and <c>net8.0</c> targets). It is a signal that the writer is being taken over, not a
		/// mechanism that keeps the caller's variable in sync.</para></param>
		/// <param name="strictControlCharacters">When <see langword="true"/>, reproduce the legacy character-reference
		/// treatment of C0 control characters instead of dropping them. See <see cref="StrictControlCharacters"/>.</param>
		/// <exception cref="NotSupportedException">If <typeparamref name="TRune"/> is neither <see cref="char"/> nor <see cref="byte"/>.</exception>
		public CrystalXmlWriter(ref TWriter writer, bool strictControlCharacters = false)
		{
			if (typeof(TRune) != typeof(char) && typeof(TRune) != typeof(byte))
			{
				throw ErrorUnsupportedRune();
			}

			this.Writer = writer;
			this.StrictControlCharacters = strictControlCharacters;
			this.Depth = 0;
			this.TagPending = false;
			this.HasContent = false;
		}

		private static NotSupportedException ErrorUnsupportedRune()
			=> new($"{nameof(CrystalXmlWriter<TRune, TWriter>)} only supports 'char' (UTF-16) or 'byte' (UTF-8) as its output unit, but was instantiated with '{typeof(TRune).Name}'.");

		#region Events...

		/// <inheritdoc />
		public void WriteStartElement(in XmlName name)
		{
			CloseTagIfPending();
			WriteAscii("<");
			WriteName(in name);
			++this.Depth;
			this.TagPending = true;
			this.HasContent = false;
		}

		/// <inheritdoc />
		public void WriteAttribute(in XmlName name, ReadOnlySpan<char> value)
		{
			Contract.Debug.Requires(this.TagPending, "Attributes can only be written while the start tag is still open");
			WriteAscii(" ");
			WriteName(in name);
			WriteAscii("=\"");
			WriteEscaped(value, inAttribute: true);
			WriteAscii("\"");
		}

		/// <inheritdoc />
		public void WriteText(ReadOnlySpan<char> text)
		{
			CloseTagIfPending();
			// even an empty span counts: it is what forces the expanded <Name></Name> form
			this.HasContent = true;
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
			Contract.Debug.Requires(XmlCharHelpers.IsAscii(ascii), "Raw content must be pre-validated ASCII");
			CloseTagIfPending();
			this.HasContent = true;
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
		public void WriteEndElement(in XmlName name)
		{
			Contract.Debug.Requires(this.Depth > 0, "There is no open element to close");
			--this.Depth;

			if (this.TagPending && !this.HasContent)
			{
				// no content at all: self-closing form, including the space the reference writer emits
				WriteAscii(" />");
				this.TagPending = false;
			}
			else
			{
				CloseTagIfPending();
				WriteAscii("</");
				WriteName(in name);
				WriteAscii(">");
			}

			// whatever happens, the parent element now has content: this child
			this.HasContent = true;
		}

		private void CloseTagIfPending()
		{
			if (this.TagPending)
			{
				WriteAscii(">");
				this.TagPending = false;
			}
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
						// NewLineHandling.Replace of the reference writer, measured against a live DCS: in TEXT, every line
						// ending (\r\n, a lone \r, a lone \n) becomes a RAW \r\n, never a character reference.
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
		private void WriteName(in XmlName name)
		{
			// a default(XmlName) would emit `<>` on the char core and `<>` on the byte core: malformed either way, and a bug
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
