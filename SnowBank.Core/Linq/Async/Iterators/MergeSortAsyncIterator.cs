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

	/// <summary>Merge all the elements of several ordered queries into one single async sequence</summary>
	/// <typeparam name="TSource">Type of the elements from the source async sequences</typeparam>
	/// <typeparam name="TKey">Type of the keys extracted from the source elements</typeparam>
	/// <typeparam name="TResult">Type of the elements of resulting async sequence</typeparam>
	public sealed class MergeSortAsyncIterator<TSource, TKey, TResult> : MergeAsyncIterator<TSource, TKey, TResult>
	{

		public MergeSortAsyncIterator(IEnumerable<IAsyncQuery<TSource>> sources, int? limit, Func<TSource, TKey> keySelector, Func<TSource, TResult> resultSelector, IComparer<TKey>? comparer, CancellationToken ct)
			: base(sources, limit, keySelector, resultSelector, comparer, ct)
		{ }

		protected override AsyncLinqIterator<TResult> Clone()
		{
			return new MergeSortAsyncIterator<TSource, TKey, TResult>(m_sources, m_limit, m_keySelector, m_resultSelector, m_keyComparer, this.Cancellation);
		}

		protected override bool FindNext(out int index, out TSource current)
		{
			index = -1;
			current = default!;
			TKey min = default!;

			var iterators = m_iterators;
			var keyComparer = m_keyComparer;
			Contract.Debug.Requires(iterators != null);

			for (int i = 0; i < iterators.Length; i++)
			{
				if (!iterators[i].Active) continue;

				if (index == -1 || keyComparer.Compare(iterators[i].Current, min) < 0)
				{
					min = iterators[i].Current;
					index = i;
				}
			}

			if (index >= 0)
			{
				current = iterators[index].Iterator.Current;
				if (m_remaining == null || m_remaining.Value > 1)
				{ // start getting the next value on this iterator
					AdvanceIterator(index);
				}
			}

			return index != -1;
		}

		/// <summary>Apply a transformation on the results of the merge sort</summary>
		public override AsyncLinqIterator<TNew> Select<TNew>(Func<TResult, TNew> selector)
		{
			return new MergeSortAsyncIterator<TSource, TKey, TNew>(
				m_sources,
				m_limit,
				m_keySelector,
				(kvp) => selector(m_resultSelector(kvp)),
				m_keyComparer,
				this.Cancellation
			);
		}

		/// <summary>Limit the number of elements returned by the MergeSort</summary>
		/// <param name="limit">Maximum number of results to return</param>
		/// <returns>New MergeSort that will only return the specified number of results</returns>
		public override AsyncLinqIterator<TResult> Take(int limit)
		{
			if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit), "Value cannot be less than zero");

			if (m_limit != null && m_limit < limit) return this;

			return new MergeSortAsyncIterator<TSource, TKey, TResult>(
				m_sources,
				limit,
				m_keySelector,
				m_resultSelector,
				m_keyComparer,
				this.Cancellation
			);
		}

	}

}
