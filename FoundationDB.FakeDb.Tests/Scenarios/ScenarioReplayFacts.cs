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

	/// <summary>Replays every corpus scenario on the FakeDb emulator and compares the live trace against the committed golden (recorded on a real cluster).</summary>
	/// <remarks>This is the default, CI-facing gate of the differential harness: no Docker, no native client. Scenarios without a recorded golden are skipped explicitly.</remarks>
	[TestFixture]
	[Category("Fdb-Scenario")]
	public class ScenarioReplayFacts : FakeDbScenarioTest
	{

		[TestCaseSource(typeof(ScenarioCorpus), nameof(ScenarioCorpus.TestCases))]
		public async Task Replay(string scenarioName)
		{
			var scenario = ScenarioCorpus.Get(scenarioName);
			if (!ScenarioGoldens.TryLoad(scenarioName, out var golden))
			{
				Assert.Ignore($"No golden trace recorded yet for '{scenarioName}': run ScenarioRecordFacts.Record(\"{scenarioName}\") against a local Docker daemon.");
			}

			using var db = await OpenScenarioDatabaseAsync(scenarioName);
			var live = await ScenarioRunner.RunAsync(scenario, db, this.Cancellation);

			var divergences = TraceComparer.Compare(golden, live, scenario);
			if (divergences.Count > 0)
			{
				Log("Live FakeDb trace:");
				Log(live.ToJsonText());
				Assert.Fail(TraceComparer.Render("golden", "fakedb", divergences));
			}
		}

	}

}
