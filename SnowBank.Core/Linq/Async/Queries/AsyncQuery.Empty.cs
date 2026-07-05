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
	using System.Collections.Immutable;

	public static partial class AsyncQuery
	{

		/// <summary>Returns an empty async sequence</summary>
		[Pure]
		public static IAsyncLinqQuery<T> Empty<T>()
		{
			return EmptyIterator<T>.Default;
		}

		/// <summary>A query that returns nothing</summary>
		internal sealed class EmptyIterator<TSource> : IAsyncLinqQuery<TSource>, IAsyncEnumerable<TSource>, IAsyncEnumerator<TSource>
		{
			public static readonly EmptyIterator<TSource> Default = new();

			private EmptyIterator()
			{ }

			/// <inheritdoc />
			public CancellationToken Cancellation => CancellationToken.None;

			/// <inheritdoc />
			[MustDisposeResource]
			IAsyncEnumerator<TSource> IAsyncEnumerable<TSource>.GetAsyncEnumerator(CancellationToken ct) => this;

			/// <inheritdoc />
			[MustDisposeResource]
			IAsyncEnumerator<TSource> IAsyncQuery<TSource>.GetAsyncEnumerator(CancellationToken ct) => this;

			/// <inheritdoc />
			[MustDisposeResource]
			public IAsyncEnumerator<TSource> GetAsyncEnumerator(AsyncIterationHint mode) => this;

			/// <inheritdoc />
			ValueTask<bool> IAsyncEnumerator<TSource>.MoveNextAsync() => new();

			/// <inheritdoc />
			TSource IAsyncEnumerator<TSource>.Current => default!;

			/// <inheritdoc />
			ValueTask IAsyncDisposable.DisposeAsync() => new();

			/// <inheritdoc />
			public Task<bool> AnyAsync() => Task.FromResult(false);

			/// <inheritdoc />
			public Task<bool> AnyAsync(Func<TSource, bool> predicate) => Task.FromResult(false);

			/// <inheritdoc />
			public Task<bool> AnyAsync(Func<TSource, CancellationToken, Task<bool>> predicate) => Task.FromResult(false);

			/// <inheritdoc />
			public Task<bool> AllAsync(Func<TSource, bool> predicate) => Task.FromResult(true);

			/// <inheritdoc />
			public Task<bool> AllAsync(Func<TSource, CancellationToken, Task<bool>> predicate) => Task.FromResult(true);

			/// <inheritdoc />
			public Task<int> CountAsync() => Task.FromResult(0);

			/// <inheritdoc />
			public Task<int> CountAsync(Func<TSource, bool> predicate) => Task.FromResult(0);

			/// <inheritdoc />
			public Task<int> CountAsync(Func<TSource, CancellationToken, Task<bool>> predicate) => Task.FromResult(0);

			/// <inheritdoc />
			public Task<TSource> SumAsync()
			{
				if (default(TSource) is not null)
				{
					return Task.FromResult(default(TSource)!);
				}

				if (typeof(TSource).IsValueType)
				{
					// this is likely a Nullable<T> where we need to return 0 instead of null!
					// note: RuntimeHelpers.GetUninitializedObject(typeof(int?)) returns 0 instead of null, which helps us here (for a change!)
#if NET8_0_OR_GREATER
					return Task.FromResult((TSource) RuntimeHelpers.GetUninitializedObject(typeof(TSource)));
#else
					return Task.FromResult((TSource) System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(TSource))); // RuntimeHelpers.GetUninitializedObject is not on netstandard2.0
#endif
				}

				// we cannot simply return null here, because if TSource implements INumberBase<T> we should return T.Zero instead (which could be something else!)
				// => fallback to the default impl which can deal with this
				return AsyncIterators.SumUnconstrainedAsync(this);
			}

			/// <inheritdoc />
			public Task<TSource?> MinAsync(IComparer<TSource>? comparer = null)
			{
				if (default(TSource) is not null)
				{
					throw ErrorNoElements();
				}
				return Task.FromResult(default(TSource));
			}

			/// <inheritdoc />
			public Task<TSource?> MaxAsync(IComparer<TSource>? comparer = null)
			{
				if (default(TSource) is not null)
				{
					throw ErrorNoElements();
				}
				return Task.FromResult(default(TSource));
			}

			/// <inheritdoc />
			public IAsyncLinqQuery<TSource> Skip(int count)
			{
				Contract.Positive(count);
				return this;
			}

			/// <inheritdoc />
			public IAsyncLinqQuery<TSource> Take(int count)
			{
				Contract.Positive(count);
				return this;
			}

			/// <inheritdoc />
			public IAsyncLinqQuery<TSource> Take(Range range)
			{
				return this;
			}

			/// <inheritdoc />
			public IAsyncLinqQuery<TSource> TakeWhile(Func<TSource, bool> condition) => this;

			/// <inheritdoc />
			public IAsyncEnumerable<TSource> ToAsyncEnumerable(AsyncIterationHint hint = AsyncIterationHint.Default)
			{
#if NET10_0_OR_GREATER
				return AsyncEnumerable.Empty<TSource>();
#else
				return this;
#endif
			}
			/// <inheritdoc />
			public Task<TSource[]> ToArrayAsync() => Task.FromResult(Array.Empty<TSource>());

			/// <inheritdoc />
			public Task<List<TSource>> ToListAsync() => Task.FromResult(new List<TSource>());

			/// <inheritdoc />
			public Task<HashSet<TSource>> ToHashSetAsync(IEqualityComparer<TSource>? comparer = null) => Task.FromResult(new HashSet<TSource>(comparer));

			/// <inheritdoc />
			public Task<ImmutableArray<TSource>> ToImmutableArrayAsync() => Task.FromResult(ImmutableArray<TSource>.Empty);

			/// <inheritdoc />
			public Task<Dictionary<TKey, TSource>> ToDictionaryAsync<TKey>(Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer = null) where TKey : notnull
				=> Task.FromResult(new Dictionary<TKey, TSource>(comparer));

			/// <inheritdoc />
			public Task<Dictionary<TKey, TElement>> ToDictionaryAsync<TKey, TElement>(Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector, IEqualityComparer<TKey>? comparer = null) where TKey : notnull
				=> Task.FromResult(new Dictionary<TKey, TElement>(comparer));

			/// <inheritdoc />
			public Task<TSource> FirstOrDefaultAsync(TSource defaultValue) => Task.FromResult(defaultValue);

			/// <inheritdoc />
			public Task<TSource> FirstOrDefaultAsync(Func<TSource, bool> predicate, TSource defaultValue) => Task.FromResult(defaultValue);

			/// <inheritdoc />
			public Task<TSource> FirstOrDefaultAsync(Func<TSource, CancellationToken, Task<bool>> predicate, TSource defaultValue) => Task.FromResult(defaultValue);

#if !NET5_0_OR_GREATER
			// these overloads are default interface implementations on modern targets, which netstandard2.0 cannot represent
			/// <inheritdoc />
			public Task<TSource?> FirstOrDefaultAsync() => Task.FromResult(default(TSource));

			/// <inheritdoc />
			public Task<TSource?> FirstOrDefaultAsync(Func<TSource, bool> predicate) => Task.FromResult(default(TSource));

			/// <inheritdoc />
			public Task<TSource?> FirstOrDefaultAsync(Func<TSource, CancellationToken, Task<bool>> predicate) => Task.FromResult(default(TSource));

			/// <inheritdoc />
			public Task<TSource?> LastOrDefaultAsync() => Task.FromResult(default(TSource));

			/// <inheritdoc />
			public Task<TSource?> LastOrDefaultAsync(Func<TSource, bool> predicate) => Task.FromResult(default(TSource));

			/// <inheritdoc />
			public Task<TSource?> LastOrDefaultAsync(Func<TSource, CancellationToken, Task<bool>> predicate) => Task.FromResult(default(TSource));

			/// <inheritdoc />
			public Task<TSource?> SingleOrDefaultAsync() => Task.FromResult(default(TSource));

			/// <inheritdoc />
			public Task<TSource?> SingleOrDefaultAsync(Func<TSource, bool> predicate) => Task.FromResult(default(TSource));

			/// <inheritdoc />
			public Task<TSource?> SingleOrDefaultAsync(Func<TSource, CancellationToken, Task<bool>> predicate) => Task.FromResult(default(TSource));
#endif

			/// <inheritdoc />
			public Task<TSource> FirstAsync() => Task.FromException<TSource>(ErrorNoElements());

			/// <inheritdoc />
			public Task<TSource> FirstAsync(Func<TSource, bool> predicate) => Task.FromException<TSource>(ErrorNoElements());

			/// <inheritdoc />
			public Task<TSource> FirstAsync(Func<TSource, CancellationToken, Task<bool>> predicate) => Task.FromException<TSource>(ErrorNoElements());

			/// <inheritdoc />
			public Task<TSource> LastOrDefaultAsync(TSource defaultValue) => Task.FromResult(defaultValue);

			/// <inheritdoc />
			public Task<TSource> LastOrDefaultAsync(Func<TSource, bool> predicate, TSource defaultValue) => Task.FromResult(defaultValue);

			/// <inheritdoc />
			public Task<TSource> LastOrDefaultAsync(Func<TSource, CancellationToken, Task<bool>> predicate, TSource defaultValue) => Task.FromResult(defaultValue);

			/// <inheritdoc />
			public Task<TSource> LastAsync() => Task.FromException<TSource>(ErrorNoElements());

			/// <inheritdoc />
			public Task<TSource> LastAsync(Func<TSource, bool> predicate) => Task.FromException<TSource>(ErrorNoElements());

			/// <inheritdoc />
			public Task<TSource> LastAsync(Func<TSource, CancellationToken, Task<bool>> predicate) => Task.FromException<TSource>(ErrorNoElements());

			/// <inheritdoc />
			public Task<TSource> SingleAsync() => Task.FromException<TSource>(ErrorNoElements());

			/// <inheritdoc />
			public Task<TSource> SingleAsync(Func<TSource, bool> predicate) => Task.FromException<TSource>(ErrorNoElements());

			/// <inheritdoc />
			public Task<TSource> SingleAsync(Func<TSource, CancellationToken, Task<bool>> predicate) => Task.FromException<TSource>(ErrorNoElements());

			/// <inheritdoc />
			public Task<TSource> SingleOrDefaultAsync(TSource defaultValue) => Task.FromResult(defaultValue);

			/// <inheritdoc />
			public Task<TSource> SingleOrDefaultAsync(Func<TSource, bool> predicate, TSource defaultValue) => Task.FromResult(defaultValue);

			/// <inheritdoc />
			public Task<TSource> SingleOrDefaultAsync(Func<TSource, CancellationToken, Task<bool>> predicate, TSource defaultValue) => Task.FromResult(defaultValue);

			/// <inheritdoc />
			public IAsyncLinqQuery<TSource> Where(Func<TSource, bool> predicate) => this;

			/// <inheritdoc />
			public IAsyncLinqQuery<TSource> Where(Func<TSource, int, bool> predicate) => this;

			/// <inheritdoc />
			public IAsyncLinqQuery<TSource> Where(Func<TSource, CancellationToken, Task<bool>> asyncPredicate) => this;

			/// <inheritdoc />
			public IAsyncLinqQuery<TSource> Where(Func<TSource, int, CancellationToken, Task<bool>> asyncPredicate) => this;

			/// <inheritdoc />
			public IAsyncLinqQuery<TNew> Select<TNew>(Func<TSource, TNew> selector) => EmptyIterator<TNew>.Default;

			/// <inheritdoc />
			public IAsyncLinqQuery<TNew> Select<TNew>(Func<TSource, int, TNew> selector) => EmptyIterator<TNew>.Default;

			/// <inheritdoc />
			public IAsyncLinqQuery<TNew> Select<TNew>(Func<TSource, CancellationToken, Task<TNew>> asyncSelector) => EmptyIterator<TNew>.Default;

			/// <inheritdoc />
			public IAsyncLinqQuery<TNew> Select<TNew>(Func<TSource, int, CancellationToken, Task<TNew>> asyncSelector) => EmptyIterator<TNew>.Default;

			/// <inheritdoc />
			public IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<TSource, IEnumerable<TNew>> selector) => EmptyIterator<TNew>.Default;

			/// <inheritdoc />
			public IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<TSource, CancellationToken, Task<IEnumerable<TNew>>> asyncSelector) => EmptyIterator<TNew>.Default;

			/// <inheritdoc />
			public IAsyncLinqQuery<TNew> SelectMany<TCollection, TNew>(Func<TSource, IEnumerable<TCollection>> collectionSelector, Func<TSource, TCollection, TNew> resultSelector) => EmptyIterator<TNew>.Default;

			/// <inheritdoc />
			public IAsyncLinqQuery<TNew> SelectMany<TCollection, TNew>(Func<TSource, CancellationToken, Task<IEnumerable<TCollection>>> asyncCollectionSelector, Func<TSource, TCollection, TNew> resultSelector) => EmptyIterator<TNew>.Default;

			/// <inheritdoc />
			public IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<TSource, IAsyncEnumerable<TNew>> selector) => EmptyIterator<TNew>.Default;

			/// <inheritdoc />
			public IAsyncLinqQuery<TNew> SelectMany<TNew>(Func<TSource, IAsyncQuery<TNew>> selector) => EmptyIterator<TNew>.Default;

		}

	}

}
