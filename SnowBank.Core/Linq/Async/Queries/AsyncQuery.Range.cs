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
	using System.Runtime.InteropServices;
	using SnowBank.Linq.Async.Iterators;

	public static partial class AsyncQuery
	{

		/// <summary>Generates a sequence of integral numbers within a specified range.</summary>
		/// <param name="start">The value of the first integer in the sequence.</param>
		/// <param name="count">The number of sequential integers to generate.</param>
		/// <param name="ct">Token used to cancel the execution of this query</param>
		/// <returns>An <see cref="IAsyncLinqQuery{T}"/> that contains a range of sequential integral numbers.</returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than 0</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="start"/> + <paramref name="count"/> -1 is larger than <see cref="int.MaxValue"/>.</exception>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<int> Range(int start, int count, CancellationToken ct = default)
		{
			if (count < 0 || (((long)start) + count - 1) > int.MaxValue)
			{
				ThrowHelper.ThrowArgumentOutOfRangeException(nameof(count));
			}

			return new RangeIterator<int>(start, 1, count, ct);
		}

		/// <summary>Generates a sequence of integral numbers within a specified range.</summary>
		/// <param name="start">The value of the first element returned by the query.</param>
		/// <param name="delta">The value that is added to each value return by the query.</param>
		/// <param name="count">The number of elements returned by the query.</param>
		/// <param name="ct">Token used to cancel the execution of this query</param>
		/// <returns>An <see cref="IAsyncQuery{T}"/> that contains a range of sequential numbers.</returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than 0</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="start"/> + <paramref name="count"/> -1 is larger than <see cref="int.MaxValue"/>.</exception>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TNumber> Range<TNumber>(TNumber start, TNumber delta, int count, CancellationToken ct = default)
			where TNumber : INumberBase<TNumber>
		{
			if (count < 0)
			{
				ThrowHelper.ThrowArgumentOutOfRangeException(nameof(count));
			}
			return new RangeIterator<TNumber>(start, delta, count, ct);
		}

		/// <summary>Async iterator that returns a set of numbers, with a fixed increment</summary>
		/// <typeparam name="TNumber">Type of the numbers returned by this query</typeparam>
		internal sealed class RangeIterator<TNumber> : AsyncLinqIterator<TNumber>
			where TNumber : INumberBase<TNumber>
		{

			public RangeIterator(TNumber start, TNumber delta, int count, CancellationToken ct)
			{
				this.Start = start;
				this.Delta = delta;
				this.Count = count;
				this.Cancellation = ct;
			}

			/// <summary>Initial value of the query</summary>
			public TNumber Start { get; }

			/// <summary>Delta that is added to the value at each step</summary>
			public TNumber Delta { get; }

			/// <summary>Number of values returned by this query</summary>
			public int Count { get; }

			/// <summary>Current value when this iterator is enumerated</summary>
			private TNumber Cursor { get; set; } = TNumber.Zero;

			/// <summary>Number of remaining values, when this iterator is enumerated</summary>
			private int Remaining { get; set; }

			/// <inheritdoc />
			public override CancellationToken Cancellation { get; }

			/// <inheritdoc />
			protected override RangeIterator<TNumber> Clone() => new(this.Start, this.Delta, this.Count, this.Cancellation);

			/// <inheritdoc />
			protected override ValueTask<bool> OnFirstAsync()
			{
				this.Cursor = this.Start;
				this.Remaining = this.Count;
				return new(true);
			}

			/// <inheritdoc />
			protected override ValueTask<bool> OnNextAsync()
			{
				var remaining = this.Remaining;
				if (remaining <= 0)
				{
					return this.Completed();
				}

				var cursor = this.Cursor;
				this.Cursor = cursor + this.Delta;
				this.Remaining = remaining - 1;
				return new(this.Publish(cursor));
			}

			/// <inheritdoc />
			protected override ValueTask Cleanup()
			{
				this.Cursor = TNumber.Zero;
				this.Remaining = 0;
				return default;
			}

			/// <inheritdoc />
			public override Task ExecuteAsync(Action<TNumber> handler)
			{
				var cursor = this.Start;
				var delta = this.Delta;
				var count = this.Count;
				for(int i = 0; i < count; i++)
				{
					handler(cursor);
					cursor += delta;
				}

				return Task.CompletedTask;
			}

			/// <inheritdoc />
			public override Task ExecuteAsync<TState>(TState state, Action<TState, TNumber> handler)
			{
				var cursor = this.Start;
				var delta = this.Delta;
				var count = this.Count;
				for(int i = 0; i < count; i++)
				{
					handler(state, cursor);
					cursor += delta;
				}

				return Task.CompletedTask;
			}

			/// <inheritdoc />
			public override async Task ExecuteAsync(Func<TNumber, CancellationToken, Task> handler)
			{
				var cursor = this.Start;
				var delta = this.Delta;
				var count = this.Count;
				var ct = this.Cancellation;
				for(int i = 0; i < count; i++)
				{
					await handler(cursor, ct).ConfigureAwait(false);
					cursor += delta;
				}
			}

			/// <inheritdoc />
			public override async Task ExecuteAsync<TState>(TState state, Func<TState, TNumber, CancellationToken, Task> handler)
			{
				var cursor = this.Start;
				var delta = this.Delta;
				var count = this.Count;
				var ct = this.Cancellation;
				for(int i = 0; i < count; i++)
				{
					await handler(state, cursor, ct).ConfigureAwait(false);
					checked { cursor += delta; }
				}
			}

			/// <inheritdoc />
			public override Task<TAggregate> ExecuteAsync<TAggregate>(TAggregate seed, Func<TAggregate, TNumber, TAggregate> handler)
			{
				var cursor = this.Start;
				var delta = this.Delta;
				var count = this.Count;
				var accumulator = seed;
				for(int i = 0; i < count; i++)
				{
					accumulator = handler(accumulator, cursor);
					checked { cursor += delta; }
				}

				return Task.FromResult(accumulator);
			}

			/// <inheritdoc />
			public override Task<TAggregate> ExecuteAsync<TState, TAggregate>(TState state, TAggregate seed, Func<TState, TAggregate, TNumber, TAggregate> handler)
			{
				var cursor = this.Start;
				var delta = this.Delta;
				var count = this.Count;
				var accumulator = seed;
				for(int i = 0; i < count; i++)
				{
					accumulator = handler(state, accumulator, cursor);
					checked { cursor += delta; }
				}

				return Task.FromResult(accumulator);
			}

			/// <summary>Fill a span with all the results of this query</summary>
			private static void FillSpan(Span<TNumber> buffer, TNumber start, TNumber delta, int count)
			{
				var cursor = start;
				for(int i = 0; i < count; i++)
				{
					buffer[i] = cursor;
					checked { cursor += delta; }
				}
			}

			/// <inheritdoc />
			public override Task<List<TNumber>> ToListAsync()
			{
				int count = this.Count;
				if (count == 0) return Task.FromResult<List<TNumber>>([ ]);

				//note: Enumerable.Range<int>.ToList() has an optimized implementation that uses SIMD, so we should probably hot-path it!
				if (typeof(TNumber) == typeof(int))
				{
					return (Task<List<TNumber>>) (object) Task.FromResult(Enumerable.Range((int) (object) this.Start, count).ToList());
				}

				var res = new List<TNumber>(count);
				CollectionsMarshal.SetCount(res, count);
				FillSpan(CollectionsMarshal.AsSpan(res), this.Start, this.Delta, count);
				return Task.FromResult(res);
			}

			/// <inheritdoc />
			public override Task<TNumber[]> ToArrayAsync()
			{
				int count = this.Count;
				if (count == 0) return Task.FromResult(Array.Empty<TNumber>());

				//note: Enumerable.Range<int>.ToArray() has an optimized implementation that uses SIMD, so we should probably hot-path it!
				if (typeof(TNumber) == typeof(int))
				{
					// ReSharper disable once SuspiciousTypeConversion.Global
					return (Task<TNumber[]>) (object) Task.FromResult(Enumerable.Range((int) (object) this.Start, count).ToArray());
				}

				var res = new TNumber[count];
				FillSpan(res, this.Start, this.Delta, count);
				return Task.FromResult(res);
			}

			/// <inheritdoc />
			public override Task<ImmutableArray<TNumber>> ToImmutableArrayAsync()
			{
				int count = this.Count;
				if (count == 0) return Task.FromResult(ImmutableArray<TNumber>.Empty);

				var builder = ImmutableArray.CreateBuilder<TNumber>(count);
				var cursor = this.Start;
				var delta = this.Delta;
				for(int i = 0; i < count; i++)
				{
					builder.Add(cursor);
					checked { cursor += delta; }
				}
				return Task.FromResult(builder.ToImmutable());
			}

			/// <inheritdoc />
			public override Task<int> CountAsync() => Task.FromResult(this.Count);

			/// <inheritdoc />
			public override Task<bool> AnyAsync() => Task.FromResult(this.Count > 0);

			/// <inheritdoc />
			public override Task<TNumber> FirstOrDefaultAsync(TNumber defaultValue)
			{
				return Task.FromResult(this.Count > 0 ? this.Start : defaultValue);
			}

			/// <inheritdoc />
			public override Task<TNumber> FirstAsync()
			{
				return this.Count > 0 ? Task.FromResult(this.Start) : Task.FromException<TNumber>(ErrorNoElements());
			}

			/// <inheritdoc />
			public override Task<TNumber> LastOrDefaultAsync(TNumber defaultValue)
			{
				return Task.FromResult(this.Count > 0 ? this.Start + (this.Delta * TNumber.CreateChecked(this.Count - 1)) : defaultValue);
			}

			/// <inheritdoc />
			public override Task<TNumber> LastAsync()
			{
				return this.Count > 0 ? Task.FromResult(this.Start + (this.Delta * TNumber.CreateChecked(this.Count - 1))) : Task.FromException<TNumber>(ErrorNoElements());
			}


			/// <inheritdoc />
			public override Task<TNumber> SingleOrDefaultAsync(TNumber defaultValue) => this.Count switch
			{
				0 => Task.FromResult(defaultValue),
				1 => Task.FromResult(this.Start),
				_ => Task.FromException<TNumber>(ErrorMoreThenOneElement())
			};

			/// <inheritdoc />
			public override Task<TNumber> SingleAsync() => this.Count switch
			{
				0 => Task.FromException<TNumber>(ErrorNoElements()),
				1 => Task.FromResult(this.Start),
				_ => Task.FromException<TNumber>(ErrorMoreThenOneElement())
			};

			/// <inheritdoc />
			public override Task<TNumber?> MinAsync(IComparer<TNumber>? comparer = null)
			{
				if (this.Count == 0)
				{
					return default(TNumber) is null ? Task.FromResult(default(TNumber)) : Task.FromException<TNumber?>(ErrorNoElements());
				}
				return TNumber.IsPositive(this.Delta)
					? Task.FromResult<TNumber?>(this.Start)
					: Task.FromResult<TNumber?>(checked(this.Start + this.Delta * TNumber.CreateChecked(this.Count - 1)));
			}

			/// <inheritdoc />
			public override Task<TNumber?> MaxAsync(IComparer<TNumber>? comparer = null)
			{
				if (this.Count == 0)
				{
					return default(TNumber) is null ? Task.FromResult(default(TNumber)) : Task.FromException<TNumber?>(ErrorNoElements());
				}
				return TNumber.IsPositive(this.Delta)
					? Task.FromResult<TNumber?>(checked(this.Start + this.Delta * TNumber.CreateChecked(this.Count - 1)))
					: Task.FromResult<TNumber?>(this.Start);
			}

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNumber> Take(int count)
			{
				Contract.Positive(count);
				if (count == 0) return AsyncQuery.Empty<TNumber>();
				if (count >= this.Count) return this;

				return new RangeIterator<TNumber>(this.Start, this.Delta, count, this.Cancellation);
			}

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNumber> Take(Range range)
			{
				if (typeof(TNumber) == typeof(int))
				{
					var (offset, length) = range.GetOffsetAndLength(this.Count);
					if (length <= 0) return AsyncQuery.Empty<TNumber>();

					int start = (int) (object) this.Start;
					int delta = (int) (object) this.Delta;

					return (RangeIterator<TNumber>) (object) new RangeIterator<int>(start + offset * delta, delta, length, this.Cancellation);
				}

				return base.Take(range);
			}

			/// <inheritdoc />
			public override IAsyncLinqQuery<TNumber> Skip(int count)
			{
				Contract.Positive(count);
				int remaining = checked(this.Count - count);

				if (remaining <= 0)
				{
					return AsyncQuery.Empty<TNumber>();
				}

				var start = checked(this.Start + this.Delta * TNumber.CreateChecked(count));

				return new RangeIterator<TNumber>(start, this.Delta, remaining, this.Cancellation);
			}

		}

	}

}
