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
	/// <para>Combine it with <c>[CrystalJsonOutput]</c> on the same container to produce both formats from one set of registered types; alone, the container produces XML and nothing else. It does not combine with the mono-format <c>[CrystalJsonConverter]</c> alias (<c>CRYS0002</c>).</para>
	/// <para>XML output is strictly opt-in: a container that never names this attribute produces no XML.</para>
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class)]
	[PublicAPI]
	public sealed class CrystalXmlOutputAttribute : CrystalOutputAttribute
	{

		/// <summary>Enables XML output for this serializer container, deriving all defaults from the container's JSON profile</summary>
		public CrystalXmlOutputAttribute()
		{ }

		/// <summary>Enables XML output for this serializer container</summary>
		/// <param name="defaults">Default settings used for the generated converters</param>
		public CrystalXmlOutputAttribute(CrystalXmlSerializerDefaults defaults)
		{ }

		/// <summary>Default representation for dictionary-like members of this container that do not override it with <see cref="XmlPropertyAttribute.DictionaryFormat"/></summary>
		public CrystalXmlDictionaryFormat DictionaryFormat { get; set; }

		/// <summary>Strips namespaces and prefixes from the <see cref="CrystalXmlSerializerDefaults.DataContractCompat"/> output, keeping the rest of that format</summary>
		/// <remarks>
		/// <para>Under this option the DataContract profile writes <b>no XML declaration, no prefix and no <c>xmlns</c>, ever</b>:
		/// element names keep their local name only, the <c>nil</c> and <c>type</c> attributes are written bare, and a
		/// <c>type</c> value is the local contract name with no namespace half. Everything else about the format is unchanged,
		/// which is the point: the null markers, the member order, the lexical forms and the <c>KeyValueOfXY</c> entries all
		/// stay.</para>
		/// <para>It exists for one reason: an application whose stored documents were written by a namespace-free writer
		/// reads them back by local name. A namespaced document would not match. So this is the output to name when the
		/// documents already exist, and the default is the output to name for anything new.</para>
		/// <para>What it costs: <c>type="RangeCriterion"</c> has lost the namespace half of its qualified name, so two derived
		/// types with the same local name in different contract namespaces become the same annotation in the output.</para>
		/// <para>Generation-time, like the <see cref="CrystalXmlSerializerDefaults"/> preset: the generated code bakes its names
		/// as frozen literals, so a name exists with a namespace or without one and never both. On
		/// <see cref="CrystalXmlSerializerDefaults.General"/>, which has no namespaces to strip, the option does nothing and the
		/// generator says so (<c>CXML0012</c>).</para>
		/// </remarks>
		public bool OmitNamespaces { get; set; }

	}

	/// <summary>Mono-format alias that marks a partial class as a container of source-generated <b>XML</b> serializers</summary>
	/// <remarks>
	/// <para>Exactly equivalent to <c>[CrystalConverter]</c> plus <c>[CrystalXmlOutput(...)]</c> with the same parameters, and the shortest spelling for a container that only ever produces XML: no JSON entry point, no JSON proxy and no JSON facet is generated for its types.</para>
	/// <para>Being mono-format, it does not combine with another output format: pairing it with <c>[CrystalJsonOutput]</c> is rejected (<c>CRYS0002</c>). A container that produces both formats spells out <c>[CrystalConverter]</c>, <c>[CrystalJsonOutput]</c> and <c>[CrystalXmlOutput]</c>.</para>
	/// <para>Sample: <code>
	/// [CrystalXmlConverter(CrystalXmlSerializerDefaults.DataContractCompat)]
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

		/// <inheritdoc cref="CrystalXmlOutputAttribute(CrystalXmlSerializerDefaults)"/>
		/// <remarks>An XML-only container derives no JSON profile: an unspecified preset resolves to <see cref="CrystalXmlSerializerDefaults.General"/>.</remarks>
		public CrystalXmlConverterAttribute(CrystalXmlSerializerDefaults defaults) { }

		/// <inheritdoc cref="CrystalXmlOutputAttribute.DictionaryFormat"/>
		public CrystalXmlDictionaryFormat DictionaryFormat { get; set; }

		/// <inheritdoc cref="CrystalXmlOutputAttribute.OmitNamespaces"/>
		public bool OmitNamespaces { get; set; }

	}

	/// <summary>List of default configurations for source-generated XML converters</summary>
	public enum CrystalXmlSerializerDefaults
	{

		/// <summary>Derive the XML format from the container's JSON profile: a <see cref="SnowBank.Data.Json.CrystalJsonSerializerDefaults.DataContractCompat"/> JSON profile yields the DataContract XML format, anything else yields <see cref="General"/></summary>
		Inherit = 0,

		/// <summary>The standard, neutral XML format: element names follow the container's JSON naming policy, a <see langword="null"/> member is absent by default, and dictionaries default to <see cref="CrystalXmlDictionaryFormat.Direct"/></summary>
		General = 1,

		/// <summary>The format produced by <see cref="System.Runtime.Serialization.DataContractSerializer"/>: contract namespaces, data contract names, ordinal member ordering, <c>i:nil</c> and <c>i:type</c> markers, qualified-name type annotations, and unhashed <c>KeyValueOfXY</c> dictionary elements</summary>
		/// <remarks>A container with this preset produces what <c>DataContractSerializer</c> would have produced for the same type, POCO or <c>[DataContract]</c> alike. Element names come from the data contract, so combining this preset with a naming policy is rejected at build time. A declaration this output can prove nothing uses is omitted, and the ones that remain are written on the
		/// first element that needs them, so a document carries fewer declarations than the reference serializer writes and the
		/// same expanded names. To strip namespaces and prefixes altogether, see <see cref="CrystalXmlOutputAttribute.OmitNamespaces"/>.</remarks>
		DataContractCompat = 2,

	}

}
