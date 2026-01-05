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

namespace SnowBank.Text
{
	using System.Runtime.InteropServices;

	/// <summary>Options used to configure the behavior or <see cref="Base62Encoding"/> encoding and decoding methods.</summary>
	[Flags]
	public enum Base62FormattingOptions
	{
		None = 0,
		Padded = 0x1,
		Separator = 0x2,
		Lexicographic = 0x4,
	}

	/// <summary>Helper type for using the <b>Base62</b> encoding</summary>
	/// <remarks>
	/// <para>This encoding only uses alphanumeric characters, including lowercase and uppercase letters.</para>
	/// <para>There are two versions. The <b>regular</b> version (a-Z, 0-9, A-Z) is the most commonly used, and the <b>lexicographic</b> version (0-9, A-Z, a-z) which guarantees the same ordering between the string literal, and the corresponding bytes.</para>
	/// </remarks>
	public static class Base62Encoding
	{

		#region Encoding...

#if NET8_0_OR_GREATER

		/// <summary>Encodes a <see cref="Guid"/>> into a base62 representation</summary>
		public static string Encode(Guid value, Base62FormattingOptions options = default)
		{
			return Encode128(((Uuid128) value).ToUInt128(), 128, options);
		}

		/// <summary>Encodes a <see cref="Uuid128"/> into a base62 representation</summary>
		public static string Encode(Uuid128 value, Base62FormattingOptions options = default)
		{
			return Encode128(value.ToUInt128(), 128, options);
		}

		/// <summary>Encodes a <see cref="Uuid128"/> into a base62 representation</summary>
		public static string Encode(Uuid80 value, Base62FormattingOptions options = default)
		{
			return Encode128(value.ToUInt128(), 64, options);
		}

#endif

		/// <summary>Encodes a <see cref="Uuid128"/> into a base62 representation</summary>
		public static string Encode(Uuid64 value, Base62FormattingOptions options = default)
		{
			return Encode64(value.ToUInt64(), 64, options);
		}

		public static string Encode(uint value, Base62FormattingOptions options = default)
		{
			return Encode64(value, 32, options);
		}

		public static string Encode(ulong value, Base62FormattingOptions options = default)
		{
			return Encode64(value, 64, options);
		}

#if NET8_0_OR_GREATER

		public static string Encode(ulong high, ulong low, Base62FormattingOptions options = default)
		{
			var value = new UInt128(high, low);
			return Encode128(value, 128, options);
		}

		public static string Encode(UInt128 value, Base62FormattingOptions options = default)
		{
			return Encode128(value, 128, options);
		}

		public static bool TryEncodeTo(Span<char> destination, out int charsWritten, UInt128 value, Base62FormattingOptions options = default)
		{
			return TryEncode128(destination, out charsWritten, value, 128, options);
		}

#endif

		public static string EncodeSortable(uint value)
		{
			return Encode64(value, 32, Base62FormattingOptions.Padded | Base62FormattingOptions.Lexicographic);
		}

		public static string EncodeSortable(ulong value)
		{
			return Encode64(value, 64, Base62FormattingOptions.Padded | Base62FormattingOptions.Lexicographic);
		}

#if NET8_0_OR_GREATER

		public static string EncodeSortable(UInt128 value)
		{
			return Encode128(value, 64, Base62FormattingOptions.Padded | Base62FormattingOptions.Lexicographic);
		}

#endif

