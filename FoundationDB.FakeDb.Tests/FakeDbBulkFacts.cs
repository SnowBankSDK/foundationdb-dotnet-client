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
	using System.Collections.Generic;
	using System.Linq;
	using FoundationDB.Client;

	/// <summary>Data-correctness guard for the bulk operations after their timers moved from <see cref="System.Diagnostics.Stopwatch"/>
	/// to the database <see cref="System.TimeProvider"/>. The repository's own <c>DatabaseBulkFacts</c> is currently disabled, so
	/// these FakeDb-backed roundtrips are the active guard for the write cadence and the batched-read context.</summary>
	[TestFixture]
	[Category("FakeDb-Client")]
	public class FakeDbBulkFacts : FakeDbTest
	{

		[Test]
		public async Task Test_Bulk_WriteAsync_Roundtrips_All_Items()
		{
			var store = new FakeDbStore();
			using var db = store.OpenDatabase(null, readOnly: false);

			var data = new List<KeyValuePair<Slice, Slice>>();
			for (int i = 0; i < 500; i++)
			{
				data.Add(new(Key("w" + i.ToString("D4")), Value(i.ToString())));
			}

			long written = await Fdb.Bulk.WriteAsync(db, data, this.Cancellation);
			Assert.That(written, Is.EqualTo(500), "the bulk write must report every item");

			for (int i = 0; i < 500; i++)
			{
				var v = await db.ReadAsync(tr => tr.GetAsync(Key("w" + i.ToString("D4"))), this.Cancellation);
				Assert.That(v.ToStringUtf8(), Is.EqualTo(i.ToString()), $"item {i} must round-trip through the bulk write");
			}
		}

		[Test]
		public async Task Test_Bulk_InsertAsync_Roundtrips_All_Items()
		{
			var store = new FakeDbStore();
			using var db = store.OpenDatabase(null, readOnly: false);

			var source = Enumerable.Range(0, 500).ToList();
			long inserted = await Fdb.Bulk.InsertAsync(db, source, (i, tr) => tr.Set(Key("i" + i.ToString("D4")), Value(i.ToString())), this.Cancellation);
			Assert.That(inserted, Is.EqualTo(500), "the bulk insert must report every item");

			var check = await db.ReadAsync(tr => tr.GetAsync(Key("i0042")), this.Cancellation);
			Assert.That(check.ToStringUtf8(), Is.EqualTo("42"), "a sampled item must round-trip through the bulk insert");
		}

		[Test]
		public async Task Test_Bulk_ForEachAsync_Visits_Every_Item()
		{
			// exercises the batched-read path and BatchOperationContext (whose generation/total timers moved to db.Time)
			var store = new FakeDbStore();
			using var db = store.OpenDatabase(null, readOnly: false);

			var data = Enumerable.Range(0, 300).Select(i => new KeyValuePair<Slice, Slice>(Key("r" + i.ToString("D4")), Value(i.ToString()))).ToList();
			await Fdb.Bulk.WriteAsync(db, data, this.Cancellation);

			int seen = 0;
#pragma warning disable CS0618 // Fdb.Bulk.ForEachAsync is marked experimental
			await Fdb.Bulk.ForEachAsync(
				db,
				Enumerable.Range(0, 300).ToList(),
				async (batch, ctx) =>
				{
					foreach (var i in batch)
					{
						var v = await ctx.Transaction.GetAsync(Key("r" + i.ToString("D4")));
						if (v.HasValue) seen++;
					}
				},
				this.Cancellation);
#pragma warning restore CS0618

			Assert.That(seen, Is.EqualTo(300), "the batched read must visit every item across its generations");
		}

	}

}
