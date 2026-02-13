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

namespace SnowBank.Buffers
{
	using System.Buffers;
	using System.Buffers.Binary;
	using System.ComponentModel;
	using System.Globalization;
	using System.Runtime.InteropServices;
	using System.Text;
	using SnowBank.Buffers.Binary;
	using SnowBank.Buffers.Text;
	using SnowBank.Data.Binary;
	using SnowBank.Text;

	/// <summary>Slice buffer that emulates a pseudo-stream using a byte array that will automatically grow in size, if necessary</summary>
	/// <remarks>This struct MUST be passed by reference!</remarks>
	[PublicAPI, DebuggerDisplay("Position={Position}, Capacity={Capacity}"), DebuggerTypeProxy(typeof(SliceWriter.DebugView))]
	[DebuggerNonUserCode] //remove this when you need to troubleshoot this class!
	public struct SliceWriter : IBufferWriter<byte>, ISliceSerializable, ISpanEncodable, IDisposable
	{
		// Invariant
		// * Valid data always start at offset 0
		// * 'this.Position' is equal to the current size as well as the offset of the next available free spot
		// * 'this.Buffer' is either null (meaning newly created stream), or is at least as big as this.Position

		#region Private Members...

		/// <summary>Buffer holding the data</summary>
		/// <remarks>Consider calling <see cref="GetBufferUnsafe"/> to protect against this field being null.</remarks>
		public byte[]? Buffer;

		/// <summary>Position in the buffer ( == number of already written bytes)</summary>
		public int Position;

		/// <summary>Optional pool used to allocate the buffers used by this writer</summary>
		/// <remarks>If <c>null</c>, will allocate on the heap</remarks>
		public readonly ArrayPool<byte>? Pool;

		#endregion

		#region Constructors...

		/// <summary>Create a new empty binary buffer with an initial allocated size</summary>
		/// <param name="capacity">Initial capacity of the buffer</param>
		public SliceWriter([Positive] int capacity)
		{
			Contract.Positive(capacity);

			this.Buffer = capacity == 0 ? [ ] : new byte[capacity];
			this.Position = 0;
			this.Pool = null;
		}

		/// <summary>Create a new empty binary buffer with an initial allocated size</summary>
		/// <param name="capacity">Initial capacity of the buffer</param>
		/// <param name="pool">Pool qui sera utilisé pour la gestion des buffers</param>
		public SliceWriter([Positive] int capacity, ArrayPool<byte>? pool)
		{
			Contract.Positive(capacity);

			this.Buffer = capacity == 0 ? [ ] : (pool?.Rent(capacity) ?? new byte[capacity]);
			this.Position = 0;
			this.Pool = pool;
		}

		/// <summary>Create a new empty binary buffer with an initial allocated size</summary>
		/// <param name="pool">Pool qui sera utilisé pour la gestion des buffers</param>
		public SliceWriter(ArrayPool<byte>? pool)
		{
			this.Buffer = [ ];
			this.Position = 0;
			this.Pool = pool;
		}

		/// <summary>Create a new binary writer using an existing buffer</summary>
		/// <param name="buffer">Initial buffer</param>
		/// <remarks>Since the content of the <paramref name="buffer"/> will be modified, only a temporary or scratch buffer should be used. If the writer needs to grow, a new buffer will be allocated.</remarks>
		public SliceWriter(byte[] buffer)
		{
			Contract.NotNull(buffer);

			this.Buffer = buffer;
			this.Position = 0;
			this.Pool = null;
		}

		/// <summary>Create a new binary buffer using an existing buffer and with the cursor to a specific location</summary>
		/// <remarks>Since the content of the <paramref name="buffer"/> will be modified, only a temporary or scratch buffer should be used. If the writer needs to grow, a new buffer will be allocated.</remarks>
		public SliceWriter(byte[] buffer, int index)
		{
			Contract.NotNull(buffer);
			Contract.Between(index, 0, buffer.Length);

			this.Buffer = buffer;
			this.Position = index;
			this.Pool = null;
		}

		/// <summary>Creates a new binary buffer, initialized by copying pre-existing data</summary>
		/// <param name="prefix">Data that will be copied at the start of the buffer</param>
		/// <param name="capacity">Optional initial capacity of the buffer</param>
		/// <remarks>The cursor will already be placed at the end of the prefix</remarks>
		public SliceWriter(Slice prefix, int capacity = 0)
		{
			prefix.EnsureSliceIsValid();
			Contract.Positive(capacity);

			var pool = ArrayPool<byte>.Shared;
			int n = prefix.Count;
			Contract.Debug.Assert(n >= 0);

			if (capacity == 0)
			{ // most frequent usage is to add a packed integer at the end of a prefix
				capacity = BitHelpers.AlignPowerOfTwo(n + 8, powerOfTwo: 16);
			}
			else
			{
				capacity = BitHelpers.AlignPowerOfTwo(Math.Max(capacity, n), powerOfTwo: 16);
			}

			var buffer = pool.Rent(capacity);
			if (n > 0) prefix.CopyTo(buffer, 0);

			this.Buffer = buffer;
			this.Position = n;
			this.Pool = pool;
		}

		#endregion

		#region Public Properties...

		/// <summary>Returns true if the buffer contains at least some data</summary>
		public readonly bool HasData
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Position > 0;
		}

