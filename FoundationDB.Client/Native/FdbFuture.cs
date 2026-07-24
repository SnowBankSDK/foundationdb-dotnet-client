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
	using System.Threading.Tasks.Sources;
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

			/// <summary>This instance must never return to the pool: its object identity has escaped to a holder
			/// (a watch keeping the wrapper, a materialized Task hatch) that may observe it after completion.</summary>
			public const int UNPOOLED = 256;

			/// <summary>The native callback has fired and the completion has been (or is about to be) queued: from
			/// this point the queued completion owns the cleanup, and Dispose/Cancel must hand off instead of
			/// cleaning up themselves (a cleanup racing the queued item could recycle the instance under it).</summary>
			public const int FIRED = 512;
		}

#if NET8_0_OR_GREATER

		/// <summary>Entry point invoked by the fdb_c network thread when a future becomes ready</summary>
		/// <param name="futureHandle">Handle of the native future that became ready</param>
		/// <param name="parameter">Cookie passed to <c>fdb_future_set_callback</c>: a <see cref="GCHandle"/> on the managed wrapper</param>
		/// <remarks>The cookie is freed by the wrapper itself (the fire path is its sole owner), never by a concurrent
		/// <c>Dispose()</c>: a handle freed while this method is between the native invocation and the
		/// <see cref="GCHandle.Target"/> read could be recycled and resolve to a DIFFERENT live future.</remarks>
		[UnmanagedCallersOnly(CallConvs = [ typeof(CallConvCdecl) ])]
		private static void FutureReadyCallback(IntPtr futureHandle, IntPtr parameter)
		{
			if (GCHandle.FromIntPtr(parameter).Target is IFdbFuture future)
			{
				future.OnFutureFired();
			}
		}

		/// <summary>Pointer to the single completion callback shared by all futures, for <see cref="FdbNative.FutureSetCallback"/></summary>
		internal static readonly IntPtr CallbackEntryPoint = GetCallbackEntryPoint();

		private static unsafe IntPtr GetCallbackEntryPoint() => (IntPtr) (delegate* unmanaged[Cdecl]<IntPtr, IntPtr, void>) &FutureReadyCallback;

#else

		/// <summary>Internal counter to generate a unique parameter value for each futures</summary>
		internal static long s_futureCounter;

#endif

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
			var future = FdbFutureSingle<TState, TResult>.Create(handle, state, selector, ct);
#if NET8_0_OR_GREATER
			// the caller keeps the wrapper object itself (e.g. a watch), and may observe it long after completion
			future.MarkUnpooled();
