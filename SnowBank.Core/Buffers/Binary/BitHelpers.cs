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

namespace SnowBank.Buffers.Binary
{
	using System.Numerics;

	/// <summary>Helper methods to work with bits</summary>
	[PublicAPI]
	[DebuggerNonUserCode]
	public static class BitHelpers
	{

		#region Power of Twos

		/// <summary>Round a number to the next power of 2</summary>
		/// <param name="x">Positive integer that will be rounded up (if not already a power of 2)</param>
		/// <returns>Smallest power of 2 that is greater than or equal to <paramref name="x"/></returns>
		/// <remarks>Will return 1 for <paramref name="x"/> = 0 (because 0 is not a power of 2 !), and will throw for <paramref name="x"/> &lt; 0</remarks>
		/// <exception cref="System.ArgumentOutOfRangeException">If <paramref name="x"/> is greater than 2^31 and would overflow</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint NextPowerOfTwo(uint x)
		{
#if NET6_0_OR_GREATER
			return x == 0 ? 1
				: x < ((1U << 31) + 1) ? BitOperations.RoundUpToPowerOf2(x)
				: throw UnsafeHelpers.Errors.PowerOfTwoOverflow();
#else
			// cf http://en.wikipedia.org/wiki/Power_of_two#Algorithm_to_round_up_to_power_of_two

			// special cases
			if (x == 0) return 1;
			if (x > (1U << 31)) throw UnsafeHelpers.Errors.PowerOfTwoOverflow();

			--x;
			x |= (x >> 1);
			x |= (x >> 2);
			x |= (x >> 4);
			x |= (x >> 8);
			x |= (x >> 16);
			return x + 1;
#endif
		}

		/// <summary>Round a number to the next power of 2</summary>
		/// <param name="x">Positive integer that will be rounded up (if not already a power of 2)</param>
		/// <returns>Smallest power of 2 that is greater than or equal to <paramref name="x"/></returns>
		/// <remarks>Will return 1 for <paramref name="x"/> = 0 (because 0 is not a power 2 !), and will throw for <paramref name="x"/> &lt; 0</remarks>
		/// <exception cref="System.ArgumentOutOfRangeException">If <paramref name="x"/> is negative, or it is greater than 2^30 and would overflow.</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int NextPowerOfTwo(int x)
		{
#if NET6_0_OR_GREATER
			return x == 0 ? 1
				: unchecked((uint) x) <= (1U << 30) ? unchecked((int) BitOperations.RoundUpToPowerOf2((uint) x))
				: throw UnsafeHelpers.Errors.PowerOfTwoOverflow();
#else
			// cf http://en.wikipedia.org/wiki/Power_of_two#Algorithm_to_round_up_to_power_of_two

			// special cases
			if (x == 0) return 1;
			if ((uint)x > (1U << 30)) throw UnsafeHelpers.Errors.PowerOfTwoNegative();

			--x;
			x |= (x >> 1);
			x |= (x >> 2);
			x |= (x >> 4);
			x |= (x >> 8);
			x |= (x >> 16);
			return x + 1;
#endif
		}

		/// <summary>Round a number to the next power of 2</summary>
		/// <param name="x">Positive integer that will be rounded up (if not already a power of 2)</param>
		/// <returns>Smallest power of 2 that is greater than or equal to <paramref name="x"/></returns>
		/// <remarks>Will return 1 for <paramref name="x"/> = 0 (because 0 is not a power of 2 !), and will throw for <paramref name="x"/> &lt; 0</remarks>
		/// <exception cref="System.ArgumentOutOfRangeException">If <paramref name="x"/> is greater than 2^63 and would overflow</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong NextPowerOfTwo(ulong x)
		{
#if NET6_0_OR_GREATER
			return x == 0 ? 1
				: x <= (1UL << 63) ? BitOperations.RoundUpToPowerOf2(x)
				: throw UnsafeHelpers.Errors.PowerOfTwoOverflow();
#else
			// cf http://en.wikipedia.org/wiki/Power_of_two#Algorithm_to_round_up_to_power_of_two

			// special cases
			if (x == 0) return 1;
			if (x > (1UL << 63)) throw UnsafeHelpers.Errors.PowerOfTwoOverflow();

			--x;
			x |= (x >> 1);
			x |= (x >> 2);
			x |= (x >> 4);
			x |= (x >> 8);
			x |= (x >> 16);
			x |= (x >> 32);
			return x + 1;
#endif
		}

