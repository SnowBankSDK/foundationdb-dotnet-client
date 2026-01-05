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

//#define ENABLE_LOGGING
//#define FULL_DEBUG

namespace FoundationDB.Layers.Collections.Tests
{
	using SnowBank.Linq.Async;

	[TestFixture]
	public class QueuesFacts : FdbTest
	{

		[Test]
		public async Task Test_Queue_Of_Slices()
		{
			using var db = await OpenTestPartitionAsync();
			var location = db.Root;
			await CleanLocation(db, location);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			var queue = new FdbQueue(location);

			bool isEmpty = await queue.ReadAsync(db, (tr, state) => state.EmptyAsync(tr), this.Cancellation);
			Log($"Empty? {isEmpty}");
			Assert.That(isEmpty, Is.True);

			var a = Slice.Repeat('A', 16);
			var b = Slice.Repeat('B', 16);
			var c = Slice.Repeat('C', 16);
			var d = Slice.Repeat('D', 16);

			Log("Push A, B, C in separate transactions");
			await queue.WriteAsync(db, (tr, state) => state.Push(tr, a), this.Cancellation);
			await queue.WriteAsync(db, (tr, state) => state.Push(tr, b), this.Cancellation);
			await queue.WriteAsync(db, (tr, state) => state.Push(tr, c), this.Cancellation);

#if FULL_DEBUG
			await DumpSubspace(db, location);
#endif

			// Empty?
			bool empty = await queue.ReadAsync(db, (tr, state) => state.EmptyAsync(tr), this.Cancellation);
			Log($"Empty? {empty}");
			Assert.That(empty, Is.False);

			var item = await queue.ReadWriteAsync(db, (tr, state) => state.PopAsync(tr), this.Cancellation);
			Log($"Pop item: {item}");
			Assert.That(item.HasValue, Is.True);
			Assert.That(item.Value, Is.EqualTo(a));
			item = await queue.ReadWriteAsync(db, (tr, state) => state.PeekAsync(tr), this.Cancellation);
			Log($"Next item: {item}");
			Assert.That(item.HasValue, Is.True);
			Assert.That(item.Value, Is.EqualTo(b));
#if FULL_DEBUG
			await DumpSubspace(db, location);
#endif

			item = await queue.ReadWriteAsync(db, (tr, state) => state.PopAsync(tr), this.Cancellation);
			Log($"Pop item: {item}");
			Assert.That(item.HasValue, Is.True);
			Assert.That(item.Value, Is.EqualTo(b));
#if FULL_DEBUG
			await DumpSubspace(db, location);
#endif

			item = await queue.ReadWriteAsync(db, (tr, state) => state.PopAsync(tr), this.Cancellation);
			Log($"Pop item: {item}");
			Assert.That(item.HasValue, Is.True);
			Assert.That(item.Value, Is.EqualTo(c));
#if FULL_DEBUG
			await DumpSubspace(db, location);
#endif

			empty = await queue.ReadAsync(db, (tr, state) => state.EmptyAsync(tr), this.Cancellation);
			Log($"Empty? {empty}");
			Assert.That(empty, Is.True);

			Log("Push D");
			await queue.WriteAsync(db, (tr, state) => state.Push(tr, d), this.Cancellation);
#if FULL_DEBUG
			await DumpSubspace(db, location);
#endif

			Log("Clear Queue");
			await queue.WriteAsync(db, (tr, state) => state.Clear(tr), this.Cancellation);
#if FULL_DEBUG
			await DumpSubspace(db, location);
#endif

			empty = await queue.ReadAsync(db, (tr, state) => state.EmptyAsync(tr), this.Cancellation);
			Log($"Empty? {empty}");
			Assert.That(empty, Is.True);
		}

