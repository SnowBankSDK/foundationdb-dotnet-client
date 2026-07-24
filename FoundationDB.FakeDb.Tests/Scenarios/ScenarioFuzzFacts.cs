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

		/// <summary>The generator families, one entry per method on <see cref="ScenarioGenerator"/> (also the lookup used by the dual-live fuzz fixtures).</summary>
		public static Func<int, Scenario> Family(string family) => family switch
		{
			"ryw" => ScenarioGenerator.GenerateRywFuzz,
			"mtx" => ScenarioGenerator.GenerateMultiTxnFuzz,
			_ => throw new ArgumentException($"Unknown generator family '{family}'.", nameof(family)),
		};

		[TestCase("ryw")]
		[TestCase("mtx")]
		public void Test_Generator_Is_Deterministic(string family)
		{
			// resumability depends on it: the same seed must always pin the same scenario
			var generate = Family(family);
			var a = generate(42).ToJson();
			var c = generate(42).ToJson();
			Assert.That(c, Is.EqualTo(a));

			var other = generate(43).ToJson();
			Assert.That(other, Is.Not.EqualTo(a), "different seeds must differ");
		}

		[TestCase("ryw")]
		[TestCase("mtx")]
		public async Task Test_Generated_Scenario_Executes_On_FakeDb(string family)
		{
			var generate = Family(family);
			for (int seed = 0; seed < 25; seed++)
			{
				var scenario = generate(seed);
				Assert.That(Scenario.FromJson(scenario.ToJson()).ToJson(), Is.EqualTo(scenario.ToJson()), "generated scenarios must round-trip (pinning depends on it)");

				// a fresh store per seed: disposing a database takes its store down with it
				using IFdbDatabase db = new FakeDbStore().OpenDatabase(GetTestPartitionPath($"gen_{family}_{seed}"), readOnly: false);
				await CleanLocation(db);
				var trace = await ScenarioRunner.RunAsync(scenario, db, this.Cancellation);
				Assert.That(trace.Events, Has.Count.EqualTo(scenario.Steps.Count), $"seed {seed} must execute every step");
			}
		}

	}

	/// <summary>Runs generated RYW fuzz scenarios in dual-live mode (real cluster vs FakeDb); any divergence is pinned as a permanent regression scenario under <c>Scenarios/Corpus/Pinned/</c>.</summary>
	[TestFixture, Explicit("Requires a local Docker daemon"), Category("RealCluster")]
	public class ScenarioFuzzFacts : FdbTest
	{

		/// <summary>Deep-dive on one seed: runs it dual-live and dumps both traces on divergence (pick the family and seed from a failed batch).</summary>
		[TestCase("ryw", 4)]
		[TestCase("ryw", 101)]
		[TestCase("ryw", 115)]
		[TestCase("mtx", 0)]
		public async Task DiagnoseSeed(string family, int seed)
		{
			var scenario = ScenarioGeneratorFacts.Family(family)(seed);
			Log(scenario.ToJson().ToJsonText(CrystalJsonSettings.JsonIndented));

			using var realDb = await OpenTestPartitionAsync($"diag_{family}_{seed}");
			await CleanLocation(realDb);
			var real = await ScenarioRunner.RunAsync(scenario, realDb, this.Cancellation);

			using IFdbDatabase fakeDb = new FakeDbStore().OpenDatabase(GetTestPartitionPath($"diag_{family}_{seed}"), readOnly: false);
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

		/// <summary>Runs one scenario dual-live (cleaned real partition vs a fresh FakeDb store) and returns the trace divergences.</summary>
		private async Task<IReadOnlyList<TraceDivergence>> RunSeedDualLive(Scenario scenario, IFdbDatabase realDb, string partition)
		{
			await CleanLocation(realDb);
			var real = await ScenarioRunner.RunAsync(scenario, realDb, this.Cancellation);

			using IFdbDatabase fakeDb = new FakeDbStore().OpenDatabase(GetTestPartitionPath(partition), readOnly: false);
			fakeDb.Options.WithDefaultTimeout(TimeSpan.FromSeconds(15));
			await CleanLocation(fakeDb);
			var fake = await ScenarioRunner.RunAsync(scenario, fakeDb, this.Cancellation);

			return TraceComparer.Compare(real, fake, scenario);
		}

		/// <summary>Records the real-cluster trace of every generated scenario to one JSON file per seed, WITHOUT comparing anything: the output of two runs (e.g. different native client builds against the same server) can then be diffed offline.</summary>
		/// <remarks>Set <c>FDB_TEST_TRACE_OUT</c> to the output directory. Traces are already normalized (symbolized versions, relative keys), so a byte-level diff of the JSON files is meaningful; re-run differing seeds on both sides to separate real divergence from the known per-run noise (batched-GRV boundary effects).</remarks>
		[TestCase("ryw", 0, 1000)]
		[TestCase("mtx", 0, 1000)]
		public async Task RecordFuzzTraces(string family, int firstSeed, int count)
		{
			var outDir = Environment.GetEnvironmentVariable("FDB_TEST_TRACE_OUT");
			if (string.IsNullOrEmpty(outDir)) Assert.Ignore("Set FDB_TEST_TRACE_OUT to the directory that should receive the recorded traces.");
			Directory.CreateDirectory(outDir);

			var generate = ScenarioGeneratorFacts.Family(family);
			using var realDb = await OpenTestPartitionAsync($"rec_{family}_{firstSeed}_{count}");

			for (int seed = firstSeed; seed < firstSeed + count; seed++)
			{
				var scenario = generate(seed);
				await CleanLocation(realDb);
				var trace = await ScenarioRunner.RunAsync(scenario, realDb, this.Cancellation);
				await File.WriteAllTextAsync(Path.Combine(outDir, $"{scenario.Name}.json"), trace.ToJsonText(), this.Cancellation);
			}

			Log($"recorded {count} '{family}' traces to {outDir}");
		}

		[TestCase("ryw", 0, 200)]
		[TestCase("ryw", 200, 800)]
		[TestCase("mtx", 0, 200)]
		[TestCase("mtx", 200, 800)]
		[TestCase("mtx", 1000, 1000)]
		[TestCase("mtx", 2000, 1000)]
		public async Task FuzzDualLive(string family, int firstSeed, int count)
		{
			var generate = ScenarioGeneratorFacts.Family(family);
			var failures = new StringBuilder();
			int divergent = 0, transient = 0;

			// one partitioned real database for the whole batch, cleaned between seeds
			using var realDb = await OpenTestPartitionAsync($"batch_{family}_{firstSeed}_{count}");

			for (int seed = firstSeed; seed < firstSeed + count; seed++)
			{
				var scenario = generate(seed);

				var divergences = await RunSeedDualLive(scenario, realDb, $"batch_{family}_{firstSeed}_{count}");
				if (divergences.Count > 0)
				{
					// a real cluster is not perfectly deterministic (e.g. the batched GRV can land just before or
					// after a peer's commit, moving the conflict window): only a REPRODUCED divergence is a finding
					var second = await RunSeedDualLive(scenario, realDb, $"batch_{family}_{firstSeed}_{count}");
					if (second.Count == 0)
					{
						transient++;
						Log($"seed {seed}: divergence not reproduced on retry (real-cluster nondeterminism), ignored:");
						Log(TraceComparer.Render("real", "fakedb", divergences));
						continue;
					}
					divergent++;
					var pinned = ScenarioCorpus.PinScenario(scenario);
					Log($"seed {seed}: {second.Count} divergence(s), scenario pinned to {pinned}");
					failures.AppendLine($"--- seed {seed} ---");
					failures.AppendLine(TraceComparer.Render("real", "fakedb", second));
				}
			}

			Log($"fuzzed {count} '{family}' seeds starting at {firstSeed}: {divergent} divergent, {transient} transient");
			if (divergent > 0)
			{
				Assert.Fail($"{divergent}/{count} generated scenarios diverged (pinned for replay):\n{failures}");
			}
		}

	}

}
