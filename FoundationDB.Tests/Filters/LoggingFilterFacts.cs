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

#pragma warning disable CS8604 // Possible null reference argument.

namespace FoundationDB.Filters.Logging.Tests
{

	[TestFixture]
	public class LoggingFilterFacts : FdbTest
	{

		[Test]
		public async Task Test_Can_Log_A_Transaction()
		{
			const int N = 10;

			using var db = await OpenTestPartitionAsync();
			// get a tuple view of the directory
			var location = db.Root;
			await CleanLocation(db, location);

			// note: ensure that all methods are JITed
			await db.WriteAsync(async (tr) =>
			{
				var subspace = await location.Resolve(tr);

				_ = await tr.GetReadVersionAsync();
				tr.Set(subspace.Key("Warmup", 0), Slice.FromInt32(1));
				tr.Clear(subspace.Key("Warmup", 1));
				_ = await tr.GetAsync(subspace.Key("Warmup", 2));
				_ = await tr.GetRange(subspace.Key("Warmup", 3).ToRange()).ToListAsync();
				tr.ClearRange(subspace.Key("Warmup", 4), subspace.Key("Warmup", 5));
			}, this.Cancellation);

			await db.WriteAsync(async (tr) =>
			{
				var subspace = await location.Resolve(tr);

				var rnd = new Random();
				tr.Set(subspace.Key("One"), Text("111111"));
				tr.Set(subspace.Key("Two"), Text("222222"));
				for (int j = 0; j < 4; j++)
				{
					for (int i = 0; i < 100; i++)
					{
						tr.Set(subspace.Key("Range", j, rnd.Next(1000)), Slice.Empty);
					}
				}
				for (int j = 0; j < N; j++)
				{
					tr.Set(subspace.Key("X", j), Slice.FromInt32(j));
					tr.Set(subspace.Key("Y", j), Slice.FromInt32(j));
					tr.Set(subspace.Key("Z", j), Slice.FromInt32(j));
					tr.Set(subspace.Key("W", j), Slice.FromInt32(j));
				}
			}, this.Cancellation);

			bool first = true;

			void LogHandler(FdbTransactionLog log)
			{
				if (first)
				{
					Log(log.GetCommandsReport());
					first = false;
				}

				Log(log.GetTimingsReport(true));
			}

			// create a logged version of the database
			db.SetDefaultLogHandler(LogHandler);

			for (int k = 0; k < N; k++)
			{
				Log("==== " + k + " ==== ");
				Log();

				await db.WriteAsync(async (tr) =>
				{
					Assert.That(tr.Log, Is.Not.Null);
					Assert.That(tr.IsLogged(), Is.True);

					var subspace = await location.Resolve(tr);

					//tr.SetOption(FdbTransactionOption.CausalReadRisky);

					_ = await tr.GetReadVersionAsync().ConfigureAwait(false);

					await tr.GetAsync(subspace.Key("One")).ConfigureAwait(false);
					await tr.GetAsync(subspace.Key("NotFound")).ConfigureAwait(false);

					tr.Set(subspace.Key("Write"), Text("abcdef" + k.ToString()));

					//tr.Annotate("BEFORE");
					//await Task.Delay(TimeSpan.FromMilliseconds(10));
					//tr.Annotate("AFTER");

					//await tr.Snapshot.GetAsync(folder.Pack("Snap")).ConfigureAwait(false);

					tr.Annotate("This is a comment");

					//await tr.GetRangeAsync(KeySelector.LastLessOrEqual(folder.Pack("A")), KeySelector.FirstGreaterThan(folder.Pack("Z"))).ConfigureAwait(false);

					await Task.WhenAll(
						tr.GetRange(subspace.Key("Range", 0).ToRange()).ToListAsync(),
						tr.GetRange(subspace.Key("Range", 1, 0), subspace.Key("Range", 1, 200)).ToListAsync(),
						tr.GetRange(subspace.Key("Range", 2, 400), subspace.Key("Range", 2, 600)).ToListAsync(),
						tr.GetRange(subspace.Key("Range", 3, 800), subspace.Key("Range", 3, 1000)).ToListAsync()
					).ConfigureAwait(false);

					await tr.GetAsync(subspace.Key("Two")).ConfigureAwait(false);

					await tr.GetValuesAsync(Enumerable.Range(0, N).Select(x => subspace.Key("X", x))).ConfigureAwait(false);

					for (int i = 0; i < N; i++)
					{
						await tr.GetAsync(subspace.Key("Z", i)).ConfigureAwait(false);
					}

					await Task.WhenAll(Enumerable.Range(0, N / 2).Select(x => tr.GetAsync(subspace.Key("Y", x)))).ConfigureAwait(false);
					await Task.WhenAll(Enumerable.Range(N / 2, N / 2).Select(x => tr.GetAsync(subspace.Key("Y", x)))).ConfigureAwait(false);

					await Task.WhenAll(
						tr.GetAsync(subspace.Key("W", 1)),
						tr.GetAsync(subspace.Key("W", 2)),
						tr.GetAsync(subspace.Key("W", 3))
					).ConfigureAwait(false);

					tr.Set(subspace.Key("Write2"), Text("ghijkl" + k.ToString()));
					tr.Clear(subspace.Key("Clear", "0"));
					tr.ClearRange(subspace.Key("Clear", "A"), subspace.Key("Clear", "Z"));

					if (tr.Context.Retries == 0)
					{
						// make it fail
						//throw new FdbException(FdbError.TransactionTooOld, "fake timeout");
					}

				}, this.Cancellation);
			}
		}

	}

}
