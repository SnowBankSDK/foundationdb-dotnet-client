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
	using System.Diagnostics.CodeAnalysis;
	using System.Globalization;
	using System.IO;
	using System.Runtime.CompilerServices;
	using System.Runtime.ExceptionServices;
	using System.Xml.Linq;
	using Microsoft.IO;

	/// <summary>Represents the context of an HTTP request being executed</summary>
	[DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	public class BetterHttpClientContext
	{

		/// <summary>Instance of the client executing this request</summary>
		/// <remarks>This is the <see cref="HttpClient"/> (typically a <see cref="BetterHttpClient"/>) that the request was sent on. The pipeline reads its state from <see cref="Options"/>/<see cref="Clock"/>/<see cref="Services"/> instead of reaching into the client.</remarks>
		public HttpClient? Client { get; init; }

		/// <summary>Options used to configure the request pipeline (filters, hooks, credentials, ...)</summary>
		/// <remarks>These options travel with the request, and are read by the pipeline instead of reaching back into the client instance. When the context is created by the send extensions over a client that carries no runtime, they start as <see cref="BetterHttpClientOptions.Empty"/> and are filled in by the in-chain <see cref="BetterHttpPipelineHandler"/> (see <see cref="HasResolvedOptions"/>).</remarks>
		public BetterHttpClientOptions Options { get; internal set; } = BetterHttpClientOptions.Empty;

		/// <summary>True once <see cref="Options"/> and <see cref="Services"/> hold the resolved values (from a factory-built shell's runtime, or filled in by the in-chain pipeline handler); false while they hold the empty placeholders.</summary>
		internal bool HasResolvedOptions { get; set; }

		/// <summary>Clock used to measure the timestamps of this request</summary>
		/// <remarks>Plugins and filters that need to measure time should use this clock instead of their own.</remarks>
		public required IClock Clock { get; init; }

		/// <summary>Unique ID of this request (for logging purpose)</summary>
		public required string Id { get; init; }

		/// <summary>Cancellation token attached to the lifetime of this request</summary>
		public CancellationToken Cancellation { get; init; }

		/// <summary>Current stage in the execution pipeline</summary>
		public BetterHttpClientStage Stage { get; private set; }

		/// <summary>If non-null, the stage at which the request failed.</summary>
		public BetterHttpClientStage? FailedStage { get; internal set; }

		/// <summary>Bag of items that will be available throughout the lifetime of the request</summary>
		public required Dictionary<string, object?> State { get; init; }

		/// <summary>Request that will be sent to the remote HTTP server</summary>
		public required HttpRequestMessage Request { get; init; }

		/// <summary>Original response object, before it was intercepted.</summary>
		internal HttpResponseMessage? OriginalResponse { get; set; }

		/// <summary>Response that was received from the remote HTTP server</summary>
		/// <exception cref="InvalidOperationException">If no response was received by the HTTP server (error when sending the request, timeout, malformed response, ...)</exception>
		/// <seealso cref="HasResponse"/>
		public HttpResponseMessage Response
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.OriginalResponse ?? FailErrorNotAvailable();
		}

		/// <summary>Indicates if we have received a response from the remote HTTP server</summary>
		public bool HasResponse => this.OriginalResponse is not null;

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static HttpResponseMessage FailErrorNotAvailable() => throw new InvalidOperationException("The response message is not available.");

		/// <summary>Box that captured any error that happened during the processing of the request</summary>
		public ExceptionDispatchInfo? Error { get; internal set; }

		/// <summary>Provider for services used by this client when creating filters</summary>
		public IServiceProvider Services { get; internal set; } = BetterHttpClientRuntime.EmptyServiceProvider.Instance;

		/// <summary>Instant when the query was created</summary>
		/// <remarks>
		/// <para>This value is measured when the context for the query is created, before any other action is performed.</para>
		/// </remarks>
		public Instant CreatedAt { get; init; }

		/// <summary>Instant when the client started sending the request to the server</summary>
		/// <remarks>
		/// <para>This value is measured when the client has prepared the request, and will start sending the first byte to the server.</para>
		/// </remarks>
		public Instant? SendStartedAt { get; internal set; }

		/// <summary>Instant when the client completed sending the request to the server</summary>
		public Instant? SendCompletedAt { get; internal set; }

		/// <summary>Instant when the client started receiving the response to the server</summary>
		/// <remarks>
		/// <para>This value is measured when the client will start waiting for the response from the server.</para>
		/// </remarks>
		public Instant? ReceiveStartedAt { get; internal set; }

		/// <summary>Instant when the client completed receiving the response to the server</summary>
		/// <remarks>
		/// <para>This value is measured after the response from the server has been received and processed locally, but before finalization</para>
		/// <para>The delay will include both the "time to first byte", the time to receive the response body (and trailers), and the time required to post-process the body (decompression, deserialization, ...)</para>
		/// </remarks>
		public Instant? ReceiveCompletedAt { get; internal set; }

		/// <summary>Instant when the query was completed</summary>
		/// <remarks>This value is measured after the response from the server has been received and processed locally, and all filters have been finalized.</remarks>
		public Instant? CompletedAt { get; internal set; }

		/// <summary>Elapsed duration of the query</summary>
		/// <remarks>
		/// <para>This returns the time elapsed between <see cref="CreatedAt"/> and either <see cref="CompletedAt"/>, or the current system time (if <c>null</c>).</para>
		/// <para>This value will always be greater than the <i>actual</i> network operation between the client and the server, since it includes pre- and post-processing steps.</para>
		/// </remarks>
		public Duration Elapsed => (this.CompletedAt ?? this.Clock.GetCurrentInstant()) - this.CreatedAt;

		/// <summary>Gets HTTP status code returned by the server</summary>
		/// <remarks>Returns <c>0</c> if the query was not sent, canceled, the server did not respond in time, or an error prevented from processing the response.</remarks>
		public HttpStatusCode StatusCode => this.OriginalResponse?.StatusCode ?? default; //note: we return "0", is there a better value?

		/// <summary>Changes the current stage in the execution pipeline</summary>
		internal void SetStage(BetterHttpClientStage stage)
		{
			this.Stage = stage;
			this.Options.Hooks?.OnStageChanged(this, stage);
		}

		/// <summary>Sets (or clear) an item in the <see cref="State"/> dictionary</summary>
		/// <typeparam name="TState"></typeparam>
		/// <param name="key">Key of the item</param>
		/// <param name="state">New value for this item. If null, the item is removed</param>
		public void SetState<TState>(string key, TState? state)
		{
			Contract.Debug.Requires(key != null);
			if (state is null)
			{
				this.State.Remove(key);
			}
			else
			{
				this.State[key] = state;
			}
		}

		/// <summary>Reads an item that was previously stored in the <see cref="State"/> dictionary</summary>
		public bool TryGetState<TState>(string key, [MaybeNullWhen(false)] out TState state)
		{
			Contract.Debug.Requires(key != null);
			if (!this.State.TryGetValue(key, out var obj) || obj is not TState value)
			{
				state = default;
				return false;
			}

			state = value;
			return true;
		}

		/// <summary>Throws an exception if the <see cref="IsSuccessStatusCode"/> property for the HTTP response is false.</summary>
		public void EnsureSuccessStatusCode()
		{
			this.Response.EnsureSuccessStatusCode();
		}

		/// <summary>Gets a value that indicates if the response was successful</summary>
		public bool IsSuccessStatusCode => this.OriginalResponse?.IsSuccessStatusCode ?? false;

		/// <summary>Reads the response body as a string</summary>
		public Task<string> ReadAsStringAsync()
		{
			return this.Response.Content.ReadAsStringAsync(this.Cancellation);
		}

		/// <summary>Returns a stream that can be used to read the response body</summary>
		public Task<Stream> ReadAsStreamAsync()
		{
			return this.Response.Content.ReadAsStreamAsync(this.Cancellation);
		}

		/// <summary>Copies the response body into the provided stream</summary>
		public Task CopyToAsync(Stream stream)
		{
			return this.Response.Content.CopyToAsync(stream, this.Cancellation);
		}

		#region JSON Helpers...

		/// <summary>Guesses if the response body is <i>likely</i> to be a JSON document</summary>
		/// <returns><c>true</c> if there is a high probability that the body contains a JSON document</returns>
		/// <remarks>
		/// <para>Since we cannot inspect the whole response BODY (which may not have been received yet), this method can only guess by looking at the <c>Content-Type</c> header, and so may return either false-positives, or false-negatives.</para>
		/// <para>This should only be used by error handling logic that could decide whether to parse the body or not, looking for additional details.</para>
		/// </remarks>
		public bool IsLikelyJson()
		{
			//TODO: a better heuristic? The issue is that we may not have received the whole body yet, so we can inspect it until the end to match the '}' or ']' !
			if (this.OriginalResponse == null) return false;
			if (this.Response.Content.Headers.ContentType?.MediaType == "application/json")
			{
				return true;
			}

			return false;
		}

		private static readonly RecyclableMemoryStreamManager DefaultPool = new();

		/// <summary>Reads the response body as a JSON value</summary>
		public async Task<JsonValue> ReadAsJsonAsync(CrystalJsonSettings? settings = null)
		{
			this.Cancellation.ThrowIfCancellationRequested();
			using var activity = BetterHttpInstrumentation.ActivitySource.StartActivity("JSON Parse");

			try
			{
				//BUGBUG: PERF: until we have async JSON parsing, we have to buffer everything to memory
				using (var ms = DefaultPool.GetStream())
				{
					await this.CopyToAsync(ms).ConfigureAwait(false);
					return CrystalJson.Parse(ms.ToSlice(), settings);
				}
			}
			catch (Exception ex)
			{
				activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
				activity?.AddException(ex);
				throw;
			}
		}

		/// <summary>Reads the response body as a JSON Object</summary>
		public async Task<JsonObject?> ReadAsJsonObjectAsync(CrystalJsonSettings? settings = null)
		{
			this.Cancellation.ThrowIfCancellationRequested();
			using var activity = BetterHttpInstrumentation.ActivitySource.StartActivity("JSON Parse");

			try
			{
				//BUGBUG: PERF: until we have async JSON parsing, we have to buffer everything to memory
				using (var ms = DefaultPool.GetStream())
				{
					await CopyToAsync(ms).ConfigureAwait(false);
					activity?.SetTag("json.length", ms.Length);
					return CrystalJson.Parse(ms.ToSlice(), settings).AsObjectOrDefault();
				}
			}
			catch (Exception ex)
			{
				activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
				activity?.AddException(ex);
				throw;
			}
		}

		/// <summary>Reads the response body as a JSON Array</summary>
		public async Task<JsonArray?> ReadAsJsonArrayAsync(CrystalJsonSettings? settings = null)
		{
			this.Cancellation.ThrowIfCancellationRequested();
			using var activity = BetterHttpInstrumentation.ActivitySource.StartActivity("JSON Parse");

			try
			{
				//BUGBUG: PERF: until we have async JSON parsing, we have to buffer everything to memory
				using (var ms = DefaultPool.GetStream())
				{
					await CopyToAsync(ms).ConfigureAwait(false);
					return CrystalJson.Parse(ms.ToSlice(), settings).AsArrayOrDefault();
				}
			}
			catch (Exception ex)
			{
				activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
				activity?.AddException(ex);
				throw;
			}
		}

		/// <summary>Reads the response body as a JSON document, and converts the result into an instance of type <typeparamref name="TResult"/></summary>
		public async Task<TResult?> ReadAsJsonAsync<TResult>(CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			this.Cancellation.ThrowIfCancellationRequested();
			using var activity = BetterHttpInstrumentation.ActivitySource.StartActivity("JSON Parse");

			try
			{
				//BUGBUG: PERF: until we have async JSON parsing, we have to buffer everything to memory
				using (var ms = DefaultPool.GetStream())
				{
					await CopyToAsync(ms).ConfigureAwait(false);
					return CrystalJson.Deserialize<TResult?>(ms.ToSlice(), default, settings, resolver);
				}
			}
			catch (Exception ex)
			{
				activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
				activity?.AddException(ex);
				throw;
			}
		}

		#endregion

		#region XML Helpers...

		/// <summary>Guesses if the response body is <i>likely</i> to be an XML document</summary>
		/// <returns><c>true</c> if there is a high probability that the body contains an XML document</returns>
		/// <remarks>
		/// <para>Since we cannot inspect the whole response BODY (which may not have been received yet), this method can only guess by looking at the <c>Content-Type</c> header, and so may return either false-positives, or false-negatives.</para>
		/// <para>This should only be used by error handling logic that could decide whether to parse the body or not, looking for additional details.</para>
		/// </remarks>
		public bool IsLikelyXml()
		{
			//TODO: a better heuristic? The issue is that we may not have received the whole body yet, so we can inspect it until the end to match the closing tag !
			if (this.OriginalResponse == null) return false;
			if (this.Response.Content.Headers.ContentType?.MediaType == "text/xml")
			{
				return true;
			}
			return false;
		}

		/// <summary>Reads the response body as an XML document</summary>
		public async Task<XDocument?> ReadAsXmlAsync(LoadOptions options = LoadOptions.None)
		{
			this.Cancellation.ThrowIfCancellationRequested();
			using var activity = BetterHttpInstrumentation.ActivitySource.StartActivity("XML Parse");

			try
			{
				var stream = await this.Response.Content.ReadAsStreamAsync(this.Cancellation).ConfigureAwait(false);
				//note: do NOT dispose this stream here!

				return await XDocument.LoadAsync(stream, options, this.Cancellation).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
				activity?.AddException(ex);
				throw;
			}
		}

		#endregion

		public override string ToString()
		{
			return string.CreateInvariant($"{this.Request.Method} {this.Request.RequestUri} => {(this.OriginalResponse != null ? $"{(int) this.Response.StatusCode} {this.Response.ReasonPhrase}" : "<no response>")}");
		}

	}

}
