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

//#define DEBUG_THREADS

namespace FoundationDB.Client
{
	using SystemIO = System.IO;
	using FoundationDB.Client.Native;
	using FoundationDB.DependencyInjection;

	/// <summary>FoundationDB binding</summary>
	[PublicAPI]
	public static partial class Fdb
	{

		#region Constants...

		/// <summary>Keys cannot exceed 10,000 bytes</summary>
		internal const int MaxKeySize = 10 * 1000;

		/// <summary>Values cannot exceed 100,000 bytes</summary>
		internal const int MaxValueSize = 100 * 1000;

		/// <summary>Maximum size of total written keys and values by a transaction</summary>
		internal const int MaxTransactionWriteSize = 10 * 1024 * 1024;

		/// <summary>Minimum API version supported by this binding</summary>
		internal const int MinSafeApiVersion = FdbNative.FDB_API_MIN_VERSION;

		/// <summary>Highest API version that this binding can support</summary>
		internal const int MaxSafeApiVersion = FdbNative.FDB_API_MAX_VERSION;

		/// <summary>Default API version that will be selected, if the application does not specify otherwise.</summary>
		internal const int DefaultApiVersion = 730; // v7.3.x
		//INVARIANT: MinSafeApiVersion <= DefaultApiVersion <= MaxSafeApiVersion

		#endregion

		#region Members...

		/// <summary>Flag indicating if FDB has been initialized or not</summary>
		private static bool s_started; //REVIEW: replace with state flags (Starting, Started, Failed, ...)

		/// <summary>Currently selected API version</summary>
		private static int s_apiVersion;

		/// <summary>Max API version of the native binding</summary>
		private static int s_bindingVersion;

		/// <summary>Event handler called when the AppDomain gets unloaded</summary>
		private static EventHandler? s_appDomainUnloadHandler;

		#endregion

		/// <summary>Returns the minimum API version currently supported by this binding.</summary>
		/// <remarks>Attempts to select an API version lower than this value will fail.</remarks>
		public static int GetMinApiVersion()
		{
			return MinSafeApiVersion;
		}

		/// <summary>Returns the maximum API version currently supported by the installed client.</summary>
		/// <remarks>The version of the installed client (fdb_c.dll) can be different higher (or lower) than the version supported by this binding (FoundationDB.Client.dll)!
		/// If you want the highest possible version that is supported by both the binding and the client, you must call <see cref="GetMaxSafeApiVersion()"/>.
		/// Attempts to select an API version higher than this value will fail.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int GetMaxApiVersion()
		{
			return FdbNative.GetMaxApiVersion();
		}

		/// <summary>Returns the maximum API version that is supported by both this binding and the installed client.</summary>
		/// <returns>Value that can be safely passed to <see cref="Start(int)"/>, if you want to be on the bleeding edge.</returns>
		/// <remarks>This value can be lower than the value returned by <see cref="GetMaxApiVersion"/> if the FoundationDB client installed on this machine is more recent that the version of this assembly.
		/// Using this version may break your application if new features change the behavior of the client (ex: default mode for snapshot transactions between v2.x and v3.x).
		/// </remarks>
		public static int GetMaxSafeApiVersion()
		{
			return Math.Min(MaxSafeApiVersion, GetMaxApiVersion());
		}

		/// <summary>Returns the maximum API version that is supported by both this binding and the installed client.</summary>
		/// <returns>Value that can be safely passed to <see cref="Start(int)"/>, if you want to be on the bleeding edge.</returns>
		/// <remarks>This value can be lower than the value returned by <see cref="GetMaxApiVersion"/> if the FoundationDB client installed on this machine is more recent that the version of this assembly.
		/// Using this version may break your application if new features change the behavior of the client (ex: default mode for snapshot transactions between v2.x and v3.x).
		/// </remarks>
		/// <exception cref="NotSupportedException">If the max safe version is lower than <paramref name="minVersion"/> or higher than <paramref name="maxVersion"/></exception>
		public static int GetMaxSafeApiVersion(int minVersion, int? maxVersion = null)
		{
			int maxSupported = GetMaxApiVersion();
			int version = Math.Min(MaxSafeApiVersion, maxSupported);
			EnsureApiVersion(version, minVersion, maxVersion);
			return version;
		}

		/// <summary>Returns the default API version that is supported by the version of this binding</summary>
		/// <remarks>
		/// The version may be different from the version supported by the installed client, and the database cluster itself!
		/// This version should only be used by tools that are versioned and deployed alongside the binding package.
		/// Application and Layers should define their own API version and not rely on this value.
		/// </remarks>
		public static int GetDefaultApiVersion()
		{
			return Fdb.DefaultApiVersion;
		}

		/// <summary>Returns the currently selected API version.</summary>
		/// <remarks>
		/// The value will be 0 if <see cref="Fdb.Start(int)"/> has not been called yet.
		/// If less than <see cref="BindingVersion"/>, some features will be emulated by the native binding.</remarks>
		public static int ApiVersion => s_apiVersion;

		/// <summary>Returns the maximum API version supported by the currently loaded native binding</summary>
		/// <remarks>The value will be 0 if <see cref="Fdb.Start(int)"/> has not been called yet.</remarks>
		public static int BindingVersion => s_bindingVersion;

