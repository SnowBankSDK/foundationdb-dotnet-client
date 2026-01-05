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
	using Microsoft.Net.Http.Headers;

	/// <summary>Custom <see cref="DelegatingHandler"/> that adds cookies to the request</summary>
	public class CookieContainerMessageHandler : DelegatingHandler
	{

		/// <summary>Container used to store the cookies for this session</summary>
		public CookieContainer Cookies { get; }

		public CookieContainerMessageHandler(CookieContainer cookies, HttpMessageHandler next)
			: base(next)
		{
			this.Cookies = cookies;
		}

		/// <inheritdoc />
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var value = this.Cookies.GetCookieHeader(request.RequestUri!);
			if (!string.IsNullOrWhiteSpace(value))
			{
				request.Headers.Add(HeaderNames.Cookie, value);
			}

			var res = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

			if (res.Headers.TryGetValues(HeaderNames.SetCookie, out var values))
			{
				foreach (SetCookieHeaderValue cookieHeader in SetCookieHeaderValue.ParseList(values.ToList()))
				{
					Cookie cookie = new Cookie(cookieHeader.Name.Value!, cookieHeader.Value.Value, cookieHeader.Path.Value);
					if (cookieHeader.Expires.HasValue)
					{
						cookie.Expires = cookieHeader.Expires.Value.DateTime;
					}
					this.Cookies.Add(request.RequestUri!, cookie);
				}
			}

			return res;
		}

	}

}