		/// <summary>Capacity of the internal buffer</summary>
		public readonly int Capacity
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Buffer?.Length ?? 0;
		}

		/// <summary>Return the byte at the specified index</summary>
		/// <param name="index">Index in the buffer (0-based if positive, from the end if negative)</param>
		public readonly byte this[int index]
		{
			[Pure]
			get
			{
				int pos = this.Position;
				Contract.Debug.Assert(this.Buffer != null && pos >= 0);
				//note: we will get bound checking for free in release builds
				if (index < 0) index += pos;
				if ((uint) index >= pos) throw ThrowHelper.IndexOutOfRangeException();
				return this.Buffer[index];
			}
		}

		/// <summary>Returns a slice pointing to a segment inside the buffer</summary>
		/// <param name="beginInclusive">The starting position of the substring. Positive values means from the start, negative values means from the end</param>
		/// <param name="endExclusive">The end position (excluded) of the substring. Positive values means from the start, negative values means from the end</param>
		/// <returns>Slice that corresponds to the section selected. If the <paramref name="beginInclusive"/> if equal to or greater than <paramref name="endExclusive"/> then an empty Slice is returned</returns>
		/// <exception cref="ArgumentOutOfRangeException">If either <paramref name="beginInclusive"/> or <paramref name="endExclusive"/> is outside of the currently allocated buffer.</exception>
		public readonly Slice this[int? beginInclusive, int? endExclusive]
		{
			[Pure]
			get
			{
				int from = beginInclusive ?? 0;
				int pos = this.Position;
				int until = endExclusive ?? pos;

				// remap negative indexes
				if (from < 0) from += pos;
				if (until < 0) until += pos;

				// bound check
				if ((uint) from >= pos) throw ThrowHelper.ArgumentOutOfRangeException(nameof(beginInclusive), beginInclusive, "The start index must be inside the bounds of the buffer.");
				if ((uint) until > pos) throw ThrowHelper.ArgumentOutOfRangeException(nameof(endExclusive), endExclusive, "The end index must be inside the bounds of the buffer.");

				// chop chop
				int count = until - from;
				return count > 0 ? new Slice(this.Buffer!, from, count) : Slice.Empty;
			}
		}

		/// <summary>Return the byte that was previously written at the specified index</summary>
		public readonly byte this[Index index] => this[index.GetOffset(this.Position)];

		/// <summary>Return a slice that contains the bytes previously written at the specified range</summary>
		public readonly Slice this[Range range] => Substring(range);

		#endregion

		/// <summary>Returns the underlying buffer holding the data</summary>
		/// <remarks>This will never return <c>null</c>, unlike <see cref="Buffer"/> which can be <c>null</c> if the instance was never written to.</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly byte[] GetBufferUnsafe() => this.Buffer ?? [ ];

		/// <summary>Returns a byte array filled with the contents of the buffer</summary>
		/// <remarks>The buffer is copied in the byte array. And change to one will not impact the other</remarks>
		[Pure]
		public readonly byte[] GetBytes()
		{
			int p = this.Position;
			return p != 0 ? this.Buffer.AsSpan(0, p).ToArray() : [ ];
		}

		/// <summary>Returns a buffer segment pointing to the content of the buffer</summary>
		/// <remarks>Any change to the segment will change the buffer !</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly ArraySegment<byte> ToArraySegment()
		{
			return ToSlice();
		}

		/// <summary>Returns a <see cref="ReadOnlySpan{T}">ReadOnlySpan&lt;byte&gt;</see> pointing to the content of the buffer</summary>
		[Pure]
		public readonly ReadOnlySpan<byte> ToSpan()
		{
			var buffer = this.Buffer;
			var p = this.Position;
			if (buffer == null || p == 0)
			{ // empty buffer
				return default;
			}
			Contract.Debug.Assert(buffer.Length >= p, "Current position is outside of the buffer");
			return new ReadOnlySpan<byte>(buffer, 0, p);
		}

		/// <summary>Returns a <see cref="Slice"/> pointing to the content of the buffer</summary>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		[Pure]
		public readonly Slice ToSlice()
		{
			var buffer = this.Buffer;
			var p = this.Position;
			if (buffer == null || p == 0)
			{ // empty buffer
				return Slice.Empty;
			}
			Contract.Debug.Assert(buffer.Length >= p, "Current position is outside of the buffer");
			return new(buffer, 0, p);
		}

		/// <summary>Returns a <see cref="SliceOwner"/> with the content that was written to this writer</summary>
		/// <remarks>
		/// <para>The caller <b>MUST</b> dispose the returned instance, otherwise the buffer will not be returned to the pool</para>
		/// <para>The writer is reset to 0, and can be reused immediately</para>
		/// </remarks>
		[Pure]
		public SliceOwner ToSliceOwner()
		{
			var slice = ToSlice();
			this.Buffer = [ ];
			this.Position = 0;
			return SliceOwner.Create(slice, this.Pool);
		}

		/// <summary>Returns a <see cref="SliceOwner"/> with the content that was written to this writer, with optional clearing of the buffer after use.</summary>
		/// <param name="clearAfterUse">If <c>true</c>, the content of the buffer will be cleared when the returned <see cref="SliceOwner"/> is disposed</param>
		/// <remarks>
		/// <para>The caller <b>MUST</b> dispose the returned instance, otherwise the buffer will not be returned to the pool</para>
		/// <para>The writer is reset to 0, and can be reused immediately</para>
		/// </remarks>
		[Pure]
		public SliceOwner ToSliceOwner(bool clearAfterUse)
		{
			var slice = ToSlice();
			this.Buffer = [ ];
			this.Position = 0;
			return SliceOwner.Create(slice, this.Pool, clearAfterUse);
		}

		/// <summary>Returns a slice pointing to the first <paramref name="count"/> bytes of the buffer</summary>
		/// <param name="count">Size of the segment to return.</param>
		/// <returns>Slice that contains the first <paramref name="count"/> bytes written to this buffer</returns>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		/// <example><code>
		/// ({HELLO WORLD}).Head(5) => {HELLO}
		/// ({HELLO WORLD}).Head(1) => {H}
		/// ({HELLO WORLD}).Head(0) => {}
		/// </code></example>
		/// <exception cref="ArgumentException">If <paramref name="count"/> is less than zero, or larger than the current buffer size</exception>
		[Pure]
		public readonly Slice Head(int count)
		{
			if (count == 0) return Slice.Empty;
			if ((uint) count > this.Position) throw ThrowHelper.ArgumentOutOfRangeException(nameof(count), count, "Buffer is too small");
			return new Slice(this.Buffer!, 0, count);
		}

		/// <summary>Returns a slice pointing to the first <paramref name="count"/> bytes of the buffer</summary>
		/// <param name="count">Size of the segment to return.</param>
		/// <returns>Slice that contains the first <paramref name="count"/> bytes written to this buffer</returns>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		/// <example>
		/// ({HELLO WORLD}).Head(5) => {HELLO}
		/// ({HELLO WORLD}).Head(1) => {H}
		/// ({HELLO WORLD}).Head(0) => {}
		/// </example>
		/// <exception cref="ArgumentException">If <paramref name="count"/> is less than zero, or larger than the current buffer size</exception>
		[Pure]
		public readonly Slice Head(uint count)
		{
			if (count == 0) return Slice.Empty;
			if (count > this.Position) throw ThrowHelper.ArgumentOutOfRangeException(nameof(count), count, "Buffer is too small");
			return new Slice(this.Buffer!, 0, (int) count);
		}

		/// <summary>Returns a slice pointer to the last <paramref name="count"/> bytes of the buffer</summary>
		/// <param name="count">Size of the segment to return.</param>
		/// <returns>Slice that contains the last <paramref name="count"/> bytes written to this buffer</returns>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		/// <example>
		/// ({HELLO WORLD}).Tail(5) => {WORLD}
		/// ({HELLO WORLD}).Tail(1) => {D}
		/// ({HELLO WORLD}).Tail(0) => {}
		/// </example>
		/// <exception cref="ArgumentException">If <paramref name="count"/> is less than zero, or larger than the current buffer size</exception>
		public readonly Slice Tail(int count)
		{
			if (count == 0) return Slice.Empty;
			int p = this.Position;
			if ((uint) count > p) throw ThrowHelper.ArgumentOutOfRangeException(nameof(count), count, "Buffer is too small");
			return new Slice(this.Buffer!, p - count, count);
		}

		/// <summary>Returns a slice pointer to the last <paramref name="count"/> bytes of the buffer</summary>
		/// <param name="count">Size of the segment to return.</param>
		/// <returns>Slice that contains the last <paramref name="count"/> bytes written to this buffer</returns>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		/// <example><code>
		/// ({HELLO WORLD}).Tail(5) => {WORLD}
		/// ({HELLO WORLD}).Tail(1) => {D}
		/// ({HELLO WORLD}).Tail(0) => {}
		/// </code></example>
		/// <exception cref="ArgumentException">If <paramref name="count"/> is less than zero, or larger than the current buffer size</exception>
		public readonly Slice Tail(uint count)
		{
			if (count == 0) return Slice.Empty;
			int p = this.Position;
			if (count > p) throw ThrowHelper.ArgumentOutOfRangeException(nameof(count), count, "Buffer is too small");
			return new Slice(this.Buffer!, p - (int) count, (int) count);
		}

		/// <summary>Returns a slice pointing to a segment inside the buffer</summary>
		/// <param name="offset">Offset of the segment from the start of the buffer</param>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		/// <exception cref="ArgumentException">If <paramref name="offset"/> is less than zero, or after the current position</exception>
		[Pure]
		public readonly Slice Substring(int offset)
		{
			int p = this.Position;
			if (offset < 0 || offset > p) throw ThrowHelper.ArgumentException(nameof(offset), "Offset must be inside the buffer");
			int count = p - offset;
			return count > 0 ? new Slice(this.Buffer!, offset, p - offset) : Slice.Empty;
		}

		/// <summary>Returns a slice pointing to a segment inside the buffer</summary>
		/// <param name="offset">Offset of the segment from the start of the buffer</param>
		/// <param name="count">Size of the segment</param>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		/// <exception cref="ArgumentException">If either <paramref name="offset"/> or <paramref name="count"/> are less than zero, or do not fit inside the current buffer</exception>
		[Pure]
		public readonly Slice Substring(int offset, int count)
		{
			int p = this.Position;
			if ((uint) offset >= p) throw ThrowHelper.ArgumentException(nameof(offset), "Offset must be inside the buffer");
			if (count < 0 | offset + count > p) throw ThrowHelper.ArgumentException(nameof(count), "The buffer is too small");

			return count > 0 ? new Slice(this.Buffer!, offset, count) : Slice.Empty;
		}

		/// <summary>Returns a slice pointing to a segment inside the buffer</summary>
		/// <param name="range">Range to return</param>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		/// <exception cref="ArgumentException">If the <paramref name="range"/> does not fit inside the current buffer</exception>
		[Pure]
		public readonly Slice Substring(Range range) //REVIEW: convert to an indexer? writer[4..10] instead of writer.Substring(4..10) ?
		{
			(int offset, int count) = range.GetOffsetAndLength(this.Position);
			return count > 0 ? new Slice(this.Buffer!, offset, count) : Slice.Empty;
		}

		/// <summary>Truncate the buffer by setting the cursor to the specified position.</summary>
		/// <param name="position">New size of the buffer</param>
		/// <remarks>If the buffer was smaller, it will be resized and filled with zeroes. If it was bigger, the cursor will be set to the specified position, but previous data will not be deleted.</remarks>
		public void SetLength(int position)
		{
			Contract.Debug.Requires(position >= 0);

			int p = this.Position;
			if (p < position)
			{
				int missing = position - p;
				var buffer = EnsureBytes(missing);
				//TODO: native memset() ?
				Array.Clear(buffer, p, missing);
			}
			this.Position = position;
		}

		/// <summary>Delete the first N bytes of the buffer, and shift the remaining to the front</summary>
		/// <param name="bytes">Number of bytes to remove at the head of the buffer</param>
		/// <returns>New size of the buffer (or 0 if it is empty)</returns>
		/// <remarks>This should be called after every successful write to the underlying stream, to update the buffer.</remarks>
		public int Flush(int bytes)
		{
			if (bytes == 0) return this.Position;
			if (bytes < 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(bytes));

			if (bytes < this.Position)
			{ // copy the leftover data to the start of the buffer
				int remaining = this.Position - bytes;
				this.Buffer.AsSpan(bytes, remaining).CopyTo(this.Buffer.AsSpan());
				this.Position = remaining;
				return remaining;
			}
			else
			{
				//REVIEW: should we throw if there are fewer bytes in the buffer than we want to flush ?
				this.Position = 0;
				return 0;
			}
		}

		/// <summary>Empties the current buffer after a successful write</summary>
		/// <param name="shrink">If <c>true</c>, release the buffer if it was large and mostly unused. If <c>false</c>, keep the same buffer independent of its current size</param>
		/// <param name="zeroes">If <c>true</c>, fill the existing buffer with zeroes, if it is reused, to ensure that no previous data can leak.</param>
		/// <remarks>If the current buffer is large enough, and less than 1/8th was used, then it will be discarded and a new smaller one will be allocated as needed</remarks>
		public void Reset(bool shrink = false, bool zeroes = false)
		{
			var buffer = this.Buffer;
			if (buffer == null) return; // already empty buffer

			Contract.Debug.Assert(buffer.Length >= this.Position);
			// reduce size ?
			// If the buffer exceeds 64K, and we used less than 1/8 of it the last time, we will "shrink" the buffer
			if (shrink && buffer.Length > 65536 && this.Position <= (buffer.Length >> 3))
			{ // kill the buffer
				this.Pool?.Return(buffer, zeroes);
				this.Buffer = [ ];
			}
			else if (zeroes)
			{ // Clear it
				buffer.AsSpan(0, this.Position).Clear();
			}
			this.Position = 0;
		}

		/// <summary>Returns the current buffer to the pool</summary>
		public void Release(bool clear = false)
		{
			var buffer = this.Buffer;
			this.Buffer = [ ];
			this.Position = 0;
			if (buffer != null && buffer.Length != 0)
			{
				this.Pool?.Return(buffer, clear);
			}
		}

		/// <summary>Releases the resources allocated by this instance</summary>
		public void Dispose()
		{
			Release(clear: false);
		}

		/// <summary>Advance the cursor of the buffer without writing anything, and return the previous position</summary>
		/// <param name="skip">Number of bytes to skip</param>
		/// <param name="pad">Pad value (0xFF by default)</param>
		/// <returns>Position of the cursor BEFORE moving it. Can be used as a marker to go back later and fill some value</returns>
		/// <remarks>Will fill the skipped bytes with <paramref name="pad"/></remarks>
		public int Skip([Positive] int skip, byte pad = 0xFF)
		{
			Contract.Debug.Requires(skip >= 0);

			int p = this.Position;
			if (skip == 0) return p;

			EnsureBytes(skip).AsSpan(p, skip).Fill(pad);
			this.Position = p + skip;
			return p;
		}

		/// <summary>Advance the cursor by the specified amount, and return the skipped over chunk (that can be filled later by the caller)</summary>
		/// <param name="count">Number of bytes to allocate</param>
		/// <param name="pad">Pad value (0xFF by default)</param>
		/// <returns>Slice that corresponds to the reserved segment in the buffer</returns>
		/// <remarks>Will fill the reserved segment with <paramref name="pad"/> and the cursor will be positioned immediately after the segment.</remarks>
		public Span<byte> Allocate(int count, byte pad)
		{
			Contract.Positive(count);
			if (count == 0) return default;

			int offset = Skip(count, pad);
			return this.Buffer.AsSpan(offset, count);
		}

		/// <summary>Advance the cursor by the specified amount, and return the skipped over chunk (that can be filled later by the caller)</summary>
		/// <param name="count">Number of bytes to allocate</param>
		/// <returns>Slice that corresponds to the reserved segment in the buffer</returns>
		public Span<byte> Allocate(int count)
		{
			Contract.Positive(count);
			if (count == 0) return default;

			var buffer = EnsureBytes(count);
			int p = this.Position;
			this.Position = p + count;
			return buffer.AsSpan(p, count);
		}

		/// <summary>Advance the cursor by the amount required end up on an aligned byte position</summary>
		/// <param name="alignment">Number of bytes to align to</param>
		/// <param name="pad">Pad value (0 by default)</param>
		public void Align(int alignment, byte pad = 0)
		{
			Contract.Debug.Requires(alignment > 0);
			int r = this.Position % alignment;
			if (r > 0) Skip(alignment - r, pad);
		}

		/// <summary>Rewinds the cursor to a previous position in the buffer, while saving the current position</summary>
		/// <param name="cursor">Will receive the current cursor position</param>
		/// <param name="position">Previous position in the buffer</param>
		public void Rewind(out int cursor, int position)
		{
			Contract.Debug.Requires(position >= 0 && position <= this.Position);
			cursor = this.Position;
			this.Position = position;
		}

		#region Bytes...

		/// <summary>Adds a byte to the end of the buffer, and advance the cursor</summary>
		/// <param name="value">Byte, 8 bits</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteByte(byte value)
		{
			var buffer = EnsureBytes(1);
			int p = this.Position;
			buffer[p] = value;
			this.Position = p + 1;
		}

		/// <summary>Adds a byte to the end of the buffer, and advance the cursor</summary>
		/// <param name="value">Byte, 8 bits</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteByte(char value)
		{
			Contract.Debug.Assert(value <= 255);
			var buffer = EnsureBytes(1);
			int p = this.Position;
			buffer[p] = (byte) value;
			this.Position = p + 1;
		}

		/// <summary>Adds a byte to the end of the buffer, and advance the cursor</summary>
		/// <param name="value">Byte, 8 bits</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteByte(int value)
		{
			var buffer = EnsureBytes(1);
			int p = this.Position;
			buffer[p] = (byte) value;
			this.Position = p + 1;
		}

		/// <summary>Adds a byte to the end of the buffer, and advance the cursor</summary>
		/// <param name="value">Byte, 8 bits</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteByte(sbyte value)
		{
			var buffer = EnsureBytes(1);
			int p = this.Position;
			buffer[p] = (byte) value;
			this.Position = p + 1;
		}

		/// <summary>Adds a 1-byte boolean to the end of the buffer, and advance the cursor</summary>
		/// <param name="value">Boolean, encoded as either 0 or 1.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteByte(bool value)
		{
			var buffer = EnsureBytes(1);
			int p = this.Position;
			buffer[p] = value ? (byte) 1 : (byte) 0;
			this.Position = p + 1;
		}

		/// <summary>Dangerously writes a single byte at the end of the buffer, without any capacity checks!</summary>
		/// <remarks>
		/// This method DOES NOT check the buffer capacity before writing, and caller MUST have resized the buffer beforehand!
		/// Failure to do so may introduce memory correction (buffer overflow!).
		/// This should ONLY be used in performance-sensitive code paths that have been audited thoroughly!
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void UnsafeWriteByte(byte value)
		{
			Contract.Debug.Requires(this.Buffer != null && this.Position < this.Buffer.Length);
			this.Buffer[this.Position++] = value;
		}

		/// <summary>Writes two bytes at the end of the buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBytes(byte value1, byte value2)
		{
			var buffer = EnsureBytes(2);
			int p = this.Position;
			buffer[p] = value1;
			buffer[p + 1] = value2;
			this.Position = p + 2;
		}

		/// <summary>Dangerously writes two bytes at the end of the buffer, without any capacity checks!</summary>
		/// <remarks>
		/// This method DOES NOT check the buffer capacity before writing, and caller MUST have resized the buffer beforehand!
		/// Failure to do so may introduce memory correction (buffer overflow!).
		/// This should ONLY be used in performance-sensitive code paths that have been audited thoroughly!
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void UnsafeWriteBytes(byte value1, byte value2)
		{
			Contract.Debug.Requires(this.Buffer != null && this.Position + 1 < this.Buffer.Length);
			int p = this.Position;
			this.Buffer[p] = value1;
			this.Buffer[p + 1] = value2;
			this.Position = p + 2;
		}

		/// <summary>Writes three bytes at the end of the buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBytes(byte value1, byte value2, byte value3)
		{
			var buffer = EnsureBytes(3);
			int p = this.Position;
			buffer[p] = value1;
			buffer[p + 1] = value2;
			buffer[p + 2] = value3;
			this.Position = p + 3;
		}

		/// <summary>Dangerously writes three bytes at the end of the buffer, without any capacity checks!</summary>
		/// <remarks>
		/// This method DOES NOT check the buffer capacity before writing, and caller MUST have resized the buffer beforehand!
		/// Failure to do so may introduce memory correction (buffer overflow!).
		/// This should ONLY be used in performance-sensitive code paths that have been audited thoroughly!
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void UnsafeWriteBytes(byte value1, byte value2, byte value3)
		{
			Contract.Debug.Requires(this.Buffer != null && this.Position + 2 < this.Buffer.Length);
			var buffer = this.Buffer;
			int p = this.Position;
			buffer[p] = value1;
			buffer[p + 1] = value2;
			buffer[p + 2] = value3;
			this.Position = p + 3;
		}

		/// <summary>Write four bytes at the end of the buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBytes(byte value1, byte value2, byte value3, byte value4)
		{
			var buffer = EnsureBytes(4);
			int p = this.Position;
			buffer[p] = value1;
			buffer[p + 1] = value2;
			buffer[p + 2] = value3;
			buffer[p + 3] = value4;
			this.Position = p + 4;
		}

		/// <summary>Dangerously write four bytes at the end of the buffer, without any capacity checks!</summary>
		/// <remarks>
		/// This method DOES NOT check the buffer capacity before writing, and caller MUST have resized the buffer beforehand!
		/// Failure to do so may introduce memory correction (buffer overflow!).
		/// This should ONLY be used in performance-sensitive code paths that have been audited thoroughly!
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void UnsafeWriteBytes(byte value1, byte value2, byte value3, byte value4)
		{
			Contract.Debug.Requires(this.Buffer != null && this.Position + 3 < this.Buffer.Length);
			var buffer = this.Buffer;
			int p = this.Position;
			buffer[p] = value1;
			buffer[p + 1] = value2;
			buffer[p + 2] = value3;
			buffer[p + 3] = value4;
			this.Position = p + 4;
		}

		/// <summary>Writes five bytes at the end of the buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBytes(byte value1, byte value2, byte value3, byte value4, byte value5)
		{
			var buffer = EnsureBytes(5);
			int p = this.Position;
			buffer[p] = value1;
			buffer[p + 1] = value2;
			buffer[p + 2] = value3;
			buffer[p + 3] = value4;
			buffer[p + 4] = value5;
			this.Position = p + 5;
		}

		/// <summary>Dangerously write five bytes at the end of the buffer, without any capacity checks!</summary>
		/// <remarks>
		/// This method DOES NOT check the buffer capacity before writing, and caller MUST have resized the buffer beforehand!
		/// Failure to do so may introduce memory correction (buffer overflow!).
		/// This should ONLY be used in performance-sensitive code paths that have been audited thoroughly!
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void UnsafeWriteBytes(byte value1, byte value2, byte value3, byte value4, byte value5)
		{
			Contract.Debug.Requires(this.Buffer != null && this.Position + 4 < this.Buffer.Length);
			var buffer = this.Buffer;
			int p = this.Position;
			buffer[p] = value1;
			buffer[p + 1] = value2;
			buffer[p + 2] = value3;
			buffer[p + 3] = value4;
			buffer[p + 4] = value5;
			this.Position = p + 5;
		}

		/// <summary>Write a byte array to the end of the buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBytes(byte[]? data)
		{
			if (data != null)
			{
				WriteBytes(data.AsSpan());
			}
		}

		/// <summary>Write a chunk of a byte array to the end of the buffer</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public void WriteBytes(byte[] data, int offset, int count)
		{
			if (count > 0)
			{
				WriteBytes(data.AsSpan(offset, count));
			}
		}

		/// <summary>Write a slice of bytes to the end of the buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBytes(Slice data)
		{
			WriteBytes(data.Span);
		}

		/// <summary>Write a span of bytes to the end of the buffer</summary>
