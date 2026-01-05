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

namespace SnowBank.Linq.Async.Iterators
{
	using System.Buffers;
	using System.Collections.Immutable;
	using SnowBank.Buffers;
	using SnowBank.Linq.Async.Expressions;

	/// <summary>Base class for all async iterators</summary>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	[PublicAPI]
	public abstract class AsyncLinqIterator<TResult> : IAsyncLinqQuery<TResult>, IAsyncEnumerable<TResult>, IAsyncEnumerator<TResult>
	{
		//REVIEW: we could need an IAsyncIterator<T> interface that holds all the Select(),Where(),Take(),... so that it can be used by AsyncEnumerable to either call them directly (if the query supports it) or use a generic implementation
		// => this would be implemented by AsyncIterator<T> as well as FdbRangeQuery<T> (and ony other 'self optimizing' class)

		private const int STATE_SEQ = 0;
		private const int STATE_INIT = 1;
		private const int STATE_ITERATING = 2;
		private const int STATE_COMPLETED = 3;
		private const int STATE_DISPOSED = -1;

		protected TResult? m_current;
		protected int m_state;
		protected AsyncIterationHint m_mode;

		#region IAsyncEnumerable<TResult>...

		public abstract CancellationToken Cancellation { get; }

		[MustDisposeResource]
		IAsyncEnumerator<TResult> IAsyncEnumerable<TResult>.GetAsyncEnumerator(CancellationToken ct)
			=> AsyncQuery.GetCancellableAsyncEnumerator(this, AsyncIterationHint.All, ct);

		[MustDisposeResource]
		IAsyncEnumerator<TResult> IAsyncQuery<TResult>.GetAsyncEnumerator(CancellationToken ct)
			=> AsyncQuery.GetCancellableAsyncEnumerator(this, AsyncIterationHint.All, ct);

		[MustDisposeResource]
		public IAsyncEnumerator<TResult> GetAsyncEnumerator(AsyncIterationHint mode)
		{
			this.Cancellation.ThrowIfCancellationRequested();

			// reuse the same instance the first time
			if (Interlocked.CompareExchange(ref m_state, STATE_INIT, STATE_SEQ) == STATE_SEQ)
			{
				m_mode = mode;
				return this;
			}

			// create a new one
			var iterator = Clone();
			iterator.m_mode = mode;
			Volatile.Write(ref iterator.m_state, STATE_INIT);
			return iterator;
		}

		protected abstract AsyncLinqIterator<TResult> Clone();

		#endregion

		#region IAsyncEnumerator<TResult>...

		public TResult Current
		{
			get
			{
				if (Volatile.Read(ref m_state) != STATE_ITERATING) EnsureIsIterating();
				return m_current!;
			}
		}

		public async ValueTask<bool> MoveNextAsync()
		{
			var state = Volatile.Read(ref m_state);

			if (state == STATE_COMPLETED)
			{
				return false;
			}
			if (state != STATE_INIT && state != STATE_ITERATING)
			{
				EnsureIsIterating();
				return false;
			}

			if (this.Cancellation.IsCancellationRequested)
			{
				return await Canceled().ConfigureAwait(false);
			}

			try
			{
				if (state == STATE_INIT)
				{
					if (!await OnFirstAsync().ConfigureAwait(false))
					{ // did not start at all ?
						return await Completed().ConfigureAwait(false);
					}

					if (Interlocked.CompareExchange(ref m_state, STATE_ITERATING, STATE_INIT) != STATE_INIT)
					{ // something happened while we were starting ?
						return false;
					}
				}

				return await OnNextAsync().ConfigureAwait(false);
			}
			catch (Exception)
			{
				await MarkAsFailed().ConfigureAwait(false);
				throw;
			}
		}

		#endregion

		#region LINQ...

		/// <inheritdoc />
		public virtual IAsyncEnumerable<TResult> ToAsyncEnumerable(AsyncIterationHint hint = AsyncIterationHint.Default)
		{
			return this;
		}

		#region To{Collection}Async...

		/// <inheritdoc />
		public virtual async Task<TResult[]> ToArrayAsync()
		{
			var buffer = new Buffer<TResult>(0, ArrayPool<TResult>.Shared);
			await foreach (var item in this)
			{
				buffer.Add(item);
			}
			return buffer.ToArrayAndClear();
		}

