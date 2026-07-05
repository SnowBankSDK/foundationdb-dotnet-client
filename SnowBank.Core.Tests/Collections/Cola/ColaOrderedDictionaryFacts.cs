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
	public class ColaOrderedDictionaryFacts : SimpleTest
	{

		[Test]
		public void Test_Empty_Dictionary()
		{
			var cola = new ColaOrderedDictionary<int, string>(42, Comparer<int>.Default, StringComparer.Ordinal);
			Assert.That(cola.Count, Is.EqualTo(0));
			Assert.That(cola.KeyComparer, Is.SameAs(Comparer<int>.Default));
			Assert.That(cola.ValueComparer, Is.SameAs(StringComparer.Ordinal));
			Assert.That(cola.Capacity, Is.EqualTo(63), "Capacity should be the next power of 2, minus 1");
		}

		private void DumpStore<TKey, TValue>(ColaOrderedDictionary<TKey, TValue> store)
		{
			var sw = new StringWriter();
			store.Debug_Dump(sw);
			Log(sw);
		}

		[Test]
		public void Test_ColaOrderedDictionary_Add()
		{
			var cmp = new CountingComparer<int>();

			var cola = new ColaOrderedDictionary<int, string>(cmp);
			Assert.That(cola.Count, Is.EqualTo(0));

			cola.Add(42, "42");
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(1));
			Assert.That(cola.ContainsKey(42), Is.True);

			cola.Add(1, "1");
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(2));
			Assert.That(cola.ContainsKey(1), Is.True);

			cola.Add(66, "66");
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(3));
			Assert.That(cola.ContainsKey(66), Is.True);

			cola.Add(123, "123");
			DumpStore(cola);
			Assert.That(cola.Count, Is.EqualTo(4));
			Assert.That(cola.ContainsKey(123), Is.True);

			for(int i = 1; i < 100; i++)
			{
				cola.Add(-i, (-i).ToString());
			}
			DumpStore(cola);


			cmp.Reset();
			cola.ContainsKey(-99);
			Log($"Lookup last inserted: {cmp.Count}");

			cmp.Reset();
			cola.ContainsKey(42);
			Log($"Lookup first inserted: {cmp.Count}");

			cmp.Reset();
			cola.ContainsKey(77);
			Log($"Lookup not found: {cmp.Count}");

			var keys = new List<int>();

			foreach(var kvp in cola)
			{
				Assert.That(kvp.Value, Is.EqualTo(kvp.Key.ToString()));
				keys.Add(kvp.Key);
			}

			Assert.That(keys.Count, Is.EqualTo(cola.Count));
			Assert.That(keys, Is.Ordered);
			Log(string.Join(", ", keys));

		}

		[Test]
		public void Test_ColaOrderedDictionary_Remove()
		{
			const int N = 100;

			// add a bunch of random values
			int seed = 1333019583;
			Log($"Seed {seed}");
			var rnd = new Random(seed);

			var cola = new ColaOrderedDictionary<int, string>();
			var list = new List<int>();

			int x = 0;
			for (int i = 0; i < N; i++)
			{
				x += (1 + rnd.Next(10));
				string s = $"value of {x}";

				cola.Add(x, s);
				list.Add(x);
			}
			Assert.That(cola.Count, Is.EqualTo(N));

			foreach(var item in list)
			{
				Assert.That(cola.ContainsKey(item), $"{item} is missing");
			}

			DumpStore(cola);

			// now start removing items one by one
			while (list.Count > 0)
			{
				int p = rnd.Next(list.Count);
				x = list[p];
				list.RemoveAt(p);

				bool res = cola.Remove(x);
				if (!res)
				{
					DumpStore(cola);
				}

				Assert.That(res, Is.True, $"Remove({x}) failed");

				Assert.That(cola.Count, Is.EqualTo(list.Count), $"After removing {x}");
			}

			DumpStore(cola);
		}

		[Test]
		[Category("Benchmark")]
		[Category("LongRunning")]
		public void Test_MiniBench()
		{
#if DEBUG
			const int N = 1_000_000;
#else
			const int N = 10_000_000;
#endif

			var rnd = new Random();

			//WARMUP
			Warmup();

			static void Warmup()
			{
				using var store = new ColaOrderedDictionary<long, long>();
				store.Add(1, 1);
				store.Add(42, 42);
				store.Add(1234, 1234);
				store.TryGetValue(42, out _);
				store.TryGetValue(404, out _);
			}

			#region Sequentially inserted....

			Log($"Inserting {N:N0} sequential key/value pairs into a COLA ordered dictionary");
			GC.Collect();

			var store = new ColaOrderedDictionary<long, long>();

			long total = 0;
			var sw = Stopwatch.StartNew();
			for (int i = 0; i < N; i++)
			{
				store.SetItem(i, i);
				Interlocked.Increment(ref total);
				if (i % (N / 10) == 0) LogPartial(".");
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
				var _ = store.TryGetValue(i, out var x);
				if (!_ || x != i) Assert.Fail();
			}
			sw.Stop();
			Log($"SeqReadOrdered: {total:N0} keys in {sw.Elapsed.TotalSeconds:N3} sec => {(total / sw.Elapsed.TotalSeconds):N0} kps");

			// random reads

			sw.Restart();
			for (int i = 0; i < total; i++)
			{
				var _ = store.TryGetValue(rnd.Next(N), out var x);
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
			store.Dispose();
			GC.Collect();

			store = new ColaOrderedDictionary<long, long>();
			total = 0;
			sw.Restart();
			for (int i = 0; i < N; i++)
			{
				store.Add(values[i], i);
				Interlocked.Increment(ref total);
				if (i % (N / 10) == 0) LogPartial(".");
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
				var _ = store.TryGetValue(i, out var x);
				if (!_) Assert.Fail();
			}
			sw.Stop();
			Log($"SeqReadUnordered: {total:N0} keys in {sw.Elapsed.TotalSeconds:N3} sec => {(total / sw.Elapsed.TotalSeconds):N0} kps");

			// random reads

			sw.Restart();
			for (int i = 0; i < total; i++)
			{
				var _ = store.TryGetValue(rnd.Next(N), out var x);
				if (!_) Assert.Fail();
			}
			sw.Stop();
			Log($"RndReadUnordered: {total:N0} keys in {sw.Elapsed.TotalSeconds:N3} sec => {(total / sw.Elapsed.TotalSeconds):N0} kps");

			#endregion

		}

	}

}

#endif
