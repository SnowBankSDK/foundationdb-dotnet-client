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

	/// <summary>Records (or refreshes) the golden trace of corpus scenarios by executing them against a real FoundationDB cluster (Testcontainers).</summary>
	/// <remarks>
	/// <para>Run explicitly from the Unit Test Sessions UI (or with a CLI filter); requires a local Docker daemon. The golden is written into the source tree under <c>Scenarios/Goldens/</c> and must be committed.</para>
	/// <para>Re-record when the corpus changes, and whenever the fdb server container image version bumps, review that diff as "server behavior changed".</para>
	/// </remarks>
	[TestFixture, Explicit("Requires a local Docker daemon"), Category("RealCluster")]
	public class ScenarioRecordFacts : FdbTest
	{

		[TestCaseSource(typeof(ScenarioCorpus), nameof(ScenarioCorpus.TestCases))]
		public async Task Record(string scenarioName)
		{
			var scenario = ScenarioCorpus.Get(scenarioName);

			using var db = await OpenTestPartitionAsync(scenarioName);
			await CleanLocation(db);

			var trace = await ScenarioRunner.RunAsync(scenario, db, this.Cancellation);

			var path = ScenarioGoldens.Save(trace);
			Log($"Golden trace recorded to {path}");
			Log(trace.ToJsonText());
		}

	}

}
