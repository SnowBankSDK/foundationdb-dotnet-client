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
	using SnowBank.Data;

	/// <summary>An address whose JSON form is a compact array, and not an object with one property per member</summary>
	/// <remarks>The member-based form of this type would be <c>{"Tenant":"acme","Zone":"eu","Node":"n1"}</c>; its own form is <c>["acme","eu","n1"]</c>.</remarks>
	// this type decides its own JSON format on purpose; the generator tests assert the diagnostic
#pragma warning disable CJSON0025
	public sealed record SelfShapedAddress : IJsonPackable, IJsonSerializable, IJsonDeserializable<SelfShapedAddress>
	{

		public string Tenant { get; set; } = "";

		public string Zone { get; set; } = "";

		public string Node { get; set; } = "";

		public JsonArray ToJsonValue() => JsonArray.Create(JsonString.Return(this.Tenant), JsonString.Return(this.Zone), JsonString.Return(this.Node));

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver)
		{
			var arr = this.ToJsonValue();
			return settings.IsReadOnly() ? CrystalJsonMarshall.FreezeTopLevel(arr) : arr;
		}

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => this.ToJsonValue().JsonSerialize(writer);

		/// <inheritdoc />
		public static SelfShapedAddress JsonDeserialize(JsonValue value, ICrystalJsonTypeResolver? resolver)
		{
			if (value is not JsonArray arr || arr.Count != 3) throw JsonBindingException.CannotBindJsonValueToThisType(value, typeof(SelfShapedAddress));
			return new() { Tenant = arr.Get<string>(0), Zone = arr.Get<string>(1), Node = arr.Get<string>(2) };
		}

	}
#pragma warning restore CJSON0025

	/// <summary>A witness summary that hand-rolls all three JSON interfaces, in the shape of the Teleport wire type it stands for</summary>
	/// <remarks>Its own form differs from a member-based converter in three ways at once: the property names are lower-cased, the <see cref="Authority"/> member is written as a compact array, and a format marker that matches no member is added.</remarks>
	// this type decides its own JSON format on purpose; the generator tests assert the diagnostic
#pragma warning disable CJSON0025
	public sealed record SelfShapedWitness : IJsonPackable, IJsonSerializable, IJsonDeserializable<SelfShapedWitness>
	{

		public SelfShapedAddress? Authority { get; set; }

		public string? Health { get; set; }

		public int Hops { get; set; }

		private static class Names
		{
			public const string Authority = "authority";
			public const string Health = "health";
			public const string Hops = "hops";
			public const string Format = "v";
		}

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver)
		{
			var obj = new JsonObject(4);
			obj.Add(Names.Format, JsonNumber.Return(1));
			obj.AddIfNotNull(Names.Authority, this.Authority?.ToJsonValue());
			obj.AddIfNotNull(Names.Health, JsonString.Return(this.Health));
			obj.Add(Names.Hops, JsonNumber.Return(this.Hops));
			return settings.IsReadOnly() ? CrystalJsonMarshall.FreezeTopLevel(obj) : obj;
		}

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => ((IJsonPackable) this).JsonPack(writer.Settings, writer.Resolver).JsonSerialize(writer);

		/// <inheritdoc />
		public static SelfShapedWitness JsonDeserialize(JsonValue value, ICrystalJsonTypeResolver? resolver)
		{
			if (value is not JsonObject obj) throw JsonBindingException.CannotBindJsonValueToThisType(value, typeof(SelfShapedWitness));
			return new()
			{
				Authority = obj.GetValueOrDefault(Names.Authority) is { IsNull: false } authority ? SelfShapedAddress.JsonDeserialize(authority, resolver) : null,
				Health = obj.Get<string?>(Names.Health, null),
				Hops = obj.Get<int>(Names.Hops, 0),
			};
		}

	}
