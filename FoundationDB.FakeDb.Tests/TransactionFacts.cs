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
// ReSharper disable JoinDeclarationAndInitializer

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
namespace FoundationDB.Testing.Tests
{
	using System.Text;
	using FoundationDB.Client;
	using Microsoft.Extensions.Time.Testing;
	using SnowBank.Data.Tuples;

	[TestFixture]
	public class TransactionFacts : FakeDbTest
	{

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

		[Test]
		public void Test_Can_Create_And_Dispose_Transactions()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				Assert.That(db, Is.InstanceOf<FdbDatabase>(), "This test only works directly on FdbDatabase");

				using (var tr = (FdbTransaction) db.BeginTransaction(this.Cancellation))
				{
					Assert.That(tr, Is.Not.Null, "BeginTransaction should return a valid instance");

					var handler = tr.Context.GetTransactionHandler();
					Assert.That(handler, Is.Not.Null.And.InstanceOf<FakeDbStore.TransactionHandler>());

					Assert.That(handler.IsClosed, Is.False, "Transaction handler should not be closed");
					Assert.That(tr.Database, Is.SameAs(db), "Transaction should reference the parent Database");
					Assert.That(tr.Size, Is.Zero, "Estimated size should be zero");
					Assert.That(tr.IsReadOnly, Is.False, "Transaction is not read-only");
					Assert.That(tr.IsSnapshot, Is.False, "Transaction is not in snapshot mode by default");

					// manually dispose the transaction
					// ReSharper disable once DisposeOnUsingVariable
					tr.Dispose();

					Assert.That(handler.IsClosed, Is.True, "Transaction handler should now be closed");

					// multiple calls to dispose should not do anything more
					// ReSharper disable once DisposeOnUsingVariable
					Assert.That(() => { tr.Dispose(); }, Throws.Nothing);
				}
			}
		}

		[Test]
		public void Test_Can_Get_A_Snapshot_Version_Of_A_Transaction()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
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
			}
		}

		[Test]
		public async Task Test_Creating_A_ReadOnly_Transaction_Throws_When_Writing()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
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
		}

		[Test]
		public void Test_Creating_Concurrent_Transactions_Are_Independent()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
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

					var handler1 = tr1.Context.GetTransactionHandler();
					var handler2 = tr2.Context.GetTransactionHandler();
					Assert.That(((FdbTransaction)tr1).Context.GetTransactionHandler(), Is.Not.EqualTo(((FdbTransaction)tr2).Context.GetTransactionHandler()), "Should have different FDB_FUTURE* handles");

					// disposing the first should not impact the second

					tr1.Dispose();

					Assert.That(handler1.IsClosed, Is.True, "First FDB_FUTURE* handle should be closed");
					Assert.That(handler2.IsClosed, Is.False, "Second FDB_FUTURE* handle should still be opened");
				}
				finally
				{
					tr1?.Dispose();
					tr2?.Dispose();
				}
			}
		}

		[Test]
		public async Task Test_Commiting_An_Empty_Transaction_Does_Nothing()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					Assert.That(tr, Is.InstanceOf<FdbTransaction>());

					// do nothing with it
					await tr.CommitAsync();
					// => should not fail!

					//TODO: check commit version?
				}
			}
		}

		[Test]
		public async Task Test_Resetting_An_Empty_Transaction_Does_Nothing()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					// do nothing with it
					await tr.CommitAsync();
					// => should not fail!

					// Committed version should be -1 (where is it specified?)
					long ver = tr.GetCommittedVersion();
					Assert.That(ver, Is.EqualTo(-1), "Committed version of empty transaction should be -1");
				}
			}
		}

		[Test]
		public void Test_Cancelling_An_Empty_Transaction_Does_Nothing()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					Assert.That(tr, Is.InstanceOf<FdbTransaction>());

					// do nothing with it
					tr.Cancel();
					// => should not fail!
				}
			}
		}

		[Test]
		public async Task Test_Cancelling_Transaction_Before_Commit_Should_Throw_Immediately()
		{

			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);
					tr.Set(subspace.Key(1), Value("hello"));
					tr.Cancel();

					Assert.That(
						async () => await tr.CommitAsync(),
						Throws.InstanceOf<FdbException>().With.Property(nameof(FdbException.Code)).EqualTo(FdbError.TransactionCancelled),
						"Committing an already cancelled exception should fail"
					);
				}
			}
		}

		[Test]
		public async Task Test_Can_Get_Transaction_Read_Version()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					long ver = await tr.GetReadVersionAsync();
					Assert.That(ver, Is.GreaterThan(0), "Read version should be > 0");

					// if we ask for it again, we should have the same value
					long ver2 = await tr.GetReadVersionAsync();
					Assert.That(ver2, Is.EqualTo(ver), "Read version should not change inside same transaction");
				}
			}
		}

		[Test]
		public async Task Test_Write_And_Read_Simple_Keys()
		{
			// test that we can read and write simple keys

			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				long ticks = DateTime.UtcNow.Ticks;
				long writeVersion;
				long readVersion;

				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// write a bunch of keys
				Log("Write some keys...");
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);
					tr.Set(subspace.Key("hello"), Value("World!"));
					tr.Set(subspace.Key("timestamp"), Slice.FromInt64(ticks));
					tr.Set(subspace.Key("blob"), new byte[] { 42, 123, 7 }.AsSlice());

					Log("> committing...");
					await tr.CommitAsync();

					writeVersion = tr.GetCommittedVersion();
					Log($"> commit version = {writeVersion:N0}");
					Assert.That(writeVersion, Is.GreaterThan(0), "Committed version of non-empty transaction should be > 0");
				}

				// read them back
				Log("Read back keys...");
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);

					Slice bytes;

					readVersion = await tr.GetReadVersionAsync();
					Log($"> read version = {readVersion:N0}");
					Assert.That(readVersion, Is.GreaterThan(0), "Read version should be > 0");

					bytes = await tr.GetAsync(subspace.Key("hello"));
					Log($"> {bytes:V}");
					Assert.That(bytes.ToStringUtf8(), Is.EqualTo("World!"));

					bytes = await tr.GetAsync(subspace.Key("timestamp"));
					Log($"> {bytes:X}");
					Assert.That(bytes.ToInt64(), Is.EqualTo(ticks));

					bytes = await tr.GetAsync(subspace.Key("blob"));
					Log($"> {bytes:X}");
					Assert.That(bytes.GetBytes(), Is.EqualTo(new byte[] { 42, 123, 7 }));
				}

				Assert.That(readVersion, Is.GreaterThanOrEqualTo(writeVersion), "Read version should not be before previous committed version");
			}
		}

		[Test]
		public async Task Test_Can_Resolve_Key_Selector()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				#region Insert a bunch of keys ...
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);
					// keys
					// - (test,) + \0
					// - (test, 0) .. (test, 19)
					// - (test,) + \xFF
					tr.Set(subspace.Bytes(FdbKey.MinValue), Value("min"));
					for (int i = 0; i < 20; i++)
					{
						tr.Set(subspace.Key(i), Value(i.ToString()));
					}
					tr.Set(subspace.Bytes(FdbKey.MaxValue), Value("max"));
					await tr.CommitAsync();
				}
				#endregion

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);

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

			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{

				// write some actual keys outside the boundary, otherwise the test will not be very useful...
				await db.WriteAsync(tr =>
				{
					tr.Set(Key("AAA"), Value("Smallest"));
					tr.Set(Key("GGG"), Value("Good game!"));
					tr.Set(Key("ZZZ"), Value("Largest"));
				}, this.Cancellation);

				DumpStore(store, "Content of db");

				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					// before <00>
					Log($"GetKey({KeySelector.LastLessThan(FdbKey.MinValue)})");
					var key = await tr.GetKeyAsync(KeySelector.LastLessThan(FdbKey.MinValue));
					Log($"> {key:K}");
					Assert.That(key, Is.EqualTo(Slice.Empty), "lLT(<00>) => ''");

					// before the first key in the db
					Log($"GetKey({KeySelector.FirstGreaterOrEqual(FdbKey.MinValue)})");
					var minKey = await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(FdbKey.MinValue));
					Log($"> {minKey:K}");
					Assert.That(minKey, Is.EqualTo(Key("AAA")));

					Log($"GetKey({KeySelector.LastLessThan(minKey)})");
					key = await tr.GetKeyAsync(KeySelector.LastLessThan(minKey));
					Log($"> {key:K}");
					Assert.That(key, Is.EqualTo(Slice.Empty), "lLT(min_key) => ''");

					// after the last key in the db

					Log($"GetKey({KeySelector.LastLessThan(FdbKey.MaxValue)})");
					var maxKey = await tr.GetKeyAsync(KeySelector.LastLessThan(FdbKey.MaxValue));
					Log($"> {maxKey:K}");
					Assert.That(maxKey, Is.EqualTo(Key("ZZZ")));

					Log($"GetKey({KeySelector.FirstGreaterThan(maxKey)})");
					key = await tr.GetKeyAsync(KeySelector.FirstGreaterThan(maxKey));
					Log($"> {key:K}");
					Assert.That(key, Is.EqualTo(FdbKey.MaxValue), "fGT(maxKey) => <FF>");

					// after <FF>
					Log($"GetKey({KeySelector.FirstGreaterThan(FdbKey.MaxValue)})");
					key = await tr.GetKeyAsync(KeySelector.FirstGreaterThan(FdbKey.MaxValue));
					Log($"> {key:K}");
					Assert.That(key, Is.EqualTo(FdbKey.MaxValue), "fGT(<FF>) => <FF>");

					Log($"GetKey({KeySelector.FirstGreaterThan(FdbKey.MaxValue + FdbKey.MaxValue)})");
					Assert.That(async () => await tr.GetKeyAsync(KeySelector.FirstGreaterThan(FdbKey.MaxValue + FdbKey.MaxValue)), Throws.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.KeyOutsideLegalRange));
					Log($"GetKey({KeySelector.LastLessThan(Fdb.System.MinValue)})");
					Assert.That(async () => await tr.GetKeyAsync(KeySelector.LastLessThan(Fdb.System.MinValue)), Throws.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.KeyOutsideLegalRange));

					tr.Options.WithReadAccessToSystemKeys();

					Log($"GetKey({KeySelector.FirstGreaterThan(FdbKey.MaxValue)})");
					var firstSystemKey = await tr.GetKeyAsync(KeySelector.FirstGreaterThan(FdbKey.MaxValue));
					Log($"> {firstSystemKey:K}");
					// usually the first key in the system space is <FF>/backupDataFormat, but that may change in the future version.
					Assert.That(firstSystemKey, Is.GreaterThan(FdbKey.MaxValue), "key should be between <FF> and <FF><FF>");
					Assert.That(firstSystemKey, Is.LessThan(Fdb.System.MaxValue), "key should be between <FF> and <FF><FF>");

					// with access to system keys, the maximum possible key becomes <FF><FF>
					Log($"GetKey({KeySelector.FirstGreaterOrEqual(Fdb.System.MaxValue)})");
					key = await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(Fdb.System.MaxValue));
					Log($"> {key:K}");
					Assert.That(key, Is.EqualTo(Fdb.System.MaxValue), "fGE(<FF><FF>) => <FF><FF> (with access to system keys)");

					Log($"GetKey({KeySelector.FirstGreaterThan(Fdb.System.MaxValue)})");
					key = await tr.GetKeyAsync(KeySelector.FirstGreaterThan(Fdb.System.MaxValue));
					Log($"> {key:K}");
					Assert.That(key, Is.EqualTo(Fdb.System.MaxValue), "fGT(<FF><FF>) => <FF><FF> (with access to system keys)");

					Log($"GetKey({KeySelector.LastLessThan(Fdb.System.MinValue)})");
					key = await tr.GetKeyAsync(KeySelector.LastLessThan(Fdb.System.MinValue));
					Log($"> {key:K}");
					Assert.That(key, Is.EqualTo(maxKey), "lLT(<FF><00>) => max_key (with access to system keys)");

					Log($"GetKey({KeySelector.FirstGreaterThan(maxKey)})");
					key = await tr.GetKeyAsync(KeySelector.FirstGreaterThan(maxKey));
					Log($"> {key:K}");
					Assert.That(key, Is.EqualTo(firstSystemKey), "fGT(max_key) => first_system_key (with access to system keys)");
				}
			}

		}

		[Test]
		public async Task Test_Get_Multiple_Values()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				int[] ids = [ 8, 7, 2, 9, 5, 0, 3, 4, 6, 1 ];

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);
					for (int i = 0; i < ids.Length; i++)
					{
						tr.Set(subspace.Key(i), Value($"#{i}"));
					}
					await tr.CommitAsync();
				}

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);

					var results = await tr.GetValuesAsync(ids, id => subspace.Key(id));

					Assert.That(results, Is.Not.Null);
					Assert.That(results.Length, Is.EqualTo(ids.Length));

					Log(string.Join(", ", results));

					for (int i = 0; i < ids.Length;i++)
					{
						Assert.That(results[i].ToString(), Is.EqualTo($"#{ids[i]}"));
					}
				}
			}
		}

		[Test]
		public async Task Test_Get_Multiple_Keys()
		{
			const int N = 20;

			using(var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				#region Insert a bunch of keys ...
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);
					// keys
					// - (test,) + \0
					// - (test, 0) .. (test, N-1)
					// - (test,) + \xFF
					tr.Set(subspace.Bytes(FdbKey.MinValue), Value("min"));
					for (int i = 0; i < 20; i++)
					{
						tr.Set(subspace.Key(i), Value(i.ToString()));
					}
					tr.Set(subspace.Bytes(FdbKey.MaxValue), Value("max"));
					await tr.CommitAsync();
				}
				#endregion

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);

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
					var results2 = await tr.GetKeysAsync((IEnumerable<FdbKeySelector<FdbTupleKey<int>>>)selectors);
					Assert.That(results2, Is.EqualTo(results));

					// GetKeysAsync(real enumerable)
					var results3 = await tr.GetKeysAsync(selectors.Select(x => x));
					Assert.That(results3, Is.EqualTo(results));
				}
			}
		}

		[Test]
		public async Task Test_Can_Check_Value()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// write a bunch of keys
				await db.WriteAsync(async tr =>
				{
					var subspace = await location.Resolve(tr);
					tr.Set(subspace.Key("hello"), Value("World!"));
					tr.Set(subspace.Key("foo"), Slice.Empty);
				}, this.Cancellation);

				async Task Check(IFdbReadOnlyTransaction tr, FdbTupleKey<string> key, Slice expected, FdbValueCheckResult result, Slice actual)
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
					var subspace = await location.Resolve(tr);

					// hello should only be equal to 'World!', not any other value, empty or nil
					await Check(tr, subspace.Key("hello"), Value("World!"), FdbValueCheckResult.Success, Value("World!"));
					await Check(tr, subspace.Key("hello"), Value("Le Monde!"), FdbValueCheckResult.Failed, Value("World!"));
					await Check(tr, subspace.Key("hello"), Slice.Nil, FdbValueCheckResult.Failed, Value("World!"));
					await Check(tr, subspace.Key("hello"), subspace.Key("hello").ToSlice(), FdbValueCheckResult.Failed, Value("World!"));
				}

				// foo should only be equal to Empty, *not* Nil or any other value
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);
					await Check(tr, subspace.Key("foo"), Slice.Empty, FdbValueCheckResult.Success, Slice.Empty);
					await Check(tr, subspace.Key("foo"), Value("bar"), FdbValueCheckResult.Failed, Slice.Empty);
					await Check(tr, subspace.Key("foo"), Slice.Nil, FdbValueCheckResult.Failed, Slice.Empty);
					await Check(tr, subspace.Key("foo"), subspace.Key("foo").ToSlice(), FdbValueCheckResult.Failed, Slice.Empty);
				}

				// not_found should only be equal to Nil, *not* Empty or any other value
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);
					await Check(tr, subspace.Key("not_found"), Slice.Nil, FdbValueCheckResult.Success, Slice.Nil);
					await Check(tr, subspace.Key("not_found"), Slice.Empty, FdbValueCheckResult.Failed, Slice.Nil);
					await Check(tr, subspace.Key("not_found"), subspace.Key("not_found").ToSlice(), FdbValueCheckResult.Failed, Slice.Nil);
				}

				// checking, changing and checking again: 2nd check should see the modified value!
				// not_found should only be equal to Nil, *not* Empty or any other value
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);

					await Check(tr, subspace.Key("hello"), Value("World!"), FdbValueCheckResult.Success, Value("World!"));
					await Check(tr, subspace.Key("not_found"), Slice.Nil, FdbValueCheckResult.Success, Slice.Nil);

					tr.Set(subspace.Key("hello"), Value("Le Monde!"));
					await Check(tr, subspace.Key("hello"), Value("Le Monde!"), FdbValueCheckResult.Success, Value("Le Monde!"));
					await Check(tr, subspace.Key("hello"), Value("World!"), FdbValueCheckResult.Failed, Value("Le Monde!"));

					tr.Set(subspace.Key("not_found"), Value("Surprise!"));
					await Check(tr, subspace.Key("not_found"), Value("Surprise!"), FdbValueCheckResult.Success, Value("Surprise!"));
					await Check(tr, subspace.Key("not_found"), Slice.Nil, FdbValueCheckResult.Failed, Value("Surprise!"));

					//note: don't commit!
				}
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
		public async Task Test_Can_Perform_Atomic_Operations()
		{
			// test that we can perform atomic mutations on keys
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				//note: we take a risk by reading the key separately, but this simplifies the rest of the code !
				Task<Slice> ResolveKey(string name) => db.ReadAsync(async tr => (await location.Resolve(tr)).Key(name).ToSlice(), this.Cancellation);

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

				// calling with an invalid mutation type should fail
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					key = await ResolveKey("invalid");
					Assert.That(() => tr.Atomic(key, Slice.FromFixed32(42), (FdbMutationType) 42), Throws.InstanceOf<NotSupportedException>());
				}
			}
		}

		[Test]
		public async Task Test_Can_AtomicAdd32()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// setup
				await db.WriteAsync(async (tr) =>
				{
					Log("resolving...");
					var subspace = await location.Resolve(tr);
					Log(subspace);
					tr.Set(subspace.Key("AAA"), Slice.FromFixed32(0));
					tr.Set(subspace.Key("BBB"), Slice.FromFixed32(1));
					tr.Set(subspace.Key("CCC"), Slice.FromFixed32(43));
					tr.Set(subspace.Key("DDD"), Slice.FromFixed32(255));
					//EEE does not exist
				}, this.Cancellation);

				// execute
				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					tr.AtomicAdd32(subspace.Key("AAA"), 1);
					tr.AtomicAdd32(subspace.Key("BBB"), 42);
					tr.AtomicAdd32(subspace.Key("CCC"), -1);
					tr.AtomicAdd32(subspace.Key("DDD"), 42);
					tr.AtomicAdd32(subspace.Key("EEE"), 42);
				}, this.Cancellation);

				// check
				_ = await db.ReadAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					Assert.That((await tr.GetAsync(subspace.Key("AAA"))).ToHexString(' '), Is.EqualTo("01 00 00 00"));
					Assert.That((await tr.GetAsync(subspace.Key("BBB"))).ToHexString(' '), Is.EqualTo("2B 00 00 00"));
					Assert.That((await tr.GetAsync(subspace.Key("CCC"))).ToHexString(' '), Is.EqualTo("2A 00 00 00"));
					Assert.That((await tr.GetAsync(subspace.Key("DDD"))).ToHexString(' '), Is.EqualTo("29 01 00 00"));
					Assert.That((await tr.GetAsync(subspace.Key("EEE"))).ToHexString(' '), Is.EqualTo("2A 00 00 00"));
					return 123;
				}, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_AtomicIncrement32()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				//await db.WriteAsync(tr => tr.ClearRange(db.GlobalSpace.ToRange()), this.Cancellation);

				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// setup
				await db.WriteAsync(async tr =>
				{
					var subspace = await location.Resolve(tr);
					tr.Set(subspace.Key("AAA"), Slice.FromFixed32(0));
					tr.Set(subspace.Key("BBB"), Slice.FromFixed32(1));
					tr.Set(subspace.Key("CCC"), Slice.FromFixed32(42));
					tr.Set(subspace.Key("DDD"), Slice.FromFixed32(255));
					//EEE does not exist
				}, this.Cancellation);

				// execute
				await db.WriteAsync(async tr =>
				{
					var subspace = await location.Resolve(tr);
					tr.AtomicIncrement32(subspace.Key("AAA"));
					tr.AtomicIncrement32(subspace.Key("BBB"));
					tr.AtomicIncrement32(subspace.Key("CCC"));
					tr.AtomicIncrement32(subspace.Key("DDD"));
					tr.AtomicIncrement32(subspace.Key("EEE"));
				}, this.Cancellation);

				// check
				await db.ReadAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					Assert.That((await tr.GetAsync(subspace.Key("AAA"))).ToHexString(' '), Is.EqualTo("01 00 00 00"));
					Assert.That((await tr.GetAsync(subspace.Key("BBB"))).ToHexString(' '), Is.EqualTo("02 00 00 00"));
					Assert.That((await tr.GetAsync(subspace.Key("CCC"))).ToHexString(' '), Is.EqualTo("2B 00 00 00"));
					Assert.That((await tr.GetAsync(subspace.Key("DDD"))).ToHexString(' '), Is.EqualTo("00 01 00 00"));
					Assert.That((await tr.GetAsync(subspace.Key("EEE"))).ToHexString(' '), Is.EqualTo("01 00 00 00"));
				}, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_AtomicAdd64()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// setup
				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					tr.Set(subspace.Key("AAA"), Slice.FromFixed64(0));
					tr.Set(subspace.Key("BBB"), Slice.FromFixed64(1));
					tr.Set(subspace.Key("CCC"), Slice.FromFixed64(43));
					tr.Set(subspace.Key("DDD"), Slice.FromFixed64(255));
					//EEE does not exist
				}, this.Cancellation);

				// execute
				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					tr.AtomicAdd64(subspace.Key("AAA"), 1);
					tr.AtomicAdd64(subspace.Key("BBB"), 42);
					tr.AtomicAdd64(subspace.Key("CCC"), -1);
					tr.AtomicAdd64(subspace.Key("DDD"), 42);
					tr.AtomicAdd64(subspace.Key("EEE"), 42);
				}, this.Cancellation);

				// check
				await db.ReadAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					Assert.That((await tr.GetAsync(subspace.Key("AAA"))).ToHexString(' '), Is.EqualTo("01 00 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(subspace.Key("BBB"))).ToHexString(' '), Is.EqualTo("2B 00 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(subspace.Key("CCC"))).ToHexString(' '), Is.EqualTo("2A 00 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(subspace.Key("DDD"))).ToHexString(' '), Is.EqualTo("29 01 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(subspace.Key("EEE"))).ToHexString(' '), Is.EqualTo("2A 00 00 00 00 00 00 00"));
				}, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_AtomicIncrement64()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// setup
				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					tr.Set(subspace.Key("AAA"), Slice.FromFixed64(0));
					tr.Set(subspace.Key("BBB"), Slice.FromFixed64(1));
					tr.Set(subspace.Key("CCC"), Slice.FromFixed64(42));
					tr.Set(subspace.Key("DDD"), Slice.FromFixed64(255));
					//EEE does not exist
				}, this.Cancellation);

				// execute
				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					tr.AtomicIncrement64(subspace.Key("AAA"));
					tr.AtomicIncrement64(subspace.Key("BBB"));
					tr.AtomicIncrement64(subspace.Key("CCC"));
					tr.AtomicIncrement64(subspace.Key("DDD"));
					tr.AtomicIncrement64(subspace.Key("EEE"));
				}, this.Cancellation);

				// check
				await db.ReadAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					Assert.That((await tr.GetAsync(subspace.Key("AAA"))).ToHexString(' '), Is.EqualTo("01 00 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(subspace.Key("BBB"))).ToHexString(' '), Is.EqualTo("02 00 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(subspace.Key("CCC"))).ToHexString(' '), Is.EqualTo("2B 00 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(subspace.Key("DDD"))).ToHexString(' '), Is.EqualTo("00 01 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(subspace.Key("EEE"))).ToHexString(' '), Is.EqualTo("01 00 00 00 00 00 00 00"));
				}, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_AtomicCompareAndClear()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// setup
				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					tr.Set(subspace.Key("AAA"), Slice.FromFixed32(0));
					tr.Set(subspace.Key("BBB"), Slice.FromFixed32(1));
					tr.Set(subspace.Key("CCC"), Slice.FromFixed32(42));
					tr.Set(subspace.Key("DDD"), Slice.FromFixed64(0));
					tr.Set(subspace.Key("EEE"), Slice.FromFixed64(1));
					//FFF does not exist
				}, this.Cancellation);

				// execute
				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					tr.AtomicCompareAndClear(subspace.Key("AAA"), Slice.FromFixed32(0));  // should be cleared
					tr.AtomicCompareAndClear(subspace.Key("BBB"), Slice.FromFixed32(0));  // should not be touched
					tr.AtomicCompareAndClear(subspace.Key("CCC"), Slice.FromFixed32(42)); // should be cleared
					tr.AtomicCompareAndClear(subspace.Key("DDD"), Slice.FromFixed64(0));  // should be cleared
					tr.AtomicCompareAndClear(subspace.Key("EEE"), Slice.FromFixed64(0));  // should not be touched
					tr.AtomicCompareAndClear(subspace.Key("FFF"), Slice.FromFixed64(42)); // should not be created
				}, this.Cancellation);

				// check
				_ = await db.ReadAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					Assert.That((await tr.GetAsync(subspace.Key("AAA"))), Is.EqualTo(Slice.Nil));
					Assert.That((await tr.GetAsync(subspace.Key("BBB"))).ToHexString(' '), Is.EqualTo("01 00 00 00"));
					Assert.That((await tr.GetAsync(subspace.Key("CCC"))), Is.EqualTo(Slice.Nil));
					Assert.That((await tr.GetAsync(subspace.Key("DDD"))), Is.EqualTo(Slice.Nil));
					Assert.That((await tr.GetAsync(subspace.Key("EEE"))).ToHexString(' '), Is.EqualTo("01 00 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(subspace.Key("FFF"))), Is.EqualTo(Slice.Nil));
					return 123;
				}, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_AppendIfFits()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				// setup
				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
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
					var subspace = await location.Resolve(tr);
					tr.AtomicAppendIfFits(subspace.Key("AAA"), Value("Hello, World!"));
					tr.AtomicAppendIfFits(subspace.Key("BBB"), Value("Hello"));
					tr.AtomicAppendIfFits(subspace.Key("BBB"), Value(", World!"));
					tr.AtomicAppendIfFits(subspace.Key("CCC"), Slice.Repeat('c', 10_000)); // should just fit exactly!
					tr.AtomicAppendIfFits(subspace.Key("DDD"), Value("!")); // should not fit!
					tr.AtomicAppendIfFits(subspace.Key("EEE"), Value("Hello, World!"));
				}, this.Cancellation);

				// check
				await db.ReadAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					Assert.That((await tr.GetAsync(subspace.Key("AAA"))).ToString(), Is.EqualTo("Hello, World!"));
					Assert.That((await tr.GetAsync(subspace.Key("BBB"))).ToString(), Is.EqualTo("BBBBBBBBBBHello, World!"));
					Assert.That((await tr.GetAsync(subspace.Key("CCC"))), Is.EqualTo(Slice.Repeat('C', 90_000) + Slice.Repeat('c', 10_000)));
					Assert.That((await tr.GetAsync(subspace.Key("DDD"))), Is.EqualTo(Slice.Repeat('D', 100_000)));
					Assert.That((await tr.GetAsync(subspace.Key("EEE"))).ToString(), Is.EqualTo("Hello, World!"));
				}, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_Snapshot_Read()
		{

			using(var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// write a bunch of keys
				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					tr.Set(subspace.Key("hello"), Value("World!"));
					tr.Set(subspace.Key("foo"), Value("bar"));
				}, this.Cancellation);

				// read them using snapshot
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);

					Slice bytes;

					bytes = await tr.Snapshot.GetAsync(subspace.Key("hello"));
					Assert.That(bytes.ToUnicode(), Is.EqualTo("World!"));

					bytes = await tr.Snapshot.GetAsync(subspace.Key("foo"));
					Assert.That(bytes.ToUnicode(), Is.EqualTo("bar"));
				}

			}

		}

		[Test]
		public async Task Test_CommittedVersion_On_ReadOnly_Transactions()
		{
			//note: until CommitAsync() is called, the value of the committed version is unspecified, but current implementation returns -1

			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
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
			}
		}

		[Test]
		public async Task Test_CommittedVersion_On_Write_Transactions()
		{
			//note: until CommitAsync() is called, the value of the committed version is unspecified, but current implementation returns -1

			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					// take the read version (to compare with the committed version below)
					long readVersion = await tr.GetReadVersionAsync();

					long ver = tr.GetCommittedVersion();
					Assert.That(ver, Is.EqualTo(-1), "Initial committed version");

					var subspace = await db.Root.Resolve(tr);
					tr.Set(subspace.Key("foo"), Value("bar"));

					// until the transaction commits, the committed version should still be -1
					ver = tr.GetCommittedVersion();
					Assert.That(ver, Is.EqualTo(-1), "Committed version after a single write");

					// committing a read only transaction

					await tr.CommitAsync();

					ver = tr.GetCommittedVersion();
					Assert.That(ver, Is.GreaterThanOrEqualTo(readVersion), "Committed version of write transaction should be >= the read version");
				}
			}
		}

		[Test]
		public async Task Test_CommittedVersion_After_Reset()
		{
			//note: until CommitAsync() is called, the value of the committed version is unspecified, but current implementation returns -1

			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					// take the read version (to compare with the committed version below)
					long rv1 = await tr.GetReadVersionAsync();

					var subspace = await db.Root.Resolve(tr);

					// do something and commit
					tr.Set(subspace.Key("foo"), Value("bar"));
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
			}
		}

		[Test]
		public async Task Test_Regular_Read_With_Concurrent_Change_Should_Conflict()
		{
			// see http://community.foundationdb.com/questions/490/snapshot-read-vs-non-snapshot-read/492

			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					tr.Set(subspace.Key("foo"), Value("foo"));
				}, this.Cancellation);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var trA = db.BeginTransaction(this.Cancellation))
				using (var trB = db.BeginTransaction(this.Cancellation))
				{
					var subspaceA = await location.Resolve(trA);
					var subspaceB = await location.Resolve(trB);

					// regular read
					_ = await trA.GetAsync(subspaceA.Key("foo"));
					trA.Set(subspaceA.Key("foo"), Value("bar"));

					// this will conflict with our read
					trB.Set(subspaceB.Key("foo"), Value("bar"));
					await trB.CommitAsync();

					// should fail with a "not_comitted" error
					Assert.That(
						async () => await trA.CommitAsync(),
						Throws.InstanceOf<FdbException>().With.Property(nameof(FdbException.Code)).EqualTo(FdbError.NotCommitted),
						"Commit should conflict !"
					);
				}
			}

		}

		[Test]
		public async Task Test_Snapshot_Read_With_Concurrent_Change_Should_Not_Conflict()
		{

			// see http://community.foundationdb.com/questions/490/snapshot-read-vs-non-snapshot-read/492

			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					tr.Set(subspace.Key("foo"), Value("foo"));
				}, this.Cancellation);

				using (var trA = db.BeginTransaction(this.Cancellation))
				using (var trB = db.BeginTransaction(this.Cancellation))
				{
					var subspaceA = await location.Resolve(trA);
					var subspaceB = await location.Resolve(trB);

					// reading with snapshot mode should not conflict
					_ = await trA.Snapshot.GetAsync(subspaceA.Key("foo"));
					trA.Set(subspaceA.Key("foo"), Value("bar"));

					// this would normally conflict with the previous read if it wasn't a snapshot read
					trB.Set(subspaceB.Key("foo"), Value("bar"));
					await trB.CommitAsync();

					// should succeed
					await trA.CommitAsync();
				}
			}

		}

		[Test]
		public async Task Test_GetRange_With_Concurrent_Change_Should_Conflict()
		{
			using(var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				Log("# Limit=1, Forward, Conflict");
				{
					// we will read the first key from [0, 100), expected 50
					// but another transaction will insert 42, in effect changing the result of our range
					// => this should conflict the GetRange

					// setup
					await db.WriteAsync(async (tr) =>
					{
						var subspace = await location.Resolve(tr);
						tr.Set(subspace.Key("foo", 50), Value("fifty"));
					}, this.Cancellation);

					// check
					using (var tr1 = db.BeginTransaction(this.Cancellation))
					{
						var subspace = await location.Resolve(tr1);

						// [0, 100) limit 1 => 50
						var kvp = await tr1
							.GetRange(subspace.Key("foo"), subspace.Key("foo", 100))
							.FirstOrDefaultAsync();
						Assert.That(kvp.Key, Is.EqualTo(subspace.Key("foo", 50)));

						// 42 < 50 > conflict !!!
						using (var tr2 = db.BeginTransaction(this.Cancellation))
						{
							var subspace2 = await location.Resolve(tr2);
							tr2.Set(subspace2.Key("foo", 42), Value("forty-two"));
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
						var subspace = await location.Resolve(tr);
						tr.ClearRange(subspace);
						tr.Set(subspace.Key("foo", 50), Value("fifty"));
					}, this.Cancellation);

					// check
					using (var tr1 = db.BeginTransaction(this.Cancellation))
					{
						var subspace = await location.Resolve(tr1);

						// [0, 100) limit 1 => 50
						var kvp = await tr1
							.GetRange(subspace.Key("foo"), subspace.Key("foo", 100))
							.FirstOrDefaultAsync();
						Assert.That(kvp.Key, Is.EqualTo(subspace.Key("foo", 50)));

						// 77 > 50 => no conflict
						using (var tr2 = db.BeginTransaction(this.Cancellation))
						{
							var subspace2 = await location.Resolve(tr2);
							tr2.Set(subspace2.Key("foo", 77), Value("docm"));
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
						var subspace = await location.Resolve(tr);
						tr.ClearRange(subspace);
						tr.Set(subspace.Key("foo", 50), Value("fifty"));
					}, this.Cancellation);

					// check
					using (var tr1 = db.BeginTransaction(this.Cancellation))
					{
						var subspace = await location.Resolve(tr1);

						// [0, 100) limit 1 => 50
						var kvp = await tr1
							.GetRange(subspace.Key("foo"), subspace.Key("foo", 100))
							.LastOrDefaultAsync();

						Assert.That(kvp.Key, Is.EqualTo(subspace.Key("foo", 50)));

						// 37 < 50 => no conflict
						using (var tr2 = db.BeginTransaction(this.Cancellation))
						{
							var subspace2 = await location.Resolve(tr2);
							tr2.Set(subspace2.Key("foo", 77), Value("docm"));
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
						var subspace = await location.Resolve(tr);
						tr.ClearRange(subspace);
						tr.Set(subspace.Key("foo", 50), Value("fifty"));
					}, this.Cancellation);

					// check
					using (var tr1 = db.BeginTransaction(this.Cancellation))
					{
						var subspace = await location.Resolve(tr1);

						// [0, 100) limit 1 => 50
						var kvp = await tr1
							.GetRange(subspace.Key("foo"), subspace.Key("foo", 100))
							.LastOrDefaultAsync();

						Assert.That(kvp.Key, Is.EqualTo(subspace.Key("foo", 50)));

						// 37 < 50 => no conflict
						using (var tr2 = db.BeginTransaction(this.Cancellation))
						{
							var subspace2 = await location.Resolve(tr2);
							tr2.Set(subspace2.Key("foo", 37), Value("totally_random_number"));
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
						var subspace = await location.Resolve(tr);
						tr.ClearRange(subspace);
						tr.Set(subspace.Key("foo", 49), Value("forty nine"));
						tr.Set(subspace.Key("foo", 50), Value("fifty"));
						tr.Set(subspace.Key("foo", 51), Value("fifty one"));
					}, this.Cancellation);

					// check conflict
					using (var tr1 = db.BeginTransaction(this.Cancellation))
					{
						var subspace = await location.Resolve(tr1);

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
							var subspace2 = await location.Resolve(tr2);
							tr2.Set(subspace2.Key("foo", 37), Value("totally_random_number"));
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
						var subspace = await location.Resolve(tr);
						tr.ClearRange(subspace);
						tr.Set(subspace.Key("foo", 49), Value("forty nine"));
						tr.Set(subspace.Key("foo", 50), Value("fifty"));
						tr.Set(subspace.Key("foo", 51), Value("fifty one"));
					}, this.Cancellation);

					// check no conflict
					using (var tr1 = db.BeginTransaction(this.Cancellation))
					{
						var subspace = await location.Resolve(tr1);

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
							var subspace2 = await location.Resolve(tr2);
							tr2.Set(subspace2.Key("foo", 77), Value("docm"));
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
						var subspace = await location.Resolve(tr);
						tr.ClearRange(subspace);
						tr.Set(subspace.Key("foo", 49), Value("forty nine"));
						tr.Set(subspace.Key("foo", 50), Value("fifty"));
						tr.Set(subspace.Key("foo", 51), Value("fifty one"));
					}, this.Cancellation);

					// check conflict
					using (var tr1 = db.BeginTransaction(this.Cancellation))
					{
						var subspace = await location.Resolve(tr1);

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
							var subspace2 = await location.Resolve(tr2);
							tr2.Set(subspace2.Key("foo", 77), Value("conflict"));
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
						var subspace = await location.Resolve(tr);
						tr.ClearRange(subspace);
						tr.Set(subspace.Key("foo", 49), Value("forty nine"));
						tr.Set(subspace.Key("foo", 50), Value("fifty"));
						tr.Set(subspace.Key("foo", 51), Value("fifty one"));
					}, this.Cancellation);

					// check no conflict
					using (var tr1 = db.BeginTransaction(this.Cancellation))
					{
						var subspace = await location.Resolve(tr1);

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
							var subspace2 = await location.Resolve(tr2);
							tr2.Set(subspace2.Key("foo", 37), Value("totally_random_number"));
							await tr2.CommitAsync();
						}

						// we need to write something to force a conflict
						tr1.Set(subspace.Key("bar"), Slice.Empty);

						// should not conflict!
						Assert.That(async () => await tr1.CommitAsync(), Throws.Nothing, "Transaction should not conflict because the mutation does not change the result of the GetRange!");
					}
				}
			}
		}

		[Test]
		public async Task Test_GetKey_With_Concurrent_Change_Should_Conflict()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					tr.ClearRange(subspace);
					tr.Set(subspace.Key("foo", 50), Value("fifty"));
				}, this.Cancellation);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// we will ask for the first key from >= 0, expecting 50, but if another transaction inserts something BEFORE 50, our key selector would have returned a different result, causing a conflict

				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr1);
					// fGE{0} => 50
					var key = await tr1.GetKeyAsync(subspace.Key("foo", 0).FirstGreaterOrEqual());
					Assert.That(key, Is.EqualTo(subspace.Key("foo", 50)));

					// 42 < 50 => conflict !!!
					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						var subspace2 = await location.Resolve(tr2);
						tr2.Set(subspace2.Key("foo", 42), Value("forty-two"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(subspace.Key("bar"), Slice.Empty);

					Assert.That(
						async () => await tr1.CommitAsync(),
						Throws.InstanceOf<FdbException>().With.Property(nameof(FdbException.Code)).EqualTo(FdbError.NotCommitted),
						"The Set(42) in TR2 should have conflicted with the GetKey(fGE{0}) in TR1"
					);
				}

				// if the other transaction insert something AFTER 50, our key selector would have still returned the same result, and we would have any conflict

				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					tr.ClearRange(subspace);
					tr.Set(subspace.Key("foo", 50), Value("fifty"));
				}, this.Cancellation);

				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr1);
					// fGE{0} => 50
					var key = await tr1.GetKeyAsync(FdbKeySelector.FirstGreaterOrEqual(subspace.Key("foo", 0)));
					Assert.That(key, Is.EqualTo(subspace.Key("foo", 50).ToSlice()));

					// 77 > 50 => no conflict
					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						var subspace2 = await location.Resolve(tr2);
						tr2.Set(subspace2.Key("foo", 77), Value("docm"));
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
					var subspace = await location.Resolve(tr);
					tr.ClearRange(subspace);
					tr.Set(subspace.Key("foo", 50), Value("fifty"));
					tr.Set(subspace.Key("foo", 100), Value("one hundred"));
				}, this.Cancellation);

				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr1);

					// fGE{50} + 1 => 100
					var key = await tr1.GetKeyAsync(subspace.Key("foo", 50).FirstGreaterOrEqual() + 1);
					Assert.That(key, Is.EqualTo(subspace.Key("foo", 100).ToSlice()));

					// 77 between 50 and 100 => conflict !!!
					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						var subspace2 = await location.Resolve(tr2);
						tr2.Set(subspace2.Key("foo", 77), Value("docm"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(subspace.Key("bar"), Slice.Empty);

					// should conflict!
					Assert.That(
						async () => await tr1.CommitAsync(),
						Throws.InstanceOf<FdbException>().With.Property(nameof(FdbException.Code)).EqualTo(FdbError.NotCommitted),
						"The Set(77) in TR2 should have conflicted with the GetKey(fGE{50} + 1) in TR1"
					);
				}

				// does conflict arise from changes in VALUES in the database? or from changes in RESULTS to user queries ?

				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					tr.ClearRange(subspace);
					tr.Set(subspace.Key("foo", 50), Value("fifty"));
					tr.Set(subspace.Key("foo", 100), Value("one hundred"));
				}, this.Cancellation);

				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr1);
					// fGT{50} => 100
					var key = await tr1.GetKeyAsync(subspace.Key("foo", 50).FirstGreaterThan());
					Assert.That(key, Is.EqualTo(subspace.Key("foo", 100)));

					// another transaction changes the VALUE of 50 and 100 (but does not change the fact that they exist nor add keys in between)
					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						var subspace2 = await location.Resolve(tr2);
						tr2.Set(subspace2.Key("foo", 100), Value("cent"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(subspace.Key("bar"), Slice.Empty);

					// this causes a conflict in the current version of FDB
					Assert.That(
						async () => await tr1.CommitAsync(),
						Throws.InstanceOf<FdbException>().With.Property(nameof(FdbException.Code)).EqualTo(FdbError.NotCommitted),
						"The Set(100) in TR2 should have conflicted with the GetKey(fGT{50}) in TR1"
					);
				}

				// LastLessThan does not create conflicts if the pivot key is changed

				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					tr.ClearRange(subspace);
					tr.Set(subspace.Key("foo", 50), Value("fifty"));
					tr.Set(subspace.Key("foo", 100), Value("one hundred"));
				}, this.Cancellation);

				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr1);
					// lLT{100} => 50
					var key = await tr1.GetKeyAsync(subspace.Key("foo", 100).LastLessThan());
					Assert.That(key, Is.EqualTo(subspace.Key("foo", 50)));

					// another transaction changes the VALUE of 50 and 100 (but does not change the fact that they exist nor add keys in between)
					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						var subspace2 = await location.Resolve(tr2);
						tr2.Clear(subspace2.Key("foo", 100));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(subspace.Key("bar"), Slice.Empty);

					// this causes a conflict in the current version of FDB
					await tr1.CommitAsync();
				}

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

			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
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

					Assert.That(value, Is.Not.EqualTo(Slice.Nil));
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

					Assert.That(value, Is.Not.EqualTo(Slice.Nil));
					Assert.That(value.ToInt32(), Is.EqualTo(2), "T1 should have seen the value modified by T2");

					// committing should not conflict, because we read the value AFTER it was changed
					await tr1.CommitAsync();
				}

			}
		}

		[Test]
		public async Task Test_Read_Isolation_From_Writes()
		{
			// What we expected by default:
			// - Regular reads see the writes made by the transaction itself, but not the writes made by other transactions that committed in between
			// - Snapshot reads never see the writes made since the transaction read version, but will see the writes made by the transaction itself

			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				// Reads (before and after):
				// - A and B will use regular reads
				// - C and D will use snapshot reads
				// Writes:
				// - A and C will be modified by the transaction itself
				// - B and D will be modified by a different transaction

				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					tr.Set(subspace.Key("A"), Value("a"));
					tr.Set(subspace.Key("B"), Value("b"));
					tr.Set(subspace.Key("C"), Value("c"));
					tr.Set(subspace.Key("D"), Value("d"));
				}, this.Cancellation);

				DumpStore(store, "Initial db state");

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);

					// check initial state
					Assert.That((await tr.GetAsync(subspace.Key("A"))).ToStringUtf8(), Is.EqualTo("a"));
					Assert.That((await tr.GetAsync(subspace.Key("B"))).ToStringUtf8(), Is.EqualTo("b"));
					Assert.That((await tr.Snapshot.GetAsync(subspace.Key("C"))).ToStringUtf8(), Is.EqualTo("c"));
					Assert.That((await tr.Snapshot.GetAsync(subspace.Key("D"))).ToStringUtf8(), Is.EqualTo("d"));

					// mutate (not yet committed)
					tr.Set(subspace.Key("A"), Value("aa"));
					tr.Set(subspace.Key("C"), Value("cc"));
					await db.WriteAsync((tr2) =>
					{ // have another transaction change B and D under our nose
						tr2.Set(subspace.Key("B"), Value("bb"));
						tr2.Set(subspace.Key("D"), Value("dd"));
					}, this.Cancellation);

					// check what the transaction sees
					Assert.That((await tr.GetAsync(subspace.Key("A"))).ToStringUtf8(), Is.EqualTo("aa"), "The transaction own writes should change the value of regular reads");
					Assert.That((await tr.GetAsync(subspace.Key("B"))).ToStringUtf8(), Is.EqualTo("b"), "Other transaction writes should not change the value of regular reads");
					Assert.That((await tr.Snapshot.GetAsync(subspace.Key("C"))).ToStringUtf8(), Is.EqualTo("cc"), "The transaction own writes should be visible in snapshot reads");
					Assert.That((await tr.Snapshot.GetAsync(subspace.Key("D"))).ToStringUtf8(), Is.EqualTo("d"), "Other transaction writes should not change the value of snapshot reads");

					//note: committing here would conflict
				}
			}
		}

		[Test]
		public async Task Test_Read_Isolation_From_Writes_Pre_300()
		{
			// By in API v200 and below:
			// - Regular reads see the writes made by the transaction itself, but not the writes made by other transactions that committed in between
			// - Snapshot reads never see the writes made since the transaction read version, but will see the writes made by the transaction itself
			// In API 300, this can be emulated by setting the SnapshotReadYourWriteDisable options

			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				// Reads (before and after):
				// - A and B will use regular reads
				// - C and D will use snapshot reads
				// Writes:
				// - A and C will be modified by the transaction itself
				// - B and D will be modified by a different transaction

				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					tr.Set(subspace.Key("A"), Value("a"));
					tr.Set(subspace.Key("B"), Value("b"));
					tr.Set(subspace.Key("C"), Value("c"));
					tr.Set(subspace.Key("D"), Value("d"));
				}, this.Cancellation);

				//Log("Initial db state:");
				//await DumpSubspace(db, location);

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);

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
		}

		[Test]
		public async Task Test_ReadYourWritesDisable_Isolation()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				#region Default behaviour...

				// By default, a transaction see its own writes with non-snapshot reads

				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					tr.Set(subspace.Key("a"), Value("a"));
					tr.Set(subspace.Key("b", 10), Value("PRINT \"HELLO\""));
					tr.Set(subspace.Key("b", 20), Value("GOTO 10"));
				}, this.Cancellation);

				using(var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);

					var data = await tr.GetAsync(subspace.Key("a"));
					Assert.That(data.ToUnicode(), Is.EqualTo("a"));
					
					var res = await tr.GetRange(subspace.Key("b").ToRange()).Select(kvp => kvp.Value.ToString()).ToArrayAsync();
					Assert.That(res, Is.EqualTo([ "PRINT \"HELLO\"", "GOTO 10" ]));

					tr.Set(subspace.Key("a"), Value("aa"));
					tr.Set(subspace.Key("b", 15), Value("PRINT \"WORLD\""));

					data = await tr.GetAsync(subspace.Key("a"));
					Assert.That(data.ToUnicode(), Is.EqualTo("aa"), "The transaction own writes should be visible by default");
					res = await tr.GetRange(subspace.Key("b").ToRange()).Select(kvp => kvp.Value.ToString()).ToArrayAsync();
					Assert.That(res, Is.EqualTo([ "PRINT \"HELLO\"", "PRINT \"WORLD\"", "GOTO 10" ]), "The transaction own writes should be visible by default");

					//note: don't commit
				}

				#endregion

				#region ReadYourWritesDisable behaviour...

				// The ReadYourWritesDisable option cause reads to always return the value in the database

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);

					tr.Options.SetOption(FdbTransactionOption.ReadYourWritesDisable);

					var data = await tr.GetAsync(subspace.Key("a"));
					Assert.That(data.ToUnicode(), Is.EqualTo("a"));
					var res = await tr.GetRange(subspace.Key("b").ToRange()).Select(kvp => kvp.Value.ToString()).ToArrayAsync();
					Assert.That(res, Is.EqualTo([ "PRINT \"HELLO\"", "GOTO 10" ]));

					tr.Set(subspace.Key("a"), Value("aa"));
					tr.Set(subspace.Key("b", 15), Value("PRINT \"WORLD\""));

					data = await tr.GetAsync(subspace.Key("a"));
					Assert.That(data.ToUnicode(), Is.EqualTo("a"), "The transaction own writes should not be seen with ReadYourWritesDisable option enabled");
					res = await tr.GetRange(subspace.Key("b").ToRange()).Select(kvp => kvp.Value.ToString()).ToArrayAsync();
					Assert.That(res, Is.EqualTo([ "PRINT \"HELLO\"", "GOTO 10" ]), "The transaction own writes should not be seen with ReadYourWritesDisable option enabled");

					//note: don't commit
				}

				#endregion
			}
		}

		[Test]
		public async Task Test_Can_Set_Read_Version()
		{
			// Verify that we can set a read version on a transaction
			// * tr1 will set value to 1
			// * tr2 will set value to 2
			// * tr3 will SetReadVersion(TR1.CommittedVersion) and we expect it to read 1 (and not 2)

			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				long committedVersion;

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// create first version
				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr1);
					tr1.Set(subspace.Key("concurrent"), Slice.FromByte(1));
					await tr1.CommitAsync();

					// get this version
					committedVersion = tr1.GetCommittedVersion();
				}

				// mutate in another transaction
				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr2);
					tr2.Set(subspace.Key("concurrent"), Slice.FromByte(2));
					await tr2.CommitAsync();
				}

				// read the value with TR1's committed version
				using (var tr3 = db.BeginTransaction(this.Cancellation))
				{
					tr3.SetReadVersion(committedVersion);

					long ver = await tr3.GetReadVersionAsync();
					Assert.That(ver, Is.EqualTo(committedVersion), "GetReadVersion should return the same value as SetReadVersion!");

					var subspace = await location.Resolve(tr3);
					var bytes = await tr3.GetAsync(subspace.Key("concurrent"));

					Assert.That(bytes.GetBytes(), Is.EqualTo(new byte[] { 1 }), "Should have seen the first version!");
				}

			}

		}

		[Test]
		public async Task Test_Has_Access_To_System_Keys()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr = db.BeginTransaction(this.Cancellation))
				{

					// should fail if access to system keys has not been requested

					Assert.That(
						async () => await tr.GetRange(Key("\xFF"), Key("\xFF\xFF"), new FdbRangeOptions { Limit = 10 }).ToListAsync(),
						Throws.InstanceOf<FdbException>().With.Property(nameof(FdbException.Code)).EqualTo(FdbError.KeyOutsideLegalRange),
						"Should not have access to system keys by default"
					);

					// should succeed once system access has been requested
					tr.Options.WithReadAccessToSystemKeys();

					var keys = await tr.GetRange(Key("\xFF"), Key("\xFF\xFF"), new FdbRangeOptions { Limit = 10 }).ToListAsync();
					Assert.That(keys, Is.Not.Null);
				}
			}
		}

		[Test]
		public void Test_Can_Set_Transaction_Options()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					Assert.That(tr.Options.Timeout, Is.Zero, "Timeout (default)");
					Assert.That(tr.Options.RetryLimit, Is.Zero, "RetryLimit (default)");
					Assert.That(tr.Options.MaxRetryDelay, Is.Zero, "MaxRetryDelay (default)");
					Assert.That(tr.Options.Tracing, Is.EqualTo(FdbTracingOptions.Default), "Tracing (default)");

					tr.Options.Timeout = 1_000; // 1 sec max
					tr.Options.RetryLimit = 5; // 5 retries max
					tr.Options.MaxRetryDelay = 500; // .5 sec max
					tr.Options.Tracing = FdbTracingOptions.RecordTransactions | FdbTracingOptions.RecordOperations | FdbTracingOptions.RecordApiCalls;

					Assert.That(tr.Options.Timeout, Is.EqualTo(1_000), "Timeout");
					Assert.That(tr.Options.RetryLimit, Is.EqualTo(5), "RetryLimit");
					Assert.That(tr.Options.MaxRetryDelay, Is.EqualTo(500), "MaxRetryDelay");
					Assert.That(tr.Options.Tracing, Is.EqualTo(FdbTracingOptions.RecordTransactions | FdbTracingOptions.RecordOperations | FdbTracingOptions.RecordApiCalls), "Tracing");
				}
			}
		}

		[Test]
		public void Test_Transaction_Options_Inherit_Default_From_Database()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				Assert.That(db.Options.DefaultTimeout, Is.Zero, "db.DefaultTimeout (default)");
				Assert.That(db.Options.DefaultRetryLimit, Is.Zero, "db.DefaultRetryLimit (default)");
				Assert.That(db.Options.DefaultMaxRetryDelay, Is.Zero, "db.DefaultMaxRetryDelay (default)");
				Assert.That(db.Options.DefaultTracing, Is.EqualTo(FdbTracingOptions.Default), "db.DefaultTracing (default)");

				db.Options.DefaultTimeout = 500;
				db.Options.DefaultRetryLimit = 3;
				db.Options.DefaultMaxRetryDelay = 600;
				db.Options.DefaultTracing = FdbTracingOptions.RecordTransactions | FdbTracingOptions.RecordOperations;

				Assert.That(db.Options.DefaultTimeout, Is.EqualTo(500), "db.DefaultTimeout");
				Assert.That(db.Options.DefaultRetryLimit, Is.EqualTo(3), "db.DefaultRetryLimit");
				Assert.That(db.Options.DefaultMaxRetryDelay, Is.EqualTo(600), "db.DefaultMaxRetryDelay");
				Assert.That(db.Options.DefaultTracing, Is.EqualTo(FdbTracingOptions.RecordTransactions | FdbTracingOptions.RecordOperations), "db.DefaultTracing");

				// transaction should be already configured with the default options

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					Assert.That(tr.Options.Timeout, Is.EqualTo(500), "tr.Timeout");
					Assert.That(tr.Options.RetryLimit, Is.EqualTo(3), "tr.RetryLimit");
					Assert.That(tr.Options.MaxRetryDelay, Is.EqualTo(600), "tr.MaxRetryDelay");
					Assert.That(tr.Options.Tracing, Is.EqualTo(FdbTracingOptions.RecordTransactions | FdbTracingOptions.RecordOperations), "tr.Tracing");

					// changing the default on the db should only affect new transactions

					db.Options.DefaultTimeout = 600;
					db.Options.DefaultRetryLimit = 4;
					db.Options.DefaultMaxRetryDelay = 700;
					db.Options.DefaultTracing = FdbTracingOptions.RecordApiCalls | FdbTracingOptions.RecordSteps;

					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						Assert.That(tr2.Options.Timeout, Is.EqualTo(600), "tr2.Options.Timeout");
						Assert.That(tr2.Options.RetryLimit, Is.EqualTo(4), "tr2.Options.RetryLimit");
						Assert.That(tr2.Options.MaxRetryDelay, Is.EqualTo(700), "tr2.Options.MaxRetryDelay");
						Assert.That(tr2.Options.Tracing, Is.EqualTo(FdbTracingOptions.RecordApiCalls | FdbTracingOptions.RecordSteps), "tr2.Options.Tracing");

						// original transaction should not be affected
						Assert.That(tr.Options.Timeout, Is.EqualTo(500), "tr.Options.Timeout");
						Assert.That(tr.Options.RetryLimit, Is.EqualTo(3), "tr.Options.RetryLimit");
						Assert.That(tr.Options.MaxRetryDelay, Is.EqualTo(600), "tr.Options.MaxRetryDelay");
						Assert.That(tr.Options.Tracing, Is.EqualTo(FdbTracingOptions.RecordTransactions | FdbTracingOptions.RecordOperations), "tr.Options.Tracing");
					}

					// resetting the transaction should use the new database settings
					tr.Reset();
					Assert.That(tr.Options.Timeout, Is.EqualTo(600), "tr.Options.Timeout (after reset)");
					Assert.That(tr.Options.RetryLimit, Is.EqualTo(4), "tr.Options.RetryLimit (after reset)");
					Assert.That(tr.Options.MaxRetryDelay, Is.EqualTo(700), "tr.Options.MaxRetryDelay (after reset)");
					Assert.That(tr.Options.Tracing, Is.EqualTo(FdbTracingOptions.RecordApiCalls | FdbTracingOptions.RecordSteps), "tr.Options.Tracing (after reset)");
				}
			}
		}

		[Test]
		public async Task Test_Transaction_RetryLoop_Respects_DefaultRetryLimit_Value()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			using (var go = new CancellationTokenSource())
			{
				Assert.That(db.DefaultTimeout, Is.Zero, "db.DefaultTimeout (default)");
				Assert.That(db.DefaultRetryLimit, Is.Zero, "db.DefaultRetryLimit (default)");

				// By default, a transaction that gets reset or retried, clears the RetryLimit and Timeout settings, which needs to be reset everytime.
				// But if the DefaultRetryLimit and DefaultTimeout are set on the database instance, they should automatically be re-applied inside transaction loops!
				db.DefaultRetryLimit = 3;

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

					Assert.That(tr.Options.RetryLimit, Is.EqualTo(3));

					// simulate a retryable error condition
					throw new FdbException(FdbError.TransactionTooOld);
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
		}

		[Test]
		public async Task Test_Transaction_RetryLoop_Resets_RetryLimit_And_Timeout()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
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
		}

		[Test]
		public async Task Test_Can_Add_Read_Conflict_Range()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr1);

					await tr1.GetAsync(subspace.Key(1));
					// tr1 writes to one key
					tr1.Set(subspace.Key(1), Value("hello"));
					// but add the second as a conflict range
					tr1.AddReadConflictKey(subspace.Key(2));

					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						var subspace2 = await location.Resolve(tr2);

						// tr2 writes to the second key
						tr2.Set(subspace2.Key(2), Value("world"));

						// tr2 should succeed
						await tr2.CommitAsync();
					}

					// tr1 should conflict on the second key
					Assert.That(
						async () => await tr1.CommitAsync(),
						Throws.InstanceOf<FdbException>().With.Property(nameof(FdbException.Code)).EqualTo(FdbError.NotCommitted),
						"Transaction should have resulted in a conflict on key2"
					);
				}
			}
		}

		[Test]
		public async Task Test_Can_Add_Write_Conflict_Range()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr1);

					// tr1 reads the conflicting key
					await tr1.GetAsync(subspace.Key(0));
					// and writes to key1
					tr1.Set(subspace.Key(1), Value("hello"));

					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						var subspace2 = await location.Resolve(tr2);

						// tr2 changes key2, but adds a conflict range on the conflicting key
						tr2.Set(subspace2.Key(2), Value("world"));

						// and writes on the third
						tr2.AddWriteConflictKey(subspace2.Key(0)); // conflict!

						await tr2.CommitAsync();
					}

					// tr1 should conflict
					Assert.That(
						async () => await tr1.CommitAsync(),
						Throws.InstanceOf<FdbException>().With.Property(nameof(FdbException.Code)).EqualTo(FdbError.NotCommitted),
						"Transaction should have resulted in a conflict"
					);
				}
			}
		}

		[Test]
		public async Task Test_Can_Setup_And_Cancel_Watches()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				await db.WriteAsync(async tr =>
				{
					var subspace = await location.Resolve(tr);
					tr.Set(subspace.Key("watched"), Value("some value"));
					tr.Set(subspace.Key("witness"), Value("some other value"));
				}, this.Cancellation);

				using (var cts = new CancellationTokenSource())
				{
					FdbWatch w1;
					FdbWatch w2;

					using (var tr = db.BeginTransaction(this.Cancellation))
					{
						var subspace = await location.Resolve(tr);
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
						var subspace = await location.Resolve(tr);
						tr.Set(subspace.Key("watched"), Value("some new value"));
					}, this.Cancellation);

					// the first watch should have triggered
					await Task.Delay(100, this.Cancellation);
					Assert.That(w1.Task.Status, Is.EqualTo(TaskStatus.RanToCompletion), "w1 should have been triggered because key1 was changed");
					Assert.That(w2.Task.Status, Is.EqualTo(TaskStatus.WaitingForActivation), "w2 should still be pending because key2 was untouched");

					// cancelling the token associated to the watch should cancel them
					await cts.CancelAsync();

					await Task.Delay(100, this.Cancellation);
					Assert.That(w2.Task.Status, Is.EqualTo(TaskStatus.Canceled), "w2 should have been cancelled");
				}
			}
		}

		[Test]
		public async Task Test_Cannot_Use_Transaction_CancellationToken_With_Watch()
		{
			// tr.Watch(..., tr.Cancellation) is forbidden, because the watch would not survive the transaction

			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);
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
		}

		[Test]
		public async Task Test_Setting_Key_To_Same_Value_Should_Not_Trigger_Watch()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

