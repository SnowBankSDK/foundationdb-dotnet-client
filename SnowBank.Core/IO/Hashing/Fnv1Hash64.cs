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

namespace SnowBank.IO.Hashing
{
	using System.Text;

	/// <summary>Helper for computing 64-bits FNV-1 hashes (FowlerNollVo)</summary>
	[PublicAPI]
	public static class Fnv1Hash64
	{
		// cf http://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
		// cf http://isthe.com/chongo/tech/comp/fnv/

		public const ulong FNV1_64_OFFSET_BASIS = 14695981039346656037;
		public const ulong FNV1_64_PRIME = 1099511628211;

		/*
			hash = FNV_offset_basis
			for each octet_of_data to be hashed
				hash = hash * FNV_prime
				hash = hash XOR octet_of_data
			return hash
		 */

		/// <summary>Computes the 64-bits FNV-1 hash of the utf-8 representation of a string</summary>
		/// <param name="text">String to process</param>
		/// <param name="ignoreCase">If <c>true</c>, the string is converted to lowercase (invariant) before conversion to utf-8</param>
		/// <param name="encoding">Encoding used to convert the string into bytes (utf-8 by default)</param>
		/// <returns>Corresponding FNV-1 64-bit hash</returns>
		public static ulong FromString(string text, bool ignoreCase, Encoding? encoding = null)
		{
			if (string.IsNullOrEmpty(text)) return 0;
			if (ignoreCase) text = text.ToLowerInvariant();
			byte[] bytes = (encoding ?? Encoding.UTF8).GetBytes(text);
			return FromBytes(bytes);
		}

		/// <summary>Computes the 64-bits FNV-1 hash of the utf-8 representation of a string</summary>
		/// <returns>Corresponding FNV-1 64-bit hash</returns>
		public static ulong FromChars(char[] buffer, int offset, int count, Encoding? encoding = null)
		{
			Contract.DoesNotOverflow(buffer, offset, count);
			if (count == 0) return FNV1_64_OFFSET_BASIS;
			var bytes = (encoding ?? Encoding.UTF8).GetBytes(buffer, offset, count);
			return Continue(FNV1_64_OFFSET_BASIS, bytes);
		}

		/// <summary>Computes the 64-bits FNV-1 hash of a byte array</summary>
		/// <param name="bytes">Bytes to process</param>
		/// <returns>Corresponding FNV-1 64-bit hash</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromBytes(byte[] bytes)
		{
			return Continue(FNV1_64_OFFSET_BASIS, bytes);
		}

		/// <summary>Computes the 64-bits FNV-1 hash of a byte array, with a custom initial seed</summary>
		/// <param name="seed">Initial seed (0 for default)</param>
		/// <param name="bytes">Bytes to process</param>
		/// <returns>Corresponding FNV-1 64-bit hash</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromBytes(ulong seed, byte[] bytes)
		{
			return Continue(seed ^ FNV1_64_OFFSET_BASIS, bytes);
		}

		/// <summary>Computes the 64-bit FNV-1 hash of a block of data</summary>
		/// <param name="bytes">Block of data</param>
		/// <returns>64-bit hash computed over the entire array</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromBytes(Slice bytes)
		{
			return Continue(FNV1_64_OFFSET_BASIS, bytes);
		}

		/// <summary>Computes the 64-bit FNV-1 hash over a segment of data</summary>
		/// <param name="bytes">Backing store for the buffer that contains the bytes to process</param>
		/// <param name="offset">Offset of the first byte to process</param>
		/// <param name="count">Number of bytes to process</param>
		/// <returns>Hash of the buffer section</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromBytes(byte[] bytes, int offset, int count)
		{
			return Continue(FNV1_64_OFFSET_BASIS, bytes, offset, count);
		}

		/// <summary>Continues computing the 64-bits FNV-1 hash on an additional chunk of data</summary>
		/// <param name="hash">Hash computed for all previous chunks, or the initial seed (<see cref="FNV1_64_OFFSET_BASIS"/>) if this is the first chunk.</param>
		/// <param name="bytes">New chunk of bytes to process</param>
		/// <returns>Updated FNV-1 64-bit hash that will include the additional data</returns>
		[Pure]
		public static ulong Continue(ulong hash, byte[] bytes)
		{
			Contract.Debug.Requires(bytes != null);
			if (bytes.Length == 0) return hash;

			foreach(var b in bytes)
			{
				hash = (hash * FNV1_64_PRIME) ^ b;
			}
			return hash;
		}

