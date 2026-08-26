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
	using System.ComponentModel;
	using FoundationDB.Filters.Logging;

	/// <summary>Database connection context.</summary>
	[PublicAPI]
	public interface IFdbDatabase : IFdbRetryable, IDisposable
	{
		/// <summary>Name of the database</summary>
		[Obsolete("This property is not supported anymore and will always return \"DB\".")]
		string Name { get; }

		/// <summary>Returns a cancellation token that is linked with the lifetime of this database instance</summary>
		/// <remarks>The token will be cancelled if the database instance is disposed</remarks>
		CancellationToken Cancellation { get; }

		/// <summary>Time source used by this database for managed waits and timestamps (watch idle-timeouts and transaction-log stamps).</summary>
		/// <remarks>
		/// <para>Defaults to the system clock. A test can inject a fake provider (for example through a <c>FakeDbStore</c> or <c>AddFakeDb</c>) so that
		/// managed time-based waits advance with virtual time instead of the wall clock.</para>
		/// <para>A database backed by the native fdb client must keep a system-based provider: the native client enforces its own timeouts and retry
		/// backoff, so a fake clock cannot drive those and would only desynchronize the managed bulk-write commit cadence from the native
		/// five-second transaction limit.</para>
		/// </remarks>
		TimeProvider Time { get; }

		/// <summary>Returns the root path used by this database instance</summary>
		FdbDirectorySubspaceLocation Root { get; }

		/// <summary>Directory Layer used by this database instance</summary>
		FdbDirectoryLayer DirectoryLayer { get; }

		/// <summary>If <see langword="true"/>, this database instance will only allow starting read-only transactions.</summary>
		bool IsReadOnly { get; }

		/// <summary>Helper that can set options for this database</summary>
		IFdbDatabaseOptions Options { get; }

		/// <summary>Sets the default log handler for this database</summary>
		/// <param name="handler">Default handler that is attached to any new transaction, and will be invoked when they complete.</param>
		/// <param name="options"></param>
		/// <remarks>This handler may not be called if logging is disabled, if a transaction overrides its handler, or if it calls <see cref="IFdbReadOnlyTransaction.StopLogging"/></remarks>
		void SetDefaultLogHandler(Action<FdbTransactionLog>? handler, FdbLoggingOptions? options = null);

		/// <summary>Starts a new transaction on this database, with the specified mode</summary>
		/// <param name="mode">Mode of the transaction (read-only, read-write, ....)</param>
		/// <param name="ct">Optional cancellation token that can abort all pending async operations started by this transaction.</param>
		/// <param name="context">Existing parent context, if the transaction needs to be linked with a retry loop, or a parent transaction. If null, will create a new standalone context valid only for this transaction</param>
		/// <returns>New transaction instance that can read from or write to the database.</returns>
		/// <remarks>You MUST call Dispose() on the transaction when you are done with it. You SHOULD wrap it in a 'using' statement to ensure that it is disposed in all cases.</remarks>
		/// <example><code>
		/// using(var tr = db.BeginTransaction(CancellationToken.None))
		/// {
		///		tr.Set(Slice.FromString("Hello"), Slice.FromString("World"));
		///		tr.Clear(Slice.FromString("OldValue"));
		///		await tr.CommitAsync();
		/// }
		/// </code></example>
		[Obsolete("Use BeginTransaction(...) instead", error: true)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		ValueTask<IFdbTransaction> BeginTransactionAsync(FdbTransactionMode mode, CancellationToken ct, FdbOperationContext? context = null);

		/// <summary>Starts a new transaction on this database, with the specified mode</summary>
		/// <param name="mode">Mode of the transaction (read-only, read-write, ....)</param>
		/// <param name="ct">Optional cancellation token that can abort all pending async operations started by this transaction.</param>
		/// <param name="context">Existing parent context, if the transaction needs to be linked with a retry loop, or a parent transaction. If null, will create a new standalone context valid only for this transaction</param>
		/// <returns>New transaction instance that can read from or write to the database.</returns>
		/// <remarks>You MUST call Dispose() on the transaction when you are done with it. You SHOULD wrap it in a 'using' statement to ensure that it is disposed in all cases.</remarks>
		/// <example><code>
		/// using(var tr = db.BeginTransaction(CancellationToken.None))
		/// {
		///		tr.Set(Slice.FromString("Hello"), Slice.FromString("World"));
		///		tr.Clear(Slice.FromString("OldValue"));
		///		await tr.CommitAsync();
		/// }
		/// </code></example>
		IFdbTransaction BeginTransaction(FdbTransactionMode mode, CancellationToken ct, FdbOperationContext? context = null);

		/// <summary>Gets a <see cref="IFdbTenant">tenant</see> from this database</summary>
		/// <param name="name">Name of the tenant</param>
		/// <returns>Instance that can execute transactions in the context of this tenant</returns>
		IFdbTenant GetTenant(FdbTenantName name);

		/// <summary>Returns the currently enforced API version for this database instance.</summary>
		int GetApiVersion();

		/// <summary>Returns a value between 0 and 1 that reflect the saturation of the client main thread.</summary>
		/// <returns>Value between 0 (no activity) and 1 (completely saturated)</returns>
		/// <remarks>The value is updated in the background at regular interval (by default every second).</remarks>
		double GetMainThreadBusyness();

		/// <summary>Attempts to reboot a work in the cluster</summary>
		Task RebootWorkerAsync(string name, bool check, int duration, CancellationToken ct);

		/// <summary>Forces a recovery that could induce data loss</summary>
		Task ForceRecoveryWithDataLossAsync(string dcId, CancellationToken ct);

		/// <summary>Creates a new snapshot of the database</summary>
		Task CreateSnapshotAsync(string uid, string snapCommand, CancellationToken ct);

		/// <summary>Returns the protocol version reported by the coordinator this client is connected to.</summary>
		/// <param name="expectedVersion">If this value is not equal to <see cref="FdbProtocolVersion.None"/>, the task will not complete until the protocol version is <c>different</c> than expected (or <paramref name="ct"/> fires).</param>
		/// <param name="ct">Token used to cancel the operation.</param>
		/// <returns>Task that returns the current protocol version</returns>
		/// <remarks>This will never complete if the remote server is running a protocol from FDB 5.0 or older.</remarks>
		Task<FdbProtocolVersion> GetServerProtocolVersionAsync(FdbProtocolVersion expectedVersion, CancellationToken ct);

		/// <summary>Returns the client status</summary>
		Task<Slice> GetClientStatus(CancellationToken ct);

	}

}
