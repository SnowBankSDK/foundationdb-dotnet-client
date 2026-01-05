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
	using System.Net.Http.Headers;

	/// <summary>Helper to configure default headers for <see cref="BetterHttpClient">HTTP clients</see></summary>
	[PublicAPI]
	public sealed class BetterDefaultHeaders
	{

		/// <summary>Adds a custom default HTTP header</summary>
		/// <param name="name">Name of the header</param>
		/// <param name="value">Value of the header</param>
		public void Add(string name, string? value)
		{
			Contract.NotNullOrWhiteSpace(name);

			if (value is null)
			{
				return;
			}

			if (!this.Extra.TryGetValue(name, out var existing))
			{
				existing = [ ];
				this.Extra[name] = existing;
			}

			existing.Add(value);
		}

		/// <summary>Adds a custom default HTTP header with multiple values</summary>
		/// <param name="name">Name of the header</param>
		/// <param name="values">Values of the header</param>
		public void Add(string name, ReadOnlySpan<string> values)
		{
			Contract.NotNullOrWhiteSpace(name);

			if (values.Length == 0)
			{
				return;
			}

			if (!this.Extra.TryGetValue(name, out var existing))
			{
				existing = [ ];
				this.Extra[name] = existing;
			}

			foreach (var value in values)
			{
				existing.Add(value);
			}
		}

		/// <summary>Adds a custom default HTTP header with multiple values</summary>
		/// <param name="name">Name of the header</param>
		/// <param name="values">Values of the header</param>
		public void Add(string name, string[]? values)
		{
			Contract.NotNullOrWhiteSpace(name);

			if (values is null || values.Length == 0)
			{
				return;
			}

			if (!this.Extra.TryGetValue(name, out var existing))
			{
				existing = [ ];
				this.Extra[name] = existing;
			}

			foreach (var value in values)
			{
				existing.Add(value);
			}
		}

		/// <summary>Adds a custom default HTTP header with multiple values</summary>
		/// <param name="name">Name of the header</param>
		/// <param name="values">Values of the header</param>
		public void Add(string name, IEnumerable<string>? values)
		{
			Contract.NotNullOrWhiteSpace(name);

			if (values is null)
			{
				return;
			}

			if (values.TryGetSpan(out var span))
			{
				Add(name, span);
				return;
			}

			var items = values.ToList();
			if (items.Count == 0)
			{
				return;
			}

			if (!this.Extra.TryGetValue(name, out var existing))
			{
				existing = items;
				this.Extra[name] = existing;
			}
			else
			{
				existing.AddRange(items);
			}
		}

		public string this[string name]
		{
			// not getter!
			set => Add(name, value);
		}

		/// <summary>Value of the <c>Accept</c> HTTP header</summary>
		public IList<MediaTypeWithQualityHeaderValue>? Accept { get; set; }

		/// <summary>Value of the <c>Accept-Charset</c> HTTP header</summary>
		public IList<StringWithQualityHeaderValue>? AcceptCharset { get; set; }

		/// <summary>Value of the <c>Accept-Encoding</c> HTTP header</summary>
		public IList<StringWithQualityHeaderValue>? AcceptEncoding { get; set; }

		/// <summary>Value of the <c>Accept-Language</c> HTTP header</summary>
		public IList<StringWithQualityHeaderValue>? AcceptLanguage { get; set; }

		public AuthenticationHeaderValue? Authorization { get; set; }

		/// <summary>Value of the <c>Cache-Control</c> HTTP header</summary>
		public CacheControlHeaderValue? CacheControl { get; set; }

		/// <summary>Value of the <c>Connection-Close</c> HTTP header</summary>
		public bool? ConnectionClose { get; set; }

		/// <summary>Value of the <c>User-Agent</c> HTTP header</summary>
		public IList<ProductInfoHeaderValue>? UserAgent { get; set; }

		/// <summary>Value of the <c>Via</c> HTTP header</summary>
		public IList<ViaHeaderValue>? Via { get; set; }

		/// <summary>Value of the <c>Referer</c> HTTP header</summary>
		public Uri? Referrer { get; set; }

		/// <summary>Values of the other HTTP headers that are not exposed via their own property</summary>
		public Dictionary<string, List<string>> Extra { get; set; } = new(StringComparer.Ordinal); //REVIEW: should this be OrdinalIgnoreCase?

		private static void AddRange<TValue>(IList<TValue> items, HttpHeaderValueCollection<TValue> collection)
			where TValue : class
		{
			foreach(var item in items) collection.Add(item);
		}

		/// <summary>Apply the headers of a request onto this instance</summary>
		/// <param name="headers">Headers to use as source</param>
		public void Apply(HttpRequestHeaders headers)
		{
			if (this.Accept != null) AddRange(this.Accept, headers.Accept);
			if (this.AcceptCharset != null) AddRange(this.AcceptCharset, headers.AcceptCharset);
			if (this.AcceptEncoding != null) AddRange(this.AcceptEncoding, headers.AcceptEncoding);
			if (this.AcceptLanguage != null) AddRange(this.AcceptLanguage, headers.AcceptLanguage);
			if (this.Authorization != null) headers.Authorization = this.Authorization;
			if (this.CacheControl != null) headers.CacheControl = this.CacheControl;
			if (this.ConnectionClose != null) headers.ConnectionClose = this.ConnectionClose;
			if (this.UserAgent != null) AddRange(this.UserAgent, headers.UserAgent);
			if (this.Referrer != null) headers.Referrer = this.Referrer;
			if (this.Via != null) AddRange(this.Via, headers.Via);

			foreach (var kv in this.Extra)
			{
				headers.Add(kv.Key, kv.Value);
			}
		}

	}

}
