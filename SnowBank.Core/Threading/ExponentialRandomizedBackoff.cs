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

namespace SnowBank.Threading
{
	using NodaTime;

	/// <summary>Provides a state machine that can be used to retry operations after a randomized delay with built-in exponential backoff</summary>
	public sealed class ExponentialRandomizedBackoff
	{

		/// <summary>Initial delay</summary>
		public TimeSpan Initial { get; }

		/// <summary>Maximum delay</summary>
		public TimeSpan Maximum { get; }

		/// <summary>Multiply the delay by this factor after every retry</summary>
		public double BackoffFactor { get; }

		/// <summary>Randomize the delay between this value, and <see cref="RandomHigh"/> on every attempt</summary>
		/// <remarks>If <see cref="RandomHigh"/> equals <see cref="RandomLow"/> then there is no randomization</remarks>
		public double RandomLow { get; }

		/// <summary>Randomize the delay between <see cref="RandomLow"/> and this value on every attempt</summary>
		/// <remarks>If <see cref="RandomHigh"/> equals <see cref="RandomLow"/> then there is no randomization</remarks>
		public double RandomHigh { get; }

		/// <summary>Last delay</summary>
		public TimeSpan Last { get; private set; }

		/// <summary>Number of attempts</summary>
		public int Iterations { get; private set; }

		/// <summary>Internal state</summary>
		private TimeSpan Current { get; set; }

		private Random Rng { get; }

		private static Random SharedRng
#if !NETSTANDARD2_0
			=> Random.Shared;
#else
			{ get; } = new();
#endif

		/// <summary>Create a new state machine</summary>
		/// <param name="initial">Initial delay (after the first failure)</param>
		/// <param name="maximum">Maximum retry delay (before taking into account the randomization)</param>
		/// <param name="backoffFactor">Factor that is multiplied with the last delay to get th next delay (must be greater than 0, and usually greater than 1)</param>
		/// <param name="randomLow">Minimum randomized factor applied to each delay (must be greater than 0)</param>
		/// <param name="randomHigh">Maximum randomized factor applied to each delay (must be greater than, or equal to <paramref name="randomLow"/>)</param>
		/// <param name="rng">Pseudo-random number generator used by this instance</param>
		/// <exception cref="ArgumentOutOfRangeException">If any of the settings is out of range</exception>
		public ExponentialRandomizedBackoff(TimeSpan initial, TimeSpan maximum, double backoffFactor = 2.0d, double randomLow = 1.0d, double randomHigh = 1.5d, Random? rng = null)
		{
			if (initial < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(initial), "Initial delay cannot be negative.");
			if (maximum < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maximum), "Maximum delay cannot be negative.");
			if (backoffFactor <= 0) throw new ArgumentOutOfRangeException(nameof(backoffFactor), "Growth factor must be greater than zero.");
			if (randomLow <= 0) throw new ArgumentOutOfRangeException(nameof(randomLow), "Random factor must be greater than zero.");
			if (randomHigh < randomLow) throw new ArgumentOutOfRangeException(nameof(randomHigh), "Random factor must be greater than zero.");

			this.Initial = initial;
			this.Maximum = maximum;
			this.BackoffFactor = backoffFactor;
			this.RandomLow = randomLow;
			this.RandomHigh = randomHigh;
			this.Current = initial;
			this.Rng = rng ?? SharedRng;
		}

