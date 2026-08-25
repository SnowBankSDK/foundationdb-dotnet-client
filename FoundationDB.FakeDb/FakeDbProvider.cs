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

namespace FoundationDB.Testing
{
	using FoundationDB.Client;
	using FoundationDB.DependencyInjection;
	using Microsoft.Extensions.Options;

	/// <summary>Options used to configure the behavior of <see cref="FakeDbProvider"/> instances</summary>
	public record FakeDbProviderOptions : FdbDatabaseProviderOptions
	{

		/// <summary>Store that is used by the provider</summary>
		public FakeDbStore? Store { get; set; }

		/// <summary>Time source for the simulated database. Highest precedence, before the DI-resolved provider and the system clock.</summary>
		public TimeProvider? Time { get; set; }

	}

	/// <summary>Provides access to a simulated in-memory database instance</summary>
	/// <remarks>This emulator is currently <b>experimental</b> and may not accurately reproduce the behavior of an actual fdb cluster, most notably due to the absence of network latency!</remarks>
	public class FakeDbProvider : IFdbDatabaseProvider
	{

		public FakeDbProvider(IOptions<FakeDbProviderOptions> optionsAccessor, TimeProvider? timeProvider = null)
		{
			Contract.NotNull(optionsAccessor);
			this.Cancellation = this.LifeTime.Token;
			this.ProviderOptions = optionsAccessor.Value;
			this.Root = new(this.ProviderOptions.ConnectionOptions.Root ?? FdbPath.Root);
			// precedence: the explicit option, else the DI-resolved provider, else the system clock
			this.Time = this.ProviderOptions.Time ?? timeProvider ?? TimeProvider.System;
		}

		public static IFdbDatabaseProvider Create(FakeDbProviderOptions options)
		{
			Contract.NotNull(options);
			return new FakeDbProvider(Microsoft.Extensions.Options.Options.Create(options));
		}

		private FakeDbStore? Store { get; set; }

		/// <summary>Resolved time source for the databases this provider opens (see <see cref="FakeDbProviderOptions.Time"/>).</summary>
		private TimeProvider Time { get; }

		private IFdbDatabase? Db { get; set; }

		public IFdbDatabaseScopeProvider? Parent { get; }

		public FdbDirectorySubspaceLocation Root { get; }

		public bool IsAvailable { get; private set; }

		public FakeDbProviderOptions ProviderOptions { get; }

		FdbDatabaseProviderOptions IFdbDatabaseProvider.ProviderOptions => this.ProviderOptions;

		private CancellationTokenSource LifeTime { get; } = new();

		public CancellationToken Cancellation { get; }

		private Exception? Error { get; set; }

		/// <inheritdoc/>
		public void Dispose()
		{
			using (this.LifeTime)
			{
				Stop();
			}
		}

		public ValueTask<IFdbDatabase> GetDatabase(CancellationToken ct)
		{
			var db = this.Db;
			return db != null ? new ValueTask<IFdbDatabase>(db) : GetDatabaseDeferred(this, ct);

			static ValueTask<IFdbDatabase> GetDatabaseDeferred(FakeDbProvider provider, CancellationToken ct)
			{
				lock (provider)
				{
					if (provider.Db == null && provider.Error == null && provider.ProviderOptions.AutoStart)
					{ // start is deferred
						provider.Start();
					}

					if (provider.Error != null)
					{
						return new ValueTask<IFdbDatabase>(Task.FromException<IFdbDatabase>(provider.Error));
					}

					if (provider.Db == null)
					{
						return new ValueTask<IFdbDatabase>(Task.FromException<IFdbDatabase>(new InvalidOperationException("Database provider is not started yet")));
					}

					return new ValueTask<IFdbDatabase>(provider.Db);
				}
			}
		}

		public bool TryGetDatabase([MaybeNullWhen(false)] out IFdbDatabase db)
		{
			db = this.Db;
			return db != null;
		}

