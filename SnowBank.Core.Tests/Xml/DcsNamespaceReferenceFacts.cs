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

// This file is compiled for the net472 validation target too: see the remark on ReferenceDcsWire.cs.

namespace SnowBank.Data.Xml.Tests
{
	using SnowBank.Data.Xml.Tests.Acme;

	/// <summary>
	/// Pins the namespace rules of the reference format: every fact here asserts the raw, unstripped
	/// <see cref="System.Runtime.Serialization.DataContractSerializer"/> wire only
	/// (<see cref="ReferenceDcsWire.Serialize"/> with <c>strip: false</c>). CrystalXml's generated default profile has
	/// no namespace vocabulary, so none of the probe types are enrolled with <c>[CrystalSerializable]</c> and no test
	/// here calls a generated <c>ToXmlText</c>. <see cref="DcsWireFidelityFacts"/> covers the axes a stripped wire
	/// still shows (member order and renames, the nil truth table, collection and dictionary item names, polymorphism
	/// discriminators without a namespace, scalar lexical forms, enums); this fixture adds only what stripping erases.
	/// </summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-XML")]
	public sealed class DcsNamespaceReferenceFacts : SimpleTest
	{

		[Test]
		public void Test_Root_Always_Declares_The_Xsi_Instance_Namespace_First()
		{
			// xmlns:i is on the root even though this document has no nil and no type, and it is the first attribute
			string wire = ReferenceDcsWire.Serialize(new NamespaceDefaultProbe { Name = "x" }, typeof(NamespaceDefaultProbe), strip: false);
			Log(wire);

			Assert.That(wire, Does.StartWith("""<NamespaceDefaultProbe xmlns:i="http://www.w3.org/2001/XMLSchema-instance" """));
		}

		[Test]
		public void Test_Default_Namespace_Is_Derived_From_The_Clr_Namespace_And_Declared_Last()
		{
			// the root's own contract namespace is the default namespace, declared last among the root's attributes
			// (here, the only other one is xmlns:i)
			string expectedNamespace = $"http://schemas.datacontract.org/2004/07/{typeof(NamespaceDefaultProbe).Namespace}";
			string expected = $"""<NamespaceDefaultProbe xmlns:i="http://www.w3.org/2001/XMLSchema-instance" xmlns="{expectedNamespace}"><Name>x</Name></NamespaceDefaultProbe>""";

			string wire = ReferenceDcsWire.Serialize(new NamespaceDefaultProbe { Name = "x" }, typeof(NamespaceDefaultProbe), strip: false);
			Log(wire);

			Assert.That(wire, Is.EqualTo(expected));
		}

		[Test]
		public void Test_Explicit_Contract_Namespace_Is_The_Default_Namespace()
		{
			string wire = ReferenceDcsWire.Serialize(new NamespaceExplicitProbe { Name = "x" }, typeof(NamespaceExplicitProbe), strip: false);
			Log(wire);

			Assert.That(wire, Is.EqualTo("""<NamespaceExplicit xmlns:i="http://www.w3.org/2001/XMLSchema-instance" xmlns="urn:acme:catalog:1"><Name>x</Name></NamespaceExplicit>"""));
		}

		[Test]
		public void Test_Poco_Mode_Still_Gets_The_Clr_Namespace_Derived_Default()
		{
			// A type with no [DataContract] at all still gets a default namespace derived from its CLR namespace: POCO
			// mode changes member selection (public get+set only, alphabetical), not the namespace rule.
			string expectedNamespace = $"http://schemas.datacontract.org/2004/07/{typeof(NamespacePocoProbe).Namespace}";
			string expected = $"""<NamespacePocoProbe xmlns:i="http://www.w3.org/2001/XMLSchema-instance" xmlns="{expectedNamespace}"><Name>x</Name></NamespacePocoProbe>""";

			string wire = ReferenceDcsWire.Serialize(new NamespacePocoProbe { Name = "x" }, typeof(NamespacePocoProbe), strip: false);
			Log(wire);

			Assert.That(wire, Is.EqualTo(expected));
		}

