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

// ReSharper disable StringLiteralTypo
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
namespace FoundationDB.Testing.Tests
{
	using System.Diagnostics;
	using System.Text;
	using FoundationDB.Client;
	using FoundationDB.Storage;

	[Category("FakeDb-Client")]
	public abstract class FakeDbTest : SimpleTest
	{

		[DebuggerNonUserCode]
		protected ValueTask<FdbDatabase> OpenTestDatabaseAsync()
		{
			var store = new FakeDbStore();
			return new(store.OpenDatabase(FdbPath.Root, readOnly: false));
		}

		[DebuggerNonUserCode]
		protected static Slice Key(string literal) => Slice.FromStringAscii(literal);

		[DebuggerNonUserCode]
		protected static Slice Value(string literal) => Slice.FromStringUtf8(literal);

		[DebuggerNonUserCode]
		protected static Slice Text(string text) => Slice.FromStringUtf8(text);

		[DebuggerNonUserCode]
		protected static void Log(IKeySubspace? subspace) => Log(subspace?.ToString() ?? "<null>");

		[DebuggerNonUserCode]
		protected static void Log(ISubspaceLocation? location) => Log(location?.ToString() ?? "<null>");

		[DebuggerNonUserCode]
		protected void DumpStore(FakeDbStore store, string label)
		{
			DumpStore(store.CurrentSnapshotUnsafe, label);
		}

		[DebuggerNonUserCode]
		protected void DumpStore(Snapshot snapshot, string label)
		{
			Log();
			var sb = new StringBuilder();
			sb.AppendLineInvariant($"### {label}");
			sb.AppendLineInvariant($"* Version: {snapshot.Version:X}");

			sb.AppendLineInvariant($"* Keys: {snapshot.Count:N0}");
			foreach (var x in snapshot.ReadData())
			{
				sb.AppendLineInvariant($"| - {x.Key:K} = {x.Value:P}");
			}

			var conflicts = snapshot.ReadConflicts().ToList();
			sb.AppendLineInvariant($"* Ranges: {conflicts.Count:N0}");
			foreach (var x in conflicts)
			{
				sb.AppendLineInvariant($"| - {x.Begin:K}..{x.End:K}: {x.Version:N0}");
			}
			LogPartial(sb);
		}

	}

	[TestFixture]
	public class FakeDbFacts : FakeDbTest
	{

		[Test]
		[Order(0)]
		public async Task Test_Can_Perform_Basic_Operations()
		{
			var store = new FakeDbStore();

			var db = store.OpenDatabase(null, readOnly: false);

			Assert.That(db, Is.Not.Null);
			Assert.That(db.Root, Is.Not.Null, "db.Root");
			Assert.That(db.Cancellation.IsCancellationRequested, Is.False, "db.Cancellation should be alive before dispose");

			var tr = db.BeginTransaction(FdbTransactionMode.Default, this.Cancellation);
			Assert.That(tr, Is.Not.Null);
			Assert.That(tr.Cancellation.IsCancellationRequested, Is.False);
			Assert.That(tr.Context.Database, Is.SameAs(db));
			Assert.That(tr.Context.GetTransactionHandler(), Is.InstanceOf<FakeDbStore.TransactionHandler<ColaCommittedCursor>>());
			var handler = (FakeDbStore.TransactionHandler<ColaCommittedCursor>) tr.Context.GetTransactionHandler();
			Assert.That(handler.Store, Is.SameAs(store));

			tr.Dispose();
			Assert.That(tr.Cancellation.IsCancellationRequested, Is.True, "tr.Cancellation should be triggered after transaction is disposed");
			Assert.That(db.Cancellation.IsCancellationRequested, Is.False, "db.Cancellation should be alive even if transaction is disposed");

			db.Dispose();
			Assert.That(db.Cancellation.IsCancellationRequested, Is.True, "db.Cancellation should be triggered after dispose");
		}

