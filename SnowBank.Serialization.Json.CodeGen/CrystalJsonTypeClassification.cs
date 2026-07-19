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

namespace SnowBank.Serialization.Json.CodeGen
{

	/// <summary>Type of JsonValue instance that would be used to represent a serialized type</summary>
	public enum JsonPrimitiveType
	{
		/// <summary>The type is not a JSON value</summary>
		None,
		/// <summary>The type can be anything derived from JsonValue</summary>
		Value,
		/// <summary>The type is an instance of JsonObject</summary>
		Object,
		/// <summary>The type is an instance of JsonArray</summary>
		Array,
		/// <summary>The type is an instance of JsonString</summary>
		String,
		/// <summary>The type is an instance of JsonNumber</summary>
		Number,
		/// <summary>The type is an instance of JsonBoolean</summary>
		Boolean,
		/// <summary>The type is an instance of JsonDateTime</summary>
		DateTime
	}

	/// <summary>CrystalJson-specific classification of a <see cref="TypeMetadata"/>, kept out of the generic type descriptor.</summary>
	/// <remarks>These recompute from the generic metadata (namespace, name, interfaces) so that <see cref="TypeMetadata"/> itself carries no CrystalJson knowledge.</remarks>
	public static class CrystalJsonTypeClassification
	{

		/// <summary>Returns the kind of <c>JsonValue</c> this type is (or one of its derived types), or <see cref="JsonPrimitiveType.None"/> if it is not a JSON value.</summary>
		public static JsonPrimitiveType JsonType(this TypeMetadata type)
		{
			if (type.NameSpace != KnownTypeSymbols.CrystalJsonNamespace)
			{
				return JsonPrimitiveType.None;
			}

			return type.Name switch
			{
				KnownTypeSymbols.JsonValueName => JsonPrimitiveType.Value,
				KnownTypeSymbols.JsonObjectName => JsonPrimitiveType.Object,
				KnownTypeSymbols.JsonArrayName => JsonPrimitiveType.Array,
				KnownTypeSymbols.JsonStringName => JsonPrimitiveType.String,
				KnownTypeSymbols.JsonBooleanName => JsonPrimitiveType.Boolean,
				KnownTypeSymbols.JsonNumberName => JsonPrimitiveType.Number,
				KnownTypeSymbols.JsonDateTimeName => JsonPrimitiveType.DateTime,
				_ => JsonPrimitiveType.None,
			};
		}

		/// <summary>Tests if this type implements <c>IJsonPackable</c></summary>
		public static bool IsJsonPackable(this TypeMetadata type) => ImplementsCrystalJsonInterface(type, "IJsonPackable");

		/// <summary>Tests if this type implements <c>IJsonSerializable</c></summary>
		public static bool IsJsonSerializable(this TypeMetadata type) => ImplementsCrystalJsonInterface(type, "IJsonSerializable");

		/// <summary>Tests if this type implements <c>IJsonDeserializable&lt;T&gt;</c></summary>
		public static bool IsJsonDeserializable(this TypeMetadata type) => ImplementsCrystalJsonInterface(type, "IJsonDeserializable");

		private static bool ImplementsCrystalJsonInterface(TypeMetadata type, string interfaceName)
		{
			foreach (var iface in type.Interfaces)
			{
				if (iface.Name == interfaceName && iface.NameSpace == KnownTypeSymbols.CrystalJsonNamespace)
				{
					return true;
				}
			}
			return false;
		}

	}

}
