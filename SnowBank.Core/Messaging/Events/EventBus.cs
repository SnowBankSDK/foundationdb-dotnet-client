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

namespace SnowBank.Messaging.Events
{
	using NodaTime;

	public class EventBus : IEventBus
	{

		public IEventSink[] Sinks { get; }

		public IClock Clock { get; }

		private IMessageDispatcher<IEvent> Dispatcher { get; }

		public EventBus(IEnumerable<IEventSink> sinks, IClock? clock)
		{
			this.Sinks = sinks.ToArray();
			this.Clock = clock ?? SystemClock.Instance;

			this.Dispatcher = this.Sinks.Length switch
			{
				0 => MessageDispatcher.Null<IEvent>(),
				1 => CreateDispatcher(this.Sinks[0]),
				_ => MessageDispatcher.Combine<IEvent>(this.Sinks.Select(sink => CreateDispatcher(sink)))
			};

			this.Dispatcher.Start();

			static IMessageDispatcher<IEvent> CreateDispatcher(IEventSink sink)
			{
				return sink.Async
					? MessageDispatcher.Buffered<IEvent, IEventSink>(
						sink,
						(batch, sink, ct) => sink.Dispatch(batch, ct)
					)
					: MessageDispatcher.Direct<IEvent, IEventSink>(
						sink,
						(one, sink) => sink.Dispatch(one, CancellationToken.None).GetAwaiter().GetResult(),
						(batch, sink) => sink.Dispatch(batch, CancellationToken.None).GetAwaiter().GetResult()
					);
			}
		}

		public Instant Now() => this.Clock.GetCurrentInstant();

		public static EventBus Create(IEventSink sink, IClock? clock = null) => new([ sink ], clock);

		public static EventBus CreateInMemory(IEventFilter? filter = null, IClock? clock = null) => new([ InMemoryEventSink.Create(filter) ], clock);

		public List<IEventSink> GetSinks()
		{
			return [ ..this.Sinks ];
		}

		public void Dispatch(IEvent evt)
		{
			Contract.Debug.Requires(evt != null);
			this.Dispatcher.Dispatch(evt);
		}

		public void Dispatch(ReadOnlyMemory<IEvent> batch)
		{
			if (batch.Length != 0)
			{
				this.Dispatcher.Dispatch(batch);
			}
		}

		public Task DrainAsync(CancellationToken ct)
		{
			return this.Dispatcher.DrainAsync(false, ct);
		}

		public ValueTask DisposeAsync()
		{
			return this.Dispatcher.DisposeAsync();
		}

	}

}
