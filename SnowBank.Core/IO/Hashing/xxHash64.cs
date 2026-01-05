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

	/// <summary>Calcul de hash xxHash sur 64 bits</summary>
	/// <remarks>IMPORTANT: Ce hash n'est PAS cryptographique ! Il peut leaker des informations sur les données hashées, et ne doit donc pas être utilisé publiquement dans un scenario de protection de données! (il faut plutot utiliser SHA ou HMAC pour ce genre de choses)</remarks>
	[Obsolete("Use System.IO.Hashing.XxHash64 instead")]
	public static class XxHash64
	{
		// From https://code.google.com/p/xxhash/

		// En théorie, 2x plus rapide que XXH32 sur x64, par contre beaucoup plus lent (~3x) sur x86
		// See http://fastcompression.blogspot.fr/2014/07/xxhash-wider-64-bits.html

		// Implémentation basée sur le patch r35: https://code.google.com/p/xxhash/source/detail?r=35

		private const ulong INITIAL_SEED = 0;

		private const ulong PRIME64_1 = 11400714785074694791UL;
		private const ulong PRIME64_2 = 14029467366897019727UL;
		private const ulong PRIME64_3 = 1609587929392839161UL;
		private const ulong PRIME64_4 = 9650029242287828579UL;
		private const ulong PRIME64_5 = 2870177450012600261UL;

		/// <summary>Hash of the empty buffer</summary>
		public const ulong HASH0 = 0xEF46DB3751D8E999UL;

		/// <summary>Calcul le xxHash 64-bit d'un chaîne (a partir de sa représentation UTF-16 en mémoire)</summary>
		/// <param name="text">Chaîne de texte à convertir</param>
		/// <returns>Code xxHash 64 bit calculé sur la représentation UTF-16 de la chaîne. Attention: N'est pas garantit unique!</returns>
		public static ulong FromText(string text)
		{
			Contract.NotNull(text);
			unsafe
			{
				if (text.Length == 0) return ContinueUnsafe(INITIAL_SEED, null, 0);
				fixed (char* chars = text)
				{
					return ContinueUnsafe(INITIAL_SEED, (byte*) chars, checked((uint) text.Length * sizeof(char)));
				}
			}
		}

		/// <summary>Calcul le xxHash 64-bit d'un chaîne (a partir de sa représentation UTF-16 en mémoire)</summary>
		/// <returns>Code xxHash 64 bit calculé sur la représentation UTF-16 de la chaîne. Attention: N'est pas garantit unique!</returns>
		public static ulong FromText(ReadOnlySpan<char> buffer)
		{
			unsafe
			{
				if (buffer.Length == 0) return ContinueUnsafe(INITIAL_SEED, null, 0);
				fixed (char* chars = &MemoryMarshal.GetReference(buffer))
				{
					return ContinueUnsafe(INITIAL_SEED, (byte*) chars, checked((uint) buffer.Length * sizeof(char)));
				}
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromBytes(byte[] input)
		{
			Contract.NotNull(input);
			return Continue(INITIAL_SEED, input.AsSlice());
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromBytes(Slice input)
		{
			return Continue(INITIAL_SEED, input);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromBytes(ReadOnlySpan<byte> input)
		{
			return Continue(INITIAL_SEED, input);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe ulong FromBytesUnsafe(byte* input, uint len)
		{
			Contract.PointerNotNull(input);
			return ContinueUnsafe(INITIAL_SEED, input, len);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong Continue(ulong seed, byte[] input)
		{
			Contract.NotNull(input);
			return Continue(seed, input.AsSlice());
		}

		[Pure]
		public static ulong Continue(ulong seed, Slice input)
		{
			Contract.NotNull(input.Array, paramName: nameof(input));
			unsafe
			{
				if (input.Count == 0) return ContinueUnsafe(seed, null, 0);
				fixed (byte* ptr = &input.DangerousGetPinnableReference())
				{
					return ContinueUnsafe(seed, ptr, (uint) input.Count);
				}
			}
		}

		[Pure]
		public static ulong Continue(ulong seed, ReadOnlySpan<byte> input)
		{
			unsafe
			{
				if (input.Length == 0) return ContinueUnsafe(seed, null, 0);
				fixed (byte* pBytes = &MemoryMarshal.GetReference(input))
				{
					return ContinueUnsafe(seed, pBytes, (uint) input.Length);
				}
			}
		}

		[Pure]
		public static unsafe ulong ContinueUnsafe(ulong seed, byte* input, uint len)
		{
			Contract.Debug.Requires(input != null || len == 0);
			Contract.Debug.Requires(BitConverter.IsLittleEndian, "Not implemented for Big Endian architectures !");

			if (len == 0 && seed == INITIAL_SEED) return HASH0;

			byte* p = input;
			if (p == null)
			{
				// only allow a null pointer if length is 0
				if (len > 0) throw new ArgumentNullException(nameof(input));
				p = (byte*) 16;
			}

			byte* bEnd = p + len;
			ulong h64;

			if (len >= 32)
			{
				ulong* p64 = (ulong*) p;
				ulong* limit = (ulong*) (bEnd - 32);
				ulong v1 = seed + PRIME64_1 + PRIME64_2;
				ulong v2 = seed + PRIME64_2;
				ulong v3 = seed + 0;
				ulong v4 = seed - PRIME64_1;

				do
				{
					// for(# in 1..4) => v# += XXH_get64bits(p) * PRIME64_2; p+=8; v# = XXH_rotl64(v#, 31); v# *= PRIME64_1;
					v1 += *(p64 + 0) * PRIME64_2; v1 = ((v1 << 31) | (v1 >> (64 - 31))) * PRIME64_1;
					v2 += *(p64 + 1) * PRIME64_2; v2 = ((v2 << 31) | (v2 >> (64 - 31))) * PRIME64_1;
					v3 += *(p64 + 2) * PRIME64_2; v3 = ((v3 << 31) | (v3 >> (64 - 31))) * PRIME64_1;
					v4 += *(p64 + 3) * PRIME64_2; v4 = ((v4 << 31) | (v4 >> (64 - 31))) * PRIME64_1;
					p64 += 4; // note: 4 x sizeof(ulong) == 32
				}
				while (p64 <= limit);

				// h64 = XXH_rotl64(v1, 1) + XXH_rotl64(v2, 7) + XXH_rotl64(v3, 12) + XXH_rotl64(v4, 18)
				h64 = ((v1 << 1) | (v1 >> (64 - 1))) + ((v2 << 7) | (v2 >> (64 - 7))) + ((v3 << 12) | (v3 >> (64 - 12))) + ((v4 << 18) | (v4 >> (64 - 18)));

				// for(# in 1..4) =>
				//	v# *= PRIME64_2; v# = XXH_rotl64(v#, 31); v# *= PRIME64_1; h64 ^= v#;
				//	h64 = h64 * PRIME64_1 + PRIME64_4;

				v1 *= PRIME64_2; v1 = ((v1 << 31) | (v1 >> (64 - 31))) * PRIME64_1; h64 ^= v1; h64 = h64 * PRIME64_1 + PRIME64_4;
				v2 *= PRIME64_2; v2 = ((v2 << 31) | (v2 >> (64 - 31))) * PRIME64_1; h64 ^= v2; h64 = h64 * PRIME64_1 + PRIME64_4;
				v3 *= PRIME64_2; v3 = ((v3 << 31) | (v3 >> (64 - 31))) * PRIME64_1; h64 ^= v3; h64 = h64 * PRIME64_1 + PRIME64_4;
				v4 *= PRIME64_2; v4 = ((v4 << 31) | (v4 >> (64 - 31))) * PRIME64_1; h64 ^= v4; h64 = h64 * PRIME64_1 + PRIME64_4;

				p = (byte*) p64;
			}
			else
			{
				h64 = seed + PRIME64_5;
			}

			h64 += len;

			while (p <= bEnd - 8)
			{
				ulong k1;
				k1 = *((ulong*) p) * PRIME64_2; k1 = ((k1 << 31) | (k1 >> (64 - 31))) * PRIME64_1; h64 ^= k1;
				h64 = ((h64 << 27) | (h64 >> (64 - 27))) * PRIME64_1 + PRIME64_4;
				p += 8;
			}

			if (p <= bEnd - 4)
			{
				h64 ^= *((uint*) p) * PRIME64_1;
				h64 = ((h64 << 23) | (h64 >> (64 - 23))) * PRIME64_2 + PRIME64_3;
				p += 4;
			}

			while (p < bEnd)
			{
				h64 ^= (*p) * PRIME64_5;
				h64 = ((h64 << 11) | (h64 >> (64 - 11))) * PRIME64_1;
				p++;
			}

			h64 ^= h64 >> 33;
			h64 *= PRIME64_2;
			h64 ^= h64 >> 29;
			h64 *= PRIME64_3;
			h64 ^= h64 >> 32;
			return h64;
		}

		/// <summary>Context for computing the hash in a streaming scenario</summary>
		[DebuggerDisplay("Total={Total}, Seed={Seed}")]
		public struct StreamContext
		{

			public StreamContext(ulong seed)
			{
				this.Seed = seed;
				this.Total = 0;
				this.V1 = seed + PRIME64_1 + PRIME64_2;
				this.V2 = seed + PRIME64_2;
				this.V3 = seed + 0;
				this.V4 = seed - PRIME64_1;
			}

			/// <summary>Seed for this hash computation</summary>
			public readonly ulong Seed;

			/// <summary>Number of bytes written to this context</summary>
			public ulong Total;

			// internal coefficient updated when appending chunks of at least 32 bytes

			internal ulong V1;
			internal ulong V2;
			internal ulong V3;
			internal ulong V4;

		}

		/// <summary>Start a new streaming context</summary>
		/// <param name="seed">Seed for the computation</param>
		/// <returns>Instance that should be passed by reference to <see cref="AppendData"/> or <see cref="CompleteStream"/></returns>
		public static StreamContext StartStream(ulong seed = INITIAL_SEED)
		{
			return new StreamContext(seed);
		}

		/// <summary>Append the next chunk of data to a running hash computation</summary>
		/// <param name="state">Streaming context (created by <see cref="StartStream"/>)</param>
		/// <param name="input">New chunk of data to append</param>
		/// <returns>Number of bytes consumed from <paramref name="input"/>.</returns>
		/// <remarks>
		/// <para>This method will only consume multiples of 32 bytes chunks.</para>
		/// <para>If <paramref name="input"/> is smaller than 32 bytes, nothing will be done and the method will return 0.</para>
		/// <para>If the method returns less than the size of <paramref name="input"/>, the caller is responsible for keeping track the extra bytes that were not consume, and present them again on the next call, once more data is available.</para>
		/// </remarks>
		public static unsafe int AppendData(ref StreamContext state, ReadOnlySpan<byte> input)
		{
			fixed (byte* pInput = input)
			{
				return (int) AppendDataUnsafe(ref state, pInput, (uint) input.Length);
			}
		}

		internal static unsafe uint AppendDataUnsafe(ref StreamContext state, byte* input, uint len)
		{
			Contract.Debug.Requires(BitConverter.IsLittleEndian, "Not implemented for Big Endian architectures !");
			if (state is { V1: ulong.MaxValue, V2: ulong.MaxValue, V3: ulong.MaxValue, V4: ulong.MaxValue })
			{
				throw new InvalidOperationException("Streaming hash context has already been completed");
			}

			if (len == 0) return 0;
			Contract.PointerNotNull(input);

			byte* p = input;
			byte* bEnd = input + len;

			if (len < 32) return 0;

			ulong* p64 = (ulong*) p;
			ulong* limit = (ulong*) (bEnd - 32);
			ulong v1 = state.V1;
			ulong v2 = state.V2;
			ulong v3 = state.V3;
			ulong v4 = state.V4;

			do
			{
				// for(# in 1..4) => v# += XXH_get64bits(p) * PRIME64_2; p+=8; v# = XXH_rotl64(v#, 31); v# *= PRIME64_1;
				v1 += *(p64 + 0) * PRIME64_2;
				v1 = ((v1 << 31) | (v1 >> (64 - 31))) * PRIME64_1;
				v2 += *(p64 + 1) * PRIME64_2;
				v2 = ((v2 << 31) | (v2 >> (64 - 31))) * PRIME64_1;
				v3 += *(p64 + 2) * PRIME64_2;
				v3 = ((v3 << 31) | (v3 >> (64 - 31))) * PRIME64_1;
				v4 += *(p64 + 3) * PRIME64_2;
				v4 = ((v4 << 31) | (v4 >> (64 - 31))) * PRIME64_1;
				p64 += 4; // note: 4 x sizeof(ulong) == 32
			} while (p64 <= limit);

			state.V1 = v1;
			state.V2 = v2;
			state.V3 = v3;
			state.V4 = v4;

			state.Total += len & 0xFFFFFFE0;

			return len & 0xFFFFFFE0;
		}

		/// <summary>Append the last chunk of data and complete the hash computation for a stream</summary>
		/// <param name="state">Streaming context (created by <see cref="StartStream"/>, and updated by calling <see cref="AppendData"/>)</param>
		/// <param name="final">Final chunk of data that MUST be smaller than 32 bytes.</param>
		/// <returns>Hashcode for the stream.</returns>
		public static unsafe ulong CompleteStream(ref StreamContext state, ReadOnlySpan<byte> final)
		{
			fixed (byte* pInput = final)
			{
				return CompleteStreamUnsafe(ref state, pInput, (uint) final.Length);
			}
		}

		internal static unsafe ulong CompleteStreamUnsafe(ref StreamContext state, byte* input, uint len)
		{
			if (state is { V1: ulong.MaxValue, V2: ulong.MaxValue, V3: ulong.MaxValue, V4: ulong.MaxValue })
			{
				throw new InvalidOperationException("Streaming hash context has already been completed");
			}

			byte* p = input;
			if (p == null)
			{
				// only allow a null pointer if length is 0
				if (len > 0) throw new ArgumentNullException(nameof(input));
				p = (byte*) 16;
			}

			byte* bEnd = p + len;
			ulong h64;

			state.Total += len;
			ulong totalLength = state.Total;

			if (totalLength >= 32)
			{
				ulong* p64 = (ulong*) p;
				ulong* limit = (ulong*) (bEnd - 32);
				var v1 = state.V1;
				var v2 = state.V2;
				var v3 = state.V3;
				var v4 = state.V4;

				// h64 = XXH_rotl64(v1, 1) + XXH_rotl64(v2, 7) + XXH_rotl64(v3, 12) + XXH_rotl64(v4, 18)
				h64 = ((v1 << 1) | (v1 >> (64 - 1))) + ((v2 << 7) | (v2 >> (64 - 7))) + ((v3 << 12) | (v3 >> (64 - 12))) + ((v4 << 18) | (v4 >> (64 - 18)));

				// for(# in 1..4) =>
				//	v# *= PRIME64_2; v# = XXH_rotl64(v#, 31); v# *= PRIME64_1; h64 ^= v#;
				//	h64 = h64 * PRIME64_1 + PRIME64_4;

				v1 *= PRIME64_2; v1 = ((v1 << 31) | (v1 >> (64 - 31))) * PRIME64_1; h64 ^= v1; h64 = h64 * PRIME64_1 + PRIME64_4;
				v2 *= PRIME64_2; v2 = ((v2 << 31) | (v2 >> (64 - 31))) * PRIME64_1; h64 ^= v2; h64 = h64 * PRIME64_1 + PRIME64_4;
				v3 *= PRIME64_2; v3 = ((v3 << 31) | (v3 >> (64 - 31))) * PRIME64_1; h64 ^= v3; h64 = h64 * PRIME64_1 + PRIME64_4;
				v4 *= PRIME64_2; v4 = ((v4 << 31) | (v4 >> (64 - 31))) * PRIME64_1; h64 ^= v4; h64 = h64 * PRIME64_1 + PRIME64_4;

				p = (byte*) p64;
			}
			else
			{
				h64 = state.Seed + PRIME64_5;
			}

			h64 += totalLength;

			while (p <= bEnd - 8)
			{
				ulong k1;
				k1 = *((ulong*) p) * PRIME64_2; k1 = ((k1 << 31) | (k1 >> (64 - 31))) * PRIME64_1; h64 ^= k1;
				h64 = ((h64 << 27) | (h64 >> (64 - 27))) * PRIME64_1 + PRIME64_4;
				p += 8;
			}

			if (p <= bEnd - 4)
			{
				h64 ^= *((uint*) p) * PRIME64_1;
				h64 = ((h64 << 23) | (h64 >> (64 - 23))) * PRIME64_2 + PRIME64_3;
				p += 4;
			}

			while (p < bEnd)
			{
				h64 ^= (*p) * PRIME64_5;
				h64 = ((h64 << 11) | (h64 >> (64 - 11))) * PRIME64_1;
				p++;
			}

			h64 ^= h64 >> 33;
			h64 *= PRIME64_2;
			h64 ^= h64 >> 29;
			h64 *= PRIME64_3;
			h64 ^= h64 >> 32;

			state.V1 = ulong.MaxValue;
			state.V2 = ulong.MaxValue;
			state.V3 = ulong.MaxValue;
			state.V4 = ulong.MaxValue;
			return h64;
		}

#if FULL_DEBUG
		public static void Test()
		{

			var bytes = Encoding.UTF8.GetBytes("test");
			ulong h = XxHash64.Continue(123, bytes, 0, bytes.Length);
			Console.WriteLine(h + " : " + h.ToString("X16"));

			const string TEXT = "Il était une fois un petit chaperon rouge qui s'en allait au bois, et c'est alors que sa mère prit peur et l'envoya chez sa tante et son oncle à Bel Air";
			bytes = Encoding.UTF8.GetBytes(TEXT);

			h = XxHash64.Compute(bytes);
			Console.WriteLine(h + " : " + h.ToString("X16"));

			bytes = Encoding.UTF8.GetBytes("12345345234572");
			h = XxHash64.Continue(0x9747b28c, bytes, 0, bytes.Length);
			Console.WriteLine(h);
			Console.WriteLine(h.ToString("X8"));
			Console.WriteLine(h + " : " + h.ToString("X16"));
		}
#endif

	}

}
