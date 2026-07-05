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

#define USE_FAST_STRING_ALLOCATOR

namespace SnowBank.Text
{
	using System.Buffers.Text;
	using System.IO;
	using System.Runtime.InteropServices;
	using SnowBank.Buffers.Binary;
	using SnowBank.Buffers.Text;
#if NETSTANDARD2_0
	using MemoryMarshal = SnowBank.Compat.MemoryMarshalCompat; // shim adds CreateReadOnlySpan/CreateSpan/AsRef, absent from netstandard2.0
	using Convert = SnowBank.Compat.ConvertCompat; // shim adds the ReadOnlySpan<byte> ToBase64String overload, absent from netstandard2.0
#endif

	/// <summary>Helper type for using the <b>Base62</b> encoding</summary>
	[PublicAPI]
	public static class Base64Encoding
	{

		private const int CHARMAP_PAGE_SIZE = 256;

		#region Encoding Maps...

		private const char Base64PadChar = '=';

		/// <summary>Regular Base64 character map</summary>
		private static readonly char[] Base64CharMap =
		[
			// HI
			'A', 'A', 'A', 'A', 'B', 'B', 'B', 'B', 'C', 'C',
			'C', 'C', 'D', 'D', 'D', 'D', 'E', 'E', 'E', 'E',
			'F', 'F', 'F', 'F', 'G', 'G', 'G', 'G', 'H', 'H',
			'H', 'H', 'I', 'I', 'I', 'I', 'J', 'J', 'J', 'J',
			'K', 'K', 'K', 'K', 'L', 'L', 'L', 'L', 'M', 'M',
			'M', 'M', 'N', 'N', 'N', 'N', 'O', 'O', 'O', 'O',
			'P', 'P', 'P', 'P', 'Q', 'Q', 'Q', 'Q', 'R', 'R',
			'R', 'R', 'S', 'S', 'S', 'S', 'T', 'T', 'T', 'T',
			'U', 'U', 'U', 'U', 'V', 'V', 'V', 'V', 'W', 'W',
			'W', 'W', 'X', 'X', 'X', 'X', 'Y', 'Y', 'Y', 'Y',
			'Z', 'Z', 'Z', 'Z', 'a', 'a', 'a', 'a', 'b', 'b',
			'b', 'b', 'c', 'c', 'c', 'c', 'd', 'd', 'd', 'd',
			'e', 'e', 'e', 'e', 'f', 'f', 'f', 'f', 'g', 'g',
			'g', 'g', 'h', 'h', 'h', 'h', 'i', 'i', 'i', 'i',
			'j', 'j', 'j', 'j', 'k', 'k', 'k', 'k', 'l', 'l',
			'l', 'l', 'm', 'm', 'm', 'm', 'n', 'n', 'n', 'n',
			'o', 'o', 'o', 'o', 'p', 'p', 'p', 'p', 'q', 'q',
			'q', 'q', 'r', 'r', 'r', 'r', 's', 's', 's', 's',
			't', 't', 't', 't', 'u', 'u', 'u', 'u', 'v', 'v',
			'v', 'v', 'w', 'w', 'w', 'w', 'x', 'x', 'x', 'x',
			'y', 'y', 'y', 'y', 'z', 'z', 'z', 'z', '0', '0',
			'0', '0', '1', '1', '1', '1', '2', '2', '2', '2',
			'3', '3', '3', '3', '4', '4', '4', '4', '5', '5',
			'5', '5', '6', '6', '6', '6', '7', '7', '7', '7',
			'8', '8', '8', '8', '9', '9', '9', '9', '+', '+',
			'+', '+', '/', '/', '/', '/',
			// LO
			'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
			'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
			'U', 'V', 'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd',
			'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n',
			'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x',
			'y', 'z', '0', '1', '2', '3', '4', '5', '6', '7',
			'8', '9', '+', '/', 'A', 'B', 'C', 'D', 'E', 'F',
			'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P',
			'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
			'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
			'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
			'u', 'v', 'w', 'x', 'y', 'z', '0', '1', '2', '3',
			'4', '5', '6', '7', '8', '9', '+', '/', 'A', 'B',
			'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L',
			'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V',
			'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd', 'e', 'f',
			'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p',
			'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
			'0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
			'+', '/', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H',
			'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R',
			'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', 'a', 'b',
			'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l',
			'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v',
			'w', 'x', 'y', 'z', '0', '1', '2', '3', '4', '5',
			'6', '7', '8', '9', '+', '/'
		];

		private const char Base64UrlPadChar = '*';

