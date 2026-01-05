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

	internal sealed class FdbDatabaseTombstoneProvider<TState> : IFdbDatabaseScopeProvider<TState>
	{

		public FdbDatabaseTombstoneProvider(IFdbDatabaseScopeProvider? parent, Exception error, CancellationToken lifetime)
		{
			Contract.Debug.Requires(error != null);
			this.Parent = parent;
			this.Error = error;
			this.Lifetime = parent != null ? CancellationTokenSource.CreateLinkedTokenSource(parent.Cancellation, lifetime) : CancellationTokenSource.CreateLinkedTokenSource(lifetime);
		}

		/// <inheritdoc />
		public IFdbDatabaseScopeProvider? Parent { get; }

		public Exception Error { get; private set; }

		public TState? GetState() => default;

		private CancellationTokenSource Lifetime { get; }

		/// <inheritdoc />
		public CancellationToken Cancellation => this.Lifetime.Token;

		private bool m_disposed;

		public void Dispose()
		{
			if (!m_disposed)
			{
				m_disposed = true;
				try
				{
					this.Lifetime.Cancel();
				}
				finally
				{
					this.Lifetime.Dispose();
					this.Error = new ObjectDisposedException(this.GetType().Name);
				}
			}
		}

		/// <inheritdoc />
		public FdbDirectorySubspaceLocation Root
		{
			get
			{
				if (m_disposed) throw ThrowHelper.ObjectDisposedException(this);
				throw new InvalidOperationException("Database provider has failed.", this.Error);
			}
		}

		/// <inheritdoc />
		public ValueTask<IFdbDatabase> GetDatabase(CancellationToken ct)
		{
			return new ValueTask<IFdbDatabase>(
				ct.IsCancellationRequested ? Task.FromCanceled<IFdbDatabase>(ct)
				: m_disposed ? Task.FromException<IFdbDatabase>(ThrowHelper.ObjectDisposedException(this))
				: Task.FromException<IFdbDatabase>(this.Error)
			);
		}

		/// <inheritdoc />
		public bool TryGetDatabase([MaybeNullWhen(false)] out IFdbDatabase db)
		{
			db = null;
			return false;
		}

		/// <inheritdoc />
		public ValueTask<TState?> GetState(IFdbReadOnlyTransaction tr)
		{
			return new ValueTask<TState?>(
				tr.Cancellation.IsCancellationRequested ? Task.FromCanceled<TState?>(tr.Cancellation)
				: m_disposed ? Task.FromException<TState?>(ThrowHelper.ObjectDisposedException(this))
				: Task.FromException<TState?>(this.Error)
			);
		}

		/// <inheritdoc />
		public bool TryGetState(IFdbReadOnlyTransaction tr, out TState? state)
		{
			state = default;
			return false;
		}

		/// <inheritdoc />
		public ValueTask<(IFdbDatabase Database, TState? State)> GetDatabaseAndState(CancellationToken ct)
		{
			return new ValueTask<(IFdbDatabase, TState?)>(
				ct.IsCancellationRequested ? Task.FromCanceled<(IFdbDatabase, TState?)>(ct)
				: m_disposed ? Task.FromException<(IFdbDatabase, TState?)>(ThrowHelper.ObjectDisposedException(this))
				: Task.FromException<(IFdbDatabase, TState?)>(this.Error)
			);
		}

		/// <inheritdoc />
		public bool TryGetDatabaseAndState([MaybeNullWhen(false)] out IFdbDatabase db, out TState? state)
		{
			db = null;
			state = default;
			return false;
		}

		/// <inheritdoc />
		public IFdbDatabaseScopeProvider<TNewState> CreateScope<TNewState>(Func<IFdbDatabase, CancellationToken, Task<(IFdbDatabase Db, TNewState State)>> start, CancellationToken lifetime = default)
		{
			// poison all child scopes with the same error
			return new FdbDatabaseTombstoneProvider<TNewState>(this, this.Error, lifetime);
		}

		/// <inheritdoc />
		public bool IsAvailable => false;

	}

}