#if ENABLE_LOGGING
				db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

				Log("Set to initial value...");
				await db.WriteAsync(async tr =>
				{
					var subspace = await location.Resolve(tr);
					tr.Set(subspace.Key("watched"), Text("initial value"));
				}, this.Cancellation);

				Log("Create watch...");
				var w = await db.ReadWriteAsync(async tr =>
				{
					var subspace = await location.Resolve(tr);
					return tr.Watch(subspace.Key("watched"), this.Cancellation);
				}, this.Cancellation);
				Assert.That(w.IsAlive, Is.True, "Watch should still be alive");
				Assert.That(w.Task.Status, Is.EqualTo(TaskStatus.WaitingForActivation));

				// change the key to the same value
				Log("Set to same value...");
				await db.WriteAsync(async tr =>
				{
					var subspace = await location.Resolve(tr);
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
					var subspace = await location.Resolve(tr);
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
		}

		[Test]
		public async Task Test_Watched_Key_Changed_By_Same_Transaction_Before_Commit_Should_Trigger_Watch()
		{
			// Steps:
			// - T1: set a watch on a key, but does not commit yet
			// - T1: change the value of the watched key
			// - T1: commit
			// Expect:
			// - Watch should fire as soon as T1 commits

			var store = new FakeDbStore();
			using var db = store.OpenDatabase(FdbPath.Root, readOnly: false);
			var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

			Log("Set to initial value...");
			await db.WriteAsync(async tr =>
			{
				var subspace = await location.Resolve(tr);
				tr.Set(subspace.Key("watched"), Text("initial value"));
			}, this.Cancellation);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			using var tr = db.BeginTransaction(this.Cancellation);

			var subspace = await location.Resolve(tr);

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
			// Steps:
			// - T1: set a watch on a key, but do not commit yet
			// - T2: update the watched key and commit before T1
			// - T1: commit after T2
			// Expect:
			// - Watch should fire as soon as T1 commits

			var store = new FakeDbStore();
			using var db = store.OpenDatabase(FdbPath.Root, readOnly: false);
			var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

			Log("Set to initial value...");
			await db.WriteAsync(async tr =>
			{
				var subspace = await location.Resolve(tr);
				tr.Set(subspace.Key("watched"), Text("initial value"));
			}, this.Cancellation);

#if ENABLE_LOGGING
			db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

			using var tr1 = db.BeginTransaction(this.Cancellation);

			var subspace1 = await location.Resolve(tr1);

			Log("T1: Create watch");
			var w = tr1.Watch(subspace1.Key("watched"), this.Cancellation);

			// T2: change the key to the same value
			using (var tr2 = db.BeginTransaction(this.Cancellation))
			{
				var subspace2 = await location.Resolve(tr2);
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
		public async Task Test_Can_Get_Addresses_For_Key()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					tr.Set(subspace.Key(1), Value("one"));
				}, this.Cancellation);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// look for the address of key1
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);

					var addresses = await tr.GetAddressesForKeyAsync(subspace.Key(1));
					Assert.That(addresses, Is.Not.Null);
					Log($"{subspace.Key(1)} is stored at: {string.Join(", ", addresses)}");
					Assert.That(addresses.Length, Is.GreaterThan(0));
					Assert.That(addresses[0], Is.Not.Null.Or.Empty);

					//note: it is difficult to test the returned value, because it depends on the test db configuration
					// it will most probably be 127.0.0.1 unless you have customized the Test DB settings to point to somewhere else
					// either way, it should look like a valid IP address (IPv4 or v6?)

					for (int i = 0; i < addresses.Length; i++)
					{
						Assert.That(System.Net.IPAddress.TryParse(addresses[i], out var address), Is.True, $"Result address {addresses[i]} does not seem to be a valid IP address");
						Log($"- {address}");
					}
				}

				// do the same but for a key that does not exist
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);

					var addresses = await tr.GetAddressesForKeyAsync(subspace.Key(404));
					Assert.That(addresses, Is.Not.Null);
					Log($"{subspace.Key(404)} would be stored at: {string.Join(", ", addresses)}");

					// the API still return a list of addresses, probably of servers that would store this value if you would call Set(...)

					for (int i = 0; i < addresses.Length; i++)
					{
						Assert.That(System.Net.IPAddress.TryParse(addresses[i], out var address), Is.True, $"Result address {addresses[i]} does not seem to be a valid IP address");
						Log($"- {address}");
					}

				}
			}

		}

		[Test]
		public async Task Test_Can_Get_Boundary_Keys()
		{
			using (var db = new FakeDbStore().OpenDatabase(FdbPath.Root, readOnly: false))
			{
				//var cf = await db.GetCoordinatorsAsync();
				//Log("Connected to {0}", cf.ToString());

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

					HashSet<string> distinctNodes = new(StringComparer.Ordinal);
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
						for(int i=0;i<n;i++)
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
					foreach(var machine in distinctNodes)
					{
						Log("- " + machine);
					}
					Log();
					Log($"Cluster topology: {distinctNodes.Count} process(es) with {(replicationFactor == 1 ? "single" : replicationFactor == 2 ? "double" : replicationFactor == 3 ? "triple" : replicationFactor.ToString())} replication");
				}
			}
		}

		[Test]
		public void Test_VersionStamps_Share_The_Same_Token_Per_Transaction_Attempt()
		{
			// Verify that we can set version-stamped keys inside a transaction

			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
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
			}
		}

		[Test]
		public async Task Test_VersionStamp_Operations()
		{
			// Verify that we can set version-stamped keys inside a transaction

			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				VersionStamp vsActual; // will contain the actual version stamp used by the database

				Log("Inserting keys with version stamps:");
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);

					// should return an 80-bit incomplete stamp, using a random token
					var vs = tr.CreateVersionStamp();
					Log($"> placeholder stamp: {vs} with token '{vs.ToSlice():X}'");

					// a single key using the 80-bit stamp
					tr.SetVersionStampedKey(subspace.Key("foo", vs, 123), Value("Hello, World!"));

					// simulate a batch of 3 keys, using 96-bits stamps
					tr.SetVersionStampedKey(subspace.Key("bar", tr.CreateVersionStamp(0)), Value("Zero"));
					tr.SetVersionStampedKey(subspace.Key("bar", tr.CreateVersionStamp(1)), Value("One"));
					tr.SetVersionStampedKey(subspace.Key("bar", tr.CreateVersionStamp(42)), Value("FortyTwo"));

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

				//await DumpSubspace(db, location);

				Log("Checking database content:");
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var subspace = await location.Resolve(tr);
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
		}

		[Test]
		public async Task Test_GetMetadataVersion()
		{
			//note: this test may be vulnerable to exterior changes to the database!
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
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
		}

		[Test]
		public async Task Test_GetMetadataVersion_Custom_Keys()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
