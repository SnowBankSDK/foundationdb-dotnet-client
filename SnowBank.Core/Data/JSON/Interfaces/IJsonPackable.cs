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
	/// <summary>Supports custom JSON serialization into <see cref="JsonValue"/>.</summary>
	/// <remarks>
	/// <para>Types that handle their own custom serialization should typically implement this interface, as well as <see cref="IJsonSerializable"/> and <see cref="IJsonDeserializable{TSelf}"/>.</para>
	/// </remarks>
	[PublicAPI]
	public interface IJsonPackable
	{

		/// <summary>Converts an instance of this type into the equivalent JSON value, usually a JSON Object or Array</summary>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <remarks>If the type also implements <see cref="IJsonDeserializable{TSelf}"/>, the result of <see cref="IJsonDeserializable{TSelf}.JsonDeserialize">parsing</see> the resulting JSON value should produce an object that is equivalent to the original (or as close as possible if some values loose some precision after serialization)</remarks>
		JsonValue JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver);

	}

}
