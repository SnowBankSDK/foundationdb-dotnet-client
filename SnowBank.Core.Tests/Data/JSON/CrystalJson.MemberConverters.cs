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

// Simulates the Newtonsoft attribute WITHOUT referencing the package: the resolver matches by name+namespace,
// so a hand-written (or generator-injected) definition must be recognized the same as the real one.
namespace Newtonsoft.Json
{
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct)]
	public sealed class JsonConverterAttribute : Attribute
	{
		public Type ConverterType { get; }

		public JsonConverterAttribute(Type converterType) => this.ConverterType = converterType;
	}
}

namespace SnowBank.Data.Json.Tests
{
	using STJ = System.Text.Json.Serialization;

	/// <summary>Pins member-level and type-level <c>[JsonConverter(typeof(...))]</c> support on the reflection path (both output routes and the bind path)</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	[SetInvariantCulture]
	public sealed class CrystalJsonMemberConverterFacts : SimpleTest
	{

		/// <summary>Legacy-style scalar converter: bool in the output as "0"/"1" strings (tolerant read)</summary>
		public sealed class BoolAsBitStringConverter : IJsonMemberConverter<bool>
		{

			public JsonValue Pack(ref CrystalJsonPackContext context, bool instance)
				=> JsonString.Return(instance ? "1" : "0");

			public bool Unpack(JsonValue value, ICrystalJsonTypeResolver? resolver)
				=> value switch
				{
					JsonBoolean b => b.ToBoolean(),
					JsonNumber n => n.ToInt32() != 0,
					JsonString s => s.Value is "1" or "true" or "True",
					_ => throw new JsonBindingException($"Cannot convert {value.Type} into a legacy bit-string boolean")
				};

		}

		public sealed class LegacyFlagsDto
		{

			[STJ.JsonConverter(typeof(BoolAsBitStringConverter))]
			public bool Enabled { get; set; }

#pragma warning disable CS0436 // deliberate: the file's own Newtonsoft.Json.JsonConverterAttribute shadows the package type on purpose, to prove name-based recognition
			[Newtonsoft.Json.JsonConverter(typeof(BoolAsBitStringConverter))]
#pragma warning restore CS0436
			public bool Archived { get; set; }

			[STJ.JsonConverter(typeof(BoolAsBitStringConverter))]
			public bool? Optional { get; set; }

			public bool Plain { get; set; }

		}

		/// <summary>Converter declared for the NULLABLE form itself: takes responsibility for the "present but unreadable" states ("" and garbage both mean "no value" in the legacy protocol)</summary>
		public sealed class LegacyOptionalDateConverter : IJsonMemberConverter<DateTime?>
		{

			public static int UnpackCalls;

			public JsonValue Pack(ref CrystalJsonPackContext context, DateTime? instance)
				// the legacy body wrote "" for a null member; that form must stay unreachable, the pipeline owns the null-member output
				=> instance is { } value ? JsonString.Return(value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)) : throw new InvalidOperationException("Pack must never see a null member");

			public DateTime? Unpack(JsonValue value, ICrystalJsonTypeResolver? resolver)
			{
				UnpackCalls++;
				return value is JsonString s && DateTime.TryParseExact(s.Value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed)
					? parsed
					: null; // "" and unparseable are both "no value" (the legacy contract consults HasValue)
			}

		}

		public sealed class LegacyOptionalDateDto
		{

			[JsonConvertWith(typeof(LegacyOptionalDateConverter))]
			public DateTime? When { get; set; }

			public int Plain { get; set; }

		}

		public sealed class WrongArityDto
		{

			// a converter declared for DateTime? on a NON-nullable member: refused loudly on the native path
			[JsonConvertWith(typeof(LegacyOptionalDateConverter))]
			public DateTime When { get; set; }

		}

		/// <summary>Type-level converter target: the whole struct has a custom scalar output form</summary>
		[STJ.JsonConverter(typeof(TemperatureConverter))]
		public readonly record struct Temperature(double Celsius);

		public sealed class TemperatureConverter : IJsonMemberConverter<Temperature>
		{

			public JsonValue Pack(ref CrystalJsonPackContext context, Temperature instance)
				=> JsonString.Return(instance.Celsius.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "C");

