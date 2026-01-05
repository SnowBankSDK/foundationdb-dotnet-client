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

namespace SnowBank.Linq.Iterators
{

	/// <summary>Performs a Merge Sort on several concurrent range queries</summary>
	/// <typeparam name="TSource">Type of the elements in the source queries</typeparam>
	/// <typeparam name="TKey">Type of values extracted from the keys, that will be used for sorting</typeparam>
	/// <typeparam name="TResult">Type of results returned</typeparam>
	public abstract class MergeIterator<TSource, TKey, TResult> : Iterator<TResult>
	{

		// Takes several range queries that return **SORTED** lists of items
		// - Make all queries iterators run concurrently
		// - At each step, finds the "smallest" value from all remaining iterators, transform it into a TResult and expose it as the current element
		// - Extract a TKey value from the keys and compare them with the provided comparer

		// The order of the extracted keys MUST be the same as the order of the binary keys ! This algorithm will NOT work if extracted keys are not in the same order as there binary representation !

		protected IEnumerable<IEnumerable<TSource>> m_sources;
		protected Func<TSource, TKey> m_keySelector;
		protected IComparer<TKey> m_keyComparer;
		protected Func<TSource, TResult> m_resultSelector;
		protected int? m_limit;

		protected IteratorState[]? m_iterators;
		protected int? m_remaining;

		protected struct IteratorState
		{
			public bool Active;
			public IEnumerator<TSource> Iterator;
			public bool Next;
			public bool HasCurrent;
			public TKey Current;
		}

		protected MergeIterator(IEnumerable<IEnumerable<TSource>> sources, int? limit, Func<TSource, TKey> keySelector, Func<TSource, TResult> resultSelector, IComparer<TKey> comparer)
		{
			Contract.Debug.Requires(sources != null && (limit == null || limit >= 0) && keySelector != null && resultSelector != null);
			m_sources = sources;
			m_limit = limit;
			m_keySelector = keySelector;
			m_keyComparer = comparer ?? Comparer<TKey>.Default;
			m_resultSelector = resultSelector;
		}

		protected override bool OnFirst()
		{
			if (m_remaining != null && m_remaining.Value < 0)
			{ // empty list ??
				return Completed();
			}

			// even if the caller only wants the first, we will probably need to read more than that...
			var sources = m_sources.ToArray();
			var iterators = new IteratorState[sources.Length];
			try
			{
				// start all the iterators
				for (int i = 0; i < sources.Length;i++)
				{
					var state = new IteratorState
					{
						Active = true,
						Iterator = sources[i].GetEnumerator()
					};
					state.Next = state.Iterator.MoveNext();

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
				try { Cleanup(tmp); } catch { }
				throw;
			}
			finally
			{
				m_iterators = iterators;
			}
		}

		/// <summary>Finds the next smallest item from all the active iterators</summary>
		protected override bool OnNext()
		{
			if (m_remaining is <= 0)
			{
				return Completed();
			}

			int index;
			TSource current;

			do
			{
				// ensure all iterators are ready
				var iterators = m_iterators;
				Contract.Debug.Requires(iterators != null);
				for (int i = 0; i < iterators.Length;i++)
				{
					if (!iterators[i].Active) continue;

					if (!iterators[i].HasCurrent)
					{
						if (!iterators[i].Next)
						{ // this one is done, remove it
							iterators[i].Iterator.Dispose();
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
					return Completed();
				}
			}
			while(index < 0);

			var result = m_resultSelector(current);

			// store the current pair
			if (!Publish(result))
			{ // something happened..
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
			iterators[index].Next = iterators[index].Iterator.MoveNext();
		}

		private static void Cleanup(IteratorState[]? iterators)
		{
			if (iterators != null)
			{
				List<Exception>? errors = null;

				for (int i = 0; i < iterators.Length; i++)
				{
					if (iterators[i].Active && iterators[i].Iterator != null)
					{
						try
						{
							var iterator = iterators[i].Iterator;
							iterators[i] = new IteratorState();
							iterator.Dispose();
						}
						catch (Exception e)
						{
							(errors ??= [ ]).Add(e);
						}
					}
				}

				if (errors != null) throw new AggregateException(errors);
			}
		}

		protected override void Cleanup()
		{
			try
			{
				Cleanup(m_iterators);
			}
			finally
			{
				m_iterators = null;
				m_remaining = 0;
			}
		}

	}

}
