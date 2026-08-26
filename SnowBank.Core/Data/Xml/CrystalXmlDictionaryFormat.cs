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

	/// <summary>Specifies how a dictionary-like member is represented in XML output</summary>
	[PublicAPI]
	public enum CrystalXmlDictionaryFormat
	{

		/// <summary>Inherits the setting from the enclosing profile, or from the container's <see cref="CrystalXmlOutputAttribute.DictionaryFormat"/></summary>
		Default = 0,

		/// <summary>Each entry is a nested element named after the key: <c>&lt;scores&gt;&lt;math&gt;12&lt;/math&gt;&lt;/scores&gt;</c></summary>
		/// <remarks>The default for the <see cref="CrystalXmlSerializerDefaults.General"/> profile. The key must be a valid XML name (NCName); a key that is not raises <see cref="System.Xml.XmlException"/> at write time.</remarks>
		Direct,

		/// <summary>Each entry is an element carrying the key in an attribute, and the value as text content: <c>&lt;score key="math"&gt;12&lt;/score&gt;</c></summary>
		KeyAttribute,

		/// <summary>Each entry is a self-closed element carrying both the key and the value as attributes: <c>&lt;score key="math" value="12" /&gt;</c></summary>
		KeyValueAttributes,

		/// <summary>Each entry is an element with nested <c>Key</c> and <c>Value</c> child elements, matching the output produced by <see cref="System.Runtime.Serialization.DataContractSerializer"/> for dictionaries</summary>
		KeyValueElements,

	}

}
