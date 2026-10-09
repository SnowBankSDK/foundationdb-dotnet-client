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

namespace FoundationDB.FdbLite.Bench
{
	using System.Runtime.CompilerServices;

	/// <summary>Driver for a sampling profiler: a warm-up pass over every selected workload, then 3 marked rounds per workload with 2-second idle gaps, so each workload shows up as a named, isolated burst in the timeline.</summary>
	/// <remarks>
	/// <para>Each workload runs inside its own <c>Workload_*</c> frame, marked <see cref="MethodImplOptions.NoInlining"/>, so a call tree and the allocation backtraces split by workload instead of pooling under the replay loop. A workload without a frame (a new one, not yet listed in <see cref="RunNamed"/>) still runs, under the plain replay frame, and the header says so.</para>
	/// <para>No statistics: the profiler is the instrument. The <c>###</c> markers carry a wall-clock stamp to correlate with the timeline.</para>
	/// </remarks>
	public static class ProfileMode
	{

		private const double WarmupTolerance = 0.05;

		public static int Run(BenchOptions options)
		{
			var loads = options.Loads();
			options.PrintHeader(loads.Count);
			Console.WriteLine($"# profile: warm-up then 3 marked rounds per workload and engine, 2 s gaps; correlate the ### markers with the profiler timeline by wall clock");
			foreach (var load in loads.Where(l => !HasFrame(l)))
			{
				Console.WriteLine($"# NOTE: '{load.Name}' has no Workload_* frame (add it to ProfileMode.RunNamed); it profiles under the plain replay frame");
			}

			static void Mark(string what) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ### {what}");

			// warm-up to a steady state: tier-1 code arrives from a background compile, and the 250 ms pause
			// between passes is what lets it land before the next pass judges it
			Mark("warmup BEGIN");
			foreach (var engine in options.Engines)
			{
				foreach (var load in loads)
				{
					double best = double.MaxValue;
					for (int w = 0; w < Math.Max(2, options.MaxWarmup); w++)
					{
						double ns;
						try { ns = RunNamed(options, engine, load).NsPerOp; }
						catch (Exception) { break; }
						Thread.Sleep(250);
						if (w >= 1 && ns >= best * (1 - WarmupTolerance)) break;
						if (ns < best) best = ns;
					}
				}
			}
			Mark("warmup END (settling 3 s)");
			Thread.Sleep(3_000);

			foreach (var load in loads)
			{
				foreach (var engine in options.Engines)
				{
					for (int round = 1; round <= 3; round++)
					{
						Thread.Sleep(2_000);
						Mark($"{load.Name} [{engine}] round {round} BEGIN");
						try
						{
							var result = RunNamed(options, engine, load);
							Mark($"{load.Name} [{engine}] round {round} END ns/op={result.NsPerOp:F0}");
						}
						catch (Exception e)
						{
							Mark($"{load.Name} [{engine}] round {round} FAILED {e.GetType().Name}: {e.Message}");
							break;
						}
					}
				}
			}
			Thread.Sleep(2_000);
			Mark("profile COMPLETE");
			return 0;
		}

