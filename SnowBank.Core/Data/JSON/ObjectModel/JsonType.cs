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
	/// <summary>Type of JSON value (String, Number, Boolean, Object, Array, Null, ...)</summary>
	public enum JsonType
	{
		//IMPORTANT: the order of these values has an impact on the ordering of JSON values when sorting (except Null which is always last)

		/// <summary>Empty value, can either be an <see cref="JsonNull.Null">explicit null</see> (usually denoted by the <c>null</c> literal in the source document), a <see cref="JsonNull.Missing">missing value</see> (ex: <c>obj["does_not_exist"]</c>), or <see cref="JsonNull.Error">an error</see> of some kind</summary>
		/// <remarks>The <see cref="JsonNull.Missing"/> is used when chaining (ex: <c>obj["foo"]["bar"]["baz"]) and allows distinguishing between explicit null entries denoted by <see cref="JsonNull.Null"/>, vs missing fields.</c></remarks>
		Null,

		/// <summary>JSON Boolean, either <see cref="JsonBoolean.True"/> or <see cref="JsonBoolean.False"/>.</summary>
		Boolean,

		/// <summary>JSON Number, any integer, floating point, or decimal value.</summary>
		/// <remarks>Can express integer types up to <see cref="ulong.MaxValue"/>, which may not be compatible with remote targets like JavaScript or Python.</remarks>
		Number,

		/// <summary>[EXTENSION] JSON Date, with or without a timezone.</summary>
		/// <remarks>
		/// <para>This type only exists in memory while serializing CLR objects (for optimization purpose), and is serialized as a string literal.</para>
		/// <para>The same field will be parsed back as a <see cref="JsonType.String"/> value.</para>
		/// <para>This type behaves the same way as a <see cref="JsonString"/>, and the caller should not use this type explicitly.</para>
		/// </remarks>
		DateTime,

		/// <summary>JSON String literal, a sequence of zero or more Unicode characters.</summary>
		/// <remarks>Includes the empty string (<c>""</c>), but cannot be null.</remarks>
		String,

		/// <summary>JSON Array, an ordered list of zero or more elements.</summary>
		/// <remarks>Includes the empty array (<c>[]</c>), but cannot be null.</remarks>
		Array,

		/// <summary>JSON Object, an unordered set of name/values pairs.</summary>
		/// <remarks>Includes the empty object (<c>{}</c>), but cannot be null.</remarks>
		Object,

	}

}
