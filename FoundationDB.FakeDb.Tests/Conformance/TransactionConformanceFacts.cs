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

// ReSharper disable AccessToDisposedClosure
// ReSharper disable AccessToModifiedClosure
// ReSharper disable ReplaceAsyncWithTaskReturn
// ReSharper disable JoinDeclarationAndInitializer
// ReSharper disable TooWideLocalVariableScope
// ReSharper disable StringLiteralTypo
// ReSharper disable ConvertToUsingDeclaration
// ReSharper disable MethodHasAsyncOverload

namespace FoundationDB.Client.Tests
{
	using System.Diagnostics;
	using System.Text;
	using SnowBank.Buffers;
	using SnowBank.Data.Tuples;

	public abstract class TransactionConformanceFacts : FdbTest
	{

		/// <summary>Opts this test out of watch buggify so it sees clean, deterministic watch semantics (no-op on a real cluster; on the
		/// FakeDb head it disables the seeded chaos the harness turns on by default). Call at the top of a test that asserts exact watch timing.</summary>
		protected virtual void RequireCleanWatches() { }

		[Test]
		public async Task Test_Can_Create_And_Dispose_Transactions()
		{
			using var db = await OpenTestDatabaseAsync();
			Assert.That(db, Is.InstanceOf<FdbDatabase>(), "This test only works directly on FdbDatabase");

			using (var tr = (FdbTransaction) db.BeginTransaction(this.Cancellation))
			{
				Assert.That(tr, Is.Not.Null, "BeginTransaction should return a valid instance");
				Assert.That(FdbTransactionDebugger.GetState(tr) == FdbTransactionDebugger.TransactionState.Ready, "Transaction should be in ready state");
				Assert.That(FdbTransactionDebugger.IsStillAlive(tr), Is.True, "Transaction should be alive");
				Assert.That(FdbTransactionDebugger.GetHandler(tr).IsClosed, Is.False, "Transaction handle should not be closed");
				Assert.That(tr.Database, Is.SameAs(db), "Transaction should reference the parent Database");
				Assert.That(tr.Size, Is.Zero, "Estimated size should be zero");
				Assert.That(tr.IsReadOnly, Is.False, "Transaction is not read-only");
				Assert.That(tr.IsSnapshot, Is.False, "Transaction is not in snapshot mode by default");

				// manually dispose the transaction
				// ReSharper disable once DisposeOnUsingVariable
				tr.Dispose();

				Assert.That(FdbTransactionDebugger.GetState(tr) == FdbTransactionDebugger.TransactionState.Disposed, "Transaction should now be in the disposed state");
				Assert.That(FdbTransactionDebugger.IsStillAlive(tr), Is.False, "Transaction should be not be alive anymore");
				Assert.That(FdbTransactionDebugger.GetHandler(tr).IsClosed, Is.True, "Transaction handle should now be closed");

				// multiple calls to dispose should not do anything more
				// ReSharper disable once DisposeOnUsingVariable
				Assert.That(() => { tr.Dispose(); }, Throws.Nothing);
			}
		}

		[Test]
		public async Task Test_Can_Get_A_Snapshot_Version_Of_A_Transaction()
		{
			using var db = await OpenTestDatabaseAsync();

			using var tr = db.BeginTransaction(this.Cancellation);

			Assert.That(tr, Is.Not.Null, "BeginTransaction should return a valid instance");
			Assert.That(tr.IsSnapshot, Is.False, "Transaction is not in snapshot mode by default");

			// verify that the snapshot version is also ok
			var snapshot = tr.Snapshot;
			Assert.That(snapshot, Is.Not.Null, "tr.Snapshot should never return null");
			Assert.That(snapshot.IsSnapshot, Is.True, "Snapshot transaction should be marked as such");
			Assert.That(tr.Snapshot, Is.SameAs(snapshot), "tr.Snapshot should not create a new instance");
			Assert.That(snapshot.Id, Is.EqualTo(tr.Id), "Snapshot transaction should have the same id as its parent");
			Assert.That(snapshot.Context, Is.SameAs(tr.Context), "Snapshot transaction should have the same context as its parent");
		}

		[Test]
		public async Task Test_Creating_A_ReadOnly_Transaction_Throws_When_Writing()
		{
			using var db = await OpenTestDatabaseAsync();
			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
			{
				Assert.That(tr, Is.Not.Null);

				var subspace = await db.Root.Resolve(tr);

				// reading should not fail
				await tr.GetAsync(subspace.Key("Hello"));

				// any attempt to recast into a writable transaction should fail!
				var tr2 = (IFdbTransaction)tr;
				Assert.That(tr2.IsReadOnly, Is.True, "Transaction should be marked as readonly");
				Assert.That(() => tr2.Set(subspace.Key("ReadOnly", "Hello"), Slice.Empty), Throws.InvalidOperationException);
				Assert.That(() => tr2.Clear(subspace.Key("ReadOnly", "Hello")), Throws.InvalidOperationException);
				Assert.That(() => tr2.ClearRange(subspace.Key("ReadOnly", "ABC"), subspace.Key("ReadOnly", "DEF")), Throws.InvalidOperationException);
				Assert.That(() => tr2.AtomicIncrement32(subspace.Key("ReadOnly", "Counter")), Throws.InvalidOperationException);
			}
		}

		[Test]
		public async Task Test_Creating_Concurrent_Transactions_Are_Independent()
		{
			using var db = await OpenTestDatabaseAsync();
			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			IFdbTransaction? tr1 = null;
			IFdbTransaction? tr2 = null;
			try
			{
				// concurrent transactions should have separate FDB_FUTURE* handles

				tr1 = db.BeginTransaction(this.Cancellation);
				tr2 = db.BeginTransaction(this.Cancellation);

				Assert.That(tr1, Is.Not.Null);
				Assert.That(tr2, Is.Not.Null);

				Assert.That(tr1, Is.Not.SameAs(tr2), "Should create two different transaction objects");

				Assert.That(tr1, Is.InstanceOf<FdbTransaction>());
				Assert.That(tr2, Is.InstanceOf<FdbTransaction>());
				Assert.That(FdbTransactionDebugger.GetHandler(tr1), Is.Not.EqualTo(FdbTransactionDebugger.GetHandler(tr2)), "Should have different FDB_FUTURE* handles");

				// disposing the first should not impact the second

				tr1.Dispose();

				Assert.That(FdbTransactionDebugger.IsStillAlive(tr1), Is.False, "First transaction should be dead");
				Assert.That(FdbTransactionDebugger.GetHandler(tr1).IsClosed, Is.True, "First FDB_FUTURE* handle should be closed");

				Assert.That(FdbTransactionDebugger.IsStillAlive(tr2), Is.True, "Second transaction should still be alive");
				Assert.That(FdbTransactionDebugger.GetHandler(tr2).IsClosed, Is.False, "Second FDB_FUTURE* handle should still be opened");
			}
			finally
			{
				tr1?.Dispose();
				tr2?.Dispose();
			}
		}

		[Test]
		public async Task Test_Commiting_An_Empty_Transaction_Does_Nothing()
		{
			using var db = await OpenTestDatabaseAsync();
			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			using var tr = db.BeginTransaction(this.Cancellation);
			Assert.That(tr, Is.InstanceOf<FdbTransaction>());

			// do nothing with it
			await tr.CommitAsync();
			// => should not fail!

			Assert.That(FdbTransactionDebugger.IsStillAlive(tr), Is.False);
			Assert.That(FdbTransactionDebugger.GetState(tr), Is.EqualTo(FdbTransactionDebugger.TransactionState.Committed));
		}

		[Test]
		public async Task Test_Resetting_An_Empty_Transaction_Does_Nothing()
		{
			using var db = await OpenTestDatabaseAsync();
			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			using var tr = db.BeginTransaction(this.Cancellation);
			// do nothing with it
			tr.Reset();
			// => should not fail!

			// Committed version should be -1 (where is it specified?)
			long ver = tr.GetCommittedVersion();
			Assert.That(ver, Is.EqualTo(-1), "Committed version of empty transaction should be -1");
		}

		[Test]
		public async Task Test_Cancelling_An_Empty_Transaction_Does_Nothing()
		{
			using var db = await OpenTestDatabaseAsync();
			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			using var tr = db.BeginTransaction(this.Cancellation);
			Assert.That(tr, Is.InstanceOf<FdbTransaction>());

			// do nothing with it
			tr.Cancel();
			// => should not fail!

			Assert.That(FdbTransactionDebugger.IsStillAlive(tr), Is.False);
			Assert.That(FdbTransactionDebugger.GetState(tr), Is.EqualTo(FdbTransactionDebugger.TransactionState.Canceled));
		}

		[Test]
		public async Task Test_Cancelling_Transaction_Before_Commit_Should_Throw_Immediately()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);
			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			using var tr = db.BeginTransaction(this.Cancellation);
			var subspace = await db.Root.Resolve(tr);

			tr.Set(subspace.Key(1), Text("hello"));
			tr.Cancel();

