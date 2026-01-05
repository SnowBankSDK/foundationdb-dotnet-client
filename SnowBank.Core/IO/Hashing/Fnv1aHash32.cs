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

// ReSharper disable InconsistentNaming

namespace SnowBank.IO.Hashing
{
	using System.Text;
	using System.Runtime.InteropServices;

	/// <summary>Helper for computing 32-bits FNV-1a hashes (aka "Alternative" FowlerNollVo)</summary>
	[PublicAPI]
	public static class Fnv1aHash32
	{
		// The difference with "regular" FNV-1 is the order of operations (MUL and XOR) is reversed.

		/*
			hash = FNV_offset_basis
			for each octet_of_data to be hashed
				hash = hash XOR octet_of_data
				hash = hash * FNV_prime
			return hash
		 */

		public const uint FNV1_32_OFFSET_BASIS = 2166136261;
		public const uint FNV1_32_PRIME = 16777619;

		/// <summary>Computes the 32-bits FNV-1a hash of the utf-8 representation of a string</summary>
		/// <param name="text">String to process</param>
		/// <param name="ignoreCase">If <c>true</c>, the string is converted to lowercase (invariant) before conversion to utf-8</param>
		/// <param name="encoding">Encoding used to convert the string into bytes (utf-8 by default)</param>
		/// <returns>Corresponding FNV-1a 32-bit hash</returns>
		public static uint FromString(string text, bool ignoreCase = false, Encoding? encoding = null)
		{
			if (string.IsNullOrEmpty(text)) return 0;
			if (ignoreCase) text = text.ToLowerInvariant();
			byte[] bytes = (encoding ?? Encoding.UTF8).GetBytes(text);
			return Continue(FNV1_32_OFFSET_BASIS, bytes);
		}

		/// <summary>Computes the 32-bits FNV-1a hash of a byte array</summary>
		/// <param name="bytes">Bytes to process</param>
		/// <returns>Corresponding FNV-1a 32-bit hash</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint FromBytes(byte[] bytes)
		{
			return Continue(FNV1_32_OFFSET_BASIS, bytes);
		}

		/// <summary>Computes the 32-bits FNV-1a hash of a span of bytes</summary>
		/// <param name="bytes">Bytes to process</param>
		/// <returns>Corresponding FNV-1a 32-bit hash</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint FromBytes(ReadOnlySpan<byte> bytes)
		{
			return Continue(FNV1_32_OFFSET_BASIS, bytes);
		}

		/// <summary>Computes the 32-bits FNV-1a hash of a <see cref="Slice"/></summary>
		/// <param name="bytes">Bytes to process</param>
		/// <returns>Corresponding FNV-1a 32-bit hash</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint FromBytes(Slice bytes)
		{
			return Continue(FNV1_32_OFFSET_BASIS, bytes);
		}

		/// <summary>Computes the 32-bits FNV-1a hash of a byte array, with a custom initial seed</summary>
		/// <param name="seed">Initial seed (0 for default)</param>
		/// <param name="bytes">Bytes to process</param>
		/// <returns>Corresponding FNV-1a 32-bit hash</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint FromBytes(uint seed, byte[] bytes)
		{
			return Continue(seed ^ FNV1_32_OFFSET_BASIS, bytes);
		}

		/// <summary>Computes the 32-bits FNV-1a hash of a span of bytes</summary>
		/// <param name="buffer">Pointer to the start of the data in memory</param>
		/// <param name="count">Size (in bytes) of the buffer</param>
		/// <returns>Corresponding FNV-1a 32-bit hash</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe uint FromBytesUnsafe(byte* buffer, int count)
		{
			return ContinueUnsafe(FNV1_32_OFFSET_BASIS, buffer, count);
		}

		/// <summary>Continues computing the 32-bits FNV-1a hash on an additional chunk of data</summary>
		/// <param name="hash">Hash computed for all previous chunks, or the initial seed (<see cref="FNV1_32_OFFSET_BASIS"/>) if this is the first chunk.</param>
		/// <param name="bytes">New chunk of bytes to process</param>
		/// <returns>Updated FNV-1a 32-bit hash that will include the additional data</returns>
		[Pure]
		public static uint Continue(uint hash, byte[]? bytes) => Continue(hash, new ReadOnlySpan<byte>(bytes));

		/// <summary>Continues computing the 32-bits FNV-1a hash on an additional chunk of data</summary>
		/// <param name="hash">Hash computed for all previous chunks, or the initial seed (<see cref="FNV1_32_OFFSET_BASIS"/>) if this is the first chunk.</param>
		/// <param name="bytes">New chunk of bytes to process</param>
		/// <returns>Updated FNV-1a 32-bit hash that will include the additional data</returns>
		[Pure]
		public static uint Continue(uint hash, ReadOnlySpan<byte> bytes)
		{
			if (bytes.Length == 0) return hash;
			unsafe
			{
				fixed (byte* pBytes = &MemoryMarshal.GetReference(bytes))
				{
					return ContinueUnsafe(hash, pBytes, bytes.Length);
				}
			}
		}

