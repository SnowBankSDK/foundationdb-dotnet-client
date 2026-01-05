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
	using System.Collections.Concurrent;
	using FoundationDB.Client.Core;
	using FoundationDB.Client.Native;

	[DebuggerDisplay("Name={Name}")]
	public class FdbTenant : IFdbTenant, IDisposable
	{

		/// <summary>Underlying handler for this tenant</summary>
		private readonly IFdbTenantHandler m_handler;

		/// <summary>Name of this tenant</summary>
		private readonly FdbTenantName m_name;

		/// <summary>Parent database that this tenant belongs to</summary>
		private readonly FdbDatabase m_db;

		/// <summary>Global cancellation source that is cancelled when the current db instance gets disposed.</summary>
		private readonly CancellationTokenSource m_cts;

		/// <summary>List of all "pending" transactions created from this tenant instance (and that have not yet been disposed)</summary>
		private readonly ConcurrentDictionary<int, FdbTransaction> m_transactions = new();

		/// <summary>Set to true when the current db instance gets disposed.</summary>
		private volatile bool m_disposed;

		internal FdbTenant(FdbDatabase db, IFdbTenantHandler handler, FdbTenantName name)
		{
			Contract.Debug.Requires(db != null && handler != null);

			m_handler = handler;
			m_name = name;
			m_db = db;
			m_cts = CancellationTokenSource.CreateLinkedTokenSource(db.Cancellation);
			this.Cancellation = m_cts.Token;
		}

		/// <inheritdoc />
		public FdbTenantName Name => m_name;

		/// <inheritdoc />
		public IFdbDatabase Database => m_db;

		/// <summary>Returns a cancellation token that is linked with the lifetime of this database instance</summary>
		/// <remarks>The token will be cancelled if the database instance is disposed</remarks>
		public CancellationToken Cancellation { get; }

		public bool StillAlive => !m_handler.IsClosed;

		#region Transaction Management...

		/// <summary>Start a new transaction on this database</summary>
		/// <param name="mode">Mode of the new transaction (read-only, read-write, ...)</param>
		/// <param name="ct">Optional cancellation token that can abort all pending async operations started by this transaction.</param>
		/// <param name="context">If not null, attach the new transaction to an existing context.</param>
		/// <returns>New transaction instance that can read from or write to the database.</returns>
		/// <remarks>You MUST call Dispose() on the transaction when you are done with it. You SHOULD wrap it in a 'using' statement to ensure that it is disposed in all cases.</remarks>
		/// <example>
		/// using(var tr = db.BeginTransaction(CancellationToken.None))
		/// {
		///		tr.Set(Slice.FromString("Hello"), Slice.FromString("World"));
		///		tr.Clear(Slice.FromString("OldValue"));
		///		await tr.CommitAsync();
		/// }</example>
		public IFdbTransaction BeginTransaction(FdbTransactionMode mode, CancellationToken ct, FdbOperationContext? context = null)
		{
			ct.ThrowIfCancellationRequested();
			if (context == null)
			{
				context = new FdbOperationContext(m_db, this, mode, ct);
			}
			else
			{
				if (context.Tenant == null || context.Tenant != this) throw new ArgumentException("This operation context was created for a different tenant instance", nameof(context));
			}
			return CreateNewTransaction(context);
		}

		/// <summary>Start a new transaction on this database, with an optional context</summary>
		/// <param name="context">Optional context in which the transaction will run</param>
		internal FdbTransaction CreateNewTransaction(FdbOperationContext context)
		{
			Contract.Debug.Requires(context != null && context.Database != null);
			ThrowIfDisposed();

			// force the transaction to be read-only, if the database itself is read-only
			var mode = context.Mode;
			if (m_db.IsReadOnly) mode |= FdbTransactionMode.ReadOnly;

			int id = Interlocked.Increment(ref FdbDatabase.TransactionIdCounter);

			// ensure that if anything happens, either we return a valid Transaction, or we dispose it immediately
			FdbTransaction? trans = null;
			try
			{
				var transactionHandler = m_handler.CreateTransaction(context);

				trans = new FdbTransaction(m_db, this, context, id, transactionHandler, mode);
				RegisterTransaction(trans);
				context.AttachTransaction(trans);
				m_db.ConfigureTransactionDefaults(trans);

				// flag as ready
				trans.State = FdbTransaction.STATE_READY;
				return trans;
			}
			catch (Exception)
			{
				if (trans != null)
				{
					context.ReleaseTransaction(trans);
					trans.Dispose();
				}
				throw;
			}
		}

		internal void EnsureTransactionIsValid(FdbTransaction transaction)
		{
			Contract.Debug.Requires(transaction != null);
			ThrowIfDisposed();
			//TODO?
		}

		/// <summary>Add a new transaction to the list of tracked transactions</summary>
		internal void RegisterTransaction(FdbTransaction transaction)
		{
			Contract.Debug.Requires(transaction != null);

			if (!m_transactions.TryAdd(transaction.Id, transaction))
			{
				throw Fdb.Errors.FailedToRegisterTransactionOnTenant(transaction, this);
			}
		}

		/// <summary>Remove a transaction from the list of tracked transactions</summary>
		internal void UnregisterTransaction(FdbTransaction transaction)
		{
			Contract.Debug.Requires(transaction != null);

			//do nothing is already disposed
			if (m_disposed) return;

			// Unregister the transaction. We do not care if it has already been done
			m_transactions.TryRemove(System.Collections.Generic.KeyValuePair.Create(transaction.Id, transaction));
		}

		/// <summary>Fetch the id of this tenant</summary>
		public async Task<long> GetIdAsync(CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();
			ThrowIfDisposed();

			return await m_handler.GetIdAsync(ct).ConfigureAwait(false);
		}

		#endregion

		#region IDisposable...

		private void ThrowIfDisposed()
		{
			if (m_disposed)
			{
				throw ThrowHelper.ObjectDisposedException(this);
			}
		}

		/// <summary>Close this database instance, aborting any pending transaction that was created by this instance.</summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>Close this database instance, aborting any pending transaction that was created by this instance.</summary>
		protected virtual void Dispose(bool disposing)
		{
			if (!m_disposed)
			{
				m_disposed = true;
				if (disposing)
				{
					m_db.UnregisterTenant(this);

					try
					{
						// mark this tenant as dead, but keep the handle alive until after all the callbacks have fired
						foreach (var trans in m_transactions.Values)
						{
							if (trans is { StillAlive: true })
							{
								trans.Cancel();
							}
						}
						m_transactions.Clear();

						//note: will block until all the registered callbacks have finished executing
						using (m_cts)
						{
							try { m_cts.Cancel(); }
							catch(ObjectDisposedException) { }
						}
					}
					finally
					{
						if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "Dispose", "Disposing tenant handler");
						try { m_handler.Dispose(); }
						catch (Exception e)
						{
							if (Logging.On) Logging.Exception(this, "Dispose", e);
						}
					}
				}
			}
		}

		#endregion

		#region Transactionals...

		//NOTE: other bindings use different names or concept for transactionals, and some also support ReadOnly vs ReadWrite transaction
		// - Python uses the @transactional decorator with first arg called db_or_trans
		// - JAVA uses db.run() and db.runAsync(), but does not have a method for read-only transactions
		// - Ruby uses db.transact do |tr|
		// - Go uses db.Transact(...) and db.ReadTransact(...)
		// - NodeJS uses fdb.doTransaction(function(...) { ... })

		// Conventions:
		// - ReadAsync() => read-only transactions, return something to the caller
		// - WriteAsync() => writable transactions, does not return anything to the caller
		// - ReadWriteAsync() => writable transactions, return something to the caller

		#region IFdbReadOnlyRetryable...

		/// <summary>Empty type that is used to prevent ambiguity when switching on delegate types</summary>
		private struct Nothing { }

		private Task<TResult> ExecuteReadOnlyAsync<TState, TIntermediate, TResult>(TState state, Delegate handler, Delegate? success, CancellationToken ct)
		{
			Contract.NotNull(handler);
			if (ct.IsCancellationRequested) return Task.FromCanceled<TResult>(ct);
			ThrowIfDisposed();

			var context = new FdbOperationContext(m_db, this, FdbTransactionMode.ReadOnly | FdbTransactionMode.InsideRetryLoop | FdbTransactionMode.UseTenant, ct);
			return FdbOperationContext.ExecuteInternal<TState, TIntermediate, TResult>(context, state, handler, success);
		}

		/// <inheritdoc/>
		public Task ReadAsync(Func<IFdbReadOnlyTransaction, Task> handler, CancellationToken ct)
			=> ExecuteReadOnlyAsync<Nothing, Nothing, Nothing>(default(Nothing), handler, null, ct);

		/// <inheritdoc/>
		public Task ReadAsync(Func<IFdbReadOnlyTransaction, ValueTask> handler, CancellationToken ct)
			=> ExecuteReadOnlyAsync<Nothing, Nothing, Nothing>(default(Nothing), handler, null, ct);

		/// <inheritdoc/>
		public Task ReadAsync<TState>(TState state, Func<IFdbReadOnlyTransaction, TState, Task> handler, CancellationToken ct)
			=> ExecuteReadOnlyAsync<TState, Nothing, Nothing>(state, handler, null, ct);

		/// <inheritdoc/>
		public Task ReadAsync<TState>(TState state, Func<IFdbReadOnlyTransaction, TState, ValueTask> handler, CancellationToken ct)
			=> ExecuteReadOnlyAsync<TState, Nothing, Nothing>(state, handler, null, ct);

		/// <inheritdoc/>
		public Task<TResult> ReadAsync<TResult>(Func<IFdbReadOnlyTransaction, Task<TResult>> handler, CancellationToken ct)
			=> ExecuteReadOnlyAsync<Nothing, TResult, TResult>(default(Nothing), handler, null, ct);

		/// <inheritdoc/>
		public Task<TResult> ReadAsync<TResult>(Func<IFdbReadOnlyTransaction, ValueTask<TResult>> handler, CancellationToken ct)
			=> ExecuteReadOnlyAsync<Nothing, TResult, TResult>(default(Nothing), handler, null, ct);

		/// <inheritdoc/>
		public Task<TResult> ReadAsync<TState, TResult>(TState state, Func<IFdbReadOnlyTransaction, TState, Task<TResult>> handler, CancellationToken ct)
			=> ExecuteReadOnlyAsync<TState, TResult, TResult>(state, handler, null, ct);

		/// <inheritdoc/>
		public Task<TResult> ReadAsync<TState, TResult>(TState state, Func<IFdbReadOnlyTransaction, TState, ValueTask<TResult>> handler, CancellationToken ct)
			=> ExecuteReadOnlyAsync<TState, TResult, TResult>(state, handler, null, ct);

		/// <inheritdoc/>
		public Task<TResult> ReadAsync<TResult>(Func<IFdbReadOnlyTransaction, Task<TResult>> handler, Action<IFdbReadOnlyTransaction, TResult> success, CancellationToken ct)
			=> ExecuteReadOnlyAsync<Nothing, TResult, TResult>(default(Nothing), handler, success, ct);

		/// <inheritdoc/>
		public Task<TResult> ReadAsync<TIntermediate, TResult>(Func<IFdbReadOnlyTransaction, Task<TIntermediate>> handler, Func<IFdbReadOnlyTransaction, TIntermediate, TResult> success, CancellationToken ct)
			=> ExecuteReadOnlyAsync<Nothing, TIntermediate, TResult>(default(Nothing), handler, success, ct);

		/// <inheritdoc/>
		public Task<TResult> ReadAsync<TIntermediate, TResult>(Func<IFdbReadOnlyTransaction, ValueTask<TIntermediate>> handler, Func<IFdbReadOnlyTransaction, TIntermediate, TResult> success, CancellationToken ct)
			=> ExecuteReadOnlyAsync<Nothing, TIntermediate, TResult>(default(Nothing), handler, success, ct);

		/// <inheritdoc/>
		public Task<TResult> ReadAsync<TIntermediate, TResult>(Func<IFdbReadOnlyTransaction, Task<TIntermediate>> handler, Func<IFdbReadOnlyTransaction, TIntermediate, Task<TResult>> success, CancellationToken ct)
			=> ExecuteReadOnlyAsync<Nothing, TIntermediate, TResult>(default(Nothing), handler, success, ct);

		/// <inheritdoc/>
		public Task<TResult> ReadAsync<TIntermediate, TResult>(Func<IFdbReadOnlyTransaction, ValueTask<TIntermediate>> handler, Func<IFdbReadOnlyTransaction, TIntermediate, ValueTask<TResult>> success, CancellationToken ct)
			=> ExecuteReadOnlyAsync<Nothing, TIntermediate, TResult>(default(Nothing), handler, success, ct);

		/// <inheritdoc/>
		public Task<TResult> ReadAsync<TState, TIntermediate, TResult>(TState state, Func<IFdbReadOnlyTransaction, TState, Task<TIntermediate>> handler, Func<IFdbReadOnlyTransaction, TIntermediate, Task<TResult>> success, CancellationToken ct)
			=> ExecuteReadOnlyAsync<TState, TIntermediate, TResult>(state, handler, success, ct);

		#endregion
		/// <inheritdoc/>
		public Task<TResult> ReadAsync<TState, TIntermediate, TResult>(TState state, Func<IFdbReadOnlyTransaction, TState, ValueTask<TIntermediate>> handler, Func<IFdbReadOnlyTransaction, TIntermediate, ValueTask<TResult>> success, CancellationToken ct)
			=> ExecuteReadOnlyAsync<TState, TIntermediate, TResult>(state, handler, success, ct);

		#region IFdbRetryable...

		private Task<TResult> ExecuteReadWriteAsync<TState, TIntermediate, TResult>(TState state, Delegate handler, Delegate? success, CancellationToken ct)
		{
			Contract.NotNull(handler);
			if (ct.IsCancellationRequested) return Task.FromCanceled<TResult>(ct);
			ThrowIfDisposed();
			if (m_db.IsReadOnly) throw new InvalidOperationException("Cannot mutate a read-only database.");

			var context = new FdbOperationContext(m_db, this, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop | FdbTransactionMode.UseTenant, ct);
			return FdbOperationContext.ExecuteInternal<TState, TIntermediate, TResult>(context, state, handler, success);
		}

		/// <inheritdoc/>
		public Task WriteAsync(Action<IFdbTransaction> handler, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, object?, object?>(null, handler, null, ct);
		}

		/// <inheritdoc/>
		public Task WriteAsync<TState>(TState state, Action<IFdbTransaction, TState> handler, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<TState, object?, object?>(state, handler, null, ct);
		}

		/// <inheritdoc/>
		public Task WriteAsync(Func<IFdbTransaction, Task> handler, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, object?, object?>(null, handler, null, ct);
		}

		/// <inheritdoc/>
		public Task WriteAsync<TState>(TState state, Func<IFdbTransaction, TState, Task> handler, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<TState, object?, object?>(state, handler, null, ct);
		}

		/// <inheritdoc/>
		public Task WriteAsync(Action<IFdbTransaction> handler, Action<IFdbTransaction> success, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, object?, object?>(null, handler, success, ct);
		}

		/// <inheritdoc/>
		public Task WriteAsync(Action<IFdbTransaction> handler, Func<IFdbTransaction, Task> success, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, object?, object?>(null, handler, success, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadWriteAsync<TResult>(Action<IFdbTransaction> handler, Func<IFdbTransaction, TResult> success, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, object?, TResult>(null, handler, success, ct);
		}

		/// <inheritdoc/>
		public Task WriteAsync(Func<IFdbTransaction, Task> handler, Action<IFdbTransaction> success, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, object?, object?>(null, handler, success, ct);
		}

		/// <inheritdoc/>
		public Task WriteAsync(Func<IFdbTransaction, Task> handler, Func<IFdbTransaction, Task> success, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, object?, object?>(null, handler, success, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadWriteAsync<TResult>(Func<IFdbTransaction, Task<TResult>> handler, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, TResult, TResult>(null, handler, null, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadWriteAsync<TState, TResult>(TState state, Func<IFdbTransaction, TState, Task<TResult>> handler, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<TState, TResult, TResult>(state, handler, null, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadWriteAsync<TResult>(Func<IFdbTransaction, Task<TResult>> handler, Action<IFdbTransaction, TResult> success, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, TResult, TResult>(null, handler, success, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadWriteAsync<TResult>(Func<IFdbTransaction, Task> handler, Func<IFdbTransaction, Task<TResult>> success, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, object?, TResult>(null, handler, success, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadWriteAsync<TResult>(Func<IFdbTransaction, Task> handler, Func<IFdbTransaction, TResult> success, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, object?, TResult>(null, handler, success, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadWriteAsync<TIntermediate, TResult>(Func<IFdbTransaction, Task<TIntermediate>> handler, Func<IFdbTransaction, TIntermediate, TResult> success, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, TIntermediate, TResult>(null, handler, success, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadWriteAsync<TIntermediate, TResult>(Func<IFdbTransaction, Task<TIntermediate>> handler, Func<IFdbTransaction, TIntermediate, Task<TResult>> success, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, TIntermediate, TResult>(null, handler, success, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadWriteAsync<TState, TIntermediate, TResult>(TState state, Func<IFdbTransaction, TState, Task<TIntermediate>> handler, Func<IFdbTransaction, TIntermediate, Task<TResult>> success, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<TState, TIntermediate, TResult>(state, handler, success, ct);
		}

		#endregion

		#endregion

	}

}
