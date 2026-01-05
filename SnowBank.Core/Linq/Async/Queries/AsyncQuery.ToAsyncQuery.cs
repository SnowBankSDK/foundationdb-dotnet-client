// #region Copyright (c) 2023-2026 SnowBank SAS
// //
// // All rights are reserved. Reproduction or transmission in whole or in part, in
// // any form or by any means, electronic, mechanical or otherwise, is prohibited
// // without the prior written consent of the copyright owner.
// //
// #endregion

namespace SnowBank.Linq
{
	using Async.Iterators;

	public static partial class AsyncQuery
	{

		#region IEnumerable<T>...

		/// <summary>Wraps an <see cref="IEnumerable{T}"/> into an <see cref="IAsyncLinqQuery{T}"/> that will return the same elements.</summary>
		/// <example><code>
		/// // starts with a "regular" IEnumerable&lt;T&gt;
		/// var items = Enumerable.Range(0, 10).Select(...).Where(...);
		/// // switch to an IAsyncQuery&lt;T>
		/// var query = await enumerable
		///		.ToAsyncQuery(stoppingToken) // inject the cancellation token here
		///		.Select(((x, ct) => SomeAsyncMethod(x, ct)) // call an async method
		///		.ToListAsync(); // return a list with the results
		/// </code></example>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<T> ToAsyncQuery<T>(this IEnumerable<T> source, CancellationToken ct)
		{
			Contract.NotNull(source);

			return new EnumerableIterator<T>(source, ct);
		}

		/// <summary>Wraps a sequence of items into an async sequence of items</summary>
		/// <typeparam name="TSource">Type of elements of the inner sequence</typeparam>
		internal sealed class EnumerableIterator<TSource> : AsyncLinqIterator<TSource>
		{

			public EnumerableIterator(IEnumerable<TSource> source, CancellationToken ct)
			{
				this.Source = source;
				this.Cancellation = ct;
			}

			private IEnumerable<TSource> Source { get; }

			public override CancellationToken Cancellation { get; }

			/// <inheritdoc />
			protected override EnumerableIterator<TSource> Clone() => new(this.Source, this.Cancellation);

			private IEnumerator<TSource>? m_inner;

			/// <inheritdoc />
			protected override ValueTask<bool> OnFirstAsync()
			{
				var inner = this.Source.GetEnumerator();
				Contract.Debug.Assert(inner != null, "The underlying sequence returned an empty enumerator.");
				m_inner = inner;
				return new(true);
			}

			/// <inheritdoc />
			protected override ValueTask<bool> OnNextAsync()
			{
				var inner = m_inner;
				if (inner == null || !inner.MoveNext())
				{
					return this.Completed();
				}
				return new(Publish(inner.Current));
			}

			/// <inheritdoc />
			protected override ValueTask Cleanup()
			{
				m_inner?.Dispose();
				m_inner = null;
				return default;
			}

			/// <inheritdoc />
			public override Task<int> CountAsync() => Task.FromResult(this.Source.Count());

			/// <inheritdoc />
			public override Task<int> CountAsync(Func<TSource, bool> predicate) => Task.FromResult(this.Source.Count(predicate));

			/// <inheritdoc />
			public override Task<bool> AnyAsync() => Task.FromResult(this.Source.Any());

			/// <inheritdoc />
			public override Task<bool> AnyAsync(Func<TSource, bool> predicate) => Task.FromResult(this.Source.Any(predicate));

			/// <inheritdoc />
			public override Task<bool> AllAsync(Func<TSource, bool> predicate) => Task.FromResult(this.Source.All(predicate));

			/// <inheritdoc />
			public override Task<TSource> FirstOrDefaultAsync(TSource defaultValue) => Task.FromResult(this.Source.FirstOrDefault(defaultValue));

			/// <inheritdoc />
			public override Task<TSource> FirstOrDefaultAsync(Func<TSource, bool> predicate, TSource defaultValue) => Task.FromResult(this.Source.FirstOrDefault(predicate, defaultValue));

			/// <inheritdoc />
			public override Task<TSource> FirstAsync() => Task.FromResult(this.Source.First());

			/// <inheritdoc />
			public override Task<TSource> FirstAsync(Func<TSource, bool> predicate) => Task.FromResult(this.Source.First(predicate));

