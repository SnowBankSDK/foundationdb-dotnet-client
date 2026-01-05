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

namespace SnowBank.Linq
{
	using System.Buffers;
	using System.Collections.Immutable;
	using System.Threading;
	using System.Threading.Tasks;
	using SnowBank.Buffers;
	using SnowBank.Linq.Async.Iterators;

	/// <summary>Provides a set of static methods for querying objects that implement <see cref="IAsyncEnumerable{T}"/>.</summary>
	[PublicAPI]
	public static partial class AsyncQuery
	{
		#region Staying in the Monad...

		#region Parallel/Prefetch

		/// <summary>Projects each element of an async sequence into a new form, allowing concurrent execution.</summary>
		/// <remarks>
		/// <para>This method can process multiple elements concurrently which could complete out of order, but the output will keep in the same ordering as the source query.</para>
		/// <para>The maximum number of current tasks can be controlled via <see cref="ParallelAsyncQueryOptions.MaxConcurrency"/>.</para>
		/// <para>The <see cref="TaskScheduler"/> that is used to process each element can be controller via <see cref="ParallelAsyncQueryOptions.Scheduler"/>.</para>
		/// </remarks>
		/// <seealso cref="Select{TSource,TResult}(IAsyncQuery{TSource},Func{TSource,CancellationToken,Task{TResult}})" />
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> SelectParallel<TSource, TResult>(this IAsyncQuery<TSource> source, Func<TSource, CancellationToken, Task<TResult>> asyncSelector, ParallelAsyncQueryOptions? options = null)
		{
			Contract.NotNull(source);
			Contract.NotNull(asyncSelector);

			return new ParallelAsyncIterator<TSource, TResult>(source, asyncSelector, options ?? new ParallelAsyncQueryOptions());
		}

		/// <summary>Always prefetch the next item from the inner sequence.</summary>
		/// <typeparam name="TSource">Type of the items in the source sequence</typeparam>
		/// <param name="source">Source sequence that has a high latency, and from which we want to prefetch a set number of items.</param>
		/// <returns>Sequence that prefetch the next item, when outputting the current item.</returns>
		/// <remarks>
		/// This iterator can help smooth out the query pipeline when every call to the inner sequence has a somewhat high latency (ex: reading the next page of results from the database).
		/// Avoid pre-fetching from a source that is already reading from a buffer of results.
		/// </remarks>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TSource> Prefetch<TSource>(this IAsyncQuery<TSource> source)
		{
			Contract.NotNull(source);

			return new PrefetchingAsyncIterator<TSource>(source, 1);
		}

		/// <summary>Always prefetch the next item from the inner sequence.</summary>
		/// <typeparam name="TSource">Type of the items in the source sequence</typeparam>
		/// <param name="source">Source sequence that has a high latency, and from which we want to prefetch a set number of items.</param>
		/// <param name="ct">Cancellation token for this query</param>
		/// <returns>Sequence that prefetch the next item, when outputting the current item.</returns>
		/// <remarks>
		/// This iterator can help smooth out the query pipeline when every call to the inner sequence has a somewhat high latency (ex: reading the next page of results from the database).
		/// Avoid pre-fetching from a source that is already reading from a buffer of results.
		/// </remarks>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TSource> Prefetch<TSource>(this IAsyncEnumerable<TSource> source, CancellationToken ct = default)
		{
			Contract.NotNull(source);
			return new PrefetchingAsyncIterator<TSource>(source.ToAsyncQuery(ct), 1);
		}
		/// <summary>Prefetch a certain number of items from the inner sequence, before outputting the results one by one.</summary>
		/// <typeparam name="TSource">Type of the items in the source sequence</typeparam>
		/// <param name="source">Source sequence that has a high latency, and from which we want to prefetch a set number of items.</param>
		/// <param name="prefetchCount">Maximum number of items to buffer from the source before they are consumed by the rest of the query.</param>
		/// <returns>Sequence that returns items from a buffer of pre-fetched list.</returns>
		/// <remarks>
		/// This iterator can help smooth out the query pipeline when every call to the inner sequence has a somewhat high latency (ex: reading the next page of results from the database).
		/// Avoid pre-fetching from a source that is already reading from a buffer of results.
		/// </remarks>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TSource> Prefetch<TSource>(this IAsyncQuery<TSource> source, int prefetchCount)
		{
			Contract.NotNull(source);
			if (prefetchCount <= 0) throw new ArgumentOutOfRangeException(nameof(prefetchCount), prefetchCount, "Prefetch count must be at least one.");

			return new PrefetchingAsyncIterator<TSource>(source, prefetchCount);
		}