		public IFdbDatabaseScopeProvider<TState> CreateScope<TState>(Func<IFdbDatabase, CancellationToken, Task<(IFdbDatabase Db, TState State)>> start, CancellationToken lifetime = default)
		{
			return Fdb.CreateScope<TState>(this, start, lifetime);
		}

		public void Start()
		{
			// either we use a global store (shared by multiple components
			var store = this.ProviderOptions.Store;
			bool ownsStore;
			if (store != null)
			{
				//TODO: we still have to make sure that the API verison is compatible
				if (this.ProviderOptions.ApiVersion > store.ApiVersion)
				{
					throw new InvalidOperationException($"Cannot use the global store because its API version ({store.ApiVersion}) is less than our expected version ({this.ProviderOptions.ApiVersion}).");
				}
				// the store is owned by the caller and shared across providers: stopping this provider must not dispose it
				ownsStore = false;
			}
			else
			{
				// we create our own local store instance, that is not shared
				store = new FakeDbStore(this.ProviderOptions.ApiVersion, time: this.Time);
				ownsStore = true;
			}

			try
			{
				var db = store.OpenDatabase(this.ProviderOptions.ConnectionOptions.Root, this.ProviderOptions.ConnectionOptions.ReadOnly, ownsStore);

				if (this.ProviderOptions.DefaultLogHandler != null)
				{ // enable transaction capture and logging!
					db.SetDefaultLogHandler(this.ProviderOptions.DefaultLogHandler, this.ProviderOptions.DefaultLogOptions);
				}

				SetDatabase(store, db, null);
			}
			catch (Exception e)
			{
				SetDatabase(null, null, e);
			}
		}

		public void Stop()
		{
			if (!this.LifeTime.IsCancellationRequested) this.LifeTime.Cancel();
			SetDatabase(null, null, new InvalidOperationException("Database provider has been stopped"));
		}

		public void SetDatabase(FakeDbStore? store, IFdbDatabase? db, Exception? e)
		{
			lock (this)
			{
				if (this.Db != null && this.Db != db)
				{ // Dispose the previous instance
					this.Db?.Dispose();
				}

				this.Store = store;
				this.Db = db;
				this.Error = e;
				this.IsAvailable = db != null && e != null;
			}
		}

	}

	public static class FakeDbDependencyInjectionExtensions
	{

		public static IServiceCollection AddFakeDb(this IServiceCollection services, int apiVersion, FdbPath root = default, Action<FakeDbProviderOptions>? configure = null, TimeProvider? time = null)
		{
			Contract.NotNull(services);
			Contract.GreaterThan(apiVersion, 0, nameof(apiVersion));

			services.AddSingleton<IFdbDatabaseProvider>(static sp => new FakeDbProvider(sp.GetRequiredService<IOptions<FakeDbProviderOptions>>(), sp.GetService<TimeProvider>()));
			services.Configure<FakeDbProviderOptions>(c =>
			{
				c.ApiVersion = apiVersion;
				c.ConnectionOptions.Root = root;
				c.Time = time; // explicit override, wins over the DI-resolved provider when set
				configure?.Invoke(c);
			});
			return services;
		}

		public static IServiceCollection AddFakeDb(this IServiceCollection services, FakeDbStore store, FdbPath root = default, Action<FdbDatabaseProviderOptions>? configure = null)
		{
			Contract.NotNull(services);
			Contract.NotNull(store);

			services.AddSingleton<IFdbDatabaseProvider>(static sp => new FakeDbProvider(sp.GetRequiredService<IOptions<FakeDbProviderOptions>>(), sp.GetService<TimeProvider>()));
			services.Configure<FakeDbProviderOptions>(c =>
			{
				c.Store = store;
				c.ApiVersion = store.ApiVersion;
				c.ConnectionOptions.Root = root;
				configure?.Invoke(c);
			});
			return services;
		}

	}

}