#if NET9_0_OR_GREATER
		public void WriteBytes(params ReadOnlySpan<byte> data)
#else
		public void WriteBytes(ReadOnlySpan<byte> data)
#endif
		{
			int p = this.Position;
			data.CopyTo(EnsureBytes(data.Length).AsSpan(p));
			this.Position = checked(p + data.Length);
		}

		/// <summary>Write a chunk of a byte array to the end of the buffer, with a prefix</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public void WriteBytes(byte prefix, byte[] data, int offset, int count)
		{
			WriteBytes(prefix, count != 0 ? data.AsSpan(offset, count) : default);
		}

		/// <summary>Write a segment of bytes to the end of the buffer, with a prefix</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBytes(byte prefix, Slice data)
		{
			WriteBytes(prefix, data.Span);
		}

		/// <summary>Write a segment of bytes to the end of the buffer, with a prefix</summary>
		public void WriteBytes(byte prefix, ReadOnlySpan<byte> data)
		{
			int count = data.Length;
			var buffer = EnsureBytes(checked(count + 1));
			int p = this.Position;
			buffer[p] = prefix;
			if (count > 0)
			{
				data.CopyTo(buffer.AsSpan(p + 1));
			}
			this.Position = checked(p + count + 1);
		}

		/// <summary>Write a segment of bytes to the end of the buffer, with a prefix</summary>
		public void WriteBytes(byte prefix, Span<byte> data)
		{
			int count = data.Length;
			var buffer = EnsureBytes(checked(count + 1));
			int p = this.Position;
			buffer[p] = prefix;
			if (count > 0)
			{
				data.CopyTo(buffer.AsSpan(p + 1));
			}
			this.Position = checked(p + count + 1);
		}

		/// <summary>Dangerously write a segment of bytes at the end of the buffer, without any capacity checks!</summary>
		/// <remarks>
		/// This method DOES NOT check the buffer capacity before writing, and caller MUST have resized the buffer beforehand!
		/// Failure to do so may introduce memory correction (buffer overflow!).
		/// This should ONLY be used in performance-sensitive code paths that have been audited thoroughly!
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void UnsafeWriteBytes(Slice data)
		{
			UnsafeWriteBytes(data.Span);
		}

		/// <summary>Dangerously write a segment of bytes at the end of the buffer, without any capacity checks!</summary>
		/// <remarks>
		/// This method DOES NOT check the buffer capacity before writing, and caller MUST have resized the buffer beforehand!
		/// Failure to do so may introduce memory correction (buffer overflow!).
		/// This should ONLY be used in performance-sensitive code paths that have been audited thoroughly!
		/// </remarks>
#if NET9_0_OR_GREATER
		public void UnsafeWriteBytes(params ReadOnlySpan<byte> data)
#else
		public void UnsafeWriteBytes(ReadOnlySpan<byte> data)
