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
	using System.Net.Security;
	using System.Security.Authentication;
	using System.Security.Cryptography.X509Certificates;
	using Microsoft.Extensions.DependencyInjection;

	/// <summary>Base class of generic options for <see cref="BetterHttpClient">HTTP clients</see></summary>
	[PublicAPI]
	public record BetterHttpClientOptions
	{

		/// <summary>Optional hooks</summary>
		/// <remarks>Mostly used for unit testing or low-level debugging</remarks>
		public IBetterHttpHooks? Hooks { get; set; }

		/// <summary>Default initial HTTP version for all requests</summary>
		public Version DefaultRequestVersion { get; set; } = HttpVersion.Version11;

		/// <summary>Default policy for selecting the HTTP version of a request</summary>
		public HttpVersionPolicy DefaultVersionPolicy { get; set; } = HttpVersionPolicy.RequestVersionOrHigher;

		/// <summary>List of filters that will be able to intercept and or modify the request and response</summary>
		public List<IBetterHttpFilter> Filters { get; } = [ ];

		/// <summary>List of wrappers that can be applied to the underlying HTTP message handler</summary>
		public List<Func<HttpMessageHandler, BetterHttpClientOptions, IServiceProvider, HttpMessageHandler>> Handlers { get; set; } = [ ];

		/// <summary>List of default headers applied to each requests</summary>
		public BetterDefaultHeaders DefaultRequestHeaders { get; set; } = new();

		/// <summary>Specifies whether the client should follow redirection responses.</summary>
		public bool? AllowAutoRedirect { get; set; }

		/// <summary>Specifies the type of decompression method used by the handler for automatic decompression of the HTTP content response.</summary>
		public DecompressionMethods? AutomaticDecompression { get; set; }

		/// <summary>Default cookie container that will be used by each request.</summary>
		public CookieContainer? Cookies { get; set; }

		/// <summary>Default credentials that will be used by each request.</summary>
		public IBetterCredentials? Credentials { get; set; }

		/// <summary>List of custom options that will be added to the <see cref="HttpRequestMessage.Options"/> of the request, before evaluated any filters</summary>
		/// <remarks>Use this to "inject" any custom option that could be used to override the behavior of filters</remarks>
		public List<KeyValuePair<string, object?>>? Options { get; set; }

		/// <summary>Specifies the proxy information used by the client.</summary>
		public IWebProxy? Proxy { get; set; }

		/// <summary>Default credentials used to authenticate against a proxy.</summary>
		public ICredentials? DefaultProxyCredentials { get; set; }

		/// <summary>Callback used to validate the certificate presented by the remote server</summary>
		public Func<HttpRequestMessage, X509Certificate2?, X509Chain?, SslPolicyErrors, bool>? ServerCertificateCustomValidationCallback { get; set; }

		/// <summary>Options used to configure the certificate that this client will provide when asked to authenticate by the remote server</summary>
		public ClientCertificateOption? ClientCertificateOptions { get; set; }

		/// <summary>Specifies whether the remote server certificate should be checked with the local revocation list</summary>
		public bool? CheckCertificateRevocationList { get; set; }

		/// <summary>Specifies the <see cref="SslProtocols"/> supported by this client.</summary>
		public SslProtocols? SslProtocols { get; set; }

		/// <summary>Specifies a custom collection for the certificates used by this client to authenticate with the remote server</summary>
		public X509CertificateCollection? ClientCertificates { get; set; }

		/// <summary>Accepts any server certificate, even if they are self-signed or expired.</summary>
		/// <remarks>This is a convenience method that will set <see cref="ServerCertificateCustomValidationCallback"/> to a cached callback that always returns <c>true</c></remarks>
		[Obsolete("This is dangerous! Please acknowledge this by using a #pragma to disable this warning.")]
		public void DangerousAcceptAnyServerCertificate()
		{
			this.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
		}

		/// <summary>Adds a delegating handler to the chain of handlers used by this client</summary>
		/// <typeparam name="THandler">Type of handler, that must be constructible using the <see cref="IServiceProvider"/> that will be used to build the client</typeparam>
		/// <remarks>
		/// <para>The handlers are applied, in order, to wrap the previous <see cref="HttpMessageHandler"/>.</para>
		/// <para>This handler will wrap all previously defined handlers, and will be wrapped by any following handler.</para>
		/// </remarks>
		public void WithDelegatingHandler<THandler>()
			where THandler : DelegatingHandler
		{
			this.Handlers.Add((inner, _, services) =>
			{
				var handler = ActivatorUtilities.CreateInstance<THandler>(services);
				handler.InnerHandler = inner;
				return handler;
			});
		}

		/// <summary>Checks if a type implementing <see cref="HttpMessageHandler"/> is considered a "test" client that is emulating requests "in-process"</summary>
		internal static bool IsTestClient(Type type)
		{
			// Microsoft.AspNetCore.TestHost.ClientHandler
			if (type.Name == "ClientHandler" && type.Namespace == "Microsoft.AspNetCore.TestHost")
			{
				return true;
			}

			return false;
		}

		/// <summary>Applies the transport-level wrappers (credentials, custom handlers, filter wrappers) on top of a raw transport handler, WITHOUT the <see cref="MagicalHandler"/>.</summary>
		/// <remarks>On the pooled path the <see cref="MagicalHandler"/> is registered separately (as an <c>AddHttpMessageHandler</c>); this method builds the rest of the chain that sits between it and the socket transport.</remarks>
		internal HttpMessageHandler BuildTransportPipeline(HttpMessageHandler handler, IServiceProvider services)
		{
			Contract.Debug.Requires(handler is not null);

			if (this.Credentials is not null)
			{
				handler = this.Credentials.Configure(handler, this, services);
				Contract.Debug.Assert(handler is not null);
			}

			// add any optional wrappers on top of that
			foreach (var factory in this.Handlers)
			{
				handler = factory(handler, this, services);
				Contract.Debug.Assert(handler is not null);
			}

			// filters may also wrap handlers
			foreach (var filter in this.Filters)
			{
				handler = filter.Wrap(this, handler);
				Contract.Debug.Assert(handler is not null);
			}

			return handler;
		}

		internal MagicalHandler WrapHandler(HttpMessageHandler handler, IServiceProvider services)
		{
			return new MagicalHandler(BuildTransportPipeline(handler, services));
		}

		/// <summary>Applies any default configuration to the specified handler</summary>
		protected virtual HttpMessageHandler ConfigureDefaults(HttpMessageHandler handler)
		{
			if (this.AllowAutoRedirect is not null || this.AutomaticDecompression is not null)
			{
				switch (handler)
				{
					case BetterHttpClientHandler clientHandler:
					{
						if (this.AllowAutoRedirect is not null) clientHandler.AllowAutoRedirect = this.AllowAutoRedirect.Value;
						if (this.AutomaticDecompression is not null) clientHandler.AutomaticDecompression = this.AutomaticDecompression.Value;
						return clientHandler;
					}
					case HttpClientHandler clientHandler:
					{
						if (this.AllowAutoRedirect is not null) clientHandler.AllowAutoRedirect = this.AllowAutoRedirect.Value;
						if (this.AutomaticDecompression is not null) clientHandler.AutomaticDecompression = this.AutomaticDecompression.Value;
						return clientHandler;
					}
					default:
					{
#if DEBUG
						//TODO: for delegating handlers, maybe we could try going up the chain of inner handlers until we find something? or should be wrap the handler (similar to how we handle cookies)
						if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif

						return handler;
					}
				}
			}

			return handler;
		}

		/// <summary>Update the specified handler to properly handle cookies, if they are required</summary>
		/// <remarks><para>If the top handler is <see cref="HttpClientHandler"/> it will be configured directly; otherwise, it will be wrapped with an instance of <see cref="CookieContainerMessageHandler"/> that will handle the <c>Cookie</c> and <c>Set-Cookie</c> headers automatically.</para></remarks>
		protected virtual HttpMessageHandler ConfigureCookies(HttpMessageHandler handler)
		{
			if (this.Cookies is not null)
			{
				switch (handler)
				{
					case BetterHttpClientHandler clientHandler:
					{
						clientHandler.UseCookies = true;
						clientHandler.CookieContainer = this.Cookies;

						return clientHandler;
					}
					case HttpClientHandler clientHandler:
					{
						clientHandler.UseCookies = true;
						clientHandler.CookieContainer = this.Cookies;

						return clientHandler;
					}
					case CookieContainerMessageHandler:
					{
						throw new InvalidOperationException("Cannot wrap cookies twice on the same HTTP handler");
					}
					default:
					{
						return new CookieContainerMessageHandler(this.Cookies, handler);
					}
				}
			}
			return handler;
		}

		/// <summary>Apply filters to the specified handler.</summary>
		protected virtual HttpMessageHandler ConfigureFilters(HttpMessageHandler handler)
		{
			foreach (var filter in this.Filters)
			{
				handler = filter.Wrap(this, handler);
			}
			return handler;
		}

		protected virtual HttpMessageHandler ConfigureProxy(HttpMessageHandler handler)
		{
			if (this.Proxy is not null || this.DefaultProxyCredentials is not null)
			{
				switch (handler)
				{
					case BetterHttpClientHandler clientHandler:
					{
						if (this.Proxy is not null)
						{
							clientHandler.UseProxy = true;
							clientHandler.Proxy = this.Proxy;
						}

						if (this.DefaultProxyCredentials is not null)
						{
							clientHandler.DefaultProxyCredentials = this.DefaultProxyCredentials;
						}

						return handler;
					}
					case HttpClientHandler clientHandler:
					{
						if (this.Proxy is not null)
						{
							clientHandler.UseProxy = true;
							clientHandler.Proxy = this.Proxy;
						}

						if (this.DefaultProxyCredentials is not null)
						{
							clientHandler.DefaultProxyCredentials = this.DefaultProxyCredentials;
						}

						return handler;
					}
					default:
					{
						if (IsTestClient(handler.GetType()))
						{ // this is in-memory, there will be no concept of proxy, so we can simply skip all of this
							return handler;
						}

#if DEBUG
						//TODO: for delegating handlers, maybe we could try going up the chain of inner handlers until we find something? or should be wrap the handler (similar to how we handle cookies)
						if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif

						return handler;
					}
				}
			}

			return handler;
		}

		protected virtual HttpMessageHandler ConfigureHttps(HttpMessageHandler handler)
		{
			if (this.ClientCertificates is not null
			 || this.ServerCertificateCustomValidationCallback is not null
			 || this.ClientCertificateOptions is not null
			 || this.CheckCertificateRevocationList is not null
			 || this.SslProtocols is not null)
			{
				switch (handler)
				{
					case BetterHttpClientHandler betterHandler:
					{
						if (this.ClientCertificates is not null) betterHandler.ClientCertificates.AddRange(this.ClientCertificates);
						if (this.ServerCertificateCustomValidationCallback is not null) betterHandler.ServerCertificateCustomValidationCallback = this.ServerCertificateCustomValidationCallback;
						if (this.ClientCertificateOptions is not  null) betterHandler.ClientCertificateOptions = this.ClientCertificateOptions.Value;
						if (this.CheckCertificateRevocationList is not  null) betterHandler.CheckCertificateRevocationList = this.CheckCertificateRevocationList.Value;
						if (this.SslProtocols is not  null) betterHandler.SslProtocols = this.SslProtocols.Value;

						return betterHandler;
					}
					case HttpClientHandler clientHandler:
					{
						if (this.ClientCertificates is not null) clientHandler.ClientCertificates.AddRange(this.ClientCertificates);
						if (this.ServerCertificateCustomValidationCallback is not null) clientHandler.ServerCertificateCustomValidationCallback = this.ServerCertificateCustomValidationCallback;
						if (this.ClientCertificateOptions is not null) clientHandler.ClientCertificateOptions = this.ClientCertificateOptions.Value;
						if (this.CheckCertificateRevocationList is not null) clientHandler.CheckCertificateRevocationList = this.CheckCertificateRevocationList.Value;
						if (this.SslProtocols is not null) clientHandler.SslProtocols = this.SslProtocols.Value;

						return clientHandler;
					}
					default:
					{
						if (IsTestClient(handler.GetType()))
						{ // this is in-memory, there will be no TLS negotiation, so we can simply skip all of this
							return handler;
						}

#if DEBUG
						// this is an unsupported handler type, and we don't really know how to configure TLS
						// => if this is a DelegatingHandler, maybe walk the chain of inner handlers to find a HttpClientHandler?
						if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif

						return handler;
					}
				}
			}

			return handler;
		}

		/// <summary>Apply these options to a new <see cref="HttpMessageHandler"/></summary>
		/// <param name="handler">Handler that will be configured</param>
		/// <returns>Configured handler. This could be a different instance that wraps the original handler</returns>
		[MustUseReturnValue]
		public HttpMessageHandler Configure(HttpMessageHandler handler)
		{
			Contract.NotNull(handler);

			if (handler is BetterHttpClientHandler betterHandler)
			{
				betterHandler.Setup(this);
			}

			handler = ConfigureHttps(handler);

			handler = ConfigureDefaults(handler);

			handler = ConfigureFilters(handler);

			handler = ConfigureProxy(handler);

			handler = ConfigureCookies(handler);

			return handler;
		}

	}

}
