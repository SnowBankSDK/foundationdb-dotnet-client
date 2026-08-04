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

		/// <summary>Maximum nesting depth a generated XML emission may reach before it refuses to go deeper</summary>
		/// <remarks>
		/// <para>An object graph containing a reference cycle has no XML representation (neither profile has a
		/// <c>z:Id</c>/<c>z:Ref</c> form), so the generated emission must stop instead of recursing forever. It counts
		/// levels of generated recursion and raises <see cref="CrystalXmlCycleException"/> once this cap is reached: a
		/// typed, catchable error rather than a <see cref="StackOverflowException"/>, which .NET cannot catch and which
		/// takes the whole process down.</para>
		/// <para>The guard cannot distinguish a genuine reference cycle from an acyclic graph that is simply nested deeper
		/// than this cap: either shape hits the same counter and raises the same exception. A caller that knows its data
		/// has no cycles but legitimately needs more than 256 levels should flatten the graph instead.</para>
		/// <para>The counter only tracks generated recursion: it cannot cross a call into <see cref="ICrystalXmlSerializer{T}.WriteXml{TEmitter}"/>
		/// (a custom member converter) or <see cref="ICrystalXmlSerializable.WriteXml{TEmitter}"/> (a self-writing type),
		/// because it resets to zero on the other side of either call. A cycle that runs through one of those hooks is not
		/// covered by this guard and still overflows the native stack.</para>
		/// <para>The value is a deliberate compromise between "no legitimate document is this deep" and "the native stack
		/// is still nowhere near exhausted". The measured overflow point of the generated recursion is around 2300 nested
		/// <c>WriteXmlElement</c> frames (one frame per level); 256 leaves roughly a nine-fold margin, which still holds
		/// even if a future emission spends several frames per level, runs in a DEBUG build where nothing inlines, or runs
		/// on a thread with a smaller stack. Documents nested deeper than 256 elements are not a shape this serializer
		/// supports; a graph that legitimately needs it should be flattened instead.</para>
		/// </remarks>
		public const int MaxDepth = 256;

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
		/// <param name="destination">Destination stream; ownership stays with the caller, who is responsible for flushing and disposing it</param>
		/// <param name="serializer">Serializer that knows how to write a <typeparamref name="T"/></param>
		/// <param name="value">Value to serialize, or <see langword="null"/> to write the empty/self-closing root element</param>
		/// <param name="settings">Optional settings passed through to <paramref name="serializer"/></param>
		/// <param name="rootName">Optional override for the name of the root element</param>
		/// <remarks>
		/// <para>Writes synchronously through a pooled <see cref="byte"/> buffer (<see cref="StreamBufferProxy"/>), draining
		/// to <paramref name="destination"/> via the plain synchronous <see cref="Stream.Write(byte[],int,int)"/> whenever
		/// the buffer would need to grow. This deliberately does <b>not</b> go through a <see cref="System.IO.Pipelines.PipeWriter"/>:
		/// <c>StreamPipeWriter.FlushAsync()</c> returns a <see cref="ValueTask{TResult}"/> that is only guaranteed to have
		/// completed synchronously for a sink like <see cref="MemoryStream"/>; blocking on it via <c>.GetAwaiter().GetResult()</c>
		/// for a <see cref="FileStream"/> or a <see cref="System.Net.Sockets.NetworkStream"/> is not a supported use of an
		/// <see cref="System.Threading.Tasks.Sources.IValueTaskSource{TResult}"/>-backed <see cref="ValueTask{TResult}"/> and
		/// can throw or return early - a real risk this method's own signature is synchronous, so there is no way to await
		/// it correctly here.</para>
		/// <para><b>Failure-path contract.</b> If <paramref name="serializer"/> throws, cleanup (a) always returns the pooled
		/// buffer to <see cref="ArrayPool{Byte}.Shared"/>, (b) does <b>not</b> write the still-buffered tail to
		/// <paramref name="destination"/> - only whatever had already reached it through an earlier, growth-triggered drain,
		/// which is unavoidable once a large document has forced more than one chunk out the door, and this is by design,
		/// not a bug, and (c) never lets a cleanup failure replace the original exception: the caller always sees the
		/// original exception from <paramref name="serializer"/>. On success, the buffered tail is drained the same way the
		/// mid-document chunks were, and the buffer is returned identically. Either way, this method never closes, disposes,
		/// or otherwise completes <paramref name="destination"/>: ownership is entirely the caller's, for the whole call.</para>
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
		/// <para><b>Failure-path contract.</b> If <paramref name="serializer"/> throws, cleanup (a) always returns the pooled
		/// buffer to <see cref="ArrayPool{Char}.Shared"/>, (b) does <b>not</b> write the still-buffered tail to
		/// <paramref name="destination"/> - only whatever had already reached it through an earlier, growth-triggered drain,
		/// which is unavoidable once a large document has forced more than one chunk out the door, and this is by design,
		/// not a bug, and (c) never lets a cleanup failure replace the original exception: the caller always sees the
		/// original exception from <paramref name="serializer"/>. On success, the buffered tail is drained the same way the
		/// mid-document chunks were, and the buffer is returned identically. Either way, this method never closes, disposes,
		/// or otherwise completes <paramref name="destination"/>: ownership is entirely the caller's, for the whole call.</para>
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
		/// <remarks><paramref name="destination"/> is an interface, so its concrete type is not known at compile time and
		/// cannot itself satisfy the <c>struct</c> constraint on <c>TWriter</c>; see the remarks on <see cref="BufferWriterProxy{TRune}"/>.
		/// This overload owns no pooled resource of its own (the buffer is entirely <paramref name="destination"/>'s), so
		/// there is nothing to return on the failure path: whatever <paramref name="serializer"/> already wrote through
		/// <paramref name="destination"/> before throwing simply stays there, exactly as it would for any other
		/// <see cref="IBufferWriter{T}"/> consumer.</remarks>
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

		/// <summary>Struct wrapper that lets a reference-typed <see cref="IBufferWriter{T}"/> satisfy the <c>struct</c> constraint on <see cref="CrystalXmlWriter{TRune,TWriter}"/>'s <c>TWriter</c></summary>
		/// <remarks>
		/// <para>Every field here is a reference to the caller's own buffer writer, so a copy of this struct - which is
		/// exactly what <see cref="CrystalXmlWriter{TRune,TWriter}"/>'s constructor makes when it takes the writer by value -
		/// still forwards to the same underlying instance. Unlike <see cref="ValueStringWriter"/> or <see cref="SliceWriter"/>,
		/// which hold their buffer inline and therefore require the "read back through <c>emitter.Writer</c>, never through
		/// the abandoned caller variable" discipline, this proxy has no state of its own to lose: it is safe by construction,
		/// not merely by convention.</para>
		/// <para>Used only by the public <see cref="WriteTo{T}(IBufferWriter{byte},ICrystalXmlSerializer{T},T,CrystalJsonSettings?,string?)"/>
		/// overload: the caller supplies and owns the buffer writer, so there is no pooled resource of this type's own to
		/// manage on either the success or the failure path.</para>
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

		/// <summary>Struct <see cref="IBufferWriter{Byte}"/> that buffers into a pooled <see cref="byte"/> array and drains it to a <see cref="Stream"/> synchronously</summary>
		/// <remarks>
		/// <para><see cref="Stream"/> is a push-based sink (<see cref="Stream.Write(byte[],int,int)"/>), not an
		/// <see cref="IBufferWriter{T}"/>. This buffers writes into a rented array and drains it, synchronously, whenever the
		/// array would need to grow to fit the next request, and once more at the end via <see cref="Drain"/> (success) or
		/// <see cref="Abandon"/> (failure) - see <see cref="CrystalXml.WriteTo{T}(Stream,ICrystalXmlSerializer{T},T,CrystalJsonSettings?,string?)"/>'s
		/// remarks for the exact contract each of those two implements.</para>
		/// <para><b>Single-owner discipline.</b> <see cref="Buffer"/> is returned to <see cref="ArrayPool{Byte}.Shared"/> in
		/// exactly one place each for the "write it" and the "just give it back" cases (<see cref="Drain"/> and
		/// <see cref="Abandon"/> respectively), and both null the field as the very first step - before the array can
		/// possibly be written to the stream and before any exception from that write can be thrown - so no caller can ever
		/// observe a stale array, and no code path can return the same array twice.</para>
		/// <para>The rented buffer and the running <see cref="Count"/> are fields on this struct, which lives, by value,
		/// inside <see cref="CrystalXmlWriter{TRune,TWriter}.Writer"/>: every mutation happens through that one instance
		/// (the emitter is always used by <see langword="ref"/>), so there is no aliasing hazard here, unlike the general
		/// by-value-writer caveat documented on <see cref="CrystalXmlWriter{TRune,TWriter}"/> itself.</para>
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

				// not enough room left in the current buffer (or no buffer rented yet): Drain() writes whatever is
				// pending and returns the current array exactly once (a no-op if Buffer is already null), THEN a
				// fresh one is rented, sized for this request - no double-return path, because Drain() always owns
				// the transition from "have a buffer" to "have none" before renting the replacement
				Drain();
				this.Buffer = ArrayPool<byte>.Shared.Rent(Math.Max(needed, DefaultBufferSize));
			}

			/// <summary>Writes any buffered bytes to the wrapped <see cref="Stream"/> and returns the buffer to the pool - the success-path cleanup, and also what a mid-document growth spill uses</summary>
			/// <remarks>Claims and nulls <see cref="Buffer"/> before the write, so the array is returned to the pool exactly
			/// once even if <see cref="Stream.Write(byte[],int,int)"/> itself throws (a genuine I/O failure, not the
			/// serializer exception this call is not meant to run under - see <see cref="Abandon"/> for that case).</remarks>
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

			/// <summary>Returns the buffer to the pool WITHOUT writing the still-pending tail - the failure-path cleanup</summary>
			/// <remarks>Deliberately does not call <see cref="Stream.Write(byte[],int,int)"/>: per the failure-path contract
			/// documented on <see cref="CrystalXml.WriteTo{T}(Stream,ICrystalXmlSerializer{T},T,CrystalJsonSettings?,string?)"/>,
			/// content that has not already reached the destination through an earlier growth-triggered <see cref="Drain"/>
			/// must not be written after the serializer has thrown.</remarks>
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
		/// <remarks>
		/// <para><see cref="TextWriter"/> is a push-based sink (<see cref="TextWriter.Write(char[],int,int)"/>), not an
		/// <see cref="IBufferWriter{T}"/>. This buffers writes into a rented array and drains it whenever the array would
		/// need to grow to fit the next request, and once more at the end via <see cref="Drain"/> (success) or
		/// <see cref="Abandon"/> (failure) - see <see cref="CrystalXml.WriteTo{T}(TextWriter,ICrystalXmlSerializer{T},T,CrystalJsonSettings?,string?)"/>'s
		/// remarks for the exact contract each of those two implements.</para>
		/// <para><b>Single-owner discipline</b> (structurally identical to <see cref="StreamBufferProxy"/>, its byte-core
		/// twin). <see cref="Buffer"/> is returned to <see cref="ArrayPool{Char}.Shared"/> in exactly one place each for the
		/// "write it" and the "just give it back" cases, and both null the field as the very first step - before the array
		/// can possibly be written to the destination and before any exception from that write can be thrown - so no caller
		/// can ever observe a stale array, and no code path can return the same array twice. (An earlier version of this
		/// type read <c>this.Buffer.Length</c> and called <see cref="ArrayPool{Char}.Return"/> again AFTER an unconditional
		/// flush-and-null step, which threw a <see cref="NullReferenceException"/> on the first grow past the initial rent
		/// and would have double-returned the array had the null check alone been patched in; this shape has exactly one
		/// return per buffer, full stop.)</para>
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
				var buffer = this.Buffer;
				if (buffer is not null && buffer.Length - this.Count >= needed)
				{
					return;
				}

				// not enough room left in the current buffer (or no buffer rented yet): Drain() writes whatever is
				// pending and returns the current array exactly once (a no-op if Buffer is already null), THEN a
				// fresh one is rented, sized for this request - no double-return path, because Drain() always owns
				// the transition from "have a buffer" to "have none" before renting the replacement
				Drain();
				this.Buffer = ArrayPool<char>.Shared.Rent(Math.Max(needed, DefaultBufferSize));
			}

			/// <summary>Writes any buffered characters to the wrapped <see cref="TextWriter"/> and returns the buffer to the pool - the success-path cleanup, and also what a mid-document growth spill uses</summary>
			/// <remarks>Claims and nulls <see cref="Buffer"/> before the write, so the array is returned to the pool exactly
			/// once even if <see cref="TextWriter.Write(char[],int,int)"/> itself throws (a genuine I/O failure, not the
			/// serializer exception this call is not meant to run under - see <see cref="Abandon"/> for that case).</remarks>
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

			/// <summary>Returns the buffer to the pool WITHOUT writing the still-pending tail - the failure-path cleanup</summary>
			/// <remarks>Deliberately does not call <see cref="TextWriter.Write(char[],int,int)"/>: per the failure-path
			/// contract documented on <see cref="CrystalXml.WriteTo{T}(TextWriter,ICrystalXmlSerializer{T},T,CrystalJsonSettings?,string?)"/>,
			/// content that has not already reached the destination through an earlier growth-triggered <see cref="Drain"/>
			/// must not be written after the serializer has thrown.</remarks>
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
