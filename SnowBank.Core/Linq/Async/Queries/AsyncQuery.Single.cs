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

	public static partial class AsyncQuery
	{

		/// <summary>Returns the first and only element of an async sequence, or an exception if it is empty or have two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> SingleAsync<T>(this IAsyncQuery<T> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.SingleAsync();
			}

			return AsyncIterators.SingleAsync(source);
		}

		/// <summary>Returns the first and only element of an async sequence, or an exception if it is empty or have two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> SingleAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, bool> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.SingleAsync(predicate);
			}

			return AsyncIterators.SingleAsync(source, predicate);
		}

		/// <summary>Returns the first and only element of an async sequence, or an exception if it is empty or have two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> SingleAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.SingleAsync(predicate);
			}

			return AsyncIterators.SingleAsync(source, predicate);
		}

	}

	public static partial class AsyncIterators
	{

		/// <summary>Returns the first and only element of an async sequence, or an exception if it is empty or have two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static async Task<T> SingleAsync<T>(IAsyncQuery<T> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.Head);

			if (!await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				throw AsyncQuery.ErrorNoElements();
			}

			var item = iterator.Current;

			if (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				throw AsyncQuery.ErrorMoreThenOneElement();
			}

			return item;
		}

		/// <summary>Returns the first and only element of an async sequence, or an exception if it is empty or have two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static async Task<T> SingleAsync<T>(IAsyncQuery<T> source, Func<T, bool> predicate)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.Iterator);

			T? single = default;
			bool found = false;

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (predicate(item))
				{
					if (found)
					{
						throw AsyncQuery.ErrorMoreThanOneMatch();
					}
					single = item;
					found = true;
				}
			}

			if (!found)
			{
				throw AsyncQuery.ErrorNoMatch();
			}

			return single!;
		}

		/// <summary>Returns the first and only element of an async sequence, or an exception if it is empty or have two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static async Task<T> SingleAsync<T>(IAsyncQuery<T> source, Func<T, CancellationToken, Task<bool>> predicate)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.Iterator);

			T? single = default;
			bool found = false;
			var ct = source.Cancellation;

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (await predicate(item, ct).ConfigureAwait(false))
				{
					if (found)
					{
						throw AsyncQuery.ErrorMoreThanOneMatch();
					}
					single = item;
					found = true;
				}
			}

			if (!found)
			{
				throw AsyncQuery.ErrorNoMatch();
			}

			return single!;
		}

	}

}
