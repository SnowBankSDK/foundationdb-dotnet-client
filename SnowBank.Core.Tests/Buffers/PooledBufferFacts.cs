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

namespace SnowBank.Buffers.Tests
{
	using System.Buffers;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class PooledBufferFacts : SimpleTest
	{

		[Test]
		public void Test_Buffer_With_Empty_Buffer()
		{
			var buffer = PooledBuffer<int>.Create(ArrayPool<int>.Shared, 0);
			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.Capacity, Is.EqualTo(0));
			Assert.That(buffer.ToArray(), Is.Empty);

			buffer.Add(123);

			Assert.That(buffer.Count, Is.EqualTo(1));
			Assert.That(buffer.Capacity, Is.EqualTo(16));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 123 ]));
		}

		[Test]
		public void Test_Buffer_With_Initial_Capacity()
		{
			var buffer = PooledBuffer<int>.Create(ArrayPool<int>.Shared, 16);
			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.Capacity, Is.EqualTo(16));
			Assert.That(buffer.ToArray(), Is.Empty);

			buffer.Add(123);

			Assert.That(buffer.Count, Is.EqualTo(1));
			Assert.That(buffer.Capacity, Is.EqualTo(16));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 123 ]));
		}

		[Test]
		public void Test_Buffer_Resize_Should_Use_The_Pool()
		{
			// use the stack for the initial array
			var buffer = PooledBuffer<int>.Create(ArrayPool<int>.Shared, 16);
			buffer.Add(123);

			Assert.That(buffer.Count, Is.EqualTo(1));
			Assert.That(buffer.Capacity, Is.EqualTo(16));
			Assert.That(buffer[0], Is.EqualTo(123));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 123 ]));

			// should fill the buffer
			buffer.AddRange([ 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 ]);
			Assert.That(buffer.Count, Is.EqualTo(16));
			Assert.That(buffer.Capacity, Is.EqualTo(16));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 123, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 ]));

			// adding more should swap to a larger buffer
			buffer.Add(17);
			Assert.That(buffer.Count, Is.EqualTo(17));
			Assert.That(buffer.Capacity, Is.EqualTo(32));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 123, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 ]));
		}

		[Test]
		public void Test_Buffer_ToArray()
		{
			// start with an empty buffer (no allocations yet)
			var buffer = PooledBuffer<int>.Create();
			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.Capacity, Is.EqualTo(0));

			// adding first item should allocate a buffer from the pool
			buffer.Add(123);
			Assert.That(buffer.Count, Is.EqualTo(1));
			int capacityAfterAdd = buffer.Capacity;
			Assert.That(capacityAfterAdd, Is.GreaterThanOrEqualTo(2));
			Assert.That(buffer[0], Is.EqualTo(123));

			var array1 = buffer.ToArray();
			Assert.That(array1, Is.EqualTo((int[]) [ 123 ]));
			Assert.That(buffer.Count, Is.EqualTo(1), "First call to ToArray() should not empty the buffer");
			Assert.That(buffer.Capacity, Is.EqualTo(capacityAfterAdd));

			// can call ToArray() multiple times without changing the buffer, but should return a new array each time
			var array2 = buffer.ToArray();
			Assert.That(array2, Is.EqualTo((int[]) [ 123 ]));
			Assert.That(array2, Is.Not.SameAs(array1));

			// if we reuse the buffer it should keep the same buffer
			buffer.Add(456);

			Assert.That(buffer.Count, Is.EqualTo(2));
			Assert.That(buffer.Capacity, Is.EqualTo(capacityAfterAdd));
			Assert.That(buffer[0], Is.EqualTo(123));
			Assert.That(buffer[1], Is.EqualTo(456));
			var array3 = buffer.ToArray();
			Assert.That(array3, Is.EqualTo((int[]) [ 123, 456 ]));
			Assert.That(array1, Is.EqualTo((int[]) [ 123 ]), "None on of the previous arrays should be changed");
			Assert.That(array2, Is.EqualTo((int[]) [ 123 ]), "None on of the previous arrays should be changed");
		}

		[Test]
		public void Test_Buffer_ToArrayAndClear()
		{
			// start with an empty buffer (no allocations yet)
			var buffer = PooledBuffer<int>.Create();
			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.Capacity, Is.EqualTo(0));

			// adding first item should allocate a buffer from the pool
			buffer.Add(123);
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 123 ]));
			Assert.That(buffer.Count, Is.EqualTo(1), "First call to ToArray() should not empty the buffer");
			Assert.That(buffer.Capacity, Is.GreaterThanOrEqualTo(1));

			var array = buffer.ToArray(clear: true);

			Assert.That(array, Is.EqualTo((int[]) [ 123 ]));
			Assert.That(buffer.Count, Is.EqualTo(0), "Call to ToArrayAndClear() should empty the buffer");
			Assert.That(buffer.Capacity, Is.EqualTo(16), "Call to ToArrayAndClear() on a small buffer should keep the buffer");

			// if we reuse the buffer, it should allocate a new backing store
			//note: it IS very likely that the pool will give us back the exact same buffer we just returned
			buffer.Add(456);

			Assert.That(buffer.Count, Is.EqualTo(1));
			Assert.That(buffer.Capacity, Is.GreaterThanOrEqualTo(1));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 456 ]));
			Assert.That(array, Is.EqualTo((int[]) [ 123 ]), "first array should be unchanged");
		}

		[Test]
		public void Test_Buffer_Add()
		{
			var buffer = PooledBuffer<int>.Create(initialCapacity: 16);
			Assume.That(buffer.Capacity, Is.EqualTo(16), "The shared pool should allocate 16 even if we asked for 6");
			Assert.That(buffer.Count, Is.EqualTo(0));

			buffer.Add(1);
			Assert.That(buffer.Count, Is.EqualTo(1));
			Assert.That(buffer.ToArray(), Is.EqualTo(((int[]) [ 1 ])));
			Assert.That(buffer.Capacity, Is.EqualTo(16));

			// add items that fit in the buffer
			buffer.Add(2);
			buffer.Add(3);
			buffer.Add(4);
			Assert.That(buffer.Count, Is.EqualTo(4));
			Assert.That(buffer.ToArray(), Is.EqualTo(((int[]) [ 1, 2, 3, 4 ])));
			Assert.That(buffer.Capacity, Is.EqualTo(16));

			// add up to the limit, no resize
			for (int i = 5; i <= 16; i++)
			{
				buffer.Add(i);
				Assert.That(buffer.Count, Is.EqualTo(i));
				Assert.That(buffer.Capacity, Is.EqualTo(16));
				Assert.That(buffer.ToArray(), Is.EqualTo(Enumerable.Range(1, i).ToArray()));
			}

			// add one extra
			buffer.Add(17);
			Assert.That(buffer.Count, Is.EqualTo(17));
			Assert.That(buffer.Capacity, Is.EqualTo(32));
			Assert.That(buffer.ToArray(), Is.EqualTo(((int[]) [ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 ])));
		}

		[Test]
		public void Test_Buffer_AddRange()
		{
			var buffer = PooledBuffer<int>.Create(initialCapacity: 16);
			Assume.That(buffer.Capacity, Is.EqualTo(16), "The shared pool should allocate 16 even if we asked for 6");
			Assert.That(buffer.Count, Is.EqualTo(0));

			// add nothing
			buffer.AddRange([ ]);
			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.ToArray(), Is.Empty);
			Assert.That(buffer.Capacity, Is.EqualTo(16));

			// add one item
			buffer.AddRange([ 1 ]);
			Assert.That(buffer.Count, Is.EqualTo(1));
			Assert.That(buffer.ToArray(), Is.EqualTo(((int[]) [ 1 ])));
			Assert.That(buffer.Capacity, Is.EqualTo(16));

			// add items that fit in the buffer
			buffer.AddRange([ 2, 3, 4 ]);
			Assert.That(buffer.Count, Is.EqualTo(4));
			Assert.That(buffer.ToArray(), Is.EqualTo(((int[]) [ 1, 2, 3, 4 ])));
			Assert.That(buffer.Capacity, Is.EqualTo(16));

			// too many items, should trigger a resize!
			buffer.AddRange([ 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 ]); // one extra!
			Assert.That(buffer.Count, Is.EqualTo(17));
			Assert.That(buffer.ToArray(), Is.EqualTo(((int[]) [ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 ])));
			Assert.That(buffer.Capacity, Is.GreaterThanOrEqualTo(17));
		}

		[Test]
		public void Test_Buffer_BufferWriter_GetSpan_Zero()
		{
			var buffer = PooledBuffer<int>.Create(initialCapacity: 16);
			Assume.That(buffer.Capacity, Is.EqualTo(16));

			// GetSpan() should return the original span entirely
			var chunk = buffer.GetSpan();
			Assert.That(chunk.Length, Is.EqualTo(16), "GetSpan(0) should return all the space available");
			Assert.That(buffer.Count, Is.EqualTo(0), "GetSpan() should not advance the cursor");

			// only consume 2 items from the chunk
			chunk[0] = 1;
			chunk[1] = 2;
			buffer.Advance(2);
			Assert.That(buffer.Count, Is.EqualTo(2), "Advance() should advance the cursor");
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 1, 2, ]));

			// GetSpan() should not return what remains in the buffer
			chunk = buffer.GetSpan();
			Assert.That(chunk.Length, Is.EqualTo(14), "GetSpan(0) should return all the space available");

			// consume most of the span
			chunk[0] = 3;
			chunk[1] = 4;
			chunk[2] = 5;
			chunk[3] = 6;
			chunk[4] = 7;
			chunk[5] = 8;
			chunk[6] = 9;
			chunk[7] = 10;
			chunk[8] = 11;
			chunk[9] = 12;
			chunk[10] = 13;
			chunk[11] = 14;
			chunk[12] = 15;
			// one remains!
			buffer.Advance(13);
			Assert.That(buffer.Count, Is.EqualTo(15), "Advance() should advance the cursor");
			Assert.That(buffer.Capacity, Is.EqualTo(16));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 ]));

			// only one byte remains
			chunk = buffer.GetSpan();
			Assert.That(chunk.Length, Is.EqualTo(1));
			Assert.That(buffer.Count, Is.EqualTo(15));
			Assert.That(buffer.Capacity, Is.EqualTo(16)); // no resize yet!

			// fill it
			chunk[0] = 16;
			buffer.Advance(1);

			// requesting more should trigger a resize!
			Assert.That(buffer.Count, Is.EqualTo(16), "Advance() should advance the cursor");
			Assert.That(buffer.Capacity, Is.EqualTo(16), "Buffer should not have been resized yet!");
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 ]));

			// now ask for more
			chunk = buffer.GetSpan();
			Assert.That(chunk.Length, Is.EqualTo(16));
			Assert.That(buffer.Count, Is.EqualTo(16));
			Assert.That(buffer.Capacity, Is.EqualTo(32), "Buffer should have been resized");
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 ]));

			chunk[0] = 17;
			chunk[1] = 18;
			chunk[2] = 19;
			buffer.Advance(3);
			Assert.That(buffer.Count, Is.EqualTo(19), "Advance() should advance the cursor");
			Assert.That(buffer.Capacity, Is.EqualTo(32));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 ]));
		}

		[Test]
		public void Test_Buffer_BufferWriter_GetSpan_Exact()
		{
			var buffer = PooledBuffer<int>.Create(initialCapacity: 16);
			Assume.That(buffer.Capacity, Is.EqualTo(16));

			// GetSpan(...) with a non-zero size, should return a span with that size
			var chunk = buffer.GetSpan(4);
			Assert.That(chunk.Length, Is.EqualTo(4), "GetSpan(>0) should return a span with the exact size");
			Assert.That(buffer.Count, Is.EqualTo(0), "GetSpan() should not advance the cursor");
			Assert.That(buffer.Capacity, Is.EqualTo(16));

			// only consume 2 items from the chunk
			chunk[0] = 1;
			chunk[1] = 2;
			buffer.Advance(2);
			Assert.That(buffer.Count, Is.EqualTo(2), "Advance() should advance the cursor");
			Assert.That(buffer.Capacity, Is.EqualTo(16));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 1, 2, ]));

			// GetSpan() with the exact remaining size should not trigger a resize
			chunk = buffer.GetSpan(14);
			Assert.That(chunk.Length, Is.EqualTo(14), "GetSpan(0) should return all the space available");
			Assert.That(buffer.Capacity, Is.EqualTo(16));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 1, 2, ]));

			// consume most of the span
			chunk[0] = 3;
			chunk[1] = 4;
			chunk[2] = 5;
			chunk[3] = 6;
			chunk[4] = 7;
			chunk[5] = 8;
			chunk[6] = 9;
			chunk[7] = 10;
			chunk[8] = 11;
			chunk[9] = 12;
			chunk[10] = 13;
			chunk[11] = 14;
			chunk[12] = 15;
			// one remains!
			buffer.Advance(13); // one remains!
			Assert.That(buffer.Count, Is.EqualTo(15), "Advance() should advance the cursor");
			Assert.That(buffer.Capacity, Is.EqualTo(16));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 ]));

			// only one byte remains, so if we ask for more it should resize first
			chunk = buffer.GetSpan(13);
			Assert.That(chunk.Length, Is.EqualTo(13));
			Assert.That(buffer.Count, Is.EqualTo(15));
			Assert.That(buffer.Capacity, Is.EqualTo(32));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 ]));

			chunk[0] = 16;
			chunk[1] = 17;
			chunk[2] = 18;
			buffer.Advance(3);
			Assert.That(buffer.Count, Is.EqualTo(18), "Advance() should advance the cursor");
			Assert.That(buffer.Capacity, Is.EqualTo(32));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 ]));
		}

		[Test]
		public void Test_Buffer_BufferWriter_RandomWalk()
		{
			// process a list of N random items by adding them to the buffer via random small chunks
			
			var randomItems= GetRandomNumbers(1000);
			Dump(randomItems);

			using var buffer = PooledBuffer<int>.Create();

			ReadOnlySpan<int> remaining = randomItems;
			while (remaining.Length > 0)
			{
				var chunk = NextRandomChunk(remaining, 17);
				Assert.That(chunk.Length, Is.GreaterThan(0).And.LessThanOrEqualTo(remaining.Length));

				var span = buffer.GetSpan(chunk.Length);
				Assert.That(span.Length, Is.GreaterThanOrEqualTo(chunk.Length));

				chunk.CopyTo(span);
				buffer.Advance(chunk.Length);

				remaining = remaining.Slice(chunk.Length);
			}

			Assert.That(buffer.Count, Is.EqualTo(randomItems.Length));
			Assert.That(buffer.Capacity, Is.GreaterThanOrEqualTo(randomItems.Length));
			Assert.That(buffer.ToArray(), Is.EqualTo(randomItems));
		}

	}

}
