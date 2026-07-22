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

#define FULL_DEBUG

namespace FoundationDB.Layers.Collections.Tests
{
	public abstract class RankedTestFacts : FdbTest
	{
		[Test]
		public async Task Test_Vector_Fast()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				await CleanLocation(db);

#if ENABLE_LOGGING
				db.SetDefaultLogHandler((log) => Log(log.GetTimingsReport(true)));
#endif

				var rankedSet = new FdbRankedSet(db.Root);
				await db.WriteAsync(tr => rankedSet.OpenAsync(tr), this.Cancellation);

				Log(await rankedSet.ReadAsync(db, (tr, state) => PrintRankedSet(state, tr), this.Cancellation));
				Log();

				var rnd = new Random();
				var sw = Stopwatch.StartNew();
				for (int i = 0; i < 100; i++)
				{
					//Log("Inserting " + i);
					await db.WriteAsync(async tr =>
					{
						var state = await rankedSet.Resolve(tr);
						await state.InsertAsync(tr, TuPack.EncodeKey(rnd.Next()));
					}, this.Cancellation);
				}
				sw.Stop();
				Log($"Done in {sw.Elapsed.TotalSeconds:N3} sec");
#if FULL_DEBUG
				await DumpSubspace(db);
#endif

				Log(await rankedSet.ReadAsync(db, (tr, state) => PrintRankedSet(state, tr), this.Cancellation));
			}
		}

		private static async Task<string> PrintRankedSet(FdbRankedSet.State rs, IFdbReadOnlyTransaction tr)
		{
			var sb = new StringBuilder();
			for (int l = 0; l < 6; l++)
			{
				sb.AppendInvariant($"Level {l}:\r\n");
				await tr.GetRange(rs.Subspace.Key(l).ToRange()).ForEachAsync((kvp) =>
				{
					sb.AppendInvariant($"\t{rs.Subspace.Unpack(kvp.Key)} = {kvp.Value.ToInt64()}\r\n");
				});
			}
			return sb.ToString();
		}

	}


	/// <summary>Runs the suite against the in-memory FakeDb emulator (no Docker, no native client) for fast iteration.</summary>
	[TestFixture]
	public sealed class RankedTestFactsFakeDbFacts : RankedTestFacts
	{

		private FakeDbTestBackend Backend { get; } = new();

		protected override bool UseRealServer => false;

		[TearDown]
		public void ResetBackend() => this.Backend.Reset();

		protected override Task<IFdbDatabase> OpenTestDatabaseAsync(bool readOnly = false) => this.Backend.OpenAsync(FdbPath.Root, readOnly);

		protected override Task<IFdbDatabase> OpenTestPartitionAsync(string? testMethod = null) => this.Backend.OpenAsync(GetTestPartitionPath(testMethod));

	}

	/// <summary>Runs the suite against a real FoundationDB cluster (Testcontainers). Run explicitly from the Unit Test Sessions UI or with the <c>RealCluster</c> category; requires a local Docker daemon and the native client.</summary>
	[TestFixture, Explicit("Requires a local Docker daemon"), Category("RealCluster")]
	public sealed class RankedTestFactsRealClusterFacts : RankedTestFacts
	{
		// inherits the full FdbTest behavior: container startup, native client probing, real connection
	}

}
