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

// ReSharper disable StringLiteralTypo

//#define ENABLE_LOGGING

namespace FoundationDB.Linq.Tests
{
	using FoundationDB.Layers.Indexing;
	using FoundationDB.Linq.Expressions;
	using FoundationDB.Linq.Providers;

	[TestFixture]
	public class AsyncQueryableFacts : FdbTest
	{

		[Test]
		public async Task Test_AsyncQueryable_Basics()
		{
			using(var db = await OpenTestPartitionAsync())
			{
				var location = db.Root;
				await CleanLocation(db, location);

#if ENABLE_LOGGING
				db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					tr.Set(subspace.Key("Hello"), Text("World!"));
					tr.Set(subspace.Key("Narf"), Text("Zort"));
				}, this.Cancellation);

				await db.ReadAsync(async tr =>
				{
					var subspace = await location.Resolve(tr);

					var range = tr.Query().RangeStartsWith(subspace.GetPrefix());
					Assert.That(range, Is.InstanceOf<FdbAsyncSequenceQuery<KeyValuePair<Slice, Slice>>>());
					Assert.That(range.Expression, Is.InstanceOf<FdbQueryRangeExpression>());
					Log(range.Expression!.GetDebugView());

					var projection = range.Select(kvp => kvp.Value.ToString());
					Assert.That(projection, Is.InstanceOf<FdbAsyncSequenceQuery<string>>());
					Assert.That(projection.Expression, Is.InstanceOf<FdbQueryTransformExpression<KeyValuePair<Slice, Slice>, string>>());
					Log(projection.Expression!.GetDebugView());

					var results = await projection.ToListAsync();
					Log($"ToListAsync() => [ {string.Join(", ", results)} ]");

					var count = await projection.CountAsync();
					Log($"CountAsync() => {count}");
					Assert.That(count, Is.EqualTo(2));

					var first = await projection.FirstAsync();
					Log($"FirstAsync() => {first}");
					Assert.That(first, Is.EqualTo("World!"));
				}, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Query_Index_Single()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				await CleanLocation(db);

#if ENABLE_LOGGING
				db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

				var indexFoos = new FdbIndex<long, string>(db.Root);

				await db.WriteAsync(async (tr) =>
				{
					var foos = await indexFoos.Resolve(tr);
					foos.Add(tr, 1, "red");
					foos.Add(tr, 2, "green");
					foos.Add(tr, 3, "blue");
					foos.Add(tr, 4, "red");
				}, this.Cancellation);

				// find all elements that are read
				var ids = await db.ReadAsync(async _ =>
				{
					var lookup = indexFoos.Query(db).Lookup(x => x == "red");

					Assert.That(lookup, Is.InstanceOf<FdbAsyncSequenceQuery<long>>());
					Assert.That(lookup.Expression, Is.InstanceOf<FdbQueryIndexLookupExpression<long, string>>());
					Log(lookup.Expression!.GetDebugView());

					return await lookup.ToListAsync();
				}, this.Cancellation);

				Log($"=> [ {string.Join(", ", ids)} ]");

			}

		}

		[Test]
		public async Task Test_Query_Index_Range()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				await CleanLocation(db);

#if ENABLE_LOGGING
				db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

				var index = new FdbIndex<string, int>(db.Root);

				await db.WriteAsync(async (tr) =>
				{
					var foos = await index.Resolve(tr);
					foos.Add(tr, "alpha", 10);
					foos.Add(tr, "bravo", 16);
					foos.Add(tr, "charly", 12);
					foos.Add(tr, "echo", 666);
					foos.Add(tr, "foxtrot", 54321);
					foos.Add(tr, "golf", 768);
					foos.Add(tr, "tango", 12345);
					foos.Add(tr, "sierra", 667);
					foos.Add(tr, "victor", 1234);
					foos.Add(tr, "whisky", 9001);
				}, this.Cancellation);

				Log("# Find all up to 100");
				{
					var lookup = index.Query(db).Lookup(x => x <= 100);
					Assert.That(lookup, Is.InstanceOf<FdbAsyncSequenceQuery<string>>());
					Assert.That(lookup.Expression, Is.InstanceOf<FdbQueryIndexLookupExpression<string, int>>());
					Log(lookup.Expression!.GetDebugView());

					var ids = await lookup.ToListAsync();
					Log($"=> [ {string.Join(", ", ids)} ]");
					Assert.That(ids, Is.EqualTo([ "alpha", "charly", "bravo" ]));
				}

				Log("# Find all over NINE THOUSAAAAND"); //*: technically "it's over 8000" </NERD_FACT>
				{
					var lookup = index.Query(db).Lookup(x => x >= 9000);
					Assert.That(lookup, Is.InstanceOf<FdbAsyncSequenceQuery<string>>());
					Assert.That(lookup.Expression, Is.InstanceOf<FdbQueryIndexLookupExpression<string, int>>());
					Log(lookup.Expression!.GetDebugView());

					var ids = await lookup.ToListAsync();
					Log($"=> [ {string.Join(", ", ids)} ]");
					Assert.That(ids, Is.EqualTo([ "whisky", "tango", "foxtrot" ]));
				}

				Log("# > 9001");
				{
					var lookup = index.Query(db).Lookup(x => x > 9001);
					Assert.That(lookup, Is.InstanceOf<FdbAsyncSequenceQuery<string>>());
					Assert.That(lookup.Expression, Is.InstanceOf<FdbQueryIndexLookupExpression<string, int>>());
					Log(lookup.Expression!.GetDebugView());

					var ids = await lookup.ToListAsync();
					Log($"=> [ {string.Join(", ", ids)} ]");
					Assert.That(ids, Is.EqualTo([ "tango", "foxtrot" ]));
				}

				Log("# <= 10");
				{
					var lookup = index.Query(db).Lookup(x => x <= 10); // should match one
					Assert.That(lookup, Is.InstanceOf<FdbAsyncSequenceQuery<string>>());
					Assert.That(lookup.Expression, Is.InstanceOf<FdbQueryIndexLookupExpression<string, int>>());
					Log(lookup.Expression!.GetDebugView());

					var ids = await lookup.ToListAsync();
					Log($"=> [ {string.Join(", ", ids)} ]");
					Assert.That(ids, Is.EqualTo([ "alpha" ]));
				}

				Log("# < 10");
				{
					var lookup = index.Query(db).Lookup(x => x < 10); // no match expected
					Assert.That(lookup, Is.InstanceOf<FdbAsyncSequenceQuery<string>>());
					Assert.That(lookup.Expression, Is.InstanceOf<FdbQueryIndexLookupExpression<string, int>>());
					Log(lookup.Expression!.GetDebugView());

					var ids = await lookup.ToListAsync();
					Log($"=> [ {string.Join(", ", ids)} ]");
					Assert.That(ids, Is.Empty);
				}
			}

		}

	}

}
