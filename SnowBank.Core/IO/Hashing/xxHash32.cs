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

	/// <summary>Calcul de hash xxHash sur 32 bits</summary>
	/// <remarks>IMPORTANT: Ce hash n'est PAS cryptographique ! Il peut leaker des informations sur les données hashées, et ne doit donc pas être utilisé publiquement dans un scenario de protection de données! (il faut plutot utiliser SHA ou HMAC pour ce genre de choses)</remarks>
	[Obsolete("Use System.IO.Hashing.XxHash32 instead")]
	public static class XxHash32
	{
		// From https://code.google.com/p/xxhash/

		// Benchmark (see https://code.google.com/p/xxhash/wiki/xxh32)

		//		Name            Speed       Q.Score   Author
		//		xxHash          5.4 GB/s     10
		//		MumurHash 3a    2.7 GB/s     10       Austin Appleby
		//		SpookyHash      2.0 GB/s     10       Bob Jenkins
		//		SBox            1.4 GB/s      9       Bret Mulvey
		//		Lookup3         1.2 GB/s      9       Bob Jenkins
		//		CityHash64      1.05 GB/s    10       Pike & Alakuijala
		//		FNV             0.55 GB/s     5       Fowler, Noll, Vo
		//		CRC32           0.43 GB/s     9
		//		MD5-32          0.33 GB/s    10       Ronald L. Rivest
		//		SHA1-32         0.28 GB/s    10

		// WEIRD: Dans la pratique, L'implémentation ci dessous donne 2.4 GB/s avec xxHash alors que MurmurHash obtiens 4.3GB/s ??

		private const uint INITIAL_SEED = 0;

		private const uint PRIME32_1 = 2654435761U;
		private const uint PRIME32_2 = 2246822519U;
		private const uint PRIME32_3 = 3266489917U;
		private const uint PRIME32_4 =  668265263U;
		private const uint PRIME32_5 =  374761393U;

		/// <summary>Hash of the empty buffer</summary>
		public const uint HASH0 = 0x02CC5D05;

		/// <summary>Calcul le xxHash 32-bit d'un chaîne (a partir de sa représentation UTF-16 en mémoire)</summary>
		/// <param name="text">Chaîne de texte à convertir</param>
		/// <returns>Code xxHash 32 bit calculé sur la représentation UTF-16 de la chaîne. Attention: N'est pas garantit unique!</returns>
		public static uint Compute(string text)
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

		public static ulong Compute(string text, int offset, int count)
		{
			Contract.DoesNotOverflow(text, offset, count);
			unsafe
			{
				if (count == 0) return ContinueUnsafe(INITIAL_SEED, null, 0);
				fixed (char* chars = text)
				{
					return ContinueUnsafe(INITIAL_SEED, (byte*) (chars + offset), checked((uint) count * sizeof(char)));
				}
			}
		}

		/// <summary>Calcul le xxHash 32-bit d'un chaîne (a partir de sa représentation UTF-16 en mémoire)</summary>
		/// <returns>Code xxHash 32 bit calculé sur la représentation UTF-16 de la chaîne. Attention: N'est pas garantit unique!</returns>
		public static uint Compute(char[] buffer, int offset, int count)
		{
			Contract.DoesNotOverflow(buffer, offset, count);
			unsafe
			{
				if (count == 0) return ContinueUnsafe(INITIAL_SEED, null, 0);
				fixed (char* chars = &buffer[offset])
				{
					return ContinueUnsafe(INITIAL_SEED, (byte*) chars, checked((uint) count * sizeof(char)));
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Compute(byte[] input)
		{
			Contract.NotNull(input);
			return Continue(INITIAL_SEED, input, 0, input.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Compute(Slice buffer)
		{
			return Continue(INITIAL_SEED, buffer.Array, buffer.Offset, buffer.Count);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Compute(ReadOnlySpan<byte> buffer)
		{
			return Continue(INITIAL_SEED, buffer);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Compute(byte[] input, int offset, int len)
		{
			Contract.NotNull(input);
			return Continue(INITIAL_SEED, input, offset, len);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe uint Compute(byte* input, uint len)
		{
			Contract.PointerNotNull(input);

			return ContinueUnsafe(INITIAL_SEED, input, len);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Continue(uint seed, byte[] input)
		{
			Contract.NotNull(input);
			return Continue(seed, input, 0, input.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Continue(uint seed, Slice buffer)
		{
			return Continue(seed, buffer.Array, buffer.Offset, buffer.Count);
		}

		public static uint Continue(uint seed, byte[] input, int offset, int len)
		{
			Contract.DoesNotOverflow(input, offset, len);

			unsafe
			{
				if (len == 0) return ContinueUnsafe(seed, null, 0);
				fixed (byte* ptr = input)
				{
					return ContinueUnsafe(seed, ptr + offset, (uint) len);
				}
			}
		}

		public static uint Continue(uint seed, ReadOnlySpan<byte> buffer)
		{
			unsafe
			{
				if (buffer.Length == 0) return ContinueUnsafe(seed, null, 0);
				fixed (byte* ptr = &MemoryMarshal.GetReference(buffer))
				{
					return ContinueUnsafe(seed, ptr, (uint) buffer.Length);
				}
			}
		}

		public static unsafe uint ContinueUnsafe(uint seed, byte* input, uint len)
		{
			Contract.Debug.Requires(input != null || len == 0);
			Contract.Debug.Requires(BitConverter.IsLittleEndian, "Not implemented for Big Endian architectures !");

			if (len == 0 && seed == INITIAL_SEED) return HASH0;

			byte* p = input;
			if (p == null)
			{
				// autorise le null pointer (UNIQUEMENT si len == 0)!
				if (len > 0) throw ThrowHelper.ArgumentNullException(nameof(input));
				p = (byte*) 16;
			}

			byte* bEnd = p + len;
			uint h32;

			if (len >= 16)
			{
				uint* p32 = (uint*) p;
				uint* limit = (uint*) (bEnd - 16);
				uint v1 = seed + PRIME32_1 + PRIME32_2;
				uint v2 = seed + PRIME32_2;
				uint v3 = seed + 0;
				uint v4 = seed - PRIME32_1;

				do
				{
					v1 += *(p32) * PRIME32_2; v1 = ((v1 << 13) | (v1 >> (32 - 13))) * PRIME32_1;
					v2 += *(p32 + 1) * PRIME32_2; v2 = ((v2 << 13) | (v2 >> (32 - 13))) * PRIME32_1;
					v3 += *(p32 + 2) * PRIME32_2; v3 = ((v3 << 13) | (v3 >> (32 - 13))) * PRIME32_1;
					v4 += *(p32 + 3) * PRIME32_2; v4 = ((v4 << 13) | (v4 >> (32 - 13))) * PRIME32_1;
					p32 += 4;
				}
				while (p32 <= limit);

				h32 = ((v1 << 1) | (v1 >> (32 - 1))) + ((v2 << 7) | (v2 >> (32 - 7))) + ((v3 << 12) | (v3 >> (32 - 12))) + ((v4 << 18) | (v4 >> (32 - 18)));
				p = (byte*) p32;
			}
			else
			{
				h32 = seed + PRIME32_5;
			}

			h32 += len;
			while (p <= bEnd - 4)
			{
				h32 += *((uint*) p) * PRIME32_3;
				h32 = ((h32 << 17) | (h32 >> (32 - 17))) * PRIME32_4;
				p += 4;
			}
			while (p < bEnd)
			{
				h32 += (*p) * PRIME32_5;
				h32 = ((h32 << 11) | (h32 >> (32 - 11))) * PRIME32_1;
				p++;
			}
			h32 ^= h32 >> 15;
			h32 *= PRIME32_2;
			h32 ^= h32 >> 13;
			h32 *= PRIME32_3;
			h32 ^= h32 >> 16;
			return h32;
		}

#if FULL_DEBUG
		public static void Test()
		{

			var bytes = Encoding.UTF8.GetBytes("test");
			uint h = XxHash32.Continue(123, bytes, 0, bytes.Length);
			Console.WriteLine(h + " : " + h.ToString("X8"));

			const string TEXT = "Il était une fois un petit chaperon rouge qui s'en allait au bois, et c'est alors que sa mère prit peur et l'envoya chez sa tante et son oncle à Bel Air";
			bytes = Encoding.UTF8.GetBytes(TEXT);

			h = XxHash32.Compute(bytes);
			Console.WriteLine(h + " : " + h.ToString("X8"));

			bytes = Encoding.UTF8.GetBytes("12345345234572");
			h = XxHash32.Continue(0x9747b28c, bytes, 0, bytes.Length);
			Console.WriteLine(h);
			Console.WriteLine(h.ToString("X8"));
			Console.WriteLine(h + " : " + h.ToString("X8"));
		}
#endif

	}

}
