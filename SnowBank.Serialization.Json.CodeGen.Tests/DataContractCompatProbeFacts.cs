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
	using System.Runtime.Serialization;

	#region Probe types...

	/// <summary>Legacy DataContract DTO, as found in a DCJS-era application</summary>
	[DataContract]
	public sealed record ProbeLegacyDto
	{

		[DataMember(Name = "renamed_id")]
		public string? Id { get; set; }

		// no [DataMember]: DCJS would exclude it
		public string? NotAMember { get; set; }

		[System.Text.Json.Serialization.JsonIgnore]
		public string? Hidden { get; set; }

	}

	[CrystalJsonConverter]
	[CrystalJsonSerializable(typeof(ProbeLegacyDto))]
	public static partial class ProbeConverters
	{
		// generated code goes here!
	}

	#endregion

	/// <summary>Probes that pin how the SOURCE-GENERATED path treats legacy DataContract DTOs (it does not recognize them)</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class DataContractCompatProbeFacts : SimpleTest
	{

		[Test]
		public void Test_Generator_Ignores_DataContract_And_JsonIgnore()
		{
			var dto = new ProbeLegacyDto { Id = "X1", NotAMember = "kept", Hidden = "visible" };

			var obj = JsonObject.Parse(ProbeConverters.ProbeLegacyDto.ToJsonText(dto)).AsObject();

			using (Assert.EnterMultipleScope())
			{
				// [DataMember(Name=...)] is NOT recognized: the member is emitted under its C# name
				Assert.That(obj.Get<string>("Id"), Is.EqualTo("X1"), "the generator does not read [DataMember] renames");
				Assert.That(obj.ContainsKey("renamed_id"), Is.False);

				// [DataContract] opt-in is NOT recognized: all public members are serialized
				Assert.That(obj.Get<string>("NotAMember"), Is.EqualTo("kept"), "the generator does not apply the [DataContract] opt-in rule");

				// [JsonIgnore] is NOT recognized by the generator
				Assert.That(obj.Get<string>("Hidden"), Is.EqualTo("visible"), "the generator does not honor [JsonIgnore]");
			}
		}

	}

}