		/// <summary>Continues computing the 32-bits FNV-1a hash on an additional chunk of data</summary>
		/// <param name="hash">Hash computed for all previous chunks, or the initial seed (<see cref="FNV1_32_OFFSET_BASIS"/>) if this is the first chunk.</param>
		/// <param name="bytes">New chunk of bytes to process</param>
		/// <returns>Updated FNV-1a 32-bit hash that will include the additional data</returns>
		[Pure]
		public static uint Continue(uint hash, Slice bytes)
		{
			if (bytes.Count == 0) return hash;
			unsafe
			{
				fixed (byte* pBytes = &bytes.DangerousGetPinnableReference())
				{
					return ContinueUnsafe(hash, pBytes, bytes.Count);
				}
			}
		}

		/// <summary>Continues computing the 32-bits FNV-1a hash on an additional chunk of data</summary>
		/// <param name="hash">Hash computed for all previous chunks, or the initial seed (<see cref="FNV1_32_OFFSET_BASIS"/>) if this is the first chunk.</param>
		/// <param name="bytes">Point to the start of the new chunk to process</param>
		/// <param name="count">Size (in bytes) of the chunk</param>
		/// <returns>Updated FNV-1a 32-bit hash that will include the additional data</returns>
		[Pure]
		public static unsafe uint ContinueUnsafe(uint hash, byte* bytes, int count)
		{
			if (count == 0 || bytes == null) return hash;
			if (count < 0) throw Contract.FailArgumentNotPositive(nameof(count));

			byte* bp = bytes;
			byte* be = bytes + count;
			//TODO: unroll?
			while (bp < be)
			{
				hash = (hash ^ (*bp++)) * FNV1_32_PRIME;
			}
			return hash;
		}

		/// <summary>Continues computing the 32-bits FNV-1a hash on an additional chunk of data</summary>
		/// <param name="hash">Hash computed for all previous chunks, or the initial seed (<see cref="FNV1_32_OFFSET_BASIS"/>) if this is the first chunk.</param>
		/// <param name="buffer">Backing store for the buffer that contains the bytes to process</param>
		/// <param name="offset">Offset of the first byte to process</param>
		/// <param name="count">Number of bytes to process</param>
		/// <returns>Updated FNV-1a 32-bit hash that will include the additional data</returns>
		public static uint Continue(uint hash, byte[]? buffer, int offset, int count)
		{
			if (count == 0 || buffer == null) return hash;

			if (offset < 0 || offset >= buffer.Length) throw ThrowHelper.ArgumentException(nameof(offset), "Offset must be within the buffer");
			int end = offset + count;
			if (end < 0 || end > buffer.Length) throw ThrowHelper.ArgumentException(nameof(buffer), "The buffer does not have enough data for the specified byte count");

			for (int i = offset; i < end; i++)
			{
				hash = (hash ^ buffer[i]) * FNV1_32_PRIME;
			}

			return hash;
		}

		/// <summary>Continues computing the 32-bits FNV-1a hash on the next byte of data</summary>
		/// <param name="hash">Hash computed for all previous chunks, or the initial seed (<see cref="FNV1_32_OFFSET_BASIS"/>) if this is the first byte.</param>
		/// <param name="x">Additional byte</param>
		/// <returns>Updated FNV-1a 32-bit hash that will include the additional byte</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Continue(uint hash, byte x)
		{
			return (hash ^ x) * FNV1_32_PRIME;
		}

		/// <summary>Computes a hashcode of a byte array, derived from 32-bits FNV-1a hash of its content</summary>
		/// <returns>Corresponding FNV-1a 32-bit hash</returns>
		public static int GetHashCode(byte[]? buffer)
		{
			if (buffer == null) return -1;
			if (buffer.Length == 0) return 0;

			return Diffuse(Continue(FNV1_32_PRIME, buffer));
		}

		/// <summary>Computes a hashcode of a byte array segment, derived from 32-bits FNV-1a hash of its content</summary>
		/// <returns>Corresponding FNV-1a 32-bit hash</returns>
		[Pure]
		public static int GetHashCode(byte[] buffer, int offset, int count)
		{
			Contract.NotNull(buffer);
			if (count == 0) return 0;

			return Diffuse(Continue(FNV1_32_PRIME, buffer, offset, count));
		}

		/// <summary>Converts a 32-bits FNV-1a hash into a usable Hashcode by mixing the bits</summary>
		/// <param name="hash">32-bits FNV-1 Hash</param>
		/// <returns>Mixed hash that can be used as a Hashcode</returns>
		[Pure]
		public static int Diffuse(uint hash)
		{
			// cf Figure 4: http://bretm.home.comcast.net/~bretm/hash/6.html
			hash += hash << 13;
			hash ^= hash >> 7;
			hash += hash << 3;
			hash ^= hash >> 17;
			hash += hash << 5;
			return (int) hash;
		}

	}

}
