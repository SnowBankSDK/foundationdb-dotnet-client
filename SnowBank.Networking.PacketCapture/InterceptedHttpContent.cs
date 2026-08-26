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

namespace SnowBank.Networking.PacketCapture
{
	using System.Net;
	using System.Net.Http;
	using Microsoft.IO;

	/// <summary>Wraps an inner <see cref="HttpContent"/> so that the actual bytes sent of the transport can be captured</summary>
	internal class InterceptedHttpContent : HttpContent
	{
		//note: when wrapping a response HttpContent, the observed concrete type is usually HttpConnectionResponseContent

		private HttpContent Inner { get; }
		
		private MemoryStream? Mirror { get; set; }

		public RecyclableMemoryStreamManager Pool { get; }

		public InterceptedHttpContent(HttpContent inner, RecyclableMemoryStreamManager pool)
		{
			this.Inner = inner;
			this.Pool = pool;
			//note: unfortunately, there does not seem to be any way of directly exposing the headers of the inner content and all the existing helper methods are "internal"
			// => we have to copy the headers! :(
			foreach (var kv in inner.Headers)
			{
				//note: we assume that they have already been verified by the inner content
				this.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
			}
		}

		public bool HasCapturedData() => this.Mirror != null;

		public Slice GetCapturedData()
		{
			if (this.Mirror == null) return Slice.Nil;
			//note: do NOT use the mirror stream's buffer because it is pooled!
			return this.Mirror.ToArray().AsSlice();
		}

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			var mirror = this.Mirror;
			if (mirror is not null)
			{
				this.Mirror = null;
				mirror.Dispose();
			}
			base.Dispose(disposing);
		}

		private StandardOutputInterceptorStream InterceptWriteStream(Stream stream)
		{
			var mirror = this.Pool.GetStream();
			var interceptor = new StandardOutputInterceptorStream(stream, mirror);
			this.Mirror = mirror;
			return interceptor;
		}

		private InputInterceptorStream InterceptReadStream(Stream stream)
		{
			var mirror = this.Pool.GetStream();
			var interceptor = new InputInterceptorStream(stream, mirror);
			this.Mirror = mirror;
			return interceptor;
		}

		/// <inheritdoc/>
		protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
		{
			return this.Inner.CopyToAsync(InterceptWriteStream(stream));
		}

		/// <inheritdoc/>
		protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);
			return this.Inner.CopyToAsync(InterceptWriteStream(stream), context, cancellationToken);
		}

		/// <inheritdoc/>
		protected override void SerializeToStream(Stream stream, TransportContext? context, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			this.Inner.CopyTo(InterceptWriteStream(stream), context, cancellationToken);
		}

		/// <inheritdoc/>
		protected override async Task<Stream> CreateContentReadStreamAsync()
		{
			return InterceptReadStream(await this.Inner.ReadAsStreamAsync());
		}

		/// <inheritdoc/>
		protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
		{
			return InterceptReadStream(await this.Inner.ReadAsStreamAsync(cancellationToken));
		}

		/// <inheritdoc/>
		protected override Stream CreateContentReadStream(CancellationToken cancellationToken)
		{
			return InterceptReadStream(this.Inner.ReadAsStream(cancellationToken));
		}

		/// <inheritdoc/>
		protected override bool TryComputeLength(out long length)
		{
			//note: we cannot call TryComputeLength(...) on the inner content since it is "protected internal",
			// but when looking at the source code, calling "Inner.Headers.ContentLength" will do it for us!
			// We can infer that if TryComputeLength would return false, the ContentLength would be null

			long? l = this.Inner.Headers.ContentLength;
			length = l ?? 0;
			return l != null;
		}

	}

}