		/// <inheritdoc />
		public virtual async Task<List<TResult>> ToListAsync()
		{
			var buffer = new Buffer<TResult>(0, ArrayPool<TResult>.Shared);
			await foreach (var item in this)
			{
				buffer.Add(item);
			}
			return buffer.ToListAndClear();
		}

		/// <inheritdoc />
		public virtual async Task<ImmutableArray<TResult>> ToImmutableArrayAsync()
		{
			var buffer = new Buffer<TResult>(0, ArrayPool<TResult>.Shared);
			await foreach (var item in this)
			{
				buffer.Add(item);
			}
			return buffer.ToImmutableArrayAndClear();
		}

		/// <inheritdoc />
		public virtual Task<Dictionary<TKey, TResult>> ToDictionaryAsync<TKey>([InstantHandle] Func<TResult, TKey> keySelector, IEqualityComparer<TKey>? comparer = null)
			where TKey : notnull
		{
			return ToDictionaryAsync(keySelector, x => x, comparer);
		}

		/// <inheritdoc />
		public virtual async Task<Dictionary<TKey, TElement>> ToDictionaryAsync<TKey, TElement>([InstantHandle] Func<TResult, TKey> keySelector, [InstantHandle] Func<TResult, TElement> elementSelector, IEqualityComparer<TKey>? comparer = null)
			where TKey : notnull
		{

			var map = new Dictionary<TKey, TElement>(comparer);
			await foreach (var item in this)
			{
				map.Add(keySelector(item), elementSelector(item));
			}

			return map;
		}

		/// <inheritdoc />
		public virtual async Task<HashSet<TResult>> ToHashSetAsync(IEqualityComparer<TResult>? comparer = null)
		{
			var buffer = new Buffer<TResult>(0, ArrayPool<TResult>.Shared);
			await foreach (var item in this)
			{
				buffer.Add(item);
			}
			return buffer.ToHashSetAndClear(comparer);
		}

		#endregion

		#region CountAsync...

		public virtual Task<int> CountAsync()
		{
			return AsyncIterators.CountAsync(this);
		}

		public virtual Task<int> CountAsync(Func<TResult, bool> predicate)
		{
			Contract.NotNull(predicate);

			return AsyncIterators.CountAsync(this, predicate);
		}

		public virtual Task<int> CountAsync(Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(predicate);

			return AsyncIterators.CountAsync(this, predicate);
		}

		#endregion

		#region AnyAsync...

		/// <inheritdoc />
		public virtual Task<bool> AnyAsync()
		{
			return AsyncIterators.AnyAsync(this);
		}

		/// <inheritdoc />
		public virtual Task<bool> AnyAsync(Func<TResult, bool> predicate)
		{
			Contract.NotNull(predicate);

			return AsyncIterators.AnyAsync(this, predicate);
		}

		/// <inheritdoc />
		public virtual Task<bool> AnyAsync(Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(predicate);

			return AsyncIterators.AnyAsync(this, predicate);
		}

		#endregion

		#region AllAsync...

		/// <inheritdoc />
		public virtual Task<bool> AllAsync(Func<TResult, bool> predicate)
		{
			Contract.NotNull(predicate);

			return AsyncIterators.AllAsync(this, predicate);
		}

		/// <inheritdoc />
		public virtual Task<bool> AllAsync(Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(predicate);

			return AsyncIterators.AllAsync(this, predicate);
		}

		#endregion

		#region FirstOrDefaultAsync...

		/// <inheritdoc />
		public Task<TResult?> FirstOrDefaultAsync() => FirstOrDefaultAsync(default(TResult)!)!;

		/// <inheritdoc />
		public virtual Task<TResult> FirstOrDefaultAsync(TResult defaultValue)
		{
			return AsyncIterators.FirstOrDefaultAsync(this, defaultValue);
		}

		/// <inheritdoc />
		public Task<TResult?> FirstOrDefaultAsync(Func<TResult, bool> predicate) => FirstOrDefaultAsync(predicate, default(TResult)!)!;

		/// <inheritdoc />
		public virtual Task<TResult> FirstOrDefaultAsync(Func<TResult, bool> predicate, TResult defaultValue)
		{
			Contract.NotNull(predicate);

			return AsyncIterators.FirstOrDefaultAsync(this, predicate, defaultValue);
		}

