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

// ReSharper disable AccessToModifiedClosure
// ReSharper disable UseObjectOrCollectionInitializer
namespace SnowBank.Buffers.Tests
{
	/// <summary>Actor that pushes messages around until a counter reaches 0</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class BitMapFacts : SimpleTest
	{

		[Test]
		public void Test_BitMap64_Set()
		{
			var map = new BitMap64(128);
			Assume.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));
			Assert.That(map.Population(), Is.EqualTo(0));

			map.Set(0);
			Assert.That(map.ToString("B"), Is.EqualTo("10000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));
			Assert.That(map.Population(), Is.EqualTo(1));

			map.Set(7);
			Assert.That(map.ToString("B"), Is.EqualTo("10000001000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));
			Assert.That(map.Population(), Is.EqualTo(2));

			map.Set(8);
			Assert.That(map.ToString("B"), Is.EqualTo("10000001100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));
			Assert.That(map.Population(), Is.EqualTo(3));

			map.Set(42);
			Assert.That(map.ToString("B"), Is.EqualTo("10000001100000000000000000000000000000000010000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));
			Assert.That(map.Population(), Is.EqualTo(4));

			map.Set(63);
			Assert.That(map.ToString("B"), Is.EqualTo("10000001100000000000000000000000000000000010000000000000000000010000000000000000000000000000000000000000000000000000000000000000"));
			Assert.That(map.Population(), Is.EqualTo(5));

			map.Flip(64);
			Assert.That(map.ToString("B"), Is.EqualTo("10000001100000000000000000000000000000000010000000000000000000011000000000000000000000000000000000000000000000000000000000000000"));
			Assert.That(map.Population(), Is.EqualTo(6));

			map[127] = true;
			Assert.That(map.ToString("B"), Is.EqualTo("10000001100000000000000000000000000000000010000000000000000000011000000000000000000000000000000000000000000000000000000000000001"));
			Assert.That(map.Population(), Is.EqualTo(7));

			map.Toggle(126, true);
			Assert.That(map.ToString("B"), Is.EqualTo("10000001100000000000000000000000000000000010000000000000000000011000000000000000000000000000000000000000000000000000000000000011"));
			Assert.That(map.Population(), Is.EqualTo(8));

			map[42] = true; // already set
			Assert.That(map.ToString("B"), Is.EqualTo("10000001100000000000000000000000000000000010000000000000000000011000000000000000000000000000000000000000000000000000000000000011"));
			Assert.That(map.Population(), Is.EqualTo(8));

			Assert.That(() => map.Set(128), Throws.InstanceOf<IndexOutOfRangeException>());
			Assert.That(map.ToString("B"), Is.EqualTo("10000001100000000000000000000000000000000010000000000000000000011000000000000000000000000000000000000000000000000000000000000011"));

			Assert.That(() => map.Set(-1), Throws.InstanceOf<IndexOutOfRangeException>());
			Assert.That(map.ToString("B"), Is.EqualTo("10000001100000000000000000000000000000000010000000000000000000011000000000000000000000000000000000000000000000000000000000000011"));

			Assert.That(map.ToString("X"), Is.EqualTo("8000040000000181C000000000000001"));
		}

		[Test]
		public void Test_BitMap64_Clear()
		{
			const int BITS = 128;
			var map = new BitMap64(BITS);
			map.SetAll();
			Assume.That(map.ToString("B"), Is.EqualTo("11111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111"));
			Assert.That(map.Population(), Is.EqualTo(BITS));

			map.Clear(0);
			Assert.That(map.ToString("B"), Is.EqualTo("01111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111"));
			Assert.That(map.Population(), Is.EqualTo(BITS - 1));

			map.Clear(7);
			Assert.That(map.ToString("B"), Is.EqualTo("01111110111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111"));
			Assert.That(map.Population(), Is.EqualTo(BITS - 2));

			map.Clear(8);
			Assert.That(map.ToString("B"), Is.EqualTo("01111110011111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111"));
			Assert.That(map.Population(), Is.EqualTo(BITS - 3));

			map.Clear(42);
			Assert.That(map.ToString("B"), Is.EqualTo("01111110011111111111111111111111111111111101111111111111111111111111111111111111111111111111111111111111111111111111111111111111"));
			Assert.That(map.Population(), Is.EqualTo(BITS - 4));

			map.Clear(63);
			Assert.That(map.ToString("B"), Is.EqualTo("01111110011111111111111111111111111111111101111111111111111111101111111111111111111111111111111111111111111111111111111111111111"));
			Assert.That(map.Population(), Is.EqualTo(BITS - 5));

			map.Flip(64);
			Assert.That(map.ToString("B"), Is.EqualTo("01111110011111111111111111111111111111111101111111111111111111100111111111111111111111111111111111111111111111111111111111111111"));
			Assert.That(map.Population(), Is.EqualTo(BITS - 6));

			map[127] = false;
			Assert.That(map.ToString("B"), Is.EqualTo("01111110011111111111111111111111111111111101111111111111111111100111111111111111111111111111111111111111111111111111111111111110"));
			Assert.That(map.Population(), Is.EqualTo(BITS - 7));

			map.Toggle(126, false);
			Assert.That(map.ToString("B"), Is.EqualTo("01111110011111111111111111111111111111111101111111111111111111100111111111111111111111111111111111111111111111111111111111111100"));
			Assert.That(map.Population(), Is.EqualTo(BITS - 8));

			map[42] = false; // already cleared
			Assert.That(map.ToString("B"), Is.EqualTo("01111110011111111111111111111111111111111101111111111111111111100111111111111111111111111111111111111111111111111111111111111100"));
			Assert.That(map.Population(), Is.EqualTo(BITS - 8));

			Assert.That(() => map.Set(128), Throws.InstanceOf<IndexOutOfRangeException>());
			Assert.That(map.ToString("B"), Is.EqualTo("01111110011111111111111111111111111111111101111111111111111111100111111111111111111111111111111111111111111111111111111111111100"));

			Assert.That(() => map.Set(-1), Throws.InstanceOf<IndexOutOfRangeException>());
			Assert.That(map.ToString("B"), Is.EqualTo("01111110011111111111111111111111111111111101111111111111111111100111111111111111111111111111111111111111111111111111111111111100"));

			Assert.That(map.ToString("X"), Is.EqualTo("7FFFFBFFFFFFFE7E3FFFFFFFFFFFFFFE"));
		}

		[Test]
		public void Test_BitMap64_Population()
		{
			const int BITS = 64 + 128 + 256;
			var map = new BitMap64(BITS);
			Assert.That(map.Population(), Is.EqualTo(0));
			map[42] = true;
			map[128] = true;
			map[BITS - 1] = true;
			Assert.That(map.Population(), Is.EqualTo(3));

			map.SetAll();
			Assert.That(map.Population(),Is.EqualTo(BITS));

			map.Clear(42);
			Assert.That(map.Population(), Is.EqualTo(BITS - 1));

			map.ClearAll();
			Assert.That(map.Population(), Is.EqualTo(0));
		}

		[Test]
		public void Test_BitMap46_ClearAll_ResetAll()
		{
			const int BITS = 128;

			var map = new BitMap64(BITS);
			map[42] = true;
			map[63] = true;
			Assert.That(map.Population(), Is.EqualTo(2));

			// clear all bits
			map.ClearAll();
			// should be filled with zeroes
			Assert.That(map.ToString("X"), Is.EqualTo("00000000000000000000000000000000"));
			Assert.That(map.Population(), Is.EqualTo(0));
			Assert.That(map.GetLowestBit(), Is.EqualTo(-1L));
			Assert.That(map.GetHighestBit(), Is.EqualTo(-1L));

			// set all bits
			map.SetAll();
			// should be filled with ones
			Assert.That(map.ToString("X"), Is.EqualTo("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"));
			Assert.That(map.Population(), Is.EqualTo(BITS));
			Assert.That(map.GetLowestBit(), Is.EqualTo(0));
			Assert.That(map.GetHighestBit(), Is.EqualTo(BITS - 1));

			map[42] = false;
			Assert.That(map.ToString("X"), Is.EqualTo("FFFFFBFFFFFFFFFFFFFFFFFFFFFFFFFF"));
			Assert.That(map.Population(), Is.EqualTo(BITS - 1));
			Assert.That(map.GetLowestBit(), Is.EqualTo(0));
			Assert.That(map.GetHighestBit(), Is.EqualTo(BITS - 1));
		}

		[Test]
		public void Test_BitMap64_SetClear_And_Count()
		{
			var map = new BitMap64(128);
			long pop = 0;
			Assert.That(map.Population(), Is.EqualTo(0));

			map.SetAndCount(42, ref pop);
			Assume.That(map.GetSetBits().ToArray(), Is.EqualTo(new [] { 42L }));
			Assert.That(pop, Is.EqualTo(1));
			Assert.That(map.Population(), Is.EqualTo(pop));

			map.SetAndCount(63, ref pop);
			Assume.That(map.GetSetBits().ToArray(), Is.EqualTo(new[] { 42L, 63L }));
			Assert.That(pop, Is.EqualTo(2));
			Assert.That(map.Population(), Is.EqualTo(pop));

			// already set
			map.SetAndCount(42, ref pop);
			Assume.That(map.GetSetBits().ToArray(), Is.EqualTo(new[] { 42L, 63L }));
			Assert.That(pop, Is.EqualTo(2));
			Assert.That(map.Population(), Is.EqualTo(pop));

			// out of bounds
			Assert.That(() => map.SetAndCount(256, ref pop), Throws.Exception);
			Assume.That(map.GetSetBits().ToArray(), Is.EqualTo(new[] { 42L, 63L }));
			Assert.That(pop, Is.EqualTo(2));
			Assert.That(map.Population(), Is.EqualTo(pop));

			// remove non existing
			map.ClearAndCount(79, ref pop);
			Assume.That(map.GetSetBits().ToArray(), Is.EqualTo(new[] { 42L, 63L }));
			Assert.That(pop, Is.EqualTo(2));
			Assert.That(map.Population(), Is.EqualTo(pop));

			// remove existing
			map.ClearAndCount(63, ref pop);
			Assume.That(map.GetSetBits().ToArray(), Is.EqualTo(new[] { 42L }));
			Assert.That(pop, Is.EqualTo(1));
			Assert.That(map.Population(), Is.EqualTo(pop));

			// remove existing again
			map.ClearAndCount(63, ref pop);
			Assume.That(map.GetSetBits().ToArray(), Is.EqualTo(new[] { 42L }));
			Assert.That(pop, Is.EqualTo(1));
			Assert.That(map.Population(), Is.EqualTo(pop));

			// remove last
			map.ClearAndCount(42, ref pop);
			Assume.That(map.GetSetBits().ToArray(), Is.EqualTo(Array.Empty<long>()));
			Assert.That(pop, Is.EqualTo(0));
			Assert.That(map.Population(), Is.EqualTo(pop));

			// remove any
			map.ClearAndCount(123, ref pop);
			Assume.That(map.GetSetBits().ToArray(), Is.EqualTo(Array.Empty<long>()));
			Assert.That(pop, Is.EqualTo(0));
			Assert.That(map.Population(), Is.EqualTo(pop));

		}

		[Test]
		public void Test_BitMap64_Lowest_Highest_Bit()
		{
			const int BITS = 128;

			var map = new BitMap64(BITS);
			Assert.That(map.GetLowestBit(), Is.EqualTo(-1));
			Assert.That(map.GetHighestBit(), Is.EqualTo(-1));

			map[42] = true;
			Assert.That(map.GetLowestBit(), Is.EqualTo(42));
			Assert.That(map.GetHighestBit(), Is.EqualTo(42));

			map[63] = true;
			Assert.That(map.GetLowestBit(), Is.EqualTo(42));
			Assert.That(map.GetHighestBit(), Is.EqualTo(63));

			map[BITS - 1] = true;
			Assert.That(map.GetLowestBit(), Is.EqualTo(42));
			Assert.That(map.GetHighestBit(), Is.EqualTo(BITS - 1));

			map[0] = true;
			Assert.That(map.GetLowestBit(), Is.EqualTo(0));
			Assert.That(map.GetHighestBit(), Is.EqualTo(BITS - 1));

			map[BITS - 1] = false;
			Assert.That(map.GetLowestBit(), Is.EqualTo(0));
			Assert.That(map.GetHighestBit(), Is.EqualTo(63));

			map[0] = false;
			Assert.That(map.GetLowestBit(), Is.EqualTo(42));
			Assert.That(map.GetHighestBit(), Is.EqualTo(63));
		}

		[Test]
		public void Test_BitMap64_FindNext()
		{
			const int SIZE = 256;

			// bit map (empty)
			var map = new BitMap64(SIZE);

			void Verify(int start, int end, int expect)
			{
#if DEBUG
				string s = map.ToString("B");
				if (start > 0) s = new string('_', start) + s[start..];
				if (end <= SIZE) s = s[..end] + new string('_', SIZE - end);
				Log("MAP: " + s);
#endif
				var actual = map.FindNext(start, end);
#if DEBUG
				if (expect >= 0)
				{
					Log($"     {new string(' ', expect)}{'^'} {expect} -> {actual}");
				}
				else
				{
					Log($"     <not found> -> {actual}");
				}
#endif
				if (actual != expect)
				{
					Assert.That(actual, Is.EqualTo(expect), $"Invalid result in range {start} <= idx < {end}");
				}
			}

			Verify(0, SIZE, -1);

			map.Set(42);
			Verify(0, SIZE, 42);
			Verify(42, SIZE, 42);
			Verify(0, 43, 42);
			Verify(43, SIZE, -1);
			Verify(0, 42, -1);

			map.Set(40);
			Verify(0, SIZE, 40);
			Verify(40, SIZE, 40);
			Verify(0, 41, 40);
			Verify(0, 43, 40);
			Verify(43, SIZE, -1);
			Verify(0, 40, -1);

			Verify(40, 43, 40);
			Verify(41, 43, 42);
			Verify(42, 43, 42);

			map.ClearAll();
			map.Set(234);
			Verify(0, SIZE, 234);
			Verify(234, SIZE, 234);
			Verify(42, 237, 234);
			Verify(231, 237, 234);
			Verify(0, 234, -1);
			Verify(0, 235, 234);
		}

		[Test]
		public void Test_BitMap64_FindSpan()
		{
			const int SIZE = 256;

			// naive reference: first index >= start where `span` consecutive bits are all set (within SIZE)
			static long Naive(bool[] bits, uint span, long start)
			{
				for (long i = Math.Max(0L, start); i + span <= bits.Length; i++)
				{
					bool all = true;
					for (uint j = 0; j < span; j++)
					{
						if (!bits[i + j]) { all = false; break; }
					}
					if (all) return i;
				}
				return -1L;
			}

			// explicit cases
			{
				var m = new BitMap64(SIZE);
				Assert.That(m.FindSpan(1, 0), Is.EqualTo(-1L), "empty map has no run");
				m.Set(10); m.Set(11); m.Set(12);
				Assert.That(m.FindSpan(1, 0), Is.EqualTo(10L));
				Assert.That(m.FindSpan(3, 0), Is.EqualTo(10L));
				Assert.That(m.FindSpan(4, 0), Is.EqualTo(-1L), "no run of 4");
				Assert.That(m.FindSpan(3, 11), Is.EqualTo(-1L), "run of 3 does not start at or after 11");
				Assert.That(m.FindSpan(2, 11), Is.EqualTo(11L));
			}

			// randomized parity against the naive reference, across many maps and span sizes
			var rnd = new Random(20260801);
			for (int iter = 0; iter < 1000; iter++)
			{
				var map = new BitMap64(SIZE);
				var bits = new bool[SIZE];
				int ones = rnd.Next(0, SIZE + 1);
				for (int k = 0; k < ones; k++)
				{
					int b = rnd.Next(SIZE);
					map.Set(b);
					bits[b] = true;
				}
				for (uint span = 1; span <= 40; span++)
				{
					long start = rnd.Next(0, SIZE);
					long expected = Naive(bits, span, start);
					long actual = map.FindSpan(span, start);
					if (actual != expected)
					{
						Assert.That(actual, Is.EqualTo(expected), $"span={span} start={start} map={map.ToString("B")}");
					}
				}
			}
		}

		[Test]
		public void Test_BitMap64_TestAndClear()
		{
			var map = new BitMap64(128);

			Assert.That(map.TestAndClear(42), Is.False);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));

			map.Set(42);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000010000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));
			Assert.That(map.TestAndClear(42), Is.True);
			Assert.That(map.Test(42), Is.False);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));

			Assert.That(map.TestAndClear(42), Is.False);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));

