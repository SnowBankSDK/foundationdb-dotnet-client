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

namespace SnowBank.Linq.Async.Iterators
{
	using SnowBank.Buffers;

	/// <summary>Performs a Merge Sort on several concurrent range queries</summary>
	/// <typeparam name="TSource">Type of the elements in the source queries</typeparam>
	/// <typeparam name="TKey">Type of values extracted from the keys, that will be used for sorting</typeparam>
	/// <typeparam name="TResult">Type of results returned</typeparam>
	public abstract class MergeAsyncIterator<TSource, TKey, TResult> : AsyncLinqIterator<TResult>
	{
		// Takes several range queries that return **SORTED** lists of items
		// - Make all iterators run concurrently
		// - At each step, finds the "smallest" value from all remaining iterators, transform it into a TResult and expose it as the current element
		// - Extract a TKey value from the keys and compare them with the provided comparer

		// The order of the extracted keys MUST be the same as the order of the binary keys ! This algorithm will NOT work if extracted keys are not in the same order as there binary representation !

		protected readonly IEnumerable<IAsyncQuery<TSource>> m_sources;
		protected readonly Func<TSource, TKey> m_keySelector;
		protected readonly IComparer<TKey> m_keyComparer;
		protected readonly Func<TSource, TResult> m_resultSelector;
		protected readonly CancellationToken m_ct;
		protected int? m_limit;

		protected IteratorState[]? m_iterators;
		protected int? m_remaining;

		protected struct IteratorState
		{
			public bool Active;
			public IAsyncEnumerator<TSource> Iterator;
			public ValueTask<bool> Next;
			public bool HasCurrent;
			public TKey Current;
		}

		protected MergeAsyncIterator(IEnumerable<IAsyncQuery<TSource>> sources, int? limit, Func<TSource, TKey> keySelector, Func<TSource, TResult> resultSelector, IComparer<TKey>? comparer, CancellationToken ct)
		{
			Contract.Debug.Requires(sources != null && (limit == null || limit >= 0) && keySelector != null && resultSelector != null);
			m_sources = sources;
			m_limit = limit;
			m_keySelector = keySelector;
			m_keyComparer = comparer ?? Comparer<TKey>.Default;
			m_resultSelector = resultSelector;
			m_ct = ct;
		}

		public override CancellationToken Cancellation => m_ct;

		protected override async ValueTask<bool> OnFirstAsync()
		{
			if (m_remaining is < 0)
			{ // empty list ??
				return await Completed().ConfigureAwait(false);
			}

			// even if the caller only wants the first, we will probably need to read more than that...
			var mode = m_mode;
			if (mode == AsyncIterationHint.Head) mode = AsyncIterationHint.Iterator;

			if (!m_sources.TryGetSpan(out var sources))
			{
				sources = m_sources.ToArray();
			}

			var iterators = new IteratorState[sources.Length];
			try
			{
				// start all the iterators
				for (int i = 0; i < sources.Length;i++)
				{
					var state = new IteratorState
					{
						Active = true,
						Iterator = sources[i].GetAsyncEnumerator(mode)
					};
					state.Next = state.Iterator.MoveNextAsync();

					iterators[i] = state;
				}

				m_remaining = m_limit;
				return iterators.Length > 0;
			}
			catch(Exception)
			{
				// dispose already opened iterators
				var tmp = iterators;
				iterators = null;
				try { await Cleanup(tmp).ConfigureAwait(false); } catch { }
				throw;
			}
			finally
			{
				m_iterators = iterators;
			}
		}

		/// <summary>Finds the next smallest item from all the active iterators</summary>
		protected override async ValueTask<bool> OnNextAsync()
		{
			if (m_remaining is <= 0)
			{
				return await Completed().ConfigureAwait(false);
			}

			int index;
			TSource current;

			var iterators = m_iterators;
			Contract.Debug.Requires(iterators != null);

			do
			{
				// ensure all iterators are ready
				for (int i = 0; i < iterators.Length;i++)
				{
					if (!iterators[i].Active) continue;

					if (!iterators[i].HasCurrent)
					{
						if (!await iterators[i].Next.ConfigureAwait(false))
						{ // this one is done, remove it
							await iterators[i].Iterator.DisposeAsync().ConfigureAwait(false);
							iterators[i] = default;
							continue;
						}

						iterators[i].Current = m_keySelector(iterators[i].Iterator.Current);
						iterators[i].HasCurrent = true;
					}

				}

				// find the next value to advance
				if (!FindNext(out index, out current))
				{ // nothing left anymore ?
					return await Completed().ConfigureAwait(false);
				}
			}
			while(index < 0);

			var result = m_resultSelector(current);

			// store the current pair
			if (!Publish(result))
			{ // something happened...
				return false;
			}

			// advance the current iterator
			--m_remaining;

			return true;
		}

		protected abstract bool FindNext(out int index, out TSource current);

		protected void AdvanceIterator(int index)
		{
			var iterators = m_iterators;
			Contract.Debug.Requires(iterators != null);
			iterators[index].HasCurrent = false;
			iterators[index].Current = default!;
			iterators[index].Next = iterators[index].Iterator.MoveNextAsync();
		}

		private static async ValueTask Cleanup(IteratorState[]? iterators)
		{
			if (iterators != null)
			{
				List<Exception>? errors = null;

				for (int i = 0; i < iterators.Length; i++)
				{
					if (iterators[i].Active)
					{
						try
						{
							var iterator = iterators[i].Iterator;
							iterators[i] = new IteratorState();
							await iterator.DisposeAsync().ConfigureAwait(false);
						}
						catch (Exception e)
						{
							(errors ??= new List<Exception>()).Add(e);
						}
					}
				}

				if (errors != null) throw new AggregateException(errors);
			}
		}

		protected override async ValueTask Cleanup()
		{
			try
			{
				await Cleanup(m_iterators).ConfigureAwait(false);
			}
			finally
			{
				m_iterators = null;
				m_remaining = 0;
			}
		}

	}

}
