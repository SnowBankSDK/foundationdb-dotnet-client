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

#if !NETFRAMEWORK
// the IValueTaskSource-based future (and its reuse gate) only exists on the modern targets; the netstandard2.0
// build keeps the TaskCompletionSource shape

namespace FoundationDB.Client.Tests
{
	using FoundationDB.Client.Native;

	/// <summary>Tests of the <see cref="FdbFuture{TResult}"/> completion machinery that need no cluster: the
	/// two-phase reuse gate (result consumed + cleanup done), the reset cycle, and the stale-token guard.</summary>
	[TestFixture]
	[Category("Fdb-Client-InProcess")]
	[Parallelizable(ParallelScope.All)]
	public class FutureFacts : FdbSimpleTest
	{

		/// <summary>Minimal concrete future: no native handles, everything observable</summary>
		private sealed class TestFuture : FdbFuture<int>
		{
			public int ReuseSignals;

			public int CloseSignals;

			protected override void CloseHandles() => Interlocked.Increment(ref this.CloseSignals);

			protected override void CancelHandles() { }

			protected override void ReleaseMemory() { }

			protected override void OnReadyForReuse() => Interlocked.Increment(ref this.ReuseSignals);

			public bool RunCleanup() => TryCleanup();

			public void Rearm() => ResetForReuse();

			public void SetFlagPublic(int flag) => SetFlag(flag);

			public bool TrySetFlagPublic(int flag) => TrySetFlag(flag);
		}

		[Test]
		public void Test_Reuse_Fires_Only_After_Consume_And_Cleanup()
		{
			var future = new TestFuture();

			Assert.That(future.TrySetResult(42), Is.True);
			Assert.That(future.ReuseSignals, Is.Zero);

			// consuming the result is only one of the two release phases
			Assert.That(future.AsValueTask().GetAwaiter().GetResult(), Is.EqualTo(42));
			Assert.That(future.ReuseSignals, Is.Zero, "consumed but not cleaned: must not signal yet");

			// the cleanup is the second phase
			Assert.That(future.RunCleanup(), Is.True);
			Assert.That(future.ReuseSignals, Is.EqualTo(1));
			Assert.That(future.CloseSignals, Is.EqualTo(1));

			// cleanup is once-only: a second call must not re-signal
			Assert.That(future.RunCleanup(), Is.False);
			Assert.That(future.ReuseSignals, Is.EqualTo(1));
		}

		[Test]
		public void Test_Reuse_Fires_When_Cleanup_Precedes_Consumption()
		{
			var future = new TestFuture();

			Assert.That(future.TrySetResult(123), Is.True);
			Assert.That(future.RunCleanup(), Is.True);
			Assert.That(future.ReuseSignals, Is.Zero, "cleaned but not consumed: must not signal yet");

			Assert.That(future.AsValueTask().GetAwaiter().GetResult(), Is.EqualTo(123));
			Assert.That(future.ReuseSignals, Is.EqualTo(1));
		}

		[Test]
		public void Test_Faulted_And_Canceled_Consumption_Still_Releases()
		{
			var canceled = new TestFuture();
			canceled.RunCleanup(); // cleanup of a pending future cancels it
			Assert.That(() => canceled.AsValueTask().GetAwaiter().GetResult(), Throws.InstanceOf<TaskCanceledException>());
			Assert.That(canceled.ReuseSignals, Is.EqualTo(1), "a canceled consumption is still a consumption");

			var faulted = new TestFuture();
			Assert.That(faulted.TrySetException(new InvalidOperationException("kaboom")), Is.True);
			faulted.RunCleanup();
			Assert.That(() => faulted.AsValueTask().GetAwaiter().GetResult(), Throws.InvalidOperationException);
			Assert.That(faulted.ReuseSignals, Is.EqualTo(1));
		}

		[Test]
		public void Test_Abandoned_Future_Never_Signals()
		{
			var future = new TestFuture();
			Assert.That(future.TrySetResult(7), Is.True);
			Assert.That(future.RunCleanup(), Is.True);

			// the consumer walked away without awaiting: the instance must not be handed out again
			Assert.That(future.ReuseSignals, Is.Zero);
		}

		[Test]
		public void Test_Reset_Supports_A_Full_Second_Lifecycle()
		{
			var future = new TestFuture();
			Assert.That(future.TrySetResult(1), Is.True);
			Assert.That(future.AsValueTask().GetAwaiter().GetResult(), Is.EqualTo(1));
			future.RunCleanup();
			Assert.That(future.ReuseSignals, Is.EqualTo(1));

			future.Rearm();

			Assert.That(future.IsReady, Is.False, "rearmed instance must be pending again");
			Assert.That(future.TrySetResult(2), Is.True);
			Assert.That(future.AsValueTask().GetAwaiter().GetResult(), Is.EqualTo(2));
			Assert.That(future.RunCleanup(), Is.True, "the COMPLETED flag must have been reset");
			Assert.That(future.ReuseSignals, Is.EqualTo(2));
		}

