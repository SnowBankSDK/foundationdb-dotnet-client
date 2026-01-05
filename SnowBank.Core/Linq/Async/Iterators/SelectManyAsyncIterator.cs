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
	internal sealed class SelectManyExpressionAsyncIterator<TSource, TResult> : AsyncFilterIterator<TSource, TResult>
	{

		private readonly AsyncTransformExpression<TSource, IEnumerable<TResult>> m_selector;

		private IEnumerator<TResult>? m_batch;

		public SelectManyExpressionAsyncIterator(IAsyncQuery<TSource> source, AsyncTransformExpression<TSource, IEnumerable<TResult>> selector)
			: base(source)
		{
			// Must have at least one, but not both
			Contract.Debug.Requires(selector != null);

			m_selector = selector;
		}

		protected override AsyncLinqIterator<TResult> Clone()
		{
			return new SelectManyExpressionAsyncIterator<TSource, TResult>(m_source, m_selector);
		}

		protected override async ValueTask<bool> OnNextAsync()
		{
			// if we are in a batch, iterate over it
			// if not, wait for the next batch

			var ct = this.Cancellation;
			var iterator = m_iterator;
			Contract.Debug.Requires(iterator != null);

			while (!ct.IsCancellationRequested)
			{

				if (m_batch == null)
				{

					if (!await iterator.MoveNextAsync().ConfigureAwait(false))
					{ // inner completed
						return await Completed().ConfigureAwait(false);
					}

					if (ct.IsCancellationRequested) break;

					IEnumerable<TResult> sequence;
					if (!m_selector.Async)
					{
						sequence = m_selector.Invoke(iterator.Current);
					}
					else
					{
						sequence = await m_selector.InvokeAsync(iterator.Current, ct).ConfigureAwait(false);
					}
					if (sequence == null) throw new InvalidOperationException("The inner sequence returned a null collection");

					m_batch = sequence.GetEnumerator();
					Contract.Debug.Assert(m_batch != null);
				}

				if (!m_batch.MoveNext())
				{ // the current batch is exhausted, move to the next
					m_batch.Dispose();
					m_batch = null;
					continue;
				}

				return Publish(m_batch.Current);
			}

			return await Canceled().ConfigureAwait(false);
		}

		protected override async ValueTask Cleanup()
		{
			try
			{
				m_batch?.Dispose();
			}
			finally
			{
				m_batch = null;
				await base.Cleanup().ConfigureAwait(false);
			}
		}
	}

	/// <summary>Iterates over an async sequence of items</summary>
	/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
	/// <typeparam name="TCollection">Type of the elements of the sequences produced from each <typeparamref name="TSource"/> elements</typeparam>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	internal sealed class SelectManyExpressionAsyncIterator<TSource, TCollection, TResult> : AsyncFilterIterator<TSource, TResult>
	{
		private readonly AsyncTransformExpression<TSource, IEnumerable<TCollection>> m_collectionSelector;
		private readonly Func<TSource, TCollection, TResult> m_resultSelector;
		private TSource? m_sourceCurrent;
		private IEnumerator<TCollection>? m_batch;

		public SelectManyExpressionAsyncIterator(
			IAsyncQuery<TSource> source,
			AsyncTransformExpression<TSource, IEnumerable<TCollection>> collectionSelector,
			Func<TSource, TCollection, TResult> resultSelector
		)
			: base(source)
		{
			Contract.Debug.Requires(collectionSelector != null && resultSelector != null);

			m_collectionSelector = collectionSelector;
			m_resultSelector = resultSelector;
		}

		protected override AsyncLinqIterator<TResult> Clone()
		{
			return new SelectManyExpressionAsyncIterator<TSource, TCollection, TResult>(m_source, m_collectionSelector, m_resultSelector);
		}

		protected override async ValueTask<bool> OnNextAsync()
		{
			// if we are in a batch, iterate over it
			// if not, wait for the next batch

			var ct = this.Cancellation;
			var iterator = m_iterator;
			Contract.Debug.Requires(iterator != null);

			while (!ct.IsCancellationRequested)
			{
				var batch = m_batch;
				if (batch == null)
				{

					if (!await iterator.MoveNextAsync().ConfigureAwait(false))
					{ // inner completed
						return await Completed().ConfigureAwait(false);
					}

					if (ct.IsCancellationRequested) break;

					m_sourceCurrent = iterator.Current;

					IEnumerable<TCollection> sequence;

					if (!m_collectionSelector.Async)
					{
						// ReSharper disable once MethodHasAsyncOverloadWithCancellation
						sequence = m_collectionSelector.Invoke(m_sourceCurrent);
					}
					else
					{
						sequence = await m_collectionSelector.InvokeAsync(m_sourceCurrent, ct).ConfigureAwait(false);
					}
					if (sequence == null) throw new InvalidOperationException("The inner sequence returned a null collection");

					m_batch = batch = sequence.GetEnumerator();
					Contract.Debug.Requires(batch != null);
				}

				if (!batch.MoveNext())
				{ // the current batch is exhausted, move to the next
					batch.Dispose();
					m_batch = null;
					m_sourceCurrent = default;
					continue;
				}

				return Publish(m_resultSelector(m_sourceCurrent!, batch.Current));
			}

			return await Canceled().ConfigureAwait(false);
		}

		protected override async ValueTask Cleanup()
		{
			try
			{ // cleanup any pending batch
				m_batch?.Dispose();
			}
			finally
			{
				m_batch = null;
				await base.Cleanup().ConfigureAwait(false);
			}
		}

	}

	/// <summary>Iterates over an async sequence of items</summary>
	/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	internal sealed class SelectManyExpressionAsyncEnumerableIterator<TSource, TResult> : AsyncFilterIterator<TSource, TResult>
	{

		private readonly AsyncTransformExpression<TSource, IAsyncEnumerable<TResult>> m_selector;

		private IAsyncEnumerator<TResult>? m_batch;

		public SelectManyExpressionAsyncEnumerableIterator(IAsyncQuery<TSource> source, AsyncTransformExpression<TSource, IAsyncEnumerable<TResult>> selector)
			: base(source)
		{
			// Must have at least one, but not both
			Contract.Debug.Requires(selector != null);

			m_selector = selector;
		}

		protected override AsyncLinqIterator<TResult> Clone()
		{
			return new SelectManyExpressionAsyncEnumerableIterator<TSource, TResult>(m_source, m_selector);
		}

		protected override async ValueTask<bool> OnNextAsync()
		{
			// if we are in a batch, iterate over it
			// if not, wait for the next batch

			var ct = this.Cancellation;
			var iterator = m_iterator;
			Contract.Debug.Requires(iterator != null);

			while (!ct.IsCancellationRequested)
			{

				if (m_batch == null)
				{

					if (!await iterator.MoveNextAsync().ConfigureAwait(false))
					{ // inner completed
						return await Completed().ConfigureAwait(false);
					}

					if (ct.IsCancellationRequested) break;

					IAsyncEnumerable<TResult> sequence;
					if (!m_selector.Async)
					{
						// ReSharper disable once MethodHasAsyncOverloadWithCancellation
						sequence = m_selector.Invoke(iterator.Current);
					}
					else
					{
						sequence = await m_selector.InvokeAsync(iterator.Current, ct).ConfigureAwait(false);
					}
					if (sequence == null) throw new InvalidOperationException("The inner sequence returned a null collection");

					m_batch = sequence.GetAsyncEnumerator(ct);
					Contract.Debug.Assert(m_batch != null);
				}

				if (!await m_batch.MoveNextAsync().ConfigureAwait(false))
				{ // the current batch is exhausted, move to the next
					await m_batch.DisposeAsync().ConfigureAwait(false);
					m_batch = null;
					continue;
				}

				return Publish(m_batch.Current);
			}

			return await Canceled().ConfigureAwait(false);
		}

		protected override async ValueTask Cleanup()
		{
			try
			{
				if (m_batch != null)
				{
					await m_batch.DisposeAsync().ConfigureAwait(false);
				}
			}
			finally
			{
				m_batch = null;
				await base.Cleanup().ConfigureAwait(false);
			}
		}
	}

	/// <summary>Iterates over an async sequence of items</summary>
	/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	internal sealed class SelectManyExpressionAsyncQueryIterator<TSource, TResult> : AsyncFilterIterator<TSource, TResult>
	{

		private readonly AsyncTransformExpression<TSource, IAsyncQuery<TResult>> m_selector;

		private IAsyncEnumerator<TResult>? m_batch;

		public SelectManyExpressionAsyncQueryIterator(IAsyncQuery<TSource> source, AsyncTransformExpression<TSource, IAsyncQuery<TResult>> selector)
			: base(source)
		{
			// Must have at least one, but not both
			Contract.Debug.Requires(selector != null);

			m_selector = selector;
		}

		protected override AsyncLinqIterator<TResult> Clone()
		{
			return new SelectManyExpressionAsyncQueryIterator<TSource, TResult>(m_source, m_selector);
		}

		protected override async ValueTask<bool> OnNextAsync()
		{
			// if we are in a batch, iterate over it
			// if not, wait for the next batch

			var ct = this.Cancellation;
			var iterator = m_iterator;
			Contract.Debug.Requires(iterator != null);

			while (!ct.IsCancellationRequested)
			{

				if (m_batch == null)
				{

					if (!await iterator.MoveNextAsync().ConfigureAwait(false))
					{ // inner completed
						return await Completed().ConfigureAwait(false);
					}

					if (ct.IsCancellationRequested) break;

					IAsyncQuery<TResult> sequence;
					if (!m_selector.Async)
					{
						// ReSharper disable once MethodHasAsyncOverloadWithCancellation
						sequence = m_selector.Invoke(iterator.Current);
					}
					else
					{
						sequence = await m_selector.InvokeAsync(iterator.Current, ct).ConfigureAwait(false);
					}
					if (sequence == null) throw new InvalidOperationException("The inner sequence returned a null collection");

					m_batch = sequence.GetAsyncEnumerator(m_mode);
					Contract.Debug.Assert(m_batch != null);
				}

				if (!await m_batch.MoveNextAsync().ConfigureAwait(false))
				{ // the current batch is exhausted, move to the next
					await m_batch.DisposeAsync().ConfigureAwait(false);
					m_batch = null;
					continue;
				}

				return Publish(m_batch.Current);
			}

			return await Canceled().ConfigureAwait(false);
		}

		protected override async ValueTask Cleanup()
		{
			try
			{
				if (m_batch != null)
				{
					await m_batch.DisposeAsync().ConfigureAwait(false);
				}
			}
			finally
			{
				m_batch = null;
				await base.Cleanup().ConfigureAwait(false);
			}
		}
	}

	/// <summary>Iterates over an async sequence of items</summary>
	/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	/// <remarks>This is simpler and faster version for the most usual scenario (paged queries)</remarks>
	internal sealed class SelectManyAsyncIterator<TSource, TResult> : AsyncLinqIterator<TResult>
	{

		public SelectManyAsyncIterator(IAsyncQuery<TSource> source, Func<TSource, IEnumerable<TResult>> selector)
		{
			Contract.Debug.Requires(source != null && selector != null);
			this.Source = source;
			this.Selector = selector;
		}

		private IAsyncQuery<TSource> Source { get; }

		private Func<TSource, IEnumerable<TResult>> Selector { get; }

		/// <inheritdoc />
		public override CancellationToken Cancellation => this.Source.Cancellation;

		private IAsyncEnumerator<TSource>? Iterator { get; set; }

		private IEnumerator<TResult>? Batch { get; set; }

		/// <inheritdoc />
		protected override SelectManyAsyncIterator<TSource, TResult> Clone() => new(this.Source, this.Selector);

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
			if (iterator == null) return await this.Completed().ConfigureAwait(false);

			var batch = this.Batch;
			var ct = this.Cancellation;
			while (!ct.IsCancellationRequested)
			{
				// get the next item from the batch
				if (batch != null && batch.MoveNext())
				{
					return Publish(batch.Current);
				}

				batch?.Dispose();
				// get the next batch from the inner iterator...
				if (!await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					// we are done!
					this.Batch = null;
					return await this.Completed().ConfigureAwait(false);
				}

				batch = this.Selector(iterator.Current).GetEnumerator();
				this.Batch = batch;
			}

			return await this.Canceled().ConfigureAwait(false);
		}

		/// <inheritdoc />
		protected override ValueTask Cleanup()
		{
			var batch = this.Batch;
			if (batch != null)
			{
				batch.Dispose();
				this.Batch = null;
			}

			var iterator = this.Iterator;
			if (iterator == null) return default;

			this.Iterator = null;
			return iterator.DisposeAsync();
		}

		private async Task FillBufferAsync(Buffer<TResult> buffer)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.All);

			var selector = this.Selector;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				buffer.AddRange(selector(iterator.Current));
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
		public override async Task<bool> AnyAsync()
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.Iterator);
			//note: we don't use "Head" here, because the first batch could be empty!

			var selector = this.Selector;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var batch = selector(iterator.Current);
				if (batch.Any())
				{
					return true;
				}
			}

			return false;
		}

		/// <inheritdoc />
		public override async Task<bool> AnyAsync(Func<TResult, bool> predicate)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.Iterator);
			//note: we don't use "Head" here, because the first batch could be empty!

			var selector = this.Selector;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var batch = selector(iterator.Current);
				if (batch.Any(predicate))
				{
					return true;
				}
			}

			return false;
		}

		/// <inheritdoc />
		public override async Task<int> CountAsync()
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.Iterator);
			//note: we don't use "Head" here, because the first batch could be empty!


			int total = 0;
			var selector = this.Selector;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var batch = selector(iterator.Current);
				checked { total += batch.Count(); }
			}

			return total;
		}

		/// <inheritdoc />
		public override async Task<int> CountAsync(Func<TResult, bool> predicate)
		{
			await using var iterator = this.Source.GetAsyncEnumerator(AsyncIterationHint.Iterator);
			//note: we don't use "Head" here, because the first batch could be empty!


			int total = 0;
			var selector = this.Selector;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var batch = selector(iterator.Current);
				checked { total += batch.Count(predicate); }
			}

			return total;
		}

	}

}
