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
	using System.Threading.Channels;
	using SnowBank.Data.Json;

	/// <summary>Extensions methods for the <see cref="IPubSub"/> abstraction</summary>
	[PublicAPI]
	public static class PubSubExtensions
	{

		/// <summary>Helper that wraps a channel subscription and forwards all received messages into a <see cref="ChannelWriter{T}"/></summary>
		[DebuggerDisplay("Channel={Channel}")]
		private sealed class ChannelSubscription : IAsyncDisposable
		{

			public ChannelSubscription(string channel, ChannelWriter<JsonValue> writer, bool autoCompleteOnClose)
			{
				this.Channel = channel;
				this.Writer = writer;
				this.AutoCompleteOnClose = autoCompleteOnClose;
			}

			/// <summary>Name of the channel we are subscribed to</summary>
			public string Channel { get; }

			/// <summary>All received messages are written to this instance</summary>
			public ChannelWriter<JsonValue> Writer { get; }

			/// <summary>Token used to cancel the underlying subscription</summary>
			public IAsyncDisposable? InnerSubscription { get; set; }

			/// <summary>If true, calls TryComplete on the channel writer when we are done/aborted</summary>
			public bool AutoCompleteOnClose { get;}

			/// <summary>Callback invoked by the subscription when a new message is available</summary>
			public ValueTask Callback(string channelId, JsonValue message, CancellationToken ct)
			{
				Contract.Debug.Requires(channelId == this.Channel && message != null && message.IsReadOnly);
				return this.Writer.WriteAsync(message, ct);
			}

			public ValueTask DisposeAsync()
			{
				if (this.AutoCompleteOnClose)
				{
					this.Writer.TryComplete();
				}
				return this.InnerSubscription?.DisposeAsync() ?? default;
			}
		}

		/// <summary>Subscribes to a channel, and propagates all messages received to a <see cref="Channel{T}">Channel writer</see>.</summary>
		/// <param name="pubsub">Source of messages</param>
		/// <param name="channel">Channel to subscribe to</param>
		/// <param name="writer">All message received will be written to this instance</param>
		/// <param name="autoCompleteOnClose">If <see langword="true"/>, the method <see cref="ChannelWriter{T}.TryComplete"/> will be called on the writer when the subscription is disposed.</param>
		/// <param name="ct">Token used to forcefully abort the subscription.</param>
		/// <returns>Subscription token that must be disposed when the caller wants to cancel the subscription.</returns>
		/// <remarks>Any new message will be pumped into the channel <paramref name="writer"/>, until either <paramref name="ct"/> is triggered, or the caller invokes <see cref="IAsyncDisposable.DisposeAsync"/> on the returned token.</remarks>
		/// 
		public static async Task<IAsyncDisposable> SubscribeAsync(this IPubSub pubsub, string channel, ChannelWriter<JsonValue> writer, bool autoCompleteOnClose, CancellationToken ct)
		{
			Contract.NotNull(channel);
			Contract.NotNull(writer);

			var sub = new ChannelSubscription(channel, writer, autoCompleteOnClose);

			sub.InnerSubscription = await pubsub.SubscribeAsync(
				channel,
				sub.Callback,
				ct
			).ConfigureAwait(false);

			return sub;
		}

		/// <summary>Returns an <see cref="IAsyncEnumerable{T}"/> that will read all messages received on a channel.</summary>
		/// <param name="pubsub">Source of messages</param>
		/// <param name="channel">Channel to subscribe to</param>
		/// <param name="ct">Token used to stop reading from this channel</param>
		/// <returns>Enumerable that will asynchronously return all messages received on this channel, in sequential order.</returns>
		/// <remarks>
		/// <para>The enumerator will never stop until either <paramref name="ct"/> is triggered, or the caller disposes of the async enumerator.</para>
		/// </remarks>
		public static async IAsyncEnumerable<JsonValue> ReadAllAsync(this IPubSub pubsub, string channel, [EnumeratorCancellation] CancellationToken ct)
		{
			// we use a channel to decouple the thread that receive messages from the consumer of this enumeration
			var bus = Channel.CreateUnbounded<JsonValue>(new UnboundedChannelOptions() { SingleReader = true });

			// subscribes to the channel
			await using var sub = await SubscribeAsync(pubsub, channel, bus.Writer, autoCompleteOnClose: true, ct).ConfigureAwait(false);

			await foreach (var msg in bus.Reader.ReadAllAsync(ct).ConfigureAwait(false))
			{
				yield return msg;
			}
		}

	}

}
