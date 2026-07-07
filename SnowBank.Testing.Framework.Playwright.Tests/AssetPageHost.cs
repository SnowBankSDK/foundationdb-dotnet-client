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

namespace SnowBank.Testing.Framework.Playwright.Tests
{
	using System.Text;
	using Microsoft.AspNetCore.Builder;
	using Microsoft.AspNetCore.Http;
	using Microsoft.AspNetCore.Routing;

	/// <summary>Serves a page that references a configurable number of static assets (css/js/png), to exercise packet capture under many small in-memory requests.</summary>
	public static class AssetPageHost
	{
		/// <summary>Maps a page referencing <paramref name="assetCount"/> css/js/png assets.</summary>
		/// <param name="assetCount">Number of each asset type to reference (total assets = 3 * <paramref name="assetCount"/>).</param>
		/// <param name="uniformBodyBytes">When set, every css/js asset returns a body of this size (bytes), regardless of index;
		/// used to exercise the capture path under realistic multi-MB bodies. When <see langword="null"/> (the default),
		/// only index 0 of each type is "large" (~512 KB) and the rest are small (~256 B).</param>
		public static void MapAssetPage(IEndpointRouteBuilder app, int assetCount, int? uniformBodyBytes = null)
		{
			app.MapGet("/", (HttpContext _) =>
			{
				var sb = new StringBuilder();
				sb.Append("<html><head>");
				for (int i = 0; i < assetCount; i++)
				{
					sb.Append($"<link rel=\"stylesheet\" href=\"/asset/{i}.css\"/>");
					sb.Append($"<script src=\"/asset/{i}.js\"></script>");
				}
				sb.Append("</head><body><h1>assets</h1>");
				for (int i = 0; i < assetCount; i++)
				{
					sb.Append($"<img src=\"/asset/{i}.png\"/>");
				}
				sb.Append("</body></html>");
				return Results.Content(sb.ToString(), "text/html");
			});

			app.MapGet("/asset/{name}", (string name) =>
			{
				// index 0 of each type is "large" (~512 KB) to surface body-size effects;
				// when uniformBodyBytes is set, EVERY asset uses that size instead.
				bool large = name.StartsWith("0.");
				int fillSize = uniformBodyBytes ?? (large ? 512 * 1024 : 256);
				if (name.EndsWith(".css"))
				{
					return Results.Content(new string('/', fillSize) + "\nbody{}", "text/css");
				}
				if (name.EndsWith(".js"))
				{
					return Results.Content("var x=" + new string('0', fillSize) + ".length;", "application/javascript");
				}
				if (name.EndsWith(".png"))
				{
					// 1x1 transparent PNG
					var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
					return Results.Bytes(png, "image/png");
				}
				return Results.NotFound();
			});
		}
	}
}
