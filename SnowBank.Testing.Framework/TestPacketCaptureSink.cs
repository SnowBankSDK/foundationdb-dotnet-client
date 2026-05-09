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
