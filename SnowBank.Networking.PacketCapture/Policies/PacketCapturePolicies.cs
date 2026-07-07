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
	using Microsoft.AspNetCore.Connections;

	public static class PacketCapturePolicies
	{

		public static IPacketCapturePolicy All { get; } = new DefaultPacketCapturePolicy(
			(connection) => true,
			(request) =>
			{
				if (request.Request.Path.StartsWithSegments("/dev/network"))
				{ // by default, we ignore requests for the PacketViewer application (if locally hosted); otherwise we will have an infinite loop!
					return CapturedHttpFields.None;
				}
				//if (request.Request.ContentType == "application/grpc")
				//{ // by default, we ignore gRPC-type requests (because of the "bug" of the missing Flush on WriteSingleMessageAsync at https://github.com/grpc/grpc-dotnet/blob/b82c0c7354162a5489027b388f787c3b2a0a97a2/src/Grpc.AspNetCore.Server/Internal/PipeExtensions.cs#L48)
				//	return CapturedHttpFields.None;
				//}
				return CapturedHttpFields.All;
			});

		/// <summary>Policy that will not capture any packets.</summary>
		public static IPacketCapturePolicy None { get; } = new DefaultPacketCapturePolicy(
			(_) => false,
			(_) => CapturedHttpFields.None
		);

		/// <summary>Creates a custom capture policy</summary>
		/// <param name="connectionFilter">Predicate called on new connections. No packet will be captured from this connection if it returns <c>false</c>.</param>
		/// <param name="requestFilter">Predicated called on incoming requests. The package will not be captured if it returns <c>false</c>.</param>
		/// <returns>Custom policy</returns>
		public static IPacketCapturePolicy Create(Func<ConnectionContext, bool> connectionFilter, Func<HttpContext, CapturedHttpFields> requestFilter)
			=> new DefaultPacketCapturePolicy(connectionFilter, requestFilter);

	}

}