#endif
			return future;
		}

		/// <summary>Wraps a FDBFuture* pointer into a <see cref="Task"/></summary>
		/// <param name="handle">FDBFuture* pointer</param>
		/// <param name="ct">Optional cancellation token that can be used to cancel the future</param>
		/// <returns>Object that tracks the execution of the FDBFuture handle</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Task CreateTaskFromHandle(FutureHandle handle, CancellationToken ct)
		{
			return FdbFutureSingle<object?, object?>.CreateTask(handle, null, null, ct);
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
			return FdbFutureSingle<TState, TResult>.CreateTask(handle, state, selector, ct);
		}

		/// <summary>Wraps a FDBFuture* pointer into a <see cref="ValueTask{T}"/> that MUST be consumed exactly once</summary>
		/// <typeparam name="TResult">Type of the result of the task</typeparam>
		/// <typeparam name="TState">Type of the state that will be passed to the result selector</typeparam>
		/// <param name="handle">FDBFuture* pointer</param>
		/// <param name="state">State that is passed to the result selector</param>
		/// <param name="selector">Lambda that will be called once the future completes successfully, to extract the result from the future handle.</param>
		/// <param name="ct">Optional cancellation token that can be used to cancel the future</param>
		/// <remarks>On the netstandard2.0 target this wraps the underlying Task (same allocations as before); on modern targets no Task is allocated at all.</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ValueTask<TResult> CreateValueTaskFromHandle<TState, TResult>(FutureHandle handle, TState state, Func<FutureHandle, TState, TResult>? selector, CancellationToken ct)
		{
			return FdbFutureSingle<TState, TResult>.Create(handle, state, selector, ct).AsValueTask();
		}

		/// <summary>Wraps multiple FDBFuture* pointers into a single <see cref="ValueTask{TResult}"/> that MUST be consumed exactly once</summary>
		/// <remarks>If at least one future fails, the whole task will fail. On the netstandard2.0 target this wraps the underlying Task.</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ValueTask<TResult> CreateValueTaskFromHandleArray<TState, TResult>(FutureHandle[] handles, TState state, Action<FutureHandle, int, TState> completionHandler, Func<TState, TResult> resultSelector, CancellationToken ct)
		{
			return new FdbFutureArray<TState, TResult>(handles, state, completionHandler, resultSelector, ct).AsValueTask();
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
			return FdbFutureArray<TState, TResult>.CreateTask(handles, state, completionHandler, resultSelector, ct);
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

#if NET8_0_OR_GREATER

	/// <summary>Non-generic seam through which the single shared native callback reaches a future wrapper</summary>
	internal interface IFdbFuture
	{
		/// <summary>Invoked on the fdb network thread when one of the native futures watched by this wrapper has become ready</summary>
		void OnFutureFired();
	}

	/// <summary>Small fixed-slot pool of reusable future wrappers (one pool per closed generic type)</summary>
	/// <remarks>Slot-scan design (Interlocked exchange per slot): immune to the ABA hazard a CAS-linked free-list
	/// would have with recycled nodes. A full pool drops the instance to the GC; an empty pool makes the renter
	/// allocate - both degrade to the non-pooled behavior, never block.</remarks>
	internal static class FdbFuturePool<TFuture> where TFuture : class
	{

		// ponytail: fixed 32 slots per closed generic; make it CPU-scaled if the storm ever shows misses
		private static readonly TFuture?[] Slots = new TFuture?[32];

		internal static TFuture? TryRent()
		{
			var slots = Slots;
			for (int i = 0; i < slots.Length; i++)
			{
				var candidate = slots[i];
				if (candidate is not null && Interlocked.CompareExchange(ref slots[i], null, candidate) == candidate)
				{
					return candidate;
				}
			}
			return null;
		}

		internal static void TryReturn(TFuture instance)
		{
			var slots = Slots;
			for (int i = 0; i < slots.Length; i++)
			{
				if (slots[i] is null && Interlocked.CompareExchange(ref slots[i], instance, null) is null)
				{
					return;
				}
			}
			// pool full: the instance falls to the GC
		}

	}

#endif

	/// <summary>Base class for all FDBFuture wrappers</summary>
	/// <typeparam name="TResult">Type of the Task's result</typeparam>
#if NET8_0_OR_GREATER
	[DebuggerDisplay("Flags={m_flags}")]
	public abstract class FdbFuture<TResult> : IValueTaskSource<TResult>, IDisposable
#else
	[DebuggerDisplay("Flags={m_flags}, State={this.Task.Status}")]
	public abstract class FdbFuture<TResult> : TaskCompletionSource<TResult>, IDisposable
#endif
	{

		#region Private Members...

		/// <summary>Flags of the future (bit field of FLAG_xxx values)</summary>
		private int m_flags;

		/// <summary>Future key in the callback dictionary</summary>
		protected IntPtr m_key;

		/// <summary>Optional registration on the parent Cancellation Token</summary>
		/// <remarks>Is only valid if FLAG_HAS_CTR is set</remarks>
		protected CancellationTokenRegistration m_ctr;

#if NET8_0_OR_GREATER

		/// <summary>Completion source of this future (replaces the TaskCompletionSource base class: no Task allocated unless a consumer explicitly asks for one)</summary>
		/// <remarks>Mutable struct: must not be exposed as a readonly member</remarks>
		private ManualResetValueTaskSourceCore<TResult> m_core;

		/// <summary>0 while pending; 1 once a completer has won the publication race (result, error or cancellation)</summary>
		private int m_resultState;

		/// <summary>Lazily materialized Task, for the few consumers that need multi-consumption semantics (watches, the cached read version)</summary>
		private Task<TResult>? m_task;

		/// <summary>Release phases still pending before this instance may be reused: the consumer reading the result
		/// (<see cref="IValueTaskSource{TResult}.GetResult"/>) and the cleanup path closing the native resources
		/// (<see cref="DoCleanup"/>). Only when BOTH have happened does <see cref="OnReadyForReuse"/> fire; an
		/// abandoned, never-consumed future simply stays at 1 and falls to the GC like a non-pooled instance.</summary>
		private int m_releasesPending;

		/// <summary>Guards the single-consumption contract within one lifecycle: <see cref="ManualResetValueTaskSourceCore{T}.GetResult"/>
		/// does NOT invalidate its token, so without this a double await of the same ValueTask would silently
		/// double-release the reuse gate and corrupt the pool.</summary>
		private int m_consumed;


#endif

		#endregion

#if NET8_0_OR_GREATER

		protected FdbFuture()
		{
			// mirror the TaskCompletionSource default: continuations run synchronously on the completing thread
			// (which for the fire path is always a thread-pool work item, never the fdb network thread)
			m_core.RunContinuationsAsynchronously = false;
			m_releasesPending = 2;
		}

		protected FdbFuture(TaskCreationOptions options) : this()
		{
			if ((options & TaskCreationOptions.RunContinuationsAsynchronously) != 0)
			{
				m_core.RunContinuationsAsynchronously = true;
			}
		}

		#region Completion (mirrors the TaskCompletionSource surface, so that the wrapper bodies compile against both shapes)...

		public bool TrySetResult(TResult result)
		{
			if (Interlocked.CompareExchange(ref m_resultState, 1, 0) == 0)
			{
				m_core.SetResult(result);
				return true;
			}
			return false;
		}

		public bool TrySetException(Exception error)
		{
			if (Interlocked.CompareExchange(ref m_resultState, 1, 0) == 0)
			{
				m_core.SetException(error);
				return true;
			}
			return false;
		}

		public bool TrySetCanceled()
		{
			if (Interlocked.CompareExchange(ref m_resultState, 1, 0) == 0)
			{
				// a TaskCanceledException (not a plain OperationCanceledException) so that awaiters observe the same
				// exception type as with TaskCompletionSource.TrySetCanceled; ValueTask.AsTask() also recognizes it
				// (it is an OCE subclass) and surfaces a Canceled task to Task consumers
				m_core.SetException(new TaskCanceledException());
				return true;
			}
			return false;
		}

		/// <summary>Exposes this future as a <see cref="ValueTask{TResult}"/> that MUST be consumed exactly once</summary>
		public ValueTask<TResult> AsValueTask() => new(this, m_core.Version);

		/// <summary>Real <see cref="Task{TResult}"/> over this future, for consumers that need multi-consumption semantics (watches, the cached read version)</summary>
		/// <remarks>Materialized on first access, and this consumes the underlying value-task: a future whose <see cref="AsValueTask"/> has already been handed out must never also materialize a Task. Accessing this also pins the instance out of the pool: a holder that re-reads it after completion must keep observing THIS lifecycle.</remarks>
		public Task<TResult> Task
		{
			get
			{
				SetFlag(FdbFuture.Flags.UNPOOLED);
				return m_task ??= AsValueTask().AsTask();
			}
		}

		/// <summary>Permanently excludes this instance from wrapper pooling (required when the wrapper object itself is handed to a holder that may observe it after completion, like a watch)</summary>
		internal void MarkUnpooled() => SetFlag(FdbFuture.Flags.UNPOOLED);

		/// <summary>Materializes the Task hatch on a freshly armed-to-be instance, BEFORE the native callback can fire</summary>
		/// <remarks>The wrapper Task registers as the (sole) consumer of the value-task core: when the future completes,
		/// the wrapper consumes the result exactly once and never touches this instance again, so the instance stays
		/// POOLABLE - the caller keeps only the returned Task, never the wrapper object. Must be called before
		/// <c>Initialize</c>: materializing after arming would race an inline completion, whose consumer release could
		/// recycle the instance while the getter still holds it (and a stale cache write would then poison the next
		/// lifecycle).</remarks>
		internal Task<TResult> MaterializeTask()
		{
			Contract.Debug.Requires(m_task is null && Volatile.Read(ref m_resultState) == 0, "hatch must be materialized on a fresh lifecycle, before the callback is armed");
			return m_task = AsValueTask().AsTask();
		}

		TResult IValueTaskSource<TResult>.GetResult(short token)
		{
			if (token != m_core.Version)
			{ // stale token (a ValueTask consumed after the instance was reused): must throw WITHOUT touching the
			  // release gate of the CURRENT lifecycle
				return m_core.GetResult(token);
			}
			if (Interlocked.Exchange(ref m_consumed, 1) != 0)
			{ // the core does not enforce this itself, and a silent second consumption would double-release the gate
				throw new InvalidOperationException("The ValueTask of an FDB future was consumed more than once.");
			}
			try
			{
				return m_core.GetResult(token);
			}
			finally
			{
				ReleaseOnce();
			}
		}

		ValueTaskSourceStatus IValueTaskSource<TResult>.GetStatus(short token) => m_core.GetStatus(token);

		void IValueTaskSource<TResult>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags) => m_core.OnCompleted(continuation, state, token, flags);

		private void ReleaseOnce()
		{
			if (Interlocked.Decrement(ref m_releasesPending) == 0)
			{
				OnReadyForReuse();
			}
		}

		/// <summary>Invoked exactly once, when the result has been consumed AND the cleanup has run: a pooled
		/// implementation returns the instance to its pool here (after <see cref="ResetForReuse"/>). Default: no-op,
		/// the instance falls to the GC.</summary>
		protected virtual void OnReadyForReuse()
		{ }

		/// <summary>Rearms this instance for a new lifecycle (only legal from <see cref="OnReadyForReuse"/>, or on an
		/// instance provably out of circulation): resets the completion core (invalidating the previous version token),
		/// the flags and the release gate.</summary>
		protected void ResetForReuse()
		{
			Contract.Debug.Requires(Volatile.Read(ref m_key) == IntPtr.Zero && Volatile.Read(ref m_releasesPending) == 0);
			Contract.Debug.Requires(m_ctr.Equals(default(CancellationTokenRegistration)), "cancellation registration still armed on a recycling instance");

			m_flags = 0;
			m_task = null;
			m_resultState = 0;
			m_consumed = 0;
			m_core.Reset();
			Volatile.Write(ref m_releasesPending, 2);
		}

		#endregion

#else

		protected FdbFuture() { }

		protected FdbFuture(TaskCreationOptions options) : base(options) { }

		/// <summary>Exposes this future as a <see cref="ValueTask{TResult}"/> (wraps the underlying Task on this target)</summary>
		public ValueTask<TResult> AsValueTask() => new(this.Task);

#endif

		/// <summary>The future has been settled (result, error or cancellation published, or publication imminent on another thread)</summary>
		internal bool IsSettled
#if NET8_0_OR_GREATER
			=> Volatile.Read(ref m_resultState) != 0;
#else
			=> this.Task.IsCompleted;
#endif

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
				if (!this.IsSettled)
				{
					TrySetCanceled();
				}
				// The only surviving value after this would be a Task and an optional WorkItem on the ThreadPool that will signal it...
			}
			finally
			{
				CloseHandles();
#if NET8_0_OR_GREATER
				ReleaseOnce();
#endif
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

#if NET8_0_OR_GREATER

		/// <summary>Register a future with the callback mechanism and return the corresponding callback parameter</summary>
		/// <param name="future">Future instance</param>
		/// <returns>Parameter that can be passed to <see cref="FdbNative.FutureSetCallback"/> and that uniquely identify this future: a normal <see cref="GCHandle"/> on the instance.</returns>
		/// <remarks>Once at least one native callback is armed with this cookie, the FIRE PATH is the sole owner of the
		/// handle: only <see cref="UnregisterCallback"/> calls reached from <see cref="IFdbFuture.OnFutureFired"/> (or
		/// from a code path that has proven no callback can fire anymore) may run. Freeing the handle while the network
		/// thread is between the native invocation and the <see cref="GCHandle.Target"/> read is undefined behavior.</remarks>
		internal static IntPtr RegisterCallback(FdbFuture<TResult> future)
		{
			Contract.Debug.Requires(future != null);

			var prm = GCHandle.ToIntPtr(GCHandle.Alloc(future));

			// critical region
			try { }
			finally
			{
				Volatile.Write(ref future.m_key, prm);
				Interlocked.Increment(ref DebugCounters.CallbackHandlesTotal);
				Interlocked.Increment(ref DebugCounters.CallbackHandles);
			}
			return prm;
		}

		/// <summary>Release the callback cookie of a future</summary>
		/// <param name="future">Future that has just fired, or that is known to never fire</param>
		/// <remarks>Idempotent: the first caller wins the exchange and frees the handle.</remarks>
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
					GCHandle.FromIntPtr(key).Free();
					Interlocked.Decrement(ref DebugCounters.CallbackHandles);
				}
			}
		}

