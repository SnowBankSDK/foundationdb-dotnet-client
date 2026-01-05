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
	using System.Globalization;
	using System.Runtime.InteropServices;
	using System.Text;
	using SnowBank.Data.Json;
	using SnowBank.Buffers;
	using SnowBank.Buffers.Text;

	/// <summary>Provides methods for encoding and escaping JSON strings.</summary>
	public static partial class JsonEncoding
	{

		//note: the lookup table is in JsonEncoding.LookupTable.cs

		internal static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

		/// <summary>Checks if a character requires escaping before being written to a JSON document</summary>
		/// <param name="c">Character to inspect</param>
		/// <returns><see langword="false"/> if the character is valid, or <see langword="true"/> if it must be escaped</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool NeedsEscaping(char c) => EscapingLookupTable[c] != 0;

		/// <summary>Checks if a string requires escaping before being written to a JSON document</summary>
		/// <param name="s">Text to inspect</param>
		/// <returns><see langword="false"/> if all characters are valid, or <see langword="true"/> if at least one character must be escaped</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool NeedsEscaping(string? s)
		{
			return s != null && NeedsEscaping(s.AsSpan());
		}

		/// <summary>Checks if a string requires escaping before being written to a JSON document</summary>
		/// <param name="text">Text to inspect</param>
		/// <returns><see langword="false"/> if all characters are valid, or <see langword="true"/> if at least one character must be escaped</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool NeedsEscaping(ReadOnlySpan<char> text)
		{
			if (text.Length == 0) return false;

			// Notes on performance:
			// - We assume that 99.99+% of string will NOT require escaping, and so lookup[c] will (almost) always be false.
			// - If we use a bitwise OR (|), we only need one test/branch per batch of 4 characters, compared to a logical OR (||).
			//
			// Testing with BenchmarkDotNet and .NET 9 we found that:
			// - this approach equivalent to a naive "foreach(var c in s) { ... }", even for small strings
			// - unrolling two ulong (8 chars) vs one ulong (4 chars) only yield ~10% perf (probably due to 50% less loop check)
			// - testing with SSE3 LoadVector128 is slower, and using AVX2 "Gather" instructions to perform the lookup is also slower
			// - trying to use a "switch(length & 3) { case 1: ... case 1: ... case 2: ... }" is _slower_ then a simple "while(len-- > 0)"
			//
			// Other notes:
			// - "switch(s.Length)" with dedicated optimized code paths for arrays of length 1, 2, or 3 is SLOWER, probably due to the additional jump destination lookup table
			// - trying to remove array bound checks does not really yield any difference. My guess is that since the code never actually overflows, the cpu branch predictor "learns" that the check will never be taken and is "optimized away"
			// - "ref char ptr = ref s[0]" is a little bit faster than "fixed (char* ptr = s)", because it generates slightly less assembly code, but the difference is in the order of less than 1ns (but still measurable)

			ref char ptr = ref Unsafe.AsRef(in text[0]);
			ref int map = ref EscapingLookupTable[0];

			// read by 8
			int len = text.Length;
			if (len >= 8)
			{
				len >>= 3;
				while(len-- > 0)
				{
					ulong x = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<char, byte>(ref ptr));
					ulong y = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<char, byte>(ref Unsafe.Add(ref ptr, 4)));

					int sz = Unsafe.Add(ref map, (ushort) x) | Unsafe.Add(ref map, (ushort) y);
					x >>= 16; y >>= 16;
					sz |= Unsafe.Add(ref map, (ushort) x) | Unsafe.Add(ref map, (ushort) y);
					x >>= 16; y >>= 16;
					sz |= Unsafe.Add(ref map, (ushort) x) | Unsafe.Add(ref map, (ushort) y);
					x >>= 16; y >>= 16;
					sz |= Unsafe.Add(ref map, (ushort) x) | Unsafe.Add(ref map, (ushort) y);
			
					if (sz != 0)
					{
						return true;
					}

					ptr = ref Unsafe.Add(ref ptr, 8);
				}
				len = text.Length & 7;
			}

			// read 4
			if (len >= 4)
			{
				ulong x = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<char, byte>(ref ptr));
		
				int sz = Unsafe.Add(ref map, (ushort) x);
				x >>= 16;
				sz |= Unsafe.Add(ref map, (ushort) x);
				x >>= 16;
				sz |= Unsafe.Add(ref map, (ushort) x);
				x >>= 16;
				sz |= Unsafe.Add(ref map, (ushort) x);
		
				if (sz != 0)
				{
					return true;
				}

				ptr = ref Unsafe.Add(ref ptr, 4);
				len -= 4;
			}
			// read 0 to 3
			while(len-- > 0)
			{
				if ((Unsafe.Add(ref map, ptr)) != 0)
				{
					return true;
				}

				ptr = ref Unsafe.Add(ref ptr, 1);
			}

			return false;
		}

		/// <summary>Computes the required buffer capacity to encode the specified string</summary>
		/// <param name="text">Text to inspect</param>
		/// <param name="withQuotes">If <see langword="true"/> (default), include the two double quotes around the string (<c>"..."</c>)</param>
		/// <returns>
		/// <para> the length of <paramref name="text"/> if no escaping is required</para>
		/// <para> a greater value if at least one character needs to be escaped.</para>
		/// <para> <see langword="4"/> if <paramref name="text"/> is <see langword="null"/> (<c>"null"</c>).</para>
		/// </returns>
		public static int ComputeEscapedSize(string? text, bool withQuotes = true)
		{
			return text == null ? 4 : ComputeEscapedSize(text.AsSpan(), withQuotes);
		}

		/// <summary>Computes the required buffer capacity to encode the specified string</summary>
		/// <param name="text">Text to inspect</param>
		/// <param name="withQuotes">If <see langword="true"/> (default), include the two double quotes around the string (<c>"..."</c>)</param>
		/// <returns>
		/// <para> the length of <paramref name="text"/> if no escaping is required</para>
		/// <para> a greater value if at least one character needs to be escaped.</para>
		/// </returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int ComputeEscapedSize(ReadOnlySpan<char> text, bool withQuotes = true)
		{
			if (text.Length == 0)
			{
				return withQuotes ? 2 : 0;
			}

			long sz = text.Length;
			if (withQuotes)
			{
				sz += 2;
			}

			ref char ptr = ref Unsafe.AsRef(in text[0]);
			ref int map = ref EscapingLookupTable[0];

			// read by 8
			int len = text.Length;

			if (len >= 8)
			{
				len >>= 3;
				while(len-- > 0)
				{
					ulong x = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<char, byte>(ref ptr));
					ulong y = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<char, byte>(ref Unsafe.Add(ref ptr, 4)));

					sz += Unsafe.Add(ref map, (ushort) x) + Unsafe.Add(ref map, (ushort) y);
					x >>= 16; y >>= 16;
					sz += Unsafe.Add(ref map, (ushort) x) + Unsafe.Add(ref map, (ushort) y);
					x >>= 16; y >>= 16;
					sz += Unsafe.Add(ref map, (ushort) x) + Unsafe.Add(ref map, (ushort) y);
					x >>= 16; y >>= 16;
					sz += Unsafe.Add(ref map, (ushort) x) + Unsafe.Add(ref map, (ushort) y);
			
					ptr = ref Unsafe.Add(ref ptr, 8);
				}
				len = text.Length & 7;
			}

			// read 4
			if (len >= 4)
			{
				ulong x = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<char, byte>(ref ptr));
		
				sz += Unsafe.Add(ref map, (ushort) x);
				x >>= 16;
				sz += Unsafe.Add(ref map, (ushort) x);
				x >>= 16;
				sz += Unsafe.Add(ref map, (ushort) x);
				x >>= 16;
				sz += Unsafe.Add(ref map, (ushort) x);
		
				ptr = ref Unsafe.Add(ref ptr, 4);
				len -= 4;
			}
			// read 0 to 3
			while(len-- > 0)
			{
				sz += Unsafe.Add(ref map, ptr);
				ptr = ref Unsafe.Add(ref ptr, 1);
			}

			// force an overflow exception if this is more than 2GiB !
			// => the caller probably does not have any way to handle this, so it is safer to "blow up" here!
			return checked((int) sz);
		}

		/// <summary>Finds the position in the string of the first character that would need to be escaped in a valid JSON string</summary>
		/// <param name="s">Text to inspect</param>
		/// <returns>Index of the first invalid character, or <see langword="-1"/> if all the characters are valid</returns>
		public static int IndexOfFirstInvalidChar(ReadOnlySpan<char> s)
		{
			ref char start = ref Unsafe.AsRef(in s[0]);
			ref int map = ref EscapingLookupTable[0];
			ref char ptr = ref start;
			{
				// read by 8
				int len = s.Length;
				if (len >= 8)
				{
					len >>= 3;
					while(len-- > 0)
					{
						ulong x = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<char, byte>(ref ptr));
						ulong y = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<char, byte>(ref Unsafe.Add(ref ptr, 4)));
						
						int sz = Unsafe.Add(ref map, (ushort) x) | Unsafe.Add(ref map, (ushort) y);
						x >>= 16; y >>= 16;
						sz |= Unsafe.Add(ref map, (ushort) x) | Unsafe.Add(ref map, (ushort) y);
						x >>= 16; y >>= 16;
						sz |= Unsafe.Add(ref map, (ushort) x) | Unsafe.Add(ref map, (ushort) y);
						x >>= 16; y >>= 16;
						sz |= Unsafe.Add(ref map, (ushort) x) | Unsafe.Add(ref map, (ushort) y);
						
						if (sz != 0) goto found_required_escaping;
						
						ptr = ref Unsafe.Add(ref ptr, 8);
					}
					len = s.Length & 7;
				}
				
				// read 4
				if (len >= 4)
				{
					ulong x = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<char, byte>(ref ptr));
					
					int sz = Unsafe.Add(ref map, (ushort) x);
					x >>= 16;
					sz |= Unsafe.Add(ref map, (ushort) x);
					x >>= 16;
					sz |= Unsafe.Add(ref map, (ushort) x);
					x >>= 16;
					sz |= Unsafe.Add(ref map, (ushort) x);
					
					if (sz != 0) goto found_required_escaping;
					
					ptr = ref Unsafe.Add(ref ptr, 4);
					len -= 4;
				}
				
			check_tail:
			
				// check up to 3 chars
				while(len-- > 0)
				{
					if ((Unsafe.Add(ref map, ptr)) != 0)
					{
#if NET8_0_OR_GREATER
						return (int) (Unsafe.ByteOffset(ref start, ref ptr) >> 1);
#else
						return ((int) Unsafe.ByteOffset(ref start, ref ptr)) >> 1;
#endif
					}
					ptr = ref Unsafe.Add(ref ptr, 1);
				}
				
				return -1;
				
			found_required_escaping:
			
				// we are close to it, we still need to find the exact spot
				// set the len to the actual number of characters left, and start iterating from there