			/// <inheritdoc />
			public override Task<TSource> LastOrDefaultAsync(TSource defaultValue) => Task.FromResult(this.Source.LastOrDefault(defaultValue));

			/// <inheritdoc />
			public override Task<TSource> LastOrDefaultAsync(Func<TSource, bool> predicate, TSource defaultValue) => Task.FromResult(this.Source.LastOrDefault(predicate, defaultValue));

			/// <inheritdoc />
			public override Task<TSource> LastAsync() => Task.FromResult(this.Source.Last());

			/// <inheritdoc />
			public override Task<TSource> LastAsync(Func<TSource, bool> predicate) => Task.FromResult(this.Source.Last(predicate));

			/// <inheritdoc />
			public override Task<TSource> SingleAsync() => Task.FromResult(this.Source.Single());

			/// <inheritdoc />
			public override Task<TSource> SingleAsync(Func<TSource, bool> predicate) => Task.FromResult(this.Source.Single(predicate));

			/// <inheritdoc />
			public override Task<TSource[]> ToArrayAsync() => Task.FromResult(this.Source.ToArray());

			/// <inheritdoc />
			public override Task<List<TSource>> ToListAsync() => Task.FromResult(this.Source.ToList());

			/// <inheritdoc />
			public override Task<Dictionary<TKey, TSource>> ToDictionaryAsync<TKey>(Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer = null)
				=> Task.FromResult(this.Source.ToDictionary(keySelector, comparer));

			/// <inheritdoc />
			public override Task<Dictionary<TKey, TElement>> ToDictionaryAsync<TKey, TElement>(Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey>? comparer = null)
				=> Task.FromResult(this.Source.ToDictionary(keySelector, elementSelector, comparer));

			/// <inheritdoc />
			public override Task<HashSet<TSource>> ToHashSetAsync(IEqualityComparer<TSource>? comparer = null) => Task.FromResult(this.Source.ToHashSet(comparer));

