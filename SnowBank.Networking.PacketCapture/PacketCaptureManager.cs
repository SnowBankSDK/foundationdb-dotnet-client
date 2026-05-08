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

namespace SnowBank.Networking.PacketCapture
{
	using System.Net;
	using System.Runtime.CompilerServices;
	using System.Runtime.ExceptionServices;
	using System.Threading.Channels;
	using Microsoft.AspNetCore.Connections;
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Logging.Abstractions;
	using Microsoft.Extensions.Options;
	using Microsoft.IO;

	/// <summary>Handles the capture of http requests</summary>
	public sealed class PacketCaptureManager : IPacketCaptureManager
	{
		public PacketCaptureOptions Options { get; }

		public IPacketCaptureStore Store { get; }

		public IPacketCaptureSink[] Sinks { get; }

		public RecyclableMemoryStreamManager Pool { get; }

		private CancellationTokenSource Lifetime { get; } = new CancellationTokenSource();

		public NodaTime.IClock Clock { get; }

		private Task RunTask { get; set; } = Task.CompletedTask;

		private Channel<(CapturedPacketMetadata? Metadata, Slice RequestBody, Slice ResponseBody, TaskCompletionSource? Signal)> Pipeline { get; } = Channel.CreateUnbounded<(CapturedPacketMetadata?, Slice, Slice, TaskCompletionSource?)>();

		private ILogger<PacketCaptureManager> Logger { get; }

		public PacketCaptureManager(IOptions<PacketCaptureOptions> options, IPacketCaptureStore store, ILoggerFactory? logger, NodaTime.IClock? clock, IEnumerable<IPacketCaptureSink> ambientSinks)
		{
			Contract.NotNull(options);
			Contract.NotNull(store);

			this.Options = options.Value;
			this.Store = store;
			this.Sinks = this.Options.Sinks.ToArray();
			this.Sinks = this.Options.AddAmbientSinks
				? this.Options.Sinks.Union(ambientSinks).ToArray()
				: this.Options.Sinks.ToArray();
			this.Logger = (logger ?? NullLoggerFactory.Instance).CreateLogger<PacketCaptureManager>();
			this.Clock = clock ?? NodaTime.SystemClock.Instance;
			this.Pool = this.Options.StreamPool ?? new RecyclableMemoryStreamManager();
		}

		public bool IsRunning => !this.RunTask.IsCompleted;

		public void Start()
		{
			this.Logger.LogDebug("Starting capture of packets!");
			this.RunTask = Task.Run(() => this.Run(), this.Lifetime.Token);
		}

		public void PrepareShutdown()
		{
			this.Logger.LogDebug("Preparing for shutdown of capture...");
			this.Pipeline.Writer.TryComplete();
		}

		public void Shutdown()
		{
			this.Logger.LogDebug("Stopping packet capture!");
			this.Pipeline.Writer.TryComplete();
			this.Lifetime.Cancel();
		}

		/// <summary>Decide if this HTTP connection should be captured</summary>
		internal bool ShouldCaptureConnection(ConnectionContext context)
		{
			if (!this.Options.Enabled || this.Lifetime.IsCancellationRequested) return false;
			try
			{
				return this.Options.CapturePolicy.ShouldCaptureConnection(context);
			}
			catch (Exception e)
			{
				this.Logger.LogWarning(e, $"Packet capture policy failed for connection {context.ConnectionId}");
				return false;
			}
		}

		public static (string Host, int? Port) FormatEndPoint(EndPoint? ep)
		{
			return ep switch
			{
				null            => ("<virtual>", null),
				IPEndPoint ipep => (IPAddress.IsLoopback(ipep.Address) ? "localhost" : ipep.Address.ToString(), ipep.Port),
				DnsEndPoint dns => (dns.Host, dns.Port),
				_               => (ep.ToString()!, null)
			};
		}

		internal PacketCaptureConnectionContext StartNewSession(ConnectionContext context)
		{
			var now = this.Clock.GetCurrentInstant();

			var remotePeer = FormatEndPoint(context.RemoteEndPoint);
			var localPeer = FormatEndPoint(context.LocalEndPoint);

			return new PacketCaptureConnectionContext(this, context, now, remotePeer.Host, remotePeer.Port, localPeer.Host, localPeer.Port);
		}

