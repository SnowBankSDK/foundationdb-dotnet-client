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
	using System.Buffers;
	using System.Buffers.Binary;
	using System.Diagnostics.CodeAnalysis;
	using System.Numerics;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using SnowBank.Buffers.Binary;

	/// <summary>Builder of compressed bitmaps that can set or clear bits in a random order, in memory</summary>
	[DebuggerDisplay("Size={m_size}, Bounds={m_lowest}..{m_highest}")]
	public struct CompressedBitmapBuilder : IDisposable
	{

		/// <summary>Returns a new instance of an empty bitmap builder</summary>
		public static CompressedBitmapBuilder Empty => new([ ], 0, BitRange.Empty);

		/// <summary>Buffer of compressed words</summary>
		private CompressedWord[] m_words;

		/// <summary>Number of words used in the buffer</summary>
		private int m_size;

		/// <summary>Index of the lowest bit that is set (or int.MaxValue)</summary>
		private long m_lowest;

		/// <summary>Index of the highest bit that is set (or -1)</summary>
		private long m_highest;

		/// <summary>Optional pool used to allocate <see cref="m_words"/></summary>
		private ArrayPool<CompressedWord>? m_pool;

		/// <summary></summary>
		/// <param name="bitmap"></param>
		/// <param name="pool">Optional pool used to allocate internal buffer</param>
		/// <exception cref="ArgumentNullException">If <paramref name="bitmap"/> is <see langword="null"/></exception>
		/// <exception cref="ArgumentException">If <paramref name="bitmap"/> does not have a valid size (must be multiple of 4 bytes)</exception>
		/// <remarks>If <paramref name="pool"/> is not null, this instance <b>MUST</b> be disposed, otherwise the rented buffers will not be returned to the pool!</remarks>
		public CompressedBitmapBuilder(CompressedBitmap bitmap, ArrayPool<CompressedWord>? pool = null)
		{
			Contract.NotNull(bitmap);
			Contract.Multiple(bitmap.ByteCount, 4, "The underlying buffer size should be a multiple of 4 bytes", nameof(bitmap));

			if (bitmap.Count == 0)
			{ // empty bitmap
				m_words = [ ];
				var range = BitRange.Empty;
				m_lowest = range.Lowest;
				m_highest = range.Highest;
				m_pool = pool;
			}
			else
			{ // decode the bimapt, extract the bounds

				m_words = DecodeWords(bitmap.Data.Span, bitmap.Count, bitmap.Bounds, pool);
				m_size = bitmap.Count;
				m_pool = pool;

				var bounds = bitmap.Bounds;
				m_lowest = bounds.Lowest;
				m_highest = bounds.Highest;
			}
		}

		public CompressedBitmapBuilder(Slice data, ArrayPool<CompressedWord>? pool = null)
			: this(new CompressedBitmap(SliceOwner.Create(data), ownsData: false), pool)
		{ }

		public CompressedBitmapBuilder(SliceOwner data, bool ownsData, ArrayPool<CompressedWord>? pool = null)
			: this(new CompressedBitmap(data, ownsData), pool)
		{ }

		internal CompressedBitmapBuilder(CompressedWord[] words, int size, BitRange range)
		{
			Contract.Debug.Requires(words != null && size >= 0);
			m_words = words;
			m_size = size;
			m_lowest = range.Lowest;
			m_highest = range.Highest;
		}

		public void Dispose()
		{
			var pool = m_pool;
			if (pool != null)
			{
				var words = m_words;
				m_words = [ ];
				m_pool = null;
				if (words.Length != 0)
				{
					pool.Return(words);
				}
				m_size = 0;
			}
		}

		public readonly ReadOnlySpan<CompressedWord> Words => m_words.AsSpan(0, m_size);

		internal static CompressedWord[] DecodeWords(ReadOnlySpan<byte> data, int size, BitRange bounds, ArrayPool<CompressedWord>? pool)
		{
			Contract.Debug.Requires(size >= 0 && data.Length >= 4 && (data.Length & 3) == 0);

			CompressedWord[] words;
			if (pool == null)
			{
				int capacity = BitHelpers.NextPowerOfTwo(size);
				if (capacity < 0) capacity = size;
				words = new CompressedWord[capacity];
			}
			else
			{
				words = pool.Rent(size);
				// we don't know if it was cleared by the previous user!
				Array.Clear(words);
			}

			var buf = data[4..];

			int i = 0;
			while(buf.Length > 0)
			{
				words[i++] = new(BinaryPrimitives.ReadUInt32LittleEndian(buf));
				buf = buf[4..];
			}

			return words;
		}

		/// <summary>Returns the number of compressed words in the builder</summary>
		public readonly int Count => m_size;

		/// <summary>Compute the word index and mask, from a bit offset</summary>
		/// <param name="offset">Bit offset (0-based)</param>
		/// <param name="mask">Mask of the bit in the word</param>
		/// <returns>Index of the data word (0-based)</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int GetWordIndex(long offset, out uint mask)
		{
			mask = 1U << (int) (offset % 31);
			return checked((int) (offset / 31));
		}

		/// <summary>Returns the index of where a data word is located in the current compressed bitmap</summary>
		/// <param name="wordIndex">Data word index (0-based) in the uncompressed bitmap</param>
		/// <param name="offset">Receives the index in the compressed word table that would need to be patched</param>
		/// <param name="position">Receives the absolute index of the word at <paramref name="offset"/> (ie: compressed word #<paramref name="offset"/> is the <paramref name="position"/>th uncompressed data word)</param>
		/// <returns>True if the modified word is inside the bitmap, or false if it falls outside</returns>
		private bool GetCompressedWordIndex(int wordIndex, out int offset, out int position)
		{
			// we need to find the offset in the compressed word list, where this word index should be stored or inserted

			//var words = new ReadOnlySpan<CompressedWord>(m_words, 0, m_size);
			var words = m_words;
			var size = m_size;

			int p = 0;
			for (int i = 0; i < size; i++)
			{
				int o = p;
				p += words[i].WordCount;
				if (p > wordIndex)
				{
					offset = i;
					position = o;
					return true;
				}
			}
			// we are outside
			offset = size;
			position = p;
			return false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureCapacity(int minSize)
		{
			if (minSize > m_size)
			{
				Grow(minSize);
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void Grow(int minSize)
		{
			int newSize = (int) BitOperations.RoundUpToPowerOf2((uint) minSize);
			if (newSize < 0) newSize = minSize;
			if (newSize < 8) newSize = 8;
			if (m_pool != null)
			{
				var prev = m_words;
				var tmp = m_pool.Rent(newSize);
				prev.AsSpan(0, m_size).CopyTo(tmp);
				m_words = tmp;
				if (prev.Length != 0)
				{
					m_pool.Return(prev);
				}
			}
			else
			{
				Array.Resize(ref m_words, newSize);
			}
		}

		/// <summary>Gets or sets the value of a bit in the bitmap.</summary>
		/// <param name="index">Absolute index (0-based) of the bit to test</param>
		/// <returns>True if the bit is set; otherwise, false.</returns>
		public bool this[int index]
		{
			readonly get => Test(index);
			set { if (value) Set(index); else Clear(index); }
		}

		/// <summary>Test the value of a bit in the bitmap.</summary>
		/// <param name="index">Absolute index (0-based) of the bit to test</param>
		/// <returns>True if the bit is set; otherwise, false.</returns>
		public readonly bool Test(int index)
		{
			throw new NotImplementedException();
		}

		/// <summary>Shift all words after <paramref name="position"/> by <paramref name="count"/> slots</summary>
		internal void Shift(int position, int count)
		{
			// ensure we have enough space
			EnsureCapacity(m_size + count);
			// move everything to the right of position
			var words = m_words;
			words.AsSpan(position, m_size - position).CopyTo(words.AsSpan(position + count));
			//Array.Copy(m_words, position, m_words, position + count, m_size - position);
			// and update the size
			m_size += count;
		}

		/// <summary>Set a bit in the bitmap.</summary>
		/// <param name="index">Absolute index (0-based) of the bit to set</param>
		/// <returns>True if the bit was changed from 0 to 1; or false if it was already set.</returns>
		public bool Set(long index)
		{
			Contract.Positive(index);

			//Console.WriteLine("Set({0}) on {1}-words bitmap", index, m_size);

			if (m_highest < 0)
			{ // first bit set in an empty builder
				m_highest = index;
				m_lowest = index;
			}
			else
			{ // update the bounds
				if (index > m_highest) m_highest = index;
				if (index < m_lowest) m_lowest = index;
			}

			int wordIndex = GetWordIndex(index, out uint mask);
			//Console.WriteLine("> bitOffset {0} is in data word #{1} with mask {2}", index, wordIndex, mask);

			if (!GetCompressedWordIndex(wordIndex, out int offset, out int position))
			{ // falls outside the bitmap, need to add new words

				int count = wordIndex - position;
				if (count > 0)
				{
					//Console.WriteLine("> outside by {0}, need filler", count);
					EnsureCapacity(m_size + 2);
					m_words[m_size++] = CompressedWord.MakeZeroes(count);
				}
				else
				{
					//Console.WriteLine("> outside, right next to it");
					EnsureCapacity(m_size + 1);
				}
				m_words[m_size++] = CompressedWord.MakeLiteral(mask);
				return true;
			}

			//Console.WriteLine("> would be in slot #{0} which starts at data-word #{1}", offset, position);

			// read the existing word
			var word = m_words[offset];

			// we can patch literals in place
			if (word.IsLiteral)
			{
				//Console.WriteLine("> PATCH [...] [{0}:0x{0:X8}] [...]", offset, mask);
				var before = word.RawValue;
				var after = before | mask;
				if (before == after)
				{
					return false;
				}

				if (after == CompressedWord.ALL_ONES)
				{ // convert to an all 1 literal!
					if (offset > 0)
					{
						// check if we can merge with a previous 1-filler
						var prev = m_words[offset - 1];
						if (!prev.IsLiteral && prev.FillBit == 1)
						{
							m_words[offset - 1] = CompressedWord.MakeOnes(prev.FillCount + 1);
							int r = m_size - offset - 1;
							if (r > 0)
							{
								m_words.AsSpan(offset + 1, r).CopyTo(m_words.AsSpan(offset));
								//Array.Copy(m_words, offset + 1, m_words, offset, r);
							}

							--m_size;
							return true;
						}
					}
					//TODO: also need to check if the next one is also a filler!

					// convert this one to a filler
					after = CompressedWord.MakeOnes(1);
				}
				m_words[offset] = new(after);
				return true;
			}

			// if it is an all-1 filler, our job is already done
			if (word.FillBit == 1)
			{
				//Console.WriteLine("> was already a 1-filler");
				return false;
			}

			// for all-1 fillers, we must break them so that we can insert a new literal
			SplitFiller(word, offset, mask, wordIndex - position);
			return true;
		}

		/// <summary>Clear a bit in the bitmap</summary>
		/// <param name="index">Offset (0-based) of the bit to clear</param>
		/// <returns>True if the bit was changed from 1 to 0; or false if it was already unset.</returns>
		public bool Clear(long index)
		{
			Contract.Positive(index);

			int wordIndex = GetWordIndex(index, out uint mask);

			if (!GetCompressedWordIndex(wordIndex, out int offset, out int position))
			{ // outside the buffer, nothing to do
				return false;
			}

			var word = m_words[offset];

			if (!word.IsLiteral)
			{
				if (word.FillBit == 0)
				{ // already a 0, nothing to do
					return false;
				}

				// for all-1 fillers, we must break them so that we can insert a new literal
				SplitFiller(word, offset, ~mask, wordIndex - position);
				//TODO: update lowest/highest?
				return true;
			}

			// patch the literal

			uint o = word.RawValue;
			uint w = o & ~mask;
			if (w == o)
			{ // no changes
				return false;
			}

			if (w == 0)
			{ // last bit was removed
				if (offset == m_size -1)
				{ // that was the last one, truncate!
					// also kill any 0-fillers up until there
					--m_size;
					m_highest -= (m_highest % 31);

					while(m_size > 0)
					{
						int p = m_size - 1;
						var ww = m_words[p];
						if (!ww.IsZero)
						{
							break;
						}
						m_size = p;
						m_highest -= 31;
					}
					if (m_lowest > m_highest)
					{
						m_lowest = m_highest;
					}
				}
				else
				{ // convert to filler
					m_words[offset] = CompressedWord.MakeZeroes(1);
					if ((index / 31) == (m_lowest / 31))
					{ // we changed the lowest bit!
						m_lowest = ((index / 31) + 1) * 31;
						int p = offset + 1;
						while (p < m_size)
						{
							var ww = m_words[p];
							if (!ww.IsZero)
							{
								if (ww.IsLiteral)
								{
									// we need to compute the offset lowest bit!
									int off = BitOperations.TrailingZeroCount(m_words[p].Literal);
									m_lowest += off;
								}
								break;
							}
							m_lowest += 31;
							++p;
						}
						//TODO: 
					}
					//TODO: merge!
				}
			}
			else if ((index / 31) == (m_highest / 31))
			{ // maybe we removed the highest bit?
				m_highest += BitOperations.Log2(w) - BitOperations.Log2(o);
			}
			else if ((index / 31) == (m_lowest / 31))
			{ // maybe we removed the lowest bit?
				m_lowest += BitOperations.TrailingZeroCount(w) - BitOperations.TrailingZeroCount(o);
			}

			m_words[offset] = CompressedWord.MakeLiteral(w);
			//TODO: update lowest/highest!
			return true;

		}

		private void SplitFiller(CompressedWord word, int offset, uint value, int relativeOffset)
		{
			bool set = word.FillBit == 1;

			int count = word.FillCount;	// how many words we need to split
			// how many full words are there before our inserted literal?
			// in bits: index - position
			//Console.WriteLine("> Gap of " + (index - (position * 31)) + " in front of our literal");
			int head = ((relativeOffset * 31) / 31); // how many empty words will stay before the inserted literal;
			int tail = count - head - 1; // how many empty words will stay after the inserted literal

			//Console.WriteLine("> Splitting 1-filler with repeat count {1} at {0}, with {2} before and {3} after", offset, count, head, tail);

			if (head > 0)
			{ // keep the current filler, need to insert one or two words after it

				// update the current filler
				m_words[offset] = CompressedWord.MakeFiller(set, head);

				if (tail > 0)
				{ // insert a literal and a filler
					//Console.WriteLine("> INSERT [...] ({0}:{1}) [{2}:0x{3:X8}] ({4}:{5}) [...]", offset, head, offset + 1, mask, offset + 2, tail);
					Shift(offset + 1, 2);
					m_words[offset + 1] = CompressedWord.MakeLiteral(value);
					m_words[offset + 2] = CompressedWord.MakeFiller(set, tail);
				}
				else
				{ // only a literal
					//Console.WriteLine("> INSERT [...] ({0}:{1}) [0x{2:X8}] [...]", offset, head, mask);
					Shift(offset + 1, 1);
					m_words[offset + 1] = CompressedWord.MakeLiteral(value);
				}
			}
			else
			{
				if (tail > 0)
				{ // replace current with a literal and add a filler
					//Console.WriteLine("> INSERT [....] [{0}:0x{1:X8}] ({2}:{3}) [...]", offset, mask, offset + 1, tail);
					Shift(offset + 1, 1);
					m_words[offset + 1] = CompressedWord.MakeFiller(set, tail);
				}
				else
				{ // patch in place
					//Console.WriteLine("> PATCH [...] [{0}:0x{0:X8}] [...]", offset, mask);
				}
				m_words[offset] = CompressedWord.MakeLiteral(value);
			}
		}

		/// <summary>Pack the builder back into a slice</summary>
		public readonly Slice ToSlice() => Pack(m_words, m_size, m_highest, null).Data;

		/// <summary>Pack the builder back into a slice</summary>
		public readonly SliceOwner ToSlice(ArrayPool<byte>? pool) => Pack(m_words, m_size, m_highest, null);

		public readonly CompressedBitmap ToBitmap(ArrayPool<byte>? pool = null)
		{
			return m_size == 0
				? CompressedBitmap.Empty
				: new(
					data: Pack(m_words, m_size, m_highest, pool),
					bounds: new(m_lowest, m_highest),
					ownsData: true
				  );
		}

		internal static SliceOwner Pack(CompressedWord[] words, int size, long highest, ArrayPool<byte>? pool)
		{
			Contract.Debug.Requires(size >= 0 && size <= words.Length);

			if (size == 0)
			{ // empty bitmap
				return SliceOwner.Empty;
			}

			var writer = new SliceWriter(checked((size + 1) << 2), pool);
			writer.WriteUInt32(CompressedWord.MakeHeader(highest));
			for (int i = 0; i < size; i++)
			{
				writer.WriteUInt32(words[i].RawValue);
			}
			return writer.ToSliceOwner();
		}

		public readonly bool[] ToBooleanArray()
		{
			int n = checked((int) (m_highest + 1));
			var res = new List<bool>(n);

			for (int i = 0; i < m_size; i++)
			{
				var word = m_words[i];
				if (word.IsLiteral)
				{
					int w = word.Literal;
					int j = 31;
					while (j-- > 0)
					{
						res.Add((w & 1) == 1);
						w >>= 1;
					}
				}
				else if (word.FillBit == 1)
				{
					int j = word.FillCount * 31;
					while (j-- > 0)
					{
						res.Add(true);
					}
				}
				else
				{
					int j = word.FillCount * 31;
					while (j-- > 0)
					{
						res.Add(false);
					}
				}
			}

			if (res.Count > n) res.RemoveRange(n, res.Count - n);

			return res.ToArray();
		}

	}

	public sealed class CompressedBitmapIndexBuilder<TKey> : IDisposable
		where TKey : notnull
	{

		private Dictionary<TKey, CompressedBitmapBuilder> Builders { get; }

		private ArrayPool<CompressedWord>? Pool { get; }

		public CompressedBitmapIndexBuilder(int capacity = 0, IEqualityComparer<TKey>? comparer = null, ArrayPool<CompressedWord>? pool = null)
		{
			this.Builders = new(capacity, comparer);
			this.Pool = pool;
		}

		public int Count => this.Builders.Count;

		public void Add(TKey key, int index)
		{
			ref var builder = ref CollectionsMarshal.GetValueRefOrAddDefault(this.Builders, key, out var exists);
			if (!exists)
			{
				builder = new(CompressedBitmap.Empty, this.Pool);
			}
			builder.Set(index);
		}

		public bool TryGetBitmap(TKey key, [MaybeNullWhen(false)] out CompressedBitmap bitmap, ArrayPool<byte>? pool = null)
		{
			if (this.Builders.TryGetValue(key, out var builder))
			{
				bitmap = builder.ToBitmap(pool);
				return true;
			}

			bitmap = default;
			return false;
		}

		public void Dispose()
		{
			if (this.Pool != null)
			{
				foreach (var builder in this.Builders.Values)
				{
					builder.Dispose();
				}
			}
			this.Builders.Clear();
		}

	}

}
