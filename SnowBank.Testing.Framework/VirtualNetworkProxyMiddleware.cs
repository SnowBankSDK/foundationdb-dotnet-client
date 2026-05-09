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
	using System.Globalization;
	using System.Net;
	using Microsoft.AspNetCore.Builder;
	using Microsoft.AspNetCore.Http;
	using StructuredFieldValues;

	/// <summary>Middleware that updates the <see cref="HttpContext.Connection"/> using the headers injected during virtual network calls.</summary>
	public sealed class VirtualNetworkProxyMiddleware : IMiddleware
	{

		/// <inheritdoc/>
		public Task InvokeAsync(HttpContext context, RequestDelegate next)
		{
			// use the headers that were injected by the virtual network client to populate the Connection metadata

			if (context.Connection.RemoteIpAddress is null)
			{
				if (context.Request.Headers.TryGetValue("X-SBK-ORIGIN", out var values))
				{
					var error = SfvParser.ParseItem(values.ToString(), out var items);
					if (error is null
					 && items.Parameters.TryGetValue("peer", out var value)
					 && value is string peer
					 && TryParsePeer(peer, out var ip, out var port))
					{
						context.Connection.RemoteIpAddress = ip;
						context.Connection.RemotePort = port;
					}
				}
			}

			if (context.Connection.LocalIpAddress is null)
			{
				if (context.Request.Headers.TryGetValue("X-SBK-TARGET", out var values))
				{
					var error = SfvParser.ParseItem(values.ToString(), out var items);
					if (error is null
					 && items.Parameters.TryGetValue("peer", out var value)
					 && value is string peer
					 && TryParsePeer(peer, out var ip, out var port))
					{
						context.Connection.LocalIpAddress = ip;
						context.Connection.LocalPort = port;
					}
				}
			}

			return next(context);
		}

		private static bool TryParsePeer(ReadOnlySpan<char> literal, [MaybeNullWhen(false)] out IPAddress ip, out int port)
		{
			// we expect the format "IP:PORT"

			int p = literal.Length > 0 ? literal.LastIndexOf(':') : -1;
			if (p < 0 || !int.TryParse(literal[(p + 1)..], CultureInfo.InvariantCulture, out port) || !IPAddress.TryParse(literal[..p], out ip))
			{
				ip = null;
				port = 0;
				return false;
			}

			return true;
		}

	}

	/// <summary>Helper methods for injecter the <see cref="VirtualNetworkProxyMiddleware"/> into a test component</summary>
	public static class VirtualNetworkProxyMiddlewareExtensions
	{

		public static IApplicationBuilder UseVirtualNetworkProxy(this IApplicationBuilder app)
		{
			return app.UseMiddleware<VirtualNetworkProxyMiddleware>();
		}

	}

}
