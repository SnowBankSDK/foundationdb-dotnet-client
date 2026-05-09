#region Copyright (c) 2023-2026 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace SnowBank.Testing.Framework
{
	using Microsoft.AspNetCore.Builder;
	using Microsoft.AspNetCore.Routing;

	/// <summary>Interface for host or host builders that can add additional routes (aka "Minimal API")</summary>
	public interface IMinimalApiTestComponentBuilder
	{

		void AddRoute(Action<IEndpointRouteBuilder> handler);

		// all the Map{Get|Post|...} methods are implemented as extension methods

	}

	/// <summary>Extensions methods for quickly scaffolding custom APIs on test components that implement <see cref="IMinimalApiTestComponentBuilder"/></summary>
	public static class MinimalApiTestComponentBuilderExtensions
	{

		extension(IMinimalApiTestComponentBuilder @this)
		{

			/// <summary>Maps a new HTTP route on this host</summary>
			public void MapMethods([StringSyntax("Route")] string pattern, IEnumerable<string> httpMethods, Delegate handler, bool isFallback, Action<RouteHandlerBuilder>? configure = null)
			{
				@this.AddRoute((ep) =>
				{
					var builder = ep.MapMethods(pattern, httpMethods, handler);
					configure?.Invoke(builder);
				});
			}

			/// <summary>Maps a new HTTP route on this host</summary>
			public void Map([StringSyntax("Route")] string pattern, Delegate handler, Action<RouteHandlerBuilder>? configure = null)
			{
				@this.AddRoute((ep) =>
				{
					var builder = ep.Map(pattern, handler);
					configure?.Invoke(builder);
				});
			}

			/// <summary>Maps a new HTTP GET route on this host</summary>
			public void MapGet([StringSyntax("Route")] string pattern, Delegate handler, Action<RouteHandlerBuilder>? configure = null)
			{
				@this.AddRoute((ep) =>
				{
					var builder = ep.MapGet(pattern, handler);
					configure?.Invoke(builder);
				});
			}

			/// <summary>Maps a new HTTP POST route on this host</summary>
			public void MapPost([StringSyntax("Route")] string pattern, Delegate handler, Action<RouteHandlerBuilder>? configure = null)
			{
				@this.AddRoute((ep) =>
				{
					var builder = ep.MapPost(pattern, handler);
					configure?.Invoke(builder);
				});
			}

			/// <summary>Maps a new HTTP PUT route on this host</summary>
			public void MapPut([StringSyntax("Route")] string pattern, Delegate handler, Action<RouteHandlerBuilder>? configure = null)
			{
				@this.AddRoute((ep) =>
				{
					var builder = ep.MapPut(pattern, handler);
					configure?.Invoke(builder);
				});
			}

			/// <summary>Maps a new HTTP PATCH route on this host</summary>
			public void MapPatch([StringSyntax("Route")] string pattern, Delegate handler, Action<RouteHandlerBuilder>? configure = null)
			{
				@this.AddRoute((ep) =>
				{
					var builder = ep.MapPatch(pattern, handler);
					configure?.Invoke(builder);
				});
			}

			/// <summary>Maps a new HTTP DELETE route on this host</summary>
			public void MapDelete([StringSyntax("Route")] string pattern, Delegate handler, Action<RouteHandlerBuilder>? configure = null)
			{
				@this.AddRoute((ep) =>
				{
					var builder = ep.MapDelete(pattern, handler);
					configure?.Invoke(builder);
				});
			}

			/// <summary>Maps a new HTTP fallback route on this host</summary>
			public void MapFallback([StringSyntax("Route")] string pattern, Delegate handler, Action<RouteHandlerBuilder>? configure = null)
			{
				@this.AddRoute((ep) =>
				{
					var builder = ep.MapFallback(pattern, handler);
					configure?.Invoke(builder);
				});
			}

		}

	}

}