		[Test]
		public async Task Test_Disposing_One_Database_On_A_Shared_Store_Keeps_The_Store_Alive()
		{
			// Several hosts share one FakeDbStore, as the FakeDb provider does with FakeDbProviderOptions.Store.
			// Disposing one host's database must leave the store, and the other hosts' databases, alive.
			var store = new FakeDbStore();

			var dbA = store.OpenDatabase(FdbPath.Root, readOnly: false, ownsStore: false);
			var dbB = store.OpenDatabase(FdbPath.Root, readOnly: false, ownsStore: false);

			await dbB.WriteAsync(tr => tr.Set(Key("hello"), Value("world")), this.Cancellation);

			// one host stops
			dbA.Dispose();
			Assert.That(dbA.Cancellation.IsCancellationRequested, Is.True, "the disposed database is cancelled");

			// the shared store and the other host survive
			Assert.That(store.IsClosed, Is.False, "the shared store must stay open after one database is disposed");
			Assert.That(dbB.Cancellation.IsCancellationRequested, Is.False, "the other database must stay alive");

			// dbB still reads what it wrote and keeps working
			await dbB.ReadAsync(async tr => Assert.That(await tr.GetAsync(Key("hello")), Is.EqualTo(Value("world"))), this.Cancellation);
			await dbB.WriteAsync(tr => tr.Set(Key("again"), Value("ok")), this.Cancellation);

			// the store's owner tears it down: now every remaining database is cancelled
			store.Dispose();
			Assert.That(store.IsClosed, Is.True);
			Assert.That(dbB.Cancellation.IsCancellationRequested, Is.True, "disposing the store cancels the remaining databases");
		}

		[Test]
		public void Test_Disposing_An_Owning_Database_Closes_Its_Store()
		{
			// The standalone default: a database opened with ownsStore: true (the default) disposes its store.
			var store = new FakeDbStore();
			var db = store.OpenDatabase(FdbPath.Root, readOnly: false);

			db.Dispose();
			Assert.That(store.IsClosed, Is.True, "an owning database disposes its store");
		}

