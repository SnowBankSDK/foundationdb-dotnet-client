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

namespace SnowBank.Data.Binary
{
	using System;
	using System.Buffers;
	using System.Buffers.Binary;
	using System.Text;
	using SnowBank.Data.Tuples;

	/// <summary>Encoders that can encode primitive types into binary form</summary>
	public static class SpanEncoders
	{

		#region Encoding Helper Methods...

#if NET7_0_OR_GREATER
		// calling static abstract interface members through a type parameter needs runtime support that netstandard2.0/NetFX lacks

		/// <summary>Encodes a value, using a pool if necessary</summary>
		/// <typeparam name="TEncoder">Encoder used for this operation</typeparam>
		/// <typeparam name="TValue">Type of the encoded value</typeparam>
		/// <param name="value">Value to encode</param>
		/// <param name="pool">Pool used to allocate buffers</param>
		/// <param name="maxSize">If specified, maximum allowed size for the buffer. If 0, allows the buffer to grow up to the maximum size supported by the CLR.</param>
		/// <param name="result">Receives the span that points to the encoded result</param>
		/// <param name="buffer">Receives the buffer allocated from the pool to hold the result, or <c>null</c> if the result is already hosted somewhere else in memory.</param>
		/// <param name="range">Receives the range in <paramref name="buffer"/> that contains <paramref name="result"/> (only if buffer is not <c>null</c>)</param>
		/// <exception cref="ArgumentException">If the buffer size would exceed the maximum allowed size (or maximum limit supported by the CLR)</exception>
		/// <remarks>
		/// <para>The method will call <see cref="ISpanEncoder{TValue}.TryGetSpan"/> to extract any already encoded result.</para>
		/// <para>If no span could be extracted, then it will call <see cref="ISpanEncoder{TValue}.TryGetSizeHint"/>, and rent a buffer from the pool with a safe initial size.</para>
		/// <para>The method will call <see cref="ISpanEncoder{TValue}.TryEncode"/> repeatedly, with a larger and larger buffer, until it returns <c>true</c>, throws, or the buffer reaches the maximum allowed size.</para>
		/// </remarks>
		public static void Encode<TEncoder, TValue>(in TValue? value, ArrayPool<byte> pool, int maxSize, out ReadOnlySpan<byte> result, out byte[]? buffer, out Range range)
#if NET9_0_OR_GREATER
			where TValue : allows ref struct
#endif
			where TEncoder : ISpanEncoder<TValue>
		{
			if (TEncoder.TryGetSpan(value, out result))
			{
				buffer = null;
				range = default;
				return;
			}

			int size = TEncoder.TryGetSizeHint(value, out var keySizeHint) ? keySizeHint : 128;
			if (maxSize <= 0) maxSize = 1 << 30;

			byte[]? tmp = null;
			try
			{
				while (true)
				{
					tmp = pool.Rent(size);
					if (TEncoder.TryEncode(tmp, out int bytesWritten, in value))
					{
						if (bytesWritten == 0)
						{
							pool.Return(tmp);
							tmp = null;
							buffer = null;
							result = default;
							range = default;
							return;
						}

						buffer = tmp;
						result = tmp.AsSpan(0, bytesWritten);
						range = new(0, bytesWritten);
						tmp = null;
						break;
					}

					pool.Return(tmp);

					if (size >= maxSize)
					{
						// it would be too large anyway!
						throw new ArgumentException("Cannot format item because it would exceed the maximum allowed length.");
					}

					size *= 2;
				}
			}
			finally
			{
				if (tmp is not null)
				{
					buffer = null;
					result = default;
					range = default;
					pool.Return(tmp);
				}
			}
		}

		/// <summary>Returns a <see cref="Slice"/> that contains the encoded binary form of the value, using the specified encoder</summary>
		/// <typeparam name="TEncoder">Encoder used by the operation</typeparam>
		/// <typeparam name="TValue">Type of the encoded value</typeparam>
		/// <param name="value">Value to encode</param>
		/// <returns><see cref="Slice"/> that contains the encoded value.</returns>
		public static Slice ToSlice<TEncoder, TValue>(in TValue? value)
#if NET9_0_OR_GREATER
			where TValue : allows ref struct
#endif
			where TEncoder : ISpanEncoder<TValue>
		{
			var pool = ArrayPool<byte>.Shared;
			Encode<TEncoder, TValue>(in value, pool, 0, out var span, out var buffer, out _);
			var slice = span.ToSlice();
			if (buffer is not null)
			{
				pool.Return(buffer);
			}

			return slice;
		}


		/// <summary>Returns a <see cref="SliceOwner"/> that contains the encoded binary form of the value, using the specified encoder and a buffer rented from a pool.</summary>
		/// <typeparam name="TEncoder">Encoder used by the operation</typeparam>
		/// <typeparam name="TValue">Type of the encoded value</typeparam>
		/// <param name="value">Value to encode</param>
		/// <param name="pool">Pool used to rent buffers (use the shared pool if <c>null</c>)</param>
		/// <returns><see cref="SliceOwner"/> that contains the encoded value stored in buffer rented from the pool.</returns>
		/// <remarks>
		/// <para>The caller <b>MUST</b> dispose the result once done, otherwise the rented buffer will not be returned to the pool.</para>
		/// </remarks>
		public static SliceOwner ToSlice<TEncoder, TValue>(in TValue? value, ArrayPool<byte>? pool)
#if NET9_0_OR_GREATER
			where TValue : allows ref struct
#endif
			where TEncoder : ISpanEncoder<TValue>
		{
			pool ??= ArrayPool<byte>.Shared;

			Encode<TEncoder, TValue>(in value, pool, 0, out var span, out var buffer, out var range);

			return buffer is null
				? SliceOwner.Copy(span, pool)
				: SliceOwner.Create(buffer.AsSlice(range));
		}
#endif

		/// <summary>Writes a byte at the start of the destination buffer, if it is large enough.</summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="bytesWritten">Receives <c>1</c> if the operation was successful; otherwise, <c>0</c></param>
		/// <param name="byte1">Byte to write</param>
		/// <returns><c>true</c> if the buffer was large enough; otherwise, <c>false</c>.</returns>
		internal static bool TryWriteByte(Span<byte> destination, out int bytesWritten, byte byte1)
		{
			if (destination.Length < 1)
			{
				bytesWritten = 0;
				return false;
			}

			destination[0] = byte1;
			bytesWritten = 1;
			return true;
		}

