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
	using System.Runtime.CompilerServices;
	using System.Runtime.ExceptionServices;

	/// <summary>Provides the Better<i>*</i> request pipeline - the IoC callback lifecycle, the request builders and the typed helpers - as extension methods on any <see cref="HttpClient"/>.</summary>
	/// <remarks>
	/// <para>These used to be instance methods on <see cref="BetterHttpClient"/>; they were moved out so the client can be a thin empty shell over a pooled handler chain. The call syntax is unchanged (<c>client.CreateGetRequest(uri)</c>, <c>client.SendAsync(request, ctx =&gt; ..., ct)</c>).</para>
	/// <para>The per-request options, clock and services are read from the runtime that <see cref="IBetterHttpClientFactory"/> attaches to the client; a raw <see cref="HttpClient"/> not built by the factory falls back to empty options and the system clock.</para>
	/// </remarks>
	[PublicAPI]
	public static class BetterHttpRequestExtensions
	{

		/// <summary>Key used to attach the per-request <see cref="BetterHttpClientContext"/> to <see cref="HttpRequestMessage.Options"/>.</summary>
		internal static readonly HttpRequestOptionsKey<BetterHttpClientContext> OptionKey = new("BetterHttp");

		/// <summary>Process-wide counter used to generate per-request correlation ids.</summary>
		/// <remarks>MUST be a field</remarks>
		private static long RequestCounter;

		private static string NewRequestId(string clientId)
			=> string.CreateInvariant($"{clientId}:{Interlocked.Increment(ref RequestCounter):D8}");

		#region Request Creation...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static Uri ConvertPathToUri(this HttpClient client, string path)
			=> client.EnsureRelativeUri(new Uri(path, UriKind.RelativeOrAbsolute));

		/// <summary>Ensures that a path is a relative URI, or an absolute URI under the client's <see cref="HttpClient.BaseAddress"/> (if it has one).</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uri EnsureRelativeUri(this HttpClient client, Uri path)
		{
			if (path.IsAbsoluteUri)
			{
				var baseAddress = client.BaseAddress;
				// a pooled client has no base address (the call site provides absolute uris); only validate when a base address is set.
				if (baseAddress is not null && !baseAddress.IsBaseOf(path))
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
		/// <param name="client">Client that will send the request</param>
		/// <param name="method">Method of the HTTP request</param>
		/// <param name="path">Local path (relative to the <see cref="HttpClient.BaseAddress"/> of the client) or absolute URI</param>
		/// <param name="content">Optional <see cref="HttpContent"/> that will be sent as the Body of the request</param>
		public static HttpRequestMessage CreateRequestMessage(this HttpClient client, HttpMethod method, string path, HttpContent? content = null)
			=> client.CreateRequestMessage(method, client.ConvertPathToUri(path), content);

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a specific HTTP method</summary>
		/// <param name="client">Client that will send the request</param>
		/// <param name="method">Method of the HTTP request</param>
		/// <param name="path">Local path (relative to the <see cref="HttpClient.BaseAddress"/> of the client) or absolute URI</param>
		/// <param name="content">Optional <see cref="HttpContent"/> that will be sent as the Body of the request</param>
		public static HttpRequestMessage CreateRequestMessage(this HttpClient client, HttpMethod method, Uri path, HttpContent? content = null)
		{
			Contract.Debug.Requires(method != null && path != null);
			var req = new HttpRequestMessage(method, client.EnsureRelativeUri(path))
			{
				Version = client.DefaultRequestVersion,
				VersionPolicy = client.DefaultVersionPolicy,
				Content = content,
			};
			//note: the default headers will be added later in the pipeline
			return req;
		}

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a GET request</summary>
		public static HttpRequestMessage CreateGetRequest(this HttpClient client, string path)
			=> client.CreateGetRequest(client.ConvertPathToUri(path));

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a GET request</summary>
		public static HttpRequestMessage CreateGetRequest(this HttpClient client, Uri path)
			=> client.CreateRequestMessage(HttpMethod.Get, path);

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a POST request</summary>
		public static HttpRequestMessage CreatePostRequest(this HttpClient client, string path, HttpContent? content)
			=> client.CreatePostRequest(client.ConvertPathToUri(path), content);

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a POST request</summary>
		public static HttpRequestMessage CreatePostRequest(this HttpClient client, Uri path, HttpContent? content)
			=> client.CreateRequestMessage(HttpMethod.Post, path, content);

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a PUT request</summary>
		public static HttpRequestMessage CreatePutRequest(this HttpClient client, string path, HttpContent content)
			=> client.CreatePutRequest(client.ConvertPathToUri(path), content);

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a PUT request</summary>
		public static HttpRequestMessage CreatePutRequest(this HttpClient client, Uri path, HttpContent content)
			=> client.CreateRequestMessage(HttpMethod.Put, path, content);

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a PATCH request</summary>
		public static HttpRequestMessage CreatePatchRequest(this HttpClient client, string path, HttpContent content)
			=> client.CreatePatchRequest(client.ConvertPathToUri(path), content);

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a PATCH request</summary>
		public static HttpRequestMessage CreatePatchRequest(this HttpClient client, Uri path, HttpContent content)
			=> client.CreateRequestMessage(HttpMethod.Patch, path, content);

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a DELETE request</summary>
		public static HttpRequestMessage CreateDeleteRequest(this HttpClient client, string path)
			=> client.CreateDeleteRequest(client.ConvertPathToUri(path));

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a DELETE request</summary>
		public static HttpRequestMessage CreateDeleteRequest(this HttpClient client, Uri path)
			=> client.CreateRequestMessage(HttpMethod.Delete, path);

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a HEAD request</summary>
		public static HttpRequestMessage CreateHeadRequest(this HttpClient client, string path)
			=> client.CreateHeadRequest(client.ConvertPathToUri(path));

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a HEAD request</summary>
		public static HttpRequestMessage CreateHeadRequest(this HttpClient client, Uri path)
			=> client.CreateRequestMessage(HttpMethod.Head, path);

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a OPTIONS request</summary>
		public static HttpRequestMessage CreateOptionsRequest(this HttpClient client, string path)
			=> client.CreateHeadRequest(client.ConvertPathToUri(path));

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a OPTIONS request</summary>
		public static HttpRequestMessage CreateOptionsRequest(this HttpClient client, Uri path)
			=> client.CreateRequestMessage(HttpMethod.Options, path);

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a TRACE request</summary>
		public static HttpRequestMessage CreateTraceRequest(this HttpClient client, string path)
			=> client.CreateTraceRequest(client.ConvertPathToUri(path));

		/// <summary>Creates a new <see cref="HttpRequestMessage"/> for a TRACE request</summary>
		public static HttpRequestMessage CreateTraceRequest(this HttpClient client, Uri path)
			=> client.CreateRequestMessage(HttpMethod.Trace, path);

		#endregion

		#region Sending...

		/// <summary>Sends an HTTP request to the remote target</summary>
		/// <typeparam name="TResult">Type of the expected result</typeparam>
		/// <param name="client">Client that will send the request</param>
		/// <param name="request">Request message, prepared with <see cref="CreateRequestMessage(System.Net.Http.HttpClient,System.Net.Http.HttpMethod,System.Uri,System.Net.Http.HttpContent?)"/> (or similar methods)</param>
		/// <param name="handler">Handler that will be called with the result of the request, and which is responsible for processing the response and generating the result</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the request (as returned by <paramref name="handler"/>) if it was successful; otherwise, an exception is thrown.</returns>
		public static Task<TResult> SendAsync<TResult>(this HttpClient client, HttpRequestMessage request, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> SendCoreAsync<TResult>(client, request, handler, ct);

		/// <summary>Sends an HTTP request to the remote target</summary>
		/// <typeparam name="TResult">Type of the expected result</typeparam>
		/// <param name="client">Client that will send the request</param>
		/// <param name="request">Request message, prepared with <see cref="CreateRequestMessage(System.Net.Http.HttpClient,System.Net.Http.HttpMethod,System.Uri,System.Net.Http.HttpContent?)"/> (or similar methods)</param>
		/// <param name="handler">Handler that will be called with the result of the request, and which is responsible for processing the response and generating the result</param>
		/// <param name="ct">Token used to cancel the operation</param>
		public static Task<TResult> SendAsync<TResult>(this HttpClient client, HttpRequestMessage request, Func<BetterHttpClientContext, TResult> handler, CancellationToken ct)
			=> SendCoreAsync<TResult>(client, request, handler, ct);

		/// <summary>Sends an HTTP request to the remote target</summary>
		/// <param name="client">Client that will send the request</param>
		/// <param name="request">Request message, prepared with <see cref="CreateRequestMessage(System.Net.Http.HttpClient,System.Net.Http.HttpMethod,System.Uri,System.Net.Http.HttpContent?)"/> (or similar methods)</param>
		/// <param name="handler">Handler that will be called with the result of the request, and which is responsible for processing the response.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		public static Task SendAsync(this HttpClient client, HttpRequestMessage request, Func<BetterHttpClientContext, Task> handler, CancellationToken ct)
			=> SendCoreAsync<object?>(client, request, handler, ct);

		/// <summary>Sends an HTTP request to the remote target</summary>
		/// <param name="client">Client that will send the request</param>
		/// <param name="request">Request message, prepared with <see cref="CreateRequestMessage(System.Net.Http.HttpClient,System.Net.Http.HttpMethod,System.Uri,System.Net.Http.HttpContent?)"/> (or similar methods)</param>
		/// <param name="handler">Handler that will be called with the result of the request, and which is responsible for processing the response.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		public static Task SendAsync(this HttpClient client, HttpRequestMessage request, Action<BetterHttpClientContext> handler, CancellationToken ct)
			=> SendCoreAsync<object?>(client, request, handler, ct);

		/// <summary>Sends an HTTP request to the remote target, and processes the response.</summary>
		private static async Task<TResult> SendCoreAsync<TResult>(HttpClient client, HttpRequestMessage request, Delegate handler, CancellationToken ct)
		{
			Contract.Debug.Requires(client != null && request != null && handler != null);

			var runtime = BetterHttpClientRuntime.Resolve(client);
			var options = runtime.Options;
			var clock = runtime.Clock;

			var startedAt = clock.GetCurrentInstant();
			var context = new BetterHttpClientContext()
			{
				Id = NewRequestId(runtime.Id),
				Client = client,
				Options = options,
				Clock = clock,
				Services = runtime.Services,
				Cancellation = ct,
				State = new(StringComparer.Ordinal),
				Request = request,
				CreatedAt = startedAt,
			};

			try
			{
				request.Options.Set(OptionKey, context);

				// throw immediately if already cancelled!
				ct.ThrowIfCancellationRequested();

				#region Configure...

				context.SetStage(BetterHttpClientStage.Configure);
				foreach (var filter in options.Filters)
				{
					Contract.Debug.Assert(filter != null);
					try
					{
						await filter.Configure(context).ConfigureAwait(false);
					}
					catch (Exception e)
					{
						if (!(options.Hooks?.OnFilterError(context, e) ?? false))
						{
							throw;
						}
					}
				}
				options.Hooks?.OnConfigured(context);

				#endregion

				using (request)
				{
					//note: handling of the request is performed inside the delegating handler

					#region Send...

					HttpResponseMessage res;
					try
					{
						context.SendStartedAt = clock.GetCurrentInstant();
						res = await client.SendAsync(context.Request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
						context.OriginalResponse = res;
					}
					catch (Exception)
					{
						context.SendCompletedAt = clock.GetCurrentInstant();
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
							foreach (var filter in options.Filters)
							{
								try
								{
									await filter.PrepareResponse(context).ConfigureAwait(false);
								}
								catch (Exception e)
								{
									options.Hooks?.OnFilterError(context, e);
								}
							}

							options.Hooks?.OnResponsePrepared(context);
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
							context.ReceiveStartedAt = clock.GetCurrentInstant();
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
									// somehow we got an unexpected delegate type?
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

							context.ReceiveCompletedAt = clock.GetCurrentInstant();
							context.SetStage(BetterHttpClientStage.CompleteResponse);
							foreach (var filter in options.Filters)
							{
								try
								{
									await filter.CompleteResponse(context).ConfigureAwait(false);
								}
								catch (Exception e)
								{
									if (!(options.Hooks?.OnFilterError(context, e) ?? false))
									{
										throw;
									}
								}
							}
							options.Hooks?.OnResponseCompleted(context);

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
				options.Hooks?.OnError(context, e);
				throw;
			}
			finally
			{
				#region Finalize...

				await FinalizeQuery(context).ConfigureAwait(false);

				#endregion
			}
		}

		private static async Task FinalizeQuery(BetterHttpClientContext context)
		{
			var options = context.Options;
			try
			{
				context.SetStage(BetterHttpClientStage.Finalize);
				foreach (var filter in options.Filters)
				{
					try
					{
						await filter.Finalize(context).ConfigureAwait(false);
					}
					catch (Exception e)
					{
						if (!(options.Hooks?.OnFilterError(context, e) ?? false))
						{
							throw;
						}
					}
				}
			}
			finally
			{
				context.CompletedAt = context.Clock.GetCurrentInstant();
				options.Hooks?.OnQueryFinalized(context);
			}
		}

		#endregion

	}

}
