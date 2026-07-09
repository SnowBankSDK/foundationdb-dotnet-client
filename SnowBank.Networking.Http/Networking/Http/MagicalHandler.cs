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

namespace SnowBank.Networking.Http
{

	/// <summary>Custom HTTP handler that applies any pre- or post-filter to the pipeline, once the <see cref="HttpClient"/> has fully set up the request.</summary>
	/// <remarks>
	/// <para>Due to how <see cref="System.Net.Http.Headers.HttpHeaders"/> are handled by <see cref="HttpClient"/>, we are forced to hook our logic in a delegating handler, which will be invoked once the client has completely set up the request (added default headers, etc...).</para>
	/// <para>This handler is registered on every pooled bundle (via <c>AddHttpMessageHandler</c>) and reads all its state from the per-request <see cref="BetterHttpClientContext"/> that travels on <see cref="HttpRequestMessage.Options"/>; it holds no per-client state of its own.</para>
	/// </remarks>
	internal sealed class MagicalHandler : DelegatingHandler
	{

		/// <summary>Creates a handler whose <see cref="DelegatingHandler.InnerHandler"/> is assigned later by the handler factory.</summary>
		public MagicalHandler()
		{ }

		/// <summary>Creates a handler that wraps the specified inner handler (used by the transitional per-call creation path).</summary>
		public MagicalHandler(HttpMessageHandler innerHandler)
			: base(innerHandler)
		{ }

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			if (!request.Options.TryGetValue(BetterHttpRequestExtensions.OptionKey, out var context))
			{ // not enabled? skip it!
				return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
			}

			var options = context.Options;

			context.SetStage(BetterHttpClientStage.PrepareRequest);

			// inject any custom request option
			if (options.Options is not null)
			{
				IDictionary<string, object?> dict = request.Options;
				foreach (var kv in options.Options)
				{
					dict.Add(kv);
				}
			}

			// notify all filters
			foreach (var filter in options.Filters)
			{
				try
				{
					await filter.PrepareRequest(context).ConfigureAwait(false);
				}
				catch (Exception e)
				{
					if (!(options.Hooks?.OnFilterError(context, e) ?? false))
					{
						throw;
					}
				}
			}

			// handle authentication
			if (options.Credentials is not null)
			{
				await options.Credentials.OnBeforeRequest(context).ConfigureAwait(false);
			}

			options.Hooks?.OnRequestPrepared(context);

			context.SetStage(BetterHttpClientStage.Send);
			var res = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

			context.SetStage(BetterHttpClientStage.CompleteRequest);
			foreach (var filter in options.Filters)
			{
				try
				{
					await filter.CompleteRequest(context).ConfigureAwait(false);
				}
				catch (Exception e)
				{
					if (!(options.Hooks?.OnFilterError(context, e) ?? false))
					{
						throw;
					}
				}
			}

			options.Hooks?.OnRequestCompleted(context);

			//note: handling of the response is deferred to the caller!
			return res;
		}

	}

}
