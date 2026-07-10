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
	using System.Diagnostics;
	using FoundationDB.Testing;

	/// <summary>Empirical probes that settle the comparison rules the campaign corpora depend on; run against the real cluster, conclusions are recorded in the campaign notes.</summary>
	/// <remarks>These are measurements, not assertions: they log observed behavior and only assert the invariants that scenarios would rely on.</remarks>
	[TestFixture, Explicit("Requires a local Docker daemon"), Category("RealCluster")]
	public class ScenarioProbeFacts : FdbTest
	{

		[Test]
		public async Task Probe_TransactionOrder_Under_Concurrent_Commits()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			// N transactions committed concurrently: do batched commits share a TransactionVersion
			// with distinct TransactionOrder values, or does each get its own version?
			const int N = 16;
			var stamps = await Task.WhenAll(Enumerable.Range(0, N).Select(async i =>
			{
				using var tr = db.BeginTransaction(FdbTransactionMode.Default, this.Cancellation);
				var subspace = await db.Root.Resolve(tr);
				tr.Set(subspace.Key("probe", i), Text($"v{i}"));
				var pending = tr.GetVersionStampAsync();
				await tr.CommitAsync();
				return await pending;
			}));

			var groups = stamps.GroupBy(s => s.TransactionVersion).OrderBy(g => g.Key).ToList();
			foreach (var g in groups)
			{
				Log($"version {g.Key:x16}: orders [{string.Join(", ", g.Select(s => s.TransactionOrder).OrderBy(o => o))}]");
			}
			Log($"=> {groups.Count} distinct versions for {N} concurrent commits; max TransactionOrder = {stamps.Max(s => s.TransactionOrder)}");

			// invariant scenarios rely on: stamps are unique
			Assert.That(stamps.Select(s => (s.TransactionVersion, s.TransactionOrder)).Distinct().Count(), Is.EqualTo(N), "versionstamps must be unique across concurrent commits");
		}

		[Test]
		public async Task Probe_Approximate_Size_Formulas()
		{
			// measures the native client's per-operation approximate-size accounting at two key sizes,
			// to calibrate FakeDb's implementation (FDBV-007)
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			await db.ReadWriteAsync(async tr =>
			{
				long size = await tr.GetApproximateSizeAsync();

				async Task<long> Delta(string label)
				{
					long now = await tr.GetApproximateSizeAsync();
					long delta = now - size;
					size = now;
					Log($"> {label}: +{delta}");
					return delta;
				}

				foreach (int keyLen in (int[]) [ 10, 50 ])
				{
					var key = Slice.FromStringAscii(new string('k', keyLen));
					var key2 = Slice.FromStringAscii(new string('l', keyLen));
					var value = Slice.FromStringAscii(new string('v', 100));

					await tr.GetAsync(key);
					await Delta($"get(k{keyLen})");

					tr.Set(key, value);
					await Delta($"set(k{keyLen}, v100)");

					tr.Clear(key2);
					await Delta($"clear(k{keyLen})");

					tr.ClearRange(key, key2);
					await Delta($"clearRange(k{keyLen}, k{keyLen})");

					tr.AtomicAdd64(key, 1);
					await Delta($"atomicAdd(k{keyLen}, 8)");
				}

				tr.Reset();
				long afterReset = await tr.GetApproximateSizeAsync();
				Log($"> after reset: {afterReset}");
				return 0;
			}, this.Cancellation);
		}

		[Test]
		public async Task Probe_ReadVersion_Advance_And_Idle()
		{
			using var db = await OpenTestPartitionAsync();
			await CleanLocation(db);

			// After each commit: does an immediate GRV equal the commit version, and does the
			// version advance during an idle gap with no commits?
			int equalImmediate = 0, aheadImmediate = 0, advancedAfterIdle = 0;
			long maxImmediateDelta = 0, maxIdleDelta = 0;

			for (int i = 0; i < 10; i++)
			{
				long committed;
				using (var tr = db.BeginTransaction(FdbTransactionMode.Default, this.Cancellation))
				{
					var subspace = await db.Root.Resolve(tr);
					tr.Set(subspace.Key("load", i), Text("x"));
					await tr.CommitAsync();
					committed = tr.GetCommittedVersion();
				}

				long immediate = await db.ReadAsync(tr => tr.GetReadVersionAsync(), this.Cancellation);
				if (immediate == committed) { equalImmediate++; } else { aheadImmediate++; maxImmediateDelta = Math.Max(maxImmediateDelta, immediate - committed); }

				var sw = Stopwatch.StartNew();
				await Task.Delay(200, this.Cancellation);
				long afterIdle = await db.ReadAsync(tr => tr.GetReadVersionAsync(), this.Cancellation);
				if (afterIdle > immediate) { advancedAfterIdle++; maxIdleDelta = Math.Max(maxIdleDelta, afterIdle - immediate); }

				Log($"commit={committed:x} grv+0={immediate:x} (delta {immediate - committed}) grv+{sw.ElapsedMilliseconds}ms={afterIdle:x} (delta {afterIdle - immediate})");

				// invariant scenarios rely on: a GRV is never behind a commit this client observed
				Assert.That(immediate, Is.GreaterThanOrEqualTo(committed), "GRV must include the last commit");
			}

			Log($"=> immediate GRV: {equalImmediate}x equal, {aheadImmediate}x ahead (max +{maxImmediateDelta}); idle 200ms: advanced {advancedAfterIdle}/10 times (max +{maxIdleDelta})");
		}

	}

}
