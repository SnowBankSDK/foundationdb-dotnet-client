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
	using System.Text.Json.Serialization;
	using SnowBank.Data;

	/// <summary>A plain member-shaped type, given a bespoke format by an author-written hook in its container</summary>
	public sealed record HookedCat
	{

		public string? Name { get; set; }

		public int Lives { get; set; }

	}

	/// <summary>Root of a small polymorphic tree, whose derived type is hooked</summary>
	[JsonPolymorphic]
	[JsonDerivedType(typeof(HookedDog), "dog")]
	public abstract record HookedAnimal
	{

		public string? Name { get; set; }

	}

	/// <inheritdoc cref="HookedAnimal" />
	public sealed record HookedDog : HookedAnimal
	{

		public bool Barks { get; set; }

	}

	/// <summary>Container whose per-type scopes carry author-written converter methods</summary>
	/// <remarks>A scope is named after the type it serves, so inside the container that name refers to the SCOPE. Both the enrollment and the hook signatures name the serialized type with a qualified name.</remarks>
	[CrystalConverter]
	[CrystalJsonOutput]
	[CrystalSerializable(typeof(Tests.HookedCat))]
	[CrystalSerializable(typeof(Tests.HookedAnimal))]
	public static partial class HookedHost
	{

		public static partial class HookedCat
		{

			// the bespoke format: a compact array, with the name upper-cased
			public static void Serialize(CrystalJsonWriter writer, Tests.HookedCat? instance) => Pack(instance, writer.Settings, writer.Resolver).JsonSerialize(writer);

			public static JsonValue Pack(Tests.HookedCat? instance, CrystalJsonSettings? settings = default, ICrystalJsonTypeResolver? resolver = default)
				=> instance is null ? JsonNull.Null : JsonArray.Create(JsonString.Return(instance.Name?.ToUpperInvariant()), JsonNumber.Return(instance.Lives));

			public static Tests.HookedCat Unpack(JsonValue value, ICrystalJsonTypeResolver? resolver = default)
			{
				var arr = value.AsArray();
				return new() { Name = arr.Get<string?>(0, null)?.ToLowerInvariant(), Lives = arr.Get<int>(1, 0) };
			}

		}

		public static partial class HookedDog
		{

			// a hooked derived type owns its discriminator: the generator writes none after a hook returns
			public static JsonValue Pack(Tests.HookedDog? instance, CrystalJsonSettings? settings = default, ICrystalJsonTypeResolver? resolver = default)
			{
				if (instance is null) return JsonNull.Null;
				var obj = new JsonObject(3);
				obj[PropertyNames._TypeDiscriminatorProperty_] = PropertyEncodedNames._TypeDiscriminatorValue_;
				obj["woof"] = JsonString.Return(instance.Name);
				obj["loud"] = JsonBoolean.Return(instance.Barks);
				return obj;
			}

		}

	}

	/// <summary>Pins that an author-written method in a container's per-type scope takes over the generated converter, on all three facets and both pack entry points</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	[SetInvariantCulture]
	public sealed class CrystalJsonConverterHookFacts : SimpleTest
	{

		[Test]
		public void Test_Serialize_Hook_Takes_Over()
		{
			var cat = new HookedCat { Name = "felix", Lives = 9 };

			var text = HookedHost.HookedCat.ToJsonText(cat, CrystalJsonSettings.JsonCompact);
			Log($"text: {text}");

			Assert.That(text, Is.EqualTo("""["FELIX",9]"""), "the author's Serialize must replace the member crawl");
		}

		[Test]
		public void Test_Pack_Hook_Takes_Over_On_Both_Entry_Points()
		{
			var cat = new HookedCat { Name = "felix", Lives = 9 };
			var settings = CrystalJsonSettings.JsonCompact;

			// top-level entry point
			var packed = HookedHost.HookedCat.Pack(cat, settings);
			Log($"top-level: {packed.ToJsonText(settings)}");

			// the converter's own IJsonPacker facet, which threads a walk context (what a nested member uses)
			var context = CrystalJsonPackContext.Create(settings, null);
			var nested = HookedHost.HookedCat.Default.Pack(ref context, cat);
			Log($"context:   {nested.ToJsonText(settings)}");

			using (Assert.EnterMultipleScope())
			{
				Assert.That(packed.ToJsonText(settings), Is.EqualTo("""["FELIX",9]"""), "the top-level Pack must route to the author's method");
				Assert.That(nested.ToJsonText(settings), Is.EqualTo("""["FELIX",9]"""), "the context Pack must route to the author's method too");
			}
		}

		[Test]
		public void Test_Unpack_Hook_Takes_Over()
		{
			var decoded = HookedHost.HookedCat.Unpack(JsonValue.Parse("""["FELIX",9]"""));

			using (Assert.EnterMultipleScope())
			{
				Assert.That(decoded.Name, Is.EqualTo("felix"), "the author's Unpack must replace the member binding");
				Assert.That(decoded.Lives, Is.EqualTo(9));
			}
		}

		[Test]
		public void Test_Converter_Still_Answers_The_Interface()
		{
			// the generator keeps implementing IJsonConverter<T>: an author mistake cannot leave the interface unimplemented
			IJsonConverter<HookedCat> converter = HookedHost.HookedCat.Default;
			var cat = new HookedCat { Name = "felix", Lives = 9 };

			var text = CrystalJson.Serialize(cat, converter, CrystalJsonSettings.JsonCompact);
			Log($"text: {text}");

			Assert.That(text, Is.EqualTo("""["FELIX",9]"""), "the interface facet must reach the hook as well");
		}

		[Test]
		public void Test_Polymorphic_Hook_Owns_The_Discriminator()
		{
			HookedAnimal dog = new HookedDog { Name = "rex", Barks = true };
			var settings = CrystalJsonSettings.JsonCompact;

			// packing through the ROOT dispatches to the derived converter, which routes to the hook
			var packed = HookedHost.HookedAnimal.Pack(dog, settings).AsObject();
			Log($"packed: {packed.ToJsonText(settings)}");

			using (Assert.EnterMultipleScope())
			{
				Assert.That(packed.Get<string>("$type"), Is.EqualTo("dog"), "the hook writes the discriminator itself, and the generator adds none");
				Assert.That(packed.Get<string>("woof"), Is.EqualTo("rex"));
				Assert.That(packed.Get<bool>("loud"), Is.True);
				Assert.That(packed.Count, Is.EqualTo(3), "the generator must not inject a second discriminator property");
			}
		}

		[Test]
		public void Test_Unhooked_Facets_Stay_Generated()
		{
			// HookedDog hooks Pack only: its written and read forms stay member-shaped
			var dog = new HookedDog { Name = "rex", Barks = true };

			var text = HookedHost.HookedDog.ToJsonText(dog, CrystalJsonSettings.JsonCompact);
			Log($"text: {text}");

			Assert.That(text, Does.Contain("\"Name\":\"rex\""), "the written form must stay a member crawl");
		}

	}

}
