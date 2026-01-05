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

namespace FoundationDB.Client
{
	using SnowBank.Linq.Async.Iterators;

	[PublicAPI]
	public static class FdbMergeQueryExtensions
	{

		#region MergeSort (x OR y)

		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<KeyValuePair<Slice, Slice>> MergeSort<TKey>(this IFdbReadOnlyTransaction trans, IEnumerable<KeySelectorPair> ranges, Func<KeyValuePair<Slice, Slice>, TKey> keySelector, IComparer<TKey>? keyComparer = null)
		{
			//TODO: Range options ?
			Contract.NotNull(trans);
			Contract.NotNull(ranges);
			Contract.NotNull(keySelector);

			trans.EnsureCanRead();

			return new MergeSortAsyncIterator<KeyValuePair<Slice, Slice>, TKey, KeyValuePair<Slice, Slice>>(
				ranges.Select(range => trans.GetRange(range, new FdbRangeOptions { Streaming = FdbStreamingMode.Iterator })).ToArray<IAsyncQuery<KeyValuePair<Slice, Slice>>>(),
				null,
				keySelector,
				(kv) => kv,
				keyComparer,
				trans.Cancellation
			);
		}

		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> MergeSort<TKey, TResult>(this IFdbReadOnlyTransaction trans, IEnumerable<KeySelectorPair> ranges, Func<KeyValuePair<Slice, Slice>, TKey> keySelector, Func<KeyValuePair<Slice, Slice>, TResult> resultSelector, IComparer<TKey>? keyComparer = null)
		{
			//TODO: Range options ?
			Contract.NotNull(trans);
			Contract.NotNull(ranges);
			Contract.NotNull(keySelector);
			Contract.NotNull(resultSelector);

			trans.EnsureCanRead();
			return new MergeSortAsyncIterator<KeyValuePair<Slice, Slice>, TKey, TResult>(
				ranges.Select(range => trans.GetRange(range, new FdbRangeOptions { Streaming = FdbStreamingMode.Iterator })).ToArray<IAsyncQuery<KeyValuePair<Slice, Slice>>>(),
				default,
				keySelector,
				resultSelector,
				keyComparer,
				trans.Cancellation
			);
		}

		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Union<TKey, TResult>(IEnumerable<IAsyncQuery<TResult>> sources, Func<TResult, TKey> keySelector, IComparer<TKey>? keyComparer = null)
		{
			Contract.NotNull(sources);
			Contract.NotNull(keySelector);

			var arr = (sources as IAsyncQuery<TResult>[]) ?? sources.ToArray();
			if (arr.Length == 0) return AsyncQuery.Empty<TResult>();
			var ct = arr[0].Cancellation;

			return new MergeSortAsyncIterator<TResult, TKey, TResult>(
				arr,
				null,
				keySelector,
				(x) => x,
				keyComparer,
				ct
			);
		}

		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Union<TResult>(IEnumerable<IAsyncQuery<TResult>> sources, IComparer<TResult>? keyComparer = null)
		{
			Contract.NotNull(sources);

			var arr = (sources as IAsyncQuery<TResult>[]) ?? sources.ToArray();
			if (arr.Length == 0) return AsyncQuery.Empty<TResult>();
			var ct = arr[0].Cancellation;

			return new MergeSortAsyncIterator<TResult, TResult, TResult>(
				arr,
				null,
				(x) => x,
				(x) => x,
				keyComparer,
				ct
			);
		}

		#endregion

