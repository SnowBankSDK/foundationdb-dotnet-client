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
	using SnowBank.Linq.Async.Expressions;
	using SnowBank.Linq.Async.Iterators;

	public static partial class AsyncQuery
	{

		/// <summary>Projects each element of an async sequence to an <see cref="IEnumerable{T}"/> and flattens the resulting sequences into one async sequence.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> SelectMany<TSource, TResult>(this IAsyncQuery<TSource> source, Func<TSource, IEnumerable<TResult>> selector)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			return source switch
			{
				IAsyncLinqQuery<TSource> iterator => iterator.SelectMany(selector),
				_ => AsyncIterators.SelectManyImpl(source, selector)
			};
		}

		/// <summary>Projects each element of an async sequence to an <see cref="IAsyncEnumerable{T}"/> and flattens the resulting sequences into one async sequence.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> SelectMany<TSource, TResult>(this IAsyncQuery<TSource> source, Func<TSource, IAsyncEnumerable<TResult>> selector)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			return source switch
			{
				IAsyncLinqQuery<TSource> iterator => iterator.SelectMany(selector),
				_ => Flatten(source, new AsyncTransformExpression<TSource, IAsyncEnumerable<TResult>>(selector))
			};
		}

		/// <summary>Projects each element of an async sequence to an <see cref="IAsyncQuery{T}"/> and flattens the resulting sequences into one async sequence.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> SelectMany<TSource, TResult>(this IAsyncQuery<TSource> source, Func<TSource, IAsyncQuery<TResult>> selector)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			return source switch
			{
				IAsyncLinqQuery<TSource> iterator => iterator.SelectMany(selector),
				_ => Flatten(source, new AsyncTransformExpression<TSource, IAsyncQuery<TResult>>(selector))
			};
		}

		/// <summary>Projects each element of an async sequence to an <see cref="IEnumerable{T}"/> and flattens the resulting sequences into one async sequence.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> SelectMany<TSource, TResult>(this IAsyncQuery<TSource> source, Func<TSource, CancellationToken, Task<IEnumerable<TResult>>> selector)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			return source switch
			{
				IAsyncLinqQuery<TSource> iterator => iterator.SelectMany(selector),
				_ => AsyncIterators.SelectManyImpl(source, selector)
			};
		}

		/// <summary>Projects each element of an async sequence to an <see cref="IEnumerable{T}"/> flattens the resulting sequences into one async sequence, and invokes a result selector function on each element therein.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> SelectMany<TSource, TCollection, TResult>(this IAsyncQuery<TSource> source, Func<TSource, IEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector)
		{
			Contract.NotNull(source);
			Contract.NotNull(collectionSelector);
			Contract.NotNull(resultSelector);

			return source switch
			{
				IAsyncLinqQuery<TSource> iterator => iterator.SelectMany(collectionSelector, resultSelector),
				_ => AsyncIterators.SelectManyImpl(source, collectionSelector, resultSelector)
			};
		}

		/// <summary>Projects each element of an async sequence to an <see cref="IEnumerable{T}"/> flattens the resulting sequences into one async sequence, and invokes a result selector function on each element therein.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> SelectMany<TSource, TCollection, TResult>(this IAsyncQuery<TSource> source, Func<TSource, CancellationToken, Task<IEnumerable<TCollection>>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector)
		{
			Contract.NotNull(source);
			Contract.NotNull(collectionSelector);
			Contract.NotNull(resultSelector);

			return source switch
			{
				IAsyncLinqQuery<TSource> iterator => iterator.SelectMany(collectionSelector, resultSelector),
				_ => AsyncIterators.SelectManyImpl(source, collectionSelector, resultSelector)
			};
		}

	}

	public static partial class AsyncIterators
	{

		/// <summary>Projects each element of an async sequence to an <see cref="IEnumerable{T}"/> and flattens the resulting sequences into one async sequence.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> SelectManyImpl<TSource, TResult>(IAsyncQuery<TSource> source, Func<TSource, IEnumerable<TResult>> selector)
		{
			return new SelectManyAsyncIterator<TSource, TResult>(source, selector);
		}

		/// <summary>Projects each element of an async sequence to an <see cref="IAsyncEnumerable{T}"/> and flattens the resulting sequences into one async sequence.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> SelectManyImpl<TSource, TResult>(IAsyncQuery<TSource> source, Func<TSource, CancellationToken, Task<IEnumerable<TResult>>> selector)
		{
			return AsyncQuery.Flatten(source, new AsyncTransformExpression<TSource, IEnumerable<TResult>>(selector));
		}

		/// <summary>Projects each element of an async sequence to an <see cref="IEnumerable{T}"/> flattens the resulting sequences into one async sequence, and invokes a result selector function on each element therein.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> SelectManyImpl<TSource, TCollection, TResult>(IAsyncQuery<TSource> source, Func<TSource, IEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector)
		{
			return AsyncQuery.Flatten(source, new(collectionSelector), resultSelector);
		}

		/// <summary>Projects each element of an async sequence to an <see cref="IEnumerable{T}"/> flattens the resulting sequences into one async sequence, and invokes a result selector function on each element therein.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> SelectManyImpl<TSource, TCollection, TResult>(IAsyncQuery<TSource> source, Func<TSource, CancellationToken, Task<IEnumerable<TCollection>>> collectionSelector, Func<TSource, TCollection, TResult> resultSelector)
		{
			return AsyncQuery.Flatten(source, new(collectionSelector), resultSelector);
		}

	}

}
