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
	using Microsoft.IO;

	/// <summary>Options for configuring <see cref="PacketCaptureManager"/></summary>
	public sealed record PacketCaptureOptions
	{

		public const string DefaultAssetsPath = "./PacketCapture/Viewer/wwwroot";

		public bool Enabled { get; set; }

		public string? ActorId { get; set; }

		public IPacketCapturePolicy CapturePolicy { get; set; } = PacketCapturePolicies.None;

		public CapturedHttpFields AllowedFields { get; set; } = CapturedHttpFields.All;

		public List<IPacketCaptureSink> Sinks { get; set; } = [ ];

		public bool AddAmbientSinks { get; set; }

		// hooks

		public Func<PacketCaptureConnectionContext, ValueTask>? OnConnectionStarted { get; set; }

		public Func<PacketCaptureConnectionContext, ValueTask>? OnConnectionCompleted { get; set; }

		public Func<PacketCaptureConnectionContext, ValueTask>? OnRequestCaptureStarted { get; set; }

		public Func<PacketCaptureConnectionContext, ValueTask>? OnRequestCaptureCompleted { get; set; }

		public RecyclableMemoryStreamManager? StreamPool { get; set; }

		public string? AssetsPath { get; set; }

		public TimeSpan? ContentExpiration { get; set; } = TimeSpan.FromHours(1);

		public bool CaptureStackTraces { get; set; }

	}

	/// <summary>List of HTTP fields that can be captured</summary>
	[Flags]
	public enum CapturedHttpFields
	{
		//note: this is a copy/paste from HttpLoggingFields

		/// <summary>No logging.</summary>
		None = 0x0,

		/// <summary>Flag for logging the HTTP Request Path, which includes both the <see cref="Microsoft.AspNetCore.Http.HttpRequest.Path"/> and <see cref="Microsoft.AspNetCore.Http.HttpRequest.PathBase"/>.
		/// <p>
		/// For example:
		/// Path: /index
		/// PathBase: /app
		/// </p>
		/// </summary>
		RequestPath = 0x1,

		/// <summary>Flag for logging the HTTP Request <see cref="Microsoft.AspNetCore.Http.HttpRequest.QueryString"/>.
		/// <p>
		/// For example:
		/// Query: ?index=1
		/// </p>
		/// </summary>
		RequestQuery = 0x2,

		/// <summary>Flag for logging the HTTP Request <see cref="Microsoft.AspNetCore.Http.HttpRequest.Protocol"/>.
		/// <p>
		/// For example:
		/// Protocol: HTTP/1.1
		/// </p>
		/// </summary>
		RequestProtocol = 0x4,

		/// <summary>Flag for logging the HTTP Request <see cref="Microsoft.AspNetCore.Http.HttpRequest.Method"/>.
		/// <p>
		/// For example:
		/// Method: GET
		/// </p>
		/// </summary>
		RequestMethod = 0x8,

		/// <summary>Flag for logging the HTTP Request <see cref="Microsoft.AspNetCore.Http.HttpRequest.Scheme"/>.
		/// <p>
		/// For example:
		/// Scheme: https
		/// </p>
		/// </summary>
		RequestScheme = 0x10,

		/// <summary>Flag for logging the HTTP Response <see cref="Microsoft.AspNetCore.Http.HttpResponse.StatusCode"/>.
		/// <p>
		/// For example:
		/// StatusCode: 200
		/// </p>
		/// </summary>
		ResponseStatusCode = 0x20,

		/// <summary>
		/// Flag for logging the HTTP Request <see cref="Microsoft.AspNetCore.Http.HttpRequest.Headers"/>.
		/// Request Headers are logged as soon as the middleware is invoked.
		/// Headers are redacted by default with the character '[Redacted]' unless specified in
		/// the <see cref="Microsoft.AspNetCore.Http.Headers.RequestHeaders"/>.
		/// <p>
		/// For example:
		/// Connection: keep-alive
		/// My-Custom-Request-Header: [Redacted]
		/// </p>
		/// </summary>
		RequestHeaders = 0x40,

		/// <summary>
		/// Flag for logging the HTTP Response <see cref="Microsoft.AspNetCore.Http.HttpResponse.Headers"/>.
		/// Response Headers are logged when the <see cref="Microsoft.AspNetCore.Http.HttpResponse.Body"/> is written to
		/// or when <see cref="Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature.StartAsync(System.Threading.CancellationToken)"/>
		/// is called.
		/// Headers are redacted by default with the character '[Redacted]' unless specified in
		/// the <see cref="Microsoft.AspNetCore.Http.Headers.ResponseHeaders"/>.
		/// <p>
		/// For example:
		/// Content-Length: 16
		/// My-Custom-Response-Header: [Redacted]
		/// </p>
		/// </summary>
		ResponseHeaders = 0x80,

		/// <summary>Flag for logging the HTTP Request <see cref="Microsoft.AspNetCore.Http.Features.IHttpRequestTrailersFeature.Trailers"/>.</summary>
		/// <remarks>Request Trailers are currently not logged.</remarks>
		RequestTrailers = 0x100,

		/// <summary>Flag for logging the HTTP Response <see cref="Microsoft.AspNetCore.Http.Features.IHttpResponseTrailersFeature.Trailers"/>.</summary>
		ResponseTrailers = 0x200,

		/// <summary>Flag for logging the HTTP Request <see cref="Microsoft.AspNetCore.Http.HttpRequest.Body"/>. </summary>
		/// <remarks>Logging the request body has performance implications, as it requires buffering the entire request body.</remarks>
		RequestBody = 0x400,

		/// <summary>Flag for logging the HTTP Response <see cref="Microsoft.AspNetCore.Http.HttpResponse.Body"/>.</summary>
		/// <remarks>Logging the response body has performance implications, as it requires buffering the entire response body.</remarks>
		ResponseBody = 0x800,

		/// <summary>Flag for logging a collection of HTTP Request properties, including <see cref="RequestPath"/>, <see cref="RequestQuery"/>, <see cref="RequestProtocol"/>, <see cref="RequestMethod"/>, and <see cref="RequestScheme"/>.</summary>
		RequestProperties = RequestPath | RequestQuery | RequestProtocol | RequestMethod | RequestScheme,

		/// <summary>Flag for logging HTTP Request properties and headers. Includes <see cref="RequestProperties"/> and <see cref="RequestHeaders"/></summary>
		RequestPropertiesAndHeaders = RequestProperties | RequestHeaders,

		/// <summary>Flag for logging HTTP Response properties and headers. Includes <see cref="ResponseStatusCode"/> and <see cref="ResponseHeaders"/></summary>
		ResponsePropertiesAndHeaders = ResponseStatusCode | ResponseHeaders,

		/// <summary>Flag for logging the entire HTTP Request. Includes <see cref="RequestPropertiesAndHeaders"/> and <see cref="RequestBody"/>.</summary>
		/// <remarks>Logging the request body has performance implications, as it requires buffering the entire request body.</remarks>
		Request = RequestPropertiesAndHeaders | RequestBody,

		/// <summary>Flag for logging the entire HTTP Response. Includes <see cref="ResponsePropertiesAndHeaders"/> and <see cref="ResponseBody"/>.</summary>
		/// <remarks>Logging the response body has performance implications, as it requires buffering the entire response body.</remarks>
		Response = ResponseStatusCode | ResponseHeaders | ResponseBody,

		/// <summary>Flag for logging both the HTTP Request and Response. Includes <see cref="Request"/> and <see cref="Response"/>.</summary>
		/// <remarks>Logging the request and response body has performance implications, as it requires buffering the entire request and response bodies.</remarks>
		All = Request | Response,

	}

}