		/// <summary>Sets the desired API version of the binding.
		/// The selected version level may affect the availability and behavior or certain features.
		/// </summary>
		/// <remarks>
		/// The version can only be set before calling <see cref="Fdb.Start(int)"/> or any method that indirectly calls it.
		/// If the value is 0, then the maximum version supported by both this binding and the FoundationDB client (see <see cref="GetMaxSafeApiVersion()"/>).
		/// If you want to be conservative, you should target a specific version level, and only change to newer versions after making sure that all tests are passing!
		/// </remarks>
		/// <exception cref="InvalidOperationException">When attempting to change the API version after the binding has been started.</exception>
		/// <exception cref="ArgumentException">When attempting to set a negative version, or a version that is either less or greater than the minimum and maximum supported versions.</exception>
		[Obsolete("Use Fdb.Start(int) to specify the API version", true)]
		public static void UseApiVersion(int value)
		{
			value = CheckApiVersion(value);
			if (s_apiVersion == value) return; // Already set to same version... skip it.
			if (s_started) throw new InvalidOperationException($"You cannot set API version {value} because version {s_apiVersion} has already been selected");
			s_apiVersion = value;
		}

		private static int CheckApiVersion(int value)
		{
			if (value < 0) throw new ArgumentException("API version must be a positive integer.");
			if (value == 0)
			{ // 0 means "use the default version"
				value = GetMaxSafeApiVersion();
			}

			//note: we don't actually select the version yet, only when Start() is called.

			int min = GetMinApiVersion();
			if (value < min) throw new ArgumentException($"The minimum API version supported by the native fdb client is {min}, which is higher than version {value} requested by the application. You must upgrade the application and/or .NET binding!");
			int max = GetMaxApiVersion();
			if (value > max) throw new ArgumentException($"The maximum API version supported by the native fdb client is {max}, which is lower than version {value} required by the application. You must upgrade the native fdb client to a higher version!");

			return value;
		}

		/// <summary>Ensure that the currently selected <see cref="ApiVersion"/> is between the specified bounds</summary>
		/// <param name="min">Minimum version that is supported by the caller. If the current version is lower, an exception will be thrown</param>
		/// <param name="max">If not null, maximum version that is supported by the caller. If the current version is higher, an exception will be thrown</param>
		/// <exception cref="NotSupportedException">If the current version does not match the specified range</exception>
		public static void EnsureApiVersion(int min, int? max = null)
		{
			//TODO: add overload that takes a Range? (C# 8.0)
			if (ApiVersion <= 0) throw new InvalidOperationException("The fdb API version must be set before calling this method.");
			EnsureApiVersion(ApiVersion, min, max);
		}

		private static void EnsureApiVersion(int version, int min, int? max = null)
		{
			//TODO: add overload that takes a Range? (C# 8.0)
			if (min > 0 && min > version) throw new NotSupportedException($"The current fdb API version is {version}, which is lower than the minimum version {min} required by the caller.");
			if (max != null && max.Value < version) throw new NotSupportedException($"The current fdb API version is {version}, which is higher than the maximum version {max.Value} required by the caller.");
		}

		/// <summary>Returns the version of the client library, git commit hash of the build, and the supported protocol version</summary>
		public static (string Version, string Protocol, string CommitHash) GetClientVersion()
		{
			if (!s_started) throw new InvalidOperationException("You must first select an API version by calling Fdb.Start(...) before this method.");

			// the string will look something like:
			// - "7.1.29,1b2517abce552441e3d0ed8836d1cc3f40e61a2a,fdb00b071010000"
			// - "7.3.36,f9a42ee0e79763cf4f38dd84d9e554f0b128b6b0,fdb00b073000000"

			var s = FdbNative.GetClientVersion();

			// the first part is the human-readable version, next is the commit hash of the build, and then the protocol version
			var parts = s.Split(',');
			if (parts.Length != 3)
			{
				throw new InvalidOperationException($"Unrecognized client version format: '{s}'");
			}

			return (parts[0], parts[2], parts[1]);
		}

		#region Network Thread / Event Loop...

		private static Thread? s_eventLoop;
		private static bool s_eventLoopStarted;
		private static bool s_eventLoopRunning;
		private static bool s_eventLoopStopRequested;
		private static int? s_eventLoopThreadId;

		/// <summary>Starts the thread running the FDB event loop</summary>
		private static void StartEventLoop()
		{
			if (s_eventLoop == null)
			{
				if (Logging.On) Logging.Verbose(typeof(Fdb), "StartEventLoop", "Starting network thread...");

				s_eventLoopStarted = false;
				s_eventLoopStopRequested = false;
				s_eventLoopRunning = false;

				var thread = new Thread(EventLoop)
				{
					Name = "FdbNetworkLoop",
					IsBackground = true,
					Priority = ThreadPriority.AboveNormal
				};
				s_eventLoop = thread;
				try
				{
					thread.Start();
					s_eventLoopStarted = true;
				}
				catch (Exception e)
				{
					s_eventLoopStarted = false;
					s_eventLoop = null;
					if (Logging.On) Logging.Exception(typeof(Fdb), "StartEventLoop", e);
					throw;
				}
			}
		}

