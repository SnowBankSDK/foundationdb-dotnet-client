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

//enable this to enable verbose traces when doing paging
//#define DEBUG_RANGE_PAGING

namespace FoundationDB.Client
{
	using SnowBank.Linq.Async.Iterators;

	/// <summary>Async iterator that fetches the results by batch, but return them one by one</summary>
	[DebuggerDisplay("State={m_state}, Current={m_current}, Iteration={Iteration}, AtEnd={AtEnd}, HasMore={HasMore}")]
	internal sealed class FdbPagedIterator<TState, TResult> : AsyncLinqIterator<ReadOnlyMemory<TResult>>
	{

		#region Iterable Properties...

		private IFdbRangeQuery Query { get; }

		private TState State { get; }

		private KeySelectorPair Range { get; }

		private FdbKeyValueDecoder<TState, TResult> Decoder { get; }

		#endregion

		#region Iterator Properties...

		/// <summary>Key selector describing the beginning of the current range (when paging)</summary>
		private KeySelector Begin { get; set; }

		/// <summary>Key selector describing the end of the current range (when paging)</summary>
		private KeySelector End { get; set; }

		/// <summary>If non-null, contains the remaining allowed number of rows</summary>
		private int? RemainingCount { get; set; }

		/// <summary>If non-null, contains the remaining allowed number of bytes</summary>
		private int? RemainingSize { get; set; }

		/// <summary>Iteration number of current page (in iterator mode)</summary>
		private int Iteration { get; set; }

		/// <summary>Current page (may contain all records or only a segment at a time)</summary>
		private ReadOnlyMemory<TResult> Chunk { get; set; }

		/// <summary>If true, we have more records pending</summary>
		private bool HasMore { get; set; }

		/// <summary>True if we have reached the last page</summary>
		private bool AtEnd { get; set; }

		/// <summary>Running total of rows that have been read</summary>
		private int RowCount { get; set; }

		/// <summary>Current/Last batch read task</summary>
		private Task<bool>? PendingReadTask { get; set; }

		#endregion

		public FdbPagedIterator(IFdbRangeQuery query, KeySelectorPair range, TState state, FdbKeyValueDecoder<TState, TResult> decoder)
		{
			Contract.Debug.Requires(query != null);

			this.Query = query;
			this.Range = range;
			this.State = state;
			this.Decoder = decoder;
		}

		protected override AsyncLinqIterator<ReadOnlyMemory<TResult>> Clone()
		{
			return new FdbPagedIterator<TState, TResult>(this.Query, this.Range, this.State, this.Decoder);
		}

		#region IFdbAsyncEnumerator<T>...

		public override CancellationToken Cancellation => this.Query.Transaction.Cancellation;

		protected override async ValueTask<bool> OnFirstAsync()
		{
			this.RemainingCount = this.Query.Limit;
			this.RemainingSize = this.Query.TargetBytes;
			this.Begin = this.Query.Begin;
			this.End = this.Query.End;

			if (this.RemainingCount == 0)
			{
				// we can safely optimize this case by not doing any query, because it should not have any impact on conflict resolutions.
				// => The result of 'query.Take(0)' will not change even if someone adds/remove to the range
				// => The result of 'query.Take(X)' where X would be computed from reads in the db, and be equal to 0, would conflict because of those reads anyway.
				return false;
			}

			var bounds = this.Range;

			// if the original range has been changed, we need to ensure that the current begin/end do not overflow:
			if (this.Begin != bounds.Begin || this.End != bounds.End)
			{
				//TODO: find a better way to do this!
				var tr = this.Query.IsSnapshot ? this.Query.Transaction.Snapshot : this.Query.Transaction;
				var keys = await tr.GetKeysAsync([ bounds.Begin, this.Begin, bounds.End, this.End ]).ConfigureAwait(false);

				var min = keys[0] >= keys[1] ? keys[0] : keys[1];
				var max = keys[2] <= keys[3] ? keys[2] : keys[3];
				if (min >= max) return false;	// range is empty

				// rewrite the initial selectors with the bounded keys
				this.Begin = KeySelector.FirstGreaterOrEqual(min);
				this.End = KeySelector.FirstGreaterOrEqual(max);
			}
			return true;
		}

