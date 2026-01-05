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

namespace FoundationDB.Layers.Experimental.Indexing
{
	using System.Text;

	public static class WordAlignHybridEncoder
	{

		// Encodes 31-bit words from the input into 32-bit words to the output

		#region Format...

		// RULES:
		//
		// - Compressed bitmaps are a stream 32-bit words and are always a multiple of 4 bytes in size.
		//   - Word at index K (0-based) starts at offset K * 4 in the buffer
		//	 - The last word written must have at least one bit set, so by definition a bitmap cannot end up with a run of all-0 words.
		//     > this means that the empty bitmap has no data word - only a header - and is 4-bytes long
		//   - The source index of the last word is stored in the header, so that appending to a bitmap can be done efficiently.
		//     > this means that a bitmap with a single bit set will have a single literal word and be 8-bytes long.
		//	 - Words are encoded in little-endian for convenience (no need to swap on INTEL platforms), with LSB being bit 0 in the source, and MSB being bit 31 in the source 
		//	   - Word Index  ║                       0                       ║                       1                       ║ ...
		//     - Byte Offset ║     0     |     1     |     2     |     3     ║     4     |     5     |     6     |     7     ║ ...
		//	   - Source Bit  ║  7 ... 0  | 15 ... 8  | 23 ... 16 | 31 ... 24 ║  7 ... 0  | 15 ... 8  | 23 ... 16 | 31 ... 24 ║ ...
		//
		// WIRE FORMAT:
		//
		// - The empty bitmap (no bit set) is the empty slice (0 bytes, no header, no data words).
		// - A non-empty bitmap (at least one bit set) is a slice of at least 8 bytes (a header and one data word)
		// - The first 4 bytes are the HEADER, and following groups of 4 bytes are the DATA words.
		// - Word 0 contains the HEADER which encodes the offset of the HIGHEST_SET_BIT set in the bitmap
		//   > If all bits (0..31) are set (1), the bounds are unknown, which means that the bitmap is probably not yet complete (or is streamed).
		//   > If all bits (0..31) are unset (0), the bitmap is empty and should not have any data words.
		//   > By definition, this bit is in the last word (which will either be a LITERAL, or a FILLER with fill bit 1.
		//	 > The number of Data Words in the bitmap should be equal to "(HIGHEST_SET_BIT + 30) DIV 31"
		//   > The offset of the highest bit in the last word will be "HIGHEST_SET_BIT % 31"
		//   > The number of padding bits (0) inserted in the last word will then be "30 - (HIGHEST_BIT % 31)"
		// - Data words with MSB unset (0) are LITERAL words that contain 31 consecutive bits from the uncompressed bitmap with at least one bit set (a LITERAL cannot be equal to 0 or 0x7FFFFFFF)
		// - Data words with MSB set (1) are FILLER words that represent a run of N words with all bits either set (1) or unset(0).
		//	 > A compressed bitmap cannot end by a FILLER word with a fill bit of 0, but CAN start by one or more FILLER words with a fill bit of 0.
		//   > The index of the LOWEST word with at least a bit set is easy to determine by looking at the first consecutive FILLER words with fill bit 0
		//   > Optionally by counting the first bits of the following LITERAL can also help compute the LOWEST_SET_BIT index.
		//
		// Note: Bits below are ordered from 31 (right) to 0 (left) for convenience, but they are still written in little-endian in the byte buffer.
		//
		//     Bytes ║        3        |        2        |        1        |        0        ║
		//           ║ 3 3 2 2 2 2 2 2 | 2 2 2 2 1 1 1 1 | 1 1 1 1 1 1 0 0 | 0 0 0 0 0 0 0 0 ║
		//      Bits ║ 1 0 9 8 7 6 5 4 | 3 2 1 0 9 8 7 6 | 5 4 3 2 1 0 9 8 | 7 6 5 4 3 2 1 0 ║
		//   --------║-----------------|-----------------|-----------------|-----------------║
		//   HEADER  ║ H H H H H H H H | H H H H H H H H | H H H H H H H H | H H H H H H H H ║ H = offset of the highest bit that is set in the bitmap. 
		//   LITERAL ║ 0 X X X X X X X | X X X X X X X X | X X X X X X X X | X X X X X X X X ║ X = bits of uncompressed data
		//   FILLER  ║ 1 F N N N N N N | N N N N N N N N | N N N N N N N N | N N N N N N N N ║ F = 1 or 0; N = number of 31-bit words with all bits equals to F
		//
		// SIZE AND SPLITTING:
		//
		// - A compressed bitmap can only encode a set bit up to offset 2^31 - 1, which means that it cannot encode more than 2,147,483,648 documents
		// - Since .NET use signed integers for array indexers anyway, this fits exactly between 0 and int.MaxValue
		// - In the worst case scenario (only LITERAL words), this would need 69,273,667 LITERAL words plus a HEADER word, for a total of 277,094,672 bytes (~264,25 MB).
		// - A value stored in FoundationDB has a max size of 100,000 bytes which means that we can only store a mini of 24,999 literals (worst case scenario), or 774,969 uncompressed bits (if splitting must be decided BEFORE compressing when final size is unknown).
		// - The LCM between 31 and 8 is 248, which means that you should split compressed bitmaps in 248 bit chunks to end up with no padding between chunks (31 bytes in the source makes exactly 8 x 31-bit words)
		// - There does not seem to be any power of two that is also divisible by 31, so it is not possible to split bitmaps by using bit masking or shifting
		// - There does not seem to be power of 10 that is also divisible by 31, so it is not possible to split bitmaps by nice chunks like 10,000 or 100,000
		// - Targetting a maximum worst case size of 64KB for a compressed bitmap gives a max chunk of 507,873 bits.