		/// <summary>Stops the thread running the FDB event loop</summary>
		private static void StopEventLoop()
		{
			if (s_eventLoopStarted)
			{

				// We cannot be called from the network thread itself, or else we will deadlock !
				Fdb.EnsureNotOnNetworkThread();

				if (Logging.On) Logging.Verbose(typeof(Fdb), "StopEventLoop", "Stopping network thread...");

				s_eventLoopStopRequested = true;

				var err = FdbNative.StopNetwork();
				if (err != FdbError.Success)
				{
					if (Logging.On) Logging.Warning(typeof(Fdb), "StopEventLoop", $"Failed to stop event loop: {err.ToString()}");
				}
				s_eventLoopStarted = false;

				var thread = s_eventLoop;
				if (thread != null && thread.IsAlive)
				{
					// BUGBUG: specs says that we need to wait for the network thread to stop gracefully, or else data integrity may not be guaranteed...
					// We should wait for a bit, and only attempt to Abort() the thread after a timeout (30sec ? more ?)

					// keep track of how much time it took to stop...
					var duration = Stopwatch.StartNew();

					try
					{
						//TODO: replace with a ManualResetEvent that would get signaled at the end of the event loop ?
						while (thread.IsAlive && duration.Elapsed.TotalSeconds < 5)
						{
							// wait a bit...
							Thread.Sleep(100);
						}

						if (thread.IsAlive)
						{
							if (Logging.On) Logging.Warning(typeof(Fdb), "StopEventLoop", $"The fdb network thread has not stopped after {duration.Elapsed.TotalSeconds:N0} seconds. Forcing shutdown...");

							// Force a shutdown
#if !NET6_0_OR_GREATER
							thread.Abort();
#endif

							bool stopped = thread.Join(TimeSpan.FromSeconds(30));
							//REVIEW: is this even useful? If the thread is stuck in a native P/Invoke call, it won't get notified until it returns to managed code ...
							// => in that case, we have a zombie thread on our hands...

							if (!stopped)
							{
								if (Logging.On) Logging.Warning(typeof(Fdb), "StopEventLoop", $"The fdb network thread failed to stop after more than {duration.Elapsed.TotalSeconds:N0} seconds. Transaction integrity may not be guaranteed.");
							}
						}
					}
					catch (ThreadAbortException)
					{
						// Should not happen, unless we are called from a thread that is itself being stopped ?
					}
					finally
					{
						s_eventLoop = null;
						duration.Stop();
						if (duration.Elapsed.TotalSeconds >= 20)
						{
							if (Logging.On) Logging.Warning(typeof(Fdb), "StopEventLoop", $"The fdb network thread took a long time to stop ({duration.Elapsed.TotalSeconds:N0} seconds).");
						}
					}
				}
			}
		}

		/// <summary>Entry point for the Network Thread</summary>
		private static void EventLoop()
		{
			//TODO: we need to move the crash handling logic outside this method, so that an app can hook up an event and device what to do: crash or keep running (dangerous!).
			// At least, the app would get the just to do some emergency shutdown logic before letting the process die....

			try
			{
				s_eventLoopRunning = true;

				s_eventLoopThreadId = Environment.CurrentManagedThreadId;

				if (Logging.On) Logging.Verbose(typeof(Fdb), "EventLoop", $"FDB Event Loop running on thread #{Fdb.s_eventLoopThreadId.Value}...");

				var err = FdbNative.RunNetwork();
				if (err != FdbError.Success)
				{
					if (s_eventLoopStopRequested || Environment.HasShutdownStarted)
					{ // this was requested, or can be explained by the computer shutting down...
						if (Logging.On) Logging.Info(typeof(Fdb), "EventLoop", $"The fdb network thread returned with error code {err}: {FdbNative.GetErrorMessage(err)}");
					}
					else
					{ // this was NOT expected !
						if (Logging.On) Logging.Error(typeof(Fdb), "EventLoop", $"The fdb network thread returned with error code {err}: {FdbNative.GetErrorMessage(err)}");
#if DEBUG
						Console.Error.WriteLine("THE FDB NETWORK EVENT LOOP HAS FAILED!");
						Console.Error.WriteLine("=> " + err);
						// REVIEW: should we FailFast in release mode also?
						// => this may be a bit surprising for most users when applications unexpectedly crash for no apparent reason.
						Environment.FailFast("The FoundationDB Network Event Loop failed with error " + err + " and was terminated.");
#endif
					}
				}
			}
			catch (Exception e)
			{
#if !NET6_0_OR_GREATER
				if (e is ThreadAbortException)
				{ // some other thread tried to Abort() us. This probably means that we should exit ASAP...
					Thread.ResetAbort();
					return;
				}
#endif

				//note: any error is this thread is BAD NEWS for the process, the network thread usually cannot be restarted safely.

				if (e is AccessViolationException)
				{
					// An access violation occured inside the native code. This good be caused by:
					// - a bug in fdb_c.dll
					// - a bug in our own marshalling code that calls into fdb_c.dll
					// - some other random heap corruption that caused us to pass bogus data to fdb_c.dll
					// - a random cosmic ray that flipped some bits in memory...
					if (Debugger.IsAttached) Debugger.Break();

					// This error is VERY BAD NEWS, and means that we CANNOT continue safely running fdb in this process
					// The only reasonable option is to exit the process immediately !

					Console.Error.WriteLine("THE FDB NETWORK EVENT LOOP HAS CRASHED!");
					Console.Error.WriteLine("=> " + e.ToString());
					Environment.FailFast("The FoundationDB Network Event Loop crashed with an Access Violation, and had to be terminated. You may try to create full memory dumps, as well as attach a debugger to this process (it will automatically break when this problem occurs).", e);
					return;
				}

				if (Logging.On) Logging.Exception(typeof(Fdb), "EventLoop", e);

#if DEBUG
				// if we are running in DEBUG build, we want to get the attention of the developer on this.
				// the best way is to make the test runner explode in midair with a scary looking message!

				Console.Error.WriteLine("THE FDB NETWORK EVENT LOOP HAS CRASHED!");
				Console.Error.WriteLine("=> " + e.ToString());
				// REVIEW: should we FailFast in release mode also?
				// => this may be a bit surprising for most users when applications unexpectedly crash for no apparent reason.
				Environment.FailFast("The FoundationDB Network Event Loop crashed and had to be terminated: " + e.Message, e);
#endif
			}
			finally
			{
				if (Logging.On) Logging.Verbose(typeof(Fdb), "EventLoop", "FDB Event Loop stopped");

				s_eventLoopThreadId = null;
				s_eventLoopRunning = false;
			}
		}

