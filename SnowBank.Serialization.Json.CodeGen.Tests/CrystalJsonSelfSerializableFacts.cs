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
	using System.ComponentModel.DataAnnotations;

	#region Types...

	/// <summary>Stand-in for a layer attribute (like a document-collection marker) that opts its targets into self-serialization</summary>
	/// <remarks>The JSON generator must not know this attribute: it only recognizes the <see cref="CrystalJsonSelfSerializableAttribute"/> meta-marker it carries.</remarks>
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	[CrystalJsonSelfSerializable]
	public sealed class FancyDocumentAttribute : Attribute
	{

		public FancyDocumentAttribute(string name)
		{
			this.Name = name;
		}

		public string Name { get; }

	}

	/// <summary>Entity that self-serializes: the generated code lives under the entity's single reserved scope (<c>Widget.Json</c>)</summary>
	[FancyDocument("Widgets")]
	public sealed partial record Widget
	{

		/// <summary>Unique id of this widget</summary>
		[Key]
		public required Guid Id { get; init; }

		/// <summary>Display name of this widget</summary>
		public required string Name { get; init; }

		/// <summary>Over 8000?</summary>
		public int Level { get; init; }

		/// <summary>Optional labels</summary>
		public string[]? Tags { get; init; }

		/// <summary>Main part of this widget (referenced type, crawled into the same generated set)</summary>
		public WidgetPart? MainPart { get; init; }

	}

	/// <summary>Plain record referenced by <see cref="Widget"/>: its converters nest inside the entity's reserved scope (<c>Widget.Json.WidgetPart</c>)</summary>
	public sealed record WidgetPart
	{

		public required string Label { get; init; }

		public int Weight { get; init; }

	}

	#endregion

	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public class CrystalJsonSelfSerializableFacts : SimpleTest
	{

		private static Widget MakeSampleWidget() => new()
		{
			Id = Guid.Parse("b6a16abe-e30c-4198-8358-5f0d8fd9c283"),
			Name = "Sprocket",
			Level = 9001,
			Tags = [ "shiny", "new" ],
			MainPart = new() { Label = "Cog", Weight = 42 },
		};

		[Test]
		public void Test_SelfSerializable_Nests_Generated_Code_Under_The_Json_Scope()
		{
			// the entity reserves exactly ONE member name, 'Json': all the generated code lives inside it
			Assert.That(typeof(Widget.Json).DeclaringType, Is.EqualTo(typeof(Widget)));
			Assert.That(typeof(Widget.Json.JsonConverter).DeclaringType, Is.EqualTo(typeof(Widget.Json)));
			Assert.That(typeof(Widget.Json.ReadOnly).DeclaringType, Is.EqualTo(typeof(Widget.Json)));
			Assert.That(typeof(Widget.Json.Writable).DeclaringType, Is.EqualTo(typeof(Widget.Json)));

			// the converter singleton
			Assert.That(Widget.Json.Default, Is.Not.Null.And.InstanceOf<IJsonConverter<Widget>>());

			// property names use the default (General) policy: PascalCase, as-declared
			Assert.That(Widget.Json.PropertyNames.Id, Is.EqualTo("Id"));
			Assert.That(Widget.Json.PropertyNames.Name, Is.EqualTo("Name"));
			Assert.That(Widget.Json.PropertyNames.Level, Is.EqualTo("Level"));

			// the referenced type is crawled into the same generated set, hosted inside the scope with its
			// plain name (inside 'Json' it cannot shadow the referenced type in the entity's own source)
			Assert.That(Widget.Json.WidgetPart.Default, Is.Not.Null.And.InstanceOf<IJsonConverter<WidgetPart>>());
			Assert.That(typeof(Widget.Json.WidgetPart.ReadOnly).DeclaringType?.DeclaringType, Is.EqualTo(typeof(Widget.Json)));
		}

		[Test]
		public void Test_SelfSerializable_Resolver()
		{
			// the scope exposes a resolver that bundles all the converters generated for the entity's object graph
			var resolver = Widget.Json.GetResolver();
			Assert.That(resolver, Is.Not.Null);

			Assert.That(resolver.TryGetConverterFor<Widget>(out var widgetConverter), Is.True);
			Assert.That(widgetConverter, Is.SameAs(Widget.Json.Default));

			Assert.That(resolver.TryGetConverterFor<WidgetPart>(out var partConverter), Is.True);
			Assert.That(partConverter, Is.SameAs(Widget.Json.WidgetPart.Default));
		}

		[Test]
		public void Test_SelfSerializable_RoundTrip()
		{
			var widget = MakeSampleWidget();

			Log("ToJsonText:");
			var json = Widget.Json.ToJsonText(widget);
			Log(json);

			Log("Parse...");
			var parsed = JsonValue.Parse(json);
			Assert.That(parsed, IsJson.Object);
			Assert.Multiple(() =>
			{
				Assert.That(parsed["Id"], IsJson.EqualTo(widget.Id));
				Assert.That(parsed["Name"], IsJson.EqualTo("Sprocket"));
				Assert.That(parsed["Level"], IsJson.EqualTo(9001));
				Assert.That(parsed["Tags"], IsJson.Array.And.EqualTo((string[]) [ "shiny", "new" ]));
				Assert.That(parsed["MainPart"], IsJson.Object);
				Assert.That(parsed["MainPart"]["Label"], IsJson.EqualTo("Cog"));
				Assert.That(parsed["MainPart"]["Weight"], IsJson.EqualTo(42));
			});

			// note: Widget is a record holding an array, so record equality cannot be used (arrays compare by reference)
			Log("Deserialize...");
			var decoded = Widget.Json.Deserialize(json);
			Assert.That(decoded, Is.Not.Null);
			Assert.Multiple(() =>
			{
				Assert.That(decoded.Id, Is.EqualTo(widget.Id));
				Assert.That(decoded.Name, Is.EqualTo(widget.Name));
				Assert.That(decoded.Level, Is.EqualTo(widget.Level));
				Assert.That(decoded.Tags, Is.EqualTo(widget.Tags));
				Assert.That(decoded.MainPart, Is.EqualTo(widget.MainPart));
			});

			Log("Pack/Unpack...");
			var packed = Widget.Json.Default.Pack(widget);
			Assert.That(packed, IsJson.Object);
			var unpacked = Widget.Json.Default.Unpack(packed);
			Assert.Multiple(() =>
			{
				Assert.That(unpacked.Id, Is.EqualTo(widget.Id));
				Assert.That(unpacked.Name, Is.EqualTo(widget.Name));
				Assert.That(unpacked.Level, Is.EqualTo(widget.Level));
				Assert.That(unpacked.Tags, Is.EqualTo(widget.Tags));
				Assert.That(unpacked.MainPart, Is.EqualTo(widget.MainPart));
			});
		}

		[Test]
		public void Test_SelfSerializable_ReadOnly_Proxy()
		{
			var widget = MakeSampleWidget();

			var proxy = Widget.Json.ToReadOnly(widget);
			Log(proxy.ToString());
			Assert.That(proxy.Id, Is.EqualTo(widget.Id));
			Assert.That(proxy.Name, Is.EqualTo("Sprocket"));
			Assert.That(proxy.Level, Is.EqualTo(9001));
			Assert.That(proxy.MainPart.Label, Is.EqualTo("Cog"));
			Assert.That(proxy.ToJsonValue(), IsJson.Object.And.ReadOnly);

			// copy-on-write mutation returns a new frozen proxy, does not touch the original
			var mutated = proxy.With(m =>
			{
				m.Name = "Gizmo";
				Assert.That(m.Name, Is.EqualTo("Gizmo"));
			});
			Assert.That(mutated.Name, Is.EqualTo("Gizmo"));
			Assert.That(proxy.Name, Is.EqualTo("Sprocket"));

			// materialize back into the entity
			var decoded = mutated.ToValue();
			Assert.That(decoded, Is.Not.Null);
			Assert.That(decoded.Name, Is.EqualTo("Gizmo"));
			Assert.That(decoded.Id, Is.EqualTo(widget.Id));
		}

		[Test]
		public void Test_SelfSerializable_Writable_Proxy()
		{
			var widget = MakeSampleWidget();

			var proxy = Widget.Json.ToMutable(widget);
			Assert.That(proxy.Name, Is.EqualTo("Sprocket"));

			proxy.Name = "Doohickey";
			proxy.Level = 123;

			Assert.That(proxy.Name, Is.EqualTo("Doohickey"));
			Assert.That(proxy.Level, Is.EqualTo(123));
			// the original instance is untouched (the proxy wraps a packed copy)
			Assert.That(widget.Name, Is.EqualTo("Sprocket"));

			var decoded = proxy.ToValue();
			Assert.That(decoded.Name, Is.EqualTo("Doohickey"));
			Assert.That(decoded.Level, Is.EqualTo(123));
			Assert.That(decoded.Id, Is.EqualTo(widget.Id));
		}

	}

}
