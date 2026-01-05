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
	/// <summary>Base interface for queries that will read ranges of results from the database</summary>
	public interface IFdbRangeQuery
	{
		/// <summary>Parent transaction used to perform the GetRange operation</summary>
		IFdbReadOnlyTransaction Transaction { get; }

		/// <summary>Key selector describing the beginning of the range that will be queried</summary>
		KeySelector Begin { get; }

		/// <summary>Key selector describing the end of the range that will be queried</summary>
		KeySelector End { get; }

		/// <summary>Key selector pair describing the beginning and end of the range that will be queried</summary>
		KeySelectorPair Range => new(this.Begin, this.End);

		/// <summary>Should we perform the range using snapshot mode ?</summary>
		bool IsSnapshot { get; }

		/// <summary>Options used by this query</summary>
		FdbRangeOptions Options { get; }

		/// <summary>Limit in number of bytes to return</summary>
		int? Limit { get; }

		/// <summary>Limit in number of bytes to return</summary>
		int? TargetBytes { get; }

		/// <summary>Returns the <see cref="FdbStreamingMode"/> used by this query</summary>
		FdbStreamingMode Streaming { get; }

		/// <summary>Returns the <see cref="FdbFetchMode"/> used by this query</summary>
		FdbFetchMode Fetch { get; }

		/// <summary>Should the results be returned in reverse order (from last key to first key)</summary>
		bool IsReversed { get; }

	}

	/// <summary>Query that will asynchronously stream decoded results from one or more GetRange operations</summary>
	/// <typeparam name="TResult">Type of the results decoded from the key/value pairs</typeparam>
	public interface IFdbRangeQuery<TResult> : IFdbRangeQuery, IAsyncLinqQuery<TResult>
	{

		/// <summary>Returns pages of results, as they arrive</summary>
		/// <remarks>Processing a batch of results at a time can be more efficient than iterating on each individual key/value</remarks>
		[MustUseReturnValue, LinqTunnel]
		IFdbPagedQuery<TResult> Paged();

		/// <summary>Reverse the order in which the results will be returned</summary>
		/// <returns>A new query object that will return the results in reverse order when executed</returns>
		/// <remarks>
		/// <para>Calling <see cref="Reverse"/> on an already reversed query will cancel the effect, and the results will be returned in their natural order.</para>
		/// <para>Note: Combining the effects of <see cref="Take(int)"/>/<see cref="Skip"/> and <see cref="Reverse"/> may have an impact on performance, especially if the <see cref="FdbTransactionOption.ReadYourWritesDisable"/> option set.</para>
		/// </remarks>
		[MustUseReturnValue, LinqTunnel]
		IFdbRangeQuery<TResult> Reverse();

		/// <summary>Returns only up to a specific number of results</summary>
		/// <param name="count">Maximum number of results to return</param>
		/// <returns>A new query object that will only return up to <paramref name="count"/> results when executed</returns>
		[MustUseReturnValue, LinqTunnel]
		new IFdbRangeQuery<TResult> Take([Positive] int count);

		/// <summary>Returns only results in a specific range</summary>
		/// <param name="range">Range of the results to return</param>
		/// <returns>A new query object that will only a range of results when executed</returns>
		new IFdbRangeQuery<TResult> Take(Range range);

		/// <summary>Skips a specific number of results</summary>
		/// <param name="count">Number of results to skip</param>
		/// <returns>A new query object that will ignore the <paramref name="count"/> results when executed</returns>
		[MustUseReturnValue, LinqTunnel]
		new IFdbRangeQuery<TResult> Skip([Positive] int count);

		/// <summary>Executes an action on each key/value pairs in the range results</summary>
		Task ForEachAsync(Action<TResult> action);

		/// <summary>Executes an action on each key/value pairs in the range results</summary>
		Task<TAggregate> ForEachAsync<TAggregate>(TAggregate aggregate, Action<TAggregate, TResult> action);

		#region Pseudo-LINQ...

		// we override some of these to return the same interface

		/// <summary>Projects each element of the range results into a new form.</summary>
		/// <param name="selector">Function that is invoked for each source element, and will return the corresponding transformed element.</param>
		/// <returns>New range query that outputs the sequence of transformed elements</returns>
		/// <example><c>query.Select((kv) => $"{kv.Key:K} = {kv.Value:V}")</c></example>
		[MustUseReturnValue, LinqTunnel]
		new IFdbRangeQuery<TNew> Select<TNew>(Func<TResult, TNew> selector);

		/// <summary>Projects each element of the range results into a new form.</summary>
		/// <param name="selector">Function that is invoked for each source element, and will return the corresponding transformed element.</param>
		/// <returns>New range query that outputs the sequence of transformed elements</returns>
		/// <example><c>query.Select((kv) => $"{kv.Key:K} = {kv.Value:V}")</c></example>
		[MustUseReturnValue, LinqTunnel]
		new IFdbRangeQuery<TNew> Select<TNew>(Func<TResult, int, TNew> selector);

		new IFdbRangeQuery<TNew> Select<TNew>(Func<TResult, CancellationToken, Task<TNew>> selector);

		new IFdbRangeQuery<TNew> Select<TNew>(Func<TResult, int, CancellationToken, Task<TNew>> selector);

		new IFdbRangeQuery<TNew> SelectMany<TNew>(Func<TResult, IEnumerable<TNew>> selector);

		new IFdbRangeQuery<TNew> SelectMany<TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TNew>>> selector);

		new IFdbRangeQuery<TNew> SelectMany<TNew>(Func<TResult, IAsyncEnumerable<TNew>> selector);

		new IFdbRangeQuery<TNew> SelectMany<TNew>(Func<TResult, IAsyncQuery<TNew>> selector);

		new IFdbRangeQuery<TNew> SelectMany<TCollection, TNew>(Func<TResult, IEnumerable<TCollection>> collectionSelector, Func<TResult, TCollection, TNew> resultSelector);

		new IFdbRangeQuery<TNew> SelectMany<TCollection, TNew>(Func<TResult, CancellationToken, Task<IEnumerable<TCollection>>> collectionSelector, Func<TResult, TCollection, TNew> resultSelector);

		new IFdbRangeQuery<TResult> TakeWhile(Func<TResult, bool> condition);

		#endregion

	}

	/// <summary>Query that will asynchronously stream the key/value pairs returned by one or more GetRange operations</summary>
	public interface IFdbKeyValueRangeQuery : IFdbRangeQuery<KeyValuePair<Slice, Slice>>
	{

		/// <summary>Decode the key/value pairs into instances of type <typeparamref name="TResult"/></summary>
		/// <typeparam name="TState">Type of the state that is passed to the decoder</typeparam>
		/// <typeparam name="TResult">Type of the returned results</typeparam>
		/// <param name="state">State that is passed to <paramref name="decoder"/></param>
		/// <param name="decoder">Handler that is called for each key/value pair to decode</param>
		/// <returns>Range query that returns a sequence of decoded items</returns>
		/// <remarks>This method is optimized to reduce redundant memory copies, and will decode the key/value directly from the native memory</remarks>
		IFdbRangeQuery<TResult> Decode<TState, TResult>(TState state, FdbKeyValueDecoder<TState, TResult> decoder);

		/// <summary>Decode the key/value pairs into instances of type <typeparamref name="TResult"/></summary>
		/// <typeparam name="TResult">Type of the returned results</typeparam>
		/// <param name="decoder">Handler that is called for each key/value pair to decode</param>
		/// <returns></returns>
		/// <remarks>This method is optimized to reduce redundant memory copies, and will decode the key/value directly from the native memory</remarks>
		IFdbRangeQuery<TResult> Decode<TResult>(FdbKeyValueDecoder<TResult> decoder);

	}

	public delegate void PageAction<TResult>(ReadOnlySpan<TResult> page);

	public delegate void PageFunc<TResult, TOther>(ReadOnlySpan<TResult> page, Span<TOther> output);

	public delegate void PageAggregator<in TAggregate, TResult>(TAggregate aggregate, ReadOnlySpan<TResult> page);

	/// <summary>Query that will asynchronously stream batches of decoded results from onr or more GetRange operations</summary>
	/// <typeparam name="TResult">Type of the results decoded from the key/value pairs</typeparam>
	/// <remarks>
	/// <para>The results are returned batches of results, as soon as they are received by the client. The size of the batch may vary, and can be controlled by the <see cref="FdbRangeOptions.Streaming"/> option.</para>
	/// </remarks>
	public interface IFdbPagedQuery<TResult> : IFdbRangeQuery, IAsyncQuery<ReadOnlyMemory<TResult>>
	{

		/// <summary>Reverses the order in which the results will be returned</summary>
		/// <returns>A new query object that will return the results in reverse order when executed</returns>
		/// <remarks>
		/// <para>Calling <see cref="Reverse"/> on an already reversed query will cancel the effect, and the results will be returned in their natural order.</para>
		/// </remarks>
		[MustUseReturnValue, LinqTunnel]
		IFdbPagedQuery<TResult> Reverse();

		/// <summary>Projects each element of the range results into a new form.</summary>
		/// <param name="lambda">Function that is invoked for each source element, and will return the corresponding transformed element.</param>
		/// <returns>New range query that outputs the sequence of transformed elements</returns>
		/// <example><c>query.Select((kv) => $"{kv.Key:K} = {kv.Value:V}")</c></example>
		[MustUseReturnValue, LinqTunnel]
		IFdbPagedQuery<TOther> Select<TOther>(PageFunc<TResult, TOther> lambda);

		/// <summary>Executes an action on each pages of key/value pairs in the range results</summary>
		Task ForEachAsync(PageAction<TResult> handler);

		/// <summary>Executes an action on each pages of key/value pairs in the range results</summary>
		Task<TAggregate> ForEachAsync<TAggregate>(TAggregate aggregate, PageAggregator<TAggregate, TResult> action);

		/// <summary>Returns a flattened list of all the elements of the range results</summary>
		Task<List<TResult>> ToListAsync();

		/// <summary>Returns a flattened array with all the elements of the range results</summary>
		Task<TResult[]> ToArrayAsync();

	}

}