		/// <summary>Round a number to the next power of 2</summary>
		/// <param name="x">Positive integer that will be rounded up (if not already a power of 2)</param>
		/// <returns>Smallest power of 2 that is greater than or equal to <paramref name="x"/></returns>
		/// <remarks>Will return 1 for <paramref name="x"/> = 0 (because 0 is not a power 2 !), and will throw for <paramref name="x"/> &lt; 0</remarks>
		/// <exception cref="System.ArgumentOutOfRangeException">If <paramref name="x"/> is negative, or it is greater than 2^62 and would overflow.</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long NextPowerOfTwo(long x)
		{
#if NET6_0_OR_GREATER
			return x == 0 ? 1
				: unchecked((ulong) x) <= (1UL << 62) ? unchecked((long) BitOperations.RoundUpToPowerOf2((ulong) x))
				: throw UnsafeHelpers.Errors.PowerOfTwoOverflow();
#else
			// cf http://en.wikipedia.org/wiki/Power_of_two#Algorithm_to_round_up_to_power_of_two

			// special cases
			if (x == 0) return 1;
			if ((ulong) x > (1UL << 62)) throw UnsafeHelpers.Errors.PowerOfTwoNegative();

			--x;
			x |= (x >> 1);
			x |= (x >> 2);
			x |= (x >> 4);
			x |= (x >> 8);
			x |= (x >> 16);
			x |= (x >> 32);
			return x + 1;
#endif
		}

		/// <summary>Test if a number is a power of 2</summary>
		/// <returns>True if <paramref name="x"/> is expressible as 2^i (i>=0)</returns>
		/// <remarks>0 is NOT considered to be a power of 2</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsPowerOfTwo(int x)
		{
#if NET6_0_OR_GREATER
			return x > 0 && BitOperations.IsPow2(unchecked((uint) x));
#else
			return x > 0 & unchecked((x & (x - 1)) == 0);
#endif
		}

		/// <summary>Test if a number is a power of 2</summary>
		/// <returns>True if <paramref name="x"/> is expressible as 2^i (i>=0)</returns>
		/// <remarks>
		/// <para>0 is NOT considered to be a power of 2.</para>
		/// <para>This method guarantees that IsPowerOfTwo(x) == (NextPowerOfTwo(x) == x)</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsPowerOfTwo(uint x)
		{
#if NET6_0_OR_GREATER
			return x > 0 && BitOperations.IsPow2(x);
#else
			return x != 0 & unchecked((x & (x - 1)) == 0);
#endif
		}

		/// <summary>Test if a number is a power of 2</summary>
		/// <returns>True if <paramref name="x"/> is expressible as 2^i (i>=0)</returns>
		/// <remarks>0 is NOT considered to be a power of 2</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsPowerOfTwo(long x)
		{
#if NET6_0_OR_GREATER
			return x > 0 && BitOperations.IsPow2(unchecked((ulong) x));
#else
			return x > 0 & unchecked((x & (x - 1)) == 0);
#endif
		}

		/// <summary>Test if a number is a power of 2</summary>
		/// <returns>True if <paramref name="x"/> is expressible as 2^i (i>=0)</returns>
		/// <remarks>0 is NOT considered to be a power of 2</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsPowerOfTwo(ulong x)
		{
#if NET6_0_OR_GREATER
			return x > 0 && BitOperations.IsPow2(x);
#else
			return x != 0 & unchecked((x & (x - 1)) == 0);
#endif
		}

		#endregion

		#region Alignment / Padding...

		//REVIEW: align/padding should probably be moved somewhere else because it does not really have anything to do bith bit twiddling...

		/// <summary>Round a size to a multiple of a specific value</summary>
		/// <param name="size">Minimum size required</param>
		/// <param name="alignment">Final size must be a multiple of this number</param>
		/// <param name="minimum">Result cannot be less than this value</param>
		/// <returns>Size rounded up to the next multiple of <paramref name="alignment"/>, or 0 if <paramref name="size"/> is negative</returns>
		/// <remarks>For alignments that are powers of two, <see cref="AlignPowerOfTwo(int,int)"/> will be faster</remarks>
		/// <exception cref="System.OverflowException">If the rounded size overflows over 2 GB</exception>
		[Pure]
		public static int Align(int size, [Positive] int alignment, int minimum = 0)
		{
			Paranoid.Requires(alignment > 0);
			long x = Math.Max(size, minimum);
			x += alignment - 1;
			x /= alignment;
			x *= alignment;
			return checked((int) x);
		}

		/// <summary>Round a size to a multiple of power of two</summary>
		/// <param name="size">Minimum size required</param>
		/// <param name="powerOfTwo">Must be a power two</param>
		/// <returns>Size rounded up to the next multiple of <paramref name="powerOfTwo"/></returns>
		/// <exception cref="System.OverflowException">If the rounded size overflows over 2 GB</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int AlignPowerOfTwo(int size, [PowerOfTwo] int powerOfTwo = 16)
		{
			Paranoid.Requires(BitOperations.IsPow2(powerOfTwo));
			if (size <= 0)
			{
				return size < 0 ? 0 : powerOfTwo;
			}
			int mask = powerOfTwo - 1;
			// force an exception if we overflow above 2GB
			return checked(size + mask) & ~mask;
		}

