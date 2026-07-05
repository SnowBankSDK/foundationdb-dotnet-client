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

namespace SnowBank.Numerics
{
	using System.Linq;

	/// <summary>Helper for measuring the results of benchmarks</summary>
	[PublicAPI]
	public static class RobustBenchmark
	{

		/// <summary>Pre-JIT this type before starting a benchmark</summary>
		public static void Warmup()
		{
			try
			{
				_ = RobustBenchmark.Median([ RobustBenchmark.GetElapsedTime(Stopwatch.GetTimestamp()), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) ]);
				_ = RobustBenchmark.Run(() => { }, 3, 10, new RobustHistogram());
			}
			// ReSharper disable once EmptyGeneralCatchClause
			catch { }
		}

		/// <summary>Data for a run of the benchmark</summary>
		/// <typeparam name="TResult"></typeparam>
		[DebuggerDisplay("Index={Index}, {Rejected?\"BAD \":\"GOOD \"}, Duration={Duration}, Iterations={Iterations}, Result={Result}")]
		[PublicAPI]
		public sealed class RunData<TResult>
		{
			/// <summary>Run number</summary>
			public int Index { get; set; }

			/// <summary>Number of iterations in this run</summary>
			public long Iterations { get; set; }

			/// <summary>Total duration of this run</summary>
			public TimeSpan Duration { get; set; }

			/// <summary>If <c>true</c> this run is rejected (outlier)</summary>
			public bool Rejected { get; set; }

			/// <summary>Result of this run</summary>
			public required TResult Result { get; set; }

		}

		/// <summary>Report for a benchmark session</summary>
		/// <typeparam name="TResult"></typeparam>
		[DebuggerDisplay("Runs={NumberOfRuns}, Iterations={IterationsPerRun}, Duration={TotalDuration}, BestIps={BestIterationsPerSecond}, BestNanos={BestIterationsNanos}")]
		[PublicAPI]
		public sealed class Report<TResult>
		{

			/// <summary>Number of runs in the session</summary>
			public int NumberOfRuns { get; set; }

			/// <summary>Number of iterations per run</summary>
			public int IterationsPerRun { get; set; }

			/// <summary>Total duration of all runs</summary>
			public TimeSpan RawTotal { get; set; }

			/// <summary>List of the durations for each run</summary>
			public required List<TimeSpan> RawTimes { get; set; }

			/// <summary>List of the results for each run</summary>
			public required List<TResult> Results { get; set; }

			/// <summary>List of the raw data for each run</summary>
			public List<RunData<TResult>>? Runs { get; set; }

			/// <summary>Number of rejected runs</summary>
			public int RejectedRuns { get; set; }

			/// <summary>List of the durations of each accepted run</summary>
			public List<TimeSpan>? Times { get; set; }

			/// <summary>Total number of iterations across all accepted runs</summary>
			public long TotalIterations { get; set; }

			/// <summary>Total duration of all accepted runs</summary>
			public TimeSpan TotalDuration { get; set;  }

			/// <summary>Average duration of all accepted runs</summary>
			public TimeSpan AverageDuration { get; set; }

			/// <summary>Median duration of all accepted runs</summary>
			public TimeSpan MedianDuration { get; set; }

			/// <summary>Median iterations per second of all accepted runs</summary>
			public double MedianIterationsPerSecond { get; set; }

			/// <summary>Median nanoseconds par iteration of all accepted runs</summary>
			public double MedianIterationsNanos { get; set; }

			/// <summary>Duration of the fastest accepted run</summary>
			public TimeSpan BestDuration { get; set; }

			/// <summary>Iterations per seconds of the fastest accepted run</summary>
			public double BestIterationsPerSecond { get; set; }

			/// <summary>Nanoseconds per iteration of the fastest accepted run</summary>
			public double BestIterationsNanos { get; set; }

			/// <summary>Standard deviation of the duration of all accepted runs</summary>
			public TimeSpan StdDevDuration { get; set; }

			/// <summary>Standard deviation of the nanoseconds per iteration of all accepted runs</summary>
			public double StdDevIterationNanos { get; set; }

			// ReSharper disable InconsistentNaming

			/// <summary>Number of garbage collections observed on the current thread during the execution of the session</summary>
			public long? GcAllocatedOnThread { get; set; }

			/// <summary>Number of gen0 collection per 1 million iterations</summary>
			public double GC0 { get; set; }

			/// <summary>Number of gen2 collection per 1 million iterations</summary>
			public double GC1 { get; set; }

			/// <summary>Number of gen2 collection per 1 million iterations</summary>
			public double GC2 { get; set; }

			// ReSharper restore InconsistentNaming

			/// <summary>Total raw time of the best run</summary>
			public TimeSpan BestRunTotalTime { get; set; }

