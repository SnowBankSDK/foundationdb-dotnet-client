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

	/// <summary>Generate items asynchronously, using a user-provided lambda</summary>
	/// <typeparam name="TOutput">Type of the items produced by this generator</typeparam>
	public class AnonymousAsyncGenerator<TOutput> : AsyncLinqIterator<TOutput>
	{
		// use a custom lambda that returns Maybe<TOutput> results, asynchronously
		// => as long as the result has a value, continue iterating
		// => if the result does not have a value, stop iterating
		// => if the lambda fails, or the result represents an error, throw the error down the chain and stop iterating

		// ITERABLE

		private readonly Delegate m_generator;
		// can be either one of:
		// - Func<long, CancellationToken, Task<Maybe<TOutput>>>
		// - Func<long, CancellationToken, ValueTask<Maybe<TOutput>>>

		// ITERATOR

		private long m_index;

		private readonly CancellationToken m_ct;

		public AnonymousAsyncGenerator(Func<long, CancellationToken, Task<Maybe<TOutput>>> generator, CancellationToken ct)
			: this((Delegate) generator, ct)
		{ }

		public AnonymousAsyncGenerator(Func<long, CancellationToken, ValueTask<Maybe<TOutput>>> generator, CancellationToken ct)
			: this((Delegate) generator, ct)
		{ }

		private AnonymousAsyncGenerator(Delegate generator, CancellationToken ct)
		{
			Contract.Debug.Requires(generator != null);
			m_generator = generator;
			m_index = -1;
			m_ct = ct;
		}

		public override CancellationToken Cancellation => m_ct;

		protected override AsyncLinqIterator<TOutput> Clone()
		{
			return new AnonymousAsyncGenerator<TOutput>(m_generator, m_ct);
		}

		protected override ValueTask<bool> OnFirstAsync()
		{
			m_index = 0;
			return new ValueTask<bool>(true);
		}

		protected override async ValueTask<bool> OnNextAsync()
		{
			m_ct.ThrowIfCancellationRequested();
			if (m_index < 0) return false;

			long index = m_index;
			Maybe<TOutput> res;
			if (m_generator is Func<long, CancellationToken, Task<Maybe<TOutput>>> genT)
			{
				res = await genT(index, m_ct).ConfigureAwait(false);
			}
			else if (m_generator is Func<long, CancellationToken, ValueTask<Maybe<TOutput>>> genV)
			{
				res = await genV(index, m_ct).ConfigureAwait(false);
			}
			else
			{
				throw new InvalidOperationException();
			}

			if (res.Failed) res.ThrowForNonSuccess();
			if (res.IsEmpty) return await Completed().ConfigureAwait(false);
			m_index = checked(index + 1);
			return Publish(res.Value);
		}

		protected override ValueTask Cleanup()
		{
			m_index = -1;
			return default;
		}

	}

}
