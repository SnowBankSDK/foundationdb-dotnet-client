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
	/// <summary>Transactional context that can execute, inside a retry loop, idempotent actions using read-only transactions.</summary>
	[PublicAPI]
	public interface IFdbReadOnlyRetryable
	{

		#region Important Note: Differences with Python's @transactional and Java's TransactionContext

		// This interface is supposed to be the equivalent of the @transactional Python attribute, and the TransactionContext base class in Java,
		// but it has one MAJOR difference with the other bindings!
		//
		// In the other bindings, the notion of @transactional is a way to hide, from the caller, the behaviour of the object in case of failures:
		// - sometimes the errors will bubble up (if the instance is a Transaction), and the caller has to deal with it
		// - sometimes the errors will be retried under the hood an unspecified number of times, and may or may not still blow up at some point.

		// I think that this is a very dangerous thing, for the following reasons:
		// 1. This can easily create race conditions and weird bugs: since your lambda may or may not be called multiple times, the code must be aware of this fact!
		//   => the most common bug is to update a global cache or state inside the lambda, BEFORE the transaction is committed. This can either fill the case with invalid value (commit fails),
		//      or worse you could update multiple times a global value if the transaction is retried.
		// 2. This is not composable: even though the actions themselves could be composable, the code that must deal with errors and cancellation is NOT.
		//   If you want to write robust code you HAVE to know the exact behavior of the instance, which would force you to have different code path depending on the KIND of transactional instance you got.
		//   => If you have to check the actual type (Transaction or Database) anyway, why not explicitly ask for one or the other?
		// 3. Since .NET cannot simulate the @transactional Python attribute behavior easily, you are forced to add multiple version of the methods, ones that takes IFdbTransactions and ones that take an IFdbTransactional
		//   This creates a lot of code duplication, and you end up with alias methods that are simply doing `FooAsync(IFdbTransaction dbOrTrans, ...) { return db.ReadWriteAsync((tr) => FooAsync(tr, ...), ...); }`
		//   => the caller of the layer could easily write the same thing, and now would be in a position to compose multiple calls to the layer (and other layers) in the same retry loop.

		// For these reasons, regular IFdbTransaction DO NOT implement this interface, and the interface is called "IFdbRetryable" instead of "IFdbTransactional" (which used to be the name of this interface before the design change)

		#endregion

		/// <summary>Runs an idempotent transaction block inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent handler that will only read from the database, and may be retried if a recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Task that succeeds if no error occurred during execution of <paramref name="handler"/>.</returns>
		/// <remarks>
		/// Since the method does not result any result, it should only be used to verify the content of the database.
		/// Any attempt to write or commit using the transaction will throw.
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task ReadAsync(Func<IFdbReadOnlyTransaction, Task> handler, CancellationToken ct);

		/// <summary>Runs an idempotent transaction block inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="state">Caller-provided context or state that will be passed through to the <paramref name="handler"/></param>
		/// <param name="handler">Idempotent handler that will only read from the database, and may be retried if a recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Task that succeeds if no error occurred during execution of <paramref name="handler"/>.</returns>
		/// <remarks>
		/// Since the method does not result any result, it should only be used to verify the content of the database.
		/// Any attempt to write or commit using the transaction will throw.
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task ReadAsync<TState>(TState state, Func<IFdbReadOnlyTransaction, TState, Task> handler, CancellationToken ct);

		/// <summary>Runs an idempotent transaction block inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent handler that will only read from the database, and may be retried if a recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the last successful execution of <paramref name="handler"/>.</returns>
		/// <remarks>
		/// Any attempt to write or commit using the transaction will throw.
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task<TResult> ReadAsync<TResult>(Func<IFdbReadOnlyTransaction, Task<TResult>> handler, CancellationToken ct);

		/// <summary>Runs an idempotent transaction block inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="state">Caller-provided context or state that will be passed through to the <paramref name="handler"/></param>
		/// <param name="handler">Idempotent handler that will only read from the database, and may be retried if a recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the last successful execution of <paramref name="handler"/>.</returns>
		/// <remarks>
		/// Any attempt to write or commit using the transaction will throw.
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task<TResult> ReadAsync<TState, TResult>(TState state, Func<IFdbReadOnlyTransaction, TState, Task<TResult>> handler, CancellationToken ct);

		/// <summary>Runs an idempotent transaction block inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent handler that will only read from the database, and may be retried if a recoverable error occurs.</param>
		/// <param name="success">Handler that will be called once the transaction executes successfully. The result from the last invocation of <paramref name="handler"/> will be passed as the second parameter.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the last successful execution of <paramref name="handler"/>.</returns>
		/// <remarks>
		/// Any attempt to write or commit using the transaction will throw.
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task<TResult> ReadAsync<TResult>(Func<IFdbReadOnlyTransaction, Task<TResult>> handler, Action<IFdbReadOnlyTransaction, TResult> success, CancellationToken ct);

		/// <summary>Runs an idempotent transaction block inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent handler that will only read from the database, and may be retried if a recoverable error occurs.</param>
		/// <param name="success">Handler that will be called once the transaction executes successfully. The result from the last invocation of <paramref name="handler"/> will be passed as the second parameter.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result return by <paramref name="success"/>, if it was called.</returns>
		/// <remarks>
		/// Any attempt to write or commit using the transaction will throw.
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task<TResult> ReadAsync<TIntermediate, TResult>(Func<IFdbReadOnlyTransaction, Task<TIntermediate>> handler, Func<IFdbReadOnlyTransaction, TIntermediate, TResult> success, CancellationToken ct);

		/// <summary>Runs an idempotent transaction block inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent handler that will only read from the database, and may be retried if a recoverable error occurs.</param>
		/// <param name="success">Handler that will be called once the transaction executes successfully. The result from the last invocation of <paramref name="handler"/> will be passed as the second parameter.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result return by <paramref name="success"/>, if it was called.</returns>
		/// <remarks>
		/// Any attempt to write or commit using the transaction will throw.
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task<TResult> ReadAsync<TIntermediate, TResult>(Func<IFdbReadOnlyTransaction, Task<TIntermediate>> handler, Func<IFdbReadOnlyTransaction, TIntermediate, Task<TResult>> success, CancellationToken ct);

		/// <summary>Runs an idempotent transaction block inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="state">Caller-provided context or state that will be passed through to the <paramref name="handler"/></param>
		/// <param name="handler">Idempotent handler that will only read from the database, and may be retried if a recoverable error occurs.</param>
		/// <param name="success">Handler that will be called once the transaction executes successfully. The result from the last invocation of <paramref name="handler"/> will be passed as the second parameter.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result return by <paramref name="success"/>, if it was called.</returns>
		/// <remarks>
		/// Any attempt to write or commit using the transaction will throw.
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task<TResult> ReadAsync<TState, TIntermediate, TResult>(TState state, Func<IFdbReadOnlyTransaction, TState, Task<TIntermediate>> handler, Func<IFdbReadOnlyTransaction, TIntermediate, Task<TResult>> success, CancellationToken ct);

	}

}
