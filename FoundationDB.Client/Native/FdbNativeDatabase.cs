#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

// enable this to capture the stacktrace of the ctor, when troubleshooting leaked database handles
//#define CAPTURE_STACKTRACES

namespace FoundationDB.Client.Native
{
	using FoundationDB.Client.Core;

	/// <summary>Wraps a native FDBDatabase* handle</summary>
	[DebuggerDisplay("Handle={m_handle}, Closed={m_handle.IsClosed}")]
	internal sealed class FdbNativeDatabase : IFdbDatabaseHandler
	{
		/// <summary>Handle that wraps the native FDB_DATABASE*</summary>
		private readonly DatabaseHandle m_handle;

		/// <summary>Path to the cluster file</summary>
		private readonly string? m_clusterFile;

		/// <summary>Connection string to the cluster</summary>
		/// <remarks>If null, then a cluster file was used</remarks>
		private readonly string? m_connectionString;

#if CAPTURE_STACKTRACES
		private readonly StackTrace m_stackTrace;
#endif

		public FdbNativeDatabase(DatabaseHandle handle, string? clusterFile, string? connectionString)
		{
			Contract.NotNull(handle);

			m_handle = handle;
			m_clusterFile = clusterFile;
			m_connectionString = connectionString;
#if CAPTURE_STACKTRACES
			m_stackTrace = new StackTrace();
#endif
		}

#if DEBUG
		// We add a destructor in DEBUG builds to help track leaks of databases...
		~FdbNativeDatabase()
		{
#if CAPTURE_STACKTRACES
			Trace.WriteLine("A database handle (" + m_handle + ") was leaked by " + m_stackTrace);
#endif
			// If you break here, that means that a native database handler was leaked by a FdbDatabase instance (or that the database instance was leaked)
			if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
			Dispose(false);
		}
#endif

		public static ValueTask<IFdbDatabaseHandler> CreateDatabaseAsync(string? clusterFile, CancellationToken ct)
		{
			if (Fdb.GetMaxApiVersion() < 610)
			{ // Older version used a different way to create a database handle (fdb_create_cluster) which is not supported anymore
				throw new NotSupportedException("API versions older than 610 are not supported.");
			}

			// Starting from 6.1, creating a database handler can be done directly
			var err = FdbNative.CreateDatabase(clusterFile, out var handle);
			FdbNative.DieOnError(err);

			return new ValueTask<IFdbDatabaseHandler>(new FdbNativeDatabase(handle, clusterFile, default(string)));
		}

		public static ValueTask<IFdbDatabaseHandler> CreateDatabaseFromConnectionStringAsync(string connectionString, CancellationToken ct)
		{
			Fdb.EnsureApiVersion(720);

			var err = FdbNative.CreateDatabaseFromConnectionString(connectionString, out var handle);
			FdbNative.DieOnError(err);

			return new ValueTask<IFdbDatabaseHandler>(new FdbNativeDatabase(handle, default(string), connectionString));
		}

		public string? ClusterFile => m_clusterFile;

		public string? ConnectionString => m_connectionString;

		public bool IsInvalid => m_handle.IsInvalid;

		public bool IsClosed => m_handle.IsClosed;

		public int GetApiVersion() => Fdb.ApiVersion;

		public int GetMaxApiVersion() => FdbNative.GetMaxApiVersion();

		public void SetOption(FdbDatabaseOption option, ReadOnlySpan<byte> data)
		{
			Fdb.EnsureNotOnNetworkThread();

			unsafe
			{
				fixed (byte* ptr = data)
				{
					FdbNative.DieOnError(FdbNative.DatabaseSetOption(m_handle, option, ptr, data.Length));
				}
			}
		}

		public IFdbTransactionHandler CreateTransaction(FdbOperationContext context)
		{
			TransactionHandle? handle = null;
			try
			{
				var err = FdbNative.DatabaseCreateTransaction(m_handle, out handle);
				FdbNative.DieOnError(err);

				return new FdbNativeTransaction(this, null, handle);
			}
			catch(Exception)
			{
				handle?.Dispose();
				throw;
			}
		}

		public IFdbTenantHandler OpenTenant(FdbTenantName name)
		{
			Fdb.EnsureApiVersion(710);

			TenantHandle? handle = null;
			try
			{
				var err = FdbNative.DatabaseOpenTenant(m_handle, name.Value.Span, out handle);
				FdbNative.DieOnError(err);

				return new FdbNativeTenant(this, handle);
			}
			catch (Exception)
			{
				handle?.Dispose();
				throw;
			}
		}

		public Task RebootWorkerAsync(ReadOnlySpan<char> name, bool check, int duration, CancellationToken ct)
		{
			Contract.Debug.Requires(name.Length > 0 && duration >= 0);
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			Fdb.EnsureApiVersion(700);

			return FdbFuture.CreateTaskFromHandle(
				FdbNative.DatabaseRebootWorker(m_handle, name, check, duration),
				ct
			);
		}

		public Task ForceRecoveryWithDataLossAsync(ReadOnlySpan<char> dcId, CancellationToken ct)
		{
			Contract.Debug.Requires(dcId.Length > 0);
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			Fdb.EnsureApiVersion(700);

			return FdbFuture.CreateTaskFromHandle(
				FdbNative.DatabaseForceRecoveryWithDataLoss(m_handle, dcId),
				ct
			);
		}

		public Task CreateSnapshotAsync(ReadOnlySpan<char> uid, ReadOnlySpan<char> snapCommand, CancellationToken ct)
		{
			Contract.Debug.Requires(uid.Length > 0);
			Contract.Debug.Requires(snapCommand.Length > 0);
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			Fdb.EnsureApiVersion(700);

			return FdbFuture.CreateTaskFromHandle(
				FdbNative.DatabaseCreateSnapshot(m_handle, uid, snapCommand),
				ct
			);
		}

		public double GetMainThreadBusyness()
		{
			Fdb.EnsureApiVersion(700);
			return FdbNative.GetMainThreadBusyness(m_handle);
		}

		public Task<FdbProtocolVersion> GetServerProtocolVersionAsync(FdbProtocolVersion expectedVersion, CancellationToken ct)
		{
			Fdb.EnsureApiVersion(700);
			return FdbFuture.CreateTaskFromHandle(
				FdbNative.DatabaseGetServerProtocol(m_handle, expectedVersion.VersionWithFlags),
				this,
				static (h, _) =>
				{
					var err = FdbNative.FutureGetUInt64(h, out var value);
					FdbNative.DieOnError(err);
					return new FdbProtocolVersion(value);
				},
				ct);
		}

		public Task<Slice> GetClientStatus(CancellationToken ct)
		{
			Fdb.EnsureApiVersion(730);

			return FdbFuture.CreateTaskFromHandle(
				FdbNative.DatabaseGetClientStatus(m_handle),
				this,
				static (h, _) =>
				{
					var err = FdbNative.FutureGetKey(h, out var result);
					FdbNative.DieOnError(err);
					return Slice.FromBytes(result);
				},
				ct);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (disposing)
			{
				m_handle.Dispose();
			}
		}

	}

}
