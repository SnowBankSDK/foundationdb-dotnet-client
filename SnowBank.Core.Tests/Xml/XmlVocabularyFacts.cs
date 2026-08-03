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

namespace SnowBank.Data.Xml.Tests
{
	using NUnit.Framework;
	using SnowBank.Data.Xml;

	/// <summary>Pins the declarative XML vocabulary: <see cref="XmlPropertyAttribute"/>, <see cref="CrystalXmlOutputAttribute"/>, and the dictionary format defaults</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-XML")]
	[Parallelizable(ParallelScope.All)]
	public sealed class XmlVocabularyFacts : SimpleTest
	{

		[Test]
		public void Test_XmlProperty_Defaults_Are_Inherit()
		{
			var attr = new XmlPropertyAttribute();
			Assert.That(attr.Name, Is.Null);
			Assert.That(attr.Attribute, Is.False);
			Assert.That(attr.ItemName, Is.Null);
			Assert.That(attr.DictionaryFormat, Is.EqualTo(XmlDictionaryFormat.Default));
		}

		[Test]
		public void Test_CrystalXmlOutput_Defaults_Derive_From_Container()
		{
			var attr = new CrystalXmlOutputAttribute();
			Assert.That(attr.Profile, Is.EqualTo(XmlOutputProfile.Default));
			Assert.That(attr.DictionaryFormat, Is.EqualTo(XmlDictionaryFormat.Default));
		}

		[Test]
		public void Test_XmlProperty_Constructor_Sets_Name()
		{
			var attr = new XmlPropertyAttribute("@id");
			Assert.That(attr.Name, Is.EqualTo("@id"), "the runtime attribute stores what was written; the '@' sugar is normalized by the generator, not here");
		}

		[Test]
		public void Test_Exceptions_Derive_From_InvalidOperationException_And_Carry_Context()
		{
			var invalidName = new CrystalXmlInvalidNameException("1bad");
			Assert.That(invalidName, Is.InstanceOf<InvalidOperationException>());
			Assert.That(invalidName.Name, Is.EqualTo("1bad"));

			var unknownType = new CrystalXmlUnknownTypeException(typeof(XmlVocabularyFacts));
			Assert.That(unknownType, Is.InstanceOf<InvalidOperationException>());
			Assert.That(unknownType.Type, Is.EqualTo(typeof(XmlVocabularyFacts)));

			var cycle = new CrystalXmlCycleException(typeof(XmlVocabularyFacts));
			Assert.That(cycle, Is.InstanceOf<InvalidOperationException>());
			Assert.That(cycle.Type, Is.EqualTo(typeof(XmlVocabularyFacts)));

			var notSupported = new CrystalXmlNotSupportedException(typeof(XmlVocabularyFacts));
			Assert.That(notSupported, Is.InstanceOf<InvalidOperationException>());
			Assert.That(notSupported.Type, Is.EqualTo(typeof(XmlVocabularyFacts)));
		}

	}

}
