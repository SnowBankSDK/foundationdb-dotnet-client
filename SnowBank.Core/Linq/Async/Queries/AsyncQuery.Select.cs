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

		/// <summary>Projects each element of an async sequence into a new form.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Select<TSource, TResult>(this IAsyncQuery<TSource> source, Func<TSource, TResult> selector)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			return source switch
			{
				IAsyncLinqQuery<TSource> iterator => iterator.Select(selector),
				_ => AsyncIterators.Select(source, selector)
			};
		}

		/// <summary>Projects each element of an async sequence into a new form.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Select<TSource, TResult>(this IAsyncQuery<TSource> source, Func<TSource, int, TResult> selector)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			return source switch
			{
				IAsyncLinqQuery<TSource> iterator => iterator.Select(selector),
				_ => AsyncIterators.Select(source, selector)
			};
		}

		/// <summary>Projects each element of an async sequence into a new form.</summary>
		[Pure, LinqTunnel]
		[OverloadResolutionPriority(1)]
		public static IAsyncLinqQuery<TResult> Select<TSource, TResult>(this IAsyncQuery<TSource> source, Func<TSource, CancellationToken, Task<TResult>> selector)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			return source switch
			{
				IAsyncLinqQuery<TSource> iterator => iterator.Select(selector),
				_ => AsyncIterators.Select(source, selector)
			};
		}

		/// <summary>Projects each element of an async sequence into a new form.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Select<TSource, TResult>(this IAsyncQuery<TSource> source, Func<TSource, int, CancellationToken, Task<TResult>> selector)
		{
			Contract.NotNull(source);
			Contract.NotNull(selector);

			return source switch
			{
				IAsyncLinqQuery<TSource> iterator => iterator.Select(selector),
				_ => AsyncIterators.Select(source, selector)
			};
		}

	}

	public static partial class AsyncIterators
	{

		/// <summary>Projects each element of an async sequence into a new form.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Select<TSource, TResult>(IAsyncQuery<TSource> source, Func<TSource, TResult> selector)
		{
			Contract.Debug.Requires(source != null && selector != null);

			return new SelectAsyncIterator<TSource,TResult>(source, selector);
		}

		/// <summary>Projects each element of an async sequence into a new form.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Select<TResult, TSource>(IAsyncQuery<TSource> source, Func<TSource, int, TResult> selector)
		{
			Contract.Debug.Requires(source != null && selector != null);

			//note: we have to create a new scope everytime GetAsyncEnumerator() is called,
			// otherwise multiple enumerations of the same query would not reset the index to 0!
			return AsyncQuery.Defer(() =>
			{
				int index = -1;
				return source.Select((item) => selector(item, checked(++index)));

			}, source.Cancellation);
		}

		/// <summary>Projects each element of an async sequence into a new form.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Select<TSource, TResult>(IAsyncQuery<TSource> source, Func<TSource, CancellationToken, Task<TResult>> selector)
		{
			Contract.Debug.Requires(source != null && selector != null);

			return new SelectTaskAsyncIterator<TSource,TResult>(source, selector);
		}

		/// <summary>Projects each element of an async sequence into a new form.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Select<TResult, TSource>(IAsyncQuery<TSource> source, Func<TSource, int, CancellationToken, Task<TResult>> selector)
		{
			Contract.Debug.Requires(source != null && selector != null);

			//note: we have to create a new scope everytime GetAsyncEnumerator() is called,
			// otherwise multiple enumerations of the same query would not reset the index to 0!
			return AsyncQuery.Defer(() =>
			{
				int index = -1;
				return source.Select((item, ct) => selector(item, checked(++index), ct));

			}, source.Cancellation);
		}

	}

}