			map.Set(41);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000100000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));
			Assert.That(map.TestAndClear(42), Is.False);
			Assert.That(map.Test(42), Is.False);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000100000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));

			map.Set(43);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000101000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));
			Assert.That(map.TestAndClear(42), Is.False);
			Assert.That(map.Test(42), Is.False);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000101000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));

			map.Set(42 + 64);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000101000000000000000000000000000000000000000000000000000000000000001000000000000000000000"));
			Assert.That(map.TestAndClear(42), Is.False);
			Assert.That(map.Test(42), Is.False);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000101000000000000000000000000000000000000000000000000000000000000001000000000000000000000"));

			map.Set(42);
			Assert.That(map.Test(42), Is.True);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000111000000000000000000000000000000000000000000000000000000000000001000000000000000000000"));
			Assert.That(map.TestAndClear(42), Is.True);
			Assert.That(map.Test(42), Is.False);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000101000000000000000000000000000000000000000000000000000000000000001000000000000000000000"));

			Assert.That(map.TestAndClear(42), Is.False);
			Assert.That(map.Test(42), Is.False);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000101000000000000000000000000000000000000000000000000000000000000001000000000000000000000"));

		}

		[Test]
		public void Test_BitMap64_Enumerable()
		{
			var map = new BitMap64(256);
			map[1] = true;
			map[2] = true;
			map[5] = true;
			map[7] = true;
			map[42] = true;
			map[63] = true;
			map[123] = true;
			map[128] = true;
			map[255] = true;

			using (var it = map.GetEnumerator())
			{
				for (int i = 0; i < 256; i++)
				{
					Assert.That(it.MoveNext(), Is.True);
					Assert.That(it.Current, Is.EqualTo(map[i]), $"#{i}");
				}
				Assert.That(it.MoveNext(), Is.False, "Should be done after 256 bits");
			}

			// LINQ
			Assert.That(map.Select((b, i) => new KeyValuePair<int, bool>(i, b)).Where(kv => kv.Value).Select(kv => kv.Key).ToArray(), Is.EqualTo(new[] { 1, 2, 5, 7, 42, 63, 123, 128, 255 }));

			// ToArray
			var bits = map.ToArray();
			Assert.That(bits.Length, Is.EqualTo(256));
			for (int i = 0; i < bits.Length; i++)
			{
				Assert.That(bits[i], Is.EqualTo(map[i]), $"#{i}");
			}

		}

	}
}
