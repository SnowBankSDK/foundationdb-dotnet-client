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

namespace SnowBank.Linq.Async.Expressions
{

	/// <summary>Expression that execute an action on each item, but does not change the source expression in any way</summary>
	/// <typeparam name="TSource">Type of observed items</typeparam>
	[PublicAPI]
	public sealed class AsyncObserverExpression<TSource>
	{

		private readonly Action<TSource>? m_handler;

		private readonly Func<TSource, CancellationToken, Task>? m_asyncHandler;

		public AsyncObserverExpression(Action<TSource> handler)
		{
			Contract.NotNull(handler);
			m_handler = handler;
		}

		public AsyncObserverExpression(Func<TSource, CancellationToken, Task> asyncHandler)
		{
			Contract.NotNull(asyncHandler);
			m_asyncHandler = asyncHandler;
		}

		public bool Async => m_handler != null;

		public TSource Invoke(TSource item)
		{
			if (m_handler == null) throw FailInvalidOperation();
			m_handler(item);
			return item;
		}

		public async Task<TSource> InvokeAsync(TSource item, CancellationToken ct)
		{
			if (m_asyncHandler != null)
			{
				await m_asyncHandler(item, ct).ConfigureAwait(false);
			}
			else
			{
				Contract.Debug.Assert(m_handler != null);
				m_handler(item);
			}

			return item;
		}

		[Pure]
		private static InvalidOperationException FailInvalidOperation()
		{
			return new InvalidOperationException("Cannot invoke asynchronous observer synchronously");
		}

		public AsyncObserverExpression<TSource> Then(AsyncObserverExpression<TSource> expr)
		{
			return Then(this, expr);
		}

		public static AsyncObserverExpression<TSource> Then(AsyncObserverExpression<TSource> left, AsyncObserverExpression<TSource> right)
		{
			Contract.NotNull(left);
			Contract.NotNull(right);

			if (left.m_handler != null)
			{
				var f = left.m_handler;
				Contract.Debug.Requires(f != null);
				if (right.m_handler != null)
				{
					var g = right.m_handler;
					Contract.Debug.Requires(g != null);
					return new AsyncObserverExpression<TSource>((x) => { f(x); g(x); });
				}
				else
				{
					var g = right.m_asyncHandler;
					Contract.Debug.Requires(g != null);
					return new AsyncObserverExpression<TSource>((x, ct) => { f(x); return g(x, ct); });
				}
			}
			else
			{
				var f = left.m_asyncHandler;
				Contract.Debug.Requires(f != null);
				if (right.m_asyncHandler != null)
				{
					var g = right.m_asyncHandler;
					Contract.Debug.Requires(g != null);
					return new AsyncObserverExpression<TSource>(async (x, ct) => { await f(x, ct).ConfigureAwait(false); await g(x, ct).ConfigureAwait(false); });
				}
				else
				{
					var g = right.m_handler;
					Contract.Debug.Requires(g != null);
					return new AsyncObserverExpression<TSource>(async (x, ct) => { await f(x, ct).ConfigureAwait(false); g(x); });
				}
			}
		}

	}

}
