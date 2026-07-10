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

	/// <summary>Health checks of the scenario corpus itself: every entry must round-trip through JSON and execute on FakeDb without authoring errors, golden or not.</summary>
	[TestFixture]
	[Category("Fdb-Scenario")]
	public class ScenarioCorpusFacts : FakeDbScenarioTest
	{

		[TestCaseSource(typeof(ScenarioCorpus), nameof(ScenarioCorpus.TestCases))]
		public async Task CheckScenario(string scenarioName)
		{
			var scenario = ScenarioCorpus.Get(scenarioName);

			// pinned scenarios must reload identically from their JSON form
			Assert.That(Scenario.FromJson(scenario.ToJson()).ToJson(), Is.EqualTo(scenario.ToJson()), "the scenario must round-trip through JSON");

			using var db = await OpenScenarioDatabaseAsync(scenarioName);
			var trace = await ScenarioRunner.RunAsync(scenario, db, this.Cancellation);

			Assert.That(trace.Events, Has.Count.EqualTo(scenario.Steps.Count), "one trace event per step");

			// fdb error outcomes (e.g. the conflict loser) are legitimate; non-fdb exceptions in the demo corpus are not
			foreach (var e in trace.Events)
			{
				Assert.That(e.Outcome.ContainsKey("exception"), Is.False, $"step {e.Step} ({e.Op}) threw {e.Outcome["exception"]}");
			}
		}

	}

}
