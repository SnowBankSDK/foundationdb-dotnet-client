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

	/// <summary>Probes that pin the exact behavior of the REFLECTION path when consuming legacy DataContract DTOs (DCJS migration scenarios)</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	[SetInvariantCulture]
	public sealed class CrystalJsonDataContractCompatFacts : SimpleTest
	{

		#region Test DTOs...

		[DataContract]
		public sealed class LegacyOrderDto
		{

			[DataMember(Name = "order_id")]
			public string? OrderId { get; set; }

			[DataMember(Order = 2, IsRequired = true)]
			public string? Customer { get; set; }

			[DataMember(EmitDefaultValue = false)]
			public int Quantity { get; set; }

			// no [DataMember]: must be excluded on a [DataContract] type
			public string? NotAMember { get; set; }

			// private member with [DataMember]: DCJS serializes it, CrystalJson reflection only sees public members
			[DataMember]
			private string? Secret { get; set; }

			[DataMember]
			[System.Text.Json.Serialization.JsonIgnore]
			public string? Both { get; set; }

			public void SetSecret(string value) => this.Secret = value;

		}

		public sealed class DateDto
		{
			public DateTime When { get; set; }

			public DateTimeOffset At { get; set; }
		}

		public sealed class EnumDto
		{
			public DayOfWeek Plain { get; set; }

			[JsonProperty(EnumFormat = JsonEnumFormat.String)]
			public DayOfWeek Stringy { get; set; }
		}

		public sealed class DictDto
		{
			public Dictionary<string, int>? Counts { get; set; }

			public Dictionary<int, string>? Names { get; set; }
		}

		public sealed class LifecycleDto
		{

			public LifecycleDto()
			{
				this.CtorRan = true;
			}

			public string? Name { get; set; }

			public bool CtorRan { get; }

			public bool CallbackRan { get; private set; }

			[OnDeserialized]
			private void OnDeserializedCallback(StreamingContext ctx) => this.CallbackRan = true;

		}

		public sealed class ModernDto
		{

			[System.Text.Json.Serialization.JsonPropertyName("renamed")]
			public string? Original { get; set; }

			[System.Text.Json.Serialization.JsonIgnore]
			public string? Hidden { get; set; }

			public int Kept { get; set; }

		}

		[System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "__type")]
		[System.Text.Json.Serialization.JsonDerivedType(typeof(LegacyShapeCircle), "Circle:Acme.Contracts")]
		[System.Text.Json.Serialization.JsonDerivedType(typeof(LegacyShapeSquare), "Square:Acme.Contracts")]
		public abstract class LegacyShape
		{
			public string? Label { get; set; }
		}

		public sealed class LegacyShapeCircle : LegacyShape
		{
			public double Radius { get; set; }
		}

		public sealed class LegacyShapeSquare : LegacyShape
		{
			public double Side { get; set; }
		}

		public sealed class RequiredDto
		{
			public required string Name { get; set; }

			public int Age { get; set; }
		}

		#endregion

		[Test]
		public void Test_DataContract_Member_Selection_And_Naming()
		{
			var dto = new LegacyOrderDto { OrderId = "X1", Customer = "acme", Quantity = 0, NotAMember = "nope", Both = "b" };
			dto.SetSecret("hidden");

			var obj = CrystalJson.Parse(CrystalJson.Serialize(dto)).AsObject();

			using (Assert.EnterMultipleScope())
			{
				// [DataMember(Name=...)] rename is honored
				Assert.That(obj.Get<string>("order_id"), Is.EqualTo("X1"));
				Assert.That(obj.ContainsKey("OrderId"), Is.False, "member must be emitted under its DataMember name");

				// opt-in: public member without [DataMember] is excluded
				Assert.That(obj.ContainsKey("NotAMember"), Is.False, "non-[DataMember] public member must be excluded on a [DataContract] type");

				// private [DataMember] is NOT serialized by the reflection path (public members only)
				Assert.That(obj.ContainsKey("Secret"), Is.False, "private members are never seen by the reflection path");

				// EmitDefaultValue=false is not honored: default int is still emitted
				Assert.That(obj.ContainsKey("Quantity"), Is.True, "EmitDefaultValue=false is ignored: value-type default is emitted");
				Assert.That(obj.Get<int>("Quantity"), Is.Zero);

				// on a [DataContract] type, [JsonIgnore] is not consulted (the [DataMember] opt-in short-circuits)
				Assert.That(obj.Get<string>("Both"), Is.EqualTo("b"), "[JsonIgnore] is ignored on members of a [DataContract] type");

				// Order=2 does not reorder: first emitted member is still the first declared one
				Assert.That(obj.Keys.First(), Is.EqualTo("order_id"), "DataMember.Order is ignored (declaration order wins)");
			}
		}

		[Test]
		public void Test_DataContract_IsRequired_Is_Not_Enforced()
		{
			// DCJS would throw on a missing IsRequired=true member; CrystalJson reflection does not
			var dto = CrystalJson.Deserialize<LegacyOrderDto>("""{ "order_id": "X1" }""");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(dto.OrderId, Is.EqualTo("X1"));
				Assert.That(dto.Customer, Is.Null, "missing IsRequired member binds to null without throwing");
			}
		}

		[Test]
		public void Test_Microsoft_Dates_Are_Read_By_Default()
		{
			// 1234567890000 ms since epoch == 2009-02-13T23:31:30Z
			var dto = CrystalJson.Deserialize<DateDto>("""{ "When": "\/Date(1234567890000)\/", "At": "\/Date(1234567890000)\/" }""");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(dto.When.ToUniversalTime(), Is.EqualTo(new DateTime(2009, 2, 13, 23, 31, 30, DateTimeKind.Utc)));
				Assert.That(dto.At.UtcDateTime, Is.EqualTo(new DateTime(2009, 2, 13, 23, 31, 30, DateTimeKind.Utc)));
			}
		}

		[Test]
		public void Test_Microsoft_Dates_With_Offset_Suffix_Preserve_The_Instant()
		{
			// In the "/Date(ms+HHMM)/" form the milliseconds are ALWAYS the UTC epoch offset; the suffix only carries
			// the producer's local offset for display. Reading it back must preserve the instant.
			// 1234567890000 ms since epoch == 2009-02-13T23:31:30Z, regardless of the "+0200" suffix.
			var dto = CrystalJson.Deserialize<DateDto>("""{ "When": "\/Date(1234567890000+0200)\/", "At": "\/Date(1234567890000+0200)\/" }""");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(dto.At.UtcDateTime, Is.EqualTo(new DateTime(2009, 2, 13, 23, 31, 30, DateTimeKind.Utc)),
					"the epoch milliseconds must not be shifted through the machine's local offset");
				Assert.That(dto.At.Offset, Is.EqualTo(TimeSpan.FromHours(2)), "the producer's offset must be preserved on DateTimeOffset");
				Assert.That(dto.When.ToUniversalTime(), Is.EqualTo(new DateTime(2009, 2, 13, 23, 31, 30, DateTimeKind.Utc)));
			}
		}

		[Test]
		public void Test_Microsoft_Dates_Emission_Is_OptIn()
		{
			var dto = new DateDto { When = new DateTime(2009, 2, 13, 23, 31, 30, DateTimeKind.Utc), At = new DateTimeOffset(2009, 2, 13, 23, 31, 30, TimeSpan.Zero) };

			var iso = CrystalJson.Serialize(dto);
			Assert.That(iso, Does.Not.Contain("/Date("), "default emission is ISO 8601");

			var ms = CrystalJson.Serialize(dto, CrystalJsonSettings.Json.WithMicrosoftDates());
			Assert.That(ms, Does.Contain(@"\/Date(1234567890000)\/"), "WithMicrosoftDates() must emit the legacy format");
		}

		[Test]
		public void Test_Enums_Default_Numeric_With_PerMember_Override()
		{
			var obj = CrystalJson.Parse(CrystalJson.Serialize(new EnumDto { Plain = DayOfWeek.Friday, Stringy = DayOfWeek.Friday })).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj["Plain"], Is.InstanceOf<JsonNumber>(), "enums are numeric by default (same as DCJS)");
				Assert.That(obj.Get<int>("Plain"), Is.EqualTo((int) DayOfWeek.Friday));
				Assert.That(obj["Stringy"], Is.InstanceOf<JsonString>(), "[JsonProperty(EnumFormat=String)] must override per member");
			}

			// global override
			var obj2 = CrystalJson.Parse(CrystalJson.Serialize(new EnumDto { Plain = DayOfWeek.Friday }, CrystalJsonSettings.Json.WithEnumAsStrings())).AsObject();
			Assert.That(obj2["Plain"], Is.InstanceOf<JsonString>());

			// both representations bind on the way in
			var back = CrystalJson.Deserialize<EnumDto>("""{ "Plain": 5, "Stringy": "Friday" }""");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(back.Plain, Is.EqualTo(DayOfWeek.Friday));
				Assert.That(back.Stringy, Is.EqualTo(DayOfWeek.Friday));
			}
		}

		[Test]
		public void Test_Dictionaries_Are_Object_Maps()
		{
			var dto = new DictDto
			{
				Counts = new() { ["a"] = 1, ["b"] = 2 },
				Names = new() { [1] = "one", [2] = "two" },
			};

			var obj = CrystalJson.Parse(CrystalJson.Serialize(dto)).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj["Counts"], Is.InstanceOf<JsonObject>(), "dictionaries are emitted as JSON object maps, not KVP arrays");
				Assert.That(obj.GetPathValue("Counts.a").As<int>(), Is.EqualTo(1));
				Assert.That(obj["Names"], Is.InstanceOf<JsonObject>(), "non-string keys are stringified");
				Assert.That(obj.GetObject("Names").Get<string>("1"), Is.EqualTo("one"));
			}

			// round-trip
			var back = CrystalJson.Deserialize<DictDto>(CrystalJson.Serialize(dto));
			using (Assert.EnterMultipleScope())
			{
				Assert.That(back.Counts, Is.EqualTo(dto.Counts));
				Assert.That(back.Names, Is.EqualTo(dto.Names));
			}

			// the DCJS shape (array of {"Key":..,"Value":..}) is NOT readable into a dictionary
			Assert.That(
				() => CrystalJson.Deserialize<Dictionary<string, int>>("""[ { "Key": "a", "Value": 1 } ]"""),
				Throws.InstanceOf<JsonBindingException>(),
				"the legacy DCJS dictionary wire shape has no built-in reader");

			// a standalone KeyValuePair<K,V> serializes as a 2-element array [key, value]
			Assert.That(CrystalJson.Serialize(new KeyValuePair<int, string>(1, "one"), CrystalJsonSettings.JsonCompact), Is.EqualTo("""[1,"one"]"""));
		}

		[Test]
		public void Test_Deserialize_Invokes_Parameterless_Ctor_But_No_Callbacks()
		{
			// the input actively lies on the read-only members: they must keep the values the ctor produced
			var dto = CrystalJson.Deserialize<LifecycleDto>("""{ "Name": "x", "CtorRan": false, "CallbackRan": true }""");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(dto.Name, Is.EqualTo("x"));
				Assert.That(dto.CtorRan, Is.True, "deserialization must construct via the parameterless ctor (not an uninitialized object)");
				Assert.That(dto.CallbackRan, Is.False, "[OnDeserialized] must NOT be invoked by CrystalJson");
			}
		}

		[Test]
		public void Test_SystemTextJson_Attributes_On_Plain_Type()
		{
			var obj = CrystalJson.Parse(CrystalJson.Serialize(new ModernDto { Original = "v", Hidden = "h", Kept = 1 })).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj.Get<string>("renamed"), Is.EqualTo("v"), "[JsonPropertyName] rename is honored");
				Assert.That(obj.ContainsKey("Original"), Is.False);
				Assert.That(obj.ContainsKey("Hidden"), Is.False, "[JsonIgnore] excludes the member on a non-DataContract type");
				Assert.That(obj.Get<int>("Kept"), Is.EqualTo(1));
			}

			var back = CrystalJson.Deserialize<ModernDto>("""{ "renamed": "v2", "Kept": 2 }""");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(back.Original, Is.EqualTo("v2"));
				Assert.That(back.Kept, Is.EqualTo(2));
			}
		}

		[Test]
		public void Test_Polymorphism_With_Legacy_Discriminator()
		{
			LegacyShape shape = new LegacyShapeCircle { Label = "c1", Radius = 2.5 };

			var json = CrystalJson.Serialize(shape);
			var obj = CrystalJson.Parse(json).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj.Get<string>("__type"), Is.EqualTo("Circle:Acme.Contracts"), "the discriminator property name and value are fully configurable");
				Assert.That(obj.Get<double>("Radius"), Is.EqualTo(2.5));
			}

			var back = CrystalJson.Deserialize<LegacyShape>(json);
			Assert.That(back, Is.InstanceOf<LegacyShapeCircle>());
			Assert.That(((LegacyShapeCircle) back).Radius, Is.EqualTo(2.5));
		}

		[Test]
		public void Test_Required_Keyword_Is_Not_Enforced_By_Reflection()
		{
			// the source-generated path throws on a missing `required` member; the reflection path does not (divergence to keep in mind)
			var dto = CrystalJson.Deserialize<RequiredDto>("""{ "Age": 5 }""");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(dto.Age, Is.EqualTo(5));
				Assert.That(dto.Name, Is.Null, "missing `required` member binds to null without throwing on the reflection path");
			}
		}

	}

}
