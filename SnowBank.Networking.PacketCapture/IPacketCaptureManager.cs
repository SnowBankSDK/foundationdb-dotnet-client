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

	/// <summary>Service that can ingest a stream of captured http requests</summary>
	public interface IPacketCaptureManager
	{

		/// <summary>Records a new captured record</summary>
		/// <param name="metadata">Metadata about the captured query</param>
		/// <param name="requestBody">Captured body of the request (or <see cref="Slice.Nil"/> if the request does not have a body)</param>
		/// <param name="responseBody">Captured body of the response (or <see cref="Slice.Nil"/> if the response does not have a body)</param>
		/// <returns></returns>
		ValueTask Emit(CapturedPacketMetadata metadata, Slice requestBody, Slice responseBody);

		/// <summary>Ensures all captured records have been processed (logged to disk, processed by the sinks, ...)</summary>
		/// <remarks>Please call this before stopping the capture (or aborting a test run); otherwise, some captured records may be lost</remarks>
		Task DrainAsync(CancellationToken ct);

	}

}
