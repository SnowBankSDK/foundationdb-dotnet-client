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

	/// <summary>Prefetches items from the inner sequence, before outputting them down the line.</summary>
	/// <typeparam name="TInput">Type of the items from the source sequence</typeparam>
	public class PrefetchingAsyncIterator<TInput> : AsyncFilterIterator<TInput, TInput>
	{

		// This iterator can be used to already ask for the next few items, while they are being processed somewhere down the line of the query.
		// This can be useful, when combined with Batching or Windowing, to maximize the throughput of db queries that read pages of results at a time.

		// ITERABLE

		// max number of items to prefetch
		private readonly int m_prefetchCount;

		// ITERATOR

		// buffer storing the items in the current window
		private Queue<TInput>? m_buffer;
		// holds on to the last pending call to m_iterator.MoveNext() when our buffer is full
		private Task<bool>? m_nextTask;

		/// <summary>Create a new batching iterator</summary>
		/// <param name="source">Source sequence of items that must be batched by waves</param>
		/// <param name="prefetchCount">Maximum size of a batch to return down the line</param>
		public PrefetchingAsyncIterator(IAsyncQuery<TInput> source, int prefetchCount)
			: base(source)
		{
			Contract.Debug.Requires(prefetchCount > 0);
			m_prefetchCount = prefetchCount;
		}

		protected override AsyncLinqIterator<TInput> Clone()
		{
			return new PrefetchingAsyncIterator<TInput>(m_source, m_prefetchCount);
		}

		protected override void OnStarted(IAsyncEnumerator<TInput> iterator)
		{
			// pre-allocate the buffer with the number of slot we expect to use
			m_buffer = new Queue<TInput>(m_prefetchCount);
		}

		protected override ValueTask<bool> OnNextAsync()
		{
			var buffer = m_buffer;
			if (buffer is { Count: > 0 })
			{
				var nextTask = m_nextTask;
				if (nextTask == null || !nextTask.IsCompleted)
				{
					return new ValueTask<bool>(Publish(buffer.Dequeue()));
				}
			}

			return PrefetchNextItemsAsync();
		}

		protected virtual async ValueTask<bool> PrefetchNextItemsAsync()
		{
			var ct = this.Cancellation;
			ct.ThrowIfCancellationRequested();

			// read items from the source until the next call to Inner.MoveNext() is not already complete, or we have filled our prefetch buffer, then returns the first item in the buffer.
			var iterator = m_iterator;
			Contract.Debug.Requires(m_innerHasCompleted || iterator != null);

			var ft = Interlocked.Exchange(ref m_nextTask, null);
			if (ft == null)
			{ // read the next item from the inner iterator
				if (m_innerHasCompleted) return await Completed().ConfigureAwait(false);
				ft = iterator!.MoveNextAsync().AsTask();
			}

			// always wait for the first item (so that we have at least something in the batch)
			bool hasMore = await ft.ConfigureAwait(false);

			// most db queries will read items by chunks, so there is a high chance the next following calls to MoveNext() will already be completed
			// as long as this is the case, and that our buffer is not full, continue eating items. Stop only when we end up with a pending task.

			while (hasMore && !ct.IsCancellationRequested)
			{
				m_buffer ??= new Queue<TInput>(m_prefetchCount);
				m_buffer.Enqueue(iterator!.Current);

				var vt = iterator.MoveNextAsync();
				if (m_buffer.Count >= m_prefetchCount || !vt.IsCompleted)
				{ // save it for next time
					m_nextTask = vt.AsTask();
					break;
				}

				// we know the task is already completed, so we will immediately get the next result, or blow up if the inner iterator failed
				hasMore = vt.Result;
				//note: if inner blows up, we won't send any previously read items down the line. This may change the behavior of queries with a .Take(N) that would have stopped before reading the (N+1)th item that would have failed.
			}
			ct.ThrowIfCancellationRequested();

			if (!hasMore)
			{
				await MarkInnerAsCompleted().ConfigureAwait(false);
				if (m_buffer == null || m_buffer.Count == 0)
				{ // that was the last batch!
					return await Completed().ConfigureAwait(false);
				}
			}

			var current = m_buffer!.Dequeue();
			return Publish(current);
		}

		protected override void OnStopped()
		{
			m_buffer = null;

			// defuse the task, which should fail once we dispose the inner iterator below...
			var t = Interlocked.Exchange(ref m_nextTask, null);
			if (t != null)
			{
				ObserveTask(t);
			}

			static void ObserveTask(Task task)
			{
				switch (task.Status)
				{
					case TaskStatus.Faulted:
					case TaskStatus.Canceled:
						TouchFaultedTask(task);
						return;

					case TaskStatus.RanToCompletion:
						return;

					default:
						task.ContinueWith(TouchFaultedTask, TaskContinuationOptions.OnlyOnFaulted);
						return;
				}

			}

			static void TouchFaultedTask(Task t)
			{
#if DEBUG
				var error = t.Exception;
				if (t.IsFaulted)
				{
					System.Diagnostics.Debug.WriteLine($"### muted unobserved failed Task[{t.Id}]: [{error?.InnerException?.GetType().Name}] {error?.InnerException?.Message}");
				}
#else
				_ = t.Exception;
#endif
			}

		}

	}

}
