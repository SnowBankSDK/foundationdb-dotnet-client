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
	using System.Numerics;
	using System.Reflection;
	using SnowBank.Runtime;

	public static partial class AsyncQuery
	{

#if NET7_0_OR_GREATER
		// generic math (INumberBase, .NET 7+); the per-primitive overloads below remain available on all targets
		/// <summary>Returns the sum of all elements in the specified async sequence that satisfy a condition.</summary>
		public static Task<T> SumAsync<T>(this IAsyncQuery<T> source)
			where T : INumberBase<T>
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T> query)
			{
				return query.SumAsync();
			}

			if (typeof(T) == typeof(int)) return (Task<T>) (object) AsyncIterators.SumInt32Async((IAsyncQuery<int>) source);
			if (typeof(T) == typeof(long)) return (Task<T>) (object) AsyncIterators.SumInt64Async((IAsyncQuery<long>) source);
			if (typeof(T) == typeof(float)) return (Task<T>) (object) AsyncIterators.SumFloatAsync((IAsyncQuery<float>) source);
			if (typeof(T) == typeof(double)) return (Task<T>) (object) AsyncIterators.SumDoubleAsync((IAsyncQuery<double>) source);
			if (typeof(T) == typeof(decimal)) return (Task<T>) (object) AsyncIterators.SumDecimalAsync((IAsyncQuery<decimal>) source);

			return AsyncIterators.SumAsync(source);
		}

		/// <summary>Returns the sum of all elements in the specified async sequence that satisfy a condition.</summary>
		public static Task<T?> SumAsync<T>(this IAsyncQuery<T?> source)
			where T : struct, INumberBase<T>
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<T?> query)
			{
				return query.SumAsync();
			}

			if (typeof(T) == typeof(int)) return (Task<T?>) (object) AsyncIterators.SumInt32Async((IAsyncQuery<int?>) source);
			if (typeof(T) == typeof(long)) return (Task<T?>) (object) AsyncIterators.SumInt64Async((IAsyncQuery<long?>) source);
			if (typeof(T) == typeof(float)) return (Task<T?>) (object) AsyncIterators.SumFloatAsync((IAsyncQuery<float?>) source);
			if (typeof(T) == typeof(double)) return (Task<T?>) (object) AsyncIterators.SumDoubleAsync((IAsyncQuery<double?>) source);
			if (typeof(T) == typeof(decimal)) return (Task<T?>) (object) AsyncIterators.SumDecimalAsync((IAsyncQuery<decimal?>) source);

			return AsyncIterators.SumNullableAsync(source);
		}
#endif

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<int> SumAsync(this IAsyncQuery<int> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<int> query)
			{
				return query.SumAsync();
			}

			return AsyncIterators.SumInt32Async(source);
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<int?> SumAsync(this IAsyncQuery<int?> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<int?> query)
			{
				return query.SumAsync();
			}

			return AsyncIterators.SumInt32Async(source);
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<long> SumAsync(this IAsyncQuery<long> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<long> query)
			{
				return query.SumAsync();
			}

			return AsyncIterators.SumInt64Async(source);
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<long?> SumAsync(this IAsyncQuery<long?> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<long?> query)
			{
				return query.SumAsync();
			}

			return AsyncIterators.SumInt64Async(source);
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<float> SumAsync(this IAsyncQuery<float> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<float> query)
			{
				return query.SumAsync();
			}

			return AsyncIterators.SumFloatAsync(source);
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<float?> SumAsync(this IAsyncQuery<float?> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<float?> query)
			{
				return query.SumAsync();
			}

			return AsyncIterators.SumFloatAsync(source);
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<double> SumAsync(this IAsyncQuery<double> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<double> query)
			{
				return query.SumAsync();
			}

			return AsyncIterators.SumDoubleAsync(source);
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<double?> SumAsync(this IAsyncQuery<double?> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<double?> query)
			{
				return query.SumAsync();
			}

			return AsyncIterators.SumDoubleAsync(source);
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<decimal> SumAsync(this IAsyncQuery<decimal> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<decimal> query)
			{
				return query.SumAsync();
			}

			return AsyncIterators.SumDecimalAsync(source);
		}

		/// <summary>Returns the sum of all elements in the specified async sequence.</summary>
		public static Task<decimal?> SumAsync(this IAsyncQuery<decimal?> source)
		{
			Contract.NotNull(source);

			if (source is IAsyncLinqQuery<decimal?> query)
			{
				return query.SumAsync();
			}

			return AsyncIterators.SumDecimalAsync(source);
		}

	}

	public static partial class AsyncIterators
	{

#if NET7_0_OR_GREATER
		// dispatches to the generic-math SumAsync<T>/SumNullableAsync<T> (.NET 7+)
		private static class SumTrampolines<T>
		{

			private static MethodInfo? s_sumAsyncImplMethod;

			public static MethodInfo GetSumMethod() => s_sumAsyncImplMethod ??= CreateTrampoline();

			[MethodImpl(MethodImplOptions.NoInlining)]
			[UnconditionalSuppressMessage("Trimming", "IL2091", Justification = "generic-math dispatch; the concrete numeric types use a compile-time fast path, only user INumberBase<T> types reach reflection")]
			[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "generic-math dispatch; the concrete numeric types use a compile-time fast path, only user INumberBase<T> types reach reflection")]
			private static MethodInfo CreateTrampoline()
			{
				var nullable = Nullable.GetUnderlyingType(typeof(T));
				MethodInfo? m = nullable != null
					? typeof(AsyncIterators).GetMethod(nameof(SumNullableAsync), BindingFlags.Static | BindingFlags.Public)?.MakeGenericMethod(nullable)
					: typeof(AsyncIterators).GetMethod(nameof(SumAsync), BindingFlags.Static | BindingFlags.Public)?.MakeGenericMethod(typeof(T));

				return m ?? throw new NotSupportedException("Can only sum elements of type that implement INumberBase<T>.");
			}

		}
