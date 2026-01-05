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

	/// <summary>Expression that evaluate a condition on each item</summary>
	/// <typeparam name="TSource">Type of the filtered elements</typeparam>
	[PublicAPI]
	public sealed class AsyncFilterExpression<TSource>
	{

		private readonly Func<TSource, bool>? m_filter;

		private readonly Func<TSource, CancellationToken, Task<bool>>? m_asyncFilter;

		public AsyncFilterExpression(Func<TSource, bool> filter)
		{
			Contract.NotNull(filter);
			m_filter = filter;
		}

		public AsyncFilterExpression(Func<TSource, CancellationToken, Task<bool>> asyncFilter)
		{
			Contract.NotNull(asyncFilter);
			m_asyncFilter = asyncFilter;
		}

		public bool Async => m_asyncFilter != null;

		public bool Invoke(TSource item)
		{
			if (m_filter == null) throw FailInvalidOperation();
			return m_filter(item);
		}

		public Task<bool> InvokeAsync(TSource item, CancellationToken ct)
		{
			if (m_asyncFilter != null)
			{
				return m_asyncFilter(item, ct);
			}
			else
			{
				Contract.Debug.Requires(m_filter != null);
				return Task.FromResult(m_filter(item));
			}
		}

		[Pure]
		private static InvalidOperationException FailInvalidOperation()
		{
			return new InvalidOperationException("Cannot invoke asynchronous filter synchronously.");
		}

		public AsyncFilterExpression<TSource> AndAlso(AsyncFilterExpression<TSource> expr)
		{
			return AndAlso(this, expr);
		}

		public AsyncFilterExpression<TSource> OrElse(AsyncFilterExpression<TSource> expr)
		{
			return OrElse(this, expr);
		}

		public static AsyncFilterExpression<TSource> AndAlso(AsyncFilterExpression<TSource> left, AsyncFilterExpression<TSource> right)
		{
			Contract.NotNull(left);
			Contract.NotNull(right);

			// combine two expressions into a logical AND expression.
			// Note: if the first expression returns false, the second one will NOT be evaluated

			if (left.m_filter != null)
			{ // we are async
				var f = left.m_filter;
				if (right.m_filter != null)
				{ // so is the next one
					var g = right.m_filter;
					return new AsyncFilterExpression<TSource>((x) => f(x) && g(x));
				}
				else
				{ // next one is async
					var g = right.m_asyncFilter!;
					return new AsyncFilterExpression<TSource>((x, ct) => f(x) ? g(x, ct) : Task.FromResult(false));
				}
			}
			else
			{ // we are async
				var f = left.m_asyncFilter;
				Contract.Debug.Assert(f != null);
				if (right.m_asyncFilter != null)
				{ // so is the next one
					var g = right.m_asyncFilter;
					Contract.Debug.Assert(g != null);
					return new AsyncFilterExpression<TSource>(async (x, ct) => (await f(x, ct).ConfigureAwait(false)) && (await g(x, ct).ConfigureAwait(false)));
				}
				else
				{
					var g = right.m_filter;
					Contract.Debug.Assert(g != null);
					return new AsyncFilterExpression<TSource>(async (x, ct) => (await f(x, ct).ConfigureAwait(false)) && g(x));
				}
			}
		}

		public static AsyncFilterExpression<TSource> OrElse(AsyncFilterExpression<TSource> left, AsyncFilterExpression<TSource> right)
		{
			Contract.NotNull(left);
			Contract.NotNull(right);

			// combine two expressions into a logical OR expression.
			// Note: if the first expression returns true, the second one will NOT be evaluated

			if (left.m_filter != null)
			{ // we are async
				var f = left.m_filter;
				Contract.Debug.Assert(f != null);
				if (right.m_filter != null)
				{ // so is the next one
					var g = right.m_filter;
					Contract.Debug.Assert(g != null);
					return new AsyncFilterExpression<TSource>((x) => f(x) || g(x));
				}
				else
				{ // next one is async
					var g = right.m_asyncFilter;
					Contract.Debug.Assert(g != null);
					return new AsyncFilterExpression<TSource>((x, ct) => f(x) ? Task.FromResult(true) : g(x, ct));
				}
			}
			else
			{ // we are async
				var f = left.m_asyncFilter;
				Contract.Debug.Assert(f != null);
				if (right.m_asyncFilter != null)
				{ // so is the next one
					var g = left.m_asyncFilter;
					Contract.Debug.Assert(g != null);
					return new AsyncFilterExpression<TSource>(async (x, ct) => (await f(x, ct).ConfigureAwait(false)) || (await g(x, ct).ConfigureAwait(false)));
				}
				else
				{
					var g = left.m_filter;
					Contract.Debug.Assert(g != null);
					return new AsyncFilterExpression<TSource>(async (x, ct) => (await f(x, ct).ConfigureAwait(false)) || g(x));
				}
			}
		}

	}

}