		[Test]
		public async Task Test_Queue_Of_Ints()
		{
			using var db = await OpenTestPartitionAsync();
			var location = db.Root;
			await CleanLocation(db, location);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			var queue = new FdbQueue<int>(location);

			bool isEmpty = await queue.ReadAsync(db, (tr, state) => state.EmptyAsync(tr), this.Cancellation);
			Log($"Empty? {isEmpty}");
			Assert.That(isEmpty, Is.True);

			Log("Push 10, 8, 6 in separate transactions");
			await queue.WriteAsync(db, (tr, state) => state.Push(tr, 10), this.Cancellation);
			await queue.WriteAsync(db, (tr, state) => state.Push(tr, 8), this.Cancellation);
			await queue.WriteAsync(db, (tr, state) => state.Push(tr, 6), this.Cancellation);

#if FULL_DEBUG
			await DumpSubspace(db, location);
#endif

			// Empty?
			bool empty = await queue.ReadAsync(db, (tr, state) => state.EmptyAsync(tr), this.Cancellation);
			Log($"Empty? {empty}");
			Assert.That(empty, Is.False);

			var item = await queue.ReadWriteAsync(db, (tr, state) => state.PopAsync(tr), this.Cancellation);
			Log($"Pop item: {item}");
			Assert.That(item.HasValue, Is.True);
			Assert.That(item.Value, Is.EqualTo(10));
			item = await queue.ReadWriteAsync(db, (tr, state) => state.PeekAsync(tr), this.Cancellation);
			Log($"Next item: {item}");
			Assert.That(item.HasValue, Is.True);
			Assert.That(item.Value, Is.EqualTo(8));
#if FULL_DEBUG
			await DumpSubspace(db, location);
#endif

			item = await queue.ReadWriteAsync(db, (tr, state) => state.PopAsync(tr), this.Cancellation);
			Log($"Pop item: {item}");
			Assert.That(item.HasValue, Is.True);
			Assert.That(item.Value, Is.EqualTo(8));
#if FULL_DEBUG
			await DumpSubspace(db, location);
#endif

			item = await queue.ReadWriteAsync(db, (tr, state) => state.PopAsync(tr), this.Cancellation);
			Log($"Pop item: {item}");
			Assert.That(item.HasValue, Is.True);
			Assert.That(item.Value, Is.EqualTo(6));
#if FULL_DEBUG
			await DumpSubspace(db, location);
#endif

			empty = await queue.ReadAsync(db, (tr, state) => state.EmptyAsync(tr), this.Cancellation);
			Log($"Empty? {empty}");
			Assert.That(empty, Is.True);

			Log("Push 5");
			await queue.WriteAsync(db, (tr, state) => state.Push(tr, 5), this.Cancellation);
#if FULL_DEBUG
			await DumpSubspace(db, location);
#endif

			Log("Clear Queue");
			await queue.WriteAsync(db, (tr, state) => state.Clear(tr), this.Cancellation);
#if FULL_DEBUG
			await DumpSubspace(db, location);
#endif

			empty = await queue.ReadAsync(db, (tr, state) => state.EmptyAsync(tr), this.Cancellation);
			Log($"Empty? {empty}");
			Assert.That(empty, Is.True);
		}

		[Test]
		public async Task Test_Queue_Of_Strings()
		{
			using var db = await OpenTestPartitionAsync();
			var location = db.Root;
			await CleanLocation(db, location);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			var queue = new FdbQueue<string, FdbUtf8Value>(location, FdbValueCodec.Utf8);

			bool isEmpty = await queue.ReadAsync(db, (tr, state) => state.EmptyAsync(tr), this.Cancellation);
			Log($"Empty? {isEmpty}");
			Assert.That(isEmpty, Is.True);

			Log("Push 'foo', 'bar', 'baz' in separate transactions");
			await queue.WriteAsync(db, (tr, state) => state.Push(tr, "foo"), this.Cancellation);
			await queue.WriteAsync(db, (tr, state) => state.Push(tr, "bar"), this.Cancellation);
			await queue.WriteAsync(db, (tr, state) => state.Push(tr, "baz"), this.Cancellation);

#if FULL_DEBUG
			await DumpSubspace(db, location);
#endif

			// Empty?
			bool empty = await queue.ReadAsync(db, (tr, state) => state.EmptyAsync(tr), this.Cancellation);
			Log($"Empty? {empty}");
			Assert.That(empty, Is.False);

			var item = await queue.ReadWriteAsync(db, (tr, state) => state.PopAsync(tr), this.Cancellation);
			Log($"Pop item: {item}");
			Assert.That(item.HasValue, Is.True);
			Assert.That(item.Value, Is.EqualTo("foo"));
			item = await queue.ReadWriteAsync(db, (tr, state) => state.PeekAsync(tr), this.Cancellation);
			Log($"Next item: {item}");
			Assert.That(item.HasValue, Is.True);
			Assert.That(item.Value, Is.EqualTo("bar"));
#if FULL_DEBUG
			await DumpSubspace(db, location);
#endif

			item = await queue.ReadWriteAsync(db, (tr, state) => state.PopAsync(tr), this.Cancellation);
			Log($"Pop item: {item}");
			Assert.That(item.HasValue, Is.True);
			Assert.That(item.Value, Is.EqualTo("bar"));
#if FULL_DEBUG
			await DumpSubspace(db, location);
#endif

			item = await queue.ReadWriteAsync(db, (tr, state) => state.PopAsync(tr), this.Cancellation);
			Log($"Pop item: {item}");
			Assert.That(item.HasValue, Is.True);
			Assert.That(item.Value, Is.EqualTo("baz"));
#if FULL_DEBUG
			await DumpSubspace(db, location);
#endif

			empty = await queue.ReadAsync(db, (tr, state) => state.EmptyAsync(tr), this.Cancellation);
			Log($"Empty? {empty}");
			Assert.That(empty, Is.True);

			Log("Push 5");
			await queue.WriteAsync(db, (tr, state) => state.Push(tr, "jazz"), this.Cancellation);
#if FULL_DEBUG
			await DumpSubspace(db, location);
#endif

			Log("Clear Queue");
			await queue.WriteAsync(db, (tr, state) => state.Clear(tr), this.Cancellation);
#if FULL_DEBUG
			await DumpSubspace(db, location);
#endif

			empty = await queue.ReadAsync(db, (tr, state) => state.EmptyAsync(tr), this.Cancellation);
			Log($"Empty? {empty}");
			Assert.That(empty, Is.True);
		}