		/// <summary>Writes two bytes at the start of the destination buffer, if it is large enough.</summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="bytesWritten">Receives <c>2</c> if the operation was successful; otherwise, <c>0</c></param>
		/// <param name="byte1">First byte to write</param>
		/// <param name="byte2">Second byte to write</param>
		/// <returns><c>true</c> if the buffer was large enough; otherwise, <c>false</c>.</returns>
		internal static bool TryWriteBytes(Span<byte> destination, out int bytesWritten, byte byte1, byte byte2)
		{
			if (destination.Length < 2)
			{
				bytesWritten = 0;
				return false;
			}

			destination[0] = byte1;
			destination[1] = byte2;
			bytesWritten = 2;
			return true;
		}

		/// <summary>Writes three bytes at the start of the destination buffer, if it is large enough.</summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="bytesWritten">Receives <c>3</c> if the operation was successful; otherwise, <c>0</c></param>
		/// <param name="byte1">First byte to write</param>
		/// <param name="byte2">Second byte to write</param>
		/// <param name="byte3">Third byte to write</param>
		/// <returns><c>true</c> if the buffer was large enough; otherwise, <c>false</c>.</returns>
		internal static bool TryWriteBytes(Span<byte> destination, out int bytesWritten, byte byte1, byte byte2, byte byte3)
		{
			if (destination.Length < 3)
			{
				bytesWritten = 0;
				return false;
			}

			destination[0] = byte1;
			destination[1] = byte2;
			destination[2] = byte3;
			bytesWritten = 3;
			return true;
		}

		/// <summary>Writes four bytes at the start of the destination buffer, if it is large enough.</summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="bytesWritten">Receives <c>4</c> if the operation was successful; otherwise, <c>0</c></param>
		/// <param name="byte1">First byte to write</param>
		/// <param name="byte2">Second byte to write</param>
		/// <param name="byte3">Third byte to write</param>
		/// <param name="byte4">Third byte to write</param>
		/// <returns><c>true</c> if the buffer was large enough; otherwise, <c>false</c>.</returns>
		internal static bool TryWriteBytes(Span<byte> destination, out int bytesWritten, byte byte1, byte byte2, byte byte3, byte byte4)
		{
			if (destination.Length < 4)
			{
				bytesWritten = 0;
				return false;
			}

			destination[0] = byte1;
			destination[1] = byte2;
			destination[2] = byte3;
			destination[3] = byte4;
			bytesWritten = 4;
			return true;
		}

		/// <summary>Writes a span of bytes at the start of the destination buffer, if it is large enough.</summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="bytesWritten">Receives the length of <paramref name="bytes"/> if the operation was successful; otherwise, <c>0</c></param>
		/// <param name="bytes">Span of bytes to write</param>
		/// <returns><c>true</c> if the buffer was large enough; otherwise, <c>false</c>.</returns>
		internal static bool TryWriteBytes(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> bytes)
		{
			if (!bytes.TryCopyTo(destination))
			{
				bytesWritten = 0;
				return false;
			}
			bytesWritten = bytes.Length;
			return true;
		}

		#endregion

		/// <summary>Encodes raw binary values (<see cref="Slice"/>, spans or arrays of bytes, ...)</summary>
		public readonly struct RawEncoder : 
			ISpanEncoder<Slice>, ISpanDecoder<Slice>,
			ISpanEncoder<byte[]>, ISpanDecoder<byte[]>,
			ISpanEncoder<ReadOnlyMemory<byte>>, ISpanDecoder<ReadOnlyMemory<byte>>,
			ISpanEncoder<MemoryStream>, ISpanDecoder<MemoryStream>
#if NET9_0_OR_GREATER
			, ISpanEncoder<ReadOnlySpan<byte>>
#endif
		{

			#region Slice...

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in Slice value, out ReadOnlySpan<byte> span)
			{
				span = value.Span;
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in Slice value, out int sizeHint)
			{
				sizeHint = value.Count;
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in Slice value)
			{
				return value.TryCopyTo(destination, out bytesWritten);
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out Slice value)
			{
				value = Slice.FromBytes(source);
				return true;
			}

			#endregion

			#region ReadOnlySpan<byte>...

#if NET5_0_OR_GREATER
			/// <inheritdoc />
#else
			/// <summary>Returns a span of the encoded representation of the value, if it can be done without any memory allocations</summary>
			/// <param name="value">Value to encode</param>
			/// <param name="span">Receives a span with the encoded value</param>
			/// <returns><c>true</c> if the span is available; otherwise, <c>false</c></returns>
			/// <remarks>
			/// <para>This should only return true when the in-memory layout of the value is the same as its encoded representation.</para>
			/// <para>If this method returns <c>false</c>, use <see cref="TryEncode(Span{byte}, out int, in ReadOnlySpan{byte})"/> to encode the value into a temporary buffer.</para>
			/// </remarks>
#endif
			public static bool TryGetSpan(scoped in ReadOnlySpan<byte> value, out ReadOnlySpan<byte> span)
			{
				span = value;
				return true;
			}

#if NET5_0_OR_GREATER
			/// <inheritdoc />
#else
			/// <summary>Returns a hint for the minimum capacity required to format the value.</summary>
			/// <param name="value">Value that needs to be encoded</param>
			/// <param name="sizeHint">Receives the minimum buffer size that should be passed to <see cref="TryEncode(Span{byte}, out int, in ReadOnlySpan{byte})"/></param>
			/// <returns><c>true</c> if a minimum size is known, or <c>false</c> if computing this size would be too costly</returns>
			/// <remarks>
			/// <para>The returned capacity <b>MAY</b> be smaller than the actual size required: some encoders may return a good estimate for 99%+ of the cases, in which case the capacity is a good starting point.</para>
			/// <para>For example, a UTF-8 encoder may assume that each character will take 2 bytes <i>on average</i>, which is smaller than the maximum of 3 bytes.</para>
			/// </remarks>
#endif
			public static bool TryGetSizeHint(in ReadOnlySpan<byte> value, out int sizeHint)
			{
				sizeHint = value.Length;
				return true;
			}

#if NET6_0_OR_GREATER
			/// <inheritdoc />
#else
			/// <summary>Encodes the value to the destination buffer, if it is large enough.</summary>
			/// <param name="destination">Destination buffer that will receive the encoded representation of the value</param>
			/// <param name="bytesWritten">Number of bytes that where written to the buffer, if the operation is successful</param>
			/// <param name="value">Value to encode</param>
			/// <returns><c>false</c> if the buffer is not large enough, <c>true</c> if the operation was successful, or an exception if the encoding failed for other reasons</returns>
			/// <remarks>
			/// <para>This method behaves similarly to <see cref="ISpanFormattable.TryFormat"/>: the caller allocates a buffer with a safe initial capacity. If the buffer is too small, then the caller should retry with a larger buffer, until the method returns <c>true</c> or fails.</para>
			/// <para>Please note that the method MUST NOT return <c>false</c> for a reason other than a buffer being too small, otherwise the caller may end up in an infinite retry loop, passing a larger and larger buffer.</para>
			/// </remarks>
#endif
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ReadOnlySpan<byte> value)
			{
				return value.TryCopyTo(destination, out bytesWritten);
			}