		/// <summary>Round a size to a multiple of a specific value</summary>
		/// <param name="size">Minimum size required</param>
		/// <param name="alignment">Final size must be a multiple of this number</param>
		/// <param name="minimum">Result cannot be less than this value</param>
		/// <returns>Size rounded up to the next multiple of <paramref name="alignment"/>.</returns>
		/// <remarks>
		/// For alignments that are powers of two, <see cref="AlignPowerOfTwo(uint,uint)"/> will be faster.
		/// </remarks>
		/// <exception cref="System.OverflowException">If the rounded size overflows over 2 GB</exception>
		[Pure]
		public static uint Align(uint size, uint alignment, uint minimum = 0)
		{
			Paranoid.Requires(alignment > 0);
			ulong x = Math.Max(size, minimum);
			x += alignment - 1;
			x /= alignment;
			x *= alignment;
			return checked((uint) x);
		}

		/// <summary>Round a size to a multiple of power of two</summary>
		/// <param name="size">Minimum size required</param>
		/// <param name="powerOfTwo">Must be a power two</param>
		/// <returns>Size rounded up to the next multiple of <paramref name="powerOfTwo"/></returns>
		/// <exception cref="System.OverflowException">If the rounded size overflows over 4 GB</exception>
		[Pure]
		public static uint AlignPowerOfTwo(uint size, [PowerOfTwo] uint powerOfTwo = 16U)
		{
			Paranoid.Requires(BitHelpers.IsPowerOfTwo(powerOfTwo));
			if (size == 0) return powerOfTwo;
			uint mask = powerOfTwo - 1;
			// force an exception if we overflow above 4GB
			return checked(size + mask) & ~mask;
		}

		/// <summary>Round a size to a multiple of a specific value</summary>
		/// <param name="size">Minimum size required</param>
		/// <param name="alignment">Final size must be a multiple of this number</param>
		/// <param name="minimum">Result cannot be less than this value</param>
		/// <returns>Size rounded up to the next multiple of <paramref name="alignment"/>, or 0 if <paramref name="size"/> is negative</returns>
		/// <remarks>For alignments that are powers of two, <see cref="AlignPowerOfTwo(long,long)"/> will be faster</remarks>
		/// <exception cref="System.OverflowException">If the rounded size overflows over 2^63</exception>
		[Pure]
		public static long Align(long size, [Positive] long alignment, long minimum = 0)
		{
			Paranoid.Requires(alignment > 0);
			long x = Math.Max(size, minimum);
			// we have to divide first and check the modulo, because adding (alignment+1) before could overflow at the wrong time
			long y = x /alignment;
			if (x % alignment != 0) ++y;
			return checked(y * alignment);
		}

		/// <summary>Round a size to a multiple of power of two</summary>
		/// <param name="size">Minimum size required</param>
		/// <param name="powerOfTwo">Must be a power two</param>
		/// <returns>Size rounded up to the next multiple of <paramref name="powerOfTwo"/></returns>
		/// <exception cref="System.OverflowException">If the rounded size overflows over long.MaxValue</exception>
		[Pure]
		public static long AlignPowerOfTwo(long size, [PowerOfTwo] long powerOfTwo = 16L)
		{
			Paranoid.Requires(BitHelpers.IsPowerOfTwo(powerOfTwo));
			if (size <= 0)
			{
				return size < 0 ? 0 : powerOfTwo;
			}
			// force an exception if we overflow above ulong.MaxValue
			long mask = powerOfTwo - 1;
			return checked(size + mask) & ~mask;
		}

		/// <summary>Round a size to a multiple of a specific value</summary>
		/// <param name="size">Minimum size required</param>
		/// <param name="alignment">Final size must be a multiple of this number</param>
		/// <param name="minimum">Result cannot be less than this value</param>
		/// <returns>Size rounded up to the next multiple of <paramref name="alignment"/>.</returns>
		/// <remarks>
		/// For alignments that are powers of two, <see cref="AlignPowerOfTwo(ulong,ulong)"/> will be faster.
		/// </remarks>
		/// <exception cref="System.OverflowException">If the rounded size overflows over 2^63</exception>
		[Pure]
		public static ulong Align(ulong size, ulong alignment, ulong minimum = 0)
		{
			Paranoid.Requires(alignment > 0);
			ulong x = Math.Max(size, minimum);
			// we have to divide first and check the modulo, because adding (alignment+1) before could overflow at the wrong time
			ulong y = x / alignment;
			if (x % alignment != 0) ++y;
			return checked(y * alignment);
		}