			public Temperature Unpack(JsonValue value, ICrystalJsonTypeResolver? resolver)
				=> value switch
				{
					JsonString s when s.Value.EndsWith("C") => new(double.Parse(s.Value.Substring(0, s.Value.Length - 1), System.Globalization.CultureInfo.InvariantCulture)),
					JsonNumber n => new(n.ToDouble()),
					_ => throw new JsonBindingException($"Cannot convert {value.Type} into a Temperature")
				};

		}

		public sealed class WeatherDto
		{
			public Temperature Outside { get; set; }
		}

		/// <summary>Naming a converter type we cannot run (a real STJ converter) must be ignored, not fail the whole type</summary>
		public sealed class ForeignConverterDto
		{

			[STJ.JsonConverter(typeof(STJ.JsonStringEnumConverter))]
			public DayOfWeek Day { get; set; }

		}

		[Test]
		public void Test_Nullable_Form_Converter_Is_Honored_On_Nullable_Member()
		{
			// JC8 ruling (a): IJsonMemberConverter<T?> is honoured when the member is nullable, with precedence
			// over the lift, so a converter can answer "no value" for a PRESENT but unreadable input

			// write: a value packs through the converter; a null member packs without it (pipeline invariant)
			var json = CrystalJson.Serialize(new LegacyOptionalDateDto { When = new DateTime(2024, 9, 20), Plain = 1 });
			Assert.That(CrystalJson.Parse(json).AsObject().Get<string>("When"), Is.EqualTo("2024-09-20"));

			// read: a readable value binds; "" and garbage are PRESENT values that reach the converter and mean "no value"
			Assert.That(CrystalJson.Deserialize<LegacyOptionalDateDto>("""{ "When": "2024-09-20", "Plain": 1 }""").When, Is.EqualTo(new DateTime(2024, 9, 20)));
			Assert.That(CrystalJson.Deserialize<LegacyOptionalDateDto>("""{ "When": "", "Plain": 1 }""").When, Is.Null, "the empty string is present, reaches the converter, and means no value");
			Assert.That(CrystalJson.Deserialize<LegacyOptionalDateDto>("""{ "When": "not a date", "Plain": 1 }""").When, Is.Null, "an unreadable value is present, reaches the converter, and means no value");

			// default(T) is itself a legitimate domain value (MinValue renders as year 1): it must bind as a VALUE, distinctly from "no value"
			Assert.That(CrystalJson.Deserialize<LegacyOptionalDateDto>("""{ "When": "0001-01-01", "Plain": 1 }""").When, Is.EqualTo(DateTime.MinValue), "default(T) is a real value, distinct from no-value and from null");

			// pipeline invariant: JSON null and missing are handled BEFORE the converter, even in the T? form
			int callsBefore = LegacyOptionalDateConverter.UnpackCalls;
			Assert.That(CrystalJson.Deserialize<LegacyOptionalDateDto>("""{ "When": null, "Plain": 1 }""").When, Is.Null);
			Assert.That(CrystalJson.Deserialize<LegacyOptionalDateDto>("""{ "Plain": 1 }""").When, Is.Null);
			Assert.That(LegacyOptionalDateConverter.UnpackCalls, Is.EqualTo(callsBefore), "null and missing never reach the converter");
		}

		[Test]
		public void Test_Nullable_Form_Converter_Write_Of_Null_Stays_Pipeline_Controlled()
		{
			// the T? declaration transfers the READ side only: Pack never sees null (the converter throws if it ever
			// does), so its legacy null-write form is unreachable by design, and the null-member output follows the settings
			var dto = new LegacyOptionalDateDto { When = null, Plain = 1 };

			var obj = CrystalJson.Parse(CrystalJson.Serialize(dto)).AsObject();
			Assert.That(obj.ContainsKey("When"), Is.False, "a null member is omitted by default, never written as the converter's \"\" form");

			var objNulls = CrystalJson.Parse(CrystalJson.Serialize(dto, CrystalJsonSettings.Json.WithNullMembers())).AsObject();
			Assert.That(objNulls.ContainsKey("When"), Is.True, "WithNullMembers() governs the null-member output");
			Assert.That(objNulls["When"].IsNull, Is.True, "the pipeline writes JSON null, not the converter's \"\" form");

			// a PRESENT value still routes through the converter
			Assert.That(CrystalJson.Parse(CrystalJson.Serialize(new LegacyOptionalDateDto { When = new DateTime(2024, 9, 20) })).AsObject().Get<string>("When"), Is.EqualTo("2024-09-20"));
		}