		internal const uint TYPE_MASK = 0x80000000;
		internal const int TYPE_SHIFT = 31;
		internal const uint BIT_TYPE_LITERAL = 0;
		internal const uint BIT_TYPE_FILL = 0x80000000;
		internal const uint LITERAL_MASK = 0x7FFFFFFF;
		internal const uint FILL_MASK = 0x40000000;
		internal const int FILL_SHIFT = 30;
		internal const uint BIT_FILL_ZERO = 0;
		internal const uint BIT_FILL_ONE = 0x40000000;
		internal const uint LENGTH_MASK = 0x3FFFFFFF;

		#endregion

		/// <summary>Helper class to read 31-bit words from an uncompressed source</summary>
		private sealed unsafe class UncompressedWordReader
		{
			/// <summary>Value returned by <see cref="Read"/> or <see cref="Peek"/> when the input have less than 31 bits remaining</summary>
			public const uint NotEnough = 0xFFFFFFFF;

			private byte* m_buffer;
			private int m_remaining;
			private ulong m_register;
			private int m_bits;

			public UncompressedWordReader(byte* buffer, int count)
			{
				//TODO: bound check
				m_buffer = buffer;
				m_remaining = count;
			}

			private bool FillRegister(ref ulong register, ref int bits)
			{
				Contract.Debug.Requires((uint) bits < 31, "Bad bits " + bits);

				int remaining = m_remaining;
				if (remaining == 0)
				{ // No more data available
					return false;
				}

				//note: difficult to optimize here since we need to read BigEndian

				int freeBits = 64 - bits;
				byte* ptr = m_buffer;
				while (freeBits >= 8 && remaining > 0)
				{
					freeBits -= 8;
					register |= (ulong)(*ptr++) << freeBits;
					--remaining;
				}
				Paranoid.Assert(remaining >= 0);
				bits = 64 - freeBits;
				Paranoid.Assert((uint) bits <= 64, $"bits = {bits} free_bits = {freeBits}");

				m_buffer = ptr;
				m_remaining = remaining;
				return true;
			}