		[Test]
		public void Test_Cross_Namespace_Child_And_Generic_Collections_Declare_A_Depth_Numbered_Prefix()
		{
			// a member element lives in the declaring type's namespace (no prefix); the nested type's own members live
			// in the nested contract's namespace, prefixed. The generated prefix is d{depth}p{n}, and every member
			// here sits at depth 2 (direct child of the root), so each one that needs a foreign namespace declares
			// d2p1. The declaration follows the declared type, not the value: NullChild, NullList and EmptyList
			// declare the same prefix a set value would, even though they never write an element using it, and the
			// declaration comes before the i:nil attribute on the same element.
			var value = new NamespaceNestedProbe
			{
				Child = new NamespaceChildProbe { Name = "child" },
				NullChild = null,
				PlainList = ["a", "b"],
				NullList = null,
				EmptyList = [],
				PlainDict = new Dictionary<string, string> { ["k1"] = "v1" },
			};

			string wire = ReferenceDcsWire.Serialize(value, typeof(NamespaceNestedProbe), strip: false);
			Log(wire);

			Assert.That(
				wire,
				Is.EqualTo(
					"""<NamespaceNestedProbe xmlns:i="http://www.w3.org/2001/XMLSchema-instance" xmlns="urn:acme:catalog:1">"""
					+ """<Child xmlns:d2p1="urn:acme:catalog:2"><d2p1:Name>child</d2p1:Name></Child>"""
					+ """<NullChild xmlns:d2p1="urn:acme:catalog:2" i:nil="true" />"""
					+ """<PlainList xmlns:d2p1="http://schemas.microsoft.com/2003/10/Serialization/Arrays"><d2p1:string>a</d2p1:string><d2p1:string>b</d2p1:string></PlainList>"""
					+ """<NullList xmlns:d2p1="http://schemas.microsoft.com/2003/10/Serialization/Arrays" i:nil="true" />"""
					+ """<EmptyList xmlns:d2p1="http://schemas.microsoft.com/2003/10/Serialization/Arrays" />"""
					+ """<PlainDict xmlns:d2p1="http://schemas.microsoft.com/2003/10/Serialization/Arrays"><d2p1:KeyValueOfstringstring><d2p1:Key>k1</d2p1:Key><d2p1:Value>v1</d2p1:Value></d2p1:KeyValueOfstringstring></PlainDict>"""
					+ """</NamespaceNestedProbe>"""));
		}

		[Test]
		public void Test_ItypeQName_Bare_In_The_Same_Namespace_Prefixed_Across_Namespaces()
		{
			// i:type is a QName. A derived type in the slot's own namespace writes a bare local name (no prefix, no
			// declaration); a derived type in another namespace writes prefix:Local and declares the prefix on the
			// same element; a boxed primitive in an object slot writes a QName in the built-in XML Schema namespace
			var value = new NamespacePolyProbe
			{
				SameNamespaceSubtype = new NamespaceDerivedSameNs { Field = "same" },
				OtherNamespaceSubtype = new NamespaceDerivedOtherNs { Field = "other" },
				BoxedPrimitive = "boxed",
			};

			string wire = ReferenceDcsWire.Serialize(value, typeof(NamespacePolyProbe), strip: false);
			Log(wire);

			Assert.That(
				wire,
				Is.EqualTo(
					"""<NamespacePolyProbe xmlns:i="http://www.w3.org/2001/XMLSchema-instance" xmlns="urn:acme:catalog:1">"""
					+ """<SameNamespaceSubtype i:type="NamespaceDerivedSameNs"><Field>same</Field></SameNamespaceSubtype>"""
					+ """<OtherNamespaceSubtype xmlns:d2p1="urn:acme:catalog:2" i:type="d2p1:NamespaceDerivedOtherNs"><Field>other</Field></OtherNamespaceSubtype>"""
					+ """<BoxedPrimitive xmlns:d2p1="http://www.w3.org/2001/XMLSchema" i:type="d2p1:string">boxed</BoxedPrimitive>"""
					+ """</NamespacePolyProbe>"""));
		}

