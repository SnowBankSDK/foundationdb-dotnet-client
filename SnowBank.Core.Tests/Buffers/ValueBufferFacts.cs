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
	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class ValueBufferFacts : SimpleTest
	{

		[Test]
		public void Test_Buffer_With_Initial_Capacity()
		{
			var buffer = new ValueBuffer<int>(7);
			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.Capacity, Is.GreaterThanOrEqualTo(7));
			Assert.That(buffer.ToArray(), Is.Empty);

			buffer.Add(123);

			Assert.That(buffer.Count, Is.EqualTo(1));
			Assert.That(buffer.Capacity, Is.GreaterThanOrEqualTo(7));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 123 ]));
		}

		[Test]
		public void Test_Buffer_With_Heap_Backed_Array()
		{
			// array on the heap
			var array = new int[7];

			var buffer = new ValueBuffer<int>(array);
			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.Capacity, Is.GreaterThanOrEqualTo(7));
			Assert.That(buffer.ToArray(), Is.Empty);

			buffer.Add(123);

			Assert.That(buffer.Count, Is.EqualTo(1));
			Assert.That(buffer.Capacity, Is.EqualTo(array.Length));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 123 ]));
			Assert.That(Unsafe.AreSame(ref buffer[0], ref array[0]));
		}

		[Test]
		public void Test_Buffer_With_Stack_Backed_Array()
		{
			// use the stack
			Span<int> span = stackalloc int[7];

			var buffer = new ValueBuffer<int>(span);
			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.Capacity, Is.GreaterThanOrEqualTo(7));
			Assert.That(buffer.ToArray(), Is.Empty);

			buffer.Add(123);

			Assert.That(buffer.Count, Is.EqualTo(1));
			Assert.That(buffer.Capacity, Is.EqualTo(span.Length));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 123 ]));
			Assert.That(Unsafe.AreSame(ref buffer[0], ref span[0]));
		}

		[Test]
		public void Test_Buffer_Resize_Should_Use_The_Pool()
		{
			// use the stack for the initial array
			Span<int> span = stackalloc int[7];
			span.Fill(-1);

			var buffer = new ValueBuffer<int>(span);
			buffer.Add(123);

			Assert.That(buffer.Count, Is.EqualTo(1));
			Assert.That(buffer.Capacity, Is.EqualTo(span.Length));
			Assert.That(buffer[0], Is.EqualTo(123));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 123 ]));
			Assert.That(span.ToArray(), Is.EqualTo((int[]) [ 123, -1, -1, -1, -1, -1, -1 ]), "Should have written to the stack-based buffer");

			Assert.That(Unsafe.AreSame(ref buffer[0], ref span[0]), Is.True, "Should use the stack as the buffer");
			
			// should fill the buffer
			buffer.AddRange([ 2, 3, 4, 5, 6, 7 ]);
			Assert.That(buffer.Count, Is.EqualTo(7));
			Assert.That(buffer.Capacity, Is.EqualTo(span.Length));
			Assert.That(buffer[0], Is.EqualTo(123));
			Assert.That(buffer[1], Is.EqualTo(2));
			Assert.That(buffer[2], Is.EqualTo(3));
			Assert.That(buffer[3], Is.EqualTo(4));
			Assert.That(buffer[4], Is.EqualTo(5));
			Assert.That(buffer[5], Is.EqualTo(6));
			Assert.That(buffer[6], Is.EqualTo(7));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 123, 2, 3, 4, 5, 6, 7 ]));
			Assert.That(span.ToArray(), Is.EqualTo((int[]) [ 123, 2, 3, 4, 5, 6, 7 ]), "Should have written to the stack-based buffer");

			Assert.That(Unsafe.AreSame(ref buffer[0], ref span[0]), Is.True, "Should use the stack as the buffer");

			// adding more should switch to a heap-backed pooled buffer
			buffer.Add(8);
			Assert.That(buffer.Count, Is.EqualTo(8));
			Assert.That(buffer.Capacity, Is.GreaterThan(span.Length));
			Assert.That(buffer[0], Is.EqualTo(123));
			Assert.That(buffer[1], Is.EqualTo(2));
			Assert.That(buffer[2], Is.EqualTo(3));
			Assert.That(buffer[3], Is.EqualTo(4));
			Assert.That(buffer[4], Is.EqualTo(5));
			Assert.That(buffer[5], Is.EqualTo(6));
			Assert.That(buffer[6], Is.EqualTo(7));
			Assert.That(buffer[7], Is.EqualTo(8));

			Assert.That(Unsafe.AreSame(ref buffer[0], ref span[0]), Is.False, "Should have switched to a different buffer");
		}

		[Test]
		public void Test_Buffer_ToArray()
		{
			// start with an empty buffer (no allocations yet)
			var buffer = new ValueBuffer<int>(0);
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
			var buffer = new ValueBuffer<int>(0);
			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.Capacity, Is.EqualTo(0));

			// adding first item should allocate a buffer from the pool
			buffer.Add(123);
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 123 ]));
			Assert.That(buffer.Count, Is.EqualTo(1), "First call to ToArray() should not empty the buffer");
			Assert.That(buffer.Capacity, Is.GreaterThanOrEqualTo(1));

			// calling ToArrayAndClear() should clear the buffer (and return the buffer to the pool!)
			// => also, it should clear the content buffer sending it back!
			ref readonly int backingStore = ref buffer[0];

			var array2 = buffer.ToArrayAndClear();
			//note: this COULD fail if there are concurrent executing tests that got this buffer and already started using it!
			Assert.That(backingStore, Is.EqualTo(0), "ToArrayAndClear() should clear the buffer before returning it to the pool. MAY FAIL WHEN RUNING TESTS IN PARALLEL!");

			Assert.That(array2, Is.EqualTo((int[]) [ 123 ]));
			Assert.That(buffer.Count, Is.EqualTo(0), "Call to ToArrayAndClear() should empty the buffer");
			Assert.That(buffer.Capacity, Is.EqualTo(0), "Call to ToArrayAndClear() should return the buffer to the pool");


			// if we reuse the buffer, it should allocate a new backing store
			//note: it IS very likely that the pool will gives us back the exact same buffer we just returned
			buffer.Add(456);

			Assert.That(buffer.Count, Is.EqualTo(1));
			Assert.That(buffer.Capacity, Is.GreaterThanOrEqualTo(1));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 456 ]));
			Assert.That(array2, Is.EqualTo((int[]) [ 123 ]));
		}

		[Test]
		public void Test_Buffer_AddRange()
		{
			var initial = new int[6];
			var buffer = new ValueBuffer<int>(initial);
			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.Capacity, Is.EqualTo(initial.Length));

			buffer.Add(42);
			Assert.That(buffer.Count, Is.EqualTo(1));
			Assert.That(buffer.ToArray(), Is.EqualTo(((int[]) [ 42 ])));
			Assert.That(buffer.Capacity, Is.EqualTo(initial.Length));
			Assert.That(initial, Is.EqualTo((int[]) [ 42, 0, 0, 0, 0, 0 ]), "Should fill the initial buffer");

			// mutating the original buffer before the first resize should be observed
			initial[0] = 1;
			Assert.That(buffer.ToArray(), Is.EqualTo(((int[]) [ 1 ])));

			// add items that fit in the buffer
			buffer.AddRange([ 2, 3, 4 ]);
			Assert.That(buffer.Count, Is.EqualTo(4));
			Assert.That(buffer.ToArray(), Is.EqualTo(((int[]) [ 1, 2, 3, 4 ])));
			Assert.That(buffer.Capacity, Is.EqualTo(initial.Length));
			Assert.That(initial, Is.EqualTo((int[]) [ 1, 2, 3, 4, 0, 0 ]), "Should fill the initial buffer");

			// too many items, should trigger a resize!
			buffer.AddRange([ 5, 6, 7, 8 ]);
			Assert.That(buffer.Count, Is.EqualTo(8));
			Assert.That(buffer.ToArray(), Is.EqualTo(((int[]) [ 1, 2, 3, 4, 5, 6, 7, 8 ])));
			Assert.That(buffer.Capacity, Is.GreaterThanOrEqualTo(8));
			Assert.That(initial, Is.EqualTo((int[]) [ 1, 2, 3, 4, 0, 0 ]), "Should not use the initial buffer after the first resize!");

			// mutating the original buffer should not change the buffer content, once a resize has occurred
			initial[0] = 42;
			Assert.That(buffer[0], Is.EqualTo(1));
			Assert.That(buffer.ToArray(), Is.EqualTo(((int[]) [ 1, 2, 3, 4, 5, 6, 7, 8 ])));

		}

		[Test]
		public void Test_Buffer_BufferWriter_Like()
		{
			// We cannot implement IBufferWriter<T> since we are not able to implement GetMemory(..),
			// but we can emulate the same API with GetSpan(...) and Advance()

			Span<int> span = stackalloc int[8];
			span.Fill(-1);

			var buffer = new ValueBuffer<int>(span);
			Assume.That(buffer.Capacity, Is.EqualTo(8));

			// GetSpan() should return the original span entirely
			var chunk = buffer.GetSpan();
			Assert.That(chunk.Length, Is.EqualTo(8));
			Assert.That(Unsafe.AreSame(ref chunk[0], ref span[0]), Is.True, "Should return the same span!");
			Assert.That(buffer.Count, Is.EqualTo(0), "GetSpan() should not advance the cursor");

			// GetSpan(...) with a hint smaller than the capacity should also return the full buffer
			chunk = buffer.GetSpan(4);
			Assert.That(chunk.Length, Is.EqualTo(8));
			Assert.That(Unsafe.AreSame(ref chunk[0], ref span[0]), Is.True, "Should return the entire span if sizeHint is smaller than capacity");
			Assert.That(buffer.Count, Is.EqualTo(0), "GetSpan() should not advance the cursor");

			// only consume 2 items from the chunk
			chunk[0] = 1;
			chunk[1] = 2;
			buffer.Advance(2);
			Assert.That(buffer.Count, Is.EqualTo(2), "Advance() should advance the cursor");
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 1, 2, ]));

			// GetSpan() should not return what remains in the buffer
			chunk = buffer.GetSpan();
			Assert.That(chunk.Length, Is.EqualTo(6));
			// and it should skip the first 2 items
			Assert.That(Unsafe.AreSame(ref chunk[0], ref span[2]), Is.True, "New chunk should skip the first 2 items of the original buffer");

			// consume most of the span
			chunk[0] = 3;
			chunk[1] = 4;
			chunk[2] = 5;
			chunk[3] = 6;
			chunk[4] = 7;
			// one remains!
			buffer.Advance(5);
			Assert.That(buffer.Count, Is.EqualTo(7), "Advance() should advance the cursor");
			Assert.That(buffer.Capacity, Is.EqualTo(8));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 1, 2, 3, 4, 5, 6, 7 ]));

			// only one byte remains
			chunk = buffer.GetSpan();
			Assert.That(chunk.Length, Is.EqualTo(1));
			Assert.That(Unsafe.AreSame(ref chunk[0], ref span[7]), Is.True, "New chunk should skip the first 7 items of the original buffer");
			Assert.That(buffer.Count, Is.EqualTo(7));
			Assert.That(buffer.Capacity, Is.EqualTo(8)); // no resize yet!

			// requesting more should trigger a resize!

			chunk = buffer.GetSpan(13);
			Assert.That(chunk.Length, Is.GreaterThanOrEqualTo(17));
			Assert.That(buffer.Count, Is.EqualTo(7));
			Assert.That(buffer.Capacity, Is.GreaterThanOrEqualTo(20));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 1, 2, 3, 4, 5, 6, 7 ]));

			// buffer should not use the original span anymore!
			Assert.That(Unsafe.IsAddressLessThan(ref chunk[0], ref span[0]) || Unsafe.IsAddressGreaterThan(ref chunk[0], ref span[^1]), Is.True, "Buffer should not use the original span anymore");

			chunk[0] = 8;
			chunk[1] = 9;
			chunk[2] = 10;
			buffer.Advance(3);
			Assert.That(buffer.Count, Is.EqualTo(10), "Advance() should advance the cursor");
			Assert.That(buffer.Capacity, Is.GreaterThanOrEqualTo(20));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 ]));
		}

		[Test]
		public void Test_Buffer_BufferWriter_RandomWalk()
		{
			// process a list of N random items by adding them to the buffer via random small chunks
			
			var randomItems= GetRandomNumbers(1000);
			Dump(randomItems);

			using var buffer = new ValueBuffer<int>(0);

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