		[Test]
		[Order(10)]
		public async Task Test_Can_Read_And_Write()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(null, false))
			{
				await It("Can write keys", () => db.WriteAsync(tr =>
				{
					tr.Set(Key("K1"), Value("V1"));
					tr.Set(Key("K2"), Value("V2"));
					tr.Set(Key("K0"), Value("V0"));
				}, this.Cancellation));

				await It("Can read back the keys", () => db.ReadAsync(async tr =>
				{
					Assert.That(await tr.GetAsync(Key("K0")), Is.EqualTo(Value("V0")));
					Assert.That(await tr.GetAsync(Key("K1")), Is.EqualTo(Value("V1")));
					Assert.That(await tr.GetAsync(Key("K2")), Is.EqualTo(Value("V2")));
				}, this.Cancellation));
			}
		}

		[Test]
		[Order(11)]
		public async Task Test_Can_ClearRange()
		{

			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(null, false))
			{
				//setup
				await db.WriteAsync((tr) =>
				{
					tr.Set(Key("AAA"), Value("Sentinel AAA"));
					for (int i = 0; i < 20; i++)
					{
						tr.Set(Key($"Key{i:D02}"), Value("Value of " + i));
					}
					tr.Set(Key("ZZZ"), Value("Sentinel ZZZ"));
				}, this.Cancellation);

				DumpStore(store, "Before");

				// clear values (without reading from the transaction)
				await It("Can ClearRange (without RYW)", async () =>
				{
					// before: AAA, Key00...Key19 and ZZZ

					await db.WriteAsync(tr =>
					{
						// remove ['Key03', 'Key07')
						tr.ClearRange(Key("Key03"), Key("Key07"));
						// remove ['Key15', 'Key25')
						tr.ClearRange(Key("Key15"), Key("Key25"));
					}, this.Cancellation);

					// after: AAA, Key00..Key02, Key07..Key14, ZZZ

					DumpStore(store, "After");

					await db.ReadAsync(async (tr) =>
					{
						Assert.That(await tr.GetAsync(Key("AAA")), Is.EqualTo(Value("Sentinel AAA")), "Sentinel AAA should be untouched");
						for (int i = 0; i < 20; i++)
						{
							if (i >= 3 && i < 7 || i >= 15)
							{
								Assert.That(await tr.GetAsync(Key($"Key{i:D02}")), Is.EqualTo(Slice.Nil), $"Key {i} should have been removed");
							}
							else
							{
								Assert.That(await tr.GetAsync(Key($"Key{i:D02}")), Is.EqualTo(Value("Value of " + i)), $"Key {i} should have been left untouched");
							}
						}
						Assert.That(await tr.GetAsync(Key("ZZZ")), Is.EqualTo(Value("Sentinel ZZZ")), "Sentinel ZZZ should be untouched");
					}, this.Cancellation);
				});
			}
		}

		[Test]
		[Order(12)]
		public async Task Test_Can_GetRange()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				// prepare get range
				await db.WriteAsync(tr =>
				{
					//note: for fun, we insert in reverse order...
					for (int i = 9; i >= 0; i--)
					{
						tr.Set(Key("Key" + i), Value("Value" + i));
					}
				}, this.Cancellation);

				await It("GetRange 'Key2' <= k < 'Key7' (forward)", async () =>
				{
					var chunk = await db.ReadAsync(tr => tr.GetRangeAsync(Key("Key2"), Key("Key7")), this.Cancellation);
					Log($"> {chunk.Count}");
					foreach (var x in chunk.Items)
					{
						Log($"- {x.Key:K} = {x.Value:V}");
					}
					Assert.That(chunk.Reversed, Is.False);
					Assert.That(chunk.Items.Select(x => x.Key), Is.EqualTo([ Key("Key2"), Key("Key3"), Key("Key4"), Key("Key5"), Key("Key6") ]));
					Assert.That(chunk.Items.Select(x => x.Value), Is.EqualTo([ Key("Value2"), Key("Value3"), Key("Value4"), Key("Value5"), Key("Value6") ]));
					Assert.That(chunk.IsEmpty, Is.False, ".IsEmpty");
					Assert.That(chunk.First, Is.EqualTo(Key("Key2")), ".First");
					Assert.That(chunk.Last, Is.EqualTo(Key("Key6")), ".Last");
					Assert.That(chunk.HasMore, Is.False, ".HasMore");
					Assert.That(chunk.Iteration, Is.EqualTo(1), ".Iteration");
				});

				await It("GetRange 'Key2' <= k < 'Key7' (reverse)", async () =>
				{
					var chunk = await db.ReadAsync(tr => tr.GetRangeAsync(Key("Key2"), Key("Key7"), FdbRangeOptions.Reversed), this.Cancellation);
					Log($"> {chunk.Count}");
					foreach (var x in chunk.Items)
					{
						Log($"- {x.Key:K} = {x.Value:V}");
					}
					Assert.That(chunk.Reversed, Is.True);
					Assert.That(chunk.Items.Select(x => x.Key), Is.EqualTo([ Key("Key6"), Key("Key5"), Key("Key4"), Key("Key3"), Key("Key2") ]));
					Assert.That(chunk.Items.Select(x => x.Value), Is.EqualTo([ Key("Value6"), Key("Value5"), Key("Value4"), Key("Value3"), Key("Value2") ]));
					Assert.That(chunk.IsEmpty, Is.False, ".IsEmpty");
					Assert.That(chunk.First, Is.EqualTo(Key("Key6")), ".First");
					Assert.That(chunk.Last, Is.EqualTo(Key("Key2")), ".Last");
					Assert.That(chunk.HasMore, Is.False, ".HasMore");
					Assert.That(chunk.Iteration, Is.EqualTo(1), ".Iteration");
				});

			}
		}

		[Test]
		[Order(10)]
		public async Task Test_Can_Merge_Writes_To_Same_Key()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(null, false))
			{
#if ENABLE_LOGGING
				db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

				await It("Can write same key multiple time", async () =>
				{
					await db.WriteAsync(tr =>
					{
						tr.Set(Key("K1"), Value("V1a"));
						tr.Set(Key("K1"), Value("V1b"));
						tr.Set(Key("K1"), Value("V1c"));
						tr.Clear(Key("K1"));
						tr.Set(Key("K1"), Value("V1d"));
						tr.Set(Key("K1"), Value("V1e"));
					}, this.Cancellation);

					DumpStore(store, "After Change");

					var res = await db.ReadAsync(tr => tr.GetAsync(Key("K1")), this.Cancellation);
					Log($"> {res:P}");
					Assert.That(res, Is.EqualTo(Value("V1e")));
				});

				await It("Can merge AtomicAdd with Set", async () =>
				{
					await db.WriteAsync(tr =>
					{
						tr.Set(Key("K1"), Slice.FromFixed32(1));
						tr.AtomicAdd32(Key("K1"), 41);
					}, this.Cancellation);

					DumpStore(store, "After Change");

					var res = await db.ReadAsync(tr => tr.GetAsync(Key("K1")), this.Cancellation);
					Log($"> {res:P}");
					Assert.That(res, Is.EqualTo(Slice.FromFixed32(42)));
				});

				await It("Can merge AtomicAdd with Clear", async () =>
				{
					await db.WriteAsync(tr =>
					{
						tr.Clear(Key("K1"));
						tr.AtomicAdd32(Key("K1"), 19);
						tr.AtomicAdd32(Key("K1"), 22);
					}, this.Cancellation);

					DumpStore(store, "After Change");

					var res = await db.ReadAsync(tr => tr.GetAsync(Key("K1")), this.Cancellation);
					Log($"> {res:P}");
					Assert.That(res, Is.EqualTo(Slice.FromFixed32(41)));
				});

				await It("Can coalesce AtomicAdd on Read", async () =>
				{
					var res = await db.ReadWriteAsync(async tr =>
					{
						tr.Set(Key("K1"), Slice.FromFixed32(1));
						tr.AtomicAdd32(Key("K1"), 43);
						tr.AtomicAdd32(Key("K1"), -2);
						// Reading the value before commit should return the updated value
						return await tr.GetAsync(Key("K1"));
					}, this.Cancellation);
					Assert.That(res, Is.EqualTo(Slice.FromFixed32(42)));

					DumpStore(store, "After Change");

					res = await db.ReadAsync(tr => tr.GetAsync(Key("K1")), this.Cancellation);
					Log($"> {res:P}");
					Assert.That(res, Is.EqualTo(Slice.FromFixed32(42)));
				});

			}
		}

		[Test]
		[Order(20)]
		public async Task Test_Can_Read_Your_Writes()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(null, false))
			{
				// setup
				await db.WriteAsync(tr => tr.Set(Key("K0"), Value("V0a")), this.Cancellation);

				await It(
					"Can read your writes",
					() => db.WriteAsync(async (tr) =>
					{
						// check before
						Assert.That(await tr.GetAsync(Key("K0")), Is.EqualTo(Value("V0a")));
						Assert.That(await tr.GetAsync(Key("K1")), Is.EqualTo(Slice.Nil));
						Assert.That(await tr.GetAsync(Key("K2")), Is.EqualTo(Slice.Nil));

						// update K0 & K1
						tr.Set(Key("K0"), Value("V0b"));
						tr.Set(Key("K1"), Value("V1a"));

						// check we see the changes
						Assert.That(await tr.GetAsync(Key("K0")), Is.EqualTo(Value("V0b")));
						Assert.That(await tr.GetAsync(Key("K1")), Is.EqualTo(Value("V1a")));
						Assert.That(await tr.GetAsync(Key("K2")), Is.EqualTo(Slice.Nil));

						// update K0, K1 & K2
						tr.Set(Key("K0"), Value("V0c"));
						tr.Set(Key("K1"), Value("V1b"));
						tr.Set(Key("K2"), Value("V2a"));

						// check we see the changes
						Assert.That(await tr.GetAsync(Key("K0")), Is.EqualTo(Value("V0c")));
						Assert.That(await tr.GetAsync(Key("K1")), Is.EqualTo(Value("V1b")));
						Assert.That(await tr.GetAsync(Key("K2")), Is.EqualTo(Value("V2a")));
					}, this.Cancellation)
				);

				await It(
					"Only store the final values in the database",
					() => db.ReadAsync(async tr =>
					{
						// ensure only the last changes were persisted
						Assert.That(await tr.GetAsync(Key("K0")), Is.EqualTo(Value("V0c")));
						Assert.That(await tr.GetAsync(Key("K1")), Is.EqualTo(Value("V1b")));
						Assert.That(await tr.GetAsync(Key("K2")), Is.EqualTo(Value("V2a")));
					}, this.Cancellation)
				);

			}

		}

		[Test]
		[Order(21)]
		public async Task Test_Can_ClearRange_ReadYourWrites()
		{

			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(null, false))
			{
				//setup
				await db.WriteAsync((tr) =>
				{
					tr.Set(Key("AAA"), Value("Sentinel AAA"));
					for (int i = 0; i < 20; i++)
					{
						tr.Set(Key($"Key{i:D02}"), Value($"Value of {i}"));
					}
					tr.Set(Key("ZZZ"), Value("Sentinel ZZZ"));
				}, this.Cancellation);

				DumpStore(store, "Before");

				// clear values (without reading from the transaction)
				await It("Can ClearRange (without RYW)", () => db.ReadWriteAsync(async tr =>
				{
					// remove ['Key03', 'Key07')
					tr.ClearRange(Key("Key03"), Key("Key07"));
					// remove ['Key15', 'Key25')
					tr.ClearRange(Key("Key15"), Key("Key25"));

					// check from the same transaction

					Assert.That(await tr.GetAsync(Key("AAA")), Is.EqualTo(Value("Sentinel AAA")), "Sentinel AAA should be untouched");
					for (int i = 0; i < 20; i++)
					{
						var v = await tr.GetAsync(Key($"Key{i:D02}"));
						if (i >= 3 && i < 7 || i >= 15)
						{
							Assert.That(v, Is.EqualTo(Slice.Nil), $"Key {i} should have been removed");
						}
						else
						{
							Assert.That(v, Is.EqualTo(Value($"Value of {i}")), $"Key {i} should have been left untouched");
						}
					}

					Assert.That(await tr.GetAsync(Key("ZZZ")), Is.EqualTo(Value("Sentinel ZZZ")), "Sentinel ZZZ should be untouched");

					return true;
				}, this.Cancellation));

				// after: AAA, Key00..Key02, Key07..Key14, ZZZ

				DumpStore(store, "After");

				await db.ReadAsync(async (tr) =>
				{
					Assert.That(await tr.GetAsync(Key("AAA")), Is.EqualTo(Value("Sentinel AAA")), "Sentinel AAA should be untouched");
					for (int i = 0; i < 20; i++)
					{
						if (i >= 3 && i < 7 || i >= 15)
						{
							Assert.That(await tr.GetAsync(Key($"Key{i:D02}")), Is.EqualTo(Slice.Nil), $"Key {i} should have been removed");
						}
						else
						{
							Assert.That(await tr.GetAsync(Key($"Key{i:D02}")), Is.EqualTo(Value("Value of " + i)), $"Key {i} should have been left untouched");
						}
					}
					Assert.That(await tr.GetAsync(Key("ZZZ")), Is.EqualTo(Value("Sentinel ZZZ")), "Sentinel ZZZ should be untouched");
				}, this.Cancellation);
			}
		}

		[Test]
		[Order(22)]
		public async Task Test_Can_GetRange_ReadYourWrites()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				// prepare get range
				await db.WriteAsync(tr =>
				{
					//note: for fun, we insert in reverse order...
					for (int i = 9; i >= 0; i--)
					{
						if (i == 5) continue;
						tr.Set(Key("Key" + i), Value("Value" + i));
					}
				}, this.Cancellation);

				await It("GetRange 'Key2' <= k < 'Key8' (forward)", async () =>
				{

					var chunk = await db.ReadWriteAsync(async tr =>
					{
						// add new stuff before and after
						tr.Set(Key("AAA"), Value("Value AAA"));
						tr.Set(Key("ZZZ"), Value("Value ZZZ"));
						// change key1 that is outside the range read
						tr.Set(Key("Key1"), Value("Value1 changed"));

						// change 4
						tr.Set(Key("Key4"), Value("Value4 changed"));
						// fill the hole at 5
						tr.Set(Key("Key5"), Value("Value5 added"));
						// empty clear range
						tr.ClearRange(Key("Key4a"), Key("Key4z"));
						// clear range that deletes 6
						tr.ClearRange(Key("Key5z"), Key("Key6a"));

						// clear range after the range read
						tr.ClearRange(Key("Key8b"), Key("Key9z"));
						
						DumpStore(store, "in transaction");

						var h = (FakeDbStore.TransactionHandler<ColaCommittedCursor>) tr.Context.GetTransactionHandler();
						var s = h.GetSnapshotBlocking();
						var mutations = FakeDbDebugger.GetSnapshotMutations(s);
						Log($"% Mutations: {mutations.Count:N0}");
						foreach (var entry in mutations.IterateOrdered())
						{
							Log($"% - {entry.Begin} ~ {entry.End}: {entry.Value}");
						}

						var readConflicts = FakeDbDebugger.GetSnapshotReadConflicts(s);
						Log($"% Read Conflicts: {readConflicts.Count:N0}");
						foreach (var entry in readConflicts.IterateOrdered())
						{
							Log($"% - {entry.Begin} ~ {entry.End}");
						}

						var writeConflicts = FakeDbDebugger.GetSnapshotWriteConflicts(s);
						Log($"% Write Conflicts: {writeConflicts.Count:N0}");
						foreach (var entry in writeConflicts.IterateOrdered())
						{
							Log($"% - {entry.Begin} ~ {entry.End}");
						}

						return await tr.GetRangeAsync(Key("Key2"), Key("Key8"));
					}, this.Cancellation);
					Log($"> {chunk.Count}");
					foreach (var x in chunk.Items)
					{
						Log($"- {x.Key:K} = {x.Value:V}");
					}
					Assert.That(chunk.Reversed, Is.False);
					Assert.That(chunk.Items.Select(x => x.Key), Is.EqualTo([ Key("Key2"), Key("Key3"), Key("Key4"), Key("Key5"), Key("Key7") ]));
					Assert.That(chunk.Items.Select(x => x.Value), Is.EqualTo([ Key("Value2"), Key("Value3"), Key("Value4 changed"), Key("Value5 added"), Key("Value7") ]));
					Assert.That(chunk.IsEmpty, Is.False, ".IsEmpty");
					Assert.That(chunk.First, Is.EqualTo(Key("Key2")), ".First");
					Assert.That(chunk.Last, Is.EqualTo(Key("Key7")), ".Last");
					Assert.That(chunk.HasMore, Is.False, ".HasMore");
					Assert.That(chunk.Iteration, Is.EqualTo(1), ".Iteration");
				});

			}
		}

		[Test]
		[Order(23)]
		public async Task Test_Can_GetRange_ReadYourWrites2()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				// prepare get range
				await db.WriteAsync(tr =>
				{
					tr.Set(Key("A"), Value("AAA"));
					tr.Set(Key("Z"), Value("ZZZ"));
				}, this.Cancellation);

				await It("GetRange 'Key2' <= k < 'Key8' (forward)", async () =>
				{
					var chunk = await db.ReadWriteAsync(async tr =>
					{
						// clear range B subspace
						tr.ClearRange(Key("B\x00"), Key("B\xFF"));
						tr.Set(Key("BA"), Value("Value of BA"));
						tr.Set(Key("BB"), Value("Value of BB"));
						tr.Set(Key("BC"), Value("Value of BC"));
						// transaction allso does some unrelated work before and after
						tr.Set(Key("Axx"), Value("some stuff"));
						tr.Set(Key("Cxx"), Value("some stuff"));
						tr.Set(Key("Dxx"), Value("some stuff"));
						tr.Set(Key("Exx"), Value("some stuff"));

						DumpStore(store, "in transaction");

						var h = (FakeDbStore.TransactionHandler<ColaCommittedCursor>) tr.Context.GetTransactionHandler();
						var s = h.GetSnapshotBlocking();
						var mutations = FakeDbDebugger.GetSnapshotMutations(s);
						Log($"% Mutations: {mutations.Count:N0}");
						foreach (var entry in mutations.IterateOrdered())
						{
							Log($"% - {entry.Begin} ~ {entry.End}: {entry.Value}");
						}

						var readConflicts = FakeDbDebugger.GetSnapshotReadConflicts(s);
						Log($"% Read Conflicts: {readConflicts.Count:N0}");
						foreach (var entry in readConflicts.IterateOrdered())
						{
							Log($"% - {entry.Begin} ~ {entry.End}");
						}

						var writeConflicts = FakeDbDebugger.GetSnapshotWriteConflicts(s);
						Log($"% Write Conflicts: {writeConflicts.Count}");
						foreach (var entry in writeConflicts.IterateOrdered())
						{
							Log($"% - {entry.Begin} ~ {entry.End}");
						}

						return await tr.GetRangeAsync(Key("B\x00"), Key("B\xFF"));
					}, this.Cancellation);
					Log($"> {chunk.Count}");
					foreach (var x in chunk.Items)
					{
						Log($"- {x.Key:K} = {x.Value:V}");
					}
					Assert.That(chunk.Reversed, Is.False);
					Assert.That(chunk.Items.Select(x => x.Key), Is.EqualTo([ Key("BA"), Key("BB"), Key("BC") ]));
					Assert.That(chunk.Items.Select(x => x.Value), Is.EqualTo([ Key("Value of BA"), Key("Value of BB"), Key("Value of BC") ]));
					Assert.That(chunk.IsEmpty, Is.False, ".IsEmpty");
					Assert.That(chunk.First, Is.EqualTo(Key("BA")), ".First");
					Assert.That(chunk.Last, Is.EqualTo(Key("BC")), ".Last");
					Assert.That(chunk.HasMore, Is.False, ".HasMore");
					Assert.That(chunk.Iteration, Is.EqualTo(1), ".Iteration");
				});

			}
		}

		[Test]
		[Order(30)]
		public async Task Test_Can_Read_SystemKeys()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(null, false))
			{
				// by default, access is denied
				await It("Cannot read system keys by default", () => db.ReadAsync(async tr =>
				{
					Assert.That(async () => await tr.GetAsync(FdbKey.ToSystemKey("/foo")), Throws.Exception, "Should not be able to read without system keys access enabled");
				}, this.Cancellation));

				// read access
				await It("Can read system keys after setting the option ReadSystemKeys", () => db.ReadAsync(async tr =>
				{
					tr.Options.WithReadAccessToSystemKeys();
					Assert.That(async () => await tr.GetAsync(FdbKey.ToSystemKey("/foo")), Throws.Nothing, "Should be able to read system keys with ReadSystemKeys option");
				}, this.Cancellation));

				// write access (allows read)
				await It("Can read system keys after setting the option AccessSystemKeys", () => db.WriteAsync(async tr =>
				{
					tr.Options.WithWriteAccessToSystemKeys();
					Assert.That(async () => await tr.GetAsync(FdbKey.ToSystemKey("/foo")), Throws.Nothing, "Should be able to read system keys with AccessSystemKeys option");
				}, this.Cancellation));
			}
		}

		[Test]
		[Order(31)]
		public async Task Test_Can_Write_SystemKeys()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(null, false))
			{
				// by default, access is denied
				await It("Cannot write system keys by default", () => db.WriteAsync(tr =>
				{
					Assert.That(() => tr.Set(FdbKey.ToSystemKey("/foo/bar"), Value("Hello!")), Throws.Exception);
					Assert.That(() => tr.Clear(FdbKey.ToSystemKey("/foo")), Throws.Exception);
					Assert.That(() => tr.ClearRange(FdbKey.ToSystemKey("/foo"), FdbKey.ToSystemKey("/fooz")), Throws.Exception);
					Assert.That(() => tr.AtomicAdd32(FdbKey.ToSystemKey("/bar"), 123), Throws.Exception);

					//note: clearRange that ends exactly at FF is fine!
					Assert.That(() => tr.ClearRange(Slice.FromStringAscii("zzz"), FdbKey.SystemPrefix), Throws.Nothing);

				}, this.Cancellation));

				// read access
				await It("Can read and write system keys after setting the option WriteSystemKeys", () => db.WriteAsync(async tr =>
				{
					tr.Options.WithWriteAccessToSystemKeys();
					// we should be able to read...
					var res = await tr.GetAsync(Fdb.System.MetadataVersionKey);
					Assert.That(res, Is.Not.EqualTo(Slice.Nil));
					// ... and write
					tr.Set(FdbKey.ToSystemKey("/foo/bar"), Value("Hello!"));
					tr.Clear(FdbKey.ToSystemKey("/foo"));
					tr.ClearRange(FdbKey.ToSystemKey("/foo"), FdbKey.ToSystemKey("/fooz"));
					tr.AtomicAdd32(FdbKey.ToSystemKey("/bar"), 123);
				}, this.Cancellation));
			}
		}

		[Test]
		[Order(31)]
		public async Task Test_Can_Read_And_Touch_MetadataVersionKey()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(null, false))
			{
				// can read the value
				await It("Can read metadata version key", () => db.ReadAsync(async tr =>
				{
					var ver = await tr.GetMetadataVersionKeyAsync();
					Log("> " + ver);
					Assert.That(ver, Is.Not.Null);
				}, this.Cancellation));

				// can read the same value multiple times
				await It("Metadata version key does not change between transactions", async () =>
				{
					var ver1 = await db.ReadAsync(tr => tr.GetMetadataVersionKeyAsync(), this.Cancellation);
					Log("> T1: " + ver1);
					Assert.That(ver1, Is.Not.Null);
					var ver2 = await db.ReadAsync(tr => tr.GetMetadataVersionKeyAsync(), this.Cancellation);
					Log("> T2: " + ver2);
					Assert.That(ver2, Is.Not.Null);
					Assert.That(ver1, Is.EqualTo(ver2), "Metadata version key should not change between normal transactions");
				});

				// version should change after "touching" it
				await It("Metadata version key changes after call to TouchMetadataVersionKey", async () =>
				{
					var ver1 = await db.ReadAsync(tr => tr.GetMetadataVersionKeyAsync(), this.Cancellation);
					Log("> T1: " + ver1);
					Assert.That(ver1, Is.Not.Null);

					await db.WriteAsync(tr => tr.TouchMetadataVersionKey(), this.Cancellation);

					var ver2 = await db.ReadAsync(tr => tr.GetMetadataVersionKeyAsync(), this.Cancellation);
					Log("> T2: " + ver2);
					Assert.That(ver2, Is.Not.Null);

					Assert.That(ver2, Is.Not.EqualTo(ver1), "Metadata version key should have changed between normal transactions");
					Assert.That(ver2, Is.GreaterThan(ver1), "Metadata version key should been greater than before");
				});

			}
		}

		[Test]
		[Order(40)]
		public async Task Test_Can_Atomic_Add_Snapshot()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(null, false))
			{
				await db.WriteAsync(tr =>
				{
					// K0: not found
					tr.Set(Key("K1"), Slice.Zero(4));
					tr.Set(Key("K2"), Slice.Zero(8));
					tr.Set(Key("K3"), Slice.FromInt32(0x01234567));
					tr.Set(Key("K4"), Slice.FromInt64(0x0123456789ABCDEF));
				}, this.Cancellation);

				DumpStore(store, "Before");

				await It("Can atomic add", () => db.WriteAsync(tr =>
				{
					tr.AtomicAdd32(Key("K0"), 0x1234);
					tr.AtomicAdd32(Key("K1"), 1);
					tr.AtomicAdd64(Key("K2"), 1);
					tr.AtomicAdd32(Key("K3"), 0x12345678);
					tr.AtomicAdd64(Key("K4"), 0x123456789ABCDEF0);
				}, this.Cancellation));

				DumpStore(store, "After");

				await db.ReadAsync(async tr =>
				{
					Assert.That(await tr.GetAsync(Key("K0")), Is.EqualTo(Slice.FromFixed32(0x1234)));
					Assert.That(await tr.GetAsync(Key("K1")), Is.EqualTo(Slice.FromFixed32(1)));
					Assert.That(await tr.GetAsync(Key("K2")), Is.EqualTo(Slice.FromFixed64(1)));
					Assert.That(await tr.GetAsync(Key("K3")), Is.EqualTo(Slice.FromFixed32(0x01234567 + 0x12345678)));
					Assert.That(await tr.GetAsync(Key("K4")), Is.EqualTo(Slice.FromFixed64(0x0123456789ABCDEF + 0x123456789ABCDEF0)));
				}, this.Cancellation);
			}
		}

		[Test]
		[Order(41)]
		public async Task Test_Can_Atomic_Add_ReadYourWrites()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(null, false))
			{
				await db.WriteAsync(tr =>
				{
					// K0: not found
					tr.Set(Key("K1"), Slice.Zero(4));
					tr.Set(Key("K2"), Slice.Zero(8));
					tr.Set(Key("K3"), Slice.FromInt32(0x01234567));
					tr.Set(Key("K4"), Slice.FromInt64(0x0123456789ABCDEF));
				}, this.Cancellation);

				DumpStore(store, "Before");

				await It("Can atomic add (ryw)", () => db.WriteAsync(async (tr) =>
				{
					tr.AtomicAdd32(Key("K0"), 0x1234);
					tr.AtomicAdd32(Key("K1"), 1);
					tr.AtomicAdd64(Key("K2"), 1);
					tr.AtomicAdd32(Key("K3"), 0x12345678);
					tr.AtomicAdd64(Key("K4"), 0x123456789ABCDEF0);

					// read it back from the same transaction
					Assert.That(await tr.GetAsync(Key("K0")), Is.EqualTo(Slice.FromFixed32(0x1234)));
					Assert.That(await tr.GetAsync(Key("K1")), Is.EqualTo(Slice.FromFixed32(1)));
					Assert.That(await tr.GetAsync(Key("K2")), Is.EqualTo(Slice.FromFixed64(1)));
					Assert.That(await tr.GetAsync(Key("K3")), Is.EqualTo(Slice.FromFixed32(0x01234567 + 0x12345678)));
					Assert.That(await tr.GetAsync(Key("K4")), Is.EqualTo(Slice.FromFixed64(0x0123456789ABCDEF + 0x123456789ABCDEF0)));
				}, this.Cancellation));

				DumpStore(store, "After");

				// read it after commit
				await db.ReadAsync(async tr =>
				{
					Assert.That(await tr.GetAsync(Key("K0")), Is.EqualTo(Slice.FromFixed32(0x1234)));
					Assert.That(await tr.GetAsync(Key("K1")), Is.EqualTo(Slice.FromFixed32(1)));
					Assert.That(await tr.GetAsync(Key("K2")), Is.EqualTo(Slice.FromFixed64(1)));
					Assert.That(await tr.GetAsync(Key("K3")), Is.EqualTo(Slice.FromFixed32(0x01234567 + 0x12345678)));
					Assert.That(await tr.GetAsync(Key("K4")), Is.EqualTo(Slice.FromFixed64(0x0123456789ABCDEF + 0x123456789ABCDEF0)));
				}, this.Cancellation);

			}
		}

		[Test]
		[Order(100)]
		public async Task Test_Can_DirectoryLayer()
		{
			var store = new FakeDbStore();
			using (var db = store.OpenDatabase(FdbPath.Root, readOnly: false))
			{
				_ = await db.ReadWriteAsync(async tr =>
				{
					Log("Opening directory /Hello/World...");
					var subspace = await db.Directory.CreateOrOpenAsync(tr, FdbPath.Relative("Hello", "World"));
					Assert.That(subspace, Is.Not.Null);
					Log($"> {subspace.Path}: {subspace.GetPrefix():K}");

					tr.Set(subspace.Key("Foo", 123, "Bar"), Value("It works!"));

					return subspace.GetPrefix(); // illegal, but it's fine for a test!
				}, this.Cancellation);

				DumpStore(store, "After first open");

				_ = await db.ReadWriteAsync(async tr =>
				{
					Log("Opening directory /Hello/World again...");
					var subspace = await db.Directory.CreateOrOpenAsync(tr, FdbPath.Relative("Hello", "World"));
					Assert.That(subspace, Is.Not.Null);
					Log($"> {subspace.Path}: {subspace.GetPrefix():K}");

					tr.Set(subspace.Key("Bar", 456), Value("It works again!!"));

					return subspace.GetPrefix(); // illegal, but it's fine for a test!
				}, this.Cancellation);

				DumpStore(store, "After second open");

				await db.WriteAsync(async tr =>
				{
					Log("Removing directory /Hello/World...");
					await db.Directory.RemoveAsync(tr, FdbPath.Relative("Hello", "World"));
				}, this.Cancellation);

				DumpStore(store, "After remove");
			}
		}

	}

}
