#region Copyright Attribution
// Copyright (c) 2011 The LevelDB Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file. See the AUTHORS file for names of contributors.

// This is a port and adaptation of the 'histogram.cc' file of LevelDB 1.9. (https://code.google.com/p/leveldb/source/browse/util/histogram.cc)
// > Ported from C++ to C#
// > Added more buckets for smaller precisions (may break the Standard Deviation)
// > Added timescale units configuration (defaults to milliseconds)
// > Added Median Absolute Deviation
// > Added nicer ASCII barcharts and formating helper methods

#endregion

namespace FdbShell
{
	public sealed class RobustHistogram
	{

		public enum TimeScale
		{
			Ticks,
			Nanoseconds,
			Microseconds,
			Milliseconds,
			Seconds,
		}

		const int NumBuckets = 154 + 25;
		private static readonly double[] BucketLimits =
		[
			0.01, 0.02, 0.03, 0.04, 0.05, 0.06, 0.07, 0.08, 0.09,
			0.10, 0.12, 0.14, 0.16, 0.18, 0.20, 0.25, 0.30, 0.35, 0.40, 0.45, 0.50, 0.60, 0.70, 0.80, 0.90,
			1, 2, 3, 4, 5, 6, 7, 8, 9,
			10, 12, 14, 16, 18, 20, 25, 30, 35, 40, 45, 50, 60, 70, 80, 90,
			100, 120, 140, 160, 180, 200, 250, 300, 350, 400, 450, 500, 600, 700, 800, 900,
			1000, 1200, 1400, 1600, 1800, 2000, 2500, 3000, 3500, 4000, 4500, 5000, 6000, 7000, 8000, 9000,
			10000, 12000, 14000, 16000, 18000, 20000, 25000, 30000, 35000, 40000, 45000, 50000, 60000, 70000, 80000, 90000,
			100000, 120000, 140000, 160000, 180000, 200000, 250000, 300000, 350000, 400000, 450000, 500000, 600000, 700000, 800000, 900000,
			1000000, 1200000, 1400000, 1600000, 1800000, 2000000, 2500000, 3000000, 3500000, 4000000, 4500000, 5000000, 6000000, 7000000, 8000000, 9000000, 
			10000000, 12000000, 14000000, 16000000, 18000000, 20000000, 25000000, 30000000, 35000000, 40000000, 45000000, 50000000, 60000000, 70000000, 80000000, 90000000,
			100000000, 120000000, 140000000, 160000000, 180000000, 200000000, 250000000, 300000000, 350000000, 400000000, 450000000, 500000000, 600000000, 700000000, 800000000, 900000000,
			1000000000, 1200000000, 1400000000, 1600000000, 1800000000, 2000000000, 2500000000, 3000000000, 3500000000, 4000000000, 4500000000, 5000000000, 6000000000, 7000000000, 8000000000, 9000000000,
			1e200
		];
		private static readonly double SUM_RATIO = (1d / BucketLimits[0]);
		private static readonly double SUM_SQUARES_RATIO = SUM_RATIO * SUM_RATIO;

		private const string BarTicksChars = "<[[((|))]]>";
		private const string BarChartChars = " .:;+=xX$&#";
		private const string VerticalChartChars = " _.-:=xX&#@";

		public RobustHistogram()
			: this(TimeScale.Microseconds)
		{ }

		public RobustHistogram(TimeScale scale)
		{
			this.Clear();
			this.Scale = scale;
			this.TicksToUnit = GetScaleToTicksRatio(scale);
		}

		private static double GetScaleToTicksRatio(TimeScale scale)
		{
			switch(scale)
			{
				case TimeScale.Ticks: return 1.0d;
				case TimeScale.Nanoseconds: return 1E-2d;
				case TimeScale.Microseconds: return 1E1d;
				case TimeScale.Milliseconds: return 1E4d;
				case TimeScale.Seconds: return 1E7d;
				default: return 1.0d;
			}
		}

		[System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(Buckets))]
		public void Clear()
		{
			this.Min = BucketLimits[NumBuckets - 1];
			this.Max = 0;
			this.Count = 0;
			this.InternalSum = 0;
			this.InternalSumSquares = 0;
			if (this.Buckets == null)
			{
				this.Buckets = new long[NumBuckets];
			}
			else
			{
				Array.Clear(this.Buckets, 0, this.Buckets.Length);
			}
		}

