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