		/// <summary>Web Safe Base64 character map</summary>
		private static readonly char[] Base64UrlCharMap =
		[
			// HI
			'A', 'A', 'A', 'A', 'B', 'B', 'B', 'B', 'C', 'C',
			'C', 'C', 'D', 'D', 'D', 'D', 'E', 'E', 'E', 'E',
			'F', 'F', 'F', 'F', 'G', 'G', 'G', 'G', 'H', 'H',
			'H', 'H', 'I', 'I', 'I', 'I', 'J', 'J', 'J', 'J',
			'K', 'K', 'K', 'K', 'L', 'L', 'L', 'L', 'M', 'M',
			'M', 'M', 'N', 'N', 'N', 'N', 'O', 'O', 'O', 'O',
			'P', 'P', 'P', 'P', 'Q', 'Q', 'Q', 'Q', 'R', 'R',
			'R', 'R', 'S', 'S', 'S', 'S', 'T', 'T', 'T', 'T',
			'U', 'U', 'U', 'U', 'V', 'V', 'V', 'V', 'W', 'W',
			'W', 'W', 'X', 'X', 'X', 'X', 'Y', 'Y', 'Y', 'Y',
			'Z', 'Z', 'Z', 'Z', 'a', 'a', 'a', 'a', 'b', 'b',
			'b', 'b', 'c', 'c', 'c', 'c', 'd', 'd', 'd', 'd',
			'e', 'e', 'e', 'e', 'f', 'f', 'f', 'f', 'g', 'g',
			'g', 'g', 'h', 'h', 'h', 'h', 'i', 'i', 'i', 'i',
			'j', 'j', 'j', 'j', 'k', 'k', 'k', 'k', 'l', 'l',
			'l', 'l', 'm', 'm', 'm', 'm', 'n', 'n', 'n', 'n',
			'o', 'o', 'o', 'o', 'p', 'p', 'p', 'p', 'q', 'q',
			'q', 'q', 'r', 'r', 'r', 'r', 's', 's', 's', 's',
			't', 't', 't', 't', 'u', 'u', 'u', 'u', 'v', 'v',
			'v', 'v', 'w', 'w', 'w', 'w', 'x', 'x', 'x', 'x',
			'y', 'y', 'y', 'y', 'z', 'z', 'z', 'z', '0', '0',
			'0', '0', '1', '1', '1', '1', '2', '2', '2', '2',
			'3', '3', '3', '3', '4', '4', '4', '4', '5', '5',
			'5', '5', '6', '6', '6', '6', '7', '7', '7', '7',
			'8', '8', '8', '8', '9', '9', '9', '9', '-', '-',
			'-', '-', '_', '_', '_', '_',
			// LO
			'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
			'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
			'U', 'V', 'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd',
			'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n',
			'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x',
			'y', 'z', '0', '1', '2', '3', '4', '5', '6', '7',
			'8', '9', '-', '_', 'A', 'B', 'C', 'D', 'E', 'F',
			'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P',
			'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
			'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
			'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
			'u', 'v', 'w', 'x', 'y', 'z', '0', '1', '2', '3',
			'4', '5', '6', '7', '8', '9', '-', '_', 'A', 'B',
			'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L',
			'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V',
			'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd', 'e', 'f',
			'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p',
			'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
			'0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
			'-', '_', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H',
			'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R',
			'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', 'a', 'b',
			'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l',
			'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v',
			'w', 'x', 'y', 'z', '0', '1', '2', '3', '4', '5',
			'6', '7', '8', '9', '-', '_'
		];

		#endregion

		#region Base64 Generic

