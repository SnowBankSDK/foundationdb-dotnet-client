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

// enable this to help debug Futures
//#define DEBUG_FUTURES

namespace FoundationDB.Client.Native
{
	using System.Collections.Concurrent;
	using FoundationDB.Client.Utils;

	/// <summary>Helper class to create FDBFutures</summary>
	public static class FdbFuture
	{

		public static class Flags
		{
			/// <summary>Future is being constructed and is not yet ready.</summary>
			public const int DEFAULT = 0;

			/// <summary>The future has completed (either success or failure)</summary>
			public const int COMPLETED = 1;

			/// <summary>The future has been cancelled from an external source (manually, or via then CancellationToken)</summary>
			public const int CANCELLED = 2;

			/// <summary>The resources allocated by this future have been released</summary>
			public const int MEMORY_RELEASED = 4;

			/// <summary>The future has been constructed, and is listening for the callbacks</summary>
			public const int READY = 64;

			/// <summary>Dispose has been called</summary>
			public const int DISPOSED = 128;
		}

		/// <summary>Internal counter to generate a unique parameter value for each futures</summary>
		internal static long s_futureCounter;

		/// <summary>Creates a new <see cref="FdbFutureSingle{TState,TResult}"/> from an FDBFuture* pointer</summary>
		/// <typeparam name="TResult">Type of the result of the task</typeparam>
		/// <typeparam name="TState">Type of the state that will be passed to the result selector</typeparam>
		/// <param name="handle">FDBFuture* pointer</param>
		/// <param name="state">State that is passed to the result selector</param>
		/// <param name="selector">Func that will be called to get the result once the future completes (and did not fail)</param>
		/// <param name="ct">Optional cancellation token that can be used to cancel the future</param>
		/// <returns>Object that tracks the execution of the FDBFuture handle</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbFutureSingle<TState, TResult> FromHandle<TState, TResult>(FutureHandle handle, TState state, Func<FutureHandle, TState, TResult> selector, CancellationToken ct)
		{
			return new(handle, state, selector, ct);
		}

		/// <summary>Wraps a FDBFuture* pointer into a <see cref="Task"/></summary>
		/// <param name="handle">FDBFuture* pointer</param>
		/// <param name="ct">Optional cancellation token that can be used to cancel the future</param>
		/// <returns>Object that tracks the execution of the FDBFuture handle</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Task CreateTaskFromHandle(FutureHandle handle, CancellationToken ct)
		{
			return new FdbFutureSingle<object?, object?>(handle, null, null, ct).Task;
		}

		/// <summary>Wraps a FDBFuture* pointer into a <see cref="Task{T}"/></summary>
		/// <typeparam name="TResult">Type of the result of the task</typeparam>
		/// <typeparam name="TState">Type of the state that will be passed to the result selector</typeparam>
		/// <param name="handle">FDBFuture* pointer</param>
		/// <param name="state">State that is passed to the result selector</param>
		/// <param name="selector">Lambda that will be called once the future completes successfully, to extract the result from the future handle.</param>
		/// <param name="ct">Optional cancellation token that can be used to cancel the future</param>
		/// <returns>Task that will either return the result of the continuation lambda, or an exception</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Task<TResult> CreateTaskFromHandle<TState, TResult>(FutureHandle handle, TState state, Func<FutureHandle, TState, TResult>? selector, CancellationToken ct)
		{
			return new FdbFutureSingle<TState, TResult>(handle, state, selector, ct).Task;
		}

		/// <summary>Wraps multiple <see cref="FdbFuture{T}"/> handles into a single <see cref="Task{TResult}"/> that returns an array of T</summary>
		/// <typeparam name="TResult">Type of the result of the task</typeparam>
		/// <typeparam name="TState">Type of the state that will be passed to the result selector</typeparam>
		/// <param name="handles">Array of FDBFuture* pointers</param>
		/// <param name="state">State that is passed to the result selector</param>
		/// <param name="completionHandler">Lambda that will be called once for each future that completes successfully, to extract the decoded value for this future handle.</param>
		/// <param name="resultSelector">Lambda that will be called after all the results have been decoded, to extract the final result of the operation</param>
		/// <param name="ct">Optional cancellation token that can be used to cancel the future</param>
		/// <returns>Task that will either return the result of the operation, or an exception</returns>
		/// <remarks>If at least one future fails, the whole task will fail.</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Task<TResult> CreateTaskFromHandleArray<TState, TResult>(FutureHandle[] handles, TState state, Action<FutureHandle, int, TState> completionHandler, Func<TState, TResult> resultSelector, CancellationToken ct)
		{
			return new FdbFutureArray<TState, TResult>(handles, state, completionHandler, resultSelector, ct).Task;
		}

		/// <summary>Create a generic <see cref="FdbFuture{T}"/> that has a lifetime tied to a cancellation token</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="ct">Token used to cancel the future from the outside</param>
		/// <param name="options">Optional creation options for the underlying <see cref="Task{T}"/></param>
		/// <returns>Future that will automatically be cancelled if the linked token is cancelled.</returns>
		/// <remarks>This is mostly used to create Watches or futures that behave similarly to watches.</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbFuture<T> Create<T>(CancellationToken ct, TaskCreationOptions options = TaskCreationOptions.None)
		{
			return new FdbFutureTask<T>(ct, options);
		}

	}