		/// <summary>Returns true if the Network thread start is executing, otherwise false</summary>
		public static bool IsNetworkRunning => s_eventLoopRunning;

		/// <summary>Returns 'true' if we are currently running on the Event Loop thread</summary>
		internal static bool IsNetworkThread
		{
			get
			{
				var eventLoopThreadId = s_eventLoopThreadId;
				return eventLoopThreadId.HasValue && Environment.CurrentManagedThreadId == eventLoopThreadId.Value;
			}
		}

		/// <summary>Throws if the current thread is the Network Thread.</summary>
		/// <remarks>Should be used to ensure that we do not execute tasks continuations from the network thread, to avoid deadlocks.</remarks>
		internal static void EnsureNotOnNetworkThread([CallerMemberName] string? callerMethod = null)
		{
#if DEBUG_THREADS
			if (Logging.On && Logging.IsVerbose)
			{
				Logging.Verbose(null, callerMethod, String.Format("[Executing on thread #{0}]", Environment.CurrentManagedThreadId));
			}
#endif

			if (Fdb.IsNetworkThread)
			{ // cannot commit from same thread as the network loop because it could lead to a deadlock
				FailCannotExecuteOnNetworkThread();
			}
		}

		[DoesNotReturn, StackTraceHidden]
		private static void FailCannotExecuteOnNetworkThread()
		{
#if DEBUG_THREADS
			if (Debugger.IsAttached) Debugger.Break();
#endif
			throw Fdb.Errors.CannotExecuteOnNetworkThread();
		}

		#endregion

		#region Database...

		private static async ValueTask<FdbDatabase> CreateDatabaseInternalAsync(string? clusterFile, FdbDirectoryLayer directory, FdbDirectorySubspaceLocation root, bool readOnly, CancellationToken ct)
		{
			EnsureIsStarted();
			ct.ThrowIfCancellationRequested();

			// "" should also be considered to mean "default cluster file"
			if (string.IsNullOrEmpty(clusterFile)) clusterFile = null;

			//TODO: check the path ? (exists, readable, ...)

			var handler = await FdbNativeDatabase.CreateDatabaseAsync(clusterFile, ct).ConfigureAwait(false);
			return FdbDatabase.Create(handler, directory, root, readOnly, Fdb.NetworkThreadStopped);
		}

		private static async ValueTask<FdbDatabase> CreateDatabaseFromConnectionStringInternalAsync(string connectionString, FdbDirectoryLayer directory, FdbDirectorySubspaceLocation root, bool readOnly, CancellationToken ct)
		{
			EnsureIsStarted();
			ct.ThrowIfCancellationRequested();

			//TODO: can we perform a validation check on the connection string before passing it on ?

			var handler = await FdbNativeDatabase.CreateDatabaseFromConnectionStringAsync(connectionString, ct).ConfigureAwait(false);
			return FdbDatabase.Create(handler, directory, root, readOnly, Fdb.NetworkThreadStopped);
		}

		/// <summary>Create a new connection with the "DB" database on the cluster specified by the default cluster file.</summary>
		/// <param name="ct">Token used to abort the operation</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <exception cref="OperationCanceledException">If the token <paramref name="ct"/> is cancelled</exception>
		/// <remarks>Since connections are not pooled, so this method can be costly and should NOT be called every time you need to read or write from the database. Instead, you should open a database instance at the start of your process, and use it a singleton.</remarks>
		public static Task<IFdbDatabase> OpenAsync(CancellationToken ct = default)
		{
			return OpenInternalAsync(new FdbConnectionOptions(), ct);
		}

		/// <summary>Create a new connection with a database using the specified options</summary>
		/// <param name="options">Connection options used to specify the cluster file, partition path, default timeouts, etc...</param>
		/// <param name="ct">Token used to abort the operation</param>
		/// <returns>Task that will return an FdbDatabase, or an exception</returns>
		/// <exception cref="InvalidOperationException">If <see name="FdbConnectionOptions.DbName"/> is anything other than 'DB'</exception>
		/// <exception cref="OperationCanceledException">If the token <paramref name="ct"/> is cancelled</exception>
		public static Task<IFdbDatabase> OpenAsync(FdbConnectionOptions options, CancellationToken ct)
		{
			Contract.NotNull(options);
			return OpenInternalAsync(options, ct);
		}

