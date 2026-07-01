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

	/// <summary>Middleware that handles setting up HTTP connections for capture</summary>
	public sealed class PacketCaptureConnectionMiddleware
	{
		public PacketCaptureManager Manager { get; }

		public ConnectionDelegate Next { get; }

		public PacketCaptureConnectionMiddleware(PacketCaptureManager manager, ConnectionDelegate next)
		{
			this.Manager = manager;
			this.Next = next;
		}

		public Task OnConnectAsync(ConnectionContext ctx)
		{
			return this.Manager.ShouldCaptureConnection(ctx) ? PerformCapture(ctx, this.Next) : this.Next(ctx);
		}

		private async Task PerformCapture(ConnectionContext ctx, ConnectionDelegate next)
		{
			var session = this.Manager.StartNewSession(ctx);
			try
			{
				ctx.Features.Set(session);

				if (this.Manager.Options.OnConnectionStarted is { } onStarted)
				{
					await onStarted(session);
				}

				await next(ctx);
			}
			finally
			{
				if (this.Manager.Options.OnConnectionCompleted is { } onCompleted)
				{
					await onCompleted(session);
				}

				ctx.Features.Set(default(PacketCaptureConnectionContext));
				session.Dispose();
			}
		}

	}

}
