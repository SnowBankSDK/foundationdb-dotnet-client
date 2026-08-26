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

namespace SnowBank.Data.Json
{
	using System.Buffers;
	using System.Diagnostics.CodeAnalysis;
	using System.IO;
	using System.Net.Http.Headers;
	using SnowBank.Runtime;

	/// <summary><see cref="HttpContent"/> that uses <see cref="CrystalJson"/> to serialize JSON payloads</summary>
	[PublicAPI]
	public class CrystalJsonContent : HttpContent
	{

		private static readonly ActivitySource ActivitySource = new("SnowBank.Data.Json");

		private CrystalJsonContent(
			object? inputValue,
			Type inputType,
			MediaTypeHeaderValue? mediaType,
			CrystalJsonSettings? jsonSettings,
			ICrystalJsonTypeResolver? jsonResolver)
		{
			Contract.NotNull(inputType);
#if DEBUG
			if (inputValue != null && !inputType.IsInstanceOfType(inputValue))
			{
				throw new ArgumentException($"Input value is of type {inputValue.GetType().GetFriendlyName()} which is not compatible with specified type {inputType.GetFriendlyName()}");
			}
#endif

			this.Value = inputValue;
			this.ObjectType = inputType;
			this.Headers.ContentType = mediaType ?? new("application/json") { CharSet = "utf-8" };
			this.JsonSettings = jsonSettings ?? CrystalJsonSettings.JsonIndented;
			this.JsonResolver = jsonResolver ?? CrystalJson.DefaultResolver;
		}

		// Renders the body through a source-generated converter (trim-safe). The closure receives the target
		// encoding and returns the encoded bytes; the reflection fields above are unused on this path.
		private CrystalJsonContent(object? inputValue, Type inputType, MediaTypeHeaderValue? mediaType, Func<Encoding?, SliceOwner> converterRenderer)
		{
			this.Value = inputValue;
			this.ObjectType = inputType;
			this.Headers.ContentType = mediaType ?? new("application/json") { CharSet = "utf-8" };
			this.JsonSettings = CrystalJsonSettings.JsonIndented;
			this.JsonResolver = CrystalJson.DefaultResolver;
			this.ConverterRenderer = converterRenderer;
		}

		private readonly Func<Encoding?, SliceOwner>? ConverterRenderer;

		/// <summary>Instance that will be returned in the body as serialized JSON</summary>
		public object? Value { get; }

		/// <summary>Type of the instance</summary>
		public Type ObjectType { get;}

		/// <summary>JSON serialization settings</summary>
		private CrystalJsonSettings JsonSettings { get; }

		/// <summary>JSON type resolver</summary>
		private ICrystalJsonTypeResolver JsonResolver { get; }