		/// <summary>Create a new database handler instance using the specified cluster file, database name, global subspace and read only settings</summary>
		internal static async Task<IFdbDatabase> OpenInternalAsync(FdbConnectionOptions options, CancellationToken ct)
		{
			Contract.Debug.Requires(options != null);
			ct.ThrowIfCancellationRequested();

			string? connectionString = options.ConnectionString;
			string? clusterFile = options.ClusterFile;
			bool readOnly = options.ReadOnly;
			var directory = new FdbDirectoryLayer(SubspaceLocation.Root);
			var root = new FdbDirectorySubspaceLocation(options.Root ?? FdbPath.Root);
			bool hasPartition = root.Path.Count != 0;


			FdbDatabase? db = null;
			bool success = false;
			try
			{
				if (!string.IsNullOrWhiteSpace(connectionString))
				{
					if (Logging.On) Logging.Info(typeof(Fdb), nameof(OpenInternalAsync), $"Connecting to database using connection string '{connectionString}' and root '{root}' ...");
					db = await CreateDatabaseFromConnectionStringInternalAsync(connectionString, directory, root, !hasPartition && readOnly, ct).ConfigureAwait(false);
				}
				else
				{
					if (Logging.On) Logging.Info(typeof(Fdb), nameof(OpenInternalAsync), $"Connecting to database using cluster file '{clusterFile ?? "<default>"}' and root '{root}' ...");
					db = await CreateDatabaseInternalAsync(clusterFile, directory, root, !hasPartition && readOnly, ct).ConfigureAwait(false);
				}

				// set the default options
				if (options.DefaultTimeout != TimeSpan.Zero) db.Options.WithDefaultTimeout(options.DefaultTimeout);
				if (options.DefaultRetryLimit != 0) db.Options.WithDefaultRetryLimit(options.DefaultRetryLimit);
				if (options.DefaultMaxRetryDelay != 0) db.Options.WithDefaultMaxRetryDelay(options.DefaultMaxRetryDelay);
				if (options.DataCenterId != null) db.Options.WithDataCenterId(options.DataCenterId);
				if (options.MachineId != null) db.Options.WithMachineId(options.MachineId);
				db.Options.WithTracing(options.DefaultTracing);

				if (hasPartition)
				{ // open the partition, and switch the root of the db
					await Fdb.Directory.SwitchToNamedPartitionAsync(db, root.Path, ct).ConfigureAwait(false);
				}

				success = true;
				return db;
			}
			finally
			{
				if (!success)
				{
					// cleanup the cluster if something went wrong
					db?.Dispose();
				}
			}
		}

		#endregion

		/// <summary>Ensure that we have loaded the C API library, and that the Network Thread has been started</summary>
		private static void EnsureIsStarted()
		{
			if (s_eventLoopStopRequested)
			{
				throw new InvalidOperationException("The fdb network thread has been stopped.");
			}
			if (!s_eventLoopStarted)
			{
				throw new InvalidOperationException("The fdb API version has not been selected. You must call Fdb.Start(...) in your Main() or Startup class before calling this method.");
			}
		}

		/// <summary>Start the Network Thread, using the pre-selected API version.</summary>
		/// <remarks>If you need a specific API version level, you should call <see cref="Start(int)"/>. Otherwise, the max safe default API version will be selected.</remarks>
		[Obsolete("Use should always specify the desired API version, to prevent any breaking change when updating the FoundationDB .NET Client to a newer version! Change this call to Fdb.Start(int) ensure maximum forward compatibility.", error: true)]
		public static void Start()
		{
			Start(s_apiVersion);
		}

