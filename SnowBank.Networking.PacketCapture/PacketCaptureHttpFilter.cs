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
	using SnowBank.Networking.Http;

	/// <summary>Filter that capturing all the queries sent by a <see cref="BetterHttpClient"/> to the remote server</summary>
	public class PacketCaptureHttpFilter : IBetterHttpFilter
	{

		private const string NAME = "PacketCapture";

		/// <inheritdoc/>
		public string Name => NAME;

		public PacketCaptureHttpFilter(PacketCaptureManager manager)
		{
			this.Manager = manager;
		}

		public PacketCaptureManager Manager { get; }

		private sealed record RequestState
		{
			public required PacketCaptureClientHandlerSession Session { get; init; }

			public HttpContent? Original { get; set; }

			public InterceptedHttpContent? Intercepted { get; set; }

		}

		/// <inheritdoc/>
		public ValueTask Configure(BetterHttpClientContext context)
		{
			Contract.Debug.Requires(context is not null && context.Id != null);

			var session = new PacketCaptureClientHandlerSession
			{
				StartedAt = this.Manager.Clock.GetCurrentInstant(),
				CreatedAt = context.CreatedAt,
				Fields = this.Manager.Options.AllowedFields,
				TraceIdentifier = context.Id,
				StackTrace = this.Manager.Options.CaptureStackTraces ? new StackTrace(2).ToString() : null,
			};

			var rs = new RequestState()
			{
				Session = session,
			};
			context.State[NAME] = rs;
			return default;
		}

		/// <inheritdoc/>
		public ValueTask PrepareRequest(BetterHttpClientContext context)
		{
			if (!context.TryGetState<RequestState>(NAME, out var rs)) return default;

			rs.Session.Request = context.Request;

			if (PacketCaptureClientHandlerSession.CanHaveRequestBody(context.Request.Method) && rs.Session.Fields.HasFlag(CapturedHttpFields.RequestBody))
			{
				var original = context.Request.Content;
				if (original != null)
				{
					rs.Original = original;
					rs.Intercepted = new(original, this.Manager.Pool);
					context.Request.Content = rs.Intercepted;
				}
			}
			return default;
		}

		/// <inheritdoc/>
		public ValueTask CompleteRequest(BetterHttpClientContext context)
		{
			if (!context.TryGetState<RequestState>(NAME, out var rs)) return default;

			rs.Session.ProcessedAt = this.Manager.Clock.GetCurrentInstant();

			if (rs.Intercepted is not null)
			{
				context.Request.Content = rs.Original;
				if (rs.Intercepted.HasCapturedData())
				{
					rs.Session.RequestBody = rs.Intercepted.GetCapturedData();
				}
				rs.Intercepted.Dispose();
				rs.Intercepted = null;
				rs.Original = null;
			}

			return default;
		}

		/// <inheritdoc/>
		public ValueTask PrepareResponse(BetterHttpClientContext context)
		{
			if (!context.TryGetState<RequestState>(NAME, out var rs)) return default;

			if (context.HasResponse)
			{
				rs.Session.Response = context.Response;

				if (PacketCaptureClientHandlerSession.CanHaveResponseBody(context.Request.Method) && rs.Session.Fields.HasFlag(CapturedHttpFields.ResponseBody))
				{
					rs.Original = context.Response.Content;
					rs.Intercepted = new(context.Response.Content, this.Manager.Pool);
					context.Response.Content = rs.Intercepted;
				}
			}

			return default;
		}

		/// <inheritdoc/>
		public ValueTask CompleteResponse(BetterHttpClientContext context)
		{
			if (!context.TryGetState<RequestState>(NAME, out var rs)) return default;

			rs.Session.EndedAt = this.Manager.Clock.GetCurrentInstant();

			if (context.HasResponse)
			{
				if (rs.Intercepted is not null)
				{
					context.Response.Content = rs.Original;
					if (rs.Intercepted.HasCapturedData())
					{
						rs.Session.ResponseBody = rs.Intercepted.GetCapturedData();
					}

					rs.Intercepted.Dispose();
					rs.Intercepted = null;
					rs.Original = null;
				}
			}

			return default;
		}

		/// <inheritdoc/>
		public ValueTask Finalize(BetterHttpClientContext context)
		{
			if (!context.TryGetState<RequestState>(NAME, out var rs)) return default;

			var metadata = rs.Session.GetMetadata();
			return this.Manager.Emit(metadata, rs.Session.RequestBody, rs.Session.ResponseBody);
		}

	}

}
