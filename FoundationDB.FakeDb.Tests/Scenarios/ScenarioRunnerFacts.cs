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
