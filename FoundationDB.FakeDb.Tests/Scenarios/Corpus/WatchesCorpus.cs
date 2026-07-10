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

	/// <summary>Watches campaign corpus (design spec §7.1): where watch folklore is uncertain, the recorded real-cluster behavior IS the spec, and FakeDb is pinned to it.</summary>
	public static class WatchesCorpus
	{

		/// <summary>All the scenarios of the watches campaign.</summary>
		public static IEnumerable<Scenario> All()
		{
			yield return IdenticalValueWrite();
			yield return AbaSingleCommit();
			yield return AbaTwoCommits();
			yield return OwnTxModifiedKey();
			yield return TxDisposedWithoutCommit();
			yield return TxCommitConflict();
			yield return TxReset();
			yield return VsClear();
			yield return VsClearRange();
			yield return VsAtomic();
			yield return VsVersionstampedWrite();
			yield return NonexistentCreateThenDelete();
			yield return TwoWatchesOneKey();
			yield return MetadataVersion();
		}

		private static Scenario IdenticalValueWrite()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			b.Set("A", "k1", "same");
			b.Commit("A");
			b.Begin("A");
			int w = b.Watch("A", "k1");
			b.Commit("A");
			b.Begin("B");
			b.Set("B", "k1", "same");          // writes the value the watch already sees
			b.Commit("B");
			b.ExpectPending(w, ScenarioTolerance.AllowSpuriousWatchFire); // the contract permits a spurious fire here
			return b.Build("watch_identical_value_write", "writing the identical value: does the watch fire? (contractually it may, spurious fires tolerated)");
		}

		private static Scenario AbaSingleCommit()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			b.Set("A", "k1", "a");
			b.Commit("A");
			b.Begin("A");
			int w = b.Watch("A", "k1");
			b.Commit("A");
			b.Begin("B");
			b.Set("B", "k1", "b");
			b.Set("B", "k1", "a");             // back to the baseline within ONE commit
			b.Commit("B");
			b.ExpectPending(w, ScenarioTolerance.AllowSpuriousWatchFire);
			return b.Build("watch_aba_single_commit", "ABA within one commit: the committed value equals the watch baseline");
		}

		private static Scenario AbaTwoCommits()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			b.Set("A", "k1", "a");
			b.Commit("A");
			b.Begin("A");
			int w = b.Watch("A", "k1");
			b.Commit("A");
			b.Begin("B");
			b.Set("B", "k1", "b");
			b.Commit("B");                     // the first commit really changes the value: the watch must fire...
			b.ExpectFired(w);
			b.Begin("B");
			b.Set("B", "k1", "a");             // ...even though a later commit restores the baseline
			b.Commit("B");
			b.Begin("A");
			b.Get("A", "k1");
			b.Commit("A");
			return b.Build("watch_aba_two_commits", "ABA across two commits: the intermediate change fires the watch");
		}

		private static Scenario OwnTxModifiedKey()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			b.Set("A", "k1", "mine");
			int w = b.Watch("A", "k1");        // watch created AFTER the same transaction wrote the key
			b.Commit("A");
			b.ExpectPending(w, ScenarioTolerance.AllowSpuriousWatchFire); // baseline should be the transaction's own value
			b.Begin("B");
			b.Set("B", "k1", "other");
			b.Commit("B");
			b.ExpectFired(w);
			return b.Build("watch_own_tx_modified_key", "read-your-writes baseline: watching a key the same transaction modified");
		}

		private static Scenario TxDisposedWithoutCommit()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			int w = b.Watch("A", "k1");
			b.Dispose("A");                    // the creating transaction never commits
			b.ExpectPending(w);                // observation: cancelled/error or still pending?
			return b.Build("watch_tx_disposed_without_commit", "a watch whose creating transaction is disposed without committing");
		}

		private static Scenario TxCommitConflict()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			b.Get("A", "k1");                  // A reads k1...
			int w = b.Watch("A", "k2");        // ...and watches k2
			b.Begin("B");
			b.Set("B", "k1", "b1");
			b.Commit("B");                     // ...B invalidates A's read
			b.Set("A", "k3", "a1");
			b.Commit("A");                     // A's commit fails with a conflict
			b.ExpectPending(w);                // observation: what happens to the watch of a conflicted transaction?
			return b.Build("watch_tx_commit_conflict", "a watch whose creating transaction fails to commit with a conflict");
		}

		private static Scenario TxReset()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			int w = b.Watch("A", "k1");
			b.Reset("A");                      // reset wipes the transaction state
			b.Commit("A");                     // committing the (now blank) transaction
			b.ExpectPending(w);                // observation: cancelled/error or still pending?
			b.Dispose("A");
			return b.Build("watch_tx_reset", "a watch created before the transaction is reset");
		}

		private static Scenario VsClear()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			b.Set("A", "k1", "v");
			b.Commit("A");
			b.Begin("A");
			int w = b.Watch("A", "k1");
			b.Commit("A");
			b.Begin("B");
			b.Clear("B", "k1");
			b.Commit("B");
			b.ExpectFired(w);
			b.Begin("A");
			b.Get("A", "k1");                  // post-fire read observes the deletion
			b.Commit("A");
			return b.Build("watch_vs_clear", "clearing the watched key fires the watch");
		}

		private static Scenario VsClearRange()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			b.Set("A", "k1", "v");
			b.Commit("A");
			b.Begin("A");
			int w = b.Watch("A", "k1");
			b.Commit("A");
			b.Begin("B");
			b.ClearRange("B", "k", "k9");      // the range covers the watched key
			b.Commit("B");
			b.ExpectFired(w);
			return b.Build("watch_vs_clearrange", "a cleared range covering the watched key fires the watch");
		}

		private static Scenario VsAtomic()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			b.Atomic("A", "counter", Slice.FromFixed64(1), FdbMutationType.Add);
			b.Commit("A");
			b.Begin("A");
			int w = b.Watch("A", "counter");
			b.Commit("A");
			b.Begin("B");
			b.Atomic("B", "counter", Slice.FromFixed64(1), FdbMutationType.Add);
			b.Commit("B");
			b.ExpectFired(w);
			return b.Build("watch_vs_atomic", "an atomic mutation of the watched key fires the watch");
		}

		private static Scenario VsVersionstampedWrite()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			int w = b.Watch("A", "k1");
			b.Commit("A");
			b.Begin("B");
			b.SetVersionstampedValue("B", "k1", Slice.Zero(10), 0);
			int vs = b.GetVersionstamp("B");
			b.Commit("B");
			b.ExpectVersionstamp(vs);          // register the stamp so the fired value renders symbolized
			b.ExpectFired(w);
			b.Begin("A");
			b.Get("A", "k1");                  // the stamped value, symbolized
			b.Commit("A");
			return b.Build("watch_vs_versionstamped_write", "a versionstamped write to the watched key fires the watch");
		}

		private static Scenario NonexistentCreateThenDelete()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			int w1 = b.Watch("A", "k1");       // watching a key that does not exist (nil baseline)
			b.Commit("A");
			b.ExpectPending(w1, ScenarioTolerance.AllowSpuriousWatchFire);
			b.Begin("B");
			b.Set("B", "k1", "born");          // creation fires
			b.Commit("B");
			b.ExpectFired(w1);
			b.Begin("A");
			int w2 = b.Watch("A", "k1");       // new watch on the now-existing key
			b.Commit("A");
			b.Begin("B");
			b.Clear("B", "k1");                // deletion fires
			b.Commit("B");
			b.ExpectFired(w2);
			return b.Build("watch_nonexistent_create_then_delete", "watch on a non-existent key: creation fires, then a new watch sees the deletion fire");
		}

		private static Scenario TwoWatchesOneKey()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			int w1 = b.Watch("A", "k1");
			b.Commit("A");
			b.Begin("B");
			int w2 = b.Watch("B", "k1");       // second watch on the same key, from another actor
			b.Commit("B");
			b.Begin("C");
			b.Set("C", "k1", "boom");
			b.Commit("C");
			b.ExpectFired(w1);
			b.ExpectFired(w2);                 // both must fire
			return b.Build("watch_two_watches_one_key", "multiple watches on one key all fire on a single change");
		}

		private static Scenario MetadataVersion()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			int w = b.WatchMetadataVersion("A");
			b.Commit("A");
			b.ExpectPending(w, ScenarioTolerance.AllowSpuriousWatchFire);
			b.Begin("B");
			b.TouchMetadataVersion("B");
			b.Commit("B");
			b.ExpectFired(w);
			b.Begin("A");
			b.GetMetadataVersion("A");         // the bumped version, symbolized
			b.Commit("A");
			return b.Build("watch_metadataversion", "watching the global metadata-version key: a touch by another transaction fires it");
		}

	}

}
