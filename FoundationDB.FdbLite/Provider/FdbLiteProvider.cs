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

namespace FoundationDB.FdbLite
{
	using FoundationDB.Client;
	using FoundationDB.DependencyInjection;
	using FoundationDB.Storage;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Options;

	/// <summary>Options of an <see cref="FdbLiteProvider"/>: which store it opens, and how.</summary>
	public record FdbLiteProviderOptions : FdbDatabaseProviderOptions
	{

		/// <summary>Path of the store file. <see langword="null"/> (the default) opens a non-persistent in-memory store that dies with the provider.</summary>
		/// <remarks>The file is created on first start when it does not exist. A missing parent directory is created too.</remarks>
		public string? Path { get; set; }

		/// <summary>Page geometry of a store created by this provider. Ignored for an existing file, which keeps the geometry recorded in its header.</summary>
		public FdbLiteGeometry Geometry { get; set; } = FdbLiteGeometry.Default;

		/// <summary>Store that is used by the provider, when several providers (or components) share one store. When set, <see cref="Path"/>, <see cref="Geometry"/>, <see cref="Retention"/> and <see cref="Time"/> are ignored: the store was configured by its creator, who also disposes it.</summary>
		public FdbLiteStore? Store { get; set; }

		/// <summary>Retention policy for published versions; <see langword="null"/> (the default) keeps the versions the engine can still serve, a recent-version window like a real cluster's.</summary>
		public FdbSnapshotRetentionPolicy? Retention { get; set; }

		/// <summary>Time source for the store. Highest precedence, before the DI-resolved provider and the system clock.</summary>
		public TimeProvider? Time { get; set; }

	}

	/// <summary>Provides access to a database over an <see cref="FdbLiteStore"/>: a file on disk, or an in-memory store.</summary>
	/// <remarks>The database this provider opens speaks the same transaction API as a real cluster, in one process, without a network. Every behaviour difference against a cluster is described on <see cref="FdbLiteBackend"/>.</remarks>
	public class FdbLiteProvider : IFdbDatabaseProvider
	{

		public FdbLiteProvider(IOptions<FdbLiteProviderOptions> optionsAccessor, TimeProvider? timeProvider = null)
		{
			Contract.NotNull(optionsAccessor);
			this.Cancellation = this.LifeTime.Token;
			this.ProviderOptions = optionsAccessor.Value;
			this.Root = new(this.ProviderOptions.ConnectionOptions.Root ?? FdbPath.Root);
			// precedence: the explicit option, else the DI-resolved provider, else the system clock
			this.Time = this.ProviderOptions.Time ?? timeProvider ?? TimeProvider.System;
		}

		public static IFdbDatabaseProvider Create(FdbLiteProviderOptions options)
		{
			Contract.NotNull(options);
			return new FdbLiteProvider(Microsoft.Extensions.Options.Options.Create(options));
		}

		/// <summary>The store opened by <see cref="Start"/>, or <see langword="null"/> before it</summary>
		public FdbLiteStore? Store { get; private set; }

		/// <summary>Resolved time source for the store this provider opens (see <see cref="FdbLiteProviderOptions.Time"/>).</summary>
		private TimeProvider Time { get; }

		private IFdbDatabase? Db { get; set; }

		public IFdbDatabaseScopeProvider? Parent => null;

		public FdbDirectorySubspaceLocation Root { get; }

		public bool IsAvailable { get; private set; }

		public FdbLiteProviderOptions ProviderOptions { get; }

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

			static ValueTask<IFdbDatabase> GetDatabaseDeferred(FdbLiteProvider provider, CancellationToken ct)
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
			var options = this.ProviderOptions;
			var store = options.Store;
			bool ownsStore;
			if (store != null)
			{ // the store is shared with other providers and owned by its creator: stopping this provider must not dispose it
				if (options.ApiVersion > store.ApiVersion)
				{
					throw new InvalidOperationException($"Cannot use the shared store because its API version ({store.ApiVersion}) is less than our expected version ({options.ApiVersion}).");
				}
				ownsStore = false;
			}
			else
			{
				try
				{
					store = OpenStore(options, this.Time);
				}
				catch (Exception e)
				{
					SetDatabase(null, null, e);
					return;
				}
				ownsStore = true;
			}

			try
			{
				var db = store.OpenDatabase(options.ConnectionOptions.Root, options.ConnectionOptions.ReadOnly, ownsStore);

				if (options.DefaultLogHandler != null)
				{ // enable transaction capture and logging
					db.SetDefaultLogHandler(options.DefaultLogHandler, options.DefaultLogOptions);
				}

				SetDatabase(store, db, null);
			}
			catch (Exception e)
			{
				if (ownsStore) store.Dispose();
				SetDatabase(null, null, e);
			}
		}

