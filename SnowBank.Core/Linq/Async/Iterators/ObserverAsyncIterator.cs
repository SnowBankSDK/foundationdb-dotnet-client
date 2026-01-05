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
	using SnowBank.Linq.Async.Expressions;

	/// <summary>Observe the items of an async sequence</summary>
	/// <typeparam name="TSource">Type of the observed elements</typeparam>
	public sealed class ObserverAsyncIterator<TSource> : AsyncFilterIterator<TSource, TSource>
	{

		private readonly AsyncObserverExpression<TSource> m_observer;

		public ObserverAsyncIterator(IAsyncQuery<TSource> source, AsyncObserverExpression<TSource> observer)
			: base(source)
		{
			Contract.Debug.Requires(observer != null);
			m_observer = observer;
		}

		protected override AsyncLinqIterator<TSource> Clone()
		{
			return new ObserverAsyncIterator<TSource>(m_source, m_observer);
		}

		protected override async ValueTask<bool> OnNextAsync()
		{
			var iterator = m_iterator;
			Contract.Debug.Requires(iterator != null);

			var ct = this.Cancellation;
			while (!ct.IsCancellationRequested)
			{
				if (!await iterator.MoveNextAsync().ConfigureAwait(false))
				{ // completed
					return await Completed().ConfigureAwait(false);
				}

				if (ct.IsCancellationRequested) break;

				TSource current = iterator.Current;
				if (!m_observer.Async)
				{
					// ReSharper disable once MethodHasAsyncOverloadWithCancellation
					m_observer.Invoke(current);
				}
				else
				{
					await m_observer.InvokeAsync(current, ct).ConfigureAwait(false);
				}

				return Publish(current);
			}

			return await Canceled().ConfigureAwait(false);
		}

	}

}
