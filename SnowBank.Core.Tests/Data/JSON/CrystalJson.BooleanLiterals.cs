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

	/// <summary>Pins the <c>[JsonBooleanLiterals]</c> attribute: custom wire literals for booleans, tolerant read by default, strict opt-out</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	[SetInvariantCulture]
	public sealed class CrystalJsonBooleanLiteralFacts : SimpleTest
	{

		public sealed class LegacyBoolDto
		{

			[JsonBooleanLiterals("0", "1")]
			public bool Enabled { get; set; }

			[JsonBooleanLiterals("0", "1", StrictLiterals = true)]
			public bool Locked { get; set; }

			[JsonBooleanLiterals(0, 1)]
			public bool Counted { get; set; }

			[JsonBooleanLiterals("N", "Y")]
			public bool? Maybe { get; set; }

			public bool Plain { get; set; }

		}

		[Test]
		public void Test_Literals_On_Write()
		{
			var dto = new LegacyBoolDto { Enabled = true, Locked = false, Counted = true, Maybe = false, Plain = true };

			// text route
			var obj = CrystalJson.Parse(CrystalJson.Serialize(dto)).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj.Get<string>("Enabled"), Is.EqualTo("1"), "string flavour emits the configured literal");
				Assert.That(obj.Get<string>("Locked"), Is.EqualTo("0"));
				Assert.That(obj["Counted"], Is.InstanceOf<JsonNumber>(), "int flavour emits a JSON number");
				Assert.That(obj.Get<int>("Counted"), Is.EqualTo(1));
				Assert.That(obj.Get<string>("Maybe"), Is.EqualTo("N"));
				Assert.That(obj["Plain"], Is.InstanceOf<JsonBoolean>(), "members without the attribute are untouched");
			}

			// DOM route must agree
			var dom = JsonValue.FromValue(dto).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(dom.Get<string>("Enabled"), Is.EqualTo("1"));
				Assert.That(dom.Get<int>("Counted"), Is.EqualTo(1));
			}
		}

		[Test]
		public void Test_Tolerant_Read_Accepts_Literals_And_Genuine_Booleans()
		{
			// the configured literals...
			var dto = CrystalJson.Deserialize<LegacyBoolDto>("""{ "Enabled": "1", "Counted": 0, "Maybe": "Y" }""");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(dto.Enabled, Is.True);
				Assert.That(dto.Counted, Is.False);
				Assert.That(dto.Maybe, Is.True);
			}

			// ... and genuine true/false, so a modernized upstream needs no redeploy
			dto = CrystalJson.Deserialize<LegacyBoolDto>("""{ "Enabled": true, "Counted": false, "Maybe": false }""");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(dto.Enabled, Is.True);
				Assert.That(dto.Counted, Is.False);
				Assert.That(dto.Maybe, Is.False);
			}

			// string literals compare case-insensitively (lenient parse)
			Assert.That(CrystalJson.Deserialize<LegacyBoolDto>("""{ "Maybe": "y" }""").Maybe, Is.True);

			// a missing nullable member stays null
			Assert.That(CrystalJson.Deserialize<LegacyBoolDto>("{ }").Maybe, Is.Null);

			// an unknown literal is an error, not a silent false
			Assert.That(
				() => CrystalJson.Deserialize<LegacyBoolDto>("""{ "Enabled": "yes" }"""),
				Throws.InstanceOf<JsonBindingException>());
		}

		[Test]
		public void Test_Strict_Literals_Reject_Genuine_Booleans()
		{
			// the strict opt-out: catching a silently-changed upstream matters more than tolerance
			Assert.That(CrystalJson.Deserialize<LegacyBoolDto>("""{ "Locked": "1" }""").Locked, Is.True);
			Assert.That(
				() => CrystalJson.Deserialize<LegacyBoolDto>("""{ "Locked": true }"""),
				Throws.InstanceOf<JsonBindingException>(), "StrictLiterals must reject a genuine boolean");
		}

		[Test]
		public void Test_Round_Trip()
		{
			var dto = new LegacyBoolDto { Enabled = true, Locked = true, Counted = false, Maybe = true, Plain = false };
			var back = CrystalJson.Deserialize<LegacyBoolDto>(CrystalJson.Serialize(dto));
			using (Assert.EnterMultipleScope())
			{
				Assert.That(back.Enabled, Is.True);
				Assert.That(back.Locked, Is.True);
				Assert.That(back.Counted, Is.False);
				Assert.That(back.Maybe, Is.True);
				Assert.That(back.Plain, Is.False);
			}
		}

	}

}
