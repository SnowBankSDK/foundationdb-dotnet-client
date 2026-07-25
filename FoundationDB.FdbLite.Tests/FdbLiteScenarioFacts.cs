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
	using System.Text;
	using FoundationDB.Storage.FdbLite;
	using FoundationDB.Testing;

	/// <summary>Runs the differential scenario corpus against the persistent backend: it must trace identically to the in-memory backend, and to the recorded real-cluster goldens where they exist.</summary>
	[TestFixture]
	[Category("Fdb-Scenario")]
	public class FdbLiteScenarioFacts : FakeDbScenarioTest
	{

		/// <summary>Every corpus scenario must produce the SAME trace on both backends (the in-memory backend is the validated reference).</summary>
		[TestCaseSource(typeof(ScenarioCorpus), nameof(ScenarioCorpus.TestCases))]
		public async Task Differential_Against_FakeDb(string scenarioName)
		{
			var scenario = ScenarioCorpus.Get(scenarioName);

			using IFdbDatabase fakeDb = new FakeDbStore().OpenDatabase(GetTestPartitionPath(scenarioName), readOnly: false);
			await CleanLocation(fakeDb);
			var expected = await ScenarioRunner.RunAsync(scenario, fakeDb, this.Cancellation);

			using var store = FdbLiteStore.CreateInMemory(FdbLiteGeometry.Hypothesis);
			using IFdbDatabase lite = store.OpenDatabase(GetTestPartitionPath(scenarioName), readOnly: false);
			await CleanLocation(lite);
			var actual = await ScenarioRunner.RunAsync(scenario, lite, this.Cancellation);

			var divergences = TraceComparer.Compare(expected, actual, scenario);
			if (divergences.Count > 0)
			{
				Assert.Fail(TraceComparer.Render("fakedb", "fdblite", divergences));
			}
		}

		/// <summary>Docker-free differential fuzz sweep: every generated scenario must trace identically on both emulators.</summary>
		/// <remarks>A divergence here means the persistent backend disagrees with the validated in-memory reference; triage the seed dual-live against the real cluster (<c>ScenarioFuzzFacts.DiagnoseSeed</c>) to grade which side is wrong before pinning it to the corpus.</remarks>
		[TestCase("ryw", 0, 500)]
		[TestCase("mtx", 0, 500)]
		public async Task Fuzz_Differential_Against_FakeDb(string family, int firstSeed, int count)
		{
			var generate = ScenarioGeneratorFacts.Family(family);
			var failures = new StringBuilder();
			int divergent = 0;

			for (int seed = firstSeed; seed < firstSeed + count; seed++)
			{
				var scenario = generate(seed);

				using IFdbDatabase fakeDb = new FakeDbStore().OpenDatabase(GetTestPartitionPath($"fuzz_{family}_{seed}"), readOnly: false);
				await CleanLocation(fakeDb);
				var expected = await ScenarioRunner.RunAsync(scenario, fakeDb, this.Cancellation);

				using var store = FdbLiteStore.CreateInMemory(FdbLiteGeometry.Hypothesis);
				using IFdbDatabase lite = store.OpenDatabase(GetTestPartitionPath($"fuzz_{family}_{seed}"), readOnly: false);
				await CleanLocation(lite);
				var actual = await ScenarioRunner.RunAsync(scenario, lite, this.Cancellation);

				var divergences = TraceComparer.Compare(expected, actual, scenario);
				if (divergences.Count > 0)
				{
					divergent++;
					failures.AppendLine($"--- seed {seed} ---");
					failures.AppendLine(TraceComparer.Render("fakedb", "fdblite", divergences));
				}
			}

			Log($"fuzzed {count} '{family}' seeds starting at {firstSeed}: {divergent} divergent");
			if (divergent > 0)
			{
				Assert.Fail($"{divergent}/{count} generated scenarios diverged between the emulators:\n{failures}");
			}
		}

		/// <summary>Where a real-cluster golden exists, the persistent backend must replay it exactly (the transitive real-fdb oracle).</summary>
		[TestCaseSource(typeof(ScenarioCorpus), nameof(ScenarioCorpus.TestCases))]
		public async Task Replay_Golden_On_FdbLite(string scenarioName)
		{
			if (!ScenarioGoldens.TryLoad(scenarioName, out var golden))
			{
				Assert.Ignore("no golden recorded for this scenario");
				return;
			}
			var scenario = ScenarioCorpus.Get(scenarioName);

			using var store = FdbLiteStore.CreateInMemory(FdbLiteGeometry.Hypothesis);
			using IFdbDatabase lite = store.OpenDatabase(GetTestPartitionPath(scenarioName), readOnly: false);
			await CleanLocation(lite);
			var live = await ScenarioRunner.RunAsync(scenario, lite, this.Cancellation);

			var divergences = TraceComparer.Compare(golden, live, scenario);
			if (divergences.Count > 0)
			{
				Assert.Fail(TraceComparer.Render("golden", "fdblite", divergences));
			}
		}

	}

}
