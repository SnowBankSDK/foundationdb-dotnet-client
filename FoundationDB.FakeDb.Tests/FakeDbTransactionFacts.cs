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
				var handler = (FakeDbStore.TransactionHandler) FdbTransactionDebugger.GetHandler(tr);
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

			var handler = (FakeDbStore.TransactionHandler) FdbTransactionDebugger.GetHandler(tr);
			var snapshot = handler.GetSnapshotBlocking();
			foreach (var entry in FakeDbDebugger.GetSnapshotMutations(snapshot).IterateOrdered())
			{
				Log($"mutation: [{entry.Begin} .. {entry.End}) = {entry.Value}");
			}

			var resolved = await tr.GetKeyAsync(new KeySelector(Key("k6"), true, -1));
			Assert.That(resolved, Is.EqualTo(Key("k5")), "LLE(k6) - 1 over the merged view");
		}

		[Test]
		public async Task Test_All_Mutation_Types_Are_Accepted_At_The_Emulation_Floor()
		{
			// FakeDb's emulation floor is api level 610 (MIN_API_VERSION), where every mutation type is already
			// available — so the "old level rejects newer mutations" behavior is unreachable on the emulator by
			// design. The managed client's gate consults the DATABASE's selected level (identically for a real
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
			// RetryDelayMaximum, the retry loop STALLS while virtual time is frozen (however long we really wait) and
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

			// the first backoff is on VIRTUAL time: a generous real settle must not let the loop past the first delay
			await Wait(200);
			Assert.That(op.IsCompleted, Is.False, "the retry backoff must be gated by virtual time, not the wall clock");
			Assert.That(attempts, Is.EqualTo(1), "the retry loop is parked in its first backoff");

			// advancing virtual time past the backoffs lets the loop retry and finally commit on the 3rd attempt
			await AdvanceAndPump(fake, TimeSpan.FromSeconds(4), TimeSpan.FromMilliseconds(250));
			for (int i = 0; i < 100 && !op.IsCompleted; i++)
			{
				await Wait(10);
			}
			Assert.That(op.IsCompleted, Is.True, "advancing virtual time must release the backoff and complete the retry loop");
			await op;
			Assert.That(attempts, Is.EqualTo(3), "two retryable failures, then success");
		}

	}

}
