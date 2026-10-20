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
	using FoundationDB.FakeDb;
	using FoundationDB.Client;

	/// <summary>A key or value returned by a read belongs to the caller, as with the native client (which copies every result into managed memory):
	/// it must keep its bytes after the transaction ends, while later transactions reuse the emulator's pooled buffers.</summary>
	[TestFixture]
	[Category("FakeDb-Client")]
	public class FakeDbValueLifetimeFacts : FakeDbTest
	{

		private static Slice Payload(char c, int index) => Slice.FromStringAscii($"{c}{index:D4}-" + new string(c, 120));

		/// <summary>Runs many transactions that write and read other keys, so pooled buffers get reused</summary>
		private async Task Churn(IFdbDatabase db)
		{
			for (int round = 0; round < 50; round++)
			{
				await db.WriteAsync(async tr =>
				{
					for (int i = 0; i < 40; i++)
					{
						tr.Set(Key($"churn/{round:D3}/{i:D3}"), Payload('Z', i));
					}
					for (int i = 0; i < 40; i++)
					{
						_ = await tr.GetAsync(Key($"churn/{round:D3}/{i:D3}"));
					}
				}, this.Cancellation);
			}
		}

		private static void AssertUnchanged(string what, Slice retained, byte[] expected)
		{
			Assert.That(retained.ToArray(), Is.EqualTo(expected), $"{what} changed after its transaction ended (the emulator returned a view into a recycled buffer)");
		}

		[Test]
		public async Task Test_Read_Your_Writes_Value_Outlives_The_Transaction()
		{
			var store = new FakeDbStore();
			using var db = store.OpenDatabase(null, readOnly: false);

			var expected = Payload('A', 1).ToArray();
			var retained = await db.ReadWriteAsync(async tr =>
			{
				tr.Set(Key("ryw"), Payload('A', 1));
				return await tr.GetAsync(Key("ryw")); // served from this transaction's own writes
			}, this.Cancellation);

			await Churn(db);
			AssertUnchanged("A read-your-writes value", retained, expected);
		}

		[Test]
		public async Task Test_Committed_Value_Outlives_The_Transaction()
		{
			var store = new FakeDbStore();
			using var db = store.OpenDatabase(null, readOnly: false);

			await db.WriteAsync(tr => tr.Set(Key("committed"), Payload('B', 2)), this.Cancellation);
			var expected = Payload('B', 2).ToArray();
			var retained = await db.ReadAsync(tr => tr.GetAsync(Key("committed")), this.Cancellation);

			await Churn(db);
			AssertUnchanged("A committed value", retained, expected);
		}

		[Test]
		public async Task Test_Batch_And_Range_Results_Outlive_The_Transaction()
		{
			var store = new FakeDbStore();
			using var db = store.OpenDatabase(null, readOnly: false);

			var (values, range) = await db.ReadWriteAsync(async tr =>
			{
				for (int i = 0; i < 5; i++) tr.Set(Key($"batch/{i}"), Payload('C', i));
				var values = await tr.GetValuesAsync([ Key("batch/0"), Key("batch/1") ]);
				var range = await tr.GetRange(Key("batch/0"), Key("batch/5")).ToListAsync();
				return (values, range);
			}, this.Cancellation);

			await Churn(db);
			AssertUnchanged("A GetValuesAsync value", values[0], Payload('C', 0).ToArray());
			AssertUnchanged("A GetValuesAsync value", values[1], Payload('C', 1).ToArray());
			Assert.That(range, Has.Count.EqualTo(5));
			for (int i = 0; i < 5; i++)
			{
				AssertUnchanged("A GetRange key", range[i].Key, Key($"batch/{i}").ToArray());
				AssertUnchanged("A GetRange value", range[i].Value, Payload('C', i).ToArray());
			}
		}

		/// <summary>The directory layer caches the prefix it reads: a directory opened through the cache in the transaction that created it must keep its prefix in later transactions</summary>
		[Test]
		public async Task Test_Directory_Created_Then_Opened_From_Cache_In_The_Same_Transaction([Values(false, true)] bool openOtherFirst)
		{
			var store = new FakeDbStore();
			using var db = store.OpenDatabase(FdbPath.Root, readOnly: false);
			var other = FdbPath.Absolute("Other");
			var target = FdbPath.Absolute("Target");

			await db.WriteAsync(tr => db.DirectoryLayer.CreateAsync(tr, other), this.Cancellation);

			await db.WriteAsync(async tr =>
			{
				if (openOtherFirst) _ = await db.DirectoryLayer.TryOpenCachedAsync(tr, other);
				await db.DirectoryLayer.CreateAsync(tr, target);
				_ = await db.DirectoryLayer.TryOpenCachedAsync(tr, target); // fills the cache from this transaction's own writes
			}, this.Cancellation);

			await Churn(db);

			await db.WriteAsync(async tr =>
			{
				var cached = await db.DirectoryLayer.TryOpenCachedAsync(tr, target);
				Assert.That(cached, Is.Not.Null);
				tr.Set(cached!.Key("marker"), Slice.FromStringAscii("hello"));
			}, this.Cancellation);

			var marker = await db.ReadAsync(async tr => await tr.GetAsync((await db.DirectoryLayer.TryOpenAsync(tr, target))!.Key("marker")), this.Cancellation);
			Assert.That(marker.ToStringAscii(), Is.EqualTo("hello"), "a write through the cached subspace must land under the directory's stored prefix");
		}

		/// <summary>On a fresh database, the transaction that creates the first directory also writes the partition stamp, which the cache keeps as the expected value of later value checks</summary>
		[Test]
		public async Task Test_Directory_Cache_Filled_On_A_Fresh_Database_Passes_Later_Value_Checks()
		{
			var store = new FakeDbStore();
			using var db = store.OpenDatabase(FdbPath.Root, readOnly: false);
			var target = FdbPath.Absolute("Target");

			await db.WriteAsync(async tr =>
			{
				await db.DirectoryLayer.CreateAsync(tr, target);
				_ = await db.DirectoryLayer.TryOpenCachedAsync(tr, target);
			}, this.Cancellation);

			await Churn(db);

			int attempts = 0;
			await db.WriteAsync(async tr =>
			{
				++attempts;
				var cached = await db.DirectoryLayer.TryOpenCachedAsync(tr, target);
				tr.Set(cached!.Key("marker"), Slice.FromStringAscii("hello"));
			}, this.Cancellation);

			Assert.That(attempts, Is.EqualTo(1), "nothing changed the directory tree, so the cached value checks must pass at the first attempt");
		}
	}

}
