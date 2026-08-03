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

	/// <summary>Attribute that controls how a field or property is projected into XML output</summary>
	/// <remarks>
	/// <para>This is where every XML-only concern lives: the existing JSON vocabulary (<c>[JsonProperty]</c>, <c>[JsonPropertyName]</c>, <c>[JsonIgnore]</c>, ...) is never modified by this feature. The resolution ladder, per setting (never all-or-nothing), is: (1) the defaults of the container's <see cref="XmlOutputProfile"/>; (2) the JSON vocabulary, which supplies the wire <em>name</em> as-is (never re-transformed by a naming policy); (3) this attribute, which overrides option by option (a lone <see cref="ItemName"/> still lets <see cref="Name"/> fall back to step 2, then to the naming-policy-transformed .NET member name).</para>
	/// </remarks>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
	[PublicAPI]
	public sealed class XmlPropertyAttribute : Attribute
	{

		/// <summary>Configure how this field or property is projected into XML</summary>
		public XmlPropertyAttribute()
		{ }

		/// <summary>Configure how this field or property is projected into XML</summary>
		public XmlPropertyAttribute(string name)
		{
			this.Name = name;
		}

		/// <summary>XML name for this member</summary>
		/// <remarks>Sugar: a leading <c>"@"</c> (ex: <c>"@id"</c>) is normalized at compile time into <see cref="Name"/> without the prefix, with <see cref="Attribute"/> set to <see langword="true"/>. This runtime attribute only ever stores the already-normalized value; the generator performs the substitution.</remarks>
		public string? Name { get; set; }

		/// <summary>Projects this member as an XML ATTRIBUTE (annotation) instead of a nested element</summary>
		/// <remarks>Scalars only; forbidden on a complex type or a collection. Also forbidden under the <see cref="XmlOutputProfile.DataContract"/> profile, which has no concept of a user-facing XML attribute.</remarks>
		public bool Attribute { get; set; }

		/// <summary>Name used for collection items and dictionary entries</summary>
		/// <remarks>Setting this on a collection member switches it to the wrapped form (ex: <c>&lt;tags&gt;&lt;tag&gt;sf&lt;/tag&gt;&lt;/tags&gt;</c>). This is a purely XML concept: it never flows back into the JSON vocabulary.</remarks>
		public string? ItemName { get; set; }

		/// <summary>Dictionary representation for this member</summary>
		/// <remarks><see cref="XmlDictionaryFormat.Default"/> inherits from the profile, or from the container's <see cref="CrystalXmlOutputAttribute.DictionaryFormat"/>.</remarks>
		public XmlDictionaryFormat DictionaryFormat { get; set; }

	}

}
