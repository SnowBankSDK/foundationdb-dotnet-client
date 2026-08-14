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

// ReSharper disable AccessToDisposedClosure
namespace FoundationDB.Testing.Tests
{
	using FoundationDB.Client;
	using FoundationDB.Storage;
	using Microsoft.Extensions.Time.Testing;

	/// <summary>FakeDb-specific transaction tests that have no real-cluster equivalent (virtual clock, store internals).</summary>
	/// <remarks>The dual-backend transaction conformance tests live in <c>Conformance/TransactionConformanceFacts.cs</c>; this fixture is only for behaviors that exist solely on the emulator.</remarks>
	[TestFixture]
	public class FakeDbTransactionFacts : FakeDbTest
	{

		[Test]
		public async Task Test_Atomic_Add_Inside_Uncommitted_Cleared_Range_Survives_Commit()
		{
			// pinned by the RYW fuzzer: an atomic add on a key covered by an earlier uncommitted clear-range
			// must create the key with the operand value (add over nil), like the real cluster does
			var db = await OpenTestDatabaseAsync();

			await db.WriteAsync(tr => tr.Set(Key("k1"), Value("c1")), this.Cancellation);

			using (var tr = db.BeginTransaction(FdbTransactionMode.Default, this.Cancellation))
			{
				tr.ClearRange(Key("k0"), Key("k6"));
				tr.Atomic(Key("k1"), Slice.FromFixed64(0x5D), FdbMutationType.Add);

				// diagnostic: dump the mutation log before the commit
				var handler = (FakeDbStore.TransactionHandler<ColaCommittedCursor>) FdbTransactionDebugger.GetHandler(tr);
				var snapshot = handler.GetSnapshotBlocking();
				foreach (var entry in FakeDbDebugger.GetSnapshotMutations(snapshot).IterateOrdered())
				{
					Log($"mutation: [{entry.Begin} .. {entry.End}) = {entry.Value}");
				}

				// the merged read must also see add-over-nil (not add-over-committed)
				var live = await tr.GetAsync(Key("k1"));
				Assert.That(live, Is.EqualTo(Slice.FromFixed64(0x5D)), "read-your-writes over clear-then-atomic");

				await tr.CommitAsync();
			}

			var actual = await db.ReadAsync(tr => tr.GetAsync(Key("k1")), this.Cancellation);
			Assert.That(actual, Is.EqualTo(Slice.FromFixed64(0x5D)), "the key must exist with the operand value after commit");
		}

		[Test]
		public async Task Test_Cancel_After_Dispose_Is_A_Noop()
		{
			// At teardown a transaction can be cancelled and disposed at the same time: the client sets the state to
			// DISPOSED and then disposes the handler, while a Cancel that already won the state race reaches the handler
			// afterwards. FdbTransaction.Dispose guards its OWN CancellationTokenSource with catch(ObjectDisposedException);
			// the FakeDb handler's Cancel must do the same, so cancelling an already-disposed lifetime is a no-op rather
			// than an ObjectDisposedException surfacing at teardown.
			var db = await OpenTestDatabaseAsync();
			var tr = db.BeginTransaction(FdbTransactionMode.Default, this.Cancellation);
			var handler = FdbTransactionDebugger.GetHandler(tr);

			// the racing Dispose disposed the handler's lifetime CTS first
			handler.Dispose();

			// the later Cancel must not throw
			Assert.That(() => handler.Cancel(), Throws.Nothing, "cancelling an already-disposed transaction handler must be a no-op");
		}

