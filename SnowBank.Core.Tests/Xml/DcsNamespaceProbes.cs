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

// This file is compiled for the net472 validation target too: see the remark on ReferenceDcsOutput.cs. The oracle is
// a live DCS on both CLRs, so the facts in DcsNamespaceReferenceFacts.cs also show that the two agree on the
// standard (unstripped) output, as the stripped facts already do.

// note: these types carry an explicit or default contract namespace on purpose, unlike most of DcsProbes.cs, whose
// StrippingXmlWriter-compared facts erase namespaces entirely and so stay indifferent to them. Most of them are
// registered in NamespaceProbeSerializers at the end of this file, so DcsNamespaceReferenceFacts.cs compares the
// unstripped reference output (ReferenceDcsOutput.Serialize(..., strip: false)) to the generated default output. The two
// IsReference probes stay out of the container: see the remark on NamespaceSharedProbe.

/// <summary>Declared in the GLOBAL namespace: there is no CLR namespace to derive a contract namespace from</summary>
[System.Runtime.Serialization.DataContract]
public sealed class AcmeGlobalNamespaceProbe
{
	[System.Runtime.Serialization.DataMember] public string? Name;
}

namespace SnowBank.Data.Xml.Tests.Acme
{
	using System.Runtime.Serialization;
	using System.Text.Json.Serialization;
	using SnowBank.Data.Json;
	using SnowBank.Data.Xml;

	#region Root namespace declaration...

	/// <summary>No explicit <see cref="DataContractAttribute.Namespace"/>: the default namespace is derived from the CLR namespace</summary>
	[DataContract]
	public sealed class NamespaceDefaultProbe
	{
		[DataMember] public string? Name;
	}

	/// <summary>Explicit <see cref="DataContractAttribute.Namespace"/></summary>
	[DataContract(Name = "NamespaceExplicit", Namespace = "urn:acme:catalog:1")]
	public sealed class NamespaceExplicitProbe
	{
		[DataMember] public string? Name;
	}

	/// <summary>POCO mode, no <see cref="DataContractAttribute"/> at all: still gets the CLR-namespace-derived default</summary>
	public sealed class NamespacePocoProbe
	{
		public string? Name { get; set; }
	}

	#endregion

	#region Cross-namespace nesting, generic collections, dictionaries...

	/// <summary>Lives in its own contract namespace, distinct from <see cref="NamespaceNestedProbe"/>'s</summary>
	[DataContract(Name = "NamespaceChild", Namespace = "urn:acme:catalog:2")]
	public sealed class NamespaceChildProbe
	{
		[DataMember] public string? Name;
	}

	/// <summary>
	/// A parent in one namespace holding a child in another, an unannotated generic list, and a plain dictionary. The
	/// child and the collection members carry a namespace declaration whether or not the value is null, and the
	/// declaration precedes any <c>i:nil</c> attribute on the same element.
	/// </summary>
	[DataContract(Namespace = "urn:acme:catalog:1")]
	public sealed class NamespaceNestedProbe
	{
		[DataMember(Order = 1)] public NamespaceChildProbe? Child;
		[DataMember(Order = 2)] public NamespaceChildProbe? NullChild;
		[DataMember(Order = 3)] public List<string>? PlainList;
		[DataMember(Order = 4)] public List<string>? NullList;
		[DataMember(Order = 5)] public List<string>? EmptyList;
		[DataMember(Order = 6)] public Dictionary<string, string>? PlainDict;
	}

	#endregion

	#region i:type QNames across namespaces...

	// note: the two attribute families name the same two derived types, one per consumer. The live DCS oracle reads
	// [KnownType] and knows nothing about [JsonDerivedType]; the generator reads [JsonDerivedType] and knows nothing
	// about [KnownType]. The JSON tags below never reach the XML output: the DataContract profile writes the derived
	// type's contract name as i:type, so the tags only have to be unique.
	[DataContract(Name = "NamespaceBase", Namespace = "urn:acme:catalog:1")]
	[KnownType(typeof(NamespaceDerivedSameNs))]
	[KnownType(typeof(NamespaceDerivedOtherNs))]
	[JsonPolymorphic]
	[JsonDerivedType(typeof(NamespaceDerivedSameNs), "same")]
	[JsonDerivedType(typeof(NamespaceDerivedOtherNs), "other")]
	public class NamespaceBaseProbe
	{
		[DataMember] public string? Field;
	}

	/// <summary>Same contract namespace as <see cref="NamespaceBaseProbe"/>: its <c>i:type</c> value is a bare local name</summary>
	[DataContract(Name = "NamespaceDerivedSameNs", Namespace = "urn:acme:catalog:1")]
	public sealed class NamespaceDerivedSameNs : NamespaceBaseProbe;