		/// <summary>Start the Network Thread, using the specified API version level</summary>
		/// <param name="apiVersion">API version that will be used by this application. A value of 0 mean "max safe default version".</param>
		/// <remarks>This method can only be called once per process, and the API version cannot be changed until the process restarts.</remarks>
		public static void Start(int apiVersion)
		{
			if (s_started) return;

			//BUGBUG: Specs say we cannot restart the network thread anymore in the process after stopping it ! :(

			s_started = true;

			apiVersion = CheckApiVersion(apiVersion);
			if (Logging.On) Logging.Info(typeof(Fdb), "Start", $"Selecting fdb API version {apiVersion}");

			// we must know the actual version of the C binding in use, because it will change the methods we will call later.
			int bindingVersion = FdbNative.GetMaxApiVersion();

			// select the appropriate API level that the binding will emulate (obviously, cannot be higher than `nativeVersion`
			FdbError err = FdbNative.SelectApiVersion(apiVersion);
			if (err != FdbError.Success)
			{
				if (Logging.On) Logging.Error(typeof(Fdb), "Start", $"Failed to fdb API version {apiVersion}: {err}");

				switch (err)
				{
					case FdbError.ApiVersionNotSupported:
					{ // bad version was selected ?
						// note: we already bound check the values before, so that means that fdb_c.dll is either an older version or an incompatible new version.
						throw new FdbException(err, $"The API version {apiVersion} is not supported by the FoundationDB client library (fdb_c.dll) installed on this system. The binding only supports versions {GetMinApiVersion()} to {bindingVersion}. You either need to upgrade the .NET binding or the FoundationDB client library to a newer version.");
					}
#if DEBUG
					case FdbError.ApiVersionAlreadySet:
					{ // Temporary hack to allow multiple debugging using the cached host process in VS
						Console.Error.WriteLine("FATAL: CANNOT REUSE EXISTING PROCESS! FoundationDB client cannot be restarted once stopped. Current process will be terminated.");
						Environment.FailFast("FATAL: CANNOT REUSE EXISTING PROCESS! FoundationDB client cannot be restarted once stopped. Current process will be terminated.");
						break;
					}
#endif
				}
				FdbNative.DieOnError(err);
			}
			s_apiVersion = apiVersion;
			s_bindingVersion = bindingVersion;

			#region Set all network options...

			if (!string.IsNullOrWhiteSpace(Fdb.Options.TracePath))
			{
				if (Logging.On) Logging.Verbose(typeof(Fdb), "Start", $"Will trace client activity in '{Fdb.Options.TracePath}'");
				// create trace directory if missing...
				if (!SystemIO.Directory.Exists(Fdb.Options.TracePath)) SystemIO.Directory.CreateDirectory(Fdb.Options.TracePath);

				FdbNative.DieOnError(SetNetworkOption(FdbNetworkOption.TraceEnable, Fdb.Options.TracePath));
			}

			if (Fdb.Options.TlsCertificateBytes.Count != 0)
			{
				if (Logging.On) Logging.Verbose(typeof(Fdb), "Start", $"Will load TLS root certificate and private key from memory ({Fdb.Options.TlsCertificateBytes.Count} bytes)");

				FdbNative.DieOnError(SetNetworkOption(FdbNetworkOption.TlsCertBytes, Fdb.Options.TlsCertificateBytes));
			}
			else if (!string.IsNullOrWhiteSpace(Fdb.Options.TlsCertificatePath))
			{
				if (Logging.On) Logging.Verbose(typeof(Fdb), "Start", $"Will load TLS root certificate and private key from '{Fdb.Options.TlsCertificatePath}'");

				FdbNative.DieOnError(SetNetworkOption(FdbNetworkOption.TlsCertPath, Fdb.Options.TlsCertificatePath));
			}

			if (Fdb.Options.TlsPrivateKeyBytes.Count != 0)
			{
				if (Logging.On) Logging.Verbose(typeof(Fdb), "Start", $"Will load TLS private key from memory ({Fdb.Options.TlsPrivateKeyBytes.Count} bytes)");

				FdbNative.DieOnError(SetNetworkOption(FdbNetworkOption.TlsKeyBytes, Fdb.Options.TlsPrivateKeyBytes));
			}
			else if (!string.IsNullOrWhiteSpace(Fdb.Options.TlsPrivateKeyPath))
			{
				if (Logging.On) Logging.Verbose(typeof(Fdb), "Start", $"Will load TLS private key from '{Fdb.Options.TlsPrivateKeyPath}'");

				FdbNative.DieOnError(SetNetworkOption(FdbNetworkOption.TlsKeyPath, Fdb.Options.TlsPrivateKeyPath));
			}

			if (!string.IsNullOrWhiteSpace(Fdb.Options.TlsPassword))
			{
				// let's not write the password in the log files :)

				FdbNative.DieOnError(SetNetworkOption(FdbNetworkOption.TlsPassword, Fdb.Options.TlsPassword));
			}

			if (Fdb.Options.TlsVerificationPattern.Count != 0)
			{
				if (Logging.On) Logging.Verbose(typeof(Fdb), "Start", $"Will verify TLS peers with pattern '{Fdb.Options.TlsVerificationPattern}'");

				FdbNative.DieOnError(SetNetworkOption(FdbNetworkOption.TlsVerifyPeers, Fdb.Options.TlsVerificationPattern));
			}

			if (Fdb.Options.TlsCaBytes.Count != 0)
			{
				if (Logging.On) Logging.Verbose(typeof(Fdb), "Start", $"Will load TLS certificate authority bundle from memory ({Fdb.Options.TlsCaBytes.Count} bytes)");

				FdbNative.DieOnError(SetNetworkOption(FdbNetworkOption.TlsCaBytes, Fdb.Options.TlsCaBytes));
			}
			else if (!string.IsNullOrWhiteSpace(Fdb.Options.TlsCaPath))
			{
				if (Logging.On) Logging.Verbose(typeof(Fdb), "Start", $"Will load TLS certificate authority bundle from '{Fdb.Options.TlsCaPath}'");

				FdbNative.DieOnError(SetNetworkOption(FdbNetworkOption.TlsCaPath, Fdb.Options.TlsCaPath));
			}

			#endregion

			try { }
			finally
			{
				// register with the AppDomain to ensure that everything is cleared when the process exists
				s_appDomainUnloadHandler = (_, _) =>
				{
					if (s_started)
					{
						//note: since app domain is unloading, the logger may also have already stopped...
						if (Logging.On) Logging.Verbose(typeof(Fdb), "AppDomainUnloadHandler", "AppDomain is unloading, stopping FoundationDB Network Thread...");
						Stop();
					}
				};
				AppDomain.CurrentDomain.DomainUnload += s_appDomainUnloadHandler;
				AppDomain.CurrentDomain.ProcessExit += s_appDomainUnloadHandler;

				if (Logging.On) Logging.Verbose(typeof(Fdb), "Start", "Setting up Network Thread...");

				FdbNative.DieOnError(FdbNative.SetupNetwork());
				s_started = true; //BUGBUG: already set at the start of the method. Maybe we need state flags ?

				// hook the callback for when the network thread stops
				Fdb.NetworkThreadLifetime = new CancellationTokenSource();
				FdbNative.AddNetworkThreadCompletionHook(OnNetworkThreadCompletion, IntPtr.Zero);
			}

			if (Logging.On) Logging.Info(typeof(Fdb), "Start", "Network thread has been set up");

			StartEventLoop();
		}

		/// <summary>Use to detect the shutdown of the network thread</summary>
		private static CancellationTokenSource? NetworkThreadLifetime;

		/// <summary>Token that will fire when the network thread stops or is aborted</summary>
		public static CancellationToken NetworkThreadStopped => NetworkThreadLifetime?.Token ?? new CancellationToken(true);

		private static void OnNetworkThreadCompletion(IntPtr paramter)
		{
			if (Logging.On) Logging.Info(typeof(Fdb), "Stop", "Network thread has completed.");
			Fdb.NetworkThreadLifetime?.Cancel();
		}

