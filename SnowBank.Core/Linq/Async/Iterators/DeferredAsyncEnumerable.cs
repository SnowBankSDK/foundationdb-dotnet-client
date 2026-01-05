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
	/// <summary>Iterator that will generate the underlying async sequence "just in time" when it is itself iterated</summary>
	/// <typeparam name="TState">State that is passed to the generator</typeparam>
	/// <typeparam name="TResult">Type of elements of the async sequence</typeparam>
	internal class DeferredAsyncQueryIterator<TState, TResult> : AsyncLinqIterator<TResult>
	{

		public TState State { get; }

		private Delegate Generator { get; }

		private IAsyncEnumerator<TResult>? Inner { get; set; }

		public DeferredAsyncQueryIterator(TState state, Delegate generator, CancellationToken ct)
		{
			Contract.Debug.Requires(generator != null);
			this.State = state;
			this.Generator = generator;
			this.Cancellation = ct;
		}

		public override CancellationToken Cancellation { get; }

		protected override ValueTask Cleanup()
		{
			var inner = this.Inner;
			this.Inner = null;
			return inner?.DisposeAsync() ?? default;
		}

		protected override AsyncLinqIterator<TResult> Clone()
		{
			return new DeferredAsyncQueryIterator<TState, TResult>(this.State, this.Generator, this.Cancellation);
		}

		protected override async ValueTask<bool> OnFirstAsync()
		{
			IAsyncQuery<TResult> sequence;

			switch (this.Generator)
			{
				#region IAsyncQuery<TResult>...

				case Func<IAsyncQuery<TResult>> fn:
				{
					sequence = fn();
					break;
				}
				case Func<TState, IAsyncQuery<TResult>> fn:
				{
					sequence = fn(this.State);
					break;
				}
				case Func<Task<IAsyncQuery<TResult>>> fn:
				{
					sequence = await fn().ConfigureAwait(false);
					break;
				}
				case Func<CancellationToken, Task<IAsyncQuery<TResult>>> fn:
				{
					sequence = await fn(this.Cancellation).ConfigureAwait(false);
					break;
				}
				case Func<TState, Task<IAsyncQuery<TResult>>> fn:
				{
					sequence = await fn(this.State).ConfigureAwait(false);
					break;
				}
				case Func<TState, CancellationToken, Task<IAsyncQuery<TResult>>> fn:
				{
					sequence = await fn(this.State, this.Cancellation).ConfigureAwait(false);
					break;
				}
				case Func<TState, ValueTask<IAsyncQuery<TResult>>> fn:
				{
					sequence = await fn(this.State).ConfigureAwait(false);
					break;
				}
				case Func<TState, CancellationToken, ValueTask<IAsyncQuery<TResult>>> fn:
				{
					sequence = await fn(this.State, this.Cancellation).ConfigureAwait(false);
					break;
				}

				#endregion

				#region IAsyncLinqQuery<TResult>...

				case Func<Task<IAsyncLinqQuery<TResult>>> fn:
				{
					sequence = await fn().ConfigureAwait(false);
					break;
				}
				case Func<CancellationToken, Task<IAsyncLinqQuery<TResult>>> fn:
				{
					sequence = await fn(this.Cancellation).ConfigureAwait(false);
					break;
				}
				case Func<TState, Task<IAsyncLinqQuery<TResult>>> fn:
				{
					sequence = await fn(this.State).ConfigureAwait(false);
					break;
				}
				case Func<TState, CancellationToken, Task<IAsyncLinqQuery<TResult>>> fn:
				{
					sequence = await fn(this.State, this.Cancellation).ConfigureAwait(false);
					break;
				}
				case Func<TState, ValueTask<IAsyncLinqQuery<TResult>>> fn:
				{
					sequence = await fn(this.State).ConfigureAwait(false);
					break;
				}
				case Func<TState, CancellationToken, ValueTask<IAsyncLinqQuery<TResult>>> fn:
				{
					sequence = await fn(this.State, this.Cancellation).ConfigureAwait(false);
					break;
				}

				#endregion

				#region IAsyncEnumerable<TResult>...

				case Func<IAsyncEnumerable<TResult>> fn:
				{
					sequence = fn().ToAsyncQuery(this.Cancellation);
					break;
				}
				case Func<TState, IAsyncEnumerable<TResult>> fn:
				{
					sequence = fn(this.State).ToAsyncQuery(this.Cancellation);
					break;
				}
				case Func<Task<IAsyncEnumerable<TResult>>> fn:
				{
					sequence = (await fn().ConfigureAwait(false)).ToAsyncQuery(this.Cancellation);
					break;
				}
				case Func<TState, Task<IAsyncEnumerable<TResult>>> fn:
				{
					sequence = (await fn(this.State).ConfigureAwait(false)).ToAsyncQuery(this.Cancellation);
					break;
				}
				case Func<TState, CancellationToken, Task<IAsyncEnumerable<TResult>>> fn:
				{
					sequence = (await fn(this.State, this.Cancellation).ConfigureAwait(false)).ToAsyncQuery(this.Cancellation);
					break;
				}
				case Func<TState, ValueTask<IAsyncEnumerable<TResult>>> fn:
				{
					sequence = (await fn(this.State).ConfigureAwait(false)).ToAsyncQuery(this.Cancellation);
					break;
				}
				case Func<TState, CancellationToken, ValueTask<IAsyncEnumerable<TResult>>> fn:
				{
					sequence = (await fn(this.State, this.Cancellation).ConfigureAwait(false)).ToAsyncQuery(this.Cancellation);
					break;
				}

				#endregion

				default:
				{
#if DEBUG
					if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
					throw new InvalidOperationException("Unsupported deferred delegate type");
				}
			}

			if (sequence == null) throw new InvalidOperationException("Deferred generator cannot return a null async sequence.");

			this.Inner = sequence.GetAsyncEnumerator(AsyncIterationHint.Default); //TODO: configurable hint?
			Contract.Debug.Assert(this.Inner != null);

			return true;
		}

		protected override async ValueTask<bool> OnNextAsync()
		{
			var inner = this.Inner;
			if (inner == null) throw ThrowHelper.ObjectDisposedException(this);

			if (!(await inner.MoveNextAsync().ConfigureAwait(false)))
			{
				return await Completed().ConfigureAwait(false);
			}
			return Publish(inner.Current);
		}

	}

}
