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
	using System.Globalization;
	using System.Net.Http.Headers;
	using System.Runtime.CompilerServices;
	using System.Runtime.ExceptionServices;
	using Microsoft.Extensions.Logging;

	/// <summary><see cref="HttpClient"/>, but with a Better<i>*</i> API</summary>
	[DebuggerDisplay("Id={Id}, BaseAddress={HostAddress}")]
	[PublicAPI]
	public sealed class BetterHttpClient : IDisposable
	{

		/// <summary>Client wrapped by this instance</summary>
		private HttpClient Client { get; }

		/// <summary>Original HTTP handler that will actually execute the request</summary>
		/// <remarks>This instance will wrap the handler with custom logic and filters</remarks>
		private HttpMessageHandler Handler { get; }

		/// <summary>Unique ID of this client</summary>
		/// <remarks>This can be used as a key in a connection pool, or as a prefix for a Correlation ID, etc...</remarks>
		public string Id { get; }

		/// <summary>Options used to configure this client</summary>
		public BetterHttpClientOptions Options { get; }

		/// <summary>Counter used to track all requests issued by this client</summary>
		/// <remarks>MUST be a field</remarks>
		private int RequestCounter;

		/// <summary>Address of the remote target</summary>
		public Uri HostAddress { get; }

		/// <summary>Container for the cookies used by this client</summary>
		public CookieContainer? Cookies { get; }

		/// <summary>Time of creating of this client</summary>
		public Instant CreatedAt { get; }

		/// <summary>Clock used by this client to generate timestamps</summary>
		/// <remarks>Plugins and filter that need to measure time should use this clock instead of their own.</remarks>
		public IClock Clock { get; }

		/// <summary>Provider for services used by this client when creating filters</summary>
		private IServiceProvider Services { get; }

		/// <summary>Internal logger</summary>
		private ILogger Logger { get; }

		internal static readonly HttpRequestOptionsKey<BetterHttpClientContext> OptionKey = new ("BetterHttp");

		/// <summary>Custom HTTP handler that will apply any pre- or post-filter to the pipeline</summary>
		internal sealed class MagicalHandler : DelegatingHandler
		{
			//note: due to how HttpHeaders are handled by the HttpClient, we are forced to hook our logic in a delegating handler,
			// which will be invoked once the HttpClient has completely set up the request (added default headers, etc...)

			public MagicalHandler(HttpMessageHandler innerHandler)
				: base(innerHandler)
			{ }

			protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				if (!request.Options.TryGetValue(OptionKey, out var context))
				{ // not enabled? skip it!
					return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
				}

				var client = context.Client;
				var filters = client.Options.Filters;

				context.SetStage(BetterHttpClientStage.PrepareRequest);
				foreach (var filter in filters)
				{
					try
					{
						await filter.PrepareRequest(context).ConfigureAwait(false);
					}
					catch (Exception e)
					{
						if (!(client.Options.Hooks?.OnFilterError(context, e) ?? false))
						{
							throw;
						}
					}
				}
				client.Options.Hooks?.OnRequestPrepared(context);

				context.SetStage(BetterHttpClientStage.Send);
				var res = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

				context.SetStage(BetterHttpClientStage.CompleteRequest);
				foreach (var filter in filters)
				{
					try
					{
						await filter.CompleteRequest(context).ConfigureAwait(false);
					}
					catch (Exception e)
					{
						if (!(client.Options.Hooks?.OnFilterError(context, e) ?? false))
						{
							throw;
						}
					}
				}
				client.Options.Hooks?.OnRequestCompleted(context);

				//note: handling of the response is deferred to the caller!
				return res;
			}
		}

		public BetterHttpClient(
			Uri hostAddress,
			BetterHttpClientOptions options,
			HttpMessageHandler handler,
			ILogger<BetterHttpClient> logger,
			NodaTime.IClock? clock,
			IServiceProvider services)
		{
			this.Id = CorrelationIdGenerator.GetNextId();
			this.HostAddress = hostAddress;
			this.Handler = handler;
			this.Options = options;
			this.Cookies = options.Cookies;

			this.Logger = logger;
			this.Clock = clock ?? NodaTime.SystemClock.Instance;
			this.CreatedAt = this.Clock.GetCurrentInstant();
			this.Services = services;

			this.Client = CreateClientState();
		}

		public HttpRequestHeaders DefaultRequestHeaders => this.Client.DefaultRequestHeaders;

		private HttpClient CreateClientState()
		{
			// note: we _could_ skip HttpClient altogether, and directly invoke the HttpMessageHandler, BUT:
			// 1) there is a lot of cancellation/timeout/error handling logic that is already implemented by HttpClient
			// 2) all the methods to copy over the default headers are "internal" and cannot be accessed easily, without using reflection or unsafe shenanigans! :(

			var client = CreateClient(this.Handler);

			// copy all the default headers
			this.Options.DefaultRequestHeaders.Apply(client.DefaultRequestHeaders);

			return client;
		}

		private HttpClient CreateClient(HttpMessageHandler handler)
		{
			// add our own delegating handler that will be able to hook into the request lifecycle
			handler = new MagicalHandler(handler);

			// add any optional wrappers on top of that
			if (this.Options.Handlers.Count > 0)
			{
				foreach (var factory in this.Options.Handlers)
				{
					handler = factory(handler, this.Services);
				}
			}

			var client = new HttpClient(handler, disposeHandler: true)
			{
				BaseAddress = this.HostAddress,
				DefaultRequestVersion = this.Options.DefaultRequestVersion,
				DefaultVersionPolicy = this.Options.DefaultVersionPolicy,
			};
			return client;
		}

		public string NewRequestId()
		{
			return string.Create(CultureInfo.InvariantCulture, $"{this.Id}:{Interlocked.Increment(ref this.RequestCounter):D8}");
		}

		#region Request Creation...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private Uri ConvertPathToUri(string path)
			=> EnsureRelativeUri(new Uri(path, UriKind.RelativeOrAbsolute));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uri EnsureRelativeUri(Uri path)
		{
			if (path.IsAbsoluteUri)
			{
				// must be the same base address!
				if (!this.HostAddress.IsBaseOf(path))
				{
					throw ErrorPathMustBeRelative();
				}
			}
			return path;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static ArgumentException ErrorPathMustBeRelative()
			// ReSharper disable once NotResolvedInText
			=> new("The query path must be a relative URI.", "path");

		/// <summary>Creates a new <see cref="HttpRequestMessage"/></summary>
		/// <param name="method">Method of the HTTP request</param>
		/// <param name="path">Local path (relative to the <see cref="HostAddress"/> of the client)</param>
		/// <param name="content">Optional <see cref="HttpContent"/> that will be sent as the Body of the request</param>
		public HttpRequestMessage CreateRequestMessage(HttpMethod method, string path, HttpContent? content = null)
			=> CreateRequestMessage(method, ConvertPathToUri(path), content);

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a specific HTTP method</summary>
		/// <param name="method">Method of the HTTP request</param>
		/// <param name="path">Local path (relative to the <see cref="HostAddress"/> of the client)</param>
		/// <param name="content">Optional <see cref="HttpContent"/> that will be sent as the Body of the request</param>
		public HttpRequestMessage CreateRequestMessage(HttpMethod method, Uri path, HttpContent? content = null)
		{
			Contract.Debug.Requires(method != null && path != null);
			var req = new HttpRequestMessage(method, EnsureRelativeUri(path))
			{
				Version = this.Options.DefaultRequestVersion,
				VersionPolicy = this.Options.DefaultVersionPolicy,
				Content = content,
			};
			//note: the default headers will be added later in the pipeline
			return req;
		}

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a GET request</summary>
		/// <param name="path">Local path (relative to the <see cref="HostAddress"/> of the client)</param>
		public HttpRequestMessage CreateGetRequest(string path)
			=> CreateGetRequest(ConvertPathToUri(path));

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a GET request</summary>
		/// <param name="path">Local path (relative to the <see cref="HostAddress"/> of the client)</param>
		public HttpRequestMessage CreateGetRequest(Uri path)
			=> CreateRequestMessage(HttpMethod.Get, path);

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a POST request</summary>
		/// <param name="path">Local path (relative to the <see cref="HostAddress"/> of the client)</param>
		/// <param name="content"><see cref="HttpContent"/> that will be sent as the Body of the request</param>
		public HttpRequestMessage CreatePostRequest(string path, HttpContent? content)
			=> CreatePostRequest(ConvertPathToUri(path), content);

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a POST request</summary>
		/// <param name="path">Local path (relative to the <see cref="HostAddress"/> of the client)</param>
		/// <param name="content"><see cref="HttpContent"/> that will be sent as the Body of the request</param>
		public HttpRequestMessage CreatePostRequest(Uri path, HttpContent? content)
			=> CreateRequestMessage(HttpMethod.Post, path, content);

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a PUT request</summary>
		/// <param name="path">Local path (relative to the <see cref="HostAddress"/> of the client)</param>
		/// <param name="content"><see cref="HttpContent"/> that will be sent as the Body of the request</param>
		public HttpRequestMessage CreatePutRequest(string path, HttpContent content)
			=> CreatePutRequest(ConvertPathToUri(path), content);

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a PUT request</summary>
		/// <param name="path">Local path (relative to the <see cref="HostAddress"/> of the client)</param>
		/// <param name="content"><see cref="HttpContent"/> that will be sent as the Body of the request</param>
		public HttpRequestMessage CreatePutRequest(Uri path, HttpContent content)
			=> CreateRequestMessage(HttpMethod.Put, path, content);

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a PATCH request</summary>
		/// <param name="path">Local path (relative to the <see cref="HostAddress"/> of the client)</param>
		/// <param name="content"><see cref="HttpContent"/> that will be sent as the Body of the request</param>
		public HttpRequestMessage CreatePatchRequest(string path, HttpContent content)
			=> CreatePatchRequest(ConvertPathToUri(path), content);

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a PATCH request</summary>
		/// <param name="path">Local path (relative to the <see cref="HostAddress"/> of the client)</param>
		/// <param name="content"><see cref="HttpContent"/> that will be sent as the Body of the request</param>
		public HttpRequestMessage CreatePatchRequest(Uri path, HttpContent content)
			=> CreateRequestMessage(HttpMethod.Patch, path, content);

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a DELETE request</summary>
		/// <param name="path">Local path (relative to the <see cref="HostAddress"/> of the client)</param>
		public HttpRequestMessage CreateDeleteRequest(string path)
			=> CreateDeleteRequest(ConvertPathToUri(path));

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a DELETE request</summary>
		/// <param name="path">Local path (relative to the <see cref="HostAddress"/> of the client)</param>
		public HttpRequestMessage CreateDeleteRequest(Uri path)
			=> CreateRequestMessage(HttpMethod.Delete, path);

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a HEAD request</summary>
		/// <param name="path">Local path (relative to the <see cref="HostAddress"/> of the client)</param>
		public HttpRequestMessage CreateHeadRequest(string path)
			=> CreateHeadRequest(ConvertPathToUri(path));

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a HEAD request</summary>
		/// <param name="path">Local path (relative to the <see cref="HostAddress"/> of the client)</param>
		public HttpRequestMessage CreateHeadRequest(Uri path)
			=> CreateRequestMessage(HttpMethod.Head, path);

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a OPTIONS request</summary>
		/// <param name="path">Local path (relative to the <see cref="HostAddress"/> of the client)</param>
		public HttpRequestMessage CreateOptionsRequest(string path)
			=> CreateHeadRequest(ConvertPathToUri(path));

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a OPTIONS request</summary>
		/// <param name="path">Local path (relative to the <see cref="HostAddress"/> of the client)</param>
		public HttpRequestMessage CreateOptionsRequest(Uri path)
			=> CreateRequestMessage(HttpMethod.Options, path);

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a TRACE request</summary>
		/// <param name="path">Local path (relative to the <see cref="HostAddress"/> of the client)</param>
		public HttpRequestMessage CreateTraceRequest(string path)
			=> CreateTraceRequest(ConvertPathToUri(path));

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a TRACE request</summary>
		/// <param name="path">Local path (relative to the <see cref="HostAddress"/> of the client)</param>
		public HttpRequestMessage CreateTraceRequest(Uri path)
			=> CreateRequestMessage(HttpMethod.Trace, path);

		#endregion

		/// <summary>Sends an HTTP request to the remote target</summary>
		/// <typeparam name="TResult">Type of the expected result</typeparam>
		/// <param name="request">Request message, prepared with <see cref="CreateRequestMessage(System.Net.Http.HttpMethod,System.Uri,System.Net.Http.HttpContent?)"/> (or similar methods)</param>
		/// <param name="handler">Handler that will be called with the result of the request, and which is responsible for processing the response and generating the result</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the request (as returned by <see cref="handler"/>) if it was successful; otherwise, and exception is thrown.</returns>
		public Task<TResult> SendAsync<TResult>(HttpRequestMessage request, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> SendCoreAsync<TResult>(request, handler, ct);

		/// <summary>Sends an HTTP request to the remote target</summary>
		/// <typeparam name="TResult">Type of the expected result</typeparam>
		/// <param name="request">Request message, prepared with <see cref="CreateRequestMessage(System.Net.Http.HttpMethod,System.Uri,System.Net.Http.HttpContent?)"/> (or similar methods)</param>
		/// <param name="handler">Handler that will be called with the result of the request, and which is responsible for processing the response and generating the result</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the request (as returned by <see cref="handler"/>) if it was successful; otherwise, and exception is thrown.</returns>
		public Task<TResult> SendAsync<TResult>(HttpRequestMessage request, Func<BetterHttpClientContext, TResult> handler, CancellationToken ct)
			=> SendCoreAsync<TResult>(request, handler, ct);

		/// <summary>Sends an HTTP request to the remote target</summary>
		/// <param name="request">Request message, prepared with <see cref="CreateRequestMessage(System.Net.Http.HttpMethod,System.Uri,System.Net.Http.HttpContent?)"/> (or similar methods)</param>
		/// <param name="handler">Handler that will be called with the result of the request, and which is responsible for processing the response.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		public Task SendAsync(HttpRequestMessage request, Func<BetterHttpClientContext, Task> handler, CancellationToken ct)
			=> SendCoreAsync<object?>(request, handler, ct);

		/// <summary>Sends an HTTP request to the remote target</summary>
		/// <param name="request">Request message, prepared with <see cref="CreateRequestMessage(System.Net.Http.HttpMethod,System.Uri,System.Net.Http.HttpContent?)"/> (or similar methods)</param>
		/// <param name="handler">Handler that will be called with the result of the request, and which is responsible for processing the response.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		public Task SendAsync(HttpRequestMessage request, Action<BetterHttpClientContext> handler, CancellationToken ct)
			=> SendCoreAsync<object?>(request, handler, ct);

		/// <summary>Sends an HTTP request to the remote target, and processes the response.</summary>
		private async Task<TResult> SendCoreAsync<TResult>(HttpRequestMessage request, Delegate handler, CancellationToken ct)
		{
			Contract.Debug.Requires(request != null && handler != null);

			var filters = this.Options.Filters;

			var context = new BetterHttpClientContext()
			{
				Id = NewRequestId(),
				Client = this,
				Cancellation = ct,
				State = new(StringComparer.Ordinal),
				Request = request,
			};

			try
			{
				request.Options.Set(OptionKey, context);

				// throw immediately if already cancelled!
				ct.ThrowIfCancellationRequested();

				#region Configure...

				context.SetStage(BetterHttpClientStage.Configure);
				foreach (var filter in filters)
				{
					Contract.Debug.Assert(filter != null);
					try
					{
						await filter.Configure(context).ConfigureAwait(false);
					}
					catch (Exception e)
					{
						if (!(this.Options.Hooks?.OnFilterError(context, e) ?? false))
						{
							throw;
						}
					}
				}
				this.Options.Hooks?.OnConfigured(context);

				#endregion

				using (request)
				{
					//note: handling of the request is performed inse the delegating handler

					#region Send...

					HttpResponseMessage res;
					try
					{
						res = await this.Client.SendAsync(context.Request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
						context.OriginalResponse = res;
					}
					catch (Exception)
					{
						context.FailedStage ??= context.Stage;
						throw;
					}

					#endregion

					using (res)
					{
						#region Prepare Response...

						context.SetStage(BetterHttpClientStage.PrepareResponse);
						try
						{
							foreach (var filter in filters)
							{
								try
								{
									await filter.PrepareResponse(context).ConfigureAwait(false);
								}
								catch (Exception e)
								{
									this.Options.Hooks?.OnFilterError(context, e);
								}
							}

							this.Options.Hooks?.OnPrepareResponse(context);
						}
						catch (Exception)
						{
							context.FailedStage ??= context.Stage;
							throw;
						}

						#endregion

						#region Handle Response...

						context.SetStage(BetterHttpClientStage.HandleResponse);
						try
						{
							switch (handler)
							{
								case Func<BetterHttpClientContext, Task<TResult>> asyncResultHandler:
								{
									return await asyncResultHandler(context).ConfigureAwait(false);
								}
								case Func<BetterHttpClientContext, TResult> resultHandler:
								{
									return resultHandler(context);
								}
								case Func<BetterHttpClientContext, Task> asyncVoidHandler:
								{
									Contract.Debug.Requires(typeof(TResult) == typeof(object));
									await asyncVoidHandler(context).ConfigureAwait(false);
									return default!;
								}
								case Action<BetterHttpClientContext> voidHandler:
								{
									Contract.Debug.Requires(typeof(TResult) == typeof(object));
									voidHandler(context);
									return default!;
								}
								default:
								{
#if DEBUG
									// c'est pas normal! normalement on controle exactement le type de handler passé a cette fonction!
									if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
									throw new ArgumentException("Unexpected delegate type", nameof(handler));
								}
							}
						}
						catch (Exception)
						{
							context.FailedStage ??= context.Stage;
							throw;
						}
						finally
						{
							#region Complete Response...

							context.SetStage(BetterHttpClientStage.CompleteResponse);
							foreach (var filter in filters)
							{
								try
								{
									await filter.CompleteResponse(context).ConfigureAwait(false);
								}
								catch (Exception e)
								{
									if (!(this.Options.Hooks?.OnFilterError(context, e) ?? false))
									{
										throw;
									}
								}
							}
							this.Options.Hooks?.OnCompleteResponse(context);

							#endregion
						}

						#endregion
					}
				}
			}
			catch (Exception e)
			{
				context.Error = ExceptionDispatchInfo.Capture(e);
				context.FailedStage ??= context.Stage;
				this.Options.Hooks?.OnError(context, e);
				throw;
			}
			finally
			{
				#region Finalize...

				context.SetStage(BetterHttpClientStage.Finalize);
				foreach (var filter in filters)
				{
					try
					{
						await filter.Finalize(context).ConfigureAwait(false);
					}
					catch (Exception e)
					{
						if (!(this.Options.Hooks?.OnFilterError(context, e) ?? false))
						{
							throw;
						}
					}
				}
				this.Options.Hooks?.OnFinalizeQuery(context);

				#endregion
			}
		}

		#region IDisposable...

		/// <inheritdoc />
		public void Dispose()
		{
			Dispose(true);
		}

		private void Dispose(bool disposing)
		{
			if (disposing)
			{
				this.Client.Dispose();
			}
		}

		#endregion

	}

}
