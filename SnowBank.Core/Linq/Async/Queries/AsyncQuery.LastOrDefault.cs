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

		/// <summary>Returns the last element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T?> LastOrDefaultAsync<T>(this IAsyncQuery<T> source) => LastOrDefaultAsync(source, default(T?));

		/// <summary>Returns the last element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T> LastOrDefaultAsync<T>(this IAsyncQuery<T> source, T defaultValue)
		{
			Contract.NotNull(source);

			return source switch
			{
				IAsyncLinqQuery<T> query => query.LastOrDefaultAsync(defaultValue),
				_ => AsyncIterators.LastOrDefaultAsync<T>(source, defaultValue)
			};
		}

		/// <summary>Returns the last element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T?> LastOrDefaultAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, bool> predicate) => LastOrDefaultAsync(source, predicate!, default(T?));

		/// <summary>Returns the last element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T> LastOrDefaultAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, bool> predicate, T defaultValue)
		{
			Contract.NotNull(source);

			return source switch
			{
				IAsyncLinqQuery<T> query => query.LastOrDefaultAsync(predicate, defaultValue),
				_ => AsyncIterators.LastOrDefaultAsync<T>(source, predicate, defaultValue)
			};
		}

		/// <summary>Returns the last element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T?> LastOrDefaultAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, CancellationToken, Task<bool>> predicate) => LastOrDefaultAsync(source, predicate!, default(T?));

		/// <summary>Returns the last element of an async sequence, or the default value for the type if it is empty</summary>
		public static Task<T> LastOrDefaultAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, CancellationToken, Task<bool>> predicate, T defaultValue)
		{
			Contract.NotNull(source);

			return source switch
			{
				IAsyncLinqQuery<T> query => query.LastOrDefaultAsync(predicate, defaultValue),
				_ => AsyncIterators.LastOrDefaultAsync<T>(source, predicate, defaultValue)
			};
		}

	}

	public static partial class AsyncIterators
	{

		/// <summary>Returns the last element of an async sequence, or the default value for the type if it is empty</summary>
		public static async Task<T> LastOrDefaultAsync<T>(IAsyncQuery<T> source, T defaultValue)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			if (!await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				return defaultValue;
			}

			T result;
			do
			{
				result = iterator.Current;
			}
			while (await iterator.MoveNextAsync().ConfigureAwait(false));

			return result;
		}

		/// <summary>Returns the last element of an async sequence, or the default value for the type if it is empty</summary>
		public static async Task<T> LastOrDefaultAsync<T>(IAsyncQuery<T> source, Func<T, bool> predicate, T defaultValue)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			T result = defaultValue;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (predicate(item))
				{
					result = iterator.Current;
				}
			}
			return result;
		}

		/// <summary>Returns the last element of an async sequence, or the default value for the type if it is empty</summary>
		public static async Task<T> LastOrDefaultAsync<T>(IAsyncQuery<T> source, Func<T, CancellationToken, Task<bool>> predicate, T defaultValue)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			T result = defaultValue;
			var ct = source.Cancellation;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (await predicate(item, ct).ConfigureAwait(false))
				{
					result = iterator.Current;
				}
			}
			return result;
		}

	}

}