#endif

		/// <summary>Version of <see cref="SumAsync{T}"/> that does not have a generic constraint on <typeparamref name="T"/>, and that will perform a runtime dispatch to a compatible method</summary>
		/// <typeparam name="T">Type of results, that should implement see <see cref="INumberBase{T}"/></typeparam>
		/// <param name="source">Query to sum</param>
		/// <returns>Sum of the results in the query, if <typeparamref name="T"/> is supported</returns>
		/// <exception cref="NotSupportedException">If <typeparamref name="T"/> does not implement <see cref="INumberBase{T}"/></exception>
		/// <remarks>This method can be used by generic iterators that cannot place a constraint on the types of their element, but still be able to provide summing if the type supports it.</remarks>
		[UnconditionalSuppressMessage("Trimming", "IL2087", Justification = "generic-math dispatch; the concrete numeric types use a compile-time fast path, only user INumberBase<T> types reach reflection")]
		public static Task<T> SumUnconstrainedAsync<T>(IAsyncLinqQuery<T> source)
		{
			if (default(T) is not null)
			{
				if (typeof(T) == typeof(int)) return (Task<T>) (object) SumInt32Async((IAsyncQuery<int>) source);
				if (typeof(T) == typeof(long)) return (Task<T>) (object) SumInt64Async((IAsyncQuery<long>) source);
				if (typeof(T) == typeof(float)) return (Task<T>) (object) SumFloatAsync((IAsyncQuery<float>) source);
				if (typeof(T) == typeof(double)) return (Task<T>) (object) SumDoubleAsync((IAsyncQuery<double>) source);
				if (typeof(T) == typeof(decimal)) return (Task<T>) (object) SumDecimalAsync((IAsyncQuery<decimal>) source);
			}
			else
			{
				if (typeof(T) == typeof(int?)) return (Task<T>) (object) SumInt32Async((IAsyncQuery<int?>) source);
				if (typeof(T) == typeof(long?)) return (Task<T>) (object) SumInt64Async((IAsyncQuery<long?>) source);
				if (typeof(T) == typeof(float?)) return (Task<T>) (object) SumFloatAsync((IAsyncQuery<float?>) source);
				if (typeof(T) == typeof(double?)) return (Task<T>) (object) SumDoubleAsync((IAsyncQuery<double?>) source);
				if (typeof(T) == typeof(decimal?)) return (Task<T>) (object) SumDecimalAsync((IAsyncQuery<decimal?>) source);
			}

#if NET7_0_OR_GREATER
			// generic math dispatch; on older targets only the concrete numeric types tested above are supported
			var nullable = Nullable.GetUnderlyingType(typeof(T));
			if (nullable != null)
			{
				if (nullable.IsGenericInstanceOf(typeof(INumberBase<>)))
				{
					return (Task<T>) SumTrampolines<T>.GetSumMethod().Invoke(null, [ source ])!;
				}
			}
			else
			{
				if (typeof(T).IsGenericInstanceOf(typeof(INumberBase<>)))
				{
					return (Task<T>) SumTrampolines<T>.GetSumMethod().Invoke(null, [ source ])!;
				}
			}

			throw new NotSupportedException();
#else
			throw new NotSupportedException();
#endif
		}
