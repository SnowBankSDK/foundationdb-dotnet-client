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

		/// <summary>Generates a deterministic selector/boundary stress scenario for the given seed: two actors mix wide-offset key-selector probes and selector-bounded range reads with mid-key mutations, racing commits for conflict coverage.</summary>
		/// <remarks>
		/// <para>Containment invariant: the scenario runs inside a shared cluster, where a selector walk that escapes the
		/// test partition resolves into NEIGHBOR partitions' keys - comparable for a bare GetKey (the runner renders any
		/// out-of-partition resolution as the outside marker) but fatally incomparable for a range scan (the real backend
		/// would return neighbor data that FakeDb does not have). The generator therefore seeds an always-committed FLOOR
		/// run (<c>!0..!3</c>) and CEILING run (<c>~0..~3</c>) that are never mutated afterwards, keeps every selector
		/// pivot strictly between the two runs, and bounds every offset to the run length (|offset| &lt;= 4): any walk is
		/// absorbed inside the sentinels, so resolutions and scans stay in-partition by construction. The true
		/// end-of-keyspace clamp (FDBV-018) is pinned by the conformance facts, not fuzzed here.</para>
		/// <para>Selector probes go snapshot in a transaction with pending writes (the merged-view conflict ranges are
		/// client-implementation-specific, the family-1 rule), and range reads order their pivots so only the RESOLUTIONS
		/// can invert (which legally yields an empty read on both backends).</para>
		/// </remarks>
		public static Scenario GenerateSelectorFuzz(int seed)
		{
			var rnd = new Random(seed);
			var b = new ScenarioBuilder();

			// phase 1: the sentinel runs (always, never touched again) plus a sparse middle
			b.Begin("A");
			for (int i = 0; i < 4; i++)
			{
				b.Set("A", "!" + i, "s" + i);
				b.Set("A", "~" + i, "s" + i);
			}
			for (int i = 0; i < Keys.Length; i++)
			{
				if (rnd.Next(2) == 0)
				{
					b.Set("A", Keys[i], "c" + rnd.Next(10));
				}
			}
			b.Commit("A");

			// phase 2: two actors race probes and mid-key mutations
			string[] actors = [ "A", "B" ];
			var open = new bool[actors.Length];
			var canLoseCommit = new bool[actors.Length];
			var hasWrites = new bool[actors.Length];

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
					{ // must precede any read (a later opt-in poisons the transaction)
						b.SetOption(actor, ScenarioTransactionOption.ReadYourWritesDisable);
					}
					continue;
				}
				switch (rnd.Next(100))
				{
					case < 10:
					{
						b.Set(actor, PickKey(rnd), "v" + rnd.Next(10));
						hasWrites[a] = true;
						break;
					}
					case < 16:
					{
						b.Clear(actor, PickKey(rnd));
						hasWrites[a] = true;
						break;
					}
					case < 22:
					{
						var (lo, hi) = PickOrderedPair(rnd);
						b.ClearRange(actor, lo, hi);
						hasWrites[a] = true;
						break;
					}
					case < 60:
					{ // wide-offset selector probe (snapshot when the merged view would resolve it: the family-1 rule)
						bool snapshot = rnd.Next(4) == 0 || hasWrites[a]; // draw first: the RNG stream must not depend on the actor's state
						var (pivot, offset) = PickSelectorProbe(rnd);
						b.GetKey(actor, new ScenarioSelector(Slice.FromStringAscii(pivot), OrEqual: rnd.Next(2) == 0, Offset: offset), snapshot);
						if (!snapshot) canLoseCommit[a] = true;
						break;
					}
					case < 85:
					{ // selector-bounded range read; ordered pivots, but offsets can still invert the resolutions (legal empty read)
						var (p1, o1) = PickSelectorProbe(rnd);
						var (p2, o2) = PickSelectorProbe(rnd);
						bool swap = string.CompareOrdinal(p1, p2) > 0;
						string lo = swap ? p2 : p1, hi = swap ? p1 : p2;
						// the BEGIN bound clamps to -3: a backward walk starts at the base (the last key at/below the
						// pivot, at worst the floor-run top !3), so only three absorbing keys sit below it - a fourth
						// step exits the partition and the scan would read neighbor data on a shared cluster. An END
						// bound below the partition only inverts the range (legal empty), and a bare GetKey renders
						// as the outside marker, so both keep the full envelope.
						int lofs = Math.Max(swap ? o2 : o1, -3), hofs = swap ? o1 : o2;
						bool snapshot = rnd.Next(4) == 0 || hasWrites[a];
						b.GetRange(actor,
							new ScenarioSelector(Slice.FromStringAscii(lo), OrEqual: rnd.Next(2) == 0, Offset: lofs),
							new ScenarioSelector(Slice.FromStringAscii(hi), OrEqual: rnd.Next(2) == 0, Offset: hofs),
							limit: rnd.Next(3) == 0 ? rnd.Next(1, 4) : null,
							reverse: rnd.Next(3) == 0,
							snapshot: snapshot);
						if (!snapshot) canLoseCommit[a] = true;
						break;
					}
					case < 95:
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

			return b.Build($"fuzz_sel_{seed:D4}", $"generated selector/boundary stress fuzz (seed {seed})");
		}

		/// <summary>Generates a deterministic versionstamp/atomics/clear-range fuzz scenario for the given seed: two BLIND writers race stamped keys/values, the full atomic vocabulary and clear-ranges over shared pools while a read-only observer samples the evolving state between commits.</summary>
		/// <remarks>
		/// <para>Determinism rules: writers never read and the observer never writes, so every commit deterministically succeeds (the watch-family pattern) and the interleaving is fully scripted. Absolute stamp bytes never match across backends, so comparability rides the symbolic stamp rendering: a commit that wrote stamped data ALWAYS requests its versionstamp and observes it immediately after the commit - the observation registers the stamp's byte pattern under a first-appearance symbol BEFORE any read can return those bytes, which keeps symbol assignment identical on both backends (first appearance = commit order; same-transaction stamps share the symbol and are disambiguated by their literal batch-order and user-version bytes).</para>
		/// <para>Key pools: stamped keys live under their own prefixes ("evt-" tuple-style suffix stamps with user versions, "log-" mid-key stamps), sorting outside the plain pool on either side, so plain-pool clear-ranges never cover them; dedicated prefix wipes exercise clear-over-stamped explicitly. Atomics use per-type operand shapes (mixed-width adds, bit masks, numeric and byte-string min/max, append, compare-and-clear against plausible and impossible values) on the plain pool, where sets, clears and clear-ranges land on the same keys.</para>
		/// </remarks>
		public static Scenario GenerateVersionstampAtomicsFuzz(int seed)
		{
			var rnd = new Random(seed);
			var b = new ScenarioBuilder();

			// phase 1: seed some committed plain state (blind: cannot fail)
			b.Begin("A");
			int seeded = rnd.Next(0, 6);
			for (int i = 0; i < seeded; i++)
			{
				b.Set("A", PickKey(rnd), "c" + rnd.Next(10));
			}
			b.Commit("A");

			// phase 2: two blind writers and one read-only observer race over the pools
			string[] actors = [ "A", "B", "R" ];
			var open = new bool[actors.Length];
			var hasStamped = new bool[actors.Length];

			int ops = rnd.Next(25, 41);
			for (int i = 0; i < ops; i++)
			{
				int a = rnd.Next(actors.Length);
				string actor = actors[a];
				if (!open[a])
				{
					b.Begin(actor);
					open[a] = true;
					hasStamped[a] = false;
					continue;
				}
				if (a == 2)
				{ // the observer: reads only, so its commit is a no-op that always succeeds
					switch (rnd.Next(100))
					{
						case < 35:
						{
							b.Get("R", rnd.Next(4) == 0 ? "m" + rnd.Next(2) : PickKey(rnd), snapshot: rnd.Next(4) == 0);
							break;
						}
						case < 60:
						{
							var (lo, hi) = PickOrderedPair(rnd);
							b.GetRange("R",
								new ScenarioSelector(Slice.FromStringAscii(lo), OrEqual: false, Offset: 1),
								new ScenarioSelector(Slice.FromStringAscii(hi), OrEqual: rnd.Next(2) == 0, Offset: 1),
								limit: rnd.Next(3) == 0 ? rnd.Next(1, 4) : null,
								reverse: rnd.Next(3) == 0,
								snapshot: rnd.Next(4) == 0);
							break;
						}
						case < 90:
						{ // sweep a stamped prefix: every returned key renders through the stamp symbol table
							string p = rnd.Next(2) == 0 ? "evt" : "log";
							b.GetRange("R",
								new ScenarioSelector(Slice.FromStringAscii(p + "-"), OrEqual: false, Offset: 1),
								new ScenarioSelector(Slice.FromStringAscii(p + "."), OrEqual: false, Offset: 1),
								limit: rnd.Next(4) == 0 ? rnd.Next(1, 4) : null,
								reverse: rnd.Next(3) == 0,
								snapshot: rnd.Next(4) == 0);
							break;
						}
						default:
						{
							b.Commit("R");
							open[a] = false;
							break;
						}
					}
					continue;
				}
				switch (rnd.Next(100))
				{
					case < 12:
					{
						b.Set(actor, PickKey(rnd), "v" + rnd.Next(10));
						break;
					}
					case < 19:
					{
						b.Clear(actor, PickKey(rnd));
						break;
					}
					case < 26:
					{
						var (lo, hi) = PickOrderedPair(rnd);
						b.ClearRange(actor, lo, hi);
						break;
					}
					case < 32:
					{ // wipe a stamped prefix: committed stamped keys must vanish on both backends
						string p = rnd.Next(2) == 0 ? "evt" : "log";
						b.ClearRange(actor, p + "-", p + ".");
						break;
					}
					case < 50:
					{
						var (param, mutation) = PickAtomicOp(rnd);
						b.Atomic(actor, PickKey(rnd), param, mutation);
						break;
					}
					case < 56:
					{ // compare-and-clear against a value the key may plausibly hold (hit and miss both matter)
						var operand = rnd.Next(3) switch
						{
							0 => Slice.FromStringAscii("v" + rnd.Next(10)),
							1 => Slice.FromStringAscii("c" + rnd.Next(10)),
							_ => Slice.Empty, // clear-if-absent-or-empty
						};
						b.Atomic(actor, PickKey(rnd), operand, FdbMutationType.CompareAndClear);
						break;
					}
					case < 66:
					{ // tuple-style suffix stamp with a user version: the 2-byte suffix orders same-transaction keys
						b.SetVersionstampedKey(actor, Slice.FromStringAscii("evt-") + VersionStamp.Incomplete(rnd.Next(0, 3)).ToSlice(), 4, "e" + rnd.Next(10));
						hasStamped[a] = true;
						break;
					}
					case < 72:
					{ // mid-key stamp: the placeholder sits between prefix and tail
						b.SetVersionstampedKey(actor, Slice.FromStringAscii("log-") + Slice.Zero(10) + Slice.FromStringAscii("-" + rnd.Next(3)), 4, "l" + rnd.Next(10));
						hasStamped[a] = true;
						break;
					}
					case < 78:
					{
						b.SetVersionstampedValue(actor, "m" + rnd.Next(2), Slice.FromStringAscii("ver=") + Slice.Zero(10), 4);
						hasStamped[a] = true;
						break;
					}
					case < 94:
					{
						bool probe = rnd.Next(2) == 0; // draw first: the RNG stream must not depend on the branch taken
						if (hasStamped[a])
						{ // mandatory observation: register the stamp symbol before any read can return the stamped bytes
							int vs = b.GetVersionstamp(actor);
							b.Commit(actor);
							b.ExpectVersionstamp(vs);
						}
						else
						{
							b.Commit(actor);
							if (probe)
							{ // blind commit: always safe to probe
								b.GetCommittedVersion(actor);
							}
						}
						open[a] = false;
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

			// epilogue: settle every remaining transaction so the final state (and every pending stamp) is fully determined
			for (int a = 0; a < actors.Length; a++)
			{
				if (!open[a]) continue;
				if (hasStamped[a])
				{
					int vs = b.GetVersionstamp(actors[a]);
					b.Commit(actors[a]);
					b.ExpectVersionstamp(vs);
				}
				else
				{
					b.Commit(actors[a]);
				}
			}

			return b.Build($"fuzz_vsa_{seed:D4}", $"generated versionstamp/atomics/clear-range fuzz (seed {seed})");
		}

		/// <summary>Generates a deterministic read-write-atomics fuzz scenario for the given seed: two READ-WRITE actors each mutate a pool key with an own atomic - CompareAndClear (whose effect on key PRESENCE depends on the committed value) or a plain atomic (which always leaves the key present) - then resolve a non-snapshot selector or range read THROUGH the pool, then commit; a blind peer races sets, clears and clear-ranges over the same pool so its commits can land inside a reader's conflict window.</summary>
		/// <remarks>
		/// <para>This is family-4 round two: round one never had an actor both read the merged view AND commit, so the merged-path read-conflict machinery for own atomics (<c>MarkMergedRangeReadConflict(atomicsAreLocal: true)</c>) was never put under a conflict outcome. Here a read-write actor's own CompareAndClear makes the resolved key set depend on the committed value, so a peer write to that key must conflict the read - the discriminator is a plain atomic (Add and friends) at the same position, whose presence is locally determined, so a peer write there must NOT conflict. The commit outcome (committed vs NotCommitted) is what the dual-live differential compares; the emulator differential is blind to it because both engines share the overlay, which is the dual-live oracle's reason to exist.</para>
		/// <para>Containment: the sentinel runs (<c>!0..!3</c> floor, <c>~0..~3</c> ceiling, committed once and never touched) plus the begin-bound floor clamp keep every selector and range read inside the partition, the same envelope the selector family established. This family stays focused on the merged-path conflict outcome; the read-of-own-pending-stamp semantics (unreadable, fdb error 1036) are covered by a dedicated conformance fact rather than fuzzed, since an unreadable read kills the transaction and would cascade over the rest of a scenario.</para>
		/// </remarks>
		public static Scenario GenerateReadWriteAtomicsFuzz(int seed)
		{
			var rnd = new Random(seed);
			var b = new ScenarioBuilder();

			// phase 1: sentinel runs (containment) plus a sparse committed middle whose values a CompareAndClear can hit or miss
			b.Begin("A");
			for (int i = 0; i < 4; i++)
			{
				b.Set("A", "!" + i, "s" + i);
				b.Set("A", "~" + i, "s" + i);
			}
			for (int i = 0; i < Keys.Length; i++)
			{
				if (rnd.Next(2) == 0)
				{
					b.Set("A", Keys[i], "c" + rnd.Next(10));
				}
			}
			b.Commit("A");

			// phase 2: two read-write actors (W1, W2) and a blind peer (P) that races their keys
			string[] actors = [ "W1", "W2", "P" ];
			var open = new bool[actors.Length];
			var canLoseCommit = new bool[actors.Length];
			var hasWrites = new bool[actors.Length];

			int ops = rnd.Next(28, 46);
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
					continue;
				}

				if (a == 2)
				{ // the peer: a blind writer, so its commit never loses a conflict and is a clean write-version source
					switch (rnd.Next(100))
					{
						case < 42:
						{
							b.Set("P", PickKey(rnd), "p" + rnd.Next(10));
							break;
						}
						case < 62:
						{
							b.Clear("P", PickKey(rnd));
							break;
						}
						case < 78:
						{
							var (lo, hi) = PickOrderedPair(rnd);
							b.ClearRange("P", lo, hi);
							break;
						}
						default:
						{
							b.Commit("P");
							open[a] = false;
							break;
						}
					}
					continue;
				}

				// a read-write actor
				switch (rnd.Next(100))
				{
					case < 20:
					{ // own compare-and-clear: the key's PRESENCE becomes committed-value-dependent (the hazard)
						var operand = rnd.Next(3) switch
						{
							0 => Slice.FromStringAscii("c" + rnd.Next(10)), // plausible hit against a phase-1 value
							1 => Slice.FromStringAscii("z" + rnd.Next(10)), // implausible miss
							_ => Slice.Empty,                                // clear-if-empty
						};
						b.Atomic(actor, PickKey(rnd), operand, FdbMutationType.CompareAndClear);
						hasWrites[a] = true;
						break;
					}
					case < 32:
					{ // own plain atomic: the discriminator - always leaves the key present, so presence is local
						var (param, mutation) = PickAtomicOp(rnd);
						b.Atomic(actor, PickKey(rnd), param, mutation);
						hasWrites[a] = true;
						break;
					}
					case < 40:
					{
						b.Set(actor, PickKey(rnd), "v" + rnd.Next(10));
						hasWrites[a] = true;
						break;
					}
					case < 46:
					{
						b.Clear(actor, PickKey(rnd));
						hasWrites[a] = true;
						break;
					}
					case < 68:
					{ // selector read resolving through the pool; NON-snapshot so it can lose a conflict
						bool snapshot = rnd.Next(5) == 0; // draw first: the RNG stream must not depend on the actor's state
						var (pivot, offset) = PickSelectorProbe(rnd);
						b.GetKey(actor, new ScenarioSelector(Slice.FromStringAscii(pivot), OrEqual: rnd.Next(2) == 0, Offset: offset), snapshot);
						if (!snapshot) canLoseCommit[a] = true;
						break;
					}
					case < 90:
					{ // selector-bounded range read over the pool; the returned set depends on which keys exist
						bool snapshot = rnd.Next(5) == 0;
						var (p1, o1) = PickSelectorProbe(rnd);
						var (p2, o2) = PickSelectorProbe(rnd);
						bool swap = string.CompareOrdinal(p1, p2) > 0;
						string lo = swap ? p2 : p1, hi = swap ? p1 : p2;
						int lofs = Math.Max(swap ? o2 : o1, -3), hofs = swap ? o1 : o2; // the begin-bound floor clamp keeps the walk inside the partition
						b.GetRange(actor,
							new ScenarioSelector(Slice.FromStringAscii(lo), OrEqual: rnd.Next(2) == 0, Offset: lofs),
							new ScenarioSelector(Slice.FromStringAscii(hi), OrEqual: rnd.Next(2) == 0, Offset: hofs),
							limit: rnd.Next(3) == 0 ? rnd.Next(1, 4) : null,
							reverse: rnd.Next(3) == 0,
							snapshot: snapshot);
						if (!snapshot) canLoseCommit[a] = true;
						break;
					}
					case < 97:
					{ // settle: plain commit (a conflict-free commit succeeds; a canLoseCommit read makes NotCommitted possible)
						b.Commit(actor);
						if (!canLoseCommit[a] && rnd.Next(2) == 0)
						{ // a conflict-free commit always succeeds, so the transaction is still there to probe
							b.GetCommittedVersion(actor);
						}
						open[a] = false;
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
				if (open[a]) b.Commit(actors[a]);
			}

			return b.Build($"fuzz_rwa_{seed:D4}", $"generated read-write atomics (compare-and-clear under selector reads) fuzz (seed {seed})");
		}

		/// <summary>Picks an atomic mutation with a per-type operand shape: mixed-width adds (the operand length defines the arithmetic width), 8-byte bit masks and numeric min/max, short byte-string min/max, and append.</summary>
		private static (Slice Param, FdbMutationType Mutation) PickAtomicOp(Random rnd) => rnd.Next(10) switch
		{
			0 => (Slice.FromFixed64(rnd.Next(1, 100)), FdbMutationType.Add),
			1 => (Slice.FromFixed32(rnd.Next(1, 100)), FdbMutationType.Add),
			2 => (Slice.FromFixed64(rnd.Next(256)), FdbMutationType.BitAnd),
			3 => (Slice.FromFixed64(rnd.Next(256)), FdbMutationType.BitOr),
			4 => (Slice.FromFixed64(rnd.Next(256)), FdbMutationType.BitXor),
			5 => (Slice.FromFixed64(rnd.Next(1000)), FdbMutationType.Max),
			6 => (Slice.FromFixed64(rnd.Next(1000)), FdbMutationType.Min),
			7 => (Slice.FromStringAscii("b" + rnd.Next(10)), FdbMutationType.ByteMin),
			8 => (Slice.FromStringAscii("b" + rnd.Next(10)), FdbMutationType.ByteMax),
			_ => (Slice.FromStringAscii("+" + rnd.Next(10)), FdbMutationType.AppendIfFits),
		};

		/// <summary>Picks a selector pivot and its offset together, honoring the containment invariant: pivots between the sentinel runs get the full +-4 envelope, pivots ON a sentinel run get INWARD offsets only (the floor walks up from at least +1, the ceiling walks down from at most 0), so no walk can escape the partition.</summary>
		private static (string Pivot, int Offset) PickSelectorProbe(Random rnd) => rnd.Next(12) switch
		{
			< 6 => (Keys[rnd.Next(Keys.Length)], rnd.Next(-4, 5)),
			< 8 => (Keys[rnd.Next(Keys.Length)] + "a", rnd.Next(-4, 5)),
			8 => ("!z", rnd.Next(-4, 5)),
			9 => ("kz", rnd.Next(-4, 5)),
			10 => ("!" + rnd.Next(4), rnd.Next(1, 5)),
			_ => ("~" + rnd.Next(4), rnd.Next(-4, 1)),
		};

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
