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

// ReSharper disable RedundantTypeArgumentsOfMethod

namespace SnowBank.Data.Json
{
	using System.ComponentModel;

	public sealed partial class JsonArray
	{

		#region Parse -> ParseArray

		/// <inheritdoc cref="JsonValue.ParseArray(string?,CrystalJsonSettings?)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new static JsonArray Parse(
#if NET8_0_OR_GREATER
			[StringSyntax(StringSyntaxAttribute.Json)]
#endif
			string? jsonText,
			CrystalJsonSettings? settings = null
		) => JsonValue.ParseArray(jsonText, settings);

		/// <inheritdoc cref="JsonValue.ParseArray(ReadOnlySpan{char},CrystalJsonSettings?)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new static JsonArray Parse(
#if NET8_0_OR_GREATER
			[StringSyntax(StringSyntaxAttribute.Json)]
#endif
			ReadOnlySpan<char> jsonText,
			CrystalJsonSettings? settings = null
		) => JsonValue.ParseArray(jsonText, settings);

		/// <inheritdoc cref="JsonValue.ParseArray(ReadOnlySpan{byte},CrystalJsonSettings?)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new static JsonArray Parse(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null)
			=> JsonValue.ParseArray(jsonBytes, settings);

		/// <inheritdoc cref="JsonValue.ParseArray(System.Slice,CrystalJsonSettings?)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new static JsonArray Parse(Slice jsonBytes, CrystalJsonSettings? settings = null)
			=> JsonValue.ParseArray(jsonBytes, settings);

		/// <inheritdoc cref="JsonValue.Parse(string,IFormatProvider?)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new static JsonArray Parse(string jsonText, IFormatProvider? provider)
			=> JsonValue.Parse(jsonText, provider).AsArray();

		/// <inheritdoc cref="JsonValue.Parse(ReadOnlySpan{char},IFormatProvider?)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new static JsonArray Parse(ReadOnlySpan<char> jsonText, IFormatProvider? provider)
			=> JsonValue.Parse(jsonText, provider).AsArray();

		#endregion

		#region Hide ParseArray

		// we use EB Never to hide redundant ParseArray methods that come from the base class

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

		#region Hide ParseObject

		// we use EB Never to hide unwanted ParseObject methods that come from the base class

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

	}

}
