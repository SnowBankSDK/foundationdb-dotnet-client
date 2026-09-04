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

namespace FoundationDB.Testing.Tests
{
	using FoundationDB.Client;
	using Microsoft.Extensions.DependencyInjection;

	/// <summary>The <c>AddFakeDb(...)</c> door: the registration shape the README documents.</summary>
	[TestFixture]
	[Category("FakeDb-Client")]
	public class FakeDbProviderFacts : FakeDbTest
	{

		[Test]
		public async Task Test_AddFakeDb_With_No_Root_Opens_The_Cluster_Root()
		{
			// `services.AddFakeDb(730)` is the documented one-liner: the omitted root must mean the cluster root
			var services = new ServiceCollection();
			services.AddFakeDb(730);
			using var sp = services.BuildServiceProvider();

			var provider = sp.GetRequiredService<IFdbDatabaseProvider>();
			var db = await provider.GetDatabase(this.Cancellation);
			Assert.That(db.Root.Path, Is.EqualTo(FdbPath.Root));
			Assert.That(provider.IsAvailable, Is.True, "a started provider with a database is available");

			await db.WriteAsync(tr => tr.Set(Key("k1"), Value("v1")), this.Cancellation);
			Assert.That(await db.ReadAsync(tr => tr.GetAsync(Key("k1")), this.Cancellation), Is.EqualTo(Value("v1")));
		}

	}

}
