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

	/// <summary>Represents the credentials that the client will provide to the remote server</summary>
	public interface IBetterCredentials
	{

		/// <summary>When <c>true</c>, these credentials act exclusively through <see cref="OnBeforeRequest"/> (their <see cref="Configure"/> half is a no-op), so they may be attached per-shell (<see cref="BetterHttpShellOptions.Credentials"/>) - the "different identity per transient client" pattern.</summary>
		/// <remarks>Transport-coupled credentials (the default) configure the shared pooled handler when the name's chain is built, and therefore belong to a named policy registered at startup.</remarks>
		bool IsPerRequestOnly => false;

		/// <summary>Configures the options to configure any filter or delegating handler that would be required to process these credentials</summary>
		HttpMessageHandler Configure(HttpMessageHandler handler, BetterHttpClientOptions options, IServiceProvider services);

		/// <summary>Called right before sending a request to the remote server</summary>
		/// <remarks>This method can be used to inject any necessary headers in the request.</remarks>
		ValueTask OnBeforeRequest(BetterHttpClientContext context);

	}

	/// <summary>Wraps an existing <see cref="ICredentials"/> instance</summary>
	/// <remarks>These credentials will be injected into the <see cref="HttpClientHandler"/> (or equivalent) used by the client.</remarks>
	public sealed class WrappedCredentials : IBetterCredentials
	{

		/// <summary>Wraps an existing <see cref="ICredentials"/></summary>
		public WrappedCredentials(ICredentials credentials)
		{
			Contract.NotNull(credentials);
			this.Credentials = credentials;
		}

		/// <summary>Wrapped credentials</summary>
		public ICredentials Credentials { get; }

		/// <inheritdoc/>
		HttpMessageHandler IBetterCredentials.Configure(HttpMessageHandler handler, BetterHttpClientOptions options, IServiceProvider _)
		{
			switch (handler)
			{
				case HttpClientHandler clientHandler:
				{
					clientHandler.Credentials = this.Credentials;
					break;
				}
				case BetterHttpClientHandler clientHandler:
				{
					clientHandler.Credentials = this.Credentials; 
					break;
				}
				default:
				{
					if (BetterHttpClientOptions.IsTestClient(handler.GetType()))
					{ // this is in-memory, there will be no TLS negotiation, so we can simply skip all of this
						return handler;
					}

#if DEBUG
					//TODO: for delegating handlers, maybe we could try going up the chain of inner handlers until we find something? or should be wrap the handler (similar to how we handle cookies)
					if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif

					return handler;
				}
			}
			return handler;
		}

		/// <inheritdoc/>
		ValueTask IBetterCredentials.OnBeforeRequest(BetterHttpClientContext context) => default;

	}

	/// <summary>Configures the <see cref="HttpClientHandler"/> to use the default user credentials</summary>
	/// <remarks>These credentials will set the <see cref="HttpClientHandler.UseDefaultCredentials"/> property to <c>true</c>.</remarks>
	public sealed class UseDefaultCredentials : IBetterCredentials
	{

		public static readonly IBetterCredentials Instance = new UseDefaultCredentials();

		private UseDefaultCredentials() { }

		HttpMessageHandler IBetterCredentials.Configure(HttpMessageHandler handler, BetterHttpClientOptions options, IServiceProvider _)
		{
			switch (handler)
			{
				case HttpClientHandler clientHandler:
				{
					clientHandler.UseDefaultCredentials = true;
					break;
				}
				case BetterHttpClientHandler clientHandler:
				{
					clientHandler.UseDefaultCredentials = true;
					break;
				}
				default:
				{
					if (BetterHttpClientOptions.IsTestClient(handler.GetType()))
					{
						// BUGBUG: REVIEW: this is not "possible" using TestServer. Should we throw here in this case? (the test will not work as expected)
						return handler;
					}
#if DEBUG
					//TODO: for delegating handlers, maybe we could try going up the chain of inner handlers until we find something? or should be wrap the handler (similar to how we handle cookies)
					if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
					return handler;
				}
			}
			return handler;
		}

		/// <inheritdoc/>
		ValueTask IBetterCredentials.OnBeforeRequest(BetterHttpClientContext context) => default;

	}

}
