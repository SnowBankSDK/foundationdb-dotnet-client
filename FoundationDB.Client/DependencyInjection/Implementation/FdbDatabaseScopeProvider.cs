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

namespace FoundationDB.DependencyInjection
{
	using FoundationDB.Client;

	/// <summary>Default implementation of a child database scope provider</summary>
	/// <typeparam name="TState">Type of the State created by the init handler of this scope</typeparam>
	internal sealed class FdbDatabaseScopeProvider<TState> : IFdbDatabaseScopeProvider<TState>
	{

		public FdbDatabaseScopeProvider(IFdbDatabaseScopeProvider parent, Func<IFdbDatabase, CancellationToken, Task<(IFdbDatabase, TState)>> handler, CancellationToken lifetime = default)
		{
			Contract.NotNull(parent);
			Contract.NotNull(handler);

			this.Parent = parent;
			this.Handler = handler;
			this.LifeTime = lifetime == default || lifetime == parent.Cancellation
				? CancellationTokenSource.CreateLinkedTokenSource(parent.Cancellation)
				: CancellationTokenSource.CreateLinkedTokenSource(parent.Cancellation, lifetime);
			this.DbTask = new Lazy<Task>(this.InitAsync, LazyThreadSafetyMode.ExecutionAndPublication);
			this.State = default;
		}

		/// <inheritdoc />
		public IFdbDatabaseScopeProvider Parent { get; }

		public Func<IFdbDatabase, CancellationToken, Task<(IFdbDatabase, TState)>> Handler { get; }

		private readonly Lazy<Task> DbTask;

		private readonly ReaderWriterLockSlim Lock = new();

		private IFdbDatabase? Db { get; set; }

		private TState? State { get; set; }

		private Exception? Error { get; set; }

		private CancellationTokenSource LifeTime { get; }

		/// <inheritdoc />
		public CancellationToken Cancellation => this.LifeTime.Token;

