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

namespace SnowBank.Data.Xml
{

	/// <summary>Marker attribute that enables source-generated XML output for every type declared in the annotated serializer container</summary>
	/// <remarks>
	/// <para>Applied on the same partial container class that already carries <c>[CrystalJsonConverter]</c> and one or more <c>[CrystalJsonSerializable(...)]</c> attributes. When present, the generator emits, in addition to the JSON reader/writer, a <c>WriteXml&lt;TEmitter&gt;</c> body and the XML output methods (<c>ToXmlText</c>, <c>ToXmlSlice</c>, <c>ToXmlBytes</c>, <c>WriteXmlTo</c>, <c>ToXDocument</c>) for each generated type.</para>
	/// <para>Without this attribute, a container only produces JSON: the XML vocabulary is strictly opt-in.</para>
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class)]
	[PublicAPI]
	public sealed class CrystalXmlOutputAttribute : Attribute
	{

		/// <summary>Enables XML output for this serializer container, deriving all defaults from the container's JSON profile</summary>
		public CrystalXmlOutputAttribute()
		{ }

		/// <summary>XML wire produced for the types declared in this container</summary>
		/// <remarks>Defaults to <see cref="XmlOutputProfile.Default"/>, which derives the profile from the container's JSON profile: a DataContract-compatible JSON profile (ex: <c>DataContractCompat</c>) produces the <see cref="XmlOutputProfile.DataContract"/> wire, any other JSON profile (ex: <c>Web</c>, standard) produces the <see cref="XmlOutputProfile.Modern"/> wire. An explicit override is possible, but combining it with an incompatible JSON profile (ex: a naming policy together with the DataContract wire) is a compilation error.</remarks>
		public XmlOutputProfile Profile { get; set; }

		/// <summary>Default representation for dictionary-like members of this container that do not override it with <see cref="XmlPropertyAttribute.DictionaryFormat"/></summary>
		public XmlDictionaryFormat DictionaryFormat { get; set; }

	}

	/// <summary>Specifies which XML wire shape a <see cref="CrystalXmlOutputAttribute"/> container produces</summary>
	[PublicAPI]
	public enum XmlOutputProfile
	{

		/// <summary>Derive the XML wire from the container's JSON profile</summary>
		Default = 0,

		/// <summary>The XML a reader of the equivalent JSON would predict: element names follow the same naming policy as the JSON, a <see langword="null"/> member is absent by default, and dictionaries default to <see cref="XmlDictionaryFormat.Direct"/></summary>
		Modern,

		/// <summary>The wire produced by <see cref="System.Runtime.Serialization.DataContractSerializer"/>: data contract names, ordinal member ordering, explicit <c>nil</c> markers, and unhashed <c>KeyValueOfXY</c> dictionary elements</summary>
		DataContract,

	}

}
