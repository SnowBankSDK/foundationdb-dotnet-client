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

// ReSharper disable GrammarMistakeInComment

namespace SnowBank.Buffers
{
	using System.Collections;
	using System.Numerics;
	using SnowBank.Buffers.Binary;

	/// <summary>Represents a fixed-size bit map, backed by an array of 64-bit words</summary>
	[DebuggerDisplay("Capacity={Capacity}, Words={Words.Length")]
	[PublicAPI]
	public readonly struct BitMap64 : IFormattable, IReadOnlyList<bool>
	{
		// Maps are fixed length array of 64-bit ulongs (or words)
		// Each word contains bits in order from LSB to MSB
		// Word at offset 0 contains bits 0 to 63, offset 1 contains bits 64 to 127, and so on
		// Attempts to manipulate bits outside the map will throw
		// This struct is NOT sponsored by a certain Japanese game console vendor...

		/// <summary>Shift to get index of word from bit index</summary>
		public const int IndexShift = 6;

		/// <summary>Number of bits per word</summary>
		public const int WordSize = 1 << IndexShift;

		/// <summary>Mask to get offset of bit inside word from bit index</summary>
		public const int WordMask = (1 << IndexShift) - 1;

		/// <summary>Array used to store the bits of this bitmap</summary>
		public readonly Memory<ulong> Words;

		/// <summary>Initialize a new BitMap</summary>
		/// <param name="bits">Minimum required capacity (in bits)</param>
		/// <remarks>
		/// The internal capacity will always be rounded to the upper 64 bits.
		/// All bits will be cleared (0)
		/// </remarks>
		public BitMap64(long bits)
		{
			Contract.Between(bits, 0, 1L << 31, message: "The bitmap would be too large in memory");
			if (bits == 0)
			{
				this.Words = Array.Empty<ulong>();
			}
			else
			{
				this.Words = new ulong[checked(bits + WordMask) / WordSize];
			}
		}

		/// <summary>Initialize a Bitmap from an existing storage</summary>
		/// <param name="words">Array of words used by this bitmap (each word storing 64 bits)</param>
		/// <remarks>This Bitmap will mutate the content of <paramref name="words"/>. Likewise, any change made to <paramref name="words"/> will be visible from this instance</remarks>
		public BitMap64(ulong[]? words)
		{
			this.Words = words ?? [ ];
		}

		/// <summary>Initialize a Bitmap from an existing storage</summary>
		/// <param name="words">Array of words used by this bitmap (each word storing 64 bits)</param>
		/// <remarks>This Bitmap will mutate the content of <paramref name="words"/>. Likewise, any change made to <paramref name="words"/> will be visible from this instance</remarks>
		public BitMap64(Memory<ulong> words)
		{
			this.Words = words;
		}

		/// <summary>Return a copy of the bitmap</summary>
		/// <returns>New bitmap which is identical, but does not share the same underlying storage</returns>
		public BitMap64 Copy()
		{
			return new BitMap64(this.Words.ToArray());
		}

		/// <summary>Returns the capacity (in bits) of this bitmap</summary>
		public long Capacity => checked(((long) this.Words.Length) * WordSize);

		/// <summary>Gets or sets the value of a specific bit</summary>
		public bool this[int bit]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Test(this.Words.Span, bit);
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set => Toggle(this.Words.Span, bit, value);
		}