		/// <inheritdoc />
		public Task<TResult?> FirstOrDefaultAsync(Func<TResult, CancellationToken, Task<bool>> predicate) => FirstOrDefaultAsync(predicate, default(TResult)!)!;

		/// <inheritdoc />
		public virtual Task<TResult> FirstOrDefaultAsync(Func<TResult, CancellationToken, Task<bool>> predicate, TResult defaultValue)
		{
			Contract.NotNull(predicate);

			return AsyncIterators.FirstOrDefaultAsync(this, predicate, defaultValue);
		}

		#endregion

		#region FirstAsync...

		/// <inheritdoc />
		public virtual Task<TResult> FirstAsync()
		{
			return AsyncIterators.FirstAsync(this);
		}

		/// <inheritdoc />
		public virtual Task<TResult> FirstAsync(Func<TResult, bool> predicate)
		{
			Contract.NotNull(predicate);

			return AsyncIterators.FirstAsync(this, predicate);
		}

		/// <inheritdoc />
		public virtual Task<TResult> FirstAsync(Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(predicate);

			return AsyncIterators.FirstAsync(this, predicate);
		}

		#endregion

		#region SingleOrDefaultAsync...

		/// <inheritdoc />
		public Task<TResult?> SingleOrDefaultAsync() => SingleOrDefaultAsync(default(TResult)!)!;

		/// <inheritdoc />
		public virtual Task<TResult> SingleOrDefaultAsync(TResult defaultValue)
		{
			return AsyncIterators.SingleOrDefaultAsync(this, defaultValue);
		}

		/// <inheritdoc />
		public Task<TResult?> SingleOrDefaultAsync(Func<TResult, bool> predicate) => SingleOrDefaultAsync(predicate, default(TResult)!)!;

		/// <inheritdoc />
		public virtual Task<TResult> SingleOrDefaultAsync(Func<TResult, bool> predicate, TResult defaultValue)
		{
			Contract.NotNull(predicate);

			return AsyncIterators.SingleOrDefaultAsync(this, predicate, defaultValue);
		}

		/// <inheritdoc />
		public Task<TResult?> SingleOrDefaultAsync(Func<TResult, CancellationToken, Task<bool>> predicate) => SingleOrDefaultAsync(predicate, default(TResult)!)!;

		/// <inheritdoc />
		public virtual Task<TResult> SingleOrDefaultAsync(Func<TResult, CancellationToken, Task<bool>> predicate, TResult defaultValue)
		{
			Contract.NotNull(predicate);

			return AsyncIterators.SingleOrDefaultAsync(this, predicate, defaultValue);
		}

		#endregion

		#region SingleAsync...

		/// <inheritdoc />
		public virtual Task<TResult> SingleAsync()
		{
			return AsyncIterators.SingleAsync(this);
		}

		/// <inheritdoc />
		public virtual Task<TResult> SingleAsync(Func<TResult, bool> predicate)
		{
			Contract.NotNull(predicate);

			return AsyncIterators.SingleAsync(this, predicate);
		}

		/// <inheritdoc />
		public virtual Task<TResult> SingleAsync(Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(predicate);

			return AsyncIterators.SingleAsync(this, predicate);
		}

		#endregion

		#region LastOrDefaultAsync...

		/// <inheritdoc />
		public Task<TResult?> LastOrDefaultAsync() => LastOrDefaultAsync(default(TResult)!)!;

		/// <inheritdoc />
		public virtual Task<TResult> LastOrDefaultAsync(TResult defaultValue)
		{
			return AsyncIterators.LastOrDefaultAsync(this, defaultValue);
		}

		/// <inheritdoc />
		public Task<TResult?> LastOrDefaultAsync(Func<TResult, bool> predicate) => LastOrDefaultAsync(predicate, default(TResult)!)!;

		/// <inheritdoc />
		public virtual Task<TResult> LastOrDefaultAsync(Func<TResult, bool> predicate, TResult defaultValue)
		{
			Contract.NotNull(predicate);

			return AsyncIterators.LastOrDefaultAsync(this, predicate, defaultValue);
		}

		/// <inheritdoc />
		public Task<TResult?> LastOrDefaultAsync(Func<TResult, CancellationToken, Task<bool>> predicate) => LastOrDefaultAsync(predicate, default(TResult)!)!;

		/// <inheritdoc />
		public virtual Task<TResult> LastOrDefaultAsync(Func<TResult, CancellationToken, Task<bool>> predicate, TResult defaultValue)
		{
			Contract.NotNull(predicate);

			return AsyncIterators.LastOrDefaultAsync(this, predicate, defaultValue);
		}

