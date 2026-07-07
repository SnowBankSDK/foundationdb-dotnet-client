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

namespace SnowBank.Messaging
{
	using System.Threading.Channels;

	public static class MessageDispatcher
	{
		public static IMessageDispatcher<TMessage> Null<TMessage>()
			where TMessage : class
			=> new NullMessageDispatcher<TMessage>();

		public static IMessageDispatcher<TMessage> Direct<TMessage, TState>(TState state, Action<TMessage, TState> one, Action<ReadOnlyMemory<TMessage>, TState> many)
			where TMessage : class
			=> new DirectMessageDispatcher<TMessage, TState>(state, one, many);


		public static IMessageDispatcher<TMessage> Buffered<TMessage, TState>(TState state, Func<ReadOnlyMemory<TMessage>, TState, CancellationToken, Task> handler)
			where TMessage : class
			=> new BufferedMessageDispatcher<TMessage, TState>(state, handler);

		public static IMessageDispatcher<TMessage> Combine<TMessage>(IEnumerable<IMessageDispatcher<TMessage>> dispatchers)
			where TMessage : class
			=> new MultipleMessageDispatcher<TMessage>(dispatchers.ToArray());

	}

	/// <summary>Dispatch messages into the void</summary>
	public sealed class NullMessageDispatcher<TMessage> : IMessageDispatcher<TMessage>
		where TMessage : class
	{
		public ValueTask DisposeAsync() => default;

		public void Start() { /* NO-OP */ }

		public void Complete() { /* NO-OP */ }

		public Task DrainAsync(bool final, CancellationToken ct) => Task.CompletedTask;

		public void Dispatch(TMessage message) { /* NO-OP */ }

		public void Dispatch(ReadOnlyMemory<TMessage> batch) { /* NO-OP */ }

	}

	/// <summary>Dispatch messages to multiple dispatchers</summary>
	public sealed class MultipleMessageDispatcher<TMessage> : IMessageDispatcher<TMessage>
		where TMessage : class
	{

		public MultipleMessageDispatcher(IMessageDispatcher<TMessage>[] dispatchers)
		{
			this.Dispatchers = dispatchers;
		}

		public IMessageDispatcher<TMessage>[] Dispatchers { get; set; }

		public void Start()
		{
			foreach (var dispatcher in this.Dispatchers)
			{
				dispatcher.Start();
			}
		}

		public void Complete()
		{
			foreach (var dispatcher in this.Dispatchers)
			{
				dispatcher.Complete();
			}
		}

		public ValueTask DisposeAsync()
		{
			return new ValueTask(Task.WhenAll(this.Dispatchers.Select(dispatcher => dispatcher.DisposeAsync().AsTask())));
		}

		public void Dispatch(TMessage message)
		{
			foreach (var dispatcher in this.Dispatchers)
			{
				dispatcher.Dispatch(message);
			}
		}

		public void Dispatch(ReadOnlyMemory<TMessage> batch)
		{
			foreach (var dispatcher in this.Dispatchers)
			{
				dispatcher.Dispatch(batch);
			}
		}

		public Task DrainAsync(bool final, CancellationToken ct)
		{
			return Task.WhenAll(this.Dispatchers.Select(dispatcher => dispatcher.DrainAsync(final, ct)));
		}

	}