		[Test]
		public void Test_Concurrent_Flag_Writes_Do_Not_Lose_Updates()
		{
			// SetFlag and TrySetFlag race each other in production (a completion path publishing FIRED while
			// another path publishes UNPOOLED): whatever the interleaving, both flags must survive.

			const int ITERATIONS = 100_000;

			var futures = new TestFuture[ITERATIONS];
			for (int i = 0; i < ITERATIONS; i++)
			{
				futures[i] = new TestFuture();
			}

			using var barrier = new Barrier(2);

			var left = Task.Factory.StartNew(() =>
			{
				for (int i = 0; i < ITERATIONS; i++)
				{
					barrier.SignalAndWait();
					futures[i].SetFlagPublic(FdbFuture.Flags.UNPOOLED);
				}
			}, TaskCreationOptions.LongRunning);

			var right = Task.Factory.StartNew(() =>
			{
				for (int i = 0; i < ITERATIONS; i++)
				{
					barrier.SignalAndWait();
					futures[i].TrySetFlagPublic(FdbFuture.Flags.FIRED);
				}
			}, TaskCreationOptions.LongRunning);

			Task.WaitAll(left, right);

			int lost = 0;
			for (int i = 0; i < ITERATIONS; i++)
			{
				if (!futures[i].HasFlag(FdbFuture.Flags.UNPOOLED) || !futures[i].HasFlag(FdbFuture.Flags.FIRED))
				{
					lost++;
				}
			}
			Assert.That(lost, Is.Zero, $"{lost:N0} of {ITERATIONS:N0} racing flag pairs lost one of the two writes");
		}

		[Test]
		public void Test_Stale_ValueTask_Fails_Loudly_And_Does_Not_Corrupt_The_Gate()
		{
			var future = new TestFuture();
			Assert.That(future.TrySetResult(1), Is.True);
			var stale = future.AsValueTask();
			Assert.That(stale.GetAwaiter().GetResult(), Is.EqualTo(1));
			future.RunCleanup();
			future.Rearm();

			// consuming the previous lifecycle's ValueTask must throw (token mismatch), never return the new result...
			Assert.That(() => stale.GetAwaiter().GetResult(), Throws.InvalidOperationException);

			// ...and must not have consumed a release of the new lifecycle
			Assert.That(future.TrySetResult(2), Is.True);
			Assert.That(future.ReuseSignals, Is.EqualTo(1), "the stale consumption must not count toward the new gate");
			Assert.That(future.AsValueTask().GetAwaiter().GetResult(), Is.EqualTo(2));
			future.RunCleanup();
			Assert.That(future.ReuseSignals, Is.EqualTo(2), "the new lifecycle still needs its own two releases");
		}

	}

	/// <summary>Tests of the interaction between the future wrappers and the caller's cancellation token,
	/// against a live cluster.</summary>
	[TestFixture]
	[Category("Fdb-Client-Live")]
	public class FutureCancellationFacts : FdbTest
	{

		[Test]
		public async Task Test_Batch_Reads_Are_Not_Cancelled_By_Another_Operations_Token()
		{
			// every read below uses a token that is never cancelled while it runs, so no read may ever surface
			// a TaskCanceledException, whatever the scheduling inside the wrapper. The installed hook stalls the
			// initializing thread right before it attaches the cancellation registration: if that registration
			// can lose the race against the operation's own completion, it stays armed past the lifecycle, and
			// the transaction disposal that follows every operation fires it into an innocent later operation.

			using var db = await OpenTestPartitionAsync();
			var location = db.Root;

			await db.WriteAsync(async tr =>
			{
				var subspace = await location.Resolve(tr);
				for (int i = 0; i < 16; i++)
				{
					tr.Set(subspace.Key("probe", i), Slice.FromInt32(i));
				}
			}, this.Cancellation);

			FdbFuture.BeforeCancellationRegistration = static () => Thread.Sleep(1);
			try
			{
				var workers = Enumerable.Range(0, 4).Select(w => Task.Run(async () =>
				{
					for (int i = 0; i < 50; i++)
					{
						int batch = 1 + ((w + i) & 3);
						var results = await db.ReadAsync(async tr =>
						{
							var subspace = await location.Resolve(tr);
							return await tr.GetValuesAsync(Enumerable.Range(i, batch).Select(k => subspace.Key("probe", k & 15)));
						}, this.Cancellation);
						Assert.That(results, Has.Length.EqualTo(batch));
					}
				}, this.Cancellation)).ToArray();

				try
				{
					await Task.WhenAll(workers);
				}
				catch (OperationCanceledException ex) when (!this.Cancellation.IsCancellationRequested)
				{
					Assert.Fail($"a read was cancelled even though its token was never cancelled: {ex}");
				}
			}
			finally
			{
				FdbFuture.BeforeCancellationRegistration = null;
			}
		}

	}

}

#endif