		/// <summary>Continues computing the 64-bits FNV-1 hash on an additional chunk of data</summary>
		/// <param name="hash">Hash computed for all previous chunks, or the initial seed (<see cref="FNV1_64_OFFSET_BASIS"/>) if this is the first chunk.</param>
		/// <param name="bytes">New chunk of bytes to process</param>
		/// <returns>Updated FNV-1 64-bit hash that will include the additional data</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong Continue(ulong hash, Slice bytes)
		{
			return Continue(hash, bytes.Array, bytes.Offset, bytes.Count);
		}

		/// <summary>Continues computing the 64-bits FNV-1 hash on an additional chunk of data</summary>
		/// <param name="hash">Hash computed for all previous chunks, or the initial seed (<see cref="FNV1_64_OFFSET_BASIS"/>) if this is the first chunk.</param>
		/// <param name="buffer">Backing store for the buffer that contains the bytes to process</param>
		/// <param name="offset">Offset of the first byte to process</param>
		/// <param name="count">Number of bytes to process</param>
		/// <returns>Updated FNV-1 64-bit hash that will include the additional data</returns>
		[Pure]
		public static ulong Continue(ulong hash, byte[] buffer, int offset, int count)
		{
			Contract.DoesNotOverflow(buffer, offset, count);
			if (count == 0) return hash;

			// note: these tests are here to convince the JIT to disable the checks and unroll the loop below !!!
			if (offset < 0 || offset >= buffer.Length) ThrowHelper.ThrowArgumentException(nameof(offset), "Offset must be within the buffer");
			int end = offset + count;
			if (end < 0 || end > buffer.Length) ThrowHelper.ThrowArgumentException(nameof(buffer), "The buffer does not have enough data for the specified byte count");

			for (int i = offset; i < end; i++)
			{
				hash = (hash * FNV1_64_PRIME) ^ buffer[i];
			}

			return hash;
		}

		/// <summary>Computes the 64-bits FNV-1 hash of a <see cref="byte"/></summary>
		/// <returns>Corresponding FNV-1 64-bit hash</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromByte(byte x)
		{
			return Continue(FNV1_64_OFFSET_BASIS, x);
		}

		/// <summary>Continues computing the 64-bits FNV-1 hash on the next byte of data</summary>
		/// <param name="hash">Hash computed for all previous chunks, or the initial seed (<see cref="FNV1_64_OFFSET_BASIS"/>) if this is the first byte.</param>
		/// <param name="x">Additional byte</param>
		/// <returns>Updated FNV-1 64-bit hash that will include the additional data</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong Continue(ulong hash, byte x)
		{
			return (hash * FNV1_64_PRIME) ^ x;
		}

		/// <summary>Computes the 64-bits FNV-1 hash of a <see cref="short"/></summary>
		/// <returns>Corresponding FNV-1 64-bit hash</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromInt16(short x)
		{
			return Continue(FNV1_64_OFFSET_BASIS, x);
		}

		/// <summary>Continues computing the 64-bits FNV-1 hash on the next two bytes of data</summary>
		/// <param name="hash">Hash computed for all previous chunks, or the initial seed (<see cref="FNV1_64_OFFSET_BASIS"/>) if this is the first byte.</param>
		/// <param name="x">Additional bytes</param>
		/// <returns>Updated FNV-1 64-bit hash that will include the additional data</returns>
		public static ulong Continue(ulong hash, short x)
		{
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			hash = (hash * FNV1_64_PRIME) ^ ((byte)(x >> 8));
			return hash;
		}

		/// <summary>Computes the 64-bits FNV-1 hash of a <see cref="ushort"/></summary>
		/// <returns>Corresponding FNV-1 64-bit hash</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromUInt16(ushort x)
		{
			return Continue(FNV1_64_OFFSET_BASIS, x);
		}

