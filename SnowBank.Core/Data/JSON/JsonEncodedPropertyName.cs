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
	using SnowBank.Text;

	/// <summary>Cache for the various encoded forms of a JSON property name</summary>
	[DebuggerDisplay("{Value}")]
	public sealed class JsonEncodedPropertyName : IEquatable<JsonEncodedPropertyName>, IEquatable<string>, IFormattable
#if NET9_0_OR_GREATER
		, IEquatable<ReadOnlySpan<char>>, IEquatable<ReadOnlyMemory<char>>
#endif
	{

		/// <summary>Constructs an instance from the specified property name</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonEncodedPropertyName(string value)
		{
			var camelCased = CrystalJsonWriter.CamelCase(value);
			this.Value = value;
			this.ValueCamelCased = camelCased;
			this.JsonLiteral = JsonEncoding.Encode(value);
			this.JsonLiteralCamelCased = ReferenceEquals(value, camelCased) ? this.JsonLiteral : JsonEncoding.Encode(camelCased);
			this.JavaScriptLiteral = JavaScriptEncoding.EncodePropertyName(value);
			this.JavaScriptLiteralCamelCased = ReferenceEquals(value, camelCased) ? JavaScriptLiteral : JavaScriptEncoding.EncodePropertyName(camelCased);
		}

		/// <summary>Original non-encoded name of the property (ex: `<c>FooBar</c>` or `<c>hello'world"!!!</c>`)</summary>
		public readonly string Value;

		/// <summary>Camel-cased non-encoded name of the property (ex: `<c>fooBar</c>` or `<c>hello'world"!!!</c>`)</summary>
		public readonly string ValueCamelCased;

		/// <summary>Name encoded as the field name of a JSON Object (ex: `<c>"FooBar"</c>` or `<c>"hello'world\"!!!"</c>`)</summary>
		/// <remarks>Includes the double-quotes</remarks>
		public readonly string JsonLiteral;

		/// <summary>Camel-cased name encoded as the field name of a JSON Object (ex: `<c>"fooBar"</c>` or `<c>"hello'world\"!!!"</c>`)</summary>
		/// <remarks>Includes the double-quotes</remarks>
		public readonly string JsonLiteralCamelCased;

		/// <summary>Name encoded as the field name of a Javascript Object (ex: <c>FooBar</c> (no quotes) or <c>'hello\'world"!!!'</c> (with quotes))</summary>
		/// <remarks>Includes the single quotes <i>if required</i></remarks>
		public readonly string JavaScriptLiteral;

		/// <summary>Camel-cased name encoded as the field name of a Javascript Object (ex: <c>fooBar</c> (no quotes) or <c>'hello\'world"!!!'</c> (with quotes))</summary>
		/// <remarks>Includes the single quotes <i>if required</i></remarks>
		public readonly string JavaScriptLiteralCamelCased;

		/// <inheritdoc />
		public override string ToString() => this.Value;

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => this.Value;

		/// <inheritdoc />
		public override int GetHashCode() => this.Value.GetHashCode();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj is JsonEncodedPropertyName name && name.Value == this.Value;

		/// <inheritdoc />
		public bool Equals(JsonEncodedPropertyName? other) => other is not null && other.Value == this.Value;

		/// <inheritdoc />
		public bool Equals(string? other) => other == this.Value;

#if NET9_0_OR_GREATER
		/// <inheritdoc />
#else
		/// <inheritdoc cref="Equals(string?)"/>
#endif
		public bool Equals(ReadOnlySpan<char> other) => other.SequenceEqual(this.Value.AsSpan());

#if NET9_0_OR_GREATER
		/// <inheritdoc />
#else
		/// <inheritdoc cref="Equals(string?)"/>
#endif
		public bool Equals(ReadOnlyMemory<char> other) => other.Span.SequenceEqual(this.Value.AsSpan());

	}

}
