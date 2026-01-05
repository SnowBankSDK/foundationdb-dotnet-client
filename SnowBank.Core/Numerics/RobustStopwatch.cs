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

	/// <summary>Helper for measuring precise elapsed time</summary>
	/// <remarks>
	/// <para>This type is used as a shim for <see cref="Stopwatch"/> for .NET 7 or lower.</para>
	/// <para>If you are on .NET 8 or higher, you can use <see cref="Stopwatch.GetTimestamp"/> and <see cref="Stopwatch.GetElapsedTime(long)"/> directly.</para>
	/// </remarks>
	[DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	public struct RobustStopwatch
	{

		static RobustStopwatch()
		{
			// JIT warmup!

			var sw = new RobustStopwatch();
			sw.Start();
			_ = sw.Elapsed;
			_ = sw.IsRunning;
			sw.Stop();
			_ = sw.Elapsed;
			_ = sw.IsRunning;
			sw.Restart();
			_ = sw.Elapsed;
			_ = sw.IsRunning;
			sw.Reset();
		}

		internal RobustStopwatch(long timestamp)
		{
			this.LastStart = timestamp;
			this.Tally = TimeSpan.Zero;
		}

		private long LastStart;

		private TimeSpan Tally;

		/// <summary>Creates a new stopwatch with the clock already running</summary>
		public static RobustStopwatch StartNew() => new(Stopwatch.GetTimestamp());

		/// <summary>Restarts the stopwatch</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Restart()
		{
			this.Tally = TimeSpan.Zero;
			this.LastStart = Stopwatch.GetTimestamp();
		}

		/// <summary>Starts the stopwatch</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Start()
		{
			this.LastStart = Stopwatch.GetTimestamp();
		}

		/// <summary>Stops the stopwatch</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TimeSpan Stop()
		{
			var elapsed = this.Elapsed;
			this.Tally = elapsed;
			this.LastStart = 0;
			return elapsed;
		}

		/// <summary>Stops and reset the stopwatch</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Reset()
		{
			this.Tally = TimeSpan.Zero;
			this.LastStart = 0;
		}

		/// <summary>Tests if the stopwatch is running</summary>
		public readonly bool IsRunning
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.LastStart != 0;
		}

		/// <summary>Returns the elapsed time</summary>
		public readonly TimeSpan Elapsed
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (this.LastStart != 0 ? GetElapsedTime(this.LastStart) : TimeSpan.Zero) + this.Tally;
		}

		/// <summary>Returns the elapsed time (in ticks)</summary>
		public readonly long ElapsedTicks
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Elapsed.Ticks;
		}

		/// <summary>Returns the resolution of the stopwatch, i.e. the minimum time interval that can be measured</summary>
		public static TimeSpan MinimumResolution => System.TimeSpan.FromTicks((long) Math.Ceiling((double) TimeSpan.TicksPerSecond / Stopwatch.Frequency));

		/// <inheritdoc />
		public override string ToString() => $"{Elapsed} (IsRunning = {this.IsRunning})";

		/// <summary>Returns a timestamp for the current time</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long GetTimestamp() => Stopwatch.GetTimestamp();

		/// <summary>Computes the elapsed time since the given timestamp</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TimeSpan GetElapsedTime(long startingTimestamp)
#if NET8_0_OR_GREATER
			=> Stopwatch.GetElapsedTime(startingTimestamp);
#else
			=> GetElapsedTime(startingTimestamp, Stopwatch.GetTimestamp());
#endif

		/// <summary>Computes the elapsed time between two timestamps</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp)
#if NET8_0_OR_GREATER
			=> Stopwatch.GetElapsedTime(startingTimestamp, endingTimestamp);
#else
			=> new TimeSpan((long) ((endingTimestamp - startingTimestamp) * s_tickFrequency));

		private static readonly double s_tickFrequency = (double) TimeSpan.TicksPerSecond / Stopwatch.Frequency;
#endif

	}

}
