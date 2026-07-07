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
	public sealed record FilePacketCaptureOptions
	{

		public string BasePath { get; set; } = ".";

		public string PathTemplate { get; set; } = "{RemotePeer}/{Date}/{Timestamp}-{TraceId}-{Path}.log";

		//TODO: compression?
		//TODO: crypto?

	}

	/// <summary>Sink that writes captured packets to a file on disk</summary>
	public sealed class FilePacketCaptureSink : IPacketCaptureSink
	{
		public string Name => "File";

		public FilePacketCaptureOptions Options { get; }

		public FilePacketCaptureSink(FilePacketCaptureOptions options)
		{
			Contract.NotNull(options);

			if (string.IsNullOrEmpty(options.BasePath)) throw new ArgumentException($"Requires option '{nameof(options.BasePath)}' is missing.");
			if (string.IsNullOrEmpty(options.PathTemplate)) throw new ArgumentException($"Requires option '{nameof(options.PathTemplate)}' is missing.");

			this.Options = options;
		}

		private static string SafePeerPathSegment(string? peer)
		{
			return peer?.Replace('.', '_').Replace(':', '_') ?? "unknown";
		}

		private static string GetFilePath(CapturedPacket packet, string basePath, string pathTemplate)
		{
			string filePath = pathTemplate;
			var metadata = packet.Metadata;
			filePath = filePath.Replace("{Id}", packet.Id.ToString());
			filePath = filePath.Replace("{Date}", metadata.StartedAt.ToString("yyyyMMdd", null));
			filePath = filePath.Replace("{Timestamp}", metadata.StartedAt.ToString("HHmmss'_'fffffff", null));
			filePath = filePath.Replace("{RemotePeer}", SafePeerPathSegment(metadata.Connection.RemoteHost));
			filePath = filePath.Replace("{Path}", metadata.Request.Path?.Replace('/', '~') ?? "unkown");
			filePath = filePath.Replace("{TraceId}", metadata.TraceId.Replace(':', '_'));
			string path = Path.Combine(basePath, filePath);
			//BUGBUG: TODO: how to efficiently check that the filepath has not escaped the basepath?
			Contract.Debug.Ensures(!System.IO.Path.GetRelativePath(basePath, path).StartsWith('.'), "Packet path cannot escape the base path!");
			return path;
		}

		public async ValueTask Emit(ReadOnlyMemory<CapturedPacket> packets, CancellationToken ct)
		{
			string? prevFolder = null;
			var basePath = Path.GetFullPath(this.Options.BasePath);
			var pathTemplate = this.Options.PathTemplate;

			for (int i = 0; i < packets.Length; i++)
			{
				var packet = packets.Span[i];
				string path = GetFilePath(packet, basePath, pathTemplate);

				// ensure the containing folder exists!
				string folder = Path.GetDirectoryName(path)!;
				if (folder != prevFolder)
				{
					if (!Directory.Exists(folder))
					{
						Directory.CreateDirectory(folder);
					}
					prevFolder = folder;
				}

				await File.WriteAllTextAsync(path, packet.GetBasicDump(includeBody: true), ct).ConfigureAwait(false);
			}
		}

	}

}