			#endregion

			#region ReadOnlyMemory<byte>...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in ReadOnlyMemory<byte> value, out ReadOnlySpan<byte> span)
			{
				span = value.Span;
				return true;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in ReadOnlyMemory<byte> value, out int sizeHint)
			{
				sizeHint = value.Length;
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ReadOnlyMemory<byte> value)
			{
				return value.Span.TryCopyTo(destination, out bytesWritten);
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out ReadOnlyMemory<byte> value)
			{
				value = Slice.FromBytes(source).Memory;
				return true;
			}

			#endregion

			#region byte[]...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in byte[]? value, out ReadOnlySpan<byte> span)
			{
				span = value;
				return true;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in byte[]? value, out int sizeHint)
			{
				sizeHint = value?.Length ?? 0;
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in byte[]? value)
			{
				if (value is null || value.Length == 0)
				{
					bytesWritten = 0;
					return true;
				}
				return value.TryCopyTo(destination, out bytesWritten);
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out byte[] value)
			{
				value = source.ToArray();
				return true;
			}

			#endregion

			#region MemoryStream...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in MemoryStream? value, out ReadOnlySpan<byte> span)
			{
				if (value is null)
				{
					span = default;
					return true;
				}

				if (value.TryGetBuffer(out var buffer))
				{
					span = buffer.Array.AsSpan(buffer.Offset, buffer.Count);
					return true;
				}

				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in MemoryStream? value, out int sizeHint)
			{
				if (value is null)
				{
					sizeHint = 0;
					return true;
				}

				if (value.TryGetBuffer(out var buffer))
				{
					sizeHint = buffer.Count;
					return true;
				}

				sizeHint = 0;
				return false;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in MemoryStream? value)
			{
				if (value is null)
				{
					bytesWritten = 0;
					return true;
				}

				long longLength = value.Length;
				if (longLength > int.MaxValue)
				{ // this should never happen, but if it does, we have to throw!
					throw new InvalidOperationException("Stream cannot exceed 2GiB.");
				}

				int length = unchecked((int) longLength);
				if (destination.Length < length)
				{
					bytesWritten = 0;
					return false;
				}

				if (value.TryGetBuffer(out var buffer))
				{
					buffer.Array.AsSpan(buffer.Offset, buffer.Count).CopyTo(destination);
				}
				else
				{
#if NET5_0_OR_GREATER
					var remaining = destination[..length];
					while (remaining.Length > 0)
					{
						int n = value.Read(remaining);
						if (n == 0) throw new InvalidOperationException("Failed to read the stream.");
						remaining = remaining[n..];
					}
#else
					// Stream.Read(Span<byte>) is not available on netstandard2.0: read through a pooled temporary array
					var tmp = ArrayPool<byte>.Shared.Rent(length);
					try
					{
						int offset = 0;
						while (offset < length)
						{
							int n = value.Read(tmp, offset, length - offset);
							if (n == 0) throw new InvalidOperationException("Failed to read the stream.");
							offset += n;
						}
						tmp.AsSpan(0, length).CopyTo(destination);
					}
					finally
					{
						ArrayPool<byte>.Shared.Return(tmp);
					}
#endif
				}

				bytesWritten = length;
				return true;
			}


			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out MemoryStream value)
			{
				value = new(source.ToArray(), 0, source.Length, writable: false, publiclyVisible: true);
				return true;
			}

			#endregion

		}

		/// <summary>Encodes tuples using the Tuple Layer Encoding</summary>
		/// <typeparam name="TTuple">Type of the tuple to encode (must implement <see cref="IVarTuple"/>)</typeparam>
		/// <seealso cref="TuPack"/>
		public readonly struct TupleEncoder<TTuple> : ISpanEncoder<TTuple>
			where TTuple : IVarTuple
		{

			#region IVarTuple...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in TTuple? value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in TTuple? value, out int sizeHint)
			{
				sizeHint = 0;
				return false;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in TTuple? value)
			{
				if (value is null or STuple)
				{
					bytesWritten = 0;
					return true;
				}

				return TuPack.TryPackTo(destination, out bytesWritten, in value);
			}

			#endregion

			//TODO: maybe support ValueTuple<...>?
		}

		/// <summary>Encodes strings as UTF-8 bytes</summary>
		/// <remarks>
		/// <para>This encoder can produce larger values than <see cref="Utf16Encoder"/> when the encoded text contains mostly non-Latin text (which will use up to 3 bytes per character instead of 2)</para>
		/// </remarks>
		public readonly struct Utf8Encoder : 
			ISpanEncoder<string>, ISpanDecoder<string>,
			ISpanEncoder<ReadOnlyMemory<char>>, ISpanDecoder<ReadOnlyMemory<char>>,
			ISpanEncoder<StringBuilder>
#if NET9_0_OR_GREATER
			, ISpanEncoder<ReadOnlySpan<char>>