			/// <summary>Peek a full 31-bit word from the input</summary>
			/// <returns>Value of the next word, or <see cref="NotEnough"/> if there are not enough bits remaining</returns>
			public uint Peek()
			{
				Contract.Debug.Requires((uint) m_bits <= 64);

				int bits = m_bits;
				ulong register = m_register;

				if (bits == 31)
				{ // short curt
					return (uint)(register >> (64 - 31));
				}

				if (bits < 31)
				{ // not enough bits, need to fill
					if (!FillRegister(ref register, ref bits) || bits < 31)
					{ // not enough remaining
						return NotEnough;
					}
					Contract.Debug.Assert(bits is > 0 and <= 64);

					// save it for the next read
					m_register = register;
					m_bits = bits;
					Contract.Debug.Ensures(m_bits is > 0 and <= 64 && m_remaining >= 0, "Corrupted state after peeking");
				}

				// peek 31 bits from the register
				return (uint)(register >> (64 - 31));
			}

			/// <summary>Reads a full 31-bit word from the input</summary>
			/// <returns>Value of the word, or <see cref="NotEnough"/> if there are not enough bits remaining</returns>
			public uint Read()
			{
				Contract.Debug.Requires(m_bits >= 0 && m_bits <= 64);

				int bits = m_bits;
				ulong register = m_register;

				if (bits == 31)
				{ // shortcut
					m_bits = 0;
					m_register = 0;
					return (uint)(register >> (64 - 31));
				}

				// if not enough bits, must refill the register
				if (bits < 31 && (!FillRegister(ref register, ref bits) || bits < 31))
				{ // not enough bits remaining
					return NotEnough;
				}
				Contract.Debug.Assert(bits is >= 31 and <= 64, "bad bits " + bits);

				// consume 31 bits from the register
				m_bits = bits - 31;
				m_register = register << 31;

				Contract.Debug.Ensures(m_bits is >= 0 and <= 64 - 31 && m_remaining >= 0, $"Corrupted state after reading {m_bits} {m_remaining}");
				return (uint) (register >> (64 - 31));
			}
		
			/// <summary>Only read the next word if it is equal to the expected value</summary>
			/// <param name="expected">Value that the word must have to be read</param>
			/// <returns>True if the next word was equal to <paramref name="expected"/>; otherwise, false.</returns>
			public bool ReadIf(uint expected)
			{
				Contract.Debug.Requires(expected != NotEnough);

				if (m_bits < 31 && m_remaining == 0)
				{ // not enough bits
					return false;
				}

				uint peek = Peek();
				if (peek != expected) return false;

				// advance the cursor
				Contract.Debug.Assert(m_bits >= 0);
				m_register <<= 31;
				if (m_bits >= 31) m_bits -= 31; else m_bits = 0;

				Contract.Debug.Ensures(m_bits >= 0);
				return true;
			}

			/// <summary>Returns the number of bits left in the register (0 if empty)</summary>
			public int Bits => m_bits;

			/// <summary>Returns the last word, padded with 0s</summary>
			/// <exception cref="InvalidOperationException">If there is at least one full word remaining</exception>
			public uint ReadLast()
			{
				if (m_bits >= 31) throw new InvalidOperationException("There are still words left to read in the source");

				if (m_bits == 0) throw new InvalidOperationException("There are no more bits in the source");

				//note: padding should already be there
				var value = (uint)(m_register >> (64 - 31));
				m_register = 0;
				m_bits = 0;
				return value;
			}

		}

		/// <summary>Compress a slice in memory</summary>
		public static int CompressTo(Slice input, CompressedBitmapWriter output)
		{
			if (input.IsNullOrEmpty) return 0;

			unsafe
			{
				fixed (byte* ptr = input.Array)
				{
					return CompressToUnsafe(ptr + input.Offset, input.Count, output);
				}
			}
		}

