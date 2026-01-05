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

		/// <summary>Filters an async sequence of values based on a predicate.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Where<TResult>(this IAsyncQuery<TResult> source, Func<TResult, bool> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			return source switch
			{
				IAsyncLinqQuery<TResult> iterator => iterator.Where(predicate),
				_ => AsyncIterators.Where(source, predicate)
			};
		}

		/// <summary>Filters an async sequence of values based on a predicate.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Where<TResult>(this IAsyncQuery<TResult> source, Func<TResult, int, bool> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			return source switch
			{
				IAsyncLinqQuery<TResult> iterator => iterator.Where(predicate),
				_ => AsyncIterators.Where(source, predicate)
			};
		}

		/// <summary>Filters an async sequence of values based on a predicate.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Where<TResult>(this IAsyncQuery<TResult> source, Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			return source switch
			{
				IAsyncLinqQuery<TResult> iterator => iterator.Where(predicate),
				_ => AsyncIterators.Where(source, predicate)
			};
		}

		/// <summary>Filters an async sequence of values based on a predicate.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Where<TResult>(this IAsyncQuery<TResult> source, Func<TResult, int, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			return source switch
			{
				IAsyncLinqQuery<TResult> iterator => iterator.Where(predicate),
				_ => AsyncIterators.Where(source, predicate)
			};
		}

	}

	public static partial class AsyncIterators
	{

		/// <summary>Filters an async sequence of values based on a predicate.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Where<TResult>(IAsyncQuery<TResult> source, Func<TResult, bool> predicate)
		{
			Contract.Debug.Requires(source != null && predicate != null);

			return new WhereAsyncIterator<TResult>(source, predicate);
		}

		/// <summary>Filters an async sequence of values based on a predicate.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Where<TResult>(IAsyncQuery<TResult> source, Func<TResult, int, bool> predicate)
		{
			Contract.Debug.Requires(source != null && predicate != null);

			//note: we have to create a new scope everytime GetAsyncEnumerator() is called,
			// otherwise multiple enumerations of the same query would not reset the index to 0!

			return AsyncQuery.Defer(() =>
			{
				int index = -1;
				return source.Where((item) => predicate(item, checked(++index)));
			}, source.Cancellation);
		}

		/// <summary>Filters an async sequence of values based on a predicate.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Where<TResult>(IAsyncQuery<TResult> source, Func<TResult, CancellationToken, Task<bool>> predicate)
		{
			Contract.Debug.Requires(source != null && predicate != null);

			return new WhereExpressionAsyncIterator<TResult>(source, new(predicate));
		}

		/// <summary>Filters an async sequence of values based on a predicate.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Where<TResult>(IAsyncQuery<TResult> source, Func<TResult, int, CancellationToken, Task<bool>> predicate)
		{
			Contract.Debug.Requires(source != null && predicate != null);

			//note: we have to create a new scope everytime GetAsyncEnumerator() is called,
			// otherwise multiple enumerations of the same query would not reset the index to 0!

			return AsyncQuery.Defer(() =>
			{
				int index = -1;
				return source.Where((item, ct) => predicate(item, checked(++index), ct));
			}, source.Cancellation);
		}

	}

}