#if NET7_0_OR_GREATER
		// generic math (INumberBase, .NET 7+); the per-primitive overloads below remain available on all targets

		/// <summary>Sums the results of a query</summary>
		/// <typeparam name="T">Type of the elements</typeparam>
		/// <param name="source">Query to sum</param>
		/// <returns>Sum of all the results, or zero if the query is empty.</returns>
		public static async Task<T> SumAsync<T>(IAsyncQuery<T> source) where T : INumberBase<T>
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			if (!await iterator.MoveNextAsync().ConfigureAwait(false))
			{ // empty sequence
				return T.Zero;
			}

			T sum = iterator.Current;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				sum = checked(sum + iterator.Current);
			}

			return sum;
		}

		/// <summary>Sums the nullable results of a query</summary>
		/// <typeparam name="T">Underlying type of the nullable elements</typeparam>
		/// <param name="source">Query to sum</param>
		/// <returns>Sum of all the results, or zero if the query is empty.</returns>
		public static async Task<T?> SumNullableAsync<T>(IAsyncQuery<T?> source) where T : struct, INumberBase<T>
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			T sum = T.Zero;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (item is not null)
				{
					sum = checked(sum + item.GetValueOrDefault());
				}
			}

			return sum;
		}
#endif

		/// <summary>Sums the results of a query</summary>
		/// <param name="source">Query to sum</param>
		/// <returns>Sum of all the results, or zero if the query is empty.</returns>
		public static async Task<int> SumInt32Async(IAsyncQuery<int> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			int sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				checked { sum += iterator.Current; }
			}

			return sum;
		}

		/// <summary>Sums the results of a query that are not null.</summary>
		/// <param name="source">Query to sum</param>
		/// <returns>Sum of all the results, or zero if the query is empty.</returns>
		public static async Task<int?> SumInt32Async(IAsyncQuery<int?> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			int sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (item is not null)
				{
					sum = checked(sum + item.GetValueOrDefault());
				}
			}

			return sum;
		}

		/// <summary>Sums the results of a query</summary>
		/// <param name="source">Query to sum</param>
		/// <returns>Sum of all the results, or zero if the query is empty.</returns>
		public static async Task<long> SumInt64Async(IAsyncQuery<long> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			long sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				checked { sum += iterator.Current; }
			}

			return sum;
		}

		/// <summary>Sums the results of a query that are not null.</summary>
		/// <param name="source">Query to sum</param>
		/// <returns>Sum of all the results, or zero if the query is empty.</returns>
		public static async Task<long?> SumInt64Async(IAsyncQuery<long?> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			long sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (item is not null)
				{
					sum = checked(sum + item.GetValueOrDefault());
				}
			}

			return sum;
		}

		/// <summary>Sums the results of a query</summary>
		/// <param name="source">Query to sum</param>
		/// <returns>Sum of all the results, or zero if the query is empty.</returns>
		public static async Task<float> SumFloatAsync(IAsyncQuery<float> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			//note: like Enumerable and AsyncEnumerable, we will also use a double as the accumulator (to reduce precision loss)

			double sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				sum += iterator.Current;
			}

			return (float) sum;
		}

		/// <summary>Sums the results of a query that are not null.</summary>
		/// <param name="source">Query to sum</param>
		/// <returns>Sum of all the results, or zero if the query is empty.</returns>
		public static async Task<float?> SumFloatAsync(IAsyncQuery<float?> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			//note: like Enumerable and AsyncEnumerable, we will also use a double as the accumulator (to reduce precision loss)
			double sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (item is not null)
				{
					sum += item.GetValueOrDefault();
				}
			}

			return (float) sum;
		}

		/// <summary>Sums the results of a query</summary>
		/// <param name="source">Query to sum</param>
		/// <returns>Sum of all the results, or zero if the query is empty.</returns>
		public static async Task<double> SumDoubleAsync(IAsyncQuery<double> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			double sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				sum += iterator.Current;
			}

			return sum;
		}

		/// <summary>Sums the results of a query that are not null.</summary>
		/// <param name="source">Query to sum</param>
		/// <returns>Sum of all the results, or zero if the query is empty.</returns>
		public static async Task<double?> SumDoubleAsync(IAsyncQuery<double?> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			double sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (item is not null)
				{
					sum += item.GetValueOrDefault();
				}
			}

			return sum;
		}

		/// <summary>Sums the results of a query</summary>
		/// <param name="source">Query to sum</param>
		/// <returns>Sum of all the results, or zero if the query is empty.</returns>
		public static async Task<decimal> SumDecimalAsync(IAsyncQuery<decimal> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			decimal sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				sum += iterator.Current;
			}

			return sum;
		}

		/// <summary>Sums the results of a query that are not null.</summary>
		/// <param name="source">Query to sum</param>
		/// <returns>Sum of all the results, or zero if the query is empty.</returns>
		public static async Task<decimal?> SumDecimalAsync(IAsyncQuery<decimal?> source)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.All);

			decimal sum = 0;
			while (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				var item = iterator.Current;
				if (item is not null)
				{
					sum += item.GetValueOrDefault();
				}
			}

			return sum;
		}

	}

}
