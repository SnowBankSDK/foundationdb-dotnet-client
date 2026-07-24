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

		/// <summary>Counter keys, mutated ONLY by 8-byte atomic adds and clears so their exact value stays modelable; they sort below every <see cref="Keys"/> entry, so a generated ClearRange never covers them.</summary>
		private static readonly string[] CounterKeys = [ "ctr0", "ctr1" ];

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

		/// <summary>Generates a deterministic watch-lifecycle fuzz scenario for the given seed: owner transactions arm watches (on plain keys, atomic counters, or the global metadata version) while a blind writer races commits across them, pinning arm/fire/pending/cancel interleavings.</summary>
		/// <remarks>
		/// <para>Determinism rules: every committer is BLIND (owners never read, the writer never reads), so every commit deterministically succeeds and the generator tracks the exact committed value of every slot: plain keys hold opaque strings, counter keys are mutated only by 8-byte atomic adds and clears (absent counts as zero), and the metadata version is symbolic and monotone (versionstamped: a touch always changes it, a revert is impossible). A watch's baseline is the committed state at its owner's first key-using step (the read-version pin), so a slot that changed between pin and arm is expected to fire immediately.</para>
		/// <para>Fired expectations are strict only while the endpoint value at observation still differs from the baseline; a fire whose slot reverted to the baseline is contract-undefined on a real cluster (the changes-and-changes-back caveat) and is observed as a tolerant pending instead. Pending expectations carry <see cref="ScenarioTolerance.AllowSpuriousWatchFire"/> (the fdb contract permits spurious fires), and at most one grace-paying observation is emitted per scenario. The conflicted-commit settle is pinned by the hand-written corpus, not generated (it would need a read, breaking blind-commit tracking).</para>
		/// </remarks>
		public static Scenario GenerateWatchFuzz(int seed)
		{
			var rnd = new Random(seed);
			var b = new ScenarioBuilder();

			// the tracked committed state (null = absent), one slot per watchable target: the plain keys,
			// then the counter keys (values encoded "#<long>"), then the global metadata version (symbolic
			// "mv<n>": versionstamped, so every touch is a fresh value and a revert is impossible)
			int counterBase = Keys.Length;
			int metadataIndex = counterBase + CounterKeys.Length;
			var committed = new string?[metadataIndex + 1];
			committed[metadataIndex] = "mv0";
			int mvTouches = 0;
			string KeyName(int k) => k < counterBase ? Keys[k] : CounterKeys[k - counterBase];

			// phase 1: seed some committed values (blind: cannot fail)
			b.Begin("W");
			for (int i = 0; i < Keys.Length; i++)
			{
				if (rnd.Next(2) == 0)
				{
					string v = "c" + rnd.Next(10);
					b.Set("W", Keys[i], v);
					committed[i] = v;
				}
			}
			b.Commit("W");

			// the writer's open-transaction staged writes (last write wins per key)
			bool writerOpen = false;
			var staged = new string?[committed.Length];
			var stagedTouched = new bool[committed.Length];

			// owner actors and their armed watches
			string[] owners = [ "A", "B" ];
			var ownerOpen = new bool[owners.Length];
			var ownerPin = new string?[owners.Length][]; // committed state at the owner's read-version pin (null until pinned)
			var ownerWatchCount = new int[owners.Length];

			var watches = new List<(int Handle, int KeyIndex, string? Baseline, int Owner, bool Armed, bool Cancelled, bool Fired)>();

			int ops = rnd.Next(30, 46);
			for (int i = 0; i < ops; i++)
			{
				switch (rnd.Next(100))
				{
					case < 45:
					{ // the writer: stage blind mutations and commit them
						if (!writerOpen)
						{
							b.Begin("W");
							writerOpen = true;
							Array.Clear(stagedTouched);
							break;
						}
						switch (rnd.Next(14))
						{
							case < 4:
							{
								int k = rnd.Next(Keys.Length);
								string v = "v" + rnd.Next(10);
								b.Set("W", Keys[k], v);
								staged[k] = v;
								stagedTouched[k] = true;
								break;
							}
							case < 6:
							{
								int k = rnd.Next(Keys.Length);
								b.Clear("W", Keys[k]);
								staged[k] = null;
								stagedTouched[k] = true;
								break;
							}
							case < 7:
							{
								var (lo, hi) = PickOrderedIndexPair(rnd);
								b.ClearRange("W", Keys[lo], Keys[hi]);
								for (int k = lo; k < hi; k++)
								{
									staged[k] = null;
									stagedTouched[k] = true;
								}
								break;
							}
							case < 9:
							{ // atomic add on a counter (absent counts as zero; 8-byte little-endian wrap, exactly what both heads store)
								int k = counterBase + rnd.Next(CounterKeys.Length);
								long operand = rnd.Next(-9, 10);
								b.Atomic("W", KeyName(k), Slice.FromFixed64(operand), FdbMutationType.Add);
								string? current = stagedTouched[k] ? staged[k] : committed[k];
								staged[k] = "#" + unchecked((current is null ? 0L : long.Parse(current[1..])) + operand);
								stagedTouched[k] = true;
								break;
							}
							case < 10:
							{
								int k = counterBase + rnd.Next(CounterKeys.Length);
								b.Clear("W", KeyName(k));
								staged[k] = null;
								stagedTouched[k] = true;
								break;
							}
							case < 11:
							{ // versionstamped: every touch is a fresh value, so a commit with a touch always changes the slot
								b.TouchMetadataVersion("W");
								staged[metadataIndex] = "mv" + (++mvTouches);
								stagedTouched[metadataIndex] = true;
								break;
							}
							default:
							{ // commit: publish the staged writes, latch the fire flag of every armed watch whose key changed
								b.Commit("W");
								writerOpen = false;
								for (int k = 0; k < committed.Length; k++)
								{
									if (!stagedTouched[k] || committed[k] == staged[k]) continue;
									committed[k] = staged[k];
									for (int w = 0; w < watches.Count; w++)
									{
										var entry = watches[w];
										if (entry.Armed && !entry.Cancelled && entry.KeyIndex == k)
										{
											watches[w] = entry with { Fired = true };
										}
									}
								}
								break;
							}
						}
						break;
					}
					default:
					{ // an owner: arm watches, then commit (arm) or dispose (cancel)
						int o = rnd.Next(owners.Length);
						string owner = owners[o];
						if (!ownerOpen[o])
						{
							b.Begin(owner);
							ownerOpen[o] = true;
							ownerPin[o] = null;
							ownerWatchCount[o] = 0;
							break;
						}
						if (ownerWatchCount[o] < 3 && rnd.Next(3) != 0)
						{ // arm one more watch (plain key, counter, or the metadata version); the first key-using step pins the owner's read version
							int k = rnd.Next(committed.Length);
							ownerPin[o] ??= (string?[]) committed.Clone();
							int handle = k == metadataIndex ? b.WatchMetadataVersion(owner) : b.Watch(owner, KeyName(k));
							watches.Add((handle, k, ownerPin[o][k], o, Armed: false, Cancelled: false, Fired: false));
							ownerWatchCount[o]++;
							break;
						}
						// fate: commit arms this owner's watches (a blind commit always succeeds), dispose cancels them
						bool dispose = rnd.Next(4) == 0;
						if (dispose)
						{
							b.Dispose(owner);
						}
						else
						{
							b.Commit(owner);
						}
						ownerOpen[o] = false;
						for (int w = 0; w < watches.Count; w++)
						{
							var entry = watches[w];
							if (entry.Owner != o || entry.Armed || entry.Cancelled) continue;
							if (dispose)
							{
								watches[w] = entry with { Cancelled = true };
							}
							else
							{ // armed; a key that already moved between the pin and now fires immediately
								watches[w] = entry with { Armed = true, Fired = entry.Baseline != committed[entry.KeyIndex] };
							}
						}
						break;
					}
				}
			}

			// epilogue: publish any staged writes, arm the leftover owners, then observe every watch
			if (writerOpen)
			{
				b.Commit("W");
				for (int k = 0; k < committed.Length; k++)
				{
					if (!stagedTouched[k] || committed[k] == staged[k]) continue;
					committed[k] = staged[k];
					for (int w = 0; w < watches.Count; w++)
					{
						var entry = watches[w];
						if (entry.Armed && !entry.Cancelled && entry.KeyIndex == k)
						{
							watches[w] = entry with { Fired = true };
						}
					}
				}
			}
			for (int o = 0; o < owners.Length; o++)
			{
				if (!ownerOpen[o]) continue;
				b.Commit(owners[o]);
				for (int w = 0; w < watches.Count; w++)
				{
					var entry = watches[w];
					if (entry.Owner != o || entry.Armed || entry.Cancelled) continue;
					watches[w] = entry with { Armed = true, Fired = entry.Baseline != committed[entry.KeyIndex] };
				}
			}

			// observations: strict for cancellations and fires whose key still differs from the baseline; a fire
			// whose key REVERTED to the baseline before observation is undefined on a real cluster (a watch may
			// miss a change that was undone before the watch machinery looked at it), so it can only be observed
			// with the spurious tolerance; at most ONE grace-paying observation (clean pending or reverted fire)
			bool pendingObserved = false;
			foreach (var entry in watches)
			{
				bool reverted = entry.Fired && !entry.Cancelled && committed[entry.KeyIndex] == entry.Baseline;
				if (entry.Cancelled || (entry.Fired && !reverted))
				{ // settled: a cancellation surfaces its error through the same observation step
					b.ExpectFired(entry.Handle);
				}
				else if (!pendingObserved)
				{
					b.ExpectPending(entry.Handle, ScenarioTolerance.AllowSpuriousWatchFire);
					pendingObserved = true;
				}
			}

			return b.Build($"fuzz_wch_{seed:D4}", $"generated watch-lifecycle fuzz (seed {seed})");
		}

		private static (int Lo, int Hi) PickOrderedIndexPair(Random rnd)
		{
			int a = rnd.Next(Keys.Length), c = rnd.Next(Keys.Length);
			return a == c ? (a, Math.Min(a + 1, Keys.Length - 1)) : a < c ? (a, c) : (c, a);
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
