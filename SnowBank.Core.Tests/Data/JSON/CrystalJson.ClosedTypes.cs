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

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedMember.Global
namespace SnowBank.Data.Json.Tests
{
	using System.Reflection;

	/// <summary>Holds JSON DOM values as members.</summary>
	public sealed class JsonValueHolder
	{
		public JsonValue? Any { get; set; }
		public JsonObject? Obj { get; set; }
		public JsonArray? Arr { get; set; }
	}

	public partial class CrystalJsonTest
	{

		[Test]
		public void Test_JsonValue_Is_Closed_And_Output_Is_Unchanged()
		{
			// JsonValue is a closed class with exactly seven subtypes
			var attr = typeof(JsonValue).GetCustomAttributesData().Single(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.IsClosedTypeAttribute");
			var derived = ((IReadOnlyCollection<CustomAttributeTypedArgument>) attr.NamedArguments.Single(a => a.MemberName == "DerivedTypes").TypedValue.Value!).Select(x => (Type) x.Value!).ToArray();
			Assert.That(derived, Is.EquivalentTo(new[] { typeof(JsonNull), typeof(JsonBoolean), typeof(JsonString), typeof(JsonNumber), typeof(JsonDateTime), typeof(JsonArray), typeof(JsonObject) }));

			// JsonValue carries no [JsonPolymorphic]: no discriminator on any path
			var holder = new JsonValueHolder
			{
				Any = JsonNumber.Return(42),
				Obj = JsonObject.Create([ ("hello", "world"), ("n", 1) ]),
				Arr = JsonArray.Create("a", 1, true),
			};
			var json = CrystalJson.Serialize(holder, CrystalJsonSettings.JsonCompact);
			Assert.That(json, Is.EqualTo("""{"Any":42,"Obj":{"hello":"world","n":1},"Arr":["a",1,true]}"""));
			Assert.That(CrystalJson.Serialize<JsonValue>(holder.Obj, CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"hello":"world","n":1}"""));
			Assert.That(JsonValue.FromValue(holder).ToJsonText(CrystalJsonSettings.JsonCompact), Is.EqualTo(json));

			var copy = CrystalJson.Deserialize<JsonValueHolder>(json);
			Assert.That(copy.Any, IsJson.EqualTo(42));
			Assert.That(copy.Obj, IsJson.EqualTo(holder.Obj));
			Assert.That(copy.Arr, IsJson.EqualTo(holder.Arr));
		}

	}

}

// JsonPolymorphicAttribute.InferClosedTypePolymorphism ships in System.Text.Json 11, the version of the net11.0 runtime,
// and a closed class in this assembly needs IsClosedTypeAttribute, which the older targets declare as internal to SnowBank.Core.
// Guard the closed hierarchies to net11.0.
#if NET11_0_OR_GREATER

namespace SnowBank.Data.Json.Tests
{
	using STJ = System.Text.Json;

	#region Closed hierarchies...

	/// <summary>Event of a shopping cart: a closed hierarchy, with derived types inferred from <c>closed</c>.</summary>
	[STJ.Serialization.JsonPolymorphic(InferClosedTypePolymorphism = true)]
	public closed class ClosedCartEvent
	{
		public string? CartId { get; set; }
	}

	public sealed class CartCreated : ClosedCartEvent
	{
		public string? Owner { get; set; }
	}

	public sealed class ItemAdded : ClosedCartEvent
	{
		public string? Sku { get; set; }
		public int Quantity { get; set; }
	}

	public sealed class CartCheckedOut : ClosedCartEvent
	{
		public decimal Total { get; set; }
	}

	/// <summary>Holds a closed hierarchy as a member and as a list of members.</summary>
	public sealed class ClosedCartHistory
	{
		public ClosedCartEvent? Last { get; set; }
		public List<ClosedCartEvent>? Events { get; set; }
	}

	/// <summary>Root of a closed hierarchy with a nested closed hierarchy.</summary>
	[STJ.Serialization.JsonPolymorphic(InferClosedTypePolymorphism = true)]
	public closed class ClosedShape
	{
		public string? Label { get; set; }
	}

	/// <summary>Nested closed hierarchy: replaced by its own derived types in the list of <see cref="ClosedShape"/>.</summary>
	public closed class ClosedPolygon : ClosedShape
	{
		public int Sides { get; set; }
	}

	public sealed class ClosedTriangle : ClosedPolygon
	{
		public double Base { get; set; }
	}

	public sealed class ClosedSquare : ClosedPolygon
	{
		public double Side { get; set; }
	}

	public sealed class ClosedCircle : ClosedShape
	{
		public double Radius { get; set; }
	}

	/// <summary>Holds a member typed with the nested closed hierarchy.</summary>
	public sealed class ClosedPolygonHolder
	{
		public ClosedPolygon? Polygon { get; set; }
	}

	/// <summary>The opt-in with a custom discriminator property name.</summary>
	[STJ.Serialization.JsonPolymorphic(InferClosedTypePolymorphism = true, TypeDiscriminatorPropertyName = "kind")]
	public closed class ClosedCommand
	{
	}

	public sealed class StartCommand : ClosedCommand
	{
		public int Delay { get; set; }
	}

	public sealed class StopCommand : ClosedCommand
	{
		public bool Force { get; set; }
	}

	/// <summary>Explicit registrations replace the inferred list.</summary>
	[STJ.Serialization.JsonPolymorphic(InferClosedTypePolymorphism = true)]
	[STJ.Serialization.JsonDerivedType(typeof(ExplicitLeafA), "a")]
	public closed class ClosedWithExplicitTypes
	{
	}

	public sealed class ExplicitLeafA : ClosedWithExplicitTypes
	{
		public int Value { get; set; }
	}

	public sealed class ExplicitLeafB : ClosedWithExplicitTypes
	{
		public int Value { get; set; }
	}

	/// <summary>The opt-in on a class that is not closed.</summary>
	[STJ.Serialization.JsonPolymorphic(InferClosedTypePolymorphism = true)]
	public abstract class NotClosedBase
	{
	}

	public sealed class NotClosedLeaf : NotClosedBase
	{
		public int Value { get; set; }
	}

	/// <summary>The opt-in on a class that is not closed, with explicit registrations.</summary>
	[STJ.Serialization.JsonPolymorphic(InferClosedTypePolymorphism = true)]
	[STJ.Serialization.JsonDerivedType(typeof(NotClosedExplicitLeaf), "leaf")]
	public abstract class NotClosedExplicitBase
	{
	}

	public sealed class NotClosedExplicitLeaf : NotClosedExplicitBase
	{
		public int Value { get; set; }
	}

	/// <summary>A closed class without the opt-in.</summary>
	public closed class ClosedWithoutOptIn
	{
		public int Common { get; set; }
	}

	public sealed class PlainClosedLeaf : ClosedWithoutOptIn
	{
		public int Value { get; set; }
	}

	/// <summary>A closed class that opts out explicitly.</summary>
	[STJ.Serialization.JsonPolymorphic(InferClosedTypePolymorphism = false)]
	public closed class ClosedOptedOut
	{
	}

	public sealed class OptedOutLeaf : ClosedOptedOut
	{
		public int Value { get; set; }
	}

	/// <summary>An abstract class with the same members as <see cref="ClosedWithoutOptIn"/>, but not closed.</summary>
	public abstract class OpenTwinBase
	{
		public int Common { get; set; }
	}

	public sealed class OpenTwinLeaf : OpenTwinBase
	{
		public int Value { get; set; }
	}

	/// <summary>A generic closed hierarchy.</summary>
	[STJ.Serialization.JsonPolymorphic(InferClosedTypePolymorphism = true)]
	public closed class ClosedResult<T>
	{
	}

	public sealed class ClosedSuccess<T> : ClosedResult<T>
	{
		public T? Value { get; set; }
	}

	public sealed class ClosedIntFailure : ClosedResult<int>
	{
		public string? Error { get; set; }
	}

	/// <summary>A derived type that uses one type parameter for both type arguments of the base.</summary>
	[STJ.Serialization.JsonPolymorphic(InferClosedTypePolymorphism = true)]
	public closed class ClosedPair<TFirst, TSecond>
	{
	}

	public sealed class ClosedSamePair<T> : ClosedPair<T, T>
	{
		public T? First { get; set; }
	}

	/// <summary>A derived type that binds its type parameter through an array type argument of the base.</summary>
	[STJ.Serialization.JsonPolymorphic(InferClosedTypePolymorphism = true)]
	public closed class ClosedBag<T>
	{
	}

	public sealed class ClosedArrayBag<T> : ClosedBag<T[]>
	{
		public T[]? Items { get; set; }
	}

	/// <summary>A derived type that applies only to the constructions of the root over a list type.</summary>
	[STJ.Serialization.JsonPolymorphic(InferClosedTypePolymorphism = true)]
	public closed class ClosedGap<T>
	{
	}

	public sealed class ClosedGapA<T> : ClosedGap<T>
	{
		public T? Value { get; set; }
	}

	public sealed class ClosedGapB<U> : ClosedGap<List<U>>
	{
		public int Count { get; set; }
	}

	/// <summary>A derived type with a generic constraint.</summary>
	[STJ.Serialization.JsonPolymorphic(InferClosedTypePolymorphism = true)]
	public closed class ClosedBox<T>
	{
	}

	public sealed class ClosedStructBox<T> : ClosedBox<T> where T : struct
	{
		public T Value { get; set; }
	}

	public sealed class ClosedAnyBox<T> : ClosedBox<T>
	{
		public T? Value { get; set; }
	}

	/// <summary>A derived type with a constraint that names another type parameter.</summary>
	[STJ.Serialization.JsonPolymorphic(InferClosedTypePolymorphism = true)]
	public closed class ClosedLink<TFrom, TTo>
	{
	}

	public sealed class ClosedUpcast<TFrom, TTo> : ClosedLink<TFrom, TTo> where TFrom : TTo
	{
		public TFrom? From { get; set; }
	}

	public sealed class ClosedAnyLink<TFrom, TTo> : ClosedLink<TFrom, TTo>
	{
		public TTo? To { get; set; }
	}

	/// <summary>Derived types bound through a type nested in a generic type.</summary>
	[STJ.Serialization.JsonPolymorphic(InferClosedTypePolymorphism = true)]
	public closed class ClosedSlot<T>
	{
	}

	public sealed class ClosedHolder<U>
	{
		public sealed class Item
		{
		}
	}

	public sealed class ClosedAnySlot<T> : ClosedSlot<T>
	{
	}

	public sealed class ClosedItemSlot<U> : ClosedSlot<ClosedHolder<U>.Item>
	{
	}

	public sealed class ClosedIntItemSlot : ClosedSlot<ClosedHolder<int>.Item>
	{
	}

	/// <summary>A derived type bound to a tuple type with element names.</summary>
	[STJ.Serialization.JsonPolymorphic(InferClosedTypePolymorphism = true)]
	public closed class ClosedTupleResult<T>
	{
	}

	public sealed class ClosedAnyTuple<T> : ClosedTupleResult<T>
	{
	}

	public sealed class ClosedNamedTuple : ClosedTupleResult<(int A, int B)>
	{
	}

	/// <summary>A derived type with an 'unmanaged' constraint.</summary>
	[STJ.Serialization.JsonPolymorphic(InferClosedTypePolymorphism = true)]
	public closed class ClosedRaw<T>
	{
	}

	public sealed class ClosedRawOnly<T> : ClosedRaw<T> where T : unmanaged
	{
	}

	public sealed class ClosedAnyRaw<T> : ClosedRaw<T>
	{
	}

	public struct ClosedManagedStruct
	{
		public string? Name { get; set; }
	}

	/// <summary>A closed hierarchy declared inside a generic type.</summary>
	public static class ClosedOuter<T>
	{
		[STJ.Serialization.JsonPolymorphic(InferClosedTypePolymorphism = true)]
		public closed class Node
		{
		}

		public sealed class Leaf : Node
		{
			public T? Value { get; set; }
		}
	}

	/// <summary>Two derived types with the same simple name.</summary>
	[STJ.Serialization.JsonPolymorphic(InferClosedTypePolymorphism = true)]
	public closed class ClosedDuplicateNames
	{
	}

	public sealed class DuplicateOuterA : ClosedDuplicateNames
	{
		public sealed class Same : ClosedDuplicateNames { }
	}

	public sealed class DuplicateOuterB : ClosedDuplicateNames
	{
		public sealed class Same : ClosedDuplicateNames { }
	}

	/// <summary>A closed class with no derived type.</summary>
	[STJ.Serialization.JsonPolymorphic(InferClosedTypePolymorphism = true)]
	public closed class ClosedWithoutDerivedTypes
	{
	}

	#endregion

	public partial class CrystalJsonTest
	{

		private static readonly STJ.JsonSerializerOptions StjReflectionOptions = new() { TypeInfoResolver = new STJ.Serialization.Metadata.DefaultJsonTypeInfoResolver() };

		[Test]
		public void Test_ClosedType_Infers_Derived_Types_With_Simple_Name_Discriminators()
		{
			// the discriminator of each derived type is its simple name, and it is written first
			Assert.That(
				CrystalJson.Serialize<ClosedCartEvent>(new CartCreated { CartId = "c1", Owner = "alice" }, CrystalJsonSettings.JsonCompact),
				Does.StartWith("""{"$type":"CartCreated",""")
			);
			Assert.That(
				CrystalJson.Serialize<ClosedCartEvent>(new ItemAdded { CartId = "c1", Sku = "SKU-42", Quantity = 2 }, CrystalJsonSettings.JsonCompact),
				Does.StartWith("""{"$type":"ItemAdded",""")
			);
			Assert.That(
				CrystalJson.Serialize<ClosedCartEvent>(new CartCheckedOut { CartId = "c1", Total = 12.5m }, CrystalJsonSettings.JsonCompact),
				Does.StartWith("""{"$type":"CartCheckedOut",""")
			);
		}

		[Test]
		public void Test_ClosedType_Round_Trip()
		{
			ClosedCartEvent[] events =
			[
				new CartCreated { CartId = "c1", Owner = "alice" },
				new ItemAdded { CartId = "c1", Sku = "SKU-42", Quantity = 2 },
				new CartCheckedOut { CartId = "c1", Total = 12.5m },
			];

			foreach (var evt in events)
			{
				var json = CrystalJson.Serialize<ClosedCartEvent>(evt);
				Log(json);
				var decoded = CrystalJson.Deserialize<ClosedCartEvent>(json);
				Assert.That(decoded, Is.InstanceOf(evt.GetType()));
				Assert.That(CrystalJson.Serialize<ClosedCartEvent>(decoded), Is.EqualTo(json));
			}

			// as members of a container
			var history = new ClosedCartHistory { Last = events[^1], Events = [ ..events ] };
			var text = CrystalJson.Serialize(history, CrystalJsonSettings.JsonCompact);
			Log(text);
			var copy = CrystalJson.Deserialize<ClosedCartHistory>(text);
			using (Assert.EnterMultipleScope())
			{
				Assert.That(copy.Last, Is.InstanceOf<CartCheckedOut>());
				Assert.That(copy.Events, Is.Not.Null.And.Count.EqualTo(3));
				Assert.That(copy.Events![0], Is.InstanceOf<CartCreated>().With.Property(nameof(CartCreated.Owner)).EqualTo("alice"));
				Assert.That(copy.Events![1], Is.InstanceOf<ItemAdded>().With.Property(nameof(ItemAdded.Quantity)).EqualTo(2));
				Assert.That(copy.Events![2], Is.InstanceOf<CartCheckedOut>().With.Property(nameof(CartCheckedOut.Total)).EqualTo(12.5m));
				Assert.That(CrystalJson.Serialize(copy, CrystalJsonSettings.JsonCompact), Is.EqualTo(text));
			}

			// the DOM path writes the same discriminator
			Assert.That(JsonValue.FromValue<ClosedCartEvent>(events[1])["$type"], IsJson.EqualTo("ItemAdded"));
		}

		[Test]
		public void Test_ClosedType_Unknown_Or_Missing_Discriminator_Throws()
		{
			Assert.That(() => CrystalJson.Deserialize<ClosedCartEvent>("""{ "$type": "CartDeleted", "CartId": "c1" }"""), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => CrystalJson.Deserialize<ClosedCartEvent>("""{ "CartId": "c1" }"""), Throws.InstanceOf<JsonBindingException>());
		}

		[Test]
		public void Test_ClosedType_Custom_Discriminator_Property_Name()
		{
			var json = CrystalJson.Serialize<ClosedCommand>(new StopCommand { Force = true }, CrystalJsonSettings.JsonCompact);
			Assert.That(json, Is.EqualTo("""{"kind":"StopCommand","Force":true}"""));
			Assert.That(CrystalJson.Deserialize<ClosedCommand>(json), Is.InstanceOf<StopCommand>().With.Property(nameof(StopCommand.Force)).True);
		}

		[Test]
		public void Test_ClosedType_Nested_Closed_Hierarchy_Is_Followed()
		{
			// the nested closed class is replaced by its own derived types, and gets no discriminator of its own
			Assert.That(CrystalJson.Serialize<ClosedShape>(new ClosedTriangle { Label = "t", Sides = 3, Base = 2 }, CrystalJsonSettings.JsonCompact), Does.StartWith("""{"$type":"ClosedTriangle","""));
			Assert.That(CrystalJson.Serialize<ClosedShape>(new ClosedSquare { Label = "s", Sides = 4, Side = 1 }, CrystalJsonSettings.JsonCompact), Does.StartWith("""{"$type":"ClosedSquare","""));
			Assert.That(CrystalJson.Serialize<ClosedShape>(new ClosedCircle { Label = "c", Radius = 1 }, CrystalJsonSettings.JsonCompact), Does.StartWith("""{"$type":"ClosedCircle","""));

			Assert.That(CrystalJson.Deserialize<ClosedShape>("""{ "$type": "ClosedTriangle", "Sides": 3, "Base": 2 }"""), Is.InstanceOf<ClosedTriangle>().With.Property(nameof(ClosedTriangle.Base)).EqualTo(2));
			Assert.That(() => CrystalJson.Deserialize<ClosedShape>("""{ "$type": "ClosedPolygon", "Sides": 3 }"""), Throws.InstanceOf<JsonBindingException>());

			// a member typed with the nested closed class reads the discriminator of the derived type
			var holder = new ClosedPolygonHolder { Polygon = new ClosedSquare { Label = "s", Sides = 4, Side = 3 } };
			var json = CrystalJson.Serialize(holder, CrystalJsonSettings.JsonCompact);
			Log(json);
			Assert.That(json, Does.Contain("""{"$type":"ClosedSquare","""));
			var copy = CrystalJson.Deserialize<ClosedPolygonHolder>(json);
			Assert.That(copy.Polygon, Is.InstanceOf<ClosedSquare>().With.Property(nameof(ClosedSquare.Side)).EqualTo(3));
		}

		[Test]
		public void Test_ClosedType_Nested_Closed_Type_Rejects_Sibling_Discriminators()
		{
			// a document read as the nested closed type must carry the discriminator of one of ITS derived types
			Assert.That(() => CrystalJson.Deserialize<ClosedPolygon>("""{ "$type": "ClosedCircle", "Radius": 1 }"""), Throws.InstanceOf<JsonBindingException>());
			Assert.That(() => CrystalJson.Deserialize<ClosedPolygonHolder>("""{ "Polygon": { "$type": "ClosedCircle", "Radius": 1 } }"""), Throws.InstanceOf<JsonBindingException>());
			// NOTE: a top-level array binds its items through a reflection call, which wraps the binding exception
			Assert.That(() => CrystalJson.Deserialize<ClosedPolygon[]>("""[ { "$type": "ClosedCircle", "Radius": 1 } ]"""), Throws.InstanceOf<JsonBindingException>().Or.InnerException.InstanceOf<JsonBindingException>());
			Assert.That(() => CrystalJson.Deserialize<ClosedPolygon>("""{ "Sides": 3 }"""), Throws.InstanceOf<JsonBindingException>());

			// control: the discriminators of its own derived types are accepted
			Assert.That(CrystalJson.Deserialize<ClosedPolygon>("""{ "$type": "ClosedSquare", "Side": 2 }"""), Is.InstanceOf<ClosedSquare>());
			Assert.That(CrystalJson.Deserialize<ClosedPolygon[]>("""[ { "$type": "ClosedTriangle" }, { "$type": "ClosedSquare" } ]"""), Has.Length.EqualTo(2));
		}

		[Test]
		public void Test_ClosedType_Generic_Bindings()
		{
			// one type parameter for both type arguments of the base: bound for ClosedPair<int, int>, and no derived type applies to ClosedPair<int, string>
			Assert.That(CrystalJson.Serialize<ClosedPair<int, int>>(new ClosedSamePair<int> { First = 1 }, CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"$type":"ClosedSamePair","First":1}"""));
			Assert.That(CrystalJson.Deserialize<ClosedPair<int, int>>("""{ "$type": "ClosedSamePair", "First": 1 }"""), Is.InstanceOf<ClosedSamePair<int>>());
			Assert.That(() => CrystalJson.Deserialize<ClosedPair<int, string>>("""{ "$type": "ClosedSamePair" }"""), Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("applies to its type arguments"));
			Assert.That(() => STJ.JsonSerializer.Deserialize<ClosedPair<int, string>>("""{ "$type": "ClosedSamePair" }""", StjReflectionOptions), Throws.InstanceOf<InvalidOperationException>());

			// a type parameter bound through an array type argument
			Assert.That(CrystalJson.Serialize<ClosedBag<int[]>>(new ClosedArrayBag<int> { Items = [ 1, 2 ] }, CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"$type":"ClosedArrayBag","Items":[1,2]}"""));
			Assert.That(CrystalJson.Deserialize<ClosedBag<int[]>>("""{ "$type": "ClosedArrayBag", "Items": [ 1, 2 ] }"""), Is.InstanceOf<ClosedArrayBag<int>>());

			// a closed hierarchy declared inside a generic type
			Assert.That(CrystalJson.Serialize<ClosedOuter<int>.Node>(new ClosedOuter<int>.Leaf { Value = 7 }, CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"$type":"Leaf","Value":7}"""));
			Assert.That(CrystalJson.Deserialize<ClosedOuter<int>.Node>("""{ "$type": "Leaf", "Value": 7 }"""), Is.InstanceOf<ClosedOuter<int>.Leaf>());
		}

		[Test]
		public void Test_ClosedType_Explicit_Derived_Types_Replace_Inference()
		{
			Assert.That(CrystalJson.Serialize<ClosedWithExplicitTypes>(new ExplicitLeafA { Value = 1 }, CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"$type":"a","Value":1}"""));
			Assert.That(CrystalJson.Deserialize<ClosedWithExplicitTypes>("""{ "$type": "a", "Value": 1 }"""), Is.InstanceOf<ExplicitLeafA>());

			// ExplicitLeafB is in the closed list, but not in the explicit list
			Assert.That(() => CrystalJson.Serialize<ClosedWithExplicitTypes>(new ExplicitLeafB { Value = 2 }), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => CrystalJson.Deserialize<ClosedWithExplicitTypes>("""{ "$type": "ExplicitLeafB", "Value": 2 }"""), Throws.InstanceOf<JsonBindingException>());
		}

		[Test]
		public void Test_ClosedType_OptIn_On_Type_That_Is_Not_Closed_Throws()
		{
			Assert.That(() => CrystalJson.Serialize<NotClosedBase>(new NotClosedLeaf { Value = 1 }), Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("is not a closed type"));
			Assert.That(() => CrystalJson.Deserialize<NotClosedBase>("""{ "$type": "NotClosedLeaf", "Value": 1 }"""), Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("is not a closed type"));

			// System.Text.Json throws the same way, even when explicit registrations exist
			Assert.That(() => CrystalJson.Serialize<NotClosedExplicitBase>(new NotClosedExplicitLeaf { Value = 1 }), Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("is not a closed type"));
			Assert.That(() => STJ.JsonSerializer.Serialize<NotClosedExplicitBase>(new NotClosedExplicitLeaf { Value = 1 }, StjReflectionOptions), Throws.InstanceOf<InvalidOperationException>());
		}

		[Test]
		public void Test_ClosedType_Without_OptIn_Keeps_Output()
		{
			// a closed class without the opt-in writes the same JSON as an abstract class that is not closed: no discriminator
			Assert.That(
				CrystalJson.Serialize<ClosedWithoutOptIn>(new PlainClosedLeaf { Common = 1, Value = 2 }, CrystalJsonSettings.JsonCompact),
				Is.EqualTo(CrystalJson.Serialize<OpenTwinBase>(new OpenTwinLeaf { Common = 1, Value = 2 }, CrystalJsonSettings.JsonCompact))
			);
			Assert.That(CrystalJson.Serialize<ClosedWithoutOptIn>(new PlainClosedLeaf { Common = 1, Value = 2 }), Does.Not.Contain("$type"));
			Assert.That(CrystalJson.Serialize<ClosedWithoutOptIn>(new PlainClosedLeaf { Common = 1, Value = 2 }), Does.Contain("\"Value\""), "control: the members are written");

			// InferClosedTypePolymorphism = false with no [JsonDerivedType] turns polymorphism off, as in System.Text.Json
			Assert.That(CrystalJson.Serialize<ClosedOptedOut>(new OptedOutLeaf { Value = 2 }, CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"Value":2}"""));
			Assert.That(CrystalJson.Deserialize<OptedOutLeaf>("""{ "Value": 2 }"""), Is.InstanceOf<OptedOutLeaf>().With.Property(nameof(OptedOutLeaf.Value)).EqualTo(2));
			Assert.That(STJ.JsonSerializer.Serialize<ClosedOptedOut>(new OptedOutLeaf { Value = 2 }, StjReflectionOptions), Is.EqualTo("{}"), "System.Text.Json writes the members of the declared type only");
		}

		[Test]
		public void Test_ClosedType_Generic_Hierarchy()
		{
			// a generic definition is closed over the type arguments of the base, and its discriminator drops the arity suffix
			Assert.That(CrystalJson.Serialize<ClosedResult<int>>(new ClosedSuccess<int> { Value = 42 }, CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"$type":"ClosedSuccess","Value":42}"""));
			Assert.That(CrystalJson.Serialize<ClosedResult<int>>(new ClosedIntFailure { Error = "boom" }, CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"$type":"ClosedIntFailure","Error":"boom"}"""));
			Assert.That(CrystalJson.Deserialize<ClosedResult<int>>("""{ "$type": "ClosedSuccess", "Value": 42 }"""), Is.InstanceOf<ClosedSuccess<int>>().With.Property(nameof(ClosedSuccess<>.Value)).EqualTo(42));
			Assert.That(CrystalJson.Deserialize<ClosedResult<int>>("""{ "$type": "ClosedIntFailure", "Error": "boom" }"""), Is.InstanceOf<ClosedIntFailure>());
		}

		/// <summary>Returns the derived types that the runtime path lists for a closed root</summary>
		private static Type[] DerivedTypesOf<TRoot>()
		{
			var resolved = CrystalJson.DefaultResolver.ResolveJsonType(typeof(TRoot));
			Assert.That(resolved, Is.Not.Null);
			return resolved!.DerivedTypeMap?.Values.ToArray() ?? [ ];
		}

		[Test]
		public void Test_ClosedType_Generic_Root_Skips_The_Derived_Types_That_Do_Not_Apply()
		{
			// ClosedIntFailure derives from ClosedResult<int> only, so ClosedResult<string> has one derived type
			Assert.That(CrystalJson.Serialize<ClosedResult<string>>(new ClosedSuccess<string> { Value = "x" }, CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"$type":"ClosedSuccess","Value":"x"}"""));
			Assert.That(CrystalJson.Deserialize<ClosedResult<string>>("""{ "$type": "ClosedSuccess", "Value": "x" }"""), Is.InstanceOf<ClosedSuccess<string>>().With.Property(nameof(ClosedSuccess<>.Value)).EqualTo("x"));
			Assert.That(() => CrystalJson.Deserialize<ClosedResult<string>>("""{ "$type": "ClosedIntFailure", "Error": "boom" }"""), Throws.InstanceOf<JsonBindingException>());

			// ClosedGapB<U> derives from ClosedGap<List<U>> only
			Assert.That(CrystalJson.Serialize<ClosedGap<int>>(new ClosedGapA<int> { Value = 1 }, CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"$type":"ClosedGapA","Value":1}"""));
			Assert.That(() => CrystalJson.Deserialize<ClosedGap<int>>("""{ "$type": "ClosedGapB", "Count": 1 }"""), Throws.InstanceOf<JsonBindingException>());
			Assert.That(CrystalJson.Serialize<ClosedGap<List<int>>>(new ClosedGapB<int> { Count = 2 }, CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"$type":"ClosedGapB","Count":2}"""));
			Assert.That(CrystalJson.Deserialize<ClosedGap<List<int>>>("""{ "$type": "ClosedGapA", "Value": [ 1 ] }"""), Is.InstanceOf<ClosedGapA<List<int>>>());

			// the constraint of ClosedStructBox<T> is 'struct'
			Assert.That(CrystalJson.Serialize<ClosedBox<string>>(new ClosedAnyBox<string> { Value = "x" }, CrystalJsonSettings.JsonCompact), Is.EqualTo("""{"$type":"ClosedAnyBox","Value":"x"}"""));
			Assert.That(() => CrystalJson.Deserialize<ClosedBox<string>>("""{ "$type": "ClosedStructBox" }"""), Throws.InstanceOf<JsonBindingException>());
			Assert.That(CrystalJson.Deserialize<ClosedBox<int>>("""{ "$type": "ClosedStructBox", "Value": 3 }"""), Is.InstanceOf<ClosedStructBox<int>>());

			// the constraint of ClosedUpcast<TFrom, TTo> is 'TFrom : TTo', which holds through variance as well
			Assert.That(CrystalJson.Deserialize<ClosedLink<List<string>, IEnumerable<object>>>("""{ "$type": "ClosedUpcast", "From": [ "a" ] }"""), Is.InstanceOf<ClosedUpcast<List<string>, IEnumerable<object>>>());
			Assert.That(() => CrystalJson.Deserialize<ClosedLink<object, string>>("""{ "$type": "ClosedUpcast" }"""), Throws.InstanceOf<JsonBindingException>());
			Assert.That(CrystalJson.Deserialize<ClosedLink<object, string>>("""{ "$type": "ClosedAnyLink", "To": "x" }"""), Is.InstanceOf<ClosedAnyLink<object, string>>());

			// the type arguments of a containing type are part of the match
			Assert.That(DerivedTypesOf<ClosedSlot<ClosedHolder<int>.Item>>(), Is.EquivalentTo(new[] { typeof(ClosedAnySlot<ClosedHolder<int>.Item>), typeof(ClosedItemSlot<int>), typeof(ClosedIntItemSlot) }));
			Assert.That(DerivedTypesOf<ClosedSlot<ClosedHolder<long>.Item>>(), Is.EquivalentTo(new[] { typeof(ClosedAnySlot<ClosedHolder<long>.Item>), typeof(ClosedItemSlot<long>) }));

			// tuple element names are not part of the type
			Assert.That(DerivedTypesOf<ClosedTupleResult<(int, int)>>(), Is.EquivalentTo(new[] { typeof(ClosedAnyTuple<(int, int)>), typeof(ClosedNamedTuple) }));

			// an 'unmanaged' constraint excludes a struct with a reference field
			Assert.That(DerivedTypesOf<ClosedRaw<ClosedManagedStruct>>(), Is.EquivalentTo(new[] { typeof(ClosedAnyRaw<ClosedManagedStruct>) }));
			Assert.That(DerivedTypesOf<ClosedRaw<int>>(), Is.EquivalentTo(new[] { typeof(ClosedRawOnly<int>), typeof(ClosedAnyRaw<int>) }));

			// control: the same roots work in System.Text.Json 11 when every derived type applies
			Assert.That(STJ.JsonSerializer.Serialize<ClosedResult<int>>(new ClosedSuccess<int> { Value = 1 }, StjReflectionOptions), Is.EqualTo("""{"$type":"ClosedSuccess","Value":1}"""));
			Assert.That(STJ.JsonSerializer.Serialize<ClosedGap<List<int>>>(new ClosedGapB<int> { Count = 2 }, StjReflectionOptions), Is.EqualTo("""{"$type":"ClosedGapB","Count":2}"""));
			Assert.That(STJ.JsonSerializer.Serialize<ClosedBox<int>>(new ClosedAnyBox<int> { Value = 1 }, StjReflectionOptions), Is.EqualTo("""{"$type":"ClosedAnyBox","Value":1}"""));

			// System.Text.Json 11 throws for the whole root as soon as one derived type does not apply. A change in that behavior shows up here.
			Assert.That(() => STJ.JsonSerializer.Serialize<ClosedResult<string>>(new ClosedSuccess<string> { Value = "x" }, StjReflectionOptions), Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("ClosedIntFailure"));
			Assert.That(() => STJ.JsonSerializer.Serialize<ClosedGap<int>>(new ClosedGapA<int> { Value = 1 }, StjReflectionOptions), Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("ClosedGapB"));
			Assert.That(() => STJ.JsonSerializer.Serialize<ClosedBox<string>>(new ClosedAnyBox<string> { Value = "x" }, StjReflectionOptions), Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("ClosedStructBox"));
			Assert.That(() => STJ.JsonSerializer.Serialize<ClosedLink<object, string>>(new ClosedAnyLink<object, string> { To = "x" }, StjReflectionOptions), Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("ClosedUpcast"));
		}

		[Test]
		public void Test_ClosedType_Invalid_Hierarchies_Throw()
		{
			// two derived types with the same simple name
			Assert.That(() => CrystalJson.Serialize<ClosedDuplicateNames>(new DuplicateOuterA()), Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("'Same'"));
			Assert.That(() => STJ.JsonSerializer.Serialize<ClosedDuplicateNames>(new DuplicateOuterA(), StjReflectionOptions), Throws.InstanceOf<InvalidOperationException>());

			// a closed class with no derived type
			Assert.That(() => CrystalJson.Deserialize<ClosedWithoutDerivedTypes>("{ }"), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => STJ.JsonSerializer.Deserialize<ClosedWithoutDerivedTypes>("{ }", StjReflectionOptions), Throws.InstanceOf<InvalidOperationException>());
		}

		[Test]
		public void Test_ClosedType_Parity_With_System_Text_Json()
		{
			// CrystalJson and System.Text.Json write the same JSON for the same closed hierarchy, and read each other's output

			void Check<TBase>(TBase value) where TBase : class
			{
				var crystal = CrystalJson.Serialize<TBase>(value, CrystalJsonSettings.JsonCompact);
				var stj = STJ.JsonSerializer.Serialize<TBase>(value, StjReflectionOptions);
				var name = typeof(TBase).Name;
				Log($"{name}: {crystal}");
				Log($"{new string(' ', name.Length)}  {stj}");

				// same text: same discriminator written first, same members in the same order, same values
				Assert.That(crystal, Is.EqualTo(stj), $"{name}: same JSON text");

				// each side reads the other side's output
				Assert.That(CrystalJson.Deserialize<TBase>(stj), Is.InstanceOf(value.GetType()));
				Assert.That(CrystalJson.Serialize<TBase>(CrystalJson.Deserialize<TBase>(stj), CrystalJsonSettings.JsonCompact), Is.EqualTo(crystal));
				Assert.That(STJ.JsonSerializer.Deserialize<TBase>(crystal, StjReflectionOptions), Is.InstanceOf(value.GetType()));
				Assert.That(STJ.JsonSerializer.Serialize<TBase>(STJ.JsonSerializer.Deserialize<TBase>(crystal, StjReflectionOptions)!, StjReflectionOptions), Is.EqualTo(stj));
			}

			using (Assert.EnterMultipleScope())
			{
				Check<ClosedCartEvent>(new CartCreated { CartId = "c1", Owner = "alice" });
				Check<ClosedCartEvent>(new ItemAdded { CartId = "c1", Sku = "SKU-42", Quantity = 2 });
				Check<ClosedCartEvent>(new CartCheckedOut { CartId = "c1", Total = 12.5m });
				Check<ClosedShape>(new ClosedTriangle { Label = "t", Sides = 3, Base = 2.5 });
				Check<ClosedShape>(new ClosedCircle { Label = "c", Radius = 1.5 });
				Check<ClosedCommand>(new StartCommand { Delay = 5 });
				Check<ClosedWithExplicitTypes>(new ExplicitLeafA { Value = 1 });
				Check<ClosedResult<int>>(new ClosedSuccess<int> { Value = 42 });
				Check<ClosedResult<int>>(new ClosedIntFailure { Error = "boom" });
				Check<ClosedPair<int, int>>(new ClosedSamePair<int> { First = 1 });
				Check<ClosedBag<int[]>>(new ClosedArrayBag<int> { Items = [ 1, 2 ] });
				Check<ClosedOuter<int>.Node>(new ClosedOuter<int>.Leaf { Value = 7 });
			}
		}

	}

}

#endif
