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

namespace SnowBank.Data
{

	/// <summary>Marks a partial class as a container of source-generated serializers, for one or more output formats</summary>
	/// <remarks>
	/// <para>This marker is format-neutral: it says that the class hosts generated code, and nothing about the output(s) it produces. Each format is requested by its own output attribute (<c>[CrystalJsonOutput]</c>, <c>[CrystalXmlOutput]</c>), and a container that names none of them is rejected (<c>CRYS0001</c>): a container that produces nothing is never what the author meant.</para>
	/// <para>The types to serialize are registered with one <see cref="CrystalSerializableAttribute"/> per "root" type; nested and referenced types are discovered automatically.</para>
	/// <para>Sample: <code>
	/// [CrystalConverter]
	/// [CrystalJsonOutput(CrystalJsonSerializerDefaults.Web)]
	/// [CrystalXmlOutput]
	/// [CrystalSerializable(typeof(User))]
	/// [CrystalSerializable(typeof(Product))]
	/// public static partial class ApplicationSerializers
	/// {
	///		// generated code will be inserted here
	/// }
	/// </code></para>
	/// <para>The mono-format aliases <c>[CrystalJsonConverter]</c> and <c>[CrystalXmlConverter]</c> bundle this marker with a single output format, for the common case of a container that only ever produces one output.</para>
	/// <para>Do not subclass: the source generator matches exact attribute metadata names, a derived attribute is invisible to it.</para>
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class)]
	[PublicAPI]
	public class CrystalConverterAttribute : Attribute
	{

		/// <summary>Uses this class as a container for source-generated serializers</summary>
		public CrystalConverterAttribute() { }

	}

	/// <summary>Base class of the attributes that request one output format from a <see cref="CrystalConverterAttribute"/> container</summary>
	/// <remarks>
	/// <para>Each format contributes one derived attribute carrying its own parameters (<c>[CrystalJsonOutput]</c> for the JSON output, <c>[CrystalXmlOutput]</c> for the XML one). A container generates exactly the formats it names, and nothing else.</para>
	/// <para>This base exists purely for documentation and discoverability (it groups the output attributes under a common ancestor so a reader can find them all from one place). The source generator does not walk this hierarchy: it matches each output attribute by its exact metadata name, so deriving a new output attribute from this base does not register it as a recognized format - the generator would never see it.</para>
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class)]
	[PublicAPI]
	public abstract class CrystalOutputAttribute : Attribute
	{

		/// <summary>Requests one output format from the container</summary>
		protected CrystalOutputAttribute() { }

	}

	/// <summary>Registers one or more types in a source-generated serializer container</summary>
	/// <remarks>
	/// <para>The registration is format-neutral: the registered type gets a generated serializer for every output format the container requests.</para>
	/// <para>Any derived type, nested type, or type referenced by the members of these types is also included in the source code generation.</para>
	/// <para>A registered type that implements <c>IJsonSerializable</c>, <c>IJsonPackable</c> or <c>IJsonDeserializable&lt;T&gt;</c> keeps its own format: the generated converter calls the type's method for each of those facets, and generates the other ones from its members. Set <see cref="IgnoreCustomSerialization"/> to take the members instead.</para>
	/// <para>Do not subclass: the source generator matches exact attribute metadata names, a derived attribute is invisible to it.</para>
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	[PublicAPI]
	public class CrystalSerializableAttribute : Attribute
	{

		/// <summary>Generates a serializer for instances of this type</summary>
		/// <param name="type">Type that will have a source-generated serializer added to this container.</param>
		public CrystalSerializableAttribute(Type type)
		{
			this.Types = [ type ];
		}

		/// <summary>Generates serializers for instances of the following types</summary>
		/// <param name="types">List of types that will have a source-generated serializer added to this container.</param>
		public CrystalSerializableAttribute(params Type[] types)
		{
			this.Types = types;
		}

		/// <summary>List of types to include in this container</summary>
		public Type[] Types { get; set; }

		/// <summary>Generates a member-based converter even when the type implements <c>IJsonSerializable</c>, <c>IJsonPackable</c> or <c>IJsonDeserializable&lt;T&gt;</c></summary>
		/// <remarks>
		/// <para>By default the generated converter calls the type's own method for each facet the type implements, so that a container and the runtime path produce the same bytes. This restores the member-based converter, for code that took a dependence on the member-based output.</para>
		/// <para>Two limits, both by construction:</para>
		/// <para>It cannot rescue a type the generator is unable to construct. A type with <c>required</c> members and no parameterless constructor does not compile when its converter is built from its members, whichever way this option is set.</para>
		/// <para>It has no effect on a transitively-discovered type. Only the types named in a <see cref="CrystalSerializableAttribute"/> carry this option; the nested and referenced types the generator discovers carry no attribute of their own, and always keep their own format.</para>
		/// </remarks>
		public bool IgnoreCustomSerialization { get; set; }

	}

}