	/// <summary>Dispatch messages using a buffered <see cref="Channel{TMessage}">channel</see> for async processing</summary>
	public sealed class BufferedMessageDispatcher<TMessage, TState> : IMessageDispatcher<TMessage>
		where TMessage : class
	{

		private Channel<object> Pipeline { get; }
		// TMessage or TaskCompletionSource

		private TState State { get; }

		private Func<ReadOnlyMemory<TMessage>, TState, CancellationToken, Task> Handler { get; }

		public BufferedMessageDispatcher(TState state, Func<ReadOnlyMemory<TMessage>, TState, CancellationToken, Task> handler)
		{
			Contract.NotNull(handler);

			this.Pipeline = Channel.CreateUnbounded<object>(new UnboundedChannelOptions() { SingleReader = true, SingleWriter = false });
			this.State = state;
			this.Handler = handler;
		}

		private CancellationTokenSource Lifetime { get; } = new CancellationTokenSource();

		private Task RunTask = Task.CompletedTask;

		public void Dispatch(TMessage message)
		{
			this.Pipeline.Writer.TryWrite(message);
		}

		public void Dispatch(ReadOnlyMemory<TMessage> batch)
		{
			var writer = this.Pipeline.Writer;
			foreach (var message in batch.Span)
			{
				writer.TryWrite(message);
			}
		}

		public void Start()
		{
			this.RunTask = Task.Factory.StartNew(() => Run(this.Lifetime.Token), this.Lifetime.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
		}

		public void Complete()
		{
			this.Pipeline.Writer.TryComplete();
		}

		public async Task DrainAsync(bool final, CancellationToken ct)
		{
			var runTask = this.RunTask;
			if (!runTask.IsCanceled)
			{
				var tcs = new TaskCompletionSource<object?>();
				try
				{
					this.Pipeline.Writer.TryWrite(tcs);
					if (final) this.Pipeline.Writer.TryComplete();

					var t = await Task.WhenAny(tcs.Task, runTask, Task.Delay(Timeout.Infinite, ct)).ConfigureAwait(false);
					if (t == tcs.Task)
					{
						await tcs.Task.ConfigureAwait(false);
					}
				}
				finally
				{
					tcs.TrySetCanceled();
				}
			}
		}

		public async ValueTask DisposeAsync()
		{
			using (this.Lifetime)
			{
				// protection just in case...
				this.Lifetime.CancelAfter(TimeSpan.FromSeconds(5));

				// drain!
				await DrainAsync(true, this.Lifetime.Token).ConfigureAwait(false);

				// cancel the main task
				this.Lifetime.Cancel();
				// wait for the main task to complete..
				try
				{
					await this.RunTask.ConfigureAwait(false);
				}
				catch (Exception)
				{
					//BUGBUG: TODO: logger?
				}
			}

		}

		private async Task Run(CancellationToken ct)
		{
			const int MAX_BATCH_SIZE = 64;

			var reader = this.Pipeline.Reader;
			var batch = new TMessage[MAX_BATCH_SIZE];
			int pos = 0;

			bool running = true;
			try
			{
				while (running && !ct.IsCancellationRequested)
				{
					TaskCompletionSource<object?>? signal = null;

					// wait for events...
					if (!await reader.WaitToReadAsync(ct).ConfigureAwait(false))
					{ // not more messages...
						running = false;
					}
					else
					{
						// read as much as possible in a batch...
						while (reader.TryRead(out var item))
						{
							// either it's a signal
							if (item is TaskCompletionSource<object?> tcs)
							{
								//note: we must dispatch items in the current batch before triggering the signal!
								signal = tcs;
								break;
							}

							if (item is not TMessage evt) throw new InvalidOperationException($"Channel can only receive {typeof(TMessage).Name} or {nameof(TaskCompletionSource<object?>)} !");

							batch[pos++] = evt;
							if (pos >= batch.Length) break;
						}
					}

					// dispatch the batch if there are any messages in it...
					if (pos != 0)
					{
						var chunk = batch.AsMemory(0, pos);
						await this.Handler(chunk, this.State, ct).ConfigureAwait(false);
						pos = 0;
						chunk.Span.Clear();
					}

					// trigger the signal if there is one
					signal?.TrySetResult(null);
				}
			}
			catch (Exception)
			{
				if (!ct.IsCancellationRequested)
				{
					//TODO: logger?
				}
			}
		}

	}

	/// <summary>Dispatch messages directly, using the caller's thread</summary>
	public sealed class DirectMessageDispatcher<TMessage, TState> : IMessageDispatcher<TMessage>
		where TMessage : class
	{

		public DirectMessageDispatcher(TState state, Action<TMessage, TState> one, Action<ReadOnlyMemory<TMessage>, TState> many)
		{
			this.State = state;
			this.DispatchOne = one;
			this.DispatchMany = many;
		}

		private TState State { get; }

		private Action<TMessage, TState> DispatchOne { get; }

		private Action<ReadOnlyMemory<TMessage>, TState> DispatchMany { get; }

		private bool Ready = true;

		public void Start() { /* NO-OP */ }

		public void Complete()
		{
			this.Ready = false;
		}

		public ValueTask DisposeAsync()
		{
			this.Ready = false;
			return default;
		}

		public void Dispatch(TMessage message)
		{
			if (this.Ready) this.DispatchOne(message, this.State);
		}

		public void Dispatch(ReadOnlyMemory<TMessage> batch)
		{
			if (this.Ready) this.DispatchMany(batch, this.State);
		}

		public Task DrainAsync(bool final, CancellationToken ct)
		{
			return Task.CompletedTask;
		}

	}

}
