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
	using System.Buffers;
	using SnowBank.Data.Tuples;
	using NUnit.Framework;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class ColaRangeDictionaryFacts : SimpleTest
	{

		private void DumpStore<TKey, TValue>(ColaRangeDictionary<TKey, TValue> store)
		{
			var sw = new StringWriter();
			store.Debug_Dump(sw);
			Log(sw);
		}

		[Test]
		public void Test_Empty_RangeDictionary()
		{
			using var cola = new ColaRangeDictionary<int, string>(0, Comparer<int>.Default, StringComparer.Ordinal);

			Assert.Multiple(() =>
			{
				Assert.That(cola.Count, Is.EqualTo(0));
				Assert.That(cola.KeyComparer, Is.SameAs(Comparer<int>.Default));
				Assert.That(cola.ValueComparer, Is.SameAs(StringComparer.Ordinal));
				Assert.That(cola.Capacity, Is.EqualTo(31), "Capacity should be the next power of 2, minus 1");
				Assert.That(cola.Bounds, Is.Not.Null);
				Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
				Assert.That(cola.Bounds.End, Is.EqualTo(0));
			});
		}

		[Test]
		public void Test_RangeDictionary_Insert_Single()
		{
			using var cola = new ColaRangeDictionary<int, string>();

			cola.Mark(0, 1, "A");
			Assert.That(cola.Count, Is.EqualTo(1));

			var items = cola.ToArray();
			Assert.That(items.Length, Is.EqualTo(1));
			Assert.That(items[0].Begin, Is.EqualTo(0));
			Assert.That(items[0].End, Is.EqualTo(1));
			Assert.That(items[0].Value, Is.EqualTo("A"));
		}

		[Test]
		public void Test_RangeDictionary_Insert_In_Order_Non_Overlapping()
		{
			using var cola = new ColaRangeDictionary<int, string>();

			cola.Mark(0, 1, "A");
			Log($"FIRST  = {{ {string.Join(", ", cola)} }}");
			Log($"Bounds = {cola.Bounds}");
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(1));

			cola.Mark(2, 3, "B");
			Log($"SECOND = {{ {string.Join(", ", cola)} }}");
			Log($"Bounds = {cola.Bounds}");
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(2));

			cola.Mark(4, 5, "C");
			Log($"THIRD  = {{ {string.Join(", ", cola)} }}");
			Log($"Bounds = {cola.Bounds}");
			DumpStore(cola);

			Assert.That(cola.Count, Is.EqualTo(3));
			var runs = cola.ToArray();
			Assert.That(runs.Length, Is.EqualTo(3));

			Assert.That(runs[0].Begin, Is.EqualTo(0));
			Assert.That(runs[0].End, Is.EqualTo(1));
			Assert.That(runs[0].Value, Is.EqualTo("A"));

			Assert.That(runs[1].Begin, Is.EqualTo(2));
			Assert.That(runs[1].End, Is.EqualTo(3));
			Assert.That(runs[1].Value, Is.EqualTo("B"));

			Assert.That(runs[2].Begin, Is.EqualTo(4));
			Assert.That(runs[2].End, Is.EqualTo(5));
			Assert.That(runs[2].Value, Is.EqualTo("C"));

			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(5));
		}

		[Test]
		public void Test_RangeDictionary_Insert_Out_Of_Order_Non_Overlapping()
		{
			using var cola = new ColaRangeDictionary<int, string>();

			cola.Mark(0, 1, "A");
			Log($"FIRST  = {{ {string.Join(", ", cola)} }}");
			Log($"Bounds = {cola.Bounds}");
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(1));

			cola.Mark(4, 5, "B");
			Log($"SECOND = {{ {string.Join(", ", cola)} }}");
			Log($"Bounds = {cola.Bounds}");
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(2));

			cola.Mark(2, 3, "C");
			Log($"THIRD  = {{ {string.Join(", ", cola)} }}");
			Log($"Bounds = {cola.Bounds}");
			DumpStore(cola);

			Assert.That(cola.Count, Is.EqualTo(3));
			var runs = cola.ToArray();
			Assert.That(runs.Length, Is.EqualTo(3));

			Assert.That(runs[0].Begin, Is.EqualTo(0));
			Assert.That(runs[0].End, Is.EqualTo(1));
			Assert.That(runs[0].Value, Is.EqualTo("A"));

			Assert.That(runs[1].Begin, Is.EqualTo(2));
			Assert.That(runs[1].End, Is.EqualTo(3));
			Assert.That(runs[1].Value, Is.EqualTo("C"));

			Assert.That(runs[2].Begin, Is.EqualTo(4));
			Assert.That(runs[2].End, Is.EqualTo(5));
			Assert.That(runs[2].Value, Is.EqualTo("B"));

			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(5));

		}
		[Test]
		public void Test_RangeDictionary_Insert_Partially_Overlapping()
		{
			using var cola = new ColaRangeDictionary<int, string>();

			cola.Mark(0, 1, "A");
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(1));

			cola.Mark(0, 2, "B");
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(1));

			cola.Mark(1, 3, "C");
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(2));

			cola.Mark(-1, 2, "D");
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(2));

			Log($"Result = {{ {string.Join(", ", cola)} }}");
			Log($"Bounds = {cola.Bounds}");
		}

		[Test]
		public void Test_RangeDictionary_Insert_Completely_Overlapping()
		{
			using var cola = new ColaRangeDictionary<int, string>();

			cola.Mark(4, 5, "A");
			Log($"BEFORE = {{ {string.Join(", ", cola)} }}");
			Log($"Bounds = {cola.Bounds}");
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(1));
			Assert.That(cola.Bounds.Begin, Is.EqualTo(4));
			Assert.That(cola.Bounds.End, Is.EqualTo(5));

			// overlaps all the ranges at once
			// 0123456789   0123456789   0123456789
			// ____A_____ + BBBBBBBBBB = BBBBBBBBBB
			cola.Mark(0, 10, "B");
			Log($"AFTER  = {{ {string.Join(", ", cola)} }}");
			Log($"Bounds = {cola.Bounds}");
			DumpStore(cola);

			Assert.That(cola.Count, Is.EqualTo(1));
			var runs = cola.ToArray();
			Assert.That(runs.Length, Is.EqualTo(1));
			Assert.That(runs[0].Begin, Is.EqualTo(0));
			Assert.That(runs[0].End, Is.EqualTo(10));
			Assert.That(runs[0].Value, Is.EqualTo("B"));

			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(10));
		}

		[Test]
		public void Test_RangeDictionary_Insert_Contained()
		{
			using var cola = new ColaRangeDictionary<int, string>();

			cola.Mark(0, 10, "A");
			Log($"BEFORE = {{ {string.Join(", ", cola)} }}");
			Log($"Bounds = {cola.Bounds}");
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(1));
			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(10));

			// overlaps all the ranges at once

			// 0123456789   0123456789   0123456789
			// AAAAAAAAAA + ____B_____ = AAAABAAAAA
			cola.Mark(4, 5, "B");
			Log($"AFTER  = {{ {string.Join(", ", cola)} }}");
			Log($"Bounds = {cola.Bounds}");
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(3));
			var items = cola.ToArray();
			Assert.That(items.Length, Is.EqualTo(3));

			Assert.That(items[0].Begin, Is.EqualTo(0));
			Assert.That(items[0].End, Is.EqualTo(4));
			Assert.That(items[0].Value, Is.EqualTo("A"));

			Assert.That(items[1].Begin, Is.EqualTo(4));
			Assert.That(items[1].End, Is.EqualTo(5));
			Assert.That(items[1].Value, Is.EqualTo("B"));

			Assert.That(items[2].Begin, Is.EqualTo(5));
			Assert.That(items[2].End, Is.EqualTo(10));
			Assert.That(items[2].Value, Is.EqualTo("A"));

			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(10));
		}

		[Test]
		public void Test_RangeDictionary_Insert_That_Fits_Between_Two_Ranges()
		{
			using var cola = new ColaRangeDictionary<int, string>();

			cola.Mark(0, 1, "A");
			cola.Mark(2, 3, "B");
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(2));

			cola.Mark(1, 2, "C");
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(3));

			Log($"Result = {{ {string.Join(", ", cola)} }}");
			Log($"Bounds = {cola.Bounds}");

			var items = cola.ToArray();
			Assert.That(items.Length, Is.EqualTo(3));

			Assert.That(items[0].Begin, Is.EqualTo(0));
			Assert.That(items[0].End, Is.EqualTo(1));
			Assert.That(items[0].Value, Is.EqualTo("A"));

			Assert.That(items[1].Begin, Is.EqualTo(1));
			Assert.That(items[1].End, Is.EqualTo(2));
			Assert.That(items[1].Value, Is.EqualTo("C"));

			Assert.That(items[2].Begin, Is.EqualTo(2));
			Assert.That(items[2].End, Is.EqualTo(3));
			Assert.That(items[2].Value, Is.EqualTo("B"));

		}

		[Test]
		public void Test_RangeDictionary_Insert_That_Join_Two_Ranges()
		{
			using var cola = new ColaRangeDictionary<int, string>();

			cola.Mark(0, 1, "A");
			cola.Mark(2, 3, "A");
			Log($"BEFORE = {{ {string.Join(", ", cola)} }}");
			Log($"Bounds = {cola.Bounds}");
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(2));

			// A_A_ + _A__ = AAA_
			cola.Mark(1, 2, "A");
			Log($"AFTER  = {{ {string.Join(", ", cola)} }}");
			Log($"Bounds = {cola.Bounds}");
			DumpStore(cola);

			Assert.That(cola.Count, Is.EqualTo(1));
			var runs = cola.ToArray();
			Assert.That(runs[0].Begin, Is.EqualTo(0));
			Assert.That(runs[0].End, Is.EqualTo(3));
			Assert.That(runs[0].Value, Is.EqualTo("A"));

		}

		[Test]
		public void Test_RangeDictionary_Insert_That_Replace_All_Ranges()
		{
			using var cola = new ColaRangeDictionary<int, string>();
			cola.Mark(0, 1, "A");
			cola.Mark(2, 3, "A");
			cola.Mark(4, 5, "A");
			cola.Mark(6, 7, "A");
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(4));
			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(7));

			cola.Mark(-1, 10, "B");
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(1));
			Assert.That(cola.Bounds.Begin, Is.EqualTo(-1));
			Assert.That(cola.Bounds.End, Is.EqualTo(10));

			Log($"Result = {{ {string.Join(", ", cola)} }}");
			Log($"Bounds = {cola.Bounds}");
		}

		[Test]
		public void Test_RangeDictionary_Insert_Backwards()
		{
			const int N = 100;

			using var cola = new ColaRangeDictionary<int, string>();

			for(int i = N; i > 0; i--)
			{
				int x = i << 1;
				cola.Mark(x - 1, x, i.ToString());
			}

			Assert.That(cola.Count, Is.EqualTo(N));

			Log($"Result = {{ {string.Join(", ", cola)} }}");
			Log($"Bounds = {cola.Bounds}");
		}

		[Test]
		[Ignore("BUGBUG: The implementation is currently buggy!")]
		public void Test_RangeDictionary_Can_Remove()
		{
			// > Items: 5 (10 ~ 15, True), (20 ~ 50, False), (51 ~ 62, True), ( 63 ~ 65, True), (68 ~ 75, False)
			// > Levels: 3 used, 5 allocated
			// -  0|#: (68 ~ 75, False)
			// -  1|_: 
			// -  2|#: (10 ~ 15, True), (20 ~ 50, False), (51 ~ 62, True), (63 ~ 65, True)

			// Legent: '#' = True, '-' = False
			// 0         1         2         3         4         5         6         7         
			// 01234567890123456789012345678901234567890123456789012345678901234567890123456789
			//           #####     ------------------------------ ########### ##   --------        

			Log("# Initial State:");
			{
				using var store = GetFilledRange();
				DumpStore(store);
			}

			{
				Log("# Remove [0, 100), Shift(-100)");

				// 0         1         2         3         4         5         6         7         
				// 01234567890123456789012345678901234567890123456789012345678901234567890123456789
				//           #####     ------------------------------ ########### ##   --------        
				// xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx....

				using var store = GetFilledRange();
				store.Remove(0, 100, -100, (x, y) => x + y);
				DumpStore(store);
				Assert.That(store.Count, Is.EqualTo(0));
			}

			{
				Log("# Remove [0, 12), Apply -12");

				// 0         1         2         3         4         5         6         7         
				// 01234567890123456789012345678901234567890123456789012345678901234567890123456789
				//           #####     ------------------------------ ########### ##   --------    
				// XXXXXXXXXXXX
				// ###     ------------------------------ ########### ##   --------                

				using var store = GetFilledRange();
				store.Remove(0, 12, -12, (x, y) => x + y);
				DumpStore(store);
				Assert.That(store.Count, Is.EqualTo(5));
				int i = 0;
				foreach (var entry in store)
				{
					if (i == 0) CompareEntries(entry, 0, 3, true);
					else if (i == 1) CompareEntries(entry, 8, 38, false);
					else if (i == 2) CompareEntries(entry, 39, 50, true);
					else if (i == 3) CompareEntries(entry, 51, 53, true);
					else if (i == 4) CompareEntries(entry, 56, 63, false);
					i++;
				}
			}

			{
				Log("# Remove [12, 55), Apply -43");

				// 0         1         2         3         4         5         6         7         
				// 01234567890123456789012345678901234567890123456789012345678901234567890123456789
				//           #####     ------------------------------ ########### ##   --------    
				//             XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
				//           ######### ##   --------                                               

				using var store = GetFilledRange();
				store.Remove(12, 55, -43, (x, y) => x + y);
				DumpStore(store);
				Assert.That(store.Count, Is.EqualTo(4));
				int i = 0;
				foreach (var entry in store)
				{
					if (i == 0) CompareEntries(entry, 10, 12, true);
					if (i == 1) CompareEntries(entry, 12, 19, true);
					else if (i == 2) CompareEntries(entry, 20, 22, true);
					else if (i == 3) CompareEntries(entry, 25, 32, false);
					i++;
				}
			}

			{
				Log("# D");

				using var store = GetFilledRange();
				store.Remove(0, 8, -8, (x, y) => x + y);
				DumpStore(store);
				Assert.That(store.Count, Is.EqualTo(5));
				int i = 0;
				foreach (var entry in store)
				{
					if (i == 0) CompareEntries(entry, 2, 7, true);
					else if (i == 1) CompareEntries(entry, 12, 42, false);
					else if (i == 2) CompareEntries(entry, 43, 54, true);
					else if (i == 3) CompareEntries(entry, 55, 57, true);
					else if (i == 4) CompareEntries(entry, 60, 67, false);
					i++;
				}
			}

			{
				Log("# E");

				using var store = GetFilledRange();
				store.Remove(20, 62, -42, (x, y) => x + y);
				DumpStore(store);
				Assert.That(store.Count, Is.EqualTo(3));
				int i = 0;
				foreach (var entry in store)
				{
					if (i == 0) CompareEntries(entry, 10, 15, true);
					else if (i == 1) CompareEntries(entry, 21, 23, true);
					else if (i == 2) CompareEntries(entry, 26, 33, false);
					i++;
				}
			}

			{
				Log("# F");

				using var store = GetFilledRange();
				store.Remove(0, 50, -50, (x, y) => x + y);
				DumpStore(store);
				Assert.That(store.Count, Is.EqualTo(3));
				int i = 0;
				foreach (var entry in store)
				{
					if (i == 0) CompareEntries(entry, 1, 12, true);
					else if (i == 1) CompareEntries(entry, 13, 15, true);
					else if (i == 2) CompareEntries(entry, 18, 25, false);
					i++;
				}
			}

			{
				Log("# G");

				using var store = GetFilledRange();
				store.Remove(0, 15, -15, (x, y) => x + y);
				DumpStore(store);
				Assert.That(store.Count, Is.EqualTo(4));
				int i = 0;
				foreach (var entry in store)
				{
					if (i == 0) CompareEntries(entry, 5, 35, false);
					else if (i == 1) CompareEntries(entry, 36, 47, true);
					else if (i == 2) CompareEntries(entry, 48, 50, true);
					else if (i == 3) CompareEntries(entry, 53, 60, false);
					i++;
				}
			}

			{
				Log("# H");

				// 0         1         2         3         4         5         6         7         
				// 01234567890123456789012345678901234567890123456789012345678901234567890123456789
				//           #####     ------------------------------ ########### ##   --------    
				// XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX                    
				// ## ##   --------                                                                

				using var store = GetFilledRange();
				store.Remove(0, 60, -60, (x, y) => x + y);
				DumpStore(store);
				Assert.That(store.Count, Is.EqualTo(3));
				int i = 0;
				foreach (var entry in store)
				{
					if (i == 0) CompareEntries(entry, 0, 2, true);
					else if (i == 1) CompareEntries(entry, 3, 5, true);
					else if (i == 2) CompareEntries(entry, 8, 15, false);
					i++;
				}
			}

			{
				Log("# I");
				using var store = GetFilledRange();
				store.Remove(0, 51, -51, (x, y) => x + y);
				DumpStore(store);
				Assert.That(store.Count, Is.EqualTo(3));
				int i = 0;
				foreach (var entry in store)
				{
					if (i == 0) CompareEntries(entry, 0, 11, true);
					else if (i == 1) CompareEntries(entry, 12, 14, true);
					else if (i == 2) CompareEntries(entry, 17, 24, false);
					i++;
				}
			}

			{
				Log("# J");

				using var store = GetFilledRange();
				store.Remove(20, 30, -10, (x, y) => x + y);
				DumpStore(store);
				Assert.That(store.Count, Is.EqualTo(5));
				int i = 0;
				foreach (var entry in store)
				{
					if (i == 0) CompareEntries(entry, 10, 15, true);
					else if (i == 1) CompareEntries(entry, 20, 40, false);
					else if (i == 2) CompareEntries(entry, 41, 52, true);
					else if (i == 3) CompareEntries(entry, 53, 55, true);
					else if (i == 4) CompareEntries(entry, 58, 65, false);
					i++;
				}
			}

			{
				Log("# K");

				using var store = GetFilledRange();
				store.Remove(20, 50, -30, (x, y) => x + y);
				DumpStore(store);
				Assert.That(store.Count, Is.EqualTo(4));
				int i = 0;
				foreach (var entry in store)
				{
					if (i == 0) CompareEntries(entry, 10, 15, true);
					else if (i == 1) CompareEntries(entry, 21, 32, true);
					else if (i == 2) CompareEntries(entry, 33, 35, true);
					else if (i == 3) CompareEntries(entry, 38, 45, false);
					i++;
				}
			}

			{
				Log("# L");

				using var store = GetFilledRange();
				store.Remove(10, 30, -20, (x, y) => x + y);
				DumpStore(store);
				Assert.That(store.Count, Is.EqualTo(4));
				int i = 0;
				foreach (var entry in store)
				{
					if (i == 0) CompareEntries(entry, 10, 30, false);
					else if (i == 1) CompareEntries(entry, 31, 42, true);
					else if (i == 2) CompareEntries(entry, 43, 45, true);
					else if (i == 3) CompareEntries(entry, 48, 55, false);
					i++;
				}
			}

			{
				Log("# M");

				using var store = GetFilledRange();
				store.Remove(30, 40, -10, (x, y) => x + y);
				DumpStore(store);
				Assert.That(store.Count, Is.EqualTo(6));
				int i = 0;
				foreach (var entry in store)
				{
					if (i == 0) CompareEntries(entry, 10, 15, true);
					else if (i == 1) CompareEntries(entry, 20, 30, false);
					else if (i == 2) CompareEntries(entry, 30, 40, false);
					else if (i == 3) CompareEntries(entry, 41, 52, true);
					else if (i == 4) CompareEntries(entry, 53, 55, true);
					else if (i == 5) CompareEntries(entry, 58, 65, false);
					i++;
				}
			}

			{
				Log("# N");

				using var store = GetFilledRange();
				store.Remove(30, 50, -20, (x, y) => x + y);
				DumpStore(store);
				Assert.That(store.Count, Is.EqualTo(5));
				int i = 0;
				foreach (var entry in store)
				{
					if (i == 0) CompareEntries(entry, 10, 15, true);
					else if (i == 1) CompareEntries(entry, 20, 30, false);
					else if (i == 2) CompareEntries(entry, 31, 42, true);
					else if (i == 3) CompareEntries(entry, 43, 45, true);
					else if (i == 4) CompareEntries(entry, 48, 55, false);
					i++;
				}
			}
		}

		public void CompareEntries<TKey, TValue>(ColaRangeDictionary<TKey, TValue>.Entry entry, TKey beginInclusive, TKey endExclusive, TValue value)
		{
			Assert.That(entry.Begin, Is.EqualTo(beginInclusive));
			Assert.That(entry.End, Is.EqualTo(endExclusive));
			Assert.That(entry.Value, Is.EqualTo(value));
		}

		public ColaRangeDictionary<int, bool> GetFilledRange()
		{
			//returns a colaRange prefilled with this :
			// {10-15, true}
			// {20-50, false}
			// {51,62, true}
			// {63,65, true}
			// {68,75, false}

			var cola = new ColaRangeDictionary<int, bool>(ArrayPool<ColaRangeDictionary<int, bool>.Entry>.Shared);
			cola.Mark(10, 15, true);
			cola.Mark(20, 50, false);
			cola.Mark(51, 62, true);
			cola.Mark(63, 65, true);
			cola.Mark(68, 75, false);

			return cola;
		}

		enum RangeColor
		{
			Black,
			White
		}

		[Test]
		public void Test_RangeDictionary_Black_And_White()
		{
			// we have a space from 0 <= x < 100 that is empty
			// we insert a random serie of ranges that are either Black or White
			// after each run, we check that all ranges are correctly ordered, merged, and so on.

			const int S = 100; // [0, 100)
			const int N = 1000; // number of repetitions
			const int K = 25; // max number of ranges inserted per run

			var rnd = new Random();
			int seed = rnd.Next();
			Log($"Using random seed {seed}");
			rnd = new Random(seed);

			for(int i = 0; i < N; i++)
			{
				using var cola = new ColaRangeDictionary<int, RangeColor>(ArrayPool<ColaRangeDictionary<int, RangeColor>.Entry>.Shared);

				var witnessColors = new RangeColor?[S];
				var witnessIndexes = new int?[S];

				// choose a random number of ranges
				int k = rnd.Next(3, K);

				Log();
				Log($"# Starting run {i} with {k} insertions");

				int p = 0;
				for(int j = 0; j<k;j++)
				{
					var begin = rnd.Next(S);
					// 50/50 of inserting a single element, or a range
					var end = (rnd.Next(2) == 1 ? begin : rnd.Next(2) == 1 ? rnd.Next(begin, S) : Math.Min(S - 1, begin + rnd.Next(5))) + 1; // reminder: +1 because 'end' is EXCLUDED 
					Assert.That(begin, Is.LessThan(end));
					// 50/50 for the coloring
					var color = rnd.Next(2) == 1 ? RangeColor.White : RangeColor.Black;

					// uncomment this line if you want to reproduce this exact run
					//Log("\t\tcola.Mark(" + begin + ", " + end + ", RangeColor." + color + ");");

					cola.Mark(begin, end, color);
					for(int z = begin; z < end; z++)
					{
						witnessColors[z] = color;
						witnessIndexes[z] = p;
					}

					//Log(" >        |{0}|", String.Join("", witnessIndexes.Select(x => x.HasValue ? (char)('A' + x.Value) : ' ')));
					Debug.WriteLine("          |{0}| + [{1,2}, {2,2}) = {3} > #{4,2} [ {5} ]", string.Join("", witnessColors.Select(w => !w.HasValue ? ' ' : w.Value == RangeColor.Black ? '#' : '°')), begin, end, color, cola.Count, string.Join(", ", cola));

					++p;
				}

				// pack the witness list into ranges
				var witnessRanges = new List<STuple<int, int, RangeColor>>();
				RangeColor? prev = null;
				p = 0;
				for (int z = 1; z < S;z++)
				{
					if (witnessColors[z] != prev)
					{ // switch

						if (prev.HasValue)
						{
							witnessRanges.Add(STuple.Create(p, z, prev.Value));
						}
						p = z;
						prev = witnessColors[z];
					}
				}

				Log($"> RANGES: #{cola.Count,2} [ {string.Join(", ", cola)} ]");
				Log($"          #{witnessRanges.Count,2} [ {string.Join(", ", witnessRanges)} ]");

				var counter = new int[S];
				var observedIndexes = new int?[S];
				var observedColors = new RangeColor?[S];
				p = 0;
				foreach(var range in cola)
				{
					Assert.That(range.Begin, Is.LessThan(range.End), $"Begin < End {range}");
					for (int z = range.Begin; z < range.End; z++)
					{
						observedIndexes[z] = p;
						counter[z]++;
						observedColors[z] = range.Value;
					}
					++p;
				}

				Log($"> INDEXS: |{string.Join("", observedIndexes.Select(x => x.HasValue ? (char) ('A' + x.Value) : ' '))}|");
				Log($"          |{string.Join("", witnessIndexes.Select(x => x.HasValue ? (char) ('A' + x.Value) : ' '))}|");

				Log($"> COLORS: |{string.Join("", observedColors.Select(w => !w.HasValue ? ' ' : w.Value == RangeColor.Black ? '#' : '°'))}|");
				Log($"          |{string.Join("", witnessColors.Select(w => !w.HasValue ? ' ' : w.Value == RangeColor.Black ? '#' : '°'))}|");

				// verify the colors
				foreach(var range in cola)
				{
					for (int z = range.Begin; z < range.End; z++)
					{
						Assert.That(range.Value, Is.EqualTo(witnessColors[z]), $"#{z} color mismatch for {range}");
						Assert.That(counter[z], Is.EqualTo(1), $"Duplicate at offset #{z} for {range}");
					}
				}

				// verify that nothing was missed
				for(int z = 0; z < S; z++)
				{
					if (witnessColors[z] == null)
					{
						if (counter[z] != 0) Log($"@ FAIL!!! |{new string('-', z)}^");
						Assert.That(counter[z], Is.EqualTo(0), $"Should be void at offset {z}");
					}
					else
					{
						if (counter[z] != 1) Log($"@ FAIL!!! |{new string('-', z)}^");
						Assert.That(counter[z], Is.EqualTo(1), string.Format("Should be filled with {1} at offset {0}", z, witnessColors[z]));
					}
				}
			}
		}

		[Test]
		public void Test_RangeDictionary_Insert_Random_Ranges()
		{
			const int N = 1000;
			const int K = 1000 * 1000;

			using var cola = new ColaRangeDictionary<int, int>();

			var rnd = new Random();
			int seed = 2040305906; // rnd.Next();
			Log($"seed {seed}");
			rnd = new Random(seed);

			int[] expected = new int[N];

			var sw = Stopwatch.StartNew();
			for (int i = 0; i < K; i++)
			{
				if (rnd.Next(10000) < 42)
				{
					//Log("Clear");
					cola.Clear();
				}
				else
				{

					int x = rnd.Next(N);
					int y = rnd.Next(2) == 0 ? x + 1 : rnd.Next(N);
					if (y == x) ++y;
					if (x <= y)
					{
						//Log();
						//Log("Add " + x + " ~ " + y + " = " + i);
						cola.Mark(x, y, i);
					}
					else
					{
						//Log();
						//Log("ddA " + y + " ~ " + x + " = " + i);
						cola.Mark(y, x, i);
					}
				}
				//Log("  = " + cola + " -- <> = " + cola.Bounds);
				//cola.Debug_Dump();

			}
			sw.Stop();

			Log($"Inserted {K:N0} random ranges in {sw.Elapsed.TotalSeconds:N3} sec");
			DumpStore(cola);

			Log($"Result = {cola}");
			Log($"Bounds = {cola.Bounds}");

		}

	}

}

#endif
