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
	using System.Text;

	/// <summary>Sink that writes captured packets to either the <see cref="System.Diagnostics.Debug"/> or <see cref="System.Diagnostics.Trace"/> debugger output.</summary>
	public sealed class DiagnosticsPacketCaptureSink : IPacketCaptureSink
	{
		public string Name { get; }

		public bool Debug { get; }

		public DiagnosticsPacketCaptureSink(bool debug, string name)
		{
			this.Debug = debug;
			this.Name = name;
		}

		public ValueTask Emit(ReadOnlyMemory<CapturedPacket> packets, CancellationToken ct)
		{
			foreach (var packet in packets.Span)
			{
				if (this.Debug)
				{
					System.Diagnostics.Debug.WriteLine(packet.GetBasicDump(includeBody: false));
				}
				else
				{
					System.Diagnostics.Trace.WriteLine(packet.GetBasicDump(includeBody: false));
				}
			}
			return default;
		}

	}

	public sealed record ConsolePacketCaptureOptions
	{
		public bool Color { get; set; }

	}

	public sealed class ConsolePacketCaptureSink : IPacketCaptureSink
	{
		public string Name => "Trace";

		public ConsolePacketCaptureOptions Options { get; }

		public ConsolePacketCaptureSink(ConsolePacketCaptureOptions options)
		{
			this.Options = options;
		}

		private StringBuilder Cache { get; } = new StringBuilder();

		public ValueTask Emit(ReadOnlyMemory<CapturedPacket> packets, CancellationToken ct)
		{
			var sb = this.Cache;
			sb.Clear();

			if (this.Options.Color)
			{
				sb.Append("\x1b[38;5;0007m");
				foreach (var packet in packets.Span)
				{
					sb.AppendLine(packet.GetBasicDump(includeBody: false));
				}
				sb.Append("\x1b[0m");
			}
			else
			{
				foreach (var packet in packets.Span)
				{
					sb.AppendLine(packet.GetBasicDump(includeBody: false));
				}
			}

			Console.WriteLine(sb.ToString());
			return default;
		}

	}

}
