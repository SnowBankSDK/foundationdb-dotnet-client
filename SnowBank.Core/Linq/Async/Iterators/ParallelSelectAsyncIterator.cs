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

//#define FULL_DEBUG

namespace SnowBank.Linq.Async.Iterators
{

	/// <summary>[EXPERIMENTAL] Iterates over an async sequence of items, kick off an async task in parallel, and returning the results in order</summary>
	/// <typeparam name="TSource">Type of elements of the inner async sequence</typeparam>
	/// <typeparam name="TResult">Type of elements of the outer async sequence</typeparam>
	public sealed class ParallelAsyncIterator<TSource, TResult> : AsyncFilterIterator<TSource, TResult>
	{

		/// <summary>Default max concurrency when doing batch queries</summary>
		/// <remarks>TODO: this is a placeholder value !</remarks>
		public const int DefaultMaxConcurrency = 32;

		// The goal is to have an underlying sequence providing "seed" values (say, documents ids from an ongoing mergesort or intersect)
		// that will kick off tasks for each one (say, a fetch or load for each of the ids) and output the results of these tasks *in order* as they complete
		// Since we can't spin out too many tasks, we also want to be able to put a cap no the max number of pending tasks

		private readonly Func<TSource, CancellationToken, Task<TResult>> m_taskSelector;
		private readonly ParallelAsyncQueryOptions m_options;

		private CancellationTokenSource? m_cts;
		private CancellationToken m_token;
		private volatile bool m_done;

		/// <summary>Pump that reads values from the inner iterator</summary>
		private AsyncIteratorPump<TSource>? m_pump;
		/// <summary>Inner pump task</summary>
		private Task? m_pumpTask;
		/// <summary>Queue that holds items that are being processed</summary>
		private AsyncTransformQueue<TSource, TResult>? m_processingQueue;

		public ParallelAsyncIterator(
			IAsyncQuery<TSource> source,
			Func<TSource, CancellationToken, Task<TResult>> taskSelector,
			ParallelAsyncQueryOptions options
		)
			: base(source)
		{
			Contract.Debug.Requires(taskSelector != null && options != null);

			m_taskSelector = taskSelector;
			m_options = options;
		}

		protected override AsyncLinqIterator<TResult> Clone()
		{
			return new ParallelAsyncIterator<TSource, TResult>(m_source, m_taskSelector, m_options);
		}
		protected override async ValueTask<bool> OnFirstAsync()
		{
			if (!await base.OnFirstAsync().ConfigureAwait(false))
			{
				return false;
			}

			LogDebug("[OnFirstAsync] wiring up inner iterator");

			m_cts = new CancellationTokenSource();
			m_token = m_cts.Token;
			m_done = false;

			// we need a queue to hold the pending tasks (and their results)
			m_processingQueue = new AsyncTransformQueue<TSource, TResult>(m_taskSelector, m_options.MaxConcurrency ?? DefaultMaxConcurrency, m_options.Scheduler);

			// we also need a pump that will work on the inner sequence
			Contract.Debug.Assert(m_iterator != null);
			m_pump = new AsyncIteratorPump<TSource>(m_iterator, m_processingQueue);

			// start pumping
			m_pumpTask = m_pump.PumpAsync(m_token).ContinueWith((t) =>
			{
				// ReSharper disable once RedundantAssignment
				var e = t.Exception!; // observe the exception
				LogDebug($"Pump stopped with error: {e.Message}");
			}, TaskContinuationOptions.OnlyOnFaulted);

			LogDebug("[OnFirstAsync] pump started");

			Contract.Debug.Ensures(m_pumpTask != null);

			return true;
		}

		protected override async ValueTask<bool> OnNextAsync()
		{
			try
			{
				LogDebug($"[OnNextAsync] #{Environment.CurrentManagedThreadId}");

				if (m_done) return false;

				Contract.Debug.Requires(m_processingQueue != null);
				var next = await m_processingQueue.ReceiveAsync(this.Cancellation).ConfigureAwait(false);
				LogDebug("[OnNextAsync] got result from queue");

				if (!next.HasValue)
				{
					m_done = true;
					if (next.Failed)
					{
						LogDebug("[OnNextAsync] received failure");
						// we want to make sure that the exception callstack is as clean as possible,
						// so we rely on Maybe<T>.ThrowIfFailed() to do the correct thing!
						await MarkAsFailed().ConfigureAwait(false);
						next.ThrowForNonSuccess();
						return false;
					}
					else
					{
						LogDebug("[OnNextAsync] received completion");
						return await Completed().ConfigureAwait(false);
					}
				}
				LogDebug($"[OnNextAsync] received value {next.Value}");

				return Publish(next.Value);
			}
			catch (Exception e)
			{
				LogDebug($"[OnNextAsync] received failed: {e.Message}");
				m_done = true;
				throw;
			}
#if FULL_DEBUG
			finally
			{
				LogDebug("[/OnNextAsync] " + Environment.CurrentManagedThreadId);
			}
#endif
		}

		public override ValueTask DisposeAsync()
		{
			if (m_cts != null)
			{
				if (!m_cts.IsCancellationRequested)
				{
					try { m_cts.Cancel(); } catch(ObjectDisposedException) { }
				}
				m_cts.Dispose();
			}
			//TODO: cancel the pump and queue ?
			//TODO: wait for m_pumpTask to complete ??
			return base.DisposeAsync();
		}

		[Conditional("FULL_DEBUG")]
		private static void LogDebug(string msg)
		{
#if FULL_DEBUG
			Console.WriteLine("[SelectAsync] " + msg);
#endif
		}

	}

}