#else

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

#endif

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
		public bool IsReady => this.IsSettled;

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
					if (!this.IsSettled)
					{
						CancelHandles();
						TrySetCanceled();
					}
				}
				finally
				{
#if NET8_0_OR_GREATER
					// an armed callback still holds the cookie: fdb_future_cancel above forces the future ready, so the
					// fire is guaranteed and the fire path performs the cleanup (freeing the cookie and destroying the
					// native handle here would race the network thread resolving the cookie); likewise a fire that has
					// already queued its completion owns the cleanup through that work item
					if (Volatile.Read(ref m_key) == IntPtr.Zero && !HasFlag(FdbFuture.Flags.FIRED))
					{
						TryCleanup();
					}
#else
					TryCleanup();
#endif
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
#if NET8_0_OR_GREATER
				if (Volatile.Read(ref m_key) != IntPtr.Zero)
				{ // an armed callback still holds the cookie: force the fire (cancel makes the future ready) and let
				  // the fire path free the cookie and destroy the native handle; the task itself settles right now
					UnregisterCancellationRegistration();
					if (!this.IsSettled)
					{
						TrySetCanceled();
					}
					CancelHandles();
				}
				else if (HasFlag(FdbFuture.Flags.FIRED) && !HasFlag(FdbFuture.Flags.COMPLETED))
				{ // the fire already queued the completion: that work item owns the cleanup (cleaning up here could
				  // recycle the instance before the queued item runs); the task itself settles right now
					UnregisterCancellationRegistration();
					if (!this.IsSettled)
					{
						TrySetCanceled();
					}
				}
				else
				{
					TryCleanup();
				}
#else
				try
				{
					TryCleanup();
				}
				finally
				{
					if (Volatile.Read(ref m_key) != IntPtr.Zero) UnregisterCallback(this);
				}
#endif
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
