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

namespace FoundationDB.Linq
{
	using FoundationDB.Layers.Indexing;
	using FoundationDB.Linq.Providers;

	/// <summary>Extensions methods that help create a query expression tree</summary>
	[PublicAPI]
	[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
	public static class FdbAsyncQueryable
	{

		internal static Task<T> ExecuteSingle<T>(this IFdbAsyncQueryable<T> query, CancellationToken ct = default)
		{
			return query.Provider.ExecuteAsync<T>(query.Expression!, ct);
		}

		internal static Task<T> ExecuteSequence<T>(this IFdbAsyncSequenceQueryable<T> query, CancellationToken ct = default)
		{
			return query.Provider.ExecuteAsync<T>(query.Expression!, ct);
		}

		#region Database Queries...

		/// <summary>Start a query on a transaction</summary>
		/// <param name="tr">Source transaction</param>
		/// <returns>Query that will use this transaction as a source</returns>
		public static IFdbTransactionQueryable Query(this IFdbReadOnlyTransaction tr)
		{
			Contract.NotNull(tr);

			return new FdbTransactionQuery(tr);
		}

		/// <summary>Return a range of keys</summary>
		/// <param name="query">Source database query</param>
		/// <param name="range">Pair of key selectors</param>
		/// <returns>Query that will return the keys from the specified <paramref name="range"/></returns>
		public static IFdbAsyncSequenceQueryable<KeyValuePair<Slice, Slice>> Range(this IFdbTransactionQueryable query, KeySelectorPair range)
		{
			Contract.NotNull(query);

			var expr = FdbQueryExpressions.Range(range);

			return query.Provider.CreateSequenceQuery(expr);
		}

		/// <summary>Return all the keys that share a common prefix</summary>
		/// <param name="query">Source database query</param>
		/// <param name="prefix">Shared prefix</param>
		/// <returns>Query that will return the keys that share the specified <paramref name="prefix"/></returns>
		public static IFdbAsyncSequenceQueryable<KeyValuePair<Slice, Slice>> RangeStartsWith(this IFdbTransactionQueryable query, Slice prefix)
		{
			Contract.NotNull(query);

			var expr = FdbQueryExpressions.RangeStartsWith(prefix);

			return query.Provider.CreateSequenceQuery(expr);
		}

		#endregion

		#region Index Queries...

		/// <summary>Creates a new query on this index</summary>
		public static IFdbIndexQueryable<TId, TValue> Query<TId, TValue>(this FdbIndex<TId, TValue> index, IFdbDatabase db)
		{
			Contract.NotNull(index);
			Contract.NotNull(db);

			return new FdbIndexQuery<TId, TValue>(db, index);
		}

		/// <summary>Creates a new query that will look up specific values on this index</summary>
		public static IFdbAsyncSequenceQueryable<TId> Lookup<TId, TValue>(this IFdbIndexQueryable<TId, TValue> query, Expression<Func<TValue , bool>> predicate)
		{
			var expr = FdbQueryIndexLookupExpression<TId, TValue>.Lookup(query.Index, predicate);

			return query.Provider.CreateSequenceQuery(expr);
		}

		#endregion

		/// <summary>Projects each element of a sequence query into a new form.</summary>
		public static IFdbAsyncSequenceQueryable<TResult> Select<TSource, TResult>(this IFdbAsyncSequenceQueryable<TSource> query, Expression<Func<TSource, TResult>> selector)
		{
			Contract.NotNull(query);
			Contract.NotNull(selector);
			if (query.Expression is not FdbQuerySequenceExpression<TSource> sourceExpr)
			{
				throw new ArgumentException(null, nameof(query));
			}

			var expr = FdbQueryExpressions.Transform(sourceExpr, selector);

			return query.Provider.CreateSequenceQuery<TResult>(expr);
		}

		/// <summary>Filters a sequence query of values based on a predicate.</summary>
		public static IFdbAsyncSequenceQueryable<T> Where<T>(this IFdbAsyncSequenceQueryable<T> query, Expression<Func<T, bool>> predicate)
		{
			Contract.NotNull(query);
			Contract.NotNull(predicate);
			if (query.Expression is not FdbQuerySequenceExpression<T> sourceExpr)
			{
				throw new ArgumentException(null, nameof(query));
			}

			var expr = FdbQueryExpressions.Filter(sourceExpr, predicate);

			return query.Provider.CreateSequenceQuery<T>(expr);
		}

		/// <summary>Returns an async sequence that would return the results of this query as they arrive.</summary>
		public static IAsyncQuery<T> ToAsyncEnumerable<T>(this IFdbAsyncSequenceQueryable<T> query)
		{
			Contract.NotNull(query);

			if (query is not FdbAsyncSequenceQuery<T> sequenceQuery)
			{
				throw new ArgumentException("Source query type not supported", nameof(query));
			}

			return sequenceQuery.ToEnumerable();
		}

		/// <summary>Returns the first element of a sequence query</summary>
		public static Task<int> CountAsync<T>(this IFdbAsyncSequenceQueryable<T> query, CancellationToken ct = default)
		{
			Contract.NotNull(query);

			var expr = FdbQueryExpressions.Single<T, int>(
				(FdbQuerySequenceExpression<T>) query.Expression!,
				"CountAsync",
				(source) => source.CountAsync()
			);

			return query.Provider.CreateQuery<int>(expr).ExecuteSingle(ct);
		}

		/// <summary>Returns the first element of a sequence query</summary>
		public static Task<T> FirstAsync<T>(this IFdbAsyncSequenceQueryable<T> query, CancellationToken ct = default)
		{
			Contract.NotNull(query);
			if (ct.IsCancellationRequested) return Task.FromCanceled<T>(ct);

			var expr = FdbQueryExpressions.Single<T, T>(
				(FdbQuerySequenceExpression<T>) query.Expression!,
				"FirstAsync",
				(source) => source.FirstAsync()
			);

			return query.Provider.CreateQuery<T>(expr).ExecuteSingle(ct);
		}

		/// <summary>Returns the first element of a sequence query</summary>
		public static Task<T?> FirstOrDefaultAsync<T>(this IFdbAsyncSequenceQueryable<T> query, CancellationToken ct = default)
		{
			Contract.NotNull(query);
			if (ct.IsCancellationRequested) return Task.FromCanceled<T?>(ct);

			var expr = FdbQueryExpressions.Single<T, T?>(
				(FdbQuerySequenceExpression<T>) query.Expression!,
				"FirstOrDefaultAsync",
				(source) => source.FirstOrDefaultAsync()
			);

			return query.Provider.CreateQuery<T?>(expr).ExecuteSingle(ct);
		}

		/// <summary>Immediately executes a sequence query and return a list of all the results once it has completed.</summary>
		public static Task<List<T>> ToListAsync<T>(this IFdbAsyncSequenceQueryable<T> query, CancellationToken ct = default)
		{
			Contract.NotNull(query);
			if (ct.IsCancellationRequested) return Task.FromCanceled<List<T>>(ct);

			return query.Provider.ExecuteAsync<List<T>>(query.Expression!, ct);

		}

		/// <summary>Immediately executes a sequence query and return an array of all the results once it has completed.</summary>
		public static Task<T[]> ToArrayAsync<T>(this IFdbAsyncSequenceQueryable<T> query, CancellationToken ct = default)
		{
			Contract.NotNull(query);
			if (ct.IsCancellationRequested) return Task.FromCanceled<T[]>(ct);

			return query.Provider.ExecuteAsync<T[]>(query.Expression!, ct);
		}

	}

}
