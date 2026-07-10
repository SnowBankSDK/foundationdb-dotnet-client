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

	/// <summary>Read-your-writes campaign corpus (design spec §7.2): single-actor, fully deterministic scenarios exercising the managed RYW re-implementation below the validation seam.</summary>
	public static class RywCorpus
	{

		/// <summary>All the scenarios of the RYW campaign.</summary>
		public static IEnumerable<Scenario> All()
		{
			yield return GetAfterSetClear();
			yield return GetAfterClearRange();
			yield return GetAfterAtomic();
			yield return RangeMergeLimits();
			yield return RangeMergeReverse();
			yield return SelectorsUncommitted();
			yield return SnapshotDefaultSeesOwnWrites();
			yield return SnapshotRywDisabled();
			yield return RywDisabledTransaction();
			yield return RywDisableAfterReadPoisons();
			yield return OverlapClearRangeThenSet();
		}

		private static Scenario GetAfterSetClear()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			b.Get("A", "k1");            // absent
			b.Set("A", "k1", "v1");
			b.Get("A", "k1");            // own write visible
			b.Clear("A", "k1");
			b.Get("A", "k1");            // own clear visible
			b.Set("A", "k1", "v2");
			b.Get("A", "k1");            // last write wins
			b.Commit("A");
			b.Begin("A");
			b.Get("A", "k1");            // committed value
			b.Dispose("A");
			return b.Build("ryw_get_after_set_clear", "get observes uncommitted set, clear, and re-set within one transaction");
		}

		private static Scenario GetAfterClearRange()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			b.Set("A", "k1", "v1");
			b.Set("A", "k2", "v2");
			b.Set("A", "k3", "v3");
			b.Commit("A");
			b.Begin("A");
			b.Set("A", "k4", "v4");            // uncommitted write...
			b.ClearRange("A", "k2", "k9");     // ...swallowed by an uncommitted clear-range over committed and uncommitted keys
			b.Get("A", "k1");                  // survives (below the range)
			b.Get("A", "k2");                  // cleared (committed)
			b.Get("A", "k3");                  // cleared (committed)
			b.Get("A", "k4");                  // cleared (was uncommitted)
			b.GetRange("A",
				new ScenarioSelector(Slice.FromStringAscii("k"), OrEqual: false, Offset: 1),
				new ScenarioSelector(Slice.FromStringAscii("k9"), OrEqual: false, Offset: 1));
			b.Commit("A");
			return b.Build("ryw_get_after_clearrange", "an uncommitted clear-range hides committed keys and swallows earlier uncommitted writes");
		}

		private static Scenario GetAfterAtomic()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			b.Atomic("A", "counter", Slice.FromFixed64(1), FdbMutationType.Add);
			b.Get("A", "counter");             // 1: atomic over absent
			b.Atomic("A", "counter", Slice.FromFixed64(2), FdbMutationType.Add);
			b.Get("A", "counter");             // 3: coalesced add+add
			b.Commit("A");
			b.Begin("A");
			b.Atomic("A", "counter", Slice.FromFixed64(4), FdbMutationType.Add);
			b.Get("A", "counter");             // 7: atomic over committed
			b.Dispose("A");                    // abandoned: the +4 is discarded
			b.Begin("A");
			b.Get("A", "counter");             // 3 again
			b.Dispose("A");
			return b.Build("ryw_get_after_atomic", "get observes coalesced atomic adds over absent, uncommitted and committed values; a disposed transaction discards them");
		}

		private static Scenario RangeMergeLimits()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			b.Set("A", "k1", "c1");
			b.Set("A", "k3", "c3");
			b.Set("A", "k5", "c5");
			b.Commit("A");
			b.Begin("A");
			b.Set("A", "k2", "u2");            // uncommitted, interleaved with committed keys
			b.Set("A", "k4", "u4");
			b.GetRange("A",
				new ScenarioSelector(Slice.FromStringAscii("k1"), OrEqual: false, Offset: 1),
				new ScenarioSelector(Slice.FromStringAscii("k9"), OrEqual: false, Offset: 1),
				limit: 3);                     // merged view truncated mid-stream
			b.GetRange("A",
				new ScenarioSelector(Slice.FromStringAscii("k1"), OrEqual: false, Offset: 1),
				new ScenarioSelector(Slice.FromStringAscii("k9"), OrEqual: false, Offset: 1),
				tolerance: ScenarioTolerance.AllowConservativeHasMore); // the real client hints hasMore on merged unlimited reads
			b.Commit("A");
			return b.Build("ryw_range_merge_limits", "range reads merge uncommitted writes into committed data, honoring limits and hasMore");
		}

		private static Scenario RangeMergeReverse()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			b.Set("A", "k1", "c1");
			b.Set("A", "k3", "c3");
			b.Set("A", "k5", "c5");
			b.Commit("A");
			b.Begin("A");
			b.Set("A", "k2", "u2");
			b.Set("A", "k4", "u4");
			b.GetRange("A",
				new ScenarioSelector(Slice.FromStringAscii("k1"), OrEqual: false, Offset: 1),
				new ScenarioSelector(Slice.FromStringAscii("k9"), OrEqual: false, Offset: 1),
				limit: 3, reverse: true);      // k5, k4, k3 + hasMore
			b.Dispose("A");
			return b.Build("ryw_range_merge_reverse", "reverse range reads merge uncommitted writes, honoring limits from the high end");
		}

		private static Scenario SelectorsUncommitted()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			b.Set("A", "k1", "c1");
			b.Set("A", "k5", "c5");
			b.Commit("A");
			b.Begin("A");
			b.Set("A", "k3", "u3");            // uncommitted key between the committed ones
			b.GetKey("A", new ScenarioSelector(Slice.FromStringAscii("k2"), OrEqual: false, Offset: 1)); // FGE(k2) -> uncommitted k3
			b.GetKey("A", new ScenarioSelector(Slice.FromStringAscii("k3"), OrEqual: true, Offset: 1));  // FGT(k3) -> k5
			b.GetKey("A", new ScenarioSelector(Slice.FromStringAscii("k4"), OrEqual: true, Offset: 0));  // LLE(k4) -> uncommitted k3
			b.GetKey("A", new ScenarioSelector(Slice.FromStringAscii("k3"), OrEqual: false, Offset: 0)); // LLT(k3) -> k1
			b.Clear("A", "k5");
			b.GetKey("A", new ScenarioSelector(Slice.FromStringAscii("k3"), OrEqual: true, Offset: 1));  // FGT(k3) after clearing k5 -> outside the subspace
			b.Dispose("A");
			return b.Build("ryw_selectors_uncommitted", "key selectors resolve against the merged view: uncommitted keys are found, uncommitted clears are skipped");
		}

		private static Scenario SnapshotDefaultSeesOwnWrites()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			b.Set("A", "k1", "v1");
			b.Get("A", "k1", snapshot: true);  // default snapshot isolation still observes own writes
			b.Commit("A");
			return b.Build("ryw_snapshot_default_sees_own_writes", "snapshot reads observe the transaction's own writes by default");
		}

		private static Scenario SnapshotRywDisabled()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			b.SetOption("A", ScenarioTransactionOption.SnapshotReadYourWritesDisable);
			b.Set("A", "k1", "v1");
			b.Get("A", "k1", snapshot: true);  // snapshot RYW disabled: does NOT observe the own write
			b.Get("A", "k1");                  // regular read still does
			b.Commit("A");
			return b.Build("ryw_snapshot_ryw_disabled", "SnapshotReadYourWritesDisable makes snapshot reads bypass the transaction's own writes");
		}

		private static Scenario RywDisabledTransaction()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			b.Set("A", "k1", "old");
			b.Commit("A");
			b.Begin("A");
			b.SetOption("A", ScenarioTransactionOption.ReadYourWritesDisable);
			b.Set("A", "k1", "new");
			b.Get("A", "k1");                  // RYW disabled: reads the committed value
			b.Get("A", "missing");             // absent stays absent
			b.Commit("A");
			b.Begin("A");
			b.Get("A", "k1");                  // the write still committed
			b.Dispose("A");
			return b.Build("ryw_disabled_transaction", "ReadYourWritesDisable makes reads bypass the transaction's own writes, which still commit");
		}

		private static Scenario RywDisableAfterReadPoisons()
		{
			// discovered while authoring this corpus: on the real cluster, setting ReadYourWritesDisable
			// AFTER the transaction has performed a read leaves the transaction unusable for reads and commit
			// (writes are still accepted, but doomed). This scenario pins whatever the real cluster does.
			var b = new ScenarioBuilder();
			b.Begin("A");
			b.Set("A", "k1", "old");
			b.Commit("A");
			b.Begin("A");
			b.Get("A", "k1");                  // a read happened...
			b.SetOption("A", ScenarioTransactionOption.ReadYourWritesDisable); // ...before the option
			b.Get("A", "k1");
			b.Set("A", "k2", "doomed");
			b.Commit("A");
			b.Begin("A");
			b.Get("A", "k2");                  // did the doomed write land?
			b.Dispose("A");
			return b.Build("ryw_disable_after_read_poisons", "setting ReadYourWritesDisable after a read poisons the transaction (reads and commit fail)");
		}

		private static Scenario OverlapClearRangeThenSet()
		{
			var b = new ScenarioBuilder();
			b.Begin("A");
			b.Set("A", "k1", "c1");
			b.Set("A", "k2", "c2");
			b.Set("A", "k3", "c3");
			b.Commit("A");
			b.Begin("A");
			b.ClearRange("A", "k1", "k9");
			b.Set("A", "k2", "n2");            // re-set inside the cleared range
			b.Get("A", "k1");                  // cleared
			b.Get("A", "k2");                  // the re-set wins over the clear
			b.Get("A", "k3");                  // cleared
			b.GetRange("A",
				new ScenarioSelector(Slice.FromStringAscii("k"), OrEqual: false, Offset: 1),
				new ScenarioSelector(Slice.FromStringAscii("k9"), OrEqual: false, Offset: 1));
			b.Commit("A");
			return b.Build("ryw_overlap_clearrange_then_set", "a set inside an uncommitted cleared range resurfaces in reads, ranges and the final state");
		}

	}

}
