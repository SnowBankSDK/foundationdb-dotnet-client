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
	using FoundationDB.Testing;

	/// <summary>Deterministic checks of the scenario generator itself (no cluster).</summary>
	[TestFixture]
	[Category("Fdb-Scenario")]
	public class ScenarioGeneratorFacts : FakeDbScenarioTest
	{

		[Test]
		public void Test_Generator_Is_Deterministic()
		{
			// resumability depends on it: the same seed must always pin the same scenario
			var a = ScenarioGenerator.GenerateRywFuzz(42).ToJson();
			var c = ScenarioGenerator.GenerateRywFuzz(42).ToJson();
			Assert.That(c, Is.EqualTo(a));

			var other = ScenarioGenerator.GenerateRywFuzz(43).ToJson();
			Assert.That(other, Is.Not.EqualTo(a), "different seeds must differ");
		}

		[Test]
		public async Task Test_Generated_Scenario_Executes_On_FakeDb()
		{
			var scenario = ScenarioGenerator.GenerateRywFuzz(1);
			Assert.That(Scenario.FromJson(scenario.ToJson()).ToJson(), Is.EqualTo(scenario.ToJson()), "generated scenarios must round-trip (pinning depends on it)");

			using var db = await OpenScenarioDatabaseAsync();
			var trace = await ScenarioRunner.RunAsync(scenario, db, this.Cancellation);
			Assert.That(trace.Events, Has.Count.EqualTo(scenario.Steps.Count));
		}

	}

	/// <summary>Runs generated RYW fuzz scenarios in dual-live mode (real cluster vs FakeDb); any divergence is pinned as a permanent regression scenario under <c>Scenarios/Corpus/Pinned/</c>.</summary>
	[TestFixture, Explicit("Requires a local Docker daemon"), Category("RealCluster")]
	public class ScenarioFuzzFacts : FdbTest
	{

		/// <summary>Deep-dive on one seed: runs it dual-live and dumps both traces on divergence (pick the seed from a failed batch).</summary>
		[TestCase(4)]
		[TestCase(101)]
		[TestCase(115)]
		public async Task DiagnoseSeed(int seed)
		{
			var scenario = ScenarioGenerator.GenerateRywFuzz(seed);
			Log(scenario.ToJson().ToJsonText(CrystalJsonSettings.JsonIndented));

			using var realDb = await OpenTestPartitionAsync($"diag_{seed}");
			await CleanLocation(realDb);
			var real = await ScenarioRunner.RunAsync(scenario, realDb, this.Cancellation);

			using IFdbDatabase fakeDb = new FakeDbStore().OpenDatabase(GetTestPartitionPath($"diag_{seed}"), readOnly: false);
			fakeDb.Options.WithDefaultTimeout(TimeSpan.FromSeconds(15));
			await CleanLocation(fakeDb);
			var fake = await ScenarioRunner.RunAsync(scenario, fakeDb, this.Cancellation);

			var divergences = TraceComparer.Compare(real, fake, scenario);
			if (divergences.Count > 0)
			{
				Log("REAL trace:");
				Log(real.ToJsonText());
				Log("FAKEDB trace:");
				Log(fake.ToJsonText());
				Assert.Fail(TraceComparer.Render("real", "fakedb", divergences));
			}
		}

		[TestCase(0, 200)]
		[TestCase(200, 800)]
		public async Task FuzzRywDualLive(int firstSeed, int count)
		{
			var failures = new StringBuilder();
			int divergent = 0;

			// one partitioned real database for the whole batch, cleaned between seeds
			using var realDb = await OpenTestPartitionAsync($"batch_{firstSeed}_{count}");

			for (int seed = firstSeed; seed < firstSeed + count; seed++)
			{
				var scenario = ScenarioGenerator.GenerateRywFuzz(seed);

				await CleanLocation(realDb);
				var real = await ScenarioRunner.RunAsync(scenario, realDb, this.Cancellation);

				using IFdbDatabase fakeDb = new FakeDbStore().OpenDatabase(GetTestPartitionPath($"batch_{firstSeed}_{count}"), readOnly: false);
				fakeDb.Options.WithDefaultTimeout(TimeSpan.FromSeconds(15));
				await CleanLocation(fakeDb);
				var fake = await ScenarioRunner.RunAsync(scenario, fakeDb, this.Cancellation);

				var divergences = TraceComparer.Compare(real, fake, scenario);
				if (divergences.Count > 0)
				{
					divergent++;
					var pinned = ScenarioCorpus.PinScenario(scenario);
					Log($"seed {seed}: {divergences.Count} divergence(s), scenario pinned to {pinned}");
					failures.AppendLine($"--- seed {seed} ---");
					failures.AppendLine(TraceComparer.Render("real", "fakedb", divergences));
				}
			}

			Log($"fuzzed {count} seeds starting at {firstSeed}: {divergent} divergent");
			if (divergent > 0)
			{
				Assert.Fail($"{divergent}/{count} generated scenarios diverged (pinned for replay):\n{failures}");
			}
		}

	}

}
