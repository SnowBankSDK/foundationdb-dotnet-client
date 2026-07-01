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
			//note: le manager fait deja les try/catch!
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
				// REVIEW : TODO : Vérifier le fonctionnement de WebSocket, il faudra peut-être passer par le même mécanisme
				// Mais un différent de gRPC car les 5 premiers octets de gRPC sont la taille du message, et c'est donc un détail d'implémentation du protocole et pas commun avec WebSocket
				if (context.Request.ContentType == "application/grpc")
				{
					// Si on est gRPC alors on va déléguer au Flush du stream la responsabilité de Emit un PacketCapture (et non au CompleteRequest)
					// Le but, étant d'avoir les Stream messages en packets distincts, histoire de les voirs apparaitre au fil de l'eau et non uniquement lorsque
					// la requête HTTP se termine
					// De plus, ca nous permettra de ne pas encombrer la mémoire avec des packets en RAM jusqu'a ce que la connexion se termine
					// Car dans le cas du ClientWindows, la requête REGISTER ne se termine jamais par session!
					
					var isStreaming = context.Features.Get<IEndpointFeature>()?.Endpoint?.Metadata.GetMetadata<GrpcMethodMetadata>()?.Method.Type != MethodType.Unary;
					if (isStreaming)
					{
						// Streaming de messages, besoin d'emit un packet a chaque flush
						responseInterceptor = new GrpcOutputInterceptorStream(responseBodyOriginal, responseBodyMirror, context);
					}
					else
					{
						// Si pas de streaming, alors on laisse l'intercepteur basique (1 seul message par requete)
						responseInterceptor = new StandardOutputInterceptorStream(responseBodyOriginal, responseBodyMirror);
					}
				}
				else
				{
					// Pour tout le reste, alors on utilise l'interceptor basique
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

					// Le gRPC Interceptor fonctionne sur la base de messages et non de requêtes
					// Car on ne peut pas distinguer un stream d'un single, donc le single a le même fonctionnement que le stream
					// Des qu'un message complet est écrit sur le stream, alors on envoie le packet au manager
					var isGrpc = requestContext.ResponseInterceptor is GrpcOutputInterceptorStream;

					//note: we must NOT dispose the inner streams ourselves! (only if someone down the line explicitly called Disposed() on the streams!)
					requestContext.RequestInterceptor = null;
					requestContext.ResponseInterceptor = null;

					CapturedPacketMetadata metadata = requestContext.GetMetadata();
					//TODO: conserver le stream?
					Slice requestBody = requestContext.GetRequestBody();
					Slice responseBody = requestContext.GetResponseBody();

					if (!isGrpc)
					{
						// Si on est pas un GrpcOutputInterceptorStream alors besoin d'emettre le packet de la requête lors du complete
						await requestContext.Connection.Manager.Emit(metadata, requestBody, responseBody);
					}
					else
					{
						// Si on est gRPC, et que le Header de la Request contient TE:Trailers et qu'on est en mode Streaming, alors il faut emit un packet avec juste les Trailers
						var isStreaming = requestContext.HttpContext.Features.Get<IEndpointFeature>()?.Endpoint?.Metadata.GetMetadata<GrpcMethodMetadata>()?.Method.Type != MethodType.Unary;
						if (isStreaming && requestContext.HttpContext.Request.Headers.TE == "trailers")
						{
							Interlocked.Increment(ref requestContext.CompletedMessages);
							// On a besoin d'emit les trailers de fin de requête
							await requestContext.Connection.Manager.Emit(metadata, requestBody, responseBody);
						}
					}
					
					// return the intercepted streams to the pool!
					//REVIEW: si on décide de garder les streams dans le store, ca ne sera plus a nous de les dispose!
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

					// TODO : Modifier le MetaData.Response.Headers.Date a la date actuelle et non la date de la première réponse
					
					//TODO: conserver le stream?
					Slice requestBody = context.GetRequestBody();
					Slice responseBody = context.GetResponseBody();

					// On crée un packet avec toujours la même request mais le response body est censé contenir uniquement le dernier message gRPC envoyé
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
