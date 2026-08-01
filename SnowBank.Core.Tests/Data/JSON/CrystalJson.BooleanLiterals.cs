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

		/// <summary>The omit-when-false form: one attribute carrying the intent</summary>
		public sealed class OmitWhenFalseDto
		{
			[JsonBooleanLiterals(null, "1")]
			public bool Flag { get; set; }
		}

		/// <summary>The idiom for "emit true, or emit nothing": ordinary JSON booleans, the attribute changing only the omission</summary>
		public sealed class OmitWhenFalseBoolDto
		{
			[JsonBooleanLiterals(null, true)]
			public bool Flag { get; set; }

			[JsonBooleanLiterals(null, true, StrictLiterals = true)]
			public bool Strict { get; set; }
		}

		/// <summary>The same wire expressed the long way, as two attributes describing a mechanism</summary>
		public sealed class CompositionDto
		{
			[JsonBooleanLiterals("0", "1")]
			[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
			public bool Flag { get; set; }
		}

		[Test]
		public void Test_Null_False_Literal_Omits_The_Member()
		{
			Assert.That(CrystalJson.Serialize(new OmitWhenFalseDto { Flag = true }, CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"Flag":"1"}"""));
			Assert.That(CrystalJson.Serialize(new OmitWhenFalseDto { Flag = false }, CrystalJsonSettings.JsonCompact), Is.EqualTo("{}"), "false emits nothing at all");

			// the DOM route must agree, or the two write routes disagree about the member's very presence
			Assert.That(JsonValue.FromValue(new OmitWhenFalseDto { Flag = false }, CrystalJsonSettings.JsonCompact).ToJsonText(CrystalJsonSettings.JsonCompact), Is.EqualTo("{}"), "DOM route");
		}

		[Test]
		public void Test_One_Attribute_And_Two_Attributes_Produce_The_Same_Wire()
		{
			// the most valuable pin in this fixture: a consumer composing [JsonBooleanLiterals] with
			// [JsonIgnore(WhenWritingDefault)] must get BYTE-IDENTICAL output to the single-attribute form, because
			// the short form is only sugar over exactly that composition. If these ever diverge, migrating from one
			// spelling to the other silently changes a wire.
			foreach (var value in new[] { true, false })
			{
				var one = CrystalJson.Serialize(new OmitWhenFalseDto { Flag = value }, CrystalJsonSettings.JsonCompact);
				var two = CrystalJson.Serialize(new CompositionDto { Flag = value }, CrystalJsonSettings.JsonCompact);
				Assert.That(one, Is.EqualTo(two), $"the two spellings must agree byte for byte (Flag = {value})");
			}
		}

		[Test]
		public void Test_Omit_When_False_Read_Matrix()
		{
			// absent and an explicit null both read false: absent because there is nothing to bind, null because the
			// pipeline owns null before any member converter sees it. Both land on default(bool), which IS false,
			// and that is the whole point of the shape (the producer emits the member only when it is true).
			Assert.That(CrystalJson.Deserialize<OmitWhenFalseDto>("{}")!.Flag, Is.False, "absent reads false");
			Assert.That(CrystalJson.Deserialize<OmitWhenFalseDto>("""{"Flag":null}""")!.Flag, Is.False, "an explicit null reads false");
			Assert.That(CrystalJson.Deserialize<OmitWhenFalseDto>("""{"Flag":"1"}""")!.Flag, Is.True, "the true literal reads true");

			// reading stays tolerant of a modernized producer, exactly as with a non-null false literal
			Assert.That(CrystalJson.Deserialize<OmitWhenFalseDto>("""{"Flag":true}""")!.Flag, Is.True, "a genuine boolean is still accepted");

			// and an unknown literal still refuses rather than silently reading false
			Assert.That(() => CrystalJson.Deserialize<OmitWhenFalseDto>("""{"Flag":"maybe"}"""), Throws.InstanceOf<JsonBindingException>());
		}

		[Test]
		public void Test_Emit_True_Or_Nothing_Is_Ordinary_Booleans_Plus_Omission()
		{
			// the idiom: no custom literal at all, the wire stays ordinary JSON booleans, and the ONLY thing the
			// attribute changes is that false is not emitted
			Assert.That(CrystalJson.Serialize(new OmitWhenFalseBoolDto { Flag = true }, CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"Flag":true}"""));
			Assert.That(CrystalJson.Serialize(new OmitWhenFalseBoolDto { Flag = false }, CrystalJsonSettings.JsonCompact), Is.EqualTo("{}"));

			// [JsonBooleanLiterals(false, true)] is an identity: both literals are what a bool serializes to anyway.
			// Legal, and it does nothing, which is worth pinning because someone will write it.
			Assert.That(new JsonBooleanLiteralsAttribute(false, true).FalseLiteral, Is.EqualTo(JsonBoolean.False));
			Assert.That(new JsonBooleanLiteralsAttribute(false, true).TrueLiteral, Is.EqualTo(JsonBoolean.True));
		}

		[Test]
		public void Test_StrictLiterals_With_No_False_Literal()
		{
			// RULED HERE, since the combination has no obvious prior answer: with whenFalse null there is no configured
			// false literal, so a present `false` reads as FALSE even under StrictLiterals. Absence already means
			// false in this shape, and an explicit false is that same state spelled out, so refusing it would reject a
			// value the shape considers legal. Strict still does its job on anything that is not a configured literal.
			Assert.That(CrystalJson.Deserialize<OmitWhenFalseBoolDto>("""{"Strict":false}""")!.Strict, Is.False, "an explicit false reads false, even under StrictLiterals");
			Assert.That(CrystalJson.Deserialize<OmitWhenFalseBoolDto>("""{"Strict":true}""")!.Strict, Is.True, "and the configured literal IS a genuine boolean here, so strict must not reject it");
			Assert.That(CrystalJson.Deserialize<OmitWhenFalseBoolDto>("{}")!.Strict, Is.False, "absent reads false");
			Assert.That(CrystalJson.Deserialize<OmitWhenFalseBoolDto>("""{"Strict":null}""")!.Strict, Is.False, "an explicit null reads false");
			Assert.That(() => CrystalJson.Deserialize<OmitWhenFalseBoolDto>("""{"Strict":"1"}"""), Throws.InstanceOf<JsonBindingException>(), "strict still rejects a value that is not a configured literal");
		}

		[Test]
		public void Test_Literal_Type_Guard()
		{
			// the arguments are `object`, so the compiler no longer rejects a bad literal type: this guard replaces it
			var ex = Assert.Throws<ArgumentException>(() => _ = new JsonBooleanLiteralsAttribute(System.DayOfWeek.Friday, "1"));
			Assert.That(ex!.Message, Does.StartWith(string.Format(CrystalJson.Errors.BooleanLiteralTypeNotSupported, "whenFalse", "DayOfWeek")));

			Assert.Throws<ArgumentNullException>(() => _ = new JsonBooleanLiteralsAttribute("0", null!), "a true literal is required");

			// a mixed pair is deliberately legal: legacy wires are not always internally consistent
			Assert.That(new JsonBooleanLiteralsAttribute("0", 1).TrueLiteral, Is.EqualTo(JsonNumber.Return(1)));
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
