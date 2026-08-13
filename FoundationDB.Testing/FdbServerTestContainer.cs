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
			this.PortName = string.CreateInvariant($"{port}/tcp");
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
#if !NETFRAMEWORK
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
					config.HostConfig ??= new();
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

			// a fresh volume has no database until "configure new" runs once. A configured one answers immediately
			await Fdb.Provisioning.EnsureDatabaseConfiguredAsync(this.RunFdbCliAsync, TimeSpan.FromSeconds(30), log: SimpleTest.Log, ct: ct).ConfigureAwait(false);

			SimpleTest.Log($"FdbServer test container '{name}' ready");
		}

		/// <summary>Runs <c>fdbcli</c> inside the test container, and returns its exit code and combined console output</summary>
		public async Task<(int ExitCode, string Output)> RunFdbCliAsync(string[] arguments, CancellationToken ct)
		{
			var container = this.Container ?? throw new InvalidOperationException("The container has not been started.");
			var result = await container.ExecAsync([ "fdbcli", ..arguments ], ct).ConfigureAwait(false);
			return ((int) result.ExitCode, result.Stdout.Length != 0 ? result.Stdout : result.Stderr);
		}
#else
		// Docker.DotNet cannot run on the .NET Framework CLR (its request serialization reads generic attributes,
		// which netfx reflection does not support): drive the docker CLI directly instead, mimicking the
		// Testcontainers behavior of the modern targets (fixed name and port, reuse an existing container,
		// wait for the "FDBD joined cluster." log line)
		public async Task StartContainer(TimeSpan startTimeout, CancellationToken ct)
		{
			SimpleTest.Log($"Starting FdbServer test container for {this.ConnectionString}...");

			var name = this.Name; // "fdb-test-7.4-net4.7"
			var portLiteral = this.Port.ToString(CultureInfo.InvariantCulture); // ex: "4661"

			using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
			{
				cts.CancelAfter(startTimeout);
				var token = cts.Token;

				// reuse the container if it already exists (same behavior as WithReuse(reuse: true))
				var (code, state) = await RunDockerAsync($"inspect --format {{{{.State.Running}}}} {name}", token).ConfigureAwait(false);
				if (code != 0)
				{ // the container does not exist yet: create and start it
					string output;
					(code, output) = await RunDockerAsync($"run --detach --name {name} --publish 127.0.0.1:{portLiteral}:{portLiteral} --volume {this.VolumeName}:/var/fdb/data --env FDB_NETWORKING_MODE=host --env FDB_PORT={portLiteral} --env FDB_COORDINATOR_PORT={portLiteral} --cgroupns host {this.Image}", token).ConfigureAwait(false);
					if (code != 0)
					{
						SimpleTest.LogError($"FdbServer test container '{name}' failed to start due to a Docker error: {output}");
						throw new InvalidOperationException($"Failed to start docker container '{name}': {output}");
					}
				}
				else if (!state.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
				{ // the container exists but is stopped: restart it
					string output;
					(code, output) = await RunDockerAsync($"start {name}", token).ConfigureAwait(false);
					if (code != 0)
					{
						SimpleTest.LogError($"FdbServer test container '{name}' failed to restart due to a Docker error: {output}");
						throw new InvalidOperationException($"Failed to restart docker container '{name}': {output}");
					}
				}

				// wait until the server has joined the cluster (same wait strategy as the modern targets)
				while (true)
				{
					(code, var logs) = await RunDockerAsync($"logs {name}", token).ConfigureAwait(false);
					if (code == 0 && logs.Contains("FDBD joined cluster."))
					{
						break;
					}
					await Task.Delay(250, token).ConfigureAwait(false);
				}
			}

			// a fresh volume has no database until "configure new" runs once. A configured one answers immediately
			await Fdb.Provisioning.EnsureDatabaseConfiguredAsync(this.RunFdbCliAsync, TimeSpan.FromSeconds(30), log: SimpleTest.Log, ct: ct).ConfigureAwait(false);

			SimpleTest.Log($"FdbServer test container '{name}' ready");
		}

		/// <summary>Runs <c>fdbcli</c> inside the test container, and returns its exit code and combined console output</summary>
		public Task<(int ExitCode, string Output)> RunFdbCliAsync(string[] arguments, CancellationToken ct)
		{
			// quote the arguments that carry spaces (ex: --exec "configure new single ssd")
			var sb = new System.Text.StringBuilder("exec ").Append(this.Name).Append(" fdbcli");
			foreach (var arg in arguments)
			{
				sb.Append(' ');
				if (arg.IndexOf(' ') >= 0)
				{
					sb.Append('"').Append(arg).Append('"');
				}
				else
				{
					sb.Append(arg);
				}
			}
			return RunDockerAsync(sb.ToString(), ct);
		}

		/// <summary>Runs the docker CLI with the specified arguments, and returns the exit code and console output</summary>
		private static async Task<(int ExitCode, string Output)> RunDockerAsync(string arguments, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();
			var psi = new System.Diagnostics.ProcessStartInfo("docker", arguments)
			{
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
			};
			using var process = System.Diagnostics.Process.Start(psi) ?? throw new InvalidOperationException("Failed to launch the docker CLI.");
			var stdOut = process.StandardOutput.ReadToEndAsync();
			var stdErr = process.StandardError.ReadToEndAsync();
			await Task.Run(process.WaitForExit, ct).ConfigureAwait(false);
			string output = await stdOut.ConfigureAwait(false);
			string error = await stdErr.ConfigureAwait(false);
			return (process.ExitCode, output.Length != 0 ? output : error);
		}
#endif

		public ValueTask DisposeAsync()
		{
			var container = this.Container;
			this.Container = null;
			return container?.DisposeAsync() ?? default;
		}

	}

}