#endif
		{

			#region String...

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in string? value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return string.IsNullOrEmpty(value);
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in string? value, out int sizeHint)
			{
				sizeHint = Encoding.UTF8.GetByteCount(value ?? "");
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in string? value)
			{
#if NET8_0_OR_GREATER
				return Encoding.UTF8.TryGetBytes(value, destination, out bytesWritten);
#else
				// Encoding.TryGetBytes is not available on netstandard2.0: measure first, then encode via the span polyfill
				if (Encoding.UTF8.GetByteCount(value ?? "") > destination.Length)
				{
					bytesWritten = 0;
					return false;
				}
				bytesWritten = Encoding.UTF8.GetBytes(value.AsSpan(), destination);
				return true;
#endif
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out string? value)
			{
				value = Encoding.UTF8.GetString(source);
				return true;
			}

			#endregion

			#region ReadOnlySpan<char>...

			/// <inheritdoc cref="TryGetSpan(in string?,out ReadOnlySpan{byte})" />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in ReadOnlySpan<char> value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return value.Length == 0;
			}

			/// <inheritdoc cref="TryGetSizeHint(in string?,out int)" />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in ReadOnlySpan<char> value, out int sizeHint)
			{
				sizeHint = Encoding.UTF8.GetByteCount(value);
				return true;
			}

			/// <inheritdoc cref="TryEncode(System.Span{byte},out int,in string?)" />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ReadOnlySpan<char> value)
			{
#if NET8_0_OR_GREATER
				return Encoding.UTF8.TryGetBytes(value, destination, out bytesWritten);
#else
				// Encoding.TryGetBytes is not available on netstandard2.0: measure first, then encode via the span polyfill
				if (Encoding.UTF8.GetByteCount(value) > destination.Length)
				{
					bytesWritten = 0;
					return false;
				}
				bytesWritten = Encoding.UTF8.GetBytes(value, destination);
				return true;
#endif
			}

			#endregion

			#region ReadOnlyMemory<char>...

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in ReadOnlyMemory<char> value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return value.Length == 0;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in ReadOnlyMemory<char> value, out int sizeHint)
			{
				sizeHint = Encoding.UTF8.GetByteCount(value.Span);
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ReadOnlyMemory<char> value)
			{
#if NET8_0_OR_GREATER
				return Encoding.UTF8.TryGetBytes(value.Span, destination, out bytesWritten);
#else
				// Encoding.TryGetBytes is not available on netstandard2.0: measure first, then encode via the span polyfill
				if (Encoding.UTF8.GetByteCount(value.Span) > destination.Length)
				{
					bytesWritten = 0;
					return false;
				}
				bytesWritten = Encoding.UTF8.GetBytes(value.Span, destination);
				return true;
#endif
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out ReadOnlyMemory<char> value)
			{
				value = Encoding.UTF8.GetString(source).AsMemory();
				return true;
			}

			#endregion

			#region StringBuilder...

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in StringBuilder? value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return value is null || value.Length == 0;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in StringBuilder? value, out int sizeHint)
			{
				sizeHint = Encoding.UTF8.GetMaxByteCount(value?.Length ?? 0);
				return true;
			}

			/// <inheritdoc />
			[Pure]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in StringBuilder? value)
			{
				if (value is null || value.Length == 0)
				{
					bytesWritten = 0;
					return true;
				}

#if NET5_0_OR_GREATER
				int cursor = 0;
				foreach (var chunk in value.GetChunks())
				{
					if (!Encoding.UTF8.TryGetBytes(chunk.Span, destination[cursor..], out int len))
					{
						bytesWritten = 0;
						return false;
					}
					cursor = checked(cursor + len);
				}

				bytesWritten = cursor;
				return true;
#else
				// StringBuilder.GetChunks and Encoding.TryGetBytes are not available on netstandard2.0: encode the materialized string
				var literal = value.ToString();
				if (Encoding.UTF8.GetByteCount(literal) > destination.Length)
				{
					bytesWritten = 0;
					return false;
				}
				bytesWritten = Encoding.UTF8.GetBytes(literal.AsSpan(), destination);
				return true;
#endif
			}

			#endregion

		}

		/// <summary>Encodes strings as UTF-16 bytes</summary>
		/// <remarks>
		/// <para>Each character will be encoded as 2 bytes (in little-endian)</para>
		/// <para>This encoder can produce more compact values than <see cref="Utf8Encoder"/> when the text contains mostly non-Latin text (which would use up to 3 bytes per character)</para>
		/// </remarks>
		public readonly struct Utf16Encoder : 
			ISpanEncoder<string>, ISpanDecoder<string>,
			ISpanEncoder<ReadOnlyMemory<char>>, ISpanDecoder<ReadOnlyMemory<char>>,
			ISpanEncoder<StringBuilder>
#if NET9_0_OR_GREATER
			, ISpanEncoder<ReadOnlySpan<char>>
#endif
		{

			#region String...

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in string? value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return string.IsNullOrEmpty(value);
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in string? value, out int sizeHint)
			{
				sizeHint = Encoding.Unicode.GetByteCount(value ?? "");
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in string? value)
			{
				return Encoding.Unicode.TryGetBytes(value, destination, out bytesWritten);
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out string? value)
			{
				value = Encoding.Unicode.GetString(source);
				return true;
			}

			#endregion

			#region ReadOnlySpan<char>...

			/// <inheritdoc cref="TryGetSpan(in string?,out ReadOnlySpan{byte})" />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in ReadOnlySpan<char> value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return value.Length == 0;
			}

			/// <inheritdoc cref="TryGetSizeHint(in string?,out int)" />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in ReadOnlySpan<char> value, out int sizeHint)
			{
				sizeHint = Encoding.Unicode.GetByteCount(value);
				return true;
			}

			/// <inheritdoc cref="TryEncode(System.Span{byte},out int,in string?)" />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ReadOnlySpan<char> value)
			{
				return Encoding.Unicode.TryGetBytes(value, destination, out bytesWritten);
			}

			#endregion

			#region ReadOnlyMemory<char>...

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in ReadOnlyMemory<char> value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return value.Length == 0;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in ReadOnlyMemory<char> value, out int sizeHint)
			{
				sizeHint = Encoding.Unicode.GetByteCount(value.Span);
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ReadOnlyMemory<char> value)
			{
				return Encoding.Unicode.TryGetBytes(value.Span, destination, out bytesWritten);
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out ReadOnlyMemory<char> value)
			{
				value = Encoding.Unicode.GetString(source).AsMemory();
				return true;
			}

			#endregion

			#region StringBuilder...

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in StringBuilder? value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return value is null || value.Length == 0;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in StringBuilder? value, out int sizeHint)
			{
				sizeHint = Encoding.Unicode.GetMaxByteCount(value?.Length ?? 0);
				return true;
			}

			/// <inheritdoc />
			[Pure]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in StringBuilder? value)
			{
				if (value is null || value.Length == 0)
				{
					bytesWritten = 0;
					return true;
				}

#if NET5_0_OR_GREATER
				int cursor = 0;
				foreach (var chunk in value.GetChunks())
				{
					if (!Encoding.Unicode.TryGetBytes(chunk.Span, destination[cursor..], out int len))
					{
						bytesWritten = 0;
						return false;
					}
					cursor = checked(cursor + len);
				}

				bytesWritten = cursor;
				return true;
#else
				// StringBuilder.GetChunks and Encoding.TryGetBytes are not available on netstandard2.0: encode the materialized string
				var literal = value.ToString();
				if (Encoding.Unicode.GetByteCount(literal) > destination.Length)
				{
					bytesWritten = 0;
					return false;
				}
				bytesWritten = Encoding.Unicode.GetBytes(literal.AsSpan(), destination);
				return true;
#endif
			}

			#endregion

		}

		/// <summary>Encodes primitive integers into little-endian, using a fixed size</summary>
		public readonly struct FixedSizeLittleEndianEncoder :
			ISpanEncoder<int>, ISpanDecoder<int>,
			ISpanEncoder<uint>, ISpanDecoder<uint>,
			ISpanEncoder<long>, ISpanDecoder<long>,
			ISpanEncoder<ulong>, ISpanDecoder<ulong>,
			ISpanEncoder<float>, ISpanDecoder<float>,
			ISpanEncoder<double>, ISpanDecoder<double>
