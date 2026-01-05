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
// ReSharper disable PossibleMultipleEnumeration
// ReSharper disable AccessToDisposedClosure
namespace SnowBank.Linq.Tests
{
	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class EnumerableFacts : SimpleTest
	{

		[Test]
		public void Test_Can_None()
		{
			Assert.That(Enumerable.Range(0, 10).None(), Is.False);
			Assert.That(Enumerable.Range(0, 1).None(), Is.False);
			Assert.That(Enumerable.Empty<int>().None(), Is.True);
			Assert.That(Array.Empty<int>().None(), Is.True);
			Assert.That(new int[] { 1 }.None(), Is.False);
			Assert.That(new int[] { 1, 2, 3 }.None(), Is.False);
		}

		[Test]
		public void Test_Can_None_With_Predicate()
		{
			var source = Enumerable.Range(0, 10);

			bool any = source.None(x => x % 2 == 1);
			Assert.That(any, Is.False);

			any = source.None(x => x < 0);
			Assert.That(any, Is.True);

			any = Enumerable.Empty<int>().None(x => x == 42);
			Assert.That(any, Is.True);
		}

		[Test]
		public void Test_Enumerable_Buffered_Array()
		{
			int[] RangeArray(int count) => Enumerable.Range(0, count).ToArray();

			{ // empty
				var items = RangeArray(0);

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.False);
			}
			{ // less than chunk size
				var items = RangeArray(7);

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.True);
				var chunk = iterator.Current;
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(0, 7).ToList()));
				Assert.That(iterator.MoveNext(), Is.False);
			}
			{ // exactly the chunk size
				var items = RangeArray(20);

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.True);
				var chunk = iterator.Current;
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(0, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.False);
			}
			{ // one more than chunk size
				var items = RangeArray(21);

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.True);
				var chunk = iterator.Current;
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(0, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(20, 1).ToList()));
				Assert.That(iterator.MoveNext(), Is.False);
			}
			{ // last chunk full
				var items = RangeArray(60);

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.True);
				var chunk = iterator.Current;
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(0, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(20, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(40, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.False);
			}
			{ // last chunk not full
				var items = RangeArray(77);

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.True);
				var chunk = iterator.Current;
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(0, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(20, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(40, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(60, 17).ToList()));
				Assert.That(iterator.MoveNext(), Is.False);
			}
		}

		[Test]
		public void Test_Enumerable_Buffered_List()
		{
			List<int> RangeList(int count) => Enumerable.Range(0, count).ToList();

			{ // empty
				var items = RangeList(0);

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.False);
			}
			{ // less than chunk size
				var items = RangeList(7);

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.True);
				var chunk = iterator.Current;
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(0, 7).ToList()));
				Assert.That(iterator.MoveNext(), Is.False);
			}
			{ // exactly the chunk size
				var items = RangeList(20);

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.True);
				var chunk = iterator.Current;
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(0, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.False);
			}
			{ // one more than chunk size
				var items = RangeList(21);

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.True);
				var chunk = iterator.Current;
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(0, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(20, 1).ToList()));
				Assert.That(iterator.MoveNext(), Is.False);
			}
			{ // last chunk full
				var items = RangeList(60);

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.True);
				var chunk = iterator.Current;
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(0, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(20, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(40, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.False);
			}
			{ // last chunk not full
				var items = RangeList(77);

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.True);
				var chunk = iterator.Current;
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(0, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(20, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(40, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(60, 17).ToList()));
				Assert.That(iterator.MoveNext(), Is.False);
			}
		}

		[Test]
		public void Test_Enumerable_Buffered_Countable()
		{
			{ // empty
				var items = Enumerable.Empty<int>();

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.False);
			}
			{ // less than chunk size
				var items = Enumerable.Range(0, 7);

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.True);
				var chunk = iterator.Current;
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(0, 7).ToList()));
				Assert.That(iterator.MoveNext(), Is.False);
			}
			{ // exactly the chunk size
				var items = Enumerable.Range(0, 20);

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.True);
				var chunk = iterator.Current;
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(0, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.False);
			}
			{ // one more than chunk size
				var items = Enumerable.Range(0, 21);

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.True);
				var chunk = iterator.Current;
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(0, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(20, 1).ToList()));
				Assert.That(iterator.MoveNext(), Is.False);
			}
			{ // last chunk full
				var items = Enumerable.Range(0, 60);

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.True);
				var chunk = iterator.Current;
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(0, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(20, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(40, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.False);
			}
			{ // last chunk not full
				var items = Enumerable.Range(0, 77);

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.True);
				var chunk = iterator.Current;
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(0, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(20, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(40, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(60, 17).ToList()));
				Assert.That(iterator.MoveNext(), Is.False);
			}
		}

		[Test]
		public void Test_Enumerable_Buffered_Uncountable()
		{
			IEnumerable<int> RangeUncountable(int count)
			{
				for (int i = 0; i < count; i++)
				{
					yield return i;
				}
			}

			{ // empty
				var items = RangeUncountable(0);

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.False);
			}
			{ // less than chunk size
				var items = RangeUncountable(7);

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.True);
				var chunk = iterator.Current;
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(0, 7).ToList()));
				Assert.That(iterator.MoveNext(), Is.False);
			}
			{ // exactly the chunk size
				var items = RangeUncountable(20);

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.True);
				var chunk = iterator.Current;
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(0, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.False);
			}
			{ // one more than chunk size
				var items = RangeUncountable(21);

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.True);
				var chunk = iterator.Current;
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(0, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(20, 1).ToList()));
				Assert.That(iterator.MoveNext(), Is.False);
			}
			{ // last chunk full
				var items = RangeUncountable(60);

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.True);
				var chunk = iterator.Current;
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(0, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(20, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(40, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.False);
			}
			{ // last chunk not full
				var items = RangeUncountable(77);

				using var iterator = items.Buffered(20).GetEnumerator();

				Assert.That(iterator.MoveNext(), Is.True);
				var chunk = iterator.Current;
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(0, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(20, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(40, 20).ToList()));
				Assert.That(iterator.MoveNext(), Is.True);
				Assert.That(iterator.Current, Is.SameAs(chunk), "Iterator should reuse the same list");
				Assert.That(chunk, Is.EqualTo(Enumerable.Range(60, 17).ToList()));
				Assert.That(iterator.MoveNext(), Is.False);
			}
		}

		[Test]
		public void Test_Record_Items()
		{

			var source = Enumerable.Range(0, 10);

			var before = new List<int>();
			var after = new List<int>();

			var query = source
				.Observe((x) => before.Add(x))
				.Where((x) => x % 2 == 1)
				.Observe((x) => after.Add(x))
				.Select((x) => x + 1);

			Log("query: " + query);

			var results = query.ToList();

			Log($"input : {string.Join(", ", source)}");
			Log($"before: {string.Join(", ", before)}");
			Log($"after : {string.Join(", ", after)}");
			Log($"output: {string.Join(", ", results)}");

			Assert.That(before, Is.EqualTo(Enumerable.Range(0, 10).ToList()));
			Assert.That(after, Is.EqualTo(Enumerable.Range(0, 10).Where(x => x % 2 == 1).ToList()));
			Assert.That(results, Is.EqualTo(Enumerable.Range(1, 5).Select(x => x * 2).ToList()));

		}

	}

}