		protected override ValueTask<bool> OnNextAsync()
		{
			// Make sure that we are not called while the previous fetch is still running
			if (this.PendingReadTask != null && !this.PendingReadTask.IsCompleted)
			{
				throw new InvalidOperationException("Cannot fetch another page while a previous read operation is still pending");
			}

			if (this.AtEnd)
			{ // we already read the last batch !
				return Completed();
			}

			// slower path, we need to actually read the first batch...
			return new ValueTask<bool>(FetchNextPageAsync());
		}

		/// <summary>Asynchronously fetch a new page of results</summary>
		/// <returns>True if Chunk contains a new page of results. False if all results have been read.</returns>
		private Task<bool> FetchNextPageAsync()
		{
			Contract.Debug.Requires(!this.AtEnd);
			Contract.Debug.Requires(this.Iteration >= 0);

			this.Cancellation.ThrowIfCancellationRequested();

			this.Iteration++;

#if DEBUG_RANGE_PAGING
			Debug.WriteLine("FdbRangeQuery.PagingIterator.FetchNextPageAsync(iter=" + this.Iteration + ") started");
#endif

			var mode = this.Query.Streaming;
			// select the appropriate streaming mode if purpose is not default
			switch(m_mode)
			{
				case AsyncIterationHint.Iterator:
				{
					// the caller is responsible for calling MoveNext(..) and deciding if it wants to continue or not
					mode = FdbStreamingMode.Iterator;
					break;
				}
				case AsyncIterationHint.All:
				{
					// we are in a ToList or ForEach, we want to read everything in as few chunks as possible
					mode = FdbStreamingMode.WantAll;
					break;
				}
				case AsyncIterationHint.Head:
				{
					// the caller only expect one (or zero) values
					mode = FdbStreamingMode.Iterator;
					break;
				}
			}

			//BUGBUG: mix the custom cancellation token with the transaction, if it is different !
			var tr = (this.Query.IsSnapshot ? this.Query.Transaction.Snapshot : this.Query.Transaction);
			var query = tr.GetRangeAsync(
				this.Begin,
				this.End,
				this.State,
				this.Decoder,
				new FdbRangeOptions()
				{
					Limit = this.RemainingCount,
					IsReversed = this.Query.IsReversed,
					TargetBytes = this.RemainingSize,
					Streaming = mode,
					Fetch = this.Query.Fetch,
				},
				this.Iteration);
			var task = ProcessResults(query);

			// keep track of this operation
			this.PendingReadTask = task;
			return task;

			async Task<bool> ProcessResults(Task<FdbRangeChunk<TResult>> read)
			{
				var result = await read.ConfigureAwait(false); 

				this.Chunk = result.Items;
				this.RowCount += result.Count;
				this.HasMore = result.HasMore;

				// subtract number of row from the remaining allowed
				if (this.RemainingCount.HasValue) this.RemainingCount = this.RemainingCount.Value - result.Count;

				// subtract size of rows from the remaining allowed
				if (this.RemainingSize.HasValue) this.RemainingSize = this.RemainingSize.Value - result.TotalBytes;

				this.AtEnd = !result.HasMore || this.RemainingCount is <= 0 || this.RemainingSize is <= 0;

				if (!this.AtEnd)
				{
					// update begin...end so that next call will continue from where we left...
					if (this.Query.IsReversed)
					{
						this.End = KeySelector.FirstGreaterOrEqual(result.Last);
					}
					else
					{
						this.Begin = KeySelector.FirstGreaterThan(result.Last);
					}
				}
#if DEBUG_RANGE_PAGING
					Debug.WriteLine("FdbRangeQuery.PagingIterator.FetchNextPageAsync() returned " + this.Chunk.Length + " results (" + this.RowCount + " total) " + (hasMore ? " with more to come" : " and has no more data"));
#endif
				if (!result.IsEmpty)
				{
					return Publish(result.Items);
				}

				return await Completed().ConfigureAwait(false);
			}
		}

		#endregion

		protected override ValueTask Cleanup()
		{
			//TODO: should we wait/cancel any pending read task ?
			this.Chunk = default;
			this.AtEnd = true;
			this.HasMore = false;
			this.RemainingCount = null;
			this.RemainingSize = null;
			this.Iteration = -1;
			this.PendingReadTask = null;
			return default;
		}
	}

}
