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
