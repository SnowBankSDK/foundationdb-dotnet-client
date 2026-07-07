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
	using System.Net;
	using SnowBank.Networking;
	using SnowBank.Networking.Http;
	using SnowBank.Networking.PacketCapture;

	/// <summary>Base class for simulated web browsers (Playwright, AngleSharp, ...) that render pages by routing their traffic onto the virtual network.</summary>
	[PublicAPI]
	public abstract class WebBrowserTestComponent : DistributedTestComponent
	{

		protected WebBrowserTestComponent(string id, IVirtualNetworkLocation location, CancellationToken lifetime, IDistributedTestComponent? parent = null)
			: base(id, location, lifetime, parent)
		{ }

		/// <summary>Guards the lazy, one-time start of the packet-capture manager against concurrent <see cref="ForwardToMeshAsync"/> calls.</summary>
		private object CaptureStartLock { get; } = new();

		/// <summary>Hook for browser engines that must download or update their binaries the first time they run on a host or CI server. Default: no-op.</summary>
		/// <remarks>Subclasses that need an install step (e.g. Playwright downloading Chromium) override this and invoke it early in their start-up.</remarks>
		protected virtual ValueTask EnsureBrowserAvailableAsync(CancellationToken ct) => default;

		/// <summary>Result of forwarding a browser request onto the virtual network.</summary>
		public sealed record MeshResponse
		{
			public required HttpStatusCode Status { get; init; }

			public required IReadOnlyList<KeyValuePair<string, string>> Headers { get; init; }

			public required byte[] Body { get; init; }
		}

		/// <summary>Forwards a browser-intercepted HTTP request onto the virtual network and returns the full response (status, headers, body).</summary>
		/// <param name="capture">When <see langword="true"/>, routes through the capturing <c>BetterHttpClient</c> path so the packet-capture filter fires; when <see langword="false"/>, uses a raw handler that bypasses capture.</param>
		protected async Task<MeshResponse> ForwardToMeshAsync(HttpMethod method, Uri url, IEnumerable<KeyValuePair<string, string>> headers, byte[]? body, string? contentType, bool capture, CancellationToken ct)
		{
			Contract.NotNull(method);
			Contract.NotNull(url);

			using var request = new HttpRequestMessage(method, url);
			foreach (var header in headers)
			{
				request.Headers.TryAddWithoutValidation(header.Key, header.Value);
			}
			if (body is not null)
			{
				request.Content = new ByteArrayContent(body);
				if (!string.IsNullOrEmpty(contentType))
				{
					request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
				}
			}

			if (capture)
			{
				// capturing path: BetterHttpClient => the PacketCaptureHttpFilter fires.
				EnsureCaptureStarted();

				// Callback shape mirrors WebRequesterTestComponent.NavigateTo exactly (single ctx arg, closes over ct).
				var client = this.GetBetterHttpClient(url);
				return await client.SendAsync(request, async (ctx) =>
				{
					var responseBody = await ctx.Response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
					return BuildResponse(ctx.Response, responseBody);
				}, ct).ConfigureAwait(false);
			}
			else
			{
				// raw path: a plain HttpClient over the virtual-network handler, no capture filter
				var factory = this.GetRequiredService<IBetterHttpClientFactory>();
				using var raw = new HttpClient(factory.CreateHttpHandler(url, new BetterHttpClientOptions()));
				using var response = await raw.SendAsync(request, ct).ConfigureAwait(false);
				var responseBody = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
				return BuildResponse(response, responseBody);
			}
		}

		/// <summary>Lazily starts the component's packet-capture manager on the first captured send, exactly once.</summary>
		/// <remarks>
		/// <para>The manager must be running for the emitted packet to reach the sink (it is drained automatically on Stop,
		/// but starting it is not automatic - callers like <c>WebRequesterTestComponent</c> do it from their single-threaded
		/// <c>OnStarting</c>; a browser subclass may override <c>OnStarting</c> entirely, so we start it lazily here instead).</para>
		/// <para><see cref="ForwardToMeshAsync"/> is called CONCURRENTLY (a real browser fetches many page assets in parallel),
		/// so this must be race-safe: <see cref="PacketCaptureManager.Start"/> just assigns <c>RunTask = Task.Run(Run)</c> with no
		/// internal guard, and two racing starts would orphan the first reader and split the mailbox across two <c>nextId</c>
		/// sequences (colliding <c>CapturedPacketId</c>s). The lock + re-check ensures at most one thread ever calls Start.</para>
		/// </remarks>
		private void EnsureCaptureStarted()
		{
			var packetManager = this.Services.GetService<PacketCaptureManager>();
			if (packetManager is null || packetManager.IsRunning) return;

			lock (this.CaptureStartLock)
			{
				if (!packetManager.IsRunning)
				{
					packetManager.Start();
				}
			}
		}

		private static MeshResponse BuildResponse(HttpResponseMessage response, byte[] body)
		{
			var headers = new List<KeyValuePair<string, string>>();
			foreach (var h in response.Headers)
			{
				AddHeader(headers, h.Key, h.Value);
			}
			foreach (var h in response.Content.Headers)
			{
				AddHeader(headers, h.Key, h.Value);
			}
			return new MeshResponse
			{
				Status = response.StatusCode,
				Headers = headers,
				Body = body,
			};
		}

		/// <summary>Appends a header's values to <paramref name="headers"/>, folding multi-value headers with a comma per RFC 9110 §5.3 - except <c>Set-Cookie</c>, which cannot be comma-folded (RFC 6265 §3): cookie values may themselves legitimately contain commas, and folding several cookies together would produce one malformed value. Each <c>Set-Cookie</c> value is therefore added as its own entry, exactly as it came off the wire.</summary>
		private static void AddHeader(List<KeyValuePair<string, string>> headers, string name, IEnumerable<string> values)
		{
			if (string.Equals(name, "Set-Cookie", StringComparison.OrdinalIgnoreCase))
			{
				foreach (var value in values)
				{
					headers.Add(new(name, value));
				}
			}
			else
			{
				headers.Add(new(name, string.Join(",", values)));
			}
		}

	}

}