		/// <summary>Round a size to a multiple of power of two</summary>
		/// <param name="size">Minimum size required</param>
		/// <param name="powerOfTwo">Must be a power two</param>
		/// <returns>Size rounded up to the next multiple of <paramref name="powerOfTwo"/></returns>
		/// <exception cref="System.OverflowException">If the rounded size overflows over ulong.MaxValue</exception>
		[Pure]
		public static ulong AlignPowerOfTwo(ulong size, [PowerOfTwo] ulong powerOfTwo = 16UL)
		{
			Paranoid.Requires(BitHelpers.IsPowerOfTwo(powerOfTwo));
			if (size == 0)
			{
				return powerOfTwo;
			}
			// force an exception if we overflow above ulong.MaxValue
			ulong mask = powerOfTwo - 1;
			return checked(size + mask) & ~mask;
		}

		/// <summary>Computes the number of padding bytes needed to align a buffer to a specific alignment</summary>
		/// <param name="size">Size of the buffer</param>
		/// <param name="powerOfTwo">Alignement required (must be a power of two)</param>
		/// <returns>Number of padding bytes required to end up with a buffer size multiple of <paramref name="powerOfTwo"/>. Returns 0 if the buffer is already aligned</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int PaddingPowerOfTwo(int size, [PowerOfTwo] int powerOfTwo = 16)
		{
			Paranoid.Requires(BitHelpers.IsPowerOfTwo(powerOfTwo));
			return (~size + 1) & (powerOfTwo - 1);

		}

		/// <summary>Computes the number of padding bytes needed to align a buffer to a specific alignment</summary>
		/// <param name="size">Size of the buffer</param>
		/// <param name="powerOfTwo">Alignement required (must be a power of two)</param>
		/// <returns>Number of padding bytes required to end up with a buffer size multiple of <paramref name="powerOfTwo"/>. Returns 0 if the buffer is already aligned</returns>
		/// <remarks>Result is unspecified if <paramref name="powerOfTwo"/> is 0 or not a power of 2</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint PaddingPowerOfTwo(uint size, [PowerOfTwo] uint powerOfTwo = 16)
		{
			Paranoid.Requires(BitHelpers.IsPowerOfTwo(powerOfTwo));
			return (~size + 1) & (powerOfTwo - 1);
		}

		/// <summary>Computes the number of padding bytes needed to align a buffer to a specific alignment</summary>
		/// <param name="size">Size of the buffer</param>
		/// <param name="powerOfTwo">Alignement required (must be a power of two)</param>
		/// <returns>Number of padding bytes required to end up with a buffer size multiple of <paramref name="powerOfTwo"/>. Returns 0 if the buffer is already aligned</returns>
		/// <remarks>Result is unspecified if <paramref name="powerOfTwo"/> is 0 or not a power of 2</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long PaddingPowerOfTwo(long size, [PowerOfTwo] long powerOfTwo = 16)
		{
			Paranoid.Requires(BitHelpers.IsPowerOfTwo(powerOfTwo));
			return (~size + 1) & (powerOfTwo - 1);

		}

		/// <summary>Computes the number of padding bytes needed to align a buffer to a specific alignment</summary>
		/// <param name="size">Size of the buffer</param>
		/// <param name="powerOfTwo">Alignement required (must be a power of two)</param>
		/// <returns>Number of padding bytes required to end up with a buffer size multiple of <paramref name="powerOfTwo"/>. Returns 0 if the buffer is already aligned</returns>
		/// <remarks>Result is unspecified if <paramref name="powerOfTwo"/> is 0 or not a power of 2</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong PaddingPowerOfTwo(ulong size, [PowerOfTwo] ulong powerOfTwo = 16)
		{
			Paranoid.Requires(BitHelpers.IsPowerOfTwo(powerOfTwo));
			return (~size + 1) & (powerOfTwo - 1);
		}

#endregion

		#region CountBits...

		// CountBits(x) == POPCNT == number of bits that are set to 1 in a word
		// - CountBits(0) == 0
		// - CountBits(8) == 1
		// - CountBits(42) == 3
		// - CountBits(uint.MaxValue) == 32

