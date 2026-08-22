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
	using System.Buffers;
	using System.Collections.ObjectModel;
	using System.ComponentModel.DataAnnotations;
	using System.Net;
	using System.Text.Json.Serialization;
	using SnowBank.Numerics;

	#region Types...

	public record Person
	{

		public string? FirstName { get; set; }

		public string? FamilyName { get; set; }

	}

	public sealed record LegacyDevice
	{

		public required string Serial { get; init; }

		public string? Model { get; init; }

	}

	/// <summary>User subclass of <see cref="KeyedCollection{TKey,TItem}"/>, keyed by a property of the items</summary>
	public sealed class LegacyDeviceIndex : KeyedCollection<string, LegacyDevice>
	{
		protected override string GetKeyForItem(LegacyDevice item) => item.Serial;
	}

	/// <summary>DCJS-era shape: legacy <see cref="Collection{T}"/> members instead of <see cref="List{T}"/></summary>
	public sealed record LegacyBag
	{

		public Collection<int>? Codes { get; init; }

		public ObservableCollection<string>? Tags { get; init; }

		public LegacyDeviceIndex? Devices { get; init; }

	}

	public enum MyAwesomeEnumType
	{
		Invalid = 0,
		User = 1,
		Administrator = 2,
		SecretAgent = 007,
		DoubleAgent = 8,
	}

	public sealed record MyAwesomeUser
	{

		/// <summary>User ID.</summary>
		[Key]
		public required string Id { get; init; }

		/// <summary>Full name, for display purpose</summary>
		public required string DisplayName { get; init; }

		/// <summary>Primary email for this account</summary>
		public required string Email { get; init; }

		[JsonProperty("type", DefaultValue = 7)]
		public MyAwesomeEnumType Type { get; init; }

		public string? Description { get; init; }

		public string[]? Roles { get; init; }

		public required MyAwesomeMetadata Metadata { get; init; }

		public List<MyAwesomeStruct>? Items { get; init; }

		public Dictionary<string, MyAwesomeDevice>? Devices { get; init; }

		public JsonObject? Extras { get; init; }

	}

	public sealed record MyAwesomeMetadata
	{
		/// <summary>Date at which this account was created</summary>
		public required DateTimeOffset AccountCreated { get; init; }

		/// <summary>Date at which this account was last modified</summary>
		public required DateTimeOffset AccountModified { get; init; }

		/// <summary>Date at which this account was deleted, or <see langword="null"/> if it is still active</summary>
		public DateTimeOffset? AccountDisabled { get; init; }
	}

	public record struct MyAwesomeStruct
	{

		/// <summary>Some fancy ID</summary>
		[Key]
		public required string Id { get; init; }

		/// <summary>Is it over 8000?</summary>
		public required int Level { get; init; }

		/// <summary>Path to enlightenment</summary>
		public required JsonPath Path { get; init; }

		public JsonPath[]? Paths { get; init; }

		public JsonPath? MaybePath { get; init; }

		/// <summary>End of the road</summary>
		public bool? Disabled { get; init; }

	}

	public sealed record MyAwesomeDevice
	{
		[JsonProperty("id")]
		public required string Id { get; init; }

		[JsonProperty("model")]
		public required string Model { get; init; }

		[JsonProperty("lastSeen")]
		public DateTimeOffset? LastSeen { get; init; }

		[JsonProperty("lastAddress")]
		public System.Net.IPAddress? LastAddress { get; init; }

	}

	[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
	[JsonDerivedType(typeof(Mammal))]
	[JsonDerivedType(typeof(Dog), "dog")]
	[JsonDerivedType(typeof(Cat), "cat")]
	[JsonDerivedType(typeof(Reptilian))]
	[JsonDerivedType(typeof(TRex), "trex")]
	public abstract record Animal
	{

		public required Guid Id { get; init; }

		public required string Name { get; init; }

	}

	public abstract record Mammal : Animal
	{

		public int LegCount { get; init; }

	}

	public sealed record Dog : Mammal
	{

		public bool IsGoodDog { get; init; }

	}

	public sealed record Cat : Mammal
	{

		public int RemainingLives { get; init; }

	}

	public abstract record Reptilian : Animal
	{

	}

	public sealed record TRex : Reptilian
	{

	}

	/// <summary>Minimal shape for the required-member contract (both paths must refuse a null-or-missing member)</summary>
	public sealed record RequiredProbeDto
	{
		public required string Name { get; init; }

		public int Age { get; init; }
	}

	[CrystalJsonConverter(CrystalJsonSerializerDefaults.Web)]
	[CrystalSerializable(typeof(Person))]
	[CrystalSerializable(typeof(MyAwesomeUser))]
	[CrystalSerializable(typeof(Animal))]
	[CrystalSerializable(typeof(LegacyBag))]
	[CrystalSerializable(typeof(RequiredProbeDto))]
	public static partial class GeneratedConverters
	{
		// generated code goes here!
	}

	[JsonSourceGenerationOptions(System.Text.Json.JsonSerializerDefaults.Web)]
	[JsonSerializable(typeof(MyAwesomeUser))]
	[JsonSerializable(typeof(Person))]
	[JsonSerializable(typeof(Animal))]
	public partial class SystemTextJsonGeneratedSerializers : JsonSerializerContext;

	#endregion

	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public class CrystalJsonGeneratorFacts : SimpleTest
	{

		[Test]
		public void Sandbox()
		{
			var dog = new Dog() { Id = Guid.Parse("f512fdd2-c306-4158-9a0a-31d4c0c70f40"), Name = "Fido", LegCount = 4, IsGoodDog = true, };
			var cat = new Cat() { Id = Guid.Parse("781b6235-f401-41a8-8d18-7a5b51e0465f"), Name = "Felix", LegCount = 4, RemainingLives = 7, };
			var trex = new TRex() { Id = Guid.Parse("5697637d-cf3a-45a2-b052-a45ce7915e8c"), Name = "Marty", };
			
			Log("STJ:");
			Log(System.Text.Json.JsonSerializer.Serialize(dog, SystemTextJsonGeneratedSerializers.Default.Animal));
			Log(System.Text.Json.JsonSerializer.Serialize(dog, SystemTextJsonGeneratedSerializers.Default.Mammal));
			Log(System.Text.Json.JsonSerializer.Serialize(dog, SystemTextJsonGeneratedSerializers.Default.Dog));
			Log(System.Text.Json.JsonSerializer.Serialize(cat, SystemTextJsonGeneratedSerializers.Default.Animal));
			Log(System.Text.Json.JsonSerializer.Serialize(cat, SystemTextJsonGeneratedSerializers.Default.Mammal));
			Log(System.Text.Json.JsonSerializer.Serialize(cat, SystemTextJsonGeneratedSerializers.Default.Cat));
			Log(System.Text.Json.JsonSerializer.Serialize(trex, SystemTextJsonGeneratedSerializers.Default.Animal));
			Log(System.Text.Json.JsonSerializer.Serialize(trex, SystemTextJsonGeneratedSerializers.Default.Reptilian));
			Log(System.Text.Json.JsonSerializer.Serialize(trex, SystemTextJsonGeneratedSerializers.Default.TRex));
			//
			Log("CodeGen:");
			Log(GeneratedConverters.Animal.ToJsonText(dog));
			Log(GeneratedConverters.Mammal.ToJsonText(dog));
			Log(GeneratedConverters.Dog.ToJsonText(dog));
			Log(GeneratedConverters.Animal.ToJsonText(cat));
			Log(GeneratedConverters.Mammal.ToJsonText(cat));
			Log(GeneratedConverters.Cat.ToJsonText(cat));
			Log(GeneratedConverters.Animal.ToJsonText(trex));
			Log(GeneratedConverters.Reptilian.ToJsonText(trex));
			Log(GeneratedConverters.TRex.ToJsonText(trex));

			var dog2 = GeneratedConverters.Animal.Deserialize("{ \"$type\": \"dog\", \"id\": \"f512fdd2-c306-4158-9a0a-31d4c0c70f40\", \"name\": \"Fido\", \"legCount\": 4, \"isGoodDog\": true }");
			Assert.That(dog2, Is.InstanceOf<Dog>().And.EqualTo(dog));
			var cat2 = GeneratedConverters.Animal.Deserialize("{ \"$type\": \"cat\", \"id\": \"781b6235-f401-41a8-8d18-7a5b51e0465f\", \"name\": \"Felix\", \"legCount\": 4, \"remainingLives\": 7 }");
			Assert.That(cat2, Is.InstanceOf<Cat>().And.EqualTo(cat));
			var trex2 = GeneratedConverters.Animal.Deserialize("{ \"$type\": \"trex\", \"id\": \"5697637d-cf3a-45a2-b052-a45ce7915e8c\", \"name\": \"Marty\", }");
			Assert.That(trex2, Is.InstanceOf<TRex>().And.EqualTo(trex));
		}

		[Test]
		public void Test_Generated_Converter_With_Legacy_Collection_Members()
		{
			// Collection<T>/ObservableCollection<T> members have no dedicated sequence shape in the generator:
			// the emitted code falls back to the runtime binder, which must produce the declared collection type

			var bag = new LegacyBag
			{
				Codes = [ 1, 2, 3 ],
				Tags = [ "red", "blue" ],
				Devices = [ new() { Serial = "X1", Model = "laser" }, new() { Serial = "X2", Model = "inkjet" } ],
			};

			var json = GeneratedConverters.LegacyBag.ToJsonText(bag);
			var obj = CrystalJson.Parse(json).AsObject();
			Assert.That(obj.Get<int[]>("codes"), Is.EqualTo((int[]) [ 1, 2, 3 ]));
			Assert.That(obj.Get<string[]>("tags"), Is.EqualTo((string[]) [ "red", "blue" ]));
			Assert.That(obj["devices"][0]["serial"], IsJson.EqualTo("X1"));

			var decoded = GeneratedConverters.LegacyBag.Deserialize(json);
			Assert.That(decoded, Is.Not.Null);
			Assert.That(decoded.Codes, Is.Not.Null.And.InstanceOf<Collection<int>>());
			Assert.That(decoded.Codes, Is.EqualTo((int[]) [ 1, 2, 3 ]));
			Assert.That(decoded.Tags, Is.Not.Null.And.InstanceOf<ObservableCollection<string>>());
			Assert.That(decoded.Tags, Is.EqualTo((string[]) [ "red", "blue" ]));
			Assert.That(decoded.Devices, Is.Not.Null.And.InstanceOf<LegacyDeviceIndex>());
			Assert.That(decoded.Devices!["X2"].Model, Is.EqualTo("inkjet"), "the by-key index must be rebuilt during deserialization");

			// the DOM pack route of the generated converter must produce arrays as well
			var packed = GeneratedConverters.LegacyBag.Pack(bag).AsObject();
			Assert.That(packed.Get<int[]>("codes"), Is.EqualTo((int[]) [ 1, 2, 3 ]));
			Assert.That(packed["devices"], IsJson.Array.And.OfSize(2));

			// and unpacking that DOM must round-trip
			var unpacked = GeneratedConverters.LegacyBag.Unpack(packed);
			Assert.That(unpacked.Codes, Is.EqualTo((int[]) [ 1, 2, 3 ]));
			Assert.That(unpacked.Tags, Is.EqualTo((string[]) [ "red", "blue" ]));
			Assert.That(unpacked.Devices!["X1"].Model, Is.EqualTo("laser"));
		}

		[Test]
		public void Test_Required_Member_Throws_On_Both_Paths()
		{
			// the C# `required` contract: a null-or-missing member refuses to bind, with the same exception
			// family on the generated path (which always enforced it) and the reflection path (which now does)
			Assert.That(
				() => GeneratedConverters.RequiredProbeDto.Deserialize("""{ "age": 5 }"""),
				Throws.InstanceOf<JsonBindingException>().With.Message.Contains("Required JSON field"),
				"the generated converter enforces the `required` contract");

			Assert.That(
				() => CrystalJson.Deserialize<RequiredProbeDto>("""{ "Age": 5 }"""),
				Throws.InstanceOf<JsonBindingException>().With.Message.Contains("Required JSON field"),
				"the reflection path enforces the same contract (the two paths agree)");

			// and a document that carries the member binds on both
			Assert.That(GeneratedConverters.RequiredProbeDto.Deserialize("""{ "name": "n", "age": 5 }""").Name, Is.EqualTo("n"));
			Assert.That(CrystalJson.Deserialize<RequiredProbeDto>("""{ "Name": "n", "Age": 5 }""").Name, Is.EqualTo("n"));
		}

		[Test]
		public void Test_Get_Converter_From_Type()
		{
			// the source generate makes a static generic GetConverterFor<T> method,
			// that returns the converter singleton for each generated type
			{
				var converter = GeneratedConverters.TypeMapper.Default.GetConverterFor<Person>();
				Assert.That(converter, Is.InstanceOf<IJsonConverter<Person>>());
			}
			{
				var converter = GeneratedConverters.TypeMapper.Default.GetConverterFor<MyAwesomeUser>();
				Assert.That(converter, Is.InstanceOf<IJsonConverter<MyAwesomeUser>>());
			}
			{
				var converter = GeneratedConverters.TypeMapper.Default.GetConverterFor<MyAwesomeMetadata>();
				Assert.That(converter, Is.InstanceOf<IJsonConverter<MyAwesomeMetadata>>());
			}
			{
				var converter = GeneratedConverters.TypeMapper.Default.GetConverterFor<Dog>();
				Assert.That(converter, Is.InstanceOf<IJsonConverter<Dog>>());
			}
		}

		[Test]
		public void Test_Get_Resolver_For_Container()
		{
			var resolver = GeneratedConverters.GetResolver();
			Assert.That(resolver, Is.Not.Null.And.Not.InstanceOf<CrystalJsonTypeResolver>());

			Assert.That(resolver.TryGetConverterFor<Person>(out var personResolver), Is.True);
			Assert.That(personResolver, Is.SameAs(GeneratedConverters.Person.Default));

			Assert.That(resolver.TryGetConverterFor(typeof(Person), out var untypedResolver), Is.True);
			Assert.That(untypedResolver, Is.SameAs(GeneratedConverters.Person.Default));

			Assert.That(resolver.TryResolveTypeDefinition<Person>(out var typeDef), Is.True);
			Assert.That(typeDef, Is.Not.Null);
			Assert.That(typeDef.Type, Is.EqualTo(typeof(Person)));
			Assert.That(typeDef.Members, Is.Not.Empty.And.Length.EqualTo(2));
			Assert.That(typeDef.Members[0].Name, Is.EqualTo("firstName"));
			Assert.That(typeDef.Members[1].Name, Is.EqualTo("familyName"));

			Assert.That(resolver.TryResolveTypeDefinition(typeof(Person), out var typeDef2), Is.True);
			Assert.That(typeDef2, Is.SameAs(typeDef));
		}

		[Test]
		public void Test_Get_Definition_From_Type()
		{
			{ // using the strongly-typed generated converter
				var typeDef = GeneratedConverters.Person.Default.GetDefinition();
				Assert.That(typeDef, Is.Not.Null);
				Assert.That(typeDef.Type, Is.EqualTo(typeof(Person)));
				Assert.That(typeDef.BaseType, Is.Null);
				Assert.That(typeDef.IsSealed, Is.False);
				Assert.That(typeDef.DefaultIsNull, Is.True);
				Assert.That(typeDef.IsAnonymousType, Is.False);
				Assert.That(typeDef.NullableOfType, Is.Null);
			}
			{ // using the generated type mapper
				Assert.That(GeneratedConverters.GetResolver().TryResolveTypeDefinition<Person>(out var typeDef), Is.True);
				Assert.That(typeDef, Is.Not.Null);
				Assert.That(typeDef.Type, Is.EqualTo(typeof(Person)));
			}
		}

		[Test]
		public void Test_Generates_Code_For_Person()
		{
			// check the serializer singleton
			var serializer = GeneratedConverters.Person.Default;
			Assert.That(serializer, Is.Not.Null);
			
			// check the property names (should be camelCased)
			Assert.That(GeneratedConverters.Person.PropertyNames.FamilyName, Is.EqualTo("familyName"));
			Assert.That(GeneratedConverters.Person.PropertyNames.FirstName, Is.EqualTo("firstName"));
			Assert.That(GeneratedConverters.Person.PropertyNames.GetAllNames(), Is.EquivalentTo([ "familyName", "firstName" ]));
			
			var person = new Person()
			{
				FamilyName = "Bond",
				FirstName = "James"
			};
			
			Log("Serialize:");
			{
				var writer = new CrystalJsonWriter(0, CrystalJsonSettings.JsonIndented, CrystalJson.DefaultResolver);
				GeneratedConverters.Person.Serialize(writer, person);
				var jsonText = writer.GetString();
				Log(jsonText);
				var obj = JsonObject.Parse(jsonText);
				Assert.That(obj["familyName"], IsJson.EqualTo("Bond"));
				Assert.That(obj["firstName"], IsJson.EqualTo("James"));
			}

			Log("Pack:");
			{
				var packed = GeneratedConverters.Person.Pack(person);
				Dump(packed);
				Assert.That(packed, IsJson.Object);
				Assert.That(packed["familyName"], IsJson.EqualTo("Bond"));
				Assert.That(packed["firstName"], IsJson.EqualTo("James"));
			}

			Log("Unpack:");
			{
				var packed = JsonObject.Create([
					("familyName", "Bond"),
					("firstName", "James"),
				]);
				var unpacked = GeneratedConverters.Person.Unpack(packed);
				Dump(unpacked);
				Assert.That(unpacked, Is.Not.Null);
				Assert.That(unpacked.FamilyName, Is.EqualTo("Bond"));
				Assert.That(unpacked.FirstName, Is.EqualTo("James"));
			}

		}

		private static MyAwesomeUser MakeSampleUser() => new()
		{
			Id = "b6a16abe-e30c-4198-8358-5f0d8fd9c283",
			DisplayName = "James Bond",
			Email = "bond@example.org",
			Type = MyAwesomeEnumType.SecretAgent,
			Roles = ["user", "secret_agent"],
			Metadata = new()
			{
				AccountCreated = DateTimeOffset.Parse("2024-09-20T12:34:56.7890123Z"),
				AccountModified = DateTimeOffset.Parse("2024-09-21T10:00:25.5461402Z"),
			},
			Items =
			[
				new() { Id = "382bb7cd-f9e4-4906-874e-ab88df954fa8", Level = 123, Path = JsonPath.Create("foo.bar") },
				new() { Id = "8092e57d-16b4-4afb-ae04-28acbeb22aa8", Level = 456, Path = JsonPath.Create("bars[2].foo"), Disabled = true }
			],
			Devices = new()
			{
				["Foo"] = new() { Id = "Foo", Model = "ACME Ultra Core 9100XX Ultra Series" },
				["Bar"] = new() { Id = "Bar", Model = "iHAL 42 Pro Ultra MaXX" },
			},
			Extras = JsonObject.ReadOnly.Create([
				("hello", "world"),
				("foo", 123),
				("bar", JsonArray.Create([ 1, 2, 3 ])),
			]),
		};

		[Test]
		public void Test_Generated_Converter_Simple_Type()
		{
			{
				var person = new Person()
				{
					FamilyName = "Bond",
					FirstName = "James"
				};

				Log(person.ToString());
				Log();
				Log("# Reference System.Text.Json:");
				Log(System.Text.Json.JsonSerializer.Serialize(person));
				Log();

				// Person => string
				Log("# Actual output:");
				var json = GeneratedConverters.Person.ToJsonText(person);
				Log(json);
				Assert.That(json, Is.EqualTo("""{ "firstName": "James", "familyName": "Bond" }"""));
				Log();

				// string => Person
				Log("# Parsing:");
				var parsed = GeneratedConverters.Person.Deserialize(json);
				Assert.That(parsed, Is.Not.Null);
				Log(parsed.ToString());
				Assert.That(parsed.FirstName, Is.EqualTo("James"));
				Assert.That(parsed.FamilyName, Is.EqualTo("Bond"));
				Log();

				// Person => JsonValue
				Log("# Packing:");
				var packed = GeneratedConverters.Person.Pack(person);
				Dump(packed);
				Assert.That(packed, IsJson.Object);
				Assert.That(packed["firstName"], IsJson.EqualTo("James"));
				Assert.That(packed["familyName"], IsJson.EqualTo("Bond"));
				Log();

				// Slice
				Log("# ToJsonSlice:");
				var slice = GeneratedConverters.Person.ToJsonSlice(person);
				DumpHexa(slice);
				Assert.That(slice.StartsWith('{'), Is.True);
				Assert.That(slice.EndsWith('}'), Is.True);
				Assert.That(JsonObject.Parse(slice), IsJson.EqualTo(packed));
				Log();

				// byte[]
				Log("# ToJsonBytes:");
				var bytes = GeneratedConverters.Person.ToJsonSlice(person);
				DumpHexa(bytes);
				Assert.That(bytes[0], Is.EqualTo('{'));
				Assert.That(slice[^1], Is.EqualTo('}'));
				Assert.That(JsonObject.Parse(slice), IsJson.EqualTo(packed));
				Log();
			}

			{
				var person = new Person() { FirstName = "👍", FamilyName = "🐶" };
				Log(person.ToString());
				Log();
				Log("# Reference System.Text.Json:");
				Log(System.Text.Json.JsonSerializer.Serialize(person));
				Log();

				Log("# Actual output:");
				var json = CrystalJson.Serialize(person, GeneratedConverters.Person.Default);
				Log(json);
				Assert.That(json, Is.EqualTo("""{ "firstName": "\ud83d\udc4d", "familyName": "\ud83d\udc36" }"""));

				var parsed = CrystalJson.Deserialize<Person>(json, GeneratedConverters.Person.Default);
				Assert.That(parsed, Is.Not.Null);
				Assert.That(parsed.FirstName, Is.EqualTo("👍"));
				Assert.That(parsed.FamilyName, Is.EqualTo("🐶"));
				Log();

				Log("# Packing:");
				var packed = GeneratedConverters.Person.Pack(person);
				Dump(packed);
				Assert.That(packed, IsJson.Object);
				Assert.That(packed["firstName"], IsJson.EqualTo("👍"));
				Assert.That(packed["familyName"], IsJson.EqualTo("🐶"));
				Log();

				// Slice
				Log("# ToJsonSlice:");
				var slice = GeneratedConverters.Person.ToJsonSlice(person);
				DumpHexa(slice);
				Assert.That(slice.StartsWith('{'), Is.True);
				Assert.That(slice.EndsWith('}'), Is.True);
				Assert.That(JsonObject.Parse(slice), IsJson.EqualTo(packed));
				Log();

				// byte[]
				Log("# ToJsonBytes:");
				var bytes = GeneratedConverters.Person.ToJsonSlice(person);
				DumpHexa(bytes);
				Assert.That(bytes[0], Is.EqualTo('{'));
				Assert.That(slice[^1], Is.EqualTo('}'));
				Assert.That(JsonObject.Parse(slice), IsJson.EqualTo(packed));
				Log();

			}

			Assert.Multiple(() =>
			{
				Assert.That(CrystalJson.Serialize(new(), GeneratedConverters.Person.Default), Is.EqualTo("""{ }"""));
				Assert.That(CrystalJson.Serialize(new() { FirstName = null, FamilyName = null }, GeneratedConverters.Person.Default), Is.EqualTo("""{ }"""));
				Assert.That(CrystalJson.Serialize(new() { FirstName = "", FamilyName = "" }, GeneratedConverters.Person.Default), Is.EqualTo("""{ "firstName": "", "familyName": "" }"""));
				Assert.That(CrystalJson.Serialize(new() { FirstName = "James" }, GeneratedConverters.Person.Default), Is.EqualTo("""{ "firstName": "James" }"""));
				Assert.That(CrystalJson.Serialize(new() { FamilyName = "Bond" }, GeneratedConverters.Person.Default), Is.EqualTo("""{ "familyName": "Bond" }"""));
			});
		}

		[Test]
		public void Test_Generated_Converter_Complex_Type()
		{
			var user = MakeSampleUser();

			Log("Expected Json:");
			var expectedJson = CrystalJson.Serialize(user, CrystalJsonSettings.Json.CamelCased());
			Log(expectedJson);

			Log();
			Log("Actual Json:");
			var json = CrystalJson.Serialize(user, GeneratedConverters.MyAwesomeUser.Default);
			Log(json);
			Assert.That(json, Is.EqualTo(expectedJson));

			{ // Compare with System.Text.Json:
				Log();
				Log("System.Text.Json reference:");
				Log(System.Text.Json.JsonSerializer.Serialize(user));
			}

			// ToSlice

			// non-pooled (return a copy)
			var bytes = CrystalJson.ToSlice(user, GeneratedConverters.MyAwesomeUser.Default);
			Assert.That(bytes.ToStringUtf8(), Is.EqualTo(json));

			{ // pooled (rented buffer)
				using (var res = CrystalJson.ToSlice(user, GeneratedConverters.MyAwesomeUser.Default, ArrayPool<byte>.Shared))
				{
					Assert.That(res.IsValid, Is.True);
					Assert.That(res.Count, Is.EqualTo(bytes.Count));
					if (!res.Data.Equals(bytes))
					{
						Assert.That(res.Data, Is.EqualTo(bytes));
					}
					if (!res.Span.SequenceEqual(bytes.Span))
					{
						Assert.That(res.Data, Is.EqualTo(bytes));
					}
				}
			}

			Log();
			Log("Parse...");
			var parsed = JsonValue.Parse(json);
			DumpCompact(parsed);
			Assert.That(parsed, IsJson.Object);
			Assert.Multiple(() =>
			{
				Assert.That(parsed["id"], IsJson.EqualTo(user.Id));
				Assert.That(parsed["displayName"], IsJson.EqualTo(user.DisplayName));
				Assert.That(parsed["email"], IsJson.EqualTo(user.Email));
				Assert.That(parsed["type"], IsJson.EqualTo(nameof(MyAwesomeEnumType.SecretAgent)));
				Assert.That(parsed["metadata"], IsJson.Object);
				Assert.That(parsed["metadata"]["accountCreated"], IsJson.EqualTo(user.Metadata.AccountCreated));
				Assert.That(parsed["metadata"]["accountModified"], IsJson.EqualTo(user.Metadata.AccountModified));
				Assert.That(parsed["metadata"]["accountDisabled"], IsJson.EqualTo(user.Metadata.AccountDisabled));
				Assert.That(parsed["items"], IsJson.Array.And.OfSize(2));
				Assert.That(parsed["items"][0], IsJson.Object);
				Assert.That(parsed["items"][0]["id"], IsJson.EqualTo("382bb7cd-f9e4-4906-874e-ab88df954fa8"));
				Assert.That(parsed["items"][0]["level"], IsJson.EqualTo(123));
				Assert.That(parsed["items"][0]["path"], IsJson.EqualTo("foo.bar"));
				Assert.That(parsed["items"][0]["disabled"], IsJson.Null);
				Assert.That(parsed["items"][1], IsJson.Object);
				Assert.That(parsed["items"][1]["id"], IsJson.EqualTo("8092e57d-16b4-4afb-ae04-28acbeb22aa8"));
				Assert.That(parsed["items"][1]["level"], IsJson.EqualTo(456));
				Assert.That(parsed["items"][1]["path"], IsJson.EqualTo("bars[2].foo"));
				Assert.That(parsed["items"][1]["disabled"], IsJson.True);
				Assert.That(parsed["devices"], IsJson.Object.And.OfSize(2));
				Assert.That(parsed["devices"]["Foo"], IsJson.Object);
				Assert.That(parsed["devices"]["Foo"]["model"], IsJson.EqualTo("ACME Ultra Core 9100XX Ultra Series"));
				Assert.That(parsed["devices"]["Bar"], IsJson.Object);
				Assert.That(parsed["devices"]["Bar"]["model"], IsJson.EqualTo("iHAL 42 Pro Ultra MaXX"));
				Assert.That(parsed["extras"], IsJson.Object.And.EqualTo(user.Extras));
			});

			Log();
			Log("Deserialize...");
			var decoded = GeneratedConverters.MyAwesomeUser.Unpack(parsed);
			Assert.That(decoded, Is.Not.Null);
			Assert.That(decoded.Id, Is.EqualTo(user.Id));
			Assert.That(decoded.DisplayName, Is.EqualTo(user.DisplayName));
			Assert.That(decoded.Email, Is.EqualTo(user.Email));
			Assert.That(decoded.Type, Is.EqualTo(user.Type));
			Assert.That(decoded.Metadata, Is.EqualTo(user.Metadata));
			Assert.That(decoded.Roles, Is.EqualTo(user.Roles));
			Assert.That(decoded.Devices, Is.EqualTo(user.Devices));
			Assert.That(decoded.Extras, IsJson.EqualTo(user.Extras));

			Log();
			Log("Pack...");
			var packed = GeneratedConverters.MyAwesomeUser.Pack(user);
			Dump(packed);
			Assert.That(packed, IsJson.Object);
			Assert.Multiple(() =>
			{
				Assert.That(packed["id"], IsJson.EqualTo(user.Id));
				Assert.That(packed["displayName"], IsJson.EqualTo(user.DisplayName));
				Assert.That(packed["email"], IsJson.EqualTo(user.Email));
				Assert.That(packed["type"], IsJson.EqualTo(user.Type));
				Assert.That(packed["metadata"], IsJson.Object);
				Assert.That(packed["metadata"]["accountCreated"], IsJson.EqualTo(user.Metadata.AccountCreated));
				Assert.That(packed["metadata"]["accountModified"], IsJson.EqualTo(user.Metadata.AccountModified));
				Assert.That(packed["metadata"]["accountDisabled"], IsJson.EqualTo(user.Metadata.AccountDisabled));
				Assert.That(packed["items"], IsJson.Array.And.OfSize(2));
				Assert.That(packed["items"][0], IsJson.Object);
				Assert.That(packed["items"][0]["id"], IsJson.EqualTo("382bb7cd-f9e4-4906-874e-ab88df954fa8"));
				Assert.That(packed["items"][1], IsJson.Object);
				Assert.That(packed["items"][1]["id"], IsJson.EqualTo("8092e57d-16b4-4afb-ae04-28acbeb22aa8"));
				Assert.That(packed["devices"], IsJson.Object.And.OfSize(2));
				Assert.That(packed["devices"]["Foo"], IsJson.Object);
				Assert.That(packed["devices"]["Bar"], IsJson.Object);
				Assert.That(packed["extras"], IsJson.Object.And.EqualTo(user.Extras));
			});
		}

		[Test]
		public void Test_Generated_Converter_Derived_Type()
		{
			var dog = new Dog()
			{
				Id = Guid.Parse("f512fdd2-c306-4158-9a0a-31d4c0c70f40"),
				Name = "Fido",
				LegCount = 4,
				IsGoodDog = true, 
			};

			Assert.That(GeneratedConverters.Dog.PropertyNames._TypeDiscriminatorProperty_, Is.EqualTo("$type"));
			Assert.That(GeneratedConverters.Dog.PropertyNames._TypeDiscriminatorValue_, Is.EqualTo("dog"));
			Assert.That(GeneratedConverters.Dog.PropertyNames.Id, Is.EqualTo("id"));
			Assert.That(GeneratedConverters.Dog.PropertyNames.Name, Is.EqualTo("name"));
			Assert.That(GeneratedConverters.Dog.PropertyNames.LegCount, Is.EqualTo("legCount"));
			Assert.That(GeneratedConverters.Dog.PropertyNames.IsGoodDog, Is.EqualTo("isGoodDog"));

			Log("Serialized JSON:");
			var json = CrystalJson.Serialize(dog, GeneratedConverters.Dog.Default);
			Log(json);

			{ // Compare with System.Text.Json:
				Log();
				Log("System.Text.Json reference:");
				Log(System.Text.Json.JsonSerializer.Serialize(dog));
			}

			// ToSlice

			// non-pooled (return a copy)
			var bytes = CrystalJson.ToSlice(dog, GeneratedConverters.Dog.Default);
			Assert.That(bytes.ToStringUtf8(), Is.EqualTo(json));

			{ // pooled (rented buffer)
				using (var res = CrystalJson.ToSlice(dog, GeneratedConverters.Dog.Default, ArrayPool<byte>.Shared))
				{
					Assert.That(res.IsValid, Is.True);
					Assert.That(res.Count, Is.EqualTo(bytes.Count));
					if (!res.Data.Equals(bytes))
					{
						Assert.That(res.Data, Is.EqualTo(bytes));
					}
					if (!res.Span.SequenceEqual(bytes.Span))
					{
						Assert.That(res.Data, Is.EqualTo(bytes));
					}
				}
			}

			Log();
			Log("Parse...");
			var parsed = JsonValue.Parse(json);
			DumpCompact(parsed);
			Assert.That(parsed, IsJson.Object);
			Assert.Multiple(() =>
			{
				Assert.That(parsed["id"], IsJson.EqualTo(dog.Id));
				Assert.That(parsed["name"], IsJson.EqualTo(dog.Name));
				Assert.That(parsed["legCount"], IsJson.EqualTo(dog.LegCount));
				Assert.That(parsed["isGoodDog"], IsJson.EqualTo(dog.IsGoodDog)); //REVIEW: enum default as numbers or string ?
				Assert.That(parsed["$type"], IsJson.EqualTo("dog"));
			});

			Log();
			Log("Deserialize as Dog...");
			{
				var decodedDog = GeneratedConverters.Dog.Unpack(parsed);
				Assert.That(decodedDog, Is.Not.Null);
				Assert.That(decodedDog.Id, Is.EqualTo(dog.Id));
				Assert.That(decodedDog.Name, Is.EqualTo(dog.Name));
				Assert.That(decodedDog.LegCount, Is.EqualTo(dog.LegCount));
				Assert.That(decodedDog.IsGoodDog, Is.EqualTo(dog.IsGoodDog));
			}

			Log();
			Log("Deserialize as Animal...");
			{
				var decodedAnimal = GeneratedConverters.Animal.Unpack(parsed);
				Assert.That(decodedAnimal, Is.Not.Null.And.InstanceOf<Dog>());
				Assert.That(decodedAnimal.Id, Is.EqualTo(dog.Id));
				Assert.That(decodedAnimal.Name, Is.EqualTo(dog.Name));
				Assert.That(((Mammal) decodedAnimal).LegCount, Is.EqualTo(dog.LegCount));
				Assert.That(((Dog) decodedAnimal).IsGoodDog, Is.EqualTo(dog.IsGoodDog));
			}

			Log();
			Log("Deserialize as Mammal...");
			{
				var decodedMammal = GeneratedConverters.Mammal.Unpack(parsed);
				Assert.That(decodedMammal, Is.Not.Null.And.InstanceOf<Dog>());
				Assert.That(decodedMammal.Id, Is.EqualTo(dog.Id));
				Assert.That(decodedMammal.Name, Is.EqualTo(dog.Name));
				Assert.That(decodedMammal.LegCount, Is.EqualTo(dog.LegCount));
				Assert.That(((Dog) decodedMammal).IsGoodDog, Is.EqualTo(dog.IsGoodDog));
			}

			Log();
			Log("Pack...");
			var packed = GeneratedConverters.Dog.Pack(dog);
			Dump(packed);
			Assert.That(packed, IsJson.Object);
			Assert.Multiple(() =>
			{
				Assert.That(packed["id"], IsJson.EqualTo(dog.Id));
				Assert.That(packed["name"], IsJson.EqualTo(dog.Name));
				Assert.That(packed["legCount"], IsJson.EqualTo(dog.LegCount));
				Assert.That(packed["isGoodDog"], IsJson.EqualTo(dog.IsGoodDog));
				Assert.That(packed["$type"], IsJson.EqualTo("dog"));
			});
		}

		[Test]
		public void Test_JsonReadOnlyProxy_FromValue_SimpleType()
		{
			// Test that FromValue(TValue) returns a read-only proxy that match the original instance

			var person = new Person()
			{
				FamilyName = "Bond",
				FirstName = "James"
			};

			Log("Person:");
			Log(person.ToString());

			// Convert the Person into a proxy that wraps a read-only JsonObject
			Log("FromValue(Person)");
			var proxy = GeneratedConverters.Person.ToReadOnly(person);
			Log(proxy.ToString());
			Assert.That(proxy.FamilyName, Is.EqualTo("Bond"));
			Assert.That(proxy.FirstName, Is.EqualTo("James"));

			// inspect the wrapped JsonObject
			Log("ToJson()");
			var json = proxy.ToJsonValue();
			Dump(json);
			Assert.That(json, IsJson.Object.And.ReadOnly);
			Assert.That(json["familyName"], IsJson.EqualTo("Bond"));
			Assert.That(json["firstName"], IsJson.EqualTo("James"));
			Assert.That(json, IsJson.OfSize(2));

			// serialize back into a Person
			Log("ToValue()");
			var decoded = proxy.ToValue();
			Assert.That(decoded, Is.Not.Null);
			Log(decoded.ToString());
			Assert.That(decoded, Is.InstanceOf<Person>().And.Not.SameAs(person));
			Assert.That(decoded.FamilyName, Is.EqualTo(person.FamilyName));
			Assert.That(decoded.FirstName, Is.EqualTo(person.FirstName));
		}

		[Test]
		public void Test_JsonReadOnlyProxy_FromValue_DerivedType()
		{
			// Test that FromValue(TValue) returns a read-only proxy that match the original instance

			var cat = new Cat()
			{
				Id = Guid.NewGuid(),
				Name = "Felix",
				LegCount = 4,
				RemainingLives = 7,
			};

			Log("Cat:");
			Log(cat.ToString());

			// Convert the Person into a proxy that wraps a read-only JsonObject
			Log("FromValue(Cat)");
			var proxy = GeneratedConverters.Cat.ToReadOnly(cat);
			Log(proxy.ToString());
			Assert.That(proxy.Id, Is.EqualTo(cat.Id));
			Assert.That(proxy.Name, Is.EqualTo("Felix"));
			Assert.That(proxy.LegCount, Is.EqualTo(4));
			Assert.That(proxy.RemainingLives, Is.EqualTo(7));
			Assert.That(proxy["$type"].ToJsonValue(), IsJson.EqualTo("cat"));

			// inspect the wrapped JsonObject
			Log("ToJson()");
			var json = proxy.ToJsonValue();
			Dump(json);
			Assert.That(json, IsJson.Object.And.ReadOnly);
			Assert.That(json["id"], IsJson.EqualTo(cat.Id));
			Assert.That(json["name"], IsJson.EqualTo("Felix"));
			Assert.That(json["legCount"], IsJson.EqualTo(4));
			Assert.That(json["remainingLives"], IsJson.EqualTo(7));
			Assert.That(json["$type"], IsJson.EqualTo("cat"));
			Assert.That(json, IsJson.OfSize(5));

			// serialize back into a Person
			Log("ToValue()");
			var decoded = proxy.ToValue();
			Assert.That(decoded, Is.Not.Null);
			Log(decoded.ToString());
			Assert.That(decoded, Is.InstanceOf<Cat>().And.Not.SameAs(cat));
			Assert.That(decoded.Id, Is.EqualTo(cat.Id));
			Assert.That(decoded.Name, Is.EqualTo(cat.Name));
			Assert.That(decoded.LegCount, Is.EqualTo(cat.LegCount));
			Assert.That(decoded.RemainingLives, Is.EqualTo(cat.RemainingLives));
		}

		[Test]
		public void Test_JsonReadOnlyProxy_Mutate_SimpleType()
		{
			var person = new Person()
			{
				FamilyName = "Bond",
				FirstName = "James"
			};
			Log("Person:");
			Dump(person);

			Log("ReadOnly:");
			var proxy = GeneratedConverters.Person.ToReadOnly(person);
			Log(proxy.ToString());
			Assert.That(proxy.FamilyName, Is.EqualTo("Bond"));
			Assert.That(proxy.FirstName, Is.EqualTo("James"));
			Assert.That(proxy.ToJsonValue(), IsJson.ReadOnly);

			{ // ReadOnly.With(Mutable => .... }
				var mutated = proxy.With(
					m =>
					{
						Assert.That(m.FamilyName, Is.EqualTo("Bond"));
						Assert.That(m.FirstName, Is.EqualTo("James"));
						// the JSON should a mutable copy of the original
						Assert.That(m.ToJsonValue(), IsJson.Object.And.Mutable.And.EqualTo(proxy.ToJsonValue()));

						m.FirstName = "Jim";
						Assert.That(m.FirstName, Is.EqualTo("Jim"));
						Assert.That(m.ToJsonValue()["firstName"], IsJson.EqualTo("Jim"));
					});

				Log(mutated.ToString());
				// should return an updated object
				Assert.That(mutated.FamilyName, Is.EqualTo("Bond"));
				Assert.That(mutated.FirstName, Is.EqualTo("Jim"));
				Assert.That(mutated.ToJsonValue(), IsJson.ReadOnly);

				// should not change the original!
				Assert.That(proxy.FamilyName, Is.EqualTo("Bond"));
				Assert.That(proxy.FirstName, Is.EqualTo("James"));
			}

			{ // ReadOnly.ToMutable with { ... }

				var mutated = proxy.ToMutable() with { FirstName = "Jim" };
				Log(mutated.ToString());

				// should return an updated object
				Assert.That(mutated.FamilyName, Is.EqualTo("Bond"));
				Assert.That(mutated.FirstName, Is.EqualTo("Jim"));
				Assert.That(mutated.ToJsonValue(), IsJson.Mutable);

				// should not change the original!
				Assert.That(proxy.FamilyName, Is.EqualTo("Bond"));
				Assert.That(proxy.FirstName, Is.EqualTo("James"));
			}

		}

		[Test]
		public void Test_JsonReadOnlyProxy_Mutate_ComplexType()
		{
			var user = MakeSampleUser();

			Log("User:");
			Dump(user);

			Log("ReadOnly:");
			var proxy = GeneratedConverters.MyAwesomeUser.ToReadOnly(user);
			Log(proxy.ToString());
			Assert.That(user.DisplayName, Is.EqualTo("James Bond"));
			Assert.That(user.Roles, Is.EqualTo((string[]) ["user", "secret_agent"]));
			Assert.That(proxy.ToJsonValue(), IsJson.ReadOnly);

			{ // ReadOnly.With(Mutable => .... }
				var mutated = proxy.With(
					m =>
					{
						Assert.That(m.DisplayName, Is.EqualTo("James Bond"));
						Assert.That(m.Roles, Is.EqualTo((string[]) ["user", "secret_agent"]));
						// the JSON should a mutable copy of the original
						Assert.That(m.ToJsonValue(), IsJson.Object.And.Mutable.And.EqualTo(proxy.ToJsonValue()));

						m.DisplayName = "Jim Bond";
						Assert.That(m.DisplayName, Is.EqualTo("Jim Bond"));
						Assert.That(m.ToJsonValue()["displayName"], IsJson.EqualTo("Jim Bond"));

						m.Roles = ["user", "secret_agent", "retired"];
						Assert.That(m.Roles, Is.EqualTo((string[]) ["user", "secret_agent", "retired"]));
						Assert.That(m.ToJsonValue()["roles"], IsJson.Array.And.EqualTo((string[]) ["user", "secret_agent", "retired"]));
					});

				Log(mutated.ToString());
				// should return an updated object
				Assert.That(mutated.DisplayName, Is.EqualTo("Jim Bond"));
				Assert.That(mutated.Roles, Is.EqualTo((string[]) ["user", "secret_agent", "retired"]));
				Assert.That(mutated.ToJsonValue(), IsJson.ReadOnly);

				// should not change the original!
				Assert.That(proxy.DisplayName, Is.EqualTo("James Bond"));
				Assert.That(proxy.Roles, Is.EqualTo((string[]) ["user", "secret_agent"]));
			}

			{ // ReadOnly.ToMutable with { ... }

				var mutated = proxy.ToMutable() with { Type = MyAwesomeEnumType.DoubleAgent };
				Log(mutated.ToString());

				// should return an updated object
				Assert.That(mutated.Type, Is.EqualTo(MyAwesomeEnumType.DoubleAgent));
				Assert.That(mutated.ToJsonValue(), IsJson.Mutable);

				// should not change the original!
				Assert.That(proxy.Type, Is.EqualTo(MyAwesomeEnumType.SecretAgent));
			}
		}

		[Test]
		public void Test_JsonWritableProxy_FromValue_SimpleType()
		{
			// Test that FromValue(TValue) returns a read-only proxy that match the original instance

			var person = new Person()
			{
				FamilyName = "Bond",
				FirstName = "James"
			};

			Log("Person:");
			Log(person.ToString());

			// Convert the Person into a proxy that wraps a read-only JsonObject
			Log("FromValue(Person)");
			var proxy = GeneratedConverters.Person.ToMutable(person);
			Log(proxy.ToString());
			Assert.That(proxy.FamilyName, Is.EqualTo("Bond"));
			Assert.That(proxy.FirstName, Is.EqualTo("James"));

			// inspect the wrapped JsonObject
			Log("ToJson()");
			var json = proxy.ToJsonValue();
			Dump(json);
			Assert.That(json, IsJson.Object.And.Mutable);
			Assert.That(json["familyName"], IsJson.EqualTo("Bond"));
			Assert.That(json["firstName"], IsJson.EqualTo("James"));
			Assert.That(json, IsJson.OfSize(2));

			// mutate the object
			proxy.FirstName = "Jim";

			Assert.That(proxy.FirstName, Is.EqualTo("Jim"));
			Assert.That(json["firstName"], IsJson.EqualTo("Jim"));
			// the original should not be changed
			Assert.That(person.FirstName, Is.EqualTo("James"));

			// serialize back into a Person
			Log("ToValue()");
			var decoded = proxy.ToValue();
			Assert.That(decoded, Is.Not.Null);
			Log(decoded.ToString());
			Assert.That(decoded, Is.InstanceOf<Person>().And.Not.SameAs(person));
			Assert.That(decoded.FamilyName, Is.EqualTo(person.FamilyName));
			Assert.That(decoded.FirstName, Is.EqualTo("Jim"));
		}

		[Test]
		public void Test_JsonWritableProxy_FromValue_DerivedType()
		{
			// Test that FromValue(TValue) returns a read-only proxy that match the original instance

			var cat = new Cat()
			{
				Id = Guid.NewGuid(),
				Name = "Felix",
				LegCount = 4,
				RemainingLives = 7,
			};


			Log("Cat:");
			Log(cat.ToString());

			// Convert the Person into a proxy that wraps a read-only JsonObject
			Log("FromValue(Cat)");
			var proxy = GeneratedConverters.Cat.ToMutable(cat);
			Log(proxy.ToString());
			Assert.That(proxy.Id, Is.EqualTo(cat.Id));
			Assert.That(proxy.Name, Is.EqualTo("Felix"));
			Assert.That(proxy.LegCount, Is.EqualTo(4));
			Assert.That(proxy.RemainingLives, Is.EqualTo(7));
			Assert.That(proxy["$type"].ToJsonValue(), IsJson.EqualTo("cat"));

			// inspect the wrapped JsonObject
			Log("ToJson()");
			var json = proxy.ToJsonValue();
			Dump(json);
			Assert.That(json, IsJson.Object.And.Mutable);
			Assert.That(json["id"], IsJson.EqualTo(cat.Id));
			Assert.That(json["name"], IsJson.EqualTo("Felix"));
			Assert.That(json["legCount"], IsJson.EqualTo(4));
			Assert.That(json["remainingLives"], IsJson.EqualTo(7));
			Assert.That(json["$type"], IsJson.EqualTo("cat"));
			Assert.That(json, IsJson.OfSize(5));

			// mutate the object
			proxy.Name = "Jellie";

			Assert.That(proxy.Name, Is.EqualTo("Jellie"));
			Assert.That(json["name"], IsJson.EqualTo("Jellie"));
			// the original should not be changed
			Assert.That(cat.Name, Is.EqualTo("Felix"));

			// serialize back into a Person
			Log("ToValue()");
			var decoded = proxy.ToValue();
			Assert.That(decoded, Is.Not.Null);
			Log(decoded.ToString());
			Assert.That(decoded, Is.InstanceOf<Cat>().And.Not.SameAs(cat));
			Assert.That(decoded.Id, Is.EqualTo(cat.Id));
			Assert.That(decoded.Name, Is.EqualTo("Jellie"));
			Assert.That(decoded.LegCount, Is.EqualTo(cat.LegCount));
			Assert.That(decoded.RemainingLives, Is.EqualTo(cat.RemainingLives));
		}

		[Test]
		public void Test_JsonReadOnlyProxy_Keeps_Extra_Fields()
		{
			var obj = JsonObject.ReadOnly.Create(
			[
				("familyName", "Bond"),
				("firstName", "James"),
				("hello", "world"),
				("foo", 123),
			]);
			Dump(obj);

			var proxy = GeneratedConverters.Person.ToReadOnly(obj);
			Log(proxy.ToString());
			Assert.That(proxy.FamilyName, Is.EqualTo("Bond"));
			Assert.That(proxy.FirstName, Is.EqualTo("James"));

			var updated = proxy.With(m => m.FirstName = "Jim");

			var export = updated.ToJsonValue();
			Dump(obj);
			Assert.That(export, IsJson.Object);
			Assert.That(export["familyName"], IsJson.EqualTo("Bond"));
			Assert.That(export["firstName"], IsJson.EqualTo("Jim"));
			Assert.That(export["hello"], IsJson.EqualTo("world"));
			Assert.That(export["foo"], IsJson.EqualTo(123));
		}

		[Test]
		public void Test_JsonReadOnlyProxy_FromValue_ComplexType()
		{
			var user = MakeSampleUser();
			Log("User:");
			Log(user.ToString());

			Log("ReadOnly:");
			var proxy = GeneratedConverters.MyAwesomeUser.ToReadOnly(user);
			Log(proxy.ToString());
			Assert.That(proxy.Id, Is.EqualTo(user.Id));
			Assert.That(proxy.DisplayName, Is.EqualTo(user.DisplayName));
			Assert.That(proxy.Metadata.AccountCreated, Is.EqualTo(user.Metadata.AccountCreated));
			Assert.That(proxy.Items[0].Id, Is.EqualTo(user.Items![0].Id));
			Assert.That(proxy.Devices["Foo"].Id, Is.EqualTo(user.Devices!["Foo"].Id));
			Assert.That(proxy.Extras, IsJson.ReadOnly.And.EqualTo(user.Extras));
			Assert.That(proxy.HasId(), Is.True);
			Assert.That(proxy.HasDisplayName(), Is.True);
			Assert.That(proxy.HasEmail(), Is.True);
			Assert.That(proxy.HasMetadata(), Is.True);

			var json = proxy.ToJsonValue();
			Log("JSON:");
			Dump(json);
			Assert.That(json, IsJson.Object.And.ReadOnly);
			Assert.That(json["id"], IsJson.EqualTo(user.Id));
			Assert.That(json["metadata"]["accountCreated"], IsJson.EqualTo(user.Metadata.AccountCreated));
			Assert.That(json["items"][0]["id"], IsJson.EqualTo(user.Items[0].Id));
			Assert.That(json["devices"]["Foo"]["id"], IsJson.EqualTo(user.Devices["Foo"].Id));
			Assert.That(json["extras"], IsJson.ReadOnly.And.EqualTo(user.Extras));

			Assert.That(proxy.Equals(json), Is.True);
			Assert.That(proxy.Equals(proxy), Is.True);
			Assert.That(proxy.Equals(proxy.Get()), Is.True);
			Assert.That(proxy.Equals(JsonObject.ReadOnly.Empty), Is.False);
			Assert.That(proxy.Equals((object?) json), Is.True);
			Assert.That(proxy.Equals((object?) proxy), Is.True);
			Assert.That(proxy.Equals((object?) proxy.Get()), Is.True);
			Assert.That(proxy.Equals(ObservableJsonValue.Untracked(json)), Is.True);
			Assert.That(proxy.GetHashCode(), Is.EqualTo(json.GetHashCode()));

			var decoded = proxy.ToValue();
			Log("Decoded:");
			Log(decoded.ToString());
			Assert.That(decoded, Is.Not.Null);
			Assert.That(decoded.Id, Is.EqualTo(user.Id));
			Assert.That(decoded.DisplayName, Is.EqualTo(user.DisplayName));
		}

		[Test]
		public void Test_JsonReadOnlyProxy_With_Empty_Object()
		{
			var proxy = GeneratedConverters.MyAwesomeUser.ToReadOnly(JsonObject.ReadOnly.Empty);

			// all "required" members should throw
			Assert.That(proxy.HasId(), Is.False);
			Assert.That(() => proxy.Id, Throws.InstanceOf<JsonBindingException>());
			Assert.That(proxy.HasEmail(), Is.False);
			Assert.That(() => proxy.Email, Throws.InstanceOf<JsonBindingException>());
			Assert.That(proxy.HasDisplayName(), Is.False);
			Assert.That(() => proxy.DisplayName, Throws.InstanceOf<JsonBindingException>());

			// optional members should return their default value
			Assert.That(proxy.Description, Is.Null);
			Assert.That(proxy.Type, Is.EqualTo(MyAwesomeEnumType.SecretAgent)); // custom default value!
			Assert.That(proxy.Extras, Is.Null);

			// required inner containers should not throw
			Assert.That(proxy.HasMetadata(), Is.False);
			Assert.That(() => proxy.Metadata, Throws.Nothing);
			Assert.That(proxy.Metadata.Exists(), Is.False);
			Assert.That(proxy.Metadata.IsNullOrMissing(), Is.True);
			Assert.That(proxy.Metadata.IsObject(), Is.False);
			Assert.That(proxy.Metadata.IsObjectOrMissing(), Is.True);
			// their fields should return null
			Assert.That(proxy.Metadata.HasAccountCreated(), Is.False);
			Assert.That(() => proxy.Metadata.AccountCreated, Throws.InstanceOf<JsonBindingException>());
			Assert.That(proxy.Metadata.HasAccountModified(), Is.False);
			Assert.That(() => proxy.Metadata.AccountModified, Throws.InstanceOf<JsonBindingException>());
			Assert.That(proxy.Metadata.AccountDisabled, Is.Null);

			// optional inner containers should not throw
			Assert.That(() => proxy.Items, Throws.Nothing);
			Assert.That(proxy.Items.Exists(), Is.False);
			Assert.That(proxy.Items.IsNullOrMissing(), Is.True);
			Assert.That(proxy.Items.IsNullOrEmpty(), Is.True);
			Assert.That(proxy.Items.IsArray(), Is.False);
			Assert.That(proxy.Items.IsArrayOrMissing(), Is.True);

			// ToString() and equality methods
			Assert.That(proxy.ToString(), Is.EqualTo("(MyAwesomeUser) { }"));
			Assert.That(proxy.Equals(JsonObject.ReadOnly.Empty), Is.True);
			Assert.That(proxy.Equals(JsonObject.Create()), Is.True);
			Assert.That(proxy.Equals(ObservableJsonValue.Untracked(JsonObject.ReadOnly.Empty)), Is.True);
			Assert.That(proxy.GetHashCode(), Is.EqualTo(JsonObject.ReadOnly.Empty.GetHashCode()));
		}

		[Test]
		public void Test_JsonWritableProxy_FromValue_ComplexType()
		{
			var user = MakeSampleUser();
			Log("User:");
			Log(user.ToString());

			Log("ReadOnly:");
			var proxy = GeneratedConverters.MyAwesomeUser.ToMutable(user);
			Log(proxy.ToString());

			// it should wrap a MutableJsonValue
			Assert.That(proxy.Get(), Is.Not.Null);
			Assert.That(proxy.Get().Type, Is.EqualTo(JsonType.Object));
			// which should wrap a mutable object
			Assert.That(proxy.ToJsonValue(), Is.Not.Null);
			Assert.That(proxy.ToJsonValue(), IsJson.Object.And.Mutable);
			// it should be untracked
			Assert.That(proxy.GetContext(), Is.Null);

			// it should expose typed properties
			Assert.That(proxy.Id, Is.EqualTo(user.Id));
			Assert.That(proxy.DisplayName, Is.EqualTo(user.DisplayName));
			Assert.That(proxy.Metadata.AccountCreated, Is.EqualTo(user.Metadata.AccountCreated));
			Assert.That(proxy.Metadata.AccountModified, Is.EqualTo(user.Metadata.AccountModified));
			Assert.That(proxy.Type, Is.EqualTo(user.Type));
			Assert.That(proxy.Items[0].Id, Is.EqualTo(user.Items![0].Id));
			Assert.That(proxy.Devices["Foo"].Id, Is.EqualTo(user.Devices!["Foo"].Id));
			Assert.That(proxy.Extras.ToJsonValue(), IsJson.EqualTo(user.Extras));
			Assert.That(proxy.Extras.ToJsonValue(), IsJson.ReadOnly, "Original JSON should be left as-is until it is mutated");
			Assert.That(proxy.Extras["foo"].As<int>(), Is.EqualTo(123));
			Assert.That(proxy.Extras["bar"][^1].As<int>(), Is.EqualTo(3));
			Assert.That(proxy.Extras["foo"].ToJsonValue(), IsJson.EqualTo(123));
			Assert.That(proxy.Extras["bar"][^1].ToJsonValue(), IsJson.EqualTo(3));

			// it should be able to generate full paths to any item
			Assert.That(proxy.Metadata.GetPath(), Is.EqualTo("metadata"));
			Assert.That(proxy.Items.GetPath(), Is.EqualTo("items"));
			Assert.That(proxy.Items[0].GetPath(), Is.EqualTo("items[0]"));
			Assert.That(proxy.Items[1].GetPath(), Is.EqualTo("items[1]"));
			Assert.That(proxy.Items[1]["id"].GetPath(), Is.EqualTo("items[1].id"));
			Assert.That(proxy.Devices.GetPath(), Is.EqualTo("devices"));
			Assert.That(proxy.Devices["Foo"].GetPath(), Is.EqualTo("devices.Foo"));
			Assert.That(proxy.Extras["bar"][^1].GetPath(), Is.EqualTo("extras.bar[^1]"));

			var json = proxy.ToJsonValue();
			Log("JSON:");
			Dump(json);
			Assert.That(json, IsJson.Object.And.Mutable);
			Assert.That(json["id"], IsJson.EqualTo(user.Id));
			Assert.That(json["displayName"], IsJson.EqualTo(user.DisplayName));
			Assert.That(json["metadata"]["accountCreated"], IsJson.EqualTo(user.Metadata.AccountCreated));
			Assert.That(json["items"][0]["level"], IsJson.EqualTo(user.Items[0].Level));
			Assert.That(json["devices"]["Foo"]["id"], IsJson.EqualTo(user.Devices["Foo"].Id));
			Assert.That(json["extras"], IsJson.ReadOnly.And.EqualTo(user.Extras));

			// misc
			Assert.That(proxy.ToString(), Does.StartWith("(MyAwesomeUser) {").And.EndsWith("}"));
			Assert.That(proxy.Equals(json), Is.True);
			Assert.That(proxy.Equals(json.Copy()), Is.True);
			Assert.That(proxy.Equals(((JsonObject) json).CopyAndAdd("random", Guid.NewGuid())), Is.False);
			Assert.That(proxy.Equals(MutableJsonValue.Untracked(json)), Is.True);
			Assert.That(proxy.Equals(MutableJsonValue.Untracked(((JsonObject) json).CopyAndAdd("random", Guid.NewGuid()))), Is.False);
			// it is not allowed to get the hashcode of a mutable value!
			Assert.That(() => proxy.GetHashCode(), Throws.InstanceOf<NotSupportedException>());

			// mutate

			proxy.DisplayName = "Jim Bond";
			proxy.Metadata.AccountModified = DateTimeOffset.Parse("2024-10-12T17:37:42.6732914Z");
			proxy.Items[0].Level = 8001;
			proxy.Items.Add(new MyAwesomeStruct() { Id = "47c774e7-29e7-40fe-ba06-3098cafe77be", Level = 789, Path = JsonPath.Create("hello") });
			proxy.Devices["Foo"].LastAddress = IPAddress.Parse("192.168.1.2");
			proxy.Devices.Remove("Bar");
			proxy.Extras.Set("bonus", 456); // the readonly-object should be automatically upgraded to mutable!

			Log("Mutated:");
			Dump(proxy.ToJsonValue());

			var decoded = proxy.ToValue();
			Log("Decoded:");
			Log(decoded.ToString());
			Assert.That(decoded, Is.Not.Null);
			Assert.That(decoded.Id, Is.EqualTo(user.Id));
			Assert.That(decoded.DisplayName, Is.EqualTo("Jim Bond"));
			Assert.That(decoded.Metadata.AccountCreated, Is.EqualTo(user.Metadata.AccountCreated));
			Assert.That(decoded.Metadata.AccountModified, Is.Not.EqualTo(user.Metadata.AccountCreated).And.EqualTo(DateTimeOffset.Parse("2024-10-12T17:37:42.6732914Z")));
			Assert.That(decoded.Items![0].Level, Is.EqualTo(8001));
			Assert.That(decoded.Items, Has.Count.EqualTo(3));
			Assert.That(decoded.Items![2].Level, Is.EqualTo(789));
			Assert.That(decoded.Devices!["Foo"].LastAddress, Is.EqualTo(IPAddress.Parse("192.168.1.2")));
			Assert.That(decoded.Devices, Does.Not.ContainKey("Bar"));
			Assert.That(decoded.Extras?["bonus"], IsJson.EqualTo(456));
		}

		[Test]
		public void Test_JsonWritableProxy_With_Empty_Object()
		{
			var proxy = GeneratedConverters.MyAwesomeUser.ToMutable(JsonObject.ReadOnly.Empty);

			// all members return their default value (even required member)
			Assert.That(proxy.Id, Is.Null);
			Assert.That(proxy.Email, Is.Null);
			Assert.That(proxy.DisplayName, Is.Null);
			Assert.That(proxy.Description, Is.Null);
			Assert.That(proxy.Type, Is.EqualTo(MyAwesomeEnumType.SecretAgent)); // custom default value!

			// required inner containers should not throw
			Assert.That(() => proxy.Metadata, Throws.Nothing);
			Assert.That(proxy.Metadata.Exists(), Is.False);
			Assert.That(proxy.Metadata.IsNullOrMissing(), Is.True);
			Assert.That(proxy.Metadata.IsObject(), Is.False);
			Assert.That(proxy.Metadata.IsObjectOrMissing(), Is.True);
			// their fields should return null
			Assert.That(proxy.Metadata.AccountCreated, Is.Default);
			Assert.That(proxy.Metadata.AccountModified, Is.Default);
			Assert.That(proxy.Metadata.AccountDisabled, Is.Null);

			// JsonObject should be wrapped as MutableJsonValue
			Assert.That(proxy.Extras, Is.Not.Null); // should be wrapped!
			Assert.That(proxy.Extras.Exists(), Is.False);
			Assert.That(proxy.Extras.ToJsonValue(), IsJson.Null);

			// optional inner containers should not throw
			Assert.That(() => proxy.Items, Throws.Nothing);
			Assert.That(proxy.Items.Exists(), Is.False);
			Assert.That(proxy.Items.IsNullOrMissing(), Is.True);
			Assert.That(proxy.Items.IsNullOrEmpty(), Is.True);
			Assert.That(proxy.Items.IsArray(), Is.False);
			Assert.That(proxy.Items.IsArrayOrMissing(), Is.True);

			// misc
			Assert.That(proxy.ToString(), Is.EqualTo("(MyAwesomeUser) { }"));
			Assert.That(proxy.Equals(JsonObject.ReadOnly.Empty), Is.True);
			Assert.That(proxy.Equals(JsonObject.Create()), Is.True);
			Assert.That(proxy.Equals(MutableJsonValue.Untracked(JsonObject.ReadOnly.Empty)), Is.True);
			// it is not allowed to get the hashcode of a mutable value!
			Assert.That(() => proxy.GetHashCode(), Throws.InstanceOf<NotSupportedException>());
		}


		[Test]
		[Category("Benchmark")]
		public void Bench_Custom_Serializer()
		{
			var user = new MyAwesomeUser()
			{
				Id = "b6a16abe-e30c-4198-8358-5f0d8fd9c283",
				DisplayName = "James Bond",
				Email = "bond@example.org",
				Type = MyAwesomeEnumType.SecretAgent,
				Roles = [ "user", "secret_agent" ],
				Metadata = new ()
				{
					AccountCreated = DateTimeOffset.Parse("2024-09-20T12:34:56.7890123Z"),
					AccountModified = DateTimeOffset.Parse("2024-09-21T10:00:25.5461402Z"),
				},
				Items =
				[
					new() { Id = "382bb7cd-f9e4-4906-874e-ab88df954fa8", Level = 123, Path = JsonPath.Create("foo.bar") },
					new() { Id = "8092e57d-16b4-4afb-ae04-28acbeb22aa8", Level = 456, Path = JsonPath.Create("bars[2].foo"), Disabled = true }
				],
				Devices = new()
				{
					["Foo"] = new() { Id = "Foo", Model = "ACME Ultra Core 9100XX Ultra Series" },
					["Bar"] = new() { Id = "Bar", Model = "iHAL 42 Pro Ultra MaXX" },
				},
			};

			var stjOps = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);

			// the same payload feeds several deserializers: numeric enums are the output that all of them read without
			// extra configuration. The document is camelCase (Web container), so the REFLECTION legs read it with
			// case-insensitive matching - a case-sensitive read would bind nothing (and throws on the `required` Id)
			var json = CrystalJson.Serialize(user, GeneratedConverters.MyAwesomeUser.Default, CrystalJsonSettings.Json.WithEnumAsNumbers());
			var parsed = JsonObject.Parse(json, CrystalJsonSettings.JsonIgnoreCase);

			// warmup
			{
				_ = JsonValue.Parse(CrystalJson.Serialize(user)).As<MyAwesomeUser>();
				_ = GeneratedConverters.MyAwesomeUser.Unpack(JsonValue.Parse(CrystalJson.Serialize(user, GeneratedConverters.MyAwesomeUser.Default)));
				_ = CrystalJson.Deserialize<MyAwesomeUser>(json, CrystalJsonSettings.JsonIgnoreCase);
				_ = System.Text.Json.JsonSerializer.Deserialize<MyAwesomeUser>(json, stjOps);
				_ = System.Text.Json.JsonSerializer.Deserialize<MyAwesomeUser>(json, SystemTextJsonGeneratedSerializers.Default.MyAwesomeUser);
			}

			// the benchmark only measures what actually runs: prove the reflection leg BINDS the document
			Assert.That(CrystalJson.Deserialize<MyAwesomeUser>(json, CrystalJsonSettings.JsonIgnoreCase).Id, Is.EqualTo(user.Id), "the reflection leg must bind the camelCase document");

			Log($"JSON: {json.Length:N0} chars");

#if DEBUG
			const int RUNS = 25;
			const int ITERATIONS = 1_000;
#else
			const int RUNS = 50;
			const int ITERATIONS = 10_000;
#endif

			static void Report(string label, RobustBenchmark.Report<long> report)
			{
				Log($"* {label,-23}: {report.IterationsPerRun,7:N0} in {report.BestDuration.TotalMilliseconds,8:F1} ms at {report.BestIterationsPerSecond,10:N0} op/s ({report.BestIterationsNanos,9:N0} nanos), {(report.GcAllocatedOnThread / (report.NumberOfRuns * report.IterationsPerRun)),9:N0} allocated");
			}

			{
				var report = RobustBenchmark.Run(() => System.Text.Json.JsonSerializer.Serialize(user, stjOps), RUNS, ITERATIONS);
				Report("SERIALIZE TEXT STJ_DYN", report);
			}
			{
				var report = RobustBenchmark.Run(() => System.Text.Json.JsonSerializer.Serialize(user, SystemTextJsonGeneratedSerializers.Default.MyAwesomeUser), RUNS, ITERATIONS);
				Report("SERIALIZE TEXT STJ_GEN", report);
			}
			{
				var report = RobustBenchmark.Run(() => CrystalJson.Serialize(user), RUNS, ITERATIONS);
				Report("SERIALIZE TEXT CRY_DYN", report);
			}
			{
				var report = RobustBenchmark.Run(() => CrystalJson.Serialize(user, GeneratedConverters.MyAwesomeUser.Default), RUNS, ITERATIONS);
				Report("SERIALIZE TEXT CRY_GEN", report);
			}
			{
				var report = RobustBenchmark.Run(() => CrystalJson.ToSlice(user), RUNS, ITERATIONS);
				Report("SERIALIZE UTF8 CRY_DYN", report);
			}
			{
				var report = RobustBenchmark.Run(() => CrystalJson.ToSlice(user, GeneratedConverters.MyAwesomeUser.Default), RUNS, ITERATIONS);
				Report("SERIALIZE UTF8 CRY_GEN", report);
			}
			{
				var report = RobustBenchmark.Run(() =>
				{
					using var res = CrystalJson.ToSlice(user, GeneratedConverters.MyAwesomeUser.Default, ArrayPool<byte>.Shared);
					// use the JSON here to do something!
				}, RUNS, ITERATIONS);
				Report("SERIALIZE UTF8 POOLED", report);
			}

			{
				var report = RobustBenchmark.Run(() => System.Text.Json.JsonSerializer.Deserialize<MyAwesomeUser>(json, stjOps), RUNS, ITERATIONS);
				Report("DESERIALIZE STJ_DYN", report);
			}
			{
				var report = RobustBenchmark.Run(() => System.Text.Json.JsonSerializer.Deserialize<MyAwesomeUser>(json, SystemTextJsonGeneratedSerializers.Default.MyAwesomeUser), RUNS, ITERATIONS);
				Report("DESERIALIZE STJ_GEN", report);
			}
			{
				var report = RobustBenchmark.Run(() => CrystalJson.Deserialize<MyAwesomeUser>(json, CrystalJsonSettings.JsonIgnoreCase), RUNS, ITERATIONS);
				Report("DESERIALIZE RUNTIME", report);
			}
			{
				var report = RobustBenchmark.Run(() => GeneratedConverters.MyAwesomeUser.Unpack(JsonValue.Parse(json)), RUNS, ITERATIONS);
				Report("DESERIALIZE CODEGEN", report);
			}

			{
				var report = RobustBenchmark.Run(() => parsed.As<MyAwesomeUser>(), RUNS, ITERATIONS);
				Report("AS<T> RUNTIME", report);
			}
			{
				var report = RobustBenchmark.Run(() => GeneratedConverters.MyAwesomeUser.Unpack(parsed), RUNS, ITERATIONS);
				Report("AS<T> CODEGEN", report);
			}

			{
				var report = RobustBenchmark.Run(() => JsonValue.FromValue<MyAwesomeUser>(user), RUNS, ITERATIONS);
				Report("PACK RUNTIME", report);
			}
			{
				var report = RobustBenchmark.Run(() => GeneratedConverters.MyAwesomeUser.Pack(user), RUNS, ITERATIONS);
				Report("PACK CODEGEN", report);
			}

		}

	}

}
