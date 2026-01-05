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
	{
		// Wraps several FDBFuture* handles and return all the results at once

		#region Private Members...

		/// <summary>Value of the 'FDBFuture*'</summary>
		private readonly FutureHandle?[]? m_handles;

		/// <summary>Counter of callbacks that still need to fire.</summary>
		private int m_pending;

		/// <summary>Lambda used to extract the value from each FDBFuture</summary>
		private Action<FutureHandle, int, TState>? m_completionHandler;

		/// <summary>Lambda used to extract the result returned to the caller</summary>
		private Func<TState, TResult>? m_resultSelector;

		private TState? m_state;

		#endregion

		#region Constructors...

		internal FdbFutureArray(FutureHandle?[] handles, TState state, Action<FutureHandle, int, TState> completionHandler, Func<TState, TResult> resultSelector, CancellationToken ct)
		{
			Contract.Debug.Requires(handles != null);

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
					var err = FdbNative.FutureSetCallback(handle, CallbackHandler, prm);
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
					HandleCompletion();
					m_completionHandler = null;
					m_resultSelector = null;
					abortAllHandles = true;
					SetFlag(FdbFuture.Flags.COMPLETED);
				}
				else  if (ct.CanBeCanceled)
				{ // register for cancellation (if needed)
					RegisterForCancellation(ct);
				}
			}
			catch
			{
				// this is bad news, since we are in the constructor, we need to clear everything
				SetFlag(FdbFuture.Flags.DISPOSED);

				UnregisterCancellationRegistration();

				UnregisterCallback(this);

				abortAllHandles = true;

				Volatile.Write(ref m_completionHandler, null);
				Volatile.Write(ref m_resultSelector, null);
				m_state = default;

				// this is technically not needed, but just to be safe...
				TrySetCanceled();

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

					ThreadPool.UnsafeQueueUserWorkItem(static (f) => f.HandleCompletion(), future, true);
				}
				// else, the ctor will handle that
			}
		}

		/// <summary>Update the Task with the state of a ready Future</summary>
		/// <returns>True if we got a result, or false in case of error (or invalid state)</returns>
		private void HandleCompletion()
		{
			if (HasAnyFlags(FdbFuture.Flags.DISPOSED | FdbFuture.Flags.COMPLETED))
			{
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
