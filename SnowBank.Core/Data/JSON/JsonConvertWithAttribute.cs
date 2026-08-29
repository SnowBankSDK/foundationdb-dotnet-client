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

	/// <summary>Attaches a CrystalJson custom converter to a member or a type</summary>
	/// <remarks>
	/// <para>The named type is honored for whichever of <see cref="IJsonPacker{T}"/> (packing) and <see cref="IJsonDeserializer{T}"/> (deserializing) it
	/// implements for the decorated member's type (or the decorated type itself) - a type that is only ever written or only ever read may implement a
	/// single facet, and any attempt to use the missing direction fails with an exception naming the facet to implement. The common symmetric case
	/// implements both, e.g. via the <see cref="IJsonMemberConverter{T}"/> bundle or a source-generated <see cref="IJsonConverter{T}"/>. A type
	/// implementing neither facet throws: at metadata construction on the reflection path, and as build error CJSON0010 through the source
	/// generator.</para>
	/// <para>When to use which attribute: the System.Text.Json spelling <c>[JsonConverter(typeof(...))]</c> is also recognized, but it is only legitimate
	/// when the named converter is valid for System.Text.Json as well (both serializers then do the same thing). A CrystalJson-only converter behind the
	/// STJ attribute poisons the type for STJ (runtime <c>InvalidOperationException</c>, or a build error from the STJ source generator); such converters
	/// must use THIS attribute instead, which System.Text.Json never inspects.</para>
	/// <para>When both this attribute and a foreign spelling are present, this one wins.</para>
	/// </remarks>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct)]
	[PublicAPI]
	public sealed class JsonConvertWithAttribute : Attribute
	{

		public JsonConvertWithAttribute(Type converterType)
		{
			Contract.NotNull(converterType);
			this.ConverterType = converterType;
		}

		/// <summary>Type of the converter, which must implement the <see cref="IJsonPacker{T}"/> + <see cref="IJsonDeserializer{T}"/> pair</summary>
		public Type ConverterType { get; }

	}

}
