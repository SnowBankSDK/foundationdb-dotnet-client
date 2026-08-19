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
	/// <summary>Generic HTTP protocol that exposes all HTTP verbs without any custom processing</summary>
	[PublicAPI]
	public class RestHttpProtocol : IBetterHttpProtocol
	{

		public RestHttpProtocol(HttpClient httpClient, RestHttpClientOptions options)
		{
			this.Http = httpClient;
			this.Options = options;
		}

		public RestHttpClientOptions Options { get; }

		/// <inheritdoc />
		public HttpClient Http { get; }

		string IBetterHttpProtocol.Name => "Generic";

		public void Dispose()
		{
			this.Http.Dispose();
		}

		public Task<TResult> SendAsync<TResult>(HttpRequestMessage request, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> this.Http.SendAsync(request, handler, ct);

		#region OPTIONS...

		public Task<TResult> OptionsAsync<TResult>(string uri, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreateOptionsRequest(uri), handler, ct);

		public Task<TResult> OptionsAsync<TResult>(Uri uri, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreateOptionsRequest(uri), handler, ct);

		#endregion

		#region HEAD...

		public Task<TResult> HeadAsync<TResult>(string uri, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreateHeadRequest(uri), handler, ct);

		public Task<TResult> HeadAsync<TResult>(Uri uri, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreateHeadRequest(uri), handler, ct);

		public Task HeadAsync(string uri, Func<BetterHttpClientContext, Task> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreateHeadRequest(uri), handler, ct);

		public Task HeadAsync(Uri uri, Func<BetterHttpClientContext, Task> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreateHeadRequest(uri), handler, ct);

		public Task HeadAsync(string uri, Action<BetterHttpClientContext> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreateHeadRequest(uri), handler, ct);

		public Task HeadAsync(Uri uri, Action<BetterHttpClientContext> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreateHeadRequest(uri), handler, ct);

		#endregion

		#region GET...

		public Task<TResult> GetAsync<TResult>(string uri, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreateGetRequest(uri), handler, ct);

		public Task<TResult> GetAsync<TResult>(Uri uri, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreateGetRequest(uri), handler, ct);

		#region GET Text...

		public Task<string> GetTextAsync(string uri, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreateGetRequest(uri), query =>
			{
				query.EnsureSuccessStatusCode();
				return query.Response.Content.ReadAsStringAsync(ct);
			}, ct);

		public Task<string> GetTextAsync(Uri uri, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreateGetRequest(uri), query =>
			{
				query.EnsureSuccessStatusCode();
				return query.Response.Content.ReadAsStringAsync(ct);
			}, ct);

		#endregion

		#region GET Binary...

		public Task<byte[]> GetBinaryAsync(string uri, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreateGetRequest(uri), query =>
			{
				query.EnsureSuccessStatusCode();
				return query.Response.Content.ReadAsByteArrayAsync(ct);
			}, ct);

		public Task<byte[]> GetBinaryAsync(Uri uri, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreateGetRequest(uri), query =>
			{
				query.EnsureSuccessStatusCode();
				return query.Response.Content.ReadAsByteArrayAsync(ct);
			}, ct);

		#endregion

		#region GET Json...

		public Task<JsonObject?> GetJsonAsync(string uri, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreateGetRequest(uri), query =>
			{
				query.EnsureSuccessStatusCode();
				return query.Response.Content.ReadFromCrystalJsonObjectAsync(ct);
			}, ct);

		public Task<JsonObject?> GetJsonAsync(Uri uri, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreateGetRequest(uri), query =>
			{
				query.EnsureSuccessStatusCode();
				return query.Response.Content.ReadFromCrystalJsonObjectAsync(ct);
			}, ct);

		#endregion

		#endregion

		#region POST...

		public Task<TResult> PostAsync<TResult>(string uri, HttpContent content, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePostRequest(uri, content), handler, ct);

		public Task<TResult> PostAsync<TResult>(Uri uri, HttpContent content, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePostRequest(uri, content), handler, ct);
		
		public Task PostAsync(string uri, HttpContent content, Func<BetterHttpClientContext, Task> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePostRequest(uri, content), handler, ct);

		public Task PostAsync(Uri uri, HttpContent content, Func<BetterHttpClientContext, Task> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePostRequest(uri, content), handler, ct);

		public Task PostAsync(string uri, HttpContent content, Action<BetterHttpClientContext> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePostRequest(uri, content), handler, ct);

		public Task PostAsync(Uri uri, HttpContent content, Action<BetterHttpClientContext> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePostRequest(uri, content), handler, ct);

		#region POST Text...

		public Task<string?> PostTextAsync(string uri, string? content, Encoding? encoding, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePostRequest(uri, content != null ? new StringContent(content, encoding) : null), (query) =>
			{
				query.EnsureSuccessStatusCode();
				return query.Response.Content.ReadAsStringAsync(ct);
			}, ct)!;

		public Task<string?> PostTextAsync(Uri uri, string? content, Encoding? encoding, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePostRequest(uri, content != null ? new StringContent(content, encoding) : null), (query) =>
			{
				query.EnsureSuccessStatusCode();
				return query.Response.Content.ReadAsStringAsync(ct);
			}, ct)!;

		#endregion

		#region POST Json...

		public Task<TResult> PostJsonAsync<TRequest, TResult>(string uri, TRequest request, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePostRequest(uri, CrystalJsonContent.Create(request)), handler, ct);

		public Task<TResult> PostJsonAsync<TRequest, TResult>(Uri uri, TRequest request, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePostRequest(uri, CrystalJsonContent.Create(request)), handler, ct);

		public Task PostJsonAsync<TRequest>(string uri, TRequest request, Func<BetterHttpClientContext, Task> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePostRequest(uri, CrystalJsonContent.Create(request)), handler, ct);

		public Task PostJsonAsync<TRequest>(Uri uri, TRequest request, Func<BetterHttpClientContext, Task> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePostRequest(uri, CrystalJsonContent.Create(request)), handler, ct);

		public Task PostJsonAsync<TRequest>(string uri, TRequest request, Action<BetterHttpClientContext> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePostRequest(uri, CrystalJsonContent.Create(request)), handler, ct);

		public Task PostJsonAsync<TRequest>(Uri uri, TRequest request, Action<BetterHttpClientContext> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePostRequest(uri, CrystalJsonContent.Create(request)), handler, ct);

		#endregion

		#endregion

		#region PUT...

		public Task<TResult> PutAsync<TResult>(string uri, HttpContent content, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePutRequest(uri, content), handler, ct);

		public Task<TResult> PutAsync<TResult>(Uri uri, HttpContent content, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePutRequest(uri, content), handler, ct);

		public Task PutAsync(string uri, HttpContent content, Func<BetterHttpClientContext, Task> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePutRequest(uri, content), handler, ct);

		public Task PutAsync(Uri uri, HttpContent content, Func<BetterHttpClientContext, Task> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePutRequest(uri, content), handler, ct);

		public Task PutAsync(string uri, HttpContent content, Action<BetterHttpClientContext> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePutRequest(uri, content), handler, ct);

		public Task PutAsync(Uri uri, HttpContent content, Action<BetterHttpClientContext> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePutRequest(uri, content), handler, ct);

		#region PUT Json...

		public Task<TResult> PutJsonAsync<TRequest, TResult>(string uri, TRequest request, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePutRequest(uri, CrystalJsonContent.Create(request)), handler, ct);

		public Task<TResult> PutJsonAsync<TRequest, TResult>(Uri uri, TRequest request, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePutRequest(uri, CrystalJsonContent.Create(request)), handler, ct);

		public Task PutJsonAsync<TRequest>(string uri, TRequest request, Func<BetterHttpClientContext, Task> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePutRequest(uri, CrystalJsonContent.Create(request)), handler, ct);

		public Task PutJsonAsync<TRequest>(Uri uri, TRequest request, Func<BetterHttpClientContext, Task> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePutRequest(uri, CrystalJsonContent.Create(request)), handler, ct);

		#endregion

		#endregion

		#region DELETE...

		public Task<TResult> DeleteAsync<TResult>(string uri, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreateDeleteRequest(uri), handler, ct);

		public Task<TResult> DeleteAsync<TResult>(Uri uri, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreateDeleteRequest(uri), handler, ct);

		public Task DeleteAsync(string uri, Func<BetterHttpClientContext, Task> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreateDeleteRequest(uri), handler, ct);

		public Task DeleteAsync(Uri uri, Func<BetterHttpClientContext, Task> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreateDeleteRequest(uri), handler, ct);

		public Task DeleteAsync(string uri, Action<BetterHttpClientContext> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreateDeleteRequest(uri), handler, ct);

		public Task DeleteAsync(Uri uri, Action<BetterHttpClientContext> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreateDeleteRequest(uri), handler, ct);

		#endregion

		#region PATCH...

		public Task<TResult> PatchAsync<TResult>(string uri, HttpContent content, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePatchRequest(uri, content), handler, ct);

		public Task<TResult> PatchAsync<TResult>(Uri uri, HttpContent content, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePatchRequest(uri, content), handler, ct);

		public Task PatchAsync(string uri, HttpContent content, Func<BetterHttpClientContext, Task> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePatchRequest(uri, content), handler, ct);

		public Task PatchAsync(Uri uri, HttpContent content, Func<BetterHttpClientContext, Task> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePatchRequest(uri, content), handler, ct);

		public Task PatchAsync(string uri, HttpContent content, Action<BetterHttpClientContext> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePatchRequest(uri, content), handler, ct);

		public Task PatchAsync(Uri uri, HttpContent content, Action<BetterHttpClientContext> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePatchRequest(uri, content), handler, ct);

		#region PATCH Json...

		public Task<TResult> PatchJsonAsync<TRequest, TResult>(string uri, TRequest request, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePatchRequest(uri, CrystalJsonContent.Create(request)), handler, ct);

		public Task<TResult> PatchJsonAsync<TRequest, TResult>(Uri uri, TRequest request, Func<BetterHttpClientContext, Task<TResult>> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePatchRequest(uri, CrystalJsonContent.Create(request)), handler, ct);

		public Task PatchJsonAsync<TRequest>(string uri, TRequest request, Func<BetterHttpClientContext, Task> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePatchRequest(uri, CrystalJsonContent.Create(request)), handler, ct);

		public Task PatchJsonAsync<TRequest>(Uri uri, TRequest request, Func<BetterHttpClientContext, Task> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePatchRequest(uri, CrystalJsonContent.Create(request)), handler, ct);

		public Task PatchJsonAsync<TRequest>(string uri, TRequest request, Action<BetterHttpClientContext> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePatchRequest(uri, CrystalJsonContent.Create(request)), handler, ct);

		public Task PatchJsonAsync<TRequest>(Uri uri, TRequest request, Action<BetterHttpClientContext> handler, CancellationToken ct)
			=> this.Http.SendAsync(this.Http.CreatePatchRequest(uri, CrystalJsonContent.Create(request)), handler, ct);

		#endregion

		#endregion

	}

}
