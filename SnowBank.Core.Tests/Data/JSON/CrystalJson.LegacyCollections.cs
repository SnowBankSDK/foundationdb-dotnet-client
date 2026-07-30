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
	using System.Collections.ObjectModel;

	/// <summary>Pins support for the legacy <see cref="Collection{T}"/> family (<c>Collection&lt;T&gt;</c>, <c>ObservableCollection&lt;T&gt;</c>,
	/// user subclasses): DCJS-era DTOs use these instead of <c>List&lt;T&gt;</c>, and both directions must work on every route
	/// (runtime serialize/deserialize, DOM pack/bind, and the fallback path emitted by the source generator).</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	[SetInvariantCulture]
	public sealed class CrystalJsonLegacyCollectionsFacts : SimpleTest
	{

		public sealed class OrderDto
		{
			public int Id { get; set; }

			public double Total { get; set; }
		}

		/// <summary>User subclass of <see cref="Collection{T}"/>, a common DCJS-era shape (<c>class DeviceList : Collection&lt;string&gt;</c>)</summary>
		public sealed class DeviceList : Collection<string>
		{
		}

		public sealed class StoreDto
		{
			public string? Name { get; set; }

			public Collection<int>? Codes { get; set; }

			public Collection<string?>? Tags { get; set; }

			public Collection<OrderDto>? Orders { get; set; }

			public ObservableCollection<int>? Watched { get; set; }

			public DeviceList? Devices { get; set; }
		}

		/// <summary>Getter-only collection property, populated by the compiled adder (<c>obj.Items.Add(..)</c>) instead of a setter</summary>
		public sealed class AppendOnlyDto
		{
			public Collection<int> Items { get; } = [ ];
		}

		private static StoreDto MakeStore() => new()
		{
			Name = "store-01",
			Codes = [ 1, 2, 3 ],
			Tags = [ "red", null, "blue" ],
			Orders = [ new() { Id = 7, Total = 12.5 }, new() { Id = 8, Total = 0 } ],
			Watched = [ 4, 5 ],
			Devices = [ "printer", "scanner" ],
		};

		[Test]
		public void Test_Serialize_Collections_As_Json_Arrays()
		{
			// a Collection<T> (or subclass) must serialize like the equivalent List<T>: a plain JSON array

			Assert.That(CrystalJson.Serialize(new Collection<int> { 1, 2, 3 }), Is.EqualTo("[ 1, 2, 3 ]"));
			Assert.That(CrystalJson.Serialize(new ObservableCollection<string> { "a", "b" }), Is.EqualTo("""[ "a", "b" ]"""));
			Assert.That(CrystalJson.Serialize(new DeviceList { "printer" }), Is.EqualTo("""[ "printer" ]"""));

			var obj = CrystalJson.Parse(CrystalJson.Serialize(MakeStore())).AsObject();
			using (Assert.EnterMultipleScope())
			{
				Assert.That(obj["Codes"], IsJson.Array.And.EqualTo((int[]) [ 1, 2, 3 ]));
				Assert.That(obj["Tags"], IsJson.Array);
				Assert.That(obj["Orders"], IsJson.Array.And.OfSize(2));
				Assert.That(obj["Orders"][0]["Id"], IsJson.EqualTo(7));
				Assert.That(obj["Watched"], IsJson.Array.And.EqualTo((int[]) [ 4, 5 ]));
				Assert.That(obj["Devices"], IsJson.Array.And.EqualTo((string[]) [ "printer", "scanner" ]));
			}
		}

		[Test]
		public void Test_Pack_Collections_Into_Dom()
		{
			// JsonValue.FromValue(..) must produce a JsonArray, not an object with a "Count" field

			using (Assert.EnterMultipleScope())
			{
				Assert.That(JsonValue.FromValue(new Collection<int> { 1, 2, 3 }), IsJson.Array.And.EqualTo((int[]) [ 1, 2, 3 ]));
				Assert.That(JsonValue.FromValue(new ObservableCollection<int> { 4, 5 }), IsJson.Array.And.EqualTo((int[]) [ 4, 5 ]));
				Assert.That(JsonValue.FromValue(new DeviceList { "printer" }), IsJson.Array.And.EqualTo((string[]) [ "printer" ]));
				Assert.That(JsonValue.FromValue(MakeStore())["Codes"], IsJson.Array.And.EqualTo((int[]) [ 1, 2, 3 ]));
			}
		}

		[Test]
		public void Test_Bind_Json_Array_To_Collection_Types()
		{
			// the DOM route: As<TCollection>() / Bind(typeof(TCollection)) on a JsonArray

			var arr = JsonArray.Create(1, 2, 3);

			var col = arr.As<Collection<int>>();
			Assert.That(col, Is.Not.Null.And.InstanceOf<Collection<int>>());
			Assert.That(col, Is.EqualTo((int[]) [ 1, 2, 3 ]));

			var obs = arr.As<ObservableCollection<int>>();
			Assert.That(obs, Is.Not.Null.And.InstanceOf<ObservableCollection<int>>());
			Assert.That(obs, Is.EqualTo((int[]) [ 1, 2, 3 ]));

			var devices = JsonArray.Create("printer", "scanner").As<DeviceList>();
			Assert.That(devices, Is.Not.Null.And.InstanceOf<DeviceList>());
			Assert.That(devices, Is.EqualTo((string[]) [ "printer", "scanner" ]));

			var boxed = arr.Bind(typeof(Collection<int>));
			Assert.That(boxed, Is.InstanceOf<Collection<int>>());
			Assert.That(boxed, Is.EqualTo((int[]) [ 1, 2, 3 ]));

			// null-like stays null
			Assert.That(JsonNull.Null.As<Collection<int>>(), Is.Null);
		}

		[Test]
		public void Test_Deserialize_Collections_At_Top_Level()
		{
			var col = CrystalJson.Deserialize<Collection<int>>("[ 1, 2, 3 ]");
			Assert.That(col, Is.Not.Null.And.InstanceOf<Collection<int>>());
			Assert.That(col, Is.EqualTo((int[]) [ 1, 2, 3 ]));

			var devices = CrystalJson.Deserialize<DeviceList>("""[ "printer" ]""");
			Assert.That(devices, Is.Not.Null.And.InstanceOf<DeviceList>());
			Assert.That(devices, Is.EqualTo((string[]) [ "printer" ]));
		}

		[Test]
		public void Test_Deserialize_Dto_With_Collection_Members()
		{
			var json = CrystalJson.Serialize(MakeStore());
			var dto = CrystalJson.Deserialize<StoreDto>(json);

			using (Assert.EnterMultipleScope())
			{
				Assert.That(dto.Name, Is.EqualTo("store-01"));
				Assert.That(dto.Codes, Is.Not.Null.And.InstanceOf<Collection<int>>());
				Assert.That(dto.Codes, Is.EqualTo((int[]) [ 1, 2, 3 ]));
				Assert.That(dto.Tags, Is.Not.Null.And.InstanceOf<Collection<string?>>());
				Assert.That(dto.Tags, Is.EqualTo((string?[]) [ "red", null, "blue" ]));
				Assert.That(dto.Orders, Is.Not.Null.And.InstanceOf<Collection<OrderDto>>());
				Assert.That(dto.Orders![0].Id, Is.EqualTo(7));
				Assert.That(dto.Orders![0].Total, Is.EqualTo(12.5));
				Assert.That(dto.Orders![1].Id, Is.EqualTo(8));
				Assert.That(dto.Watched, Is.Not.Null.And.InstanceOf<ObservableCollection<int>>());
				Assert.That(dto.Watched, Is.EqualTo((int[]) [ 4, 5 ]));
				Assert.That(dto.Devices, Is.Not.Null.And.InstanceOf<DeviceList>());
				Assert.That(dto.Devices, Is.EqualTo((string[]) [ "printer", "scanner" ]));
			}

			// missing/null members stay null
			var empty = CrystalJson.Deserialize<StoreDto>("""{ "Name": "empty", "Codes": null }""");
			using (Assert.EnterMultipleScope())
			{
				Assert.That(empty.Codes, Is.Null);
				Assert.That(empty.Orders, Is.Null);
			}
		}

		/// <summary>User subclass of <see cref="List{T}"/>, another common DCJS-era shape</summary>
		public sealed class ProductList : List<OrderDto>
		{
		}

		[Test]
		public void Test_Bind_To_User_Subclass_Of_List()
		{
			// a member declared as a List<T> subclass must receive an instance of the SUBCLASS, not a plain List<T>
			// (which would not be assignable, and would silently degrade to null through a lenient setter)

			var arr = CrystalJson.Parse("""[ { "Id": 7, "Total": 12.5 } ]""").AsArray();

			var products = arr.As<ProductList>();
			Assert.That(products, Is.Not.Null.And.InstanceOf<ProductList>());
			Assert.That(products![0].Id, Is.EqualTo(7));

			var roundtrip = CrystalJson.Deserialize<ProductList>(CrystalJson.Serialize(new ProductList { new() { Id = 8, Total = 1 } }));
			Assert.That(roundtrip, Is.InstanceOf<ProductList>());
			Assert.That(roundtrip[0].Id, Is.EqualTo(8));
		}

		[Test]
		public void Test_Deserialize_GetterOnly_Collection_Via_Adder()
		{
			// no setter: the binder must append into the collection created by the ctor

			var dto = CrystalJson.Deserialize<AppendOnlyDto>("""{ "Items": [ 10, 20, 30 ] }""");
			Assert.That(dto.Items, Is.EqualTo((int[]) [ 10, 20, 30 ]));
		}

	}

}