	/// <summary>Base class for all FDBFuture wrappers</summary>
	/// <typeparam name="TResult">Type of the Task's result</typeparam>
	[DebuggerDisplay("Flags={m_flags}, State={this.Task.Status}")]
	public abstract class FdbFuture<TResult> : TaskCompletionSource<TResult>, IDisposable
	{

		#region Private Members...

		/// <summary>Flags of the future (bit field of FLAG_xxx values)</summary>
		private int m_flags;

		/// <summary>Future key in the callback dictionary</summary>
		protected IntPtr m_key;

		/// <summary>Optional registration on the parent Cancellation Token</summary>
		/// <remarks>Is only valid if FLAG_HAS_CTR is set</remarks>
		protected CancellationTokenRegistration m_ctr;

		#endregion

		protected FdbFuture() { }

		protected FdbFuture(TaskCreationOptions options) : base(options) { }

		#region State Management...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal bool HasFlag(int flag) => (Volatile.Read(ref m_flags) & flag) == flag;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal bool HasAnyFlags(int flags) => (Volatile.Read(ref m_flags) & flags) != 0;

		protected void SetFlag(int flag)
		{
			var flags = m_flags;
			Interlocked.MemoryBarrier();
			m_flags = flags | flag;
		}

		protected bool TrySetFlag(int flag)
		{
#if NET5_0_OR_GREATER
			return (Interlocked.Or(ref m_flags, flag) & flag) == 0;
#else
			// Interlocked.Or is not available: emulate it with a CompareExchange loop
			var spinner = new SpinWait();
			while (true)
			{
				int current = Volatile.Read(ref m_flags);
				if (Interlocked.CompareExchange(ref m_flags, current | flag, current) == current)
				{
					return (current & flag) == 0;
				}
				spinner.SpinOnce();
			}
#endif
		}

		protected bool TryCleanup()
		{
			// We try to clean up the future handle as soon as possible, meaning as soon as we have the result, or an error, or a cancellation

			if (TrySetFlag(FdbFuture.Flags.COMPLETED))
			{
				DoCleanup();
				return true;
			}
			return false;
		}

		private void DoCleanup()
		{
			try
			{
				// unsubscribe from the parent cancellation token if there was one
				UnregisterCancellationRegistration();

				// ensure that the task always complete !
				// note: always defer the completion on the threadpool, because we don't want to deadlock here (we can be called by Dispose)
				if (!this.Task.IsCompleted)
				{
					TrySetCanceled();
				}
				// The only surviving value after this would be a Task and an optional WorkItem on the ThreadPool that will signal it...
			}
			finally
			{
				CloseHandles();
			}
		}

		/// <summary>Release the memory allocated by this Future, if it supports it.</summary>
		/// <returns><see langword="true"/> if the memory was released, or <see langword="false"/> if the future does not support this action, if it has already been performed, or if the future has already been disposed.</returns>
		protected bool TryReleaseMemory()
		{
			if (TrySetFlag(FdbFuture.Flags.MEMORY_RELEASED))
			{
				ReleaseMemory();
				return true;
			}

			return false;
		}

		/// <summary>Close all the handles managed by this future</summary>
		protected abstract void CloseHandles();

		/// <summary>Cancel all the handles managed by this future</summary>
		protected abstract void CancelHandles();

		/// <summary>Release all memory allocated by this future</summary>
		protected abstract void ReleaseMemory();

		#endregion

		#region Callbacks...

		/// <summary>Map of all pending futures that have not yet completed</summary>
		/// <remarks>The key is the handle that is passed to <c>fdb_future_set_callback</c>,
		/// and used to retrieve the original future instance from inside the callback</remarks>
		private static readonly ConcurrentDictionary<long, FdbFuture<TResult>> s_futures = new();

		/// <summary>Register a future in the callback context and return the corresponding callback parameter</summary>
		/// <param name="future">Future instance</param>
		/// <returns>Parameter that can be passed to <see cref="FdbNative.FutureSetCallback"/> and that uniquely identify this future.</returns>
		/// <remarks>The caller MUST ensure that <see cref="UnregisterCallback"/> is called at least once, to ensure that the future instance gets removed from the map</remarks>
		internal static IntPtr RegisterCallback(FdbFuture<TResult> future)
		{
			Contract.Debug.Requires(future != null);

			// generate a new unique id for this future, that will be used to look up the future instance in the callback handler
			long id = Interlocked.Increment(ref FdbFuture.s_futureCounter);

			// note: we assume that we can only run in 64-bit mode, so it is safe to cast a long into an IntPtr
			var prm = new IntPtr(id);

			// critical region
			try { }
			finally
			{
				Volatile.Write(ref future.m_key, prm);
#if DEBUG_FUTURES
				Contract.Debug.Assert(!s_futures.ContainsKey(prm));
#endif
				s_futures[prm.ToInt64()] = future;
				Interlocked.Increment(ref DebugCounters.CallbackHandlesTotal);
				Interlocked.Increment(ref DebugCounters.CallbackHandles);
			}
			return prm;
		}

