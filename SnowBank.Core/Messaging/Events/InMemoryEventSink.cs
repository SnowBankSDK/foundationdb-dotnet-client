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

	/// <summary>Implementation of a Job Event Log that keeps all events in memory, mostly intended for unit testing</summary>
	public sealed class InMemoryEventSink : IEventSink
	{

		public InMemoryEventSink(IEventFilter filter)
			: this(filter, null)
		{
			this.Filter = filter;
		}

		public InMemoryEventSink(IEventFilter filter, IEnumerable<IEvent>? initialEvents)
		{
			Contract.NotNull(filter);
			this.Filter = filter;
			if (initialEvents != null)
			{
				this.Events.AddRange(initialEvents);
			}
		}

		public static InMemoryEventSink Create(IEventFilter? filter = null) => new InMemoryEventSink(filter ?? EventFilters.All);

		private IEventFilter Filter { get; }

		private List<IEvent> Events { get; } = [ ];

#if NET9_0_OR_GREATER
		private Lock Lock { get; } = new();
#else
		private object Lock { get; } = new();
#endif

		public bool Async => false;
		//note: in truth, this is a "lie" because we lock(), but this is not really an issue here!

		public Task Dispatch(IEvent evt, CancellationToken ct)
		{
			lock (this.Lock)
			{
				this.Events.Add(evt);
			}
			return Task.CompletedTask;
		}

		public Task Dispatch(ReadOnlyMemory<IEvent> batch, CancellationToken ct)
		{
			lock (this.Lock)
			{
				foreach(var evt in batch.Span)
				{
					this.Events.Add(evt);
				}
			}
			return Task.CompletedTask;
		}

		public IReadOnlyList<IEvent> GetAllEvents()
		{
			lock (this.Lock)
			{
				//note: we return a copy!
				return new List<IEvent>(this.Events);
			}
		}

		public InMemoryEventSink Copy()
		{
			lock (this.Lock)
			{
				return new InMemoryEventSink(this.Filter, this.Events);
			}
		}

	}

}
