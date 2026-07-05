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

	/// <summary>Supports custom JSON deserialization from <see cref="JsonValue"/> into instances of <typeparamref name="TSelf"/></summary>
	/// <remarks>
	/// <para>Types that handle their own custom serialization should typically implement this interface, as well as <see cref="IJsonSerializable"/> and <see cref="IJsonPackable"/>.</para>
	/// <para>In case where the original type <typeparamref name="TSelf"/> cannot be modified, and custom serialization is required, see <see cref="IJsonDeserializer{T}"/>.</para>
	/// </remarks>
	[PublicAPI]
	public interface IJsonDeserializable<out TSelf>
	{

#if NET7_0_OR_GREATER
		// static abstract interface members need runtime support that netstandard2.0/netfx lacks: there the interface is a marker only, and callers locate a static 'JsonDeserialize' method via reflection instead
		/// <summary>Deserializes a parsed <see cref="JsonValue"/> into an instance of type <typeparamref name="TSelf"/>.</summary>
		/// <param name="value">JSON value that will be bound to the new instance</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>A new instance of <typeparamref name="TSelf"/> that has been initialized from the contents of <paramref name="value"/>.</returns>
		static abstract TSelf JsonDeserialize(JsonValue value, ICrystalJsonTypeResolver? resolver);
#endif

	}

}
