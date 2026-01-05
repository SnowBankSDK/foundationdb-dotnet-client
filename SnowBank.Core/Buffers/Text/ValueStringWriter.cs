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

namespace SnowBank.Buffers.Text
{
	using System.Buffers;
	using System.Globalization;
	using System.Text;
	using SnowBank.Runtime.Converters;
	using SnowBank.Text;

	/// <summary>Small buffer that keeps a list of chunks that are larger and larger</summary>
	[DebuggerDisplay("Count={Count}, Capacity{Buffer.Length}")]
	[DebuggerTypeProxy(typeof(ValueStringWriterDebugView))]
	[PublicAPI]
	public struct ValueStringWriter : IDisposable, IBufferWriter<char>
	{

		// This should only be used when needing to create a list of array of a few elements with as few memory allocations as possible,
		// either by passing a pre-allocated span (on the stack usually), or by using pooled buffers when a resize is required.
		
		// One typical use is to start from a small buffer allocated on the stack, that will be used until the buffer needs to be resized,
		// in which case another buffer will be used from a shared pool.

		/// <summary>Current buffer</summary>
		private char[]? Buffer;

		/// <summary>Number of items in the buffer</summary>
		public int Count;

		/// <summary>Constructs a writer with a minimum initial capacity</summary>
		public ValueStringWriter(int capacity)
 		{
			Contract.Positive(capacity);
			this.Count = 0;
			this.Buffer = capacity > 0 ? ArrayPool<char>.Shared.Rent(capacity) : [ ];
		}

		/// <summary>Returns a span with all the items already written to this buffer</summary>
		public readonly Span<char> Span => this.Count > 0 ? this.Buffer.AsSpan(0, this.Count) : default;

		/// <summary>Returns a span with all the items already written to this buffer</summary>
		public readonly Memory<char> Memory => this.Count > 0 ? this.Buffer.AsMemory(0, this.Count) : default;

		/// <summary>Returns a span with all the items already written to this buffer</summary>
		public readonly ArraySegment<char> Segment => this.Count > 0 ? new ArraySegment<char>(this.Buffer ?? [ ], 0, this.Count) : default;

		/// <summary>Returns the current capacity of the buffer</summary>
		public readonly int Capacity => this.Buffer?.Length ?? 0;