		/// <summary>Count the number of bits set to 1 in a 32-bit signed integer</summary>
		/// <returns>Value between 0 and 32</returns>
		[Pure] //REVIEW: force inline or not?
		public static int CountBits(int value)
		{
#if NET6_0_OR_GREATER
			return BitOperations.PopCount(unchecked((uint) value));
#else
			// cf https://graphics.stanford.edu/~seander/bithacks.html#CountBitsSet64
			// PERF: this averages ~2ns/op OnMyMachine(tm)
			value = value - ((value >> 1) & 0x55555555);
			value = (value & 0x33333333) + ((value >> 2) & 0x33333333);
			value = ((value + (value >> 4) & 0xF0F0F0F) * 0x1010101) >> (32 - 8);
			return value;
#endif
		}

		/// <summary>Count the number of bits set to 1 in a 32-bit unsigned integer</summary>
		/// <returns>Value between 0 and 32</returns>
		[Pure] //REVIEW: force inline or not?
		public static int CountBits(uint value)
		{
#if NET6_0_OR_GREATER
			return BitOperations.PopCount(value);
#else
			// cf https://graphics.stanford.edu/~seander/bithacks.html#CountBitsSet64
			// PERF: this averages ~2ns/op OnMyMachine(tm)
			value = value - ((value >> 1) & 0x55555555);
			value = (value & 0x33333333) + ((value >> 2) & 0x33333333);
			value = ((value + (value >> 4) & 0xF0F0F0F) * 0x1010101) >> (32 - 8);
			return (int) value;
#endif
		}

		/// <summary>Count the number of bits set to 1 in a 64-bit signed integer</summary>
		/// <returns>Value between 0 and 64</returns>
		[Pure] //REVIEW: force inline or not?
		public static int CountBits(long value)
		{
#if NET6_0_OR_GREATER
			return BitOperations.PopCount(unchecked((ulong) value));
#else
			// cf https://graphics.stanford.edu/~seander/bithacks.html#CountBitsSet64
			// PERF: this averages ~2.5ns/op OnMyMachine(tm)
			value = value - ((value >> 1) & 0x5555555555555555);
			value = (value & 0x3333333333333333) + ((value >> 2) & 0x3333333333333333);
			value = ((value + (value >> 4) & 0x0F0F0F0F0F0F0F0F) * 0x0101010101010101) >> (64 - 8);
			return (int) value;
#endif
		}

		/// <summary>Count the number of bits set to 1 in a 32-bit unsigned integer</summary>
		/// <returns>Value between 0 and 64</returns>
		[Pure] //REVIEW: force inline or not?
		public static int CountBits(ulong value)
		{
#if NET6_0_OR_GREATER
			return BitOperations.PopCount(value);
#else
			// cf https://graphics.stanford.edu/~seander/bithacks.html#CountBitsSet64
			// PERF: this averages ~2.5ns/op OnMyMachine(tm)
			value = value - ((value >> 1) & 0x5555555555555555);
			value = (value & 0x3333333333333333) + ((value >> 2) & 0x3333333333333333);
			value = ((value + (value >> 4) & 0x0F0F0F0F0F0F0F0F) * 0x0101010101010101) >> (64 - 8);
			return (int) value;
#endif
		}

		#endregion

		#region MostSignificantBit...

		// MostSignificantBit(x) == Highest bit index (0..63) of the first bit set to 1
		// - MostSignificantBit(1) == 0
		// - MostSignificantBit(8) == 3
		// - MostSignificantBit(42) == 5
		// - MostSignificantBit(uint.MaxValue) == 31
		// Remark: if the value can be 0, the convention is to return to the word size (32 or 64)
		// - MostSignificantBit(default(uint)) == 32
		// - MostSignificantBit(default(ulong)) == 64
		// MostSignificantBitNonZeroXX(x) is a no-branch variant which is undefined for x == 0

#if !NET6_0_OR_GREATER

		private static readonly int[] MultiplyDeBruijnBitPosition32 =
		[
			0, 9, 1, 10, 13, 21, 2, 29, 11, 14, 16, 18, 22, 25, 3, 30,
			8, 12, 20, 28, 15, 17, 24, 7, 19, 27, 23, 6, 26, 5, 4, 31
		];

		private static readonly int[] MultiplyDeBruijnBitPosition64 =
		[
			63,  0, 58,  1, 59, 47, 53,  2,
			60, 39, 48, 27, 54, 33, 42,  3,
			61, 51, 37, 40, 49, 18, 28, 20,
			55, 30, 34, 11, 43, 14, 22,  4,
			62, 57, 46, 52, 38, 26, 32, 41,
			50, 36, 17, 19, 29, 10, 13, 21,
			56, 45, 25, 31, 35, 16,  9, 12,
			44, 24, 15,  8, 23,  7,  6,  5
		];
#endif

