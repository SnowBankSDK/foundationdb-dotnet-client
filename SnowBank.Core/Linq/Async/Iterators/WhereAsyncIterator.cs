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
	using SnowBank.Linq.Async.Expressions;

	/// <summary>Filters an async sequence of items</summary>
	/// <typeparam name="TSource">Type of elements of the async sequence</typeparam>
	internal sealed class WhereExpressionAsyncIterator<TSource> : AsyncFilterIterator<TSource, TSource>
	{

		private readonly AsyncFilterExpression<TSource> m_filter;

		public WhereExpressionAsyncIterator(IAsyncQuery<TSource> source, AsyncFilterExpression<TSource> filter)
			: base(source)
		{
			Contract.Debug.Requires(filter != null, "there can be only one kind of filter specified");

			m_filter = filter;
		}

		protected override AsyncLinqIterator<TSource> Clone()
		{
			return new WhereExpressionAsyncIterator<TSource>(m_source, m_filter);
		}

		protected override async ValueTask<bool> OnNextAsync()
		{
			var iterator = m_iterator;
			var filter = m_filter;
			var ct = this.Cancellation;
			Contract.Debug.Requires(iterator != null);

			while (!ct.IsCancellationRequested)
			{
				if (!await iterator.MoveNextAsync().ConfigureAwait(false))
				{ // completed
					return await Completed().ConfigureAwait(false);
				}

				if (ct.IsCancellationRequested) break;

				TSource current = iterator.Current;
				if (!filter.Async)
				{
					// ReSharper disable once MethodHasAsyncOverloadWithCancellation
					if (!filter.Invoke(current))
					{
						continue;
					}
				}
				else
				{
					if (!await filter.InvokeAsync(current, ct).ConfigureAwait(false))
					{
						continue;
					}
				}

				return Publish(current);
			}

			return await Canceled().ConfigureAwait(false);
		}

		public override AsyncLinqIterator<TSource> Where(Func<TSource, bool> predicate)
		{
			return AsyncQuery.Filter(
				m_source,
				m_filter.AndAlso(new AsyncFilterExpression<TSource>(predicate))
			);
		}

		public override AsyncLinqIterator<TSource> Where(Func<TSource, CancellationToken, Task<bool>> asyncPredicate)
		{
			return AsyncQuery.Filter(
				m_source,
				m_filter.AndAlso(new AsyncFilterExpression<TSource>(asyncPredicate))
			);
		}

		public override AsyncLinqIterator<TNew> Select<TNew>(Func<TSource, TNew> selector)
		{
			return new WhereSelectExpressionAsyncIterator<TSource, TNew>(
				m_source,
				m_filter,
				new AsyncTransformExpression<TSource, TNew>(selector),
				limit: null,
				offset: null
			);
		}

		public override AsyncLinqIterator<TNew> Select<TNew>(Func<TSource, CancellationToken, Task<TNew>> asyncSelector)
		{
			return new WhereSelectExpressionAsyncIterator<TSource, TNew>(
				m_source,
				m_filter,
				new AsyncTransformExpression<TSource, TNew>(asyncSelector),
				limit: null,
				offset: null
			);
		}

		public override AsyncLinqIterator<TSource> Take(int limit)
		{
			if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit), "Limit cannot be less than zero");

			return new WhereSelectExpressionAsyncIterator<TSource, TSource>(
				m_source,
				m_filter,
				new AsyncTransformExpression<TSource, TSource>(),
				limit: limit,
				offset: null
			);
		}

		public override async Task ExecuteAsync(Action<TSource> handler)
		{
			Contract.NotNull(handler);

			var ct = this.Cancellation;
			ct.ThrowIfCancellationRequested();

			var filter = m_filter;
			await using (var iter = StartInner())
			{
				if (!filter.Async)
				{
					while (!ct.IsCancellationRequested && (await iter.MoveNextAsync().ConfigureAwait(false)))
					{
						var current = iter.Current;
						// ReSharper disable once MethodHasAsyncOverloadWithCancellation
						if (filter.Invoke(current))
						{
							handler(current);
						}
					}
				}
				else
				{
					while (!ct.IsCancellationRequested && (await iter.MoveNextAsync().ConfigureAwait(false)))
					{
						var current = iter.Current;
						if (await filter.InvokeAsync(current, ct).ConfigureAwait(false))
						{
							handler(current);
						}
					}
				}
			}

			ct.ThrowIfCancellationRequested();
		}

		public override async Task ExecuteAsync<TState>(TState state, Action<TState, TSource> handler)
		{
			Contract.NotNull(handler);

			var ct = this.Cancellation;
			ct.ThrowIfCancellationRequested();

			var filter = m_filter;
			await using (var iter = StartInner())
			{
				if (!filter.Async)
				{
					while (!ct.IsCancellationRequested && (await iter.MoveNextAsync().ConfigureAwait(false)))
					{
						var current = iter.Current;
						// ReSharper disable once MethodHasAsyncOverloadWithCancellation
						if (filter.Invoke(current))
						{
							handler(state, current);
						}
					}
				}
				else
				{
					while (!ct.IsCancellationRequested && (await iter.MoveNextAsync().ConfigureAwait(false)))
					{
						var current = iter.Current;
						if (await filter.InvokeAsync(current, ct).ConfigureAwait(false))
						{
							handler(state, current);
						}
					}
				}
			}

			ct.ThrowIfCancellationRequested();
		}

		public override async Task<TAggregate> ExecuteAsync<TAggregate>(TAggregate seed, Func<TAggregate, TSource, TAggregate> handler)
		{
			Contract.NotNull(handler);

			var ct = this.Cancellation;
			ct.ThrowIfCancellationRequested();

			var filter = m_filter;
			await using (var iter = StartInner())
			{
				if (!filter.Async)
				{
					while (!ct.IsCancellationRequested && (await iter.MoveNextAsync().ConfigureAwait(false)))
					{
						var current = iter.Current;
						// ReSharper disable once MethodHasAsyncOverloadWithCancellation
						if (filter.Invoke(current))
						{
							seed = handler(seed, current);
						}
					}
				}
				else
				{
					while (!ct.IsCancellationRequested && (await iter.MoveNextAsync().ConfigureAwait(false)))
					{
						var current = iter.Current;
						if (await filter.InvokeAsync(current, ct).ConfigureAwait(false))
						{
							seed = handler(seed, current);
						}
					}
				}
			}

			ct.ThrowIfCancellationRequested();
			return seed;
		}

		public override async Task<TAggregate> ExecuteAsync<TState, TAggregate>(TState state, TAggregate seed, Func<TState, TAggregate, TSource, TAggregate> handler)
		{
			Contract.NotNull(handler);

			var ct = this.Cancellation;
			ct.ThrowIfCancellationRequested();

			var filter = m_filter;
			await using (var iter = StartInner())
			{
				if (!filter.Async)
				{
					while (!ct.IsCancellationRequested && (await iter.MoveNextAsync().ConfigureAwait(false)))
					{
						var current = iter.Current;
						// ReSharper disable once MethodHasAsyncOverloadWithCancellation
						if (filter.Invoke(current))
						{
							seed = handler(state, seed, current);
						}
					}
				}
				else
				{
					while (!ct.IsCancellationRequested && (await iter.MoveNextAsync().ConfigureAwait(false)))
					{
						var current = iter.Current;
						if (await filter.InvokeAsync(current, ct).ConfigureAwait(false))
						{
							seed = handler(state, seed, current);
						}
					}
				}
			}

			ct.ThrowIfCancellationRequested();
			return seed;
		}

		public override async Task ExecuteAsync(Func<TSource, CancellationToken, Task> handler)
		{
			Contract.NotNull(handler);

			var ct = this.Cancellation;
			ct.ThrowIfCancellationRequested();

			var filter = m_filter;
			await using (var iter = StartInner())
			{
				if (!filter.Async)
				{
					while (!ct.IsCancellationRequested && (await iter.MoveNextAsync().ConfigureAwait(false)))
					{
						var current = iter.Current;
						// ReSharper disable once MethodHasAsyncOverloadWithCancellation
						if (filter.Invoke(current))
						{
							await handler(current, ct).ConfigureAwait(false);
						}
					}
				}
				else
				{
					while (!ct.IsCancellationRequested && (await iter.MoveNextAsync().ConfigureAwait(false)))
					{
						var current = iter.Current;
						if (await filter.InvokeAsync(current, ct).ConfigureAwait(false))
						{
							await handler(current, ct).ConfigureAwait(false);
						}
					}
				}
			}

			ct.ThrowIfCancellationRequested();
		}

	}

	internal sealed class WhereAsyncIterator<TSource> : AsyncLinqIterator<TSource>
	{

		public WhereAsyncIterator(IAsyncQuery<TSource> source, Func<TSource, bool> filter)
		{
			Contract.Debug.Requires(source != null && filter != null);

			this.Source = source;
			this.Filter = filter;
		}


		private IAsyncQuery<TSource> Source { get; }

		private Func<TSource, bool> Filter { get; }

		/// <inheritdoc />
		public override CancellationToken Cancellation => this.Source.Cancellation;

		private IAsyncEnumerator<TSource>? Iterator { get; set; }

		protected override AsyncLinqIterator<TSource> Clone()
		{
			return new WhereAsyncIterator<TSource>(this.Source, this.Filter);
		}

		/// <inheritdoc />
		protected override ValueTask<bool> OnFirstAsync()
		{
			this.Iterator = this.Source.GetAsyncEnumerator(m_mode);
			return new(true);
		}

		protected override async ValueTask<bool> OnNextAsync()
		{
			var iterator = this.Iterator;
			if (iterator == null) return await Completed().ConfigureAwait(false);

			var filter = this.Filter;
			var ct = this.Cancellation;

			while (!ct.IsCancellationRequested)
			{
				if (!await iterator.MoveNextAsync().ConfigureAwait(false))
				{ // completed
					return await Completed().ConfigureAwait(false);
				}

				if (ct.IsCancellationRequested) break;

				var current = iterator.Current;
				if (filter(current))
				{
					return this.Publish(current);
				}
			}

			return await Canceled().ConfigureAwait(false);
		}

		/// <inheritdoc />
		protected override ValueTask Cleanup()
		{
			var iterator = this.Iterator;
			if (iterator == null) return default;

			this.Iterator = null;
			return iterator.DisposeAsync();
		}

		public override IAsyncLinqQuery<TSource> Where(Func<TSource, bool> predicate)
		{
			Contract.NotNull(predicate);

			return new WhereAsyncIterator<TSource>(this.Source, Combine(this.Filter, predicate));

			static Func<TSource, bool> Combine(Func<TSource, bool> first, Func<TSource, bool> second) => (item) => first(item) && second(item);
		}

		public override AsyncLinqIterator<TNew> Select<TNew>(Func<TSource, TNew> selector)
		{
			Contract.NotNull(selector);

			return new WhereSelectAsyncIterator<TSource, TNew>(this.Source, this.Filter, selector, null);
		}

		/// <inheritdoc />
		public override IAsyncLinqQuery<TNew> Select<TNew>(Func<TSource, CancellationToken, Task<TNew>> asyncSelector)
		{
			Contract.NotNull(asyncSelector);

			return new WhereSelectTaskAsyncIterator<TSource, TNew>(this.Source, this.Filter, asyncSelector, null);
		}

		public override async Task ExecuteAsync(Action<TSource> handler)
		{
			Contract.NotNull(handler);

			var ct = this.Cancellation;
			ct.ThrowIfCancellationRequested();

			await using var iterator = this.Source.GetAsyncEnumerator(m_mode);

			var filter = this.Filter;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				ct.ThrowIfCancellationRequested();

				var current = iterator.Current;
				// ReSharper disable once MethodHasAsyncOverloadWithCancellation
				if (filter(current))
				{
					handler(current);
				}
			}

			ct.ThrowIfCancellationRequested();
		}

		public override async Task ExecuteAsync<TState>(TState state, Action<TState, TSource> handler)
		{
			Contract.NotNull(handler);

			var ct = this.Cancellation;
			ct.ThrowIfCancellationRequested();

			await using var iterator = this.Source.GetAsyncEnumerator(m_mode);

			var filter = this.Filter;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				ct.ThrowIfCancellationRequested();

				var current = iterator.Current;
				// ReSharper disable once MethodHasAsyncOverloadWithCancellation
				if (filter(current))
				{
					handler(state, current);
				}
			}

			ct.ThrowIfCancellationRequested();
		}

		public override async Task ExecuteAsync(Func<TSource, CancellationToken, Task> handler)
		{
			Contract.NotNull(handler);

			var ct = this.Cancellation;
			ct.ThrowIfCancellationRequested();

			await using var iterator = this.Source.GetAsyncEnumerator(m_mode);

			var filter = this.Filter;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				ct.ThrowIfCancellationRequested();

				var current = iterator.Current;
				// ReSharper disable once MethodHasAsyncOverloadWithCancellation
				if (filter(current))
				{
					await handler(current, ct).ConfigureAwait(false);
				}
			}

			ct.ThrowIfCancellationRequested();

		}

		public override async Task ExecuteAsync<TState>(TState state, Func<TState, TSource, CancellationToken, Task> handler)
		{
			Contract.NotNull(handler);

			var ct = this.Cancellation;
			ct.ThrowIfCancellationRequested();

			await using var iterator = this.Source.GetAsyncEnumerator(m_mode);

			var filter = this.Filter;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				ct.ThrowIfCancellationRequested();

				var current = iterator.Current;
				if (filter(current))
				{
					await handler(state, current, ct).ConfigureAwait(false);
				}
			}

			ct.ThrowIfCancellationRequested();

		}

		public override async Task<TAggregate> ExecuteAsync<TState, TAggregate>(TState state, TAggregate seed, Func<TState, TAggregate, TSource, TAggregate> handler)
		{
			Contract.NotNull(handler);

			var ct = this.Cancellation;
			ct.ThrowIfCancellationRequested();

			await using var iterator = this.Source.GetAsyncEnumerator(m_mode);

			var filter = this.Filter;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				ct.ThrowIfCancellationRequested();

				var current = iterator.Current;
				if (filter(current))
				{
					seed = handler(state, seed, current);
				}
			}

			ct.ThrowIfCancellationRequested();
			return seed;
		}
	}

}