		/// <summary>Appends a character at the end of the buffer</summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void Write(char item)
		{
			int pos = this.Count;
			var buff = this.Buffer;
			if (buff != null && (uint) pos < (uint) buff.Length)
			{
				buff[pos] = item;
				this.Count = pos + 1;
			}
			else
			{
				AddWithResize(item);
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void AddWithResize(char item)
		{
			int pos = this.Count;
			var buffer = Grow(1);
			buffer[pos] = item;
			this.Count = pos + 1;
		}

		/// <summary>Appends a string at the end of the buffer</summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void Write(string items)
		{
			Write(items.AsSpan());
		}

		/// <summary>Appends a span of characters at the end of the buffer</summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		[OverloadResolutionPriority(1)]
		public void Write(scoped ReadOnlySpan<char> items)
		{
			int pos = this.Count;
			var buffer = this.Buffer ?? [ ];
			if ((uint) (items.Length + this.Count) <= (uint) buffer.Length)
			{
				items.CopyTo(buffer.AsSpan(pos));
				this.Count = pos + items.Length;
			}
			else
			{
				AddWithResize(items);
			}
		}

		/// <summary>Appends a span of characters at the end of the buffer</summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Write(ReadOnlyMemory<char> items)
			=> Write(items.Span);

		/// <summary>Appends two characters at the end of the buffer</summary>
		public void Write(char prefix, char value)
		{
			var span = Allocate(2);
			span[0] = prefix;
			span[1] = value;
		}

		/// <summary>Appends a prefix character and a string at the end of the buffer</summary>
		public void Write(char prefix, string value)
		{
			var span = Allocate(checked(value.Length + 1));
			span[0] = prefix;
			value.CopyTo(span[1..]);
		}

		/// <summary>Appends a prefix character and a span of characters at the end of the buffer</summary>
		public void Write(char prefix, scoped ReadOnlySpan<char> value)
		{
			var span = Allocate(checked(value.Length + 1));
			span[0] = prefix;
			value.CopyTo(span[1..]);
		}

		/// <summary>Appends three characters at the end of the buffer</summary>
		public void Write(char prefix, char value, char suffix)
		{
			var span = Allocate(3);
			span[0] = prefix;
			span[1] = value;
			span[2] = suffix;
		}

		/// <summary>Appends a string surrounded with a prefix and suffix at the end of the buffer</summary>
		public void Write(char prefix, string value, char suffix)
		{
			var span = Allocate(checked(value.Length + 2));
			span[0] = prefix;
			value.CopyTo(span[1..]);
			span[^1] = suffix;
		}

		/// <summary>Appends a string surrounded with a prefix and suffix at the end of the buffer</summary>
		public void Write(char prefix, scoped ReadOnlySpan<char> value, char suffix)
		{
			var span = Allocate(checked(value.Length + 2));
			span[0] = prefix;
			value.CopyTo(span[1..]);
			span[^1] = suffix;
		}

		/// <summary>Appends two strings at the end of the buffer</summary>
		public void Write(scoped ReadOnlySpan<char> prefix, scoped ReadOnlySpan<char> value)
		{
			var span = Allocate(checked(prefix.Length + value.Length));
			prefix.CopyTo(span);
			value.CopyTo(span[prefix.Length..]);
		}

		/// <summary>Appends a character and two strings at the end of the buffer</summary>
		public void Write(char prefix, string value, string suffix)
		{
			var span = Allocate(checked(1 + value.Length + suffix.Length));
			span[0] = prefix;
			value.CopyTo(span[1..]);
			suffix.CopyTo(span[(1 + value.Length)..]);
		}

		/// <summary>Appends three strings at the end of the buffer</summary>
		public void Write(scoped ReadOnlySpan<char> prefix, scoped ReadOnlySpan<char> value, scoped ReadOnlySpan<char> suffix)
		{
			var span = Allocate(checked(prefix.Length + value.Length + suffix.Length));
			prefix.CopyTo(span);
			value.CopyTo(span[prefix.Length..]);
			suffix.CopyTo(span[(prefix.Length + value.Length)..]);
		}

		/// <summary>Appends two strings and a suffix character at the end of the buffer</summary>
		public void Write(scoped ReadOnlySpan<char> prefix, scoped ReadOnlySpan<char> value, char suffix)
		{
			var span = Allocate(checked(prefix.Length + value.Length + 1));
			prefix.CopyTo(span);
			value.CopyTo(span[prefix.Length..]);
			span[^1] = suffix;
		}

		/// <summary>Writes a repeated character at the end of the buffer</summary>
		public void Write(char c, int count)
		{
			var span = Allocate(count)[..count];
			Contract.Debug.Assert(span.Length == count);
			span.Fill(c);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void AddWithResize(scoped ReadOnlySpan<char> items)
		{
			var buffer = Grow(items.Length);

			int pos = this.Count;
			items.CopyTo(buffer.AsSpan(pos));
			this.Count = pos + items.Length;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		[MustUseReturnValue]
		private char[] Grow(int required)
		{
			Contract.GreaterThan(required, 0);
			// Growth rate:
			// - first chunk size is 4 if empty
			// - double the buffer size
			// - except the first chunk who is set to the initial capacity

			const long MAX_CAPACITY = int.MaxValue & ~63;

			var oldBuffer = this.Buffer ?? [];
			int length = oldBuffer.Length;
			long capacity = (long) length + required;
			if (capacity > MAX_CAPACITY)
			{
				throw new InvalidOperationException($"Buffer cannot expand because it would exceed the maximum size limit ({capacity:N0} > {MAX_CAPACITY:N0}).");
			}
			capacity = Math.Max(length != 0 ? length * 2 : 4, length + required);

			// allocate a new buffer (note: may be bigger than requested)
			var newBuffer = ArrayPool<char>.Shared.Rent((int) capacity);
			if (length > 0)
			{
				oldBuffer.AsSpan().CopyTo(newBuffer);
			}

			this.Buffer = newBuffer;

			// return any previous buffer to the pool
			if (oldBuffer.Length != 0)
			{
				oldBuffer.AsSpan(0, this.Count).Clear();
				ArrayPool<char>.Shared.Return(oldBuffer);
			}

			return newBuffer;
		}

		private void Clear(bool release)
		{
			if (this.Count != 0)
			{
				this.Span.Clear();
				this.Count = 0;
			}

			// return the array to the pool
			if (release)
			{
				var buffer = this.Buffer;
				this.Buffer = null;

				if (buffer?.Length > 0)
				{
					ArrayPool<char>.Shared.Return(buffer);
				}
			}
		}

		/// <summary>Clears the content of the buffer</summary>
		/// <remarks>The buffer count is reset to zero, but the current backing store remains the same</remarks>
		public void Clear() => Clear(release: false);

		/// <summary>Returns a <see cref="string"/> with a copy of the content in this instance</summary>
		/// <remarks>
		/// <para>Calling this method will not release the buffer allocated by this instance.</para>
		/// <para>Use <see cref="ToStringAndDispose"/> to dispose the buffer as well, or <see cref="ToStringAndClear"/> if you intend to reuse the buffer for the next operation.</para>
		/// </remarks>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public readonly override string ToString() => this.Span.ToString();

		/// <summary>Returns a <see cref="string"/> with a copy of the content in this instance, and clears the buffer so that it can be reused immediately</summary>
		/// <remarks>This is the equivalent to calling <see cref="ToString"/>, followed by <see cref="Clear()"/></remarks>
		public string ToStringAndClear()
		{
			var s = ToString();
			this.Dispose();
			return s;
		}

		/// <summary>Returns a <see cref="string"/> with a copy of the content in this instance, and returns the buffer to the pool</summary>
		/// <remarks>This is the equivalent to calling <see cref="ToString"/>, followed by <see cref="Dispose"/></remarks>
		public string ToStringAndDispose()
		{
			var s = ToString();
			this.Dispose();
			return s;
		}

		/// <summary>Returns a <see cref="Slice"/> with a copy of the content in this instance</summary>
		/// <param name="pool">If non-null, pool used to allocate the b</param>
		/// <para>Calling this method will not release the buffer allocated by this instance.</para>
		/// <para>Use <see cref="ToUtf8SliceAndDispose"/> to dispose the buffer as well, or <see cref="ToUtf8SliceAndClear"/> if you intend to reuse the buffer for the next operation.</para>
		public readonly Slice ToUtf8Slice(ArrayPool<byte>? pool = null)
		{
			var enc = JsonEncoding.Utf8NoBom;
			var span = this.Span;
			int size = enc.GetByteCount(span);
			var tmp = pool?.Rent(size) ?? new byte[size];
			var written = enc.GetBytes(span, tmp);
			return tmp.AsSlice(0, written);
		}

		/// <summary>Returns a <see cref="Slice"/> with a copy of the content in this instance, and clears the buffer so that it can be reused immediately</summary>
		/// <param name="pool">If non-null, pool used to allocate the b</param>
		/// <remarks>
		/// <para>This is the equivalent to calling <see cref="ToUtf8SliceOwner"/>, followed by <see cref="Clear()"/></para>
		/// </remarks>
		public Slice ToUtf8SliceAndClear(ArrayPool<byte>? pool = null)
		{
			var slice = ToUtf8Slice(pool);
			Clear();
			return slice;
		}

		/// <summary>Returns a <see cref="Slice"/> with a copy of the content in this instance, and returns the buffer to the pool</summary>
		/// <param name="pool">If non-null, pool used to allocate the buffer for the returned slice. Otherwise, the buffer will be allocated on the heap</param>
		/// <remarks>
		/// <para>This is the equivalent to calling <see cref="ToUtf8Slice"/>, followed by <see cref="Dispose"/></para>
		/// </remarks>
		public Slice ToUtf8SliceAndDispose(ArrayPool<byte>? pool = null)
		{
			var slice = ToUtf8Slice(pool);
			Dispose();
			return slice;
		}

		/// <summary>Returns a <see cref="SliceOwner"/> with a copy of the content in this instance</summary>
		/// <param name="pool">Pool used has the backing store for the <see cref="SliceOwner"/> (<see cref="ArrayPool{T}.Shared"/> is not specified)</param>
		/// <returns>
		/// <para><see cref="SliceOwner"/> instance with a copy of the content allocated using <paramref name="pool"/>. The value <b>MUST</b> be disposed at a later point; otherwise, the buffer will not be returned to the pool.</para>
		/// <para>Calling this method will not release the buffer allocated by this instance.</para>
		/// <para>Use <see cref="ToUtf8SliceOwnerAndDispose"/> to dispose the buffer as well, or <see cref="ToUtf8SliceOwnerAndClear"/> if you intend to reuse the buffer for the next operation.</para>
		/// </returns>
		public readonly SliceOwner ToUtf8SliceOwner(ArrayPool<byte>? pool = null)
		{
			pool ??= ArrayPool<byte>.Shared;
			var enc = JsonEncoding.Utf8NoBom;
			var span = this.Span;
			int size = enc.GetByteCount(span);
			var tmp = pool.Rent(size);
			var written = enc.GetBytes(span, tmp);
			return SliceOwner.Create(tmp.AsSlice(0, written), pool);
		}

		/// <summary>Returns a <see cref="SliceOwner"/> with a copy of the content in this instance, and clears the buffer so that it can be reused immediately</summary>
		/// <param name="pool">Pool used has the backing store for the <see cref="SliceOwner"/> (<see cref="ArrayPool{T}.Shared"/> is not specified)</param>
		/// <returns>
		/// <para><see cref="SliceOwner"/> instance with a copy of the content allocated using <paramref name="pool"/>. The value <b>MUST</b> be disposed at a later point; otherwise, the buffer will not be returned to the pool.</para>
		/// <para>Calling this method will not release the buffer allocated by this instance.</para>
		/// <para>This is the equivalent to calling <see cref="ToUtf8SliceOwner"/>, followed by <see cref="Clear()"/></para>
		/// </returns>
		public SliceOwner ToUtf8SliceOwnerAndClear(ArrayPool<byte>? pool = null)
		{
			var slice = ToUtf8SliceOwner(pool);
			Clear();
			return slice;
		}

		/// <summary>Returns a <see cref="SliceOwner"/> with a copy of the content in this instance, and returns the buffer to the pool</summary>
		/// <param name="pool">Pool used has the backing store for the <see cref="SliceOwner"/> (<see cref="ArrayPool{T}.Shared"/> is not specified)</param>
		/// <returns>
		/// <para><see cref="SliceOwner"/> instance with a copy of the content allocated using <paramref name="pool"/>. The value <b>MUST</b> be disposed at a later point; otherwise, the buffer will not be returned to the pool.</para>
		/// <para>Calling this method will not release the buffer allocated by this instance.</para>
		/// <para>This is the equivalent to calling <see cref="ToUtf8SliceOwner"/>, followed by <see cref="Dispose"/></para>
		/// </returns>
		public SliceOwner ToUtf8SliceOwnerAndDispose(ArrayPool<byte>? pool = null)
		{
			var slice = ToUtf8SliceOwner(pool);
			Dispose();
			return slice;
		}

		/// <summary>Copies the content of the buffer into a destination span</summary>
		[CollectionAccess(CollectionAccessType.Read)]
		public readonly int CopyTo(Span<char> destination)
		{
			if (this.Count > destination.Length)
			{
				throw new ArgumentException("Destination buffer is too small", nameof(destination));
			}

			this.Span.CopyTo(destination);
			var count = this.Count;
			return count;
		}

		/// <summary>Copies the content of the buffer into a destination span, if it is large enough</summary>
		[CollectionAccess(CollectionAccessType.Read)]
		public readonly bool TryCopyTo(Span<char> destination, out int written)
		{
			int count = this.Count;
			if (count > destination.Length)
			{
				written = 0;
				return false;
			}

			this.Span.TryCopyTo(destination);
			written = count;
			return true;
		}

		/// <summary>Copies the content of the buffer as UTF-8 bytes into a destination buffer</summary>
		public readonly int CopyToUtf8(Span<byte> destination)
		{
			return JsonEncoding.Utf8NoBom.GetBytes(this.Span, destination);
		}

		/// <summary>Copies the content of the buffer as UTF-8 bytes into a destination buffer, if it is large enough</summary>
		public readonly bool TryCopyToUtf8(Span<byte> destination, out int written)
		{
#if NET8_0_OR_GREATER
			if (!JsonEncoding.Utf8NoBom.TryGetBytes(this.Span, destination, out written))
			{
				return false;
			}
#else
			int required = JsonEncoding.Utf8NoBom.GetByteCount(this.Span);
			if (destination.Length < required)
			{
				written = 0;
				return false;
			}

			written = JsonEncoding.Utf8NoBom.GetBytes(this.Span, destination);
#endif
			return true;
		}

		/// <summary>Copies the content of the buffer into a destination span</summary>
		[CollectionAccess(CollectionAccessType.Read)]
		public readonly int CopyTo(StringBuilder destination)
		{
			destination.Append(this.Span);
			var count = this.Count;
			return count;
		}

		/// <summary>Copies the content of the buffer into a destination writer</summary>
		public readonly void CopyTo(IBufferWriter<byte> destination)
		{
			JsonEncoding.Utf8NoBom.GetBytes(this.Span, destination);
		}

		/// <summary>Copies the content of the buffer into a destination stream</summary>
		public readonly void CopyTo(Stream destination)
		{
			Contract.NotNull(destination);
			if (!destination.CanWrite) throw new InvalidOperationException("Cannot write to destination stream");

			using var data = ToUtf8SliceOwner();
			destination.Write(data.Span);
		}

		/// <summary>Copies the content of the buffer into a destination stream</summary>
		public readonly async Task CopyToAsync(Stream destination, CancellationToken ct)
		{
			Contract.NotNull(destination);
			ct.ThrowIfCancellationRequested();
			if (!destination.CanWrite) throw new InvalidOperationException("Cannot write to destination stream");

			using var data = ToUtf8SliceOwner();

			if (destination is MemoryStream ms)
			{
				ms.Write(data.Span);
			}
			else
			{
				await destination.WriteAsync(data.Memory, ct).ConfigureAwait(false);
			}

		}

		#region IReadOnlyList<T>...

		/// <summary>Returns a reference to the character at the specified location in the buffer</summary>
		[Pure, CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public readonly ref char this[int index]
		{
			[Pure, CollectionAccess(CollectionAccessType.Read)]
			get => ref this.Buffer.AsSpan(0, this.Count)[index];
			//note: the span will perform the bound-checking for us
		}

		/// <summary>Returns an enumerator that will return all the characters written so far</summary>
		public readonly Span<char>.Enumerator GetEnumerator()
		{
			return this.Span.GetEnumerator();
		}

		#endregion

		#region IBufferWriter<T>...

		/// <summary>Allocates a fixed-size span, and advance the cursor</summary>
		/// <param name="size">Size of the buffer to allocate</param>
		/// <returns>Span of size <paramref name="size"/></returns>
		/// <remarks>The cursor is advanced by <paramref name="size"/></remarks>
		public Span<char> Allocate(int size)
		{
			Contract.Positive(size);

			// do we have enough space in the current segment?
			var buffer = this.Buffer ?? [ ];
			var count = this.Count;
			int newCount = count + size;
			if ((uint) newCount > (uint) buffer.Length)
			{
				buffer = Grow(size);
			}

			// we have enough remaining data to accomodate the requested size
			this.Count = newCount;
			return buffer.AsSpan(count, size);
		}

		/// <summary>Allocate a fixed-size section, and returns an unsafe reference to the start of the segment.</summary>
		/// <param name="size">Size of the buffer to allocate</param>
		/// <returns>Reference to the start of the span</returns>
		/// <remarks>
		/// <para><b>CAUTION</b>: the caller must take extreme care in not overflowing the allocated span!</para>
		/// <para>The cursor is advanced by <paramref name="size"/></para>
		/// </remarks>
		private ref char AllocateRefUnsafe(
#if NET8_0_OR_GREATER
			[System.Diagnostics.CodeAnalysis.ConstantExpected(Min = 1)]
#endif
			int size)
		{
			Contract.Debug.Requires(size > 0);

			// do we have enough space in the current segment?
			var buffer = this.Buffer ?? [ ];
			var count = this.Count;
			int newCount = count + size;
			if ((uint) newCount > (uint) buffer.Length)
			{
				buffer = Grow(size);
			}

			// we have enough remaining data to accomodate the requested size
			this.Count = newCount;
			return ref buffer[count];
		}

		/// <inheritdoc />
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void Advance(int count)
		{
			Contract.Positive(count);
			var buffer = this.Buffer ?? [ ];
			var newIndex = checked(this.Count + count);
			if ((uint) newIndex > (uint) buffer.Length)
			{
				throw new ArgumentException("Cannot advance past the previously allocated buffer");
			}
			this.Count = newIndex;
			Contract.Debug.Ensures((uint) this.Count <= buffer.Length);
		}

		/// <inheritdoc />
		public Memory<char> GetMemory(int sizeHint = 0)
		{
			Contract.Positive(sizeHint);

			// do we have enough space in the current segment?
			var buffer = this.Buffer ?? [ ];
			int remaining = buffer.Length - this.Count;

			if (remaining <= 0 || (sizeHint != 0 && remaining < sizeHint))
			{
				buffer = Grow(sizeHint);
			}

			// we have enough remaining data to accomodate the requested size
			return buffer.AsMemory(this.Count);
		}

		/// <inheritdoc />
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public Span<char> GetSpan(int sizeHint = 0)
		{
			Contract.Positive(sizeHint);

			// do we have enough space in the current segment?
			var buffer = this.Buffer ?? [ ];
			int remaining = buffer.Length - this.Count;

			if (remaining <= 0 || (sizeHint != 0 && remaining < sizeHint))
			{
				buffer = Grow(sizeHint);
			}

			// we have enough remaining data to accomodate the requested size
			return buffer.AsSpan(this.Count);
		}

		#endregion

		#region Formatting...

		/// <summary>Writes a text representation of a boolean (<c>"true"</c> or <c>"false"</c>)</summary>
		public void Write(bool value)
		{
			//TODO: PERF: OPTIMIZE: according to https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-9/#vectorization
			// the JIT will be able to automatically optimize the following code, so that we don't have to. .NET 9 already does this for arrays, but not yet on spans !
			//
			// if (value)
			// {
			//     buf[0] = 't';
			//     buf[1] = 'r';
			//     buf[2] = 'u';
			//     buf[3] = 'e';
			// }

			if (value)
			{ // "true"
				ref char buf = ref AllocateRefUnsafe(4);
				// LE: (((ulong)'e' << 48) | ((ulong)'u' << 32) | ((ulong)'r' << 16) | (ulong)'t')
				// BE: (((ulong)'t' << 48) | ((ulong)'r' << 32) | ((ulong)'u' << 16) | (ulong)'e')
				Unsafe.WriteUnaligned(
					ref Unsafe.As<char, byte>(ref buf),
					BitConverter.IsLittleEndian ? 0x65007500720074ul : 0x74007200750065ul
				);
			}
			else
			{ // "false"
				ref char buf = ref AllocateRefUnsafe(5);
				// LE: (((ulong)'s' << 48) | ((ulong)'l' << 32) | ((ulong)'a' << 16) | (ulong)'f')
				// BE: (((ulong)'f' << 48) | ((ulong)'a' << 32) | ((ulong)'l' << 16) | (ulong)'s')
				Unsafe.WriteUnaligned(
					ref Unsafe.As<char, byte>(ref buf),
					BitConverter.IsLittleEndian ? 0x73006C00610066ul : 0x660061006C0073ul
				);
				Unsafe.Add(ref buf, 4) = 'e';
			}
		}

		/// <summary>Writes the text representation of a 16-bit signed integer, using the Invariant culture</summary>
		/// <param name="value">Value to write</param>
		public void Write(sbyte value)
		{
#if NET9_0_OR_GREATER
			if (value >= 0)
			{
				// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
				Write(value.ToString(default(IFormatProvider)));
				return;
			}
#endif
			Span<char> buf = GetSpan(StringConverters.Base10MaxCapacityInt8);

			bool success = value.TryFormat(buf, out int written, default, NumberFormatInfo.InvariantInfo); // will be inlined as Number.TryNegativeInt32ToDecStr
			if (!success) StringConverters.ReportInternalFormattingError();

			Advance(written);
		}

		/// <summary>Writes the text representation of a 16-bit unsigned integer, using the Invariant culture</summary>
		/// <param name="value">Value to write</param>
		public void Write(byte value)
		{
#if NET9_0_OR_GREATER
			// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
			Write(value.ToString(default(IFormatProvider)));
#else

			Span<char> buf = GetSpan(StringConverters.Base10MaxCapacityUInt8);

			bool success = value.TryFormat(buf, out int written); // will be inlined as Number.TryUInt32ToDecStr
			if (!success) StringConverters.ReportInternalFormattingError();

			Advance(written);
#endif
		}

		/// <summary>Writes the text representation of a 16-bit signed integer, using the Invariant culture</summary>
		/// <param name="value">Value to write</param>
		public void Write(short value)
		{
#if NET9_0_OR_GREATER
			if ((uint) value < 300U)
			{
				// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
				Write(value.ToString(default(IFormatProvider)));
				return;
			}
#endif

			Span<char> buf = GetSpan(StringConverters.Base10MaxCapacityInt16);

			bool success = value >= 0
				? value.TryFormat(buf, out var written) // will be inlined as Number.TryUInt32ToDecStr
				: value.TryFormat(buf, out written, default, NumberFormatInfo.InvariantInfo); // will be inlined as Number.TryNegativeInt32ToDecStr
			if (!success) StringConverters.ReportInternalFormattingError();

			Advance(written);
		}

		/// <summary>Writes the text representation of a 16-bit unsigned integer, using the Invariant culture</summary>
		/// <param name="value">Value to write</param>
		public void Write(ushort value)
		{
#if NET9_0_OR_GREATER
			if (value < 300U)
			{
				// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
				Write(value.ToString(default(IFormatProvider)));
				return;
			}
#endif

			Span<char> buf = GetSpan(StringConverters.Base10MaxCapacityUInt16);

			bool success = value.TryFormat(buf, out int written); // will be inlined as Number.TryUInt32ToDecStr
			if (!success) StringConverters.ReportInternalFormattingError();

			Advance(written);
		}

		/// <summary>Writes the text representation of a 32-bit signed integer, using the Invariant culture</summary>
		/// <param name="value">Value to write</param>
		public void Write(int value)
		{
#if NET9_0_OR_GREATER
			if ((uint) value < 300U)
			{
				// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
				Write(value.ToString(default(IFormatProvider)));
				return;
			}
#endif

			Span<char> buf = GetSpan(StringConverters.Base10MaxCapacityInt32);

			bool success = value >= 0
				? value.TryFormat(buf, out var written) // will be inlined as Number.TryUInt32ToDecStr
				: value.TryFormat(buf, out written, default, NumberFormatInfo.InvariantInfo); // will be inlined as Number.TryNegativeInt32ToDecStr
			if (!success) StringConverters.ReportInternalFormattingError();

			Advance(written);
		}

		/// <summary>Writes the text representation of a 32-bit unsigned integer, using the Invariant culture</summary>
		/// <param name="value">Value to write</param>
		public void Write(uint value)
		{
#if NET9_0_OR_GREATER
			if (value < 300U)
			{
				// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
				Write(value.ToString(default(IFormatProvider)));
				return;
			}
#endif

			Span<char> buf = GetSpan(StringConverters.Base10MaxCapacityUInt32);

			bool success = value.TryFormat(buf, out int written); // will be inlined as Number.TryUInt32ToDecStr
			if (!success) StringConverters.ReportInternalFormattingError();

			Advance(written);
		}

		/// <summary>Writes the text representation of a 64-bit signed integer, using the Invariant culture</summary>
		/// <param name="value">Value to write</param>
		public void Write(long value)
		{
#if NET9_0_OR_GREATER
			if ((ulong) value < 300UL)
			{
				// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
				Write(value.ToString(default(IFormatProvider)));
				return;
			}
#endif

			Span<char> buf = GetSpan(StringConverters.Base10MaxCapacityInt64);

			bool success = value >= 0
				? value.TryFormat(buf, out var written) // will be inlined as Number.TryUInt64ToDecStr
				: value.TryFormat(buf, out written, default, NumberFormatInfo.InvariantInfo); // will be inlined as Number.TryNegativeInt64ToDecStr
			if (!success) StringConverters.ReportInternalFormattingError();

			Advance(written);
		}

		/// <summary>Writes the text representation of a 64-bit unsigned integer, using the Invariant culture</summary>
		/// <param name="value">Value to write</param>
		public void Write(ulong value)
		{
#if NET9_0_OR_GREATER
			if (value < 300UL)
			{
				// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
				Write(value.ToString(default(IFormatProvider)));
				return;
			}
#endif

			Span<char> buf = GetSpan(StringConverters.Base10MaxCapacityUInt64);

			bool success = value.TryFormat(buf, out int written); // will be inlined as Number.TryUInt64ToDecStr
			if (!success) StringConverters.ReportInternalFormattingError();

			Advance(written);
		}

		/// <summary>Writes the text representation of a <see cref="Guid"/>, using the Invariant culture</summary>
		/// <param name="value">Value to write</param>
		public void Write(Guid value)
		{
			Span<char> buf = GetSpan(StringConverters.Base16MaxCapacityGuid);

			bool success = value.TryFormat(buf, out int written);
			if (!success) StringConverters.ReportInternalFormattingError();
			
			Advance(written);
		}

		/// <summary>Writes the text representation of a <see cref="Uuid128"/>, using the Invariant culture</summary>
		/// <param name="value">Value to write</param>
		public void Write(Uuid128 value)
		{
			Span<char> buf = GetSpan(StringConverters.Base16MaxCapacityGuid);

			bool success = value.TryFormat(buf, out int written);
			if (!success) StringConverters.ReportInternalFormattingError();

			Advance(written);
		}

		/// <summary>Writes the text representation of a <see cref="Uuid64"/>, using the Invariant culture</summary>
		/// <param name="value">Value to write</param>
		public void Write(Uuid64 value)
		{
			Span<char> buf = GetSpan(StringConverters.Base16MaxCapacityUuid64);

			bool success = value.TryFormat(buf, out int written);
			if (!success) StringConverters.ReportInternalFormattingError();
			
			Advance(written);
		}

#if NET8_0_OR_GREATER

		/// <summary>Writes the text representation of a 64-bit signed integer, using the Invariant culture</summary>
		/// <param name="value">Value to write</param>
		public void Write(Int128 value)
		{
			Span<char> buf = GetSpan(StringConverters.Base10MaxCapacityInt128);

			bool success =
				  value >= 0 ? value.TryFormat(buf, out int written)
				: value.TryFormat(buf, out written, default, NumberFormatInfo.InvariantInfo);
			if (!success) StringConverters.ReportInternalFormattingError();
			
			Advance(written);
		}

		/// <summary>Writes the text representation of a 64-bit unsigned integer, using the Invariant culture</summary>
		/// <param name="value">Value to write</param>
		public void Write(UInt128 value)
		{
			Span<char> buf = GetSpan(StringConverters.Base10MaxCapacityUInt128);

			bool success = value.TryFormat(buf, out int written); // will be inlined as Number.TryUInt128ToDecStr
			if (!success) StringConverters.ReportInternalFormattingError();

			Advance(written);
		}

#endif

		/// <summary>Writes the text representation of a 32-bit IEEE floating point number, using the Invariant culture</summary>
		/// <param name="value">Value to write</param>
		public void Write(float value)
		{
			Span<char> buf = GetSpan(StringConverters.Base10MaxCapacitySingle);

			long x = unchecked((long) value);
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			bool success =
				  x != value ? value.TryFormat(buf, out var written, "R", NumberFormatInfo.InvariantInfo)
				: x >= 0 ? x.TryFormat(buf, out written) // will be inlined as Number.TryUInt64ToDecStr
				: x.TryFormat(buf, out written, default, NumberFormatInfo.InvariantInfo); // will be inlined as Number.TryNegativeInt64ToDecStr
			if (!success) StringConverters.ReportInternalFormattingError();

			Advance(written);
		}

		/// <summary>Writes the text representation of a 64-bit IEEE floating point number, using the Invariant culture</summary>
		/// <param name="value">Value to write</param>
		public void Write(double value)
		{
			Span<char> buf = GetSpan(StringConverters.Base10MaxCapacityDouble);

			long x = unchecked((long) value);
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			bool success =
				  x != value ? value.TryFormat(buf, out var written, "R", NumberFormatInfo.InvariantInfo)
				: x >= 0 ? x.TryFormat(buf, out written) // will be inlined as Number.TryUInt64ToDecStr
				: x.TryFormat(buf, out written, default, NumberFormatInfo.InvariantInfo); // will be inlined as Number.TryNegativeInt64ToDecStr
			if (!success) StringConverters.ReportInternalFormattingError();

			Advance(written);
		}

		/// <summary>Writes the text representation of a 128-bit decimal floating point number, using the Invariant culture</summary>
		/// <param name="value">Value to write</param>
		public void Write(decimal value)
		{
			Span<char> buf = GetSpan(StringConverters.Base10MaxCapacityDecimal);

			bool success = value.TryFormat(buf, out var written, default, NumberFormatInfo.InvariantInfo);
			if (!success) StringConverters.ReportInternalFormattingError();

			Advance(written);
		}

		/// <summary>Writes the text representation of a 16-bit IEEE floating point number, using the Invariant culture</summary>
		/// <param name="value">Value to write</param>
		public void Write(Half value)
		{
			Span<char> buf = GetSpan(StringConverters.Base10MaxCapacityHalf);

			//note: I'm not sure how to optimize for this type...
			bool success = value.TryFormat(buf, out var written, null, NumberFormatInfo.InvariantInfo);
			if (!success) StringConverters.ReportInternalFormattingError();

			Advance(written);
		}

		#endregion

		/// <inheritdoc />
		public void Dispose()
		{
			Clear(release: true);
		}

	}

	[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
	internal class ValueStringWriterDebugView
	{
		public ValueStringWriterDebugView(ValueStringWriter buffer)
		{
			this.Text = buffer.ToString();
		}

		[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
		public string Text { get; set; }

	}

}
