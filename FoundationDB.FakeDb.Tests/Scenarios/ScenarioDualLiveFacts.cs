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
	using FoundationDB.Testing;

	/// <summary>Runs a corpus scenario on both backends live (real Testcontainers cluster and FakeDb) and diffs the two traces directly, without goldens.</summary>
	/// <remarks>Run explicitly from the Unit Test Sessions UI; requires a local Docker daemon. This is the divergence-investigation and fuzzing mode.</remarks>
	[TestFixture, Explicit("Requires a local Docker daemon"), Category("RealCluster")]
	public class ScenarioDualLiveFacts : FdbTest
	{

		[TestCaseSource(typeof(ScenarioCorpus), nameof(ScenarioCorpus.TestCases))]
		public async Task DualLive(string scenarioName)
		{
			var scenario = ScenarioCorpus.Get(scenarioName);

			// real-cluster side
			using var realDb = await OpenTestPartitionAsync(scenarioName);
			await CleanLocation(realDb);
			var real = await ScenarioRunner.RunAsync(scenario, realDb, this.Cancellation);

			// FakeDb side: same head pattern as the conformance fixtures (fresh store, partition path, 15s default timeout)
			using IFdbDatabase fakeDb = new FakeDbStore().OpenDatabase(GetTestPartitionPath(scenarioName), readOnly: false);
			fakeDb.Options.WithDefaultTimeout(TimeSpan.FromSeconds(15));
			await CleanLocation(fakeDb);
			var fake = await ScenarioRunner.RunAsync(scenario, fakeDb, this.Cancellation);

			var divergences = TraceComparer.Compare(real, fake, scenario);
			Log(TraceComparer.Render("real", "fakedb", divergences));
			if (divergences.Count > 0)
			{
				Log("Real-cluster trace:");
				Log(real.ToJsonText());
				Log("FakeDb trace:");
				Log(fake.ToJsonText());
				Assert.Fail(TraceComparer.Render("real", "fakedb", divergences));
			}
		}

	}

}
