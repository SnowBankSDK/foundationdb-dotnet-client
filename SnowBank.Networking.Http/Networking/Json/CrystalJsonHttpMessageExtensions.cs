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

namespace SnowBank.SDK.AspNetCore.Common.Json
{
	using System.Diagnostics.CodeAnalysis;

	/// <summary>Extension methods for working with <see cref="HttpClient"/></summary>
	[PublicAPI]
	public static class CrystalJsonHttpMessageExtensions
	{

		internal const string ReflectionMessage = "JSON body serialization uses reflection over the value type. Enroll the type with [CrystalJsonConverter] and post the source-generated converter output (for example a StringContent built from the generated ToJsonText), or pass a JsonValue built from the DOM.";

		/// <summary>Sends a POST request with a value encoded as JSON bytes</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="client">HTTP Client</param>
		/// <param name="requestUri">URI of the request</param>
		/// <param name="value">Value that will be encoded as JSON</param>
		/// <param name="options">JSON serialization settings. Uses <see cref="CrystalJsonSettings.Json"/> by default.</param>
		/// <param name="cancellationToken">Token used to cancel the request.</param>
		/// <returns>The task object representing the asynchronous operation.</returns>
		[RequiresUnreferencedCode(ReflectionMessage), RequiresDynamicCode(ReflectionMessage)]
		public static Task<HttpResponseMessage> PostAsCrystalJsonAsync<TValue>(this HttpClient client, string? requestUri, TValue value, CrystalJsonSettings? options = null, CancellationToken cancellationToken = default)
		{
			Contract.NotNull(client);

			var content = CrystalJsonContent.Create(value, mediaType: null, options);
			return client.PostAsync(requestUri, content, cancellationToken);
		}

		/// <summary>Sends a POST request with a value encoded as JSON bytes</summary>
		/// <param name="client">HTTP Client</param>
		/// <param name="requestUri">URI of the request</param>
		/// <param name="value">Value that will be encoded as JSON</param>
		/// <param name="options">JSON serialization settings. Uses <see cref="CrystalJsonSettings.Json"/> by default.</param>
		/// <param name="cancellationToken">Token used to cancel the request.</param>
		/// <returns>The task object representing the asynchronous operation.</returns>
		public static Task<HttpResponseMessage> PostAsCrystalJsonAsync(this HttpClient client, string? requestUri, JsonValue value, CrystalJsonSettings? options = null, CancellationToken cancellationToken = default)
		{
			Contract.NotNull(client);

			var content = CrystalJsonContent.Create(value, mediaType: null, options);
			return client.PostAsync(requestUri, content, cancellationToken);
		}

		/// <summary>Sends a POST request with a value encoded as JSON bytes</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="client">HTTP Client</param>
		/// <param name="requestUri">URI of the request</param>
		/// <param name="value">Value that will be encoded as JSON</param>
		/// <param name="options">JSON serialization settings. Uses <see cref="CrystalJsonSettings.Json"/> by default.</param>
		/// <param name="cancellationToken">Token used to cancel the request.</param>
		/// <returns>The task object representing the asynchronous operation.</returns>
		[RequiresUnreferencedCode(ReflectionMessage), RequiresDynamicCode(ReflectionMessage)]
		public static Task<HttpResponseMessage> PostAsCrystalJsonAsync<TValue>(this HttpClient client, Uri? requestUri, TValue value, CrystalJsonSettings? options = null, CancellationToken cancellationToken = default)
		{
			Contract.NotNull(client);

			var content = CrystalJsonContent.Create(value, mediaType: null, options);
			return client.PostAsync(requestUri, content, cancellationToken);
		}

		/// <summary>Sends a POST request with a value encoded as JSON bytes</summary>
		/// <param name="client">HTTP Client</param>
		/// <param name="requestUri">URI of the request</param>
		/// <param name="value">Value that will be encoded as JSON</param>
		/// <param name="options">JSON serialization settings. Uses <see cref="CrystalJsonSettings.Json"/> by default.</param>
		/// <param name="cancellationToken">Token used to cancel the request.</param>
		/// <returns>The task object representing the asynchronous operation.</returns>
		public static Task<HttpResponseMessage> PostAsCrystalJsonAsync(this HttpClient client, Uri? requestUri, JsonValue value, CrystalJsonSettings? options = null, CancellationToken cancellationToken = default)
		{
			Contract.NotNull(client);

			var content = CrystalJsonContent.Create(value, mediaType: null, options);
			return client.PostAsync(requestUri, content, cancellationToken);
		}

		/// <summary>Sends a POST request with a value encoded as JSON bytes</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="client">HTTP Client</param>
		/// <param name="requestUri">URI of the request</param>
		/// <param name="value">Value that will be encoded as JSON</param>
		/// <param name="cancellationToken">Token used to cancel the request.</param>
		/// <returns>The task object representing the asynchronous operation.</returns>
		[RequiresUnreferencedCode(ReflectionMessage), RequiresDynamicCode(ReflectionMessage)]
		public static Task<HttpResponseMessage> PostAsCrystalJsonAsync<TValue>(this HttpClient client, string? requestUri, TValue value, CancellationToken cancellationToken)
			=> client.PostAsCrystalJsonAsync(requestUri, value, options: null, cancellationToken);

		/// <summary>Sends a POST request with a value encoded as JSON bytes</summary>
		/// <param name="client">HTTP Client</param>
		/// <param name="requestUri">URI of the request</param>
		/// <param name="value">Value that will be encoded as JSON</param>
		/// <param name="cancellationToken">Token used to cancel the request.</param>
		/// <returns>The task object representing the asynchronous operation.</returns>
		public static Task<HttpResponseMessage> PostAsCrystalJsonAsync(this HttpClient client, string? requestUri, JsonValue value, CancellationToken cancellationToken)
			=> client.PostAsCrystalJsonAsync(requestUri, value, options: null, cancellationToken);

		/// <summary>Sends a POST request with a value encoded as JSON bytes</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="client">HTTP Client</param>
		/// <param name="requestUri">URI of the request</param>
		/// <param name="value">Value that will be encoded as JSON</param>
		/// <param name="cancellationToken">Token used to cancel the request.</param>
		/// <returns>The task object representing the asynchronous operation.</returns>
		[RequiresUnreferencedCode(ReflectionMessage), RequiresDynamicCode(ReflectionMessage)]
		public static Task<HttpResponseMessage> PostAsCrystalJsonAsync<TValue>(this HttpClient client, Uri? requestUri, TValue value, CancellationToken cancellationToken)
			=> client.PostAsCrystalJsonAsync(requestUri, value, options: null, cancellationToken);

		/// <summary>Sends a POST request with a value encoded as JSON bytes</summary>
		/// <param name="client">HTTP Client</param>
		/// <param name="requestUri">URI of the request</param>
		/// <param name="value">Value that will be encoded as JSON</param>
		/// <param name="cancellationToken">Token used to cancel the request.</param>
		/// <returns>The task object representing the asynchronous operation.</returns>
		public static Task<HttpResponseMessage> PostAsCrystalJsonAsync(this HttpClient client, Uri? requestUri, JsonValue value, CancellationToken cancellationToken)
			=> client.PostAsCrystalJsonAsync(requestUri, value, options: null, cancellationToken);

	}

}