	/// <summary>Another contract namespace: its <c>i:type</c> value is a prefixed QName, declared on the same element</summary>
	[DataContract(Name = "NamespaceDerivedOtherNs", Namespace = "urn:acme:catalog:2")]
	public sealed class NamespaceDerivedOtherNs : NamespaceBaseProbe;

	[DataContract(Namespace = "urn:acme:catalog:1")]
	[KnownType(typeof(NamespaceDerivedSameNs))]
	[KnownType(typeof(NamespaceDerivedOtherNs))]
	public sealed class NamespacePolyProbe
	{
		[DataMember(Order = 1)] public NamespaceBaseProbe? SameNamespaceSubtype;
		[DataMember(Order = 2)] public NamespaceBaseProbe? OtherNamespaceSubtype;
		/// <summary>A boxed primitive in an <c>object</c> slot: its <c>i:type</c> value is a QName in the built-in XML Schema namespace</summary>
		[DataMember(Order = 3)] public object? BoxedPrimitive;
	}

	#endregion

	#region The built-in System namespace, on the DateTimeOffset two-member contract...

	/// <summary>
	/// <see cref="DateTimeOffset"/>'s own two-member contract (<c>DateTime</c>, <c>OffsetMinutes</c>) always declares
	/// <c>http://schemas.datacontract.org/2004/07/System</c>, independently of the declaring type's own namespace.
	/// </summary>
	[DataContract(Namespace = "urn:acme:catalog:1")]
	public sealed class NamespaceOffsetProbe
	{
		[DataMember] public DateTimeOffset Offset;
	}

	#endregion

	#region The built-in Serialization (z:) namespace, on IsReference...

	/// <summary>
	/// Reference-vocabulary probe only: CrystalXml does not support <c>[DataContract(IsReference = true)]</c> and its
	/// <c>z:Id</c>/<c>z:Ref</c> output. This type pins the built-in Serialization namespace, not the object-graph
	/// reference mechanism.
	/// </summary>
	[DataContract(Name = "NamespaceShared", Namespace = "urn:acme:catalog:1", IsReference = true)]
	public sealed class NamespaceSharedProbe
	{
		[DataMember] public string? Label;
	}

	[DataContract(Namespace = "urn:acme:catalog:1")]
	public sealed class NamespaceRefPairProbe
	{
		[DataMember(Order = 1)] public NamespaceSharedProbe? A;
		[DataMember(Order = 2)] public NamespaceSharedProbe? B;
	}

	#endregion

	#region Two declarations on one element...

	/// <summary>The declared contract of a slot whose runtime type lives in a THIRD namespace</summary>
	[DataContract(Name = "NamespaceTwoDeclsBase", Namespace = "urn:acme:catalog:2")]
	[KnownType(typeof(NamespaceTwoDeclsDerived))]
	[JsonPolymorphic]
	[JsonDerivedType(typeof(NamespaceTwoDeclsDerived), "third")]
	public class NamespaceTwoDeclsBaseProbe
	{
		[DataMember] public string? Field;
	}

	/// <summary>Lives in a third namespace, so the slot element needs one declaration for the declared contract and another for the <c>i:type</c> QName</summary>
	[DataContract(Name = "NamespaceTwoDeclsDerived", Namespace = "urn:acme:catalog:3")]
	public sealed class NamespaceTwoDeclsDerived : NamespaceTwoDeclsBaseProbe;

	[DataContract(Namespace = "urn:acme:catalog:1")]
	public sealed class NamespaceTwoDeclsProbe
	{
		[DataMember] public NamespaceTwoDeclsBaseProbe? Slot;
	}

	#endregion

	#region A namespace already in scope on an ancestor...

	/// <summary>Same contract namespace as <see cref="NamespaceInScopeChildProbe"/>: its elements sit where that namespace is already in scope</summary>
	[DataContract(Namespace = "urn:acme:catalog:2")]
	public sealed class NamespaceInScopeLeafProbe
	{
		[DataMember] public string? Tag;
	}

	[DataContract(Namespace = "urn:acme:catalog:2")]
	public sealed class NamespaceInScopeChildProbe
	{
		[DataMember] public NamespaceInScopeLeafProbe? Leaf;
	}

	[DataContract(Namespace = "urn:acme:catalog:1")]
	public sealed class NamespaceInScopeProbe
	{
		[DataMember] public NamespaceInScopeChildProbe? Child;
	}

	#endregion

	#region The empty namespace, stated explicitly, under a namespaced parent...

