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

namespace SnowBank.Testing.Framework.Tests
{
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Hosting;
	using NUnit.Framework;

	/// <summary>Self-tests for the hosting environment of a simulated web host</summary>
	[TestFixture]
	public class DistributedTestEnvironmentFacts : DistributedTest
	{

		[Test]
		public async Task Test_Web_Host_Runs_In_Development_By_Default()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("NODE", host => host.ConfigureApplication(app => { /* NOP */ }));
			}));

			var environment = context.GetWebHost("NODE").GetRequiredService<IHostEnvironment>();
			Assert.That(environment.EnvironmentName, Is.EqualTo(Environments.Development));
		}

		[Test]
		public async Task Test_Web_Host_Runs_In_The_Environment_Its_Test_Chose()
		{
			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("NODE", host =>
				{
					host.Component.EnvironmentName = "Test";
					host.ConfigureApplication(app => { /* NOP */ });
				});
			}));

			var environment = context.GetWebHost("NODE").GetRequiredService<IHostEnvironment>();
			Assert.That(environment.EnvironmentName, Is.EqualTo("Test"));
		}

	}

}
