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

	/// <summary>Mono-format alias that marks a partial class as a container of source-generated <b>JSON</b> converters</summary>
	/// <remarks>
	/// <para>This attribute should be applied on a partial class, that will act as a container for all generated types.</para>
	/// <para>It is exactly equivalent to <c>[CrystalConverter]</c> plus <c>[CrystalJsonOutput(...)]</c> with the same parameters, and is the shortest spelling for the common case of a container that only ever produces JSON.</para>
	/// <para>Being mono-format, it does not combine with another output format: pairing it with <c>[CrystalXmlOutput]</c> is refused (<c>CRYS0002</c>). A container that produces several outputs spells out <c>[CrystalConverter]</c> and one <c>[Crystal&lt;Format&gt;Output]</c> per output.</para>
	/// <para>All "root" data types should be included via the <see cref="CrystalSerializableAttribute"/> attribute</para>
	/// <para>Sample: <code>
	/// [CrystalJsonConverter]
	/// [CrystalSerializable(typeof(User))]
	/// [CrystalSerializable(typeof(Product))]
	/// // ... one for each "top level" type, nested/linked types are automatically discovered
	/// public static partial class ApplicationSerializers
	/// {
	///		// generated code will be inserted here
	/// }
	/// </code></para>
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class)]
	public sealed class CrystalJsonConverterAttribute : CrystalConverterAttribute
	{

		/// <summary>Use this class as a container for source-generated JSON converters</summary>
		public CrystalJsonConverterAttribute() { }

		/// <summary>Use this class as a container for source-generated JSON converters</summary>
		/// <param name="defaults">Defaults settings used for the generated converters</param>
		public CrystalJsonConverterAttribute(CrystalJsonSerializerDefaults defaults)
		{
			if (defaults == CrystalJsonSerializerDefaults.Web)
			{
				this.PropertyNameCaseInsensitive = true;
				this.PropertyNamingPolicy = CrystalJsonKnownNamingPolicy.CamelCase;
				//this.NumberHandling = JsonNumberHandling.AllowReadingFromString;
			}
			else if (defaults is not (CrystalJsonSerializerDefaults.General or CrystalJsonSerializerDefaults.DataContractCompat))
			{
				throw new ArgumentOutOfRangeException(nameof(defaults));
			}
			// DataContractCompat: no naming change (the DCJS output uses the declared member names); the profile
			// governs the VALUE formats of the generated entry points, and is applied by the source generator
		}

		/// <summary>Gets or sets the default value of <see cref="CrystalJsonSettings.IgnoreCaseForNames" />.</summary>
		public bool PropertyNameCaseInsensitive { get; set; }

		/// <summary>Gets or sets a built-in naming policy to convert JSON property names with.</summary>
		public CrystalJsonKnownNamingPolicy PropertyNamingPolicy { get; set; }

	}

	/// <summary>Requests <b>JSON</b> output from a <see cref="CrystalConverterAttribute"/> container, and carries the parameters of that output</summary>
	/// <remarks>
	/// <para>Applied on the same partial container class that carries <c>[CrystalConverter]</c> and one or more <c>[CrystalSerializable(...)]</c> attributes. The generated JSON surface is exactly the one the <c>[CrystalJsonConverter]</c> alias produces: the alias is this attribute plus the neutral marker.</para>
	/// <para>Combine it with <c>[CrystalXmlOutput]</c> on the same container to produce both outputs from one set of enrolled types.</para>
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class)]
	[PublicAPI]
	public sealed class CrystalJsonOutputAttribute : CrystalOutputAttribute
	{

		/// <summary>Enables JSON output for this serializer container</summary>
		public CrystalJsonOutputAttribute() { }

		/// <summary>Enables JSON output for this serializer container</summary>
		/// <param name="defaults">Defaults settings used for the generated converters</param>
		public CrystalJsonOutputAttribute(CrystalJsonSerializerDefaults defaults)
		{
			if (defaults == CrystalJsonSerializerDefaults.Web)
			{
				this.PropertyNameCaseInsensitive = true;
				this.PropertyNamingPolicy = CrystalJsonKnownNamingPolicy.CamelCase;
			}
			else if (defaults is not (CrystalJsonSerializerDefaults.General or CrystalJsonSerializerDefaults.DataContractCompat))
			{
				throw new ArgumentOutOfRangeException(nameof(defaults));
			}
			// DataContractCompat: no naming change (the DCJS output uses the declared member names); the profile
			// governs the VALUE formats of the generated entry points, and is applied by the source generator
		}

		/// <inheritdoc cref="CrystalJsonConverterAttribute.PropertyNameCaseInsensitive"/>
		public bool PropertyNameCaseInsensitive { get; set; }

		/// <inheritdoc cref="CrystalJsonConverterAttribute.PropertyNamingPolicy"/>
		public CrystalJsonKnownNamingPolicy PropertyNamingPolicy { get; set; }

	}

	/// <summary>The <see cref="T:System.Text.Json.JsonNamingPolicy" /> to be used at run time.</summary>
	public enum CrystalJsonKnownNamingPolicy
	{
		/// <summary>Specifies that JSON property names should not be converted.</summary>
		Unspecified,

		/// <summary>Specifies that the built-in <see cref="P:System.Text.Json.JsonNamingPolicy.CamelCase" /> be used to convert JSON property names.</summary>
		CamelCase,

		//TODO: more! (use the same values as STJ!)
	}

	/// <summary>List of default configurations for source-generated converters</summary>
	public enum CrystalJsonSerializerDefaults
	{
		/// <summary>
		///   <para>General-purpose option values. These are the same settings that are applied if a <see cref="T:System.Text.Json.JsonSerializerDefaults" /> member isn't specified.</para>
		///   <para>For information about the default property values that are applied, see JsonSerializerOptions properties.</para>
		/// </summary>
		General,

		/// <summary>
		///   <para>Option values appropriate to Web-based scenarios.</para>
		///   <para>This member implies that:</para>
		///   <para>- Property names are treated as case-insensitive.</para>
		///   <para>- "camelCase" name formatting should be employed.</para>
		///   <para>- Quoted numbers (JSON strings for number properties) are allowed.</para>
		/// </summary>
		Web,

		/// <summary>
		///   <para>For teams coming from <c>DataContractJsonSerializer</c>: the container's generated entry points default to the legacy output (<see cref="CrystalJsonSettings.DataContractCompat"/> - numeric enums, <c>\/Date(ms)\/</c> dates, ISO 8601 durations, pair-array dictionaries, explicit nulls) instead of the standard format.</para>
		///   <para>A container with this profile produces what DCJS would have produced for the same type, POCO or <c>[DataContract]</c> alike, with the documented differences. Member names stay as declared (the DCJS output has no naming policy), so combining this profile with a camelCase or case-insensitive naming option is refused at build time.</para>
		///   <para>Explicitly passed settings always replace the profile entirely; settings the baked names cannot honor fail loudly instead of silently producing another output.</para>
		///   <para>The dual-container pattern is the intended shape for a progressive portage: not-yet-ported services serialize through a container with this profile, modernized services through a default or <see cref="Web"/> container over the SAME types; when the portage completes, delete the legacy container.</para>
		/// </summary>
		DataContractCompat,
	}

	/// <summary>Meta-attribute that marks another attribute as enabling JSON source code generation on the types it decorates</summary>
	/// <remarks>
	/// <para>Apply this attribute to a custom attribute class: any (partial) type decorated with that attribute becomes <b>self-serializable</b>, meaning the source generator emits its converter and proxies nested inside the type itself (ex: <c>Widget.ReadOnly</c>, <c>Widget.Writable</c>, <c>Widget.Default</c>), without requiring a separate container class.</para>
	/// <para>This allows a library to opt its own marker attributes into JSON code generation, without the application having to reference the generator vocabulary directly.</para>
	/// <para>Sample: <code>
	/// // in the library:
	/// [CrystalJsonSelfSerializable]
	/// public sealed class DocumentAttribute : Attribute { }
	///
	/// // in the application:
	/// [Document]
	/// public sealed partial record Widget { ... } // gets Widget.ReadOnly, Widget.Writable, Widget.Default, ...
	/// </code></para>
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class)]
	public sealed class CrystalJsonSelfSerializableAttribute : Attribute
	{
	}

	/// <summary>Attribute that adds a custom JSON converter for one or more types</summary>
	/// <remarks>
	/// <para>Any derived type, nested type, or types referenced by the members of these types will also be included in the source code generation.</para>
	/// <para>This is the former, JSON-flavored spelling of the format-neutral <see cref="CrystalSerializableAttribute"/>: enrollment never was JSON-specific, and a container that produces XML enrolls its types the same way. It keeps working (and generates the same code) but new code should use <see cref="CrystalSerializableAttribute"/>.</para>
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	[Obsolete("Use [CrystalSerializable(typeof(...))] instead: type enrollment is format-neutral, and the same attribute serves every output format of the container.")]
	public sealed class CrystalJsonSerializableAttribute : CrystalSerializableAttribute
	{

		/// <summary>Generate a custom converters for instances of this type</summary>
		/// <param name="type">Type that will have a source-generated converter added to this container.</param>
		public CrystalJsonSerializableAttribute(Type type)
			: base(type)
		{ }

		/// <summary>Generate a custom converters for instances of for the following types</summary>
		/// <param name="types">List of types that will have a source-generated converter added to this container.</param>
		public CrystalJsonSerializableAttribute(params Type[] types)
			: base(types)
		{ }

	}

}
