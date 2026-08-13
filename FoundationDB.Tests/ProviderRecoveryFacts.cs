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
	using FoundationDB.DependencyInjection;

	[TestFixture]
	[Category("Fdb-Client-InProc")]
	public class ProviderRecoveryFacts : FdbTest
	{

		[Test]
		public async Task Test_Provider_Recovers_Once_The_Cluster_Becomes_Reachable()
		{
			var server = GetLocalServer();

			// point the provider at an address where nothing answers, with a short timeout so the first attempt fails quickly
			var options = new FdbDatabaseProviderOptions
			{
				ApiVersion = Fdb.ApiVersion,
				AutoStart = true,
				ConnectionOptions =
				{
					ConnectionString = "dead:beef@127.0.0.1:1",
					// a non-root partition path forces a real round trip during open, which is what times out
					Root = GetTestPartitionPath(nameof(Test_Provider_Recovers_Once_The_Cluster_Becomes_Reachable)),
					DefaultTimeout = TimeSpan.FromSeconds(2),
				},
			};
			using var provider = FdbDatabaseProvider.Create(options);

			// the first open must fail: the coordinator is unreachable
			var first = Assert.CatchAsync(async () => await provider.GetDatabase(this.Cancellation))!;
			Log($"First attempt failed as expected: [{first.GetType().Name}] {first.Message}");

			// the cluster "becomes reachable": the corrected address points at the live test container
			options.ConnectionOptions.ConnectionString = server.ConnectionString;

			// a later call must connect, in-process, without a restart
			var db = await provider.GetDatabase(this.Cancellation);
			Assert.That(db, Is.Not.Null);

			var rv = await db.ReadAsync(tr => tr.GetReadVersionAsync(), this.Cancellation);
			Assert.That(rv, Is.GreaterThan(0));
			Assert.That(provider.IsAvailable, Is.True);
		}

	}

}
