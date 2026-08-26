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

		// non-public [JsonInclude] members are honored by BOTH paths: the generator reaches them
		// through accessor thunks ([UnsafeAccessor] on modern TFMs, reflection accessors downlevel)
		[JsonInclude]
		private string? Secret { get; set; }

		[JsonInclude]
		private int Level;

		// [JsonInclude] also unlocks the non-public accessor of a public property
		[JsonInclude]
		public string? Guarded { get; private set; }

		public int Plain { get; set; }

		public void Init(string secret, int level, string guarded)
		{
			this.Secret = secret;
			this.Level = level;
			this.Guarded = guarded;
		}

		public (string? Secret, int Level) Expose() => (this.Secret, this.Level);

	}

	[CrystalJsonConverter]
	[CrystalSerializable(typeof(ProbeIncludeDto))]
	public static partial class IncludeProbeConverters
	{
		// generated code goes here!
	}

	#endregion

	/// <summary>Pins <c>[JsonInclude]</c> on the source-generated path: non-public members and accessors are honored, matching the reflection path (this very test project compiles the thunks, so the pins below execute real generated code)</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class JsonIncludeProbeFacts : SimpleTest
	{

		[Test]
		public void Test_NonPublic_JsonInclude_Members_Are_Included_By_Generated_Converter()
		{
			var dto = new ProbeIncludeDto { Plain = 1 };
			dto.Init("s3cr3t", 42, "g");

			var json = IncludeProbeConverters.ProbeIncludeDto.ToJsonText(dto);
			var obj = JsonObject.Parse(json).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj.Get<string>("Secret", ""), Is.EqualTo("s3cr3t"), "a private [JsonInclude] property is serialized through its accessor thunk");
				Assert.That(obj.Get<int>("Level", -1), Is.EqualTo(42), "a private [JsonInclude] field is serialized through its accessor thunk");
				Assert.That(obj.Get<string>("Guarded", ""), Is.EqualTo("g"));
				Assert.That(obj.Get<int>("Plain", -1), Is.EqualTo(1));
			}

			// the two paths agree on membership and values (member ORDER may differ: reflection lists fields
			// before properties, the generator keeps declaration order; order is not part of the contract)
			Assert.That(JsonObject.Parse(json), IsJson.EqualTo(JsonObject.Parse(CrystalJson.Serialize(dto))), "generated and reflection outputs must agree on content");
		}

		[Test]
		public void Test_NonPublic_JsonInclude_Members_Bind_Through_Generated_Converter()
		{
			var back = IncludeProbeConverters.ProbeIncludeDto.Deserialize("""{ "Secret": "shh", "Level": 7, "Guarded": "ok", "Plain": 2 }""");
			var (secret, level) = back.Expose();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(secret, Is.EqualTo("shh"), "a private [JsonInclude] property binds through its setter thunk");
				Assert.That(level, Is.EqualTo(7), "a private [JsonInclude] field binds through its setter thunk");
				Assert.That(back.Guarded, Is.EqualTo("ok"), "[JsonInclude] unlocks the non-public setter of a public property");
				Assert.That(back.Plain, Is.EqualTo(2));
			}
		}

	}

}
