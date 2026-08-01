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

			// private member with [DataMember]: serialized, like DCJS (the DataContract model is accessibility-blind)
			[DataMember]
			private string? Secret { get; set; }

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

		/// <summary>Every shape of the hybrid non-public rule on a [DataContract] type: private property, private field, non-public accessor, and the [JsonInclude]-only non-member</summary>
		[DataContract]
		public sealed class HybridRuleDto
		{

			[DataMember]
			private string? PrivateProp { get; set; }

			[DataMember(Name = "renamed_field")]
			private int PrivateField;

			// public [DataMember] property whose SETTER is non-public: the accessor unlocks automatically too
			[DataMember]
			public string? Locked { get; private set; }

			// [JsonInclude] alone grants nothing here: on a [DataContract] type, membership requires [DataMember]
			[System.Text.Json.Serialization.JsonInclude]
			private string? IncludeOnly { get; set; }

			public void Init(string privateProp, int privateField, string locked, string includeOnly)
			{
				this.PrivateProp = privateProp;
				this.PrivateField = privateField;
				this.Locked = locked;
				this.IncludeOnly = includeOnly;
			}

			public (string? PrivateProp, int PrivateField, string? IncludeOnly) Expose() => (this.PrivateProp, this.PrivateField, this.IncludeOnly);

		}

		/// <summary>POCO (no [DataContract]) with a private [DataMember]: the attribute means nothing there, under DCJS and under CrystalJson alike</summary>
		public sealed class PocoPrivateDto
		{

			[DataMember]
			private string? NotOptedIn { get; set; }

			public int Plain { get; set; }

			public void Init(string value) => this.NotOptedIn = value;

			public string? Expose() => this.NotOptedIn;

		}

		/// <summary>One DTO exercising all five wire divergences the legacy preset must cover at once</summary>
		public sealed class LegacyWireDto
		{
			public DayOfWeek Kind { get; set; }

			public DateTime When { get; set; }

			public TimeSpan Elapsed { get; set; }

			public Dictionary<string, int>? Counts { get; set; }

			public string? MaybeNull { get; set; }
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

		/// <summary>The same shape with the modern callback signature, which is the one this serializer accepts</summary>
		public sealed class ModernLifecycleDto
		{

			public ModernLifecycleDto()
			{
				this.CtorRan = true;
			}

			public string? Name { get; set; }

			public bool CtorRan { get; }

			public bool CallbackRan { get; private set; }

			[OnDeserialized]
			private void OnDeserializedCallback() => this.CallbackRan = true;

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
			var dto = new LegacyOrderDto { OrderId = "X1", Customer = "acme", Quantity = 0, NotAMember = "nope" };
			dto.SetSecret("hidden");

			var obj = CrystalJson.Parse(CrystalJson.Serialize(dto)).AsObject();

			using (Assert.EnterMultipleScope())
			{
				// [DataMember(Name=...)] rename is honored
				Assert.That(obj.Get<string>("order_id"), Is.EqualTo("X1"));
				Assert.That(obj.ContainsKey("OrderId"), Is.False, "member must be emitted under its DataMember name");

				// opt-in: public member without [DataMember] is excluded
				Assert.That(obj.ContainsKey("NotAMember"), Is.False, "non-[DataMember] public member must be excluded on a [DataContract] type");

				// hybrid rule: on a [DataContract] type the attribute pair IS the explicit opt-in, and the
				// DataContract model is accessibility-blind, so a private [DataMember] serializes automatically
				Assert.That(obj.Get<string>("Secret"), Is.EqualTo("hidden"), "a non-public [DataMember] serializes automatically on a [DataContract] type");

				// EmitDefaultValue=false is not honored: default int is still emitted
				Assert.That(obj.ContainsKey("Quantity"), Is.True, "EmitDefaultValue=false is ignored: value-type default is emitted");
				Assert.That(obj.Get<int>("Quantity"), Is.Zero);

				// Order=2 does not reorder: first emitted member is still the first declared one
				Assert.That(obj.Keys.First(), Is.EqualTo("order_id"), "DataMember.Order is ignored (declaration order wins)");
			}
		}

		[Test]
		public void Test_NonPublic_DataMember_Is_Automatic_On_DataContract_Types()
		{
			var dto = new HybridRuleDto();
			dto.Init("p", 42, "locked", "io");

			var obj = CrystalJson.Parse(CrystalJson.Serialize(dto)).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj.Get<string>("PrivateProp"), Is.EqualTo("p"), "private [DataMember] property serializes automatically");
				Assert.That(obj.Get<int>("renamed_field", -1), Is.EqualTo(42), "private [DataMember] field serializes automatically, under its DataMember name");
				Assert.That(obj.Get<string>("Locked"), Is.EqualTo("locked"));
				Assert.That(obj.ContainsKey("IncludeOnly"), Is.False, "[JsonInclude] alone does not grant membership on a [DataContract] type");
			}

			var back = CrystalJson.Deserialize<HybridRuleDto>("""{ "PrivateProp": "x", "renamed_field": 7, "Locked": "y", "IncludeOnly": "z" }""");
			var (privateProp, privateField, includeOnly) = back.Expose();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(privateProp, Is.EqualTo("x"), "private [DataMember] property binds on read");
				Assert.That(privateField, Is.EqualTo(7), "private [DataMember] field binds on read");
				Assert.That(back.Locked, Is.EqualTo("y"), "the non-public setter of a public [DataMember] property is unlocked automatically");
				Assert.That(includeOnly, Is.Null, "[JsonInclude] alone must not bind on a [DataContract] type");
			}
		}

		[Test]
		public void Test_Private_DataMember_On_Poco_Stays_Excluded()
		{
			// DCJS parity: without [DataContract], [DataMember] is inert, and non-public members stay invisible
			// unless STJ's [JsonInclude] opts them in
			var dto = new PocoPrivateDto { Plain = 1 };
			dto.Init("hidden");

			var obj = CrystalJson.Parse(CrystalJson.Serialize(dto)).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj.ContainsKey("NotOptedIn"), Is.False, "a private [DataMember] on a POCO stays excluded");
				Assert.That(obj.Get<int>("Plain", -1), Is.EqualTo(1));
			}

			var back = CrystalJson.Deserialize<PocoPrivateDto>("""{ "NotOptedIn": "v", "Plain": 2 }""");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(back.Expose(), Is.Null, "a private [DataMember] on a POCO must not bind");
				Assert.That(back.Plain, Is.EqualTo(2));
			}
		}

		[Test]
		public void Test_DataContract_IsRequired_Is_Enforced_On_Read()
		{
			// DCJS-faithful semantics: a [DataMember(IsRequired=true)] member ABSENT from the document throws,
			// while an explicit null SATISFIES it (deliberately distinct from the C# `required` keyword, where
			// null-or-missing both throw)
			Assert.That(
				() => CrystalJson.Deserialize<LegacyOrderDto>("""{ "order_id": "X1" }"""),
				Throws.InstanceOf<JsonBindingException>().With.Message.Contains("Customer"),
				"a missing IsRequired member must throw on read");

			var dto = CrystalJson.Deserialize<LegacyOrderDto>("""{ "order_id": "X1", "Customer": null }""");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(dto.OrderId, Is.EqualTo("X1"));
				Assert.That(dto.Customer, Is.Null, "an explicit null satisfies IsRequired, as it did under DCJS");
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
		public void Test_Enums_Default_String_With_PerMember_Override()
		{
			var obj = CrystalJson.Parse(CrystalJson.Serialize(new EnumDto { Plain = DayOfWeek.Friday, Stringy = DayOfWeek.Friday })).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj["Plain"], Is.InstanceOf<JsonString>(), "enums are string literals by default");
				Assert.That(obj.Get<string>("Plain"), Is.EqualTo("Friday"));
				Assert.That(obj["Stringy"], Is.InstanceOf<JsonString>(), "[JsonProperty(EnumFormat=String)] forces the string form per member");
			}

			// DCJS emitted numbers: byte-parity with a DCJS producer now requires the numeric opt-in
			var obj2 = CrystalJson.Parse(CrystalJson.Serialize(new EnumDto { Plain = DayOfWeek.Friday, Stringy = DayOfWeek.Friday }, CrystalJsonSettings.Json.WithEnumAsNumbers())).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj2["Plain"], Is.InstanceOf<JsonNumber>(), "WithEnumAsNumbers() restores the DCJS wire");
				Assert.That(obj2["Stringy"], Is.InstanceOf<JsonString>(), "the per-member EnumFormat=String override wins over the numeric setting");
			}

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

			// a standalone KeyValuePair<K,V> serializes as a 2-element array [key, value]
			Assert.That(CrystalJson.Serialize(new KeyValuePair<int, string>(1, "one"), CrystalJsonSettings.JsonCompact), Is.EqualTo("""[1,"one"]"""));
		}

		[Test]
		public void Test_Dictionaries_Tolerate_The_Legacy_KeyValuePair_Array_Shape_On_Read()
		{
			// DCJS serializes IDictionary<K,V> as an array of {"Key":..,"Value":..} objects; reading that shape
			// into a dictionary is tolerated by default (the object-map fast path is unaffected)
			var counts = CrystalJson.Deserialize<Dictionary<string, int>>("""[ { "Key": "a", "Value": 1 }, { "Key": "b", "Value": 2 } ]""");
			Assert.That(counts, Is.EqualTo(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }));

			var names = CrystalJson.Deserialize<Dictionary<int, string>>("""[ { "Key": 1, "Value": "one" } ]""");
			Assert.That(names, Is.EqualTo(new Dictionary<int, string> { [1] = "one" }));

			// nested inside a DTO member
			var dto = CrystalJson.Deserialize<DictDto>("""{ "Counts": [ { "Key": "x", "Value": 9 } ] }""");
			Assert.That(dto.Counts, Is.EqualTo(new Dictionary<string, int> { ["x"] = 9 }));

			// an empty array is a valid (empty) dictionary (DCJS emits [] for an empty dictionary)
			Assert.That(CrystalJson.Deserialize<Dictionary<string, int>>("[]"), Is.Empty);
		}

		[Test]
		public void Test_Dictionaries_Emit_The_Legacy_Pair_Array_Shape_On_Demand()
		{
			var dto = new DictDto
			{
				Counts = new() { ["a"] = 1, ["b"] = 2 },
				Names = new() { [1] = "one" },
			};
			var settings = CrystalJsonSettings.JsonCompact.WithDictionariesAsPairArrays();

			var obj = CrystalJson.Parse(CrystalJson.Serialize(dto, settings)).AsObject();
			using (Assert.EnterMultipleScope())
			{
				var counts = obj.GetArray("Counts");
				Assert.That(counts.Count, Is.EqualTo(2), "the dictionary must be emitted as a pair array");
				Assert.That(counts[0].AsObject().Get<string>("Key"), Is.EqualTo("a"));
				Assert.That(counts[0].AsObject().Get<int>("Value"), Is.EqualTo(1));
				Assert.That(counts[1].AsObject().Get<string>("Key"), Is.EqualTo("b"));

				var names = obj.GetArray("Names");
				Assert.That(names[0].AsObject().Get<int>("Key"), Is.EqualTo(1), "non-string keys are emitted with their natural JSON type");
				Assert.That(names[0].AsObject().Get<string>("Value"), Is.EqualTo("one"));
			}

			// top-level dictionaries, exact wire shape, and the empty case (DCJS emits [])
			Assert.That(CrystalJson.Serialize(new Dictionary<string, int> { ["a"] = 1 }, settings), Is.EqualTo("""[{"Key":"a","Value":1}]"""));
			Assert.That(CrystalJson.Serialize(new Dictionary<string, int>(), settings), Is.EqualTo("[]"));

			// the emitted shape reads back through the default read-side tolerance
			var back = CrystalJson.Deserialize<DictDto>(CrystalJson.Serialize(dto, settings));
			using (Assert.EnterMultipleScope())
			{
				Assert.That(back.Counts, Is.EqualTo(dto.Counts));
				Assert.That(back.Names, Is.EqualTo(dto.Names));
			}

			// the setting is opt-in: default settings still emit object maps
			Assert.That(CrystalJson.Parse(CrystalJson.Serialize(dto)).AsObject()["Counts"], Is.InstanceOf<JsonObject>());
		}

		[Test]
		public void Test_Legacy_KeyValuePair_Array_Shape_Is_Strict()
		{
			// every element must be an object with exactly the two members "Key" and "Value": anything else fails
			using (Assert.EnterMultipleScope())
			{
				Assert.That(
					() => CrystalJson.Deserialize<Dictionary<string, int>>("""[ { "Key": "a" } ]"""),
					Throws.InstanceOf<JsonBindingException>(), "missing Value must fail");
				Assert.That(
					() => CrystalJson.Deserialize<Dictionary<string, int>>("""[ { "key": "a", "value": 1 } ]"""),
					Throws.InstanceOf<JsonBindingException>(), "wrong member casing must fail");
				Assert.That(
					() => CrystalJson.Deserialize<Dictionary<string, int>>("""[ { "Key": "a", "Value": 1, "Extra": 2 } ]"""),
					Throws.InstanceOf<JsonBindingException>(), "extra members must fail");
				Assert.That(
					() => CrystalJson.Deserialize<Dictionary<string, int>>("""[ 1, 2 ]"""),
					Throws.InstanceOf<JsonBindingException>(), "non-object elements must fail");
				Assert.That(
					() => CrystalJson.Deserialize<Dictionary<string, int>>("""[ { "Key": "a", "Value": 1 }, [ "b", 2 ] ]"""),
					Throws.InstanceOf<JsonBindingException>(), "one non-conforming element must fail the whole bind");
			}
		}

		[Test]
		public void Test_Deserialize_Invokes_Parameterless_Ctor_And_The_Callback()
		{
			// the input actively lies on the read-only members: they must keep the values the ctor and callback produced
			var dto = CrystalJson.Deserialize<ModernLifecycleDto>("""{ "Name": "x", "CtorRan": false, "CallbackRan": false }""");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(dto.Name, Is.EqualTo("x"));
				Assert.That(dto.CtorRan, Is.True, "deserialization must construct via the parameterless ctor (not an uninitialized object)");
				Assert.That(dto.CallbackRan, Is.True, "[OnDeserialized] is invoked, after the members have been populated");
			}
		}

		[Test]
		public void Test_Legacy_StreamingContext_Callback_Signature_Is_Refused()
		{
			// the signature DCJS REQUIRES is the one we refuse: converting it costs the type its DCJS compatibility,
			// which is why the migration guide makes that a precondition of the sweep rather than a footnote
			var ex = Assert.Throws<JsonBindingException>(() => CrystalJson.Deserialize<LifecycleDto>("""{ "Name": "x" }"""));
			Assert.That(ex!.Message, Is.EqualTo(string.Format(CrystalJson.Errors.CallbackStreamingContextNotSupported, "OnDeserializedCallback")));
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
		public void Test_Required_Keyword_Is_Enforced_By_Reflection()
		{
			// both paths agree: a C# `required` member that is null OR missing throws on read (the
			// source-generated path always did; the reflection path now enforces the same contract)
			Assert.That(
				() => CrystalJson.Deserialize<RequiredDto>("""{ "Age": 5 }"""),
				Throws.InstanceOf<JsonBindingException>().With.Message.Contains("Required JSON field").And.Message.Contains("Name"),
				"a missing `required` member must throw on the reflection path");

			Assert.That(
				() => CrystalJson.Deserialize<RequiredDto>("""{ "Name": null, "Age": 5 }"""),
				Throws.InstanceOf<JsonBindingException>().With.Message.Contains("Name"),
				"an explicit null does not satisfy the C# `required` contract (unlike [DataMember(IsRequired=true)])");

			var dto = CrystalJson.Deserialize<RequiredDto>("""{ "Name": "n", "Age": 5 }""");
			Assert.That(dto.Name, Is.EqualTo("n"));
		}

		#region Double contract (conflicting wire names)...

		/// <summary>One member, two serializers, two different wire names: the "double contract" defect</summary>
		[DataContract]
		public sealed class DoubleContractDto
		{
			[DataMember(Name = "code")]
			[Newtonsoft.Json.JsonProperty("ACTIF")]
			public string? Code { get; set; }
		}

		[DataContract]
		public sealed class DoubleContractStjDto
		{
			[DataMember(Name = "code")]
			[System.Text.Json.Serialization.JsonPropertyName("CODE_X")]
			public string? Code { get; set; }
		}

		/// <summary>Both attributes agree on the wire name: a common belt-and-suspenders migration state, and legal</summary>
		[DataContract]
		public sealed class AgreeingNamesDto
		{
			[DataMember(Name = "code")]
			[System.Text.Json.Serialization.JsonPropertyName("code")]
			public string? Code { get; set; }
		}

		public sealed class NewtonsoftOnlyDto
		{
			[Newtonsoft.Json.JsonProperty("renamed")]
			public string? Value { get; set; }
		}

		/// <summary>The ignore variant of the double contract: on the DCJS wire via [DataMember], off the other wire via [JsonIgnore]</summary>
		[DataContract]
		public sealed class DualOutputDto
		{
			[DataMember]
			[System.Text.Json.Serialization.JsonIgnore]
			public string? Both { get; set; }

			[DataMember]
			public string? Plain { get; set; }
		}

		/// <summary>The Newtonsoft-pair flavour: a rename for one serializer, an exclusion for the other</summary>
		public sealed class NewtonsoftPairDto
		{
			[Newtonsoft.Json.JsonProperty("actif")]
			[System.Text.Json.Serialization.JsonIgnore]
			public string? Both { get; set; }
		}

		/// <summary>The STJ-only flavour: an explicit include next to an unconditional ignore</summary>
		public sealed class IncludeIgnorePairDto
		{
			[System.Text.Json.Serialization.JsonInclude]
			[System.Text.Json.Serialization.JsonIgnore]
			private string? Both { get; set; }

			public int Plain { get; set; }

			public void Init(string value) => this.Both = value;
		}

		/// <summary>The LEGAL neighbour: a Condition is a write rule, not an exclusion, so it is not a conflict</summary>
		[DataContract]
		public sealed class ConditionalPairDto
		{
			[DataMember(EmitDefaultValue = false)]
			[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
			public int Count { get; set; }

			[DataMember]
			public string? Name { get; set; }
		}

		[Test]
		public void Test_Conflicting_Wire_Names_Are_Refused_Loudly()
		{
			// a member carrying [DataMember(Name=x)] plus a foreign naming attribute with a DIFFERENT name is one
			// type trying to serve two wire contracts: refuse with an error naming the member, both attributes and
			// both names, instead of silently picking one (the fix on the application side is to split the DTO)

			Assert.That(
				() => CrystalJson.Serialize(new DoubleContractDto { Code = "c-1" }),
				Throws.Exception.With.Message.Contains("Code").And.Message.Contains("code").And.Message.Contains("ACTIF").And.Message.Contains("split"));

			Assert.That(
				() => CrystalJson.Deserialize<DoubleContractDto>("""{ "code": "c-1" }"""),
				Throws.Exception.With.Message.Contains("ACTIF"),
				"the same contract defect must also refuse the read direction");

			Assert.That(
				() => CrystalJson.Serialize(new DoubleContractStjDto { Code = "c-1" }),
				Throws.Exception.With.Message.Contains("CODE_X"),
				"a conflicting [JsonPropertyName] is the same defect with another serializer");
		}

		[Test]
		public void Test_Unconditional_JsonIgnore_Next_To_An_Include_Signal_Is_Refused_Loudly()
		{
			// the ignore variant of the double contract: a dual-output DTO is not supported, and the remedy the
			// message steers to is the SPLIT (one DTO per serializer), with "remove one of the two attributes" as
			// the secondary hint for the honest-mistake case - never "give the [JsonIgnore] a Condition", which
			// would flip the member to included-with-a-write-rule and ship it onto the second wire

			Assert.That(
				() => CrystalJson.Serialize(new DualOutputDto { Both = "b", Plain = "p" }),
				Throws.Exception.With.Message.Contains("Both")
					.And.Message.Contains("JsonIgnore")
					.And.Message.Contains("DataMember")
					.And.Message.Contains("split"));

			Assert.That(
				() => CrystalJson.Deserialize<DualOutputDto>("""{ "Plain": "p" }"""),
				Throws.Exception.With.Message.Contains("JsonIgnore"),
				"the read direction refuses too: the contract is built once, for both directions");

			Assert.That(
				() => CrystalJson.Serialize(new NewtonsoftPairDto { Both = "b" }),
				Throws.Exception.With.Message.Contains("JsonProperty"),
				"a Newtonsoft-style [JsonProperty] next to an unconditional [JsonIgnore] is the same defect");

			var stjPair = new IncludeIgnorePairDto { Plain = 1 };
			stjPair.Init("b");
			Assert.That(
				() => CrystalJson.Serialize(stjPair),
				Throws.Exception.With.Message.Contains("JsonInclude"),
				"[JsonInclude] next to an unconditional [JsonIgnore] is the same defect");
		}

		[Test]
		public void Test_Ignore_Conflict_Refusal_Never_Suggests_A_Condition()
		{
			var ex = Assert.Throws<JsonSerializationException>(() => CrystalJson.Serialize(new DualOutputDto { Both = "b" }))!;
			Log(ex.Message);
			Assert.That(ex.Message, Does.Not.Contain("Condition"),
				"suggesting a Condition would resolve the error while shipping the member onto the second wire for the first time");
		}

		[Test]
		public void Test_Conditional_JsonIgnore_Next_To_DataMember_Stays_Legal()
		{
			// the EmitDefaultValue recipe: the Condition records a write rule and falls through to the [DataMember] gate
			var json = CrystalJson.Serialize(new ConditionalPairDto { Count = 0, Name = "n" });
			var obj = CrystalJson.Parse(json).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj.ContainsKey("Count"), Is.False, "WhenWritingDefault omits the default value");
				Assert.That(obj.Get<string>("Name"), Is.EqualTo("n"));
			}
			Assert.That(CrystalJson.Parse(CrystalJson.Serialize(new ConditionalPairDto { Count = 3 })).AsObject().Get<int>("Count", -1), Is.EqualTo(3));
		}

		[Test]
		public void Test_Agreeing_Wire_Names_Are_Legal()
		{
			// both attributes giving the SAME name is reinforcement, not a conflict
			var json = CrystalJson.Serialize(new AgreeingNamesDto { Code = "c-1" });
			Assert.That(json, Is.EqualTo("""{ "code": "c-1" }"""));
			Assert.That(CrystalJson.Deserialize<AgreeingNamesDto>(json).Code, Is.EqualTo("c-1"));
		}

		[Test]
		public void Test_Foreign_Rename_Without_DataContract_Still_Works()
		{
			// a lone Newtonsoft [JsonProperty] rename (no [DataContract] in sight) keeps its recognized behavior
			var json = CrystalJson.Serialize(new NewtonsoftOnlyDto { Value = "v" });
			Assert.That(json, Is.EqualTo("""{ "renamed": "v" }"""));
			Assert.That(CrystalJson.Deserialize<NewtonsoftOnlyDto>(json).Value, Is.EqualTo("v"));
		}

		[Test]
		public void Test_DataContractCompat_Preset_Is_The_Five_Setting_Composition()
		{
			// settings are cached singletons: the named preset and the manual composition must be the SAME instance,
			// which is also the proof that the two are byte-equivalent on the wire
			var composed = CrystalJsonSettings.Json
				.WithEnumAsNumbers()
				.WithMicrosoftDates()
				.WithIso8601Durations()
				.WithDictionariesAsPairArrays()
				.WithNullMembers();
			Assert.That(CrystalJsonSettings.DataContractCompat, Is.SameAs(composed));

			using (Assert.EnterMultipleScope())
			{
				Assert.That(CrystalJsonSettings.DataContractCompat.EnumsAsString, Is.False);
				Assert.That(CrystalJsonSettings.DataContractCompat.DateFormatting, Is.EqualTo(CrystalJsonSettings.DateFormat.Microsoft));
				Assert.That(CrystalJsonSettings.DataContractCompat.Iso8601Durations, Is.True);
				Assert.That(CrystalJsonSettings.DataContractCompat.DictionariesAsPairArrays, Is.True);
				Assert.That(CrystalJsonSettings.DataContractCompat.ShowNullMembers, Is.True);
			}
		}

		[Test]
		public void Test_Iso8601_Durations_Emission_Is_OptIn()
		{
			var dto = new LegacyWireDto { Elapsed = new TimeSpan(1, 2, 3, 4, 5) };

			var obj = CrystalJson.Parse(CrystalJson.Serialize(dto)).AsObject();
			Assert.That(obj["Elapsed"], Is.InstanceOf<JsonNumber>(), "default emission is the number of seconds");

			var iso = CrystalJson.Serialize(dto, CrystalJsonSettings.Json.WithIso8601Durations());
			Assert.That(CrystalJson.Parse(iso).AsObject().Get<string>("Elapsed"), Is.EqualTo("P1DT2H3M4.005S"), "WithIso8601Durations() must emit the DCJS duration form");

			// the DOM route agrees with the text route
			var packed = JsonValue.FromValue(dto, CrystalJsonSettings.Json.WithIso8601Durations()).AsObject();
			Assert.That(packed.Get<string>("Elapsed"), Is.EqualTo("P1DT2H3M4.005S"), "the DOM route must honor the setting like the text route");

			// the duration form reads back without any setting (tolerant read, shipped with the primitives wave)
			var back = CrystalJson.Deserialize<LegacyWireDto>(iso);
			Assert.That(back.Elapsed, Is.EqualTo(dto.Elapsed));
		}

		[Test]
		public void Test_DataContractCompat_Preset_Produces_The_Legacy_Wire()
		{
			var dto = new LegacyWireDto
			{
				Kind = DayOfWeek.Friday,
				When = new DateTime(2009, 2, 13, 23, 31, 30, DateTimeKind.Utc),
				Elapsed = new TimeSpan(1, 2, 3, 4, 5),
				Counts = new() { ["a"] = 1 },
				MaybeNull = null,
			};

			var json = CrystalJson.Serialize(dto, CrystalJsonSettings.DataContractCompat);
			Log(json);
			var obj = CrystalJson.Parse(json).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj["Kind"], Is.InstanceOf<JsonNumber>(), "enums must be numbers on the legacy wire");
				Assert.That(obj.Get<int>("Kind"), Is.EqualTo(5));
				Assert.That(json, Does.Contain(@"\/Date(1234567890000)\/"), "dates must use the legacy Microsoft format");
				Assert.That(obj.Get<string>("Elapsed"), Is.EqualTo("P1DT2H3M4.005S"), "durations must use the ISO 8601 duration form on the legacy wire");
				Assert.That(obj["Counts"], Is.InstanceOf<JsonArray>(), "dictionaries must be pair arrays on the legacy wire");
				Assert.That(obj["Counts"][0]["Key"], IsJson.EqualTo("a"));
				Assert.That(obj["Counts"][0]["Value"], IsJson.EqualTo(1));
				Assert.That(obj.ContainsKey("MaybeNull"), Is.True, "null members must be emitted explicitly on the legacy wire");
			}

			// the default settings emit none of the four legacy forms
			var modern = CrystalJson.Serialize(dto);
			var modernObj = CrystalJson.Parse(modern).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(modernObj["Kind"], Is.InstanceOf<JsonString>());
				Assert.That(modern, Does.Not.Contain("/Date("));
				Assert.That(modernObj["Counts"], Is.InstanceOf<JsonObject>());
				Assert.That(modernObj.ContainsKey("MaybeNull"), Is.False);
			}
		}

		#endregion

	}

}
