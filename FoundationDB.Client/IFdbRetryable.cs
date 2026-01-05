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

	/// <summary>Transactional context that can execute, inside a retry loop, idempotent actions using read and/or write transactions.</summary>
	[PublicAPI]
	public interface IFdbRetryable : IFdbReadOnlyRetryable
	{
		// note: see IFdbReadOnlyRetryable for comments about the differences between the .NET binding and other binding regarding the design of Transactionals

		#region Write Only...

		// All WriteAsync(...) methods do not return anything to the caller

		/// <summary>Run an idempotent transaction block inside a writable transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent handler that will attempt to mutate the database, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically!
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// Alternatively, you can call <see cref="WriteAsync(Action{IFdbTransaction},Action{IFdbTransaction}, CancellationToken)"/> that supports a 'success' callback.
		/// </remarks>
		Task WriteAsync(Action<IFdbTransaction> handler, CancellationToken ct);

		/// <summary>Run an idempotent transaction block inside a writable transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="state">Caller-provided context or state that will be passed through to the <paramref name="handler"/></param>
		/// <param name="handler">Idempotent handler that will attempt to mutate the database, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically!
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// Alternatively, you can call <see cref="WriteAsync(Action{IFdbTransaction},Action{IFdbTransaction}, CancellationToken)"/> that supports a 'success' callback.
		/// </remarks>
		Task WriteAsync<TState>(TState state, Action<IFdbTransaction, TState> handler, CancellationToken ct);

		/// <summary>Run an idempotent transaction block inside a writable transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent handler that will attempt to mutate the database, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically!
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// Alternatively, you can call <see cref="WriteAsync(Func{IFdbTransaction,Task},Action{IFdbTransaction}, CancellationToken)"/> that supports a 'success' callback.
		/// </remarks>
		Task WriteAsync(Func<IFdbTransaction, Task> handler, CancellationToken ct);

		/// <summary>Run an idempotent transaction block inside a writable transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="state">Caller-provided context or state that will be passed through to the <paramref name="handler"/></param>
		/// <param name="handler">Idempotent handler that will attempt to mutate the database, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically!
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// Alternatively, you can call <see cref="WriteAsync(Func{IFdbTransaction,Task},Action{IFdbTransaction}, CancellationToken)"/> that supports a 'success' callback.
		/// </remarks>
		Task WriteAsync<TState>(TState state, Func<IFdbTransaction, TState, Task> handler, CancellationToken ct);

		/// <summary>Run an idempotent transaction block inside a writable transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent handler that will attempt to mutate the database, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="success">Handler that will be called once the transaction commits successfully.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically!
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda, and only inside the <paramref name="success"/> handler!
		/// Please note that there is NO guarantee that <paramref name="success"/> will be invoked even if the transaction commits successfully! The execution may be interrupted before the handler has time to execute.
		/// </remarks>
		Task WriteAsync(Action<IFdbTransaction> handler, Action<IFdbTransaction> success, CancellationToken ct);

		/// <summary>Run an idempotent transaction block inside a writable transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent handler that will attempt to mutate the database, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="success">Handler that will be called once the transaction commits successfully.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically!
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda, and only inside the <paramref name="success"/> handler!
		/// Please note that there is NO guarantee that <paramref name="success"/> will be invoked even if the transaction commits successfully! The execution may be interrupted before the handler has time to execute.
		/// </remarks>
		Task WriteAsync(Action<IFdbTransaction> handler, Func<IFdbTransaction, Task> success, CancellationToken ct);

		/// <summary>Run an idempotent transaction block inside a writable transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent handler that will attempt to mutate the database, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="success">Handler that will be called once the transaction commits successfully.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically!
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda, and only inside the <paramref name="success"/> handler!
		/// Please note that there is NO guarantee that <paramref name="success"/> will be invoked even if the transaction commits successfully! The execution may be interrupted before the handler has time to execute.
		/// </remarks>
		Task WriteAsync(Func<IFdbTransaction, Task> handler, Action<IFdbTransaction> success, CancellationToken ct);

		/// <summary>Run an idempotent transaction block inside a writable transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent handler that will attempt to mutate the database, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="success">Handler that will be called once the transaction commits successfully.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically!
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda, and only inside the <paramref name="success"/> handler!
		/// Please note that there is NO guarantee that <paramref name="success"/> will be invoked even if the transaction commits successfully! The execution may be interrupted before the handler has time to execute.
		/// </remarks>
		Task WriteAsync(Func<IFdbTransaction, Task> handler, Func<IFdbTransaction, Task> success, CancellationToken ct);

		#endregion

		#region Read/Write...

		// All ReadWriteAsync(...) methods return something to the caller

		/// <summary>Run an idempotent transaction block inside a writable transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent handler that will attempt to mutate the database, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the last successful execution of <paramref name="handler"/>.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically!
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task<TResult> ReadWriteAsync<TResult>(Func<IFdbTransaction, Task<TResult>> handler, CancellationToken ct);

		/// <summary>Run an idempotent transaction block inside a writable transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="state">Caller-provided context or state that will be passed through to the <paramref name="handler"/></param>
		/// <param name="handler">Idempotent handler that will attempt to mutate the database, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the last successful execution of <paramref name="handler"/>.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically!
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task<TResult> ReadWriteAsync<TState, TResult>(TState state, Func<IFdbTransaction, TState, Task<TResult>> handler, CancellationToken ct);

		/// <summary>Run an idempotent transaction block inside a writable transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent handler that will attempt to mutate the database, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="success">Handler that will be called once the transaction commits successfully.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result return by <paramref name="success"/>, if it was called.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically!
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda, and only inside the <paramref name="success"/> handler!
		/// Please note that there is NO guarantee that <paramref name="success"/> will be invoked even if the transaction commits successfully! The execution may be interrupted before the handler has time to execute.
		/// </remarks>
		Task<TResult> ReadWriteAsync<TResult>(Action<IFdbTransaction> handler, Func<IFdbTransaction, TResult> success, CancellationToken ct);

		/// <summary>Run an idempotent transaction block inside a writable transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent handler that will attempt to mutate the database, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="success">Handler that will be called once the transaction commits successfully.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the last successful execution of <paramref name="handler"/>.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically!
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda, and only inside the <paramref name="success"/> handler!
		/// Please note that there is NO guarantee that <paramref name="success"/> will be invoked even if the transaction commits successfully! The execution may be interrupted before the handler has time to execute.
		/// </remarks>
		Task<TResult> ReadWriteAsync<TResult>(Func<IFdbTransaction, Task<TResult>> handler, Action<IFdbTransaction, TResult> success, CancellationToken ct);

		/// <summary>Run an idempotent transaction block inside a writable transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent handler that will attempt to mutate the database, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="success">Handler that will be called once the transaction commits successfully.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result return by <paramref name="success"/>, if it was called.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically!
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda, and only inside the <paramref name="success"/> handler!
		/// Please note that there is NO guarantee that <paramref name="success"/> will be invoked even if the transaction commits successfully! The execution may be interrupted before the handler has time to execute.
		/// </remarks>
		Task<TResult> ReadWriteAsync<TResult>(Func<IFdbTransaction, Task> handler, Func<IFdbTransaction, Task<TResult>> success, CancellationToken ct);

		/// <summary>Run an idempotent transaction block inside a writable transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent handler that will attempt to mutate the database, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="success">Handler that will be called once the transaction commits successfully.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result return by <paramref name="success"/>, if it was called.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically!
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda, and only inside the <paramref name="success"/> handler!
		/// Please note that there is NO guarantee that <paramref name="success"/> will be invoked even if the transaction commits successfully! The execution may be interrupted before the handler has time to execute.
		/// </remarks>
		Task<TResult> ReadWriteAsync<TResult>(Func<IFdbTransaction, Task> handler, Func<IFdbTransaction, TResult> success, CancellationToken ct);

		/// <summary>Run an idempotent transaction block inside a writable transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent handler that will attempt to mutate the database, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="success">Handler that will be called once the transaction commits successfully. The intermediate result from the last invocation of <paramref name="handler"/> will be passed as the seconde parameter.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result return by <paramref name="success"/>, if it was called.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically!
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda, and only inside the <paramref name="success"/> handler!
		/// Please note that there is NO guarantee that <paramref name="success"/> will be invoked even if the transaction commits successfully! The execution may be interrupted before the handler has time to execute.
		/// </remarks>
		Task<TResult> ReadWriteAsync<TIntermediate, TResult>(Func<IFdbTransaction, Task<TIntermediate>> handler, Func<IFdbTransaction, TIntermediate, TResult> success, CancellationToken ct);

		/// <summary>Run an idempotent transaction block inside a writable transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent handler that will attempt to mutate the database, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="success">Handler that will be called once the transaction commits successfully. The intermediate result from the last invocation of <paramref name="handler"/> will be passed as the seconde parameter.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result return by <paramref name="success"/>, if it was called.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically!
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda, and only inside the <paramref name="success"/> handler!
		/// Please note that there is NO guarantee that <paramref name="success"/> will be invoked even if the transaction commits successfully! The execution may be interrupted before the handler has time to execute.
		/// </remarks>
		Task<TResult> ReadWriteAsync<TIntermediate, TResult>(Func<IFdbTransaction, Task<TIntermediate>> handler, Func<IFdbTransaction, TIntermediate, Task<TResult>> success, CancellationToken ct);

		/// <summary>Run an idempotent transaction block inside a writable transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="state">Caller-provided context or state that will be passed through to the <paramref name="handler"/></param>
		/// <param name="handler">Idempotent handler that will attempt to mutate the database, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="success">Handler that will be called once the transaction commits successfully. The intermediate result from the last invocation of <paramref name="handler"/> will be passed as the seconde parameter.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result return by <paramref name="success"/>, if it was called.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically!
		/// Given that the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MUST NOT mutate any global state (counters, cache, global dictionary) inside this lambda, and only inside the <paramref name="success"/> handler!
		/// Please note that there is NO guarantee that <paramref name="success"/> will be invoked even if the transaction commits successfully! The execution may be interrupted before the handler has time to execute.
		/// </remarks>
		Task<TResult> ReadWriteAsync<TState, TIntermediate, TResult>(TState state, Func<IFdbTransaction, TState, Task<TIntermediate>> handler, Func<IFdbTransaction, TIntermediate, Task<TResult>> success, CancellationToken ct);

		#endregion

	}

}