#endif
		{
			if (data.Length != 0)
			{
				int p = this.Position;
				Contract.Debug.Requires(this.Buffer != null && p >= 0 && p + data.Length <= this.Buffer.Length);

				int q = checked(p + data.Length);
				data.CopyTo(this.Buffer.AsSpan(p));
				this.Position = q;
			}
		}

		// Appending is used when the caller want to get a Slice that points to the location where the bytes where written in the internal buffer

		/// <summary>Append a byte array to the end of the buffer</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice AppendBytes(byte[]? data)
		{
			return data != null ? AppendBytes(data.AsSpan()) : Slice.Empty;
		}

		/// <summary>Append a chunk of a byte array to the end of the buffer</summary>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public Slice AppendBytes(byte[] data, int offset, int count)
		{
			return count != 0 ? AppendBytes(data.AsSpan(offset, count)) : Slice.Empty;
		}

		/// <summary>Append a segment of bytes to the end of the buffer</summary>
		/// <param name="data">Buffer containing the data to append</param>
		/// <returns>Slice that maps the interned data using the writer's buffer.</returns>
		/// <remarks>If you do not need the resulting Slice, you should call <see cref="WriteBytes(Slice)"/> instead!</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice AppendBytes(Slice data)
		{
			return AppendBytes(data.Span);
		}

		/// <summary>Append a segment of bytes to the end of the buffer</summary>
		/// <param name="data">Buffer containing the data to append</param>
		/// <returns>Slice that maps the interned data using the writer's buffer.</returns>
		/// <remarks>If you do not need the resulting Slice, you should call <see cref="WriteBytes(Slice)"/> instead!</remarks>
		[Pure]
		public Slice AppendBytes(ReadOnlySpan<byte> data)
		{
			int count = data.Length;
			if (count == 0) return Slice.Empty;

			int p = this.Position;
			var buffer = EnsureBytes(count);
			data.CopyTo(buffer.AsSpan(p));
			this.Position = checked(p + count);
			return new Slice(buffer, p, count);
		}

		/// <summary>Append a segment of bytes to the end of the buffer</summary>
		/// <param name="data">Buffer containing the data to append</param>
		/// <returns>Slice that maps the interned data using the writer's buffer.</returns>
		/// <remarks>If you do not need the resulting Slice, you should call <see cref="WriteBytes(Slice)"/> instead!</remarks>
		[Pure]
		public Slice AppendBytes(Span<byte> data)
		{
			int count = data.Length;
			if (count == 0) return Slice.Empty;

			int p = this.Position;
			var buffer = EnsureBytes(count);
			data.CopyTo(buffer.AsSpan(p));
			this.Position = checked(p + count);
			return new Slice(buffer, p, count);
		}

		#endregion

		#region Fixed, Little-Endian

		#region 16-bits

		/// <summary>Writes a 16-bit unsigned integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 2 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use WriteInt16 instead")]
		public void WriteFixed16(short value) => BinaryPrimitives.WriteInt16LittleEndian(AllocateSpan(2), value);

		/// <summary>Writes a 16-bit unsigned integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 2 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteInt16(short value) => BinaryPrimitives.WriteInt16LittleEndian(AllocateSpan(2), value);

		/// <summary>Writes a 16-bit unsigned integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 2 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use WriteUInt16 instead")]
		public void WriteFixed16(ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(AllocateSpan(2), value);

		/// <summary>Writes a 16-bit unsigned integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 2 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteUInt16(ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(AllocateSpan(2), value);

		#endregion

		#region 24-bits

		/// <summary>Writes a 24-bit unsigned integer, using little-endian encoding</summary>
		/// <param name="value">Value to write. The upper 8-bits are ignored.</param>
		/// <remarks>Advances the cursor by 3 bytes</remarks>
		[Obsolete("Use WriteInt24 instead")]
		public void WriteFixed24(int value)
		{
			unsafe
			{
				fixed (byte* ptr = AllocateSpan(3))
				{
					UnsafeHelpers.StoreUInt24LE(ptr, (uint) value);
				}
			}
		}

		/// <summary>Writes a 24-bit unsigned integer, using little-endian encoding</summary>
		/// <param name="value">Value to write. The upper 8-bits are ignored.</param>
		/// <remarks>Advances the cursor by 3 bytes</remarks>
		public void WriteInt24(int value)
		{
			unsafe
			{
				fixed (byte* ptr = AllocateSpan(3))
				{
					UnsafeHelpers.StoreUInt24LE(ptr, (uint) value);
				}
			}
		}

		/// <summary>Writes a 24-bit unsigned integer, using little-endian encoding</summary>
		/// <param name="value">Value to write. The upper 8-bits are ignored.</param>
		/// <remarks>Advances the cursor by 3 bytes</remarks>
		[Obsolete("Use WriteUInt24 instead")]
		public void WriteFixed24(uint value)
		{
			unsafe
			{
				fixed (byte* ptr = AllocateSpan(3))
				{
					UnsafeHelpers.StoreUInt24LE(ptr, value);
				}
			}
		}

		/// <summary>Writes a 24-bit unsigned integer, using little-endian encoding</summary>
		/// <param name="value">Value to write. The upper 8-bits are ignored.</param>
		/// <remarks>Advances the cursor by 3 bytes</remarks>
		public void WriteUInt24(uint value)
		{
			unsafe
			{
				fixed (byte* ptr = AllocateSpan(3))
				{
					UnsafeHelpers.StoreUInt24LE(ptr, value);
				}
			}
		}

		#endregion

		#region 32-bits

		/// <summary>Writes a 32-bit signed integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 4 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use WriteInt32 instead")]
		public void WriteFixed32(int value) => BinaryPrimitives.WriteInt32LittleEndian(AllocateSpan(4), value);

		/// <summary>Writes a 32-bit signed integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 4 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteInt32(int value) => BinaryPrimitives.WriteInt32LittleEndian(AllocateSpan(4), value);

		/// <summary>Writes a 32-bit unsigned integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 4 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use WriteUInt32 instead")]
		public void WriteFixed32(uint value) => BinaryPrimitives.WriteUInt32LittleEndian(AllocateSpan(4), value);

		/// <summary>Writes a 32-bit unsigned integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 4 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteUInt32(uint value) => BinaryPrimitives.WriteUInt32LittleEndian(AllocateSpan(4), value);

		#endregion

		#region 64-bits

		/// <summary>Writes a 64-bit signed integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 8 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use WriteInt64 instead")]
		public void WriteFixed64(long value) => BinaryPrimitives.WriteInt64LittleEndian(AllocateSpan(8), value);

		/// <summary>Writes a 64-bit signed integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 8 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteInt64(long value) => BinaryPrimitives.WriteInt64LittleEndian(AllocateSpan(8), value);

		/// <summary>Writes a 64-bit unsigned integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 8 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use WriteUInt64 instead")]
		public void WriteFixed64(ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(AllocateSpan(8), value);

		/// <summary>Writes a 64-bit unsigned integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 8 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteUInt64(ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(AllocateSpan(8), value);

		#endregion

		#region 128-bits

#if NET8_0_OR_GREATER

		/// <summary>Writes a 128-bit signed integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 16 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use WriteInt128 instead")]
		public void WriteFixed128(Int128 value) => BinaryPrimitives.WriteInt128LittleEndian(AllocateSpan(16), value);

		/// <summary>Writes a 128-bit signed integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 16 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteInt128(Int128 value) => BinaryPrimitives.WriteInt128LittleEndian(AllocateSpan(16), value);

		/// <summary>Writes a 128-bit unsigned integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 16 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use WriteUInt128 instead")]
		public void WriteFixed128(UInt128 value) => BinaryPrimitives.WriteUInt128LittleEndian(AllocateSpan(16), value);

		/// <summary>Writes a 128-bit unsigned integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 16 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteUInt128(UInt128 value) => BinaryPrimitives.WriteUInt128LittleEndian(AllocateSpan(16), value);

#endif

		#endregion

		#endregion

		#region Fixed, Big-Endian

		#region 16-bits

		/// <summary>Writes a 16-bit signed integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 2 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use WriteInt16BE instead")]
		public void WriteFixed16BE(short value) => BinaryPrimitives.WriteInt16BigEndian(AllocateSpan(2), value);

		/// <summary>Writes a 16-bit signed integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 2 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteInt16BE(short value) => BinaryPrimitives.WriteInt16BigEndian(AllocateSpan(2), value);

		/// <summary>Writes a 16-bit unsigned integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 2 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use WriteUInt16BE instead")]
		public void WriteFixed16BE(ushort value) => BinaryPrimitives.WriteUInt16BigEndian(AllocateSpan(2), value);

		/// <summary>Writes a 16-bit unsigned integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 2 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteUInt16BE(ushort value) => BinaryPrimitives.WriteUInt16BigEndian(AllocateSpan(2), value);
		#endregion

		#region 24-bits

		/// <summary>Writes a 24-bit signed integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 2 bytes</remarks>
		[Obsolete("Use WriteInt24BE instead")]
		public void WriteFixed24BE(int value)
		{
			unsafe
			{
				fixed (byte* ptr = AllocateSpan(3))
				{
					UnsafeHelpers.StoreInt24BE(ptr, value);
				}
			}
		}

		/// <summary>Writes a 24-bit signed integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 2 bytes</remarks>
		public void WriteInt24BE(int value)
		{
			unsafe
			{
				fixed (byte* ptr = AllocateSpan(3))
				{
					UnsafeHelpers.StoreInt24BE(ptr, value);
				}
			}
		}

		/// <summary>Writes a 24-bit unsigned integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 3 bytes</remarks>
		[Obsolete("Use WriteUInt24BE instead")]
		public void WriteFixed24BE(uint value)
		{
			unsafe
			{
				fixed (byte* ptr = AllocateSpan(3))
				{
					UnsafeHelpers.StoreUInt24BE(ptr, value);
				}
			}
		}

		/// <summary>Writes a 24-bit unsigned integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 3 bytes</remarks>
		public void WriteUInt24BE(uint value)
		{
			unsafe
			{
				fixed (byte* ptr = AllocateSpan(3))
				{
					UnsafeHelpers.StoreUInt24BE(ptr, value);
				}
			}
		}

		#endregion

		#region 32-bits

		/// <summary>Writes a 32-bit signed integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 4 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use WriteInt32BE instead")]
		public void WriteFixed32BE(int value) => BinaryPrimitives.WriteInt32BigEndian(AllocateSpan(4), value);

		/// <summary>Writes a 32-bit signed integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 4 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteInt32BE(int value) => BinaryPrimitives.WriteInt32BigEndian(AllocateSpan(4), value);

		/// <summary>Writes a 32-bit unsigned integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 4 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use WriteUInt32BE instead")]
		public void WriteFixed32BE(uint value) => BinaryPrimitives.WriteUInt32BigEndian(AllocateSpan(4), value);

		/// <summary>Writes a 32-bit unsigned integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 4 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteUInt32BE(uint value) => BinaryPrimitives.WriteUInt32BigEndian(AllocateSpan(4), value);

		#endregion

		#region 64-bits

		/// <summary>Writes a 64-bit signed integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 8 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use WriteInt64BE instead")]
		public void WriteFixed64BE(long value) => BinaryPrimitives.WriteInt64BigEndian(AllocateSpan(8), value);

		/// <summary>Writes a 64-bit signed integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 8 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteInt64BE(long value) => BinaryPrimitives.WriteInt64BigEndian(AllocateSpan(8), value);

		/// <summary>Writes a 64-bit unsigned integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 8 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use WriteUInt64BE instead")]
		public void WriteFixed64BE(ulong value) => BinaryPrimitives.WriteUInt64BigEndian(AllocateSpan(8), value);

		/// <summary>Writes a 64-bit unsigned integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 8 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteUInt64BE(ulong value) => BinaryPrimitives.WriteUInt64BigEndian(AllocateSpan(8), value);

		#endregion

		#region 128-bits

#if NET8_0_OR_GREATER

		/// <summary>Writes a 128-bit signed integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 16 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use WriteInt128BE instead")]
		public void WriteFixed128BE(Int128 value) => BinaryPrimitives.WriteInt128BigEndian(AllocateSpan(16), value);

		/// <summary>Writes a 128-bit signed integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 16 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteInt128BE(Int128 value) => BinaryPrimitives.WriteInt128BigEndian(AllocateSpan(16), value);

		/// <summary>Writes a 128-bit unsigned integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 16 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use WriteUInt128BE instead")]
		public void WriteFixed128BE(UInt128 value) => BinaryPrimitives.WriteUInt128BigEndian(AllocateSpan(16), value);

		/// <summary>Writes a 128-bit unsigned integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 16 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteUInt128BE(UInt128 value) => BinaryPrimitives.WriteUInt128BigEndian(AllocateSpan(16), value);

#endif

		#endregion

		#endregion

		#region Decimals...

		/// <summary>Writes a 32-bit IEEE floating point number, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 4 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteSingle(float value)
		{
			BinaryPrimitives.WriteSingleLittleEndian(AllocateSpan(4), value);
		}

		/// <summary>Writes a 32-bit IEEE floating point number, using little-endian encoding, preceded by a single byte</summary>
		/// <remarks>Advances the cursor by 5 bytes</remarks>
		public void WriteSingle(byte prefix, float value)
		{
			var buffer = AllocateSpan(5);
			buffer[0] = prefix;
			BinaryPrimitives.WriteSingleLittleEndian(buffer.Slice(1), value);
		}

		/// <summary>Writes a 32-bit IEEE floating point number, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 4 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteSingleBE(float value)
		{
			BinaryPrimitives.WriteSingleBigEndian(AllocateSpan(4), value);
		}

		/// <summary>Writes a 32-bit IEEE floating point number, using big-endian encoding, preceded by a single byte</summary>
		/// <remarks>Advances the cursor by 5 bytes</remarks>
		public void WriteSingleBE(byte prefix, float value)
		{
			var buffer = AllocateSpan(5);
			buffer[0] = prefix;
			BinaryPrimitives.WriteSingleBigEndian(buffer.Slice(1), value);
		}

		/// <summary>Writes a 64-bit IEEE floating point number, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 8 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteDouble(double value)
		{
			BinaryPrimitives.WriteDoubleLittleEndian(AllocateSpan(8), value);
		}

		/// <summary>Writes a 64-bit IEEE floating point number, using little-endian encoding, preceded by a single byte</summary>
		/// <remarks>Advances the cursor by 9 bytes</remarks>
		public void WriteDouble(byte prefix, double value)
		{
			var buffer = AllocateSpan(9);
			buffer[0] = prefix;
			BinaryPrimitives.WriteDoubleLittleEndian(buffer.Slice(1), value);
		}

		/// <summary>Writes a 64-bit IEEE floating point number, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 8 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteDoubleBE(double value)
		{
			BinaryPrimitives.WriteDoubleBigEndian(AllocateSpan(8), value);
		}

		/// <summary>Writes a 64-bit IEEE floating point number, using little-endian encoding, preceded by a single byte</summary>
		/// <remarks>Advances the cursor by 9 bytes</remarks>
		public void WriteDoubleBE(byte prefix, double value)
		{
			var buffer = AllocateSpan(9);
			buffer[0] = prefix;
			BinaryPrimitives.WriteDoubleBigEndian(buffer.Slice(1), value);
		}

		#endregion

		#region Variable size

		#region VarInts...

		/// <summary>Writes a 7-bit encoded unsigned int (aka 'Varint16') at the end, and advances the cursor</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteVarInt16(ushort value)
		{
			if (value < (1 << 7))
			{
				WriteByte((byte)value);
			}
			else
			{
				WriteVarInt16Slow(value);
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void WriteVarInt16Slow(ushort value)
		{
			const uint MASK = 128;
			//note: value is known to be >= 128
			if (value < (1 << 14))
			{
				WriteBytes(
					(byte)(value | MASK),
					(byte)(value >> 7)
				);
			}
			else
			{
				WriteBytes(
					(byte)(value | MASK),
					(byte)((value >> 7) | MASK),
					(byte)(value >> 14)
				);
			}

		}

		/// <summary>Writes a 7-bit encoded unsigned int (aka 'Varint32') at the end, and advances the cursor</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteVarInt32(uint value)
		{
			if (value < (1 << 7))
			{
				WriteByte((byte) value);
			}
			else
			{
				WriteVarInt32Slow(value);
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void WriteVarInt32Slow(uint value)
		{
			const uint MASK = 128;
			//note: value is known to be >= 128
			if (value < (1 << 14))
			{
				WriteBytes(
					(byte)(value | MASK),
					(byte)(value >> 7)
				);
			}
			else if (value < (1 << 21))
			{
				WriteBytes(
					(byte)(value | MASK),
					(byte)((value >> 7) | MASK),
					(byte)(value >> 14)
				);
			}
			else if (value < (1 << 28))
			{
				WriteBytes(
					(byte)(value | MASK),
					(byte)((value >> 7) | MASK),
					(byte)((value >> 14) | MASK),
					(byte)(value >> 21)
				);
			}
			else
			{
				WriteBytes(
					(byte)(value | MASK),
					(byte)((value >> 7) | MASK),
					(byte)((value >> 14) | MASK),
					(byte)((value >> 21) | MASK),
					(byte)(value >> 28)
				);
			}
		}

		/// <summary>Writes a 7-bit encoded unsigned long (aka 'Varint64') at the end, and advances the cursor</summary>
		public void WriteVarInt64(ulong value)
		{
			const uint MASK = 128;

			if (value < MASK)
			{
				WriteByte((byte) value);
				return;
			}

			// max encoded size is 10 bytes
			var buffer = EnsureBytes(UnsafeHelpers.SizeOfVarInt(value));
			ref byte ptr = ref buffer[this.Position];
			while (value >= MASK)
			{
				ptr = (byte) ((value & (MASK - 1)) | MASK);
				value >>= 7;
				ptr = ref Unsafe.Add(ref ptr, 1);
			}
			ptr = (byte) value;
			this.Position = Unsafe.ByteOffset(ref buffer[0], ref ptr).ToInt32() + 1;
		}

#if NET8_0_OR_GREATER

		/// <summary>Writes a 7-bit encoded unsigned 128-bit integer (aka 'Varint128') at the end, and advances the cursor</summary>
		public void WriteVarInt128(UInt128 value)
		{
			const uint MASK = 128;

			if (value < MASK)
			{
				WriteByte((byte) value);
				return;
			}

			// max encoded size is 10 bytes
			var buffer = EnsureBytes(UnsafeHelpers.SizeOfVarInt(value));
			ref byte ptr = ref buffer[this.Position];
			while (value >= MASK)
			{
				byte x = (byte) value;
				ptr = (byte) ((x & (MASK - 1)) | MASK);
				value >>= 7;
				ptr = ref Unsafe.Add(ref ptr, 1);
			}
			ptr = (byte) value;
			this.Position = Unsafe.ByteOffset(ref buffer[0], ref ptr).ToInt32() + 1;
		}

#endif

		#endregion

		#region VarBytes...

		/// <summary>Writes a length-prefixed byte array, and advances the cursor</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteVarBytes(byte[] bytes)
		{
			Contract.Debug.Requires(bytes != null);
			WriteVarBytes(bytes.AsSpan());
		}

		/// <summary>Writes a length-prefixed byte array, and advances the cursor</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteVarBytes(Slice value)
		{
			WriteVarBytes(value.Span);
		}

		/// <summary>Writes a length-prefixed byte array, and advances the cursor</summary>
		public void WriteVarBytes(ReadOnlySpan<byte> value)
		{
			int n = value.Length;
			if (n >= 128)
			{
				WriteVarBytesSlow(value);
				return;
			}

			var buffer = EnsureBytes(n + 1);
			int p = this.Position;

			// write the count (single byte)
			buffer[p] = (byte)n;

			// write the bytes
			if (n > 0) value.CopyTo(buffer.AsSpan(p + 1));
			this.Position = checked(p + n + 1);
		}

		/// <summary>Writes a length-prefixed byte array, and advances the cursor</summary>
		public void WriteVarBytes(Span<byte> value)
		{
			int n = value.Length;
			if (n >= 128)
			{
				WriteVarBytesSlow(value);
				return;
			}

			var buffer = EnsureBytes(n + 1);
			int p = this.Position;

			// write the count (single byte)
			buffer[p] = (byte)n;

			// write the bytes
			if (n > 0) value.CopyTo(buffer.AsSpan(p + 1));
			this.Position = checked(p + n + 1);
		}

		private void WriteVarBytesSlow(ReadOnlySpan<byte> value)
		{
			int n = value.Length;
			var buffer = EnsureBytes(checked(n + 5));

			// write the count
			WriteVarInt32((uint) n);
			Contract.Debug.Assert(this.Buffer == buffer);

			// write the bytes
			int p = this.Position;
			value.CopyTo(buffer.AsSpan(p));
			this.Position = checked(p + n);
		}

		#endregion

		#region VarString...

		// all VarStrings are encoded as a VarInt that contains the number of following encoded bytes
		// => caller MUST KNOW the encoding! (usually UTF-8)
		// => the string's length is NOT stored!

		/// <summary>Writes a variable-sized string, using the specified encoding</summary>
		public void WriteVarString(string? value, Encoding? encoding = null)
		{
			if (encoding == null)
			{
				WriteVarStringUtf8(value);
				return;
			}
			uint byteCount;
			if (value == null || (byteCount = checked((uint) encoding.GetByteCount(value))) == 0)
			{
				WriteByte(0);
				return;
			}
			var buffer = EnsureBytes(byteCount + UnsafeHelpers.SizeOfVarBytes(byteCount));

			// write the count
			WriteVarInt32(byteCount);
			Contract.Debug.Assert(this.Buffer == buffer);

			// write the chars
			int p = this.Position;
			int n = encoding.GetBytes(s: value, charIndex: 0, charCount: value.Length, bytes: buffer, byteIndex: p);
			this.Position = checked(p + n);
		}

		/// <summary>Writes a variable-sized string, using the specified encoding</summary>
		public void WriteVarString(ReadOnlySpan<char> value, Encoding? encoding = null)
		{
			if (encoding == null)
			{
				WriteVarStringUtf8(value);
				return;
			}
			uint byteCount;
			if ((byteCount = checked((uint) encoding.GetByteCount(value))) == 0)
			{
				WriteByte(0);
				return;
			}
			var buffer = EnsureBytes(byteCount + UnsafeHelpers.SizeOfVarBytes(byteCount));

			// write the count
			WriteVarInt32(byteCount);
			Contract.Debug.Assert(this.Buffer == buffer);

			// write the chars
			int p = this.Position;
			int n = encoding.GetBytes(value, buffer.AsSpan(p));
			this.Position = checked(p + n);
		}

		/// <summary>Writes a variable-sized string, encoded using UTF-8</summary>
		/// <param name="value">String to append</param>
		/// <remarks>The null and empty string will be stored the same way. Caller must use a different technique if they must be stored differently.</remarks>
		public void WriteVarStringUtf8(string? value)
		{
			// Format:
			// - VarInt     Number of following bytes
			// - Byte[]     UTF-8 encoded bytes
			// Examples:
			// - "" => { 0x00 }
			// - "ABC" => { 0x03 'A' 'B' 'C' }
			// - "Héllo" => { 0x06 'h' 0xC3 0xA9 'l' 'l' 'o' }

			// We need to know the encoded size beforehand, because we need to write the size first!
			int byteCount;
			if (value == null || (byteCount = Encoding.UTF8.GetByteCount(value)) == 0)
			{ // null or empty string
				WriteByte(0);
			}
			else if (byteCount == value.Length)
			{ // ASCII!
				WriteVarAsciiInternal(value.AsSpan());
			}
			else
			{ // contains non-ASCII characters, we will need to encode
				WriteVarStringUtf8Internal(value.AsSpan(), byteCount);
			}
		}

		/// <summary>Writes a variable-sized string, encoded using UTF-8</summary>
		/// <param name="value">String to append</param>
		/// <remarks>The empty string will be stored the same way. Caller must use a different technique if they must be stored differently.</remarks>
		public void WriteVarStringUtf8(ReadOnlySpan<char> value)
		{
			// Format:
			// - VarInt     Number of following bytes
			// - Byte[]     UTF-8 encoded bytes
			// Examples:
			// - "" => { 0x00 }
			// - "ABC" => { 0x03 'A' 'B' 'C' }
			// - "Héllo" => { 0x06 'h' 0xC3 0xA9 'l' 'l' 'o' }

			// We need to know the encoded size beforehand, because we need to write the size first!
			int byteCount;
			if ((byteCount = Encoding.UTF8.GetByteCount(value)) == 0)
			{ // null or empty string
				WriteByte(0);
			}
			else if (byteCount == value.Length)
			{ // ASCII!
				WriteVarAsciiInternal(value);
			}
			else
			{ // contains non-ASCII characters, we will need to encode
				WriteVarStringUtf8Internal(value, byteCount);
			}
		}

		private void WriteVarStringUtf8Internal(ReadOnlySpan<char> value, int byteCount)
		{
			Contract.Debug.Assert(byteCount > 0 && byteCount >= value.Length);
			var buffer = EnsureBytes(byteCount + UnsafeHelpers.SizeOfVarBytes(byteCount));
			WriteVarInt32((uint) byteCount);
			int p = this.Position;
			int n = Encoding.UTF8.GetBytes(value, buffer.AsSpan(p));
			this.Position = checked(p + n);
		}

		/// <summary>Writes a variable-sized string, which is known to only contain ASCII characters (0..127)</summary>
		/// <remarks>This is faster than <see cref="WriteVarString(string, Encoding)"/> when the caller KNOWS that the string is ASCII only. This should only be used with keywords and constants, NOT with user input!</remarks>
		/// <exception cref="ArgumentException">If the string contains characters above 127</exception>
		public void WriteVarStringAscii(string? value)
		{
			if (string.IsNullOrEmpty(value))
			{
				WriteByte(0);
			}
			else
			{
				WriteVarAsciiInternal(value.AsSpan());
			}
		}

		/// <summary>Writes a variable-sized string, which is known to only contain ASCII characters (0..127)</summary>
		/// <remarks>This is faster than <see cref="WriteVarString(string, Encoding)"/> when the caller KNOWS that the string is ASCII only. This should only be used with keywords and constants, NOT with user input!</remarks>
		/// <exception cref="ArgumentException">If the string contains characters above 127</exception>
		public void WriteVarStringAscii(ReadOnlySpan<char> value)
		{
			if (value.Length == 0)
			{
				WriteByte(0);
			}
			else
			{
				WriteVarAsciiInternal(value);
			}
		}

		/// <summary>Writes a variable string that is known to only contain ASCII characters</summary>
		private void WriteVarAsciiInternal(ReadOnlySpan<char> value)
		{
			// Caller must ensure that string is ASCII only! (otherwise it will be corrupted)
			Contract.Debug.Requires(value.Length > 0);

			int len = value.Length;
			var buffer = EnsureBytes(len + UnsafeHelpers.SizeOfVarBytes(len));
			int p = this.Position;

			p += UnsafeHelpers.WriteVarInt32(buffer.AsSpan(p), (uint) value.Length);

			ref byte ptr = ref buffer[p];
			int mask = 0;
			foreach(var c in value)
			{
				mask |= c;
				ptr = (byte) c;
				ptr = ref Unsafe.Add(ref ptr, 1);
			}
			if (mask >= 128) throw ThrowHelper.ArgumentException(nameof(value), "The specified string must only contain ASCII characters.");

			this.Position = checked(p + value.Length);
		}

		#endregion

		#endregion

		#region UUIDs...

		/// <summary>Writes a 128-bit UUID, and advances the cursor</summary>
		public void WriteUuid128(in Uuid128 value)
		{
			value.WriteTo(AllocateSpan(Uuid128.SizeOf));
		}

		/// <summary>Writes a 96-bit UUID, and advances the cursor</summary>
		public void WriteUuid96(in Uuid96 value)
		{
			value.WriteToUnsafe(AllocateSpan(Uuid96.SizeOf));
		}

		/// <summary>Writes an 80-bit UUID, and advances the cursor</summary>
		public void WriteUuid80(in Uuid80 value)
		{
			value.WriteToUnsafe(AllocateSpan(Uuid80.SizeOf));
		}

		/// <summary>Writes a 64-bit UUID, and advances the cursor</summary>
		public void WriteUuid64(Uuid64 value)
		{
			value.WriteToUnsafe(AllocateSpan(Uuid64.SizeOf));
		}

		/// <summary>Writes a 48-bit UUID, and advances the cursor</summary>
		public void WriteUuid48(Uuid48 value)
		{
			value.WriteToUnsafe(AllocateSpan(Uuid48.SizeOf));
		}

		#endregion

		#region Fixed-Size Text

		/// <summary>Write a string using UTF-8</summary>
		/// <param name="value">Text to write</param>
		/// <returns>Number of bytes written</returns>
		/// <example><c>writer.WriteString("Hello, World!")</c></example>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int WriteString(string? value)
		{
			return WriteStringUtf8(value);
		}

		/// <summary>Write a string that is already encoded in UTF-8</summary>
		/// <param name="value">Encoded text to write</param>
		/// <returns>Number of bytes written</returns>
		/// <example><c>writer.WriteString("Hello, World!"u8)</c></example>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int WriteString(ReadOnlySpan<byte> value)
		{
			//note: this is the same as WriteBytes, but people may look for "WriteString" if they are writing a keyword of magic signature...
			WriteBytes(value);
			return value.Length;
		}

		/// <summary>Write a string using UTF-8</summary>
		/// <param name="value">Text to write</param>
		/// <returns>Number of bytes written</returns>
		/// <example><c>writer.WriteString("Hello, World!".AsSpan(7))</c></example>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int WriteString(ReadOnlySpan<char> value)
		{
			return WriteStringUtf8(value);
		}

		/// <summary>Write a string using the specified encoding</summary>
		/// <param name="value">Text to write</param>
		/// <param name="encoding">Encoding used to convert the text to bytes</param>
		/// <returns>Number of bytes written</returns>
		/// <example><c>writer.WriteString("Héllô, Wörld!", Encoding.Latin1)</c></example>
		public int WriteString(string? value, Encoding? encoding)
		{
			if (string.IsNullOrEmpty(value)) return 0;
			encoding ??= Encoding.UTF8;

			// In order to estimate the required capacity, we try to guess for very small strings, but compute the actual value for larger strings,
			// so that we don't waste too much memory (up to 6x the string length in the worst case scenario)
			var buffer = EnsureBytes(value.Length > 128 ? encoding.GetByteCount(value) : encoding.GetMaxByteCount(value.Length));

			int p = this.Position;
			int n = encoding.GetBytes(value, 0, value.Length, buffer, p);
			this.Position = p + n;
			return n;
		}

		/// <summary>Write an already UTF-8 encoded string</summary>
		/// <returns>Number of bytes written</returns>
		public int WriteString(Utf8String value)
		{
			if (value.IsNullOrEmpty) return 0;
			int n = value.Buffer.Count;
			var buffer = EnsureBytes(n);
			int p = this.Position;
			value.Buffer.CopyTo(buffer, p);
			this.Position = p + n;
			return n;
		}

		/// <summary>Write a string using UTF-8</summary>
		/// <param name="value">Text to write</param>
		/// <returns>Number of bytes written</returns>
		/// <example><c>writer.WriteStringUtf8("Hello, World!")</c></example>
		public int WriteStringUtf8(string? value)
		{
			if (string.IsNullOrEmpty(value)) return 0;

			// In order to estimate the required capacity, we try to guess for very small strings, but compute the actual value for larger strings,
			// so that we don't waste too much memory (up to 6x the string length in the worst case scenario)
			var buffer = EnsureBytes(value.Length > 128
				? Encoding.UTF8.GetByteCount(value)
				: Encoding.UTF8.GetMaxByteCount(value.Length));

			int p = this.Position;
			int n = Encoding.UTF8.GetBytes(s: value, charIndex: 0, charCount: value.Length, bytes: buffer, byteIndex: p);
			this.Position = checked(p + n);
			return n;
		}

		/// <summary>Write a string using UTF-8</summary>
		/// <returns>Number of bytes written</returns>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public int WriteStringUtf8(char[] chars, int offset, int count)
		{
			return WriteStringUtf8(new ReadOnlySpan<char>(chars, offset, count));
		}

		/// <summary>Writes a string using UTF-8</summary>
		/// <returns>Number of bytes written</returns>
		public int WriteStringUtf8(ReadOnlySpan<char> chars)
		{
			int count = chars.Length;
			if (count == 0) return 0;

			unsafe
			{
				fixed (char* inp = &MemoryMarshal.GetReference(chars))
				{
					// For short strings, assume 6 bytes per character, and only do real work for "long" strings.
					var buffer = EnsureBytes(count > 128
						? Encoding.UTF8.GetByteCount(inp, count)
						: Encoding.UTF8.GetMaxByteCount(count));

					int p = this.Position;
					fixed (byte* outp = &buffer[p])
					{
						int n = Encoding.UTF8.GetBytes(chars: inp, charCount: count, bytes: outp, byteCount: buffer.Length - p);
						this.Position = checked(p + n);
						return n;
					}
				}
			}
		}

		/// <summary>Writes a character using UTF-8</summary>
		/// <returns>Number of bytes written</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int WriteStringUtf8(char value)
		{
			if (value < 0x80)
			{
				WriteByte((byte) value);
				return 1;
			}
			return WriteStringUtf8Slow(value);
		}

		private int WriteStringUtf8Slow(char value)
		{
			int p = this.Position;
			if (!Utf8Encoder.TryWriteCodePoint(ref this, (UnicodeCodePoint)value))
			{
				throw FailInvalidUtf8CodePoint();
			}
			return this.Position - p;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailInvalidUtf8CodePoint()
		{
			return new DecoderFallbackException("Failed to encode invalid Unicode CodePoint into UTF-8");
		}

		/// <summary>Write a string that only contains ASCII</summary>
		/// <param name="value">String with characters only in the 0..127 range</param>
		/// <remarks>Faster than <see cref="WriteString(string, Encoding)"/> when writing Magic Strings or ascii keywords</remarks>
		/// <returns>Number of bytes written</returns>
		public int WriteStringAscii(string? value)
		{
			if (string.IsNullOrEmpty(value)) return 0;

			var buffer = EnsureBytes(value.Length);
			int p = this.Position;
			foreach (var c in value)
			{
				buffer[p++] = (byte) c;
			}
			this.Position = p;
			return value.Length;
		}

		/// <summary>Writes a base 10 integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBase10(int value)
		{
			if ((uint) value <= 9)
			{
				WriteByte('0' + value);
			}
			else
			{
				WriteBase10Slow(value);
			}
		}

		/// <summary>Writes a base 10 integer</summary>
		public void WriteBase10(long value)
		{
			switch ((ulong) value)
			{
				case <= 9:
				{
					WriteByte('0' + (int) value);
					break;
				}
				case <= int.MaxValue:
				{
					WriteBase10Slow((int) value);
					break;
				}
				default:
				{
					WriteBase10Slower(value);
					break;
				}
			}
		}

		/// <summary>Writes a base 10 integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBase10(uint value)
		{
			if (value <= 9)
			{
				WriteByte('0' + (int) value);
			}
			else
			{
				WriteBase10Slow(value);
			}
		}

		/// <summary>Writes a base 10 integer</summary>
		public void WriteBase10(ulong value)
		{
			if (value <= 9)
			{
				WriteByte('0' + (int) value);
			}
			else if (value <= uint.MaxValue)
			{
				WriteBase10Slow((uint) value);
			}
			else
			{
				WriteBase10Slower(value);
			}
		}

		private void WriteBase10Slow(int value)
		{
			if (value < 0)
			{ // negative numbers
				if (value == int.MinValue)
				{ // cannot do Abs(MinValue), so special case for this one
					WriteBytes("-2147483648"u8);
					return;
				}
				value = -value;
				if (value < 10)
				{
					WriteBytes(
						(byte) '-',
						(byte) ('0' + value)
					);
					return;
				}
				WriteByte('-');
			}

			//note: 0..9 already handled before
			if (value < 100)
			{
				WriteBytes(
					(byte) ('0' + (value / 10)),
					(byte) ('0' + (value % 10))
				);
			}
			else if (value < 1_000)
			{
				WriteBytes(
					(byte) ('0' + (value / 100)),
					(byte) ('0' + (value / 10) % 10),
					(byte) ('0' + (value % 10))
				);
			}
			else if (value < 10_000)
			{
				WriteBytes(
					(byte) ('0' + (value / 1000)),
					(byte) ('0' + (value / 100) % 10),
					(byte) ('0' + (value / 10) % 10),
					(byte) ('0' + (value % 10))
				);
			}
			else if (value < 100_000)
			{
				WriteBytes(
					(byte) ('0' + (value / 10000)),
					(byte) ('0' + (value / 1000) % 10),
					(byte) ('0' + (value / 100) % 10),
					(byte) ('0' + (value / 10) % 10),
					(byte) ('0' + (value % 10))
				);
			}
			else
			{
				WriteBase10Slower(value);
			}
		}

		private void WriteBase10Slower(int value)
		{
#if NET8_0_OR_GREATER
			// max number of "digits" is 11 for int.MinValue (includes the leading '-')
			var buffer = GetSpan(11);

			value.TryFormat(buffer, out int n, provider: CultureInfo.InvariantCulture);
			this.Position += n;
#else
			// unfortunately, we will have to allocate some memory
			WriteStringAscii(value.ToString(CultureInfo.InvariantCulture));
#endif
		}

		private void WriteBase10Slower(long value)
		{
#if NET8_0_OR_GREATER
			// max number of "digits" is 20 for long.MinValue (includes the leading '-')
			var buffer = GetSpan(20);

			value.TryFormat(buffer, out int n, provider: CultureInfo.InvariantCulture);
			this.Position += n;
#else
			// unfortunately, we will have to allocate some memory
			WriteStringAscii(value.ToString(CultureInfo.InvariantCulture));
#endif
		}

		private void WriteBase10Slow(uint value)
		{
			// value is already >= 10
			if (value < 100)
			{
				WriteBytes(
					(byte) ('0' + (value / 10)),
					(byte) ('0' + (value % 10))
				);
			}
			else if (value < 1_000)
			{
				WriteBytes(
					(byte) ('0' + (value / 100)),
					(byte) ('0' + (value / 10) % 10),
					(byte) ('0' + (value % 10))
				);
			}
			else if (value < 10_000)
			{
				WriteBytes(
					(byte) ('0' + (value / 1000)),
					(byte) ('0' + (value / 100) % 10),
					(byte) ('0' + (value / 10) % 10),
					(byte) ('0' + (value % 10))
				);
			}
			else if (value < 100_000)
			{
				WriteBytes(
					(byte) ('0' + (value / 10000)),
					(byte) ('0' + (value / 1000) % 10),
					(byte) ('0' + (value / 100) % 10),
					(byte) ('0' + (value / 10) % 10),
					(byte) ('0' + (value % 10))
				);
			}
			else
			{
				WriteBase10Slower(value);
			}
		}

		private void WriteBase10Slower(ulong value)
		{
#if NET8_0_OR_GREATER
			// max number of "digits" is 20 for ulong.MaxValue
			var buffer = GetSpan(20);

			value.TryFormat(buffer, out int n, provider: CultureInfo.InvariantCulture);
			this.Position += n;
#else
			WriteStringAscii(value.ToString(CultureInfo.InvariantCulture));
#endif
		}

		#endregion

		#region Patching

		#region Bytes...

		/// <summary>Overwrite a section of the buffer that was already written, with the specified data</summary>
		/// <param name="index">Offset from the start of the buffer where to start replacing</param>
		/// <param name="data">Data that will overwrite the buffer at the specified <paramref name="index"/></param>
		/// <remarks>You must ensure that replaced section does not overlap with the current position!</remarks>
		public void PatchBytes(int index, Slice data)
		{
			if (index + data.Count > this.Position) throw ThrowHelper.IndexOutOfRangeException();
			data.Span.CopyTo(this.Buffer.AsSpan(index));
		}

		/// <summary>Overwrite a section of the buffer that was already written, with the specified data</summary>
		/// <param name="index">Offset from the start of the buffer where to start replacing</param>
		/// <param name="data">Data that will overwrite the buffer at the specified <paramref name="index"/></param>
		/// <remarks>You must ensure that replaced section does not overlap with the current position!</remarks>
		public void PatchBytes(int index, ReadOnlySpan<byte> data)
		{
			if (index + data.Length > this.Position) throw ThrowHelper.IndexOutOfRangeException();
			data.CopyTo(this.Buffer.AsSpan(index));
		}

		/// <summary>Overwrite a section of the buffer that was already written, with the specified data</summary>
		/// <param name="index">Offset from the start of the buffer where to start replacing</param>
		/// <param name="data">Data that will overwrite the buffer at the specified <paramref name="index"/></param>
		/// <remarks>You must ensure that replaced section does not overlap with the current position!</remarks>
		public void PatchBytes(int index, ReadOnlyMemory<byte> data)
		{
			if (index + data.Length > this.Position) throw ThrowHelper.IndexOutOfRangeException();
			data.Span.CopyTo(this.Buffer.AsSpan(index));
		}

		/// <summary>Overwrite a section of the buffer that was already written, with the specified data</summary>
		/// <remarks>You must ensure that replaced section does not overlap with the current position!</remarks>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public void PatchBytes(int index, byte[] buffer, int offset, int count)
		{
			if (index + count > this.Position) throw ThrowHelper.IndexOutOfRangeException();
			buffer.AsSpan(offset, count).CopyTo(this.Buffer.AsSpan(index));
		}

		#endregion

		#region 8-bits...

		/// <summary>Overwrite a byte of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchByte(int offset, byte value)
		{
			var buffer = this.Buffer;
			if ((uint) offset >= this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			buffer[offset] = value;
		}

		/// <summary>Overwrite a byte of the buffer that was already written</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchByte(int offset, sbyte value)
		{
			var buffer = this.Buffer;
			if ((uint) offset >= this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			buffer[offset] = (byte) value;
		}

		/// <summary>Overwrite a byte of the buffer that was already written</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch</param>
		/// <param name="value">Value that contains the byte to replace. The upper 24-bits are ignored.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchByte(int offset, int value)
		{
			//note: convenience method, because C# compiler likes to produce 'int' when combining bits together
			var buffer = this.Buffer;
			if ((uint) offset >= this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			buffer[offset] = (byte) value;
		}

		#endregion

		#region 16-bits...

		/// <summary>Overwrites a 16-bit location in the buffer, using little-endian encoding</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch</param>
		/// <param name="value">Value that contains the byte to replace. The upper 24-bits are ignored.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchInt16(int offset, short value)
		{
			var buffer = this.Buffer;
			if (offset + 2 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(offset, 2), value);
		}

		/// <summary>Overwrites a 16-bit location in the buffer, using little-endian encoding</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchUInt16(int offset, ushort value)
		{
			var buffer = this.Buffer;
			if (offset + 2 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset, 2), value);
		}

		/// <summary>Overwrites a 16-bit location in the buffer, using big-endian encoding</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchInt16BE(int offset, short value)
		{
			var buffer = this.Buffer;
			if (offset + 2 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			BinaryPrimitives.WriteInt16BigEndian(buffer.AsSpan(offset, 2), value);
		}

		/// <summary>Overwrites a 16-bit location in the buffer, using big-endian encoding</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchUInt16BE(int offset, ushort value)
		{
			var buffer = this.Buffer;
			if (offset + 2 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset, 2), value);
		}

		#endregion

		#region 24-bits...

		/// <summary>Overwrites a 24-bit location in the buffer, using little-endian encoding</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location. The upper 8-bits are ignored.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchInt24(int offset, int value)
		{
			var buffer = this.Buffer;
			if (offset + 3 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &buffer[offset])
				{
					UnsafeHelpers.StoreInt24LE(ptr, value);
				}
			}
		}

		/// <summary>Overwrites a 24-bit location in the buffer, using little-endian encoding</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location. The upper 8-bits are ignored.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchUInt24(int offset, uint value)
		{
			var buffer = this.Buffer;
			if (offset + 3 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &buffer[offset])
				{
					UnsafeHelpers.StoreUInt24LE(ptr, value);
				}
			}
		}

		/// <summary>Overwrites a 24-bit location in the buffer, using big-endian encoding</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location. The upper 8-bits are ignored.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchInt24BE(int offset, int value)
		{
			var buffer = this.Buffer;
			if (offset + 3 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &buffer[offset])
				{
					UnsafeHelpers.StoreInt24BE(ptr, value);
				}
			}
		}

		/// <summary>Overwrites a 24-bit location in the buffer, using big-endian encoding</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location. The upper 8-bits are ignored.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchUInt24BE(int offset, uint value)
		{
			var buffer = this.Buffer;
			if (offset + 3 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &buffer[offset])
				{
					UnsafeHelpers.StoreUInt24BE(ptr, value);
				}
			}
		}

		#endregion

		#region 32-bits...

		/// <summary>Overwrites a 32-bit location in the buffer, using little-endian encoding</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchInt32(int offset, int value)
		{
			var buffer = this.Buffer;
			if (offset + 4 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset, 4), value);
		}

		/// <summary>Overwrites a 32-bit location in the buffer, using little-endian encoding</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchUInt32(int offset, uint value)
		{
			var buffer = this.Buffer;
			if (offset + 4 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset, 4), value);
		}

		/// <summary>Overwrites a 32-bit location in the buffer, using big-endian encoding</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchInt32BE(int offset, int value)
		{
			var buffer = this.Buffer;
			if (offset + 4 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(offset, 4), value);
		}

		/// <summary>Overwrites a 32-bit location in the buffer, using big-endian encoding</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchUInt32BE(int offset, uint value)
		{
			var buffer = this.Buffer;
			if (offset + 4 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset, 4), value);
		}

		#endregion

		#region 64-bits...

		/// <summary>Overwrites a 64-bit location in the buffer, using little-endian encoding</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchInt64(int offset, long value)
		{
			var buffer = this.Buffer;
			if (offset + 8 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(offset, 8), value);
		}

		/// <summary>Overwrites a 64-bit location in the buffer, using little-endian encoding</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchUInt64(int offset, ulong value)
		{
			var buffer = this.Buffer;
			if (offset + 8 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(offset, 8), value);
		}

		/// <summary>Overwrites a 64-bit location in the buffer, using big-endian encoding</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchInt64BE(int offset, long value)
		{
			var buffer = this.Buffer;
			if (offset + 8 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			BinaryPrimitives.WriteInt64BigEndian(buffer.AsSpan(offset, 8), value);
		}

		/// <summary>Overwrites a 64-bit location in the buffer, using big-endian encoding</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchUInt64BE(int offset, ulong value)
		{
			var buffer = this.Buffer;
			if (offset + 8 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			BinaryPrimitives.WriteUInt64BigEndian(buffer.AsSpan(offset, 8), value);
		}

		#endregion

		#region 128-bits...

#if NET8_0_OR_GREATER

		/// <summary>Overwrites a 128-bit location in the buffer, using little-endian encoding</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchInt128(int offset, Int128 value)
		{
			var buffer = this.Buffer;
			if ((long) offset + Unsafe.SizeOf<Int128>() > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			BinaryPrimitives.WriteInt128LittleEndian(buffer.AsSpan(offset, Unsafe.SizeOf<Int128>()), value);
		}

		/// <summary>Overwrites a 128-bit location in the buffer, using little-endian encoding</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchUInt128(int offset, UInt128 value)
		{
			var buffer = this.Buffer;
			if ((long) offset + Unsafe.SizeOf<UInt128>() > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			BinaryPrimitives.WriteUInt128LittleEndian(buffer.AsSpan(offset, Unsafe.SizeOf<UInt128>()), value);
		}

		/// <summary>Overwrites a 128-bit location in the buffer, using big-endian encoding</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchInt128BE(int offset, Int128 value)
		{
			var buffer = this.Buffer;
			if ((long) offset + Unsafe.SizeOf<Int128>() > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			BinaryPrimitives.WriteInt128BigEndian(buffer.AsSpan(offset, Unsafe.SizeOf<Int128>()), value);
		}

		/// <summary>Overwrites a 128-bit location in the buffer, using big-endian encoding</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchUInt128BE(int offset, UInt128 value)
		{
			var buffer = this.Buffer;
			if ((long) offset + Unsafe.SizeOf<UInt128>() > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			BinaryPrimitives.WriteUInt128BigEndian(buffer.AsSpan(offset, Unsafe.SizeOf<UInt128>()), value);
		}

#endif

		#region UUIDs

		/// <summary>Overwrites a 128-bit location in the buffer</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchUuid128(int offset, Uuid128 value)
		{
			var buffer = this.Buffer;
			if ((long) offset + Unsafe.SizeOf<Uuid128>() > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			value.WriteTo(buffer.AsSpan(offset, Unsafe.SizeOf<Uuid128>()));
		}

		/// <summary>Overwrites a 96-bit location in the buffer</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchUuid96(int offset, Uuid96 value)
		{
			var buffer = this.Buffer;
			if ((long) offset + Unsafe.SizeOf<Uuid96>() > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			value.WriteTo(buffer.AsSpan(offset, Unsafe.SizeOf<Uuid96>()));
		}

		/// <summary>Overwrites a 96-bit location in the buffer</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchUuid80(int offset, Uuid80 value)
		{
			var buffer = this.Buffer;
			if ((long) offset + Unsafe.SizeOf<Uuid80>() > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			value.WriteTo(buffer.AsSpan(offset, Unsafe.SizeOf<Uuid80>()));
		}

		/// <summary>Overwrites a 96-bit location in the buffer</summary>
		/// <param name="offset">Offset, from the start of the buffer, of the location to patch.</param>
		/// <param name="value">Value to insert at this location.</param>
		/// <remarks>You must ensure that replaced location is before the current position!</remarks>
		public void PatchUuid64(int offset, Uuid64 value)
		{
			var buffer = this.Buffer;
			if ((long) offset + Unsafe.SizeOf<Uuid64>() > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			value.WriteTo(buffer.AsSpan(offset, Unsafe.SizeOf<Uuid64>()));
		}

		#endregion

		#endregion

		#endregion

		/// <summary>Return the remaining capacity in the current underlying buffer</summary>
		public readonly int RemainingCapacity
		{
			[Pure]
			get
			{
				var buffer = this.Buffer;
				if (buffer == null || this.Position >= buffer.Length) return 0;
				return buffer.Length - this.Position;
			}
		}

		/// <summary>Ensures that we can fit the specified amount of data at the end of the buffer</summary>
		/// <param name="count">Number of bytes that will be written</param>
		/// <remarks>If the buffer is too small, it will be resized, and all previously written data will be copied</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public byte[] EnsureBytes(int count)
		{
			Contract.Debug.Requires(count >= 0);
			var buffer = this.Buffer;
			var pos = this.Position;
			var newPos = pos + count;
			if (buffer == null || newPos > buffer.Length)
			{
				buffer = GrowBuffer(ref this.Buffer, pos, newPos, this.Pool);
				Contract.Debug.Ensures(buffer != null && buffer.Length >= newPos);
			}
			return buffer;
		}

		/// <inheritdoc />
		public void Advance(int count)
		{
			int newPos = checked(this.Position + count);
			if (newPos > this.Capacity) throw ErrorCannotAdvancePastEndOfBuffer();
			this.Position = newPos;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException ErrorCannotAdvancePastEndOfBuffer()
		{
			return new("Cannot advance past the end of the allocated buffer.");
		}

		/// <summary>Allocates a buffer at the current cursor location, but do not advance the cursor</summary>
		/// <param name="minCapacity">Minimum allocated capacity</param>
		/// <returns>Buffer located at the current cursor position, and of size at least equal to <paramref name="minCapacity"/></returns>
		/// <remarks>
		/// <para>After filling the returned buffer, the caller MUST advance the cursor manually!</para>
		/// <para>This is intended to be used in combination with methods like <see cref="ISpanFormattable.TryFormat"/></para>
		/// </remarks>
		public Span<byte> GetSpan(int minCapacity)
		{
			Contract.Debug.Requires(minCapacity >= 0);
			var buffer = this.Buffer;
			int pos = this.Position;
			int newPos = pos + minCapacity;
			if (buffer == null || newPos > buffer.Length)
			{
				buffer = GrowBuffer(ref this.Buffer, pos, newPos, this.Pool);
				Paranoid.Ensures(buffer != null && buffer.Length >= newPos);
			}
			return buffer.AsSpan(pos);
		}
		
		/// <summary>Allocates a buffer at the current cursor location, but do not advance the cursor</summary>
		/// <param name="minCapacity">Minimum allocated capacity</param>
		/// <returns>Buffer located at the current cursor position, and of size at least equal to <paramref name="minCapacity"/></returns>
		/// <remarks>
		/// <para>After filling the returned buffer, the caller MUST advance the cursor manually!</para>
		/// <para>This is intented to be used in combination with methods like <see cref="ISpanFormattable.TryFormat"/></para>
		/// </remarks>
		public Memory<byte> GetMemory(int minCapacity)
		{
			Contract.Debug.Requires(minCapacity >= 0);
			var buffer = this.Buffer;
			int pos = this.Position;
			int newPos = pos + minCapacity;
			if (buffer == null || newPos > buffer.Length)
			{
				buffer = GrowBuffer(ref this.Buffer, pos, newPos, this.Pool);
				Contract.Debug.Ensures(buffer != null && buffer.Length >= newPos);
			}
			return buffer.AsMemory(pos);
		}

		/// <summary>Allocates a buffer at the current cursor location, and advance the cursor</summary>
		/// <param name="count">Number of bytes to allocate</param>
		/// <returns>Buffer located at the current cursor position, and of size equal to <paramref name="count"/></returns>
		/// <remarks>
		/// <para>The buffer must be completely filled, otherwise any pre-existing data in the buffer will leak!</para>
		/// </remarks>
		public Span<byte> AllocateSpan(int count)
		{
			Contract.Debug.Requires(count >= 0);
			var buffer = this.Buffer;
			int pos = this.Position;
			int newPos = checked(pos + count);
			if (buffer == null || newPos > buffer.Length)
			{
				buffer = GrowBuffer(ref this.Buffer, pos, newPos, this.Pool);
				Contract.Debug.Ensures(buffer != null && buffer.Length >= newPos);
			}
			this.Position = newPos;
			return buffer.AsSpan(pos, count);
		}

		/// <summary>Allocates a buffer at the current cursor location, and advance the cursor</summary>
		/// <param name="count">Number of bytes to allocate</param>
		/// <returns>Buffer located at the current cursor position, and of size equal to <paramref name="count"/></returns>
		/// <remarks>
		/// <para>The buffer must be completely filled, otherwise any pre-existing data in the buffer will leak!</para>
		/// </remarks>
		public Memory<byte> AllocateMemory(int count)
		{
			Contract.Debug.Requires(count >= 0);
			var buffer = this.Buffer;
			int pos = this.Position;
			int newPos = checked(pos + count);
			if (buffer == null || newPos > buffer.Length)
			{
				buffer = GrowBuffer(ref this.Buffer, pos, newPos, this.Pool);
				Contract.Debug.Ensures(buffer != null && buffer.Length >= newPos);
			}
			this.Position = newPos;
			return buffer.AsMemory(pos, count);
		}

		/// <summary>Ensures that we can fit the specified amount of data at the end of the buffer</summary>
		/// <param name="count">Number of bytes that will be written</param>
		/// <param name="pool"></param>
		/// <remarks>If the buffer is too small, it will be resized, and all previously written data will be copied</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public byte[] EnsureBytes(int count, ArrayPool<byte> pool)
		{
			Contract.Debug.Requires(count >= 0);
			var buffer = this.Buffer;
			var pos = this.Position;
			var newPos = pos + count;
			if (buffer == null || newPos > buffer.Length)
			{
				buffer = GrowBuffer(ref this.Buffer, pos, newPos, pool);
				Contract.Debug.Ensures(buffer != null && buffer.Length >= this.Position + count);
			}
			return buffer;
		}

		/// <summary>Ensures that we can fit the specified  amount of data at the end of the buffer</summary>
		/// <param name="count">Number of bytes that will be written</param>
		/// <remarks>If the buffer is too small, it will be resized, and all previously written data will be copied</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public byte[] EnsureBytes(uint count)
		{
			return EnsureBytes(checked((int) count));
		}

		/// <summary>Ensures that we can fit data at a specific offset in the buffer</summary>
		/// <param name="offset">Offset into the buffer (from the start)</param>
		/// <param name="count">Number of bytes that will be written at this offset</param>
		/// <remarks>If the buffer is too small, it will be resized, and all previously written data will be copied</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureOffsetAndSize(int offset, int count)
		{
			Contract.Debug.Requires(offset >= 0 && count >= 0);
			if (checked(offset + count) > (this.Buffer?.Length ?? 0))
			{
				GrowBuffer(ref this.Buffer, this.Position, offset + count, this.Pool);
			}
		}

		/// <summary>Resize a buffer by doubling its capacity</summary>
		/// <param name="buffer">Reference to the variable holding the buffer to create/resize. If null, a new buffer will be allocated. If not, the content of the buffer will be copied into the new buffer.</param>
		/// <param name="keep"></param>
		/// <param name="minimumCapacity">Minimum guaranteed buffer size after resizing.</param>
		/// <param name="pool">Optional pool used by this buffer</param>
		/// <remarks>The buffer will be resized to the maximum between the previous size multiplied by 2, and <paramref name="minimumCapacity"/>. The capacity will always be rounded to a multiple of 16 to reduce memory fragmentation</remarks>
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static byte[] GrowBuffer(
			ref byte[]? buffer,
			int keep,
			int minimumCapacity,
			ArrayPool<byte>? pool
		)
		{
			Contract.Debug.Requires(minimumCapacity >= 0);

			// double the size of the buffer, or use the minimum required
			long newSize = Math.Max(buffer == null ? 0 : (((long) buffer.Length) << 1), minimumCapacity);

			// .NET (as of 4.5) cannot allocate an array with more than 2^31 - 1 items...
			if (newSize > 0x7fffffffL) throw FailCannotGrowBuffer();

			if (pool == null)
			{ // use the heap
				int size = BitHelpers.AlignPowerOfTwo((int) newSize, powerOfTwo: 16); // round up to 16 bytes, to reduce fragmentation
				if (buffer == null || buffer.Length == 0)
				{ // first buffer
					buffer = new byte[size];
				}
				else
				{ // resize and copy
					var tmp = new byte[size];
					buffer.AsSpan(0, keep).CopyTo(tmp);
					buffer = tmp;
				}
			}
			else
			{ // use the pool to resize the buffer
				int size = Math.Max((int) newSize, 64); // with a pool, we can ask for more bytes initially

				if (buffer == null || buffer.Length == 0)
				{ // first buffer
					buffer = pool.Rent(size);
				}
				else
				{ // resize and copy
					var tmp = pool.Rent(size);
					buffer.AsSpan(0, keep).CopyTo(tmp);
					pool.Return(buffer);
					buffer = tmp;
				}
			}
			return buffer;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailCannotGrowBuffer()
		{
#if DEBUG
			// If you breakpoint here, that means that you probably have an unchecked maximum buffer size, or a runaway while(..) { append(..) } code in your layer code !
			// => you should ALWAYS ensure a reasonable maximum size of your allocations !
			if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
			// note: some methods in the BCL do throw an OutOfMemoryException when attempting to allocated more than 2^31
			return new OutOfMemoryException("Buffer cannot be resized, because it would exceed the maximum allowed size");
		}

		#region ISliceSerializable...

		/// <summary>Appends the content of this writer to end of another writer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteTo(ref SliceWriter writer)
		{
			writer.WriteBytes(ToSpan());
		}

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		bool ISpanEncodable.TryGetSpan(out ReadOnlySpan<byte> span)
		{
			var buffer = this.Buffer;
			var pos = this.Position;
			if (buffer is null || pos == 0)
			{ // empty buffer
				span = default;
			}
			else
			{
				span = new(buffer, 0, pos);
			}
			return true;
		}

		/// <inheritdoc />
		bool ISpanEncodable.TryGetSizeHint(out int sizeHint)
		{
			sizeHint = this.Position;
			return true;
		}

		/// <inheritdoc />
		bool ISpanEncodable.TryEncode(Span<byte> destination, out int bytesWritten)
		{
			var pos = this.Position;
			if (destination.Length < pos)
			{
				bytesWritten = 0;
				return false;
			}
			this.Buffer.AsSpan(0, pos).CopyTo(destination);
			bytesWritten = pos;
			return true;
		}

		#endregion

		[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
		private sealed class DebugView
		{

			public DebugView(SliceWriter writer)
			{
				this.Data = writer.ToSlice();
				this.Position = writer.Position;
				this.Capacity = writer.Capacity;
			}

			public string Hex => this.Data.ToHexString(' ');

			public Slice Data { get; }

			public int Position { get; }

			public int Capacity { get; }

		}

	}

}
