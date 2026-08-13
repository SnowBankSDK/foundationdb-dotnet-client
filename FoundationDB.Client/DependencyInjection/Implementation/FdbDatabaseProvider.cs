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

namespace FoundationDB.DependencyInjection
{
	using FoundationDB.Client;
	using Microsoft.Extensions.Options;

	/// <summary>Provides access to a FoundationDB database instance</summary>
	[PublicAPI]
	public sealed class FdbDatabaseProvider : IFdbDatabaseProvider
	{

		private IFdbDatabase? Db { get; set; }

		/// <inheritdoc cref="IFdbDatabaseScopeProvider.IsAvailable"/>
		public bool IsAvailable { get; private set; }

		/// <inheritdoc/>
		public FdbDatabaseProviderOptions ProviderOptions { get; }

		private TaskCompletionSource<IFdbDatabase>? InitTask;

		private Task<IFdbDatabase> DbTask;

		private CancellationTokenSource LifeTime { get; } = new();

		private Exception? Error { get; set;}

		/// <inheritdoc cref="IFdbDatabaseScopeProvider.Root"/>
		public FdbDirectorySubspaceLocation Root { get; }

#if NET9_0_OR_GREATER
		private Lock Lock { get; } = new();
#else
		private object Lock { get; } = new();
#endif
		
		public static IFdbDatabaseProvider Create(FdbDatabaseProviderOptions options)
		{
			Contract.NotNull(options);
			return new FdbDatabaseProvider(Options.Create(options));
		}

		public FdbDatabaseProvider(IOptions<FdbDatabaseProviderOptions> optionsAccessor)
		{
			Contract.NotNull(optionsAccessor);

			var options = optionsAccessor.Value;
			
			this.ProviderOptions = options;
			this.Root = new FdbDirectorySubspaceLocation(this.ProviderOptions.ConnectionOptions.Root ?? FdbPath.Root);
			this.DbTask = Task.FromException<IFdbDatabase>(new InvalidOperationException("The database has not been initialized."));
		}

		/// <inheritdoc cref="IFdbDatabaseProvider.Start"/>
		public void Start()
		{
			if (this.InitTask == null)
			{
				lock (this.Lock)
				{
					if (this.InitTask == null)
					{
						StartCore();
						Contract.Debug.Assert(this.InitTask != null);
					}
				}
			}
		}

		private void SetupClient()
		{
			// configure the native library
			switch (this.ProviderOptions.NativeLibraryPath)
			{
				case null:
				{ // disable pre-loading
					Fdb.Options.DisableNativeLibraryPreloading();
					break;
				}
				case "":
				{ // enable pre-loading
					Fdb.Options.EnableNativeLibraryPreloading();
					break;
				}
				default:
				{ // preload specified library
					Fdb.Options.SetNativeLibPath(this.ProviderOptions.NativeLibraryPath); break;
				}
			}

			// configure the API version
			Fdb.Start(this.ProviderOptions.ApiVersion);
		}
		
		private void StartCore()
		{
			var tcs = new TaskCompletionSource<IFdbDatabase>();
			this.DbTask = tcs.Task;
			this.InitTask = tcs;

			// Configure the client inline.
			// This prevents a race condition between startup logic that would call GetRequiredService<IFdbDatabaseProvider>().Start(), and then run the application without waiting.
			try
			{
				SetupClient();
			}
			catch (Exception e)
			{
				SetDatabase(null, e);
				return;
			}

			_ = Task.Run(async () =>
			{
				try
				{
					// connect to the cluster
					var db = await Fdb.OpenAsync(this.ProviderOptions.ConnectionOptions, this.LifeTime.Token).ConfigureAwait(false);

					if (this.ProviderOptions.DefaultLogHandler is not null)
					{ // enable transaction capture and logging!

						db.SetDefaultLogHandler(this.ProviderOptions.DefaultLogHandler, this.ProviderOptions.DefaultLogOptions);
					}

					SetDatabase(db, null);
				}
				catch (Exception e)
				{
					SetDatabase(null, e);
				}

			}, this.LifeTime.Token);
		}

		/// <summary>Starts a new connection attempt if the previous one has failed</summary>
		/// <remarks>
		/// <para>Without this, the first failed <see cref="Fdb.OpenAsync(FdbConnectionOptions,CancellationToken)"/> would poison the provider for the life of the process: every later call would replay the first error, even after the cluster recovered.</para>
		/// <para>A new <see cref="InitTask"/> is installed under the lock before the new attempt runs, so a success can only ever complete the current attempt (a stale success would be disposed by <see cref="SetDatabase"/>).</para>
		/// <para>Callers that observe a failure and retry provide the pacing; attempts coalesce, at most one connection attempt is in flight at any time.</para>
		/// </remarks>
		private void RearmIfFaulted()
		{
			if (this.LifeTime.IsCancellationRequested)
			{ // stopped or disposed: the terminal error stays
				return;
			}
			lock (this.Lock)
			{
				// only re-arm a completed, failed attempt: a pending or successful one is left alone
				if (!this.LifeTime.IsCancellationRequested && this.DbTask.IsFaulted && (this.InitTask?.Task.IsCompleted ?? false))
				{
					StartCore();
				}
			}
		}