		private static int GetMaxSize(int bits)
		{
			// we use a lookup table to pre-compute the number of characters needed to encode a value of 'N' bits in base 62

			// if the table ever needs to be recomputed or expanded, here is the code for it:
#if RECOMPUTE_LOOKUP_TABLES
			static void RecomputeBitSizes()
			{
				const double BASE62_LOG = 4.1271343850450915553463964460005;

				var sizes = new int[129]; // 0 .. 128
				sizes[0] = 1; // "a"
				for (int i = 1; i < sizes.Length; i++)
				{
					sizes[i] = (int) Math.Ceiling(Math.Log(Math.Pow(2, i)) / BASE62_LOG);
				}

				var sb = new StringBuilder();
				sb.Append("[ ");
				for (int i = 0; i < sizes.Length; i++)
				{
					if (i > 0) sb.Append(", ");
					sb.Append($"{sizes[i],2}");
				}
				sb.Append(" ];");
				Console.WriteLine(sb.ToString());
			}
#endif

			ReadOnlySpan<int> bitSizes =
			[
				1,
				1,  1,  1,  1,  1,  2,  2,  2,  2,  2,  2,  3,  3,  3,  3,  3,
				3,  4,  4,  4,  4,  4,  4,  5,  5,  5,  5,  5,  5,  6,  6,  6,
				6,  6,  6,  7,  7,  7,  7,  7,  7,  8,  8,  8,  8,  8,  8,  9,
				9,  9,  9,  9,  9, 10, 10, 10, 10, 10, 10, 11, 11, 11, 11, 11,
				11, 12, 12, 12, 12, 12, 12, 13, 13, 13, 13, 13, 13, 14, 14, 14,
				14, 14, 14, 15, 15, 15, 15, 15, 15, 16, 16, 16, 16, 16, 16, 17,
				17, 17, 17, 17, 17, 18, 18, 18, 18, 18, 18, 19, 19, 19, 19, 19,
				19, 20, 20, 20, 20, 20, 20, 21, 21, 21, 21, 21, 21, 22, 22, 22
			];

			if ((uint) bits >= bitSizes.Length)
			{
				throw new ArgumentOutOfRangeException(nameof(bits), bits, "Bits number not supported");
			}

			return bitSizes[bits];
		}

		private static ReadOnlySpan<char> GetEncodeMap(Base62FormattingOptions options)
		{
			// we use a different map for Lexicographic vs AlphaNum
			ReadOnlySpan<char> base62AlphaNumMap = "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
			ReadOnlySpan<char> base62LexicographicMap = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

			return options.HasFlag(Base62FormattingOptions.Lexicographic)
				? base62LexicographicMap
				: base62AlphaNumMap;
		}

		public static string Encode64(ulong value, int bits, Base62FormattingOptions options)
		{
			bool fast = !options.HasFlag(Base62FormattingOptions.Padded);

			Span<char> chars = stackalloc char[GetMaxSize(bits)];
			ref char map = ref Unsafe.AsRef(in MemoryMarshal.GetReference(GetEncodeMap(options)));

			for (int pc = chars.Length - 1; pc >= 0; --pc)
			{
				chars[pc] = Unsafe.Add(ref map, (int) (value % 62));
				value /= 62;
				if (fast && value == 0)
				{
					return chars.Slice(pc).ToString();
				}
			}

			Contract.Debug.Ensures(value == 0);

			return chars.ToString();
		}

#if NET8_0_OR_GREATER

		private static string Encode128(UInt128 value, int bits, Base62FormattingOptions options)
		{
			bool fast = !options.HasFlag(Base62FormattingOptions.Padded);

			Span<char> chars = stackalloc char[GetMaxSize(bits)];
			ref char map = ref Unsafe.AsRef(ref MemoryMarshal.GetReference(GetEncodeMap(options)));

			for (int pc = chars.Length - 1; pc >= 0; --pc)
			{
				chars[pc] = Unsafe.Add(ref map, (int) (value % 62));
				value /= 62;
				if (fast && value == 0)
				{
					return chars.Slice(pc).ToString();
				}
			}

			Contract.Debug.Ensures(value == 0);

			return new string(chars);
		}

		private static bool TryEncode128(Span<char> destination, out int charsWritten, UInt128 value, int bits, Base62FormattingOptions options)
		{
			int size = GetMaxSize(bits);

			if (!options.HasFlag(Base62FormattingOptions.Padded))
			{ // no padding: may be smaller!

				int actualSize = 0;
				var tmp = value;
				for (int pc = size - 1; pc >= 0; --pc)
				{
					actualSize++;
					tmp /= 62;
					if (tmp == 0) break;
				}
				size = actualSize;
			}

			if (destination.Length < size)
			{
				charsWritten = 0;
				return false;
			}

			ref char map = ref Unsafe.AsRef(ref MemoryMarshal.GetReference(GetEncodeMap(options)));

			for (int pc = size - 1; pc >= 0; --pc)
			{
				destination[pc] = Unsafe.Add(ref map, (int) (value % 62));
				value /= 62;
			}

			Contract.Debug.Ensures(value == 0);
			charsWritten = size;
			return true;
		}

#endif

