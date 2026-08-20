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

// This file is compiled for the net472 validation target too: see the remark on ReferenceDcsWire.cs. The oracle is
// a live DCS on both CLRs, so the facts in DcsNamespaceReferenceFacts.cs also show that the two agree on the
// standard (unstripped) wire, as the stripped facts already do.

// note: these types carry an explicit or default contract namespace on purpose, unlike most of DcsProbes.cs, whose
// StrippingXmlWriter-compared facts erase namespaces entirely and so stay indifferent to them. None of these types
// are enrolled with [CrystalSerializable]: DcsNamespaceReferenceFacts.cs only exercises the unstripped reference
// pipeline (ReferenceDcsWire.Serialize(..., strip: false)), because the generated side has no namespace vocabulary.
namespace SnowBank.Data.Xml.Tests.Acme
{
	using System.Runtime.Serialization;

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

	[DataContract(Name = "NamespaceBase", Namespace = "urn:acme:catalog:1")]
	[KnownType(typeof(NamespaceDerivedSameNs))]
	[KnownType(typeof(NamespaceDerivedOtherNs))]
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
	/// <c>z:Id</c>/<c>z:Ref</c> wire. This type pins the built-in Serialization namespace, not the object-graph
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

}
