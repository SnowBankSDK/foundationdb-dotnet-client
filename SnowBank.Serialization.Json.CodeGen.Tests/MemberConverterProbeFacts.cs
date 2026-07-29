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

		public JsonValue Pack(bool instance, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
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

		[JsonBooleanLiterals("N", "Y")]
		public bool? Maybe { get; set; }

		[JsonProperty("day", EnumFormat = JsonEnumFormat.String)]
		public DayOfWeek Day { get; set; }

		public ProbeCourierKind Kind { get; set; }

	}

	[CrystalJsonConverter]
	[CrystalJsonSerializable(typeof(ProbeConvertedDto))]
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
			var dto = new ProbeConvertedDto { Enabled = true, Maybe = false, Day = DayOfWeek.Friday, Kind = ProbeCourierKind.Electronic };

			var obj = JsonObject.Parse(ProbeConverterHost.ProbeConvertedDto.ToJsonText(dto)).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj.Get<string>("Enabled"), Is.EqualTo("1"), "[JsonConverter] must shape the wire through the generated Serialize");
				Assert.That(obj.Get<string>("Maybe"), Is.EqualTo("N"), "[JsonBooleanLiterals] must shape the wire through the generated Serialize");
				Assert.That(obj.Get<string>("day"), Is.EqualTo("Friday"), "[JsonProperty(EnumFormat = String)] must force the string form, even though the settings default to numbers");
				Assert.That(obj.Get<int>("Kind"), Is.EqualTo(1), "an enum member without EnumFormat keeps the settings default (numbers)");
			}

			// with EnumsAsString, the token declared on the enum's own field must be used
			var objStr = JsonObject.Parse(ProbeConverterHost.ProbeConvertedDto.ToJsonText(dto, CrystalJsonSettings.Json.WithEnumAsStrings())).AsObject();
			Assert.That(objStr.Get<string>("Kind"), Is.EqualTo("E"), "enum tokens must flow through the generated Serialize");
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

	}

}