		/// <summary>Buffers the items of a bursty sequence, into a sequence of variable-sized arrays made up of items that where produced in a very short timespan.</summary>
		/// <typeparam name="TSource">Type of the items in the source sequence</typeparam>
		/// <param name="source">Source sequence, that produces bursts of items, produced from the same page of results, before reading the next page.</param>
		/// <param name="maxWindowSize">Maximum number of items to return in a single window. If more items arrive at the same time, a new window will be opened with the rest of the items.</param>
		/// <returns>Sequence of batches, where all the items of a single batch arrived at the same time. A batch is closed once the next call to MoveNext() on the inner sequence does not complete immediately. Batches can be smaller than <paramref name="maxWindowSize"/>.</returns>
		/// <remarks>
		/// This should only be called on bursty asynchronous sequences, and when you want to process items in batches, without incurring the cost of latency between two pages of results.
		/// You should avoid using this operator on sequences where each call to MoveNext() is asynchronous, since it would only produce batches with only a single item.
		/// </remarks>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TSource[]> Window<TSource>(this IAsyncQuery<TSource> source, int maxWindowSize)
		{
			Contract.NotNull(source);
			if (maxWindowSize <= 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(maxWindowSize), maxWindowSize, "Window size must be at least one.");

			return new WindowingAsyncIterator<TSource>(source, maxWindowSize);
		}

