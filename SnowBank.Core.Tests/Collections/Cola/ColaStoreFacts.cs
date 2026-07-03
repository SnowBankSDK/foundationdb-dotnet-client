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
	using SnowBank.Numerics;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class ColaStoreFacts : SimpleTest
	{
		[Test]
		public void Test_Bit_Twiddling()
		{
			Assert.That(ColaStore.LowestBit(0), Is.EqualTo(0));
			Assert.That(ColaStore.HighestBit(0), Is.EqualTo(0));

			Assert.That(ColaStore.LowestBit(42), Is.EqualTo(1));
			Assert.That(ColaStore.HighestBit(42), Is.EqualTo(5));

			for (int i = 1; i < 30; i++)
			{
				int x = 1 << i;
				Assert.That(ColaStore.LowestBit(x), Is.EqualTo(i));
				Assert.That(ColaStore.HighestBit(x), Is.EqualTo(i));

				Assert.That(ColaStore.HighestBit(x - 1), Is.EqualTo(i - 1));
				Assert.That(ColaStore.LowestBit(x - 1), Is.EqualTo(0));
			}

			Assert.That(ColaStore.LowestBit(0x60000000), Is.EqualTo(29));
			for (int i = 1; i < 30; i++)
			{
				int x = int.MaxValue - ((1 << i) - 1);
				Assert.That(ColaStore.LowestBit(x), Is.EqualTo(i), $"i={i}, x={x.ToString("X8")} : {Convert.ToString(x, 2)}");
			}
		}

		[Test]
		public void Test_Map_Index_To_Address()
		{
			// index => (level, offset)

			for (int i = 0; i < 1024; i++)
			{
				var (level, offset) = ColaStore.FromIndex(i);
				Assert.That(((1 << level) - 1) + offset, Is.EqualTo(i), $"{i} => ({level}, {offset})");
			}
		}

		[Test]
		public void Test_Map_Address_Index()
		{
			// index => (level, offset)

			for (int level = 0; level <= 10; level++)
			{
				int n = 1 << level;
				for (int offset = 0; offset < n; offset++)
				{
					int index = ColaStore.ToIndex(level, offset);
					Assert.That(index, Is.EqualTo(n - 1 + offset), $"({level}, {offset}) => {index}");
				}
			}
		}

		[Test]
		public void Test_Map_Offset_To_Index()
		{
			//N = 1
			// > 0 [0]
			Assert.That(ColaStore.MapOffsetToIndex(1, 0), Is.EqualTo(0));
			Assert.That(() => ColaStore.MapOffsetToIndex(1, 1), Throws.InstanceOf<ArgumentOutOfRangeException>());

			//N = 2
			// > 0 [_]
			// > 1 [0, 1]
			Assert.That(ColaStore.MapOffsetToIndex(2, 0), Is.EqualTo(1));
			Assert.That(ColaStore.MapOffsetToIndex(2, 1), Is.EqualTo(2));
			Assert.That(() => ColaStore.MapOffsetToIndex(2, 2), Throws.InstanceOf<ArgumentOutOfRangeException>());

			//N = 3
			// > 0 [2]
			// > 1 [0, 1]
			Assert.That(ColaStore.MapOffsetToIndex(3, 0), Is.EqualTo(1));
			Assert.That(ColaStore.MapOffsetToIndex(3, 1), Is.EqualTo(2));
			Assert.That(ColaStore.MapOffsetToIndex(3, 2), Is.EqualTo(0));
			Assert.That(() => ColaStore.MapOffsetToIndex(3, 3), Throws.InstanceOf<ArgumentOutOfRangeException>());

			//N = 5
			// > 0 [4]
			// > 1 [_, _]
			// > 2 [0, 1, 2, 3]
			Assert.That(ColaStore.MapOffsetToIndex(5, 0), Is.EqualTo(3));
			Assert.That(ColaStore.MapOffsetToIndex(5, 1), Is.EqualTo(4));
			Assert.That(ColaStore.MapOffsetToIndex(5, 2), Is.EqualTo(5));
			Assert.That(ColaStore.MapOffsetToIndex(5, 3), Is.EqualTo(6));
			Assert.That(ColaStore.MapOffsetToIndex(5, 4), Is.EqualTo(0));
			Assert.That(() => ColaStore.MapOffsetToIndex(5, 5), Throws.InstanceOf<ArgumentOutOfRangeException>());

			// N = 10
			// > 0 [_]
			// > 1 [8,9]
			// > 2 [_,_,_,_]
			// > 3 [0,1,2,3,4,5,6,7]
			for (int i = 0; i < 8; i++) Assert.That(ColaStore.MapOffsetToIndex(10, i), Is.EqualTo(7 + i), $"MapOffset(10, {i})");
			for (int i = 8; i < 10; i++) Assert.That(ColaStore.MapOffsetToIndex(10, i), Is.EqualTo(1 + (i - 8)), $"MapOffset(10, {i})");
			Assert.That(() => ColaStore.MapOffsetToIndex(10, 123), Throws.InstanceOf<ArgumentOutOfRangeException>());
		}

		private void DumpStore<T>(ColaStore<T> store)
		{
			var sw = new StringWriter();
			store.Debug_Dump(sw);
			Log(sw);
		}

		[Test]
		public void Test_ColaStore_Iterator_Seek()
		{

			var store = new ColaStore<int?>(0, Comparer<int?>.Default);

			for (int i = 0; i < 10; i++)
			{
				store.Insert(i);
			}
			DumpStore(store);

			var iterator = store.GetIterator();

			Assert.That(iterator.Seek(5, true), Is.True);
			Assert.That(iterator.Current, Is.EqualTo(5));

			Assert.That(iterator.Seek(5, false), Is.True);
			Assert.That(iterator.Current, Is.EqualTo(4));

			Assert.That(iterator.Seek(9, true), Is.True);
			Assert.That(iterator.Current, Is.EqualTo(9));

			Assert.That(iterator.Seek(9, false), Is.True);
			Assert.That(iterator.Current, Is.EqualTo(8));

			Assert.That(iterator.Seek(0, true), Is.True);
			Assert.That(iterator.Current, Is.EqualTo(0));

			Assert.That(iterator.Seek(0, false), Is.False);
			Assert.That(iterator.Current, Is.Null);

			Assert.That(iterator.Seek(10, true), Is.True);
			Assert.That(iterator.Current, Is.EqualTo(9));

			Assert.That(iterator.Seek(10, false), Is.True);
			Assert.That(iterator.Current, Is.EqualTo(9));

		}

		[Test]
		public void Test_ColaStore_Iterator_Seek_Randomized()
		{
			const int N = 1000;

			var store = new ColaStore<int?>(0, Comparer<int?>.Default);

			var rnd = new Random();
			int seed = rnd.Next();
			Log($"seed = {seed}");
			rnd = new Random(seed);

			var list = Enumerable.Range(0, N).ToList();
			while(list.Count > 0)
			{
				int p = rnd.Next(list.Count);
				store.Insert(list[p]);
				list.RemoveAt(p);
			}
			DumpStore(store);

			for (int i = 0; i < N; i++)
			{
				var iterator = store.GetIterator();

				int p = rnd.Next(N);
				bool orEqual = rnd.Next(2) == 0;

				bool res = iterator.Seek(p, orEqual);

				if (orEqual)
				{ // the key should exist
					Assert.That(res, Is.True, string.Format("Seek({0}, '<=')", p));
					Assert.That(iterator.Current, Is.EqualTo(p), string.Format("Seek({0}, '<=')", p));
					Assert.That(iterator.Valid, Is.True, string.Format("Seek({0}, '<=')", p));
				}
				else if (p == 0)
				{ // there is no key before the first
					Assert.That(res, Is.False, "Seek(0, '<')");
					Assert.That(iterator.Current, Is.Null, "Seek(0, '<')");
					Assert.That(iterator.Valid, Is.False, "Seek(0, '<')");
				}
				else
				{ // the key should exist
					Assert.That(res, Is.True, string.Format("Seek({0}, '<')", p));
					Assert.That(iterator.Current, Is.EqualTo(p - 1), string.Format("Seek({0}, '<')", p));
					Assert.That(iterator.Valid, Is.True, string.Format("Seek({0}, '<')", p));
				}
			}

		}

		[Test]
		public void Test_ColaStore_Iterator_Seek_Then_Next_Randomized()
		{
			const int N = 1000;
			const int K = 10;

			var store = new ColaStore<int?>(0, Comparer<int?>.Default);

			var rnd = new Random();
			int seed = rnd.Next();
			Log($"seed = {seed}");
			rnd = new Random(seed);

			var list = Enumerable.Range(0, N).ToList();
			while (list.Count > 0)
			{
				int p = rnd.Next(list.Count);
				store.Insert(list[p]);
				list.RemoveAt(p);
			}
			DumpStore(store);

			for (int i = 0; i < N; i++)
			{
				var iterator = store.GetIterator();

				int p = rnd.Next(N);
				bool orEqual = rnd.Next(2) == 0;

				if (p == 0 && !orEqual) continue; //TODO: what to do for this case ?

				Assert.That(iterator.Seek(p, orEqual), Is.True);
				int? x = iterator.Current;
				Assert.That(x, Is.EqualTo(orEqual ? p : p - 1));

				// all the next should be ordered (starting from p)
				while (x < N - 1)
				{
					Assert.That(iterator.Next(), Is.True, string.Format("Seek({0}).Current({1}).Next()", p, x));
					Assert.That(iterator.Current, Is.EqualTo(x + 1), string.Format("Seek({0}).Current({1}).Next()", p, x));
					++x;
				}
				// the following Next() should go past the end
				Assert.That(iterator.Next(), Is.False);
				Assert.That(iterator.Current, Is.Null);
				Assert.That(iterator.Valid, Is.False);

				// re-seek to the original location
				Assert.That(iterator.Seek(p, orEqual), Is.True);
				x = iterator.Current;
				Assert.That(x, Is.EqualTo(orEqual ? p : p - 1));

				// now go backwards
				while (x > 0)
				{
					Assert.That(iterator.Previous(), Is.True, string.Format("Seek({0}).Current({1}).Previous()", p, x));
					Assert.That(iterator.Current, Is.EqualTo(x - 1), string.Format("Seek({0}).Current({1}).Previous()", p, x));
					--x;
				}
				// the following Previous() should go past the beginning
				Assert.That(iterator.Previous(), Is.False);
				Assert.That(iterator.Current, Is.Null);
				Assert.That(iterator.Valid, Is.False);

				if (p >= K && p < N - K)
				{ // jitter dance

					// start to original location
					Assert.That(iterator.Seek(p, true), Is.True);
					Assert.That(iterator.Current, Is.EqualTo(p));

					var sb = new StringBuilder();
					sb.Append("Seek -> ");
					for(int j = 0; j < K; j++)
					{
						x = iterator.Current;
						sb.Append(iterator.Current);
						if (rnd.Next(2) == 0)
						{ // next
							sb.Append(" -> ");
							Assert.That(iterator.Next(), Is.True, string.Format("{0}", sb));
							Assert.That(iterator.Current, Is.EqualTo(x + 1), string.Format("{0} = ?", sb));
						}
						else
						{ // prev
							sb.Append(" <- ");
							Assert.That(iterator.Previous(), Is.True, string.Format("{0}", sb));
							Assert.That(iterator.Current, Is.EqualTo(x - 1), string.Format("{0} = ?", sb));
						}
					}
				}

			}

		}

		[Test]
		[Category("Benchmark")]
		[Category("LongRunning")]
		public void Test_MiniBench()
		{
			const int N = (1 << 23) - 1; // 10 * 1000 * 1000;

			var rnd = new Random();

			//WARMUP
			{
				using var warmup = new ColaStore<long>(0, Comparer<long>.Default);
				var rsw = RobustStopwatch.StartNew();
				for (int i = 0; i < 100; i++)
				{
					rsw.Restart();
					warmup.Insert(i);
					_ = rsw.Elapsed;
					rsw.Stop();
					_ = warmup.Find(1, out var level, out var offset);
					_ = GetElapsedTime(GetTimestamp());
					rsw.Start();

				}
			}

			Log($"Generating {N:N0} random keys...");
			var randomKeys = GetRandomNumbers(this.Rnd, N);
			var orderedKeys = randomKeys.ToArray();
			orderedKeys.AsSpan().Sort();

			const int BS = (N + 1) / 128;
			var timings = new List<TimeSpan>(BS);
			timings.Add(TimeSpan.Zero);
			timings.Clear();

			#region Sequentially inserted....

			var h = new RobustHistogram(RobustHistogram.TimeScale.Microseconds);

			Log($"Inserting {orderedKeys.Length:N0} sequential keys into a COLA store");
			GC.Collect();

			var store = new ColaStore<long>(0, Comparer<long>.Default);
			var sw = RobustStopwatch.StartNew();

			for (int i = 0; i < orderedKeys.Length; i++)
			{
				long ts = RobustStopwatch.GetTimestamp();

				store.SetOrAdd(orderedKeys[i]);

				h.Add(RobustStopwatch.GetElapsedTime(ts));

				if ((i % BS) == BS - 1)
				{
					var d = sw.Stop();
					timings.Add(d);
					LogPartial(".");
					sw.Start();
				}
			}

			sw.Stop();

			Log("done");
			Log($"* Inserted: {orderedKeys.Length:N0} keys");
			Log($"* Elapsed : {sw.Elapsed.TotalSeconds:N3} sec");
			Log($"* KPS: {(orderedKeys.Length / sw.Elapsed.TotalSeconds):N0} key/sec");
			Log($"* Latency : {(sw.Elapsed.TotalMilliseconds * 1000000 / orderedKeys.Length):N1} nanos / insert");
			Log(h.GetReport(detailed: true));
#if OUTPUT_CSV
			var csv = new StringBuilder();
			Log("----");
			for (int i = 0; i < timings.Count; i++)
			{
				csv.AppendLineInvariant($"{((i + 1) * BS)}\t{timings[i].TotalSeconds}");
			}
			Log(csv.ToString());
			Log("----");
#endif

			// sequential reads

			h.Clear();
			var elapsed = TimeSpan.Zero;
			sw.Restart();
			foreach (var key in orderedKeys)
			{
				long ts = RobustStopwatch.GetTimestamp();
				_ = store.Find(key, out _, out _);
				var d = RobustStopwatch.GetElapsedTime(ts);
				h.Add(d);
				elapsed += d;
			}
			sw.Stop();
			Log($"SeqReadOrdered: {orderedKeys.Length:N0} keys in {elapsed.TotalSeconds:N3} ~ {sw.Elapsed.TotalSeconds:N3} sec => {(orderedKeys.Length / sw.Elapsed.TotalSeconds):N0} ~ {(orderedKeys.Length / elapsed.TotalSeconds):N0} kps");
			Log(h.GetReport(detailed: true));

			// random reads

			h.Clear();
			elapsed = TimeSpan.Zero;
			sw.Restart();
			foreach (var key in randomKeys)
			{
				long ts = RobustStopwatch.GetTimestamp();
				_ = store.Find(key, out _, out _);
				var d = RobustStopwatch.GetElapsedTime(ts);
				h.Add(d);
				elapsed += d;
			}
			sw.Stop();
			Log($"RndReadOrdered: {orderedKeys.Length:N0} keys in {elapsed.TotalSeconds:N3} ~ {sw.Elapsed.TotalSeconds:N3} sec => {(orderedKeys.Length / sw.Elapsed.TotalSeconds):N0} ~ {(orderedKeys.Length / elapsed.TotalSeconds):N0} kps");
			Log(h.GetReport(detailed: true));
			Log();

#endregion

			#region Randomly inserted....

			Log($"Inserting {randomKeys.Length:N0} randomized keys into a COLA store");
			store.Dispose();
			GC.Collect();

			store = new(0, Comparer<long>.Default);

			timings.Clear();
			h.Clear();

			sw.Restart();
			for (int i = 0; i < randomKeys.Length; i++)
			{
				long ts = RobustStopwatch.GetTimestamp();

				store.SetOrAdd(randomKeys[i]);

				h.Add(RobustStopwatch.GetElapsedTime(ts));

				if ((i % BS) == BS - 1)
				{
					timings.Add(sw.Stop());
					LogPartial(".");
					sw.Start();
				}
			}
			sw.Stop();

			Log("done");
			Log($"* Inserted: {randomKeys.Length:N0} keys");
			Log($"* Elapsed : {sw.Elapsed.TotalSeconds:N3} sec");
			Log($"* KPS     : {(randomKeys.Length / sw.Elapsed.TotalSeconds):N0} key/sec");
			Log($"* Latency : {(sw.Elapsed.TotalMilliseconds * 1000000 / randomKeys.Length):N1} nanos / insert");
			Log(h.GetReport(detailed: true));

#if OUTPUT_CSV
			Log("----");
			csv.Clear();
			for (int i = 0; i < timings.Count;i++)
			{
				csv.AppendLineInvariant($"{((i + 1) * BS)}\t{timings[i].TotalSeconds}");
			}
			Log(csv.ToString());
			Log("----");
#endif

			// sequential reads

			h.Clear();
			sw.Restart();
			foreach (var key in orderedKeys)
			{
				long ts = RobustStopwatch.GetTimestamp();
				_ = store.Find(key, out _, out _);
				h.Add(RobustStopwatch.GetElapsedTime(ts));
			}
			sw.Stop();
			Log($"SeqReadUnordered: {randomKeys.Length:N0} keys in {sw.Elapsed.TotalSeconds:N3} sec => {(randomKeys.Length / sw.Elapsed.TotalSeconds):N0} kps");
			Log(h.GetReport(detailed: true));

			// random reads

			h.Clear();
			sw.Restart();
			foreach (var key in randomKeys)
			{
				long ts = RobustStopwatch.GetTimestamp();
				_ = store.Find(key, out _, out _);
				h.Add(RobustStopwatch.GetElapsedTime(ts));
			}
			sw.Stop();
			Log($"RndReadUnordered: {randomKeys.Length:N0} keys in {sw.Elapsed.TotalSeconds:N3} sec => {(randomKeys.Length / sw.Elapsed.TotalSeconds):N0} kps");
			Log(h.GetReport(detailed: true));
			Log();

#endregion

		}

	}

}
