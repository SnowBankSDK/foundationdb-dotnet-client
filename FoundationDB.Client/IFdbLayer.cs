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

	/// <summary>Represents a FoundationDB Layer that uses a metadata cache to speed up operations inside transactions</summary>
	/// <remarks>
	/// <para>A typical Layer will be a thin wrapper over a subspace location and some options/encoders/helpers</para>
	/// <para>When a transaction wants to interact with the Layer, is must first call Resolve(...) at least once within the transaction, and then call any method necessary on that state object.</para>
	/// <para>Storing and reusing state instances outside the transaction is NOT ALLOWED.</para>
	/// <para>The Layer implementation should try to cache complex metadata in order to reduce the latency.</para>
	/// <para>If the layer uses lower level layers, it should NOT attempt to cache these in its own state, and instead call these layer's own <see cref="IFdbLayer{T}.Resolve"/> methods</para>
	/// <para>This means that layers should not cache the TState type itself, and instead cache a "TCache" object, and create a new "TState" instance for each new transaction, that wraps the TCache</para>
	/// </remarks>
	public interface IFdbLayer
	{
		//note: this interface is primarily used to server as a "marker" for any Source Analyzer that would ensure that the layer state object does not escape the lifetime of the transaction

		/// <summary>Friendly name of the layer</summary>
		/// <remarks>For logging/debugging purpose</remarks>
		string Name { get; }

	}

	/// <summary>Represents a FoundationDB Layer that uses a metadata cache to speed up operations</summary>
	/// <typeparam name="TState">Type of the state that is linked to each transaction lifetime.</typeparam>
	/// <remarks>
	/// <para>A typical Layer will be a thin wrapper over a subspace location and some options/encoders/helpers</para>
	/// <para>When a transaction wants to interact with the Layer, is must first call Resolve(...) at least once within the transaction, and then call any method necessary on that state object.</para>
	/// <para>Storing and reusing state instances outside the transaction is NOT ALLOWED.</para>
	/// <para>The Layer implementation should try to cache complex metadata in order to reduce the latency.</para>
	/// <para>If the layer uses lower level layers, it should NOT attempt to cache these in its own state, and instead call these layer's own <see cref="Resolve"/> methods</para>
	/// <para>This means that layers should not cache the TState type itself, and instead cache a "TCache" object, and create a new "TState" instance for each new transaction, that wraps the TCache</para>
	/// </remarks>
	[PublicAPI]
	public interface IFdbLayer<TState> : IFdbLayer
	{

		/// <summary>Resolve the state for this layer, applicable to the current transaction</summary>
		/// <param name="tr">Transaction that will be used to interact with this layer</param>
		/// <returns>State handler that is valid only for this transaction.</returns>
		/// <remarks>
		/// Even though most layers will attempt to cache complex metadata, it is best practice to not call this method multiple times within the same transaction.
		/// Accessing the returned state instance outside the <paramref name="tr">transaction</paramref> scope (after commit, in the next retry loop attempt, ...) has undefined behavior and should be avoided.
		/// </remarks>
		ValueTask<TState> Resolve(IFdbReadOnlyTransaction tr);

	}

	/// <summary>Represents a FoundationDB Layer that uses a metadata cache to speed up operations</summary>
	/// <typeparam name="TState">Type of the state that is linked to each transaction lifetime.</typeparam>
	/// <typeparam name="TOptions">Type of the parameter that is passed to the <see cref="Resolve"/> method.</typeparam>
	/// <remarks>
	/// <para>A typical Layer will be a thin wrapper over a subspace location and some options/encoders/helpers</para>
	/// <para>When a transaction wants to interact with the Layer, is must first call Resolve(...) at least once within the transaction, and then call any method necessary on that state object.</para>
	/// <para>Storing and reusing state instances outside the transaction is NOT ALLOWED.</para>
	/// <para>The Layer implementation should try to cache complex metadata in order to reduce the latency.</para>
	/// <para>If the layer uses lower level layers, it should NOT attempt to cache these in its own state, and instead call these layer's own <see cref="Resolve"/> methods</para>
	/// <para>This means that layers should not cache the TState type itself, and instead cache a "TCache" object, and create a new "TState" instance for each new transaction, that wraps the TCache</para>
	/// </remarks>
	public interface IFdbLayer<TState, in TOptions> : IFdbLayer
	{

		/// <summary>Resolve the state for this layer, applicable to the current transaction</summary>
		/// <param name="tr">Transaction that will be used to interact with this layer</param>
		/// <param name="options">Optional parameter that depends on the type of layer</param>
		/// <returns>State handler that is valid only for this transaction.</returns>
		/// <remarks>
		/// Even though most layers will attempt to cache complex metadata, it is best practice to not call this method multiple times within the same transaction.
		/// Accessing the returned state instance outside the <paramref name="tr">transaction</paramref> scope (after commit, in the next retry loop attempt, ...) has undefined behavior and should be avoided.
		/// </remarks>
		ValueTask<TState> Resolve(IFdbReadOnlyTransaction tr, TOptions options);

	}

	/// <summary>Exposes the schema of key/value pairs used by this layer</summary>
	/// <remarks>This can be used by tools and loggers to generate a more user-friendly representation of the content generated by this layer.</remarks>
	public interface IFdbLayerSchemaMapper
	{

		/// <summary>LayerId used by the main <see cref="FdbDirectorySubspace"/> for this layer</summary>
		/// <remarks>Any subspace with this LayerId will use this mapper as a source of key templates</remarks>
		string LayerId { get; }

		/// <summary>Enumerates all the templates for keys that can be found subspaces using the <see cref="LayerId"/> for this mapper.</summary>
		IEnumerable<FqlTemplateExpression> GetRules();

	}

	/// <summary>Set of helper methods for working with <see cref="IFdbLayer{TState}"/> instances</summary>
	[PublicAPI]
	public static class FdbLayerExtensions
	{

		#region IFdbLayer<TState>...

		/// <summary>Run an idempotent transaction block inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="layer">Layer that will be resolved using the transaction, and will be passed as the second argument to <paramref name="handler"/>.</param>
		/// <param name="db">Database instance that will be used to start the transaction</param>
		/// <param name="handler">Idempotent handler that will only read from the database, and may be retried if a recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the last successful execution of <paramref name="handler"/>.</returns>
		/// <remarks>
		/// Any attempt to write or commit using the transaction will throw.
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static Task<TResult> ReadAsync<TState, TResult>(
			this IFdbLayer<TState> layer,
			IFdbReadOnlyRetryable db,
			Func<IFdbReadOnlyTransaction, TState, Task<TResult>> handler,
			CancellationToken ct)
		{
			return db.ReadAsync(
				(layer, handler),
				async (tr, s) =>
				{
					var state = await s.layer.Resolve(tr).ConfigureAwait(false);
					return await s.handler(tr, state).ConfigureAwait(false);
				},
				ct);
		}

		/// <summary>Run an idempotent transaction block inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="layer">Layer that will be resolved using the transaction, and will be passed as the second argument to <paramref name="handler"/>.</param>
		/// <param name="db">Database instance that will be used to start the transaction</param>
		/// <param name="handler">Idempotent handler that will only read from the database, and may be retried if a recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Task that succeeds if no error occurred during execution of <paramref name="handler"/>.</returns>
		/// <remarks>
		/// Since the method does not result any result, it should only be used to verify the content of the database.
		/// Any attempt to write or commit using the transaction will throw.
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static Task ReadAsync<TState>(
			this IFdbLayer<TState> layer,
			IFdbReadOnlyRetryable db,
			Func<IFdbReadOnlyTransaction, TState, Task> handler,
			CancellationToken ct)
		{
			return db.ReadAsync(
				(layer, handler),
				async (tr, s) =>
				{
					var state = await (s.layer).Resolve(tr).ConfigureAwait(false);
					await s.handler(tr, state).ConfigureAwait(false);
				},
				ct);
		}

		/// <summary>Run an idempotent transaction block inside a writable transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="layer">Layer that will be resolved using the transaction, and will be passed as the second argument to <paramref name="handler"/>.</param>
		/// <param name="db">Database instance that will be used to start the transaction</param>
		/// <param name="handler">Idempotent handler that will attempt to mutate the database, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the last successful execution of <paramref name="handler"/>.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically!
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static Task<TResult> ReadWriteAsync<TState, TResult>(
			this IFdbLayer<TState> layer,
			IFdbRetryable db,
			Func<IFdbTransaction, TState, Task<TResult>> handler,
			CancellationToken ct)
		{
			return db.ReadWriteAsync(
				(layer, handler),
				async (tr, s) =>
				{
					var state = await (s.layer).Resolve(tr).ConfigureAwait(false);
					return await s.handler(tr, state).ConfigureAwait(false);
				},
				ct);
		}

		/// <summary>Run an idempotent transaction block inside a writable transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="layer">Layer that will be resolved using the transaction, and will be passed as the second argument to <paramref name="handler"/>.</param>
		/// <param name="db">Database instance that will be used to start the transaction</param>
		/// <param name="handler">Idempotent handler that will attempt to mutate the database, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically!
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static Task WriteAsync<TState>(
			this IFdbLayer<TState> layer,
			IFdbRetryable db,
			Action<IFdbTransaction, TState> handler,
			CancellationToken ct)
		{
			return db.WriteAsync(
				(layer, handler),
				async (tr, s) =>
				{
					var state = await (s.layer).Resolve(tr).ConfigureAwait(false);
					s.handler(tr, state);
				},
				ct);
		}

		/// <summary>Run an idempotent transaction block inside a writable transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="layer">Layer that will be resolved using the transaction, and will be passed as the second argument to <paramref name="handler"/>.</param>
		/// <param name="db">Database instance that will be used to start the transaction</param>
		/// <param name="handler">Idempotent handler that will attempt to mutate the database, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically!
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static Task WriteAsync<TState>(
			this IFdbLayer<TState> layer,
			IFdbRetryable db,
			Func<IFdbTransaction, TState, Task> handler,
			CancellationToken ct)
		{
			return db.WriteAsync(
				(layer, handler),
				async (tr, s) =>
				{
					var state = await (s.layer).Resolve(tr).ConfigureAwait(false);
					await s.handler(tr, state).ConfigureAwait(false);
				},
				ct);
		}

		#endregion

		#region IFdbLayer<TState, TOptions>...

		/// <summary>Run an idempotent transaction block inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="layer">Layer that will be resolved using the transaction, and will be passed as the second argument to <paramref name="handler"/>.</param>
		/// <param name="db">Database instance that will be used to start the transaction</param>
		/// <param name="options">Parameter that is used when resolving this layer</param>
		/// <param name="handler">Idempotent handler that will only read from the database, and may be retried if a recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the last successful execution of <paramref name="handler"/>.</returns>
		/// <remarks>
		/// Any attempt to write or commit using the transaction will throw.
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static Task<TResult> ReadAsync<TState, TOptions, TResult>(
			this IFdbLayer<TState, TOptions> layer,
			IFdbReadOnlyRetryable db,
			TOptions options,
			Func<IFdbReadOnlyTransaction, TState, Task<TResult>> handler,
			CancellationToken ct)
		{
			return db.ReadAsync(
				(layer, options, handler),
				async (tr, s) =>
				{
					var state = await s.layer.Resolve(tr, s.options).ConfigureAwait(false);
					return await s.handler(tr, state).ConfigureAwait(false);
				},
				ct);
		}

		/// <summary>Run an idempotent transaction block inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="layer">Layer that will be resolved using the transaction, and will be passed as the second argument to <paramref name="handler"/>.</param>
		/// <param name="db">Database instance that will be used to start the transaction</param>
		/// <param name="options">Parameter that is used when resolving this layer</param>
		/// <param name="handler">Idempotent handler that will only read from the database, and may be retried if a recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Task that succeeds if no error occurred during execution of <paramref name="handler"/>.</returns>
		/// <remarks>
		/// Since the method does not result any result, it should only be used to verify the content of the database.
		/// Any attempt to write or commit using the transaction will throw.
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static Task ReadAsync<TState, TOptions>(
			this IFdbLayer<TState, TOptions> layer,
			IFdbReadOnlyRetryable db,
			TOptions options,
			Func<IFdbReadOnlyTransaction, TState, Task> handler,
			CancellationToken ct)
		{
			return db.ReadAsync(
				(layer, options, handler),
				async (tr, s) =>
				{
					var state = await (s.layer).Resolve(tr, s.options).ConfigureAwait(false);
					await s.handler(tr, state).ConfigureAwait(false);
				},
				ct);
		}

		/// <summary>Run an idempotent transaction block inside a writable transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="layer">Layer that will be resolved using the transaction, and will be passed as the second argument to <paramref name="handler"/>.</param>
		/// <param name="db">Database instance that will be used to start the transaction</param>
		/// <param name="options">Parameter that is used when resolving this layer</param>
		/// <param name="handler">Idempotent handler that will attempt to mutate the database, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the last successful execution of <paramref name="handler"/>.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically!
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static Task<TResult> ReadWriteAsync<TState, TOptions, TResult>(
			this IFdbLayer<TState, TOptions> layer,
			IFdbRetryable db,
			TOptions options,
			Func<IFdbTransaction, TState, Task<TResult>> handler,
			CancellationToken ct)
		{
			return db.ReadWriteAsync(
				(layer, options, handler),
				async (tr, s) =>
				{
					var state = await (s.layer).Resolve(tr, s.options).ConfigureAwait(false);
					return await s.handler(tr, state).ConfigureAwait(false);
				},
				ct);
		}

		/// <summary>Run an idempotent transaction block inside a writable transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="layer">Layer that will be resolved using the transaction, and will be passed as the second argument to <paramref name="handler"/>.</param>
		/// <param name="db">Database instance that will be used to start the transaction</param>
		/// <param name="options">Parameter that is used when resolving this layer</param>
		/// <param name="handler">Idempotent handler that will attempt to mutate the database, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically!
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static Task WriteAsync<TState, TOptions>(
			this IFdbLayer<TState, TOptions> layer,
			IFdbRetryable db,
			TOptions options,
			Action<IFdbTransaction, TState> handler,
			CancellationToken ct)
		{
			return db.WriteAsync(
				(layer, options, handler),
				async (tr, s) =>
				{
					var state = await (s.layer).Resolve(tr, s.options).ConfigureAwait(false);
					s.handler(tr, state);
				},
				ct);
		}

		/// <summary>Run an idempotent transaction block inside a writable transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="layer">Layer that will be resolved using the transaction, and will be passed as the second argument to <paramref name="handler"/>.</param>
		/// <param name="db">Database instance that will be used to start the transaction</param>
		/// <param name="options">Parameter that is used when resolving this layer</param>
		/// <param name="handler">Idempotent handler that will attempt to mutate the database, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically!
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static Task WriteAsync<TState, TOptions>(
			this IFdbLayer<TState, TOptions> layer,
			IFdbRetryable db,
			TOptions options,
			Func<IFdbTransaction, TState, Task> handler,
			CancellationToken ct)
		{
			return db.WriteAsync(
				(layer, options, handler),
				async (tr, s) =>
				{
					var state = await (s.layer).Resolve(tr, s.options).ConfigureAwait(false);
					await s.handler(tr, state).ConfigureAwait(false);
				},
				ct);
		}

		#endregion

	}

}
