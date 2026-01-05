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
	using SnowBank.Data.Tuples;
	using NUnit.Framework;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class ColaOrderedSetFacts : SimpleTest
	{
		private void DumpStore<TKey>(ColaOrderedSet<TKey> store)
		{
			var sw = new StringWriter();
			store.Debug_Dump(sw);
			Log(sw);
		}

		[Test]
		public void Test_Empty_ColaSet()
		{
			var cola = new ColaOrderedSet<string>(42, StringComparer.Ordinal);
			Assert.That(cola.Count, Is.EqualTo(0));
			Assert.That(cola.Comparer, Is.SameAs(StringComparer.Ordinal));
			Assert.That(cola.Capacity, Is.EqualTo(63), "Capacity should be the next power of 2, minus 1");
		}

		[Test]
		public void Test_Capacity_Is_Rounded_Up()
		{
			// default capacity is 4 levels, for 31 items max
			var cola = new ColaOrderedSet<string>();
			Assert.That(cola.Capacity, Is.EqualTo(31));

			// 63 items completely fill 5 levels
			cola = new ColaOrderedSet<string>(63);
			Assert.That(cola.Capacity, Is.EqualTo(63));

			// 64 items need 6 levels, which can hold up to 127 items
			cola = new ColaOrderedSet<string>(64);
			Assert.That(cola.Capacity, Is.EqualTo(127));
		}

		[Test]
		public void Test_ColaOrderedSet_Add()
		{
			var cola = new ColaOrderedSet<int?>();
			Assert.That(cola.Count, Is.EqualTo(0));

			cola.Add(42);
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(1));
			Assert.That(cola.Contains(42), Is.True);

			cola.Add(1);
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(2));
			Assert.That(cola.Contains(1), Is.True);

			cola.Add(66);
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(3));
			Assert.That(cola.Contains(66), Is.True);

			cola.Add(123);
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(4));
			Assert.That(cola.Contains(123), Is.True);

			cola.Add(-77);
			cola.Add(-76);
			cola.Add(-75);
			cola.Add(-74);
			cola.Add(-73);
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(9));
		}

		[Test]
		public void Test_ColaOrderedSet_Remove()
		{
			const int N = 1000;

			var cola = new ColaOrderedSet<int>();
			var list = new List<int>();

			for (int i = 0; i < N;i++)
			{
				cola.Add(i);
				list.Add(i);
			}
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(N));

			for (int i = 0; i < N; i++)
			{
				Assert.That(cola.Contains(list[i]), Is.True, $"{list[i]} is missing (offset {i})");
			}

			var rnd = new Random();
			int seed = 1073704892; // rnd.Next();
			Log($"Seed: {seed}");
			rnd = new Random(seed);
			int old = -1;
			while (list.Count > 0)
			{
				int p = rnd.Next(list.Count);
				int x = list[p]; 
				if (!cola.Contains(x))
				{
					Assert.Fail($"{x} disappeared after removing {old} ?");
				}

				bool res = cola.Remove(x);
				Assert.That(res, Is.True, $"Removing {x} did nothing");
				//Assert.That(cola.Contains(191), "blah {0}", x);

				list.RemoveAt(p);
				Assert.That(cola.Count, Is.EqualTo(list.Count));
				old = x;
			}

			DumpStore(cola);
		}

		[Test]
		public void Test_CopyTo_Return_Ordered_Values()
		{
			const int N = 1000;
			var rnd = new Random();

			var cola = new ColaOrderedSet<int>();

			// create a list of random values
			var numbers = new int[N];
			for (int i = 0, x = 0; i < N; i++) numbers[i] = (x += 1 + rnd.Next(10));

			// insert the list in a random order
			var list = new List<int>(numbers);
			while(list.Count > 0)
			{
				int p = rnd.Next(list.Count);
				cola.Add(list[p]);
				list.RemoveAt(p);
			}
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(N));

			// we can now sort the numbers to get a reference sequence
			Array.Sort(numbers);

			// foreach()ing should return the value in natural order
			list.Clear();
			foreach (var x in cola) list.Add(x);
			Assert.That(list.Count, Is.EqualTo(N));
			Assert.That(list, Is.EqualTo(numbers));

			// CopyTo() should produce the item in the expected order
			var tmp = new int[N];
			cola.CopyTo(tmp);
			Assert.That(tmp, Is.EqualTo(numbers));

			// ToArray() should do the same thing
			tmp = cola.ToArray();
			Assert.That(tmp, Is.EqualTo(numbers));

		}

		[Test]
		public void Test_Check_Costs()
		{
			const int N = 100;
			var cmp = new CountingComparer<Slice>(Comparer<Slice>.Default);
			var cola = new ColaOrderedSet<Slice>(cmp);

			Log($"Parameters: N = {N}, Log(N) = {Math.Log(N)}, Log2(N) = {Math.Log(N, 2)}, N.Log2(N) = {N * Math.Log(N, 2)}");

			Log($"Inserting ({N} items)");
			for (int i = 0; i < N; i++)
			{
				cola.Add(TuPack.EncodeKey(i << 1));
			}

			Log($"> {cmp.Count} cmps ({((double) cmp.Count / N)} / insert)");
			DumpStore(cola);

			Log($"Full scan ({(N << 1)} lookups)");
			cmp.Reset();
			int n = 0;
			for (int i = 0; i < (N << 1); i++)
			{
				if (cola.Contains(TuPack.EncodeKey(i))) ++n;
			}
			Assert.That(n, Is.EqualTo(N));
			Log($"> {cmp.Count} cmps ({((double) cmp.Count / (N << 1))} / lookup)");

			cmp.Reset();
			n = 0;
			int tail = Math.Min(16, N >> 1);
			int offset = N - tail;
			Log($"Tail scan ({tail} lookups)");
			for (int i = 0; i < tail; i++)
			{
				if (cola.Contains(TuPack.EncodeKey(offset + i))) ++n;
			}
			Log($"> {cmp.Count} cmps ({((double) cmp.Count / tail)} / lookup)");

			Log("ForEach");
			cmp.Reset();
			int p = 0;
			foreach(var x in cola)
			{
				Assert.That(TuPack.DecodeKey<int>(x), Is.EqualTo(p << 1));
				++p;
			}
			Assert.That(p, Is.EqualTo(N));
			Log($"> {cmp.Count} cmps ({((double) cmp.Count / N)} / item)");
		}

		[Test]
		[Category("Benchmark")]
		[Category("LongRunning")]
		public void Test_MiniBench()
		{
			const int N = 10 * 1000 * 1000;

			var rnd = new Random();
			long x;


			//WARMUP
			var store = new ColaOrderedSet<long>();
			store.Add(1);
			store.Add(42);
			store.Add(1234);
			store.TryGetValue(42, out x);
			store.TryGetValue(404, out x);

			#region Sequentially inserted....

			Log($"Inserting {N:N0} sequential key/value pairs into a COLA ordered set");
			GC.Collect();
			store = new ColaOrderedSet<long>();
			long total = 0;
			var sw = Stopwatch.StartNew();
			for (int i = 0; i < N; i++)
			{
				store.Add(i);
				Interlocked.Increment(ref total);
				if (i % (N / 10) == 0) Console.Write(".");
			}
			sw.Stop();

			Log("done");
			Log($"* Inserted: {total:N0} keys");
			Log($"* Elapsed : {sw.Elapsed.TotalSeconds:N3} sec");
			Log($"* KPS: {(total / sw.Elapsed.TotalSeconds):N0} key/sec");
			Log($"* Latency : {(sw.Elapsed.TotalMilliseconds * 1000000 / total):N1} nanos / insert");

			// sequential reads

			sw.Restart();
			for (int i = 0; i < total; i++)
			{
				var _ = store.TryGetValue(i, out x);
				if (!_ || x != i) Assert.Fail();
			}
			sw.Stop();
			Log($"SeqReadOrdered: {total:N0} keys in {sw.Elapsed.TotalSeconds:N3} sec => {(total / sw.Elapsed.TotalSeconds):N0} kps");

			// random reads

			sw.Restart();
			for (int i = 0; i < total; i++)
			{
				var _ = store.TryGetValue(rnd.Next(N), out x);
				if (!_) Assert.Fail();
			}
			sw.Stop();
			Log($"RndReadOrdered: {total:N0} keys in {sw.Elapsed.TotalSeconds:N3} sec => {(total / sw.Elapsed.TotalSeconds):N0} kps");

			#endregion

			#region Randomly inserted....

			Log("(preparing random insert list)");

			var tmp = new long[N];
			var values = new long[N];
			for (int i = 0; i < N; i++)
			{
				tmp[i] = rnd.Next(N);
				values[i] = i;
			}
			Array.Sort(tmp, values);

			Log($"Inserting {N:N0} sequential keys into a COLA store");
			GC.Collect();
			store = new ColaOrderedSet<long>();
			total = 0;
			sw.Restart();
			for (int i = 0; i < N; i++)
			{
				store.Add(values[i]);
				Interlocked.Increment(ref total);
				if (i % (N / 10) == 0) Console.Write(".");
			}
			sw.Stop();

			Log("done");
			Log($"* Inserted: {total:N0} keys");
			Log($"* Elapsed : {sw.Elapsed.TotalSeconds:N3} sec");
			Log($"* KPS     : {(total / sw.Elapsed.TotalSeconds):N0} key/sec");
			Log($"* Latency : {(sw.Elapsed.TotalMilliseconds * 1000000 / total):N1} nanos / insert");


			// sequential reads

			sw.Restart();
			for (int i = 0; i < total; i++)
			{
				var _ = store.TryGetValue(i, out x);
				if (!_ || x != i) Assert.Fail();
			}
			sw.Stop();
			Log($"SeqReadUnordered: {total:N0} keys in {sw.Elapsed.TotalSeconds:N3} sec => {(total / sw.Elapsed.TotalSeconds):N0} kps");

			// random reads

			sw.Restart();
			for (int i = 0; i < total; i++)
			{
				var _ = store.TryGetValue(rnd.Next(N), out x);
				if (!_) Assert.Fail();
			}
			sw.Stop();
			Log($"RndReadUnordered: {total:N0} keys in {sw.Elapsed.TotalSeconds:N3} sec => {(total / sw.Elapsed.TotalSeconds):N0} kps");

			#endregion

		}

	}

}
