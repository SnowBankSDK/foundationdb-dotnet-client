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

namespace FoundationDB.Client.Tests
{

	/// <summary>APIs added or removed at API version 800, run end to end against an 8.0 client and cluster</summary>
	/// <remarks>The API version is selected once per process: these tests run with an 8.0 native client, <c>FDB_TEST_API_VERSION=800</c> and <c>FDB_TEST_DOCKER_TAG=8.0.0</c>, and are inconclusive otherwise.</remarks>
	[TestFixture]
	public class ApiVersion800Facts : FdbTest
	{

		[Test]
		public async Task Test_Tenant_Apis_Throw_NotSupported_At_Api_800()
		{
			Assume.That(Fdb.ApiVersion, Is.GreaterThanOrEqualTo(800), "This test requires API version 800 or later.");

			using var db = await OpenTestDatabaseAsync();
			var name = FdbTenantName.Create(Literal("Tests"));

			// With fdb_c 8.0, the process aborts on any call to a native tenant function, so reaching these assertions proves that the exception came first.
			Assert.That(() => db.GetTenant(name), Throws.TypeOf<NotSupportedException>().With.Message.Contains("API version 800"));

			Assert.ThrowsAsync<NotSupportedException>(() => db.ReadAsync(tr => Fdb.Tenants.GetTenantMode(tr), this.Cancellation));
			Assert.ThrowsAsync<NotSupportedException>(() => db.WriteAsync(tr => Fdb.Tenants.CreateTenant(tr, name), this.Cancellation));
		}

		[Test]
		public async Task Test_Can_Get_Range_Split_Points_With_Limit()
		{
			Assume.That(Fdb.ApiVersion, Is.GreaterThanOrEqualTo(800), "This test requires API version 800 or later.");

			const int NUM_ITEMS = 100_000;
			const int VALUE_SIZE = 50;
			const int CHUNK_SIZE = (NUM_ITEMS * (VALUE_SIZE + 16)) / 100; // we would like to split in ~100 chunks
			const int LIMIT = 3;

			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			var rnd = new Random(123456);
			var values = Enumerable.Range(0, NUM_ITEMS).Select(_ => Slice.Random(rnd, VALUE_SIZE)).ToArray();

			const int BATCH_SIZE = 1_000_000 / VALUE_SIZE;
			Log($"Creating {values.Length:N0} keys ({VALUE_SIZE:N0} bytes per key)");
			for (int i = 0; i < values.Length; i += BATCH_SIZE)
			{
				await db.WriteAsync(async (tr) =>
				{
					var subspace = await db.Root.Resolve(tr);
					for (int j = 0; j < BATCH_SIZE && i + j < values.Length; j++)
					{
						tr.Set(subspace.Key(i + j), values[i + j]);
					}
				}, this.Cancellation);
			}

			await db.ReadAsync(async (tr) =>
			{
				var subspace = await db.Root.Resolve(tr);
				var begin = subspace.Key(0).ToSlice();
				var end = subspace.Key(values.Length).ToSlice();

				// fdb_transaction_get_range_split_points (no limit)
				var all = await tr.GetRangeSplitPointsAsync(begin, end, CHUNK_SIZE);
				Log($"Without a limit: {all.Length} keys");
				Assert.That(all, Has.Length.GreaterThan(LIMIT + 2), "The range must have more split points than the limit, or the limit is not exercised");

				// fdb_transaction_get_range_split_points_with_limit, which only fdb_c 8.0 exports
				var limited = await tr.GetRangeSplitPointsAsync(begin, end, CHUNK_SIZE, LIMIT);
				Log($"With a limit of {LIMIT}: {limited.Length} keys");
				Assert.That(limited, Has.Length.EqualTo(LIMIT + 2), "The bounds and the first split points");
				Assert.That(limited[0], Is.EqualTo(begin), "First key should be the start of the range");
				Assert.That(limited[^1], Is.EqualTo(end), "Last key should be the end of the range");
				// with a limit, the split points come from the same byte sample, cut after the limit, so they are the first ones of the unlimited call
				Assert.That(limited.Take(LIMIT + 1), Is.EqualTo(all.Take(LIMIT + 1)), "The split points should be the first split points of the unlimited call");

				var none = await tr.GetRangeSplitPointsAsync(begin, end, CHUNK_SIZE, 0);
				Assert.That(none, Is.EqualTo(new[] { begin, end }), "A limit of 0 returns only the bounds");

				var unlimited = await tr.GetRangeSplitPointsAsync(begin, end, CHUNK_SIZE, null);
				Assert.That(unlimited, Is.EqualTo(all), "A null limit returns every split point");

				Assert.That(() => tr.GetRangeSplitPointsAsync(begin, end, CHUNK_SIZE, -1), Throws.InstanceOf<ArgumentOutOfRangeException>(), "A negative limit is an argument error");
			}, this.Cancellation);
		}

	}

}
