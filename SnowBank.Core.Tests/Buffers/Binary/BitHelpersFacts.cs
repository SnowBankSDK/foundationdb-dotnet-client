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

namespace SnowBank.Buffers.Binary.Tests
{

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class BitHelpersFacts : SimpleTest
	{

		[Test]
		public void Test_BitHelpers_NextPowerOfTwo()
		{
			// signed

			// 32

			// 0 is a special case, to simplify bugger handling logic
			Assert.That(BitHelpers.NextPowerOfTwo(0), Is.EqualTo(1), "Special case for 0");
			Assert.That(BitHelpers.NextPowerOfTwo(1), Is.EqualTo(1));
			Assert.That(BitHelpers.NextPowerOfTwo(2), Is.EqualTo(2));

			for (int i = 2; i < 30; i++)
			{
				Assert.That(BitHelpers.NextPowerOfTwo((1 << i) - 1), Is.EqualTo(1 << i));
				Assert.That(BitHelpers.NextPowerOfTwo(1 << i), Is.EqualTo(1 << i));
			}

			Assert.That(() => BitHelpers.NextPowerOfTwo((1 << 30) + 1), Throws.InstanceOf<OverflowException>(), "Overflow signed 32 bits");
			Assert.That(() => BitHelpers.NextPowerOfTwo(-1), Throws.InstanceOf<OverflowException>(), "Negative signed 32 bits");
			Assert.That(() => BitHelpers.NextPowerOfTwo(-42), Throws.InstanceOf<OverflowException>(), "Negative signed 32 bits");

			// 64

			// 0 is a special case, to simplify bugger handling logic
			Assert.That(BitHelpers.NextPowerOfTwo(0L), Is.EqualTo(1), "Special case for 0");
			Assert.That(BitHelpers.NextPowerOfTwo(1L), Is.EqualTo(1));
			Assert.That(BitHelpers.NextPowerOfTwo(2L), Is.EqualTo(2));

			for (int i = 2; i < 60; i++)
			{
				Assert.That(BitHelpers.NextPowerOfTwo((1L << i) - 1L), Is.EqualTo(1L << i));
				Assert.That(BitHelpers.NextPowerOfTwo(1L << i), Is.EqualTo(1L << i));
			}

			Assert.That(() => BitHelpers.NextPowerOfTwo((1L << 62) + 1L), Throws.InstanceOf<OverflowException>(), "Overflow signed 64 bits");
			Assert.That(() => BitHelpers.NextPowerOfTwo(-1L), Throws.InstanceOf<OverflowException>(), "Negative signed 64 bits");
			Assert.That(() => BitHelpers.NextPowerOfTwo(-42L), Throws.InstanceOf<OverflowException>(), "Negative signed 64 bits");

			// unsigned

			//32

			// 0 is a special case, to simplify bugger handling logic
			Assert.That(BitHelpers.NextPowerOfTwo(0U), Is.EqualTo(1U), "Special case for 0");
			Assert.That(BitHelpers.NextPowerOfTwo(1U), Is.EqualTo(1U));
			Assert.That(BitHelpers.NextPowerOfTwo(2U), Is.EqualTo(2U));

			for (int i = 2; i < 31; i++)
			{
				Assert.That(BitHelpers.NextPowerOfTwo((1U << i) - 1), Is.EqualTo(1U << i));
				Assert.That(BitHelpers.NextPowerOfTwo(1U << i), Is.EqualTo(1U << i));
			}

			Assert.That(() => BitHelpers.NextPowerOfTwo((1U << 31) + 1), Throws.InstanceOf<OverflowException>());

			//64

			// 0 is a special case, to simplify bugger handling logic
			Assert.That(BitHelpers.NextPowerOfTwo(0UL), Is.EqualTo(1UL), "Special case for 0");
			Assert.That(BitHelpers.NextPowerOfTwo(1UL), Is.EqualTo(1UL));
			Assert.That(BitHelpers.NextPowerOfTwo(2UL), Is.EqualTo(2UL));

			for (int i = 2; i < 63; i++)
			{
				Assert.That(BitHelpers.NextPowerOfTwo((1UL << i) - 1UL), Is.EqualTo(1UL << i));
				Assert.That(BitHelpers.NextPowerOfTwo(1UL << i), Is.EqualTo(1UL << i));
			}

			Assert.That(() => BitHelpers.NextPowerOfTwo((1UL << 63) + 1), Throws.InstanceOf<OverflowException>());

		}

		[Test]
		public void Test_BitHelpers_IsPowerOfTwo()
		{
			// signed

			Assert.That(BitHelpers.IsPowerOfTwo(0), Is.False, "0 is NOT a power of 2");
			Assert.That(BitHelpers.IsPowerOfTwo(1), Is.True, "1 == 2^0");
			Assert.That(BitHelpers.IsPowerOfTwo(2), Is.True, "2 == 2^1");
			for (int i = 2; i < 30; i++)
			{
				Assert.That(BitHelpers.IsPowerOfTwo((1 << i) - 1), Is.False, $"2^{i} - 1 is NOT a power of two");
				Assert.That(BitHelpers.IsPowerOfTwo(1 << i), Is.True, $"2^{i} IS a power of two");
				Assert.That(BitHelpers.IsPowerOfTwo((1 << i) + 1), Is.False, $"2^{i} + 1 is NOT a power of two");
			}
			Assert.That(BitHelpers.IsPowerOfTwo(int.MaxValue), Is.False);
			Assert.That(BitHelpers.IsPowerOfTwo(-1), Is.False);
			Assert.That(BitHelpers.IsPowerOfTwo(-2), Is.False);
			Assert.That(BitHelpers.IsPowerOfTwo(-42), Is.False);
			Assert.That(BitHelpers.IsPowerOfTwo(int.MinValue), Is.False);

			Assert.That(BitHelpers.IsPowerOfTwo(0L), Is.False, "0 is NOT a power of 2");
			Assert.That(BitHelpers.IsPowerOfTwo(1L), Is.True, "1 == 2^0");
			Assert.That(BitHelpers.IsPowerOfTwo(2L), Is.True, "2 == 2^1");
			for (int i = 2; i < 62; i++)
			{
				Assert.That(BitHelpers.IsPowerOfTwo((1L << i) - 1), Is.False, $"2^{i} - 1 is NOT a power of two");
				Assert.That(BitHelpers.IsPowerOfTwo(1L << i), Is.True, $"2^{i} IS a power of two");
				Assert.That(BitHelpers.IsPowerOfTwo((1L << i) + 1), Is.False, $"2^{i} + 1 is NOT a power of two");
			}
			Assert.That(BitHelpers.IsPowerOfTwo(long.MaxValue), Is.False);
			Assert.That(BitHelpers.IsPowerOfTwo((long)int.MaxValue), Is.False);
			Assert.That(BitHelpers.IsPowerOfTwo(-1L), Is.False);
			Assert.That(BitHelpers.IsPowerOfTwo(-2L), Is.False);
			Assert.That(BitHelpers.IsPowerOfTwo(-42L), Is.False);
			Assert.That(BitHelpers.IsPowerOfTwo((long)int.MinValue), Is.False);
			Assert.That(BitHelpers.IsPowerOfTwo(long.MinValue), Is.False);

			// unsigned

			// 0 is a special case, to simplify bugger handling logic
			Assert.That(BitHelpers.IsPowerOfTwo(0U), Is.False, "0 is not a power of 2");
			Assert.That(BitHelpers.IsPowerOfTwo(1U), Is.True, "1 == 2^0");
			Assert.That(BitHelpers.IsPowerOfTwo(2U), Is.True, "2 == 2^1");
			for (int i = 2; i < 31; i++)
			{
				Assert.That(BitHelpers.IsPowerOfTwo((1U << i) - 1), Is.False, $"2^{i} - 1 is NOT a power of two");
				Assert.That(BitHelpers.IsPowerOfTwo(1U << i), Is.True, $"2^{i} is a power of two");
				Assert.That(BitHelpers.IsPowerOfTwo((1U << i) + 1), Is.False, $"2^{i} + 1 is NOT a power of two");
			}
			Assert.That(BitHelpers.IsPowerOfTwo(uint.MaxValue), Is.False);

			Assert.That(BitHelpers.IsPowerOfTwo(0UL), Is.False, "0 is not a power of 2");
			Assert.That(BitHelpers.IsPowerOfTwo(1UL), Is.True, "1 == 2^0");
			Assert.That(BitHelpers.IsPowerOfTwo(2UL), Is.True, "2 == 2^1");
			for (int i = 2; i < 62; i++)
			{
				Assert.That(BitHelpers.IsPowerOfTwo((1UL << i) - 1), Is.False, $"2^{i} - 1 is NOT a power of two");
				Assert.That(BitHelpers.IsPowerOfTwo(1UL << i), Is.True, $"2^{i} is a power of two");
				Assert.That(BitHelpers.IsPowerOfTwo((1UL << i) + 1), Is.False, $"2^{i} + 1 is NOT a power of two");
			}
			Assert.That(BitHelpers.IsPowerOfTwo((ulong)uint.MaxValue), Is.False);
			Assert.That(BitHelpers.IsPowerOfTwo(ulong.MaxValue), Is.False);
		}

		[Test]
		public void Test_BitHelpers_Align()
		{
			{ // int32

				Assert.That(BitHelpers.Align(124, 5), Is.EqualTo(125));
				Assert.That(BitHelpers.Align(125, 5), Is.EqualTo(125));
				Assert.That(BitHelpers.Align(126, 5), Is.EqualTo(130));
				Assert.That(BitHelpers.Align(0, 5), Is.EqualTo(0), "Align(0, *) should return 0");
				Assert.That(BitHelpers.Align(1, 5), Is.EqualTo(5), "Align(1 <= X <= B, B) should return B");
				Assert.That(BitHelpers.Align(-123, 5), Is.EqualTo(0), "Align(X < 0, *) should return 0");
				// with minimum specified
				Assert.That(BitHelpers.Align(0, 5, 15), Is.EqualTo(15), "Align(X < MIN, *, MIN) should return MIN");
				Assert.That(BitHelpers.Align(0, 5, 12), Is.EqualTo(15), "Align(X < MIN, B, MIN) should return Align(MIN, B)");
				// edge cases
				Assert.That(() => BitHelpers.Align(int.MaxValue, 5), Throws.InstanceOf<OverflowException>());
				Assert.That(() => BitHelpers.Align(int.MaxValue - 1, 5), Throws.InstanceOf<OverflowException>());
				Assert.That(BitHelpers.Align(int.MaxValue, 1), Is.EqualTo(int.MaxValue));
				Assert.That(BitHelpers.Align(int.MaxValue, int.MaxValue), Is.EqualTo(int.MaxValue));
			}
			{ // uint32

				Assert.That(BitHelpers.Align(124U, 5), Is.EqualTo(125));
				Assert.That(BitHelpers.Align(125U, 5), Is.EqualTo(125));
				Assert.That(BitHelpers.Align(126U, 5), Is.EqualTo(130));
				Assert.That(BitHelpers.Align(0U, 5), Is.EqualTo(0), "Align(0, *) should return 0");
				Assert.That(BitHelpers.Align(1U, 5), Is.EqualTo(5), "Align(1 <= X <= B, B) should return B");
				// with minimum specified
				Assert.That(BitHelpers.Align(0U, 5, 15), Is.EqualTo(15), "Align(X < MIN, *, MIN) should return MIN");
				Assert.That(BitHelpers.Align(0U, 5, 12), Is.EqualTo(15), "Align(X < MIN, B, MIN) should return Align(MIN, B)");
				// edge cases
				Assert.That(() => BitHelpers.Align(uint.MaxValue, 7), Throws.InstanceOf<OverflowException>());
				Assert.That(() => BitHelpers.Align(uint.MaxValue - 2, 7), Throws.InstanceOf<OverflowException>());
				Assert.That(BitHelpers.Align(uint.MaxValue, 1), Is.EqualTo(uint.MaxValue));
				Assert.That(BitHelpers.Align(uint.MaxValue, uint.MaxValue), Is.EqualTo(uint.MaxValue));
			}
			{ // int64

				Assert.That(BitHelpers.Align(124L, 5), Is.EqualTo(125L));
				Assert.That(BitHelpers.Align(125L, 5), Is.EqualTo(125L));
				Assert.That(BitHelpers.Align(126L, 5), Is.EqualTo(130L));
				Assert.That(BitHelpers.Align(0L, 5), Is.EqualTo(0L), "Align(0, *) should return 0");
				Assert.That(BitHelpers.Align(1L, 5), Is.EqualTo(5L), "Align(1 <= X <= B, B) should return B");
				Assert.That(BitHelpers.Align(-123L, 5), Is.EqualTo(0L), "Align(X < 0, *) should return 0");
				// with minimum specified
				Assert.That(BitHelpers.Align(0L, 5, 15), Is.EqualTo(15L), "Align(X < MIN, *, MIN) should return MIN");
				Assert.That(BitHelpers.Align(0L, 5, 12), Is.EqualTo(15L), "Align(X < MIN, B, MIN) should return Align(MIN, B)");
				// edge cases
				Assert.That(() => BitHelpers.Align(long.MaxValue, 5), Throws.InstanceOf<OverflowException>());
				Assert.That(() => BitHelpers.Align(long.MaxValue - 1, 5), Throws.InstanceOf<OverflowException>());
				Assert.That(BitHelpers.Align(long.MaxValue, 1), Is.EqualTo(long.MaxValue));
				Assert.That(BitHelpers.Align(long.MaxValue, long.MaxValue), Is.EqualTo(long.MaxValue));
			}
			{ // uint64

				Assert.That(BitHelpers.Align(124UL, 5), Is.EqualTo(125L));
				Assert.That(BitHelpers.Align(125UL, 5), Is.EqualTo(125L));
				Assert.That(BitHelpers.Align(126UL, 5), Is.EqualTo(130L));
				Assert.That(BitHelpers.Align(0UL, 5), Is.EqualTo(0L), "Align(0, *) should return 0");
				Assert.That(BitHelpers.Align(1UL, 5), Is.EqualTo(5L), "Align(1 <= X <= B, B) should return B");
				// with minimum specified
				Assert.That(BitHelpers.Align(0UL, 5, 15), Is.EqualTo(15L), "Align(X < MIN, *, MIN) should return MIN");
				Assert.That(BitHelpers.Align(0UL, 5, 12), Is.EqualTo(15L), "Align(X < MIN, B, MIN) should return Align(MIN, B)");
				// edge cases
				Assert.That(() => BitHelpers.Align(ulong.MaxValue, 13), Throws.InstanceOf<OverflowException>());
				Assert.That(() => BitHelpers.Align(ulong.MaxValue - 1, 13), Throws.InstanceOf<OverflowException>());
				Assert.That(BitHelpers.Align(ulong.MaxValue, 1), Is.EqualTo(ulong.MaxValue));
				Assert.That(BitHelpers.Align(ulong.MaxValue, ulong.MaxValue), Is.EqualTo(ulong.MaxValue));
			}
		}

		[Test]
		public void Test_BitHelpers_AlignPowerOfTwo()
		{
			// Even though 0 is a multiple of 16, it is always rounded up to 16 to simplify buffer handling logic
			Assert.That(BitHelpers.AlignPowerOfTwo(0), Is.EqualTo(16));
			Assert.That(BitHelpers.AlignPowerOfTwo(0L), Is.EqualTo(16));
			// 1..16 => 16
			for (int i = 1; i <= 16; i++) { Assert.That(BitHelpers.AlignPowerOfTwo(i), Is.EqualTo(16), $"Align({i}) => 16"); }
			for (int i = 1; i <= 16; i++) { Assert.That(BitHelpers.AlignPowerOfTwo((long) i), Is.EqualTo(16), $"Align({i}) => 16"); }
			// 17..32 => 32
			for (int i = 17; i <= 32; i++) { Assert.That(BitHelpers.AlignPowerOfTwo(i), Is.EqualTo(32), $"Align({i}) => 32"); }
			for (int i = 17; i <= 32; i++) { Assert.That(BitHelpers.AlignPowerOfTwo((long) i), Is.EqualTo(32), $"Align({i}) => 32"); }
			// 33..48 => 48
			for (int i = 33; i <= 48; i++) { Assert.That(BitHelpers.AlignPowerOfTwo(i), Is.EqualTo(48), $"Align({i}) => 48"); }
			for (int i = 33; i <= 48; i++) { Assert.That(BitHelpers.AlignPowerOfTwo((long) i), Is.EqualTo(48), $"Align({i}) => 48"); }

			// 2^N-1
			for (int i = 6; i < 30; i++)
			{
				Assert.That(BitHelpers.AlignPowerOfTwo((1 << i) - 1), Is.EqualTo(1 << i));
				Assert.That(BitHelpers.AlignPowerOfTwo((long) ((1 << i) - 1)), Is.EqualTo(1 << i));
			}
			// largest non overflowing
			Assert.That(() => BitHelpers.AlignPowerOfTwo(int.MaxValue - 15), Is.EqualTo((int.MaxValue - 15)));
			Assert.That(() => BitHelpers.AlignPowerOfTwo(long.MaxValue - 15), Is.EqualTo((long.MaxValue - 15)));

			// overflow
			Assert.That(() => BitHelpers.AlignPowerOfTwo(int.MaxValue), Throws.InstanceOf<OverflowException>());
			Assert.That(() => BitHelpers.AlignPowerOfTwo(int.MaxValue - 14), Throws.InstanceOf<OverflowException>());
			Assert.That(() => BitHelpers.AlignPowerOfTwo(long.MaxValue), Throws.InstanceOf<OverflowException>());
			Assert.That(() => BitHelpers.AlignPowerOfTwo(long.MaxValue - 14), Throws.InstanceOf<OverflowException>());

			// negative values
			//NOTE: to speed things up, we don't throw on negative values an return the minimum alignment instead
			// => if this really becomes an issue, we may revisit this
			//Assert.That(() => MemoryHelpers.Align(-1), Throws.InstanceOf<ArgumentException>());
			//Assert.That(() => MemoryHelpers.Align(int.MinValue), Throws.InstanceOf<ArgumentException>());
			Assert.That(BitHelpers.AlignPowerOfTwo(-1), Is.EqualTo(0));
			Assert.That(BitHelpers.AlignPowerOfTwo(int.MinValue), Is.EqualTo(0));
			Assert.That(BitHelpers.AlignPowerOfTwo(-1L), Is.EqualTo(0));
			Assert.That(BitHelpers.AlignPowerOfTwo(long.MinValue), Is.EqualTo(0));

			// with custom alignment

			Assert.That(BitHelpers.AlignPowerOfTwo(7, 8), Is.EqualTo(8));
			Assert.That(BitHelpers.AlignPowerOfTwo(7L, 8), Is.EqualTo(8L));
			Assert.That(BitHelpers.AlignPowerOfTwo(7U, 8), Is.EqualTo(8U));
			Assert.That(BitHelpers.AlignPowerOfTwo(7UL, 8), Is.EqualTo(8UL));

			Assert.That(BitHelpers.AlignPowerOfTwo(8, 8), Is.EqualTo(8));
			Assert.That(BitHelpers.AlignPowerOfTwo(8L, 8), Is.EqualTo(8L));
			Assert.That(BitHelpers.AlignPowerOfTwo(8U, 8), Is.EqualTo(8U));
			Assert.That(BitHelpers.AlignPowerOfTwo(8UL, 8), Is.EqualTo(8UL));

			Assert.That(BitHelpers.AlignPowerOfTwo(9, 8), Is.EqualTo(16));
			Assert.That(BitHelpers.AlignPowerOfTwo(9L, 8), Is.EqualTo(16L));
			Assert.That(BitHelpers.AlignPowerOfTwo(9U, 8), Is.EqualTo(16U));
			Assert.That(BitHelpers.AlignPowerOfTwo(9UL, 8), Is.EqualTo(16UL));

			Assert.That(BitHelpers.AlignPowerOfTwo(7, 16), Is.EqualTo(16));
			Assert.That(BitHelpers.AlignPowerOfTwo(16, 16), Is.EqualTo(16));
			Assert.That(BitHelpers.AlignPowerOfTwo(17, 16), Is.EqualTo(32));

			Assert.That(BitHelpers.AlignPowerOfTwo(7, 32), Is.EqualTo(32));
			Assert.That(BitHelpers.AlignPowerOfTwo(32, 32), Is.EqualTo(32));
			Assert.That(BitHelpers.AlignPowerOfTwo(33, 32), Is.EqualTo(64));

			Assert.That(BitHelpers.AlignPowerOfTwo(7, 64), Is.EqualTo(64));
			Assert.That(BitHelpers.AlignPowerOfTwo(64, 64), Is.EqualTo(64));
			Assert.That(BitHelpers.AlignPowerOfTwo(65, 64), Is.EqualTo(128));

			// by convention, 0 should be aligned to a full block
			Assert.That(BitHelpers.AlignPowerOfTwo(0, 32), Is.EqualTo(32));
			Assert.That(BitHelpers.AlignPowerOfTwo(0U, 32), Is.EqualTo(32));
			Assert.That(BitHelpers.AlignPowerOfTwo(0L, 32), Is.EqualTo(32));
			Assert.That(BitHelpers.AlignPowerOfTwo(0UL, 32), Is.EqualTo(32));
			// by convention, negative values are clipped to 0
			Assert.That(BitHelpers.AlignPowerOfTwo(-1, 32), Is.EqualTo(0));
			Assert.That(BitHelpers.AlignPowerOfTwo(-123L, 32), Is.EqualTo(0));
		}

		[Test]
		public void Test_BitHelpers_PaddingPowerOfTwo()
		{
			// Default padding size

			Assert.That(BitHelpers.PaddingPowerOfTwo(0), Is.EqualTo(0));
			Assert.That(BitHelpers.PaddingPowerOfTwo(1), Is.EqualTo(15));
			Assert.That(BitHelpers.PaddingPowerOfTwo(2), Is.EqualTo(14));
			Assert.That(BitHelpers.PaddingPowerOfTwo(14), Is.EqualTo(2));
			Assert.That(BitHelpers.PaddingPowerOfTwo(15), Is.EqualTo(1));
			Assert.That(BitHelpers.PaddingPowerOfTwo(16), Is.EqualTo(0));
			Assert.That(BitHelpers.PaddingPowerOfTwo(17), Is.EqualTo(15));

			Assert.That(BitHelpers.PaddingPowerOfTwo(0U), Is.EqualTo(0U));
			Assert.That(BitHelpers.PaddingPowerOfTwo(1U), Is.EqualTo(15U));
			Assert.That(BitHelpers.PaddingPowerOfTwo(2U), Is.EqualTo(14U));
			Assert.That(BitHelpers.PaddingPowerOfTwo(14U), Is.EqualTo(2U));
			Assert.That(BitHelpers.PaddingPowerOfTwo(15U), Is.EqualTo(1U));
			Assert.That(BitHelpers.PaddingPowerOfTwo(16U), Is.EqualTo(0U));
			Assert.That(BitHelpers.PaddingPowerOfTwo(17U), Is.EqualTo(15U));

			Assert.That(BitHelpers.PaddingPowerOfTwo(int.MaxValue), Is.EqualTo(1));
			Assert.That(BitHelpers.PaddingPowerOfTwo(uint.MaxValue), Is.EqualTo(1));

			// Custom padding size

			Assert.That(BitHelpers.PaddingPowerOfTwo(0, 32), Is.EqualTo(0));
			Assert.That(BitHelpers.PaddingPowerOfTwo(1, 32), Is.EqualTo(31));
			Assert.That(BitHelpers.PaddingPowerOfTwo(2, 32), Is.EqualTo(30));
			Assert.That(BitHelpers.PaddingPowerOfTwo(30, 32), Is.EqualTo(2));
			Assert.That(BitHelpers.PaddingPowerOfTwo(31, 32), Is.EqualTo(1));
			Assert.That(BitHelpers.PaddingPowerOfTwo(32, 32), Is.EqualTo(0));
			Assert.That(BitHelpers.PaddingPowerOfTwo(33, 32), Is.EqualTo(31));

			Assert.That(BitHelpers.PaddingPowerOfTwo(0U, 32), Is.EqualTo(0U));
			Assert.That(BitHelpers.PaddingPowerOfTwo(1U, 32), Is.EqualTo(31U));
			Assert.That(BitHelpers.PaddingPowerOfTwo(2U, 32), Is.EqualTo(30U));
			Assert.That(BitHelpers.PaddingPowerOfTwo(30U, 32), Is.EqualTo(2U));
			Assert.That(BitHelpers.PaddingPowerOfTwo(31U, 32), Is.EqualTo(1U));
			Assert.That(BitHelpers.PaddingPowerOfTwo(32U, 32), Is.EqualTo(0U));
			Assert.That(BitHelpers.PaddingPowerOfTwo(33U, 32), Is.EqualTo(31U));

			Assert.That(BitHelpers.PaddingPowerOfTwo(int.MaxValue, 32), Is.EqualTo(1));
			Assert.That(BitHelpers.PaddingPowerOfTwo(uint.MaxValue, 32), Is.EqualTo(1));
		}

		[Test]
		public void Test_BitHelpers_MostSignificantBit()
		{
			// int
			Assert.That(BitHelpers.MostSignificantBit(1), Is.EqualTo(0));
			Assert.That(BitHelpers.MostSignificantBit(2), Is.EqualTo(1));
			Assert.That(BitHelpers.MostSignificantBit(3), Is.EqualTo(1));
			Assert.That(BitHelpers.MostSignificantBit(4), Is.EqualTo(2));
			Assert.That(BitHelpers.MostSignificantBit(42), Is.EqualTo(5));
			Assert.That(BitHelpers.MostSignificantBit(int.MaxValue), Is.EqualTo(30));
			Assert.That(BitHelpers.MostSignificantBit(int.MinValue), Is.EqualTo(31));
			Assert.That(BitHelpers.MostSignificantBit(0), Is.EqualTo(32), "By convention, MSB(0) is the word size!");
			// uint
			Assert.That(BitHelpers.MostSignificantBit(1U), Is.EqualTo(0));
			Assert.That(BitHelpers.MostSignificantBit(2U), Is.EqualTo(1));
			Assert.That(BitHelpers.MostSignificantBit(3U), Is.EqualTo(1));
			Assert.That(BitHelpers.MostSignificantBit(4U), Is.EqualTo(2));
			Assert.That(BitHelpers.MostSignificantBit(42U), Is.EqualTo(5));
			Assert.That(BitHelpers.MostSignificantBit(uint.MaxValue), Is.EqualTo(31));
			Assert.That(BitHelpers.MostSignificantBit(0x80000000U), Is.EqualTo(31));
			Assert.That(BitHelpers.MostSignificantBit(0U), Is.EqualTo(32), "By convention, MSB(0) is the word size!");
			// long
			Assert.That(BitHelpers.MostSignificantBit(1L), Is.EqualTo(0));
			Assert.That(BitHelpers.MostSignificantBit(2L), Is.EqualTo(1));
			Assert.That(BitHelpers.MostSignificantBit(3L), Is.EqualTo(1));
			Assert.That(BitHelpers.MostSignificantBit(4L), Is.EqualTo(2));
			Assert.That(BitHelpers.MostSignificantBit(42L), Is.EqualTo(5));
			Assert.That(BitHelpers.MostSignificantBit(long.MaxValue), Is.EqualTo(62));
			Assert.That(BitHelpers.MostSignificantBit(long.MinValue), Is.EqualTo(63));
			Assert.That(BitHelpers.MostSignificantBit(0L), Is.EqualTo(64), "By convention, MSB(0) is the word size!");
			// ulong
			Assert.That(BitHelpers.MostSignificantBit(1UL), Is.EqualTo(0));
			Assert.That(BitHelpers.MostSignificantBit(2UL), Is.EqualTo(1));
			Assert.That(BitHelpers.MostSignificantBit(3UL), Is.EqualTo(1));
			Assert.That(BitHelpers.MostSignificantBit(4UL), Is.EqualTo(2));
			Assert.That(BitHelpers.MostSignificantBit(42UL), Is.EqualTo(5));
			Assert.That(BitHelpers.MostSignificantBit(ulong.MaxValue), Is.EqualTo(63));
			Assert.That(BitHelpers.MostSignificantBit(0x8000000000000000UL), Is.EqualTo(63));
			Assert.That(BitHelpers.MostSignificantBit(0UL), Is.EqualTo(64), "By convention, MSB(0) is the word size!");
		}

		[Test]
		public void Test_BitHelpers_LeastSignificantBit()
		{
			// int
			Assert.That(BitHelpers.LeastSignificantBit(1), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBit(2), Is.EqualTo(1));
			Assert.That(BitHelpers.LeastSignificantBit(3), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBit(4), Is.EqualTo(2));
			Assert.That(BitHelpers.LeastSignificantBit(42), Is.EqualTo(1));
			Assert.That(BitHelpers.LeastSignificantBit(int.MaxValue), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBit(int.MinValue>>1), Is.EqualTo(30));
			Assert.That(BitHelpers.LeastSignificantBit(int.MinValue), Is.EqualTo(31));
			Assert.That(BitHelpers.LeastSignificantBit(0), Is.EqualTo(32), "By convention, LSB(0) is the word size!");
			// uint
			Assert.That(BitHelpers.LeastSignificantBit(1U), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBit(2U), Is.EqualTo(1));
			Assert.That(BitHelpers.LeastSignificantBit(3U), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBit(4U), Is.EqualTo(2));
			Assert.That(BitHelpers.LeastSignificantBit(42U), Is.EqualTo(1));
			Assert.That(BitHelpers.LeastSignificantBit(uint.MaxValue), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBit(0x80000000U), Is.EqualTo(31));
			Assert.That(BitHelpers.LeastSignificantBit(0U), Is.EqualTo(32), "By convention, LSB(0) is the word size!");
			// long
			Assert.That(BitHelpers.LeastSignificantBit(1L), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBit(2L), Is.EqualTo(1));
			Assert.That(BitHelpers.LeastSignificantBit(3L), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBit(4L), Is.EqualTo(2));
			Assert.That(BitHelpers.LeastSignificantBit(42L), Is.EqualTo(1));
			Assert.That(BitHelpers.LeastSignificantBit(long.MaxValue), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBit(long.MinValue >> 1), Is.EqualTo(62));
			Assert.That(BitHelpers.LeastSignificantBit(long.MinValue), Is.EqualTo(63));
			Assert.That(BitHelpers.LeastSignificantBit(0L), Is.EqualTo(64), "By convention, LSB(0) is the word size!");
			// ulong
			Assert.That(BitHelpers.LeastSignificantBit(1UL), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBit(2UL), Is.EqualTo(1));
			Assert.That(BitHelpers.LeastSignificantBit(3UL), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBit(4UL), Is.EqualTo(2));
			Assert.That(BitHelpers.LeastSignificantBit(42UL), Is.EqualTo(1));
			Assert.That(BitHelpers.LeastSignificantBit(ulong.MaxValue), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBit(0x8000000000000000UL), Is.EqualTo(63));
			Assert.That(BitHelpers.LeastSignificantBit(0UL), Is.EqualTo(64), "By convention, LSB(0) is the word size!");
		}

		[Test]
		public void Test_BitHelpers_LeastSignificantBitNonZero()
		{
			// int
			Assert.That(BitHelpers.LeastSignificantBitNonZero32(1), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBitNonZero32(2), Is.EqualTo(1));
			Assert.That(BitHelpers.LeastSignificantBitNonZero32(3), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBitNonZero32(4), Is.EqualTo(2));
			Assert.That(BitHelpers.LeastSignificantBitNonZero32(42), Is.EqualTo(1));
			Assert.That(BitHelpers.LeastSignificantBitNonZero32(int.MaxValue), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBitNonZero32(int.MinValue >> 1), Is.EqualTo(30));
			Assert.That(BitHelpers.LeastSignificantBitNonZero32(int.MinValue), Is.EqualTo(31));

			// uint
			Assert.That(BitHelpers.LeastSignificantBitNonZero32(1U), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBitNonZero32(2U), Is.EqualTo(1));
			Assert.That(BitHelpers.LeastSignificantBitNonZero32(3U), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBitNonZero32(4U), Is.EqualTo(2));
			Assert.That(BitHelpers.LeastSignificantBitNonZero32(42U), Is.EqualTo(1));
			Assert.That(BitHelpers.LeastSignificantBitNonZero32(uint.MaxValue), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBitNonZero32(0x80000000U), Is.EqualTo(31));

			// long
			Assert.That(BitHelpers.LeastSignificantBitNonZero64(1L), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBitNonZero64(2L), Is.EqualTo(1));
			Assert.That(BitHelpers.LeastSignificantBitNonZero64(3L), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBitNonZero64(4L), Is.EqualTo(2));
			Assert.That(BitHelpers.LeastSignificantBitNonZero64(42L), Is.EqualTo(1));
			Assert.That(BitHelpers.LeastSignificantBitNonZero64(long.MaxValue), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBitNonZero64(long.MinValue >> 1), Is.EqualTo(62));
			Assert.That(BitHelpers.LeastSignificantBitNonZero64(long.MinValue), Is.EqualTo(63));

			// ulong
			Assert.That(BitHelpers.LeastSignificantBitNonZero64(1UL), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBitNonZero64(2UL), Is.EqualTo(1));
			Assert.That(BitHelpers.LeastSignificantBitNonZero64(3UL), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBitNonZero64(4UL), Is.EqualTo(2));
			Assert.That(BitHelpers.LeastSignificantBitNonZero64(42UL), Is.EqualTo(1));
			Assert.That(BitHelpers.LeastSignificantBitNonZero64(ulong.MaxValue), Is.EqualTo(0));
			Assert.That(BitHelpers.LeastSignificantBitNonZero64(0x8000000000000000UL), Is.EqualTo(63));
		}

		[Test]
		public void Test_BitHelpers_CountBits()
		{
			// int
			Assert.That(BitHelpers.CountBits(0), Is.EqualTo(0));
			Assert.That(BitHelpers.CountBits(1), Is.EqualTo(1));
			Assert.That(BitHelpers.CountBits(2), Is.EqualTo(1));
			Assert.That(BitHelpers.CountBits(3), Is.EqualTo(2));
			Assert.That(BitHelpers.CountBits(4), Is.EqualTo(1));
			Assert.That(BitHelpers.CountBits(42), Is.EqualTo(3));
			Assert.That(BitHelpers.CountBits(0x55555555), Is.EqualTo(16));
			Assert.That(BitHelpers.CountBits(0xDEADBEEF), Is.EqualTo(24));
			Assert.That(BitHelpers.CountBits(int.MaxValue), Is.EqualTo(31));
			Assert.That(BitHelpers.CountBits(int.MinValue), Is.EqualTo(1));
			Assert.That(BitHelpers.CountBits(-1), Is.EqualTo(32));
			// uint
			Assert.That(BitHelpers.CountBits(0U), Is.EqualTo(0));
			Assert.That(BitHelpers.CountBits(1U), Is.EqualTo(1));
			Assert.That(BitHelpers.CountBits(2U), Is.EqualTo(1));
			Assert.That(BitHelpers.CountBits(3U), Is.EqualTo(2));
			Assert.That(BitHelpers.CountBits(4U), Is.EqualTo(1));
			Assert.That(BitHelpers.CountBits(42U), Is.EqualTo(3));
			Assert.That(BitHelpers.CountBits(0x55555555U), Is.EqualTo(16));
			Assert.That(BitHelpers.CountBits(0xDEADBEEFU), Is.EqualTo(24));
			Assert.That(BitHelpers.CountBits(0x7FFFFFFFU), Is.EqualTo(31));
			Assert.That(BitHelpers.CountBits(0x80000000U), Is.EqualTo(1));
			Assert.That(BitHelpers.CountBits(uint.MaxValue), Is.EqualTo(32));
			// long
			Assert.That(BitHelpers.CountBits(0L), Is.EqualTo(0));
			Assert.That(BitHelpers.CountBits(1L), Is.EqualTo(1));
			Assert.That(BitHelpers.CountBits(2L), Is.EqualTo(1));
			Assert.That(BitHelpers.CountBits(3L), Is.EqualTo(2));
			Assert.That(BitHelpers.CountBits(4L), Is.EqualTo(1));
			Assert.That(BitHelpers.CountBits(42L), Is.EqualTo(3));
			Assert.That(BitHelpers.CountBits(0x5555555555555555L), Is.EqualTo(32));
			Assert.That(BitHelpers.CountBits(unchecked((long) 0xBADC0FFEE0DDF00DUL)), Is.EqualTo(37));
			Assert.That(BitHelpers.CountBits(long.MaxValue), Is.EqualTo(63));
			Assert.That(BitHelpers.CountBits(long.MinValue), Is.EqualTo(1));
			Assert.That(BitHelpers.CountBits(-1L), Is.EqualTo(64));
			// ulong
			Assert.That(BitHelpers.CountBits(0UL), Is.EqualTo(0));
			Assert.That(BitHelpers.CountBits(1UL), Is.EqualTo(1));
			Assert.That(BitHelpers.CountBits(2UL), Is.EqualTo(1));
			Assert.That(BitHelpers.CountBits(3UL), Is.EqualTo(2));
			Assert.That(BitHelpers.CountBits(4UL), Is.EqualTo(1));
			Assert.That(BitHelpers.CountBits(42UL), Is.EqualTo(3));
			Assert.That(BitHelpers.CountBits(0x5555555555555555UL), Is.EqualTo(32));
			Assert.That(BitHelpers.CountBits(0xBADC0FFEE0DDF00DUL), Is.EqualTo(37));
			Assert.That(BitHelpers.CountBits(0x7FFFFFFFFFFFFFFFUL), Is.EqualTo(63));
			Assert.That(BitHelpers.CountBits(0x8000000000000000UL), Is.EqualTo(1));
			Assert.That(BitHelpers.CountBits(ulong.MaxValue), Is.EqualTo(64));
		}

		[Test]
		public void Test_BitHelpers_RotL()
		{
			Assert.That(BitHelpers.RotL32(0, 17), Is.EqualTo(0U));
			Assert.That(BitHelpers.RotL32(uint.MaxValue, 17), Is.EqualTo(uint.MaxValue));
			Assert.That(BitHelpers.RotL32(0x12345678U, 0), Is.EqualTo(0x12345678U));
			Assert.That(BitHelpers.RotL32(0x12345678U, 1), Is.EqualTo(0x2468ACF0U));
			Assert.That(BitHelpers.RotL32(0x12345678U, 8), Is.EqualTo(0x34567812U));
			Assert.That(BitHelpers.RotL32(0x12345678U, 13), Is.EqualTo(0x8ACF0246U));
			Assert.That(BitHelpers.RotL32(0x12345678U, 16), Is.EqualTo(0x56781234U));
			Assert.That(BitHelpers.RotL32(0x12345678U, 17), Is.EqualTo(0xACF02468U));
			Assert.That(BitHelpers.RotL32(0x12345678U, 31), Is.EqualTo(0x091A2B3CU));
			Assert.That(BitHelpers.RotL32(0x12345678U, 32), Is.EqualTo(0x12345678U));

			Assert.That(BitHelpers.RotL64(0, 17), Is.EqualTo(0UL));
			Assert.That(BitHelpers.RotL64(ulong.MaxValue, 17), Is.EqualTo(ulong.MaxValue));
			Assert.That(BitHelpers.RotL64(0x0123456789ABCDEFUL, 0), Is.EqualTo(0x0123456789ABCDEFUL));
			Assert.That(BitHelpers.RotL64(0x0123456789ABCDEFUL, 1), Is.EqualTo(0x02468ACF13579BDEUL));
			Assert.That(BitHelpers.RotL64(0x0123456789ABCDEFUL, 8), Is.EqualTo(0x23456789ABCDEF01UL));
			Assert.That(BitHelpers.RotL64(0x0123456789ABCDEFUL, 13), Is.EqualTo(0x68ACF13579BDE024UL));
			Assert.That(BitHelpers.RotL64(0x0123456789ABCDEFUL, 16), Is.EqualTo(0x456789ABCDEF0123UL));
			Assert.That(BitHelpers.RotL64(0x0123456789ABCDEFUL, 17), Is.EqualTo(0x8ACF13579BDE0246UL));
			Assert.That(BitHelpers.RotL64(0x0123456789ABCDEFUL, 31), Is.EqualTo(0xC4D5E6F78091A2B3UL));
			Assert.That(BitHelpers.RotL64(0x0123456789ABCDEFUL, 32), Is.EqualTo(0x89ABCDEF01234567UL));
			Assert.That(BitHelpers.RotL64(0x0123456789ABCDEFUL, 40), Is.EqualTo(0xABCDEF0123456789UL));
			Assert.That(BitHelpers.RotL64(0x0123456789ABCDEFUL, 48), Is.EqualTo(0xCDEF0123456789ABUL));
			Assert.That(BitHelpers.RotL64(0x0123456789ABCDEFUL, 56), Is.EqualTo(0xEF0123456789ABCDUL));
			Assert.That(BitHelpers.RotL64(0x0123456789ABCDEFUL, 64), Is.EqualTo(0x0123456789ABCDEFUL));

		}

		[Test]
		public void Test_BitHelpers_RotR()
		{
			Assert.That(BitHelpers.RotR32(0, 17), Is.EqualTo(0U));
			Assert.That(BitHelpers.RotR32(uint.MaxValue, 17), Is.EqualTo(uint.MaxValue));
			Assert.That(BitHelpers.RotR32(0x12345678U, 0), Is.EqualTo(0x12345678U));
			Assert.That(BitHelpers.RotR32(0x12345678U, 1), Is.EqualTo(0x091A2B3CU));
			Assert.That(BitHelpers.RotR32(0x12345678U, 8), Is.EqualTo(0x78123456U));
			Assert.That(BitHelpers.RotR32(0x12345678U, 13), Is.EqualTo(0xB3C091A2U));
			Assert.That(BitHelpers.RotR32(0x12345678U, 16), Is.EqualTo(0x56781234U));
			Assert.That(BitHelpers.RotR32(0x12345678U, 17), Is.EqualTo(0x2B3C091AU));
			Assert.That(BitHelpers.RotR32(0x12345678U, 31), Is.EqualTo(0x2468ACF0U));
			Assert.That(BitHelpers.RotR32(0x12345678U, 32), Is.EqualTo(0x12345678U));

			Assert.That(BitHelpers.RotR64(0, 17), Is.EqualTo(0UL));
			Assert.That(BitHelpers.RotR64(ulong.MaxValue, 17), Is.EqualTo(ulong.MaxValue));
			Assert.That(BitHelpers.RotR64(0x0123456789ABCDEFUL, 0), Is.EqualTo(0x0123456789ABCDEFUL));
			Assert.That(BitHelpers.RotR64(0x0123456789ABCDEFUL, 1), Is.EqualTo(0x8091A2B3C4D5E6F7UL));
			Assert.That(BitHelpers.RotR64(0x0123456789ABCDEFUL, 8), Is.EqualTo(0xEF0123456789ABCDUL));
			Assert.That(BitHelpers.RotR64(0x0123456789ABCDEFUL, 13), Is.EqualTo(0x6F78091A2B3C4D5EUL));
			Assert.That(BitHelpers.RotR64(0x0123456789ABCDEFUL, 16), Is.EqualTo(0xCDEF0123456789ABUL));
			Assert.That(BitHelpers.RotR64(0x0123456789ABCDEFUL, 17), Is.EqualTo(0xE6F78091A2B3C4D5UL));
			Assert.That(BitHelpers.RotR64(0x0123456789ABCDEFUL, 24), Is.EqualTo(0xABCDEF0123456789UL));
			Assert.That(BitHelpers.RotR64(0x0123456789ABCDEFUL, 31), Is.EqualTo(0x13579BDE02468ACFUL));
			Assert.That(BitHelpers.RotR64(0x0123456789ABCDEFUL, 32), Is.EqualTo(0x89ABCDEF01234567UL));
			Assert.That(BitHelpers.RotR64(0x0123456789ABCDEFUL, 40), Is.EqualTo(0x6789ABCDEF012345UL));
			Assert.That(BitHelpers.RotR64(0x0123456789ABCDEFUL, 48), Is.EqualTo(0x456789ABCDEF0123UL));
			Assert.That(BitHelpers.RotR64(0x0123456789ABCDEFUL, 56), Is.EqualTo(0x23456789ABCDEF01UL));
			Assert.That(BitHelpers.RotR64(0x0123456789ABCDEFUL, 64), Is.EqualTo(0x0123456789ABCDEFUL));

		}

	}
}