		/// <summary>Compress a buffer in native memory</summary>
		/// <param name="buffer">Pointer to the native memory buffer to compress</param>
		/// <param name="count">Number of bytes to compress</param>
		/// <param name="output">Where to write the compressed words</param>
		/// <returns>Number of extra bits that where output in the last literal word (or none if 0)</returns>	
		internal static unsafe int CompressToUnsafe(byte* buffer, int count, CompressedBitmapWriter output)
		{
			// Simplified algorithm:
			// 1) read 31 bits from input (BE)
			// 2) if not all 0 (or all 1), then output a literal word (MSB set to 0), and jump back to step 1)
			// 3) set LENGTH = 1, FILL_BIT = 0 (or 1)
			// 4) Peek at next 31 bits, and if they are still all 0 (or 1), increment N, and jump back to step 4)
			// 5) output a repeat word, with MSB set to 1, followed by FILL_BIT, and then LENGTH-1 (30 bit), and jump back to step 1)

			// Optimizations:
			// - for very small inputs (3 bytes or fewer) we return a single literal word
			// - we read 64 bits at a time in the buffer, because it fits nicely in an UInt64 register

			var bucket = new UncompressedWordReader(buffer, count);

			uint word;
			while ((word = bucket.Read()) != UncompressedWordReader.NotEnough)
			{
				output.Write(word);
			}

			// if there are remaining bits, they are padded with 0 and written as a literal
			int bits = bucket.Bits;
			if (bits > 0)
			{
				//note: MSB will already be 0
				word = bucket.ReadLast();
				output.Write(word);
			}

			// write the header
			output.Pack();

			return bits;
		}

		/// <summary>Outputs a debug version of a compressed segment</summary>
		public static StringBuilder DumpCompressed(Slice compressed, StringBuilder? output = null)
		{
			output ??= new();

			if (compressed.Count == 0)
			{
				output.Append("Empty bitmap [0 bytes]");
				return output;
			}

			var reader = new SliceReader(compressed);

			output.Append($"Compressed [{compressed.Count:N0} bytes]:");

			uint header = reader.ReadUInt32();
			int highestBit = (int)header;
			output.Append($" {(compressed.Count >> 2) - 1:N0} words");

			uint p = 0;
			int i = 0;
			while(reader.Remaining >= 4)
			{
				uint word = reader.ReadUInt32();
				if ((word & TYPE_MASK) == BIT_TYPE_LITERAL)
				{
					output.Append($", ({i}:{p}) 0x{word:X8}");
					p += 31;
				}
				else
				{
					uint len = (word & LENGTH_MASK) + 1;
					output.Append($", ({i}:{p}) {(((word & WordAlignHybridEncoder.FILL_MASK) >> WordAlignHybridEncoder.FILL_SHIFT) == 0 ? "zero" : "one")} x {len}");
					p += len * 31;
				}
				i++;
			}
			output.Append(", MSB ").Append(highestBit);
			if (reader.Remaining > 0)
			{
				output.AppendLine($", ERROR: {reader.Remaining:N0} trailing byte(s)");
			}
			return output;
		}
	
		internal enum LogicalOperation
		{
			Not,
			And,
			Or,
			Xor,
			AndNot,
			OrNot,
			XorNot,
		}

		/// <summary>Performs a logical NOT on a compressed bitmaps</summary>
		/// <param name="bitmap">Compressed bitmap</param>
		/// <param name="size">Minimum logical size of the result (bits in the uncompressed bitmap)</param>
		/// <returns>Compressed slice with the result of flipping all the bits in <paramref name="bitmap"/>, containing up to at least <paramref name="size"/> bits.</returns>
		/// <remarks>If <paramref name="bitmap"/> is larger than <paramref name="size"/>, then the resulting bitmap will be larger.</remarks>
		public static CompressedBitmap Not(this CompressedBitmap bitmap, int size)
		{
			Contract.NotNull(bitmap);

			// there is a high change that the final bitmap will have the same size, with an optional extra filler word at the end
			var writer = new CompressedBitmapWriter(bitmap.Count + 1);
			int n = 0;
			if (bitmap.Count > 0)
			{
				foreach (var word in bitmap)
				{
					if (word.IsLiteral)
					{
						writer.Write(CompressedWord.MakeLiteral((uint)(~word.Literal)));
						n += 31;
					}
					else
					{
						int fc = word.FillCount;
						writer.Write(word.FillBit == 1 ? CompressedWord.ALL_ZEROES : CompressedWord.ALL_ONES, fc);
						n += 31 * fc;
					}
				}
			}
			if (n < size)
			{
				writer.Write(CompressedWord.ALL_ONES, size / 31);
				int r = size % 31;
				if (r > 0) writer.Write((1u << r) - 1);
			}
			return writer.GetBitmap();
		}