		/// <summary>Buffers the items of a source sequence, and outputs a sequence of fixed-sized arrays.</summary>
		/// <typeparam name="TSource">Type of the items in the source sequence</typeparam>
		/// <param name="source">Source sequence that will be cut into chunks containing at most <paramref name="batchSize"/> items.</param>
		/// <param name="batchSize">Number of items per batch. The last batch may contain fewer items, but should never be empty.</param>
		/// <returns>Sequence of arrays of size <paramref name="batchSize"/>, except the last batch which can have fewer items.</returns>
		/// <remarks>
		/// This operator does not care about the latency of each item, and will always try to fill each batch completely, before outputting a result.
		/// If you are working on an inner sequence that is bursty in nature, where items arrives in waves, you should use <see cref="Window{TSource}"/> which attempts to minimize the latency by outputting incomplete batches if needed.
		/// </remarks>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TSource[]> Batch<TSource>(this IAsyncQuery<TSource> source, int batchSize)
		{
			Contract.NotNull(source);
			if (batchSize <= 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Batch size must be at least one.");

			return new BatchingAsyncIterator<TSource>(source, batchSize);
		}

		#endregion

		#region Distinct...

		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TSource> Distinct<TSource>(this IAsyncQuery<TSource> source, IEqualityComparer<TSource>? comparer = null)
		{
			Contract.NotNull(source);
			return new DistinctAsyncIterator<TSource>(source, comparer ?? EqualityComparer<TSource>.Default);
		}

		#endregion

		#region OrderBy...

		[Pure, LinqTunnel]
		public static IOrderedAsyncQuery<TSource> OrderBy<TSource, TKey>(this IAsyncQuery<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey>? comparer = null)
		{
			Contract.NotNull(source);
			Contract.NotNull(keySelector);

			return new OrderedAsyncQuery<TSource, TKey>(source, keySelector, comparer ?? Comparer<TKey>.Default, descending: false, parent: null);
		}

		[Pure, LinqTunnel]
		public static IOrderedAsyncQuery<TSource> OrderByDescending<TSource, TKey>(this IAsyncQuery<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey>? comparer = null)
		{
			Contract.NotNull(source);
			Contract.NotNull(keySelector);

			return new OrderedAsyncQuery<TSource, TKey>(source, keySelector, comparer ?? Comparer<TKey>.Default, descending: true, parent: null);
		}

		[Pure, LinqTunnel]
		public static IOrderedAsyncQuery<TSource> ThenBy<TSource, TKey>(this IOrderedAsyncQuery<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey>? comparer = null)
		{
			Contract.NotNull(source);
			return source.CreateOrderedEnumerable(keySelector, comparer, descending: false);
		}

		[Pure, LinqTunnel]
		public static IOrderedAsyncQuery<TSource> ThenByDescending<TSource, TKey>(this IOrderedAsyncQuery<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey>? comparer = null)
		{
			Contract.NotNull(source);
			return source.CreateOrderedEnumerable(keySelector, comparer, descending: true);
		}

		#endregion

		// If you are bored, maybe consider adding:
		// - DefaultIfEmpty<T>
		// - Zip<T>
		// - OrderBy<TElement> and OrderBy<TElement, TKey>
		// - GroupBy<TKey, TElement>

		#endregion

		#region Leaving the Monad...

		/// <summary>Execute an action for each element of an async sequence</summary>
		public static Task ForEachAsync<TElement>(this IAsyncQuery<TElement> source, [InstantHandle] Action<TElement> action)
		{
			Contract.NotNull(source);
			Contract.NotNull(action);

			return source switch
			{
				AsyncLinqIterator<TElement> iterator => iterator.ExecuteAsync(action),
				_ => Run(source, AsyncIterationHint.All, action)
			};
		}

		/// <summary>Executes an action for each element of an async sequence</summary>
		public static Task ForEachAsync<TState, TElement>(this IAsyncQuery<TElement> source, TState state, [InstantHandle] Action<TState, TElement> action)
		{
			Contract.NotNull(source);
			Contract.NotNull(action);

			return source switch
			{
				AsyncLinqIterator<TElement> iterator => iterator.ExecuteAsync(state, action),
				_ => Run(source, AsyncIterationHint.All, state, action)
			};
		}

		/// <summary>Executes an async action for each element of an async sequence</summary>
		public static Task ForEachAsync<TElement>(this IAsyncQuery<TElement> source, [InstantHandle] Func<TElement, Task> asyncAction)
		{
			Contract.NotNull(asyncAction);

			return source switch
			{
				AsyncLinqIterator<TElement> iterator => iterator.ExecuteAsync((value, ct) =>
				{
					if (ct.IsCancellationRequested) return Task.FromCanceled(ct);
					return asyncAction(value);
				}),
				_ => ForEachAsync(source, (value, ct) =>
				{
					if (ct.IsCancellationRequested) return Task.FromCanceled(ct);
					return asyncAction(value);
				})
			};
		}

		/// <summary>Executes an async action for each element of an async sequence</summary>
		public static Task ForEachAsync<TElement>(this IAsyncQuery<TElement> source, [InstantHandle] Func<TElement, CancellationToken, Task> asyncAction)
		{
			Contract.NotNull(source);
			Contract.NotNull(asyncAction);

			return source switch
			{
				AsyncLinqIterator<TElement> iterator => iterator.ExecuteAsync(asyncAction),
				_ => Run(source, AsyncIterationHint.All, asyncAction)
			};
		}

		#region ToList/Array/Dictionary/HashSet...

		/// <summary>Creates a list from an async sequence.</summary>
		public static Task<List<T>> ToListAsync<T>(this IAsyncQuery<T> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.ToListAsync();
			}

			return AggregateAsync(
				source,
				new Buffer<T>(0, ArrayPool<T>.Shared),
				static (b, x) => b.Add(x),
				static (b) => b.ToListAndClear()
			);
		}

		/// <summary>Creates a list from an async sequence.</summary>
		public static Task<ImmutableArray<T>> ToImmutableArrayAsync<T>(this IAsyncQuery<T> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.ToImmutableArrayAsync();
			}

			return AggregateAsync(
				source,
				new Buffer<T>(0, ArrayPool<T>.Shared),
				static (b, x) => b.Add(x),
				static (b) => b.ToImmutableArrayAndClear()
			);
		}

		/// <summary>Creates an array from an async sequence.</summary>
		public static Task<T[]> ToArrayAsync<T>(this IAsyncQuery<T> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.ToArrayAsync();
			}

