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
	using System.Collections.Generic;
	using SnowBank.Linq.Async.Iterators;

	public static partial class AsyncQuery
	{

		/// <summary>Skips the first elements of an async sequence.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TSource> Skip<TSource>(this IAsyncQuery<TSource> source, int count)
		{
			Contract.NotNull(source);
			if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "Count cannot be less than zero");

			return source switch
			{
				IAsyncLinqQuery<TSource> iterator => iterator.Skip(count),
				_ => AsyncIterators.Skip(source, count)
			};
		}

		/// <summary>Returns a specified number of contiguous elements from the start of an async sequence.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TSource> Take<TSource>(this IAsyncQuery<TSource> source, int count)
		{
			Contract.NotNull(source);
			Contract.Positive(count, "Count cannot be less than zero");

			return source switch
			{
				IAsyncLinqQuery<TSource> iterator => iterator.Take(count),
				_ => AsyncIterators.Take(source, count)
			};
		}

		/// <summary>Returns a specified number of contiguous elements from the start of an async sequence.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TSource> Take<TSource>(this IAsyncQuery<TSource> source, Range range)
		{
			Contract.NotNull(source);

			return source is IAsyncLinqQuery<TSource> iterator ? iterator.Take(range) : AsyncIterators.Take(source, range);
		}

		/// <summary>Returns elements from an async sequence as long as a specified condition is true, and then skips the remaining elements.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TSource> TakeWhile<TSource>(this IAsyncQuery<TSource> source, Func<TSource, bool> condition)
		{
			Contract.NotNull(source);
			Contract.NotNull(condition);

			return source is IAsyncLinqQuery<TSource> iterator ? iterator.TakeWhile(condition) : AsyncIterators.TakeWhile(source, condition);
		}

		/// <summary>Returns elements from an async sequence as long as a specified condition is true, and then skips the remaining elements.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TSource> TakeWhile<TSource>(this IAsyncQuery<TSource> source, Func<TSource, bool> condition, out QueryStatistics<bool> stopped)
		{
			Contract.NotNull(source);
			Contract.NotNull(condition);

			var signal = new QueryStatistics<bool>(false);
			stopped = signal;

			// to trigger the signal, we just intercept the condition returning false (which only happen once!)
			bool Wrapped(TSource x)
			{
				if (condition(x)) return true;
				signal.Update(true);
				return false;
			}

			return TakeWhile(source, Wrapped);
		}

	}

	public static partial class AsyncIterators
	{

		/// <summary>Skips the first elements of an async sequence.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Skip<TResult>(IAsyncQuery<TResult> source, int offset)
		{
			Contract.Debug.Requires(source != null && offset >= 0);

			return new PaginatedAsyncIterator<TResult>(source, offset, null);
		}

		/// <summary>Returns a specified number of contiguous elements from the start of an async sequence.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Take<TResult>(IAsyncQuery<TResult> source, int limit)
		{
			Contract.Debug.Requires(source != null && limit >= 0);

			return new PaginatedAsyncIterator<TResult>(source, null, limit);
		}

		/// <summary>Returns a specified number of contiguous elements from the start of an async sequence.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> Take<TResult>(IAsyncQuery<TResult> source, Range range)
		{
			var start = range.Start;
			var end = range.End;

			if (!start.IsFromEnd && !end.IsFromEnd)
			{
				if (start.Value >= end.Value)
				{ // result would be empty
					return AsyncQuery.Empty<TResult>();
				}
				return new PaginatedAsyncIterator<TResult>(source, start.Value > 0 ? start.Value : null, end.Value - start.Value);
			}

			// we cannot pre-compute the index if they start from the end, because we don't already know the number of results!
#if NET10_0_OR_GREATER
			// for now, defer to the .NET 10's implementation, that buffers the results in memory
			return source.ToAsyncEnumerable().Take(range).ToAsyncQuery(source.Cancellation);
#else
			//TODO: do our own implementation for .NET 9 or lower ?
			throw new NotSupportedException("Support for from-end indexing requires .NET 10 ");
#endif
		}

		/// <summary>Returns elements from an async sequence as long as a specified condition is true, and then skips the remaining elements.</summary>
		[Pure, LinqTunnel]
		public static IAsyncLinqQuery<TResult> TakeWhile<TResult>(IAsyncQuery<TResult> source, Func<TResult, bool> condition)
		{
			Contract.Debug.Requires(source != null && condition != null);

			return new TakeWhileAsyncIterator<TResult>(source, condition);
		}

	}

	/// <summary>Iterator that can skip elements and/or return up to a specific amount</summary>
	/// <remarks>Logically, the Skip operation is performed first, then the Take operation.</remarks>
	internal sealed class PaginatedAsyncIterator<TResult> : AsyncLinqIterator<TResult>
	{

		public PaginatedAsyncIterator(IAsyncQuery<TResult> source, int? offset, int? limit)
		{
			Contract.Debug.Requires(source != null);
			Contract.Debug.Requires(offset != null || limit != null);

			this.Source = source;
			this.Offset = offset;
			this.Limit = limit;
		}

		private IAsyncQuery<TResult> Source { get; }

		private int? Offset { get; }

		private int? Limit { get; }

		/// <inheritdoc />
		public override CancellationToken Cancellation { get; }

		private IAsyncEnumerator<TResult>? Iterator { get; set; }

		private int? OffsetRemaining { get; set; }

		private int? LimitRemaining { get; set; }

		/// <inheritdoc />
		protected override AsyncLinqIterator<TResult> Clone() => new PaginatedAsyncIterator<TResult>(this.Source, this.Offset, this.Limit);

		/// <inheritdoc />
		protected override ValueTask<bool> OnFirstAsync()
		{
			this.Iterator = this.Source.GetAsyncEnumerator(m_mode);
			this.OffsetRemaining = this.Offset;
			this.LimitRemaining = this.Limit;
			return new(true);
		}

		/// <inheritdoc />
		protected override async ValueTask<bool> OnNextAsync()
		{
			var iterator = this.Iterator;
			if (iterator == null || this.LimitRemaining == 0)
			{
				return await Completed().ConfigureAwait(false);
			}

			var offset = this.OffsetRemaining ?? 0;

			while(await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				if (offset > 0)
				{
					--offset;
					this.OffsetRemaining = offset > 0 ? offset : null;
					continue;
				}

				if (this.LimitRemaining != null)
				{
					this.LimitRemaining--;
				}

				return Publish(iterator.Current);
			}

			return await Completed().ConfigureAwait(false);
		}

		/// <inheritdoc />
		protected override ValueTask Cleanup()
		{
			var iterator = this.Iterator;
			if (iterator == null) return default;

			this.Iterator = null;
			return iterator.DisposeAsync();
		}

		/// <inheritdoc />
		public override IAsyncLinqQuery<TResult> Take(int count)
		{
			Contract.Positive(count);

			if (count == 0)
			{ // the query will return nothing
				return AsyncQuery.Empty<TResult>();
			}

			if (this.Limit == null || count < this.Limit.Value)
			{
				return new PaginatedAsyncIterator<TResult>(this.Source, this.Offset, count);
			}

			// we are already taking less
			return this;
		}

		/// <inheritdoc />
		public override IAsyncLinqQuery<TResult> Skip(int count)
		{
			Contract.Positive(count);

			if (count == 0)
			{ // we skip nothing
				return this;
			}

			switch (this.Offset, this.Limit)
			{
				case (null, null):
				{ // no previous offset or limit
					return new PaginatedAsyncIterator<TResult>(this.Source, count, null);
				}
				case (_, null):
				{ // Skip().Skip(): we can just add the offsets
					var offset = checked(this.Offset.Value + count);

					return new PaginatedAsyncIterator<TResult>(this.Source, offset, null);
				}
				case (null, _):
				{ // Take().Skip(): we have to reduce the amount taken

					var limit = this.Limit.Value - count;
					if (limit <= 0) return AsyncQuery.Empty<TResult>();

					return new PaginatedAsyncIterator<TResult>(this.Source, count, limit);
				}
				default:
				{ // Skip().Take().Skip():
					var limit = this.Limit.Value - count;
					if (limit <= 0) return AsyncQuery.Empty<TResult>();
					var offset = checked(this.Offset.Value + count);

					return new PaginatedAsyncIterator<TResult>(this.Source, offset, limit);
				}

			}
		}

	}

}