			/// <summary>Histogram of the measurements (if enabled)</summary>
			public RobustHistogram? Histogram { get; set; }

		}

		/// <summary>Runs a benchmark</summary>
		public static Report<long> Run(Action test, int runs, int iterations, RobustHistogram? h = null)
		{
			return Run(
				global: test,
				setup: (_) => (long) iterations,
				test: static (g, _, _) =>
				{
					g();
					return 0;
				},
				cleanup: (_, _) => (long) iterations,
				runs,
				iterations,
				h
			);
		}

		/// <summary>Runs a benchmark</summary>
		public static Report<long> Run<T>(Func<T> test, int runs, int iterations, RobustHistogram? h = null)
		{
			return Run(
				global: test,
				setup: (_) => (long) iterations,
				test: static (g, _, _) => g(),
				cleanup: (_, _) => (long) iterations,
				runs,
				iterations,
				h
			);
		}

		/// <summary>Runs a benchmark</summary>
		public static Report<long> Run(Action<int> test, int runs, int iterations, RobustHistogram? h = null)
		{
			return Run(
				global: test,
				setup: (_) => (long) iterations,
				test: static (g, _, i) =>
				{
					g(i);
					return i;
				},
				cleanup: static (_, s) => s,
				runs,
				iterations,
				h
			);
		}

		/// <summary>Runs a benchmark</summary>
		public static Report<TResult> Run<TState, TResult>(TState state, Action<TState> test, Func<TState, TResult> cleanup, int runs, int iterations, RobustHistogram? h = null)
		{
			return Run(
				global: (state, test, cleanup),
				setup: static (g) => g.state,
				test: static (g, s, i) =>
				{
					g.test(s);
					return i;
				},
				cleanup: static (g, s) => g.cleanup(s),
				runs,
				iterations,
				h
			);
		}

		/// <summary>Runs a benchmark</summary>
		public static Report<TResult> Run<TState, TResult>(TState state, Action<TState, int> test, Func<TState, TResult> cleanup, int runs, int iterations, RobustHistogram? h = null)
		{
			return Run(
				global: (State: state, Test: test, Cleanup: cleanup),
				setup: static (g) => g.State,
				test: static (g, s, i) =>
				{
					g.Test(s, i);
					return s;
				},
				cleanup: static (g, s) => g.Cleanup(s),
				runs,
				iterations,
				h
			);
		}

		/// <summary>Runs a benchmark</summary>
		public static Report<TResult> Run<TState, TResult, TIntermediate>(TState state, Func<TState, int, TIntermediate> test, Func<TState, TResult> cleanup, int runs, int iterations, RobustHistogram? h = null)
		{
			return Run(
				global: (State: state, Test: test, Cleanup: cleanup),
				setup: static (g) => g.State,
				test: static (g, s, i) => g.Test(s, i),
				cleanup: static (g, s) => g.Cleanup(s),
				runs,
				iterations,
				h
			);
		}

		private static TimeSpan GetElapsedTime(long startingTimestamp)
			=> GetElapsedTime(startingTimestamp, Stopwatch.GetTimestamp());

		private static TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp)
#if NET8_0_OR_GREATER
			=> Stopwatch.GetElapsedTime(startingTimestamp, endingTimestamp);
#else
			=> new TimeSpan((long) ((endingTimestamp - startingTimestamp) * s_tickFrequency));

		private static readonly double s_tickFrequency = (double) TimeSpan.TicksPerSecond / Stopwatch.Frequency;
