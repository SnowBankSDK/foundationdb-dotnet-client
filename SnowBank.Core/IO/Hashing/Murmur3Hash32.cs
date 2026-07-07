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
	using System.Runtime.InteropServices;

	/// <summary>Computes a 32-bit "Murmur3" hash</summary>
	/// <remarks>IMPORTANT: This hash is NOT cryptographic! It can leak information about the hashed data, and must therefore not be used publicly in a data-protection scenario! (you should rather use SHA or HMAC for that kind of thing)</remarks>
	[PublicAPI]
	public static class Murmur3Hash32
	{
		// 32-bit version of "MurmurHash3", created by Austin Appleby. This hashing algo is ideal for computing a hashcode for use with a hash table.
		// WARNING: This is *NOT* a cryptographic hash (it can leak information about the source data), and must therefore only be computed on already-encrypted data.

		// "MurmurHash3 is the successor to MurmurHash2. It comes in 3 variants - a 32-bit version that targets low latency for hash table use and two 128-bit versions for generating unique identifiers for large blocks of data, one each for x86 and x64 platforms."

		//TODO: Google Code redirect!
		// Details: http://code.google.com/p/smhasher/wiki/MurmurHash3
		// Reference Implementation: http://code.google.com/p/smhasher/source/browse/trunk/MurmurHash3.cpp
		// Benchs & Collisions: http://code.google.com/p/smhasher/wiki/MurmurHash3_x86_32

		private const uint C1 = 0xCC9E2D51;
		private const uint C2 = 0x1B873593;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint FromBytes(byte[] bytes)
		{
			unsafe
			{
				//note: if bytes.Length == 0, pBytes will be null!
				fixed (byte* pBytes = bytes)
				{
					return ContinueUnsafe(0U, pBytes, bytes.Length);
				}
			}
		}

		[Pure]
		public static uint FromBytes(ReadOnlySpan<byte> bytes)
		{
			unsafe
			{
				fixed (byte* pBytes = &MemoryMarshal.GetReference(bytes))
				{
					return ContinueUnsafe(0U, pBytes, bytes.Length);
				}
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint FromBytes(Slice bytes)
		{
			unsafe
			{
				fixed (byte* pBytes = &bytes.DangerousGetPinnableReference())
				{
					return ContinueUnsafe(0U, pBytes, bytes.Count);
				}
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe uint FromBytesUnsafe(byte* bytes, int count)
		{
			return ContinueUnsafe(0U, bytes, count);
		}

		public static uint Continue(uint hash, byte[] bytes)
		{
			unsafe
			{
				//note: if bytes.Length == 0, pBytes will be null!
				fixed (byte* pBytes = bytes)
				{
					return ContinueUnsafe(hash, pBytes, bytes.Length);
				}
			}
		}

		public static uint Continue(uint hash, ReadOnlySpan<byte> bytes)
		{
			unsafe
			{
				fixed (byte* pBytes = &MemoryMarshal.GetReference(bytes))
				{
					return ContinueUnsafe(hash, pBytes, bytes.Length);
				}
			}
		}

		public static uint Continue(uint hash, Slice buffer)
		{
			unsafe
			{
				fixed (byte* pBytes = &buffer.DangerousGetPinnableReference())
				{
					return ContinueUnsafe(hash, pBytes, buffer.Count);
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe uint ContinueUnsafe(uint hash, byte* bytes, int count)
		{
			if (bytes == null && count != 0) ThrowHelper.ThrowArgumentNullException(nameof(bytes));
			if (count < 0) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(count));
			// BODY

			if (count > 0)
			{
				Contract.Debug.Assert(bytes != null);
				uint* bp = (uint*)bytes;
				uint k;

				int n = count >> 2;
				while (n-- > 0)
				{
					k = (*bp++) * C1;
					k = (k << 15) | (k >> (32 - 15)); // ROTL32(k1, 15)
					k *= C2;

					hash ^= k;
					hash = (hash << 13) | (hash >> (32 - 13)); // ROTL32(hash, 13)
					hash = (hash * 5) + 0xE6546B64;
				}

				// TAIL

				n = count & 3;
				if (n > 0)
				{
					byte* tail = (byte*)bp;

					k = tail[0];
					k |= (n >= 2) ? (uint)(tail[1] << 8) : 0U;
					k |= (n == 3) ? (uint)(tail[2] << 16) : 0U;

					k *= C1;
					k = (k << 15) | (k >> (32 - 15)); //ROTL32(k1, 15);
					k *= C2;

					hash ^= k;
				}
			}

			// finalization
			hash ^= (uint)count;

			// Finalization mix - force all bits of a hash block to avalanche
			hash ^= hash >> 16;
			hash *= 0x85EBCA6B;
			hash ^= hash >> 13;
			hash *= 0xC2B2AE35;
			hash ^= hash >> 16;

			return hash;
		}

		public static int GetHashCode(byte[] bytes)
		{
			return bytes != null ? (int) (Continue(0U, bytes) & int.MaxValue) : -1;
		}

		public static int GetHashCode(Slice bytes)
		{
			return !bytes.IsNull ? (int) (Continue(0U, bytes) & int.MaxValue) : -1;
		}

		public static int GetHashCode(ReadOnlySpan<byte> bytes)
		{
			return (int) (Continue(0U, bytes) & int.MaxValue);
		}

	}

}