		public static string ToBase64String(byte[] buffer)
		{
			Contract.NotNull(buffer);
#if NET9_0_OR_GREATER
			return Convert.ToBase64String(new ReadOnlySpan<byte>(buffer));
#else
			return EncodeBuffer(buffer.AsSpan(), padded: true, urlSafe: false);
#endif
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToBase64String(Slice buffer)
		{
#if NET9_0_OR_GREATER
			return Convert.ToBase64String(buffer.Span);
#else
			return EncodeBuffer(buffer.Span, padded: true, urlSafe: false);
#endif
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToBase64String(ReadOnlySpan<byte> buffer)
		{
#if NET9_0_OR_GREATER
			return Convert.ToBase64String(buffer);
#else
			return EncodeBuffer(buffer, padded: true, urlSafe: false);
#endif
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] FromBase64String(string s)
		{
			Contract.NotNull(s);
			return DecodeBuffer(s.AsSpan(), padded: true, urlSafe: false);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] FromBase64String(ReadOnlySpan<char> s)
		{
			return DecodeBuffer(s, padded: true, urlSafe: false);
		}

		#endregion

		#region Base64Url (WebSafe)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToBase64UrlString(byte[] buffer)
		{
			Contract.NotNull(buffer);
			return EncodeBuffer(buffer.AsSpan(), padded: false, urlSafe: true);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToBase64UrlString(ReadOnlySpan<byte> buffer)
		{
			return EncodeBuffer(buffer, padded: false, urlSafe: true);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToBase64UrlString(Slice buffer)
		{
			return EncodeBuffer(buffer.Span, padded: false, urlSafe: true);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] FromBase64UrlString(string s)
		{
			Contract.NotNull(s);
			return DecodeBuffer(s.AsSpan(), padded: false, urlSafe: true);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] FromBase64UrlString(ReadOnlySpan<char> s)
		{
			return DecodeBuffer(s, padded: false, urlSafe: true);
		}

		#endregion

		#region Common...

		/// <summary>Computes the number of characters required to encode byte buffer into Base64</summary>
		/// <param name="length">Number of bytes in the source</param>
		/// <param name="padded">Set to <c>true</c> if padding is required.</param>
		/// <returns>Minimum capacity required for the output character buffer, in order to encode a byte buffer of size <paramref name="length"/>, including the optional padding.</returns>
		[Pure]
		public static int GetCharsCount(int length, bool padded)
		{
			if (length < 0) throw new ArgumentOutOfRangeException(nameof(length), "Buffer length cannot be negative.");
			if (padded)
			{ // 3 source bytes are encoded as 4 output characters
				return checked((length + 2) / 3 * 4);
			}

			int chunks = checked((length / 3) * 4);
			return (length % 3) switch
			{
				0 => chunks,
				1 => checked(chunks + 2),
				_ => checked(chunks + 3)
			};
		}

		[Pure]
		public static int EstimateBytesCount(int length)
		{
			// 4 input characters are decoded as 3 output bytes
			return checked(length / 4 * 3 + 2);
		}

		[Pure]
		public static string EncodeBuffer(ReadOnlySpan<byte> buffer, bool padded = true, bool urlSafe = false)
		{
			if (buffer.Length == 0) return string.Empty;

			if (padded && !urlSafe)
			{
				return Convert.ToBase64String(buffer);
			}
#if NET9_0_OR_GREATER
			if (urlSafe && !padded)
			{
				return Base64Url.EncodeToString(buffer);
			}
#endif

			// compute the minimum buffer capacity (including optional padding)
			int size = GetCharsCount(buffer.Length, padded);

			// decode into the buffer
			return EncodeBufferUnsafe(buffer, size, padded, urlSafe);
		}

		[Pure]
		public static string EncodeBufferUnsafe(ReadOnlySpan<byte> source, bool padded = true, bool urlSafe = false)
		{
			if (source.Length == 0) return string.Empty;

			// compute the minimum buffer capacity (including optional padding)
			int size = GetCharsCount(source.Length, padded);

			// encode into the buffer
			return EncodeBufferUnsafe(source, size, padded, urlSafe);
		}

		/// <summary>Encodes a byte buffer into a Base64 string literal</summary>
		/// <param name="source">Source buffer with the bytes to encode</param>
		/// <param name="charCount">Pre-computed number of expected characters (via <see cref="GetCharsCount"/>)</param>
		/// <param name="padded">If <c>true</c>, pad the input buffer to have the length be a multiple of 3 bytes</param>
		/// <param name="urlSafe">If <c>true</c>, use the "web safe" character map (replaces '+' and '/' with '-' and '_')</param>
		/// <returns></returns>
		[Pure]
		private static string EncodeBufferUnsafe(ReadOnlySpan<byte> source, int charCount, bool padded, bool urlSafe)
		{
			Contract.Debug.Requires(charCount >= 0);

			char padChar = padded ? (urlSafe ? Base64UrlPadChar : Base64PadChar) : '\0';
			var charMap = urlSafe ? Base64UrlCharMap : Base64CharMap;

			//TODO: use String.Create(...) if available!

#if NET5_0_OR_GREATER && USE_FAST_STRING_ALLOCATOR
			// pre-allocate a string with the correct size

			var s = new string(' ', charCount);
			var b = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(s.AsSpan()), charCount);
			// encode directly into this string (!!)
			EncodeBufferUnsafe(b, source, charMap, padChar);
			return s;
#else
			// CAUTION: the mutate-a-fresh-string trick above must NOT be used here: the compat CreateSpan shim is
			// pointer-based, and its span dangles as soon as the GC moves the string (silent output corruption).
			// Encode into a transient array instead (one extra copy + allocation).
			char[] output = new char[charCount];
			int n = EncodeBufferUnsafe(output, source, charMap, padChar);
			return new string(output, 0, n);
#endif
		}

		/// <summary>Encodes a byte buffer into Base64 and writes the output into a <see cref="TextWriter"/></summary>
		/// <param name="output">Destination where to write the encoded Base64 text</param>
		/// <param name="source">Source buffer with the bytes to encode</param>
		/// <param name="padded">If <c>true</c>, pad the input buffer to have the length be a multiple of 3 bytes</param>
		/// <param name="urlSafe">If <c>true</c>, use the "web safe" character map (replaces '+' and '/' with '-' and '_')</param>
		public static void EncodeTo(TextWriter output, Slice source, bool padded = true, bool urlSafe = false)
		{
			EncodeTo(output, source.Span, padded, urlSafe);
		}

		/// <summary>Encodes a byte buffer into Base64 and writes the output into a <see cref="TextWriter"/></summary>
		/// <param name="output">Destination where to write the encoded Base64 text</param>
		/// <param name="source">Source buffer with the bytes to encode</param>
		/// <param name="padded">If <c>true</c>, pad the input buffer to have the length be a multiple of 3 bytes</param>
		/// <param name="urlSafe">If <c>true</c>, use the "web safe" character map (replaces '+' and '/' with '-' and '_')</param>
		public static void EncodeTo(TextWriter output, ReadOnlySpan<byte> source, bool padded = true, bool urlSafe = false)
		{
			Contract.NotNull(output);

			if (source.Length == 0) return;

			int size = GetCharsCount(source.Length, padded);
#if !NET9_0_OR_GREATER
			char padChar = !padded ? '\0' : urlSafe ? Base64UrlPadChar : Base64PadChar;
			var charMap = urlSafe ? Base64UrlCharMap : Base64CharMap;
#endif

			//TODO: if StringWriter, extraire le StringBuilder, et faire une version qui écrit directement dedans !

			// pour générer 4096 chars, il faut 3 072 bytes
			// pour générer 65536 chars, il faut 49 152 bytes
			const int CHARSIZE = 65536;
			const int BYTESIZE = (CHARSIZE / 4) * 3;

			int bufferSize = Math.Min(size, CHARSIZE);
			bufferSize = (bufferSize + 7) & (~0x7); // arrondi a 8 supérieur, pour garder un alignement correct
			Span<char> chars = stackalloc char[bufferSize];

			var remaining = source;
			while (remaining.Length > 0)
			{
				int sz = Math.Min(remaining.Length, BYTESIZE);
#if NET9_0_OR_GREATER
				Convert.TryToBase64Chars(remaining[..sz], chars, out int n);
#else
				int n = EncodeBufferUnsafe(chars, remaining[..sz], charMap, remaining.Length > BYTESIZE ? '\0' : padChar);
#endif
				Contract.Debug.Assert(n <= bufferSize);
				if (n <= 0) break; //REVIEW: dans quel cas on peut avoir <= 0 ???
				output.Write(chars);
				remaining = remaining[sz..];
			}
			if (remaining.Length > 0)
			{
				throw new InvalidOperationException(); // ??
			}
		}

#if !NETSTANDARD2_0
		// ValueStringWriter is not part of the netstandard2.0 backport; the Span/string Base64 overloads remain available.
		/// <summary>Encodes a byte buffer into Base64 and appends the output into a <see cref="ValueStringWriter"/></summary>
		/// <param name="output">Destination where to write the encoded Base64 text</param>
		/// <param name="source">Source buffer with the bytes to encode</param>
		/// <param name="padded">If <c>true</c>, pad the input buffer to have the length be a multiple of 3 bytes</param>
		/// <param name="urlSafe">If <c>true</c>, use the "web safe" character map (replaces '+' and '/' with '-' and '_')</param>
		public static void EncodeTo(ref ValueStringWriter output, ReadOnlySpan<byte> source, bool padded = true, bool urlSafe = false)
		{
			if (source.Length == 0) return;

			int size = GetCharsCount(source.Length, padded);
			int bufferSize = GetCharsCount(source.Length, padded);
			var chars = output.GetSpan(bufferSize);

#if NET9_0_OR_GREATER
			Convert.TryToBase64Chars(source, chars, out int n);
#else
			var charMap = urlSafe ? Base64UrlCharMap : Base64CharMap;
			int n = EncodeBufferUnsafe(chars, source, charMap, !padded ? '\0' : urlSafe ? Base64UrlPadChar : Base64PadChar);
#endif
			Contract.Debug.Assert(n == bufferSize);
			output.Advance(n);
		}
#endif

		#endregion

		#region Internal Helpers...

		/// <summary>Encodes a byte buffer into Base64 into memory</summary>
		internal static int EncodeBufferUnsafe(Span<char> output, ReadOnlySpan<byte> source, ReadOnlySpan<char> charMap, char padChar)
		{
			// portage en C# de modp_b64.c: https://code.google.com/p/stringencoders/source/browse/trunk/src/modp_b64.c

			ref byte inp = ref MemoryMarshal.GetReference(source);

			ref char e0 = ref MemoryMarshal.GetReference(charMap);
			ref char e1 = ref Unsafe.Add(ref e0, CHARMAP_PAGE_SIZE);
			//note: e2 == e1 ?

			// vérifie que le buffer peut contenir au moins les chunks complets
			ref char start = ref MemoryMarshal.GetReference(output);
			ref char stop = ref Unsafe.Add(ref start, output.Length);

			int len = source.Length;
			if (Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref start, ((source.Length / 3) * 4)), ref stop))
			{
				ThrowOutputBufferTooSmall();
			}

			ref char outp = ref start;
			int i, end;
			uint t1, t2, t3;

			// 4 bytes chunks...
			for (i = 0, end = len - 2; i < end; i += 3)
			{
				t1 = Unsafe.Add(ref inp, 0);
				t2 = Unsafe.Add(ref inp, 1);
				t3 = Unsafe.Add(ref inp, 2);
				Unsafe.Add(ref outp, 0) = Unsafe.Add(ref e0, t1);
				Unsafe.Add(ref outp, 1) = Unsafe.Add(ref e1, ((t1 & 0x03) << 4) | ((t2 >> 4) & 0x0F));
				Unsafe.Add(ref outp, 2) = Unsafe.Add(ref e1, ((t2 & 0x0F) << 2) | ((t3 >> 6) & 0x03));
				Unsafe.Add(ref outp, 3) = Unsafe.Add(ref e1, t3);
				inp = ref Unsafe.Add(ref inp, 3);
				outp = ref Unsafe.Add(ref outp, 4);
			}

			// remainder...
			switch (len - i)
			{
				case 0:
				{
					break;
				}
				case 1:
				{
					if (Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref outp,  2), ref stop))
					{
						ThrowOutputBufferTooSmall();
					}

					t1 = inp;
					Unsafe.Add(ref outp, 0) = Unsafe.Add(ref e0, t1);
					Unsafe.Add(ref outp, 1) = Unsafe.Add(ref e1, (t1 & 0x03) << 4);
					outp = ref Unsafe.Add(ref outp, 2);

					if (padChar != '\0')
					{
						if (Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref outp, 2), ref stop))
						{
							ThrowOutputBufferTooSmall();
						}
						Unsafe.Add(ref outp, 0) = padChar;
						Unsafe.Add(ref outp, 1) = padChar;
						outp = ref Unsafe.Add(ref outp, 2);
					}