		public void Merge(RobustHistogram other)
		{
			if (other.Min < this.Min) this.Min = other.Min;
			if (other.Max > this.Max) this.Max = other.Max;
			this.Count += other.Count;
			this.InternalSum += other.InternalSum;
			this.InternalSumSquares += other.InternalSumSquares;
			for (int b = 0; b < NumBuckets; b++)
			{
				this.Buckets[b] += other.Buckets[b];
			}
		}

		public void AddTicks(long ticks)
		{
			Add(ticks / this.TicksToUnit);
		}

		private static int GetBucketIndex(double value)
		{
			// Linear search to find the corresponding bucket index
			int index = 0;
			while (index < NumBuckets - 1 && BucketLimits[index] <= value)
			{
				++index;
			}
			return index;
		}

		public void Add(double value)
		{
			int bucketIndex = GetBucketIndex(value);
			++this.Buckets[bucketIndex];
			if (this.Min > value) this.Min = value;
			if (this.Max < value) this.Max = value;
			++this.Count;
			this.InternalSum += (value * SUM_RATIO);
			this.InternalSumSquares += (value * value * SUM_SQUARES_RATIO);
		}

		public void Add(TimeSpan value)
		{
			AddTicks(value.Ticks);
		}

		public TimeScale Scale { get; }

		private double TicksToUnit { get; }

		/// <summary>Returns the number of samples: Ts.Count()</summary>
		public int Count { get; private set; }

		/// <summary>Returns the minimum sample: Ts.Min()</summary>
		public double Min { get; private set; }

		/// <summary>Returns the maximum sample: Ts.Max()</summary>
		public double Max { get; private set; }

		/// <summary>Returns the sum of the values: Ts.Sum(t => t)</summary>
		public double Sum => this.InternalSum / SUM_RATIO;

		private double InternalSum { get; set; }
		private double InternalSumSquares { get; set; }

		private long[] Buckets { get; set; }

		/// <summary>Returns the median sample</summary>
		public double Median => Percentile(50);

		/// <summary>Returns the value of percentile <paramref name="p"/> (between 0 and 100)</summary>
		/// <param name="p">Percentile value, between 0 and 100 (e.g. 50 for the median)</param>
		/// <returns>Corresponding percentile value</returns>
		public double Percentile(double p)
		{
			double threshold = this.Count * (p / 100.0d);
			double sum = 0;
			for (int b = 0; b < this.Buckets.Length; b++)
			{
				sum += this.Buckets[b];
				if (sum >= threshold)
				{
					// Scale linearly within this bucket
					double leftPoint = (b == 0) ? 0 : BucketLimits[b - 1];
					double rightPoint = BucketLimits[b];
					double leftSum = sum - this.Buckets[b];
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

		/// <summary>Returns the average value</summary>
		public double Average => this.Count == 0 ? 0 : (this.Sum / this.Count);

		public static string FormatHistoBar(double value, int chars, char pad = '\0', bool sparse = false)
		{
			int marks = (int)Math.Round((value * chars * 10), MidpointRounding.AwayFromZero);

			if (value >= 1)
			{
				if (value > 1.0 + double.Epsilon) return new string('+', chars);
				if (sparse) return new string(pad == '\0' ? ' ' : pad, chars - 1) + "@";
				return new string('@', chars);
			}

			char[] s = new char[chars];

			int p = 0;

			if (value < -double.Epsilon)
			{
				s[p++] = '[';
				s[p++] = 'N';
				s[p++] = 'E';
				s[p++] = 'G'; 
				s[p++] = ']';
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

			return new string(s, 0, p);
		}

		public string GetDistribution(double begin = 1.0d, double end = 1E200, int fold = 0)
		{
			if (fold < 1) fold = 1;
			int offset = GetBucketIndex(begin);
			int len = GetBucketIndex(end) - offset;

			var data = new long[fold == 1 ? len : (int)Math.Ceiling(1.0 * len / fold)];
			for (int i = 0; i < len; i++) { data[i / fold] += this.Buckets[offset + i]; }

			long max = this.Count > 0 ? data.Max() : 0;

			if (max <= 0) return new string(' ', fold > 1 ? len / fold : len);
			max = (3 * max + this.Count) / 4;

			char[] cs = new char[data.Length];
			var rr = (double)(VerticalChartChars.Length - 1) / max;
			for (int i = 0; i < cs.Length; i++)
			{
				int p = Math.Min((int)Math.Ceiling(rr * data[i]), 10);
				cs[i] = VerticalChartChars[p];
			}
			return new string(cs);
		}

		public override string ToString() => string.CreateInvariant($"Count={this.Count}, Avg={this.Average}, Min={(this.Count > 0 ? this.Min : 0)}, Max={this.Max}");

	}

}
