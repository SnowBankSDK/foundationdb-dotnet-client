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
	/// <summary>FDBFuture wrapper</summary>
	/// <typeparam name="TState">Type of the state passed to the callback</typeparam>
	/// <typeparam name="TResult">Type of result</typeparam>
	public sealed class FdbFutureSingle<TState, TResult> : FdbFuture<TResult>
	{
		#region Private Members...

		/// <summary>Value of the 'FDBFuture*'</summary>
		private readonly FutureHandle? m_handle;

		/// <summary>Lambda used to extract the result of this FDBFuture</summary>
		private Func<FutureHandle, TState, TResult>? m_resultSelector;

		private TState? m_state;

		#endregion

		#region Constructors...

		internal FdbFutureSingle(FutureHandle handle, TState state, Func<FutureHandle, TState, TResult>? selector, CancellationToken ct)
		{
			Contract.Debug.Requires(handle != null);

			if (handle.IsInvalid)
			{ // it's dead, Jim !
				SetFlag(FdbFuture.Flags.COMPLETED);
				m_resultSelector = null;
				return;
			}

			m_handle = handle;
			try
			{

				if (FdbNative.FutureIsReady(handle))
				{ // either got a value or an error
#if DEBUG_FUTURES
					Debug.WriteLine("Future<" + typeof(T).Name + "> 0x" + handle.Handle.ToString("x") + " was already ready");
#endif
					m_resultSelector = selector;
					m_state = state;
					HandleCompletion();
#if DEBUG_FUTURES
					Debug.WriteLine("Future<" + typeof(T).Name + "> 0x" + handle.Handle.ToString("x") + " completed inline");
#endif
					return;
				}

				// register for cancellation (if needed)
				if (ct.CanBeCanceled)
				{
					if (ct.IsCancellationRequested)
					{ // we have already been cancelled

#if DEBUG_FUTURES
						Debug.WriteLine("Future<" + typeof(T).Name + "> 0x" + handle.Handle.ToString("x") + " will complete later");
#endif

						// Abort the future and simulate a Canceled task
						SetFlag(FdbFuture.Flags.COMPLETED);
						// note: we don't need to call fdb_future_cancel because fdb_future_destroy will take care of everything
						handle.Dispose();
						// also, don't keep a reference on the callback because it won't be needed
						TrySetCanceled();
						return;
					}

					// token still active
					RegisterForCancellation(ct);
				}

#if DEBUG_FUTURES
				Debug.WriteLine("Future<" + typeof(T).Name + "> 0x" + handle.Handle.ToString("x") + " will complete later");
#endif

				TrySetFlag(FdbFuture.Flags.READY);

				m_state = state;
				Volatile.Write(ref m_resultSelector, selector);

				// add this instance to the list of pending futures
				var prm = RegisterCallback(this);

				// register the callback handler
				var err = FdbNative.FutureSetCallback(handle, CallbackHandler, prm);
				if (err != FdbError.Success)
				{ // uhoh
#if DEBUG_FUTURES
					Debug.WriteLine("Failed to set callback for Future<" + typeof(T).Name + "> 0x" + handle.Handle.ToString("x") + " !!!");
#endif
					throw FdbNative.CreateExceptionFromError(err);
				}
			}
			catch
			{
				// this is bad news, since we are in the constructor, we need to clear everything
				SetFlag(FdbFuture.Flags.DISPOSED);
				UnregisterCancellationRegistration();
				UnregisterCallback(this);

				// kill the future handle
				m_handle.Dispose();

				Volatile.Write(ref m_resultSelector, null);
				m_state = default;

				// this is technically not needed, but just to be safe...
				TrySetCanceled();

				throw;
			}
			GC.KeepAlive(this);
		}

		#endregion

		/// <summary>Cached delegate of the future completion callback handler</summary>
		// ReSharper disable once StaticMemberInGenericType
		private static readonly FdbNative.FdbFutureCallback CallbackHandler = FutureCompletionCallback;

		/// <summary>Handler called when a FDBFuture becomes ready</summary>
		/// <param name="futureHandle">Handle on the future that became ready</param>
		/// <param name="parameter">Parameter to the callback</param>
		private static void FutureCompletionCallback(IntPtr futureHandle, IntPtr parameter)
		{
#if DEBUG_FUTURES
			Debug.WriteLine("Future<" + typeof(T).Name + ">.Callback(0x" + futureHandle.ToString("x") + ", " + parameter.ToString("x") + ") has fired on thread #" + Environment.CurrentManagedThreadId.ToString());
#endif

			var future = GetFutureFromCallbackParameter(parameter);
			if (future != null)
			{
				UnregisterCallback(future);

				ThreadPool.UnsafeQueueUserWorkItem(s_callback, future);
			}
		}

		private static readonly WaitCallback s_callback = f => ((FdbFutureSingle<TState, TResult>) f!).HandleCompletion();

		/// <summary>Update the Task with the state of a ready Future</summary>
		/// <returns>True if we got a result, or false in case of error (or invalid state)</returns>
		private void HandleCompletion()
		{
			if (HasAnyFlags(FdbFuture.Flags.DISPOSED | FdbFuture.Flags.COMPLETED))
			{
				return;
			}

#if DEBUG_FUTURES
			var sw = Stopwatch.StartNew();
#endif

			var selector = m_resultSelector;
			var state = m_state;
			m_resultSelector = null;
			m_state = default;

			try
			{
				var handle = m_handle;
				if (handle != null && !handle.IsClosed && !handle.IsInvalid)
				{
					UnregisterCancellationRegistration();

					FdbError err = FdbNative.FutureGetError(handle);
					if (err != FdbError.Success)
					{ // it failed...
#if DEBUG_FUTURES
						Debug.WriteLine("Future<" + typeof(T).Name + "> has FAILED: " + err);
#endif
						if (err != FdbError.OperationCancelled)
						{ // map this error code into a .NET Exception

							// Issue with TaskCompletionSource<T>.TrySetException an "un-thrown" exceptions:
							// - We have "lost" the original stacktrace of the operation that failed, and since this exception is not thrown
							//   it will appear to originate from the first "await" in the call hierarchy, usually the app code itself.
							// - We could call ExceptionDispatchInfo.SetCurrentStackTrace(), but the resulting stacktrace is not useful, since it
							//   will start from inside fdb_run_network(), then this completion callback.
							// - There does not seem to be an efficient way to "capture" the stack when the Future was created,
							//   the cost of creating a System.Diagnostics.StackTrace instance is WAY TOO BIG compared to the benefits
							
							// => there is not much we can do here!

							var ex = FdbNative.CreateExceptionFromError(err);
							TrySetException(ex);
							return;
						}
						//else: will be handle below
					}
					else
					{ // it succeeded...
						// try to get the result...
#if DEBUG_FUTURES
						Debug.WriteLine("Future<" + typeof(T).Name + "> has completed successfully");
#endif
						if (selector != null)
						{
							//note: result selector will execute from network thread, but this should be our own code that only calls into some fdb_future_get_XXXX(), which should be safe...
							var result = selector(handle, state!);

							TrySetResult(result);
							return;
						}
						else
						{
							TrySetResult(default(TResult)!);
							return;
						}
						//else: it will be handled below
					}
				}

				// most probably the future was cancelled, or we are shutting down...
				TrySetCanceled();
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
#if DEBUG_FUTURES
				sw.Stop();
				Debug.WriteLine("Future<" + typeof(T).Name + "> callback completed in " + sw.Elapsed.TotalMilliseconds.ToString() + " ms");
#endif
				TryCleanup();
			}
		}

		protected override void CloseHandles()
		{
			m_handle?.Dispose();
		}

		protected override void CancelHandles()
		{
			var handle = m_handle;
			//REVIEW: there is a possibility of a race condition with Dispose() that could potentially call FutureDestroy(handle) at the same time (not verified)
			if (handle != null && !handle.IsClosed && !handle.IsInvalid)
			{
				FdbNative.FutureCancel(handle);
			}
		}

		protected override void ReleaseMemory()
		{
			var handle = m_handle;

			//REVIEW: there is a possibility of a race condition with Dispose() that could potentially call FutureDestroy(handle) at the same time (not verified)
			if (handle != null && !handle.IsClosed && !handle.IsInvalid)
			{
				FdbNative.FutureReleaseMemory(handle);
			}
		}

	}

}