		/// <summary>Performs a logical AND between two compressed bitmaps</summary>
		/// <param name="left">First compressed bitmap</param>
		/// <param name="right">Second compressed bitmap</param>
		/// <returns>Compressed slice with the result of boolean expression <paramref name="left"/> AND <paramref name="right"/></returns>
		public static CompressedBitmap And(this CompressedBitmap left, CompressedBitmap right)
		{
			Contract.NotNull(left);
			Contract.NotNull(right);

			if (left.Count == 0 || right.Count == 0) return CompressedBitmap.Empty;
			return CompressedBinaryExpression(left, right, LogicalOperation.And);
		}

		/// <summary>Performs a logical OR between two compressed bitmaps</summary>
		/// <param name="left">First compressed bitmap</param>
		/// <param name="right">Second compressed bitmap</param>
		/// <returns>Compressed slice with the result of boolean expression <paramref name="left"/> AND <paramref name="right"/></returns>
		public static CompressedBitmap Or(this CompressedBitmap left, CompressedBitmap right)
		{
			Contract.NotNull(left);
			Contract.NotNull(right);

			if (left.Count == 0) return right.Count == 0 ? CompressedBitmap.Empty : right;
			if (right.Count == 0) return left;
			return CompressedBinaryExpression(left, right, LogicalOperation.Or);
		}

		/// <summary>Performs a logical XOR between two compressed bitmaps</summary>
		/// <param name="left">First compressed bitmap</param>
		/// <param name="right">Second compressed bitmap</param>
		/// <returns>Compressed slice with the result of boolean expression <paramref name="left"/> AND <paramref name="right"/></returns>
		public static CompressedBitmap Xor(this CompressedBitmap left, CompressedBitmap right)
		{
			if (left == null) throw new ArgumentNullException(nameof(left));
			if (right == null) throw new ArgumentNullException(nameof(right));

			if (left.Count == 0) return right.Count == 0 ? CompressedBitmap.Empty : right;
			if (right.Count == 0) return left;
			return CompressedBinaryExpression(left, right, LogicalOperation.Xor);
		}

		/// <summary>Performs a logical NAND between two compressed bitmaps</summary>
		/// <param name="left">First compressed bitmap</param>
		/// <param name="right">Second compressed bitmap</param>
		/// <returns>Compressed slice with the result of boolean expression <paramref name="left"/> AND NOT(<paramref name="right"/>)</returns>
		public static CompressedBitmap AndNot(this CompressedBitmap left, CompressedBitmap right)
		{
			if (left == null) throw new ArgumentNullException(nameof(left));
			if (right == null) throw new ArgumentNullException(nameof(right));

			if (left.Count == 0 || right.Count == 0) return CompressedBitmap.Empty;
			return CompressedBinaryExpression(left, right, LogicalOperation.AndNot);
		}

		/// <summary>Performs a logical NOR between two compressed bitmaps</summary>
		/// <param name="left">First compressed bitmap</param>
		/// <param name="right">Second compressed bitmap</param>
		/// <returns>Compressed slice with the result of boolean expression <paramref name="left"/> OR NOT(<paramref name="right"/>)</returns>
		public static CompressedBitmap OrNot(this CompressedBitmap left, CompressedBitmap right)
		{
			if (left == null) throw new ArgumentNullException(nameof(left));
			if (right == null) throw new ArgumentNullException(nameof(right));

			if (left.Count == 0) return right.Count == 0 ? CompressedBitmap.Empty : right;
			if (right.Count == 0) return left;
			return CompressedBinaryExpression(left, right, LogicalOperation.OrNot);
		}