		/// <summary>Set the value of a network option on the database handler</summary>
		private static FdbError SetNetworkOption(FdbNetworkOption option, string? value)
		{
			unsafe
			{
				var data = FdbNative.ToNativeString(value.AsSpan(), nullTerminated: false);
				fixed (byte* ptr = data)
				{
					return FdbNative.NetworkSetOption(option, ptr, data.Count);
				}
			}
		}

		/// <summary>Set the value of a network option on the database handler</summary>
		private static FdbError SetNetworkOption(FdbNetworkOption option, Slice value)
		{
			return SetNetworkOption(option, value.Span);
		}

		/// <summary>Set the value of a network option on the database handler</summary>
		private static FdbError SetNetworkOption(FdbNetworkOption option, ReadOnlySpan<byte> value)
		{
			unsafe
			{
				fixed (byte* ptr = value)
				{
					return FdbNative.NetworkSetOption(option, ptr, value.Length);
				}
			}
		}

		/// <summary>Registry of the live database instances, so that <see cref="Stop"/> can drain them before stopping the network thread</summary>
		private static readonly global::System.Collections.Concurrent.ConcurrentDictionary<FdbDatabase, byte> s_liveDatabases = new();

		internal static void RegisterDatabase(FdbDatabase db) => s_liveDatabases.TryAdd(db, 0);

		internal static void UnregisterDatabase(FdbDatabase db) => s_liveDatabases.TryRemove(db, out _);

		/// <summary>Maximum time <see cref="Stop"/> waits for the outstanding future callbacks to drain, before stopping the network thread anyway</summary>
		private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(5);

		/// <summary>Cancels and drains every outstanding future BEFORE the network thread goes away</summary>
		/// <remarks>Disposing a database cancels its transactions and watches through the native cascade; the
		/// callbacks then fire on the (still running) network thread and release their cookies. Stopping the network
		/// under armed callbacks would instead leak their cookies and leave the awaiters hanging forever.</remarks>
		private static void DrainOutstandingFutures()
		{
			foreach (var db in s_liveDatabases.Keys)
			{
				try
				{
					db.Dispose();
				}
				catch (Exception e)
				{
					if (Logging.On) Logging.Exception(typeof(Fdb), "Stop", e);
				}
			}

			var sw = global::System.Diagnostics.Stopwatch.StartNew();
			while (Volatile.Read(ref FoundationDB.Client.Utils.DebugCounters.CallbackHandles) != 0)
			{
				if (sw.Elapsed > DrainTimeout)
				{
					if (Logging.On) Logging.Warning(typeof(Fdb), "Stop", $"{Volatile.Read(ref FoundationDB.Client.Utils.DebugCounters.CallbackHandles):N0} future callback(s) were still armed after draining for {sw.Elapsed.TotalSeconds:N1}s; stopping the network thread anyway.");
					return;
				}
				Thread.Sleep(10);
			}
		}

		/// <summary>Stop the Network Thread</summary>
		public static void Stop()
		{
			if (s_started)
			{
				s_started = false;

				// Un-register the event on the AppDomain
				AppDomain.CurrentDomain.DomainUnload -= s_appDomainUnloadHandler;
				s_appDomainUnloadHandler = null;

				if (Logging.On) Logging.Verbose(typeof(Fdb), "Stop", "Draining outstanding futures...");
				DrainOutstandingFutures();

				if (Logging.On) Logging.Verbose(typeof(Fdb), "Stop", "Stopping Network Thread...");
				StopEventLoop();
				if (Logging.On) Logging.Info(typeof(Fdb), "Stop", "Network Thread stopped");
			}
		}

		#region Scopes & Providers...

		/// <summary>Create a root <see cref="IFdbDatabaseScopeProvider">scope provider</see> that will use the provided database instance</summary>
		/// <param name="db">Database instance that will be exposed</param>
		/// <param name="lifetime">Optional cancellation token that can be used to externally abort the new scope</param>
		[Pure]
		public static IFdbDatabaseScopeProvider CreateRootScope(IFdbDatabase db, CancellationToken lifetime = default)
		{
			Contract.NotNull(db);

			if (db is IFdbDatabaseProvider provider && (lifetime == default || lifetime == db.Cancellation))
			{ // already a provider, and can reuse the same cancellation token
				return provider;
			}

			return new FdbDatabaseSingletonProvider<object>(db, null, CancellationTokenSource.CreateLinkedTokenSource(lifetime, db.Cancellation));
		}

		/// <summary>Create a scope that will execute some initialization logic before the first transaction is allowed to run</summary>
		/// <param name="db">Parent provider</param>
		/// <param name="init">Handler that must run successfully once before allowing transactions on this scope</param>
		/// <param name="lifetime">Optional cancellation token that can be used to externally abort the new scope</param>
		[Pure]
		public static IFdbDatabaseScopeProvider<TState> CreateRootScope<TState>(
			IFdbDatabase db,
			Func<IFdbDatabase, CancellationToken, Task<(IFdbDatabase Db, TState state)>> init,
			CancellationToken lifetime = default
		)
		{
			Contract.NotNull(db);
			Contract.NotNull(init);
			return CreateRootScope(db).CreateScope(init, lifetime);
		}