		#endregion

		#region Decoding...

		/// <summary>Decodes a 32-bits unsigned integer from a previously encoded base62 string literal</summary>
		public static uint DecodeUInt32(ReadOnlySpan<char> value, Base62FormattingOptions options = default)
		{
			return checked((uint) Decode64(value, 32, options));
		}

		/// <summary>Decodes a 32-bits unsigned integer from a previously encoded base62 string literal</summary>
		public static bool TryDecodeUInt32(ReadOnlySpan<char> value, out uint decoded, Base62FormattingOptions options = default)
		{
			if (!TryDecode64(value, out var x, 32, options) && x > uint.MaxValue)
			{
				decoded = 0;
				return false;
			}

			decoded = unchecked((uint) x);
			return true;
		}

		/// <summary>Decodes a 64-bits unsigned integer from a previously encoded base62 string literal</summary>
		public static ulong DecodeUInt64(ReadOnlySpan<char> value, Base62FormattingOptions options = default)
		{
			return Decode64(value, 64, options);
		}

		/// <summary>Decodes a 64-bits unsigned integer from a previously encoded base62 string literal</summary>
		public static bool TryDecodeUInt64(ReadOnlySpan<char> value, out ulong decoded, Base62FormattingOptions options = default)
		{
			return TryDecode64(value, out decoded, 64, options);
		}

#if NET8_0_OR_GREATER

		/// <summary>Decodes a 128-bits unsigned integer from a previously encoded base62 string literal</summary>
		public static UInt128 DecodeUInt128(ReadOnlySpan<char> value, Base62FormattingOptions options = default)
		{
			return Decode128(value, 128, options);
		}

		/// <summary>Decodes a 128-bits unsigned integer from a previously encoded base62 string literal</summary>
		public static bool TryDecodeUInt128(ReadOnlySpan<char> value, out UInt128 decoded, Base62FormattingOptions options = default)
		{
			return TryDecode128(value, out decoded, 128, options);
		}

#endif

		private const int DecodeMapOffset = '0';

		private const int DecodeMapSize = ('z' - '0') + 1;

		private static ReadOnlySpan<int> GetDecodeMap(Base62FormattingOptions options)
		{
			// we use a lookup table that is the "inverse" of the encoding map.
			// For each character between '0' and 'z' (inclusive) we store either -1 if the character is invalid, or the corresponding decimal value between 0 and 61 

#if RECOMPUTE_LOOKUP_TABLES
			RecomputeDecodeMap(GetEncodeMap(Base62FormattingOptions.None));
			RecomputeDecodeMap(GetEncodeMap(Base62FormattingOptions.Lexicographic));
			void RecomputeDecodeMap(ReadOnlySpan<char> map)
			{
				Span<int> xs = new int[DecodeMapSize];
				xs.Fill(-1);
				for (int i = 0; i < map.Length; i++)
				{
					xs[map[i] - DecodeMapOffset] = i;
				}

				var sb = new StringBuilder();
				sb.Append("[ ");
				for (int i = 0; i < xs.Length; i++)
				{
					if (i > 0) sb.Append(", ");
					sb.Append($"{xs[i],2}");
				}
				sb.Append(" ];");
				Console.WriteLine(sb.ToString());
			}
#endif

			ReadOnlySpan<int> base62AlphaNumMap =
			[ 
				/* 0 .. 9 */
				26, 27, 28, 29, 30, 31, 32, 33, 34, 35,
				/* ... */
				-1, -1, -1, -1, -1, -1, -1,
				/* A .. Z */
				36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61,
				/* ... */
				-1, -1, -1, -1, -1, -1,
				/* a .. Z */
				 0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25
			];

			ReadOnlySpan<int> base62LexicographicMap =
			[
				/* 0 .. 9 */
				0,  1,  2,  3,  4,  5,  6,  7,  8,  9,
				/* ... */
				-1, -1, -1, -1, -1, -1, -1,
				/* A .. Z */
				10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35,
				/* ... */
				-1, -1, -1, -1, -1, -1,
				/* a .. z */
				36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61
			];

			return options.HasFlag(Base62FormattingOptions.Lexicographic)
				? base62LexicographicMap
				: base62AlphaNumMap;
		}

