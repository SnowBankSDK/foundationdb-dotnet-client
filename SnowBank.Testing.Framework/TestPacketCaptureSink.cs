#region Copyright (c) 2023-2026 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace SnowBank.Testing.Framework
{
	using SnowBank.Networking.PacketCapture;

	public class TestPacketCaptureSink : IPacketCaptureSink
	{
		public string Name => "Test";

		public IDistributedTestContext Context { get; }

		public Timeline Timeline { get; }

		public TestPacketCaptureSink(IDistributedTestContext context)
		{
			this.Context = context;
			this.Timeline = context.Timeline;
		}

		public ValueTask Emit(ReadOnlyMemory<CapturedPacket> packets, CancellationToken ct)
		{
			this.Context.EmitNetworkPackets(packets);
			var timeline = this.Timeline;
			foreach (var packet in packets.Span)
			{
				timeline.Record(Convert(packet));
			}
			return default;
		}

		private static Timeline.Datum Convert(CapturedPacket packet)
		{
			var metadata = packet.Metadata;
			var req = metadata.Request;
			var res = metadata.Response;
			return new() 
			{
				Start = metadata.StartedAt, 
				End = metadata.EndedAt, 
				Category = "HTTP",
				Label = $"{res?.Status ?? 0} {req.Method} {metadata.Uri} ({req.Headers.ContentType} => {res?.Headers?.ContentType ?? "???"}) [{packet.Id}]",
				Source = metadata.ActorId,
				CorrelationId = packet.Metadata.CorrelationId,
			};
		}

	}

}