		#region Intersect (x AND y)

		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<KeyValuePair<Slice, Slice>> Intersect<TKey>(this IFdbReadOnlyTransaction trans, IEnumerable<KeySelectorPair> ranges, Func<KeyValuePair<Slice, Slice>, TKey> keySelector, IComparer<TKey>? keyComparer = null)
		{
			//TODO: Range options ?
			Contract.NotNull(trans);
			Contract.NotNull(ranges);
			Contract.NotNull(keySelector);

			trans.EnsureCanRead();
			return new IntersectAsyncIterator<KeyValuePair<Slice, Slice>, TKey, KeyValuePair<Slice, Slice>>(
				ranges.Select(range => trans.GetRange(range, new FdbRangeOptions { Streaming = FdbStreamingMode.Iterator })).ToArray<IAsyncQuery<KeyValuePair<Slice, Slice>>>(),
				null,
				keySelector,
				(kv) => kv,
				keyComparer,
				trans.Cancellation
			);
		}

		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Intersect<TKey, TResult>(this IFdbReadOnlyTransaction trans, IEnumerable<KeySelectorPair> ranges, Func<KeyValuePair<Slice, Slice>, TKey> keySelector, Func<KeyValuePair<Slice, Slice>, TResult> resultSelector, IComparer<TKey>? keyComparer = null)
		{
			//TODO: Range options ?

			trans.EnsureCanRead();
			return new IntersectAsyncIterator<KeyValuePair<Slice, Slice>, TKey, TResult>(
				ranges.Select(range => trans.GetRange(range, new FdbRangeOptions { Streaming = FdbStreamingMode.Iterator })).ToArray(),
				null,
				keySelector,
				resultSelector,
				keyComparer,
				trans.Cancellation
			);
		}

		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Intersect<TKey, TResult>(this IAsyncQuery<TResult> first, IAsyncQuery<TResult> second, Func<TResult, TKey> keySelector, IComparer<TKey>? keyComparer = null)
		{
			Contract.NotNull(first);
			Contract.NotNull(second);

			//TODO: check that both first & second have the cancellation token, otherwise combine them?

			return new IntersectAsyncIterator<TResult, TKey, TResult>(
				[ first, second ],
				null,
				keySelector,
				(x) => x,
				keyComparer,
				first.Cancellation
			);
		}

		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Intersect<TResult>(this IAsyncQuery<TResult> first, IAsyncQuery<TResult> second, IComparer<TResult>? comparer = null)
		{
			Contract.NotNull(first);
			Contract.NotNull(second);

			//TODO: check that both first & second have the cancellation token, otherwise combine them?

			return new IntersectAsyncIterator<TResult, TResult, TResult>(
				[first, second],
				null,
				(x) => x,
				(x) => x,
				comparer,
				first.Cancellation
			);
		}

		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Intersect<TKey, TResult>(IEnumerable<IAsyncQuery<TResult>> sources, Func<TResult, TKey> keySelector, IComparer<TKey>? keyComparer = null)
		{
			Contract.NotNull(sources);
			Contract.NotNull(keySelector);

			var arr = (sources as IAsyncQuery<TResult>[]) ?? sources.ToArray();
			if (arr.Length == 0) return AsyncQuery.Empty<TResult>();
			var ct = arr[0].Cancellation;


			return new IntersectAsyncIterator<TResult, TKey, TResult>(
				arr,
				null,
				keySelector,
				(x) => x,
				keyComparer,
				ct
			);
		}

		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Intersect<TResult>(IEnumerable<IAsyncQuery<TResult>> sources, IComparer<TResult>? keyComparer = null)
		{
			Contract.NotNull(sources);

			var arr = (sources as IAsyncQuery<TResult>[]) ?? sources.ToArray();
			if (arr.Length == 0) return AsyncQuery.Empty<TResult>();
			var ct = arr[0].Cancellation;


			return new IntersectAsyncIterator<TResult, TResult, TResult>(
				arr,
				null,
				(x) => x,
				(x) => x,
				keyComparer,
				ct
			);
		}

		#endregion

		#region Except (x AND NOT y)

		/// <summary>Return the keys that are in the first range, but not in the others</summary>
		/// <typeparam name="TKey">Type of the keys returned by the query</typeparam>
		/// <param name="trans">Transaction used by the operation</param>
		/// <param name="ranges">List of at least one key selector pairs</param>
		/// <param name="keySelector">Lambda called to extract the keys from the ranges</param>
		/// <param name="keyComparer">Instance used to compare the keys returned by <paramref name="keySelector"/></param>
		/// <returns>Async query that returns only the results that are in the first range, and not in any other range.</returns>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<KeyValuePair<Slice, Slice>> Except<TKey>(this IFdbReadOnlyTransaction trans, IEnumerable<KeySelectorPair> ranges, Func<KeyValuePair<Slice, Slice>, TKey> keySelector, IComparer<TKey>? keyComparer = null)
		{
			//TODO: Range options ?
			Contract.NotNull(trans);
			Contract.NotNull(ranges);
			Contract.NotNull(keySelector);

			trans.EnsureCanRead();
			return new ExceptAsyncIterator<KeyValuePair<Slice, Slice>, TKey, KeyValuePair<Slice, Slice>>(
				ranges.Select(range => trans.GetRange(range, new FdbRangeOptions { Streaming = FdbStreamingMode.Iterator })),
				null,
				keySelector,
				(kv) => kv,
				keyComparer,
				trans.Cancellation
			);
		}

		/// <summary>Return the keys that are in the first range, but not in the others</summary>
		/// <typeparam name="TKey">Type of the keys returned by the query</typeparam>
		/// <param name="trans">Transaction used by the operation</param>
		/// <param name="ranges">List of at least one key range</param>
		/// <param name="keySelector">Lambda called to extract the keys from the ranges</param>
		/// <param name="keyComparer">Instance used to compare the keys returned by <paramref name="keySelector"/></param>
		/// <returns>Async query that returns only the results that are in the first range, and not in any other range.</returns>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<KeyValuePair<Slice, Slice>> Except<TKey>(this IFdbReadOnlyTransaction trans, IEnumerable<KeyRange> ranges, Func<KeyValuePair<Slice, Slice>, TKey> keySelector, IComparer<TKey>? keyComparer = null)
		{
			Contract.NotNull(ranges);
			return Except<TKey>(trans, ranges.Select(r => KeySelectorPair.Create(r)), keySelector, keyComparer);
		}

