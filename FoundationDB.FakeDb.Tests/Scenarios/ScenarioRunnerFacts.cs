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
	/// <summary>Structural tests of the scenario runner, executed against the FakeDb emulator (no Docker, no native client).</summary>
	[TestFixture]
	[Category("Fdb-Scenario")]
	public class ScenarioRunnerFacts : FakeDbScenarioTest
	{

		[Test]
		public async Task Test_Runner_Records_One_Event_Per_Step()
		{
			var builder = new ScenarioBuilder();
			builder.Begin("A");
			builder.Set("A", "k1", "hello");
			builder.Get("A", "k1"); // read-your-writes: sees the uncommitted value
			builder.Atomic("A", "counter", Slice.FromFixed64(1), FdbMutationType.Add);
			builder.Commit("A");
			builder.Begin("A");
			builder.Get("A", "k1");
			builder.Get("A", "missing");
			builder.Get("A", "counter");
			builder.Commit("A");
			var scenario = builder.Build("runner_smoke");

			using var db = await OpenScenarioDatabaseAsync();
			var trace = await ScenarioRunner.RunAsync(scenario, db, this.Cancellation);
			Log(trace.ToJsonText());

			Assert.That(trace.ScenarioName, Is.EqualTo("runner_smoke"));
			Assert.That(trace.Events, Has.Count.EqualTo(scenario.Steps.Count));
			Assert.That(trace.Events.Select(e => e.Step), Is.EqualTo(Enumerable.Range(0, scenario.Steps.Count)));
			Assert.That(trace.Events.Select(e => e.Op), Is.EqualTo(scenario.Steps.Select(s => s.Op.ToString())));

			// the RYW read and the post-commit read both observe the value
			Assert.That(trace.Events[2].Outcome.Get<string?>("value", null), Is.EqualTo("hello"));
			Assert.That(trace.Events[6].Outcome.Get<string?>("value", null), Is.EqualTo("hello"));

			// a missing key reads as an explicit null
			Assert.That(trace.Events[7].Outcome.ContainsKey("value"), Is.True);
			Assert.That(trace.Events[7].Outcome["value"], Is.EqualTo(JsonNull.Null));

			// the atomic add materialized a fixed64 little-endian 1
			Assert.That(trace.Events[8].Outcome.Get<string?>("value", null), Is.EqualTo(@"\x01\x00\x00\x00\x00\x00\x00\x00"));

			// both commits succeeded (no error field)
			Assert.That(trace.Events[4].Outcome.ContainsKey("error"), Is.False);
			Assert.That(trace.Events[9].Outcome.ContainsKey("error"), Is.False);

			// the final state contains the two committed keys (in byte order), rendered relative to the scenario subspace
			Assert.That(trace.FinalState.Select(kv => kv.Key), Is.EqualTo([ "counter", "k1" ]));
			Assert.That(trace.FinalState[1].Value, Is.EqualTo("hello"));
		}

		[Test]
		public async Task Test_Runner_Records_Conflict_On_Loser_Commit()
		{
			var builder = new ScenarioBuilder();
			builder.Begin("A");
			builder.Get("A", "k1");       // A reads k1...
			builder.Begin("B");
			builder.Set("B", "k1", "b1"); // ...B writes it...
			builder.Commit("B");          // ...and commits first
			builder.Set("A", "k2", "a1"); // A writes something based on its (now stale) read
			builder.Commit("A");          // => read-write conflict
			var scenario = builder.Build("runner_conflict");

			using var db = await OpenScenarioDatabaseAsync();
			var trace = await ScenarioRunner.RunAsync(scenario, db, this.Cancellation);
			Log(trace.ToJsonText());

			Assert.That(trace.Events[4].Outcome.ContainsKey("error"), Is.False, "B commits first and must win");
			Assert.That(trace.Events[6].Outcome.Get<string?>("error", null), Is.EqualTo("NotCommitted"), "A must lose the conflict");

			// only B's write is visible in the final state
			Assert.That(trace.FinalState.Select(kv => kv.Key), Is.EqualTo([ "k1" ]));
		}

		[TestCase("cac-hit", true)]   // CompareAndClear operand == committed value: it would CLEAR k3 (presence flips)
		[TestCase("cac-miss", true)]  // CompareAndClear operand != committed value: it KEEPS k3 (presence unchanged)
		[TestCase("add", false)]      // discriminator: Add always leaves k3 present, so its presence is locally determined
		public async Task Test_Own_Atomic_Under_Selector_Read_Conflict(string flavor, bool expectConflict)
		{
			// A read-write transaction that applies an own atomic to k3 and then resolves a selector THROUGH k3
			// must take a read conflict on k3 IFF the atomic's effect on k3's PRESENCE depends on the committed
			// value. Only CompareAndClear is visibility-conditional (it clears k3 iff committed(k3) == operand,
			// fdb 7.4.6 fdbclient/WriteMap.cpp coalesce -> doCompareAndClear against the existing value), so a
			// peer write to k3 must conflict the reader for BOTH CAC outcomes; every other atomic returns a
			// present value, so an own Add leaves k3 present regardless and its presence is locally determined -
			// a peer write there must NOT conflict (the atomicsAreLocal discriminator, family-4 round two).

			var builder = new ScenarioBuilder();
			builder.Begin("A");
			builder.Set("A", "k3", "v"); // committed: k3 = "v"
			builder.Set("A", "k5", "w"); // a neighbor, so fGE(k3) lands somewhere distinct when k3 is absent
			builder.Commit("A");

			builder.Begin("W");
			switch (flavor)
			{
				case "cac-hit": builder.Atomic("W", "k3", "v", FdbMutationType.CompareAndClear); break;
				case "cac-miss": builder.Atomic("W", "k3", "z", FdbMutationType.CompareAndClear); break;
				default: builder.Atomic("W", "k3", Slice.FromFixed64(1), FdbMutationType.Add); break;
			}
			builder.GetKey("W", new ScenarioSelector(Slice.FromStringAscii("k3"), OrEqual: false, Offset: 1)); // fGE(k3): resolves through k3, whose presence W's own atomic may or may not fix locally

			builder.Begin("P");
			builder.Set("P", "k3", "peer"); // a peer writes k3...
			builder.Commit("P");            // ...and commits first, after W's read version

			builder.Commit("W"); // W commits: conflicts iff k3 stayed in its read-conflict range

			var scenario = builder.Build("cac_under_selector_" + flavor);

			using var db = await OpenScenarioDatabaseAsync();
			var trace = await ScenarioRunner.RunAsync(scenario, db, this.Cancellation);
			Log(trace.ToJsonText());

			var wCommit = trace.Events[^1];
			if (expectConflict)
			{
				Assert.That(wCommit.Outcome.Get<string?>("error", null), Is.EqualTo("NotCommitted"), $"the {flavor} reader's selector resolution depended on k3's committed value, so a peer write to k3 must conflict it");
			}
			else
			{
				Assert.That(wCommit.Outcome.ContainsKey("error"), Is.False, "an own Add leaves k3 present regardless of the committed value, so the selector resolution is locally determined and a peer write to k3 must not conflict");
			}
		}

		[TestCase("cac", "c", false)]       // pending CompareAndClear on an absent key: the WALK counts it present (fdb is_kv), CONTENT excludes it (kv coalesces to absent) - the is_kv-vs-kv split
		[TestCase("clear-add", "c", true)]  // Clear then Add over the cleared span: INDEPENDENT_WRITE (SetValue base) -> present in the walk; content shows the added value
		[TestCase("add", "c", true)]        // discriminator: a plain Add is already counted by both engines (coalesces to a value)
		[TestCase("none", "e", false)]      // control: no pending write, the slot is truly absent, the walk skips it
		public async Task Test_Pending_Atomic_Counts_As_Present_In_Selector_Resolution(string flavor, string expectedKey, bool expectContentB)
		{
			// A key carrying a pending write is a PRESENT boundary key for selector / range-bound resolution
			// (fdb 7.4.6 RYWIterator::typeMap / is_kv(): INDEPENDENT_WRITE and DEPENDENT_WRITE are KV), even when
			// its coalesced value is absent - a CompareAndClear that clears still COUNTS in the offset walk. The
			// coalesced value governs only read CONTENT (RYWIterator::kv(), a separate scan): the CAC'd key is
			// present for the WALK yet absent from the CONTENT. Committed keys a < c < e; the pending write lands
			// on the absent gap key b. fGE(a)+2 counts b iff b is a present boundary key: real resolves to c, an
			// under-counting resolver to e (FDBV-036).

			var builder = new ScenarioBuilder();
			builder.Begin("A");
			builder.Set("A", "a", "va");
			builder.Set("A", "c", "vc");
			builder.Set("A", "e", "ve");
			builder.Commit("A");

			builder.Begin("W");
			switch (flavor)
			{
				case "cac": builder.Atomic("W", "b", Slice.FromStringAscii("x"), FdbMutationType.CompareAndClear); break; // b absent -> coalesces to absent
				case "clear-add": builder.Clear("W", "b"); builder.Atomic("W", "b", Slice.FromFixed64(1), FdbMutationType.Add); break;
				case "add": builder.Atomic("W", "b", Slice.FromFixed64(1), FdbMutationType.Add); break;
				default: break; // "none": no pending write on b
			}
			builder.GetKey("W", new ScenarioSelector(Slice.FromStringAscii("a"), OrEqual: false, Offset: 3)); // 3rd present key at/after a - counts b iff b is a boundary key
			builder.GetRange("W", new ScenarioSelector(Slice.FromStringAscii("a"), OrEqual: false, Offset: 1), new ScenarioSelector(Slice.FromStringAscii("e"), OrEqual: true, Offset: 1)); // [a, e] content

			var scenario = builder.Build("pending_atomic_selector_" + flavor);

			using var db = await OpenScenarioDatabaseAsync();
			var trace = await ScenarioRunner.RunAsync(scenario, db, this.Cancellation);
			Log(trace.ToJsonText());

			var getKey = trace.Events.Single(e => e.Op == "GetKey");
			Assert.That(getKey.Outcome.Get<string?>("key", null), Is.EqualTo(expectedKey), $"[{flavor}] the selector walk must count a pending-write key as a present boundary key (fdb is_kv)");

			// guardrail: the fix moves ONLY the boundary walk - CONTENT is a separate scan and must be unchanged
			var getRange = trace.Events.Single(e => e.Op == "GetRange");
			var contentKeys = getRange.Outcome.GetArray("items").Select(item => item["key"].As<string>()).ToList();
			Assert.That(contentKeys.Contains("b"), Is.EqualTo(expectContentB), $"[{flavor}] read CONTENT reflects the coalesced value (a CAC-to-absent key stays absent from content), independent of the boundary walk");
		}

		[TestCase("fwd-skip", "a", false, 2, "e")]           // is_kv anchor lands on the CAC'd c (content-absent) -> skip FORWARD to e
		[TestCase("fwd1-skip", "c", false, 1, "e")]          // offset == +1 (fGE, forward branch): anchor is the CAC'd c -> skip FORWARD to e
		[TestCase("bwd-skip", "e", true, -1, "a")]           // is_kv anchor lands on the CAC'd c -> skip BACKWARD to a (offset < 0)
		[TestCase("zero-skip", "c", true, 0, "a")]           // offset == 0 (lLE, the backward branch): anchor is the CAC'd c -> skip BACKWARD to a
		[TestCase("zero-present", "e", true, 0, "e")]        // offset == 0 control: anchor is the content-present e, no skip
		[TestCase("fwd-anchor-present", "a", false, 3, "e")] // FDBV-036 counting: c IS counted in the walk, anchor lands on the present e
		[TestCase("fwd-noskip", "a", false, 1, "a")]         // anchor is the content-present a, no skip
		public async Task Test_GetKey_Resolves_To_Content_Key_Skipping_Cleared_Anchor(string name, string pivot, bool orEqual, int offset, string expected)
		{
			// fdb getKey is getRange with limit 1 (ReadYourWrites.actor.cpp): the is_kv walk POSITIONS the anchor,
			// counting a pending CompareAndClear as a present boundary key (FDBV-036); then the read returns the
			// nearest CONTENT key via kv() - forward for offset > 0, backward for offset <= 0 - which SKIPS the
			// CAC'd key (its coalesced value is absent). Committed a < e; a pending CAC on the absent gap key c is
			// is_kv-present but content-absent, so it shifts the anchor yet is never itself returned (FDBV-037).

			var builder = new ScenarioBuilder();
			builder.Begin("A");
			builder.Set("A", "a", "va");
			builder.Set("A", "e", "ve");
			builder.Commit("A");

			builder.Begin("W");
			builder.Atomic("W", "c", Slice.FromStringAscii("x"), FdbMutationType.CompareAndClear); // c absent -> content-absent, is_kv-present
			builder.GetKey("W", new ScenarioSelector(Slice.FromStringAscii(pivot), OrEqual: orEqual, Offset: offset));

			var scenario = builder.Build("getkey_content_skip_" + name);
			using var db = await OpenScenarioDatabaseAsync();
			var trace = await ScenarioRunner.RunAsync(scenario, db, this.Cancellation);
			Log(trace.ToJsonText());

			var getKey = trace.Events.Single(e => e.Op == "GetKey");
			Assert.That(getKey.Outcome.Get<string?>("key", null), Is.EqualTo(expected), $"[{name}] getKey returns the nearest CONTENT key from the is_kv anchor, skipping a CompareAndClear'd boundary");
		}

		[Test]
		public async Task Test_GetRange_Bounds_Skip_Cleared_Anchor_Like_GetKey()
		{
			// GetRange never returns a CompareAndClear'd key, whether or not it is a bound's is_kv anchor. Here the
			// begin selector's is_kv anchor IS the CAC'd c: the bound resolves to the raw anchor c (FDBV-038), and the
			// interior merged scan then excludes c (content-absent), so the range still begins at the next content key
			// e - the same key GetKey resolves to, but via the scan rather than a bound shift. (Result-insensitive to
			// FDBV-038 because no content key sits between the raw anchor c and e; the FDBV-038 facts cover the cases
			// where it does.)
			var builder = new ScenarioBuilder();
			builder.Begin("A");
			builder.Set("A", "a", "va");
			builder.Set("A", "e", "ve");
			builder.Commit("A");

			builder.Begin("W");
			builder.Atomic("W", "c", Slice.FromStringAscii("x"), FdbMutationType.CompareAndClear); // c absent -> content-absent, is_kv-present
			builder.GetKey("W", new ScenarioSelector(Slice.FromStringAscii("a"), OrEqual: false, Offset: 2)); // is_kv anchor c -> content-skip -> e
			builder.GetRange("W", new ScenarioSelector(Slice.FromStringAscii("a"), OrEqual: false, Offset: 2), new ScenarioSelector(Slice.FromStringAscii("e"), OrEqual: true, Offset: 1));

			var scenario = builder.Build("getrange_bound_content_skip");
			using var db = await OpenScenarioDatabaseAsync();
			var trace = await ScenarioRunner.RunAsync(scenario, db, this.Cancellation);
			Log(trace.ToJsonText());

			var getKey = trace.Events.Single(e => e.Op == "GetKey");
			var getRange = trace.Events.Single(e => e.Op == "GetRange");
			var rangeKeys = getRange.Outcome.GetArray("items").Select(item => item["key"].As<string>()).ToList();

			Assert.That(getKey.Outcome.Get<string?>("key", null), Is.EqualTo("e"), "the begin selector resolves to the content key e (the CAC'd c is skipped)");
			Assert.That(rangeKeys, Is.EqualTo(new[] { "e" }), "GetRange begins at the same content key GetKey resolves to; the CAC'd c is never returned");
		}

		[TestCase("end-drop-fwd",  "a", true, 0, "e", true, 0,  false, "a,c")] // end is_kv anchor is the CAC'd e; the RAW anchor keeps e, so [a,e) includes c - a content-skipped end bound (e->c) drops c
		[TestCase("end-drop-rev",  "a", true, 0, "e", true, 0,  true,  "c,a")] // same shape, reverse: the bound bug is direction-independent (reverse changes ordering only)
		[TestCase("begin-add-fwd", "e", true, 0, "g", false, 1, false, "")]    // begin is_kv anchor is the CAC'd e; the RAW anchor keeps e, so [e,g) is empty - a content-skipped begin bound (e->c) wrongly adds c
		[TestCase("begin-add-rev", "e", true, 0, "g", false, 1, true,  "")]    // same shape, reverse
		public async Task Test_GetRange_Bounds_Keep_Raw_IsKv_Anchor_Not_Content_Skipped(string name, string bKey, bool bEq, int bOff, string eKey, bool eEq, int eOff, bool reverse, string expectedCsv)
		{
			// FDBV-038 (a correction of FDBV-037): getKey content-skips a CAC'd is_kv anchor to the nearest CONTENT
			// key, but a GetRange BOUND selector must NOT - it resolves to the RAW is_kv anchor, and the interior
			// merged scan (OnionIterator) already excludes a CompareAndClear'd key (its coalesced chain is null). fdb
			// getKey = getRange limit 1 positions its RESULT via kv(); a range BOUND stops at the is_kv anchor. Real
			// (7.4.6) traces confirm both: an end bound on a CAC'd anchor keeps content below it (else it is dropped),
			// and a begin bound on a CAC'd anchor keeps the range empty (else a content key below it is added).
			// Committed content a < c < g; a pending CompareAndClear on the absent gap key e is is_kv-present but
			// content-absent, so it can be a bound's is_kv anchor while never being read content.

			var builder = new ScenarioBuilder();
			builder.Begin("A");
			builder.Set("A", "a", "va");
			builder.Set("A", "c", "vc");
			builder.Set("A", "g", "vg");
			builder.Commit("A");

			builder.Begin("W");
			builder.Atomic("W", "e", Slice.FromStringAscii("x"), FdbMutationType.CompareAndClear); // e absent -> content-absent, is_kv-present
			builder.GetRange("W", new ScenarioSelector(Slice.FromStringAscii(bKey), OrEqual: bEq, Offset: bOff), new ScenarioSelector(Slice.FromStringAscii(eKey), OrEqual: eEq, Offset: eOff), reverse: reverse);

			var scenario = builder.Build("getrange_bound_raw_anchor_" + name);
			using var db = await OpenScenarioDatabaseAsync();
			var trace = await ScenarioRunner.RunAsync(scenario, db, this.Cancellation);
			Log(trace.ToJsonText());

			var getRange = trace.Events.Single(e => e.Op == "GetRange");
			var rangeKeys = getRange.Outcome.GetArray("items").Select(item => item["key"].As<string>()).ToList();
			var expected = expectedCsv.Length == 0 ? [] : expectedCsv.Split(',');
			Assert.That(rangeKeys, Is.EqualTo(expected), $"[{name}] a GetRange bound resolves to the raw is_kv anchor (a CAC'd key), not the content-skipped key; the interior scan excludes the CAC'd key");
		}

		[Test]
		public async Task Test_Runner_Settles_Watch_Observations()
		{
			var builder = new ScenarioBuilder();
			builder.Begin("A");
			builder.Set("A", "k1", "old");
			builder.Commit("A");
			builder.Begin("A");
			int w = builder.Watch("A", "k1");
			builder.Commit("A");
			builder.ExpectPending(w);
			builder.Begin("B");
			builder.Set("B", "k1", "new");
			builder.Commit("B");
			builder.ExpectFired(w);
			builder.Begin("A");
			builder.Get("A", "k1"); // post-fire read observes the new value
			builder.Commit("A");
			var scenario = builder.Build("runner_watch");

			using var db = await OpenScenarioDatabaseAsync();
			var trace = await ScenarioRunner.RunAsync(scenario, db, this.Cancellation);
			Log(trace.ToJsonText());

			Assert.That(trace.Events[6].Outcome.Get<string?>("watch", null), Is.EqualTo("Pending"));
			Assert.That(trace.Events[10].Outcome.Get<string?>("watch", null), Is.EqualTo("Fired"));
			Assert.That(trace.Events[12].Outcome.Get<string?>("value", null), Is.EqualTo("new"));
		}

		[Test]
		public async Task Test_Runner_Symbolizes_Versions_And_Stamps()
		{
			var builder = new ScenarioBuilder();
			builder.Begin("A");
			builder.Set("A", "k1", "v");
			int vs = builder.GetVersionstamp("A");
			builder.Commit("A");
			builder.GetCommittedVersion("A");
			builder.ExpectVersionstamp(vs);
			var scenario = builder.Build("runner_versions");

			using var db = await OpenScenarioDatabaseAsync();
			var trace = await ScenarioRunner.RunAsync(scenario, db, this.Cancellation);
			Log(trace.ToJsonText());

			// the committed version is the first observed version
			Assert.That(trace.Events[4].Outcome.Get<string?>("version", null), Is.EqualTo("v1"));

			// the versionstamp of that commit shares the same symbol root
			Assert.That(trace.Events[5].Outcome.Get<string?>("stamp", null), Does.StartWith("v1#"));
		}

		[Test]
		public async Task Test_Runner_Resolves_Selectors_And_Ranges()
		{
			var builder = new ScenarioBuilder();
			builder.Begin("A");
			builder.Set("A", "k1", "v1");
			builder.Set("A", "k2", "v2");
			builder.Set("A", "k3", "v3");
			builder.Commit("A");
			builder.Begin("A");
			builder.GetKey("A", new ScenarioSelector(Slice.FromStringAscii("k2"), OrEqual: false, Offset: 1)); // FirstGreaterOrEqual(k2) -> k2
			builder.GetKey("A", new ScenarioSelector(Slice.FromStringAscii("k2"), OrEqual: true, Offset: 1));  // FirstGreaterThan(k2) -> k3
			builder.GetKey("A", new ScenarioSelector(Slice.FromStringAscii("k1"), OrEqual: false, Offset: 0)); // LastLessThan(k1) -> below the subspace
			builder.GetRange("A",
				new ScenarioSelector(Slice.FromStringAscii("k1"), false, 1),
				new ScenarioSelector(Slice.FromStringAscii("k9"), false, 1),
				limit: 2);
			builder.GetRange("A",
				new ScenarioSelector(Slice.FromStringAscii("k1"), false, 1),
				new ScenarioSelector(Slice.FromStringAscii("k9"), false, 1),
				limit: 2, reverse: true);
			builder.Commit("A");
			var scenario = builder.Build("runner_selectors");

			using var db = await OpenScenarioDatabaseAsync();
			var trace = await ScenarioRunner.RunAsync(scenario, db, this.Cancellation);
			Log(trace.ToJsonText());

			Assert.That(trace.Events[6].Outcome.Get<string?>("key", null), Is.EqualTo("k2"));
			Assert.That(trace.Events[7].Outcome.Get<string?>("key", null), Is.EqualTo("k3"));
			Assert.That(trace.Events[8].Outcome.Get<string?>("key", null), Is.EqualTo("!outside"));

			var forward = trace.Events[9].Outcome.GetArray("items").AsObjects().Select(o => o.Get<string>("key")).ToList();
			Assert.That(forward, Is.EqualTo([ "k1", "k2" ]));

			var backward = trace.Events[10].Outcome.GetArray("items").AsObjects().Select(o => o.Get<string>("key")).ToList();
			Assert.That(backward, Is.EqualTo([ "k3", "k2" ]));
		}

		[Test]
		public async Task Test_Runner_Applies_Transaction_Options()
		{
			var builder = new ScenarioBuilder();
			builder.Begin("A");
			builder.Set("A", "k1", "committed");
			builder.Commit("A");
			builder.Begin("A");
			builder.SetOption("A", ScenarioTransactionOption.ReadYourWritesDisable);
			builder.Set("A", "k1", "uncommitted");
			builder.Get("A", "k1"); // RYW disabled: the read must NOT see the transaction's own write
			builder.Dispose("A");
			var scenario = builder.Build("runner_options");

			using var db = await OpenScenarioDatabaseAsync();
			var trace = await ScenarioRunner.RunAsync(scenario, db, this.Cancellation);
			Log(trace.ToJsonText());

			Assert.That(trace.Events[4].Outcome.Count, Is.EqualTo(0), "SetOption must succeed silently");
			Assert.That(trace.Events[6].Outcome.Get<string?>("value", null), Is.EqualTo("committed"));
		}

		[Test]
		public async Task Test_Runner_Substitutes_Stamps_In_Dump_And_Reads()
		{
			var builder = new ScenarioBuilder();
			builder.Begin("A");
			builder.SetVersionstampedKey("A", Slice.FromStringAscii("log-") + Slice.Zero(10), 4, "payload");
			builder.SetVersionstampedValue("A", "marker", Slice.Zero(10), 0);
			int vs = builder.GetVersionstamp("A");
			builder.Commit("A");
			builder.ExpectVersionstamp(vs); // registers the stamp pattern for rendering
			builder.Begin("A");
			builder.Get("A", "marker");     // the value IS the raw stamp: must render substituted
			builder.Dispose("A");
			var scenario = builder.Build("runner_stamps");

			using var db = await OpenScenarioDatabaseAsync();
			var trace = await ScenarioRunner.RunAsync(scenario, db, this.Cancellation);
			Log(trace.ToJsonText());

			var stampSymbol = trace.Events[5].Outcome.Get<string>("stamp");
			Assert.That(stampSymbol, Does.StartWith("v1#"));

			Assert.That(trace.Events[7].Outcome.Get<string?>("value", null), Is.EqualTo($"<{stampSymbol}>"));

			// the dump contains the stamped key (substituted) and the marker value (substituted)
			Assert.That(trace.FinalState.Select(kv => kv.Key), Is.EqualTo([ $"log-<{stampSymbol}>", "marker" ]));
			Assert.That(trace.FinalState[1].Value, Is.EqualTo($"<{stampSymbol}>"));
		}

	}

}
