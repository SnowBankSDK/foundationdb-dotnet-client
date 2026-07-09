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
	using System.Net.Http;
	using System.Net.Http.Headers;
	using SnowBank.Networking.Http;

	/// <summary>Holds the state of an HTTP request being captured</summary>
	internal sealed record PacketCaptureClientHandlerSession
	{

		/// <summary>Trace identifier of this request</summary>
		/// <remarks>
		/// <para>This identifier is internal to the client and offers no strong guarantees on uniqueness or ordering.</para>
		/// <para>It should only be used as a sort of Correlation ID between application logs and the captured packets.</para>
		/// </remarks>
		public required string TraceIdentifier { get; set; }

		/// <summary>Time at which the capture session was started</summary>
		public required Instant StartedAt { get; set; }

		/// <summary>Time at which the request context was created</summary>
		public required Instant CreatedAt { get; set; }

		/// <summary>Time at which the response headers were received</summary>
		public Instant? ProcessedAt { get; set; }

		/// <summary>Time at which the response was fully consumed by the client</summary>
		/// <remarks>Includes the execution time of the request handler.</remarks>
		public Instant? EndedAt { get; set; }

		/// <summary>Describes all the attributes of the query that should be captured for this particular session</summary>
		public required CapturedHttpFields Fields { get; set; }

		/// <summary>Holds the original <see cref="HttpRequestMessage">request message</see> that was sent to the server</summary>
		public HttpRequestMessage? Request { get; set; }

		/// <summary>If eligible, contains the captured body of the request that was sent to the server</summary>
		/// <remarks>If <see cref="Slice.Nil"/>, it means either means that the method does not expect a request body (ex: GET, DELETE, ...), OR that capture of the request body was not enabled in <see cref="Fields"/>.</remarks>
		public Slice RequestBody { get; set; }

		/// <summary>Holds the original <see cref="HttpResponseMessage">response message</see> that was received from the server</summary>
		public HttpResponseMessage? Response { get; set; }

		/// <summary>If eligible, contains the captured body of the response that was received from the server</summary>
		/// <remarks>If <see cref="Slice.Nil"/>, it means either means that the method does not expect a response body (ex: PUT, ...), OR that capture of the response body was not enabled in <see cref="Fields"/>.</remarks>
		public Slice ResponseBody { get; set; }

		public string? StackTrace { get; set; }

		internal static bool CanHaveRequestBody(HttpMethod method) => method.Method switch
		{
			"POST"  => true,
			"PUT"   => true,
			"PATCH" => true,
			_       => false,
		};

		internal static bool CanHaveResponseBody(HttpMethod method) => method.Method switch
		{
			"GET"     => true,
			"POST"    => true,
			"OPTIONS" => true,
			_         => false,
		};

		public CapturedPacketMetadata GetMetadata()
		{
			var fields = this.Fields;

			var req = this.Request ?? throw new InvalidOperationException("Request was not prepared correctly on this capture session.");

			var connectionInfo = new CapturedPacketMetadata.ConnectionInfo()
			{
				Id = this.TraceIdentifier,
				StartedAt = this.CreatedAt,
				RemoteHost = req.RequestUri?.DnsSafeHost ?? string.Empty,
				RemotePort = req.RequestUri?.Port ?? 0,
				//TODO: local peers?
			};
			var requestInfo = new CapturedPacketMetadata.RequestInfo()
			{
				Method = req.Method.Method,
				Path = req.RequestUri?.AbsolutePath,
				QueryString = req.RequestUri?.Query,
				Scheme = req.RequestUri?.Scheme,
				Protocol = null, //BUGBUG: TODO: what should we do here? copy the protocol from the response?
				Headers = fields.HasFlag(CapturedHttpFields.RequestHeaders) ? CloneRequestHeaders(req.Headers, req.Content?.Headers) : CapturedHttpHeaders.Empty,
				HasBody = !this.RequestBody.IsNull,
			};

			CapturedPacketMetadata.ResponseInfo responseInfo;
			var res = this.Response;
			if (res is not null)
			{ // we have a response
				responseInfo = new ()
				{
					Status = (int) res.StatusCode,
					ReasonPhrase = res.ReasonPhrase,
					Headers = fields.HasFlag(CapturedHttpFields.ResponseHeaders) ? CloneResponseHeaders(res.Headers, res.Content.Headers) : CapturedHttpHeaders.Empty,
					HasBody = !this.ResponseBody.IsNull,
				};
			}
			else
			{ // the server crashed before producing a response
				responseInfo = new ()
				{
					//TODO: BUGBUG: what should do here?
					Status = 0,
					Headers = new (),
				};
			}

			//REVIEW: TODO: if an exception has been captured, should we also inject it into the metadata?

			return new()
			{
				TraceId = this.TraceIdentifier,
				Role = CapturedPacketMetadata.ROLE_CLIENT,
				StartedAt = this.StartedAt,
				ProcessedAt = this.ProcessedAt,
				EndedAt = this.EndedAt,
				Fields = fields,
				Uri = req.RequestUri?.OriginalString ?? string.Empty,
				Connection = connectionInfo,
				Request = requestInfo,
				Response = responseInfo,
				StackTrace = this.StackTrace,
			};
		}

		internal static CapturedHttpHeaders CloneRequestHeaders(HttpRequestHeaders headers, HttpContentHeaders? content)
		{
			var builder = CapturedHttpHeaders.CreateBuilder();
			foreach (var kv in headers)
			{
				builder.AddValues(kv.Key, kv.Value);
			}
			if (content != null)
			{
				foreach (var kv in content)
				{
					builder.AddValues(kv.Key, kv.Value);
				}
			}
			return builder.ToHeaders();
		}

		internal static CapturedHttpHeaders CloneResponseHeaders(HttpResponseHeaders headers, HttpContentHeaders? content)
		{
			var builder = CapturedHttpHeaders.CreateBuilder();
			foreach (var kv in headers)
			{
				builder.AddValues(kv.Key, kv.Value);
			}
			if (content != null)
			{
				foreach (var kv in content)
				{
					builder.AddValues(kv.Key, kv.Value);
				}
			}
			return builder.ToHeaders();
		}

	}

}