		[Test]
		public async Task Test_Queue_Batch()
		{
			using var db = await OpenTestPartitionAsync();
			var location = db.Root;
			await CleanLocation(db, location);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			var queue = new FdbQueue<int>(location);

			Log("Pushing 10 items in a batch...");
			await queue.WriteAsync(db, (tr, state) =>
			{
				for (int i = 0; i < 10; i++)
				{
					state.Push(tr, i);
				}
			}, this.Cancellation);
#if FULL_DEBUG
			await DumpSubspace(db, queue.Location);
#endif

			Log("Popping 7 items in same transaction...");
			await queue.WriteAsync(db, async (tr, state) =>
			{
				for (int i = 0; i < 7; i++)
				{
					var r = await state.PopAsync(tr);
					Log($"- ({r.Value}, {r.HasValue})");
					Assert.That(r.HasValue, Is.True);
					Assert.That(r.Value, Is.EqualTo(i));
				}
			}, this.Cancellation);
#if FULL_DEBUG
			await DumpSubspace(db, queue.Location);
#endif

			bool empty = await queue.ReadAsync(db, (tr, state) => state.EmptyAsync(tr), this.Cancellation);
			Assert.That(empty, Is.False);

			Log("Popping 3 + 1 items in another transaction...");
			await queue.WriteAsync(db, async (tr, state) =>
			{
				// should be able to pop 3 items..

				var r = await state.PopAsync(tr);
				Log($"- ({r.Value}, {r.HasValue})");
				Assert.That(r.HasValue, Is.True);
				Assert.That(r.Value, Is.EqualTo(7));

				r = await state.PopAsync(tr);
				Log($"- ({r.Value}, {r.HasValue})");
				Assert.That(r.HasValue, Is.True);
				Assert.That(r.Value, Is.EqualTo(8));

				r = await state.PopAsync(tr);
				Log($"- ({r.Value}, {r.HasValue})");
				Assert.That(r.HasValue, Is.True);
				Assert.That(r.Value, Is.EqualTo(9));

				// queue should now be empty!
				r = await state.PopAsync(tr);
				Log($"- ({r.Value}, {r.HasValue})");
				Assert.That(r.HasValue, Is.False);
				Assert.That(r.Value, Is.EqualTo(0));

			}, this.Cancellation);

			empty = await queue.ReadAsync(db, (tr, state) => state.EmptyAsync(tr), this.Cancellation);
			Assert.That(empty, Is.True);
		}

