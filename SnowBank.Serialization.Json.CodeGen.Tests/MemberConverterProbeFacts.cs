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

	/// <summary>Custom scalar converter usable by both the reflection path and generated converters</summary>
	public sealed class ProbeBitStringConverter : IJsonMemberConverter<bool>
	{

		public JsonValue Pack(ref CrystalJsonPackContext context, bool instance)
			=> JsonString.Return(instance ? "1" : "0");

		public bool Unpack(JsonValue value, ICrystalJsonTypeResolver? resolver)
			=> value switch
			{
				JsonBoolean b => b.ToBoolean(),
				JsonString s => s.Value is "1",
				_ => throw new JsonBindingException($"Cannot convert {value.Type} into a bit-string boolean")
			};

	}

	/// <summary>Enum whose wire form is a domain code, declared on its own fields (DataContract spelling)</summary>
	public enum ProbeCourierKind
	{
		[EnumMember(Value = "C")]
		Paper = 0,

		[EnumMember(Value = "E")]
		Electronic = 1,
	}

	public sealed record ProbeConvertedDto
	{

		[System.Text.Json.Serialization.JsonConverter(typeof(ProbeBitStringConverter))]
		public bool Enabled { get; set; }

		// the native attribute, for CrystalJson-only converters (STJ never inspects it)
		[JsonConvertWith(typeof(ProbeBitStringConverter))]
		public bool Native { get; set; }

		// both spellings on one member: the native attribute must win
		[JsonConvertWith(typeof(ProbeBitStringConverter))]
		[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
		public bool Mixed { get; set; }

		[JsonBooleanLiterals("N", "Y")]
		public bool? Maybe { get; set; }

		[JsonProperty("day", EnumFormat = JsonEnumFormat.String)]
		public DayOfWeek Day { get; set; }

		public ProbeCourierKind Kind { get; set; }

		// the DataContract opt-out: excluded by the generator like an unconditional [JsonIgnore]
		[IgnoreDataMember]
		public string? Ghost { get; set; }

	}

	/// <summary>The omit-when-false shapes, so the generated path can be compared against reflection</summary>
	public sealed class ProbeOmitWhenFalseDto
	{
		[JsonBooleanLiterals(null, "1")]
		public bool Literal { get; set; }

		[JsonBooleanLiterals(null, true)]
		public bool Plain { get; set; }
	}

	/// <summary>Asymmetric converter: packing facet only</summary>
	public sealed class ProbePackOnlyConverter : IJsonPacker<bool>
	{
		public JsonValue Pack(ref CrystalJsonPackContext context, bool instance)
			=> JsonString.Return(instance ? "1" : "0");
	}

	/// <summary>Asymmetric converter: deserializing facet only</summary>
	public sealed class ProbeUnpackOnlyConverter : IJsonDeserializer<bool>
	{
		public bool Unpack(JsonValue value, ICrystalJsonTypeResolver? resolver)
			=> value switch
			{
				JsonString s => s.Value is "1",
				JsonBoolean b => b.ToBoolean(),
				_ => throw new JsonBindingException($"Cannot convert {value.Type} into a bit-string boolean")
			};
	}

	public sealed record ProbePackOnlyDto
	{
		[JsonConvertWith(typeof(ProbePackOnlyConverter))]
		public bool Flag { get; set; }
	}

	public sealed record ProbeUnpackOnlyDto
	{
		[JsonConvertWith(typeof(ProbeUnpackOnlyConverter))]
		public bool Flag { get; set; }
	}

	/// <summary>Legacy-shaped converter declared for the NULLABLE form itself: a present-but-unreadable value answers "no value"</summary>
	/// <remarks>Both throw-arms pin the pipeline invariant race-free: the pipeline owns null and missing on BOTH sides, so if any route ever hands them to the converter, whatever test triggered it fails loudly.</remarks>
	public sealed class ProbeNullableFormCountConverter : IJsonMemberConverter<int?>
	{

		public JsonValue Pack(ref CrystalJsonPackContext context, int? instance)
			// the legacy body wrote "" for a null member; that form must stay unreachable, the pipeline owns the null-member wire
			=> instance is { } value ? JsonString.Return(value.ToString(System.Globalization.CultureInfo.InvariantCulture)) : throw new InvalidOperationException("Pack must never see a null member");

		public int? Unpack(JsonValue value, ICrystalJsonTypeResolver? resolver)
			=> value switch
			{
				JsonString s => int.TryParse(s.Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : null,
				JsonNumber n => n.ToInt32(),
				JsonNull => throw new InvalidOperationException("the converter must never see JSON null or a missing member"),
				_ => throw new JsonBindingException($"Cannot convert {value.Type} into an optional count")
			};

	}

	/// <summary>Converter declared for the underlying value type, applied to a nullable member through the lift</summary>
	public sealed class ProbeHexCountConverter : IJsonMemberConverter<int>
	{
		public JsonValue Pack(ref CrystalJsonPackContext context, int instance)
			=> JsonString.Return(instance.ToString("x", System.Globalization.CultureInfo.InvariantCulture));

		public int Unpack(JsonValue value, ICrystalJsonTypeResolver? resolver)
			=> value is JsonString s ? int.Parse(s.Value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture) : value.ToInt32();
	}

	public sealed record ProbeNullableFormDto
	{
		[JsonConvertWith(typeof(ProbeNullableFormCountConverter))]
		public int? Count { get; set; }

		[JsonConvertWith(typeof(ProbeHexCountConverter))]
		public int? Lifted { get; set; }
	}

	public sealed record ProbeRequiredNullableFormDto
	{
		[JsonConvertWith(typeof(ProbeNullableFormCountConverter))]
		public required int? Count { get; set; }
	}

	[CrystalJsonConverter]
	[CrystalSerializable(typeof(ProbeConvertedDto))]
	[CrystalSerializable(typeof(ProbePackOnlyDto))]
	[CrystalSerializable(typeof(ProbeUnpackOnlyDto))]
	[CrystalSerializable(typeof(ProbeNullableFormDto))]
	[CrystalSerializable(typeof(ProbeOmitWhenFalseDto))]
	[CrystalSerializable(typeof(ProbeRequiredNullableFormDto))]
	public static partial class ProbeConverterHost
	{
		// generated code goes here!
	}

	#endregion

	/// <summary>Probes that the SOURCE-GENERATED path honors member converters, boolean literals, per-member EnumFormat, and enum tokens</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class MemberConverterProbeFacts : SimpleTest
	{

		[Test]
		public void Test_Generated_Serialize_Honors_Member_Converters()
		{
			var dto = new ProbeConvertedDto { Enabled = true, Native = true, Mixed = true, Maybe = false, Day = DayOfWeek.Friday, Kind = ProbeCourierKind.Electronic, Ghost = "boo" };

			var obj = JsonObject.Parse(ProbeConverterHost.ProbeConvertedDto.ToJsonText(dto)).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj.Get<string>("Enabled"), Is.EqualTo("1"), "[JsonConverter] must shape the wire through the generated Serialize");
				Assert.That(obj.Get<string>("Native"), Is.EqualTo("1"), "[JsonConvertWith] must shape the wire through the generated Serialize");
				Assert.That(obj.Get<string>("Mixed"), Is.EqualTo("1"), "the native attribute must win over a foreign spelling on the same member");
				Assert.That(obj.Get<string>("Maybe"), Is.EqualTo("N"), "[JsonBooleanLiterals] must shape the wire through the generated Serialize");
				Assert.That(obj.Get<string>("day"), Is.EqualTo("Friday"), "[JsonProperty(EnumFormat = String)] forces the string form regardless of the settings");
				Assert.That(obj.Get<string>("Kind"), Is.EqualTo("E"), "an enum member without EnumFormat follows the settings default (strings), with its token");
				Assert.That(obj.ContainsKey("Ghost"), Is.False, "[IgnoreDataMember] excludes the member from the generated converter");
			}

			// the numeric opt-in still applies to members without a per-member override
			var objNum = JsonObject.Parse(ProbeConverterHost.ProbeConvertedDto.ToJsonText(dto, CrystalJsonSettings.Json.WithEnumAsNumbers())).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(objNum.Get<int>("Kind"), Is.EqualTo(1), "WithEnumAsNumbers() restores the numeric wire through the generated Serialize");
				Assert.That(objNum.Get<string>("day"), Is.EqualTo("Friday"), "the per-member EnumFormat=String override wins over the numeric setting");
			}
		}

		[Test]
		public void Test_Generated_Pack_Honors_Member_Converters()
		{
			var dto = new ProbeConvertedDto { Enabled = false, Maybe = true, Day = DayOfWeek.Monday, Kind = ProbeCourierKind.Paper };

			var obj = ProbeConverterHost.ProbeConvertedDto.Pack(dto).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj.Get<string>("Enabled"), Is.EqualTo("0"), "[JsonConverter] must shape the DOM through the generated Pack");
				Assert.That(obj.Get<string>("Maybe"), Is.EqualTo("Y"), "[JsonBooleanLiterals] must shape the DOM through the generated Pack");
				Assert.That(obj.Get<string>("day"), Is.EqualTo("Monday"), "[JsonProperty(EnumFormat = String)] must force the string form on the Pack route too");
			}
		}

		[Test]
		public void Test_Generated_Unpack_Honors_Member_Converters()
		{
			var dto = ProbeConverterHost.ProbeConvertedDto.Deserialize("""{ "Enabled": "1", "Maybe": "y", "day": "friday", "Kind": "E" }""");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(dto.Enabled, Is.True, "the converter reads its custom literal");
				Assert.That(dto.Maybe, Is.True, "boolean literals read case-insensitively");
				Assert.That(dto.Day, Is.EqualTo(DayOfWeek.Friday), "enum strings bind case-insensitively");
				Assert.That(dto.Kind, Is.EqualTo(ProbeCourierKind.Electronic), "enum tokens bind through the generated Unpack");
			}

			// genuine forms still accepted (tolerant read)
			var dto2 = ProbeConverterHost.ProbeConvertedDto.Deserialize("""{ "Enabled": true, "Maybe": false, "day": 5, "Kind": 0 }""");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(dto2.Enabled, Is.True);
				Assert.That(dto2.Maybe, Is.False);
				Assert.That(dto2.Day, Is.EqualTo(DayOfWeek.Friday));
				Assert.That(dto2.Kind, Is.EqualTo(ProbeCourierKind.Paper));
			}

			// missing nullable member stays null
			Assert.That(ProbeConverterHost.ProbeConvertedDto.Deserialize("{ }").Maybe, Is.Null);
		}

		[Test]
		public void Test_Proxies_Honor_Member_Converters()
		{
			// using a proxy must behave identically to serializing/deserializing the concrete entity type
			var dto = new ProbeConvertedDto { Enabled = true, Maybe = true, Day = DayOfWeek.Friday, Kind = ProbeCourierKind.Electronic };

			// read-only proxy: typed reads must return CLR values, decoded through the member converters
			var ro = ProbeConverterHost.ProbeConvertedDto.ToReadOnly(dto);
			using (Assert.EnterMultipleScope())
			{
				Assert.That(ro.Enabled, Is.True, "reads back through the [JsonConverter] member converter");
				Assert.That(ro.Maybe, Is.True, "reads back through the [JsonBooleanLiterals] converter");
				Assert.That(ro.Day, Is.EqualTo(DayOfWeek.Friday), "reads back an EnumFormat=String member");
				Assert.That(ro.Kind, Is.EqualTo(ProbeCourierKind.Electronic), "reads back a token-carrying enum");
			}

			// writable proxy: setting a member must produce the same wire as packing the entity
			var w = ProbeConverterHost.ProbeConvertedDto.ToMutable(dto);
			w.Enabled = false;
			w.Maybe = false;
			w.Day = DayOfWeek.Monday;
			var json = w.ToJsonValue().AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(json.Get<string>("Enabled"), Is.EqualTo("0"), "the setter must write the converter's wire form");
				Assert.That(json.Get<string>("Maybe"), Is.EqualTo("N"), "the setter must write the configured boolean literal");
				Assert.That(json.Get<string>("day"), Is.EqualTo("Monday"), "the setter must honor EnumFormat=String");
			}

			// and the proxy round-trips back to the same entity values
			var back = w.ToValue();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(back.Enabled, Is.False);
				Assert.That(back.Maybe, Is.False);
				Assert.That(back.Day, Is.EqualTo(DayOfWeek.Monday));
				Assert.That(back.Kind, Is.EqualTo(ProbeCourierKind.Electronic));
			}
		}

		[Test]
		public void Test_Generated_Asymmetric_Converter_Facets()
		{
			// pack-only: serializing works, deserializing a PRESENT value fails loudly with a teaching message
			Assert.That(ProbeConverterHost.ProbePackOnlyDto.ToJsonText(new ProbePackOnlyDto { Flag = true }), Does.Contain("\"1\""));
			Assert.That(ProbeConverterHost.ProbePackOnlyDto.Deserialize("{ }").Flag, Is.False, "an absent member never invokes the converter");
			Assert.That(
				() => ProbeConverterHost.ProbePackOnlyDto.Deserialize("""{ "Flag": "1" }"""),
				Throws.Exception.With.Message.Contain(nameof(ProbePackOnlyConverter))
					.And.Message.Contain("IJsonDeserializer").And.Message.Contain("Unpack"));

			// unpack-only: deserializing works, any serialize attempt fails loudly with a teaching message
			Assert.That(ProbeConverterHost.ProbeUnpackOnlyDto.Deserialize("""{ "Flag": "1" }""").Flag, Is.True);
			Assert.That(
				() => ProbeConverterHost.ProbeUnpackOnlyDto.ToJsonText(new ProbeUnpackOnlyDto { Flag = true }),
				Throws.Exception.With.Message.Contain(nameof(ProbeUnpackOnlyConverter))
					.And.Message.Contain("IJsonPacker").And.Message.Contain("Pack"));
			Assert.That(
				() => ProbeConverterHost.ProbeUnpackOnlyDto.Pack(new ProbeUnpackOnlyDto { Flag = true }),
				Throws.Exception.With.Message.Contain("IJsonPacker"), "the Pack route fails the same way");
		}

		[Test]
		public void Test_NullableForm_Converter_Distinguishes_No_Value_From_Default_And_From_Null()
		{
			// the three outcomes must stay distinguishable, because default(T) is itself a legitimate domain value (0 for int?):
			// 1) present but unreadable -> the CONVERTER answers "no value" ("xx" is the proof of WHO answered: a route
			//    that bypassed the converter could only throw on it, never answer null)
			Assert.That(ProbeConverterHost.ProbeNullableFormDto.Deserialize("""{ "Count": "" }""").Count, Is.Null, "a present-but-unreadable value binds to no-value");
			Assert.That(ProbeConverterHost.ProbeNullableFormDto.Deserialize("""{ "Count": "xx" }""").Count, Is.Null, "the converter, not the pipeline, answered no-value");

			// 2) default(T) round-trips as a real value, NOT as no-value
			Assert.That(ProbeConverterHost.ProbeNullableFormDto.Deserialize("""{ "Count": "0" }""").Count, Is.EqualTo(0), "zero is a domain value, distinct from no-value");

			// 3) JSON null -> the PIPELINE answers; the converter never runs, even in the T? form (it would throw)
			Assert.That(ProbeConverterHost.ProbeNullableFormDto.Deserialize("""{ "Count": null }""").Count, Is.Null);
			Assert.That(ProbeConverterHost.ProbeNullableFormDto.Deserialize("{ }").Count, Is.Null);

			// and a readable value still binds through the converter
			Assert.That(ProbeConverterHost.ProbeNullableFormDto.Deserialize("""{ "Count": "42" }""").Count, Is.EqualTo(42));
		}

		[Test]
		public void Test_NullableForm_Converter_Write_Of_Null_Stays_Pipeline_Controlled()
		{
			// the T? declaration transfers the READ side only: Pack never sees null (the converter throws if it ever
			// does), so its legacy null-write form is unreachable by design, and the wire follows the settings
			var dto = new ProbeNullableFormDto { Count = null };

			var obj = JsonObject.Parse(ProbeConverterHost.ProbeNullableFormDto.ToJsonText(dto)).AsObject();
			Assert.That(obj.ContainsKey("Count"), Is.False, "a null member is omitted by default, never written as the converter's \"\" form");

			var objNulls = JsonObject.Parse(ProbeConverterHost.ProbeNullableFormDto.ToJsonText(dto, CrystalJsonSettings.Json.WithNullMembers())).AsObject();
			Assert.That(objNulls.ContainsKey("Count"), Is.True, "WithNullMembers() governs the null-member wire");
			Assert.That(objNulls["Count"].IsNull, Is.True, "the pipeline writes JSON null, not the converter's \"\" form");

			// a PRESENT value still routes through the converter, on both write routes
			Assert.That(ProbeConverterHost.ProbeNullableFormDto.Pack(new ProbeNullableFormDto { Count = 7 }).AsObject().Get<string>("Count"), Is.EqualTo("7"), "a present value routes through the converter");
			Assert.That(ProbeConverterHost.ProbeNullableFormDto.ToJsonText(new ProbeNullableFormDto { Count = 7 }), Does.Contain("\"7\""));
		}

		[Test]
		public void Test_NullableForm_Converter_Reads_Through_Proxies()
		{
			var ro = ProbeConverterHost.ProbeNullableFormDto.ToReadOnly(new ProbeNullableFormDto { Count = 7 });
			Assert.That(ro.Count, Is.EqualTo(7), "the read-only proxy decodes through the T?-form converter");

			var w = ProbeConverterHost.ProbeNullableFormDto.ToMutable(new ProbeNullableFormDto { Count = 7 });
			Assert.That(w.Count, Is.EqualTo(7), "the writable proxy decodes through the T?-form converter");

			// an absent member reads as no-value through the proxy, answered by the pipeline (the converter would throw)
			Assert.That(ProbeConverterHost.ProbeNullableFormDto.ToReadOnly(new ProbeNullableFormDto()).Count, Is.Null, "an absent member reads as no-value through the proxy");
		}

		[Test]
		public void Test_Lifted_Converter_On_Nullable_Member_Still_Lifts()
		{
			// regression pin: a converter declared for the underlying T keeps today's lifted behavior on a T? member
			using (Assert.EnterMultipleScope())
			{
				Assert.That(ProbeConverterHost.ProbeNullableFormDto.Deserialize("""{ "Lifted": "2a" }""").Lifted, Is.EqualTo(42));
				Assert.That(ProbeConverterHost.ProbeNullableFormDto.Deserialize("""{ "Lifted": null }""").Lifted, Is.Null);
				Assert.That(ProbeConverterHost.ProbeNullableFormDto.Deserialize("{ }").Lifted, Is.Null);
				Assert.That(ProbeConverterHost.ProbeNullableFormDto.Pack(new ProbeNullableFormDto { Lifted = 42 }).AsObject().Get<string>("Lifted"), Is.EqualTo("2a"));
				Assert.That(JsonObject.Parse(ProbeConverterHost.ProbeNullableFormDto.ToJsonText(new ProbeNullableFormDto { Lifted = null })).AsObject().ContainsKey("Lifted"), Is.False);
			}
		}

		[Test]
		public void Test_Required_NullableForm_Member_Rejects_Null_And_Missing_But_Accepts_Unreadable()
		{
			// required = the pipeline's null/missing gate; a PRESENT value the converter maps to no-value passes it
			using (Assert.EnterMultipleScope())
			{
				Assert.That(() => ProbeConverterHost.ProbeRequiredNullableFormDto.Deserialize("{ }"), Throws.InstanceOf<JsonBindingException>(), "a missing required member throws");
				Assert.That(() => ProbeConverterHost.ProbeRequiredNullableFormDto.Deserialize("""{ "Count": null }"""), Throws.InstanceOf<JsonBindingException>(), "an explicit null on a required member throws");
				Assert.That(ProbeConverterHost.ProbeRequiredNullableFormDto.Deserialize("""{ "Count": "42" }""").Count, Is.EqualTo(42));
				Assert.That(ProbeConverterHost.ProbeRequiredNullableFormDto.Deserialize("""{ "Count": "" }""").Count, Is.Null, "present-but-unreadable satisfies the presence gate and the converter answers no-value");
			}
		}

		[Test]
		public void Test_Generated_Round_Trip()
		{
			var dto = new ProbeConvertedDto { Enabled = true, Maybe = true, Day = DayOfWeek.Sunday, Kind = ProbeCourierKind.Electronic };
			var back = ProbeConverterHost.ProbeConvertedDto.Deserialize(ProbeConverterHost.ProbeConvertedDto.ToJsonText(dto));
			using (Assert.EnterMultipleScope())
			{
				Assert.That(back.Enabled, Is.True);
				Assert.That(back.Maybe, Is.True);
				Assert.That(back.Day, Is.EqualTo(DayOfWeek.Sunday));
				Assert.That(back.Kind, Is.EqualTo(ProbeCourierKind.Electronic));
			}
		}


		[Test]
		public void Test_Omit_When_False_Is_Identical_On_Both_Paths()
		{
			// the omission is decided from the member's flags, not inside the converter, so the generated path has to
			// resolve it at compile time exactly as the reflection path resolves it at contract build. Run both,
			// compare: a generated converter that emitted the member anyway would be a silent wire divergence.
			foreach (var value in new[] { true, false })
			{
				var dto = new ProbeOmitWhenFalseDto { Literal = value, Plain = value };
				var generated = ProbeConverterHost.ProbeOmitWhenFalseDto.ToJsonText(dto, CrystalJsonSettings.JsonCompact);
				var reflection = CrystalJson.Serialize(dto, CrystalJsonSettings.JsonCompact);
				Assert.That(generated, Is.EqualTo(reflection), $"the two paths must agree byte for byte (value = {value})");
			}

			var absent = ProbeConverterHost.ProbeOmitWhenFalseDto.ToJsonText(new ProbeOmitWhenFalseDto(), CrystalJsonSettings.JsonCompact);
			Assert.That(absent, Is.EqualTo("{}"), "and both members really are omitted when false, rather than merely agreeing on something wrong");

			var present = ProbeConverterHost.ProbeOmitWhenFalseDto.ToJsonText(new ProbeOmitWhenFalseDto { Literal = true, Plain = true }, CrystalJsonSettings.JsonCompact);
			Assert.That(present, Is.EqualTo(CrystalJson.Serialize(new ProbeOmitWhenFalseDto { Literal = true, Plain = true }, CrystalJsonSettings.JsonCompact)), "true emits the configured literal on both paths");
			Assert.That(CrystalJson.Parse(present).AsObject().Get<bool>("Plain"), Is.True, "the bool form keeps an ordinary JSON boolean on the wire");
		}

	}

}
