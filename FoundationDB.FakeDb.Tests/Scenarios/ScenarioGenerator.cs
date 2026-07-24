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

	/// <summary>Seeded random scenario generator (design spec §6.5): one method per generator family, each producing zero-nondeterminism scenarios with trivially comparable traces.</summary>
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

		/// <summary>Generates a deterministic multi-transaction interleaving fuzz scenario for the given seed: 2-3 actors race reads, writes and commits over a shared key pool, pinning conflict semantics and commit ordering.</summary>
		/// <remarks>Determinism rules (see the corpus authoring notes): no <see cref="ScenarioOp.GetReadVersion"/> probes (a real cluster's version clock advances with wall time), and <see cref="ScenarioOp.GetCommittedVersion"/> only after a commit that cannot conflict (no non-snapshot read since its Begin), so the probe never lands on a disposed transaction.</remarks>
		public static Scenario GenerateMultiTxnFuzz(int seed)
		{
			var rnd = new Random(seed);
			var b = new ScenarioBuilder();

			// phase 1: seed some committed state (blind writes: this commit cannot fail)
			b.Begin("A");
			int seeded = rnd.Next(0, 6);
			for (int i = 0; i < seeded; i++)
			{
				b.Set("A", PickKey(rnd), "c" + rnd.Next(10));
			}
			b.Commit("A");
			if (rnd.Next(2) == 0)
			{
				b.GetCommittedVersion("A");
			}

			// phase 2: the actors race over the shared key pool; the interleaving is explicit in the step order
			string[] actors = rnd.Next(3) == 0 ? [ "A", "B", "C" ] : [ "A", "B" ];
			var open = new bool[actors.Length];
			var canLoseCommit = new bool[actors.Length]; // a non-snapshot read since Begin: the commit can fail with a conflict
			var hasWrites = new bool[actors.Length]; // a pending mutation since Begin: selector probes must go snapshot (see below)

			int ops = rnd.Next(25, 41);
			for (int i = 0; i < ops; i++)
			{
				int a = rnd.Next(actors.Length);
				string actor = actors[a];
				if (!open[a])
				{
					b.Begin(actor);
					open[a] = true;
					canLoseCommit[a] = false;
					hasWrites[a] = false;
					if (rnd.Next(10) == 0)
					{ // must precede any read (a later opt-in poisons the transaction); reads then bypass the local writes and always establish conflict ranges
						b.SetOption(actor, ScenarioTransactionOption.ReadYourWritesDisable);
					}
					continue;
				}
				switch (rnd.Next(100))
				{
					case < 18:
					{
						b.Set(actor, PickKey(rnd), "v" + rnd.Next(10));
						hasWrites[a] = true;
						break;
					}
					case < 26:
					{
						b.Clear(actor, PickKey(rnd));
						hasWrites[a] = true;
						break;
					}
					case < 33:
					{
						var (lo, hi) = PickOrderedPair(rnd);
						b.ClearRange(actor, lo, hi);
						hasWrites[a] = true;
						break;
					}
					case < 41:
					{
						b.Atomic(actor, PickKey(rnd), Slice.FromFixed64(rnd.Next(1, 100)), FdbMutationType.Add);
						hasWrites[a] = true;
						break;
					}
					case < 60:
					{
						bool snapshot = rnd.Next(4) == 0;
						b.Get(actor, PickKey(rnd), snapshot);
						if (!snapshot) canLoseCommit[a] = true;
						break;
					}
					case < 68:
					{
						// a non-snapshot GetKey in a transaction with pending writes resolves over the merged view;
						// WHICH internal database reads the real client issues for that (and so its conflict ranges)
						// is client-implementation-specific, not database semantics: selector probes go snapshot there
						bool snapshot = rnd.Next(4) == 0 || hasWrites[a]; // draw first: the RNG stream must not depend on the actor's state
						b.GetKey(actor, new ScenarioSelector(Slice.FromStringAscii(PickKey(rnd)), OrEqual: rnd.Next(2) == 0, Offset: rnd.Next(-2, 3)), snapshot);
						if (!snapshot) canLoseCommit[a] = true;
						break;
					}
					case < 78:
					{
						var (lo, hi) = PickOrderedPair(rnd);
						bool snapshot = rnd.Next(4) == 0;
						b.GetRange(actor,
							new ScenarioSelector(Slice.FromStringAscii(lo), OrEqual: false, Offset: 1),
							new ScenarioSelector(Slice.FromStringAscii(hi), OrEqual: rnd.Next(2) == 0, Offset: 1),
							limit: rnd.Next(3) == 0 ? rnd.Next(1, 4) : null,
							reverse: rnd.Next(3) == 0,
							snapshot: snapshot);
						if (!snapshot) canLoseCommit[a] = true;
						break;
					}
					case < 94:
					{
						b.Commit(actor);
						open[a] = false;
						if (!canLoseCommit[a] && rnd.Next(2) == 0)
						{ // safe probe: a conflict-free commit always succeeds, so the transaction is still there
							b.GetCommittedVersion(actor);
						}
						break;
					}
					default:
					{
						b.Dispose(actor);
						open[a] = false;
						break;
					}
				}
			}

			// epilogue: settle every remaining transaction so the final state is fully determined
			for (int a = 0; a < actors.Length; a++)
			{
				if (open[a])
				{
					b.Commit(actors[a]);
				}
			}

			return b.Build($"fuzz_mtx_{seed:D4}", $"generated multi-transaction interleaving fuzz (seed {seed})");
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
