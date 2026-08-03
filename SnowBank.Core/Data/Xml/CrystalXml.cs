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
	using System.IO.Pipelines;
	using System.Xml;
	using System.Xml.Linq;
	using SnowBank.Buffers;
	using SnowBank.Buffers.Text;
	using SnowBank.Data.Json;

	/// <summary>Sink plumbing for <see cref="ICrystalXmlSerializer{T}"/>: the five output entry points</summary>
	/// <remarks>
	/// <para>Every method here owns the whole sink lifecycle: it constructs the destination buffer (or reuses the one the
	/// caller handed it), constructs the emitter over it, calls <see cref="ICrystalXmlSerializer{T}.WriteXml{TEmitter}"/>,
	/// and reads the result back through the emitter's own <c>Writer</c> - never through a caller-visible writer variable.
	/// This is a direct consequence of how <see cref="CrystalXmlWriter{TRune,TWriter}"/> holds its destination: the
	/// constructor copies the writer struct by value (a <c>ref</c> field is not available on every target framework this
	/// library ships for), so a variable passed to it is abandoned from that point on and must never be read, disposed or
	/// reused by the caller. Keeping that entire dance inside these static helpers means nobody outside this file has to
	/// know the rule exists.</para>
	/// <para><see cref="ToText{T}"/> and <see cref="ToSlice{T}"/>/<see cref="ToBytes{T}"/> go through the byte-exact
	/// <see cref="CrystalXmlWriter{TRune,TWriter}"/>, over a pooled <see cref="ValueStringWriter"/> or <see cref="SliceWriter"/>
	/// respectively. <see cref="ToXDocument{T}"/> and the <see cref="XmlWriter"/> overload of <see cref="WriteTo{T}(XmlWriter,ICrystalXmlSerializer{T},T,CrystalJsonSettings?,string?)"/>
	/// go through the two infoset emitters from <c>Task 3</c> instead, and only guarantee infoset equivalence, not a
	/// byte-exact wire.</para>
	/// </remarks>
	[PublicAPI]
	public static class CrystalXml
	{

		#region Byte-exact wire (CrystalXmlWriter)...

		/// <summary>Serializes <paramref name="value"/> to a <see cref="string"/> of XML text</summary>
		/// <typeparam name="T">Type of the value being serialized</typeparam>
		/// <param name="serializer">Serializer that knows how to write a <typeparamref name="T"/></param>
		/// <param name="value">Value to serialize, or <see langword="null"/> to write the empty/self-closing root element</param>
		/// <param name="settings">Optional settings passed through to <paramref name="serializer"/></param>
		/// <param name="rootName">Optional override for the name of the root element</param>
		public static string ToText<T>(ICrystalXmlSerializer<T> serializer, T? value, CrystalJsonSettings? settings = null, string? rootName = null)
		{
			Contract.NotNull(serializer);
			var sink = new ValueStringWriter();
			var emitter = new CrystalXmlWriter<char, ValueStringWriter>(ref sink);
			try
			{
				serializer.WriteXml(ref emitter, value, settings, rootName);
				return emitter.Writer.ToStringAndDispose();
			}
			catch
			{
				// the pooled buffer behind ValueStringWriter is only returned by ToStringAndDispose() above; if the
				// serializer throws before reaching it, dispose here instead so the rented array still goes back to
				// the shared pool rather than merely becoming garbage
				emitter.Writer.Dispose();
				throw;
			}
		}

		/// <summary>Serializes <paramref name="value"/> to a UTF-8 encoded <see cref="Slice"/></summary>
		/// <typeparam name="T">Type of the value being serialized</typeparam>
		/// <param name="serializer">Serializer that knows how to write a <typeparamref name="T"/></param>
		/// <param name="value">Value to serialize, or <see langword="null"/> to write the empty/self-closing root element</param>
		/// <param name="settings">Optional settings passed through to <paramref name="serializer"/></param>
		/// <param name="rootName">Optional override for the name of the root element</param>
		public static Slice ToSlice<T>(ICrystalXmlSerializer<T> serializer, T? value, CrystalJsonSettings? settings = null, string? rootName = null)
		{
			Contract.NotNull(serializer);
			var sink = new SliceWriter();
			var emitter = new CrystalXmlWriter<byte, SliceWriter>(ref sink);
			serializer.WriteXml(ref emitter, value, settings, rootName);
			return emitter.Writer.ToSlice();
		}

		/// <summary>Serializes <paramref name="value"/> to a UTF-8 encoded <see cref="byte"/> array</summary>
		/// <typeparam name="T">Type of the value being serialized</typeparam>
		/// <param name="serializer">Serializer that knows how to write a <typeparamref name="T"/></param>
		/// <param name="value">Value to serialize, or <see langword="null"/> to write the empty/self-closing root element</param>
		/// <param name="settings">Optional settings passed through to <paramref name="serializer"/></param>
		/// <param name="rootName">Optional override for the name of the root element</param>
		public static byte[] ToBytes<T>(ICrystalXmlSerializer<T> serializer, T? value, CrystalJsonSettings? settings = null, string? rootName = null)
			=> ToSlice(serializer, value, settings, rootName).ToArray();

		/// <summary>Serializes <paramref name="value"/> as UTF-8 encoded XML into <paramref name="destination"/></summary>
		/// <typeparam name="T">Type of the value being serialized</typeparam>
		/// <param name="destination">Destination stream, flushed but not disposed by this call; ownership stays with the caller</param>
		/// <param name="serializer">Serializer that knows how to write a <typeparamref name="T"/></param>
		/// <param name="value">Value to serialize, or <see langword="null"/> to write the empty/self-closing root element</param>
		/// <param name="settings">Optional settings passed through to <paramref name="serializer"/></param>
		/// <param name="rootName">Optional override for the name of the root element</param>
		/// <remarks>Writes through a <see cref="PipeWriter"/> created directly over <paramref name="destination"/>
		/// (<c>System.IO.Pipelines</c> is already a dependency of this assembly, for the unrelated Slice pipe helpers).
		/// A <see cref="PipeWriter"/> is a reference type, so it cannot itself satisfy the <c>struct</c> constraint on
		/// <see cref="CrystalXmlWriter{TRune,TWriter}"/>'s <c>TWriter</c>; <see cref="BufferWriterProxy{TRune}"/> is the
		/// thin struct wrapper that lets it through.</remarks>
		public static void WriteTo<T>(Stream destination, ICrystalXmlSerializer<T> serializer, T? value, CrystalJsonSettings? settings = null, string? rootName = null)
		{
			Contract.NotNull(destination);
			Contract.NotNull(serializer);
			var pipeWriter = PipeWriter.Create(destination, new StreamPipeWriterOptions(leaveOpen: true));
			try
			{
				var sink = new BufferWriterProxy<byte>(pipeWriter);
				var emitter = new CrystalXmlWriter<byte, BufferWriterProxy<byte>>(ref sink);
				serializer.WriteXml(ref emitter, value, settings, rootName);
				pipeWriter.FlushAsync().GetAwaiter().GetResult();
			}
			finally
			{
				// releases the PipeWriter's own pooled segments; harmless to call after a successful flush, and
				// leaveOpen: true above means this never touches the caller's destination stream
				pipeWriter.Complete();
			}
		}

		/// <summary>Serializes <paramref name="value"/> as XML text into <paramref name="destination"/></summary>
		/// <typeparam name="T">Type of the value being serialized</typeparam>
		/// <param name="destination">Destination writer, flushed but not disposed by this call; ownership stays with the caller</param>
		/// <param name="serializer">Serializer that knows how to write a <typeparamref name="T"/></param>
		/// <param name="value">Value to serialize, or <see langword="null"/> to write the empty/self-closing root element</param>
		/// <param name="settings">Optional settings passed through to <paramref name="serializer"/></param>
		/// <param name="rootName">Optional override for the name of the root element</param>
		public static void WriteTo<T>(TextWriter destination, ICrystalXmlSerializer<T> serializer, T? value, CrystalJsonSettings? settings = null, string? rootName = null)
		{
			Contract.NotNull(destination);
			Contract.NotNull(serializer);
			var sink = new TextWriterBufferProxy(destination);
			var emitter = new CrystalXmlWriter<char, TextWriterBufferProxy>(ref sink);
			try
			{
				serializer.WriteXml(ref emitter, value, settings, rootName);
			}
			finally
			{
				// flushes whatever was buffered so far (even a partial document, on the exception path) and always
				// returns the rented array to the pool
				emitter.Writer.Flush();
			}
		}

		/// <summary>Serializes <paramref name="value"/> as UTF-8 encoded XML into <paramref name="destination"/></summary>
		/// <typeparam name="T">Type of the value being serialized</typeparam>
		/// <param name="destination">Destination buffer writer</param>
		/// <param name="serializer">Serializer that knows how to write a <typeparamref name="T"/></param>
		/// <param name="value">Value to serialize, or <see langword="null"/> to write the empty/self-closing root element</param>
		/// <param name="settings">Optional settings passed through to <paramref name="serializer"/></param>
		/// <param name="rootName">Optional override for the name of the root element</param>
		/// <remarks><paramref name="destination"/> is an interface, so its concrete type is not known at compile time and
		/// cannot itself satisfy the <c>struct</c> constraint on <c>TWriter</c>; see the remarks on <see cref="BufferWriterProxy{TRune}"/>.</remarks>
		public static void WriteTo<T>(IBufferWriter<byte> destination, ICrystalXmlSerializer<T> serializer, T? value, CrystalJsonSettings? settings = null, string? rootName = null)
		{
			Contract.NotNull(destination);
			Contract.NotNull(serializer);
			var sink = new BufferWriterProxy<byte>(destination);
			var emitter = new CrystalXmlWriter<byte, BufferWriterProxy<byte>>(ref sink);
			serializer.WriteXml(ref emitter, value, settings, rootName);
		}

		#endregion

		#region Infoset (XDocumentEmitter / XmlWriterEmitter)...

		/// <summary>Serializes <paramref name="value"/> to an in-memory <see cref="XDocument"/></summary>
		/// <typeparam name="T">Type of the value being serialized</typeparam>
		/// <param name="serializer">Serializer that knows how to write a <typeparamref name="T"/></param>
		/// <param name="value">Value to serialize, or <see langword="null"/> to write the empty root element</param>
		/// <param name="settings">Optional settings passed through to <paramref name="serializer"/></param>
		/// <param name="rootName">Optional override for the name of the root element</param>
		/// <remarks>Only infoset equivalence with the byte-exact wire is guaranteed here, not a byte-exact document: see
		/// the remarks on <see cref="XDocumentEmitter"/>.</remarks>
		public static XDocument ToXDocument<T>(ICrystalXmlSerializer<T> serializer, T? value, CrystalJsonSettings? settings = null, string? rootName = null)
		{
			Contract.NotNull(serializer);
			var emitter = new XDocumentEmitter();
			serializer.WriteXml(ref emitter, value, settings, rootName);
			return emitter.ToDocument();
		}

		/// <summary>Serializes <paramref name="value"/> into <paramref name="destination"/></summary>
		/// <typeparam name="T">Type of the value being serialized</typeparam>
		/// <param name="destination">Destination writer, not owned: the caller flushes and disposes it, and configures its <see cref="XmlWriterSettings"/></param>
		/// <param name="serializer">Serializer that knows how to write a <typeparamref name="T"/></param>
		/// <param name="value">Value to serialize, or <see langword="null"/> to write the empty root element</param>
		/// <param name="settings">Optional settings passed through to <paramref name="serializer"/></param>
		/// <param name="rootName">Optional override for the name of the root element</param>
		/// <remarks>Only infoset equivalence with the byte-exact wire is guaranteed here: the concrete bytes depend on
		/// how <paramref name="destination"/> was configured. See the remarks on <see cref="XmlWriterEmitter"/>.</remarks>
		public static void WriteTo<T>(XmlWriter destination, ICrystalXmlSerializer<T> serializer, T? value, CrystalJsonSettings? settings = null, string? rootName = null)
		{
			Contract.NotNull(destination);
			Contract.NotNull(serializer);
			var emitter = new XmlWriterEmitter(destination);
			serializer.WriteXml(ref emitter, value, settings, rootName);
		}

		#endregion

		#region Sink adapters...

		/// <summary>Struct wrapper that lets a reference-typed <see cref="IBufferWriter{T}"/> (a <see cref="PipeWriter"/>, an <see cref="ArrayBufferWriter{T}"/>, ...) satisfy the <c>struct</c> constraint on <see cref="CrystalXmlWriter{TRune,TWriter}"/>'s <c>TWriter</c></summary>
		/// <remarks>
		/// <para>Every field here is a reference to the caller's own buffer writer, so a copy of this struct - which is
		/// exactly what <see cref="CrystalXmlWriter{TRune,TWriter}"/>'s constructor makes when it takes the writer by value -
		/// still forwards to the same underlying instance. Unlike <see cref="ValueStringWriter"/> or <see cref="SliceWriter"/>,
		/// which hold their buffer inline and therefore require the "read back through <c>emitter.Writer</c>, never through
		/// the abandoned caller variable" discipline, this proxy has no state of its own to lose: it is safe by construction,
		/// not merely by convention.</para>
		/// </remarks>
		private readonly struct BufferWriterProxy<TRune> : IBufferWriter<TRune>
			where TRune : unmanaged
		{

			private readonly IBufferWriter<TRune> Inner;

			public BufferWriterProxy(IBufferWriter<TRune> inner)
			{
				Contract.NotNull(inner);
				this.Inner = inner;
			}

			public void Advance(int count) => this.Inner.Advance(count);

			public Memory<TRune> GetMemory(int sizeHint = 0) => this.Inner.GetMemory(sizeHint);

			public Span<TRune> GetSpan(int sizeHint = 0) => this.Inner.GetSpan(sizeHint);

		}

		/// <summary>Struct <see cref="IBufferWriter{Char}"/> that buffers into a pooled <see cref="char"/> array and flushes it to a <see cref="TextWriter"/></summary>
		/// <remarks>
		/// <para><see cref="TextWriter"/> is a push-based sink (<see cref="TextWriter.Write(char[],int,int)"/>), not a
		/// <see cref="IBufferWriter{T}"/>, so there is no direct adapter the way there is for a <see cref="PipeWriter"/>
		/// (which already implements <see cref="IBufferWriter{Byte}"/>). This type buffers writes into a rented array and
		/// flushes whenever the array would need to grow, plus once more at the end via <see cref="Flush"/>, which the
		/// caller must call after the emitter has finished writing.</para>
		/// <para>The rented buffer and the running <see cref="Count"/> are fields on this struct, which lives, by value,
		/// inside <see cref="CrystalXmlWriter{TRune,TWriter}.Writer"/>: every mutation happens through that one instance
		/// (the emitter is always used by <see langword="ref"/>), so there is no aliasing hazard here, unlike the general
		/// by-value-writer caveat documented on <see cref="CrystalXmlWriter{TRune,TWriter}"/> itself.</para>
		/// </remarks>
		private struct TextWriterBufferProxy : IBufferWriter<char>
		{

			private const int DefaultBufferSize = 4096;

			private readonly TextWriter Writer;

			private char[]? Buffer;

			private int Count;

			public TextWriterBufferProxy(TextWriter writer)
			{
				Contract.NotNull(writer);
				this.Writer = writer;
				this.Buffer = null;
				this.Count = 0;
			}

			public void Advance(int count)
			{
				Contract.Debug.Requires(count >= 0 && this.Buffer is not null && this.Count + count <= this.Buffer.Length);
				this.Count += count;
			}

			public Memory<char> GetMemory(int sizeHint = 0)
			{
				EnsureCapacity(sizeHint);
				return this.Buffer.AsMemory(this.Count);
			}

			public Span<char> GetSpan(int sizeHint = 0)
			{
				EnsureCapacity(sizeHint);
				return this.Buffer.AsSpan(this.Count);
			}

			private void EnsureCapacity(int sizeHint)
			{
				int needed = Math.Max(sizeHint, 1);
				if (this.Buffer is null)
				{
					this.Buffer = ArrayPool<char>.Shared.Rent(Math.Max(needed, DefaultBufferSize));
					return;
				}

				if (this.Buffer.Length - this.Count >= needed)
				{
					return;
				}

				// no room left for the request: flush what has accumulated so far, and grow if even the whole
				// (now-empty) buffer would still be too small for this one request
				Flush();
				if (this.Buffer.Length < needed)
				{
					ArrayPool<char>.Shared.Return(this.Buffer);
					this.Buffer = ArrayPool<char>.Shared.Rent(Math.Max(needed, DefaultBufferSize));
				}
			}

			/// <summary>Writes any buffered characters to the wrapped <see cref="TextWriter"/> and returns the rented buffer to the pool</summary>
			/// <remarks>Must be called once, after the emitter has finished writing, to flush the final partial buffer; see the type remarks.</remarks>
			public void Flush()
			{
				if (this.Count > 0)
				{
					this.Writer.Write(this.Buffer!, 0, this.Count);
					this.Count = 0;
				}

				if (this.Buffer is not null)
				{
					ArrayPool<char>.Shared.Return(this.Buffer);
					this.Buffer = null;
				}
			}

		}

		#endregion

	}

}