			/// <inheritdoc />
			public override IAsyncLinqQuery<TSource> Skip(int count) => new EnumerableIterator<TSource>(this.Source.Skip(count), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<TSource> Take(int count) => new EnumerableIterator<TSource>(this.Source.Take(count), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<TSource> TakeWhile(Func<TSource, bool> condition) => new EnumerableIterator<TSource>(this.Source.TakeWhile(condition), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> Select<TNew>(Func<TSource, TNew> selector)
				=> new EnumerableIterator<TNew>(this.Source.Select(selector), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> Select<TNew>(Func<TSource, int, TNew> selector)
				=> new EnumerableIterator<TNew>(this.Source.Select(selector), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<TSource, IEnumerable<TNew>> selector)
				=> new EnumerableIterator<TNew>(this.Source.SelectMany(selector), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> SelectMany<TCollection, TNew>(Func<TSource, IEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TNew> resultSelector)
				=> new EnumerableIterator<TNew>(this.Source.SelectMany(collectionSelector, resultSelector), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<TSource> Where(Func<TSource, bool> predicate) => new EnumerableIterator<TSource>(this.Source.Where(predicate), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<TSource> Where(Func<TSource, int, bool> predicate) => new EnumerableIterator<TSource>(this.Source.Where(predicate), this.Cancellation);

		}

		/// <summary>Wraps an async lambda into an async sequence that will return the result of the lambda</summary>
		/// <example><code>
		/// async Task&lt;R> ProcessQuery&lt;T, R>(IAsyncQuery&lt;T> query) { ... }
		/// // creates a "singleton" query from the result of an async method, and pass this to a method that requires an IAsyncQuery&lt;T>
		/// var result = ProcessQuery(AsyncQuery.FromTask(SomeAsyncComputation(...), stoppingToken);
		/// </code></example>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<T> FromTask<T>(Func<CancellationToken, Task<T>> asyncLambda, CancellationToken ct)
		{
			//TODO: create a custom iterator for this ?
			return ToAsyncQuery([ asyncLambda ], ct).Select((x, cancel) => x(cancel));
		}

		#endregion

		#region IAsyncEnumerable<T>...

		/// <summary>Wraps an <see cref="IAsyncEnumerable{T}"/> into an <see cref="IAsyncLinqQuery{T}"/> that will return the same elements.</summary>
		/// <example><code>
		/// // we have an existing async enumerator that we want to use as a source
		/// async IAsyncEnumerable&lt;T> ComputeRange&lt;T>(int start, int count, Func&lt;int, Task&lt;T>> callback)
		/// {
		///		while(count-- > 0)
		///		{
		/// 
		///			yield return await callback(start++);
		///		}
		/// }
		/// 
		/// // but we need pass this to a method that wants an async query:
		/// Task&lt;T> PostProcess(IAsyncQuery&lt;T> query) { ... }
		/// 
		/// // we just need to wrap one type of async enumerable into another
		/// var query = ComputeRange(42, 25, (x) => /*...*/).ToAsyncQuery(stoppingToken);
		/// // and pass the resulting query to the post-processing step
		/// var result = await PostProcess(query);
		/// </code></example>
		public static IAsyncLinqQuery<T> ToAsyncQuery<T>(this IAsyncEnumerable<T> source, CancellationToken ct = default)
		{
			if (source is IAsyncLinqQuery<T> query)
			{
				if (ct.CanBeCanceled && !ct.Equals(query.Cancellation))
				{
					throw new InvalidOperationException("The CancellationToken that is passed to ToAsyncQuery() MUST be the same as the source!");
				}
				return query;
			}
			return new AsyncEnumerableIterator<T>(source, ct);
		}

		/// <summary>Wraps an <see cref="IAsyncEnumerable{T}"/> into an <see cref="IAsyncLinqQuery{T}"/></summary>
		internal sealed class AsyncEnumerableIterator<T> : AsyncLinqIterator<T>
		{

			private IAsyncEnumerable<T> Source { get; }

			private IAsyncEnumerator<T>? Iterator { get; set; }

			public AsyncEnumerableIterator(IAsyncEnumerable<T> source, CancellationToken ct)
			{
				this.Source = source;
				this.Cancellation = ct;
			}

			public override CancellationToken Cancellation { get; }

			/// <inheritdoc />
			protected override AsyncEnumerableIterator<T> Clone() => new(this.Source, this.Cancellation);

			/// <inheritdoc />
			protected override ValueTask<bool> OnFirstAsync()
			{
				this.Iterator = this.Source.GetAsyncEnumerator(this.Cancellation);
				return new(true);
			}

			/// <inheritdoc />
			protected override async ValueTask<bool> OnNextAsync()
			{
				var iterator = this.Iterator;
				if (iterator == null || !(await iterator.MoveNextAsync().ConfigureAwait(false)))
				{
					return await this.Completed().ConfigureAwait(false);
				}
				return Publish(iterator.Current);
			}

			/// <inheritdoc />
			protected override async ValueTask Cleanup()
			{
				var iterator = this.Iterator;
				this.Iterator = null;
				if (iterator != null)
				{
					await iterator.DisposeAsync().ConfigureAwait(false);
				}
			}

#if NET10_0_OR_GREATER

			/// <inheritdoc />
			public override Task<T[]> ToArrayAsync() => this.Source.ToArrayAsync(this.Cancellation).AsTask();

			/// <inheritdoc />
			public override Task<List<T>> ToListAsync() => this.Source.ToListAsync(this.Cancellation).AsTask();

			/// <inheritdoc />
			public override Task<bool> AnyAsync() => this.Source.AnyAsync(this.Cancellation).AsTask();

			/// <inheritdoc />
			public override Task<bool> AnyAsync(Func<T, bool> predicate) => this.Source.AnyAsync(predicate, this.Cancellation).AsTask();

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> Select<TNew>(Func<T, TNew> selector) => new AsyncEnumerableIterator<TNew>(this.Source.Select(selector), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNew> Select<TNew>(Func<T, int, TNew> selector) => new AsyncEnumerableIterator<TNew>(this.Source.Select(selector), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<T> Where(Func<T, bool> predicate) => new AsyncEnumerableIterator<T>(this.Source.Where(predicate), this.Cancellation);

			/// <inheritdoc />
			public override IAsyncLinqQuery<T> Where(Func<T, int, bool> predicate) => new AsyncEnumerableIterator<T>(this.Source.Where(predicate), this.Cancellation);

#endif

		}

		#endregion

	}

}