					break;
				}
				default:
				{
					t1 = inp;
					t2 = Unsafe.Add(ref inp, 1);
					if (Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref outp, 3), ref stop))
					{
						ThrowOutputBufferTooSmall();
					}
					Unsafe.Add(ref outp, 0) = Unsafe.Add(ref e0, t1);
					Unsafe.Add(ref outp, 1) = Unsafe.Add(ref e1, ((t1 & 0x03) << 4) | ((t2 >> 4) & 0x0F));
					Unsafe.Add(ref outp, 2) = Unsafe.Add(ref e1, (t2 & 0x0F) << 2);
					outp = ref Unsafe.Add(ref outp, 3);

					if (padChar != '\0')
					{
						if (Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref outp, 1), ref stop))
						{
							ThrowOutputBufferTooSmall();
						}
						outp = padChar;
						outp = ref Unsafe.Add(ref outp, 1);
					}

					break;
				}
			}

			Contract.Debug.Ensures(!Unsafe.IsAddressLessThan(ref inp, ref MemoryMarshal.GetReference(source)) && !Unsafe.IsAddressGreaterThan(ref inp, ref Unsafe.Add(ref MemoryMarshal.GetReference(source), source.Length)));
			Contract.Debug.Ensures(!Unsafe.IsAddressLessThan(ref outp, ref MemoryMarshal.GetReference(output)) && !Unsafe.IsAddressGreaterThan(ref outp, ref stop));
			return (int) (Unsafe.ByteOffset(ref output[0], ref outp).ToInt64() / Unsafe.SizeOf<char>());
		}

		[ContractAnnotation("buffer:null => halt")]
		private static void EnsureBufferIsValid(byte[] buffer, int offset, int count)
		{
			Contract.NotNull(buffer);
			if (offset < 0) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(offset));
			if (count < 0 || offset + count > buffer.Length) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(count));
		}

		[ContractAnnotation("=> halt")]
		private static void ThrowOutputBufferTooSmall()
		{
			throw new InvalidOperationException("The output buffer is too small");
		}

		/// <summary>Computes the size of a buffer required to decode a Base64 string into bytes</summary>
		/// <param name="src">Base64 string literal to decode</param>
		/// <param name="padChar">Padding character (optional)</param>
		public static int GetBytesCount(ReadOnlySpan<char> src, char padChar)
		{
			int len = src.Length;
			if (len == 0)
			{
				return 0;
			}

			int padding;
			if (padChar != '\0')
			{
				ref char ptr = ref Unsafe.AsRef(in src[0]);
				padding = 0;
				while (len > 0)
				{
					if (Unsafe.Add(ref ptr, len - 1) != padChar)
					{
						break;
					}

					++padding;
					--len;
				}
				if (padding != 0)
				{
					padding = padding switch
					{
						1 => 2,
						2 => 1,
						_ => throw ThrowHelper.FormatException("The input is not a valid Base-64 string as it contains a non-base 64 character, more than two padding characters, or an illegal character among the padding characters.")
					};
				}
			}
			else
			{
				padding = (len % 4) switch
				{
					0 => 0,
					1 or 2 => 1,
					_ => 2
				};
			}
			return (len / 4) * 3 + padding;
		}

		/// <summary>Decodes a Base64 text literal into a byte array</summary>
		/// <param name="encoded">Base64 text to decode</param>
		/// <param name="padded">Set to <c>true</c> if the encoding used padding</param>
		/// <param name="urlSafe">Set to <c>true</c> if the original used the "web safe" character map (used '-' and '_' instead of '+' and '/')</param>
		/// <returns>Decoded byte array</returns>
		[Pure]
		public static byte[] DecodeBuffer(ReadOnlySpan<char> encoded, bool padded = true, bool urlSafe = false)
		{
			if (encoded.Length == 0) return [ ];

			char padChar = padded ? (urlSafe ? Base64UrlPadChar : Base64PadChar) : '\0';

			int size = GetBytesCount(encoded, padChar); //TODO: padding + urlSafe ?
			var dest = new byte[size];

			int p = DecodeBufferUnsafe(dest, encoded, padChar);
			if (p < 0) throw ThrowHelper.FormatException("Malformed base64 string");
			return dest;
		}

		/// <summary>Decodes a Base64 text literal into a byte buffer</summary>
		/// <param name="encoded">Base64 text to decode</param>
		/// <param name="buffer">Destination buffer. If <c>null</c> or two small, will be replaced with another buffer with sufficient capacity</param>
		/// <param name="padded">Set to <c>true</c> if the encoding used padding</param>
		/// <param name="urlSafe">Set to <c>true</c> if the original used the "web safe" character map (used '-' and '_' instead of '+' and '/')</param>
		/// <returns>Decoded byte buffer, which uses <paramref name="buffer"/> as the backing store</returns>
		[Pure]
		public static Slice DecodeBuffer(ReadOnlySpan<char> encoded, ref byte[]? buffer, bool padded = true, bool urlSafe = false)
		{
			if (encoded.Length == 0) return Slice.Empty;

			char padChar = padded ? (urlSafe ? Base64UrlPadChar : Base64PadChar) : '\0';

			// compute the size of the decoded bytes
			int size = GetBytesCount(encoded, padChar); //TODO: padding + urlSafe ?
			var tmp = UnsafeHelpers.EnsureCapacity(ref buffer, size);

			int p = DecodeBufferUnsafe(tmp, encoded, padChar);
			if (p < 0) throw new FormatException("Malformed base64 string");
			return new Slice(tmp, 0, p);
		}

		/// <summary>Decodes a Base64 text literal into a byte buffer</summary>
		internal static int DecodeBufferUnsafe(Span<byte> dest, ReadOnlySpan<char> src, char padChar)
		{
			int len = src.Length;
			if (len == 0) return 0;

			if (padChar != '\0')
			{
				if (len < 4 || len % 4 != 0)
				{ // doit être un multiple de 4
					throw ThrowHelper.FormatException("Invalid padding");
				}
				// ne doit pas y avoir plus de 2 pad chars a la fin
				if (src[len - 1] == padChar)
				{
					--len;
					if (src[len - 1] == padChar)
					{
						--len;
					}
				}
			}

			int leftOver = len % 4;
			int chunks = leftOver == 0 ? (len / 4 - 1) : (len / 4);

			ref byte outp = ref dest[0];
			ref byte inp = ref Unsafe.As<char, byte>(ref Unsafe.AsRef(in src[0])); //note: we will read 4 chars at a time (8 bytes), so treat the input as raw bytes
			uint x;
			ulong y = Unsafe.ReadUnaligned<ulong>(ref inp);
			inp = ref Unsafe.Add(ref inp, 8);

			ref uint d0 = ref DecodeMap0[0];
			ref uint d1 = ref DecodeMap1[0];
			ref uint d2 = ref DecodeMap2[0];
			ref uint d3 = ref DecodeMap3[0];

			for (int i = 0; i < chunks; ++i)
			{
				x = Unsafe.Add(ref d0, (int) (y & 0xff))
				  | Unsafe.Add(ref d1, (int) ((y >> 16) & 0xff))
				  | Unsafe.Add(ref d2, (int) ((y >> 32) & 0xff))
				  | Unsafe.Add(ref d3, (int) ((y >> 48) & 0xff));

				if (x >= BADCHAR) return -1;
				// note: we write 4 bytes instead of 3, but the last is 0 and we already made sure that we would not overflow
				Unsafe.WriteUnaligned<uint>(ref outp, x);
				outp = ref Unsafe.Add(ref outp, 3);

				// read next 4 bytes from input
				y = Unsafe.ReadUnaligned<ulong>(ref inp);
				inp = ref Unsafe.Add(ref inp, 8);
			}

			switch(leftOver)
			{
				case 0:
				{
					x = Unsafe.Add(ref d0, (int) (y & 0xff))
					  | Unsafe.Add(ref d1, (int) ((y >> 16) & 0xff))
					  | Unsafe.Add(ref d2, (int) ((y >> 32) & 0xff))
					  | Unsafe.Add(ref d3, (int) ((y >> 48) & 0xff));

					if (x >= BADCHAR) return -1;

					Unsafe.Add(ref outp, 0) = (byte) x;
					Unsafe.Add(ref outp, 1) = (byte) (x >> 8);
					Unsafe.Add(ref outp, 2) = (byte) (x >> 16);

					return (chunks + 1) * 3;
				}

				case 1:
				{ // 1 output byte
					if (padChar != '\0') return -1; //impossible avec du padding
					x = Unsafe.Add(ref d0, (int) (y & 0xFF));
					outp = (byte) x;
					break;
				}
				case 2:
				{ // 1 output byte
					x = Unsafe.Add(ref d0, (int) (y & 0xFF))
					  | Unsafe.Add(ref d1, (int) ((y >> 16) & 0xFF));
					outp = (byte) x;
					break;
				}
				default:
				{ // 2 output byte
					x = Unsafe.Add(ref d0, (int) (y & 0xFF))
					  | Unsafe.Add(ref d1, (int) ((y >> 16) & 0xFF))
					  | Unsafe.Add(ref d2, (int) ((y >> 32) & 0xFF));
					Unsafe.Add(ref outp, 0) = (byte) x;
					Unsafe.Add(ref outp, 1) = (byte) (x >> 8);
					break;
				}
			}

			if (x >= BADCHAR) return -1;
			return (3 * chunks) + ((6 * leftOver) / 8);

		}

		#endregion

		#region Data...

		private const uint BADCHAR = 0x01FFFFFF;

		private static readonly uint[] DecodeMap0 =
		[
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x000000f8, 0x01ffffff, 0x000000f8, 0x01ffffff, 0x000000fc,
			0x000000d0, 0x000000d4, 0x000000d8, 0x000000dc, 0x000000e0, 0x000000e4,
			0x000000e8, 0x000000ec, 0x000000f0, 0x000000f4, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x00000000,
			0x00000004, 0x00000008, 0x0000000c, 0x00000010, 0x00000014, 0x00000018,
			0x0000001c, 0x00000020, 0x00000024, 0x00000028, 0x0000002c, 0x00000030,
			0x00000034, 0x00000038, 0x0000003c, 0x00000040, 0x00000044, 0x00000048,
			0x0000004c, 0x00000050, 0x00000054, 0x00000058, 0x0000005c, 0x00000060,
			0x00000064, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x000000fc,
			0x01ffffff, 0x00000068, 0x0000006c, 0x00000070, 0x00000074, 0x00000078,
			0x0000007c, 0x00000080, 0x00000084, 0x00000088, 0x0000008c, 0x00000090,
			0x00000094, 0x00000098, 0x0000009c, 0x000000a0, 0x000000a4, 0x000000a8,
			0x000000ac, 0x000000b0, 0x000000b4, 0x000000b8, 0x000000bc, 0x000000c0,
			0x000000c4, 0x000000c8, 0x000000cc, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff
		];

		private static readonly uint[] DecodeMap1 =
		[
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x0000e003, 0x01ffffff, 0x0000e003, 0x01ffffff, 0x0000f003,
			0x00004003, 0x00005003, 0x00006003, 0x00007003, 0x00008003, 0x00009003,
			0x0000a003, 0x0000b003, 0x0000c003, 0x0000d003, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x00000000,
			0x00001000, 0x00002000, 0x00003000, 0x00004000, 0x00005000, 0x00006000,
			0x00007000, 0x00008000, 0x00009000, 0x0000a000, 0x0000b000, 0x0000c000,
			0x0000d000, 0x0000e000, 0x0000f000, 0x00000001, 0x00001001, 0x00002001,
			0x00003001, 0x00004001, 0x00005001, 0x00006001, 0x00007001, 0x00008001,
			0x00009001, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x0000f003,
			0x01ffffff, 0x0000a001, 0x0000b001, 0x0000c001, 0x0000d001, 0x0000e001,
			0x0000f001, 0x00000002, 0x00001002, 0x00002002, 0x00003002, 0x00004002,
			0x00005002, 0x00006002, 0x00007002, 0x00008002, 0x00009002, 0x0000a002,
			0x0000b002, 0x0000c002, 0x0000d002, 0x0000e002, 0x0000f002, 0x00000003,
			0x00001003, 0x00002003, 0x00003003, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff
		];

		private static readonly uint[] DecodeMap2 =
		[
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x00800f00, 0x01ffffff, 0x00800f00, 0x01ffffff, 0x00c00f00,
			0x00000d00, 0x00400d00, 0x00800d00, 0x00c00d00, 0x00000e00, 0x00400e00,
			0x00800e00, 0x00c00e00, 0x00000f00, 0x00400f00, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x00000000,
			0x00400000, 0x00800000, 0x00c00000, 0x00000100, 0x00400100, 0x00800100,
			0x00c00100, 0x00000200, 0x00400200, 0x00800200, 0x00c00200, 0x00000300,
			0x00400300, 0x00800300, 0x00c00300, 0x00000400, 0x00400400, 0x00800400,
			0x00c00400, 0x00000500, 0x00400500, 0x00800500, 0x00c00500, 0x00000600,
			0x00400600, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x00c00f00,
			0x01ffffff, 0x00800600, 0x00c00600, 0x00000700, 0x00400700, 0x00800700,
			0x00c00700, 0x00000800, 0x00400800, 0x00800800, 0x00c00800, 0x00000900,
			0x00400900, 0x00800900, 0x00c00900, 0x00000a00, 0x00400a00, 0x00800a00,
			0x00c00a00, 0x00000b00, 0x00400b00, 0x00800b00, 0x00c00b00, 0x00000c00,
			0x00400c00, 0x00800c00, 0x00c00c00, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff
		];

		private static readonly uint[] DecodeMap3 =
		[
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x003e0000, 0x01ffffff, 0x003e0000, 0x01ffffff, 0x003f0000,
			0x00340000, 0x00350000, 0x00360000, 0x00370000, 0x00380000, 0x00390000,
			0x003a0000, 0x003b0000, 0x003c0000, 0x003d0000, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x00000000,
			0x00010000, 0x00020000, 0x00030000, 0x00040000, 0x00050000, 0x00060000,
			0x00070000, 0x00080000, 0x00090000, 0x000a0000, 0x000b0000, 0x000c0000,
			0x000d0000, 0x000e0000, 0x000f0000, 0x00100000, 0x00110000, 0x00120000,
			0x00130000, 0x00140000, 0x00150000, 0x00160000, 0x00170000, 0x00180000,
			0x00190000, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x003f0000,
			0x01ffffff, 0x001a0000, 0x001b0000, 0x001c0000, 0x001d0000, 0x001e0000,
			0x001f0000, 0x00200000, 0x00210000, 0x00220000, 0x00230000, 0x00240000,
			0x00250000, 0x00260000, 0x00270000, 0x00280000, 0x00290000, 0x002a0000,
			0x002b0000, 0x002c0000, 0x002d0000, 0x002e0000, 0x002f0000, 0x00300000,
			0x00310000, 0x00320000, 0x00330000, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff
		];
		
		#endregion

	}

}