			await AssertThrowsFdbErrorAsync(
				() => tr.CommitAsync(),
				FdbError.TransactionCancelled,
				"Committing an already cancelled exception should fail"
			);
		}

		[Test]
		public async Task Test_Cancelling_Transaction_During_Commit_Should_Abort_Task()
		{
			// we need to simulate some load on the db, to be able to cancel a Commit after it started, but before it completes
			// => we will try to commit a very large transaction in order to give us some time
			// note: if this test fails because it commits to fast, that means that your system is foo fast :)

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			var rnd = new Random();

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			using var tr = db.BeginTransaction(this.Cancellation);
			var subspace = await db.Root.Resolve(tr);

			// Writes about 5 MB of stuff in 100k chunks
			for (int i = 0; i < 50; i++)
			{
				tr.Set(subspace.Key(i), Slice.Random(rnd, 100 * 1000));
			}

			// start commiting
			var t = tr.CommitAsync();

			// but almost immediately cancel the transaction
			await Task.Delay(1);
			Assume.That(t.IsCompleted, Is.False, "Commit task already completed before having a chance to cancel");

			tr.Cancel();

			await AssertThrowsFdbErrorAsync(
				() => t,
				FdbError.TransactionCancelled,
				"Cancelling a transaction that is writing to the server should fail the commit task"
			);
		}

		[Test]
		public async Task Test_Cancelling_Token_During_Commit_Should_Abort_Task()
		{
			// we need to simulate some load on the db, to be able to cancel the token passed to Commit after it started, but before it completes
			// => we will try to commit a very large transaction in order to give us some time
			// note: if this test fails because it commits to fast, that means that your system is foo fast :)

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			var rnd = new Random();

			using var cts = new CancellationTokenSource();
			using var tr = db.BeginTransaction(cts.Token);
			var subspace = await db.Root.Resolve(tr);

			// Writes about 5 MB of stuff in 100k chunks
			for (int i = 0; i < 50; i++)
			{
				tr.Set(subspace.Key(i), Slice.Random(rnd, 100 * 1000));
			}

			// start commiting with a cancellation token
			var t = tr.CommitAsync();

			// but almost immediately cancel the token source
			await Task.Delay(1, this.Cancellation);

			Assume.That(t.IsCompleted, Is.False, "Commit task already completed before having a chance to cancel");
			cts.Cancel();

			Assert.That(async () => await t, Throws.InstanceOf<TaskCanceledException>(), "Cancelling a token passed to CommitAsync that is still pending should cancel the task");
		}

		[Test]
		public async Task Test_Can_Get_Transaction_Read_Version()
		{
			using var db = await OpenTestDatabaseAsync();
			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			using var tr = db.BeginTransaction(this.Cancellation);

			long ver = await tr.GetReadVersionAsync();
			Assert.That(ver, Is.GreaterThan(0), "Read version should be > 0");

			// if we ask for it again, we should have the same value
			long ver2 = await tr.GetReadVersionAsync();
			Assert.That(ver2, Is.EqualTo(ver), "Read version should not change inside same transaction");
		}

		[Test]
		public async Task Test_Write_And_Read_Simple_Keys()
		{
			if (!this.UseRealServer) Assert.Ignore("FakeDb divergence: Slice returned by GetAsync exposes the untrimmed backing buffer (Array.Length way larger than Count) instead of a right-sized array");

			// test that we can read and write simple keys

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			long ticks = DateTime.UtcNow.Ticks;
			long writeVersion;
			long readVersion;

			// write a bunch of keys
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				tr.Set(subspace.Key("hello"), Text("World!"));
				tr.Set(subspace.Key("timestamp"), Slice.FromInt64(ticks));
				tr.Set(subspace.Key("blob"), new byte[] { 42, 123, 7 }.AsSlice());

				await tr.CommitAsync();

				writeVersion = tr.GetCommittedVersion();
				Assert.That(writeVersion, Is.GreaterThan(0), "Committed version of non-empty transaction should be > 0");
			}

			// read them back
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				readVersion = await tr.GetReadVersionAsync();
				Assert.That(readVersion, Is.GreaterThan(0), "Read version should be > 0");

				{
					var bytes = await tr.GetAsync(subspace.Key("hello")); // => 1007 "past_version"
					Assert.That(bytes.Array, Is.Not.Null);
					Assert.That(Encoding.UTF8.GetString(bytes.Array, bytes.Offset, bytes.Count), Is.EqualTo("World!"));
				}
				{
					var bytes = await tr.GetAsync(subspace.Key("timestamp"));
					Assert.That(bytes.Array, Is.Not.Null);
					Assert.That(bytes.ToInt64(), Is.EqualTo(ticks));
				}
				{
					var bytes = await tr.GetAsync(subspace.Key("blob"));
					Assert.That(bytes.Array, Is.Not.Null);
					Assert.That(bytes.Array, Is.EqualTo(new byte[] { 42, 123, 7 }));
				}
			}

			Assert.That(readVersion, Is.GreaterThanOrEqualTo(writeVersion), "Read version should not be before previous committed version");
		}

		[Test]
		public async Task Test_Write_And_Read_Encoded_Keys()
		{
			// test that we can read and write encoded keys and values

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			long ticks = DateTime.UtcNow.Ticks;
			long writeVersion;
			long readVersion;

			var uuid = Uuid128.NewUuid();

			Log("# Write encoded keys and values...");
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				tr.Set(subspace.Key("hello"), FdbValue.ToTextUtf8("World!"));
				tr.Set(subspace.Key("timestamp"), FdbValue.ToCompactLittleEndian(ticks));
				tr.Set(subspace.Key("uuid"), uuid);
				tr.Set(subspace.Key("tuple"), STuple.Create("hello", 123, true, "world"));
				tr.Set(subspace.Key("blob"), [ 42, 123, 7 ]);

				await tr.CommitAsync();

				writeVersion = tr.GetCommittedVersion();
				Assert.That(writeVersion, Is.GreaterThan(0), "Committed version of non-empty transaction should be > 0");
			}

			Log("# Read them back...");
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				readVersion = await tr.GetReadVersionAsync();
				Assert.That(readVersion, Is.GreaterThan(0), "Read version should be > 0");

				{
					var bytes = await tr.GetAsync(subspace.Key("hello"));
					Log($"> `hello`: {bytes:X}");
					Assert.That(bytes.ToStringUtf8(), Is.EqualTo("World!"));
				}
				{
					var bytes = await tr.GetAsync(subspace.Key("timestamp"));
					Log($"> `timestamp`: {bytes:X}");
					Assert.That(bytes.ToInt64(), Is.EqualTo(ticks));
				}
				{
					var bytes = await tr.GetAsync(subspace.Key("uuid"));
					Log($"> `uuid`: {bytes:X}");
					Assert.That(bytes.ToUuid128(), Is.EqualTo(uuid));
				}
				{
					var bytes = await tr.GetAsync(subspace.Key("tuple"));
					Log($"> `tuple`: {bytes:X}");
					Assert.That(TuPack.Unpack(bytes), Is.EqualTo(STuple.Create("hello", 123, true, "world")));
				}
				{
					var bytes = await tr.GetAsync(subspace.Key("blob"));
					Log($"> `blob`: {bytes:X}");
					Assert.That(bytes.ToArray(), Is.EqualTo(new byte[] { 42, 123, 7 }));
				}
			}

			Assert.That(readVersion, Is.GreaterThanOrEqualTo(writeVersion), "Read version should not be before previous committed version");
		}

		[Test]
		public async Task Test_Write_Multiple_Simple_Values()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			const int N = 50;

			var items = Enumerable.Range(0, N).Select(i => (Key: i, Value: $"Value of #{i}")).ToArray();

			Log($"# Write {N} values...");
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				tr.SetValues(items, item => subspace.Key(item.Key).ToSlice(), item => Slice.FromStringUtf8(item.Value));
				await tr.CommitAsync();
			}

			Log("# Read them back...");
			using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				foreach (var item in items)
				{
					var value = await tr.GetAsync(subspace.Key(item.Key));
					Assert.That(value.ToStringUtf8(), Is.EqualTo(item.Value));
				}
			}

		}

		[Test]
		public async Task Test_Write_Multiple_Encoded_Values()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			const int N = 50;

			var items = Enumerable.Range(0, N).Select(i => (Key: i, Value: $"Value of #{i}")).ToArray();

			Log($"# Write {N} values...");
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				tr.SetValues(items.Select(item => (subspace.Key(item.Key), FdbValue.ToTextUtf8(item.Value))));
				await tr.CommitAsync();
			}

			Log("# Read them back...");
			using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				foreach (var item in items)
				{
					var value = await tr.GetAsync(subspace.Key(item.Key));
					Assert.That(value.ToStringUtf8(), Is.EqualTo(item.Value));
				}
			}

		}

		[Test]
		public async Task Test_Can_Resolve_Key_Selector()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			#region Insert a bunch of keys ...

			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				// keys
				// - (test,) + \0
				// - (test, 0) .. (test, 19)
				// - (test,) + \xFF
				tr.Set(subspace.First(), Text("min"));
				for (int i = 0; i < 20; i++)
				{
					tr.Set(subspace.Key(i), Text(i.ToString()));
				}

				tr.Set(subspace.Last(), Text("max"));
				await tr.CommitAsync();
			}

			#endregion

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				// >= 0
				var sel = subspace.Key(0).FirstGreaterOrEqual();
				Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(subspace.Key(0)), "fGE(0) should return 0");
				Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(subspace.First()), "fGE(0)-1 should return minKey");
				Assert.That(await tr.GetKeyAsync(sel + 1), Is.EqualTo(subspace.Key(1)), "fGE(0)+1 should return 1");

				// > 0
				sel = subspace.Key(0).FirstGreaterThan();
				Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(subspace.Key(1)), "fGT(0) should return 1");
				Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(subspace.Key(0)), "fGT(0)-1 should return 0");
				Assert.That(await tr.GetKeyAsync(sel + 1), Is.EqualTo(subspace.Key(2)), "fGT(0)+1 should return 2");

				// <= 10
				sel = subspace.Key(10).LastLessOrEqual();
				Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(subspace.Key(10)), "lLE(10) should return 10");
				Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(subspace.Key(9)), "lLE(10)-1 should return 9");
				Assert.That(await tr.GetKeyAsync(sel + 1), Is.EqualTo(subspace.Key(11)), "lLE(10)+1 should return 11");

				// < 10
				sel = subspace.Key(10).LastLessThan();
				Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(subspace.Key(9)), "lLT(10) should return 9");
				Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(subspace.Key(8)), "lLT(10)-1 should return 8");
				Assert.That(await tr.GetKeyAsync(sel + 1), Is.EqualTo(subspace.Key(10)), "lLT(10)+1 should return 10");

				// < 0
				sel = subspace.Key(0).LastLessThan();
				Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(subspace.First()), "lLT(0) should return minKey");
				Assert.That(await tr.GetKeyAsync(sel + 1), Is.EqualTo(subspace.Key(0)), "lLT(0)+1 should return 0");

				// >= 20
				sel = subspace.Key(20).FirstGreaterOrEqual();
				Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(subspace.Last()), "fGE(20) should return maxKey");
				Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(subspace.Key(19)), "fGE(20)-1 should return 19");

				// > 19
				sel = subspace.Key(19).FirstGreaterThan();
				Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(subspace.Last()), "fGT(19) should return maxKey");
				Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(subspace.Key(19)), "fGT(19)-1 should return 19");
			}
		}

		[Test]
		public async Task Test_Can_Resolve_Key_Selector_Outside_Boundaries()
		{
			// test various corner cases:

			// - k < first_key or k <= <00> resolves to:
			//   - '' always

			// - k > last_key or k >= <FF> resolve to:
			//	 - '<FF>' when access to system keys is off
			//   - '<FF>/backupRange' (usually) when access to system keys is ON

			// - k >= <FF><00> resolves to:
			//   - key_outside_legal_range when access to system keys is off
			//   - '<FF>/backupRange' (usually) when access to system keys is ON

			// - k >= <FF><FF> resolved to:
			//   - key_outside_legal_range when access to system keys is off
			//   - '<FF><FF>' when access to system keys is ON

			Slice key;

			// note: we can't have any prefix on the keys, so open the test database in read-only mode
			using var db = await OpenTestDatabaseAsync(readOnly: true);

			using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
			{
				// before <00>
				key = await tr.GetKeyAsync(KeySelector.LastLessThan(FdbKey.MinValue));
				Assert.That(key, Is.EqualTo(Slice.Empty), "lLT(<00>) => ''");

				// before the first key in the db
				var minKey = await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(FdbKey.MinValue));
				Assert.That(minKey, Is.Not.Null);
				key = await tr.GetKeyAsync(KeySelector.LastLessThan(minKey));
				Assert.That(key, Is.EqualTo(Slice.Empty), "lLT(min_key) => ''");

				// after the last key in the db

				var maxKey = await tr.GetKeyAsync(KeySelector.LastLessThan(FdbKey.MaxValue));
				Assert.That(maxKey, Is.Not.Null);
				key = await tr.GetKeyAsync(KeySelector.FirstGreaterThan(maxKey));
				Assert.That(key, Is.EqualTo(FdbKey.MaxValue), "fGT(maxKey) => <FF>");

				// after <FF>
				key = await tr.GetKeyAsync(KeySelector.FirstGreaterThan(FdbKey.MaxValue));
				Assert.That(key, Is.EqualTo(FdbKey.MaxValue), "fGT(<FF>) => <FF>");
				Assert.That(async () => await tr.GetKeyAsync(KeySelector.FirstGreaterThan(FdbKey.MaxValue + FdbKey.MaxValue)), Throws.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.KeyOutsideLegalRange));
				Assert.That(async () => await tr.GetKeyAsync(KeySelector.LastLessThan(Fdb.System.MinValue)), Throws.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.KeyOutsideLegalRange));

				tr.Options.WithReadAccessToSystemKeys();

				var firstSystemKey = await tr.GetKeyAsync(KeySelector.FirstGreaterThan(FdbKey.MaxValue));
				// usually the first key in the system space is <FF>/backupDataFormat, but that may change in the future version.
				Assert.That(firstSystemKey, Is.Not.Null);
				Assert.That(firstSystemKey, Is.GreaterThan(FdbKey.MaxValue), "key should be between <FF> and <FF><FF>");
				Assert.That(firstSystemKey, Is.LessThan(Fdb.System.MaxValue), "key should be between <FF> and <FF><FF>");

				// with access to system keys, the maximum possible key becomes <FF><FF>
				key = await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(Fdb.System.MaxValue));
				Assert.That(key, Is.EqualTo(Fdb.System.MaxValue), "fGE(<FF><FF>) => <FF><FF> (with access to system keys)");
				key = await tr.GetKeyAsync(KeySelector.FirstGreaterThan(Fdb.System.MaxValue));
				Assert.That(key, Is.EqualTo(Fdb.System.MaxValue), "fGT(<FF><FF>) => <FF><FF> (with access to system keys)");

				key = await tr.GetKeyAsync(KeySelector.LastLessThan(Fdb.System.MinValue));
				Assert.That(key, Is.EqualTo(maxKey), "lLT(<FF><00>) => max_key (with access to system keys)");
				key = await tr.GetKeyAsync(KeySelector.FirstGreaterThan(maxKey));
				Assert.That(key, Is.EqualTo(firstSystemKey), "fGT(max_key) => first_system_key (with access to system keys)");
			}
		}

		[Test]
		public async Task Test_Get_Multiple_Values()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			var ids = new[] { 8, 7, 2, 9, 5, 0, 3, 4, 6, 1 };

			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				for (int i = 0; i < ids.Length; i++)
				{
					tr.Set(subspace.Key(i), Text($"Value of #{i}"));
				}

				await tr.CommitAsync();
			}

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				var results = await tr.GetValuesAsync(ids.Select(id => subspace.Key(id).ToSlice()).ToArray());

				Assert.That(results, Is.Not.Null);
				Assert.That(results.Length, Is.EqualTo(ids.Length));

				Log(string.Join(", ", results));

				for (int i = 0; i < ids.Length; i++)
				{
					Assert.That(results[i].ToString(), Is.EqualTo($"Value of #{ids[i]}"));
				}
			}
		}

		[Test]
		public async Task Test_Get_Multiple_Values_From_Encoded_Keys()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			var ids = new[] { 8, 7, 2, 9, 5, 0, 3, 4, 6, 1 };

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				for (int i = 0; i < ids.Length; i++)
				{
					tr.Set(subspace.Key("hello", i, "world"), Text($"Value for #{i}"));
				}

				await tr.CommitAsync();
			}

			// array of keys
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				var results = await tr.GetValuesAsync(ids.Select(id => subspace.Key("hello", id, "world")).ToArray());

				Assert.That(results, Is.Not.Null);
				Assert.That(results.Length, Is.EqualTo(ids.Length));

				Log(string.Join(", ", results));

				for (int i = 0; i < ids.Length; i++)
				{
					Assert.That(results[i].ToString(), Is.EqualTo($"Value for #{ids[i]}"));
				}
			}

			// List of keys
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				var results = await tr.GetValuesAsync(ids.Select(id => subspace.Key("hello", id, "world")).ToList());

				Assert.That(results, Is.Not.Null);
				Assert.That(results.Length, Is.EqualTo(ids.Length));

				Log(string.Join(", ", results));

				for (int i = 0; i < ids.Length; i++)
				{
					Assert.That(results[i].ToString(), Is.EqualTo($"Value for #{ids[i]}"));
				}
			}

			// Sequence of keys
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				var results = await tr.GetValuesAsync(ids.Select(id => subspace.Key("hello", id, "world")));

				Assert.That(results, Is.Not.Null);
				Assert.That(results.Length, Is.EqualTo(ids.Length));

				Log(string.Join(", ", results));

				for (int i = 0; i < ids.Length; i++)
				{
					Assert.That(results[i].ToString(), Is.EqualTo($"Value for #{ids[i]}"));
				}
			}

			// array of items + key selector
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				var results = await tr.GetValuesAsync(ids, id => subspace.Key("hello", id, "world"));

				Assert.That(results, Is.Not.Null);
				Assert.That(results.Length, Is.EqualTo(ids.Length));

				Log(string.Join(", ", results));

				for (int i = 0; i < ids.Length; i++)
				{
					Assert.That(results[i].ToString(), Is.EqualTo($"Value for #{ids[i]}"));
				}
			}

			// sequence of items + key selector
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				var results = await tr.GetValuesAsync(ids.Select(x => x), id => subspace.Key("hello", id, "world"));

				Assert.That(results, Is.Not.Null);
				Assert.That(results.Length, Is.EqualTo(ids.Length));

				Log(string.Join(", ", results));

				for (int i = 0; i < ids.Length; i++)
				{
					Assert.That(results[i].ToString(), Is.EqualTo($"Value for #{ids[i]}"));
				}
			}

		}

		[Test]
		public async Task Test_Get_Multiple_Values_Decoded()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			int[] ids = [ 8, 7, 2, 9, 5, 0, 3, 4, 6, 1 ];

			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				for (int i = 0; i < ids.Length; i++)
				{
					tr.Set(subspace.Key(i), Pack(("Hello", i, "World")));
				}

				await tr.CommitAsync();
			}

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			// overload with TState
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				var results = new int[ids.Length];

				await tr.GetValuesAsync(
					ids,
					subspace, static (s, id) => s.Key(id),
					results.AsMemory(),
					7, (state, value, found) => found ? TuPack.DecodeKeyAt<int>(value, 1) * state : -1
				);
				
				Log(string.Join(", ", results));

				for (int i = 0; i < ids.Length; i++)
				{
					Assert.That(results[i], Is.EqualTo(ids[i] * 7));
				}
			}

			// overload without TState
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				var results = new int[ids.Length];

				await tr.GetValuesAsync(
					ids,
					subspace, static (s, id) => s.Key(id),
					results.AsMemory(),
					valueDecoder: (value, found) => found ? TuPack.DecodeKeyAt<int>(value, 1) * 2 : -1
				);
				
				Log(string.Join(", ", results));

				for (int i = 0; i < ids.Length; i++)
				{
					Assert.That(results[i], Is.EqualTo(ids[i] * 2));
				}
			}

			// overload with encoded keys and TState
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				var results = new int[ids.Length];

				await tr.GetValuesAsync(
					ids,
					subspace, static (s, id) => s.Key(id),
					results.AsMemory(),
					9, (state, value, found) => found ? TuPack.DecodeKeyAt<int>(value, 1) * state : -1
				);
				
				Log(string.Join(", ", results));

				for (int i = 0; i < ids.Length; i++)
				{
					Assert.That(results[i], Is.EqualTo(ids[i] * 9));
				}
			}

			// overload with encoded keys and without TState
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				var results = new int[ids.Length];

				await tr.GetValuesAsync(
					ids, subspace, static (s, id) => s.Key(id),
					results.AsMemory(),
					valueDecoder: (value, found) => found ? TuPack.DecodeKeyAt<int>(value, 1) * 3 : -1
				);
				
				Log(string.Join(", ", results));

				for (int i = 0; i < ids.Length; i++)
				{
					Assert.That(results[i], Is.EqualTo(ids[i] * 3));
				}
			}
		}

		[Test]
		public async Task Test_Get_Multiple_Keys()
		{
			const int N = 20;

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			#region Insert a bunch of keys ...

			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				// keys
				// - (test,) + \0
				// - (test, 0) .. (test, N-1)
				// - (test,) + \xFF
				tr.Set(subspace.First(), Text("min"));
				for (int i = 0; i < 20; i++)
				{
					tr.Set(subspace.Key(i), Text(i.ToString()));
				}

				tr.Set(subspace.Last(), Text("max"));
				await tr.CommitAsync();
			}

			#endregion

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				var selectors = Enumerable
					.Range(0, N)
					.Select((i) => subspace.Key(i).FirstGreaterOrEqual())
					.ToArray();

				// GetKeysAsync([])
				var results = await tr.GetKeysAsync(selectors);
				Assert.That(results, Is.Not.Null);
				Assert.That(results.Length, Is.EqualTo(20));
				for (int i = 0; i < N; i++)
				{
					Assert.That(results[i], Is.EqualTo(subspace.Key(i)));
				}

				// GetKeysAsync(cast to enumerable)
				var results2 = await tr.GetKeysAsync((IEnumerable<FdbKeySelector<FdbTupleKey<int>>>) selectors);
				Assert.That(results2, Is.EqualTo(results));

				// GetKeysAsync(real enumerable)
				var results3 = await tr.GetKeysAsync(selectors.Select(x => x));
				Assert.That(results3, Is.EqualTo(results));
			}
		}

		[Test]
		public async Task Test_Can_Check_Value()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			// write a bunch of keys
			await db.WriteAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("hello"), Text("World!"));
				tr.Set(subspace.Key("foo"), Slice.Empty);
			}, this.Cancellation);

			async Task Check(IFdbReadOnlyTransaction tr, Slice key, Slice expected, FdbValueCheckResult result, Slice actual)
			{
				Log($"Check {key} == {expected} ?");
				var res = await tr.CheckValueAsync(key, expected);
				Log($"> [{res.Result}], {res.Actual:V}");
				Assert.That(res.Actual, Is.EqualTo(actual), $"Check({key} == {expected}) => ({result}, {actual}).Actual was {res.Actual}");
				Assert.That(res.Result, Is.EqualTo(result), $"Check({key} == {expected}) => ({result}, {actual}).Result was {res.Result}");
			}

			// hello should only be equal to 'World!', not any other value, empty or nil
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				// hello should only be equal to 'World!', not any other value, empty or nil
				await Check(tr, subspace.Key("hello").ToSlice(), Text("World!"), FdbValueCheckResult.Success, Text("World!"));
				await Check(tr, subspace.Key("hello").ToSlice(), Text("Le Monde!"), FdbValueCheckResult.Failed, Text("World!"));
				await Check(tr, subspace.Key("hello").ToSlice(), Slice.Nil, FdbValueCheckResult.Failed, Text("World!"));
				await Check(tr, subspace.Key("hello").ToSlice(), subspace.Key("hello").ToSlice(), FdbValueCheckResult.Failed, Text("World!"));
			}

			// foo should only be equal to Empty, *not* Nil or any other value
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				await Check(tr, subspace.Key("foo").ToSlice(), Slice.Empty, FdbValueCheckResult.Success, Slice.Empty);
				await Check(tr, subspace.Key("foo").ToSlice(), Text("bar"), FdbValueCheckResult.Failed, Slice.Empty);
				await Check(tr, subspace.Key("foo").ToSlice(), Slice.Nil, FdbValueCheckResult.Failed, Slice.Empty);
				await Check(tr, subspace.Key("foo").ToSlice(), subspace.Key("foo").ToSlice(), FdbValueCheckResult.Failed, Slice.Empty);
			}

			// not_found should only be equal to Nil, *not* Empty or any other value
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				await Check(tr, subspace.Key("not_found").ToSlice(), Slice.Nil, FdbValueCheckResult.Success, Slice.Nil);
				await Check(tr, subspace.Key("not_found").ToSlice(), Slice.Empty, FdbValueCheckResult.Failed, Slice.Nil);
				await Check(tr, subspace.Key("not_found").ToSlice(), subspace.Key("not_found").ToSlice(), FdbValueCheckResult.Failed, Slice.Nil);
			}

			// checking, changing and checking again: 2nd check should see the modified value!
			// not_found should only be equal to Nil, *not* Empty or any other value
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				await Check(tr, subspace.Key("hello").ToSlice(), Text("World!"), FdbValueCheckResult.Success, Text("World!"));
				await Check(tr, subspace.Key("not_found").ToSlice(), Slice.Nil, FdbValueCheckResult.Success, Slice.Nil);

				tr.Set(subspace.Key("hello"), Text("Le Monde!"));
				await Check(tr, subspace.Key("hello").ToSlice(), Text("Le Monde!"), FdbValueCheckResult.Success, Text("Le Monde!"));
				await Check(tr, subspace.Key("hello").ToSlice(), Text("World!"), FdbValueCheckResult.Failed, Text("Le Monde!"));

				tr.Set(subspace.Key("not_found"), Text("Surprise!"));
				await Check(tr, subspace.Key("not_found").ToSlice(), Text("Surprise!"), FdbValueCheckResult.Success, Text("Surprise!"));
				await Check(tr, subspace.Key("not_found").ToSlice(), Slice.Nil, FdbValueCheckResult.Failed, Text("Surprise!"));

				//note: don't commit!
			}
		}

		[Test]
		public async Task Test_Can_Get_Converted_Value()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			// populate some data
			Slice rnd = Slice.Random(Random.Shared, 1024);
			long ticks = DateTime.UtcNow.Ticks;

			Task<TResult> Read<TResult>(Func<IFdbReadOnlyTransaction, IKeySubspace, Task<TResult>> handler)
			{
				return db.ReadAsync<TResult>(async tr =>
				{
					var subspace = await db.Root.Resolve(tr);
					return await handler(tr, subspace);
				}, this.Cancellation);
			}

			await db.WriteAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);

				tr.Set(subspace.Key("hello"), Text("World!"));
				tr.Set(subspace.Key("timestamp"), Slice.FromInt64(ticks));
				tr.Set(subspace.Key("blob"), rnd);
				tr.Set(subspace.Key("json"), """{ "hello": "world", "foo": "bar", "level": 9001 }"""u8);

			}, this.Cancellation);

			// read back

			{
				var res = await Read((tr, subspace) =>
					tr.GetAsync(
						subspace.Key("hello"),
						(buffer, exists) => exists ? buffer.ToStringUtf8() : "<not_found>"
					)
				);
				Dump(res);
			}
			{
				var res = await Read((tr, subspace) =>
					tr.GetAsync(
						subspace.Key("hello"), "some_state",
						(state, buffer, exists) => exists ? state + ":" + buffer.ToStringUtf8() : "<not_found>"
					)
				);
				Dump(res);
			}
			{
				var res = await Read((tr, subspace) =>
					tr.GetAsync(
						subspace.Key("json"),
						(buffer, exists) => exists ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(buffer) : null
					)
				);
				Dump(res);
			}
		}

		/// <summary>Performs (x OP y) and ensure that the result is correct</summary>
		private async Task PerformAtomicOperationAndCheck(IFdbDatabase db, Slice key, int x, FdbMutationType type, int y)
		{
			int expected = 0;
			switch(type)
			{
				case FdbMutationType.BitAnd: expected = x & y; break;
				case FdbMutationType.BitOr: expected = x | y; break;
				case FdbMutationType.BitXor: expected = x ^ y; break;
				case FdbMutationType.Add: expected = x + y; break;
				case FdbMutationType.Max: expected = Math.Max(x, y); break;
				case FdbMutationType.Min: expected = Math.Min(x, y); break;
				default: Assert.Fail("Invalid operation type"); break;
			}

			// set key = x
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				tr.Set(key, Slice.FromFixed32(x));
				await tr.CommitAsync();
			}

			// atomic key op y
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				tr.Atomic(key, Slice.FromFixed32(y), type);
				await tr.CommitAsync();
			}

			// read key
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var data = await tr.GetAsync(key);
				Assert.That(data.Count, Is.EqualTo(4), "data.Count");

				Assert.That(data.ToInt32(), Is.EqualTo(expected), $"0x{x:X8} {type} 0x{y:X8} = 0x{expected:X8}");
			}
		}

		[Test]
		public async Task Test_ClearRange_Empty_Range_Is_A_NoOp()
		{
			// a degenerate range [k, k) is empty: the cluster accepts the clear and changes nothing
			using var db = await OpenTestPartitionAsync();
			var location = db.Root;
			await CleanLocation(db, location);

			await db.WriteAsync(async tr =>
			{
				var subspace = await location.Resolve(tr);
				tr.Set(subspace.Key("a"), Text("A"));
				tr.Set(subspace.Key("b"), Text("B"));
			}, this.Cancellation);

			await db.WriteAsync(async tr =>
			{
				var subspace = await location.Resolve(tr);
				tr.ClearRange(subspace.Key("a"), subspace.Key("a"));
				tr.ClearRange(subspace.Key("zzz"), subspace.Key("zzz")); // empty range over no data at all
			}, this.Cancellation);

			await db.ReadAsync(async tr =>
			{
				var subspace = await location.Resolve(tr);
				Assert.That((await tr.GetAsync(subspace.Key("a"))).ToStringUtf8(), Is.EqualTo("A"), "the key at the degenerate range's boundary must survive");
				Assert.That((await tr.GetAsync(subspace.Key("b"))).ToStringUtf8(), Is.EqualTo("B"));
				return 0;
			}, this.Cancellation);
		}

		[Test]
		public async Task Test_Can_Perform_Atomic_Operations()
		{
			// test that we can perform atomic mutations on keys

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			//note: we take a risk by reading the key separately, but this simplifies the rest of the code !
			Task<Slice> ResolveKey(string name) => db.ReadAsync(async tr => (await db.Root.Resolve(tr)).Key(name).ToSlice(), this.Cancellation);

			Slice key;

			key = await ResolveKey("add");
			await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Add, 0);
			await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Add, 1);
			await PerformAtomicOperationAndCheck(db, key, 1, FdbMutationType.Add, 0);
			await PerformAtomicOperationAndCheck(db, key, -2, FdbMutationType.Add, 1);
			await PerformAtomicOperationAndCheck(db, key, -1, FdbMutationType.Add, 1);
			await PerformAtomicOperationAndCheck(db, key, 123456789, FdbMutationType.Add, 987654321);

			key = await ResolveKey("and");
			await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.BitAnd, 0);
			await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.BitAnd, 0x018055AA);
			await PerformAtomicOperationAndCheck(db, key, -1, FdbMutationType.BitAnd, 0x018055AA);
			await PerformAtomicOperationAndCheck(db, key, 0x00FF00FF, FdbMutationType.BitAnd, 0x018055AA);
			await PerformAtomicOperationAndCheck(db, key, 0x0F0F0F0F, FdbMutationType.BitAnd, 0x018055AA);

			key = await ResolveKey("or");
			await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.BitOr, 0);
			await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.BitOr, 0x018055AA);
			await PerformAtomicOperationAndCheck(db, key, -1, FdbMutationType.BitOr, 0x018055AA);
			await PerformAtomicOperationAndCheck(db, key, 0x00FF00FF, FdbMutationType.BitOr, 0x018055AA);
			await PerformAtomicOperationAndCheck(db, key, 0x0F0F0F0F, FdbMutationType.BitOr, 0x018055AA);

			key = await ResolveKey("xor");
			await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.BitXor, 0);
			await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.BitXor, 0x018055AA);
			await PerformAtomicOperationAndCheck(db, key, -1, FdbMutationType.BitXor, 0x018055AA);
			await PerformAtomicOperationAndCheck(db, key, 0x00FF00FF, FdbMutationType.BitXor, 0x018055AA);
			await PerformAtomicOperationAndCheck(db, key, 0x0F0F0F0F, FdbMutationType.BitXor, 0x018055AA);

			// note: gate on the DATABASE's selected api level, not the process-global Fdb.ApiVersion (which is 0 on
			// the emulator head, where the native client never starts), the client-side mutation gate consults the
			// database level, so both backends behave the same at the same level
			if (db.GetApiVersion() >= 300)
			{
				key = await ResolveKey("max");
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Max, 0);
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Max, 1);
				await PerformAtomicOperationAndCheck(db, key, 1, FdbMutationType.Max, 0);
				await PerformAtomicOperationAndCheck(db, key, 2, FdbMutationType.Max, 1);
				await PerformAtomicOperationAndCheck(db, key, 123456789, FdbMutationType.Max, 987654321);

				key = await ResolveKey("min");
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Min, 0);
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Min, 1);
				await PerformAtomicOperationAndCheck(db, key, 1, FdbMutationType.Min, 0);
				await PerformAtomicOperationAndCheck(db, key, 2, FdbMutationType.Min, 1);
				await PerformAtomicOperationAndCheck(db, key, 123456789, FdbMutationType.Min, 987654321);
			}
			else
			{
				// calling with an unsupported mutation type should fail
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					key = await ResolveKey("invalid");
					Assert.That(() => tr.Atomic(key, Slice.FromFixed32(42), FdbMutationType.Max), Throws.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.InvalidMutationType));
				}
			}

			// calling with an invalid mutation type should fail
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				key = await ResolveKey("invalid");
				Assert.That(() => tr.Atomic(key, Slice.FromFixed32(42), (FdbMutationType) 42), Throws.InstanceOf<NotSupportedException>());
			}
		}

		[Test]
		public async Task Test_Can_AtomicAdd32()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			// setup
			await db.WriteAsync(async (tr) =>
			{
				Log("resolving...");
				var subspace = await db.Root.Resolve(tr);
				Log(subspace);
				tr.Set(subspace.Key("AAA"), FdbValue.ToFixed32LittleEndian(0));
				tr.Set(subspace.Key("BBB"), FdbValue.ToFixed32LittleEndian(1));
				tr.Set(subspace.Key("CCC"), FdbValue.ToFixed32LittleEndian(43));
				tr.Set(subspace.Key("DDD"), FdbValue.ToFixed32LittleEndian(255));
				//EEE does not exist
				tr.Set(subspace.Key("FFF"), FdbValue.ToFixed32LittleEndian(0x5A5A5A5A));
				tr.Set(subspace.Key("GGG"), FdbValue.ToFixed32LittleEndian(-1));
				//HHH does not exist
			}, this.Cancellation);

			// execute
			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.AtomicAdd32(subspace.Key("AAA"), 1);
				tr.AtomicAdd32(subspace.Key("BBB"), 42);
				tr.AtomicAdd32(subspace.Key("CCC"), -1);
				tr.AtomicAdd32(subspace.Key("DDD"), 42);
				tr.AtomicAdd32(subspace.Key("EEE"), 42);
				tr.AtomicAdd32(subspace.Key("FFF"), 0xA5A5A5A5);
				tr.AtomicAdd32(subspace.Key("GGG"), 1);
				tr.AtomicAdd32(subspace.Key("HHH"), uint.MaxValue);
			}, this.Cancellation);

			// check
			await db.ReadAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				Assert.That((await tr.GetValueInt32Async(subspace.Key("AAA"))), Is.EqualTo(1));
				Assert.That((await tr.GetValueInt32Async(subspace.Key("BBB"))), Is.EqualTo(43));
				Assert.That((await tr.GetValueInt32Async(subspace.Key("CCC"))), Is.EqualTo(42));
				Assert.That((await tr.GetValueInt32Async(subspace.Key("DDD"))), Is.EqualTo(297));
				Assert.That((await tr.GetValueInt32Async(subspace.Key("EEE"))), Is.EqualTo(42));
				Assert.That((await tr.GetValueInt32Async(subspace.Key("FFF"))), Is.EqualTo(-1));
				Assert.That((await tr.GetValueInt32Async(subspace.Key("GGG"))), Is.EqualTo(0));
				Assert.That((await tr.GetValueInt32Async(subspace.Key("HHH"))), Is.EqualTo(-1));
			}, this.Cancellation);
		}

		[Test]
		public async Task Test_Can_AtomicIncrement32()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			// setup
			await db.WriteAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("AAA"), FdbValue.ToFixed32LittleEndian(0));
				tr.Set(subspace.Key("BBB"), FdbValue.ToFixed32LittleEndian(1));
				tr.Set(subspace.Key("CCC"), FdbValue.ToFixed32LittleEndian(42));
				tr.Set(subspace.Key("DDD"), FdbValue.ToFixed32LittleEndian(255));
				// EEE does not exist
				tr.Set(subspace.Key("FFF"), FdbValue.ToFixed32LittleEndian(-1));
			}, this.Cancellation);

			// execute
			await db.WriteAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.AtomicIncrement32(subspace.Key("AAA"));
				tr.AtomicIncrement32(subspace.Key("BBB"));
				tr.AtomicIncrement32(subspace.Key("CCC"));
				tr.AtomicIncrement32(subspace.Key("DDD"));
				tr.AtomicIncrement32(subspace.Key("EEE"));
				tr.AtomicIncrement32(subspace.Key("FFF"));
			}, this.Cancellation);

			// check
			await db.ReadAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				Assert.That((await tr.GetValueInt32Async(subspace.Key("AAA"))), Is.EqualTo(1));
				Assert.That((await tr.GetValueInt32Async(subspace.Key("BBB"))), Is.EqualTo(2));
				Assert.That((await tr.GetValueInt32Async(subspace.Key("CCC"))), Is.EqualTo(43));
				Assert.That((await tr.GetValueInt32Async(subspace.Key("DDD"))), Is.EqualTo(256));
				Assert.That((await tr.GetValueInt32Async(subspace.Key("EEE"))), Is.EqualTo(1));
				Assert.That((await tr.GetValueInt32Async(subspace.Key("FFF"))), Is.EqualTo(0));
			}, this.Cancellation);
		}

		[Test]
		public async Task Test_Can_AtomicAdd64()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			// setup
			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("AAA"), FdbValue.Zero64);
				tr.Set(subspace.Key("BBB"), FdbValue.ToFixed64LittleEndian(1));
				tr.Set(subspace.Key("CCC"), FdbValue.ToFixed64LittleEndian(43));
				tr.Set(subspace.Key("DDD"), FdbValue.ToFixed64LittleEndian(255));
				//EEE does not exist
				tr.Set(subspace.Key("FFF"), FdbValue.ToFixed64LittleEndian(0x5A5A5A5A5A5A5A5A));
				tr.Set(subspace.Key("GGG"), FdbValue.ToFixed64LittleEndian(-1));
				//HHH does not exist
			}, this.Cancellation);

			// execute
			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.AtomicAdd64(subspace.Key("AAA"), 1);
				tr.AtomicAdd64(subspace.Key("BBB"), 42);
				tr.AtomicAdd64(subspace.Key("CCC"), -1);
				tr.AtomicAdd64(subspace.Key("DDD"), 42);
				tr.AtomicAdd64(subspace.Key("EEE"), 42);
				tr.AtomicAdd64(subspace.Key("FFF"), 0xA5A5A5A5A5A5A5A5);
				tr.AtomicAdd64(subspace.Key("GGG"), 1);
				tr.AtomicAdd64(subspace.Key("HHH"), ulong.MaxValue);
			}, this.Cancellation);

			// check
			await db.ReadAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				Assert.That((await tr.GetValueInt64Async(subspace.Key("AAA"))), Is.EqualTo(1));
				Assert.That((await tr.GetValueInt64Async(subspace.Key("BBB"))), Is.EqualTo(43));
				Assert.That((await tr.GetValueInt64Async(subspace.Key("CCC"))), Is.EqualTo(42));
				Assert.That((await tr.GetValueInt64Async(subspace.Key("DDD"))), Is.EqualTo(297));
				Assert.That((await tr.GetValueInt64Async(subspace.Key("EEE"))), Is.EqualTo(42));
				Assert.That((await tr.GetValueInt64Async(subspace.Key("FFF"))), Is.EqualTo(-1));
				Assert.That((await tr.GetValueInt64Async(subspace.Key("GGG"))), Is.EqualTo(0));
				Assert.That((await tr.GetValueInt64Async(subspace.Key("HHH"))), Is.EqualTo(-1));
			}, this.Cancellation);
		}

		[Test]
		public async Task Test_Can_AtomicIncrement64()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			// setup
			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("AAA"), FdbValue.ToFixed64LittleEndian(0));
				tr.Set(subspace.Key("BBB"), FdbValue.ToFixed64LittleEndian(1));
				tr.Set(subspace.Key("CCC"), FdbValue.ToFixed64LittleEndian(42));
				tr.Set(subspace.Key("DDD"), FdbValue.ToFixed64LittleEndian(255));
				//EEE does not exist
				tr.Set(subspace.Key("FFF"), FdbValue.ToFixed64LittleEndian(-1));
			}, this.Cancellation);

			// execute
			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.AtomicIncrement64(subspace.Key("AAA"));
				tr.AtomicIncrement64(subspace.Key("BBB"));
				tr.AtomicIncrement64(subspace.Key("CCC"));
				tr.AtomicIncrement64(subspace.Key("DDD"));
				tr.AtomicIncrement64(subspace.Key("EEE"));
				tr.AtomicIncrement64(subspace.Key("FFF"));
			}, this.Cancellation);

			// check
			await db.ReadAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				Assert.That((await tr.GetValueInt64Async(subspace.Key("AAA"))), Is.EqualTo(1));
				Assert.That((await tr.GetValueInt64Async(subspace.Key("BBB"))), Is.EqualTo(2));
				Assert.That((await tr.GetValueInt64Async(subspace.Key("CCC"))), Is.EqualTo(43));
				Assert.That((await tr.GetValueInt64Async(subspace.Key("DDD"))), Is.EqualTo(256));
				Assert.That((await tr.GetValueInt64Async(subspace.Key("EEE"))), Is.EqualTo(1));
				Assert.That((await tr.GetValueInt64Async(subspace.Key("FFF"))), Is.EqualTo(0));
			}, this.Cancellation);
		}

		[Test]
		public async Task Test_Can_AtomicCompareAndClear()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			// setup
			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("AAA"), FdbValue.ToFixed32LittleEndian(0));
				tr.Set(subspace.Key("BBB"), FdbValue.ToFixed32LittleEndian(1));
				tr.Set(subspace.Key("CCC"), FdbValue.ToFixed32LittleEndian(42));
				tr.Set(subspace.Key("DDD"), FdbValue.ToFixed64LittleEndian(0));
				tr.Set(subspace.Key("EEE"), FdbValue.ToFixed64LittleEndian(1));
				//FFF does not exist
			}, this.Cancellation);

			// execute
			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.AtomicCompareAndClear(subspace.Key("AAA"), Slice.FromFixed32(0));  // should be cleared
				tr.AtomicCompareAndClear(subspace.Key("BBB"), Slice.FromFixed32(0));  // should not be touched
				tr.AtomicCompareAndClear(subspace.Key("CCC"), Slice.FromFixed32(42)); // should be cleared
				tr.AtomicCompareAndClear(subspace.Key("DDD"), Slice.FromFixed64(0));  // should be cleared
				tr.AtomicCompareAndClear(subspace.Key("EEE"), Slice.FromFixed64(0));  // should not be touched
				tr.AtomicCompareAndClear(subspace.Key("FFF"), Slice.FromFixed64(42)); // should not be created
			}, this.Cancellation);

			// check
			await db.ReadAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				Assert.That((await tr.GetValueInt32Async(subspace.Key("AAA"))), Is.Null);
				Assert.That((await tr.GetValueInt32Async(subspace.Key("BBB"))), Is.EqualTo(1));
				Assert.That((await tr.GetValueInt32Async(subspace.Key("CCC"))), Is.Null);
				Assert.That((await tr.GetValueInt64Async(subspace.Key("DDD"))), Is.Null);
				Assert.That((await tr.GetValueInt64Async(subspace.Key("EEE"))), Is.EqualTo(1));
				Assert.That((await tr.GetValueInt64Async(subspace.Key("FFF"))), Is.Null);
			}, this.Cancellation);
		}

		[Test]
		public async Task Test_Can_AppendIfFits()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			// setup
			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("AAA"), Slice.Empty);
				tr.Set(subspace.Key("BBB"), Slice.Repeat('B', 10));
				tr.Set(subspace.Key("CCC"), Slice.Repeat('C', 90_000));
				tr.Set(subspace.Key("DDD"), Slice.Repeat('D', 100_000));
				//EEE does not exist
			}, this.Cancellation);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			// execute
			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.AtomicAppendIfFits(subspace.Key("AAA"), Text("Hello, World!"));
				tr.AtomicAppendIfFits(subspace.Key("BBB"), Text("Hello"));
				tr.AtomicAppendIfFits(subspace.Key("BBB"), Text(", World!"));
				tr.AtomicAppendIfFits(subspace.Key("CCC"), Slice.Repeat('c', 10_000)); // should just fit exactly!
				tr.AtomicAppendIfFits(subspace.Key("DDD"), Text("!")); // should not fit!
				tr.AtomicAppendIfFits(subspace.Key("EEE"), Text("Hello, World!"));
			}, this.Cancellation);

			// check
			await db.ReadAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				Assert.That((await tr.GetAsync(subspace.Key("AAA"))).ToString(), Is.EqualTo("Hello, World!"));
				Assert.That((await tr.GetAsync(subspace.Key("BBB"))).ToString(), Is.EqualTo("BBBBBBBBBBHello, World!"));
				Assert.That((await tr.GetAsync(subspace.Key("CCC"))), Is.EqualTo(Slice.Repeat('C', 90_000) + Slice.Repeat('c', 10_000)));
				Assert.That((await tr.GetAsync(subspace.Key("DDD"))), Is.EqualTo(Slice.Repeat('D', 100_000)));
				Assert.That((await tr.GetAsync(subspace.Key("EEE"))).ToString(), Is.EqualTo("Hello, World!"));
			}, this.Cancellation);
		}

		[Test]
		public async Task Test_Can_Snapshot_Read()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			// write a bunch of keys
			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("hello"), Text("World!"));
				tr.Set(subspace.Key("foo"), Text("bar"));
			}, this.Cancellation);

			// read them using snapshot
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				Slice bytes;

				bytes = await tr.Snapshot.GetAsync(subspace.Key("hello"));
				Assert.That(bytes.ToUnicode(), Is.EqualTo("World!"));

				bytes = await tr.Snapshot.GetAsync(subspace.Key("foo"));
				Assert.That(bytes.ToUnicode(), Is.EqualTo("bar"));
			}
		}

		[Test]
		public async Task Test_CommittedVersion_On_ReadOnly_Transactions()
		{
			//note: until CommitAsync() is called, the value of the committed version is unspecified, but current implementation returns -1

			using var db = await OpenTestDatabaseAsync();
			using var tr = db.BeginTransaction(this.Cancellation);

			long ver = tr.GetCommittedVersion();
			Assert.That(ver, Is.EqualTo(-1), "Initial committed version");

			var subspace = await db.Root.Resolve(tr);
			_ = await tr.GetAsync(subspace.Key("foo"));

			// until the transaction commits, the committed version will stay -1
			ver = tr.GetCommittedVersion();
			Assert.That(ver, Is.EqualTo(-1), "Committed version after a single read");

			// committing a read only transaction

			await tr.CommitAsync();

			ver = tr.GetCommittedVersion();
			Assert.That(ver, Is.EqualTo(-1), "Read-only committed transaction have a committed version of -1");
		}

		[Test]
		public async Task Test_CommittedVersion_On_Write_Transactions()
		{
			//note: until CommitAsync() is called, the value of the committed version is unspecified, but current implementation returns -1

			using var db = await OpenTestDatabaseAsync();
			using var tr = db.BeginTransaction(this.Cancellation);

			// take the read version (to compare with the committed version below)
			long readVersion = await tr.GetReadVersionAsync();

			long ver = tr.GetCommittedVersion();
			Assert.That(ver, Is.EqualTo(-1), "Initial committed version");

			var subspace = await db.Root.Resolve(tr);
			tr.Set(subspace.Key("foo"), Text("bar"));

			// until the transaction commits, the committed version should still be -1
			ver = tr.GetCommittedVersion();
			Assert.That(ver, Is.EqualTo(-1), "Committed version after a single write");

			// committing a read only transaction

			await tr.CommitAsync();

			ver = tr.GetCommittedVersion();
			Assert.That(ver, Is.GreaterThanOrEqualTo(readVersion), "Committed version of write transaction should be >= the read version");
		}

		[Test]
		public async Task Test_CommittedVersion_After_Reset()
		{
			//note: until CommitAsync() is called, the value of the committed version is unspecified, but current implementation returns -1

			using var db = await OpenTestDatabaseAsync();
			using var tr = db.BeginTransaction(this.Cancellation);

			// take the read version (to compare with the committed version below)
			long rv1 = await tr.GetReadVersionAsync();

			var subspace = await db.Root.Resolve(tr);

			// do something and commit
			tr.Set(subspace.Key("foo"), Text("bar"));
			await tr.CommitAsync();
			long cv1 = tr.GetCommittedVersion();
			Log($"COMMIT: {rv1} / {cv1}");
			Assert.That(cv1, Is.GreaterThanOrEqualTo(rv1), "Committed version of write transaction should be >= the read version");

			// reset the transaction
			tr.Reset();

			long rv2 = await tr.GetReadVersionAsync();
			long cv2 = tr.GetCommittedVersion();
			Log($"RESET: {rv2} / {cv2}");
			//Note: the current fdb_c client does not revert the committed version to -1 ... ?
			//Assert.That(cv2, Is.EqualTo(-1), "Committed version should go back to -1 after reset");

			// read-only + commit
			await tr.GetAsync(subspace.Key("foo"));
			await tr.CommitAsync();
			cv2 = tr.GetCommittedVersion();
			Log($"COMMIT2: {rv2} / {cv2}");
			Assert.That(cv2, Is.EqualTo(-1), "Committed version of read-only transaction should be -1 even the transaction was previously used to write something");
		}

		[Test]
		public async Task Test_Regular_Read_With_Concurrent_Change_Should_Conflict()
		{
			// see http://community.foundationdb.com/questions/490/snapshot-read-vs-non-snapshot-read/492

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("foo"), Text("foo"));
			}, this.Cancellation);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			using var trA = db.BeginTransaction(this.Cancellation);
			using var trB = db.BeginTransaction(this.Cancellation);

			var subspaceA = await db.Root.Resolve(trA);
			var subspaceB = await db.Root.Resolve(trB);

			// regular read
			_ = await trA.GetAsync(subspaceA.Key("foo"));
			trA.Set(subspaceA.Key("foo"), Text("bar"));

			// this will conflict with our read
			trB.Set(subspaceB.Key("foo"), Text("bar"));
			await trB.CommitAsync();

			// should fail with a "not_comitted" error
			await AssertThrowsFdbErrorAsync(
				() => trA.CommitAsync(),
				FdbError.NotCommitted,
				"Commit should conflict !"
			);
		}

		[Test]
		public async Task Test_Snapshot_Read_With_Concurrent_Change_Should_Not_Conflict()
		{
			// see http://community.foundationdb.com/questions/490/snapshot-read-vs-non-snapshot-read/492

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("foo"), Text("foo"));
			}, this.Cancellation);

			using var trA = db.BeginTransaction(this.Cancellation);
			using var trB = db.BeginTransaction(this.Cancellation);

			var subspaceA = await db.Root.Resolve(trA);
			var subspaceB = await db.Root.Resolve(trB);

			// reading with snapshot mode should not conflict
			_ = await trA.Snapshot.GetAsync(subspaceA.Key("foo"));
			trA.Set(subspaceA.Key("foo"), Text("bar"));

			// this would normally conflict with the previous read if it wasn't a snapshot read
			trB.Set(subspaceB.Key("foo"), Text("bar"));
			await trB.CommitAsync();

			// should succeed
			await trA.CommitAsync();
		}

		[Test]
		public async Task Test_GetRange_With_Concurrent_Change_Should_Conflict()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("foo", 50), Text("fifty"));
			}, this.Cancellation);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			Log("# Limit=1, Forward, Conflict");
			{
				// we will read the first key from [0, 100), expected 50
				// but another transaction will insert 42, in effect changing the result of our range
				// => this should conflict the GetRange

				// setup
				await db.WriteAsync(async (tr) =>
				{
					var subspace = await db.Root.Resolve(tr);
					tr.Set(subspace.Key("foo", 50), Text("fifty"));
				}, this.Cancellation);

				// check
				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr1);

					// [0, 100) limit 1 => 50
					var kvp = await tr1
						.GetRange(subspace.Key("foo"), subspace.Key("foo", 100))
						.FirstOrDefaultAsync();
					Assert.That(kvp.Key, Is.EqualTo(subspace.Key("foo", 50)));

					// 42 < 50 > conflict !!!
					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						var subspace2 = await db.Root.Resolve(tr2);
						tr2.Set(subspace2.Key("foo", 42), Text("forty-two"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(subspace.Key("bar"), Slice.Empty);

					Assert.That(
						async () => await tr1.CommitAsync(),
						Throws.InstanceOf<FdbException>().With.Property(nameof(FdbException.Code)).EqualTo(FdbError.NotCommitted),
						"The Set(42) in TR2 should have conflicted with the GetRange(0, 100) in TR1"
					);
				}
			}

			Log("# Limit=1, Forward, No Conflict");
			{
				// if the other transaction insert something AFTER 50, then the result of our GetRange would not change (because of the implied limit = 1)
				// => this should NOT conflict the GetRange
				// note that if we write something in the range (0, 100) but AFTER 50, it should not conflict because we are doing a limit=1

				// setup
				await db.WriteAsync(async (tr) =>
				{
					var subspace = await db.Root.Resolve(tr);
					tr.ClearRange(subspace);
					tr.Set(subspace.Key("foo", 50), Text("fifty"));
				}, this.Cancellation);

				// check
				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr1);

					// [0, 100) limit 1 => 50
					var kvp = await tr1
						.GetRange(subspace.Key("foo"), subspace.Key("foo", 100))
						.FirstOrDefaultAsync();
					Assert.That(kvp.Key, Is.EqualTo(subspace.Key("foo", 50)));

					// 77 > 50 => no conflict
					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						var subspace2 = await db.Root.Resolve(tr2);
						tr2.Set(subspace2.Key("foo", 77), Text("docm"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(subspace.Key("bar"), Slice.Empty);

					// should not conflict!
					Assert.That(async () => await tr1.CommitAsync(), Throws.Nothing, "Transaction should not conflict because the change does not change the result of the GetRange!");
				}
			}

			Log("# Limit=1, Reverse, Conflict");
			{
				// check that reverse the range does conflict as expected

				// setup
				await db.WriteAsync(async (tr) =>
				{
					var subspace = await db.Root.Resolve(tr);
					tr.ClearRange(subspace);
					tr.Set(subspace.Key("foo", 50), Text("fifty"));
				}, this.Cancellation);

				// check
				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr1);

					// [0, 100) limit 1 => 50
					var kvp = await tr1
						.GetRange(subspace.Key("foo"), subspace.Key("foo", 100))
						.LastOrDefaultAsync();

					Assert.That(kvp.Key, Is.EqualTo(subspace.Key("foo", 50)));

					// 37 < 50 => no conflict
					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						var subspace2 = await db.Root.Resolve(tr2);
						tr2.Set(subspace2.Key("foo", 77), Text("docm"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(subspace.Key("bar"), Slice.Empty);

					// should not conflict!
					Assert.That(
						async () => await tr1.CommitAsync(),
						Throws.InstanceOf<FdbException>().With.Property(nameof(FdbException.Code)).EqualTo(FdbError.NotCommitted),
						"Transaction should conflict because the change does not change the result of the GetRange!"
					);
				}
			}

			Log("# Limit=1, Reverse, No Conflict");
			{
				// same thing but the mutation if before the result range

				// setup
				await db.WriteAsync(async (tr) =>
				{
					var subspace = await db.Root.Resolve(tr);
					tr.ClearRange(subspace);
					tr.Set(subspace.Key("foo", 50), Text("fifty"));
				}, this.Cancellation);

				// check
				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr1);

					// [0, 100) limit 1 => 50
					var kvp = await tr1
						.GetRange(subspace.Key("foo"), subspace.Key("foo", 100))
						.LastOrDefaultAsync();

					Assert.That(kvp.Key, Is.EqualTo(subspace.Key("foo", 50)));

					// 37 < 50 => no conflict
					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						var subspace2 = await db.Root.Resolve(tr2);
						tr2.Set(subspace2.Key("foo", 37), Text("totally_random_number"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(subspace.Key("bar"), Slice.Empty);

					// should not conflict!
					Assert.That(async () => await tr1.CommitAsync(), Throws.Nothing, "Transaction should not conflict because the change does not change the result of the GetRange!");
				}
			}

			Log("# Limit=3, Forward, Conflict");
			{
				// setup
				await db.WriteAsync(async (tr) =>
				{
					var subspace = await db.Root.Resolve(tr);
					tr.ClearRange(subspace);
					tr.Set(subspace.Key("foo", 49), Text("forty nine"));
					tr.Set(subspace.Key("foo", 50), Text("fifty"));
					tr.Set(subspace.Key("foo", 51), Text("fifty one"));
				}, this.Cancellation);

				// check conflict
				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr1);

					// [0, 100) limit 1 => 50
					var kvps = await tr1
						.GetRange(subspace.Key("foo"), subspace.Key("foo", 100))
						.Take(3)
						.ToListAsync();

					Assert.That(kvps.Count, Is.EqualTo(3));
					Assert.That(kvps[0].Key, Is.EqualTo(subspace.Key("foo", 49)));
					Assert.That(kvps[1].Key, Is.EqualTo(subspace.Key("foo", 50)));
					Assert.That(kvps[2].Key, Is.EqualTo(subspace.Key("foo", 51)));

					// 77 > 50 => no conflict
					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						var subspace2 = await db.Root.Resolve(tr2);
						tr2.Set(subspace2.Key("foo", 37), Text("totally_random_number"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(subspace.Key("bar"), Slice.Empty);

					// should not conflict!
					Assert.That(
						async () => await tr1.CommitAsync(), 
						Throws.InstanceOf<FdbException>().With.Property(nameof(FdbException.Code)).EqualTo(FdbError.NotCommitted),
						"Transaction should conflict because the mutation would change the result of the GetRange!"
					);
				}

			}

			Log("# Limit=3, Forward, No Conflict");
			{
				// setup
				await db.WriteAsync(async (tr) =>
				{
					var subspace = await db.Root.Resolve(tr);
					tr.ClearRange(subspace);
					tr.Set(subspace.Key("foo", 49), Text("forty nine"));
					tr.Set(subspace.Key("foo", 50), Text("fifty"));
					tr.Set(subspace.Key("foo", 51), Text("fifty one"));
				}, this.Cancellation);

				// check no conflict
				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr1);

					// [0, 100) limit 1 => 50
					var kvps = await tr1
						.GetRange(subspace.Key("foo"), subspace.Key("foo", 100))
						.Take(3)
						.ToListAsync();

					Assert.That(kvps.Count, Is.EqualTo(3));
					Assert.That(kvps[0].Key, Is.EqualTo(subspace.Key("foo", 49)));
					Assert.That(kvps[1].Key, Is.EqualTo(subspace.Key("foo", 50)));
					Assert.That(kvps[2].Key, Is.EqualTo(subspace.Key("foo", 51)));

					// 77 > 50 => no conflict
					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						var subspace2 = await db.Root.Resolve(tr2);
						tr2.Set(subspace2.Key("foo", 77), Text("docm"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(subspace.Key("bar"), Slice.Empty);

					// should not conflict!
					Assert.That(async () => await tr1.CommitAsync(), Throws.Nothing, "Transaction should not conflict because the mutation does not change the result of the GetRange!");
				}
			}

			Log("# Limit=3, Reverse, Conflict");
			{
				// setup
				await db.WriteAsync(async (tr) =>
				{
					var subspace = await db.Root.Resolve(tr);
					tr.ClearRange(subspace);
					tr.Set(subspace.Key("foo", 49), Text("forty nine"));
					tr.Set(subspace.Key("foo", 50), Text("fifty"));
					tr.Set(subspace.Key("foo", 51), Text("fifty one"));
				}, this.Cancellation);

				// check conflict
				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr1);

					// [0, 100) limit 1 => 50
					var kvps = await tr1
						.GetRange(subspace.Key("foo"), subspace.Key("foo", 100))
						.Reverse()
						.Take(3)
						.ToListAsync();

					Assert.That(kvps.Count, Is.EqualTo(3));
					Assert.That(kvps[0].Key, Is.EqualTo(subspace.Key("foo", 51)));
					Assert.That(kvps[1].Key, Is.EqualTo(subspace.Key("foo", 50)));
					Assert.That(kvps[2].Key, Is.EqualTo(subspace.Key("foo", 49)));

					// 77 > 50 => no conflict
					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						var subspace2 = await db.Root.Resolve(tr2);
						tr2.Set(subspace2.Key("foo", 77), Text("conflict"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(subspace.Key("bar"), Slice.Empty);

					// should not conflict!
					Assert.That(
						async () => await tr1.CommitAsync(),
						Throws.InstanceOf<FdbException>().With.Property(nameof(FdbException.Code)).EqualTo(FdbError.NotCommitted),
						"Transaction should conflict because the mutation would change the result of the GetRange!"
					);
				}

			}

			Log("# Limit=3, Reverse, No Conflict");
			{
				// setup
				await db.WriteAsync(async (tr) =>
				{
					var subspace = await db.Root.Resolve(tr);
					tr.ClearRange(subspace);
					tr.Set(subspace.Key("foo", 49), Text("forty nine"));
					tr.Set(subspace.Key("foo", 50), Text("fifty"));
					tr.Set(subspace.Key("foo", 51), Text("fifty one"));
				}, this.Cancellation);

				// check no conflict
				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr1);

					// [0, 100) limit 1 => 50
					var kvps = await tr1
						.GetRange(subspace.Key("foo"), subspace.Key("foo", 100))
						.Reverse()
						.Take(3)
						.ToListAsync();

					Assert.That(kvps.Count, Is.EqualTo(3));
					Assert.That(kvps[0].Key, Is.EqualTo(subspace.Key("foo", 51)));
					Assert.That(kvps[1].Key, Is.EqualTo(subspace.Key("foo", 50)));
					Assert.That(kvps[2].Key, Is.EqualTo(subspace.Key("foo", 49)));

					// 77 > 50 => no conflict
					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						var subspace2 = await db.Root.Resolve(tr2);
						tr2.Set(subspace2.Key("foo", 37), Text("totally_random_number"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(subspace.Key("bar"), Slice.Empty);

					// should not conflict!
					Assert.That(async () => await tr1.CommitAsync(), Throws.Nothing, "Transaction should not conflict because the mutation does not change the result of the GetRange!");
				}
			}
		}

		[Test]
		public async Task Test_GetKey_With_Concurrent_Change_Should_Conflict()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.ClearRange(subspace);
				tr.Set(subspace.Key("foo", 50), Text("fifty"));
			}, this.Cancellation);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			// we will ask for the first key from >= 0, expecting 50, but if another transaction inserts something BEFORE 50, our key selector would have returned a different result, causing a conflict

			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr1);
				var foo = subspace.Key("foo", 0);
				// fGE{0} => 50
				var key = await tr1.GetKeyAsync(FdbKeySelector.FirstGreaterOrEqual(foo));
				Assert.That(key, Is.EqualTo(subspace.Key("foo", 50).ToSlice()));

				// 42 < 50 => conflict !!!
				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace2 = await db.Root.Resolve(tr2);
					tr2.Set(subspace2.Key("foo", 42), Text("forty-two"));
					await tr2.CommitAsync();
				}

				// we need to write something to force a conflict
				tr1.Set(subspace.Key("bar"), Slice.Empty);

				await AssertThrowsFdbErrorAsync(() => tr1.CommitAsync(), FdbError.NotCommitted, $"The Set(42) in TR2 should have conflicted with the GetKey(fGE{foo}) in TR1");
			}

			// if the other transaction insert something AFTER 50, our key selector would have still returned the same result, and we would have any conflict

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.ClearRange(subspace);
				tr.Set(subspace.Key("foo", 50), Text("fifty"));
			}, this.Cancellation);

			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr1);
				// fGE{0} => 50
				var key = await tr1.GetKeyAsync(FdbKeySelector.FirstGreaterOrEqual(subspace.Key("foo", 0)));
				Assert.That(key, Is.EqualTo(subspace.Key("foo", 50)));

				// 77 > 50 => no conflict
				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace2 = await db.Root.Resolve(tr2);
					tr2.Set(subspace2.Key("foo", 77), Text("docm"));
					await tr2.CommitAsync();
				}

				// we need to write something to force a conflict
				tr1.Set(subspace.Key("bar"), Slice.Empty);

				// should not conflict!
				await tr1.CommitAsync();
			}

			// but if we have a large offset in the key selector, and another transaction insert something inside the offset window, the result would be different, and it should conflict

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.ClearRange(subspace);
				tr.Set(subspace.Key("foo", 50), Text("fifty"));
				tr.Set(subspace.Key("foo", 100), Text("one hundred"));
			}, this.Cancellation);

			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr1);

				// fGE{50} + 1 => 100
				var key = await tr1.GetKeyAsync(FdbKeySelector.FirstGreaterOrEqual(subspace.Key("foo", 50)) + 1);
				Assert.That(key, Is.EqualTo(subspace.Key("foo", 100)));

				// 77 between 50 and 100 => conflict !!!
				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace2 = await db.Root.Resolve(tr2);
					tr2.Set(subspace2.Key("foo", 77), Text("docm"));
					await tr2.CommitAsync();
				}

				// we need to write something to force a conflict
				tr1.Set(subspace.Key("bar"), Slice.Empty);

				// should conflict!
				await AssertThrowsFdbErrorAsync(() => tr1.CommitAsync(), FdbError.NotCommitted, "The Set(77) in TR2 should have conflicted with the GetKey(fGE{50} + 1) in TR1");
			}

			// does conflict arise from changes in VALUES in the database? or from changes in RESULTS to user queries ?

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.ClearRange(subspace);
				tr.Set(subspace.Key("foo", 50), Text("fifty"));
				tr.Set(subspace.Key("foo", 100), Text("one hundred"));
			}, this.Cancellation);

			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr1);
				// fGT{50} => 100
				var key = await tr1.GetKeyAsync(FdbKeySelector.FirstGreaterThan(subspace.Key("foo", 50)));
				Assert.That(key, Is.EqualTo(subspace.Key("foo", 100)));

				// another transaction changes the VALUE of 50 and 100 (but does not change the fact that they exist nor add keys in between)
				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace2 = await db.Root.Resolve(tr2);
					tr2.Set(subspace2.Key("foo", 100), Text("cent"));
					await tr2.CommitAsync();
				}

				// we need to write something to force a conflict
				tr1.Set(subspace.Key("bar"), Slice.Empty);

				// this causes a conflict in the current version of FDB
				await AssertThrowsFdbErrorAsync(() => tr1.CommitAsync(), FdbError.NotCommitted, "The Set(100) in TR2 should have conflicted with the GetKey(fGT{50}) in TR1");
			}

			// LastLessThan does not create conflicts if the pivot key is changed

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.ClearRange(subspace);
				tr.Set(subspace.Key("foo", 50), Text("fifty"));
				tr.Set(subspace.Key("foo", 100), Text("one hundred"));
			}, this.Cancellation);

			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr1);
				// lLT{100} => 50
				var key = await tr1.GetKeyAsync(FdbKeySelector.LastLessThan(subspace.Key("foo", 100)));
				Assert.That(key, Is.EqualTo(subspace.Key("foo", 50)));

				// another transaction changes the VALUE of 50 and 100 (but does not change the fact that they exist nor add keys in between)
				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace2 = await db.Root.Resolve(tr2);
					tr2.Clear(subspace2.Key("foo", 100));
					await tr2.CommitAsync();
				}

				// we need to write something to force a conflict
				tr1.Set(subspace.Key("bar"), Slice.Empty);

				// this causes a conflict in the current version of FDB
				await tr1.CommitAsync();
			}
		}

		[Test]
		[CoversCells("conflicts/conflict-after-selector-read")]
		public async Task Test_GetKey_FirstGreaterThan_Pivot_Change_Should_Not_Conflict()
		{
			// fGT{k} means "the first key strictly greater than k": whether k itself exists can never change the result,
			// so a concurrent write to the pivot key must not conflict with the resolution

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("foo", 100), Text("one hundred"));
			}, this.Cancellation);

			// the pivot key is ABSENT and the other transaction creates it
			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr1);
				// fGT{50} => 100
				var key = await tr1.GetKeyAsync(FdbKeySelector.FirstGreaterThan(subspace.Key("foo", 50)));
				Assert.That(key, Is.EqualTo(subspace.Key("foo", 100)));

				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace2 = await db.Root.Resolve(tr2);
					tr2.Set(subspace2.Key("foo", 50), Text("fifty"));
					await tr2.CommitAsync();
				}

				// we need to write something to force a conflict
				tr1.Set(subspace.Key("bar"), Slice.Empty);

				// fGT{50} still resolves to 100: no conflict
				await tr1.CommitAsync();
			}

			// the pivot key EXISTS and the other transaction overwrites its value
			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.ClearRange(subspace);
				tr.Set(subspace.Key("foo", 50), Text("fifty"));
				tr.Set(subspace.Key("foo", 100), Text("one hundred"));
			}, this.Cancellation);

			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr1);
				// fGT{50} => 100
				var key = await tr1.GetKeyAsync(FdbKeySelector.FirstGreaterThan(subspace.Key("foo", 50)));
				Assert.That(key, Is.EqualTo(subspace.Key("foo", 100)));

				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace2 = await db.Root.Resolve(tr2);
					tr2.Set(subspace2.Key("foo", 50), Text("FIFTY"));
					await tr2.CommitAsync();
				}

				// we need to write something to force a conflict
				tr1.Set(subspace.Key("bar"), Slice.Empty);

				// the pivot's value is not part of the resolution: no conflict
				await tr1.CommitAsync();
			}
		}

		[Test]
		[CoversCells("conflicts/conflict-after-selector-read")]
		public async Task Test_GetKey_FirstGreaterOrEqual_Resolved_Key_Change_Should_Conflict()
		{
			// the resolved key is always part of a selector's read range (a write to it conflicts, even a pure value
			// change: same coarseness as fGT in Test_GetKey_With_Concurrent_Change_Should_Conflict)

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("foo", 50), Text("fifty"));
			}, this.Cancellation);

			// the other transaction overwrites the resolved key's value
			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr1);
				// fGE{0} => 50
				var key = await tr1.GetKeyAsync(FdbKeySelector.FirstGreaterOrEqual(subspace.Key("foo", 0)));
				Assert.That(key, Is.EqualTo(subspace.Key("foo", 50)));

				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace2 = await db.Root.Resolve(tr2);
					tr2.Set(subspace2.Key("foo", 50), Text("FIFTY"));
					await tr2.CommitAsync();
				}

				// we need to write something to force a conflict
				tr1.Set(subspace.Key("bar"), Slice.Empty);

				await AssertThrowsFdbErrorAsync(() => tr1.CommitAsync(), FdbError.NotCommitted, "The Set(50) in TR2 should have conflicted with the GetKey(fGE{0}) in TR1 (the resolved key is part of the read range)");
			}

			// the other transaction CLEARS the resolved key (the resolution itself would change)
			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.ClearRange(subspace);
				tr.Set(subspace.Key("foo", 50), Text("fifty"));
				tr.Set(subspace.Key("foo", 100), Text("one hundred"));
			}, this.Cancellation);

			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr1);
				// fGE{0} => 50
				var key = await tr1.GetKeyAsync(FdbKeySelector.FirstGreaterOrEqual(subspace.Key("foo", 0)));
				Assert.That(key, Is.EqualTo(subspace.Key("foo", 50)));

				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace2 = await db.Root.Resolve(tr2);
					tr2.Clear(subspace2.Key("foo", 50));
					await tr2.CommitAsync();
				}

				// we need to write something to force a conflict
				tr1.Set(subspace.Key("bar"), Slice.Empty);

				await AssertThrowsFdbErrorAsync(() => tr1.CommitAsync(), FdbError.NotCommitted, "The Clear(50) in TR2 should have conflicted with the GetKey(fGE{0}) in TR1 (clearing the resolved key changes the resolution)");
			}
		}

		[Test]
		[CoversCells("conflicts/conflict-after-selector-read")]
		public async Task Test_GetKey_LastLessOrEqual_Absent_Pivot_Appearing_Should_Conflict()
		{
			// lLE{k} with k absent resolves below k; if another transaction creates k, the resolution itself changes,
			// so the pivot key must be part of the selector's read range

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("foo", 50), Text("fifty"));
			}, this.Cancellation);

			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr1);
				// lLE{100} => 50 (100 is absent)
				var key = await tr1.GetKeyAsync(FdbKeySelector.LastLessOrEqual(subspace.Key("foo", 100)));
				Assert.That(key, Is.EqualTo(subspace.Key("foo", 50)));

				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace2 = await db.Root.Resolve(tr2);
					tr2.Set(subspace2.Key("foo", 100), Text("one hundred"));
					await tr2.CommitAsync();
				}

				// we need to write something to force a conflict
				tr1.Set(subspace.Key("bar"), Slice.Empty);

				await AssertThrowsFdbErrorAsync(() => tr1.CommitAsync(), FdbError.NotCommitted, "The Set(100) in TR2 should have conflicted with the GetKey(lLE{100}) in TR1 (the pivot appearing changes the resolution)");
			}
		}

		[Test]
		[CoversCells("conflicts/conflict-after-range-read")]
		public async Task Test_GetRange_After_Local_Write_With_Concurrent_Change_Should_Conflict()
		{
			// same contract as Test_GetRange_With_Concurrent_Change_Should_Conflict, but the transaction writes BEFORE
			// reading: the read then resolves over the merged view, and its conflict range must still be established

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("foo", 50), Text("fifty"));
			}, this.Cancellation);

			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr1);

				// the local write forces every following read through the merged view
				tr1.Set(subspace.Key("bar"), Slice.Empty);

				// [0, 100) => 50
				var kvp = await tr1
					.GetRange(subspace.Key("foo", 0), subspace.Key("foo", 100))
					.FirstOrDefaultAsync();
				Assert.That(kvp.Key, Is.EqualTo(subspace.Key("foo", 50).ToSlice()));

				// 42 < 50 => conflict !!!
				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace2 = await db.Root.Resolve(tr2);
					tr2.Set(subspace2.Key("foo", 42), Text("forty-two"));
					await tr2.CommitAsync();
				}

				await AssertThrowsFdbErrorAsync(() => tr1.CommitAsync(), FdbError.NotCommitted, "The Set(42) in TR2 should have conflicted with the range read in TR1 (a local write must not erase the read's conflict range)");
			}
		}

		[Test]
		[CoversCells("conflicts/conflict-after-range-read")]
		public async Task Test_GetRange_Partially_Covered_By_Own_Clear_Should_Conflict()
		{
			// the range read is partially covered by an own uncommitted clear-range: the uncovered part is still read
			// from the database, so a peer write there must conflict

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("foo", 50), Text("fifty"));
				tr.Set(subspace.Key("foo", 80), Text("eighty"));
			}, this.Cancellation);

			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr1);

				tr1.ClearRange(subspace.Key("foo", 0), subspace.Key("foo", 70));

				// [0, 100) over the merged view => only 80 survives the local clear
				var items = await tr1.GetRange(subspace.Key("foo", 0), subspace.Key("foo", 100)).ToListAsync();
				Assert.That(items.Select(kv => kv.Key), Is.EqualTo((Slice[]) [ subspace.Key("foo", 80).ToSlice() ]));

				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace2 = await db.Root.Resolve(tr2);
					tr2.Set(subspace2.Key("foo", 90), Text("ninety"));
					await tr2.CommitAsync();
				}

				await AssertThrowsFdbErrorAsync(() => tr1.CommitAsync(), FdbError.NotCommitted, "The Set(90) in TR2 should have conflicted with the range read in TR1 (90 is outside the local clear, so it was read from the database)");
			}
		}

		[Test]
		[CoversCells("conflicts/conflict-after-range-read")]
		public async Task Test_GetRange_Fully_Covered_By_Own_Clear_Should_Not_Conflict()
		{
			// oracle-pinned: a range read fully answered by an own uncommitted clear-range never touches the database,
			// and the real client adds NO read conflict for it (the range-level analogue of the point-get exemption)

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("foo", 50), Text("fifty"));
			}, this.Cancellation);

			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr1);

				tr1.ClearRange(subspace.Key("foo", 0), subspace.Key("foo", 100));

				// [10, 90) over the merged view => empty, fully determined by the local clear
				var items = await tr1.GetRange(subspace.Key("foo", 10), subspace.Key("foo", 90)).ToListAsync();
				Assert.That(items, Is.Empty);

				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace2 = await db.Root.Resolve(tr2);
					tr2.Set(subspace2.Key("foo", 40), Text("forty"));
					await tr2.CommitAsync();
				}

				// no conflict: the read never depended on the database
				await tr1.CommitAsync();
			}
		}

		[Test]
		[CoversCells("conflicts/conflict-after-range-read")]
		public async Task Test_GetRange_Peer_Write_Under_Own_Clear_Segment_Should_Not_Conflict()
		{
			// oracle-pinned: the range read is PARTIALLY covered by an own clear-range and the peer writes INSIDE the
			// covered part; the real client subtracts locally-determined segments from the read conflict range at
			// segment granularity (not all-or-nothing per read)

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("foo", 50), Text("fifty"));
				tr.Set(subspace.Key("foo", 80), Text("eighty"));
			}, this.Cancellation);

			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr1);

				tr1.ClearRange(subspace.Key("foo", 0), subspace.Key("foo", 70));

				// [0, 100) over the merged view => only 80 survives the local clear
				var items = await tr1.GetRange(subspace.Key("foo", 0), subspace.Key("foo", 100)).ToListAsync();
				Assert.That(items.Select(kv => kv.Key), Is.EqualTo((Slice[]) [ subspace.Key("foo", 80).ToSlice() ]));

				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace2 = await db.Root.Resolve(tr2);
					tr2.Set(subspace2.Key("foo", 30), Text("thirty"));
					await tr2.CommitAsync();
				}

				// the write cache knows [0,70) is locally determined, so 30 is outside the read's database dependency
				await tr1.CommitAsync();
			}
		}

		[Test]
		[CoversCells("conflicts/conflict-after-range-read")]
		public async Task Test_GetRange_Over_Own_Written_Key_Should_Not_Conflict()
		{
			// oracle-pinned: a peer write to a key the transaction itself SET inside the read range does not conflict;
			// the single-key segment is subtracted from the read conflict just like a cleared segment

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr1);

				tr1.Set(subspace.Key("foo", 50), Text("mine"));

				// [40, 60) over the merged view => the own write
				var items = await tr1.GetRange(subspace.Key("foo", 40), subspace.Key("foo", 60)).ToListAsync();
				Assert.That(items.Select(kv => kv.Key), Is.EqualTo((Slice[]) [ subspace.Key("foo", 50).ToSlice() ]));

				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace2 = await db.Root.Resolve(tr2);
					tr2.Set(subspace2.Key("foo", 50), Text("theirs"));
					await tr2.CommitAsync();
				}

				// no conflict: the own-written key's segment never depended on the database
				await tr1.CommitAsync();
			}
		}

		[Test]
		[CoversCells("conflicts/own-read-own-write-no-conflict")]
		public async Task Test_Snapshot_Read_Of_Own_Atomic_Keeps_Commit_Time_Application()
		{
			// an atomic mutation applies over the COMMITTED value at commit time; a SNAPSHOT read of the key must not
			// change that (only a regular read converts the atomic into a set of the read value, per the documented
			// read-your-writes caveat)

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr1);

				// pin tr1's read version before the peer commit
				_ = await tr1.GetAsync(subspace.Key("k0"));

				// the peer gives the key a committed value AFTER tr1's read version
				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace2 = await db.Root.Resolve(tr2);
					tr2.Set(subspace2.Key("k2"), Slice.FromFixed64(1000));
					await tr2.CommitAsync();
				}

				tr1.Atomic(subspace.Key("k2"), Slice.FromFixed64(90), FdbMutationType.Add);

				// snapshot reads over the atomic (point and range): they observe the chain over tr1's own snapshot...
				var value = await tr1.Snapshot.GetAsync(subspace.Key("k2"));
				Assert.That(value, Is.EqualTo(Slice.FromFixed64(90)), "the snapshot read sees the atomic applied over tr1's snapshot (where the key is absent)");
				var items = await tr1.Snapshot.GetRange(subspace.Key("k1"), subspace.Key("k3")).ToListAsync();
				Assert.That(items.Select(kv => kv.Value), Is.EqualTo((Slice[]) [ Slice.FromFixed64(90) ]));

				// ...but must not disturb the mutation itself: no conflict, and the add applies over the peer's value
				await tr1.CommitAsync();
			}

			var final = await db.ReadAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				return await tr.GetAsync(subspace.Key("k2"));
			}, this.Cancellation);
			Assert.That(final, Is.EqualTo(Slice.FromFixed64(1090)), "the atomic must have applied over the committed value at commit time (1000 + 90), not over tr1's snapshot");
		}

		[Test]
		[CoversCells("conflicts/read-write-conflict-loses")]
		public async Task Test_Regular_Read_Of_Own_Atomic_Becomes_A_Conflictable_Set()
		{
			// the documented caveat: a REGULAR read of a key previously modified by an atomic operation converts the
			// operation into a set of the read value, and the read establishes a normal conflict range on the key

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr1);

				// pin tr1's read version before the peer commit
				_ = await tr1.GetAsync(subspace.Key("k0"));

				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace2 = await db.Root.Resolve(tr2);
					tr2.Set(subspace2.Key("k2"), Slice.FromFixed64(1000));
					await tr2.CommitAsync();
				}

				tr1.Atomic(subspace.Key("k2"), Slice.FromFixed64(90), FdbMutationType.Add);

				// the REGULAR read converts the atomic and reads through to the database at tr1's read version
				var value = await tr1.GetAsync(subspace.Key("k2"));
				Assert.That(value, Is.EqualTo(Slice.FromFixed64(90)), "the read sees the atomic applied over tr1's snapshot (where the key is absent)");

				// the conversion made the read conflictable: the peer's commit is after tr1's read version
				await AssertThrowsFdbErrorAsync(() => tr1.CommitAsync(), FdbError.NotCommitted, "Reading the atomic's key converted it into a normal read-write pattern, which conflicts with the peer's Set");
			}
		}

		[Test]
		public async Task Test_Unread_Atomic_Applies_Over_Committed_Value()
		{
			// control: an atomic the transaction never reads applies over the latest committed value, without conflict

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr1);

				// pin tr1's read version before the peer commit
				_ = await tr1.GetAsync(subspace.Key("k0"));

				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace2 = await db.Root.Resolve(tr2);
					tr2.Set(subspace2.Key("k2"), Slice.FromFixed64(1000));
					await tr2.CommitAsync();
				}

				tr1.Atomic(subspace.Key("k2"), Slice.FromFixed64(90), FdbMutationType.Add);
				await tr1.CommitAsync();
			}

			var final = await db.ReadAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				return await tr.GetAsync(subspace.Key("k2"));
			}, this.Cancellation);
			Assert.That(final, Is.EqualTo(Slice.FromFixed64(1090)), "the atomic must have applied over the committed value at commit time (1000 + 90)");
		}

		[Test]
		[CoversCells("conflicts/conflict-after-range-read")]
		public async Task Test_Conflict_Ranges_Survive_A_Range_Read_Below_All_Data()
		{
			// the recorded conflict evidence must survive LATER reads whose resolution lands below every key read
			// so far (the internal conflict-range set merges the new low range into its neighbors; a lost span
			// here silently forgets an earlier read and misses its conflict)

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				// mirrors the fuzz find: reads bypass the local writes and always establish conflict ranges
				tr1.Options.WithReadYourWritesDisable();
				var subspace = await db.Root.Resolve(tr1);

				// the conflict evidence: a range read covering k3 (empty at this read version)
				var items = await tr1.GetRange(subspace.Key("k2"), subspace.Key("k4")).ToListAsync();
				Assert.That(items, Is.Empty);

				// a point read below it, then a range read over the empty low end: its begin resolves below
				// every key, and the merge of that low range absorbs the earlier marks
				_ = await tr1.GetAsync(subspace.Key("k1"));
				_ = await tr1.GetRange(subspace.Key("k0"), subspace.Key("k2")).ToListAsync();

				// the peer commits inside the window, on the key the FIRST read covered
				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace2 = await db.Root.Resolve(tr2);
					tr2.Set(subspace2.Key("k3"), Text("v6"));
					await tr2.CommitAsync();
				}

				tr1.Set(subspace.Key("k5"), Text("w"));

				await AssertThrowsFdbErrorAsync(() => tr1.CommitAsync(), FdbError.NotCommitted, "The Set(k3) in TR2 should have conflicted with TR1's first range read (its conflict range must survive the later low reads)");
			}
		}

		[Test]
		public async Task Test_Read_Isolation()
		{
			// > initial state: A = 1
			// > T1 starts
			// > T1 gets read_version
			// >				> T2 starts
			// >				> T2 set A = 2
			// >				> T2 commits successfully
			// > T1 reads A
			// > T1 commits

			// T1 should see A == 1, because it was started before T2

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("test", "A"), Slice.FromInt32(1));
			}, this.Cancellation);
			using(var tr1 = db.BeginTransaction(this.Cancellation))
			{
				// make sure that T1 has seen the db BEFORE T2 gets executed, or else it will not really be initialized until after the first read or commit
				await tr1.GetReadVersionAsync();
				//T1 should be locked to a specific version of the db

				var subspace1 = await db.Root.Resolve(tr1);
				var key = subspace1.Key("test", "A");

				// change the value in T2
				await db.WriteAsync((tr) => tr.Set(key, Slice.FromInt32(2)), this.Cancellation);

				// read the value in T1 and commits
				var value = await tr1.GetAsync(key);

				Assert.That(value, Is.Not.Null);
				Assert.That(value.ToInt32(), Is.EqualTo(1), "T1 should NOT have seen the value modified by T2");

				// committing should not conflict, because we read the value AFTER it was changed
				await tr1.CommitAsync();
			}

			// If we do the same thing, but this time without get GetReadVersion(), then T1 should see the change made by T2 because it's actual start is delayed

			// > initial state: A = 1
			// > T1 starts
			// >				> T2 starts
			// >				> T2 set A = 2
			// >				> T2 commits successfully
			// > T1 reads A
			// > T1 commits

			// T1 should see A == 2, because in reality, it was started after T2
			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("test", "A"), Slice.FromInt32(1));
			}, this.Cancellation);
			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				//do NOT use T1 yet

				// change the value in T2
				await db.WriteAsync(async (tr2) =>
				{
					var subspace2 = await db.Root.Resolve(tr2);
					tr2.Set(subspace2.Key("test", "A"), Slice.FromInt32(2));
				}, this.Cancellation);


				// read the value in T1 and commits
				var subspace1 = await db.Root.Resolve(tr1);
				var value = await tr1.GetAsync(subspace1.Key("test", "A"));

				Assert.That(value, Is.Not.Null);
				Assert.That(value.ToInt32(), Is.EqualTo(2), "T1 should have seen the value modified by T2");

				// committing should not conflict, because we read the value AFTER it was changed
				await tr1.CommitAsync();
			}
		}

		[Test]
		public async Task Test_Read_Isolation_From_Writes()
		{
			// By default,
			// - Regular reads see the writes made by the transaction itself, but not the writes made by other transactions that committed in between
			// - Snapshot reads never see the writes made since the transaction read version, but will see the writes made by the transaction itself

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			// Reads (before and after):
			// - A and B will use regular reads
			// - C and D will use snapshot reads
			// Writes:
			// - A and C will be modified by the transaction itself
			// - B and D will be modified by a different transaction

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("A"), Text("a"));
				tr.Set(subspace.Key("B"), Text("b"));
				tr.Set(subspace.Key("C"), Text("c"));
				tr.Set(subspace.Key("D"), Text("d"));
			}, this.Cancellation);

			Log("Initial db state:");
			await DumpSubspace(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				// check initial state
				Assert.That((await tr.GetAsync(subspace.Key("A"))).ToStringUtf8(), Is.EqualTo("a"));
				Assert.That((await tr.GetAsync(subspace.Key("B"))).ToStringUtf8(), Is.EqualTo("b"));
				Assert.That((await tr.Snapshot.GetAsync(subspace.Key("C"))).ToStringUtf8(), Is.EqualTo("c"));
				Assert.That((await tr.Snapshot.GetAsync(subspace.Key("D"))).ToStringUtf8(), Is.EqualTo("d"));

				// mutate (not yet committed)
				tr.Set(subspace.Key("A"), Text("aa"));
				tr.Set(subspace.Key("C"), Text("cc"));
				await db.WriteAsync((tr2) =>
				{ // have another transaction change B and D under our nose
					tr2.Set(subspace.Key("B"), Text("bb"));
					tr2.Set(subspace.Key("D"), Text("dd"));
				}, this.Cancellation);

				// check what the transaction sees
				Assert.That((await tr.GetAsync(subspace.Key("A"))).ToStringUtf8(), Is.EqualTo("aa"), "The transaction own writes should change the value of regular reads");
				Assert.That((await tr.GetAsync(subspace.Key("B"))).ToStringUtf8(), Is.EqualTo("b"), "Other transaction writes should not change the value of regular reads");
				Assert.That((await tr.Snapshot.GetAsync(subspace.Key("C"))).ToStringUtf8(), Is.EqualTo("cc"), "The transaction own writes should be visible in snapshot reads");
				Assert.That((await tr.Snapshot.GetAsync(subspace.Key("D"))).ToStringUtf8(), Is.EqualTo("d"), "Other transaction writes should not change the value of snapshot reads");

				//note: committing here would conflict
			}
		}

		[Test]
		public async Task Test_Read_Isolation_From_Writes_Pre_300()
		{
			// By in API v200 and below:
			// - Regular reads see the writes made by the transaction itself, but not the writes made by other transactions that committed in between
			// - Snapshot reads never see the writes made since the transaction read version, but will see the writes made by the transaction itself
			// In API 300, this can be emulated by setting the SnapshotReadYourWriteDisable options

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			// Reads (before and after):
			// - A and B will use regular reads
			// - C and D will use snapshot reads
			// Writes:
			// - A and C will be modified by the transaction itself
			// - B and D will be modified by a different transaction

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("A"), Text("a"));
				tr.Set(subspace.Key("B"), Text("b"));
				tr.Set(subspace.Key("C"), Text("c"));
				tr.Set(subspace.Key("D"), Text("d"));
			}, this.Cancellation);

			Log("Initial db state:");
			await DumpSubspace(db);

			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				tr.Options.WithSnapshotReadYourWritesDisable();

				// check initial state
				Assert.That((await tr.GetAsync(subspace.Key("A"))).ToStringUtf8(), Is.EqualTo("a"));
				Assert.That((await tr.GetAsync(subspace.Key("B"))).ToStringUtf8(), Is.EqualTo("b"));
				Assert.That((await tr.Snapshot.GetAsync(subspace.Key("C"))).ToStringUtf8(), Is.EqualTo("c"));
				Assert.That((await tr.Snapshot.GetAsync(subspace.Key("D"))).ToStringUtf8(), Is.EqualTo("d"));

				// mutate (not yet committed)
				tr.Set(subspace.Key("A"), Text("aa"));
				tr.Set(subspace.Key("C"), Text("cc"));
				await db.WriteAsync((tr2) =>
				{ // have another transaction change B and D under our nose
					tr2.Set(subspace.Key("B"), Text("bb"));
					tr2.Set(subspace.Key("D"), Text("dd"));
				}, this.Cancellation);

				// check what the transaction sees
				Assert.That((await tr.GetAsync(subspace.Key("A"))).ToStringUtf8(), Is.EqualTo("aa"), "The transaction own writes should change the value of regular reads");
				Assert.That((await tr.GetAsync(subspace.Key("B"))).ToStringUtf8(), Is.EqualTo("b"), "Other transaction writes should not change the value of regular reads");
				//FAIL: test fails here because we read "CC" ??
				Assert.That((await tr.Snapshot.GetAsync(subspace.Key("C"))).ToStringUtf8(), Is.EqualTo("c"), "The transaction own writes should not change the value of snapshot reads");
				Assert.That((await tr.Snapshot.GetAsync(subspace.Key("D"))).ToStringUtf8(), Is.EqualTo("d"), "Other transaction writes should not change the value of snapshot reads");

				//note: committing here would conflict
			}
		}

		[Test]
		public async Task Test_ReadYourWritesDisable_Isolation()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			//db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			#region Default behaviour...

			// By default, a transaction see its own writes with non-snapshot reads

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("a"), Text("a"));
				tr.Set(subspace.Key("b", 10), Text("PRINT \"HELLO\""));
				tr.Set(subspace.Key("b", 20), Text("GOTO 10"));
			}, this.Cancellation);

			using(var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				var data = await tr.GetAsync(subspace.Key("a"));
				Assert.That(data.ToUnicode(), Is.EqualTo("a"));
					
				var res = await tr.GetRange(subspace.Key("b").ToRange()).Select(kvp => kvp.Value.ToString()).ToArrayAsync();
				Assert.That(res, Is.EqualTo([ "PRINT \"HELLO\"", "GOTO 10" ]));

				tr.Set(subspace.Key("a"), Text("aa"));
				tr.Set(subspace.Key("b", 15), Text("PRINT \"WORLD\""));

				data = await tr.GetAsync(subspace.Key("a"));
				Assert.That(data.ToUnicode(), Is.EqualTo("aa"), "The transaction own writes should be visible by default");
				res = await tr.GetRange(subspace.Key("b").ToRange()).Select(kvp => kvp.Value.ToString()).ToArrayAsync();
				Assert.That(res, Is.EqualTo([ "PRINT \"HELLO\"", "PRINT \"WORLD\"", "GOTO 10" ]), "The transaction own writes should be visible by default");

				//note: don't commit
			}

			#endregion

			#region ReadYourWritesDisable behaviour...

			// The ReadYourWritesDisable option cause reads to always return the value in the database

			// note: this one is tricky: you CANNOT set this option once a read has been done, and we need to read to get the subspace!
			// We will cheat by starting two transactions, the first one used to get the subspace, and the second one will "steal" the
			// key prefix to write to the db. To make it safer, the second transaction will use the same read version.

			// DON'T TRY THIS AT HOME! doing this in prod could lead to corruption !!!

			using (var trSubspace = db.BeginTransaction(this.Cancellation))
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				// resolve the subspace in a witness transaction
				var subspace = await db.Root.Resolve(trSubspace);
				tr.SetReadVersion(await trSubspace.GetReadVersionAsync());

				tr.Options.WithReadYourWritesDisable();

				var data = await tr.GetAsync(subspace.Key("a"));
				Assert.That(data.ToUnicode(), Is.EqualTo("a"));
				var res = await tr.GetRange(subspace.Key("b").ToRange()).Select(kvp => kvp.Value.ToString()).ToArrayAsync();
				Assert.That(res, Is.EqualTo([ "PRINT \"HELLO\"", "GOTO 10" ]));

				tr.Set(subspace.Key("a"), Text("aa"));
				tr.Set(subspace.Key("b", 15), Text("PRINT \"WORLD\""));

				data = await tr.GetAsync(subspace.Key("a"));
				Assert.That(data.ToUnicode(), Is.EqualTo("a"), "The transaction own writes should not be seen with ReadYourWritesDisable option enabled");
				res = await tr.GetRange(subspace.Key("b").ToRange()).Select(kvp => kvp.Value.ToString()).ToArrayAsync();
				Assert.That(res, Is.EqualTo([ "PRINT \"HELLO\"", "GOTO 10" ]), "The transaction own writes should not be seen with ReadYourWritesDisable option enabled");

				//note: don't commit!
			}

			#endregion
		}

		[Test]
		public async Task Test_Can_Set_Read_Version()
		{
			// Verify that we can set a read version on a transaction
			// * tr1 will set value to 1
			// * tr2 will set value to 2
			// * tr3 will SetReadVersion(TR1.CommittedVersion) and we expect it to read 1 (and not 2)

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			long committedVersion;

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			// create first version
			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr1);
				tr1.Set(subspace.Key("concurrent"), Slice.FromByte(1));
				await tr1.CommitAsync();

				// get this version
				committedVersion = tr1.GetCommittedVersion();
			}

			// mutate in another transaction
			using (var tr2 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr2);
				tr2.Set(subspace.Key("concurrent"), Slice.FromByte(2));
				await tr2.CommitAsync();
			}

			// read the value with TR1's committed version
			using (var tr3 = db.BeginTransaction(this.Cancellation))
			{
				tr3.SetReadVersion(committedVersion);

				long ver = await tr3.GetReadVersionAsync();
				Assert.That(ver, Is.EqualTo(committedVersion), "GetReadVersion should return the same value as SetReadVersion!");

				var subspace = await db.Root.Resolve(tr3);

				var bytes = await tr3.GetAsync(subspace.Key("concurrent"));

				Assert.That(bytes.GetBytes(), Is.EqualTo(new byte[] { 1 }), "Should have seen the first version!");
			}
		}

		[Test]
		[CoversCells("options/access-system-keys")]
		public async Task Test_Has_Access_To_System_Keys()
		{
			using var db = await OpenTestDatabaseAsync();
			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			using var tr = db.BeginTransaction(this.Cancellation);
			// should fail if access to system keys has not been requested

			await AssertThrowsFdbErrorAsync(
				() => tr.GetRange(Slice.FromByteString("\xFF"), Slice.FromByteString("\xFF\xFF"), new FdbRangeOptions { Limit = 10 }).ToListAsync(),
				FdbError.KeyOutsideLegalRange,
				"Should not have access to system keys by default"
			);

			// should succeed once system access has been requested
			tr.Options.WithReadAccessToSystemKeys();

			var keys = await tr.GetRange(Slice.FromByteString("\xFF"), Slice.FromByteString("\xFF\xFF"), new FdbRangeOptions { Limit = 10 }).ToListAsync();
			Assert.That(keys, Is.Not.Null);
		}

		[Test]
		public async Task Test_Can_Set_Transaction_Options()
		{
			using var db = await OpenTestDatabaseAsync();

			using var tr = db.BeginTransaction(this.Cancellation);


			Assert.Multiple(() =>
			{
				Assert.That(tr.Options.Timeout, Is.EqualTo(15_000), "Timeout (default)");
				Assert.That(tr.Options.RetryLimit, Is.Zero, "RetryLimit (default)");
				Assert.That(tr.Options.MaxRetryDelay, Is.Zero, "MaxRetryDelay (default)");
				Assert.That(tr.Options.Tracing, Is.EqualTo(FdbTracingOptions.Default), "Tracing (default)");
			});

			tr.Options.Timeout = 1_000; // 1 sec max
			tr.Options.RetryLimit = 5; // 5 retries max
			tr.Options.MaxRetryDelay = 500; // .5 sec max
			tr.Options.Tracing = FdbTracingOptions.RecordTransactions | FdbTracingOptions.RecordOperations | FdbTracingOptions.RecordApiCalls;

			Assert.Multiple(() =>
			{
				Assert.That(tr.Options.Timeout, Is.EqualTo(1_000), "Timeout");
				Assert.That(tr.Options.RetryLimit, Is.EqualTo(5), "RetryLimit");
				Assert.That(tr.Options.MaxRetryDelay, Is.EqualTo(500), "MaxRetryDelay");
				Assert.That(tr.Options.Tracing, Is.EqualTo(FdbTracingOptions.RecordTransactions | FdbTracingOptions.RecordOperations | FdbTracingOptions.RecordApiCalls), "Tracing");
			});
		}

		[Test]
		public async Task Test_Transaction_Options_Inherit_Default_From_Database()
		{
			using var db = await OpenTestDatabaseAsync();

			Assert.Multiple(() =>
			{
				Assert.That(db.Options.DefaultTimeout, Is.EqualTo(15_000), "db.DefaultTimeout (default)");
				Assert.That(db.Options.DefaultRetryLimit, Is.Zero, "db.DefaultRetryLimit (default)");
				Assert.That(db.Options.DefaultMaxRetryDelay, Is.Zero, "db.DefaultMaxRetryDelay (default)");
				Assert.That(db.Options.DefaultTracing, Is.EqualTo(FdbTracingOptions.Default), "db.DefaultTracing (default)");
			});

			db.Options.DefaultTimeout = 500;
			db.Options.DefaultRetryLimit = 3;
			db.Options.DefaultMaxRetryDelay = 600;
			db.Options.DefaultTracing = FdbTracingOptions.RecordTransactions | FdbTracingOptions.RecordOperations;

			Assert.Multiple(() =>
			{
				Assert.That(db.Options.DefaultTimeout, Is.EqualTo(500), "db.DefaultTimeout");
				Assert.That(db.Options.DefaultRetryLimit, Is.EqualTo(3), "db.DefaultRetryLimit");
				Assert.That(db.Options.DefaultMaxRetryDelay, Is.EqualTo(600), "db.DefaultMaxRetryDelay");
				Assert.That(db.Options.DefaultTracing, Is.EqualTo(FdbTracingOptions.RecordTransactions | FdbTracingOptions.RecordOperations), "db.DefaultTracing");
			});

			// transaction should be already configured with the default options

			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				Assert.Multiple(() =>
				{
					Assert.That(tr.Options.Timeout, Is.EqualTo(500), "tr.Timeout");
					Assert.That(tr.Options.RetryLimit, Is.EqualTo(3), "tr.RetryLimit");
					Assert.That(tr.Options.MaxRetryDelay, Is.EqualTo(600), "tr.MaxRetryDelay");
					Assert.That(tr.Options.Tracing, Is.EqualTo(FdbTracingOptions.RecordTransactions | FdbTracingOptions.RecordOperations), "tr.Tracing");
				});

				// changing the default on the db should only affect new transactions

				db.Options.DefaultTimeout = 600;
				db.Options.DefaultRetryLimit = 4;
				db.Options.DefaultMaxRetryDelay = 700;
				db.Options.DefaultTracing = FdbTracingOptions.RecordApiCalls | FdbTracingOptions.RecordSteps;

				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					Assert.Multiple(() =>
					{
						// tr2 should have the new options
						Assert.That(tr2.Options.Timeout, Is.EqualTo(600), "tr2.Options.Timeout");
						Assert.That(tr2.Options.RetryLimit, Is.EqualTo(4), "tr2.Options.RetryLimit");
						Assert.That(tr2.Options.MaxRetryDelay, Is.EqualTo(700), "tr2.Options.MaxRetryDelay");
						Assert.That(tr2.Options.Tracing, Is.EqualTo(FdbTracingOptions.RecordApiCalls | FdbTracingOptions.RecordSteps), "tr2.Options.Tracing");

						// original transaction should not be affected
						Assert.That(tr.Options.Timeout, Is.EqualTo(500), "tr.Options.Timeout");
						Assert.That(tr.Options.RetryLimit, Is.EqualTo(3), "tr.Options.RetryLimit");
						Assert.That(tr.Options.MaxRetryDelay, Is.EqualTo(600), "tr.Options.MaxRetryDelay");
						Assert.That(tr.Options.Tracing, Is.EqualTo(FdbTracingOptions.RecordTransactions | FdbTracingOptions.RecordOperations), "tr.Options.Tracing");
					});
				}

				// resetting the transaction should use the new database settings
				tr.Reset();

				Assert.Multiple(() =>
				{
					Assert.That(tr.Options.Timeout, Is.EqualTo(600), "tr.Options.Timeout (after reset)");
					Assert.That(tr.Options.RetryLimit, Is.EqualTo(4), "tr.Options.RetryLimit (after reset)");
					Assert.That(tr.Options.MaxRetryDelay, Is.EqualTo(700), "tr.Options.MaxRetryDelay (after reset)");
					Assert.That(tr.Options.Tracing, Is.EqualTo(FdbTracingOptions.RecordApiCalls | FdbTracingOptions.RecordSteps), "tr.Options.Tracing (after reset)");
				});
			}
		}

		[Test]
		public async Task Test_Transaction_RetryLoop_Respects_DefaultRetryLimit_Value()
		{
			using var db = await OpenTestDatabaseAsync();
			using var go = new CancellationTokenSource();

			Assert.That(db.Options.DefaultTimeout, Is.EqualTo(15000), "db.DefaultTimeout (default)");
			Assert.That(db.Options.DefaultRetryLimit, Is.Zero, "db.DefaultRetryLimit (default)");

			// By default, a transaction that gets reset or retried, clears the RetryLimit and Timeout settings, which needs to be reset everytime.
			// But if the DefaultRetryLimit and DefaultTimeout are set on the database instance, they should automatically be re-applied inside transaction loops!
			db.Options.DefaultRetryLimit = 3;

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			int counter = 0;
			var t = db.ReadAsync<int>((tr) =>
			{
				++counter;
				Log($"Called {counter} time(s)");
				if (counter > 4)
				{
					go.Cancel();
					tr.Context.Abort = true;
					Assert.Fail("The retry loop was called too many times!");
				}

				Assert.That(tr.Options.RetryLimit, Is.EqualTo(3), "tr.Options.RetryLimit");

				// simulate a retryable error condition
				// (the message is spelled out: the code-only ctor resolves it through the native fdb_c library, which is absent on cluster-less machines)
				throw new FdbException(FdbError.TransactionTooOld, nameof(FdbError.TransactionTooOld));
			}, go.Token);

			try
			{
				await t;
				Assert.Fail("Should have failed!");
			}
			catch (AssertionException) { throw; }
			catch (Exception e)
			{
				Assert.That(e, Is.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.TransactionTooOld));
			}

			Assert.That(counter, Is.EqualTo(4), "1 first attempt + 3 retries = 4 executions");
		}

		[Test]
		public async Task Test_Transaction_RetryLoop_Resets_RetryLimit_And_Timeout()
		{
			using var db = await OpenTestDatabaseAsync();
			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				// simulate a first error
				tr.Options.RetryLimit = 10;
				await tr.OnErrorAsync(FdbError.TransactionTooOld);
				Assert.That(tr.Options.RetryLimit, Is.Zero, "Retry limit should be reset");

				// simulate some more errors
				await tr.OnErrorAsync(FdbError.TransactionTooOld);
				await tr.OnErrorAsync(FdbError.TransactionTooOld);
				await tr.OnErrorAsync(FdbError.TransactionTooOld);
				await tr.OnErrorAsync(FdbError.TransactionTooOld);
				Assert.That(tr.Options.RetryLimit, Is.Zero, "Retry limit should be reset");

				// we still haven't failed 10 times...
				tr.Options.RetryLimit = 10;
				await tr.OnErrorAsync(FdbError.TransactionTooOld);
				Assert.That(tr.Options.RetryLimit, Is.Zero, "Retry limit should be reset");

				// we already have failed 6 times, so this one should abort
				tr.Options.RetryLimit = 2; // value is too low
				Assert.That(async () => await tr.OnErrorAsync(FdbError.TransactionTooOld), Throws.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.TransactionTooOld));
			}
		}

		[Test]
		[CoversCells("conflicts/explicit-read-conflict-range")]
		public async Task Test_Can_Add_Read_Conflict_Range()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr1);

				await tr1.GetAsync(subspace.Key(1));
				// tr1 writes to one key
				tr1.Set(subspace.Key(1), Text("hello"));
				// but add the second as a conflict range
				tr1.AddReadConflictKey(subspace.Key(2));

				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace2 = await db.Root.Resolve(tr2);

					// tr2 writes to the second key
					tr2.Set(subspace2.Key(2), Text("world"));

					// tr2 should succeed
					await tr2.CommitAsync();
				}

				// tr1 should conflict on the second key
				await AssertThrowsFdbErrorAsync(
					() => tr1.CommitAsync(),
					FdbError.NotCommitted,
					"Transaction should have resulted in a conflict on key2"
				);
			}
		}

		[Test]
		[CoversCells("conflicts/explicit-write-conflict-range")]
		public async Task Test_Can_Add_Write_Conflict_Range()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr1);

				// tr1 reads the conflicting key
				await tr1.GetAsync(subspace.Key(0));
				// and writes to key1
				tr1.Set(subspace.Key(1), Text("hello"));

				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace2 = await db.Root.Resolve(tr2);

					// tr2 changes key2, but adds a conflict range on the conflicting key
					tr2.Set(subspace2.Key(2), Text("world"));

					// and writes on the third
					tr2.AddWriteConflictKey(subspace2.Key(0)); // conflict!

					await tr2.CommitAsync();
				}

				// tr1 should conflict
				await AssertThrowsFdbErrorAsync(
					() => tr1.CommitAsync(),
					FdbError.NotCommitted,
					"Transaction should have resulted in a conflict"
				);
			}
		}

		[Test]
		[CoversCells("watches/no-fire-single-commit-aba")]
		public async Task Test_Watch_Registration_Compares_Endpoint_Values()
		{
			RequireCleanWatches(); // clean watches: this test asserts exact fire/pending semantics

			// the pre-arm window is judged by VALUES at the endpoints, not by version history: a key that changed
			// and reverted across two commits between the watching transaction's read version and its arming
			// commit registers as still-pending (oracle-pinned; contrast with the POST-arm two-commit case, where
			// the armed watch sees the intermediate commit and fires)

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			using var tr = db.BeginTransaction(this.Cancellation);
			var subspace = await db.Root.Resolve(tr);
			// the watch's expected value reads as absent at this transaction's read version
			var watch = tr.Watch(subspace.Key("k5"), this.Cancellation);

			await db.WriteAsync(async t => { var s = await db.Root.Resolve(t); t.Set(s.Key("k5"), Text("x")); }, this.Cancellation);
			await db.WriteAsync(async t => { var s = await db.Root.Resolve(t); t.Clear(s.Key("k5")); }, this.Cancellation);

			await tr.CommitAsync();

			await Task.Delay(500, this.Cancellation);
			Assert.That(watch.Task.IsCompleted, Is.False, "a change-and-revert BEFORE the arm compares equal at registration: the watch stays pending");
			watch.Cancel();
		}

		[Test]
		public async Task Test_Same_Key_Watches_Fire_Together()
		{
			// the real client stack entangles same-key watches: when one fires (here: a peer's watch arming with a
			// stale expected value, an immediate legitimate fire), every registered watch on that key fires with it
			// - a contract-permitted spurious fire for the dragged watch

			if (!this.UseRealServer) Assert.Ignore("FakeDb divergence: watches are independent - a same-key sibling's fire does not spuriously fire the others (the real client stack entangles same-key watches; spurious fires are permitted, not required, so deterministic independence is the emulator's deliberate behavior)");

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			using var trA = db.BeginTransaction(this.Cancellation);
			var subspaceA = await db.Root.Resolve(trA);
			var watchA = trA.Watch(subspaceA.Key("k5"), this.Cancellation); // expected: absent

			await db.WriteAsync(async t => { var s = await db.Root.Resolve(t); t.Set(s.Key("k5"), Text("x")); }, this.Cancellation);

			using var trB = db.BeginTransaction(this.Cancellation);
			var subspaceB = await db.Root.Resolve(trB);
			var watchB = trB.Watch(subspaceB.Key("k5"), this.Cancellation); // expected: x (the transient value)

			await db.WriteAsync(async t => { var s = await db.Root.Resolve(t); t.Clear(s.Key("k5")); }, this.Cancellation);

			await trA.CommitAsync(); // arms A's watch: expected absent == current absent -> registered pending
			await trB.CommitAsync(); // arms B's watch: expected x != current absent -> immediate fire

			var settledB = await Task.WhenAny(watchB.Task, Task.Delay(TimeSpan.FromSeconds(15), this.Cancellation));
			Assert.That(settledB, Is.SameAs(watchB.Task), "the stale-expected watch must fire immediately at its arm");

			var settledA = await Task.WhenAny(watchA.Task, Task.Delay(TimeSpan.FromSeconds(15), this.Cancellation));
			Assert.That(settledA, Is.SameAs(watchA.Task), "the sibling's fire must drag the same-key pending watch with it");
		}

		[Test]
		public async Task Test_Can_Setup_And_Cancel_Watches()
		{
			RequireCleanWatches(); // clean watches: this test asserts exact fire/pending semantics
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			await db.WriteAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("watched"), Text("some value"));
				tr.Set(subspace.Key("witness"), Text("some other value"));
			}, this.Cancellation);

			using var cts = new CancellationTokenSource();

			FdbWatch w1;
			FdbWatch w2;

			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);
				w1 = tr.Watch(subspace.Key("watched"), cts.Token);
				w2 = tr.Watch(subspace.Key("witness"), cts.Token);
				Assert.That(w1, Is.Not.Null);
				Assert.That(w2, Is.Not.Null);

				// note: Watches will get cancelled if the transaction is not committed !
				await tr.CommitAsync();
			}

			// Watches should survive the transaction
			await Task.Delay(100, this.Cancellation);
			Assert.That(w1.Task.Status, Is.EqualTo(TaskStatus.WaitingForActivation), "w1 should survive the transaction without being triggered");
			Assert.That(w2.Task.Status, Is.EqualTo(TaskStatus.WaitingForActivation), "w2 should survive the transaction without being triggered");

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("watched"), Text("some new value"));
			}, this.Cancellation);

			// the first watch should have triggered
			await Task.Delay(100, this.Cancellation);
			Assert.That(w1.Task.Status, Is.EqualTo(TaskStatus.RanToCompletion), "w1 should have been triggered because key1 was changed");
			Assert.That(w2.Task.Status, Is.EqualTo(TaskStatus.WaitingForActivation), "w2 should still be pending because key2 was untouched");

			// cancelling the token associated to the watch should cancel them
			cts.Cancel();

			await Task.Delay(100, this.Cancellation);
			Assert.That(w2.Task.Status, Is.EqualTo(TaskStatus.Canceled), "w2 should have been cancelled");
		}

		[Test]
		public async Task Test_Cannot_Use_Transaction_CancellationToken_With_Watch()
		{
			// tr.Watch(..., tr.Cancellation) is forbidden, because the watch would not survive the transaction

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);
				var key = subspace.Key("watched");

				Assert.That(() => tr.Watch(key, tr.Cancellation), Throws.Exception, "Watch(...) should reject the transaction's own cancellation");

				// should accept the same token used for the retry loop
				var w = tr.Watch(key, this.Cancellation);
				Assert.That(w, Is.Not.Null);
				w.Cancel();

				// should accept CancellationToken.None
				w = tr.Watch(key, this.Cancellation);
				Assert.That(w, Is.Not.Null);
				w.Cancel();

				// should accept some other cancellation token
				using (var cts = new CancellationTokenSource())
				{
					w = tr.Watch(key, cts.Token);
					Assert.That(w, Is.Not.Null);
					w.Cancel();
				}
			}
		}

		[Test]
		public async Task Test_Setting_Key_To_Same_Value_Should_Not_Trigger_Watch()
		{
			RequireCleanWatches(); // clean watches: this test asserts exact fire/pending semantics
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			Log("Set to initial value...");
			await db.WriteAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("watched"), Text("initial value"));
			}, this.Cancellation);

			Log("Create watch...");
			var w = await db.ReadWriteAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				return tr.Watch(subspace.Key("watched"), this.Cancellation);
			}, this.Cancellation);
			Assert.That(w.IsAlive, Is.True, "Watch should still be alive");
			Assert.That(w.Task.Status, Is.EqualTo(TaskStatus.WaitingForActivation));

			// change the key to the same value
			Log("Set to same value...");
			await db.WriteAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("watched"), Text("initial value"));
			}, this.Cancellation);

			//note: it is difficult to verify something "that should never happen"
			// let's say that 1sec is a good approximation of an infinite time
			Log("Watch should not fire");
			await Task.WhenAny(w.Task, Task.Delay(1_000, this.Cancellation));
			Assert.That(w.IsAlive, Is.True, "Watch should still be active");
			Assert.That(w.Task.Status, Is.EqualTo(TaskStatus.WaitingForActivation));

			// now really change the value
			Log("Set to a different value...");
			await db.WriteAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("watched"), Text("new value"));
			}, this.Cancellation);

			Log("Watch should fire...");
			await Task.WhenAny(w.Task, Task.Delay(1_000, this.Cancellation));
			if (!w.Task.IsCompleted)
			{
				Assert.That(w.Task.Status, Is.EqualTo(TaskStatus.RanToCompletion), "Watch should have fired by now!");
			}
			else
			{
				await w;
			}
		}

		[Test]
		public async Task Test_Watched_Key_Changed_By_Same_Transaction_Before_Commit_Should_Trigger_Watch()
		{
			RequireCleanWatches(); // clean watches: this test asserts exact fire/pending semantics

			// Steps:
			// - T1: set a watch on a key, but does not commit yet
			// - T1: change the value of the watched key
			// - T1: commit
			// Expect:
			// - Watch should fire as soon as T1 commits

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			Log("Set to initial value...");
			await db.WriteAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("watched"), Text("initial value"));
			}, this.Cancellation);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			using var tr = db.BeginTransaction(this.Cancellation);

			var subspace = await db.Root.Resolve(tr);

			Log("T1: Create watch");
			var w = tr.Watch(subspace.Key("watched"), this.Cancellation);

			Log("T1: Update watched key");
			tr.Set(subspace.Key("watched"), Text("new value"));

			Log("T1: Commit");
			await tr.CommitAsync();

			Log("Watch should fire...");
			await Task.WhenAny(w.Task, Task.Delay(1_000, this.Cancellation));
			if (!w.Task.IsCompleted)
			{
				Assert.That(w.Task.Status, Is.EqualTo(TaskStatus.RanToCompletion), "Watch should have fired by now!");
			}
			else
			{
				await w;
			}
		}
		
		[Test]
		public async Task Test_Concurrent_Change_To_Watched_Key_Before_Commit_Should_Still_Trigger_Watch()
		{
			RequireCleanWatches(); // clean watches: this test asserts exact fire/pending semantics

			// Steps:
			// - T1: set a watch on a key, but do not commit yet
			// - T2: update the watched key and commit before T1
			// - T1: commit after T2
			// Expect:
			// - Watch should fire as soon as T1 commits

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			Log("Set to initial value...");
			await db.WriteAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("watched"), Text("initial value"));
			}, this.Cancellation);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			using var tr1 = db.BeginTransaction(this.Cancellation);

			var subspace1 = await db.Root.Resolve(tr1);

			Log("T1: Create watch");
			var w = tr1.Watch(subspace1.Key("watched"), this.Cancellation);

			// T2: change the key to the same value
			using(var tr2 = db.BeginTransaction(this.Cancellation))
			{
				var subspace2 = await db.Root.Resolve(tr2);
				Log("T2: Update watched key");
				tr2.Set(subspace2.Key("watched"), Text("new value"));
				Log("T2: commit");
				await tr2.CommitAsync();
			}

			Log("T1: Commit");
			await tr1.CommitAsync();

			Log("Watch should fire...");
			await Task.WhenAny(w.Task, Task.Delay(1_000, this.Cancellation));
			if (!w.Task.IsCompleted)
			{
				Assert.That(w.Task.Status, Is.EqualTo(TaskStatus.RanToCompletion), "Watch should have fired by now!");
			}
			else
			{
				await w;
			}
		}

		[Test]
		public async Task Test_Can_Cancel_Awaited_Watch_With_CancellationToken()
		{
			// Test that calling watch.WaitAsync(CancellationToken) will throw if the token is triggered before the watch fires

			RequireCleanWatches(); // clean watches: this test asserts the watch stays pending until cancelled
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			await db.WriteAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("watched"), Text("some value"));
			}, this.Cancellation);

			// set up the watch
			FdbWatch watch;
			// we want to be able to abort this specific call
			using var killSwitch = CancellationTokenSource.CreateLinkedTokenSource(this.Cancellation);

			Log("Setup a watch on a key that will not be changed...");
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);
				watch = tr.Watch(subspace.Key("watched"), this.Cancellation);
				Assert.That(watch, Is.Not.Null);

				// note: Watches will get cancelled if the transaction is not committed !
				await tr.CommitAsync();
			}
			Assert.That(watch.Task.Status, Is.EqualTo(TaskStatus.WaitingForActivation), "watch should still be active");

			// call WaitAsync(...), but does not await it yet
			var t = watch.WaitAsync(killSwitch.Token); // pass the token from the kill switch (that has not triggered yet)

			// wait a bit, nothing should happen, the watch should still be pending
			await Task.Delay(100, this.Cancellation);
			Assert.That(watch.Task.Status, Is.EqualTo(TaskStatus.WaitingForActivation), "watch should still be active");

			// trigger the kill switch
			Log("Triggering the cancellation token...");
			killSwitch.Cancel();

			// the task should before failed
			Log("Waiting for watch to abort...");
			Assert.That(async () => await t.WaitAsync(TimeSpan.FromSeconds(5), this.Cancellation), Throws.InstanceOf<TaskCanceledException>());
			Log("Watch was aborted!");
			Assert.That(watch.Task.Status, Is.EqualTo(TaskStatus.Canceled), "watch should have been cancelled");
		}

		[Test]
		public async Task Test_Can_Cancel_Awaited_Watch_After_Timeout()
		{
			// Test that calling watch.WaitAsync(TimeSpan, CancellationToken) will throw if the timeout expires before the watch fires

			RequireCleanWatches(); // clean watches: this test asserts the watch stays pending until the timeout
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			await db.WriteAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("watched"), Text("some value"));
			}, this.Cancellation);

			// configure the watch
			FdbWatch watch;

			Log("Setup a watch on a key that will not be changed...");
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);
				watch = tr.Watch(subspace.Key("watched"), this.Cancellation);
				Assert.That(watch, Is.Not.Null);

				// note: Watches will get cancelled if the transaction is not committed !
				await tr.CommitAsync();
			}
			Assert.That(watch.Task.Status, Is.EqualTo(TaskStatus.WaitingForActivation), "watch should still be active");

			// call WaitAsync(...), but does not await it yet
			var sw = Stopwatch.StartNew();
			var t = watch.WaitAsync(TimeSpan.FromMilliseconds(500), this.Cancellation); // pass the token from the kill switch (that has not triggered yet)

			// the task should before failed
			Log("Waiting for watch to complete...");
			Assert.That(async () => await t.WaitAsync(TimeSpan.FromSeconds(2), this.Cancellation), Throws.Nothing, "Watch task should not have failed");
			sw.Stop();

			Log($"Task returned: {t.Result} in {sw.Elapsed.TotalMilliseconds:N1} ms");
			Assert.That(t.Result, Is.False, "Task should have returned 'false' (for timeout)");
			//note: since the test runner could lag, we use a rather large tolerance for the actual delay...
			Assert.That(sw.Elapsed, Is.GreaterThan(TimeSpan.FromMilliseconds(0.4)).And.LessThan(TimeSpan.FromSeconds(1)), "Timeout should have been approximately ~500ms");
		}

		[Test]
		[CoversCells("introspection/addresses")]
		public async Task Test_Can_Get_Addresses_For_Key()
		{
			//note: starting from API level 630, options IncludePortInAddress is the default!

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key(1), Text("one"));
			}, this.Cancellation);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			// look for the address of key1
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				var addresses = await tr.GetAddressesForKeyAsync(subspace.Key(1));
				Assert.That(addresses, Is.Not.Null);
				Log($"{subspace.Key(1)} is stored at: {string.Join(", ", addresses)}");
				Assert.That(addresses.Length, Is.GreaterThan(0));
				Assert.That(addresses[0], Is.Not.Null.Or.Empty);

				//note: it is difficult to test the returned value, because it depends on the test db configuration
				// it will most probably be 127.0.0.1 unless you have customized the Test DB settings to point to somewhere else
				// either way, it should look like a valid IP address (IPv4 or v6?)

				foreach (var address in addresses)
				{
					Log($"- {address}");
					// we expect "IP:PORT" or "IP:PORT:tls"
					Assert.That(address, Is.Not.Null.Or.Empty);
					Assert.That(FdbEndPoint.TryParse(address, out var ep), Is.True, $"Result address '{address}' is invalid");
					Assert.That(ep!.IsValid(), Is.True);
					Assert.That(ep.Address, Is.Not.Null);
					Assert.That(ep.Port, Is.GreaterThan(0));
				}
			}

			// do the same but for a key that does not exist
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				var addresses = await tr.GetAddressesForKeyAsync(subspace.Key(404));
				Assert.That(addresses, Is.Not.Null);
				Log($"{subspace.Key(404)} would be stored at: {string.Join(", ", addresses)}");

				// the API still return a list of addresses, probably of servers that would store this value if you would call Set(...)

				foreach (var address in addresses)
				{
					Log($"- {address}");
					// we expect "IP:PORT"
					Assert.That(address, Is.Not.Null.Or.Empty);
					Assert.That(address, Does.Contain(':'), "Result address '{0}' should contain a port number", address);
					int p = address.IndexOf(':');
					Assert.That(System.Net.IPAddress.TryParse(address.AsSpan(0, p), out _), Is.True, "Result address '{0}' does not seem to have a valid IP address '{1}'", address, address.Substring(0, p));
					Assert.That(int.TryParse(address.AsSpan(p + 1), out _), Is.True, "Result address '{0}' does not seem to have a valid port number '{1}'", address, address[(p + 1)..]);
				}

			}
		}

		[Test]
		public async Task Test_Can_Get_Boundary_Keys()
		{
			using var db = await OpenTestDatabaseAsync();

			using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
			{
				tr.Options.WithReadAccessToSystemKeys();
				// dump nodes
				Log("Server List:");
				var servers = await tr.GetRange(Fdb.System.ServerList, Fdb.System.ServerList + Fdb.System.MaxValue)
					.Select(kvp => new KeyValuePair<Slice, Slice>(kvp.Key.Substring(Fdb.System.ServerList.Count), kvp.Value))
					.ToListAsync();
				foreach (var key in servers)
				{
					// the node id seems to be at offset 8
					var nodeId = key.Value.Substring(8, 16).ToHexString();
					// the machine id seems to be at offset 24
					var machineId = key.Value.Substring(24, 16).ToHexString();
					// the datacenter id seems to be at offset 40
					var dataCenterId = key.Value.Substring(40, 16).ToHexString();

					Log($"- {key.Key:X} : ({key.Value.Count}) {key.Value:P}");
					Log($"  > node       = {nodeId}");
					Log($"  > machine    = {machineId}");
					Log($"  > datacenter = {dataCenterId}");
				}

				Log();

				// dump keyServers
				var shards = await tr.GetRange(Fdb.System.KeyServers, Fdb.System.KeyServers + Fdb.System.MaxValue)
					.Select(kvp => new KeyValuePair<Slice, Slice>(kvp.Key.Substring(Fdb.System.KeyServers.Count), kvp.Value))
					.ToListAsync();
				Log($"Key Servers: {shards.Count} shard(s)");

				var distinctNodes = new HashSet<string>(StringComparer.Ordinal);
				int replicationFactor = 0;
				string[]? ids = null;
				foreach (var key in shards)
				{
					// - the first 12 bytes are some sort of header:
					//		- bytes 0-5 usually are 01 00 01 10 A2 00
					//		- bytes 6-7 contains 0x0FDB which is the product's signature
					//		- bytes 8-9 contains the version (02 00 for "2.0"?)
					// - they are followed by k x 16-bytes machine id where k is the replication factor of the cluster
					// - followed by 4 bytes (usually all zeroes)
					// Size should be 16 x (k + 1) bytes

					int n = (key.Value.Count - 16) >> 4;
					if (ids == null || ids.Length != n) ids = new string[n];
					for (int i = 0; i < n; i++)
					{
						ids[i] = key.Value.Substring(12 + i * 16, 16).ToHexString();
						distinctNodes.Add(ids[i]);
					}

					replicationFactor = Math.Max(replicationFactor, ids.Length);

					// the node id seems to be at offset 12

					//Log("- " + key.Value.Substring(0, 12).ToAsciiOrHexaString() + " : " + String.Join(", ", ids) + " = " + key.Key);
				}

				Log();
				Log($"Distinct nodes: {distinctNodes.Count}");
				foreach (var machine in distinctNodes)
				{
					Log("- " + machine);
				}

				Log();
				Log($"Cluster topology: {distinctNodes.Count} process(es) with {(replicationFactor == 1 ? "single" : replicationFactor == 2 ? "double" : replicationFactor == 3 ? "triple" : replicationFactor.ToString())} replication");
			}
		}

		[Test]
		public async Task Test_VersionStamps_Share_The_Same_Token_Per_Transaction_Attempt()
		{
			// Verify that we can set version-stamped keys inside a transaction

			using var db = await OpenTestDatabaseAsync();
			using var tr = db.BeginTransaction(this.Cancellation);

			// should return an 80-bit incomplete stamp, using a random token
			var x = tr.CreateVersionStamp();
			Log($"> x  : {x.ToSlice():X} => {x}");
			Assert.That(x.IsIncomplete, Is.True, "Placeholder token should be incomplete");
			Assert.That(x.HasUserVersion, Is.False);
			Assert.That(x.UserVersion, Is.Zero);
			Assert.That(x.TransactionVersion >> 56, Is.EqualTo(0xFF), "Highest 8 bit of Transaction Version should be set to 1");
			Assert.That(x.TransactionOrder >> 12, Is.EqualTo(0xF), "Highest 4 bits of Transaction Order should be set to 1");

			// should return a 96-bit incomplete stamp, using the same random token and user version 0
			var x0 = tr.CreateVersionStamp(0);
			Log($"> x0 : {x0.ToSlice():X} => {x0}");
			Assert.That(x0.IsIncomplete, Is.True, "Placeholder token should be incomplete");
			Assert.That(x0.TransactionVersion, Is.EqualTo(x.TransactionVersion), "All generated stamps by one transaction should share the random token value ");
			Assert.That(x0.TransactionOrder, Is.EqualTo(x.TransactionOrder), "All generated stamps by one transaction should share the random token value ");
			Assert.That(x0.HasUserVersion, Is.True);
			Assert.That(x0.UserVersion, Is.EqualTo(0));

			// should return a 96-bit incomplete stamp, using the same random token and user version 1
			var x1 = tr.CreateVersionStamp(1);
			Log($"> x1 : {x1.ToSlice():X} => {x1}");
			Assert.That(x1.IsIncomplete, Is.True, "Placeholder token should be incomplete");
			Assert.That(x1.TransactionVersion, Is.EqualTo(x.TransactionVersion), "All generated stamps by one transaction should share the random token value ");
			Assert.That(x1.TransactionOrder, Is.EqualTo(x.TransactionOrder), "All generated stamps by one transaction should share the random token value ");
			Assert.That(x1.HasUserVersion, Is.True);
			Assert.That(x1.UserVersion, Is.EqualTo(1));

			// should return a 96-bit incomplete stamp, using the same random token and user version 42
			var x42 = tr.CreateVersionStamp(42);
			Log($"> x42: {x42.ToSlice():X} => {x42}");
			Assert.That(x42.IsIncomplete, Is.True, "Placeholder token should be incomplete");
			Assert.That(x42.TransactionVersion, Is.EqualTo(x.TransactionVersion), "All generated stamps by one transaction should share the random token value ");
			Assert.That(x42.TransactionOrder, Is.EqualTo(x.TransactionOrder), "All generated stamps by one transaction should share the random token value ");
			Assert.That(x42.HasUserVersion, Is.True);
			Assert.That(x42.UserVersion, Is.EqualTo(42));

			// Reset the transaction
			// => stamps should use a new value
			Log("Reset!");
			tr.Reset();

			var y = tr.CreateVersionStamp();
			Log($"> y  : {y.ToSlice():X} => {y}'");
			Assert.That(y, Is.Not.EqualTo(x), "VersionStamps should change when a transaction is reset");

			Assert.That(y.IsIncomplete, Is.True, "Placeholder token should be incomplete");
			Assert.That(y.HasUserVersion, Is.False);
			Assert.That(y.UserVersion, Is.Zero);
			Assert.That(y.TransactionVersion >> 56, Is.EqualTo(0xFF), "Highest 8 bit of Transaction Version should be set to 1");
			Assert.That(y.TransactionOrder >> 12, Is.EqualTo(0xF), "Highest 4 bits of Transaction Order should be set to 1");

			var y42 = tr.CreateVersionStamp(42);
			Log($"> y42: {y42.ToSlice():X} => {y42}");
			Assert.That(y42.IsIncomplete, Is.True, "Placeholder token should be incomplete");
			Assert.That(y42.TransactionVersion, Is.EqualTo(y.TransactionVersion), "All generated stamps by one transaction should share the random token value ");
			Assert.That(y42.TransactionOrder, Is.EqualTo(y.TransactionOrder), "All generated stamps by one transaction should share the random token value ");
			Assert.That(y42.HasUserVersion, Is.True);
			Assert.That(y42.UserVersion, Is.EqualTo(42));
		}

		[Test]
		public async Task Test_VersionStamp_Operations()
		{
			// Verify that we can set version-stamped keys inside a transaction

			using var db = await OpenTestPartitionAsync();

			await CleanLocation(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			VersionStamp vsActual; // will contain the actual version stamp used by the database

			Log("Inserting keys with version stamps:");
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);

				// should return an 80-bit incomplete stamp, using a random token
				var vs = tr.CreateVersionStamp();
				Log($"> placeholder stamp: {vs} with token '{vs.ToSlice():X}'");

				// a single key using the 80-bit stamp
				tr.SetVersionStampedKey(subspace.Key("foo", vs, 123), Text("Hello, World!"));

				// simulate a batch of 3 keys, using 96-bits stamps
				tr.SetVersionStampedKey(subspace.Key("bar", tr.CreateVersionStamp(0)), Text("Zero"));
				tr.SetVersionStampedKey(subspace.Key("bar", tr.CreateVersionStamp(1)), Text("One"));
				tr.SetVersionStampedKey(subspace.Key("bar", tr.CreateVersionStamp(42)), Text("FortyTwo"));

				// value that contain the stamp
				var val = Slice.FromString("$$$$$$$$$$Hello World!"); // '$' will be replaced by the stamp
				Log($"> {val:X}");
				tr.SetVersionStampedValue(subspace.Key("baz"), val, 0);

				val = Slice.FromString("Hello,") + vs.ToSlice() + Slice.FromString(", World!"); // the middle of the value should be replaced with the VersionStamp
				Log($"> {val:X}");
				tr.SetVersionStampedValue(subspace.Key("jazz"), val);

				// need to be request BEFORE the commit
				var vsTask = tr.GetVersionStampAsync();

				await tr.CommitAsync();
				Dump(tr.GetCommittedVersion());

				// need to be resolved AFTER the commit
				vsActual = await vsTask;
				Log($"> actual stamp: {vsActual} with token '{vsActual.ToSlice():X}'");
			}

			await DumpSubspace(db);

			Log("Checking database content:");
			using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);
				{
					var foo = await tr.GetRange(subspace.Key("foo").ToRange()).SingleAsync();
					Log("> Found 1 result under (foo,)");
					Log($"- {subspace.ExtractKey(foo.Key):K} = {foo.Value:V}");
					Assert.That(foo.Value.ToString(), Is.EqualTo("Hello, World!"));

					var t = subspace.Unpack(foo.Key);
					Assert.That(t.Get<string>(0), Is.EqualTo("foo"));
					Assert.That(t.Get<int>(2), Is.EqualTo(123));

					var vs = t.Get<VersionStamp>(1);
					Assert.That(vs.IsIncomplete, Is.False);
					Assert.That(vs.HasUserVersion, Is.False);
					Assert.That(vs.UserVersion, Is.Zero);
					Assert.That(vs.TransactionVersion, Is.EqualTo(vsActual.TransactionVersion));
					Assert.That(vs.TransactionOrder, Is.EqualTo(vsActual.TransactionOrder));
				}

				{
					var items = await tr.GetRange(subspace.Key("bar").ToRange()).ToListAsync();
					Log($"> Found {items.Count} results under (bar,)");
					foreach (var item in items)
					{
						Log($"- {subspace.ExtractKey(item.Key):K} = {item.Value:V}");
					}

					Assert.That(items.Count, Is.EqualTo(3), "Should have found 3 keys under 'foo'");

					Assert.That(items[0].Value.ToString(), Is.EqualTo("Zero"));
					var vs0 = subspace.DecodeLast<VersionStamp>(items[0].Key);
					Assert.That(vs0.IsIncomplete, Is.False);
					Assert.That(vs0.HasUserVersion, Is.True);
					Assert.That(vs0.UserVersion, Is.EqualTo(0));
					Assert.That(vs0.TransactionVersion, Is.EqualTo(vsActual.TransactionVersion));
					Assert.That(vs0.TransactionOrder, Is.EqualTo(vsActual.TransactionOrder));

					Assert.That(items[1].Value.ToString(), Is.EqualTo("One"));
					var vs1 = subspace.DecodeLast<VersionStamp>(items[1].Key);
					Assert.That(vs1.IsIncomplete, Is.False);
					Assert.That(vs1.HasUserVersion, Is.True);
					Assert.That(vs1.UserVersion, Is.EqualTo(1));
					Assert.That(vs1.TransactionVersion, Is.EqualTo(vsActual.TransactionVersion));
					Assert.That(vs1.TransactionOrder, Is.EqualTo(vsActual.TransactionOrder));

					Assert.That(items[2].Value.ToString(), Is.EqualTo("FortyTwo"));
					var vs42 = subspace.DecodeLast<VersionStamp>(items[2].Key);
					Assert.That(vs42.IsIncomplete, Is.False);
					Assert.That(vs42.HasUserVersion, Is.True);
					Assert.That(vs42.UserVersion, Is.EqualTo(42));
					Assert.That(vs42.TransactionVersion, Is.EqualTo(vsActual.TransactionVersion));
					Assert.That(vs42.TransactionOrder, Is.EqualTo(vsActual.TransactionOrder));
				}

				{
					var baz = await tr.GetAsync(subspace.Key("baz"));
					Log($"> {baz:X}");
					// ensure that the first 10 bytes have been overwritten with the stamp
					Assert.That(baz.Count, Is.GreaterThan(0), "Key should be present in the database");
					Assert.That(baz.StartsWith(vsActual.ToSlice()), Is.True, "The first 10 bytes should match the resolved stamp");
					Assert.That(baz.Substring(10), Is.EqualTo(Slice.FromString("Hello World!")), "The rest of the slice should be untouched");
				}
				{
					var jazz = await tr.GetAsync(subspace.Key("jazz"));
					Log($"> {jazz:X}");
					// ensure that the first 10 bytes have been overwritten with the stamp
					Assert.That(jazz.Count, Is.GreaterThan(0), "Key should be present in the database");
					Assert.That(jazz.Substring(6, 10), Is.EqualTo(vsActual.ToSlice()), "The bytes 6 to 15 should match the resolved stamp");
					Assert.That(jazz.Substring(0, 6), Is.EqualTo(Slice.FromString("Hello,")), "The start of the slice should be left intact");
					Assert.That(jazz.Substring(16), Is.EqualTo(Slice.FromString(", World!")), "The end of the slice should be left intact");
				}
			}
		}

		[Test]
		public async Task Test_GetMetadataVersion()
		{
			//note: this test may be vulnerable to exterior changes to the database!
			using var db = await OpenTestDatabaseAsync();

			// reading the mv twice in _should_ return the same value, unless the test cluster is used by another application!

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			var version1 = await db.ReadAsync(tr => tr.GetMetadataVersionKeyAsync(), this.Cancellation);
			Assert.That(version1, Is.Not.Null, "Version should be valid");
			Log($"Version1: {version1}");

			var version2 = await db.ReadAsync(tr => tr.GetMetadataVersionKeyAsync(), this.Cancellation);
			Assert.That(version1, Is.Not.Null, "Version should be valid");
			Log($"Version2: {version2}");

			Assume.That(version2, Is.EqualTo(version1), "Metadata version should be stable! Make sure the test cluster is not used concurrently when running this test!");
			// if it fails randomly here, maybe due to another process interfering with us!

			Log("Changing version...");
			await db.WriteAsync(tr => tr.TouchMetadataVersionKey(), this.Cancellation);

			var version3 = await db.ReadAsync(tr => tr.GetMetadataVersionKeyAsync(), this.Cancellation);
			Log($"Version3: {version3}");
			Assert.That(version3, Is.Not.Null.And.Not.EqualTo(version2), "Metadata version should have changed");

			// changing the metadata version and then reading it back from the same transaction should return <null>
			await db.WriteAsync(async tr =>
			{
				// We can read the version before
				var before = await tr.GetMetadataVersionKeyAsync();
				Log($"Before: {before}");
				Assert.That(before, Is.Not.Null);

				// Another read attempt should return the cached value
				var cached = await tr.GetMetadataVersionKeyAsync();
				Log($"Cached: {before}");
				Assert.That(cached, Is.Not.Null.And.EqualTo(before));

				// change the version from inside the transaction
				Log("Mutate!");
				tr.TouchMetadataVersionKey();

				// we should not be able to get the version anymore (should return null)
				var after = await tr.GetMetadataVersionKeyAsync();
				Log($"After: {after}");
				Assert.That(after, Is.Null, "Should not be able to get the version right after changing it from the same transaction.");

			}, this.Cancellation);
		}

		[Test]
		public async Task Test_GetMetadataVersion_Custom_Keys()
		{
			using var db = await OpenTestDatabaseAsync();
			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			const string FOO = "Foo";
			const string BAR = "Bar";
			const string BAZ = "Baz";

			// initial setup:
			// - Foo: version stamp
			// - Bar: different version stamp
			// - Baz: _missing_

			await db.WriteAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.TouchMetadataVersionKey(subspace.Key(FOO));
			}, this.Cancellation);
			await db.WriteAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.TouchMetadataVersionKey(subspace.Key(BAR));
			}, this.Cancellation);
			await db.WriteAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Clear(subspace.Key(BAZ));
			}, this.Cancellation);

			// changing the metadata version and then reading it back from the same transaction CANNOT WORK!
			await db.WriteAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);

				// We can read the version before
				var before1 = await tr.GetMetadataVersionKeyAsync(subspace.Key(FOO));
				Log($"Foo (before): {before1}");
				Assert.That(before1, Is.Not.Null);

				// Another read attempt should return the cached value
				var before2 = await tr.GetMetadataVersionKeyAsync(subspace.Key(BAR));
				Log($"Bar (before): {before2}");
				Assert.That(before2, Is.Not.Null.And.Not.EqualTo(before1));

				// Another read attempt should return the cached value
				var before3 = await tr.GetMetadataVersionKeyAsync(subspace.Key(BAZ));
				Log($"Baz (before): {before3}");
				Assert.That(before3, Is.EqualTo(new VersionStamp()));

				// change the version from inside the transaction
				Log("Mutate Foo!");
				tr.TouchMetadataVersionKey(subspace.Key(FOO));

				// we should not be able to get the version anymore (should return null)
				var after1 = await tr.GetMetadataVersionKeyAsync(subspace.Key(FOO));
				Log($"Foo (after): {after1}");
				Assert.That(after1, Is.Null, "Should not be able to get the version right after changing it from the same transaction.");

				// We can read the version before
				var after2 = await tr.GetMetadataVersionKeyAsync(subspace.Key(BAR));
				Log($"Bar (after): {after2}");
				Assert.That(after2, Is.Not.Null.And.EqualTo(before2));

				// We can read the version before
				var after3 = await tr.GetMetadataVersionKeyAsync(subspace.Key(BAZ));
				Log($"Baz (after): {after3}");
				Assert.That(after3, Is.EqualTo(new VersionStamp()));

			}, this.Cancellation);
		}

		[Test, Category("LongRunning")][Ignore("Takes too much time!")]
		public async Task Test_VeryBadPractice_Future_Fuzzer()
		{
#if DEBUG
			const int DURATION_SEC = 5;
#else
			const int DURATION_SEC = 20;
#endif
			const int R = 100;

			using var db = await OpenTestDatabaseAsync();
			var rnd = new Random();
			int seed = rnd.Next();
			Log($"Using random seed {seed}");
			rnd = new Random(seed);

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				for (int i = 0; i < R; i++)
				{
					tr.Set(subspace.Key("Fuzzer", i), Slice.FromInt32(i));
				}
			}, this.Cancellation);

			var start = DateTime.UtcNow;
			Log($"This test will run for {DURATION_SEC} seconds");

			int time = 0;

			var alive = new List<IFdbTransaction>();
			var sb = new StringBuilder();

			while (DateTime.UtcNow - start < TimeSpan.FromSeconds(DURATION_SEC))
			{
				switch (rnd.Next(10))
				{
					case 0:
					{ // start a new transaction
						sb.Append('T');
						var tr = db.BeginTransaction(FdbTransactionMode.Default, this.Cancellation);
						alive.Add(tr);

						break;
					}
					case 1:
					{ // drop a random transaction
						if (alive.Count == 0) continue;
						sb.Append('L');
						int p = rnd.Next(alive.Count);

						alive.RemoveAt(p);
						//no dispose
						break;
					}
					case 2:
					{ // dispose a random transaction
						if (alive.Count == 0) continue;
						sb.Append('D');
						int p = rnd.Next(alive.Count);

						var tr = alive[p];
						tr.Dispose();
						alive.RemoveAt(p);
						break;
					}
					case 3:
					{ // GC!
						sb.Append('C');
						var tr = db.BeginTransaction(FdbTransactionMode.ReadOnly, this.Cancellation);
						alive.Add(tr);
						_ = await tr.GetReadVersionAsync();
						break;
					}

					case 4:
					case 5:
					case 6:
					{ // read a random value from a random transaction
						sb.Append('G');
						if (alive.Count == 0) break;
						int p = rnd.Next(alive.Count);
						var tr = alive[p];

						int x = rnd.Next(R);
						try
						{
							var subspace = await db.Root.Resolve(tr); //TODO: cache subspace instance alongside transaction?
							_ = await tr.GetAsync(subspace.Key("Fuzzer", x));
						}
						catch (FdbException)
						{
							sb.Append('!');
						}

						break;
					}
					case 7:
					case 8:
					case 9:
					{ // read a random value, but drop the task
						sb.Append('g');
						if (alive.Count == 0) break;
						int p = rnd.Next(alive.Count);
						var tr = alive[p];

						int x = rnd.Next(R);
						var subspace = await db.Root.Resolve(tr); //TODO: cache subspace instance alongside transaction?
						_ = tr.GetAsync(subspace.Key("Fuzzer", x)).ContinueWith((_) => sb.Append('!') /*BUGBUG: locking ?*/, TaskContinuationOptions.NotOnRanToCompletion);
						// => t is not stored
						break;
					}

				}

				if ((time++) % 80 == 0)
				{
					Log(sb.ToString());
					Log($"State: {alive.Count}");
					sb.Clear();
					sb.Append('C');
					GC.Collect();
					GC.WaitForPendingFinalizers();
					GC.Collect();
				}

			}

			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
		}

		[Test]
		public async Task Test_Value_Checks()
		{
			// Verify that value-check perform as expected:
			// - We have a set of keys that are used for value checks (AAA, BBB, ...)
			// - We have a "witness" key that will be used to verify if the transaction actually committed or not.
			// - On each retry of the retry-loop, we will check that the previous iteration did update the context state as it should have.

			//NOTE: this test is vulnerable to transient errors that could happen to the cluster while it runs! (timeouts, etc...)
			//TODO: we should use a more robust way to "skip" the retries that are for unrelated reasons?

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			var initialA = Slice.FromStringAscii("Initial value of AAA");
			var initialB = Slice.FromStringAscii("Initial value of BBB");

			async Task RunCheck(Func<IFdbTransaction, bool> test, Func<IFdbTransaction, IKeySubspace, Task> handler, bool shouldCommit)
			{
				// read previous witness value
				await db.WriteAsync(async tr =>
				{
					tr.StopLogging();
					var subspace = await db.Root.Resolve(tr);

					tr.ClearRange(subspace.ToRange());
					tr.Set(subspace.Key("AAA"), initialA);
					tr.Set(subspace.Key("BBB"), initialB);
					// CCC does not exist
					tr.Set(subspace.Key("Witness"), Slice.FromStringAscii("Initial witness value"));
				}, this.Cancellation);

				await db.WriteAsync(async tr =>
				{
					var checks = tr.Context.GetValueChecksFromPreviousAttempt(result: FdbValueCheckResult.Failed);
					Log($"- Retry #{tr.Context.Retries}: prev={tr.Context.PreviousError}, checksFromPrevious={checks.Count}");
					foreach (var check in checks)
					{
						Log($"  > [{check.Tag}]: {check.Result}, {FdbKey.Dump(check.Key)} => {check.Expected:V} vs {check.Actual:V}");
					}
					if (tr.Context.Retries > 10) Assert.Fail("Too many retries!");

					if (!test(tr)) return;

					var subspace = await db.Root.Resolve(tr);
					await handler(tr, subspace);
					tr.Set(subspace.Key("Witness"), FdbValue.ToTextUtf8("New witness value"));
				}, this.Cancellation);
				await DumpSubspace(db);

				// read back the witness key to see if commit happened or not.
				var actual = await db.ReadAsync(async tr =>
				{
					tr.StopLogging();
					var subspace = await db.Root.Resolve(tr);
					return await tr.GetAsync(subspace.Key("Witness"));
				}, this.Cancellation);

				if (shouldCommit)
				{
					Assert.That(actual, Is.EqualTo(Slice.FromStringAscii("New witness value")), "Transaction SHOULD have changed the database!");
				}
				else
				{
					Assert.That(actual, Is.EqualTo(Slice.FromStringAscii("Initial witness value")), "Transaction should NOT have changed the database!");
				}
			}

			// Checking a key with its actual value should pass
			{
				Log("Value check for AAA == CORRECT => expect PASS...");
				await RunCheck(
					(tr) =>
					{
						if (tr.Context.Retries == 0)
						{ // first attempt: all should be default
							Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Unknown));
							Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Is.Empty);
							return true;
						}
						else
						{ // we don't expect any retries
							Assert.Fail("Should not execute more than once!");
							return false;
						}
					},
					(tr, subspace) =>
					{
						tr.Context.AddValueCheck("fooCheck", subspace.Key("AAA"), initialA);
						return Task.CompletedTask;
					},
					shouldCommit: true
				);
			}

			// Checking a missing key with nil should pass
			{
				Log("Value check for CCC == Nil => expect PASS...");
				await RunCheck(
					(tr) =>
					{
						if (tr.Context.Retries == 0)
						{ // first attempt: all should be default
							Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Unknown));
							Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Is.Empty);
							return true;
						}
						else
						{ // we don't expect any retries
							Assert.Fail("Should not execute more than once!");
							return false;
						}
					},
					(tr, subspace) =>
					{
						tr.Context.AddValueCheck("fooCheck", subspace.Key("CCC"), Slice.Nil);
						return Task.CompletedTask;
					},
					shouldCommit: true
				);
			}

			// Checking a multiple keys should pass
			{
				Log("Value check for (AAA == CORRECT) & (BBB == CORRECT) & (CCC == nil) => expect PASS...");
				await RunCheck(
					(tr) =>
					{
						if (tr.Context.Retries == 0)
						{ // first attepmpt: all should be default
							Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Unknown));
							Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Is.Empty);
							Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("barCheck"), Is.EqualTo(FdbValueCheckResult.Unknown));
							Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("barCheck"), Is.Empty);
							return true;
						}
						else
						{ // we don't expect any retries
							Assert.Fail("Should not execute more than once!");
							return false;
						}
					},
					(tr, subspace) =>
					{
						tr.Context.AddValueCheck("fooCheck", subspace.Key("AAA"), initialA);
						tr.Context.AddValueCheck("barCheck", subspace.Key("BBB"), initialB);
						return Task.CompletedTask;
					},
					shouldCommit: true
				);
			}

			// Checking a key with a different value should fail
			{
				Log("Value check BBB == INCORRECT => expect FAIL...");
				await RunCheck(
					(tr) =>
					{
						switch (tr.Context.Retries)
						{
							case 0:
								// on first attempt, everything should be default
								Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Unknown));
								Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Is.Empty);
								return true;
							case 1:
								// on second attempt, value-check "fooCheck" should be triggered
								Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Failed));
								Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Has.Count.EqualTo(1));
								Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck")[0].Result, Is.EqualTo(FdbValueCheckResult.Failed));
								Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("unrelated"), Is.EqualTo(FdbValueCheckResult.Unknown));
								Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("unrelated"), Is.Empty);
								Assert.That(tr.Context.PreviousError, Is.EqualTo(FdbError.NotCommitted), "Should emulate a 'not_committed'");
								return false; // stop
							default:
								Assert.Fail("Should not execute more than twice!");
								return false;
						}
					},
					(tr, subspace) =>
					{
						tr.Context.AddValueCheck("fooCheck", subspace.Key("AAA"), Slice.FromStringAscii("Different value of AAA"));
						return Task.CompletedTask;
					},
					shouldCommit: false
				);
			}

			// Checking a missing key with a value should fail
			{
				Log("Value check CCC == SOMETHING => expect FAIL...");
				await RunCheck(
					(tr) =>
					{
						switch (tr.Context.Retries)
						{
							case 0:
								// on first attempt, everything should be default
								Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Unknown));
								Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Is.Empty);
								return true;
							case 1:
								// on second attempt, value-check "fooCheck" should be triggered
								Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Failed));
								Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Has.Count.EqualTo(1));
								Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck")[0].Result, Is.EqualTo(FdbValueCheckResult.Failed));
								Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("unrelated"), Is.EqualTo(FdbValueCheckResult.Unknown));
								Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("unrelated"), Is.Empty);
								Assert.That(tr.Context.PreviousError, Is.EqualTo(FdbError.NotCommitted), "Should emulate a 'not_committed'");
								return false; // stop
							default:
								Assert.Fail("Should not execute more than twice!");
								return false;
						}
					},
					(tr, subspace) =>
					{
						tr.Context.AddValueCheck("fooCheck", subspace.Key("CCC"), FdbValue.ToTextUtf8("Some value"));
						return Task.CompletedTask;
					},
					shouldCommit: false
				);
			}

			// Changing the value after the check should not be observed by the check
			{
				Log("Value check AAA == CORRECT; Set AAA = DIFFERENT => expect PASS...");
				await RunCheck(
					(tr) =>
					{
						switch (tr.Context.Retries)
						{
							case 0:
								// on first attempt, everything should be default
								Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Unknown));
								Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Is.Empty);
								return true;
							default:
								// should not fire twice!
								Assert.Fail("Should not execute more than once!");
								return false;
						}
					},
					(tr, subspace) =>
					{
						// check
						tr.Context.AddValueCheck("fooCheck", subspace.Key("AAA"), initialA);
						// then change
						tr.Set(subspace.Key("AAA"), Slice.FromStringAscii("Different value for AAA"));
						return Task.CompletedTask;
					},
					shouldCommit: true
				);
			}

			// Clearing the key after the check should not be observed by the check
			{
				Log("Value check AAA == CORRECT; Clear AAA expect PASS...");
				await RunCheck(
					(tr) =>
					{
						switch (tr.Context.Retries)
						{
							case 0:
								// on first attempt, everything should be default
								Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Unknown));
								Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Is.Empty);
								return true;
							default:
								// should not fire twice!
								Assert.Fail("Should not execute more than once!");
								return false;
						}
					},
					(tr, subspace) =>
					{
						// check
						tr.Context.AddValueCheck("fooCheck", subspace.Key("AAA"), initialA);
						// then change
						tr.Clear(subspace.Key("AAA"));
						return Task.CompletedTask;
					},
					shouldCommit: true
				);
			}

			// Changing the value BEFORE the check should be observed by the check
			{
				Log("Set AAA = DIFFERENT; Value check AAA == CORRECT => expect FAIL...");
				await RunCheck(
					(tr) =>
					{
						switch (tr.Context.Retries)
						{
							case 0:
								// on first attempt, everything should be default
								Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Unknown));
								Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Is.Empty);
								return true;
							case 1:
								// on second attempt, value-check "fooCheck" should be triggered
								Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Failed));
								Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Has.Count.EqualTo(1));
								Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck")[0].Result, Is.EqualTo(FdbValueCheckResult.Failed));
								Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("unrelated"), Is.EqualTo(FdbValueCheckResult.Unknown));
								Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("unrelated"), Is.Empty);
								Assert.That(tr.Context.PreviousError, Is.EqualTo(FdbError.NotCommitted), "Should emulate a 'not_committed'");
								return false; // stop
							default:
								Assert.Fail("Should not execute more than twice!");
								return false;
						}
					},
					(tr, subspace) =>
					{
						// change
						tr.Set(subspace.Key("AAA"), Slice.FromStringAscii("Different value for AAA"));
						// then check
						tr.Context.AddValueCheck("fooCheck", subspace.Key("AAA"), initialA);
						return Task.CompletedTask;
					},
					shouldCommit: false
				);
			}

			// Clearing a key BEFORE the check should be observed by the check
			{
				Log("Clear AAA; Value check AAA == CORRECT => expect FAIL...");
				await RunCheck(
					(tr) =>
					{
						switch (tr.Context.Retries)
						{
							case 0:
								// on first attempt, everything should be default
								Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Unknown));
								Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Is.Empty);
								return true;
							case 1:
								// on second attempt, value-check "fooCheck" should be triggered
								Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Failed));
								Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Has.Count.EqualTo(1));
								Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck")[0].Result, Is.EqualTo(FdbValueCheckResult.Failed));
								Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("unrelated"), Is.EqualTo(FdbValueCheckResult.Unknown));
								Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("unrelated"), Is.Empty);
								Assert.That(tr.Context.PreviousError, Is.EqualTo(FdbError.NotCommitted), "Should emulate a 'not_committed'");
								return false; // stop
							default:
								Assert.Fail("Should not execute more than twice!");
								return false;
						}
					},
					(tr, subspace) =>
					{
						// change
						tr.Clear(subspace.Key("AAA"));
						// then check
						tr.Context.AddValueCheck("fooCheck", subspace.Key("AAA"), initialA);
						return Task.CompletedTask;
					},
					shouldCommit: false
				);
			}
		}

		[Test]
		public async Task Test_Value_Checks_Retries_On_Application_Exception()
		{
			// If we observe an application exception being thrown by the handler, normally we would stop the retry loop there.
			// But if there was at least one failed value-check, we HAVE to retry because it is possible that the application threw due to some invalid assumption.
			// Normally, any layer that used cached data will observe the failed check, and re-validate the cache.
			// If the application error was caused by this stale data, then it should not throw in the new attempt.
			// If the application error was caused by something completely unrelated, then it should throw again, and we should NOT retry

			using var db = await OpenTestPartitionAsync();
			db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			for (int i = 0; i < 15; i++)
			{

				if (i % 5 == 0)
				{
					Log("Clear the database...");

					await CleanLocation(db);

					// if the application code fails we have to make sure that if there was also a failed value-check, the handler retries again!

					await db.WriteAsync(async tr =>
					{
						var subspace = await db.Root.Resolve(tr);
						tr.Set(subspace.Key("Foo"), Slice.FromStringAscii("NotReady"));
						// Bar does not exist
					}, this.Cancellation);
				}

				var task = db.ReadWriteAsync(async tr =>
				{
					//note: this subspace does not use the DL so it does not introduce any value checks!
					var subspace = await db.Root.Resolve(tr);

					if (tr.Context.TestValueCheckFromPreviousAttempt("foo") == FdbValueCheckResult.Failed)
					{
						Log("# Oh, no! 'foo' check failed previously, check and initialize the db if required...");

						tr.Annotate("APP: doing the actual work to check the state of the db, and initialize the schema if required...");

						// read foo, and update the Bar key accordingly
						var foo = await tr.GetAsync(subspace.Key("Foo"));
						if (foo.ToStringAscii() == "NotReady")
						{
							tr.Annotate("APP: initializing the database!");
							Log("# Moving 'foo' from Value1 to Value2 and setting Bar...");
							tr.Set(subspace.Key("Foo"), FdbValue.ToTextUtf8("Ready"));
							tr.Set(subspace.Key("Bar"), FdbValue.ToTextUtf8("Something"));
						}
					}
					else
					{
						tr.Annotate("APP: I'm feeling lucky! Let's assume the db is already initialized");
						tr.Context.AddValueCheck("foo", subspace.Key("Foo"), FdbValue.ToTextUtf8("Ready"));
					}
					// if "Foo" was equal to "Value2", then "Bar" SHOULD exist
					// We simulate some application code reading the "Bar" value, and then finding out that it does not exist

					tr.Annotate("APP: The value of 'Bar' better not be empty...");
					var x = await tr.GetAsync(subspace.Key("Bar"));
					Log($"On attempt #{tr.Context.Retries} we found the value of Bar to be '{x}'");
					if (x.IsNull)
					{
						tr.Annotate("APP: UH OH... something's wrong! let's throw an exception!!");
						throw new InvalidOperationException("Oh noes! There is some corruption in the database!");
					}

					return x.ToStringAscii();
				}, this.Cancellation);

				Assert.That(async () => await task, Is.EqualTo("Something"));
			}
		}

		[Test]
		[CoversCells("introspection/approximate-size")]
		public async Task Test_Can_Get_Approximate_Size()
		{
			using var db = await OpenTestDatabaseAsync();

			// GET(KEY)
			Log("GET(KEY):");
			await db.ReadWriteAsync(async (tr) =>
			{
				// at the start, we expect a size of 0
				var size = await tr.GetApproximateSizeAsync();
				Log($"> Size at the start => {size}");
				Assert.That(size, Is.EqualTo(0));

				// currently, the formula seems to be: GET(KEY, VALUE) => 25 + (2 * KEY.Length)

				await tr.GetAsync(Slice.Empty);
				var prev = size;
				size = await tr.GetApproximateSizeAsync();
				Log($"> Size after reading '' => {size} (+{size - prev})");
				Assert.That(size, Is.GreaterThan(0));

				await tr.GetAsync(Literal("A"));
				prev = size;
				size = await tr.GetApproximateSizeAsync();
				Log($"> Size after reading 'A' => {size} (+{size - prev})");
				Assert.That(size, Is.GreaterThan(0));

				await tr.GetAsync(Literal("B"));
				prev = size;
				size = await tr.GetApproximateSizeAsync();
				Log($"> Size after reading 'B' => {size} (+{size - prev})");
				Assert.That(size, Is.GreaterThan(0));

				await tr.GetAsync(Literal("AB"));
				prev = size;
				size = await tr.GetApproximateSizeAsync();
				Log($"> Size after reading 'AB' => {size} (+{size - prev})");
				Assert.That(size, Is.GreaterThan(0));

				await tr.GetAsync(Literal(new string('z', 1000)));
				prev = size;
				size = await tr.GetApproximateSizeAsync();
				Log($"> Size after reading 1k*'z' => {size} (+{size - prev})");
				Assert.That(size, Is.GreaterThan(0));

				// prevent the transaction from commiting !
				tr.Reset();

				// after the reset, we expect the size to be back at 0
				size = await tr.GetApproximateSizeAsync();
				Log($"> Size after reset => {size}");
				Assert.That(size, Is.EqualTo(0));

				return size;
			}, this.Cancellation);

			// SET(KEY, VALUE)
			Log("SET(KEY, VALUE):");
			await db.WriteAsync(async (tr) =>
			{
				// at the start, we expect a size of 0
				var size = await tr.GetApproximateSizeAsync();
				Log($"> Size at the start => {size}");
				Assert.That(size, Is.EqualTo(0));

				// we will NOT commit the transaction, so we can simply write "anywhere"

				// currently, the formula seems to be: SET(KEY, VALUE) => 53 + (3 * KEY.Length) + VALUE.Length

				tr.Set(Slice.Empty, Slice.Empty);
				var prev = size;
				size = await tr.GetApproximateSizeAsync();
				Log($"> Size after writing '' = '' => {size} (+{size - prev})");
				Assert.That(size, Is.GreaterThan(0));

				tr.Set(Literal("A"), Slice.Empty);
				prev = size;
				size = await tr.GetApproximateSizeAsync();
				Log($"> Size after writing 'A' = '' => {size} (+{size - prev})");
				Assert.That(size, Is.GreaterThan(prev));

				tr.Set(Literal("B"), Slice.Empty);
				prev = size;
				size = await tr.GetApproximateSizeAsync();
				Log($"> Size after writing 'B' = '' => {size} (+{size - prev})");
				Assert.That(size, Is.GreaterThan(prev));

				tr.Set(Literal("AB"), Slice.Empty);
				prev = size;
				size = await tr.GetApproximateSizeAsync();
				Log($"> Size after writing 'AB' = '' => {size} (+{size - prev})");
				Assert.That(size, Is.GreaterThan(prev));

				tr.Set(Literal("C"), Text("A"));
				prev = size;
				size = await tr.GetApproximateSizeAsync();
				Log($"> Size after writing 'C' = 'A' => {size} (+{size - prev})");
				Assert.That(size, Is.GreaterThan(prev));

				tr.Set(Literal("D"), Text(new string('z', 1000)));
				prev = size;
				size = await tr.GetApproximateSizeAsync();
				Log($"> Size after writing 'D' = 1k * 'z' => {size} (+{size - prev})");
				Assert.That(size, Is.GreaterThan(prev));

				// prevent the transaction from commiting !
				tr.Reset();

				// after the reset, we expect the size to be back at 0
				size = await tr.GetApproximateSizeAsync();
				Log($"> Size after reset => {size}");
				Assert.That(size, Is.EqualTo(0));
			}, this.Cancellation);

			// SET(KEY, VALUE)
			Log("CLEAR(KEY):");
			await db.WriteAsync(async (tr) =>
			{
				// at the start, we expect a size of 0
				var size = await tr.GetApproximateSizeAsync();
				Log($"> Size at the start => {size}");
				Assert.That(size, Is.EqualTo(0));

				// we will NOT commit the transaction, so we can simply write "anywhere"

				// currently, the formula seems to be: CLEAR(KEY) => 50 + (4 * KEY.Length)

				tr.Clear(Slice.Empty);
				var prev = size;
				size = await tr.GetApproximateSizeAsync();
				Log($"> Size after clearing '' => {size} (+{size - prev})");
				Assert.That(size, Is.GreaterThan(0));

				tr.Clear(Literal("A"));
				prev = size;
				size = await tr.GetApproximateSizeAsync();
				Log($"> Size after clearing 'A' => {size} (+{size - prev})");
				Assert.That(size, Is.GreaterThan(prev));

				tr.Clear(Literal("B"));
				prev = size;
				size = await tr.GetApproximateSizeAsync();
				Log($"> Size after clearing 'B' => {size} (+{size - prev})");
				Assert.That(size, Is.GreaterThan(prev));

				tr.Clear(Literal("AB"));
				prev = size;
				size = await tr.GetApproximateSizeAsync();
				Log($"> Size after clearing 'AB' => {size} (+{size - prev})");
				Assert.That(size, Is.GreaterThan(prev));

				tr.Clear(Literal(new string('z', 1000)));
				prev = size;
				size = await tr.GetApproximateSizeAsync();
				Log($"> Size after clearing 1k * 'z' => {size} (+{size - prev})");
				Assert.That(size, Is.GreaterThan(prev));

				// prevent the transaction from commiting !
				tr.Reset();

				// after the reset, we expect the size to be back at 0
				size = await tr.GetApproximateSizeAsync();
				Log($"> Size after reset => {size}");
				Assert.That(size, Is.EqualTo(0));
			}, this.Cancellation);
		}

		[Test]
		[CoversCells("introspection/split-points")]
		public async Task Test_Can_Get_Range_Split_Points()
		{
			const int NUM_ITEMS = 100_000;
			const int VALUE_SIZE = 50;
			const int CHUNK_SIZE = (NUM_ITEMS * (VALUE_SIZE + 16)) / 100; // we would like to split in ~100 chunks

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			// we will setup a list of 1K keys with randomized value size (that we keep track of)
			var rnd = new Random(123456);
			var values = Enumerable.Range(0, NUM_ITEMS).Select(_ => Slice.Random(rnd, VALUE_SIZE)).ToArray();

			const int BATCH_SIZE = 1_000_000 / VALUE_SIZE;
			Log($"Creating {values.Length:N0} keys ({VALUE_SIZE:N0} bytes per key) with {NUM_ITEMS * VALUE_SIZE:N0} total bytes ({BATCH_SIZE:N0} per batch)");
			for (int i = 0; i < values.Length; i += BATCH_SIZE)
			{
				await db.WriteAsync(async (tr) =>
				{
					var subspace = await db.Root.Resolve(tr);

					// fill the db with keys from (0,) = XXX to (N-1,) = XXX
					for (int j = 0; j < BATCH_SIZE && i + j < values.Length; j++)
					{
						tr.Set(subspace.Key(i + j), values[i + j]);
					}
				}, this.Cancellation);
			}

			//db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

			Log($"Get split points for chunks of {CHUNK_SIZE:N0} bytes...");
			var keys = await db.ReadAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);

				var begin = subspace.Key(0).ToSlice();
				var end = subspace.Key(values.Length).ToSlice();

				var keys = await tr.GetRangeSplitPointsAsync(begin, end, CHUNK_SIZE);
				Assert.That(keys, Is.Not.Null.Or.Empty);
				Log($"Found {keys.Length} split points");

				// looking at the implementation, it guarantees that the first and last "split points" will be the bounds of the range repeated (even if the keys do not exist)
				Assert.That(keys, Has.Length.GreaterThan(2), "We expect at least 1 split point between the bounds of the range!");
				Assert.That(keys[0], Is.EqualTo(begin), "First key should be the start of the range");
				Assert.That(keys[^1], Is.EqualTo(end), "Last key should be the end of the range");

				// all keys should be in ascending order
				for (int i = 1; i < keys.Length; i++)
				{
					Assert.That(keys[i], Is.GreaterThan(keys[i - 1]), "Split points should be sorted");
				}
				return keys;
			}, this.Cancellation);

			var chunks = new List<KeyValuePair<Slice, Slice>[]>();

			for(int i = 0; i < keys.Length - 1; i++)
			{
				var (chunk, begin, end) = await db.ReadAsync(async tr => 
				{
					var subspace = await db.Root.Resolve(tr);

					// we will get all the keys in between and dump some statistics
					var range = KeyRange.Create(keys[i], keys[i + 1]);
					var chunk = await tr.GetRange(range).ToArrayAsync();

					return (chunk, subspace.PrettyPrint(range.Begin), subspace.PrettyPrint(range.End));
				}, this.Cancellation);

				chunks.Add(chunk);
				Assert.That(chunk, Is.Not.Null.Or.Empty, "There should be at least one key in chunk {0}..{1}", begin, end);
				var actualChunkSize = chunk.Sum(kv => kv.Key.Count + kv.Value.Count);
				Log($"> {begin} .. {end}:\t{chunk.Length,6:N0} results, size(k+v) = {actualChunkSize,7:N0} bytes, ratio = {(100.0 * actualChunkSize) / CHUNK_SIZE,6:N0}%");
			}

			Log($"Statistics: smallest = {chunks.Min(c => c.Sum(kv => kv.Value.Count)):N0} bytes, largest = {chunks.Max(c => c.Sum(kv => kv.Value.Count)):N0} bytes, average = {chunks.Average(c => c.Sum(kv => kv.Value.Count)):N0} bytes");

			// we should have read all our keys!
			Assert.That(chunks.Sum(c => c.Length), Is.EqualTo(NUM_ITEMS), "All keys should be accounted for");
		}

		[Test]
		[CoversCells("introspection/estimated-range-size")]
		public async Task Test_Can_Get_Estimated_Range_Size_Bytes()
		{
			const int NUM_ITEMS = 50_000;
			const int VALUE_SIZE = 32;

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			// we will setup a list of N keys with randomized value size (that we keep track of)
			var rnd = new Random(123456);
			var values = Enumerable.Range(0, NUM_ITEMS).Select(_ => Slice.Random(rnd, VALUE_SIZE)).ToArray();

			Log($"Creating {values.Length:N0} keys ({VALUE_SIZE:N0} bytes per key) with {NUM_ITEMS * VALUE_SIZE:N0} total bytes");
			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);

				// fill the db with keys from (..., 0) = XXX to (..., N-1) = XXX
				for (int i = 0; i < values.Length; i++)
				{
					tr.Set(subspace.Key(i), values[i]);
				}
			}, this.Cancellation);

			Log("Get estimated ranges size...");
			for (int i = 0; i < 25; i++)
			{
				await db.ReadAsync(async (tr) =>
				{
					var subspace = await db.Root.Resolve(tr);

					int x = rnd.Next(NUM_ITEMS);
					int y = rnd.Next(NUM_ITEMS);
					if (x == y) y++;
					if (x > y) { (x, y) = (y, x); }

					var begin = subspace.Key(x).ToSlice();
					var end = subspace.Key(y).ToSlice();

					var estimatedSize = await tr.GetEstimatedRangeSizeBytesAsync(begin, end);

					var exactSize = await tr.GetRange(begin, end).Select(kv => kv.Value.Count + kv.Key.Count).SumAsync();

					Log($"> ({x,6:N0} .. {y,6:N0}): estimated = {estimatedSize,9:N0} bytes, exact(key+value) = {exactSize,9:N0} bytes, ratio = {(100.0 * estimatedSize) / exactSize,6:N1}%");
					Assert.That(estimatedSize, Is.GreaterThanOrEqualTo(0)); //note: it is _possible_ to have 0 for very small ranges :(

					return estimatedSize;
				}, this.Cancellation);
			}
		}

		[Test]
		[CoversCells("conflicts/report-conflicting-keys-option", "conflicts/conflicting-keys-special-keyspace")]
		public async Task Test_Can_Get_Conflicting_Keys_Manually()
		{

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			// setup
			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("A"), Text("Initial A"));
				tr.Set(subspace.Key("B"), Text("Initial B"));
				tr.Set(subspace.Key("C"), Text("Initial C"));
				tr.Set(subspace.Key("D"), Text("Initial D")); // Eurobeat intensifies
			}, this.Cancellation);

			Log("Start T1 & T2");
			using var t1 = db.BeginTransaction(this.Cancellation);
			using var t2 = db.BeginTransaction(this.Cancellation);

			// enable Conflicting Keys option
			t1.Options.WithReportConflictingKeys();

			// T1: read values, change something
			Log("# T1: read/write");
			var subspace1 = await db.Root.Resolve(t1);
			_ = await t1.GetAsync(subspace1.Key("A"));
			_ = await t1.GetAsync(subspace1.Key("B"));
			_ = await t1.GetAsync(subspace1.Key("C"));
			_ = await t1.GetAsync(subspace1.Key("D"));
			t1.Set(subspace1.Key("X"), Text("Something"));

			// T2: modifies values read by T1
			Log("# T2: write");
			var subspace2 = await db.Root.Resolve(t2);
			t2.Set(subspace2.Key("A"), Text("Updated A"));
			t2.Clear(subspace2.Key("B"));
			t2.ClearRange(subspace2.Key("C").ToRange(inclusive: true));

			// T2 commits first, should succeed
			Log("# T2: Commit");
			Assert.That(async () => await t2.CommitAsync(), Throws.Nothing);

			// T1 commits second, should fail with NotCommited
			Log("# T1: Commit");
			Assert.That(async () => await t1.CommitAsync(), Throws.InstanceOf<FdbException>(out var captured));
			Log($"> {captured.Exception.Code}");
			Assert.That(captured.Exception.Code, Is.EqualTo(FdbError.NotCommitted));

			// now, we should be able to get the list of conflicts by reading the special key
			Log("T1: Read Conflincting Keys");
			var res = await t1.GetRange(FdbSystemKey.TransactionConflictingKeys.ToRange()).ToArrayAsync();
			Log($"> got {res.Length} results");
			foreach (var kv in res)
			{
				Log($"  - {kv.Key}: {kv.Value}");
				Assert.That(kv.Key.StartsWith(Fdb.System.TransactionConflictingKeysPrefix));
				Assert.That(kv.Value.Count, Is.EqualTo(1));
				Assert.That(kv.Value[0], Is.AnyOf('0', '1'));
			}
			// we expect 3 conflicts, each conflict with a begin/end key
			Assert.That(res.Count, Is.EqualTo(3 * 2));
		}

		[Test]
		public async Task Test_Can_Get_Conflicting_Keys_Automatically()
		{

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			// setup
			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("A"), Text("Initial A"));
				tr.Set(subspace.Key("B"), Text("Initial B"));
				tr.Set(subspace.Key("C"), Text("Initial C"));
				tr.Set(subspace.Key("D"), Text("Initial D")); // Eurobeat intensifies
			}, this.Cancellation);

			static async Task<List<KeyRange>> GetConflictingKeys(IFdbReadOnlyTransaction tr)
			{
				// Reading all keys inside \xFF\xFF/transaction/conflicting_keys/ should return a list of "bounds" of the conflicting ranges
				// - each range start with a k/v pair with value '1' (ASCII 49), and the tail of the key will be the start of the range (inclusive).
				// - the following k/v pair should have value '0' (ASCII 48), and the tail of the key will be the end of the range (exclusive).

				var res = new List<KeyRange>();
				Slice begin = Slice.Nil;
				await foreach (var kv in tr.GetRange(FdbSystemKey.TransactionConflictingKeys.ToRange(inclusive: true)))
				{
					if (!kv.Key.StartsWith(Fdb.System.TransactionConflictingKeysPrefix) || kv.Value.Count != 1 || kv.Value[0] != (begin.IsNull ? (byte)'1' : (byte)'0'))
					{ // something's wrong with the encoding ??
#if DEBUG
						// check if the range is correct, or maybe the encoding has changed since 7.4 ?
						if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
						break;
					}
					if (begin.IsNull)
					{
						begin = kv.Key.Substring(Fdb.System.TransactionConflictingKeysPrefix.Count);
					}
					else
					{
						var end = kv.Key.Substring(Fdb.System.TransactionConflictingKeysPrefix.Count);
						res.Add(new KeyRange(begin, end));
						begin = default;
					}
				}
				return res;
			}

			Log("Start");
			await db.WriteAsync(async t1 =>
			{
				Log($"# T1: #{t1.Context.Retries}");

				// enable Conflicting Keys option
				t1.Options.WithReportConflictingKeys();
				t1.Context.OnConflict(async (tr, _) =>
				{
					Log($"#!# OnCommitFailure called: {tr.Context.PreviousError}");
					var conflictingKeys = await GetConflictingKeys(tr);
					Log($"#!# found {conflictingKeys.Count} ranges:");
					foreach (var kr in conflictingKeys)
					{
						Log($"#!# - {kr}");
					}
				});

				// T1: read values, change something
				if (t1.Context.Retries == 0)
				{
					Log("# T1: read/write");
					var subspace1 = await db.Root.Resolve(t1);
					_ = await t1.GetAsync(subspace1.Key("A"));
					_ = await t1.GetAsync(subspace1.Key("B"));
					_ = await t1.GetAsync(subspace1.Key("C"));
					_ = await t1.GetAsync(subspace1.Key("D"));
					t1.Set(subspace1.Key("X"), Text("Something"));
				}

				// T2: modifies values read by T1
				Log("# T2: write");
				await db.WriteAsync(async t2 =>
				{
					var subspace2 = await db.Root.Resolve(t2);
					t2.Set(subspace2.Key("A"), Text("Updated A"));
					t2.Clear(subspace2.Key("B"));
					t2.ClearRange(subspace2.Key("C").ToRange(inclusive: true));
				}, this.Cancellation);

			}, this.Cancellation);

		}

		[Test]
		[Ignore("This kills my test server!")] // DO NOT RUN !
		public async Task Test_Can_Construct_Data()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			// Example:
			// - 02000000|cfdf|6400000000000000|ff02000000000000|7b00000000000000
			// - PrefixLength: 2
			// - Prefix: <CF><DF>
			// - ValueSize: 100 (0x64)
			// - KeyCount: 767 (0x2FF)
			// - Seed: 123 (0x7B)
			// 

			int valueSize = 100;
			int keyCount = 767;
			long seed = 123;

			// Generate the data set
			Log("# Generating data....");
			await db.WriteAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);
				var prefix = subspace.GetPrefix();
				Log($"Prefix: {prefix}");

				var sw = new SliceWriter();
				// StringRefs are stored as a length (32 bits LE) followed by the bytes
				sw.WriteInt32(prefix.Count);
				sw.WriteBytes(prefix);
				sw.WriteInt64(valueSize);
				sw.WriteInt64(keyCount);
				sw.WriteInt64(seed);

				var command = sw.ToSlice();
				DumpHexa(command);

				tr.Options.WithWriteAccessToSystemKeys();
				tr.Set(FdbSystemKey.ConstructData, command);
			}, this.Cancellation);

			// Verify the dataset

			Log("# Checking generated keys");
			var kvps = await db.ReadAsync(async tr =>
			{
				var subspace = await db.Root.Resolve(tr);

				return await tr.GetRange(subspace.ToRange()).ToArrayAsync();
			}, this.Cancellation);

			Log($"> Found {kvps.Length} keys");
			foreach (var kvp in kvps)
			{
				Log($"  - {kvp.Key:K} = {kvp.Value:V}");
			}

		}

		[Test]
		public async Task Test_GetRange_Merged_Begin_Bound_Honors_Negative_Offset()
		{
			// a range read in a transaction with pending writes resolves its BOUND selectors over the merged view
			// with full key-selector semantics: a begin selector walking BACKWARD from its pivot must land below
			// the keys masked by an own clear-range (selector fuzz family, seed 0 step 32)

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("a"), Text("alpha"));
				tr.Set(subspace.Key("c"), Text("charlie"));
				tr.Set(subspace.Key("d"), Text("delta"));
			}, this.Cancellation);

			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr1);

				// the own clear-range masks 'c' (end exclusive: 'd' survives); merged view = { a, d }
				tr1.ClearRange(subspace.Key("c"), subspace.Key("d"));

				// begin = lLT{z} - 1: last visible key < z is 'd', one step back lands on 'a' (NOT the masked 'c')
				// end   = lLT{z}    : 'd'
				// so [a, d) over the merged view = { a }
				var chunk = await tr1.Snapshot.GetRangeAsync(
					FdbKeySelector.LastLessThan(subspace.Key("z")) - 1,
					FdbKeySelector.LastLessThan(subspace.Key("z")),
					new FdbRangeOptions { Limit = 1 });
				Assert.That(chunk.Count, Is.EqualTo(1), "the backward-walking begin bound must resolve below the keys masked by the own clear");
				Assert.That(chunk[0].Key, Is.EqualTo(subspace.Key("a").ToSlice()));
			}
		}

		[Test]
		public async Task Test_GetRange_Merged_End_Bound_Skips_Own_Cleared_Keys()
		{
			// the end bound of a merged range read must resolve over the VISIBLE keys: a last-less-than selector
			// whose pivot sits just above an own-cleared key resolves below that key, making the range empty
			// (selector fuzz family, seed 0 step 34)

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("a"), Text("alpha"));
				tr.Set(subspace.Key("c"), Text("charlie"));
				tr.Set(subspace.Key("d"), Text("delta"));
			}, this.Cancellation);

			using (var tr1 = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr1);

				// merged view = { a, d }
				tr1.ClearRange(subspace.Key("c"), subspace.Key("d"));

				// begin = fGE{a} = 'a'; end = lLT{d}: last visible key < d is 'a' (NOT the masked 'c')
				// so [a, a) is EMPTY - resolving the end onto the masked 'c' would wrongly return { a }
				var chunk = await tr1.Snapshot.GetRangeAsync(
					FdbKeySelector.FirstGreaterOrEqual(subspace.Key("a")),
					FdbKeySelector.LastLessThan(subspace.Key("d")),
					new FdbRangeOptions());
				Assert.That(chunk.Count, Is.Zero, "the end bound must resolve below the keys masked by the own clear, yielding an empty range");
			}
		}

		[TestCase("suffix")]    // 0xFF placeholder with user-version bytes after the stamp region (the evt- fuzz shape)
		[TestCase("midkey")]    // zero placeholder with an ascii tail after it (the log- fuzz shape)
		[TestCase("midkey-ff")] // 0xFF placeholder with the same ascii tail (separates the byte values from the layout)
		[TestCase("end-zero")]  // zero placeholder at the very end of the key
		public async Task Test_Stamped_Key_Into_Own_Cleared_Range_Applies_In_Order(string flavor)
		{
			// a transaction that wipes a range and THEN writes a versionstamped key into the wiped span must
			// commit cleanly and land the completed key: mutations apply in submission order, so the earlier
			// wipe never swallows (or fails) the later stamped write - for EVERY placeholder layout, including
			// zero placeholder bytes that sort the pending entry adjacent to the wipe's begin bound
			// (versionstamp fuzz family, seeds 16/40 for the suffix shape, seed 62 for the mid-key shape)

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			Slice placeholderTail = flavor switch
			{
				"suffix" => VersionStamp.Incomplete(0).ToSlice(),
				"midkey" => Slice.Zero(10) + Text("-2"),
				"midkey-ff" => Slice.Repeat(0xFF, 10) + Text("-2"),
				_ => Slice.Zero(10),
			};

			Slice prefix;
			VersionStamp stamp;
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);
				prefix = subspace.GetPrefix().Copy(); // detach: the resolved prefix must not outlive the transaction's memory

				tr.ClearRange(prefix + Text("evt-"), prefix + Text("evt."));
				tr.SetVersionStampedKey(prefix + Text("evt-") + placeholderTail, prefix.Count + 4, Text("after"));

				var stampTask = tr.GetVersionStampAsync();
				await tr.CommitAsync();
				stamp = await stampTask;
			}

			Slice completedTail = flavor switch
			{
				"suffix" => VersionStamp.Complete(stamp.TransactionVersion, stamp.TransactionOrder, 0).ToSlice(),
				"midkey" or "midkey-ff" => stamp.ToSlice() + Text("-2"),
				_ => stamp.ToSlice(),
			};

			await db.ReadAsync(async (tr) =>
			{
				var completed = prefix + Text("evt-") + completedTail;
				var value = await tr.GetAsync(completed);
				Assert.That(value, Is.EqualTo(Text("after")), $"the stamped key ({flavor}) written after the own wipe must land with its value");

				var chunk = await tr.GetRangeAsync(
					KeySelector.FirstGreaterOrEqual(prefix + Text("evt-")),
					KeySelector.FirstGreaterOrEqual(prefix + Text("evt.")),
					new FdbRangeOptions());
				Assert.That(chunk.Count, Is.EqualTo(1), $"the wiped span must hold exactly the one stamped key ({flavor})");
				return true;
			}, this.Cancellation);
		}

		[Test]
		public async Task Test_Duplicate_Stamped_Keys_Last_Write_Wins()
		{
			// two versionstamped-key writes with IDENTICAL placeholder bytes (same user version) complete to
			// the SAME key: the last submitted mutation wins, like any other same-key write ordering
			// (versionstamp fuzz family, seed 11: real keeps the later value)

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			Slice prefix;
			VersionStamp stamp;
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var subspace = await db.Root.Resolve(tr);
				prefix = subspace.GetPrefix().Copy(); // detach: the resolved prefix must not outlive the transaction's memory

				var placeholder = prefix + Text("evt-") + VersionStamp.Incomplete(0).ToSlice();
				tr.SetVersionStampedKey(placeholder, prefix.Count + 4, Text("first"));
				tr.SetVersionStampedKey(placeholder, prefix.Count + 4, Text("second"));

				var stampTask = tr.GetVersionStampAsync();
				await tr.CommitAsync();
				stamp = await stampTask;
			}

			await db.ReadAsync(async (tr) =>
			{
				var completed = prefix + Text("evt-") + VersionStamp.Complete(stamp.TransactionVersion, stamp.TransactionOrder, 0).ToSlice();
				var value = await tr.GetAsync(completed);
				Assert.That(value, Is.EqualTo(Text("second")), "identical placeholders complete to one key and the LAST submitted write wins");
				return true;
			}, this.Cancellation);
		}

		// Inlined from FoundationDB.Tests/TestHelpers.cs (not available outside that assembly).
		private static async Task AssertThrowsFdbErrorAsync(Func<Task> asyncTest, FdbError expectedCode, string message)
		{
			try
			{
				await asyncTest();
				Assert.Fail(message);
			}
			catch (AssertionException) { throw; }
			catch (Exception e)
			{
				Assert.That(e, Is.InstanceOf<FdbException>().With.Property("Code").EqualTo(expectedCode), message);
			}
		}

	}

}
