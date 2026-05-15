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
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text;
	using SnowBank.Runtime;

	/// <summary>Helper that will aggregate individual measurements, and output detailed distribution reports and charts.</summary>
	[PublicAPI]
	[DebuggerTypeProxy(typeof(DebugView))]
	public sealed class RobustHistogram
	{

		/// <summary>Specifies the type of the measurement</summary>
		public enum DimensionType
		{
			/// <summary>Use the type that corresponds to the context</summary>
			Unspecified = 0,

			/// <summary>Duration, or Amount of Time</summary>
			Time = 1,

			/// <summary>Size (in bytes, powers of 10, 1GB = 10^9 bytes)</summary>
			Size = 2,

			/// <summary>Ratio (0..1)</summary>
			Ratio = 3,
		}

		/// <summary>Specifies the scale and/or type of units for the measurement</summary>
		public enum TimeScale
		{
			// TIME
			/// <summary>Duration, measured in BCL ticks (<see cref="TimeSpan.Ticks"/>)</summary>
			Ticks,
			/// <summary>Duration, measured in nanoseconds (10E-9 seconds)</summary>
			Nanoseconds,
			/// <summary>Duration, measured in microseconds (10E-6 seconds)</summary>
			Microseconds,
			/// <summary>Duration, measured in milliseconds (10E-3 seconds)</summary>
			Milliseconds,
			/// <summary>Duration, measured in seconds</summary>
			Seconds,
			// SIZE
			/// <summary>Size, measured in bytes</summary>
			Bytes,
			/// <summary>Size, measured in KibiBytes (1_024 bytes)</summary>
			KiloBytes,
			/// <summary>Size, measured in MebiBytes (1_048_576 bytes)</summary>
			MegaBytes,
			/// <summary>Size, measured in GibiBytes (1_073_741_824 bytes)</summary>
			GigaBytes,
			// RATIO
			/// <summary>Ratio between two values, scalar with no unit, ...</summary>
			Ratio,
		}

		private sealed class DebugView
		{
			public DebugView(RobustHistogram h)
			{
				var buckets = h.Buckets;

				var samples = new List<DebugViewItem>();
				var limits = BucketLimits;
				for (int i = 0; i < buckets.Length; i++)
				{
					if (buckets[i] == 0) continue;
					samples.Add(new(i == 0 ? 0 : limits[i - 1], limits[i], buckets[i]));
				}

				this.Count = h.Count;
				this.Samples = samples;
				this.Min = h.Min;
				this.Median = h.Median;
				this.Max = h.Max;
			}

			public int Count;
			public double Min;
			public double Median;
			public double Max;

			[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
			public List<DebugViewItem> Samples;

		}

		[DebuggerDisplay("{Count}", Name = "{From}\u2026{To}")]
		private readonly struct DebugViewItem
		{
			public DebugViewItem(double from, double to, int count)
			{
				this.From = from;
				this.To = to;
				this.Count = count;
			}

			// ReSharper disable once NotAccessedField.Local
			public readonly double From;

			// ReSharper disable once NotAccessedField.Local
			public readonly double To;

			public readonly int Count;
		}


		/// <summary>First row of the horizontal scale (includes the number literals)</summary>
		public const string HorizontalScale = "0.001      .    0.01       .    0.1        .    1          .    10         .    100        .    1k         .    10k        .    100k            1M         .    10M        .    100M       .    1G         .    10G        .    100G       .    1T         .    10T        .    100T       .    ";

		/// <summary>Second row of the horizontal scale (includes the ticks and markers)</summary>
		public const string HorizontalShade = "|---------------|===============|¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤|---------------|===============|¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤|---------------|===============|¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤|---------------|===============|¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤|---------------|===============|¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤|---------------|===============|¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤";

		private const int NumDecades = 13;

		private const int NumBuckets = NumDecades * 16 + 1;

		/// <summary>Minimum value that can be measured (<c>0</c>)</summary>
		public const double MinValue = 0;

		/// <summary>Maximum value that can be measured (<c>10E200</c>)</summary>
		public const double MaxValue = 1e200;

		private static ReadOnlySpan<double> BucketLimits =>
		[
			/*   [0] */ 0.001, 0.0012, 0.0014, 0.0016, 0.0018, 0.002, 0.0025, 0.003, 0.0035, 0.0040, 0.0045, 0.005, 0.006, 0.007, 0.008, 0.009,
			/*  [16] */ 0.01, 0.012, 0.014, 0.016, 0.018, 0.02, 0.025, 0.03, 0.035, 0.040, 0.045, 0.05, 0.06, 0.07, 0.08, 0.09,
			/*  [32] */ 0.10, 0.12, 0.14, 0.16, 0.18, 0.20, 0.25, 0.30, 0.35, 0.40, 0.45, 0.50, 0.60, 0.70, 0.80, 0.90,
			/*  [48] */ 1, 1.2, 1.4, 1.6, 1.8, 2, 2.5, 3, 3.5, 4, 4.5, 5, 6, 7, 8, 9,
			/*  [64] */ 10, 12, 14, 16, 18, 20, 25, 30, 35, 40, 45, 50, 60, 70, 80, 90,
			/*  [80] */ 100, 120, 140, 160, 180, 200, 250, 300, 350, 400, 450, 500, 600, 700, 800, 900,
			/*  [96] */ 1_000, 1_200, 1_400, 1_600, 1_800, 2_000, 2_500, 3_000, 3_500, 4_000, 4_500, 5_000, 6_000, 7_000, 8_000, 9_000,
			/* [112] */ 10_000, 12_000, 14_000, 16_000, 18_000, 20_000, 25_000, 30_000, 35_000, 40_000, 45_000, 50_000, 60_000, 70_000, 80_000, 90_000,
			/* [128] */ 100_000, 120_000, 140_000, 160_000, 180_000, 200_000, 250_000, 300_000, 350_000, 400_000, 450_000, 500_000, 600_000, 700_000, 800_000, 900_000,
			/* [144] */ 1_000_000, 1_200_000, 1_400_000, 1_600_000, 1_800_000, 2_000_000, 2_500_000, 3_000_000, 3_500_000, 4_000_000, 4_500_000, 5_000_000, 6_000_000, 7_000_000, 8_000_000, 9_000_000,
			/* [160] */ 10_000_000, 12_000_000, 14_000_000, 16_000_000, 18_000_000, 20_000_000, 25_000_000, 30_000_000, 35_000_000, 40_000_000, 45_000_000, 50_000_000, 60_000_000, 70_000_000, 80_000_000, 90_000_000,
			/* [176] */ 100_000_000, 120_000_000, 140_000_000, 160_000_000, 180_000_000, 200_000_000, 250_000_000, 300_000_000, 350_000_000, 400_000_000, 450_000_000, 500_000_000, 600_000_000, 700_000_000, 800_000_000, 900_000_000,
			/* [192] */ 1_000_000_000, 1_200_000_000, 1_400_000_000, 1_600_000_000, 1_800_000_000, 2_000_000_000, 2_500_000_000, 3_000_000_000, 3_500_000_000, 4_000_000_000, 4_500_000_000, 5_000_000_000, 6_000_000_000, 7_000_000_000, 8_000_000_000, 9_000_000_000,
			/*  */ 1e200
		];

		private const int INDEX_ONE = 48;
		private const int INDEX_HUNDRED = 80;
		private const int INDEX_TEN_THOUSAND = 112;
		private const int INDEX_MILLION = 144;
		private const int INDEX_BILLION = 192;
		private static readonly double SUM_RATIO = (1d / BucketLimits[0]);
		private static readonly double SUM_SQUARES_RATIO = SUM_RATIO * SUM_RATIO;

		private static readonly string BarTicksChars = "<[[((|))]]>";
		private static readonly string BarChartChars = " .:;+=xX$&#";
		private static readonly string VerticalChartChars = " _.-:=xX&#@";

		/// <summary>Constructs a new <see cref="RobustHistogram"/> for measuring durations with microsecond precision.</summary>
		public RobustHistogram()
			: this(TimeScale.Microseconds)
		{ }

		/// <summary>Constructs a new <see cref="RobustHistogram"/> with the given <see cref="TimeScale"/></summary>
		/// <param name="scale"></param>
		public RobustHistogram(TimeScale scale)
		{
			this.Min = MaxValue;
			this.Max = MinValue;
			this.Buckets = new int[NumBuckets];
			this.Scale = scale;
			switch (scale)
			{
				case TimeScale.Ticks:
				case TimeScale.Nanoseconds:
				case TimeScale.Microseconds:
				case TimeScale.Milliseconds:
				case TimeScale.Seconds:
				{
					this.Dimension = DimensionType.Time;
					break;
				}

				case TimeScale.Bytes:
				case TimeScale.KiloBytes:
				case TimeScale.MegaBytes:
				case TimeScale.GigaBytes:
				{
					this.Dimension = DimensionType.Size;
					break;
				}
				case TimeScale.Ratio:
				{
					this.Dimension = DimensionType.Ratio;
					break;
				}
			}
			this.TicksToUnit = GetScaleToTicksRatio(scale);
		}

		/// <summary>Pre-JIT this type, before running a benchmark</summary>
		public static void Warmup()
		{
			PlatformHelpers.PreJit(typeof(RobustHistogram));
		}

		[Pure]
		private static double GetScaleToTicksRatio(TimeScale scale)
		{
			switch(scale)
			{
				case TimeScale.Nanoseconds: return 100; // 100 ns / tick
				case TimeScale.Ticks: return 1;
				case TimeScale.Microseconds: return 1.0 / 10; // 10 ticks / µsec
				case TimeScale.Milliseconds: return 1.0 / 10_000; // 10 K ticks / ms
				case TimeScale.Seconds: return 1.0 / 10_000_000; // 10 M ticks / s

				case TimeScale.Ratio: return 1;
				default: return 1;
			}
		}

		[Pure]
		private static double GetScaleFactor(TimeScale scale)
		{
			switch (scale)
			{
				// time is normalized into seconds
				case TimeScale.Nanoseconds: return 1E-9;
				case TimeScale.Ticks: return 1.0 / TimeSpan.TicksPerSecond;
				case TimeScale.Microseconds: return 1E-6;
				case TimeScale.Milliseconds: return 1E-3;
				case TimeScale.Seconds: return 1.0;

				// size is normalized into bytes
				case TimeScale.Bytes: return 1.0;
				case TimeScale.KiloBytes: return 1_024;
				case TimeScale.MegaBytes: return 1_024 * 1_024;
				case TimeScale.GigaBytes: return 1_024 * 1_024 * 1_024;

				// ratio is normalized into 100%
				case TimeScale.Ratio: return 100.0;
				default: return 1.0d;
			}
		}

		[Pure]
		private static string GetScaleUnit(TimeScale scale)
		{
			switch(scale)
			{
				case TimeScale.Ticks: return "t";
				case TimeScale.Nanoseconds: return "ns";
				case TimeScale.Microseconds: return "µs";
				case TimeScale.Milliseconds: return "ms";
				case TimeScale.Seconds: return "s";
				case TimeScale.Bytes: return "b";
				case TimeScale.KiloBytes: return "KiB";
				case TimeScale.MegaBytes: return "MiB";
				case TimeScale.GigaBytes: return "GiB";
				case TimeScale.Ratio: return "%";
				default: return "";
			}
		}

		[Pure]
		private string Friendly(double value, double xs)
		{
			//notes:
			// - the methods tries to always return a string of 8 or less (though it is possible to return more for extreme values)
			// - time is normalized into "ticks" which are 1/10,000,000th of a second
			// - size is normalized into "bytes"
			

			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if (value == 0.0) return "0";

			switch (this.Dimension)
			{
				case DimensionType.Time:
				{
					value *= xs;
					return value switch
					{
						< 0.000001 => string.CreateInvariant($"{value * 1E9:N1} ns"), // 100.0 - 999.9 ns
						< 0.001    => string.CreateInvariant($"{value * 1_000_000:N1} µs"), // 100.0 - 999.9 µs
						< 1        => string.CreateInvariant($"{value * 1_000:N1} ms"), // 100.0 - 999.9 ms
						< 10       => string.CreateInvariant($"{value:N2} sec"), // 1.00 - 9.99 sec
						< 60       => string.CreateInvariant($"{value:N1} sec"), // 10.0 - 59.9 sec
						_          => string.CreateInvariant($"{value:N0} sec") // 60 sec - 9999 sec
					};
				}
				case DimensionType.Size:
				{
					value *= xs;
					return value switch
					{
						< 1_000         => string.Format(CultureInfo.InvariantCulture, "{0:N0} B", value), // 0 B - 999 B
						< 1_000_000     => string.Format(CultureInfo.InvariantCulture, "{0:N1} KB", value / 1_000), // 1.0 KB - 999.9 KB
						< 1_000_000_000 => string.Format(CultureInfo.InvariantCulture, "{0:N1} MB", value / 1_000_000), // 1.0 MB - 999.9 MB
						_               => string.Format(CultureInfo.InvariantCulture, "{0:N1} GB", value / 1_000_000_000), // 1.0 GB - 999.9 GB
					};
				}
				case DimensionType.Ratio:
				{
					// normalized into %
					value = value * xs;
					return string.Format(CultureInfo.InvariantCulture, "{0:N1} %", value); // 0.0 % - 100.0 %
				}
			}

			// "unspecified"

			if (value < 1)
			{
				if (value < 0.000001)
				{
					return string.Format(CultureInfo.InvariantCulture, "{0:N1}n", value * 1_000_000_000);
				}
				if (value < 0.001)
				{
					return string.Format(CultureInfo.InvariantCulture, "{0:N1}µ", value * 1_000_000);
				}
				if (value < 0.01)
				{
					return string.Format(CultureInfo.InvariantCulture, "{0:N3}", value);
				}
				if (value < 0.1)
				{
					return string.Format(CultureInfo.InvariantCulture, "{0:N2}", value);
				}
				return string.Format(CultureInfo.InvariantCulture, "{0:N1}", value);
			}
			if (value <= 10)
			{
				return string.Format(CultureInfo.InvariantCulture, "{0:N1}", value);
			}
			if (value < 1_000)
			{
				return string.Format(CultureInfo.InvariantCulture, "{0:N0}", value);
			}
			if (value < 1_000_000)
			{
				return string.Format(CultureInfo.InvariantCulture, "{0:N1} K", value / 1_000);
			}
			return string.Format(CultureInfo.InvariantCulture, "{0:N1} M", value / 1_000_000);
		}

		[Pure]
		private TimeSpan ToTimeSpan(double value)
		{
			return TimeSpan.FromTicks((long)(value / this.TicksToUnit));
		}

		/// <summary>Clear all samples from this histogram, so that it can be reused for another measurement run</summary>
		public void Clear()
		{
			this.Min = MaxValue;
			this.Max = MinValue;
			this.Count = 0;
			this.InternalSum = 0;
			this.InternalSumSquares = 0;
			this.Buckets.AsSpan().Clear();
		}

		/// <summary>Merge the measurements of another histogram into this instance</summary>
		/// <param name="other">Histogram that contains measurements from another run</param>
		public void Merge(RobustHistogram other)
		{
			if (other.Min < this.Min) this.Min = other.Min;
			if (other.Max > this.Max) this.Max = other.Max;
			this.Count += other.Count;
			this.InternalSum += other.InternalSum;
			this.InternalSumSquares += other.InternalSumSquares;
			var buckets = this.Buckets.AsSpan(0, NumBuckets);
			var otherBuckets = other.Buckets.AsSpan(0, NumBuckets);
			for (int i = 0; i < buckets.Length; i++)
			{
				buckets[i] += otherBuckets[i];
			}
		}

		private static int GetBucketIndex(double value)
		{
			// We want to find, for each value 'x', the bucket 'B' that verifies: BucketLimits[B - 1] <= x < BucketLimits[B]
			// Note: If value > MaxValue, we assume that we are still in the last bucket, which may break some measurements

			// This method is performance sensitive, because it will usually be called by benchmarks or when profiling the hot path of an algorithm.
			// => we will first compute in which "quadrant" the value is located, and then finish by performing a binary search
			// => we assume that the most common values will be small numbers, and that large numbers are uncommon.

			int begin, end;
			if (value < 1)
			{ // 0 <= x < 1
				begin = 0;
				end = INDEX_ONE;
			}
			else if (value < 100)
			{ // 1 <= x < 100
				begin = INDEX_ONE;
				end = INDEX_HUNDRED;
			}
			else if (value < 10_000)
			{ // 100 <= x < 1K
				begin = INDEX_HUNDRED;
				end = INDEX_TEN_THOUSAND;
			}
			else if (value < 1_000_000)
			{ // 1K <= x < 1M
				begin = INDEX_TEN_THOUSAND;
				end = INDEX_MILLION;
			}
			else if (value < 1_000_000_000)
			{ // 1M <= x < 1G
				begin = INDEX_MILLION;
				end = INDEX_BILLION;
			}
			else if (value < MaxValue)
			{ // 1G <= x < MAX
				begin = INDEX_BILLION;
				end = NumBuckets;
			}
			else
			{ // over max value
				return NumBuckets - 1;
			}

			// found the bucket in this range
			int p = BucketLimits.Slice(begin, end - begin).BinarySearch(value);

			return begin + (p < 0 ? (~p) : p + 1);
		}

		private static unsafe int BinarySearch(double[] array, int index, int length, double value)
		{
			int lo = index;
			int hi = index + length - 1;
			fixed (double* ptr = &array[0])
			{
				while (lo <= hi)
				{
					int idx = lo + (hi - lo >> 1);
					double x = ptr[idx];
					// ReSharper disable once CompareOfFloatsByEqualityOperator
					if (value == x) return idx;
					if (value > x)
					{
						lo = idx + 1;
					}
					else
					{
						hi = idx - 1;
					}
				}

				return ~lo;
			}
		}

#if FULL_DEBUG
		public static void ValidateGetBucketIndex()
		{
			// Pseudo-test that ensures that GetBucketIndex(...) is behaving as expected

			Action<double> getBucket = (x) =>
			{
				int idx;
				idx = GetBucketIndex(x);
				Console.WriteLine($"{x,10:R}: idx={idx,3} : {(idx == 0 ? 0 : BucketLimits[idx - 1]),10:R} <= {x,10:R} < {BucketLimits[idx],10:R}");

			};

			getBucket(0);
			getBucket(0.001);
			getBucket(0.01);
			getBucket(0.1);
			getBucket(1);
			getBucket(10);
			getBucket(11);
			getBucket(12);
			getBucket(12.01);
			getBucket(50);
			getBucket(100);
			getBucket(500);
			getBucket(1000);
			getBucket(5000);
			getBucket(10000);
			getBucket(50000);
			getBucket(100000);
			getBucket(500000);
			getBucket(1000000);
			getBucket(5000000);
			getBucket(1e200);
			getBucket(1e201);
		}
#endif

		/// <summary>Add a new measurement</summary>
		/// <remarks>The unit if the value should match the unit that was specified when the histogram was created</remarks>
		public void Add(double value)
		{
			++this.Buckets[GetBucketIndex(value)];
			++this.Count;
			this.InternalSum += (value * SUM_RATIO);
			this.InternalSumSquares += (value * value * SUM_SQUARES_RATIO);
			if (this.Min > value) this.Min = value;
			if (this.Max < value) this.Max = value;
		}

		/// <summary>Add a new duration measurement</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(TimeSpan value)
		{
			Add(value.Ticks * this.TicksToUnit);
		}

		/// <summary>Add a new duration measurement, expressed in ticks (100 ns per tick)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AddTicks(long ticks)
		{
			Add(ticks * this.TicksToUnit);
		}

		/// <summary>Add a new duration measurement, expressed in nanoseconds</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AddNanos(double nanos)
		{
			Add(nanos * (this.TicksToUnit / 100.0)); // 100 nanos per tick
		}

		/// <summary>Timescale used by this histogram</summary>
		public TimeScale Scale { get; }

		/// <summary>Dimension of values measured by this histogram</summary>
		public DimensionType Dimension { get; }

		/// <summary>Conversion factor from ticks (100ns) to the timescale unit used by this histogram</summary>
		private double TicksToUnit { get; }

		/// <summary>Number of samples that where measured so far: Ts.Count()</summary>
		public int Count { get; private set; }

		/// <summary>Lowest sample measured so far: Ts.Min()</summary>
		public double Min { get; private set; }

		/// <summary>Highest sample measured so far: Ts.Max()</summary>
		public double Max { get; private set; }

		/// <summary>Sum of all samples measured so far: Ts.Sum(t => t)</summary>
		public double Sum
		{
			[Pure]
			get => this.InternalSum / SUM_RATIO;
		}

		/// <summary>Sum of the squares of all samples measured so far: Ts.Sum(t => t * t)</summary>
		public double SumSquares
		{
			[Pure]
			get => this.InternalSumSquares / SUM_SQUARES_RATIO;
		}

		/// <summary>Raw sum of all samples</summary>
		private double InternalSum;// { get; set; }

		/// <summary>Raw sum of the squares of all samples</summary>
		private double InternalSumSquares;// { get; set; }

		/// <summary>Array that contains the number of samples that where measured for each bucket</summary>
		/// <remarks>The entry <c>Buckets[B]</c> contains the number of samples whose value <c>x</c> is bounded by <c>Buckets[B - 1] &lt;= x &lt; Buckets[B]</c></remarks>
		private int[] Buckets { get; set; }

		/// <summary>Compute the median of all measured samples (50% percentile)</summary>
		public double Median
		{
			[Pure]
			get => Percentile(50);
		}

		#region Unit Conversions...

		#region Time...

		/// <summary>Convert a measured value into seconds (s)</summary>
		/// <param name="value">Value to convert (any of <see cref="Median"/>, <see cref="Max"/>, ...)</param>
		/// <returns><paramref name="value"/> converted into seconds</returns>
		/// <remarks>This only works if the histogram is measuring elapsed time.</remarks>
		public double ToSeconds(double value) => value * GetScaleFactor(this.Scale);

		/// <summary>Convert a measured value into seconds with a given precision</summary>
		/// <param name="value">Value to convert (any of <see cref="Median"/>, <see cref="Max"/>, ...)</param>
		/// <param name="precision">Number of digits for rounding</param>
		/// <returns><paramref name="value"/> converted into seconds and rounded to the specified <paramref name="precision"/></returns>
		/// <remarks>This only works if the histogram is measuring elapsed time.</remarks>
		public double ToSeconds(double value, int precision) => Math.Round(value * GetScaleFactor(this.Scale), precision, MidpointRounding.AwayFromZero);

		/// <summary>Convert a measured value into milliseconds (ms)</summary>
		/// <param name="value">Value to convert (any of <see cref="Median"/>, <see cref="Max"/>, ...)</param>
		/// <returns><paramref name="value"/> converted into milliseconds</returns>
		/// <remarks>This only works if the histogram is measuring elapsed time.</remarks>
		public double ToMilliseconds(double value) => value * GetScaleFactor(this.Scale) * 1E3;

		/// <summary>Convert a measured value into milliseconds (ms) with a given precision</summary>
		/// <param name="value">Value to convert (any of <see cref="Median"/>, <see cref="Max"/>, ...)</param>
		/// <param name="precision">Number of digits for rounding</param>
		/// <returns><paramref name="value"/> converted into milliseconds and rounded to the specified <paramref name="precision"/></returns>
		/// <remarks>This only works if the histogram is measuring elapsed time.</remarks>
		public double ToMilliseconds(double value, int precision) => Math.Round(value * GetScaleFactor(this.Scale) * 1E3, precision, MidpointRounding.AwayFromZero);

		/// <summary>Convert a measured value into microseconds (µs)</summary>
		/// <param name="value">Value to convert (any of <see cref="Median"/>, <see cref="Max"/>, ...)</param>
		/// <returns><paramref name="value"/> converted into microseconds</returns>
		/// <remarks>This only works if the histogram is measuring elapsed time.</remarks>
		public double ToMicroseconds(double value) => value * GetScaleFactor(this.Scale) * 1E6;

		/// <summary>Convert a measured value into microseconds (µs) with a given precision</summary>
		/// <param name="value">Value to convert (any of <see cref="Median"/>, <see cref="Max"/>, ...)</param>
		/// <param name="precision">Number of digits for rounding</param>
		/// <returns><paramref name="value"/> converted into microseconds and rounded to the specified <paramref name="precision"/></returns>
		/// <remarks>This only works if the histogram is measuring elapsed time.</remarks>
		public double ToMicroseconds(double value, int precision) => Math.Round(value * GetScaleFactor(this.Scale) * 1E6, precision, MidpointRounding.AwayFromZero);

		/// <summary>Convert a measured value into nanoseconds (ns)</summary>
		/// <param name="value">Value to convert (any of <see cref="Median"/>, <see cref="Max"/>, ...)</param>
		/// <returns><paramref name="value"/> converted into nanoseconds</returns>
		/// <remarks>This only works if the histogram is measuring elapsed time.</remarks>
		public double ToNanoseconds(double value) => value * GetScaleFactor(this.Scale) * 1E9;

		/// <summary>Convert a measured value into nanoseconds (ns) with a given precision</summary>
		/// <param name="value">Value to convert (any of <see cref="Median"/>, <see cref="Max"/>, ...)</param>
		/// <param name="precision">Number of digits for rounding</param>
		/// <returns><paramref name="value"/> converted into nanoseconds and rounded to the specified <paramref name="precision"/></returns>
		/// <remarks>This only works if the histogram is measuring elapsed time.</remarks>
		public double ToNanoseconds(double value, int precision) => Math.Round(value * GetScaleFactor(this.Scale) * 1E9, precision, MidpointRounding.AwayFromZero);

		/// <summary>Convert a number of nanoseconds (ns) into the equivalent measured value</summary>
		/// <param name="value">Number of seconds to convert</param>
		/// <returns><paramref name="value"/> converted from nanoseconds in the corresponding mesaured value</returns>
		/// <remarks>This only works if the histogram is measuring elapsed time.</remarks>
		public double FromNanoseconds(double value) => value / (GetScaleFactor(this.Scale) * 1E9);

		#endregion

		#region Bytes...

		/// <summary>Convert a measured value into bytes</summary>
		/// <param name="value">Value to convert (any of <see cref="Median"/>, <see cref="Max"/>, ...)</param>
		/// <returns><paramref name="value"/> converted into bytes</returns>
		/// <remarks>This only works if the histogram is measuring bytes.</remarks>
		public double ToBytes(double value) => value * GetScaleFactor(this.Scale);

		/// <summary>Convert a measured value into bytes with a given precision</summary>
		/// <param name="value">Value to convert (any of <see cref="Median"/>, <see cref="Max"/>, ...)</param>
		/// <param name="precision">Number of digits for rounding</param>
		/// <returns><paramref name="value"/> converted into bytes and rounded to the specified <paramref name="precision"/></returns>
		/// <remarks>This only works if the histogram is measuring bytes.</remarks>
		public double ToBytes(double value, int precision) => Math.Round(value * GetScaleFactor(this.Scale), precision, MidpointRounding.AwayFromZero);

		/// <summary>Convert a measured value into kilobytes (10^3 or 1,000 bytes)</summary>
		/// <param name="value">Value to convert (any of <see cref="Median"/>, <see cref="Max"/>, ...)</param>
		/// <returns><paramref name="value"/> converted into kilobytes</returns>
		/// <remarks>If you want kibibytes (1,024 bytes), please call <see cref="ToKibiBytes(double)"/> instead.</remarks>
		/// <remarks>This only works if the histogram is measuring bytes.</remarks>
		public double ToKiloBytes(double value) => value * GetScaleFactor(this.Scale) * 1E-3;

		/// <summary>Convert a measured value into kilobytes (10^3 or 1,000 bytes) with a given precision</summary>
		/// <param name="value">Value to convert (any of <see cref="Median"/>, <see cref="Max"/>, ...)</param>
		/// <param name="precision">Number of digits for rounding</param>
		/// <returns><paramref name="value"/> converted into kilobytes and rounded to the specified <paramref name="precision"/></returns>
		/// <remarks>If you want kibibytes (1,024 bytes), please call <see cref="ToKibiBytes(double, int)"/> instead.</remarks>
		/// <remarks>This only works if the histogram is measuring bytes. Other scales will return an incorrect value.</remarks>
		public double ToKiloBytes(double value, int precision) => Math.Round(value * GetScaleFactor(this.Scale) * 1E-3, precision, MidpointRounding.AwayFromZero);

		/// <summary>Convert a measured value into kibibytes (2^10 or 1024 bytes)</summary>
		/// <param name="value">Value to convert (any of <see cref="Median"/>, <see cref="Max"/>, ...)</param>
		/// <returns><paramref name="value"/> converted into kibibytes</returns>
		/// <remarks>If you want kilobytes (1,000 bytes), please call <see cref="ToKiloBytes(double)"/> instead.</remarks>
		/// <remarks>This only works if the histogram is measuring bytes.</remarks>
		public double ToKibiBytes(double value) => value * GetScaleFactor(this.Scale) / 1_024;

		/// <summary>Convert a measured value into kibibytes (2^10 or 1024 bytes) with a given precision</summary>
		/// <param name="value">Value to convert (any of <see cref="Median"/>, <see cref="Max"/>, ...)</param>
		/// <param name="precision">Number of digits for rounding</param>
		/// <returns><paramref name="value"/> converted into kibibytes and rounded to the specified <paramref name="precision"/></returns>
		/// <remarks>If you want kilobytes (1,000 bytes), please call <see cref="ToKiloBytes(double, int)"/> instead.</remarks>
		/// <remarks>This only works if the histogram is measuring bytes. Other scales will return an incorrect value.</remarks>
		public double ToKibiBytes(double value, int precision) => Math.Round(value * GetScaleFactor(this.Scale) / 1_024, precision, MidpointRounding.AwayFromZero);

		/// <summary>Convert a measured value into megabytes (10^6 or 1,000,000 bytes)</summary>
		/// <param name="value">Value to convert (any of <see cref="Median"/>, <see cref="Max"/>, ...)</param>
		/// <returns><paramref name="value"/> converted into megabytes</returns>
		/// <remarks>If you want mebibytes (2^20 bytes), please call <see cref="ToKibiBytes(double)"/> instead.</remarks>
		/// <remarks>This only works if the histogram is measuring bytes.</remarks>
		public double ToMegaBytes(double value) => value * GetScaleFactor(this.Scale) * 1E-6;

		/// <summary>Convert a measured value into megabytes (10^6 or 1,000,000 bytes) with a given precision</summary>
		/// <param name="value">Value to convert (any of <see cref="Median"/>, <see cref="Max"/>, ...)</param>
		/// <param name="precision">Number of digits for rounding</param>
		/// <returns><paramref name="value"/> converted into megabytes and rounded to the specified <paramref name="precision"/></returns>
		/// <remarks>If you want mebibytes (2^20 bytes), please call <see cref="ToKibiBytes(double, int)"/> instead.</remarks>
		/// <remarks>This only works if the histogram is measuring bytes. Other scales will return an incorrect value.</remarks>
		public double ToMegaBytes(double value, int precision) => Math.Round(value * GetScaleFactor(this.Scale) * 1E-6, precision, MidpointRounding.AwayFromZero);

		/// <summary>Convert a measured value into mebibytes (2^20 or 1,048,576 bytes)</summary>
		/// <param name="value">Value to convert (any of <see cref="Median"/>, <see cref="Max"/>, ...)</param>
		/// <returns><paramref name="value"/> converted into mebibytes</returns>
		/// <remarks>If you want megabytes (10^6 bytes), please call <see cref="ToKiloBytes(double)"/> instead.</remarks>
		/// <remarks>This only works if the histogram is measuring bytes.</remarks>
		public double ToMebiBytes(double value) => value * GetScaleFactor(this.Scale) / 1_048_576;

		/// <summary>Convert a measured value into mebibytes (2^20 or 1,048,576 bytes) with a given precision</summary>
		/// <param name="value">Value to convert (any of <see cref="Median"/>, <see cref="Max"/>, ...)</param>
		/// <param name="precision">Number of digits for rounding</param>
		/// <returns><paramref name="value"/> converted into mebibytes and rounded to the specified <paramref name="precision"/></returns>
		/// <remarks>If you want megabytes (10^6 bytes), please call <see cref="ToKiloBytes(double, int)"/> instead.</remarks>
		/// <remarks>This only works if the histogram is measuring bytes. Other scales will return an incorrect value.</remarks>
		public double ToMebiBytes(double value, int precision) => Math.Round(value * GetScaleFactor(this.Scale) / 1_048_576, precision, MidpointRounding.AwayFromZero);

		/// <summary>Convert a measured value into megabytes (10^9 or 1,000,000,000 bytes)</summary>
		/// <param name="value">Value to convert (any of <see cref="Median"/>, <see cref="Max"/>, ...)</param>
		/// <returns><paramref name="value"/> converted into gigabytes</returns>
		/// <remarks>If you want mebibytes (2^30 bytes), please call <see cref="ToGibiBytes(double)"/> instead.</remarks>
		/// <remarks>This only works if the histogram is measuring bytes.</remarks>
		public double ToGigaBytes(double value) => value * GetScaleFactor(this.Scale) * 1E-9;

		/// <summary>Convert a measured value into megabytes (10^9 or 1,000,000,000 bytes) with a given precision</summary>
		/// <param name="value">Value to convert (any of <see cref="Median"/>, <see cref="Max"/>, ...)</param>
		/// <param name="precision">Number of digits for rounding</param>
		/// <returns><paramref name="value"/> converted into gigabytes and rounded to the specified <paramref name="precision"/></returns>
		/// <remarks>If you want mebibytes (2^30 bytes), please call <see cref="ToGibiBytes(double, int)"/> instead.</remarks>
		/// <remarks>This only works if the histogram is measuring bytes. Other scales will return an incorrect value.</remarks>
		public double ToGigaBytes(double value, int precision) => Math.Round(value * GetScaleFactor(this.Scale) * 1E-9, precision, MidpointRounding.AwayFromZero);

		/// <summary>Convert a measured value into mebibytes (2^30 or 1,073,741,824 bytes)</summary>
		/// <param name="value">Value to convert (any of <see cref="Median"/>, <see cref="Max"/>, ...)</param>
		/// <returns><paramref name="value"/> converted into gibibytes</returns>
		/// <remarks>If you want megabytes (10^9 bytes), please call <see cref="ToGigaBytes(double)"/> instead.</remarks>
		/// <remarks>This only works if the histogram is measuring bytes.</remarks>
		public double ToGibiBytes(double value) => value * GetScaleFactor(this.Scale) / 1_073_741_824;

		/// <summary>Convert a measured value into mebibytes (2^30 or 1,073,741,824 bytes) with a given precision</summary>
		/// <param name="value">Value to convert (any of <see cref="Median"/>, <see cref="Max"/>, ...)</param>
		/// <param name="precision">Number of digits for rounding</param>
		/// <returns><paramref name="value"/> converted into gibibytes and rounded to the specified <paramref name="precision"/></returns>
		/// <remarks>If you want megabytes (10^9 bytes), please call <see cref="ToGigaBytes(double, int)"/> instead.</remarks>
		/// <remarks>This only works if the histogram is measuring bytes. Other scales will return an incorrect value.</remarks>
		public double ToGibiBytes(double value, int precision) => Math.Round(value * GetScaleFactor(this.Scale) / 1_073_741_824, precision, MidpointRounding.AwayFromZero);

		#endregion

		#endregion

		/// <summary>Computes the value of the given percentile <paramref name="p"/> (0..100%)</summary>
		/// <param name="p">Value of the percentile, expressed in % from 0 to 100 (ex: 50 is equivalent to the median)</param>
		/// <returns>Corresponding percentile value</returns>
		[MustUseReturnValue, Pure]
		public double Percentile(double p)
		{
			if (this.Count <= 0) return double.NaN;
			double threshold = this.Count * (p / 100.0d);
			double sum = 0;

			var buckets = this.Buckets;
			var limits = BucketLimits;

			for (int b = 0; b < buckets.Length; b++)
			{
				sum += buckets[b];
				if (sum >= threshold)
				{
					// Scale linearly within this bucket
					double leftPoint = (b == 0) ? 0 : limits[b - 1];
					double rightPoint = limits[b];
					double leftSum = sum - buckets[b];
					double rightSum = sum;
					double pos = (threshold - leftSum) / (rightSum - leftSum);
					double r = leftPoint + (rightPoint - leftPoint) * pos;
					if (r < this.Min) r = this.Min;
					if (r > this.Max) r = this.Max;
					return r;
				}
			}
			return this.Max;
		}

		/// <summary>Computes the Mean Absolute Deviation</summary>
		[MustUseReturnValue, Pure]
		// ReSharper disable once InconsistentNaming
		public double MAD()
		{
			if (this.Count == 0) return 0;
			var median = Percentile(50);

			var array = this.Buckets
				.Select((x, i) =>
				{
					double leftPoint = i > 0 ? BucketLimits[i - 1] : 0;
					double rightPoint = BucketLimits[i];
					// assume that we are in between of the two buckets
					return new { Count = x, Deviation = Math.Abs(((leftPoint + rightPoint) / 2d) - median) };
				})
				.Where(kvp => kvp.Count > 0)
				.OrderBy((kvp => kvp.Deviation))
				.ToArray();

			double threshold = this.Count * 0.5d;
			double sum = 0;
			for (int b = 0; b < array.Length; b++)
			{
				sum += array[b].Count;
				if (sum >= threshold)
				{
					// Scale linearly within this bucket
					double leftPoint = (b == 0) ? 0 : array[b - 1].Deviation;
					double rightPoint = array[b].Deviation;
					double leftSum = sum - array[b].Count;
					double rightSum = sum;
					double pos = (threshold - leftSum) / (rightSum - leftSum);
					double r = leftPoint + (rightPoint - leftPoint) * pos;
					return r;
				}
			}
			return array.LastOrDefault()?.Deviation ?? double.NaN;
		}

		/// <summary>Compute the arithmetic average</summary>
		public double Average
		{
			[MustUseReturnValue, Pure]
			get => this.Count == 0 ? 0 : (this.Sum / this.Count);
		}

		/// <summary>Compute the standard deviation</summary>
		public double StandardDeviation
		{
			[MustUseReturnValue, Pure]
			get
			{
				if (this.Count == 0) return 0;
				double variance = ((this.SumSquares * this.Count) - (this.Sum * this.Sum)) / ((double) this.Count * this.Count);
				return Math.Sqrt(variance);
			}
		}

		[MustUseReturnValue, Pure]
		private static string FormatHistoBar(double value, int chars, char pad = '\0', bool sparse = false)
		{
			int marks = (int) Math.Round((value * chars * 10), MidpointRounding.AwayFromZero);

			if (value >= 1)
			{
				if (value > 1.0 + double.Epsilon) return new string('+', chars);
				if (sparse) return new string(pad == '\0' ? ' ' : pad, chars - 1) + "@";
				return new string('@', chars);
			}

			var buf = new char[chars];
			var s = buf.AsSpan();

			int p = 0;

			if (value < -double.Epsilon)
			{
				s[0] = '[';
				s[1] = 'N';
				s[2] = 'E';
				s[3] = 'G';
				s[4] = ']';
				p += 5;
			}
			else if (marks == 0)
			{
				if (value > double.Epsilon) s[p++] = '`';
			}
			else if (marks > 0)
			{
				while (marks > 10)
				{
					s[p++] = !sparse ? '#' : pad == '\0' ? ' ' : pad;
					marks -= 10;
				}

				if (marks > 0)
				{
					if (sparse)
						s[p++] = BarTicksChars[marks];
					else
						s[p++] = BarChartChars[marks];
				}
			}

			if (pad != '\0')
			{
				while (p < chars) s[p++] = pad;
			}

			return new string(buf, 0, p);
		}

		/// <summary>Generate an areaplot that will display the distribution of samples, by value</summary>
		/// <returns>String that will look like this: <c>"   __xX=-___x___     "</c></returns>
		[MustUseReturnValue, Pure]
		public string GetDistribution(double begin = 1.0d, double end = MaxValue, int fold = 0)
		{
			if (fold < 1) fold = 1;
			int offset = GetBucketIndex(begin);
			int len = GetBucketIndex(end) - offset;

			var buckets = this.Buckets;
			var data = new long[fold == 1 ? len : (int)Math.Ceiling(1.0 * len / fold)];
			for (int i = 0; i < len; i++) { data[i / fold] += buckets[offset + i]; }

			long max = this.Count > 0 ? data.Max() : 0;

			if (max <= 0) return new string(' ', len);
			max = (3 * max + this.Count) / 4;

			Span<char> cs = stackalloc char[data.Length];
			var rr = (double)(VerticalChartChars.Length - 1) / max;
			for (int i = 0; i < cs.Length; i++)
			{
				int p = Math.Min((int)Math.Ceiling(rr * data[i]), 10);
				cs[i] = VerticalChartChars[p];
			}
			return new string(cs);
		}

		/// <summary>Computes the distribution of data between the specified bounds</summary>
		[MustUseReturnValue, Pure]
		public double[] GetDistributionData(double start = 1.0d, double end = MaxValue)
		{
			int offset = GetBucketIndex(start);
			int len = Math.Max(GetBucketIndex(end) - offset, 0);
			double[] xs = new double[len];
			for (int i = 0; i < len; i++)
			{
				xs[i] = this.Buckets[i + offset];
			}
			return xs;
		}

		/// <summary>Computes the distribution of data between the computed <see cref="LowThreshold"/> and <see cref="HighThreshold"/></summary>
		[MustUseReturnValue, Pure]
		public string GetDistributionAuto() => GetDistribution(this.LowThreshold, this.HighThreshold);

		private static void Write(Span<char> buf, int offset, double p, char c, char ifEqual = '\0', char replaceWith = '\0')
		{
			int x = GetBucketIndex(p) - offset;
			if (x >= 0 && x < buf.Length)
			{
				buf[x] = ifEqual != '\0' && buf[x] == ifEqual ? replaceWith : c;
			}
		}

		/// <summary>Generates a horizontal boxplot that displays the distribution of the sampled values</summary>
		/// <returns><para>String that will look like <c>"¤     ×·(——[==#===]——————)···×          @"</c>.</para>
		/// <code>Legend:
		/// - '#' = MEDIAN
		/// - '¤', '@' = MIN, MAX
		/// - '[', ']' = P25, P75
		/// - '(', ')' = P05, P95
		/// - 'x' = P01, P99
		/// </code>
		/// </returns>
		[MustUseReturnValue, Pure]
		public string GetPercentile(double begin = 1.0d, double end = MaxValue)
		{
			int offset = GetBucketIndex(begin);
			int len = Math.Max(GetBucketIndex(end) - offset, 0);

			return string.Create(len, (Histo: this, Offset: offset), static (cs, s) =>
			{
				var h = s.Histo;

				Span<double> percentiles = stackalloc double[101];
				percentiles[0] = 0;
				for (int i = 1; i <= 100; i++)
				{
					percentiles[i] = h.Percentile(i);
				}

				var limits = BucketLimits;
				int offset = s.Offset;
				for (int i = 0; i < cs.Length; i++)
				{
					double p = limits[i + offset];
					if (p >= percentiles[1] && p <= percentiles[99])
					{
						int idx = percentiles.BinarySearch(p);
						if (idx < 0) idx = ~idx;
						cs[i] = idx < 5 | idx > 95 ? '\u00B7' // '.'
								: idx < 25 | idx > 75 ? '\u2014' // '-'
								: '=';
					}
					else
					{
						cs[i] = ' ';
					}
				}

				Write(cs, offset, percentiles[1], '\u00D7'); // P01
				Write(cs, offset, percentiles[99], '\u00D7'); // P99
				Write(cs, offset, percentiles[5], '('); // P05
				Write(cs, offset, percentiles[95], ')'); // P95
				Write(cs, offset, percentiles[25], '[', '(', '{'); // P25
				Write(cs, offset, percentiles[75], ']', ')', '}'); // P75
				Write(cs, offset, s.Histo.Min, '¤'); // MIN
				Write(cs, offset, s.Histo.Max, '@'); // MAX
				Write(cs, offset, percentiles[50], '#'); // MEDIAN
			});
		}

		/// <summary>Formats the percentile distribution between the computed <see cref="LowThreshold"/> and <see cref="HighThreshold"/></summary>
		[MustUseReturnValue, Pure]
		public string GetPercentileAuto() => GetPercentile(this.LowThreshold, this.HighThreshold);

		/// <summary>Computes the minimum threshold for the given value</summary>
		[MustUseReturnValue, Pure]
		public static double GetMinThreshold(double value)
		{
			return Math.Pow(1_000, Math.Floor(Math.Log10(value) / 3));
		}

		/// <summary>Computes the maximum threshold for the given value</summary>
		[MustUseReturnValue, Pure]
		public static double GetMaxThreshold(double value)
		{
			return Math.Pow(1_000, Math.Ceiling(Math.Log10(value) / 3));
		}

		/// <summary>Minimum value of the lowest bucket with at least one measurement</summary>
		public double LowThreshold
		{
			[MustUseReturnValue, Pure]
			get => GetMinThreshold(this.Min);
		}

		/// <summary>Maximum value of the highest bucket with at least one measurement</summary>
		public double HighThreshold
		{
			[MustUseReturnValue, Pure]
			get => GetMaxThreshold(this.Max);
		}

		/// <summary>Truncate a distribution scale according to the specified range</summary>
		[MustUseReturnValue, Pure]
		public static string GetScale(double start = 1.0d, double end = MaxValue, string? scaleString = null)
		{
			scaleString ??= HorizontalScale;
			int from = Math.Max(GetBucketIndex(start) - 1, 0);
			if (end >= MaxValue) return scaleString[from..];

			int to = Math.Max(GetBucketIndex(end) - 1, 0);
			int len = Math.Max(to - from, 1);

			return scaleString.Substring(from, len);
		}

		/// <summary>Formats the scale between the computed <see cref="LowThreshold"/> and <see cref="HighThreshold"/></summary>
		[MustUseReturnValue, Pure]
		public string GetScaleAuto(string? scaleString = null) => GetScale(this.LowThreshold, this.HighThreshold, scaleString);

		/// <summary>Generate a short text description of the percentiles of this distribution</summary>
		/// <returns><c>"P5 --| P25 == [ P50 ]== P75 |-- P95"</c></returns>
		[MustUseReturnValue, Pure]
		public string GetPercentiles()
		{
			return string.CreateInvariant($"{this.Percentile(5),5:#,##0.0} --| {this.Percentile(25),5:#,##0.0} ==[ {this.Percentile(50),5:#,##0.0} ]== {this.Percentile(75),5:#,##0.0} |-- {this.Percentile(95),5:#,##0.0}");
		}

		/// <summary>Generate a text report of the measurements, that can be written to the console or in a log file</summary>
		/// <param name="detailed">If <c>false</c>, generate a simple table. If <c>true</c>, output a more detailed version with bar graphs, that could exceed 80 characters per line.</param>
		[MustUseReturnValue, Pure]
		public string GetReport(bool detailed)
		{
			var r = new StringBuilder(1_024);

			var unit = GetScaleUnit(this.Scale);
			var xs = GetScaleFactor(this.Scale);

			r.AppendLine(CultureInfo.InvariantCulture, $"- Total : {this.Count:N0} ops in {ToTimeSpan(this.Sum).TotalSeconds:0.0##} sec ({this.Count / ToTimeSpan(this.Sum).TotalSeconds:N0} ops/sec)");

			if (this.Count > 0)
			{
				double min = this.Count == 0 ? 0d : this.Min;
				double max = this.Max;
				double median = this.Median;

				r.AppendLine(CultureInfo.InvariantCulture, $"- Min/Max: {Friendly(min, xs)} {unit} .. {Friendly(max, xs)} {unit}");
				r.AppendLine(CultureInfo.InvariantCulture, $"- Average: {Friendly(this.Average, xs)} {unit} (+/-{Friendly(this.StandardDeviation, xs)} {unit})");
				r.AppendLine(CultureInfo.InvariantCulture, $"- Median : {Friendly(median, xs)} {unit} (+/-{Friendly(this.MAD(), xs)} {unit})");
				r.AppendLine(CultureInfo.InvariantCulture, $"- Distrib: ({Friendly(Percentile(5), xs)}) - {Friendly(Percentile(25), xs)} =[ {Friendly(median, xs)} ]= {Friendly(Percentile(75), xs)} - ({Friendly(Percentile(95), xs)})");

				if (detailed)
				{
					r.AppendLine("   ___________________________________________________________________________________________________________________________________ ");
					r.AppendLine("  |____[ Min , Max )____|___Count____|__Percent____________________________________________________|___Cumulative________|__Weight____|");
				}
				else
				{
					r.AppendLine("   _________________________________________________________________________ ");
					r.AppendLine("  |____[ Min , Max )____|___Count____|__Percent__________________|__Cumul.__|");

				}

				double mult = 100.0d / this.Count;
				double sum = 0;
				var buckets = this.Buckets;
				var limits = BucketLimits;
				for (int b = 0; b < buckets.Length; b++)
				{
					long count = buckets[b];
					if (count <= 0) continue;

					sum += count;
					double left = ((b == 0) ? 0.0 : limits[b - 1]);
					double right = limits[b];

					r.AppendFormat(CultureInfo.InvariantCulture,
						"  | {0,8} - {1,-8} | {2,10:#,###,###} | {3,7:##0.000}% " + (detailed ? "{5,50} " : "{5, 16} ") + "| {4,7:##0.000}%" + (detailed ? " {6,10} | {7,10:N0}" : ""),
						/* 0 */ Friendly(left, xs),			// left
						/* 1 */ Friendly(right, xs),		// right
						/* 2 */ count,						// count
						/* 3 */ mult * count,				// percentage
						/* 4 */ mult * sum,					// cumulative percentage
						/* 5 */ FormatHistoBar((double)count / this.Count, detailed ? 50 : 16, pad: ' '),
						/* 6 */ detailed ? FormatHistoBar(sum / this.Count, 10, pad: '-', sparse: true) : string.Empty,
						/* 7 */ detailed ? Friendly((count * (left + right) / 2), xs) : ""
					);
					r.AppendLine(" |");
				}
				if (detailed)
				{
					r.AppendLine("  |_____________________|____________|_____________________________________________________________|_____________________|____________|");
				}
				else
				{
					r.AppendLine("  |_____________________|____________|___________________________|__________|");
				}
			}
			return r.ToString();
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return string.CreateInvariant($"Count={this.Count}, Avg={this.Average}, Min={(this.Count > 0 ? this.Min : 0)}, Max={this.Max}, Med={this.Median}");
		}

	}

}
