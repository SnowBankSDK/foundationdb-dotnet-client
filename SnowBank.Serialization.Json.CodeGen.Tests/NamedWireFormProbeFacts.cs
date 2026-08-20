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

namespace SnowBank.Serialization.Json.CodeGen.Tests
{

	#region Probe types...

	public sealed record ProbeNamedWireFormDto
	{

		// the POSITIONAL form: [JsonProperty("...")]
		[JsonProperty("CHAMP")]
		public string? Field { get; set; }

		// the NAMED form: the very same rename, spelled through the settable property
		[JsonProperty(PropertyName = "SOUSCHAMP")]
		public string? SubField { get; set; }

		// the named form next to the other named arguments of the same attribute
		[JsonProperty(PropertyName = "JOUR", EnumFormat = JsonEnumFormat.String)]
		public DayOfWeek Day { get; set; }

		public string? Plain { get; set; }

	}

	#endregion

	[CrystalJsonConverter]
	[CrystalSerializable(typeof(ProbeNamedWireFormDto))]
	public static partial class ProbeNamedWireFormHost
	{
	}

	/// <summary>Pins that <c>[JsonProperty(PropertyName = "...")]</c> renames a member in generated converters, exactly like the positional <c>[JsonProperty("...")]</c> spelling</summary>
	/// <remarks>The attribute exposes <see cref="JsonPropertyAttribute.PropertyName"/> as a settable property, and the reflection path reads that property whichever way it was filled; the generated path used to read the constructor argument ONLY, so the named spelling compiled and was silently dropped, and the member went out under its raw C# name.</remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	public sealed class NamedWireFormProbeFacts : SimpleTest
	{

		[Test]
		public void Test_Generated_Serialize_Honors_The_Named_Form()
		{
			var dto = new ProbeNamedWireFormDto { Field = "a", SubField = "b", Day = DayOfWeek.Friday, Plain = "c" };

			var obj = JsonObject.Parse(ProbeNamedWireFormHost.ProbeNamedWireFormDto.ToJsonText(dto));
			Log(obj.ToJsonText());

			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj["CHAMP"], IsJson.EqualTo("a"), "the positional spelling renames the member");
				Assert.That(obj["SOUSCHAMP"], IsJson.EqualTo("b"), "the named spelling must rename the member too");
				Assert.That(obj.ContainsKey("SubField"), Is.False, "the raw C# name must not reach the wire");
				Assert.That(obj["JOUR"], IsJson.EqualTo("Friday"), "the name is honored next to the other named arguments of the same attribute");
				Assert.That(obj.ContainsKey("Day"), Is.False);
				Assert.That(obj["Plain"], IsJson.EqualTo("c"), "a member without the attribute keeps its declared name");
			}
		}

		[Test]
		public void Test_Generated_Pack_Honors_The_Named_Form()
		{
			var dto = new ProbeNamedWireFormDto { Field = "a", SubField = "b" };

			var obj = ProbeNamedWireFormHost.ProbeNamedWireFormDto.Pack(dto).AsObject();

			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj["SOUSCHAMP"], IsJson.EqualTo("b"), "the DOM route takes the same wire name as the text route");
				Assert.That(obj.ContainsKey("SubField"), Is.False);
			}
		}

		[Test]
		public void Test_Generated_Reads_Bind_The_Named_Form()
		{
			var dto = ProbeNamedWireFormHost.ProbeNamedWireFormDto.Deserialize("""{ "CHAMP": "a", "SOUSCHAMP": "b", "JOUR": "Friday", "Plain": "c" }""");

			using (Assert.EnterMultipleScope())
			{
				Assert.That(dto.Field, Is.EqualTo("a"));
				Assert.That(dto.SubField, Is.EqualTo("b"), "the read side must look for the renamed field");
				Assert.That(dto.Day, Is.EqualTo(DayOfWeek.Friday));
				Assert.That(dto.Plain, Is.EqualTo("c"));
			}
		}

		[Test]
		public void Test_Both_Paths_Produce_The_Same_Bytes()
		{
			// the reflection bridge always read the PropertyName property, whichever spelling filled it:
			// separate green suites never prove the two paths agree on the wire
			var dto = new ProbeNamedWireFormDto { Field = "a", SubField = "b", Day = DayOfWeek.Monday, Plain = "c" };

			var generated = ProbeNamedWireFormHost.ProbeNamedWireFormDto.ToJsonText(dto);
			var reflection = CrystalJson.Serialize(dto);

			Assert.That(generated, Is.EqualTo(reflection));
		}

	}

}
