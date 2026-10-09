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
	using SnowBank.Numerics;

	/// <summary>Per-operation latency distributions for one timed section.</summary>
	/// <remarks>
	/// <para>A median and a min/max spread cannot tell a noisy run from a bimodal one, and the two call for opposite responses: noise is discarded, two populations are a finding. The histograms here keep the shape.</para>
	/// <para>Opt-in with <c>--latency</c>, off by default: timing one operation costs two <see cref="Stopwatch.GetTimestamp"/> calls, and the operations under test run in tens to hundreds of nanoseconds, so the instrument is a measurable part of the thing measured. Off, the headline ns/op stays comparable with runs taken before.</para>
	/// <para>An operation faster than one timer tick (100 ns on a 10 MHz clock) reads as 0 or as one whole tick, so the buckets at or below <see cref="TimerResolutionNanos"/> describe the clock, not the engine. The slow populations, which are what this profile exists to find, sit well above it.</para>
	/// </remarks>
	public sealed class LatencyProfile
	{

		/// <summary>Nanoseconds per timer tick: the floor below which a sample means nothing.</summary>
		public static double TimerResolutionNanos { get; } = 1_000_000_000.0 / Stopwatch.Frequency;

		/// <summary>Nanoseconds above which an operation is recorded as slow for the burst analysis.</summary>
		public const double SlowThresholdNanos = 500;

		public RobustHistogram Set { get; } = new(RobustHistogram.TimeScale.Microseconds);

		public RobustHistogram Clear { get; } = new(RobustHistogram.TimeScale.Microseconds);

		public RobustHistogram ClearRange { get; } = new(RobustHistogram.TimeScale.Microseconds);

		public RobustHistogram Commit { get; } = new(RobustHistogram.TimeScale.Microseconds);

		public RobustHistogram Get { get; } = new(RobustHistogram.TimeScale.Microseconds);

		public RobustHistogram GetRange { get; } = new(RobustHistogram.TimeScale.Microseconds);

		public RobustHistogram? For(KvOp op) => op switch
		{
			KvOp.Set => this.Set,
			KvOp.Clear => this.Clear,
			KvOp.ClearRange => this.ClearRange,
			KvOp.Commit => this.Commit,
			KvOp.Get => this.Get,
			KvOp.GetRange => this.GetRange,
			_ => null,
		};

		/// <summary>Indexes of the operations slower than <see cref="SlowThresholdNanos"/>, in order.</summary>
		/// <remarks>
		/// <para>The position is the evidence: a fixed per-operation tax scatters uniformly, a state-dependent cost arrives in bursts, and the period between bursts names the state.</para>
		/// <para>Pre-sized, so the list never reallocates into the large object heap during the timed section, where the resulting collections would stall the operations being measured.</para>
		/// </remarks>
		public List<int> SlowIndexes { get; } = new(1_000_000);

		public void Merge(LatencyProfile other)
		{
			this.Set.Merge(other.Set);
			this.Clear.Merge(other.Clear);
			this.ClearRange.Merge(other.ClearRange);
			this.Commit.Merge(other.Commit);
			this.Get.Merge(other.Get);
			this.GetRange.Merge(other.GetRange);
			// positions come from one run, never concatenated: the last one says whether warm-up had finished
			this.SlowIndexes.Clear();
			this.SlowIndexes.AddRange(other.SlowIndexes);
		}

		/// <summary>Groups the slow operations into bursts and describes the shape.</summary>
		/// <param name="totalOps">Operations in the timed section.</param>
		/// <param name="joinGap">Slow operations closer together than this belong to the same burst.</param>
		public string DescribeClustering(int totalOps, int joinGap = 8)
		{
			var idx = this.SlowIndexes;
			if (idx.Count == 0) return "no operation exceeded the threshold";

			var sizes = new List<int>();
			var gaps = new List<int>();
			int start = idx[0], prev = idx[0], size = 1;
			int biggest = 0, biggestStart = idx[0], biggestEnd = idx[0];
			for (int i = 1; i < idx.Count; i++)
			{
				if (idx[i] - prev <= joinGap) { size++; }
				else
				{
					if (size > biggest) { biggest = size; biggestStart = start; biggestEnd = prev; }
					sizes.Add(size); gaps.Add(idx[i] - start); start = idx[i]; size = 1;
				}
				prev = idx[i];
			}
			if (size > biggest) { biggest = size; biggestStart = start; biggestEnd = prev; }
			sizes.Add(size);

			sizes.Sort();
			gaps.Sort();
			double pct = 100.0 * idx.Count / Math.Max(1, totalOps);
			// density per tenth of the run: a step says the engine entered another mode, a decay says warm-up
			var deciles = new int[10];
			foreach (var i in idx)
			{
				int d = totalOps <= 0 ? 0 : Math.Min(9, (int) ((long) i * 10 / totalOps));
				deciles[d]++;
			}
			int per = Math.Max(1, totalOps / 10);
			string profile = string.Join(" ", deciles.Select(c => $"{100.0 * c / per,3:N0}%"));

			return $"{idx.Count:N0} slow ops ({pct:N2}%) in {sizes.Count:N0} bursts; burst size min/med/max = {sizes[0]}/{sizes[sizes.Count / 2]}/{sizes[^1]}; first={idx[0]:N0} last={idx[^1]:N0}; biggest burst = {biggest:N0} ops spanning [{biggestStart:N0}..{biggestEnd:N0}]; density by decile: {profile}";
		}

		public IEnumerable<(string Name, RobustHistogram Histogram)> All()
		{
			yield return ("set", this.Set);
			yield return ("clear", this.Clear);
			yield return ("clearrange", this.ClearRange);
			yield return ("commit", this.Commit);
			yield return ("get", this.Get);
			yield return ("getrange", this.GetRange);
		}

	}

}