			return AggregateAsync(
				source,
				new Buffer<T>(0, ArrayPool<T>.Shared),
				static (b, x) => b.Add(x),
				static (b) => b.ToArrayAndClear()
			);
		}

		/// <summary>Creates a Dictionary from an async sequence according to a specified key selector function and key comparer.</summary>
		public static Task<Dictionary<TKey, TSource>> ToDictionaryAsync<TSource, TKey>(this IAsyncQuery<TSource> source, [InstantHandle] Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer = null)
			where TKey: notnull
		{
			Contract.NotNull(source);
			Contract.NotNull(keySelector);

			if (source is IAsyncLinqQuery<TSource> query)
			{
				return query.ToDictionaryAsync(keySelector, comparer);
			}

			return AggregateAsync(
				source,
				(
					Results: new Dictionary<TKey, TSource>(comparer),
					KeySelector: keySelector
				),
				static (s, x) => s.Results.Add(s.KeySelector(x), x),
				static (s) => s.Results
			);
		}

		/// <summary>Creates a Dictionary from an async sequence according to a specified key selector function, a comparer, and an element selector function.</summary>
		public static Task<Dictionary<TKey, TElement>> ToDictionaryAsync<TSource, TKey, TElement>(this IAsyncQuery<TSource> source, [InstantHandle] Func<TSource, TKey> keySelector, [InstantHandle] Func<TSource, TElement> elementSelector, IEqualityComparer<TKey>? comparer = null)
			where TKey: notnull
		{
			Contract.NotNull(keySelector);
			Contract.NotNull(elementSelector);

			if (source is IAsyncLinqQuery<TSource> query)
			{
				return query.ToDictionaryAsync(keySelector, elementSelector, comparer);
			}

			return AggregateAsync(
				source,
				(
					Results: new Dictionary<TKey, TElement>(comparer),
					KeySelector: keySelector,
					ElementSelector: elementSelector
				),
				static (s, x) => s.Results.Add(s.KeySelector(x), s.ElementSelector(x)),
				static (s) => s.Results
			);
		}

		/// <summary>Creates a Dictionary from an async sequence of pairs of keys and values.</summary>
		public static Task<Dictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue>(this IAsyncQuery<KeyValuePair<TKey, TValue>> source, IEqualityComparer<TKey>? comparer = null)
			where TKey: notnull
		{
			return ToDictionaryAsync(source, kv => kv.Key, kv => kv.Value, comparer);
		}

		/// <summary>Creates a Hashset from an async sequence.</summary>
		public static Task<HashSet<T>> ToHashSetAsync<T>(this IAsyncQuery<T> source, IEqualityComparer<T>? comparer = null)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.ToHashSetAsync(comparer);
			}

			return AggregateAsync(
				source,
				new Buffer<T>(0, ArrayPool<T>.Shared),
				(buffer, x) => buffer.Add(x),
				(buffer) => buffer.ToHashSetAndClear(comparer)
			);
		}

		#endregion

		#region ElemetAtAsync...

		/// <summary>Returns the element at a specific location of an async sequence, or an exception if there are not enough elements</summary>
		public static async Task<T> ElementAtAsync<T>(this IAsyncQuery<T> source, int index)
		{
			Contract.NotNull(source);
			if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));

			int counter = index;
			T item = default!;
			await Run(
				source,
				AsyncIterationHint.All,
				(x) =>
				{
					if (counter-- == 0) { item = x; return false; }
					return true;
				}
			).ConfigureAwait(false);

			if (counter >= 0) throw new InvalidOperationException("The sequence was too small");
			return item;
		}

		/// <summary>Returns the element at a specific location of an async sequence, or the default value for the type if there are not enough elements</summary>
		public static async Task<T> ElementAtOrDefaultAsync<T>(this IAsyncQuery<T> source, int index)
		{
			Contract.NotNull(source);
			if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));

			int counter = index;
			T item = default!;

			//TODO: use ExecuteAsync() if the source is an Iterator!
			await Run(
				source,
				AsyncIterationHint.All,
				(x) =>
				{
					if (counter-- == 0) { item = x; return false; }
					return true;
				}
			).ConfigureAwait(false);

			if (counter >= 0) return default!;
			return item;
		}

		#endregion

		#region AsEnumerable...

		/// <summary>Helper method that checks that the cancellation token is the same as the source</summary>
		/// <exception cref="InvalidOperationException">If the token is different</exception>
		/// <remarks>This is used to simplify the pattern of adapting <see cref="IAsyncEnumerable{T}.GetAsyncEnumerator"/> calls.</remarks>
		[MustDisposeResource]
		public static IAsyncEnumerator<T> GetCancellableAsyncEnumerator<T>(IAsyncQuery<T> query, AsyncIterationHint hint, CancellationToken ct)
		{
			if (ct.CanBeCanceled && !ct.Equals(query.Cancellation))
			{
				throw new InvalidOperationException("The CancellationToken that is passed to GetAsyncEnumerator() MUST be the same as the source!");
			}
			return query.GetAsyncEnumerator(hint);
		}

		#endregion

		#endregion

		#region Query Statistics...

		//TODO: move this somewhere else?

		/// <summary>Measure the number of items that pass through this point of the query</summary>
		/// <remarks>The values returned in <paramref name="counter"/> are only safe to read once the query has ended</remarks>
		[LinqTunnel]
		public static IAsyncLinqQuery<TSource> WithCountStatistics<TSource>(this IAsyncQuery<TSource> source, out QueryStatistics<int> counter)
		{
			Contract.NotNull(source);

			var signal = new QueryStatistics<int>(0);
			counter = signal;

			// to count, we just increment the signal each type a value flows through here
			return Select(source, (x) =>
			{
				signal.Update(checked(signal.Value + 1));
				return x;
			});
		}

		/// <summary>Measure the number and size of slices that pass through this point of the query</summary>
		/// <remarks>The values returned in <paramref name="statistics"/> are only safe to read once the query has ended</remarks>
		[LinqTunnel]
		public static IAsyncLinqQuery<KeyValuePair<Slice, Slice>> WithSizeStatistics(this IAsyncQuery<KeyValuePair<Slice, Slice>> source, out QueryStatistics<KeyValueSizeStatistics> statistics)
		{
			Contract.NotNull(source);

			var data = new KeyValueSizeStatistics();
			statistics = new(data);

			// to count, we just increment the signal each type a value flows through here
			return Select(source,(kvp) =>
			{
				data.Add(kvp.Key.Count, kvp.Value.Count);
				return kvp;
			});
		}

		/// <summary>Measure the number and sizes of the keys and values that pass through this point of the query</summary>
		/// <remarks>The values returned in <paramref name="statistics"/> are only safe to read once the query has ended</remarks>
		[LinqTunnel]
		public static IAsyncLinqQuery<Slice> WithSizeStatistics(this IAsyncQuery<Slice> source, out QueryStatistics<DataSizeStatistics> statistics)
		{
			Contract.NotNull(source);

			var data = new DataSizeStatistics();
			statistics = new(data);

			// to count, we just increment the signal each type a value flows through here
			return Select(source, (x) =>
			{
				data.Add(x.Count);
				return x;
			});
		}

		/// <summary>Execute an action on each item passing through the sequence, without modifying the original sequence</summary>
		/// <remarks>The <paramref name="handler"/> is execute inline before passing the item down the line, and should not block</remarks>
		[LinqTunnel]
		public static IAsyncLinqQuery<TSource> Observe<TSource>(this IAsyncQuery<TSource> source, Action<TSource> handler)
		{
			Contract.NotNull(source);
			Contract.NotNull(handler);

			return new ObserverAsyncIterator<TSource>(source, new(handler));
		}

		/// <summary>Execute an action on each item passing through the sequence, without modifying the original sequence</summary>
		/// <remarks>The <paramref name="asyncHandler"/> is execute inline before passing the item down the line, and should not block</remarks>
		[LinqTunnel]
		public static IAsyncLinqQuery<TSource> Observe<TSource>(this IAsyncQuery<TSource> source, Func<TSource, CancellationToken, Task> asyncHandler)
		{
			Contract.NotNull(source);
			Contract.NotNull(asyncHandler);

			return new ObserverAsyncIterator<TSource>(source, new(asyncHandler));
		}

		#endregion

		#region Error Helpers...


		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static InvalidOperationException ErrorNoElements() => new("Sequence contains no elements");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static InvalidOperationException ErrorMoreThenOneElement() => new("Sequence contains more than one elements");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static InvalidOperationException ErrorNoMatch() => new("Sequence contains no matching element");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static InvalidOperationException ErrorMoreThanOneMatch() => new("Sequence contains more than one matching element");

		#endregion

	}

	/// <summary>Contains generic fallback implementations for custom async iterators</summary>
	public static partial class AsyncIterators
	{

		// each LINQ operator can provide a typical implementation in this type

	}

}