#if NET5_0_OR_GREATER
			, ISpanEncoder<Half>, ISpanDecoder<Half>
#endif
#if NET7_0_OR_GREATER
			, ISpanEncoder<Int128>, ISpanDecoder<Int128>
			, ISpanEncoder<UInt128>, ISpanDecoder<UInt128>
#endif
		{

			#region Int32...

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in int value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in int value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<int>();
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in int value)
			{
				if (destination.Length < Unsafe.SizeOf<int>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteInt32LittleEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<int>();
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out int value)
				=> BinaryPrimitives.TryReadInt32LittleEndian(source, out value);

			#endregion

			#region UInt32...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in uint value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in uint value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<uint>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in uint value)
			{
				if (destination.Length < Unsafe.SizeOf<uint>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<uint>();
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out uint value)
				=> BinaryPrimitives.TryReadUInt32LittleEndian(source, out value);

			#endregion

			#region Int64...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in long value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in long value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<long>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in long value)
			{
				if (destination.Length < Unsafe.SizeOf<long>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteInt64LittleEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<long>();
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out long value)
				=> BinaryPrimitives.TryReadInt64LittleEndian(source, out value);

			#endregion

			#region UInt64...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in ulong value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in ulong value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<ulong>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ulong value)
			{
				if (destination.Length < Unsafe.SizeOf<ulong>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteUInt64LittleEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<ulong>();
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out ulong value)
				=> BinaryPrimitives.TryReadUInt64LittleEndian(source, out value);

			#endregion

#if NET7_0_OR_GREATER

			#region Int128...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in Int128 value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in Int128 value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<Int128>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in Int128 value)
			{
				if (destination.Length < Unsafe.SizeOf<Int128>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteInt128LittleEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<Int128>();
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out Int128 value)
				=> BinaryPrimitives.TryReadInt128LittleEndian(source, out value);

			#endregion

			#region UInt128...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in UInt128 value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in UInt128 value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<UInt128>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in UInt128 value)
			{
				if (destination.Length < Unsafe.SizeOf<UInt128>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteUInt128LittleEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<UInt128>();
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out UInt128 value)
				=> BinaryPrimitives.TryReadUInt128LittleEndian(source, out value);

			#endregion

#endif

			#region Single...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in float value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in float value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<float>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in float value)
			{
				if (destination.Length < Unsafe.SizeOf<float>())
				{
					bytesWritten = 0;
					return false;
				}

#if NET5_0_OR_GREATER
				BinaryPrimitives.WriteSingleLittleEndian(destination, value);
#else
				// BinaryPrimitives.WriteSingleLittleEndian is not available on netstandard2.0
				SnowBank.Compat.BinaryPrimitivesCompat.WriteSingleLittleEndian(destination, value);
#endif
				bytesWritten = Unsafe.SizeOf<float>();
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
			public static bool TryDecode(ReadOnlySpan<byte> source, out float value)
				=> BinaryPrimitives.TryReadSingleLittleEndian(source, out value);
#else
			// BinaryPrimitives.TryReadSingleLittleEndian is not available on netstandard2.0: check the length, then use the compat shim
			public static bool TryDecode(ReadOnlySpan<byte> source, out float value)
			{
				if (source.Length < Unsafe.SizeOf<float>())
				{
					value = 0;
					return false;
				}
				value = SnowBank.Compat.BinaryPrimitivesCompat.ReadSingleLittleEndian(source);
				return true;
			}
#endif

			#endregion

			#region Double...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in double value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in double value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<double>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in double value)
			{
				if (destination.Length < Unsafe.SizeOf<double>())
				{
					bytesWritten = 0;
					return false;
				}

#if NET5_0_OR_GREATER
				BinaryPrimitives.WriteDoubleLittleEndian(destination, value);
#else
				// BinaryPrimitives.WriteDoubleLittleEndian is not available on netstandard2.0
				SnowBank.Compat.BinaryPrimitivesCompat.WriteDoubleLittleEndian(destination, value);
#endif
				bytesWritten = Unsafe.SizeOf<double>();
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
			public static bool TryDecode(ReadOnlySpan<byte> source, out double value)
				=> BinaryPrimitives.TryReadDoubleLittleEndian(source, out value);
#else
			// BinaryPrimitives.TryReadDoubleLittleEndian is not available on netstandard2.0: check the length, then use the compat shim
			public static bool TryDecode(ReadOnlySpan<byte> source, out double value)
			{
				if (source.Length < Unsafe.SizeOf<double>())
				{
					value = 0;
					return false;
				}
				value = SnowBank.Compat.BinaryPrimitivesCompat.ReadDoubleLittleEndian(source);
				return true;
			}
#endif

			#endregion

#if NET5_0_OR_GREATER

			#region Half...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in Half value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in Half value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<Half>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in Half value)
			{
				if (destination.Length < Unsafe.SizeOf<Half>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteHalfLittleEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<Half>();
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out Half value)
				=> BinaryPrimitives.TryReadHalfLittleEndian(source, out value);

			#endregion

#endif

		}

		/// <summary>Encodes primitive integers into big-endian, using a fixed size</summary>
		public readonly struct FixedSizeBigEndianEncoder :
			ISpanEncoder<int>, ISpanDecoder<int>,
			ISpanEncoder<uint>, ISpanDecoder<uint>,
			ISpanEncoder<long>, ISpanDecoder<long>,
			ISpanEncoder<ulong>, ISpanDecoder<ulong>,
			ISpanEncoder<float>, ISpanDecoder<float>,
			ISpanEncoder<double>, ISpanDecoder<double>
#if NET5_0_OR_GREATER
			, ISpanEncoder<Half>, ISpanDecoder<Half>
#endif
#if NET7_0_OR_GREATER
			, ISpanEncoder<Int128>, ISpanDecoder<Int128>
			, ISpanEncoder<UInt128>, ISpanDecoder<UInt128>
#endif
		{

			#region Int32...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in int value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in int value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<int>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in int value)
			{
				if (destination.Length < Unsafe.SizeOf<int>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteInt32BigEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<int>();
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out int value)
				=> BinaryPrimitives.TryReadInt32BigEndian(source, out value);

			#endregion

			#region UInt32...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in uint value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in uint value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<uint>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in uint value)
			{
				if (destination.Length < Unsafe.SizeOf<uint>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteUInt32BigEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<uint>();
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out uint value)
				=> BinaryPrimitives.TryReadUInt32BigEndian(source, out value);

			#endregion

			#region Int64...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in long value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in long value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<long>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in long value)
			{
				if (destination.Length < Unsafe.SizeOf<long>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteInt64BigEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<long>();
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out long value)
				=> BinaryPrimitives.TryReadInt64BigEndian(source, out value);

			#endregion

			#region UInt64...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in ulong value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in ulong value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<ulong>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ulong value)
			{
				if (destination.Length < Unsafe.SizeOf<ulong>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteUInt64BigEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<ulong>();
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out ulong value)
				=> BinaryPrimitives.TryReadUInt64BigEndian(source, out value);

			#endregion

#if NET7_0_OR_GREATER

			#region Int128...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in Int128 value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in Int128 value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<Int128>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in Int128 value)
			{
				if (destination.Length < Unsafe.SizeOf<Int128>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteInt128BigEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<Int128>();
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out Int128 value)
				=> BinaryPrimitives.TryReadInt128BigEndian(source, out value);

			#endregion

			#region UInt128...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in UInt128 value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in UInt128 value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<UInt128>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in UInt128 value)
			{
				if (destination.Length < Unsafe.SizeOf<UInt128>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteUInt128BigEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<UInt128>();
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out UInt128 value)
				=> BinaryPrimitives.TryReadUInt128BigEndian(source, out value);

			#endregion

#endif

			#region Single...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in float value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in float value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<float>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in float value)
			{
				if (destination.Length < Unsafe.SizeOf<float>())
				{
					bytesWritten = 0;
					return false;
				}

#if NET5_0_OR_GREATER
				BinaryPrimitives.WriteSingleBigEndian(destination, value);
#else
				// BinaryPrimitives.WriteSingleBigEndian is not available on netstandard2.0
				SnowBank.Compat.BinaryPrimitivesCompat.WriteSingleBigEndian(destination, value);
#endif
				bytesWritten = Unsafe.SizeOf<float>();
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
			public static bool TryDecode(ReadOnlySpan<byte> source, out float value)
				=> BinaryPrimitives.TryReadSingleBigEndian(source, out value);
#else
			// BinaryPrimitives.TryReadSingleBigEndian is not available on netstandard2.0: check the length, then use the compat shim
			public static bool TryDecode(ReadOnlySpan<byte> source, out float value)
			{
				if (source.Length < Unsafe.SizeOf<float>())
				{
					value = 0;
					return false;
				}
				value = SnowBank.Compat.BinaryPrimitivesCompat.ReadSingleBigEndian(source);
				return true;
			}
#endif

			#endregion

			#region Double...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in double value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in double value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<double>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in double value)
			{
				if (destination.Length < Unsafe.SizeOf<double>())
				{
					bytesWritten = 0;
					return false;
				}

#if NET5_0_OR_GREATER
				BinaryPrimitives.WriteDoubleBigEndian(destination, value);
#else
				// BinaryPrimitives.WriteDoubleBigEndian is not available on netstandard2.0
				SnowBank.Compat.BinaryPrimitivesCompat.WriteDoubleBigEndian(destination, value);
#endif
				bytesWritten = Unsafe.SizeOf<double>();
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET5_0_OR_GREATER
			public static bool TryDecode(ReadOnlySpan<byte> source, out double value)
				=> BinaryPrimitives.TryReadDoubleBigEndian(source, out value);
#else
			// BinaryPrimitives.TryReadDoubleBigEndian is not available on netstandard2.0: check the length, then use the compat shim
			public static bool TryDecode(ReadOnlySpan<byte> source, out double value)
			{
				if (source.Length < Unsafe.SizeOf<double>())
				{
					value = 0;
					return false;
				}
				value = SnowBank.Compat.BinaryPrimitivesCompat.ReadDoubleBigEndian(source);
				return true;
			}
#endif

			#endregion

#if NET5_0_OR_GREATER

			#region Half...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in Half value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in Half value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<Half>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in Half value)
			{
				if (destination.Length < Unsafe.SizeOf<Half>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteHalfBigEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<Half>();
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out Half value)
				=> BinaryPrimitives.TryReadHalfBigEndian(source, out value);

			#endregion

#endif

		}

		/// <summary>Encodes UUID values into big-endian, using a fixed size</summary>
		public readonly struct FixedSizeUuidEncoder :
			ISpanEncoder<Guid>, ISpanDecoder<Guid>,
			ISpanEncoder<Uuid128>, ISpanDecoder<Uuid128>,
			ISpanEncoder<Uuid96>, ISpanDecoder<Uuid96>,
			ISpanEncoder<Uuid80>, ISpanDecoder<Uuid80>,
			ISpanEncoder<Uuid64>, ISpanDecoder<Uuid64>,
			ISpanEncoder<Uuid48>, ISpanDecoder<Uuid48>,
			ISpanEncoder<VersionStamp>, ISpanDecoder<VersionStamp>
		{

			#region Guid...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in Guid value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in Guid value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<Guid>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in Guid value)
			{
				return new Uuid128(value).TryWriteTo(destination, out bytesWritten);
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out Guid value)
			{
				if (source.Length < Uuid128.SizeOf)
				{
					value = Guid.Empty;
					return false;
				}

				value = (Guid) Uuid128.Read(source);
				return true;
			}

			#endregion

			#region Uuid128...

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in Uuid128 value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in Uuid128 value, out int sizeHint)
			{
				sizeHint = Uuid128.SizeOf;
				return true;
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in Uuid128 value)
			{
				return value.TryWriteTo(destination, out bytesWritten);
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out Uuid128 value)
			{
				if (source.Length < Uuid128.SizeOf)
				{
					value = default;
					return false;
				}

				value = Uuid128.Read(source);
				return true;
			}

			#endregion

			#region Uuid96...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in Uuid96 value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in Uuid96 value, out int sizeHint)
			{
				sizeHint = Uuid96.SizeOf;
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in Uuid96 value)
			{
				return value.TryWriteTo(destination, out bytesWritten);
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out Uuid96 value)
			{
				if (source.Length < Uuid96.SizeOf)
				{
					value = default;
					return false;
				}

				value = Uuid96.Read(source);
				return true;
			}

			#endregion

			#region Uuid80...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in Uuid80 value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in Uuid80 value, out int sizeHint)
			{
				sizeHint = Uuid80.SizeOf;
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in Uuid80 value)
			{
				return value.TryWriteTo(destination, out bytesWritten);
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out Uuid80 value)
			{
				if (source.Length < Uuid80.SizeOf)
				{
					value = default;
					return false;
				}

				value = Uuid80.Read(source);
				return true;
			}

			#endregion

			#region Uuid64...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in Uuid64 value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in Uuid64 value, out int sizeHint)
			{
				sizeHint = Uuid64.SizeOf;
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in Uuid64 value)
			{
				return value.TryWriteTo(destination, out bytesWritten);
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out Uuid64 value)
			{
				if (source.Length < Uuid64.SizeOf)
				{
					value = default;
					return false;
				}

				value = Uuid64.Read(source);
				return true;
			}

			#endregion

			#region Uuid48...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in Uuid48 value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in Uuid48 value, out int sizeHint)
			{
				sizeHint = Uuid48.SizeOf;
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in Uuid48 value)
			{
				return value.TryWriteTo(destination, out bytesWritten);
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out Uuid48 value)
			{
				if (source.Length < Uuid48.SizeOf)
				{
					value = default;
					return false;
				}

				value = Uuid48.Read(source);
				return true;
			}

			#endregion

			#region VersionStamp...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in VersionStamp value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in VersionStamp value, out int sizeHint)
			{
				sizeHint = value.HasUserVersion ? 12 : 10;
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in VersionStamp value)
			{
				return value.TryWriteTo(destination, out bytesWritten);
			}

			
			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryDecode(ReadOnlySpan<byte> source, out VersionStamp value)
			{
				if (source.Length < 10)
				{
					value = default;
					return false;
				}

				value = VersionStamp.ReadFrom(source); // will throw if not 10 or 12 bytes
				return true;
			}

			#endregion

		}

		/// <summary>Encodes primitive integers into little-endian, using as few bytes as possible</summary>
		public readonly struct CompactLittleEndianEncoder : ISpanEncoder<short>, ISpanEncoder<ushort>, ISpanEncoder<int>, ISpanEncoder<uint>, ISpanEncoder<long>, ISpanEncoder<ulong>
		{

			#region Int16...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in short value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in short value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<short>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in short value)
			{
				//note: negative values will be encoded as 4 bytes
				return TryEncode(destination, out bytesWritten, unchecked((ushort) value));
			}

			#endregion

			#region UInt16...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in ushort value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in ushort value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<ushort>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ushort value)
			{
				switch (value)
				{
					case <= 0xFF:
					{
						if (destination.Length < 1) goto too_small;
						destination[0] = unchecked((byte) value);
						bytesWritten = 1;
						return true;
					}
					default:
					{
						if (destination.Length < 2) goto too_small;
						BinaryPrimitives.WriteUInt16LittleEndian(destination, value);
						bytesWritten = 2;
						return true;
					}
				}

			too_small:
				bytesWritten = 0;
				return false;
			}

			#endregion

			#region Int32...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in int value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in int value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<int>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in int value)
			{
				//note: negative values will be encoded as 4 bytes
				return TryEncode(destination, out bytesWritten, unchecked((uint) value));
			}

			#endregion

			#region UInt32...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in uint value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in uint value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<uint>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in uint value)
			{
				switch (value)
				{
					case <= 0xFF:
					{
						if (destination.Length < 1) goto too_small;
						destination[0] = unchecked((byte) value);
						bytesWritten = 1;
						return true;
					}
					case <= 0xFFFF:
					{
						if (destination.Length < 2) goto too_small;
						destination[0] = unchecked((byte) value);
						destination[1] = (byte) (value >> 8);
						bytesWritten = 2;
						return true;
					}
					case <= 0xFFFFFF:
					{
						if (destination.Length < 3) goto too_small;
						destination[0] = unchecked((byte) value);
						destination[1] = unchecked((byte) (value >> 8));
						destination[2] = unchecked((byte) (value >> 16));
						bytesWritten = 3;
						return true;
					}
					default:
					{
						if (destination.Length < 4) goto too_small;
						BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
						bytesWritten = 4;
						return true;
					}
				}

			too_small:
				bytesWritten = 0;
				return false;
			}

			#endregion

			#region Int64...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in long value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in long value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<long>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in long value)
			{
				//note: negative values will be written as 8 bytes
				return TryEncode(destination, out bytesWritten, unchecked((ulong) value));
			}

			#endregion

			#region UInt64...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in ulong value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in ulong value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<ulong>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ulong value)
			{
				if (value <= uint.MaxValue)
				{ // smaller than 32 bits
					return TryEncode(destination, out bytesWritten, unchecked((uint) value));
				}

				// first write the 32 lower bits
				if (destination.Length < 5)
				{
					goto too_small;
				}
				BinaryPrimitives.WriteUInt32LittleEndian(destination, unchecked((uint) value));

				// then write the 32 upper bits
				if (!TryEncode(destination[4..], out int len, (uint) (value >> 32)))
				{
					goto too_small;
				}

				bytesWritten = 4 + len;
				return true;

			too_small:
				bytesWritten = 0;
				return false;
			}

			#endregion

		}

		/// <summary>Encodes primitive integers into little-endian, using as few bytes as possible</summary>
		public readonly struct CompactBigEndianEncoder : ISpanEncoder<short>, ISpanEncoder<ushort>, ISpanEncoder<int>, ISpanEncoder<uint>, ISpanEncoder<long>, ISpanEncoder<ulong>
		{

			#region Int16...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in short value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in short value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<short>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in short value)
			{
				//note: negative values will be encoded as 4 bytes
				return TryEncode(destination, out bytesWritten, unchecked((ushort) value));
			}

			#endregion

			#region UInt16...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in ushort value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in ushort value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<ushort>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ushort value)
			{
				switch (value)
				{
					case <= 0xFF:
					{
						if (destination.Length < 1) goto too_small;
						destination[0] = unchecked((byte) value);
						bytesWritten = 1;
						return true;
					}
					default:
					{
						if (destination.Length < 2) goto too_small;
						BinaryPrimitives.WriteUInt16BigEndian(destination, value);
						bytesWritten = 2;
						return true;
					}
				}

			too_small:
				bytesWritten = 0;
				return false;
			}

			#endregion

			#region Int32...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in int value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in int value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<int>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in int value)
			{
				//note: negative values will be encoded as 4 bytes
				return TryEncode(destination, out bytesWritten, unchecked((uint) value));
			}

			#endregion

			#region UInt32...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in uint value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in uint value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<uint>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in uint value)
			{
				switch (value)
				{
					case <= 0xFF:
					{
						if (destination.Length < 1) goto too_small;
						destination[0] = unchecked((byte) value);
						bytesWritten = 1;
						return true;
					}
					case <= 0xFFFF:
					{
						if (destination.Length < 2) goto too_small;
						destination[0] = unchecked((byte) (value >> 8));
						destination[1] = unchecked((byte) value);
						bytesWritten = 2;
						return true;
					}
					case <= 0xFFFFFF:
					{
						if (destination.Length < 3) goto too_small;
						destination[0] = unchecked((byte) (value >> 16));
						destination[1] = unchecked((byte) (value >> 8));
						destination[2] = unchecked((byte) value);
						bytesWritten = 3;
						return true;
					}
					default:
					{
						if (destination.Length < 4) goto too_small;
						BinaryPrimitives.WriteUInt32BigEndian(destination, value);
						bytesWritten = 4;
						return true;
					}
				}

			too_small:
				bytesWritten = 0;
				return false;
			}

			#endregion

			#region Int64...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in long value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in long value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<long>();
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in long value)
			{
				//note: negative values will be written as 8 bytes
				return TryEncode(destination, out bytesWritten, unchecked((ulong) value));
			}

			#endregion

			#region UInt64...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in ulong value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSizeHint(in ulong value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<ulong>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ulong value)
			{
				if (value <= uint.MaxValue)
				{ // smaller than 32 bits
					return TryEncode(destination, out bytesWritten, unchecked((uint) value));
				}

				// first write the 32 upper bits
				if (!TryEncode(destination, out int len, unchecked((uint) (value >> 32))))
				{
					goto too_small;
				}

				// then write the 32 lower bits (fixed size)
				destination = destination[len..];
				if (destination.Length < Unsafe.SizeOf<uint>())
				{
					goto too_small;
				}
				BinaryPrimitives.WriteUInt32BigEndian(destination, unchecked((uint) value));

				bytesWritten = len + Unsafe.SizeOf<uint>();
				return true;

			too_small:
				bytesWritten = 0;
				return false;

			}

			#endregion

		}

		/// <summary>Encodes primitive integers using the VarInt compact representation</summary>
		public readonly struct VarIntEncoder : ISpanEncoder<ushort>, ISpanEncoder<uint>, ISpanEncoder<ulong>
		{

			#region Int16...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in ushort value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in ushort value, out int sizeHint)
			{
				sizeHint = (uint) value switch
				{
					< 1U << 7  => 1,
					< 1U << 14 => 2,
					_ => 3,
				};
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ushort value)
			{
				return value <= 127
					? TryWriteByte(destination, out bytesWritten, (byte) value)
					: TryEncodeSlow(destination, out bytesWritten, value);

				[MethodImpl(MethodImplOptions.NoInlining)]
				static bool TryEncodeSlow(Span<byte> destination, out int bytesWritten, uint value)
				{
					const uint MASK = 128;
					//note: value is known to be >= 128

					if (value < (1 << 14))
					{
						return TryWriteBytes(
							destination,
							out bytesWritten,
							(byte)(value | MASK),
							(byte)(value >> 7)
						);
					}

					return TryWriteBytes(
						destination,
						out bytesWritten,
						(byte)(value | MASK),
						(byte)((value >> 7) | MASK),
						(byte)(value >> 14)
					);
				}
			}

			#endregion

			#region Int32...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in uint value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in uint value, out int sizeHint)
			{
				sizeHint = value switch
				{
					< 1U << 7  => 1,
					< 1U << 14 => 2,
					< 1U << 21 => 3,
					< 1U << 28 => 4,
					_ => 5
				};
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in uint value)
			{
				return value <= 127
					? TryWriteByte(destination, out bytesWritten, (byte) value)
					: TryEncodeSlow(destination, out bytesWritten, value);

				[MethodImpl(MethodImplOptions.NoInlining)]
				static bool TryEncodeSlow(Span<byte> destination, out int bytesWritten, uint value)
				{
					const uint MASK = 128;
					//note: value is known to be >= 128

					if (value < (1 << 14))
					{
						return TryWriteBytes(
							destination,
							out bytesWritten,
							(byte)(value | MASK),
							(byte)(value >> 7)
						);
					}

					if (value < (1 << 21))
					{
						return TryWriteBytes(
							destination,
							out bytesWritten,
							(byte)(value | MASK),
							(byte)((value >> 7) | MASK),
							(byte)(value >> 14)
						);
					}

					if (value < (1 << 28))
					{
						return TryWriteBytes(
							destination,
							out bytesWritten,
							(byte)(value | MASK),
							(byte)((value >> 7) | MASK),
							(byte)((value >> 14) | MASK),
							(byte)(value >> 21)
						);
					}

					return TryWriteBytes(
						destination,
						out bytesWritten,
						[
							(byte) (value | MASK),
							(byte) ((value >> 7) | MASK),
							(byte) ((value >> 14) | MASK),
							(byte) ((value >> 21) | MASK),
							(byte) (value >> 28)
						]
					);
				}
			}

			#endregion

			#region Int64...

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryGetSpan(scoped in ulong value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in ulong value, out int sizeHint)
			{
				sizeHint = value switch
				{
					< 1UL << 7  => 1,
					< 1UL << 14 => 2,
					< 1UL << 21 => 3,
					< 1UL << 28 => 4,
					< 1UL << 35 => 5,
					< 1UL << 42 => 6,
					< 1UL << 49 => 7,
					< 1UL << 56 => 8,
					< 1UL << 63 => 9,
					_ => 10
				};
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ulong value)
			{
				if (value <= uint.MaxValue)
				{
					return TryEncode(destination, out bytesWritten, (uint) value);
				}

				return TryEncodeSlow(destination, out bytesWritten, value);

				[MethodImpl(MethodImplOptions.NoInlining)]
				static bool TryEncodeSlow(Span<byte> destination, out int bytesWritten, ulong value)
				{
					const uint MASK = 128;

					int p = 0;
					while (value >= MASK)
					{
						if (p >= destination.Length) goto too_small;
						destination[p++] = (byte) ((value & (MASK - 1)) | MASK);
						value >>= 7;
					}

					if (p >= destination.Length) goto too_small;
					destination[p++] = (byte) value;

					bytesWritten = p;
					return true;

				too_small:
					bytesWritten = 0;
					return false;
				}

			}

			#endregion

		}

	}

}