		private static bool HasFrame(Workload load) => load.Name is
			"insert.seq.idx.v0" or "insert.seq.idx.v8" or "insert.seq.idx.v64" or "insert.seq.idx.v1k"
			or "insert.seq.flat.v8" or "insert.seq.scatter.v8" or "insert.rand.idx.v8" or "insert.seq.idx.v8.batch1k"
			or "insert.rand.idx.v8.batch100" or "insert.rand.idx.v8.batch10"
			or "replace.same.seq" or "replace.same.rand" or "replace.same.skew" or "replace.grow.rand"
			or "replace.shrink.rand" or "replace.same.v1k"
			or "delete.rand.idx.v8" or "delete.seq.idx.v8" or "delete.range.idx.v8"
			or "read.seq.idx.v8" or "read.rand.idx.v8" or "read.stride.idx.v8" or "read.skew.idx.v8"
			or "read.stride.idx.v1k" or "read.stride.flat.v8"
			or "range.10.idx.v8" or "range.1k.idx.v8" or "range.1k.idx.v8.verify" or "range.1k.idx.v8.keysonly" or "range.1k.idx.v8.reverse"
			or "range.1k.idx.v1k" or "range.full.idx.v8"
			or "ledger.append.churn";

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static RunResult RunNamed(BenchOptions o, string engine, Workload load) => load.Name switch
		{
			"insert.seq.idx.v0" => Workload_insert_seq_idx_v0(o, engine, load),
			"insert.seq.idx.v8" => Workload_insert_seq_idx_v8(o, engine, load),
			"insert.seq.idx.v64" => Workload_insert_seq_idx_v64(o, engine, load),
			"insert.seq.idx.v1k" => Workload_insert_seq_idx_v1k(o, engine, load),
			"insert.seq.flat.v8" => Workload_insert_seq_flat_v8(o, engine, load),
			"insert.seq.scatter.v8" => Workload_insert_seq_scatter_v8(o, engine, load),
			"insert.rand.idx.v8" => Workload_insert_rand_idx_v8(o, engine, load),
			"insert.seq.idx.v8.batch1k" => Workload_insert_seq_idx_v8_batch1k(o, engine, load),
			"insert.rand.idx.v8.batch100" => Workload_insert_rand_idx_v8_batch100(o, engine, load),
			"insert.rand.idx.v8.batch10" => Workload_insert_rand_idx_v8_batch10(o, engine, load),
			"replace.same.seq" => Workload_replace_same_seq(o, engine, load),
			"replace.same.rand" => Workload_replace_same_rand(o, engine, load),
			"replace.same.skew" => Workload_replace_same_skew(o, engine, load),
			"replace.grow.rand" => Workload_replace_grow_rand(o, engine, load),
			"replace.shrink.rand" => Workload_replace_shrink_rand(o, engine, load),
			"replace.same.v1k" => Workload_replace_same_v1k(o, engine, load),
			"delete.rand.idx.v8" => Workload_delete_rand_idx_v8(o, engine, load),
			"delete.seq.idx.v8" => Workload_delete_seq_idx_v8(o, engine, load),
			"delete.range.idx.v8" => Workload_delete_range_idx_v8(o, engine, load),
			"read.seq.idx.v8" => Workload_read_seq_idx_v8(o, engine, load),
			"read.rand.idx.v8" => Workload_read_rand_idx_v8(o, engine, load),
			"read.stride.idx.v8" => Workload_read_stride_idx_v8(o, engine, load),
			"read.skew.idx.v8" => Workload_read_skew_idx_v8(o, engine, load),
			"read.stride.idx.v1k" => Workload_read_stride_idx_v1k(o, engine, load),
			"read.stride.flat.v8" => Workload_read_stride_flat_v8(o, engine, load),
			"range.10.idx.v8" => Workload_range_10_idx_v8(o, engine, load),
			"range.1k.idx.v8" => Workload_range_1k_idx_v8(o, engine, load),
			"range.1k.idx.v8.verify" => Workload_range_1k_idx_v8_verify(o, engine, load),
			"range.1k.idx.v8.keysonly" => Workload_range_1k_idx_v8_keysonly(o, engine, load),
			"range.1k.idx.v8.reverse" => Workload_range_1k_idx_v8_reverse(o, engine, load),
			"range.1k.idx.v1k" => Workload_range_1k_idx_v1k(o, engine, load),
			"range.full.idx.v8" => Workload_range_full_idx_v8(o, engine, load),
			"ledger.append.churn" => Workload_ledger_append_churn(o, engine, load),
			_ => Replay.Once(o, engine, load),
		};

		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_insert_seq_idx_v0(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_insert_seq_idx_v8(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_insert_seq_idx_v64(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_insert_seq_idx_v1k(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_insert_seq_flat_v8(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_insert_seq_scatter_v8(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_insert_rand_idx_v8(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_insert_seq_idx_v8_batch1k(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_insert_rand_idx_v8_batch100(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_insert_rand_idx_v8_batch10(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_replace_same_seq(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_replace_same_rand(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_replace_same_skew(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_replace_grow_rand(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_replace_shrink_rand(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_replace_same_v1k(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_delete_rand_idx_v8(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_delete_seq_idx_v8(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_delete_range_idx_v8(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_read_seq_idx_v8(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_read_rand_idx_v8(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_read_stride_idx_v8(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_read_skew_idx_v8(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_read_stride_idx_v1k(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_read_stride_flat_v8(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_range_10_idx_v8(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_range_1k_idx_v8(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_range_1k_idx_v8_verify(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_range_1k_idx_v8_keysonly(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_range_1k_idx_v8_reverse(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_range_1k_idx_v1k(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_range_full_idx_v8(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);
		[MethodImpl(MethodImplOptions.NoInlining)] private static RunResult Workload_ledger_append_churn(BenchOptions o, string engine, Workload load) => Replay.Once(o, engine, load);

	}

}