		/// <summary>Return the position of the highest bit that is set</summary>
		/// <returns>Value between 0 and 32</returns>
		/// <remarks>
		/// Result is 32 if <paramref name="v"/> is 0.
		/// If the value of <paramref name="v"/> is known to be non-zero, then you can call <see cref="MostSignificantBitNonZero32"/> directly.
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int MostSignificantBit(int v)
		{
#if NET6_0_OR_GREATER
			return v == 0 ? 32 : BitOperations.Log2(unchecked((uint) v));
#else
			return v == 0 ? 32 : MostSignificantBitNonZero32((uint) v);
#endif
		}

		/// <summary>Return the position of the highest bit that is set</summary>
		/// <returns>Value between 0 and 32</returns>
		/// <remarks>
		/// Result is 32 if <paramref name="v"/> is 0.
		/// If the value of <paramref name="v"/> is known to be non-zero, then you can call <see cref="MostSignificantBitNonZero32"/> directly.
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int MostSignificantBit(uint v)
		{
#if NET6_0_OR_GREATER
			return v == 0 ? 32 : BitOperations.Log2(v);
#else
			return v == 0 ? 32 : MostSignificantBitNonZero32(v);
#endif
		}

		/// <summary>Return the position of the highest bit that is set</summary>
		/// <remarks>Result is unspecified if <paramref name="v"/> is 0.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int MostSignificantBitNonZero32(uint v)
		{
#if NET6_0_OR_GREATER
			return BitOperations.Log2(0);
#else
			// from: http://graphics.stanford.edu/~seander/bithacks.html#IntegerLogDeBruijn
			v |= v >> 1; // first round down to one less than a power of 2
			v |= v >> 2;
			v |= v >> 4;
			v |= v >> 8;
			v |= v >> 16;

			var r = (v * 0x07C4ACDDU) >> 27;
			return MultiplyDeBruijnBitPosition32[r & 31];
#endif
		}

		/// <summary>Return the position of the highest bit that is set</summary>
		/// <returns>Value between 0 and 64</returns>
		/// <remarks>
		/// Result is 64 if <paramref name="v"/> is 0.
		/// If the value of <paramref name="v"/> is known to be non-zero, then you can call <see cref="MostSignificantBitNonZero64"/> directly.
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int MostSignificantBit(long v)
		{
#if NET6_0_OR_GREATER
			return v == 0 ? 64 : BitOperations.Log2(unchecked((ulong) v));
#else
			return v == 0 ? 64 : MostSignificantBitNonZero64((ulong) v);
#endif
		}

		/// <summary>Return the position of the highest bit that is set</summary>
		/// <returns>Value between 0 and 64</returns>
		/// <remarks>
		/// Result is 64 if <paramref name="v"/> is zero.
		/// If the value of <paramref name="v"/> is known to be non-zero, then you can call <see cref="MostSignificantBitNonZero64"/> directly.
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int MostSignificantBit(ulong v)
		{
#if NET6_0_OR_GREATER
			return v == 0 ? 64 : BitOperations.Log2(v);
#else
			return v == 0 ? 64 : MostSignificantBitNonZero64(v);
#endif
		}

		/// <summary>Return the position of the highest bit that is set</summary>
		/// <remarks>Result is unspecified if <paramref name="nonZero"/> is 0.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int MostSignificantBitNonZero64(ulong nonZero)
		{
#if NET6_0_OR_GREATER
			return BitOperations.Log2(nonZero);
#else
			ulong v = nonZero;
			v |= v >> 1;
			v |= v >> 2;
			v |= v >> 4;
			v |= v >> 8;
			v |= v >> 16;
			v |= v >> 32;

			var r = ((v - (v >> 1)) * 0x07EDD5E59A4E28C2UL) >> 58;
			return MultiplyDeBruijnBitPosition64[r & 63];
#endif
		}

		#endregion

		#region LeastSignificantBit...

		// LeastSignificantBit(x) == Smallest bit index (0..63) of the first bit set to 1
		// - LeastSignificantBit(1) == 0
		// - LeastSignificantBit(8) == 3
		// - LeastSignificantBit(42) == 2
		// - LeastSignificantBit(uint.MaxValue) = 0
		// Remark: if the value is 0, the convention is to return to the word size (32 or 64)
		// - LeastSignificantBit(default(uint)) == 32
		// - LeastSignificantBit(default(ulong)) == 64
		// LeastSignificantBitNonZeroXX(x) is a no-branch variant which is undefined for x == 0

		/// <summary>Return the position of the lowest bit that is set</summary>
		/// <returns>Value between 0 and 32</returns>
		/// <remarks>Result is 32 if <paramref name="v"/> is 0</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int LeastSignificantBit(int v)
		{
#if NET6_0_OR_GREATER
			return BitOperations.TrailingZeroCount(unchecked((uint) v));
#else
			return v == 0 ? 32 : LeastSignificantBitNonZero32(v);
#endif
		}

