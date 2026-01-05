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

		/// <summary>Returns the number of elements in an async sequence.</summary>
		public static Task<int> CountAsync<T>(this IAsyncQuery<T> source)
		{
			Contract.NotNull(source);

			return source switch
			{
				IAsyncLinqQuery<T> query => query.CountAsync(),
				_ => AsyncIterators.CountAsync<T>(source)
			};
		}

		/// <summary>Returns a number that represents how many elements in the specified async sequence satisfy a condition.</summary>
		public static Task<int> CountAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, bool> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			return source switch
			{
				IAsyncLinqQuery<T> query => query.CountAsync(predicate),
				_ => AsyncIterators.CountAsync<T>(source, predicate)
			};
		}

		/// <summary>Returns a number that represents how many elements in the specified async sequence satisfy a condition.</summary>
		public static Task<int> CountAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			return source switch
			{
				IAsyncLinqQuery<T> query => query.CountAsync(predicate),
				_ => AsyncIterators.CountAsync<T>(source, predicate)
			};
		}

	}

	public static partial class AsyncIterators
	{

		/// <summary>Returns the number of elements in an async sequence.</summary>
		public static async Task<int> CountAsync<T>(IAsyncQuery<T> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			int count = 0;
			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				checked
				{
					count++;
				}
			}

			return count;
		}

		/// <summary>Returns a number that represents how many elements in the specified async sequence satisfy a condition.</summary>
		public static async Task<int> CountAsync<T>(IAsyncQuery<T> source, Func<T, bool> predicate)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			int count = 0;
			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				if (predicate(iterator.Current))
				{
					checked { count++; }
				}
			}
			return count;
		}

		/// <summary>Returns a number that represents how many elements in the specified async sequence satisfy a condition.</summary>
		public static async Task<int> CountAsync<T>(IAsyncQuery<T> source, Func<T, CancellationToken, Task<bool>> predicate)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			int count = 0;
			var ct = source.Cancellation;
			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				if (await predicate(iterator.Current, ct).ConfigureAwait(false))
				{
					checked { count++; }
				}
			}
			return count;
		}

	}

}