#endif

		/// <summary>Runs a benchmark</summary>
		public static Report<TResult> Run<TGlobal, TState, TResult, TIntermediate>(TGlobal global, Func<TGlobal, TState> setup, Func<TGlobal, TState, int, TIntermediate> test, Func<TGlobal, TState, TResult> cleanup, int runs, int iterations, RobustHistogram? h = null)
		{
			var times = new List<TimeSpan>(runs);
			var results = new List<TResult>(runs);

			// first call to ensure JIT has already done at least Tier 0
			long startTimestamp = Stopwatch.GetTimestamp();
			_ = GetElapsedTime(startTimestamp);
			
			TimeSpan bestRunTotalTime = TimeSpan.MaxValue;

			int totalGc0 = 0;
			int totalGc1 = 0;
			int totalGc2 = 0;

#if NET5_0_OR_GREATER
			long totalAllocatedOnThread = 0;
#endif

			var totalElapsed = TimeSpan.Zero;
			startTimestamp = Stopwatch.GetTimestamp();

			// note: le premier run est toujours ignoré !
			for (int k = -1; k < runs; k++)
			{
				long runStart = Stopwatch.GetTimestamp();
				long ts = runStart;

				var state = setup(global);

				var gcCount0 = GC.CollectionCount(0);
				var gcCount1 = GC.CollectionCount(1);
				var gcCount2 = GC.CollectionCount(2);
#if NET5_0_OR_GREATER
				long allocatedAtStart = GC.GetAllocatedBytesForCurrentThread();
#endif

				var overhead = GetElapsedTime(ts);
				long iterationStart = Stopwatch.GetTimestamp();
				if (h != null)
				{
					for (int i = 0; i < iterations; i++)
					{

						ts = Stopwatch.GetTimestamp();
						test(global, state, i);
						var testElapsed = GetElapsedTime(ts);

						h.Add(testElapsed);
						overhead += GetElapsedTime(ts) - testElapsed;
					}
				}
				else
				{
					for (int i = 0; i < iterations; i++)
					{
						test(global, state, i);
					}
				}
				var iterationElapsed = GetElapsedTime(iterationStart);

#if NET5_0_OR_GREATER
				long allocatedOnThread = GC.GetAllocatedBytesForCurrentThread() - allocatedAtStart;
#endif

				if (k >= 0)
				{
					totalGc0 += GC.CollectionCount(0) - gcCount0;
					totalGc1 += GC.CollectionCount(1) - gcCount1;
					totalGc2 += GC.CollectionCount(2) - gcCount2;
#if NET5_0_OR_GREATER
					totalAllocatedOnThread += allocatedOnThread;
#endif
				}

				var result = cleanup(global, state);
				totalElapsed += iterationElapsed;

				if (k >= 0)
				{
					var t = iterationElapsed - overhead;
					if (t.Ticks < 1) t = new TimeSpan(1);
					times.Add(t);
					results.Add(result);
				}
			}

			var report = new Report<TResult>
			{
				NumberOfRuns = runs,
				IterationsPerRun = iterations,
				Results = results,
				RawTimes = times,
				RawTotal = GetElapsedTime(startTimestamp),
				BestRunTotalTime = bestRunTotalTime,
				Histogram = h,
#if NET5_0_OR_GREATER
				GcAllocatedOnThread = totalAllocatedOnThread,
#endif
				GC0 = totalGc0, //(totalGC0 * 1000000.0) / (runs * iterations),
				GC1 = totalGc1, //(totalGC1 * 1000000.0) / (runs * iterations),
				GC2 = totalGc2, //(totalGC2 * 1000000.0) / (runs * iterations),
			};

			List<int>? outliers = null;
			var filtered = times.Count >= 3 ? PeirceCriterion.FilterOutliers(times, x => x.Ticks, out outliers) : times;
			var outliersMap = outliers?.ToArray() ?? [ ];

			report.Times = filtered;
			report.RejectedRuns = outliersMap.Length;
			report.Runs = times.Select((ticks, i) => new RunData<TResult>
			{
				Index = i,
				Iterations = iterations,
				Duration = ticks,
				Rejected = outliersMap.Contains(i),
				Result = results[i],
			}).ToList();

			report.TotalDuration = TimeSpan.FromTicks(filtered.Sum(x => x.Ticks));
			report.TotalIterations = (long) iterations * filtered.Count;

			// median
			var sorted = filtered.ToArray();
			Array.Sort(sorted);
			report.BestDuration = times.Min();
			report.AverageDuration = TimeSpan.FromTicks((long) sorted.Average(x => (double) x.Ticks));
			var median = Median(sorted);
			report.MedianDuration = median;
			report.StdDevDuration = MeanAbsoluteDeviation(sorted, median);

			report.MedianIterationsPerSecond = report.TotalIterations / report.TotalDuration.TotalSeconds;
			report.MedianIterationsNanos = (double) (report.TotalDuration.Ticks * 100) / report.TotalIterations;

			report.BestIterationsPerSecond = report.IterationsPerRun / report.BestDuration.TotalSeconds;
			report.BestIterationsNanos = (double) (report.BestDuration.Ticks * 100) / report.IterationsPerRun;

			report.StdDevIterationNanos = (double) (report.StdDevDuration.Ticks * 100) / report.TotalIterations;

			return report;
		}

		private static TimeSpan Median(TimeSpan[] sortedData)
		{
			int n = sortedData.Length;
			return n == 0 ? TimeSpan.Zero
				: (n & 1) == 1 ? sortedData[n >> 1]
				: TimeSpan.FromTicks((sortedData[n >> 1].Ticks + sortedData[(n >> 1) - 1].Ticks) >> 1);
		}

		private static TimeSpan MeanAbsoluteDeviation(TimeSpan[] sortedData, TimeSpan med)
		{
			// note: the MAD is computed relative to the Median, and not the arithmetic mean.
			long sum = 0;
			foreach (var data in sortedData)
			{
				var x = (data - med).Ticks;
				sum += x * x;
			}

			var variance = sortedData.Length == 0 ? 0.0 : ((double) sum / sortedData.Length);
			return System.TimeSpan.FromTicks((long) Math.Sqrt(variance));
		}

	}

}
