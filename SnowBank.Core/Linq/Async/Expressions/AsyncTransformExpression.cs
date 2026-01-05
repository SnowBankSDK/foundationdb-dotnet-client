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

	/// <summary>Expression that applies a transformation on each item</summary>
	/// <typeparam name="TSource">Type of the source items</typeparam>
	/// <typeparam name="TResult">Type of the transformed items</typeparam>
	[PublicAPI]
	public sealed class AsyncTransformExpression<TSource, TResult>
	{

		private readonly Func<TSource, TResult>? m_transform;

		private readonly Func<TSource, CancellationToken, Task<TResult>>? m_asyncTransform;

		private static readonly Func<TSource, TSource> IdentityTransform = (x) => x;

		public AsyncTransformExpression()
		{
			if (typeof(TResult) != typeof(TSource)) throw new InvalidOperationException("Can only be called if source and result types are the same!");
			m_transform = (Func<TSource, TResult>) (object) IdentityTransform;
		}

		public AsyncTransformExpression(Func<TSource, TResult> transform)
		{
			Contract.NotNull(transform);
			m_transform = transform;
		}

		public AsyncTransformExpression(Func<TSource, CancellationToken, Task<TResult>> asyncTransform)
		{
			Contract.NotNull(asyncTransform);
			m_asyncTransform = asyncTransform;
		}

		public bool Async => m_asyncTransform != null;

		public bool IsIdentity()
		{
			//note: Identity Function is not async, and is only possible if TSource == TResult, so we can skip checking the types ourselves...
			return m_transform != null && ReferenceEquals(m_transform, IdentityTransform);
		}

		public TResult Invoke(TSource item)
		{
			if (m_transform == null) throw FailInvalidOperation();
			return m_transform(item);
		}

		public Task<TResult> InvokeAsync(TSource item, CancellationToken ct)
		{
			if (m_asyncTransform != null)
			{
				return m_asyncTransform(item, ct);
			}
			else
			{
				Contract.Debug.Requires(m_transform != null);
				return Task.FromResult(m_transform(item));
			}
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException FailInvalidOperation() => new("Cannot invoke asynchronous transform synchronously");

		public AsyncTransformExpression<TSource, TCasted> Cast<TCasted>()
		{
			if (typeof(TCasted) == typeof(TResult))
			{ // we are already of the correct type, we just need to fool the compiler into believing it!
				return (AsyncTransformExpression<TSource, TCasted>)(object)this;
			}
			else
			{
				//note: if TCasted and TResult are not compatible, this will just blow up at execution time.
				if (m_transform != null)
				{
					var f = m_transform;
					return new((x) => (TCasted) (object) f(x)!);
				}
				else
				{
					var f = m_asyncTransform!;
					return new(async (x, ct) => (TCasted) (object) (await f(x, ct).ConfigureAwait(false))!);
				}
			}
		}

		public AsyncTransformExpression<TSource, TOuter> Then<TOuter>(AsyncTransformExpression<TResult, TOuter> expr)
		{
			return Then<TOuter>(this, expr);
		}

		public static AsyncTransformExpression<TSource, TOuter> Then<TOuter>(AsyncTransformExpression<TSource, TResult> left, AsyncTransformExpression<TResult, TOuter> right)
		{
			Contract.NotNull(left);
			Contract.NotNull(right);

			if (left.IsIdentity())
			{ // we can optimize the left expression away, since we know that TSource == TResult !
				//note: fool the compiler into believing that TSource == TResult
				return (AsyncTransformExpression<TSource, TOuter>) (object) right;
			}

			if (right.IsIdentity())
			{ // we can optimize the right expression away, since we know that TResult == TOuter !
				return (AsyncTransformExpression<TSource, TOuter>) (object) left;
			}

			if (left.m_transform != null)
			{
				var f = left.m_transform;
				Contract.Debug.Assert(f != null);
				if (right.m_transform != null)
				{
					var g = right.m_transform;
					Contract.Debug.Assert(g != null);
					return new((x) => g(f(x)));
				}
				else
				{
					var g = right.m_asyncTransform;
					Contract.Debug.Assert(g != null);
					return new((x, ct) => g(f(x), ct));
				}
			}
			else
			{
				var f = left.m_asyncTransform;
				Contract.Debug.Assert(f != null);
				if (right.m_asyncTransform != null)
				{
					var g = right.m_asyncTransform;
					Contract.Debug.Assert(g != null);
					return new(async (x, ct) => await g(await f(x, ct).ConfigureAwait(false), ct).ConfigureAwait(false));
				}
				else
				{
					var g = right.m_transform;
					Contract.Debug.Assert(g != null);
					return new(async (x, ct) => g(await f(x, ct).ConfigureAwait(false)));
				}
			}
		}

	}

}