		/// <inheritdoc />
		public FdbDirectorySubspaceLocation Root => this.Parent.Root;

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void UpdateInternalState(IFdbDatabase? db, TState? state, Exception? error)
		{
			this.Lock.EnterWriteLock();
			try
			{
				this.State = state;
				this.Db = db;
				this.Error = error;
			}
			finally
			{
				this.Lock.ExitWriteLock();
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private (IFdbDatabase? Database, TState? State, Exception? error) ReadInternalState()
		{
			this.Lock.EnterReadLock();
			try
			{
				return (this.Db, this.State, this.Error);
			}
			finally
			{
				this.Lock.ExitReadLock();
			}
		}

		private async Task InitAsync()
		{
			var ct = this.LifeTime.Token;
			try
			{
				var db = await this.Parent.GetDatabase(ct).ConfigureAwait(false);
				ct.ThrowIfCancellationRequested();
				(db, var state) = await this.Handler(db, ct).ConfigureAwait(false);
				Contract.Debug.Assert(db != null);
				UpdateInternalState(db, state, null);
			}
			catch (Exception e)
			{
				//TODO: should be do something if the state implements IDisposable or IAsyncDisposable?
				UpdateInternalState(null, default, e);
				Interlocked.MemoryBarrier();
				throw;
			}
		}

		/// <inheritdoc />
		public IFdbDatabaseScopeProvider<TNewState> CreateScope<TNewState>(Func<IFdbDatabase, CancellationToken, Task<(IFdbDatabase, TNewState)>> start, CancellationToken lifetime = default)
		{
			Contract.NotNull(start);
			//REVIEW: should we instantly failed if lifetime is already disposed?
			return new FdbDatabaseScopeProvider<TNewState>(this, start, lifetime);
		}

		/// <inheritdoc />
		public bool IsAvailable => this.DbTask.IsValueCreated && this.DbTask.Value.Status == TaskStatus.RanToCompletion;

		private async ValueTask<(IFdbDatabase? Database, TState? state, Exception? error)> EnsureInitialized(CancellationToken ct)
		{
			if (this.LifeTime.IsCancellationRequested) throw ThrowHelper.ObjectDisposedException(this);
			var t = this.DbTask.Value;

			if (!t.IsCompleted && ct.CanBeCanceled)
			{
				//REVIEW: is there a faster way to do this? (we want to make sure not to leak tons of Task.Delay(...) that will linger on until the original ct is triggered
				using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
				{
					await Task.WhenAny(t, Task.Delay(Timeout.InfiniteTimeSpan, cts.Token)).ConfigureAwait(false);
					ct.ThrowIfCancellationRequested();
				}
			}
			await t.ConfigureAwait(false);

			return ReadInternalState();
		}

		/// <inheritdoc />
		public ValueTask<(IFdbDatabase Database, TState? State)> GetDatabaseAndState(CancellationToken ct = default)
		{
			//BUGBUG: what if the parent scope has been shut down?
			var t = ReadInternalState();
			return t.Database != null && !this.LifeTime.IsCancellationRequested
				? new ValueTask<(IFdbDatabase, TState?)>((t.Database, t.State))
				: GetDatabaseAndStateSlow(this, ct);

			static async ValueTask<(IFdbDatabase, TState?)> GetDatabaseAndStateSlow(FdbDatabaseScopeProvider<TState> provider, CancellationToken ct)
			{
				var (db, state, _) = await provider.EnsureInitialized(ct).ConfigureAwait(false);
				Contract.Debug.Assert(db != null);
				return (db!, state);
			}
		}

		/// <inheritdoc />
		public bool TryGetDatabaseAndState([MaybeNullWhen(false)] out IFdbDatabase db, out TState? state)
		{
			var t = ReadInternalState();

			if (t.Database != null && !this.LifeTime.IsCancellationRequested)
			{
				db = t.Database;
				state = t.State;
				return true;
			}

			db = null;
			state = default;
			state = default;
			return false;
		}

		/// <inheritdoc />
		public ValueTask<IFdbDatabase> GetDatabase(CancellationToken ct = default)
		{
			//BUGBUG: what if the parent scope has been shut down?
			var (db, _, _) = ReadInternalState();
			return db != null && !this.LifeTime.IsCancellationRequested
				? new ValueTask<IFdbDatabase>(db)
				: GetDatabaseSlow(this, ct);

			static async ValueTask<IFdbDatabase> GetDatabaseSlow(FdbDatabaseScopeProvider<TState> provider, CancellationToken ct)
			{
				var (db, _, _) = await provider.EnsureInitialized(ct).ConfigureAwait(false);
				Contract.Debug.Assert(db != null);
				return db;
			}
		}

		/// <inheritdoc />
		public bool TryGetDatabase([MaybeNullWhen(false)] out IFdbDatabase db)
		{
			var state = ReadInternalState();
			if (state.Database != null && !this.LifeTime.IsCancellationRequested)
			{
				db = state.Database;
				return true;
			}
			db = null;
			return false;
		}

		/// <inheritdoc />
		public ValueTask<TState?> GetState(IFdbReadOnlyTransaction tr)
		{
			tr.Cancellation.ThrowIfCancellationRequested();
			var (db, state, _) = ReadInternalState();
			return db != null && !this.LifeTime.IsCancellationRequested
				? new ValueTask<TState?>(state)
				: GetStateSlow(this, tr);

			static async ValueTask<TState?> GetStateSlow(FdbDatabaseScopeProvider<TState> provider, IFdbReadOnlyTransaction tr)
			{
				var (_, state, _) = await provider.EnsureInitialized(tr.Cancellation).ConfigureAwait(false);
				return state;
			}
		}

		/// <inheritdoc />
		public bool TryGetState(IFdbReadOnlyTransaction tr, out TState? state)
		{
			var t = ReadInternalState();
			if (t.Database != null && !tr.Cancellation.IsCancellationRequested)
			{
				state = t.State;
				return true;
			}

			state = default;
			return false;
		}

		public void Dispose()
		{
			using (this.LifeTime)
			{
				this.LifeTime.Cancel();
			}
		}

	}

}