		/// <summary>Remove a future from the callback handler dictionary</summary>
		/// <param name="future">Future that has just completed, or is being destroyed</param>
		internal static void UnregisterCallback(FdbFuture<TResult> future)
		{
			Contract.Debug.Requires(future != null);

			// critical region
			try
			{ }
			finally
			{
				var key = Interlocked.Exchange(ref future.m_key, IntPtr.Zero);
				if (key != IntPtr.Zero)
				{
					// note: KeyValuePair.Create (instead of a target-typed `new`) so that, on netstandard2.0, the argument type can be inferred for the generic TryRemove(KeyValuePair<,>) compat extension
					if (s_futures.TryRemove(KeyValuePair.Create(key.ToInt64(), future)))
					{
						Interlocked.Decrement(ref DebugCounters.CallbackHandles);
					}
				}
			}
		}

		internal static FdbFuture<TResult>? GetFutureFromCallbackParameter(IntPtr parameter)
		{
			Contract.Debug.Requires(parameter != default);

			if (s_futures.TryGetValue(parameter.ToInt64(), out var future))
			{
				Contract.Debug.Assert(future != null);
				if (Volatile.Read(ref future.m_key) == parameter)
				{
					return future;
				}
#if DEBUG_FUTURES
				// If you breakpoint here, that means that a future callback fired but was not able to find a matching registration
				// => either the FdbFuture<T> was incorrectly disposed, or there is some problem in the callback dictionary
				if (System.Diagnostics.Debugger.IsAttached)  System.Diagnostics.Debugger.Break();
#endif
			}
			return null;
		}

		#endregion

		#region Cancellation...

		protected void RegisterForCancellation(CancellationToken ct)
		{
			//note: if the token is already cancelled, the callback handler will run inline and any exception would bubble up here
			//=> this is not a problem because the ctor already has a try/catch that will clean up everything
			m_ctr = ct.Register(
				CancellationHandler,
				this,
				false
			);
		}

		protected void UnregisterCancellationRegistration()
		{
			// unsubscribe from the parent cancellation token if there was one
			m_ctr.Dispose();
			m_ctr = default;
		}

		private static void CancellationHandler(object? state)
		{
			if (state is FdbFuture<TResult> future)
			{
#if DEBUG_FUTURES
				Debug.WriteLine("Future<" + typeof(T).Name + ">.Cancel(0x" + future.m_handle.Handle.ToString("x") + ") was called on thread #" + Environment.CurrentManagedThreadId.ToString());
#endif
				future.Cancel();
			}
		}

		#endregion

		/// <summary>Return true if the future has completed (successfully or not)</summary>
		public bool IsReady => this.Task.IsCompleted;

		/// <summary>Make the Future awaitable</summary>
		public TaskAwaiter<TResult> GetAwaiter()
		{
			return this.Task.GetAwaiter();
		}

		/// <summary>Try to abort the task (if it is still running)</summary>
		public void Cancel()
		{
			if (HasAnyFlags(FdbFuture.Flags.DISPOSED | FdbFuture.Flags.COMPLETED | FdbFuture.Flags.CANCELLED))
			{
				return;
			}

			if (TrySetFlag(FdbFuture.Flags.CANCELLED))
			{
				try
				{
					if (!this.Task.IsCompleted)
					{
						CancelHandles();
						TrySetCanceled();
					}
				}
				finally
				{
					TryCleanup();
				}
			}
		}

		/// <summary>Free memory allocated by this future after it has completed.</summary>
		/// <remarks>This method provides no benefit to most application code, and should only be called when attempting to write thread-safe custom layers.</remarks>
		public void Clear()
		{
			if (HasFlag(FdbFuture.Flags.DISPOSED))
			{
				return;
			}

			if (!this.Task.IsCompleted)
			{
				throw new InvalidOperationException("Cannot release memory allocated by a future that has not yet completed");
			}

			TryReleaseMemory();
		}

		/// <inheritdoc />
		public void Dispose()
		{
			if (TrySetFlag(FdbFuture.Flags.DISPOSED))
			{
				try
				{
					TryCleanup();
				}
				finally
				{
					if (Volatile.Read(ref m_key) != IntPtr.Zero) UnregisterCallback(this);
				}
			}
		}

	}

	/// <summary>Generic <see cref="FdbFuture{TResult}"/> that will behave like a <see cref="Task{TResult}"/></summary>
	/// <remarks>Can be used to replicate the behaviors of Watches or other async database operations</remarks>
	public sealed class FdbFutureTask<TResult> : FdbFuture<TResult>
	{

		public FdbFutureTask(CancellationToken ct, TaskCreationOptions options) : base(options)
		{
			if (ct.CanBeCanceled)
			{
				RegisterForCancellation(ct);
			}
		}

		/// <inheritdoc />
		protected override void CloseHandles()
		{
			// NOP
		}

		/// <inheritdoc />
		protected override void CancelHandles()
		{
			// NOP
		}

		/// <inheritdoc />
		protected override void ReleaseMemory()
		{
			// NOP
		}

	}

}
