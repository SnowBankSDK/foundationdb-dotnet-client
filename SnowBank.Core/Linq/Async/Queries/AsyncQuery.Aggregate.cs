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
	using SnowBank.Linq.Async.Iterators;

	public static partial class AsyncQuery
	{

		#region Aggregate...

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		/// <typeparam name="TSource">Type of the elements from the source async sequences</typeparam>
		/// <param name="source">Source async query</param>
		/// <param name="aggregator">Function that is called for each element as it arrives, starting from the second element.
		/// The first argument will the first element of the sequence and then the value returned by the previous invocation.
		/// The second argument will be the current element.
		/// The returned value will be passed to the next call, or will be the return value of the aggregation.
		/// </param>
		/// <returns>Last value returned by <paramref name="aggregator"/> if the query returns two or more elements. The element itself if the query returns only one element.</returns>
		/// <exception cref="InvalidOperationException">If the query returns no elements.</exception>
		public static async Task<TSource> AggregateAsync<TSource>(this IAsyncQuery<TSource> source, [InstantHandle] Func<TSource, TSource, TSource> aggregator)
		{
			Contract.NotNull(source);
			Contract.NotNull(aggregator);

			//TODO: this one is a bit different from the other aggregates: the 'seed' value is the value of first element, and the operation must throw if the query is empty.

			await using (var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All))
			{
				Contract.Debug.Assert(iterator != null, "The sequence returned a null async iterator");

				if (!(await iterator.MoveNextAsync().ConfigureAwait(false)))
				{
					throw ErrorNoElements();
				}

				var accumulator = iterator.Current;
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					accumulator = aggregator(accumulator, iterator.Current);
				}

				return accumulator;
			}
		}

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		/// <typeparam name="TSource">Type of the elements from the source async sequences</typeparam>
		/// <typeparam name="TAggregate">Type of the aggregate that is computed</typeparam>
		/// <param name="source">Source async query</param>
		/// <param name="seed">Initial value for the aggregate that is passed as the first argument to <paramref name="aggregator"/></param>
		/// <param name="aggregator">Function that is called for each element as it arrives. The result will be passed back to the next function call for the following element.</param>
		/// <example><code>
		/// // query that results a set of integers
		/// IAsyncQuery&lt;int> query = ...;
		/// // manually compute the sum of all integers
		/// long sum = await query.AggregateAsync(
		///		0L, // use a long for the sum
		///		(sum, x) => sum + x // add to the sum
		/// );
		/// </code></example>
		public static Task<TAggregate> AggregateAsync<TSource, TAggregate>(this IAsyncQuery<TSource> source, TAggregate seed, [InstantHandle] Func<TAggregate, TSource, TAggregate> aggregator)
		{
			Contract.NotNull(source);
			Contract.NotNull(aggregator);

			return source switch
			{
				AsyncLinqIterator<TSource> iterator => iterator.ExecuteAsync(aggregator, seed, static (callback, accumulate, x) => callback(accumulate, x)),
				_ => Run(source, AsyncIterationHint.All, aggregator, seed, static (callback, accumulate, x) => callback(accumulate, x))
			};
		}

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		/// <typeparam name="TSource">Type of the elements from the source async sequences</typeparam>
		/// <typeparam name="TAccumulator">Type of the accumulator that is updated with each element</typeparam>
		/// <param name="source">Source async query</param>
		/// <param name="accumulator">Reference that is passed as the first argument to <paramref name="aggregator"/>, usually a list or buffer of some kind.</param>
		/// <param name="aggregator">Action that is called for each element as it arrives. The action should mutate the accumulator (adding to a list or buffer, updating some state, ...).</param>
		/// <example><code>
		/// // query that results a set of strings, some may be empty
		/// IAsyncQuery&lt;string> query = ...;
		/// // buffer all the non-empty values
		/// List&lt;string> result = await query.AggregateAsync(
		///		[ ], // starts with an empty list
		///		(buffer, x) => { if (!string.IsNullOrEmpty(x)) buffer.Add(x) } // keep non-empty elements
		/// );
		/// </code></example>
		public static async Task<TAccumulator> AggregateAsync<TSource, TAccumulator>(this IAsyncQuery<TSource> source, TAccumulator accumulator, [InstantHandle] Action<TAccumulator, TSource> aggregator)
		{
			Contract.NotNull(source);
			Contract.NotNull(aggregator);

			await ForEachAsync(
				source,
				(Fn: aggregator, Acc: accumulator),
				static (s, x) => s.Fn(s.Acc, x)
			).ConfigureAwait(false);

			return accumulator;
		}

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		/// <typeparam name="TSource">Type of the elements from the source async sequences</typeparam>
		/// <typeparam name="TAggregate">Type of the intermediate aggregate that is computed on each element</typeparam>
		/// <typeparam name="TResult">Type of the result of the aggregation</typeparam>
		/// <param name="source">Source async query</param>
		/// <param name="seed">Initial value for the aggregate that is passed as the first argument to <paramref name="aggregator"/></param>
		/// <param name="aggregator">Function that is called for each element as it arrives. The result will be passed back to the next function call for the following element.</param>
		/// <param name="resultSelector">Function that is called with the last aggregate value (or <paramref name="seed"/> if the query is empty), and computes the final result.</param>
		/// <returns>The value returned by <paramref name="resultSelector"/>.</returns>
		/// <example><code>
		/// // query that returns a set of integers
		/// IAsyncQuery&lt;int> query = ...;
		/// // compute the average value of the results
		/// double average = await query.AggregateAsync(
		///     (Sum: 0L, Size: 0), // initial seed
		///     (acc, x) => (acc.Sum + x, acc.Size + 1), // sum the xs, and increment count
		///     (acc) => (double) acc.Sum / acc.Size // compute the average (expressed as a double)
		/// );
		/// </code></example>
		public static async Task<TResult> AggregateAsync<TSource, TAggregate, TResult>(this IAsyncQuery<TSource> source, TAggregate seed, [InstantHandle] Func<TAggregate, TSource, TAggregate> aggregator, [InstantHandle] Func<TAggregate, TResult> resultSelector)
		{
			Contract.NotNull(source);
			Contract.NotNull(aggregator);
			Contract.NotNull(resultSelector);

			// accumulate
			var accumulate = await AggregateAsync(source, seed, aggregator).ConfigureAwait(false);
			// transform
			return resultSelector(accumulate);
		}

		/// <summary>Applies an accumulator function over an async sequence.</summary>
		/// <typeparam name="TSource">Type of the elements from the source async sequences</typeparam>
		/// <typeparam name="TAccumulator">Type of the accumulator that is updated with each element</typeparam>
		/// <typeparam name="TResult">Type of the result of the aggregation</typeparam>
		/// <param name="source">Source async query</param>
		/// <param name="accumulator">Value that is passed as the first argument to <paramref name="aggregator"/></param>
		/// <param name="aggregator">Action that is called for each element as it arrives.</param>
		/// <param name="resultSelector">Function that is called with the last aggregate value (or <paramref name="accumulator"/> if the query is empty), and computes the final result.</param>
		/// <example><code>
		/// // query that returns a set of strings, in some deterministic order
		/// IAsyncQuery&lt;string> query = ...;
		/// // compute the aggregate hash of all the strings
		/// var hash = await query.AggregateAsync(
		///     new FancyHashAggregator(/* ... */), // initializes the inner aggregator
		///     (fha, x) => fha.Append(x), // append the current result to the hash
		///     (fha) => fha.ComputeHash(), // compute the final hash value
		/// );
		/// </code></example>
		public static async Task<TResult> AggregateAsync<TSource, TAccumulator, TResult>(this IAsyncQuery<TSource> source, TAccumulator accumulator, [InstantHandle] Action<TAccumulator, TSource> aggregator, [InstantHandle] Func<TAccumulator, TResult> resultSelector)
		{
			Contract.NotNull(source);
			Contract.NotNull(aggregator);
			Contract.NotNull(resultSelector);

			await ForEachAsync(
				source,
				(Fn: aggregator, Acc: accumulator),
				static (s, x) => s.Fn(s.Acc, x)
			).ConfigureAwait(false);

			return resultSelector(accumulator);
		}

		#endregion

	}

}