		/// <summary>Return the keys that are in the first range, but not in the others</summary>
		/// <typeparam name="TKey">Type of the keys used for the comparison</typeparam>
		/// <typeparam name="TResult">Type of the results returned by the query</typeparam>
		/// <param name="trans">Transaction used by the operation</param>
		/// <param name="ranges">List of at least one key selector pairs</param>
		/// <param name="keySelector">Lambda called to extract the keys used by the sort</param>
		/// <param name="resultSelector">Lambda called to extract the values returned by the query</param>
		/// <param name="keyComparer">Instance used to compare the keys returned by <paramref name="keySelector"/></param>
		/// <returns>Async query that returns only the results that are in the first range, and not in any other range.</returns>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Except<TKey, TResult>(this IFdbReadOnlyTransaction trans, IEnumerable<KeySelectorPair> ranges, Func<KeyValuePair<Slice, Slice>, TKey> keySelector, Func<KeyValuePair<Slice, Slice>, TResult> resultSelector, IComparer<TKey>? keyComparer = null)
		{
			//TODO: Range options ?

			trans.EnsureCanRead();
			return new ExceptAsyncIterator<KeyValuePair<Slice, Slice>, TKey, TResult>(
				ranges.Select(range => trans.GetRange(range, new FdbRangeOptions { Streaming = FdbStreamingMode.Iterator })),
				null,
				keySelector,
				resultSelector,
				keyComparer,
				trans.Cancellation
			);
		}

		/// <summary>Return the keys that are in the first range, but not in the others</summary>
		/// <typeparam name="TKey">Type of the keys used for the comparison</typeparam>
		/// <typeparam name="TResult">Type of the results returned by the query</typeparam>
		/// <param name="trans">Transaction used by the operation</param>
		/// <param name="ranges">List of at least one key ranges</param>
		/// <param name="keySelector">Lambda called to extract the keys used by the sort</param>
		/// <param name="resultSelector">Lambda called to extract the values returned by the query</param>
		/// <param name="keyComparer">Instance used to compare the keys returned by <paramref name="keySelector"/></param>
		/// <returns>Async query that returns only the results that are in the first range, and not in any other range.</returns>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Except<TKey, TResult>(this IFdbReadOnlyTransaction trans, IEnumerable<KeyRange> ranges, Func<KeyValuePair<Slice, Slice>, TKey> keySelector, Func<KeyValuePair<Slice, Slice>, TResult> resultSelector, IComparer<TKey>? keyComparer = null)
		{
			Contract.NotNull(ranges);
			return Except<TKey, TResult>(trans, ranges.Select(r => KeySelectorPair.Create(r)), keySelector, resultSelector, keyComparer);
		}

		/// <summary>Sequence the return only the elements of <paramref name="first"/> that are not in <paramref name="second"/>, using a custom key comparison</summary>
		/// <typeparam name="TKey">Type of the keys that will be used for comparison</typeparam>
		/// <typeparam name="TResult">Type of the results of the query</typeparam>
		/// <param name="first">First query that contains the elements that could be in the result</param>
		/// <param name="second">Second query that contains the elements that cannot be in the result</param>
		/// <param name="keySelector">Lambda used to extract keys from both queries.</param>
		/// <param name="keyComparer">Instance used to compare keys</param>
		/// <returns>Async query that returns only the elements that are in <paramref name="first"/>, and not in <paramref name="second"/></returns>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Except<TKey, TResult>(this IAsyncQuery<TResult> first, IAsyncQuery<TResult> second, Func<TResult, TKey> keySelector, IComparer<TKey>? keyComparer = null)
		{
			Contract.NotNull(first);
			Contract.NotNull(second);
			Contract.NotNull(keySelector);

			//TODO: check that both first & second have the cancellation token, otherwise combine them?

			return new ExceptAsyncIterator<TResult, TKey, TResult>(
				[ first, second ],
				null,
				keySelector,
				(x) => x,
				keyComparer,
				first.Cancellation
			);
		}

		/// <summary>Sequence the return only the elements of <paramref name="first"/> that are not in <paramref name="second"/></summary>
		/// <typeparam name="TResult">Type of the results of the query</typeparam>
		/// <param name="first">First query that contains the elements that could be in the result</param>
		/// <param name="second">Second query that contains the elements that cannot be in the result</param>
		/// <param name="comparer">Instance used to compare elements</param>
		/// <returns>Async query that returns only the elements that are in <paramref name="first"/>, and not in <paramref name="second"/></returns>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Except<TResult>(this IAsyncQuery<TResult> first, IAsyncQuery<TResult> second, IComparer<TResult>? comparer = null)
		{
			Contract.NotNull(first);
			Contract.NotNull(second);

			//TODO: check that both first & second have the cancellation token, otherwise combine them?

			return new ExceptAsyncIterator<TResult, TResult, TResult>(
				[ first, second ],
				null,
				(x) => x,
				(x) => x,
				comparer,
				first.Cancellation
			);
		}

		#endregion

	}

}