		[Test]
		public void Test_Nullable_Form_Converter_On_NonNullable_Member_Is_Refused_Loudly()
		{
			// the sharp edge: a T?-shaped converter on a non-nullable member fails loudly on the native path
			Assert.That(
				() => CrystalJson.Serialize(new WrongArityDto { When = new DateTime(2024, 9, 20) }),
				Throws.Exception.With.Message.Contains("When").And.Message.Contains(nameof(LegacyOptionalDateConverter)).And.Message.Contains("DateTime"),
				"the refusal names the member, the converter and the types");
		}

		[Test]
		public void Test_Member_Converter_On_Text_Route()
		{
			var dto = new LegacyFlagsDto { Enabled = true, Archived = false, Optional = true, Plain = true };
			var obj = CrystalJson.Parse(CrystalJson.Serialize(dto)).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj["Enabled"], Is.InstanceOf<JsonString>(), "the member converter must shape the output (STJ spelling)");
				Assert.That(obj.Get<string>("Enabled"), Is.EqualTo("1"));
				Assert.That(obj["Archived"], Is.InstanceOf<JsonString>(), "the member converter must shape the output (Newtonsoft spelling)");
				Assert.That(obj.Get<string>("Archived"), Is.EqualTo("0"));
				Assert.That(obj.Get<string>("Optional"), Is.EqualTo("1"), "a converter for T must lift over a T? member");
				Assert.That(obj["Plain"], Is.InstanceOf<JsonBoolean>(), "members without a converter are untouched");
			}
		}

		[Test]
		public void Test_Member_Converter_On_Dom_Route()
		{
			var dto = new LegacyFlagsDto { Enabled = true, Archived = true, Optional = null, Plain = false };
			var obj = JsonValue.FromValue(dto).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj["Enabled"], Is.InstanceOf<JsonString>(), "the DOM route must apply the member converter as well");
				Assert.That(obj.Get<string>("Enabled"), Is.EqualTo("1"));
				Assert.That(obj.Get<string>("Archived"), Is.EqualTo("1"));
				Assert.That(obj.ContainsKey("Optional"), Is.False, "a null value keeps the default null handling (omitted)");
			}
		}

		[Test]
		public void Test_Member_Converter_On_Read()
		{
			// the converter's tolerant read: custom literals AND the genuine JSON forms
			var dto = CrystalJson.Deserialize<LegacyFlagsDto>("""{ "Enabled": "1", "Archived": false, "Optional": "0", "Plain": true }""");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(dto.Enabled, Is.True, "custom literal accepted");
				Assert.That(dto.Archived, Is.False, "genuine boolean still accepted (the converter decides)");
				Assert.That(dto.Optional, Is.False);
				Assert.That(dto.Plain, Is.True);
			}

			// round-trip
			var dto2 = CrystalJson.Deserialize<LegacyFlagsDto>(CrystalJson.Serialize(new LegacyFlagsDto { Enabled = true, Optional = true }));
			Assert.That(dto2.Enabled, Is.True);
			Assert.That(dto2.Optional, Is.True);

			// missing nullable member stays null
			var dto3 = CrystalJson.Deserialize<LegacyFlagsDto>("""{ "Enabled": "1" }""");
			Assert.That(dto3.Optional, Is.Null);
		}

		[Test]
		public void Test_Type_Level_Converter()
		{
			// standalone value
			Assert.That(CrystalJson.Serialize(new Temperature(23.5)), Is.EqualTo("\"23.5C\""), "type-level converter must apply to a standalone value (text route)");
			Assert.That(JsonValue.FromValue(new Temperature(23.5)).ToStringOrDefault(), Is.EqualTo("23.5C"), "and on the DOM route");
			Assert.That(CrystalJson.Deserialize<Temperature>("\"23.5C\"").Celsius, Is.EqualTo(23.5), "and on the bind path");
			Assert.That(CrystalJson.Deserialize<Temperature>("23.5").Celsius, Is.EqualTo(23.5), "the converter decides what it tolerates on read");

			// hosted in a DTO member (no member-level attribute: the type carries it)
			var obj = CrystalJson.Parse(CrystalJson.Serialize(new WeatherDto { Outside = new(-5.25) })).AsObject();
			Assert.That(obj.Get<string>("Outside"), Is.EqualTo("-5.25C"));
			Assert.That(CrystalJson.Deserialize<WeatherDto>("""{ "Outside": "-5.25C" }""").Outside.Celsius, Is.EqualTo(-5.25));

			// and inside a collection
			Assert.That(CrystalJson.Serialize(new[] { new Temperature(1), new Temperature(2) }), Is.EqualTo("[ \"1C\", \"2C\" ]"));
		}

		/// <summary>CrystalJson-only converter attached with the NATIVE attribute (the STJ spelling would poison the type for STJ)</summary>
		public sealed class NativeFlagsDto
		{

			[JsonConvertWith(typeof(BoolAsBitStringConverter))]
			public bool Enabled { get; set; }

			// both spellings, naming DIFFERENT converters: the native attribute must win
			[JsonConvertWith(typeof(BoolAsBitStringConverter))]
			[STJ.JsonConverter(typeof(STJ.JsonStringEnumConverter))]
			public bool Mixed { get; set; }

			[JsonConvertWith(typeof(BoolAsBitStringConverter))]
			public bool? Optional { get; set; }

		}

		[JsonConvertWith(typeof(NativeTemperatureConverter))]
		public readonly record struct NativeTemperature(double Celsius);

		public sealed class NativeTemperatureConverter : IJsonMemberConverter<NativeTemperature>
		{
			public JsonValue Pack(ref CrystalJsonPackContext context, NativeTemperature instance)
				=> JsonString.Return(instance.Celsius.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "C");

			public NativeTemperature Unpack(JsonValue value, ICrystalJsonTypeResolver? resolver)
				=> value switch
				{
					JsonString s when s.Value.EndsWith("C") => new(double.Parse(s.Value.Substring(0, s.Value.Length - 1), System.Globalization.CultureInfo.InvariantCulture)),
					JsonNumber n => new(n.ToDouble()),
					_ => throw new JsonBindingException($"Cannot convert {value.Type} into a NativeTemperature")
				};
		}

		public sealed class BrokenConverterDto
		{
			// names a type that does NOT have the Pack/Unpack pair: the native attribute fails loudly (no legacy meaning to preserve)
			[JsonConvertWith(typeof(string))]
			public bool Broken { get; set; }
		}

		[Test]
		public void Test_Native_Attribute_On_Members_And_Types()
		{
			// member-level, both directions and both output routes
			var dto = new NativeFlagsDto { Enabled = true, Mixed = true, Optional = false };
			var obj = CrystalJson.Parse(CrystalJson.Serialize(dto)).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj.Get<string>("Enabled"), Is.EqualTo("1"), "[JsonConvertWith] must shape the output");
				Assert.That(obj.Get<string>("Mixed"), Is.EqualTo("1"), "the native attribute must win over a foreign spelling on the same member");
				Assert.That(obj.Get<string>("Optional"), Is.EqualTo("0"), "a converter for T lifts over a T? member");
			}
			Assert.That(JsonValue.FromValue(dto).AsObject().Get<string>("Enabled"), Is.EqualTo("1"), "the DOM route applies it as well");

			var back = CrystalJson.Deserialize<NativeFlagsDto>("""{ "Enabled": "1", "Mixed": false, "Optional": "0" }""");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(back.Enabled, Is.True);
				Assert.That(back.Mixed, Is.False);
				Assert.That(back.Optional, Is.False);
			}

			// type-level: standalone value, and hosted in a collection
			Assert.That(CrystalJson.Serialize(new NativeTemperature(23.5)), Is.EqualTo("\"23.5C\""));
			Assert.That(CrystalJson.Deserialize<NativeTemperature>("\"23.5C\"").Celsius, Is.EqualTo(23.5));
			Assert.That(CrystalJson.Serialize(new[] { new NativeTemperature(1) }), Is.EqualTo("[ \"1C\" ]"));
		}

		/// <summary>Asymmetric converter: this type is only ever written, so the converter only implements the packing facet</summary>
		public sealed class BitStringPackOnlyConverter : IJsonPacker<bool>
		{
			public JsonValue Pack(ref CrystalJsonPackContext context, bool instance)
				=> JsonString.Return(instance ? "1" : "0");
		}

		/// <summary>Asymmetric converter: this type is only ever read, so the converter only implements the deserializing facet</summary>
		public sealed class BitStringUnpackOnlyConverter : IJsonDeserializer<bool>
		{
			public bool Unpack(JsonValue value, ICrystalJsonTypeResolver? resolver)
				=> value switch
				{
					JsonString s => s.Value is "1",
					JsonBoolean b => b.ToBoolean(),
					_ => throw new JsonBindingException($"Cannot convert {value.Type} into a bit-string boolean")
				};
		}

		public sealed class PackOnlyDto
		{
			[JsonConvertWith(typeof(BitStringPackOnlyConverter))]
			public bool Flag { get; set; }
		}

		public sealed class UnpackOnlyDto
		{
			[JsonConvertWith(typeof(BitStringUnpackOnlyConverter))]
			public bool Flag { get; set; }
		}

		[Test]
		public void Test_Asymmetric_Converter_PackOnly()
		{
			// the present facet works normally...
			Assert.That(CrystalJson.Serialize(new PackOnlyDto { Flag = true }), Does.Contain("\"1\""));

			// ... a member that never reaches the converter is unaffected ...
			Assert.That(CrystalJson.Deserialize<PackOnlyDto>("{ }").Flag, Is.False, "an absent member never invokes the converter");

			// ... and any attempt to USE the missing facet fails loudly, with a message that teaches
			Assert.That(
				() => CrystalJson.Deserialize<PackOnlyDto>("""{ "Flag": "1" }"""),
				Throws.Exception.With.Message.Contain(nameof(BitStringPackOnlyConverter))
					.And.Message.Contain("IJsonDeserializer").And.Message.Contain("Unpack"),
				"the missing deserializing facet must fail loudly and name what to implement");
		}

		[Test]
		public void Test_Asymmetric_Converter_UnpackOnly()
		{
			// the present facet works normally...
			Assert.That(CrystalJson.Deserialize<UnpackOnlyDto>("""{ "Flag": "1" }""").Flag, Is.True);

			// ... and any attempt to USE the missing facet fails loudly, with a message that teaches
			Assert.That(
				() => CrystalJson.Serialize(new UnpackOnlyDto { Flag = true }),
				Throws.Exception.With.Message.Contain(nameof(BitStringUnpackOnlyConverter))
					.And.Message.Contain("IJsonPacker").And.Message.Contain("Pack"),
				"the missing packing facet must fail loudly and name what to implement");
		}

		[Test]
		public void Test_Native_Attribute_Fails_Loudly_On_Invalid_Converter()
		{
			// unlike the foreign spellings (ignored for compat), our own attribute naming a type without the
			// Pack/Unpack pair is a configuration bug and must not be silently dropped
			Assert.That(
				() => CrystalJson.Serialize(new BrokenConverterDto { Broken = true }),
				Throws.InstanceOf<InvalidOperationException>().With.Message.Contain("JsonConvertWith"));
		}

		[Test]
		public void Test_Foreign_Converter_Type_Is_Ignored()
		{
			// a [JsonConverter] naming a type that is not a CrystalJson converter (here, a real STJ converter)
			// cannot be executed: the attribute is ignored and the member serializes under the default rules,
			// which is what already happened before member converters existed (no behavior change for those sites)
			var obj = CrystalJson.Parse(CrystalJson.Serialize(new ForeignConverterDto { Day = DayOfWeek.Friday })).AsObject();
			Assert.That(obj.Get<string>("Day"), Is.EqualTo("Friday"), "the foreign converter is ignored, the enum keeps the default form");
		}

	}

}