		/// <inheritdoc cref="IFdbDatabaseProvider.Stop"/>
		public void Stop()
		{
			this.LifeTime.Cancel();
			Interlocked.Exchange(ref this.InitTask, null)?.TrySetCanceled();
			SetDatabase(null, null);
		}

		/// <summary>Sets or clears the current <see cref="IFdbDatabase"/> singleton that will be returned by this provider</summary>
		public void SetDatabase(IFdbDatabase? db, Exception? e)
		{
			if (this.Db != null && this.Db != db)
			{ // Dispose the previous instance
				this.Db?.Dispose();
			}
			this.Db = db;
			this.Error = e;
			var tcs = Volatile.Read(ref this.InitTask);
			if (db == null)
			{
				if (e != null)
				{
					tcs?.TrySetException(e);
					this.DbTask = Task.FromException<IFdbDatabase>(e);
				} 
				else
				{
					tcs?.TrySetCanceled();
					this.DbTask = Task.FromException<IFdbDatabase>(new InvalidOperationException("There is no database available at this time."));
				}
				this.IsAvailable = false;
			}
			else
			{
				if (tcs == null || !tcs.TrySetResult(db))
				{
#if DEBUG
					if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
					db.Dispose();
					return;
				}
				this.DbTask = Task.FromResult(db);
				this.IsAvailable = true;
			}
		}

		/// <inheritdoc />
		public void Dispose()
		{
			this.LifeTime.Cancel();
			this.IsAvailable = false;
			this.Db?.Dispose();
			this.Db = null;
			this.Error = new ObjectDisposedException("Database has been shut down.");
			this.DbTask = Task.FromException<IFdbDatabase>(this.Error);
			Interlocked.Exchange(ref this.InitTask, null)?.TrySetCanceled();

			if (this.ProviderOptions.AutoStop)
			{
				// Stop the network thread as well
				// note: this is irreversible!
				try
				{
					Fdb.Stop();
				}
				catch
				{
					// swallow all exceptions, since the process is likely being terminated anyway...
				}
			}
		}

		IFdbDatabaseScopeProvider? IFdbDatabaseScopeProvider.Parent => null;

		/// <inheritdoc />
		public CancellationToken Cancellation => this.LifeTime.Token;

		/// <inheritdoc cref="IFdbDatabaseScopeProvider.GetDatabase"/>
		public ValueTask<IFdbDatabase> GetDatabase(CancellationToken ct = default)
		{
			var db = this.Db;
			return db != null
				? new ValueTask<IFdbDatabase>(db)
				: GetDatabaseRare(this, ct);

			static async ValueTask<IFdbDatabase> GetDatabaseRare(FdbDatabaseProvider provider, CancellationToken ct)
			{
				if (provider.InitTask == null && provider.ProviderOptions.AutoStart)
				{ // start is deferred
					provider.Start();
				}
				else if (provider.DbTask.IsFaulted)
				{ // the previous connection attempt failed: start a new one instead of replaying the old error
					provider.RearmIfFaulted();
				}

				var t = provider.DbTask;
				if (!t.IsCompleted && ct.CanBeCanceled)
				{
					//REVIEW: is there a faster way to do this? (we want to make sure not to leak tons of Task.Delay(...) that will linger on until the original ct is triggered
					using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
					{
						await Task.WhenAny(t, Task.Delay(Timeout.InfiniteTimeSpan, cts.Token)).ConfigureAwait(false);
						ct.ThrowIfCancellationRequested();
					}
				}
				return await provider.DbTask.ConfigureAwait(false);
			}
		}

		/// <inheritdoc cref="IFdbDatabaseScopeProvider.TryGetDatabase"/>
		public bool TryGetDatabase([MaybeNullWhen(false)] out IFdbDatabase db)
		{
			db = this.Db;
			return db != null;
		}

		/// <inheritdoc />
		public IFdbDatabaseScopeProvider<TState> CreateScope<TState>(Func<IFdbDatabase, CancellationToken, Task<(IFdbDatabase Db, TState State)>> start, CancellationToken lifetime = default)
		{
			return new FdbDatabaseScopeProvider<TState>(this, start, lifetime);
		}

	}

}
