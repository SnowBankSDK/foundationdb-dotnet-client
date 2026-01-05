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

		/// <summary>Returns the first and only element of an async sequence, the default value for the type if it is empty, or an exception if it has two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T?> SingleOrDefaultAsync<T>(this IAsyncQuery<T> source) => SingleOrDefaultAsync(source, default(T?));

		/// <summary>Returns the first and only element of an async sequence, the default value for the type if it is empty, or an exception if it has two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> SingleOrDefaultAsync<T>(this IAsyncQuery<T> source, T defaultValue)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.SingleOrDefaultAsync(defaultValue);
			}

			return AsyncIterators.SingleOrDefaultAsync(source, defaultValue);
		}

		/// <summary>Returns the first and only element of an async sequence, the default value for the type if it is empty, or an exception if it has two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T?> SingleOrDefaultAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, bool> predicate) => SingleOrDefaultAsync(source, predicate!, default(T?));

		/// <summary>Returns the first and only element of an async sequence, the default value for the type if it is empty, or an exception if it has two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> SingleOrDefaultAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, bool> predicate, T defaultValue)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.SingleOrDefaultAsync(predicate, defaultValue);
			}

			return AsyncIterators.SingleOrDefaultAsync(source, predicate, defaultValue);
		}

		/// <summary>Returns the first and only element of an async sequence, the default value for the type if it is empty, or an exception if it has two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T?> SingleOrDefaultAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, CancellationToken, Task<bool>> predicate) => SingleOrDefaultAsync(source, predicate!, default(T?));

		/// <summary>Returns the first and only element of an async sequence, the default value for the type if it is empty, or an exception if it has two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static Task<T> SingleOrDefaultAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, CancellationToken, Task<bool>> predicate, T defaultValue)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.SingleOrDefaultAsync(predicate, defaultValue);
			}

			return AsyncIterators.SingleOrDefaultAsync(source, predicate, defaultValue);
		}

	}

	public static partial class AsyncIterators
	{
		/// <summary>Returns the first and only element of an async sequence, the default value for the type if it is empty, or an exception if it has two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static async Task<T> SingleOrDefaultAsync<T>(IAsyncQuery<T> source, T defaultValue)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.Head);

			if (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					throw AsyncQuery.ErrorMoreThenOneElement();
				}
				return item;
			}
			return defaultValue;
		}

		/// <summary>Returns the first and only element of an async sequence, the default value for the type if it is empty, or an exception if it has two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static async Task<T> SingleOrDefaultAsync<T>(IAsyncQuery<T> source, [InstantHandle] Func<T, bool> predicate, T defaultValue)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.Iterator);

			var result = defaultValue;
			bool found = false;
			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (predicate(item))
				{
					if (found) throw AsyncQuery.ErrorMoreThanOneMatch();
					result = item;
					found = true;
				}
			}
			return result;
		}

		/// <summary>Returns the first and only element of an async sequence, the default value for the type if it is empty, or an exception if it has two or more elements</summary>
		/// <remarks>Will need to call MoveNext at least twice to ensure that there is no second element.</remarks>
		public static async Task<T> SingleOrDefaultAsync<T>(IAsyncQuery<T> source, [InstantHandle] Func<T, CancellationToken, Task<bool>> predicate, T defaultValue)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.Iterator);

			var result = defaultValue;
			bool found = false;
			var ct = source.Cancellation;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (await predicate(item, ct).ConfigureAwait(false))
				{
					if (found) throw AsyncQuery.ErrorMoreThanOneMatch();
					result = item;
					found = true;
				}
			}
			return result;
		}

	}

}
