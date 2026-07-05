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

namespace SnowBank.Linq
{
	using Async.Iterators;

	public static partial class AsyncQuery
	{

		/// <summary>Returns an async sequence with a single element, which is a constant</summary>
		[Pure]
		public static IAsyncLinqQuery<T> Singleton<T>(T value, CancellationToken ct = default)
		{
			return new SingletonIterator<T>(() => value, ct);
		}

		/// <summary>Returns an async sequence which will produce a single element, using the specified lambda</summary>
		/// <param name="selector">Lambda that will be called once per iteration, to produce the single element of this sequence</param>
		/// <param name="ct">Token used to cancel the execution of this query</param>
		/// <remarks>If the sequence is iterated multiple times, then <paramref name="selector"/> will be called once for each iteration.</remarks>
		[Pure, LinqTunnel]
		[OverloadResolutionPriority(1)]
		public static IAsyncLinqQuery<T> Singleton<T>(Func<T> selector, CancellationToken ct = default)
		{
			Contract.NotNull(selector);
			return new SingletonIterator<T>(selector, ct);
		}

		/// <summary>Returns an async sequence which will produce a single element, using the specified lambda</summary>
		/// <param name="selector">Lambda that will be called once per iteration, to produce the single element of this sequence</param>
		/// <param name="ct">Token used to cancel the execution of this query</param>
		/// <remarks>If the sequence is iterated multiple times, then <paramref name="selector"/> will be called once for each iteration.</remarks>
		[Pure, LinqTunnel]
		[OverloadResolutionPriority(1)]
		public static IAsyncLinqQuery<T> Singleton<T>(Func<CancellationToken, Task<T>> selector, CancellationToken ct = default)
		{
			Contract.NotNull(selector);
			return new SingletonIterator<T>(selector, ct);
		}

		/// <summary>Returns an async sequence which will produce a single element, using the specified lambda</summary>
		/// <param name="selector">Lambda that will be called once per iteration, to produce the single element of this sequence</param>
		/// <param name="ct">Token used to cancel the execution of this query</param>
		/// <remarks>If the sequence is iterated multiple times, then <paramref name="selector"/> will be called once for each iteration.</remarks>
		[Pure, LinqTunnel]
		[OverloadResolutionPriority(2)]
		public static IAsyncLinqQuery<T> Singleton<T>(Func<Task<T>> selector, CancellationToken ct = default)
		{
			Contract.NotNull(selector);
			return new SingletonIterator<T>(selector, ct);
		}


		/// <summary>A query that will return only a single element</summary>
		internal sealed class SingletonIterator<TElement> : AsyncLinqIterator<TElement>
		{

			private Delegate? Factory { get; }

			private bool Called { get; set; }

			private SingletonIterator(Delegate lambda, CancellationToken ct)
			{
				Contract.Debug.Requires(lambda != null);
				this.Factory = lambda;
				this.Cancellation = ct;
			}

			public SingletonIterator(Func<TElement> lambda, CancellationToken ct)
				: this((Delegate) lambda, ct)
			{ }

			public SingletonIterator(Func<Task<TElement>> lambda, CancellationToken ct)
				: this((Delegate) lambda, ct)
			{ }

			public SingletonIterator(Func<ValueTask<TElement>> lambda, CancellationToken ct)
				: this((Delegate) lambda, ct)
			{ }

			public SingletonIterator(Func<CancellationToken, Task<TElement>> lambda, CancellationToken ct)
				: this((Delegate) lambda, ct)
			{ }

			public SingletonIterator(Func<CancellationToken, ValueTask<TElement>> lambda, CancellationToken ct)
				: this((Delegate) lambda, ct)
			{ }

			public override CancellationToken Cancellation { get; }

			/// <inheritdoc />
			protected override AsyncLinqIterator<TElement> Clone() => new SingletonIterator<TElement>(this.Factory!, this.Cancellation);

			/// <inheritdoc />
			protected override ValueTask<bool> OnFirstAsync()
			{
				if (this.Factory == null)
				{
					return new(false);
				}

				this.Called = false;
				return new(true);
			}

			/// <inheritdoc />
			protected override async ValueTask<bool> OnNextAsync()
			{
				var factory = this.Factory;
				if (factory == null || this.Called)
				{
					return await this.Completed().ConfigureAwait(false);
				}

				this.Called = true;
				var current = factory switch
				{
					Func<TElement> fn => fn(),
					Func<Task<TElement>> fn => await fn().ConfigureAwait(false),
					Func<CancellationToken, Task<TElement>> fn => await fn(this.Cancellation).ConfigureAwait(false),
					Func<ValueTask<TElement>> fn => await fn().ConfigureAwait(false),
					Func<CancellationToken, ValueTask<TElement>> fn => await fn(this.Cancellation).ConfigureAwait(false),
					_ => throw new InvalidOperationException("Unsupported delegate type")
				};

				return Publish(current);
			}

			/// <inheritdoc />
			protected override ValueTask Cleanup()
			{
				return default;
			}

		}

	}

}
