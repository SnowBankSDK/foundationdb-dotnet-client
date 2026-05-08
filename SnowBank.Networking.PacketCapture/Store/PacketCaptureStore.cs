#region Copyright (c) 2023-2026 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace SnowBank.Networking.PacketCapture
{
	using System.Runtime.CompilerServices;
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Options;

	/// <summary>Dispatcher that can asynchronously push captured messages to a sink</summary>
	public interface IPacketCaptureStoreDispatcher
	{

		Task Dispatch(CapturedPacketId last, CancellationToken ct);

	}

	/// <summary>Store that asynchronously dispatch captured packets to one or more <see cref="IPacketCaptureStoreDispatcher"/></summary>
	/// <remarks>Uses a ring buffer to hold bursts of requests while they are being pushed to the sinks</remarks>
	public class PacketCaptureStore : IPacketCaptureStore
	{

		private IPacketCaptureStoreDispatcher[] Dispatchers { get; }

		private PacketCaptureStoreOptions Options { get; }

		private ILogger Logger { get; }

		private readonly CapturedPacket?[] Ring;
		private CapturedPacketId LastId;
		private CapturedPacketId FirstId = CapturedPacketId.Zero;

		public PacketCaptureStore(IOptions<PacketCaptureStoreOptions> options, IEnumerable<IPacketCaptureStoreDispatcher> dispatchers, ILogger<PacketCaptureStore> logger)
		{
			this.Options = options.Value;
			if (this.Options.BufferSize <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(options), "BufferSize must be greater than 1");
			}

			this.Dispatchers = dispatchers.ToArray();
			this.Logger = logger;

			this.Ring = new CapturedPacket?[this.Options.BufferSize];
		}

		private CapturedPacketDescriptor ToDescriptor(CapturedPacket packet)
		{
			return new()
			{
				Id = packet.Id,
				Metadata = packet.Metadata,
				// TODO : Cache depending on the content (Fingerprint / Hashcode...), so that we don't always re-fetch the same bytes...
				RequestBodyId = !packet.RequestBody.IsNull ? "TODO:" + packet.Id : null,
				ResponseBodyId = !packet.ResponseBody.IsNull ? "TODO:" + packet.Id : null,
			};
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int GetRingOffset(CapturedPacketId id, int ringSize)
		{
			return (int) (id.Value % (ulong) ringSize);
		}

		public Task AddPacket(CapturedPacket packet, CancellationToken ct)
		{
			var id = packet.Id;

			var ring = this.Ring;

			//TODO: if we reuse a slot, we need to remove any stored bodies as well!
			var offset = GetRingOffset(id, ring.Length);
			ring[offset] = packet;
			this.LastId = packet.Id;

			offset++;
			if (offset >= ring.Length) offset = 0;
			this.FirstId = ring[offset]?.Id ?? CapturedPacketId.Zero;

#if FULLDEBUG
			DumpState();
#endif

			//TODO: store bodies!

			return Task.CompletedTask;
		}

		public Task AddBatch(ReadOnlyMemory<CapturedPacket> packets, CancellationToken ct)
		{
			if (packets.Length == 0) return Task.CompletedTask;

			var ring = this.Ring;
			var lastId = this.LastId;
			var lastOffset = 0;
			foreach(var packet in packets.Span)
			{
				var id = packet.Id;
				Contract.Debug.Assert(id.IsSuccessorOf(lastId) || lastId.IsEmpty, "There is a gap in packets!!");
				lastOffset = GetRingOffset(id, ring.Length);
				ring[lastOffset] = packet;
				//TODO: store bodies!
				lastId = id;
			}
			this.LastId = lastId;

			lastOffset++;
			if (lastOffset >= ring.Length) lastOffset -= ring.Length;
			this.FirstId = ring[lastOffset]?.Id ?? CapturedPacketId.Zero;

#if FULLDEBUG
			DumpState();
#endif

			return DispatchBatch(this.Dispatchers, lastId, ct);

			static async Task DispatchBatch(IPacketCaptureStoreDispatcher[] dispatchers, CapturedPacketId lastId, CancellationToken ct)
			{
				foreach (var dispatcher in dispatchers)
				{
					await dispatcher.Dispatch(lastId, ct);
				}
			}
		}

		public Task<CapturedPacketDescriptor?> GetPacket(CapturedPacketId id, CancellationToken ct)
		{
			var ring = this.Ring;
			var packet = ring[GetRingOffset(id, ring.Length)];

			var res = packet != null && packet.Id == id ? ToDescriptor(packet) : null;

			return Task.FromResult(res);

		}
		
		public Task<CapturedPacketQueryResult> GetNext(CapturedPacketId cursor, CancellationToken ct)
		{
			var result = new CapturedPacketQueryResult();

			var last = this.LastId;
			if (cursor != CapturedPacketId.Zero && cursor.Generation != last.Generation)
			{ // start from scratch!
				cursor = CapturedPacketId.Zero;
			}

			result.First = this.FirstId;
			result.Last = last;

			if (cursor >= last)
			{ // caller is up to date
				//TODO:BUGBUG: si cursor > last, y a un problème! (reboot?)
				return Task.FromResult(result);
			}
			
			var ring = this.Ring;
			int start = GetRingOffset(cursor, ring.Length);
			var packet = ring[start];
			if (packet != null)
			{
				// TODO...
				//if(packet.Id != cursor)
				//{ // ring has rolled over one (ore more) times since last call!

				//	// copy everything to the result!
				//	result.MissingResults = true;
				//	throw new NotImplementedException();
				//}
			} 

			var packets = new List<CapturedPacketDescriptor>();

			// copy all items since last cursor
			int current = start;
			int n = ring.Length;
			int r = n;
			while(r-- > 0)
			{
				current = (current + 1) % n;
				packet = ring[current];
				if (packet == null) continue;
				packets.Add(ToDescriptor(packet));
				if (packet.Id == last) break;
			}

			result.Packets = packets;

			return Task.FromResult(result);
		}

		public Task<Slice> GetBody(string id, string type, CancellationToken ct)
		{
			try
			{
				var pktId = CapturedPacketId.Parse(id);
				var pkt = this.Ring.FirstOrDefault(p => p?.Id == pktId);
				if (pkt != null)
				{
					switch (type)
					{
						case "request":  return Task.FromResult(pkt.RequestBody);
						case "response": return Task.FromResult(pkt.ResponseBody);
					}
				}
			}
			catch (Exception)
			{
				// TODO ...
			}
			return Task.FromResult(Slice.Nil);
		}

#if FULLDEBUG
		private void DumpState()
		{
			var sb = new StringBuilder();

			var ring = this.Ring;
			sb.Append("LastId: ").Append(this.LastId).Append(" @ ").Append(this.LastId.GetRingOffset(ring.Length)).Append("; Content=");
			for (int i = 0; i < ring.Length; i++)
			{
				var packet = ring[i];
				if (packet == null) continue;
				sb.Append(i).Append("=").Append(packet.Id).Append("; ");
			}
			System.Diagnostics.Trace.WriteLine(sb.ToString());
		}
#endif

	}

}
