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

	/// <summary>Returns only the values for the keys that are in all the sub queries</summary>
	/// <typeparam name="TSource">Type of the elements from the source async sequences</typeparam>
	/// <typeparam name="TKey">Type of the keys extracted from the source elements</typeparam>
	/// <typeparam name="TResult">Type of the elements of resulting async sequence</typeparam>
	public sealed class IntersectAsyncIterator<TSource, TKey, TResult> : MergeAsyncIterator<TSource, TKey, TResult>
	{
		public IntersectAsyncIterator(IEnumerable<IAsyncQuery<TSource>> sources, int? limit, Func<TSource, TKey> keySelector, Func<TSource, TResult> resultSelector, IComparer<TKey>? comparer, CancellationToken ct)
			: base(sources, limit, keySelector, resultSelector, comparer, ct)
		{ }

		protected override AsyncLinqIterator<TResult> Clone()
		{
			return new IntersectAsyncIterator<TSource, TKey, TResult>(m_sources, m_limit, m_keySelector, m_resultSelector, m_keyComparer, this.Cancellation);
		}

		protected override bool FindNext(out int index, out TSource current)
		{
			index = -1;
			current = default!;

			// we only return a value if all are equal
			// if not, find the current max, and advance all iterators that are lower

			TKey min = default!;
			TKey max = default!;
			var iterators = m_iterators;
			var keyComparer = m_keyComparer;
			Contract.Debug.Requires(iterators != null && keyComparer != null);

			for (int i = 0; i < iterators.Length; i++)
			{
				if (!iterators[i].Active)
				{ // all must be still active!
					return false;
				}

				TKey key = iterators[i].Current;
				if (index == -1)
				{
					min = max = key;
					index = i;
				}
				else
				{
					if (keyComparer.Compare(key, min) < 0) { min = key; }
					if (keyComparer.Compare(key, max) > 0) { max = key; index = i; }
				}
			}

			if (index == -1)
			{ // no values ?
				return false;
			}

			if (keyComparer.Compare(min, max) == 0)
			{ // all equal !
				// return the value of the first max encountered
				current = iterators[index].Iterator.Current;

				// advance everyone !
				for (int i = 0; i < iterators.Length;i++)
				{
					if (iterators[i].Active) AdvanceIterator(i);
				}
				return true;
			}

			// advance all the values that are lower than the max
			for (int i = 0; i < iterators.Length; i++)
			{
				if (iterators[i].Active && keyComparer.Compare(iterators[i].Current, max) < 0)
				{
					AdvanceIterator(i);
				}
			}

			// keep searching
			index = -1;
			return true;
		}

		/// <summary>Apply a transformation on the results of the intersection</summary>
		public override AsyncLinqIterator<TNew> Select<TNew>(Func<TResult, TNew> selector)
		{
			return new IntersectAsyncIterator<TSource, TKey, TNew>(
				m_sources,
				m_limit,
				m_keySelector,
				(kvp) => selector(m_resultSelector(kvp)),
				m_keyComparer,
				this.Cancellation
			);
		}

		/// <summary>Limit the number of elements returned by the intersection</summary>
		/// <param name="limit">Maximum number of results to return</param>
		/// <returns>New Intersect that will only return the specified number of results</returns>
		public override AsyncLinqIterator<TResult> Take(int limit)
		{
			if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit), "Value cannot be less than zero");

			if (m_limit != null && m_limit < limit) return this;

			return new IntersectAsyncIterator<TSource, TKey, TResult>(
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