		#endregion

		#region LastAsync...

		/// <inheritdoc />
		public virtual Task<TResult> LastAsync()
		{
			return AsyncIterators.LastAsync(this);
		}

		/// <inheritdoc />
		public virtual Task<TResult> LastAsync(Func<TResult, bool> predicate)
		{
			Contract.NotNull(predicate);

			return AsyncIterators.LastAsync(this, predicate);
		}

		/// <inheritdoc />
		public virtual Task<TResult> LastAsync(Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(predicate);

			return AsyncIterators.LastAsync(this, predicate);
		}

		#endregion

		#region MinAsync/MaxAsync...

		/// <inheritdoc />
		public virtual Task<TResult?> MinAsync(IComparer<TResult>? comparer = null)
		{
			return AsyncIterators.MinAsync<TResult>(this, comparer ?? Comparer<TResult>.Default);
		}

		/// <inheritdoc />
		public virtual Task<TResult?> MaxAsync(IComparer<TResult>? comparer = null)
		{
			return AsyncIterators.MaxAsync<TResult>(this, comparer ?? Comparer<TResult>.Default);
		}

		#endregion

		#region SumAsync...

		public virtual Task<TResult> SumAsync()
		{
			return AsyncIterators.SumUnconstrainedAsync<TResult>(this);
		}

		#endregion

