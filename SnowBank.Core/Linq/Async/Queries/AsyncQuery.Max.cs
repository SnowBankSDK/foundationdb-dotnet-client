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

		/// <summary>Returns the largest value in the specified async sequence</summary>
		public static Task<T?> MaxAsync<T>(this IAsyncQuery<T> source, IComparer<T>? comparer = null)
		{
			Contract.NotNull(source);

			return source switch
			{
				IAsyncLinqQuery<T> query => query.MaxAsync(comparer),
				_ => AsyncIterators.MaxAsync(source, comparer)
			};
		}

	}

	public static partial class AsyncIterators
	{

		/// <summary>Returns the largest value in the specified async sequence</summary>
		public static async Task<int> MaxAsync(IAsyncQuery<int> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			// get the first
			if (!await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				throw AsyncQuery.ErrorNoElements();
			}
			var min = iterator.Current;

			// compare with the rest
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				min = Math.Max(iterator.Current, min);
			}

			return min;
		}

		/// <summary>Returns the largest value in the specified async sequence</summary>
		public static async Task<long> MaxAsync(IAsyncQuery<long> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			// get the first
			if (!await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				throw AsyncQuery.ErrorNoElements();
			}
			var min = iterator.Current;

			// compare with the rest
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				min = Math.Max(iterator.Current, min);
			}

			return min;
		}

		/// <summary>Returns the largest value in the specified async sequence</summary>
		public static async Task<float> MaxAsync(IAsyncQuery<float> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			// get the first
			if (!await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				throw AsyncQuery.ErrorNoElements();
			}
			var min = iterator.Current;

			// compare with the rest
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				min = Math.Max(iterator.Current, min);
			}

			return min;
		}

		/// <summary>Returns the largest value in the specified async sequence</summary>
		public static async Task<double> MaxAsync(IAsyncQuery<double> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			// get the first
			if (!await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				throw AsyncQuery.ErrorNoElements();
			}
			var min = iterator.Current;

			// compare with the rest
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				min = Math.Max(iterator.Current, min);
			}

			return min;
		}

		/// <summary>Returns the largest value in the specified async sequence</summary>
		public static async Task<decimal> MaxAsync(IAsyncQuery<decimal> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			// get the first
			if (!await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				throw AsyncQuery.ErrorNoElements();
			}
			var min = iterator.Current;

			// compare with the rest
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				min = Math.Max(iterator.Current, min);
			}

			return min;
		}

		/// <summary>Returns the largest value in the specified async sequence</summary>
		public static Task<T?> MaxAsync<T>(IAsyncQuery<T> source, IComparer<T>? comparer)
		{
			comparer ??= Comparer<T>.Default;

			if (default(T) is not null)
			{
				if (typeof(T) == typeof(int) && ReferenceEquals(comparer, Comparer<int>.Default)) return (Task<T?>) (object) MaxAsync((IAsyncQuery<int>) source);
				if (typeof(T) == typeof(long) && ReferenceEquals(comparer, Comparer<long>.Default)) return (Task<T?>) (object) MaxAsync((IAsyncQuery<long>) source);
				if (typeof(T) == typeof(float) && ReferenceEquals(comparer, Comparer<float>.Default)) return (Task<T?>) (object) MaxAsync((IAsyncQuery<float>) source);
				if (typeof(T) == typeof(double) && ReferenceEquals(comparer, Comparer<double>.Default)) return (Task<T?>) (object) MaxAsync((IAsyncQuery<double>) source);
				if (typeof(T) == typeof(decimal) && ReferenceEquals(comparer, Comparer<decimal>.Default)) return (Task<T?>) (object) MaxAsync((IAsyncQuery<decimal>) source);

				return MaxAsyncStruct(source, comparer);
			}

			return MaxAsyncRef(source, comparer);

			static async Task<T?> MaxAsyncStruct(IAsyncQuery<T> source, IComparer<T> comparer)
			{
				await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

				// we will throw if the query is empty
				if (!await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					throw AsyncQuery.ErrorNoElements();
				}
				var max = iterator.Current;

				// compare with the rest
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					var candidate = iterator.Current;
					if (comparer.Compare(candidate, max) > 0)
					{
						max = candidate;
					}
				}

				return max;
			}

			static async Task<T?> MaxAsyncRef(IAsyncQuery<T> source, IComparer<T> comparer)
			{
				await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

				// we will return null if the query is empty, or only contains null
				var max = default(T);
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					var candidate = iterator.Current;
					if (candidate is not null && (max is null || comparer.Compare(candidate, max) > 0))
					{
						max = candidate;
					}
				}
				return max;
			}
		}

	}

}