		/// <summary>Continues computing the 64-bits FNV-1 hash on the next two bytes of data</summary>
		/// <param name="hash">Hash computed for all previous chunks, or the initial seed (<see cref="FNV1_64_OFFSET_BASIS"/>) if this is the first byte.</param>
		/// <param name="x">Additional bytes</param>
		/// <returns>Updated FNV-1 64-bit hash that will include the additional data</returns>
		public static ulong Continue(ulong hash, ushort x)
		{
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			hash = (hash * FNV1_64_PRIME) ^ ((byte)(x >> 8));
			return hash;
		}

		/// <summary>Computes the 64-bits FNV-1 hash of a <see cref="int"/></summary>
		/// <returns>Corresponding FNV-1 64-bit hash</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromInt32(int x)
		{
			return Continue(FNV1_64_OFFSET_BASIS, x);
		}

		/// <summary>Continues computing the 64-bits FNV-1 hash on the next four bytes of data</summary>
		/// <param name="hash">Hash computed for all previous chunks, or the initial seed (<see cref="FNV1_64_OFFSET_BASIS"/>) if this is the first byte.</param>
		/// <param name="x">Additional bytes</param>
		/// <returns>Updated FNV-1 64-bit hash that will include the additional data</returns>
		public static ulong Continue(ulong hash, int x)
		{
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			return hash;
		}

		/// <summary>Computes the 64-bits FNV-1 hash of a <see cref="uint"/></summary>
		/// <returns>Corresponding FNV-1 64-bit hash</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromUInt32(uint x)
		{
			return Continue(FNV1_64_OFFSET_BASIS, x);
		}

		/// <summary>Continues computing the 64-bits FNV-1 hash on the next four bytes of data</summary>
		/// <param name="hash">Hash computed for all previous chunks, or the initial seed (<see cref="FNV1_64_OFFSET_BASIS"/>) if this is the first byte.</param>
		/// <param name="x">Additional bytes</param>
		/// <returns>Updated FNV-1 64-bit hash that will include the additional data</returns>
		public static ulong Continue(ulong hash, uint x)
		{
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			return hash;
		}

		/// <summary>Computes the 64-bits FNV-1 hash of a <see cref="long"/></summary>
		/// <returns>Corresponding FNV-1 64-bit hash</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromInt64(long x)
		{
			return Continue(FNV1_64_OFFSET_BASIS, x);
		}

		/// <summary>Continues computing the 64-bits FNV-1 hash on the next eight bytes of data</summary>
		/// <param name="hash">Hash computed for all previous chunks, or the initial seed (<see cref="FNV1_64_OFFSET_BASIS"/>) if this is the first byte.</param>
		/// <param name="x">Additional bytes</param>
		/// <returns>Updated FNV-1 64-bit hash that will include the additional data</returns>
		public static ulong Continue(ulong hash, long x)
		{
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			return hash;
		}

		/// <summary>Computes the 64-bits FNV-1 hash of a <see cref="ulong"/></summary>
		/// <returns>Corresponding FNV-1 64-bit hash</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromUInt64(ulong x)
		{
			return Continue(FNV1_64_OFFSET_BASIS, x);
		}

		/// <summary>Continues computing the 64-bits FNV-1 hash on the next eight bytes of data</summary>
		/// <param name="hash">Hash computed for all previous chunks, or the initial seed (<see cref="FNV1_64_OFFSET_BASIS"/>) if this is the first byte.</param>
		/// <param name="x">Additional bytes</param>
		/// <returns>Updated FNV-1 64-bit hash that will include the additional data</returns>
		public static ulong Continue(ulong hash, ulong x)
		{
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			return hash;
		}

		/// <summary>Computes the 64-bits FNV-1 hash of a <see cref="Guid"/></summary>
		/// <returns>Corresponding FNV-1 64-bit hash</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromGuid(Guid value)
		{
			return Continue(FNV1_64_OFFSET_BASIS, value);
		}

		/// <summary>Continues computing the 64-bits FNV-1 hash on the next sixteen bytes of data</summary>
		/// <param name="hash">Hash computed for all previous chunks, or the initial seed (<see cref="FNV1_64_OFFSET_BASIS"/>) if this is the first byte.</param>
		/// <param name="x">Additional bytes</param>
		/// <returns>Updated FNV-1 64-bit hash that will include the additional data</returns>
		public static ulong Continue(ulong hash, Guid x)
		{
			unsafe
			{
				ulong* q = (ulong*)(&x);
				hash = Continue(hash, q[0]);
				return Continue(hash, q[1]);
			}
		}

	}

}
