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
	using System;
	using FoundationDB.Storage.FdbLite;
	using FoundationDB.Testing;

	/// <summary>The owner's acceptance benchmark for the span-first range read (perf-goals.md scenario 1): a read-only aggregate scan over a large range must allocate O(1) in the number of pairs.</summary>
	/// <remarks>
	/// <para>Seeds N int32 pairs into an FdbLite store, then sums the values in ONE read-only transaction via the value
	/// decoder (<c>ReadOnlySpan&lt;byte&gt; -&gt; int32</c>) folded into a <c>long</c> - no per-pair <see cref="Slice"/>
	/// materialization. Because the range read now streams straight off the mapped pages, total bytes allocated is FLAT
	/// across N: a one-million-pair sum allocates essentially the same as a one-thousand-pair one (only the fixed
	/// transaction / context / task shell, which the owner's goal explicitly allows).</para>
	/// <para>The sweep is logged; the slope guard (bytes-per-extra-pair bounded well below a per-pair copy) fails the
	/// build if per-pair allocation ever returns to this path. Docker-free (heap pager).</para>
	/// </remarks>
	[TestFixture]
	[Category("FdbLite")]
	public class FdbLiteAggregateScanFacts : FakeDbScenarioTest
	{

		/// <summary>Fast always-run guard: a 100x range is enough to catch a per-pair regression (seconds to seed).</summary>
		[Test]
		public Task Aggregate_Scan_Allocation_Is_Flat_In_N() => RunSweepAsync([ 1_000, 100_000 ]);

		/// <summary>The owner's headline acceptance: the full one-thousand / one-hundred-thousand / one-million sweep. Opt-in (seeding a million pairs is the slow part); run it from the Unit Test Sessions UI for the marquee number.</summary>
		[Test]
		[Explicit("seeds up to 1M pairs (~minutes); the fast guard above runs always")]
		public Task Aggregate_Scan_Allocation_Is_Flat_To_One_Million() => RunSweepAsync([ 1_000, 100_000, 1_000_000 ]);

		private async Task RunSweepAsync(int[] sweep)
		{
			var results = new (int N, long BytesPerScan)[sweep.Length];

			for (int s = 0; s < sweep.Length; s++)
			{
				int n = sweep[s];
				using var store = FdbLiteStore.CreateInMemory(FdbLiteGeometry.Hypothesis);
				using IFdbDatabase db = store.OpenDatabase(GetTestPartitionPath($"agg_{n}"), readOnly: false);
				await CleanLocation(db);

				await SeedAsync(db, n, this.Cancellation);

				// resolve the folder bounds ONCE (outside the measured fold) so the scan's allocation is not masked by
				// the directory resolution: the measured transaction is pure range-read + fold
				var (beginKey, endKey) = await db.ReadAsync(async tr =>
				{
					var folder = await db.Root.Resolve(tr);
					return (folder.Key(0).ToSlice(), folder.Key(n).ToSlice());
				}, this.Cancellation);

				long expected = (long) n * (n - 1) / 2;
				long bytes = MeasureFoldBytes(db, beginKey, endKey, this.Cancellation, out long sum);
				Assert.That(sum, Is.EqualTo(expected), $"N={n}: the fold read the wrong values");

				results[s] = (n, bytes);
			}

			Log("aggregate scan allocation (total bytes / scan), span->int32 fold via VisitRangeAsync(WantAllValuesOnly):");
			foreach (var (n, bytes) in results)
			{
				Log($"  N = {n,10:N0}   bytes/scan = {bytes,12:N0}");
			}

			// the mechanism is O(1) in N: the delta from the smallest to the largest scan, spread over the extra pairs,
			// must be a tiny fraction of a per-pair copy (~tens of bytes). A copying regression makes this ~40 B/pair.
			var small = results[0];
			var large = results[^1];
			double bytesPerExtraPair = (double) (large.BytesPerScan - small.BytesPerScan) / (large.N - small.N);
			Log($"  slope = {bytesPerExtraPair:F4} bytes per extra pair (flat << 1; a per-pair copy would be ~40+)");
			Assert.That(bytesPerExtraPair, Is.LessThan(2.0), "aggregate scan allocation scales with N - per-pair materialization is back on the range path");
		}

		private async Task SeedAsync(IFdbDatabase db, int n, System.Threading.CancellationToken ct)
		{
			const int Batch = 50_000;
			for (int start = 0; start < n; start += Batch)
			{
				int end = Math.Min(start + Batch, n);
				await db.WriteAsync(async tr =>
				{
					var folder = await db.Root.Resolve(tr);
					for (int i = start; i < end; i++)
					{
						tr.Set(folder.Key(i), Slice.FromInt32(i));
					}
				}, ct);
			}
		}

		private static long MeasureFoldBytes(IFdbDatabase db, Slice beginKey, Slice endKey, System.Threading.CancellationToken ct, out long sum)
		{
			sum = RunFold(db, beginKey, endKey, ct); // warm (JIT + directory cache)

			const int K = 4;
			long before = GC.GetTotalAllocatedBytes(precise: true);
			long s = 0;
			for (int i = 0; i < K; i++) { s = RunFold(db, beginKey, endKey, ct); }
			long after = GC.GetTotalAllocatedBytes(precise: true);
			sum = s;
			return (after - before) / K;
		}

		private static long RunFold(IFdbDatabase db, Slice beginKey, Slice endKey, System.Threading.CancellationToken ct)
			=> db.ReadAsync(async tr =>
			{
				var acc = new long[1];
				await tr.VisitRangeAsync(
					beginKey,
					endKey,
					acc,
					static (long[] state, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value) => state[0] += value.ToInt32(),
					FdbRangeOptions.WantAllValuesOnly);
				return acc[0];
			}, ct).GetAwaiter().GetResult();

	}

}
