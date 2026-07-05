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
	using System.ComponentModel;
	using System.Threading.Channels;
	using SnowBank.Linq.Async;
	using SnowBank.Linq.Async.Expressions;
	using SnowBank.Linq.Async.Iterators;

	public static partial class AsyncQuery
	{

		#region Create...

		/// <summary>Create a new async query that will transform an inner async query</summary>
		/// <typeparam name="TSource">Type of elements of the inner async query</typeparam>
		/// <typeparam name="TResult">Type of elements of the outer async query</typeparam>
		/// <param name="source">Source async query that will be wrapped</param>
		/// <param name="factory">Factory method called when the outer sequence starts iterating. Must return an async enumerator</param>
		/// <returns>New async query</returns>
		internal static AsyncSequence<TSource, TResult> Create<TSource, TResult>(
			IAsyncQuery<TSource> source,
			Func<IAsyncEnumerator<TSource>,
			IAsyncEnumerator<TResult>> factory)
		{
			return new(source, factory);
		}

		/// <summary>Create a new async query from a factory method</summary>
		/// <typeparam name="TResult">Type of elements of the outer async query</typeparam>
		/// <param name="factory">Factory method called when the query starts iterating. Must return an async enumerator</param>
		/// <param name="state">Caller-provided state that will be passed to the factory method</param>
		/// <param name="ct">Cancellation token for this query</param>
		/// <returns>New async query</returns>
		public static IAsyncQuery<TResult> Create<TResult>(
			Func<object?, AsyncIterationHint, CancellationToken, IAsyncEnumerator<TResult>> factory,
			object? state,
			CancellationToken ct)
		{
			return new AnonymousIterable<TResult>(factory, state, ct);
		}

		internal sealed class AnonymousIterable<T> : IAsyncQuery<T>, IAsyncEnumerable<T>
		{

			private readonly Func<object?, AsyncIterationHint, CancellationToken, IAsyncEnumerator<T>> m_factory;
			private readonly object? m_state;
			private readonly CancellationToken m_ct;

			public AnonymousIterable(Func<object?, AsyncIterationHint, CancellationToken, IAsyncEnumerator<T>> factory, object? state, CancellationToken ct)
			{
				Contract.Debug.Requires(factory != null);
				m_factory = factory;
				m_state = state;
				m_ct = ct;
			}

			public CancellationToken Cancellation => m_ct;

			[MustDisposeResource]
			[EditorBrowsable(EditorBrowsableState.Never)]
			public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct)
			{
				// We assume that the most frequent caller of this "alternate" entry point will be "await foreach" which wants to scan everything,
				// and that "regular" LINQ usage will go through the IAsyncLinqQuery<T> interface, which calls the GetAsyncEnumerator(AsyncIterationHint) overload.
				return GetCancellableAsyncEnumerator(this, AsyncIterationHint.All, ct);
			}

			[MustDisposeResource]
			public IAsyncEnumerator<T> GetAsyncEnumerator(AsyncIterationHint hint)
			{
				m_ct.ThrowIfCancellationRequested();
				return m_factory(m_state, hint, m_ct);
			}
		}

		/// <summary>Creates a new async query from a factory that will be invoked on the first iteration</summary>
		public static IAsyncLinqQuery<TResult> Defer<TResult>(Func<IAsyncQuery<TResult>> factory, CancellationToken ct)
		{
			Contract.NotNull(factory);
			return new DeferredAsyncQueryIterator<object?, TResult>(null, factory, ct);
		}

		/// <summary>Creates a new async query from a factory that will be invoked on the first iteration</summary>
		public static IAsyncLinqQuery<TResult> Defer<TResult>(Func<Task<IAsyncQuery<TResult>>> factory, CancellationToken ct)
		{
			Contract.NotNull(factory);
			return new DeferredAsyncQueryIterator<object?, TResult>(null, factory, ct);
		}

		/// <summary>Creates a new async query from a factory that will be invoked on the first iteration</summary>
		public static IAsyncLinqQuery<TResult> Defer<TState, TResult>(TState state, Func<TState, IAsyncQuery<TResult>> factory, CancellationToken ct)
		{
			Contract.NotNull(factory);
			return new DeferredAsyncQueryIterator<TState, TResult>(state, factory, ct);
		}

		/// <summary>Creates a new async query from a factory that will be invoked on the first iteration</summary>
		public static IAsyncLinqQuery<TResult> Defer<TState, TResult>(TState state, Func<TState, Task<IAsyncQuery<TResult>>> factory, CancellationToken ct)
		{
			Contract.NotNull(factory);
			return new DeferredAsyncQueryIterator<TState, TResult>(state, factory, ct);
		}

		/// <summary>Creates a new async query from a factory that will be invoked on the first iteration</summary>
		public static IAsyncLinqQuery<TResult> Defer<TState, TResult>(TState state, Func<TState, CancellationToken, Task<IAsyncQuery<TResult>>> factory, CancellationToken ct)
		{
			Contract.NotNull(factory);
			return new DeferredAsyncQueryIterator<TState, TResult>(state, factory, ct);
		}

		/// <summary>Creates a new async query from a factory that will be invoked on the first iteration</summary>
		public static IAsyncLinqQuery<TResult> Defer<TState, TResult>(TState state, Func<TState, Task<IAsyncEnumerable<TResult>>> factory, CancellationToken ct)
		{
			Contract.NotNull(factory);
			return new DeferredAsyncQueryIterator<TState, TResult>(state, factory, ct);
		}

		/// <summary>Creates a new async query from a factory that will be invoked on the first iteration</summary>
		public static IAsyncLinqQuery<TResult> Defer<TState, TResult>(TState state, Func<TState, CancellationToken, Task<IAsyncEnumerable<TResult>>> factory, CancellationToken ct)
		{
			Contract.NotNull(factory);
			return new DeferredAsyncQueryIterator<TState, TResult>(state, factory, ct);
		}

		/// <summary>Creates a new async query from a factory that will be invoked on the first iteration</summary>
		public static IAsyncLinqQuery<TResult> Defer<TResult>(Func<CancellationToken, Task<IAsyncQuery<TResult>>> factory, CancellationToken ct)
		{
			Contract.NotNull(factory);
			return new DeferredAsyncQueryIterator<object?, TResult>(null, factory, ct);
		}

		/// <summary>Creates a new async query from a factory that will be invoked on the first iteration</summary>
		public static IAsyncLinqQuery<TResult> Defer<TResult>(Func<CancellationToken, Task<IAsyncLinqQuery<TResult>>> factory, CancellationToken ct)
		{
			Contract.NotNull(factory);
			return new DeferredAsyncQueryIterator<object?, TResult>(null, factory, ct);
		}

		/// <summary>Creates a new async query from a factory that will be invoked on the first iteration</summary>
		public static IAsyncLinqQuery<TResult> Defer<TState, TResult>(TState state, Func<TState, CancellationToken, Task<IAsyncLinqQuery<TResult>>> factory, CancellationToken ct)
		{
			Contract.NotNull(factory);
			return new DeferredAsyncQueryIterator<TState, TResult>(state, factory, ct);
		}

		/// <summary>Creates a new async query from a factory that will be invoked on the first iteration</summary>
		public static IAsyncLinqQuery<TResult> Defer<TResult>(Func<CancellationToken, Task<IAsyncEnumerable<TResult>>> factory, CancellationToken ct)
		{
			Contract.NotNull(factory);
			return new DeferredAsyncQueryIterator<object?, TResult>(null, factory, ct);
		}

		#endregion

		#region Channel...

		public static async IAsyncEnumerable<TResult> Pump<TResult>(Func<ChannelWriter<TResult>, Task> handler, [EnumeratorCancellation] CancellationToken ct)
		{
			var channel = Channel.CreateUnbounded<TResult>(new() { SingleReader = true, SingleWriter = true });
			try
			{
#if NET8_0_OR_GREATER
				await using var ctr = ct.Register((c) => ((Channel<TResult>) c!).Writer.TryComplete(new OperationCanceledException(ct)), channel);
#else
				using var ctr = ct.Register((c) => ((Channel<TResult>) c!).Writer.TryComplete(new OperationCanceledException(ct)), channel); // CancellationTokenRegistration has no DisposeAsync on this target (sync Dispose does not wait for an in-flight callback)
#endif

				var t = Task.Run(async () =>
				{
					try
					{
						ct.ThrowIfCancellationRequested();
						await handler(channel.Writer).ConfigureAwait(false);
						channel.Writer.TryComplete();
					}
					catch (Exception ex)
					{
						channel.Writer.TryComplete(ex);
					}
				}, ct);

#if NET8_0_OR_GREATER
				await foreach (var r in channel.Reader.ReadAllAsync().WithCancellation(ct).ConfigureAwait(false))
				{
					yield return r;
				}
#else
				// ChannelReader.ReadAllAsync is not exposed on this target: same drain loop, written out by hand
				while (await channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
				{
					while (channel.Reader.TryRead(out var r))
					{
						yield return r;
					}
				}
#endif

				await t.ConfigureAwait(false);
			}
			finally
			{
				channel.Writer.TryComplete();
			}

		}

		#endregion

		#region Helpers...

		internal static SelectManyExpressionAsyncIterator<TSource, TResult> Flatten<TSource, TResult>(
			IAsyncQuery<TSource> source,
			AsyncTransformExpression<TSource, IEnumerable<TResult>> selector)
		{
			return new(source, selector);
		}

		internal static SelectManyExpressionAsyncIterator<TSource, TCollection, TResult> Flatten<TSource, TCollection, TResult>(
			IAsyncQuery<TSource> source,
			AsyncTransformExpression<TSource, IEnumerable<TCollection>> collectionSelector,
			Func<TSource, TCollection, TResult> resultSelector)
		{
			return new(source, collectionSelector, resultSelector);
		}

		internal static SelectManyExpressionAsyncEnumerableIterator<TSource, TResult> Flatten<TSource, TResult>(
			IAsyncQuery<TSource> source,
			AsyncTransformExpression<TSource, IAsyncEnumerable<TResult>> selector)
		{
			return new(source, selector);
		}

		internal static SelectManyExpressionAsyncQueryIterator<TSource, TResult> Flatten<TSource, TResult>(
			IAsyncQuery<TSource> source,
			AsyncTransformExpression<TSource, IAsyncQuery<TResult>> selector)
		{
			return new(source, selector);
		}

		internal static WhereSelectExpressionAsyncIterator<TSource, TResult> Map<TSource, TResult>(
			IAsyncQuery<TSource> source,
			AsyncTransformExpression<TSource, TResult> selector,
			int? limit = null, int?
			offset = null)
		{
			return new(source, filter: null, transform: selector, limit: limit, offset: offset);
		}

		internal static WhereExpressionAsyncIterator<TResult> Filter<TResult>(
			IAsyncQuery<TResult> source,
			AsyncFilterExpression<TResult> filter)
		{
			return new(source, filter);
		}

		#endregion

		#region Run...

		/// <summary>Immediately executes an action on each element of an async query</summary>
		/// <typeparam name="TSource">Type of elements of the async query</typeparam>
		/// <param name="source">Source async query</param>
		/// <param name="mode">If different from default, can be used to optimise the way the source will produce the items</param>
		/// <param name="action">Action to perform on each element as it arrives</param>
		/// <returns>Number of items that have been processed</returns>
		internal static async Task Run<TSource>(
			IAsyncQuery<TSource> source,
			AsyncIterationHint mode,
			[InstantHandle] Action<TSource> action)
		{
			Contract.NotNull(source);
			Contract.NotNull(action);

			await using (var iterator = source.GetAsyncEnumerator(mode))
			{
				Contract.Debug.Assert(iterator != null, "The underlying sequence returned a null async iterator");

				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					action(iterator.Current);
				}
			}
		}

		/// <summary>Immediately executes an action on each element of an async query</summary>
		/// <typeparam name="TState">Type of the state</typeparam>
		/// <typeparam name="TSource">Type of elements of the async query</typeparam>
		/// <param name="source">Source async query</param>
		/// <param name="mode">If different from default, can be used to optimise the way the source will produce the items</param>
		/// <param name="state">Value that will be passed as the first parameter to <paramref name="action"/></param>
		/// <param name="action">Action to perform on each element as it arrives</param>
		/// <returns>Number of items that have been processed</returns>
		internal static async Task Run<TState, TSource>(
			IAsyncQuery<TSource> source,
			AsyncIterationHint mode,
			TState state,
			[InstantHandle] Action<TState, TSource> action)
		{
			Contract.NotNull(source);
			Contract.NotNull(action);

			await using (var iterator = source.GetAsyncEnumerator(mode))
			{
				Contract.Debug.Assert(iterator != null, "The underlying sequence returned a null async iterator");

				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					action(state, iterator.Current);
				}
			}
		}

		/// <summary>Immediately computes an aggregate on each element returned by an async query</summary>
		/// <typeparam name="TSource">Type of elements of the async query</typeparam>
		/// <typeparam name="TAggregate">Type of the aggregate that is computed</typeparam>
		/// <param name="source">Source async query</param>
		/// <param name="mode">If different from default, can be used to optimise the way the source will produce the items</param>
		/// <param name="seed">Initial value for the aggregate that is passed as the first argument to <paramref name="action"/></param>
		/// <param name="action">Function that is called for each element as it arrives. The result will be passed back to the next function call for the following element.</param>
		/// <returns>Value returned by the last call of <paramref name="action"/>, or <paramref name="seed"/> if the query returned no elements.</returns>
		internal static async Task<TAggregate> Run<TAggregate, TSource>(
			IAsyncQuery<TSource> source,
			AsyncIterationHint mode,
			TAggregate seed,
			[InstantHandle] Func<TAggregate, TSource, TAggregate> action)
		{
			Contract.NotNull(source);
			Contract.NotNull(action);

			await using (var iterator = source.GetAsyncEnumerator(mode))
			{
				Contract.Debug.Assert(iterator != null, "The underlying sequence returned a null async iterator");

				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					seed = action(seed, iterator.Current);
				}
			}
			return seed;
		}

		/// <summary>Immediately computes an aggregate on each element returned by an async query</summary>
		/// <typeparam name="TState">Type of the state passed to the handler</typeparam>
		/// <typeparam name="TAggregate">Type of the aggregate that is computed</typeparam>
		/// <typeparam name="TSource">Type of elements of the async query</typeparam>
		/// <param name="source">Source async query</param>
		/// <param name="mode">If different from default, can be used to optimise the way the source will produce the items</param>
		/// <param name="state">State that is passed as the first argument to <paramref name="action"/></param>
		/// <param name="seed">Initial value for the aggregate that is passed as the second argument to <paramref name="action"/></param>
		/// <param name="action">Function that is called for each element as it arrives. The result will be passed back to the next function call for the following element.</param>
		/// <returns>Value returned by the last call of <paramref name="action"/>, or <paramref name="seed"/> if the query returned no elements.</returns>
		internal static async Task<TAggregate> Run<TState, TAggregate, TSource>(
			IAsyncQuery<TSource> source,
			AsyncIterationHint mode,
			TState state,
			TAggregate seed,
			[InstantHandle] Func<TState, TAggregate, TSource, TAggregate> action)
		{
			Contract.NotNull(source);
			Contract.NotNull(action);

			await using (var iterator = source.GetAsyncEnumerator(mode))
			{
				Contract.Debug.Assert(iterator != null, "The underlying sequence returned a null async iterator");

				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					seed = action(state, seed, iterator.Current);
				}
			}
			return seed;
		}


		/// <summary>Immediately executes an action on each element of an async query, with the possibility of stopping before the end</summary>
		/// <typeparam name="TSource">Type of elements of the async query</typeparam>
		/// <param name="source">Source async query</param>
		/// <param name="mode">If different from default, can be used to optimise the way the source will produce the items</param>
		/// <param name="action">Lambda called for each element as it arrives. If the return value is true, the next value will be processed. If the return value is false, the iterations will stop immediately.</param>
		/// <returns>Number of items that have been processed successfully</returns>
		internal static async Task Run<TSource>(
			IAsyncQuery<TSource> source,
			AsyncIterationHint mode,
			Func<TSource, bool> action)
		{
			Contract.NotNull(source);
			Contract.NotNull(action);

			await using (var iterator = source.GetAsyncEnumerator(mode))
			{
				Contract.Debug.Assert(iterator != null, "The underlying sequence returned a null async iterator");

				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					if (!action(iterator.Current))
					{
						break;
					}
				}
			}
		}

		/// <summary>Immediately execute an async action on each element of an async query</summary>
		/// <typeparam name="TSource">Type of elements of the async query</typeparam>
		/// <param name="source">Source async query</param>
		/// <param name="mode">Expected execution mode of the query</param>
		/// <param name="action">Asynchronous action to perform on each element as it arrives</param>
		/// <returns>Number of items that have been processed</returns>
		internal static async Task Run<TSource>(
			IAsyncQuery<TSource> source,
			AsyncIterationHint mode,
			Func<TSource, CancellationToken, Task> action)
		{
			await using (var iterator = source.GetAsyncEnumerator(mode))
			{
				Contract.Debug.Assert(iterator != null, "The underlying sequence returned a null async iterator");

				var ct = source.Cancellation;
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					await action(iterator.Current, ct).ConfigureAwait(false);
				}
			}
		}

		/// <summary>Immediately execute an async action on each element of an async query</summary>
		/// <typeparam name="TState">Type of the state</typeparam>
		/// <typeparam name="TSource">Type of elements of the async query</typeparam>
		/// <param name="source">Source async query</param>
		/// <param name="mode">Expected execution mode of the query</param>
		/// <param name="state">Value that will be passed as the first parameter to <paramref name="action"/></param>
		/// <param name="action">Asynchronous action to perform on each element as it arrives</param>
		/// <returns>Number of items that have been processed</returns>
		internal static async Task Run<TState, TSource>(
			IAsyncQuery<TSource> source,
			AsyncIterationHint mode,
			TState state,
			Func<TState, TSource, CancellationToken, Task> action)
		{
			await using (var iterator = source.GetAsyncEnumerator(mode))
			{
				Contract.Debug.Assert(iterator != null, "The underlying sequence returned a null async iterator");

				var ct = source.Cancellation;
				while (await iterator.MoveNextAsync().ConfigureAwait(false))
				{
					await action(state, iterator.Current, ct).ConfigureAwait(false);
				}
			}
		}

		/// <summary>Helper async method to get the first element of an async query</summary>
		/// <typeparam name="TSource">Type of elements of the async query</typeparam>
		/// <param name="source">Source async query</param>
		/// <param name="single">If true, the sequence must contain at most one element</param>
		/// <param name="orDefault">When the sequence is empty: If true then returns the default value for the type. Otherwise, throws an exception</param>
		/// <param name="defaultValue">Value that is returned when <paramref name="orDefault"/> if <c>true</c> and the query does not return any results.</param>
		/// <returns>Value of the first element of the <param ref="source"/> sequence, or the default value, or an exception (depending on <paramref name="single"/> and <paramref name="orDefault"/></returns>
		public static async Task<TSource> Head<TSource>(
			IAsyncQuery<TSource> source,
			bool single,
			bool orDefault,
			TSource defaultValue
			)
		{
			await using var iterator = source.GetAsyncEnumerator(AsyncIterationHint.Head);
			Contract.Debug.Assert(iterator != null, "The underlying sequence returned a null async iterator");

			if (await iterator.MoveNextAsync().ConfigureAwait(false))
			{
				TSource first = iterator.Current;
				if (single)
				{
					if (await iterator.MoveNextAsync().ConfigureAwait(false)) throw ErrorMoreThenOneElement();
				}
				return first;
			}
			if (!orDefault) throw ErrorNoElements();
			return defaultValue;
		}

		#endregion

	}

}
