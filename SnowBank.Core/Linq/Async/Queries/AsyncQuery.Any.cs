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

		/// <summary>Determines whether an async sequence contains any elements.</summary>
		/// <remarks>This is the logical equivalent to "source.Count() > 0" but can be better optimized by some providers</remarks>
		public static Task<bool> AnyAsync<T>(this IAsyncQuery<T> source)
		{
			Contract.NotNull(source);

			return source switch
			{
				IAsyncLinqQuery<T> query => query.AnyAsync(),
				_ => AsyncIterators.AnyAsync<T>(source)
			};
		}

		/// <summary>Determines whether any element of an async sequence satisfies a condition.</summary>
		public static Task<bool> AnyAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, bool> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			return source switch
			{
				IAsyncLinqQuery<T> query => query.AnyAsync(predicate),
				_ => AsyncIterators.AnyAsync<T>(source, predicate)
			};
		}

		/// <summary>Determines whether any element of an async sequence satisfies a condition.</summary>
		public static Task<bool> AnyAsync<T>(this IAsyncQuery<T> source, [InstantHandle] Func<T, CancellationToken, Task<bool>> predicate)
		{
			Contract.NotNull(source);
			Contract.NotNull(predicate);

			return source switch
			{
				IAsyncLinqQuery<T> query => query.AnyAsync(predicate),
				_ => AsyncIterators.AnyAsync<T>(source, predicate)
			};
		}

	}

	public static partial class AsyncIterators
	{

		/// <summary>Determines whether an async sequence contains any elements.</summary>
		public static async Task<bool> AnyAsync<T>(IAsyncQuery<T> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.Head);

			return await iterator.MoveNextAsync().ConfigureAwait(false);
		}

		/// <summary>Determines whether any element of an async sequence satisfies a condition.</summary>
		public static async Task<bool> AnyAsync<T>(IAsyncQuery<T> source, Func<T, bool> predicate)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.Iterator);

			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				if (predicate(iterator.Current)) return true;
			}

			return false;
		}

		/// <summary>Determines whether any element of an async sequence satisfies a condition.</summary>
		public static async Task<bool> AnyAsync<T>(IAsyncQuery<T> source, Func<T, CancellationToken, Task<bool>> predicate)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.Iterator);

			var ct = source.Cancellation;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				if (await predicate(iterator.Current, ct).ConfigureAwait(false)) return true;
			}

			return false;
		}

	}

}
