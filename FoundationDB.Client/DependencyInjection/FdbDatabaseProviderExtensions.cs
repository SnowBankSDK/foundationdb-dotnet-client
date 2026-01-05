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
	using FoundationDB.DependencyInjection;

	[PublicAPI]
	public static class FdbDatabaseProviderExtensions
	{

		/// <summary>Convert this database instance into a <see cref="IFdbDatabaseProvider">provider</see></summary>
		/// <param name="db">Database singleton to wrap into a provider</param>
		/// <param name="lifetime">External cancellation token that can remotely disable this provider (without impacting the original database instance)</param>
		/// <returns>Provider instance that will always return the <paramref name="db"/> instance.</returns>
		/// <remarks>
		/// Disposing the original database, or cancelling the provided cancellation token will also trigger the cancellation of all the downstream scopes created from this provider.
		/// </remarks>
		[Pure]
		public static IFdbDatabaseScopeProvider AsDatabaseProvider(this IFdbDatabase db, CancellationToken lifetime = default)
		{
			// Default database implementation is already a provider!
			if (db is IFdbDatabaseProvider provider && (lifetime == CancellationToken.None || lifetime == db.Cancellation))
			{
				return provider;
			}
			// Wrap the database in a separate provider.
			return new FdbDatabaseSingletonProvider<object>(db, null, CancellationTokenSource.CreateLinkedTokenSource(db.Cancellation, lifetime));
		}

		/// <summary>Create a scope that will execute some initialization logic before the first transaction is allowed to run</summary>
		/// <param name="db">Parent provider</param>
		/// <param name="init">Handler that must run successfully once before allowing transactions on this scope</param>
		/// <param name="lifetime">External cancellation token that can remotely disable this provider (without impacting the original database instance)</param>
		/// <returns>Provider instance that will always return the <paramref name="db"/> instance.</returns>
		/// <remarks>Disposing the original database, or cancelling the provided cancellation token will also trigger the cancellation of all the downstream scopes created from this provider.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IFdbDatabaseScopeProvider<TState> CreateRootScope<TState>(
			this IFdbDatabase db,
			Func<IFdbDatabase, CancellationToken, Task<(IFdbDatabase Db, TState state)>> init,
			CancellationToken lifetime = default
		)
		{
			return Fdb.CreateRootScope(db, init, lifetime);
		}

		/// <summary>Create a scope that will execute some initialization logic before the first transaction is allowed to run</summary>
		/// <param name="provider">Parent provider</param>
		/// <param name="init">Handler that must run successfully once before allowing transactions on this scope</param>
		/// <param name="lifetime">Optional cancellation token that can be used to externally abort the new scope</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IFdbDatabaseScopeProvider CreateScope(
			this IFdbDatabaseScopeProvider provider,
			Func<IFdbDatabase, CancellationToken, Task> init,
			CancellationToken lifetime = default
		)
		{
			return Fdb.CreateScope(provider, init, lifetime);
		}

		/// <summary>Create a scope that will execute some initialization logic before the first transaction is allowed to run</summary>
		/// <param name="db">Parent database</param>
		/// <param name="init">Handler that must run successfully once before allowing transactions on this scope</param>
		/// <param name="lifetime">Optional cancellation token that can be used to externally abort the new scope</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IFdbDatabaseScopeProvider CreateRootScope(
			this IFdbDatabase db,
			Func<IFdbDatabase, CancellationToken, Task> init,
			CancellationToken lifetime = default
		)
		{
			return Fdb.CreateRootScope(db, init, lifetime);
		}

		/// <summary>Wait for the scope to become ready.</summary>
		public static ValueTask EnsureIsReady(this IFdbDatabaseScopeProvider provider, CancellationToken ct)
		{
			Contract.NotNull(provider);

			return provider.IsAvailable && !ct.IsCancellationRequested ? default : WaitForRedinessDeferred(provider, ct);

			static async ValueTask WaitForRedinessDeferred(IFdbDatabaseScopeProvider provider, CancellationToken ct)
			{
				ct.ThrowIfCancellationRequested();
				_ = await provider.GetDatabase(ct).ConfigureAwait(false);
			}
		}

		/// <summary>Runs a transactional lambda function inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="provider">Provider of the database</param>
		/// <param name="handler">Asynchronous handler that will be retried until it succeeds, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// Since the handler can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static Task<TResult> ReadAsync<TResult>(
			this IFdbDatabaseScopeProvider provider,
			[InstantHandle] Func<IFdbReadOnlyTransaction, Task<TResult>> handler,
			CancellationToken ct)
		{
			Contract.NotNull(provider);

			return !ct.IsCancellationRequested && provider.TryGetDatabase(out var db) 
				? db.ReadAsync(handler, ct)
				: ReadDeferred(provider, handler, ct);

			static async Task<TResult> ReadDeferred(IFdbDatabaseScopeProvider provider, Func<IFdbReadOnlyTransaction, Task<TResult>> handler, CancellationToken ct)
			{
				var db = await provider.GetDatabase(ct).ConfigureAwait(false);
				return await db.ReadAsync(handler, ct).ConfigureAwait(false);
			}
		}

		/// <summary>Runs a transactional lambda function inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="provider">Provider of the database</param>
		/// <param name="handler">Asynchronous handler that will be retried until it succeeds, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// Since the handler can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static Task<TResult> ReadAsync<TState, TResult>(
			this IFdbDatabaseScopeProvider<TState> provider,
			[InstantHandle] Func<IFdbReadOnlyTransaction, TState?, Task<TResult>> handler,
			CancellationToken ct)
		{
			Contract.NotNull(provider);

			return !ct.IsCancellationRequested && provider.TryGetDatabaseAndState(out var db, out var state) 
				? db.ReadAsync(state, handler, ct)
				: ReadDeferred(provider, handler, ct);

			static async Task<TResult> ReadDeferred(IFdbDatabaseScopeProvider<TState> provider, Func<IFdbReadOnlyTransaction, TState?, Task<TResult>> handler, CancellationToken ct)
			{
				var (db, state) = await provider.GetDatabaseAndState(ct).ConfigureAwait(false);
				return await db.ReadAsync(state, handler, ct).ConfigureAwait(false);
			}
		}

		/// <summary>Runs a transactional lambda function inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="provider">Provider of the database</param>
		/// <param name="handler">Asynchronous handler that will be retried until it succeeds, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// Since the handler can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static Task<List<TResult>> QueryAsync<TResult>(
			this IFdbDatabaseScopeProvider provider,
			[InstantHandle] Func<IFdbReadOnlyTransaction, IAsyncQuery<TResult>> handler,
			CancellationToken ct)
		{
			Contract.NotNull(provider);

			return !ct.IsCancellationRequested && provider.TryGetDatabase(out var db)
				? db.QueryAsync(handler, ct)
				: QueryDeferred(provider, handler, ct);

			static async Task<List<TResult>> QueryDeferred(IFdbDatabaseScopeProvider provider, Func<IFdbReadOnlyTransaction, IAsyncQuery<TResult>> handler, CancellationToken ct)
			{
				var db = await provider.GetDatabase(ct).ConfigureAwait(false);
				return await db.QueryAsync(handler, ct).ConfigureAwait(false);
			}
		}

		/// <summary>Runs a transactional lambda function inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="provider">Provider of the database</param>
		/// <param name="handler">Asynchronous handler that will be retried until it succeeds, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// Since the handler can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static Task<List<TResult>> QueryAsync<TResult>(
			this IFdbDatabaseScopeProvider provider,
			[InstantHandle] Func<IFdbReadOnlyTransaction, IAsyncEnumerable<TResult>> handler,
			CancellationToken ct)
		{
			Contract.NotNull(provider);

			return !ct.IsCancellationRequested && provider.TryGetDatabase(out var db)
				? db.QueryAsync(handler, ct)
				: QueryDeferred(provider, handler, ct);

			static async Task<List<TResult>> QueryDeferred(IFdbDatabaseScopeProvider provider, Func<IFdbReadOnlyTransaction, IAsyncEnumerable<TResult>> handler, CancellationToken ct)
			{
				var db = await provider.GetDatabase(ct).ConfigureAwait(false);
				return await db.QueryAsync(handler, ct).ConfigureAwait(false);
			}
		}

		/// <summary>Runs a transactional lambda function inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="provider">Provider of the database</param>
		/// <param name="handler">Asynchronous handler that will be retried until it succeeds, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// Since the handler can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static Task<List<TResult>> QueryAsync<TResult>(
			this IFdbDatabaseScopeProvider provider,
			[InstantHandle] Func<IFdbReadOnlyTransaction, Task<IAsyncQuery<TResult>>> handler,
			CancellationToken ct)
		{
			Contract.NotNull(provider);

			return !ct.IsCancellationRequested && provider.TryGetDatabase(out var db)
				? db.QueryAsync(handler, ct)
				: QueryDeferred(provider, handler, ct);

			static async Task<List<TResult>> QueryDeferred(IFdbDatabaseScopeProvider provider, Func<IFdbReadOnlyTransaction, Task<IAsyncQuery<TResult>>> handler, CancellationToken ct)
			{
				var db = await provider.GetDatabase(ct).ConfigureAwait(false);
				return await db.QueryAsync(handler, ct).ConfigureAwait(false);
			}
		}

		/// <summary>Runs a transactional lambda function inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="provider">Provider of the database</param>
		/// <param name="handler">Asynchronous handler that will be retried until it succeeds, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// Since the handler can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static Task<List<TResult>> QueryAsync<TResult>(
			this IFdbDatabaseScopeProvider provider,
			[InstantHandle] Func<IFdbReadOnlyTransaction, Task<IAsyncLinqQuery<TResult>>> handler,
			CancellationToken ct)
		{
			Contract.NotNull(provider);

			return !ct.IsCancellationRequested && provider.TryGetDatabase(out var db)
				? db.QueryAsync(handler, ct)
				: QueryDeferred(provider, handler, ct);

			static async Task<List<TResult>> QueryDeferred(IFdbDatabaseScopeProvider provider, Func<IFdbReadOnlyTransaction, Task<IAsyncLinqQuery<TResult>>> handler, CancellationToken ct)
			{
				var db = await provider.GetDatabase(ct).ConfigureAwait(false);
				return await db.QueryAsync(handler, ct).ConfigureAwait(false);
			}
		}
		/// <summary>Runs a transactional lambda function inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="provider">Provider of the database</param>
		/// <param name="handler">Asynchronous handler that will be retried until it succeeds, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// Since the handler can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static Task<List<TResult>> QueryAsync<TResult>(
			this IFdbDatabaseScopeProvider provider,
			[InstantHandle] Func<IFdbReadOnlyTransaction, Task<IAsyncEnumerable<TResult>>> handler,
			CancellationToken ct)
		{
			Contract.NotNull(provider);

			return !ct.IsCancellationRequested && provider.TryGetDatabase(out var db)
				? db.QueryAsync(handler, ct)
				: QueryDeferred(provider, handler, ct);

			static async Task<List<TResult>> QueryDeferred(IFdbDatabaseScopeProvider provider, Func<IFdbReadOnlyTransaction, Task<IAsyncEnumerable<TResult>>> handler, CancellationToken ct)
			{
				var db = await provider.GetDatabase(ct).ConfigureAwait(false);
				return await db.QueryAsync(handler, ct).ConfigureAwait(false);
			}
		}

		/// <summary>Run an idempotent transactional block that returns a value, inside a read-write transaction, which can be executed more than once if any retry-able error occurs.</summary>
		/// <param name="provider">Provider of the database</param>
		/// <param name="handler">Idempotent asynchronous lambda function that will be retried until the transaction commits, or a non-recoverable error occurs. The returned value of the last call will be the result of the operation.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the lambda function if the transaction committed successfully.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static Task<TResult> ReadWriteAsync<TResult>(
			this IFdbDatabaseScopeProvider provider,
			[InstantHandle] Func<IFdbTransaction, Task<TResult>> handler,
			CancellationToken ct)
		{
			Contract.NotNull(provider);

			return !ct.IsCancellationRequested && provider.TryGetDatabase(out var db)
				? db.ReadWriteAsync(handler, ct)
				: ReadWriteDeferred(provider, handler, ct);

			static async Task<TResult> ReadWriteDeferred(
				IFdbDatabaseScopeProvider provider,
				Func<IFdbTransaction, Task<TResult>> handler,
				CancellationToken ct)
			{
				var db = await provider.GetDatabase(ct).ConfigureAwait(false);
				return await db.ReadWriteAsync(handler, ct).ConfigureAwait(false);
			}
		}

		/// <summary>Run an idempotent transactional block that returns a value, inside a read-write transaction, which can be executed more than once if any retry-able error occurs.</summary>
		/// <param name="provider">Provider of the database</param>
		/// <param name="handler">Idempotent asynchronous lambda function that will be retried until the transaction commits, or a non-recoverable error occurs. The returned value of the last call will be the result of the operation.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the lambda function if the transaction committed successfully.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static Task<TResult> ReadWriteAsync<TState, TResult>(
			this IFdbDatabaseScopeProvider<TState> provider,
			[InstantHandle] Func<IFdbTransaction, TState?, Task<TResult>> handler,
			CancellationToken ct)
		{
			Contract.NotNull(provider);

			return !ct.IsCancellationRequested && provider.TryGetDatabaseAndState(out var db, out var state)
				? db.ReadWriteAsync(state, handler, ct)
				: ReadWriteDeferred(provider, handler, ct);

			static async Task<TResult> ReadWriteDeferred(IFdbDatabaseScopeProvider<TState> provider, Func<IFdbTransaction, TState?, Task<TResult>> handler, CancellationToken ct)
			{
				var (db, state) = await provider.GetDatabaseAndState(ct).ConfigureAwait(false);
				return await db.ReadWriteAsync(state, handler, ct).ConfigureAwait(false);
			}
		}

		/// <summary>Run an idempotent transactional block that returns a value, inside a read-write transaction, which can be executed more than once if any retry-able error occurs.</summary>
		/// <param name="provider">Provider of the database</param>
		/// <param name="handler">Idempotent asynchronous lambda function that will be retried until the transaction commits, or a non-recoverable error occurs. The returned value of the last call will be the result of the operation.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the lambda function if the transaction committed successfully.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static Task WriteAsync(
			this IFdbDatabaseScopeProvider provider,
			[InstantHandle] Func<IFdbTransaction, Task> handler,
			CancellationToken ct)
		{
			Contract.NotNull(provider);

			return !ct.IsCancellationRequested && provider.TryGetDatabase(out var db)
				? db.WriteAsync(handler, ct)
				: WriteDeferred(provider, handler, ct);

			static async Task WriteDeferred(IFdbDatabaseScopeProvider provider, Func<IFdbTransaction, Task> handler, CancellationToken ct)
			{
				var db = await provider.GetDatabase(ct).ConfigureAwait(false);
				await db.WriteAsync(handler, ct).ConfigureAwait(false);
			}
		}

		/// <summary>Run an idempotent transactional block that returns a value, inside a read-write transaction, which can be executed more than once if any retry-able error occurs.</summary>
		/// <param name="provider">Provider of the database</param>
		/// <param name="handler">Idempotent asynchronous lambda function that will be retried until the transaction commits, or a non-recoverable error occurs. The returned value of the last call will be the result of the operation.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the lambda function if the transaction committed successfully.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static Task WriteAsync<TState>(
			this IFdbDatabaseScopeProvider<TState> provider,
			[InstantHandle] Func<IFdbTransaction, TState?, Task> handler,
			CancellationToken ct)
		{
			Contract.NotNull(provider);

			return !ct.IsCancellationRequested && provider.TryGetDatabaseAndState(out var db, out var state)
				? db.WriteAsync(WrapHandler(handler, state), ct)
				: WriteDeferred(provider, handler, ct);

			static Func<IFdbTransaction, Task> WrapHandler(Func<IFdbTransaction, TState?, Task> handler, TState? state) => (tr) => handler(tr, state);

			static async Task WriteDeferred(IFdbDatabaseScopeProvider<TState> provider, Func<IFdbTransaction, TState?, Task> handler, CancellationToken ct)
			{
				var (db, state) = await provider.GetDatabaseAndState(ct).ConfigureAwait(false);
				await db.WriteAsync(WrapHandler(handler, state), ct).ConfigureAwait(false);
			}
		}

		/// <summary>Run an idempotent transaction block inside a write-only transaction, which can be executed more than once if any retry-able error occurs.</summary>
		/// <param name="provider">Provider of the database</param>
		/// <param name="handler">Idempotent handler that should only call write methods on the transaction, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static Task WriteAsync(
			this IFdbDatabaseScopeProvider provider,
			[InstantHandle] Action<IFdbTransaction> handler,
			CancellationToken ct)
		{
			Contract.NotNull(provider);

			return !ct.IsCancellationRequested && provider.TryGetDatabase(out var db)
				? db.WriteAsync(handler, ct)
				: WriteDeferred(provider, handler, ct);

			static async Task WriteDeferred(IFdbDatabaseScopeProvider provider, Action<IFdbTransaction> handler, CancellationToken ct)
			{
				var db = await provider.GetDatabase(ct).ConfigureAwait(false);
				await db.WriteAsync(handler, ct).ConfigureAwait(false);
			}
		}

		/// <summary>Run an idempotent transaction block inside a write-only transaction, which can be executed more than once if any retry-able error occurs.</summary>
		/// <param name="provider">Provider of the database</param>
		/// <param name="handler">Idempotent handler that should only call write methods on the transaction, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static Task WriteAsync<TState>(
			this IFdbDatabaseScopeProvider<TState> provider,
			[InstantHandle] Action<IFdbTransaction, TState?> handler,
			CancellationToken ct)
		{
			Contract.NotNull(provider);

			return !ct.IsCancellationRequested && provider.TryGetDatabaseAndState(out var db, out var state)
				? db.WriteAsync(WrapHandler(handler, state), ct)
				: WriteDeferred(provider, handler, ct);

			static Action<IFdbTransaction> WrapHandler(Action<IFdbTransaction, TState?> handler, TState? state)
			{
				return (tr) => handler(tr, state);
			}

			static async Task WriteDeferred(IFdbDatabaseScopeProvider<TState> provider, Action<IFdbTransaction, TState?> handler, CancellationToken ct)
			{
				var (db, state) = await provider.GetDatabaseAndState(ct).ConfigureAwait(false);
				await db.WriteAsync(WrapHandler(handler, state), ct).ConfigureAwait(false);
			}
		}

	}

}
