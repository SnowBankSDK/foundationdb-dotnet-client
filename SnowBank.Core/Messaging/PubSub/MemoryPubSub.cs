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

namespace SnowBank.Messaging.PubSub
{
	using System.Collections.Concurrent;
	using SnowBank.Data.Json;
	using Microsoft.Extensions.DependencyInjection;

	public class MemoryPubSub : IPubSub
	{

		private sealed class Subscription : IAsyncDisposable
		{

			public MemoryPubSub Parent { get; }

			public Guid Id { get; }

			public string Channel { get; }

			public Func<string, JsonValue, CancellationToken, ValueTask> Handler { get; }

			public Subscription(MemoryPubSub parent, Guid id, string channel, Func<string, JsonValue, CancellationToken, ValueTask> handler)
			{
				this.Parent = parent;
				this.Id = id;
				this.Channel = channel;
				this.Handler = handler;
			}

			public ValueTask DisposeAsync()
			{
				this.Parent.CancelSubscription(this);
				return default;
			}

		}

		public Task<IAsyncDisposable> SubscribeAsync(string channel, Func<string, JsonValue, CancellationToken, ValueTask> onMessageReceived, CancellationToken ct)
		{
			if (m_disposed) throw new ObjectDisposedException(this.GetType().Name);
			var subscription = new Subscription(this, Guid.NewGuid(), channel, onMessageReceived);
			this.SubscribersByChannel.TryAdd(subscription.Id, subscription);
			return Task.FromResult<IAsyncDisposable>(subscription);
		}

		private void CancelSubscription(Subscription subscription)
		{
			this.SubscribersByChannel.TryRemove(subscription.Id, out _);
		}

		public async Task PublishAsync(string channel, JsonValue message, CancellationToken ct)
		{
			Contract.NotNull(channel);
			Contract.NotNull(message);
			Contract.Debug.Requires(message.IsReadOnly);
			if (m_disposed) throw new ObjectDisposedException(this.GetType().Name);
			ct.ThrowIfCancellationRequested();

			foreach (var subscribersByChannel in this.SubscribersByChannel.Values)
			{
				if (string.Equals(subscribersByChannel.Channel, channel, StringComparison.OrdinalIgnoreCase))
				{
					// Execute method for everyone subscribed on this channel:
					await subscribersByChannel.Handler(channel, message, ct).ConfigureAwait(false);
				}
			}
		}

		private ConcurrentDictionary<Guid, Subscription> SubscribersByChannel { get; set; } = new ConcurrentDictionary<Guid, Subscription>();
		
		private bool m_disposed;
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (m_disposed) return;
			m_disposed = true;
			if (disposing)
			{
				this.SubscribersByChannel.Clear();
			}
		}

	}

	public static class MemoryPubSubExtension
	{
		public static IServiceCollection AddMemoryPubSub(this IServiceCollection services, Action<MemoryPubSubOptions>? configure = null)
		{
			services.AddSingleton<IPubSub, MemoryPubSub>();
			services.Configure<MemoryPubSubOptions>(option => configure?.Invoke(option));
			return services;
		}
	}

	public class MemoryPubSubOptions
	{
		//TODO?
	}

}
