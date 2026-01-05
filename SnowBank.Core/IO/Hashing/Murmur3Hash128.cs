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

	/// <summary>Calcul d'un hash "Murmur3" sur 128 bits</summary>
	/// <remarks>IMPORTANT: Ce hash n'est PAS cryptographique ! Il peut leaker des informations sur les données hashées, et ne doit donc pas être utilisé publiquement dans un scenario de protection de données! (il faut plutot utiliser SHA ou HMAC pour ce genre de choses)</remarks>
	[PublicAPI]
	public static class Murmur3Hash128
	{
		// Version 32 bits de "MurmurHash3", créé par Austin Appleby. Cet algo de hashing est idéal pour calcule un hashcode pour utilisation avec une table de hashage.
		// ATTENTION: Ce n'est *PAS* un hash cryptographique (il peut leaker des informations sur les données sources), et ne doit donc être calculé que sur les données déja cryptées.

		// "MurmurHash3 is the successor to MurmurHash2. It comes in 3 variants - a 32-bit version that targets low latency for hash table use and two 128-bit versions for generating unique identifiers for large blocks of data, one each for x86 and x64 platforms."

		// Details: http://code.google.com/p/smhasher/wiki/MurmurHash3
		// Reference Implementation: http://code.google.com/p/smhasher/source/browse/trunk/MurmurHash3.cpp
		// Benchs & Collisions: http://code.google.com/p/smhasher/wiki/MurmurHash3_x86_32

		private const ulong C1 = 0x87c37b91114253d5UL;
		private const ulong C2 = 0x4cf5ad432745937fUL;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Guid FromBytes(byte[]? bytes)
		{
			return Continue(0, bytes);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Guid FromBytes(Slice bytes)
		{
			return Continue(0, bytes);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Guid FromBytes(ReadOnlySpan<byte> bytes)
		{
			return Continue(0, bytes);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe Guid FromBytesUnsafe(uint seed, byte* bytes, int count)
		{
			return ContinueUnsafe(0, bytes, count);
		}

		[Pure]
		public static Guid Continue(uint seed, byte[]? bytes)
		{
			// IMPORTANT: il faut quand même laisser passer le cas où null, ou Length == 0

			unsafe
			{
				if (bytes == null || bytes.Length == 0) return ContinueUnsafe(seed, null, 0);
				fixed (byte* pBytes = bytes)
				{
					return ContinueUnsafe(seed, pBytes, bytes.Length);
				}
			}
		}

		[Pure]
		public static Guid Continue(uint seed, Slice bytes)
		{
			// IMPORTANT: il faut quand même laisser passer le cas où null, ou Length == 0

			unsafe
			{
				if (bytes.Count == 0) return ContinueUnsafe(seed, null, 0);
				fixed (byte* pBytes = &bytes.DangerousGetPinnableReference())
				{
					return ContinueUnsafe(seed, pBytes, bytes.Count);
				}
			}
		}

		[Pure]
		public static Guid Continue(uint seed, ReadOnlySpan<byte> bytes)
		{
			// IMPORTANT: il faut quand même laisser passer le cas où null, ou Length == 0

			unsafe
			{
				if (bytes.Length == 0) return ContinueUnsafe(seed, null, 0);
				fixed (byte* pBytes = &MemoryMarshal.GetReference(bytes))
				{
					return ContinueUnsafe(seed, pBytes, bytes.Length);
				}
			}
		}

		[Pure]
		public static Guid FromGuid(Guid value)
		{
			unsafe
			{
				return ContinueUnsafe(0, (byte*) &value, 16);
			}
		}

		[Pure]
		public static unsafe Guid ContinueUnsafe(uint seed, void* bytes, int count)
		{
			Contract.Debug.Requires(count >= 0 && (count == 0 || bytes != null));

			// BODY
			ulong h1 = seed;
			ulong h2 = seed;

			ulong* bp = (ulong*) bytes;
			ulong k1;
			ulong k2;

			int n = count >> 4;
			while (n-- > 0)
			{
				k1 = (*bp++) * C1;
				k2 = (*bp++) * C2;

				// K1
				k1 = (k1 << 31) | (k1 >> (64 - 31)); // ROTL64(k1, 31)
				k1 *= C2;
				h1 ^= k1;
				h1 = (h1 << 27) | (h1 >> (64 - 27)); // ROTL64(h1, 27)
				h1 += h2;
				h1 = (h1 * 5) + 0x52dce729;

				// K2
				k2 = (k2 << 33) | (k2 >> (64 - 33)); // ROTL64(k2, 33)
				k2 *= C1;
				h2 ^= k2;
				h2 = (h2 << 31) | (h2 >> (64 - 31)); // ROTL64(h2, 31)
				h2 += h1;
				h2 = (h2 * 5) + 0x38495ab5;
			}

			// TAIL

			n = count & 15;
			if (n > 0)
			{
				byte* tail = ((byte*)bp + n - 1);

				if (n > 8)
				{
					k2 = *tail--;
					--n;
					while (n > 8)
					{
						k2 = (k2 << 8) | *(tail--);
						--n;
					}

					k2 *= C2;
					k2 = (k2 << 33) | (k2 >> (64 - 33)); // ROTL64(k2, 33)
					k2 *= C1;
					h2 ^= k2;
				}

				if (n > 0)
				{
					k1 = *tail--;
					--n;
					while (n > 0)
					{
						k1 = (k1 << 8) | (*tail--);
						--n;
					}

					k1 *= C1;
					k1 = (k1 << 31) | (k1 >> (64 - 31)); // ROTL64(k1, 31)
					k1 *= C2;
					h1 ^= k1;
				}
			}

			// finalization
			h1 ^= (ulong)count;
			h2 ^= (ulong)count;

			h1 += h2;
			h2 += h1;

			// Finalization mix - force all bits of a hash block to avalanche
			h1 ^= h1 >> 33;
			h1 *= 0xff51afd7ed558ccdUL;
			h1 ^= h1 >> 33;
			h1 *= 0xc4ceb9fe1a85ec53UL;
			h1 ^= h1 >> 33;

			h2 ^= h2 >> 33;
			h2 *= 0xff51afd7ed558ccdUL;
			h2 ^= h2 >> 33;
			h2 *= 0xc4ceb9fe1a85ec53UL;
			h2 ^= h2 >> 33;

			h1 += h2;
			h2 += h1;

			Guid r;
			{
				ulong* p = (ulong*) &r;
				p[0] = h1;
				p[1] = h2;
			}
			return r;
		}

	}

}
