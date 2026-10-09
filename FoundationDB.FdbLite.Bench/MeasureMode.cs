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
	using System.Reflection;
	using System.Runtime.InteropServices;
	using System.Text.Json;

	/// <summary>One (workload, engine) result of a measure run.</summary>
	public sealed record BenchRow(
		string Workload,
		string Engine,
		int Ops,
		double MedianNsPerOp,
		double MinNsPerOp,
		double MaxNsPerOp,
		double BytesPerOp,
		long FileBytes,
		double[] Samples,
		Dictionary<string, long> Counters,
		Dictionary<string, LatencyRow>? Latency
	);

	/// <summary>Percentiles of one operation kind, in microseconds.</summary>
	public sealed record LatencyRow(int Count, double P50, double P99, double P999);

	/// <summary>A measure run, as written to the JSON file.</summary>
	public sealed record BenchReport(
		string Host,
		string Os,
		string Runtime,
		string Version,
		string Scale,
		int PageLog2,
		string Consolidation,
		DateTimeOffset Timestamp,
		List<BenchRow> Rows
	)
	{

		private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

		public static BenchReport Create(BenchOptions options)
		{
			string version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? typeof(Program).Assembly.GetName().Version?.ToString() ?? "?";
			return new(Environment.MachineName, RuntimeInformation.OSDescription, RuntimeInformation.FrameworkDescription, version, options.Scale.ToString(), options.PageLog2, "Off", DateTimeOffset.Now, []);
		}

		public void Save(string path)
		{
			File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
		}

		public static BenchReport Load(string path)
		{
			return JsonSerializer.Deserialize<BenchReport>(File.ReadAllText(path), JsonOptions) ?? throw new JsonException($"{path} holds no report");
		}

		/// <summary>Prints the delta of every row present in both files, the new median against the old one.</summary>
		public static int Compare(string oldPath, string newPath) => Compare(Load(oldPath), oldPath, Load(newPath), newPath);

		public static int Compare(BenchReport old, string oldLabel, BenchReport current, string currentLabel)
		{
			Console.WriteLine();
			Console.WriteLine($"# compare: {oldLabel} ({old.Host}, {old.Runtime}, {old.Timestamp:yyyy-MM-dd HH:mm}) -> {currentLabel} ({current.Host}, {current.Runtime}, {current.Timestamp:yyyy-MM-dd HH:mm})");
			if (old.Host != current.Host || old.Runtime != current.Runtime || old.PageLog2 != current.PageLog2 || old.Scale != current.Scale)
			{
				Console.WriteLine("# NOTE: the two runs differ in host, runtime, page size or scale; the deltas mix those differences with the change under test");
			}
			Console.WriteLine($"# a delta beyond +/-10% is marked; the min..max spread of each run says whether the delta is above the noise");
			Console.WriteLine($"{"workload",-30} {"engine",-11} {"old ns/op",12} {"new ns/op",12} {"delta",8}   {"old B/op",9} {"new B/op",9}   spread old / new");
			int matched = 0, slower = 0, faster = 0;
			foreach (var row in current.Rows)
			{
				var before = old.Rows.FirstOrDefault(r => r.Workload == row.Workload && r.Engine == row.Engine);
				if (before is null) continue;
				matched++;
				double delta = before.MedianNsPerOp == 0 ? 0 : 100.0 * (row.MedianNsPerOp - before.MedianNsPerOp) / before.MedianNsPerOp;
				string mark = delta > 10 ? ">> slower" : delta < -10 ? "<< faster" : "";
				if (delta > 10) slower++; else if (delta < -10) faster++;
				Console.WriteLine($"{row.Workload,-30} {row.Engine,-11} {before.MedianNsPerOp,12:N0} {row.MedianNsPerOp,12:N0} {delta,7:+0.0;-0.0;0.0}%   {before.BytesPerOp,9:N1} {row.BytesPerOp,9:N1}   {Spread(before)} / {Spread(row)}  {mark}");
			}
			Console.WriteLine($"# {matched} rows compared: {slower} slower by more than 10%, {faster} faster by more than 10%, {current.Rows.Count - matched} rows without a counterpart");
			return 0;
		}

		private static string Spread(BenchRow r) => r.MedianNsPerOp == 0 ? "n/a" : $"{100.0 * (r.MaxNsPerOp - r.MinNsPerOp) / r.MedianNsPerOp:N0}%";

	}

	/// <summary>Times every workload on every engine: warm-up passes until a pass stops beating the best one, then the timed passes, engines interleaved per pass.</summary>
	/// <remarks>
	/// <para>Tiered compilation is the largest source of false findings in a harness of this kind: the first few hundred thousand operations of a process run in tier-0 code, and a single discarded pass does not outlast the background tier-1 compile. The warm-up therefore runs until a pass no longer improves on the best by more than 5%, up to the ceiling.</para>
	/// <para>A workload whose steps write nothing runs every pass over one prepared store per engine. A mutating workload rebuilds its store for every pass, since a second replay would start from a different state.</para>
	/// </remarks>
	public static class MeasureMode
	{

		private const double WarmupTolerance = 0.05;

		public static int Run(BenchOptions options)
		{
			var loads = options.Loads();
			options.PrintHeader(loads.Count);
			Console.WriteLine($"# warm-up until a pass stops beating the best by more than {WarmupTolerance:P0} (at most {options.MaxWarmup} passes), then {options.Repeats} timed passes; engines interleaved per pass");
			Console.WriteLine($"# median ns/op with the min..max spread, managed bytes allocated per op, file size; a wide spread means the number is not usable");
			if (options.Latency) Console.WriteLine($"# per-operation latency on (timer resolution {LatencyProfile.TimerResolutionNanos:N0} ns): p50/p99/p999 per operation kind, in us");

			var report = BenchReport.Create(options);
			var sw = Stopwatch.StartNew();
			JitWarmup(options);
			foreach (var load in loads)
			{
				Console.WriteLine();
				Console.WriteLine($"== {load.Name}  [{load.Shape}]  {load.Ops:N0} ops");
				var samples = new Dictionary<string, List<double>>();
				var allocs = new Dictionary<string, List<double>>();
				var last = new Dictionary<string, RunResult>();
				var latency = new Dictionary<string, LatencyProfile>();
				var failed = new HashSet<string>();
				foreach (var e in options.Engines) { samples[e] = []; allocs[e] = []; latency[e] = new LatencyProfile(); }

				// a read-only workload is prepared once per engine, and every pass re-runs only the timed section
				var prepared = new Dictionary<string, IKvDriver>();
				try
				{
					if (load.ReadOnlySteps)
					{
						foreach (var engine in options.Engines)
						{
							try
							{
								var d = options.Open(engine, load.Name);
								Replay.Run(d, load.Prepare, out _);
								prepared[engine] = d;
							}
							catch (Exception ex)
							{
								failed.Add(engine);
								Console.WriteLine($"   {engine,-11} FAILED: {ex.GetType().Name}: {ex.Message}");
							}
						}
					}

					RunResult Pass(string engine) => prepared.TryGetValue(engine, out var store)
						? Replay.Timed(store, load, options.Latency)
						: Replay.Once(options, engine, load);

					foreach (var engine in options.Engines)
					{
						if (failed.Contains(engine)) continue;
						double best = double.MaxValue;
						for (int w = 0; w < options.MaxWarmup; w++)
						{
							double ns;
							try { ns = Pass(engine).NsPerOp; }
							catch (Exception) { break; } // reported by the timed loop below
							if (ns >= best * (1.0 - WarmupTolerance)) break; // no longer improving: steady state
							best = ns;
						}
					}

					for (int i = 1; i <= options.Repeats; i++)
					{
						// interleaved within the pass, so a drift over the run lands on every engine alike
						foreach (var engine in options.Engines)
						{
							if (failed.Contains(engine)) continue;
							try
							{
								var run = Pass(engine);
								samples[engine].Add(run.NsPerOp);
								allocs[engine].Add(run.BytesPerOp);
								last[engine] = run;
								if (run.Latency is { } lp) latency[engine].Merge(lp);
							}
							catch (Exception ex)
							{
								failed.Add(engine);
								Console.WriteLine($"   {engine,-11} FAILED: {ex.GetType().Name}: {ex.Message}");
							}
						}
					}
				}
				finally
				{
					foreach (var store in prepared.Values) store.Dispose();
				}

				foreach (var engine in options.Engines)
				{
					var s = samples[engine];
					if (s.Count == 0) continue;
					s.Sort();
					double median = s[s.Count / 2];
					var a = allocs[engine];
					a.Sort();
					var run = last[engine];
					string counters = string.Join(" ", run.Counters.Where(kv => kv.Key is "pagesWritten" or "leafPages" or "leafFillPct" or "keyCount").Select(kv => $"{kv.Key}={kv.Value:N0}"));
					Console.WriteLine($"   {engine,-11} {median,10:N0} ns/op  ({s[0]:N0}..{s[^1]:N0})  {a[a.Count / 2],8:N1} B/op  {(run.FileSize == 0 ? "in-memory" : $"file {run.FileSize >> 10:N0} KiB"),-16} {counters}");

					Dictionary<string, LatencyRow>? latencyRows = null;
					if (options.Latency)
					{
						latencyRows = [];
						foreach (var (name, h) in latency[engine].All())
						{
							if (h.Count == 0) continue;
							latencyRows[name] = new(h.Count, h.Percentile(50), h.Percentile(99), h.Percentile(99.9));
							Console.WriteLine($"      {name,-10} n={h.Count,10:N0}  p50 {h.Percentile(50),9:N2} us  p99 {h.Percentile(99),9:N2} us  p999 {h.Percentile(99.9),9:N2} us");
						}
						Console.WriteLine($"      slow (> {LatencyProfile.SlowThresholdNanos:N0} ns): {latency[engine].DescribeClustering(load.Ops)}");
					}
					report.Rows.Add(new(load.Name, engine, load.Ops, median, s[0], s[^1], a[a.Count / 2], run.FileSize, s.ToArray(), run.Counters, latencyRows));
				}
			}

			Console.WriteLine();
			Console.WriteLine($"# measure: {report.Rows.Count} rows in {sw.Elapsed.TotalSeconds:N1} s");
			if (options.Out is { } outPath)
			{
				report.Save(outPath);
				Console.WriteLine($"# written to {outPath}");
			}
			if (options.Compare is { } oldPath)
			{
				BenchReport.Compare(BenchReport.Load(oldPath), oldPath, report, options.Out ?? "this run");
			}
			return 0;
		}

		/// <summary>Drives a small workload through every engine before any measurement, off the clock.</summary>
		/// <remarks>The per-workload warm-up does not cover the first-ever JIT of the shared path (the replay loop, the slice handling, the step iteration): whichever (engine, workload) runs first would pay it, and the first row of the matrix would read several times slower than its steady state. A few thousand operations are needed, since a handful of calls does not reach tier 1.</remarks>
		private static void JitWarmup(BenchOptions options)
		{
			var load = Workloads.Insert("jit.warmup", KeyShape.Index, 4_000, 8, AccessOrder.Random, 1_000);
			var probe = Workloads.Read("jit.warmup.read", KeyShape.Index, 4_000, 4_000, 8, AccessOrder.Random);
			foreach (var engine in options.Engines)
			{
				try
				{
					for (int i = 0; i < 3; i++)
					{
						Replay.Once(options, engine, load);
						Replay.Once(options, engine, probe);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"# JIT warm-up for {engine} failed ({ex.GetType().Name}): the first rows of {engine} may include compilation");
				}
			}
		}

	}

}
