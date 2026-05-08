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
	/// <summary>Middleware that handles capturing HTTP requests</summary>
	public sealed class PacketCaptureRequestMiddleware : IMiddleware
	{

		public Task InvokeAsync(HttpContext context, RequestDelegate next)
		{
			var session = context.Features.Get<PacketCaptureConnectionContext?>();
			if (session != null)
			{
				var fields = session.ShouldCapture(context) & session.Manager.Options.AllowedFields;
				if (fields != CapturedHttpFields.None)
				{
					return PerformCapture(session, fields, context, next);
				}
			}
			return next(context);

			static async Task PerformCapture(PacketCaptureConnectionContext session, CapturedHttpFields fields, HttpContext context, RequestDelegate next)
			{
				session.BeginRequest(context, fields);
				try
				{
					await next(context);

					// Suite https://github.com/grpc/grpc-dotnet/issues/1679 (au 05/04/2023 toujours open)
					// On flush nous même les bytes car WriteSingleMessageAsync ne flush pas (bug ?)
					var response = context.Response;
					var bodyWriter = response.BodyWriter;
					if (response.HasStarted && bodyWriter.CanGetUnflushedBytes && bodyWriter.UnflushedBytes > 0)
					{
						await bodyWriter.FlushAsync(context.RequestAborted);
					}
				}
				finally
				{
					session.EndRequest(context);
				}
			}
		}

	}

}