		/// <summary>Gets or sets the value of a specific bit</summary>
		public bool this[long bit]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Test(this.Words.Span, bit);
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set => Toggle(this.Words.Span, bit, value);
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return ToString(null);
		}

		/// <inheritdoc />
		[Pure]
		public string ToString(string? format, IFormatProvider? provider = null)
		{
			switch (format ?? "D")
			{
				case "D":
				case "X":
				{
					return Stringify16(this.Words.Span, this.Capacity);
				}

				case "B":
				{
					return Stringify2(this.Words.Span, this.Capacity);
				}
				case "P":
				{
					return Stringify2(this.Words.Span, this.Capacity, '_', '#');
				}

				default:
				{
					throw new ArgumentException("Unsupported format specifier.", nameof(format));
				}
			}
		}

		/// <summary>Returns a base-16 string representation of this bitmap, composed of 0-9+A-F characters</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string ToHex()
		{
			return Stringify16(this.Words.Span, this.Capacity);
		}

		/// <summary>Returns a base-2 string representation of this bitmap, composed of 1s and 0s</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string ToBinary(char zero = '0', char one = '1')
		{
			return Stringify2(this.Words.Span, this.Capacity, zero, one);
		}

		/// <summary>Returns a string representation of a bitmap in the specified base</summary>
		[Pure]
		public static string Stringify(ReadOnlySpan<ulong> map, long capacity, int @base)
		{
			if (map.Length == 0 || capacity == 0)
			{
				return string.Empty;
			}

			unsafe
			{
				fixed (ulong* ptr = map)
				{
					return Stringify(ptr, capacity, @base);
				}
			}
		}

		/// <summary>Returns a string representation of a bitmap in the specified base</summary>
		[Pure]
		public static unsafe string Stringify(ulong* map, long capacity, int @base)
		{
			if (map == null || capacity == 0) return string.Empty;
			Contract.Debug.Requires((capacity & 63) == 0);

			switch (@base)
			{
				case 2:
				{
					return Stringify2(map, capacity);
				}
				case 16:
				{
					return Stringify16(map, capacity);
				}
				//TODO: base 64?
				default:
				{
					throw new ArgumentOutOfRangeException(nameof(@base), @base, "Base can only be 2 or 16");
				}
			}
		}

		/// <summary>Returns a base-2 string representation of a bitmap, composed of 1s and 0s</summary>
		[Pure]
		internal static string Stringify2(ReadOnlySpan<ulong> map, long capacity, char zero = '0', char one = '1')
		{
			if (map.Length == 0 || capacity == 0)
			{
				return string.Empty;
			}

			unsafe
			{
				fixed (ulong* ptr = map)
				{
					return Stringify2(ptr, capacity, zero, one);
				}
			}
		}

		[Pure]
		internal static unsafe string Stringify2(ulong* map, long capacity, char zero = '0', char one = '1')
		{
			var chars = new char[capacity];
			fixed (char* buf = &chars[0])
			{
				char* outp = buf;
				ulong* inp = map;
				ulong* end = map + (capacity / WordSize);
				while (inp < end)
				{
					ulong w = *inp++;
					for (int j = 0; j < 64; j++)
					{
						//TODO: unroll?
						*outp++ = (w & 1) == 0 ? zero : one;
						w >>= 1;
					}
				}
			}
			return new string(chars);
		}

		/// <summary>Returns a base-16 string representation of a bitmap, composed of 0-9+A-F characters</summary>
		[Pure]
		internal static string Stringify16(ReadOnlySpan<ulong> map, long capacity)
		{
			if (map.Length == 0 || capacity == 0)
			{
				return string.Empty;
			}

			unsafe
			{
				fixed (ulong* ptr = map)
				{
					return Stringify16(ptr, capacity);
				}
			}
		}

		[Pure]
		internal static unsafe string Stringify16(ulong* map, long capacity)
		{
			var chars = new char[checked((capacity / WordSize) * 16)];
			fixed (char* buf = &chars[0])
			{
				char* outp = buf;
				ulong* inp = map;
				ulong* end = inp + (capacity / WordSize);
				while (inp < end)
				{ // unroll to the max!
					ulong w = *inp++;
					outp[0x0] = UnsafeHelpers.Nibble((byte) (w >> 60));
					outp[0x1] = UnsafeHelpers.Nibble((byte) (w >> 56));
					outp[0x2] = UnsafeHelpers.Nibble((byte) (w >> 52));
					outp[0x3] = UnsafeHelpers.Nibble((byte) (w >> 48));
					outp[0x4] = UnsafeHelpers.Nibble((byte) (w >> 44));
					outp[0x5] = UnsafeHelpers.Nibble((byte) (w >> 40));
					outp[0x6] = UnsafeHelpers.Nibble((byte) (w >> 36));
					outp[0x7] = UnsafeHelpers.Nibble((byte) (w >> 32));
					outp[0x8] = UnsafeHelpers.Nibble((byte) (w >> 28));
					outp[0x9] = UnsafeHelpers.Nibble((byte) (w >> 24));
					outp[0xA] = UnsafeHelpers.Nibble((byte) (w >> 20));
					outp[0xB] = UnsafeHelpers.Nibble((byte) (w >> 16));
					outp[0xC] = UnsafeHelpers.Nibble((byte) (w >> 12));
					outp[0xD] = UnsafeHelpers.Nibble((byte) (w >> 8));
					outp[0xE] = UnsafeHelpers.Nibble((byte) (w >> 4));
					outp[0xF] = UnsafeHelpers.Nibble((byte) (w));
					outp += 16;
				}
			}
			return new string(chars);
		}

		/// <summary>Returns the index of the lowest bit set, or a negative value if all bits are cleared</summary>
		/// <remarks>This is O(N)</remarks>
		[Pure]
		public long GetLowestBit()
		{
			var words = this.Words;
			if (words.Length == 0) return -1L;
			unsafe
			{
				fixed (ulong* ptr = words.Span)
				{
					return GetLowestBit(ptr, this.Capacity);
				}
			}
		}

		/// <summary>Returns the index of the lowest bit set, or a negative value if all bits are cleared</summary>
		/// <remarks>This is O(N)</remarks>
		[Pure]
		public static unsafe long GetLowestBit(ulong* words, [Positive] long capacity)
		{
			Contract.Debug.Requires(words != null && capacity >= 0);
			var ptr = words;
			var end = ptr + WordsForCapacity(capacity);
			while (ptr < end)
			{
				ulong w = *ptr++;
				if (w != 0)
				{
					return ((ptr - 1 - words) * WordSize) + BitOperations.TrailingZeroCount(w);
				}
			}
			return -1L;
		}

		/// <summary>Returns the index of the highest bit set, or a negative value if all bits are cleared</summary>
		/// <remarks>This is O(N)</remarks>
		[Pure]
		public long GetHighestBit()
		{
			var words = this.Words;
			if (words.Length == 0) return -1L;
			unsafe
			{
				fixed (ulong* ptr = words.Span)
				{
					return GetHighestBit(ptr, this.Capacity);
				}
			}
		}

		/// <summary>Returns the index of the highest bit set, or a negative value if all bits are cleared</summary>
		/// <remarks>This is O(N)</remarks>
		[Pure]
		public static unsafe long GetHighestBit(ulong* words, [Positive] long capacity)
		{
			Contract.Debug.Requires(words != null && capacity >= 0);
			var ptr = words + (WordsForCapacity(capacity) - 1);
			while (ptr >= words)
			{
				ulong w = *ptr--;
				if (w != 0)
				{
					return ((ptr + 1 - words) * WordSize) + BitOperations.Log2(w);
				}
			}
			return -1L;
		}

		/// <summary>Counts the number of bits that are set</summary>
		/// <returns>Number of bits set, or 0 if all bits are cleared</returns>
		/// <remarks>This is O(N)</remarks>
		[Pure]
		public long Population()
		{
			long count = 0;
			//TODO: unroll ?
			foreach (var w in this.Words.Span)
			{
				count += BitOperations.PopCount(w);
			}
			return count;
		}

		/// <summary>Clears all bits of a map</summary>
		public void ClearAll()
		{
			ClearAll(this.Words.Span);
		}

		/// <summary>Clears all bits of a map</summary>
		public static void ClearAll(Span<ulong> map)
		{
			map.Clear();
		}

		/// <summary>Clears all bits of a map</summary>
		public void SetAll()
		{
			SetAll(this.Words.Span);
		}

		/// <summary>Clears all bits of a map</summary>
		public static void SetAll(Span<ulong> map)
		{
			map.Fill(ulong.MaxValue);
		}

		/// <summary>Returns the number of 64-bit words required to store a bitmap of the specified capacity</summary>
		/// <param name="capacity">Capacity (in bits) of the bitmap</param>
		/// <returns>Size (in 64-bit words) of the buffer</returns>
		/// <remarks>This returns the minimum length for an ulong[] array large enough to fit all the bits.</remarks>
		public static int WordsForCapacity(long capacity)
		{
			return checked((int) ((capacity + (WordSize - 1)) / WordSize));
		}

		/// <summary>Returns the number of bytes required to store a bitmap of the specified capacity</summary>
		/// <param name="capacity">Capacity (in bits) of the bitmap</param>
		/// <returns>Size (in bytes) of the buffer</returns>
		public static uint BytesForCapacity(long capacity)
		{
			return checked((uint) (WordsForCapacity(capacity) * sizeof(ulong)));
		}

		/// <summary>Tests if a specific bit is set</summary>
		/// <returns>If true, the bit is set (1). If false, the bit is not set (0).</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Test([Positive] long bitIndex)
		{
			return Test(this.Words.Span, bitIndex);
		}

		/// <summary>Tests if a specific bit is set</summary>
		/// <returns>If true, the bit is set (1). If false, the bit is not set (0).</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Test(ReadOnlySpan<ulong> map, [Positive] long bitIndex)
		{
			return (map[checked((int) (bitIndex >> IndexShift))] & (1UL << (int) (bitIndex & WordMask))) != 0;
		}

		/// <summary>Tests if all the bits in a range are set</summary>
		/// <returns>If true, the bit is set (1). If false, the bit is not set (0).</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TestAll([Positive] long bitIndex, long count)
		{
			Contract.DoesNotOverflow(this.Capacity, bitIndex, count);
			return TestAll(this.Words.Span, bitIndex, count);
		}

		/// <summary>Tests if all the bits in a range are set</summary>
		/// <returns>If true, all the bits are set (1). If false, at least on bit is cleared (0).</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TestAll(ReadOnlySpan<ulong> map, [Positive] long bitIndex, long count)
		{
			Contract.Debug.Requires(bitIndex >= 0 && count >= 0);
			long end = checked(bitIndex + count);
			//TODO: optimize me!
			for (long idx = bitIndex; idx < end; idx++)
			{
				if ((map[checked((int) (bitIndex >> IndexShift))] & (1UL << (int) (bitIndex & WordMask))) == 0)
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>Tests if at least one bit in a range is set</summary>
		/// <returns>If true, at least one bit is set (1). If false, all the bits are cleared (0).</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TestAny([Positive] long bitIndex, long count)
		{
			Contract.DoesNotOverflow(this.Capacity, bitIndex, count);
			return TestAny(this.Words.Span, bitIndex, count);
		}

		/// <summary>Tests if at least one bit in a range is set</summary>
		/// <returns>If true, at least one bit is set (1). If false, all the bits are cleared (0).</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TestAny(ReadOnlySpan<ulong> map, [Positive] long bitIndex, long count)
		{
			Contract.Debug.Requires(bitIndex >= 0 && count >= 0);
			long end = checked(bitIndex + count);
			//TODO: optimize me!
			for (long idx = bitIndex; idx < end; idx++)
			{
				if ((map[checked((int) (bitIndex >> IndexShift))] & (1UL << (int) (bitIndex & WordMask))) != 0)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Sets a specific bit</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set([Positive] long bitIndex)
		{
			Set(this.Words.Span, bitIndex);
		}

		/// <summary>Sets a specific bit</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Set(Span<ulong> map, [Positive] long bitIndex)
		{
			//note: null check and index bound check will be done by the BCL for us
			map[checked((int) (bitIndex >> IndexShift))] |= 1UL << (int) (bitIndex & WordMask);
		}

		/// <summary>Sets a specific bit</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set([Positive] long bitIndex, long count)
		{
			Contract.DoesNotOverflow(this.Capacity, bitIndex, count);
			Set(this.Words.Span, bitIndex, count);
		}

		/// <summary>Sets a specific bit</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Set(Span<ulong> map, [Positive] long bitIndex, [Positive] long count)
		{
			Contract.Debug.Requires(bitIndex >= 0 && count >= 0);
			long end = checked(bitIndex + count);
			for (long idx = bitIndex; idx < end; idx++)
			{
				//TODO:OPTIMIZE: do not recompute the offset each time!
				map[checked((int) (idx >> IndexShift))] |= 1UL << (int) (idx & WordMask);
			}
		}

		/// <summary>Sets a specific bit, while maintaining a population count</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetAndCount([Positive] long bitIndex, ref long count)
		{
			SetAndCount(this.Words.Span, bitIndex, ref count);
		}

		/// <summary>Sets a specific bit, while maintaining a population count</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SetAndCount(Span<ulong> map, [Positive] long bitIndex, ref long count)
		{
			//TODO: optimized version?
			if (!TestAndSet(map, bitIndex)) ++count;
		}

		/// <summary>Sets a specific bit and return its previous state</summary>
		public bool TestAndSet([Positive] long bitIndex)
		{
			return TestAndSet(this.Words.Span, bitIndex);
		}

		/// <summary>Sets a specific bit and return its previous state</summary>
		public static bool TestAndSet(Span<ulong> map, [Positive] long bitIndex)
		{
			//note: null check and index bound check will be done by the BCL for us
			int idx = checked((int) (bitIndex >> IndexShift));
			ulong val = map[idx];
			ulong m = 1UL << (int) (bitIndex & WordMask);
			map[idx] = val | m;
			return (val & m) != 0;
		}

		/// <summary>Clears a specific bit</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Clear([Positive] long bitIndex)
		{
			//TODO: boundcheck?
			Clear(this.Words.Span, bitIndex);
		}

		/// <summary>Clears a specific bit</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Clear(Span<ulong> map, [Positive] long bitIndex)
		{
			//note: null check and index boundcheck will be done by the BCL for us
			map[checked((int) (bitIndex >> IndexShift))] &= ~(1UL << (int) (bitIndex & WordMask));
		}

		/// <summary>Clears a range of bits</summary>
		public void Clear([Positive] long bitIndex, long count)
		{
			Contract.DoesNotOverflow(this.Capacity, bitIndex, count);
			Clear(this.Words.Span, bitIndex, count);
		}

		/// <summary>Clears a range of bits</summary>
		public static void Clear(Span<ulong> map, [Positive] long bitIndex, [Positive] long count)
		{
			Contract.Debug.Requires(bitIndex >= 0 && count >= 0);
			long end = checked(bitIndex + count);
			for (long idx = bitIndex; idx < end; idx++)
			{
				//TODO:OPTIMIZE: do not recompute the offset each time!
				map[checked((int) (idx >> IndexShift))] &= ~(1UL << (int) (idx & WordMask));
			}
		}

		/// <summary>Clears a specific bit, while maintaining a population count</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ClearAndCount([Positive] long bitIndex, ref long count)
		{
			ClearAndCount(this.Words.Span, bitIndex, ref count);
		}

		/// <summary>Clears a specific bit, while maintaining a population count</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ClearAndCount(Span<ulong> map, [Positive] long bitIndex, ref long count)
		{
			if (TestAndClear(map, bitIndex)) --count;
		}

		/// <summary>Changes the state of a specific bit</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Toggle([Positive] long bitIndex, bool on)
		{
			Toggle(this.Words.Span, bitIndex, on);
		}

		/// <summary>Changes the state of a specific bit</summary>
		public static void Toggle(Span<ulong> map, [Positive] long bitIndex, bool on)
		{
			//note: null check and index bound check will be done by the BCL for us
			if (on)
			{
				map[checked((int) (bitIndex >> IndexShift))] |= 1UL << (int) (bitIndex & WordMask);
			}
			else
			{
				map[checked((int) (bitIndex >> IndexShift))] &= ~(1UL << (int) (bitIndex & WordMask));
			}
		}

		/// <summary>Changes the state of a specific bit, while maintaining a population count</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ToggleAndCount([Positive] long bitIndex, bool on, ref long count)
		{
			ToggleAndCount(this.Words.Span, bitIndex, on, ref count);
		}

		/// <summary>Changes the state of a specific bit, while maintaining a population count</summary>
		public static void ToggleAndCount(Span<ulong> map, [Positive] long bitIndex, bool on, ref long count)
		{
			if (on)
			{
				SetAndCount(map, bitIndex, ref count);
			}
			else
			{
				ClearAndCount(map, bitIndex, ref count);
			}
		}

		/// <summary>Inverts the state of a specific bit, and return its previous state</summary>
		/// <returns>If true, the bit has transitioned from set (1) to cleared (0). If false, the bit has transitioned from cleared (0) to set (1).</returns>
		public bool Flip([Positive] long bitIndex)
		{
			return Flip(this.Words.Span, bitIndex);
		}

		/// <summary>Inverts the state of a specific bit, and return its previous state</summary>
		/// <returns>If true, the bit has transitioned from set (1) to cleared (0). If false, the bit has transitioned from cleared (0) to set (1).</returns>
		public static bool Flip(Span<ulong> map, [Positive] long bitIndex)
		{
			//note: null check and index bound check will be done by the BCL for us
			int idx = checked((int) (bitIndex >> IndexShift));
			ulong val = map[idx];
			ulong m = 1UL << (int)(bitIndex & WordMask);
			bool wasSet = (val & m) != 0;
			if (wasSet)
			{
				 val &= ~m;
			}
			else
			{
				val |= m;
			}
			map[idx] = val;
			return wasSet;
		}

		/// <summary>Clears a specific bit and return its previous state</summary>
		/// <returns>If true, the bit was set (1). If false, the bit was already cleared (0).</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TestAndClear([Positive] long bitIndex)
		{
			return TestAndClear(this.Words.Span, bitIndex);
		}

		/// <summary>Clears a specific bit and return its previous state</summary>
		/// <returns>If true, the bit was set (1). If false, the bit was already cleared (0).</returns>
		public static bool TestAndClear(Span<ulong> map, [Positive] long bitIndex)
		{
			//note: null check and index boundcheck will be done by the BCL for us
			int idx = checked((int) (bitIndex >> IndexShift));
			ulong val = map[idx];
			ulong m = 1UL << (int)(bitIndex & WordMask);
			map[idx] = val & ~m;
			return (val & m) != 0;
		}

		/// <summary>Finds the index of the first set bit starting at a specific position in the map, or a negative value if the range contains all 0 until the end</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public long FindNext(long start)
		{
			return FindNext(this.Words.Span, this.Capacity, start, this.Capacity);
		}

		/// <summary>Find the index of the first set bit in the specified range of the map, or a negative value if the range contains all 0</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public long FindNext(long start, long endExclusive)
		{
			return FindNext(this.Words.Span, this.Capacity, start, endExclusive);
		}

		/// <summary>Find the index of the first set bit in the specified range of the map, or a negative value if the range contains all 0</summary>
		[Pure]
		public static long FindNext(ReadOnlySpan<ulong> map, long capacity, long start, long endExclusive)
		{
			if ((ulong) start > (ulong) capacity) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(start));
			if (endExclusive < 0) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(endExclusive));

			if (endExclusive > capacity) endExclusive = capacity;
			if (capacity == 0 || start >= endExclusive) return -1L;
			unsafe
			{
				fixed (ulong* ptr = map)
				{
					return FindNextUnsafe(ptr, start, endExclusive);
				}
			}
		}

		/// <summary>Find the index of the first set bit in the specified range of the map, or a negative value if the range contains all 0</summary>
		/// <param name="map">Pointer to the first word of the bitmap</param>
		/// <param name="start">Bit index of the first position to scan</param>
		/// <param name="endExclusive">Bit index of the position where the scan ends</param>
		[Pure]
		public static unsafe long FindNextUnsafe(ulong* map, [Positive] long start, [Positive] long endExclusive)
		{
			Contract.Debug.Requires(map != null && start >= 0 & endExclusive >= 0);
			if (endExclusive <= start) return -1;

			// look in the first word
			long p = start >> IndexShift;
			long last = endExclusive >> IndexShift; // may be outside the buffer if endExclusive == SIZE

			// look at the first word (which may be the last one)
			ulong m = ~((1UL << (int) (start & WordMask)) - 1);
			if (p == last)
			{ // we are looking at a single word, se we need to mask out any extra bits
				m &= (1UL << (int) (endExclusive & WordMask)) - 1;
			}
			ulong w = map[p] & m;
			if (w != 0) return (p << IndexShift) + BitOperations.TrailingZeroCount(w);
			++p;

			// look at the intermediate words (if any)
			while (p < last)
			{
				w = map[p]; // no masking needed
				if (w != 0) return (p << IndexShift) + BitOperations.TrailingZeroCount(w);
				++p;
			}

			// look at the last word (if it is incomplete)
			if (p < endExclusive)
			{
				m = (1UL << (int) (endExclusive & WordMask)) - 1;
				//note: if m == 0, then we read a byte for nothing, but skip a branch
				w = map[p] & m;
				if (w != 0) return (p << IndexShift) + BitOperations.TrailingZeroCount(w);
			}
			return -1;
		}

		#region Enumerable<bool>...

		/// <summary>Capacity of this bitmap</summary>
		int IReadOnlyCollection<bool>.Count => checked((int)this.Capacity);

		/// <summary>Returns an array of <see cref="bool"/> for each bit in this map</summary>
		[Pure]
		public bool[] ToArray()
		{
			return ToArray(this.Words);
		}

		/// <summary>Returns an array of <see cref="bool"/> for each bit in a buffer</summary>
		[Pure]
		public static bool[] ToArray(ReadOnlyMemory<ulong> map)
		{
			if (map.Length == 0) return [ ];
			unsafe
			{
				var bits = new bool[checked(map.Length * WordSize)];
				using (var it = new BitEnumerator(map))
				{
					fixed (bool* buf = &bits[0])
					{
						bool* ptr = buf;
						while (it.MoveNext()) *ptr++ = it.Current;
					}
				}
				return bits;
			}
		}

		/// <summary>Enumerates the indexes of all the set bits in this map</summary>
		[Pure]
		public BitIndexEnumerable GetSetBits()
		{
			return new BitIndexEnumerable(this.Words, this.Capacity);
		}

		/// <summary>Enumerates all the bits in this map</summary>
		[Pure]
		public BitEnumerator GetEnumerator() => new(this.Words);

		IEnumerator<bool>  IEnumerable<bool>.GetEnumerator() => new BitEnumerator(this.Words);

		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <summary>Enumerator for the content of a <see cref="BitMap64"/></summary>
		public struct BitEnumerator : IEnumerator<bool>
		{
			private long Index;
			private ulong Word;
			private ReadOnlyMemory<ulong> Map;

			internal BitEnumerator(ReadOnlyMemory<ulong> words)
			{
				this.Map = words;
				this.Word = 0;
				this.Index = -1;
				this.Current = false;
			}

			/// <inheritdoc />
			public void Dispose()
			{
				this.Map = default;
			}

			/// <inheritdoc />
			public bool MoveNext()
			{
				var idx = this.Index + 1;
				int m = (int) (idx & WordMask);
				if (m != 0)
				{
					this.Current = (this.Word & (1UL << m)) != 0;
					this.Index = idx;
					return true;
				}
				// new word started
				return MoveNextRare(idx);
			}

			private bool MoveNextRare(long idx)
			{
				int m = (int)(idx >> IndexShift);
				if (m >= this.Map.Length)
				{
					return false;
				}
				var w = this.Map.Span[m];
				this.Current = (w & 1) != 0;
				this.Word = w;
				this.Index = idx;
				return true;
			}

			/// <inheritdoc />
			public void Reset()
			{
				this.Index = -1;
				this.Word = 0;
				this.Current = false;
			}

			/// <inheritdoc />
			public bool Current { get; private set; }

			object IEnumerator.Current => this.Current;
		}

		/// <summary>List of the indexes of all the set bits in a <see cref="BitMap64"/></summary>
		public struct BitIndexEnumerable : IEnumerator<long>, IEnumerable<long>
		{
			private long Index;
			private readonly Memory<ulong> Map;
			private long Capacity;

			internal BitIndexEnumerable(Memory<ulong> words, long capacity)
			{
				this.Index = 0;
				this.Map = words;
				this.Capacity = capacity;
			}

			/// <summary>Returns an enumerator for this list</summary>
			public BitIndexEnumerable GetEnumerator()
			{
				return new BitIndexEnumerable(this.Map, this.Capacity);
			}

			IEnumerator<long> IEnumerable<long>.GetEnumerator()
			{
				return GetEnumerator();
			}
			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}

			/// <inheritdoc />
			public void Dispose()
			{
				this.Index = -2;
				this.Capacity = 0;
			}

			/// <inheritdoc />
			public bool MoveNext()
			{
				var idx = this.Index + 1;
				if (idx < 0 || (idx = FindNext(this.Map.Span, this.Capacity, idx, this.Capacity)) < 0)
				{
					this.Index = -2;
					return false;
				}
				this.Index = idx;
				return true;
			}

			/// <inheritdoc />
			public void Reset()
			{
				this.Index = -1;
			}

			/// <inheritdoc />
			public long Current => this.Index;

			object IEnumerator.Current => this.Current;

		}

		#endregion

	}

}
