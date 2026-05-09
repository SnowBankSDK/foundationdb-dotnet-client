#region Copyright (c) 2023-2026 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
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
