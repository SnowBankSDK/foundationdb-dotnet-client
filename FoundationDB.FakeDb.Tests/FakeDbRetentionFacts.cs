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

namespace FoundationDB.Testing.Tests
{
	using FoundationDB.Client;
	using FoundationDB.FakeDb;
	using FoundationDB.Storage;
	using Microsoft.Extensions.Time.Testing;

	/// <summary>Tests for the snapshot retention policies: the default real-cluster 5 second window on the store's (virtual) clock, the built-in policies, and the custom-callback surface.</summary>
	[TestFixture]
	[Parallelizable(ParallelScope.All)]
	public sealed class FakeDbRetentionFacts : SimpleTest
	{

		private static async Task<long> CommitValue(IFdbDatabase db, string key, int value, CancellationToken ct)
		{
			using var tr = db.BeginTransaction(FdbTransactionMode.Default, ct);
			tr.Set(Slice.FromString(key), Slice.FromInt32(value));
			await tr.CommitAsync();
			return tr.GetCommittedVersion();
		}

		private static async Task<Slice> ReadAtVersion(IFdbDatabase db, long version, string key, CancellationToken ct)
		{
			using var tr = db.BeginTransaction(FdbTransactionMode.Default, ct);
			tr.SetReadVersion(version);
			return await tr.GetAsync(Slice.FromString(key));
		}

		[Test]
		public async Task Test_Default_Retention_Is_The_Five_Second_Window_On_The_Store_Clock()
		{
			// the default emulates the real cluster: reads older than the 5 s MVCC window fail with
			// transaction_too_old, and the window runs on the store's clock, so a fake provider ages
			// versions in virtual time
			var clock = new FakeTimeProvider();
			using var store = new FakeDbStore(time: clock);
			using var db = store.OpenDatabase(FdbPath.Root, readOnly: false);

			long v1 = await CommitValue(db, "hello", 1, this.Cancellation);

			clock.Advance(TimeSpan.FromSeconds(3));
			long v2 = await CommitValue(db, "hello", 2, this.Cancellation);

			// 3 s old: both versions are inside the window
			Assert.That((await ReadAtVersion(db, v1, "hello", this.Cancellation)).ToInt32(), Is.EqualTo(1), "v1 is 3 s old and still readable");

			clock.Advance(TimeSpan.FromSeconds(3));
			long v3 = await CommitValue(db, "hello", 3, this.Cancellation);

			// v1 is now 6 s old: aged out, exactly like a real cluster
			var ex = Assert.ThrowsAsync<FdbException>(async () => await ReadAtVersion(db, v1, "hello", this.Cancellation), "v1 is 6 s old and must have aged out");
			Assert.That(ex!.Code, Is.EqualTo(FdbError.TransactionTooOld));

			// v2 is 3 s old: still readable
			Assert.That((await ReadAtVersion(db, v2, "hello", this.Cancellation)).ToInt32(), Is.EqualTo(2), "v2 is 3 s old and still readable");
			Assert.That((await ReadAtVersion(db, v3, "hello", this.Cancellation)).ToInt32(), Is.EqualTo(3));
		}

		[Test]
		public async Task Test_A_Frozen_Clock_Retains_Everything()
		{
			// a fake clock that never advances keeps every version inside the window: a virtual-time
			// test that does not touch its clock loses nothing to retention
			var clock = new FakeTimeProvider();
			using var store = new FakeDbStore(time: clock);
			using var db = store.OpenDatabase(FdbPath.Root, readOnly: false);

			long first = await CommitValue(db, "k", 0, this.Cancellation);
			for (int i = 1; i <= 20; i++)
			{
				await CommitValue(db, "k", i, this.Cancellation);
			}

			Assert.That((await ReadAtVersion(db, first, "k", this.Cancellation)).ToInt32(), Is.EqualTo(0), "the oldest version never aged: the clock never moved");
		}

		[Test]
		public async Task Test_KeepLast_Bounds_The_Readable_Versions_By_Count()
		{
			using var store = new FakeDbStore(retention: FdbSnapshotRetention.KeepLast(2));
			using var db = store.OpenDatabase(FdbPath.Root, readOnly: false);

			long v1 = await CommitValue(db, "k", 1, this.Cancellation);
			long v2 = await CommitValue(db, "k", 2, this.Cancellation);
			long v3 = await CommitValue(db, "k", 3, this.Cancellation);

			var ex = Assert.ThrowsAsync<FdbException>(async () => await ReadAtVersion(db, v1, "k", this.Cancellation), "only the last 2 versions stay readable");
			Assert.That(ex!.Code, Is.EqualTo(FdbError.TransactionTooOld));
			Assert.That((await ReadAtVersion(db, v2, "k", this.Cancellation)).ToInt32(), Is.EqualTo(2));
			Assert.That((await ReadAtVersion(db, v3, "k", this.Cancellation)).ToInt32(), Is.EqualTo(3));
		}

		[Test]
		public async Task Test_KeepEverything_Is_The_Forensic_Mode()
		{
			// with the forensic policy the whole run stays inspectable, however far the clock advances
			var clock = new FakeTimeProvider();
			using var store = new FakeDbStore(time: clock, retention: FdbSnapshotRetention.KeepEverything);
			using var db = store.OpenDatabase(FdbPath.Root, readOnly: false);

			long first = await CommitValue(db, "k", 0, this.Cancellation);
			for (int i = 1; i <= 5; i++)
			{
				clock.Advance(TimeSpan.FromSeconds(10));
				await CommitValue(db, "k", i, this.Cancellation);
			}

			Assert.That((await ReadAtVersion(db, first, "k", this.Cancellation)).ToInt32(), Is.EqualTo(0), "a minute of virtual time passed and the first version is still readable");
		}

		[Test]
		public async Task Test_A_Custom_Callback_Inspects_The_Retained_Set_And_Chooses_Its_Drops()
		{
			// a policy is any callback over the retained set: this one keeps the oldest version (a
			// baseline probe would do this) and the head, dropping the middle
			int calls = 0;
			using var store = new FakeDbStore(retention: ctx =>
			{
				calls++;
				for (int i = 1; i < ctx.Count - 1; i++)
				{
					ctx.Drop(ctx[i].Version);
				}
			});
			using var db = store.OpenDatabase(FdbPath.Root, readOnly: false);

			long v1 = await CommitValue(db, "k", 1, this.Cancellation);
			long v2 = await CommitValue(db, "k", 2, this.Cancellation);
			long v3 = await CommitValue(db, "k", 3, this.Cancellation);

			Assert.That(calls, Is.GreaterThanOrEqualTo(3), "the policy must have run at every publish");
			// the initial snapshot is the oldest retained entry, so v1 (the first commit) was a middle entry and dropped
			var ex = Assert.ThrowsAsync<FdbException>(async () => await ReadAtVersion(db, v2, "k", this.Cancellation), "v2 was a middle entry when v3 published");
			Assert.That(ex!.Code, Is.EqualTo(FdbError.TransactionTooOld));
			Assert.That((await ReadAtVersion(db, v3, "k", this.Cancellation)).ToInt32(), Is.EqualTo(3), "the head always survives");
		}

	}

}
