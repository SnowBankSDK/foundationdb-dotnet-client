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

#if !NETFRAMEWORK

namespace FoundationDB.Client.Tests
{
	using System.Net;
	using Docker.DotNet;
	using Docker.DotNet.Models;
	using DotNet.Testcontainers.Configurations;

	/// <summary>Tests of the Docker start path of <see cref="FdbServerTestContainer"/></summary>
	/// <remarks>Uses a private container and volume: the shared <c>fdb-test-7.4-netX.Y</c> containers are in use by other test processes on the same host.</remarks>
	[TestFixture]
	[Category("Fdb-Client-Live")]
	[NonParallelizable]
	public class FdbServerTestContainerFacts : FdbSimpleTest
	{

		private const string OldTag = "7.4.6";

		private const string NewTag = "7.4.7";

		private static readonly string Name = string.CreateInvariant($"fdb-test-selftest-net{Environment.Version.Major}.{Environment.Version.Minor}");

		// outside the 4600..4699 range of the shared containers
		private static readonly int Port = 4700 + Environment.Version.Major;

		private static IDockerClient CreateDockerClient() => TestcontainersSettings.OS.DockerEndpointAuthConfig.GetDockerClientBuilder(Guid.Empty).Build();

		[TearDown]
		public async Task RemovePrivateContainer()
		{
			using var docker = CreateDockerClient();
			try
			{
				await docker.Containers.RemoveContainerAsync(Name, new() { Force = true }, this.Cancellation);
			}
			catch (DockerContainerNotFoundException)
			{ }
			try
			{
				await docker.Volumes.RemoveAsync(Name, force: true, this.Cancellation);
			}
			catch (DockerApiException e) when (e.StatusCode == HttpStatusCode.NotFound)
			{ }
		}

		[Test]
		public async Task Test_Stale_Container_From_Previous_Image_Is_Replaced()
		{
			// a previous run of the harness left a container built from the previous image tag under the same name
			await using (var stale = new FdbServerTestContainer(Name, OldTag, Port, Name))
			{
				await stale.StartContainer(TimeSpan.FromSeconds(60), this.Cancellation);
			}

			using var docker = CreateDockerClient();
			var planted = await docker.Containers.InspectContainerAsync(Name, this.Cancellation);
			Assume.That(planted.Config!.Image, Is.EqualTo("foundationdb/foundationdb:" + OldTag));
			await docker.Containers.StopContainerAsync(planted.ID, new(), this.Cancellation);

			// the run after the image bump: Testcontainers does not recognize the stale container, and the name is taken
			await using var fresh = new FdbServerTestContainer(Name, NewTag, Port, Name);
			await fresh.StartContainer(TimeSpan.FromSeconds(60), this.Cancellation);

			var current = await docker.Containers.InspectContainerAsync(Name, this.Cancellation);
			Assert.That(current.ID, Is.Not.EqualTo(planted.ID), "the stale container must be replaced");
			Assert.That(current.Config!.Image, Is.EqualTo(fresh.Image));
			Assert.That(current.State!.Running, Is.True);

			var (_, output) = await fresh.RunFdbCliAsync([ "--exec", "status minimal" ], this.Cancellation);
			Assert.That(output, Does.Contain("The database is available"));
		}

		[Test]
		public async Task Test_Same_Image_Container_Without_Reuse_Hash_Is_Replaced()
		{
			// a container with the right image but unknown to Testcontainers (no reuse hash): the create call answers Conflict
			using var docker = CreateDockerClient();
			await docker.Images.CreateImageAsync(new() { FromImage = "foundationdb/foundationdb", Tag = NewTag }, null, new Progress<JSONMessage>(), this.Cancellation);
			var planted = await docker.Containers.CreateContainerAsync(new() { Image = "foundationdb/foundationdb:" + NewTag, Name = Name }, this.Cancellation);

			await using var fresh = new FdbServerTestContainer(Name, NewTag, Port, Name);
			await fresh.StartContainer(TimeSpan.FromSeconds(60), this.Cancellation);

			var current = await docker.Containers.InspectContainerAsync(Name, this.Cancellation);
			Assert.That(current.ID, Is.Not.EqualTo(planted.ID), "the conflicting container must be replaced");
			Assert.That(current.State!.Running, Is.True);

			var (_, output) = await fresh.RunFdbCliAsync([ "--exec", "status minimal" ], this.Cancellation);
			Assert.That(output, Does.Contain("The database is available"));
		}

	}

}

#endif
