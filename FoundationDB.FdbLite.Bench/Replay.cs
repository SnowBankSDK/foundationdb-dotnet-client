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

	/// <summary>What one timed (engine, workload) pass produced.</summary>
	public sealed record RunResult(double NsPerOp, double BytesPerOp, long FileSize, Dictionary<string, long> Counters, LatencyProfile? Latency);

	/// <summary>The replay loop shared by every mode.</summary>
	public static class Replay
	{

		private static readonly double ToNanos = 1_000_000_000.0 / Stopwatch.Frequency;

		/// <summary>Replays a step array. Returns the elapsed wall time; reads are counted into <paramref name="checksum"/>, never compared here.</summary>
		/// <remarks>Reads never interleave with an open batch: a read inside a batch would measure read-your-writes, which the raw engine does not have, so an open batch is committed before any read.</remarks>
		public static TimeSpan Run(IKvDriver driver, KvStep[] steps, out long checksum, LatencyProfile? profile = null)
		{
			long sum = 0;
			bool open = false;
			int opIndex = 0;
			var sw = Stopwatch.StartNew();
			foreach (var step in steps)
			{
				// one timestamp before the operation and one after, hoisted out of the switch so every arm is instrumented alike
				var h = profile?.For(step.Op);
				long t0 = h is not null ? Stopwatch.GetTimestamp() : 0;
				switch (step.Op)
				{
					case KvOp.Set:
						if (!open) { driver.BeginBatch(); open = true; }
						driver.Set(step.Key, step.Value);
						break;
					case KvOp.Clear:
						if (!open) { driver.BeginBatch(); open = true; }
						driver.Clear(step.Key);
						break;
					case KvOp.ClearRange:
						if (!open) { driver.BeginBatch(); open = true; }
						driver.ClearRange(step.Key, step.EndKey);
						break;
					case KvOp.Commit:
						if (open) { driver.Commit(); open = false; }
						break;
					case KvOp.Get:
						if (open) { driver.Commit(); open = false; }
						sum += driver.Probe(step.Key);
						break;
					case KvOp.GetRange:
						if (open) { driver.Commit(); open = false; }
						sum += driver.ScanCount(step.Key, step.EndKey, step.Limit, step.Reverse, step.ReadValues);
						break;
				}
				if (h is not null)
				{
					double ns = (Stopwatch.GetTimestamp() - t0) * ToNanos;
					h.AddNanos(ns);
					// where the slow ones happened, not only how many: the pattern is the diagnosis
					if (ns >= LatencyProfile.SlowThresholdNanos) profile!.SlowIndexes.Add(opIndex);
				}
				++opIndex;
			}
			if (open) driver.Commit();
			sw.Stop();
			checksum = sum;
			return sw.Elapsed;
		}

		/// <summary>Times one pass of <paramref name="load"/>.Steps over an already prepared store.</summary>
		public static RunResult Timed(IKvDriver driver, Workload load, bool latency)
		{
			var profile = latency ? new LatencyProfile() : null;
			driver.OnTimedSectionStarting(load);
			// managed allocation over the timed section: the replay is single-threaded, so the per-thread counter is
			// exact and, unlike wall time, deterministic
			long allocBefore = GC.GetAllocatedBytesForCurrentThread();
			var elapsed = Run(driver, load.Steps, out _, profile);
			long allocated = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
			double nsPerOp = load.Ops == 0 ? 0 : elapsed.TotalMilliseconds * 1_000_000.0 / load.Ops;
			double bytesPerOp = load.Ops == 0 ? 0 : (double) allocated / load.Ops;
			var counters = new Dictionary<string, long>();
			foreach (var (k, v) in driver.Stats()) counters[k] = v;
			return new(nsPerOp, bytesPerOp, driver.FileSize, counters, profile);
		}

		/// <summary>Opens a fresh store, prepares it untimed, times one pass, and drops the store.</summary>
		public static RunResult Once(BenchOptions options, string engine, Workload load)
		{
			using var d = options.Open(engine, load.Name);
			Run(d, load.Prepare, out _);
			return Timed(d, load, options.Latency);
		}

	}

}