		/// <summary>Computes a randomized delay</summary>
		/// <param name="baseDelay">Base delay (before randomization)</param>
		/// <param name="low">Minimum randomized factor applied to the base delay (must be greater than 0, defaults to 1)</param>
		/// <param name="high">Maximum randomized factor applied to the base delay (must be greater or equal to <paramref name="low"/>, defaults to 1.5)</param>
		/// <param name="rng">Pseudo-random number generator used to compute the delay</param>
		/// <returns>Randomized delay 'd' that is equal to the base delay, multiplied by a random factor between <paramref name="low"/> and <paramref name="high"/></returns>
		/// <example><code>await Task.Delay(ExponentialRandomizedBackoff.GetNext(TimeSpan.FromSecondes(30), 0.75, 1.25), ct);</code></example>
		public static TimeSpan GetNext(TimeSpan baseDelay, double low = 1.0d, double high = 1.5d, Random? rng = null)
		{
			rng ??= SharedRng;
			var f =  low == high ? 1 : low + (rng.NextDouble() * (high - low));
			return TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * f);
		}

		/// <summary>Computes the minimum delay before attempting the next operation</summary>
		/// <param name="iterations">Number of calls to this method since the last <see cref="Reset"/></param>
		/// <returns>Randomized delay</returns>
		public TimeSpan GetNext(out int iterations)
		{
			TimeSpan current = this.Current;

			// compute the next one
			var next = TimeSpan.FromMilliseconds(this.BackoffFactor * current.TotalMilliseconds);
			if (next > this.Maximum) next = this.Maximum;

			// randomize the current one
			var f =  this.RandomLow == this.RandomHigh ? 1 : this.RandomLow + (this.Rng.NextDouble() * (this.RandomHigh - this.RandomLow));
			current = TimeSpan.FromMilliseconds(current.TotalMilliseconds * f);

			this.Last = current;
			this.Current = next;
			iterations = ++this.Iterations;
			return current;
		}

		/// <summary>Computes the minimum delay before attempting the next operation</summary>
		/// <returns>Randomized delay</returns>
		public TimeSpan GetNext()
		{
			return GetNext(out _);
		}

		/// <summary>Computes the earliest instant when the next operation should be attempted again</summary>
		/// <param name="now">Current time</param>
		/// <param name="iterations">Number of calls to this method since the last <see cref="Reset"/></param>
		/// <returns>Instant in the future with a randomized delay</returns>
		public Instant AdvanceBy(Instant now, out int iterations)
		{
			var delay = GetNext(out iterations);
			return now.Plus(Duration.FromTimeSpan(delay));
		}

		/// <summary>Computes the earliest instant when the next operation should be attempted again</summary>
		/// <param name="now">Current time</param>
		/// <returns>Instant in the future with a randomized delay</returns>
		public Instant AdvanceBy(Instant now)
		{
			var delay = GetNext();
			return now.Plus(Duration.FromTimeSpan(delay));
		}

		/// <summary>Computes the earliest instant when the next operation should be attempted again</summary>
		/// <param name="now">Current time</param>
		/// <param name="iterations">Number of calls to this method since the last <see cref="Reset"/></param>
		/// <returns>Instant in the future with a randomized delay</returns>
		public DateTimeOffset AdvanceBy(DateTimeOffset now, out int iterations)
		{
			var delay = GetNext(out iterations);
			return now + delay;
		}

		/// <summary>Computes the earliest instant when the next operation should be attempted again</summary>
		/// <param name="now">Current time</param>
		/// <returns>Instant in the future with a randomized delay</returns>
		public DateTimeOffset AdvanceBy(DateTimeOffset now)
		{
			var delay = GetNext();
			return now + delay;
		}

		/// <summary>Computes the earliest instant when the next operation should be attempted again</summary>
		/// <param name="now">Current time</param>
		/// <param name="iterations">Number of calls to this method since the last <see cref="Reset"/></param>
		/// <returns>Instant in the future with a randomized delay</returns>
		public DateTimeOffset AdvanceBy(DateTime now, out int iterations)
		{
			var delay = GetNext(out iterations);
			return now + delay;
		}

		/// <summary>Computes the earliest instant when the next operation should be attempted again</summary>
		/// <param name="now">Current time</param>
		/// <returns>Instant in the future with a randomized delay</returns>
		public DateTimeOffset AdvanceBy(DateTime now)
		{
			var delay = GetNext();
			return now + delay;
		}

		/// <summary>Computes the next instant after a randomized delay</summary>
		/// <param name="now">Current time</param>
		/// <param name="baseDelay">Base delay (before randomization)</param>
		/// <param name="low">Minimum randomized factor applied to the base delay (must be greater than 0, defaults to 1)</param>
		/// <param name="high">Maximum randomized factor applied to the base delay (must be greater or equal to <paramref name="low"/>, defaults to 1.5)</param>
		/// <param name="rng">Pseudo-random number generator used to compute the delay</param>
		/// <returns>Instant in the future that is after <paramref name="now"/> by a randomized delay 'd' that is equal to the <paramref name="baseDelay">base delay</paramref>, multiplied by a random factor between <paramref name="low"/> and <paramref name="high"/></returns>
		public static Instant AdvanceBy(Instant now, TimeSpan baseDelay, double low = 1.0d, double high = 1.5d, Random? rng = null)
			=> now.Plus(Duration.FromTimeSpan(GetNext(baseDelay, low, high, rng)));

		/// <summary>Reset the delay to the initial value, following a successful operation.</summary>
		/// <remarks>
		/// <para>This method should either be called at the start of a new request, or after a successful request, to reset the current state.</para>
		/// <para>This should not be called between unsuccessful attempts, otherwise the delay will never grow by the specified <see cref="BackoffFactor"/>.</para>
		/// </remarks>
		public void Reset()
		{
			this.Current = this.Initial;
			this.Iterations = 0;
		}

		/// <summary>Returns a task that will complete when the next operation should be attempted</summary>
		/// <param name="ct">Cancellation token used to abort the task</param>
		/// <returns>Task that will complete when the next delay has elapsed. The result will be the elapsed time.</returns>
		/// <remarks><para>This is equivalent to calling <c>Task.Delay(backoff.GetNext(), ct)</c>, except that it returns the delay itself.</para></remarks>
		public async Task<TimeSpan> Wait(CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();
			var delay = GetNext(out _);
			await Task.Delay(delay, ct).ConfigureAwait(false);
			return delay;
		}

		/// <summary>Returns a task that will complete when the next operation should be attempted, given the specified parameters</summary>
		/// <param name="baseDelay">Base delay (before randomization)</param>
		/// <param name="ct">Cancellation token used to abort the task</param>
		/// <param name="low">Minimum randomized factor applied to the base delay (must be greater than 0, defaults to 1)</param>
		/// <param name="high">Maximum randomzed factor applied to the base delay (must be greater or eqaul to <paramref name="low"/>, defaults to 1.5)</param>
		/// <param name="rng">Pseudo-random number generator used to compute the delay</param>
		/// <returns>Task that will complete when the randomized delay has elapsed. The result will be the elapsed time.</returns>
		/// <remarks><para>This is equivalent to calling <c>Task.Delay(ExponentialRandomizedBackoff.GetNext(baseDelay, low, high, rnd), ct)</c>, except that it returns the delay itself.</para></remarks>
		public static async Task<TimeSpan> Wait(TimeSpan baseDelay, CancellationToken ct, double low = 1.0d, double high = 1.5d, Random? rng = null)
		{
			ct.ThrowIfCancellationRequested();
			var delay = GetNext(baseDelay, low, high, rng);
			await Task.Delay(delay, ct).ConfigureAwait(false);
			return delay;
		}

	}

}
