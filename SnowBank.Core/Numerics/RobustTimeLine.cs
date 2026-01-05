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
	//REVIEW: TODO: move this to a more appropriate namespace ? Testing? Benchmarking?

	/// <summary>Helper for generating reports of time measurements</summary>
	[PublicAPI]
	public class RobustTimeLine
	{

		public List<RobustHistogram> Histos { get; }

		/// <summary>Time interval between stop steps</summary>
		public TimeSpan Step { get; }

		/// <summary>Measurement scale</summary>
		public RobustHistogram.TimeScale Scale { get; }

		/// <summary>Callback invoked when the measurement has completed</summary>
		public Func<RobustHistogram, int, bool>? Completed { get; }

		private int LastIndex { get; set; }

		private Stopwatch Clock { get; }

		private int Offset { get; set; }

		/// <summary>Constructs a new <see cref="RobustTimeLine"/> instance</summary>
		public RobustTimeLine(TimeSpan step, RobustHistogram.TimeScale scale = RobustHistogram.TimeScale.Milliseconds, Func<RobustHistogram, int, bool>? onCompleted = null)
		{
			if (step <= TimeSpan.Zero) throw new ArgumentException("Time step must be greater than zero", nameof(step));

			this.Histos = [ ];
			this.Step = step;
			this.Scale = scale;
			this.Completed = onCompleted;
			this.Clock = Stopwatch.StartNew();
		}

		/// <summary>Number of measurements</summary>
		public int Count => this.Histos.Count;

		/// <summary>Starts the clock</summary>
		public void Start()
		{
			this.Clock.Restart();
		}

		/// <summary>Stops the clock</summary>
		public void Stop()
		{
			this.Clock.Stop();
		}

		private int GetGraphIndex(TimeSpan elapsed)
		{
			return (int)(elapsed.Ticks / this.Step.Ticks);
		}

		private bool HasFrame(int index)
		{
			index -= this.Offset;
			return index >= 0 && index < this.Histos.Count;
		}

		private RobustHistogram GetFrame(TimeSpan elapsed)
		{
			int index = GetGraphIndex(elapsed);

			if (index != this.LastIndex && this.Completed != null && HasFrame(this.LastIndex))
			{
				if (this.Completed(this.Histos[this.LastIndex - this.Offset], this.LastIndex))
				{ // reset!
					this.Histos.Clear();
					this.Offset = this.LastIndex;
				}
				this.LastIndex = index;
			}

			while (!HasFrame(index))
			{
				this.Histos.Add(new(this.Scale));
			}

			return this.Histos[index - this.Offset];
		}

		/// <summary>Records a new measurement</summary>
		public void Add(double value)
		{
			GetFrame(this.Clock.Elapsed).Add(value);
		}

		/// <summary>Records a new measurement</summary>
		public void Add(TimeSpan value)
		{
			GetFrame(this.Clock.Elapsed).Add(value);
		}

		/// <summary>Merges the results in the given time windows</summary>
		public RobustHistogram MergeResults(TimeSpan window)
		{
			return MergeResults((int)Math.Max(1, Math.Ceiling((double)window.Ticks / this.Step.Ticks)));
		}

		/// <summary>Merges the results of the last measurements</summary>
		public RobustHistogram MergeResults(int samples)
		{
			var merged = new RobustHistogram(this.Scale);
			foreach(var h in this.Histos.Reverse<RobustHistogram>().Take(samples))
			{
				merged.Merge(h);
			}
			return merged;
		}

		/// <summary>Merges all the results</summary>
		public RobustHistogram MergeResults()
		{
			return MergeResults(this.Histos.Count);
		}

	}

}