		/// <summary>Decide if this HTTP request should be captured</summary>
		internal CapturedHttpFields ShouldCaptureRequest(HttpContext context)
		{
			if (!this.Options.Enabled || this.Lifetime.IsCancellationRequested) return CapturedHttpFields.None;
			try
			{
				return this.Options.CapturePolicy.ShouldCaptureRequest(context);
			}
			catch (Exception e)
			{
				this.Logger.LogWarning(e, $"Packet capture policy failed for request {context.TraceIdentifier}");
				return CapturedHttpFields.None;
			}
		}

		//TODO: REVIEW: passer des MemoryStream plutot?
		public ValueTask Emit(CapturedPacketMetadata metadata, Slice requestBody, Slice responseBody)
		{

			Contract.NotNull(metadata);
			Contract.Debug.Requires(metadata.Request != null);
			Contract.Debug.Requires(metadata.Response != null);
			var ct = this.Lifetime.Token;
			return ct.IsCancellationRequested
				? default
				: this.Pipeline.Writer.WriteAsync((metadata, requestBody, responseBody, null), ct);
		}

		public async Task DrainAsync(CancellationToken ct)
		{
			if (!this.RunTask.IsCompleted)
			{
				using (var cts = CancellationTokenSource.CreateLinkedTokenSource(this.Lifetime.Token, ct))
				{
					cts.Token.ThrowIfCancellationRequested();

					var tcs = new TaskCompletionSource();
					try
					{
						await this.Pipeline.Writer.WriteAsync((null, default, default, tcs), cts.Token);

						// on doit stopper si:
						// - notre signal est trigger par la run task
						// - la run task a stoppé pour une raison ou une autre
						// - le CancellationToken de l'appelant est triggered
						var delay = Task.Delay(Timeout.Infinite, cts.Token);
						var t = await Task.WhenAny(tcs.Task, delay, this.RunTask);
						if (t == delay)
						{
							Contract.Debug.Assert(ct.IsCancellationRequested);
							tcs.TrySetCanceled(cts.Token);
							ct.ThrowIfCancellationRequested();
							//note: should always throw!
						}
						else if (t == tcs.Task)
						{
							await t;
						}
					}
					finally
					{
						tcs.TrySetResult();
					}
				}
			}
		}

		private async Task Run()
		{
			var ct = this.Lifetime.Token;

			var reader = this.Pipeline.Reader;

			var buffer = new CapturedPacket[16];

			string generation = Uuid64.NewUuid().ToString("x");
			var nextId = new CapturedPacketId(generation, 1);

			try
			{
				while (!ct.IsCancellationRequested)
				{
					if (!await reader.WaitToReadAsync(ct))
					{
						// we are done!
						break;
					}

					var actorId = this.Options.ActorId;
					int offset = 0;
					while (reader.TryRead(out var entry))
					{
						if (entry.Signal != null)
						{
							// on doit flush le batch existant s'il y en a un
							if (offset != 0)
							{
								await EmitPacketBatch(buffer.AsMemory(0, offset), ct);
								offset = 0;
							}

							// puis on peut trigger le signal
							entry.Signal.TrySetResult();
							continue;
						}

						Contract.Debug.Assert(entry.Metadata != null);
						entry.Metadata.ActorId = actorId;
						buffer[offset++] = new()
						{
							Id = nextId++,
							Metadata = entry.Metadata,
							RequestBody = entry.RequestBody,
							ResponseBody = entry.ResponseBody
						};
						if (offset >= buffer.Length)
						{
							await EmitPacketBatch(buffer.AsMemory(0, offset), ct);
							offset = 0;
						}
					}

					if (offset != 0)
					{
						await EmitPacketBatch(buffer.AsMemory(0, offset), ct);
					}
				}
			}
			catch (Exception e)
			{
				if (!ct.IsCancellationRequested)
				{
					this.Logger.LogError(e, "Packet Capture dispatcher thread has crashed");
				}
			}
		}

		private async ValueTask EmitPacketBatch(ReadOnlyMemory<CapturedPacket> packets, CancellationToken ct)
		{
			// on ajoute les packets a notre store local (qui peut faire dev>null selon la cfg)
			await this.Store.AddBatch(packets, ct);

			foreach (var sink in this.Sinks)
			{
				ct.ThrowIfCancellationRequested();
				try
				{
					await sink.Emit(packets, ct);
				}
				catch (Exception e)
				{
					this.Logger.LogWarning(e, $"Failed to emit packet batch to sink {sink.Name}");
				}
			}
		}

		public void ReportCaptureError(HttpContext context, ExceptionDispatchInfo edi)
		{
			this.Logger.LogError(edi.SourceException, $"Failed to capture packet {context.TraceIdentifier}");
			//TODO?
		}
	}

}
