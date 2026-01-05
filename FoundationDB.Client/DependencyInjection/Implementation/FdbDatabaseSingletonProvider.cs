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

	/// <summary>Default implementation of a scope provider that uses a pre-initialized database instance</summary>
	/// <typeparam name="TState">Type of the state that will be passed to consumers of this type</typeparam>
	public sealed class FdbDatabaseSingletonProvider<TState> : IFdbDatabaseScopeProvider<TState>
	{

		public FdbDatabaseSingletonProvider(IFdbDatabase db, TState? state, CancellationTokenSource lifetime)
		{
			Contract.Debug.Requires(db != null && lifetime != null);
			this.Lifetime = lifetime;
			Volatile.Write(ref this.InternalState, new Scope(db, state));
			Interlocked.MemoryBarrier();
		}

		private sealed class Scope
		{
			public readonly IFdbDatabase Db;

			public readonly TState? State;

			public Scope(IFdbDatabase db, TState? state)
			{
				this.Db = db;
				this.State = state;
			}
		}

		private Scope? InternalState;

		private CancellationTokenSource Lifetime { get; }

		public void Dispose()
		{
			if (Interlocked.Exchange(ref this.InternalState, null) != null)
			{
				this.Lifetime.Cancel();
			}
		}

		/// <inheritdoc />
		public IFdbDatabaseScopeProvider? Parent => null;

		/// <inheritdoc />
		public bool IsAvailable => Volatile.Read(ref this.InternalState) != null;

		/// <inheritdoc />
		public CancellationToken Cancellation => this.Lifetime.Token;

		/// <inheritdoc />
		public FdbDirectorySubspaceLocation Root => EnsureReady().Db.Root;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private Scope EnsureReady()
		{
			var scope = Volatile.Read(ref this.InternalState);
			if (scope == null) throw ThrowHelper.ObjectDisposedException(this);
			return scope;
		}

		/// <inheritdoc />
		public ValueTask<IFdbDatabase> GetDatabase(CancellationToken ct = default)
		{
			var scope = EnsureReady();
			return new ValueTask<IFdbDatabase>(scope.Db);
		}

		/// <inheritdoc />
		public bool TryGetDatabase([MaybeNullWhen(false)] out IFdbDatabase db)
		{
			var scope = Volatile.Read(ref this.InternalState);
			if (scope != null)
			{
				db = scope.Db;
				return true;
			}

			db = null;
			return false;
		}

		/// <inheritdoc />
		public ValueTask<TState?> GetState(IFdbReadOnlyTransaction tr)
		{
			tr.Cancellation.ThrowIfCancellationRequested();
			var scope = EnsureReady();
			return new ValueTask<TState?>(scope.State);
		}

		/// <inheritdoc />
		public bool TryGetState(IFdbReadOnlyTransaction tr, out TState? state)
		{
			var scope = Volatile.Read(ref this.InternalState);
			if (scope != null && !tr.Cancellation.IsCancellationRequested)
			{
				state = scope.State;
				return true;
			}

			state = default;
			return false;
		}

		/// <inheritdoc />
		public ValueTask<(IFdbDatabase Database, TState? State)> GetDatabaseAndState(CancellationToken ct = default)
		{
			var scope = EnsureReady();
			return new ValueTask<(IFdbDatabase, TState?)>((scope.Db, scope.State));
		}

		/// <inheritdoc />
		public bool TryGetDatabaseAndState([MaybeNullWhen(false)] out IFdbDatabase db, out TState? state)
		{
			var scope = Volatile.Read(ref this.InternalState);
			if (scope != null)
			{
				db = scope.Db;
				state = scope.State;
				return true;
			}

			db = null;
			state = default;
			return false;
		}

		/// <inheritdoc />
		public IFdbDatabaseScopeProvider<TNewState> CreateScope<TNewState>(Func<IFdbDatabase, CancellationToken, Task<(IFdbDatabase Db, TNewState State)>> start, CancellationToken lifetime = default)
		{
			_ = EnsureReady();
			return new FdbDatabaseScopeProvider<TNewState>(this, start, lifetime);
		}

	}

}
