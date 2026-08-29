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

namespace SnowBank.Data.Json.Tests
{
	using SnowBank.Data;
	using SnowBank.Data.Xml;

	/// <summary>DTO shared by every container of <see cref="AttributeVocabularyFacts"/>, so that the outputs they produce are directly comparable</summary>
	public sealed record VocabularyDto
	{

		public int Id { get; set; }

		public string? Label { get; set; }

		public bool Enabled { get; set; }

	}

	// The one deliberate use of the obsolete registration spelling in this repository: the alias must keep working for
	// applications that have not migrated yet, and "keeps working" is only worth anything if something exercises it.
#pragma warning disable CS0618
	[CrystalJsonConverter]
	[CrystalJsonSerializable(typeof(VocabularyDto))]
	public static partial class LegacySpellingSerializers { }
#pragma warning restore CS0618

	/// <summary>The same container, in the spelling the alias stands for</summary>
	[CrystalConverter]
	[CrystalJsonOutput]
	[CrystalSerializable(typeof(VocabularyDto))]
	public static partial class ModernSpellingSerializers { }

	/// <summary>An XML-only container over the same DTO: no JSON entry point is generated for it at all</summary>
	[CrystalXmlConverter]
	[CrystalSerializable(typeof(VocabularyDto))]
	public static partial class XmlOnlySerializers { }

	/// <summary>A dual-output container over the same DTO, whose XML output must match the XML-only one</summary>
	[CrystalConverter]
	[CrystalJsonOutput]
	[CrystalXmlOutput]
	[CrystalSerializable(typeof(VocabularyDto))]
	public static partial class DualSerializers { }

	/// <summary>Pins the container attribute vocabulary at RUNTIME: the obsolete and modern spellings produce the same bytes, and an XML-only container produces the same XML as a dual one</summary>
	/// <remarks>The generator's own truth table (which surface each combination emits) is pinned by <c>ContainerOutputMatrixFacts</c> in the CodeGen test suite; this fixture pins what the emitted code actually writes.</remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	public sealed class AttributeVocabularyFacts : SimpleTest
	{

		private static VocabularyDto MakeSample() => new() { Id = 42, Label = "hello", Enabled = true };

		[Test]
		public void Test_Obsolete_Attribute_Spelling_Still_Generates_A_Working_Container()
		{
			var dto = MakeSample();

			var json = LegacySpellingSerializers.VocabularyDto.ToJsonText(dto);
			Log(json);

			Assert.That(json, Is.Not.Null.And.Not.Empty, "the obsolete alias must still produce a container that serializes");

			var back = LegacySpellingSerializers.VocabularyDto.Deserialize(json);
			Assert.That(back, Is.EqualTo(dto), "and one that round-trips");
		}

		[Test]
		public void Test_Obsolete_And_Modern_Spellings_Produce_Identical_Bytes()
		{
			// "alias" is a promise about the output, not only about the vocabulary: the same DTO through either container
			// must produce the same bytes, or an application would change its output simply by migrating its attributes
			var dto = MakeSample();

			var legacy = LegacySpellingSerializers.VocabularyDto.ToJsonBytes(dto);
			var modern = ModernSpellingSerializers.VocabularyDto.ToJsonBytes(dto);

			Log($"legacy: {legacy.Length} bytes");
			Log($"modern: {modern.Length} bytes");

			Assert.That(modern, Is.EqualTo(legacy), "migrating [CrystalJsonSerializable] to [CrystalSerializable] must not change one byte of the output");
		}

		[Test]
		public void Test_Xml_Only_Container_Writes_The_Same_Xml_As_A_Dual_One()
		{
			// dropping the JSON surface must change nothing about the XML the container writes
			var dto = MakeSample();

			var xmlOnly = XmlOnlySerializers.VocabularyDto.ToXmlText(dto);
			var dual = DualSerializers.VocabularyDto.ToXmlText(dto);

			Log(xmlOnly);

			Assert.That(xmlOnly, Is.EqualTo(dual), "the XML output does not depend on whether the container also produces JSON");
		}

	}

}
