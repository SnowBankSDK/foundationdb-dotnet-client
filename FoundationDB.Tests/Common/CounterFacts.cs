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

namespace FoundationDB.Layers.Counters.Tests
{

	[TestFixture]
	[Obsolete]
	public class CounterFacts : FdbTest
	{

		[Test]
		public async Task Test_FdbCounter_Can_Increment_And_SetTotal()
		{
			using var db = await this.OpenTestPartitionAsync();
			var location = db.Root;
			await this.CleanLocation(db, location);

			var counter = new FdbHighContentionCounter(location);

			await db.WriteAsync(async tr => await counter.Add(tr, 100), this.Cancellation);
			var res = await db.ReadAsync(tr => counter.GetSnapshot(tr), this.Cancellation);
			Assert.That(res, Is.EqualTo(100));

			await db.WriteAsync(async tr => await counter.Add(tr, -10), this.Cancellation);
			res = await db.ReadAsync(tr => counter.GetSnapshot(tr), this.Cancellation);
			Assert.That(res, Is.EqualTo(90));

			await db.WriteAsync(async tr => await counter.SetTotal(tr, 500), this.Cancellation);
			res = await db.ReadAsync(tr => counter.GetSnapshot(tr), this.Cancellation);
			Assert.That(res, Is.EqualTo(500));
		}

		[Test]
		public async Task Bench_FdbCounter_Increment_Sequentially()
		{
			const int N = 100;

			using var db = await this.OpenTestPartitionAsync();
			var location = db.Root;
			await this.CleanLocation(db, location);

			var c = new FdbHighContentionCounter(location);

			Log($"Doing {N:N0} inserts in one thread...");

			var sw = Stopwatch.StartNew();
			for (int i = 0; i < N; i++)
			{
				await db.WriteAsync(async tr => await c.Add(tr, 1), this.Cancellation);
			}
			sw.Stop();

			Log($"> {N:N0} inserts completed in {sw.Elapsed.TotalMilliseconds:N1} ms ({(sw.Elapsed.TotalMilliseconds * 1000 / N):N0} µs/add)");

#if DEBUG
			await this.DumpSubspace(db, location);
#endif

			var res = await db.ReadAsync(tr => c.GetSnapshot(tr), this.Cancellation);
			Assert.That(res, Is.EqualTo(N));
		}

		[Test]
		public async Task Bench_FdbCounter_Increment_Concurrently()
		{
			const int B = 100;

			// repeat the process 10 times...
			foreach(int W in new [] { 1, 2, 5, 10, 20, 50, 100 })
			{
				int N = B * W;

				using (var db = await OpenTestPartitionAsync())
				{
					var location = db.Root[$"run_{W}"];
					await CleanLocation(db, location);

					var c = new FdbHighContentionCounter(location);

					Log($"Doing {W:N0} x {B:N0} inserts in {W:N0} threads...");

					var signal = new TaskCompletionSource<object?>();
					var workers = Enumerable.Range(0, W)
						.Select(async (_) =>
						{
							await signal.Task.ConfigureAwait(false);
							for (int i = 0; i < B; i++)
							{
								await db.WriteAsync(async tr => await c.Add(tr, 1), this.Cancellation);
							}
						}).ToArray();

					var sw = Stopwatch.StartNew();
					// start
					ThreadPool.UnsafeQueueUserWorkItem((_) => signal.TrySetResult(null), null);
					// wait
					await Task.WhenAll(workers);
					sw.Stop();
					Log($"> {N} completed in {sw.Elapsed.TotalMilliseconds:N1} ms ({(sw.Elapsed.TotalMilliseconds * 1000 / B):N0} µs/add)");

					long n = await db.ReadAsync(tr => c.GetSnapshot(tr), this.Cancellation);
					if (n != N)
					{ // fail
						await DumpSubspace(db, location);
						Assert.That(n, Is.EqualTo(N), "Counter value does not match (first call)");
					}

					// wait a bit, in case there was some coalesce still running...
					await Task.Delay(200);

					n = await db.ReadAsync(tr => c.GetSnapshot(tr), this.Cancellation);
					if (n != N)
					{ // fail
						await DumpSubspace(db, location);
						Assert.That(n, Is.EqualTo(N), "Counter value does not match (second call)");
					}

				}
			}

		}

	}

}
