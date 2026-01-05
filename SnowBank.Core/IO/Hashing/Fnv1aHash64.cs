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
	using System.Runtime.InteropServices;
	using System.Text;

	/// <summary>Helper for computing 64-bits FNV-1a hashes (aka "Alternative" FowlerNollVo)</summary>
	[PublicAPI]
	public static class Fnv1aHash64
	{
		// The difference with "regular" FNV-1 is the order of operations (MUL and XOR) is reversed.

		/* 
			hash = FNV_offset_basis
			for each octet_of_data to be hashed
				hash = hash XOR octet_of_data
				hash = hash * FNV_prime
			return hash
		 */

		public const ulong FNV1_64_OFFSET_BASIS = 14695981039346656037;
		public const ulong FNV1_64_PRIME = 1099511628211;

		/// <summary>Computes the 64-bits FNV-1a hash of the utf-8 representation of a string</summary>
		/// <param name="text">String to process</param>
		/// <param name="ignoreCase">If <c>true</c>, the string is converted to lowercase (invariant) before conversion to utf-8</param>
		/// <param name="encoding">Encoding used to convert the string into bytes (utf-8 by default)</param>
		/// <returns>Corresponding FNV-1a 64-bit hash</returns>
		public static ulong FromString(string text, bool ignoreCase, Encoding? encoding = null)
		{
			if (string.IsNullOrEmpty(text)) return 0;
			if (ignoreCase) text = text.ToLowerInvariant();

			encoding ??= Encoding.UTF8;

			int count = encoding.GetByteCount(text);
			if (count <= 4096)
			{ // use the stack for the temporary buffer
				unsafe
				{
					//REVIEW: use Span<char>!
					byte* tmp = stackalloc byte[count];
					fixed (char* chars = text)
					{
						if (encoding.GetBytes(chars, text.Length, tmp, count) != count) throw new InvalidOperationException();
						return FromBytesUnsafe(tmp, count);
					}
				}
			}
			else
			{ // use the heap
				byte[] bytes = encoding.GetBytes(text);
				return FromBytes(bytes);
			}
		}

		/// <summary>Computes the 64-bits FNV-1a hash of a byte array</summary>
		/// <param name="bytes">Bytes to process</param>
		/// <returns>Corresponding FNV-1a 64-bit hash</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromBytes(byte[] bytes)
		{
			return Continue(FNV1_64_OFFSET_BASIS, bytes);
		}

		/// <summary>Computes the 64-bits FNV-1a hash of a span of bytes</summary>
		/// <param name="bytes">Bytes to process</param>
		/// <returns>Corresponding FNV-1a 64-bit hash</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromBytes(ReadOnlySpan<byte> bytes)
		{
			return Continue(FNV1_64_OFFSET_BASIS, bytes);
		}

		/// <summary>Computes the 64-bits FNV-1a hash of a <see cref="Slice"/></summary>
		/// <param name="bytes">Bytes to process</param>
		/// <returns>Corresponding FNV-1a 64-bit hash</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromBytes(Slice bytes)
		{
			return Continue(FNV1_64_OFFSET_BASIS, bytes);
		}

		/// <summary>Computes the 64-bits FNV-1a hash of a span of bytes</summary>
		/// <param name="buffer">Pointer to the start of the data in memory</param>
		/// <param name="count">Size (in bytes) of the buffer</param>
		/// <returns>Corresponding FNV-1a 64-bit hash</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe ulong FromBytesUnsafe(byte* buffer, int count)
		{
			return ContinueUnsafe(FNV1_64_OFFSET_BASIS, buffer, count);
		}

		/// <summary>Continues computing the 64-bits FNV-1a hash on an additional chunk of data</summary>
		/// <param name="hash">Hash computed for all previous chunks, or the initial seed (<see cref="FNV1_64_OFFSET_BASIS"/>) if this is the first chunk.</param>
		/// <param name="bytes">New chunk of bytes to process</param>
		/// <returns>Updated FNV-1a 64-bit hash that will include the additional data</returns>
		[Pure]
		public static ulong Continue(ulong hash, byte[]? bytes)
		{
			if (bytes == null || bytes.Length == 0) return hash;

			foreach (var b in bytes)
			{
				hash = (hash ^ b) * FNV1_64_PRIME;
			}
			return hash;
		}

		/// <summary>Continues computing the 64-bits FNV-1a hash on an additional chunk of data</summary>
		/// <param name="hash">Hash computed for all previous chunks, or the initial seed (<see cref="FNV1_64_OFFSET_BASIS"/>) if this is the first chunk.</param>
		/// <param name="bytes">New chunk of bytes to process</param>
		/// <returns>Updated FNV-1a 64-bit hash that will include the additional data</returns>
		[Pure]
		public static ulong Continue(ulong hash, ReadOnlySpan<byte> bytes)
		{
			unsafe
			{
				fixed (byte* pBytes = &MemoryMarshal.GetReference(bytes))
				{
					return ContinueUnsafe(hash, pBytes, bytes.Length);
				}
			}
		}

		/// <summary>Continues computing the 64-bits FNV-1a hash on an additional chunk of data</summary>
		/// <param name="hash">Hash computed for all previous chunks, or the initial seed (<see cref="FNV1_64_OFFSET_BASIS"/>) if this is the first chunk.</param>
		/// <param name="bytes">New chunk of bytes to process</param>
		/// <returns>Updated FNV-1a 64-bit hash that will include the additional data</returns>
		[Pure]
		public static ulong Continue(ulong hash, Slice bytes)
		{
			unsafe
			{
				fixed (byte* pBytes = &bytes.DangerousGetPinnableReference())
				{
					return ContinueUnsafe(hash, pBytes, bytes.Count);
				}
			}
		}

		/// <summary>Continues computing the 64-bits FNV-1a hash on an additional chunk of data</summary>
		/// <param name="hash">Hash computed for all previous chunks, or the initial seed (<see cref="FNV1_64_OFFSET_BASIS"/>) if this is the first chunk.</param>
		/// <param name="bytes">Point to the start of the new chunk to process</param>
		/// <param name="count">Size (in bytes) of the chunk</param>
		/// <returns>Updated FNV-1a 64-bit hash that will include the additional data</returns>
		[Pure]
		public static unsafe ulong ContinueUnsafe(ulong hash, byte* bytes, int count)
		{
			if (count == 0 || bytes == null) return hash;
			if (count < 0) throw ThrowHelper.ArgumentException(nameof(count), "Count must be a positive integer");

			byte* bp = bytes;
			byte* be = bytes + count;
			//TODO: unroll?
			while (bp < be)
			{
				hash = (hash ^ (*bp++)) * FNV1_64_PRIME;
			}

			return hash;
		}

	}

}