#if NET8_0_OR_GREATER
				len = s.Length - (int) (Unsafe.ByteOffset(ref start, ref ptr) >> 1);
#else
				len = s.Length - ((int) Unsafe.ByteOffset(ref start, ref ptr) >> 1);
#endif
				goto check_tail;
			}
		}

		/// <summary>Encodes the specified text into the provided text buffer.</summary>
		/// <param name="destination">The writer to which the encoded text will be written.</param>
		/// <param name="text">The text to encode.</param>
		/// <remarks>The double quotes (<c>"...."</c>) are automatically added, unless the string is <c>null</c> in which case the literal <c>'null'</c> literal is used</remarks>
		public static void EncodeTo(ref ValueStringWriter destination, string? text)
		{
			if (text == null)
			{
				destination.Write("null");
				return;
			}

			EncodeTo(ref destination, text.AsSpan());
		}

		/// <summary>Encodes the specified text into the provided text buffer.</summary>
		/// <param name="destination">The writer to which the encoded text will be written.</param>
		/// <param name="text">The text to encode.</param>
		/// <remarks>The double quotes (<c>"...."</c>) are automatically added, unless the string is <c>null</c> in which case the literal <c>'null'</c> is used.</remarks>
		public static void EncodeTo(ref ValueStringWriter destination, ReadOnlySpan<char> text)
		{
			if (text.Length == 0)
			{ // empty string
				destination.Write("\"\"");
				return;
			}
		
			// look for the first invalid character
			// - either they are all valid, then we simply copy everything
			// - or we can at least copy up to this position, and then start converting with a slow loop

			int first = IndexOfFirstInvalidChar(text);
			if (first < 0)
			{ // the string is clean, no need for escaping

				destination.Write('"', text, '"');
				return;
			}

			// we need to compute the size required to escape the rest
			int required = ComputeEscapedSize(text[first..], withQuotes: false);

			// and allocate a buffer for the whole string: double-quote + [first] + escape([tail]) + double-quote
			var span = destination.Allocate(checked(2 + first + required));

			// open the string
			span[0] = '"';
			span = span[1..];

			if (first > 0)
			{ // copy the first clean portion of the string
				text[..first].CopyTo(span);
				text = text[first..];
				span = span[first..];
			}

			// encode the rest
			if (!TryEncodeToSlow(span, text, out int written)
			    || written != required)
			{ // this is NOT supposed to happen! this means that ComputeEscapedSize does not agree with TryEncodeToSlow !
				throw new InvalidOperationException("Internal formatting error");
			}

			// close the string
			span[written] = '"';
		}

		/// <summary>Attempts to encode the specified text into the provided output span, if it is large enough.</summary>
		/// <param name="text">The text to encode.</param>
		/// <param name="destination">The span to which the encoded text will be written.</param>
		/// <param name="charsWritten">Outputs the number of characters written to the span, including any double quotes.</param>
		/// <param name="withQuotes">If <see langword="true"/> (default), includes double quotes around the string (<c>"..."</c>).</param>
		/// <returns><see langword="true"/> if the buffer was large enough; otherwise, <see langword="false"/>.</returns>
		/// <remarks>The buffer may contain partially serialized data after this method returns.</remarks>
		public static bool TryEncodeTo(ReadOnlySpan<char> text, Span<char> destination, out int charsWritten, bool withQuotes = true)
		{
			// in all cases, we need space for two double quotes!
			if (withQuotes && destination.Length < 2)
			{
				goto too_small;
			}

			if (text.Length == 0)
			{ // empty string
				if (withQuotes)
				{
					destination[0] = '"';
					destination[1] = '"';
					charsWritten = 2;
				}
				else
				{
					charsWritten = 0;
				}
				return true;
			}

			// look for the first invalid character
			// - either they are all valid, then we simply copy everything
			// - or we can at least copy up to this position, and then start converting with a slow loop

			int first = IndexOfFirstInvalidChar(text);
			if (first < 0)
			{ // the string is clean, no need for escaping

				if (withQuotes)
				{
					if ((uint) destination.Length < (uint) text.Length + 2)
					{
						goto too_small;
					}
					destination[0] = '"';
					text.CopyTo(destination[1..]);
					destination[text.Length + 1] = '"';
					charsWritten = text.Length + 2;
					return true;
				}

				return text.TryCopyTo(destination, out charsWritten);
			}

			// open double quotes
			if (withQuotes)
			{
				destination[0] = '"';
				destination = destination[1..];
			}

			if (first > 0)
			{ // copy the first clean portion of the string

				if (destination.Length < first)
				{
					goto too_small;
				}

				text[..first].CopyTo(destination);
				text = text[first..];
				destination = destination[first..];
			}

			if (!TryEncodeToSlow(destination, text, out int extra)
			    || extra >= destination.Length)
			{
				goto too_small;
			}

			if (withQuotes)
			{ // close double quotes
				destination[extra] = '"';
				extra += 2; // need to include the first double-quote from above!
			}

			charsWritten = first + extra;
			return true;

		too_small:
			charsWritten = 0;
			return false;
		}

		private static bool TryEncodeToSlow(Span<char> output, ReadOnlySpan<char> s, out int charsWritten)
		{
			// we always start on the first non-clean character of the string
			
			ref int map = ref EscapingLookupTable[0];
			ref char start = ref output[0];
			ref char ptr = ref start;
			int remaining = output.Length;
			
			foreach(var c in s)
			{
				switch(Unsafe.Add(ref map, c))
				{
					case 0:
					{
						if (remaining < 1)
						{
							goto too_small;
						}
						ptr = c;
						ptr = ref Unsafe.Add(ref ptr, 1);
						--remaining;
						break;
					}
					case 1:
					{
						if (remaining < 2)
						{
							goto too_small;
						}
						ptr = '\\';
						
						if (c is '\\' or '"')
							Unsafe.Add(ref ptr, 1) = c;
						else
							Unsafe.Add(ref ptr, 1) = c switch
							{
								'\n' => 'n',
								'\r' => 'r',
								'\t' => 't',
								'\b' => 'b',
								_ => 'f',
							};
						ptr = ref Unsafe.Add(ref ptr, 2);
						remaining -= 2;
						break;
					}
					default:
					{
						if (remaining < 6)
						{
							goto too_small;
						}

						Unsafe.WriteUnaligned<uint>(ref Unsafe.As<char, byte>(ref ptr), 0x0075005C); // '\u' => 5C 00 75 00
						
						ptr = ref Unsafe.Add(ref ptr, 2);

						int b1, b2;
						// most ASCII chars are < 256
						if (c <= 0xFF)
						{
							Unsafe.WriteUnaligned<uint>(ref Unsafe.As<char, byte>(ref ptr), 0x00300030); // "00" => 30 00 30 00
						}
						else
						{
							b1 = (c >> 12) & 0xF;
							b2 = (c >> 8) & 0xF;
							b1 += (b1 < 10 ? 48 : 87);
							b2 += (b2 < 10 ? 48 : 87);
							Unsafe.WriteUnaligned<int>(ref Unsafe.As<char, byte>(ref ptr), b2 << 16 | b1);
						}
						
						ptr = ref Unsafe.Add(ref ptr, 2);
						
						b1 = (c >> 4) & 0xF;
						b2 = c & 0xF;
						b1 += (b1 < 10 ? 48 : 87);
						b2 += (b2 < 10 ? 48 : 87);
						Unsafe.WriteUnaligned<int>(ref Unsafe.As<char, byte>(ref ptr), b2 << 16 | b1);
				
						ptr = ref Unsafe.Add(ref ptr, 2);
						remaining -= 6;
						break;
					}
				}	
			}
			charsWritten = output.Length - remaining;
			return true;
			
		too_small:
			charsWritten = 0;
			return false;
		}

		/// <summary>Encodes the specified text into the provided byte buffer.</summary>
		/// <param name="destination">The writer to which the encoded text will be written as UTF-8 bytes.</param>
		/// <param name="text">The text to encode.</param>
		/// <param name="withQuotes">Determines whether the encoded text should be enclosed in double quotes.</param>
		/// <remarks>The double quotes (<c>"...."</c>) are automatically added, unless the string is <c>null</c> in which case the literal <c>'null'</c> is used.</remarks>
		public static void EncodeTo(ref SliceWriter destination, ReadOnlySpan<char> text, bool withQuotes = true)
		{
			// it is somewhat possible to get an estimate of the escaped _byte size_ from the number of escaped chars
			// - any "non-ASCII" character will either be converted to UTF-8 bytes (up to 3 bytes), OR will be escaped as \uXXXX (6 bytes)
			// - any escaped character will always be ASCII, so it will always take 1 byte
			// So, if we computed en UTF-8 encoded size of the original string, and the escaped string length minus the original string length,
			// we should always have an upper bound for the required size

			var nonEscapedByteCount = Utf8NoBom.GetByteCount(text);
			var escapedCharsCount = ComputeEscapedSize(text, withQuotes);
			var escapedByteUpperBound = checked(nonEscapedByteCount + (escapedCharsCount - text.Length));

			var span = destination.GetSpan(escapedByteUpperBound);
			if (!TryEncodeTo(text, span, out int bytesWritten, withQuotes))
			{
				// this is NOT supposed to happen, unless ComputeEscapedSize failed for some reason?
				throw new InvalidOperationException();
			}
			destination.Advance(bytesWritten);
		}

		/// <summary>Encodes the specified text into the provided byte buffer.</summary>
		/// <param name="destination">The writer to which the encoded text will be written as UTF-8 bytes.</param>
		/// <param name="text">The text to encode.</param>
		/// <param name="withQuotes">Specifies whether the encoded text should be enclosed in double quotes. Defaults to <c>true</c>.</param>
		/// <remarks>The double quotes (<c>"...."</c>) are automatically added, unless the string is <c>null</c> in which case the literal <c>'null'</c> is used.</remarks>
		public static void EncodeTo(ref ValueBuffer<byte> destination, ReadOnlySpan<char> text, bool withQuotes = true)
		{
			var span = destination.GetSpan(ComputeEscapedSize(text, withQuotes));
			if (!TryEncodeTo(text, span, out int bytesWritten, withQuotes))
			{
				// this is NOT supposed to happen, unless ComputeEscapedSize failed for some reason?
				throw new InvalidOperationException();
			}
			destination.Advance(bytesWritten);
		}

		/// <summary>Attempts to encode the specified text into the provided span as UTF-8 bytes.</summary>
		/// <param name="text">The text to encode.</param>
		/// <param name="destination">The span of bytes where the encoded JSON will be written as UTF-8 bytes.</param>
		/// <param name="bytesWritten">When this method returns, contains the number of bytes written to the destination span.</param>
		/// <param name="withQuotes">If set to <see langword="true"/>, the encoded JSON will include surrounding quotes.</param>
		/// <returns><see langword="true"/> if the encoding was successful and the entire encoded JSON fits within the destination span; otherwise, <see langword="false"/>.</returns>
		/// <remarks>
		/// <para>This method encodes the input text as JSON, handling necessary escaping of characters.</para>
		/// <para>If <paramref name="withQuotes"/> is <see langword="true"/>, the encoded JSON will be surrounded by double quotes.</para>
		/// <para>If the destination span is too small to hold the encoded JSON, the method returns <see langword="false"/> and <paramref name="bytesWritten"/> is set to <see langword="0"/>.</para>
		/// </remarks>
		public static bool TryEncodeTo(ReadOnlySpan<char> text, Span<byte> destination, out int bytesWritten, bool withQuotes = true)
		{
			// in all cases, we need space for two double quotes!
			if (withQuotes && destination.Length < 2)
			{
				goto too_small;
			}

			if (text.Length == 0)
			{ // empty string
				if (withQuotes)
				{
					destination[0] = (byte) '"';
					destination[1] = (byte) '"';
					bytesWritten = 2;
				}
				else
				{
					bytesWritten = 0;
				}
				return true;
			}

			// look for the first invalid character
			// - either they are all valid, then we simply copy everything
			// - or we can at least copy up to this position, and then start converting with a slow loop

			int first = IndexOfFirstInvalidChar(text);
			if (first < 0)
			{ // the string is clean, no need for escaping

				int bytesNeeded = Utf8NoBom.GetByteCount(text);
				if (withQuotes)
				{
					if (destination.Length < checked(bytesNeeded + 2))
					{
						goto too_small;
					}
					destination[0] = (byte) '"';
					bytesNeeded = Utf8NoBom.GetBytes(text, destination[1..]);
					destination[bytesNeeded + 1] = (byte) '"';
					bytesWritten = bytesNeeded + 2;
					return true;
				}

				if (destination.Length < bytesNeeded)
				{
					goto too_small;
				}
				bytesNeeded = Utf8NoBom.GetBytes(text, destination);
				bytesWritten = bytesNeeded;
				return true;
			}

			// open double quotes
			if (withQuotes)
			{
				destination[0] = (byte) '"';
				destination = destination[1..];
			}

			if (first > 0)
			{ // copy the first clean portion of the string

				int bytesNeeded = Utf8NoBom.GetByteCount(text[..first]);
				if (destination.Length < bytesNeeded)
				{
					goto too_small;
				}

				bytesNeeded = Utf8NoBom.GetBytes(text[..first], destination);
				text = text[first..];
				destination = destination[bytesNeeded..];
			}

			if (!TryEncodeToSlow(destination, text, out int extra)
				|| extra >= destination.Length)
			{
				goto too_small;
			}

			if (withQuotes)
			{ // close double quotes
				destination[extra] = (byte) '"';
				bytesWritten = first + extra + 2;
				return true;
			}
			else
			{
				bytesWritten = first + extra;
				return true;
			}

		too_small:
			bytesWritten = 0;
			return false;
		}

		private static bool TryEncodeToSlow(Span<byte> output, ReadOnlySpan<char> s, out int bytesWritten)
		{
			ref int map = ref EscapingLookupTable[0];
			ref byte start = ref output[0];
			ref byte ptr = ref start;
			int remaining = output.Length;

			foreach (var c in s)
			{
				switch (Unsafe.Add(ref map, c))
				{
					case 0:
					{
						if (c <= 0x7F)
						{
							if (remaining < 1)
							{
								goto too_small;
							}

							ptr = (byte) c;
							ptr = ref Unsafe.Add(ref ptr, 1);
							--remaining;
						}
						else
						{
							if (!Utf8Encoder.TryWriteCodePoint(MemoryMarshal.CreateSpan(ref ptr, remaining), c, out int n))
							{
								goto too_small;
							}
							ptr = ref Unsafe.Add(ref ptr, n);
							remaining -= n;
						}

						break;
					}
					case 1:
					{
						if (remaining < 2)
						{
							goto too_small;
						}
						ptr = (byte) '\\';

						if (c is '\\' or '"')
							Unsafe.Add(ref ptr, 1) = (byte) c;
						else
							Unsafe.Add(ref ptr, 1) = c switch
							{
								'\n' => (byte) 'n',
								'\r' => (byte) 'r',
								'\t' => (byte) 't',
								'\b' => (byte) 'b',
								_ => (byte) 'f',
							};
						ptr = ref Unsafe.Add(ref ptr, 2);
						remaining -= 2;
						break;
					}
					default:
					{
						if (remaining < 6)
						{
							goto too_small;
						}

						Unsafe.WriteUnaligned<ushort>(ref ptr, 0x755C); // '\u' => 5C 75

						ptr = ref Unsafe.Add(ref ptr, 2);

						int b1, b2;
						// most ASCII chars are < 256
						if (c <= 0xFF)
						{
							Unsafe.WriteUnaligned<ushort>(ref ptr, 0x3030); // "00" => 30 30
						}
						else
						{
							b1 = (c >> 12) & 0xF;
							b2 = (c >> 8) & 0xF;
							b1 += (b1 < 10 ? 48 : 87);
							b2 += (b2 < 10 ? 48 : 87);
							Unsafe.WriteUnaligned<ushort>(ref ptr, (ushort) (b2 << 8 | b1));
						}

						ptr = ref Unsafe.Add(ref ptr, 2);

						b1 = (c >> 4) & 0xF;
						b2 = c & 0xF;
						b1 += (b1 < 10 ? 48 : 87);
						b2 += (b2 < 10 ? 48 : 87);
						Unsafe.WriteUnaligned<ushort>(ref ptr, (ushort) (b2 << 8 | b1));

						ptr = ref Unsafe.Add(ref ptr, 2);
						remaining -= 6;
						break;
					}
				}
			}
			bytesWritten = output.Length - remaining;
			return true;

		too_small:
			bytesWritten = 0;
			return false;
		}

		/// <summary>Encodes a string literal into a valid JSON string literal</summary>
		/// <param name="text">Text to encode.</param>
		/// <returns>String with the correct escaping and surrounded by double-quotes (<c>"..."</c>), or <c>"null"</c> if <paramref name="text"/> is <c>null</c>.</returns>
		/// <example><code>EncodeJsonString("foo") => "\"foo\""</code></example>
		public static string Encode(string? text)
		{
			// handle quickly the easy cases
			if (text == null)
			{ // => null
				return "null";
			}
			if (text.Length == 0)
			{ // => ""
				return "\"\"";
			}
			
			// first check if we actually need to encode anything
			if (NeedsEscaping(text))
			{ // yes => slow path
				return EncodeSlow(text);
			}

			// nothing to do, except add the double quotes
			return string.Concat("\"", text, "\"");
		}

		internal static string EncodeSlow(string text)
		{
			// note: we assume that the typical overhead of escaping characters will be up to 6 characters if there is only one or two "invalid" characters
			// this assumption totally breaks down for non-latin languages!
			var sb = StringBuilderCache.Acquire(checked(text.Length + 2 + 6));
			return StringBuilderCache.GetStringAndRelease(AppendSlow(sb, text, true));
		}

		/// <summary>Encodes a string literal into a valid JSON string literal</summary>
		/// <param name="text">Text to encode.</param>
		/// <returns>String with the correct escaping and surrounded by double-quotes (<c>"..."</c>), or <c>"null"</c> if <paramref name="text"/> is <c>null</c>.</returns>
		/// <example><code>EncodeJsonString("foo") => "\"foo\""</code></example>
		public static string Encode(ReadOnlySpan<char> text)
		{
			// handle quickly the easy cases
			if (text.Length == 0)
			{ // => ""
				return "\"\"";
			}
			
			// first check if we actually need to encode anything
			if (NeedsEscaping(text))
			{ // yes => slow path
				return EncodeSlow(text);
			}

			// nothing to do, except add the double quotes
			return string.Concat("\"", text, "\"");
		}

		internal static string EncodeSlow(ReadOnlySpan<char> text)
		{
			// note: we assume that the typical overhead of escaping characters will be up to 6 characters if there is only one or two "invalid" characters
			// this assumption totally breaks down for non-latin languages!
			var sb = StringBuilderCache.Acquire(checked(text.Length + 2 + 6));
			return StringBuilderCache.GetStringAndRelease(AppendSlow(sb, text, true));
		}

		/// <summary>Encodes a string literal that must be written to a JSON document (slow path)</summary>
		internal static StringBuilder AppendSlow(StringBuilder sb, string? text, bool includeQuotes)
		{
			if (text == null)
			{ // bypass
				return sb.Append("null");
			}
			return AppendSlow(sb, text.AsSpan(), includeQuotes);
		}

		/// <summary>Encodes a string literal that must be written to a JSON document (slow path)</summary>
		internal static unsafe StringBuilder AppendSlow(StringBuilder sb, ReadOnlySpan<char> text, bool includeQuotes)
		{
			// We check and encode in a single pass:
			// - we have a cursor on the last changed character (initially set to 0)
			// - as long as we see valid characters, we advance the cursor
			// - if we find a character that needs to be escaped (or reach the end of the string):
			//   - we copy the clean text from the previous cursor to the current position,
			//   - we encode the current character,
			//   - we advance the cursor to the next character
			//
			// A string that did not require any replacement will end up with the cursor still set to 0
			//
			// note: we do not encode the forward slash ('/'), to help distinguish with it, and '\/' that is frequently used to encode dates.

			if (includeQuotes) sb.Append('"');
			int i = 0, last = 0;
			int n = text.Length;
			fixed (char* str = text)
			{
				char* ptr = str;
				while (n-- > 0)
				{
					char c = *ptr++;
					if (c <= '/')
					{ // ASCII 0..47
						if (c == '"')
						{ // " -> \"
							goto escape_backslash;
						}
						if (c >= ' ')
						{ // ASCII 32..47 : from space to '/'
							goto next; // => not modified
						}
						// ASCII 0..31 : encoded
						// - we directly escape any of \n, \r, \t, \b and \f
						// - all others will be escaped as Unicode: \uXXXX
						switch (c)
						{
							case '\n': c = 'n'; goto escape_backslash;
							case '\r': c = 'r'; goto escape_backslash;
							case '\t': c = 't'; goto escape_backslash;
							case '\b': c = 'b'; goto escape_backslash;
							case '\f': c = 'f'; goto escape_backslash;
						}
						// encode as \uXXXX
						goto escape_unicode;
					}
					if (c == '\\')
					{ // \ -> \\
						goto escape_backslash;
					}
					if (c >= 0xD800 && (c < 0xE000 || c >= 0xFFFE))
					{ // warning, the Unicode range D800 - DFFF is used to escape non-BMP characters (> 0x10000), and FFFE/FFFF corresponds to BOM UTF-16 (LE/BE)
						goto escape_unicode;
					}
					// => skip
					goto next;

					// character encoded with a single backslash => \c
				escape_backslash:
					if (i > last) sb.Append(text.Slice(last, i - last));
					last = i + 1;
					sb.Append('\\').Append(c);
					goto next;

					// character encoded as Unicode using 16 bits
				escape_unicode:
					if (i > last) sb.Append(text.Slice(last, i - last));
					last = i + 1;
					sb.Append(@"\u").Append(((int) c).ToString("x4", NumberFormatInfo.InvariantInfo)); //TODO: PERF: optimize this!

				next:
					// no encoding required.
					++i;

				} // while
			} // fixed

			if (last == 0)
			{ // the text did not require any escaping
				sb.Append(text);
			}
			else if (last < text.Length)
			{ // append the tail that did not need any escaping
				sb.Append(text.Slice(last, text.Length - last));
			}
			return includeQuotes ? sb.Append('"') : sb;
		}

		/// <summary>Encodes a string literal into a JSON string, and appends the result to a <see cref="StringBuilder"/>.</summary>
		/// <param name="sb">The target <see cref="StringBuilder"/> to which the encoded string will be appended.</param>
		/// <param name="text">The string literal to encode.</param>
		/// <returns>The same <see cref="StringBuilder"/> instance with the encoded string appended.</returns>
		/// <remarks>
		/// <para>If <paramref name="text"/> is <see langword="null"/>, the method appends the <c>null</c> literal.</para>
		/// <para>If <paramref name="text"/> is an empty string, the method appends an empty JSON string (<c>""</c>).</para>
		/// <para>If <paramref name="text"/> contains characters that need escaping, the method encodes the string appropriately.</para>
		/// </remarks>
		public static StringBuilder Append(StringBuilder sb, string? text)
		{
			if (text == null)
			{ // null -> "null"
				return sb.Append("null");
			}
			if (text.Length == 0)
			{ // empty -> "\"\""
				return sb.Append("\"\"");
			}
			if (!JsonEncoding.NeedsEscaping(text))
			{ // no encoding required
				return sb.Append('"').Append(text).Append('"');
			}
			// encoding required
			return AppendSlow(sb, text, true);
		}

	}

}