		/// <summary>Return the position of the lowest bit that is set</summary>
		/// <returns>Value between 0 and 32</returns>
		/// <remarks>Result is 32 if <paramref name="v"/> is 0</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int LeastSignificantBit(uint v)
		{
#if NET6_0_OR_GREATER
			return BitOperations.TrailingZeroCount(v);
#else
			return v == 0 ? 32 : LeastSignificantBitNonZero32(v);
#endif
		}

		/// <summary>Return the position of the lowest bit that is set</summary>
		/// <remarks>Result is unspecified if <paramref name="nonZero"/> is 0</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int LeastSignificantBitNonZero32(long nonZero)
		{
#if NET6_0_OR_GREATER
			return BitOperations.TrailingZeroCount(unchecked((ulong) nonZero));
#else
			// This solution does not have any branch, but conversion to float may not be fast enough on some architecture...
			//PERF: this averages 2.5ns/op OnMyMachine()
			unsafe
			{
				//note: nonZero must be a long, because -int.MaxValue would overflow on 32-bit
				var d = (float) (nonZero & -nonZero);
				return (int) (((*(uint*) &d) >> 23) - 0x7f);
				//note: this returns -127 if w == 0, which is "negative"
			}
#endif
		}

		/// <summary>Return the position of the lowest bit that is set</summary>
		/// <returns>Value between 0 and 64</returns>
		/// <remarks>Result is 64 if <paramref name="v"/> is 0</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int LeastSignificantBit(ulong v)
		{
#if NET6_0_OR_GREATER
			return BitOperations.TrailingZeroCount(v);
#else
			return v == 0 ? 64 : LeastSignificantBitNonZero64((long) v);
#endif
		}

		/// <summary>Return the position of the lowest bit that is set</summary>
		/// <returns>Value between 0 and 64</returns>
		/// <remarks>Result is 64 if <paramref name="v"/> is 0</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int LeastSignificantBit(long v)
		{
#if NET6_0_OR_GREATER
			return BitOperations.TrailingZeroCount(unchecked((ulong) v));
#else
			return v == 0 ? 64 : LeastSignificantBitNonZero64(v);
#endif
		}

		/// <summary>Return the position of the lowest bit that is set</summary>
		/// <remarks>Result is unspecified if <paramref name="nonZero"/> is 0</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int LeastSignificantBitNonZero64(ulong nonZero)
		{
#if NET6_0_OR_GREATER
			return BitOperations.TrailingZeroCount(nonZero);
#else
			return LeastSignificantBitNonZero64(unchecked((long) nonZero));
#endif
		}

		/// <summary>Return the position of the lowest bit that is set</summary>
		/// <remarks>Result is unspecified if <paramref name="nonZero"/> is 0</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int LeastSignificantBitNonZero64(long nonZero)
		{
#if NET6_0_OR_GREATER
			return BitOperations.TrailingZeroCount(unchecked((ulong) nonZero));
#else
			// This solution does not have any branch, but conversion to double may not be fast enough on some architecture...
			//PERF: this averages 2.5ns/op OnMyMachine()
			unsafe
			{
				// isolated LS1B to double
				var d = (double)(nonZero & -nonZero);
				// exponent is in bits 52 to 62 (11 bits)
				ulong l = *((ulong*)&d);
				ulong exp = (l >> 52) & ((1 << 11) - 1);
				return (int)(exp - 1023);
				//note: this returns -1023 if w == 0, which is "negative"
			}
#endif
		}

		#endregion

		#region FirstNonZeroByte...

		// FirstNonZeroByte(x) == offset of the first byte in a multibyte word, that has at least one bit set to 1
		// - FirstNonZeroByte(0x000042) == 0
		// - FirstNonZeroByte(0x004200) == 1
		// - FirstNonZeroByte(0x004201) == 0
		// - FirstNonZeroByte(0x420000) == 2
		// - FirstNonZeroByte(0x420001) == 0
		// Remark: if the value is 0, the convention is to return to the word size in bytes (4 or 8)
		// - FirstNonZeroByte(default(uint)) == 4
		// - FirstNonZeroByte(default(ulong)) == 8

		/// <summary>Return the offset of the first non-zero byte</summary>
		/// <returns>Value between 0 and 4</returns>
		/// <remarks>Returns 4 if <paramref name="v"/> is 0</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int FirstNonZeroByte(int v)
		{
			return v == 0 ? 4 : (LeastSignificantBitNonZero32(v) >> 3);
		}