		#region Where...

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TResult> Where(Func<TResult, bool> predicate)
		{
			Contract.NotNull(predicate);

			return AsyncIterators.Where(this, predicate);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TResult> Where(Func<TResult, int, bool> predicate)
		{
			Contract.NotNull(predicate);

			return AsyncIterators.Where(this, predicate);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TResult> Where(Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(predicate);

			return AsyncIterators.Where(this, predicate);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TResult> Where(Func<TResult, int, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(predicate);

			return AsyncIterators.Where(this, predicate);
		}

		#endregion

		#region Select...

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TNew> Select<TNew>(Func<TResult, TNew> selector)
		{
			Contract.NotNull(selector);

			return AsyncIterators.Select(this, selector);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TNew> Select<TNew>(Func<TResult, int, TNew> selector)
		{
			Contract.NotNull(selector);

			return AsyncIterators.Select(this, selector);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TNew> Select<TNew>(Func<TResult, CancellationToken, Task<TNew>> selector)
		{
			Contract.NotNull(selector);

			return AsyncIterators.Select(this, selector);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TNew> Select<TNew>(Func<TResult, int, CancellationToken, Task<TNew>> selector)
		{
			Contract.NotNull(selector);

			return AsyncIterators.Select(this, selector);
		}

		#endregion

		#region SelectMany...

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<TResult, IEnumerable<TNew>> selector)
		{
			Contract.NotNull(selector);

			return AsyncIterators.SelectManyImpl(this, selector);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TNew>>> selector)
		{
			Contract.NotNull(selector);

			return AsyncIterators.SelectManyImpl(this, selector);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TNew> SelectMany<TCollection, TNew>(Func<TResult, IEnumerable<TCollection>> collectionSelector, Func<TResult, TCollection, TNew> resultSelector)
		{
			Contract.NotNull(collectionSelector);
			Contract.NotNull(resultSelector);

			return AsyncIterators.SelectManyImpl(this, collectionSelector, resultSelector);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TNew> SelectMany<TCollection, TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TCollection>>> collectionSelector, Func<TResult, TCollection, TNew> resultSelector)
		{
			Contract.NotNull(collectionSelector);
			Contract.NotNull(resultSelector);

			return AsyncIterators.SelectManyImpl(this, collectionSelector, resultSelector);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<TResult, IAsyncEnumerable<TNew>> selector)
		{
			Contract.NotNull(selector);

			return AsyncQuery.Flatten(this, new AsyncTransformExpression<TResult, IAsyncEnumerable<TNew>>(selector));
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<TResult, IAsyncQuery<TNew>> selector)
		{
			Contract.NotNull(selector);

			return AsyncQuery.Flatten(this, new AsyncTransformExpression<TResult, IAsyncQuery<TNew>>(selector));
		}

		#endregion

		#region Take/TakeWhile/Skip...

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TResult> Take(int count)
		{
			return AsyncIterators.Take(this, count);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TResult> Take(Range range)
		{
			return AsyncIterators.Take(this, range);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TResult> TakeWhile(Func<TResult, bool> condition)
		{
			return AsyncIterators.TakeWhile(this, condition);
		}

		/// <inheritdoc />
		public virtual IAsyncLinqQuery<TResult> Skip(int count)
		{
			return AsyncIterators.Skip(this, count);
		}

		#endregion

		/// <summary>Execute an action on the result of this async sequence</summary>
		public virtual Task ExecuteAsync(Action<TResult> handler)
		{
			return AsyncQuery.Run(this, AsyncIterationHint.All, handler);
		}

		/// <summary>Execute an action on the result of this async sequence</summary>
		public virtual Task ExecuteAsync<TState>(TState state, Action<TState, TResult> handler)
		{
			return AsyncQuery.Run(this, AsyncIterationHint.All, state, handler);
		}

		/// <summary>Execute an action on the result of this async sequence</summary>
		public virtual Task<TAggregate> ExecuteAsync<TAggregate>(TAggregate seed, Func<TAggregate, TResult, TAggregate> handler)
		{
			return AsyncQuery.Run(this, AsyncIterationHint.All, seed, handler);
		}

		/// <summary>Execute an action on the result of this async sequence</summary>
		public virtual Task<TAggregate> ExecuteAsync<TState, TAggregate>(TState state, TAggregate seed, Func<TState, TAggregate, TResult, TAggregate> handler)
		{
			return AsyncQuery.Run(this, AsyncIterationHint.All, state, seed, handler);
		}

		public virtual Task ExecuteAsync(Func<TResult, CancellationToken, Task> handler)
		{
			return AsyncQuery.Run(this, AsyncIterationHint.All, handler);
		}

		public virtual Task ExecuteAsync<TState>(TState state, Func<TState, TResult, CancellationToken, Task> handler)
		{
			return AsyncQuery.Run(this, AsyncIterationHint.All, state, handler);
		}

		#endregion

		#region Iterator Impl...

		protected abstract ValueTask<bool> OnFirstAsync();

		protected abstract ValueTask<bool> OnNextAsync();

		protected bool Publish(TResult current)
		{
			if (Volatile.Read(ref m_state) == STATE_ITERATING)
			{
				m_current = current;
				return true;
			}
			return false;
		}

		protected async ValueTask<bool> Completed()
		{
			if (Volatile.Read(ref m_state) == STATE_INIT)
			{ // nothing should have been done by the iterator
				Interlocked.CompareExchange(ref m_state, STATE_COMPLETED, STATE_INIT);
			}
			else if (Interlocked.CompareExchange(ref m_state, STATE_COMPLETED, STATE_ITERATING) == STATE_ITERATING)
			{ // the iterator has done at least something, so we can clean it up
				await Cleanup().ConfigureAwait(false);
			}
			return false;
		}

		/// <summary>Mark the current iterator as failed, and clean up the state</summary>
		protected ValueTask MarkAsFailed()
		{
			//TODO: store the state "failed" somewhere?
			return DisposeAsync();
		}

		protected async ValueTask<bool> Canceled()
		{
			//TODO: store the state "canceled" somewhere?
			await DisposeAsync().ConfigureAwait(false);
			this.Cancellation.ThrowIfCancellationRequested(); // should throw here!
			return false; //note: should not be reached
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		protected void EnsureIsIterating()
		{
			switch (Volatile.Read(ref m_state))
			{
				case STATE_SEQ:
					throw ThrowHelper.InvalidOperationException("The async iterator should have been initialized with a call to GetEnumerator()");

				case STATE_ITERATING:
					break;

				case STATE_DISPOSED:
					throw ThrowHelper.ObjectDisposedException(this, "The async iterator has already been closed");

				default:
				{
					throw ThrowHelper.InvalidOperationException("Unexpected state");
				}
			}
		}

		protected abstract ValueTask Cleanup();

		#endregion

		#region IAsyncDisposable...

		public virtual async ValueTask DisposeAsync()
		{
			if (Interlocked.Exchange(ref m_state, STATE_DISPOSED) != STATE_DISPOSED)
			{
				try
				{
					await Cleanup().ConfigureAwait(false);
				}
				finally
				{
					m_current = default!;
					GC.SuppressFinalize(this);
				}
			}
		}

		#endregion

	}

}
