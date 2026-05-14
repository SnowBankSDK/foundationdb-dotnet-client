#region Copyright (c) 2023-2026 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace FoundationDB.Client.Tests
{
	using System.Net;
	using Docker.DotNet;
	using Docker.DotNet.Models;
	using DotNet.Testcontainers.Builders;
	using DotNet.Testcontainers.Configurations;
	using DotNet.Testcontainers.Containers;

	public sealed class FdbServerTestContainer : IAsyncDisposable
	{

		private IContainer? Container { get; set; }

		public string Name { get; }

		public string Tag { get; }

		public string Image { get; }

		public string VolumeName { get; }

		public string Description { get; }

		public string Id { get; }

		public int Port { get; }

		public string PortName { get; }

		public string ConnectionString { get; }

		public FdbServerTestContainer(string name, string tag, int port, string volumeName)
		{
			Contract.NotNullOrEmpty(tag);
			Contract.GreaterOrEqual(port, 0);
			Contract.NotNullOrEmpty(volumeName);

			this.Name = name;
			this.Description = "docker";
			this.Id = "docker";
			this.Port = port;
			this.PortName = string.Create(CultureInfo.InvariantCulture, $"{port}/tcp");
			this.Tag = tag;
			this.Image = "foundationdb/foundationdb:" + tag;
			this.VolumeName = volumeName;

			this.ConnectionString = $"{this.Id}:{this.Description}@127.0.0.1:{this.Port}";
		}

		/// <summary>Start the container</summary>
		/// <param name="startTimeout">Maximum startup delay allowed</param>
		/// <param name="ct">Cancellation token for the current test</param>
		/// <returns>Task that is either immediately completed, completes when the container becomes ready, or fails if the container failed to start.</returns>
		/// <remarks>Only one thread per process must call this method.</remarks>
		public async Task StartContainer(TimeSpan startTimeout, CancellationToken ct)
		{
			// Start the container.
			SimpleTest.Log($"Starting FdbServer test container for {this.ConnectionString}...");

			var name = this.Name; // "fdb-test-net10.0"
			var port = this.Port; // ex: 4530
			var portLiteral = port.ToString(CultureInfo.InvariantCulture); // ex: "4530"
			string portBindingName = this.PortName; // ex: "4530/tcp"

			// Create a new instance of a container.
			var container = new ContainerBuilder(this.Image)
				.WithName(name)
				.WithReuse(reuse: true)
				.WithPortBinding(port, port)
				.WithVolumeMount(this.VolumeName, "/var/fdb/data", AccessMode.ReadWrite)
				.WithEnvironment("FDB_NETWORKING_MODE", "host")
				.WithEnvironment("FDB_PORT", portLiteral)
				.WithEnvironment("FDB_COORDINATOR_PORT", portLiteral)
				.WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("FDBD joined cluster."))
				.WithCreateParameterModifier(config =>
				{
					// this is probably not needed, but just to be safe, force the config to something that is known to work
					config.HostConfig.CgroupnsMode = "host";
					config.ExposedPorts = new Dictionary<string, EmptyStruct>()
					{
						[portBindingName] = default,
					};
					config.HostConfig.PortBindings = new Dictionary<string, IList<PortBinding>>()
					{
						// fdb does not use IPv6, force to IPv4 only
						[portBindingName] = [ new() { HostIP = "127.0.0.1", HostPort = port.ToString(null, CultureInfo.InvariantCulture) }, ],
					};
				})
				.WithStartupCallback((c, _) =>
				{
					switch (c.State)
					{
						case TestcontainersStates.Running:
						{
							SimpleTest.Log($"Docker container '{c.Name}' ({c.Id}) using image {c.Image.FullName} is {c.State} on port {c.Hostname}:{port}");
							break;
						}
						default:
						{
							SimpleTest.LogError($"Docker container '{c.Name}' ({c.Id}) using image {c.Image.FullName} is {c.State} ({c.Health})");
							break;
						}
					}
					return Task.CompletedTask;
				})
				.Build();

			this.Container = container;

			using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
			{
				cts.CancelAfter(startTimeout);
				try
				{
					await container.StartAsync(cts.Token).ConfigureAwait(false);
				}
				catch (DockerApiException dex)
				{
					if (dex.StatusCode == HttpStatusCode.Conflict)
					{
						SimpleTest.LogError($"FdbServer test container '{name}' is conflicting with another container! Please delete the old container (but keep the volumes!), and restart the test.", dex);
					}
					else
					{
						SimpleTest.LogError($"FdbServer test container '{name}' failed to start due to a Docker error", dex);
					}
					throw;
				}
				catch (Exception e)
				{
					SimpleTest.LogError($"FdbServer test container '{name}' failed to start due to an internal error", e);
					throw;
				}
			}

			SimpleTest.Log($"FdbServer test container '{name}' ready");
		}

		public ValueTask DisposeAsync()
		{
			var container = this.Container;
			this.Container = null;
			return container?.DisposeAsync() ?? default;
		}

	}

}
