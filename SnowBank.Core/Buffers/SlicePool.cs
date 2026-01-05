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
	using System.Globalization;

	/// <summary>Slice generator that use one or more underlying buffers to store the produced bytes, in order to reduce memory allocations.</summary>
	/// <remarks>
	/// This is faster than a SliceWriter when writing a lot of keys that will not survive the lifetime of the pool itself.
	/// Warning: Since the pool can reuse its internal buffer between sessions, this breaks the immutability contract for long-lived Slices, and may introduce corruption if not used properly.
	/// Warning: instances of this type are NOT thread-safe. In multithreaded contexts, each thread should either use locking, or have its own pool instance.
	/// </remarks>
	[Obsolete("Use ISliceBufferWriter or ISliceAllocator instead")]
	public sealed class SlicePool //REVIEW: change the name into something like "SliceAllocator", so that we can use "SlicePool" in the same meaning as MemoryPool or ArrayPool?
	{
		//note: a SliceWriter only keeps a single buffer (resized as needed) that remembers all the slices produced, while a SlicePool will allocate new buffers without copying.
		// => SliceWriter should be used when formatting a binary protocol, where we need to complete buffer (in order to write it to disk or to a socket)
		// => SlicePool should be used for short-lived slices that are produced and then consumed immediately, or that have the same lifetime as the pool itself.
		// => SliceBuffer keeps a reference to all the buffers that were used, while SlicePool only keeps a reference on the last one!

		/// <summary>Default initial capacity for a slice pool</summary>
		internal const int DefaultCapacity = 16;

		/// <summary>Stores the current buffer page</summary>
		internal byte[] Buffer;

		/// <summary>Cursor to the next free byte in the current buffer page</summary>
		internal int Position;

		/// <summary>Create a new slice pool with the default initial capacity</summary>
		public SlicePool()
			: this(DefaultCapacity)
		{ }

		/// <summary>Create a new slice pool using the provided initial buffer</summary>
		public SlicePool(byte[] buffer)
		{
			Contract.NotNull(buffer);
			this.Buffer = buffer;
		}

		/// <summary>Create a new slice pool using the specific initial capacity</summary>
		public SlicePool(int capacity)
		{
			Contract.Positive(capacity);

			this.Buffer = [ ];
			EnsureBytes(capacity);
		}

		/// <summary>Reset the slice pool</summary>
		/// <remarks>Any slice previously created from this pool should not be in use anymore, or they will be overwritten!</remarks>
		public void Reset()
		{
			this.Position = 0;
		}

		/// <summary>Reset the slice pool, and ensure that it can hold a specific capacity</summary>
		/// <remarks>Any slice previously created from this pool should not be in use anymore, or they will be overwritten!</remarks>
		public void Reset(int capacity)
		{
			Contract.Positive(capacity);

			this.Position = 0;
			EnsureBytes(capacity);
		}

		/// <summary>Make sure that the current buffer page can hold a specified free capacity</summary>
		/// <param name="capacity">Minimum number of bytes that must fit in the buffer page</param>
		/// <returns>Current buffer page (if it had enough free space), or a newly allocated page</returns>
		/// <remarks>If a new page is allocated, the value of <see cref="Position"/> is updated accordingly. This means that you SHOULD NOT read the value of <see cref="Position"/> before a call to <see cref="EnsureBytes"/>, or read it again after.</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)] // potentially called for each byte written, so should be inlined by the caller
		internal byte[] EnsureBytes(int capacity)
		{
			var buffer = this.Buffer;
			return capacity <= buffer.Length - this.Position ? buffer : NewPage(capacity);
		}

		/// <summary>Allocate a new buffer page that is large enough for the specified capacity</summary>
		[MethodImpl(MethodImplOptions.NoInlining)] // make sure this does not get inlined by mistake
		private byte[] NewPage(int capacity)
		{
			long newCapacity = Math.Max(this.Buffer.Length, DefaultCapacity);
			while (newCapacity < capacity) newCapacity <<= 1;
			if (newCapacity > int.MaxValue) newCapacity = capacity;
			//TODO: better handling of max values!

			this.Buffer = new byte[newCapacity];
			this.Position = 0;
			return this.Buffer;
		}

		/// <summary>Allocate a new slice from the pool</summary>
		/// <param name="count">Size of the slice</param>
		/// <returns>Slice that maps the allocated region</returns>
		/// <remarks>There is NO guarantee that the allocated slice is filled with zero! The caller should manually clear the slice before using it, if necessary</remarks>
		public Span<byte> AllocateSpan(int count)
		{
			var buffer = EnsureBytes(count);
			int p = this.Position;
			this.Position = p + count;
			return buffer.AsSpan(p, count);
		}

		#region 8 bits

		//note: single byte slices are created from the static ByteSprite in Slice, and not from the pool's buffer

		/// <summary>Return a 1-byte slice from an 8-bit integer</summary>
		/// <remarks>Slices produced by this method ARE ordered lexicographically</remarks>
		/// <example>Fixed8(0x12) => { 0x12 }</example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Fixed8(byte value)
		{
			return Slice.FromByte(value);
		}

		/// <summary>Return a 1-byte slice from an 8-bit signed integer</summary>
		/// <remarks>Slices produced by this method ARE ordered lexicographically</remarks>
		/// <example>Fixed8(0x12) => { 0x12 }</example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Fixed8(sbyte value)
		{
			return Slice.FromByte((byte) value);
		}

		#endregion

		#region 16 bits

		#region Little-Endian

		internal int WriteFixed16(ushort value)
		{
			var buffer = EnsureBytes(2);
			int p = this.Position;
			buffer[p] = (byte)value;
			buffer[p + 1] = (byte)(value >> 8);
			this.Position = p + 2;
			return p;
		}

		/// <summary>Return a fixed size, little-endian encoded, 16-bit signed integer</summary>
		/// <remarks>Slices produced by this method are NOT ordered lexicographically</remarks>
		/// <example>Fixed16(0x1234) => { 0x34 0x12 }</example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Fixed16(short value)
		{
			//perf: it is faster to inline the call to Slice.ctor
			int p = WriteFixed16((ushort)value);
			return new Slice(this.Buffer, p, 2);
		}

		/// <summary>Return a fixed size, little-endian encoded, 16-bit unsigned integer</summary>
		/// <remarks>Slices produced by this method are NOT ordered lexicographically</remarks>
		/// <example>Fixed16(0x1234) => { 0x34 0x12 }</example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Fixed16(ushort value)
		{
			//perf: it is faster to inline the call to Slice.ctor
			int p = WriteFixed16(value);
			return new Slice(this.Buffer, p, 2);
		}

		#endregion

		#region Big-Endian

		internal int WriteFixed16BE(ushort value)
		{
			var buffer = EnsureBytes(2);
			int p = this.Position;
			buffer[p] = (byte)(value >> 8);
			buffer[p + 1] = (byte)value;
			this.Position = p + 2;
			return p;
		}

		/// <summary>Return a fixed size, big-endian encoded, 16-bit signed integer</summary>
		/// <remarks>Slices produced by this method ARE ordered lexicographically</remarks>
		/// <example>Fixed16BE(0x1234) => { 0x12 0x34 }</example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Fixed16BE(short value)
		{
			//perf: it is faster to inline the call to Slice.ctor
			int p = WriteFixed16BE((ushort)value);
			return new Slice(this.Buffer, p, 2);
		}

		/// <summary>Return a fixed size, big-endian encoded, 16-bit signed integer</summary>
		/// <remarks>Slices produced by this method ARE ordered lexicographically</remarks>
		/// <example>Fixed16BE(0x1234) => { 0x12 0x34 }</example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Fixed16BE(ushort value)
		{
			//perf: it is faster to inline the call to Slice.ctor
			int p = WriteFixed16BE(value);
			return new Slice(this.Buffer, p, 2);
		}

		#endregion

		#endregion

		#region 32 bits

		#region Little-Endian

		internal int WriteFixed32(uint value)
		{
			var buffer = EnsureBytes(4);
			int p = this.Position;
			buffer[p] = (byte)value;
			buffer[p + 1] = (byte)(value >> 8);
			buffer[p + 2] = (byte)(value >> 16);
			buffer[p + 3] = (byte)(value >> 24);
			this.Position = p + 4;
			return p;
		}

		/// <summary>Return a fixed size, little-endian encoded, 32-bit signed integer</summary>
		/// <remarks>Slices produced by this method are NOT ordered lexicographically</remarks>
		/// <example>Fixed32(0x12345678) => { 0x78 0x56 0x34 0x12 }</example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Fixed32(int value)
		{
			//perf: it is faster to inline the call to Slice.ctor
			int p = WriteFixed32((uint)value);
			return new Slice(this.Buffer, p, 4);
		}

		/// <summary>Return a fixed size, little-endian encoded, 32-bit unsigned integer</summary>
		/// <remarks>Slices produced by this method are NOT ordered lexicographically</remarks>
		/// <example>Fixed32(0x12345678) => { 0x78 0x56 0x34 0x12 }</example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Fixed32(uint value)
		{
			//perf: it is faster to inline the call to Slice.ctor
			int p = WriteFixed32(value);
			return new Slice(this.Buffer, p, 4);
		}

		#endregion

		#region Big-Endian

		internal int WriteFixed32BE(uint value)
		{
			var buffer = EnsureBytes(4);
			int p = this.Position;
			buffer[p] = (byte)(value >> 24);
			buffer[p + 1] = (byte)(value >> 16);
			buffer[p + 2] = (byte)(value >> 8);
			buffer[p + 3] = (byte)value;
			this.Position = p + 4;
			return p;
		}

		/// <summary>Return a fixed size, big-endian encoded, 32-bit signed integer</summary>
		/// <remarks>Slices produced by this method ARE ordered lexicographically</remarks>
		/// <example>Fixed32BE(0x12345678) => { 0x12 0x34 0x56 0x78 }</example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Fixed32BE(int value)
		{
			//perf: it is faster to inline the call to Slice.ctor
			int p = WriteFixed32BE((uint)value);
			return new Slice(this.Buffer, p, 4);
		}

		/// <summary>Return a fixed size, big-endian encoded, 32-bit unsigned integer</summary>
		/// <remarks>Slices produced by this method ARE ordered lexicographically</remarks>
		/// <example>Fixed32BE(0x12345678) => { 0x12 0x34 0x56 0x78 }</example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Fixed32BE(uint value)
		{
			//perf: it is faster to inline the call to Slice.ctor
			int p = WriteFixed32BE(value);
			return new Slice(this.Buffer, p, 4);
		}

		#endregion

		#endregion

		#region 64 bits

		#region Little-Endian

		/// <summary>Writes a 64-bit unsigned integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 8 bytes</remarks>
		internal int WriteFixed64(ulong value)
		{
			var buffer = EnsureBytes(8);
			int p = this.Position;
			buffer[p] = (byte)value;
			buffer[p + 1] = (byte)(value >> 8);
			buffer[p + 2] = (byte)(value >> 16);
			buffer[p + 3] = (byte)(value >> 24);
			buffer[p + 4] = (byte)(value >> 32);
			buffer[p + 5] = (byte)(value >> 40);
			buffer[p + 6] = (byte)(value >> 48);
			buffer[p + 7] = (byte)(value >> 56);
			this.Position = p + 8;
			return p;
		}

		/// <summary>Return a fixed size, little-endian encoded, 64-bit signed integer</summary>
		/// <remarks>Slices produced by this method are NOT ordered lexicographically</remarks>
		/// <example>Fixed64(0x0123456789ABCDEF) => { 0xEF 0xCD 0xAB 0x89 0x67 0x45 0x23 0x01 }</example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Fixed64(long value)
		{
			//perf: it is faster to inline the call to Slice.ctor
			int p = WriteFixed64((uint)value);
			return new Slice(this.Buffer, p, 8);
		}

		/// <summary>Return a fixed size, little-endian encoded, 64-bit unsigned integer</summary>
		/// <remarks>Slices produced by this method are NOT ordered lexicographically</remarks>
		/// <example>Fixed64(0x0123456789ABCDEF) => { 0xEF 0xCD 0xAB 0x89 0x67 0x45 0x23 0x01 }</example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Fixed64(ulong value)
		{
			//perf: it is faster to inline the call to Slice.ctor
			int p = WriteFixed64(value);
			return new Slice(this.Buffer, p, 8);
		}

		#endregion

		#region Big-Endian

		/// <summary>Writes a 64-bit unsigned integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 8 bytes</remarks>
		internal int WriteFixed64BE(ulong value)
		{
			var buffer = EnsureBytes(8);
			int p = this.Position;
			buffer[p] = (byte)(value >> 56);
			buffer[p + 1] = (byte)(value >> 48);
			buffer[p + 2] = (byte)(value >> 40);
			buffer[p + 3] = (byte)(value >> 32);
			buffer[p + 4] = (byte)(value >> 24);
			buffer[p + 5] = (byte)(value >> 16);
			buffer[p + 6] = (byte)(value >> 8);
			buffer[p + 7] = (byte)value;
			this.Position = p + 8;
			return p;
		}

		/// <summary>Return a fixed size, big-endian encoded, 64-bit signed integer</summary>
		/// <remarks>Slices produced by this method ARE ordered lexicographically</remarks>
		/// <example>Fixed64(0x0123456789ABCDEF) => { 0x01 0x23 0x45 0x67 0x89 0xAB 0xCD 0xEF }</example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Fixed64BE(long value)
		{
			//perf: it is faster to inline the call to Slice.ctor
			int p = WriteFixed64BE((uint)value);
			return new Slice(this.Buffer, p, 8);
		}

		/// <summary>Return a fixed size, big-endian encoded, 64-bit signed integer</summary>
		/// <remarks>Slices produced by this method ARE ordered lexicographically</remarks>
		/// <example>Fixed64(0x0123456789ABCDEF) => { 0x01 0x23 0x45 0x67 0x89 0xAB 0xCD 0xEF }</example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Fixed64BE(ulong value)
		{
			//perf: it is faster to inline the call to Slice.ctor
			int p = WriteFixed64BE(value);
			return new Slice(this.Buffer, p, 8);
		}

		/// <summary>Return a fixed size, big-endian encoded, 64-bit UUID</summary>
		/// <remarks>Slices produced by this method ARE ordered lexicographically</remarks>
		[Pure]
		public Slice Uuid64(Uuid64 value)
		{
			var buffer = EnsureBytes(8);
			int p = this.Position;
			value.WriteToUnsafe(buffer.AsSpan(p));
			this.Position = p + 8;
			return new Slice(buffer, p, 8);
		}

		#endregion

		#endregion

		#region 128 bits

		// since there are no 128-bit native integer types, we currently emulate this using two 64-bit integers

		#region Little-Endian

		/// <summary>Writes a 128-bit unsigned integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 16 bytes</remarks>
		internal int WriteFixed128(ulong lo, ulong hi)
		{
			var buffer = EnsureBytes(16);
			int p = this.Position;
			buffer[p] = (byte)lo;
			buffer[p + 1] = (byte)(lo >> 8);
			buffer[p + 2] = (byte)(lo >> 16);
			buffer[p + 3] = (byte)(lo >> 24);
			buffer[p + 4] = (byte)(lo >> 32);
			buffer[p + 5] = (byte)(lo >> 40);
			buffer[p + 6] = (byte)(lo >> 48);
			buffer[p + 7] = (byte)(lo >> 56);
			buffer[p + 8] = (byte)hi;
			buffer[p + 9] = (byte)(hi >> 8);
			buffer[p + 10] = (byte)(hi >> 16);
			buffer[p + 11] = (byte)(hi >> 24);
			buffer[p + 12] = (byte)(hi >> 32);
			buffer[p + 13] = (byte)(hi >> 40);
			buffer[p + 14] = (byte)(hi >> 48);
			buffer[p + 15] = (byte)(hi >> 56);
			this.Position = p + 16;
			return p;
		}

		/// <summary>Return a fixed size, little-endian encoded, 128-bit signed integer</summary>
		/// <remarks>Slices produced by this method are NOT ordered lexicographically</remarks>
		/// <example>Fixed64(0x0123456789ABCDEF) => { 0xEF 0xCD 0xAB 0x89 0x67 0x45 0x23 0x01 }</example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Fixed128(long lo, long hi)
		{
			//perf: it is faster to inline the call to Slice.ctor
			int p = WriteFixed128((ulong)lo, (ulong)hi);
			return new Slice(this.Buffer, p, 16);
		}

		/// <summary>Return a fixed size, little-endian encoded, 128-bit unsigned integer</summary>
		/// <remarks>Slices produced by this method are NOT ordered lexicographically</remarks>
		/// <example>Fixed64(0x0123456789ABCDEF) => { 0xEF 0xCD 0xAB 0x89 0x67 0x45 0x23 0x01 }</example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Fixed128(ulong lo, ulong hi)
		{
			//perf: it is faster to inline the call to Slice.ctor
			int p = WriteFixed128(lo, hi);
			return new Slice(this.Buffer, p, 16);
		}

		#endregion

		#region Big-Endian

		/// <summary>Writes a 128-bit unsigned integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 16 bytes</remarks>
		internal int WriteFixed128BE(ulong lo, ulong hi)
		{
			var buffer = EnsureBytes(16);
			int p = this.Position;
			unsafe
			{
				fixed (byte* ptr = &buffer[p])
				{
					ptr[0] = (byte) (lo >> 56);
					ptr[1] = (byte) (lo >> 48);
					ptr[2] = (byte) (lo >> 40);
					ptr[3] = (byte) (lo >> 32);
					ptr[4] = (byte) (lo >> 24);
					ptr[5] = (byte) (lo >> 16);
					ptr[6] = (byte) (lo >> 8);
					ptr[7] = (byte) lo;
					ptr[8] = (byte) (hi >> 56);
					ptr[9] = (byte) (hi >> 48);
					ptr[10] = (byte) (hi >> 40);
					ptr[11] = (byte) (hi >> 32);
					ptr[12] = (byte) (hi >> 24);
					ptr[13] = (byte) (hi >> 16);
					ptr[14] = (byte) (hi >> 8);
					ptr[15] = (byte) hi;
				}
			}
			this.Position = p + 16;
			return p;
		}

		/// <summary>Return a fixed size, big-endian encoded, 128-bit signed integer</summary>
		/// <remarks>Slices produced by this method ARE ordered lexicographically</remarks>
		/// <example>Fixed128BE(0x0011223344556677, 0x8899AABBCCDDEEFF) => { 0x00 0x11 0x22 0x33 0x44 0x55 0x66 0x77 0x88 0x99 0xAA 0xBB 0xCC 0xDD 0xEE 0xFF }</example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Fixed128BE(long lo, long hi)
		{
			//perf: it is faster to inline the call to Slice.ctor
			int p = WriteFixed128BE((ulong)lo, (ulong)hi);
			return new Slice(this.Buffer, p, 16);
		}

		/// <summary>Return a fixed size, big-endian encoded, 128-bit signed integer</summary>
		/// <remarks>Slices produced by this method ARE ordered lexicographically</remarks>
		/// <example>Fixed128BE(0x0011223344556677, 0x8899AABBCCDDEEFF) => { 0x00 0x11 0x22 0x33 0x44 0x55 0x66 0x77 0x88 0x99 0xAA 0xBB 0xCC 0xDD 0xEE 0xFF }</example>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Fixed128BE(ulong lo, ulong hi)
		{
			//perf: it is faster to inline the call to Slice.ctor
			int p = WriteFixed128BE(lo, hi);
			return new Slice(this.Buffer, p, 16);
		}

		/// <summary>Return a fixed size, big-endian encoded, 128-bit UUID</summary>
		/// <remarks>Slices produced by this method ARE ordered lexicographically</remarks>
		[Pure]
		public Slice Uuid128(Guid value)
		{
			var buffer = EnsureBytes(16);
			int p = this.Position;
			new Uuid128(value).WriteTo(buffer.AsSpan(p));
			this.Position = p + 16;
			return new Slice(buffer, p, 16);
		}

		/// <summary>Return a fixed size, big-endian encoded, 128-bit UUID</summary>
		/// <remarks>Slices produced by this method ARE ordered lexicographically</remarks>
		[Pure]
		public Slice Uuid128(Uuid128 value)
		{
			var buffer = EnsureBytes(16);
			int p = this.Position;
			value.WriteTo(buffer.AsSpan(p));
			this.Position = p + 16;
			return new Slice(buffer, p, 16);
		}

		#endregion

		#endregion

		#region Strings...

		/// <summary>Create a slice containing the UTF-8 bytes of the string <paramref name="text"/>.</summary>
		/// <remarks>
		/// DO NOT call this method to encode special strings that contain binary prefixes, like "\xFF/some/system/path" or "\xFE\x01\x02\x03", because they do not map to UTF-8 directly.
		/// For these case, or when you known that the string only contains ASCII only (with 100% certainty), you should use <see cref="Keyword"/>.
		/// </remarks>
		[Pure]
		public Slice Utf8(string? text)
		{
			if (text == null) return Slice.Nil;
			if (text.Length == 0) return Slice.Empty;

			if (text.Length <= 128)
			{
				//TODO: use the local pool!
			}
			// create from the heap
			return Slice.FromString(text);
		}

		/// <summary>Create a slice from an byte string, where all the characters map directly into bytes (0..255), without performing any validation</summary>
		/// <remarks>
		/// This method does not make any effort to detect characters above 255, which will be truncated to their lower 8 bits, introducing corruption when the string will be decoded. Please MAKE SURE to not call this with untrusted data.
		/// Slices encoded by this method are ONLY compatible with UTF-8 encoding if all characters are between 0 and 127. If this is not the case, then decoding it as an UTF-8 sequence may introduce corruption.
		/// </remarks>
		[Pure]
		public Slice Keyword(string? text)
		{
			if (text == null) return Slice.Nil;
			if (text.Length == 0) return Slice.Empty;

			if (text.Length <= 128)
			{
				//TODO: use the local pool!
			}
			return Slice.FromByteString(text);
		}

		/// <summary>Create a slice from an ASCII string, where all the characters map directory into bytes (0..255). The string will be checked before being encoded.</summary>
		/// <remarks>
		/// This method will check each character and fail if at least one is greater than 255.
		/// Slices encoded by this method are only guaranteed to roundtrip if decoded with <see cref="Slice.ToByteString"/>. If the original string only contained ASCII characters (0..127) then it can also be decoded by <see cref="Slice.ToUnicode"/>.
		/// The only difference between this method and <see cref="Keyword"/> is that the later will truncate non-ASCII characters to their lowest 8 bits, while the former will throw an exception.
		/// </remarks>
		/// <exception cref="FormatException">If at least one character is greater than 255.</exception>
		[Pure]
		public Slice Ascii(string? text)
		{
			if (text == null) return Slice.Nil;
			int count = text.Length;
			if (count == 0) return Slice.Empty;

			if (count > 128)
			{
				return Slice.FromStringAscii(text);
			}

			// use the local pool to store the bytes
			var buffer = EnsureBytes(count);
			int p = this.Position;

			unsafe
			{
				//TODO: merge this with Slice.ConvertByteStringChecked() !
				// (or create Slice.ConvertByteStringUnchecked() ?)
				fixed (char* inp = text)
				fixed (byte* outp = &buffer[p])
				{
					var src = inp;
					var stop = src + count;
					var dst = outp;
					while (src < stop)
					{
						*dst++ = (byte) (*src++);
					}
				}
			}
			this.Position = p + count;
			return new Slice(buffer, p, count);
		}

		#endregion

		#region Numbers

		/// <summary>Format a number into a slice</summary>
		/// <param name="value">Number to format</param>
		/// <param name="format">Format as would be used with <see cref="System.Int32.ToString(string)"/></param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Number(int value, string format)
		{
			return Ascii(value.ToString(format, CultureInfo.InvariantCulture));
		}

		/// <summary>Format a number into a slice</summary>
		/// <param name="value">Number to format</param>
		/// <param name="format">Format as would be used with <see cref="System.Int32.ToString(string)"/></param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Number(uint value, string format)
		{
			return Ascii(value.ToString(format, CultureInfo.InvariantCulture));
		}

		/// <summary>Format a number into a slice</summary>
		/// <param name="value">Number to format</param>
		/// <param name="format">Format as would be used with <see cref="System.Int32.ToString(string)"/></param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Number(long value, string format)
		{
			return Ascii(value.ToString(format, CultureInfo.InvariantCulture));
		}

		/// <summary>Format a number into a slice</summary>
		/// <param name="value">Number to format</param>
		/// <param name="format">Format as would be used with <see cref="System.Int32.ToString(string)"/></param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Number(ulong value, string format)
		{
			return Ascii(value.ToString(format, CultureInfo.InvariantCulture));
		}

		/// <summary>Format a decimal number into a slice</summary>
		/// <param name="value">Number to format</param>
		/// <param name="format">Format as would be used with <see cref="System.Int32.ToString(string)"/></param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Number(float value, string format)
		{
			return Ascii(value.ToString(format, CultureInfo.InvariantCulture));
		}

		/// <summary>Format a decimal number into a slice</summary>
		/// <param name="value">Number to format</param>
		/// <param name="format">Format as would be used with <see cref="System.Int32.ToString(string)"/></param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Number(double value, string format)
		{
			return Ascii(value.ToString(format, CultureInfo.InvariantCulture));
		}

		/// <summary>Format a number into a binary slice of variable size</summary>
		/// <param name="value">Number fo format</param>
		/// <param name="ks">Size of the key</param>
		[Pure]
		public Slice Fixed(int value, int ks)
		{
			var buffer = EnsureBytes(ks);
			int p = this.Position;
			int c = p + ks - 1;
			while (c >= p)
			{
				buffer[c--] = (byte) value;
				value >>= 8;
			}
			this.Position = p + ks;
			return new Slice(buffer, p, ks);
		}

		/// <summary>Copies an array of bytes</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Bytes(byte[] bytes)
		{
			return Bytes(bytes, 0, bytes.Length);
		}

		/// <summary>Copies a section of an array of bytes</summary>
		[Pure]
		public Slice Bytes(byte[] bytes, int offset, int count)
		{
			if (count == 0) return Slice.Empty;
			var buffer = EnsureBytes(count);
			int p = this.Position;
			System.Buffer.BlockCopy(bytes, offset, buffer, p, count);
			this.Position = p + count;
			return new Slice(buffer, p, count);
		}

		/// <summary>Copies a slice of bytes</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice Bytes(Slice bytes)
		{
			return Bytes(bytes.Array, bytes.Offset, bytes.Count);
		}

		#endregion
	}
}
