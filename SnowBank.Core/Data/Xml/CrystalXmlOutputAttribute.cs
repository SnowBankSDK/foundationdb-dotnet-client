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

	/// <summary>Requests <b>XML</b> output from a <see cref="CrystalConverterAttribute"/> container, and carries the parameters of that format</summary>
	/// <remarks>
	/// <para>Applied on the same partial container class that carries <c>[CrystalConverter]</c> and one or more <c>[CrystalSerializable(...)]</c> attributes. When present, the generator emits a <c>WriteXml&lt;TEmitter&gt;</c> body and the XML output methods (<c>ToXmlText</c>, <c>ToXmlSlice</c>, <c>ToXmlBytes</c>, <c>WriteXmlTo</c>, <c>ToXDocument</c>) for each generated type.</para>
	/// <para>Combine it with <c>[CrystalJsonOutput]</c> on the same container to produce both formats from one set of enrolled types; alone, the container produces XML and nothing else. It does not combine with the mono-format <c>[CrystalJsonConverter]</c> alias (<c>CRYS0002</c>).</para>
	/// <para>XML output is strictly opt-in: a container that never names this attribute produces no XML.</para>
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class)]
	[PublicAPI]
	public sealed class CrystalXmlOutputAttribute : CrystalOutputAttribute
	{

		/// <summary>Enables XML output for this serializer container, deriving all defaults from the container's JSON profile</summary>
		public CrystalXmlOutputAttribute()
		{ }

		/// <summary>XML format produced for the types declared in this container</summary>
		/// <remarks>Defaults to <see cref="CrystalXmlOutputProfile.Default"/>, which derives the profile from the container's JSON profile: a DataContract-compatible JSON profile (ex: <c>DataContractCompat</c>) produces the <see cref="CrystalXmlOutputProfile.DataContract"/> format, any other JSON profile (ex: <c>Web</c>, standard) produces the <see cref="CrystalXmlOutputProfile.Modern"/> format. An explicit override is possible, but combining it with an incompatible JSON profile (ex: a naming policy together with the DataContract format) is a compilation error.</remarks>
		public CrystalXmlOutputProfile Profile { get; set; }

		/// <summary>Default representation for dictionary-like members of this container that do not override it with <see cref="XmlPropertyAttribute.DictionaryFormat"/></summary>
		public CrystalXmlDictionaryFormat DictionaryFormat { get; set; }

	}

	/// <summary>Mono-format alias that marks a partial class as a container of source-generated <b>XML</b> serializers</summary>
	/// <remarks>
	/// <para>Exactly equivalent to <c>[CrystalConverter]</c> plus <c>[CrystalXmlOutput(...)]</c> with the same parameters, and the shortest spelling for a container that only ever produces XML: no JSON entry point, no JSON proxy and no JSON facet is generated for its types.</para>
	/// <para>Being mono-format, it does not combine with another output format: pairing it with <c>[CrystalJsonOutput]</c> is refused (<c>CRYS0002</c>). A container that produces both formats spells out <c>[CrystalConverter]</c>, <c>[CrystalJsonOutput]</c> and <c>[CrystalXmlOutput]</c>.</para>
	/// <para>Sample: <code>
	/// [CrystalXmlConverter(Profile = CrystalXmlOutputProfile.DataContract)]
	/// [CrystalSerializable(typeof(LegacyOrder))]
	/// public static partial class LegacyRenderSerializers
	/// {
	///		// generated code will be inserted here
	/// }
	/// </code></para>
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class)]
	[PublicAPI]
	public sealed class CrystalXmlConverterAttribute : CrystalConverterAttribute
	{

		/// <summary>Use this class as a container for source-generated XML serializers</summary>
		public CrystalXmlConverterAttribute() { }

		/// <inheritdoc cref="CrystalXmlOutputAttribute.Profile"/>
		/// <remarks>An XML-only container derives no JSON profile: an unspecified profile resolves to <see cref="CrystalXmlOutputProfile.Modern"/>.</remarks>
		public CrystalXmlOutputProfile Profile { get; set; }

		/// <inheritdoc cref="CrystalXmlOutputAttribute.DictionaryFormat"/>
		public CrystalXmlDictionaryFormat DictionaryFormat { get; set; }

	}

	/// <summary>Specifies which XML format shape a <see cref="CrystalXmlOutputAttribute"/> container produces</summary>
	[PublicAPI]
	public enum CrystalXmlOutputProfile
	{

		/// <summary>Derive the XML format from the container's JSON profile</summary>
		Default = 0,

		/// <summary>The XML a reader of the equivalent JSON would predict: element names follow the same naming policy as the JSON, a <see langword="null"/> member is absent by default, and dictionaries default to <see cref="CrystalXmlDictionaryFormat.Direct"/></summary>
		Modern,

		/// <summary>The format produced by <see cref="System.Runtime.Serialization.DataContractSerializer"/>: data contract names, ordinal member ordering, explicit <c>nil</c> markers, and unhashed <c>KeyValueOfXY</c> dictionary elements</summary>
		DataContract,

	}

}
