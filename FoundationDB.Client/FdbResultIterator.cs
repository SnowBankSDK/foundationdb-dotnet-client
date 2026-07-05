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
//#define DEBUG_RANGE_ITERATOR

namespace FoundationDB.Client
{
	using SnowBank.Linq.Async.Iterators;

	/// <summary>Async iterator that fetches the results by batch, but return them one by one</summary>
	[DebuggerDisplay("State={m_state}, Current={m_current}, RemainingInChunk={m_itemsRemainingInChunk}, OutOfChunks={m_outOfChunks}")]
	internal sealed class FdbResultIterator<TState, TResult> : AsyncLinqIterator<TResult>
	{

		private readonly FdbRangeQuery<TState, TResult> m_query;

		private readonly TState m_queryState;

		/// <summary>Iterator used to read chunks from the database</summary>
		private IAsyncEnumerator<ReadOnlyMemory<TResult>>? m_chunkIterator;

		/// <summary>True if we have reached the last page</summary>
		private bool m_outOfChunks;

		/// <summary>Current chunk (may contain all records or only a segment at a time)</summary>
		private ReadOnlyMemory<TResult> m_chunk;

		/// <summary>Number of remaining items in the current batch</summary>
		private int m_itemsRemainingInChunk;

		/// <summary>Offset in the current batch of the current item</summary>
		private int m_currentOffsetInChunk;

		#region IFdbAsyncEnumerator<T>...

		public FdbResultIterator(FdbRangeQuery<TState, TResult> query, TState state)
		{
			Contract.Debug.Requires(query != null);

			m_query = query;
			m_queryState = state;
		}

		protected override AsyncLinqIterator<TResult> Clone()
		{
			return new FdbResultIterator<TState, TResult>(m_query, m_queryState);
		}

		public override CancellationToken Cancellation => m_query.Transaction.Cancellation;

		protected override ValueTask<bool> OnFirstAsync()
		{
			// on first call, set up the page iterator
			m_chunkIterator ??= new FdbPagedIterator<TState, TResult>(m_query, m_query.OriginalRange, m_queryState, m_query.Decoder).GetAsyncEnumerator(m_mode);
			return new ValueTask<bool>(true);
		}

		protected override ValueTask<bool> OnNextAsync()
		{
			if (m_itemsRemainingInChunk > 0)
			{ // we need can get another one from the batch

				return new ValueTask<bool>(ProcessNextItem());
			}

			if (m_outOfChunks)
			{ // we already read the last batch !
#if DEBUG_RANGE_ITERATOR
				Debug.WriteLine("No more items and it was the last batch");
#endif
				return new ValueTask<bool>(false);
			}

			// slower path, we need to actually read the first batch...
			m_chunk = default;
			m_currentOffsetInChunk = -1;
			return ReadAnotherBatchAsync();
		}

		private async ValueTask<bool> ReadAnotherBatchAsync()
		{
			Contract.Debug.Requires(m_itemsRemainingInChunk == 0 && m_currentOffsetInChunk == -1 && !m_outOfChunks);

			var iterator = m_chunkIterator;
			Contract.Debug.Requires(iterator != null);

			// start reading the next batch
			if (await iterator.MoveNextAsync().ConfigureAwait(false))
			{ // we got a new chunk !

				//note: Dispose() or Cleanup() maybe have been called concurrently!
				EnsureIsIterating();

				var chunk = iterator.Current;

				//note: if the range is empty, we may have an empty chunk, that is equivalent to no chunk
				if (chunk.Length > 0)
				{
#if DEBUG_RANGE_ITERATOR
					Debug.WriteLine("Got a new chunk from page iterator: " + chunk.Length);
#endif
					m_chunk = chunk;
					m_itemsRemainingInChunk = chunk.Length;

					// transform the first one
					return ProcessNextItem();
				}
#if DEBUG_RANGE_ITERATOR
				Debug.WriteLine("Got empty chunk from page iterator!");
#endif
			}

#if DEBUG_RANGE_ITERATOR
			Debug.WriteLine("No more chunks from page iterator");
#endif
			m_outOfChunks = true;
			return await Completed().ConfigureAwait(false);
		}

		private bool ProcessNextItem()
		{
			Contract.Debug.Requires(m_chunk.Length > 0 && m_itemsRemainingInChunk > 0);
			++m_currentOffsetInChunk;
			--m_itemsRemainingInChunk;
			return Publish(m_chunk.Span[m_currentOffsetInChunk]);
		}

		#endregion

		#region LINQ

#if NET5_0_OR_GREATER
		public override AsyncLinqIterator<TOther> Select<TOther>(Func<TResult, TOther> selector)
#else
		// covariant return types in overrides need runtime support that netstandard2.0/NetFX lacks: this override returns the base method's type instead
		public override IAsyncLinqQuery<TOther> Select<TOther>(Func<TResult, TOther> selector)
#endif
		{
			var query = (FdbRangeQuery<TState, TOther>) m_query.Select(selector);

			return new FdbResultIterator<TState, TOther>(query, m_queryState);
		}

#if NET5_0_OR_GREATER
		public override AsyncLinqIterator<TResult> Take(int limit)
#else
		// covariant return types in overrides need runtime support that netstandard2.0/NetFX lacks: this override returns the base method's type instead
		public override IAsyncLinqQuery<TResult> Take(int limit)
#endif
		{
			var query = (FdbRangeQuery<TState, TResult>) m_query.Take(limit);
			return new FdbResultIterator<TState, TResult>(query, m_queryState);
		}

		#endregion

		protected override async ValueTask Cleanup()
		{
			try
			{
				if (m_chunkIterator != null)
				{
					await m_chunkIterator.DisposeAsync().ConfigureAwait(false);
				}
			}
			finally
			{
				m_chunkIterator = null;
				m_chunk = null;
				m_outOfChunks = true;
				m_currentOffsetInChunk = 0;
				m_itemsRemainingInChunk = 0;
			}
		}

	}

}
