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
	using System.Runtime.ExceptionServices;
	using Grpc.AspNetCore.Server;
	using Grpc.Core;
	using Microsoft.AspNetCore.Connections;
	using Microsoft.AspNetCore.Http.Features;

	/// <summary>Represents a capture session over the lifetime of an HTTP connection</summary>
	public sealed class PacketCaptureConnectionContext : IDisposable
	{

		public ConnectionContext Context { get; }

		public PacketCaptureManager Manager { get; }

		public NodaTime.Instant StartedAt { get; }

		public NodaTime.Instant? CompletedAt { get; internal set; }

		public string RemoteHost { get; }

		public int? RemotePort { get; }

		public string LocalHost { get; }

		public int? LocalPort { get; }

		private int StartedRequests;

		private int CompletedRequests;

		public PacketCaptureConnectionContext(PacketCaptureManager manager, ConnectionContext context, NodaTime.Instant startedAt, string remoteHost, int? remotePort, string localHost, int? localPort)
		{
			this.Context = context;
			this.Manager = manager;
			this.StartedAt = startedAt;
			this.RemoteHost = remoteHost;
			this.RemotePort = remotePort;
			this.LocalHost = localHost;
			this.LocalPort = localPort;
		}

		public void Dispose()
		{
			this.CompletedAt = this.Manager.Clock.GetCurrentInstant();
		}

		public CapturedHttpFields ShouldCapture(HttpContext context)
		{
			//note: the manager already does the try/catch!
			return this.Manager.ShouldCaptureRequest(context);
		}

		public async ValueTask BeginRequest(HttpContext context, CapturedHttpFields fields)
		{
			try
			{
				var manager = this.Manager;

				var requestBodyOriginal = context.Request.Body;
				var requestBodyMirror = manager.Pool.GetStream();
				var requestInterceptor = new InputInterceptorStream(requestBodyOriginal, requestBodyMirror);

				var responseBodyOriginal = context.Response.Body;
				var responseBodyMirror = manager.Pool.GetStream();

				OutputInterceptorStream responseInterceptor;
				// REVIEW : TODO : Check how WebSocket works, we may have to go through the same mechanism
				// But a different one from gRPC because the first 5 bytes of gRPC are the message size, which is an implementation detail of the protocol and not common with WebSocket
				if (context.Request.ContentType == "application/grpc")
				{
					// If we are gRPC then we delegate the responsibility of emitting a PacketCapture to the stream's Flush (and not to CompleteRequest)
					// The goal being to have the Stream messages in distinct packets, so that we see them appear as they come and not only when
					// the HTTP request completes
					// Moreover, this allows us to not clutter the memory with packets in RAM until the connection completes
					// Because in the case of the ClientWindows, the REGISTER request never completes per session!
					
					var isStreaming = context.Features.Get<IEndpointFeature>()?.Endpoint?.Metadata.GetMetadata<GrpcMethodMetadata>()?.Method.Type != MethodType.Unary;
					if (isStreaming)
					{
						// Streaming of messages, we need to emit a packet on each flush
						responseInterceptor = new GrpcOutputInterceptorStream(responseBodyOriginal, responseBodyMirror, context);
					}
					else
					{
						// If no streaming, then we keep the basic interceptor (a single message per request)
						responseInterceptor = new StandardOutputInterceptorStream(responseBodyOriginal, responseBodyMirror);
					}
				}
				else
				{
					// For everything else, we use the basic interceptor
					responseInterceptor = new StandardOutputInterceptorStream(responseBodyOriginal, responseBodyMirror);
				}

				var requestContext = new PacketCaptureRequestContext(
					connection: this,
					fields,
					startedAt: manager.Clock.GetCurrentInstant(),
					context,
					requestBodyOriginal,
					requestInterceptor,
					requestBodyMirror,
					responseBodyOriginal,
					responseInterceptor,
					responseBodyMirror
				);

				context.Features.Set(requestContext);

				if (responseInterceptor is GrpcOutputInterceptorStream gRpcOutputInterceptorStream)
				{
					gRpcOutputInterceptorStream.OnMessageWritten = () => CompleteMessage(requestContext);
				}
				
				Interlocked.Increment(ref this.StartedRequests);
				context.Response.OnCompleted(CompleteRequest, requestContext);

				context.Request.Body = requestInterceptor;
				context.Response.Body = responseInterceptor;

				if (this.Manager.Options.OnRequestCaptureStarted is { } onStarted)
				{
					await onStarted(requestContext);
				}
			}
			catch (Exception e)
			{
				context.Features.Set(default(PacketCaptureRequestContext));
				this.Manager.ReportCaptureError(context, ExceptionDispatchInfo.Capture(e));
				// note: do not bubble up the error to the caller!
			}

			static async Task CompleteRequest(object state)
			{
				var requestContext = (PacketCaptureRequestContext) state;
				requestContext.EndedAt = requestContext.Connection.Manager.Clock.GetCurrentInstant();

				Interlocked.Increment(ref requestContext.Connection.CompletedRequests);
				var httpContext = requestContext.HttpContext;
				try
				{
					// restore request to its original configuration
					httpContext.Features.Set(default(PacketCaptureRequestContext));
					httpContext.Request.Body = requestContext.RequestBodyOriginal;
					httpContext.Response.Body = requestContext.ResponseBodyOriginal;

					// The gRPC Interceptor works on the basis of messages and not requests
					// Because we cannot distinguish a stream from a single, so the single behaves the same way as the stream
					// As soon as a complete message is written to the stream, we send the packet to the manager
					var isGrpc = requestContext.ResponseInterceptor is GrpcOutputInterceptorStream;

					//note: we must NOT dispose the inner streams ourselves! (only if someone down the line explicitly called Disposed() on the streams!)
					requestContext.RequestInterceptor = null;
					requestContext.ResponseInterceptor = null;

					CapturedPacketMetadata metadata = requestContext.GetMetadata();
					//TODO: keep the stream?
					Slice requestBody = requestContext.GetRequestBody();
					Slice responseBody = requestContext.GetResponseBody();

					if (!isGrpc)
					{
						// If we are not a GrpcOutputInterceptorStream then we need to emit the request packet on complete
						await requestContext.Connection.Manager.Emit(metadata, requestBody, responseBody);
					}
					else
					{
						// If we are gRPC, and the Request Header contains TE:Trailers and we are in Streaming mode, then we must emit a packet with just the Trailers
						var isStreaming = requestContext.HttpContext.Features.Get<IEndpointFeature>()?.Endpoint?.Metadata.GetMetadata<GrpcMethodMetadata>()?.Method.Type != MethodType.Unary;
						if (isStreaming && requestContext.HttpContext.Request.Headers.TE == "trailers")
						{
							Interlocked.Increment(ref requestContext.CompletedMessages);
							// We need to emit the end-of-request trailers
							await requestContext.Connection.Manager.Emit(metadata, requestBody, responseBody);
						}
					}
					
					// return the intercepted streams to the pool!
					//REVIEW: if we decide to keep the streams in the store, it will no longer be up to us to dispose them!
					requestContext.RequestBodyMirror?.Dispose();
					requestContext.ResponseBodyMirror?.Dispose();

					if (requestContext.Connection.Manager.Options.OnRequestCaptureCompleted is { } onCompleted)
					{
						await onCompleted(requestContext);
					}
				}
				catch (Exception e)
				{
					requestContext.Connection.Manager.ReportCaptureError(requestContext.HttpContext, ExceptionDispatchInfo.Capture(e));
					//note: do not bubble up the error to the caller!
				}
			}

			async Task CompleteMessage(PacketCaptureRequestContext context)
			{
				Interlocked.Increment(ref context.CompletedMessages);
				try
				{
					CapturedPacketMetadata metadata = context.GetMetadata();

					// TODO : Change the MetaData.Response.Headers.Date to the current date and not the date of the first response

					//TODO: keep the stream?
					Slice requestBody = context.GetRequestBody();
					Slice responseBody = context.GetResponseBody();

					// We create a packet always with the same request but the response body is supposed to contain only the last gRPC message sent
					await context.Connection.Manager.Emit(metadata, requestBody, responseBody);
				}
				catch (Exception e)
				{
					context.Connection.Manager.ReportCaptureError(context.HttpContext, ExceptionDispatchInfo.Capture(e));
					//note: do not bubble up the error to the caller!
				}
			}
		}

		public void EndRequest(HttpContext context)
		{
			//note: this is called by the inner middleware when all downstream middlewares have done their job
			// this is NOT the end of the request, though! There are more operations that could be done!
			// => the real end of the request is when the "Response.OnCompleted" event fires

			var requestSession = context.Features.Get<PacketCaptureRequestContext?>();
			if (requestSession != null)
			{
				requestSession.ProcessedAt = this.Manager.Clock.GetCurrentInstant();
			}
		}
	}
}
