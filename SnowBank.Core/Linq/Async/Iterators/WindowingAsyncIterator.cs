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

	/// <summary>Merges bursts of already-completed items from a source async sequence, into a sequence of batches.</summary>
	/// <typeparam name="TInput">Type of the items from the source sequence</typeparam>
	public class WindowingAsyncIterator<TInput> : AsyncFilterIterator<TInput, TInput[]>
	{
		// Typical use cas: to merge back into arrays the result of readers that read one page at a time from the database, but return each item individually.
		// This iterator will attempt to reconstruct full batches from sequences of items that where all produced at the same time, so that asynchronous operations
		// can be performed on them as soon as possible, without waiting for the next batch to arrive.

		#region Example sequence...

		// Example: Inner iterator reads 10 items from the db, using 3 pages of 4 items, 3 items and then 3 items. The sequence of calls to Inner.MoveNext() will look like this:

		// query = db.GetRange(...).Batched(CAPACITY).Select(T[] => ...).ToListAsync()
		//           | ^ Inner ^   | ^ Outer         | ^ Parent        | ^ Execution point

		// - Outer.MoveNext() called by Parent
		//   - Inner.MoveNext().Status => pending, waiting for page 1
		//	  							   ... (waiting for db)
		//	  						   => completed, Current = item #0
		//   - Inner.MoveNext().Status => completed, Current = item #1
		//   - Inner.MoveNext().Status => completed, Current = item #2
		//   - Inner.MoveNext().Status => completed, Current = item #3
		//   - Inner.MoveNext().Status => pending, waiting for page 2, on hold
		//   => Publish([ item#0, item#1, item#2, item#3 ])
		// - batch being processed by Parent...
		//                                 ... (db returned page 2)
		// - Outer.MoveNext() called Parent
		//   - (pending task)          => completed, Current = item #4
		//   - Inner.MoveNext().Status => completed, Current = item #5
		//   - Inner.MoveNext().Status => completed, Current = item #6
		//   - Inner.MoveNext().Status => pending, waiting for page 3, on hold
		//   => Publish([ item#4, item#5, item#6 ])
		// - batch being processed by Parent...
		//                                 ... (db returned page 3)
		// - Outer.MoveNext() called by Parent
		//   - (pending task)          => completed, Current = item #7
		//   - Inner.MoveNext().Status => completed, Current = item #8
		//   - Inner.MoveNext().Status => completed, Current = item #9
		//   - Inner.MoveNext().Status => completed, DONE
		//   - Mark Inner as Completed.
		//   => Publish([ item#7, item#8, item#9 ])
		// - batch being processed by Parent...
		// - Outer.MoveNext() called by Parent
		//   - Inner has already completed, no more data available.
		//   => Completed()
		// - Outer is DONE

		#endregion

		// ITERABLE

		// maximum size of a buffer
		private readonly int m_maxWindowSize;

		// ITERATOR

		// buffer storing the items in the current window
		private List<TInput>? m_buffer;
		// holds on to the last pending call to m_iterator.MoveNext() when our buffer is full
		private Task<bool>? m_nextTask;

		/// <summary>Create a new batching iterator</summary>
		/// <param name="source">Source sequence of items that must be batched by waves</param>
		/// <param name="maxWindowSize">Maximum size of a batch to return down the line</param>
		public WindowingAsyncIterator(IAsyncQuery<TInput> source, int maxWindowSize)
			: base(source)
		{
			Contract.Debug.Requires(maxWindowSize > 0);
			m_maxWindowSize = maxWindowSize;
		}

		protected override AsyncLinqIterator<TInput[]> Clone()
		{
			return new WindowingAsyncIterator<TInput>(m_source, m_maxWindowSize);
		}

		protected override void OnStarted(IAsyncEnumerator<TInput> iterator)
		{
			// pre-allocate the inner buffer, if it is not too big
			m_buffer = new List<TInput>(Math.Min(m_maxWindowSize, 1024));
		}

		protected override async ValueTask<bool> OnNextAsync()
		{
			// read items from the source until the next call to Inner.MoveNext() is not already complete, or we have filled our buffer

			var iterator = m_iterator;
			var buffer = m_buffer;
			var ct = this.Cancellation;
			Contract.Debug.Requires(iterator != null && buffer != null);

			var ft = Interlocked.Exchange(ref m_nextTask, null);
			if (ft == null)
			{ // read the next item from the inner iterator
				if (m_innerHasCompleted) return await Completed().ConfigureAwait(false);
				ft = iterator.MoveNextAsync().AsTask();
			}

			// always wait for the first item (so that we have at least something in the batch)
			bool hasMore = await ft.ConfigureAwait(false);

			// most db queries will read items by chunks, so there is a high chance the next following calls to MoveNext() will already be completed
			// as long as this is the case, and that our buffer is not full, continue eating items. Stop only when we end up with a pending task.

			while (hasMore && !ct.IsCancellationRequested)
			{
				buffer.Add(iterator.Current);

				var vt = iterator.MoveNextAsync();
				if (buffer.Count >= m_maxWindowSize || !vt.IsCompleted)
				{ // save it for next time

					//TODO: add heuristics to check if the batch is large enough to stop there, or if we should eat the latency and wait for the next wave of items to arrive!
					// ex: we batch by 10, inner return 11 consecutive items. We will transform the first 10, then only fill the next batch with the 11th item because the 12th item is still not ready.

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
				//Console.WriteLine("## inner has finished");
				m_innerHasCompleted = true;
				if (buffer.Count == 0)
				{ // that was the last batch!
					//Console.WriteLine("# we got nothing ! :(");
					return await Completed().ConfigureAwait(false);
				}
			}

			var items = buffer.ToArray();
			buffer.Clear();
			return Publish(items);
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