		private async Task RunMultiClientTest(IFdbDatabase db, FdbDirectorySubspaceLocation location, string desc, int K, int NUM, CancellationToken ct)
		{
			Log($"Starting {desc} test with {K:N0} threads and {NUM:N0} iterations");

			await CleanLocation(db, location);

			var queue = new FdbQueue<string>(location);

			// use a CTS to ensure that everything will stop in case of problems...
			using var go = new CancellationTokenSource(TimeSpan.FromSeconds(30));
			var tok = go.Token;

			var pushLock = new AsyncCancelableMutex(tok);
			var popLock = new AsyncCancelableMutex(tok);

			int pushCount = 0;
			int popCount = 0;
			int stalls = 0;

			var start = DateTime.UtcNow;

			var pushTreads = Enumerable.Range(0, K)
				.Select(async id =>
				{
					int i = 0;
					try
					{
						// wait for the signal
						await pushLock.Task.ConfigureAwait(false);

						var res = new List<string>(NUM);

						for (; i < NUM; i++)
						{
							var item = $"{id}.{i}";
							await queue.WriteAsync(db, (tr, state) => state.Push(tr, item), tok).ConfigureAwait(false);
							Interlocked.Increment(ref pushCount);
							res.Add(item);
						}

						Log($"PushThread[{id}] pushed {NUM:N0} items in {(DateTime.UtcNow - start).TotalSeconds:N1} sec");

						return res;
					}
					catch (Exception e)
					{
						Log($"PushThread[{id}] failed after {i} push and {(DateTime.UtcNow - start).TotalSeconds:N1} sec: {e}");
						Assert.Fail($"PushThread[{id}] failed: {e.Message}");
						throw;
					}
						
				}).ToArray();

			var popThreads = Enumerable.Range(0, K)
				.Select(async id =>
				{
					int i = 0;
					try
					{
						// make everyone wait a bit, to ensure that they all start roughly at the same time
						await popLock.Task.ConfigureAwait(false);

						var res = new List<string?>(NUM);

						while (i < NUM)
						{
							var item = await queue.ReadWriteAsync(db, (tr, state) => state.PopAsync(tr), tok).ConfigureAwait(false);
							if (item.HasValue)
							{
								Interlocked.Increment(ref popCount);
								res.Add(item.Value);
								++i;
							}
							else
							{
								Interlocked.Increment(ref stalls);
								await Task.Delay(10, this.Cancellation).ConfigureAwait(false);
							}
						}
						Log($"PopThread[{id}] popped {NUM:N0} items in {(DateTime.UtcNow - start).TotalSeconds:N1} sec");

						return res;
					}
					catch (Exception e)
					{
						Log($"PopThread[{id}] failed: {e}");
						Assert.Fail($"PopThread[{id}] failed after {i} pops and {(DateTime.UtcNow - start).TotalSeconds:N1} sec: {e.Message}");
						throw;
					}
				}).ToArray();

			var sw = Stopwatch.StartNew();
			pushLock.Set(async: true);

			await Task.Delay(50, this.Cancellation);
			popLock.Set(async: true);

			await Task.WhenAll(pushTreads);
			Log("Push threads are finished!");
			await Task.WhenAll(popThreads);

			sw.Stop();
			Log($"> Finished {desc} test in {sw.Elapsed.TotalSeconds:N3} seconds");
			Log($"> Pushed {pushCount:N0}, Popped {popCount:N0} and Stalled {stalls:N0}");

			var pushedItems = pushTreads.SelectMany(t => t.Result).ToList();
			var poppedItems = popThreads.SelectMany(t => t.Result).ToList();

			Assert.That(pushCount, Is.EqualTo(K * NUM));
			Assert.That(popCount, Is.EqualTo(K * NUM));

			// all pushed items should have been popped (with no duplicates)
			Assert.That(poppedItems, Is.EquivalentTo(pushedItems));

			// the queue should be empty
			bool empty = await queue.ReadAsync(db, (tr, state) => state.EmptyAsync(tr), ct);
			Assert.That(empty, Is.True);
		}

		[Test]
		[Category("Benchmark")]
		[Category("LongRunning")]
		public async Task Bench_Concurrent_Clients()
		{
			const int NUM = 100;

			using var db = await OpenTestPartitionAsync();
			var location = db.Root;
			await CleanLocation(db, location);

			await RunMultiClientTest(db, location, "simple queue", 1, NUM, this.Cancellation);
			await RunMultiClientTest(db, location, "simple queue", 2, NUM, this.Cancellation);
			await RunMultiClientTest(db, location, "simple queue", 4, NUM, this.Cancellation);
			await RunMultiClientTest(db, location, "simple queue", 10, NUM, this.Cancellation);
		}

		[Test, Category("Benchmark")]
		public async Task Test_Log_Queue()
		{
			const int NUM = 100;

			using var db = await OpenTestPartitionAsync();
			var location = db.Root;
			await CleanLocation(db, location);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			await RunMultiClientTest(db, location, "simple queue", 4, NUM, this.Cancellation);
		}

	}

}