#pragma warning restore CJSON0025

	/// <summary>A plain member-based envelope, registered next to <see cref="SelfShapedWitness"/> so that the witness is reached as a MEMBER of another generated type</summary>
	public sealed record SelfShapedEnvelope
	{

		public string? Id { get; set; }

		public SelfShapedWitness? Witness { get; set; }

	}

	/// <summary>A type that implements ONE facet only: its packed form is an array, while it has no say on how it is written or read</summary>
	// this type decides its own JSON format on purpose; the generator tests assert the diagnostic
#pragma warning disable CJSON0025
	public sealed record SelfShapedPackOnly : IJsonPackable
	{

		public string? Name { get; set; }

		public int Rank { get; set; }

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => JsonArray.Create(JsonString.Return(this.Name), JsonNumber.Return(this.Rank));

	}
#pragma warning restore CJSON0025

	/// <summary>Container registering the hand-rolled types and the envelope that nests one of them</summary>
	[CrystalConverter]
	[CrystalJsonOutput]
	[CrystalSerializable(typeof(SelfShapedWitness))]
	[CrystalSerializable(typeof(SelfShapedEnvelope))]
	[CrystalSerializable(typeof(SelfShapedPackOnly))]
	public static partial class SelfShapedHost { }

	/// <summary>A second container that registers the same witness type, opting out of its own format</summary>
	[CrystalConverter]
	[CrystalJsonOutput]
	[CrystalSerializable(typeof(SelfShapedWitness), IgnoreCustomSerialization = true)]
	public static partial class SelfShapedOptOutHost { }

	/// <summary>Pins that a source-generated container calls a registered type's OWN serialization methods, producing bytes identical to the runtime path</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	[SetInvariantCulture]
	public sealed class CrystalJsonSelfShapedTypeFacts : SimpleTest
	{

		private static SelfShapedWitness MakeWitness() => new()
		{
			Authority = new() { Tenant = "acme", Zone = "eu", Node = "n1" },
			Health = "live",
			Hops = 3,
		};

		[Test]
		public void Test_Container_Serialize_Matches_Runtime_Path()
		{
			var witness = MakeWitness();
			var settings = CrystalJsonSettings.JsonCompact;

			// the runtime path is the oracle: CrystalJsonVisitor defers to IJsonSerializable
			var runtime = CrystalJson.Serialize(witness, settings);
			Log($"runtime:   {runtime}");

			var container = SelfShapedHost.SelfShapedWitness.ToJsonText(witness, settings);
			Log($"container: {container}");

			Assert.That(container, Is.EqualTo(runtime), "the container must call the type's own JsonSerialize, not crawl its members");
		}

		[Test]
		public void Test_Container_Pack_Matches_Runtime_Path()
		{
			var witness = MakeWitness();
			var settings = CrystalJsonSettings.JsonCompact;

			// the runtime path is the oracle: CrystalJsonDomWriter defers to IJsonPackable
			var runtime = JsonValue.FromValue(witness, settings);
			Log($"runtime:   {runtime.ToJsonText(settings)}");

			var container = SelfShapedHost.SelfShapedWitness.Pack(witness, settings);
			Log($"container: {container.ToJsonText(settings)}");

			Assert.That(container.ToJsonText(settings), Is.EqualTo(runtime.ToJsonText(settings)), "the container must call the type's own JsonPack, not crawl its members");
		}

		[Test]
		public void Test_Container_Unpack_Calls_The_Types_Own_Deserializer()
		{
			var witness = MakeWitness();
			var packed = JsonValue.FromValue(witness, CrystalJsonSettings.Json);
			Log($"packed: {packed.ToJsonText(CrystalJsonSettings.JsonCompact)}");

			var decoded = SelfShapedHost.SelfShapedWitness.Unpack(packed);

			using (Assert.EnterMultipleScope())
			{
				Assert.That(decoded.Health, Is.EqualTo("live"), "the container must call the type's own JsonDeserialize, which reads the lower-cased names");
				Assert.That(decoded.Hops, Is.EqualTo(3));
				Assert.That(decoded.Authority, Is.Not.Null, "the address is written as an array, which only the type's own reader understands");
				Assert.That(decoded.Authority!.Tenant, Is.EqualTo("acme"));
			}
		}

		[Test]
		public void Test_Nested_Member_Pack_Routes_Through_The_Context_Entry_Point()
		{
			// the witness is reached as a MEMBER of another generated type, which packs it through
			// Pack(ref CrystalJsonPackContext, ...) rather than through the top-level Pack overload
			var envelope = new SelfShapedEnvelope { Id = "e1", Witness = MakeWitness() };
			var settings = CrystalJsonSettings.JsonCompact;

			var packed = SelfShapedHost.SelfShapedEnvelope.Pack(envelope, settings).AsObject();
			Log($"packed: {packed.ToJsonText(settings)}");

			var nested = packed["Witness"];
			var expected = JsonValue.FromValue(envelope.Witness, settings);

			Assert.That(nested.ToJsonText(settings), Is.EqualTo(expected.ToJsonText(settings)), "the context pack entry point must call the type's own JsonPack too");
		}

		[Test]
		public void Test_Nested_Member_Serialize_Routes_Through_The_Local_Converter()
		{
			var envelope = new SelfShapedEnvelope { Id = "e1", Witness = MakeWitness() };
			var settings = CrystalJsonSettings.JsonCompact;

			var text = SelfShapedHost.SelfShapedEnvelope.ToJsonText(envelope, settings);
			Log($"text: {text}");

			var nested = CrystalJson.Parse(text).AsObject()["Witness"];
			var expected = JsonValue.FromValue(envelope.Witness, settings);

			Assert.That(nested.ToJsonText(settings), Is.EqualTo(expected.ToJsonText(settings)), "a nested member must be written through the type's own JsonSerialize");
		}

		[Test]
		public void Test_Facets_Are_Resolved_Independently()
		{
			var value = new SelfShapedPackOnly { Name = "abc", Rank = 7 };
			var settings = CrystalJsonSettings.JsonCompact;

			var packed = SelfShapedHost.SelfShapedPackOnly.Pack(value, settings);
			var written = SelfShapedHost.SelfShapedPackOnly.ToJsonText(value, settings);
			Log($"packed:  {packed.ToJsonText(settings)}");
			Log($"written: {written}");

			using (Assert.EnterMultipleScope())
			{
				// the type implements IJsonPackable, so the packed form is its own
				Assert.That(packed.ToJsonText(settings), Is.EqualTo("""["abc",7]"""), "the packed form must come from the type's own JsonPack");
				// it implements neither IJsonSerializable nor IJsonDeserializable<T>, so those two facets stay generated
				Assert.That(written, Is.EqualTo("""{"Name":"abc","Rank":7}"""), "the written form must stay a member-based converter");
				Assert.That(SelfShapedHost.SelfShapedPackOnly.Unpack(JsonValue.Parse(written)).Name, Is.EqualTo("abc"), "the read form must stay a member binding");
			}
		}

		[Test]
		public void Test_IgnoreCustomSerialization_Restores_The_Member_Crawl()
		{
			var witness = MakeWitness();
			var settings = CrystalJsonSettings.JsonCompact;

			var optedOut = SelfShapedOptOutHost.SelfShapedWitness.ToJsonText(witness, settings);
			var deferred = SelfShapedHost.SelfShapedWitness.ToJsonText(witness, settings);
			Log($"opted-out: {optedOut}");
			Log($"deferred:  {deferred}");

			using (Assert.EnterMultipleScope())
			{
				Assert.That(optedOut, Does.StartWith("""{"Authority":"""), "the opted-out container must walk the members");
				Assert.That(optedOut, Is.Not.EqualTo(deferred), "the two containers of the same type must disagree, which is what the option is for");
				Assert.That(SelfShapedOptOutHost.SelfShapedWitness.Pack(witness, settings).ToJsonText(settings), Does.StartWith("""{"Authority":"""), "the opt-out covers the packed form too");
				Assert.That(SelfShapedOptOutHost.SelfShapedWitness.Unpack(JsonValue.Parse(optedOut)).Health, Is.EqualTo("live"), "the opt-out covers the read form too");
			}
		}

	}

}
