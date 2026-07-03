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

namespace SnowBank.Threading.Tests
{
	using SnowBank.Numerics;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class ProbabilisticTriggerFacts : SimpleTest
	{

		[Test]
		public void Test_Probabilistic_Trigger()
		{
#if DEBUG
			const int RUNS = 10_000;
#else
			const int RUNS = 100_000;
#endif

			const double K = 0.1;
			const int MIN = 10;
			const int MEDIAN = 50;
			const int MAX = 100;

			var schrodingerApparatus = new ProbabilisticTrigger(Random.Shared, K, MIN, MEDIAN, MAX);

			var h = new RobustHistogram(RobustHistogram.TimeScale.Bytes);

			var buckets = new int[MAX + 1];

			for (int r = 0; r < RUNS; r++)
			{
				schrodingerApparatus.Reset();
				Assert.That(schrodingerApparatus.Attempts, Is.Zero);
				Assert.That(schrodingerApparatus.Triggered, Is.False);

				// repeatedly roll the dice until we trigger
				int rolls = 0;
				bool alive = true;
				for (int i = 1; i <= 125; i++)
				{
					++rolls;
					if (schrodingerApparatus.RollDice())
					{
						alive = false;
						break;
					}
				}

				Assert.That(rolls, Is.GreaterThanOrEqualTo(MIN));
				Assert.That(rolls, Is.LessThanOrEqualTo(MAX));
				Assert.That(alive, Is.False);
				buckets[rolls]++;
				h.Add(rolls);

				Assert.That(schrodingerApparatus.Attempts, Is.EqualTo(rolls));
				Assert.That(schrodingerApparatus.Triggered, Is.True);
			}

			{
				var sb = new StringBuilder();
				sb.AppendLine("==== DISTRIBUTION =========================");
				var scale = 200.0 / buckets.Max();
				for (int i = 0; i <= MAX; i++)
				{
					sb.AppendLineInvariant($"[{i:D03} | ");
					sb.Append('#', (int) Math.Ceiling(buckets[i] * scale));
				}
				Log(sb);
			}
			{
				var sb = new StringBuilder();
				sb.AppendLine("==== CUMULATIVE =========================");
				var scale = 200.0 / buckets.Sum();
				long s = 0;
				for (int i = 0; i <= MAX; i++)
				{
					s += buckets[i];
					int x = (int) Math.Ceiling(s * scale);
					sb.AppendLineInvariant($"[{i:D03} | ");
					sb.Append(x < 100 ? '%' : '#', x);
				}
				Log(sb);
			}

			Log(h.GetReport(true));


		}

	}

}
