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
	using System.Collections.Concurrent;
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

		/// <summary>User subclass of <see cref="ObservableCollection{T}"/></summary>
		public sealed class WatchList : ObservableCollection<int>
		{
		}

		public sealed class Device
		{
			public string? Serial { get; set; }

			public string? Model { get; set; }
		}

		/// <summary>User subclass of <see cref="KeyedCollection{TKey,TItem}"/>: the by-key index is derived from the items themselves</summary>
		public sealed class DeviceIndex : KeyedCollection<string, Device>
		{
			protected override string GetKeyForItem(Device item) => item.Serial!;
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
		public void Test_ObservableCollection_All_Routes()
		{
			// serialize + DOM pack, declared type and user subclass
			Assert.That(CrystalJson.Serialize(new ObservableCollection<int> { 1, 2, 3 }), Is.EqualTo("[ 1, 2, 3 ]"));
			Assert.That(JsonValue.FromValue(new WatchList { 4, 5 }), IsJson.Array.And.EqualTo((int[]) [ 4, 5 ]));

			// top-level deserialize
			var obs = CrystalJson.Deserialize<ObservableCollection<int>>("[ 1, 2, 3 ]");
			Assert.That(obs, Is.InstanceOf<ObservableCollection<int>>().And.EqualTo((int[]) [ 1, 2, 3 ]));

			// user subclass must receive an instance of the subclass
			var watch = CrystalJson.Deserialize<WatchList>("[ 4, 5 ]");
			Assert.That(watch, Is.InstanceOf<WatchList>().And.EqualTo((int[]) [ 4, 5 ]));

			// the deserialized instance is a real live ObservableCollection: change notifications still fire
			int events = 0;
			obs.CollectionChanged += (_, _) => events++;
			obs.Add(4);
			Assert.That(events, Is.EqualTo(1));
		}

		[Test]
		public void Test_KeyedCollection_All_Routes()
		{
			var devices = new DeviceIndex
			{
				new() { Serial = "X1", Model = "laser" },
				new() { Serial = "X2", Model = "inkjet" },
			};

			// serialize: an array of the items (never a dictionary), like the legacy serializer
			var json = CrystalJson.Serialize(devices);
			var arr = CrystalJson.Parse(json).AsArray();
			Assert.That(arr, Has.Count.EqualTo(2));
			Assert.That(arr[0]["Serial"], IsJson.EqualTo("X1"));
			Assert.That(arr[1]["Model"], IsJson.EqualTo("inkjet"));

			// DOM pack
			Assert.That(JsonValue.FromValue(devices), IsJson.Array.And.OfSize(2));

			// deserialize: rebuilt through Add(), so GetKeyForItem ran and the by-key indexer works again
			var decoded = CrystalJson.Deserialize<DeviceIndex>(json);
			Assert.That(decoded, Is.InstanceOf<DeviceIndex>());
			Assert.That(decoded, Has.Count.EqualTo(2));
			Assert.That(decoded["X2"].Model, Is.EqualTo("inkjet"));

			// DOM bind
			var bound = arr.As<DeviceIndex>();
			Assert.That(bound, Is.Not.Null);
			Assert.That(bound!["X1"].Model, Is.EqualTo("laser"));

			// duplicate keys in the output must fail loudly, never silently drop an item
			Assert.That(() => CrystalJson.Deserialize<DeviceIndex>("""[ { "Serial": "X1" }, { "Serial": "X1" } ]"""), Throws.Exception);
		}

		[Test]
		public void Test_Deserialize_GetterOnly_Collection_Via_Adder()
		{
			// no setter: the binder must append into the collection created by the ctor

			var dto = CrystalJson.Deserialize<AppendOnlyDto>("""{ "Items": [ 10, 20, 30 ] }""");
			Assert.That(dto.Items, Is.EqualTo((int[]) [ 10, 20, 30 ]));
		}

		[Test]
		public void Test_Queues_And_Stacks_Roundtrip()
		{
			// queues serialize front-first, and round-trip in the same order
			var queue = CrystalJson.Deserialize<Queue<int>>("[ 1, 2, 3 ]");
			Assert.That(queue, Is.InstanceOf<Queue<int>>().And.EqualTo((int[]) [ 1, 2, 3 ]));
			Assert.That(queue.Dequeue(), Is.EqualTo(1), "the output order is the dequeue order");
			Assert.That(CrystalJson.Serialize(new Queue<int>([ 1, 2, 3 ])), Is.EqualTo("[ 1, 2, 3 ]"));

			// stacks serialize top-first, and the round-trip PRESERVES the output (unlike legacy serializers that reversed it)
			var stack = new Stack<int>();
			stack.Push(1); stack.Push(2); stack.Push(3);
			var json = CrystalJson.Serialize(stack);
			Assert.That(json, Is.EqualTo("[ 3, 2, 1 ]"), "top of the stack comes first in the output");
			var stack2 = CrystalJson.Deserialize<Stack<int>>(json);
			Assert.That(stack2.Peek(), Is.EqualTo(3), "the first output element is the top of the stack");
			Assert.That(CrystalJson.Serialize(stack2), Is.EqualTo(json), "round-trip preserves the order");

			// concurrent variants follow the same rules; a bag preserves the CONTENT (it is unordered by contract)
			Assert.That(CrystalJson.Serialize(CrystalJson.Deserialize<ConcurrentQueue<int>>("[ 1, 2, 3 ]")), Is.EqualTo("[ 1, 2, 3 ]"));
			Assert.That(CrystalJson.Serialize(CrystalJson.Deserialize<ConcurrentStack<int>>("[ 3, 2, 1 ]")), Is.EqualTo("[ 3, 2, 1 ]"));
			Assert.That(CrystalJson.Deserialize<ConcurrentBag<int>>("[ 1, 2, 3 ]"), Is.EquivalentTo((int[]) [ 1, 2, 3 ]));
		}

		[Test]
		public void Test_Linked_And_Sorted_Collections_Roundtrip()
		{
			var linked = CrystalJson.Deserialize<LinkedList<int>>("[ 1, 2, 3 ]");
			Assert.That(linked, Is.InstanceOf<LinkedList<int>>().And.EqualTo((int[]) [ 1, 2, 3 ]));

			var sorted = CrystalJson.Deserialize<SortedSet<int>>("[ 3, 1, 2 ]");
			Assert.That(sorted, Is.InstanceOf<SortedSet<int>>().And.EqualTo((int[]) [ 1, 2, 3 ]));

			var immutableSorted = CrystalJson.Deserialize<System.Collections.Immutable.ImmutableSortedSet<int>>("[ 3, 1, 2 ]");
			Assert.That(immutableSorted, Is.InstanceOf<System.Collections.Immutable.ImmutableSortedSet<int>>().And.EqualTo((int[]) [ 1, 2, 3 ]));
		}

		public sealed class MyStringIntDict : Dictionary<string, int>
		{
		}

		[Test]
		public void Test_Dictionary_Family_Roundtrip()
		{
			var sorted = CrystalJson.Deserialize<SortedDictionary<string, int>>("""{ "b": 2, "a": 1 }""");
			Assert.That(sorted, Is.InstanceOf<SortedDictionary<string, int>>());
			Assert.That(sorted["a"], Is.EqualTo(1));

			var concurrent = CrystalJson.Deserialize<ConcurrentDictionary<string, int>>("""{ "a": 1 }""");
			Assert.That(concurrent, Is.InstanceOf<ConcurrentDictionary<string, int>>());
			Assert.That(concurrent["a"], Is.EqualTo(1));

			// no parameterless ctor: bound through an inner Dictionary<K,V> then wrapped
			var ro = CrystalJson.Deserialize<ReadOnlyDictionary<string, int>>("""{ "a": 1 }""");
			Assert.That(ro, Is.InstanceOf<ReadOnlyDictionary<string, int>>());
			Assert.That(ro["a"], Is.EqualTo(1));

			// a user subclass must receive an instance of the subclass
			var custom = CrystalJson.Deserialize<MyStringIntDict>("""{ "a": 1, "b": 2 }""");
			Assert.That(custom, Is.InstanceOf<MyStringIntDict>());
			Assert.That(custom["b"], Is.EqualTo(2));
		}

		[Test]
		public void Test_NonGeneric_Collections_Roundtrip()
		{
			// Hashtable binds from a JSON object (keys are the member names, values are CLR objects)...
			var table = CrystalJson.Deserialize<System.Collections.Hashtable>("""{ "a": 1, "b": "two" }""");
			Assert.That(table.Count, Is.EqualTo(2));
			Assert.That(table["a"], Is.EqualTo(1));
			Assert.That(table["b"], Is.EqualTo("two"));
			Assert.That(CrystalJson.Serialize(table), Is.EqualTo("""{ "a": 1, "b": "two" }""").Or.EqualTo("""{ "b": "two", "a": 1 }"""));

			// ... and also accepts the legacy DCJS output shape [ { "Key": .., "Value": .. } ]
			var fromPairs = CrystalJson.Deserialize<System.Collections.Hashtable>("""[ { "Key": "a", "Value": 1 } ]""");
			Assert.That(fromPairs["a"], Is.EqualTo(1));

			// ArrayList: elements are bound as CLR objects
			var list = CrystalJson.Deserialize<System.Collections.ArrayList>("""[ 1, "two", 3 ]""");
			Assert.That(list, Is.InstanceOf<System.Collections.ArrayList>());
			Assert.That(list.Count, Is.EqualTo(3));
			Assert.That(list[1], Is.EqualTo("two"));

			// StringCollection: pre-generics collection, recognized through its public Add(string)
			var strings = CrystalJson.Deserialize<System.Collections.Specialized.StringCollection>("""[ "a", "b" ]""");
			Assert.That(strings, Is.InstanceOf<System.Collections.Specialized.StringCollection>());
			Assert.That(strings.Count, Is.EqualTo(2));
			Assert.That(strings[0], Is.EqualTo("a"));
		}

		/// <summary>Collection-initializer duck typing: IEnumerable&lt;T&gt; + public Add(T), but NOT ICollection&lt;T&gt;</summary>
		public sealed class DuckBag : System.Collections.IEnumerable, IEnumerable<int>
		{
			private List<int> Store { get; } = [ ];

			public void Add(int item) => this.Store.Add(item);

			public IEnumerator<int> GetEnumerator() => this.Store.GetEnumerator();

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => this.Store.GetEnumerator();
		}

		[Test]
		public void Test_DuckTyped_Add_Collection_Roundtrip()
		{
			// the same duck-typing rule the C# compiler uses for collection initializers (and DataContractSerializer honors)
			var bag = CrystalJson.Deserialize<DuckBag>("[ 1, 2, 3 ]");
			Assert.That(bag, Is.InstanceOf<DuckBag>().And.EqualTo((int[]) [ 1, 2, 3 ]));
		}

		public sealed class InterfaceMembersDto
		{
			public ISet<int>? Set { get; set; }

#if NET5_0_OR_GREATER
			// IReadOnlySet<T> does not exist on net472/netstandard2.0
			public IReadOnlySet<int>? RoSet { get; set; }
#endif

			public IReadOnlyList<int>? RoList { get; set; }

			public IDictionary<string, int>? Dict { get; set; }

			public IReadOnlyDictionary<string, int>? RoDict { get; set; }
		}

		[Test]
		public void Test_Interface_Declared_Members_Roundtrip()
		{
			var dto = new InterfaceMembersDto
			{
				Set = new HashSet<int> { 1, 2 },
#if NET5_0_OR_GREATER
				RoSet = new HashSet<int> { 3, 4 },
#endif
				RoList = [ 5, 6 ],
				Dict = new Dictionary<string, int> { ["a"] = 1 },
				RoDict = new Dictionary<string, int> { ["b"] = 2 },
			};

			var back = CrystalJson.Deserialize<InterfaceMembersDto>(CrystalJson.Serialize(dto));
			using (Assert.EnterMultipleScope())
			{
				Assert.That(back.Set, Is.EquivalentTo((int[]) [ 1, 2 ]));
#if NET5_0_OR_GREATER
				Assert.That(back.RoSet, Is.EquivalentTo((int[]) [ 3, 4 ]), "must bind to a type that implements IReadOnlySet<T>");
#endif
				Assert.That(back.RoList, Is.EqualTo((int[]) [ 5, 6 ]));
				Assert.That(back.Dict, Is.Not.Null);
				Assert.That(back.Dict!["a"], Is.EqualTo(1));
				Assert.That(back.RoDict, Is.Not.Null);
				Assert.That(back.RoDict!["b"], Is.EqualTo(2));
			}
		}

		[Test]
		public void Test_Unsupported_Shapes_Fail_Loudly()
		{
			// multi-dimensional arrays have no JSON representation: refuse on every route, never flatten or null out
			var grid = new int[,] { { 1, 2 }, { 3, 4 } };
			Assert.That(() => CrystalJson.Serialize(grid), Throws.InstanceOf<JsonSerializationException>());
			Assert.That(() => JsonValue.FromValue(grid), Throws.InstanceOf<JsonSerializationException>());
			Assert.That(() => CrystalJson.Deserialize<int[,]>("[ 1, 2, 3, 4 ]"), Throws.InstanceOf<JsonBindingException>());

			// jagged arrays are the supported spelling
			Assert.That(CrystalJson.Serialize(new int[][] { [ 1, 2 ], [ 3, 4 ] }), Is.EqualTo("[ [ 1, 2 ], [ 3, 4 ] ]"));
			Assert.That(CrystalJson.Deserialize<int[][]>("[ [ 1, 2 ], [ 3, 4 ] ]")[1][0], Is.EqualTo(3));

			// NameValueCollection enumerates its keys only: serializing it would silently drop the values, so it is refused
			var nvc = new System.Collections.Specialized.NameValueCollection { ["k"] = "v" };
			Assert.That(() => CrystalJson.Serialize(nvc), Throws.InstanceOf<JsonSerializationException>());
			Assert.That(() => JsonValue.FromValue(nvc), Throws.InstanceOf<JsonSerializationException>());
		}

	}

}