		private static ulong Decode64(ReadOnlySpan<char> literal, int bits, Base62FormattingOptions options)
		{
			int maxSize = GetMaxSize(bits);
			if (literal.Length > maxSize)
			{
				throw new ArgumentException($"Value is too large to fit in a {bits} bits integer", nameof(literal));
			}

			ref int map = ref Unsafe.AsRef(in MemoryMarshal.GetReference(GetDecodeMap(options)));

			ulong acc = 0;
			foreach (var c in literal)
			{
				int x = c - DecodeMapOffset;
				x = (uint) x < DecodeMapSize ? Unsafe.Add(ref map, x) : -1;
				if (x < 0)
				{
					goto invalid;
				}

				acc = checked(acc * 62 + (uint) x);
			}
			return acc;

		invalid:
			throw new ArgumentException("Malformed base62 literal");
		}

		private static bool TryDecode64(ReadOnlySpan<char> literal, out ulong decoded, int bits, Base62FormattingOptions options)
		{
			int maxSize = GetMaxSize(bits);
			if (literal.Length > maxSize)
			{
				goto invalid;
			}

			ref int map = ref Unsafe.AsRef(in MemoryMarshal.GetReference(GetDecodeMap(options)));

			ulong acc = 0;
			foreach (var c in literal)
			{
				int x = c - DecodeMapOffset;
				x = (uint) x < DecodeMapSize ? Unsafe.Add(ref map, x) : -1;
				if (x < 0)
				{
					goto invalid;
				}

				acc = checked(acc * 62 + (uint) x);
			}
			decoded = acc;
			return true;

		invalid:
			decoded = 0;
			return false;
		}

#if NET8_0_OR_GREATER

		private static UInt128 Decode128(ReadOnlySpan<char> literal, int bits, Base62FormattingOptions options)
		{
			int maxSize = GetMaxSize(bits);
			if (literal.Length > maxSize)
			{
				throw new ArgumentException($"Value is too large to fit in a {bits} bits integer", nameof(literal));
			}

			ref int map = ref Unsafe.AsRef(ref MemoryMarshal.GetReference(GetDecodeMap(options)));

			UInt128 acc = 0;
			foreach (var c in literal)
			{
				int x = c - DecodeMapOffset;
				x = (uint) x < DecodeMapSize ? Unsafe.Add(ref map, x) : -1;
				if (x < 0)
				{
					goto invalid;
				}

				acc = checked(acc * 62 + (uint) x);
			}
			return acc;

		invalid:
			throw new ArgumentException("Malformed base62 literal");

		}

		private static bool TryDecode128(ReadOnlySpan<char> literal, out UInt128 decoded, int bits, Base62FormattingOptions options)
		{
			int maxSize = GetMaxSize(bits);
			if (literal.Length > maxSize)
			{
				goto invalid;
			}

			ref int map = ref Unsafe.AsRef(ref MemoryMarshal.GetReference(GetDecodeMap(options)));

			UInt128 acc = 0;
			foreach (var c in literal)
			{
				int x = c - DecodeMapOffset;
				x = (uint) x < DecodeMapSize ? Unsafe.Add(ref map, x) : -1;
				if (x < 0)
				{
					goto invalid;
				}

				acc = checked(acc * 62 + (uint) x);
			}
			decoded = acc;
			return true;

		invalid:
			decoded = 0;
			return false;

		}

#endif

		#endregion

	}

}
