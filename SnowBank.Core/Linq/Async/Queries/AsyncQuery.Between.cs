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
	using System.Numerics;
	using SnowBank.Linq.Async.Iterators;

	public static partial class AsyncQuery
	{

		/// <summary>Generates a sequence of integral numbers between two bounds.</summary>
		/// <param name="beginInclusive">The value of the first integer in the sequence.</param>
		/// <param name="endExclusive">The value at which the sequence stops iterating.</param>
		/// <param name="ct">Token used to cancel the execution of this query</param>
		/// <returns>An <see cref="IAsyncLinqQuery{T}"/> that contains a range of sequential integral numbers from <paramref name="beginInclusive"/> (included) to <paramref name="endExclusive"/> (excluded).</returns>
		/// <remarks>The sequence is empty if <paramref name="beginInclusive"/> is greater than or equal to <paramref name="endExclusive"/></remarks>
		public static IAsyncLinqQuery<int> Between(int beginInclusive, int endExclusive, CancellationToken ct = default)
		{
			return endExclusive <= beginInclusive
				? Empty<int>()
				: new RangeIterator<int>(beginInclusive, 1, endExclusive - beginInclusive, ct);
		}

		/// <summary>Generates a sequence of integral numbers between two bounds.</summary>
		/// <param name="beginInclusive">The value of the first integer in the sequence.</param>
		/// <param name="endExclusive">The value at which the sequence stops iterating.</param>
		/// <param name="ct">Token used to cancel the execution of this query</param>
		/// <returns>An <see cref="IAsyncLinqQuery{T}"/> that contains a range of sequential integral numbers from <paramref name="beginInclusive"/> (included) to <paramref name="endExclusive"/> (excluded).</returns>
		/// <remarks>The sequence is empty if <paramref name="beginInclusive"/> is greater than or equal to <paramref name="endExclusive"/></remarks>
		public static IAsyncLinqQuery<long> Between(long beginInclusive, long endExclusive, CancellationToken ct = default)
		{
			if (endExclusive <= beginInclusive) return Empty<long>();

			long count = endExclusive - beginInclusive;
			return count <= int.MaxValue
				? new RangeIterator<long>(beginInclusive, 1L, (int) count, ct)
				: new BetweenIterator<long>(beginInclusive, endExclusive, (x) => x + 1, Comparer<long>.Default, ct);
		}

		/// <summary>Generates a sequence of integral numbers between two bounds.</summary>
		/// <param name="beginInclusive">The value of the first integer in the sequence.</param>
		/// <param name="endExclusive">The value at which the sequence stops iterating.</param>
		/// <param name="ct">Token used to cancel the execution of this query</param>
		/// <returns>An <see cref="IAsyncLinqQuery{T}"/> that contains a range of sequential integral numbers from <paramref name="beginInclusive"/> (included) to <paramref name="endExclusive"/> (excluded).</returns>
		/// <remarks>The sequence is empty if <paramref name="beginInclusive"/> is greater than or equal to <paramref name="endExclusive"/></remarks>
		public static IAsyncLinqQuery<TNumber> Between<TNumber>(TNumber beginInclusive, TNumber endExclusive, CancellationToken ct = default) where TNumber : IIncrementOperators<TNumber>
			=> new BetweenIterator<TNumber>(beginInclusive, endExclusive, (x) => ++x, Comparer<TNumber>.Default, ct);

		/// <summary>Generates a sequence of integral numbers between two bounds.</summary>
		/// <param name="beginInclusive">The value of the first integer in the sequence.</param>
		/// <param name="endExclusive">The value at which the sequence stops iterating.</param>
		/// <param name="successor">Function which is called to produce the next element in the sequence of result. It <b>MUST</b> always return an element that is strictly greater than its input, otherwise the sequence will never terminate.</param>
		/// <param name="ct">Token used to cancel the execution of this query</param>
		/// <returns>An <see cref="IAsyncLinqQuery{T}"/> that contains a range of sequential integral numbers from <paramref name="beginInclusive"/> (included) to <paramref name="endExclusive"/> (excluded).</returns>
		/// <remarks>The sequence is empty if <paramref name="beginInclusive"/> is greater than or equal to <paramref name="endExclusive"/></remarks>
		public static IAsyncLinqQuery<TValue> Between<TValue>(TValue beginInclusive, TValue endExclusive, Func<TValue, TValue> successor, CancellationToken ct = default)
			=> new BetweenIterator<TValue>(beginInclusive, endExclusive, successor, Comparer<TValue>.Default, ct);

		/// <summary>Generates a sequence of integral numbers between two bounds.</summary>
		/// <param name="beginInclusive">The value of the first integer in the sequence.</param>
		/// <param name="endExclusive">The value at which the sequence stops iterating.</param>
		/// <param name="successor">Function which is called to produce the next element in the sequence of result. It <b>MUST</b> always return an element that is strictly greater than its input, otherwise the sequence will never terminate.</param>
		/// <param name="comparer">Instance use to compare values in the sequence. The sequence will continue enumerating as long as this comparing the current cursor with <paramref name="endExclusive"/> returns a negative value.</param>
		/// <param name="ct">Token used to cancel the execution of this query</param>
		/// <returns>An <see cref="IAsyncLinqQuery{T}"/> that contains a range of sequential integral numbers from <paramref name="beginInclusive"/> (included) to <paramref name="endExclusive"/> (excluded).</returns>
		/// <remarks>The sequence is empty if <paramref name="beginInclusive"/> is greater than or equal to <paramref name="endExclusive"/>, according to <paramref name="comparer"/>.</remarks>
		public static IAsyncLinqQuery<TValue> Between<TValue>(TValue beginInclusive, TValue endExclusive, Func<TValue, TValue> successor, IComparer<TValue>? comparer, CancellationToken ct = default)
			=> new BetweenIterator<TValue>(beginInclusive, endExclusive, successor, comparer ?? Comparer<TValue>.Default, ct);

		/// <summary>Async iterator that returns a set of increasing values between two bounds</summary>
		/// <typeparam name="TValue">Type of the values returned by this query</typeparam>
		internal sealed class BetweenIterator<TValue> : AsyncLinqIterator<TValue>
		{

			public BetweenIterator(TValue beginInclusive, TValue endExclusive, Func<TValue, TValue> successor, IComparer<TValue> comparer, CancellationToken ct)
			{
				Contract.Debug.Requires(successor != null && comparer != null);

				this.BeginInclusive = beginInclusive;
				this.EndExclusive = endExclusive;
				this.Successor = successor;
				this.Comparer = comparer;
				this.Cursor = default!;
				this.Cancellation = ct;

#if DEBUG
				// sanity check the successor, to prevent infinite loops!
				if (comparer.Compare(beginInclusive, successor(beginInclusive)) >= 0)
				{
					throw new InvalidOperationException("The successor function MUST return a value that is greater than the previous value.");
				}
#endif
			}

			/// <summary>Initial value of this query</summary>
			public TValue BeginInclusive { get; }

			/// <summary>Value at which the query stops enumerating</summary>
			/// <remarks>This value is excluded from the results</remarks>
			public TValue EndExclusive { get; }

			/// <summary>Function called at each step to get the next value</summary>
			/// <remarks>This function MUST return a value that is strictly greater than the previous value (according to <see cref="Comparer"/>), otherwise the iterator will never complete.</remarks>
			public Func<TValue, TValue> Successor { get; }

			public IComparer<TValue> Comparer { get; }

			private TValue Cursor { get; set; }

			/// <inheritdoc />
			public override CancellationToken Cancellation { get; }

			/// <inheritdoc />
			protected override BetweenIterator<TValue> Clone() => new(this.BeginInclusive, this.EndExclusive, this.Successor, this.Comparer, this.Cancellation);

			/// <inheritdoc />
			protected override ValueTask<bool> OnFirstAsync()
			{
				this.Cursor = this.BeginInclusive;
				return new(true);
			}

			/// <inheritdoc />
			protected override ValueTask<bool> OnNextAsync()
			{
				var cursor = this.Cursor;
				if (this.Comparer.Compare(cursor, this.EndExclusive) >= 0)
				{
					return this.Completed();
				}

				this.Cursor = this.Successor(cursor);
				return new(this.Publish(cursor));
			}

			/// <inheritdoc />
			protected override ValueTask Cleanup()
			{
				this.Cursor = default!;
				return default;
			}

			/// <inheritdoc />
			public override Task ExecuteAsync(Action<TValue> handler)
			{
				var cursor = this.BeginInclusive;
				var end = this.EndExclusive;
				var successor = this.Successor;
				var comparer = this.Comparer;
				while (comparer.Compare(cursor, end) < 0)
				{
					handler(cursor);
					cursor = successor(cursor);
				}

				return Task.CompletedTask;
			}

			/// <inheritdoc />
			public override Task ExecuteAsync<TState>(TState state, Action<TState, TValue> handler)
			{
				var cursor = this.BeginInclusive;
				var end = this.EndExclusive;
				var successor = this.Successor;
				var comparer= this.Comparer;
				while (comparer.Compare(cursor, end) < 0)
				{
					handler(state, cursor);
					cursor = successor(cursor);
				}

				return Task.CompletedTask;
			}

			/// <inheritdoc />
			public override async Task ExecuteAsync(Func<TValue, CancellationToken, Task> handler)
			{
				var cursor = this.BeginInclusive;
				var end = this.EndExclusive;
				var successor = this.Successor;
				var comparer= this.Comparer;
				var ct = this.Cancellation;
				while (comparer.Compare(cursor, end) < 0)
				{
					await handler(cursor, ct).ConfigureAwait(false);
					cursor = successor(cursor);
				}
			}

			/// <inheritdoc />
			public override async Task ExecuteAsync<TState>(TState state, Func<TState, TValue, CancellationToken, Task> handler)
			{
				var cursor = this.BeginInclusive;
				var end = this.EndExclusive;
				var successor = this.Successor;
				var comparer= this.Comparer;
				var ct = this.Cancellation;

				while (comparer.Compare(cursor, end) < 0)
				{
					await handler(state, cursor, ct).ConfigureAwait(false);
					cursor = successor(cursor);
				}
			}

			/// <inheritdoc />
			public override Task<TAggregate> ExecuteAsync<TAggregate>(TAggregate seed, Func<TAggregate, TValue, TAggregate> handler)
			{
				var accumulator = seed;
				var cursor = this.BeginInclusive;
				var end = this.EndExclusive;
				var successor = this.Successor;
				var comparer= this.Comparer;

				while (comparer.Compare(cursor, end) < 0)
				{
					accumulator = handler(accumulator, cursor);
					cursor = successor(cursor);
				}

				return Task.FromResult(accumulator);
			}

			/// <inheritdoc />
			public override Task<TAggregate> ExecuteAsync<TState, TAggregate>(TState state, TAggregate seed, Func<TState, TAggregate, TValue, TAggregate> handler)
			{
				var accumulator = seed;
				var cursor = this.BeginInclusive;
				var end = this.EndExclusive;
				var successor = this.Successor;
				var comparer= this.Comparer;

				while (comparer.Compare(cursor, end) < 0)
				{
					accumulator = handler(state, accumulator, cursor);
					cursor = successor(cursor);
				}

				return Task.FromResult(accumulator);
			}

			internal List<TValue> ToListInternal()
			{
				var cursor = this.BeginInclusive;
				var end = this.EndExclusive;
				var comparer = this.Comparer;
				var successor = this.Successor;

				var res = new List<TValue>(); // we don't have a reliable way to know the size in advance

				while (comparer.Compare(cursor, end) < 0)
				{
					res.Add(cursor);
					cursor = successor(cursor);
				}

				return res;
			}

			/// <summary>Tests if this range is empty</summary>
			/// <remarks>The range is considered empty if <see cref="BeginInclusive"/> is already greater than or equal to <see cref="EndExclusive"/></remarks>
			public bool IsEmpty() => this.Comparer.Compare(this.BeginInclusive, this.EndExclusive) >= 0;

			/// <inheritdoc />
			public override Task<List<TValue>> ToListAsync() => Task.FromResult(ToListInternal());

			/// <inheritdoc />
			public override Task<TValue[]> ToArrayAsync() => Task.FromResult(ToListInternal().ToArray());

			/// <inheritdoc />
			public override Task<ImmutableArray<TValue>> ToImmutableArrayAsync() => Task.FromResult(ToListInternal().ToImmutableArray());

			/// <inheritdoc />
			public override Task<int> CountAsync()
			{
				var cursor = this.BeginInclusive;
				var end = this.EndExclusive;
				var comparer = this.Comparer;
				var successor = this.Successor;

				long count = 0;

				while (comparer.Compare(cursor, end) < 0)
				{
					++count;
					cursor = successor(cursor);
				}

				return Task.FromResult(checked((int) count));
			}

			/// <inheritdoc />
			public override Task<bool> AnyAsync() => Task.FromResult(this.Comparer.Compare(this.BeginInclusive, this.EndExclusive) < 0);

			/// <inheritdoc />
			public override Task<TValue> FirstOrDefaultAsync(TValue defaultValue)
			{
				return Task.FromResult(IsEmpty() ? defaultValue : this.BeginInclusive);
			}

			/// <inheritdoc />
			public override Task<TValue> FirstAsync()
			{
				return IsEmpty() ? Task.FromException<TValue>(ErrorNoElements()) : Task.FromResult(this.BeginInclusive);
			}

			/// <inheritdoc />
			public override Task<TValue?> MinAsync(IComparer<TValue>? comparer = null)
			{
				if (comparer != null && !comparer.Equals(this.Comparer))
				{ // the comparer is not compatible, we will have to defer to the base implementation
					return base.MinAsync(comparer);
				}

				return !IsEmpty() ? Task.FromResult<TValue?>(this.BeginInclusive)
					: default(TValue) is null ? Task.FromResult(default(TValue))
					: Task.FromException<TValue?>(ErrorNoElements());
			}

		}

	}

}
