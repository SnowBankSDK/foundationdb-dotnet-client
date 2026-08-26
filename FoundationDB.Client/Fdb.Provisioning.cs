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

namespace FoundationDB.Client
{
	using System.Threading.Tasks;

	public static partial class Fdb
	{

		/// <summary>Helper class for provisioning a database on a freshly created cluster</summary>
		/// <remarks>
		/// <para>A FoundationDB cluster on a brand-new storage volume has no database until <c>configure new</c> runs once. Until then every transaction hangs and <c>status</c> reports the database as unavailable.</para>
		/// <para>The client API cannot write the database configuration, so the work goes through the <c>fdbcli</c> tool that ships inside the server's docker image; callers supply the way to run it (<c>docker exec</c>, Testcontainers exec, ...).</para>
		/// </remarks>
		[PublicAPI]
		public static class Provisioning
		{

			/// <summary>Runs <c>fdbcli</c> with the given arguments, and returns its exit code and combined console output</summary>
			/// <remarks>The delegate owns the transport (<c>docker exec</c> into a container, a local binary, ...); the arguments never need extra quoting beyond what that transport requires.</remarks>
			public delegate Task<(int ExitCode, string Output)> FdbCliRunner(string[] arguments, CancellationToken ct);

			/// <summary>Output marker of a <c>configure new</c> that created the database</summary>
			private const string ConfigureCreatedMarker = "Database created";

			/// <summary>Output marker of a <c>configure new</c> that found an existing configuration (another starter won the race, or the volume was never fresh)</summary>
			private const string ConfigureAlreadyExistsMarker = "Database already exists";

			/// <summary>Output marker of a <c>status</c> that sees a usable database</summary>
			/// <remarks>Matched as a substring: right after creation the full text is "The database is available, but has issues (...)" while data distribution warms up.</remarks>
			private const string StatusAvailableMarker = "The database is available";

			/// <summary>Ensures that the cluster reachable by <paramref name="runFdbCli"/> has a configured, available database, creating it if the volume is fresh</summary>
			/// <param name="runFdbCli">Runs <c>fdbcli</c> against the cluster (for a docker container: <c>docker exec &lt;container&gt; fdbcli &lt;arguments&gt;</c>)</param>
			/// <param name="timeout">Maximum total time to wait for the database to become available</param>
			/// <param name="configuration">Configuration given to <c>configure new</c> (defaults to <c>"single ssd"</c>, the single-node local dev shape)</param>
			/// <param name="probeInterval">Delay between two availability probes (defaults to 500 ms)</param>
			/// <param name="log">Receives one line per outcome, so that a first run is never silent</param>
			/// <param name="time">Time source for the availability wait (the deadline and the poll delay); defaults to the system clock. A test can pass a fake provider to drive the timeout with virtual time.</param>
			/// <param name="ct">Token used to abort the wait</param>
			/// <remarks>
			/// <para>Idempotent and safe against concurrent starters: <c>configure new</c> refuses to touch an existing configuration, and that refusal counts as success.</para>
			/// <para>Every <c>fdbcli</c> invocation passes <c>--no-status</c>: the initial status check would itself wait 30 to 60 seconds on the unconfigured database this method exists to fix.</para>
			/// </remarks>
			/// <exception cref="InvalidOperationException">If <c>configure new</c> fails for another reason than an existing database, or if the database is still unavailable after <paramref name="timeout"/>; the message carries the manual recovery command.</exception>
			public static async Task EnsureDatabaseConfiguredAsync(FdbCliRunner runFdbCli, TimeSpan timeout, string configuration = "single ssd", TimeSpan? probeInterval = null, Action<string>? log = null, TimeProvider? time = null, CancellationToken ct = default)
			{
				Contract.NotNull(runFdbCli);
				Contract.GreaterThan(timeout, TimeSpan.Zero);
				Contract.NotNullOrWhiteSpace(configuration);
				time ??= TimeProvider.System;

				string configureCommand = "configure new " + configuration;
				string manualRecipe = $"fdbcli --no-status --exec \"{configureCommand}\"";
				var interval = probeInterval ?? TimeSpan.FromMilliseconds(500);

				// Act first: on a configured database this returns immediately with the already-exists refusal,
				// while any status-based probe would first wait ~30 seconds for the unavailable case.
				var (exitCode, output) = await runFdbCli([ "--no-status", "--exec", configureCommand ], ct).ConfigureAwait(false);
				if (output.Contains(ConfigureCreatedMarker))
				{
					log?.Invoke($"FoundationDB database created ('{configureCommand}' on a fresh volume).");
				}
				else if (output.Contains(ConfigureAlreadyExistsMarker))
				{
					log?.Invoke("FoundationDB database already configured, leaving it untouched.");
				}
				else
				{
					throw new InvalidOperationException($"FoundationDB database provisioning failed: '{configureCommand}' exited with code {exitCode}: {output.Trim()}. Run '{manualRecipe}' in the fdb container, then restart.");
				}

				// Confirm: the database must actually answer before callers start opening connections.
				var since = time.GetTimestamp();
				while (true)
				{
					(_, output) = await runFdbCli([ "--no-status", "--exec", "status minimal" ], ct).ConfigureAwait(false);
					if (output.Contains(StatusAvailableMarker))
					{
						log?.Invoke("FoundationDB database is available.");
						return;
					}
					if (time.GetElapsedTime(since) >= timeout)
					{
						throw new InvalidOperationException($"FoundationDB database is still unavailable after {timeout.TotalSeconds:N0} seconds: {output.Trim()}. Run '{manualRecipe}' in the fdb container, then restart.");
					}
#if NET8_0_OR_GREATER
					await Task.Delay(interval, time, ct).ConfigureAwait(false);
#else
					await time.Delay(interval, ct).ConfigureAwait(false);
#endif
				}
			}

		}

	}

}
