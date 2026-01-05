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

namespace FoundationDB.Linq.Expressions
{

	/// <summary>Helper class to construct Query Expressions</summary>
	[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
	public static class FdbQueryExpressions
	{

		/// <summary>Return a single result from the query</summary>
		public static FdbQuerySingleExpression<TSource, TResult> Single<TSource, TResult>(FdbQuerySequenceExpression<TSource> source, string? name, Expression<Func<IAsyncQuery<TSource>, Task<TResult>>> lambda)
		{
			Contract.NotNull(source);
			Contract.NotNull(lambda);

			name ??= lambda.Name ?? "Lambda";

			return new FdbQuerySingleExpression<TSource, TResult>(source, name, lambda);
		}

		/// <summary>Return a sequence of results from the query</summary>
		public static FdbQueryAsyncEnumerableExpression<T> Sequence<T>(IAsyncQuery<T> source)
		{
			Contract.NotNull(source);

			return new FdbQueryAsyncEnumerableExpression<T>(source);
		}

		/// <summary>Execute a Range read from the database, and return all the keys and values</summary>
		public static FdbQueryRangeExpression Range(KeySelectorPair range, FdbRangeOptions? options = null)
		{
			return new FdbQueryRangeExpression(range, options);
		}

		/// <summary>Execute a Range read from the database, and return all the keys and values</summary>
		public static FdbQueryRangeExpression Range(KeySelector start, KeySelector stop, FdbRangeOptions? options = null)
		{
			return Range(new KeySelectorPair(start, stop), options);
		}

		/// <summary>Execute a Range read from the database, and return all the keys and values</summary>
		public static FdbQueryRangeExpression RangeStartsWith(Slice prefix, FdbRangeOptions? options = null)
		{
			// starts_with('A') means ['A', B')
			return Range(KeySelectorPair.StartsWith(prefix), options);
		}

		/// <summary>Execute a Range read from the database, and return all the keys and values</summary>
		[Obsolete]
		public static FdbQueryRangeExpression RangeStartsWith(IVarTuple tuple, FdbRangeOptions? options = null)
		{
			return RangeStartsWith(TuPack.Pack(tuple), options);
		}

		/// <summary>Return the intersection between one of more sequences of results</summary>
		public static FdbQueryIntersectExpression<T> Intersect<T>(params FdbQuerySequenceExpression<T>[] expressions)
		{
			Contract.NotNull(expressions);
			if (expressions.Length <= 1) throw new ArgumentException("There must be at least two sequences to perform an intersection");

			//TODO: check that all the other have the same type
			//var type = expressions[0].Type;

			return new FdbQueryIntersectExpression<T>(expressions, null);
		}

		/// <summary>Return the union between one of more sequences of results</summary>
		public static FdbQueryUnionExpression<T> Union<T>(params FdbQuerySequenceExpression<T>[] expressions)
		{
			Contract.NotNull(expressions);
			if (expressions.Length <= 1) throw new ArgumentException("There must be at least two sequences to perform an intersection");

			//TODO: check that all the other have the same type
			//var type = expressions[0].Type;

			return new FdbQueryUnionExpression<T>(expressions, null);
		}

		/// <summary>Transform each element of a sequence into a new sequence</summary>
		public static FdbQueryTransformExpression<TSource, TResult> Transform<TSource, TResult>(FdbQuerySequenceExpression<TSource> source, Expression<Func<TSource, TResult>> transform)
		{
			Contract.NotNull(source);
			Contract.NotNull(transform);
			if (source.ElementType != typeof(TSource)) throw new ArgumentException($"Source sequence has type {source.ElementType.Name} that is not compatible with transform input type {typeof(TSource).Name}", nameof(source));

			return new FdbQueryTransformExpression<TSource, TResult>(source, transform);
		}

		/// <summary>Filter out the elements of e sequence that do not match a predicate</summary>
		public static FdbQueryFilterExpression<T> Filter<T>(FdbQuerySequenceExpression<T> source, Expression<Func<T, bool>> filter)
		{
			Contract.NotNull(source);
			Contract.NotNull(filter);
			if (source.ElementType != typeof(T)) throw new ArgumentException($"Source sequence has type {source.ElementType.Name} that is not compatible with filter input type {typeof(T).Name}", nameof(source));

			return new FdbQueryFilterExpression<T>(source, filter);
		}

		/// <summary>Returns a human-readable explanation of a query that returns a single element</summary>
		public static string ExplainSingle<T>(FdbQueryExpression<T> expression, CancellationToken ct)
		{
			Contract.NotNull(expression);
			if (expression.Shape != FdbQueryShape.Single) throw new InvalidOperationException("Invalid shape (single expected)");

			var expr = expression.CompileSingle();
			return expr.GetDebugView();
		}


		/// <summary>Returns a human-readable explanation of a query that returns a sequence of elements</summary>
		public static string ExplainSequence<T>(FdbQuerySequenceExpression<T> expression)
		{
			Contract.NotNull(expression);
			if (expression.Shape != FdbQueryShape.Sequence) throw new InvalidOperationException("Invalid shape (sequence expected)");

			var expr = expression.CompileSequence();
			return expr.GetDebugView();
		}

	}

}
