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
	using System.Text.Json.Serialization;

	#region Probe types...

	public sealed record ProbeIncludeDto
	{

		// non-public [JsonInclude] members are honored by the reflection path, but NOT by the generator yet:
		// the generator emits a SYSLIB1038 warning at build time and omits the member (interim gap)
		[JsonInclude]
		private string? Secret { get; set; }

		public int Plain { get; set; }

		public void Init(string secret) => this.Secret = secret;

	}

	[CrystalJsonConverter]
	[CrystalJsonSerializable(typeof(ProbeIncludeDto))]
	public static partial class IncludeProbeConverters
	{
		// generated code goes here!
	}

	#endregion

	/// <summary>Pins the INTERIM state of <c>[JsonInclude]</c> on the source-generated path (diagnostic + omission, until unsafe accessors land)</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class JsonIncludeProbeFacts : SimpleTest
	{

		[Test]
		public void Test_NonPublic_JsonInclude_Member_Is_Omitted_By_Generated_Converter()
		{
			// the generated converter must still compile and work, minus the non-public member
			var dto = new ProbeIncludeDto { Plain = 1 };
			dto.Init("s3cr3t");

			var obj = JsonObject.Parse(IncludeProbeConverters.ProbeIncludeDto.ToJsonText(dto)).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj.ContainsKey("Secret"), Is.False, "interim: the generator omits non-public [JsonInclude] members (SYSLIB1038 at build time)");
				Assert.That(obj.Get<int>("Plain", -1), Is.EqualTo(1));
			}
		}

	}

}
