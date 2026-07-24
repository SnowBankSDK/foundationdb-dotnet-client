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

// This file is not compiled for the net472 validation target: the COLA collections are not part of the netstandard2.0 build of SnowBank.Core.
#if !NETFRAMEWORK

namespace SnowBank.Collections.CacheOblivious.Test
{
	using NUnit.Framework;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class ColaRangeSetFacts : SimpleTest
	{

		private void DumpStore<TKey>(ColaRangeSet<TKey> store)
		{
			var sw = new StringWriter();
			store.Debug_Dump(sw);
			Log(sw);
		}

		[Test]
		public void Test_Empty_RangeSet()
		{
			var cola = new ColaRangeSet<int>(0, Comparer<int>.Default);
			Assert.That(cola.Count, Is.EqualTo(0));
			Assert.That(cola.Comparer, Is.SameAs(Comparer<int>.Default));
			Assert.That(cola.Capacity, Is.EqualTo(31), "Initial capacity should hold 5 levels which is 1+2+4+8+16 = 31 items");
			Assert.That(cola.Bounds, Is.Not.Null);
			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(0));
		}

		[Test]
		public void Test_RangeSet_Insert_Non_Overlapping()
		{
			var cola = new ColaRangeSet<int>();
			Assert.That(cola.Count, Is.EqualTo(0));

			cola.Mark(0, 1);
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(1));

			cola.Mark(2, 3);
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(2));

			cola.Mark(4, 5);
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(3));

			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(5));

			Log($"Result = {{ {string.Join(", ", cola)} }}");
			Log($"Bounds = {cola.Bounds}");
		}

		[Test]
		public void Test_RangeSet_Insert_Partially_Overlapping()
		{
			var cola = new ColaRangeSet<int>();
			Assert.That(cola.Count, Is.EqualTo(0));

			cola.Mark(0, 1);
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(1));

			cola.Mark(0, 2);
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(1));

			cola.Mark(1, 3);
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(1));

			cola.Mark(-1, 2);
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(1));

			Log($"Result = {{ {string.Join(", ", cola)} }}");
			Log($"Bounds = {cola.Bounds}");
		}

		[Test]
		public void Test_RangeSet_Insert_Completely_Overlapping()
		{
			var cola = new ColaRangeSet<int>();
			cola.Mark(1, 2);
			cola.Mark(4, 5);
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(2));
			Assert.That(cola.Bounds.Begin, Is.EqualTo(1));
			Assert.That(cola.Bounds.End, Is.EqualTo(5));

			// overlaps the first range completely
			cola.Mark(0, 3);
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(2));
			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(5));

			Log($"Result = {{ {string.Join(", ", cola)} }}");
			Log($"Bounds = {cola.Bounds}");
		}

		[Test]
		public void Test_RangeSet_Insert_That_Join_Two_Ranges()
		{
			var cola = new ColaRangeSet<int>();
			cola.Mark(0, 1);
			cola.Mark(2, 3);
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(2));

			cola.Mark(1, 2);
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(1));

			Log($"Result = {{ {string.Join(", ", cola)} }}");
			Log($"Bounds = {cola.Bounds}");
		}

		[Test]
		public void Test_RangeSet_Insert_That_Replace_All_Ranges()
		{
			var cola = new ColaRangeSet<int>();
			cola.Mark(0, 1);
			cola.Mark(2, 3);
			cola.Mark(4, 5);
			cola.Mark(6, 7);
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(4));
			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(7));

			cola.Mark(-1, 10);
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(1));
			Assert.That(cola.Bounds.Begin, Is.EqualTo(-1));
			Assert.That(cola.Bounds.End, Is.EqualTo(10));

			Log($"Result = {{ {string.Join(", ", cola)} }}");
			Log($"Bounds = {cola.Bounds}");
		}

		[Test]
		public void Test_RangeSet_Insert_Below_All_That_Joins_Following_Ranges()
		{
			// a range starting below every existing begin, touching the first range, with MORE ranges beyond:
			// the merge must absorb every touched range into their full union - a lost span here silently
			// erases previously-marked ranges (e.g. recorded read conflicts) from the set
			var cola = new ColaRangeSet<int>();
			cola.Mark(10, 11);
			cola.Mark(20, 40);
			cola.Mark(50, 60);
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(3));

			// starts below 10, touches [10,11) and reaches into [20,40): union with both, [50,60) untouched
			cola.Mark(0, 20);
			DumpStore(cola);
			Log($"Result = {{ {string.Join(", ", cola)} }}");
			Assert.That(cola.Count, Is.EqualTo(2));
			Assert.That(cola.ContainsKey(30), Is.True, "the [20,40) span must survive the merge");
			Assert.That(cola.ContainsKey(55), Is.True);
			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(60));

			// same shape, but the new range stops exactly AT the following range's begin (adjacency, the
			// seed-2114 signature): the union must still cover the whole absorbed span
			cola = new ColaRangeSet<int>();
			cola.Mark(10, 11);
			cola.Mark(20, 40);
			cola.Mark(0, 20);
			DumpStore(cola);
			Log($"Result = {{ {string.Join(", ", cola)} }}");
			Assert.That(cola.Count, Is.EqualTo(1));
			Assert.That(cola.ContainsKey(30), Is.True, "the [20,40) span must survive the adjacency merge");
			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(40));
		}

		[Test]
		public void Test_RangeSet_Mark_Degenerate_Range_Is_A_NoOp()
		{
			// an empty range (begin == end) contains nothing, so marking it must leave the set unchanged
			// (FoundationDB accepts a degenerate conflict range as a no-op; only a backwards range is a caller error)
			var cola = new ColaRangeSet<int>();

			cola.Mark(0, 0);
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(0), "Marking an empty range on an empty set should not add anything");

			cola.Mark(0, 1);
			cola.Mark(4, 5);
			Assert.That(cola.Count, Is.EqualTo(2));

			cola.Mark(0, 0); // at the begin of an existing range
			cola.Mark(1, 1); // at the (exclusive) end of an existing range
			cola.Mark(2, 2); // in the gap between two ranges
			cola.Mark(7, 7); // past the current bounds
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(2), "Marking an empty range should never change existing ranges");
			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(5), "Marking an empty range should not extend the bounds");

			// a backwards range is still rejected
			Assert.That(() => cola.Mark(2, 1), Throws.InvalidOperationException);
		}

		[Test]
		public void Test_RangeSet_Insert_Backwards()
		{
			const int N = 100;

			var cola = new ColaRangeSet<int>();

			for(int i = N; i > 0; i--)
			{
				int x = i << 1;
				cola.Mark(x - 1, x);
			}

			Assert.That(cola.Count, Is.EqualTo(N));

			Log($"Result = {{ {string.Join(", ", cola)} }}");
			Log($"Bounds = {cola.Bounds}");
		}

	}

}

#endif