		/// <summary>Return the offset of the first non-zero byte</summary>
		/// <returns>Value between 0 and 4</returns>
		/// <remarks>Returns 4 if <paramref name="v"/> is 0</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int FirstNonZeroByte(uint v)
		{
			return v == 0 ? 4 : (LeastSignificantBitNonZero32((int) v) >> 3);
		}

		/// <summary>Return the offset of the first non-zero byte</summary>
		/// <returns>Value between 0 and 8</returns>
		/// <remarks>Returns 8 if <paramref name="v"/> is 0</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int FirstNonZeroByte(long v)
		{
			return v == 0 ? 8 : (LeastSignificantBitNonZero64(v) >> 3);
		}

		/// <summary>Return the offset of the first non-zero byte</summary>
		/// <returns>Value between 0 and 8</returns>
		/// <remarks>Returns 8 if <paramref name="v"/> is 0</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int FirstNonZeroByte(ulong v)
		{
			return v == 0 ? 8 : (LeastSignificantBitNonZero64((long) v) >> 3);
		}

		#endregion

		#region LastNonZeroByte...

		// LastNonZeroByte(x) == offset of the first byte in a multibyte word, that has at least one bit set to 1
		// - LastNonZeroByte(0x000042) == 0
		// - LastNonZeroByte(0x004200) == 1
		// - LastNonZeroByte(0x004201) == 1
		// - LastNonZeroByte(0x420000) == 2
		// - LastNonZeroByte(0x420001) == 2
		// Remark: if the value is 0, the convention is to return to the word size in bytes (4 or 8)
		// - LastNonZeroByte(default(uint)) == 4
		// - LastNonZeroByte(default(ulong)) == 8

		/// <summary>Return the offset of the last non-zero byte</summary>
		/// <remarks>Returns 4 if <paramref name="v"/> is 0</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int LastNonZeroByte(int v)
		{
			return v == 0 ? 4 : (MostSignificantBitNonZero32((uint) v) >> 3);
		}

		/// <summary>Return the offset of the last non-zero byte</summary>
		/// <remarks>Returns 4 if <paramref name="v"/> is 0</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int LastNonZeroByte(uint v)
		{
			return v == 0 ? 4 : (MostSignificantBitNonZero32(v) >> 3);
		}

		/// <summary>Return the offset of the last non-zero byte</summary>
		/// <remarks>Returns 8 if <paramref name="v"/> is 0</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int LastNonZeroByte(long v)
		{
			return v == 0 ? 8 : (MostSignificantBitNonZero64((ulong) v) >> 3);
		}

		/// <summary>Return the offset of the last non-zero byte</summary>
		/// <remarks>Returns 8 if <paramref name="v"/> is 0</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int LastNonZeroByte(ulong v)
		{
			return v == 0 ? 8 : (MostSignificantBitNonZero64(v) >> 3);
		}

		#endregion

		#region RotL/RotR...

		/// <summary>Rotate bits to the left (ROTL)</summary>
		/// <example>RotL32(0x12345678, 4) = 0x23456781</example>
		/// <remarks>Equivalent of the 'rotl' CRT function, or the 'ROL' x86 instruction</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint RotL32(uint x, int n)
		{
#if NET6_0_OR_GREATER
			return BitOperations.RotateLeft(x, n);
#else
			return (x << n) | (x >> (32 - n));
#endif
		}

		/// <summary>Rotate bits to the right (ROTR)</summary>
		/// <example>RotR32(0x12345678, 4) = 0x81234567</example>
		/// <remarks>Equivalent of the 'rotr' CRT function, or the 'ROR' x86 instruction</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint RotR32(uint x, int n)
		{
#if NET6_0_OR_GREATER
			return BitOperations.RotateRight(x, n);
#else
			return (x >> n) | (x << (32 - n));
#endif
		}

		/// <summary>Rotate bits to the left (ROTL64)</summary>
		/// <example>RotL64(0x0123456789ABCDEF, 4) = 0x123456789ABCDEF0</example>
		/// <remarks>Equivalent of the '_rotl64' CRT function, or the 'ROL' x64 instruction</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong RotL64(ulong x, int n)
		{
#if NET6_0_OR_GREATER
			return BitOperations.RotateLeft(x, n);
#else
			return (x << n) | (x >> (64 - n));
#endif
		}

		/// <summary>Rotate bits to the right (ROTR64)</summary>
		/// <example>RotR64(0x0123456789ABCDEF, 4) = 0xF0123456789ABCDE</example>
		/// <remarks>Equivalent of the '_rotr64' CRT function, or the 'ROR' x64 instruction</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong RotR64(ulong x, int n)
		{
#if NET6_0_OR_GREATER
			return BitOperations.RotateRight(x, n);
#else
			return (x >> n) | (x << (64 - n));
#endif
		}

		#endregion

	}

}
