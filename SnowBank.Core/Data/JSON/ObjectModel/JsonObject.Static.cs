#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

// ReSharper disable RedundantTypeArgumentsOfMethod

namespace SnowBank.Data.Json
{
	using System.ComponentModel;

	public sealed partial class JsonObject
	{

		#region Parse aliasing...

		#region Parse -> ParseObject

		/// <inheritdoc cref="JsonValue.ParseObject(string?,CrystalJsonSettings?)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new static JsonObject Parse(
#if NET8_0_OR_GREATER
			[StringSyntax(StringSyntaxAttribute.Json)]
#endif
			string? jsonText,
			CrystalJsonSettings? settings = null
		) => JsonValue.ParseObject(jsonText, settings);

		/// <inheritdoc cref="JsonValue.ParseObject(ReadOnlySpan{char},CrystalJsonSettings?)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new static JsonObject Parse(
#if NET8_0_OR_GREATER
			[StringSyntax(StringSyntaxAttribute.Json)]
#endif
			ReadOnlySpan<char> jsonText,
			CrystalJsonSettings? settings = null
		) => JsonValue.ParseObject(jsonText, settings);

		/// <inheritdoc cref="JsonValue.ParseObject(ReadOnlySpan{byte},CrystalJsonSettings?)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new static JsonObject Parse(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null)
			=> JsonValue.ParseObject(jsonBytes, settings);

		/// <inheritdoc cref="JsonValue.ParseObject(System.Slice,CrystalJsonSettings?)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new static JsonObject Parse(Slice jsonBytes, CrystalJsonSettings? settings = null)
			=> JsonValue.ParseObject(jsonBytes, settings);

		/// <inheritdoc cref="JsonValue.Parse(string,IFormatProvider?)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new static JsonObject Parse(string jsonText, IFormatProvider? provider)
			=> JsonValue.Parse(jsonText, provider).AsObject();

		/// <inheritdoc cref="JsonValue.Parse(ReadOnlySpan{char},IFormatProvider?)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new static JsonObject Parse(ReadOnlySpan<char> jsonText, IFormatProvider? provider)
			=> JsonValue.Parse(jsonText, provider).AsObject();

		#endregion

		#region Hide ParseObject

		// we use EB Never to hide redundant ParseObject methods that come from the base class

		[EditorBrowsable(EditorBrowsableState.Never)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new static JsonObject ParseObject(string? jsonText, CrystalJsonSettings? settings = null) => JsonValue.ParseObject(jsonText, settings);

		[EditorBrowsable(EditorBrowsableState.Never)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new static JsonObject ParseObject(ReadOnlySpan<char> jsonText, CrystalJsonSettings? settings = null) => JsonValue.ParseObject(jsonText, settings);

		[EditorBrowsable(EditorBrowsableState.Never)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new static JsonObject ParseObject(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null) => JsonValue.ParseObject(jsonBytes, settings);

		[EditorBrowsable(EditorBrowsableState.Never)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new static JsonObject ParseObject(Slice jsonBytes, CrystalJsonSettings? settings = null) => JsonValue.ParseObject(jsonBytes, settings);

		#endregion

		#region Hide ParseArray

		// we use EB Never to hide unwanted ParseArray methods that come from the base class

		[EditorBrowsable(EditorBrowsableState.Never)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new static JsonArray ParseArray(string? jsonText, CrystalJsonSettings? settings = null) => JsonValue.ParseArray(jsonText, settings);

		[EditorBrowsable(EditorBrowsableState.Never)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new static JsonArray ParseArray(ReadOnlySpan<char> jsonText, CrystalJsonSettings? settings = null) => JsonValue.ParseArray(jsonText, settings);

		[EditorBrowsable(EditorBrowsableState.Never)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new static JsonArray ParseArray(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null) => JsonValue.ParseArray(jsonBytes, settings);

		[EditorBrowsable(EditorBrowsableState.Never)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new static JsonArray ParseArray(Slice jsonBytes, CrystalJsonSettings? settings = null) => JsonValue.ParseArray(jsonBytes, settings);

		#endregion

		#endregion

		#region Load from file...

		public static JsonObject LoadFrom(string path, CrystalJsonSettings? settings = null)
		{
			Contract.NotNullOrWhiteSpace(path);
			return (JsonObject) CrystalJson.LoadAndParseInternal(path, settings);
		}

		public static Task<JsonObject> LoadFromAsync(string path, CancellationToken ct) => LoadFromAsync(path, null, ct);

		private static InvalidOperationException ErrorFileDoesNotContainExpectedObject(JsonValue value) => new($"Specified JSON file was loaded, but was of type {value.Type} instead of expected type Object");

		public static async Task<JsonObject> LoadFromAsync(string path, CrystalJsonSettings? settings, CancellationToken ct)
		{
			Contract.NotNullOrWhiteSpace(path);
			ct.ThrowIfCancellationRequested();

			var value = await CrystalJson.LoadAndParseInternalAsync(path, settings, CrystalJson.LoadOptions.None, ct).ConfigureAwait(false);
			return value as JsonObject ?? throw ErrorFileDoesNotContainExpectedObject(value);
		}

		public static async Task<JsonObject?> TryLoadFromAsync(string path, CrystalJsonSettings? settings, CancellationToken ct)
		{
			Contract.NotNullOrWhiteSpace(path);
			ct.ThrowIfCancellationRequested();

			var value = await CrystalJson.LoadAndParseInternalAsync(path, settings, CrystalJson.LoadOptions.ReturnNullIfMissing, ct).ConfigureAwait(false);

			// if file is missing, we should have a JsonNull
			if (value is JsonNull)
			{
				return settings.IsReadOnly() ? JsonObject.ReadOnly.Empty : JsonObject.Create();
			}

			// otherwise, it should be our object
			return value as JsonObject ?? throw ErrorFileDoesNotContainExpectedObject(value);
		}

		#endregion

	}

}