		[Test]
		public void Test_Builtin_Namespace_Xml_Schema_On_A_Boxed_Primitive_Itype()
		{
			// http://www.w3.org/2001/XMLSchema is not derived from any CLR namespace: it is the QName namespace of a
			// primitive i:type in an anyType slot. The test above exercises it too; this one isolates it, so each
			// built-in namespace has a test of its own.
			var value = new NamespacePolyProbe { BoxedPrimitive = 123 };

			string wire = ReferenceDcsWire.Serialize(value, typeof(NamespacePolyProbe), strip: false);
			Log(wire);

			Assert.That(wire, Does.Contain("""<BoxedPrimitive xmlns:d2p1="http://www.w3.org/2001/XMLSchema" i:type="d2p1:int">123</BoxedPrimitive>"""));
		}

		[Test]
		public void Test_Builtin_Namespace_Arrays_On_An_Unannotated_Generic_Collection()
		{
			// http://schemas.microsoft.com/2003/10/Serialization/Arrays, on an unannotated generic List<T> or
			// Dictionary<K,V>. Isolated here for the same reason as the XML Schema fact above.
			var value = new NamespaceNestedProbe { PlainList = ["a"] };

			string wire = ReferenceDcsWire.Serialize(value, typeof(NamespaceNestedProbe), strip: false);
			Log(wire);

			Assert.That(wire, Does.Contain("""<PlainList xmlns:d2p1="http://schemas.microsoft.com/2003/10/Serialization/Arrays"><d2p1:string>a</d2p1:string></PlainList>"""));
		}

		[Test]
		public void Test_Builtin_Namespace_System_On_The_DateTimeOffset_Two_Member_Contract()
		{
			// http://schemas.datacontract.org/2004/07/System, on DateTimeOffset's own two-member contract (DateTime,
			// OffsetMinutes). Declared independently of NamespaceOffsetProbe's own namespace (urn:acme:catalog:1): a
			// built-in namespace is never derived from the declaring type.
			var value = new NamespaceOffsetProbe { Offset = new DateTimeOffset(2026, 8, 20, 14, 5, 6, 789, TimeSpan.FromHours(2)) };

			string wire = ReferenceDcsWire.Serialize(value, typeof(NamespaceOffsetProbe), strip: false);
			Log(wire);

			Assert.That(
				wire,
				Is.EqualTo(
					"""<NamespaceOffsetProbe xmlns:i="http://www.w3.org/2001/XMLSchema-instance" xmlns="urn:acme:catalog:1">"""
					+ """<Offset xmlns:d2p1="http://schemas.datacontract.org/2004/07/System"><d2p1:DateTime>2026-08-20T12:05:06.789Z</d2p1:DateTime><d2p1:OffsetMinutes>120</d2p1:OffsetMinutes></Offset>"""
					+ """</NamespaceOffsetProbe>"""));
		}

		[Test]
		public void Test_Builtin_Namespace_Serialization_Z_On_IsReference()
		{
			// http://schemas.microsoft.com/2003/10/Serialization/ (prefix z), on [DataContract(IsReference = true)]
			// with a shared instance. This pins the wire vocabulary only: CrystalXml does not support the
			// object-graph reference mechanism (z:Id/z:Ref tracking), so there is no generated side to compare.
			var shared = new NamespaceSharedProbe { Label = "shared" };
			var value = new NamespaceRefPairProbe { A = shared, B = shared };

			string wire = ReferenceDcsWire.Serialize(value, typeof(NamespaceRefPairProbe), strip: false);
			Log(wire);

			Assert.That(
				wire,
				Is.EqualTo(
					"""<NamespaceRefPairProbe xmlns:i="http://www.w3.org/2001/XMLSchema-instance" xmlns="urn:acme:catalog:1">"""
					+ """<A z:Id="i1" xmlns:z="http://schemas.microsoft.com/2003/10/Serialization/"><Label>shared</Label></A>"""
					+ """<B z:Ref="i1" xmlns:z="http://schemas.microsoft.com/2003/10/Serialization/" />"""
					+ """</NamespaceRefPairProbe>"""));
		}

	}

}
