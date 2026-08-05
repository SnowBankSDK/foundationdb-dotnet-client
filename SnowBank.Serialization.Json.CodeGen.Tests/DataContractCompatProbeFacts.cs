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
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;
	using System.Text;

	#region Probe types...

	// note: a [DataContract] DTO cannot be enrolled here: the generator refuses it at build time (error
	// CJSON0014, the interim constraint until generated containers learn the DataContract contract model),
	// pinned by DataContractRefusalDiagnosticFacts through the in-process harness

	public sealed record ProbeDictDto
	{
		public Dictionary<string, int>? Counts { get; set; }
	}

	public sealed record ProbeKvpDto
	{
		public KeyValuePair<string, int> Pair { get; set; }

		public List<KeyValuePair<string, int>>? Pairs { get; set; }
	}

	/// <summary>The acceptance shape for generated [DataContract] support: opt-in membership, a rename, an unannotated
	/// public member that must stay out, a non-public [DataMember] that must come in, and a required-presence member</summary>
	[DataContract]
	public sealed class ProbeContractDto
	{
		[DataMember(Name = "id")]
		public string? Id { get; set; }

		[DataMember(Name = "count")]
		public int Count { get; set; }

		/// <summary>No [DataMember]: excluded by the DataContract model even though it is public</summary>
		public string? NotAMember { get; set; }

		[DataMember(Name = "secret")]
		private string? Secret { get; set; }

		public string? GetSecret() => this.Secret;

		public void SetSecret(string? value) => this.Secret = value;

		[DataMember(Name = "req", IsRequired = true)]
		public string? Req { get; set; }
	}

	/// <summary>Records which lifecycle callbacks fired, so the generated and reflection paths can be compared directly</summary>
	public sealed class ProbeCallbackDto
	{
		[System.Text.Json.Serialization.JsonIgnore]
		public List<string> Trace { get; } = [];

		/// <summary>Set by the pre-populate hook: proves it ran BEFORE the members were written</summary>
		[System.Text.Json.Serialization.JsonIgnore]
		public string? NameSeenByPreHook { get; set; }

		public string? Name { get; set; }

		public int Rank { get; set; }

		[OnSerializing]
		private void BeforeWrite() => this.Trace.Add("OnSerializing");

		[OnSerialized]
		private void AfterWrite() => this.Trace.Add("OnSerialized");

		[OnDeserializing]
		private void BeforeRead()
		{
			this.Trace.Add("OnDeserializing");
			this.NameSeenByPreHook = this.Name;
		}

		[OnDeserialized]
		private void AfterRead(JsonObject document)
		{
			this.Trace.Add("OnDeserialized:" + document.Count);
			this.Trace.Add("sawName:" + this.Name);
		}
	}

	[CrystalJsonConverter]
	[CrystalSerializable(typeof(ProbeDictDto))]
	[CrystalSerializable(typeof(ProbeKvpDto))]
	[CrystalSerializable(typeof(ProbeContractDto))]
	[CrystalSerializable(typeof(ProbeCallbackDto))]
	public static partial class ProbeConverters
	{
		// generated code goes here!
	}

	#endregion

	/// <summary>Probes the DCJS-era wire shapes that the SOURCE-GENERATED path shares with the reflection path (a [DataContract] type itself cannot be enrolled: CJSON0014)</summary>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public sealed class DataContractCompatProbeFacts : SimpleTest
	{

		[Test]
		public void Test_Generated_Converter_Reads_Legacy_Dictionary_Pair_Arrays()
		{
			// generated dictionary reads route through the shared runtime binders, so the tolerance for the
			// DCJS wire shape [ {"Key":..,"Value":..} ] applies to generated converters as well
			var dto = ProbeConverters.ProbeDictDto.Deserialize("""{ "Counts": [ { "Key": "a", "Value": 1 }, { "Key": "b", "Value": 2 } ] }""");
			Assert.That(dto.Counts, Is.EqualTo(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }));

			// and the strictness applies there too
			Assert.That(
				() => ProbeConverters.ProbeDictDto.Deserialize("""{ "Counts": [ { "key": "a", "value": 1 } ] }"""),
				Throws.InstanceOf<JsonBindingException>(), "non-conforming elements must fail through the generated path as well");
		}

		[Test]
		public void Test_Generated_Converter_Reads_The_Legacy_Standalone_Pair_Shape()
		{
			// DCJS spells a STANDALONE pair in lowercase, unlike the "Key"/"Value" of its dictionary pair-array shape;
			// generated pair reads route through the shared runtime binder, so the tolerance reaches them too
			var dto = ProbeConverters.ProbeKvpDto.Deserialize("""{ "Pair": { "key": "k", "value": 7 }, "Pairs": [ { "key": "a", "value": 1 } ] }""");
			Assert.That(dto.Pair, Is.EqualTo(new KeyValuePair<string, int>("k", 7)));
			Assert.That(dto.Pairs, Is.EqualTo(new List<KeyValuePair<string, int>> { new("a", 1) }));

			// the "Key"/"Value" spelling keeps binding
			Assert.That(ProbeConverters.ProbeKvpDto.Deserialize("""{ "Pair": { "Key": "k", "Value": 7 } }""").Pair, Is.EqualTo(new KeyValuePair<string, int>("k", 7)));

			// and an object carrying neither spelling refuses instead of yielding a default-filled pair
			Assert.That(
				() => ProbeConverters.ProbeKvpDto.Deserialize("""{ "Pair": { "foo": 1 } }"""),
				Throws.InstanceOf<JsonBindingException>(), "an unrecognizable pair object must fail through the generated path as well");
		}

		#region The three-way acceptance gate: generated vs reflection vs live DCJS...

		private static string DcjsSerialize<T>(T dto)
		{
			var serializer = new DataContractJsonSerializer(typeof(T));
			using var ms = new MemoryStream();
			serializer.WriteObject(ms, dto);
			return Encoding.UTF8.GetString(ms.ToArray());
		}

		/// <summary>Compares two wires by MEMBERSHIP and VALUES, ignoring member order</summary>
		/// <remarks>Order is deliberately not compared: DCJS emits members alphabetically by wire name, both of our paths
		/// emit declaration order, and matching the runtime path (not DCJS) is the acceptance bar for generated output.</remarks>
		private static void AssertSameMembers(string actual, string expected, string message)
		{
			var a = CrystalJson.Parse(actual).AsObject();
			var b = CrystalJson.Parse(expected).AsObject();
			Assert.That(a.Keys, Is.EquivalentTo(b.Keys), message + " (member set)");
			foreach (var key in b.Keys)
			{
				Assert.That(a[key], Is.EqualTo(b[key]), message + " (member '" + key + "')");
			}
		}

		private static ProbeContractDto MakeContractDto()
		{
			var dto = new ProbeContractDto { Id = "X", Count = 7, NotAMember = "must not appear", Req = "r" };
			dto.SetSecret("s");
			return dto;
		}

		[Test]
		public void Test_Generated_DataContract_Membership_Matches_Reflection_And_Dcjs()
		{
			var dto = MakeContractDto();

			var generated = ProbeConverters.ProbeContractDto.ToJsonText(dto, CrystalJsonSettings.JsonCompact);
			var reflection = CrystalJson.Serialize(dto, CrystalJsonSettings.JsonCompact);
			var dcjs = DcjsSerialize(dto);

			// the bar: same membership, same names, same values as the runtime path, which is what CJSON0014 existed to protect
			AssertSameMembers(generated, reflection, "generated vs the reflection path");

			// ORDER is deliberately excluded from that comparison, and the divergence is pre-existing rather than new:
			// the reflection sweep emits fields before properties and public before non-public (an artifact of how it
			// builds its member list), while generated code follows source declaration order. So a type with a
			// non-public member already gets a different member SEQUENCE from the two paths, independently of
			// [DataContract]. Member ordering is tracked as a both-paths question, alongside the DCJS ordering rule.
			Assert.That(
				CrystalJson.Parse(generated).AsObject().Keys, Is.Not.EqualTo(CrystalJson.Parse(reflection).AsObject().Keys).AsCollection,
				"if this starts matching, the ordering question was settled somewhere and this note should be revisited");

			// and semantically the DataContract model DCJS itself applies: opt-in membership, renames, non-public included
			AssertSameMembers(generated, dcjs, "generated vs the legacy serializer");

			var obj = CrystalJson.Parse(generated).AsObject();
			Assert.That(obj.Keys, Is.EquivalentTo(new[] { "id", "count", "secret", "req" }), "[DataMember] names, and only opted-in members");
			Assert.That(obj["secret"], Is.EqualTo(JsonString.Return("s")), "a non-public [DataMember] is serialized");
			Assert.That(obj.ContainsKey("NotAMember"), Is.False, "a public member without [DataMember] stays out");
		}

		[Test]
		public void Test_Generated_DataContract_Binds_Like_Reflection()
		{
			var wire = DcjsSerialize(MakeContractDto());

			var generated = ProbeConverters.ProbeContractDto.Deserialize(wire);
			var reflection = CrystalJson.Deserialize<ProbeContractDto>(wire)!;

			foreach (var (label, back) in new[] { ("generated", generated), ("reflection", reflection) })
			{
				Assert.That(back.Id, Is.EqualTo("X"), label + ": renamed member binds");
				Assert.That(back.Count, Is.EqualTo(7), label + ": second member binds");
				Assert.That(back.GetSecret(), Is.EqualTo("s"), label + ": non-public [DataMember] binds");
				Assert.That(back.NotAMember, Is.Null, label + ": an unannotated member is never populated");
			}
		}

		[Test]
		public void Test_Generated_DataMember_IsRequired_Demands_Presence_Not_A_Value()
		{
			// DCJS semantics, which the reflection path already implements: an ABSENT member throws, an explicit null satisfies.
			// Deliberately unlike the C# `required` keyword, which refuses null too.
			var withNull = """{"id":"X","count":7,"secret":"s","req":null}""";
			Assert.That(ProbeConverters.ProbeContractDto.Deserialize(withNull).Req, Is.Null, "generated: an explicit null satisfies IsRequired");
			Assert.That(CrystalJson.Deserialize<ProbeContractDto>(withNull)!.Req, Is.Null, "reflection: same");

			var absent = """{"id":"X","count":7,"secret":"s"}""";
			Assert.That(() => CrystalJson.Deserialize<ProbeContractDto>(absent), Throws.InstanceOf<JsonBindingException>(), "reflection: an absent IsRequired member throws");
			Assert.That(() => ProbeConverters.ProbeContractDto.Deserialize(absent), Throws.InstanceOf<JsonBindingException>(), "generated must do the same, or the two paths disagree");
		}

		[Test]
		public void Test_Lifecycle_Callbacks_Fire_Identically_On_Both_Paths()
		{
			// ONE comparison, not two tests that happen to agree: the same type goes through both paths and the
			// traces must match. A generated converter that silently skipped the callbacks would leave the methods
			// inert while looking entirely correct to a reader, which is the trap this test exists to close.
			const string Wire = """{"Name":"n","Rank":7}""";

			// write side
			var generatedWriteDto = new ProbeCallbackDto { Name = "n", Rank = 7 };
			var reflectionWriteDto = new ProbeCallbackDto { Name = "n", Rank = 7 };
			ProbeConverters.ProbeCallbackDto.ToJsonText(generatedWriteDto, CrystalJsonSettings.JsonCompact);
			CrystalJson.Serialize(reflectionWriteDto, CrystalJsonSettings.JsonCompact);
			Assert.That(generatedWriteDto.Trace, Is.EqualTo(reflectionWriteDto.Trace), "write-side callback traces must match between the two paths");
			Assert.That(generatedWriteDto.Trace, Is.EqualTo(new[] { "OnSerializing", "OnSerialized" }), "and both must have actually fired");

			// the DOM route is a second write route, and it must not disagree with the text route
			var generatedPackDto = new ProbeCallbackDto { Name = "n", Rank = 7 };
			ProbeConverters.ProbeCallbackDto.Pack(generatedPackDto, CrystalJsonSettings.JsonCompact);
			Assert.That(generatedPackDto.Trace, Is.EqualTo(generatedWriteDto.Trace), "the generated DOM route must fire the same callbacks as the generated text route");

			// read side
			var generatedRead = ProbeConverters.ProbeCallbackDto.Deserialize(Wire);
			var reflectionRead = CrystalJson.Deserialize<ProbeCallbackDto>(Wire)!;
			Assert.That(generatedRead.Trace, Is.EqualTo(reflectionRead.Trace), "read-side callback traces must match between the two paths");
			Assert.That(generatedRead.Trace, Is.EqualTo(new[] { "OnDeserializing", "OnDeserialized:2", "sawName:n" }), "and both must have actually fired, in order");
		}

		[Test]
		public void Test_The_Callback_Bracket_Is_Exact_On_Both_Paths()
		{
			// the bracket has to be strict, not approximate: the pre-hook runs before the FIRST member is written and
			// the post-hook after the LAST. A latch that opens after the first member has landed does not latch.
			const string Wire = """{"Name":"n","Rank":7}""";

			var generated = ProbeConverters.ProbeCallbackDto.Deserialize(Wire);
			var reflection = CrystalJson.Deserialize<ProbeCallbackDto>(Wire)!;

			foreach (var (label, dto) in new[] { ("generated", generated), ("reflection", reflection) })
			{
				Assert.That(dto.NameSeenByPreHook, Is.Null, label + ": the pre-populate hook must observe an UNPOPULATED instance");
				Assert.That(dto.Name, Is.EqualTo("n"), label + ": and the members are populated afterwards");
				Assert.That(dto.Trace, Does.Contain("sawName:n"), label + ": the post-populate hook must observe the FULLY populated instance");
			}
		}

		[Test]
		public void Test_Proxies_Have_No_Lifecycle_Until_A_Value_Is_Materialised()
		{
			// A proxy is a lazy typed VIEW over the DOM: nothing is constructed and nothing is populated, so there is
			// no interval for a pre/post pair to bracket. Callbacks therefore belong to materialisation, not to
			// viewing. This matters to anyone who has adopted "stay on the proxy" as a principle: a callback will
			// not run for them until they call ToValue().
			var json = CrystalJson.Parse("""{"Name":"n","Rank":7}""");

			var proxy = ProbeConverters.ProbeCallbackDto.ToReadOnly(json);
			Assert.That(proxy.Name, Is.EqualTo("n"), "reading through the proxy works");
			// nothing to assert a trace on: no instance exists yet, which IS the point

			var materialised = proxy.ToValue();
			Assert.That(materialised.Trace, Is.EqualTo(new[] { "OnDeserializing", "OnDeserialized:2", "sawName:n" }), "materialising through ToValue() runs the full read lifecycle");
		}

		#endregion

	}

}
