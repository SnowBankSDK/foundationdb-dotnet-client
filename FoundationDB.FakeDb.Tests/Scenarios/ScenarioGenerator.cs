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

namespace FoundationDB.Client.Tests
{
	using FoundationDB.Client;

	/// <summary>Seeded random scenario generator (design spec §6.5). Single-transaction read-your-writes fuzzing is the primary target: zero nondeterminism, a huge input space, trivially comparable traces.</summary>
	public static class ScenarioGenerator
	{

		private static readonly string[] Keys = [ "k0", "k1", "k2", "k3", "k4", "k5", "k6", "k7" ];

		/// <summary>Generates a deterministic single-transaction RYW fuzz scenario for the given seed (same seed, same scenario, always).</summary>
		public static Scenario GenerateRywFuzz(int seed)
		{
			var rnd = new Random(seed);
			var b = new ScenarioBuilder();

			// phase 1: seed some committed state
			b.Begin("A");
			int seeded = rnd.Next(0, 5);
			for (int i = 0; i < seeded; i++)
			{
				b.Set("A", PickKey(rnd), "c" + rnd.Next(10));
			}
			b.Commit("A");

			// phase 2: one transaction mixing mutations and reads over the merged view
			b.Begin("A");
			int ops = rnd.Next(15, 26);
			for (int i = 0; i < ops; i++)
			{
				switch (rnd.Next(100))
				{
					case < 25:
					{
						b.Set("A", PickKey(rnd), "v" + rnd.Next(10));
						break;
					}
					case < 35:
					{
						b.Clear("A", PickKey(rnd));
						break;
					}
					case < 45:
					{
						var (lo, hi) = PickOrderedPair(rnd);
						b.ClearRange("A", lo, hi);
						break;
					}
					case < 55:
					{ // atomic add, sometimes over short ascii values: the operand length defines the arithmetic width
						b.Atomic("A", PickKey(rnd), Slice.FromFixed64(rnd.Next(1, 100)), FdbMutationType.Add);
						break;
					}
					case < 75:
					{
						b.Get("A", PickKey(rnd), snapshot: rnd.Next(4) == 0);
						break;
					}
					case < 85:
					{
						b.GetKey("A", new ScenarioSelector(Slice.FromStringAscii(PickKey(rnd)), OrEqual: rnd.Next(2) == 0, Offset: rnd.Next(-2, 3)));
						break;
					}
					default:
					{
						var (lo, hi) = PickOrderedPair(rnd);
						b.GetRange("A",
							new ScenarioSelector(Slice.FromStringAscii(lo), OrEqual: false, Offset: 1),
							new ScenarioSelector(Slice.FromStringAscii(hi), OrEqual: rnd.Next(2) == 0, Offset: 1),
							limit: rnd.Next(3) == 0 ? rnd.Next(1, 4) : null,
							reverse: rnd.Next(3) == 0,
							snapshot: rnd.Next(4) == 0);
						break;
					}
				}
			}
			b.Commit("A");

			return b.Build($"fuzz_ryw_{seed:D4}", $"generated single-transaction RYW fuzz (seed {seed})");
		}

		private static string PickKey(Random rnd) => Keys[rnd.Next(Keys.Length)];

		private static (string Lo, string Hi) PickOrderedPair(Random rnd)
		{
			int a = rnd.Next(Keys.Length), c = rnd.Next(Keys.Length);
			if (a == c)
			{ // a range needs distinct bounds: extend the upper one past the key
				return (Keys[a], Keys[a] + "z");
			}
			return a < c ? (Keys[a], Keys[c]) : (Keys[c], Keys[a]);
		}

	}

}