		[Test]
		public async Task Test_Selector_Resolution_Over_Mixed_Uncommitted_Mutations()
		{
			// pinned by the RYW fuzzer (seed 173): LastLessOrEqual(k6) - 1 over a mix of clears, re-sets and
			// atomic adds must resolve against the merged visible keys {k0,k1,k2,k4,k5,k6} => k5
			var db = await OpenTestDatabaseAsync();

			using var tr = db.BeginTransaction(FdbTransactionMode.Default, this.Cancellation);
			tr.ClearRange(Key("k0"), Key("k5"));
			tr.Atomic(Key("k4"), Slice.FromFixed64(0x2F), FdbMutationType.Add);
			tr.Set(Key("k0"), Value("v5"));
			tr.Clear(Key("k2"));
			tr.Clear(Key("k6"));
			tr.Clear(Key("k1"));
			tr.Atomic(Key("k5"), Slice.FromFixed64(0x4F), FdbMutationType.Add);
			_ = await tr.GetAsync(Key("k7"));
			tr.ClearRange(Key("k0"), Key("k0z"));
			_ = await tr.GetRangeAsync(KeySelector.FirstGreaterOrEqual(Key("k0")), KeySelector.FirstGreaterThan(Key("k2")), new FdbRangeOptions { IsReversed = true });
			tr.Set(Key("k2"), Value("v7"));
			tr.Set(Key("k6"), Value("v1"));
			tr.Set(Key("k1"), Value("v1"));
			tr.Set(Key("k0"), Value("v5"));
			_ = await tr.GetAsync(Key("k2"));

			var handler = (FakeDbStore.TransactionHandler<ColaCommittedCursor>) FdbTransactionDebugger.GetHandler(tr);
			var snapshot = handler.GetSnapshotBlocking();
			foreach (var entry in FakeDbDebugger.GetSnapshotMutations(snapshot).IterateOrdered())
			{
				Log($"mutation: [{entry.Begin} .. {entry.End}) = {entry.Value}");
			}

			var resolved = await tr.GetKeyAsync(new KeySelector(Key("k6"), true, -1));
			Assert.That(resolved, Is.EqualTo(Key("k5")), "LLE(k6) - 1 over the merged view");
		}

		[Test]
		public async Task Test_Merged_Range_Reads_Stay_Bounded_In_A_Write_Heavy_Transaction()
		{
			// Once a transaction has a pending write, every selector resolves through the merged view (committed
			// snapshot + local mutations). The merged resolution used to enumerate the entire committed store and,
			// per candidate key, linearly scan the whole local mutation set: O(committedKeys x pendingMutations)
			// per read, so a transaction interleaving a few hundred range reads with writes took minutes of CPU
			// where the real cluster takes milliseconds. The bounded path walks only the keys the selector needs
			// and finds the covering mutation by seek, which keeps this transaction inside a generous real ceiling.
			var db = await OpenTestDatabaseAsync();

			// a committed store big enough that a per-read full-store enumeration dominates
			const int CommittedKeys = 4_000;
			for (int batch = 0; batch < CommittedKeys; batch += 500)
			{
				int start = batch;
				await db.WriteAsync(tr =>
				{
					for (int i = start; i < start + 500; i++)
					{
						tr.Set(Key($"doc{i:D6}"), Value($"v{i}"));
					}
				}, this.Cancellation);
			}

			var sw = System.Diagnostics.Stopwatch.StartNew();
			using (var tr = db.BeginTransaction(FdbTransactionMode.Default, this.Cancellation))
			{
				for (int i = 0; i < 150; i++)
				{
					var prefix = $"doc{(i * 17) % CommittedKeys:D6}";
					_ = await tr.GetRangeAsync(
						KeySelector.FirstGreaterOrEqual(Key(prefix)),
						KeySelector.FirstGreaterOrEqual(Key(prefix + "z")),
						new FdbRangeOptions { Limit = 5 });
					tr.Set(Key(prefix + "-a"), Value($"w{i}"));
					tr.Set(Key(prefix + "-b"), Value($"w{i}"));
				}
				await tr.CommitAsync();
			}
			sw.Stop();
			Log($"write-heavy merged transaction: {sw.Elapsed.TotalSeconds:N1}s for 150 reads over {CommittedKeys:N0} committed keys");
			Assert.That(sw.Elapsed, Is.LessThan(TimeSpan.FromSeconds(15)), "merged-view reads must stay bounded to the requested neighborhood (the full-store path takes minutes here)");
		}

		[Test]
		public async Task Test_All_Mutation_Types_Are_Accepted_At_The_Emulation_Floor()
		{
			// FakeDb's emulation floor is api level 610 (MIN_API_VERSION), where every mutation type is already
			// available, so the "old level rejects newer mutations" behavior is unreachable on the emulator by
			// design. The managed client's gate consults the database's selected level (identically for a real
			// database); this pins that the floor level accepts the whole mutation vocabulary.
			var store = new FakeDbStore(apiVersion: FakeDbStore.MIN_API_VERSION);
			using var db = store.OpenDatabase(FdbPath.Root, readOnly: false);

			using (var tr = db.BeginTransaction(FdbTransactionMode.Default, this.Cancellation))
			{
				tr.Atomic(Key("k1"), Slice.FromFixed64(1), FdbMutationType.Add);
				tr.Atomic(Key("k2"), Slice.FromFixed64(1), FdbMutationType.Max);
				tr.Atomic(Key("k3"), Slice.FromStringAscii("mm"), FdbMutationType.ByteMin);
				tr.Atomic(Key("k4"), Slice.FromStringAscii("mm"), FdbMutationType.ByteMax);
				tr.Atomic(Key("k5"), Slice.FromFixed64(1), FdbMutationType.CompareAndClear);
				await tr.CommitAsync();
			}

			var actual = await db.ReadAsync(tr => tr.GetAsync(Key("k3")), this.Cancellation);
			Assert.That(actual, Is.EqualTo(Slice.FromStringAscii("mm")));
		}