		/// <summary>Create a scope that will execute some initialization logic before the first transaction is allowed to run</summary>
		/// <param name="db">Parent database</param>
		/// <param name="init">Handler that must run successfully once before allowing transactions on this scope</param>
		/// <param name="lifetime">Optional cancellation token that can be used to externally abort the new scope</param>
		[Pure]
		public static IFdbDatabaseScopeProvider CreateRootScope(
			IFdbDatabase db, 
			Func<IFdbDatabase, CancellationToken, Task> init,
			CancellationToken lifetime = default)
		{
			Contract.NotNull(db);
			Contract.NotNull(init);

			return CreateRootScope(db).CreateScope<object?>(async (database, cancel) =>
			{
				await init(database, cancel).ConfigureAwait(false);
				return (db, null);
			}, lifetime);
		}

		/// <summary>Create a scope provider that will run some initialization logic before transactions are allowed to run</summary>
		/// <param name="parent">Parent scope that will provide a database instance to this scope</param>
		/// <param name="handler">Handler that will be called once the parent provider becomes ready, and before any transactions started from this scope</param>
		/// <param name="lifetime">Optional cancellation token that can be used to externally abort the new scope</param>
		[Pure]
		public static IFdbDatabaseScopeProvider CreateScope(
			IFdbDatabaseScopeProvider parent,
			Func<IFdbDatabase, CancellationToken, Task<IFdbDatabase>> handler,
			CancellationToken lifetime = default
		)
		{
			Contract.NotNull(parent);
			Contract.NotNull(handler);
			return new FdbDatabaseScopeProvider<object>(
				parent, 
				async (db, ct) =>
				{
					var res = await handler(db, ct).ConfigureAwait(false);
					return (db, res);
				},
				lifetime
			);
		}

		/// <summary>Create a scope provider that will run some initialization logic before transactions are allowed to run</summary>
		/// <param name="parent">Parent scope that will provide a database instance to this scope</param>
		/// <param name="handler">Handler that will be called once the parent provider becomes ready, and before any transactions started from this scope</param>
		/// <param name="lifetime">Optional cancellation token that can be used to externally abort the new scope</param>
		[Pure]
		public static IFdbDatabaseScopeProvider<TState> CreateScope<TState>(
			IFdbDatabaseScopeProvider parent,
			Func<IFdbDatabase, CancellationToken, Task<(IFdbDatabase, TState)>> handler,
			CancellationToken lifetime = default

		)
		{
			Contract.NotNull(parent);
			Contract.NotNull(handler);
			return new FdbDatabaseScopeProvider<TState>(parent, handler, lifetime);
		}

		/// <summary>Create a scope provider that will run some initialization logic before transactions are allowed to run</summary>
		/// <param name="parent">Parent scope that will provide a database instance to this scope</param>
		/// <param name="handler">Handler that will be called once the parent provider becomes ready, and before any transactions started from this scope</param>
		/// <param name="lifetime">Optional cancellation token that can be used to externally abort the new scope</param>
		[Pure]
		public static IFdbDatabaseScopeProvider<TState> CreateScope<TState>(
			IFdbDatabaseScopeProvider parent,
			Func<IFdbDatabase, CancellationToken, Task<TState>> handler,
			CancellationToken lifetime = default
		)
		{
			Contract.NotNull(parent);
			Contract.NotNull(handler);
			return new FdbDatabaseScopeProvider<TState>(
				parent,
				async (db, ct) =>
				{
					var res = await handler(db, ct).ConfigureAwait(false);
					return (db, res);
				},
				lifetime
			);
		}

		/// <summary>Create a scope that will execute some initialization logic before the first transaction is allowed to run</summary>
		/// <param name="provider">Parent provider</param>
		/// <param name="init">Handler that must run successfully once before allowing transactions on this scope</param>
		/// <param name="lifetime">Optional cancellation token that can be used to externally abort the new scope</param>
		[Pure]
		public static IFdbDatabaseScopeProvider CreateScope(
			IFdbDatabaseScopeProvider provider,
			Func<IFdbDatabase, CancellationToken, Task> init,
			CancellationToken lifetime = default
		)
		{
			Contract.NotNull(provider);
			Contract.NotNull(init);
			return provider.CreateScope<object?>(async (db, cancel) =>
			{
				await init(db, cancel).ConfigureAwait(false);
				return (db, null);
			}, lifetime);
		}

		/// <summary>Create a poisoned <see cref="IFdbDatabaseScopeProvider{TState}">database provider</see> that will always throw the same error back to the caller</summary>
		/// <typeparam name="TState">Unused in this case</typeparam>
		/// <param name="error">Exception that will be thrown every time someone attempts to use this scope (or a child scope)</param>
		/// <param name="lifetime">Optional cancellation token that can be used to externally abort the new scope</param>
		[Pure]
		public static IFdbDatabaseScopeProvider<TState> CreateFailedScope<TState>(Exception error, CancellationToken lifetime = default)
		{
			Contract.NotNull(error);
			return new FdbDatabaseTombstoneProvider<TState>(null, error, lifetime);
		}

		/// <summary>Create a poisoned <see cref="IFdbDatabaseScopeProvider">database provider</see> that will always throw the same error back to the caller</summary>
		/// <param name="error">Exception that will be thrown every time someone attempts to use this scope (or a child scope)</param>
		/// <param name="lifetime">Optional cancellation token that can be used to externally abort the new scope</param>
		[Pure]
		public static IFdbDatabaseScopeProvider CreateFailedScope(Exception error, CancellationToken lifetime = default)
		{
			Contract.NotNull(error);
			return new FdbDatabaseTombstoneProvider<object>(null, error, lifetime);
		}

		#endregion

	}

}
