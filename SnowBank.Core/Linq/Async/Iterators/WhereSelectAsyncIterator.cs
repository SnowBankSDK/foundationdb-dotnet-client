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

	/// <summary>Iterates over an async sequence of items</summary>
	/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	internal sealed class WhereSelectExpressionAsyncIterator<TSource, TResult> : AsyncFilterIterator<TSource, TResult>
	{
		private readonly AsyncFilterExpression<TSource>? m_filter;
		private readonly AsyncTransformExpression<TSource, TResult> m_transform;

		//note: both limit and offset are applied AFTER filtering!
		private readonly int? m_limit;
		private readonly int? m_offset;

		private int? m_remaining;
		private int? m_skipped;

		public WhereSelectExpressionAsyncIterator(
			IAsyncQuery<TSource> source,
			AsyncFilterExpression<TSource>? filter,
			AsyncTransformExpression<TSource, TResult> transform,
			int? limit = null,
			int? offset = null
		)
			: base(source)
		{
			Contract.Debug.Requires(transform != null); // must do at least something
			Contract.Debug.Requires((limit ?? 0) >= 0 && (offset ?? 0) >= 0); // bounds cannot be negative

			m_filter = filter;
			m_transform = transform;
			m_limit = limit;
			m_offset = offset;
		}

		protected override AsyncLinqIterator<TResult> Clone()
		{
			return new WhereSelectExpressionAsyncIterator<TSource, TResult>(m_source, m_filter, m_transform, m_limit, m_offset);
		}

		protected override ValueTask<bool> OnFirstAsync()
		{
			m_remaining = m_limit;
			m_skipped = m_offset;
			return base.OnFirstAsync();
		}

		protected override async ValueTask<bool> OnNextAsync()
		{
			if (m_remaining is <= 0)
			{ // reached limit!
				return await Completed().ConfigureAwait(false);
			}

			var ct = this.Cancellation;
			var iterator = m_iterator;
			Contract.Debug.Requires(iterator != null);

			while (!ct.IsCancellationRequested)
			{
				if (!await iterator.MoveNextAsync().ConfigureAwait(false))
				{ // completed
					return await Completed().ConfigureAwait(false);
				}
				if (ct.IsCancellationRequested) break;

				#region Filtering...

				TSource current = iterator.Current;
				var filter = m_filter;
				if (filter != null)
				{
					if (!filter.Async)
					{
						if (!filter.Invoke(current)) continue;
					}
					else
					{
						if (!await filter.InvokeAsync(current, ct).ConfigureAwait(false)) continue;
					}
				}

				#endregion

				#region Skipping...

				if (m_skipped != null)
				{
					if (m_skipped.Value > 0)
					{ // skip this result
						m_skipped = m_skipped.Value - 1;
						continue;
					}
					// we can now start outputting results...
					m_skipped = null;
				}

				#endregion

				#region Transforming...

				TResult result;
				if (!m_transform.Async)
				{
					result = m_transform.Invoke(current);
				}
				else
				{
					result = await m_transform.InvokeAsync(current, ct).ConfigureAwait(false);
				}

				#endregion

				#region Publishing...

				// decrement remaining quota
				--m_remaining;

				return Publish(result);

				#endregion
			}

			return await Canceled().ConfigureAwait(false);
		}

		public override IAsyncLinqQuery<TNew> Select<TNew>(Func<TResult, TNew> selector)
		{
			Contract.NotNull(selector);

			return new WhereSelectExpressionAsyncIterator<TSource, TNew>(
				m_source,
				m_filter,
				m_transform.Then(new AsyncTransformExpression<TResult, TNew>(selector)),
				m_limit,
				m_offset
			);
		}

		public override IAsyncLinqQuery<TNew> Select<TNew>(Func<TResult, CancellationToken, Task<TNew>> asyncSelector)
		{
			Contract.NotNull(asyncSelector);

			return new WhereSelectExpressionAsyncIterator<TSource, TNew>(
				m_source,
				m_filter,
				m_transform.Then(new AsyncTransformExpression<TResult, TNew>(asyncSelector)),
				m_limit,
				m_offset
			);
		}

		public override IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<TResult, IEnumerable<TNew>> selector)
		{
			Contract.NotNull(selector);

			if (m_filter == null && m_limit == null && m_offset == null)
			{
				return new SelectManyExpressionAsyncIterator<TSource, TNew>(
					m_source,
					m_transform.Then(new AsyncTransformExpression<TResult, IEnumerable<TNew>>(selector))
				);
			}

			// other cases are too complex :(
			return base.SelectMany(selector);
		}

		public override IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TNew>>> asyncSelector)
		{
			Contract.NotNull(asyncSelector);

			if (m_filter == null && m_limit == null && m_offset == null)
			{
				return new SelectManyExpressionAsyncIterator<TSource, TNew>(
					m_source,
					m_transform.Then(new AsyncTransformExpression<TResult, IEnumerable<TNew>>(asyncSelector))
				);
			}

			// other cases are too complex :(
			return base.SelectMany(asyncSelector);
		}

		public override IAsyncLinqQuery<TResult> Take(int limit)
		{
			if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit), "Limit cannot be less than zero");

			if (m_limit != null && m_limit.Value <= limit)
			{
				// we are already taking less than that
				return this;
			}

			return new WhereSelectExpressionAsyncIterator<TSource, TResult>(
				m_source,
				m_filter,
				m_transform,
				limit,
				m_offset
			);
		}

		public override IAsyncLinqQuery<TResult> Skip(int offset)
		{
			if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be less than zero");

			if (offset == 0) return this;

			if (m_offset != null) offset += m_offset.Value;

			return new WhereSelectExpressionAsyncIterator<TSource, TResult>(
				m_source,
				m_filter,
				m_transform,
				m_limit,
				offset
			);
		}

		public override IAsyncLinqQuery<TResult> Where(Func<TResult, bool> predicate)
		{
			Contract.NotNull(predicate);

			// note: the only possible optimization here is if TSource == TResult, then we can combine both predicates
			// remember: limit/offset are applied AFTER the filtering, so can only combine if they are null
			// also, since transform is done after filtering, we can only optimize if transform is null (not allowed!) or the identity function
			if (m_limit == null
				&& (m_offset == null || m_offset.Value == 0)
				&& typeof(TSource) == typeof(TResult) //BUGBUG: type comparison maybe should check derived classes also ?
				&& m_transform.IsIdentity())
			{
				var filter = new AsyncFilterExpression<TSource>((Func<TSource, bool>)(Delegate)predicate);
				if (m_filter != null) filter = m_filter.AndAlso(filter);

				//BUGBUG: if the query already has a select, it should be evaluated BEFORE the new filter,
				// but currently WhereSelectAsyncIterator<> filters before transformations !

				return new WhereSelectExpressionAsyncIterator<TSource, TResult>(
					m_source,
					filter,
					m_transform,
					m_limit,
					m_offset
				);
			}

			// no easy optimization...
			return base.Where(predicate);
		}

		public override IAsyncLinqQuery<TResult> Where(Func<TResult, CancellationToken, Task<bool>> asyncPredicate)
		{
			Contract.NotNull(asyncPredicate);

			// note: the only possible optimization here is if TSource == TResult, then we can combine both predicates
			// remember: limit/offset are applied AFTER the filtering, so can only combine if they are null
			if (m_limit == null && (m_offset == null || m_offset.Value == 0) && typeof(TSource) == typeof(TResult)) //BUGBUG: type comparison maybe should check derived classes also ?
			{
				var asyncFilter = new AsyncFilterExpression<TSource>((Func<TSource, CancellationToken, Task<bool>>)(Delegate)asyncPredicate);
				if (m_filter != null) asyncFilter = m_filter.AndAlso(asyncFilter);

				//BUGBUG: if the query already has a select, it should be evaluated BEFORE the new filter,
				// but currently WhereSelectAsyncIterator<> filters before transformations !

				return new WhereSelectExpressionAsyncIterator<TSource, TResult>(
					m_source,
					asyncFilter,
					m_transform,
					m_limit,
					m_offset
				);
			}

			// no easy optimization...
			return base.Where(asyncPredicate);
		}

		public override async Task ExecuteAsync(Action<TResult> handler)
		{
			Contract.NotNull(handler);

			int? remaining = m_limit;
			int? skipped = m_offset;
			var ct = this.Cancellation;

			await using(var iterator = StartInner())
			{
				while (remaining == null || remaining.Value > 0)
				{
					if (!await iterator.MoveNextAsync().ConfigureAwait(false))
					{ // completed
						break;
					}

					// Filter...

					TSource current = iterator.Current;
					var filter = m_filter;
					if (filter != null)
					{
						if (!filter.Async)
						{
							// ReSharper disable once MethodHasAsyncOverloadWithCancellation
							if (!filter.Invoke(current)) continue;
						}
						else
						{
							if (!await filter.InvokeAsync(current, ct).ConfigureAwait(false)) continue;
						}
					}

					// Skip...

					if (skipped != null)
					{
						if (skipped.Value > 0)
						{ // skip this result
							skipped = skipped.Value - 1;
							continue;
						}
						// we can now start outputting results...
						skipped = null;
					}

					// Transform...

					TResult result;
					if (!m_transform.Async)
					{
						// ReSharper disable once MethodHasAsyncOverloadWithCancellation
						result = m_transform.Invoke(current);
					}
					else
					{
						result = await m_transform.InvokeAsync(current, ct).ConfigureAwait(false);
					}

					// Publish...

					// decrement remaining quota
					--remaining;
					handler(result);
				}

				ct.ThrowIfCancellationRequested();
			}
		}

		public override async Task ExecuteAsync<TState>(TState state, Action<TState, TResult> handler)
		{
			Contract.NotNull(handler);

			int? remaining = m_limit;
			int? skipped = m_offset;
			var ct = this.Cancellation;

			await using(var iterator = StartInner())
			{
				while (remaining == null || remaining.Value > 0)
				{
					if (!await iterator.MoveNextAsync().ConfigureAwait(false))
					{ // completed
						break;
					}

					// Filter...

					TSource current = iterator.Current;
					var filter = m_filter;
					if (filter != null)
					{
						if (!filter.Async)
						{
							// ReSharper disable once MethodHasAsyncOverloadWithCancellation
							if (!filter.Invoke(current)) continue;
						}
						else
						{
							if (!await filter.InvokeAsync(current, ct).ConfigureAwait(false)) continue;
						}
					}

					// Skip...

					if (skipped != null)
					{
						if (skipped.Value > 0)
						{ // skip this result
							skipped = skipped.Value - 1;
							continue;
						}
						// we can now start outputting results...
						skipped = null;
					}

					// Transform...

					TResult result;
					if (!m_transform.Async)
					{
						// ReSharper disable once MethodHasAsyncOverloadWithCancellation
						result = m_transform.Invoke(current);
					}
					else
					{
						result = await m_transform.InvokeAsync(current, ct).ConfigureAwait(false);
					}

					// Publish...

					// decrement remaining quota
					--remaining;
					handler(state, result);
				}

				ct.ThrowIfCancellationRequested();
			}
		}

		public override async Task<TAggregate> ExecuteAsync<TAggregate>(TAggregate seed, Func<TAggregate, TResult, TAggregate> handler)
		{
			Contract.NotNull(handler);

			int? remaining = m_limit;
			int? skipped = m_offset;
			var ct = this.Cancellation;

			await using(var iterator = StartInner())
			{
				while (remaining == null || remaining.Value > 0)
				{
					if (!await iterator.MoveNextAsync().ConfigureAwait(false))
					{ // completed
						break;
					}

					// Filter...

					TSource current = iterator.Current;
					var filter = m_filter;
					if (filter != null)
					{
						if (!filter.Async)
						{
							// ReSharper disable once MethodHasAsyncOverloadWithCancellation
							if (!filter.Invoke(current)) continue;
						}
						else
						{
							if (!await filter.InvokeAsync(current, ct).ConfigureAwait(false)) continue;
						}
					}

					// Skip...

					if (skipped != null)
					{
						if (skipped.Value > 0)
						{ // skip this result
							skipped = skipped.Value - 1;
							continue;
						}
						// we can now start outputting results...
						skipped = null;
					}

					// Transform...

					TResult result;
					if (!m_transform.Async)
					{
						// ReSharper disable once MethodHasAsyncOverloadWithCancellation
						result = m_transform.Invoke(current);
					}
					else
					{
						result = await m_transform.InvokeAsync(current, ct).ConfigureAwait(false);
					}

					// Publish...

					// decrement remaining quota
					--remaining;
					seed = handler(seed, result);
				}

				ct.ThrowIfCancellationRequested();
			}

			return seed;
		}

		public override async Task<TAggregate> ExecuteAsync<TState, TAggregate>(TState state, TAggregate seed, Func<TState, TAggregate, TResult, TAggregate> handler)
		{
			Contract.NotNull(handler);

			int? remaining = m_limit;
			int? skipped = m_offset;
			var ct = this.Cancellation;

			await using(var iterator = StartInner())
			{
				while (remaining == null || remaining.Value > 0)
				{
					if (!await iterator.MoveNextAsync().ConfigureAwait(false))
					{ // completed
						break;
					}

					// Filter...

					TSource current = iterator.Current;
					var filter = m_filter;
					if (filter != null)
					{
						if (!filter.Async)
						{
							// ReSharper disable once MethodHasAsyncOverloadWithCancellation
							if (!filter.Invoke(current)) continue;
						}
						else
						{
							if (!await filter.InvokeAsync(current, ct).ConfigureAwait(false)) continue;
						}
					}

					// Skip...

					if (skipped != null)
					{
						if (skipped.Value > 0)
						{ // skip this result
							skipped = skipped.Value - 1;
							continue;
						}
						// we can now start outputting results...
						skipped = null;
					}

					// Transform...

					TResult result;
					if (!m_transform.Async)
					{
						// ReSharper disable once MethodHasAsyncOverloadWithCancellation
						result = m_transform.Invoke(current);
					}
					else
					{
						result = await m_transform.InvokeAsync(current, ct).ConfigureAwait(false);
					}

					// Publish...

					// decrement remaining quota
					--remaining;
					seed = handler(state, seed, result);
				}

				ct.ThrowIfCancellationRequested();
			}

			return seed;
		}

		public override async Task ExecuteAsync(Func<TResult, CancellationToken, Task> handler)
		{
			Contract.NotNull(handler);

			int? remaining = m_limit;
			int? skipped = m_offset;
			var ct = this.Cancellation;

			await using (var iterator = StartInner())
			{
				while (remaining == null || remaining.Value > 0)
				{
					if (!await iterator.MoveNextAsync().ConfigureAwait(false))
					{ // completed
						break;
					}

					// Filter...

					TSource current = iterator.Current;
					var filter = m_filter;
					if (filter != null)
					{
						if (!filter.Async)
						{
							// ReSharper disable once MethodHasAsyncOverloadWithCancellation
							if (!filter.Invoke(current)) continue;
						}
						else
						{
							if (!await filter.InvokeAsync(current, ct).ConfigureAwait(false)) continue;
						}
					}

					// Skip...

					if (skipped != null)
					{
						if (skipped.Value > 0)
						{ // skip this result
							skipped = skipped.Value - 1;
							continue;
						}
						// we can now start outputting results...
						skipped = null;
					}

					// Transform...

					TResult result;
					if (!m_transform.Async)
					{
						// ReSharper disable once MethodHasAsyncOverloadWithCancellation
						result = m_transform.Invoke(current);
					}
					else
					{
						result = await m_transform.InvokeAsync(current, ct).ConfigureAwait(false);
					}

					// Publish...

					// decrement remaining quota
					--remaining;

					await handler(result, ct).ConfigureAwait(false);
				}

				ct.ThrowIfCancellationRequested();
			}
		}

	}

	/// <summary>Iterates over an async sequence of items</summary>
	/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	/// <remarks>This is a simpler version that only supports non-async selectors and predicates (the most frequently used)</remarks>
	internal sealed class SelectAsyncIterator<TSource, TResult> : AsyncLinqIterator<TResult>
	{

		public SelectAsyncIterator(IAsyncQuery<TSource> source, Func<TSource, TResult> transform)
		{
			this.Source = source;
			this.Transform = transform;
		}

		private IAsyncQuery<TSource> Source { get; }

		private Func<TSource, TResult> Transform { get; }

		/// <inheritdoc />
		public override CancellationToken Cancellation => this.Source.Cancellation;

		private IAsyncEnumerator<TSource>? Iterator { get; set; }

		/// <inheritdoc />
		protected override SelectAsyncIterator<TSource, TResult> Clone() => new(this.Source, this.Transform);

		/// <inheritdoc />
		protected override ValueTask<bool> OnFirstAsync()
		{
			this.Iterator = this.Source.GetAsyncEnumerator(m_mode);
			return new(true);
		}

		/// <inheritdoc />
		protected override async ValueTask<bool> OnNextAsync()
		{
			var iterator = this.Iterator;
			if (iterator == null)
			{
				return await Completed().ConfigureAwait(false);
			}

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				return Publish(this.Transform(iterator.Current));
			}

			return await this.Completed().ConfigureAwait(false);
		}

		/// <inheritdoc />
		protected override ValueTask Cleanup()
		{
			var iterator = this.Iterator;
			if (iterator == null) return default;

			this.Iterator = null;
			return iterator.DisposeAsync();
		}

		/// <inheritdoc />
		public override async Task ExecuteAsync(Action<TResult> handler)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var transform = this.Transform;

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				handler(transform(iterator.Current));
			}
		}

		/// <inheritdoc />
		public override async Task ExecuteAsync<TState>(TState state, Action<TState, TResult> handler)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var transform = this.Transform;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				handler(state, transform(iterator.Current));
			}
		}

		/// <inheritdoc />
		public override async Task ExecuteAsync(Func<TResult, CancellationToken, Task> handler)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var transform = this.Transform;
			var ct = this.Cancellation;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				var value = transform(item);
				await handler(value, ct).ConfigureAwait(false);
			}
		}

		/// <inheritdoc />
		public override async Task ExecuteAsync<TState>(TState state, Func<TState, TResult, CancellationToken, Task> handler)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var transform = this.Transform;
			var ct = this.Cancellation;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				await handler(state, transform(iterator.Current), ct).ConfigureAwait(false);
			}
		}

		/// <inheritdoc />
		public override async Task<TAggregate> ExecuteAsync<TAggregate>(TAggregate seed, Func<TAggregate, TResult, TAggregate> handler)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var transform = this.Transform;
			var accumulator = seed;

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				accumulator = handler(accumulator, transform(iterator.Current));
			}
			return accumulator;
		}

		/// <inheritdoc />
		public override async Task<TAggregate> ExecuteAsync<TState, TAggregate>(TState state, TAggregate seed, Func<TState, TAggregate, TResult, TAggregate> handler)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var transform = this.Transform;
			var accumulator = seed;

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				accumulator = handler(state, accumulator, transform(iterator.Current));
			}
			return accumulator;
		}

		private async Task FillBufferAsync(Buffer<TResult> buffer)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var transform = this.Transform;

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				buffer.Add(transform(iterator.Current));
			}
		}

		/// <inheritdoc />
		public override async Task<TResult[]> ToArrayAsync()
		{
			//note: we assume that the source does not implement IAsyncLinqQuery<TResult> (otherwise, we would have called their own implementation, instead of this one!)

			var buffer = new Buffer<TResult>(pool: ArrayPool<TResult>.Shared);
			await FillBufferAsync(buffer).ConfigureAwait(false);
			return buffer.ToArrayAndClear();
		}

		/// <inheritdoc />
		public override async Task<List<TResult>> ToListAsync()
		{
			//note: we assume that the source does not implement IAsyncLinqQuery<TResult> (otherwise, we would have called their own implementation, instead of this one!)

			var buffer = new Buffer<TResult>(pool: ArrayPool<TResult>.Shared);
			await FillBufferAsync(buffer).ConfigureAwait(false);
			return buffer.ToListAndClear();
		}

		/// <inheritdoc />
		public override async Task<ImmutableArray<TResult>> ToImmutableArrayAsync()
		{
			//note: we assume that the source does not implement IAsyncLinqQuery<TResult> (otherwise, we would have called their own implementation, instead of this one!)

			var buffer = new Buffer<TResult>(pool: ArrayPool<TResult>.Shared);
			await FillBufferAsync(buffer).ConfigureAwait(false);
			return buffer.ToImmutableArrayAndClear();
		}

		/// <inheritdoc />
		public override async Task<HashSet<TResult>> ToHashSetAsync(IEqualityComparer<TResult>? comparer = null)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var transform = this.Transform;
			var set = new HashSet<TResult>(comparer);

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				set.Add(transform(iterator.Current));
			}

			return set;
		}

		/// <inheritdoc />
		public override async Task<Dictionary<TKey, TResult>> ToDictionaryAsync<TKey>(Func<TResult, TKey> keySelector, IEqualityComparer<TKey>? comparer = null)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var transform = this.Transform;
			var map = new Dictionary<TKey, TResult>(comparer);

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = transform(iterator.Current);
				map.Add(keySelector(item), item);
			}

			return map;
		}

		/// <inheritdoc />
		public override async Task<Dictionary<TKey, TElement>> ToDictionaryAsync<TKey, TElement>(Func<TResult, TKey> keySelector, Func<TResult, TElement> elementSelector, IEqualityComparer<TKey>? comparer = null)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var transform = this.Transform;
			var map = new Dictionary<TKey, TElement>(comparer);

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = transform(iterator.Current);
				map.Add(keySelector(item), elementSelector(item));
			}

			return map;
		}
		/// <inheritdoc />
		public override SelectAsyncIterator<TSource, TNew> Select<TNew>(Func<TResult, TNew> selector)
		{
			return new(this.Source, Combine(this.Transform, selector));

			static Func<TSource, TNew> Combine(Func<TSource, TResult> inner, Func<TResult, TNew> outer) => (item) => outer(inner(item));
		}

		/// <inheritdoc />
		public override SelectTaskAsyncIterator<TSource, TNew> Select<TNew>(Func<TResult, CancellationToken, Task<TNew>> selector)
		{
			return new(this.Source, Combine(this.Transform, selector));

			static Func<TSource, CancellationToken, Task<TNew>> Combine(Func<TSource, TResult> inner, Func<TResult, CancellationToken, Task<TNew>> outer) => (item, ct) => outer(inner(item), ct);
		}

		/// <inheritdoc />
		public override WhereSelectAsyncIterator<TSource, TResult> Where(Func<TResult, bool> predicate)
		{
			return new(this.Source, null, this.Transform, predicate);
		}

	}

	/// <summary>Iterates over an async sequence of items</summary>
	/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	/// <remarks>This is a simpler version that only supports non-async selectors and predicates (the most frequently used)</remarks>
	internal sealed class SelectTaskAsyncIterator<TSource, TResult> : AsyncLinqIterator<TResult>
	{

		public SelectTaskAsyncIterator(IAsyncQuery<TSource> source, Func<TSource, CancellationToken, Task<TResult>> transform)
		{
			this.Source = source;
			this.Transform = transform;
		}

		private IAsyncQuery<TSource> Source { get; }

		private Func<TSource, CancellationToken, Task<TResult>> Transform { get; }

		/// <inheritdoc />
		public override CancellationToken Cancellation => this.Source.Cancellation;

		private IAsyncEnumerator<TSource>? Iterator { get; set; }

		/// <inheritdoc />
		protected override SelectTaskAsyncIterator<TSource, TResult> Clone() => new(this.Source, this.Transform);

		/// <inheritdoc />
		protected override ValueTask<bool> OnFirstAsync()
		{
			this.Iterator = this.Source.GetAsyncEnumerator(m_mode);
			return new(true);
		}

		/// <inheritdoc />
		protected override async ValueTask<bool> OnNextAsync()
		{
			var iterator = this.Iterator;
			if (iterator == null || !await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				return await this.Completed().ConfigureAwait(false);
			}

			return Publish(await this.Transform(iterator.Current, this.Cancellation).ConfigureAwait(false));
		}

		/// <inheritdoc />
		protected override ValueTask Cleanup()
		{
			var iterator = this.Iterator;
			if (iterator == null) return default;

			this.Iterator = null;
			return iterator.DisposeAsync();
		}

		/// <inheritdoc />
		public override async Task ExecuteAsync(Action<TResult> handler)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var transform = this.Transform;
			var ct = this.Cancellation;

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				handler(await transform(iterator.Current, ct).ConfigureAwait(false));
			}
		}

		/// <inheritdoc />
		public override async Task ExecuteAsync<TState>(TState state, Action<TState, TResult> handler)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var transform = this.Transform;
			var ct = this.Cancellation;

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				handler(state, await transform(iterator.Current, ct).ConfigureAwait(false));
			}
		}

		/// <inheritdoc />
		public override async Task ExecuteAsync(Func<TResult, CancellationToken, Task> handler)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var transform = this.Transform;
			var ct = this.Cancellation;

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				await handler(await transform(iterator.Current, ct).ConfigureAwait(false), ct).ConfigureAwait(false);
			}
		}

		/// <inheritdoc />
		public override async Task ExecuteAsync<TState>(TState state, Func<TState, TResult, CancellationToken, Task> handler)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var transform = this.Transform;
			var ct = this.Cancellation;

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				await handler(state, await transform(iterator.Current, ct).ConfigureAwait(false), ct).ConfigureAwait(false);
			}
		}

		/// <inheritdoc />
		public override async Task<TAggregate> ExecuteAsync<TAggregate>(TAggregate seed, Func<TAggregate, TResult, TAggregate> handler)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var transform = this.Transform;
			var accumulator = seed;
			var ct = this.Cancellation;

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				accumulator = handler(accumulator, await transform(iterator.Current, ct).ConfigureAwait(false));
			}

			return accumulator;
		}

		/// <inheritdoc />
		public override async Task<TAggregate> ExecuteAsync<TState, TAggregate>(TState state, TAggregate seed, Func<TState, TAggregate, TResult, TAggregate> handler)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var transform = this.Transform;
			var accumulator = seed;
			var ct = this.Cancellation;

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				accumulator = handler(state, accumulator, await transform(iterator.Current, ct).ConfigureAwait(false));
			}

			return accumulator;
		}

		private async Task FillBufferAsync(Buffer<TResult> buffer)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var transform = this.Transform;
			var ct = this.Cancellation;

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				buffer.Add(await transform(iterator.Current, ct).ConfigureAwait(false));
			}
		}

		/// <inheritdoc />
		public override async Task<TResult[]> ToArrayAsync()
		{
			//note: we assume that the source does not implement IAsyncLinqQuery<TResult> (otherwise, we would have called their own implementation, instead of this one!)

			var buffer = new Buffer<TResult>(pool: ArrayPool<TResult>.Shared);
			await FillBufferAsync(buffer).ConfigureAwait(false);
			return buffer.ToArrayAndClear();
		}

		/// <inheritdoc />
		public override async Task<List<TResult>> ToListAsync()
		{
			//note: we assume that the source does not implement IAsyncLinqQuery<TResult> (otherwise, we would have called their own implementation, instead of this one!)

			var buffer = new Buffer<TResult>(pool: ArrayPool<TResult>.Shared);
			await FillBufferAsync(buffer).ConfigureAwait(false);
			return buffer.ToListAndClear();
		}

		/// <inheritdoc />
		public override async Task<ImmutableArray<TResult>> ToImmutableArrayAsync()
		{
			//note: we assume that the source does not implement IAsyncLinqQuery<TResult> (otherwise, we would have called their own implementation, instead of this one!)

			var buffer = new Buffer<TResult>(pool: ArrayPool<TResult>.Shared);
			await FillBufferAsync(buffer).ConfigureAwait(false);
			return buffer.ToImmutableArrayAndClear();
		}

		/// <inheritdoc />
		public override async Task<HashSet<TResult>> ToHashSetAsync(IEqualityComparer<TResult>? comparer = null)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var transform = this.Transform;
			var ct = this.Cancellation;
			var set = new HashSet<TResult>(comparer);

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				set.Add(await transform(iterator.Current, ct).ConfigureAwait(false));
			}

			return set;
		}

		/// <inheritdoc />
		public override async Task<Dictionary<TKey, TResult>> ToDictionaryAsync<TKey>(Func<TResult, TKey> keySelector, IEqualityComparer<TKey>? comparer = null)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var transform = this.Transform;
			var ct = this.Cancellation;
			var map = new Dictionary<TKey, TResult>(comparer);

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = await transform(iterator.Current, ct).ConfigureAwait(false);
				map.Add(keySelector(item), item);
			}

			return map;
		}

		/// <inheritdoc />
		public override async Task<Dictionary<TKey, TElement>> ToDictionaryAsync<TKey, TElement>(Func<TResult, TKey> keySelector, Func<TResult, TElement> elementSelector, IEqualityComparer<TKey>? comparer = null)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var transform = this.Transform;
			var ct = this.Cancellation;
			var map = new Dictionary<TKey, TElement>(comparer);

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = await transform(iterator.Current, ct).ConfigureAwait(false);
				map.Add(keySelector(item), elementSelector(item));
			}

			return map;
		}

		/// <inheritdoc />
		public override SelectTaskAsyncIterator<TSource, TNew> Select<TNew>(Func<TResult, TNew> selector)
		{
			return new(this.Source, Combine(this.Transform, selector));

			static Func<TSource, CancellationToken, Task<TNew>> Combine(Func<TSource, CancellationToken, Task<TResult>> inner, Func<TResult, TNew> outer) => async (item, ct) => outer(await inner(item, ct).ConfigureAwait(false));
		}

		/// <inheritdoc />
		public override SelectTaskAsyncIterator<TSource, TNew> Select<TNew>(Func<TResult, CancellationToken, Task<TNew>> selector)
		{
			return new(this.Source, Combine(this.Transform, selector));

			static Func<TSource, CancellationToken, Task<TNew>> Combine(Func<TSource, CancellationToken, Task<TResult>> inner, Func<TResult, CancellationToken, Task<TNew>> outer) => async (item, ct) => await outer(await inner(item, ct).ConfigureAwait(false), ct).ConfigureAwait(false);
		}

		/// <inheritdoc />
		public override WhereSelectTaskAsyncIterator<TSource, TResult> Where(Func<TResult, bool> predicate)
		{
			return new(this.Source, null, this.Transform, predicate);
		}

	}

	/// <summary>Iterates over an async sequence of items</summary>
	/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	/// <remarks>This is a simpler version that only supports non-async selectors and predicates (the most frequently used)</remarks>
	internal sealed class WhereSelectAsyncIterator<TSource, TResult> : AsyncLinqIterator<TResult>
	{

		public WhereSelectAsyncIterator(IAsyncQuery<TSource> source, Func<TSource, bool>? preFilter, Func<TSource, TResult> transform, Func<TResult, bool>? postFilter)
		{
			this.Source = source;
			this.PreFilter = preFilter;
			this.Transform = transform;
			this.PostFilter = postFilter;
		}

		private IAsyncQuery<TSource> Source { get; }

		private Func<TSource, bool>? PreFilter { get; }

		private Func<TResult, bool>? PostFilter { get; }

		private Func<TSource, TResult> Transform { get; }

		/// <inheritdoc />
		public override CancellationToken Cancellation => this.Source.Cancellation;

		private IAsyncEnumerator<TSource>? Iterator { get; set; }

		/// <inheritdoc />
		protected override WhereSelectAsyncIterator<TSource, TResult> Clone() => new(this.Source, this.PreFilter, this.Transform, this.PostFilter);

		/// <inheritdoc />
		protected override ValueTask<bool> OnFirstAsync()
		{
			this.Iterator = this.Source.GetAsyncEnumerator(m_mode);
			return new(true);
		}

		/// <inheritdoc />
		protected override async ValueTask<bool> OnNextAsync()
		{
			var iterator = this.Iterator;
			if (iterator == null)
			{
				return await Completed().ConfigureAwait(false);
			}

			var preFilter = this.PreFilter;
			var postFilter = this.PostFilter;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (preFilter == null || preFilter(item))
				{
					var value = this.Transform(item);
					if (postFilter == null || postFilter(value))
					{
						return Publish(value);
					}
				}
			}

			return await this.Completed().ConfigureAwait(false);
		}

		/// <inheritdoc />
		protected override ValueTask Cleanup()
		{
			var iterator = this.Iterator;
			if (iterator == null) return default;

			this.Iterator = null;
			return iterator.DisposeAsync();
		}

		/// <inheritdoc />
		public override async Task ExecuteAsync(Action<TResult> handler)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var preFilter = this.PreFilter;
			var transform = this.Transform;
			var postFilter = this.PostFilter;

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (preFilter == null || preFilter(item))
				{
					var value = transform(item);
					if (postFilter == null || postFilter(value))
					{
						handler(value);
					}
				}
			}
		}

		/// <inheritdoc />
		public override async Task ExecuteAsync<TState>(TState state, Action<TState, TResult> handler)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var preFilter = this.PreFilter;
			var transform = this.Transform;
			var postFilter = this.PostFilter;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (preFilter == null || preFilter(item))
				{
					var value = transform(item);
					if (postFilter == null || postFilter(value))
					{
						handler(state, value);
					}
				}
			}
		}

		/// <inheritdoc />
		public override async Task ExecuteAsync(Func<TResult, CancellationToken, Task> handler)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var preFilter = this.PreFilter;
			var transform = this.Transform;
			var postFilter = this.PostFilter;
			var ct = this.Cancellation;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (preFilter == null || preFilter(item))
				{
					var value = transform(item);
					if (postFilter == null || postFilter(value))
					{
						await handler(value, ct).ConfigureAwait(false);
					}
				}
			}
		}

		/// <inheritdoc />
		public override async Task ExecuteAsync<TState>(TState state, Func<TState, TResult, CancellationToken, Task> handler)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var preFilter = this.PreFilter;
			var transform = this.Transform;
			var postFilter = this.PostFilter;
			var ct = this.Cancellation;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (preFilter == null || preFilter(item))
				{
					var value = transform(item);
					if (postFilter == null || postFilter(value))
					{
						await handler(state, value, ct).ConfigureAwait(false);
					}
				}
			}
		}

		/// <inheritdoc />
		public override async Task<TAggregate> ExecuteAsync<TAggregate>(TAggregate seed, Func<TAggregate, TResult, TAggregate> handler)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var preFilter = this.PreFilter;
			var transform = this.Transform;
			var postFilter = this.PostFilter;
			var accumulator = seed;

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (preFilter == null || preFilter(item))
				{
					var value = transform(item);
					if (postFilter == null || postFilter(value))
					{
						accumulator = handler(accumulator, value);
					}
				}
			}
			return accumulator;
		}

		/// <inheritdoc />
		public override async Task<TAggregate> ExecuteAsync<TState, TAggregate>(TState state, TAggregate seed, Func<TState, TAggregate, TResult, TAggregate> handler)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var preFilter = this.PreFilter;
			var transform = this.Transform;
			var postFilter = this.PostFilter;
			var accumulator = seed;

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (preFilter == null || preFilter(item))
				{
					var value = transform(item);
					if (postFilter == null || postFilter(value))
					{
						accumulator = handler(state, accumulator, value);
					}
				}
			}
			return accumulator;
		}

		private async Task FillBufferAsync(Buffer<TResult> buffer)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var preFilter = this.PreFilter;
			var transform = this.Transform;
			var postFilter = this.PostFilter;

			if (preFilter == null && postFilter == null)
			{
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					buffer.Add(transform(iterator.Current));
				}
			}
			else
			{
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					var item = iterator.Current;
					if (preFilter == null || preFilter(item))
					{
						var value = transform(item);
						if (postFilter == null || postFilter(value))
						{
							buffer.Add(value);
						}
					}
				}
			}
		}

		/// <inheritdoc />
		public override async Task<TResult[]> ToArrayAsync()
		{
			//note: we assume that the source does not implement IAsyncLinqQuery<TResult> (otherwise, we would have called their own implementation, instead of this one!)

			var buffer = new Buffer<TResult>(pool: ArrayPool<TResult>.Shared);
			await FillBufferAsync(buffer).ConfigureAwait(false);
			return buffer.ToArrayAndClear();
		}

		/// <inheritdoc />
		public override async Task<List<TResult>> ToListAsync()
		{
			//note: we assume that the source does not implement IAsyncLinqQuery<TResult> (otherwise, we would have called their own implementation, instead of this one!)

			var buffer = new Buffer<TResult>(pool: ArrayPool<TResult>.Shared);
			await FillBufferAsync(buffer).ConfigureAwait(false);
			return buffer.ToListAndClear();
		}

		/// <inheritdoc />
		public override async Task<ImmutableArray<TResult>> ToImmutableArrayAsync()
		{
			//note: we assume that the source does not implement IAsyncLinqQuery<TResult> (otherwise, we would have called their own implementation, instead of this one!)

			var buffer = new Buffer<TResult>(pool: ArrayPool<TResult>.Shared);
			await FillBufferAsync(buffer).ConfigureAwait(false);
			return buffer.ToImmutableArrayAndClear();
		}

		/// <inheritdoc />
		public override IAsyncLinqQuery<TNew> Select<TNew>(Func<TResult, TNew> selector)
		{
			if (this.PostFilter != null)
			{
				// we cannot easily combine the pre- and post-filter without calling the original selector twice!
				return new WhereSelectAsyncIterator<TResult, TNew>(this, null, selector, null);
			}

			return new WhereSelectAsyncIterator<TSource, TNew>(this.Source, this.PreFilter, Combine(this.Transform, selector), null);

			static Func<TSource, TNew> Combine(Func<TSource, TResult> inner, Func<TResult, TNew> outer) => (item) => outer(inner(item));
		}

		/// <inheritdoc />
		public override WhereSelectAsyncIterator<TSource, TResult> Where(Func<TResult, bool> predicate)
		{
			return new(this.Source, this.PreFilter, this.Transform, this.PostFilter == null ? predicate : Combine(this.PostFilter, predicate));

			static Func<TResult, bool> Combine(Func<TResult, bool> first, Func<TResult, bool> second) => (item) => first(item) && second(item);
		}

	}

	/// <summary>Iterates over an async sequence of items</summary>
	/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	/// <remarks>This is a simpler version that only supports non-async selectors and predicates (the most frequently used)</remarks>
	internal sealed class WhereSelectTaskAsyncIterator<TSource, TResult> : AsyncLinqIterator<TResult>
	{

		public WhereSelectTaskAsyncIterator(IAsyncQuery<TSource> source, Func<TSource, bool>? preFilter, Func<TSource, CancellationToken, Task<TResult>> transform, Func<TResult, bool>? postFilter)
		{
			this.Source = source;
			this.PreFilter = preFilter;
			this.Transform = transform;
			this.PostFilter = postFilter;
		}

		private IAsyncQuery<TSource> Source { get; }

		private Func<TSource, bool>? PreFilter { get; }

		private Func<TResult, bool>? PostFilter { get; }

		private Func<TSource, CancellationToken, Task<TResult>> Transform { get; }

		/// <inheritdoc />
		public override CancellationToken Cancellation => this.Source.Cancellation;

		private IAsyncEnumerator<TSource>? Iterator { get; set; }

		/// <inheritdoc />
		protected override WhereSelectTaskAsyncIterator<TSource, TResult> Clone() => new(this.Source, this.PreFilter, this.Transform, this.PostFilter);

		/// <inheritdoc />
		protected override ValueTask<bool> OnFirstAsync()
		{
			this.Iterator = this.Source.GetAsyncEnumerator(m_mode);
			return new(true);
		}

		/// <inheritdoc />
		protected override async ValueTask<bool> OnNextAsync()
		{
			var iterator = this.Iterator;
			if (iterator == null)
			{
				return await Completed().ConfigureAwait(false);
			}

			var preFilter = this.PreFilter;
			var postFilter = this.PostFilter;
			var ct = this.Cancellation;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (preFilter == null || preFilter(item))
				{
					var value = await this.Transform(iterator.Current, ct).ConfigureAwait(false);
					if (postFilter == null || postFilter(value))
					{
						return Publish(value);
					}
				}
			}

			return await this.Completed().ConfigureAwait(false);
		}

		/// <inheritdoc />
		protected override ValueTask Cleanup()
		{
			var iterator = this.Iterator;
			if (iterator == null) return default;

			this.Iterator = null;
			return iterator.DisposeAsync();
		}

		private async Task FillBufferAsync(Buffer<TResult> buffer)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var preFilter = this.PreFilter;
			var transform = this.Transform;
			var postFilter = this.PostFilter;
			var ct = this.Cancellation;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (preFilter == null || preFilter(item))
				{
					var value = await transform(item, ct).ConfigureAwait(false);
					if (postFilter == null || postFilter(value))
					{
						buffer.Add(value);
					}
				}
			}
		}

		/// <inheritdoc />
		public override async Task<TResult[]> ToArrayAsync()
		{
			//note: we assume that the source does not implement IAsyncLinqQuery<TResult> (otherwise, we would have called their own implementation, instead of this one!)

			var buffer = new Buffer<TResult>(pool: ArrayPool<TResult>.Shared);
			await FillBufferAsync(buffer).ConfigureAwait(false);
			return buffer.ToArrayAndClear();
		}

		/// <inheritdoc />
		public override async Task<List<TResult>> ToListAsync()
		{
			//note: we assume that the source does not implement IAsyncLinqQuery<TResult> (otherwise, we would have called their own implementation, instead of this one!)

			var buffer = new Buffer<TResult>(pool: ArrayPool<TResult>.Shared);
			await FillBufferAsync(buffer).ConfigureAwait(false);
			return buffer.ToListAndClear();
		}

		/// <inheritdoc />
		public override async Task<ImmutableArray<TResult>> ToImmutableArrayAsync()
		{
			//note: we assume that the source does not implement IAsyncLinqQuery<TResult> (otherwise, we would have called their own implementation, instead of this one!)

			var buffer = new Buffer<TResult>(pool: ArrayPool<TResult>.Shared);
			await FillBufferAsync(buffer).ConfigureAwait(false);
			return buffer.ToImmutableArrayAndClear();
		}

		/// <inheritdoc />
		public override IAsyncLinqQuery<TNew> Select<TNew>(Func<TResult, TNew> selector)
		{
			if (this.PostFilter != null)
			{
				// we cannot easily combine the pre- and post-filter without calling the original selector twice!
				return new WhereSelectAsyncIterator<TResult, TNew>(this, null, selector, null);
			}

			return new WhereSelectTaskAsyncIterator<TSource, TNew>(this.Source, this.PreFilter, Combine(this.Transform, selector), null);

			static Func<TSource, CancellationToken, Task<TNew>> Combine(Func<TSource, CancellationToken, Task<TResult>> inner, Func<TResult, TNew> outer) => async (item, ct) => outer(await inner(item, ct).ConfigureAwait(false));
		}

		/// <inheritdoc />
		public override IAsyncLinqQuery<TNew> Select<TNew>(Func<TResult, CancellationToken, Task<TNew>> selector)
		{
			if (this.PostFilter != null)
			{
				// we cannot easily combine the pre- and post-filter without calling the original selector twice!
				return new WhereSelectTaskAsyncIterator<TResult, TNew>(this, null, selector, null);
			}

			return new WhereSelectTaskAsyncIterator<TSource, TNew>(this.Source, this.PreFilter, Combine(this.Transform, selector), null);

			static Func<TSource, CancellationToken, Task<TNew>> Combine(Func<TSource, CancellationToken, Task<TResult>> inner, Func<TResult, CancellationToken, Task<TNew>> outer) => async (item, ct) => await outer(await inner(item, ct).ConfigureAwait(false), ct).ConfigureAwait(false);
		}

		/// <inheritdoc />
		public override WhereSelectTaskAsyncIterator<TSource, TResult> Where(Func<TResult, bool> predicate)
		{
			return new(this.Source, this.PreFilter, this.Transform, this.PostFilter == null ? predicate : Combine(this.PostFilter, predicate));

			static Func<TResult, bool> Combine(Func<TResult, bool> first, Func<TResult, bool> second) => (item) => first(item) && second(item);
		}

	}

}
