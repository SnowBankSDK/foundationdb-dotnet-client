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

	/// <summary>Extension methods for deserializing HTTP response bodies into JSON</summary>
	[PublicAPI]
	public static class CrystalJsonContentExtensions
	{

		internal const string ReflectionMessage = "JSON body deserialization uses reflection to bind the target type. Enroll the type with [CrystalJsonConverter] and read the DOM with ReadFromCrystalJsonObjectAsync, then bind it with the source-generated converter.";

		/// <summary>Reads the body of the response as a JSON Object</summary>
		public static Task<JsonObject?> ReadFromCrystalJsonObjectAsync(this HttpContent content, CancellationToken ct)
		{
			return ReadFromCrystalJsonObjectAsync(content, null, ct);
		}

		/// <summary>Reads the body of the response as a JSON Object</summary>
		public static Task<JsonObject?> ReadFromCrystalJsonObjectAsync(this HttpContent content, CrystalJsonSettings? settings, CancellationToken ct)
		{
			Contract.NotNull(content);

			//Encoding? sourceEncoding = JsonContent.GetEncoding(content.Headers.ContentType?.CharSet);
			Encoding? sourceEncoding = null; //TODO!

			return ReadFromJsonObjectAsyncCore(content, sourceEncoding, settings, ct);
		}

		/// <summary>Reads the body of the response as a JSON Object</summary>
		private static async Task<JsonObject?> ReadFromJsonObjectAsyncCore(HttpContent content, Encoding? sourceEncoding, CrystalJsonSettings? settings, CancellationToken ct)
		{
			var bytes  = await content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

			return CrystalJson.Parse(bytes.AsSlice(), settings).AsObject();
		}

		/// <summary>Reads the body of the response as a JSON value, converted into an instance of type <typeparamref name="T"/></summary>
		[RequiresUnreferencedCode(ReflectionMessage)]
		[RequiresDynamicCode(ReflectionMessage)]
		public static Task<T?> ReadFromCrystalJsonAsync<T>(this HttpContent content, CancellationToken ct)
		{
			return ReadFromCrystalJsonAsync<T>(content, null, null, ct);
		}

		/// <summary>Reads the body of the response as a JSON value, converted into an instance of type <typeparamref name="T"/></summary>
		[RequiresUnreferencedCode(ReflectionMessage)]
		[RequiresDynamicCode(ReflectionMessage)]
		public static Task<T?> ReadFromCrystalJsonAsync<T>(this HttpContent content, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver, CancellationToken ct)
		{
			Contract.NotNull(content);

			//Encoding? sourceEncoding = JsonContent.GetEncoding(content.Headers.ContentType?.CharSet);
			Encoding? sourceEncoding = null; //TODO!

			return ReadFromJsonAsyncCore<T>(content, sourceEncoding, settings, resolver, ct);
		}

		/// <summary>Reads the body of the response as a JSON value, converted into an instance of type <typeparamref name="T"/></summary>
		[RequiresUnreferencedCode(ReflectionMessage)]
		[RequiresDynamicCode(ReflectionMessage)]
		private static async Task<T?> ReadFromJsonAsyncCore<T>(HttpContent content, Encoding? sourceEncoding, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver, CancellationToken ct)
		{
			var bytes  = await content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

			return CrystalJson.Deserialize(bytes.AsSlice(), default(T), settings, resolver);
		}

	}

}
