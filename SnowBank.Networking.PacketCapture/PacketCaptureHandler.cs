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
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Net;
	using System.Net.Http;

	/// <summary>Delegating handler that captures every request flowing through a pooled <c>BetterHttp</c> chain, so that packet capture "rides the pipeline" instead of firing only for requests that went through the <c>BetterHttpClient</c> send extension.</summary>
	/// <remarks>
	/// <para>This handler is inserted as the OUTERMOST message handler of every name (above the pipeline's <c>MagicalHandler</c>), so a bare handler obtained from <see cref="IHttpMessageHandlerFactory"/> (the shape gRPC/SignalR consumers use) is captured just like a full <c>BetterHttpClient</c> send.</para>
	/// <para>It is the CANONICAL capturer: it stamps <see cref="RidesChainKey"/> on the request so the <see cref="PacketCaptureHttpFilter"/> (driven by the BetterHttpClient send extensions) stands down for the same request and does not double-capture.</para>
	/// <para>Bodies are intercepted with the same <see cref="InterceptedHttpContent"/> mirror machinery the filter uses. The response packet is emitted once, as soon as the caller has finished consuming the body - whichever comes first: the body is fully serialized (buffered reads), the response read-stream is disposed (streaming reads), or the response itself is disposed. This mirrors the filter's "emit after the response is consumed" timing without owning the read lifecycle. When there is no response body to capture (or the request failed), the packet is emitted right away.</para>
	/// <para>A long-lived, potentially-infinite response - a gRPC duplex body (<c>application/grpc</c>) or a Server-Sent Events feed (<c>text/event-stream</c>) - is NEVER mirrored. Its body stays open for the whole life of the connection, so mirroring it would grow without bound, never emit a packet (the body never ends), and tie this wrapper's disposal to the transport's live stream. Such a response is captured at headers only - request metadata plus response status and headers, its body marked as streaming - and its content is passed through completely untouched. The matching request body of a streaming/duplex request is left alone for the same reason: its bytes keep being written long after the response headers have arrived, so restoring and disposing an interceptor around it would tear the request mid-flight. Every finite response keeps its usual, complete body capture. See <see cref="IsStreamingContent"/>.</para>
	/// </remarks>
	internal sealed class PacketCaptureHandler : DelegatingHandler
	{

		/// <summary>Marker stamped on <see cref="HttpRequestMessage.Options"/> by this handler, so the <see cref="PacketCaptureHttpFilter"/> defers to it (capture rides the chain, and must happen exactly once).</summary>
		internal static readonly HttpRequestOptionsKey<bool> RidesChainKey = new("SnowBank.PacketCapture.RidesChain");

		/// <summary>Process-wide counter used to generate best-effort correlation ids for in-chain captures.</summary>
		private static long TraceCounter;

		public PacketCaptureHandler(PacketCaptureManager manager)
		{
			this.Manager = manager;
		}

		private PacketCaptureManager Manager { get; }

		private static string NewTraceId() => string.Concat("chain:", Interlocked.Increment(ref TraceCounter).ToString("D8", CultureInfo.InvariantCulture));

		/// <summary>Tests whether an HTTP content is a long-lived / potentially-infinite stream (a gRPC duplex body or a Server-Sent Events feed) whose body must be passed through untouched instead of being mirrored.</summary>
		/// <remarks>Detection is by media type: <c>application/grpc</c> (and its variants, e.g. <c>application/grpc+proto</c>, <c>application/grpc-web</c>) and <c>text/event-stream</c>. These exactly identify the two shapes whose body never ends while the connection is live. Content type alone is sufficient and precise here: every finite response advertises a different type and keeps its complete body capture, whereas a structural signal such as "no <c>Content-Length</c>" would also match finite chunked responses and silently drop their bodies from the capture.</remarks>
		private static bool IsStreamingContent(HttpContent content)
		{
			var mediaType = content.Headers.ContentType?.MediaType;
			return mediaType is not null
			    && (mediaType.StartsWith("application/grpc", StringComparison.OrdinalIgnoreCase)
			     || mediaType.Equals("text/event-stream", StringComparison.OrdinalIgnoreCase));
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var manager = this.Manager;
			var clock = manager.Clock;
			var fields = manager.Options.AllowedFields;

			// mark the request as "capture rides the chain" so the filter (driven by the BetterHttpClient send extension)
			// stands down: we are the single, canonical capturer for anything flowing through a pooled chain.
			request.Options.Set(RidesChainKey, true);

			var now = clock.GetCurrentInstant();
			var session = new PacketCaptureClientHandlerSession
			{
				TraceIdentifier = NewTraceId(),
				StartedAt = now,
				CreatedAt = now,
				Fields = fields,
				Request = request,
				StackTrace = manager.Options.CaptureStackTraces ? new StackTrace(2).ToString() : null,
			};

			// intercept the request body (the bytes serialized to the wire during the send). A streaming/duplex request body
			// (e.g. gRPC) keeps being written long after the response headers return, so we must NOT wrap it: the interceptor
			// is restored and disposed as soon as the headers arrive (see CompleteRequestCapture), and disposing its mirror
			// while the body is still being pumped would throw and tear the request mid-flight. Such a body is left untouched.
			InterceptedHttpContent? requestInterceptor = null;
			HttpContent? originalRequestContent = null;
			if (request.Content is not null
			 && PacketCaptureClientHandlerSession.CanHaveRequestBody(request.Method)
			 && fields.HasFlag(CapturedHttpFields.RequestBody)
			 && !IsStreamingContent(request.Content))
			{
				originalRequestContent = request.Content;
				requestInterceptor = new InterceptedHttpContent(originalRequestContent, manager.Pool);
				request.Content = requestInterceptor;
			}

			HttpResponseMessage response;
			try
			{
				response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
			}
			catch
			{
				// the request failed before we obtained a response: capture what we can, emit a request-only packet, and rethrow.
				session.ProcessedAt = clock.GetCurrentInstant();
				session.EndedAt = session.ProcessedAt;
				CompleteRequestCapture(session, request, ref requestInterceptor, ref originalRequestContent);
				await manager.Emit(session.GetMetadata(), session.RequestBody, Slice.Nil).ConfigureAwait(false);
				throw;
			}

			session.ProcessedAt = clock.GetCurrentInstant();
			CompleteRequestCapture(session, request, ref requestInterceptor, ref originalRequestContent);

			session.Response = response;

			// A long-lived streaming response (gRPC duplex, Server-Sent Events) stays open for the whole life of the connection:
			// mirroring its body would never complete (so the packet would never emit), grow without bound, and couple this
			// wrapper's disposal to the transport's live stream. Such a response is captured at headers only and its body is
			// passed through UNTOUCHED (no wrap, no disposal interference).
			var streaming = response.Content is not null && IsStreamingContent(response.Content);

			// A finite response body is mirrored and the packet emitted once the caller has consumed it (see
			// CapturingResponseContent). A streaming body - or no capturable body at all - emits the metadata right away, at headers.
			if (!streaming
			 && response.Content is not null
			 && PacketCaptureClientHandlerSession.CanHaveResponseBody(request.Method)
			 && fields.HasFlag(CapturedHttpFields.ResponseBody))
			{
				response.Content = new CapturingResponseContent(response.Content, manager, session);
			}
			else
			{
				session.ResponseStreaming = streaming;
				session.EndedAt = clock.GetCurrentInstant();
				await manager.Emit(session.GetMetadata(), session.RequestBody, Slice.Nil).ConfigureAwait(false);
			}

			return response;
		}

		/// <summary>Restores the original request content and captures the intercepted request body onto the session.</summary>
		private static void CompleteRequestCapture(PacketCaptureClientHandlerSession session, HttpRequestMessage request, ref InterceptedHttpContent? interceptor, ref HttpContent? original)
		{
			if (interceptor is not null)
			{
				request.Content = original;
				if (interceptor.HasCapturedData())
				{
					session.RequestBody = interceptor.GetCapturedData();
				}
				interceptor.Dispose();
				interceptor = null;
				original = null;
			}
		}

		/// <summary>Response content wrapper that mirrors the response body (via <see cref="InterceptedHttpContent"/>) and emits the captured packet exactly once, as soon as the body has been consumed: on full serialization (buffered reads), on read-stream disposal (streaming reads), or on content disposal (fallback).</summary>
		private sealed class CapturingResponseContent : InterceptedHttpContent
		{

			public CapturingResponseContent(HttpContent inner, PacketCaptureManager manager, PacketCaptureClientHandlerSession session)
				: base(inner, manager.Pool)
			{
				this.InnerContent = inner;
				this.Manager = manager;
				this.Session = session;
			}

			/// <summary>The wrapped inner content, kept so we can dispose it (releasing any lifetime it holds, e.g. the virtual transport's linked token source) when this wrapper is disposed.</summary>
			private HttpContent InnerContent { get; }

			private PacketCaptureManager Manager { get; }

			private PacketCaptureClientHandlerSession Session { get; }

			private int Emitted;

			/// <summary>Captures the response body (whatever has been mirrored so far) and emits the packet, at most once.</summary>
			private void EmitOnce()
			{
				if (Interlocked.Exchange(ref this.Emitted, 1) != 0) return;

				this.Session.EndedAt = this.Manager.Clock.GetCurrentInstant();
				if (this.HasCapturedData())
				{
					this.Session.ResponseBody = this.GetCapturedData();
				}
				// Emit writes to the manager's unbounded channel, so it completes synchronously; nothing here can await.
				_ = this.Manager.Emit(this.Session.GetMetadata(), this.Session.RequestBody, this.Session.ResponseBody);
			}

			// Buffered reads (ReadAsByteArray/ReadAsString/CopyTo/LoadIntoBuffer) serialize the whole content in one pass.
			protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
			{
				await base.SerializeToStreamAsync(stream, context, cancellationToken).ConfigureAwait(false);
				EmitOnce();
			}

			protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
			{
				await base.SerializeToStreamAsync(stream, context).ConfigureAwait(false);
				EmitOnce();
			}

			protected override void SerializeToStream(Stream stream, TransportContext? context, CancellationToken cancellationToken)
			{
				base.SerializeToStream(stream, context, cancellationToken);
				EmitOnce();
			}

			// Streaming reads (ReadAsStream, and the convenience GetStringAsync/GetByteArrayAsync which read the content stream and
			// dispose it, not the response) - emit when the read-stream is disposed.
			protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
				=> new EmitOnDisposeStream(await base.CreateContentReadStreamAsync(cancellationToken).ConfigureAwait(false), EmitOnce);

			protected override async Task<Stream> CreateContentReadStreamAsync()
				=> new EmitOnDisposeStream(await base.CreateContentReadStreamAsync().ConfigureAwait(false), EmitOnce);

			protected override Stream CreateContentReadStream(CancellationToken cancellationToken)
				=> new EmitOnDisposeStream(base.CreateContentReadStream(cancellationToken), EmitOnce);

			protected override void Dispose(bool disposing)
			{
				if (disposing)
				{
					EmitOnce();
				}
				base.Dispose(disposing);
				if (disposing)
				{
					// forward disposal to the wrapped content so it can release its own lifetime (InterceptedHttpContent never disposes its inner).
					this.InnerContent.Dispose();
				}
			}

		}

		/// <summary>Read-only stream that forwards to an inner stream and fires a callback the first time it is disposed (used to detect that a streaming response body has been consumed).</summary>
		private sealed class EmitOnDisposeStream : Stream
		{

			public EmitOnDisposeStream(Stream inner, Action onDisposed)
			{
				this.Inner = inner;
				this.OnDisposed = onDisposed;
			}

			private Stream Inner { get; }

			private Action OnDisposed { get; }

			protected override void Dispose(bool disposing)
			{
				if (disposing)
				{
					this.OnDisposed();
					this.Inner.Dispose();
				}
				base.Dispose(disposing);
			}

			public override async ValueTask DisposeAsync()
			{
				this.OnDisposed();
				await this.Inner.DisposeAsync().ConfigureAwait(false);
			}

			public override bool CanRead => this.Inner.CanRead;

			public override bool CanSeek => false;

			public override bool CanWrite => false;

			public override long Length => this.Inner.Length;

			public override long Position { get => this.Inner.Position; set => throw new NotSupportedException(); }

			public override void Flush() { }

			public override int Read(byte[] buffer, int offset, int count) => this.Inner.Read(buffer, offset, count);

			public override int Read(Span<byte> buffer) => this.Inner.Read(buffer);

			public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => this.Inner.ReadAsync(buffer, offset, count, cancellationToken);

			public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => this.Inner.ReadAsync(buffer, cancellationToken);

			public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

			public override void SetLength(long value) => throw new NotSupportedException();

			public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

		}

	}

}
