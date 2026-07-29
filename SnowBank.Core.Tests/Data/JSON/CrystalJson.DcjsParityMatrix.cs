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
	using System.Runtime.Serialization.Json;

	/// <summary>The DataContractJsonSerializer parity matrix: for every DCJS attribute/construct of the legacy migration, pins the wire
	/// the REAL legacy serializer produces (live, in-process; the net472 leg runs the actual .NET Framework DCJS) next to CrystalJson's
	/// wire, and asserts SEMANTIC compatibility in both directions rather than byte equality.</summary>
	/// <remarks>
	/// <para>The bar (owner-ruled): "similar enough that it will work with well-behaved clients". Field names and membership are
	/// respected, ignored members stay ignored, and the two read directions are the mechanical proof: CrystalJson binds the DCJS wire
	/// to the same value (stored data, rolling upgrades), and DCJS binds CrystalJson's compat-mode wire (frozen legacy clients). Byte
	/// differences that a well-behaved parser absorbs (ISO vs <c>\/Date()\/</c>, omitted vs explicit nulls, key order, <c>\/</c>
	/// escaping) are pinned side by side as documentation, not asserted away.</para>
	/// <para>The <c>CjLegacyWire</c> column is the migration recipe: the exact settings that produce a wire a frozen DCJS client reads.</para>
	/// </remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[SetInvariantCulture]
	public sealed class CrystalJsonDcjsParityMatrixFacts : SimpleTest
	{

		#region DCJS oracle helpers...

		private static string DcjsSerialize<T>(T dto)
		{
			var serializer = new DataContractJsonSerializer(typeof(T));
			using var ms = new MemoryStream();
			serializer.WriteObject(ms, dto);
			return System.Text.Encoding.UTF8.GetString(ms.ToArray());
		}

		private static T DcjsDeserialize<T>(string json)
		{
			var serializer = new DataContractJsonSerializer(typeof(T));
			using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
			return (T) serializer.ReadObject(ms)!;
		}

		#endregion

		#region Matrix DTOs...

		[DataContract]
		public sealed record DxRenameDto
		{
			[DataMember(Name = "renamed_id")]
			public string? Id { get; set; }

			// no [DataMember]: excluded by the opt-in rule, on both serializers
			public string? NotAMember { get; set; }
		}

		/// <summary>No [DataContract]: both serializers include public members, and [IgnoreDataMember] excludes one</summary>
		public sealed record DxIgnoredDto
		{
			public int Kept { get; set; }

			[IgnoreDataMember]
			public string? Secret { get; set; }
		}

		[DataContract]
		public sealed record DxOrderDto
		{
			[DataMember(Order = 2)]
			public string? Alpha { get; set; }

			[DataMember]
			public string? Zulu { get; set; }
		}

		[DataContract]
		public sealed record DxDefaultsDto
		{
			[DataMember]
			public int Count { get; set; }

			[DataMember(EmitDefaultValue = false)]
			public int Quantity { get; set; }
		}

		[DataContract]
		public sealed record DxNullsDto
		{
			[DataMember]
			public string? Name { get; set; }

			[DataMember]
			public int Value { get; set; }
		}

		[DataContract]
		public sealed record DxRequiredDto
		{
			[DataMember(IsRequired = true)]
			public string? Id { get; set; }
		}

		[DataContract]
		public sealed record DxDatesDto
		{
			[DataMember]
			public DateTime When { get; set; }
		}

		public enum DxKind
		{
			[EnumMember(Value = "C")]
			Paper = 0,

			[EnumMember(Value = "E")]
			Electronic = 1,
		}

		[DataContract]
		public sealed record DxEnumsDto
		{
			[DataMember]
			public DxKind Kind { get; set; }
		}

		[DataContract]
		public sealed record DxDictDto
		{
			[DataMember]
			public Dictionary<string, int>? Counts { get; set; }

			public bool Equals(DxDictDto? other)
				=> other != null && (this.Counts ?? [ ]).OrderBy(kv => kv.Key).SequenceEqual((other.Counts ?? [ ]).OrderBy(kv => kv.Key));

			public override int GetHashCode() => this.Counts?.Count ?? 0;
		}

		[DataContract]
		public sealed record DxListDto
		{
			[DataMember]
			public List<string>? Tags { get; set; }

			public bool Equals(DxListDto? other)
				=> other != null && (this.Tags ?? [ ]).SequenceEqual(other.Tags ?? [ ]);

			public override int GetHashCode() => this.Tags?.Count ?? 0;
		}

		[DataContract]
		public sealed record DxSlashDto
		{
			[DataMember]
			public string? Path { get; set; }
		}

		[DataContract]
		public sealed record DxPrivateDto
		{
			[DataMember]
			private string? Secret { get; set; }

			[DataMember]
			public int Kept { get; set; }

			public void SetSecret(string? value) => this.Secret = value;

			public string? GetSecret() => this.Secret;
		}

		[DataContract]
		public sealed record DxPrivateIncludedDto
		{
			[DataMember]
			[System.Text.Json.Serialization.JsonInclude]
			private string? Secret { get; set; }

			[DataMember]
			public int Kept { get; set; }

			public void SetSecret(string? value) => this.Secret = value;

			public string? GetSecret() => this.Secret;
		}

		#endregion

		private static readonly CrystalJsonSettings Compact = CrystalJsonSettings.JsonCompact;

		/// <summary>Asserts a case: the live DCJS oracle wire, the CrystalJson wire on both routes, and the two read directions</summary>
		private static void Check<T>(
			T dto,
			string dcjsWire,
			string cjWire,
			CrystalJsonSettings? cjSettings = null,
			string? cjLegacyWire = null,
			CrystalJsonSettings? cjLegacySettings = null,
			Action<T>? verifyCjRead = null,
			Action<T>? verifyDcjsRead = null)
			where T : notnull
		{
			var cj = cjSettings ?? Compact;

			// the ORACLE: what the real DataContractJsonSerializer produces (documentation-grade, pinned inline by the caller)
			Assert.That(DcjsSerialize(dto), Is.EqualTo(dcjsWire), "the DCJS oracle wire drifted");

			// CrystalJson's wire, byte-identical across its own routes
			Assert.That(CrystalJson.Serialize(dto, cj), Is.EqualTo(cjWire), "CrystalJson text route");
			Assert.That(JsonValue.FromValue(dto, cj).ToJsonText(cj), Is.EqualTo(cjWire), "CrystalJson DOM route must agree");

			// the legacy-compat wire, when the default wire is not what a frozen DCJS client can read
			if (cjLegacyWire != null)
			{
				Assert.That(CrystalJson.Serialize(dto, cjLegacySettings ?? Compact), Is.EqualTo(cjLegacyWire), "CrystalJson legacy-compat wire (the migration recipe)");
			}

			// read direction A: CrystalJson binds the wire the legacy serializer produced (stored data, rolling upgrades)
			var fromDcjs = CrystalJson.Deserialize<T>(dcjsWire)!;
			if (verifyCjRead != null) verifyCjRead(fromDcjs);
			else Assert.That(fromDcjs, Is.EqualTo(dto), "CrystalJson must bind the DCJS wire to the same value");

			// read direction B: the legacy serializer binds CrystalJson's (compat) wire (frozen legacy clients)
			var fromCj = DcjsDeserialize<T>(cjLegacyWire ?? cjWire);
			if (verifyDcjsRead != null) verifyDcjsRead(fromCj);
			else Assert.That(fromCj, Is.EqualTo(dto), "the legacy DCJS client must bind CrystalJson's wire to the same value");
		}

		[Test]
		public void Test_DataMember_OptIn_And_Rename()
		{
			// [DataContract] opt-in + [DataMember(Name=...)]: full semantic parity
			Check(
				new DxRenameDto { Id = "X", NotAMember = "n" },
				dcjsWire: """{"renamed_id":"X"}""",
				cjWire: """{"renamed_id":"X"}""",
				// NotAMember cannot round-trip through the wire on either serializer: compare only the contract members
				verifyCjRead: v => Assert.That(v.Id, Is.EqualTo("X")),
				verifyDcjsRead: v => Assert.That(v.Id, Is.EqualTo("X")));
		}

		[Test]
		public void Test_IgnoreDataMember_Is_Honored()
		{
			// [IgnoreDataMember] is DCJS's opt-out on non-[DataContract] types; CrystalJson must exclude the member as well
			Check(
				new DxIgnoredDto { Kept = 1, Secret = "s" },
				dcjsWire: """{"Kept":1}""",
				cjWire: """{"Kept":1}""",
				verifyCjRead: v => { Assert.That(v.Kept, Is.EqualTo(1)); Assert.That(v.Secret, Is.Null, "[IgnoreDataMember] excludes the member from binding as well"); },
				verifyDcjsRead: v => Assert.That(v.Kept, Is.EqualTo(1)));
		}

		[Test]
		public void Test_DataMember_Order_Is_Not_Part_Of_The_Contract()
		{
			// DCJS orders members (no-Order first, then by Order); CrystalJson uses declaration order.
			// The KEYS are what matters; both readers bind either order.
			Check(
				new DxOrderDto { Alpha = "a", Zulu = "z" },
				dcjsWire: """{"Zulu":"z","Alpha":"a"}""",
				cjWire: """{"Alpha":"a","Zulu":"z"}""");
		}

		[Test]
		public void Test_EmitDefaultValue_False()
		{
			// DCJS omits the default; CrystalJson emits it (harmless extra for a well-behaved reader).
			// The modern rewrite is [JsonIgnore(WhenWritingDefault)], per the wave-2 table.
			Check(
				new DxDefaultsDto { Count = 0, Quantity = 0 },
				dcjsWire: """{"Count":0}""",
				cjWire: """{"Count":0,"Quantity":0}""");
		}

		[Test]
		public void Test_Null_Members()
		{
			// DCJS emits explicit nulls; CrystalJson omits them by default. Both readers treat missing and null alike.
			// WithNullMembers() restores byte-level emission for legacy endpoints that want it.
			Check(
				new DxNullsDto { Name = null, Value = 1 },
				dcjsWire: """{"Name":null,"Value":1}""",
				cjWire: """{"Value":1}""",
				cjLegacyWire: """{"Name":null,"Value":1}""",
				cjLegacySettings: Compact.WithNullMembers());
		}

		[Test]
		public void Test_IsRequired_Hazard()
		{
			// the one place where CrystalJson's omit-nulls default can BREAK a legacy client: a [DataMember(IsRequired=true)]
			// member that is null gets omitted, and the legacy DCJS reader THROWS on the missing required member.
			// Recipe: serialize such endpoints with WithNullMembers() (or guarantee the value is present).
			var dto = new DxRequiredDto { Id = null };
			var cjWire = CrystalJson.Serialize(dto, Compact);
			Assert.That(cjWire, Is.EqualTo("{}"), "CrystalJson omits the null member by default");
			Assert.That(() => DcjsDeserialize<DxRequiredDto>(cjWire), Throws.InstanceOf<SerializationException>(),
				"the legacy reader requires the member: this is the documented hazard");

			// the recipe restores compatibility
			var legacyWire = CrystalJson.Serialize(dto, Compact.WithNullMembers());
			Assert.That(legacyWire, Is.EqualTo("""{"Id":null}"""));
			Assert.That(DcjsDeserialize<DxRequiredDto>(legacyWire).Id, Is.Null);
		}

		[Test]
		public void Test_DateTime_Formats()
		{
			// DCJS: the \/Date(ms)\/ epoch form; CrystalJson: ISO 8601 by default, tolerant read of both,
			// WithMicrosoftDates() for frozen legacy readers (which reject ISO).
			var when = new DateTime(2009, 2, 13, 23, 31, 30, DateTimeKind.Utc); // epoch 1234567890000
			Check(
				new DxDatesDto { When = when },
				dcjsWire: """{"When":"\/Date(1234567890000)\/"}""",
				cjWire: """{"When":"2009-02-13T23:31:30Z"}""",
				cjLegacyWire: """{"When":"\/Date(1234567890000)\/"}""",
				cjLegacySettings: Compact.WithMicrosoftDates());
		}

		[Test]
		public void Test_Enums_Are_Numeric_In_Dcjs()
		{
			// DCJS-JSON always writes enums as NUMBERS, and IGNORES [EnumMember(Value=...)] (that attribute only shapes
			// the XML serializer's output). CrystalJson honors the token in its string default; frozen legacy readers
			// need WithEnumAsNumbers().
			Check(
				new DxEnumsDto { Kind = DxKind.Electronic },
				dcjsWire: """{"Kind":1}""",
				cjWire: """{"Kind":"E"}""",
				cjLegacyWire: """{"Kind":1}""",
				cjLegacySettings: Compact.WithEnumAsNumbers());
		}

		[Test]
		public void Test_Dictionaries()
		{
			// DCJS: an array of {"Key":..,"Value":..} pairs; CrystalJson: a JSON object map, with default-on read
			// tolerance for the legacy shape, and WithDictionariesAsPairArrays() for frozen legacy readers.
			Check(
				new DxDictDto { Counts = new() { ["a"] = 1, ["b"] = 2 } },
				dcjsWire: """{"Counts":[{"Key":"a","Value":1},{"Key":"b","Value":2}]}""",
				cjWire: """{"Counts":{"a":1,"b":2}}""",
				cjLegacyWire: """{"Counts":[{"Key":"a","Value":1},{"Key":"b","Value":2}]}""",
				cjLegacySettings: Compact.WithDictionariesAsPairArrays());
		}

		[Test]
		public void Test_Collections()
		{
			// plain arrays/lists: full parity
			Check(
				new DxListDto { Tags = [ "x", "y" ] },
				dcjsWire: """{"Tags":["x","y"]}""",
				cjWire: """{"Tags":["x","y"]}""");
		}

		[Test]
		public void Test_Slash_Escaping()
		{
			// DCJS escapes '/' as '\/' (in every string, not just dates); CrystalJson does not.
			// Both are valid JSON encodings of the same string: semantic parity despite the byte difference.
			Check(
				new DxSlashDto { Path = "a/b" },
				dcjsWire: """{"Path":"a\/b"}""",
				cjWire: """{"Path":"a/b"}""");
		}

		[Test]
		public void Test_NonPublic_DataMember()
		{
			// DCJS serializes a private [DataMember]; CrystalJson requires the explicit [JsonInclude] opt-in
			// (deliberate: no silent wire change). The rewrite is mechanical: keep [DataMember], add [JsonInclude].
			var dto = new DxPrivateDto { Kept = 1 };
			dto.SetSecret("s");
			Assert.That(DcjsSerialize(dto), Is.EqualTo("""{"Kept":1,"Secret":"s"}"""), "the legacy serializer includes the private member");
			Assert.That(CrystalJson.Serialize(dto, Compact), Is.EqualTo("""{"Kept":1}"""), "without [JsonInclude], CrystalJson omits it");

			var included = new DxPrivateIncludedDto { Kept = 1 };
			included.SetSecret("s");
			Assert.That(CrystalJson.Serialize(included, Compact), Is.EqualTo("""{"Kept":1,"Secret":"s"}"""), "[JsonInclude] restores the legacy membership");
			var back = CrystalJson.Deserialize<DxPrivateIncludedDto>("""{"Kept":1,"Secret":"s"}""")!;
			Assert.That(back.GetSecret(), Is.EqualTo("s"), "and the private member binds on read");
		}

	}

}