		[Test]
		public async Task Test_Conflicting_Keys_Report_Serves_Boundary_Pairs()
		{
			// the conflicting-keys report is transaction-local: a failed commit collects the conflicting read
			// ranges (when the option is set), and the special keyspace serves one boundary pair per range
			var db = await OpenTestDatabaseAsync();
			await db.WriteAsync(tr =>
			{
				tr.Set(Key("A"), Value("a"));
				tr.Set(Key("B"), Value("b"));
			}, this.Cancellation);

			using var t1 = db.BeginTransaction(FdbTransactionMode.Default, this.Cancellation);
			using var t2 = db.BeginTransaction(FdbTransactionMode.Default, this.Cancellation);

			t1.Options.WithReportConflictingKeys();
			_ = await t1.GetAsync(Key("A"));
			_ = await t1.GetAsync(Key("B"));
			t1.Set(Key("X"), Value("x"));

			t2.Set(Key("A"), Value("a2"));
			await t2.CommitAsync();

			Assert.That(async () => await t1.CommitAsync(), Throws.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.NotCommitted));

			var res = await t1.GetRange(FdbSystemKey.TransactionConflictingKeys.ToRange()).ToArrayAsync();
			foreach (var kv in res) Log($"  - {kv.Key:K} = {kv.Value:V}");
			Assert.That(res, Has.Length.EqualTo(2), "one conflicting read range = two boundary entries");
			Assert.That(res[0].Value, Is.EqualTo(Slice.FromStringAscii("1")), "range begin marker");
			Assert.That(res[1].Value, Is.EqualTo(Slice.FromStringAscii("0")), "range end marker");
			Assert.That(res[0].Key.Substring(Fdb.System.TransactionConflictingKeysPrefix.Count), Is.EqualTo(Key("A")));
		}

		[Test]
		public async Task Test_Retry_Backoff_Runs_On_Virtual_Time()
		{
			// A retryable error backs off on the store's TimeProvider: with a fake clock and a realistic
			// RetryDelayMaximum, the retry loop stalls while virtual time is frozen (however long we really wait) and
			// proceeds only when virtual time advances. The default policy (RetryDelayMaximum == 0) retries instantly,
			// keeping normal tests fast.

			var fake = new FakeTimeProvider();
			var store = new FakeDbStore(730, time: fake)
			{
				RetryDelayInitial = TimeSpan.FromMilliseconds(50),
				RetryDelayMaximum = TimeSpan.FromSeconds(1),
			};
			using var db = store.OpenDatabase(FdbPath.Root, readOnly: false);

			int attempts = 0;
			var op = db.WriteAsync(
				tr =>
				{
					attempts++;
					if (attempts <= 2) throw new FdbException(FdbError.NotCommitted, "simulated conflict");
					// succeed on the 3rd attempt (an empty write commits fine)
				},
				this.Cancellation
			);

			// wait (on the WALL clock) for the transaction's first attempt to run and park on its backoff; without
			// advancing the fake clock, the loop must stay parked there, because that backoff is gated on VIRTUAL time
			await WaitUntil(() => attempts >= 1, TimeSpan.FromSeconds(5), "the transaction should execute its first attempt");
			Assert.That(op.IsCompleted, Is.False, "the retry backoff must be gated by virtual time, not the wall clock");
			Assert.That(attempts, Is.EqualTo(1), "the retry loop is parked in its first backoff");

			// advancing virtual time past the backoffs lets the loop retry and finally commit on the 3rd attempt
			await AdvanceAndPump(fake, TimeSpan.FromSeconds(4), TimeSpan.FromMilliseconds(250));
			await WaitUntil(() => op.IsCompleted, TimeSpan.FromSeconds(5), "advancing virtual time must release the backoff and complete the retry loop");
			await op;
			Assert.That(attempts, Is.EqualTo(3), "two retryable failures, then success");
		}

	}

}
