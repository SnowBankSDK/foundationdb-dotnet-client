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
	/// <summary>Tests for the trace capture model: version symbolization, JSON round-trip, and the divergence comparer.</summary>
	[TestFixture]
	[Category("Fdb-Scenario")]
	public class ScenarioTraceFacts : SimpleTest
	{

		[Test]
		public void Test_Symbolizer_Assigns_Symbols_By_First_Appearance()
		{
			var symbols = new VersionSymbolizer();

			// distinct versions get v1, v2, ... in order of first appearance
			Assert.That(symbols.Version(123_456_789L), Is.EqualTo("v1"));
			Assert.That(symbols.Version(987_654_321L), Is.EqualTo("v2"));

			// repeats map to the same symbol
			Assert.That(symbols.Version(123_456_789L), Is.EqualTo("v1"));

			// negative sentinel (no version) is stable
			Assert.That(symbols.Version(-1L), Is.EqualTo("none"));

			// a versionstamp shares the table with plain versions: stamp == committed version is preserved
			var stamp = VersionStamp.Complete(987_654_321UL, 0);
			Assert.That(symbols.Stamp(stamp), Is.EqualTo("v2#0"));

			// a new stamp version extends the table
			var other = VersionStamp.Complete(555UL, 3, userVersion: 7);
			Assert.That(other.HasUserVersion, Is.True);
			Assert.That(symbols.Stamp(other), Is.EqualTo("v3#3u7"));

			// incomplete stamps carry no version
			Assert.That(symbols.Stamp(VersionStamp.Incomplete()), Is.EqualTo("incomplete"));
		}

		[Test]
		public void Test_Symbolizer_Substitutes_Observed_Stamps_In_Rendered_Bytes()
		{
			var symbols = new VersionSymbolizer();

			// observing a stamp (ExpectVersionstamp, GetMetadataVersion) registers its byte pattern
			var stamp = VersionStamp.Complete(0x0123456789ABCDEFUL, 7);
			Assert.That(symbols.Stamp(stamp), Is.EqualTo("v1#7"));

			// rendered keys/values substitute the observed pattern
			var payload = Slice.FromStringAscii("pre") + stamp.ToSlice() + Slice.FromStringAscii("post");
			Assert.That(symbols.Render(payload), Is.EqualTo("pre<v1#7>post"));

			// multiple occurrences all substitute
			var twice = stamp.ToSlice() + Slice.FromStringAscii("-") + stamp.ToSlice();
			Assert.That(symbols.Render(twice), Is.EqualTo("<v1#7>-<v1#7>"));

			// an unobserved stamp stays as raw escaped bytes
			var other = VersionStamp.Complete(42UL, 0);
			Assert.That(symbols.Render(other.ToSlice()), Is.EqualTo(ScenarioText.Encode(other.ToSlice())));

			// nil/empty passthrough matches the plain codec
			Assert.That(symbols.Render(Slice.Nil), Is.Null);
			Assert.That(symbols.Render(Slice.Empty), Is.EqualTo(""));
		}

		private static ScenarioTrace MakeTrace(string value = "world", string watchOutcome = "Fired", string lastKey = "k2")
		{
			return new()
			{
				ScenarioName = "trace_test",
				Events =
				[
					new() { Step = 0, Op = "Begin", Actor = "A", Args = new JsonObject(), Outcome = new JsonObject() },
					new() { Step = 1, Op = "Get", Actor = "A", Args = JsonObject.Create("key", "hello"), Outcome = JsonObject.Create("value", value) },
					new() { Step = 2, Op = "ExpectPending", Actor = null, Args = JsonObject.Create("handle", 0), Outcome = JsonObject.Create("watch", watchOutcome) },
				],
				FinalState =
				[
					new("k1", "v1"),
					new(lastKey, "v2"),
				],
			};
		}

		[Test]
		public void Test_Trace_Json_Roundtrip()
		{
			var trace = MakeTrace();
			var json = trace.ToJson();
			Log(json.ToJsonText(CrystalJsonSettings.JsonIndented));

			var decoded = ScenarioTrace.FromJson(json);
			Assert.That(decoded.ScenarioName, Is.EqualTo("trace_test"));
			Assert.That(decoded.Events, Has.Count.EqualTo(3));
			Assert.That(decoded.Events[1].Outcome["value"].ToString(), Is.EqualTo("world"));
			Assert.That(decoded.FinalState, Has.Count.EqualTo(2));
			Assert.That(decoded.ToJson(), Is.EqualTo(json));
		}

		[Test]
		public void Test_Comparer_Identical_Traces_Yield_No_Divergence()
		{
			var scenario = new ScenarioBuilder().Begin("A").Build("trace_test");
			var divergences = TraceComparer.Compare(MakeTrace(), MakeTrace(), scenario);
			Assert.That(divergences, Is.Empty);
		}

		[Test]
		public void Test_Comparer_Reports_Outcome_And_FinalState_Differences()
		{
			var scenario = new ScenarioBuilder().Begin("A").Build("trace_test");

			// a differing outcome field is located precisely
			var divergences = TraceComparer.Compare(MakeTrace(value: "world"), MakeTrace(value: "WORLD"), scenario);
			Assert.That(divergences, Has.Count.EqualTo(1));
			Assert.That(divergences[0].Step, Is.EqualTo(1));
			Assert.That(divergences[0].Path, Does.Contain("value"));
			Assert.That(divergences[0].Expected, Is.EqualTo("world"));
			Assert.That(divergences[0].Actual, Is.EqualTo("WORLD"));

			// a final-state difference is keyed by the diverging key
			divergences = TraceComparer.Compare(MakeTrace(lastKey: "k2"), MakeTrace(lastKey: "k3"), scenario);
			Assert.That(divergences, Has.Count.EqualTo(2));
			Assert.That(divergences.Select(d => d.Path), Is.EquivalentTo([ "finalState[k2]", "finalState[k3]" ]));

			// truncated traces report the length mismatch
			var shorter = MakeTrace() with { Events = MakeTrace().Events.Take(2).ToList() };
			divergences = TraceComparer.Compare(MakeTrace(), shorter, scenario);
			Assert.That(divergences.Select(d => d.Path), Does.Contain("events.length"));

			// the report renders as readable text
			var report = TraceComparer.Render("golden", "fakedb", TraceComparer.Compare(MakeTrace(), MakeTrace(value: "X"), scenario));
			Log(report);
			Assert.That(report, Does.Contain("step 1").And.Contain("golden").And.Contain("fakedb"));
		}

		[Test]
		public void Test_Comparer_Applies_Spurious_Watch_Fire_Tolerance()
		{
			// scenario whose step 2 is an annotated ExpectPending (steps 0 and 1 are padding to align indexes)
			var builder = new ScenarioBuilder();
			builder.Begin("A");
			int w = builder.Watch("A", "k1");
			builder.ExpectPending(w, ScenarioTolerance.AllowSpuriousWatchFire);
			var tolerant = builder.Build("trace_test");

			// golden says Pending, live fired anyway: accepted under the annotation
			var divergences = TraceComparer.Compare(MakeTrace(watchOutcome: "Pending"), MakeTrace(watchOutcome: "Fired"), tolerant);
			Assert.That(divergences, Is.Empty);

			// the reverse too: in dual-live mode the REFERENCE side can be the spurious one (e.g. a same-key
			// sibling's fire dragging the watch along on the real cluster), so both states are legal on each side
			divergences = TraceComparer.Compare(MakeTrace(watchOutcome: "Fired"), MakeTrace(watchOutcome: "Pending"), tolerant);
			Assert.That(divergences, Is.Empty);

			// without the annotation, the same difference is a divergence
			var strictBuilder = new ScenarioBuilder();
			strictBuilder.Begin("A");
			int w2 = strictBuilder.Watch("A", "k1");
			strictBuilder.ExpectPending(w2);
			var strict = strictBuilder.Build("trace_test");
			divergences = TraceComparer.Compare(MakeTrace(watchOutcome: "Pending"), MakeTrace(watchOutcome: "Fired"), strict);
			Assert.That(divergences, Has.Count.EqualTo(1));
		}

	}

}
