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
	using System.Runtime.Serialization;
	using STJ = System.Text.Json.Serialization;

	/// <summary>Pins the System.Text.Json <c>[JsonInclude]</c> semantics on the reflection path (non-public members and accessors)</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	[SetInvariantCulture]
	public sealed class CrystalJsonIncludeFacts : SimpleTest
	{

		public sealed class IncludeDto
		{

			[STJ.JsonInclude]
			private string? Secret { get; set; }

			[STJ.JsonInclude]
			private int Level;

			// no [JsonInclude]: non-public members stay invisible
			private string? StaysHidden { get; set; }

			[STJ.JsonInclude]
			public string? Guarded { get; private set; }

			public int Plain { get; set; }

			public void Init(string secret, int level, string hidden, string guarded)
			{
				this.Secret = secret;
				this.Level = level;
				this.StaysHidden = hidden;
				this.Guarded = guarded;
			}

			public (string? Secret, int Level, string? StaysHidden) Expose() => (this.Secret, this.Level, this.StaysHidden);

		}

		[DataContract]
		public sealed class LegacyIncludeDto
		{

			[DataMember(Name = "secret")]
			[STJ.JsonInclude]
			private string? Secret { get; set; }

			// private [DataMember] WITHOUT [JsonInclude]: serialized anyway (hybrid rule, the DataContract
			// model is accessibility-blind and the attribute pair is the explicit declaration of intent)
			[DataMember]
			private string? AlsoSerialized { get; set; }

			// [JsonInclude] WITHOUT [DataMember]: no membership on a [DataContract] type (the opt-in is [DataMember])
			[STJ.JsonInclude]
			private string? IncludeOnly { get; set; }

			[DataMember]
			public string? Name { get; set; }

			public void Init(string secret, string also, string includeOnly)
			{
				this.Secret = secret;
				this.AlsoSerialized = also;
				this.IncludeOnly = includeOnly;
			}

			public string? ExposeSecret() => this.Secret;

		}

		[Test]
		public void Test_JsonInclude_Serializes_NonPublic_Members()
		{
			var dto = new IncludeDto { Plain = 1 };
			dto.Init("s3cr3t", 42, "invisible", "g");

			var obj = CrystalJson.Parse(CrystalJson.Serialize(dto)).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj.Get<string>("Secret"), Is.EqualTo("s3cr3t"), "[JsonInclude] private property must be serialized");
				Assert.That(obj.Get<int>("Level", -1), Is.EqualTo(42), "[JsonInclude] private field must be serialized");
				Assert.That(obj.ContainsKey("StaysHidden"), Is.False, "a non-public member without [JsonInclude] stays invisible");
				Assert.That(obj.Get<string>("Guarded"), Is.EqualTo("g"));
				Assert.That(obj.Get<int>("Plain", -1), Is.EqualTo(1));
			}
		}

		[Test]
		public void Test_JsonInclude_Binds_NonPublic_Members()
		{
			var dto = CrystalJson.Deserialize<IncludeDto>("""{ "Secret": "shh", "Level": 7, "StaysHidden": "nope", "Guarded": "ok", "Plain": 2 }""");
			var (secret, level, staysHidden) = dto.Expose();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(secret, Is.EqualTo("shh"), "[JsonInclude] private property must bind on read");
				Assert.That(level, Is.EqualTo(7), "[JsonInclude] private field must bind on read");
				Assert.That(staysHidden, Is.Null, "a non-public member without [JsonInclude] must not bind");
				Assert.That(dto.Guarded, Is.EqualTo("ok"), "[JsonInclude] must unlock a non-public setter of a public property");
				Assert.That(dto.Plain, Is.EqualTo(2));
			}
		}

		[Test]
		public void Test_JsonInclude_On_DataContract_Types_Membership_Comes_From_DataMember()
		{
			var dto = new LegacyIncludeDto { Name = "n" };
			dto.Init("s3cr3t", "also", "io");

			var obj = CrystalJson.Parse(CrystalJson.Serialize(dto)).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj.Get<string>("secret"), Is.EqualTo("s3cr3t"), "private [DataMember] + [JsonInclude] serializes under the DataMember name ([JsonInclude] is now redundant there)");
				Assert.That(obj.Get<string>("AlsoSerialized"), Is.EqualTo("also"), "hybrid rule: a private [DataMember] serializes automatically on a [DataContract] type");
				Assert.That(obj.ContainsKey("IncludeOnly"), Is.False, "[JsonInclude] without [DataMember] grants no membership on a [DataContract] type");
				Assert.That(obj.Get<string>("Name"), Is.EqualTo("n"));
			}

			var back = CrystalJson.Deserialize<LegacyIncludeDto>("""{ "secret": "shh", "Name": "m" }""");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(back.ExposeSecret(), Is.EqualTo("shh"), "private [DataMember] + [JsonInclude] must bind on read");
				Assert.That(back.Name, Is.EqualTo("m"));
			}
		}

	}

}
