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
	using System.Collections.Immutable;
	using System.ComponentModel;

	/// <summary>Provides asynchronous iteration over the results of a query from a remote source.</summary>
	/// <typeparam name="T">Type of the results returned by this query</typeparam>
	/// <remarks>
	/// <para>This type is very similar to <see cref="IAsyncEnumerable{T}"/>, but with a few key differences in regard to the flow of some arguments</para>
	/// <para>The <see cref="CancellationToken"/> comes from the <b>source</b> of the query (usually a transaction or some other transient scope, like an HTTP request), instead of being passed by the last step in the chain (the call to <see cref="GetAsyncEnumerator(AsyncIterationHint)"/>.</para>
	/// <para>The iterator code can pass a "hint", up the chain of operators, to the source of the query, to specify if the intent is to fetch either all the elements (using <see cref="AsyncQuery.ToListAsync{T}"/>, <see cref="AsyncQuery.ToArrayAsync{T}"/>, ...), only the first page of results (<see cref="AsyncQuery.Take{TSource}(SnowBank.Linq.IAsyncQuery{TSource},int)"/>, <see cref="AsyncQuery.Skip{TSource}"/>, ...), or a single element (<see cref="AsyncQuery.FirstOrDefaultAsync{T}(SnowBank.Linq.IAsyncQuery{T})"/>, ...).</para>
	/// <para>See also <see cref="IAsyncLinqQuery{T}"/> that adds LINQ operator support (Select, Where, OrderBy, ...) on the results of the query.</para>
	/// </remarks>
	/// <seealso cref="IAsyncLinqQuery{T}"/>
	public interface IAsyncQuery<out T>
	{

		/// <summary>Cancellation token that should be used by all async operations performed in the context of this query</summary>
		/// <remarks>This token is controlled by the source (usually a transaction or other transient scope)</remarks>
		CancellationToken Cancellation { get; }

		/// <summary>Gets an asynchronous enumerator over the sequence.</summary>
		/// <param name="hint">Defines how the enumerator will be used by the caller. The source provider can use the mode to optimize how the results are produced.</param>
		/// <returns>Enumerator for asynchronous enumeration over the sequence.</returns>
		[Pure, MustDisposeResource]
		IAsyncEnumerator<T> GetAsyncEnumerator(AsyncIterationHint hint);

		/// <summary>Get an asynchronous enumerator over the sequence, using the default iteration mode.</summary>
		/// <param name="ct">This argument should be either <see cref="CancellationToken.None"/>, or the <b>same</b> token as <see cref="Cancellation"/></param>
		/// <returns>Enumerator for asynchronous enumeration over the sequence.</returns>
		/// <remarks>
		/// <para>The source query will be called with the <see cref="AsyncIterationHint.All"/> which should be efficient for use with <c>await foreach</c> pattern, but is less optimized for cases where only the first few results will actually be consumed.</para>
		/// <para>Consider calling <see cref="AsyncQuery.ToAsyncEnumerable{T}"/> if you need to specify a different iteration hint</para>
		/// </remarks>
		/// <seealso cref="AsyncQuery.ToAsyncEnumerable{T}" />
		[Pure, MustDisposeResource]
		[EditorBrowsable(EditorBrowsableState.Never)]
		IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default);

	}

	/// <summary>Provides optimized LINQ operations other the results of an asynchronous query</summary>
	/// <typeparam name="T">Type of the results returned by this query</typeparam>
	public interface IAsyncLinqQuery<T> : IAsyncQuery<T>
	{

		/// <summary>Returns an <see cref="IAsyncEnumerable{T}"/> view of this query</summary>
		/// <param name="hint">Hint passed to the source provider.</param>
		/// <returns>Sequence that will asynchronously return the results of this query.</returns>
		/// <remarks>
		/// <para>For best performance, the caller should take care to provide a <paramref name="hint"/> that matches how this query will be consumed downstream.</para>
		/// <para>If the hint does not match, performance may be degraded.
		/// For example, if the caller will consumer this query using <c>await foreach</c> or <c>ToListAsync</c>, but uses <see cref="AsyncIterationHint.Iterator"/>, the provider may fetch small pages initially, before ramping up.
		/// The opposite is also true if the caller uses <see cref="AsyncIterationHint.All"/> but consumes the query using <c>AnyAsync()</c> or <c>FirstOrDefaultAsync</c>, the provider may fetch large pages and waste most of it except the first few elements.
		/// </para>
		/// </remarks>
		IAsyncEnumerable<T> ToAsyncEnumerable(AsyncIterationHint hint = AsyncIterationHint.Default);

		#region To{Collection}Async...

		/// <summary>Returns a list of all the elements of the range results</summary>
		Task<List<T>> ToListAsync();

		/// <summary>Returns an array with all the elements of the range results</summary>
		Task<T[]> ToArrayAsync();

		Task<ImmutableArray<T>> ToImmutableArrayAsync();

		Task<Dictionary<TKey, T>> ToDictionaryAsync<TKey>([InstantHandle] Func<T, TKey> keySelector, IEqualityComparer<TKey>? comparer = null) where TKey : notnull;

		/// <summary>Returns a dictionary with the decoded keys and values of the range results</summary>
		Task<Dictionary<TKey, TElement>> ToDictionaryAsync<TKey, TElement>([InstantHandle] Func<T, TKey> keySelector, [InstantHandle] Func<T, TElement> elementSelector, IEqualityComparer<TKey>? comparer = null) where TKey : notnull;

		Task<HashSet<T>> ToHashSetAsync(IEqualityComparer<T>? comparer = null);

		#endregion

		#region CountAsync...

		/// <summary>Returns the number of elements returned by this query.</summary>
		Task<int> CountAsync();

		/// <summary>Returns the number of elements returned by this query that match the given predicate.</summary>
		Task<int> CountAsync(Func<T, bool> predicate);

		/// <summary>Returns the number of elements returned by this query that match the given predicate.</summary>
		Task<int> CountAsync(Func<T, CancellationToken, Task<bool>> predicate);

		#endregion

		#region AnyAsync...

		/// <summary>Returns <see langword="true"/> if the range query yields at least one element, or <see langword="false"/> if there was no result.</summary>
		Task<bool> AnyAsync();

		Task<bool> AnyAsync(Func<T, bool> predicate);

		Task<bool> AnyAsync(Func<T, CancellationToken, Task<bool>> predicate);

		#endregion

		#region AllAsync...
		
		Task<bool> AllAsync(Func<T, bool> predicate);

		Task<bool> AllAsync(Func<T, CancellationToken, Task<bool>> predicate);

		#endregion

		#region FirstOrDefaultAsync...

		/// <summary>Returns the first result of the query, or the default for this type if the query yields no results.</summary>
		Task<T?> FirstOrDefaultAsync() => FirstOrDefaultAsync(default(T)!)!;

		/// <summary>Returns the first result of the query, or the default for this type if the query yields no results.</summary>
		Task<T> FirstOrDefaultAsync(T defaultValue);

		Task<T?> FirstOrDefaultAsync(Func<T, bool> predicate) => FirstOrDefaultAsync(predicate, default(T)!)!;

		Task<T> FirstOrDefaultAsync(Func<T, bool> predicate, T defaultValue);

		Task<T?> FirstOrDefaultAsync(Func<T, CancellationToken, Task<bool>> predicate) => FirstOrDefaultAsync(predicate, default(T)!)!;

		Task<T> FirstOrDefaultAsync(Func<T, CancellationToken, Task<bool>> predicate, T defaultValue);
		
		#endregion

		#region FirstAsync...

		/// <summary>Returns the first result of the query, or an exception if the query yields no result.</summary>
		/// <exception cref="InvalidOperationException">If the query yields no result</exception>
		Task<T> FirstAsync();

		Task<T> FirstAsync(Func<T, bool> predicate);

		Task<T> FirstAsync(Func<T, CancellationToken, Task<bool>> predicate);

		#endregion

		#region SingleOrDefaultAsync...

		/// <summary>Returns the last result of the query, or the default for this type if the query yields no results.</summary>
		Task<T?> SingleOrDefaultAsync() => SingleOrDefaultAsync(default(T)!)!;

		Task<T> SingleOrDefaultAsync(T defaultValue);

		Task<T?> SingleOrDefaultAsync(Func<T, bool> predicate) => SingleOrDefaultAsync(predicate, default(T)!)!;

		Task<T> SingleOrDefaultAsync(Func<T, bool> predicate, T defaultValue);

		Task<T?> SingleOrDefaultAsync(Func<T, CancellationToken, Task<bool>> predicate) => SingleOrDefaultAsync(predicate, default(T)!)!;

		Task<T> SingleOrDefaultAsync(Func<T, CancellationToken, Task<bool>> predicate, T defaultValue);

		#endregion

		#region SingleAsync...

		/// <summary>Returns the only result of the query, or an exception if it yields either zero, or more than one result.</summary>
		/// <exception cref="InvalidOperationException">If the query yields two or more results</exception>
		Task<T> SingleAsync();

		Task<T> SingleAsync(Func<T, bool> predicate);

		Task<T> SingleAsync(Func<T, CancellationToken, Task<bool>> predicate);

		#endregion

		#region LastOrDefaultAsync...

		/// <summary>Returns the last result of the query, or the default for this type if the query yields no results.</summary>
		Task<T?> LastOrDefaultAsync() => LastOrDefaultAsync(default(T)!)!;

		Task<T> LastOrDefaultAsync(T defaultValue);

		Task<T?> LastOrDefaultAsync(Func<T, bool> predicate) => LastOrDefaultAsync(predicate, default(T)!)!;

		Task<T> LastOrDefaultAsync(Func<T, bool> predicate, T defaultValue);

		Task<T?> LastOrDefaultAsync(Func<T, CancellationToken, Task<bool>> predicate) => LastOrDefaultAsync(predicate, default(T)!)!;

		Task<T> LastOrDefaultAsync(Func<T, CancellationToken, Task<bool>> predicate, T defaultValue);

		#endregion

		#region LastAsync...

		/// <summary>Returns the last result of the query, or an exception if the query yields no result.</summary>
		/// <exception cref="InvalidOperationException">If the query yields no result</exception>
		Task<T> LastAsync();

		Task<T> LastAsync(Func<T, bool> predicate);

		Task<T> LastAsync(Func<T, CancellationToken, Task<bool>> predicate);

		#endregion

		#region MinAsync/MaxAsync...

		Task<T?> MinAsync(IComparer<T>? comparer = null);

		Task<T?> MaxAsync(IComparer<T>? comparer = null);

		#endregion

		#region SumAsync...

		Task<T> SumAsync();
		//note: only if T implements INumberBase<T>

		#endregion

		#region Where...

		/// <summary>Filters the range results based on a predicate.</summary>
		/// <remarks>Caution: filtering occurs on the client side !</remarks>
		/// <example><c>query.Where((kv) => kv.Key.StartsWith(prefix))</c> or <c>query.Where((kv) => !kv.Value.IsNull)</c></example>
		[MustUseReturnValue, LinqTunnel]
		IAsyncLinqQuery<T> Where(Func<T, bool> predicate);

		IAsyncLinqQuery<T> Where(Func<T, int, bool> predicate);

		IAsyncLinqQuery<T> Where(Func<T, CancellationToken, Task<bool>> predicate);

		IAsyncLinqQuery<T> Where(Func<T, int, CancellationToken, Task<bool>> predicate);

		#endregion

		#region Select...

		/// <summary>Projects each element of the range results into a new form.</summary>
		/// <param name="selector">Function that is invoked for each source element, and will return the corresponding transformed element.</param>
		/// <returns>New range query that outputs the sequence of transformed elements</returns>
		/// <example><c>query.Select((kv) => $"{kv.Key:K} = {kv.Value:V}")</c></example>
		[MustUseReturnValue, LinqTunnel]
		IAsyncLinqQuery<TNew> Select<TNew>(Func<T, TNew> selector);

		/// <summary>Projects each element of the range results into a new form.</summary>
		/// <param name="selector">Function that is invoked for each source element, and will return the corresponding transformed element.</param>
		/// <returns>New range query that outputs the sequence of transformed elements</returns>
		/// <example><c>query.Select((kv) => $"{kv.Key:K} = {kv.Value:V}")</c></example>
		[MustUseReturnValue, LinqTunnel]
		IAsyncLinqQuery<TNew> Select<TNew>(Func<T, int, TNew> selector);

		IAsyncLinqQuery<TNew> Select<TNew>(Func<T, CancellationToken, Task<TNew>> selector);

		IAsyncLinqQuery<TNew> Select<TNew>(Func<T, int, CancellationToken, Task<TNew>> selector);

		#endregion

		#region SelectMany...
		
		IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<T, IEnumerable<TNew>> selector);

		IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<T, CancellationToken, Task<IEnumerable<TNew>>> selector);

		IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<T, IAsyncEnumerable<TNew>> selector);

		IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<T, IAsyncQuery<TNew>> selector);

		IAsyncLinqQuery<TNew> SelectMany<TCollection, TNew>(Func<T, IEnumerable<TCollection>> collectionSelector, Func<T, TCollection, TNew> resultSelector);

		IAsyncLinqQuery<TNew> SelectMany<TCollection, TNew>(Func<T, CancellationToken, Task<IEnumerable<TCollection>>> collectionSelector, Func<T, TCollection, TNew> resultSelector);

		#endregion

		IAsyncLinqQuery<T> Skip(int count);

		IAsyncLinqQuery<T> Take(int count);

		IAsyncLinqQuery<T> Take(Range range);

		IAsyncLinqQuery<T> TakeWhile(Func<T, bool> condition);

	}

}
