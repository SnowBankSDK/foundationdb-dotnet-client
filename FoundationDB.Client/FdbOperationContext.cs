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
	using System.Linq;
	using FoundationDB.Client.Core;

	/// <summary>
	/// Represents the context of a retry-able transactional function which accepts a read-only or read-write transaction.
	/// </summary>
	[DebuggerDisplay("Retries={Retries}, Committed={Committed}, Elapsed={Elapsed}")]
	[PublicAPI]
	public sealed class FdbOperationContext : IDisposable
	{

		/// <summary>The database used by the operation</summary>
		public IFdbDatabase Database => m_db;
		private readonly FdbDatabase m_db;

		/// <summary>The tenant used by the operation, if present</summary>
		public IFdbTenant? Tenant => m_tenant;
		private readonly FdbTenant? m_tenant;

		/// <summary>Cancellation token associated with the operation</summary>
		public CancellationToken Cancellation { get; }

		/// <summary>If set to true, will abort and not commit the transaction. If false, will try to commit the transaction (and retry on failure)</summary>
		public bool Abort { get; set; }

		/// <summary>Current attempt number (0 for first, 1+ for retries)</summary>
		public int Retries { get; private set; }

		/// <summary>Error code of the previous attempt</summary>
		/// <remarks>Equal to <see cref="FdbError.Success"/> for the first attempt, or the error code generate by the previous failed attempt.</remarks>
		public FdbError PreviousError { get; private set; }

		/// <summary>Stopwatch that is started at the creation of the transaction, and stopped when it commits or gets disposed</summary>
		private Stopwatch Clock { get; } = Stopwatch.StartNew();

		/// <summary>Duration of all the previous attempts before the current one (starts at 0, and gets updated at each reset/retry)</summary>
		internal TimeSpan BaseDuration { get; private set; }

		/// <summary>Time elapsed since the start of the first attempt</summary>
		public TimeSpan ElapsedTotal => this.Clock.Elapsed;

		/// <summary>Time elapsed since the start of the current attempt</summary>
		/// <remarks>This value is reset to zero every time the transaction fails and is retried.
		/// Note that this may not represent the actual lifetime of the transaction with the database itself, which starts at the first read operation.</remarks>
		public TimeSpan Elapsed => this.Clock.Elapsed.Subtract(this.BaseDuration);

		/// <summary>If true, the transaction has been committed successfully</summary>
		public bool Committed { get; private set; }

		/// <summary>If true, the lifetime of the context is handled by an external retry loop. If false, the context is linked to the lifetime of the transaction instance.</summary>
		internal bool Shared => (this.Mode & FdbTransactionMode.InsideRetryLoop) != 0;

		/// <summary>Mode of the transaction</summary>
		public FdbTransactionMode Mode { get; }

		/// <summary>Internal source of cancellation, able to abort any pending IO operations attached to this transaction</summary>
		internal CancellationTokenSource? TokenSource { get; }

		/// <summary>Transaction instance currently being used by this context</summary>
		internal FdbTransaction? Transaction;
		//note: field accessed via interlocked operations!

#if NET9_0_OR_GREATER
		internal readonly Lock PadLock = new();
#else
		internal readonly object PadLock = new();
#endif

		/// <summary>Current <see cref="System.Diagnostics.Activity"/>, if tracing is enabled</summary>
		public Activity? Activity { get; private set; }

		/// <summary>Create a new retry loop operation context</summary>
		/// <param name="db">Database that will be used by the retry loop</param>
		/// <param name="tenant">Tenant where the transaction will be executed</param>
		/// <param name="mode">Operation mode of the retry loop</param>
		/// <param name="ct">Optional cancellation token that will abort the retry loop if triggered.</param>
		internal FdbOperationContext(FdbDatabase db, FdbTenant? tenant, FdbTransactionMode mode, CancellationToken ct)
		{
			Contract.NotNull(db);

			m_db = db;
			m_tenant = tenant;
			this.Mode = mode;
			// note: we don't start the clock yet, only when the context starts executing...

			// by default, we hook ourselves to the database's CancellationToken, but we may need to also
			// hook with a different, caller-provided, token and respond to cancellation from both sites.
			var token = db.Cancellation;
			if (ct.CanBeCanceled && !ct.Equals(token))
			{
				this.TokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, ct);
				token = this.TokenSource.Token;
			}
			this.Cancellation = token;
		}

		#region Success Handlers...

		/// <summary>List of one or more state change callback</summary>
		/// <remarks>Either <c>null</c>, a single <see cref="Delegate"/>, or an array of Delegate</remarks>
		private object? StateCallbacks; // interlocked!

		private void RegisterStateCallback(object callback)
		{
			Contract.Debug.Requires(callback is Delegate or IHandleTransactionLifecycle);
			lock (this.PadLock)
			{
				var previous = this.StateCallbacks;
				if (previous == null)
				{ // first handler for this context
					this.StateCallbacks = callback;
				}
				else if (previous is object[] arr)
				{ // one more 
					Array.Resize(ref arr, arr.Length + 1);
					arr[^1] = callback;
					this.StateCallbacks = arr;
				}
				else
				{ // second handler
					Contract.Debug.Assert(!ReferenceEquals(previous, callback));
					this.StateCallbacks = new [] { previous, callback };
				}
			}
		}

		private Task ExecuteHandlers(ref object? handlers, FdbOperationContext ctx, FdbTransactionState state)
		{
			var cbk = Interlocked.Exchange(ref handlers, null);
			return cbk switch
			{
				null         => Task.CompletedTask,
				object[] arr => ExecuteMultipleHandlers(arr, ctx, state, this.Cancellation),
				_            => ExecuteSingleHandler(cbk, ctx, state, this.Cancellation)
			};
		}

		private static Task ExecuteSingleHandler(object del, FdbOperationContext ctx, FdbTransactionState arg, CancellationToken ct)
		{
			switch (del)
			{
				case IHandleTransactionLifecycle htl:
				{
					htl.OnTransactionStateChanged(ctx, arg);
					return Task.CompletedTask;
				}
				case Action<IFdbTransaction, FdbTransactionState> act:
				{
					act(ctx.Transaction!, arg);
					return Task.CompletedTask;
				}
				case Func<IFdbTransaction, FdbTransactionState, CancellationToken, Task> fct:
				{
					return fct(ctx.Transaction!, arg, ct);
				}
				case Action<FdbOperationContext, FdbTransactionState> act:
				{
					act(ctx, arg);
					return Task.CompletedTask;
				}
				case Func<FdbOperationContext, FdbTransactionState, CancellationToken, Task> fct:
				{
					return fct(ctx, arg, ct);
				}
			}

			throw new NotSupportedException("Unexpected handler delegate type.");
		}

		private static async Task ExecuteMultipleHandlers(object?[] arr, FdbOperationContext ctx, FdbTransactionState arg, CancellationToken ct)
		{
			foreach (object? del in arr)
			{
				if (del != null)
				{
					await ExecuteSingleHandler(del, ctx, arg, ct).ConfigureAwait(false);
				}
			}
		}

		/// <summary>Registers an observer that will only be called once the transaction state changes</summary>
		public void Observe(IHandleTransactionLifecycle observer)
		{
			Contract.NotNull(observer);
			RegisterStateCallback(observer);
		}

		/// <summary>Registers a callback that will be called when the transaction state changes</summary>
		public void Observe(Action<FdbOperationContext, FdbTransactionState> callback)
		{
			Contract.NotNull(callback);
			RegisterStateCallback(callback);
		}

		/// <summary>Registers a callback that will be called when the transaction state changes</summary>
		public void Observe(Func<FdbOperationContext, CancellationToken, Task> callback)
		{
			Contract.NotNull(callback);
			RegisterStateCallback(callback);
		}

		/// <summary>Register a callback that will only be called once the transaction has completed successfully (after a commit for write transactions)</summary>
		/// <remarks>NOTE: there are _no_ guarantees that the callback will fire at all, so this should only be used for cache updates or idempotent operations!</remarks>
		public void OnCommitFailed(Action<IFdbTransaction, FdbError> callback)
		{
			Contract.NotNull(callback);

			void CommitFailedHandler(IFdbTransaction tr, FdbTransactionState state)
			{
				if (state is FdbTransactionState.Faulted) callback(tr, tr.Context.PreviousError);
			}

			RegisterStateCallback((Action<IFdbTransaction, FdbTransactionState>) CommitFailedHandler);
		}

		/// <summary>Register a callback that will only be called once the transaction has completed successfully (after a commit for write transactions)</summary>
		/// <remarks>
		/// <para>Please note that it is <b>NOT</b> guaranteed that the callback will be called at all! This should only be used for cache updates, idempotent operations, or for logging purpose.</para>
		/// </remarks>
		public void OnCommitFailed(Func<IFdbTransaction, FdbError, CancellationToken, Task> callback)
		{
			Contract.NotNull(callback);

			Task CommitFailedHandler(IFdbTransaction tr, FdbTransactionState state, CancellationToken cancel)
				=> state is FdbTransactionState.Faulted ? callback(tr, tr.Context.PreviousError, cancel) : Task.CompletedTask;

			RegisterStateCallback((Func<IFdbTransaction, FdbTransactionState, CancellationToken, Task>) CommitFailedHandler);
		}

		/// <summary>Register a callback that will be called if the transaction fails to commit with a <see cref="FdbError.NotCommitted"/> error (conflict with another transaction)</summary>
		/// <remarks>
		/// <para>Please note that it is <b>NOT</b> guaranteed that the callback will be called at all! This should only be used for cache updates, idempotent operations, or for logging purpose.</para>
		/// </remarks>
		public void OnConflict(Action<IFdbTransaction> callback)
		{
			Contract.NotNull(callback);

			void ConflictHandler(IFdbTransaction tr, FdbTransactionState state)
			{
				if (state is FdbTransactionState.Faulted && tr.Context.PreviousError == FdbError.NotCommitted) callback(tr);
			}

			RegisterStateCallback((Action<IFdbTransaction, FdbTransactionState>) ConflictHandler);
		}

		/// <summary>Register a callback that will only be called once the transaction has completed successfully (after a commit for write transactions)</summary>
		/// <remarks>
		/// <para>Please note that it is <b>NOT</b> guaranteed that the callback will be called at all! This should only be used for cache updates, idempotent operations, or for logging purpose.</para>
		/// </remarks>
		public void OnConflict(Func<IFdbTransaction, CancellationToken, Task> callback)
		{
			Contract.NotNull(callback);

			Task ConflictHandler(IFdbTransaction tr, FdbTransactionState state, CancellationToken cancel)
				=> state is FdbTransactionState.Faulted && tr.Context.PreviousError == FdbError.NotCommitted ? callback(tr, cancel) : Task.CompletedTask;

			RegisterStateCallback((Func<IFdbTransaction, FdbTransactionState, CancellationToken, Task>) ConflictHandler);
		}

		/// <summary>Register a callback that will only be called once the transaction has completed successfully (after a commit for write transactions)</summary>
		/// <remarks>
		/// <para>Please note that it is <b>NOT</b> guaranteed that the callback will be called at all! This should only be used for cache updates, idempotent operations, or for logging purpose.</para>
		/// </remarks>
		public void OnSuccess(Action<IFdbTransaction> callback)
		{
			Contract.NotNull(callback);

			void SuccessHandler(IFdbTransaction tr, FdbTransactionState state)
			{
				if (state is FdbTransactionState.Commit or FdbTransactionState.Completed) callback(tr);
			}

			RegisterStateCallback((Action<IFdbTransaction, FdbTransactionState>) SuccessHandler);
		}

		/// <summary>Register a callback that will only be called once the transaction has completed successfully (after a commit for write transactions)</summary>
		/// <remarks>
		/// <para>Please note that it is <b>NOT</b> guaranteed that the callback will be called at all! This should only be used for cache updates, idempotent operations, or for logging purpose.</para>
		/// </remarks>
		public void OnSuccess(Func<IFdbTransaction, CancellationToken, Task> callback)
		{
			Contract.NotNull(callback);

			Task SuccessHandler(IFdbTransaction tr, FdbTransactionState state, CancellationToken cancel)
				=> state is FdbTransactionState.Commit or FdbTransactionState.Completed ? callback(tr, cancel) : Task.CompletedTask;

			RegisterStateCallback((Func<IFdbTransaction, FdbTransactionState, CancellationToken, Task>) SuccessHandler);
		}

		#endregion

		#region Local Data...

		// To help build a caching layer on top of transactions, it is necessary to be able to attach cached instance to each transaction
		// The intent is that a layer implementation will use to build a "per-transaction state" on the first call to the layer within this transaction,
		// and then reuse the same instance if the layer is called multiple times within the same transaction.

		// The convention is that each layer uses its own type has the "key" of the cached instances.
		// But since a layer can have multiple "instances" (ex: multiple indexes, multiple queues, ...) each instance is also coupled with a "token" key
		// If a layer can only have a single instance, it can use the example the empty string has its "token" key.
		// If not, the layer can use any type of key, if it can guarantee that it is unique. For exemple: for a record table "Users", the key can be the name
		// itself, IF IT IS NOT POSSIBLE TO HAVE A DIFFERENT "Users" TABLE IN THE APPLICATION! If there can be collisions, then the key should be prefixed by
		// some other "namespace" to make it globally unique.

		/// <summary>Stores contextual data that can be added and retrieved at any step of the transaction's processing</summary>
		/// <remarks>Access to this collection must be performed under lock, because a transaction can be used concurrently from multiple threads!</remarks>
		/// map[ typeof(TState) => map[ TToken => TState ] ]
		private Dictionary<Type, object>? LocalData { get; set; }

		/// <summary>Returns the container for features attached to this context</summary>
		/// <returns>The existing or newly created container.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private Dictionary<Type, object> GetOrCreateLocalDataContainer()
		{
			return this.LocalData ??= new ();
		}

		/// <summary>Clears any local data currently attached on this transaction</summary>
		internal void ClearAllLocalData()
		{
			lock (this.PadLock)
			{
				this.LocalData?.Clear();
			}
		}

		internal void ResetInternals()
		{
			lock (this.PadLock)
			{
				// reset the context clock!
				this.Clock.Restart();
				this.BaseDuration = TimeSpan.Zero;
				this.Retries = 0;
				this.Abort = false;
				this.Committed = false;
			}
		}

		/// <summary>Set the value of a cached instance attached to the transaction</summary>
		/// <typeparam name="TState">Type of the instance</typeparam>
		/// <typeparam name="TToken">Type of the key used to distinguish multiple instance of the same "type"</typeparam>
		/// <param name="key">Value of the key to remove. If there can be only one instance per <typeparamref name="TState"/>, use a constant such as the <c>string.Empty</c></param>
		/// <param name="newState">New instance that must be attached to the transaction</param>
		/// <returns>If there was already a cached instance for this key, it will be discarded</returns>
		public void SetLocalData<TState, TToken>(TToken key, TState newState)
			where TState : class
			where TToken : notnull
		{
			Contract.NotNull(key);
			Contract.NotNull(newState);
			lock (this.PadLock)
			{
				var container = GetOrCreateLocalDataContainer();
				if (!container.TryGetValue(typeof(TState), out var slot))
				{
					slot = new Dictionary<string, TState>(StringComparer.Ordinal);
					container[typeof(TState)] = slot;
				}
				var items = (Dictionary<TToken, TState>) slot;
				items[key] = newState;
			}
		}

		/// <summary>Replace the value of a cached instance attached to the transaction, and return the previous one</summary>
		/// <typeparam name="TState">Type of the instance</typeparam>
		/// <typeparam name="TToken">Type of the key used to distinguish multiple instance of the same "type"</typeparam>
		/// <param name="key">Value of the key to remove. If there can be only one instance per <typeparamref name="TState"/>, use a constant such as the <c>string.Empty</c></param>
		/// <param name="newState">New instance that must be attached to the transaction</param>
		/// <returns>Previous cached instance, or null if none was found.</returns>
		public TState? ReplaceLocalData<TState, TToken>(TToken key, TState newState)
			where TState : class
			where TToken : notnull
		{
			Contract.NotNull(key);
			Contract.NotNull(newState);
			lock (this.PadLock)
			{
				var container = GetOrCreateLocalDataContainer();
				if (!container.TryGetValue(typeof(TState), out var slot))
				{
					var items =  new Dictionary<TToken, TState>
					{
						[key] = newState
					};
					container[typeof(TState)] = items;
					return null;
				}
				else
				{
					var items = (Dictionary<TToken, TState>) slot;
					items.TryGetValue(key, out var previous);
					items[key] = newState;
					return previous;
				}
			}
		}

		/// <summary>Remove a cached instance previously attached to the transaction</summary>
		/// <typeparam name="TState">Type of the instance</typeparam>
		/// <typeparam name="TToken">Type of the key used to distinguish multiple instance of the same "type"</typeparam>
		/// <param name="key">Value of the key to remove. If there can be only one instance per <typeparamref name="TState"/>, use a constant such as the <c>string.Empty</c></param>
		/// <returns>Returns <see langword="true"/> if the value was found and removed; otherwise, false.</returns>
		public bool RemoveLocalData<TState, TToken>(TToken key)
			where TState : class
			where TToken : notnull
		{
			Contract.NotNull(key);
			lock (this.PadLock)
			{
				var container = this.LocalData;
				if (container != null && container.TryGetValue(typeof(TState), out var slot))
				{
					var items = (Dictionary<TToken, TState>) slot;
					return items.Remove(key);
				}
				return false;
			}
		}

		/// <summary>Return the corresponding instance attached to the transaction, if it exists.</summary>
		/// <typeparam name="TState">Type of the instance</typeparam>
		/// <typeparam name="TToken">Type of the key used to distinguish multiple instance of the same "type"</typeparam>
		/// <param name="key">Value of the key. If there can be only one instance per <typeparamref name="TState"/>, use a constant such as the <c>string.Empty</c></param>
		/// <param name="state">Receive the value if it was found; otherwise, <c>default(<typeparamref name="TState"/>)</c></param>
		/// <returns>Returns <see langword="true"/> if the value was found; otherwise, <see langword="false"/>.</returns>
		[ContractAnnotation("=>false, state:null; =>true, state:notnull")]
		public bool TryGetLocalData<TState, TToken>(TToken key, [NotNullWhen(true)] out TState? state)
			where TState : class
			where TToken : notnull
		{
			Contract.NotNull(key);
			lock (this.PadLock)
			{
				var container = this.LocalData;
				if (container != null && container.TryGetValue(typeof(TState), out var slot))
				{
					var items = (Dictionary<TToken, TState>) slot;
					if (items.TryGetValue(key, out var value))
					{
						state = value;
						return true;
					}
				}
				state = default!;
				return false;
			}
		}

		/// <summary>Return the corresponding instance attached to the transaction, or use the specified instance no value was found.</summary>
		/// <typeparam name="TState">Type of the instance</typeparam>
		/// <typeparam name="TToken">Type of the key used to distinguish multiple instance of the same "type"</typeparam>
		/// <param name="key">Value of the key. If there can be only one instance per <typeparamref name="TState"/>, use a constant such as the <c>string.Empty</c></param>
		/// <param name="newState">Instance that will be used if no value already exists in this transaction.</param>
		/// <returns>Either the existing value, or <paramref name="newState"/>.</returns>
		public TState GetOrCreateLocalData<TState, TToken>(TToken key, TState newState)
			where TState : class
			where TToken : notnull
		{
			Contract.NotNull(key);
			Contract.NotNull(newState);
			lock (this.PadLock)
			{
				var container = GetOrCreateLocalDataContainer();
				TState? result;
				if (container.TryGetValue(typeof(TState), out var slot))
				{
					var items = (Dictionary<TToken, TState>) slot;
					if (!items.TryGetValue(key, out result))
					{
						result = newState;
						items[key] = result;
					}
				}
				else
				{
					result = newState;
					slot = new Dictionary<TToken, TState>
					{
						[key] = result
					};
					container[typeof(TState)] = slot;
				}
				return result;
			}
		}

		/// <summary>Return the corresponding instance attached to the transaction, or invoke the specified factory if it was not already specified.</summary>
		/// <typeparam name="TState">Type of the instance</typeparam>
		/// <typeparam name="TToken">Type of the key used to distinguish multiple instance of the same "type"</typeparam>
		/// <param name="key">Value of the key. If there can be only one instance per <typeparamref name="TState"/>, use a constant such as the <c>string.Empty</c></param>
		/// <param name="factory">Handler called to generate a new <typeparamref name="TState"/> instance if there is no match.</param>
		/// <returns>Either the existing value, or the value returned by invoking <paramref name="factory"/>.</returns>
		//REVIEW: should we return a tuple (TState Data, bool Created) instead ?
		public TState GetOrCreateLocalData<TState, TToken>(TToken key, Func<TState> factory)
			where TState : class
			where TToken : notnull
		{
			Contract.NotNull(key);
			Contract.NotNull(factory);
			lock (this.PadLock)
			{
				var container = GetOrCreateLocalDataContainer();
				TState? result;
				if (container.TryGetValue(typeof(TState), out var slot))
				{
					var items = (Dictionary<TToken, TState>) slot;
					if (!items.TryGetValue(key, out result))
					{
						result = factory() ?? throw new InvalidOperationException("Constructed state cannot be null");
						items[key] = result;
					}
				}
				else
				{
					result = factory() ?? throw new InvalidOperationException("Constructed state cannot be null");
					slot = new Dictionary<TToken, TState>
					{
						[key] = result
					};
					container[typeof(TState)] = slot;
				}
				return result;
			}
		}

		#endregion

		#region Value Checkers...

		/// <summary>List of outstanding checks that must match in order for the transaction to commit successfully</summary>
		private List<(Slice Key, Slice ExpectedValue, string Tag, Task<(FdbValueCheckResult Result, Slice Actual)> ActualValue)>? ValueChecks { get; set; }

		/// <summary>Return the result of all value checks performed with the specified tag in the previous attempt</summary>
		/// <param name="tag">Tag that was passed to a call to <see cref="AddValueCheck"/> (or similar overloads) in the previous attempt of this context, that failed (meaning the expected value did not match with the database)</param>
		/// <returns>Combined result of all value checks with this tag.
		/// If <see cref="FdbValueCheckResult.Unknown"/> no check was performed with this tag in the previous attempt, and the application is left to decide the odds of the value having changed in the database.</returns>
		/// If <see cref="FdbValueCheckResult.Failed"/> then at least on check with this tag failed, and there is a very high probability that the cached values have changed in the database.
		/// If <see cref="FdbValueCheckResult.Success"/> then all checks with this tag passed, and there is a very low probability that the cached values have changed in the database.
		/// <remarks>
		/// The caller should use this as a hint about the likelyhood that previously cached data is invalid, and should be discarded.
		/// Note that this method can fall victim to the ABA pattern, meaning that a subsquent read of the checked key could return the expected value (changed back to its original value by another transaction).
		/// To reduce the chances of ABA, checked keys should only be updated using atomic increment operations, or use versionstamps if possible.
		/// If you need to get more precise results about which key/value pair passed or failed, you can also call <see cref="GetValueChecksFromPreviousAttempt"/> with the same tag, which will returns all key/value pairs with their individual results.
		/// </remarks>
		public FdbValueCheckResult TestValueCheckFromPreviousAttempt(string tag)
		{
			Dictionary<string, (FdbValueCheckResult Result, List<(FdbValueCheckResult, Slice, Slice, Slice)>)>? checks;
			lock (this.PadLock)
			{
				checks = this.FailedValueCheckTags;
			}
			return checks != null && checks.TryGetValue(tag, out var check) ? check.Result : FdbValueCheckResult.Unknown;
		}

		/// <summary>Return the list of all value-checks performed in the previous transaction attempt</summary>
		/// <param name="tag">If not-null, only return the checks with the specified tag.</param>
		/// <param name="result">If not-null, only return the checks with the specified result</param>
		/// <returns>List of value-checks that match the specified filters.</returns>
		public List<(string Tag, FdbValueCheckResult Result, Slice Key, Slice Expected, Slice Actual)> GetValueChecksFromPreviousAttempt(string? tag = null, FdbValueCheckResult? result = null)
		{
			Dictionary<string, (FdbValueCheckResult Result, List<(FdbValueCheckResult Result, Slice Key, Slice Expected, Slice Actual)> Checks)>? checks;
			lock (this.PadLock)
			{
				checks = this.FailedValueCheckTags;
			}

			var res = new List<(string Tag, FdbValueCheckResult Result, Slice Key, Slice Expected, Slice Actual)>();
			if (checks != null)
			{
				foreach (var kv in checks)
				{
					if (tag == null || tag == kv.Key)
					{
						foreach (var item in kv.Value.Checks)
						{
							if (result == null || item.Result == result)
							{
								res.Add((kv.Key, item.Result, item.Key, item.Expected, item.Actual));
							}
						}
					}
				}
			}
			return res;
		}

		/// <summary>Contains the tags of all the failed value checks from the previous attempt</summary>
		private Dictionary<string, (FdbValueCheckResult Result, List<(FdbValueCheckResult Result, Slice Key, Slice Expected, Slice Actual)>)>? FailedValueCheckTags { get; set; }

		/// <inheritdoc cref="AddValueCheck"/>
		public void AddValueCheck<TKey>(string tag, in TKey key, Slice expectedValue)
			where TKey : struct, IFdbKey
		{
			// unfortunately, we have to store the encoded key and value into the heap, since the check is async
			AddValueCheck(tag, FdbKeyHelpers.ToSlice(in key), expectedValue);
		}

		/// <inheritdoc cref="AddValueCheck"/>
		public void AddValueCheck<TKey, TValue>(string tag, in TKey key, in TValue expectedValue)
			where TKey : struct, IFdbKey
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
		{
			// unfortunately, we have to store the encoded key and value into the heap, since the check is async
			AddValueCheck(tag, FdbKeyHelpers.ToSlice(in key), FdbValueHelpers.ToSlice(expectedValue));
		}

		/// <summary>Add a check on the value of the key, that will be resolved before the transaction is able to commit</summary>
		/// <param name="tag">Application-provided tag that can be used later to decide which layer failed the check.</param>
		/// <param name="key">Key to check</param>
		/// <param name="expectedValue">Expected value of the key. A value of <see cref="Slice.Nil"/> means the key is expected to NOT exist.</param>
		/// <remarks>
		/// If key does not have the expected value, the transaction will fail to commit, with an <see cref="FdbError.NotCommitted"/> error (simulating a conflict), which should trigger a retry,
		/// and a call to <see cref="TestValueCheckFromPreviousAttempt"/>(<paramref name="tag"/>) will return <see cref="FdbValueCheckResult.Failed"/> for the next iteration of the retry-loop.
		/// Any change to the value of the key _after_ this call, in the same transaction, will not be seen by this check. Only the value seen by this transaction at call time is considered.
		/// </remarks>
		public void AddValueCheck(string tag, Slice key, Slice expectedValue)
		{
			Contract.NotNullOrEmpty(tag);

			var tr = this.Transaction;
			if (tr == null) throw new InvalidOperationException();
			var task = tr.CheckValueAsync(key.Span, expectedValue);

			lock (this.PadLock)
			{
				(this.ValueChecks ??= [ ]).Add((key, expectedValue, tag, task));
			}
		}

		/// <summary>Add a check on the values of one or more keys, that will be resolved before the transaction is able to commit</summary>
		/// <param name="tag">Application-provided tag that can be used later to decide which layer failed the check.</param>
		/// <param name="items">List of keys to check, and their expected values. A value of <see cref="Slice.Nil"/> means the corresponding key is expected to NOT exist.</param>
		/// <remarks>
		/// If any of the keys does not have the expected value, the transaction will fail to commit, with an <see cref="FdbError.NotCommitted"/> error (simulating a conflict), which should trigger a retry,
		/// and a call to <see cref="TestValueCheckFromPreviousAttempt"/>(<paramref name="tag"/>) will return <see cref="FdbValueCheckResult.Failed"/> for the next iteration of the retry-loop.
		/// Any change to the value of these keys _after_ this call, in the same transaction, will not be seen by this check. Only values seen by this transaction at call time are considered.
		/// </remarks>
		public void AddValueChecks(string tag, IEnumerable<KeyValuePair<Slice, Slice>> items)
		{
			Contract.NotNull(items);
			if (items.TryGetSpan(out var span))
			{
				AddValueChecks(tag, span);
			}
			else
			{
				AddValueChecks(tag, items.ToArray().AsSpan());
			}
		}

		/// <summary>Add a check on the values of one or more keys, that will be resolved before the transaction is able to commit</summary>
		/// <param name="tag">Application-provided tag that can be used later to decide which layer failed the check.</param>
		/// <param name="items">List of keys to check, and their expected values. A value of <see cref="Slice.Nil"/> means the corresponding key is expected to NOT exist.</param>
		/// <remarks>
		/// If any of the keys does not have the expected value, the transaction will fail to commit, with an <see cref="FdbError.NotCommitted"/> error (simulating a conflict), which should trigger a retry,
		/// and a call to <see cref="TestValueCheckFromPreviousAttempt"/>(<paramref name="tag"/>) will return <see cref="FdbValueCheckResult.Failed"/> for the next iteration of the retry-loop.
		/// Any change to the value of these keys _after_ this call, in the same transaction, will not be seen by this check. Only values seen by this transaction at call time are considered.
		/// </remarks>
		public void AddValueChecks(string tag, KeyValuePair<Slice, Slice>[] items)
		{
			Contract.NotNull(items);
			AddValueChecks(tag, items.AsSpan());
		}

		/// <summary>Add a check on the values of one or more keys, that will be resolved before the transaction is able to commit</summary>
		/// <param name="tag">Application-provided tag that can be used later to decide which layer failed the check.</param>
		/// <param name="items">List of keys to check, and their expected values. A value of <see cref="Slice.Nil"/> means the corresponding key is expected to NOT exist.</param>
		/// <remarks>
		/// If any of the keys does not have the expected value, the transaction will fail to commit, with an <see cref="FdbError.NotCommitted"/> error (simulating a conflict), which should trigger a retry,
		/// and a call to <see cref="TestValueCheckFromPreviousAttempt"/>(<paramref name="tag"/>) will return <see cref="FdbValueCheckResult.Failed"/> for the next iteration of the retry-loop.
		/// Any change to the value of these keys _after_ this call, in the same transaction, will not be seen by this check. Only values seen by this transaction at call time are considered.
		/// </remarks>
		public void AddValueChecks(string tag, ReadOnlySpan<KeyValuePair<Slice, Slice>> items)
		{
			var tr = this.Transaction;
			if (tr == null) throw new InvalidOperationException("Transaction is not in a valid state.");

			// quick paths...
			if (items.Length == 0) return;
			if (items.Length == 1)
			{
				AddValueCheck(tag, items[0].Key, items[0].Value);
				return;
			}

			var taskBuffer = ArrayPool<Task<(FdbValueCheckResult, Slice)>>.Shared.Rent(items.Length);
			try
			{
				for (int i = 0; i < items.Length; i++)
				{
					taskBuffer[i] = tr.CheckValueAsync(items[i].Key.Span, items[i].Value);
				}

				lock (this.PadLock)
				{
					var checks = this.ValueChecks ??= new List<(Slice, Slice, string, Task<(FdbValueCheckResult Result, Slice Actual)>)>();
					int capacity = checked(checks.Count + items.Length);
					if (capacity > checks.Capacity) checks.Capacity = capacity;
					for (int i = 0; i < items.Length; i++)
					{
						checks.Add((items[i].Key, items[i].Value, tag, taskBuffer[i]));
					}
				}
			}
			finally
			{
				ArrayPool<Task<(FdbValueCheckResult, Slice)>>.Shared.Return(taskBuffer, clearArray: true);
			}
		}

		private bool ObserveValueCheckResult(string tag, Slice key, Slice expectedValue, Slice actualResult, FdbValueCheckResult result)
		{
			var tags = (this.FailedValueCheckTags ??= new Dictionary<string, (FdbValueCheckResult Result, List<(FdbValueCheckResult, Slice, Slice, Slice)> Checks)>(StringComparer.Ordinal));

			if (!tags.TryGetValue(tag, out (FdbValueCheckResult Result, List<(FdbValueCheckResult Result, Slice Key, Slice Expected, Slice Actual)> Checks) previous))
			{
				previous.Result = FdbValueCheckResult.Success;
				previous.Checks = [ ];
			}

			bool pass;
			previous.Checks.Add((result, key, expectedValue, actualResult));
			if (result == FdbValueCheckResult.Failed)
			{
				previous.Result = FdbValueCheckResult.Failed;
				if (this.Transaction?.IsLogged() == true) this.Transaction.Annotate($"Failed value-check '{tag}' for {FdbKey.Dump(key)}: expected {expectedValue:V}, actual {actualResult:V}");
				pass = false;
			}
			else
			{
				Contract.Debug.Assert(result == FdbValueCheckResult.Success);
				pass = true;
			}

			tags[tag] = previous;

			return pass;
		}

		private bool HasPendingValueChecks([MaybeNullWhen(false)] out List<(Slice, Slice, string, Task<(FdbValueCheckResult, Slice)>)> checks)
		{
			lock (this.PadLock)
			{
				checks = this.ValueChecks;
				this.ValueChecks = null;
				return checks != null;
			}

		}

		private ValueTask<bool> ValidateValueChecks(bool ignoreFailedTasks)
		{
			if (!HasPendingValueChecks(out var checks))
			{
				return new ValueTask<bool>(true);
			}
			return ValidateValueChecksSlow(checks, ignoreFailedTasks);
		}

		private async ValueTask<bool> ValidateValueChecksSlow(List<(Slice Key, Slice ExpectedValue, string Tag, Task<(FdbValueCheckResult Result, Slice Actual)> ActualValue)> checks, bool ignoreFailedTasks)
		{
			//note: even if it looks like we are sequentially awaiting all tasks, by the time the first one is complete,
			// the rest of the tasks will probably be already completed as well. Anyway, we need to inspect each individual task
			// to check for any failed tasks anyway, so we can't use Task.WhenAll(...) here

			bool pass = true;
			int pending = 0;

			foreach (var check in checks)
			{
				(FdbValueCheckResult Result, Slice Actual) result;
				if (!check.ActualValue.IsCompleted) pending++;
				try
				{
					result = await check.ActualValue.ConfigureAwait(false);
				}
				catch (FdbException) when (ignoreFailedTasks)
				{
					continue;
				}
				pass &= ObserveValueCheckResult(check.Tag, check.Key, check.ExpectedValue, result.Actual, result.Result);
			}

			if (pending != 0 && this.Transaction?.IsLogged() == true)
			{
				this.Transaction.Annotate($"Awaited {pending:N0}/{checks.Count:N0} pending value-check(s)");
			}

			if (!pass && this.Transaction?.IsLogged() == true)
			{
				this.Transaction.Annotate("At least ony value-check failed!");
			}

			return pass;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailValueCheck()
		{
			return new FdbException(FdbError.NotCommitted, "The value of the a key in the database did not match the expected value.");
		}

		private static string? PrettifyMethodName(string? name)
		{
			// we want to clean up things like "<SomeType.SomeMethod>b__0"
			if (name is not null && name.Length >= 6 && name[0] == '<')
			{
				int p = name.LastIndexOf('>');
				if (p > 1)
				{
					return name[1..p];
				}
			}
			return name;
		}

		private static string? PrettifyTargetName(string? name)
		{
			if (name != null && name.IndexOf(".<>c__DisplayClass", StringComparison.Ordinal) > 0)
			{
				//TODO: !!
			}
			return name;
		}

		#endregion

		/// <summary>Execute a retry loop on this context</summary>
		internal static async Task<TResult> ExecuteInternal<TState, TIntermediate, TResult>(FdbOperationContext context, TState? state, Delegate handler, Delegate? success)
		{
			Contract.Debug.Requires(context != null && handler != null && context.Shared);

			var db = context.m_db;
			var tenant = context.m_tenant;
			Contract.Debug.Requires(tenant != null || db != null);

			if (context.Abort) throw new InvalidOperationException("Operation context has already been aborted or disposed");

			var tracingOptions = db.Options.DefaultTracing;
			var targetName = PrettifyTargetName((handler.Target?.GetType() ?? handler.Method.DeclaringType)?.GetFriendlyName());
			var methodName = PrettifyMethodName(handler.Method.Name);

			using var mainActivity = tracingOptions.HasFlag(FdbTracingOptions.RecordTransactions) ? FdbClientInstrumentation.ActivitySource.StartActivity(context.Mode == FdbTransactionMode.ReadOnly ? "FDB Read" : "FDB ReadWrite", kind: ActivityKind.Client) : null;
			context.Activity = mainActivity;

			bool reportTransStarted = false;
			bool reportOpStarted = false;
			context.Clock.Restart();

			try
			{
				// make sure to reset everything (in case a context is reused multiple times)
				context.Committed = false;
				context.Retries = 0;
				context.BaseDuration = TimeSpan.Zero;
				//note: we start the clock immediately, but the transaction's 5 seconds max lifetime is actually measured from the first read operation (Get, GetRange, GetReadVersion, etc...)
				// => algorithms that monitor the elapsed duration to rate limit themselves may think that the trans is older than it really is...
				// => we would need to plug into the transaction handler itself to be notified when exactly a read op starts...

				TResult result = default!;

				using (var trans = tenant != null ? tenant.BeginTransaction(context.Mode, CancellationToken.None, context) : db.BeginTransaction(context.Mode, CancellationToken.None, context))
				{
					//note: trans may be different from context.Transaction if it has been filtered!
					Contract.Debug.Assert(context.Transaction != null);

					if (mainActivity?.IsAllDataRequested == true)
					{
						mainActivity.SetTag("db.system", "fdb");
						//REVIEW: should we include the cluster file content for "db.connection_string"?
						//note: we do not provide "db.user" because this does not exist in fdb

						mainActivity.SetTag("db.fdb.trans.id", trans.Id);
						if (trans.IsReadOnly) mainActivity.SetTag("db.fdb.trans.readonly", true);
						if (trans.IsSnapshot) mainActivity.SetTag("db.fdb.trans.snapshot", true);
					}

					FdbClientInstrumentation.ReportTransactionStart(context, trans);
					reportTransStarted = true;

					while (!context.Committed && !context.Cancellation.IsCancellationRequested)
					{
						bool hasResult = false;
						bool hasRunValueChecks = false;
						result = default!;

						using var attemptActivity = tracingOptions.HasFlag(FdbTracingOptions.RecordOperations) ? FdbClientInstrumentation.ActivitySource.StartActivity(targetName + "::" + methodName, kind: ActivityKind.Client) : null;
						if (attemptActivity?.IsAllDataRequested == true)
						{
							attemptActivity.SetTag("db.system", "fdb");
							attemptActivity.SetTag("db.fdb.trans.id", trans.Id);
							if (context.Retries > 0) attemptActivity.SetTag("db.fdb.error.retry_count", context.Retries);
							if (context.PreviousError != FdbError.Success) attemptActivity.SetTag("db.fdb.error.previous", context.PreviousError);
						}


						Activity? currentActivity = null;
						try
						{
							TIntermediate intermediate;

							currentActivity = tracingOptions.HasFlag(FdbTracingOptions.RecordSteps) ? FdbClientInstrumentation.ActivitySource.StartActivity("FDB Handler") : null;
							if (currentActivity != null)
							{
								context.Activity = currentActivity;
								if (currentActivity.IsAllDataRequested)
								{
									currentActivity.SetTag("db.system", "fdb");
									currentActivity.SetTag("db.fdb.trans.id", trans.Id);
									if (context.Retries > 0) currentActivity.SetTag("db.fdb.error.retry_count", context.Retries);
									if (context.PreviousError != FdbError.Success) currentActivity.SetTag("db.fdb.error.previous", context.PreviousError);
									currentActivity.SetTag("db.fdb.handler.target", targetName);
									currentActivity.SetTag("db.fdb.handler.method", methodName);
								}
							}

							Contract.Debug.Assert(!reportOpStarted);
							FdbClientInstrumentation.ReportOperationStarted(context, trans);
							reportOpStarted = true;

							// call the user provided lambda
							switch (handler)
							{
								#region Read Only...

								#region With State...

								case Func<IFdbReadOnlyTransaction, TState, Task<TResult>> f:
								{
									intermediate = default!;
									result = await f(trans, state!).ConfigureAwait(false);
									hasResult = true;
									break;
								}
								case Func<IFdbReadOnlyTransaction, TState, ValueTask<TResult>> f:
								{
									intermediate = default!;
									result = await f(trans, state!).ConfigureAwait(false);
									hasResult = true;
									break;
								}
								case Func<IFdbReadOnlyTransaction, TState, TResult> f:
								{
									intermediate = default!;
									result = f(trans, state!);
									hasResult = true;
									break;
								}
								case Func<IFdbReadOnlyTransaction, TState, Task<TIntermediate>> f:
								{
									intermediate = await f(trans, state!).ConfigureAwait(false);
									break;
								}
								case Func<IFdbReadOnlyTransaction, TState, ValueTask<TIntermediate>> f:
								{
									intermediate = await f(trans, state!).ConfigureAwait(false);
									break;
								}
								case Func<IFdbReadOnlyTransaction, TState, TIntermediate> f:
								{
									intermediate = f(trans, state!);
									break;
								}
								case Func<IFdbReadOnlyTransaction, TState, Task> f:
								{
									intermediate = default!;
									await f(trans, state!).ConfigureAwait(false);
									break;
								}
								case Func<IFdbReadOnlyTransaction, TState, ValueTask> f:
								{
									intermediate = default!;
									await f(trans, state!).ConfigureAwait(false);
									break;
								}
								case Action<IFdbReadOnlyTransaction, TState> a:
								{
									intermediate = default!;
									a(trans, state!);
									break;
								}

								#endregion

								#region w/o State...

								case Func<IFdbReadOnlyTransaction, Task<TResult>> f:
								{
									intermediate = default!;
									result = await f(trans).ConfigureAwait(false);
									hasResult = true;
									break;
								}
								case Func<IFdbReadOnlyTransaction, ValueTask<TResult>> f:
								{
									intermediate = default!;
									result = await f(trans).ConfigureAwait(false);
									hasResult = true;
									break;
								}
								case Func<IFdbReadOnlyTransaction, TResult> f:
								{
									intermediate = default!;
									result = f(trans);
									hasResult = true;
									break;
								}
								case Func<IFdbReadOnlyTransaction, Task<TIntermediate>> f:
								{
									intermediate = await f(trans).ConfigureAwait(false);
									break;
								}
								case Func<IFdbReadOnlyTransaction, ValueTask<TIntermediate>> f:
								{
									intermediate = await f(trans).ConfigureAwait(false);
									break;
								}
								case Func<IFdbReadOnlyTransaction, TIntermediate> f:
								{
									intermediate = f(trans);
									break;
								}
								case Func<IFdbReadOnlyTransaction, Task> f:
								{
									intermediate = default!;
									await f(trans).ConfigureAwait(false);
									break;
								}
								case Func<IFdbReadOnlyTransaction, ValueTask> f:
								{
									intermediate = default!;
									await f(trans).ConfigureAwait(false);
									break;
								}
								case Action<IFdbReadOnlyTransaction> a:
								{
									intermediate = default!;
									a(trans);
									break;
								}

								#endregion

								#endregion

								#region Read/Write...

								#region w/o state...

								case Func<IFdbTransaction, Task<TResult>> f:
								{
									intermediate = default!;
									result = await f(trans).ConfigureAwait(false);
									hasResult = true;
									break;
								}
								case Func<IFdbTransaction, ValueTask<TResult>> f:
								{
									intermediate = default!;
									result = await f(trans).ConfigureAwait(false);
									hasResult = true;
									break;
								}
								case Func<IFdbTransaction, Task<TIntermediate>> f:
								{
									intermediate = await f(trans).ConfigureAwait(false);
									break;
								}
								case Func<IFdbTransaction, ValueTask<TIntermediate>> f:
								{
									intermediate = await f(trans).ConfigureAwait(false);
									break;
								}
								case Func<IFdbTransaction, Task> f:
								{
									intermediate = default!;
									await f(trans).ConfigureAwait(false);
									break;
								}
								case Func<IFdbTransaction, ValueTask> f:
								{
									intermediate = default!;
									await f(trans).ConfigureAwait(false);
									break;
								}
								case Func<IFdbTransaction, TIntermediate> f:
								{
									intermediate = f(trans);
									break;
								}
								case Func<IFdbTransaction, TResult> f:
								{
									intermediate = default!;
									result = f(trans);
									hasResult = true;
									break;
								}
								case Action<IFdbTransaction> a:
								{
									intermediate = default!;
									a(trans);
									break;
								}

#endregion

								#region With state...

								case Func<IFdbTransaction, TState, Task<TIntermediate>> f:
								{
									intermediate = await f(trans, state!).ConfigureAwait(false);
									break;
								}
								case Func<IFdbTransaction, TState, ValueTask<TIntermediate>> f:
								{
									intermediate = await f(trans, state!).ConfigureAwait(false);
									break;
								}
								case Func<IFdbTransaction, TState, Task<TResult>> f:
								{
									intermediate = default!;
									result = await f(trans, state!).ConfigureAwait(false);
									hasResult = true;
									break;
								}
								case Func<IFdbTransaction, TState, ValueTask<TResult>> f:
								{
									intermediate = default!;
									result = await f(trans, state!).ConfigureAwait(false);
									hasResult = true;
									break;
								}
								case Func<IFdbTransaction, TState, Task> f:
								{
									intermediate = default!;
									await f(trans, state!).ConfigureAwait(false);
									break;
								}
								case Func<IFdbTransaction, TState, ValueTask> f:
								{
									intermediate = default!;
									await f(trans, state!).ConfigureAwait(false);
									break;
								}
								case Func<IFdbTransaction, TState, TIntermediate> f:
								{
									intermediate = f(trans, state!);
									break;
								}
								case Func<IFdbTransaction, TState, TResult> f:
								{
									intermediate = default!;
									result = f(trans, state!);
									hasResult = true;
									break;
								}
								case Action<IFdbTransaction, TState> a:
								{
									intermediate = default!;
									a(trans, state!);
									break;
								}

#endregion

								#endregion

								default:
								{
									throw new NotSupportedException($"Cannot execute handlers of type {handler.GetType().Name}");
								}
							}

							if (currentActivity != null)
							{
								currentActivity.Dispose();
								currentActivity = null;
								context.Activity = mainActivity;
							}

							if (context.Abort)
							{
								break;
							}

							// make sure any pending value checks are completed and match the expected value!
							context.FailedValueCheckTags?.Clear();
							hasRunValueChecks = true;
							if (context.HasPendingValueChecks(out var valueChecks))
							{
								currentActivity = tracingOptions.HasFlag(FdbTracingOptions.RecordSteps) ? FdbClientInstrumentation.ActivitySource.StartActivity("FDB Value Checks") : null;
								if (currentActivity != null)
								{
									context.Activity = currentActivity;
									if (currentActivity.IsAllDataRequested)
									{
										currentActivity.SetTag("db.system", "fdb");
										currentActivity.SetTag("db.fdb.trans.id", trans.Id);
										currentActivity.SetTag("db.fdb.checks.count", valueChecks.Count);
										if (context.Retries > 0) currentActivity.SetTag("db.fdb.error.retry_count", context.Retries);
										if (context.PreviousError != FdbError.Success) currentActivity.SetTag("db.fdb.error.previous", context.PreviousError);
									}
								}

								if (!await context.ValidateValueChecksSlow(valueChecks, ignoreFailedTasks: false).ConfigureAwait(false))
								{
									if (currentActivity != null)
									{
										using (currentActivity)
										{
											currentActivity.SetStatus(ActivityStatusCode.Error, "Value checks failed");
										}
										currentActivity = null;
										context.Activity = mainActivity;
									}
									throw FailValueCheck();
								}

								if (currentActivity != null)
								{
									currentActivity.Dispose();
									currentActivity = null;
									context.Activity = mainActivity;
								}
							}

							if (!trans.IsReadOnly)
							{ // commit the transaction

								currentActivity = tracingOptions.HasFlag(FdbTracingOptions.RecordSteps) ? FdbClientInstrumentation.ActivitySource.StartActivity("FDB Commit") : null;
								if (currentActivity != null)
								{
									context.Activity = currentActivity;
									if (currentActivity.IsAllDataRequested)
									{
										currentActivity.SetTag("db.system", "fdb");
										currentActivity.SetTag("db.fdb.trans.id", trans.Id);
										currentActivity.SetTag("db.fdb.trans.size", trans.Size); // estimated!
										if (context.Retries > 0) currentActivity.SetTag("db.fdb.error.retry_count", context.Retries);
										if (trans is FdbTransaction fdbTrans && fdbTrans.TryGetCachedReadVersion(out var rv))
										{
											currentActivity.SetTag("db.fdb.trans.read_version", rv);
										}
									}
								}

								//TODO: maybe measure the time taken to commit?
								await trans.CommitAsync().ConfigureAwait(false);

								if (currentActivity != null)
								{
									using (currentActivity)
									{
										currentActivity.SetTag("db.fdb.trans.commit_version", trans.GetCommittedVersion());
									}
									currentActivity = null;
									context.Activity = mainActivity;
								}

								FdbClientInstrumentation.ReportOperationCommitted(trans, context);
							}

							// we are done
							context.Committed = true;
							context.PreviousError = FdbError.Success;

							// execute any state callbacks, if there are any
							if (context.StateCallbacks != null)
							{
								await context.ExecuteHandlers(ref context.StateCallbacks, context, trans.IsReadOnly ? FdbTransactionState.Completed : FdbTransactionState.Commit).ConfigureAwait(false);
							}

							// execute any final logic, if there is any
							if (success != null)
							{
								//TODO: instrument success handlers with ActivitySource?

								// if TIntermediate == TResult, the order of delegate resolution may be unstable
								// => we will copy the result in both fields!
								if (hasResult && typeof(TIntermediate) == typeof(TResult))
								{
									intermediate = (TIntermediate) (object) result!;
								}

								switch (success)
								{
									#region Read Only...

									case Func<IFdbReadOnlyTransaction, TIntermediate, Task<TResult>> f:
									{
										result = await f(trans, intermediate!).ConfigureAwait(false);
										hasResult = true;
										break;
									}
									case Func<IFdbReadOnlyTransaction, TIntermediate, ValueTask<TResult>> f:
									{
										result = await f(trans, intermediate!).ConfigureAwait(false);
										hasResult = true;
										break;
									}
									case Func<IFdbReadOnlyTransaction, TResult, Task> f:
									{
										if (!hasResult) throw new ArgumentException("Success handler requires the result to be computed by the loop handler.", nameof(success));
										await f(trans, result).ConfigureAwait(false);
										break;
									}
									case Func<IFdbReadOnlyTransaction, TResult, ValueTask> f:
									{
										if (!hasResult) throw new ArgumentException("Success handler requires the result to be computed by the loop handler.", nameof(success));
										await f(trans, result).ConfigureAwait(false);
										break;
									}
									case Func<IFdbReadOnlyTransaction, TIntermediate, TResult> f:
									{
										result = f(trans, intermediate!);
										hasResult = true;
										break;
									}
									case Func<IFdbReadOnlyTransaction, Task<TResult>> f:
									{
										result = await f(trans).ConfigureAwait(false);
										hasResult = true;
										break;
									}
									case Func<IFdbReadOnlyTransaction, ValueTask<TResult>> f:
									{
										result = await f(trans).ConfigureAwait(false);
										hasResult = true;
										break;
									}
									case Func<IFdbReadOnlyTransaction, Task> f:
									{
										await f(trans).ConfigureAwait(false);
										result = default!;
										hasResult = true;
										break;
									}
									case Func<IFdbReadOnlyTransaction, ValueTask> f:
									{
										await f(trans).ConfigureAwait(false);
										result = default!;
										hasResult = true;
										break;
									}
									case Func<IFdbReadOnlyTransaction, TResult> f:
									{
										result = f(trans);
										hasResult = true;
										break;
									}
									case Action<IFdbReadOnlyTransaction, TResult> a:
									{
										if (!hasResult) throw new ArgumentException("Success handler requires the result to be computed by the loop handler.", nameof(success));
										a(trans, result);
										break;
									}
									case Action<IFdbReadOnlyTransaction> a:
									{
										a(trans);
										break;
									}

									#endregion

									#region Read/Write...

									case Func<IFdbTransaction, TIntermediate, Task<TResult>> f:
									{
										result = await f(trans, intermediate!).ConfigureAwait(false);
										hasResult = true;
										break;
									}
									case Func<IFdbTransaction, TIntermediate, ValueTask<TResult>> f:
									{
										result = await f(trans, intermediate!).ConfigureAwait(false);
										hasResult = true;
										break;
									}
									case Func<IFdbTransaction, TResult, Task> f:
									{
										if (!hasResult) throw new ArgumentException("Success handler requires the result to be computed by the loop handler.", nameof(success));
										await f(trans, result).ConfigureAwait(false);
										break;
									}
									case Func<IFdbTransaction, TResult, ValueTask> f:
									{
										if (!hasResult) throw new ArgumentException("Success handler requires the result to be computed by the loop handler.", nameof(success));
										await f(trans, result).ConfigureAwait(false);
										break;
									}
									case Func<IFdbTransaction, TIntermediate, TResult> f:
									{
										result = f(trans, intermediate!);
										hasResult = true;
										break;
									}
									case Func<IFdbTransaction, Task<TResult>> f:
									{
										result = await f(trans).ConfigureAwait(false);
										hasResult = true;
										break;
									}
									case Func<IFdbTransaction, ValueTask<TResult>> f:
									{
										result = await f(trans).ConfigureAwait(false);
										hasResult = true;
										break;
									}
									case Func<IFdbTransaction, Task> f:
									{
										await f(trans).ConfigureAwait(false);
										result = default!;
										hasResult = true;
										break;
									}
									case Func<IFdbTransaction, ValueTask> f:
									{
										await f(trans).ConfigureAwait(false);
										result = default!;
										hasResult = true;
										break;
									}
									case Func<IFdbTransaction, TResult> f:
									{
										result = f(trans);
										hasResult = true;
										break;
									}
									case Action<IFdbTransaction, TResult> a:
									{
										if (!hasResult) throw new ArgumentException("Success handler requires the result to be computed by the loop handler.", nameof(success));
										a(trans, result);
										break;
									}
									case Action<IFdbTransaction> a:
									{
										a(trans);
										break;
									}

									#endregion

									default:
									{
										throw new NotSupportedException($"Cannot execute completion handler of type {handler.GetType().Name}");
									}
								}
							}

							if (!hasResult)
							{
								if (typeof(TResult) == typeof(TIntermediate))
								{
									result = (TResult) (object) intermediate!;
								}
								else
								{
									throw new ArgumentException($"Success handler is required to convert intermediate type {typeof(TIntermediate).Name} into result type {typeof(TResult).Name}.");
								}
							}

						}
						catch (FdbException e)
						{
							if (currentActivity != null)
							{
								using (currentActivity)
								{
									currentActivity.SetStatus(ActivityStatusCode.Error, e.Message);
									currentActivity.SetTag("db.fdb.error.code", e.Code);
								}
								context.Activity = mainActivity;
							}
							if (attemptActivity != null)
							{
								attemptActivity.SetStatus(ActivityStatusCode.Error, e.Message);
								attemptActivity.SetTag("db.fdb.error.code", e.Code);
							}

							context.PreviousError = e.Code;

							// execute any state callbacks, if there are any
							if (context.StateCallbacks != null)
							{
								await context.ExecuteHandlers(ref context.StateCallbacks, context, FdbTransactionState.Faulted).ConfigureAwait(false);
							}

							if (Logging.On && Logging.IsVerbose) Logging.Verbose(string.Format(CultureInfo.InvariantCulture, "fdb: transaction {0} failed with error code {1}", trans.Id, e.Code));

							if (!hasRunValueChecks)
							{ // we need to resolve any check that would still have passed before the error

								context.FailedValueCheckTags?.Clear();
								if (!await context.ValidateValueChecks(ignoreFailedTasks: true).ConfigureAwait(false))
								{
									// we don't override the original error, though!
								}
							}

							bool shouldRethrow = false;
							try
							{
								await trans.OnErrorAsync(e.Code).ConfigureAwait(false);
							}
							catch (FdbException e2)
							{
								if (Logging.On && Logging.IsError) Logging.Error(string.Format(CultureInfo.InvariantCulture, "fdb: transaction {0} failed with un-retryable error code {1}", trans.Id, e.Code));
								// if the code is the same, we prefer re-throwing the original exception to keep the stacktrace intact!
								if (e2.Code != e.Code)
								{
									Contract.Debug.Assert(reportOpStarted);
									reportOpStarted = false;
									FdbClientInstrumentation.ReportOperationCompleted(context, trans, e2.Code);
									throw;
								}
								shouldRethrow = true;
							}

							// re-throw original exception because it is not retryable.
							if (shouldRethrow)
							{
								Contract.Debug.Assert(reportOpStarted);
								reportOpStarted = false;
								FdbClientInstrumentation.ReportOperationCompleted(context, trans, context.PreviousError);
								throw;
							}

							if (Logging.On && Logging.IsVerbose) Logging.Verbose(string.Format(CultureInfo.InvariantCulture, "fdb: transaction {0} can be safely retried", trans.Id));
						}
						catch (Exception e)
						{
							if (currentActivity != null)
							{
								using (currentActivity)
								{
									currentActivity.SetStatus(ActivityStatusCode.Error, e.Message);
									currentActivity.SetTag("db.fdb.error.code", FdbError.UnknownError);
								}
								context.Activity = mainActivity;
							}
							if (attemptActivity != null)
							{
								attemptActivity.SetStatus(ActivityStatusCode.Error, e.Message);
								attemptActivity.SetTag("db.fdb.error.code", FdbError.UnknownError);
							}

							context.PreviousError = FdbError.UnknownError;
							if (context.Transaction?.IsLogged() == true) context.Transaction.Annotate($"Handler failed with error: [{e.GetType().Name}] {e.Message}");

							// execute any state callbacks, if there are any
							if (context.StateCallbacks != null)
							{
								await context.ExecuteHandlers(ref context.StateCallbacks, context, FdbTransactionState.Faulted).ConfigureAwait(false);
							}

							// if we have pending value-checks, we have to run them !
							// => the exception thrown by the handler may be caused because of an invalid assumption on the value of a key,
							// and we HAVE to run the handler again, to give a change to the layer code to realize that it needs to update any cached data
							//TODO: REVIEW: how can we distinguish between errors that are caused by the bad assumption of a failed value check, and errors that are completely unrelated?

							bool shouldThrow = true;
							if (!hasRunValueChecks && !context.Cancellation.IsCancellationRequested)
							{
								context.FailedValueCheckTags?.Clear();
								if (!await context.ValidateValueChecks(ignoreFailedTasks: false).ConfigureAwait(false))
								{
									context.PreviousError = FdbError.NotCommitted;
									try
									{
										await trans.OnErrorAsync(FdbError.NotCommitted).ConfigureAwait(false);
									}
									catch (FdbException e2)
									{
										if (context.Transaction?.IsLogged() == true) context.Transaction.Annotate("Handler failure MAY be due to a failed value-check, but no more attempt is possible!");
										if (e2.Code != FdbError.NotCommitted)
										{
											Contract.Debug.Assert(reportOpStarted);
											reportOpStarted = false;
											FdbClientInstrumentation.ReportOperationCompleted(context, trans, e2.Code);
											throw;
										}
									}

									shouldThrow = false;
									// note: technically we are after the "OnError" so any new comment will be seen as part of the next attempt...
									if (context.Transaction?.IsLogged() == true)
									{
										context.Transaction.Annotate($"Previous attempt failed because of the following failed value-check(s): {string.Join(", ", context.FailedValueCheckTags?.Where(x => x.Value.Result == FdbValueCheckResult.Failed).Select(x => x.Key) ?? Array.Empty<string>())}");
									}
								}
							}

							if (shouldThrow)
							{
								Contract.Debug.Assert(reportOpStarted);
								reportOpStarted = false;
								FdbClientInstrumentation.ReportOperationCompleted(context, trans, context.PreviousError);
								throw;
							}
						}

						Contract.Debug.Assert(reportOpStarted);
						reportOpStarted = false;
						FdbClientInstrumentation.ReportOperationCompleted(context, trans, context.PreviousError);

						// update the base time for the next attempt
						context.BaseDuration = context.Clock.Elapsed;
						context.Retries++;
					}
				}

				if (context.Cancellation.IsCancellationRequested)
				{
					context.Cancellation.ThrowIfCancellationRequested();
				}

				if (context.Abort)
				{
					mainActivity?.SetStatus(ActivityStatusCode.Error, "Transaction was aborted");

					// execute any state callbacks, if there are any
					if (context.StateCallbacks != null)
					{
						await context.ExecuteHandlers(ref context.StateCallbacks, context, FdbTransactionState.Aborted).ConfigureAwait(false);
					}

					throw new OperationCanceledException(context.Cancellation);
				}

				return result!;

			}
			catch (Exception e)
			{
				//TODO: REVIEW: in order to call "AddException(...)" we need a ref to package OpenTelemetry.API !
				mainActivity?.SetStatus(ActivityStatusCode.Error, e.Message);
				throw;
			}
			finally
			{
				if (reportOpStarted)
				{
					FdbClientInstrumentation.ReportOperationCompleted(context, null, context.PreviousError);
				}

				if (reportTransStarted)
				{
					FdbClientInstrumentation.ReportTransactionStop(context);
				}

				if (context.BaseDuration.TotalSeconds >= 10)
				{
					//REVIEW: this may not be a good idea to spam the logs with long-running transactions??
					if (Logging.On) Logging.Info(string.Format(CultureInfo.InvariantCulture, "fdb WARNING: long transaction ({0:N1} sec elapsed in transaction lambda function ({1} retries, {2})", context.BaseDuration.TotalSeconds, context.Retries, context.Committed ? "committed" : "not committed"));
				}
				context.Dispose();
			}
		}

		internal void AttachTransaction(FdbTransaction trans)
		{
			if (Interlocked.CompareExchange(ref this.Transaction, trans, null) != null)
			{
				throw new InvalidOperationException("Cannot attach another transaction to this context because another transaction is still attache");
			}
		}

		internal void ReleaseTransaction(FdbTransaction trans)
		{
			// only if this is still the current one!
			Interlocked.CompareExchange(ref this.Transaction, null, trans);
		}

		/// <summary>Return the underlying native handler for this transaction</summary>
		/// <remarks>This is only intended for testing or troubleshooting purpose!</remarks>
		public IFdbTransactionHandler GetTransactionHandler() => this.Transaction?.Handler ?? throw new InvalidOperationException("Transaction has already been disposed");

		/// <summary>Return the currently enforced API version for the database attached to this transaction.</summary>
		public int GetApiVersion() => m_db.GetApiVersion();

		/// <inheritdoc />
		public void Dispose()
		{
			this.Abort = true;
			var cts = this.TokenSource;
			if (cts != null)
			{
				using(cts)
				{
					if (!cts.IsCancellationRequested)
					{
						try { cts.Cancel(); } catch(ObjectDisposedException) { }
					}
				}
			}
		}

	}
 
	/// <summary>Marker interface for objects that need to respond to transaction state change (commit, failure, ...)</summary>
	public interface IHandleTransactionLifecycle
	{
		/// <summary>Called when the transaction state changes (reset, commit, rollback, ...)</summary>
		/// <param name="ctx">Context that changed state</param>
		/// <param name="state">New state</param>
		void OnTransactionStateChanged(FdbOperationContext ctx, FdbTransactionState state);

	}

	/// <summary>Current state of a transaction</summary>
	public enum FdbTransactionState
	{
		/// <summary>The last execution of the handler failed</summary>
		Faulted,
		/// <summary>The last execution of the handler successfully executed the read-only transaction</summary>
		Completed,
		/// <summary>The last execution of the handler successfully committed the transaction</summary>
		Commit,
		/// <summary>The last execution of the handler was aborted</summary>
		Aborted,
	}

	/// <summary>Result of a deferred value-check</summary>
	public enum FdbValueCheckResult
	{
		/// <summary>There was no value-check performed with this tag in the previous attempt.</summary>
		Unknown = 0,

		/// <summary>All value-checks performed with this tag passed in the previous attempt.</summary>
		Success = 1,

		/// <summary>At least one value-check performed with this tag failed in the previous attempt.</summary>
		Failed = 2,
	}

}