#if ENABLE_LOGGING
				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));
#endif

				// initial setup:
				// - Foo: version stamp
				// - Bar: different version stamp
				// - Baz: _missing_

				await db.WriteAsync(async tr =>
				{
					var subspace = await db.Root.Resolve(tr);
					tr.TouchMetadataVersionKey(subspace.Key("Foo"));
				}, this.Cancellation);
				DumpStore(store, "After touch Foo");

				await db.WriteAsync(async tr =>
				{
					var subspace = await db.Root.Resolve(tr);
					tr.TouchMetadataVersionKey(subspace.Key("Bar"));
				}, this.Cancellation);
				DumpStore(store, "After touch Bar");

				await db.WriteAsync(async tr =>
				{
					var subspace = await db.Root.Resolve(tr);
					tr.Clear(subspace.Key("Baz"));
				}, this.Cancellation);

				DumpStore(store, "After clear Baz");

				// changing the metadata version and then reading it back from the same transaction CANNOT WORK!
				await db.WriteAsync(async tr =>
				{
					var subspace = await db.Root.Resolve(tr);

					// We can read the version before
					var before1 = await tr.GetMetadataVersionKeyAsync(subspace.Key("Foo"));
					Log($"Foo (before): {before1}");
					Assert.That(before1, Is.Not.Null);

					// Another read attempt should return the cached value
					var before2 = await tr.GetMetadataVersionKeyAsync(subspace.Key("Bar"));
					Log($"Bar (before): {before2}");
					Assert.That(before2, Is.Not.Null.And.Not.EqualTo(before1));

					// Another read attempt should return the cached value
					var before3 = await tr.GetMetadataVersionKeyAsync(subspace.Key("Baz"));
					Log($"Baz (before): {before3}");
					Assert.That(before3, Is.EqualTo(new VersionStamp()));

					// change the version from inside the transaction
					Log("Mutate Foo!");
					tr.TouchMetadataVersionKey(subspace.Key("Foo"));

					// we should not be able to get the version anymore (should return null)
					var after1 = await tr.GetMetadataVersionKeyAsync(subspace.Key("Foo"));
					Log($"Foo (after): {after1}");
					Assert.That(after1, Is.Null, "Should not be able to get the version right after changing it from the same transaction.");

					// We can read the version before
					var after2 = await tr.GetMetadataVersionKeyAsync(subspace.Key("Bar"));
					Log($"Bar (after): {after2}");
					Assert.That(after2, Is.Not.Null.And.EqualTo(before2));

					// We can read the version before
					var after3 = await tr.GetMetadataVersionKeyAsync(subspace.Key("Baz"));
					Log($"Baz (after): {after3}");
					Assert.That(after3, Is.EqualTo(new VersionStamp()));

				}, this.Cancellation);

			}
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

			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
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

			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				var initialA = Slice.FromStringAscii("Initial value of AAA");
				var initialB = Slice.FromStringAscii("Initial value of BBB");

				async Task RunCheck(Func<IFdbTransaction, bool> test, Func<IFdbTransaction, IKeySubspace, Task> handler, bool shouldCommit)
				{
					// read previous witness value
					await db.WriteAsync(async tr =>
					{
						tr.StopLogging();
						var subspace = await location.Resolve(tr);

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

						var subspace = await location.Resolve(tr);
						await handler(tr, subspace);
						tr.Set(subspace.Key("Witness"), Slice.FromStringAscii("New witness value"));
					}, this.Cancellation);
					//await DumpSubspace(db, location);

					// read back the witness key to see if commit happened or not.
					var actual = await db.ReadAsync(async tr =>
					{
						tr.StopLogging();
						var subspace = await location.Resolve(tr);
						return await tr.GetAsync(subspace.Key("Witness"));
					}, this.Cancellation);

					if (shouldCommit)
						Assert.That(actual, Is.EqualTo(Slice.FromStringAscii("New witness value")), "Transaction SHOULD have changed the database!");
					else
						Assert.That(actual, Is.EqualTo(Slice.FromStringAscii("Initial witness value")), "Transaction should NOT have changed the database!");
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
							tr.Context.AddValueCheck("fooCheck", subspace.Key("CCC"), Slice.FromStringAscii("Some value"));
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
		}

		[Test]
		public async Task Test_Value_Checks_Retries_On_Application_Exception()
		{
			// If we observe an application exception being thrown by the handler, normally we would stop the retry loop there.
			// But if there was at least one failed value-check, we HAVE to retry because it is possible that the application threw due to some invalid assumption.
			// Normally, any layer that used cached data will observe the failed check, and re-validate the cache.
			// If the application error was caused by this stale data, then it should not throw in the new attempt.
			// If the application error was caused by something completely unrelated, then it should throw again, and we should NOT retry

			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				var location = db.Root.WithPrefix(TuPack.EncodeKey("test"));

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				for (int i = 0; i < 15; i++)
				{

					if (i % 5 == 0)
					{
						Log("Clear the database...");

						await db.WriteAsync(async tr =>
						{
							var subspace = await location.TryResolve(tr);
							if (subspace is not null)
							{
								tr.ClearRange(subspace.ToRange());
							}
						}, this.Cancellation);

						// if the application code fails we have to make sure that if there was also a failed value-check, the handler retries again!

						await db.WriteAsync(async tr =>
						{
							var subspace = await location.Resolve(tr);
							tr.Set(subspace.Key("Foo"), Slice.FromStringAscii("NotReady"));
							// Bar does not exist
						}, this.Cancellation);
					}

					var task = db.ReadWriteAsync(async tr =>
					{
						//note: this subspace does not use the DL so it does not introduce any value checks!
						var subspace = await location.Resolve(tr);

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
								tr.Set(subspace.Key("Foo"), Slice.FromStringAscii("Ready"));
								tr.Set(subspace.Key("Bar"), Slice.FromStringAscii("Something"));
							}
						}
						else
						{
							tr.Annotate("APP: I'm feeling lucky! Let's assume the db is already initialized");
							tr.Context.AddValueCheck("foo", subspace.Key("Foo"), Slice.FromStringAscii("Ready"));
						}
						// Verify that if "Foo" was equal to "Value2", then "Bar" SHOULD exist
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

		}

	}

}