		private static FdbLiteStore OpenStore(FdbLiteProviderOptions options, TimeProvider time)
		{
			if (options.Path == null)
			{
				return FdbLiteStore.CreateInMemory(options.Geometry, options.ApiVersion, time: time, retention: options.Retention);
			}

			var directory = System.IO.Path.GetDirectoryName(options.Path);
			if (!string.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}
			return FdbLiteStore.OpenOrCreateFile(options.Path, options.Geometry, options.ApiVersion, time: time, retention: options.Retention);
		}

		public void Stop()
		{
			if (!this.LifeTime.IsCancellationRequested) this.LifeTime.Cancel();
			SetDatabase(null, null, new InvalidOperationException("Database provider has been stopped"));
		}

		private void SetDatabase(FdbLiteStore? store, IFdbDatabase? db, Exception? e)
		{
			lock (this)
			{
				if (this.Db != null && this.Db != db)
				{ // dispose the previous instance (which disposes an owned store)
					this.Db.Dispose();
				}

				this.Store = store;
				this.Db = db;
				this.Error = e;
				this.IsAvailable = db != null && e == null;
			}
		}

	}

	public static class FdbLiteDependencyInjectionExtensions
	{

		/// <summary>Registers an <see cref="IFdbDatabaseProvider"/> over a file-backed <see cref="FdbLiteStore"/>.</summary>
		/// <param name="services">Service collection</param>
		/// <param name="apiVersion">API version the database speaks (for example 730)</param>
		/// <param name="path">Path of the store file, created on first start when missing</param>
		/// <param name="root">Root directory path of the database (<see cref="FdbPath.Root"/> by default)</param>
		/// <param name="configure">Further options: geometry, retention, time source, read-only, transaction logging</param>
		public static IServiceCollection AddFdbLite(this IServiceCollection services, int apiVersion, string path, FdbPath root = default, Action<FdbLiteProviderOptions>? configure = null)
		{
			Contract.NotNull(services);
			Contract.NotNullOrEmpty(path);
			return AddFdbLite(services, apiVersion, root, c =>
			{
				c.Path = path;
				configure?.Invoke(c);
			});
		}

		/// <summary>Registers an <see cref="IFdbDatabaseProvider"/> over an in-memory <see cref="FdbLiteStore"/> that dies with the provider (or a file, when <paramref name="configure"/> sets <see cref="FdbLiteProviderOptions.Path"/>).</summary>
		/// <param name="services">Service collection</param>
		/// <param name="apiVersion">API version the database speaks (for example 730)</param>
		/// <param name="root">Root directory path of the database (<see cref="FdbPath.Root"/> by default)</param>
		/// <param name="configure">Further options: path, geometry, retention, time source, read-only, transaction logging</param>
		public static IServiceCollection AddFdbLite(this IServiceCollection services, int apiVersion, FdbPath root = default, Action<FdbLiteProviderOptions>? configure = null)
		{
			Contract.NotNull(services);
			Contract.GreaterThan(apiVersion, 0, nameof(apiVersion));

			services.AddSingleton<IFdbDatabaseProvider>(static sp => new FdbLiteProvider(sp.GetRequiredService<IOptions<FdbLiteProviderOptions>>(), sp.GetService<TimeProvider>()));
			services.Configure<FdbLiteProviderOptions>(c =>
			{
				c.ApiVersion = apiVersion;
				c.ConnectionOptions.Root = root.IsEmpty ? FdbPath.Root : root; // the default(FdbPath) of an omitted argument is the relative empty path, not the cluster root
				configure?.Invoke(c);
			});
			return services;
		}

		/// <summary>Registers an <see cref="IFdbDatabaseProvider"/> over a store shared with other providers; the caller keeps ownership of <paramref name="store"/> and disposes it.</summary>
		public static IServiceCollection AddFdbLite(this IServiceCollection services, FdbLiteStore store, FdbPath root = default, Action<FdbDatabaseProviderOptions>? configure = null)
		{
			Contract.NotNull(services);
			Contract.NotNull(store);

			services.AddSingleton<IFdbDatabaseProvider>(static sp => new FdbLiteProvider(sp.GetRequiredService<IOptions<FdbLiteProviderOptions>>(), sp.GetService<TimeProvider>()));
			services.Configure<FdbLiteProviderOptions>(c =>
			{
				c.Store = store;
				c.ApiVersion = store.ApiVersion;
				c.ConnectionOptions.Root = root.IsEmpty ? FdbPath.Root : root; // the default(FdbPath) of an omitted argument is the relative empty path, not the cluster root
				configure?.Invoke(c);
			});
			return services;
		}

	}

}