		/// <summary>Performs a logical NXOR between two compressed bitmaps</summary>
		/// <param name="left">First compressed bitmap</param>
		/// <param name="right">Second compressed bitmap</param>
		/// <returns>Compressed slice with the result of boolean expression <paramref name="left"/> XOR NOT(<paramref name="right"/>)</returns>
		public static CompressedBitmap XorNot(this CompressedBitmap left, CompressedBitmap right)
		{
			if (left == null) throw new ArgumentNullException(nameof(left));
			if (right == null) throw new ArgumentNullException(nameof(right));

			if (left.Count == 0) return right.Count == 0 ? CompressedBitmap.Empty : right;
			if (right.Count == 0) return left;
			return CompressedBinaryExpression(left, right, LogicalOperation.XorNot);
		}

		/// <summary>Performs a binary operation between two compressed bitmaps</summary>
		/// <param name="left">First compressed bitmap</param>
		/// <param name="right">Second compressed bitmap</param>
		/// <param name="op">Type of operation to perform (And, Or, Xor, ...)</param>
		/// <returns>Compressed slice with the result of boolean expression <paramref name="left"/> AND <paramref name="right"/></returns>
		internal static CompressedBitmap CompressedBinaryExpression(CompressedBitmap left, CompressedBitmap right, LogicalOperation op)
		{
			Contract.Debug.Requires(left != null && right != null && /*op != LogicalOperation.And &&*/ Enum.IsDefined(typeof(LogicalOperation), op));

			var writer = new CompressedBitmapWriter();
			using (var itLeft = left.GetEnumerator())
			using (var itRight = right.GetEnumerator())
			{
				int ln = 0; // remaining count of current word in left
				int rn = 0; // remaining count of current word in right

				uint lw = 0; // value of current word in left (if ln > 0)
				uint rw = 0; // value of current word in right (if rn > 0)

				const int DONE = -1;

				while (true)
				{
					if (ln == 0)
					{
						if (!itLeft.MoveNext())
						{ // left is done
							if (op == LogicalOperation.And || rn == DONE)
							{ // no need to continue
								break;
							}
							// continue with right until it's done
							ln = DONE;
							lw = 0;
							continue;
						}
						ln = itLeft.Current.WordCount;
						lw = itLeft.Current.WordValue;
					}
					if (rn == 0)
					{
						if (!itRight.MoveNext())
						{ // right is done
							if (op == LogicalOperation.And || ln == DONE)
							{ // no need to continue
								break;
							}
							// continue with left until it's done
							rn = DONE;
							rw = 0;
							continue;
						}
						rn = itRight.Current.WordCount;
						rw = itRight.Current.WordValue;
					}

					if (ln == DONE)
					{ // copy right
						writer.Write(rw, rn);
						rn = 0;
					}
					else if (rn == DONE)
					{ // copy left
						writer.Write(lw, ln);
						ln = 0;
					}
					else
					{ // merge left & right
						int n = Math.Min(ln, rn);
						switch (op)
						{
							case LogicalOperation.And:    writer.Write(lw & rw, n); break;
							case LogicalOperation.AndNot: writer.Write(lw & (~rw & LITERAL_MASK), n); break;
							case LogicalOperation.Or:     writer.Write(lw | rw, n); break;
							case LogicalOperation.OrNot:  writer.Write(lw | (~rw & LITERAL_MASK), n); break;
							case LogicalOperation.Xor:    writer.Write(lw ^ rw, n); break;
							case LogicalOperation.XorNot: writer.Write(lw ^ (~rw & LITERAL_MASK), n); break;
							default: throw new InvalidOperationException();
						}
						ln -= n;
						rn -= n;
					}
				}
			}

			return writer.GetBitmap();
		}

	}

}
