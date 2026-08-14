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

//#define DEBUG_FUTURES

namespace FoundationDB.Client.Native
{
	/// <summary>FDBFuture[] wrapper</summary>
	/// <typeparam name="TState">Type of the state passed to the callback</typeparam>
	/// <typeparam name="TResult">Type of the final result returned once all sub-tasks have completed</typeparam>
	public sealed class FdbFutureArray<TState, TResult> : FdbFuture<TResult>
#if NET8_0_OR_GREATER
		, IFdbFuture
#endif
	{
		// Wraps several FDBFuture* handles and return all the results at once

		#region Private Members...

		/// <summary>Value of the 'FDBFuture*'</summary>
		/// <remarks>Mutable because pooled instances are re-initialized with a new batch of handles on each lifecycle</remarks>
		private FutureHandle?[]? m_handles;

		/// <summary>Counter of callbacks that still need to fire.</summary>
		private int m_pending;

		/// <summary>Lambda used to extract the value from each FDBFuture</summary>
		private Action<FutureHandle, int, TState>? m_completionHandler;

		/// <summary>Lambda used to extract the result returned to the caller</summary>
		private Func<TState, TResult>? m_resultSelector;

		private TState? m_state;

		#endregion

		#region Constructors...

		private FdbFutureArray() { }

		internal FdbFutureArray(FutureHandle?[] handles, TState state, Action<FutureHandle, int, TState> completionHandler, Func<TState, TResult> resultSelector, CancellationToken ct)
		{
			Initialize(handles, state, completionHandler, resultSelector, ct);
		}

		/// <summary>Wraps the handles and returns the combined <see cref="Task{TResult}"/>, keeping the wrapper itself poolable</summary>
		/// <remarks>Same hatch discipline as <see cref="FdbFutureSingle{TState,TResult}.CreateTask"/>: the Task is
		/// materialized BEFORE any callback is armed, and the caller never sees the wrapper object.</remarks>
		internal static Task<TResult> CreateTask(FutureHandle?[] handles, TState state, Action<FutureHandle, int, TState> completionHandler, Func<TState, TResult> resultSelector, CancellationToken ct)
		{
#if NET8_0_OR_GREATER
			var future = FdbFuturePool<FdbFutureArray<TState, TResult>>.TryRent() ?? new FdbFutureArray<TState, TResult>();
			var task = future.MaterializeTask();
			future.Initialize(handles, state, completionHandler, resultSelector, ct);
			return task;
#else
			return new FdbFutureArray<TState, TResult>(handles!, state, completionHandler, resultSelector, ct).Task;
#endif
		}

		private void Initialize(FutureHandle?[] handles, TState state, Action<FutureHandle, int, TState> completionHandler, Func<TState, TResult> resultSelector, CancellationToken ct)
		{
			Contract.Debug.Requires(handles != null);
			Contract.Debug.Requires(m_key == IntPtr.Zero, "rented instance still has an armed cookie");
			Contract.Debug.Requires(Volatile.Read(ref m_pending) == 0, "rented instance still has armed handles");
			Contract.Debug.Requires(!this.IsSettled, "rented instance is already settled");

			m_handles = handles;

			bool abortAllHandles = false;

			try
			{
				if (ct.IsCancellationRequested)
				{ // already cancelled, we must abort everything

					SetFlag(FdbFuture.Flags.COMPLETED);
					abortAllHandles = true;
					TrySetCanceled();
					return;
				}

				m_state = state;
				Volatile.Write(ref m_completionHandler, completionHandler);
				Volatile.Write(ref m_resultSelector, resultSelector);

				if (ct.CanBeCanceled)
				{ // register for cancellation before arming the callbacks (same order as FdbFutureSingle): a
				  // registration created after arming can lose the race against its own completion, and a
				  // registration that survives its lifecycle fires into whatever operation occupies this
				  // (recycled) instance next
					RegisterForCancellation(ct);
				}

				// add this instance to the list of pending futures
				var prm = RegisterCallback(this);

				foreach (var handle in handles)
				{
					if (handle is null || FdbNative.FutureIsReady(handle))
					{ // this handle is already done
						continue;
					}

					Interlocked.Increment(ref m_pending);

					// register the callback handler
#if NET8_0_OR_GREATER
					var err = FdbNative.FutureSetCallback(handle, FdbFuture.CallbackEntryPoint, prm);
#else
					var err = FdbNative.FutureSetCallback(handle, CallbackHandler, prm);
#endif
					if (err != FdbError.Success)
					{ // uhoh
#if DEBUG_FUTURES
						System.Diagnostics.Debug.WriteLine("Failed to set callback for Future<" + typeof(T).Name + "> 0x" + handle.Handle.ToString("x") + " !!!");
#endif
						throw FdbNative.CreateExceptionFromError(err);
					}
				}

				// allow the callbacks to handle completion
				TrySetFlag(FdbFuture.Flags.READY);

				if (Volatile.Read(ref m_pending) == 0)
				{ // all callbacks have already fired (or all handles were already completed)
					UnregisterCallback(this);
#if NET8_0_OR_GREATER
					// inline completion: HandleCompletion below can release BOTH reuse gates synchronously, and this
					// method still writes to the instance afterwards, so this (rare) lifecycle retires to the GC
					// instead of getting recycled under our feet
					SetFlag(FdbFuture.Flags.UNPOOLED);
#endif
					HandleCompletion();
					m_completionHandler = null;
					m_resultSelector = null;
					abortAllHandles = true;
					SetFlag(FdbFuture.Flags.COMPLETED);
				}
			}
			catch
			{
				// this is bad news, since we are in the constructor, we need to clear everything
				SetFlag(FdbFuture.Flags.DISPOSED);

				UnregisterCancellationRegistration();

				Volatile.Write(ref m_completionHandler, null);
				Volatile.Write(ref m_resultSelector, null);
				m_state = default;

				// this is technically not needed, but just to be safe...
				TrySetCanceled();

#if NET8_0_OR_GREATER
				if (Volatile.Read(ref m_pending) > 0)
				{ // some handles are already armed with the cookie: cancel them so that their fires drain;
				  // the LAST fire releases the cookie and destroys the handles (freeing here would race the
				  // network thread resolving the cookie)
					CancelHandles(handles);
					throw;
				}
#endif

				UnregisterCallback(this);

				abortAllHandles = true;

				throw;
			}
			finally
			{
				if (abortAllHandles)
				{
					CloseHandles(handles);
				}
			}
			GC.KeepAlive(this);
		}

		#endregion

#if NET8_0_OR_GREATER

		/// <summary>Scrubs and rearms this instance, then offers it back to the pool (see <see cref="FdbFutureSingle{TState,TResult}.OnReadyForReuse"/>)</summary>
		protected override void OnReadyForReuse()
		{
			if (HasFlag(FdbFuture.Flags.UNPOOLED))
			{ // the object identity escaped (or the lifecycle completed inline): retire to the GC
				return;
			}
			m_handles = null;
			m_completionHandler = null;
			m_resultSelector = null;
			m_state = default;
			ResetForReuse();
			FdbFuturePool<FdbFutureArray<TState, TResult>>.TryReturn(this);
		}

#endif

		protected override void CloseHandles()
		{
			CloseHandles(m_handles);
		}

		protected override void CancelHandles()
		{
			CancelHandles(m_handles);
		}

		protected override void ReleaseMemory()
		{
			var handles = m_handles;
			if (handles != null)
			{
				foreach (var handle in handles)
				{
					if (handle != null && !handle.IsClosed && !handle.IsInvalid)
					{
						//REVIEW: there is a possibility of a race condition with Dispose() that could potentially call FutureDestroy(handle) at the same time (not verified)
						FdbNative.FutureReleaseMemory(handle);
					}
				}
			}
		}

		private static void CloseHandles(FutureHandle?[]? handles)
		{
			if (handles != null)
			{
				foreach (var handle in handles)
				{
					//note: Dispose() will be a no-op if already called
					handle?.Dispose();
				}
			}
		}

		private static void CancelHandles(FutureHandle?[]? handles)
		{
			if (handles != null)
			{
				foreach (var handle in handles)
				{
					if (handle != null && !handle.IsClosed && !handle.IsInvalid)
					{
						//REVIEW: there is a possibility of a race condition with Dispose() that could potentially call FutureDestroy(handle) at the same time (not verified)
						FdbNative.FutureCancel(handle);
					}
				}
			}
		}

#if NET8_0_OR_GREATER

		/// <summary>Invoked (via the shared native callback) each time one of the watched futures becomes ready</summary>
		void IFdbFuture.OnFutureFired()
		{
			if (Interlocked.Decrement(ref m_pending) == 0)
			{ // the last armed handle has fired

				if (HasFlag(FdbFuture.Flags.READY))
				{ // we can proceed to read all the results
					// FIRED must be visible before the cookie is released (see FdbFutureSingle.OnFutureFired)
					TrySetFlag(FdbFuture.Flags.FIRED);

					UnregisterCallback(this);

					ThreadPool.UnsafeQueueUserWorkItem(static (f) => f.HandleCompletion(), this, true);
				}
				else if (HasFlag(FdbFuture.Flags.DISPOSED))
				{ // the ctor failed after arming this subset: the last fire releases the cookie and the handles
					UnregisterCallback(this);

					TryCleanup();
				}
				// else: the ctor is still arming the other handles, and will observe m_pending == 0 itself
			}
		}

#else

		/// <summary>Cached delegate of the future completion callback handler</summary>
		// ReSharper disable once StaticMemberInGenericType
		private static readonly FdbNative.FdbFutureCallback CallbackHandler = FutureCompletionCallback;

		/// <summary>Handler called when a FDBFuture becomes ready</summary>
		/// <param name="futureHandle">Handle on the future that became ready</param>
		/// <param name="parameter">Parameter to the callback (unused)</param>
		private static void FutureCompletionCallback(IntPtr futureHandle, IntPtr parameter)
		{
#if DEBUG_FUTURES
			System.Diagnostics.Debug.WriteLine("Future<" + typeof(T).Name + ">.Callback(0x" + futureHandle.ToString("x") + ", " + parameter.ToString("x") + ") has fired on thread #" + Environment.CurrentManagedThreadId.ToString());
#endif

			var future = (FdbFutureArray<TState, TResult>?) GetFutureFromCallbackParameter(parameter);

			if (future != null && Interlocked.Decrement(ref future.m_pending) == 0)
			{ // the last future handle has fired, we can proceed to read all the results

				if (future.HasFlag(FdbFuture.Flags.READY))
				{
					UnregisterCallback(future);

					// the generic overload with 'preferLocal' is not available, using the legacy WaitCallback overload instead (which cannot favor the local thread's queue)
					ThreadPool.UnsafeQueueUserWorkItem(static (f) => ((FdbFutureArray<TState, TResult>) f!).HandleCompletion(), future);
				}
				// else, the ctor will handle that
			}
		}

#endif

		/// <summary>Update the Task with the state of a ready Future</summary>
		/// <returns>True if we got a result, or false in case of error (or invalid state)</returns>
		private void HandleCompletion()
		{
			if (HasAnyFlags(FdbFuture.Flags.DISPOSED | FdbFuture.Flags.COMPLETED))
			{
				// a disposed-in-flight future hands its cleanup to this (queued) completion; TryCleanup is a no-op
				// when the cleanup already ran
				TryCleanup();
				return;
			}

#if DEBUG_FUTURES
			System.Diagnostics.Debug.WriteLine("FutureArray<" + typeof(T).Name + ">.Callback(...) handling completion on thread #" + Environment.CurrentManagedThreadId.ToString());
#endif

			try
			{
				UnregisterCancellationRegistration();

				FdbError errGlobal = FdbError.Success;
				var completionHandler = Volatile.Read(ref m_completionHandler);
				var resultSelector = Volatile.Read(ref m_resultSelector);
				var state = m_state;
				m_completionHandler = null;
				m_resultSelector = null;
				m_state = default;
				var handles = m_handles;
				Contract.Debug.Assert(handles != null);

				for (int i = 0; i < handles.Length; i++)
				{
					var handle = handles[i];

					if (handle != null && !handle.IsClosed && !handle.IsInvalid)
					{
						FdbError err = FdbNative.FutureGetError(handle);
						if (err != FdbError.Success)
						{ // it failed...
							if (err != FdbError.OperationCancelled)
							{ // get the exception from the error code

								//REVIEW: should we only return the first error? or rank them to return the "worst" ?
								// => for now, only report the first error
								if (errGlobal == FdbError.Success)
								{
									errGlobal = err;
								}
							}
							else
							{
								errGlobal = FdbError.OperationCancelled;
								break;
							}
						}
						else
						{ // it succeeded...
							// try to get the result...
							if (completionHandler != null)
							{
								//note: result selector will execute from network thread, but this should be our own code that only calls into some fdb_future_get_XXXX(), which should be safe...
								completionHandler(handle, i, state!);
							}
						}
					}
				}

				if (errGlobal == FdbError.OperationCancelled)
				{ // the transaction has been cancelled
					TrySetCanceled();
				}
				else if (errGlobal != FdbError.Success)
				{ // there was at least one error
					var ex = FdbNative.CreateExceptionFromError(errGlobal);
					// See the note in FdbFutureSingle<T> about the "lost" callstack of "un-thrown" exceptions
					TrySetException(ex);
				}
				else
				{ // success
					try
					{
						// compute the final result
						var result = resultSelector is not null ? resultSelector(state!) : default;

						TrySetResult(result!);
					}
					catch (Exception ex)
					{
						TrySetException(ex);
					}
				}

			}
			catch (Exception e)
			{ // something went wrong
				if (e is ThreadAbortException)
				{
					TrySetCanceled();
					throw;
				}
				TrySetException(e);
			}
			finally
			{
				TryCleanup();
			}
		}

	}

}
