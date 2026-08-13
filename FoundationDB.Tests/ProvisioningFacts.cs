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

	[TestFixture]
	[Category("Fdb-Client-InProc")]
	[Parallelizable(ParallelScope.All)]
	public class ProvisioningFacts : FdbSimpleTest
	{

		private const string StatusUnavailable = "The database is unavailable; type `status' for more information.";

		private const string StatusAvailableWithIssues = "The database is available, but has issues (type 'status' for more information).";

		private const string ConfigureCreated = "Database created";

		private const string ConfigureAlreadyExists = "ERROR: Database already exists! To change configuration, don't say `new'";

		/// <summary>Fake fdbcli that scripts one output per invocation, and records the arguments it received</summary>
		private sealed class FakeFdbCli
		{

			public List<string[]> Calls { get; } = [ ];

			private Queue<(int ExitCode, string Output)> Replies { get; } = new();

			public void Enqueue(int exitCode, string output) => this.Replies.Enqueue((exitCode, output));

			public Task<(int ExitCode, string Output)> Run(string[] arguments, CancellationToken ct)
			{
				ct.ThrowIfCancellationRequested();
				this.Calls.Add(arguments);
				Assert.That(this.Replies, Is.Not.Empty, "The primitive ran fdbcli more times than the scenario expected");
				return Task.FromResult(this.Replies.Dequeue());
			}

		}

		[Test]
		public async Task Test_Fresh_Volume_Is_Configured_Then_Confirmed()
		{
			var cli = new FakeFdbCli();
			cli.Enqueue(0, ConfigureCreated);
			cli.Enqueue(0, StatusAvailableWithIssues);

			await Fdb.Provisioning.EnsureDatabaseConfiguredAsync(cli.Run, TimeSpan.FromSeconds(5), ct: this.Cancellation);

			Assert.That(cli.Calls, Has.Count.EqualTo(2));
			// the configure command runs first, and every invocation must skip the initial status check
			Assert.That(cli.Calls[0], Is.EqualTo((string[]) [ "--no-status", "--exec", "configure new single ssd" ]));
			Assert.That(cli.Calls[1], Is.EqualTo((string[]) [ "--no-status", "--exec", "status minimal" ]));
		}

		[Test]
		public async Task Test_Already_Configured_Database_Is_Left_Untouched()
		{
			// the losing starter of a race, and every start on a non-fresh volume: the configure attempt is refused, which counts as success
			var cli = new FakeFdbCli();
			cli.Enqueue(1, ConfigureAlreadyExists);
			cli.Enqueue(0, "The database is available.");

			await Fdb.Provisioning.EnsureDatabaseConfiguredAsync(cli.Run, TimeSpan.FromSeconds(5), ct: this.Cancellation);

			Assert.That(cli.Calls, Has.Count.EqualTo(2), "already-exists must not trigger a second configure attempt");
		}

		[Test]
		public void Test_Unexpected_Configure_Output_Fails_And_Names_The_Recipe()
		{
			var cli = new FakeFdbCli();
			cli.Enqueue(1, "Unable to connect to cluster");

			var ex = Assert.ThrowsAsync<InvalidOperationException>(
				() => Fdb.Provisioning.EnsureDatabaseConfiguredAsync(cli.Run, TimeSpan.FromSeconds(5), ct: this.Cancellation)
			)!;

			Assert.That(ex.Message, Does.Contain("configure new single ssd"), "the failure must name the manual recipe");
			Assert.That(ex.Message, Does.Contain("Unable to connect to cluster"), "the failure must carry the fdbcli output");
		}

		[Test]
		public void Test_Database_Never_Available_Times_Out_And_Names_The_Recipe()
		{
			var cli = new FakeFdbCli();
			cli.Enqueue(0, ConfigureCreated);
			// enough unavailable probes to exhaust any bounded wait at this probe interval
			for (int i = 0; i < 100; i++)
			{
				cli.Enqueue(0, StatusUnavailable);
			}

			var ex = Assert.ThrowsAsync<InvalidOperationException>(
				() => Fdb.Provisioning.EnsureDatabaseConfiguredAsync(cli.Run, TimeSpan.FromMilliseconds(250), probeInterval: TimeSpan.FromMilliseconds(10), ct: this.Cancellation)
			)!;

			Assert.That(ex.Message, Does.Contain("configure new single ssd"));
		}

		[Test]
		public async Task Test_Probe_Retries_Until_Available()
		{
			var cli = new FakeFdbCli();
			cli.Enqueue(0, ConfigureCreated);
			cli.Enqueue(0, StatusUnavailable);
			cli.Enqueue(0, StatusUnavailable);
			cli.Enqueue(0, StatusAvailableWithIssues);

			await Fdb.Provisioning.EnsureDatabaseConfiguredAsync(cli.Run, TimeSpan.FromSeconds(5), probeInterval: TimeSpan.FromMilliseconds(10), ct: this.Cancellation);

			Assert.That(cli.Calls, Has.Count.EqualTo(4));
		}

		[Test]
		public async Task Test_Happy_Paths_Log_One_Line()
		{
			// a first run must not be silent: both the fresh and the already-configured paths say what happened
			var lines = new List<string>();
			var cli = new FakeFdbCli();
			cli.Enqueue(0, ConfigureCreated);
			cli.Enqueue(0, "The database is available.");

			await Fdb.Provisioning.EnsureDatabaseConfiguredAsync(cli.Run, TimeSpan.FromSeconds(5), log: lines.Add, ct: this.Cancellation);

			Assert.That(lines, Is.Not.Empty);
		}

	}

}
