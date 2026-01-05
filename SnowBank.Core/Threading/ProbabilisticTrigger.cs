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

	/// <summary>Helps decide whether to trigger some process or postpone to the next opportunity, with some adjustable probabilities</summary>
	/// <remarks>
	/// <para>This rolls a virtual dice that will, on average, returns true after a certain number of attempts, with enforced minimum and maximum.</para>
	/// <para>Let's say, for example, that you need to perform some cleanup operation on average every 50 iterations, but never before 20 iterations or after 100 iterations.
	/// When calling <see cref="RollDice()"/> repeatedly, you should observe a run at least 20 consecutive <c>false</c> results, with the first <c>true</c> result happening (on average) before the 50th call, and never after the 100th call.</para>
	/// <para>This type is not thread-safe, and requires external locking if shared between multiple threads.</para>
	/// <para><code lang="c#">
	/// // trigger on average every 50 calls, never before 10, always before 100
	/// var trigger = new ProbabilisticTrigger(Random.Shared, 0.1, 10, 50, 100);
	/// source.OnChanged((...) =>
	/// {
	///    // process the event...
	///    // maybe kick off some cleaning operation
	///    if (trigger.RollDice())
	///    {
	///       await PerformCleanupOperation(...);
	///       trigger.Reset(); // reset the trigger!
	///    }
	/// });
	/// </code></para>
	/// </remarks>
	[DebuggerDisplay("Attempts={Attempts}, Triggered={Triggered}, Min={Minimum}, Med={Median}")]
	public sealed class ProbabilisticTrigger
	{

		/// <summary>Random generator used to resolve the probabilities</summary>
		public Random Rng { get; }

		/// <summary>Minimum number of attempts before we are allowed to trigger.</summary>
		public int Minimum { get; }

		/// <summary>Median number of attempts before the first trigger.</summary>
		/// <remarks>This represents the "center point" where half of the players would have triggered at least once.</remarks>
		public int Median { get; }

		/// <summary>Maximum number of failed attempts before a forced trigger (if non-null).</summary>
		public int? Maximum { get; }

		/// <summary>Factor (<c>0</c> &lt; k &lt;= <c>1.0</c>) used to smooth the probability distribution curve. Lower means smoother.</summary>
		public double Steepness { get; }

		/// <summary>Number of calls to <see cref="RollDice()"/> since the last call to <see cref="Reset"/></summary>
		public int Attempts { get; private set; }

		/// <summary>Sets to true whenever <see cref="RollDice()"/> returns <c>true</c>, and reset to <c>false</c> whenever <see cref="Reset"/> is called.</summary>
		public bool Triggered { get; private set; }

		public ProbabilisticTrigger(Random rng, double steepness, int minimum, int median, int? maximum)
		{
			if (steepness is <= 0 or > 1) throw new ArgumentOutOfRangeException(nameof(steepness), steepness, "steepness factor 'k' must satisfy 0 < k <= 1");
			if (minimum < 0) throw new ArgumentOutOfRangeException(nameof(minimum), minimum, "Low threshold must be a positive number");
			if (median < minimum) throw new ArgumentOutOfRangeException(nameof(median), median, "High threshold cannot be less than the low threshold");
			if (maximum < median) throw new ArgumentOutOfRangeException(nameof(maximum), maximum, "Maximum cannot be less then the high threshold");

			this.Rng = rng;
			this.Steepness = steepness;
			this.Minimum = minimum;
			this.Median = median;
			this.Maximum = maximum;
		}

		/// <summary>Rolls the dice again, to device whether to perform some action immediately or wait for the next opportunity.</summary>
		/// <returns><c>true</c> if the action should be performed immediately; or <c>false</c> if it should be delayed.</returns>
		/// <remarks>
		/// <para>Calling this method will increment the internal <see cref="Attempts"/>, which can be used to infer the number of attempts since the last call to <see cref="Reset"/>.</para>
		/// <para>Whenever the method returns <c>true</c>, you should call <see cref="Reset"/> in order to start a new run, otherwise the trigger will assume that the attempt failed.</para>
		/// </remarks>
		public bool RollDice()
		{
			int n = ++this.Attempts;
			if (!RollDice(this.Rng, n, this.Steepness, this.Minimum, this.Median, this.Maximum))
			{
				return false;
			}

			this.Triggered = true;
			return true;
		}

		/// <summary>Resets the internal <see cref="Attempts"/> to <c>0</c>, before starting a new session.</summary>
		public void Reset()
		{
			this.Attempts = 0;
			this.Triggered = false;
		}

		/// <summary>Rolls the dice, to device whether to perform some operation or wait for the next opportunity.</summary>
		/// <param name="rng">Random number generator</param>
		/// <param name="iteration">Current attempt number, starting at 1</param>
		/// <param name="k">Coefficient used to "smooth" the internal probability distribution curve, in the range <c>0</c> &lt; <c>k</c> &lt; <c>1.0</c>. Lower values giving more spread out probabilities around the center point. Recommended values are between 0.05 and 0.2</param>
		/// <param name="minimum">Minimum number of attempts before the operation is allowed to execute. The method will always return <c>false</c> if <paramref name="iteration"/> is less than the minimum.</param>
		/// <param name="median">Number of rolls required to have 50% chances to trigger at least once. If you have multiple actors, all repeatedly calling this method until it returns true, half of them should have stopped before reaching this point and half of them should still be in the race.</param>
		/// <param name="maximum">Maximum number of attempts before the operation is guaranteed to execute. The method will always return <c>true</c> if <paramref name="iteration"/> is greater than or equal to this value. If <c>null</c>, there is no maximum, and it is possible (even though extremely unlikely) that this method returns <c>false</c> forever.</param>
		/// <returns></returns>
		public static bool RollDice(Random rng, int iteration, double k, int minimum, int median, int? maximum)
		{
			Contract.Debug.Requires(rng is not null && iteration >= 0 && k is > 0 and <= 1 && (median >= minimum) && (maximum is null || maximum >= median));

			if (iteration < minimum)
			{ // never before the minimum threshold
				return false;
			}

			if (iteration >= maximum)
			{ // we reached the maximum
				return true;
			}

			// S(n) = 1 / (1 + e(-k.(n-median))
			double n         = (iteration - minimum) - (median - minimum);
			double sPrevious = 1.0 / (1.0 + Math.Exp(-k * (n - 1))); // probability to trigger at or before attempt N-1
			double sCurrent  = 1.0 / (1.0 + Math.Exp(-k * n));       // probability to trigger at or before attempt N
			Contract.Debug.Assert(sCurrent > sPrevious);

			// The delta between the two gives us the probability to trigger at exactly attempt N
			double p         = (sCurrent - sPrevious) / (1.0 - sPrevious);
			Contract.Debug.Assert(p is >= 0 and < 1);

			// Roll the dice to decide
			return rng.NextDouble() < p;
		}

	}

}
