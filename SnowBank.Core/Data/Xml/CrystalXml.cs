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
	using System.Xml;
	using System.Xml.Linq;
	using SnowBank.Buffers;
	using SnowBank.Buffers.Text;
	using SnowBank.Data.Json;

	/// <summary>Sink plumbing for <see cref="ICrystalXmlSerializer{T}"/>: the eight output entry points</summary>
	/// <remarks>
	/// <para>Every method here owns the whole sink lifecycle: it constructs the destination buffer, constructs the emitter
	/// over it, calls <see cref="ICrystalXmlSerializer{T}.WriteXml{TEmitter}"/>, and reads the result back through the
	/// emitter's own <c>Writer</c>, never through the caller's writer variable (the emitter copies the writer struct by
	/// value: see the remarks on <see cref="CrystalXmlWriter{TRune,TWriter}"/>). Keeping that dance inside these helpers
	/// means nobody outside this file has to know the rule exists.</para>
	/// <para><see cref="ToText{T}"/> and <see cref="ToSlice{T}"/>/<see cref="ToBytes{T}"/> go through the byte-exact
	/// <see cref="CrystalXmlWriter{TRune,TWriter}"/>. <see cref="ToXDocument{T}"/> and the <see cref="XmlWriter"/> overload of
	/// <see cref="WriteTo{T}(XmlWriter,ICrystalXmlSerializer{T},T,CrystalJsonSettings?,string?)"/> go through the infoset
	/// emitters instead, and only guarantee infoset equivalence, not a byte-exact output.</para>
	/// </remarks>
	[PublicAPI]
	public static class CrystalXml
	{

		/// <summary>Maximum nesting depth a generated XML emission may reach before it raises <see cref="CrystalXmlCycleException"/></summary>
		/// <remarks>
		/// <para>Locked to <see cref="CrystalJsonWriter.MaxDepth"/>, which documents the depth/cycle guard shared by both
		/// formats: a document must not serialize on one and be refused by the other over where "too deep" starts.</para>
		/// <para>The counter only tracks generated recursion: it resets to zero across a call into <see cref="ICrystalXmlSerializer{T}.WriteXml{TEmitter}"/>
		/// (a custom member converter) or <see cref="ICrystalXmlSerializable.WriteXml{TEmitter}"/> (a self-writing type), so a
		/// cycle running through such a hook is not covered and still overflows the native stack. The measured overflow point
		/// of the generated recursion is around 2300 nested <c>WriteXmlElement</c> frames (one frame per level).</para>
		/// </remarks>
		public const int MaxDepth = CrystalJsonWriter.MaxDepth;

		#region Byte-exact format (CrystalXmlWriter)...

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
				// the serializer threw before ToStringAndDispose(): return the pooled buffer instead of leaking it
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
		/// <param name="destination">Destination stream; ownership stays with the caller, who is responsible for flushing and disposing it</param>
		/// <param name="serializer">Serializer that knows how to write a <typeparamref name="T"/></param>
		/// <param name="value">Value to serialize, or <see langword="null"/> to write the empty/self-closing root element</param>
		/// <param name="settings">Optional settings passed through to <paramref name="serializer"/></param>
		/// <param name="rootName">Optional override for the name of the root element</param>
		/// <remarks>
		/// <para>Writes synchronously through a pooled <see cref="byte"/> buffer (<see cref="StreamBufferProxy"/>), draining
		/// to <paramref name="destination"/> via <see cref="Stream.Write(byte[],int,int)"/> whenever the buffer would need to
		/// grow. Deliberately not a <see cref="System.IO.Pipelines.PipeWriter"/>: this method is synchronous, and blocking on
		/// <c>StreamPipeWriter.FlushAsync()</c> is not a supported use of the <see cref="System.Threading.Tasks.Sources.IValueTaskSource{TResult}"/>-backed
		/// <see cref="ValueTask{TResult}"/> a <see cref="FileStream"/> or <see cref="System.Net.Sockets.NetworkStream"/> returns.</para>
		/// <para><b>Failure-path contract.</b> If <paramref name="serializer"/> throws, the pooled buffer is always returned to
		/// <see cref="ArrayPool{Byte}.Shared"/>, the still-buffered tail is <b>not</b> written to <paramref name="destination"/>
		/// (chunks already pushed out by an earlier growth-triggered drain stay written), and the caller always sees the
		/// original exception, never a cleanup failure. This method never closes, disposes, or otherwise completes
		/// <paramref name="destination"/>: ownership stays with the caller for the whole call.</para>
		/// </remarks>
		public static void WriteTo<T>(Stream destination, ICrystalXmlSerializer<T> serializer, T? value, CrystalJsonSettings? settings = null, string? rootName = null)
		{
			Contract.NotNull(destination);
			Contract.NotNull(serializer);
			var sink = new StreamBufferProxy(destination);
			var emitter = new CrystalXmlWriter<byte, StreamBufferProxy>(ref sink);
			try
			{
				serializer.WriteXml(ref emitter, value, settings, rootName);
				emitter.Writer.Drain();
			}
			catch
			{
				emitter.Writer.Abandon();
				throw;
			}
		}

		/// <summary>Serializes <paramref name="value"/> as XML text into <paramref name="destination"/></summary>
		/// <typeparam name="T">Type of the value being serialized</typeparam>
		/// <param name="destination">Destination writer; ownership stays with the caller, who is responsible for flushing and disposing it</param>
		/// <param name="serializer">Serializer that knows how to write a <typeparamref name="T"/></param>
		/// <param name="value">Value to serialize, or <see langword="null"/> to write the empty/self-closing root element</param>
		/// <param name="settings">Optional settings passed through to <paramref name="serializer"/></param>
		/// <param name="rootName">Optional override for the name of the root element</param>
		/// <remarks>
		/// <para>Writes through a pooled <see cref="char"/> buffer (<see cref="TextWriterBufferProxy"/>), draining to
		/// <paramref name="destination"/> via <see cref="TextWriter.Write(char[],int,int)"/> whenever the buffer would need
		/// to grow.</para>
		/// <para>Same failure-path contract as <see cref="WriteTo{T}(Stream,ICrystalXmlSerializer{T},T,CrystalJsonSettings?,string?)"/>:
		/// on failure the pooled buffer is always returned, the un-drained tail is not written, and the caller sees the
		/// original exception; either way <paramref name="destination"/> is never closed or disposed.</para>
		/// </remarks>
		public static void WriteTo<T>(TextWriter destination, ICrystalXmlSerializer<T> serializer, T? value, CrystalJsonSettings? settings = null, string? rootName = null)
		{
			Contract.NotNull(destination);
			Contract.NotNull(serializer);
			var sink = new TextWriterBufferProxy(destination);
			var emitter = new CrystalXmlWriter<char, TextWriterBufferProxy>(ref sink);
			try
			{
				serializer.WriteXml(ref emitter, value, settings, rootName);
				emitter.Writer.Drain();
			}
			catch
			{
				emitter.Writer.Abandon();
				throw;
			}
		}

		/// <summary>Serializes <paramref name="value"/> as UTF-8 encoded XML into <paramref name="destination"/></summary>
		/// <typeparam name="T">Type of the value being serialized</typeparam>
		/// <param name="destination">Destination buffer writer</param>
		/// <param name="serializer">Serializer that knows how to write a <typeparamref name="T"/></param>
		/// <param name="value">Value to serialize, or <see langword="null"/> to write the empty/self-closing root element</param>
		/// <param name="settings">Optional settings passed through to <paramref name="serializer"/></param>
		/// <param name="rootName">Optional override for the name of the root element</param>
		/// <remarks><paramref name="destination"/> is an interface, so it cannot itself satisfy the <c>struct</c> constraint
		/// on <c>TWriter</c>; see the remarks on <see cref="BufferWriterProxy{TRune}"/>. This overload owns no pooled resource
		/// of its own, so on failure whatever <paramref name="serializer"/> already wrote through
		/// <paramref name="destination"/> simply stays there.</remarks>
		public static void WriteTo<T>(IBufferWriter<byte> destination, ICrystalXmlSerializer<T> serializer, T? value, CrystalJsonSettings? settings = null, string? rootName = null)
		{
			Contract.NotNull(destination);
			Contract.NotNull(serializer);
			var sink = new BufferWriterProxy<byte>(destination);
			var emitter = new CrystalXmlWriter<byte, BufferWriterProxy<byte>>(ref sink);
			serializer.WriteXml(ref emitter, value, settings, rootName);
		}

		#endregion

		#region Infoset (CrystalXDocumentEmitter / CrystalXmlWriterEmitter)...

		/// <summary>Serializes <paramref name="value"/> to an in-memory <see cref="XDocument"/></summary>
		/// <typeparam name="T">Type of the value being serialized</typeparam>
		/// <param name="serializer">Serializer that knows how to write a <typeparamref name="T"/></param>
		/// <param name="value">Value to serialize, or <see langword="null"/> to write the empty root element</param>
		/// <param name="settings">Optional settings passed through to <paramref name="serializer"/></param>
		/// <param name="rootName">Optional override for the name of the root element</param>
		/// <remarks>Only infoset equivalence with the byte-exact output is guaranteed here, not a byte-exact document: see
		/// the remarks on <see cref="CrystalXDocumentEmitter"/>.</remarks>
		public static XDocument ToXDocument<T>(ICrystalXmlSerializer<T> serializer, T? value, CrystalJsonSettings? settings = null, string? rootName = null)
		{
			Contract.NotNull(serializer);
			var emitter = new CrystalXDocumentEmitter();
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
		/// <remarks>Only infoset equivalence with the byte-exact output is guaranteed here: the concrete bytes depend on
		/// how <paramref name="destination"/> was configured. See the remarks on <see cref="CrystalXmlWriterEmitter"/>.</remarks>
		public static void WriteTo<T>(XmlWriter destination, ICrystalXmlSerializer<T> serializer, T? value, CrystalJsonSettings? settings = null, string? rootName = null)
		{
			Contract.NotNull(destination);
			Contract.NotNull(serializer);
			var emitter = new CrystalXmlWriterEmitter(destination);
			serializer.WriteXml(ref emitter, value, settings, rootName);
		}

		#endregion

		#region Sink adapters...

		/// <summary>Struct wrapper that lets a reference-typed <see cref="IBufferWriter{T}"/> satisfy the <c>struct</c> constraint on <see cref="CrystalXmlWriter{TRune,TWriter}"/>'s <c>TWriter</c></summary>
		/// <remarks>Every field here is a reference to the caller's own buffer writer, so the by-value copy the emitter's
		/// constructor makes still forwards to the same instance: unlike the pooled sinks, this proxy has no state of its own
		/// to lose and no resource to manage on either path.</remarks>
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

		/// <summary>Struct <see cref="IBufferWriter{Byte}"/> that buffers into a pooled <see cref="byte"/> array and drains it to a <see cref="Stream"/> synchronously</summary>
		/// <remarks>
		/// <para><see cref="Stream"/> is a push-based sink, not an <see cref="IBufferWriter{T}"/>: writes accumulate in a
		/// rented array that is drained synchronously whenever it would need to grow, and once more at the end via
		/// <see cref="Drain"/> (success) or <see cref="Abandon"/> (failure); the exact contract of each is on
		/// <see cref="CrystalXml.WriteTo{T}(Stream,ICrystalXmlSerializer{T},T,CrystalJsonSettings?,string?)"/>.</para>
		/// <para><b>Single-owner discipline.</b> <see cref="Buffer"/> is returned to <see cref="ArrayPool{Byte}.Shared"/> in
		/// exactly one place each (<see cref="Drain"/> and <see cref="Abandon"/>), and both null the field before writing
		/// anything, so no code path can return the same array twice.</para>
		/// </remarks>
		private struct StreamBufferProxy : IBufferWriter<byte>
		{

			private const int DefaultBufferSize = 4096;

			private readonly Stream Writer;

			private byte[]? Buffer;

			private int Count;

			public StreamBufferProxy(Stream stream)
			{
				Contract.NotNull(stream);
				this.Writer = stream;
				this.Buffer = null;
				this.Count = 0;
			}

			public void Advance(int count)
			{
				Contract.Debug.Requires(count >= 0 && this.Buffer is not null && this.Count + count <= this.Buffer.Length);
				this.Count += count;
			}

			public Memory<byte> GetMemory(int sizeHint = 0)
			{
				EnsureCapacity(sizeHint);
				return this.Buffer.AsMemory(this.Count);
			}

			public Span<byte> GetSpan(int sizeHint = 0)
			{
				EnsureCapacity(sizeHint);
				return this.Buffer.AsSpan(this.Count);
			}

			private void EnsureCapacity(int sizeHint)
			{
				int needed = Math.Max(sizeHint, 1);
				var buffer = this.Buffer;
				if (buffer is not null && buffer.Length - this.Count >= needed)
				{
					return;
				}

				// no room left (or nothing rented yet): Drain() writes the pending bytes and returns the current
				// array exactly once (a no-op if Buffer is already null), then a fresh one is rented for this request
				Drain();
				this.Buffer = ArrayPool<byte>.Shared.Rent(Math.Max(needed, DefaultBufferSize));
			}

			/// <summary>Writes any buffered bytes to the wrapped <see cref="Stream"/> and returns the buffer to the pool: the success-path cleanup, also used by mid-document growth spills</summary>
			/// <remarks>Nulls <see cref="Buffer"/> before the write, so the array goes back to the pool exactly once even if
			/// <see cref="Stream.Write(byte[],int,int)"/> itself throws.</remarks>
			public void Drain()
			{
				var buffer = this.Buffer;
				if (buffer is null)
				{
					return;
				}
				this.Buffer = null;
				try
				{
					if (this.Count > 0)
					{
						this.Writer.Write(buffer, 0, this.Count);
					}
				}
				finally
				{
					this.Count = 0;
					ArrayPool<byte>.Shared.Return(buffer);
				}
			}

			/// <summary>Returns the buffer to the pool WITHOUT writing the still-pending tail: the failure-path cleanup</summary>
			/// <remarks>Deliberately writes nothing: the failure-path contract on <see cref="CrystalXml.WriteTo{T}(Stream,ICrystalXmlSerializer{T},T,CrystalJsonSettings?,string?)"/>
			/// forbids writing content after the serializer has thrown.</remarks>
			public void Abandon()
			{
				var buffer = this.Buffer;
				this.Buffer = null;
				this.Count = 0;
				if (buffer is not null)
				{
					ArrayPool<byte>.Shared.Return(buffer);
				}
			}

		}

		/// <summary>Struct <see cref="IBufferWriter{Char}"/> that buffers into a pooled <see cref="char"/> array and drains it to a <see cref="TextWriter"/></summary>
		/// <remarks>The <see cref="char"/> twin of <see cref="StreamBufferProxy"/>: same buffering and same single-owner
		/// discipline (<see cref="Buffer"/> is returned to <see cref="ArrayPool{Char}.Shared"/> in exactly one place each in
		/// <see cref="Drain"/> and <see cref="Abandon"/>, both nulling the field first), draining to a <see cref="TextWriter"/>
		/// instead of a <see cref="Stream"/>. The exact contract is on
		/// <see cref="CrystalXml.WriteTo{T}(TextWriter,ICrystalXmlSerializer{T},T,CrystalJsonSettings?,string?)"/>.</remarks>
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
				var buffer = this.Buffer;
				if (buffer is not null && buffer.Length - this.Count >= needed)
				{
					return;
				}

				// no room left (or nothing rented yet): Drain() writes the pending characters and returns the current
				// array exactly once (a no-op if Buffer is already null), then a fresh one is rented for this request
				Drain();
				this.Buffer = ArrayPool<char>.Shared.Rent(Math.Max(needed, DefaultBufferSize));
			}

			/// <summary>Writes any buffered characters to the wrapped <see cref="TextWriter"/> and returns the buffer to the pool: the success-path cleanup, also used by mid-document growth spills</summary>
			/// <remarks>Nulls <see cref="Buffer"/> before the write, so the array goes back to the pool exactly once even if
			/// <see cref="TextWriter.Write(char[],int,int)"/> itself throws.</remarks>
			public void Drain()
			{
				var buffer = this.Buffer;
				if (buffer is null)
				{
					return;
				}
				this.Buffer = null;
				try
				{
					if (this.Count > 0)
					{
						this.Writer.Write(buffer, 0, this.Count);
					}
				}
				finally
				{
					this.Count = 0;
					ArrayPool<char>.Shared.Return(buffer);
				}
			}

			/// <summary>Returns the buffer to the pool WITHOUT writing the still-pending tail: the failure-path cleanup</summary>
			/// <remarks>Deliberately writes nothing: the failure-path contract on <see cref="CrystalXml.WriteTo{T}(TextWriter,ICrystalXmlSerializer{T},T,CrystalJsonSettings?,string?)"/>
			/// forbids writing content after the serializer has thrown.</remarks>
			public void Abandon()
			{
				var buffer = this.Buffer;
				this.Buffer = null;
				this.Count = 0;
				if (buffer is not null)
				{
					ArrayPool<char>.Shared.Return(buffer);
				}
			}

		}

		#endregion

	}

}