		/// <summary>Creates a <see cref="CrystalJsonContent"/> that will serialize a <see cref="JsonValue"/> into the body of the HTTP request or response</summary>
		/// <param name="inputValue">Value to serialize</param>
		/// <param name="mediaType">Media type reported in the <c>Content-Type</c> header. Uses <c>"application/json; charset=utf-8"</c> by default.</param>
		/// <param name="settings">Custom JSON serialization settings. Uses <see cref="CrystalJsonSettings.Json"/> by default.</param>
		/// <param name="resolver">Custom JSON type resolver. Uses <see cref="CrystalJson.DefaultResolver"/> by default.</param>
		public static CrystalJsonContent Create(JsonValue? inputValue, MediaTypeHeaderValue? mediaType = null, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new CrystalJsonContent(inputValue ?? JsonNull.Null, inputValue?.GetType() ?? typeof(JsonValue), mediaType, settings, resolver);

		/// <summary>Creates a <see cref="CrystalJsonContent"/> that will serialize an instance into the body of the HTTP request or response</summary>
		/// <param name="inputValue">Instance to serialize</param>
		/// <param name="inputType">Type of the instance. Use <c>typeof(object)</c> if unknown.</param>
		/// <param name="mediaType">Media type reported in the <c>Content-Type</c> header. Uses <c>"application/json; charset=utf-8"</c> by default.</param>
		/// <param name="settings">Custom JSON serialization settings. Uses <see cref="CrystalJsonSettings.Json"/> by default.</param>
		/// <param name="resolver">Custom JSON type resolver. Uses <see cref="CrystalJson.DefaultResolver"/> by default.</param>
		[RequiresUnreferencedCode(ReflectionMessage)]
		[RequiresDynamicCode(ReflectionMessage)]
		public static CrystalJsonContent Create(object? inputValue, Type inputType, MediaTypeHeaderValue? mediaType = null, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new CrystalJsonContent(inputValue, inputType, mediaType, settings, resolver);

		/// <summary>Creates a <see cref="CrystalJsonContent"/> that will serialize an instance of type <typeparamref name="T"/> into the body of the HTTP request or response</summary>
		/// <typeparam name="T">Type of the instance</typeparam>
		/// <param name="inputValue">Instance to serialize</param>
		/// <param name="mediaType">Media type reported in the <c>Content-Type</c> header. Uses <c>"application/json; charset=utf-8"</c> by default.</param>
		/// <param name="settings">Custom JSON serialization settings. Uses <see cref="CrystalJsonSettings.Json"/> by default.</param>
		/// <param name="resolver">Custom JSON type resolver. Uses <see cref="CrystalJson.DefaultResolver"/> by default.</param>
		[RequiresUnreferencedCode(ReflectionMessage)]
		[RequiresDynamicCode(ReflectionMessage)]
		public static CrystalJsonContent Create<T>(T? inputValue, MediaTypeHeaderValue? mediaType = null, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new CrystalJsonContent(inputValue, typeof(T), mediaType, settings, resolver);

		/// <summary>Creates a <see cref="CrystalJsonContent"/> that serializes <paramref name="value"/> of type <typeparamref name="T"/> through a source-generated converter.</summary>
		/// <typeparam name="T">Type of the instance</typeparam>
		/// <param name="value">Instance to serialize</param>
		/// <param name="serializer">Source-generated serializer for <typeparamref name="T"/> (for example <c>MyConverters.T.Default</c>). This is the trim-safe and AoT-safe path.</param>
		/// <param name="mediaType">Media type reported in the <c>Content-Type</c> header. Uses <c>"application/json; charset=utf-8"</c> by default.</param>
		/// <param name="settings">Custom JSON serialization settings. Uses <see cref="CrystalJsonSettings.Json"/> by default.</param>
		/// <param name="resolver">Custom JSON type resolver used for nested types. Uses <see cref="CrystalJson.DefaultResolver"/> by default.</param>
		public static CrystalJsonContent Create<T>(T? value, IJsonSerializer<T> serializer, MediaTypeHeaderValue? mediaType = null, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(serializer);
			var jsonSettings = settings ?? CrystalJsonSettings.JsonIndented;
			return new CrystalJsonContent(value, typeof(T), mediaType, (encoding) =>
				encoding is null || Encoding.UTF8.Equals(encoding)
					? CrystalJson.ToSlice(value, serializer, ArrayPool<byte>.Shared, jsonSettings, resolver)
					: SliceOwner.Create(encoding.GetBytes(CrystalJson.Serialize(value, serializer, jsonSettings, resolver)).AsSlice()));
		}

		internal const string ReflectionMessage = "JSON body serialization uses reflection over the value type. Enroll the type with [CrystalJsonConverter] and build the content from the source-generated converter output.";

		private SliceOwner CachedBytes;

		// The reflection call here is reachable only through the Create(object, Type) and Create<T> factories, both
		// [RequiresUnreferencedCode] so the caller is already warned, or through Create(JsonValue) whose value is a
		// JSON DOM node that serializes without member reflection.
		[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Reachable only via the [RequiresUnreferencedCode] Create factories, or a JsonValue DOM node.")]
		[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Reachable only via the [RequiresDynamicCode] Create factories, or a JsonValue DOM node.")]
		private SliceOwner RenderBytes()
		{
			using var activity = ActivitySource.StartActivity("JSON Serialize");

			Encoding? targetEncoding = GetEncoding(this.Headers.ContentType?.CharSet);

			try
			{
				if (this.ConverterRenderer is not null)
				{ // source-generated converter path (trim-safe)
					var owner = this.ConverterRenderer(targetEncoding);
					activity?.SetTag("json.length", owner.Count);
					return owner;
				}
				else if (targetEncoding != null && !Encoding.UTF8.Equals(targetEncoding))
				{ // exotic encoding!
					string jsonText = CrystalJson.Serialize(this.Value, this.ObjectType, this.JsonSettings, this.JsonResolver);
					var bytes = targetEncoding.GetBytes(jsonText).AsSlice();
					activity?.SetTag("json.length", bytes.Count);
					return SliceOwner.Create(bytes);
				}
				else
				{ // UTF-8: direct to bytes
					var pool = ArrayPool<byte>.Shared;
					var bytes = CrystalJson.ToSlice(this.Value, this.ObjectType, pool, this.JsonSettings, this.JsonResolver);
					activity?.SetTag("json.length", bytes.Count);
					return bytes;
				}
			}
			catch (Exception ex)
			{
				activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
				activity?.AddException(ex);
				throw;
			}
		}

		protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
		{
			var cached = this.CachedBytes;
			if (!cached.IsValid)
			{
				this.CachedBytes = cached = RenderBytes();
			}

			try
			{
				//REVIEW: where can we get a valid cancellation token??
				await stream.WriteAsync(cached.Memory).ConfigureAwait(false);
			}
			finally
			{
				cached.Dispose();
				// we assume that SerializeToStreamAsync will be called only once, and *after* TryComputeLength, so we can return our buffer to the pool
				// => if we are called more than once, we may re-render the bytes several types!
				this.CachedBytes = default;
			}
		}

		protected override bool TryComputeLength(out long length)
		{
			//PERF: if we could know the size (in bytes) of the rendered JSON in advance, we could optimize the request further by setting the Content-Length header...
			// The issue is that if the JSON payload is huge, this will use a lot of memory.
			// => if we had a way to "guess" if the JSON will be small or not, we could decide to pre-render or not.

			var cached = this.CachedBytes;
			if (!cached.IsValid)
			{
				this.CachedBytes = cached = RenderBytes();
			}

			length = cached.Count;
			return true;
		}

		internal static Encoding? GetEncoding(string? charset)
		{
			Encoding? encoding = null;

			if (charset != null)
			{
				try
				{
					// Remove at most a single set of quotes.
					if (charset.Length > 2 && charset[0] == '\"' && charset[^1] == '\"')
					{
						encoding = Encoding.GetEncoding(charset[1..^1]);
					}
					else
					{
						encoding = Encoding.GetEncoding(charset);
					}
				}
				catch (ArgumentException e)
				{
					throw new InvalidOperationException("Invalid charset", e);
				}

				Contract.Debug.Ensures(encoding != null);
			}

			return encoding;
		}

	}

}