	/// <summary>The explicitly stated ABSENCE of a namespace, distinct from an unspecified one</summary>
	[DataContract(Namespace = "")]
	public sealed class NamespaceEmptyChildProbe
	{
		[DataMember] public string? Name;
	}

	[DataContract(Namespace = "urn:acme:catalog:1")]
	public sealed class NamespaceEmptyParentProbe
	{
		[DataMember] public NamespaceEmptyChildProbe? Child;
	}

	#endregion

	#region Mixed-namespace service envelope...

	/// <summary>
	/// Structural twin of a measured service-envelope shape: a generic contract whose declared name expands the
	/// payload's contract name (<c>Response{0}</c>), in an explicit contract namespace, wrapping a payload type
	/// that has no contract at all. One document carries three namespace classes: the envelope's explicit one,
	/// the payload's CLR-derived one, and the error type's CLR-derived one.
	/// </summary>
	[DataContract(Name = "Response{0}", Namespace = "urn:acme:services:data")]
	public class NamespaceEnvelopeBaseProbe
	{
		[DataMember(Name = "success")] public bool Success;
		[DataMember(Name = "message")] public string? Message;
		[DataMember(Name = "errors")] public List<NamespaceEnvelopeErrorProbe>? Errors;
	}

	/// <summary>The payload slot: a base-declared member set carried by every closed payload type</summary>
	[DataContract(Name = "Response{0}", Namespace = "urn:acme:services:data")]
	public sealed class NamespaceEnvelopeProbe<T> : NamespaceEnvelopeBaseProbe
	{
		[DataMember(Name = "d")] public T? Result;
	}

	/// <summary>No contract at all: the payload lives in the CLR-derived namespace, unlike its envelope</summary>
	public sealed class NamespaceEnvelopePayloadProbe
	{
		public string? Title { get; set; }
	}

	/// <summary>A contract with no explicit namespace: the third namespace class of the envelope document</summary>
	[DataContract]
	public sealed class NamespaceEnvelopeErrorProbe
	{
		[DataMember(Name = "id")] public string? Source;
		[DataMember(Name = "message")] public string? Message;
	}

	#endregion

	#region Test container...

	// The generated side of DcsNamespaceReferenceFacts. Only the DEFAULT output is registered here: this fixture is about
	// the namespaces themselves, and the namespace-free output has none, so a namespace-free twin would compare nothing.
	// NamespaceSharedProbe and NamespaceRefPairProbe are left out on purpose: their output is z:Id/z:Ref, the object-graph
	// reference mechanism CrystalXml does not support.
	[CrystalConverter]
	[CrystalJsonOutput(CrystalJsonSerializerDefaults.DataContractCompat)]
	[CrystalXmlOutput]
	[CrystalSerializable(typeof(NamespaceDefaultProbe))]
	[CrystalSerializable(typeof(NamespaceExplicitProbe))]
	[CrystalSerializable(typeof(NamespacePocoProbe))]
	[CrystalSerializable(typeof(NamespaceChildProbe))]
	[CrystalSerializable(typeof(NamespaceNestedProbe))]
	[CrystalSerializable(typeof(NamespaceBaseProbe))]
	[CrystalSerializable(typeof(NamespaceDerivedSameNs))]
	[CrystalSerializable(typeof(NamespaceDerivedOtherNs))]
	[CrystalSerializable(typeof(NamespacePolyProbe))]
	[CrystalSerializable(typeof(NamespaceOffsetProbe))]
	// the payload type argument is fully qualified: inside this class's attribute list, the bare name binds to
	// the generated nested holder of the same name, and a static class cannot be a type argument (CS0718)
	[CrystalSerializable(typeof(NamespaceEnvelopeProbe<global::SnowBank.Data.Xml.Tests.Acme.NamespaceEnvelopePayloadProbe>))]
	[CrystalSerializable(typeof(NamespaceTwoDeclsProbe))]
	[CrystalSerializable(typeof(NamespaceTwoDeclsBaseProbe))]
	[CrystalSerializable(typeof(NamespaceTwoDeclsDerived))]
	[CrystalSerializable(typeof(NamespaceInScopeProbe))]
	[CrystalSerializable(typeof(NamespaceInScopeChildProbe))]
	[CrystalSerializable(typeof(NamespaceInScopeLeafProbe))]
	[CrystalSerializable(typeof(NamespaceEmptyParentProbe))]
	[CrystalSerializable(typeof(NamespaceEmptyChildProbe))]
	[CrystalSerializable(typeof(global::AcmeGlobalNamespaceProbe))]
	public static partial class NamespaceProbeSerializers
	{
	}

	#endregion

}
