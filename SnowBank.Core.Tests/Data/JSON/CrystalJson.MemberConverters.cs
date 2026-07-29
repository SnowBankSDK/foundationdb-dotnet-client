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

		/// <summary>Legacy-style scalar converter: bool on the wire as "0"/"1" strings (tolerant read)</summary>
		public sealed class BoolAsBitStringConverter : IJsonMemberConverter<bool>
		{

			public JsonValue Pack(bool instance, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
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

			[Newtonsoft.Json.JsonConverter(typeof(BoolAsBitStringConverter))]
			public bool Archived { get; set; }

			[STJ.JsonConverter(typeof(BoolAsBitStringConverter))]
			public bool? Optional { get; set; }

			public bool Plain { get; set; }

		}

		/// <summary>Type-level converter target: the whole struct has a custom scalar wire form</summary>
		[STJ.JsonConverter(typeof(TemperatureConverter))]
		public readonly record struct Temperature(double Celsius);

		public sealed class TemperatureConverter : IJsonMemberConverter<Temperature>
		{

			public JsonValue Pack(Temperature instance, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
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
		public void Test_Member_Converter_On_Text_Route()
		{
			var dto = new LegacyFlagsDto { Enabled = true, Archived = false, Optional = true, Plain = true };
			var obj = CrystalJson.Parse(CrystalJson.Serialize(dto)).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj["Enabled"], Is.InstanceOf<JsonString>(), "the member converter must shape the wire (STJ spelling)");
				Assert.That(obj.Get<string>("Enabled"), Is.EqualTo("1"));
				Assert.That(obj["Archived"], Is.InstanceOf<JsonString>(), "the member converter must shape the wire (Newtonsoft spelling)");
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
