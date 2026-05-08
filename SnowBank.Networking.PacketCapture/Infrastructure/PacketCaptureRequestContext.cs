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
	using System.Net;
	using Microsoft.AspNetCore.Http.Features;

	/// <summary>Represents a capture session over the lifetime of a unique HTTP request</summary>
	public sealed record PacketCaptureRequestContext
	{

		/// <summary>Capture context of the parent connection</summary>
		public PacketCaptureConnectionContext Connection { get; }

		/// <summary>Defines the properties on the request that will be captured</summary>
		public CapturedHttpFields Fields { get; }

		/// <summary>Moment when the request was started</summary>
		public Instant StartedAt { get; }

		/// <summary>Moment when the request was processed (but not completed)</summary>
		public Instant? ProcessedAt { get; internal set; }

		/// <summary>Moment when the request was completed</summary>
		public Instant? EndedAt { get; internal set; }

		/// <summary>HTTP Context of the current request</summary>
		public HttpContext HttpContext { get; }

		/// <summary>Original request body stream</summary>
		internal Stream RequestBodyOriginal { get; }

		/// <summary>Original response body stream</summary>
		internal Stream ResponseBodyOriginal { get; }

		/// <summary>Interceptor that captures the request body stream</summary>
		internal InputInterceptorStream? RequestInterceptor { get; set; }

		internal MemoryStream? RequestBodyMirror { get; }

		/// <summary>Interceptor that captures the response body stream</summary>
		internal OutputInterceptorStream? ResponseInterceptor { get; set; }

		internal MemoryStream? ResponseBodyMirror { get; set; }

		internal int CompletedMessages;

		internal PacketCaptureRequestContext(PacketCaptureConnectionContext connection, CapturedHttpFields fields, Instant startedAt, HttpContext httpContext, Stream requestBodyOriginal, InputInterceptorStream? requestInterceptor, MemoryStream? requestBodyMirror, Stream responseBodyOriginal, OutputInterceptorStream? responseInterceptor, MemoryStream? responseBodyMirror)
		{
			this.Connection = connection;
			this.Fields = fields;
			this.StartedAt = startedAt;
			this.HttpContext = httpContext;
			this.RequestBodyOriginal = requestBodyOriginal;
			this.RequestInterceptor = requestInterceptor;
			this.RequestBodyMirror = requestBodyMirror;
			this.ResponseBodyOriginal = responseBodyOriginal;
			this.ResponseInterceptor = responseInterceptor;
			this.ResponseBodyMirror = responseBodyMirror;
		}

		public CapturedPacketMetadata GetMetadata()
		{
			var traceId = this.HttpContext.TraceIdentifier;
			if (this.CompletedMessages > 0) traceId += $":{this.CompletedMessages:D8}";
			return new()
			{
				TraceId = traceId,
				Reconstructed = true,
				Fields = this.Fields,
				Role = CapturedPacketMetadata.ROLE_SERVER,
				StartedAt = this.StartedAt,
				ProcessedAt = this.ProcessedAt,
				EndedAt = this.EndedAt,
				Connection = BuildConnectionInfo(),
				Request = BuildRequestInfo(),
				Response = BuildResponseInfo(),
			};
		}

		public Slice GetRequestBody() => (this.Fields.HasFlag(CapturedHttpFields.RequestBody) && this.RequestBodyMirror != null) ? this.RequestBodyMirror.ToArray().AsSlice() : default;

		public Slice GetResponseBody() => (this.Fields.HasFlag(CapturedHttpFields.ResponseBody) && this.ResponseBodyMirror != null) ? this.ResponseBodyMirror.ToArray().AsSlice() : default;

		private CapturedPacketMetadata.ConnectionInfo BuildConnectionInfo()
		{
			var cnx = this.Connection;
			return new()
			{
				Id = cnx.Context.ConnectionId,
				StartedAt = this.Connection.StartedAt,
				RemoteHost = cnx.RemoteHost,
				RemotePort = cnx.RemotePort,
				LocalHost = cnx.LocalHost,
				LocalPort = cnx.LocalPort,
			};
		}

		private CapturedPacketMetadata.RequestInfo BuildRequestInfo()
		{
			var fields = this.Fields;

			var req = this.HttpContext.Request;
			var requestInfo = new CapturedPacketMetadata.RequestInfo
			{
				Scheme = fields.HasFlag(CapturedHttpFields.RequestScheme) ? req.Scheme : null,
				Protocol = fields.HasFlag(CapturedHttpFields.RequestProtocol) ? req.Protocol : null,
				Method = fields.HasFlag(CapturedHttpFields.RequestMethod) ? req.Method : null,
				Path = fields.HasFlag(CapturedHttpFields.RequestPath) ? req.Path.ToString() : null,
				QueryString = fields.HasFlag(CapturedHttpFields.RequestQuery) ? (req.QueryString.HasValue ? req.QueryString.ToString() : string.Empty) : null,
				Headers = fields.HasFlag(CapturedHttpFields.RequestHeaders) ? CapturedHttpHeaders.Create(req.Headers) : CapturedHttpHeaders.Empty,
				HasBody = fields.HasFlag(CapturedHttpFields.RequestBody) && this.RequestBodyMirror != null,
			};

			//TODO: BUGBUG: RequestTrailers!

			return requestInfo;
		}

		private CapturedPacketMetadata.ResponseInfo BuildResponseInfo()
		{
			var fields = this.Fields;

			var res = this.HttpContext.Features.Get<IHttpResponseFeature>()!;
			var responseInfo = new CapturedPacketMetadata.ResponseInfo
			{
				Status = fields.HasFlag(CapturedHttpFields.ResponseStatusCode) ? res.StatusCode : 0,
				ReasonPhrase = fields.HasFlag(CapturedHttpFields.ResponseStatusCode) ? (res.ReasonPhrase ?? ((HttpStatusCode) res.StatusCode).ToString()) : null,
				Headers = fields.HasFlag(CapturedHttpFields.ResponseHeaders) ? CapturedHttpHeaders.Create(res.Headers) : CapturedHttpHeaders.Empty,
				HasBody = fields.HasFlag(CapturedHttpFields.ResponseBody) && this.ResponseBodyMirror != null,
			};

			//TODO: BUGBUG: ResponseTrailers!

			return responseInfo;
		}

	}

}
