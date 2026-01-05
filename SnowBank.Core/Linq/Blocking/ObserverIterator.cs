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

	/// <summary>Observe the items of a sequence</summary>
	/// <typeparam name="TSource">Type of the observed elements</typeparam>
	public sealed class ObserverIterator<TSource> : FilterIterator<TSource, TSource>
	{

		private readonly Action<TSource> m_observer;

		public ObserverIterator(IEnumerable<TSource> source, Action<TSource> observer)
			: base(source)
		{
			Contract.NotNull(observer);
			m_observer = observer;
		}

		protected override Iterator<TSource> Clone()
		{
			return new ObserverIterator<TSource>(m_source, m_observer);
		}

		protected override bool OnNext()
		{
			var iterator = m_iterator;
			if (iterator == null) throw ThrowHelper.ObjectDisposedException(this);

			if (!iterator.MoveNext())
			{ // completed
				return Completed();
			}

			TSource current = iterator.Current;
			m_observer(current);

			return Publish(current);
		}

	}

}
