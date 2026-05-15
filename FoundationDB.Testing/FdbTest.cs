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
	using System.Text;
	using FoundationDB.DependencyInjection;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.DependencyInjection.Extensions;

	/// <summary>Base class for all FoundationDB tests that will interact with a live FoundationDB cluster</summary>
	[NonParallelizable]
	[Category("Fdb-Client-Live")]
	[FixtureLifeCycle(LifeCycle.SingleInstance)]
	public abstract class FdbTest : FdbSimpleTest
	{

		internal const string DockerImageTag73 = "7.3.68";
		internal const string DockerImageTag74 = "7.4.6";

		protected int OverrideApiVersion;

		#region Singleton Management...

		/// <summary>Container instance for this process</summary>
		/// <remarks>The first thread will create the instance. The other threads will synchronize with it.</remarks>
		private static FdbServerTestContainer? ServerContainer;

		/// <summary>Signal used to synchronize all the threads</summary>
		private static readonly TaskCompletionSource ServerReadySignal = new();

		/// <summary>Lock used to ensure only one thread starts the container</summary>
#if NET9_0_OR_GREATER
		private static readonly System.Threading.Lock Lock = new();
#else
		private static readonly object Lock = new();
#endif

		#endregion

		protected virtual Task OnBeforeAllTests() => Task.CompletedTask;

		[OneTimeSetUp]
		protected async Task BeforeAllTests()
		{
			// We use the name of the .NET runtime version as part of the name of the container, in order to run the tests on multiple targets concurrently.
			// ex: for .NET 10, the container will use the "-net10.0" suffix
			var targetFrameworkVersion = GetRuntimeFrameworkVersion();
			var targetMoniker = string.CreateInvariant($"net{targetFrameworkVersion.Major}.{targetFrameworkVersion.Minor}");

			// We also use fdb version present in the docker image tag, in order to be able to test on multiple versions of fdb concurrently (that may use a different on-disk format).
			// the tag will start with the version number, which we will use to have a different set of containers for the various version (73, 74, ...)
			// ex: if the tag is "7.3.68", then the container name will be "fdb-test-7.3-net10". For the tag "7.4.6" it will be "fdb-test-7.4-net10"
			var dockerImageTag = Environment.GetEnvironmentVariable("FDB_TEST_DOCKER_TAG");
			if (string.IsNullOrEmpty(dockerImageTag)) dockerImageTag = DockerImageTag74;

			if (!Version.TryParse(dockerImageTag, out var targetServerVersion))
			{
				Assert.Fail($"Failed to parse docker tag '{dockerImageTag}' to determine the version of the FoundationDB server.");
				return;
			}

			// Suffix that we will use the for container and data volumes. ex: "-7.3-net9.0" or "-7.4-net10.0"
			var containerSuffix = string.CreateInvariant($"-{targetServerVersion.Major}.{targetServerVersion.Minor}-{targetMoniker}");

			//Note: We need a solution to allocate a dynamic port for the container _before_ starting the container itself, since we need to inject env variables with the port.
			// We cannot use the dynamic port allocation of the builder itself, since we would know the port after the start when it's too late!
			// => we will combine the versions of the .NET runtime and fdb container version into a pseudo hash function that spreads the port over a range of 4600..4699
			// => we ASSUME that the minor versions of both .NET and FDB will always be 0-9 (ie: no version 7.12)

			// Formula:
			// P = SRV.Major * 10 + SRV.Minor    // P =  7 * 10 + 4 = 74
			// Q = FX.Major * 10 + FX.Minor      // Q = 10 * 10 + 0 = 100
			// H = (P * 13 + Q * 17) % 100       // H = (74 * 13 + 100 * 17) % 100 = 2662 % 100 = 62
			// PORT = 4600 + H                   // PORT = 4600 + 62 = 4662
			int port = 4600 + (((10 * targetServerVersion.Major + targetServerVersion.Minor) * 13) + ((10 * targetFrameworkVersion.Major + targetFrameworkVersion.Minor) * 17)) % 100;

			// Some common values:
			// | fdb | dotnet  | Port |
			// +-----+---------+------+
			// | 7.3 | net8.0  | 4609 |
			// | 7.3 | net9.0  | 4679 |
			// | 7.3 | net10.0 | 4649 |
			// | 7.4 | net8.0  | 4622 |
			// | 7.4 | net9.0  | 4692 |
			// | 7.4 | net10.0 | 4662 |
			// | 7.4 | net11.0 | 4632 |

			var name = "fdb-test" + containerSuffix;
			var volumeName = "fdb-test" + containerSuffix;

			bool mustStartServer = false;
			var container = FdbTest.ServerContainer;

			if (container is null)
			{
				// lock this, in case the RunSettings are ignored, and multiple threads start at the same time
				lock (Lock)
				{
					container = FdbTest.ServerContainer;
					if (container == null)
					{
						container = new(name, dockerImageTag, port, volumeName);
						FdbTest.ServerContainer = container;
						mustStartServer = true;
					}
				}
			}

			if (mustStartServer)
			{ // we won the race, we are responsible for starting the Docker Container and setting up FoundationDB in this process

				try
				{
					var probe = FdbClientNativeExtensions.ProbeNativeLibraryPaths();
					if (probe.Path == null)
					{
						Assert.Fail($"Could not locate the native client library for platform '{probe.Rid}'. Looked in the following places: {string.Join(", ", probe.ProbedPaths)}");
						return;
					}

					Fdb.Options.NativeLibPath = probe.Path;

					// We must ensure that FDB is running before executing the tests
					// => By default, we always use 
					if (Fdb.ApiVersion == 0)
					{
						int version = OverrideApiVersion;
						if (version == 0) version = Fdb.GetDefaultApiVersion();
						if (version > Fdb.GetMaxApiVersion())
						{
							Assume.That(version, Is.LessThanOrEqualTo(Fdb.GetMaxApiVersion()), "Unit tests require that the native fdb client version be at least equal to the current binding version!");
						}

						Fdb.Start(version);
					}
					else if (OverrideApiVersion != 0 && OverrideApiVersion != Fdb.ApiVersion)
					{
						//note: cannot change API version on the fly! :(
						Assume.That(Fdb.ApiVersion, Is.EqualTo(OverrideApiVersion), "The API version selected is not what this test is expecting!");
					}

					// only allow ~20 seconds for the container to start
					// => most common failure is when Docker Desktop has not started yet on the local machine!
					await container.StartContainer(TimeSpan.FromSeconds(20), this.Cancellation).ConfigureAwait(false);

					Log("FDB Test Server is ready!");
					FdbTest.ServerReadySignal.TrySetResult();
				}
				catch (Exception e)
				{
					LogError("FDB Test Server failed to start!", e);
					Assert.Warn($"Failed to start docker container '{container.Id}': [{e.GetType().Name}] {e.Message}");
					FdbTest.ServerReadySignal.TrySetException(e);
				}
			}

			// call the hook if defined on the derived test class
			await OnBeforeAllTests();
		}

		[OneTimeTearDown]
		protected void AfterAllTests()
		{
			// call the hook if defined on the derived test class
			OnAfterAllTests().GetAwaiter().GetResult();
		}

		protected virtual Task OnAfterAllTests() => Task.CompletedTask;

		protected FdbServerTestContainer GetLocalServer()
		{
			var server = FdbTest.ServerContainer;
			Assume.That(server, Is.Not.Null, "Local test server container was not started!?");
			return server!;
		}

		/// <summary>Returns the Major.Minor version of the currently running framework (<c>8.0</c>, <c>9.0</c>, <c>10.0</c>, ...)</summary>
		protected static Version GetRuntimeFrameworkVersion()
		{
			return new Version(Environment.Version.Major, Environment.Version.Minor);
		}

		private async Task<FdbServerTestContainer> WaitForTestServerToBecomeReady()
		{
			this.Cancellation.ThrowIfCancellationRequested();

			await FdbTest.ServerReadySignal.Task.WaitAsync(TimeSpan.FromSeconds(20), this.Cancellation);

			return FdbTest.ServerContainer ?? throw new InvalidOperationException("FDB Test Server was not started properly?");
		}

		/// <summary>Connect to the local test database</summary>
		//[DebuggerStepThrough]
		protected async Task<IFdbDatabase> OpenTestDatabaseAsync(bool readOnly = false)
		{
			var server = await this.WaitForTestServerToBecomeReady();

			var options = new FdbConnectionOptions
			{
				ConnectionString = server.ConnectionString,
				Root = FdbPath.Root, // core tests cannot rely on the DirectoryLayer!
				DefaultTimeout = TimeSpan.FromSeconds(15),
				ReadOnly = readOnly,
			};

			return await Fdb.OpenAsync(options, this.Cancellation);
		}

		/// <summary>Connect to the local test database</summary>
		//[DebuggerStepThrough]
		protected async Task<IFdbDatabase> OpenTestPartitionAsync([CallerMemberName] string? testMethod = null)
		{
			// We already use a dedicated fdbserver docker image for each .NET runtime version, so we are isolated
			// from other processes that would be spawn by test runners that execute the same test suites for 
			// multiple .NET framework concurrently (ex: ReSharper when using "In all target frameworks" option).
			// 
			// We only have to protect against concurrent executions of other test classes interfering with each other by using the caller's name has a subdirectory name.

			var testSuite = GetType().GetFriendlyName();
			if (testSuite.EndsWith("Facts")) testSuite = testSuite[..^("Facts".Length)];

			var path = FdbPath.Root[FdbPathSegment.Partition("Tests")][testSuite];

			if (!string.IsNullOrEmpty(testMethod))
			{
				if (testMethod.StartsWith("Test_")) testMethod = testMethod["Test_".Length..];
				path = path[testMethod, "test"];
			}

			var container = await WaitForTestServerToBecomeReady().ConfigureAwait(false);

			var options = new FdbConnectionOptions
			{
				ConnectionString = container.ConnectionString,
				Root = path,
				DefaultTimeout = TimeSpan.FromSeconds(15),
			};

			return await Fdb.OpenAsync(options, this.Cancellation);
		}

		[DebuggerStepThrough]
		protected Task CleanLocation(IFdbDatabaseProvider db)
		{
			Assert.That(db.Root.IsPartition, Is.False);
			return CleanLocation(db, db.Root);
		}

		[DebuggerStepThrough]
		protected Task CleanLocation(IFdbDatabaseProvider db, FdbPath path)
		{
			return CleanLocation(db, db.Root[path]);
		}

		[DebuggerStepThrough]
		protected async Task CleanLocation(IFdbDatabaseProvider db, ISubspaceLocation location)
		{
			await CleanLocation(await db.GetDatabase(this.Cancellation), location);
		}

		[DebuggerStepThrough]
		protected Task CleanLocation(IFdbDatabase db)
		{
			Assert.That(db.Root.IsPartition, Is.False);
			return CleanLocation(db, db.Root);
		}

		[DebuggerStepThrough]
		protected Task CleanLocation(IFdbDatabase db, FdbPath path)
		{
			return CleanLocation(db, db.Root[path]);
		}

		[DebuggerStepThrough]
		protected Task CleanLocation(IFdbDatabase db, ISubspaceLocation location)
		{
			Log($"# Using location {location.Path}");
			Assert.That(db, Is.Not.Null, "null db");
			if (location.Path.Count == 0 && location.Prefix.Count == 0)
			{
				Assert.Fail("Cannot clean the root of the database!");
			}

			// if the prefix part is empty, then we simply recursively remove the corresponding subdirectory tree
			// If it is not empty, we only remove the corresponding subspace (without touching the subdirectories!)

			return db.WriteAsync(async tr =>
			{
				tr.StopLogging();

				if (location.Path.Count == 0)
				{ // subspace under the root of the partition

					// get and clear subspace
					tr.ClearRange(KeyRange.StartsWith(location.Prefix));
				}
				else if (location.Prefix.Count == 0)
				{
					// remove previous
					await db.DirectoryLayer.TryRemoveAsync(tr, location.Path);

					// create new
					_ = await db.DirectoryLayer.CreateAsync(tr, location.Path);
				}
				else
				{ // subspace under a directory subspace

					// make sure the parent path exists!
					var subspace = await db.DirectoryLayer.CreateOrOpenAsync(tr, location.Path);

					// get and clear subspace
					tr.ClearRange(subspace.Bytes(location.Prefix).ToRange(inclusive: true));
				}
			}, this.Cancellation);
		}

		[DebuggerStepThrough]
		protected Task CleanSubspace(IFdbDatabase db, IKeySubspace subspace)
		{
			Assert.That(subspace, Is.Not.Null, "null db");
			Assert.That(subspace.GetPrefix(), Is.Not.EqualTo(Slice.Empty), "Cannot clean the root of the database!");

			return db.WriteAsync(tr => tr.ClearRange(subspace), this.Cancellation);
		}

		[DebuggerStepThrough]
		protected async Task DumpSubspace(IFdbDatabase db, IKeySubspace subspace)
		{
			Assert.That(db, Is.Not.Null);

			using var tr = db.BeginTransaction(this.Cancellation);

			tr.StopLogging();
			await DumpSubspace(tr, subspace).ConfigureAwait(false);
		}

		[DebuggerStepThrough]
		protected Task DumpSubspace(IFdbDatabase db, bool recursive = true)
		{
			return DumpSubspace(db, db.Root, recursive);
		}

		[DebuggerStepThrough]
		protected async Task DumpSubspace(IFdbDatabase db, ISubspaceLocation path, bool recursive = true)
		{
			Assert.That(db, Is.Not.Null);

			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				tr.StopLogging();

				var subspace = await path.TryResolve(tr);
				if (subspace == null)
				{
					Log($"Dumping content of subspace {path}:");
					Log("> EMPTY!");
					return;
				}

				await DumpSubspace(tr, subspace).ConfigureAwait(false);

				if (recursive && path.Prefix.Count == 0)
				{
					var names = await db.DirectoryLayer.TryListAsync(tr, path.Path);
					if (names != null)
					{
						foreach (var name in names)
						{
							var child = await db.DirectoryLayer.TryOpenAsync(tr, name);
							if (child != null)
							{
								await DumpSubspace(tr, child);
							}
						}
					}
				}
			}
		}

		[DebuggerStepThrough]
		protected async Task DumpSubspace(IFdbReadOnlyTransaction tr, IKeySubspace subspace)
		{
			Assert.That(tr, Is.Not.Null);
			Assert.That(subspace, Is.Not.Null);

			var sb = new StringBuilder();

			sb.AppendLine($"Dumping content of {subspace} at {subspace.GetPrefix():K}:");

			int count = 0;
			await tr
				.GetRange(KeyRange.StartsWith(subspace.GetPrefix()))
				.ForEachAsync((kvp) =>
				{
					var key = subspace.ExtractKey(kvp.Key, boundCheck: true);
					++count;
					string keyDump;
					try
					{
						// attempts decoding it as a tuple
						keyDump = TuPack.Unpack(key).ToString()!;
					}
					catch (Exception)
					{
						// not a tuple, dump as bytes
						keyDump = $"'{key}'";
					}
						
					sb.AppendLine($"- {keyDump} = {kvp.Value}");
				});

			if (count == 0)
			{
				sb.AppendLine("> empty !");
			}
			else
			{
				sb.AppendLine($"> Found {count:N0} values");
			}
			Log(sb.ToString());
		}

		[DebuggerStepThrough]
		protected async Task DumpSubspace(IFdbReadOnlyTransaction tr, ISubspaceLocation location)
		{
			var subspace = await location.TryResolve(tr);
			if (subspace != null)
			{
				await DumpSubspace(tr, subspace);
			}
			else
			{
				Log($"# Location {location} not found!");
			}
		}

		[DebuggerStepThrough]
		protected async Task DeleteSubspace(IFdbDatabase db, IKeySubspace subspace)
		{
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				tr.ClearRange(subspace);
				await tr.CommitAsync();
			}
		}

		[DebuggerStepThrough]
		protected async Task DumpTree(IFdbDatabase db, FdbDirectorySubspaceLocation path)
		{
			Assert.That(db, Is.Not.Null);

			Log($"# Tree of {path}:");

			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				tr.StopLogging();

				await ProcessFolder(tr, path, 0);
			}

			Log();


			static async Task ProcessFolder(IFdbReadOnlyTransaction tr, FdbDirectorySubspaceLocation path, int depth)
			{
				var indent = new string('\t', depth);

				var subspace = await path.TryResolve(tr);
				if (subspace == null)
				{
					Log($"# {indent}- {path} => NOT FOUND");
					return;
				}

				long n = await tr.GetEstimatedRangeSizeBytesAsync(subspace.ToRange());

				Log($"# {indent}- {subspace.Path[^1]} at {TuPack.Unpack(subspace.GetPrefix())} {(n == 0 ? "<empty>" : $"~{n:N0} bytes")}");

				var names = await tr.Database.DirectoryLayer.TryListAsync(tr, path.Path);
				if (names != null)
				{
					foreach (var name in names)
					{
						await ProcessFolder(tr, path[name[^1]], depth + 1);
					}
				}
			}
		}

		#region Read/Write Helpers...

		protected Task<T> DbRead<T>(IFdbRetryable db, Func<IFdbReadOnlyTransaction, Task<T>> handler)
		{
			return db.ReadAsync(handler, this.Cancellation);
		}

		protected Task<List<T>> DbQuery<T>(IFdbRetryable db, Func<IFdbReadOnlyTransaction, IAsyncQuery<T>> handler)
		{
			return db.QueryAsync(handler, this.Cancellation);
		}

		protected Task DbWrite(IFdbRetryable db, Action<IFdbTransaction> handler)
		{
			return db.WriteAsync(handler, this.Cancellation);
		}

		protected Task DbWrite(IFdbRetryable db, Func<IFdbTransaction, Task> handler)
		{
			return db.WriteAsync(handler, this.Cancellation);
		}

		protected Task DbVerify(IFdbRetryable db, Func<IFdbReadOnlyTransaction, Task> handler)
		{
			return db.ReadAsync(async (tr) => { await handler(tr); return true; }, this.Cancellation);
		}

		#endregion

		#region IFdbDatabaseProvider helpers...

		/// <summary>Service provider that is shared by all methods of this test class</summary>
		/// <remarks>Can be overriden by calling <see cref="ConfigureCommonServices"/> during the setup stage of the test method</remarks>
		private IServiceCollection? SharedServices { get; set; }

		private IServiceProvider? LocalServices { get; set; }

		protected void ConfigureCommonServices(Action<IServiceCollection>? configure = null)
		{
			Assume.That(this.SharedServices, Is.Null, "Common services can only be configured once per test class!");
			var server = FdbTest.ServerContainer!;
			Assume.That(server, Is.Not.Null, "FDB Test Server was not started properly");
			Assume.That(Fdb.ApiVersion, Is.GreaterThan(0), "The fdb API version was not configured properly!");

			var services = new ServiceCollection();
			services.AddOptions().AddLogging();
			services.AddSingleton<IClock>(this.Clock);
			services.AddFoundationDb(Fdb.ApiVersion, (options) =>
			{
				options.ConnectionOptions.ConnectionString = server.ConnectionString;
			});

			configure?.Invoke(services);

			this.SharedServices = services;
		}

		protected IServiceProvider ConfigureServices(Action<IServiceCollection>? configure = null)
		{
			Assume.That(this.LocalServices, Is.Null, "Local services can only be configured once per test method!");
			var server = FdbTest.ServerContainer!;
			Assume.That(server, Is.Not.Null, "FDB Test Server was not started properly");

			var services = new ServiceCollection();
			if (this.SharedServices is null)
			{
				ConfigureCommonServices();
			}
			services.Add(this.SharedServices!);

			configure?.Invoke(services);

			var provider = services.BuildServiceProvider();
			this.LocalServices = provider;
			return provider;
		}

		protected IServiceProvider GetServices() => this.LocalServices ?? ConfigureServices();

		protected T GetRequiredService<T>() where T : notnull => GetServices().GetRequiredService<T>();

		/// <summary>Return the database provider for this test</summary>
		protected IFdbDatabaseProvider GetDatabaseProvider() => GetRequiredService<IFdbDatabaseProvider>();

		/// <summary>Resolve the database instance that can be used by this test</summary>
		protected async ValueTask<IFdbDatabase> GetDatabaseAsync()
		{
			await FdbTest.ServerReadySignal.Task.WaitAsync(TimeSpan.FromSeconds(10), this.Cancellation);

			var dbProvider = GetDatabaseProvider();
			var db = await dbProvider.GetDatabase(this.Cancellation);
			//Log("# Using database location /{0}", db.Root.Path);
			db.SetDefaultLogHandler(null);
			return db;
		}

		/// <summary>Run an idempotent transactional block that returns a value, inside a read-write transaction, which can be executed more than once if any retry-able error occurs.</summary>
		protected Task WriteAsync(Func<IFdbTransaction, Task> handler)
			=> GetDatabaseProvider().WriteAsync(handler, this.Cancellation);

		/// <summary>Run an idempotent transaction block inside a write-only transaction, which can be executed more than once if any retry-able error occurs.</summary>
		protected Task WriteAsync(Action<IFdbTransaction> handler)
			=> GetDatabaseProvider().WriteAsync(handler, this.Cancellation);

		/// <summary>Runs a transactional lambda function inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		protected Task<TResult> ReadAsync<TResult>(Func<IFdbReadOnlyTransaction, Task<TResult>> handler)
			=> GetDatabaseProvider().ReadAsync(handler, this.Cancellation);

		/// <summary>Run an idempotent transactional block that returns a value, inside a read-write transaction, which can be executed more than once if any retry-able error occurs.</summary>
		protected Task<TResult> ReadWriteAsync<TResult>(Func<IFdbTransaction, Task<TResult>> handler)
			=> GetDatabaseProvider().ReadWriteAsync(handler, this.Cancellation);

		/// <summary>Runs a transactional lambda function inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		protected Task<List<TResult>> QueryAsync<TResult>(Func<IFdbReadOnlyTransaction, IAsyncQuery<TResult>> handler)
			=> GetDatabaseProvider().QueryAsync(handler, this.Cancellation);

		/// <summary>Runs a transactional lambda function inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		protected Task<List<TResult>> QueryAsync<TResult>(Func<IFdbReadOnlyTransaction, Task<IAsyncQuery<TResult>>> handler)
			=> GetDatabaseProvider().QueryAsync(handler, this.Cancellation);

		#endregion

	}

}
